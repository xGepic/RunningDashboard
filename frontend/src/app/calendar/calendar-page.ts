import { DatePipe, DecimalPipe } from '@angular/common';
import { httpResource } from '@angular/common/http';
import { Component, computed, inject, input, viewChild } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { NgxEchartsDirective } from 'ngx-echarts';
import { weekdays } from '../goal-races/training-plan';
import { Break, Breaks, breakLabel, breakOn, kindLabel } from '../shared/breaks';
import { ChartTheme } from '../shared/chart-theme';
import { dayOf, daysSinceMonday, fullMonthNames, isoDate, today } from '../shared/dates';
import { monthlyDistanceChart, weekdayDistanceChart } from '../shared/charts';
import { formatHoursMinutes } from '../shared/format';
import { Units } from '../shared/units';
import { reloadOnSync } from '../sync/reload-on-sync';
import { RidePage } from '../rides/ride';
import { Run, RunMonths, RunPage } from '../runs/run';
import { SwimPage } from '../swims/swim';
import { BreaksPanel } from './breaks-panel';
import { monthFragments, yearPath } from './calendar-links';
import { YearReviewPanel } from './year-review';

/** All Runs, Rides or Swims of a month's Training Weeks fit on one page. */
const pageSize = 200;

type Sport = 'run' | 'ride' | 'swim';

/** A Run, Ride or Swim in its day, linking to its own page. */
interface CalendarActivity {
  id: number;
  name: string;
  startLocal: string;
  sport: Sport;
  /** In metres, shown under its name; a Swim's in metres as on the Swims page, the others' in the Distance Unit. */
  distance: number;
}

/** The pages Runs, Rides and Swims have, and what a Ride or Swim is called for a screen reader (Runs go without). */
const sports: Record<Sport, { path: string; label: string | null }> = {
  run: { path: '/runs', label: null },
  ride: { path: '/rides', label: 'Ride' },
  swim: { path: '/swims', label: 'Swim' },
};

/** A day of the month view; days of the months around it fill its first and last Training Weeks. */
interface CalendarDay {
  date: Date;
  otherMonth: boolean;
  isToday: boolean;
  /** For the week's distance, which counts Runs only. */
  runs: Run[];
  /** Its Runs, Rides and Swims in the order they started. */
  activities: CalendarActivity[];
  /** The Break the day is in; none on most days. */
  break: Break | null;
  /** Whether the day names its Break: its first day, and the first of it in each Training Week of the grid. */
  namesBreak: boolean;
}

/** A year of Runs as a box per month; a month's fragment (/calendar#Sep) shows that month's Training Weeks instead. */
@Component({
  selector: 'app-calendar-page',
  imports: [BreaksPanel, DatePipe, DecimalPipe, NgxEchartsDirective, RouterLink, YearReviewPanel],
  templateUrl: './calendar-page.html',
  styleUrl: './calendar-page.scss',
})
export class CalendarPage {
  private readonly router = inject(Router);
  private readonly theme = inject(ChartTheme);
  private readonly fragment = toSignal(inject(ActivatedRoute).fragment);
  /** Their days tinted in the month, and listed under it. */
  private readonly breaks = inject(Breaks);

  // From the route and the query string (component input binding).
  /** The year from /calendar/2025; the current year at /calendar or when malformed. */
  readonly year = input<string>();
  /** "review" shows the year's highlights instead of its months (?view=review); a month is always its Training Weeks. */
  readonly view = input<string>();

  protected readonly selectedYear = computed(() => {
    const year = this.year();
    return year && /^\d{4}$/.test(year) ? Number(year) : new Date().getFullYear();
  });
  /** /calendar for the current year, /calendar/2025 for another. */
  protected readonly yearPath = computed(() => yearPath(this.selectedYear()));
  /** Distances and Pace in the Distance Unit from Settings. */
  protected readonly units = inject(Units);

  /** 1 for January, from the fragment; none for the year view. */
  protected readonly selectedMonth = computed(() => {
    const index = monthFragments.indexOf(this.fragment() ?? '');
    return index >= 0 ? index + 1 : null;
  });
  /** Monday of the month's first Training Week to Sunday of its last, as the grid shows them. */
  private readonly gridRange = computed(() => {
    const month = this.selectedMonth();
    if (month === null) return null;
    const first = new Date(this.selectedYear(), month - 1, 1);
    const last = new Date(this.selectedYear(), month, 0);
    return {
      from: new Date(first.getFullYear(), first.getMonth(), first.getDate() - daysSinceMonday(first)),
      to: new Date(last.getFullYear(), last.getMonth(), last.getDate() + 6 - daysSinceMonday(last)),
    };
  });

  protected readonly reviewing = computed(() => this.view() === 'review' && this.selectedMonth() === null);
  /** The Review, which the toolbar's "Download image" saves. */
  protected readonly reviewPanel = viewChild(YearReviewPanel);

  protected readonly isCurrentYear = computed(() => this.selectedYear() >= new Date().getFullYear());

  /** The year's monthly totals. */
  protected readonly months = httpResource<RunMonths>(() => ({ url: '/api/runs/months', params: { year: String(this.selectedYear()) } }));

  /** The month's Training Weeks as the Run, Ride and Swim lists take them, oldest first; none for the year view. */
  private readonly gridParams = computed(() => {
    const range = this.gridRange();
    if (!range) return null;
    return { from: isoDate(range.from), to: isoDate(range.to), pageSize: String(pageSize), sort: 'date', direction: 'asc' };
  });

  /** The Runs of the month's Training Weeks; not loaded for the year view. */
  protected readonly runs = httpResource<RunPage>(() => {
    const params = this.gridParams();
    return params ? { url: '/api/runs', params } : undefined;
  });
  /** The Rides of the month's Training Weeks, shown in their days but counted nowhere. */
  protected readonly rides = httpResource<RidePage>(() => {
    const params = this.gridParams();
    return params ? { url: '/api/rides', params } : undefined;
  });
  /** The Swims of the month's Training Weeks, shown in their days but counted nowhere. */
  protected readonly swims = httpResource<SwimPage>(() => {
    const params = this.gridParams();
    return params ? { url: '/api/swims', params } : undefined;
  });

  protected readonly gridFailed = computed(() => !!(this.runs.error() || this.rides.error() || this.swims.error()));

  /** The grid's Training Weeks, Monday first. */
  protected readonly weeks = computed(() => {
    const range = this.gridRange();
    const month = this.selectedMonth();
    if (!range || month === null || this.gridFailed() || !this.runs.hasValue() || !this.rides.hasValue() || !this.swims.hasValue()) return [];
    const runsByDay = new Map<string, Run[]>();
    for (const run of this.runs.value().items) {
      const key = dayOf(run.startLocal);
      runsByDay.set(key, [...(runsByDay.get(key) ?? []), run]);
    }
    const activitiesByDay = new Map<string, CalendarActivity[]>();
    const activities = [
      ...this.runs.value().items.map((a) => ({ ...a, sport: 'run' as const })),
      ...this.rides.value().items.map((a) => ({ ...a, sport: 'ride' as const })),
      ...this.swims.value().items.map((a) => ({ ...a, sport: 'swim' as const })),
    ].sort((a, b) => a.startLocal.localeCompare(b.startLocal));
    for (const { id, name, startLocal, sport, distance } of activities) {
      const key = dayOf(startLocal);
      activitiesByDay.set(key, [...(activitiesByDay.get(key) ?? []), { id, name, startLocal, sport, distance }]);
    }
    const todayKey = isoDate(today());
    const breaks = this.breaks.list();
    const weeks: CalendarDay[][] = [];
    for (let date = range.from; date <= range.to; date = new Date(date.getFullYear(), date.getMonth(), date.getDate() + 1)) {
      if (daysSinceMonday(date) === 0) weeks.push([]);
      const key = isoDate(date);
      const onBreak = breakOn(breaks, key);
      weeks[weeks.length - 1].push({
        date,
        otherMonth: date.getMonth() !== month - 1,
        isToday: key === todayKey,
        runs: runsByDay.get(key) ?? [],
        activities: activitiesByDay.get(key) ?? [],
        break: onBreak,
        namesBreak: !!onBreak && (onBreak.start === key || daysSinceMonday(date) === 0),
      });
    }
    return weeks.map((days) => ({ start: days[0].date, days, distance: days.flatMap((d) => d.runs).reduce((sum, r) => sum + r.distance, 0) }));
  });

  /** Whether the grid shows a Ride or Swim, for which its legend is there. */
  protected readonly hasListedActivities = computed(() =>
    this.weeks().some((week) => week.days.some((d) => d.activities.some((a) => a.sport !== 'run'))),
  );

  /** The month's own totals, without the days of the months around it; none for the year view. */
  protected readonly monthTotals = computed(() => {
    const month = this.selectedMonth();
    return month !== null && this.months.hasValue() ? (this.months.value().months.find((m) => m.month === month) ?? null) : null;
  });
  /** The days the Breaks panel lists Breaks of: the month's own, or the whole year's. */
  protected readonly breakRange = computed(() => {
    const year = this.selectedYear();
    const month = this.selectedMonth();
    if (month === null) return { from: `${year}-01-01`, to: `${year}-12-31` };
    return { from: isoDate(new Date(year, month - 1, 1)), to: isoDate(new Date(year, month, 0)) };
  });

  /** "September 2026". */
  protected readonly monthTitle = computed(() => {
    const month = this.selectedMonth();
    return month === null ? null : `${fullMonthNames[month - 1]} ${this.selectedYear()}`;
  });

  /** Back to the year of the first Run. */
  protected readonly hasPreviousYear = computed(() => {
    const firstYear = this.months.hasValue() ? this.months.value().firstYear : null;
    return firstYear !== null && this.selectedYear() > firstYear;
  });

  /** January to December; months still ahead have no totals to show. */
  protected readonly boxes = computed(() => {
    if (!this.months.hasValue()) return [];
    const now = new Date();
    const year = this.months.value();
    return year.months.map((m) => ({
      ...m,
      name: fullMonthNames[m.month - 1],
      fragment: monthFragments[m.month - 1],
      future: year.year > now.getFullYear() || (year.year === now.getFullYear() && m.month > now.getMonth() + 1),
      unfinished: year.year === now.getFullYear() && m.month === now.getMonth() + 1,
    }));
  });

  /** The year's Moving Time in whole hours and in days; none until it adds up to more than a day. */
  protected readonly timeRun = computed(() => {
    const seconds = this.months.hasValue() ? this.months.value().months.reduce((sum, m) => sum + m.movingTime, 0) : 0;
    if (seconds <= 86_400) return null;
    return {
      hours: Math.floor(seconds / 3600),
      days: (seconds / 86_400).toFixed(1),
      when: this.isCurrentYear() ? 'this year' : `in ${this.selectedYear()}`,
    };
  });

  /** January of the first year with Runs is as far back as the months go. */
  protected readonly hasPreviousMonth = computed(() => this.hasPreviousYear() || this.selectedMonth() !== 1);
  /** The current month is as far ahead as they go. */
  protected readonly isCurrentMonth = computed(() => {
    const now = new Date();
    return this.selectedYear() * 12 + (this.selectedMonth() ?? 0) >= now.getFullYear() * 12 + now.getMonth() + 1;
  });
  /** "August 2026" and "October 2026" around the month shown, for the arrows. */
  protected readonly neighbourMonths = computed(() => {
    const month = this.selectedMonth() ?? 1;
    const name = (offset: number) => {
      const date = new Date(this.selectedYear(), month - 1 + offset, 1);
      return `${fullMonthNames[date.getMonth()]} ${date.getFullYear()}`;
    };
    return { previous: name(-1), next: name(1) };
  });

  /** The whole year, a point for each month so far, in the unit the boxes show. */
  protected readonly monthlyDistance = computed(() =>
    monthlyDistanceChart(this.selectedYear(), this.boxes(), this.units.unit(), this.theme.colors()),
  );

  /** The year's distance and Runs per day of the week, Monday first; none without Runs in the year. */
  protected readonly weekdayTotals = computed(() => {
    if (!this.months.hasValue()) return [];
    const days = this.months.value().weekdays;
    if (days.every((d) => d.runs === 0)) return [];
    return days.map((d) => ({ ...d, name: weekdays.find((w) => w.day === d.day)!.name }));
  });
  /** "Sundays", the day of the week with the most distance, with its share of the year's; none without Runs. */
  protected readonly mostRunWeekday = computed(() => {
    const days = this.weekdayTotals();
    if (days.length === 0) return null;
    const most = days.reduce((top, d) => (d.distance > top.distance ? d : top));
    const total = days.reduce((sum, d) => sum + d.distance, 0);
    return { name: `${most.name}s`, share: Math.round((100 * most.distance) / total) };
  });
  protected readonly weekdayDistance = computed(() => weekdayDistanceChart(this.weekdayTotals(), this.units.unit(), this.theme.colors()));

  protected readonly weekdayNames = ['Mon', 'Tue', 'Wed', 'Thu', 'Fri', 'Sat', 'Sun'];
  protected readonly formatHoursMinutes = formatHoursMinutes;
  protected readonly sports = sports;
  protected readonly breakLabel = breakLabel;
  protected readonly kindLabel = kindLabel;

  constructor() {
    reloadOnSync(() => {
      this.months.reload();
      this.runs.reload();
      this.rides.reload();
      this.swims.reload();
    });
  }

  /** Months or Review; the year stays. */
  protected showView(view: 'review' | null): void {
    this.router.navigate([], { queryParams: { view }, queryParamsHandling: 'merge' });
  }

  /** The query string stays as it is. */
  protected shiftYear(years: number): void {
    this.router.navigate(yearPath(this.selectedYear() + years), { queryParamsHandling: 'preserve' });
  }

  /** The query string stays as it is; stepping past December or January changes the year. */
  protected shiftMonth(months: number): void {
    const date = new Date(this.selectedYear(), (this.selectedMonth() ?? 1) - 1 + months, 1);
    this.router.navigate(yearPath(date.getFullYear()), { queryParamsHandling: 'preserve', fragment: monthFragments[date.getMonth()] });
  }

  /** A point opens its month, as its box does. */
  protected openChartMonth(index: number): void {
    const box = this.boxes()[index];
    if (box && !box.future) this.router.navigate(this.yearPath(), { queryParamsHandling: 'preserve', fragment: box.fragment });
  }
}

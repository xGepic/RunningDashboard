import { DatePipe } from '@angular/common';
import { httpResource } from '@angular/common/http';
import { Component, computed, inject, input, viewChild } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { NgxEchartsDirective } from 'ngx-echarts';
import { ChartTheme } from '../shared/chart-theme';
import { daysSinceMonday, fullMonthNames, isoDate, today } from '../shared/dates';
import { monthlyDistanceChart } from '../shared/charts';
import { formatHoursMinutes } from '../shared/format';
import { Units } from '../shared/units';
import { reloadOnSync } from '../sync/reload-on-sync';
import { Run, RunMonths, RunPage } from '../runs/run';
import { monthFragments, yearPath } from './calendar-links';
import { YearReviewPanel } from './year-review';

/** All Runs of a month's Training Weeks fit on one page. */
const pageSize = 200;

/** A day of the month view; days of the months around it fill its first and last Training Weeks. */
interface CalendarDay {
  date: Date;
  otherMonth: boolean;
  isToday: boolean;
  runs: Run[];
}

/** A year of Runs as a box per month; a month's fragment (/calendar#Sep) shows that month's Training Weeks instead. */
@Component({
  selector: 'app-calendar-page',
  imports: [DatePipe, NgxEchartsDirective, RouterLink, YearReviewPanel],
  templateUrl: './calendar-page.html',
  styleUrl: './calendar-page.scss',
})
export class CalendarPage {
  private readonly router = inject(Router);
  private readonly theme = inject(ChartTheme);
  private readonly fragment = toSignal(inject(ActivatedRoute).fragment);

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

  /** The Runs of the month's Training Weeks, oldest first; not loaded for the year view. */
  protected readonly runs = httpResource<RunPage>(() => {
    const range = this.gridRange();
    if (!range) return undefined;
    return {
      url: '/api/runs',
      params: {
        from: isoDate(range.from),
        to: isoDate(range.to),
        pageSize: String(pageSize),
        sort: 'date',
        direction: 'asc',
      },
    };
  });

  /** The grid's Training Weeks, Monday first. */
  protected readonly weeks = computed(() => {
    const range = this.gridRange();
    const month = this.selectedMonth();
    if (!range || month === null || !this.runs.hasValue()) return [];
    const runsByDay = new Map<string, Run[]>();
    for (const run of this.runs.value().items) {
      const key = run.startLocal.slice(0, 10);
      runsByDay.set(key, [...(runsByDay.get(key) ?? []), run]);
    }
    const todayKey = isoDate(today());
    const weeks: CalendarDay[][] = [];
    for (let date = range.from; date <= range.to; date = new Date(date.getFullYear(), date.getMonth(), date.getDate() + 1)) {
      if (daysSinceMonday(date) === 0) weeks.push([]);
      const key = isoDate(date);
      weeks[weeks.length - 1].push({ date, otherMonth: date.getMonth() !== month - 1, isToday: key === todayKey, runs: runsByDay.get(key) ?? [] });
    }
    return weeks.map((days) => ({ start: days[0].date, days, distance: days.flatMap((d) => d.runs).reduce((sum, r) => sum + r.distance, 0) }));
  });

  /** The month's own totals, without the days of the months around it; none for the year view. */
  protected readonly monthTotals = computed(() => {
    const month = this.selectedMonth();
    return month !== null && this.months.hasValue() ? (this.months.value().months.find((m) => m.month === month) ?? null) : null;
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

  protected readonly weekdayNames = ['Mon', 'Tue', 'Wed', 'Thu', 'Fri', 'Sat', 'Sun'];
  protected readonly formatHoursMinutes = formatHoursMinutes;

  constructor() {
    reloadOnSync(() => {
      this.months.reload();
      this.runs.reload();
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

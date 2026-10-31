import { DatePipe } from '@angular/common';
import { httpResource } from '@angular/common/http';
import { Component, computed, inject, input } from '@angular/core';
import { RouterLink } from '@angular/router';
import { addDays, currentMonth, dayOf, isoDate, lastDayOf, localDate, mondayOf, monthDate, monthKey, today } from '../shared/dates';
import { formatDuration, formatHoursMinutes, runCount } from '../shared/format';
import { HistoryList, ListParams, requestParams } from '../shared/history-list';
import { SportSwitch } from '../shared/sport-switch';
import { Units } from '../shared/units';
import { Shoe } from '../shoes/shoe';
import { Run, RunPage, RunSortKey } from './run';
import { RunExport, runTable } from './run-export';

/** All Runs of a month fit on one page; the pager only shows up past this. */
const pageSize = 200;

/** A Run this long (metres) or longer gets a Long badge. */
const longRunDistance = 20_000;

@Component({
  selector: 'app-runs-page',
  imports: [DatePipe, RouterLink, SportSwitch],
  templateUrl: './runs-page.html',
  styleUrls: ['../shared/history-list.scss', './runs-page.scss'],
})
export class RunsPage extends HistoryList<RunSortKey, Run> {
  // From the query string (component input binding), besides the sort order and page.
  /** "2026-10"; the current month when missing or malformed. */
  readonly month = input<string>();
  readonly search = input<string>();
  /** "2026-10-05": shows that Training Week instead of a month; any day of it will do. */
  readonly week = input<string>();
  /** A Shoe's id: lists every Run logged with it, of all time, instead of a month or week. */
  readonly shoe = input<string>();

  protected readonly selectedMonth = computed(() => {
    const month = this.month();
    return month && /^\d{4}-(0[1-9]|1[0-2])$/.test(month) ? month : currentMonth();
  });
  protected readonly monthStart = computed(() => monthDate(this.selectedMonth(), 0));
  protected readonly previousMonthStart = computed(() => monthDate(this.selectedMonth(), -1));
  protected readonly nextMonthStart = computed(() => monthDate(this.selectedMonth(), 1));
  protected readonly isCurrentMonth = computed(() => this.selectedMonth() >= currentMonth());

  /** The Monday of the Training Week asked for; null for a month, also when the week is malformed. */
  protected readonly weekStart = computed(() => {
    const week = this.week();
    if (!week || !/^\d{4}-\d{2}-\d{2}$/.test(week) || isoDate(localDate(week)) !== week) return null;
    return mondayOf(localDate(week));
  });
  protected readonly isCurrentWeek = computed(() => this.weekStart()! >= mondayOf(today()));

  /** The days listed: the Training Week asked for, or else the month; none for a Shoe's Runs. */
  protected readonly period = computed(() => {
    if (this.shoe()) return null;
    const monday = this.weekStart();
    return monday ? { from: monday, to: addDays(monday, 6) } : { from: this.monthStart(), to: lastDayOf(this.monthStart()) };
  });

  /** Distances and Pace in the Distance Unit from Settings. */
  protected readonly units = inject(Units);

  /** A month's or Training Week's Runs, or a Shoe's. */
  protected readonly runs = httpResource<RunPage>(() => ({
    url: '/api/runs',
    params: requestParams({
      ...this.periodParams(),
      shoe: this.shoe(),
      search: this.search(),
      sort: this.sort(),
      direction: this.direction(),
      page: this.page(),
      pageSize: String(pageSize),
    }),
  }));
  protected readonly list = this.runs;
  /** Pace starts with the fastest. */
  protected readonly ascendingFirst: readonly RunSortKey[] = ['pace'];
  protected readonly detailPath = '/runs';

  protected readonly searched = computed(() => !!this.search());

  /** Only loaded for a Shoe's Runs, for its name. */
  private readonly shoes = httpResource<Shoe[]>(() => (this.shoe() ? '/api/shoes' : undefined));
  protected readonly shoeName = computed(() => (this.shoes.hasValue() ? this.shoes.value().find((s) => s.id === this.shoe())?.name : undefined));

  protected readonly longRunDistance = longRunDistance;

  /** Totals of the Runs listed: count, distance, Moving Time and their average Pace. */
  protected readonly summary = computed(() => {
    // Read in the page header, outside the template's error check: value() throws while the Runs failed to load.
    const runs = this.runs.hasValue() ? this.runs.value().items : [];
    const distance = runs.reduce((sum, r) => sum + r.distance, 0);
    const movingTime = runs.reduce((sum, r) => sum + r.movingTime, 0);
    return { runs: runs.length, distance, movingTime, pace: distance > 0 ? movingTime / (distance / 1000) : null };
  });

  /**
   * The listed Runs, each with how many listed Runs share its day. Sorted by date, the Runs of a shared day are
   * next to each other: the first one names the day, the others continue it.
   */
  protected readonly rows = computed(() => {
    const items = this.runs.hasValue() ? this.runs.value().items : [];
    const runsPerDay = new Map<string, number>();
    for (const run of items) runsPerDay.set(dayOf(run.startLocal), (runsPerDay.get(dayOf(run.startLocal)) ?? 0) + 1);
    const byDate = this.sortKey() === 'date';
    return items.map((run, i) => ({
      run,
      sameDayRuns: runsPerDay.get(dayOf(run.startLocal))!,
      continuesDay: byDate && i > 0 && dayOf(items[i - 1].startLocal) === dayOf(run.startLocal),
    }));
  });

  protected readonly formatDuration = formatDuration;
  protected readonly formatHoursMinutes = formatHoursMinutes;
  protected readonly runCount = runCount;

  /** A changed search or filter starts again at page 1. */
  protected filter(params: ListParams): void {
    this.navigate({ ...params, page: null });
  }

  /** Steps a month back or forward; the filters and sort order stay. */
  protected shiftMonth(months: number): void {
    this.showMonth(monthKey(monthDate(this.selectedMonth(), months)));
  }

  protected thisMonth(): string {
    return currentMonth();
  }

  /** Back from a Shoe's Runs to the current month's. */
  protected clearShoe(): void {
    this.navigate({ shoe: null, month: null, week: null, page: null });
  }

  /** The current month stays out of the URL, so "/runs" always opens on it. */
  protected showMonth(month: string): void {
    if (!month) return;
    this.navigate({ month: month === currentMonth() ? null : month, week: null, page: null });
  }

  /** Steps a Training Week back or forward; the filters and sort order stay. */
  protected shiftWeek(weeks: number): void {
    this.showWeek(addDays(this.weekStart()!, weeks * 7));
  }

  protected showWeek(monday: Date): void {
    this.navigate({ week: isoDate(monday), month: null, page: null });
  }

  protected thisWeek(): Date {
    return mondayOf(today());
  }

  /** The month the shown Training Week starts in. */
  protected weeksMonth(): string {
    return monthKey(this.weekStart()!);
  }

  /** The month, Training Week or Shoe as listed, search and sort order included, or every Run in the list's sort order. */
  protected exportRuns(scope: 'period' | 'all'): void {
    const order = { sort: this.sort(), direction: this.direction() };
    const params = scope === 'period'
      ? requestParams({ ...this.periodParams(), shoe: this.shoe(), search: this.search(), ...order })
      : requestParams(order);
    this.exportCsv<RunExport>('/api/runs/export', params, this.exportFileName(scope), (runs) => runTable(runs, this.units.unit()));
  }

  /** "runs-2026-09.csv", "runs-week-2026-10-05.csv", "runs-nike-pegasus-41.csv", or "runs-2026-10-12.csv" for all. */
  private exportFileName(scope: 'period' | 'all'): string {
    const monday = this.weekStart();
    const shoe = this.shoe();
    if (scope === 'all') return this.datedFileName('runs');
    if (shoe) return `runs-${slug(this.shoeName() ?? '') || shoe}.csv`;
    return monday ? `runs-week-${isoDate(monday)}.csv` : `runs-${this.selectedMonth()}.csv`;
  }

  /** The listed days as the request's from and to; none for a Shoe's Runs. */
  private periodParams(): { from?: string; to?: string } {
    const period = this.period();
    return period ? { from: isoDate(period.from), to: isoDate(period.to) } : {};
  }

  protected clearSearch(): void {
    this.navigate({ search: null, page: null });
  }
}

/** "nike-pegasus-41" for "Nike Pegasus 41". */
function slug(name: string): string {
  return name.toLowerCase().replace(/[^a-z0-9]+/g, '-').replace(/^-|-$/g, '');
}

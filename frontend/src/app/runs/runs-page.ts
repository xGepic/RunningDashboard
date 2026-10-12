import { DatePipe } from '@angular/common';
import { httpResource } from '@angular/common/http';
import { Component, computed, inject, input } from '@angular/core';
import { Router, RouterLink } from '@angular/router';
import { formatDistance, formatDuration, formatPace } from '../shared/format';
import { reloadOnSync } from '../sync/reload-on-sync';
import { RunMonths, RunPage, RunSortKey } from './run';

/** The list's state, kept in the URL so the back button and shared links work. */
type ListParams = Partial<Record<'view' | 'year' | 'month' | 'search' | 'sportType' | 'sort' | 'direction' | 'page', string | null>>;

/** All Runs of a month fit on one page; the pager only shows up past this. */
const pageSize = 200;

/** The Run types the filter offers, as Strava's sport types; an empty one for all. */
const sportTypes = [
  { value: '', label: 'All Runs' },
  { value: 'Run', label: 'Road' },
  { value: 'TrailRun', label: 'Trail' },
  { value: 'VirtualRun', label: 'Treadmill' },
];

/** A Run this long (metres) or longer gets a Long badge. */
const longRunDistance = 20_000;

@Component({
  selector: 'app-runs-page',
  imports: [DatePipe, RouterLink],
  templateUrl: './runs-page.html',
  styleUrl: './runs-page.scss',
})
export class RunsPage {
  private readonly router = inject(Router);

  // From the query string (component input binding).
  /** "months" for the year's monthly totals; the list of a month's Runs otherwise. */
  readonly view = input<string>();
  /** The year the monthly totals show; the current year when missing or malformed. */
  readonly year = input<string>();
  /** "2026-10"; the current month when missing or malformed. */
  readonly month = input<string>();
  readonly search = input<string>();
  readonly sportType = input<string>();
  readonly sort = input<RunSortKey>();
  readonly direction = input<'asc' | 'desc'>();
  readonly page = input<string>();

  protected readonly selectedMonth = computed(() => {
    const month = this.month();
    return month && /^\d{4}-(0[1-9]|1[0-2])$/.test(month) ? month : currentMonth();
  });
  protected readonly monthStart = computed(() => monthDate(this.selectedMonth(), 0));
  protected readonly previousMonthStart = computed(() => monthDate(this.selectedMonth(), -1));
  protected readonly nextMonthStart = computed(() => monthDate(this.selectedMonth(), 1));
  protected readonly isCurrentMonth = computed(() => this.selectedMonth() >= currentMonth());

  protected readonly monthsView = computed(() => this.view() === 'months');
  protected readonly selectedYear = computed(() => {
    const year = this.year();
    return year && /^\d{4}$/.test(year) ? Number(year) : new Date().getFullYear();
  });
  protected readonly isCurrentYear = computed(() => this.selectedYear() >= new Date().getFullYear());

  protected readonly sortKey = computed(() => this.sort() ?? 'date');
  protected readonly ascending = computed(() => this.direction() === 'asc');

  /** A month's Runs; not loaded while the monthly totals show. */
  protected readonly runs = httpResource<RunPage>(() => this.monthsView() ? undefined : ({
    url: '/api/runs',
    params: Object.fromEntries(
      Object.entries({
        from: isoDate(this.monthStart()),
        to: isoDate(lastDayOf(this.monthStart())),
        search: this.search(),
        sportType: this.sportType(),
        sort: this.sort(),
        direction: this.direction(),
        page: this.page(),
        pageSize: String(pageSize),
      }).filter((entry): entry is [string, string] => !!entry[1]),
    ),
  }));

  /** The year's monthly totals; loaded only while they show. */
  protected readonly months = httpResource<RunMonths>(() => {
    if (!this.monthsView()) return undefined;
    const sportType = this.sportType();
    return { url: '/api/runs/months', params: { year: String(this.selectedYear()), ...(sportType ? { sportType } : {}) } };
  });

  /** The months up to the current one, each with its share of the longest month's distance, for the bars. */
  protected readonly monthRows = computed(() => {
    const year = this.months.hasValue() ? this.months.value() : null;
    if (!year) return [];
    const now = new Date();
    const months = year.months.filter((m) => year.year < now.getFullYear() || m.month <= now.getMonth() + 1);
    const longest = Math.max(1, ...months.map((m) => m.distance));
    return months.map((m) => {
      const start = new Date(year.year, m.month - 1, 1);
      const key = monthKey(start);
      return { ...m, start, share: (m.distance / longest) * 100, monthParam: key === currentMonth() ? null : key };
    });
  });

  protected readonly hasPreviousYear = computed(() => {
    const firstYear = this.months.hasValue() ? this.months.value().firstYear : null;
    return firstYear !== null && this.selectedYear() > firstYear;
  });

  protected readonly pageCount = computed(() => {
    const page = this.runs.value();
    return page ? Math.max(1, Math.ceil(page.totalCount / page.pageSize)) : 1;
  });

  /** The monthly totals filter by type only. */
  protected readonly filtered = computed(() => !!((!this.monthsView() && this.search()) || this.sportType()));

  protected readonly sportTypes = sportTypes;
  protected readonly longRunDistance = longRunDistance;

  /** Totals of the Runs listed, or of the year's months: count, distance, Moving Time and their average Pace. */
  protected readonly summary = computed(() => {
    const parts = this.monthsView()
      ? this.months.hasValue() ? this.months.value().months : []
      : (this.runs.value()?.items ?? []).map((r) => ({ runs: 1, distance: r.distance, movingTime: r.movingTime }));
    const distance = parts.reduce((sum, p) => sum + p.distance, 0);
    const movingTime = parts.reduce((sum, p) => sum + p.movingTime, 0);
    return {
      runs: parts.reduce((sum, p) => sum + p.runs, 0),
      distance,
      hours: Math.floor(movingTime / 3600),
      minutes: String(Math.floor((movingTime % 3600) / 60)).padStart(2, '0'),
      pace: distance > 0 ? movingTime / (distance / 1000) : null,
    };
  });

  /** The longest Run listed, which fills its distance bar. */
  protected readonly longest = computed(() => Math.max(1, ...(this.runs.value()?.items ?? []).map((r) => r.distance)));

  protected readonly formatDistance = formatDistance;
  protected readonly formatDuration = formatDuration;
  protected readonly formatPace = formatPace;

  constructor() {
    reloadOnSync(() => (this.monthsView() ? this.months : this.runs).reload());
  }

  /**
   * Between a month's Runs and the monthly totals of its year, and back to the month the list showed. The search does
   * not apply to the totals, so it is dropped.
   */
  protected showView(months: boolean): void {
    if (months === this.monthsView()) return;
    if (!months) {
      this.navigate({ view: null, year: null });
      return;
    }
    const year = this.monthStart().getFullYear();
    this.navigate({ view: 'months', year: year === new Date().getFullYear() ? null : String(year), search: null, page: null });
  }

  /** The current year stays out of the URL, like the current month. */
  protected shiftYear(years: number): void {
    const year = this.selectedYear() + years;
    this.navigate({ year: year === new Date().getFullYear() ? null : String(year) });
  }

  /** The whole row opens the month's Runs; its name stays a link of its own. */
  protected openMonth(monthParam: string | null, event: MouseEvent): void {
    if ((event.target as HTMLElement).closest('a')) return;
    this.navigate({ view: null, year: null, month: monthParam, page: null });
  }

  /** "12h 05m". */
  protected formatHours(seconds: number): string {
    return `${Math.floor(seconds / 3600)}h ${String(Math.floor((seconds % 3600) / 60)).padStart(2, '0')}m`;
  }

  /** A changed search or filter starts again at page 1. */
  protected filter(params: ListParams): void {
    this.navigate({ ...params, page: null });
  }

  /** Steps a month back or forward; the filters and sort order stay. */
  protected shiftMonth(months: number): void {
    this.showMonth(monthKey(monthDate(this.selectedMonth(), months)));
  }

  /** The current month stays out of the URL, so "/runs" always opens on it. */
  protected showMonth(month: string): void {
    if (!month) return;
    this.navigate({ month: month === currentMonth() ? null : month, page: null });
  }

  /** Clicking the current sort column flips the direction; another column starts with its natural direction. */
  protected sortBy(key: RunSortKey): void {
    const direction = key === this.sortKey() ? (this.ascending() ? 'desc' : 'asc') : key === 'pace' ? 'asc' : 'desc';
    this.navigate({ sort: key === 'date' && direction === 'desc' ? null : key, direction: direction === 'desc' ? null : direction, page: null });
  }

  /** The whole row opens the Run; its name stays a link of its own. */
  protected open(runId: number, event: MouseEvent): void {
    if ((event.target as HTMLElement).closest('a')) return;
    this.router.navigate(['/runs', runId]);
  }

  protected goToPage(page: number): void {
    this.navigate({ page: page > 1 ? String(page) : null });
  }

  protected clearFilters(): void {
    this.navigate({ search: null, sportType: null, page: null });
  }

  protected sortIndicator(key: RunSortKey): string {
    return key === this.sortKey() ? (this.ascending() ? ' ↑' : ' ↓') : '';
  }

  protected ariaSort(key: RunSortKey): 'ascending' | 'descending' | null {
    return key === this.sortKey() ? (this.ascending() ? 'ascending' : 'descending') : null;
  }

  private navigate(params: ListParams): void {
    const queryParams = Object.fromEntries(Object.entries(params).map(([key, value]) => [key, value || null]));
    this.router.navigate([], { queryParams, queryParamsHandling: 'merge' });
  }
}

/** "2026-10" for today's month, in local time. */
function currentMonth(): string {
  return monthKey(new Date());
}

function monthKey(date: Date): string {
  return `${date.getFullYear()}-${String(date.getMonth() + 1).padStart(2, '0')}`;
}

/** The first day of the month `offset` months after `month` ("2026-10"). */
function monthDate(month: string, offset: number): Date {
  const [year, monthNumber] = month.split('-').map(Number);
  return new Date(year, monthNumber - 1 + offset, 1);
}

function lastDayOf(monthStart: Date): Date {
  return new Date(monthStart.getFullYear(), monthStart.getMonth() + 1, 0);
}

/** "2026-10-31", in local time. */
function isoDate(date: Date): string {
  return `${monthKey(date)}-${String(date.getDate()).padStart(2, '0')}`;
}

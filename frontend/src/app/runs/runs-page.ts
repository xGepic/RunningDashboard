import { DatePipe } from '@angular/common';
import { httpResource } from '@angular/common/http';
import { Component, computed, inject, input } from '@angular/core';
import { Router, RouterLink } from '@angular/router';
import { formatDistance, formatDuration, formatPace } from '../shared/format';
import { reloadOnSync } from '../sync/reload-on-sync';
import { RunPage, RunSortKey } from './run';

/** The list's state, kept in the URL so the back button and shared links work. */
type ListParams = Partial<Record<'month' | 'search' | 'sportType' | 'sort' | 'direction' | 'page', string | null>>;

/** All Runs of a month fit on one page; the pager only shows up past this. */
const pageSize = 200;

@Component({
  selector: 'app-runs-page',
  imports: [DatePipe, RouterLink],
  templateUrl: './runs-page.html',
  styleUrl: './runs-page.scss',
})
export class RunsPage {
  private readonly router = inject(Router);

  // From the query string (component input binding).
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

  protected readonly sortKey = computed(() => this.sort() ?? 'date');
  protected readonly ascending = computed(() => this.direction() === 'asc');

  protected readonly runs = httpResource<RunPage>(() => ({
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

  protected readonly pageCount = computed(() => {
    const page = this.runs.value();
    return page ? Math.max(1, Math.ceil(page.totalCount / page.pageSize)) : 1;
  });

  protected readonly filtered = computed(() => !!(this.search() || this.sportType()));

  protected readonly formatDistance = formatDistance;
  protected readonly formatDuration = formatDuration;
  protected readonly formatPace = formatPace;

  constructor() {
    reloadOnSync(() => this.runs.reload());
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

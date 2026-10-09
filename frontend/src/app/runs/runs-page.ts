import { DatePipe } from '@angular/common';
import { httpResource } from '@angular/common/http';
import { Component, computed, inject, input } from '@angular/core';
import { Router, RouterLink } from '@angular/router';
import { formatDistance, formatDuration, formatPace } from '../shared/format';
import { reloadOnSync } from '../sync/reload-on-sync';
import { RunPage, RunSortKey } from './run';

/** The list's state, kept in the URL so the back button and shared links work. */
type ListParams = Partial<Record<'search' | 'from' | 'to' | 'sportType' | 'sort' | 'direction' | 'page', string | null>>;

@Component({
  selector: 'app-runs-page',
  imports: [DatePipe, RouterLink],
  templateUrl: './runs-page.html',
  styleUrl: './runs-page.scss',
})
export class RunsPage {
  private readonly router = inject(Router);

  // From the query string (component input binding).
  readonly search = input<string>();
  readonly from = input<string>();
  readonly to = input<string>();
  readonly sportType = input<string>();
  readonly sort = input<RunSortKey>();
  readonly direction = input<'asc' | 'desc'>();
  readonly page = input<string>();

  protected readonly sortKey = computed(() => this.sort() ?? 'date');
  protected readonly ascending = computed(() => this.direction() === 'asc');

  protected readonly runs = httpResource<RunPage>(() => ({
    url: '/api/runs',
    params: Object.fromEntries(
      Object.entries({
        search: this.search(),
        from: this.from(),
        to: this.to(),
        sportType: this.sportType(),
        sort: this.sort(),
        direction: this.direction(),
        page: this.page(),
      }).filter((entry): entry is [string, string] => !!entry[1]),
    ),
  }));

  protected readonly pageCount = computed(() => {
    const page = this.runs.value();
    return page ? Math.max(1, Math.ceil(page.totalCount / page.pageSize)) : 1;
  });

  protected readonly filtered = computed(() => !!(this.search() || this.from() || this.to() || this.sportType()));

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

  /** Clicking the current sort column flips the direction; another column starts with its natural direction. */
  protected sortBy(key: RunSortKey): void {
    const direction = key === this.sortKey() ? (this.ascending() ? 'desc' : 'asc') : key === 'pace' ? 'asc' : 'desc';
    this.navigate({ sort: key === 'date' && direction === 'desc' ? null : key, direction: direction === 'desc' ? null : direction, page: null });
  }

  protected goToPage(page: number): void {
    this.navigate({ page: page > 1 ? String(page) : null });
  }

  protected clearFilters(): void {
    this.navigate({ search: null, from: null, to: null, sportType: null, page: null });
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

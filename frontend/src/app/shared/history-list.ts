import { HttpResourceRef } from '@angular/common/http';
import { Directive, computed, inject, input } from '@angular/core';
import { Router } from '@angular/router';
import { reloadOnSync } from '../sync/reload-on-sync';

/** One page of a history, as GET /api/runs, /api/rides and /api/swims return it. */
export interface ListPage<Item> {
  items: Item[];
  /** 1-based. */
  page: number;
  pageSize: number;
  totalCount: number;
}

/** The list's state, kept in the URL so the back button and shared links work; null or empty drops a parameter. */
export type ListParams = Partial<Record<string, string | null>>;

/** The request's query parameters: those without a value are left out. */
export function requestParams(params: Record<string, string | null | undefined>): Record<string, string> {
  return Object.fromEntries(Object.entries(params).filter((entry): entry is [string, string] => !!entry[1]));
}

/**
 * What Run, Ride and Swim history share: the sort order and page from the query string, sorting by a column, the
 * pager, and the distance bars. Newest first unless the URL says otherwise; reloaded as a sync brings activities.
 */
@Directive()
export abstract class HistoryList<SortKey extends string, Item extends { distance: number }> {
  protected readonly router = inject(Router);

  // From the query string (component input binding).
  readonly sort = input<SortKey>();
  readonly direction = input<'asc' | 'desc'>();
  readonly page = input<string>();

  /** The page listed. */
  protected abstract readonly list: HttpResourceRef<ListPage<Item> | undefined>;

  /** Columns whose first click sorts ascending, such as Pace with the fastest first; the others start with the biggest. */
  protected abstract readonly ascendingFirst: readonly SortKey[];

  protected readonly sortKey = computed(() => this.sort() ?? 'date');
  protected readonly ascending = computed(() => this.direction() === 'asc');

  protected readonly pageCount = computed(() => {
    if (!this.list.hasValue()) return 1;
    const page = this.list.value();
    return Math.max(1, Math.ceil(page.totalCount / page.pageSize));
  });

  /** The longest listed, which fills its distance bar. */
  protected readonly longest = computed(() => Math.max(1, ...(this.list.hasValue() ? this.list.value().items : []).map((i) => i.distance)));

  constructor() {
    // Only called once the page is up, when the subclass has set the list.
    reloadOnSync(() => this.list.reload());
  }

  /** Clicking the current sort column flips the direction; another column starts with its natural direction. */
  protected sortBy(key: SortKey): void {
    const direction = key === this.sortKey() ? (this.ascending() ? 'desc' : 'asc') : this.ascendingFirst.includes(key) ? 'asc' : 'desc';
    this.navigate({ sort: key === 'date' && direction === 'desc' ? null : key, direction: direction === 'desc' ? null : direction, page: null });
  }

  protected goToPage(page: number): void {
    this.navigate({ page: page > 1 ? String(page) : null });
  }

  protected sortIndicator(key: SortKey): string {
    return key === this.sortKey() ? (this.ascending() ? ' ↑' : ' ↓') : '';
  }

  protected ariaSort(key: SortKey): 'ascending' | 'descending' | null {
    return key === this.sortKey() ? (this.ascending() ? 'ascending' : 'descending') : null;
  }

  protected navigate(params: ListParams): void {
    const queryParams = Object.fromEntries(Object.entries(params).map(([key, value]) => [key, value || null]));
    this.router.navigate([], { queryParams, queryParamsHandling: 'merge' });
  }
}

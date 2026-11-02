import { DOCUMENT } from '@angular/common';
import { HttpClient, HttpResourceRef } from '@angular/common/http';
import { Directive, computed, inject, input, signal } from '@angular/core';
import { Router } from '@angular/router';
import { reloadOnSync } from '../sync/reload-on-sync';
import { CsvTable, saveCsv } from './csv';
import { currentYear, isoDate, today } from './dates';
import { openUnlessLink } from './open-unless-link';

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

/** One page of a history listed a year at a time, as GET /api/rides and /api/swims return it. */
export interface YearPage<Item> extends ListPage<Item> {
  /** Every year with an item of the history, newest first, whatever is asked for. */
  years: number[];
}

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
  private readonly http = inject(HttpClient);
  private readonly document = inject(DOCUMENT);

  // From the query string (component input binding).
  readonly sort = input<SortKey>();
  readonly direction = input<'asc' | 'desc'>();
  readonly page = input<string>();

  /** The page listed. */
  protected abstract readonly list: HttpResourceRef<ListPage<Item> | undefined>;

  /** Columns whose first click sorts ascending, such as Pace with the fastest first; the others start with the biggest. */
  protected abstract readonly ascendingFirst: readonly SortKey[];

  /** Where an item's own page is, followed by its id: "/runs". */
  protected abstract readonly detailPath: string;

  protected readonly sortKey = computed(() => this.sort() ?? 'date');
  protected readonly ascending = computed(() => this.direction() === 'asc');

  protected readonly pageCount = computed(() => {
    if (!this.list.hasValue()) return 1;
    const page = this.list.value();
    return Math.max(1, Math.ceil(page.totalCount / page.pageSize));
  });

  /** The longest listed, which fills its distance bar. */
  protected readonly longest = computed(() => Math.max(1, ...(this.list.hasValue() ? this.list.value().items : []).map((i) => i.distance)));

  /** While an export is being fetched. */
  protected readonly exporting = signal(false);
  protected readonly exportError = signal<string | null>(null);

  constructor() {
    // Only called once the page is up, when the subclass has set the list.
    reloadOnSync(() => this.list.reload());
  }

  /** Saves every row of the export `url` the `params` ask for (search, date range, sort order) as `fileName`. */
  protected exportCsv<Row>(url: string, params: Record<string, string>, fileName: string, table: (rows: Row[]) => CsvTable): void {
    this.exporting.set(true);
    this.exportError.set(null);
    this.http.get<Row[]>(url, { params }).subscribe({
      next: (rows) => {
        this.exporting.set(false);
        saveCsv(this.document, fileName, table(rows));
      },
      error: () => {
        this.exporting.set(false);
        this.exportError.set('The export could not be made. Try again.');
      },
    });
  }

  /** "runs-2026-10-14.csv": what is exported, and when. */
  protected datedFileName(name: string): string {
    return `${name}-${isoDate(today())}.csv`;
  }

  /** Clicking the current sort column flips the direction; another column starts with its natural direction. */
  protected sortBy(key: SortKey): void {
    const direction = key === this.sortKey() ? (this.ascending() ? 'desc' : 'asc') : this.ascendingFirst.includes(key) ? 'asc' : 'desc';
    this.navigate({ sort: key === 'date' && direction === 'desc' ? null : key, direction: direction === 'desc' ? null : direction, page: null });
  }

  /** The whole row opens the item's page; a link in it, such as its name, stays a link of its own. */
  protected open(id: number, event: MouseEvent): void {
    openUnlessLink(event, this.router, [this.detailPath, id]);
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

/**
 * What Ride and Swim history add: they list a year at a time, the current one unless the URL says otherwise, stepped
 * through with ← and → as the Runs step through months.
 */
@Directive()
export abstract class YearlyHistoryList<SortKey extends string, Item extends { distance: number }> extends HistoryList<SortKey, Item> {
  /** From the query string: "2025"; the current year when missing or malformed. */
  readonly year = input<string>();

  protected abstract override readonly list: HttpResourceRef<YearPage<Item> | undefined>;

  protected readonly selectedYear = computed(() => {
    const year = this.year();
    return year && /^\d{4}$/.test(year) ? Number(year) : currentYear();
  });
  protected readonly isCurrentYear = computed(() => this.selectedYear() >= currentYear());

  /** The years with items; none until they are in. */
  private readonly years = computed(() => (this.list.hasValue() ? this.list.value().years : []));

  /** ← stops at the first year with items. */
  protected readonly isFirstYear = computed(() => this.years().length > 0 && this.selectedYear() <= this.years().at(-1)!);

  /** The newest year with items, to go to from a year without any; none when that is the year listed. */
  protected readonly latestYear = computed(() => {
    const latest = this.years()[0];
    return latest !== undefined && latest !== this.selectedYear() ? latest : null;
  });

  /** The year's days as the request's from and to. */
  protected yearParams(): { from: string; to: string } {
    return { from: `${this.selectedYear()}-01-01`, to: `${this.selectedYear()}-12-31` };
  }

  /** Steps a year back or forward; the filters and sort order stay. */
  protected shiftYear(years: number): void {
    this.showYear(this.selectedYear() + years);
  }

  protected thisYear(): number {
    return currentYear();
  }

  /** The current year stays out of the URL, so the page always opens on it. */
  protected showYear(year: number): void {
    this.navigate({ year: year === currentYear() ? null : String(year), page: null });
  }

  /** "rides-2025.csv" for the year listed, or "rides-2026-10-14.csv", named after today, for all of them. */
  protected exportFileName(name: string, scope: 'year' | 'all'): string {
    return scope === 'all' ? this.datedFileName(name) : `${name}-${this.selectedYear()}.csv`;
  }
}

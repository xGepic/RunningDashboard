import { DatePipe, DecimalPipe } from '@angular/common';
import { httpResource } from '@angular/common/http';
import { Component, computed, inject, input } from '@angular/core';
import { Router, RouterLink } from '@angular/router';
import { formatDistance, formatDuration, formatHoursMinutes, formatSwimPace } from '../shared/format';
import { SportSwitch } from '../shared/sport-switch';
import { reloadOnSync } from '../sync/reload-on-sync';
import { SwimPage, SwimSortKey } from './swim';

/** The list's state, kept in the URL so the back button and shared links work. */
type ListParams = Partial<Record<'sort' | 'direction' | 'page', string | null>>;

/** Swims per page. */
const pageSize = 10;

/** Swim history: like Ride history, all Swims ten a page, with Swim Pace per 100 m and distances in metres. */
@Component({
  selector: 'app-swims-page',
  imports: [DatePipe, DecimalPipe, RouterLink, SportSwitch],
  templateUrl: './swims-page.html',
  // The Runs list's look, so all histories match; a Swim, like a Ride, has no page of its own.
  styleUrls: ['../runs/runs-page.scss', '../rides/rides-page.scss'],
})
export class SwimsPage {
  private readonly router = inject(Router);

  // From the query string (component input binding).
  readonly sort = input<SwimSortKey>();
  readonly direction = input<'asc' | 'desc'>();
  readonly page = input<string>();

  protected readonly sortKey = computed(() => this.sort() ?? 'date');
  protected readonly ascending = computed(() => this.direction() === 'asc');

  /** A page of all Swims, with the totals of all of them. */
  protected readonly swims = httpResource<SwimPage>(() => ({
    url: '/api/swims',
    params: Object.fromEntries(
      Object.entries({
        sort: this.sort(),
        direction: this.direction(),
        page: this.page(),
        pageSize: String(pageSize),
      }).filter((entry): entry is [string, string] => !!entry[1]),
    ),
  }));

  /** Of all Swims, for the page header; none until they are in, or when they could not be loaded. */
  protected readonly totals = computed(() => (this.swims.hasValue() ? this.swims.value().totals : null));

  protected readonly pageCount = computed(() => {
    const page = this.swims.value();
    return page ? Math.max(1, Math.ceil(page.totalCount / page.pageSize)) : 1;
  });

  /** The longest Swim listed, which fills its distance bar. */
  protected readonly longest = computed(() => Math.max(1, ...(this.swims.value()?.items ?? []).map((s) => s.distance)));

  protected readonly formatDistance = formatDistance;
  protected readonly formatDuration = formatDuration;
  protected readonly formatHoursMinutes = formatHoursMinutes;
  protected readonly formatSwimPace = formatSwimPace;

  constructor() {
    reloadOnSync(() => this.swims.reload());
  }

  /** Clicking the current sort column flips the direction; another column starts with the biggest first, Swim Pace with the fastest. */
  protected sortBy(key: SwimSortKey): void {
    const direction = key === this.sortKey() ? (this.ascending() ? 'desc' : 'asc') : key === 'pace' ? 'asc' : 'desc';
    this.navigate({ sort: key === 'date' && direction === 'desc' ? null : key, direction: direction === 'desc' ? null : direction, page: null });
  }

  protected goToPage(page: number): void {
    this.navigate({ page: page > 1 ? String(page) : null });
  }

  protected sortIndicator(key: SwimSortKey): string {
    return key === this.sortKey() ? (this.ascending() ? ' ↑' : ' ↓') : '';
  }

  protected ariaSort(key: SwimSortKey): 'ascending' | 'descending' | null {
    return key === this.sortKey() ? (this.ascending() ? 'ascending' : 'descending') : null;
  }

  private navigate(params: ListParams): void {
    const queryParams = Object.fromEntries(Object.entries(params).map(([key, value]) => [key, value || null]));
    this.router.navigate([], { queryParams, queryParamsHandling: 'merge' });
  }
}

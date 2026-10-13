import { DatePipe, DecimalPipe } from '@angular/common';
import { httpResource } from '@angular/common/http';
import { Component, computed, inject, input } from '@angular/core';
import { Router, RouterLink } from '@angular/router';
import { formatDuration, formatHoursMinutes } from '../shared/format';
import { SportSwitch } from '../shared/sport-switch';
import { Units } from '../shared/units';
import { reloadOnSync } from '../sync/reload-on-sync';
import { Ride, RidePage, RideSortKey } from './ride';

/** The list's state, kept in the URL so the back button and shared links work. */
type ListParams = Partial<Record<'sort' | 'direction' | 'page', string | null>>;

/** Rides per page. */
const pageSize = 10;

/** The badge each kind of Ride gets, by Strava's sport type. */
const kinds: Record<string, { label: string; tone: string }> = {
  Ride: { label: 'Road', tone: '' },
  VirtualRide: { label: 'Virtual', tone: 'muted' },
  GravelRide: { label: 'Gravel', tone: 'amber' },
  MountainBikeRide: { label: 'MTB', tone: 'green' },
};

/** Ride history: like Run history, but all Rides, ten a page, without filters or a page per Ride. */
@Component({
  selector: 'app-rides-page',
  imports: [DatePipe, DecimalPipe, RouterLink, SportSwitch],
  templateUrl: './rides-page.html',
  // The Runs list's look, so both histories match.
  styleUrls: ['../runs/runs-page.scss', './rides-page.scss'],
})
export class RidesPage {
  private readonly router = inject(Router);

  // From the query string (component input binding).
  readonly sort = input<RideSortKey>();
  readonly direction = input<'asc' | 'desc'>();
  readonly page = input<string>();

  protected readonly sortKey = computed(() => this.sort() ?? 'date');
  protected readonly ascending = computed(() => this.direction() === 'asc');

  /** A page of all Rides, with the totals of all of them. */
  protected readonly rides = httpResource<RidePage>(() => ({
    url: '/api/rides',
    params: Object.fromEntries(
      Object.entries({
        sort: this.sort(),
        direction: this.direction(),
        page: this.page(),
        pageSize: String(pageSize),
      }).filter((entry): entry is [string, string] => !!entry[1]),
    ),
  }));

  /** Of all Rides, for the page header; none until they are in, or when they could not be loaded. */
  protected readonly totals = computed(() => (this.rides.hasValue() ? this.rides.value().totals : null));

  protected readonly pageCount = computed(() => {
    const page = this.rides.value();
    return page ? Math.max(1, Math.ceil(page.totalCount / page.pageSize)) : 1;
  });

  /** The longest Ride listed, which fills its distance bar. */
  protected readonly longest = computed(() => Math.max(1, ...(this.rides.value()?.items ?? []).map((r) => r.distance)));

  protected readonly formatDuration = formatDuration;
  protected readonly formatHoursMinutes = formatHoursMinutes;
  /** Distances and Avg Speed (km/h or mph) in the Distance Unit; elevation stays in metres. */
  protected readonly units = inject(Units);

  constructor() {
    reloadOnSync(() => this.rides.reload());
  }

  protected kind(ride: Ride): { label: string; tone: string } {
    return kinds[ride.sportType] ?? { label: ride.sportType, tone: 'muted' };
  }

  /** Clicking the current sort column flips the direction; another column starts with the biggest first. */
  protected sortBy(key: RideSortKey): void {
    const direction = key === this.sortKey() && !this.ascending() ? 'asc' : 'desc';
    this.navigate({ sort: key === 'date' && direction === 'desc' ? null : key, direction: direction === 'desc' ? null : direction, page: null });
  }

  protected goToPage(page: number): void {
    this.navigate({ page: page > 1 ? String(page) : null });
  }

  protected sortIndicator(key: RideSortKey): string {
    return key === this.sortKey() ? (this.ascending() ? ' ↑' : ' ↓') : '';
  }

  protected ariaSort(key: RideSortKey): 'ascending' | 'descending' | null {
    return key === this.sortKey() ? (this.ascending() ? 'ascending' : 'descending') : null;
  }

  private navigate(params: ListParams): void {
    const queryParams = Object.fromEntries(Object.entries(params).map(([key, value]) => [key, value || null]));
    this.router.navigate([], { queryParams, queryParamsHandling: 'merge' });
  }
}

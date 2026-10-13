import { DatePipe, DecimalPipe } from '@angular/common';
import { httpResource } from '@angular/common/http';
import { Component, computed, inject, input } from '@angular/core';
import { Router, RouterLink } from '@angular/router';
import { currentMonth, isoDate, lastDayOf, monthDate, monthKey } from '../shared/dates';
import { DistanceUnit, formatDistance, formatDuration, formatHoursMinutes, formatSpeed } from '../shared/format';
import { SportSwitch } from '../shared/sport-switch';
import { reloadOnSync } from '../sync/reload-on-sync';
import { Ride, RidePage, RideSortKey } from './ride';

/** The list's state, kept in the URL so the back button and shared links work. */
type ListParams = Partial<Record<'month' | 'search' | 'sort' | 'direction' | 'page' | 'unit', string | null>>;

/** All Rides of a month fit on one page; the pager only shows up past this. */
const pageSize = 200;

/** The badge each kind of Ride gets, by Strava's sport type. */
const kinds: Record<string, { label: string; tone: string }> = {
  Ride: { label: 'Road', tone: '' },
  VirtualRide: { label: 'Virtual', tone: 'muted' },
  GravelRide: { label: 'Gravel', tone: 'amber' },
  MountainBikeRide: { label: 'MTB', tone: 'green' },
};

/** Ride history: like Run history, a month of Rides at a time, but without a page per Ride. */
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
  /** "2026-10"; the current month when missing or malformed. */
  readonly month = input<string>();
  readonly search = input<string>();
  readonly sort = input<RideSortKey>();
  readonly direction = input<'asc' | 'desc'>();
  readonly page = input<string>();
  /** "mi" shows distances in miles and Avg Speed in mph; kilometres otherwise. */
  readonly unit = input<string>();

  protected readonly selectedMonth = computed(() => {
    const month = this.month();
    return month && /^\d{4}-(0[1-9]|1[0-2])$/.test(month) ? month : currentMonth();
  });
  protected readonly monthStart = computed(() => monthDate(this.selectedMonth(), 0));
  protected readonly previousMonthStart = computed(() => monthDate(this.selectedMonth(), -1));
  protected readonly nextMonthStart = computed(() => monthDate(this.selectedMonth(), 1));
  protected readonly isCurrentMonth = computed(() => this.selectedMonth() >= currentMonth());

  protected readonly distanceUnit = computed<DistanceUnit>(() => (this.unit() === 'mi' ? 'mi' : 'km'));

  protected readonly sortKey = computed(() => this.sort() ?? 'date');
  protected readonly ascending = computed(() => this.direction() === 'asc');

  /** A month's Rides. */
  protected readonly rides = httpResource<RidePage>(() => ({
    url: '/api/rides',
    params: Object.fromEntries(
      Object.entries({
        from: isoDate(this.monthStart()),
        to: isoDate(lastDayOf(this.monthStart())),
        search: this.search(),
        sort: this.sort(),
        direction: this.direction(),
        page: this.page(),
        pageSize: String(pageSize),
      }).filter((entry): entry is [string, string] => !!entry[1]),
    ),
  }));

  protected readonly pageCount = computed(() => {
    const page = this.rides.value();
    return page ? Math.max(1, Math.ceil(page.totalCount / page.pageSize)) : 1;
  });

  protected readonly searched = computed(() => !!this.search());

  /** Totals of the Rides listed: count, distance, Moving Time and their Avg Speed. */
  protected readonly summary = computed(() => {
    const items = this.rides.value()?.items ?? [];
    const distance = items.reduce((sum, r) => sum + r.distance, 0);
    const movingTime = items.reduce((sum, r) => sum + r.movingTime, 0);
    return {
      rides: items.length,
      distance,
      movingTime,
      speed: movingTime > 0 ? distance / 1000 / (movingTime / 3600) : null,
    };
  });

  /** The longest Ride listed, which fills its distance bar. */
  protected readonly longest = computed(() => Math.max(1, ...(this.rides.value()?.items ?? []).map((r) => r.distance)));

  protected readonly formatDistance = formatDistance;
  protected readonly formatDuration = formatDuration;
  protected readonly formatHoursMinutes = formatHoursMinutes;
  protected readonly formatSpeed = formatSpeed;

  constructor() {
    reloadOnSync(() => this.rides.reload());
  }

  protected kind(ride: Ride): { label: string; tone: string } {
    return kinds[ride.sportType] ?? { label: ride.sportType, tone: 'muted' };
  }

  /** Kilometres stay out of the URL, like the current month. */
  protected showUnit(unit: DistanceUnit): void {
    this.navigate({ unit: unit === 'km' ? null : unit });
  }

  /** A changed search or filter starts again at page 1. */
  protected filter(params: ListParams): void {
    this.navigate({ ...params, page: null });
  }

  /** Steps a month back or forward; the filters and sort order stay. */
  protected shiftMonth(months: number): void {
    this.showMonth(monthKey(monthDate(this.selectedMonth(), months)));
  }

  /** The current month stays out of the URL, so "/rides" always opens on it. */
  protected showMonth(month: string): void {
    if (!month) return;
    this.navigate({ month: month === currentMonth() ? null : month, page: null });
  }

  /** Clicking the current sort column flips the direction; another column starts with the biggest first. */
  protected sortBy(key: RideSortKey): void {
    const direction = key === this.sortKey() && !this.ascending() ? 'asc' : 'desc';
    this.navigate({ sort: key === 'date' && direction === 'desc' ? null : key, direction: direction === 'desc' ? null : direction, page: null });
  }

  protected goToPage(page: number): void {
    this.navigate({ page: page > 1 ? String(page) : null });
  }

  protected clearSearch(): void {
    this.navigate({ search: null, page: null });
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

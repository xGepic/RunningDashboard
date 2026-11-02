import { DatePipe, DecimalPipe } from '@angular/common';
import { httpResource } from '@angular/common/http';
import { Component, computed, inject, input } from '@angular/core';
import { RouterLink } from '@angular/router';
import { formatDuration, formatHoursMinutes } from '../shared/format';
import { YearlyHistoryList, requestParams } from '../shared/history-list';
import { SportSwitch } from '../shared/sport-switch';
import { Units } from '../shared/units';
import { Ride, RideBadge, RideKind, RidePage, RideSortKey, rideBadge } from './ride';
import { rideTable } from './ride-export';

/** Rides per page, as many as the Runs': a year's nearly always fit on one; the pager only shows up past this. */
const pageSize = 200;

/**
 * Ride history: like Run history, but a year's Rides at a time, 200 a page; the only filter keeps just the outdoor or
 * just the virtual Rides.
 */
@Component({
  selector: 'app-rides-page',
  imports: [DatePipe, DecimalPipe, RouterLink, SportSwitch],
  templateUrl: './rides-page.html',
  styleUrl: '../shared/history-list.scss',
})
export class RidesPage extends YearlyHistoryList<RideSortKey, Ride> {
  /** From the query string; all Rides without it. */
  readonly kind = input<RideKind>();

  /** A page of the year's Rides of the kind, with the totals of all of them. */
  protected readonly rides = httpResource<RidePage>(() => ({
    url: '/api/rides',
    params: requestParams({
      kind: this.kind(),
      ...this.yearParams(),
      sort: this.sort(),
      direction: this.direction(),
      page: this.page(),
      pageSize: String(pageSize),
    }),
  }));
  protected readonly list = this.rides;
  /** Every column starts with the biggest. */
  protected readonly ascendingFirst: readonly RideSortKey[] = [];
  protected readonly detailPath = '/rides';

  /** Of the year's Rides of the kind, for the page header; none until they are in, or when they could not be loaded. */
  protected readonly totals = computed(() => (this.rides.hasValue() ? this.rides.value().totals : null));

  protected readonly formatDuration = formatDuration;
  protected readonly formatHoursMinutes = formatHoursMinutes;
  /** Distances and Avg Speed (km/h or mph) in the Distance Unit; elevation stays in metres. */
  protected readonly units = inject(Units);

  /** "virtual " or "outdoor " before "Rides" while only those are listed. */
  protected readonly kindName = computed(() => (this.kind() ? `${this.kind()} ` : ''));

  protected badge(ride: Ride): RideBadge {
    return rideBadge(ride.sportType);
  }

  /** Lists only the Rides of the kind, or all of them, from the first page. */
  protected showKind(kind: RideKind | null): void {
    this.navigate({ kind, page: null });
  }

  /** The year's Rides of the kind, or those of every year, in the list's sort order. */
  protected exportRides(scope: 'year' | 'all'): void {
    const params = requestParams({ kind: this.kind(), ...(scope === 'year' ? this.yearParams() : {}), sort: this.sort(), direction: this.direction() });
    this.exportCsv<Ride>('/api/rides/export', params, this.exportFileName('rides', scope), (rides) => rideTable(rides, this.units.unit()));
  }
}

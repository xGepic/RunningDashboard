import { DatePipe, DecimalPipe } from '@angular/common';
import { httpResource } from '@angular/common/http';
import { Component, computed, inject } from '@angular/core';
import { RouterLink } from '@angular/router';
import { formatDuration, formatHoursMinutes } from '../shared/format';
import { HistoryList, requestParams } from '../shared/history-list';
import { SportSwitch } from '../shared/sport-switch';
import { Units } from '../shared/units';
import { Ride, RidePage, RideSortKey } from './ride';

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
  styleUrl: '../shared/history-list.scss',
})
export class RidesPage extends HistoryList<RideSortKey, Ride> {
  /** A page of all Rides, with the totals of all of them. */
  protected readonly rides = httpResource<RidePage>(() => ({
    url: '/api/rides',
    params: requestParams({ sort: this.sort(), direction: this.direction(), page: this.page(), pageSize: String(pageSize) }),
  }));
  protected readonly list = this.rides;
  /** Every column starts with the biggest. */
  protected readonly ascendingFirst: readonly RideSortKey[] = [];

  /** Of all Rides, for the page header; none until they are in, or when they could not be loaded. */
  protected readonly totals = computed(() => (this.rides.hasValue() ? this.rides.value().totals : null));

  protected readonly formatDuration = formatDuration;
  protected readonly formatHoursMinutes = formatHoursMinutes;
  /** Distances and Avg Speed (km/h or mph) in the Distance Unit; elevation stays in metres. */
  protected readonly units = inject(Units);

  protected kind(ride: Ride): { label: string; tone: string } {
    return kinds[ride.sportType] ?? { label: ride.sportType, tone: 'muted' };
  }
}

import { DatePipe, DecimalPipe } from '@angular/common';
import { httpResource } from '@angular/common/http';
import { Component, inject, input } from '@angular/core';
import { RouterLink } from '@angular/router';
import { RunMap } from '../runs/run-map';
import { formatDuration } from '../shared/format';
import { Units } from '../shared/units';
import { RideBadge, RideDetail, rideBadge } from './ride';

/**
 * A Ride's own page: its figures and, for an outdoor one, its route. Laid out like a Run's page, but Strava's summary
 * is all there is of a Ride, so it has no Splits, and the map has no replay.
 */
@Component({
  selector: 'app-ride-detail-page',
  imports: [DatePipe, DecimalPipe, RouterLink, RunMap],
  templateUrl: './ride-detail-page.html',
  styleUrl: '../runs/run-detail-page.scss',
})
export class RideDetailPage {
  /** From the route, rides/:id. */
  readonly id = input.required<string>();

  protected readonly ride = httpResource<RideDetail>(() => `/api/rides/${this.id()}`);
  protected readonly formatDuration = formatDuration;
  /** Distances and speeds in the Distance Unit; elevation stays in metres. */
  protected readonly units = inject(Units);

  protected badge(ride: RideDetail): RideBadge {
    return rideBadge(ride.sportType);
  }

  /**
   * How many facts go in a row, at most `max`, spread over as few rows as evenly as can be, as on a Run's page.
   * Counts the facts the template shows.
   */
  protected factsPerRow(ride: RideDetail, max: number): number {
    const facts = 8 + (ride.averagePower !== null ? 1 : 0) + (ride.averageCadence !== null ? 1 : 0);
    return Math.ceil(facts / Math.ceil(facts / max));
  }
}

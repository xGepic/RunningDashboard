import { DatePipe, DecimalPipe } from '@angular/common';
import { httpResource } from '@angular/common/http';
import { Component, input } from '@angular/core';
import { RouterLink } from '@angular/router';
import { formatDuration, formatSwimPace } from '../shared/format';
import { SwimDetail } from './swim';

/**
 * A Swim's own page: its figures. Laid out like a Run's page, but Strava's summary is all there is of a Swim, so it
 * has no Splits; and the Athlete swims in pools only, so it has no route either.
 */
@Component({
  selector: 'app-swim-detail-page',
  imports: [DatePipe, DecimalPipe, RouterLink],
  templateUrl: './swim-detail-page.html',
  styleUrl: '../runs/run-detail-page.scss',
})
export class SwimDetailPage {
  /** From the route, swims/:id. */
  readonly id = input.required<string>();

  protected readonly swim = httpResource<SwimDetail>(() => `/api/swims/${this.id()}`);
  protected readonly formatDuration = formatDuration;
  protected readonly formatSwimPace = formatSwimPace;
}

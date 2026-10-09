import { DatePipe, DecimalPipe } from '@angular/common';
import { httpResource } from '@angular/common/http';
import { Component, input } from '@angular/core';
import { RouterLink } from '@angular/router';
import { formatDistance, formatDuration, formatPace } from '../shared/format';
import { RunDetail } from './run';

@Component({
  selector: 'app-run-detail-page',
  imports: [DatePipe, DecimalPipe, RouterLink],
  templateUrl: './run-detail-page.html',
  styleUrl: './run-detail-page.scss',
})
export class RunDetailPage {
  /** From the route, runs/:id. */
  readonly id = input.required<string>();

  protected readonly run = httpResource<RunDetail>(() => `/api/runs/${this.id()}`);
  protected readonly formatDistance = formatDistance;
  protected readonly formatDuration = formatDuration;
  protected readonly formatPace = formatPace;
}

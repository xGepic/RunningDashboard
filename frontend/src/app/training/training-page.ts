import { DatePipe, DecimalPipe } from '@angular/common';
import { httpResource } from '@angular/common/http';
import { Component, inject, input } from '@angular/core';
import { Router, RouterLink } from '@angular/router';
import { GoalRace } from '../goal-races/goal-race';
import { formatDistance, formatDuration, formatPace, formatPaceDifference } from '../shared/format';
import { reloadOnSync } from '../sync/reload-on-sync';
import { Training } from './training';

@Component({
  selector: 'app-training-page',
  imports: [DatePipe, DecimalPipe, RouterLink],
  templateUrl: './training-page.html',
  styleUrl: './training-page.scss',
})
export class TrainingPage {
  private readonly router = inject(Router);

  /** From the query string: the Goal Race whose block to show; without it, the block today falls in. */
  readonly goalRace = input<string>();

  protected readonly training = httpResource<Training>(() => {
    const goalRaceId = this.goalRace();
    return { url: '/api/training', params: goalRaceId ? { goalRaceId } : undefined };
  });
  protected readonly goalRaces = httpResource<GoalRace[]>(() => '/api/goal-races');

  protected readonly formatDistance = formatDistance;
  protected readonly formatDuration = formatDuration;
  protected readonly formatPace = formatPace;
  protected readonly formatPaceDifference = formatPaceDifference;

  constructor() {
    reloadOnSync(() => this.training.reload());
  }

  protected select(goalRaceId: string): void {
    this.router.navigate([], { queryParams: { goalRace: goalRaceId || null } });
  }
}

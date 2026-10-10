import { DatePipe, DecimalPipe } from '@angular/common';
import { httpResource } from '@angular/common/http';
import { Component, computed, inject, input } from '@angular/core';
import { Router, RouterLink } from '@angular/router';
import { NgxEchartsDirective } from 'ngx-echarts';
import { GoalRace } from '../goal-races/goal-race';
import { ChartTheme } from '../shared/chart-theme';
import { weekLabel, weeklyDistanceChart } from '../shared/charts';
import { formatDistance, formatDuration, formatPace, formatPaceDifference } from '../shared/format';
import { reloadOnSync } from '../sync/reload-on-sync';
import { Training } from './training';

@Component({
  selector: 'app-training-page',
  imports: [DatePipe, DecimalPipe, NgxEchartsDirective, RouterLink],
  templateUrl: './training-page.html',
  styleUrl: './training-page.scss',
})
export class TrainingPage {
  private readonly router = inject(Router);
  private readonly theme = inject(ChartTheme);

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
  protected readonly weekLabel = weekLabel;

  protected readonly recentWeeksChart = computed(() => {
    const weeks = this.training.value()?.countdown?.recentWeeks ?? [];
    return weeks.length ? weeklyDistanceChart(weeks, this.theme.colors()) : null;
  });

  constructor() {
    reloadOnSync(() => this.training.reload());
  }

  protected select(goalRaceId: string): void {
    this.router.navigate([], { queryParams: { goalRace: goalRaceId || null } });
  }
}

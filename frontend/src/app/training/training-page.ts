import { DatePipe, DecimalPipe, NgTemplateOutlet } from '@angular/common';
import { httpResource } from '@angular/common/http';
import { Component, computed, inject, input } from '@angular/core';
import { Router, RouterLink } from '@angular/router';
import { NgxEchartsDirective } from 'ngx-echarts';
import { GoalRace } from '../goal-races/goal-race';
import { ChartTheme } from '../shared/chart-theme';
import { weekLabel, weeklyDistanceChart } from '../shared/charts';
import { formatDistance, formatDuration, formatPace, formatPaceDifference } from '../shared/format';
import { reloadOnSync } from '../sync/reload-on-sync';
import { Phase } from '../goal-races/training-plan';
import { DayState, LinedUpWeek, Training } from './training';

/** How the calendar strip names each day's state. */
const dayStates: Record<DayState, string> = {
  ran: 'run as planned',
  missed: 'missed',
  unplanned: 'run on an unplanned day',
  planned: 'still to come',
  rest: 'rest day',
};

@Component({
  selector: 'app-training-page',
  imports: [DatePipe, DecimalPipe, NgTemplateOutlet, NgxEchartsDirective, RouterLink],
  templateUrl: './training-page.html',
  styleUrl: './training-page.scss',
})
export class TrainingPage {
  private readonly router = inject(Router);
  private readonly theme = inject(ChartTheme);

  /** From the query string: the Goal Race whose block to show; without it, the block today falls in. */
  readonly goalRace = input<string>();

  /** From the query string: the Goal Race whose block to compare with, or "none"; without it, the previous one. */
  readonly compareWith = input<string>();

  protected readonly training = httpResource<Training>(() => {
    const params = Object.fromEntries(
      Object.entries({ goalRaceId: this.goalRace(), compareWith: this.compareWith() }).filter(
        (entry): entry is [string, string] => !!entry[1],
      ),
    );
    return { url: '/api/training', params };
  });
  protected readonly goalRaces = httpResource<GoalRace[]>(() => '/api/goal-races');

  protected readonly formatDistance = formatDistance;
  protected readonly formatDuration = formatDuration;
  protected readonly formatPace = formatPace;
  protected readonly formatPaceDifference = formatPaceDifference;
  protected readonly weekLabel = weekLabel;
  protected readonly dayStates = dayStates;

  /** The table's rows: lined up with the comparison by the API, or else the viewed block's weeks alone. */
  protected readonly rows = computed((): LinedUpWeek[] => {
    const block = this.training.value()?.block;
    if (!block) return [];
    return block.comparison?.weeks ?? block.weeks.map((week) => ({ weeksToRace: week.weeksToRace, viewed: week, compared: null }));
  });

  /** The Goal Races the viewed block can be compared with. */
  protected readonly comparableRaces = computed(() => {
    const viewed = this.training.value()?.block?.goalRace.id;
    return this.goalRaces.hasValue() ? this.goalRaces.value().filter((r) => r.id !== viewed) : [];
  });

  /** What the comparison picker shows: the choice in the URL, or else the default the API picked. */
  protected readonly comparisonChoice = computed(
    () => this.compareWith() ?? String(this.training.value()?.block?.comparison?.goalRace.id ?? 'none'),
  );

  protected readonly recentWeeksChart = computed(() => {
    const weeks = this.training.value()?.countdown?.recentWeeks ?? [];
    return weeks.length ? weeklyDistanceChart(weeks, this.theme.colors()) : null;
  });

  constructor() {
    reloadOnSync(() => this.training.reload());
  }

  /** The Phase whose band starts at this row. */
  protected phaseStartingAt(phases: Phase[], weeksToRace: number): Phase | undefined {
    return phases.find((p) => p.firstWeeksToRace === weeksToRace);
  }

  /** Marks an actual value against the plan as the API judged it: met, short, or neither yet. */
  protected metClass(met: boolean | null | undefined): string {
    return met === true ? 'met' : met === false ? 'short' : '';
  }

  /** Opens another block, compared with its own previous Goal Race. */
  protected select(goalRaceId: string): void {
    this.router.navigate([], { queryParams: { goalRace: goalRaceId || null, compareWith: null } });
  }

  /** Compares with another Goal Race's block, "none", or (null) the default previous one. */
  protected compare(compareWith: string | null): void {
    this.router.navigate([], { queryParams: { compareWith }, queryParamsHandling: 'merge' });
  }
}

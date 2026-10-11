import { DatePipe, DecimalPipe, NgTemplateOutlet } from '@angular/common';
import { httpResource } from '@angular/common/http';
import { Component, computed, effect, inject, input, signal, viewChild } from '@angular/core';
import { RouterLink } from '@angular/router';
import { NgxEchartsDirective } from 'ngx-echarts';
import { GoalRace } from '../goal-races/goal-race';
import { GoalRacePanel } from '../goal-races/goal-race-panel';
import { ChartTheme } from '../shared/chart-theme';
import { km, weekLabel, weeklyDistanceChart } from '../shared/charts';
import { daysBetween, localDate, today } from '../shared/dates';
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
  imports: [DatePipe, DecimalPipe, GoalRacePanel, NgTemplateOutlet, NgxEchartsDirective, RouterLink],
  templateUrl: './training-page.html',
  styleUrl: './training-page.scss',
})
export class TrainingPage {
  private readonly theme = inject(ChartTheme);

  /** From the query string: the Goal Race whose block to show; without it, the block today falls in. */
  readonly goalRace = input<string>();

  /** Compared, as the API does by default, with the previous Goal Race's block. */
  protected readonly training = httpResource<Training>(() => {
    const goalRaceId = this.goalRace();
    return goalRaceId ? { url: '/api/training', params: { goalRaceId } } : { url: '/api/training' };
  });
  protected readonly goalRaces = httpResource<GoalRace[]>(() => '/api/goal-races');

  protected readonly formatDistance = formatDistance;
  protected readonly formatDuration = formatDuration;
  protected readonly formatPace = formatPace;
  protected readonly formatPaceDifference = formatPaceDifference;
  protected readonly weekLabel = weekLabel;
  protected readonly dayStates = dayStates;
  protected readonly km = km;
  protected readonly todayDate = today();

  private readonly goalRacePanel = viewChild(GoalRacePanel);

  /**
   * Whether the block or countdown shown is that of the upcoming Goal Race, which then shows its details and actions,
   * so the Goal Race panel below leaves them out.
   */
  protected readonly upcomingRaceShown = computed(() => {
    const training = this.training.hasValue() ? this.training.value() : null;
    const race = training?.countdown?.goalRace ?? training?.block?.goalRace;
    return !!race && localDate(race.date) >= today();
  });

  /** Ticks every second while the countdown is shown. */
  private readonly now = signal(Date.now());

  /** The table's rows: lined up with the comparison by the API, or else the viewed block's weeks alone. */
  protected readonly rows = computed((): LinedUpWeek[] => {
    const block = this.training.value()?.block;
    if (!block) return [];
    return block.comparison?.weeks ?? block.weeks.map((week) => ({ weeksToRace: week.weeksToRace, viewed: week, compared: null }));
  });

  /** The countdown's figures: the block's weeks on a timeline from today to race day, and its facts. */
  protected readonly figures = computed(() => {
    const countdown = this.training.hasValue() ? this.training.value().countdown : null;
    if (!countdown) return null;
    const { goalRace } = countdown;
    const now = today();
    const start = localDate(countdown.trainingBlockStart);
    const race = localDate(goalRace.date);
    const weekCount = Math.floor(daysBetween(start, race) / 7) + 1;
    const span = Math.max(1, daysBetween(now, race));
    const recent = countdown.recentWeeks;
    const total = recent.reduce((sum, w) => sum + w.distance, 0);
    return {
      weekCount,
      daysToRace: daysBetween(now, race),
      blockLeft: Math.min(100, Math.max(0, (daysBetween(now, start) / span) * 100)),
      weeks: Array.from({ length: weekCount }, (_, i) => ({ race: i === weekCount - 1, alpha: 0.18 + (0.5 * i) / weekCount })),
      targetPace: goalRace.targetTime === null ? null : goalRace.targetTime / (goalRace.distance / 1000),
      recentTotal: total,
      recentAverage: recent.length ? total / recent.length : 0,
    };
  });

  /** Days, hours, minutes and seconds until the next block starts, Monday 00:00. */
  protected readonly clock = computed(() => {
    const start = this.training.hasValue() ? this.training.value().countdown?.trainingBlockStart : null;
    if (!start) return [];
    const left = Math.max(0, localDate(start).getTime() - this.now());
    const pad = (n: number) => String(n).padStart(2, '0');
    return [
      { value: String(Math.floor(left / 86_400_000)), unit: 'days' },
      { value: pad(Math.floor(left / 3_600_000) % 24), unit: 'hrs' },
      { value: pad(Math.floor(left / 60_000) % 60), unit: 'min' },
      { value: pad(Math.floor(left / 1000) % 60), unit: 'sec' },
    ];
  });

  protected readonly recentWeeksChart = computed(() => {
    const weeks = this.training.value()?.countdown?.recentWeeks ?? [];
    return weeks.length ? weeklyDistanceChart(weeks, this.theme.colors()) : null;
  });

  constructor() {
    reloadOnSync(() => this.training.reload());
    effect((onCleanup) => {
      // value() throws while the resource is in error, so ask hasValue() first.
      if (!this.training.hasValue() || !this.training.value().countdown) return;
      const timer = setInterval(() => this.now.set(Date.now()), 1000);
      onCleanup(() => clearInterval(timer));
    });
  }

  /** Opens the Goal Race form below on the race with this id. */
  protected editRace(id: number): void {
    const race = this.goalRaces.hasValue() ? this.goalRaces.value().find((r) => r.id === id) : undefined;
    if (race) this.goalRacePanel()?.edit(race);
  }

  /** A Goal Race was added, changed or deleted: the block, its comparison or the countdown may change. */
  protected goalRacesChanged(): void {
    this.goalRaces.reload();
    this.training.reload();
  }

  /** The Phase whose band starts at this row. */
  protected phaseStartingAt(phases: Phase[], weeksToRace: number): Phase | undefined {
    return phases.find((p) => p.firstWeeksToRace === weeksToRace);
  }

  /** Marks an actual value against the plan as the API judged it: met, short, or neither yet. */
  protected metClass(met: boolean | null | undefined): string {
    return met === true ? 'met' : met === false ? 'short' : '';
  }
}

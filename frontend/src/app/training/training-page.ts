import { DatePipe, DecimalPipe, NgTemplateOutlet } from '@angular/common';
import { httpResource } from '@angular/common/http';
import { Component, computed, effect, inject, input, signal, untracked, viewChild } from '@angular/core';
import { RouterLink } from '@angular/router';
import { NgxEchartsDirective } from 'ngx-echarts';
import { GoalRace } from '../goal-races/goal-race';
import { GoalRacePanel } from '../goal-races/goal-race-panel';
import { ChartTheme } from '../shared/chart-theme';
import { km, weekLabel, weeklyDistanceChart } from '../shared/charts';
import { daysBetween, localDate, today } from '../shared/dates';
import { formatDistance, formatDuration, formatPace, formatPaceDifference } from '../shared/format';
import { reloadOnSync } from '../sync/reload-on-sync';
import { Phase, RunType, runTypes } from '../goal-races/training-plan';
import { DayState, GoalRaceSummary, LinedUpWeek, Training } from './training';

/** The Run Types by type, for the calendar strip's labels. */
const runTypeByType = Object.fromEntries(runTypes.map((runType) => [runType.type, runType])) as Record<
  RunType,
  (typeof runTypes)[number]
>;

/** How the calendar strip names each day's state. */
const dayStates: Record<DayState, string> = {
  ran: 'run as planned',
  missed: 'missed',
  unplanned: 'run on an unplanned day',
  planned: 'still to come',
  rest: 'rest day',
};

interface Hero {
  /** What it counts down to: the Training Block's start, or race day. */
  to: 'block' | 'race';
  goalRace: GoalRaceSummary;
  /** The block's first Monday. */
  start: Date;
  /** Local midnight counted down to. */
  target: Date;
  /** Whole calendar days until the target. */
  days: number;
  daysToRace: number;
  weekCount: number;
  /** The week of the block today is in, from 1; none before the block. */
  currentWeek: number | null;
  targetPace: number | null;
  /** On the timeline, as a percentage of its width: where the block's weeks start, and today. */
  blockLeft: number;
  todayLeft: number;
  weeks: { race: boolean; alpha: number }[];
}

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
  protected readonly runTypes = runTypes;
  protected readonly runTypeByType = runTypeByType;
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

  /** Ticks every second while the hero counts down. */
  private readonly now = signal(Date.now());

  /** Local midnight of the current day, as a time; changes only at midnight. */
  private readonly day = computed(() => {
    const now = new Date(this.now());
    return new Date(now.getFullYear(), now.getMonth(), now.getDate()).getTime();
  });

  /** The block start the page has already reloaded for, so a reload that still brings the countdown is not repeated. */
  private reloadedFor: number | null = null;

  /** The table's rows: lined up with the comparison by the API, or else the viewed block's weeks alone. */
  protected readonly rows = computed((): LinedUpWeek[] => {
    const block = this.training.value()?.block;
    if (!block) return [];
    return block.comparison?.weeks ?? block.weeks.map((week) => ({ weeksToRace: week.weeksToRace, viewed: week, compared: null }));
  });

  /**
   * The hero's countdown: before the next Training Block, to its start; during the upcoming Goal Race's block, to race
   * day. With the block's weeks on a timeline (from today to race day before the block, across the block during it) and
   * its facts.
   */
  protected readonly hero = computed((): Hero | null => {
    const training = this.training.hasValue() ? this.training.value() : null;
    const day = new Date(this.day());
    let to: Hero['to'];
    let goalRace: GoalRaceSummary;
    let start: Date;
    let targetPace: number | null;
    if (training?.countdown) {
      to = 'block';
      goalRace = training.countdown.goalRace;
      start = localDate(training.countdown.trainingBlockStart);
      targetPace = goalRace.targetTime === null ? null : goalRace.targetTime / (goalRace.distance / 1000);
    } else if (training?.block?.weeks.some((w) => w.status === 'current') && localDate(training.block.goalRace.date) >= day) {
      to = 'race';
      goalRace = training.block.goalRace;
      start = localDate(training.block.weeks[0].start);
      targetPace = training.block.targetPace;
    } else {
      return null;
    }
    const race = localDate(goalRace.date);
    const target = to === 'block' ? start : race;
    const weekCount = Math.floor(daysBetween(start, race) / 7) + 1;
    const span = to === 'block' ? Math.max(1, daysBetween(day, race)) : weekCount * 7;
    const fromStart = daysBetween(start, day);
    return {
      to,
      goalRace,
      start,
      target,
      days: daysBetween(day, target),
      daysToRace: daysBetween(day, race),
      weekCount,
      currentWeek: to === 'race' ? Math.floor(fromStart / 7) + 1 : null,
      targetPace,
      blockLeft: to === 'block' ? Math.min(100, (-fromStart / span) * 100) : 0,
      todayLeft: to === 'block' ? 0 : Math.min(100, (fromStart / span) * 100),
      weeks: Array.from({ length: weekCount }, (_, i) => ({ race: i === weekCount - 1, alpha: 0.18 + (0.5 * i) / weekCount })),
    };
  });

  /** The recent Training Weeks' total and average distance under the countdown. */
  protected readonly recent = computed(() => {
    const weeks = this.training.hasValue() ? (this.training.value().countdown?.recentWeeks ?? []) : [];
    const total = weeks.reduce((sum, w) => sum + w.distance, 0);
    return { total, average: weeks.length ? total / weeks.length : 0 };
  });

  /** Days, hours, minutes and seconds until what the hero counts down to, at 00:00. */
  protected readonly clock = computed(() => {
    const target = this.hero()?.target;
    if (!target) return [];
    const left = Math.max(0, target.getTime() - this.now());
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
      if (!this.hero()) return;
      const timer = setInterval(() => this.now.set(Date.now()), 1000);
      onCleanup(() => clearInterval(timer));
    });
    // When the block starts, the API has its Training Block instead of the countdown, and the hero counts to race day.
    effect(() => {
      const hero = this.hero();
      if (hero?.to !== 'block' || this.now() < hero.target.getTime() || this.reloadedFor === hero.target.getTime()) return;
      this.reloadedFor = hero.target.getTime();
      untracked(() => this.training.reload());
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

  /** The Phase this week is in; none outside the Phases. */
  protected phaseOf(phases: Phase[] | null, weeksToRace: number): Phase | undefined {
    return phases?.find((p) => p.firstWeeksToRace <= weeksToRace && weeksToRace <= p.lastWeeksToRace);
  }

  /** Marks an actual value against the plan as the API judged it: met, short, or neither yet. */
  protected metClass(met: boolean | null | undefined): string {
    return met === true ? 'met' : met === false ? 'short' : '';
  }
}

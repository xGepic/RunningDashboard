import { Phase, PhaseType, RunType } from '../goal-races/training-plan';
import { DistanceJump, WeekDistance } from '../shared/charts';

/** As returned by GET /api/training. Distances in metres, times in seconds, paces in seconds per km. */
export interface Training {
  /** "block": a Goal Race's Training Block; "countdown": today is in no block, but a Goal Race is ahead; "none": neither. */
  view: 'block' | 'countdown' | 'none';
  block: TrainingBlock | null;
  countdown: Countdown | null;
}

/** Between Training Blocks: how long until the next one, and how much was run lately. */
export interface Countdown {
  /** The Goal Race whose Training Block comes next. */
  goalRace: GoalRaceSummary;
  /** The Monday that block starts, yyyy-MM-dd. */
  trainingBlockStart: string;
  daysRemaining: number;
  /** The last Training Weeks up to the current one, oldest first; weeks without Runs as zero. */
  recentWeeks: WeekDistance[];
}

/** The Goal Race a Training Block leads up to. */
export interface GoalRaceSummary {
  id: number;
  name: string;
  /** yyyy-MM-dd. */
  date: string;
  distance: number;
  targetTime: number | null;
}

export interface TrainingBlock {
  goalRace: GoalRaceSummary;
  /** Target Time ÷ Goal Race distance in whole seconds, rounded down so running it beats the Target Time; none without one. */
  targetPace: number | null;
  /** The Training Plan's Phases, first to last; none without a plan. */
  phases: Phase[] | null;
  /** First week to Race Week. */
  weeks: TrainingBlockWeek[];
  /** The block this one is compared with; none when the comparison is off. */
  comparison: Comparison | null;
}

/** Another Goal Race's Training Block, lined up with the viewed one by Weeks to Race. */
export interface Comparison {
  goalRace: GoalRaceSummary;
  /**
   * One per Weeks to Race in either block, first to Race Week. Both sides' Long Runs are compared with the viewed
   * Goal Race's Target Pace.
   */
  weeks: LinedUpWeek[];
}

/** One Weeks to Race in both blocks; a block that does not have that week has nothing on its side. */
export interface LinedUpWeek {
  weeksToRace: number;
  viewed: TrainingBlockWeek | null;
  compared: TrainingBlockWeek | null;
}

export interface TrainingBlockWeek {
  /** 0 for the Race Week, −1 for the week before, and so on. */
  weeksToRace: number;
  /** The Monday, yyyy-MM-dd. */
  start: string;
  /** The Sunday, yyyy-MM-dd. */
  end: string;
  /** "current": today is in this week. */
  status: 'past' | 'current' | 'future';
  /** What was run; none for a future week. */
  actual: WeekActual | null;
  /** What the Training Plan has for the week, against what was run; none without a plan. */
  plan: WeekPlan | null;
}

/** A planned Training Week against what was run in it. */
export interface WeekPlan {
  phase: PhaseType;
  recoveryWeek: boolean;
  /** Metres; none if not planned. */
  plannedDistance: number | null;
  /** Metres; none if not planned. */
  plannedLongRun: number | null;
  /** Whether the week's distance reached the plan's; none for a future week or nothing planned. */
  distanceMet: boolean | null;
  /** Whether the Long Run reached the plan's; none for a future week or nothing planned. */
  longRunMet: boolean | null;
  /** Monday to Sunday: the calendar strip. */
  days: PlannedDay[];
  /** None unless the planned distance makes the week one, against the planned weeks before it. */
  distanceJump: DistanceJump | null;
}

export interface PlannedDay {
  /** yyyy-MM-dd. */
  date: string;
  state: DayState;
  /** The Running Day's; none for a rest day or a Run on an unplanned day. */
  runType: RunType | null;
}

/**
 * "ran": a Running Day with a Run; "missed": a past Running Day without one; "unplanned": a Run on another day;
 * "planned": a Running Day still to come; "rest": neither.
 */
export type DayState = 'ran' | 'missed' | 'unplanned' | 'planned' | 'rest';

export interface WeekActual {
  runs: number;
  distance: number;
  movingTime: number;
  /** Sum over the week's Runs; none if no Run has one. */
  relativeEffort: number | null;
  /** The longest Run of the week; none in a week without Runs. */
  longRun: LongRun | null;
  /** None unless the week's distance so far makes it one. */
  distanceJump: DistanceJump | null;
}

export interface LongRun {
  runId: number;
  name: string;
  startLocal: string;
  distance: number;
  pace: number | null;
  /** Whole seconds per km slower (positive) or faster (negative) than the Target Pace; none without a Target Time. */
  paceVsTarget: number | null;
}

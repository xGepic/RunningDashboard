import { WeekDistance } from '../shared/charts';

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
  /** First week to Race Week. */
  weeks: TrainingBlockWeek[];
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
}

export interface WeekActual {
  runs: number;
  distance: number;
  movingTime: number;
  /** Sum over the week's Runs; none if no Run has one. */
  relativeEffort: number | null;
  /** The longest Run of the week; none in a week without Runs. */
  longRun: LongRun | null;
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

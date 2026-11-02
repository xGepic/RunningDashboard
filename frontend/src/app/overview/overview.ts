import { TrainingTimeWeek, WeekDistance } from '../shared/charts';

/** As returned by GET /api/overview. Distances in metres, times in seconds. */
export interface Overview {
  totals: Totals;
  years: YearTotals[];
  /** Every Training Week from the first Run's up to the current one; weeks without Runs have distance 0. */
  weeks: WeekDistance[];
  /** The current year's Yearly Goal; none without one. */
  yearlyGoal: YearlyGoal | null;
  /** None without one. */
  weeklyTimeGoal: WeeklyTimeGoal | null;
  /** None without Runs. */
  streak: Streak | null;
  /**
   * Every Training Week from the first Run's or Listed Activity's up to the current one, with all zeros for an empty
   * week; empty without Runs or without Rides and Swims.
   */
  trainingTime: TrainingTimeWeek[];
  /** The current Training Week's Training Time, Rides and Swims or not; none without Runs. */
  thisWeek: TrainingTimeWeek | null;
  /** Today's date a year ago; none without Runs on it. */
  onThisDay: OnThisDay | null;
}

/** Today's date a year ago, by local time; on 29 February, 28 February a year ago. */
export interface OnThisDay {
  /** yyyy-MM-dd. */
  day: string;
  /** At least one, newest first. */
  runs: OnThisDayRun[];
}

export interface OnThisDayRun {
  id: number;
  name: string;
  /** Local start, e.g. "2025-11-02T07:30:00". */
  startLocal: string;
  /** Metres. */
  distance: number;
  /** Seconds. */
  movingTime: number;
}

/**
 * Consecutive Training Weeks with at least one Run. The current week adds to it once it has a Run and only breaks it
 * once it ends without one.
 */
export interface Streak {
  /** Weeks in the Streak up to now. */
  current: number;
  /** The Monday of the current Streak's first Training Week (yyyy-MM-dd); none while it is 0. */
  start: string | null;
  thisWeekHasRun: boolean;
  /** The more recent of two equally long ones, so the ongoing Streak once it ties. */
  longest: LongestStreak;
  /** The last 12 Training Weeks up to the current one, oldest first, but none before the first Run's. */
  recentWeeks: StreakWeek[];
}

export interface LongestStreak {
  weeks: number;
  /** The Monday of its first Training Week (yyyy-MM-dd). */
  start: string;
  /** The Sunday of its last Training Week (yyyy-MM-dd). */
  end: string;
}

export interface StreakWeek {
  /** The Monday (yyyy-MM-dd). */
  start: string;
  hasRun: boolean;
}

/** The Training Time the Athlete wants in every Training Week, Runs, Rides and Swims together, in seconds. */
export interface WeeklyTimeGoal {
  time: number;
}

/** The distance the Athlete wants to run in a calendar year, in metres. */
export interface YearlyGoal {
  year: number;
  distance: number;
}

export interface Totals {
  runs: number;
  distance: number;
  movingTime: number;
  elevationGain: number;
}

export interface YearTotals extends Totals {
  year: number;
}

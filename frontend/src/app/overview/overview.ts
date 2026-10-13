import { WeekDistance } from '../shared/charts';

/** As returned by GET /api/overview. Distances in metres, times in seconds, pace in seconds per km. */
export interface Overview {
  totals: Totals;
  years: YearTotals[];
  /** Every Training Week from the first Run's up to the current one; weeks without Runs have distance 0. */
  weeks: WeekDistance[];
  /** Every month from the first Run's up to the current one; months without Runs have no pace. */
  months: MonthPace[];
  /** The current year's Yearly Goal; none without one. */
  yearlyGoal: YearlyGoal | null;
  /** None without Runs. */
  streak: Streak | null;
}

/**
 * Consecutive Training Weeks with at least one Run. The current week adds to it once it has a Run and only breaks it
 * once it ends without one.
 */
export interface Streak {
  /** Weeks in the Streak up to now. */
  current: number;
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

export interface MonthPace {
  year: number;
  month: number;
  distance: number;
  pace: number | null;
}

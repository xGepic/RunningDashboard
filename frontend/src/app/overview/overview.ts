import { WeekDistance } from '../shared/charts';

/** As returned by GET /api/overview. Distances in metres, times in seconds, pace in seconds per km. */
export interface Overview {
  totals: Totals;
  years: YearTotals[];
  /**
   * Every Training Week from the first Run's up to the current one; weeks without Runs have distance 0. Each says
   * whether it is a Distance Jump.
   */
  weeks: WeekDistance[];
  /** Every month from the first Run's up to the current one; months without Runs have no pace. */
  months: MonthPace[];
  /** The current year's Yearly Goal; none without one. */
  yearlyGoal: YearlyGoal | null;
  /** Every local date with Runs, oldest first. */
  days: DayDistance[];
  /** The current and the longest Streak, over the whole history. */
  streak: Streak;
}

/** Consecutive Training Weeks with at least one Run. */
export interface Streak {
  /** Training Weeks; 0 without Runs. */
  current: number;
  /** None without Runs. */
  longest: LongestStreak | null;
}

/** The longest Streak over the whole history; of equally long ones, the most recent. */
export interface LongestStreak {
  /** Training Weeks. */
  weeks: number;
  /** The Monday of its first Training Week, yyyy-MM-dd. */
  first: string;
  /** The Monday of its last Training Week, yyyy-MM-dd. */
  last: string;
}

/** Metres run on one local date, all its Runs together. */
export interface DayDistance {
  /** yyyy-MM-dd. */
  date: string;
  distance: number;
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

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

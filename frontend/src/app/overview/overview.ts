/** As returned by GET /api/overview. Distances in metres, times in seconds, pace in seconds per km. */
export interface Overview {
  totals: Totals;
  years: YearTotals[];
  /** Every Training Week from the first Run's up to the current one; weeks without Runs have distance 0. */
  weeks: WeekDistance[];
  /** Every month from the first Run's up to the current one; months without Runs have no pace. */
  months: MonthPace[];
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

export interface WeekDistance {
  /** The Monday, yyyy-MM-dd. */
  start: string;
  distance: number;
}

export interface MonthPace {
  year: number;
  month: number;
  distance: number;
  pace: number | null;
}

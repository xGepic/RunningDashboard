/** A Shoe as listed by GET /api/shoes: active ones first, then retired, each most recently used first. */
export interface Shoe {
  /** Strava's gear id. */
  id: string;
  name: string;
  /** Metres, as Strava counts them. */
  distance: number;
  retired: boolean;
  runs: number;
  /** Local start of the latest Run in this Shoe. */
  lastRun: string | null;
  /** Metres of its Runs in the last {@link recentWeeks} weeks, today included. */
  recentDistance: number;
}

/** How many weeks back a Shoe's recent distance goes, as GET /api/shoes counts it. */
export const recentWeeks = 5;

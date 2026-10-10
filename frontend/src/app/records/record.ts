/** One Best Effort distance as returned by GET /api/records, shortest first. Distances in metres, times in seconds. */
export interface RecordDistance {
  /** Strava's name for the distance, e.g. "5K" or "Half-Marathon". */
  name: string;
  distance: number;
  /** 1k, 5k, 10k, half marathon and marathon; the others are shown when the Athlete picks them. */
  shownByDefault: boolean;
  /** Each Best Effort that improved the Personal Best, oldest first. */
  progression: PersonalBest[];
  /** The current Personal Best: the last step of the progression. */
  personalBest: PersonalBest;
}

/** A Best Effort that was a Personal Best when it was run. */
export interface PersonalBest {
  runId: number;
  runName: string;
  /** Local start of the Run, without a time zone. */
  date: string;
  elapsedTime: number;
  /** Elapsed time per km in seconds. */
  timePerKm: number | null;
}

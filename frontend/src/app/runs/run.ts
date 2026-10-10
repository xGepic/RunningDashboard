/** A Run as listed by GET /api/runs. Distance in metres, times in seconds, pace in seconds per km. */
export interface Run {
  id: number;
  name: string;
  sportType: string;
  /** Wall-clock start where the Run happened, without a time zone. */
  startLocal: string;
  distance: number;
  movingTime: number;
  pace: number | null;
  /** Name of the Shoe logged with the Run, if Strava still lists it. */
  shoeName: string | null;
}

/** One page of GET /api/runs. */
export interface RunPage {
  items: Run[];
  /** 1-based. */
  page: number;
  pageSize: number;
  totalCount: number;
}

export type RunSortKey = 'date' | 'distance' | 'movingTime' | 'pace';

/** A Run with its detail, as returned by GET /api/runs/{id}. */
export interface RunDetail extends Run {
  elapsedTime: number;
  elevationGain: number;
  relativeEffort: number | null;
  stravaUrl: string;
  /** Whether Best Efforts and Splits are in: still to be fetched, stored, or no longer on Strava. */
  detail: 'pending' | 'loaded' | 'unavailable';
  bestEfforts: BestEffort[];
  splits: Split[];
}

/** Strava's fastest time over a standard distance within this Run. */
export interface BestEffort {
  name: string;
  distance: number;
  elapsedTime: number;
  /** Elapsed time per km in seconds; not Pace, which is Moving Time based. */
  timePerKm: number | null;
  /** Whether it was a Personal Best when it was run: faster than every earlier Best Effort over the distance. */
  personalBest: boolean;
}

/** A Split: one kilometre of the Run; the last one is usually shorter. */
export interface Split {
  index: number;
  distance: number;
  movingTime: number;
  pace: number | null;
  elevationDifference: number;
}

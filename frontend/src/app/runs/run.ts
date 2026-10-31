import { Weekday } from '../goal-races/training-plan';
import { ListPage } from '../shared/history-list';

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
export type RunPage = ListPage<Run>;

/** GET /api/runs/months: one year's Runs added up per calendar month. Distance in metres, times in seconds. */
export interface RunMonths {
  year: number;
  /** The year of the first Run; none without Runs. */
  firstYear: number | null;
  /** January to December; months without Runs as zero. */
  months: MonthTotals[];
  /** Monday to Sunday; days without Runs as zero. */
  weekdays: WeekdayTotals[];
}

/** The year's Runs on one day of the week added up, by their local start. */
export interface WeekdayTotals {
  day: Weekday;
  runs: number;
  distance: number;
}

export interface MonthTotals {
  /** 1 for January. */
  month: number;
  runs: number;
  distance: number;
  movingTime: number;
  /** Seconds per km over all of the month's Runs; none without Runs. */
  pace: number | null;
}

export type RunSortKey = 'date' | 'distance' | 'movingTime' | 'pace';

/** As returned by GET /api/runs/{id}/temperature, apart from the Run so its page need not wait for Open-Meteo. */
export interface RunTemperature {
  /** °C halfway through the Run, from the weather history where it started; none without GPS, on a treadmill, or while it cannot be looked up. */
  temperature: number | null;
}

/** A Run with its detail, as returned by GET /api/runs/{id}. */
export interface RunDetail extends Run {
  elapsedTime: number;
  elevationGain: number;
  relativeEffort: number | null;
  /** Beats per minute; none without a heart-rate recording. */
  averageHeartRate: number | null;
  stravaUrl: string;
  /** Whether Best Efforts and Splits are in: still to be fetched, stored, or no longer on Strava. */
  detail: 'pending' | 'loaded' | 'unavailable';
  /** Google's encoded polyline of the route; none for a Run without GPS, e.g. on a treadmill. */
  route: string | null;
  bestEfforts: BestEffort[];
  splits: Split[];
  /** Strava's splits per mile, for an Athlete who reads miles; their pace is still per km. */
  mileSplits: Split[];
  /** None unless it is a Fitness Run: not on a treadmill and not on a Goal Race's race day. */
  fitness: RunFitness | null;
}

/** A Fitness Run's figures. */
export interface RunFitness {
  /** Metres per heartbeat; none without heart rate. */
  aerobicEfficiency: number | null;
  /** Steps per minute, both feet together. */
  cadence: number | null;
  /** Whether it is its Training Week's Long Run. */
  isLongRun: boolean;
  /** A fraction, 0.05 being 5%; only for a Long Run of at least 10 km. */
  cardiacDrift: number | null;
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

import { ListPage } from '../shared/history-list';

/** A Swim as listed by GET /api/swims. Distance in metres, times in seconds, pace in seconds per 100 m. */
export interface Swim {
  id: number;
  name: string;
  /** Wall-clock start where the Swim happened, without a time zone. */
  startLocal: string;
  distance: number;
  movingTime: number;
  /** Swim Pace: Moving Time per 100 m. */
  pace: number | null;
  stravaUrl: string;
}

/** One page of GET /api/swims. */
export interface SwimPage extends ListPage<Swim> {
  /** Of every Swim, not just this page's. */
  totals: SwimTotals;
}

/** Distance in metres, Moving Time in seconds, Swim Pace in seconds per 100 m; none without distance. */
export interface SwimTotals {
  swims: number;
  distance: number;
  movingTime: number;
  pace: number | null;
}

export type SwimSortKey = 'date' | 'distance' | 'movingTime' | 'pace';

/** A Swim as GET /api/swims/:id gives it. Distance in metres, times in seconds, pace in seconds per 100 m. */
export interface SwimDetail {
  id: number;
  name: string;
  /** Wall-clock start where the Swim happened, without a time zone. */
  startLocal: string;
  distance: number;
  movingTime: number;
  elapsedTime: number;
  /** Swim Pace: Moving Time per 100 m; none without distance. */
  pace: number | null;
  /** Beats per minute; none without a heart-rate recording. */
  averageHeartRate: number | null;
  maxHeartRate: number | null;
  /** Google's encoded polyline; none without GPS, such as in a pool. */
  route: string | null;
  stravaUrl: string;
}

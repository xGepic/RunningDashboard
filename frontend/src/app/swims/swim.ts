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
export interface SwimPage {
  items: Swim[];
  /** 1-based. */
  page: number;
  pageSize: number;
  totalCount: number;
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

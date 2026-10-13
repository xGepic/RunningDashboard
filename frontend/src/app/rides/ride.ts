/** A Ride as listed by GET /api/rides. Distance and elevation in metres, times in seconds, speed in km/h. */
export interface Ride {
  id: number;
  name: string;
  /** Strava's sport type: Ride, VirtualRide, GravelRide or MountainBikeRide. */
  sportType: string;
  /** Wall-clock start where the Ride happened, without a time zone. */
  startLocal: string;
  distance: number;
  movingTime: number;
  /** Avg Speed: distance ÷ Moving Time. */
  speed: number | null;
  elevationGain: number;
  stravaUrl: string;
}

/** One page of GET /api/rides. */
export interface RidePage {
  items: Ride[];
  /** 1-based. */
  page: number;
  pageSize: number;
  totalCount: number;
}

export type RideSortKey = 'date' | 'distance' | 'movingTime' | 'speed' | 'elevation';

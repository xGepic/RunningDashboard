import { ListPage } from '../shared/history-list';

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
export interface RidePage extends ListPage<Ride> {
  /** Of every Ride, not just this page's. */
  totals: RideTotals;
}

/** Distance in metres, Moving Time in seconds, Avg Speed in km/h; none without Moving Time. */
export interface RideTotals {
  rides: number;
  distance: number;
  movingTime: number;
  speed: number | null;
}

export type RideSortKey = 'date' | 'distance' | 'movingTime' | 'speed' | 'elevation';

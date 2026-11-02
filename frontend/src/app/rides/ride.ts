import { YearPage } from '../shared/history-list';

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
export interface RidePage extends YearPage<Ride> {
  /** Of every Ride that matches, not just this page's. */
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

/** Which Rides the history lists besides all of them: the virtual ones (e.g. on Zwift) or the outdoor ones. */
export type RideKind = 'outdoor' | 'virtual';

/** A Ride as GET /api/rides/:id gives it. Distance and elevation in metres, times in seconds, speeds in km/h. */
export interface RideDetail {
  id: number;
  name: string;
  /** Strava's sport type: Ride, VirtualRide, GravelRide or MountainBikeRide. */
  sportType: string;
  /** Wall-clock start where the Ride happened, without a time zone. */
  startLocal: string;
  distance: number;
  movingTime: number;
  elapsedTime: number;
  /** Avg Speed: distance ÷ Moving Time; none without Moving Time. */
  speed: number | null;
  maxSpeed: number | null;
  elevationGain: number;
  /** Beats per minute; none without a heart-rate recording. */
  averageHeartRate: number | null;
  maxHeartRate: number | null;
  /** Watts, measured or estimated by Strava. */
  averagePower: number | null;
  /** Pedal revolutions per minute; none without a cadence sensor. */
  averageCadence: number | null;
  /** Google's encoded polyline; none without GPS, and for a virtual Ride. */
  route: string | null;
  stravaUrl: string;
}

export interface RideBadge {
  label: string;
  tone: string;
}

/** The badge each kind of Ride gets, by Strava's sport type. */
const badges: Record<string, RideBadge> = {
  Ride: { label: 'Road', tone: '' },
  VirtualRide: { label: 'Virtual', tone: 'muted' },
  GravelRide: { label: 'Gravel', tone: 'amber' },
  MountainBikeRide: { label: 'MTB', tone: 'green' },
};

/** The badge for a Ride of Strava's <code>sportType</code>. */
export function rideBadge(sportType: string): RideBadge {
  return badges[sportType] ?? { label: sportType, tone: 'muted' };
}

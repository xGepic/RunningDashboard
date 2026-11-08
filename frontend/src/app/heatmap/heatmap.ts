import { LatLng, decodePolyline } from '../shared/polyline';

/** A Run on the Heatmap, as GET /api/heatmap gives it. */
export interface HeatmapRun {
  id: number;
  name: string;
  /** Local wall-clock start, ISO without offset. */
  startLocal: string;
  /** Metres. */
  distance: number;
  /** The summary's simplified route, as Google's encoded polyline. */
  route: string;
}

export interface HeatmapData {
  /** Every Run with a route outdoors, oldest first. */
  runs: HeatmapRun[];
}

/** A Run with its route's points, ready to draw. */
export interface DrawnRun extends HeatmapRun {
  points: LatLng[];
}

/** South-west and north-east corners. */
export type Bounds = [LatLng, LatLng];

/** Runs starting this close to each other, in km, start at the same place. */
const samePlace = 5;

/** How far from the usual start a Run may start and still be part of the home area, in km. */
const homeRadius = 10;

/** Decodes each Run's route; a route without points is left out. */
export function drawnRuns(runs: HeatmapRun[]): DrawnRun[] {
  return runs.map((run) => ({ ...run, points: decodePolyline(run.route) })).filter((run) => run.points.length > 0);
}

/** The years with Runs on the Heatmap, newest first. */
export function heatmapYears(runs: HeatmapRun[]): number[] {
  return [...new Set(runs.map((run) => yearOf(run)))].sort((a, b) => b - a);
}

/** The Runs of one year, by local start; all of them without one. */
export function runsIn<T extends HeatmapRun>(runs: T[], year: number | null): T[] {
  return year === null ? runs : runs.filter((run) => yearOf(run) === year);
}

/** The box around every Run's route; none without Runs. */
export function boundsOf(runs: DrawnRun[]): Bounds | null {
  const points = runs.flatMap((run) => run.points);
  if (points.length === 0) return null;
  const lats = points.map(([lat]) => lat);
  const lngs = points.map(([, lng]) => lng);
  return [
    [Math.min(...lats), Math.min(...lngs)],
    [Math.max(...lats), Math.max(...lngs)],
  ];
}

/**
 * Where the Athlete usually runs: the box around the Runs that start within 10 km of the usual start, the start with
 * the most Runs starting within 5 km of it. A holiday Run far away does not shrink home to a dot, and two places run
 * from about as often do not stretch it over the land between them. None without Runs.
 */
export function homeArea(runs: DrawnRun[]): Bounds | null {
  const starts = runs.map((run) => run.points[0]);
  let usual: LatLng | null = null;
  let most = 0;
  for (const start of starts) {
    const count = starts.filter((other) => kilometresBetween(other, start) <= samePlace).length;
    if (count > most) [usual, most] = [start, count];
  }
  const centre = usual;
  return centre && boundsOf(runs.filter((run) => kilometresBetween(run.points[0], centre) <= homeRadius));
}

function yearOf(run: HeatmapRun): number {
  return Number(run.startLocal.slice(0, 4));
}

/** Great-circle distance. */
function kilometresBetween([lat1, lng1]: LatLng, [lat2, lng2]: LatLng): number {
  const radians = (degrees: number) => (degrees * Math.PI) / 180;
  const a =
    Math.sin(radians(lat2 - lat1) / 2) ** 2 +
    Math.cos(radians(lat1)) * Math.cos(radians(lat2)) * Math.sin(radians(lng2 - lng1) / 2) ** 2;
  return 2 * 6371 * Math.asin(Math.sqrt(a));
}

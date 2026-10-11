import { LatLng } from '../shared/polyline';

/** One stretch of the Run with its own pace: a Split, or the whole Run when it has no Splits. */
export interface ReplayStretch {
  /** Metres. */
  distance: number;
  /** Seconds. */
  movingTime: number;
  /** Seconds per km; none without distance. */
  pace: number | null;
}

/** Where the Athlete was at one moment of the replay. */
export interface ReplayFrame {
  position: LatLng;
  /** The route's points up to here, ending with <code>position</code>. */
  trail: LatLng[];
  /** Seconds of Moving Time. */
  time: number;
  /** Metres covered. */
  distance: number;
  /** 1-based: the km (Split) the Athlete is in. */
  stretch: number;
  pace: number | null;
}

/**
 * Replays a Run along its route at the pace of each stretch: the time within a stretch is spread evenly over its
 * distance, so the Run speeds up and slows down km by km. The route is GPS-drawn and its length differs a little from
 * the Run's distance, so distances are scaled onto it.
 */
export class Replay {
  /** Seconds of Moving Time the replay lasts. */
  readonly duration: number;

  private readonly alongRoute: number[];
  private readonly runDistance: number;
  /** Where each stretch ends, in metres and seconds since the start. */
  private readonly ends: { distance: number; time: number }[];

  constructor(
    private readonly points: LatLng[],
    private readonly stretches: ReplayStretch[],
  ) {
    this.alongRoute = [0];
    for (let i = 1; i < points.length; i++) this.alongRoute.push(this.alongRoute[i - 1] + metresBetween(points[i - 1], points[i]));
    let distance = 0;
    let time = 0;
    this.ends = stretches.map((s) => ({ distance: (distance += s.distance), time: (time += s.movingTime) }));
    this.runDistance = distance;
    this.duration = time;
  }

  /** The frame <code>time</code> seconds into the Run, held to the start and the finish. */
  at(time: number): ReplayFrame {
    const t = Math.min(Math.max(time, 0), this.duration);
    const index = Math.max(0, this.ends.findIndex((end) => end.time >= t));
    const stretch = this.stretches[index];
    const start = index === 0 ? { distance: 0, time: 0 } : this.ends[index - 1];
    const share = stretch?.movingTime ? (t - start.time) / stretch.movingTime : 1;
    const distance = start.distance + share * (stretch?.distance ?? 0);
    const { position, passed } = this.pointAt(this.runDistance ? (distance / this.runDistance) * this.routeLength : 0);
    return {
      position,
      trail: [...this.points.slice(0, passed + 1), position],
      time: t,
      distance,
      stretch: index + 1,
      pace: stretch?.pace ?? null,
    };
  }

  private get routeLength(): number {
    return this.alongRoute[this.alongRoute.length - 1] ?? 0;
  }

  /** The point <code>metres</code> along the route, and the index of the last route point passed. */
  private pointAt(metres: number): { position: LatLng; passed: number } {
    const along = this.alongRoute;
    let low = 0;
    let high = along.length - 1;
    while (low < high) {
      const middle = Math.ceil((low + high) / 2);
      if (along[middle] <= metres) low = middle;
      else high = middle - 1;
    }
    const [from, to] = [this.points[low], this.points[low + 1]];
    if (!to) return { position: from, passed: low };
    const share = (metres - along[low]) / (along[low + 1] - along[low] || 1);
    return { position: [from[0] + (to[0] - from[0]) * share, from[1] + (to[1] - from[1]) * share], passed: low };
  }
}

/** Great-circle distance in metres. */
function metresBetween([lat1, lng1]: LatLng, [lat2, lng2]: LatLng): number {
  const radians = Math.PI / 180;
  const dLat = (lat2 - lat1) * radians;
  const dLng = (lng2 - lng1) * radians;
  const a = Math.sin(dLat / 2) ** 2 + Math.cos(lat1 * radians) * Math.cos(lat2 * radians) * Math.sin(dLng / 2) ** 2;
  return 2 * 6_371_000 * Math.asin(Math.sqrt(a));
}

import { DistanceUnit, distanceIn } from '../shared/format';

/**
 * How a Pacing Plan spreads the Pace over the race: the same throughout, or steadily faster (Negative Split) or slower
 * (Positive Split) from start to finish.
 */
export type PacingStrategy = 'even' | 'negative' | 'positive';

/** One Split of a Pacing Plan, or a Checkpoint that falls within a Split. */
export interface PacingRow {
  /** Metres from the start to where the row ends. */
  end: number;
  /** The Split's 1-based number; none for a Checkpoint within a Split. */
  split: number | null;
  /** Metres the Split covers; none for a Checkpoint within a Split. */
  distance: number | null;
  /** Seconds the Split takes; none for a Checkpoint within a Split. */
  time: number | null;
  /** Seconds per km over the Split; none for a Checkpoint within a Split. */
  pace: number | null;
  /** Seconds from the start to the row's end. */
  clock: number;
  /** "5K", "Half" or "Finish" for a Checkpoint; none for a Split that ends at none. */
  checkpoint: string | null;
}

/** How far apart (metres) two points may be to count as the same, e.g. a Checkpoint and a Split's end. */
const samePoint = 1;

/** Checkpoints every 5 km, as timing mats are laid out. */
const checkpointEvery = 5000;

/** The shortest race (metres) with a half-way Checkpoint: a shorter one is over before half-way is worth a look. */
const halfWayFrom = 15000;

/**
 * The Splits and Checkpoints of a race run in a finish time. With a Negative or Positive Split, the Pace changes evenly
 * with the distance from start to finish, so the second half takes `share` less or more time than the first; the
 * finish time stays the same.
 */
export class PacingPlan {
  /** Seconds per metre on average. */
  private readonly average: number;
  /** How far the Pace at the start is above the average, as a fraction of it; at the finish it is as far below. */
  private readonly slope: number;

  /**
   * @param distance Metres.
   * @param finishTime Seconds.
   * @param share How much faster or slower the second half is than the first, 0.02 being 2%; ignored for an even Pace.
   */
  constructor(
    readonly distance: number,
    readonly finishTime: number,
    strategy: PacingStrategy,
    share: number,
  ) {
    this.average = finishTime / distance;
    // The halves take average·distance/2·(1 ± slope/2), so second ÷ first = ratio when slope = 2(1 − ratio)/(1 + ratio).
    const ratio = strategy === 'negative' ? 1 - share : strategy === 'positive' ? 1 + share : 1;
    this.slope = (2 * (1 - ratio)) / (1 + ratio);
  }

  /** Seconds per km at the start. */
  get startPace(): number {
    return this.paceAt(0) * 1000;
  }

  /** Seconds per km at the finish. */
  get finishPace(): number {
    return this.paceAt(this.distance) * 1000;
  }

  /** Seconds from the start to `metres` in. */
  clockAt(metres: number): number {
    return this.average * ((1 + this.slope) * metres - (this.slope * metres * metres) / this.distance);
  }

  /** The Splits in `unit`, the last one usually shorter, with each Checkpoint on the Split it ends or within it. */
  rows(unit: DistanceUnit): PacingRow[] {
    const splitLength = 1 / distanceIn(1, unit);
    const rows: PacingRow[] = [];
    for (let start = 0, split = 1; this.distance - start >= samePoint; start += splitLength, split++) {
      const end = this.distance - (start + splitLength) < samePoint ? this.distance : start + splitLength;
      const time = this.clockAt(end) - this.clockAt(start);
      rows.push({ end, split, distance: end - start, time, pace: (time / (end - start)) * 1000, clock: this.clockAt(end), checkpoint: null });
    }

    for (const [name, at] of this.checkpoints()) {
      const ending = rows.find((row) => Math.abs(row.end - at) < samePoint);
      if (ending) ending.checkpoint = name;
      else rows.splice(rows.findIndex((row) => row.end > at), 0, { end: at, split: null, distance: null, time: null, pace: null, clock: this.clockAt(at), checkpoint: name });
    }
    return rows;
  }

  /**
   * Every 5 km before the finish, half-way from 15 km on unless it is one of them, and the finish, by name and metres
   * in.
   */
  private checkpoints(): [string, number][] {
    const checkpoints: [string, number][] = [];
    for (let at = checkpointEvery; this.distance - at >= samePoint; at += checkpointEvery) checkpoints.push([`${at / 1000}K`, at]);
    const half = this.distance / 2;
    if (this.distance > halfWayFrom - samePoint && !checkpoints.some(([, at]) => Math.abs(at - half) < samePoint)) checkpoints.push(['Half', half]);
    checkpoints.push(['Finish', this.distance]);
    return checkpoints;
  }

  /** Seconds per metre at `metres` in. */
  private paceAt(metres: number): number {
    return this.average * (1 + this.slope * (1 - (2 * metres) / this.distance));
  }
}


import { Component, computed, inject, signal } from '@angular/core';
import { formatDuration, parseDuration } from '../shared/format';
import { Units } from '../shared/units';
import { PacingPlan, PacingRow, PacingStrategy } from './pacing';

/** The distances the calculator offers, in metres. */
export const raceDistances = [
  { name: '5K', distance: 5000 },
  { name: '10K', distance: 10000 },
  { name: 'Half marathon', distance: 21097.5 },
  { name: 'Marathon', distance: 42195 },
] as const;

/** The field the Athlete typed in or slid last; the other one is worked out from it. */
type Source = 'pace' | 'time';

/** The shares a Negative or Positive Split offers, in %. */
export const splitShares = [1, 2, 3, 4, 5, 6, 7, 8, 9, 10] as const;

/** The stretch of the Pacing Plan from one Checkpoint, or the start, to the next. */
export interface PacingStretch {
  /** The Checkpoint it ends at. */
  checkpoint: PacingRow;
  /** Seconds it takes. */
  time: number;
  /** Seconds per km over it. */
  pace: number;
  /** The Splits that end within it, the one ending at its Checkpoint included. */
  splits: PacingRow[];
}

/** The pace sliders' ends and where they start, in seconds per km. */
const slowestPace = 600;
const fastestPace = 150;
const startingPace = 330;

/**
 * A race distance's finish time at a steady pace, or the pace for a finish time. The pace is per km or mile, as the
 * Distance Unit says. Under it, the Pacing Plan for that finish time.
 */
@Component({
  selector: 'app-race-calculator-page',
  templateUrl: './race-calculator-page.html',
  styleUrl: './race-calculator-page.scss',
})
export class RaceCalculatorPage {
  protected readonly units = inject(Units);
  protected readonly distances = raceDistances;
  protected readonly shares = splitShares;

  protected readonly distance = signal<number>(10000);
  protected readonly source = signal<Source>('pace');
  private readonly typed = signal('');

  /** What the Athlete typed, in seconds; null while the field is empty or not a duration. */
  private readonly typedSeconds = computed(() => parseDuration(this.typed()) || null);

  protected readonly typedInvalid = computed(() => this.typed().trim() !== '' && this.typedSeconds() === null);

  /** Seconds per km or mile. */
  protected readonly pace = computed(() => {
    const seconds = this.typedSeconds();
    if (seconds === null) return null;
    return this.source() === 'pace' ? seconds : seconds / this.units.fromMetres(this.distance());
  });

  protected readonly finishTime = computed(() => {
    const seconds = this.typedSeconds();
    if (seconds === null) return null;
    return this.source() === 'time' ? seconds : seconds * this.units.fromMetres(this.distance());
  });

  /** The field as typed, or worked out from the other one: m:ss for the pace, h:mm:ss for the time. */
  protected fieldValue(field: Source): string {
    if (this.source() === field) return this.typed();
    const seconds = field === 'pace' ? this.pace() : this.finishTime();
    return seconds === null ? '' : formatDuration(seconds);
  }

  /** Whole seconds per km or mile, from 2:30 to 10:00 per km. */
  protected readonly paceRange = computed(() => {
    const perUnit = (secondsPerKm: number) => Math.round(secondsPerKm / this.units.fromMetres(1000));
    return { min: perUnit(fastestPace), max: perUnit(slowestPace), start: perUnit(startingPace) };
  });

  /** The finish times at the pace slider's ends, over the distance. */
  protected readonly timeRange = computed(() => {
    const { min, max, start } = this.paceRange();
    const distance = this.units.fromMetres(this.distance());
    return { min: Math.round(min * distance), max: Math.round(max * distance), start: Math.round(start * distance) };
  });

  /** Where a slider sits: at the figure, or at its start until there is one. */
  protected sliderValue(field: Source): number {
    const seconds = field === 'pace' ? this.pace() : this.finishTime();
    return Math.round(seconds ?? (field === 'pace' ? this.paceRange() : this.timeRange()).start);
  }

  protected readonly strategy = signal<PacingStrategy>('even');
  /** In %. */
  protected readonly share = signal<number>(5);

  /** Until there is a finish time, one of no time, for the Checkpoints alone. */
  protected readonly plan = computed(() => new PacingPlan(this.distance(), this.finishTime() ?? 0, this.strategy(), this.share() / 100));

  /** The plan's Splits and Checkpoints in the Distance Unit. */
  protected readonly rows = computed(() => this.plan().rows(this.units.unit()));

  /** A time of the plan, or "–" until there is a finish time. */
  protected duration(seconds: number): string {
    return this.finishTime() === null ? '–' : formatDuration(seconds);
  }

  /** A Pace of the plan, or "–" until there is a finish time. */
  protected splitPace(secondsPerKm: number): string {
    return this.finishTime() === null ? '–' : this.units.pace(secondsPerKm);
  }

  /** The rows by Checkpoint: about 10 stretches for a marathon instead of its 43 km. */
  protected readonly stretches = computed<PacingStretch[]>(() => {
    const stretches: PacingStretch[] = [];
    let splits: PacingRow[] = [];
    let start = { end: 0, clock: 0 };
    for (const row of this.rows()) {
      if (row.split !== null) splits.push(row);
      if (row.checkpoint === null) continue;
      const time = row.clock - start.clock;
      stretches.push({ checkpoint: row, time, pace: (time / (row.end - start.end)) * 1000, splits });
      splits = [];
      start = row;
    }
    return stretches;
  });

  /** The ends (metres) of the stretches whose Splits are shown. */
  private readonly opened = signal<ReadonlySet<number>>(new Set());

  protected isOpen(stretch: PacingStretch): boolean {
    return this.opened().has(stretch.checkpoint.end);
  }

  protected readonly allOpen = computed(() => this.stretches().every((stretch) => this.isOpen(stretch)));

  protected toggle(stretch: PacingStretch): void {
    const opened = new Set(this.opened());
    if (!opened.delete(stretch.checkpoint.end)) opened.add(stretch.checkpoint.end);
    this.opened.set(opened);
  }

  /** Every stretch's Splits, or none once they are all shown. */
  protected toggleAll(): void {
    this.opened.set(this.allOpen() ? new Set() : new Set(this.stretches().map((stretch) => stretch.checkpoint.end)));
  }

  protected type(field: Source, text: string): void {
    this.source.set(field);
    this.typed.set(text);
  }

  /** A slider moved: its field takes the figure, as if typed. */
  protected slide(field: Source, seconds: number): void {
    this.type(field, formatDuration(seconds));
  }

  protected readonly formatDuration = formatDuration;
}

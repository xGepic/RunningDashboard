import { Component, computed, inject, signal } from '@angular/core';
import { formatDuration, parseDuration } from '../shared/format';
import { Units } from '../shared/units';

/** The distances the calculator offers, in metres. */
export const raceDistances = [
  { name: '1 mile', distance: 1609.344 },
  { name: '5K', distance: 5000 },
  { name: '10K', distance: 10000 },
  { name: '15K', distance: 15000 },
  { name: '10 miles', distance: 16093.44 },
  { name: 'Half marathon', distance: 21097.5 },
  { name: 'Marathon', distance: 42195 },
  { name: '50K', distance: 50000 },
] as const;

/** The field the Athlete typed in or slid last; the other one is worked out from it. */
type Source = 'pace' | 'time';

/** The pace sliders' ends and where they start, in seconds per km. */
const slowestPace = 600;
const fastestPace = 150;
const startingPace = 330;

/**
 * A race distance's finish time at a steady pace, or the pace for a finish time. The pace is per km or mile, as the
 * Distance Unit says.
 */
@Component({
  selector: 'app-race-calculator-page',
  templateUrl: './race-calculator-page.html',
  styleUrl: './race-calculator-page.scss',
})
export class RaceCalculatorPage {
  protected readonly units = inject(Units);
  protected readonly distances = raceDistances;

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

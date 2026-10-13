import { HttpClient } from '@angular/common/http';
import { Injectable, inject, signal } from '@angular/core';
import { DistanceUnit, distanceIn, formatDistance, formatPace, formatPaceDifference, formatSpeed, formatTotalDistance, parseDistance } from './format';

/** As returned by GET/PUT /api/settings. */
export interface Settings {
  distanceUnit: DistanceUnit;
}

/**
 * The Athlete's Distance Unit, from Settings, and formatters that show distances, paces and speeds in it. The API
 * always speaks metres and seconds; only what is shown and typed follows the unit. Kilometres until the setting is in.
 */
@Injectable({ providedIn: 'root' })
export class Units {
  private readonly http = inject(HttpClient);

  readonly unit = signal<DistanceUnit>('km');

  /** Whether the Athlete has chosen a unit since the app started: the stored one, coming in later, must not undo it. */
  private chosen = false;

  constructor() {
    // Without the setting the dashboard still works, in kilometres; there is nothing more to show for it.
    this.http.get<Settings>('/api/settings').subscribe({
      next: (settings) => {
        if (!this.chosen) this.unit.set(settings.distanceUnit);
      },
      error: () => {},
    });
  }

  /** Shown at once; if it cannot be saved, the unit before comes back, unless another has been chosen since. */
  choose(unit: DistanceUnit): void {
    const before = this.unit();
    this.chosen = true;
    this.unit.set(unit);
    this.http.put<Settings>('/api/settings', { distanceUnit: unit }).subscribe({
      error: () => {
        if (this.unit() === unit) this.unit.set(before);
      },
    });
  }

  distance(metres: number, fractionDigits = 2): string {
    return formatDistance(metres, this.unit(), fractionDigits);
  }

  /** Whole kilometres or miles, e.g. "4,321 km". */
  totalDistance(metres: number): string {
    return formatTotalDistance(metres, this.unit());
  }

  pace(secondsPerKm: number | null): string {
    return formatPace(secondsPerKm, this.unit());
  }

  speed(kilometresPerHour: number | null): string {
    return formatSpeed(kilometresPerHour, this.unit());
  }

  /** Against the Target Pace: "16 s/mi slower". */
  paceDifference(secondsPerKm: number): string {
    return formatPaceDifference(secondsPerKm, this.unit());
  }

  /**
   * Metres from a distance the Athlete typed in the unit; null if it is not a positive number. A field left as
   * {@link inputValue} filled it from `original` gives back those metres, so saving it unchanged does not round it.
   */
  parse(text: string, original: number | null = null): number | null {
    if (original !== null && text.trim() === this.inputValue(original)) return original;
    return parseDistance(text, this.unit());
  }

  /** Metres in the unit, to one decimal, to fill a form field: "32.5". */
  inputValue(metres: number): string {
    return String(Math.round(this.fromMetres(metres) * 10) / 10);
  }

  /** A distance the Athlete typed in the unit, in metres. */
  toMetres(distance: number): number {
    return distance / distanceIn(1, this.unit());
  }

  /** Metres in the unit, unrounded, e.g. to fill a form. */
  fromMetres(metres: number): number {
    return distanceIn(metres, this.unit());
  }
}

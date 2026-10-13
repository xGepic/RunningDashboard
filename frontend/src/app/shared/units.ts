import { HttpClient } from '@angular/common/http';
import { Injectable, inject, signal } from '@angular/core';
import { DistanceUnit, distanceIn, formatDistance, formatPace, formatSpeed, formatTotalDistance } from './format';

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

  constructor() {
    this.http.get<Settings>('/api/settings').subscribe({ next: (settings) => this.unit.set(settings.distanceUnit), error: () => {} });
  }

  /** Shown at once; if it cannot be saved, the unit before comes back. */
  choose(unit: DistanceUnit): void {
    const before = this.unit();
    this.unit.set(unit);
    this.http.put<Settings>('/api/settings', { distanceUnit: unit }).subscribe({ error: () => this.unit.set(before) });
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

  /** A distance the Athlete typed in the unit, in metres. */
  toMetres(distance: number): number {
    return distance / distanceIn(1, this.unit());
  }

  /** Metres in the unit, unrounded, e.g. to fill a form. */
  fromMetres(metres: number): number {
    return distanceIn(metres, this.unit());
  }
}

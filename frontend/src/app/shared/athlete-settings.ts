import { HttpClient } from '@angular/common/http';
import { Injectable, computed, inject, signal } from '@angular/core';
import { DistanceUnit } from './format';

/** As returned by GET/PATCH /api/settings. */
export interface Settings {
  distanceUnit: DistanceUnit;
  /** Metres after which every Shoe is due to be retired. */
  shoeMaximumDistance: number;
}

/** What applies until the stored settings are in, or when they cannot be loaded. */
const defaults: Settings = { distanceUnit: 'km', shoeMaximumDistance: 700_000 };

/**
 * The Athlete's settings, loaded once as the app starts. A change shows at once and is saved in the background;
 * each setting is saved on its own.
 */
@Injectable({ providedIn: 'root' })
export class AthleteSettings {
  private readonly http = inject(HttpClient);
  private readonly current = signal<Settings>(defaults);

  /** Settings changed since the app started: the stored ones, coming in later, must not undo them. */
  private readonly changed = new Set<keyof Settings>();

  readonly distanceUnit = computed(() => this.current().distanceUnit);
  readonly shoeMaximumDistance = computed(() => this.current().shoeMaximumDistance);

  constructor() {
    // Without the settings the dashboard still works with the defaults; there is nothing more to show for it.
    this.http.get<Settings>('/api/settings').subscribe({
      next: (stored) => this.current.update((now) => ({ ...stored, ...pick(now, this.changed) })),
      error: () => {},
    });
  }

  /**
   * Shows the changes at once and saves them. If they cannot be saved, what was there before comes back, unless it
   * has been changed again since. Resolves to whether they were saved.
   */
  change(changes: Partial<Settings>): Promise<boolean> {
    const before = this.current();
    const keys = Object.keys(changes) as (keyof Settings)[];
    keys.forEach((key) => this.changed.add(key));
    this.current.update((now) => ({ ...now, ...changes }));
    return new Promise((resolve) =>
      this.http.patch<Settings>('/api/settings', changes).subscribe({
        next: () => resolve(true),
        error: () => {
          const undone = keys.filter((key) => this.current()[key] === changes[key]);
          this.current.update((now) => ({ ...now, ...pick(before, new Set(undone)) }));
          resolve(false);
        },
      }),
    );
  }
}

function pick(settings: Settings, keys: Set<keyof Settings>): Partial<Settings> {
  return Object.fromEntries([...keys].map((key) => [key, settings[key]]));
}

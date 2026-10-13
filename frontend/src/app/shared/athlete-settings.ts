import { DOCUMENT } from '@angular/common';
import { HttpClient } from '@angular/common/http';
import { Injectable, computed, inject, signal } from '@angular/core';
import { DistanceUnit } from './format';
import { Theme } from './themes';

/** As returned by GET/PATCH /api/settings. */
export interface Settings {
  distanceUnit: DistanceUnit;
  /** Metres after which every Shoe is due to be retired. */
  shoeMaximumDistance: number;
  theme: Theme;
}

/** What applies until the stored settings are in, or when they cannot be loaded. */
const defaults: Settings = { distanceUnit: 'km', shoeMaximumDistance: 700_000, theme: 'blue' };

/**
 * The Athlete's settings, loaded once as the app starts. A change shows at once and is saved in the background;
 * each setting is saved on its own.
 */
@Injectable({ providedIn: 'root' })
export class AthleteSettings {
  private readonly http = inject(HttpClient);
  private readonly document = inject(DOCUMENT);
  private readonly current = signal<Settings>(defaults);

  /** Settings changed since the app started: the stored ones, coming in later, must not undo them. */
  private readonly changed = new Set<keyof Settings>();

  readonly distanceUnit = computed(() => this.current().distanceUnit);
  readonly shoeMaximumDistance = computed(() => this.current().shoeMaximumDistance);
  readonly theme = computed(() => this.current().theme);

  constructor() {
    // Without the settings the dashboard still works with the defaults; there is nothing more to show for it.
    this.http.get<Settings>('/api/settings').subscribe({
      next: (stored) => this.show({ ...defaults, ...stored, ...pick(this.current(), this.changed) }),
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
    this.show({ ...this.current(), ...changes });
    return new Promise((resolve) =>
      this.http.patch<Settings>('/api/settings', changes).subscribe({
        next: () => resolve(true),
        error: () => {
          const undone = keys.filter((key) => this.current()[key] === changes[key]);
          this.show({ ...this.current(), ...pick(before, new Set(undone)) });
          resolve(false);
        },
      }),
    );
  }

  /** The Theme goes on the page together with the setting, so whatever then reads colours from CSS gets the Theme's. */
  private show(settings: Settings): void {
    this.current.set(settings);
    this.document.documentElement.dataset['theme'] = settings.theme;
  }
}

function pick(settings: Settings, keys: Set<keyof Settings>): Partial<Settings> {
  return Object.fromEntries([...keys].map((key) => [key, settings[key]]));
}

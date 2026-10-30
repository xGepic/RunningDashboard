import { DatePipe } from '@angular/common';
import { HttpClient, HttpErrorResponse, httpResource } from '@angular/common/http';
import { Component, computed, effect, inject, linkedSignal, signal, untracked } from '@angular/core';
import { Connection, StravaApp } from '../connection/connection';
import { AthleteSettings } from '../shared/athlete-settings';
import { FieldErrors, fieldErrorsOf, formError } from '../shared/field-errors';
import { DistanceUnit, formatWhole, unitNames } from '../shared/format';
import { Theme, themes } from '../shared/themes';
import { Units } from '../shared/units';
import { SyncIndicator } from '../sync/sync-indicator';
import { SyncState } from '../sync/sync-state';
import { Backup } from './backup';
import { RateLimit, StravaUsage } from './strava-usage';

/** Metres; the most the API takes as the Shoes' Maximum Distance. */
const maxShoeMaximumDistance = 5_000_000;

/** For "Enter the distance in miles, e.g. 435." */
const examples: Record<DistanceUnit, string> = { km: '700', mi: '435' };

/** The rows of the Strava API usage, each with its 15 minutes and then its day. */
const usageRows: { requests: RateLimit['requests']; label: string }[] = [
  { requests: 'read', label: 'Reads' },
  { requests: 'all', label: 'All requests' },
];
const usagePeriods: RateLimit['period'][] = ['fifteen-minutes', 'day'];

/** From this share of a limit on, it is nearly used up. */
const nearlyUsedUp = 0.8;

/**
 * How the dashboard is shown (the Distance Unit and the Theme), the Shoes' Maximum Distance, the link to Strava with how much
 * of its rate limits is used, and the backups.
 */
@Component({
  selector: 'app-settings-page',
  imports: [DatePipe, SyncIndicator],
  templateUrl: './settings-page.html',
  styleUrl: './settings-page.scss',
})
export class SettingsPage {
  protected readonly settings = inject(AthleteSettings);
  protected readonly units = inject(Units);
  protected readonly connection = httpResource<Connection>(() => '/api/connection');
  protected readonly backup = httpResource<Backup>(() => '/api/backup');
  protected readonly rateLimits = httpResource<StravaUsage>(() => '/api/strava/rate-limits');
  private readonly http = inject(HttpClient);

  protected readonly distanceUnits: DistanceUnit[] = ['km', 'mi'];

  protected readonly themes = themes;
  protected readonly theme = this.settings.theme;

  /** The Maximum Distance as typed, in the Distance Unit; filled again whenever the setting or the unit changes. */
  protected readonly maximumDistance = linkedSignal(() => this.units.inputValue(this.settings.shoeMaximumDistance()));
  protected readonly maximumDistanceError = signal<string | null>(null);
  protected readonly saving = signal(false);

  /** The Strava API app as typed; the Client ID starts as the one entered before, the secret is never sent back. */
  protected readonly clientId = linkedSignal(() => this.connection.hasValue() ? (this.connection.value().stravaApp?.clientId ?? '') : '');
  protected readonly clientSecret = signal('');
  protected readonly stravaAppErrors = signal<FieldErrors>({});
  protected readonly savingStravaApp = signal(false);
  protected readonly formError = formError;

  protected readonly backingUp = signal(false);
  /** Why "Back up now" failed, as the backend says; until the next try. */
  protected readonly backupError = signal<string | null>(null);

  protected readonly formatWhole = formatWhole;

  /** Reads and all requests, each with its 15 minutes and its day, and how close each is to its limit. */
  protected readonly usageRows = computed(() => {
    const limits = this.rateLimits.hasValue() ? this.rateLimits.value().limits : [];
    return usageRows
      .map(({ requests, label }) => ({
        label,
        cells: usagePeriods
          .map((period) => limits.find((l) => l.requests === requests && l.period === period))
          .filter((limit) => limit !== undefined)
          .map((limit) => ({
            ...limit,
            share: Math.min(100, (limit.used / limit.limit) * 100),
            level: limit.used >= limit.limit ? 'over' : limit.used >= limit.limit * nearlyUsedUp ? 'near' : null,
          })),
      }))
      .filter((row) => row.cells.length > 0);
  });

  constructor() {
    // A sync uses the limits up as it goes: read them again with every new sync status after the first.
    const sync = inject(SyncState);
    let first = true;
    effect(() => {
      sync.status();
      if (!first) untracked(() => this.rateLimits.reload());
      first = false;
    });
  }

  /** Shown at once; if it cannot be saved, the Theme before comes back, unless another has been picked since. */
  protected pickTheme(theme: Theme): void {
    void this.settings.change({ theme });
  }

  protected async saveMaximumDistance(): Promise<void> {
    const distance = this.units.parse(this.maximumDistance(), this.settings.shoeMaximumDistance());
    const unit = this.units.unit();
    if (distance === null) {
      this.maximumDistanceError.set(`Enter the distance in ${unitNames[unit]}, e.g. ${examples[unit]}.`);
      return;
    }
    if (distance > maxShoeMaximumDistance) {
      this.maximumDistanceError.set(`At most ${formatWhole(this.units.fromMetres(maxShoeMaximumDistance))} ${unit}.`);
      return;
    }
    this.maximumDistanceError.set(null);
    this.saving.set(true);
    const saved = await this.settings.change({ shoeMaximumDistance: distance });
    this.saving.set(false);
    if (!saved) this.maximumDistanceError.set('The Maximum Distance could not be saved.');
  }

  protected saveStravaApp(): void {
    this.savingStravaApp.set(true);
    this.stravaAppErrors.set({});
    this.http.put<StravaApp>('/api/strava/app', { clientId: this.clientId(), clientSecret: this.clientSecret() }).subscribe({
      next: (stravaApp) => {
        this.savingStravaApp.set(false);
        this.clientSecret.set('');
        this.connection.update((current) => current && { ...current, stravaApp });
      },
      error: (response: HttpErrorResponse) => {
        this.savingStravaApp.set(false);
        this.stravaAppErrors.set(fieldErrorsOf(response, 'The Strava API app could not be saved.'));
      },
    });
  }

  protected backUpNow(): void {
    this.backingUp.set(true);
    this.backupError.set(null);
    this.http.post<Backup>('/api/backup', null).subscribe({
      next: (backup) => {
        this.backingUp.set(false);
        this.backup.set(backup);
      },
      error: (error: HttpErrorResponse) => {
        this.backingUp.set(false);
        this.backupError.set(error.error?.detail ?? 'The backup could not be made.');
      },
    });
  }
}

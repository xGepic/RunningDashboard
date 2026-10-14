import { DatePipe } from '@angular/common';
import { HttpClient, HttpErrorResponse, httpResource } from '@angular/common/http';
import { Component, inject, linkedSignal, signal } from '@angular/core';
import { Connection } from '../connection/connection';
import { AthleteSettings } from '../shared/athlete-settings';
import { DistanceUnit, formatWhole, unitNames } from '../shared/format';
import { Theme, themes } from '../shared/themes';
import { Units } from '../shared/units';
import { SyncIndicator } from '../sync/sync-indicator';
import { Backup } from './backup';

/** Metres; the most the API takes as the Shoes' Maximum Distance. */
const maxShoeMaximumDistance = 5_000_000;

/** For "Enter the distance in miles, e.g. 435." */
const examples: Record<DistanceUnit, string> = { km: '700', mi: '435' };

/** How the dashboard is shown (the Distance Unit and the Theme), the Shoes' Maximum Distance, the link to Strava and the backups. */
@Component({
  selector: 'app-settings-page',
  imports: [DatePipe, SyncIndicator],
  templateUrl: './settings-page.html',
  styleUrl: './settings-page.scss',
})
export class SettingsPage {
  private readonly settings = inject(AthleteSettings);
  protected readonly units = inject(Units);
  protected readonly connection = httpResource<Connection>(() => '/api/connection');
  protected readonly backup = httpResource<Backup>(() => '/api/backup');
  private readonly http = inject(HttpClient);

  protected readonly distanceUnits: DistanceUnit[] = ['km', 'mi'];

  protected readonly themes = themes;
  protected readonly theme = this.settings.theme;

  /** The Maximum Distance as typed, in the Distance Unit; filled again whenever the setting or the unit changes. */
  protected readonly maximumDistance = linkedSignal(() => this.units.inputValue(this.settings.shoeMaximumDistance()));
  protected readonly maximumDistanceError = signal<string | null>(null);
  protected readonly saving = signal(false);

  protected readonly backingUp = signal(false);
  /** Why "Back up now" failed, as the backend says; until the next try. */
  protected readonly backupError = signal<string | null>(null);

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

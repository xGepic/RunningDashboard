import { DatePipe } from '@angular/common';
import { httpResource } from '@angular/common/http';
import { Component, inject, linkedSignal, signal } from '@angular/core';
import { Connection } from '../connection/connection';
import { AthleteSettings } from '../shared/athlete-settings';
import { DistanceUnit } from '../shared/format';
import { Units } from '../shared/units';
import { SyncIndicator } from '../sync/sync-indicator';

/** Metres; the most the API takes as the Shoes' Maximum Distance. */
const maxShoeMaximumDistance = 5_000_000;

/** For "Enter the distance in miles, e.g. 435." */
const unitNames: Record<DistanceUnit, { name: string; example: string }> = {
  km: { name: 'kilometres', example: '700' },
  mi: { name: 'miles', example: '435' },
};

const wholeNumber = new Intl.NumberFormat('en', { maximumFractionDigits: 0 });

/** How the dashboard is shown (the Distance Unit), the Shoes' Maximum Distance, and the link to Strava. */
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

  protected readonly distanceUnits: DistanceUnit[] = ['km', 'mi'];

  /** The Maximum Distance as typed, in the Distance Unit; filled again whenever the setting or the unit changes. */
  protected readonly maximumDistance = linkedSignal(() => this.units.inputValue(this.settings.shoeMaximumDistance()));
  protected readonly maximumDistanceError = signal<string | null>(null);
  protected readonly saving = signal(false);

  protected async saveMaximumDistance(): Promise<void> {
    const distance = this.units.parse(this.maximumDistance(), this.settings.shoeMaximumDistance());
    const unit = this.units.unit();
    if (distance === null) {
      this.maximumDistanceError.set(`Enter the distance in ${unitNames[unit].name}, e.g. ${unitNames[unit].example}.`);
      return;
    }
    if (distance > maxShoeMaximumDistance) {
      this.maximumDistanceError.set(`At most ${wholeNumber.format(this.units.fromMetres(maxShoeMaximumDistance))} ${unit}.`);
      return;
    }
    this.maximumDistanceError.set(null);
    this.saving.set(true);
    const saved = await this.settings.change({ shoeMaximumDistance: distance });
    this.saving.set(false);
    if (!saved) this.maximumDistanceError.set('The Maximum Distance could not be saved.');
  }
}

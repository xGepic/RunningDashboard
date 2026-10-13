import { DatePipe } from '@angular/common';
import { httpResource } from '@angular/common/http';
import { Component, inject } from '@angular/core';
import { Connection } from '../connection/connection';
import { DistanceUnit } from '../shared/format';
import { Units } from '../shared/units';
import { SyncIndicator } from '../sync/sync-indicator';

/** How the dashboard is shown (the Distance Unit) and its link to Strava. */
@Component({
  selector: 'app-settings-page',
  imports: [DatePipe, SyncIndicator],
  templateUrl: './settings-page.html',
  styleUrl: './settings-page.scss',
})
export class SettingsPage {
  protected readonly units = inject(Units);
  protected readonly connection = httpResource<Connection>(() => '/api/connection');

  protected readonly distanceUnits: DistanceUnit[] = ['km', 'mi'];
}

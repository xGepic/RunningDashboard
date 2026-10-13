import { DatePipe } from '@angular/common';
import { Component, booleanAttribute, inject, input } from '@angular/core';
import { SyncState } from './sync-state';

/**
 * Sync progress, pauses, errors and the actions that go with them. In full on the Settings page, with the last sync
 * and Sync now; compact in the app shell, where it only shows up while a sync runs or something went wrong.
 */
@Component({
  selector: 'app-sync-indicator',
  imports: [DatePipe],
  templateUrl: './sync-indicator.html',
  styleUrl: './sync-indicator.scss',
})
export class SyncIndicator {
  protected readonly sync = inject(SyncState);

  /** Only what needs the Athlete's attention: progress, errors and reconnecting. */
  readonly compact = input(false, { transform: booleanAttribute });
}

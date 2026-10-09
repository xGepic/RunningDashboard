import { DatePipe } from '@angular/common';
import { Component, inject } from '@angular/core';
import { SyncState } from './sync-state';

/** Sync progress, pauses, errors and the actions that go with them, shown in the app shell. */
@Component({
  selector: 'app-sync-indicator',
  imports: [DatePipe],
  templateUrl: './sync-indicator.html',
  styleUrl: './sync-indicator.scss',
})
export class SyncIndicator {
  protected readonly sync = inject(SyncState);
}

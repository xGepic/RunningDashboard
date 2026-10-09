import { Component, inject } from '@angular/core';
import { SyncState } from './sync-state';

@Component({
  selector: 'app-sync-button',
  templateUrl: './sync-button.html',
  styleUrl: './sync-button.scss',
})
export class SyncButton {
  protected readonly sync = inject(SyncState);
}

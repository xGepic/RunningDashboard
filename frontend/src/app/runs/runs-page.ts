import { DatePipe } from '@angular/common';
import { httpResource } from '@angular/common/http';
import { Component, effect, inject, untracked } from '@angular/core';
import { formatDistance, formatDuration, formatPace } from '../shared/format';
import { SyncState } from '../sync/sync-state';
import { Run } from './run';

@Component({
  selector: 'app-runs-page',
  imports: [DatePipe],
  templateUrl: './runs-page.html',
  styleUrl: './runs-page.scss',
})
export class RunsPage {
  private readonly sync = inject(SyncState);

  protected readonly runs = httpResource<Run[]>(() => '/api/runs');
  protected readonly formatDistance = formatDistance;
  protected readonly formatDuration = formatDuration;
  protected readonly formatPace = formatPace;

  constructor() {
    // Reload after each finished sync; the first pass only records where we start from.
    let seenLastSyncedAt: string | null | undefined;
    effect(() => {
      const lastSyncedAt = this.sync.lastSyncedAt();
      if (seenLastSyncedAt !== undefined && lastSyncedAt !== seenLastSyncedAt) untracked(() => this.runs.reload());
      seenLastSyncedAt = lastSyncedAt;
    });
  }
}

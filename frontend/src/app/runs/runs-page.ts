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
    // Reload whenever a sync stored more Runs or finished; the first pass only records where we start from.
    let seenProgress: string | undefined;
    effect(() => {
      const status = this.sync.status();
      const progress = `${status?.importedRuns}|${status?.lastSyncedAt}`;
      if (seenProgress !== undefined && progress !== seenProgress) untracked(() => this.runs.reload());
      seenProgress = progress;
    });
  }
}

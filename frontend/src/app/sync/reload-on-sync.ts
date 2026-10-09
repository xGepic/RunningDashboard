import { effect, inject, untracked } from '@angular/core';
import { SyncState } from './sync-state';

/**
 * Calls <code>reload</code> whenever a sync stored more Runs or finished, so a page fills up during the import.
 * Call from an injection context (a component constructor or field initialiser).
 */
export function reloadOnSync(reload: () => void): void {
  const sync = inject(SyncState);
  let seenProgress: string | undefined;
  effect(() => {
    const status = sync.status();
    const progress = `${status?.importedRuns}|${status?.lastSyncedAt}`;
    // The first pass only records where we start from.
    if (seenProgress !== undefined && progress !== seenProgress) untracked(reload);
    seenProgress = progress;
  });
}

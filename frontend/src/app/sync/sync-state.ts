import { DOCUMENT } from '@angular/common';
import { HttpClient } from '@angular/common/http';
import { DestroyRef, Injectable, computed, inject, signal } from '@angular/core';

/** As returned by GET/POST /api/sync. */
export interface SyncStatus {
  running: boolean;
  phase: 'idle' | 'importing-activities' | 'importing-details';
  /** Runs, Rides and Swims stored so far. */
  importedActivities: number;
  /** Runs stored so far: only they have details to load. */
  importedRuns: number;
  /** Runs whose Best Efforts and splits have been fetched. */
  detailedRuns: number;
  /** Set while the sync waits for Strava's rate limit to reset. */
  pausedUntil: string | null;
  lastSyncedAt: string | null;
  lastError: string | null;
  reconnectRequired: boolean;
}

const pollIntervalMs = 2000;
/** How old the last sync may be when the dashboard comes back into view; the backend syncs hourly besides. */
const staleAfterMs = 15 * 60 * 1000;

/**
 * The backend's sync status, polled while a sync runs. When the dashboard's tab comes back into view, a sync starts
 * if the last one is more than 15 minutes old, so a Run uploaded meanwhile shows up without "Sync now".
 */
@Injectable({ providedIn: 'root' })
export class SyncState {
  private readonly http = inject(HttpClient);
  private readonly document = inject(DOCUMENT);
  private pollTimer?: ReturnType<typeof setTimeout>;

  readonly status = signal<SyncStatus | null>(null);
  readonly running = computed(() => this.status()?.running ?? false);

  constructor() {
    this.refresh();
    const backInView = () => {
      if (this.document.visibilityState === 'visible') this.refresh((status) => this.syncIfStale(status));
    };
    this.document.addEventListener('visibilitychange', backInView);
    inject(DestroyRef).onDestroy(() => this.document.removeEventListener('visibilitychange', backInView));
  }

  syncNow(): void {
    this.http.post<SyncStatus>('/api/sync', null).subscribe({
      next: (status) => this.update(status),
      // e.g. 409 when Strava is not connected; show whatever the backend reports now.
      error: () => this.refresh(),
    });
  }

  /**
   * Syncs unless a sync runs, the last one is recent, Strava waits to be reconnected (it would only fail again) or
   * no sync ever finished (Strava may not be connected).
   */
  private syncIfStale(status: SyncStatus): void {
    if (status.running || status.reconnectRequired || status.lastSyncedAt === null) return;
    if (Date.now() - Date.parse(status.lastSyncedAt) > staleAfterMs) this.syncNow();
  }

  private refresh(then?: (status: SyncStatus) => void): void {
    this.http.get<SyncStatus>('/api/sync').subscribe({
      next: (status) => {
        this.update(status);
        then?.(status);
      },
      // A failed poll keeps the last status and tries again while that one says a sync runs, so it does not freeze there.
      error: () => this.pollWhileRunning(),
    });
  }

  private update(status: SyncStatus): void {
    this.status.set(status);
    this.pollWhileRunning();
  }

  private pollWhileRunning(): void {
    clearTimeout(this.pollTimer);
    if (this.running()) this.pollTimer = setTimeout(() => this.refresh(), pollIntervalMs);
  }
}

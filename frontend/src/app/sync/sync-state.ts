import { HttpClient } from '@angular/common/http';
import { Injectable, computed, inject, signal } from '@angular/core';

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

/** The backend's sync status, polled while a sync runs. */
@Injectable({ providedIn: 'root' })
export class SyncState {
  private readonly http = inject(HttpClient);
  private pollTimer?: ReturnType<typeof setTimeout>;

  readonly status = signal<SyncStatus | null>(null);
  readonly running = computed(() => this.status()?.running ?? false);

  constructor() {
    this.refresh();
  }

  syncNow(): void {
    this.http.post<SyncStatus>('/api/sync', null).subscribe({
      next: (status) => this.update(status),
      // e.g. 409 when Strava is not connected; show whatever the backend reports now.
      error: () => this.refresh(),
    });
  }

  private refresh(): void {
    this.http.get<SyncStatus>('/api/sync').subscribe({
      next: (status) => this.update(status),
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

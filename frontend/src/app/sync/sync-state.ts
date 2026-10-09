import { HttpClient } from '@angular/common/http';
import { Injectable, computed, inject, signal } from '@angular/core';

/** As returned by GET/POST /api/sync. */
export interface SyncStatus {
  running: boolean;
  lastSyncedAt: string | null;
}

const pollIntervalMs = 2000;

/** The backend's sync status, polled while a sync runs. */
@Injectable({ providedIn: 'root' })
export class SyncState {
  private readonly http = inject(HttpClient);
  private readonly status = signal<SyncStatus | null>(null);
  private pollTimer?: ReturnType<typeof setTimeout>;

  readonly running = computed(() => this.status()?.running ?? false);
  readonly lastSyncedAt = computed(() => this.status()?.lastSyncedAt ?? null);

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
    this.http.get<SyncStatus>('/api/sync').subscribe((status) => this.update(status));
  }

  private update(status: SyncStatus): void {
    this.status.set(status);
    clearTimeout(this.pollTimer);
    if (status.running) this.pollTimer = setTimeout(() => this.refresh(), pollIntervalMs);
  }
}

import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { SyncState, SyncStatus } from './sync-state';

describe('SyncState', () => {
  const idle: SyncStatus = {
    running: false,
    phase: 'idle',
    importedActivities: 565,
    importedRuns: 540,
    detailedRuns: 540,
    pausedUntil: null,
    lastSyncedAt: '2026-10-09T18:00:00+00:00',
    lastError: null,
    reconnectRequired: false,
  };
  const running: SyncStatus = { ...idle, running: true, phase: 'importing-activities', importedActivities: 337 };

  let http: HttpTestingController;
  let sync: SyncState;

  beforeEach(() => {
    vi.useFakeTimers();
    TestBed.configureTestingModule({ providers: [provideHttpClient(), provideHttpClientTesting()] });
    http = TestBed.inject(HttpTestingController);
    sync = TestBed.inject(SyncState);
  });

  afterEach(() => vi.useRealTimers());

  const fail = () => http.expectOne('/api/sync').flush(null, { status: 502, statusText: 'Bad Gateway' });

  it('polls while a sync runs, and stops once it is done', () => {
    http.expectOne('/api/sync').flush(running);
    vi.advanceTimersByTime(2000);
    http.expectOne('/api/sync').flush(idle);
    vi.advanceTimersByTime(2000);

    http.expectNone('/api/sync');
    expect(sync.running()).toBe(false);
  });

  it('keeps polling after a failed poll while a sync runs, so its progress does not freeze', () => {
    http.expectOne('/api/sync').flush(running);
    vi.advanceTimersByTime(2000);
    fail();
    vi.advanceTimersByTime(2000);
    http.expectOne('/api/sync').flush({ ...running, importedActivities: 400 });

    expect(sync.status()?.importedActivities).toBe(400);
  });

  it('does not poll after a failed request when no sync was running', () => {
    fail();
    vi.advanceTimersByTime(2000);

    http.expectNone('/api/sync');
    expect(sync.status()).toBeNull();
  });
});

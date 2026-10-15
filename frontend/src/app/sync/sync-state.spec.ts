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

  afterEach(() => {
    vi.restoreAllMocks();
    vi.useRealTimers();
  });

  const fail = () => http.expectOne('/api/sync').flush(null, { status: 502, statusText: 'Bad Gateway' });

  /** The dashboard's tab comes back into view at <time> (UTC) on the day of the last sync in `idle`. */
  const backInViewAt = (time: string) => {
    vi.setSystemTime(new Date(`2026-10-09T${time}:00Z`));
    vi.spyOn(document, 'visibilityState', 'get').mockReturnValue('visible');
    document.dispatchEvent(new Event('visibilitychange'));
  };
  const expectSyncStarted = () => http.expectOne({ url: '/api/sync', method: 'POST' }).flush(running);

  it('syncs when the tab comes back into view more than 15 minutes after the last sync', () => {
    http.expectOne('/api/sync').flush(idle);

    backInViewAt('18:16');
    http.expectOne({ url: '/api/sync', method: 'GET' }).flush(idle);

    expectSyncStarted();
    expect(sync.running()).toBe(true);
  });

  it('does not sync when the tab comes back into view soon after the last sync', () => {
    http.expectOne('/api/sync').flush(idle);

    backInViewAt('18:14');
    http.expectOne({ url: '/api/sync', method: 'GET' }).flush(idle);

    http.expectNone('/api/sync');
  });

  it('does not sync when the tab comes back into view while Strava waits to be reconnected', () => {
    http.expectOne('/api/sync').flush(idle);

    backInViewAt('20:00');
    http.expectOne({ url: '/api/sync', method: 'GET' }).flush({ ...idle, reconnectRequired: true });

    http.expectNone('/api/sync');
  });

  it('does not sync when the tab comes back into view before any sync finished', () => {
    http.expectOne('/api/sync').flush({ ...idle, lastSyncedAt: null });

    backInViewAt('20:00');
    http.expectOne({ url: '/api/sync', method: 'GET' }).flush({ ...idle, lastSyncedAt: null });

    http.expectNone('/api/sync');
  });

  it('does not start a second sync when the tab comes back into view during one', () => {
    http.expectOne('/api/sync').flush(idle);

    backInViewAt('20:00');
    http.expectOne({ url: '/api/sync', method: 'GET' }).flush({ ...running, lastSyncedAt: idle.lastSyncedAt });

    http.expectNone({ url: '/api/sync', method: 'POST' });
  });

  it('does nothing when the tab is hidden', () => {
    http.expectOne('/api/sync').flush(idle);
    vi.setSystemTime(new Date('2026-10-09T20:00:00Z'));
    vi.spyOn(document, 'visibilityState', 'get').mockReturnValue('hidden');

    document.dispatchEvent(new Event('visibilitychange'));

    http.expectNone('/api/sync');
  });

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

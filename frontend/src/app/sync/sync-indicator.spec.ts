import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { SyncIndicator } from './sync-indicator';
import { SyncStatus } from './sync-state';

describe('SyncIndicator', () => {
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

  async function render(status: SyncStatus, compact = false): Promise<HTMLElement> {
    TestBed.configureTestingModule({
      imports: [SyncIndicator],
      providers: [provideHttpClient(), provideHttpClientTesting()],
    });
    const fixture = TestBed.createComponent(SyncIndicator);
    fixture.componentRef.setInput('compact', compact);
    fixture.detectChanges();
    TestBed.inject(HttpTestingController).expectOne('/api/sync').flush(status);
    await fixture.whenStable();
    return fixture.nativeElement as HTMLElement;
  }

  it('shows import progress and a rate-limit pause while syncing', async () => {
    const element = await render({
      ...idle,
      running: true,
      phase: 'importing-activities',
      importedActivities: 337,
      importedRuns: 312,
      pausedUntil: '2026-10-09T18:15:00+00:00',
    });

    // Runs, Rides and Swims alike.
    expect(element.textContent).toContain('Importing activities… 337 activities');
    expect(element.textContent).toContain('Paused for Strava’s rate limit until');
    expect(element.querySelector('button')?.disabled).toBe(true);
  });

  it('shows how many Runs are detailed while Run details load', async () => {
    const element = await render({
      ...idle,
      running: true,
      phase: 'importing-details',
      importedRuns: 540,
      detailedRuns: 312,
    });

    expect(element.textContent).toContain('Loading Run details');
    expect(element.textContent).toContain('312 of 540 Runs detailed');
  });

  it('asks to reconnect Strava when access was lost', async () => {
    const element = await render({ ...idle, reconnectRequired: true, lastError: 'Strava no longer accepts the app’s access.' });

    const link = element.querySelector('a');
    expect(link?.textContent).toContain('Reconnect Strava');
    expect(link?.getAttribute('href')).toBe('/api/strava/connect');
    expect(element.querySelector('button')).toBeNull();
  });

  it('shows the last error next to Sync now when idle', async () => {
    const element = await render({ ...idle, lastError: 'Sync failed: Strava answered 500.' });

    expect(element.textContent).toContain('Sync failed: Strava answered 500.');
    expect(element.querySelector('button')?.textContent).toContain('Sync now');
  });

  describe('compact, in the top bar', () => {
    it('shows nothing while all is well: the last sync and Sync now are on the Settings page', async () => {
      const element = await render({ ...idle, detailedRuns: 500 }, true);

      expect(element.textContent!.trim()).toBe('');
      expect(element.querySelector('button')).toBeNull();
    });

    it('shows the progress while syncing, without a button', async () => {
      const element = await render({ ...idle, running: true, phase: 'importing-activities', importedActivities: 337 }, true);

      expect(element.textContent).toContain('Importing activities… 337 activities');
      expect(element.querySelector('button')).toBeNull();
    });

    it('shows the last error, and asks to reconnect Strava when access was lost', async () => {
      expect((await render({ ...idle, lastError: 'Sync failed: Strava answered 500.' }, true)).textContent).toContain('Sync failed');

      TestBed.resetTestingModule();
      const lost = await render({ ...idle, reconnectRequired: true, lastError: 'Strava no longer accepts the app’s access.' }, true);
      expect(lost.querySelector('a')?.getAttribute('href')).toBe('/api/strava/connect');
    });
  });
});

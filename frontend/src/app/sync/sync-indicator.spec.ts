import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { SyncIndicator } from './sync-indicator';
import { SyncStatus } from './sync-state';

describe('SyncIndicator', () => {
  const idle: SyncStatus = {
    running: false,
    phase: 'idle',
    importedRuns: 540,
    detailedRuns: 540,
    pausedUntil: null,
    lastSyncedAt: '2026-10-09T18:00:00+00:00',
    lastError: null,
    reconnectRequired: false,
  };

  async function render(status: SyncStatus): Promise<HTMLElement> {
    TestBed.configureTestingModule({
      imports: [SyncIndicator],
      providers: [provideHttpClient(), provideHttpClientTesting()],
    });
    const fixture = TestBed.createComponent(SyncIndicator);
    fixture.detectChanges();
    TestBed.inject(HttpTestingController).expectOne('/api/sync').flush(status);
    await fixture.whenStable();
    return fixture.nativeElement as HTMLElement;
  }

  it('shows import progress and a rate-limit pause while syncing', async () => {
    const element = await render({
      ...idle,
      running: true,
      phase: 'importing-runs',
      importedRuns: 312,
      pausedUntil: '2026-10-09T18:15:00+00:00',
    });

    expect(element.textContent).toContain('Importing Runs');
    expect(element.textContent).toContain('312 Runs');
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
});

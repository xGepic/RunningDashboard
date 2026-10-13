import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { Connection } from '../connection/connection';
import { SyncStatus } from '../sync/sync-state';
import { SettingsPage } from './settings-page';

const connected: Connection = {
  connected: true,
  athlete: { id: 1, firstName: 'Stefan', lastName: 'Simanek' },
  connectedAt: '2026-10-09T08:00:00+00:00',
};

const idle: SyncStatus = {
  running: false,
  phase: 'idle',
  importedActivities: 565,
  importedRuns: 540,
  detailedRuns: 540,
  pausedUntil: null,
  lastSyncedAt: '2026-10-13T07:30:00+00:00',
  lastError: null,
  reconnectRequired: false,
};

describe('SettingsPage', () => {
  async function render(unit: 'km' | 'mi', connection: Connection = connected) {
    TestBed.configureTestingModule({ imports: [SettingsPage], providers: [provideHttpClient(), provideHttpClientTesting()] });
    const fixture = TestBed.createComponent(SettingsPage);
    const http = TestBed.inject(HttpTestingController);
    fixture.detectChanges();
    http.expectOne('/api/settings').flush({ distanceUnit: unit });
    http.expectOne('/api/connection').flush(connection);
    await fixture.whenStable();
    fixture.detectChanges();
    // The sync status is asked for once the connection shows.
    for (const sync of http.match('/api/sync')) sync.flush(idle);
    fixture.detectChanges();
    return { fixture, http, element: fixture.nativeElement as HTMLElement };
  }

  const unitButtons = (element: HTMLElement) => [...element.querySelectorAll<HTMLButtonElement>('[aria-label="Distance Unit"] button')];
  const pressed = (element: HTMLElement) => unitButtons(element).find((b) => b.getAttribute('aria-pressed') === 'true')?.textContent?.trim();

  it('shows the stored Distance Unit as chosen', async () => {
    const { element } = await render('mi');

    expect(unitButtons(element).map((b) => b.textContent!.trim())).toEqual(['km', 'mi']);
    expect(pressed(element)).toBe('mi');
  });

  it('saves a newly chosen unit and shows it at once', async () => {
    const { fixture, http, element } = await render('km');

    unitButtons(element)[1].click();
    fixture.detectChanges();

    expect(pressed(element)).toBe('mi');
    expect(http.expectOne((r) => r.method === 'PUT' && r.url === '/api/settings').request.body).toEqual({ distanceUnit: 'mi' });
  });

  it('shows who Strava is connected as, since when, and the last sync', async () => {
    const { element } = await render('km');

    const strava = element.querySelector('.strava')!.textContent!;
    expect(strava).toContain('Connected as Stefan Simanek');
    expect(strava).toContain('since 9 Oct 2026');
    expect(strava).toContain('Last synced');
  });

  it('offers to connect Strava when it is not connected', async () => {
    const { element } = await render('km', { connected: false, athlete: null, connectedAt: null });

    expect(element.querySelector<HTMLAnchorElement>('.strava a[href="/api/strava/connect"]')?.textContent?.trim()).toBe('Connect Strava');
  });
});

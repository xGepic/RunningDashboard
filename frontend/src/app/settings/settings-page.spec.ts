import { formatDate } from '@angular/common';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { Connection } from '../connection/connection';
import { SyncState, SyncStatus } from '../sync/sync-state';
import { Backup } from './backup';
import { SettingsPage } from './settings-page';
import { StravaUsage } from './strava-usage';

const connected: Connection = {
  connected: true,
  athlete: { id: 1, firstName: 'Stefan', lastName: 'Simanek' },
  connectedAt: '2026-10-09T08:00:00+00:00',
  stravaApp: { clientId: '12345', inConfiguration: true },
};

const notConnected: Connection = { connected: false, athlete: null, connectedAt: null, stravaApp: { clientId: '12345', inConfiguration: true } };

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

const backedUp: Backup = { folder: 'E:\\Backups\\RunningDashboard', lastBackupAt: '2026-10-14T07:05:00+00:00', lastError: null };

const reported: StravaUsage = {
  observedAt: '2026-10-14T18:07:00+00:00',
  limits: [
    { requests: 'read', period: 'fifteen-minutes', used: 85, limit: 100, resetsAt: '2026-10-14T18:15:00+00:00' },
    { requests: 'read', period: 'day', used: 350, limit: 1000, resetsAt: '2026-10-15T00:00:00+00:00' },
    { requests: 'all', period: 'fifteen-minutes', used: 86, limit: 200, resetsAt: '2026-10-14T18:15:00+00:00' },
    { requests: 'all', period: 'day', used: 2000, limit: 2000, resetsAt: '2026-10-15T00:00:00+00:00' },
  ],
};

describe('SettingsPage', () => {
  afterEach(() => delete document.documentElement.dataset['theme']);

  async function render(
    unit: 'km' | 'mi',
    connection: Connection = connected,
    shoeMaximumDistance = 700_000,
    theme = 'blue',
    backup = backedUp,
    usage = reported,
  ) {
    TestBed.configureTestingModule({ imports: [SettingsPage], providers: [provideHttpClient(), provideHttpClientTesting()] });
    const fixture = TestBed.createComponent(SettingsPage);
    const http = TestBed.inject(HttpTestingController);
    fixture.detectChanges();
    http.expectOne('/api/settings').flush({ distanceUnit: unit, shoeMaximumDistance, theme });
    http.expectOne('/api/connection').flush(connection);
    http.expectOne('/api/backup').flush(backup);
    http.expectOne('/api/strava/rate-limits').flush(usage);
    await fixture.whenStable();
    fixture.detectChanges();
    // The sync status is asked for once the connection shows.
    for (const sync of http.match('/api/sync')) sync.flush(idle);
    fixture.detectChanges();
    // A new sync status reads Strava's usage again.
    for (const again of http.match('/api/strava/rate-limits')) again.flush(usage);
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
    expect(http.expectOne((r) => r.method === 'PATCH' && r.url === '/api/settings').request.body).toEqual({ distanceUnit: 'mi' });
  });

  describe('the Theme', () => {
    const themeButtons = (element: HTMLElement) => [...element.querySelectorAll<HTMLButtonElement>('[aria-label="Theme"] button')];
    const picked = (element: HTMLElement) => themeButtons(element).find((b) => b.getAttribute('aria-pressed') === 'true')?.textContent?.trim();

    it('shows the stored Theme as picked, and on the page', async () => {
      const { element } = await render('km', connected, 700_000, 'purple');

      expect(themeButtons(element).map((b) => b.textContent!.trim())).toEqual(['Blue', 'Teal', 'Green', 'Orange', 'Red', 'Pink', 'Purple']);
      expect(picked(element)).toBe('Purple');
      expect(document.documentElement.dataset['theme']).toBe('purple');
    });

    it('saves a newly picked Theme and shows it at once', async () => {
      const { fixture, http, element } = await render('km');

      themeButtons(element)[3].click();
      fixture.detectChanges();

      expect(picked(element)).toBe('Orange');
      expect(document.documentElement.dataset['theme']).toBe('orange');
      expect(http.expectOne((r) => r.method === 'PATCH' && r.url === '/api/settings').request.body).toEqual({ theme: 'orange' });
    });

    it('goes back to the Theme before when it could not be saved', async () => {
      const { fixture, http, element } = await render('km');

      themeButtons(element)[3].click();
      http.expectOne((r) => r.method === 'PATCH').flush('down', { status: 502, statusText: 'Bad Gateway' });
      await fixture.whenStable();
      fixture.detectChanges();

      expect(picked(element)).toBe('Blue');
      expect(document.documentElement.dataset['theme']).toBe('blue');
    });
  });

  it('shows who Strava is connected as, since when, and the last sync', async () => {
    const { element } = await render('km');

    const strava = element.querySelector('.strava')!.textContent!;
    expect(strava).toContain('Connected as Stefan Simanek');
    expect(strava).toContain('since 9 Oct 2026');
    expect(strava).toContain('Last synced');
  });

  it('offers to connect Strava when it is not connected', async () => {
    const { element } = await render('km', notConnected);

    expect(element.querySelector<HTMLAnchorElement>('.strava a[href="/api/strava/connect"]')?.textContent?.trim()).toBe('Connect Strava');
    // The Strava API app is in the backend's configuration: nothing to enter.
    expect(element.querySelector('.strava form')).toBeNull();
  });

  describe('the Strava API app', () => {
    const connectLink = (element: HTMLElement) => element.querySelector('.strava a[href="/api/strava/connect"]');
    const input = (element: HTMLElement, label: string) =>
      [...element.querySelectorAll<HTMLLabelElement>('.strava label')].find((l) => l.textContent!.includes(label))!.querySelector('input')!;
    const enter = (element: HTMLElement, clientId: string, clientSecret: string) => {
      input(element, 'Client ID').value = clientId;
      input(element, 'Client ID').dispatchEvent(new Event('input'));
      input(element, 'Client Secret').value = clientSecret;
      input(element, 'Client Secret').dispatchEvent(new Event('input'));
      element.querySelector<HTMLFormElement>('.strava form')!.dispatchEvent(new Event('submit'));
    };

    it('asks for one before Strava can be connected', async () => {
      const { element } = await render('km', { ...notConnected, stravaApp: null });

      expect(element.querySelector('.strava')!.textContent).toContain('localhost');
      expect(input(element, 'Client ID').value).toBe('');
      expect(connectLink(element)).toBeNull();
    });

    it('saves the one entered, then offers to connect Strava', async () => {
      const { fixture, http, element } = await render('km', { ...notConnected, stravaApp: null });

      enter(element, '67890', 'entered-secret');
      fixture.detectChanges();
      const put = http.expectOne((r) => r.method === 'PUT' && r.url === '/api/strava/app');
      expect(put.request.body).toEqual({ clientId: '67890', clientSecret: 'entered-secret' });
      put.flush({ clientId: '67890', inConfiguration: false });
      fixture.detectChanges();

      expect(connectLink(element)?.textContent?.trim()).toBe('Connect Strava');
      expect(input(element, 'Client ID').value).toBe('67890');
      expect(input(element, 'Client Secret').value).toBe('');
    });

    it('shows the one entered before, to change it if Strava does not accept it', async () => {
      const { element } = await render('km', { ...notConnected, stravaApp: { clientId: '67890', inConfiguration: false } });

      expect(input(element, 'Client ID').value).toBe('67890');
      expect(input(element, 'Client Secret').placeholder).toContain('Saved');
      expect(connectLink(element)).not.toBeNull();
    });

    it('shows what the backend refused, by field', async () => {
      const { fixture, http, element } = await render('km', { ...notConnected, stravaApp: null });

      enter(element, 'my app', '');
      http.expectOne((r) => r.method === 'PUT').flush(
        { errors: { clientId: ['The Client ID is the number Strava shows for the API app.'] } },
        { status: 400, statusText: 'Bad Request' },
      );
      fixture.detectChanges();

      expect(input(element, 'Client ID').closest('label')!.textContent).toContain('The Client ID is the number Strava shows');
      expect(connectLink(element)).toBeNull();
    });
  });

  describe('the Maximum Distance of the Shoes', () => {
    const field = (element: HTMLElement) => element.querySelector<HTMLInputElement>('.shoes input')!;
    const type = async (fixture: { whenStable: () => Promise<unknown> }, element: HTMLElement, text: string) => {
      field(element).value = text;
      field(element).dispatchEvent(new Event('input'));
      element.querySelector<HTMLFormElement>('.shoes form')!.dispatchEvent(new Event('submit'));
      await fixture.whenStable();
    };

    it('shows it in the Distance Unit', async () => {
      const { element } = await render('mi');

      // 700 km is 434.96 mi.
      expect(field(element).value).toBe('435');
      expect(element.querySelector('.shoes label')!.textContent).toContain('Maximum Distance (mi)');
    });

    it('saves a typed distance as metres', async () => {
      const { fixture, http, element } = await render('km');

      await type(fixture, element, '800');

      expect(http.expectOne((r) => r.method === 'PATCH').request.body).toEqual({ shoeMaximumDistance: 800_000 });
    });

    it('keeps its metres when saved as it was, though the field shows it rounded', async () => {
      const { fixture, http, element } = await render('mi');

      await type(fixture, element, '435');

      expect(http.expectOne((r) => r.method === 'PATCH').request.body).toEqual({ shoeMaximumDistance: 700_000 });
    });

    it('does not send a distance it cannot read, or one beyond 5,000 km', async () => {
      const { fixture, http, element } = await render('km');

      await type(fixture, element, 'far');
      expect(element.querySelector('.shoes .field-error')?.textContent).toContain('Enter the distance in kilometres, e.g. 700.');
      await type(fixture, element, '6000');
      expect(element.querySelector('.shoes .field-error')?.textContent).toContain('At most 5,000 km.');

      http.expectNone((r) => r.method === 'PATCH');
    });

    it('says when it could not be saved, and shows the distance before again', async () => {
      const { fixture, http, element } = await render('km');

      await type(fixture, element, '800');
      http.expectOne((r) => r.method === 'PATCH').flush('down', { status: 502, statusText: 'Bad Gateway' });
      await fixture.whenStable();
      fixture.detectChanges();

      expect(element.querySelector('.shoes [role="alert"]')?.textContent).toContain('could not be saved');
      expect(field(element).value).toBe('700');
    });
  });

  describe('the Strava API usage', () => {
    const rows = (element: HTMLElement) =>
      [...element.querySelectorAll('.usage tbody tr')].map((row) =>
        [...row.querySelectorAll('th, td')].map((cell) =>
          [...cell.childNodes].map((part) => part.textContent!.trim()).filter((text) => text).join(' '),
        ),
      );
    const tracks = (element: HTMLElement) => [...element.querySelectorAll('.usage .track')].map((t) => t.className.replace('track', '').trim());
    const time = (iso: string) => formatDate(iso, 'HH:mm', 'en-US');

    it('shows how much of each of Strava\'s limits is used, and when it resets', async () => {
      const { element } = await render('km');

      expect(element.querySelector('.usage thead')!.textContent).toContain('15 minutes');
      expect(element.querySelector('.usage thead')!.textContent).toContain('Day (UTC)');
      expect(rows(element)).toEqual([
        ['Reads', `85 / 100 resets ${time('2026-10-14T18:15:00+00:00')}`, `350 / 1,000 resets ${time('2026-10-15T00:00:00+00:00')}`],
        ['All requests', `86 / 200 resets ${time('2026-10-14T18:15:00+00:00')}`, `2,000 / 2,000 resets ${time('2026-10-15T00:00:00+00:00')}`],
      ]);
      expect(element.querySelector('.usage')!.textContent).toContain(`as Strava reported it at ${time('2026-10-14T18:07:00+00:00')}`);
    });

    it('marks a limit nearly used up, and one used up', async () => {
      const { element } = await render('km');

      expect(tracks(element)).toEqual(['near', '', '', 'over']);
    });

    it('says it is not known before Strava has answered', async () => {
      const { element } = await render('km', connected, 700_000, 'blue', backedUp, { observedAt: null, limits: [] });

      expect(element.querySelector('.usage table')).toBeNull();
      expect(element.querySelector('.usage')!.textContent).toContain('Not known yet');
    });

    it('reads it again with each new sync status, as a sync uses it up', async () => {
      const { fixture, http, element } = await render('km');

      TestBed.inject(SyncState).status.set({ ...idle, running: true, phase: 'importing-details' });
      fixture.detectChanges();
      const nearlyUsedUp = { ...reported.limits[0], used: 99 };
      http.expectOne('/api/strava/rate-limits').flush({ ...reported, limits: [nearlyUsedUp, ...reported.limits.slice(1)] });
      await fixture.whenStable();
      fixture.detectChanges();

      expect(rows(element)[0][1]).toContain('99 / 100');
    });

    it('is not shown before Strava is connected', async () => {
      const { element } = await render('km', notConnected);

      expect(element.querySelector('.usage')).toBeNull();
    });
  });

  describe('the backups', () => {
    async function showBackups(backup: Backup) {
      const rendered = await render('km', connected, 700_000, 'blue', backup);
      return { ...rendered, card: rendered.element.querySelector('.backup')! };
    }

    const facts = (card: Element) => [...card.querySelectorAll('.backup-details div')].map((d) => [d.querySelector('dt')!.textContent, d.querySelector('dd')!.textContent]);
    const button = (card: Element) => card.querySelector<HTMLButtonElement>('button')!;

    it('shows the folder and when the last backup was made', async () => {
      const { card } = await showBackups(backedUp);

      expect(facts(card)[0]).toEqual(['Folder', 'E:\\Backups\\RunningDashboard']);
      expect(facts(card)[1][0]).toBe('Last backup');
      expect(facts(card)[1][1]).toContain('14 Oct 2026');
      expect(card.querySelector('[role="alert"]')).toBeNull();
    });

    it('says when there is none yet, and why the last one failed', async () => {
      const { card } = await showBackups({ ...backedUp, lastBackupAt: null, lastError: 'The backup could not be written to E:\\Backups: Access denied.' });

      expect(facts(card)[1]).toEqual(['Last backup', 'None yet']);
      expect(card.querySelector('[role="alert"]')!.textContent).toBe('The backup could not be written to E:\\Backups: Access denied.');
    });

    it('backs up now, and shows the new backup', async () => {
      const { fixture, http, card } = await showBackups({ ...backedUp, lastBackupAt: null });

      button(card).click();
      fixture.detectChanges();
      expect(button(card).disabled).toBe(true);
      http.expectOne((r) => r.method === 'POST' && r.url === '/api/backup').flush(backedUp);
      fixture.detectChanges();

      expect(button(card).disabled).toBe(false);
      expect(facts(card)[1][1]).toContain('14 Oct 2026');
    });

    it('says why backing up now failed, as the backend tells', async () => {
      const { fixture, http, card } = await showBackups(backedUp);

      button(card).click();
      http.expectOne((r) => r.method === 'POST' && r.url === '/api/backup').flush(
        { detail: 'The backup could not be written to E:\\Backups: Disk full.' },
        { status: 500, statusText: 'Internal Server Error' },
      );
      fixture.detectChanges();

      expect(card.querySelector('[role="alert"]')!.textContent).toBe('The backup could not be written to E:\\Backups: Disk full.');
      expect(button(card).disabled).toBe(false);
    });
  });
});

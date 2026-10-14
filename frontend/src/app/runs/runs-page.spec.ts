import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { Router, provideRouter, withComponentInputBinding } from '@angular/router';
import { RouterTestingHarness } from '@angular/router/testing';
import { ApplicationRef } from '@angular/core';
import { Run, RunPage } from './run';
import { RunExport } from './run-export';
import { RunsPage } from './runs-page';

const run = (id: number, name: string, startLocal: string, distance: number, movingTime: number): Run => ({
  id, name, sportType: 'Run', startLocal, distance, movingTime, pace: movingTime / (distance / 1000), shoeName: null,
});

/** October 2026 up to Mon 12 Oct. */
const october: RunPage = {
  items: [run(2, 'Easy Sunday', '2026-10-11T08:00:00', 8000, 2880), run(1, 'Tempo Tuesday', '2026-10-06T18:00:00', 10000, 3000)],
  page: 1,
  pageSize: 200,
  totalCount: 2,
};

describe('RunsPage', () => {
  beforeEach(() => {
    vi.useFakeTimers({ toFake: ['Date'] });
    vi.setSystemTime(new Date(2026, 9, 12, 9));
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        provideRouter([{ path: 'runs', component: RunsPage }], withComponentInputBinding()),
      ],
    });
  });

  afterEach(() => vi.useRealTimers());

  /** `page` 'error' answers the Runs with a server error. */
  async function open(url: string, page: RunPage | 'error' = october, unit: 'km' | 'mi' = 'km') {
    const harness = await RouterTestingHarness.create();
    const http = TestBed.inject(HttpTestingController);
    await harness.navigateByUrl(url);
    for (const sync of http.match('/api/sync')) sync.flush({ running: false, importedActivities: 0, lastSyncedAt: null });
    for (const settings of http.match('/api/settings')) settings.flush({ distanceUnit: unit });
    harness.detectChanges();
    const request = http.expectOne((r) => r.url === '/api/runs');
    if (page === 'error') request.flush(null, { status: 500, statusText: 'Server Error' });
    else request.flush(page);
    await new Promise((resolve) => setTimeout(resolve));
    harness.detectChanges();
    return { http, element: harness.routeNativeElement!, request: request.request };
  }

  const names = (element: HTMLElement) => [...element.querySelectorAll('.runs tbody tr .name a')].map((a) => a.textContent!.trim());

  it("lists the month's Runs, with no monthly totals view any more", async () => {
    const { http, element, request } = await open('/runs');

    expect([request.params.get('from'), request.params.get('to')]).toEqual(['2026-10-01', '2026-10-31']);
    expect(names(element)).toEqual(['Easy Sunday', 'Tempo Tuesday']);
    expect([...element.querySelectorAll('button')].map((b) => b.textContent!.trim())).not.toContain('Months');
    http.expectNone((r) => r.url === '/api/runs/months');
  });

  it('says so when the Runs cannot be loaded, without totals', async () => {
    const { element } = await open('/runs', 'error');

    expect(element.querySelector('[role="alert"]')!.textContent).toContain('Runs could not be loaded');
    expect(element.querySelector('.summary')).toBeNull();
  });

  it('always lists all Runs, whatever type an old link asks for', async () => {
    const { element, request } = await open('/runs?sportType=TrailRun');

    expect(request.params.has('sportType')).toBe(false);
    expect(element.querySelector('[aria-label="Type"]')).toBeNull();
  });

  it('shows the list for an old link to the monthly totals', async () => {
    const { http, element } = await open('/runs?view=months&year=2025');

    expect(names(element)).toEqual(['Easy Sunday', 'Tempo Tuesday']);
    http.expectNone((r) => r.url === '/api/runs/months');
  });

  describe('the Distance Unit', () => {
    const firstRow = (element: HTMLElement) => [...element.querySelectorAll('.runs tbody tr')][0].querySelectorAll('td');

    it('comes from Settings: distances and paces in miles, with no switch of its own', async () => {
      const { element } = await open('/runs', october, 'mi');

      const cells = firstRow(element);
      expect(cells[2].textContent!.trim()).toBe('4.97 mi');
      expect(cells[4].textContent!.trim()).toBe('9:39 /mi');
      expect(element.querySelector('.summary')!.textContent).toContain('11.2 mi');
      expect(element.querySelector('[aria-label="Units"]')).toBeNull();
    });

    it('ignores the unit of an old link', async () => {
      const { element } = await open('/runs?unit=mi');

      expect(firstRow(element)[2].textContent!.trim()).toBe('8.00 km');
    });
  });

  describe('the Today button', () => {
    const today = (element: HTMLElement) =>
      [...element.querySelectorAll<HTMLButtonElement>('.period-nav button')].find((b) => b.textContent!.trim() === 'Today')!;

    it('is off on the current month', async () => {
      const { element } = await open('/runs');

      expect(today(element).disabled).toBe(true);
    });

    it('goes back to the current month from an earlier one, keeping the search', async () => {
      const { http, element } = await open('/runs?month=2026-03&search=Lauf');

      today(element).click();
      await new Promise((resolve) => setTimeout(resolve));
      const request = http.expectOne((r) => r.url === '/api/runs');

      expect([request.request.params.get('from'), request.request.params.get('search')]).toEqual(['2026-10-01', 'Lauf']);
    });
  });

  describe('a Training Week', () => {
    const period = (request: { params: { get(name: string): string | null } }) => [request.params.get('from'), request.params.get('to')];
    const navButton = (element: HTMLElement, label: string) =>
      [...element.querySelectorAll<HTMLButtonElement>('.period-nav button')].find((b) => (b.getAttribute('aria-label') ?? b.textContent!.trim()) === label)!;
    const next = async (http: HttpTestingController) => {
      await new Promise((resolve) => setTimeout(resolve));
      TestBed.inject(ApplicationRef).tick();
      return http.expectOne((r) => r.url === '/api/runs');
    };

    it('lists its Runs Monday to Sunday', async () => {
      const { element, request } = await open('/runs?week=2026-10-05');

      expect(period(request)).toEqual(['2026-10-05', '2026-10-11']);
      expect(element.querySelector('.period-nav h2')!.textContent!.trim()).toBe('5 Oct – 11 Oct 2026');
      expect(element.querySelector('.period-nav')!.getAttribute('aria-label')).toBe('Training Week');
    });

    it('is the week of any day it is given', async () => {
      const { request } = await open('/runs?week=2026-10-01');

      expect(period(request)).toEqual(['2026-09-28', '2026-10-04']);
    });

    it('falls back to the month for a malformed week', async () => {
      const { request } = await open('/runs?week=2026-02-31');

      expect(period(request)).toEqual(['2026-10-01', '2026-10-31']);
    });

    it('steps a week at a time, keeping the search, up to the current week', async () => {
      const { http, element } = await open('/runs?week=2026-10-05&search=Lauf');

      navButton(element, 'Next week').click();
      const request = await next(http);
      expect([...period(request.request), request.request.params.get('search')]).toEqual(['2026-10-12', '2026-10-18', 'Lauf']);
      request.flush(october);
      await new Promise((resolve) => setTimeout(resolve));
      TestBed.inject(ApplicationRef).tick();
      expect(navButton(element, 'Next week').disabled).toBe(true);
      expect(navButton(element, 'Today').disabled).toBe(true);

      navButton(element, 'Previous week').click();
      expect(period((await next(http)).request)).toEqual(['2026-10-05', '2026-10-11']);
    });

    it('opens the whole month it starts in', async () => {
      const { http, element } = await open('/runs?week=2026-09-28');

      navButton(element, 'All of September').click();
      expect(period((await next(http)).request)).toEqual(['2026-09-01', '2026-09-30']);
      expect(TestBed.inject(Router).url).toBe('/runs?month=2026-09');
    });

    it('exports as the week, named after its Monday', async () => {
      const saves = captureSaves();
      const { http, element } = await open('/runs?week=2026-10-05');

      const button = [...element.querySelectorAll<HTMLButtonElement>('.export-menu button')].find((b) => b.textContent!.trim() === 'This week')!;
      button.click();
      const request = http.expectOne((r) => r.url === '/api/runs/export');
      request.flush([]);

      expect(period(request.request)).toEqual(['2026-10-05', '2026-10-11']);
      expect(saves.map((s) => s.name)).toEqual(['runs-week-2026-10-05.csv']);
      vi.restoreAllMocks();
    });
  });

  describe('a day with two Runs', () => {
    const double: RunPage = {
      ...october,
      items: [run(3, 'Evening shakeout', '2026-10-11T18:30:00', 5000, 1650), ...october.items],
      totalCount: 3,
    };
    const rows = (element: HTMLElement) => [...element.querySelectorAll<HTMLElement>('.runs tbody tr')];

    it('groups them, naming the day once with how many Runs it has', async () => {
      const { element } = await open('/runs', double);

      const [evening, morning, tuesday] = rows(element);
      expect(evening.classList).toContain('same-day');
      expect(evening.querySelector('.day-count')!.textContent!.trim()).toBe('2 Runs');
      expect(morning.classList).toContain('continues-day');
      expect(morning.querySelector('.day-count')).toBeNull();
      expect(tuesday.classList).not.toContain('same-day');
      expect(tuesday.querySelector('.day-count')).toBeNull();
    });

    it('still tags each of them when sorted by something else', async () => {
      const { element } = await open('/runs?sort=distance', double);

      expect(rows(element).map((r) => r.querySelector('.day-count')?.textContent!.trim() ?? null)).toEqual(['2 Runs', '2 Runs', null]);
      expect(element.querySelector('.same-day, .continues-day')).toBeNull();
    });
  });

  /** What the page handed the browser to save: the file's name and its bytes. */
  function captureSaves(): { name: string; blob: Blob }[] {
    const saves: { name: string; blob: Blob }[] = [];
    URL.createObjectURL = vi.fn((blob: Blob) => {
      saves.push({ name: '', blob });
      return 'blob:csv';
    });
    URL.revokeObjectURL = vi.fn();
    vi.spyOn(HTMLAnchorElement.prototype, 'click').mockImplementation(function (this: HTMLAnchorElement) {
      saves.at(-1)!.name = this.download;
    });
    return saves;
  }

  describe('Export CSV', () => {
    afterEach(() => vi.restoreAllMocks());

    const exported: RunExport = {
      id: 1, name: 'Lauf im Prater', sportType: 'Run', startLocal: '2026-09-28T09:00:00', distance: 10020, movingTime: 3300, pace: 329.34,
      elevationGain: 8, averageHeartRate: 148, cadence: 168, relativeEffort: 35, shoeName: null, stravaUrl: 'https://www.strava.com/activities/1',
    };
    const menuButton = (element: HTMLElement, label: string) =>
      [...element.querySelectorAll<HTMLButtonElement>('.export-menu button')].find((b) => b.textContent!.trim() === label)!;

    it('saves the month shown, with its search and sort order, named after the month', async () => {
      const saves = captureSaves();
      const { http, element } = await open('/runs?month=2026-09&search=lauf&sort=pace&direction=asc');

      menuButton(element, 'This month').click();
      const request = http.expectOne((r) => r.url === '/api/runs/export');
      request.flush([exported]);

      expect(request.request.params.keys().sort()).toEqual(['direction', 'from', 'search', 'sort', 'to']);
      expect([request.request.params.get('from'), request.request.params.get('to'), request.request.params.get('search')])
        .toEqual(['2026-09-01', '2026-09-30', 'lauf']);
      expect(saves.map((s) => s.name)).toEqual(['runs-2026-09.csv']);
      const bytes = new Uint8Array(await saves[0].blob.arrayBuffer());
      expect([...bytes.slice(0, 3)]).toEqual([0xef, 0xbb, 0xbf]);
      const lines = new TextDecoder().decode(bytes).split('\r\n');
      expect(lines[0]).toBe('Date;Name;Sport Type;Distance (km);Moving Time;Pace (min/km);Elevation Gain (m);Avg Heart Rate (bpm);Cadence (spm);Relative Effort;Shoe;Strava');
      expect(lines[1]).toBe('2026-09-28 09:00;Lauf im Prater;Run;10,02;0:55:00;5:29;8;148;168;35;;https://www.strava.com/activities/1');
    });

    it('saves every Run, whatever the month or search, named after today', async () => {
      const saves = captureSaves();
      const { http, element } = await open('/runs?month=2026-09&search=lauf');

      menuButton(element, 'All Runs').click();
      const request = http.expectOne((r) => r.url === '/api/runs/export');
      request.flush([exported]);

      expect(request.request.params.keys()).toEqual([]);
      expect(saves.map((s) => s.name)).toEqual(['runs-2026-10-12.csv']);
    });

    it('says when the export could not be made', async () => {
      const saves = captureSaves();
      const { http, element } = await open('/runs');

      menuButton(element, 'All Runs').click();
      http.expectOne((r) => r.url === '/api/runs/export').flush(null, { status: 500, statusText: 'Server Error' });
      await new Promise((resolve) => setTimeout(resolve));
      TestBed.inject(ApplicationRef).tick();

      expect(saves).toEqual([]);
      expect(element.querySelector('[role="alert"]')!.textContent).toContain('The export could not be made.');
    });
  });
});

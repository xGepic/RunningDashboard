import { HttpRequest, provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { Router, provideRouter, withComponentInputBinding } from '@angular/router';
import { RouterTestingHarness } from '@angular/router/testing';
import { RidePage } from './ride';
import { RidesPage } from './rides-page';

const rides: RidePage = {
  items: [
    {
      id: 1, name: 'Morning Ride', sportType: 'Ride', startLocal: '2026-10-11T08:00:00', distance: 42000, movingTime: 5400,
      speed: 28, elevationGain: 310, stravaUrl: 'https://www.strava.com/activities/1',
    },
  ],
  page: 1,
  pageSize: 10,
  totalCount: 1,
  totals: { rides: 1, distance: 42000, movingTime: 5400, speed: 28 },
  years: [2026, 2024],
};

const noRides: RidePage = { ...rides, items: [], totalCount: 0, totals: { rides: 0, distance: 0, movingTime: 0, speed: null } };

describe('RidesPage', () => {
  beforeEach(() => {
    vi.useFakeTimers({ toFake: ['Date'] });
    vi.setSystemTime(new Date(2026, 9, 12, 9));
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        provideRouter([{ path: 'rides', component: RidesPage }], withComponentInputBinding()),
      ],
    });
  });

  afterEach(() => {
    vi.useRealTimers();
    vi.restoreAllMocks();
  });

  /** The request for the Rides that `open` answered. */
  let listed: HttpRequest<unknown>;

  /** `page` 'error' answers the Rides with a server error. */
  async function open(page: RidePage | 'error', url = '/rides'): Promise<HTMLElement> {
    const harness = await RouterTestingHarness.create();
    const http = TestBed.inject(HttpTestingController);
    await harness.navigateByUrl(url);
    for (const sync of http.match('/api/sync')) sync.flush({ running: false, importedActivities: 0, lastSyncedAt: null });
    for (const settings of http.match('/api/settings')) settings.flush({ distanceUnit: 'km' });
    harness.detectChanges();
    const request = http.expectOne((r) => r.url === '/api/rides');
    listed = request.request;
    if (page === 'error') request.flush(null, { status: 500, statusText: 'Server Error' });
    else request.flush(page);
    await new Promise((resolve) => setTimeout(resolve));
    harness.detectChanges();
    return harness.routeNativeElement!;
  }

  const yearNavButton = (element: HTMLElement, label: string) =>
    [...element.querySelectorAll<HTMLButtonElement>('.period-nav button')].find((b) => (b.getAttribute('aria-label') ?? b.textContent!.trim()).startsWith(label))!;

  it("lists this year's Rides, with no year after it", async () => {
    const element = await open(rides);

    expect([listed.params.get('from'), listed.params.get('to')]).toEqual(['2026-01-01', '2026-12-31']);
    expect(element.querySelector('.period-nav h2')!.textContent).toBe('2026');
    expect(yearNavButton(element, 'Next year').disabled).toBe(true);
    expect(yearNavButton(element, 'Today').disabled).toBe(true);
  });

  it('steps a year back from the first page, keeping the kind', async () => {
    const element = await open(rides, '/rides?kind=outdoor&page=2');
    const http = TestBed.inject(HttpTestingController);

    yearNavButton(element, 'Previous year').click();
    await new Promise((resolve) => setTimeout(resolve));
    const request = http.expectOne((r) => r.url === '/api/rides');

    expect([request.request.params.get('from'), request.request.params.get('kind'), request.request.params.get('page')]).toEqual(['2025-01-01', 'outdoor', null]);
    expect(TestBed.inject(Router).url).toBe('/rides?kind=outdoor&year=2025');
  });

  it('goes back to this year without a year in the link', async () => {
    const element = await open(rides, '/rides?year=2025');

    yearNavButton(element, 'Today').click();
    await new Promise((resolve) => setTimeout(resolve));

    expect(TestBed.inject(Router).url).toBe('/rides');
  });

  it('steps no further back than the first year with Rides', async () => {
    const element = await open(rides, '/rides?year=2024');

    expect(yearNavButton(element, 'Previous year').disabled).toBe(true);
    expect(yearNavButton(element, 'Next year').disabled).toBe(false);
  });

  it('lists this year when the year in the link is not one', async () => {
    await open(rides, '/rides?year=last');

    expect(listed.params.get('from')).toBe('2026-01-01');
  });

  it('shows the totals of the year', async () => {
    const element = await open(rides);

    expect(element.querySelector('.summary')!.textContent).toContain('42.0 km');
    expect(element.querySelector('.summary')!.getAttribute('aria-label')).toBe('Totals of the Rides in 2026');
  });

  it('says when a year has no Rides of the kind, with a way to the newest year with Rides', async () => {
    const element = await open(noRides, '/rides?kind=virtual&year=2025');

    expect(element.querySelector('.empty')!.textContent).toContain('No virtual Rides in 2025.');
    expect(element.querySelector('.empty button')!.textContent!.trim()).toBe('Go to 2026');
  });

  it('lists only the virtual Rides once Virtual is picked, from the first page', async () => {
    const element = await open(rides);
    const http = TestBed.inject(HttpTestingController);

    [...element.querySelectorAll<HTMLButtonElement>('.segmented button')].find((b) => b.textContent!.trim() === 'Virtual')!.click();
    await new Promise((resolve) => setTimeout(resolve));
    const request = http.expectOne((r) => r.url === '/api/rides');

    expect(request.request.params.get('kind')).toBe('virtual');
    expect(request.request.params.get('page')).toBeNull();
  });

  it('says so when the Rides cannot be loaded, without totals', async () => {
    const element = await open('error');

    expect(element.querySelector('[role="alert"]')!.textContent).toContain('Rides could not be loaded');
    expect(element.querySelector('.summary')).toBeNull();
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

  const menuButton = (element: HTMLElement, label: string) =>
    [...element.querySelectorAll<HTMLButtonElement>('.export-menu button')].find((b) => b.textContent!.trim() === label)!;

  it('exports the year shown, of the kind, named after the year', async () => {
    const saves = captureSaves();
    const element = await open(rides, '/rides?kind=outdoor&year=2025');
    const http = TestBed.inject(HttpTestingController);

    menuButton(element, 'This year').click();
    const request = http.expectOne((r) => r.url === '/api/rides/export');
    request.flush(rides.items);

    expect([request.request.params.get('kind'), request.request.params.get('from'), request.request.params.get('to')]).toEqual(['outdoor', '2025-01-01', '2025-12-31']);
    expect(saves.map((s) => s.name)).toEqual(['rides-2025.csv']);
    const text = await saves[0].blob.text();
    expect(text).toContain('2026-10-11 08:00;Morning Ride;Ride;42,00;1:30:00;28,0;310;https://www.strava.com/activities/1');
  });

  it('exports every Ride of every year in the sort order shown, named after today', async () => {
    const saves = captureSaves();
    const element = await open(rides, '/rides?year=2025');
    const http = TestBed.inject(HttpTestingController);

    menuButton(element, 'All Rides').click();
    const request = http.expectOne((r) => r.url === '/api/rides/export');
    request.flush(rides.items);

    expect(request.request.params.keys()).toEqual([]);
    expect(saves.map((s) => s.name)).toEqual(['rides-2026-10-12.csv']);
  });
});

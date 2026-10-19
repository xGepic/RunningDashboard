import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { provideRouter, withComponentInputBinding } from '@angular/router';
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
};

describe('RidesPage', () => {
  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        provideRouter([{ path: 'rides', component: RidesPage }], withComponentInputBinding()),
      ],
    });
  });

  /** `page` 'error' answers the Rides with a server error. */
  async function open(page: RidePage | 'error'): Promise<HTMLElement> {
    const harness = await RouterTestingHarness.create();
    const http = TestBed.inject(HttpTestingController);
    await harness.navigateByUrl('/rides');
    for (const sync of http.match('/api/sync')) sync.flush({ running: false, importedActivities: 0, lastSyncedAt: null });
    for (const settings of http.match('/api/settings')) settings.flush({ distanceUnit: 'km' });
    harness.detectChanges();
    const request = http.expectOne((r) => r.url === '/api/rides');
    if (page === 'error') request.flush(null, { status: 500, statusText: 'Server Error' });
    else request.flush(page);
    await new Promise((resolve) => setTimeout(resolve));
    harness.detectChanges();
    return harness.routeNativeElement!;
  }

  it('shows the totals of all Rides', async () => {
    const element = await open(rides);

    expect(element.querySelector('.summary')!.textContent).toContain('42.0 km');
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

  afterEach(() => vi.restoreAllMocks());

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

  it('exports every Ride in the sort order shown, named after today', async () => {
    const saves = captureSaves();
    const element = await open(rides);
    const http = TestBed.inject(HttpTestingController);

    [...element.querySelectorAll<HTMLButtonElement>('button')].find((b) => b.textContent!.trim() === 'Export CSV')!.click();
    const request = http.expectOne((r) => r.url === '/api/rides/export');
    request.flush(rides.items);

    expect(request.request.params.keys()).toEqual([]);
    expect(saves.map((s) => s.name)).toEqual([expect.stringMatching(/^rides-\d{4}-\d{2}-\d{2}\.csv$/)]);
    const text = await saves[0].blob.text();
    expect(text).toContain('2026-10-11 08:00;Morning Ride;Ride;42,00;1:30:00;28,0;310;https://www.strava.com/activities/1');
  });
});

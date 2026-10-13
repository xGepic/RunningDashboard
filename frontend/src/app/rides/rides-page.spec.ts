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

  it('says so when the Rides cannot be loaded, without totals', async () => {
    const element = await open('error');

    expect(element.querySelector('[role="alert"]')!.textContent).toContain('Rides could not be loaded');
    expect(element.querySelector('.summary')).toBeNull();
  });
});

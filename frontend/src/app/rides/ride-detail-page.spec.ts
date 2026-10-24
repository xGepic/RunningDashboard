import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { provideRouter, withComponentInputBinding } from '@angular/router';
import { RouterTestingHarness } from '@angular/router/testing';
import { RideDetail } from './ride';
import { RideDetailPage } from './ride-detail-page';

const zwiftRide: RideDetail = {
  id: 1, name: 'Zwift Watopia', sportType: 'VirtualRide', startLocal: '2026-10-11T19:00:00', distance: 30000, movingTime: 3600,
  elapsedTime: 3700, speed: 30, maxSpeed: 52.3, elevationGain: 310, averageHeartRate: null, maxHeartRate: null, averagePower: 182.5,
  averageCadence: null, route: null, stravaUrl: 'https://www.strava.com/activities/1',
};

describe('RideDetailPage', () => {
  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        provideRouter([{ path: 'rides/:id', component: RideDetailPage }], withComponentInputBinding()),
      ],
    });
  });

  /** `ride` 'error' answers with a server error. */
  async function open(ride: RideDetail | 'error'): Promise<HTMLElement> {
    const harness = await RouterTestingHarness.create();
    const http = TestBed.inject(HttpTestingController);
    await harness.navigateByUrl('/rides/1');
    for (const settings of http.match('/api/settings')) settings.flush({ distanceUnit: 'km' });
    harness.detectChanges();
    const request = http.expectOne('/api/rides/1');
    if (ride === 'error') request.flush(null, { status: 500, statusText: 'Server Error' });
    else request.flush(ride);
    await new Promise((resolve) => setTimeout(resolve));
    harness.detectChanges();
    return harness.routeNativeElement!;
  }

  it('shows the Ride with its kind, Avg Speed and power, without what it was recorded without', async () => {
    const element = await open(zwiftRide);

    expect(element.querySelector('h1')!.textContent).toContain('Zwift Watopia');
    expect(element.querySelector('.eyebrow')!.textContent).toContain('Virtual');
    const facts = element.querySelector('.summary')!.textContent!;
    expect(facts).toContain('30.0 km/h');
    expect(facts).toContain('183 W');
    expect(facts).not.toContain('Cadence');
    expect(element.querySelector('app-run-map')).toBeNull();
  });

  it('says so when the Ride cannot be loaded', async () => {
    const element = await open('error');

    expect(element.querySelector('[role="alert"]')!.textContent).toContain('This Ride could not be loaded');
  });
});

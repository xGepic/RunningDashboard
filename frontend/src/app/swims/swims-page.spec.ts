import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { provideRouter, withComponentInputBinding } from '@angular/router';
import { RouterTestingHarness } from '@angular/router/testing';
import { SwimPage } from './swim';
import { SwimsPage } from './swims-page';

const swims: SwimPage = {
  items: [
    {
      id: 1, name: 'Lunch Swim', startLocal: '2026-10-11T12:00:00', distance: 2000, movingTime: 2400, pace: 120,
      stravaUrl: 'https://www.strava.com/activities/1',
    },
  ],
  page: 1,
  pageSize: 10,
  totalCount: 1,
  totals: { swims: 1, distance: 2000, movingTime: 2400, pace: 120 },
};

describe('SwimsPage', () => {
  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        provideRouter([{ path: 'swims', component: SwimsPage }], withComponentInputBinding()),
      ],
    });
  });

  /** `page` 'error' answers the Swims with a server error. */
  async function open(page: SwimPage | 'error'): Promise<HTMLElement> {
    const harness = await RouterTestingHarness.create();
    const http = TestBed.inject(HttpTestingController);
    await harness.navigateByUrl('/swims');
    for (const sync of http.match('/api/sync')) sync.flush({ running: false, importedActivities: 0, lastSyncedAt: null });
    harness.detectChanges();
    const request = http.expectOne((r) => r.url === '/api/swims');
    if (page === 'error') request.flush(null, { status: 500, statusText: 'Server Error' });
    else request.flush(page);
    await new Promise((resolve) => setTimeout(resolve));
    harness.detectChanges();
    return harness.routeNativeElement!;
  }

  it('shows the totals of all Swims', async () => {
    const element = await open(swims);

    expect(element.querySelector('.summary')!.textContent).toContain('2:00 /100m');
  });

  it("links each Swim to its own page", async () => {
    const element = await open(swims);

    expect(element.querySelector('tbody a')!.getAttribute('href')).toBe('/swims/1');
  });

  it('says so when the Swims cannot be loaded, without totals', async () => {
    const element = await open('error');

    expect(element.querySelector('[role="alert"]')!.textContent).toContain('Swims could not be loaded');
    expect(element.querySelector('.summary')).toBeNull();
  });
});

import { HttpRequest, provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { Router, provideRouter, withComponentInputBinding } from '@angular/router';
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
  years: [2026, 2025],
};

describe('SwimsPage', () => {
  beforeEach(() => {
    vi.useFakeTimers({ toFake: ['Date'] });
    vi.setSystemTime(new Date(2026, 9, 12, 9));
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        provideRouter([{ path: 'swims', component: SwimsPage }], withComponentInputBinding()),
      ],
    });
  });

  afterEach(() => vi.useRealTimers());

  /** The request for the Swims that `open` answered. */
  let listed: HttpRequest<unknown>;

  /** `page` 'error' answers the Swims with a server error. */
  async function open(page: SwimPage | 'error', url = '/swims'): Promise<HTMLElement> {
    const harness = await RouterTestingHarness.create();
    const http = TestBed.inject(HttpTestingController);
    await harness.navigateByUrl(url);
    for (const sync of http.match('/api/sync')) sync.flush({ running: false, importedActivities: 0, lastSyncedAt: null });
    harness.detectChanges();
    const request = http.expectOne((r) => r.url === '/api/swims');
    listed = request.request;
    if (page === 'error') request.flush(null, { status: 500, statusText: 'Server Error' });
    else request.flush(page);
    await new Promise((resolve) => setTimeout(resolve));
    harness.detectChanges();
    return harness.routeNativeElement!;
  }

  it("lists this year's Swims, with their totals", async () => {
    const element = await open(swims);

    expect([listed.params.get('from'), listed.params.get('to')]).toEqual(['2026-01-01', '2026-12-31']);
    expect(element.querySelector('.period-nav h2')!.textContent).toBe('2026');
    expect(element.querySelector('.summary')!.textContent).toContain('2:00 /100m');
  });

  it('steps a year back', async () => {
    const element = await open(swims);
    const http = TestBed.inject(HttpTestingController);

    element.querySelector<HTMLButtonElement>('.period-nav button[aria-label^="Previous year"]')!.click();
    await new Promise((resolve) => setTimeout(resolve));
    const request = http.expectOne((r) => r.url === '/api/swims');

    expect([request.request.params.get('from'), request.request.params.get('to')]).toEqual(['2025-01-01', '2025-12-31']);
    expect(TestBed.inject(Router).url).toBe('/swims?year=2025');
  });

  it('says when a year has no Swims, with a way to the newest year with Swims', async () => {
    const element = await open({ ...swims, items: [], totalCount: 0, years: [2025] });

    expect(element.querySelector('.empty')!.textContent).toContain('No Swims in 2026.');
    expect(element.querySelector('.empty button')!.textContent!.trim()).toBe('Go to 2025');
  });

  it('links each Swim to its own page', async () => {
    const element = await open(swims);

    expect(element.querySelector('tbody a')!.getAttribute('href')).toBe('/swims/1');
  });

  it('says so when the Swims cannot be loaded, without totals', async () => {
    const element = await open('error');

    expect(element.querySelector('[role="alert"]')!.textContent).toContain('Swims could not be loaded');
    expect(element.querySelector('.summary')).toBeNull();
  });
});

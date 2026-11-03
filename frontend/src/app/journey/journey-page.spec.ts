import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { Component, input } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { Router, provideRouter, withComponentInputBinding } from '@angular/router';
import { RouterTestingHarness } from '@angular/router/testing';
import { Journey, JourneyPageData } from './journey';
import { JourneyMap } from './journey-map';
import { JourneyPage } from './journey-page';

/** jsdom cannot draw Leaflet's map; the page only hands it the journey. */
@Component({ selector: 'app-journey-map', template: '' })
class StubMap {
  readonly journey = input.required<Journey>();
}

/** Vienna, Bratislava at 50 km (reached), Budapest at 200 km, back to Vienna at 400 km: picked for a 410 km Yearly Goal in 2026. */
function journeyOf(year: number, distance: number): JourneyPageData {
  return {
    journey: {
      year,
      distance,
      length: 400_000,
      yearlyGoal: year === 2026 ? 410_000 : null,
      stops: [
        { name: 'Vienna', country: 'Austria', latitude: 48, longitude: 16, at: 0, reached: null },
        { name: 'Bratislava', country: 'Slovakia', latitude: 48, longitude: 17, at: 50_000, reached: { date: `${year}-03-01`, runId: 7, runName: 'Long one' } },
        { name: 'Budapest', country: 'Hungary', latitude: 47, longitude: 19, at: 200_000, reached: null },
        { name: 'Vienna', country: 'Austria', latitude: 48, longitude: 16, at: 400_000, reached: null },
      ],
    },
    years: [2026, 2025],
  };
}

describe('JourneyPage', () => {
  beforeEach(() => {
    vi.useFakeTimers({ toFake: ['Date'] });
    vi.setSystemTime(new Date(2026, 9, 9, 18));
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        provideRouter([{ path: 'journey', component: JourneyPage }], withComponentInputBinding()),
      ],
    });
    TestBed.overrideComponent(JourneyPage, { remove: { imports: [JourneyMap] }, add: { imports: [StubMap] } });
  });

  afterEach(() => vi.useRealTimers());

  async function open(url: string, year: number, distance = 120_000) {
    const harness = await RouterTestingHarness.create();
    const http = TestBed.inject(HttpTestingController);
    await harness.navigateByUrl(url);
    for (const request of http.match('/api/sync')) request.flush({ running: false, importedActivities: 0, lastSyncedAt: null });
    for (const request of http.match('/api/settings')) request.flush({ distanceUnit: 'km' });
    harness.detectChanges();
    http.expectOne((r) => r.url === '/api/journey' && r.params.get('year') === String(year)).flush(journeyOf(year, distance));
    await harness.fixture.whenStable();
    harness.detectChanges();
    return { harness, http, element: harness.routeNativeElement! };
  }

  it("lists each Stop with when it was reached and by which Run, or how far it still is", async () => {
    const { element } = await open('/journey', 2026);

    const column = (n: number) => [...element.querySelectorAll(`tbody td:nth-child(${n})`)].map((td) => td.textContent!.replace(/\s+/g, ' ').trim());
    expect(column(1)).toEqual(['Bratislava · Slovakia', 'Budapest · Hungary', 'Vienna · Austria']);
    expect(column(2)).toEqual(['50 km', '200 km', '400 km']);
    // The date and the Run sit apart by their margin.
    expect(column(3)).toEqual(['1 MarLong one', '80 km to go', '280 km to go']);
    expect(element.querySelector('tbody a')!.getAttribute('href')).toBe('/runs/7');
    expect(element.querySelector('app-journey-card .headline .value')!.textContent).toBe('80');
    // The card does not link to the page it is on.
    expect(element.querySelector('app-journey-card .map-link')).toBeNull();
    expect(element.querySelector('app-journey-map')).not.toBeNull();
    expect(element.querySelector('.subtitle')!.textContent).toContain(
      'Vienna and back via 2 cities, 400 km, picked for your 410 km Yearly Goal: reach the goal and you are back in Vienna.');
  });

  it('says how a Yearly Goal would fit the tour in a year without one', async () => {
    const { element } = await open('/journey?year=2025', 2025);

    expect(element.querySelector('.subtitle')!.textContent).toContain(
      'Vienna and back via 2 cities, 400 km. With a Yearly Goal for the year, you get the longest tour that fits within it.');
  });

  it('switches to an earlier year, and back to the current one without a year in the address', async () => {
    const { harness, http, element } = await open('/journey?year=2025', 2025);

    const buttons = () => [...element.querySelectorAll<HTMLButtonElement>('.segmented button')];
    expect(buttons().map((b) => [b.textContent!.trim(), b.getAttribute('aria-pressed')])).toEqual([['2026', 'false'], ['2025', 'true']]);

    buttons()[0].click();
    await new Promise((resolve) => setTimeout(resolve));
    expect(TestBed.inject(Router).url).toBe('/journey');
    harness.detectChanges();
    http.expectOne((r) => r.url === '/api/journey' && r.params.get('year') === '2026');
  });
});

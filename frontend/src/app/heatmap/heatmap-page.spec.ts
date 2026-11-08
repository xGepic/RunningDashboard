import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { Component, input } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { Router, provideRouter, withComponentInputBinding } from '@angular/router';
import { RouterTestingHarness } from '@angular/router/testing';
import { DrawnRun, HeatmapData } from './heatmap';
import { HeatmapMap } from './heatmap-map';
import { HeatmapPage } from './heatmap-page';

/** jsdom cannot draw Leaflet's map; the page only hands it the Runs and asks it to fit. */
@Component({ selector: 'app-heatmap-map', template: '' })
class StubMap {
  readonly runs = input.required<DrawnRun[]>();
  readonly fitted: string[] = [];
  fit(area: 'home' | 'all'): void {
    this.fitted.push(area);
  }
}

/** Google's polyline example: two points. */
const route = '_p~iF~ps|U_ulLnnqC';

const heatmap: HeatmapData = {
  runs: [
    { id: 1, name: 'Old loop', startLocal: '2025-06-01T08:00:00', distance: 15_000, route },
    { id: 2, name: 'Prater', startLocal: '2026-03-01T09:00:00', distance: 10_000, route },
    { id: 3, name: 'Danube', startLocal: '2026-04-01T09:00:00', distance: 21_100, route },
  ],
};

describe('HeatmapPage', () => {
  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        provideRouter([{ path: 'heatmap', component: HeatmapPage }], withComponentInputBinding()),
      ],
    });
    TestBed.overrideComponent(HeatmapPage, { remove: { imports: [HeatmapMap] }, add: { imports: [StubMap] } });
  });

  async function open(url: string, data: HeatmapData = heatmap) {
    const harness = await RouterTestingHarness.create();
    const http = TestBed.inject(HttpTestingController);
    await harness.navigateByUrl(url);
    for (const request of http.match('/api/sync')) request.flush({ running: false, importedActivities: 0, lastSyncedAt: null });
    for (const request of http.match('/api/settings')) request.flush({ distanceUnit: 'km' });
    harness.detectChanges();
    http.expectOne('/api/heatmap').flush(data);
    await harness.fixture.whenStable();
    harness.detectChanges();
    return { harness, http, element: harness.routeNativeElement! };
  }

  const text = (element: Element, selector: string) => element.querySelector(selector)!.textContent!.replace(/\s+/g, ' ').trim();
  const mapOf = (harness: RouterTestingHarness) => harness.fixture.debugElement.query((e) => e.componentInstance instanceof StubMap).componentInstance as StubMap;

  it('draws every Run of all years, with how many and how far', async () => {
    const { harness, element } = await open('/heatmap');

    expect(mapOf(harness).runs().map((r) => r.id)).toEqual([1, 2, 3]);
    expect(text(element, '.card-title')).toBe('All years');
    expect(text(element, '.card-sub')).toBe('3 Runs · 46 km');
    const years = [...element.querySelectorAll('.segmented button')].map((b) => [b.textContent!.trim(), b.getAttribute('aria-pressed')]);
    expect(years).toEqual([['All', 'true'], ['2026', 'false'], ['2025', 'false']]);
  });

  it("draws one year's Runs, and goes back to all years without a year in the address", async () => {
    const { harness, element } = await open('/heatmap?year=2026');

    expect(mapOf(harness).runs().map((r) => r.id)).toEqual([2, 3]);
    expect(text(element, '.card-title')).toBe('2026');

    element.querySelector<HTMLButtonElement>('.segmented button')!.click();
    await harness.fixture.whenStable();

    expect(TestBed.inject(Router).url).toBe('/heatmap');
  });

  it('fits the map to home or to every Run shown', async () => {
    const { harness, element } = await open('/heatmap');

    const [home, all] = element.querySelectorAll<HTMLButtonElement>('.views button');
    home.click();
    all.click();

    expect(mapOf(harness).fitted).toEqual(['home', 'all']);
  });

  it('says when there are no Runs with a route, and has no year switch for one year', async () => {
    const { element } = await open('/heatmap', { runs: [] });

    expect(text(element, '.empty')).toBe('No Runs with a route yet. Treadmill Runs and Runs without GPS have none.');
    expect(element.querySelector('app-heatmap-map')).toBeNull();
    expect(element.querySelector('.segmented')).toBeNull();
  });

  it('says when the year has no Runs with a route', async () => {
    const { element } = await open('/heatmap?year=2020');

    expect(text(element, '.empty')).toBe('No Runs with a route in 2020.');
  });

  it('says when the Heatmap could not be loaded', async () => {
    const harness = await RouterTestingHarness.create();
    const http = TestBed.inject(HttpTestingController);
    await harness.navigateByUrl('/heatmap');
    for (const request of http.match('/api/sync')) request.flush({ running: false, importedActivities: 0, lastSyncedAt: null });
    harness.detectChanges();
    http.expectOne('/api/heatmap').flush('Boom', { status: 500, statusText: 'Server Error' });
    await harness.fixture.whenStable();
    harness.detectChanges();

    expect(text(harness.routeNativeElement!, '[role="alert"]')).toBe('The Heatmap could not be loaded.');
  });
});

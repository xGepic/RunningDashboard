import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { Component } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { Router, provideRouter } from '@angular/router';
import { App } from './app';

@Component({ template: '' })
class Blank {}

describe('App', () => {
  beforeEach(() => {
    TestBed.configureTestingModule({
      imports: [App],
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        provideRouter(['', 'training', 'runs', 'calendar', 'calendar/:year', 'records', 'fitness', 'shoes', 'calculator', 'settings', 'journey', 'heatmap'].map((path) => ({ path, component: Blank }))),
      ],
    });
  });

  async function render(url = '/') {
    const fixture = TestBed.createComponent(App);
    await TestBed.inject(Router).navigateByUrl(url);
    fixture.detectChanges();
    return { fixture, element: fixture.nativeElement as HTMLElement };
  }

  it('lists the Calendar after the Activities, opened with key 4', async () => {
    const { element } = await render();

    const links = [...element.querySelectorAll('.sidebar nav[aria-label="Pages"] a')].map((a) => [a.querySelector('.label')!.textContent, a.querySelector('kbd')!.textContent]);
    expect(links).toEqual([['Overview', '1'], ['Training', '2'], ['Activities', '3'], ['Calendar', '4'], ['Records', '5'], ['Fitness', '6'], ['Shoes', '7'], ['Calculator', '8']]);

    document.body.dispatchEvent(new KeyboardEvent('keydown', { key: '4', bubbles: true }));
    await new Promise((resolve) => setTimeout(resolve));
    expect(TestBed.inject(Router).url).toBe('/calendar');
  });

  it('links to Settings at the bottom of the sidebar, without a key, and names it in the breadcrumb', async () => {
    const { element } = await render('/settings');

    const link = element.querySelector<HTMLAnchorElement>('.sidebar .settings-link a')!;
    expect(link.getAttribute('href')).toBe('/settings');
    expect(link.getAttribute('aria-current')).toBe('page');
    expect(link.querySelector('kbd')).toBeNull();
    expect([...element.querySelectorAll('.tabs a')].map((a) => a.textContent!.trim())).toContain('Settings');
    expect([...element.querySelectorAll('.crumbs .current')].map((c) => c.textContent!.trim())).toEqual(['Settings']);
  });

  it('names the month shown in the breadcrumb', async () => {
    const crumbs = async (url: string) => {
      const { element } = await render(url);
      return [...element.querySelectorAll('.crumbs a, .crumbs .current')].map((c) => c.textContent!.trim());
    };

    expect(await crumbs('/calendar/2025')).toEqual(['Calendar']);
    expect(await crumbs('/calendar/2025?ref=mail#Dec')).toEqual(['Calendar', 'December 2025']);
  });

  it('puts the Virtual Journey below the Overview, which stays highlighted', async () => {
    const { element } = await render('/journey?year=2025');

    expect([...element.querySelectorAll('.crumbs a, .crumbs .current')].map((c) => c.textContent!.trim())).toEqual(['Overview', 'Virtual Journey']);
    expect(element.querySelector('.sidebar nav[aria-label="Pages"] a[aria-current="page"] .label')!.textContent).toBe('Overview');
  });

  it('puts the Heatmap below the Activities, leading back to the Runs without its year', async () => {
    const { element } = await render('/heatmap?year=2025');

    expect([...element.querySelectorAll('.crumbs a, .crumbs .current')].map((c) => c.textContent!.trim())).toEqual(['Activities', 'Heatmap']);
    expect(element.querySelector('.sidebar nav[aria-label="Pages"] a[aria-current="page"] .label')!.textContent).toBe('Activities');
    expect(element.querySelector('.crumbs a')!.getAttribute('href')).toBe('/runs');
  });

  it('leads from a month back to its year, keeping the query string', async () => {
    const { element } = await render('/calendar/2025?ref=mail#Dec');

    expect(element.querySelector('.crumbs a')!.getAttribute('href')).toBe('/calendar/2025?ref=mail');
  });

  describe('the badge on the Shoes link', () => {
    const shoe = (name: string, distance: number, retired = false) => ({ id: name, name, distance, retired, runs: 10, lastRun: null });

    /** The Maximum Distance stays at its default of 700 km unless given. */
    async function badges(shoes: unknown[], shoeMaximumDistance?: number) {
      const { fixture, element } = await render();
      const http = TestBed.inject(HttpTestingController);
      http.expectOne('/api/settings').flush(shoeMaximumDistance ? { shoeMaximumDistance } : {});
      http.expectOne('/api/shoes').flush(shoes);
      await new Promise((resolve) => setTimeout(resolve));
      fixture.detectChanges();
      return {
        sidebar: element.querySelector('.sidebar a[href="/shoes"] .badge'),
        tab: element.querySelector('.tabs a[href="/shoes"] .badge'),
      };
    }

    it('counts the active Shoes from 90% of the Maximum Distance on, amber while none is over it', async () => {
      const { sidebar, tab } = await badges([shoe('Novablast', 630_000), shoe('Pegasus', 629_000), shoe('Old', 900_000, true)]);

      expect(sidebar!.textContent).toBe('1');
      expect(sidebar!.classList).toContain('amber');
      expect(sidebar!.getAttribute('title')).toBe('1 Shoe near or over the Maximum Distance');
      expect(tab!.textContent).toBe('1');
    });

    it('is red once a Shoe is over the Maximum Distance', async () => {
      const { sidebar } = await badges([shoe('Novablast', 701_000), shoe('Pegasus', 650_000)]);

      expect(sidebar!.textContent).toBe('2');
      expect(sidebar!.classList).toContain('red');
      expect(sidebar!.getAttribute('title')).toBe('2 Shoes near or over the Maximum Distance');
    });

    it('measures against the Maximum Distance set in Settings', async () => {
      const { sidebar } = await badges([shoe('Novablast', 630_000)], 800_000);

      expect(sidebar).toBeNull();
    });

    it('is left out while no Shoe is near the Maximum Distance', async () => {
      const { sidebar, tab } = await badges([shoe('Novablast', 400_000)]);

      expect(sidebar).toBeNull();
      expect(tab).toBeNull();
    });
  });

  describe('the last activity, at the bottom of the sidebar above Settings', () => {
    const page = <T,>(items: T[]) => ({ items, page: 1, pageSize: 1, totalCount: items.length, totals: {} });
    const run = { id: 42, name: 'Evening Run', sportType: 'Run', startLocal: '2026-10-12T18:05:00', distance: 10_240, movingTime: 3_125, pace: 305, shoeName: null };
    const ride = {
      id: 77, name: 'Gravel loop', sportType: 'GravelRide', startLocal: '2026-10-13T09:30:00', distance: 42_000, movingTime: 5_400, speed: 28,
      elevationGain: 310, stravaUrl: 'https://www.strava.com/activities/77',
    };
    const swim = { id: 88, name: 'Lunch Swim', startLocal: '2026-10-11T12:00:00', distance: 1_500, movingTime: 1_930, pace: 129, stravaUrl: 'https://www.strava.com/activities/88' };

    async function show(runs: unknown[], rides: unknown[], swims: unknown[]) {
      const { fixture, element } = await render();
      const http = TestBed.inject(HttpTestingController);
      http.expectOne('/api/runs?pageSize=1').flush(page(runs));
      http.expectOne('/api/rides?pageSize=1').flush(page(rides));
      http.expectOne('/api/swims?pageSize=1').flush(page(swims));
      await new Promise((resolve) => setTimeout(resolve));
      fixture.detectChanges();
      return element.querySelector('.last-activity')!;
    }

    it('is the newest Run, linked to its details', async () => {
      const last = await show([run], [], [swim]);

      expect(last.querySelector('.group-label')!.textContent).toBe('Last Activity');
      expect(last.querySelector('a')!.getAttribute('href')).toBe('/runs/42');
      expect(last.querySelector('.name')!.textContent).toBe('Evening Run');
      expect(last.querySelector('.when')!.textContent).toBe('12 Oct 2026 · Run');
      expect(last.querySelector('.figures')!.textContent).toBe('10.24 km · 52:05');
    });

    it('is a Ride when that came after the last Run, linked to its details', async () => {
      const last = await show([run], [ride], [swim]);

      expect(last.querySelector('a')!.getAttribute('href')).toBe('/rides/77');
      expect(last.querySelector('.name')!.textContent).toBe('Gravel loop');
      expect(last.querySelector('.when')!.textContent).toBe('13 Oct 2026 · Ride');
      expect(last.querySelector('.figures')!.textContent).toBe('42.00 km · 1:30:00');
    });

    it('shows a Swim in metres, as Swim history does, linked to its details', async () => {
      const last = await show([], [], [swim]);

      expect(last.querySelector('a')!.getAttribute('href')).toBe('/swims/88');
      expect(last.querySelector('.when')!.textContent).toBe('11 Oct 2026 · Swim');
      expect(last.querySelector('.figures')!.textContent).toBe('1,500 m · 32:10');
    });

    it('is left out until there is an activity', async () => {
      expect(await show([], [], [])).toBeNull();
    });
  });
});

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
        provideRouter(['', 'training', 'runs', 'calendar', 'calendar/:year', 'records', 'shoes', 'settings'].map((path) => ({ path, component: Blank }))),
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
    expect(links).toEqual([['Overview', '1'], ['Training', '2'], ['Activities', '3'], ['Calendar', '4'], ['Records', '5'], ['Shoes', '6']]);

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

  it('leads from a month back to its year, keeping the query string', async () => {
    const { element } = await render('/calendar/2025?ref=mail#Dec');

    expect(element.querySelector('.crumbs a')!.getAttribute('href')).toBe('/calendar/2025?ref=mail');
  });

  it('shows the last Run at the bottom of the sidebar, linked to its details', async () => {
    const { fixture, element } = await render();

    TestBed.inject(HttpTestingController)
      .expectOne('/api/runs?pageSize=1')
      .flush({
        items: [{ id: 42, name: 'Evening Run', sportType: 'Run', startLocal: '2026-10-12T18:05:00', distance: 10_240, movingTime: 3_125, pace: 305, shoeName: null }],
        page: 1,
        pageSize: 1,
        totalCount: 312,
      });
    await new Promise((resolve) => setTimeout(resolve));
    fixture.detectChanges();

    const lastRun = element.querySelector('.last-run')!;
    expect(lastRun.querySelector('.group-label')!.textContent).toBe('Last Run');
    expect(lastRun.querySelector('a')!.getAttribute('href')).toBe('/runs/42');
    expect(lastRun.querySelector('.name')!.textContent).toBe('Evening Run');
    expect(lastRun.querySelector('.when')!.textContent).toBe('12 Oct 2026');
    expect(lastRun.querySelector('.figures')!.textContent).toBe('10.24 km · 52:05');
  });
});

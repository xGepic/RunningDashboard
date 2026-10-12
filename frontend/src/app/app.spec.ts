import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
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
        provideRouter(['', 'training', 'runs', 'calendar', 'calendar/:year', 'records', 'shoes'].map((path) => ({ path, component: Blank }))),
      ],
    });
  });

  async function render(url = '/') {
    const fixture = TestBed.createComponent(App);
    await TestBed.inject(Router).navigateByUrl(url);
    fixture.detectChanges();
    return { fixture, element: fixture.nativeElement as HTMLElement };
  }

  it('lists the Calendar after the Runs, opened with key 4', async () => {
    const { element } = await render();

    const links = [...element.querySelectorAll('.sidebar nav a')].map((a) => [a.querySelector('.label')!.textContent, a.querySelector('kbd')!.textContent]);
    expect(links).toEqual([['Overview', '1'], ['Training', '2'], ['Runs', '3'], ['Calendar', '4'], ['Records', '5'], ['Shoes', '6']]);

    document.body.dispatchEvent(new KeyboardEvent('keydown', { key: '4', bubbles: true }));
    await new Promise((resolve) => setTimeout(resolve));
    expect(TestBed.inject(Router).url).toBe('/calendar');
  });

  it('names the month shown in the breadcrumb', async () => {
    const crumbs = async (url: string) => {
      const { element } = await render(url);
      return [...element.querySelectorAll('.crumbs a, .crumbs .current')].map((c) => c.textContent!.trim());
    };

    expect(await crumbs('/calendar/2025')).toEqual(['Calendar']);
    expect(await crumbs('/calendar/2025?unit=mi#Dec')).toEqual(['Calendar', 'December 2025']);
  });

  it('leads from a month back to its year, keeping the filters', async () => {
    const { element } = await render('/calendar/2025?unit=mi&sportType=TrailRun#Dec');

    expect(element.querySelector('.crumbs a')!.getAttribute('href')).toBe('/calendar/2025?unit=mi&sportType=TrailRun');
  });
});

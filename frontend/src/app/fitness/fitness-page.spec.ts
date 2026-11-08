import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { Component } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { By } from '@angular/platform-browser';
import { NgxEchartsDirective, provideEchartsCore } from 'ngx-echarts';
import { Break } from '../shared/breaks';
import { Fitness } from './fitness';
import { FitnessPage } from './fitness-page';
import { FreshnessCard } from './freshness-card';

/** Fitness & Freshness loads and is tested on its own (freshness-card.spec.ts). */
@Component({ selector: 'app-freshness-card', template: '' })
class StubFreshnessCard {}

const fitness: Fitness = {
  runs: [
    { id: 1, name: 'Easy', startLocal: '2026-09-08T18:00:00', distance: 8000, cadence: 168 },
    { id: 2, name: 'Long <fast>', startLocal: '2026-09-13T08:00:00', distance: 12000, cadence: 164 },
    { id: 3, name: 'Shakeout', startLocal: '2026-10-02T07:00:00', distance: 5000, cadence: null },
  ],
  months: [
    { year: 2026, month: 9, cadence: 166 },
    { year: 2026, month: 10, cadence: null },
  ],
};

interface ChartOptions {
  series: { name: string; data: { value: [string, number]; title: string }[]; markArea?: { data: Record<string, unknown>[][] } }[];
  tooltip: { formatter: (params: { data: { value: [string, number]; title: string } }) => string };
}

describe('FitnessPage', () => {
  beforeEach(() => {
    vi.stubGlobal('ResizeObserver', class { observe() {} unobserve() {} disconnect() {} });
    TestBed.configureTestingModule({
      imports: [FitnessPage],
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        // jsdom cannot draw; the tests read the charts' options instead.
        provideEchartsCore({ echarts: { init: () => new Proxy({}, { get: () => () => undefined }) } }),
      ],
    });
    TestBed.overrideComponent(FitnessPage, { remove: { imports: [FreshnessCard] }, add: { imports: [StubFreshnessCard] } });
  });

  afterEach(() => vi.unstubAllGlobals());

  async function render(answer: Fitness | 'error' = fitness, breaks: Break[] = []) {
    const fixture = TestBed.createComponent(FitnessPage);
    const http = TestBed.inject(HttpTestingController);
    fixture.detectChanges();
    for (const settings of http.match('/api/settings')) settings.flush({ distanceUnit: 'km' });
    for (const request of http.match('/api/breaks')) request.flush(breaks);
    const request = http.expectOne('/api/fitness');
    if (answer === 'error') request.flush(null, { status: 500, statusText: 'Server Error' });
    else request.flush(answer);
    await fixture.whenStable();
    fixture.detectChanges();
    const charts = () =>
      Object.fromEntries(
        fixture.debugElement
          .queryAll(By.directive(NgxEchartsDirective))
          .map((chart) => [chart.parent!.nativeElement.classList[1], chart.injector.get(NgxEchartsDirective).options()]),
      ) as Record<string, ChartOptions>;
    return { element: fixture.nativeElement as HTMLElement, charts };
  }

  it('charts only Cadence, as a dot per Run that has one and a line through the monthly averages that have one', async () => {
    const { charts } = await render();

    expect(Object.keys(charts())).toEqual(['cadence']);
    const cadence = charts()['cadence'];
    expect(cadence.series.map((s) => s.name)).toEqual(['Runs', 'Monthly average']);
    expect(cadence.series[0].data.map((p) => p.value)).toEqual([['2026-09-08T18:00:00', 168], ['2026-09-13T08:00:00', 164]]);
    expect(cadence.series[1].data.map((p) => p.value)).toEqual([['2026-09-15', 166]]);
  });

  it('shades each Break within the Runs’ dates over its days, named by its kind', async () => {
    const injury: Break = { id: 1, kind: 'injury', start: '2026-09-10', end: '2026-09-12', note: 'Calf strain' };
    const later: Break = { id: 2, kind: 'holiday', start: '2026-12-20', end: '2026-12-31', note: null };

    const { charts } = await render(fitness, [injury, later]);

    // The holiday is after the last Run, so it is left out.
    const bands = charts()['cadence'].series[1].markArea!.data;
    expect(bands.length).toBe(1);
    expect(bands[0][0]).toEqual(expect.objectContaining({ xAxis: '2026-09-10', name: 'Injury' }));
    expect(bands[0][1]).toEqual({ xAxis: '2026-09-13' });
  });

  it("names the Run in a dot's tooltip, its name made safe for HTML", async () => {
    const { charts } = await render();

    const cadence = charts()['cadence'];
    const tooltip = cadence.tooltip.formatter({ data: cadence.series[0].data[1] });
    expect(tooltip).toMatch(/Long &#60;fast&#62; · 13 Sept? 2026/);
    expect(tooltip).toContain('164 spm');
  });

  it('has the monthly averages as a table, newest first', async () => {
    const { element } = await render();

    expect([...element.querySelectorAll('.months tbody tr')].map((row) => [...row.querySelectorAll('td')].map((cell) => cell.textContent!.trim())))
      .toEqual([['Oct 2026', '–'], ['Sep 2026', '166 spm']]);
  });

  it('says when there are no Fitness Runs yet', async () => {
    expect((await render({ ...fitness, runs: [], months: [] })).element.querySelector('.empty')!.textContent).toBe('No Fitness Runs yet.');
  });

  it('says when the figures could not be loaded', async () => {
    expect((await render('error')).element.querySelector('[role="alert"]')!.textContent).toContain('could not be loaded');
  });
});

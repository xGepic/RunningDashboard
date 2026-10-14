import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { By } from '@angular/platform-browser';
import { NgxEchartsDirective, provideEchartsCore } from 'ngx-echarts';
import { Fitness } from './fitness';
import { FitnessPage } from './fitness-page';

const fitness: Fitness = {
  goodCardiacDrift: 0.05,
  runs: [
    { id: 1, name: 'Easy', startLocal: '2026-09-08T18:00:00', distance: 8000, aerobicEfficiency: 1.19, cardiacDrift: null, cadence: 168 },
    { id: 2, name: 'Long <fast>', startLocal: '2026-09-13T08:00:00', distance: 12000, aerobicEfficiency: 1.05, cardiacDrift: 0.05, cadence: 164 },
    { id: 3, name: 'Shakeout', startLocal: '2026-10-02T07:00:00', distance: 5000, aerobicEfficiency: null, cardiacDrift: null, cadence: null },
  ],
  months: [
    { year: 2026, month: 9, aerobicEfficiency: 1.12, cardiacDrift: 0.05, cadence: 166 },
    { year: 2026, month: 10, aerobicEfficiency: null, cardiacDrift: null, cadence: null },
  ],
};

interface ChartOptions {
  series: { name: string; data: { value: [string, number]; title: string }[]; markArea?: { data: { yAxis: number }[][] } }[];
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
  });

  afterEach(() => vi.unstubAllGlobals());

  async function render(answer: Fitness | 'error' = fitness) {
    const fixture = TestBed.createComponent(FitnessPage);
    const http = TestBed.inject(HttpTestingController);
    fixture.detectChanges();
    for (const settings of http.match('/api/settings')) settings.flush({ distanceUnit: 'km' });
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

  it('charts each figure as a dot per Run that has it and a line through the monthly averages that have it', async () => {
    const { charts } = await render();

    const efficiency = charts()['efficiency'];
    expect(efficiency.series.map((s) => s.name)).toEqual(['Runs', 'Monthly average']);
    expect(efficiency.series[0].data.map((p) => p.value)).toEqual([['2026-09-08T18:00:00', 1.19], ['2026-09-13T08:00:00', 1.05]]);
    expect(efficiency.series[1].data.map((p) => p.value)).toEqual([['2026-09-15', 1.12]]);
    expect(charts()['cadence'].series[0].data.map((p) => p.value[1])).toEqual([168, 164]);
  });

  it('shades Cardiac Drift as good up to 5%, and only charts the Long Runs that have it', async () => {
    const { charts } = await render();

    const drift = charts()['drift'];
    expect(drift.series[0].data.map((p) => p.value)).toEqual([['2026-09-13T08:00:00', 0.05]]);
    expect(drift.series[0].markArea!.data).toEqual([[{ yAxis: 0 }, { yAxis: 0.05 }]]);
  });

  it("names the Run in a dot's tooltip, its name made safe for HTML", async () => {
    const { charts } = await render();

    const drift = charts()['drift'];
    const tooltip = drift.tooltip.formatter({ data: drift.series[0].data[0] });
    expect(tooltip).toMatch(/Long &#60;fast&#62; · 13 Sept? 2026/);
    expect(tooltip).toContain('5.0%');
  });

  it('has the monthly averages as a table, newest first', async () => {
    const { element } = await render();

    expect([...element.querySelectorAll('.months tbody tr')].map((row) => [...row.querySelectorAll('td')].map((cell) => cell.textContent!.trim())))
      .toEqual([['Oct 2026', '–', '–', '–'], ['Sep 2026', '1.12 m/beat', '5.0%', '166 spm']]);
  });

  it('says when there are no Fitness Runs yet', async () => {
    expect((await render({ ...fitness, runs: [], months: [] })).element.querySelector('.empty')!.textContent).toBe('No Fitness Runs yet.');
  });

  it('says when the figures could not be loaded', async () => {
    expect((await render('error')).element.querySelector('[role="alert"]')!.textContent).toContain('could not be loaded');
  });
});

import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { Router, provideRouter } from '@angular/router';
import { By } from '@angular/platform-browser';
import { NgxEchartsDirective, provideEchartsCore } from 'ngx-echarts';
import { Break } from '../shared/breaks';
import { ChartColors, ChartTheme } from '../shared/chart-theme';
import { Freshness, FreshnessDay } from './freshness';
import { FreshnessCard } from './freshness-card';

/**
 * A year of days up to Fri 2026-10-09: Fitness builds from 20 to 41 on 5 Sep, then falls back to 24; Fatigue is 30,
 * but 10 over the last week. A Run of 50 every other day, and a marathon of 487 on Sun 27 Sep.
 */
function freshness(): Freshness {
  const days: FreshnessDay[] = Array.from({ length: 365 }, (_, i) => {
    const date = new Date(2025, 9, 10 + i);
    const iso = `${date.getFullYear()}-${String(date.getMonth() + 1).padStart(2, '0')}-${String(date.getDate()).padStart(2, '0')}`;
    const fitness = i <= 330 ? 20 + (21 * i) / 330 : 41 - (i - 330) * 0.5;
    const fatigue = i >= 358 ? 10 : 30;
    const marathon = iso === '2026-09-27';
    const load = marathon ? 487 : i % 2 ? 0 : 50;
    return { date: iso, load, fitness, fatigue, form: fitness - fatigue, runId: marathon ? 99 : load ? i : null, runName: marathon ? 'Finisher.' : load ? 'Easy' : null };
  });
  return {
    days,
    races: [
      { date: '2026-03-15', name: 'Spring Half' },
      { date: '2026-09-20', name: 'Autumn <10K>' },
      { date: '2027-04-11', name: 'Next spring' },
    ],
  };
}

/** jsdom has no CSS tokens to read: Fitness blue, Fatigue orange. */
const colors = { series: '#4a9eff', fatigue: '#d95926', breaks: { injury: '#e8857a', illness: '#f2c46d', holiday: '#7fb2ff', other: '#a0a8b8' } } as ChartColors;

interface ChartOptions {
  series: {
    name: string;
    data: [string, number][];
    markLine?: { silent?: boolean; data: { xAxis: string; name: string; runId?: number }[] };
    markArea?: { data: { xAxis: string; name?: string }[][] };
  }[];
  tooltip: { formatter: (params: { dataIndex: number }[]) => string };
}

describe('FreshnessCard', () => {
  beforeEach(() => {
    vi.stubGlobal('ResizeObserver', class { observe() {} unobserve() {} disconnect() {} });
    TestBed.configureTestingModule({
      imports: [FreshnessCard],
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        provideRouter([]),
        // jsdom cannot draw; the tests read the chart's options instead.
        provideEchartsCore({ echarts: { init: () => new Proxy({}, { get: () => () => undefined }) } }),
        { provide: ChartTheme, useValue: { colors: () => colors } },
      ],
    });
  });

  afterEach(() => vi.unstubAllGlobals());

  async function render(answer: Freshness | 'error' = freshness(), breaks: Break[] = []) {
    const fixture = TestBed.createComponent(FreshnessCard);
    const http = TestBed.inject(HttpTestingController);
    fixture.detectChanges();
    for (const request of http.match('/api/breaks')) request.flush(breaks);
    const request = http.expectOne('/api/fitness/freshness');
    if (answer === 'error') request.flush(null, { status: 500, statusText: 'Server Error' });
    else request.flush(answer);
    await fixture.whenStable();
    fixture.detectChanges();
    const chart = () => fixture.debugElement.query(By.directive(NgxEchartsDirective))?.injector.get(NgxEchartsDirective).options() as unknown as ChartOptions | undefined;
    const pick = (label: string) => {
      fixture.debugElement.queryAll(By.css('.segmented button')).find((b) => b.nativeElement.textContent.trim() === label)!.nativeElement.click();
      fixture.detectChanges();
    };
    const click = (event: object) => fixture.debugElement.query(By.directive(NgxEchartsDirective)).triggerEventHandler('chartClick', event);
    return { element: fixture.nativeElement as HTMLElement, chart, pick, click };
  }

  const texts = (element: Element, selector: string) => [...element.querySelectorAll(selector)].map((e) => e.textContent!.replace(/\s+/g, ' ').trim());

  it("heads the card with where the Athlete is, with today's figures small beneath", async () => {
    const { element } = await render();

    expect(texts(element, '.headline')).toEqual(["You're recovering: training less than your recent average, so your Fitness is dropping."]);
    expect(texts(element, '.stat .label')).toEqual(['Fitness', 'Fatigue', 'Form']);
    expect(texts(element, '.stat .value')).toEqual(['24', '10', '+14']);
  });

  it('charts Fitness over a faint Fatigue, naming the Goal Races and the hardest day, and shading Breaks', async () => {
    const breaks: Break[] = [{ id: 1, start: '2026-06-01', end: '2026-06-07', kind: 'holiday', note: null }];
    const { chart } = await render(freshness(), breaks);

    const [fatigue, fitness] = chart()!.series;
    expect([fatigue.name, fitness.name]).toEqual(['Fatigue', 'Fitness']);
    expect(fitness.data[0][0]).toBe('2026-04-10');
    expect(fitness.data.at(-1)).toEqual(['2026-10-09', 24]);
    // Goal Races are named but not clicked; the hardest day opens its Run.
    expect(fitness.markLine).toEqual(expect.objectContaining({ silent: true, data: [{ xAxis: '2026-09-20', name: 'Autumn <10K>' }] }));
    expect(fatigue.markLine!.silent).toBeUndefined();
    expect(fatigue.markLine!.data).toEqual([{ xAxis: '2026-09-27', name: 'Finisher. · 27 Sep', runId: 99 }]);
    expect(fitness.markArea!.data.map(([start, end]) => [start.xAxis, start.name, end.xAxis])).toEqual([['2026-06-01', 'Holiday', '2026-06-08']]);
  });

  it("names the day's hardest Run and its figures in the tooltip, and a Goal Race on it", async () => {
    const { chart } = await render();

    const options = chart()!;
    const index = (date: string) => options.series[1].data.findIndex(([d]) => d === date);
    const marathon = options.tooltip.formatter([{ dataIndex: index('2026-09-27') }]);
    expect(marathon).toContain('Sun 27 Sep 2026 · Finisher.');
    expect(marathon).toContain('Training Load');
    expect(marathon).toContain('487');
    expect(options.tooltip.formatter([{ dataIndex: index('2026-09-20') }])).toContain('Autumn &#60;10K&#62;');
  });

  it("opens a hardest day's Run when its marker is clicked, and nothing for other clicks", async () => {
    const { chart, click } = await render();
    const navigate = vi.spyOn(TestBed.inject(Router), 'navigate').mockResolvedValue(true);

    click({ componentType: 'series', data: ['2026-09-27', 30] });
    click({ componentType: 'markLine', data: { xAxis: '2026-09-20', name: 'Autumn <10K>' } });
    expect(navigate).not.toHaveBeenCalled();

    click({ componentType: 'markLine', data: chart()!.series[0].markLine!.data[0] });
    expect(navigate).toHaveBeenCalledWith(['/runs', 99]);
  });

  it('names a hard day on a Goal Race by the race, and opens its Run', async () => {
    const data = freshness();
    data.races.push({ date: '2026-09-27', name: 'Berlin Marathon' });
    const { chart } = await render(data);

    const [fatigue, fitness] = chart()!.series;
    expect(fatigue.markLine!.data).toEqual([{ xAxis: '2026-09-27', name: 'Berlin Marathon', runId: 99 }]);
    expect(fitness.markLine!.data.map((m) => m.name)).toEqual(['Autumn <10K>']);
  });

  it('switches the range', async () => {
    const { element, chart, pick } = await render();

    expect(texts(element, '.segmented button')).toEqual(['3M', '6M', '1Y', 'All']);
    expect(element.querySelector('.segmented button[aria-pressed="true"]')!.textContent!.trim()).toBe('6M');

    pick('3M');
    expect(chart()!.series[1].data[0][0]).toBe('2026-07-10');

    pick('All');
    expect(chart()!.series[1].data.length).toBe(365);
  });

  it('lists the range week by week, newest first, in the table', async () => {
    const { element, pick } = await render();
    pick('3M');

    const rows = [...element.querySelectorAll('tbody tr')].map((tr) => [...tr.querySelectorAll('td')].map((td) => td.textContent!.trim()));
    // Mon 5 Oct to Fri 9 Oct: Runs of 50 on the 5th, 7th and 9th.
    expect(rows[0]).toEqual(['5 Oct 2026', '150', '24', '10', '+14']);
  });

  it('says when there are no Runs yet, without a range switch', async () => {
    const { element } = await render({ days: [], races: [] });

    expect(texts(element, '.empty')).toEqual(['No Runs yet, so no Fitness or Fatigue.']);
    expect(element.querySelector('.segmented')).toBeNull();
  });

  it('has no title, but heads the card with where the Athlete is', async () => {
    const { element } = await render();

    expect(element.querySelector('.card-title')).toBeNull();
    expect(element.querySelector('h2')!.textContent).toContain("You're recovering");
    expect(element.querySelector('section')!.getAttribute('aria-label')).toBe('Fitness & Freshness');
  });

  it('says when the figures could not be loaded', async () => {
    const { element } = await render('error');

    expect(texts(element, '[role="alert"]')).toEqual(['Fitness & Freshness could not be loaded.']);
  });
});

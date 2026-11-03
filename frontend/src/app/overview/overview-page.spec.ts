import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { By } from '@angular/platform-browser';
import { provideRouter } from '@angular/router';
import { NgxEchartsDirective, provideEchartsCore } from 'ngx-echarts';
import { Overview } from './overview';
import { OverviewPage } from './overview-page';

/** Friday 9 Oct 2026: a Run and a Ride this week, a 7-hour Weekly Time Goal and a Yearly Goal. */
const overview: Overview = {
  totals: { runs: 1, distance: 10000, movingTime: 3600, elevationGain: 0 },
  years: [{ year: 2026, runs: 1, distance: 10000, movingTime: 3600, elevationGain: 0 }],
  weeks: [{ start: '2026-10-05', distance: 10000 }],
  yearlyGoal: { year: 2026, distance: 2_000_000 },
  weeklyTimeGoal: { time: 7 * 3600 },
  streak: { current: 1, start: '2026-10-05', thisWeekHasRun: true, longest: { weeks: 1, start: '2026-10-05', end: '2026-10-11' }, recentWeeks: [{ start: '2026-10-05', hasRun: true }] },
  trainingTime: [{ start: '2026-10-05', runs: 3600, rides: 7200, swims: 0 }],
  thisWeek: { start: '2026-10-05', runs: 3600, rides: 7200, swims: 0 },
  onThisDay: null,
  // Vienna, Bratislava at 50 km (reached on 14 Sep), Budapest at 200 km, back to Vienna at 400 km.
  journey: {
    year: 2026,
    distance: 163_400,
    length: 400_000,
    yearlyGoal: null,
    finished: false,
    stops: [
      { name: 'Vienna', country: 'Austria', latitude: 48, longitude: 16, at: 0, reached: null },
      { name: 'Bratislava', country: 'Slovakia', latitude: 48, longitude: 17, at: 50_000, reached: { date: '2026-09-14', runId: 7, runName: 'Long one' } },
      { name: 'Budapest', country: 'Hungary', latitude: 47, longitude: 19, at: 200_000, reached: null },
      { name: 'Vienna', country: 'Austria', latitude: 48, longitude: 16, at: 400_000, reached: null },
    ],
  },
};

interface ChartOptions {
  series: { markLine?: { data: { yAxis: number }[] } }[];
}

describe('OverviewPage', () => {
  beforeEach(() => {
    vi.useFakeTimers({ toFake: ['Date'] });
    vi.setSystemTime(new Date(2026, 9, 9, 18));
    vi.stubGlobal('ResizeObserver', class { observe() {} unobserve() {} disconnect() {} });
    TestBed.configureTestingModule({
      imports: [OverviewPage],
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        provideRouter([]),
        // jsdom cannot draw; the tests read the charts' options instead.
        provideEchartsCore({ echarts: { init: () => new Proxy({}, { get: () => () => undefined }) } }),
      ],
    });
  });

  afterEach(() => {
    vi.useRealTimers();
    vi.unstubAllGlobals();
  });

  async function render(settings: { showYearlyGoal?: boolean; showWeeklyTimeGoal?: boolean } = {}, changes: Partial<Overview> = {}) {
    const fixture = TestBed.createComponent(OverviewPage);
    const http = TestBed.inject(HttpTestingController);
    fixture.detectChanges();
    for (const request of http.match('/api/settings')) request.flush({ distanceUnit: 'km', ...settings });
    for (const request of http.match('/api/sync')) request.flush({ running: false, importedActivities: 0, lastSyncedAt: null });
    for (const request of http.match('/api/breaks')) request.flush([]);
    fixture.detectChanges();
    for (const request of http.match('/api/overview')) request.flush({ ...overview, ...changes });
    await fixture.whenStable();
    fixture.detectChanges();
    const element = fixture.nativeElement as HTMLElement;
    const trainingTimeChart = () =>
      fixture.debugElement.queryAll(By.directive(NgxEchartsDirective)).map((chart) => chart.injector.get(NgxEchartsDirective).options() as unknown as ChartOptions)[1];
    return { element, trainingTimeChart };
  }

  it('shows both goals with the Streak, and the weekly goal in the Training Time chart', async () => {
    const { element, trainingTimeChart } = await render();

    expect(element.querySelector('app-yearly-goal-card')).not.toBeNull();
    expect(element.querySelector('app-weekly-time-goal-card')).not.toBeNull();
    expect(element.querySelector('.goal-row')!.classList).toContain('cards-3');
    expect(trainingTimeChart().series[0].markLine!.data).toEqual([{ yAxis: 7 }]);
    expect(element.textContent).toContain('Weekly goal met');
  });

  it('leaves out the goals Settings hide', async () => {
    const { element, trainingTimeChart } = await render({ showYearlyGoal: false, showWeeklyTimeGoal: false });

    expect(element.querySelector('app-yearly-goal-card')).toBeNull();
    expect(element.querySelector('app-weekly-time-goal-card')).toBeNull();
    expect(element.querySelector('.goal-row')!.classList).toContain('cards-1');
    expect(trainingTimeChart().series[0].markLine).toBeUndefined();
    expect(element.textContent).not.toContain('Weekly goal met');
  });

  it('hides each goal on its own', async () => {
    const { element } = await render({ showYearlyGoal: false });

    expect(element.querySelector('app-yearly-goal-card')).toBeNull();
    expect(element.querySelector('app-weekly-time-goal-card')).not.toBeNull();
    expect(element.querySelector('.goal-row')!.classList).toContain('cards-2');
  });

  it("shows today's date a year ago: its distance, Runs, Pace and Moving Time", async () => {
    const { element } = await render({}, {
      onThisDay: {
        day: '2025-10-09',
        runs: [
          { id: 2, name: 'Half marathon', startLocal: '2025-10-09T17:00:00', distance: 21_097, movingTime: 7200 },
          { id: 1, name: 'Lakeside loop', startLocal: '2025-10-09T07:30:00', distance: 12_000, movingTime: 3600 },
        ],
      },
    });

    const card = element.querySelector('app-on-this-day-card')!;
    const text = (selector: string) => [...card.querySelectorAll(selector)].map((e) => e.textContent!.replace(/\s+/g, ' ').trim());
    expect(text('.card-sub')).toEqual(['9 Oct 2025, a year ago']);
    expect(text('.distance .value, .distance .unit')).toEqual(['33.1', 'km']);
    expect(text('.runs li')).toEqual(['Half marathon21.1 km', 'Lakeside loop12.0 km']);
    expect(text('.stat .value')).toEqual(['5:26 /km', '3:00:00']);
    expect([...element.querySelectorAll('app-on-this-day-card a')].map((a) => a.getAttribute('href'))).toEqual(['/runs/2', '/runs/1']);
    expect(element.querySelector('.goal-row > app-on-this-day-card')).not.toBeNull();
    expect(element.querySelector('.goal-row')!.classList).toContain('cards-4');
  });

  it('shows where the Athlete is on the Virtual Journey, and opens its page', async () => {
    const { element } = await render();

    const card = element.querySelector('app-journey-card')!;
    const text = (selector: string) => [...card.querySelectorAll(selector)].map((e) => e.textContent!.replace(/\s+/g, ' ').trim());
    expect(text('.card-sub')).toEqual(['2026 · Vienna and back via 2 cities, 400 km']);
    expect(text('.headline .value, .headline .unit')).toEqual(['37', 'km to Budapest']);
    expect(text('.leg')).toEqual(['Left Bratislava on 14 Sep']);
    expect(text('.stat .value')).toEqual(['163 km', '1 of 3', '41 %']);
    expect(text('.stop.current .name')).toEqual(['Bratislava', 'Budapest']);
    expect(card.querySelectorAll('.stop.reached').length).toBe(1);
    expect(card.querySelector('a')!.getAttribute('href')).toBe('/journey');
    // Under the goal row, across the Overview.
    expect(element.querySelector('.goal-row + app-journey-card')).not.toBeNull();
  });

  it('leaves out On this day without Runs on this date a year ago', async () => {
    const { element } = await render();

    expect(element.querySelector('app-on-this-day-card')).toBeNull();
  });
});

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

  async function render(settings: { showYearlyGoal?: boolean; showWeeklyTimeGoal?: boolean } = {}) {
    const fixture = TestBed.createComponent(OverviewPage);
    const http = TestBed.inject(HttpTestingController);
    fixture.detectChanges();
    for (const request of http.match('/api/settings')) request.flush({ distanceUnit: 'km', ...settings });
    for (const request of http.match('/api/sync')) request.flush({ running: false, importedActivities: 0, lastSyncedAt: null });
    for (const request of http.match('/api/breaks')) request.flush([]);
    fixture.detectChanges();
    for (const request of http.match('/api/overview')) request.flush(overview);
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
});

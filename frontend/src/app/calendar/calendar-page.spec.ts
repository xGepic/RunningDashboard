import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { By } from '@angular/platform-browser';
import { Router, provideRouter, withComponentInputBinding } from '@angular/router';
import { RouterTestingHarness } from '@angular/router/testing';
import { NgxEchartsDirective, provideEchartsCore } from 'ngx-echarts';
import { Connection } from '../connection/connection';
import { Ride } from '../rides/ride';
import { MonthTotals, Run, RunMonths } from '../runs/run';
import { Break } from '../shared/breaks';
import { Swim } from '../swims/swim';
import { ImageDownload } from '../shared/image-download';
import { calendarRoutes } from './calendar-routes';
import { YearReview } from './review';

const empty = (month: number): MonthTotals => ({ month, runs: 0, distance: 0, movingTime: 0, pace: null });

/** 2026 up to Mon 12 Oct: three Runs in September, two in October. */
const year2026: RunMonths = {
  year: 2026,
  firstYear: 2024,
  months: Array.from({ length: 12 }, (_, i) => empty(i + 1)).map((m) =>
    m.month === 9
      ? { month: 9, runs: 3, distance: 60688.2, movingTime: 26263, pace: 432.75 }
      : m.month === 10
        ? { month: 10, runs: 2, distance: 9594.9, movingTime: 3981, pace: 414.9 }
        : m,
  ),
};

const run = (id: number, name: string, startLocal: string, distance: number): Run => ({
  id, name, sportType: 'Run', startLocal, distance, movingTime: distance * 0.36, pace: 360, shoeName: null,
});

/** The Training Weeks around September 2026, Mon 31 Aug to Sun 4 Oct. */
const septemberWeeks: Run[] = [
  run(1, 'Late August jog', '2026-08-31T07:00:00', 5000),
  run(2, 'Tempo Tuesday', '2026-09-08T18:00:00', 10000),
  run(3, 'Evening shakeout', '2026-09-08T21:00:00', 3000),
  run(4, 'Sunday long run with a very long name', '2026-09-13T08:00:00', 21000),
  run(5, 'October opener', '2026-10-03T09:00:00', 8000),
];

const ride = (id: number, name: string, startLocal: string): Ride => ({
  id, name, sportType: 'VirtualRide', startLocal, distance: 30000, movingTime: 3600, speed: 30, elevationGain: 0, stravaUrl: '',
});

const swim = (id: number, name: string, startLocal: string): Swim => ({
  id, name, startLocal, distance: 2000, movingTime: 2400, pace: 120, stravaUrl: '',
});

/** The text of an element's parts, one space between them: "September 60.7 km 7h 17m". */
const text = (element: Element) =>
  [...element.children].map((part) => part.textContent!.replace(/\s+/g, ' ').trim()).filter(Boolean).join(' ');

describe('CalendarPage', () => {
  /** What GET /api/breaks answers, whenever it is asked. */
  let breaks: Break[] = [];

  beforeEach(() => {
    breaks = [];
    vi.useFakeTimers({ toFake: ['Date'] });
    vi.setSystemTime(new Date(2026, 9, 12, 9));
    // The chart resizes with its card; jsdom has no ResizeObserver.
    vi.stubGlobal('ResizeObserver', class { observe() {} unobserve() {} disconnect() {} });
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        provideRouter(calendarRoutes, withComponentInputBinding()),
        // jsdom cannot draw; the tests read the chart's options instead.
        provideEchartsCore({ echarts: { init: () => new Proxy({}, { get: () => () => undefined }) } }),
      ],
    });
  });

  afterEach(() => {
    vi.useRealTimers();
    vi.unstubAllGlobals();
  });

  /** Opens `url`, answering the monthly totals with `months`, which are of all Runs. */
  async function open(url: string, months: RunMonths = year2026, unit: 'km' | 'mi' = 'km') {
    const harness = await RouterTestingHarness.create();
    const http = TestBed.inject(HttpTestingController);
    /** Lets a navigation or a response take effect; requests it sets off stay open. */
    const settle = async () => {
      await new Promise((resolve) => setTimeout(resolve));
      for (const request of http.match('/api/breaks')) request.flush(breaks);
      harness.detectChanges();
    };
    await harness.navigateByUrl(url);
    for (const sync of http.match('/api/sync')) sync.flush({ running: false, importedActivities: 0, lastSyncedAt: null });
    for (const settings of http.match('/api/settings')) settings.flush({ distanceUnit: unit });
    harness.detectChanges();
    http
      .expectOne((r) => r.url === '/api/runs/months' && r.params.get('year') === String(months.year) && !r.params.has('sportType'))
      .flush(months);
    await settle();
    const element = harness.routeNativeElement!;
    return { harness, http, element, settle, boxes: () => [...element.querySelectorAll('.month-box')] };
  }

  /** Opens a month's `url`, answering its Training Weeks' Runs with `runs`, and Rides and Swims, from `from` to `to`. */
  async function openMonth(
    url: string, from: string, to: string, runs: Run[] = septemberWeeks, unit: 'km' | 'mi' = 'km',
    { rides = [], swims = [] }: { rides?: Ride[]; swims?: Swim[] } = {},
  ) {
    const page = await open(url, year2026, unit);
    const answer = (list: string, items: unknown[], totals: unknown) => {
      const request = page.http.expectOne((r) => r.url === list);
      expect(Object.fromEntries(request.request.params.keys().map((key) => [key, request.request.params.get(key)]))).toEqual({
        from, to, pageSize: '200', sort: 'date', direction: 'asc',
      });
      request.flush({ items, page: 1, pageSize: 200, totalCount: items.length, totals });
    };
    answer('/api/runs', runs, undefined);
    answer('/api/rides', rides, { rides: rides.length, distance: 0, movingTime: 0, speed: null });
    answer('/api/swims', swims, { swims: swims.length, distance: 0, movingTime: 0, pace: null });
    await page.settle();
    const days = () => [...page.harness.routeNativeElement!.querySelectorAll('.week .day')];
    return { ...page, days };
  }

  /** A day as "8: Tempo Tuesday, Evening shakeout", with "(other month)" for the faded days around the month. */
  const day = (cell: Element) => {
    const names = [...cell.querySelectorAll('a .name')].map((name) => name.textContent!.trim());
    const date = cell.querySelector('.date-number')!.textContent!.trim();
    return `${date}${cell.classList.contains('other-month') ? ' (other month)' : ''}${names.length ? ': ' + names.join(', ') : ''}`;
  };

  describe('year view', () => {
    it("lists the year's Breaks under its months", async () => {
      breaks = [
        { id: 1, kind: 'injury', start: '2025-12-28', end: '2026-01-04', note: null },
        { id: 2, kind: 'holiday', start: '2026-08-01', end: '2026-08-14', note: 'Crete' },
      ];

      const { element, settle } = await open('/calendar');
      await settle();

      expect([...element.querySelectorAll('app-breaks-panel li .days')].map((days) => days.textContent!.trim())).toEqual([
        '28 Dec 2025 – 4 Jan 2026',
        '1 Aug – 14 Aug 2026',
      ]);
    });

    it('shows a box per month with its distance and Moving Time', async () => {
      const { boxes } = await open('/calendar');

      expect(boxes().map(text)).toEqual([
        'January –', 'February –', 'March –', 'April –', 'May –', 'June –', 'July –', 'August –',
        'September 60.7 km 7h 17m', 'October 9.6 km 1h 06m', 'November', 'December',
      ]);
    });

    it('opens a month from its box, even one without Runs, but not one still ahead', async () => {
      const { boxes } = await open('/calendar');

      expect(boxes().map((box) => box.getAttribute('href'))).toEqual([
        '/calendar#Jan', '/calendar#Feb', '/calendar#Mar', '/calendar#Apr', '/calendar#May', '/calendar#Jun',
        '/calendar#Jul', '/calendar#Aug', '/calendar#Sep', '/calendar#Oct', null, null,
      ]);
    });

    it('puts any other year in the path of its months', async () => {
      const { boxes } = await open('/calendar/2025', { ...year2026, year: 2025 });

      expect(boxes()[11].getAttribute('href')).toBe('/calendar/2025#Dec');
    });

    it('steps through the years from the first with Runs to the current one', async () => {
      const { harness, http, settle } = await open('/calendar');
      // /calendar and /calendar/2025 are different routes: the page is rendered anew.
      const step = (label: string) => harness.routeNativeElement!.querySelector<HTMLButtonElement>(`button[aria-label^="${label}"]`)!;
      const answer = async (year: number) => {
        await settle();
        http.expectOne((r) => r.url === '/api/runs/months' && r.params.get('year') === String(year)).flush({ ...year2026, year });
        await settle();
      };

      expect(step('Next year').disabled).toBe(true);
      step('Previous year').click();
      await settle();
      expect(TestBed.inject(Router).url).toBe('/calendar/2025');

      await answer(2025);
      step('Previous year').click();
      await settle();
      await answer(2024);
      expect(step('Previous year').disabled).toBe(true);

      step('Next year').click();
      await settle();
      expect(TestBed.inject(Router).url).toBe('/calendar/2025');
    });

    it('shows miles when they are chosen in Settings, with no switch of its own', async () => {
      const { boxes, element } = await open('/calendar', year2026, 'mi');

      expect(text(boxes()[8])).toBe('September 37.7 mi 7h 17m');
      expect(element.querySelector('[aria-label="Units"]')).toBeNull();
    });

    it('ignores the unit of an old link', async () => {
      const { boxes } = await open('/calendar?unit=mi');

      expect(text(boxes()[8])).toBe('September 60.7 km 7h 17m');
    });

    it("draws a trend through the finished months' distances only, carried on to the end of the year", async () => {
      // 100 km in January, 10 km more every month up to 180 km in September; October has only begun.
      const steady: RunMonths = {
        ...year2026,
        months: year2026.months.map((m) => ({ ...m, runs: m.month <= 10 ? 1 : 0, distance: m.month <= 9 ? 90_000 + m.month * 10_000 : m.month === 10 ? 5000 : 0 })),
      };
      const { harness } = await open('/calendar', steady);
      const chart = harness.fixture.debugElement.query(By.directive(NgxEchartsDirective)).injector.get(NgxEchartsDirective);
      const series = (chart.options() as { series: { name?: string; data: number[] }[] }).series;

      expect(series[1].name).toBe('Trend');
      expect(series[1].data).toEqual([100, 110, 120, 130, 140, 150, 160, 170, 180, 190, 200, 210]);
    });

    it('says how many hours and days were run this year, once that is more than a day', async () => {
      // 161 h 6 m 40 s in all: 6.7 days.
      const busy: RunMonths = { ...year2026, months: year2026.months.map((m) => ({ ...m, movingTime: m.month <= 10 ? 58000 : 0 })) };
      const { element } = await open('/calendar', busy);

      expect(element.querySelector('.time-run')!.textContent!.trim()).toBe("You have run 161 hours this year, that's 6.7 days.");
    });

    it('names the year when it is not the current one', async () => {
      const busy: RunMonths = { ...year2026, year: 2025, months: year2026.months.map((m) => ({ ...m, movingTime: 36000 })) };
      const { element } = await open('/calendar/2025', busy);

      expect(element.querySelector('.time-run')!.textContent!.trim()).toBe("You have run 120 hours in 2025, that's 5.0 days.");
    });

    it('says nothing about the hours run before they add up to more than a day', async () => {
      // 8 h 24 m in all.
      const { element } = await open('/calendar');

      expect(element.querySelector('.time-run')).toBeNull();
    });

    it('always shows all Runs, whatever type an old link asks for', async () => {
      const { element } = await open('/calendar?sportType=TrailRun');

      expect(element.querySelector('[aria-label="Type"]')).toBeNull();
    });

    it('charts the whole year, with a distance for each month so far, and opens a month from its point', async () => {
      const { harness, settle } = await open('/calendar', year2026, 'mi');
      const chart = harness.fixture.debugElement.query(By.directive(NgxEchartsDirective));
      const options = chart.injector.get(NgxEchartsDirective).options() as { xAxis: { data: string[] }; series: { data: (number | null)[] }[] };

      expect(options.xAxis.data).toEqual(['Jan', 'Feb', 'Mar', 'Apr', 'May', 'Jun', 'Jul', 'Aug', 'Sep', 'Oct', 'Nov', 'Dec']);
      // November and December are still ahead: no point yet.
      expect(options.series[0].data.slice(8)).toEqual([37.7, 6, null, null]);

      chart.triggerEventHandler('chartClick', { dataIndex: 8 });
      await settle();
      expect(TestBed.inject(Router).url).toBe('/calendar#Sep');
    });
  });

  describe('month view', () => {
    it("shows the month's Training Weeks Monday to Sunday with each day's Runs, linking to them", async () => {
      const { days, harness } = await openMonth('/calendar#Sep', '2026-08-31', '2026-10-04');

      const all = days();
      expect(all.length).toBe(35);
      expect(all.slice(0, 7).map(day)).toEqual([
        '31 (other month): Late August jog', '1', '2', '3', '4', '5', '6',
      ]);
      expect(day(all[8])).toBe('8: Tempo Tuesday, Evening shakeout');
      expect(day(all[13])).toBe('13: Sunday long run with a very long name');
      expect(all.slice(28).map(day)).toEqual([
        '28', '29', '30', '1 (other month)', '2 (other month)', '3 (other month): October opener', '4 (other month)',
      ]);
      expect(all[8].querySelector('a')!.getAttribute('href')).toBe('/runs/2');
      // The full name shows on hover, as long ones are cut short.
      expect(all[13].querySelector('a')!.getAttribute('title')).toBe('Sunday long run with a very long name');
      expect(harness.routeNativeElement!.querySelector('.month-box')).toBeNull();
    });

    it("tints a Break's days in its kind's colour and names it on its first day in each Training Week", async () => {
      breaks = [
        { id: 1, kind: 'injury', start: '2026-09-10', end: '2026-09-16', note: 'Calf strain' },
        { id: 2, kind: 'holiday', start: '2026-10-01', end: '2026-10-09', note: null },
      ];

      const { days } = await openMonth('/calendar#Sep', '2026-08-31', '2026-10-04');

      const all = days();
      const onBreak = all.filter((cell) => cell.classList.contains('on-break')).map((cell) => cell.querySelector('.date-number')!.textContent!.trim());
      expect(onBreak).toEqual(['10', '11', '12', '13', '14', '15', '16', '1', '2', '3', '4']);
      expect(all[10].classList).toContain('break-injury');
      expect(all[10].getAttribute('title')).toBe('Injury · Calf strain');
      const named = all.filter((cell) => cell.querySelector('.break-tag'));
      expect(named.map((cell) => `${cell.querySelector('.date-number')!.textContent!.trim()} ${cell.querySelector('.break-tag')!.textContent!.trim()}`)).toEqual([
        '10 Injury', '14 Injury', '1 Holiday',
      ]);
      expect(all[9].getAttribute('title')).toBeNull();
    });

    it("lists the month's Breaks under it", async () => {
      breaks = [
        { id: 1, kind: 'injury', start: '2026-08-28', end: '2026-09-03', note: 'Calf strain' },
        { id: 2, kind: 'holiday', start: '2026-10-01', end: '2026-10-09', note: null },
      ];

      const { element } = await openMonth('/calendar#Sep', '2026-08-31', '2026-10-04');

      // The October holiday's days in the grid are not September's.
      const items = [...element.querySelectorAll('app-breaks-panel li')].map((li) => [...li.children].slice(0, 4).map((part) => part.textContent!.trim()));
      expect(items).toEqual([['Injury', '28 Aug – 3 Sept 2026', '7 days', 'Calf strain']]);
    });

    it("adds up each whole Training Week next to its Sunday, and shows the month's own totals at the top", async () => {
      const { element } = await openMonth('/calendar#Sep', '2026-08-31', '2026-10-04');

      // Mon 31 Aug and Sat 3 Oct count for their weeks, as Training Weeks run Monday to Sunday.
      expect([...element.querySelectorAll('.week .week-total')].map((total) => total.textContent!.trim())).toEqual([
        '5.0 km', '34.0 km', '–', '–', '8.0 km',
      ]);
      const summary = [...element.querySelectorAll('.summary .stat')].map(text);
      expect(summary).toEqual(['Runs 3', 'Distance 60.7 km', 'Moving Time 7h 17m', 'Avg Pace 7:13 /km']);
      expect(element.querySelector('h1')!.textContent!.trim()).toBe('September 2026');
    });

    it('shows Rides and Swims in their days in the order they started, linking to them, without counting them', async () => {
      const { days, element } = await openMonth('/calendar#Sep', '2026-08-31', '2026-10-04', septemberWeeks, 'km', {
        rides: [ride(11, 'Zwift spin', '2026-09-08T19:30:00'), ride(12, 'Gravel loop', '2026-09-20T10:00:00')],
        swims: [swim(21, 'Morning swim', '2026-09-08T06:30:00')],
      });

      const all = days();
      // The sport is spoken before a Ride's or Swim's name, as it is only shown by colour.
      expect(day(all[8])).toBe('8: Swim: Morning swim, Tempo Tuesday, Ride: Zwift spin, Evening shakeout');
      expect(day(all[20])).toBe('20: Ride: Gravel loop');
      expect([...all[8].querySelectorAll('a')].map((a) => [a.getAttribute('href'), a.className])).toEqual([
        ['/swims/21', 'swim'], ['/runs/2', 'run'], ['/rides/11', 'ride'], ['/runs/3', 'run'],
      ]);
      expect(all[8].querySelector('a.ride')!.getAttribute('title')).toBe('Zwift spin');
      expect([...element.querySelectorAll('.week .week-total')].map((total) => total.textContent!.trim())).toEqual([
        '5.0 km', '34.0 km', '–', '–', '8.0 km',
      ]);
      expect(text(element.querySelector('.sport-legend')!)).toBe("Runs Rides Swims The week's distance counts Runs only.");
    });

    it("shows each activity's distance under its name, a Swim's in metres, and none without one", async () => {
      const { days } = await openMonth('/calendar#Sep', '2026-08-31', '2026-10-04', septemberWeeks, 'km', {
        rides: [ride(11, 'Zwift spin', '2026-09-08T19:30:00'), { ...ride(12, 'Trainer', '2026-09-20T10:00:00'), distance: 0 }],
        swims: [swim(21, 'Morning swim', '2026-09-08T06:30:00')],
      });

      const distances = (cell: Element) => [...cell.querySelectorAll('a')].map((a) => a.querySelector('.distance')?.textContent!.trim() ?? null);
      expect(distances(days()[8])).toEqual(['2,000 m', '10.0 km', '30.0 km', '3.0 km']);
      expect(distances(days()[20])).toEqual([null]);
    });

    it("shows Run and Ride distances in miles when they are chosen in Settings, but a Swim's still in metres", async () => {
      const { days } = await openMonth('/calendar#Sep', '2026-08-31', '2026-10-04', septemberWeeks, 'mi', {
        rides: [ride(11, 'Zwift spin', '2026-09-13T15:00:00')],
        swims: [swim(21, 'Morning swim', '2026-09-13T06:30:00')],
      });

      expect([...days()[13].querySelectorAll('a .distance')].map((d) => d.textContent!.trim())).toEqual(['2,000 m', '13.0 mi', '18.6 mi']);
    });

    it('leaves the legend out when the month has only Runs', async () => {
      const { element } = await openMonth('/calendar#Sep', '2026-08-31', '2026-10-04');

      expect(element.querySelector('.sport-legend')).toBeNull();
    });

    it("says so when the month's Rides could not be loaded", async () => {
      const page = await open('/calendar#Sep');
      page.http.expectOne((r) => r.url === '/api/runs').flush({ items: septemberWeeks, page: 1, pageSize: 200, totalCount: 5 });
      page.http.expectOne((r) => r.url === '/api/rides').flush(null, { status: 500, statusText: 'Server Error' });
      page.http.expectOne((r) => r.url === '/api/swims').flush({ items: [], page: 1, pageSize: 200, totalCount: 0 });
      await page.settle();

      expect(page.element.querySelector('[role="alert"]')!.textContent!.trim()).toBe("The month's activities could not be loaded.");
      expect(page.element.querySelector('.month-grid')).toBeNull();
    });

    it('shows the totals in miles when they are chosen in Settings', async () => {
      const { element } = await openMonth('/calendar#Sep', '2026-08-31', '2026-10-04', septemberWeeks, 'mi');

      expect(element.querySelectorAll('.week .week-total')[1].textContent!.trim()).toBe('21.1 mi');
      expect(text(element.querySelectorAll('.summary .stat')[3])).toBe('Avg Pace 11:36 /mi');
    });

    it('steps month by month across the turn of the year, keeping the filters', async () => {
      const router = () => TestBed.inject(Router);
      const page = await open('/calendar/2025?from=link#Jan', { ...year2026, year: 2025 });
      const step = (label: string) => page.harness.routeNativeElement!.querySelector<HTMLButtonElement>(`button[aria-label^="${label}"]`)!;

      expect(page.element.querySelector('.period-nav h2')!.textContent!.trim()).toBe('January 2025');
      step('Previous month').click();
      await page.settle();
      expect(router().url).toBe('/calendar/2024?from=link#Dec');
    });

    it('steps from December into January of the current year', async () => {
      const page = await open('/calendar/2025#Dec', { ...year2026, year: 2025 });

      page.element.querySelector<HTMLButtonElement>('button[aria-label^="Next month"]')!.click();
      await page.settle();
      expect(TestBed.inject(Router).url).toBe('/calendar#Jan');
    });

    it('goes back no further than January of the first year with Runs, and no further ahead than this month', async () => {
      const first = await open('/calendar/2024#Jan', { ...year2026, year: 2024 });
      expect(first.element.querySelector<HTMLButtonElement>('button[aria-label^="Previous month"]')!.disabled).toBe(true);

      await first.harness.navigateByUrl('/calendar#Oct');
      first.http.expectOne((r) => r.url === '/api/runs/months' && r.params.get('year') === '2026').flush(year2026);
      await first.settle();
      const element = first.harness.routeNativeElement!;
      expect(element.querySelector<HTMLButtonElement>('button[aria-label^="Next month"]')!.disabled).toBe(true);
      expect(element.querySelector<HTMLButtonElement>('button[aria-label^="Previous month"]')!.disabled).toBe(false);
    });
  });

  describe('the Review', () => {
    const review2025: YearReview = {
      year: 2025, from: '2025-01-01', until: '2025-12-31',
      totals: { runs: 5, distance: 78000, movingTime: 27900, elevationGain: 412 },
      previousYear: { runs: 2, distance: 22000, movingTime: 7920, elevationGain: 500 },
      longestRun: { id: 6, name: 'Half Race', startLocal: '2025-03-15T09:00:00', distance: 30000, movingTime: 10800 },
      biggestWeek: { start: '2025-01-06', end: '2025-01-12', runs: 2, distance: 31000 },
      biggestMonth: { month: 1, runs: 3, distance: 43000 },
      personalBests: [
        { name: '5K', distance: 5000, elapsedTime: 1500, previousElapsedTime: 1560, runId: 5, runName: 'Sunday Run', startLocal: '2025-01-12T09:00:00' },
        { name: 'Half-Marathon', distance: 21097, elapsedTime: 7000, previousElapsedTime: null, runId: 6, runName: 'Half Race', startLocal: '2025-03-15T09:00:00' },
      ],
      longestStreak: { weeks: 2, start: '2025-01-01', end: '2025-01-12' },
      goalRaces: [{
        id: 1, name: 'Spring Half', date: '2025-03-15', distance: 21097, targetTime: 7200,
        raceResult: { runId: 6, runName: 'Half Race', elapsedTime: 7000, source: 'bestEffort', differenceFromTarget: -200 },
      }],
      mostUsedShoe: { name: 'Nike Pegasus 41', runs: 2, distance: 42000 },
      rides: 1,
      swims: 1,
    };
    const months2025: RunMonths = { ...year2026, year: 2025 };

    async function openReview(url: string, review: YearReview, months = months2025) {
      const page = await open(url, months);
      const request = page.http.expectOne((r) => r.url === '/api/review');
      expect(request.request.params.get('year')).toBe(String(review.year));
      request.flush(review);
      await page.settle();
      return page;
    }

    it("headlines the year's distance with its change, its months as bars, and its totals as tiles", async () => {
      const { element, boxes } = await openReview('/calendar/2025?view=review', review2025);

      expect(boxes()).toEqual([]);
      const hero = element.querySelector('.hero')!;
      expect(hero.querySelector('h2')!.textContent!.trim()).toBe('2025 in Runs');
      expect(text(hero.querySelector('.headline')!)).toBe('78 km +255% vs. 2024');
      expect(hero.querySelector('.sentence')!.textContent!.replace(/\s+/g, ' ').trim()).toBe('5 Runs, 8 hours on your feet and 412 m climbed.');
      const bars = [...hero.querySelectorAll('.month-bars .bar')];
      expect(bars.map((b) => b.textContent!.trim()).join('')).toBe('JFMAMJJASOND');
      expect(bars.map((b) => b.classList.contains('biggest'))).toEqual(Array.from({ length: 12 }, (_, i) => i === 8));
      expect(bars[8].getAttribute('title')).toBe('September: 60.7 km');
      expect(bars[0].getAttribute('title')).toBe('January: no Runs');
      expect([...element.querySelectorAll('.tiles .tile')].map(text)).toEqual([
        'Runs 5 +150%', 'Moving Time 7h 45m +252%', 'Elevation Gain 412 m −18%', 'Per week 1.5 km', 'Average Run 15.6 km',
      ]);
    });

    it('shows the highlights, the Personal Bests set, the Goal Races and the Rides and Swims beside', async () => {
      const { element } = await openReview('/calendar/2025?view=review', review2025);

      expect([...element.querySelectorAll('.highlight')].map(text)).toEqual([
        'Longest Run 30.0 km Half Race · 15 Mar · 3:00:00',
        'Biggest Training Week 31.0 km 6 Jan – 12 Jan · 2 Runs',
        'Biggest month 43.0 km January · 3 Runs',
        'Longest Streak 2 weeks 1 Jan – 12 Jan',
        'Most-used Shoe Nike Pegasus 41 42 km · 2 Runs',
      ]);
      expect(element.querySelector('.highlight')!.getAttribute('href')).toBe('/runs/6');
      expect(element.querySelector('.personal-bests .summary')!.textContent).toBe('2 set in 2025 · biggest leap: 5K, 3.8% faster');
      const medal = (m: Element) => [...m.querySelectorAll('.distance, .badge, .time, .gain, .was, .run')].map((part) => part.textContent!.trim()).join(' ');
      expect([...element.querySelectorAll('.medal')].map(medal)).toEqual([
        '5K Biggest leap 25:00 −1:00 · 3.8% faster was 26:00 Sunday Run · 12 Jan',
        'Half-Marathon 1:56:40 First time no earlier Best Effort Half Race · 15 Mar',
      ]);
      expect(element.querySelector('.medal .run')!.getAttribute('href')).toBe('/runs/5');
      expect(text(element.querySelector('.goal-races li')!)).toBe('Spring Half 15 Mar 2025 · 21.1 km 1:56:40 (3:20 under)');
      expect(element.querySelector('.footnote')!.textContent!.replace(/\s+/g, ' ').trim())
        .toBe('Also 1 Ride and 1 Swim, listed under Activities and not counted here.');
    });

    it.each([
      [5, [3, 2]],
      [7, [4, 3]],
      [8, [4, 4]],
      [9, [3, 3, 3]],
      [3, [3]],
    ])('lays out %i Personal Bests at most four to a row, the rows as even as they can be', async (count, expected) => {
      const personalBests = Array.from({ length: count }, (_, i) => ({ ...review2025.personalBests[0], distance: 1000 * (i + 1), name: `${i + 1}K` }));
      const { element } = await openReview('/calendar/2025?view=review', { ...review2025, personalBests });

      const rows = [0];
      for (const item of element.querySelectorAll('.medals > li')) {
        if (item.classList.contains('row-break')) rows.push(0);
        else rows[rows.length - 1]++;
      }
      expect(rows).toEqual(expected);
    });

    it('compares the current year so far with the same time last year', async () => {
      const { element } = await openReview('/calendar?view=review', { ...review2025, year: 2026, from: '2026-01-01', until: '2026-10-12' }, year2026);

      expect(element.querySelector('.hero h2')!.textContent!.replace(/\s+/g, ' ').trim()).toBe('2026 in Runs · so far, up to 12 Oct');
      expect(text(element.querySelector('.headline')!)).toContain('vs. the same time in 2025');
    });

    it('says when the history starts within the year, which has nothing to compare with', async () => {
      const { element } = await openReview('/calendar/2025?view=review', { ...review2025, from: '2025-04-28', previousYear: null });

      expect(element.querySelector('.hero h2')!.textContent!.replace(/\s+/g, ' ').trim()).toBe('2025 in Runs · from 28 Apr, when the history starts');
      expect(element.querySelector('.chip')).toBeNull();
    });

    it('says when the year has no Runs', async () => {
      const { element } = await openReview('/calendar/2025?view=review', {
        ...review2025, totals: { runs: 0, distance: 0, movingTime: 0, elevationGain: 0 }, previousYear: null, longestRun: null, biggestWeek: null,
        biggestMonth: null, personalBests: [], longestStreak: null, goalRaces: [], mostUsedShoe: null, rides: 0, swims: 0,
      });

      expect(element.querySelector('.empty')!.textContent).toBe('No Runs in 2025.');
    });

    describe('as an image', () => {
      /** What the image was drawn from, copied when it was saved, and under which file name. */
      let saved: { image: HTMLElement; fileName: string }[];
      const stefan: Connection = { connected: true, athlete: { id: 1, firstName: 'Stefan', lastName: 'Simanek' }, connectedAt: null, stravaApp: null };

      beforeEach(() => {
        saved = [];
        TestBed.configureTestingModule({
          providers: [{
            provide: ImageDownload,
            useValue: { save: async (image: HTMLElement, fileName: string) => void saved.push({ image: image.cloneNode(true) as HTMLElement, fileName }) },
          }],
        });
      });

      /** Clicks "Download image", answers the connection, and waits for the image to be saved. */
      async function download(page: { element: HTMLElement; http: HttpTestingController }, connection: Connection) {
        [...page.element.querySelectorAll('button')].find((b) => b.textContent!.trim() === 'Download image')!.click();
        page.http.expectOne('/api/connection').flush(connection);
        await vi.waitFor(() => expect(saved).toHaveLength(1));
        return saved[0];
      }

      it("saves the Review titled with the Athlete's first name, without Run names, days of the month or links", async () => {
        const page = await openReview('/calendar/2025?view=review', review2025);

        const { image, fileName } = await download(page, stefan);

        expect(fileName).toBe('Year in Review 2025.png');
        expect(image.querySelector('.hero h2')!.textContent!.replace(/\s+/g, ' ').trim()).toBe("Stefan's 2025 in Runs");
        expect(text(image.querySelector('.headline')!)).toBe('78 km +255% vs. 2024');
        expect([...image.querySelectorAll('.highlight')].map(text)).toEqual([
          'Longest Run 30.0 km Mar · 3:00:00',
          'Biggest Training Week 31.0 km Jan · 2 Runs',
          'Biggest month 43.0 km January · 3 Runs',
          'Longest Streak 2 weeks Jan',
          'Most-used Shoe Nike Pegasus 41 42 km · 2 Runs',
        ]);
        const medal = (m: Element) => [...m.querySelectorAll('.distance, .time, .run')].map((part) => part.textContent!.trim()).join(' ');
        expect([...image.querySelectorAll('.medal')].map(medal)).toEqual(['5K 25:00 Jan', 'Half-Marathon 1:56:40 Mar']);
        expect(text(image.querySelector('.goal-races li')!)).toBe('Spring Half Mar 2025 · 21.1 km 1:56:40 (3:20 under)');
        expect(image.textContent).not.toContain('Half Race');
        expect(image.textContent).not.toContain('Sunday Run');
        expect(image.querySelectorAll('a[href], button, [title]')).toHaveLength(0);
        expect(image.querySelector('.footnote')).toBeNull();
      });

      it('says "so far" for the current year, and goes without a name when no Athlete is connected', async () => {
        const page = await openReview('/calendar?view=review', { ...review2025, year: 2026, from: '2026-01-01', until: '2026-10-12' }, year2026);

        const { image, fileName } = await download(page, { connected: false, athlete: null, connectedAt: null, stravaApp: null });

        expect(fileName).toBe('Year in Review 2026.png');
        expect(image.querySelector('.hero h2')!.textContent!.replace(/\s+/g, ' ').trim()).toBe('2026 in Runs · so far');
      });
    });

    it('switches between the months and the review, keeping the year', async () => {
      const { element, settle } = await open('/calendar/2025', months2025);
      const button = (label: string) => [...element.querySelectorAll<HTMLButtonElement>('[aria-label="View"] button')].find((b) => b.textContent === label)!;
      expect(button('Months').getAttribute('aria-pressed')).toBe('true');

      button('Review').click();
      await settle();

      expect(TestBed.inject(Router).url).toBe('/calendar/2025?view=review');
      expect(button('Review').getAttribute('aria-pressed')).toBe('true');
      button('Months').click();
      await settle();
      expect(TestBed.inject(Router).url).toBe('/calendar/2025');
    });
  });
});

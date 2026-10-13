import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { provideRouter, withComponentInputBinding } from '@angular/router';
import { RouterTestingHarness } from '@angular/router/testing';
import { Run, RunPage } from './run';
import { RunsPage } from './runs-page';

const run = (id: number, name: string, startLocal: string, distance: number, movingTime: number): Run => ({
  id, name, sportType: 'Run', startLocal, distance, movingTime, pace: movingTime / (distance / 1000), shoeName: null,
});

/** October 2026 up to Mon 12 Oct. */
const october: RunPage = {
  items: [run(2, 'Easy Sunday', '2026-10-11T08:00:00', 8000, 2880), run(1, 'Tempo Tuesday', '2026-10-06T18:00:00', 10000, 3000)],
  page: 1,
  pageSize: 200,
  totalCount: 2,
};

describe('RunsPage', () => {
  beforeEach(() => {
    vi.useFakeTimers({ toFake: ['Date'] });
    vi.setSystemTime(new Date(2026, 9, 12, 9));
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        provideRouter([{ path: 'runs', component: RunsPage }], withComponentInputBinding()),
      ],
    });
  });

  afterEach(() => vi.useRealTimers());

  async function open(url: string, page: RunPage = october) {
    const harness = await RouterTestingHarness.create();
    const http = TestBed.inject(HttpTestingController);
    await harness.navigateByUrl(url);
    for (const sync of http.match('/api/sync')) sync.flush({ running: false, importedRuns: 0, lastSyncedAt: null });
    harness.detectChanges();
    const request = http.expectOne((r) => r.url === '/api/runs');
    request.flush(page);
    await new Promise((resolve) => setTimeout(resolve));
    harness.detectChanges();
    return { http, element: harness.routeNativeElement!, request: request.request };
  }

  const names = (element: HTMLElement) => [...element.querySelectorAll('.runs tbody tr .name a')].map((a) => a.textContent!.trim());

  it("lists the month's Runs, with no monthly totals view any more", async () => {
    const { http, element, request } = await open('/runs');

    expect([request.params.get('from'), request.params.get('to')]).toEqual(['2026-10-01', '2026-10-31']);
    expect(names(element)).toEqual(['Easy Sunday', 'Tempo Tuesday']);
    expect([...element.querySelectorAll('button')].map((b) => b.textContent!.trim())).not.toContain('Months');
    http.expectNone((r) => r.url === '/api/runs/months');
  });

  it('always lists all Runs, whatever type an old link asks for', async () => {
    const { element, request } = await open('/runs?sportType=TrailRun');

    expect(request.params.has('sportType')).toBe(false);
    expect(element.querySelector('[aria-label="Type"]')).toBeNull();
  });

  it('shows the list for an old link to the monthly totals', async () => {
    const { http, element } = await open('/runs?view=months&year=2025');

    expect(names(element)).toEqual(['Easy Sunday', 'Tempo Tuesday']);
    http.expectNone((r) => r.url === '/api/runs/months');
  });

  describe('the Today button', () => {
    const today = (element: HTMLElement) =>
      [...element.querySelectorAll<HTMLButtonElement>('.period-nav button')].find((b) => b.textContent!.trim() === 'Today')!;

    it('is off on the current month', async () => {
      const { element } = await open('/runs');

      expect(today(element).disabled).toBe(true);
    });

    it('goes back to the current month from an earlier one, keeping the search', async () => {
      const { http, element } = await open('/runs?month=2026-03&search=Lauf');

      today(element).click();
      await new Promise((resolve) => setTimeout(resolve));
      const request = http.expectOne((r) => r.url === '/api/runs');

      expect([request.request.params.get('from'), request.request.params.get('search')]).toEqual(['2026-10-01', 'Lauf']);
    });
  });

  describe('a day with two Runs', () => {
    const double: RunPage = {
      ...october,
      items: [run(3, 'Evening shakeout', '2026-10-11T18:30:00', 5000, 1650), ...october.items],
      totalCount: 3,
    };
    const rows = (element: HTMLElement) => [...element.querySelectorAll<HTMLElement>('.runs tbody tr')];

    it('groups them, naming the day once with how many Runs it has', async () => {
      const { element } = await open('/runs', double);

      const [evening, morning, tuesday] = rows(element);
      expect(evening.classList).toContain('same-day');
      expect(evening.querySelector('.day-count')!.textContent!.trim()).toBe('2 Runs');
      expect(morning.classList).toContain('continues-day');
      expect(morning.querySelector('.day-count')).toBeNull();
      expect(tuesday.classList).not.toContain('same-day');
      expect(tuesday.querySelector('.day-count')).toBeNull();
    });

    it('still tags each of them when sorted by something else', async () => {
      const { element } = await open('/runs?sort=distance', double);

      expect(rows(element).map((r) => r.querySelector('.day-count')?.textContent!.trim() ?? null)).toEqual(['2 Runs', '2 Runs', null]);
      expect(element.querySelector('.same-day, .continues-day')).toBeNull();
    });
  });
});

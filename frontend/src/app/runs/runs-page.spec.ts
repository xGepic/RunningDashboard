import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { Router, provideRouter } from '@angular/router';
import { MonthTotals, RunMonths } from './run';
import { RunsPage } from './runs-page';

const empty = (month: number): MonthTotals => ({ month, runs: 0, distance: 0, movingTime: 0, pace: null });

/** 2026 up to Mon 12 Oct: three Runs in September, two in October. */
const months: RunMonths = {
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

describe('RunsPage monthly totals', () => {
  beforeEach(() => {
    vi.useFakeTimers({ toFake: ['Date'] });
    vi.setSystemTime(new Date(2026, 9, 12, 9));
  });

  afterEach(() => vi.useRealTimers());

  async function render(unit?: string) {
    TestBed.configureTestingModule({
      imports: [RunsPage],
      providers: [provideHttpClient(), provideHttpClientTesting(), provideRouter([])],
    });
    const fixture = TestBed.createComponent(RunsPage);
    fixture.componentRef.setInput('view', 'months');
    if (unit) fixture.componentRef.setInput('unit', unit);
    fixture.detectChanges();
    const http = TestBed.inject(HttpTestingController);
    http.expectOne('/api/sync').flush({ running: false, importedRuns: 0, lastSyncedAt: null });
    fixture.detectChanges();
    const request = http.expectOne((r) => r.url === '/api/runs/months');
    expect(request.request.params.get('year')).toBe('2026');
    request.flush(months);
    await fixture.whenStable();
    // The list of a month's Runs is not loaded alongside.
    expect(http.match((r) => r.url === '/api/runs')).toEqual([]);
    return { fixture, element: fixture.nativeElement as HTMLElement };
  }

  const cells = (row: Element) => [...row.querySelectorAll('td')].map((cell) => cell.textContent!.trim());

  it('shows each month so far with its Runs, distance, Moving Time and Pace, and the year in the header', async () => {
    const { element } = await render();

    const rows = [...element.querySelectorAll('.months tbody tr')];
    expect(rows.map((row) => cells(row)[0])).toEqual([
      'January', 'February', 'March', 'April', 'May', 'June', 'July', 'August', 'September', 'October',
    ]);
    expect(cells(rows[8])).toEqual(['September', '3', '60.69 km', '7h 17m', '7:13 /km']);
    expect(cells(rows[0])).toEqual(['January', '–', '–', '–', '–']);
    const summary = [...element.querySelectorAll('.summary .value')].map((value) => value.textContent!.trim());
    expect(summary).toEqual(['5', '70.3 km', '8h 24m', '7:10 /km']);
  });

  it('shows distances in miles and Pace per mile when asked', async () => {
    const { element } = await render('mi');

    const rows = [...element.querySelectorAll('.months tbody tr')];
    expect(cells(rows[8])).toEqual(['September', '3', '37.71 mi', '7h 17m', '11:36 /mi']);
    const summary = [...element.querySelectorAll('.summary .value')].map((value) => value.textContent!.trim());
    expect(summary).toEqual(['5', '43.7 mi', '8h 24m', '11:33 /mi']);
  });

  it('keeps kilometres out of the URL when switching back', async () => {
    const { element } = await render('mi');
    const navigate = vi.spyOn(TestBed.inject(Router), 'navigate').mockResolvedValue(true);

    const km = [...element.querySelectorAll<HTMLButtonElement>('[aria-label="Units"] button')].find((b) => b.textContent!.trim() === 'km')!;
    km.click();

    expect(navigate).toHaveBeenCalledWith([], { queryParams: { unit: null }, queryParamsHandling: 'merge' });
  });

  it("opens a month's Runs from its row", async () => {
    const { element } = await render();
    const navigate = vi.spyOn(TestBed.inject(Router), 'navigate').mockResolvedValue(true);

    (element.querySelectorAll('.months tbody tr')[8].querySelector('td.number') as HTMLElement).click();

    expect(navigate).toHaveBeenCalledWith([], {
      queryParams: { view: null, year: null, month: '2026-09', page: null },
      queryParamsHandling: 'merge',
    });
  });
});

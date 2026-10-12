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

  async function open(url: string) {
    const harness = await RouterTestingHarness.create();
    const http = TestBed.inject(HttpTestingController);
    await harness.navigateByUrl(url);
    for (const sync of http.match('/api/sync')) sync.flush({ running: false, importedRuns: 0, lastSyncedAt: null });
    harness.detectChanges();
    const request = http.expectOne((r) => r.url === '/api/runs');
    request.flush(october);
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

  it('shows the list for an old link to the monthly totals', async () => {
    const { http, element } = await open('/runs?view=months&year=2025');

    expect(names(element)).toEqual(['Easy Sunday', 'Tempo Tuesday']);
    http.expectNone((r) => r.url === '/api/runs/months');
  });
});

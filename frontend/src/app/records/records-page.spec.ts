import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { By } from '@angular/platform-browser';
import { Router, provideRouter } from '@angular/router';
import { NgxEchartsDirective, provideEchartsCore } from 'ngx-echarts';
import { RecordDistance } from './record';
import { RecordsPage } from './records-page';

/** A 5K Personal Best improved once: 25:00 in Run 1, then 23:20 in Run 2. */
const records: RecordDistance[] = [
  {
    name: '5K',
    distance: 5000,
    shownByDefault: true,
    progression: [
      { runId: 1, runName: 'First 5K', date: '2026-03-01T09:00:00', elapsedTime: 1500, timePerKm: 300 },
      { runId: 2, runName: 'Parkrun', date: '2026-09-12T09:00:00', elapsedTime: 1400, timePerKm: 280 },
    ],
    personalBest: { runId: 2, runName: 'Parkrun', date: '2026-09-12T09:00:00', elapsedTime: 1400, timePerKm: 280 },
  },
];

describe('RecordsPage', () => {
  beforeEach(() => {
    vi.stubGlobal('ResizeObserver', class { observe() {} unobserve() {} disconnect() {} });
    TestBed.configureTestingModule({
      imports: [RecordsPage],
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        provideRouter([{ path: 'runs/:id', children: [] }]),
        // jsdom cannot draw; the tests click the chart through its event instead.
        provideEchartsCore({ echarts: { init: () => new Proxy({}, { get: () => () => undefined }) } }),
      ],
    });
  });

  afterEach(() => vi.unstubAllGlobals());

  it('opens the Run that set a Personal Best from its dot, and nothing from the point that carries it to today', async () => {
    const fixture = TestBed.createComponent(RecordsPage);
    const http = TestBed.inject(HttpTestingController);
    fixture.detectChanges();
    for (const request of http.match('/api/settings')) request.flush({ distanceUnit: 'km' });
    for (const request of http.match('/api/sync')) request.flush({ running: false, importedActivities: 0, lastSyncedAt: null });
    fixture.detectChanges();
    http.expectOne('/api/records').flush(records);
    await fixture.whenStable();
    fixture.detectChanges();

    const chart = fixture.debugElement.query(By.directive(NgxEchartsDirective));
    const router = TestBed.inject(Router);

    chart.triggerEventHandler('chartClick', { dataIndex: 2 });
    await fixture.whenStable();
    expect(router.url).toBe('/');

    chart.triggerEventHandler('chartClick', { dataIndex: 0 });
    await fixture.whenStable();
    expect(router.url).toBe('/runs/1');
  });
});

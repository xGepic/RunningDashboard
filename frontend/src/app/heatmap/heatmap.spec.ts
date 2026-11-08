import { DrawnRun, HeatmapRun, boundsOf, drawnRuns, heatmapYears, homeArea, runsIn } from './heatmap';

function run(id: number, startLocal: string, points: [number, number][] = [[48.2, 16.4]]): DrawnRun {
  return { id, name: `Run ${id}`, startLocal, distance: 10_000, route: '', points };
}

describe('drawnRuns', () => {
  it("decodes each Run's route and leaves out one without points", () => {
    const runs: HeatmapRun[] = [
      // Google's polyline example: (38.5, -120.2), (40.7, -120.95).
      { id: 1, name: 'Example', startLocal: '2026-03-01T09:00:00', distance: 10_000, route: '_p~iF~ps|U_ulLnnqC' },
      { id: 2, name: 'Empty', startLocal: '2026-03-02T09:00:00', distance: 5_000, route: '' },
    ];

    expect(drawnRuns(runs).map((r) => [r.id, r.points])).toEqual([[1, [[38.5, -120.2], [40.7, -120.95]]]]);
  });
});

describe('heatmapYears and runsIn', () => {
  const runs = [run(1, '2024-12-31T23:30:00'), run(2, '2026-01-01T00:30:00'), run(3, '2026-05-01T09:00:00')];

  it('lists the years with Runs, newest first, by local start', () => {
    expect(heatmapYears(runs)).toEqual([2026, 2024]);
  });

  it("picks a year's Runs, or all of them without one", () => {
    expect(runsIn(runs, 2026).map((r) => r.id)).toEqual([2, 3]);
    expect(runsIn(runs, null)).toBe(runs);
  });
});

describe('boundsOf', () => {
  it("boxes every point of every route, and is none without Runs", () => {
    const runs = [run(1, '2026-01-01', [[48.2, 16.4], [48.25, 16.3]]), run(2, '2026-01-02', [[48.1, 16.5]])];

    expect(boundsOf(runs)).toEqual([[48.1, 16.3], [48.25, 16.5]]);
    expect(boundsOf([])).toBeNull();
  });
});

describe('homeArea', () => {
  it('boxes the Runs that start near where the most Runs start, leaving out one far away', () => {
    const vienna = [
      run(1, '2026-01-01', [[48.2, 16.4], [48.22, 16.42]]),
      run(2, '2026-01-02', [[48.21, 16.37], [48.18, 16.39]]),
      run(3, '2026-01-03', [[48.19, 16.41]]),
    ];
    const lisbon = run(4, '2026-08-01', [[38.7, -9.1], [38.72, -9.14]]);

    expect(homeArea([...vienna, lisbon])).toEqual([[48.18, 16.37], [48.22, 16.42]]);
  });

  it('picks the place with more Runs over one about 30 km away, not the land between', () => {
    // Vienna, and Tulln about 30 km west of it.
    const vienna = [run(1, '2026-01-01', [[48.2, 16.4]]), run(2, '2026-01-02', [[48.21, 16.38]]), run(3, '2026-01-03', [[48.19, 16.41]])];
    const tulln = [run(4, '2026-02-01', [[48.33, 16.05]]), run(5, '2026-02-02', [[48.34, 16.06]])];

    expect(homeArea([...tulln, ...vienna])).toEqual([[48.19, 16.38], [48.21, 16.41]]);
  });

  it('is none without Runs', () => {
    expect(homeArea([])).toBeNull();
  });
});

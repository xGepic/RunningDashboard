import { ChartColors } from './chart-theme';
import { WeekDistance, distanceJumpNote, weeklyDistanceChart } from './charts';

const colors = { series: '#4a9eff', seriesDeep: '#1f6fd1', muted: '#7c889b', faint: '#5a6577', warning: '#f2c46d' } as unknown as ChartColors;

interface ChartOptions {
  series: { data: unknown[] }[];
  tooltip: { formatter: (params: { dataIndex: number }[]) => string };
}

describe('weeklyDistanceChart', () => {
  const weeks: WeekDistance[] = [
    { start: '2026-07-20', distance: 50000, distanceJump: null },
    { start: '2026-07-27', distance: 60000, distanceJump: { comparedWith: '2026-07-20', increase: 0.19999999999999996 } },
  ];

  it('marks Distance Jump bars in the warning colour', () => {
    const options = weeklyDistanceChart(weeks, colors) as unknown as ChartOptions;

    expect(options.series[0].data).toEqual([50, { value: 60, itemStyle: { color: '#f2c46d' } }]);
  });

  it("says in a Distance Jump's tooltip by how much the week went up and which week it was compared with", () => {
    const options = weeklyDistanceChart(weeks, colors) as unknown as ChartOptions;

    expect(options.tooltip.formatter([{ dataIndex: 1 }])).toContain('Distance Jump: +20% on the week of 20 Jul 2026');
    expect(options.tooltip.formatter([{ dataIndex: 0 }])).not.toContain('Distance Jump');
  });

  it('takes weeks that say nothing about Distance Jumps', () => {
    const options = weeklyDistanceChart([{ start: '2026-07-20', distance: 50000 }], colors) as unknown as ChartOptions;

    expect(options.series[0].data).toEqual([50]);
  });
});

describe('distanceJumpNote', () => {
  it('gives the increase to a tenth of a percent at most', () => {
    expect(distanceJumpNote({ comparedWith: '2026-09-07', increase: 0.125 })).toBe('Distance Jump: +12.5% on the week of 7 Sep 2026');
    expect(distanceJumpNote({ comparedWith: '2026-09-07', increase: 2 / 3 })).toBe('Distance Jump: +66.7% on the week of 7 Sep 2026');
  });

  it('never reads +10% for a week just over the limit', () => {
    expect(distanceJumpNote({ comparedWith: '2026-09-07', increase: 0.1004 })).toBe('Distance Jump: +10.1% on the week of 7 Sep 2026');
  });

  it('is empty for a week that is not a Distance Jump', () => {
    expect(distanceJumpNote(null)).toBe('');
  });
});

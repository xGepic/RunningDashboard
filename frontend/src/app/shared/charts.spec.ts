import { ChartColors } from './chart-theme';
import { TrainingTimeWeek, paceAxis, trainingTimeChart, trainingTimeLabel } from './charts';

describe('paceAxis', () => {
  it('ticks every 10 seconds around close Paces', () => {
    // 6:39 to 7:08 /km
    expect(paceAxis([399, 428], 'km')).toEqual({ min: 390, max: 430, interval: 10 });
  });

  it('takes a coarser round step when the Paces spread wider', () => {
    // 5:19 to 7:08 /km
    expect(paceAxis([319, 428], 'km')).toEqual({ min: 300, max: 450, interval: 30 });
  });

  it('ticks on round Paces per mile', () => {
    const axis = paceAxis([399, 428], 'mi')!;
    const perMile = 1.609344;
    // 10:42 to 11:29 /mi
    expect(Math.round(axis.min * perMile)).toBe(640);
    expect(Math.round(axis.max * perMile)).toBe(690);
    expect(Math.round(axis.interval * perMile)).toBe(10);
  });

  it('gives a single Pace a step either side', () => {
    expect(paceAxis([300], 'km')).toEqual({ min: 295, max: 305, interval: 5 });
  });

  it('leaves the axis to ECharts without a Pace', () => {
    expect(paceAxis([], 'km')).toBeNull();
  });
});

describe('trainingTimeChart', () => {
  const colors = { series: '#4a9eff', ride: '#d95926', swim: '#199e70', surface: '#141b27' } as ChartColors;
  const weeks: TrainingTimeWeek[] = [
    { start: '2026-09-28', runs: 3000, rides: 5100, swims: 2400 },
    { start: '2026-10-05', runs: 2100, rides: 900, swims: 0 },
    { start: '2026-10-12', runs: 0, rides: 0, swims: 0 },
  ];
  type Series = { name: string; stack: string; data: { value: number; itemStyle: { borderRadius: number | number[] } }[] };
  const series = () => (trainingTimeChart(weeks, colors)['series'] as Series[]);

  it('stacks Runs, Rides and Swims from the bottom, in hours', () => {
    expect(series().map((s) => s.name)).toEqual(['Runs', 'Rides', 'Swims']);
    expect(series().every((s) => s.stack === 'time')).toBe(true);
    expect(series().map((s) => s.data[0].value)).toEqual([0.83, 1.42, 0.67]);
  });

  it('rounds only the top segment of a column', () => {
    const radii = (week: number) => series().map((s) => s.data[week].itemStyle.borderRadius);
    expect(radii(0)).toEqual([0, 0, [3, 3, 0, 0]]);
    expect(radii(1)).toEqual([0, [3, 3, 0, 0], 0]);
    expect(radii(2)).toEqual([0, 0, 0]);
  });
});

describe('trainingTimeLabel', () => {
  it('shows hours and minutes, and a dash for nothing', () => {
    expect(trainingTimeLabel(3960)).toBe('1h 06m');
    expect(trainingTimeLabel(0)).toBe('–');
  });
});

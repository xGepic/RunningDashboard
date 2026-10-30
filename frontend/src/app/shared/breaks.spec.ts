import { Break, breakDays, breakLabel, breakOn, breaksInWeek, breaksWithin } from './breaks';
import { ChartColors } from './chart-theme';
import { breakBands, breakTooltip } from './charts';

const injury: Break = { id: 1, kind: 'injury', start: '2026-09-10', end: '2026-09-16', note: 'Calf strain' };
const holiday: Break = { id: 2, kind: 'holiday', start: '2026-10-01', end: '2026-10-01', note: null };
const breaks = [injury, holiday];

describe('Breaks', () => {
  it('names a Break by its kind and note', () => {
    expect(breakLabel(injury)).toBe('Injury · Calf strain');
    expect(breakLabel(holiday)).toBe('Holiday');
  });

  it('counts its days, the first and last included', () => {
    expect(breakDays(injury)).toBe(7);
    expect(breakDays(holiday)).toBe(1);
  });

  it('finds the Breaks with a day in a stretch of days, its ends included', () => {
    expect(breaksWithin(breaks, '2026-09-01', '2026-09-10')).toEqual([injury]);
    expect(breaksWithin(breaks, '2026-09-17', '2026-09-30')).toEqual([]);
    expect(breaksWithin(breaks, '2026-09-16', '2026-10-01')).toEqual([injury, holiday]);
  });

  it('finds the Breaks with a day in a Training Week', () => {
    expect(breaksInWeek(breaks, '2026-09-07')).toEqual([injury]);
    expect(breaksInWeek(breaks, '2026-09-14')).toEqual([injury]);
    expect(breaksInWeek(breaks, '2026-09-21')).toEqual([]);
    expect(breaksInWeek(breaks, '2026-09-28')).toEqual([holiday]);
  });

  it('finds the Break of a day', () => {
    expect(breakOn(breaks, '2026-09-12')).toBe(injury);
    expect(breakOn(breaks, '2026-09-17')).toBeNull();
  });
});

describe('Break bands in the weekly charts', () => {
  const colors = { breaks: { injury: '#e8857a', illness: '#f2c46d', holiday: '#3dd0db', other: '#9aa3b2' } } as ChartColors;
  const weeks = ['2026-08-31', '2026-09-07', '2026-09-14', '2026-09-21', '2026-09-28'].map((start) => ({ start }));

  it('shades each Break over the Training Weeks it has days in, named by its kind', () => {
    const { markArea } = breakBands(weeks, breaks, colors) as { markArea: { data: Record<string, unknown>[][] } };

    expect(markArea.data.map(([from, to]) => [from['xAxis'], to['xAxis'], from['name']])).toEqual([
      [1, 2, 'Injury'],
      [4, 4, 'Holiday'],
    ]);
  });

  it('shades nothing without a Break among the weeks', () => {
    expect(breakBands(weeks.slice(0, 1), breaks, colors)).toEqual({});
  });

  it('names a week’s Breaks in its tooltip, made safe for HTML', () => {
    expect(breakTooltip('2026-09-14', [{ ...injury, note: '<calf>' }], colors)).toContain('Injury · &#60;calf&#62;');
    expect(breakTooltip('2026-09-21', breaks, colors)).toBe('');
  });
});

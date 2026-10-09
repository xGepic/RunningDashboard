import { formatDistance, formatDuration, formatPace } from './format';

describe('formatDistance', () => {
  it('shows metres as kilometres with two decimals', () => {
    expect(formatDistance(5081.2)).toBe('5.08 km');
    expect(formatDistance(42582.8)).toBe('42.58 km');
  });
});

describe('formatDuration', () => {
  it('shows minutes and seconds under an hour', () => {
    expect(formatDuration(2096)).toBe('34:56');
    expect(formatDuration(65)).toBe('1:05');
  });

  it('shows hours from one hour on', () => {
    expect(formatDuration(18043)).toBe('5:00:43');
    expect(formatDuration(3600)).toBe('1:00:00');
  });
});

describe('formatPace', () => {
  it('shows seconds per kilometre as min:ss /km', () => {
    expect(formatPace(412.5)).toBe('6:53 /km');
    expect(formatPace(426)).toBe('7:06 /km');
  });

  it('never shows 60 seconds when rounding up to the next minute', () => {
    expect(formatPace(359.6)).toBe('6:00 /km');
  });

  it('shows a dash for a Run without a Pace', () => {
    expect(formatPace(null)).toBe('–');
  });
});

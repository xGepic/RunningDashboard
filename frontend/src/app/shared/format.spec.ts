import { formatDifference, formatDistance, formatDuration, formatHours, formatPace, formatTotalDistance, parseDuration } from './format';

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

describe('formatTotalDistance', () => {
  it('shows whole kilometres with thousands separators', () => {
    expect(formatTotalDistance(4_321_456)).toBe('4,321 km');
    expect(formatTotalDistance(42_582.8)).toBe('43 km');
  });
});

describe('formatHours', () => {
  it('shows whole hours for long totals', () => {
    expect(formatHours(1_843_200)).toBe('512 h');
    expect(formatHours(5_399)).toBe('1 h');
  });
});

describe('parseDuration', () => {
  it('reads h:mm:ss and m:ss as typed for a Target Time', () => {
    expect(parseDuration('5:00:00')).toBe(18000);
    expect(parseDuration(' 3:59:30 ')).toBe(14370);
    expect(parseDuration('24:30')).toBe(1470);
  });

  it('gives nothing for text that is not a duration', () => {
    expect(parseDuration('')).toBeNull();
    expect(parseDuration('5h')).toBeNull();
    expect(parseDuration('4:75:00')).toBeNull();
    expect(parseDuration('1:2:3:4')).toBeNull();
  });
});

describe('formatDifference', () => {
  it('says how far over or under a Target Time a result was', () => {
    expect(formatDifference(11)).toBe('0:11 over');
    expect(formatDifference(-280)).toBe('4:40 under');
    expect(formatDifference(-3725)).toBe('1:02:05 under');
  });

  it('says when a result hit the Target Time exactly', () => {
    expect(formatDifference(0)).toBe('on target');
  });
});

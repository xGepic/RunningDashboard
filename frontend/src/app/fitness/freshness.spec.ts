import { FreshnessDay, dayLabel, daysIn, formatLoad, freshnessPhase, freshnessWeeks, hardestDays, racesIn } from './freshness';

/** One day per date from `start`, with the figures `of` gives for its index. */
function daysFrom(start: string, count: number, of: (i: number) => Partial<FreshnessDay> = (i) => ({ fitness: i, fatigue: 2 * i, form: -i })): FreshnessDay[] {
  const [year, month, day] = start.split('-').map(Number);
  return Array.from({ length: count }, (_, i) => {
    const date = new Date(year, month - 1, day + i);
    const iso = `${date.getFullYear()}-${String(date.getMonth() + 1).padStart(2, '0')}-${String(date.getDate()).padStart(2, '0')}`;
    return { date: iso, load: 10, fitness: 0, fatigue: 0, form: 0, runId: i, runName: `Run ${i}`, ...of(i) };
  });
}

describe('daysIn', () => {
  const days = daysFrom('2025-01-01', 500); // up to 2026-05-15

  it('keeps the last months of the range, up to the last day', () => {
    const shown = daysIn(days, '3m');

    expect(shown[0].date).toBe('2026-02-16');
    expect(shown[shown.length - 1].date).toBe('2026-05-15');
    expect(daysIn(days, '1y')[0].date).toBe('2025-05-16');
  });

  it('keeps every day for All', () => {
    expect(daysIn(days, 'all').length).toBe(500);
  });
});

describe('freshnessPhase', () => {
  /** Sixty days to Mon 2026-06-29 with Fitness at 40, and Fatigue at `fatigue` on the last. */
  const history = (fatigue: number, fitness = 40) => daysFrom('2026-05-01', 60, (i) => ({ fitness, fatigue: i === 59 ? fatigue : 40 }));

  it('reads where the Athlete is from Fatigue against Fitness', () => {
    expect(freshnessPhase(history(70), [])).toBe(
      "You're ramping up fast: Fatigue is well above Fitness. Big jumps like this are when injuries happen, so take it easier for a few days.",
    );
    expect(freshnessPhase(history(50), [])).toBe("You're building: training harder than your recent average, so your Fitness is climbing.");
    expect(freshnessPhase(history(36), [])).toBe("You're holding steady: training about as much as your recent average.");
    expect(freshnessPhase(history(20), [])).toBe("You're recovering: training less than your recent average, so your Fitness is dropping.");
  });

  it('says whether the Athlete is tapering for a Goal Race in the next three weeks', () => {
    const race = (date: string) => [{ date: '2026-05-10', name: 'Past 10K' }, { date, name: 'Summer Half' }];

    expect(freshnessPhase(history(20), race('2026-07-11'))).toBe("You're tapering for Summer Half in 12 days: Fatigue is below Fitness, so you'll arrive fresh.");
    expect(freshnessPhase(history(50), race('2026-07-11'))).toBe(
      "Summer Half is in 12 days and you're still training harder than your recent average; easing off now lets you arrive fresh.",
    );
    expect(freshnessPhase(history(20), race('2026-06-30'))).toBe("You're tapering for Summer Half tomorrow: Fatigue is below Fitness, so you'll arrive fresh.");
    expect(freshnessPhase(history(20), race('2026-06-29'))).toContain('Summer Half today');
    // More than three weeks ahead: not yet.
    expect(freshnessPhase(history(20), race('2026-07-21'))).toContain("You're recovering");
  });

  it('says when it is too early to tell, or Fitness has faded away', () => {
    expect(freshnessPhase(history(20).slice(0, 41), [])).toBe(
      'It takes about six weeks of Runs before Fitness and Fatigue settle, so this is only a first look.',
    );
    expect(freshnessPhase(history(0, 0.4), [])).toBe("You haven't trained for a while, so your Fitness is close to zero.");
    expect(freshnessPhase([], [])).toBeNull();
  });
});

describe('hardestDays', () => {
  it('names up to three days of at least twice the typical load, hardest first and apart from each other', () => {
    // A typical day with a Run has 50; days 20, 22 and 60 are hard, day 90 harder, day 100 only a little harder.
    const loads: Record<number, number> = { 20: 300, 22: 280, 60: 150, 90: 480, 100: 90 };
    const days = daysFrom('2026-05-01', 120, (i) => ({ load: loads[i] ?? (i % 2 ? 0 : 50) }));

    // Day 22 is too close to day 20 to be named as well.
    expect(hardestDays(days).map((d) => [d.date, d.load])).toEqual([
      ['2026-07-30', 480],
      ['2026-05-21', 300],
      ['2026-06-30', 150],
    ]);
  });

  it('names none without Runs or without a day that stands out', () => {
    expect(hardestDays(daysFrom('2026-05-01', 30, () => ({ load: 0 })))).toEqual([]);
    expect(hardestDays(daysFrom('2026-05-01', 30, () => ({ load: 50 })))).toEqual([]);
  });
});

describe('freshnessWeeks', () => {
  it("sums each Training Week's load and takes the figures of its last day, newest first", () => {
    // Thu 2026-01-01 to Tue 2026-01-13: a partial week, a full one, and a partial one.
    const weeks = freshnessWeeks(daysFrom('2026-01-01', 13));

    expect(weeks.map((w) => [w.start, w.load, w.fitness, w.form])).toEqual([
      ['2026-01-12', 20, 12, -12],
      ['2026-01-05', 70, 10, -10],
      ['2025-12-29', 40, 3, -3],
    ]);
  });
});

describe('racesIn', () => {
  it('keeps the Goal Races within the days shown', () => {
    const days = daysFrom('2026-03-01', 31);
    const races = [
      { date: '2026-02-28', name: 'Before' },
      { date: '2026-03-15', name: 'Within' },
      { date: '2026-04-12', name: 'Ahead' },
    ];

    expect(racesIn(races, days).map((r) => r.name)).toEqual(['Within']);
    expect(racesIn(races, [])).toEqual([]);
  });
});

describe('formatLoad and dayLabel', () => {
  it('rounds to whole numbers with a true minus sign, and a plus when signed', () => {
    expect(formatLoad(52.4)).toBe('52');
    expect(formatLoad(-4.6)).toBe('−5');
    expect(formatLoad(3.2, true)).toBe('+3');
    expect(formatLoad(0, true)).toBe('0');
  });

  it('names the year only when it is not the current one', () => {
    expect(dayLabel('2026-09-27', '2026-11-08')).toBe('27 Sep');
    expect(dayLabel('2024-10-30', '2026-11-08')).toBe('30 Oct 2024');
  });
});

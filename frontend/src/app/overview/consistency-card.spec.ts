import { TestBed } from '@angular/core/testing';
import { ConsistencyCard } from './consistency-card';
import { DayDistance, Streak } from './overview';

/** On Fri 9 Oct 2026. 2026 starts on a Thursday, so its heatmap runs from Mon 29 Dec 2025 to Sun 3 Jan 2027. */
describe('ConsistencyCard', () => {
  beforeEach(() => {
    vi.useFakeTimers({ toFake: ['Date'] });
    vi.setSystemTime(new Date(2026, 9, 9, 18));
  });

  afterEach(() => vi.useRealTimers());

  const days: DayDistance[] = [
    { date: '2024-06-02', distance: 8000 },
    { date: '2025-12-31', distance: 5000 },
    { date: '2026-01-01', distance: 10000 },
    { date: '2026-09-15', distance: 13000 },
    { date: '2026-09-20', distance: 20000 },
  ];

  const streak: Streak = { current: 3, longest: { weeks: 6, first: '2025-03-03', last: '2025-04-07' } };

  async function render(given: DayDistance[], givenStreak: Streak = streak) {
    TestBed.configureTestingModule({ imports: [ConsistencyCard] });
    const fixture = TestBed.createComponent(ConsistencyCard);
    fixture.componentRef.setInput('days', given);
    fixture.componentRef.setInput('streak', givenStreak);
    await fixture.whenStable();
    const element = fixture.nativeElement as HTMLElement;
    const cell = (date: string) => element.querySelector(`.cell[data-date="${date}"]`) as HTMLElement | null;
    const years = () => [...element.querySelectorAll('.segmented button')].map((b) => b.textContent!.trim());
    const pressed = () => element.querySelector('.segmented button[aria-pressed="true"]')?.textContent!.trim();
    const pick = async (year: string) => {
      ([...element.querySelectorAll('.segmented button')].find((b) => b.textContent!.trim() === year) as HTMLElement).click();
      await fixture.whenStable();
    };
    /** Each stat as "label: value". */
    const stats = () =>
      [...element.querySelectorAll('.stat')].map(
        (stat) => `${stat.querySelector('.label')!.textContent!.trim()}: ${stat.querySelector('.value')!.textContent!.replace(/\s+/g, ' ').trim()}`,
      );
    return { element, cell, years, pressed, pick, stats };
  }

  it('starts at the current year, a column per Training Week and a row per weekday', async () => {
    const { element, cell, years, pressed } = await render(days);

    expect(years()).toEqual(['2026', '2025', '2024']);
    expect(pressed()).toBe('2026');
    // 53 weeks of 7 days, the days of 2025 and 2027 among them left blank.
    expect(element.querySelectorAll('.cells .cell').length).toBe(53 * 7);
    expect(element.querySelectorAll('.cells .cell[data-date]').length).toBe(365);
    expect(cell('2025-12-31')).toBeNull();
    expect(cell('2026-01-01')?.style.gridRow).toBe('4');
  });

  it("shades each day relative to the year's longest, with its date and distance on hover", async () => {
    const { cell } = await render(days);

    expect(cell('2026-09-20')?.dataset['level']).toBe('4');
    expect(cell('2026-09-15')?.dataset['level']).toBe('3');
    expect(cell('2026-01-01')?.dataset['level']).toBe('2');
    expect(cell('2026-01-02')?.dataset['level']).toBe('0');
    expect(cell('2026-09-15')?.title).toBe('Tue 15 Sep 2026: 13.00 km');
    expect(cell('2026-01-02')?.title).toBe('Fri 2 Jan 2026: no Run');
    // Not run yet, rather than without a Run.
    expect(cell('2026-10-10')?.title).toBe('Sat 10 Oct 2026');
  });

  it('shows another year when picked, shaded against that year', async () => {
    const { cell, pick, pressed } = await render(days);

    await pick('2025');

    expect(pressed()).toBe('2025');
    expect(cell('2025-12-31')?.dataset['level']).toBe('4');
    expect(cell('2026-09-20')).toBeNull();
  });

  it('shows the current and the longest Streak, from its first Monday to its last Sunday', async () => {
    const { stats } = await render(days);

    expect(stats()).toEqual(['Current Streak: 3 weeks', 'Longest Streak: 6 weeks · 3 Mar – 13 Apr 2025']);
  });

  it('ends the longest Streak today while it is the current one', async () => {
    const { stats } = await render(days, { current: 3, longest: { weeks: 3, first: '2026-09-21', last: '2026-10-05' } });

    expect(stats()[1]).toBe('Longest Streak: 3 weeks · 21 Sep – 9 Oct 2026');
  });

  it('keeps the Streaks when another year is picked', async () => {
    const { stats, pick } = await render(days);
    const before = stats();

    await pick('2024');

    expect(stats()).toEqual(before);
  });

  it('shows an empty current year and no Streak without Runs', async () => {
    const { element, years, stats } = await render([], { current: 0, longest: null });

    expect(stats()).toEqual(['Current Streak: 0 weeks', 'Longest Streak: –']);

    expect(years()).toEqual(['2026']);
    expect(element.querySelectorAll('.cells .cell[data-date]').length).toBe(365);
    expect(element.querySelectorAll('.cells .cell[data-level="0"]').length).toBe(365);
  });
});

import { TestBed } from '@angular/core/testing';
import { Break } from '../shared/breaks';
import { Streak } from './overview';
import { StreakCard } from './streak-card';

/** Friday 9 Oct 2026, in the Training Week starting Monday 5 Oct. */
describe('StreakCard', () => {
  beforeEach(() => {
    vi.useFakeTimers({ toFake: ['Date'] });
    vi.setSystemTime(new Date(2026, 9, 9, 18));
  });

  afterEach(() => vi.useRealTimers());

  const longest = { weeks: 23, start: '2025-01-06', end: '2025-06-15' };

  const streakOf = (streak: Partial<Streak>): Streak => ({ current: 14, start: '2026-06-29', thisWeekHasRun: false, longest, recentWeeks: [], ...streak });

  async function render(streak: Partial<Streak>, breaks: Break[] = []) {
    TestBed.configureTestingModule({ imports: [StreakCard] });
    const fixture = TestBed.createComponent(StreakCard);
    fixture.componentRef.setInput('streak', streakOf(streak));
    fixture.componentRef.setInput('breaks', breaks);
    await fixture.whenStable();
    const element = fixture.nativeElement as HTMLElement;
    const textOf = (selector: string) => element.querySelector(selector)?.textContent!.replace(/\s+/g, ' ').trim();
    return { fixture, element, textOf };
  }

  it('counts the weeks and says how many days are left to run this week', async () => {
    const { textOf } = await render({ current: 14, thisWeekHasRun: false });

    expect(textOf('.count .value')).toBe('14');
    expect(textOf('.count .unit')).toBe('weeks');
    // Friday, Saturday and Sunday.
    expect(textOf('.status')).toBe('This week still needs a Run · 3 days left');
  });

  it('says since when the current Streak runs, with the year only for another one', async () => {
    expect((await render({ current: 15, start: '2026-06-29' })).textOf('.count .since')).toBe('since 29 Jun');
    TestBed.resetTestingModule();
    expect((await render({ current: 50, start: '2025-10-20' })).textOf('.count .since')).toBe('since 20 Oct 2025');
  });

  it('gives no start for a Streak of none', async () => {
    const { textOf } = await render({ current: 0, start: null });

    expect(textOf('.count .since')).toBeUndefined();
  });

  it('counts all seven days on Monday', async () => {
    vi.setSystemTime(new Date(2026, 9, 5, 7));

    const { textOf } = await render({ current: 14, thisWeekHasRun: false });

    expect(textOf('.status')).toBe('This week still needs a Run · 7 days left');
  });

  it('counts the days left again when the Streak is reloaded on another day', async () => {
    const { fixture, textOf } = await render({ current: 14, thisWeekHasRun: false });

    vi.setSystemTime(new Date(2026, 9, 10, 8));
    fixture.componentRef.setInput('streak', streakOf({ current: 14, thisWeekHasRun: false }));
    await fixture.whenStable();

    expect(textOf('.status')).toBe('This week still needs a Run · 2 days left');
  });

  it('warns on Sunday that it is the last day to keep the streak', async () => {
    vi.setSystemTime(new Date(2026, 9, 11, 9));

    const { element, textOf } = await render({ current: 14, thisWeekHasRun: false });

    expect(textOf('.status')).toBe('Last day to keep the Streak');
    expect(element.querySelector('.status')!.classList).toContain('warning');
  });

  it('ticks off this week once it has a Run, even on Sunday', async () => {
    vi.setSystemTime(new Date(2026, 9, 11, 9));

    const { element, textOf } = await render({ current: 15, thisWeekHasRun: true });

    expect(textOf('.status')).toBe('This week ✓');
    expect(element.querySelector('.status')!.classList).not.toContain('warning');
  });

  it('asks for a Run this week when there is no streak', async () => {
    const { textOf } = await render({ current: 0, thisWeekHasRun: false });

    expect(textOf('.count .value')).toBe('0');
    expect(textOf('.count .unit')).toBe('weeks');
    expect(textOf('.status')).toBe('Run this week to start a new Streak');
  });

  it('says week for a streak of one', async () => {
    const { textOf } = await render({ current: 1, thisWeekHasRun: true });

    expect(textOf('.count .unit')).toBe('week');
  });

  it('names the longest streak with its weeks, Monday to Sunday', async () => {
    const { textOf } = await render({ current: 14, longest: { weeks: 23, start: '2025-01-06', end: '2025-06-15' } });

    expect(textOf('.longest')).toBe('Longest: 23 weeks · 6 Jan – 15 Jun 2025');
  });

  it('gives both years when the longest streak spans two', async () => {
    const { textOf } = await render({ current: 14, longest: { weeks: 24, start: '2024-12-30', end: '2025-06-15' } });

    expect(textOf('.longest')).toBe('Longest: 24 weeks · 30 Dec 2024 – 15 Jun 2025');
  });

  it('says when the current streak is the longest ever', async () => {
    const { textOf } = await render({ current: 23, longest: { weeks: 23, start: '2026-05-04', end: '2026-10-11' } });

    expect(textOf('.longest')).toBe('Your Longest Streak ever');
  });

  it('shows the recent weeks, with the current one outlined until it has a Run', async () => {
    const recentWeeks = [
      { start: '2026-09-14', hasRun: true },
      { start: '2026-09-21', hasRun: false },
      { start: '2026-09-28', hasRun: true },
      { start: '2026-10-05', hasRun: false },
    ];
    const { element } = await render({ current: 1, thisWeekHasRun: false, recentWeeks });

    const squares = [...element.querySelectorAll('.strip .week')];
    expect(squares.map((s) => [s.classList.contains('has-run'), s.classList.contains('pending')])).toEqual([
      [true, false],
      [false, false],
      [true, false],
      [false, true],
    ]);
    expect(squares.map((s) => s.getAttribute('title'))).toEqual([
      'Week of 14 Sep: Run',
      'Week of 21 Sep: no Run',
      'Week of 28 Sep: Run',
      'This week: no Run yet',
    ]);
    expect(element.querySelector('.strip')!.getAttribute('aria-label')).toBe('The last 4 Training Weeks: 2 with a Run');
  });

  it('fills the current week once it has a Run', async () => {
    const { element } = await render({ current: 1, thisWeekHasRun: true, recentWeeks: [{ start: '2026-10-05', hasRun: true }] });

    const square = element.querySelector('.strip .week')!;
    expect(square.classList).toContain('has-run');
    expect(square.classList).not.toContain('pending');
    expect(square.getAttribute('title')).toBe('This week: Run');
  });

  describe('with Breaks', () => {
    const injury: Break = { id: 1, kind: 'injury', start: '2026-09-20', end: '2026-10-02', note: 'Calf strain' };
    const holiday: Break = { id: 2, kind: 'holiday', start: '2026-10-07', end: '2026-10-16', note: null };

    it('names the Break of a week without a Run, counting the Streak as without it', async () => {
      const recentWeeks = [
        { start: '2026-09-14', hasRun: true },
        { start: '2026-09-21', hasRun: false },
        { start: '2026-09-28', hasRun: true },
        { start: '2026-10-05', hasRun: true },
      ];
      const { element, textOf } = await render({ current: 2, thisWeekHasRun: true, recentWeeks }, [injury]);

      const squares = [...element.querySelectorAll('.strip .week')];
      expect(squares.map((s) => s.classList.contains('on-break'))).toEqual([false, true, false, false]);
      expect(squares[1].classList).toContain('break-injury');
      expect(squares[1].getAttribute('title')).toBe('Week of 21 Sep: no Run · Injury · Calf strain');
      // A week with a Run is a Run week, Break or not.
      expect(squares[2].getAttribute('title')).toBe('Week of 28 Sep: Run');
      expect(element.querySelector('.strip')!.getAttribute('aria-label')).toBe('The last 4 Training Weeks: 3 with a Run, 1 without one in a Break');
      expect(textOf('.count .value')).toBe('2');
    });

    it('says this week is a Break instead of what it still needs', async () => {
      vi.setSystemTime(new Date(2026, 9, 11, 9));

      const { textOf, element } = await render({ current: 14, thisWeekHasRun: false, recentWeeks: [{ start: '2026-10-05', hasRun: false }] }, [holiday]);

      expect(textOf('.status')).toBe('Break this week: Holiday');
      expect(element.querySelector('.status')!.classList).not.toContain('warning');
      expect(element.querySelector('.strip .week')!.classList).toContain('pending');
    });

    it('says the Streak ended in a Break', async () => {
      const recentWeeks = [
        { start: '2026-09-21', hasRun: false },
        { start: '2026-09-28', hasRun: false },
        { start: '2026-10-05', hasRun: false },
      ];

      const { textOf } = await render({ current: 0, thisWeekHasRun: false, recentWeeks }, [injury]);

      expect(textOf('.status')).toBe('The Streak ended in a Break (Injury) · Run this week to start a new one');
    });
  });
});

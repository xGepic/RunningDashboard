import { TestBed } from '@angular/core/testing';
import { ConsistencyCard } from './consistency-card';
import { DayDistance } from './overview';

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

  async function render(given: DayDistance[]) {
    TestBed.configureTestingModule({ imports: [ConsistencyCard] });
    const fixture = TestBed.createComponent(ConsistencyCard);
    fixture.componentRef.setInput('days', given);
    await fixture.whenStable();
    const element = fixture.nativeElement as HTMLElement;
    const cell = (date: string) => element.querySelector(`.cell[data-date="${date}"]`) as HTMLElement | null;
    const years = () => [...element.querySelectorAll('.segmented button')].map((b) => b.textContent!.trim());
    const pressed = () => element.querySelector('.segmented button[aria-pressed="true"]')?.textContent!.trim();
    const pick = async (year: string) => {
      ([...element.querySelectorAll('.segmented button')].find((b) => b.textContent!.trim() === year) as HTMLElement).click();
      await fixture.whenStable();
    };
    return { element, cell, years, pressed, pick };
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

  it('shows an empty current year without Runs', async () => {
    const { element, years } = await render([]);

    expect(years()).toEqual(['2026']);
    expect(element.querySelectorAll('.cells .cell[data-date]').length).toBe(365);
    expect(element.querySelectorAll('.cells .cell[data-level="0"]').length).toBe(365);
  });
});

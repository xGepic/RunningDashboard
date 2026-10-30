import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { TrainingTimeWeek } from '../shared/charts';
import { WeeklyTimeGoal } from './overview';
import { WeeklyTimeGoalCard, parseHours } from './weekly-time-goal-card';

/** Friday 9 Oct 2026, in the Training Week from Monday 5 Oct: Friday, Saturday and Sunday are left. */
describe('WeeklyTimeGoalCard', () => {
  beforeEach(() => {
    vi.useFakeTimers({ toFake: ['Date'] });
    vi.setSystemTime(new Date(2026, 9, 9, 18));
  });

  afterEach(() => vi.useRealTimers());

  /** 1h 30m of Runs, 2h of Rides and 30m of Swims: 4 hours. */
  const thisWeek: TrainingTimeWeek = { start: '2026-10-05', runs: 5400, rides: 7200, swims: 1800 };

  async function render(goal: WeeklyTimeGoal | null, week: TrainingTimeWeek = thisWeek) {
    TestBed.configureTestingModule({ imports: [WeeklyTimeGoalCard], providers: [provideHttpClient(), provideHttpClientTesting()] });
    const fixture = TestBed.createComponent(WeeklyTimeGoalCard);
    fixture.componentRef.setInput('goal', goal);
    fixture.componentRef.setInput('thisWeek', week);
    await fixture.whenStable();
    const element = fixture.nativeElement as HTMLElement;
    const stats = () =>
      [...element.querySelectorAll('.stat')].map((stat) => `${stat.querySelector('.label')!.textContent!.trim()}: ${stat.querySelector('.value')!.textContent!.trim()}`);
    const click = (text: string) => {
      [...element.querySelectorAll<HTMLButtonElement>('button')].find((b) => b.textContent!.trim() === text)!.click();
      fixture.detectChanges();
    };
    const type = (text: string) => {
      const input = element.querySelector<HTMLInputElement>('input')!;
      input.value = text;
      input.dispatchEvent(new Event('input'));
      element.querySelector('form')!.dispatchEvent(new Event('submit'));
      fixture.detectChanges();
    };
    return { fixture, element, stats, click, type, http: TestBed.inject(HttpTestingController) };
  }

  it("adds up the week's Runs, Rides and Swims against the goal, with what is left for the days to come", async () => {
    const { element, stats } = await render({ time: 7 * 3600 });

    expect(element.querySelector('.done .value')!.textContent).toBe('4h 00m');
    expect(element.querySelector('.done .of')!.textContent).toBe('/ 7h');
    expect(element.querySelector('.percent')!.textContent!.trim()).toBe('57 %');
    // 3 hours over Friday, Saturday and Sunday.
    expect(stats()).toEqual(['To go: 3h 00m', 'Days left: 3', 'Needed / day: 1h 00m']);
  });

  it('shows each sport as its part of the bar', async () => {
    const { element } = await render({ time: 8 * 3600 });

    const parts = [...element.querySelectorAll<HTMLElement>('.bar span')];
    expect(parts.map((part) => [part.className, part.style.width])).toEqual([
      ['runs', '18.75%'],
      ['rides', '25%'],
      ['swims', '6.25%'],
    ]);
  });

  it('says when the goal is reached, the bar full', async () => {
    const { element, stats } = await render({ time: 3 * 3600 + 1800 });

    expect(stats()).toEqual(['Status: Goal reached']);
    // 1h 30m of the 3h 30m goal, then the Rides fill the rest; nothing is left for the Swims.
    const widths = [...element.querySelectorAll<HTMLElement>('.bar span')].map((part) => parseFloat(part.style.width));
    expect(widths[0]).toBeCloseTo(42.857, 2);
    expect(widths[1]).toBeCloseTo(57.143, 2);
    expect(widths[2]).toBeCloseTo(0, 6);
  });

  it('offers to set a goal without one', async () => {
    const { element } = await render(null);

    expect(element.querySelector('.none')!.textContent).toContain('No weekly time goal yet.');
  });

  it('sets the goal in hours', async () => {
    const page = await render(null);
    const changed = vi.fn();
    page.fixture.componentInstance.changed.subscribe(changed);

    page.click('Set a goal');
    page.type('7.5');

    const request = page.http.expectOne('/api/weekly-time-goal');
    expect(request.request.method).toBe('PUT');
    expect(request.request.body).toEqual({ time: 27000 });
    request.flush({ time: 27000 });
    expect(changed).toHaveBeenCalled();
  });

  it('fills in the goal to change it', async () => {
    const page = await render({ time: 27000 });

    page.click('Edit');

    expect(page.element.querySelector('input')!.value).toBe('7.5');
  });

  it('asks again for hours it cannot read', async () => {
    const page = await render(null);
    page.click('Set a goal');

    page.type('seven');

    expect(page.element.querySelector('.field-error')!.textContent).toContain('e.g. 7.5 or 7:30');
    page.http.expectNone('/api/weekly-time-goal');
  });

  it('removes the goal once confirmed', async () => {
    vi.stubGlobal('confirm', vi.fn(() => true));
    const page = await render({ time: 27000 });

    page.click('Remove');

    expect(page.http.expectOne('/api/weekly-time-goal').request.method).toBe('DELETE');
    vi.unstubAllGlobals();
  });
});

describe('parseHours', () => {
  it('reads whole and decimal hours, with a point or a comma, and h:mm', () => {
    expect(parseHours('7')).toBe(25200);
    expect(parseHours('7.5')).toBe(27000);
    expect(parseHours(' 7,25 ')).toBe(26100);
    expect(parseHours('7:30')).toBe(27000);
  });

  it('reads nothing else, and no time that is not more than nothing', () => {
    expect(parseHours('')).toBeNull();
    expect(parseHours('0')).toBeNull();
    expect(parseHours('7h')).toBeNull();
    expect(parseHours('7:75')).toBeNull();
    expect(parseHours('-2')).toBeNull();
  });
});

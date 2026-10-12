import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { YearlyGoal } from './overview';
import { YearlyGoalCard } from './yearly-goal-card';

/** On Fri 9 Oct 2026, 281 of the year's 365 days are over. */
describe('YearlyGoalCard', () => {
  beforeEach(() => {
    vi.useFakeTimers({ toFake: ['Date'] });
    vi.setSystemTime(new Date(2026, 9, 9, 18));
  });

  afterEach(() => vi.useRealTimers());

  async function render(goal: YearlyGoal | null, distance: number) {
    TestBed.configureTestingModule({
      imports: [YearlyGoalCard],
      providers: [provideHttpClient(), provideHttpClientTesting()],
    });
    const fixture = TestBed.createComponent(YearlyGoalCard);
    fixture.componentRef.setInput('goal', goal);
    fixture.componentRef.setInput('distance', distance);
    await fixture.whenStable();
    const element = fixture.nativeElement as HTMLElement;
    const text = () => element.textContent!.replace(/\s+/g, ' ');
    /** Each stat as "label: value". */
    const stats = () =>
      [...element.querySelectorAll('.stat')].map((stat) => `${stat.querySelector('.label')!.textContent!.trim()}: ${stat.querySelector('.value')!.textContent!.trim()}`);
    return { fixture, element, text, stats, http: TestBed.inject(HttpTestingController) };
  }

  it('compares the distance so far with the goal spread evenly over the year', async () => {
    const { element, stats } = await render({ year: 2026, distance: 2_000_000 }, 1_642_000);

    expect(element.querySelector('.done .value')!.textContent).toBe('1,642');
    expect(element.querySelector('.done .of')!.textContent).toBe('/ 2,000 km');
    expect(element.querySelector('.percent')!.textContent!.trim()).toBe('82 %');
    // By the schedule, 2000 km × 281 / 365 = 1539.7 km; and 358 km to go over the last 84 days, which are 12 weeks.
    expect(stats()).toEqual(['Ahead of schedule: 102 km', 'To go: 358 km', 'Needed / week: 29.8 km']);
    expect((element.querySelector('.scheduled') as HTMLElement).style.left).toBe(`${(281 / 365) * 100}%`);
  });

  it('says how far behind the schedule the year is', async () => {
    const { stats } = await render({ year: 2026, distance: 2_000_000 }, 1_400_000);

    expect(stats()[0]).toBe('Behind schedule: 140 km');
  });

  it('says when the goal is reached', async () => {
    const { stats } = await render({ year: 2026, distance: 1_500_000 }, 1_642_000);

    expect(stats()).toEqual(['Status: Goal reached']);
  });

  it('sets a goal in kilometres', async () => {
    const { fixture, element, text, http } = await render(null, 1_642_000);
    expect(text()).toContain('No distance goal for 2026 yet.');
    let changed = false;
    fixture.componentInstance.changed.subscribe(() => (changed = true));

    (element.querySelector('.none button') as HTMLButtonElement).click();
    await fixture.whenStable();
    const input = element.querySelector('input') as HTMLInputElement;
    input.value = '2000';
    input.dispatchEvent(new Event('input'));
    (element.querySelector('form') as HTMLFormElement).dispatchEvent(new Event('submit'));

    const request = http.expectOne('/api/yearly-goal');
    expect(request.request.method).toBe('PUT');
    expect(request.request.body).toEqual({ distance: 2_000_000 });
    request.flush({ year: 2026, distance: 2_000_000 });
    expect(changed).toBe(true);
  });

  it('does not send a goal that is not a distance', async () => {
    const { fixture, element, http } = await render(null, 0);

    (element.querySelector('.none button') as HTMLButtonElement).click();
    await fixture.whenStable();
    const input = element.querySelector('input') as HTMLInputElement;
    input.value = 'lots';
    input.dispatchEvent(new Event('input'));
    (element.querySelector('form') as HTMLFormElement).dispatchEvent(new Event('submit'));
    await fixture.whenStable();

    http.expectNone('/api/yearly-goal');
    expect(element.querySelector('.field-error')?.textContent).toContain('Enter the goal in kilometres');
  });
});

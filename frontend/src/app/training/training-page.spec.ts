import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { RunType } from '../goal-races/training-plan';
import { DayState, Training, TrainingBlockWeek, WeekPlan } from './training';
import { TrainingPage } from './training-page';

/** Weeks −11 to −9 of the second marathon's planned block, on Wed 10 Mar 2027. */
const training: Training = {
  view: 'block',
  countdown: null,
  block: {
    goalRace: { id: 2, name: 'Second marathon', date: '2027-05-23', distance: 42195, targetTime: 18000 },
    targetPace: 426,
    phases: [{ type: 'peak', firstWeeksToRace: -11, lastWeeksToRace: -8 }],
    comparison: null,
    weeks: [
      week(-11, '2027-03-01', 'past', 32000, {
        distanceMet: true,
        days: ['rest', 'ran easy', 'rest', 'ran tempo', 'rest', 'rest', 'ran long'],
      }),
      week(-10, '2027-03-08', 'current', 8000, {
        days: ['rest', 'ran easy', 'rest', 'planned tempo', 'rest', 'unplanned', 'planned long'],
      }),
      week(-9, '2027-03-15', 'future', null, {
        recoveryWeek: true,
        plannedDistance: 24000,
        days: ['rest', 'planned easy', 'rest', 'rest', 'rest', 'rest', 'planned long'],
      }),
    ],
  },
};

function week(
  weeksToRace: number,
  start: string,
  status: TrainingBlockWeek['status'],
  distance: number | null,
  /** Each day as its state, followed by its Run Type on a Running Day: "ran long". */
  plan: Partial<Omit<WeekPlan, 'days'>> & { days: (DayState | `${DayState} ${RunType}`)[] },
): TrainingBlockWeek {
  const monday = new Date(start);
  const day = (i: number) => new Date(monday.getTime() + i * 86_400_000).toISOString().slice(0, 10);
  return {
    weeksToRace,
    start,
    end: day(6),
    status,
    actual: distance === null ? null : { runs: 3, distance, movingTime: 11520, relativeEffort: null, longRun: null },
    plan: {
      phase: 'peak',
      recoveryWeek: false,
      plannedDistance: 30000,
      plannedLongRun: null,
      distanceMet: null,
      longRunMet: null,
      ...plan,
      days: plan.days.map((written, i) => {
        const [state, runType] = written.split(' ') as [DayState, RunType?];
        return { date: day(i), state, runType: runType ?? null };
      }),
    },
  };
}

/** The day before the block starts, counting down to it. */
const countdown: Training = {
  view: 'countdown',
  block: null,
  countdown: { goalRace: training.block!.goalRace, trainingBlockStart: '2027-03-01', daysRemaining: 1, recentWeeks: [] },
};

describe('TrainingPage', () => {
  beforeEach(() => {
    vi.useFakeTimers({ toFake: ['Date', 'setInterval', 'clearInterval'] });
    vi.setSystemTime(new Date(2027, 2, 10, 10));
  });

  afterEach(() => vi.useRealTimers());

  async function render(answer = training) {
    TestBed.configureTestingModule({
      imports: [TrainingPage],
      providers: [provideHttpClient(), provideHttpClientTesting(), provideRouter([])],
    });
    const fixture = TestBed.createComponent(TrainingPage);
    fixture.detectChanges();
    const http = TestBed.inject(HttpTestingController);
    // The sync status comes in first; the page reloads its block on it, so answer whatever it asks last.
    http.expectOne('/api/sync').flush({ running: false, importedRuns: 0, lastSyncedAt: null });
    fixture.detectChanges();
    http.expectOne('/api/goal-races').flush([]);
    http.match('/api/training').filter((request) => !request.cancelled).forEach((request) => request.flush(answer));
    await fixture.whenStable();
    return { element: fixture.nativeElement as HTMLElement, fixture, http };
  }

  const row = (element: HTMLElement, weeksToRace: number) =>
    element.querySelector(`tbody tr[data-weeks-to-race="${weeksToRace}"]`) as HTMLTableRowElement;

  it('shows each planned week with its calendar strip', async () => {
    const { element } = await render();

    const states = (weeksToRace: number) =>
      [...row(element, weeksToRace).querySelectorAll('.strip li')].map((day) => day.className.replace('day ', ''));
    expect(states(-11)).toEqual(['rest', 'ran', 'rest', 'ran', 'rest', 'rest', 'ran']);
    expect(states(-10)).toEqual(['rest', 'ran', 'rest', 'planned', 'rest', 'unplanned', 'planned']);
    expect(row(element, -10).textContent).toContain('Saturday: run on an unplanned day');
  });

  it("labels each Running Day of the strip with its Run Type, named in full on hover", async () => {
    const { element } = await render();

    const days = [...row(element, -10).querySelectorAll('.strip li')];
    expect(days.map((day) => day.querySelector('.run-type')?.textContent ?? '')).toEqual(['', 'E', '', 'T', '', '', 'L']);
    expect(days[6].getAttribute('title')).toBe('Sun 14 Mar: Long, still to come');
    expect(days[6].textContent).toContain('Sunday: Long, still to come');
    expect(days[5].getAttribute('title')).toBe('Sat 13 Mar: run on an unplanned day');
    expect(days[0].getAttribute('title')).toBe('Mon 8 Mar: rest day');
  });

  it('marks each of its weeks with the Phase', async () => {
    const { element } = await render();

    for (const weeksToRace of [-11, -10]) {
      expect(row(element, weeksToRace).classList).toContain('phase-peak');
      expect(row(element, weeksToRace).querySelector('td.to-race')?.textContent).toContain('peak Phase');
    }
  });

  it('puts the planned distance next to the actual one, and only the plan for a future week', async () => {
    const { element } = await render();

    expect(row(element, -11).textContent).toContain('32.00 km');
    expect(row(element, -11).textContent).toContain('planned 30.00 km');
    expect(row(element, -11).querySelector('.met')?.textContent).toContain('32.00 km');
    // The current week is still running, so it is neither met nor short yet.
    expect(row(element, -10).querySelector('.met, .short')).toBeNull();
    expect(row(element, -9).textContent).toContain('planned 24.00 km');
    expect(row(element, -9).textContent).toContain('Recovery Week');
    expect(row(element, -9).classList).toContain('recovery-week');
  });

  it('counts down to race day during the block, in place of the race card', async () => {
    const { element } = await render();

    const hero = element.querySelector('.hero')!.textContent!.replace(/\s+/g, ' ');
    expect(hero).toContain('Race Day');
    expect(hero).toContain('74 days to go');
    expect(hero).toContain('Exact time until race day, Sunday 00:00');
    expect(hero).toContain('Week 2 of 12 of the Training Block');
    expect(element.querySelector('.card.race')).toBeNull();
  });

  it('switches to the race day countdown when the block starts', async () => {
    vi.setSystemTime(new Date(2027, 1, 28, 23, 59, 59));
    const { element, fixture, http } = await render(countdown);
    expect(element.querySelector('.hero')!.textContent).toContain('Next Training Block');

    vi.advanceTimersByTime(2000);
    fixture.detectChanges();
    http.expectOne('/api/training').flush(training);
    await fixture.whenStable();

    const hero = element.querySelector('.hero')!.textContent!.replace(/\s+/g, ' ');
    expect(hero).toContain('Race Day');
    expect(hero).toContain('83 days to go');
    expect(hero).toContain('Week 1 of 12 of the Training Block');
  });
});

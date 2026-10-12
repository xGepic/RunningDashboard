import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { TrainingPlan, TrainingPlanWeek, TrainingPlanWeeksRequest } from './training-plan';
import { TrainingPlanWeeksEditor } from './training-plan-weeks-editor';

/** The last three weeks of the second marathon's plan, enough for the editor's rows. */
const plan: TrainingPlan = {
  goalRaceId: 7,
  length: 16,
  phases: [],
  weeks: [
    week(-2, '2027-05-03', {
      plannedDistance: 45000,
      plannedLongRun: 18000,
      runningDays: [
        { day: 'tuesday', runType: 'easy' },
        { day: 'thursday', runType: 'tempo' },
        { day: 'sunday', runType: 'long' },
      ],
      recoveryWeek: true,
    }),
    week(-1, '2027-05-10', {}),
    week(0, '2027-05-17', { plannedDistance: 55000, plannedLongRun: 42195, runningDays: [{ day: 'sunday', runType: 'race' }] }),
  ],
};

function week(weeksToRace: number, start: string, planned: Partial<TrainingPlanWeek>): TrainingPlanWeek {
  return {
    weeksToRace,
    start,
    end: start,
    phase: 'taper',
    plannedDistance: null,
    plannedLongRun: null,
    runningDays: [],
    recoveryWeek: false,
    ...planned,
  };
}

describe('TrainingPlanWeeksEditor', () => {
  async function render() {
    TestBed.configureTestingModule({
      imports: [TrainingPlanWeeksEditor],
      providers: [provideHttpClient(), provideHttpClientTesting()],
    });
    const fixture = TestBed.createComponent(TrainingPlanWeeksEditor);
    fixture.componentRef.setInput('plan', plan);
    await fixture.whenStable();
    const element = fixture.nativeElement as HTMLElement;
    const row = (weeksToRace: number) => element.querySelector(`tr[data-weeks-to-race="${weeksToRace}"]`) as HTMLElement;
    const click = async (button: Element | null) => {
      (button as HTMLElement).click();
      await fixture.whenStable();
    };
    /** Saves the weeks and returns what the editor sent. */
    const save = async () => {
      await click(element.querySelector('button.save'));
      const request = TestBed.inject(HttpTestingController).expectOne('/api/goal-races/7/training-plan/weeks');
      expect(request.request.method).toBe('PUT');
      return request.request.body as TrainingPlanWeeksRequest;
    };
    /** Picks a Run Type, or "rest", for a day of a week. */
    const pick = async (weeksToRace: number, day: string, runType: string) => {
      const select = row(weeksToRace).querySelector(`select[aria-label="${day}"]`) as HTMLSelectElement;
      select.value = runType;
      select.dispatchEvent(new Event('change'));
      await fixture.whenStable();
    };
    return { fixture, element, row, click, save, pick };
  }

  it('copies the planned distance, Long Run and Running Days with their Run Types from the week above, but not the Recovery Week', async () => {
    const { row, click, save } = await render();

    await click(row(-1).querySelector('button.copy'));
    const sent = await save();

    expect(sent.weeks[1]).toEqual({
      weeksToRace: -1,
      plannedDistance: 45000,
      plannedLongRun: 18000,
      runningDays: [
        { day: 'tuesday', runType: 'easy' },
        { day: 'thursday', runType: 'tempo' },
        { day: 'sunday', runType: 'long' },
      ],
      recoveryWeek: false,
    });
    expect((row(-1).querySelector('input.distance') as HTMLInputElement).value).toBe('45');
    expect((row(-1).querySelector('select[aria-label="Thursday"]') as HTMLSelectElement).value).toBe('tempo');
  });

  it('offers no copy on the first week of the plan', async () => {
    const { row } = await render();

    expect(row(-2).querySelector('button.copy')).toBeNull();
  });

  it('shows each day as its Run Type or rest', async () => {
    const { row } = await render();
    const shown = (day: string) => (row(-2).querySelector(`select[aria-label="${day}"]`) as HTMLSelectElement).value;

    expect(['Monday', 'Tuesday', 'Thursday', 'Sunday'].map(shown)).toEqual(['rest', 'easy', 'tempo', 'long']);
  });

  it('offers rest and then the Run Types easy, tempo, intervals, race and long', async () => {
    const { row } = await render();
    const select = row(-2).querySelector('select[aria-label="Monday"]') as HTMLSelectElement;

    expect([...select.options].map((o) => o.value)).toEqual(['rest', 'easy', 'tempo', 'intervals', 'race', 'long']);
  });

  it('picks a Run Type or rest per day and sends the Running Days Monday first', async () => {
    const { pick, save } = await render();

    await pick(-2, 'Saturday', 'long');
    await pick(-2, 'Monday', 'intervals');
    await pick(-2, 'Thursday', 'rest');
    await pick(-2, 'Tuesday', 'race');

    expect((await save()).weeks[0].runningDays).toEqual([
      { day: 'monday', runType: 'intervals' },
      { day: 'tuesday', runType: 'race' },
      { day: 'saturday', runType: 'long' },
      { day: 'sunday', runType: 'long' },
    ]);
  });

  it('sends typed kilometres as metres, and nothing for empty fields', async () => {
    const { fixture, row, save } = await render();
    const distance = row(-1).querySelector('input.distance') as HTMLInputElement;

    distance.value = '32,5';
    distance.dispatchEvent(new Event('input'));
    await fixture.whenStable();

    expect((await save()).weeks[1]).toMatchObject({ plannedDistance: 32500, plannedLongRun: null });
  });

  it('does not let the Race Week be a Recovery Week', async () => {
    const { row } = await render();

    expect((row(0).querySelector('input.recovery') as HTMLInputElement).disabled).toBe(true);
    expect((row(-1).querySelector('input.recovery') as HTMLInputElement).disabled).toBe(false);
  });

  it('shows the errors the API reports on their row', async () => {
    const { fixture, row, click, element } = await render();

    await click(element.querySelector('button.save'));
    TestBed.inject(HttpTestingController)
      .expectOne('/api/goal-races/7/training-plan/weeks')
      .flush(
        { errors: { 'weeks[2].recoveryWeek': ["The Race Week can't be a Recovery Week."] } },
        { status: 400, statusText: 'Bad Request' },
      );
    await fixture.whenStable();

    expect(row(0).textContent).toContain("The Race Week can't be a Recovery Week.");
  });

  it('drops errors once the plan changes shape, since they were for rows that have moved', async () => {
    const { fixture, element, row, click } = await render();
    const longRun = row(-1).querySelector('input.long-run') as HTMLInputElement;
    longRun.value = 'far';
    longRun.dispatchEvent(new Event('input'));
    await fixture.whenStable();
    await click(element.querySelector('button.save'));

    fixture.componentRef.setInput('plan', { ...plan, length: 12, weeks: plan.weeks.slice(1) });
    await fixture.whenStable();

    expect(element.textContent).not.toContain('Enter kilometres');
    // What was typed stays, for the week that is still in the plan.
    expect((row(-1).querySelector('input.long-run') as HTMLInputElement).value).toBe('far');
  });

  it('refuses kilometres it cannot read before sending anything', async () => {
    const { fixture, row, click, element } = await render();
    const longRun = row(-1).querySelector('input.long-run') as HTMLInputElement;

    longRun.value = 'far';
    longRun.dispatchEvent(new Event('input'));
    await fixture.whenStable();
    await click(element.querySelector('button.save'));

    TestBed.inject(HttpTestingController).expectNone('/api/goal-races/7/training-plan/weeks');
    expect(row(-1).textContent).toContain('Enter kilometres');
  });
});

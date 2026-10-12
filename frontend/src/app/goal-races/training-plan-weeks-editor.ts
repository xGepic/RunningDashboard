import { DatePipe } from '@angular/common';
import { HttpClient, HttpErrorResponse } from '@angular/common/http';
import { Component, inject, input, linkedSignal, output, signal } from '@angular/core';
import { FieldErrors, formError } from '../shared/field-errors';
import { kilometresToMetres } from '../shared/format';
import {
  DayPlan,
  RunningDay,
  runTypes,
  TrainingPlan,
  TrainingPlanWeek,
  TrainingPlanWeekRequest,
  Weekday,
  weekdays,
} from './training-plan';

/** One row of the editor: a week of the plan with what the Athlete has entered for it. */
interface WeekRow {
  week: TrainingPlanWeek;
  /** The planned distance as typed. */
  distanceKm: string;
  /** The planned Long Run as typed. */
  longRunKm: string;
  /** Monday first. */
  runningDays: RunningDay[];
  recoveryWeek: boolean;
}

/** The fields of a week the API reports errors for, as "weeks[i].field" with rows numbered as sent. */
type WeekField = 'weeksToRace' | 'plannedDistance' | 'plannedLongRun' | 'runningDays' | 'recoveryWeek';

/** The plan's weeks in a table: planned distance, planned Long Run, a Run Type or rest per day, and Recovery Week for each. */
@Component({
  selector: 'app-training-plan-weeks-editor',
  imports: [DatePipe],
  templateUrl: './training-plan-weeks-editor.html',
  styleUrl: './training-plan-weeks-editor.scss',
})
export class TrainingPlanWeeksEditor {
  private readonly http = inject(HttpClient);

  readonly plan = input.required<TrainingPlan>();
  /** The plan as saved with the new weeks. */
  readonly saved = output<TrainingPlan>();

  protected readonly weekdays = weekdays;
  protected readonly runTypes = runTypes;
  protected readonly formError = formError;

  /**
   * One per week of the plan. When the plan changes shape, weeks still in it keep what was entered, saved or not;
   * new weeks start from the plan.
   */
  protected readonly rows = linkedSignal<TrainingPlan, WeekRow[]>({
    source: this.plan,
    computation: (plan, previous) =>
      plan.weeks.map((week) => {
        const entered = previous?.value.find((row) => row.week.weeksToRace === week.weeksToRace);
        return entered ? { ...entered, week } : rowOf(week);
      }),
  });
  /** Keyed by row number, so they go once the plan changes shape. */
  protected readonly errors = linkedSignal<TrainingPlan, FieldErrors>({ source: this.plan, computation: () => ({}) });
  protected readonly saving = signal(false);
  /** Whether the weeks were saved and not changed since. */
  protected readonly justSaved = signal(false);

  protected fieldErrors(index: number, field: WeekField): string[] {
    return this.errors()[`weeks[${index}].${field}`] ?? [];
  }

  protected edit(index: number, change: Partial<Omit<WeekRow, 'week'>>): void {
    this.rows.update((rows) => rows.map((row, i) => (i === index ? { ...row, ...change } : row)));
    this.justSaved.set(false);
  }

  /** What a day shows in its cell: the Run Type's short name, or rest. */
  protected shortName(plan: DayPlan): string {
    return runTypes.find((t) => t.type === plan)?.short ?? 'Rest';
  }

  protected runTypeOn(row: WeekRow, day: Weekday): DayPlan {
    return row.runningDays.find((d) => d.day === day)?.runType ?? 'rest';
  }

  /** Makes `day` a Running Day of that Run Type, or a rest day. */
  protected pickRunType(index: number, day: Weekday, runType: DayPlan): void {
    const others = this.rows()[index].runningDays.filter((d) => d.day !== day);
    const picked = runType === 'rest' ? others : [...others, { day, runType }];
    this.edit(index, { runningDays: weekdays.flatMap((weekday) => picked.filter((d) => d.day === weekday.day)) });
  }

  /** The Weekly Target and Running Days of the week above; whether it is a Recovery Week stays the Athlete's call. */
  protected copyFromPrevious(index: number): void {
    const { distanceKm, longRunKm, runningDays } = this.rows()[index - 1];
    this.edit(index, { distanceKm, longRunKm, runningDays });
  }

  protected save(): void {
    const typingErrors: FieldErrors = {};
    const weeks = this.rows().map((row, i): TrainingPlanWeekRequest => {
      const plannedDistance = kilometres(row.distanceKm, () => (typingErrors[`weeks[${i}].plannedDistance`] = [typingError]));
      const plannedLongRun = kilometres(row.longRunKm, () => (typingErrors[`weeks[${i}].plannedLongRun`] = [typingError]));
      return { weeksToRace: row.week.weeksToRace, plannedDistance, plannedLongRun, runningDays: row.runningDays, recoveryWeek: row.recoveryWeek };
    });
    this.errors.set(typingErrors);
    if (Object.keys(typingErrors).length > 0) return;

    this.saving.set(true);
    this.http.put<TrainingPlan>(`/api/goal-races/${this.plan().goalRaceId}/training-plan/weeks`, { weeks }).subscribe({
      next: (plan) => {
        this.saving.set(false);
        this.justSaved.set(true);
        this.saved.emit(plan);
      },
      error: (response: HttpErrorResponse) => {
        this.saving.set(false);
        this.errors.set(response.error?.errors ?? { [formError]: ['The weeks could not be saved.'] });
      },
    });
  }
}

const typingError = 'Enter kilometres, e.g. 32.5.';

function rowOf(week: TrainingPlanWeek): WeekRow {
  return {
    week,
    distanceKm: week.plannedDistance === null ? '' : String(week.plannedDistance / 1000),
    longRunKm: week.plannedLongRun === null ? '' : String(week.plannedLongRun / 1000),
    runningDays: week.runningDays,
    recoveryWeek: week.recoveryWeek,
  };
}

/** Metres from kilometres as typed; null for an empty field, and for one it cannot read after telling `invalid`. */
function kilometres(text: string, invalid: () => void): number | null {
  if (text.trim() === '') return null;
  const metres = kilometresToMetres(text);
  if (metres === null) invalid();
  return metres;
}

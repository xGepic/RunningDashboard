import { DatePipe } from '@angular/common';
import { HttpClient, HttpErrorResponse, httpResource } from '@angular/common/http';
import { Component, computed, effect, inject, input, signal, untracked } from '@angular/core';
import { RouterLink } from '@angular/router';
import { FieldErrors, formError } from '../shared/field-errors';
import { GoalRace } from './goal-race';
import { PhaseType, TrainingPlan, TrainingPlanRequest, phaseTypes, phaseWeeks, planLengths } from './training-plan';
import { TrainingPlanWeeksEditor } from './training-plan-weeks-editor';

/** Creates or reshapes a Goal Race's Training Plan: its length and the type of each Phase. */
@Component({
  selector: 'app-training-plan-page',
  imports: [DatePipe, RouterLink, TrainingPlanWeeksEditor],
  templateUrl: './training-plan-page.html',
  styleUrl: './training-plan-page.scss',
})
export class TrainingPlanPage {
  private readonly http = inject(HttpClient);

  /** From the route, goal-races/:id/training-plan. */
  readonly id = input.required<string>();

  protected readonly goalRaces = httpResource<GoalRace[]>(() => '/api/goal-races');
  protected readonly race = computed(() =>
    this.goalRaces.hasValue() ? this.goalRaces.value().find((r) => String(r.id) === this.id()) : undefined,
  );
  /** The saved plan; an error (404) while the race has none. */
  protected readonly plan = httpResource<TrainingPlan>(() => `/api/goal-races/${this.id()}/training-plan`);
  protected readonly hasPlan = computed(() => this.plan.hasValue());
  protected readonly formError = formError;

  protected readonly planLengths = planLengths;
  protected readonly phaseTypes = phaseTypes;

  protected readonly length = signal<number>(16);
  protected readonly phases = signal<PhaseType[]>(defaultPhases(16));
  protected readonly errors = signal<FieldErrors>({});
  protected readonly saving = signal(false);
  protected readonly saved = signal(false);

  /** Each Phase's first and last Weeks to Race, as the plan would split. */
  protected readonly phaseRanges = computed(() =>
    this.phases().map((_, i) => {
      const first = 1 - this.length() + i * phaseWeeks;
      return { first, last: first + phaseWeeks - 1 };
    }),
  );

  /** The saved length and Phases; saving the weeks leaves them, and so the form, as they are. */
  private readonly savedShape = computed(
    () => {
      if (!this.plan.hasValue()) return undefined;
      const plan = this.plan.value();
      return { length: plan.length, phases: plan.phases.map((p) => p.type) };
    },
    { equal: (a, b) => a?.length === b?.length && a?.phases.join() === b?.phases.join() },
  );

  constructor() {
    // Start the form from the saved plan, once it is in, and again whenever its shape is saved.
    effect(() => {
      const shape = this.savedShape();
      if (!shape) return;
      untracked(() => {
        this.length.set(shape.length);
        this.phases.set(shape.phases);
      });
    });
  }

  /**
   * Like the plan's weeks, Phases count back from the race: a shorter plan keeps the last Phases, a longer one adds
   * Builds at the start.
   */
  protected setLength(length: number): void {
    const count = length / phaseWeeks;
    const phases = this.phases();
    this.length.set(length);
    this.phases.set(count <= phases.length ? phases.slice(-count) : [...Array<PhaseType>(count - phases.length).fill('build'), ...phases]);
    this.saved.set(false);
  }

  protected setPhase(index: number, type: PhaseType): void {
    this.phases.update((phases) => phases.map((p, i) => (i === index ? type : p)));
    this.saved.set(false);
  }

  protected save(): void {
    const request: TrainingPlanRequest = { length: this.length(), phases: this.phases() };
    this.saving.set(true);
    this.http.put<TrainingPlan>(`/api/goal-races/${this.id()}/training-plan`, request).subscribe({
      next: (plan) => {
        this.saving.set(false);
        this.errors.set({});
        this.saved.set(true);
        this.plan.set(plan);
        this.goalRaces.reload();
      },
      error: (response: HttpErrorResponse) => {
        this.saving.set(false);
        this.errors.set(response.error?.errors ?? { [formError]: ['The Training Plan could not be saved.'] });
      },
    });
  }

  /** Deletes the plan with everything planned for its weeks; the race's own Training Block length counts again. */
  protected remove(): void {
    const race = this.race();
    if (!race || !confirm(`Delete the Training Plan of ${race.name}, with everything planned for its weeks?`)) return;
    this.http.delete(`/api/goal-races/${this.id()}/training-plan`).subscribe({
      next: () => {
        this.errors.set({});
        this.saved.set(false);
        this.length.set(16);
        this.phases.set(defaultPhases(16));
        this.plan.reload();
        this.goalRaces.reload();
      },
      error: () => this.errors.set({ [formError]: ['The Training Plan could not be deleted.'] }),
    });
  }
}

/** Builds, then Peaks for the last two Phases before the Taper where there is room. */
function defaultPhases(length: number): PhaseType[] {
  const count = length / phaseWeeks;
  return Array.from({ length: count }, (_, i): PhaseType => (i === count - 1 ? 'taper' : i >= count - 3 ? 'peak' : 'build'));
}

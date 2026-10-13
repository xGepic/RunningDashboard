import { DecimalPipe } from '@angular/common';
import { HttpClient, HttpErrorResponse } from '@angular/common/http';
import { Component, computed, inject, input, output, signal } from '@angular/core';
import { NonNullableFormBuilder, ReactiveFormsModule } from '@angular/forms';
import { daysBetween, today } from '../shared/dates';
import { FieldErrors, formError } from '../shared/field-errors';
import { Units } from '../shared/units';
import { YearlyGoal } from './overview';

const whole = new Intl.NumberFormat('en', { maximumFractionDigits: 0 });
const oneDecimal = new Intl.NumberFormat('en', { minimumFractionDigits: 1, maximumFractionDigits: 1 });

/** For "Enter the goal in miles, e.g. 1200." */
const unitNames = { km: { name: 'kilometres', example: '2000' }, mi: { name: 'miles', example: '1200' } };

/** Metres; the most the API accepts as a goal. */
const maxDistance = 20_000_000;

/**
 * The current year's Yearly Goal: how far the Athlete has got, and whether that is ahead of or behind the goal spread
 * evenly over the days of the year. Sets, changes and removes the goal.
 */
@Component({
  selector: 'app-yearly-goal-card',
  imports: [DecimalPipe, ReactiveFormsModule],
  templateUrl: './yearly-goal-card.html',
  styleUrl: './yearly-goal-card.scss',
})
export class YearlyGoalCard {
  private readonly http = inject(HttpClient);
  /** The goal is shown and typed in the Distance Unit; the API keeps it in metres. */
  protected readonly units = inject(Units);

  /** The current year's goal; none without one. */
  readonly goal = input.required<YearlyGoal | null>();

  /** Metres run so far this year. */
  readonly distance = input.required<number>();

  /** The goal was set, changed or removed. */
  readonly changed = output<void>();

  protected readonly formError = formError;
  protected readonly year = today().getFullYear();
  protected readonly editing = signal(false);
  protected readonly saving = signal(false);
  protected readonly errors = signal<FieldErrors>({});

  /** The distance as typed, in the Distance Unit. */
  protected readonly form = inject(NonNullableFormBuilder).group({ distance: '' });

  /** Where the year stands against the goal; none without one. */
  protected readonly progress = computed(() => {
    const goal = this.goal()?.distance;
    if (!goal) return null;
    const done = this.distance();
    const now = today();
    const start = new Date(now.getFullYear(), 0, 1);
    const daysInYear = daysBetween(start, new Date(now.getFullYear() + 1, 0, 1));
    // Days before today: today's Runs put the Athlete ahead, not the day itself.
    const elapsed = daysBetween(start, now);
    const scheduled = (goal * elapsed) / daysInYear;
    const remaining = Math.max(0, goal - done);
    return {
      goal,
      done,
      percent: (done / goal) * 100,
      schedulePercent: (elapsed / daysInYear) * 100,
      ahead: done - scheduled,
      remaining,
      perWeek: remaining / ((daysInYear - elapsed) / 7),
    };
  });

  /** Whole kilometres or miles, without the unit: "1,642". */
  protected whole(metres: number): string {
    return whole.format(this.units.fromMetres(metres));
  }

  protected oneDecimal(metres: number): string {
    return oneDecimal.format(this.units.fromMetres(metres));
  }

  protected edit(): void {
    const goal = this.goal();
    this.form.setValue({ distance: goal ? this.units.inputValue(goal.distance) : '' });
    this.errors.set({});
    this.editing.set(true);
  }

  protected cancel(): void {
    this.errors.set({});
    this.editing.set(false);
  }

  protected save(): void {
    // A goal saved as it was keeps its metres, though the field shows it rounded.
    const distance = this.units.parse(this.form.getRawValue().distance, this.goal()?.distance ?? null);
    if (distance === null) {
      const unit = unitNames[this.units.unit()];
      this.errors.set({ distance: [`Enter the goal in ${unit.name}, e.g. ${unit.example}.`] });
      return;
    }
    if (distance > maxDistance) {
      this.errors.set({ distance: [`The goal is at most ${this.whole(maxDistance)} ${this.units.unit()}.`] });
      return;
    }
    this.saving.set(true);
    this.http.put<YearlyGoal>('/api/yearly-goal', { distance }).subscribe({
      next: () => {
        this.saving.set(false);
        this.editing.set(false);
        this.changed.emit();
      },
      error: (response: HttpErrorResponse) => {
        this.saving.set(false);
        const errors = response.error?.errors as FieldErrors | undefined;
        this.errors.set(errors?.['distance'] ? { distance: errors['distance'] } : { [formError]: ['The goal could not be saved.'] });
      },
    });
  }

  protected remove(): void {
    if (!confirm(`Remove the goal for ${this.year}?`)) return;
    this.http.delete('/api/yearly-goal').subscribe({
      next: () => {
        this.editing.set(false);
        this.changed.emit();
      },
      error: () => this.errors.set({ [formError]: ['The goal could not be removed.'] }),
    });
  }
}

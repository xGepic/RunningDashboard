import { DecimalPipe } from '@angular/common';
import { HttpClient, HttpErrorResponse } from '@angular/common/http';
import { Component, computed, inject, input, output, signal } from '@angular/core';
import { NonNullableFormBuilder, ReactiveFormsModule } from '@angular/forms';
import { daysBetween, today } from '../shared/dates';
import { FieldErrors, formError } from '../shared/field-errors';
import { kilometresToMetres } from '../shared/format';
import { YearlyGoal } from './overview';

const wholeKm = new Intl.NumberFormat('en', { maximumFractionDigits: 0 });
const oneDecimalKm = new Intl.NumberFormat('en', { minimumFractionDigits: 1, maximumFractionDigits: 1 });

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

  protected readonly form = inject(NonNullableFormBuilder).group({ km: '' });

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

  protected wholeKm(metres: number): string {
    return wholeKm.format(metres / 1000);
  }

  protected oneDecimalKm(metres: number): string {
    return oneDecimalKm.format(metres / 1000);
  }

  protected edit(): void {
    const goal = this.goal();
    this.form.setValue({ km: goal ? String(goal.distance / 1000) : '' });
    this.errors.set({});
    this.editing.set(true);
  }

  protected cancel(): void {
    this.errors.set({});
    this.editing.set(false);
  }

  protected save(): void {
    const distance = kilometresToMetres(this.form.getRawValue().km);
    if (distance === null) {
      this.errors.set({ km: ['Enter the goal in kilometres, e.g. 2000.'] });
      return;
    }
    if (distance > maxDistance) {
      this.errors.set({ km: [`The goal is at most ${wholeKm.format(maxDistance / 1000)} km.`] });
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
        // The API names the field in metres; the form's field is the one in kilometres.
        const errors = response.error?.errors as FieldErrors | undefined;
        this.errors.set(errors?.['distance'] ? { km: errors['distance'] } : { [formError]: ['The goal could not be saved.'] });
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

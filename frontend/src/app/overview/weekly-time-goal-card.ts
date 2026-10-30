import { DecimalPipe } from '@angular/common';
import { HttpClient, HttpErrorResponse } from '@angular/common/http';
import { Component, computed, inject, input, output, signal } from '@angular/core';
import { NonNullableFormBuilder, ReactiveFormsModule } from '@angular/forms';
import { TrainingTimeWeek, goalTimeLabel, trainingTimeOf } from '../shared/charts';
import { daysSinceMonday, today } from '../shared/dates';
import { FieldErrors, formError } from '../shared/field-errors';
import { formatHoursMinutes } from '../shared/format';
import { WeeklyTimeGoal } from './overview';

/** Seconds; the most the API accepts as a goal. */
const maxTime = 100 * 3600;

/** Seconds from hours as typed: "7", "7.5", "7,5" or "7:30"; null if it is none of them, or not more than nothing. */
export function parseHours(text: string): number | null {
  const value = text.trim();
  const clock = /^(\d{1,3}):([0-5]\d)$/.exec(value);
  const seconds = clock
    ? Number(clock[1]) * 3600 + Number(clock[2]) * 60
    : /^\d+([.,]\d+)?$/.test(value)
      ? Math.round(Number(value.replace(',', '.')) * 60) * 60
      : null;
  return seconds !== null && seconds > 0 ? seconds : null;
}

/**
 * The Weekly Time Goal: this Training Week's Training Time, Runs, Rides and Swims together, against the goal, and what
 * the week still needs. Sets, changes and removes the goal.
 */
@Component({
  selector: 'app-weekly-time-goal-card',
  imports: [DecimalPipe, ReactiveFormsModule],
  templateUrl: './weekly-time-goal-card.html',
  // The Yearly Goal's look, with a bar of its own.
  styleUrls: ['./yearly-goal-card.scss', './weekly-time-goal-card.scss'],
})
export class WeeklyTimeGoalCard {
  private readonly http = inject(HttpClient);

  /** None without one. */
  readonly goal = input.required<WeeklyTimeGoal | null>();

  /** This Training Week's Training Time. */
  readonly thisWeek = input.required<TrainingTimeWeek>();

  /** The goal was set, changed or removed. */
  readonly changed = output<void>();

  protected readonly formError = formError;
  protected readonly goalLabel = goalTimeLabel;
  protected readonly formatHoursMinutes = formatHoursMinutes;
  protected readonly editing = signal(false);
  protected readonly saving = signal(false);
  protected readonly errors = signal<FieldErrors>({});

  /** Hours per week as typed. */
  protected readonly form = inject(NonNullableFormBuilder).group({ hours: '' });

  /** Where the week stands against the goal; none without one. */
  protected readonly progress = computed(() => {
    const goal = this.goal()?.time;
    if (!goal) return null;
    const week = this.thisWeek();
    const done = trainingTimeOf(week);
    const remaining = Math.max(0, goal - done);
    // Days of this Training Week still to come, today included: 7 on Monday, 1 on Sunday.
    const daysLeft = 7 - daysSinceMonday(today());
    // Each sport's part of the bar, as a share of the goal; together at most the whole bar.
    let left = 100;
    const segments = (['runs', 'rides', 'swims'] as const).map((sport) => {
      const width = Math.min(left, (week[sport] / goal) * 100);
      left -= width;
      return { sport, width };
    });
    return { goal, done, percent: (done / goal) * 100, remaining, daysLeft, perDay: remaining / daysLeft, segments };
  });

  protected edit(): void {
    const goal = this.goal();
    this.form.setValue({ hours: goal ? String(Math.round((goal.time / 3600) * 100) / 100) : '' });
    this.errors.set({});
    this.editing.set(true);
  }

  protected cancel(): void {
    this.errors.set({});
    this.editing.set(false);
  }

  protected save(): void {
    const time = parseHours(this.form.getRawValue().hours);
    if (time === null) {
      this.errors.set({ hours: ['Enter the hours per week, e.g. 7.5 or 7:30.'] });
      return;
    }
    if (time > maxTime) {
      this.errors.set({ hours: [`The goal is at most ${maxTime / 3600} hours a week.`] });
      return;
    }
    this.saving.set(true);
    this.http.put<WeeklyTimeGoal>('/api/weekly-time-goal', { time }).subscribe({
      next: () => {
        this.saving.set(false);
        this.editing.set(false);
        this.changed.emit();
      },
      error: (response: HttpErrorResponse) => {
        this.saving.set(false);
        const errors = response.error?.errors as FieldErrors | undefined;
        this.errors.set(errors?.['time'] ? { hours: errors['time'] } : { [formError]: ['The goal could not be saved.'] });
      },
    });
  }

  protected remove(): void {
    if (!confirm('Remove the weekly goal?')) return;
    this.http.delete('/api/weekly-time-goal').subscribe({
      next: () => {
        this.editing.set(false);
        this.changed.emit();
      },
      error: () => this.errors.set({ [formError]: ['The goal could not be removed.'] }),
    });
  }
}

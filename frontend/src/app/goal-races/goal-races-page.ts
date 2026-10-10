import { DatePipe } from '@angular/common';
import { HttpClient, HttpErrorResponse, httpResource } from '@angular/common/http';
import { Component, inject, signal } from '@angular/core';
import { NonNullableFormBuilder, ReactiveFormsModule } from '@angular/forms';
import { formatDistance, formatDuration, parseDuration } from '../shared/format';
import { GoalRace, GoalRaceRequest, distancePresets } from './goal-race';

/** Field errors as the API reports them in its problem details, keyed by field name. */
type FieldErrors = Record<string, string[]>;

/** The distance picker's choice for a distance that is not a preset. */
const custom = 'custom';

/** Where errors that belong to no single field go. */
const formError = 'form';

@Component({
  selector: 'app-goal-races-page',
  imports: [DatePipe, ReactiveFormsModule],
  templateUrl: './goal-races-page.html',
  styleUrl: './goal-races-page.scss',
})
export class GoalRacesPage {
  private readonly http = inject(HttpClient);

  protected readonly races = httpResource<GoalRace[]>(() => '/api/goal-races');
  protected readonly presets = distancePresets;
  protected readonly custom = custom;
  protected readonly formError = formError;
  protected readonly formatDuration = formatDuration;

  /** The race being edited; null while the form creates a new one. */
  protected readonly editing = signal<GoalRace | null>(null);
  protected readonly errors = signal<FieldErrors>({});
  protected readonly saving = signal(false);

  private readonly formBuilder = inject(NonNullableFormBuilder);

  protected readonly form = this.formBuilder.group({
    name: '',
    date: '',
    /** A preset's distance in metres, as text, or "custom". */
    distance: String(distancePresets[0].distance),
    customKm: '',
    /** h:mm:ss, or empty for none. */
    targetTime: '',
    /** From a number input: null while it is empty or not a number. */
    trainingBlockWeeks: this.formBuilder.control<number | null>(16),
  });

  protected distanceLabel(metres: number): string {
    return presetFor(metres)?.name ?? formatDistance(metres);
  }

  protected edit(race: GoalRace): void {
    const preset = presetFor(race.distance);
    this.form.setValue({
      name: race.name,
      date: race.date,
      distance: preset ? String(preset.distance) : custom,
      customKm: preset ? '' : String(race.distance / 1000),
      targetTime: race.targetTime === null ? '' : formatDuration(race.targetTime),
      trainingBlockWeeks: race.trainingBlockWeeks,
    });
    this.errors.set({});
    this.editing.set(race);
  }

  protected cancel(): void {
    this.form.reset();
    this.errors.set({});
    this.editing.set(null);
  }

  protected save(): void {
    const value = this.form.getRawValue();
    const targetTime = value.targetTime.trim() === '' ? null : parseDuration(value.targetTime);
    const distance = value.distance === custom ? kilometresToMetres(value.customKm) : Number(value.distance);
    const weeks = value.trainingBlockWeeks;

    // What the API could not even read gets its message here, in the units the Athlete typed.
    const typingErrors: FieldErrors = {};
    if (targetTime === null && value.targetTime.trim() !== '')
      typingErrors['targetTime'] = ['Write the Target Time as h:mm:ss, e.g. 4:59:59.'];
    if (value.distance === custom && distance === null)
      typingErrors['customKm'] = ['Enter the distance in kilometres, e.g. 15.5.'];
    if (weeks !== null && !Number.isInteger(weeks))
      typingErrors['trainingBlockWeeks'] = ['The Training Block is a whole number of weeks.'];
    if (Object.keys(typingErrors).length > 0) {
      this.errors.set(typingErrors);
      return;
    }

    const request: GoalRaceRequest = { name: value.name, date: value.date || null, distance, targetTime, trainingBlockWeeks: weeks };
    const editing = this.editing();
    const submission = editing
      ? this.http.put<GoalRace>(`/api/goal-races/${editing.id}`, request)
      : this.http.post<GoalRace>('/api/goal-races', request);

    this.saving.set(true);
    submission.subscribe({
      next: () => {
        this.saving.set(false);
        this.cancel();
        this.races.reload();
      },
      error: (response: HttpErrorResponse) => {
        this.saving.set(false);
        this.errors.set(response.error?.errors ?? { [formError]: ['The Goal Race could not be saved.'] });
      },
    });
  }

  protected remove(race: GoalRace): void {
    if (!confirm(`Delete ${race.name}?`)) return;
    this.http.delete(`/api/goal-races/${race.id}`).subscribe({
      next: () => {
        if (this.editing()?.id === race.id) this.cancel();
        this.races.reload();
      },
      error: () => this.errors.set({ [formError]: [`${race.name} could not be deleted.`] }),
    });
  }
}

function presetFor(metres: number) {
  return distancePresets.find((preset) => preset.distance === metres);
}

/** Metres from kilometres as typed, accepting a decimal comma; null if it is not a positive number. */
function kilometresToMetres(text: string): number | null {
  const km = Number(text.trim().replace(',', '.'));
  return text.trim() === '' || Number.isNaN(km) || km <= 0 ? null : Math.round(km * 1000 * 10) / 10;
}

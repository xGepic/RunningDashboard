import { DatePipe } from '@angular/common';
import { HttpClient, HttpErrorResponse, httpResource } from '@angular/common/http';
import { Component, ElementRef, Injector, afterNextRender, computed, inject, signal, viewChild } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { NonNullableFormBuilder, ReactiveFormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { formatDifference, formatDistance, formatDuration, formatPace, kilometresToMetres, parseDuration } from '../shared/format';
import { reloadOnSync } from '../sync/reload-on-sync';
import { daysBetween, localDate, today } from '../shared/dates';
import { FieldErrors, formError } from '../shared/field-errors';
import { GoalRace, GoalRaceRequest, distancePresets } from './goal-race';

/** The distance picker's choice for a distance that is not a preset. */
const custom = 'custom';

@Component({
  selector: 'app-goal-races-page',
  imports: [DatePipe, ReactiveFormsModule, RouterLink],
  templateUrl: './goal-races-page.html',
  styleUrl: './goal-races-page.scss',
})
export class GoalRacesPage {
  private readonly http = inject(HttpClient);
  private readonly injector = inject(Injector);

  protected readonly races = httpResource<GoalRace[]>(() => '/api/goal-races');
  protected readonly presets = distancePresets;
  protected readonly custom = custom;
  protected readonly formError = formError;
  protected readonly formatDuration = formatDuration;
  protected readonly formatDifference = formatDifference;
  protected readonly formatDistance = formatDistance;
  protected readonly formatPace = formatPace;

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

  private readonly formCard = viewChild<ElementRef<HTMLElement>>('formCard');
  private readonly formValue = toSignal(this.form.valueChanges, { initialValue: this.form.getRawValue() });

  /** The nearest Goal Race from today on, with its countdown and the weeks of its Training Block. */
  protected readonly next = computed(() => {
    const races = this.races.hasValue() ? this.races.value() : [];
    const now = today();
    const race = races.filter((r) => localDate(r.date) >= now).sort((a, b) => a.date.localeCompare(b.date))[0];
    if (!race) return null;
    const start = localDate(race.trainingBlockStart);
    const weekCount = race.trainingPlanLength ?? race.trainingBlockWeeks;
    const weeks = Array.from({ length: weekCount }, (_, i) => {
      const monday = new Date(start.getFullYear(), start.getMonth(), start.getDate() + i * 7);
      const days = daysBetween(monday, now);
      const state = i === weekCount - 1 ? 'race' : days >= 7 ? 'done' : days >= 0 ? 'current' : 'upcoming';
      return { number: i + 1, monday, state, opacity: 0.18 + (0.5 * i) / weekCount };
    });
    return {
      race,
      daysToRace: daysBetween(now, localDate(race.date)),
      daysToStart: daysBetween(now, start),
      currentWeek: weeks.find((w) => w.state === 'current')?.number ?? null,
      weeks,
      targetPace: race.targetTime === null ? null : race.targetTime / (race.distance / 1000),
    };
  });

  /** While a Goal Race is upcoming no other can be added, so the form is there only to edit a race. */
  protected readonly formShown = computed(() => this.editing() !== null || (this.races.hasValue() && this.next() === null));

  /** Where the Training Block of the race being entered would start, as the form stands. */
  protected readonly blockPreview = computed(() => {
    const { date, trainingBlockWeeks } = this.formValue();
    if (!date || !trainingBlockWeeks || trainingBlockWeeks < 1 || !Number.isInteger(trainingBlockWeeks)) return null;
    const race = localDate(date);
    const monday = new Date(race.getFullYear(), race.getMonth(), race.getDate() - ((race.getDay() + 6) % 7));
    return { start: new Date(monday.getFullYear(), monday.getMonth(), monday.getDate() - (trainingBlockWeeks - 1) * 7), race };
  });

  constructor() {
    // Race Results come from the Runs, so a sync can bring one in.
    reloadOnSync(() => this.races.reload());
  }

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
    // The form may only appear once editing starts.
    afterNextRender(() => this.formCard()?.nativeElement.scrollIntoView({ behavior: 'smooth', block: 'start' }), {
      injector: this.injector,
    });
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

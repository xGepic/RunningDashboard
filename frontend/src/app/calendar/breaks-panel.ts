import { HttpClient, HttpErrorResponse } from '@angular/common/http';
import { Component, ElementRef, Injector, afterNextRender, computed, inject, input, signal, viewChild } from '@angular/core';
import { NonNullableFormBuilder, ReactiveFormsModule } from '@angular/forms';
import { Break, BreakKind, BreakRequest, Breaks, breakDays, breakKinds, breaksWithin, kindLabel } from '../shared/breaks';
import { isoDate, localDate, today } from '../shared/dates';
import { FieldErrors, fieldErrorsOf, formError } from '../shared/field-errors';

/**
 * The Breaks with days from `from` to `to`, and a modal to mark a Break or to change one. Whatever shows Breaks
 * gets them again once one is saved or removed.
 */
@Component({
  selector: 'app-breaks-panel',
  imports: [ReactiveFormsModule],
  templateUrl: './breaks-panel.html',
  styleUrl: './breaks-panel.scss',
})
export class BreaksPanel {
  private readonly http = inject(HttpClient);
  private readonly injector = inject(Injector);
  private readonly breaks = inject(Breaks);

  /** The first day shown, yyyy-MM-dd. */
  readonly from = input.required<string>();
  /** The last day shown, yyyy-MM-dd. */
  readonly to = input.required<string>();
  /** What is shown, e.g. "September 2026" or "2026". */
  readonly period = input.required<string>();

  protected readonly kinds = breakKinds;
  protected readonly kindLabel = kindLabel;
  protected readonly breakDays = breakDays;
  protected readonly formError = formError;

  protected readonly shown = computed(() => breaksWithin(this.breaks.list(), this.from(), this.to()));

  /** The Break being changed; null while the form marks a new one. */
  protected readonly editing = signal<Break | null>(null);
  protected readonly open = signal(false);
  protected readonly errors = signal<FieldErrors>({});
  protected readonly saving = signal(false);

  protected readonly form = inject(NonNullableFormBuilder).group({
    kind: 'injury' as BreakKind,
    start: '',
    end: '',
    note: '',
  });

  private readonly dialog = viewChild.required<ElementRef<HTMLDialogElement>>('dialog');

  /** "3 Mar – 24 Mar 2026", or "28 Dec 2025 – 4 Jan 2026" across a year's end, or one day. */
  protected days(period: Break): string {
    const format = (date: string, year: boolean) =>
      localDate(date).toLocaleDateString('en-GB', { day: 'numeric', month: 'short', ...(year ? { year: 'numeric' } : {}) });
    if (period.start === period.end) return format(period.start, true);
    return `${format(period.start, period.start.slice(0, 4) !== period.end.slice(0, 4))} – ${format(period.end, true)}`;
  }

  /** The form for a new Break of today, or of the first day shown when today is not among them. */
  mark(): void {
    const now = isoDate(today());
    const day = now >= this.from() && now <= this.to() ? now : this.from();
    this.show(null, { kind: 'injury', start: day, end: day, note: '' });
  }

  protected edit(period: Break): void {
    this.show(period, { kind: period.kind, start: period.start, end: period.end, note: period.note ?? '' });
  }

  protected cancel(): void {
    this.open.set(false);
    this.editing.set(null);
    this.errors.set({});
    this.dialog().nativeElement.close();
  }

  /** Closed with Esc: dropped, as with Cancel. */
  protected dialogClosed(): void {
    if (this.open()) this.cancel();
  }

  protected save(): void {
    const value = this.form.getRawValue();
    const request: BreakRequest = { kind: value.kind, start: value.start || null, end: value.end || null, note: value.note };
    const editing = this.editing();
    const submission = editing ? this.http.put<Break>(`/api/breaks/${editing.id}`, request) : this.http.post<Break>('/api/breaks', request);
    this.saving.set(true);
    submission.subscribe({
      next: () => {
        this.saving.set(false);
        this.cancel();
        this.breaks.reload();
      },
      error: (response: HttpErrorResponse) => {
        this.saving.set(false);
        this.errors.set(fieldErrorsOf(response, 'The Break could not be saved.'));
      },
    });
  }

  protected remove(period: Break): void {
    if (!confirm(`Remove the ${kindLabel(period.kind)} Break of ${this.days(period)}?`)) return;
    this.http.delete(`/api/breaks/${period.id}`).subscribe({
      next: () => {
        if (this.editing()?.id === period.id) this.cancel();
        this.breaks.reload();
      },
      error: () => this.errors.set({ [formError]: [`The ${kindLabel(period.kind)} Break could not be removed.`] }),
    });
  }

  private show(period: Break | null, value: { kind: BreakKind; start: string; end: string; note: string }): void {
    this.form.setValue(value);
    this.errors.set({});
    this.editing.set(period);
    this.open.set(true);
    // Opened once the form is in it, so its first field gets the focus.
    afterNextRender(() => this.dialog().nativeElement.showModal(), { injector: this.injector });
  }
}

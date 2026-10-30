import { httpResource } from '@angular/common/http';
import { Injectable, computed } from '@angular/core';
import { addDays, daysBetween, isoDate, localDate } from './dates';

export type BreakKind = 'injury' | 'illness' | 'holiday' | 'other';

/** A Break as GET /api/breaks lists them, oldest first: days the Athlete marked with a reason. */
export interface Break {
  id: number;
  kind: BreakKind;
  /** The first day, yyyy-MM-dd. */
  start: string;
  /** The last day, yyyy-MM-dd; the first one for a Break of a day. */
  end: string;
  /** None without one. */
  note: string | null;
}

/** What POST and PUT /api/breaks take. */
export interface BreakRequest {
  kind: BreakKind;
  start: string | null;
  end: string | null;
  note: string;
}

/** The kinds in the order the form offers them, each named as the dashboard shows it. */
export const breakKinds: readonly { kind: BreakKind; label: string }[] = [
  { kind: 'injury', label: 'Injury' },
  { kind: 'illness', label: 'Illness' },
  { kind: 'holiday', label: 'Holiday' },
  { kind: 'other', label: 'Other' },
];

export function kindLabel(kind: BreakKind): string {
  return breakKinds.find((k) => k.kind === kind)!.label;
}

/** "Injury · Calf strain", or "Injury" without a note. */
export function breakLabel(period: Break): string {
  return period.note ? `${kindLabel(period.kind)} · ${period.note}` : kindLabel(period.kind);
}

/** The Breaks with a day from `from` to `to` (yyyy-MM-dd, both included), in their order. */
export function breaksWithin(breaks: readonly Break[], from: string, to: string): Break[] {
  return breaks.filter((period) => period.start <= to && from <= period.end);
}

/** The Breaks with a day in the Training Week starting on the Monday `start` (yyyy-MM-dd). */
export function breaksInWeek(breaks: readonly Break[], start: string): Break[] {
  return breaksWithin(breaks, start, isoDate(addDays(localDate(start), 6)));
}

/** The Break on the day `date` (yyyy-MM-dd); none on a day without one, as Breaks never overlap. */
export function breakOn(breaks: readonly Break[], date: string): Break | null {
  return breaks.find((period) => period.start <= date && date <= period.end) ?? null;
}

/** How many days a Break has, its first and last included. */
export function breakDays(period: Break): number {
  return daysBetween(localDate(period.start), localDate(period.end)) + 1;
}

/**
 * The Breaks, loaded once for every page that shows them and again after one is marked, changed or removed. They only
 * explain, so a page goes without them while they load or when they cannot be loaded.
 */
@Injectable({ providedIn: 'root' })
export class Breaks {
  private readonly resource = httpResource<Break[]>(() => '/api/breaks');

  /** Oldest first. */
  readonly list = computed(() => (this.resource.hasValue() ? this.resource.value() : []));

  reload(): void {
    this.resource.reload();
  }
}

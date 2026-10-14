import { DatePipe } from '@angular/common';
import { httpResource } from '@angular/common/http';
import { Component, computed, inject, input } from '@angular/core';
import { RouterLink } from '@angular/router';
import { fullMonthNames } from '../shared/dates';
import { formatDifference, formatDuration, formatHoursMinutes } from '../shared/format';
import { Units } from '../shared/units';
import { reloadOnSync } from '../sync/reload-on-sync';
import { ReviewTotals, YearReview } from './review';

/** A year's highlights, under the Calendar's Review tab: its totals against the year before, and its best moments. */
@Component({
  selector: 'app-year-review',
  imports: [DatePipe, RouterLink],
  templateUrl: './year-review.html',
  styleUrl: './year-review.scss',
})
export class YearReviewPanel {
  readonly year = input.required<number>();

  /** Distances in the Distance Unit from Settings. */
  protected readonly units = inject(Units);

  protected readonly review = httpResource<YearReview>(() => ({ url: '/api/review', params: { year: String(this.year()) } }));

  /** "so far" for the current year, "from 28 Apr" for the first one when it started later; null for a whole year. */
  protected readonly period = computed(() => {
    if (!this.review.hasValue()) return null;
    const review = this.review.value();
    if (!review.until.endsWith('-12-31')) return 'so far';
    return review.from.endsWith('-01-01') ? null : 'from';
  });

  /** "vs. 2025", or "vs. the same time in 2025" for the current year. */
  protected readonly comparedWith = computed(() => {
    if (!this.review.hasValue()) return '';
    const review = this.review.value();
    return this.period() === 'so far' ? `vs. the same time in ${review.year - 1}` : `vs. ${review.year - 1}`;
  });

  /** The four totals, each with its change against the year before. */
  protected readonly totals = computed(() => {
    if (!this.review.hasValue()) return [];
    const { totals, previousYear } = this.review.value();
    const figure = (label: string, of: (t: ReviewTotals) => number, format: (value: number) => string) => ({
      label,
      value: format(of(totals)),
      change: previousYear ? change(of(totals), of(previousYear)) : null,
    });
    return [
      figure('Runs', (t) => t.runs, String),
      figure('Distance', (t) => t.distance, (v) => this.units.totalDistance(v)),
      figure('Moving Time', (t) => t.movingTime, formatHoursMinutes),
      figure('Elevation Gain', (t) => t.elevationGain, (v) => `${Math.round(v).toLocaleString('en')} m`),
    ];
  });

  protected readonly fullMonthNames = fullMonthNames;
  protected readonly formatDuration = formatDuration;
  protected readonly formatDifference = formatDifference;

  constructor() {
    reloadOnSync(() => this.review.reload());
  }

  /** "1:00 faster" than the Personal Best the year started with. */
  protected improvement(elapsedTime: number, previous: number): string {
    return `${formatDuration(previous - elapsedTime)} faster`;
  }
}

/** "+12%" or "−5%" against the year before; none when there was nothing to compare with. */
function change(current: number, previous: number): { text: string; up: boolean } | null {
  if (previous === 0) return null;
  const percent = Math.round(((current - previous) / previous) * 100);
  return { text: `${percent > 0 ? '+' : percent < 0 ? '−' : '±'}${Math.abs(percent)}%`, up: percent >= 0 };
}

import { DatePipe } from '@angular/common';
import { httpResource } from '@angular/common/http';
import { Component, computed, inject, input } from '@angular/core';
import { RouterLink } from '@angular/router';
import { MonthTotals } from '../runs/run';
import { fullMonthNames, monthNames } from '../shared/dates';
import { formatDifference, formatDuration, formatHoursMinutes, formatWhole } from '../shared/format';
import { Units } from '../shared/units';
import { reloadOnSync } from '../sync/reload-on-sync';
import { ReviewTotals, YearReview } from './review';

/** A year's highlights, under the Calendar's Review tab: a headline, its totals against the year before, and its best moments. */
@Component({
  selector: 'app-year-review',
  imports: [DatePipe, RouterLink],
  templateUrl: './year-review.html',
  styleUrl: './year-review.scss',
})
export class YearReviewPanel {
  readonly year = input.required<number>();
  /** The year's totals per month, as the Calendar has them, for the bars in the headline; none while they load. */
  readonly months = input<MonthTotals[] | null>(null);

  /** Distances in the Distance Unit from Settings. */
  protected readonly units = inject(Units);

  protected readonly review = httpResource<YearReview>(() => ({ url: '/api/review', params: { year: String(this.year()) } }));

  /** "so far" for the current year, "from" for the first one when it started later; null for a whole year. */
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

  /** The headline: the year's distance, big, with its change, and the rest of the totals in a sentence. */
  protected readonly headline = computed(() => {
    if (!this.review.hasValue()) return null;
    const { totals, previousYear } = this.review.value();
    return {
      distance: formatWhole(this.units.fromMetres(totals.distance)),
      change: previousYear ? change(totals.distance, previousYear.distance) : null,
      runs: totals.runs,
      hours: formatWhole(totals.movingTime / 3600),
      climbed: formatWhole(totals.elevationGain),
    };
  });

  /** January to December as bars against the biggest month; months still ahead are empty. */
  protected readonly monthBars = computed(() => {
    const months = this.months();
    if (!months?.length) return [];
    const biggest = Math.max(...months.map((m) => m.distance));
    return months.map((m) => ({
      initial: monthNames[m.month - 1][0],
      height: biggest > 0 ? Math.max(m.distance > 0 ? 4 : 0, (m.distance / biggest) * 100) : 0,
      biggest: biggest > 0 && m.distance === biggest,
      title: `${fullMonthNames[m.month - 1]}: ${m.runs > 0 ? this.units.distance(m.distance, 1) : 'no Runs'}`,
    }));
  });

  /** The tiles under the headline: totals with their change, and averages that put the year in proportion. */
  protected readonly tiles = computed(() => {
    if (!this.review.hasValue()) return [];
    const review = this.review.value();
    const { totals, previousYear } = review;
    const withChange = (label: string, of: (t: ReviewTotals) => number, value: string) => ({
      label,
      value,
      change: previousYear ? change(of(totals), of(previousYear)) : null,
    });
    const weeks = Math.max(1, (Date.parse(review.until) - Date.parse(review.from)) / (7 * 86_400_000) + 1 / 7);
    return [
      withChange('Runs', (t) => t.runs, String(totals.runs)),
      withChange('Moving Time', (t) => t.movingTime, formatHoursMinutes(totals.movingTime)),
      withChange('Elevation Gain', (t) => t.elevationGain, `${formatWhole(totals.elevationGain)} m`),
      { label: 'Per week', value: this.units.distance(totals.distance / weeks, 1), change: null },
      { label: 'Average Run', value: this.units.distance(totals.runs ? totals.distance / totals.runs : 0, 1), change: null },
    ];
  });

  /**
   * The year's Personal Bests by distance, each with how much faster it was than the one the year started with. The
   * biggest leap (in %) stands out; the bars compare each leap with it. At most four to a row, the rows as even as
   * they can be: 5 are 3 and 2, 7 are 4 and 3; `breakAfter` ends a row.
   */
  protected readonly personalBests = computed(() => {
    const bests = this.review.hasValue() ? this.review.value().personalBests : [];
    const leaps = bests.map((b) => (b.previousElapsedTime === null ? null : (b.previousElapsedTime - b.elapsedTime) / b.previousElapsedTime));
    const biggest = Math.max(0, ...leaps.filter((l): l is number => l !== null));
    const perRow = Math.ceil(bests.length / Math.ceil(bests.length / maxMedalsPerRow));
    return bests.map((best, i) => {
      const leap = leaps[i];
      return {
        ...best,
        time: formatDuration(best.elapsedTime),
        percent: leap === null ? null : `${(leap * 100).toFixed(1)}% faster`,
        saved: best.previousElapsedTime === null ? null : `−${formatDuration(best.previousElapsedTime - best.elapsedTime)}`,
        was: best.previousElapsedTime === null ? null : formatDuration(best.previousElapsedTime),
        bar: leap === null || biggest === 0 ? 0 : Math.max(4, (leap / biggest) * 100),
        biggestLeap: leap !== null && biggest > 0 && leap === biggest,
        breakAfter: (i + 1) % perRow === 0 && i < bests.length - 1,
      };
    });
  });

  /** "8 set in 2026 · biggest leap: 10K, 11.2% faster". */
  protected readonly personalBestSummary = computed(() => {
    const bests = this.personalBests();
    if (!bests.length) return '';
    const set = `${bests.length} set in ${this.year()}`;
    const leap = bests.find((b) => b.biggestLeap);
    return leap ? `${set} · biggest leap: ${leap.name}, ${leap.percent}` : set;
  });

  protected readonly fullMonthNames = fullMonthNames;
  protected readonly formatDuration = formatDuration;
  protected readonly formatDifference = formatDifference;

  constructor() {
    reloadOnSync(() => this.review.reload());
  }
}

/** Personal Bests side by side on a wide page. */
const maxMedalsPerRow = 4;

/** "+12%" or "−5%" against the year before; none when there was nothing to compare with. */
function change(current: number, previous: number): { text: string; up: boolean } | null {
  if (previous === 0) return null;
  const percent = Math.round(((current - previous) / previous) * 100);
  return { text: `${percent > 0 ? '+' : percent < 0 ? '−' : '±'}${Math.abs(percent)}%`, up: percent >= 0 };
}

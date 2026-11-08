import { httpResource } from '@angular/common/http';
import { Component, computed, inject, signal } from '@angular/core';
import { Router } from '@angular/router';
import { NgxEchartsDirective } from 'ngx-echarts';
import { Breaks } from '../shared/breaks';
import { ChartTheme } from '../shared/chart-theme';
import { weekLabel } from '../shared/charts';
import { reloadOnSync } from '../sync/reload-on-sync';
import { Freshness, FreshnessRange, daysIn, formatLoad, freshnessPhase, freshnessRanges, freshnessWeeks, hardestDays, racesIn } from './freshness';
import { clickedRun, freshnessChart } from './freshness-chart';

/**
 * Fitness & Freshness: where the Athlete is in their training, in words, and how Fitness and Fatigue went over the
 * range picked, from the Runs' Relative Effort. Loads its own figures, apart from the rest of the
 * Fitness page.
 */
@Component({
  selector: 'app-freshness-card',
  imports: [NgxEchartsDirective],
  templateUrl: './freshness-card.html',
  styleUrl: './freshness-card.scss',
})
export class FreshnessCard {
  private readonly theme = inject(ChartTheme);
  /** Shaded in the chart, as they explain a drop. */
  private readonly breaks = inject(Breaks);

  protected readonly freshness = httpResource<Freshness>(() => '/api/fitness/freshness');

  protected readonly ranges = freshnessRanges;
  protected readonly range = signal<FreshnessRange>('6m');

  private readonly allDays = computed(() => (this.freshness.hasValue() ? this.freshness.value().days : []));

  /** The days of the range picked, up to today. */
  protected readonly days = computed(() => daysIn(this.allDays(), this.range()));

  /** Where the Athlete is in their training today, from the whole history; none without Runs. */
  protected readonly phase = computed(() => freshnessPhase(this.allDays(), this.freshness.hasValue() ? this.freshness.value().races : []));

  /** Today's figures, small, under the summary; none without Runs. */
  protected readonly today = computed(() => {
    const last = this.days().at(-1);
    return last ? { fitness: formatLoad(last.fitness), fatigue: formatLoad(last.fatigue), form: formatLoad(last.form, true) } : null;
  });

  protected readonly chart = computed(() => {
    const days = this.days();
    if (days.length === 0 || !this.freshness.hasValue()) return null;
    return freshnessChart(days, racesIn(this.freshness.value().races, days), hardestDays(days), this.breaks.list(), this.theme.colors());
  });

  /** The range by Training Week, newest first, for the table view. */
  protected readonly weeks = computed(() =>
    freshnessWeeks(this.days()).map((w) => ({
      label: weekLabel(w.start),
      load: formatLoad(w.load),
      fitness: formatLoad(w.fitness),
      fatigue: formatLoad(w.fatigue),
      form: formatLoad(w.form, true),
    })),
  );

  private readonly router = inject(Router);

  constructor() {
    reloadOnSync(() => this.freshness.reload());
  }

  /** A click on one of the hardest days' markers opens its Run. */
  protected open(event: { componentType?: string; data?: unknown }): void {
    const runId = clickedRun(event);
    if (runId !== null) this.router.navigate(['/runs', runId]);
  }
}

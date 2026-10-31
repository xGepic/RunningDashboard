import { httpResource } from '@angular/common/http';
import { Component, computed, inject } from '@angular/core';
import { NgxEchartsDirective } from 'ngx-echarts';
import { Breaks } from '../shared/breaks';
import { ChartTheme } from '../shared/chart-theme';
import { monthNames } from '../shared/dates';
import { reloadOnSync } from '../sync/reload-on-sync';
import { Fitness, FitnessMonth, FitnessRun, formatCadence } from './fitness';
import { fitnessChart } from './fitness-chart';

/** The Cadence of the Fitness Runs over the whole history. A Run's Aerobic Efficiency and Cardiac Drift are on its own page. */
@Component({
  selector: 'app-fitness-page',
  imports: [NgxEchartsDirective],
  templateUrl: './fitness-page.html',
  styleUrl: './fitness-page.scss',
})
export class FitnessPage {
  private readonly theme = inject(ChartTheme);
  /** Shaded in the chart, as they explain a dip. */
  private readonly breaks = inject(Breaks);

  protected readonly fitness = httpResource<Fitness>(() => '/api/fitness');

  /** None until the figures are in, or when no Run has a Cadence. */
  protected readonly cadenceChart = computed(() => {
    if (!this.fitness.hasValue()) return null;
    const fitness = this.fitness.value();
    const runs = fitness.runs.filter((r): r is FitnessRun & { cadence: number } => r.cadence !== null);
    if (runs.length === 0) return null;
    return fitnessChart(
      {
        runs: runs.map((r) => ({ startLocal: r.startLocal, name: r.name, value: r.cadence })),
        months: fitness.months
          .filter((m): m is FitnessMonth & { cadence: number } => m.cadence !== null)
          .map((m) => ({ year: m.year, month: m.month, value: m.cadence })),
        format: formatCadence,
        breaks: this.breaks.list(),
      },
      this.theme.colors(),
    );
  });

  /** The monthly averages, newest first, for the table view. */
  protected readonly months = computed(() =>
    (this.fitness.hasValue() ? this.fitness.value().months : [])
      .map((m) => ({ ...m, label: `${monthNames[m.month - 1]} ${m.year}` }))
      .reverse(),
  );

  protected readonly formatCadence = formatCadence;

  constructor() {
    reloadOnSync(() => this.fitness.reload());
  }
}

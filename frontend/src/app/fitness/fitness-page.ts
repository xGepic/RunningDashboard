import { httpResource } from '@angular/common/http';
import { Component, computed, inject } from '@angular/core';
import { NgxEchartsDirective } from 'ngx-echarts';
import { Breaks } from '../shared/breaks';
import { ChartTheme } from '../shared/chart-theme';
import { monthNames } from '../shared/dates';
import { reloadOnSync } from '../sync/reload-on-sync';
import { Fitness, FitnessMonth, FitnessRun, formatCadence, formatDrift, formatEfficiency } from './fitness';
import { FitnessChartData, fitnessChart } from './fitness-chart';

type Figure = 'aerobicEfficiency' | 'cardiacDrift' | 'cadence';

/** Whether the Athlete is getting fitter: Aerobic Efficiency, Cardiac Drift and Cadence of the Fitness Runs over the whole history. */
@Component({
  selector: 'app-fitness-page',
  imports: [NgxEchartsDirective],
  templateUrl: './fitness-page.html',
  styleUrl: './fitness-page.scss',
})
export class FitnessPage {
  private readonly theme = inject(ChartTheme);
  /** Shaded in every chart, as they explain a dip. */
  private readonly breaks = inject(Breaks);

  protected readonly fitness = httpResource<Fitness>(() => '/api/fitness');

  protected readonly efficiencyChart = computed(() => this.chart('aerobicEfficiency', formatEfficiency));
  protected readonly driftChart = computed(() => this.chart('cardiacDrift', formatDrift, true));
  protected readonly cadenceChart = computed(() => this.chart('cadence', formatCadence));

  /** The monthly averages, newest first, for the table view. */
  protected readonly months = computed(() =>
    (this.fitness.hasValue() ? this.fitness.value().months : [])
      .map((m) => ({ ...m, label: `${monthNames[m.month - 1]} ${m.year}` }))
      .reverse(),
  );

  protected readonly goodCardiacDrift = computed(() => (this.fitness.hasValue() ? formatDrift(this.fitness.value().goodCardiacDrift) : ''));

  protected readonly formatEfficiency = formatEfficiency;
  protected readonly formatDrift = formatDrift;
  protected readonly formatCadence = formatCadence;

  constructor() {
    reloadOnSync(() => this.fitness.reload());
  }

  /** None until the figures are in, or when no Run has this one. */
  private chart(figure: Figure, format: (value: number) => string, withGoodBand = false) {
    if (!this.fitness.hasValue()) return null;
    const fitness = this.fitness.value();
    const runs = fitness.runs.filter((r): r is FitnessRun & Record<Figure, number> => r[figure] !== null);
    if (runs.length === 0) return null;
    const data: FitnessChartData = {
      runs: runs.map((r) => ({ startLocal: r.startLocal, name: r.name, value: r[figure] })),
      months: fitness.months
        .filter((m): m is FitnessMonth & Record<Figure, number> => m[figure] !== null)
        .map((m) => ({ year: m.year, month: m.month, value: m[figure] })),
      format,
      goodUpTo: withGoodBand ? fitness.goodCardiacDrift : undefined,
      breaks: this.breaks.list(),
    };
    return fitnessChart(data, this.theme.colors());
  }
}

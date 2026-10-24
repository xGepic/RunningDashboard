import { DatePipe, DecimalPipe } from '@angular/common';
import { httpResource } from '@angular/common/http';
import { Component, computed, inject, input } from '@angular/core';
import { RouterLink } from '@angular/router';
import type { EChartsCoreOption } from 'echarts/core';
import { NgxEchartsDirective } from 'ngx-echarts';
import { formatCadence, formatDrift, formatEfficiency } from '../fitness/fitness';
import { ChartColors, ChartTheme, tooltipHtml } from '../shared/chart-theme';
import { paceChart, paceLabel } from '../shared/charts';
import { DistanceUnit, distanceIn, formatDistance, formatDuration } from '../shared/format';
import { Units } from '../shared/units';
import { RunDetail, RunTemperature, Split } from './run';
import { RunMap } from './run-map';
import { ReplayStretch } from './replay';

@Component({
  selector: 'app-run-detail-page',
  imports: [DatePipe, DecimalPipe, RouterLink, NgxEchartsDirective, RunMap],
  templateUrl: './run-detail-page.html',
  styleUrl: './run-detail-page.scss',
})
export class RunDetailPage {
  /** From the route, runs/:id. */
  readonly id = input.required<string>();

  private readonly theme = inject(ChartTheme);

  protected readonly run = httpResource<RunDetail>(() => `/api/runs/${this.id()}`);
  /** Looked up on Open-Meteo the first time, so it may come after the Run. */
  protected readonly temperature = httpResource<RunTemperature>(() => `/api/runs/${this.id()}/temperature`);
  /** °C; none while loading, when it could not be loaded, or for a Run without one. */
  protected readonly celsius = computed(() => (this.temperature.hasValue() ? this.temperature.value().temperature : null));
  protected readonly formatDuration = formatDuration;
  protected readonly formatEfficiency = formatEfficiency;
  protected readonly formatCadence = formatCadence;
  protected readonly formatDrift = formatDrift;
  /** Distances, Pace and Splits in the Distance Unit; elevation stays in metres. */
  protected readonly units = inject(Units);

  /** The Splits per km, or per mile when the Distance Unit is miles and Strava has them; `unit` is what each one is. */
  protected readonly splits = computed<{ unit: DistanceUnit; splits: Split[] }>(() => {
    if (!this.run.hasValue()) return { unit: 'km', splits: [] };
    const run = this.run.value();
    return this.units.unit() === 'mi' && run.mileSplits.length > 0 ? { unit: 'mi', splits: run.mileSplits } : { unit: 'km', splits: run.splits };
  });

  /** What the map's replay moves at: each km Split's pace, or the Run's average while it has no Splits. */
  protected readonly replayStretches = computed<ReplayStretch[]>(() => {
    if (!this.run.hasValue()) return [];
    const run = this.run.value();
    return run.splits.length ? run.splits : [run];
  });

  /**
   * How many facts go in a row, at most `max`, spread over as few rows as evenly as can be: 11 facts at most 6 a
   * row are 6 and 5. The CSS stretches each row across the whole width. Counts the facts the template shows.
   */
  protected factsPerRow(run: RunDetail, max: number): number {
    const facts = 9 +(run.fitness ? 2 + (run.fitness.cardiacDrift !== null ? 1 : 0) : 0);
    return Math.ceil(facts / Math.ceil(facts / max));
  }

  protected readonly splitPace = computed(() => {
    if (!this.run.hasValue()) return null;
    const { unit, splits } = this.splits();
    // The few metres left over at the end have a Pace of noise that would stretch the axis; the table still shows them.
    const charted = splits.filter((s) => distanceIn(s.distance, unit) >= minChartedSplit);
    if (!charted.some((s) => s.pace !== null)) return null;
    return splitPaceChart(charted, unit, this.run.value().pace, this.units.unit(), this.theme.colors());
  });
}

/** The shortest Split the Pace chart shows, in km or miles as the Splits are. */
const minChartedSplit = 0.1;

/** Pace per Split, as in the Overview's Pace chart; a dashed line marks the Run's own Pace. */
function splitPaceChart(splits: Split[], splitUnit: DistanceUnit, runPace: number | null, unit: DistanceUnit, colors: ChartColors): EChartsCoreOption {
  return paceChart(
    {
      labels: splits.map((s) => String(s.index)),
      paces: splits.map((s) => s.pace),
      // A single Split would be no line at all, so it shows as a dot.
      showSymbol: splits.length === 1,
      average: runPace,
      tooltip: (index) => {
        const split = splits[index];
        if (split.pace === null) return tooltipHtml(colors, `${splitUnit} ${split.index}`, 'no Pace');
        return tooltipHtml(colors, `${splitUnit} ${split.index}`, paceLabel(split.pace, unit), `/${unit}`, `${formatDistance(split.distance, unit)} in ${formatDuration(split.movingTime)}`);
      },
    },
    unit,
    colors,
  );
}

import { DatePipe, DecimalPipe } from '@angular/common';
import { httpResource } from '@angular/common/http';
import { Component, computed, inject, input } from '@angular/core';
import { RouterLink } from '@angular/router';
import type { EChartsCoreOption } from 'echarts/core';
import { NgxEchartsDirective } from 'ngx-echarts';
import { ChartColors, ChartTheme, baseChartOptions, tooltipHtml } from '../shared/chart-theme';
import { categoryAxis, valueAxis } from '../shared/charts';
import { DistanceUnit, formatDistance, formatDuration, formatPace } from '../shared/format';
import { Units } from '../shared/units';
import { RunDetail, Split } from './run';
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
  protected readonly formatDuration = formatDuration;
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

  protected readonly splitPace = computed(() => {
    if (!this.run.hasValue()) return null;
    const { unit, splits } = this.splits();
    if (!splits.some((s) => s.pace !== null)) return null;
    return splitPaceChart(splits, unit, this.run.value().pace, this.units.unit(), this.theme.colors());
  });
}

/** Faster is up, as in the Overview's Pace chart; a dashed line marks the Run's own Pace. */
function splitPaceChart(splits: Split[], splitUnit: DistanceUnit, runPace: number | null, unit: DistanceUnit, colors: ChartColors): EChartsCoreOption {
  const base = baseChartOptions(colors);
  const paceLabel = (seconds: number) => formatPace(seconds, unit).replace(` /${unit}`, '');
  return {
    ...base,
    tooltip: {
      ...base.tooltip,
      axisPointer: { type: 'line', lineStyle: { color: colors.tooltipBorder, type: 'solid' } },
      formatter: (params: { dataIndex: number }[]) => {
        const split = splits[params[0]?.dataIndex];
        if (!split) return '';
        if (split.pace === null) return tooltipHtml(colors, `${splitUnit} ${split.index}`, 'no Pace');
        return tooltipHtml(colors, `${splitUnit} ${split.index}`, paceLabel(split.pace), `/${unit}`, `${formatDistance(split.distance, unit)} in ${formatDuration(split.movingTime)}`);
      },
    },
    xAxis: { ...categoryAxis(splits.map((s) => String(s.index)), colors), boundaryGap: false },
    yAxis: { ...valueAxis(colors, paceLabel, false), inverse: true },
    series: [
      {
        type: 'line',
        data: splits.map((s) => (s.pace === null ? null : Math.round(s.pace))),
        connectNulls: false,
        smooth: false,
        lineStyle: { width: 2, color: colors.series, join: 'round' },
        itemStyle: { color: colors.background, borderColor: colors.seriesLight, borderWidth: 2 },
        symbol: 'circle',
        symbolSize: 9,
        showSymbol: splits.length === 1,
        areaStyle: {
          origin: 'end',
          color: {
            type: 'linear',
            x: 0,
            y: 0,
            x2: 0,
            y2: 1,
            colorStops: [
              { offset: 0, color: 'rgba(74,158,255,0.22)' },
              { offset: 1, color: 'rgba(74,158,255,0)' },
            ],
          },
        },
        markLine:
          runPace === null
            ? undefined
            : {
                silent: true,
                symbol: 'none',
                lineStyle: { color: colors.muted, type: 'dashed', width: 1 },
                label: { color: colors.muted, formatter: () => `avg ${paceLabel(runPace)}`, position: 'insideEndTop' },
                data: [{ yAxis: Math.round(runPace) }],
              },
      },
    ],
  };
}

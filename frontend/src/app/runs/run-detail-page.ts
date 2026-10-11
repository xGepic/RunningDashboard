import { DatePipe, DecimalPipe } from '@angular/common';
import { httpResource } from '@angular/common/http';
import { Component, computed, inject, input } from '@angular/core';
import { RouterLink } from '@angular/router';
import type { EChartsCoreOption } from 'echarts/core';
import { NgxEchartsDirective } from 'ngx-echarts';
import { ChartColors, ChartTheme, baseChartOptions, tooltipHtml } from '../shared/chart-theme';
import { categoryAxis, valueAxis } from '../shared/charts';
import { formatDistance, formatDuration, formatPace } from '../shared/format';
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
  protected readonly formatDistance = formatDistance;
  protected readonly formatDuration = formatDuration;
  protected readonly formatPace = formatPace;

  /** What the map's replay moves at: each Split's pace, or the Run's average while it has no Splits. */
  protected readonly replayStretches = computed<ReplayStretch[]>(() => {
    if (!this.run.hasValue()) return [];
    const run = this.run.value();
    return run.splits.length ? run.splits : [run];
  });

  protected readonly splitPace = computed(() => {
    if (!this.run.hasValue()) return null;
    const run = this.run.value();
    if (!run.splits.some((s) => s.pace !== null)) return null;
    return splitPaceChart(run.splits, run.pace, this.theme.colors());
  });
}

/** Faster is up, as in the Overview's Pace chart; a dashed line marks the Run's own Pace. */
function splitPaceChart(splits: Split[], runPace: number | null, colors: ChartColors): EChartsCoreOption {
  const base = baseChartOptions(colors);
  const paceLabel = (seconds: number) => formatPace(seconds).replace(' /km', '');
  return {
    ...base,
    tooltip: {
      ...base.tooltip,
      axisPointer: { type: 'line', lineStyle: { color: colors.tooltipBorder, type: 'solid' } },
      formatter: (params: { dataIndex: number }[]) => {
        const split = splits[params[0]?.dataIndex];
        if (!split) return '';
        if (split.pace === null) return tooltipHtml(colors, `km ${split.index}`, 'no Pace');
        return tooltipHtml(colors, `km ${split.index}`, paceLabel(split.pace), '/km', `${formatDistance(split.distance)} in ${formatDuration(split.movingTime)}`);
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

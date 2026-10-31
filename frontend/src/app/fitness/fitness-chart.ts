import type { EChartsCoreOption } from 'echarts/core';
import { Break, breaksWithin, kindLabel } from '../shared/breaks';
import { ChartColors, baseChartOptions, escapeHtml, tooltipHtml } from '../shared/chart-theme';
import { withAlpha } from '../shared/charts';
import { addDays, fullMonthNames, isoDate, localDate } from '../shared/dates';

/** One figure over time: a dot per Run and a line through the monthly averages. */
export interface FitnessChartData {
  runs: { startLocal: string; name: string; value: number }[];
  months: { year: number; month: number; value: number }[];
  /** "168 spm" from a value as charted. */
  format: (value: number) => string;
  /** Shaded over their days and named by their kind, those within the Runs' dates. */
  breaks?: readonly Break[];
}

interface Point {
  value: [string, number];
  title: string;
}

/**
 * Dots per Run, faint, so the monthly-average line in the series colour reads as the trend. One measure, one axis;
 * a dot's tooltip names its Run, a point of the line its month.
 */
export function fitnessChart(data: FitnessChartData, colors: ChartColors): EChartsCoreOption {
  const base = baseChartOptions(colors);
  const runs: Point[] = data.runs.map((run) => ({
    value: [run.startLocal, run.value],
    title: `${escapeHtml(run.name)} · ${new Date(run.startLocal).toLocaleDateString('en-GB', { day: 'numeric', month: 'short', year: 'numeric' })}`,
  }));
  // Mid-month, so the line sits among its month's dots.
  const months: Point[] = data.months.map((m) => ({
    value: [`${m.year}-${String(m.month).padStart(2, '0')}-15`, m.value],
    title: `${fullMonthNames[m.month - 1]} ${m.year} · average`,
  }));
  return {
    ...base,
    tooltip: {
      ...base.tooltip,
      trigger: 'item',
      formatter: (params: { data: Point }) => tooltipHtml(colors, params.data.title, data.format(params.data.value[1])),
    },
    xAxis: {
      type: 'time',
      axisLine: { show: false },
      axisTick: { show: false },
      axisLabel: { color: colors.faint, hideOverlap: true },
      splitLine: { show: false },
    },
    yAxis: {
      type: 'value',
      scale: true,
      splitNumber: 4,
      axisLabel: { color: colors.faint, formatter: data.format },
      splitLine: { lineStyle: { color: colors.grid, width: 1, type: 'dashed' } },
    },
    series: [
      {
        name: 'Runs',
        type: 'scatter',
        data: runs,
        symbolSize: 8,
        itemStyle: { color: withAlpha(colors.series, 0.35), borderColor: colors.surface, borderWidth: 1 },
        emphasis: { itemStyle: { color: colors.seriesLight } },
      },
      {
        name: 'Monthly average',
        type: 'line',
        z: 3,
        data: months,
        lineStyle: { width: 2, color: colors.series, join: 'round' },
        itemStyle: { color: colors.background, borderColor: colors.series, borderWidth: 2 },
        symbol: 'circle',
        symbolSize: 8,
        animationDuration: 700,
        animationEasing: 'cubicOut',
        ...breakBands(data, colors),
      },
    ],
  };
}

/** Each Break within the Runs' dates as a band from its first day to the end of its last, named by its kind. */
function breakBands(data: FitnessChartData, colors: ChartColors) {
  const days = data.runs.map((run) => run.startLocal.slice(0, 10)).sort();
  if (!data.breaks?.length || days.length === 0) return {};
  const shown = breaksWithin(data.breaks, days[0], days[days.length - 1]);
  if (shown.length === 0) return {};
  return {
    markArea: {
      silent: true,
      label: { show: true, position: 'insideTop', fontSize: 10.5 },
      data: shown.map((period) => {
        const color = colors.breaks[period.kind];
        return [
          { xAxis: period.start, name: kindLabel(period.kind), itemStyle: { color: withAlpha(color, 0.1) }, label: { color } },
          { xAxis: isoDate(addDays(localDate(period.end), 1)) },
        ];
      }),
    },
  };
}

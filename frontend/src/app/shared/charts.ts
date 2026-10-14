import type { EChartsCoreOption } from 'echarts/core';
import { ChartColors, baseChartOptions, tooltipHtml } from './chart-theme';
import { fullMonthNames, monthNames } from './dates';
import { DistanceUnit, distanceIn, formatPace } from './format';

/** Distance (metres) run in the Training Week starting on the Monday <code>start</code> (yyyy-MM-dd). */
export interface WeekDistance {
  start: string;
  distance: number;
}

/** "5 Oct 2026" for the Training Week starting Monday 2026-10-05. */
export function weekLabel(start: string): string {
  const [year, month, day] = start.split('-').map(Number);
  return `${day} ${monthNames[month - 1]} ${year}`;
}

/** "5 Oct" for the Training Week starting Monday 2026-10-05. */
export function shortWeekLabel(start: string): string {
  const [, month, day] = start.split('-').map(Number);
  return `${day} ${monthNames[month - 1]}`;
}

/** A #rrggbb colour token at an opacity, e.g. "rgba(74,158,255,0.22)". */
export function withAlpha(hex: string, alpha: number): string {
  const [red, green, blue] = [1, 3, 5].map((i) => parseInt(hex.slice(i, i + 2), 16));
  return `rgba(${red},${green},${blue},${alpha})`;
}

/** A top-to-bottom gradient between two colours. */
function verticalGradient(top: string, bottom: string) {
  return {
    type: 'linear',
    x: 0,
    y: 0,
    x2: 0,
    y2: 1,
    colorStops: [
      { offset: 0, color: top },
      { offset: 1, color: bottom },
    ],
  };
}

/** The fill under a line: the series colour, fading out towards the bottom. */
export function areaFill(colors: ChartColors) {
  return verticalGradient(withAlpha(colors.series, 0.22), withAlpha(colors.series, 0));
}

export function categoryAxis(labels: string[], colors: ChartColors) {
  return {
    type: 'category',
    data: labels,
    axisLine: { show: false },
    axisTick: { show: false },
    axisLabel: { color: colors.faint, hideOverlap: true, margin: 12 },
  };
}

export function valueAxis(colors: ChartColors, formatter: (value: number) => string, fromZero: boolean) {
  return {
    type: 'value',
    scale: !fromZero,
    axisLabel: { color: colors.faint, formatter },
    splitLine: { lineStyle: { color: colors.grid, width: 1, type: 'dashed' } },
  };
}

/** Column chart of distance per Training Week, in kilometres or miles; a column can be clicked. */
export function weeklyDistanceChart(weeks: WeekDistance[], unit: DistanceUnit, colors: ChartColors): EChartsCoreOption {
  const base = baseChartOptions(colors);
  return {
    ...base,
    tooltip: {
      ...base.tooltip,
      axisPointer: { type: 'shadow', shadowStyle: { color: withAlpha(colors.series, 0.06) } },
      formatter: (params: { dataIndex: number }[]) => {
        const week = weeks[params[0]?.dataIndex];
        if (!week) return '';
        return tooltipHtml(colors, `Week of ${weekLabel(week.start)}`, distanceIn(week.distance, unit).toFixed(1), unit);
      },
    },
    xAxis: categoryAxis(weeks.map((w) => shortWeekLabel(w.start)), colors),
    yAxis: valueAxis(colors, (value) => `${value} ${unit}`, true),
    series: [
      {
        type: 'bar',
        data: weeks.map((w) => Math.round(distanceIn(w.distance, unit) * 10) / 10),
        barMaxWidth: weeks.length > 12 ? 24 : 56,
        barCategoryGap: weeks.length > 12 ? '18%' : '28%',
        itemStyle: { color: verticalGradient(colors.barTop, colors.seriesDeep), borderRadius: [3, 3, 1, 1] },
        emphasis: { itemStyle: { color: colors.barHover } },
        cursor: 'pointer',
        animationDuration: 700,
        animationEasing: 'cubicOut',
      },
    ],
  };
}

/** Distance (metres) and Runs in one calendar month; `month` is 1 for January. */
export interface MonthDistance {
  month: number;
  runs: number;
  distance: number;
  /** The current month, still running: charted, but left out of the trend. */
  unfinished?: boolean;
  /** Still ahead: on the axis, without a distance. */
  future?: boolean;
}

/**
 * The least-squares straight line through `values` (at 0, 1, 2, …), read off at each of `count` positions; none
 * with fewer than two values.
 */
function linearTrend(values: number[], count: number): number[] | null {
  if (values.length < 2) return null;
  const meanX = (values.length - 1) / 2;
  const meanY = values.reduce((sum, y) => sum + y, 0) / values.length;
  const slope =
    values.reduce((sum, y, x) => sum + (x - meanX) * (y - meanY), 0) / values.reduce((sum, _, x) => sum + (x - meanX) ** 2, 0);
  return Array.from({ length: count }, (_, x) => meanY + slope * (x - meanX));
}

/**
 * Line chart of distance per calendar month of `year`, in kilometres or miles; months without Runs sit at zero, months
 * still ahead have no point. A dashed trend, fitted to the finished months only, runs across all of them.
 */
export function monthlyDistanceChart(year: number, months: MonthDistance[], unit: DistanceUnit, colors: ChartColors): EChartsCoreOption {
  const base = baseChartOptions(colors);
  const values = months.map((m) => (m.future ? null : Math.round(distanceIn(m.distance, unit) * 10) / 10));
  // Months run from January, so the finished ones come first, then the unfinished one and those still ahead.
  const finished = values.filter((value, i): value is number => value !== null && !months[i].unfinished);
  const trend = linearTrend(finished, months.length);
  return {
    ...base,
    tooltip: {
      ...base.tooltip,
      axisPointer: { type: 'line', lineStyle: { color: colors.tooltipBorder, type: 'solid' } },
      formatter: (params: { dataIndex: number }[]) => {
        const month = months[params[0]?.dataIndex];
        if (!month) return '';
        const title = `${fullMonthNames[month.month - 1]} ${year}`;
        if (month.future) return tooltipHtml(colors, title, 'still to come');
        if (month.runs === 0) return tooltipHtml(colors, title, 'no Runs');
        return tooltipHtml(colors, title, distanceIn(month.distance, unit).toFixed(1), unit, `${month.runs} ${month.runs === 1 ? 'Run' : 'Runs'}`);
      },
    },
    xAxis: { ...categoryAxis(months.map((m) => monthNames[m.month - 1]), colors), boundaryGap: false },
    yAxis: (() => {
      // The first point sits on the axis: keep the labels clear of it.
      const axis = valueAxis(colors, (value) => `${value} ${unit}`, true);
      // A falling trend may head below zero; no month can.
      return { ...axis, min: 0, axisLabel: { ...axis.axisLabel, margin: 16 } };
    })(),
    series: [
      {
        name: 'Distance',
        type: 'line',
        z: 3,
        data: values,
        smooth: false,
        lineStyle: { width: 2, color: colors.series, join: 'round' },
        // A point per month, so there is something to hover and click even for a single month.
        itemStyle: { color: colors.background, borderColor: colors.seriesLight, borderWidth: 2 },
        symbol: 'circle',
        symbolSize: 9,
        emphasis: { scale: 1.4 },
        cursor: 'pointer',
        areaStyle: { color: areaFill(colors) },
        animationDuration: 700,
        animationEasing: 'cubicOut',
      },
      ...(trend
        ? [
            {
              name: 'Trend',
              type: 'line',
              z: 2,
              data: trend.map((value) => Math.round(value * 10) / 10),
              symbol: 'none',
              silent: true,
              lineStyle: { width: 1.5, type: 'dashed', color: colors.muted },
              emphasis: { disabled: true },
              animationDuration: 700,
            },
          ]
        : []),
    ],
  };
}

/** Pace as "4:58", per km or mile, without the unit: for axis labels and tooltips that show the unit apart. */
export function paceLabel(secondsPerKm: number, unit: DistanceUnit): string {
  return formatPace(secondsPerKm, unit).replace(` /${unit}`, '');
}

/** What a pace chart shows: a Pace (seconds per km) per label, none where there is a gap. */
export interface PaceChartData {
  labels: string[];
  paces: (number | null)[];
  /** The tooltip of the point at this index. */
  tooltip: (index: number) => string;
  /** Whether each point shows a dot, or only the one under the pointer. */
  showSymbol: boolean;
  /** A dashed line at this Pace, labelled as the average; none without it. */
  average?: number | null;
}

/** Line chart of Pace per km or mile, filled below. Faster is up: the y axis runs from the slowest Pace to the fastest. */
export function paceChart(data: PaceChartData, unit: DistanceUnit, colors: ChartColors): EChartsCoreOption {
  const base = baseChartOptions(colors);
  const label = (seconds: number) => paceLabel(seconds, unit);
  const average = data.average ?? null;
  return {
    ...base,
    tooltip: {
      ...base.tooltip,
      axisPointer: { type: 'line', lineStyle: { color: colors.tooltipBorder, type: 'solid' } },
      formatter: (params: { dataIndex: number }[]) => {
        const index = params[0]?.dataIndex;
        return index === undefined || index >= data.labels.length ? '' : data.tooltip(index);
      },
    },
    xAxis: { ...categoryAxis(data.labels, colors), boundaryGap: false },
    yAxis: { ...valueAxis(colors, label, false), inverse: true },
    series: [
      {
        type: 'line',
        data: data.paces.map((pace) => (pace === null ? null : Math.round(pace))),
        connectNulls: false,
        smooth: false,
        lineStyle: { width: 2, color: colors.series, join: 'round' },
        itemStyle: { color: colors.background, borderColor: colors.seriesLight, borderWidth: 2 },
        symbol: 'circle',
        symbolSize: 9,
        showSymbol: data.showSymbol,
        areaStyle: { origin: 'end', color: areaFill(colors) },
        markLine:
          average === null
            ? undefined
            : {
                silent: true,
                symbol: 'none',
                lineStyle: { color: colors.muted, type: 'dashed', width: 1 },
                label: { color: colors.muted, formatter: () => `avg ${label(average)}`, position: 'insideEndTop' },
                data: [{ yAxis: Math.round(average) }],
              },
      },
    ],
  };
}

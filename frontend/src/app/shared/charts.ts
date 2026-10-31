import type { EChartsCoreOption } from 'echarts/core';
import { Break, breakLabel, breaksInWeek, kindLabel } from './breaks';
import { ChartColors, baseChartOptions, escapeHtml, tooltipHtml } from './chart-theme';
import { fullMonthNames, monthNames } from './dates';
import { DistanceUnit, distanceIn, formatHoursMinutes, formatPace, runCount } from './format';

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

/** Metres in kilometres or miles to one decimal, as the charts plot them. */
function chartDistance(metres: number, unit: DistanceUnit): number {
  return Math.round(distanceIn(metres, unit) * 10) / 10;
}

/** A column chart's tooltip pointer: a faint band over the column hovered. */
function columnPointer(colors: ChartColors) {
  return { type: 'shadow', shadowStyle: { color: withAlpha(colors.series, 0.06) } };
}

/** Columns per Training Week: narrower and closer together past a quarter's worth of weeks. */
function weekColumns(weekCount: number) {
  return { barMaxWidth: weekCount > 12 ? 24 : 56, barCategoryGap: weekCount > 12 ? '18%' : '28%' };
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

/**
 * A column chart's shading of each Break over the Training Weeks it has days in, named by its kind; for its first
 * series. Nothing without a Break among `weeks`.
 */
export function breakBands(weeks: readonly { start: string }[], breaks: readonly Break[], colors: ChartColors) {
  const data = breaks.flatMap((period) => {
    const indexes = weeks.flatMap((week, index) => (breaksInWeek([period], week.start).length ? [index] : []));
    if (indexes.length === 0) return [];
    const color = colors.breaks[period.kind];
    return [[{ xAxis: indexes[0], name: kindLabel(period.kind), itemStyle: { color: withAlpha(color, 0.1) }, label: { color } }, { xAxis: indexes.at(-1) }]];
  });
  return data.length ? { markArea: { silent: true, label: { show: true, position: 'insideTop', fontSize: 10.5 }, data } } : {};
}

/** A week's tooltip lines for the Breaks it has days in, each in its kind's colour. */
export function breakTooltip(start: string, breaks: readonly Break[], colors: ChartColors): string {
  return breaksInWeek(breaks, start)
    .map((period) => `<div style="font-size:11px;color:${colors.breaks[period.kind]};margin-top:4px">${escapeHtml(breakLabel(period))}</div>`)
    .join('');
}

/**
 * Column chart of distance per Training Week, in kilometres or miles; a column can be clicked. The weeks of a Break are
 * shaded and named.
 */
export function weeklyDistanceChart(weeks: WeekDistance[], unit: DistanceUnit, colors: ChartColors, breaks: readonly Break[] = []): EChartsCoreOption {
  const base = baseChartOptions(colors);
  return {
    ...base,
    tooltip: {
      ...base.tooltip,
      axisPointer: columnPointer(colors),
      formatter: (params: { dataIndex: number }[]) => {
        const week = weeks[params[0]?.dataIndex];
        if (!week) return '';
        return tooltipHtml(colors, `Week of ${weekLabel(week.start)}`, distanceIn(week.distance, unit).toFixed(1), unit) + breakTooltip(week.start, breaks, colors);
      },
    },
    xAxis: categoryAxis(weeks.map((w) => shortWeekLabel(w.start)), colors),
    yAxis: valueAxis(colors, (value) => `${value} ${unit}`, true),
    series: [
      {
        type: 'bar',
        data: weeks.map((w) => chartDistance(w.distance, unit)),
        ...weekColumns(weeks.length),
        itemStyle: { color: verticalGradient(colors.barTop, colors.seriesDeep), borderRadius: [3, 3, 1, 1] },
        emphasis: { itemStyle: { color: colors.barHover } },
        cursor: 'pointer',
        animationDuration: 700,
        animationEasing: 'cubicOut',
        ...breakBands(weeks, breaks, colors),
      },
    ],
  };
}

/** Training Time: Moving Time (seconds) of the Runs, Rides and Swims in the Training Week starting on the Monday `start`. */
export interface TrainingTimeWeek {
  start: string;
  runs: number;
  rides: number;
  swims: number;
}

/** The sports of Training Time, bottom to top of a column. */
export const trainingTimeSports = [
  { key: 'runs', label: 'Runs', color: (colors: ChartColors) => colors.series },
  { key: 'rides', label: 'Rides', color: (colors: ChartColors) => colors.ride },
  { key: 'swims', label: 'Swims', color: (colors: ChartColors) => colors.swim },
] as const;

/** All of a week's Training Time, in seconds. */
export function trainingTimeOf(week: TrainingTimeWeek): number {
  return week.runs + week.rides + week.swims;
}

/** A Weekly Time Goal: "7h", or "7h 30m" for one that is not whole hours. */
export function goalTimeLabel(seconds: number): string {
  return seconds % 3600 === 0 ? `${seconds / 3600}h` : formatHoursMinutes(seconds);
}

/** "1h 06m", or "–" for nothing. */
export function trainingTimeLabel(seconds: number): string {
  return seconds > 0 ? formatHoursMinutes(seconds) : '–';
}

/**
 * Stacked columns of Training Time per Training Week, in hours: Runs at the bottom in the Theme's colour, then Rides and
 * Swims. Only a column's top segment is rounded, and a hairline in the surface colour parts the segments. The weeks of
 * a Break are shaded and named, and a dashed line marks the Weekly Time Goal (seconds) when there is one.
 */
export function trainingTimeChart(
  weeks: TrainingTimeWeek[],
  colors: ChartColors,
  breaks: readonly Break[] = [],
  goal: number | null = null,
): EChartsCoreOption {
  const base = baseChartOptions(colors);
  // The index of each week's topmost sport with time; -1 for an empty week.
  const topSport = weeks.map((w) => trainingTimeSports.reduce((top, s, index) => (w[s.key] > 0 ? index : top), -1));
  return {
    ...base,
    tooltip: {
      ...base.tooltip,
      axisPointer: columnPointer(colors),
      formatter: (params: { dataIndex: number }[]) => {
        const week = weeks[params[0]?.dataIndex];
        if (!week) return '';
        // The sports top to bottom, as the column shows them.
        const rows = [...trainingTimeSports]
          .reverse()
          .map(
            (s) =>
              `<div style="display:flex;align-items:center;gap:6px;font-size:11px;color:${colors.muted};margin-top:3px">` +
              `<span style="width:8px;height:8px;border-radius:2px;background:${s.color(colors)}"></span>${s.label}` +
              `<span style="margin-left:auto;padding-left:14px;font-family:${colors.fontMono};color:${colors.text}">${trainingTimeLabel(week[s.key])}</span></div>`,
          )
          .join('');
        return tooltipHtml(colors, `Week of ${weekLabel(week.start)}`, trainingTimeLabel(trainingTimeOf(week))) + rows + breakTooltip(week.start, breaks, colors);
      },
    },
    xAxis: categoryAxis(weeks.map((w) => shortWeekLabel(w.start)), colors),
    yAxis: valueAxis(colors, (value) => `${value} h`, true),
    series: trainingTimeSports.map((sport, index) => ({
      type: 'bar',
      name: sport.label,
      stack: 'time',
      data: weeks.map((w, i) => ({
        value: Math.round((w[sport.key] / 3600) * 100) / 100,
        itemStyle: { borderRadius: topSport[i] === index ? [3, 3, 0, 0] : 0 },
      })),
      ...weekColumns(weeks.length),
      itemStyle: { color: sport.color(colors), borderColor: colors.surface, borderWidth: 1 },
      emphasis: { focus: 'series' },
      animationDuration: 700,
      animationEasing: 'cubicOut',
      ...(index === 0 ? { ...breakBands(weeks, breaks, colors), ...goalLine(goal, colors) } : {}),
    })),
  };
}

/** A dashed line across a Training Time chart at the Weekly Time Goal, in hours; nothing without a goal. */
function goalLine(goal: number | null, colors: ChartColors) {
  if (goal === null) return {};
  return {
    markLine: {
      silent: true,
      symbol: 'none',
      lineStyle: { color: colors.muted, type: 'dashed', width: 1 },
      label: { position: 'insideEndTop', color: colors.muted, fontSize: 10.5, formatter: `goal ${goalTimeLabel(goal)}` },
      data: [{ yAxis: Math.round((goal / 3600) * 100) / 100 }],
    },
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
  const values = months.map((m) => (m.future ? null : chartDistance(m.distance, unit)));
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
        return tooltipHtml(colors, title, distanceIn(month.distance, unit).toFixed(1), unit, runCount(month.runs));
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

/** Distance (metres) and number of Runs of a year on one day of the week, e.g. "Monday". */
export interface WeekdayDistance {
  name: string;
  runs: number;
  distance: number;
}

/**
 * Column chart of a year's distance per day of the week, Monday to Sunday, in kilometres or miles. The day with the
 * most stands out, labelled with its share of the year's distance.
 */
export function weekdayDistanceChart(days: WeekdayDistance[], unit: DistanceUnit, colors: ChartColors): EChartsCoreOption {
  const base = baseChartOptions(colors);
  const total = days.reduce((sum, d) => sum + d.distance, 0);
  const most = Math.max(...days.map((d) => d.distance));
  const share = (distance: number) => `${Math.round((100 * distance) / total)}%`;
  return {
    ...base,
    tooltip: {
      ...base.tooltip,
      axisPointer: columnPointer(colors),
      formatter: (params: { dataIndex: number }[]) => {
        const day = days[params[0]?.dataIndex];
        if (!day) return '';
        if (day.runs === 0) return tooltipHtml(colors, day.name, 'no Runs');
        const runs = `${runCount(day.runs)} · ${share(day.distance)} of the year's distance`;
        return tooltipHtml(colors, day.name, distanceIn(day.distance, unit).toFixed(1), unit, runs);
      },
    },
    xAxis: categoryAxis(days.map((d) => d.name.slice(0, 3)), colors),
    yAxis: valueAxis(colors, (value) => `${value} ${unit}`, true),
    series: [
      {
        type: 'bar',
        data: days.map((d) => {
          const value = chartDistance(d.distance, unit);
          if (d.distance === most && most > 0) return { value, label: { show: true, position: 'top', color: colors.text, formatter: share(d.distance) } };
          // The other days recede, so the one with the most is read first.
          return { value, itemStyle: { color: verticalGradient(withAlpha(colors.barTop, 0.45), withAlpha(colors.seriesDeep, 0.45)) } };
        }),
        barMaxWidth: 56,
        barCategoryGap: '28%',
        itemStyle: { color: verticalGradient(colors.barTop, colors.seriesDeep), borderRadius: [3, 3, 1, 1] },
        emphasis: { itemStyle: { color: colors.barHover } },
        animationDuration: 700,
        animationEasing: 'cubicOut',
      },
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

/** Steps between Pace ticks, in seconds per km or mile: ticks land on round paces like 5:00, 5:15, 5:30. */
const paceSteps = [5, 10, 15, 20, 30, 60, 120, 300, 600];

/**
 * The Pace axis (seconds per km) around these Paces: the finest step of `paceSteps` that needs at most `maxTicks`
 * ticks, ends rounded out to it, all in the unit shown so a mile axis ticks on round paces per mile too. None
 * without a Pace, or when even the coarsest step needs more ticks; ECharts then picks its own.
 */
export function paceAxis(paces: number[], unit: DistanceUnit, maxTicks = 6): { min: number; max: number; interval: number } | null {
  if (paces.length === 0) return null;
  const perUnit = 1 / distanceIn(1000, unit);
  const fastest = Math.min(...paces) * perUnit;
  const slowest = Math.max(...paces) * perUnit;
  for (const step of paceSteps) {
    let min = Math.floor(fastest / step) * step;
    let max = Math.ceil(slowest / step) * step;
    // One Pace alone, on a tick: give it a step either side rather than an axis of no height.
    if (min === max) [min, max] = [min - step, max + step];
    if ((max - min) / step + 1 <= maxTicks) return { min: min / perUnit, max: max / perUnit, interval: step / perUnit };
  }
  return null;
}

/** Line chart of Pace per km or mile, filled below. Faster is up: the y axis runs from the slowest Pace to the fastest. */
export function paceChart(data: PaceChartData, unit: DistanceUnit, colors: ChartColors): EChartsCoreOption {
  const base = baseChartOptions(colors);
  const label = (seconds: number) => paceLabel(seconds, unit);
  const average = data.average ?? null;
  const axis = paceAxis([...data.paces, average].filter((pace) => pace !== null), unit);
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
    yAxis: { ...valueAxis(colors, label, false), inverse: true, ...axis },
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

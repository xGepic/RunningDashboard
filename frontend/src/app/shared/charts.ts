import type { EChartsCoreOption } from 'echarts/core';
import { ChartColors, baseChartOptions, tooltipHtml } from './chart-theme';
import { fullMonthNames } from './dates';
import { DistanceUnit, distanceIn } from './format';

export const monthNames = ['Jan', 'Feb', 'Mar', 'Apr', 'May', 'Jun', 'Jul', 'Aug', 'Sep', 'Oct', 'Nov', 'Dec'];

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

/** A top-to-bottom gradient of the series colour, as the design's bars use. */
export function barGradient(colors: ChartColors) {
  return {
    type: 'linear',
    x: 0,
    y: 0,
    x2: 0,
    y2: 1,
    colorStops: [
      { offset: 0, color: '#5aa9ff' },
      { offset: 1, color: colors.seriesDeep },
    ],
  };
}

/** Column chart of distance per Training Week, in km. */
export function weeklyDistanceChart(weeks: WeekDistance[], colors: ChartColors): EChartsCoreOption {
  const base = baseChartOptions(colors);
  return {
    ...base,
    tooltip: {
      ...base.tooltip,
      axisPointer: { type: 'shadow', shadowStyle: { color: 'rgba(74,158,255,0.06)' } },
      formatter: (params: { dataIndex: number }[]) => {
        const week = weeks[params[0]?.dataIndex];
        if (!week) return '';
        return tooltipHtml(colors, `Week of ${weekLabel(week.start)}`, (week.distance / 1000).toFixed(1), 'km');
      },
    },
    xAxis: categoryAxis(weeks.map((w) => shortWeekLabel(w.start)), colors),
    yAxis: valueAxis(colors, (km) => `${km} km`, true),
    series: [
      {
        type: 'bar',
        data: weeks.map((w) => Math.round(w.distance / 100) / 10),
        barMaxWidth: weeks.length > 12 ? 24 : 56,
        barCategoryGap: weeks.length > 12 ? '18%' : '28%',
        itemStyle: { color: barGradient(colors), borderRadius: [3, 3, 1, 1] },
        emphasis: { itemStyle: { color: '#8cc4ff' } },
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
}

/** Line chart of distance per calendar month of `year`, in kilometres or miles; months without Runs sit at zero. */
export function monthlyDistanceChart(year: number, months: MonthDistance[], unit: DistanceUnit, colors: ChartColors): EChartsCoreOption {
  const base = baseChartOptions(colors);
  return {
    ...base,
    tooltip: {
      ...base.tooltip,
      axisPointer: { type: 'line', lineStyle: { color: colors.tooltipBorder, type: 'solid' } },
      formatter: (params: { dataIndex: number }[]) => {
        const month = months[params[0]?.dataIndex];
        if (!month) return '';
        const title = `${fullMonthNames[month.month - 1]} ${year}`;
        if (month.runs === 0) return tooltipHtml(colors, title, 'no Runs');
        return tooltipHtml(colors, title, distanceIn(month.distance, unit).toFixed(1), unit, `${month.runs} ${month.runs === 1 ? 'Run' : 'Runs'}`);
      },
    },
    xAxis: { ...categoryAxis(months.map((m) => monthNames[m.month - 1]), colors), boundaryGap: false },
    yAxis: (() => {
      // The first point sits on the axis: keep the labels clear of it.
      const axis = valueAxis(colors, (value) => `${value} ${unit}`, true);
      return { ...axis, axisLabel: { ...axis.axisLabel, margin: 16 } };
    })(),
    series: [
      {
        type: 'line',
        data: months.map((m) => Math.round(distanceIn(m.distance, unit) * 10) / 10),
        smooth: false,
        lineStyle: { width: 2, color: colors.series, join: 'round' },
        // A point per month, so there is something to hover and click even for a single month.
        itemStyle: { color: colors.background, borderColor: colors.seriesLight, borderWidth: 2 },
        symbol: 'circle',
        symbolSize: 9,
        emphasis: { scale: 1.4 },
        cursor: 'pointer',
        areaStyle: {
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
        animationDuration: 700,
        animationEasing: 'cubicOut',
      },
    ],
  };
}

/** Kilometres with one decimal, from metres: "41.0". */
export function km(metres: number): string {
  return (metres / 1000).toFixed(1);
}

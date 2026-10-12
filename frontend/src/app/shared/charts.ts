import type { EChartsCoreOption } from 'echarts/core';
import { ChartColors, baseChartOptions, tooltipHtml } from './chart-theme';

export const monthNames = ['Jan', 'Feb', 'Mar', 'Apr', 'May', 'Jun', 'Jul', 'Aug', 'Sep', 'Oct', 'Nov', 'Dec'];

/** Distance (metres) run in the Training Week starting on the Monday <code>start</code> (yyyy-MM-dd). */
export interface WeekDistance {
  start: string;
  distance: number;
  /** None if the week is not one, or where Distance Jumps are not given. */
  distanceJump?: DistanceJump | null;
}

/** A Training Week more than 10% above the week it is compared with. A warning only. */
export interface DistanceJump {
  /** The Monday of the week compared with, yyyy-MM-dd. */
  comparedWith: string;
  /** The ratio of the distances minus one: 0.2 for 20% up. */
  increase: number;
}

/** "Distance Jump: +12.5% on the week of 7 Sep 2026"; empty for a week that is not one. */
export function distanceJumpNote(jump: DistanceJump | null | undefined): string {
  if (!jump) return '';
  // A Distance Jump is more than 10% up, so it never reads as +10%, even when it is just over.
  const percent = Math.max(Number((jump.increase * 100).toFixed(1)), 10.1);
  return `Distance Jump: +${percent}% on the week of ${weekLabel(jump.comparedWith)}`;
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
        return tooltipHtml(colors, `Week of ${weekLabel(week.start)}`, (week.distance / 1000).toFixed(1), 'km', distanceJumpNote(week.distanceJump));
      },
    },
    xAxis: categoryAxis(weeks.map((w) => shortWeekLabel(w.start)), colors),
    yAxis: valueAxis(colors, (km) => `${km} km`, true),
    series: [
      {
        type: 'bar',
        data: weeks.map((w) => {
          const value = Math.round(w.distance / 100) / 10;
          return w.distanceJump ? { value, itemStyle: { color: colors.warning } } : value;
        }),
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

/** Kilometres with one decimal, from metres: "41.0". */
export function km(metres: number): string {
  return (metres / 1000).toFixed(1);
}

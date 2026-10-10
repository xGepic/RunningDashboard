import type { EChartsCoreOption } from 'echarts/core';
import { ChartColors, baseChartOptions } from './chart-theme';
import { formatDistance } from './format';

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

export function categoryAxis(labels: string[], colors: ChartColors) {
  return {
    type: 'category',
    data: labels,
    axisLine: { lineStyle: { color: colors.grid } },
    axisTick: { show: false },
    axisLabel: { color: colors.muted, hideOverlap: true },
  };
}

export function valueAxis(colors: ChartColors, formatter: (value: number) => string, fromZero: boolean) {
  return {
    type: 'value',
    scale: !fromZero,
    axisLabel: { color: colors.muted, formatter },
    splitLine: { lineStyle: { color: colors.grid, width: 1, type: 'solid' } },
  };
}

/** Column chart of distance per Training Week, in km. */
export function weeklyDistanceChart(weeks: WeekDistance[], colors: ChartColors): EChartsCoreOption {
  return {
    ...baseChartOptions(colors),
    tooltip: {
      ...baseChartOptions(colors).tooltip,
      axisPointer: { type: 'shadow' },
      formatter: (params: { dataIndex: number }[]) => {
        const week = weeks[params[0]?.dataIndex];
        if (!week) return '';
        return `Week of ${weekLabel(week.start)}<br><b>${formatDistance(week.distance)}</b>`;
      },
    },
    xAxis: categoryAxis(weeks.map((w) => weekLabel(w.start)), colors),
    yAxis: valueAxis(colors, (km) => `${km} km`, true),
    series: [
      {
        type: 'bar',
        data: weeks.map((w) => Math.round(w.distance / 100) / 10),
        barMaxWidth: 24,
        barCategoryGap: '20%',
        itemStyle: { color: colors.series, borderRadius: [4, 4, 0, 0] },
      },
    ],
  };
}

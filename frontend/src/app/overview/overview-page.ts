import { DecimalPipe } from '@angular/common';
import { httpResource } from '@angular/common/http';
import { Component, computed, inject } from '@angular/core';
import type { EChartsCoreOption } from 'echarts/core';
import { NgxEchartsDirective } from 'ngx-echarts';
import { ChartColors, ChartTheme } from '../shared/chart-theme';
import { formatDistance, formatHours, formatPace, formatTotalDistance } from '../shared/format';
import { reloadOnSync } from '../sync/reload-on-sync';
import { MonthPace, Overview, WeekDistance } from './overview';

const monthNames = ['Jan', 'Feb', 'Mar', 'Apr', 'May', 'Jun', 'Jul', 'Aug', 'Sep', 'Oct', 'Nov', 'Dec'];

@Component({
  selector: 'app-overview-page',
  imports: [DecimalPipe, NgxEchartsDirective],
  templateUrl: './overview-page.html',
  styleUrl: './overview-page.scss',
})
export class OverviewPage {
  private readonly theme = inject(ChartTheme);

  protected readonly overview = httpResource<Overview>(() => '/api/overview');
  protected readonly formatDistance = formatDistance;
  protected readonly formatTotalDistance = formatTotalDistance;
  protected readonly formatHours = formatHours;
  protected readonly formatPace = formatPace;
  protected readonly monthLabel = monthLabel;
  protected readonly weekLabel = weekLabel;

  protected readonly weeklyDistance = computed(() => {
    const weeks = this.overview.value()?.weeks ?? [];
    return weeks.length ? weeklyDistanceChart(weeks, this.theme.colors()) : null;
  });

  protected readonly monthlyPace = computed(() => {
    const months = this.overview.value()?.months ?? [];
    return months.some((m) => m.pace !== null) ? monthlyPaceChart(months, this.theme.colors()) : null;
  });

  constructor() {
    reloadOnSync(() => this.overview.reload());
  }
}

/** "5 Oct 2026" for the Training Week starting Monday 2026-10-05. */
function weekLabel(start: string): string {
  const [year, month, day] = start.split('-').map(Number);
  return `${day} ${monthNames[month - 1]} ${year}`;
}

/** "Oct 2026". */
function monthLabel(month: MonthPace): string {
  return `${monthNames[month.month - 1]} ${month.year}`;
}

/** Shared axis and tooltip styling: recessive hairline grid, muted text. */
function baseOptions(colors: ChartColors) {
  return {
    backgroundColor: 'transparent',
    textStyle: { color: colors.muted },
    grid: { left: 8, right: 16, top: 16, bottom: 8, containLabel: true },
    tooltip: {
      trigger: 'axis',
      backgroundColor: colors.surface,
      borderColor: colors.grid,
      textStyle: { color: colors.text },
    },
  };
}

function categoryAxis(labels: string[], colors: ChartColors) {
  return {
    type: 'category',
    data: labels,
    axisLine: { lineStyle: { color: colors.grid } },
    axisTick: { show: false },
    axisLabel: { color: colors.muted, hideOverlap: true },
  };
}

function valueAxis(colors: ChartColors, formatter: (value: number) => string, fromZero: boolean) {
  return {
    type: 'value',
    scale: !fromZero,
    axisLabel: { color: colors.muted, formatter },
    splitLine: { lineStyle: { color: colors.grid, width: 1, type: 'solid' } },
  };
}

function weeklyDistanceChart(weeks: WeekDistance[], colors: ChartColors): EChartsCoreOption {
  return {
    ...baseOptions(colors),
    tooltip: {
      ...baseOptions(colors).tooltip,
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

function monthlyPaceChart(months: MonthPace[], colors: ChartColors): EChartsCoreOption {
  return {
    ...baseOptions(colors),
    tooltip: {
      ...baseOptions(colors).tooltip,
      axisPointer: { type: 'line', lineStyle: { color: colors.muted } },
      formatter: (params: { dataIndex: number }[]) => {
        const month = months[params[0]?.dataIndex];
        if (!month) return '';
        const pace = month.pace === null ? 'no Runs' : `<b>${formatPace(month.pace)}</b> over ${formatDistance(month.distance)}`;
        return `${monthLabel(month)}<br>${pace}`;
      },
    },
    xAxis: categoryAxis(months.map(monthLabel), colors),
    yAxis: valueAxis(colors, (seconds) => formatPace(seconds).replace(' /km', ''), false),
    series: [
      {
        type: 'line',
        data: months.map((m) => (m.pace === null ? null : Math.round(m.pace))),
        connectNulls: false,
        lineStyle: { width: 2, color: colors.series, cap: 'round', join: 'round' },
        itemStyle: { color: colors.series },
        symbol: 'circle',
        symbolSize: 8,
        showSymbol: months.length <= 24,
      },
    ],
  };
}

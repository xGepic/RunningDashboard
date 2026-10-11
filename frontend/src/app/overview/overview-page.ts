import { DecimalPipe } from '@angular/common';
import { httpResource } from '@angular/common/http';
import { Component, computed, inject, linkedSignal } from '@angular/core';
import type { EChartsCoreOption } from 'echarts/core';
import { NgxEchartsDirective } from 'ngx-echarts';
import { ChartColors, ChartTheme, baseChartOptions } from '../shared/chart-theme';
import { WeekDistance, categoryAxis, monthNames, valueAxis, weekLabel, weeklyDistanceChart } from '../shared/charts';
import { formatDistance, formatHours, formatPace, formatTotalDistance } from '../shared/format';
import { reloadOnSync } from '../sync/reload-on-sync';
import { MonthPace, Overview } from './overview';

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

  /** Years that have Training Weeks, newest first. A week belongs to the year its Monday falls in. */
  protected readonly weekYears = computed(() => {
    const weeks = this.overview.hasValue() ? this.overview.value().weeks : [];
    return [...new Set(weeks.map(weekYear))].sort((a, b) => b - a);
  });

  /** Defaults to the newest year, and keeps the chosen year across reloads while it still has weeks. */
  protected readonly selectedYear = linkedSignal<number[], number | null>({
    source: this.weekYears,
    computation: (years, previous) =>
      previous?.value != null && years.includes(previous.value) ? previous.value : (years[0] ?? null),
  });

  protected readonly selectedWeeks = computed(() => {
    const weeks = this.overview.hasValue() ? this.overview.value().weeks : [];
    return weeks.filter((w) => weekYear(w) === this.selectedYear());
  });

  protected readonly weeklyDistance = computed(() => {
    const weeks = this.selectedWeeks();
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

function weekYear(week: WeekDistance): number {
  return Number(week.start.slice(0, 4));
}

/** "Oct 2026". */
function monthLabel(month: MonthPace): string {
  return `${monthNames[month.month - 1]} ${month.year}`;
}

function monthlyPaceChart(months: MonthPace[], colors: ChartColors): EChartsCoreOption {
  return {
    ...baseChartOptions(colors),
    tooltip: {
      ...baseChartOptions(colors).tooltip,
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

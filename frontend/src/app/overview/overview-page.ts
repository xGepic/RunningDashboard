import { DecimalPipe } from '@angular/common';
import { httpResource } from '@angular/common/http';
import { Component, computed, inject, linkedSignal } from '@angular/core';
import type { EChartsCoreOption } from 'echarts/core';
import { NgxEchartsDirective } from 'ngx-echarts';
import { ChartColors, ChartTheme, baseChartOptions, tooltipHtml } from '../shared/chart-theme';
import {
  WeekDistance,
  categoryAxis,
  distanceJumpNote,
  km,
  monthNames,
  shortWeekLabel,
  valueAxis,
  weekLabel,
  weeklyDistanceChart,
} from '../shared/charts';
import { formatDistance, formatPace } from '../shared/format';
import { reloadOnSync } from '../sync/reload-on-sync';
import { MonthPace, Overview, Totals, YearTotals } from './overview';
import { YearlyGoalCard } from './yearly-goal-card';

const wholeNumber = new Intl.NumberFormat('en', { maximumFractionDigits: 0 });

/** One all-time total, with the same figure per year for its mini bars. */
interface Kpi {
  label: string;
  value: string;
  unit: string;
  icon: string;
  years: { year: number; value: string; height: number; current: boolean }[];
}

const kpiDefinitions: { label: string; unit: string; icon: string; of: (t: Totals) => number; scale: number }[] = [
  { label: 'Runs', unit: '', icon: 'M3 12h4l3-7 4 14 3-7h4', of: (t) => t.runs, scale: 1 },
  { label: 'Distance', unit: 'km', icon: 'M4 19l5-14 4 9 3-5 4 10', of: (t) => t.distance, scale: 1000 },
  { label: 'Moving Time', unit: 'h', icon: 'M12 7v5l3 2M12 21a9 9 0 1 0 0-18 9 9 0 0 0 0 18z', of: (t) => t.movingTime, scale: 3600 },
  { label: 'Elevation gain', unit: 'm', icon: 'M3 19l6-9 4 5 3-4 5 8z', of: (t) => t.elevationGain, scale: 1 },
];

@Component({
  selector: 'app-overview-page',
  imports: [DecimalPipe, NgxEchartsDirective, YearlyGoalCard],
  templateUrl: './overview-page.html',
  styleUrl: './overview-page.scss',
})
export class OverviewPage {
  private readonly theme = inject(ChartTheme);

  protected readonly overview = httpResource<Overview>(() => '/api/overview');
  protected readonly formatDistance = formatDistance;
  protected readonly formatPace = formatPace;
  protected readonly monthLabel = monthLabel;
  protected readonly weekLabel = weekLabel;
  protected readonly distanceJumpNote = distanceJumpNote;
  protected readonly km = km;
  protected readonly thisYear = new Date().getFullYear();

  /** The years, oldest first. */
  private readonly years = computed<YearTotals[]>(() =>
    this.overview.hasValue() ? [...this.overview.value().years].sort((a, b) => a.year - b.year) : [],
  );

  protected readonly kpis = computed<Kpi[]>(() => {
    if (!this.overview.hasValue()) return [];
    const { totals } = this.overview.value();
    const years = this.years();
    return kpiDefinitions.map((kpi) => {
      const max = Math.max(1, ...years.map(kpi.of));
      return {
        label: kpi.label,
        unit: kpi.unit,
        icon: kpi.icon,
        value: wholeNumber.format(kpi.of(totals) / kpi.scale),
        years: years.map((y) => ({
          year: y.year,
          value: wholeNumber.format(kpi.of(y) / kpi.scale),
          height: Math.max(4, (kpi.of(y) / max) * 100),
          current: y.year === this.thisYear,
        })),
      };
    });
  });

  /** Metres run so far this year. */
  protected readonly thisYearsDistance = computed(() => this.years().find((y) => y.year === this.thisYear)?.distance ?? 0);

  /** The years, newest first, each with its share of the longest year's distance. */
  protected readonly yearRows = computed(() => {
    const years = this.years();
    const max = Math.max(1, ...years.map((y) => y.distance));
    return [...years].reverse().map((y) => ({ ...y, share: (y.distance / max) * 100 }));
  });

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

  /** The selected year's longest week and its average week. */
  protected readonly weekStats = computed(() => {
    const weeks = this.selectedWeeks();
    if (!weeks.length) return null;
    const peak = weeks.reduce((a, b) => (b.distance > a.distance ? b : a));
    const total = weeks.reduce((sum, w) => sum + w.distance, 0);
    return { peak, peakLabel: shortWeekLabel(peak.start), average: total / weeks.length };
  });

  protected readonly weeklyDistance = computed(() => {
    const weeks = this.selectedWeeks();
    return weeks.length ? weeklyDistanceChart(weeks, this.theme.colors()) : null;
  });

  /** Years that have months, newest first. */
  protected readonly monthYears = computed(() => {
    const months = this.overview.hasValue() ? this.overview.value().months : [];
    return [...new Set(months.map((m) => m.year))].sort((a, b) => b - a);
  });

  /** Chosen apart from the weeks' year; defaults to the newest and is kept across reloads like it. */
  protected readonly selectedMonthYear = linkedSignal<number[], number | null>({
    source: this.monthYears,
    computation: (years, previous) =>
      previous?.value != null && years.includes(previous.value) ? previous.value : (years[0] ?? null),
  });

  protected readonly selectedMonths = computed(() => {
    const months = this.overview.hasValue() ? this.overview.value().months : [];
    return months.filter((m) => m.year === this.selectedMonthYear());
  });

  protected readonly monthlyPace = computed(() => {
    const months = this.selectedMonths();
    return months.some((m) => m.pace !== null) ? monthlyPaceChart(months, this.theme.colors()) : null;
  });

  constructor() {
    reloadOnSync(() => this.overview.reload());
  }

  /** The Yearly Goal was set, changed or removed. */
  protected goalChanged(): void {
    this.overview.reload();
  }

  protected formatWhole(value: number): string {
    return wholeNumber.format(value);
  }
}

function weekYear(week: WeekDistance): number {
  return Number(week.start.slice(0, 4));
}

/** "Oct 2026". */
function monthLabel(month: MonthPace): string {
  return `${monthNames[month.month - 1]} ${month.year}`;
}

/** Faster is up: the y axis runs from the slowest pace at the bottom to the fastest at the top. */
function monthlyPaceChart(months: MonthPace[], colors: ChartColors): EChartsCoreOption {
  const base = baseChartOptions(colors);
  return {
    ...base,
    tooltip: {
      ...base.tooltip,
      axisPointer: { type: 'line', lineStyle: { color: colors.tooltipBorder, type: 'solid' } },
      formatter: (params: { dataIndex: number }[]) => {
        const month = months[params[0]?.dataIndex];
        if (!month) return '';
        if (month.pace === null) return tooltipHtml(colors, monthLabel(month), 'no Runs');
        return tooltipHtml(colors, monthLabel(month), formatPace(month.pace).replace(' /km', ''), '/km', `over ${formatDistance(month.distance)}`);
      },
    },
    xAxis: { ...categoryAxis(months.map((m) => monthNames[m.month - 1]), colors), boundaryGap: false },
    yAxis: { ...valueAxis(colors, (seconds) => formatPace(seconds).replace(' /km', ''), false), inverse: true },
    series: [
      {
        type: 'line',
        data: months.map((m) => (m.pace === null ? null : Math.round(m.pace))),
        connectNulls: false,
        smooth: false,
        lineStyle: { width: 2, color: colors.series, join: 'round' },
        itemStyle: { color: colors.background, borderColor: colors.seriesLight, borderWidth: 2 },
        symbol: 'circle',
        symbolSize: 9,
        showSymbol: false,
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
      },
    ],
  };
}

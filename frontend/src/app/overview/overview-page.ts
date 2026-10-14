import { DecimalPipe } from '@angular/common';
import { httpResource } from '@angular/common/http';
import { Component, computed, inject, linkedSignal } from '@angular/core';
import { Router, RouterLink } from '@angular/router';
import type { EChartsCoreOption } from 'echarts/core';
import { NgxEchartsDirective } from 'ngx-echarts';
import { ChartColors, ChartTheme, tooltipHtml } from '../shared/chart-theme';
import { WeekDistance, paceChart, paceLabel, shortWeekLabel, weekLabel, weeklyDistanceChart } from '../shared/charts';
import { monthNames } from '../shared/dates';
import { DistanceUnit, formatDistance, formatWhole } from '../shared/format';
import { Units } from '../shared/units';
import { reloadOnSync } from '../sync/reload-on-sync';
import { MonthPace, Overview, Totals, YearTotals } from './overview';
import { StreakCard } from './streak-card';
import { YearlyGoalCard } from './yearly-goal-card';

/** One all-time total, with the same figure per year for its mini bars. */
interface Kpi {
  label: string;
  value: string;
  unit: string;
  icon: string;
  years: { year: number; value: string; height: number; current: boolean }[];
}

/** `unit` and `scale` null: in the Distance Unit. */
const kpiDefinitions: { label: string; unit: string | null; icon: string; of: (t: Totals) => number; scale: number | null }[] = [
  { label: 'Runs', unit: '', icon: 'M3 12h4l3-7 4 14 3-7h4', of: (t) => t.runs, scale: 1 },
  { label: 'Distance', unit: null, icon: 'M4 19l5-14 4 9 3-5 4 10', of: (t) => t.distance, scale: null },
  { label: 'Moving Time', unit: 'h', icon: 'M12 7v5l3 2M12 21a9 9 0 1 0 0-18 9 9 0 0 0 0 18z', of: (t) => t.movingTime, scale: 3600 },
  { label: 'Elevation gain', unit: 'm', icon: 'M3 19l6-9 4 5 3-4 5 8z', of: (t) => t.elevationGain, scale: 1 },
];

@Component({
  selector: 'app-overview-page',
  imports: [DecimalPipe, NgxEchartsDirective, RouterLink, StreakCard, YearlyGoalCard],
  templateUrl: './overview-page.html',
  styleUrl: './overview-page.scss',
})
export class OverviewPage {
  private readonly theme = inject(ChartTheme);
  private readonly router = inject(Router);

  protected readonly overview = httpResource<Overview>(() => '/api/overview');
  /** Distances and Pace in the Distance Unit; elevation stays in metres. */
  protected readonly units = inject(Units);
  protected readonly monthLabel = monthLabel;
  protected readonly weekLabel = weekLabel;
  protected readonly formatWhole = formatWhole;
  protected readonly thisYear = new Date().getFullYear();

  /** The years, oldest first. */
  private readonly years = computed<YearTotals[]>(() =>
    this.overview.hasValue() ? [...this.overview.value().years].sort((a, b) => a.year - b.year) : [],
  );

  protected readonly kpis = computed<Kpi[]>(() => {
    if (!this.overview.hasValue()) return [];
    const { totals } = this.overview.value();
    const years = this.years();
    const metresPerUnit = this.units.toMetres(1);
    return kpiDefinitions.map((kpi) => {
      const max = Math.max(1, ...years.map(kpi.of));
      const scale = kpi.scale ?? metresPerUnit;
      return {
        label: kpi.label,
        unit: kpi.unit ?? this.units.unit(),
        icon: kpi.icon,
        value: formatWhole(kpi.of(totals) / scale),
        years: years.map((y) => ({
          year: y.year,
          value: formatWhole(kpi.of(y) / scale),
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
    return weeks.length ? weeklyDistanceChart(weeks, this.units.unit(), this.theme.colors()) : null;
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
    return months.some((m) => m.pace !== null) ? monthlyPaceChart(months, this.units.unit(), this.theme.colors()) : null;
  });

  constructor() {
    reloadOnSync(() => this.overview.reload());
  }

  /** A column opens its Training Week's Runs. */
  protected openWeek(index: number): void {
    const week = this.selectedWeeks()[index];
    if (week) this.router.navigate(['/runs'], { queryParams: { week: week.start } });
  }

  /** The Yearly Goal was set, changed or removed. */
  protected goalChanged(): void {
    this.overview.reload();
  }
}

function weekYear(week: WeekDistance): number {
  return Number(week.start.slice(0, 4));
}

/** "Oct 2026". */
function monthLabel(month: MonthPace): string {
  return `${monthNames[month.month - 1]} ${month.year}`;
}

/** Pace per month, per km or mile; a dot shows only under the pointer. */
function monthlyPaceChart(months: MonthPace[], unit: DistanceUnit, colors: ChartColors): EChartsCoreOption {
  return paceChart(
    {
      labels: months.map((m) => monthNames[m.month - 1]),
      paces: months.map((m) => m.pace),
      showSymbol: false,
      tooltip: (index) => {
        const month = months[index];
        if (month.pace === null) return tooltipHtml(colors, monthLabel(month), 'no Runs');
        return tooltipHtml(colors, monthLabel(month), paceLabel(month.pace, unit), `/${unit}`, `over ${formatDistance(month.distance, unit)}`);
      },
    },
    unit,
    colors,
  );
}

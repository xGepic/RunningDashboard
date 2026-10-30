import { DatePipe } from '@angular/common';
import { httpResource } from '@angular/common/http';
import { Component, computed, inject, signal } from '@angular/core';
import { Router, RouterLink } from '@angular/router';
import type { EChartsCoreOption } from 'echarts/core';
import { NgxEchartsDirective } from 'ngx-echarts';
import { ChartColors, ChartTheme, baseChartOptions, escapeHtml, tooltipHtml } from '../shared/chart-theme';
import { daysBetween, localDate, today } from '../shared/dates';
import { DistanceUnit, formatDuration, formatPace } from '../shared/format';
import { Units } from '../shared/units';
import { reloadOnSync } from '../sync/reload-on-sync';
import { PersonalBest, RecordDistance } from './record';

/** The distances that get a card of their own, as Strava names them. */
const featuredNames = ['5K', '10K', 'Half-Marathon', 'Marathon'];

/** A Personal Best set this many days ago or less is new. */
const newDays = 60;

@Component({
  selector: 'app-records-page',
  imports: [DatePipe, NgxEchartsDirective, RouterLink],
  templateUrl: './records-page.html',
  styleUrl: './records-page.scss',
})
export class RecordsPage {
  private readonly theme = inject(ChartTheme);
  private readonly router = inject(Router);

  protected readonly records = httpResource<RecordDistance[]>(() => '/api/records');
  protected readonly formatDuration = formatDuration;
  /** Pace per km or mile; the distances themselves (5K, Half Marathon) keep their names. */
  protected readonly units = inject(Units);
  protected readonly featuredNames = featuredNames;

  /** Distances (metres) the Athlete picked; until they pick, the ones shown by default. */
  private readonly picked = signal<ReadonlySet<number> | null>(null);

  protected readonly shown = computed<ReadonlySet<number>>(
    () => this.picked() ?? new Set((this.records.value() ?? []).filter((r) => r.shownByDefault).map((r) => r.distance)),
  );

  protected readonly featured = computed(() => {
    const records = this.records.value() ?? [];
    return featuredNames
      .map((name) => records.find((r) => r.name === name))
      .filter((r): r is RecordDistance => !!r)
      .map((record) => ({ record, improvements: record.progression.length - 1 }));
  });

  /** Every distance, with the width of its pace bar: the fastest pace fills it, the slowest a third. */
  protected readonly rows = computed(() => {
    const records = this.records.value() ?? [];
    const paces = records.map((r) => r.personalBest.timePerKm).filter((p): p is number => p !== null);
    const [fastest, slowest] = [Math.min(...paces), Math.max(...paces)];
    return records.map((record) => {
      const pace = record.personalBest.timePerKm;
      const share = pace === null ? 0 : slowest === fastest ? 100 : 30 + 70 * (1 - (pace - fastest) / (slowest - fastest));
      return { record, share };
    });
  });

  protected readonly charts = computed(() => {
    const colors = this.theme.colors();
    const now = new Date();
    return (this.records.value() ?? [])
      .filter((record) => this.shown().has(record.distance))
      .map((record) => {
        const first = record.progression[0];
        const gain = first.elapsedTime - record.personalBest.elapsedTime;
        return { record, first, gain, steps: record.progression.length - 1, options: progressionChart(record, this.units.unit(), colors, now) };
      });
  });

  constructor() {
    reloadOnSync(() => this.records.reload());
  }

  protected toggle(distance: number): void {
    const shown = new Set(this.shown());
    if (!shown.delete(distance)) shown.add(distance);
    this.picked.set(shown);
  }

  /** A dot opens the Run that set that Personal Best; the point carrying it on to today has none. */
  protected openRun(record: RecordDistance, index: number): void {
    const best = record.progression[index];
    if (best) this.router.navigate(['/runs', best.runId]);
  }

  protected isNew(best: PersonalBest): boolean {
    return daysBetween(localDate(best.date), today()) <= newDays;
  }

  /** "Half Marathon" for Strava's "Half-Marathon". */
  protected displayName(name: string): string {
    return name.replace('-', ' ');
  }
}

interface ChartPoint {
  value: [string, number];
  /** The Personal Best set at this point; none for the point that carries the current one on to today. */
  personalBest: PersonalBest | null;
  symbol?: string;
}

/**
 * Step chart of how the Personal Best improved: each step holds until the next improvement, the last one up to
 * today. Faster is up.
 */
function progressionChart(record: RecordDistance, unit: DistanceUnit, colors: ChartColors, now: Date): EChartsCoreOption {
  const points: ChartPoint[] = [
    ...record.progression.map((personalBest): ChartPoint => ({ value: [personalBest.date, personalBest.elapsedTime], personalBest })),
    { value: [now.toISOString(), record.personalBest.elapsedTime], personalBest: null, symbol: 'none' },
  ];
  const base = baseChartOptions(colors);
  return {
    ...base,
    grid: { ...base.grid, top: 8 },
    tooltip: {
      ...base.tooltip,
      trigger: 'item',
      formatter: (params: { data: ChartPoint }) => {
        const best = params.data.personalBest;
        if (!best) return '';
        const date = new Date(best.date).toLocaleDateString('en-GB', { day: 'numeric', month: 'short', year: 'numeric' });
        return tooltipHtml(colors, date, formatDuration(best.elapsedTime), '', `${formatPace(best.timePerKm, unit)} · ${escapeHtml(best.runName)}`);
      },
    },
    xAxis: {
      type: 'time',
      axisLine: { show: false },
      axisTick: { show: false },
      axisLabel: { color: colors.faint, hideOverlap: true },
      splitLine: { show: true, lineStyle: { color: colors.grid } },
    },
    yAxis: {
      type: 'value',
      scale: true,
      inverse: true,
      splitNumber: 3,
      axisLabel: { color: colors.faint, formatter: formatDuration },
      splitLine: { lineStyle: { color: colors.grid, width: 1, type: 'dashed' } },
    },
    series: [
      {
        type: 'line',
        step: 'end',
        data: points,
        lineStyle: { width: 2, color: colors.series },
        itemStyle: { color: colors.background, borderColor: colors.series, borderWidth: 2 },
        symbol: 'circle',
        symbolSize: 9,
        cursor: 'pointer',
      },
    ],
  };
}

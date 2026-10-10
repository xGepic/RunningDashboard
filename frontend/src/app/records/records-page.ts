import { DatePipe } from '@angular/common';
import { httpResource } from '@angular/common/http';
import { Component, computed, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import type { EChartsCoreOption } from 'echarts/core';
import { NgxEchartsDirective } from 'ngx-echarts';
import { ChartColors, ChartTheme, baseChartOptions } from '../shared/chart-theme';
import { formatDuration, formatPace } from '../shared/format';
import { reloadOnSync } from '../sync/reload-on-sync';
import { PersonalBest, RecordDistance } from './record';

@Component({
  selector: 'app-records-page',
  imports: [DatePipe, NgxEchartsDirective, RouterLink],
  templateUrl: './records-page.html',
  styleUrl: './records-page.scss',
})
export class RecordsPage {
  private readonly theme = inject(ChartTheme);

  protected readonly records = httpResource<RecordDistance[]>(() => '/api/records');
  protected readonly formatDuration = formatDuration;
  protected readonly formatPace = formatPace;

  /** Distances (metres) the Athlete picked; until they pick, the ones shown by default. */
  private readonly picked = signal<ReadonlySet<number> | null>(null);

  protected readonly shown = computed<ReadonlySet<number>>(
    () => this.picked() ?? new Set((this.records.value() ?? []).filter((r) => r.shownByDefault).map((r) => r.distance)),
  );

  protected readonly charts = computed(() => {
    const colors = this.theme.colors();
    const today = new Date();
    return (this.records.value() ?? [])
      .filter((record) => this.shown().has(record.distance))
      .map((record) => ({ record, options: progressionChart(record, colors, today) }));
  });

  constructor() {
    reloadOnSync(() => this.records.reload());
  }

  protected toggle(distance: number): void {
    const shown = new Set(this.shown());
    if (!shown.delete(distance)) shown.add(distance);
    this.picked.set(shown);
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
 * today. Lower is faster.
 */
function progressionChart(record: RecordDistance, colors: ChartColors, today: Date): EChartsCoreOption {
  const points: ChartPoint[] = [
    ...record.progression.map((personalBest): ChartPoint => ({ value: [personalBest.date, personalBest.elapsedTime], personalBest })),
    { value: [today.toISOString(), record.personalBest.elapsedTime], personalBest: null, symbol: 'none' },
  ];
  const base = baseChartOptions(colors);
  return {
    ...base,
    tooltip: {
      ...base.tooltip,
      trigger: 'item',
      formatter: (params: { data: ChartPoint }) => {
        const best = params.data.personalBest;
        if (!best) return '';
        const date = new Date(best.date).toLocaleDateString('en-GB', { day: 'numeric', month: 'short', year: 'numeric' });
        return `${date}<br><b>${formatDuration(best.elapsedTime)}</b> (${formatPace(best.timePerKm)})<br>${escapeHtml(best.runName)}`;
      },
    },
    xAxis: {
      type: 'time',
      axisLine: { lineStyle: { color: colors.grid } },
      axisLabel: { color: colors.muted, hideOverlap: true },
      splitLine: { show: false },
    },
    yAxis: {
      type: 'value',
      scale: true,
      axisLabel: { color: colors.muted, formatter: formatDuration },
      splitLine: { lineStyle: { color: colors.grid, width: 1, type: 'solid' } },
    },
    series: [
      {
        type: 'line',
        step: 'end',
        data: points,
        lineStyle: { width: 2, color: colors.series },
        itemStyle: { color: colors.series },
        symbol: 'circle',
        symbolSize: 8,
      },
    ],
  };
}

/** Run names come from Strava and end up in the tooltip's HTML. */
function escapeHtml(text: string): string {
  return text.replace(/[&<>"']/g, (c) => `&#${c.charCodeAt(0)};`);
}

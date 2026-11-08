import type { EChartsCoreOption } from 'echarts/core';
import { Break, breakLabel, breakOn, breaksWithin, kindLabel } from '../shared/breaks';
import { ChartColors, baseChartOptions, escapeHtml, tooltipHtml } from '../shared/chart-theme';
import { areaFill, withAlpha } from '../shared/charts';
import { localDate, monthNames } from '../shared/dates';
import { FreshnessDay, FreshnessRace, dayAfter, dayLabel, formatLoad } from './freshness';

const weekdays = ['Sun', 'Mon', 'Tue', 'Wed', 'Thu', 'Fri', 'Sat'];

/** Something to name above the chart on its day: a Goal Race, or one of the hardest days. */
interface Marker {
  date: string;
  name: string;
}

/** A marker of one of the hardest days: a click on it opens its Run. */
export interface RunMarker extends Marker {
  runId: number;
}

/** The Run a click on the chart opens, from ECharts' click event; none for a click on anything but a hard day's marker. */
export function clickedRun(event: { componentType?: string; data?: unknown }): number | null {
  const data = event.data as Partial<RunMarker> | undefined;
  return event.componentType === 'markLine' && typeof data?.runId === 'number' ? data.runId : null;
}

/**
 * Fitness & Freshness as one panel: Fitness, the figure that matters, as a filled line in the Theme's colour, and
 * Fatigue faint behind it in the second chart colour, each named at its end. Goal Races and the hardest days are
 * dashed lines named at the top, Breaks shaded and named; pointing at a day shows its figures and its hardest Run.
 * A hardest day's marker can be clicked (see clickedRun); a Goal Race's only when it is one of them.
 */
export function freshnessChart(
  days: readonly FreshnessDay[],
  races: readonly FreshnessRace[],
  hardest: readonly FreshnessDay[],
  breaks: readonly Break[],
  colors: ChartColors,
): EChartsCoreOption {
  const base = baseChartOptions(colors);
  const [from, to] = [days[0].date, days[days.length - 1].date];
  const endLabel = (name: string) => ({ show: true, formatter: name, color: colors.muted, fontSize: 11, distance: 6 });
  // A hard day on a Goal Race is named by the race, and opens its Run like the others.
  const raceOn = (date: string) => races.find((race) => race.date === date);
  const runMarkers: RunMarker[] = hardest
    .filter((day) => day.runId !== null)
    .map((day) => ({ date: day.date, name: raceOn(day.date)?.name ?? `${day.runName} · ${dayLabel(day.date, to)}`, runId: day.runId! }));
  const raceMarkers: Marker[] = races.filter((race) => !runMarkers.some((m) => m.date === race.date)).map((race) => ({ date: race.date, name: race.name }));

  return {
    ...base,
    // Room above for the markers' names, and on the right for the lines' names.
    grid: { left: 44, right: 64, top: 30, bottom: 26 },
    tooltip: {
      ...base.tooltip,
      trigger: 'axis',
      axisPointer: { type: 'line', lineStyle: { color: colors.faint, width: 1, type: 'dashed' } },
      formatter: (params: { dataIndex: number }[]) => {
        const day = days[params[0]?.dataIndex];
        return day ? dayTooltip(day, races, breaks, colors) : '';
      },
    },
    xAxis: {
      type: 'time',
      min: from,
      max: to,
      axisLine: { show: false },
      axisTick: { show: false },
      axisLabel: { color: colors.faint, hideOverlap: true },
      splitLine: { show: false },
    },
    yAxis: {
      type: 'value',
      min: 0,
      splitNumber: 4,
      axisLabel: { color: colors.faint, formatter: (value: number) => formatLoad(value) },
      splitLine: { lineStyle: { color: colors.grid, width: 1, type: 'dashed' } },
    },
    series: [
      {
        name: 'Fatigue',
        type: 'line',
        data: days.map((d) => [d.date, d.fatigue]),
        showSymbol: false,
        lineStyle: { width: 1.5, color: withAlpha(colors.fatigue, 0.55), join: 'round' },
        itemStyle: { color: colors.fatigue },
        endLabel: endLabel('Fatigue'),
        z: 2,
        animationDuration: 700,
        animationEasing: 'cubicOut',
        // The hardest days, on a series of their own so that only they answer clicks.
        markLine: {
          ...markLineStyle(colors),
          emphasis: { label: { color: colors.text }, lineStyle: { color: colors.muted, width: 1 } },
          data: runMarkers.map((marker) => ({ xAxis: marker.date, name: marker.name, runId: marker.runId })),
        },
      },
      {
        name: 'Fitness',
        type: 'line',
        data: days.map((d) => [d.date, d.fitness]),
        showSymbol: false,
        lineStyle: { width: 2.5, color: colors.series, join: 'round' },
        itemStyle: { color: colors.series },
        areaStyle: { color: areaFill(colors) },
        endLabel: endLabel('Fitness'),
        z: 3,
        animationDuration: 700,
        animationEasing: 'cubicOut',
        ...marks(raceMarkers, breaks, from, to, colors),
      },
    ],
  };
}

/** A marker's dashed line, named above the chart. */
function markLineStyle(colors: ChartColors) {
  return {
    symbol: 'none',
    lineStyle: { color: colors.faint, type: 'dashed', width: 1 },
    label: { position: 'end', color: colors.muted, fontSize: 10.5, formatter: (p: { name: string }) => p.name },
  };
}

/** The Goal Races as dashed lines named above the chart, and the Breaks as bands named by their kind. */
function marks(markers: readonly Marker[], breaks: readonly Break[], from: string, to: string, colors: ChartColors) {
  const shown = breaksWithin(breaks, from, to);
  return {
    markLine: {
      ...markLineStyle(colors),
      silent: true,
      data: markers.map((marker) => ({ xAxis: marker.date, name: marker.name })),
    },
    markArea: {
      silent: true,
      label: { show: true, position: 'insideTop', fontSize: 10.5 },
      data: shown.map((period) => {
        const color = colors.breaks[period.kind];
        return [
          { xAxis: period.start, name: kindLabel(period.kind), itemStyle: { color: withAlpha(color, 0.1) }, label: { color } },
          { xAxis: dayAfter(period.end) },
        ];
      }),
    },
  };
}

/** The day and its hardest Run, its Fitness large, then Fatigue and the day's load, and a Goal Race or Break on it. */
function dayTooltip(day: FreshnessDay, races: readonly FreshnessRace[], breaks: readonly Break[], colors: ChartColors): string {
  // Spelt out here: the browser's own short names differ between versions ("Sep" or "Sept").
  const at = localDate(day.date);
  const date = `${weekdays[at.getDay()]} ${at.getDate()} ${monthNames[at.getMonth()]} ${at.getFullYear()}`;
  const title = day.runName ? `${date} · ${escapeHtml(day.runName)}` : date;
  const row = (swatch: string | null, label: string, value: string) =>
    `<div style="display:flex;align-items:center;gap:6px;font-size:11px;color:${colors.muted};margin-top:3px">` +
    (swatch ? `<span style="width:8px;height:8px;border-radius:2px;background:${swatch}"></span>` : `<span style="width:8px"></span>`) +
    `${label}<span style="margin-left:auto;padding-left:14px;font-family:${colors.fontMono};color:${colors.text}">${value}</span></div>`;
  const race = races.find((r) => r.date === day.date);
  const period = breakOn(breaks, day.date);
  return (
    tooltipHtml(colors, title, formatLoad(day.fitness), 'Fitness') +
    row(colors.fatigue, 'Fatigue', formatLoad(day.fatigue)) +
    row(null, 'Training Load', formatLoad(day.load)) +
    (race ? `<div style="font-size:11px;color:${colors.text};margin-top:4px">${escapeHtml(race.name)}</div>` : '') +
    (period ? `<div style="font-size:11px;color:${colors.breaks[period.kind]};margin-top:4px">${escapeHtml(breakLabel(period))}</div>` : '')
  );
}

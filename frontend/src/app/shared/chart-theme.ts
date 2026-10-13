import { DOCUMENT } from '@angular/common';
import { Injectable, computed, inject } from '@angular/core';
import { AthleteSettings } from './athlete-settings';

/** Chart colours, read from the CSS tokens in styles.scss so charts and the page share one palette. */
export interface ChartColors {
  series: string;
  seriesDeep: string;
  seriesLight: string;
  barTop: string;
  barHover: string;
  grid: string;
  text: string;
  muted: string;
  faint: string;
  surface: string;
  background: string;
  tooltip: string;
  tooltipBorder: string;
  fontMono: string;
}

/** The chart colours of the app's dark look, in the Athlete's Theme. */
@Injectable({ providedIn: 'root' })
export class ChartTheme {
  private readonly document = inject(DOCUMENT);
  private readonly settings = inject(AthleteSettings);

  readonly colors = computed<ChartColors>(() => {
    // Read again for another Theme: AthleteSettings has put it on the page by the time it changes.
    this.settings.theme();
    const style = getComputedStyle(this.document.documentElement);
    const token = (name: string) => style.getPropertyValue(name).trim();
    return {
      series: token('--chart-series'),
      seriesDeep: token('--accent-deep'),
      seriesLight: token('--accent-light'),
      barTop: token('--chart-bar-top'),
      barHover: token('--chart-bar-hover'),
      grid: token('--chart-grid'),
      text: token('--text'),
      muted: token('--text-muted'),
      faint: token('--text-faint'),
      surface: token('--surface'),
      background: token('--background'),
      tooltip: token('--tooltip'),
      tooltipBorder: token('--border-hover'),
      fontMono: token('--mono'),
    };
  });
}

/** Styling every chart shares: transparent background, dashed hairline grid, mono axis labels, floating tooltip. */
export function baseChartOptions(colors: ChartColors) {
  return {
    backgroundColor: 'transparent',
    textStyle: { color: colors.faint, fontFamily: colors.fontMono, fontSize: 10.5 },
    // Extra room on the sides: labels are measured before the mono web font has loaded.
    grid: { left: 18, right: 18, top: 12, bottom: 4, containLabel: true },
    tooltip: {
      trigger: 'axis',
      backgroundColor: colors.tooltip,
      borderColor: colors.tooltipBorder,
      borderWidth: 1,
      padding: [8, 11],
      textStyle: { color: colors.text },
      extraCssText: 'border-radius:9px;box-shadow:0 10px 30px rgba(0,0,0,0.45);',
    },
  };
}

/** The tooltip's content: a small muted title over a large mono value with its unit. */
export function tooltipHtml(colors: ChartColors, title: string, value: string, unit = '', note = ''): string {
  const unitHtml = unit ? ` <span style="color:${colors.muted};font-size:12px">${unit}</span>` : '';
  const noteHtml = note ? `<div style="font-size:11px;color:${colors.muted};margin-top:2px">${note}</div>` : '';
  return (
    `<div style="font-size:11px;color:${colors.muted}">${title}</div>` +
    `<div style="font-size:15px;font-family:${colors.fontMono};font-weight:500;margin-top:2px">${value}${unitHtml}</div>` +
    noteHtml
  );
}

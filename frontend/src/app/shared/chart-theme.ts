import { DOCUMENT } from '@angular/common';
import { Injectable, computed, inject, signal } from '@angular/core';

/** Chart colours, read from the CSS tokens in styles.scss so light and dark mode each get their own steps. */
export interface ChartColors {
  series: string;
  grid: string;
  text: string;
  muted: string;
  surface: string;
}

/** The current chart colours; they change when the system switches between light and dark mode. */
@Injectable({ providedIn: 'root' })
export class ChartTheme {
  private readonly document = inject(DOCUMENT);
  private readonly scheme = signal(this.prefersDark());

  readonly colors = computed<ChartColors>(() => {
    this.scheme();
    const style = getComputedStyle(this.document.documentElement);
    const token = (name: string) => style.getPropertyValue(name).trim();
    return {
      series: token('--chart-series'),
      grid: token('--chart-grid'),
      text: token('--text'),
      muted: token('--text-muted'),
      surface: token('--surface'),
    };
  });

  constructor() {
    this.document.defaultView
      ?.matchMedia?.('(prefers-color-scheme: dark)')
      .addEventListener('change', (event) => this.scheme.set(event.matches));
  }

  private prefersDark(): boolean {
    return this.document.defaultView?.matchMedia?.('(prefers-color-scheme: dark)').matches ?? false;
  }
}

/** Styling every chart shares: transparent background, recessive hairline grid, muted text, axis tooltip. */
export function baseChartOptions(colors: ChartColors) {
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

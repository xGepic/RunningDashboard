import { httpResource } from '@angular/common/http';
import { Component, computed, inject, input } from '@angular/core';
import { Router } from '@angular/router';
import { Units } from '../shared/units';
import { reloadOnSync } from '../sync/reload-on-sync';
import { HeatmapData, drawnRuns, heatmapYears, runsIn } from './heatmap';
import { HeatmapMap } from './heatmap-map';

/** Every Run's route on one map, of all years or one. */
@Component({
  selector: 'app-heatmap-page',
  imports: [HeatmapMap],
  templateUrl: './heatmap-page.html',
  styleUrl: './heatmap-page.scss',
})
export class HeatmapPage {
  /** From ?year=2025; all years without one or when malformed. */
  readonly year = input<string>();

  private readonly router = inject(Router);
  /** Distances in the Distance Unit. */
  protected readonly units = inject(Units);

  protected readonly selectedYear = computed(() => {
    const year = this.year();
    return year && /^\d{4}$/.test(year) ? Number(year) : null;
  });

  protected readonly heatmap = httpResource<HeatmapData>(() => '/api/heatmap');

  private readonly allRuns = computed(() => (this.heatmap.hasValue() ? drawnRuns(this.heatmap.value().runs) : []));

  protected readonly years = computed(() => heatmapYears(this.allRuns()));

  /** The Runs drawn: the year's, or all. */
  protected readonly runs = computed(() => runsIn(this.allRuns(), this.selectedYear()));

  protected readonly distance = computed(() => this.runs().reduce((sum, run) => sum + run.distance, 0));

  constructor() {
    reloadOnSync(() => this.heatmap.reload());
  }

  protected pick(year: number | null): void {
    this.router.navigate([], { queryParams: { year } });
  }
}

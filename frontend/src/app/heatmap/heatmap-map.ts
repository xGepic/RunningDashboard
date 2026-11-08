import { DOCUMENT, formatDate } from '@angular/common';
import { Component, DestroyRef, ElementRef, afterNextRender, computed, effect, inject, input, signal, viewChild } from '@angular/core';
import { Router } from '@angular/router';
import * as L from 'leaflet';
import { Units } from '../shared/units';
import { Bounds, DrawnRun, boundsOf, homeArea } from './heatmap';

const osm = '&copy; <a href="https://www.openstreetmap.org/copyright">OpenStreetMap</a> contributors';

/**
 * Every Run's route on one dark map, thin and see-through in the Theme's colour, so roads run often add up and glow.
 * Pointing at a route lights it up and names the Run; clicking it opens the Run's page.
 */
@Component({
  selector: 'app-heatmap-map',
  template: `<div #map class="map" role="img" [attr.aria-label]="label()"></div>`,
  styles: `
    .map {
      height: max(440px, calc(100vh - 300px));
      /* Leaflet's panes and controls have z-indexes up to 1000; keep them inside the map so it scrolls under the top bar. */
      isolation: isolate;
    }
  `,
})
export class HeatmapMap {
  /** Oldest first, so the newest are drawn on top. */
  readonly runs = input.required<DrawnRun[]>();

  protected readonly label = computed(() => `Map of the routes of ${this.runs().length} Runs, drawn on top of each other`);

  private readonly router = inject(Router);
  private readonly units = inject(Units);
  private readonly element = viewChild.required<ElementRef<HTMLElement>>('map');
  private readonly map = signal<L.Map | null>(null);

  constructor() {
    const style = getComputedStyle(inject(DOCUMENT).documentElement);
    const token = (name: string) => style.getPropertyValue(name).trim();
    const routeStyle: L.PolylineOptions = { color: token('--accent'), weight: 2, opacity: 0.4 };
    const highlighted: L.PolylineOptions = { color: token('--text'), weight: 4, opacity: 1 };

    afterNextRender(() => {
      // Hundreds of routes draw far faster on one canvas than as SVG paths; the tolerance makes thin lines easier to point at.
      const map = L.map(this.element().nativeElement, { scrollWheelZoom: false, renderer: L.canvas({ tolerance: 4 }) });
      // OpenStreetMap's tiles are light, so styles.scss darkens them (dark-tiles), and greys them out (muted-tiles) so
      // the routes stand out.
      L.tileLayer('https://tile.openstreetmap.org/{z}/{x}/{y}.png', { attribution: osm, maxZoom: 19, className: 'dark-tiles muted-tiles' }).addTo(map);
      this.map.set(map);
    });

    let drawn: L.LayerGroup | null = null;
    let fitted = false;
    effect(() => {
      const map = this.map();
      const runs = this.runs();
      if (!map) return;
      drawn?.remove();
      drawn = L.layerGroup(
        runs.map((run) => {
          const line = L.polyline(run.points, routeStyle);
          line.bindTooltip(this.tooltip(run), { sticky: true, direction: 'top', offset: [0, -8] });
          line.on('mouseover', () => line.setStyle(highlighted).bringToFront());
          line.on('mouseout', () => line.setStyle(routeStyle));
          line.on('click', () => this.router.navigate(['/runs', run.id]));
          return line;
        }),
      ).addTo(map);
      // Fit the home area once; switching years keeps the Athlete's own zoom and position.
      if (!fitted && runs.length > 0) {
        this.fitTo(homeArea(runs));
        fitted = true;
      }
    });

    inject(DestroyRef).onDestroy(() => this.map()?.remove());
  }

  /** Shows where the Athlete usually runs, or every Run shown. */
  fit(area: 'home' | 'all'): void {
    const runs = this.runs();
    this.fitTo(area === 'home' ? homeArea(runs) : boundsOf(runs));
  }

  private fitTo(bounds: Bounds | null): void {
    if (bounds) this.map()?.fitBounds(bounds, { padding: [24, 24], maxZoom: 15 });
  }

  /** "Prater loop · 1 Mar 2026 · 10.00 km", as text: a Run's name is never read as HTML. */
  private tooltip(run: DrawnRun): HTMLElement {
    const text = document.createElement('span');
    text.textContent = `${run.name} · ${formatDate(run.startLocal, 'd MMM y', 'en-US')} · ${this.units.distance(run.distance)}`;
    return text;
  }
}

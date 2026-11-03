import { DOCUMENT } from '@angular/common';
import { Component, DestroyRef, ElementRef, afterNextRender, effect, inject, input, signal, viewChild } from '@angular/core';
import * as L from 'leaflet';
import { Journey, journeyProgress } from './journey';

const osm = '&copy; <a href="https://www.openstreetmap.org/copyright">OpenStreetMap</a> contributors';

/**
 * The Virtual Journey on a map: the tour dimmed and dashed, the part travelled in the Theme's colour, a dot per Stop
 * (named on hover) and a marker where the Athlete is.
 */
@Component({
  selector: 'app-journey-map',
  template: `<div #map class="map" role="img" [attr.aria-label]="'Map of the tour from ' + journey().stops[0].name + ' and back, with the part travelled highlighted'"></div>`,
  styles: `
    .map {
      height: 440px;
      /* Leaflet's panes and controls have z-indexes up to 1000; keep them inside the map so it scrolls under the top bar. */
      isolation: isolate;
    }
  `,
})
export class JourneyMap {
  readonly journey = input.required<Journey>();

  private readonly element = viewChild.required<ElementRef<HTMLElement>>('map');
  private readonly map = signal<L.Map | null>(null);

  constructor() {
    const style = getComputedStyle(inject(DOCUMENT).documentElement);
    const token = (name: string) => style.getPropertyValue(name).trim();

    afterNextRender(() => {
      // Zooming with the scroll wheel would catch the page's scrolling; buttons, pinch and double-click still zoom.
      const map = L.map(this.element().nativeElement, { scrollWheelZoom: false });
      // OpenStreetMap's tiles are light, so styles.scss darkens them (dark-tiles) to match the dashboard.
      L.tileLayer('https://tile.openstreetmap.org/{z}/{x}/{y}.png', { attribution: osm, maxZoom: 19, className: 'dark-tiles' }).addTo(map);
      this.map.set(map);
    });

    let drawn: L.LayerGroup | null = null;
    let fitted = false;
    effect(() => {
      const map = this.map();
      const journey = this.journey();
      if (!map || journey.stops.length === 0) return;
      drawn?.remove();

      const points = journey.stops.map((s) => L.latLng(s.latitude, s.longitude));
      const progress = journeyProgress(journey);
      const reachedCount = journey.stops.indexOf(progress.last) + 1;
      const travelled = [...points.slice(0, reachedCount), L.latLng(...progress.position)];

      const tour = L.polyline(points, { color: token('--text-faint'), weight: 3, opacity: 0.8, dashArray: '6 8' });
      const done = L.polyline(travelled, { color: token('--accent'), weight: 4, opacity: 0.95 });
      // The start and end are the same city: one dot, named once.
      const stops = journey.stops.slice(0, -1).map((stop, i) => {
        const reached = i === 0 || stop.at <= journey.distance;
        return L.circleMarker(points[i], {
          radius: 6,
          color: reached ? token('--accent') : token('--text-faint'),
          weight: 2,
          fillColor: reached ? token('--accent') : token('--surface'),
          fillOpacity: 1,
        }).bindTooltip(`${stop.name}, ${stop.country}`, { direction: 'top', offset: [0, -6] });
      });
      const here = L.circleMarker(progress.position, { radius: 8, color: token('--text'), weight: 3, fillColor: token('--accent'), fillOpacity: 1 });
      drawn = L.layerGroup([tour, done, ...stops, here]).addTo(map);
      // Fit the tour once; switching years keeps the Athlete's own zoom and position.
      if (!fitted) map.fitBounds(tour.getBounds(), { padding: [24, 24] });
      fitted = true;
    });

    inject(DestroyRef).onDestroy(() => this.map()?.remove());
  }
}

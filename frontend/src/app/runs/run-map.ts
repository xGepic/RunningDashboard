import { DOCUMENT } from '@angular/common';
import { Component, DestroyRef, ElementRef, afterNextRender, effect, inject, input, signal, viewChild } from '@angular/core';
import * as L from 'leaflet';
import { decodePolyline } from '../shared/polyline';

const osm = '&copy; <a href="https://www.openstreetmap.org/copyright">OpenStreetMap</a> contributors';

/**
 * The map views to pick from, none of which needs an API key; each provider asks for its credit and light use only.
 * OpenStreetMap's tiles are light, so styles.scss darkens them (dark-tiles) to match the dashboard's dark theme.
 */
const mapViews: Record<string, () => L.TileLayer> = {
  Map: () => L.tileLayer('https://tile.openstreetmap.org/{z}/{x}/{y}.png', { attribution: osm, maxZoom: 19, className: 'dark-tiles' }),
  Satellite: () =>
    L.tileLayer('https://server.arcgisonline.com/ArcGIS/rest/services/World_Imagery/MapServer/tile/{z}/{y}/{x}', {
      attribution: 'Tiles &copy; Esri &mdash; Source: Esri, Maxar, Earthstar Geographics, and the GIS User Community',
      maxZoom: 19,
    }),
  Topo: () =>
    L.tileLayer('https://{s}.tile.opentopomap.org/{z}/{x}/{y}.png', {
      attribution: `${osm}, SRTM | Map style: &copy; <a href="https://opentopomap.org">OpenTopoMap</a> (CC-BY-SA)`,
      subdomains: 'abc',
      maxZoom: 17,
    }),
};

/** Where the browser remembers the last map view picked; a convenience, so failing to read or write it is fine. */
const viewKey = 'runMap.view';

/** A Run's route on a map, with its start (green) and finish (red). */
@Component({
  selector: 'app-run-map',
  template: '<div #map class="map" role="img" aria-label="Map of the route, from the green start to the red finish"></div>',
  styles: `
    .map {
      height: 380px;
      border-radius: inherit;
    }
  `,
})
export class RunMap {
  /** Google's encoded polyline, as the API gives it. */
  readonly route = input.required<string>();

  private readonly element = viewChild.required<ElementRef<HTMLElement>>('map');
  private readonly map = signal<L.Map | null>(null);

  constructor() {
    const style = getComputedStyle(inject(DOCUMENT).documentElement);
    const token = (name: string) => style.getPropertyValue(name).trim();

    afterNextRender(() => {
      // Zooming with the scroll wheel would catch the page's scrolling; buttons, pinch and double-click still zoom.
      const map = L.map(this.element().nativeElement, { scrollWheelZoom: false });
      const layers = Object.fromEntries(Object.entries(mapViews).map(([name, layer]) => [name, layer()]));
      layers[rememberedView() ?? 'Map'].addTo(map);
      L.control.layers(layers, undefined, { collapsed: false }).addTo(map);
      map.on('baselayerchange', (event: L.LayersControlEvent) => rememberView(event.name));
      this.map.set(map);
    });

    let drawn: L.LayerGroup | null = null;
    effect(() => {
      const map = this.map();
      const points = decodePolyline(this.route());
      if (!map || points.length === 0) return;
      drawn?.remove();
      const line = L.polyline(points, { color: token('--accent'), weight: 4, opacity: 0.95 });
      const end = (at: L.LatLngExpression, color: string) =>
        L.circleMarker(at, { radius: 6, color: token('--background'), weight: 2, fillColor: color, fillOpacity: 1 });
      drawn = L.layerGroup([line, end(points[0], token('--met')), end(points[points.length - 1], token('--error'))]).addTo(map);
      map.fitBounds(line.getBounds(), { padding: [24, 24] });
    });

    inject(DestroyRef).onDestroy(() => this.map()?.remove());
  }
}

function rememberedView(): string | null {
  try {
    const view = localStorage.getItem(viewKey);
    return view !== null && view in mapViews ? view : null;
  } catch {
    return null;
  }
}

function rememberView(view: string): void {
  try {
    localStorage.setItem(viewKey, view);
  } catch {
    // Without storage the map just opens on the default view next time.
  }
}

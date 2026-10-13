import { DOCUMENT } from '@angular/common';
import {
  Component,
  DestroyRef,
  ElementRef,
  afterNextRender,
  computed,
  effect,
  inject,
  input,
  linkedSignal,
  signal,
  viewChild,
} from '@angular/core';
import * as L from 'leaflet';
import { formatDuration } from '../shared/format';
import { Units } from '../shared/units';
import { decodePolyline } from '../shared/polyline';
import { Replay, ReplayStretch } from './replay';

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

/** How many times faster than real time the replay can run. */
const speeds = [30, 60, 120, 300, 600];

/** A Run's route on a map, with its start (green) and finish (red), and a replay of the Run along it. */
@Component({
  selector: 'app-run-map',
  templateUrl: './run-map.html',
  styleUrl: './run-map.scss',
})
export class RunMap {
  /** Google's encoded polyline, as the API gives it. */
  readonly route = input.required<string>();

  /** The Run's Splits, or the whole Run as one stretch without them: the replay keeps each one's pace. */
  readonly stretches = input.required<ReplayStretch[]>();

  protected readonly speeds = speeds;
  protected readonly formatDuration = formatDuration;
  /** The readout's Pace and distance in the Distance Unit. */
  protected readonly units = inject(Units);

  private readonly points = computed(() => decodePolyline(this.route()));
  protected readonly replay = computed(() => new Replay(this.points(), this.stretches()));

  /** Seconds of Moving Time into the replay; back to the start for another route. */
  protected readonly time = linkedSignal({ source: this.route, computation: () => 0 });
  protected readonly playing = linkedSignal({ source: this.route, computation: () => false });
  protected readonly speed = signal(120);
  protected readonly frame = computed(() => this.replay().at(this.time()));
  /** The dot and its trail show once the replay has been started or moved. */
  private readonly replaying = computed(() => this.playing() || this.time() > 0);

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
    const line = signal<L.Polyline | null>(null);
    effect(() => {
      const map = this.map();
      const points = this.points();
      if (!map || points.length === 0) return;
      drawn?.remove();
      const route = L.polyline(points, { color: token('--accent'), weight: 4, opacity: 0.95 });
      const end = (at: L.LatLngExpression, color: string) =>
        L.circleMarker(at, { radius: 6, color: token('--background'), weight: 2, fillColor: color, fillOpacity: 1 });
      drawn = L.layerGroup([route, end(points[0], token('--met')), end(points[points.length - 1], token('--error'))]).addTo(map);
      map.fitBounds(route.getBounds(), { padding: [24, 24] });
      line.set(route);
    });

    // The replay: the route fades, and a bright trail follows the dot.
    let trail: L.Polyline | null = null;
    let dot: L.CircleMarker | null = null;
    effect(() => {
      const map = this.map();
      const route = line();
      if (!map || !route) return;
      const replaying = this.replaying();
      route.setStyle({ opacity: replaying ? 0.35 : 0.95 });
      if (!replaying) {
        trail?.remove();
        dot?.remove();
        [trail, dot] = [null, null];
        return;
      }
      const frame = this.frame();
      trail ??= L.polyline([], { color: token('--accent-light'), weight: 5, opacity: 1 }).addTo(map);
      dot ??= L.circleMarker(frame.position, { radius: 8, color: token('--text'), weight: 3, fillColor: token('--accent'), fillOpacity: 1 }).addTo(map);
      trail.setLatLngs(frame.trail);
      dot.setLatLng(frame.position).bringToFront();
    });

    // While playing, move on every animation frame by the time passed, times the speed; stop at the finish.
    effect((onCleanup) => {
      if (!this.playing()) return;
      let last = performance.now();
      const tick = (now: number) => {
        const next = this.time() + ((now - last) / 1000) * this.speed();
        last = now;
        if (next >= this.replay().duration) {
          this.time.set(this.replay().duration);
          this.playing.set(false);
          return;
        }
        this.time.set(next);
        request = requestAnimationFrame(tick);
      };
      let request = requestAnimationFrame(tick);
      onCleanup(() => cancelAnimationFrame(request));
    });

    inject(DestroyRef).onDestroy(() => this.map()?.remove());
  }

  /** Plays or pauses; at the finish, plays again from the start. */
  protected toggle(): void {
    if (!this.playing() && this.time() >= this.replay().duration) this.time.set(0);
    this.playing.update((playing) => !playing);
  }

  protected seek(seconds: number): void {
    this.time.set(seconds);
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

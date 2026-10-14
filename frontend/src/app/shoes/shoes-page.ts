import { DatePipe } from '@angular/common';
import { httpResource } from '@angular/common/http';
import { Component, computed, inject } from '@angular/core';
import { AthleteSettings } from '../shared/athlete-settings';
import { formatOneDecimal, formatWhole } from '../shared/format';
import { shoeWear } from '../shared/shoe-wear';
import { Units } from '../shared/units';
import { reloadOnSync } from '../sync/reload-on-sync';
import { Shoe } from './shoe';

/** The Shoes' colours, in list order; they repeat past the last. */
const shoeColors = ['var(--accent)', 'var(--teal)', 'var(--amber)', 'var(--met)', '#a78bfa'];

@Component({
  selector: 'app-shoes-page',
  imports: [DatePipe],
  templateUrl: './shoes-page.html',
  styleUrl: './shoes-page.scss',
})
export class ShoesPage {
  protected readonly shoes = httpResource<Shoe[]>(() => '/api/shoes');
  /** Distances in the Distance Unit, the Maximum Distance too. */
  protected readonly units = inject(Units);
  private readonly settings = inject(AthleteSettings);

  /** Each Shoe with its share of all Shoes' distance; the first active one is the most recently used. */
  protected readonly cards = computed(() => {
    const shoes = this.shoes.value() ?? [];
    const total = shoes.reduce((sum, s) => sum + s.distance, 0);
    const latest = shoes.find((s) => !s.retired && s.lastRun);
    return shoes.map((shoe, i) => ({
      shoe,
      distance: formatWhole(this.units.fromMetres(shoe.distance)),
      share: total ? Math.round((shoe.distance / total) * 100) : 0,
      average: shoe.runs ? formatOneDecimal(this.units.fromMetres(shoe.distance / shoe.runs)) : '–',
      latest: shoe === latest,
      color: shoeColors[i % shoeColors.length],
      wear: shoe.retired ? null : this.wear(shoe),
    }));
  });

  /** Side by side once there are two Shoes to compare. */
  protected readonly comparison = computed(() => {
    const cards = this.cards();
    if (cards.length < 2) return [];
    const metric = (label: string, of: (s: Shoe) => number, format: (v: number) => string) => {
      const max = Math.max(1e-9, ...cards.map((c) => of(c.shoe)));
      return {
        label,
        rows: cards.map((c) => ({ name: c.shoe.name, color: c.color, value: format(of(c.shoe)), width: (of(c.shoe) / max) * 100 })),
      };
    };
    return [
      metric('Distance', (s) => s.distance, (v) => this.units.totalDistance(v)),
      metric('Runs', (s) => s.runs, (v) => String(v)),
      metric('Avg per Run', (s) => (s.runs ? s.distance / s.runs : 0), (v) => this.units.distance(v, 1)),
    ];
  });

  constructor() {
    reloadOnSync(() => this.shoes.reload());
  }

  /** How far a Shoe is towards the Maximum Distance, the same for every Shoe and set in Settings. */
  private wear(shoe: Shoe) {
    const maxDistance = this.settings.shoeMaximumDistance();
    const left = maxDistance - shoe.distance;
    return {
      max: this.units.totalDistance(maxDistance),
      percent: Math.min(100, (shoe.distance / maxDistance) * 100),
      status: shoeWear(shoe.distance, maxDistance),
      note: left < 0 ? `${this.units.totalDistance(-left)} over` : `${this.units.totalDistance(left)} left`,
    };
  }
}

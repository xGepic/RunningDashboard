import { DatePipe } from '@angular/common';
import { httpResource } from '@angular/common/http';
import { Component, computed } from '@angular/core';
import { formatTotalDistance } from '../shared/format';
import { reloadOnSync } from '../sync/reload-on-sync';
import { Shoe } from './shoe';

/** The Shoes' colours, in list order; they repeat past the last. */
const shoeColors = ['var(--accent)', 'var(--teal)', 'var(--amber)', 'var(--met)', '#a78bfa'];

const wholeNumber = new Intl.NumberFormat('en', { maximumFractionDigits: 0 });

/** The Maximum Distance, in metres: the same for every Shoe. */
const maxDistance = 700_000;

/** From this share of its Maximum Distance on, a Shoe is close to it. */
const nearMaxShare = 0.9;

@Component({
  selector: 'app-shoes-page',
  imports: [DatePipe],
  templateUrl: './shoes-page.html',
  styleUrl: './shoes-page.scss',
})
export class ShoesPage {
  protected readonly shoes = httpResource<Shoe[]>(() => '/api/shoes');
  protected readonly formatTotalDistance = formatTotalDistance;

  /** Each Shoe with its share of all Shoes' distance; the first active one is the most recently used. */
  protected readonly cards = computed(() => {
    const shoes = this.shoes.value() ?? [];
    const total = shoes.reduce((sum, s) => sum + s.distance, 0);
    const latest = shoes.find((s) => !s.retired && s.lastRun);
    return shoes.map((shoe, i) => ({
      shoe,
      km: wholeNumber.format(shoe.distance / 1000),
      share: total ? Math.round((shoe.distance / total) * 100) : 0,
      average: shoe.runs ? (shoe.distance / shoe.runs / 1000).toFixed(1) : '–',
      latest: shoe === latest,
      color: shoeColors[i % shoeColors.length],
      wear: shoe.retired ? null : wear(shoe),
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
      metric('Distance', (s) => s.distance, (v) => formatTotalDistance(v)),
      metric('Runs', (s) => s.runs, (v) => String(v)),
      metric('Avg per Run', (s) => (s.runs ? s.distance / s.runs : 0), (v) => `${(v / 1000).toFixed(1)} km`),
    ];
  });

  constructor() {
    reloadOnSync(() => this.shoes.reload());
  }
}

/** How far a Shoe is towards the Maximum Distance. */
function wear(shoe: Shoe) {
  const share = shoe.distance / maxDistance;
  const left = maxDistance - shoe.distance;
  return {
    maxKm: maxDistance / 1000,
    percent: Math.min(100, share * 100),
    status: left < 0 ? 'over' : share >= nearMaxShare ? 'near' : 'ok',
    note: left < 0 ? `${wholeNumber.format(-left / 1000)} km over` : `${wholeNumber.format(left / 1000)} km left`,
  };
}

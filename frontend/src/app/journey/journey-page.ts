import { httpResource } from '@angular/common/http';
import { Component, computed, inject, input, linkedSignal } from '@angular/core';
import { Router, RouterLink } from '@angular/router';
import { shortWeekLabel } from '../shared/charts';
import { currentYear } from '../shared/dates';
import { Units } from '../shared/units';
import { reloadOnSync } from '../sync/reload-on-sync';
import { JourneyPageData, tourFit, tourName } from './journey';
import { JourneyCard } from './journey-card';
import { JourneyMap } from './journey-map';

/** A year on the Virtual Journey: the card's figures, the tour on a map, and when each Stop was reached. */
@Component({
  selector: 'app-journey-page',
  imports: [JourneyCard, JourneyMap, RouterLink],
  templateUrl: './journey-page.html',
  styleUrl: './journey-page.scss',
})
export class JourneyPage {
  /** From ?year=2025; the current year without one or when malformed. */
  readonly year = input<string>();

  private readonly router = inject(Router);
  /** Distances in the Distance Unit. */
  protected readonly units = inject(Units);
  protected readonly shortWeekLabel = shortWeekLabel;

  protected readonly selectedYear = computed(() => {
    const year = this.year();
    return year && /^\d{4}$/.test(year) ? Number(year) : currentYear();
  });

  protected readonly page = httpResource<JourneyPageData>(() => ({ url: '/api/journey', params: { year: String(this.selectedYear()) } }));

  /** The years to switch between, kept while another year loads. */
  protected readonly years = linkedSignal<JourneyPageData | undefined, number[]>({
    source: () => (this.page.hasValue() ? this.page.value() : undefined),
    computation: (data, previous) => data?.years ?? previous?.value ?? [this.selectedYear()],
  });

  /** What the tour is and how long, and how a Yearly Goal fits it. */
  protected readonly subtitle = computed(() => {
    if (!this.page.hasValue()) return '';
    const { journey } = this.page.value();
    const tour = `${tourName(journey)}, ${tourFit(journey, this.units)}`;
    if (journey.finished) return `${tour}.`;
    return journey.yearlyGoal !== null
      ? `${tour}: reach the goal and you are back in ${journey.stops[0].name}.`
      : `${tour}. With a Yearly Goal for the year, you get the longest tour that fits within it.`;
  });

  /** The Stops after the start, each with how far it still is when not reached yet. */
  protected readonly stops = computed(() => {
    if (!this.page.hasValue()) return [];
    const { stops, distance } = this.page.value().journey;
    return stops.slice(1).map((stop) => ({ ...stop, toGo: stop.reached ? null : stop.at - distance }));
  });

  constructor() {
    reloadOnSync(() => this.page.reload());
  }

  protected pick(year: number): void {
    this.router.navigate([], { queryParams: { year: year === currentYear() ? null : year } });
  }
}

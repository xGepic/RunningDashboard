import { Component, computed, inject, input } from '@angular/core';
import { Router, RouterLink } from '@angular/router';
import { shortWeekLabel } from '../shared/charts';
import { formatWhole } from '../shared/format';
import { openUnlessLink } from '../shared/open-unless-link';
import { Units } from '../shared/units';
import { Journey, journeyProgress, tourFit, tourName } from './journey';

/**
 * A year on the Virtual Journey: the tour as a line with its Stops, filled up to where the Athlete is, with the
 * distance to the next Stop. On the Overview it opens the Journey page.
 */
@Component({
  selector: 'app-journey-card',
  imports: [RouterLink],
  templateUrl: './journey-card.html',
  styleUrl: './journey-card.scss',
  host: { '[class.opens-page]': 'opensPage()' },
})
export class JourneyCard {
  readonly journey = input.required<Journey>();
  /** On the Overview: the card opens the Journey page. */
  readonly opensPage = input(false);

  private readonly router = inject(Router);
  /** Distances in the Distance Unit. */
  protected readonly units = inject(Units);

  protected readonly progress = computed(() => journeyProgress(this.journey()));
  protected readonly tourName = computed(() => tourName(this.journey()));
  /** "1,590 km, picked for your 1,600 km Yearly Goal", or "2,062 km" without one. */
  protected readonly fit = computed(() => tourFit(this.journey(), this.units));

  /** "37" (km) to the next Stop, or the extra distance once the tour is complete, in the Distance Unit. */
  protected readonly headline = computed(() => {
    const progress = this.progress();
    return formatWhole(Math.ceil(this.units.fromMetres(progress.next ? progress.toNext : progress.extra)));
  });

  /** "Left Bern on 14 Sep", or "Set off from Vienna" before the first Stop. */
  protected readonly leg = computed(() => {
    const { last } = this.progress();
    return last.reached ? `Left ${last.name} on ${shortWeekLabel(last.reached.date)}` : `Set off from ${last.name}`;
  });

  /** Each Stop along the line, alternately named above and below it so close ones do not overlap. */
  protected readonly stops = computed(() => {
    const { length, distance, stops } = this.journey();
    const { last, next } = this.progress();
    return stops.map((stop, i) => ({
      name: stop.name,
      left: length > 0 ? (stop.at / length) * 100 : 0,
      reached: i > 0 && stop.at <= distance,
      above: i % 2 === 0,
      // In a narrow window only these two keep their names.
      current: stop === last || stop === next,
      edge: i === 0 ? 'first' : i === stops.length - 1 ? 'last' : null,
    }));
  });

  protected readonly share = computed(() => this.progress().share * 100);
  protected readonly formatWhole = formatWhole;

  protected open(event: MouseEvent): void {
    if (this.opensPage()) openUnlessLink(event, this.router, ['/journey']);
  }
}

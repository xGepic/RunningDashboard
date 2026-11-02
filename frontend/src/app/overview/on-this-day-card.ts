import { Component, computed, inject, input } from '@angular/core';
import { RouterLink } from '@angular/router';
import { weekLabel } from '../shared/charts';
import { formatDuration, formatOneDecimal } from '../shared/format';
import { Units } from '../shared/units';
import { OnThisDay } from './overview';

/** Today's date a year ago: the day's distance with its Runs, each opening its Run. */
@Component({
  selector: 'app-on-this-day-card',
  imports: [RouterLink],
  templateUrl: './on-this-day-card.html',
  styleUrl: './on-this-day-card.scss',
})
export class OnThisDayCard {
  readonly onThisDay = input.required<OnThisDay>();
  /** Distances in the Distance Unit. */
  protected readonly units = inject(Units);

  /** "2 Nov 2025". */
  protected readonly day = computed(() => weekLabel(this.onThisDay().day));

  /** The day's Runs together: their distance, Pace and Moving Time. */
  protected readonly total = computed(() => {
    const runs = this.onThisDay().runs;
    const distance = runs.reduce((sum, r) => sum + r.distance, 0);
    const movingTime = runs.reduce((sum, r) => sum + r.movingTime, 0);
    return {
      distance: formatOneDecimal(this.units.fromMetres(distance)),
      pace: this.units.pace(distance > 0 ? movingTime / (distance / 1000) : null),
      movingTime: formatDuration(movingTime),
    };
  });

  /** Each Run's name, with its own distance once the day has more than one. */
  protected readonly runs = computed(() => {
    const runs = this.onThisDay().runs;
    return runs.map((run) => ({ id: run.id, name: run.name, distance: runs.length > 1 ? this.units.distance(run.distance, 1) : null }));
  });
}

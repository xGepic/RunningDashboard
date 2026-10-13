import { Component, computed, inject, input } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { ActivatedRoute, RouterLink } from '@angular/router';

/** The history page shown: Runs or Rides. */
export type HistoryPage = 'runs' | 'rides';

/**
 * Switches between Run history and Ride history. The Runs' unit rides along (Rides are always in km), so coming back
 * keeps it; the rest (the Runs' month and search, sort order and page) does not carry from one list to the other.
 */
@Component({
  selector: 'app-sport-switch',
  imports: [RouterLink],
  template: `
    <nav class="segmented" aria-label="History">
      <a routerLink="/runs" [queryParams]="kept()" [attr.aria-current]="current() === 'runs' ? 'page' : null">Runs</a>
      <a routerLink="/rides" [queryParams]="kept()" [attr.aria-current]="current() === 'rides' ? 'page' : null">Rides</a>
    </nav>
  `,
})
export class SportSwitch {
  readonly current = input.required<HistoryPage>();

  private readonly queryParams = toSignal(inject(ActivatedRoute).queryParamMap);

  protected readonly kept = computed(() => {
    const params = this.queryParams();
    const unit = params?.get('unit');
    return unit ? { unit } : {};
  });
}

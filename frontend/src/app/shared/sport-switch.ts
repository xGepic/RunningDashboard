import { Component, input } from '@angular/core';
import { RouterLink } from '@angular/router';

/** The history page shown: Runs, Rides or Swims. */
export type HistoryPage = 'runs' | 'rides' | 'swims';

/**
 * Switches between Run, Ride and Swim history. Nothing carries from one list to the other (the Runs' month and
 * search, sort order and page); the Distance Unit comes from Settings.
 */
@Component({
  selector: 'app-sport-switch',
  imports: [RouterLink],
  template: `
    <nav class="segmented" aria-label="History">
      <a routerLink="/runs" [attr.aria-current]="current() === 'runs' ? 'page' : null">Runs</a>
      <a routerLink="/rides" [attr.aria-current]="current() === 'rides' ? 'page' : null">Rides</a>
      <a routerLink="/swims" [attr.aria-current]="current() === 'swims' ? 'page' : null">Swims</a>
    </nav>
  `,
})
export class SportSwitch {
  readonly current = input.required<HistoryPage>();
}

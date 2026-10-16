import { Component, booleanAttribute, computed, input } from '@angular/core';
import { Achievement, medalFace } from './achievement';

/** An Achievement as a round medal with its threshold: in the Theme's colour once earned, grey while locked. */
@Component({
  selector: 'app-achievement-medal',
  template: `
    <span class="value" [class.long]="face().value.length > 3">{{ face().value }}</span>
    <span class="unit">{{ face().unit }}</span>
  `,
  styleUrl: './achievement-medal.scss',
  host: {
    'aria-hidden': 'true',
    '[class.locked]': 'locked()',
    '[class]': 'achievement().group',
    '[style.--size.px]': 'size()',
  },
})
export class AchievementMedal {
  readonly achievement = input.required<Achievement>();
  /** Grey, for an Achievement not earned yet. */
  readonly locked = input(false, { transform: booleanAttribute });
  /** Its diameter in pixels. */
  readonly size = input(72);

  protected readonly face = computed(() => medalFace(this.achievement()));
}

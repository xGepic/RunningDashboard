import { Directive, ElementRef, effect, inject, input } from '@angular/core';
import { formatWhole } from './format';
import { prefersReducedMotion } from './motion';

/** How long a number takes to count up, in milliseconds. */
const duration = 1400;

/**
 * Counts its element's text up from 0 to `appCountUp`, slowing down towards the end, after `countUpDelay` ms. With
 * "reduce motion" the number is there at once.
 */
@Directive({ selector: '[appCountUp]' })
export class CountUp {
  readonly appCountUp = input.required<number>();
  readonly countUpFormat = input<(value: number) => string>(formatWhole);
  readonly countUpDelay = input(0);

  private readonly element = inject<ElementRef<HTMLElement>>(ElementRef).nativeElement;

  constructor() {
    effect((onCleanup) => {
      const to = this.appCountUp();
      const format = this.countUpFormat();
      if (prefersReducedMotion()) {
        this.element.textContent = format(to);
        return;
      }
      const start = performance.now() + this.countUpDelay();
      let frame = 0;
      const step = (now: number) => {
        const t = Math.min(1, Math.max(0, (now - start) / duration));
        this.element.textContent = format(to * (1 - (1 - t) ** 3));
        if (t < 1) frame = requestAnimationFrame(step);
      };
      this.element.textContent = format(0);
      frame = requestAnimationFrame(step);
      onCleanup(() => cancelAnimationFrame(frame));
    });
  }
}

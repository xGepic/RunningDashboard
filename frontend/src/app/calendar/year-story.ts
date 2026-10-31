import { DatePipe } from '@angular/common';
import { Component, ElementRef, afterNextRender, computed, inject, input, output, signal } from '@angular/core';
import { MonthTotals } from '../runs/run';
import { CountUp } from '../shared/count-up';
import { fullMonthNames, monthNames } from '../shared/dates';
import { formatDifference, formatDuration, formatOneDecimal, formatWhole, percentChange, runCount } from '../shared/format';
import { prefersReducedMotion } from '../shared/motion';
import { Units } from '../shared/units';
import { YearReview, comparedWith, isSoFar, reviewTitle } from './review';

type CardKind = 'intro' | 'distance' | 'time' | 'months' | 'longest' | 'streak' | 'bests' | 'races' | 'shoe' | 'outro';

/** How long a card stays before the next one, in seconds. */
const cardSeconds = 5;
/** Cards that bring their items in one by one stay longer for each, up to a limit. */
const secondsPerItem = 0.6;
const maxCardSeconds = 10;
/** A press held longer than this pauses instead of moving on. */
const holdMilliseconds = 250;

/**
 * A year's highlights as a full-screen story: one card after another, each moving on by itself, with its numbers
 * counting up. Cards with nothing to show for the year are left out; the last card stays until the Athlete acts.
 */
@Component({
  selector: 'app-year-story',
  imports: [CountUp, DatePipe],
  templateUrl: './year-story.html',
  styleUrl: './year-story.scss',
  host: {
    role: 'dialog',
    'aria-modal': 'true',
    '[attr.aria-label]': 'title()',
    tabindex: '-1',
    '[class.reduced-motion]': 'reducedMotion',
    '(document:keydown)': 'onKeydown($event)',
  },
})
export class YearStory {
  /** The year the story tells. */
  readonly review = input.required<YearReview>();
  /** The year's totals per month, for the bars; none while they load. */
  readonly months = input<MonthTotals[] | null>(null);
  /** The Athlete's first name, for the title. */
  readonly athleteName = input<string | null>(null);

  /** Esc, the close button or Close: the Athlete is done with the story. */
  readonly closed = output<void>();
  /** The last card's "Download image": save the Review as an image, as the Calendar's button does. */
  readonly download = output<void>();

  protected readonly units = inject(Units);
  protected readonly reducedMotion = prefersReducedMotion();

  protected readonly title = computed(() => reviewTitle(this.review(), this.athleteName()));
  protected readonly soFar = computed(() => isSoFar(this.review()));
  protected readonly comparedWith = computed(() => comparedWith(this.review()));

  protected readonly distanceChange = computed(() => {
    const { totals, previousYear } = this.review();
    return previousYear ? percentChange(totals.distance, previousYear.distance) : null;
  });

  /** January to December against the biggest month, growing one after another. */
  protected readonly monthBars = computed(() => {
    const months = this.months() ?? [];
    const biggest = Math.max(0, ...months.map((m) => m.distance));
    return months.map((m) => ({
      initial: monthNames[m.month - 1][0],
      height: biggest > 0 ? Math.max(m.distance > 0 ? 4 : 0, (m.distance / biggest) * 100) : 0,
      biggest: biggest > 0 && m.distance === biggest,
    }));
  });

  /** The year's Personal Bests, the biggest leap (in %) last. */
  protected readonly personalBests = computed(() => {
    const bests = this.review().personalBests.map((best) => {
      const leap = best.previousElapsedTime === null ? null : (best.previousElapsedTime - best.elapsedTime) / best.previousElapsedTime;
      return {
        ...best,
        leap,
        time: formatDuration(best.elapsedTime),
        gain: best.previousElapsedTime === null ? 'First time' : `−${formatDuration(best.previousElapsedTime - best.elapsedTime)} · ${((leap ?? 0) * 100).toFixed(1)}% faster`,
      };
    });
    const biggest = Math.max(0, ...bests.map((b) => b.leap ?? 0));
    const leapIndex = biggest > 0 ? bests.findIndex((b) => b.leap === biggest) : -1;
    if (leapIndex < 0) return bests.map((b) => ({ ...b, biggestLeap: false }));
    const ordered = [...bests.slice(0, leapIndex), ...bests.slice(leapIndex + 1), bests[leapIndex]];
    return ordered.map((b, i) => ({ ...b, biggestLeap: i === ordered.length - 1 }));
  });

  /** The cards with something to show, in story order. */
  protected readonly cards = computed<CardKind[]>(() => {
    const review = this.review();
    const has: Record<CardKind, boolean> = {
      intro: true,
      distance: true,
      time: true,
      months: review.biggestMonth !== null,
      longest: review.longestRun !== null,
      streak: review.longestStreak !== null,
      bests: review.personalBests.length > 0,
      races: review.goalRaces.length > 0,
      shoe: review.mostUsedShoe !== null,
      outro: true,
    };
    return (Object.keys(has) as CardKind[]).filter((kind) => has[kind]);
  });

  private readonly index = signal(0);
  /** Goes up on every move, so the card and its segment start over, even when it is the same card again. */
  private readonly step = signal(0);
  private readonly held = signal(false);
  private readonly pausedByKey = signal(false);
  protected readonly paused = computed(() => this.held() || this.pausedByKey());

  protected readonly current = computed(() => ({ kind: this.cards()[this.index()], key: this.step() }));
  protected readonly isLast = computed(() => this.index() === this.cards().length - 1);

  protected readonly segments = computed(() =>
    this.cards().map((kind, i) => ({
      key: i === this.index() ? `${i}-${this.step()}` : `${i}`,
      state: i < this.index() ? 'done' : i === this.index() ? 'active' : 'ahead',
      seconds: this.secondsFor(kind),
    })),
  );

  private pressedAt: number | null = null;
  private readonly host = inject<ElementRef<HTMLElement>>(ElementRef);

  constructor() {
    afterNextRender(() => this.host.nativeElement.focus());
  }

  next(): void {
    if (this.isLast()) return;
    this.index.update((i) => i + 1);
    this.step.update((s) => s + 1);
  }

  /** Back one card; on the first, it starts over. */
  previous(): void {
    this.index.update((i) => Math.max(0, i - 1));
    this.step.update((s) => s + 1);
  }

  replay(): void {
    this.index.set(0);
    this.pausedByKey.set(false);
    this.step.update((s) => s + 1);
  }

  /** The active segment filled up: on to the next card. */
  protected segmentDone(state: string): void {
    if (state === 'active') this.next();
  }

  protected onKeydown(event: KeyboardEvent): void {
    switch (event.key) {
      case 'ArrowRight':
        this.next();
        break;
      case 'ArrowLeft':
        this.previous();
        break;
      case ' ':
        // Space still presses a focused button, e.g. Replay.
        if ((event.target as HTMLElement | null)?.closest('button')) return;
        this.pausedByKey.update((paused) => !paused);
        break;
      case 'Escape':
        this.closed.emit();
        break;
      default:
        return;
    }
    event.preventDefault();
  }

  /** Holding the pointer down pauses; a short press moves on, or back on the left third. */
  protected onPointerDown(event: PointerEvent): void {
    if (this.isControl(event)) return;
    this.pressedAt = event.timeStamp;
    this.held.set(true);
  }

  protected onPointerUp(event: PointerEvent): void {
    if (this.pressedAt === null) return;
    const tap = event.timeStamp - this.pressedAt < holdMilliseconds;
    this.pressedAt = null;
    this.held.set(false);
    if (!tap || this.isControl(event)) return;
    const { left, width } = this.host.nativeElement.getBoundingClientRect();
    if (event.clientX - left < width / 3) this.previous();
    else this.next();
  }

  protected onPointerCancel(): void {
    this.pressedAt = null;
    this.held.set(false);
  }

  private isControl(event: Event): boolean {
    return (event.target as HTMLElement | null)?.closest('button, a') !== null;
  }

  private secondsFor(kind: CardKind): number {
    const items = kind === 'bests' ? this.review().personalBests.length : 0;
    return Math.min(maxCardSeconds, cardSeconds + items * secondsPerItem);
  }

  protected readonly fullMonthNames = fullMonthNames;
  protected readonly formatDuration = formatDuration;
  protected readonly runCount = runCount;
  protected readonly formatDifference = formatDifference;
  protected readonly formatWhole = formatWhole;
  protected readonly formatOneDecimal = formatOneDecimal;
}

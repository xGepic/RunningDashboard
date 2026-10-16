import { HttpClient, httpResource } from '@angular/common/http';
import { Component, ElementRef, Injector, afterNextRender, computed, inject, input, signal, viewChild } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import { Achievement, earnedIn } from '../achievements/achievement';
import { Connection } from '../connection/connection';
import { MonthTotals } from '../runs/run';
import { ImageDownload } from '../shared/image-download';
import { reloadOnSync } from '../sync/reload-on-sync';
import { YearReview } from './review';
import { YearReviewContent } from './year-review-content';
import { YearStory } from './year-story';

/** The image's width when the Review has none on screen. */
const fallbackImageWidth = 1080;

/** A year's highlights under the Calendar's Review tab, which can also be played as a story or saved as an image to share. */
@Component({
  selector: 'app-year-review',
  imports: [YearReviewContent, YearStory],
  templateUrl: './year-review.html',
  styleUrl: './year-review.scss',
})
export class YearReviewPanel {
  readonly year = input.required<number>();
  /** The year's totals per month, as the Calendar has them, for the bars in the headline; none while they load. */
  readonly months = input<MonthTotals[] | null>(null);

  private readonly http = inject(HttpClient);
  private readonly imageDownload = inject(ImageDownload);
  private readonly injector = inject(Injector);
  private readonly host = inject<ElementRef<HTMLElement>>(ElementRef);

  protected readonly review = httpResource<YearReview>(() => ({ url: '/api/review', params: { year: String(this.year()) } }));
  private readonly allAchievements = httpResource<Achievement[]>(() => '/api/achievements');
  /** The Achievements earned in the year; none while they load or when they could not be loaded. */
  readonly achievements = computed(() => (this.allAchievements.hasValue() ? earnedIn(this.year(), this.allAchievements.value()) : []));

  private readonly downloading = signal(false);
  /** Only a year with Runs makes an image, and only one at a time. */
  readonly canDownload = computed(() => !this.downloading() && this.review.hasValue() && this.review.value().totals.runs > 0);
  protected readonly downloadFailed = signal(false);
  /** What the image is drawn from, off screen, while it is being made. */
  protected readonly imageSource = signal<{ review: YearReview; athleteName: string | null; width: number } | null>(null);
  private readonly imageElement = viewChild<ElementRef<HTMLElement>>('imageElement');

  /** Only a year with Runs has a story. */
  readonly canPlay = computed(() => this.review.hasValue() && this.review.value().totals.runs > 0);
  /** The story while it plays, with the name it is titled with. */
  protected readonly story = signal<{ athleteName: string | null } | null>(null);

  constructor() {
    reloadOnSync(() => {
      this.review.reload();
      this.allAchievements.reload();
    });
  }

  /** Plays the year's story over the whole window, titled with the Athlete's first name. */
  async play(): Promise<void> {
    if (!this.canPlay()) return;
    this.story.set({ athleteName: await this.athleteName() });
  }

  /** Saves the Review as "Year in Review 2026.png", titled with the Athlete's first name. */
  async downloadImage(): Promise<void> {
    if (!this.canDownload()) return;
    const review = this.review.value()!;
    this.downloading.set(true);
    this.downloadFailed.set(false);
    try {
      this.imageSource.set({
        review,
        athleteName: await this.athleteName(),
        width: this.host.nativeElement.getBoundingClientRect().width || fallbackImageWidth,
      });
      await new Promise<void>((resolve) => afterNextRender(() => resolve(), { injector: this.injector }));
      await this.imageDownload.save(this.imageElement()!.nativeElement, `Year in Review ${review.year}.png`);
    } catch {
      this.downloadFailed.set(true);
    } finally {
      this.imageSource.set(null);
      this.downloading.set(false);
    }
  }

  /** The Athlete's first name from Strava; none when it can't be had, and the title is then "2026 in Runs". */
  private async athleteName(): Promise<string | null> {
    const connection = await firstValueFrom(this.http.get<Connection>('/api/connection')).catch(() => null);
    return connection?.athlete?.firstName ?? null;
  }
}

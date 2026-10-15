import { HttpClient, httpResource } from '@angular/common/http';
import { Component, ElementRef, Injector, afterNextRender, computed, inject, input, signal, viewChild } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import { Connection } from '../connection/connection';
import { MonthTotals } from '../runs/run';
import { ImageDownload } from '../shared/image-download';
import { reloadOnSync } from '../sync/reload-on-sync';
import { YearReview } from './review';
import { YearReviewContent } from './year-review-content';

/** The image's width when the Review has none on screen. */
const fallbackImageWidth = 1080;

/** A year's highlights under the Calendar's Review tab, which can also be saved as an image to share. */
@Component({
  selector: 'app-year-review',
  imports: [YearReviewContent],
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

  private readonly downloading = signal(false);
  /** Only a year with Runs makes an image, and only one at a time. */
  readonly canDownload = computed(() => !this.downloading() && this.review.hasValue() && this.review.value().totals.runs > 0);
  protected readonly downloadFailed = signal(false);
  /** What the image is drawn from, off screen, while it is being made. */
  protected readonly imageSource = signal<{ review: YearReview; athleteName: string | null; width: number } | null>(null);
  private readonly imageElement = viewChild<ElementRef<HTMLElement>>('imageElement');

  constructor() {
    reloadOnSync(() => this.review.reload());
  }

  /** Saves the Review as "Year in Review 2026.png", titled with the Athlete's first name. */
  async downloadImage(): Promise<void> {
    if (!this.canDownload()) return;
    const review = this.review.value()!;
    this.downloading.set(true);
    this.downloadFailed.set(false);
    try {
      // Without a name the image is titled "2026 in Runs".
      const connection = await firstValueFrom(this.http.get<Connection>('/api/connection')).catch(() => null);
      this.imageSource.set({
        review,
        athleteName: connection?.athlete?.firstName ?? null,
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
}

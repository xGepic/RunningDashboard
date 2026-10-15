import { DOCUMENT } from '@angular/common';
import { Injectable, inject } from '@angular/core';

/** Saves part of the page as a PNG through the browser's normal download. */
@Injectable({ providedIn: 'root' })
export class ImageDownload {
  private readonly document = inject(DOCUMENT);

  /** Draws `element` at twice its size, so the text stays sharp, and saves it as `fileName`. */
  async save(element: HTMLElement, fileName: string): Promise<void> {
    // Loaded on first use, so it stays out of the initial bundle.
    const { toPng } = await import('html-to-image');
    const link = this.document.createElement('a');
    link.href = await toPng(element, { pixelRatio: 2 });
    link.download = fileName;
    link.click();
  }
}

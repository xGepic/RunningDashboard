import { DOCUMENT } from '@angular/common';
import { Injectable, inject } from '@angular/core';
import { saveUrl } from './download';

/** Saves part of the page as a PNG through the browser's normal download. */
@Injectable({ providedIn: 'root' })
export class ImageDownload {
  private readonly document = inject(DOCUMENT);

  /**
   * Draws `element` at twice its size, so the text stays sharp, and saves it as `fileName`. Never scaled down to fit
   * the browser's canvas limit, and saved from a Blob, as browsers refuse to download large data URLs.
   */
  async save(element: HTMLElement, fileName: string): Promise<void> {
    // Loaded on first use, so it stays out of the initial bundle.
    const { toBlob } = await import('html-to-image');
    const png = await toBlob(element, { pixelRatio: 2, skipAutoScale: true });
    if (!png) throw new Error('The image could not be drawn.');
    const url = URL.createObjectURL(png);
    saveUrl(this.document, url, fileName);
    URL.revokeObjectURL(url);
  }
}

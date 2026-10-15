/** Hands what `url` points at (an object or data URL) to the browser's normal download, as `fileName`. */
export function saveUrl(document: Document, url: string, fileName: string): void {
  const link = document.createElement('a');
  link.href = url;
  link.download = fileName;
  link.click();
}

/** A spreadsheet cell, already written as text; null leaves it empty. */
export type CsvCell = string | null;

export interface CsvTable {
  header: string[];
  rows: CsvCell[][];
}

/**
 * CSV as Excel with Austrian or German settings opens it with a double-click: ";" between cells and, through
 * {@link csvNumber}, "," as the decimal separator. A cell holding ";", a quote or a line break is quoted.
 */
export function toCsv(table: CsvTable): string {
  return [table.header, ...table.rows].map((row) => row.map(cell).join(';') + '\r\n').join('');
}

function cell(value: CsvCell): string {
  if (value === null) return '';
  return /[;"\r\n]/.test(value) ? `"${value.replaceAll('"', '""')}"` : value;
}

/** A number with a decimal comma and no thousands separators, e.g. "5,08". */
export function csvNumber(value: number | null, fractionDigits: number): CsvCell {
  return value === null ? null : value.toFixed(fractionDigits).replace('.', ',');
}

/** h:mm:ss even below an hour, so Excel reads every one as a duration. */
export function csvDuration(seconds: number): CsvCell {
  const total = Math.round(seconds);
  return `${Math.floor(total / 3600)}:${pad(Math.floor((total % 3600) / 60))}:${pad(total % 60)}`;
}

/** m:ss, for a Pace or Swim Pace. */
export function csvMinutesSeconds(seconds: number | null): CsvCell {
  if (seconds === null) return null;
  const total = Math.round(seconds);
  return `${Math.floor(total / 60)}:${pad(total % 60)}`;
}

/** "2026-10-06 20:35" from a local start such as "2026-10-06T20:35:26". */
export function csvDateTime(startLocal: string): CsvCell {
  return startLocal.slice(0, 16).replace('T', ' ');
}

/** Hands the CSV to the browser to save; with a byte order mark, so Excel reads it as UTF-8 and umlauts survive. */
export function saveCsv(document: Document, fileName: string, table: CsvTable): void {
  const url = URL.createObjectURL(new Blob(['﻿' + toCsv(table)], { type: 'text/csv;charset=utf-8' }));
  const link = document.createElement('a');
  link.href = url;
  link.download = fileName;
  link.click();
  URL.revokeObjectURL(url);
}

function pad(value: number): string {
  return String(value).padStart(2, '0');
}

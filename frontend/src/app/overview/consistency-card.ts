import { Component, computed, input, linkedSignal } from '@angular/core';
import { addDays, daysBetween, isoDate, localDate, today } from '../shared/dates';
import { monthNames } from '../shared/charts';
import { formatDistance } from '../shared/format';
import { DayDistance, Streak } from './overview';

const weekdayNames = ['Sun', 'Mon', 'Tue', 'Wed', 'Thu', 'Fri', 'Sat'];

/** Shades of a day with Runs, from its share of the year's furthest day; 0 is a day without. */
const levels = 4;

/** One day of the heatmap; a day of the weeks around the year that is not in it has no date. */
interface Cell {
  /** yyyy-MM-dd; none outside the year. */
  date: string | null;
  /** 1 (Monday) to 7 (Sunday). */
  row: number;
  /** The Training Week, from 1. */
  column: number;
  level: number;
  /** Later this year: not run yet, rather than without a Run. */
  future: boolean;
  title: string;
}

/**
 * How regularly the Athlete runs: the current and the longest Streak, and a calendar heatmap of one year, a cell per
 * day shaded by its distance, Monday-first rows and a column per Training Week. The year picker starts at the current
 * year, needs no new request and leaves the Streaks, which cover the whole history, as they are.
 */
@Component({
  selector: 'app-consistency-card',
  templateUrl: './consistency-card.html',
  styleUrl: './consistency-card.scss',
})
export class ConsistencyCard {
  /** Every local date with Runs and its distance in metres. */
  readonly days = input.required<DayDistance[]>();

  /** The current and the longest Streak; they cover the whole history, whichever year is shown. */
  readonly streak = input.required<Streak>();

  protected readonly formatWeeks = formatWeeks;

  /**
   * "6 weeks · 3 Mar – 13 Apr 2025": from the first Monday to the last Sunday, or to today while it is the current
   * Streak; none without Runs.
   */
  protected readonly longestStreak = computed(() => {
    const longest = this.streak().longest;
    if (!longest) return null;
    const first = localDate(longest.first);
    const lastSunday = addDays(localDate(longest.last), 6);
    const now = today();
    const last = lastSunday > now ? now : lastSunday;
    const from = first.getFullYear() === last.getFullYear() ? dayAndMonth(first) : `${dayAndMonth(first)} ${first.getFullYear()}`;
    return `${formatWeeks(longest.weeks)} · ${from} – ${dayAndMonth(last)} ${last.getFullYear()}`;
  });

  protected readonly levels = Array.from({ length: levels + 1 }, (_, level) => level);

  private readonly currentYear = today().getFullYear();

  /** The current year and the years with Runs, newest first. */
  protected readonly years = computed(() =>
    [...new Set([this.currentYear, ...this.days().map((d) => Number(d.date.slice(0, 4)))])].sort((a, b) => b - a),
  );

  /** Starts at the current year, and keeps the chosen year when the days reload. */
  protected readonly selectedYear = linkedSignal<number[], number>({
    source: this.years,
    computation: (years, previous) => (previous && years.includes(previous.value) ? previous.value : this.currentYear),
  });

  /** The chosen year's days, from the Monday on or before 1 Jan to the Sunday on or after 31 Dec. */
  protected readonly heatmap = computed(() => {
    const year = this.selectedYear();
    const distances = new Map(this.days().filter((d) => d.date.startsWith(`${year}-`)).map((d) => [d.date, d.distance]));
    const furthest = Math.max(0, ...distances.values());
    const now = today();
    const newYear = new Date(year, 0, 1);
    const newYearsEve = new Date(year, 11, 31);
    const first = addDays(newYear, -mondayFirst(newYear));
    const last = addDays(newYearsEve, 6 - mondayFirst(newYearsEve));
    const cells: Cell[] = [];
    const months: { name: string; column: number }[] = [];
    for (let i = 0; i <= daysBetween(first, last); i++) {
      const day = addDays(first, i);
      const row = mondayFirst(day) + 1;
      const column = Math.floor(i / 7) + 1;
      if (day.getFullYear() !== year) {
        cells.push({ date: null, row, column, level: 0, future: false, title: '' });
        continue;
      }
      const date = isoDate(day);
      const distance = distances.get(date);
      const future = day > now;
      const label = `${weekdayNames[day.getDay()]} ${day.getDate()} ${monthNames[day.getMonth()]} ${year}`;
      cells.push({
        date,
        row,
        column,
        // Any day with Runs is shaded, even one whose Runs add up to nothing.
        level: distance === undefined ? 0 : Math.max(1, Math.ceil((distance / (furthest || 1)) * levels)),
        future,
        title: distance !== undefined ? `${label}: ${formatDistance(distance)}` : future ? label : `${label}: no Run`,
      });
      if (day.getDate() === 1) months.push({ name: monthNames[day.getMonth()], column });
    }
    return { cells, months, columns: cells.length / 7, daysWithRuns: distances.size };
  });
}

/** "1 week", "3 weeks". */
function formatWeeks(count: number): string {
  return `${count} ${count === 1 ? 'week' : 'weeks'}`;
}

/** "3 Mar". */
function dayAndMonth(day: Date): string {
  return `${day.getDate()} ${monthNames[day.getMonth()]}`;
}

/** 0 for Monday to 6 for Sunday. */
function mondayFirst(day: Date): number {
  return (day.getDay() + 6) % 7;
}


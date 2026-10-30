import { Component, computed, input } from '@angular/core';
import { Break, breakLabel, breaksInWeek, kindLabel } from '../shared/breaks';
import { shortWeekLabel, weekLabel } from '../shared/charts';
import { daysSinceMonday, today } from '../shared/dates';
import { Streak } from './overview';

/**
 * The Streak: how many Training Weeks in a row have a Run, and what this week still needs to keep it up. A week without
 * a Run in a Break is named by it; the Streak counts as it would without Breaks.
 */
@Component({
  selector: 'app-streak-card',
  templateUrl: './streak-card.html',
  styleUrl: './streak-card.scss',
})
export class StreakCard {
  readonly streak = input.required<Streak>();
  readonly breaks = input<readonly Break[]>([]);

  /** "week" or "weeks", for the current Streak. */
  protected readonly unit = computed(() => weeks(this.streak().current));

  /** What this week means for the Streak; worked out again whenever the Streak is reloaded, so a new day counts. */
  protected readonly status = computed(() => {
    const { current, thisWeekHasRun, recentWeeks } = this.streak();
    if (thisWeekHasRun) return { text: 'This week ✓', warning: false };
    const thisWeek = recentWeeks.at(-1);
    const onBreak = thisWeek ? breaksInWeek(this.breaks(), thisWeek.start)[0] : undefined;
    if (onBreak) return { text: `Break this week: ${breakLabel(onBreak)}`, warning: false };
    if (current === 0) {
      // The week that ended it, if it was in a Break.
      const lastWeek = recentWeeks.at(-2);
      const endedBy = lastWeek && !lastWeek.hasRun ? breaksInWeek(this.breaks(), lastWeek.start)[0] : undefined;
      return endedBy
        ? { text: `The Streak ended in a Break (${kindLabel(endedBy.kind)}) · Run this week to start a new one`, warning: false }
        : { text: 'Run this week to start a new Streak', warning: false };
    }
    // Days of this Training Week still to come, today included: 7 on Monday, 1 on Sunday.
    const daysLeft = 7 - daysSinceMonday(today());
    if (daysLeft === 1) return { text: 'Last day to keep the Streak', warning: true };
    return { text: `This week still needs a Run · ${daysLeft} days left`, warning: false };
  });

  /** The recent weeks as squares; the last is the current week, outlined (pending) until it has a Run. */
  protected readonly recentWeeks = computed(() => {
    const recent = this.streak().recentWeeks;
    return recent.map((week, i) => {
      const isThisWeek = i === recent.length - 1;
      const label = isThisWeek ? 'This week' : `Week of ${shortWeekLabel(week.start)}`;
      const onBreak = week.hasRun ? undefined : breaksInWeek(this.breaks(), week.start)[0];
      const run = week.hasRun ? 'Run' : isThisWeek ? 'no Run yet' : 'no Run';
      return {
        start: week.start,
        hasRun: week.hasRun,
        pending: isThisWeek && !week.hasRun,
        breakKind: onBreak ? `break-${onBreak.kind}` : '',
        title: `${label}: ${run}${onBreak ? ` · ${breakLabel(onBreak)}` : ''}`,
      };
    });
  });

  protected readonly recentLabel = computed(() => {
    const recent = this.streak().recentWeeks;
    const onBreak = this.recentWeeks().filter((w) => w.breakKind).length;
    const label = `The last ${recent.length} Training ${recent.length === 1 ? 'Week' : 'Weeks'}: ${recent.filter((w) => w.hasRun).length} with a Run`;
    return onBreak ? `${label}, ${onBreak} without one in a Break` : label;
  });

  /** None while the current Streak is the longest; it ties a past one at most, and then it counts as the longest. */
  protected readonly longest = computed(() => {
    const { current, longest } = this.streak();
    if (current === longest.weeks) return null;
    return `${longest.weeks} ${weeks(longest.weeks)} · ${dateRange(longest.start, longest.end)}`;
  });
}

function weeks(count: number): string {
  return count === 1 ? 'week' : 'weeks';
}

/** "6 Jan – 15 Jun 2025", or "30 Dec 2024 – 15 Jun 2025" across a year's end; from two yyyy-MM-dd dates. */
function dateRange(start: string, end: string): string {
  const sameYear = start.slice(0, 4) === end.slice(0, 4);
  return `${sameYear ? shortWeekLabel(start) : weekLabel(start)} – ${weekLabel(end)}`;
}

import { addDays, isoDate, localDate, monthNames, mondayOf } from '../shared/dates';

/** One day of Fitness & Freshness, as GET /api/fitness/freshness gives it, each figure to one decimal. */
export interface FreshnessDay {
  /** yyyy-MM-dd. */
  date: string;
  /** The day's Training Load: the Relative Effort of its Runs, summed. */
  load: number;
  fitness: number;
  fatigue: number;
  /** Fitness minus Fatigue. */
  form: number;
  /** The day's hardest Run, by Relative Effort; none on a day without a Run. */
  runId: number | null;
  runName: string | null;
}

export interface FreshnessRace {
  date: string;
  name: string;
}

export interface Freshness {
  /** Every day from the first Run's up to today, oldest first. */
  days: FreshnessDay[];
  /** Every Goal Race, by date. */
  races: FreshnessRace[];
}

export type FreshnessRange = '3m' | '6m' | '1y' | 'all';

/** The spans to look at, up to today. */
export const freshnessRanges: readonly { key: FreshnessRange; label: string; months: number | null }[] = [
  { key: '3m', label: '3M', months: 3 },
  { key: '6m', label: '6M', months: 6 },
  { key: '1y', label: '1Y', months: 12 },
  { key: 'all', label: 'All', months: null },
];

/** The days within the last months of the range, up to the last day; all of them for All. */
export function daysIn(days: readonly FreshnessDay[], range: FreshnessRange): FreshnessDay[] {
  const months = freshnessRanges.find((r) => r.key === range)!.months;
  if (months === null || days.length === 0) return [...days];
  const last = localDate(days[days.length - 1].date);
  const from = isoDate(new Date(last.getFullYear(), last.getMonth() - months, last.getDate() + 1));
  return days.filter((day) => day.date >= from);
}

/** Days before a Goal Race in which Fatigue should be coming down below Fitness. */
const taperDays = 21;

/** Fatigue over Fitness above this is ramping up too fast; the acute:chronic workload ratio's injury-risk line. */
const rampingRatio = 1.5;

/** Fatigue over Fitness below this is training clearly less than the recent average. */
const restingRatio = 0.8;

/**
 * Where the Athlete is in their training today, from the whole history: Fatigue against Fitness (as the acute:chronic
 * workload ratio), or, with a Goal Race in the next three weeks, whether they are tapering for it.
 */
export function freshnessPhase(all: readonly FreshnessDay[], races: readonly FreshnessRace[]): string | null {
  const last = all.at(-1);
  if (!last) return null;
  if (all.length < 42) return 'It takes about six weeks of Runs before Fitness and Fatigue settle, so this is only a first look.';
  if (last.fitness < 1) return "You haven't trained for a while, so your Fitness is close to zero.";

  const ratio = last.fatigue / last.fitness;
  const race = races.find((r) => r.date >= last.date && daysBetweenDates(last.date, r.date) <= taperDays);
  if (race) {
    const when = raceWhen(race, last.date);
    return ratio < 1
      ? `You're tapering for ${race.name} ${when}: Fatigue is below Fitness, so you'll arrive fresh.`
      : `${race.name} is ${when} and you're still training harder than your recent average; easing off now lets you arrive fresh.`;
  }
  if (ratio > rampingRatio)
    return "You're ramping up fast: Fatigue is well above Fitness. Big jumps like this are when injuries happen, so take it easier for a few days.";
  if (ratio > 1) return "You're building: training harder than your recent average, so your Fitness is climbing.";
  if (ratio >= restingRatio) return "You're holding steady: training about as much as your recent average.";
  return "You're recovering: training less than your recent average, so your Fitness is dropping.";
}

/** "in 12 days", "tomorrow" or "today". */
function raceWhen(race: FreshnessRace, today: string): string {
  const days = daysBetweenDates(today, race.date);
  return days === 0 ? 'today' : days === 1 ? 'tomorrow' : `in ${days} days`;
}

function daysBetweenDates(from: string, to: string): number {
  return Math.round((localDate(to).getTime() - localDate(from).getTime()) / 86_400_000);
}

/**
 * The hardest days shown, to name on the chart: up to three days with at least twice the load of a typical day with
 * a Run (the median), hardest first, each at least an eighth of the days shown away from a harder one, so their
 * names do not collide.
 */
export function hardestDays(days: readonly FreshnessDay[]): FreshnessDay[] {
  const runDays = days.filter((day) => day.load > 0);
  if (runDays.length === 0) return [];
  const loads = runDays.map((day) => day.load).sort((a, b) => a - b);
  const typical = loads[Math.floor(loads.length / 2)];
  const apart = Math.max(7, Math.round(days.length / 8));
  const picked: FreshnessDay[] = [];
  for (const day of [...runDays].sort((a, b) => b.load - a.load)) {
    if (picked.length === 3 || day.load < 2 * typical) break;
    const at = localDate(day.date).getTime();
    if (picked.every((other) => Math.abs(localDate(other.date).getTime() - at) >= apart * 86_400_000)) picked.push(day);
  }
  return picked;
}

/** A Training Week of Fitness & Freshness: its summed load, and the figures on its last day shown. */
export interface FreshnessWeek {
  /** The Monday. */
  start: string;
  load: number;
  fitness: number;
  fatigue: number;
  form: number;
}

/** The days by Training Week, newest first; the first and last weeks may be partial. */
export function freshnessWeeks(days: readonly FreshnessDay[]): FreshnessWeek[] {
  const weeks = new Map<string, FreshnessWeek>();
  for (const day of days) {
    const start = isoDate(mondayOf(localDate(day.date)));
    const week = weeks.get(start);
    weeks.set(start, { start, load: (week?.load ?? 0) + day.load, fitness: day.fitness, fatigue: day.fatigue, form: day.form });
  }
  return [...weeks.values()].reverse();
}

/** The Goal Races within the days shown. */
export function racesIn(races: readonly FreshnessRace[], days: readonly FreshnessDay[]): FreshnessRace[] {
  if (days.length === 0) return [];
  const [from, to] = [days[0].date, days[days.length - 1].date];
  return races.filter((race) => race.date >= from && race.date <= to);
}

/** "52" or "−4": whole numbers, with a true minus sign; "+3" when signed. */
export function formatLoad(value: number, signed = false): string {
  const whole = Math.round(value);
  const sign = whole < 0 ? '−' : signed && whole > 0 ? '+' : '';
  return `${sign}${Math.abs(whole)}`;
}

/** "27 Sep", with the year when it is not that of <code>today</code>: "30 Oct 2024". */
export function dayLabel(date: string, today: string): string {
  const [year, month, day] = date.split('-').map(Number);
  return `${day} ${monthNames[month - 1]}${date.slice(0, 4) === today.slice(0, 4) ? '' : ` ${year}`}`;
}

/** The day after a yyyy-MM-dd date, for the end of a band over that day. */
export function dayAfter(date: string): string {
  return isoDate(addDays(localDate(date), 1));
}

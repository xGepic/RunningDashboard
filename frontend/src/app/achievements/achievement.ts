import { daysBetween, localDate, today } from '../shared/dates';
import { Units } from '../shared/units';

/** GET /api/achievements: every Achievement, by group in the order of `achievementGroups` and by threshold within it. */
export interface Achievement {
  /** Stable, e.g. "lifetime-1000000". */
  id: string;
  group: AchievementGroup;
  /** E.g. "Half-Marathon" or "1,000 km year". */
  name: string;
  /** The threshold, in metres. */
  distance: number;
  /** Earned again by every year, month or Training Week that reaches it. */
  repeatable: boolean;
  /** Each time it was earned, oldest first; empty while it is locked. */
  earned: AchievementEarned[];
  /**
   * Metres toward it, only for the next locked Achievement of its group: the longest Run, all Runs' distance, or the
   * current year's, month's or Training Week's so far.
   */
  progress: number | null;
}

/** What an Achievement counts: one Run's Best Effort, all Runs, or a calendar year's, month's or Training Week's Runs. */
export type AchievementGroup = 'singleRun' | 'lifetime' | 'year' | 'month' | 'week';

/** One time an Achievement was earned: by the Run that reached its threshold. */
export interface AchievementEarned {
  runId: number;
  runName: string;
  /** Local start of the Run that earned it. */
  date: string;
}

/**
 * The groups in the order the API lists them. `unit` is what a medal shows under its threshold; `progressPrefix` says
 * what the progress toward its next one measures, where that isn't the group's own distance.
 */
export const achievementGroups: { group: AchievementGroup; title: string; description: string; unit: string; progressPrefix: string }[] = [
  { group: 'singleRun', title: 'Single Run', description: 'A Best Effort over the distance in one Run', unit: 'K', progressPrefix: 'Longest Run ' },
  { group: 'lifetime', title: 'Lifetime', description: 'All Runs added up', unit: 'km', progressPrefix: '' },
  { group: 'year', title: 'Year', description: 'In one calendar year, earned again every year', unit: 'km / year', progressPrefix: '' },
  { group: 'month', title: 'Month', description: 'In one calendar month, earned again every month', unit: 'km / month', progressPrefix: '' },
  { group: 'week', title: 'Training Week', description: 'In one Monday – Sunday week, earned again every week', unit: 'km / week', progressPrefix: '' },
];

/** An Achievement earned this many days ago or less is new. */
const newDays = 7;

/** Up to one decimal, with thousands separators: "3,164.5", "5,000", "21.1". */
const upToOneDecimal = new Intl.NumberFormat('en', { maximumFractionDigits: 1 });

/** What a medal says: the threshold in km big, and what it counts small. */
export function medalFace(achievement: Achievement): { value: string; unit: string } {
  return { value: upToOneDecimal.format(achievement.distance / 1000), unit: groupOf(achievement).unit };
}

/** The most recent time it was earned; none while it is locked. */
export function lastEarned(achievement: Achievement): AchievementEarned | null {
  return achievement.earned.at(-1) ?? null;
}

/**
 * The most recently earned Achievement, and how many others the same Run earned; of those, the one first in the
 * groups' order (a Single Run before a Training Week). None while none is earned.
 */
export function latestAchievement(achievements: Achievement[]): { achievement: Achievement; earned: AchievementEarned; others: number; isNew: boolean } | null {
  const latest = achievements
    .map((achievement) => ({ achievement, earned: lastEarned(achievement) }))
    .filter((a): a is { achievement: Achievement; earned: AchievementEarned } => a.earned !== null);
  if (!latest.length) return null;
  const date = latest.map((a) => a.earned.date).reduce((a, b) => (b > a ? b : a));
  const byThatRun = latest.filter((a) => a.earned.date === date);
  return { ...byThatRun[0], others: byThatRun.length - 1, isNew: daysBetween(localDate(date), today()) <= newDays };
}

/** The locked Achievement closest to being earned: the highest share of its threshold. None once all are earned. */
export function closestAchievement(achievements: Achievement[]): Achievement | null {
  return achievements
    .filter((a) => a.progress !== null)
    .reduce<Achievement | null>((best, a) => (best === null || share(a) > share(best) ? a : best), null);
}

/**
 * How far the next locked Achievement of its group is: a share for its bar, and "3,164.5 of 5,000 km" in the Distance
 * Unit. None for the others.
 */
export function achievementProgress(achievement: Achievement, units: Units): { percent: number; text: string } | null {
  if (achievement.progress === null) return null;
  const [progress, threshold] = [achievement.progress, achievement.distance].map((m) => upToOneDecimal.format(units.fromMetres(m)));
  return { percent: share(achievement) * 100, text: `${groupOf(achievement).progressPrefix}${progress} of ${threshold} ${units.unit()}` };
}

/** 0 to 1: how much of its threshold the progress covers. */
function share(achievement: Achievement): number {
  return Math.min(1, (achievement.progress ?? 0) / achievement.distance);
}

function groupOf(achievement: Achievement): (typeof achievementGroups)[number] {
  return achievementGroups.find((g) => g.group === achievement.group)!;
}

/** An Achievement earned in a calendar year, as the Year in Review shows it. */
export interface YearAchievement {
  achievement: Achievement;
  /** How often it was earned that year; always 1 for one that is earned once. */
  times: number;
  /** The last time it was earned that year. */
  last: AchievementEarned;
  /** Whether the year earned it for the first time ever. */
  firstTime: boolean;
}

/** The Achievements earned in a calendar year, in the API's order. */
export function earnedIn(year: number, achievements: Achievement[]): YearAchievement[] {
  const inYear = (earned: AchievementEarned) => Number(earned.date.slice(0, 4)) === year;
  return achievements
    .map((achievement) => ({ achievement, earned: achievement.earned.filter(inYear) }))
    .filter((a) => a.earned.length > 0)
    .map(({ achievement, earned }) => ({ achievement, times: earned.length, last: earned.at(-1)!, firstTime: inYear(achievement.earned[0]) }));
}

/**
 * GET /api/review?year=: one calendar year's highlights. Distances and elevation in metres, times in seconds, dates
 * local. Training Weeks across New Year count only with their days inside the year.
 */
export interface YearReview {
  year: number;
  /** 1 Jan, or the first Run's day when the history starts within the year. */
  from: string;
  /** 31 Dec, or today for the current year. */
  until: string;
  totals: ReviewTotals;
  /** The year before up to the same day, so the current year so far meets the same time last year; none without Runs then. */
  previousYear: ReviewTotals | null;
  longestRun: ReviewRun | null;
  biggestWeek: ReviewWeek | null;
  biggestMonth: ReviewMonth | null;
  /** For each distance, the year's fastest Best Effort, if it was a Personal Best when run. */
  personalBests: ReviewPersonalBest[];
  longestStreak: ReviewStreak | null;
  goalRaces: ReviewGoalRace[];
  mostUsedShoe: ReviewShoe | null;
  /** Listed, never counted toward anything. */
  rides: number;
  swims: number;
}

/** "Stefan's 2026 in Runs", or "2026 in Runs" without the Athlete's name. */
export function reviewTitle(review: YearReview, athleteName: string | null): string {
  return `${athleteName ? `${athleteName}'s ` : ''}${review.year} in Runs`;
}

/** Whether it is the current year, which is reviewed so far. */
export function isSoFar(review: YearReview): boolean {
  return !review.until.endsWith('-12-31');
}

/** "vs. 2025", or "vs. the same time in 2025" for the current year. */
export function comparedWith(review: YearReview): string {
  return isSoFar(review) ? `vs. the same time in ${review.year - 1}` : `vs. ${review.year - 1}`;
}

export interface ReviewTotals {
  runs: number;
  distance: number;
  movingTime: number;
  elevationGain: number;
}

export interface ReviewRun {
  id: number;
  name: string;
  startLocal: string;
  distance: number;
  movingTime: number;
}

/** Its Monday and Sunday, cut to 1 Jan or 31 Dec. */
export interface ReviewWeek {
  start: string;
  end: string;
  runs: number;
  distance: number;
}

export interface ReviewMonth {
  /** 1 for January. */
  month: number;
  runs: number;
  distance: number;
}

export interface ReviewPersonalBest {
  /** Strava's name for the distance, e.g. "5K". */
  name: string;
  distance: number;
  elapsedTime: number;
  /** The Personal Best the year started with; none if the year set the first. */
  previousElapsedTime: number | null;
  runId: number;
  runName: string;
  startLocal: string;
}

/** The longest Streak within the year: its first Monday and last Sunday, cut to 1 Jan or 31 Dec. */
export interface ReviewStreak {
  weeks: number;
  start: string;
  end: string;
}

export interface ReviewGoalRace {
  id: number;
  name: string;
  date: string;
  distance: number;
  targetTime: number | null;
  raceResult: {
    runId: number;
    runName: string;
    /** None while the Run's Best Efforts are still to come. */
    elapsedTime: number | null;
    source: 'bestEffort' | 'elapsedTime' | 'pending';
    /** Seconds over (positive) or under (negative) the Target Time. */
    differenceFromTarget: number | null;
  } | null;
}

/** Distance from the year's Runs only. */
export interface ReviewShoe {
  name: string;
  runs: number;
  distance: number;
}

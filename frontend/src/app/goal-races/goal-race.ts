/** A Goal Race as returned by GET /api/goal-races, newest first. Distance in metres, Target Time in seconds. */
export interface GoalRace {
  id: number;
  name: string;
  /** yyyy-MM-dd. */
  date: string;
  distance: number;
  targetTime: number | null;
  /** Training Weeks in the block, including the Race Week, as long as the race has no Training Plan. */
  trainingBlockWeeks: number;
  /** The Training Plan's length, which is then the block's; none without a plan. */
  trainingPlanLength: number | null;
  /** The Monday the Training Block starts, yyyy-MM-dd. */
  trainingBlockStart: string;
  /** The Sunday of the Race Week, yyyy-MM-dd. */
  trainingBlockEnd: string;
  /** None without a Run on race day. */
  raceResult: RaceResult | null;
}

/** How a Goal Race went, from the longest Run on race day. */
export interface RaceResult {
  runId: number;
  runName: string;
  /** Seconds; none while pending. */
  elapsedTime: number | null;
  /**
   * The Run's Best Effort over the race distance, the whole Run's elapsed time when it has none, or pending while the
   * Run's Best Efforts are still being loaded from Strava.
   */
  source: 'bestEffort' | 'elapsedTime' | 'pending';
  /** Seconds over (positive) or under (negative) the Target Time; none without one. */
  differenceFromTarget: number | null;
}

/** What POST and PUT /api/goal-races take. */
export interface GoalRaceRequest {
  name: string;
  date: string | null;
  distance: number | null;
  targetTime: number | null;
  trainingBlockWeeks: number | null;
}

/** Distances the form offers before a custom one, in metres. */
export const distancePresets = [
  { name: 'Marathon', distance: 42195 },
  { name: 'Half marathon', distance: 21097.5 },
  { name: '10K', distance: 10000 },
  { name: '5K', distance: 5000 },
] as const;

/** Target Time ÷ distance, in seconds per km; none without a Target Time. */
export function targetPaceOf(race: { distance: number; targetTime: number | null }): number | null {
  return race.targetTime === null ? null : race.targetTime / (race.distance / 1000);
}

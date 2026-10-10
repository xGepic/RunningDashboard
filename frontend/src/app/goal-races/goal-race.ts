/** A Goal Race as returned by GET /api/goal-races, newest first. Distance in metres, Target Time in seconds. */
export interface GoalRace {
  id: number;
  name: string;
  /** yyyy-MM-dd. */
  date: string;
  distance: number;
  targetTime: number | null;
  /** Training Weeks in the block, including the Race Week. */
  trainingBlockWeeks: number;
  /** The Monday the Training Block starts, yyyy-MM-dd. */
  trainingBlockStart: string;
  /** The Sunday of the Race Week, yyyy-MM-dd. */
  trainingBlockEnd: string;
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

/** As returned by GET /api/training. Distances in metres, times in seconds, paces in seconds per km. */
export interface Training {
  /** "block": a Goal Race's Training Block; "none": today is in no Training Block. */
  view: 'block' | 'none';
  block: TrainingBlock | null;
}

export interface TrainingBlock {
  goalRace: {
    id: number;
    name: string;
    /** yyyy-MM-dd. */
    date: string;
    distance: number;
    targetTime: number | null;
  };
  /** Target Time ÷ Goal Race distance in whole seconds, rounded down so running it beats the Target Time; none without one. */
  targetPace: number | null;
  /** First week to Race Week. */
  weeks: TrainingBlockWeek[];
}

export interface TrainingBlockWeek {
  /** 0 for the Race Week, −1 for the week before, and so on. */
  weeksToRace: number;
  /** The Monday, yyyy-MM-dd. */
  start: string;
  /** The Sunday, yyyy-MM-dd. */
  end: string;
  /** "current": today is in this week. */
  status: 'past' | 'current' | 'future';
  /** What was run; none for a future week. */
  actual: WeekActual | null;
}

export interface WeekActual {
  runs: number;
  distance: number;
  movingTime: number;
  /** Sum over the week's Runs; none if no Run has one. */
  relativeEffort: number | null;
  /** The longest Run of the week; none in a week without Runs. */
  longRun: LongRun | null;
}

export interface LongRun {
  runId: number;
  name: string;
  startLocal: string;
  distance: number;
  pace: number | null;
  /** Whole seconds per km slower (positive) or faster (negative) than the Target Pace; none without a Target Time. */
  paceVsTarget: number | null;
}

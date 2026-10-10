export type PhaseType = 'build' | 'peak' | 'taper';

/** A Goal Race's Training Plan as returned by GET and PUT /api/goal-races/{id}/training-plan. */
export interface TrainingPlan {
  goalRaceId: number;
  /** Training Weeks, including the Race Week; it is the Training Block's length. */
  length: number;
  /** First to last. */
  phases: Phase[];
  /** First week to Race Week. */
  weeks: TrainingPlanWeek[];
}

/** 4 consecutive Training Weeks with one purpose. */
export interface Phase {
  type: PhaseType;
  firstWeeksToRace: number;
  lastWeeksToRace: number;
}

export interface TrainingPlanWeek {
  /** 0 for the Race Week, −1 for the week before, and so on. */
  weeksToRace: number;
  /** The Monday, yyyy-MM-dd. */
  start: string;
  /** The Sunday, yyyy-MM-dd. */
  end: string;
  /** The type of the Phase the week is in. */
  phase: PhaseType;
  /** Metres; null until planned. */
  plannedDistance: number | null;
  /** Metres; null until planned. */
  plannedLongRun: number | null;
  /** Monday first. */
  runningDays: Weekday[];
  recoveryWeek: boolean;
}

/** What PUT /api/goal-races/{id}/training-plan takes. */
export interface TrainingPlanRequest {
  length: number;
  phases: PhaseType[];
}

/** What PUT /api/goal-races/{id}/training-plan/weeks takes: the weeks to plan, each at most once. */
export interface TrainingPlanWeeksRequest {
  weeks: TrainingPlanWeekRequest[];
}

/** One week to plan: what PUT .../weeks takes for it. */
export type TrainingPlanWeekRequest = Pick<
  TrainingPlanWeek,
  'weeksToRace' | 'plannedDistance' | 'plannedLongRun' | 'runningDays' | 'recoveryWeek'
>;

/** A day of a Training Week, as the API names it. */
export type Weekday = 'monday' | 'tuesday' | 'wednesday' | 'thursday' | 'friday' | 'saturday' | 'sunday';

/** The days of a Training Week, Monday first. */
export const weekdays: { day: Weekday; name: string; initial: string }[] = [
  { day: 'monday', name: 'Monday', initial: 'M' },
  { day: 'tuesday', name: 'Tuesday', initial: 'T' },
  { day: 'wednesday', name: 'Wednesday', initial: 'W' },
  { day: 'thursday', name: 'Thursday', initial: 'T' },
  { day: 'friday', name: 'Friday', initial: 'F' },
  { day: 'saturday', name: 'Saturday', initial: 'S' },
  { day: 'sunday', name: 'Sunday', initial: 'S' },
];

export const planLengths = [12, 16, 20] as const;

export const phaseWeeks = 4;

export const phaseTypes: { type: PhaseType; name: string }[] = [
  { type: 'build', name: 'Build' },
  { type: 'peak', name: 'Peak' },
  { type: 'taper', name: 'Taper' },
];

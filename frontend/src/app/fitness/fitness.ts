/** GET /api/fitness: every Fitness Run's figures over the whole history, oldest first, and their monthly averages. */
export interface Fitness {
  /** Up to this much Cardiac Drift (a fraction) is good. */
  goodCardiacDrift: number;
  runs: FitnessRun[];
  months: FitnessMonth[];
}

export interface FitnessRun {
  id: number;
  name: string;
  startLocal: string;
  /** Metres. */
  distance: number;
  /** Metres per heartbeat; none without heart rate. */
  aerobicEfficiency: number | null;
  /** A fraction, 0.05 being 5%; only for a week's Long Run of at least 10 km. */
  cardiacDrift: number | null;
  /** Steps per minute, both feet together. */
  cadence: number | null;
}

/** The averages of a month's Fitness Runs, of those that have each figure. */
export interface FitnessMonth {
  year: number;
  /** 1 for January. */
  month: number;
  aerobicEfficiency: number | null;
  cardiacDrift: number | null;
  cadence: number | null;
}

/** Shown with each figure: "1.19 m/beat", "4.8%", "168 spm". */
export const formatEfficiency = (value: number) => `${value.toFixed(2)} m/beat`;
export const formatDrift = (value: number) => `${(value * 100).toFixed(1)}%`;
export const formatCadence = (value: number) => `${Math.round(value)} spm`;

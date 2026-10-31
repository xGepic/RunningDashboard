/** GET /api/fitness: every Fitness Run's Cadence over the whole history, oldest first, and the monthly averages. */
export interface Fitness {
  runs: FitnessRun[];
  months: FitnessMonth[];
}

export interface FitnessRun {
  id: number;
  name: string;
  startLocal: string;
  /** Metres. */
  distance: number;
  /** Steps per minute, both feet together. */
  cadence: number | null;
}

/** The average Cadence of a month's Fitness Runs that have one. */
export interface FitnessMonth {
  year: number;
  /** 1 for January. */
  month: number;
  cadence: number | null;
}

/** Shown with each figure, on the Fitness page and a Run's page: "1.19 m/beat", "4.8%", "168 spm". */
export const formatEfficiency = (value: number) => `${value.toFixed(2)} m/beat`;
export const formatDrift = (value: number) => `${(value * 100).toFixed(1)}%`;
export const formatCadence = (value: number) => `${Math.round(value)} spm`;

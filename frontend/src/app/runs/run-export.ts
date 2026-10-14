import { CsvTable, csvDateTime, csvDuration, csvMinutesSeconds, csvNumber } from '../shared/csv';
import { DistanceUnit, distanceIn } from '../shared/format';

/** A Run as GET /api/runs/export returns it. Distance and elevation in metres, times in seconds, pace in seconds per km. */
export interface RunExport {
  id: number;
  name: string;
  sportType: string;
  startLocal: string;
  distance: number;
  movingTime: number;
  pace: number | null;
  elevationGain: number;
  /** Beats per minute. */
  averageHeartRate: number | null;
  /** Steps per minute, both feet together. */
  cadence: number | null;
  relativeEffort: number | null;
  shoeName: string | null;
  stravaUrl: string;
}

/** The Runs as a spreadsheet: distance and Pace in the Distance Unit, elevation in metres. */
export function runTable(runs: RunExport[], unit: DistanceUnit): CsvTable {
  const perKm = distanceIn(1000, unit);
  return {
    header: [
      'Date', 'Name', 'Sport Type', `Distance (${unit})`, 'Moving Time', `Pace (min/${unit})`, 'Elevation Gain (m)', 'Avg Heart Rate (bpm)',
      'Cadence (spm)', 'Relative Effort', 'Shoe', 'Strava',
    ],
    rows: runs.map((run) => [
      csvDateTime(run.startLocal),
      run.name,
      run.sportType,
      csvNumber(distanceIn(run.distance, unit), 2),
      csvDuration(run.movingTime),
      csvMinutesSeconds(run.pace === null ? null : run.pace / perKm),
      csvNumber(run.elevationGain, 0),
      csvNumber(run.averageHeartRate, 0),
      csvNumber(run.cadence, 0),
      csvNumber(run.relativeEffort, 0),
      run.shoeName,
      run.stravaUrl,
    ]),
  };
}

import { CsvTable, csvDateTime, csvDuration, csvMinutesSeconds, csvNumber } from '../shared/csv';
import { Swim } from './swim';

/** The Swims as a spreadsheet; they stay in metres, whatever the Distance Unit. */
export function swimTable(swims: Swim[]): CsvTable {
  return {
    header: ['Date', 'Name', 'Distance (m)', 'Moving Time', 'Swim Pace (min/100m)', 'Strava'],
    rows: swims.map((swim) => [
      csvDateTime(swim.startLocal),
      swim.name,
      csvNumber(swim.distance, 0),
      csvDuration(swim.movingTime),
      csvMinutesSeconds(swim.pace),
      swim.stravaUrl,
    ]),
  };
}

import { CsvTable, csvDateTime, csvDuration, csvNumber } from '../shared/csv';
import { DistanceUnit, distanceIn } from '../shared/format';
import { Ride } from './ride';

/** The Rides as a spreadsheet: distance and Avg Speed in the Distance Unit, elevation in metres. */
export function rideTable(rides: Ride[], unit: DistanceUnit): CsvTable {
  const perKm = distanceIn(1000, unit);
  return {
    header: ['Date', 'Name', 'Sport Type', `Distance (${unit})`, 'Moving Time', `Avg Speed (${unit === 'km' ? 'km/h' : 'mph'})`, 'Elevation Gain (m)', 'Strava'],
    rows: rides.map((ride) => [
      csvDateTime(ride.startLocal),
      ride.name,
      ride.sportType,
      csvNumber(distanceIn(ride.distance, unit), 2),
      csvDuration(ride.movingTime),
      csvNumber(ride.speed === null ? null : ride.speed * perKm, 1),
      csvNumber(ride.elevationGain, 0),
      ride.stravaUrl,
    ]),
  };
}

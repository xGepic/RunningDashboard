import { csvDateTime, csvDuration, csvMinutesSeconds, csvNumber, toCsv } from './csv';
import { RunExport, runTable } from '../runs/run-export';
import { rideTable } from '../rides/ride-export';
import { swimTable } from '../swims/swim-export';

describe('CSV for Excel with Austrian settings', () => {
  it('separates cells with semicolons and ends each line with CRLF', () => {
    expect(toCsv({ header: ['Date', 'Name'], rows: [['2026-10-06 20:35', 'Evening Run'], ['2026-10-05 08:00', null]] })).toBe(
      'Date;Name\r\n2026-10-06 20:35;Evening Run\r\n2026-10-05 08:00;\r\n',
    );
  });

  it('quotes a cell holding a semicolon, a quote or a line break', () => {
    expect(toCsv({ header: ['Name'], rows: [['Lauf; Prater'], ['Der "lange" Lauf'], ['Zwei\nZeilen']] })).toBe(
      'Name\r\n"Lauf; Prater"\r\n"Der ""lange"" Lauf"\r\n"Zwei\nZeilen"\r\n',
    );
  });

  it('writes numbers with a decimal comma and no thousands separators', () => {
    expect(csvNumber(5.081, 2)).toBe('5,08');
    expect(csvNumber(42300, 0)).toBe('42300');
    expect(csvNumber(null, 2)).toBeNull();
  });

  it('writes durations as h:mm:ss and paces as m:ss', () => {
    expect(csvDuration(2096)).toBe('0:34:56');
    expect(csvDuration(14_404)).toBe('4:00:04');
    expect(csvMinutesSeconds(412.52)).toBe('6:53');
    expect(csvMinutesSeconds(null)).toBeNull();
  });

  it('writes the local start to the minute', () => {
    expect(csvDateTime('2026-10-06T20:35:26')).toBe('2026-10-06 20:35');
  });
});

describe('the exported tables', () => {
  const run: RunExport = {
    id: 1, name: 'Evening Run', sportType: 'Run', startLocal: '2026-10-06T20:35:26', distance: 5081, movingTime: 2096, pace: 412.52,
    elevationGain: 12.4, averageHeartRate: 151.4, cadence: 165, relativeEffort: 41, shoeName: 'Nike Pegasus 41',
    stravaUrl: 'https://www.strava.com/activities/1',
  };

  it('lists a Run with its distance and Pace in kilometres', () => {
    const table = runTable([run], 'km');

    expect(table.header).toEqual([
      'Date', 'Name', 'Sport Type', 'Distance (km)', 'Moving Time', 'Pace (min/km)', 'Elevation Gain (m)', 'Avg Heart Rate (bpm)', 'Cadence (spm)',
      'Relative Effort', 'Shoe', 'Strava',
    ]);
    expect(table.rows).toEqual([
      ['2026-10-06 20:35', 'Evening Run', 'Run', '5,08', '0:34:56', '6:53', '12', '151', '165', '41', 'Nike Pegasus 41', 'https://www.strava.com/activities/1'],
    ]);
  });

  it('lists a Run in miles when that is the Distance Unit, leaving empty what it was recorded without', () => {
    const table = runTable([{ ...run, averageHeartRate: null, cadence: null, relativeEffort: null, shoeName: null }], 'mi');

    expect(table.header[3]).toBe('Distance (mi)');
    expect(table.header[5]).toBe('Pace (min/mi)');
    expect(table.rows[0].slice(3, 11)).toEqual(['3,16', '0:34:56', '11:04', '12', null, null, null, null]);
  });

  it('lists a Ride with its Avg Speed in the Distance Unit', () => {
    const ride = {
      id: 7, name: 'Kahlenberg climb', sportType: 'Ride', startLocal: '2026-10-05T10:00:00', distance: 42300, movingTime: 5400, speed: 28.2,
      elevationGain: 620, stravaUrl: 'https://www.strava.com/activities/7',
    };

    expect(rideTable([ride], 'km').rows[0]).toEqual(
      ['2026-10-05 10:00', 'Kahlenberg climb', 'Ride', '42,30', '1:30:00', '28,2', '620', 'https://www.strava.com/activities/7'],
    );
    expect(rideTable([ride], 'mi').header[5]).toBe('Avg Speed (mph)');
    expect(rideTable([ride], 'mi').rows[0][5]).toBe('17,5');
  });

  it('lists a Swim in metres with its Swim Pace', () => {
    const swim = { id: 8, name: 'Lunch Swim', startLocal: '2026-10-02T12:00:00', distance: 1500, movingTime: 1930, pace: 128.67, stravaUrl: 'https://www.strava.com/activities/8' };

    expect(swimTable([swim])).toEqual({
      header: ['Date', 'Name', 'Distance (m)', 'Moving Time', 'Swim Pace (min/100m)', 'Strava'],
      rows: [['2026-10-02 12:00', 'Lunch Swim', '1500', '0:32:10', '2:09', 'https://www.strava.com/activities/8']],
    });
  });
});

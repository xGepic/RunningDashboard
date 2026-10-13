import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { Units } from './units';

describe('Units', () => {
  let units: Units;
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({ providers: [provideHttpClient(), provideHttpClientTesting()] });
    units = TestBed.inject(Units);
    http = TestBed.inject(HttpTestingController);
  });

  it('shows kilometres until the stored Distance Unit is in, then that unit', () => {
    expect(units.distance(5000)).toBe('5.00 km');

    http.expectOne('/api/settings').flush({ distanceUnit: 'mi' });

    expect(units.unit()).toBe('mi');
    expect(units.distance(5000)).toBe('3.11 mi');
    expect(units.distance(16093.44, 1)).toBe('10.0 mi');
    expect(units.pace(300)).toBe('8:03 /mi');
    expect(units.speed(32.18688)).toBe('20.0 mph');
    expect(units.totalDistance(1_609_344)).toBe('1,000 mi');
  });

  it('stays in kilometres when the settings cannot be loaded', () => {
    http.expectOne('/api/settings').flush('down', { status: 502, statusText: 'Bad Gateway' });

    expect(units.unit()).toBe('km');
    expect(units.pace(300)).toBe('5:00 /km');
  });

  it('switches at once when the Athlete chooses a unit, and saves it', () => {
    http.expectOne('/api/settings').flush({ distanceUnit: 'km' });

    units.choose('mi');

    expect(units.unit()).toBe('mi');
    const save = http.expectOne((r) => r.method === 'PUT' && r.url === '/api/settings');
    expect(save.request.body).toEqual({ distanceUnit: 'mi' });
    save.flush({ distanceUnit: 'mi' });
    expect(units.unit()).toBe('mi');
  });

  it('keeps a unit chosen before the stored one has come in', () => {
    units.choose('mi');
    http.expectOne((r) => r.method === 'GET' && r.url === '/api/settings').flush({ distanceUnit: 'km' });

    expect(units.unit()).toBe('mi');
  });

  it('does not go back over a later choice when an earlier save fails', () => {
    http.expectOne('/api/settings').flush({ distanceUnit: 'km' });

    units.choose('mi');
    units.choose('km');
    const [first] = http.match((r) => r.method === 'PUT');
    first.flush('down', { status: 502, statusText: 'Bad Gateway' });

    expect(units.unit()).toBe('km');
  });

  it('goes back to the unit before when saving fails', () => {
    http.expectOne('/api/settings').flush({ distanceUnit: 'km' });

    units.choose('mi');
    http.expectOne((r) => r.method === 'PUT').flush('down', { status: 502, statusText: 'Bad Gateway' });

    expect(units.unit()).toBe('km');
  });

  it('reads what the Athlete types in the unit as metres, and shows metres in it', () => {
    http.expectOne('/api/settings').flush({ distanceUnit: 'mi' });

    expect(units.toMetres(10)).toBeCloseTo(16093.44, 6);
    expect(units.fromMetres(1_609_344)).toBeCloseTo(1000, 9);
  });

  it('reads a typed distance in the unit, with a comma or a point, as metres to the decimetre', () => {
    http.expectOne('/api/settings').flush({ distanceUnit: 'mi' });

    expect(units.parse('10')).toBe(16093.4);
    expect(units.parse(' 3,1 ')).toBe(4989);
    expect(units.parse('lots')).toBeNull();
    expect(units.parse('0')).toBeNull();
    expect(units.parse('')).toBeNull();
  });

  it('keeps the metres a field was filled with when it is left as it was, so nothing drifts by rounding', () => {
    http.expectOne('/api/settings').flush({ distanceUnit: 'mi' });

    // 45 km shows as 28 mi; read back, 28 mi would be 45,061.6 m.
    expect(units.parse(units.inputValue(45_000), 45_000)).toBe(45_000);
    expect(units.parse('29', 45_000)).toBe(46671);
    expect(units.parse('28', null)).toBe(45061.6);
  });

  it('fills a form field with metres in the unit, to one decimal', () => {
    http.expectOne('/api/settings').flush({ distanceUnit: 'mi' });

    expect(units.inputValue(1_609_344)).toBe('1000');
    expect(units.inputValue(52_300)).toBe('32.5');
  });

  it('shows how far off the Target Pace is, per mile', () => {
    http.expectOne('/api/settings').flush({ distanceUnit: 'mi' });

    // 10 s/km is 16 s/mi.
    expect(units.paceDifference(10)).toBe('16 s/mi slower');
    expect(units.paceDifference(-45)).toBe('1:12 /mi faster');
    expect(units.paceDifference(0.2)).toBe('on Target Pace');
  });
});

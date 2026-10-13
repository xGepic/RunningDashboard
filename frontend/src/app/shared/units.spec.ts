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
});

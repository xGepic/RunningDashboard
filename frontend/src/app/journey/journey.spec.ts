import { Journey, JourneyStop, journeyProgress, tourFit, tourName } from './journey';

/** A short tour: Vienna, Bratislava at 50 km, Budapest at 200 km, and back to Vienna at 400 km. */
function testJourney(distance: number, reached: JourneyStop['reached'][] = []): Journey {
  const stops: Omit<JourneyStop, 'reached'>[] = [
    { name: 'Vienna', country: 'Austria', latitude: 48, longitude: 16, at: 0 },
    { name: 'Bratislava', country: 'Slovakia', latitude: 48, longitude: 17, at: 50_000 },
    { name: 'Budapest', country: 'Hungary', latitude: 47, longitude: 19, at: 200_000 },
    { name: 'Vienna', country: 'Austria', latitude: 48, longitude: 16, at: 400_000 },
  ];
  return { year: 2026, distance, length: 400_000, yearlyGoal: null, finished: false, stops: stops.map((s, i) => ({ ...s, reached: reached[i - 1] ?? null })) };
}

describe('journeyProgress', () => {
  it('sets off from the start before the first Stop', () => {
    const progress = journeyProgress(testJourney(20_000));

    expect(progress.last.name).toBe('Vienna');
    expect(progress.next!.name).toBe('Bratislava');
    expect(progress.toNext).toBe(30_000);
    expect(progress.reached).toBe(0);
    expect(progress.of).toBe(3);
    expect(progress.share).toBe(0.05);
    expect(progress.position).toEqual([48, 16.4]);
  });

  it('is as far along the leg after the last Stop reached as the distance goes', () => {
    const progress = journeyProgress(testJourney(125_000));

    expect(progress.last.name).toBe('Bratislava');
    expect(progress.next!.name).toBe('Budapest');
    expect(progress.toNext).toBe(75_000);
    expect(progress.reached).toBe(1);
    expect(progress.position).toEqual([47.5, 18]);
  });

  it('counts a Stop reached exactly', () => {
    expect(journeyProgress(testJourney(200_000)).last.name).toBe('Budapest');
  });

  it('is back at the start once the tour is complete, with the extra distance', () => {
    const journey = testJourney(430_000);
    const progress = journeyProgress(journey);

    expect(progress.last).toBe(journey.stops[3]);
    expect(progress.next).toBeNull();
    expect(progress.extra).toBe(30_000);
    expect(progress.reached).toBe(3);
    expect(progress.share).toBe(1);
    expect(progress.position).toEqual([48, 16]);
  });
});

describe('tourName', () => {
  it('names the start and counts the capitals between', () => {
    expect(tourName(testJourney(0))).toBe('Vienna and back via 2 cities');
  });
});

describe('tourFit', () => {
  const units = { totalDistance: (metres: number) => `${metres / 1000} km` };

  it('names the Yearly Goal the tour was picked for', () => {
    expect(tourFit({ ...testJourney(0), yearlyGoal: 410_000 }, units)).toBe('400 km, picked for your 410 km Yearly Goal');
  });

  it('gives the tour alone without one in the current year', () => {
    expect(tourFit(testJourney(0), units)).toBe('400 km');
  });

  it('says a finished year without one completed the tour with its distance', () => {
    expect(tourFit({ ...testJourney(420_000), finished: true }, units)).toBe("400 km, the longest the year's 420 km completed");
  });
});

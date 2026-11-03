/** One calendar year on the Virtual Journey, as GET /api/journey and GET /api/overview give it. Distances in metres. */
export interface Journey {
  year: number;
  /** The year's running distance; past the length once the tour is complete. */
  distance: number;
  /** The year's tour, as the crow flies. */
  length: number;
  /** The year's Yearly Goal, which the tour was picked for: the longest that fits within it. None without one. */
  yearlyGoal: number | null;
  /** The start, then each Stop in order; the last is the start again. */
  stops: JourneyStop[];
}

export interface JourneyStop {
  name: string;
  country: string;
  latitude: number;
  longitude: number;
  /** Metres from the start along the tour. */
  at: number;
  /** None for the start and for a Stop not reached yet. */
  reached: JourneyArrival | null;
}

/** The Run that brought the year's distance up to a Stop. */
export interface JourneyArrival {
  /** yyyy-MM-dd, the Run's local date. */
  date: string;
  runId: number;
  runName: string;
}

/** As GET /api/journey gives it. */
export interface JourneyPageData {
  journey: Journey;
  /** The years with Runs and the current year, newest first. */
  years: number[];
}

/** Where the Athlete is on the tour. */
export interface JourneyProgress {
  /** The last Stop reached; the start before the first. */
  last: JourneyStop;
  /** None once the tour is complete. */
  next: JourneyStop | null;
  /** Metres to the next Stop; 0 once complete. */
  toNext: number;
  /** Metres beyond the end once complete; 0 before. */
  extra: number;
  /** The Stops reached, the start not counted, and of how many. */
  reached: number;
  of: number;
  /** The share of the tour done, 0 to 1. */
  share: number;
  /** Latitude and longitude: as far along the leg as the distance goes, on the straight line the map draws. */
  position: [number, number];
}

export function journeyProgress(journey: Journey): JourneyProgress {
  const { stops, distance, length } = journey;
  let lastIndex = 0;
  while (lastIndex < stops.length - 1 && stops[lastIndex + 1].at <= distance) lastIndex++;
  const last = stops[lastIndex];
  const next = stops[lastIndex + 1] ?? null;
  const along = next ? (distance - last.at) / (next.at - last.at) : 0;
  return {
    last,
    next,
    toNext: next ? next.at - distance : 0,
    extra: next ? 0 : distance - length,
    reached: lastIndex,
    of: stops.length - 1,
    share: length > 0 ? Math.min(1, distance / length) : 0,
    position: next
      ? [last.latitude + (next.latitude - last.latitude) * along, last.longitude + (next.longitude - last.longitude) * along]
      : [last.latitude, last.longitude],
  };
}

/** How long the tour is: "1,590 km, picked for your 1,600 km Yearly Goal", or "2,062 km" without one. */
export function tourFit(journey: Journey, units: { totalDistance(metres: number): string }): string {
  const length = units.totalDistance(journey.length);
  return journey.yearlyGoal !== null ? `${length}, picked for your ${units.totalDistance(journey.yearlyGoal)} Yearly Goal` : length;
}

/** "Vienna and back via 10 cities". */
export function tourName(journey: Journey): string {
  const start = journey.stops[0]?.name ?? '';
  const cities = Math.max(0, journey.stops.length - 2);
  return `${start} and back via ${cities} ${cities === 1 ? 'city' : 'cities'}`;
}

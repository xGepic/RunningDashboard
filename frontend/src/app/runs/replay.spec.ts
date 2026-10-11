import { Replay } from './replay';

/** A straight route along the equator, 0.02° (about 2.2 km) long, run as 2 km: a fast first km, a slow second. */
const route: [number, number][] = [
  [0, 0],
  [0, 0.01],
  [0, 0.02],
];
const stretches = [
  { distance: 1000, movingTime: 300, pace: 300 },
  { distance: 1000, movingTime: 600, pace: 600 },
];

describe('Replay', () => {
  const replay = new Replay(route, stretches);

  it('lasts the Run’s Moving Time', () => {
    expect(replay.duration).toBe(900);
  });

  it('starts at the start of the route', () => {
    const frame = replay.at(0);
    expect(frame.position).toEqual([0, 0]);
    expect(frame.stretch).toBe(1);
    expect(frame.distance).toBe(0);
  });

  it('covers the fast first km in its 300 s, halfway along the route', () => {
    const frame = replay.at(300);
    expect(frame.distance).toBe(1000);
    expect(frame.position[1]).toBeCloseTo(0.01, 6);
    expect(frame.stretch).toBe(1);
  });

  it('moves at half the speed in the slow second km', () => {
    const frame = replay.at(600);
    expect(frame.distance).toBe(1500);
    expect(frame.position[1]).toBeCloseTo(0.015, 6);
    expect(frame.stretch).toBe(2);
    expect(frame.pace).toBe(600);
  });

  it('draws the trail through every route point passed, up to the current position', () => {
    expect(replay.at(600).trail).toEqual([[0, 0], [0, 0.01], replay.at(600).position]);
  });

  it('is held at the finish after the end', () => {
    const frame = replay.at(5000);
    expect(frame.time).toBe(900);
    expect(frame.position[1]).toBeCloseTo(0.02, 6);
    expect(frame.distance).toBe(2000);
  });
});

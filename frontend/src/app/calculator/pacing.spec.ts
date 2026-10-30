import { PacingPlan } from './pacing';

const marathon = 42195;
const fiveHours = 5 * 3600;

describe('PacingPlan', () => {
  it('runs every km at the same Pace for an even Pace', () => {
    const plan = new PacingPlan(10000, 3000, 'even', 0.02);

    const rows = plan.rows('km');

    expect(rows.map((row) => row.split)).toEqual([1, 2, 3, 4, 5, 6, 7, 8, 9, 10]);
    for (const row of rows) expect(row.time).toBeCloseTo(300, 6);
    expect(rows.map((row) => Math.round(row.clock))).toEqual([300, 600, 900, 1200, 1500, 1800, 2100, 2400, 2700, 3000]);
    expect(plan.startPace).toBeCloseTo(300, 6);
    expect(plan.finishPace).toBeCloseTo(300, 6);
  });

  it('ends a marathon with a short Split of 195 m', () => {
    const rows = new PacingPlan(marathon, fiveHours, 'even', 0).rows('km').filter((row) => row.split !== null);

    expect(rows.length).toBe(43);
    expect(rows[42].distance).toBeCloseTo(195, 6);
    expect(rows[42].clock).toBeCloseTo(fiveHours, 6);
    expect(rows[42].pace).toBeCloseTo(fiveHours / 42.195, 6);
  });

  it('gets steadily faster with a Negative Split, the second half taking the share less than the first', () => {
    const plan = new PacingPlan(marathon, fiveHours, 'negative', 0.02);

    const firstHalf = plan.clockAt(marathon / 2);
    const secondHalf = fiveHours - firstHalf;

    expect(plan.clockAt(marathon)).toBeCloseTo(fiveHours, 6);
    expect(secondHalf / firstHalf).toBeCloseTo(0.98, 9);
    expect(plan.startPace).toBeGreaterThan(fiveHours / 42.195);
    expect(plan.finishPace).toBeLessThan(fiveHours / 42.195);
    const paces = plan.rows('km').filter((row) => row.split !== null && row.distance === 1000).map((row) => row.pace!);
    for (let i = 1; i < paces.length; i++) expect(paces[i]).toBeLessThan(paces[i - 1]);
  });

  it('gets steadily slower with a Positive Split, the second half taking the share more than the first', () => {
    const plan = new PacingPlan(marathon, fiveHours, 'positive', 0.05);

    const firstHalf = plan.clockAt(marathon / 2);

    expect(plan.clockAt(marathon)).toBeCloseTo(fiveHours, 6);
    expect((fiveHours - firstHalf) / firstHalf).toBeCloseTo(1.05, 9);
    expect(plan.startPace).toBeLessThan(plan.finishPace);
  });

  it('marks every 5K, half-way and the finish as Checkpoints, on the km Splits they end', () => {
    const rows = new PacingPlan(marathon, fiveHours, 'even', 0).rows('km');

    const checkpoints = rows.filter((row) => row.checkpoint !== null);

    expect(checkpoints.map((row) => row.checkpoint)).toEqual(['5K', '10K', '15K', '20K', 'Half', '25K', '30K', '35K', '40K', 'Finish']);
    expect(checkpoints.find((row) => row.checkpoint === '10K')!.split).toBe(10);
    const half = checkpoints.find((row) => row.checkpoint === 'Half')!;
    expect(half.split).toBeNull();
    expect(half.end).toBe(21097.5);
    expect(half.clock).toBeCloseTo(fiveHours / 2, 6);
    expect(rows[rows.indexOf(half) + 1].split).toBe(22);
    expect(checkpoints.at(-1)!.split).toBe(43);
  });

  it('puts the km Checkpoints between the Splits in miles', () => {
    const rows = new PacingPlan(10000, 3000, 'even', 0).rows('mi');

    expect(rows.map((row) => row.checkpoint ?? row.split)).toEqual([1, 2, 3, '5K', 4, 5, 6, 'Finish']);
    expect(rows.at(-1)!.split).toBe(7);
    expect(rows[0].distance).toBeCloseTo(1609.344, 6);
  });

  it('has no half-way Checkpoint before 15 km', () => {
    const checkpoints = (distance: number) =>
      new PacingPlan(distance, distance * 0.3, 'even', 0)
        .rows('km')
        .filter((row) => row.checkpoint)
        .map((row) => row.checkpoint);

    expect(checkpoints(1609.344)).toEqual(['Finish']);
    expect(checkpoints(5000)).toEqual(['Finish']);
    expect(checkpoints(10000)).toEqual(['5K', 'Finish']);
    expect(checkpoints(15000)).toEqual(['5K', 'Half', '10K', 'Finish']);
    expect(checkpoints(16093.44)).toEqual(['5K', 'Half', '10K', '15K', 'Finish']);
  });
});


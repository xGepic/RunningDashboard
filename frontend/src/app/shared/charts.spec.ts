import { paceAxis } from './charts';

describe('paceAxis', () => {
  it('ticks every 10 seconds around close Paces', () => {
    // 6:39 to 7:08 /km
    expect(paceAxis([399, 428], 'km')).toEqual({ min: 390, max: 430, interval: 10 });
  });

  it('takes a coarser round step when the Paces spread wider', () => {
    // 5:19 to 7:08 /km
    expect(paceAxis([319, 428], 'km')).toEqual({ min: 300, max: 450, interval: 30 });
  });

  it('ticks on round Paces per mile', () => {
    const axis = paceAxis([399, 428], 'mi')!;
    const perMile = 1.609344;
    // 10:42 to 11:29 /mi
    expect(Math.round(axis.min * perMile)).toBe(640);
    expect(Math.round(axis.max * perMile)).toBe(690);
    expect(Math.round(axis.interval * perMile)).toBe(10);
  });

  it('gives a single Pace a step either side', () => {
    expect(paceAxis([300], 'km')).toEqual({ min: 295, max: 305, interval: 5 });
  });

  it('leaves the axis to ECharts without a Pace', () => {
    expect(paceAxis([], 'km')).toBeNull();
  });
});

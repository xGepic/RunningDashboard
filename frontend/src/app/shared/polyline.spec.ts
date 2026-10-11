import { decodePolyline } from './polyline';

describe('decodePolyline', () => {
  it('decodes the example from Google’s format description', () => {
    expect(decodePolyline('_p~iF~ps|U_ulLnnqC_mqNvxq`@')).toEqual([
      [38.5, -120.2],
      [40.7, -120.95],
      [43.252, -126.453],
    ]);
  });

  it('has no points for an empty route', () => {
    expect(decodePolyline('')).toEqual([]);
  });
});

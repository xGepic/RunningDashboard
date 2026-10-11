/** A point as [latitude, longitude] in degrees. */
export type LatLng = [number, number];

/**
 * The points of a route in Google's encoded polyline format, as Strava gives routes: each coordinate is the difference
 * from the previous point, in 1e-5 degrees, written as base-64 chunks of 5 bits.
 */
export function decodePolyline(encoded: string): LatLng[] {
  const points: LatLng[] = [];
  let index = 0;
  let lat = 0;
  let lng = 0;
  while (index < encoded.length) {
    for (const coordinate of ['lat', 'lng'] as const) {
      let result = 0;
      let shift = 0;
      let chunk: number;
      do {
        chunk = encoded.charCodeAt(index++) - 63;
        result |= (chunk & 0x1f) << shift;
        shift += 5;
      } while (chunk >= 0x20);
      const delta = result & 1 ? ~(result >> 1) : result >> 1;
      if (coordinate === 'lat') lat += delta;
      else lng += delta;
    }
    points.push([lat / 1e5, lng / 1e5]);
  }
  return points;
}

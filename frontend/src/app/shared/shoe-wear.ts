/** From this share of its Maximum Distance on, a Shoe is close to it. */
const nearMaxShare = 0.9;

/** How far a Shoe is towards the Maximum Distance: past it, close to it, or neither. */
export type ShoeWear = 'over' | 'near' | 'ok';

/** The Shoes page and the Shoes link's badge both judge a Shoe by this. Both distances in metres. */
export function shoeWear(distance: number, maximumDistance: number): ShoeWear {
  return distance > maximumDistance ? 'over' : distance / maximumDistance >= nearMaxShare ? 'near' : 'ok';
}

/** The app's link to the Athlete's Strava account, as returned by GET /api/connection. */
export interface Connection {
  connected: boolean;
  athlete: ConnectedAthlete | null;
  connectedAt: string | null;
}

export interface ConnectedAthlete {
  id: number;
  firstName: string;
  lastName: string;
}

/** Why the backend sent the browser back with ?strava=error&reason=… (mirrors the backend's ConnectError). */
export type ConnectErrorReason =
  | 'access-denied'
  | 'missing-permissions'
  | 'invalid-state'
  | 'different-athlete'
  | 'strava-rejected';

export const connectErrorMessages: Record<ConnectErrorReason, string> = {
  'access-denied': 'Strava access was declined.',
  'missing-permissions': 'Please allow all requested permissions on Strava, otherwise private Runs and Shoes are missing.',
  'invalid-state': 'The Strava login could not be verified. Please try again.',
  'different-athlete': 'This app is already connected to a different Strava athlete.',
  'strava-rejected': 'Strava did not accept the login. Please try again.',
};

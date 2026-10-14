/** The app's link to the Athlete's Strava account, as returned by GET /api/connection. */
export interface Connection {
  connected: boolean;
  athlete: ConnectedAthlete | null;
  connectedAt: string | null;
  /** The Strava API app the dashboard connects as; null until one is entered on the Settings page. */
  stravaApp: StravaApp | null;
}

/** As returned within GET /api/connection and by PUT /api/strava/app; the Client Secret never comes back. */
export interface StravaApp {
  clientId: string;
  /** Set in the backend's configuration (user secrets), which wins over the Settings page. */
  inConfiguration: boolean;
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
  | 'strava-rejected'
  | 'no-strava-app';

export const connectErrorMessages: Record<ConnectErrorReason, string> = {
  'access-denied': 'Strava access was declined.',
  'missing-permissions': 'Please allow all requested permissions on Strava, otherwise private Runs and Shoes are missing.',
  'invalid-state': 'The Strava login could not be verified. Please try again.',
  'different-athlete': 'This app is already connected to a different Strava athlete.',
  'strava-rejected': 'Strava did not accept the login. Please try again, and if it keeps failing, check the Client ID and Client Secret on Settings.',
  'no-strava-app': 'Enter your Strava API app below first.',
};

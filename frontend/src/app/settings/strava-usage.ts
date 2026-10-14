/** As returned by GET /api/strava/rate-limits: how much of Strava's rate limits is used. */
export interface StravaUsage {
  /** When Strava last reported it; null until Strava has answered since the backend started. */
  observedAt: string | null;
  /** Reads first, each per 15 minutes and then per day; empty until Strava has answered. */
  limits: RateLimit[];
}

export interface RateLimit {
  /** Read requests, which the syncs send, or every request. */
  requests: 'read' | 'all';
  /** 15 minutes starting at :00, :15, :30 and :45, or a UTC day. */
  period: 'fifteen-minutes' | 'day';
  used: number;
  limit: number;
  /** When the count starts again from zero. */
  resetsAt: string;
}

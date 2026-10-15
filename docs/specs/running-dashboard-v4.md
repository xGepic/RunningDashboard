# Running Dashboard v4

v4 adds to v3 ([running-dashboard-v3.md](running-dashboard-v3.md)). Terms follow [CONTEXT.md](../../CONTEXT.md), and the ADRs in [docs/adr](../adr) still apply.

| # | Feature | Status |
|---|---|---|
| 01 | Background sync | done |

---

## 01: Background sync

A Run uploaded to Strava shows up on the dashboard without "Sync now", as long as the dashboard is running.

- The backend syncs every hour while it runs, besides on start, after connecting and on "Sync now" (ADR 0002). The hour is `Sync:Interval` in `appsettings.json` (or a user secret), e.g. `00:30:00`; `00:00:00` turns it off.
- It skips a sync while Strava waits to be reconnected, which would only fail again, and while Strava is not connected. One that comes while a sync runs, e.g. a first import paused for the rate limits, is dropped, as "Sync now" would be.
- When the dashboard's tab comes back into view, it asks for the sync status and starts a sync if the last one finished more than 15 minutes ago, unless a sync runs, Strava waits to be reconnected or no sync has ever finished.
- A sync after the first import takes a few requests, so 24 a day stay far below Strava's daily limit of 1,000.

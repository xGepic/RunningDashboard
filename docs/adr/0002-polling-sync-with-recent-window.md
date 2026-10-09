# Sync by polling, and recheck only the 20 most recent Activities for edits

The app runs locally, so Strava webhooks can't reach it. Instead it syncs when the backend starts and when the Athlete presses "Sync now". The first import is a background job that can resume after stopping, and it stays within Strava's read limits of about 100 requests per 15 minutes and 1,000 per day. To catch Runs that were edited or deleted on Strava, each sync compares only the 20 most recent Activities, which is one request, against the stored data. The Athlete only ever edits recent Runs. An edit or deletion older than that window is accepted as a known blind spot.

## Consequences

If the app is ever deployed, webhooks become possible and should replace the recent-window check.

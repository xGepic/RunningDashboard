# The Strava API app is entered on Settings and stored in the database

The v1 spec read the Strava API app's Client ID and Client Secret from .NET user secrets. The packaged app (`publish.ps1`) runs on friends' PCs and Macs without the .NET SDK, so it has no user secrets, and Strava lets only an API app's owner connect to it, so every Athlete needs an API app of their own. So the Client ID and Client Secret are entered on the Settings page and stored in the database, in plain text like the Strava tokens next to them. The app only listens on localhost, the database file is as well protected as the user secrets file was, and a key to encrypt them with would have to be stored next to them. Credentials in the backend's configuration (e.g. user secrets) still win over the ones entered on Settings.

## Consequences

Every backup holds the Client Secret, as it already held the refresh token, so a backup folder synced to the cloud carries both. Encrypting them with Windows' DPAPI would fix that, but a backup restored on another PC or user account could then no longer read them, and DPAPI doesn't exist on the Mac.

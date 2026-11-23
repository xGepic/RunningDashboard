# Running Dashboard

A local dashboard for one Athlete's Strava running history and marathon training, with plain lists of their Rides and Swims on the side. See [CONTEXT.md](CONTEXT.md) for the domain language, [docs/specs](docs/specs) for the spec and tickets, [docs/adr](docs/adr) for decisions, and, opened in a browser, [docs/architecture/building-block-view.html](docs/architecture/building-block-view.html), [docs/architecture/runtime-view.html](docs/architecture/runtime-view.html) and [docs/architecture/deployment-view.html](docs/architecture/deployment-view.html) for the architecture and [docs/architecture/database-schema.html](docs/architecture/database-schema.html) for the database tables (each also says how to render it as an image).

## One-time setup

1. Create a Strava API app at <https://www.strava.com/settings/api> and set **Authorization Callback Domain** to `localhost`. Its Client ID and Client Secret are entered in the dashboard later.
2. Install frontend packages: `npm install --prefix frontend`

## Run

Backend (http://localhost:5148, creates and migrates `running-dashboard.db` on start):

```sh
dotnet run --project backend/src/RunningDashboard.Api
```

Frontend (http://localhost:4200, proxies `/api` to the backend):

```sh
npm start --prefix frontend
```

Open http://localhost:4200 and click **Connect Strava**. The first time, it leads to Settings: enter the Strava API app's Client ID and Client Secret there, then connect. They are stored in the database, never in the repository (see [ADR 0004](docs/adr/0004-strava-api-app-entered-on-settings.md)). Credentials in the backend's configuration, such as the user secrets `Strava:ClientId` and `Strava:ClientSecret`, win over the ones entered on Settings.

While the backend runs, it syncs every hour, and the dashboard syncs when its tab comes back into view more than 15 minutes after the last sync; **Sync now** is there for anything sooner. To sync more or less often, set `Sync:Interval` in `appsettings.json` or as a user secret, e.g. `00:30:00` (`00:00:00` turns it off).

Everything runs locally, except that the maps (a Run's and the Heatmap) load their tiles from OpenStreetMap, Esri and OpenTopoMap, so it needs an internet connection. None of them needs an account or API key.

## Backups

The backend backs up `running-dashboard.db` when it starts and every 24 hours while it runs (a restart within 24 hours of the last backup makes none), and always right before it applies new migrations. It keeps the 7 most recent backups; those made before migrations are never removed. Settings shows the folder and the last backup, and has **Back up now**.

Backups go to `backups/` next to the database. To keep them off the PC, point `Backup:Folder` at a synced folder, in `appsettings.json` or as a user secret:

```sh
dotnet user-secrets set "Backup:Folder" "C:\Users\<you>\OneDrive\RunningDashboard" --project backend/src/RunningDashboard.Api
```

A backup holds everything in the database, the Strava refresh token and any Strava API app entered on Settings too, so keep the folder private. To restore one, stop the backend, copy the backup over `running-dashboard.db` (deleting any `running-dashboard.db-wal` and `running-dashboard.db-shm` next to it), and start the backend again.

## Test

```sh
dotnet test backend
npm test --prefix frontend -- --watch=false
```

With code coverage, each as an HTML report (open its `index.html`):

```sh
pwsh backend/coverage.ps1                                 # backend/coverage
npm test --prefix frontend -- --watch=false --coverage   # frontend/coverage
```

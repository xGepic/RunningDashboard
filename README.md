# Running Dashboard

A local dashboard for one Athlete's Strava running history and marathon training, with plain lists of their Rides and Swims on the side. See [CONTEXT.md](CONTEXT.md) for the domain language, [docs/specs](docs/specs) for the spec and tickets, [docs/adr](docs/adr) for decisions, and, opened in a browser, [docs/building-block-view.html](docs/building-block-view.html), [docs/runtime-view.html](docs/runtime-view.html) and [docs/deployment-view.html](docs/deployment-view.html) for the architecture and [docs/database-schema.html](docs/database-schema.html) for the database tables (each also says how to render it as an image).

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

Everything runs locally, except that the Run map loads its tiles from OpenStreetMap, Esri and OpenTopoMap, so it needs an internet connection. None of them needs an account or API key.

## Give it to a friend

A friend on Windows or on a Mac with Apple Silicon (M1 and later) can run the whole dashboard without installing .NET or Node. Build it with:

```powershell
./publish.ps1
```

It fills `deploy/` (not committed; the apps are over 100 MB):

- `deploy/win` and `deploy/RunningDashboard-win.zip`: `RunningDashboard.exe` for Windows
- `deploy/mac` and `deploy/RunningDashboard-mac.tar.gz`: `RunningDashboard` for the Mac

GitHub can build them too: pushing a version tag runs all tests, builds both apps and attaches them to a GitHub Release (in a private repository, only people with access to it can download them there):

```sh
git tag v1.0.0
git push origin v1.0.0
```

(**Actions → Release → Run workflow** builds them without a release, as the run's artifact.)

Send them the zip or the tarball (about 50 MB). It holds no data and no credentials of yours. They:

1. Unpack it anywhere and start the app in the `RunningDashboard` folder. The dashboard opens in the browser at http://localhost:5148; closing the console (Terminal) window stops it.
   - **Windows:** double-click `RunningDashboard.exe`. SmartScreen may warn about an unknown app: **More info → Run anyway**.
   - **Mac:** the app is not from an identified developer, so macOS blocks it the first time. Either double-click `RunningDashboard`, then allow it under **System Settings → Privacy & Security → Open Anyway**, or run once in Terminal: `xattr -dr com.apple.quarantine ~/Downloads/RunningDashboard` (with the folder's path). After that, a double-click starts it.
2. Click **Connect Strava**. It leads to Settings, which asks for a Strava API app of their own: they create one at <https://www.strava.com/settings/api> with **Authorization Callback Domain** `localhost`, enter its Client ID and Client Secret, and then connect. (Strava lets an app's owner connect only, so yours would not let them in.)

The database and its backups go next to the app, so to update, they replace the app (`RunningDashboard.exe` or `RunningDashboard`) and `wwwroot` with the new ones and keep the rest.

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

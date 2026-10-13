# Running Dashboard

A local dashboard for one Athlete's Strava running history and marathon training, with plain lists of their Rides and Swims on the side. See [CONTEXT.md](CONTEXT.md) for the domain language, [docs/specs](docs/specs) for the spec and tickets, [docs/adr](docs/adr) for decisions, and [docs/database-schema.html](docs/database-schema.html), opened in a browser, for the database tables (it also says how to render them as an image).

## One-time setup

1. Create a Strava API app at <https://www.strava.com/settings/api> and set **Authorization Callback Domain** to `localhost`.
2. Store its credentials as user secrets (never commit them):

   ```sh
   dotnet user-secrets set "Strava:ClientId" "<client id>" --project backend/src/RunningDashboard.Api
   dotnet user-secrets set "Strava:ClientSecret" "<client secret>" --project backend/src/RunningDashboard.Api
   ```

3. Install frontend packages: `npm install --prefix frontend`

## Run

Backend (http://localhost:5148, creates and migrates `running-dashboard.db` on start):

```sh
dotnet run --project backend/src/RunningDashboard.Api
```

Frontend (http://localhost:4200, proxies `/api` to the backend):

```sh
npm start --prefix frontend
```

Open http://localhost:4200 and click **Connect Strava**.

Everything runs locally, except that the Run map loads its tiles from OpenStreetMap, Esri and OpenTopoMap, so it needs an internet connection. None of them needs an account or API key.

## Test

```sh
dotnet test backend
npm test --prefix frontend -- --watch=false
```

# Running Dashboard v12

v12 adds one feature to v11 ([running-dashboard-v11.md](running-dashboard-v11.md)). Terms follow [CONTEXT.md](../../CONTEXT.md), and the ADRs in [docs/adr](../adr) still apply.

| # | Feature | Status |
|---|---|---|
| 01 | Heatmap | done |

---

## 01: Heatmap

Every Run's route on one map, so the roads the Athlete runs most often stand out. Strava has one only with a subscription; this one needs nothing but the routes the dashboard already stores.

### What is on it

- Every Run with a route outdoors: treadmill Runs (also those that recorded a route) and virtual Runs (their routes lie in an invented world) are left out, and so are Runs without GPS. Rides and Swims are never on it.
- Each Run's simplified route from Strava's summary, not the full one from its detail: it is small enough to draw hundreds of Runs at once, and needs no more Strava requests.
- `GET /api/heatmap` gives `runs`, oldest first, each with its `id`, `name`, `startLocal`, `distance` and `route` (Google's encoded polyline). It always gives every Run; the page picks a year itself, so switching years needs no request.

### Heatmap page

- At /heatmap, below the Activities in the breadcrumb, opened with **Heatmap** beside the Runs | Rides | Swims switch on the Run history. /heatmap?year=2025 shows one year's Runs, by local start, picked with a year switch (All and each year with Runs; none with only one year). Going back up to the Runs leaves the year behind.
- A dark map with greyed-out OpenStreetMap tiles. Each route is a thin, see-through line in the Theme's colour, the newest on top, so a road run many times is brighter than one run once.
- Pointing at a route lights it up and names the Run, its date and distance; clicking it opens the Run's page.
- The map first shows the home area: the Runs that start within 10 km of the start with the most Runs starting within 5 km of it, so a holiday Run far away, or a second place the Athlete runs from now and then, does not zoom it out. Switching years keeps the zoom and position. **Home** goes back to the home area of the Runs shown, **All Runs** shows every one of them, wherever it was.
- Above the map: the year (or "All years"), and how many Runs and how far.
- Without Runs with a route: "No Runs with a route yet. Treadmill Runs and Runs without GPS have none." A year without any: "No Runs with a route in 2020."

---

## Out of scope

- Rides and Swims on the Heatmap, or a Heatmap of their own.
- The full routes of the Runs' details, for exact streets when zoomed in.
- A colour ramp (e.g. blue to yellow to white) by how often a road was run, or counting how often.
- Picking a month, a Training Block, a Shoe or a range of dates; satellite or topographic tiles.
- Naming the places the Athlete ran (that needs a geocoding service), or a list of them to jump to.
- A link to the Heatmap from the sidebar, the Overview, a Run's page, or the Year in Review.

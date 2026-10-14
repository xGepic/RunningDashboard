# Running Dashboard v2

v2 adds five features to v1 ([running-dashboard-v1.md](running-dashboard-v1.md)). They ship one at a time, smallest first, in the order below. Terms follow [CONTEXT.md](../../CONTEXT.md), and the ADRs in [docs/adr](../adr) still apply.

Everything comes from data that is already stored: Strava's raw JSON, the Splits and the Shoes. There are no new Strava requests, and the app stays local.

| # | Feature | Status |
|---|---|---|
| 01 | Shoe badge | done |
| 02 | Database backup | done |
| 03 | CSV export | done |
| 04 | Year in Review | ready |
| 05 | Fitness page | ready |

---

## 01: Shoe badge

The Athlete sees, without opening the Shoes page, when a Shoe is due to be retired.

- The rule is the one the Shoes page already uses, shared by both: a Shoe is **near** its Maximum Distance from 90% of it on, and **over** it past it.
- The Shoes link in the sidebar and in the top tabs carries a badge with the number of non-retired Shoes that are near or over. The badge is amber when they are all near, red when one is over, and missing when none are.
- The badge follows a change to the Maximum Distance in Settings at once, and reloads after a sync.

## 02: Database backup

The Athlete never loses their data or their Training Plans to a broken disk or a failed migration.

- The backend backs up the SQLite database when it starts, then every 24 hours while it runs, using SQLite's backup API (safe while the app is running). A restart within 24 hours of the last backup makes none, so frequent restarts don't push older backups out.
- It always backs up right before applying pending migrations.
- Backups go to the folder in `Backup:Folder` in `appsettings`, `backups/` next to the database unless set (and in `.gitignore`). Pointing it at a synced folder (e.g. OneDrive) keeps a copy off the PC.
- The 7 most recent backups are kept; backups made before migrations are kept on top of those.
- Settings shows the folder (read-only), the time of the last backup, and a "Back up now" button.
- A backup contains the Strava refresh token. Restoring is manual and documented in the README: stop the backend and copy the backup over the database.

## 03: CSV export

The Athlete can take their history into Excel.

- The Runs page shows one month at a time, so its "Export CSV" is a menu: **This month** exports what the page lists (the month, its search and sort order) as `runs-2026-09.csv`, and **All Runs** exports every Run in the list's sort order.
- The Rides and Swims lists, which have no filters, each get an "Export CSV" button that exports every Ride or Swim in the list's sort order, not only the page shown.
- Format for Excel with Austrian/German settings: `;` as the separator, `,` as the decimal separator, UTF-8 with a BOM. Exports of everything are named after the day, like `runs-2026-10-14.csv`. Moving Time is `h:mm:ss`.
- Distances and Avg Speed are plain numbers in the Distance Unit; Pace is `m:ss`. Swims stay in metres.
- Columns:
  - **Runs:** Date, Name, Sport Type, Distance, Moving Time, Pace, Elevation Gain, Avg Heart Rate, Cadence (steps per minute), Relative Effort, Shoe, Strava link
  - **Rides:** Date, Name, Sport Type, Distance, Moving Time, Avg Speed, Elevation Gain, Strava link
  - **Swims:** Date, Name, Distance (m), Moving Time, Swim Pace, Strava link
- Each Run stores its average heart rate and Cadence in their own columns (filled from the stored summaries by a migration), rather than reading them from the raw JSON on every export. The Fitness page (05) uses them too.

## 04: Year in Review

The Athlete looks back on a whole year as a story of highlights, not only as totals.

- A "Review" tab on the Calendar page, using its year switcher. It opens on the current year, labelled "so far". 2024 is labelled "from 28 Apr", when the history starts.
- What it shows for the year:
  - Totals (Runs, distance, Moving Time, elevation gain) compared with the previous year, with the change in %
  - The longest Run, the biggest Training Week and the biggest month
  - The Personal Bests set that year: for each standard distance, the year's fastest Best Effort if it was a Personal Best when it was run (a step on the Records chart)
  - The Longest Streak within the year
  - The Goal Races of the year with their Race Results
  - The most-used Shoe: the one with the most distance in that year's Runs (Strava's Shoe distance includes distance entered by hand and can't be split by year)
  - A footnote with the number of Rides and Swims. They are listed, not counted toward anything, as Listed Activities always are.
- **Training Weeks across New Year** count only with their days inside the year. The first and last weeks of a year can then be shorter than 7 days, and no Run counts in two years. The Longest Streak within the year runs from 1 Jan at the earliest to 31 Dec at the latest.

## 05: Fitness page

The Athlete sees whether they are getting fitter, from the heart rate and cadence that every Run already has.

- A new **Fitness** page in the sidebar with three charts over the whole history, with no year picker: Aerobic Efficiency, Cardiac Drift and Cadence.
- Each chart shows one dot per Fitness Run (for Cardiac Drift, per Long Run that has one) and a line for the monthly average. The Cardiac Drift chart shades the good band up to 5%.
- Run details show Aerobic Efficiency and Cadence for every Fitness Run, and Cardiac Drift for a Long Run that has one.
- Calculations:
  - **Aerobic Efficiency:** grade-adjusted distance per minute ÷ average heart rate, with grade-adjusted speed from the km Splits.
  - **Cardiac Drift:** only for Long Runs of at least 10 km. The km Splits are halved by distance: the shorter last Split counts with its distance, and a Split that straddles the middle is shared between both halves in proportion. Drift is (heart rate ÷ grade-adjusted speed in the second half) ÷ (the same in the first half) − 1. A Split without heart rate or grade-adjusted speed leaves the whole Run without Cardiac Drift.
  - **Cadence:** Strava's per-foot value × 2.
- **Storage:** a migration adds heart rate and grade-adjusted speed as columns on the Split, filled from the stored raw JSON. Import fills them from now on, the same way the mile splits got their own columns instead of being read from JSON on every request. The migration is preceded by a backup (02).

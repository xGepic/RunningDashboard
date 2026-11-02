# Running Dashboard v10

v10 adds one feature to v9 ([running-dashboard-v9.md](running-dashboard-v9.md)). Terms follow [CONTEXT.md](../../CONTEXT.md), and the ADRs in [docs/adr](../adr) still apply.

| # | Feature | Status |
|---|---|---|
| 01 | Ride and Swim history by year | done |

---

## 01: Ride and Swim history by year

The Ride history and the Swim history list one year's Rides or Swims at a time, stepped through as the Run history steps through months.

- `GET /api/rides` and `GET /api/swims` give `years`: every year with a Ride (of any kind) or a Swim, by local start, newest first, whatever the list is narrowed to. A year is asked for with the `from` and `to` every history already takes.
- The current year unless the URL names another (`?year=2025`); the current year stays out of the URL, and a malformed one lists the current year.
- In the toolbar, before the Ride kinds and alone on the Swims page: ← 2026 → and "Today", as the Run history's months. → and "Today" are off in the current year, ← in the first year with Rides or Swims. A step keeps the Ride kind and the sort order and lists from the first page.
- The totals in the page header and the pages cover only the year, and the Ride kind with it. A page holds 200 Rides or Swims, as the Run history's does, so a year nearly always fits on one.
- "Export CSV" is a menu, as on the Run history: "This year" saves the year's Rides of the kind as `rides-2025.csv` (`swims-2025.csv`); "All Rides" (or "All virtual Rides", "All Swims") saves those of every year, named after today.
- A year without Rides of the kind, or without Swims, says "No virtual Rides in 2025." or "No Swims in 2025.", with "Go to 2026" to the newest year with Rides or Swims.

---

## Out of scope

- Listing all years at once, or a range of years.
- Narrowing to a month or a Training Week.

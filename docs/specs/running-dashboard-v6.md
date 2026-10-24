# Running Dashboard v6

v6 adds one feature to v5 ([running-dashboard-v5.md](running-dashboard-v5.md)) and takes one chart away. Terms follow [CONTEXT.md](../../CONTEXT.md), and the ADRs in [docs/adr](../adr) still apply.

| # | Feature | Status |
|---|---|---|
| 01 | Training Time | done |
| 02 | No monthly Pace on the Overview | done |

---

## 01: Training Time

The Athlete sees how much they trained each week, Rides and Swims included, without the running figures changing.

### What counts

- Training Time is Moving Time, the one measure every sport has: distances of Runs, Rides and Swims don't add up.
- Each Training Week has the Moving Time of its Runs, its Rides (outdoor and Virtual) and its Swims, each on its own. An Activity belongs to the week of its local start date, as a Run does.
- It is worked out from the stored Runs and Listed Activities on every request, and nothing about it is stored.
- It is shown beside the running figures and never added to them: totals, Training Weeks, Streaks, Records, goals and Training Blocks still count Runs only.

### Overview card

- A "Training Time per Training Week" card on the Overview, under "Distance per Training Week".
- Stacked columns per Training Week, in hours: Runs at the bottom in the Theme's colour, then Rides, then Swims, with a legend above the chart. Only the top segment of a column is rounded, and a hairline in the surface colour parts the segments.
- Rides and Swims have colours of their own per Theme (`--chart-ride` and `--chart-swim` in styles.scss), away from the Theme's accent and checked together with it against the Theme's surface for colour-blind readers.
- The tooltip shows the week's Training Time and each sport's, as "1h 06m", or "–" for none.
- A year switch, chosen apart from the distance chart's, defaults to the newest year. A week belongs to the year its Monday falls in.
- Next to it, the year's average week ("Avg / week") and the share of the year's Training Time spent on Rides and Swims.
- "Show as table" lists each week's Runs, Rides, Swims and total.
- The weeks run from the first Run's or Listed Activity's week, whichever is earlier, up to the current one, with empty weeks as zero.
- Without Rides and Swims, the card is left out, since it would only repeat the Runs' Moving Time.

### Out of scope

- Relative Effort or another load measure across sports. Strava has it only for Activities recorded with heart rate, and not stored for Listed Activities.
- Training Time anywhere but the Overview, e.g. in a Training Block's weeks or the Year in Review.
- Choosing which sports count.

## 02: No monthly Pace on the Overview

The Overview drops its "Average Pace per month" chart (v1, story 25), which the Athlete found of little significance.

- The card goes, with its year switch and table, and `GET /api/overview` no longer returns months.
- Pace stays everywhere else: in the Run history, on a Run's page with its Splits, and in Training Blocks against the Target Pace.

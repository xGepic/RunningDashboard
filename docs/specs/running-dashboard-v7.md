# Running Dashboard v7

v7 adds one feature to v6 ([running-dashboard-v6.md](running-dashboard-v6.md)). Terms follow [CONTEXT.md](../../CONTEXT.md), and the ADRs in [docs/adr](../adr) still apply.

| # | Feature | Status |
|---|---|---|
| 01 | Pacing Plan | done |

---

## 01: Pacing Plan

The Athlete sees how to split a race to reach a finish time.

### In the Calculator

- The Calculator gets a "Pacing Plan" card under its result, always shown. Its Splits follow the Calculator's distance and finish time, whether the pace or the time was typed or slid. Until there is a finish time, it lists the distance's Checkpoints with "–" for their times and says to type a pace or a finish time.
- A Pacing Strategy is picked in a row of its own under the card's title: Even unless changed, Negative Split or Positive Split. For a Negative or Positive Split, how much faster or slower the second half is than the first is picked from 1% to 10%, 5% unless changed, next to the strategies. That field keeps its place while Even is picked, hidden, so picking a strategy moves nothing.
- With a Negative or Positive Split, the Pace changes evenly with the distance from start to finish, so it has no jump at half-way. The card says the Pace at the start and at the finish, e.g. "From 7:28 /km at the start to 6:45 /km at the finish: the second half 5% faster than the first." for a 5:00:00 marathon With Even, it says the one Pace.
- The plan is a table with a row per Checkpoint: every 5 km, half-way and the finish ("5K", …, "Half", …, "Finish"). A marathon has 10 rows. Only races of 15 km and longer have a half-way Checkpoint, so a 5K has the finish alone and a 10K the 5K and the finish. The Checkpoints are in km in miles too, as timing mats are.
- Each row has the clock time at the Checkpoint, and the Pace and time of the stretch from the Checkpoint before (or the start) to it.
- A Checkpoint's row opens to the Splits of its stretch, per km or per mile when the Distance Unit is miles, each with its clock time, Pace and time. A Split belongs to the stretch it ends in, so the km half-way falls within (km 22 of a marathon) goes with the 25K. "Show every km" (or mi) under the table opens every row, and "Show Checkpoints only" closes them all.
- Nothing about the Pacing Plan is stored: the Pacing Strategy and the share go back to Even and 5% when the page is opened again.

### Out of scope

- Goal Races in the Calculator: choosing one would only fill in a distance and a time the Athlete can pick and type. So there is no "Pacing Plan" link from the Training page either.
- Comparing a race, or any Run, with the Pacing Plan afterwards.
- Pacing by the course's elevation, or by heart rate.
- Setting the Pace by hand per Split or per stretch.
- A printable pace band.

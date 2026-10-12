# Running Dashboard v2: Tickets

These tickets break down [running-dashboard-v2.md](running-dashboard-v2.md). Each one is a vertical slice that works end to end (storage, API, UI, tests). They are numbered in dependency order, so blockers always come first. Terms follow [CONTEXT.md](../../CONTEXT.md) (Run Type, Running Days, Distance Jump, Streak). Decisions follow the ADRs in [docs/adr](../adr): Strava is the only data source, no new Strava requests are needed, and Strava's `workout_type` stays ignored.

**Deadline:** the second marathon's Training Block starts Mon 1 Feb 2027, and its plan is entered in January. Tickets 01, 02, 03 and 05 must be finished by then. 04, 06 and 07 can follow. 01 and 05 both change the plan editor and the Training Plan response but don't block each other. If they're built at the same time, expect a merge conflict there.

**Test seam (all tickets):** the backend HTTP API, run in an in-memory test host with real SQLite, the fake Strava handler and a fake `TimeProvider`, exactly as in v1. Tests check only the API responses. The Distance Jump rule exists only in the backend, and the frontend never computes it. Frontend tests stay light: component tests with Angular's default test runner, and no end-to-end tests.

| # | Ticket | Blocked by |
|---|---|---|
| 01 | Run Types in the Training Plan | none |
| 02 | Run Types on the calendar strip | 01 |
| 03 | Distance Jumps on the Overview | none |
| 04 | Distance Jumps on the Training page (run weeks) | 03 |
| 05 | Planned Distance Jumps | 03 |
| 06 | Consistency card: heatmap and year picker | none |
| 07 | Streaks | 06 |

---

# 01: Run Types in the Training Plan

**What to build:** In the Training Plan editor, the Athlete picks a Run Type for each day of a plan week: Easy, Long, Tempo, Intervals or Race, or "rest". This replaces switching the Running Days on and off. A Running Day is now simply a day with a Run Type. Plans that were already entered keep working, and every existing Running Day becomes Easy. "Copy from previous week" copies the Run Types.

**Blocked by:** None (can start immediately)

**Status:** ready-for-agent

- [ ] A plan week stores its Running Days as a weekday plus a Run Type, at most one per weekday. A day without one is a rest day. The Run Types are a fixed list: Easy, Long, Tempo, Intervals, Race.
- [ ] An EF Core migration turns every stored Running Day into a Running Day with Run Type Easy and leaves the rest of the plan unchanged. It has been checked once by hand against a copy of the real database.
- [ ] The plan weeks endpoint takes `runningDays` as a list of `{ day, runType }` in any order, with Run Types in lower case (`easy`, `long`, `tempo`, `intervals`, `race`). The Training Plan response returns them Monday first.
- [ ] An unknown day, an unknown Run Type, or the same day twice is rejected with a validation error keyed `weeks[i].runningDays`.
- [ ] Moving the Goal Race's date moves the Run Types with the plan, because they stay tied to Weeks to Race.
- [ ] More than one day of a week can have the same Run Type (e.g. two Long days). Run Types are not checked against the Weekly Target.
- [ ] The Training page's calendar strip keeps working: a Running Day counts as run if there's any Run on that date.
- [ ] The editor shows a picker per day ("rest" plus the five Run Types) in place of the Running Day toggles. "Copy from previous week" copies the Run Types.
- [ ] API tests cover:
  - saving and reading back a week with mixed Run Types
  - "rest" as a missing Running Day
  - the validation errors
  - moving the Goal Race
  - the existing plan and calendar tests, rewritten to the new contract
- [ ] The editor's component tests are extended: picking a Run Type and "rest", and "copy from previous week" copying Run Types.

---

# 02: Run Types on the calendar strip

**What to build:** The calendar strip of each block week on the Training page shows each Running Day's Run Type, so the Athlete can tell a missed Long day from a missed Easy day, and sees what each day they ran was planned as.

**Blocked by:** 01

**Status:** ready-for-agent

- [ ] Each day of a block week's plan returns its `runType`: the Running Day's Run Type for planned, ran and missed days, and none for rest days and for Runs on unplanned days.
- [ ] The day states (ran, missed, unplanned, planned, rest) don't change. A double on a Running Day counts as ran for that one day.
- [ ] The Long Run is still the longest Run of the Training Week, and the planned Long Run is checked against it. For example, Sunday is planned as Long but run as 18 km, and Thursday was 20 km: the Long Run is Thursday's.
- [ ] A past Training Block's plan shows its Run Types in the same way, including in the comparison.
- [ ] The strip shows each Running Day's Run Type as a short label, with the full name on hover.
- [ ] API tests cover:
  - the Run Type on ran and missed days
  - none on unplanned days
  - a double on a Running Day
  - the Long Run case above

---

# 03: Distance Jumps on the Overview

**What to build:** On the Overview's "Distance per Training Week" chart, every week that went up more than 10% compared with the week before is marked as a Distance Jump. If the week before was a Recovery Week, the comparison skips back to the last week that wasn't. The tooltip says by how much the week went up and which week it was compared with. This ticket builds the Distance Jump rule that 04 and 05 reuse.

**Blocked by:** None (can start immediately)

**Status:** ready-for-agent

- [ ] The Distance Jump rule is built once in the backend's training calendar module. Given consecutive Training Weeks with their distance, whether each had Runs (or a planned distance) and whether each is a Recovery Week, it gives each week either no Distance Jump, or the week it was compared with and the increase.
- [ ] A week is a Distance Jump if its distance is more than 1.10 times that of its comparison week. The comparison week is the previous Training Week, skipping back past Recovery Weeks to the last one that isn't. Metres are compared without rounding.
- [ ] There's no Distance Jump if no comparison week exists, if the comparison week had no Runs, or if the week itself has no distance.
- [ ] A shared lookup tells, for any range of Training Weeks, which ones are Recovery Weeks. A week is one when it falls in a Goal Race's Training Block whose Training Plan marks that Weeks to Race as a Recovery Week. Weeks outside a block, or inside a block without a plan, never are.
- [ ] The current week is judged by its distance so far.
- [ ] Every week in the Overview's weekly list returns `distanceJump`: `{ comparedWith, increase }`, or none. `comparedWith` is the comparison week's Monday, and `increase` is the ratio minus one (0.2 for +20%).
- [ ] The chart marks Distance Jump bars, and their tooltip shows the increase and the comparison week. A Distance Jump is a warning only.
- [ ] API tests cover:
  - +12.5% is a jump and exactly +10% is not
  - 50 → 30 (Recovery Week) → 60 km is a jump against 50, and 50 → 30 → 54 km is not
  - two Recovery Weeks in a row
  - no jump after a week with no Runs, and none when the week found by skipping back had none
  - outside a block, 50 → 30 → 50 km is a jump
  - the current week becoming a jump mid-week
  - a Run at 23:30 on Sunday counting in its own week

---

# 04: Distance Jumps on the Training page (run weeks)

**What to build:** The Training page warns about Distance Jumps in what was run. This shows on each week of the block view and on the recent weeks of the countdown, so the Athlete is warned both during a Training Block and between blocks.

**Blocked by:** 03

**Status:** ready-for-agent

- [ ] The actual values of every block week on the Training page return `distanceJump`, using the rule and Recovery Week lookup from 03.
- [ ] Every recent week of the countdown returns `distanceJump`. The oldest recent week is still compared with the week before it, even though that week isn't listed.
- [ ] The block rows and the countdown's recent weeks show a warning marker on Distance Jump weeks, with the increase and comparison week on hover.
- [ ] A past Training Block shown in the comparison carries its own Distance Jumps.
- [ ] API tests cover the field on block weeks (including a week after a Recovery Week of the plan) and on the countdown's recent weeks (including the oldest one listed).

---

# 05: Planned Distance Jumps

**What to build:** While entering Weekly Targets in the Training Plan editor, the Athlete sees which planned weeks are Distance Jumps compared with the planned week before (skipping back past Recovery Weeks), so a build that's too steep is caught while planning. The plan side of each block week on the Training page shows them too.

**Blocked by:** 03

**Status:** ready-for-agent

- [ ] Planned weeks are compared only with planned weeks of the same Training Plan, by their planned distance, using the rule from 03. Planned weeks are never compared with run weeks.
- [ ] The first plan week never has a planned Distance Jump. Neither does a week without a planned distance, or one whose comparison week has none.
- [ ] Every week in the Training Plan response returns `distanceJump`. So does the plan side of every block week on the Training page.
- [ ] The editor shows planned Distance Jumps from the last saved response, refreshed after each save, as a warning that never blocks saving.
- [ ] The block rows show a planned Distance Jump separately from one in what was run.
- [ ] API tests cover:
  - the first plan week
  - a week next to one without a planned distance
  - skipping a planned Recovery Week
  - a block week whose plan side and actual side disagree

---

# 06: Consistency card: heatmap and year picker

**What to build:** A new card on the Overview shows a calendar heatmap of one year, with one cell per day shaded by that day's distance, and a year picker that starts at the current year. It shows at a glance how regularly the Athlete runs and where the gaps are.

**Blocked by:** None (can start immediately)

**Status:** ready-for-agent

- [ ] The Overview response returns `days`: every local date with at least one Run and that day's total distance, across the whole history.
- [ ] A Run counts on its local start date (the same date that decides its Training Week). Two Runs on one day are added together.
- [ ] The card has a year picker over the years with Runs, starting at the current year. Changing the year needs no new request.
- [ ] The heatmap shows only the chosen calendar year (1 Jan to 31 Dec), Monday-first rows with one column per week. Cells are shaded in a few steps relative to the longest day of that year, with a tooltip giving the date and distance.
- [ ] With no Runs yet, the card shows an empty heatmap. It reloads after a sync like the other Overview cards.
- [ ] API tests cover:
  - a double summed into one day
  - a late-night Run and a Run abroad falling on their local date
  - days around New Year where one Training Week spans two years
  - no Runs at all
- [ ] A component test (like the Yearly Goal card's) checks that the year picker changes the heatmap.

---

# 07: Streaks

**What to build:** The consistency card shows the Athlete's current Streak (consecutive Training Weeks with at least one Run) and their longest Streak ever, with when it was. Rest days never break a Streak. On a Monday before the first Run of the week, the Streak still counts the weeks up to last week.

**Blocked by:** 06

**Status:** ready-for-agent

- [ ] The Overview response returns `streak`: the current Streak in Training Weeks, and the longest Streak with its first and last Training Week (Mondays).
- [ ] The current Training Week adds to the current Streak once it has a Run, and only breaks it once its Sunday has passed without one. Any earlier week without a Run ends the Streak.
- [ ] The longest Streak covers the whole history. When two are equally long, the most recent one is shown.
- [ ] Without Runs, the current Streak is 0 and there's no longest Streak.
- [ ] The card shows both Streaks. The year picker doesn't change them.
- [ ] API tests cover:
  - Monday of the current week before any Run keeping last week's Streak
  - a full week without a Run resetting it to 0
  - the longest Streak in an earlier year, with its dates
  - equally long Streaks
  - no Runs at all
- [ ] The card's component test checks that changing the year leaves the Streaks unchanged.

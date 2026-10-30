# Running Dashboard v8

v8 adds four features to v7 ([running-dashboard-v7.md](running-dashboard-v7.md)). Terms follow [CONTEXT.md](../../CONTEXT.md), and the ADRs in [docs/adr](../adr) still apply.

| # | Feature | Status |
|---|---|---|
| 01 | Breaks | done |
| 02 | Weekly Time Goal | done |
| 03 | Goals in Settings | done |
| 04 | Streak start | done |

---

## 01: Breaks

The Athlete marks the days of an injury, an illness or a holiday, so a gap in the charts or the Streak explains itself.

### What a Break is

- A kind (Injury, Illness, Holiday or Other), a first and a last day (the same day for a Break of one day), and an optional note of up to 200 characters, e.g. "Calf strain".
- A Break only explains. Totals, Training Weeks, the Streak and the Longest Streak, Records, Training Blocks and goals count as they would without it, and no other response of the API changes.
- Two Breaks never overlap, and a Break is at most 366 days long: a longer injury is two Breaks one after another.
- Each kind has a colour of its own, the same in every Theme, as Phases have (`--break-injury`, `--break-illness`, `--break-holiday` and `--break-other` in styles.scss).
- Breaks are stored in a `Breaks` table. `GET /api/breaks` lists them oldest first; `POST /api/breaks`, `PUT /api/breaks/{id}` and `DELETE /api/breaks/{id}` mark, change and remove one. A request that breaks a rule is refused with the problem by field (`kind`, `start`, `end`, `note`), an overlap under `start`.

### On the Calendar

- A "Breaks" card under the month's Training Weeks lists the Breaks with a day in that month, and under the year's months those with a day in that year. Each shows its kind, its days ("10 Sept – 16 Sept 2026"), how many days it has, its note, and "Edit" and "Remove" (after a confirmation).
- "Mark a Break" opens a modal with Kind, From, To and Note, starting on today when today is in the month or year shown, otherwise on its first day. "Edit" opens the same modal on the Break. Esc or Cancel closes it without saving.
- In the month view, a Break's days are tinted in its kind's colour, with its kind and note on hover. Its kind is named on its first day and on its first day in each later Training Week of the grid. Where the month is a list of days, a day that names its Break shows even without activities.

### Elsewhere

- **Overview**: "Distance per Training Week" and "Training Time per Training Week" shade the weeks a Break has days in, named by its kind, and a week's tooltip names its Breaks with their notes.
- **Streak**: a week without a Run that has a day in a Break is coloured in its kind's colour in the strip, and its title names the Break. While the current week has no Run and is in a Break, the status says "Break this week: Injury · Calf strain" instead of what the week still needs, and on its last day it gives no warning. When the Streak is 0 and the week that ended it was in a Break, it says "The Streak ended in a Break (Injury) · Run this week to start a new one". The count stays as it is.
- **Fitness**: Aerobic Efficiency, Cardiac Drift and Cadence shade each Break within the dates of the chart's Runs, from its first day to the end of its last, named by its kind.
- **Training**: a Training Block week's dates are followed by the kind of each Break it has days in, with the note on hover, so a missed Weekly Target reads as, say, an Illness.

## 02: Weekly Time Goal

Next to the Yearly Goal's distance, the Athlete sets how many hours a week they want to train, Rides and Swims included.

### The goal

- One Weekly Time Goal, the same for every Training Week, in seconds: more than nothing and at most 100 hours. It is stored in a `WeeklyTimeGoals` table with a single row. `PUT /api/weekly-time-goal` takes `{ time }` and sets or replaces it, `DELETE /api/weekly-time-goal` removes it; a time out of range is refused under `time`.
- It counts Training Time: the Moving Time of the week's Runs, Rides (outdoor and Virtual) and Swims together, as the Training Time chart adds them up. A week meets it once its Training Time reaches it.
- `GET /api/overview` gives the goal (`weeklyTimeGoal`, none without one) and the current Training Week's Training Time (`thisWeek`), which is there without Rides and Swims too. A changed goal counts for every week, past ones included; there is no history of goals.

### Weekly goal card

- A "Weekly goal" card on the Overview, between the Yearly Goal and the Streak. Where the window is narrower, the Yearly Goal takes the whole row and the other two share the next.
- With a goal: this week's Training Time against it ("4h 00m / 7h") and the share done, a bar with a part per sport in its Training Time colour (Runs, then Rides, then Swims, together at most the whole bar), and what the week still needs: "To go", "Days left" (today included, 7 on Monday) and "Needed / day". Once it is reached, "Goal reached".
- Without one: "No weekly time goal yet." and "Set a goal".
- "Set a goal" and "Edit" take the hours per week as 7, 7.5, 7,5 or 7:30, to the minute. "Remove" asks first.

### In the Training Time chart

- A dashed line across "Training Time per Training Week" at the goal, labelled "goal 7h".
- Next to the year's other figures, "Weekly goal met: 12 of 40 weeks": the year's weeks whose Training Time reached the goal, out of its weeks. The current week counts only once it has met the goal.
- Without Rides and Swims the chart is still left out, and the card is all there is of the goal.

## 03: Goals in Settings

The Athlete chooses which goals the Overview shows.

- A "Goals" card in Settings, under the Theme, with "Show" and "Hide" for the Yearly Goal and for the Weekly Time Goal, each saved on its own as the other settings are (`showYearlyGoal` and `showWeeklyTimeGoal` in `/api/settings`). Both are shown unless hidden, also in a settings row stored before v8.
- A hidden Yearly Goal leaves out its card. A hidden Weekly Time Goal leaves out its card, its line in the Training Time chart and "Weekly goal met".
- A hidden goal is kept as it is, and shown again as it was; `GET /api/overview` still gives it.
- The goal row lays out what it has: three cards as before, two as the Yearly Goal and the Streak were in v7, and the Streak alone across the row.

## 04: Streak start

- The Streak card says since when the current Streak runs, next to its count: "14 weeks since 29 Jun", the Monday of its first Training Week, with the year when that is not the current one ("since 20 Oct 2025").
- A Streak of 0 has no start. `GET /api/overview` gives it as the Streak's `start`.

---

## Out of scope

- Breaks that change what counts: pausing the Streak, leaving a Break's weeks out of averages, or easing Weekly Targets.
- Breaks in the Year in Review, its image and story, on a Run's page or in the CSV export.
- Marking a Break anywhere but the Calendar, e.g. from the Streak card.
- Breaks for Rides or Swims alone.
- A Weekly Time Goal for one sport only, a goal per week (e.g. lower in a Recovery Week), or goals for past weeks as they were then.
- Weekly Time Goal progress in the Streak, the Calendar, the Training page or the Year in Review.

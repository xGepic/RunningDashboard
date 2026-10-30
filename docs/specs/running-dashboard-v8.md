# Running Dashboard v8

v8 adds one feature to v7 ([running-dashboard-v7.md](running-dashboard-v7.md)). Terms follow [CONTEXT.md](../../CONTEXT.md), and the ADRs in [docs/adr](../adr) still apply.

| # | Feature | Status |
|---|---|---|
| 01 | Breaks | done |

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

### Out of scope

- Breaks that change what counts: pausing the Streak, leaving a Break's weeks out of averages, or easing Weekly Targets.
- Breaks in the Year in Review, its image and story, on a Run's page or in the CSV export.
- Marking a Break anywhere but the Calendar, e.g. from the Streak card.
- Breaks for Rides or Swims alone.

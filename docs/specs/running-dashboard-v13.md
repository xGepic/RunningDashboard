# Running Dashboard v13

v13 adds one feature to v12 ([running-dashboard-v12.md](running-dashboard-v12.md)). Terms follow [CONTEXT.md](../../CONTEXT.md), and the ADRs in [docs/adr](../adr) still apply.

v1 left a fitness/fatigue model out of scope; this reverses that, from the Relative Effort every Run already has.

| # | Feature | Status |
|---|---|---|
| 01 | Fitness & Freshness | done |

---

## 01: Fitness & Freshness

The Athlete sees how their training builds Fitness and Fatigue, and whether they are fresh or loading, as on Strava's chart of the same name.

### The figures

- A day's Training Load is the Relative Effort of its Runs, summed: treadmill Runs and race days count like any other Run, as they load the body all the same (they are only left out of the Fitness Runs, whose Pace must be reliable). A Run without Relative Effort adds nothing. Rides and Swims count toward nothing.
- Fitness and Fatigue are exponentially weighted averages of the daily load with time constants of 42 and 7 days (Banister's impulse-response model): each day, Fitness moves a 42nd and Fatigue a 7th of the way from the day before towards that day's load, days without a Run at zero. Both start at zero on the first day with a Run, so Fitness takes some weeks to build at the very start of the history.
- Form is Fitness minus Fatigue.
- `GET /api/fitness/freshness` gives `days`, every day from the first Run's up to today (or a later Run's day), oldest first, each with its `date`, `load`, `fitness`, `fatigue` and `form` to one decimal, and the `runId` and `runName` of its hardest Run by Relative Effort (none on a day without a Run); empty without Runs. It also gives `races`: every Goal Race's `date` and `name`.

### On the Fitness page

- A card at the top, above Cadence, without a title or explanation: where the Athlete is in their training heads it, large, beside the range switch (screen readers still name the card "Fitness & Freshness").
- First, in words, where the Athlete is in their training today, from the whole history. With a Goal Race in the next three weeks: "You're tapering for Salzburg Marathon in 12 days: Fatigue is below Fitness, so you'll arrive fresh." while Fatigue is below Fitness, else "Salzburg Marathon is in 12 days and you're still training harder than your recent average; easing off now lets you arrive fresh." ("tomorrow", "today"). Otherwise by Fatigue over Fitness (the acute:chronic workload ratio): above 1.5 "You're ramping up fast: Fatigue is well above Fitness. Big jumps like this are when injuries happen, so take it easier for a few days."; above 1 "You're building: training harder than your recent average, so your Fitness is climbing."; from 0.8 "You're holding steady: training about as much as your recent average."; below 0.8 "You're recovering: training less than your recent average, so your Fitness is dropping." In the first six weeks of the history: "It takes about six weeks of Runs before Fitness and Fatigue settle, so this is only a first look."; with Fitness below 1: "You haven't trained for a while, so your Fitness is close to zero."
- Under it, small: today's Fitness, Fatigue and Form.
- A chart of the days shown: Fitness as a filled line in the Theme's colour, Fatigue faint behind it in the Theme's second chart colour (as Rides have elsewhere), each named at its end. Form has no line of its own: it is the gap between them. Goal Races within the range, and up to three of its hardest days, are dashed lines named at the top ("Finisher. · 27 Sep"): days with at least twice the load of a typical day with a Run (the median), hardest first, each at least an eighth of the range (and a week) from a harder one; a hard day on a Goal Race is named by the race. Clicking a hard day's marker opens its hardest Run's page; a Goal Race's marker cannot be clicked unless it is one of the hard days. Breaks are shaded and named by their kind. Pointing at a day shows its date and hardest Run, Fitness, Fatigue and Training Load, and a Goal Race or Break on it.
- A range switch: 3M, 6M (unless the Athlete picks another), 1Y and All, each up to today. Fitness and Fatigue are always reckoned over the whole history; the range only picks what is shown; the sentence on where the Athlete is always looks at today.
- "Show week by week as a table": each Training Week of the range, newest first, with its Training Load and the Fitness, Fatigue and Form of its last day shown.
- Without Runs: "No Runs yet, so no Fitness or Fatigue.", without the range switch.
- The page's subtitle is now about both cards, and the Fitness Runs' explanation moved into the Cadence card.

---

## Out of scope

- Rides and Swims in the Training Load, or a load of Runs' own without heart rate (e.g. from distance and Pace).
- Zones for Form (e.g. "race ready"), or a target Form or Fitness for race day.
- Planning ahead: the Fitness, Fatigue and Form a Training Plan's Weekly Targets would bring by race day.
- Fitness & Freshness on the Overview, the Training page, a Run's page or in the Year in Review.
- Remembering the range picked.

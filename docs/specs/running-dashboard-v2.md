# Running Dashboard v2: Run Types, Distance Jumps and Streaks

## Problem Statement

The Athlete's second marathon is on 23 May 2027. Its 16-week Training Block starts on Monday 1 Feb 2027 and has a Target Time of under 5:00:00. v1 lets them plan each Training Week with a Weekly Target, Running Days and Recovery Weeks, and compare that plan with the Runs they actually did. Three things are still missing:

- **The plan doesn't say what each Running Day is for.** A Training Week of Tuesday, Thursday and Sunday doesn't show that Tuesday is easy, Thursday is a tempo and Sunday is the long one. The Athlete keeps that in their head. Looking back at a week, they can't see which kind of day they missed.
- **Nothing warns about building up too fast.** A sharp rise in weekly distance is a common cause of running injuries. The dashboard shows the weekly distances but doesn't flag a jump, either in what was run or, earlier, while the Athlete enters the Weekly Targets in January.
- **Long-term consistency is hard to see.** The Overview shows totals and weekly bars, but not how regularly the Athlete runs. Gaps from illness or holidays don't stand out, and there's no Streak to keep going.

## Solution

Three additions to the existing pages. Everything is built before the block starts on 1 Feb 2027.

- **Run Types in the Training Plan.** Each Running Day of a plan week gets a Run Type: Easy, Long, Tempo, Intervals or Race. Running Days are now simply the days with a Run Type. The plan editor picks a Run Type per day instead of switching weekdays on and off, and the calendar strip on the Training page shows each day's Run Type. A Running Day still counts as run if there's any Run on that date. Running Days already entered become Easy.
- **Distance Jumps.** A Training Week whose distance is more than 10% above the week it's compared with is marked as a Distance Jump. That comparison week is the previous Training Week, or, if that was a Recovery Week, the last week before it that wasn't. Weeks that were run are compared with weeks that were run, and planned weeks with planned weeks. Distance Jumps appear wherever Training Weeks are listed: in the Training Plan editor, the Training block's weeks, the countdown's recent weeks and the Overview's "Distance per Training Week" chart.
- **A consistency card on the Overview.** It has a calendar heatmap of the chosen year, with one cell per day shaded by that day's distance, plus the current Streak and the longest Streak ever. A Streak is the number of consecutive Training Weeks with at least one Run.

## User Stories

### Run Types in the Training Plan

1. As the Athlete, I want to give each Running Day of a plan week a Run Type (Easy, Long, Tempo, Intervals or Race), so that the plan says what each day is for, not just that I run.
2. As the Athlete, I want a day without a Run Type to be a rest day, so that I don't have to keep a separate set of Running Days in sync with the Run Types.
3. As the Athlete, I want the list of Run Types to be fixed, so that the plan editor stays simple and every plan uses the same words.
4. As the Athlete, I want to pick a Run Type or "rest" for each of the seven days in a plan week in the editor, so that planning a day takes one choice.
5. As the Athlete, I want "copy from previous week" to copy the Run Types too, so that entering 16 weeks of mostly identical weeks stays quick.
6. As the Athlete, I want to plan more than one day as Long (or Tempo, or any other Run Type) in the same week if I choose to, so that the editor doesn't block an unusual week.
7. As the Athlete, I want to plan a day as Race in any week, so that a tune-up race shows up in the plan without becoming a Goal Race.
8. As the Athlete, I want the Running Days I already entered before this change to become Easy days, so that my existing plan keeps working and I only need to change the days that are something else.
9. As the Athlete, I want a clear error when a plan week names an unknown Run Type or plans the same day twice, so that I can't save a broken week.
10. As the Athlete, I want the calendar strip on the Training page to show each Running Day's Run Type, so that I can see at a glance which kind of day I ran or missed.
11. As the Athlete, I want a Running Day to count as run as soon as there is any Run on that date, whatever its distance, so that the calendar strip keeps working the way it does now.
12. As the Athlete, I want two Runs on the same day to count for that one Running Day, so that a double doesn't look like a Run on an unplanned day.
13. As the Athlete, I want a Run on a day without a Run Type to still show as a Run on an unplanned day, with no Run Type, so that I can see where I ran off-plan.
14. As the Athlete, I want a missed Running Day to still show its Run Type, so that I can tell a missed Long day from a missed Easy day.
15. As the Athlete, I want the Long Run of a Training Week to still be its longest Run, whichever day I planned as Long, so that a Long Run moved to another day still counts against the planned Long Run.
16. As the Athlete, I want my Runs never to be given a Run Type, and Strava's own workout type to be ignored, so that a Run Type always means what I planned and nothing is guessed.
17. As the Athlete, I want the Weekly Target (planned distance and planned Long Run) to stay as it is, separate from the Run Types, so that I keep planning distance per week, not per day.
18. As the Athlete, I want moving the Goal Race's date to move the Run Types with the plan, so that the plan stays tied to Weeks to Race as before.
19. As the Athlete, I want a past Training Block's plan to show its Run Types the same way, so that comparing blocks shows how I planned each one.

### Distance Jumps

20. As the Athlete, I want a Training Week marked as a Distance Jump when its distance is more than 10% above the week before, so that I notice when I'm building up too fast.
21. As the Athlete, I want exactly 10% more not to count as a Distance Jump, so that the rule matches the usual "no more than 10%" advice.
22. As the Athlete, I want a week after a Recovery Week compared with the last week before it that wasn't a Recovery Week, so that going back to normal isn't flagged but going beyond it is. For example, 50 → 30 (Recovery Week) → 54 km is fine, while 50 → 30 → 60 km is a Distance Jump.
23. As the Athlete, I want two Recovery Weeks in a row to be skipped together, so that the comparison always goes back to a normal week.
24. As the Athlete, I want no Distance Jump after a week with no Runs (illness, holiday), so that coming back isn't marked as an endless jump.
25. As the Athlete, I want no Distance Jump when the week found by skipping Recovery Weeks had no Runs, so that the same rule holds when skipping back.
26. As the Athlete, I want weeks outside a Training Block, or inside one without a Training Plan, to have no Recovery Weeks and so always be compared with the previous week, so that the rule stays simple. A normal week after a lighter one will warn there, and that's acceptable.
27. As the Athlete, I want a run week to count as a Recovery Week when it falls in a Training Block whose Training Plan marks that Weeks to Race as one, so that what was run uses what was planned.
28. As the Athlete, I want the current Training Week marked as a Distance Jump as soon as its distance so far passes the limit, so that I'm warned before I add the week's Long Run on top.
29. As the Athlete, I want each Distance Jump to say which week it was compared with and by how much it went up, so that I can see why it was flagged.
30. As the Athlete, I want planned weeks in the Training Plan editor marked as Distance Jumps by their Weekly Target, so that I catch a too-steep build while planning in January rather than in week 6.
31. As the Athlete, I want planned weeks compared only with planned weeks, and run weeks only with run weeks, so that a short current week doesn't make next week's plan look like a jump.
32. As the Athlete, I want the first week of a Training Plan to have no planned Distance Jump, so that it isn't compared with weeks outside the plan.
33. As the Athlete, I want no planned Distance Jump on a week that has no planned distance, or whose comparison week has none, so that half-entered plans don't warn.
34. As the Athlete, I want Recovery Weeks skipped for planned weeks in the same way, so that a planned return after a Recovery Week isn't flagged unless it goes beyond the week before the Recovery Week.
35. As the Athlete, I want the plan editor to show planned Distance Jumps for the saved plan after each save, so that I can adjust the Weekly Targets and see the warning go away.
36. As the Athlete, I want the Training block's weeks to show Distance Jumps for both the plan and what was run, so that I can tell a planned jump from one I ran on my own.
37. As the Athlete, I want the countdown view's recent weeks to show Distance Jumps, so that I'm warned between Training Blocks too.
38. As the Athlete, I want the Overview's "Distance per Training Week" chart to mark Distance Jump weeks across my whole history, so that I can see when I built up too fast in the past.
39. As the Athlete, I want a Distance Jump shown as a warning, not an error, so that it never stops me from saving a plan.

### Consistency

40. As the Athlete, I want a consistency card on the Overview, so that I can see how regularly I run.
41. As the Athlete, I want a calendar heatmap with one cell per day of the chosen year, so that I can spot gaps and busy stretches at a glance.
42. As the Athlete, I want each day's cell shaded by that day's distance, so that Long Run days stand out from short ones.
43. As the Athlete, I want two Runs on the same day added together in its cell, so that a double shows as one bigger day.
44. As the Athlete, I want a Run to count on its local start date, so that a Run at 23:30 or one abroad falls on the day I ran it, the same as for Training Weeks.
45. As the Athlete, I want a year picker on the card, starting at the current year, so that it lines up with the Yearly Goal card and I can look back at earlier years.
46. As the Athlete, I want the heatmap to show only the calendar year I picked, even where a Training Week crosses into the next year, so that every year starts on 1 January.
47. As the Athlete, I want to see my current Streak, the number of consecutive Training Weeks with at least one Run, so that I have something to keep going.
48. As the Athlete, I want to see my longest Streak ever, and when it was, so that I have a record to beat.
49. As the Athlete, I want the Streaks to always cover my whole history, whichever year the heatmap shows, so that changing the year doesn't change my Streaks.
50. As the Athlete, I want the current week to add to my Streak once it has a Run, and only to break it once its Sunday has passed without one, so that a Monday before my first Run doesn't show a Streak of zero.
51. As the Athlete, I want a Streak counted in Training Weeks rather than days, so that the rest days in my own plan never break it.
52. As the Athlete, I want an empty card with a Streak of 0 before the first import has brought in any Runs, so that the Overview still loads.
53. As the Athlete, I want the card to update after a sync like the rest of the Overview, so that today's Run shows up without reloading.

## Implementation Decisions

### Glossary

- CONTEXT.md already has the new terms: **Run Type**, **Running Days** (each has a Run Type), **Streak** and **Distance Jump**. The UI, the API and the code use these words and avoid the alternatives listed there (workout, session, load warning, spike, run streak).

### Run Types

- **Model.** A Training Plan week no longer stores a set of weekdays. It stores its Running Days, each a weekday with a Run Type, at most one per weekday. A day without one is a rest day. The Run Type is a fixed enumeration: Easy, Long, Tempo, Intervals, Race. In the API, as with Phases, it's written in lower case: `easy`, `long`, `tempo`, `intervals`, `race`.
- **Migration.** An EF Core migration turns every stored Running Day into a Running Day with Run Type Easy. Nothing else in the plan changes.
- **API contract.** This replaces the current list of weekday names, and the frontend changes with it:
  - The week request of the "plan weeks" endpoint takes `runningDays` as a list of `{ day, runType }`, in any order.
  - The Training Plan response returns them Monday first.
  - Validation errors stay keyed `weeks[i].runningDays`, for an unknown day, an unknown Run Type, or the same day twice.
- **Calendar strip.** Each day of a block week's plan gains a `runType`: the Running Day's Run Type, or none for rest days and for Runs on unplanned days. Day states (ran, missed, unplanned, planned, rest) and how they're worked out don't change: a Running Day counts as run if there's any Run on that date.
- **Long Run and Weekly Target** don't change. The Long Run is still the longest Run of the week, and the planned Long Run is still checked against it.
- **Plan editor.** The Running Day toggles become a picker per day with "rest" and the five Run Types. "Copy from previous week" copies them.
- **Training page.** The calendar strip shows the Run Type of each Running Day (a short label or letter, with the full name on hover).

### Distance Jumps

- **One rule, in one place in the backend.** The Distance Jump rule is a pure function in the training calendar module. It takes a sequence of consecutive Training Weeks, each with a distance (or none), whether it had Runs or a planned distance, and whether it's a Recovery Week. For each week it returns either no Distance Jump, or the week it was compared with and the increase as a ratio. Every endpoint below uses this function. The frontend never computes Distance Jumps.
- **The rule.**
  - A week is a Distance Jump if its distance is more than 1.10 times the distance of its comparison week.
  - The comparison week is the previous Training Week, skipping back past Recovery Weeks to the last one that isn't.
  - There's no Distance Jump if no comparison week exists, if the comparison week had no Runs (or, for planned weeks, has no planned distance), or if the week itself has no distance.
  - The comparison uses metres, without rounding.
- **Run weeks.** A Training Week is a Recovery Week if it falls in a Goal Race's Training Block whose Training Plan marks that Weeks to Race as a Recovery Week. Otherwise it isn't. A shared lookup gives the Recovery Weeks for any range of Training Weeks, so every endpoint answers the same way. The current week is judged by its distance so far.
- **Planned weeks.** Weeks are compared within a single Training Plan by their planned distance. The first plan week is never compared with anything, and planned weeks are never compared with run weeks.
- **API contract.** Each of these gains a `distanceJump` field: `{ comparedWith, increase }`, or none. `comparedWith` is the comparison week's Monday, and `increase` is the ratio minus one (e.g. 0.2 for +20%).
  - Every week in the Overview's weekly list
  - Every recent week in the Training countdown
  - The actual values of every block week on the Training page (run weeks)
  - The plan of every block week on the Training page (planned weeks)
  - Every week in the Training Plan response (planned weeks)
- **Frontend.** Distance Jumps are warnings, never validation errors.
  - The Overview chart marks Distance Jump bars, and their tooltip shows the increase and the comparison week.
  - The Training block rows and the countdown's recent weeks show a warning marker.
  - The plan editor shows planned Distance Jumps from the last saved response.

### Consistency

- **Where the data comes from.** The Overview endpoint's response gains:
  - `days`: every local date with at least one Run, and that day's total distance, across the whole history. The year picker filters these on the client, so changing the year needs no new request.
  - `streak`: the current Streak in Training Weeks, plus the longest Streak with its first and last Training Week (Mondays). Both are none or zero without Runs.
- **Streak rule.** Count back through Training Weeks from the current one. The current week counts if it has a Run. If it doesn't, the count starts from the week before without breaking. Any earlier week without a Run ends the Streak. The longest Streak is the longest run of consecutive Training Weeks with Runs, over the whole history. If two are equally long, the most recent one is shown.
- **Days and weeks.** Days use the Run's local start date, the same date that decides its Training Week.
- **Frontend.** A new consistency card on the Overview:
  - A year picker over the years that have Runs, starting at the current year
  - A GitHub-style heatmap of that calendar year (Monday-first rows, one column per week), with each cell shaded by distance in a few steps relative to the longest day of the chosen year, and a tooltip with the date and distance
  - The current Streak and the longest Streak with its date range

  It reloads after a sync, like the other Overview cards.

### Unchanged

- Strava stays the only data source (ADR 0001). The app stays local with polling sync (ADR 0002). No new Strava requests are needed: everything comes from data already stored. Strava's `workout_type` stays ignored.

## Testing Decisions

- **One seam: the backend HTTP API, tested in memory**, exactly as in v1. Tests start the real application with a real SQLite database, a fake Strava at the HTTP-handler level and a fake clock. Tests set up Runs, Goal Races and Training Plans, then assert only on the JSON that the Training, Training Plan and Overview endpoints return. Planned Distance Jumps are tested here too, because the frontend never computes them.
- **What makes a good test:** it describes something the Athlete would recognise ("a return to 54 km after a Recovery Week is not a Distance Jump") and checks only what the API returns. It never checks the Distance Jump function, EF queries or internal types directly. Restructuring the internals must not break a test.
- **Prior art:**
  - The Training Plan tests (shaping a plan, planning weeks, validation keyed `weeks[i].field`)
  - The plan-vs-actual tests (calendar strip states for the second marathon's block, clock on Wed 10 Mar 2027)
  - The countdown tests
  - The Overview tests

  New tests follow their setup helpers and naming.
- **Scenarios to cover, test-first:**
  - Run Types:
    - Saving and reading back a week with mixed Run Types
    - Unknown Run Type
    - The same day twice
    - "Rest" as the absence of a Running Day
    - The calendar strip showing the Run Type on ran and missed days, and none on unplanned days
    - A double on a Running Day
    - The Long Run still being the longest Run when the day planned as Long was shorter
    - Moving the Goal Race moves the Run Types
  - Distance Jumps on run weeks:
    - +12.5% is a jump, exactly +10% is not
    - 50 → 30 (Recovery Week) → 60 is a jump against 50; 50 → 30 → 54 is not
    - Two Recovery Weeks in a row
    - No jump after a week with no Runs, and none when the week found by skipping back had none
    - Outside a block, 50 → 30 → 50 is a jump
    - The current week becoming a jump mid-week
    - A Sunday 23:30 Run counting in its own week
    - The field showing up in the Overview weeks, the countdown's recent weeks and the block weeks
  - Distance Jumps on planned weeks:
    - The first plan week has none
    - A week next to one without a planned distance has none
    - Recovery Weeks are skipped
    - The plan side and the actual side of the same block week disagree
  - Streak and heatmap:
    - Monday of the current week before any Run keeps last week's Streak
    - A full week without a Run resets it to 0
    - The longest Streak in an earlier year, with its date range
    - Equally long Streaks
    - No Runs at all
    - Two Runs on one day summed in `days`
    - A Run abroad or late at night falls on its local date
    - Days across New Year with a Training Week spanning two years
- **Migration:** checked once by hand against a copy of the real database (existing Running Days become Easy). It isn't covered by an automated test.
- **Frontend, light tests as in v1**, with Angular's default test runner:
  - The Training Plan editor's component tests are extended: picking a Run Type and "rest", and "copy from previous week" copying Run Types.
  - The consistency card gets a component test like the Yearly Goal card's: the year picker changes the heatmap but not the Streaks.
  - No end-to-end browser tests.

## Out of Scope

- Giving actual Runs a Run Type, reading Strava's `workout_type`, or checking whether a Run matched its Run Type (e.g. whether a Tempo day really was a tempo)
- A planned distance, pace or interval structure per Running Day, and checking a day by distance
- Editing the list of Run Types
- Rules between Run Types and the Weekly Target (e.g. "exactly one Long day per week", or the Long day matching the planned Long Run)
- Distance Jumps by Relative Effort, Moving Time or acute:chronic ratio, and fitness/fatigue curves
- Comparing planned weeks with run weeks for Distance Jumps, and comparing the first plan week with weeks before the block
- Blocking a save, or any notification, because of a Distance Jump
- Daily Streaks, Streaks that need more than one Run per week, and Streaks of weeks that met the Training Plan
- A rolling 52-week heatmap
- Race prediction, per-second streams, Shoe settings, a Goal Race history page, and choosing Training Blocks to compare. These were reconsidered as candidates but not picked for this round.
- Everything ruled out by ADRs 0001 and 0002: Garmin or any second data source, deployment and webhooks

## Further Notes

- **Deadline:** the second marathon's Training Block starts on Monday 1 Feb 2027. Run Types and planned Distance Jumps have to work before then, because the plan is entered in January.
- **Relation to v1:** the v1 spec's Out of Scope list described v1 only. This spec takes back part of "daily planned workouts, matching Runs to planned workouts": days get a Run Type, but matching stays by date only. The other items on that list are still not planned, apart from what the ADRs rule out for good.
- **Why week over week rather than acute:chronic:** the Athlete chose the simpler rule they recognise, and skipping Recovery Weeks removes its main false alarm inside a block. Outside a block, a normal week after a lighter one will warn, and the Athlete has accepted that.

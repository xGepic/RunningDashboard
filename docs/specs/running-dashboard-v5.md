# Running Dashboard v5

v5 adds two features to v4 ([running-dashboard-v4.md](running-dashboard-v4.md)). Terms follow [CONTEXT.md](../../CONTEXT.md), and the ADRs in [docs/adr](../adr) still apply.

| # | Feature | Status |
|---|---|---|
| 01 | Achievements | done |
| 02 | Year in Review story | done |

---

## 01: Achievements

The Athlete collects badges for the distances they reach and sees what the next one is.

### What can be earned

Achievements are about distance only, in four groups. Every threshold is its own Achievement, so a group is a ladder of badges.

| Group | Achievements | Earned |
|---|---|---|
| Single Run | 10K, Half-Marathon, 30K, Marathon, 50K | once, by the first Run with a Best Effort over that distance |
| Lifetime | 100, 250, 500, 1,000, 2,000, 3,000, 5,000, 7,500 and 10,000 km | once, by the Run that takes the distance of all Runs past it |
| Year | 500, 1,000, 1,500 and 2,000 km in a calendar year | again in every year that reaches it, by the Run that takes the year past it |
| Month | 100, 150, 200 and 250 km in a calendar month | again in every month that reaches it |
| Training Week | 40, 60, 80 and 100 km in a Training Week | again in every Training Week that reaches it |

- Single Run Achievements use the Best Effort, as Race Results do (ADR 0003), so a Run whose GPS came up a few metres short of 21,097 m doesn't count as a Half-Marathon. Strava has no Best Effort over 50K, so one 50K Run earns it once and that is it.
- Each Achievement is dated by the day of the Run that earned it and links to that Run.
- A repeatable Achievement shows how often it was earned ("×3") and when it was earned last. A year, month or week earns every threshold it passes, so a 210 km month earns 100, 150 and 200 km.
- Thresholds are in km, whatever the Distance Unit. Their names say km (e.g. "1,000 km year"), as Best Effort names say 10K. Distances shown next to them, such as progress, follow the Distance Unit.
- Achievements are worked out from the stored Runs on every request, and nothing about them is stored. The whole history counts from its first Run, so the first sync earns everything already reached. A Run deleted on Strava takes its Achievements with it.
- Only Runs count. Rides and Swims never earn Achievements, as they never count toward anything.

### Achievements page

- A new "Achievements" link in the sidebar, under Records (`/achievements`).
- One section per group, with the badges in threshold order:
  - **Earned** badges are in the Theme's colour, with the date and a link to the Run, plus the count and last date for repeatable ones.
  - **Locked** badges are greyed out, with progress towards them:
    - Lifetime: the distance so far, as a bar.
    - Year, Month and Training Week: the current year, month or week so far.
    - Single Run: the longest Run so far.
  - Only the next locked badge of each group shows progress. The ones after it only show their threshold.
- A summary at the top: "17 of 26 earned", and the most recent Achievement.

### Overview card

- An Achievements card on the Overview shows the latest Achievement, marked "New" for 7 days after its Run, and the closest locked Achievement, the one with the highest share of its threshold, with its progress bar. When the same Run earned several, the card shows the first in the groups' order (a Single Run before a Training Week) and says how many more it earned.
- It links to the Achievements page.

### In the Year in Review

- The Review gets an Achievements section with the Achievements earned that year: first-time ones by name, and repeatable ones with how often they were earned that year (e.g. "100 km month ×4"), under a line such as "10 earned in 2026 · 4 for the first time".
- The Year in Review image keeps the section, without Run names or days of the month, as with the other highlights.

## 02: Year in Review story

The Athlete can watch the year's highlights unfold as a short animated story instead of reading them.

- A "Play" button next to "Download image" on the Calendar's Review opens the story over the whole dashboard window. It plays the year that the Review shows, and labels the current year "so far" as the Review does.
- The story is a series of full-screen cards in the Theme. Each card moves on by itself after about 5 seconds (one that brings in its Personal Bests or Achievements one by one stays 0.6 seconds longer for each, up to 10 seconds), and a row of segments at the top shows where the story is. In order:
  1. **Intro**: "Stefan's 2026 in Runs", titled with the Athlete's first name as the image is.
  2. **Distance**: the year's distance counting up from 0, then its change against the previous year (or the same time last year, for the current year).
  3. **Time and climb**: hours on your feet and metres climbed, counting up, plus the number of Runs.
  4. **Months**: the month bars growing one after another, with the biggest month lit up and named.
  5. **Longest Run and biggest Training Week**: one after the other on the same card.
  6. **Longest Streak**: the weeks counting up, with its dates.
  7. **Personal Bests**: one medal at a time, each with its time and how much faster it was. The biggest leap comes last.
  8. **Achievements**: the badges earned that year, popping in one by one.
  9. **Goal Races**: each race with its Race Result and whether it beat the Target Time.
  10. **Most-used Shoe**.
  11. **Outro**: the year in three numbers, with "Replay", "Download image" and "Close".
- A card with nothing to show for the year (e.g. no Personal Bests, no Goal Races) is left out. A year without Runs has no Play button.
- Controls:
  - Click the right two thirds or press → for the next card, and the left third or ← for the previous one.
  - Hold the mouse button down, or press Space, to pause.
  - Esc or ✕ closes the story and goes back to the Review.
- With the system's "reduce motion" setting, cards show their final numbers at once and fade instead of sliding. They still move on by themselves.
- It uses the Review's data and the Calendar's month totals, so it needs nothing new from the backend apart from the year's Achievements (feature 01). It is drawn in the browser, so it works offline and in the packaged app.

---

## Out of scope

- Achievements for consistency (Streaks, Run counts), racing and plans (Target Times, Weekly Targets met), or conditions (Temperature, elevation, special days). These can be new groups later.
- Notifications when an Achievement is earned, besides the Overview card's "New".
- Saving the story as a video, adding music to it, or sharing it anywhere other than through the existing image download.
- A story for anything but a whole year (e.g. a Training Block).

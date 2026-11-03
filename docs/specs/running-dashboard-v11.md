# Running Dashboard v11

v11 adds one feature to v10 ([running-dashboard-v10.md](running-dashboard-v10.md)). Terms follow [CONTEXT.md](../../CONTEXT.md), and the ADRs in [docs/adr](../adr) still apply.

| # | Feature | Status |
|---|---|---|
| 01 | Virtual Journey | done |

---

## 01: Virtual Journey

Each calendar year, the year's running distance carries the Athlete on a tour of European cities, from Vienna and back. The tour is picked to fit the Yearly Goal, so reaching the goal brings the Athlete back to Vienna, whether they run 500 km a year or 3,000.

### The tours

- 23 loops from Vienna through real cities and back, from about 194 km (Vienna → Bratislava → Sopron → Wiener Neustadt → Vienna) to about 2,970 km (through ten capitals as far as Amsterdam), at most about 210 km apart up to 2,300 km and about 320 km beyond. They are fixed in the backend.
- Each leg is the great-circle distance between the two cities' centres, as the crow flies, and the map draws it as a straight line: Bratislava is always 55 km from Vienna.
- The year's tour is the longest that fits within its Yearly Goal, so reaching the goal always completes it; a goal shorter than every tour gets the shortest. A 1,600 km goal gets Vienna → Bratislava → Budapest → Zagreb → Ljubljana → Venice → Verona → Bolzano → Innsbruck → Munich → Salzburg → Vienna (1,590 km); a 500 km goal gets Vienna → Linz → České Budějovice → Brno → Vienna (499 km).
- A year without a Yearly Goal, and every earlier year (the Yearly Goal is only ever the current year's), gets the capitals tour: Vienna → Bratislava → Budapest → Zagreb → Ljubljana → Vaduz → Bern → Prague → Vienna (2,062 km).

### Where the Athlete is

- The year's distance: the distance of the year's Runs, by their local start, as in the Overview's years. Rides and Swims do not count. On 1 January the Virtual Journey starts again in Vienna; past years keep theirs.
- A Stop is reached by the Run, in order of local start, that brings the year's distance up to it, on that Run's local date. One Run can reach several Stops.
- The Athlete is between the last Stop reached and the next one, as far along the leg as the year's distance goes. Once the year's distance reaches the end, the tour is complete: back in Vienna, with the distance beyond it shown as extra.
- `GET /api/journey?year=2026` (the current year without one) gives `journey`: the `year`, its `distance`, the tour's `length`, the `yearlyGoal` it was picked for (none without one), and its `stops` in order, each with its `name`, `country`, `latitude`, `longitude`, `at` (metres from the start) and `reached` (the `date`, `runId` and `runName`; none for the start and for Stops not reached yet). It also gives `years`: the years with Runs and the current year, newest first. A year outside 1900–2099 is refused.
- `GET /api/overview` gives `journey` for the current year.

### Overview card

- A card across the Overview under the goal row, "Virtual Journey", with the year and the tour's route and its length ("2026 · Vienna and back via 10 cities, 1,590 km, picked for your 1,600 km Yearly Goal", or "…, 2,062 km" without one). It shows whenever there are Runs.
- A line from Vienna to Vienna with a dot per Stop at its place along the tour, filled up to where the Athlete is; reached Stops are filled, and a marker shows where the Athlete is. The Stops' names show under the line in a wide window; in a narrow one only the last Stop reached and the next one are named.
- Next to the line: the distance to the next Stop, large ("124 km to Vienna"), and when and where the leg began ("Left Salzburg on 27 Sep"; "Set off from Vienna" before the first Stop). Once the tour is complete: "Back in Vienna" and the extra distance.
- The year's distance, the Stops reached ("10 of 11") and the share of the tour done.
- The card opens the Journey page.

### Journey page

- At /journey, below the Overview in the breadcrumb; /journey?year=2025 for an earlier year, picked with a year switch.
- Under the title, the tour and its length; with a Yearly Goal "reach the goal and you are back in Vienna", without one that a Yearly Goal gets the longest tour that fits within it.
- A map of the tour, with the part travelled in the Theme's colour and the rest dimmed and dashed, a dot per Stop (named on hover), and a marker where the Athlete is.
- A table of the Stops: name and country, distance from the start, and when each was reached and with which Run (opening the Run's page), or how far it still is.
- The same figures as on the card.

---

## Out of scope

- Choosing a tour by hand, or tours of the Athlete's own; tours starting anywhere but Vienna; road or path distances instead of straight lines.
- Keeping earlier years' Yearly Goals, so their tours stay the ones picked for them.
- Marking on the tour where the Athlete should be today to reach the Yearly Goal.
- A Virtual Journey over all years, or from a date the Athlete picks; a second lap once the tour is complete.
- Rides and Swims on the Virtual Journey.
- The Virtual Journey in the Year in Review, its image or story, or on the Calendar.
- A Settings switch to hide the card.

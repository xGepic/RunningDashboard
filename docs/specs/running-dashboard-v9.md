# Running Dashboard v9

v9 adds two features to v8 ([running-dashboard-v8.md](running-dashboard-v8.md)). Terms follow [CONTEXT.md](../../CONTEXT.md), and the ADRs in [docs/adr](../adr) still apply.

| # | Feature | Status |
|---|---|---|
| 01 | On this day | done |
| 02 | Shoe due date | done |

---

## 01: On this day

The Overview recalls the Runs on today's date a year ago.

- The Runs on today's date a year ago, by their local start, newest first; on 29 February those on 28 February a year ago. Earlier years and Rides and Swims are left out.
- `GET /api/overview` gives `onThisDay`: the `day` and its `runs`, each with its id, name, local start, distance and Moving Time; none when the day has no Runs.
- An "On this day" card in the goal row after the Streak, titled "2 Nov 2025, a year ago":
  - the day's distance, large, over the names of its Runs, each opening the Run's page and followed by its own distance when there are more than one;
  - the day's Pace and Moving Time.
- Without such Runs the card is left out. With all four cards the row holds them side by side in a wide window (1600 px and wider), two by two in a narrower one, and one under the other below 900 px.

## 02: Shoe due date

Each active Shoe on the Shoes page says when it reaches the Maximum Distance if it keeps up the distance of its last 5 weeks.

- A Shoe's recent distance is the distance of its stored Runs in the last 5 weeks, today included. `GET /api/shoes` gives it as `recentDistance`, in metres.
- Under the Maximum Distance bar: "Due around Jan 2027 · 24.5 km/week lately", the month the distance left runs out at the recent distance per day, and the recent distance per week.
- A Shoe without Runs in the last 5 weeks says "No Runs in the last 5 weeks". A Shoe past its Maximum Distance, and a retired one, says nothing more.

---

## Out of scope

- On this day for earlier years, for Rides and Swims, or for a whole week or month.
- A due date that follows a Training Plan's Weekly Targets instead of the recent weeks.
- Warning about a Shoe that is due soon anywhere but the Shoes page.

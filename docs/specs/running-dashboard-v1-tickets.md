# Running Dashboard v1: Tickets

These tickets break down [running-dashboard-v1.md](running-dashboard-v1.md). Each one is a vertical slice that works end to end (storage, API, UI, tests). They are numbered in dependency order, so blockers always come first. Terms follow [CONTEXT.md](../../CONTEXT.md), and decisions follow the ADRs in [docs/adr](../adr).

**Deadline:** the second marathon's Training Block starts Mon 1 Feb 2027. Tickets 11 → 16 → 17, plus 13 and 18, must be finished by then. They can be worked on alongside the sync tickets (03–06).

**Test seam (all tickets):** the backend HTTP API, run in an in-memory test host with real SQLite. Strava is faked at the HTTP-handler level using recorded JSON, and the clock is a fake `TimeProvider`. Tests check only the API responses.

| # | Ticket | Blocked by |
|---|---|---|
| 01 | Walking skeleton: connect Strava | none |
| 02 | Import Runs and list them | 01 |
| 03 | Resumable import that respects rate limits, with a progress indicator | 02 |
| 04 | Token refresh and reconnecting | 02 |
| 05 | Run details: Best Efforts, splits and the detail view | 03 |
| 06 | Incremental sync and rechecking recent Activities | 05 |
| 07 | Runs list: search, filters, sorting and paging | 02 |
| 08 | Overview page | 02 |
| 09 | Shoes | 02 |
| 10 | Records page | 05 |
| 11 | Goal Races: form and list | 01 |
| 12 | Race Result | 05, 11 |
| 13 | Training page: block view with actual values | 08, 11 |
| 14 | Training page: countdown and empty state | 13 |
| 15 | Comparing Training Blocks | 13 |
| 16 | Training Plan: length and Phases | 11 |
| 17 | Training Plan: weekly targets, Running Days and Recovery Weeks | 16 |
| 18 | Training page: planned vs. actual | 13, 17 |

---

# 01: Walking skeleton: connect Strava

**What to build:** The Athlete starts the app locally, clicks "Connect Strava", logs in to Strava, and comes back to an app that shows "Connected as <first name> <last name>". This sets up the whole project structure (a .NET 10 backend with ASP.NET Core minimal APIs, the latest Angular frontend with standalone components and signals, SQLite through EF Core with migrations) and the test harness that every later ticket uses.

**Blocked by:** None (can start immediately)

**Status:** ready-for-agent

- [ ] One repository containing the backend solution and the Angular app. Both start locally with one documented command each.
- [ ] The Strava client id and secret are read from .NET user secrets and never committed.
- [ ] OAuth authorization code flow with a localhost callback, requesting the `read`, `activity:read_all` and `profile:read_all` scopes.
- [ ] After the callback, the athlete id, access token, refresh token and expiry are saved in SQLite.
- [ ] Only one Athlete can be connected. A connection from a different Strava athlete is refused.
- [ ] The app shell shows a connection state: "Connect Strava" button when there's no connection, and the Athlete's name when connected.
- [ ] The test harness exists: the app runs in an in-memory test host with a real SQLite database, a fake Strava HTTP handler serving recorded JSON fixtures, and a fake `TimeProvider`.
- [ ] Tests cover: the connection state before and after the callback, and that a second, different athlete is refused.

---

# 02: Import Runs and list them

**What to build:** After connecting, the app imports every Activity summary from Strava, keeps only Runs, and the Athlete can see them on a basic Runs page. A sync runs when the backend starts and when the Athlete presses "Sync now".

**Blocked by:** 01

**Status:** ready-for-agent

- [ ] The sync pages through all of the Athlete's Activities.
- [ ] Only Runs (sport types Run, TrailRun, VirtualRun) are stored. Other Activities are dropped.
- [ ] Each Run stores its Strava id, name, sport type, start time in UTC and local time, distance, Moving Time, elapsed time, elevation gain, Relative Effort, Shoe reference and the raw summary JSON.
- [ ] Running the sync again doesn't create duplicates.
- [ ] A sync starts when the backend starts, and a "Sync now" button in the UI starts one too.
- [ ] Only one sync runs at a time. Starting a sync while one is running returns the running sync's status.
- [ ] The Runs page lists Runs newest first with date, name, distance (km), Moving Time and Pace (min:ss/km).
- [ ] The API uses metres and seconds. The frontend formats km, min:ss/km and h:mm:ss.
- [ ] Tests cover: filtering out non-Runs, multiple pages of Activities, no duplicates on a second sync, and a second sync started while one is running.

---

# 03: Resumable import that respects rate limits, with a progress indicator

**What to build:** Importing about 3 years of history never gets the Athlete's Strava access blocked and never starts over. The Athlete can always see how far the import has got and whether it's paused.

**Blocked by:** 02

**Status:** ready-for-agent

- [ ] The Strava client reads Strava's rate-limit headers (overall and read limits, per 15 minutes and per day) and waits before reaching them, instead of sending requests that would fail.
- [ ] A 429 response pauses the sync until the next rate-limit window, and it then resumes on its own.
- [ ] Import progress is saved. After a backend restart, the import continues where it stopped.
- [ ] A sync status endpoint reports the phase, progress counts, paused-until time, last successful sync time and last error.
- [ ] A sync status indicator in the app shell shows that information. It polls while a sync is running and stops polling when it's idle.
- [ ] Runs show up on the dashboard while the import is still running.
- [ ] Tests cover: a 429 pause followed by a resume, and an interrupted import that resumes without fetching again what it already has.

---

# 04: Token refresh and reconnecting

**What to build:** The Athlete connects once and never has to think about tokens again. If Strava access is revoked, the app says so clearly and lets the Athlete reconnect.

**Blocked by:** 02

**Status:** ready-for-agent

- [ ] An access token that has expired or is about to expire is refreshed before the request, and the new tokens are saved.
- [ ] A 401 from Strava, or a failed refresh, sets the sync status to "reconnect required".
- [ ] The indicator then shows a "Reconnect Strava" action. Reconnecting with the same athlete restores syncing without losing any data.
- [ ] Tests cover: refreshing an expired token, a revoked token leading to "reconnect required", and syncing again after reconnecting.

---

# 05: Run details: Best Efforts, splits and the detail view

**What to build:** Every Run gets its full detail from Strava, and the Athlete can open a Run to see its summary, Best Efforts and per-km splits.

**Blocked by:** 03

**Status:** ready-for-agent

- [ ] After the summaries, the import fetches the detailed Activity for each Run without detail, following the rate-limit handling from ticket 03.
- [ ] Every Best Effort is stored (Strava's distance name, distance in metres, elapsed time), along with the metric splits and the raw detail JSON.
- [ ] Import progress shows how many Runs have their detail loaded (for example "312 of 540 Runs detailed").
- [ ] The Run detail page shows distance, Moving Time, elapsed time, Pace, elevation gain, Relative Effort, the list of Best Efforts, a per-km splits table with Pace, and a link to the Run on Strava.
- [ ] Runs on the Runs page link to their detail page.
- [ ] Tests cover: storing Best Efforts and splits from a recorded detailed Activity, and resuming detail fetching after an interruption.

---

# 06: Incremental sync and rechecking recent Activities

**What to build:** Opening the app or pressing "Sync now" brings in new Runs quickly. Recent edits on Strava are reflected in the app: renaming a Run, changing it to another sport, or deleting it (see ADR 0002).

**Blocked by:** 05

**Status:** ready-for-agent

- [ ] Once the first import is done, a sync fetches only Activities that started after the latest stored Run, then fetches the detail for the new Runs.
- [ ] Every sync fetches the 20 most recent Activities in one request and compares them with the stored data.
- [ ] A changed Run in that window (name, sport type, distance, Shoe and so on) is updated.
- [ ] A Run whose sport type is no longer a running type is removed, along with its Best Efforts and splits.
- [ ] A stored Run that falls inside the window's time range but is missing from the response is removed.
- [ ] Runs older than the window are never touched.
- [ ] Tests cover: a new Run, a rename, a change to Walk, a deletion, and an old Run outside the window staying unchanged.

---

# 07: Runs list: search, filters, sorting and paging

**What to build:** The Athlete can quickly find any Run in their history.

**Blocked by:** 02

**Status:** ready-for-agent

- [ ] Search by Run name (case-insensitive substring).
- [ ] Filter by date range and by running sport type.
- [ ] Sort by date, distance, Moving Time and Pace, in both directions.
- [ ] The list is paged on the server.
- [ ] Search, filters, sort order and page are kept in the URL, so the browser back button works.
- [ ] Tests cover: each filter, each sort key, combined filters, and the edges of paging.

---

# 08: Overview page

**What to build:** The Athlete sees their whole running history at a glance: totals, how volume developed week by week, and how Pace developed month by month.

**Blocked by:** 02

**Status:** ready-for-agent

- [ ] All-time totals: number of Runs, distance, Moving Time and elevation gain.
- [ ] The same totals for each calendar year.
- [ ] A chart of distance per Training Week across the full history, with weeks that have no Runs included as zero.
- [ ] A chart of average Pace per month, calculated as total Moving Time ÷ total distance.
- [ ] This ticket introduces the Training Week rule in the domain: Monday to Sunday, using each Run's local start date. Later tickets reuse it.
- [ ] Charts use ECharts.
- [ ] Tests cover: a Run at 23:30 local time on a Sunday counting in that week, a Run abroad going by its local time, the weighting of monthly Pace, and a week with no Runs.

---

# 09: Shoes

**What to build:** The Athlete sees all their Shoes and how much each has been used, and can tell which Shoe each Run was in.

**Blocked by:** 02

**Status:** ready-for-agent

- [ ] Every sync refreshes Shoes from the authenticated athlete: Strava gear id, name, distance as Strava reports it, retired flag and raw JSON.
- [ ] The Shoes page lists each Shoe with its name, Strava distance, number of Runs and date of the last Run.
- [ ] Retired Shoes are shown greyed out, after the active ones.
- [ ] The Runs list and Run detail page show the Shoe name.
- [ ] Tests cover: refreshing Shoes, a Shoe becoming retired, and the Run counts per Shoe.

---

# 10: Records page

**What to build:** The Athlete sees how each Personal Best improved over the years, counting Best Efforts inside longer Runs.

**Blocked by:** 05

**Status:** ready-for-agent

- [ ] For each Best Effort distance, the history is the Best Efforts in date order, keeping only those strictly faster than every earlier one.
- [ ] A step chart for each distance. 1k, 5k, 10k, half marathon and marathon are shown by default. The other distances Strava provides can be selected.
- [ ] A table of current Personal Bests with time, Pace, date and a link to the Run.
- [ ] The Run detail page marks Best Efforts that were a Personal Best when they were run.
- [ ] Records reflect Runs that were removed by the recent-window check.
- [ ] Tests cover: a Best Effort inside a longer Run setting a Personal Best, an equal time not adding a step, and a removed Run disappearing from the history.

---

# 11: Goal Races: form and list

**What to build:** The Athlete records the races they train for, including past ones such as the first marathon. This defines their Training Blocks.

**Blocked by:** 01

**Status:** ready-for-agent

- [ ] Create, edit and delete Goal Races with name, date, distance and an optional Target Time.
- [ ] The distance can be picked from presets (marathon 42,195 m, half marathon 21,097.5 m, 10k, 5k) or entered as a custom value.
- [ ] A Goal Race without a Training Plan has a Training Block length (any positive number of weeks). The block ends with the Race Week.
- [ ] A Goal Race whose Training Block would overlap another one's is rejected with a clear validation message (problem details shown in the form).
- [ ] A Goal Races list shows each race's date, distance, Target Time and Training Block dates.
- [ ] Tests cover: entering the first marathon (27 Sep 2026, 14 weeks) gives a block from Mon 22 Jun to Sun 27 Sep 2026, overlapping blocks are rejected, and editing a date moves the block.

---

# 12: Race Result

**What to build:** The Athlete sees how each Goal Race went, without typing in a time, compared with the Target Time (see ADR 0003).

**Blocked by:** 05, 11

**Status:** ready-for-agent

- [ ] The Race Result comes from the longest Run on the race date. It is that Run's Best Effort whose distance exactly equals the Goal Race distance.
- [ ] If the distance has no matching Best Effort, the Run's elapsed time is used.
- [ ] With no Run on the race date, there's no Race Result.
- [ ] The Goal Races list shows the Race Result, a link to the Run, and the difference from the Target Time (over or under) when there is one.
- [ ] Tests cover: a marathon Run measured at 42.58 km giving the Marathon Best Effort, several Runs on race day (a warm-up plus the race), no Run on race day, and a custom distance falling back to elapsed time.

---

# 13: Training page: block view with actual values

**What to build:** During a Training Block, the Athlete sees week by week what they actually ran, and how their Long Run Pace compares with the Pace they need for their Target Time. They can also look at any past block.

**Blocked by:** 08, 11

**Status:** ready-for-agent

- [ ] When today falls inside a Goal Race's Training Block, the Training page shows that block.
- [ ] Each Training Week is labelled with its Weeks to Race (−(length−1) … 0) and its calendar dates.
- [ ] Each week shows actual distance, Moving Time, total Relative Effort, and the Long Run (longest Run) with its distance and Pace.
- [ ] When the Goal Race has a Target Time, the Target Pace (Target Time ÷ distance) is shown, and each week's Long Run Pace is compared with it.
- [ ] A Goal Race selector opens any past or upcoming Training Block in the same view.
- [ ] Future weeks show as empty for actual values.
- [ ] Tests cover: Weeks to Race on fixed dates (for example 10 Mar 2027 is in week −11 of the second marathon block), the Long Run choice, the Target Pace for 5:00:00 over a marathon (≈ 7:06/km), and opening the 14-week first marathon block.

---

# 14: Training page: countdown and empty state

**What to build:** Between Training Blocks, the Training page is still useful. It shows how long until the next block starts and what the Athlete's recent volume looks like.

**Blocked by:** 13

**Status:** ready-for-agent

- [ ] When today isn't inside any Training Block but a future Goal Race exists, the page shows the next block's start date and the days remaining, plus weekly distance for the recent Training Weeks.
- [ ] With no upcoming Goal Race, the page shows an empty state with a link to create one.
- [ ] On the first day of a block, the page switches to the block view.
- [ ] Tests cover: 9 Oct 2026 (countdown to Mon 1 Feb 2027), 31 Jan 2027 (countdown), 1 Feb 2027 (block view), and no upcoming Goal Race.

---

# 15: Comparing Training Blocks

**What to build:** The Athlete compares their current block with a previous one, week by week, lined up by how far each week is from its race.

**Blocked by:** 13

**Status:** ready-for-agent

- [ ] The block view can show a comparison block, lined up by Weeks to Race.
- [ ] The comparison defaults to the most recent Goal Race before the one being viewed. The Athlete can pick another one or turn the comparison off.
- [ ] Weeks that exist in only one of the two blocks show as empty on the other side.
- [ ] Each week shows the same values as in ticket 13 for both blocks: distance, Moving Time, Relative Effort, and Long Run distance and Pace.
- [ ] Tests cover: comparing a 16-week block with a 14-week one (weeks −15 and −14 have nothing to compare against), and the default comparison choice.

---

# 16: Training Plan: length and Phases

**What to build:** The Athlete creates a Training Plan for a Goal Race by choosing its length and the type of each 4-week Phase.

**Blocked by:** 11

**Status:** ready-for-agent

- [ ] A Goal Race can have at most one Training Plan. A plan's length is 12, 16 or 20 weeks.
- [ ] The plan is split into 4-week Phases automatically. The Athlete chooses each Phase's type (Build, Peak or Taper).
- [ ] The last Phase must be a Taper, and this is validated.
- [ ] Plan weeks are keyed by Weeks to Race, so changing the Goal Race date moves the plan.
- [ ] When a plan exists, its length becomes the Training Block length. The Goal Race's own block length field is only used without a plan.
- [ ] Changing a plan's length keeps the weeks that are still within range, drops the ones that aren't, and adds empty weeks.
- [ ] The block overlap check (ticket 11) uses the plan's length.
- [ ] Tests cover: a valid 16-week Build–Peak–Peak–Taper plan, rejecting a length that isn't 12, 16 or 20, rejecting a last Phase that isn't a Taper, moving the race date, and changing the length.

---

# 17: Training Plan: weekly targets, Running Days and Recovery Weeks

**What to build:** The Athlete fills in their plan week by week in a table: planned distance, planned Long Run, Running Days, and Recovery Weeks.

**Blocked by:** 16

**Status:** ready-for-agent

- [ ] A plan editor table with one row per Training Week, labelled with Weeks to Race, calendar dates and Phase.
- [ ] Each row has a planned distance, a planned Long Run distance, Running Days (Monday to Sunday toggles) and a Recovery Week checkbox.
- [ ] "Copy from previous week" copies the planned distance, planned Long Run and Running Days from the row above.
- [ ] The Race Week (Weeks to Race 0) can't be marked as a Recovery Week, and this is validated.
- [ ] The plan can be edited at any time, with no history of changes kept.
- [ ] Frontend component tests cover the editor's "copy from previous week" and Running Day toggles.
- [ ] Backend tests cover: saving and reading back weekly rows, and rejecting a Recovery Week on the Race Week.

---

# 18: Training page: planned vs. actual

**What to build:** During a planned block, the Athlete sees how each week's actual running compares with their plan, including whether they ran on their planned days.

**Blocked by:** 13, 17

**Status:** ready-for-agent

- [ ] The block view shows Phase bands and marks Recovery Weeks.
- [ ] Each week shows planned vs. actual distance and planned vs. actual Long Run.
- [ ] Each week has a calendar strip with three states: a planned Running Day with a Run, a planned Running Day without a Run, and a Run on an unplanned day.
- [ ] Future weeks show only the plan.
- [ ] Blocks without a Training Plan still show actual values only (ticket 13 behaviour).
- [ ] Tests cover: a week where the plan was met, a missed Running Day, a Run on an unplanned day, a future week, and a block without a plan.

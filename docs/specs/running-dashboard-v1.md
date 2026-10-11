# Running Dashboard v1

## Problem Statement

The Athlete has been running for almost 3 years. A Garmin watch records every Run and uploads it to Strava. They finished their first marathon on 27 Sep 2026 and are training for their second on 23 May 2027, with a Target Time of under 5:00:00.

Strava and Garmin Connect show single Runs and short time ranges well. They don't answer the questions the Athlete actually has:

- What does the whole 3-year history look like? Totals per year, weekly volume over time, and how Pace has developed.
- How have Personal Bests improved over the years, counting Best Efforts inside longer Runs and not only Runs of exactly race distance?
- During a Training Block, is the Athlete hitting their own Training Plan (Weekly Targets, Long Runs, Running Days)? How does this block compare week by week with the block before the previous Goal Race?
- Are their Long Runs getting close to the Pace they need for the Target Time?

None of these are available in one place, and the Athlete's own Training Plan (Phases, Recovery Weeks, Weekly Targets) isn't stored anywhere it can be compared with real Runs.

## Solution

A local web app (Angular frontend, .NET backend) that runs on the Athlete's PC. It connects once to the Athlete's Strava account, imports every Run from the full history, and keeps them up to date. It has five pages:

- **Overview:** all-time and per-year totals, weekly distance and monthly average Pace, each for a chosen year
- **Training:** the current Training Block, with planned vs. actual for each Training Week, Long Run Pace against the Pace needed for the Target Time, and a comparison with the previous Training Block lined up by Weeks to Race. Outside a Training Block, it shows a countdown to the next one and recent weekly volume. The Goal Races are managed here too.
- **Runs:** a searchable, sortable list of Runs with a detail view (summary, Pace per km, route map with a replay, Best Efforts and Splits)
- **Records:** how each Personal Best developed over time, as a step chart for each standard distance
- **Shoes:** each Shoe with its total distance from Strava and how far it is from the 700 km Maximum Distance, with retired Shoes greyed out

The Athlete enters Goal Races and Training Plans (Phases, Weekly Targets, Running Days, Recovery Weeks) in forms in the UI. A small sync status indicator shows import progress, the time of the last sync, and any problem with the Strava connection.

## User Stories

### Connecting to Strava

1. As the Athlete, I want to connect my Strava account the first time I open the app, so that the dashboard can read my Runs.
2. As the Athlete, I want to connect only once and have the app refresh access by itself after that, so that I never have to log in again during normal use.
3. As the Athlete, I want the app to tell me clearly when the Strava connection has been revoked or has expired, and offer a "Reconnect Strava" action, so that a broken connection doesn't quietly leave my data out of date.
4. As the Athlete, I want my Strava app credentials kept out of the repository, so that I can push the code without leaking secrets.
5. As the Athlete, I want the app to belong to my Strava account only, so that no other account's data can be mixed in.

### Importing and syncing

6. As the Athlete, I want the app to import my full Strava history automatically after I connect, so that I don't have to trigger anything by hand.
7. As the Athlete, I want the import to keep only Runs (road, trail and treadmill) and ignore other Activities, so that rides, walks and strength workouts don't count toward my running totals.
8. As the Athlete, I want Runs to appear on the dashboard while the import is still running, so that I can start using the app before the full history has loaded.
9. As the Athlete, I want the import to carry on where it stopped after I close the app or hit Strava's rate limits, so that a long import is never lost and never starts over.
10. As the Athlete, I want the import to stay within Strava's rate limits by itself, pausing and resuming as needed, so that my API access is never blocked.
11. As the Athlete, I want the full detail of each Run imported (Best Efforts and per-km splits), so that Records and the Run detail view are complete.
12. As the Athlete, I want new Runs fetched automatically when the app starts, so that the dashboard is current whenever I open it.
13. As the Athlete, I want a "Sync now" button, so that I can pull in a Run I just uploaded without restarting the app.
14. As the Athlete, I want each sync to recheck my 20 most recent Activities against Strava, so that a recent Run I renamed, changed to another sport, or deleted is reflected in the app.
15. As the Athlete, I want a Run removed from the app when I change it to a non-running sport on Strava, so that it stops counting toward my totals.
16. As the Athlete, I want my Shoes refreshed on every sync, so that Shoe distances and retired flags match Strava.
17. As the Athlete, I want only one sync to run at a time, even if I press "Sync now" during the import, so that data is never fetched twice or corrupted.
18. As the Athlete, I want a sync status indicator that shows import progress (for example "312 of 540 Runs detailed"), so that I know how complete my data is.
19. As the Athlete, I want the indicator to show when the last successful sync finished, so that I know how fresh my data is.
20. As the Athlete, I want the indicator to show when syncing is paused for rate limits and when it will continue, so that a pause doesn't look like a failure.
21. As the Athlete, I want sync errors shown in the indicator, so that I notice when something went wrong.

### Overview

22. As the Athlete, I want all-time totals (number of Runs, distance, Moving Time, elevation gain), so that I can see the size of my whole running history.
23. As the Athlete, I want the same totals for each calendar year, so that I can compare my years of running.
24. As the Athlete, I want a chart of distance per Training Week for a year I pick (the newest by default), so that I can see how my volume has built up, fallen off and peaked over the 3 years.
25. As the Athlete, I want a chart of average Pace per month for a year I pick (the newest by default), calculated as total Moving Time divided by total distance, so that I can see how my Pace has developed, with longer Runs counting for more.
26. As the Athlete, I want Pace shown as min:ss/km and distances in kilometres, so that the numbers match how I think about running.
27. As the Athlete, I want Training Weeks to run from Monday to Sunday, and each Run to belong to the week of its local start time, so that a late Sunday Run counts toward that Sunday's week even when I'm travelling.

### Records

28. As the Athlete, I want a step chart for each standard distance showing every time my Personal Best improved, so that I can see how my records developed over 3 years.
29. As the Athlete, I want 5k, half marathon and marathon shown by default, so that the most relevant distances come first.
30. As the Athlete, I want to show any other distance Strava provides (400 m, ½ mile, 1 mile, 2 miles, 15k, 10 miles, 20k, 30k, 50k), so that nothing is hidden.
31. As the Athlete, I want a table of my current Personal Best for each distance, with its date and the Run it came from, so that I can look records up quickly.
32. As the Athlete, I want to open the Run a Personal Best came from, so that I can see the context of that effort.
33. As the Athlete, I want Best Efforts inside longer Runs to count (for example a fast 5k within a 14k), so that my Personal Bests reflect what I actually ran.
34. As the Athlete, I want a record to count as new only when it is strictly faster than the previous one, so that an equal time doesn't add a step.
35. As the Athlete, I want Records to update when a Run is deleted or stops being a Run on Strava (within the recent window), so that a watch glitch I fixed on Strava doesn't leave a fake Personal Best.

### Goal Races

36. As the Athlete, I want to create a Goal Race with a name, date, distance and optional Target Time in a form, so that the app knows what I'm training for.
37. As the Athlete, I want to choose common distances (marathon, half marathon, 10k, 5k) or enter a custom one, so that entering a race is quick but flexible.
38. As the Athlete, I want to enter my first marathon (27 Sep 2026, 14-week Training Block, no Target Time) as a past Goal Race, so that my current block can be compared with it.
39. As the Athlete, I want to set the Training Block length directly on a Goal Race without a Training Plan, to any number of weeks, so that past blocks like my 14-week one are stored correctly.
40. As the Athlete, I want to edit and delete Goal Races, so that I can fix mistakes and handle date changes.
41. As the Athlete, I want the app to reject a Goal Race whose Training Block would overlap another Goal Race's block, so that "the current Training Block" is never ambiguous.
41a. As the Athlete, I want the app to reject a second upcoming Goal Race until race day of the upcoming one is over, so that I only ever train for one Goal Race with one active Training Plan. The API still accepts past ones; the page offers the form to add a race only while none is upcoming.
42. As the Athlete, I want my Goal Races on the Training page, with the upcoming race's details and actions on its block or countdown and a form to add or edit a race below, so that planning and training are in one place.
43. As the Athlete, I want the Race Result calculated automatically from the longest Run on race day, using the Best Effort over the race distance, so that it comes close to my official finish time without typing it in.
44. As the Athlete, I want the Race Result to use the Run's elapsed time when the race distance has no Strava Best Effort, so that unusual distances still get a result.
45. As the Athlete, I want no Race Result shown when there's no Run on race day, so that a future or missed race doesn't show a wrong number.
46. As the Athlete, I want the Race Result shown next to the Target Time, with how far over or under it was, so that I know straight away whether I hit my goal.

### Training Plan

47. As the Athlete, I want to create a Training Plan for a Goal Race by choosing a length of 12, 16 or 20 weeks, so that the plan matches how I structure my training.
48. As the Athlete, I want the plan split into 4-week Phases automatically, so that I don't have to set Phase boundaries by hand.
49. As the Athlete, I want to choose the type of each Phase (Build, Peak or Taper), so that I can lay out blocks like Build–Peak–Peak–Taper or Build–Build–Peak–Peak–Taper.
50. As the Athlete, I want the last Phase to always be a Taper, so that the Race Week is always at the end of a Taper.
51. As the Athlete, I want to enter a Weekly Target for each Training Week (planned distance and planned Long Run distance), so that I can compare planned with actual.
52. As the Athlete, I want to choose the Running Days for each Training Week, so that I can plan fewer days in Taper and Race Week.
53. As the Athlete, I want a "copy from previous week" action for each week, so that entering 16 weeks of mostly identical Running Days and similar targets is quick.
54. As the Athlete, I want to mark any week as a Recovery Week myself, so that the plan shows my 3:1 pattern without the app assuming one.
55. As the Athlete, I want the app to refuse to mark the Race Week as a Recovery Week, so that the Race Week stays its own thing.
56. As the Athlete, I want plan weeks tied to Weeks to Race and not to calendar dates, so that moving the Goal Race date moves the whole plan with it.
57. As the Athlete, I want the Training Block length to come from the Training Plan when there is one, so that the two never disagree.
58. As the Athlete, I want to edit my Training Plan at any time during the block, so that I can adjust it when life gets in the way.
59. As the Athlete, I want to be able to enter a Training Plan before the block starts, so that my plan for 1 Feb 2027 is ready in advance.

### Training page

60. As the Athlete, I want the Training page to show the current Training Block whenever today falls inside one, so that I see my progress without looking for it.
61. As the Athlete, I want each Training Week of the block labelled with its Weeks to Race (−15 … 0) and its calendar dates, so that I know where I am in the block.
62. As the Athlete, I want Phases shown as coloured bands (Build blue, Peak red, Taper green, the same in the Training Plan editor) and Recovery Weeks marked, so that I can see the structure of the plan.
63. As the Athlete, I want the actual distance for each week next to the planned distance, so that I can see whether I hit my Weekly Target.
64. As the Athlete, I want the actual Long Run distance for each week next to the planned Long Run, so that I can follow my Long Run progression.
65. As the Athlete, I want each week's Long Run Pace compared with the Pace needed for my Target Time (7:06/km for 5:00:00 over a marathon), so that I can judge whether my goal is realistic.
66. As the Athlete, I want the weekly total of Relative Effort for each week, so that I can see training load and not only distance.
67. As the Athlete, I want the weekly Moving Time for each week, so that I can see time on my feet.
68. As the Athlete, I want a calendar strip for each week showing planned Running Days with a Run, planned Running Days without a Run, and Runs on unplanned days, so that I can see how well I stuck to the plan.
69. As the Athlete, I want future weeks of the block to show only the plan, so that I can see what's coming.
70. As the Athlete, I want the current block compared with a previous Goal Race's block, lined up by Weeks to Race, so that week −5 now sits next to week −5 last time.
71. As the Athlete, I want the comparison to be with the most recent previous Goal Race, so that the comparison is there without extra clicks.
72. As the Athlete, I want weeks with nothing to compare against (for example weeks −15 and −14 against a 14-week block) shown as empty, so that blocks of different lengths still line up.
73. As the Athlete, I want the previous Goal Race's block shown next to the current one, so that I can review how my first marathon block went.
74. As the Athlete, I want a countdown to the start of the next Training Block when today is outside a block, so that I know how long until training starts.
74a. As the Athlete, I want to open the upcoming Training Block and its Training Plan from the countdown, so that I can see the plan before the block starts.
75. As the Athlete, I want my weekly volume of the last 5 Training Weeks shown under the countdown, so that I can keep an eye on my base before the block.
76. As the Athlete, I want a clear empty state when there is no upcoming Goal Race, so that the page still makes sense with nothing planned.
77. As the Athlete, I want a Training Block without a Training Plan to show actual values only, so that past blocks without a plan are still useful.

### Shoes

78. As the Athlete, I want a list of my Shoes with name and total distance as Strava reports it, so that I can track wear.
79. As the Athlete, I want retired Shoes shown greyed out, so that my current Shoes stand out.
80. As the Athlete, I want to see the number of Runs and the date of the last Run for each Shoe, so that I know which pairs I actually use.
80a. As the Athlete, I want each active Shoe to show how far it is from the Maximum Distance of 700 km, turning amber in the last 10 % and red once past it, so that I know when to replace it.

### Runs

81. As the Athlete, I want a list of all my Runs with date, name, distance, Moving Time, Pace and Shoe, so that I can browse my history.
82. As the Athlete, I want to search Runs by name, so that I can find a specific Run such as a named race.
83. As the Athlete, I want to filter Runs by date range and running sport type, so that I can narrow the list down.
84. As the Athlete, I want to sort Runs by date, distance, Moving Time and Pace, so that I can find my longest or fastest Runs.
85. As the Athlete, I want the list paged, so that it stays fast with hundreds of Runs.
86. As the Athlete, I want a detail view for each Run with its summary (distance, Moving Time, elapsed time, Pace, elevation gain, Relative Effort, Shoe), so that I can check one Run in depth.
87. As the Athlete, I want the detail view to list the Run's Best Efforts and mark any that were a Personal Best at the time, so that I can see which records a Run set.
88. As the Athlete, I want the detail view to show per-km splits with Pace, so that I can see how I paced the Run.
88a. As the Athlete, I want a chart of the Pace of each Split, faster towards the top and with the Run's own Pace as a line, so that I can see at a glance how my Pace changed during the Run.
89. As the Athlete, I want a link from the detail view to the Run on Strava, so that I can jump to the original.
89a. As the Athlete, I want the detail view to show the Run's route on a map with its start and finish, drawn from the route Strava already gives with each Activity, in a street, satellite or topographic view that needs no API key, so that I can see where I ran.
89b. As the Athlete, I want to replay the Run along its route, with a dot moving at the Pace of each Split and play, speed and position controls, so that I can relive how I ran it.

## Implementation Decisions

### Architecture

- One repository with a .NET 10 backend (ASP.NET Core minimal APIs) and an Angular frontend (latest version, standalone components, signals). Charts use ECharts through its Angular wrapper.
- The app runs locally on the Athlete's PC. There is no hosting, no user authentication and no multi-tenancy. The backend serves a JSON API and the frontend calls it.
- Storage is SQLite through EF Core, with migrations. Each stored Strava record keeps its raw JSON next to the structured columns, so new metrics can be derived later without fetching again from Strava (which is slow because of the rate limits). If the app is ever deployed, switching to Postgres is meant to be an EF provider change.
- "Now" comes from an injected clock (.NET `TimeProvider`) everywhere, so date-dependent behaviour can be tested.
- The decisions in the ADRs apply: Strava is the only data source (0001), sync works by polling and rechecks a window of recent Activities (0002), and the Race Result comes from the Best Effort (0003).

### Modules

The modules are listed from the outside in. Each should keep a small interface and hide its complexity.

1. **Strava client.** Wraps the Strava v3 API: OAuth code exchange and token refresh, listing the Athlete's Activities (paged, optionally after a timestamp), getting a detailed Activity (Best Efforts, metric splits, gear id), and getting the authenticated athlete (which includes the Shoes). It keeps tokens fresh by itself, since access tokens expire after about 6 hours. It reads Strava's rate-limit headers (overall and read limits, per 15 minutes and per day) and reports how long to wait instead of making requests that would fail. Callers never deal with tokens or HTTP.
2. **Sync engine.** Owns everything that moves data from Strava into the store:
   - *Initial import*: first all Activity summaries, keeping only Runs, then the detail for each Run that has no detail yet. Progress is stored, so the import continues after a restart or a rate-limit pause.
   - *Incremental sync*: on backend start and on "Sync now". Fetches Activities that started after the latest stored Run, then the details for new Runs.
   - *Recent-window check* (ADR 0002): fetches the 20 most recent Activities in one request. It updates stored Runs that changed, removes Runs that are no longer a running sport type, and removes stored Runs that fall inside the window's time range but are missing from the response.
   - *Shoe refresh*: replaces Shoe data from the athlete response on every sync.
   - Only one sync can run at a time. Starting a sync while one is running returns the running one's status.
   - It reports a status: phase, progress counts, paused-until time, last successful sync time, and the last error, including "reconnect required".
3. **Store.** EF Core model holding:
   - The Strava connection (athlete id, tokens, expiry)
   - Runs: Strava id, name, sport type, start time in UTC and local, distance, Moving Time, elapsed time, elevation gain, Relative Effort, Shoe reference, raw summary JSON, raw detail JSON, whether detail has been fetched
   - Best Efforts: Run, Strava's distance name, distance in metres, elapsed time
   - Splits: Run, index, distance, Moving Time, elapsed time, elevation difference
   - Shoes: Strava gear id, name, distance from Strava, retired flag, raw JSON
   - Goal Races: name, date, distance in metres, optional Target Time, Training Block length in weeks (used only when there's no Training Plan)
   - Training Plans: Goal Race, length 12, 16 or 20
   - Phases: plan, position, type Build/Peak/Taper
   - Plan weeks: plan, Weeks to Race, planned distance, planned Long Run distance, set of Running Days (weekdays), Recovery Week flag
   - Sync state and progress

   Only Runs are stored. Other Activities are dropped when they are fetched.
4. **Training calendar (domain module).** Pure calculations, with no I/O:
   - Which Training Week (Monday to Sunday, by local start date) a Run belongs to
   - Weeks to Race for a week and a Goal Race
   - The Training Block of a Goal Race: its length comes from the Training Plan if there is one, otherwise from the Goal Race. It ends with the Race Week.
   - Which Goal Race's block contains a given date, if any
   - The next Training Block that starts after a given date
   - Long Run per week (the longest Run)
   - Planned vs. actual for each week, including the Running Days calendar strip: planned with a Run, planned without a Run, Run on an unplanned day
   - Target Pace: Target Time divided by Goal Race distance
   - Personal Best progression: for each distance, Best Efforts in date order, keeping each one that is strictly faster than all earlier ones
   - Race Result (ADR 0003): from the longest Run on race day, the Best Effort whose distance equals the Goal Race distance, otherwise that Run's elapsed time, otherwise none
   - Monthly average Pace (sum of Moving Time ÷ sum of distance)
5. **Planning rules (part of the domain).** Validates Goal Races and Training Plans:
   - Training Blocks must not overlap
   - Only one Goal Race can be upcoming (race day today or later) at a time; past Goal Races are not limited. As a Goal Race has at most one Training Plan, there is only ever one active Training Plan
   - A plan's length must be 12, 16 or 20, made of 4-week Phases, with the last Phase a Taper
   - Plan weeks cover Weeks to Race −(length−1) to 0
   - The Race Week (Weeks to Race 0) can't be a Recovery Week
   - When a plan's length changes, existing weeks keep their Weeks to Race, weeks outside the new range are dropped, and new weeks start empty

   A Goal Race without a plan accepts any positive block length.
6. **HTTP API.** Endpoints shaped around the pages, so the frontend does no domain calculations:
   - Strava connect (redirect to Strava) and the OAuth callback
   - Sync: status, and start a sync
   - Overview: all-time totals, per-year totals, weekly distance series, monthly Pace series
   - Records: for each distance, the progression points and the current Personal Best, with Run references
   - Training: either the block view (for the current or a chosen Goal Race, with weeks, Phases, plan, actuals, calendar strips, Target Pace and an optional comparison block lined up by Weeks to Race) or the countdown view (next block start, days remaining, recent weekly volume)
   - Goal Races: list with Race Result, create, update, delete
   - Training Plan: get, create or replace, update weeks
   - Shoes: list
   - Runs: paged list with search, filters and sort, and a detail view
   
   The API uses metres and seconds. The frontend formats km, min:ss/km and h:mm:ss. Validation failures return problem details with messages the UI can show.
7. **Frontend.** One Angular route per page (Overview, Records, Training, Shoes, Runs and Run detail), with the Goal Race form on the Training page, plus the Training Plan editor (a table with one row per week, Phase type pickers, Running Day toggles, Recovery Week checkbox and "copy from previous week"). The sync status indicator lives in the app shell and polls the sync status endpoint while a sync is running.

### Strava specifics

- OAuth scopes: `read`, `activity:read_all` (includes private Runs) and `profile:read_all` (includes Shoes on the athlete). The callback is on localhost. The client id and secret come from .NET user secrets.
- The running sport types are Run, TrailRun and VirtualRun.
- Best Effort distances are matched by Strava's distance name or exact distance in metres. A Goal Race distance counts as matching a Best Effort only when it is equal to within 1 m (for example 42,195 m for a marathon; Strava's half marathon Best Effort is 21,097 m, the official distance 21,097.5 m).
- New Strava API apps are limited to one connected athlete until Strava reviews them, which fits a single-Athlete app.

## Testing Decisions

- **One main seam: the backend HTTP API, tested in memory.** Tests start the real application (ASP.NET Core in-memory test host) with a real SQLite database. Only two things are replaced:
  - **Strava**, faked at the HTTP-handler level with recorded JSON from real Strava responses (activity list pages, detailed activities with Best Efforts and splits, athlete with Shoes, token responses, 401 and 429 responses with rate-limit headers)
  - **The clock**, a fake `TimeProvider` set to fixed dates such as 10 Mar 2027, 1 Feb 2027 or 23 May 2027

  Tests set up a Strava state and a date, trigger a sync and/or call page endpoints, then assert on the JSON responses.
- **What makes a good test here:** it describes behaviour the Athlete would recognise and checks only what the API returns. It never checks internal classes, EF queries or how many calls went to Strava, except where request counts *are* the behaviour, such as staying within rate limits. Restructuring the internals shouldn't break any test.
- **Domain logic is developed test-first through the main seam.** Scenarios that must be covered:
  - Week boundaries: a Run at 23:30 local on a Sunday, and a Run abroad
  - Weeks to Race and block lengths of 14 and 16 weeks
  - Comparison between blocks of different lengths
  - Countdown before 1 Feb 2027, and the first day of the block
  - Long Run, and the planned vs. actual calendar strip
  - Personal Best progression, including ties and Best Efforts inside longer Runs
  - Race Result: the 27 Sep 2026 marathon (42.58 km Run, Marathon Best Effort), no Run on race day, several Runs on race day, a distance with no Best Effort
  - Planning validation: overlapping blocks, a last Phase that isn't a Taper, Race Week marked as a Recovery Week, a length that isn't 12, 16 or 20
  - Changing the Goal Race date moves the plan
  - Sync: resuming an interrupted import, a 429 pause and resume, the recent-window check (rename, change to Walk, deletion), token refresh and revoked tokens, and a second sync started while one is running
- **A second seam (the training calendar module tested on its own) is allowed** only if some scenarios have too many combinations to set up through the API. Leave it out at first.
- **The frontend gets light tests:** component tests for the Training Plan editor and for the formatting helpers (Pace, durations), using Angular's default test runner. No end-to-end browser tests in v1.
- **Prior art:** none yet. The repository has no code. The first tests written through the main seam set the pattern, including the fake Strava handler and the recorded-JSON fixtures.

## Out of Scope

- Garmin data of any kind (HRV, sleep, Training Readiness, VO2max, Body Battery). See ADR 0001.
- Deployment, hosting, user authentication, and viewing on a phone
- Strava webhooks (only possible when hosted), and noticing edits or deletions older than the 20 most recent Activities
- Per-second streams: heart rate zones and heatmaps
- A fitness/fatigue model (CTL/ATL/TSB) and predicted race times (for example the Riegel formula)
- Daily planned workouts, matching Runs to planned workouts, and generating plans from Phase rules
- A list of all Goal Races with their Race Results, and editing or deleting past Goal Races, in the UI (the API still provides both)
- Opening a past Training Block on its own, or choosing which block to compare with, in the UI (the API still provides both)
- A 3D flyover of a Run, and replaying it second by second (that needs the per-second streams)
- History of changes to a Training Plan
- A Maximum Distance per Shoe or one the Athlete can change (it is fixed at 700 km), and retiring Shoes from the dashboard
- Handling races differently from other Runs, and tune-up races as Goal Races
- More than one Athlete, and showing data to anyone other than the Athlete
- Activities other than Runs

## Further Notes

- **Deadline:** the second marathon's 16-week Training Block starts on Monday 1 Feb 2027 (Race Week ends on Sunday 23 May 2027). The Training Plan editor and the Training page must work before then.
- **Goal Races the Athlete will enter at launch:** the first marathon (27 Sep 2026, 14-week block from 22 Jun 2026, no Target Time; Strava Run "Finisher.", 42.58 km), and the second marathon (23 May 2027, 16-week plan Build–Peak–Peak–Taper, Target Time under 5:00:00, so a Target Pace of about 7:06/km).
- **Glossary:** CONTEXT.md defines every domain term used here. The UI and code should use the same terms (Run, Training Week, Weeks to Race, Weekly Target, and so on) and avoid the alternatives it lists.
- **First import size:** the full history is a few hundred Runs, so loading all details takes a few hours because of Strava's read limit (about 100 requests per 15 minutes and 1,000 per day). The dashboard should be usable from the first page of summaries onward.

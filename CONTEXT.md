# Running Dashboard

A personal dashboard for one runner. It shows their long-term running history and their progress toward an upcoming goal race, using data from Strava.

## Language

### Runs

**Athlete**:
The single person whose running data the dashboard shows.
_Avoid_: User, runner, account

**Run**:
A Strava activity with a running sport type (road, trail or treadmill). Races are Runs and are not treated differently.
_Avoid_: Activity (when only runs are meant), workout, session

**Activity**:
Any entry recorded on Strava, of any sport. The dashboard ignores Activities that are not Runs, Rides or Swims.

**Ride**:
A Strava activity with a cycling sport type: road, virtual, gravel or mountain bike (e-bike rides are not Rides). Rides are listed in the Ride history, with their Avg Speed, and each has a page of its own; they never count toward totals, Training Weeks, Streaks, Records or goals, and only show in Training Time and in their day on the Calendar beside the Runs.
_Avoid_: Cycle, bike ride, workout

**Virtual Ride**:
A Ride with Strava's virtual sport type, e.g. on Zwift. All other Rides are outdoor Rides, by their sport type alone, so a trainer session recorded as an ordinary Ride counts as outdoor. The Ride history can list only the one or the other.
_Avoid_: Indoor ride, trainer ride

**Swim**:
A Strava activity with the swimming sport type, in a pool or open water. Swims are listed in the Swim history, with their Swim Pace, and each has a page of its own; like Rides, they never count toward totals, Training Weeks, Streaks, Records or goals, and only show in Training Time and in their day on the Calendar beside the Runs.
_Avoid_: Swim workout, session

**Swim Pace**:
A Swim's Moving Time per 100 metres, written as min:ss /100m.
_Avoid_: Pace (on its own, for Swims), Avg Speed

**Listed Activity**:
A Ride or a Swim: an Activity the dashboard stores only to show it in its history, on its own page, in Training Time and in its day on the Calendar, never to count it.
_Avoid_: Other activity, cross-training

**Avg Speed**:
A Ride's distance per Moving Time, in km/h (mph when the Distance Unit is miles). Rides have it instead of a Pace.
_Avoid_: Pace (for Rides), velocity

**Moving Time**:
The time during a Run (or Ride or Swim) when the Athlete was actually moving, without pauses. It is used for Pace, Avg Speed, Swim Pace and for weekly time totals.
_Avoid_: Duration, time (on its own)

**Long Run**:
The longest Run of a Training Week.
_Avoid_: LSD, long one

**Pace**:
Moving Time per kilometre, written as min:ss/km, or per mile (min:ss/mi) when the Distance Unit is miles.
_Avoid_: Speed (except when talking about Strava's raw data)

**Distance Unit**:
Kilometres or miles, chosen by the Athlete in Settings: the unit for distances, Pace and Avg Speed wherever the dashboard shows them, and for distances the Athlete types in. Everything is still stored in metres. Swims and elevation stay in metres.
_Avoid_: Units (on its own), measurement system

**Theme**:
The dashboard's colours, picked by the Athlete in Settings: Blue (unless the Athlete picks another), Teal, Green, Orange, Red, Pink or Purple. It colours buttons, links, bars and charts, and tints the dark background, surfaces and text towards its accent; Phases, and met or missed targets, keep their own colours.
_Avoid_: Skin, colour scheme, dark/light mode (the dashboard is always dark)

**Shoe**:
A pair of running shoes registered on Strava. Each Run is logged with at most one Shoe. A Shoe is retired once Strava no longer lists it, which is when the Athlete retires (or deletes) it there; it is kept with its last distance.
_Avoid_: Gear (when only shoes are meant), sneaker

**Temperature**:
The air temperature halfway through a Run where it started, in °C, from Open-Meteo's weather history (Strava has no weather). Treadmill Runs and Runs without GPS have none.
_Avoid_: Weather (on its own), average temp (Strava's watch reading)

**Maximum Distance**:
The distance after which a Shoe is due to be retired, the same for every Shoe: 700 km unless the Athlete sets another in Settings. Strava does not know it.
_Avoid_: Lifespan, mileage limit, retirement threshold

### Records

**Best Effort**:
Strava's fastest time over a standard distance (e.g. 1k, 5k, 10k, half marathon, marathon) within a single Run, including parts of longer Runs.
_Avoid_: Split, segment

**Split**:
One kilometre of a single Run as Strava divides it (one mile when the Distance Unit is miles), with its own Moving Time and Pace; the last Split is usually shorter. Not the same as a Best Effort.
_Avoid_: Lap, km segment

**Personal Best**:
The fastest Best Effort for a standard distance across all Runs.
_Avoid_: PR, record

### Training

**Training Week**:
A week from Monday to Sunday, the unit used for totals and charts.
_Avoid_: Calendar week, rolling 7 days

**Streak**:
The number of consecutive Training Weeks, up to the current one, that each contain at least one Run, whatever its distance. The current week only adds to it once it has a Run and only breaks it once it ends without one.
_Avoid_: Chain, run streak (as a daily streak)

**Longest Streak**:
The longest Streak across all Runs, from the Monday of its first Training Week to the Sunday of its last. Of two equally long ones it is the more recent, so the ongoing Streak becomes the Longest Streak as soon as it ties it.
_Avoid_: Best streak, record streak

**Relative Effort**:
Strava's heart-rate-based score for how hard a Run was. Summing it over a week gives the week's training load.
_Avoid_: Suffer score, load (on its own)

**Training Time**:
The Moving Time of a Training Week's Runs, Rides and Swims, each sport on its own and together: how much the Athlete trained, whatever the sport. It is shown beside the running figures, never added to them, so Rides and Swims still count toward nothing.
_Avoid_: Cross-training load, total time (on its own)

**Goal Race**:
A race the Athlete is training for, with a name, a date, a distance and an optional Target Time. Only one Goal Race is upcoming (race day today or later) at a time; past Goal Races are kept for Race Results and comparisons.
_Avoid_: Event, target race

**Target Time**:
The finish time the Athlete wants to beat in a Goal Race.
_Avoid_: Goal time, target pace

**Race Week**:
The Training Week in which a Goal Race takes place (Weeks to Race = 0). It is never a Recovery Week.
_Avoid_: Taper week

**Race Result**:
The Best Effort over the Goal Race's distance from the longest Run on race day. If that distance has no Best Effort, the Run's elapsed time is used. With no Run on race day, there is no Race Result.
_Avoid_: Finish time, official time

**Training Block**:
The Training Weeks that lead up to a Goal Race and end with its Race Week. With a Training Plan, it is 12, 16 or 20 weeks long. Without one, it can be any length (e.g. 14 weeks).
_Avoid_: Plan, cycle, build

**Weeks to Race**:
How many Training Weeks a week is from its Goal Race: the race week is 0, the week before is −1, and so on. Training Blocks are compared week by week using this number.
_Avoid_: Block week, week number

### Planning

**Training Plan**:
What the Athlete intends to do during a Training Block: its Phases, plus a Weekly Target and Running Days for each Training Week. Plan weeks are tied to Weeks to Race, not to calendar dates. A Training Block may have no Training Plan. A Goal Race has at most one Training Plan, so the upcoming Goal Race's plan is the only active one.
_Avoid_: Schedule, program, block (on its own)

**Phase**:
A stretch of 4 consecutive Training Weeks in a Training Plan with one purpose: Build, Peak or Taper. The last Phase of every Training Plan is a Taper.
_Avoid_: Cycle, mesocycle, stage

**Recovery Week**:
A Training Week within a Phase with deliberately lower load, marked as such by the Athlete in the Training Plan. When a plan's length divides into 4-week stretches, every 4th week from the plan's first starts out marked, except the Race Week; the Athlete can change that.
_Avoid_: Rest week, deload, cutback week

**Weekly Target**:
The planned distance and planned Long Run for one Training Week.
_Avoid_: Goal (for weeks), quota

**Running Days**:
The days of a Training Week on which the Training Plan schedules Runs. Each Running Day has a Run Type; a day without one is a rest day.
_Avoid_: Training days, run days

**Run Type**:
What a Running Day is planned for: Easy, Tempo, Intervals or Long. The list is fixed. It only says what was intended: actual Runs are never given a Run Type, and the Long Run of a week is still its longest Run, whichever day was planned as Long.
_Avoid_: Workout, session, workout type

**Yearly Goal**:
The distance the Athlete wants to run in the current calendar year. There is at most one; once its year is over, it no longer counts. Progress is measured against the goal spread evenly over the days of the year.
_Avoid_: Annual target, mileage goal

### Fitness

**Fitness Run**:
A Run that counts toward the fitness trends: any Run except treadmill and virtual Runs (their Pace is unreliable) and Runs on a Goal Race's race day.
_Avoid_: Easy Run (Easy is a Run Type), qualifying Run

**Aerobic Efficiency**:
How far a Fitness Run carried the Athlete per heartbeat: its grade-adjusted distance per minute divided by its average heart rate, in metres per beat. Higher is better.
_Avoid_: Efficiency Factor, EF

**Cardiac Drift**:
How much heart rate per grade-adjusted speed rises from the first half of a Long Run to the second, in %, with the km Splits halved by distance. Only Long Runs of at least 10 km that are Fitness Runs have one; up to 5% is good.
_Avoid_: Decoupling, Pa:HR

**Cadence**:
Steps per minute, both feet together: twice the per-foot value Strava stores.
_Avoid_: SPM, stride rate

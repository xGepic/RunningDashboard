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
Any entry recorded on Strava, of any sport. The dashboard ignores Activities that are not Runs.

**Moving Time**:
The time during a Run when the Athlete was actually moving, without pauses. It is used for Pace and for weekly time totals.
_Avoid_: Duration, time (on its own)

**Long Run**:
The longest Run of a Training Week.
_Avoid_: LSD, long one

**Pace**:
Moving Time per kilometre, written as min:ss/km.
_Avoid_: Speed (except when talking about Strava's raw data)

**Shoe**:
A pair of running shoes registered on Strava. Each Run is logged with at most one Shoe.
_Avoid_: Gear (when only shoes are meant), sneaker

**Maximum Distance**:
The distance after which a Shoe is due to be retired: 700 km, the same for every Shoe. It is fixed in the dashboard; Strava does not know it.
_Avoid_: Lifespan, mileage limit, retirement threshold

### Records

**Best Effort**:
Strava's fastest time over a standard distance (e.g. 1k, 5k, 10k, half marathon, marathon) within a single Run, including parts of longer Runs.
_Avoid_: Split, segment

**Split**:
One kilometre of a single Run as Strava divides it, with its own Moving Time and Pace; the last Split is usually shorter. Not the same as a Best Effort.
_Avoid_: Lap, km segment

**Personal Best**:
The fastest Best Effort for a standard distance across all Runs.
_Avoid_: PR, record

### Training

**Training Week**:
A week from Monday to Sunday, the unit used for totals and charts.
_Avoid_: Calendar week, rolling 7 days

**Relative Effort**:
Strava's heart-rate-based score for how hard a Run was. Summing it over a week gives the week's training load.
_Avoid_: Suffer score, load (on its own)

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
A Training Week within a Phase with deliberately lower load, marked as such by the Athlete in the Training Plan.
_Avoid_: Rest week, deload, cutback week

**Weekly Target**:
The planned distance and planned Long Run for one Training Week.
_Avoid_: Goal (for weeks), quota

**Running Days**:
The days of a Training Week on which the Training Plan schedules Runs.
_Avoid_: Training days, run days

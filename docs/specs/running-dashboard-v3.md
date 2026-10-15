# Running Dashboard v3

v3 adds two features to v2 ([running-dashboard-v2.md](running-dashboard-v2.md)). Terms follow [CONTEXT.md](../../CONTEXT.md), and the ADRs in [docs/adr](../adr) still apply.

| # | Feature | Status |
|---|---|---|
| 01 | Retired Shoes | done |
| 02 | Year in Review image | done |

---

## 01: Retired Shoes

A Shoe the Athlete retires on Strava shows as retired on the dashboard, instead of disappearing.

- Strava's athlete endpoint leaves retired Shoes out of its Shoe list (and deleted ones, which it can't tell apart without a request per Shoe). So a stored Shoe that Strava no longer lists is marked retired and kept, with the last distance Strava gave it. Its Runs keep showing its name, and it can still be the Year in Review's most-used Shoe.
- A retired Shoe that Strava lists again (un-retired on Strava) takes Strava's values again, and so is active again.
- Shoes deleted before v3 stay missing: their Runs show no Shoe.

## 02: Year in Review image

The Athlete can share a year's highlights as a picture.

- The Calendar's Review has a "Download image" button next to the Months | Review switch. It saves `Year in Review 2026.png` through the browser's normal download.
- The image is the Review as the page shows it, in the Athlete's Theme, titled with the Athlete's first name and the year ("Stefan's 2026 in Runs"). The current year is labelled "so far" as on the page.
- It leaves out what shouldn't go to strangers or doesn't work in a picture: Run names, days of the month (dates show as months only, e.g. "Sep"), links, the hover titles and the Rides and Swims footnote. Goal Races with their Race Results and the most-used Shoe stay.
- It is as tall as the Review needs, never cut or shrunk, at twice the screen resolution so the text stays sharp.
- It is drawn in the browser, so it works offline and in the packaged app.

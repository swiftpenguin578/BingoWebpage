# U3 item3 — summary decision boundary (resolved)

Resolved by explicit user7 October2026 U3-Q9(a)/Q10: the recommended linked pointer sentence is approved and implemented, with Danish translation and unchanged Q6 reservation. Actual-page measurements now appear in signup-item3-checks.json. The text below records the earlier decision evidence, not an active summary blocker.

Item2 committed as 856d421 (Signup setup server operations/cap limit). Item3 reference binding has reached the required Q5/Q6 decision boundary before production UI edits. No page exception or copy change has been implemented. No item3 commit or first-working-page claim.

Authority: review-notes/08-decisions.md, “Summary reservation for every page”, exception path: “if a page’s summary is frequently two lines, the implementer reports it with measurements; the planner brings it to the user, who decides between a per-page override ... or shorter summary text. No implementer-chosen exceptions.” Q6 keeps two empty loading lines at <=640px and one wider. Brief80 stop rule requires stopping the item for a product question.

Measured the actual frozen SignupSetup.dc.html using matching self-hosted Geist fonts, synthetic Autumn Bingo 2027, in Chromium and WebKit. No frozen file edits. The full reference has two summary items: “Capacity, signup code and the questions players answer when they sign up for Autumn Bingo 2027.” and “Dates are on Schedule; opening and closing signups on Overview.”

|Width|Reference height Chromium / WebKit|Reference text lines|Shared loading reservation|Shorter option height Chromium / WebKit|
|---|---|---|---|---|
|390|82.25 / 82.265625px|4 plus 4px item-row gap|2 lines|39.125 / 39.140625px (2 lines)|
|494|62.6875 / 62.6875px|3 plus 4px gap|2 lines|19.5625 / 19.5625px (1 line)|
|860|43.125 / 43.125px|2 plus 4px gap|1 line|19.5625 / 19.5625px (1 line)|
|1280|43.125 / 43.125px|2 plus 4px gap|1 line|19.5625 / 19.5625px (1 line)|
|1440|19.5625 / 19.5625px|1|1 line|19.5625 / 19.5625px (1 line)|

Line height19.575px. Exact measurements/item geometry in signup-summary-decision.json. These are reference English measurements, not claims about the unfinished production page or Danish. The shorter option was measured only by removing the first item in the browser DOM; nothing saved to production/reference source.

Recommendation: drop the first sentence and retain exactly “Dates are on Schedule; opening and closing signups on Overview.” Keep both links and shared Q6 reservation. This fits one line at every wider test width and at most two on phones. Alternative: keep the full reference copy and explicitly authorize a Signup setup loading-height override, including its widths/heights; a two-line wider reservation would still leave phone growth as shown.

Checks: both engine measurement runs exited0; git diff --check PASS. Item2 exact PostgreSQL/domain evidence is in signup-item2-checkpoint.md and signup-item2-checks.json. No additional .NET checks or Schedule remeasurement for this decision. Review environment remains running and owned by this lane; its stale marker remains preserved. Schedule links from required report remain available. No laneT files touched.

Next action: user/planner resolves shorter text versus an explicit reservation override; dispatcher wakes the same implementer via followup_task. Continue item3 and stop after its first working commit for early look. Do not redo item2 or its passing checks.

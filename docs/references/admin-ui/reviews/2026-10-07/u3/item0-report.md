# U3 item0 — shared registered-page conformance

Implemented after separate Identity correction `b790906` and the user's
explicit approval of consolidation steps1–5. U3-Q6 supersedes Q5: every loading
header reserves two empty summary lines at the existing phone breakpoint
(≤640px), one above it. Loaded reference geometry stays natural. Reservation
applies only while loading, preserving existing failure presentation. Register
row and the affected Dashboard/Events entries are reconciled.

One entry in scripts/lib/admin-page-conformance-pages.cjs registers each page's
family, URL, fixture, reference/source/module, selectors, text rows and update
probe. Identity, Dashboard and Events are registered. The generic JS-runner
entry automatically runs both engines; no runner modification is needed.
Production pageKind/_AdminDesignTemplates registration is unchanged for lane T.

## Scoped checks

Exact engine/test commands, exits, timings, shared design results and compact
geometry are in item0-checks.json. Bundled Node is recorded there.

- Generic conformance: **25 geometry cases passed per engine**, exit 0 each;
  390/494/860/1280/1440px; forced Dashboard one-line/wrapped stats preserve Q-SK2;
  exact 150ms delay/399–400ms minimum and fast149ms path; native frame/stylesheet
  and no-load-fade assertions; interaction animations retained; 1000px/342px
  document bounds and attempted document scroll; update node/focus/scroll and
  immediate control state; Danish full load, shell and culture switch without
  document replacement; literal Danish resources and shared design checks1–6.
- All transferred cases passed **before** removing their originals. Deletions
  are exactly scripts/check-u2-body.cjs and its one-line browser wrapper.
- Retained special cases passed after trimming, each engine: archived Identity
  **1**, results/empty animations **3**, Events filtered/reset document bounds
  **8**. No other tests were deleted.
- Affected Q6 regression checks, each engine: destination headers **12**,
  Dashboard reference/header geometry **8**, Events reference/header geometry
  **10**. Loaded reference layout remains verified.
- Syntax and git diff --check passed. Frozen token/component pairs identical.
  No C#/Razor changes, Release build, full JS runner or whole .NET suite.

The final shared CSS selector was restricted to aria-busy=true after transfer
validation to preserve existing failure-header rules; loading behavior is
identical. The transferred loading evidence remains applicable. Changes here
introduce no timing, transport, saving or lifecycle mechanism.

## Phone measurements and accepted movement

Both engines agree (CSS px; subpixel differences below0.1):

| Page | Width | Loading summary | Loaded summary | Header delta |
| --- | ---: | ---: | ---: | ---: |
| Identity | 390 | 39.140625 | 39.125 | -0.015625 |
| Dashboard | 390 | 39.140625 | 39.125 | -0.015625 |
| Events | 390 | 39.140625 | 43.125 | +3.984375 |
| All three | 494 | 39.140625 | 19.5625 | -19.578125 |

At494px the natural loaded summary is one line, so following content settles
upward by that delta. The user expressly directed preservation of loaded
reference geometry with Q6. At860/1280/1440 one loading line is reserved.
No page-specific exception or loaded-height minimum was added.

## Handoff

Item0 is implemented and scoped-tested, awaiting Claude's independent review;
no new visual acceptance is claimed. Historical approval interruption and
pre-Q6 evidence are retained in item0-consolidation-checkpoint.md and
item0-validation-checkpoint.json. Generic probes were corrected during creation
for fingerprinted asset names, responsive table width, async init exports and
an unchanged-population sort fixture; no assertion was weakened to hide a
product defect.

The user's review environment remains stopped. Next permitted work is item1
Schedule, with its first working commit/report and review links, then item2
without waiting. Item3 still stops for the user; full JS/Release gate only at
item4. No push, merge, deployment, lane-T edits or extra worker.

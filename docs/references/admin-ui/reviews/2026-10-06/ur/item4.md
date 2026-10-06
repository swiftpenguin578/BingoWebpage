# UR item 4 — PostgreSQL, route and command proof

Executed 6 October 2026 on the item4 working candidate based on `ff807ce`.
Final focused PostgreSQL/HTTP theories: **2 passed / 0 failed / 0 skipped**.
All printed routes returned **200** for their listed accounts: **69 live / 70
final-review**. Printed hidden Overviews and direct hidden event Identity/public
Board/Teams returned **404** for plain Admin. Discarded direct Identity/public
Board/Teams also returned **404**; discarded names are absent from public/account
lists and Events directory. Enabled accounts log in without password reset;
disabled sign-in is refused and protected navigation redirects to Login.

## Invariants and assertion authority

Real Testcontainers PostgreSQL proves exactly one visible current event per
profile without IsDevelopmentFixture exemption; current-event readiness and actual
StartNow both reject another start. FirstPublicAt equals first signup opening;
three hidden events have only Hide-permitted states (Final review, legacy Finalized,
Archived). Ruling63 (planner source labelled 7 October, accepted/relayed 6 October)
settles those boundaries; production Hide restrictions are unchanged. Non-microsecond
clock input is normalized to exact PostgreSQL microseconds and read back exactly.
Real approval of competing evidence is blocked without contributions, affiliated
published rosters include former members, board correction retains publication,
and Pending/Rejected/CouldNotUpdate WOM end outcomes are present. A GET at the
unserved loopback WOM origin `127.0.0.1:1` returns the local fake's response.

Ruling64 (planner source labelled 7 October, accepted/relayed 6 October) corrects
brief62's overbroad “Discarded appears nowhere” to event populations/navigation.
Exact change in the new UR theory/helper:

- Before: discarded name absent from the whole Dashboard HTML, including audit.
  After: discarded name and exact audit ID **present in recent audit**, absent from
  the remaining Dashboard HTML, and exact audit ID **present in Audit** for Admin
  and SuperAdmin. Authority: ruling64 §2 and FUNCTIONAL_CONTRACTS §9.3 ADM-AUDIT-01.
- Added precise absence checks: switcher event IDs; stale remembered-event HTTP
  cookie cleared with no selected sidebar ID; Dashboard held count exactly **3**
  (visible current plus two visible archives), history/chart/participation population,
  active/attention/readiness/pending evidence/milestones, shared action populations,
  Events directory, account/public lists and direct routes. Authority: ruling64 §1.
- No production audit changes, baseline test edits, skipped tests or deleted tests.
  The existing Dashboard card still chooses Live, otherwise next preparation
  (PRODUCT_REQUIREMENTS Dashboard contract); final-review card is private setup,
  while shared current-event navigation identifies final review and excludes hidden.

## Audit link observation (T1/U2 only)

Dashboard's discarded audit renders **no event navigation link**. Audit's “Open
entry” link stays within retained Audit and returns **200** for both Admin roles.
Actual synthetic targets from the final passing run:

- `final-review ReviewAdmin: discard audit link /Admin/Audit?entry=3134abeb-8587-4fcc-bda9-2b699ea1bffb&eventId=4e521523-0a2a-4e13-b35e-879fa7106e81&pageNumber=1 => 200; Dashboard discard audit has no event link.`
- `final-review ReviewOwner: discard audit link /Admin/Audit?entry=3134abeb-8587-4fcc-bda9-2b699ea1bffb&eventId=4e521523-0a2a-4e13-b35e-879fa7106e81&pageNumber=1 => 200; Dashboard discard audit has no event link.`
- `live ReviewAdmin: discard audit link /Admin/Audit?entry=73a6e1e9-bd81-4747-9cc5-7becc96b21db&eventId=93aef3ca-deb6-4f63-b21c-62fbc65822c9&pageNumber=1 => 200; Dashboard discard audit has no event link.`
- `live ReviewOwner: discard audit link /Admin/Audit?entry=73a6e1e9-bd81-4747-9cc5-7becc96b21db&eventId=93aef3ca-deb6-4f63-b21c-62fbc65822c9&pageNumber=1 => 200; Dashboard discard audit has no event link.`

## Command and required gates

- Safety tests **7 passed**, no Docker mutations or process signals: exact owned
  label/name/ID/port and PostgreSQL marker; protected user containers; local database
  name refusal; occupied port; changed PID identity; macOS Python re-exec ownership;
  inherited Production/live WOM/R2 settings overridden. Review storage rejects symlinks.
- Actual owned create/live built `2026-10-06T10:18:43.9404540+00:00`; refresh/final-review
  built `2026-10-06T10:20:13.2943750+00:00`. PostgreSQL readback: all **21** creation
  dates and **20** starts rebuilt by exactly **89.353921 seconds**. App5310/reference5320
  readiness returned 200. Matching stop succeeded; owned environment is left stopped.
  Synthetic printed final guide below is copied from that real refresh.
- Full JS runner **51 passed / 0 failed**, Chromium/WebKit included, on item4 code
  candidate before the ruling64-only test clarification. Controlled JS fixture
  generation **8/0/0**. Whole Bingo.BrowserTests **150/0/0** on the same candidate.
  These passing unaffected suites are reused per the accepted continuation.
- Final non-incremental Release solution build **0 warnings / 0 errors**. Final
  focused theories ran sequentially with --no-build after that completed build.
  Diff check passes; frozen tokens.css/components.css and old seeder unchanged
  against `4d34d63`. No whole-.NET execution, CI or independent self-review here.

Scratch execution records: `/private/tmp/bingo-ur-item4-passed.log`, final TRX
`/private/tmp/bingo-ur-item4-final-trx/_Christophers-MacBook-Air_2026-10-06_12_37_36_net10.0.trx`,
`/private/tmp/bingo-ur-final-release.log`, `/private/tmp/bingo-ur-full-js.log`,
`/private/tmp/bingo-ur-browser.log` and `/private/tmp/bingo-ur-item4-command-proof.json`.

Earlier failures retained honestly: original expanded run 0/2/0 was blocked by the
now-resolved broad audit assertion. First continuation 0/2/0 exposed the test cookie
jar replacing the intended stale preference; second 0/2/0 exposed an assumed cookie
name instead of configured Bingo.Auth. Both fixture defects were corrected. The
next run 1/1/0 lost its second case's deps file during an overlapping clean build;
sequential final execution is 2/0/0. No product assertions were relaxed for these
failures. A safety invocation without runtime escalation could not bind its ephemeral
loopback port; the authorized escalated invocation passed 7/7. Earlier command date
parser handling of PostgreSQL +00 was corrected without product timestamp tolerance.

Independent Claude review, final-SHA whole .NET zero failed/zero skipped and user
review-environment walkthrough remain pending. No U2+, lane T, push/merge/deploy.

## Required fixture corrections from proof preparation

- Kept ReviewWebsite free of all event membership; added ReviewSecondCoCaptain to
  fill the second affiliated roster (accounts 10 → 11). This changes only the new
  UR test's account-count assertion, authorized by brief62's plain-website minimum.
  Existing baseline tests were not edited.
- Added a visible archived could-not-update scenario (events 20 → 21), giving that
  WOM outcome a real accessible WOM page instead of only a hidden Overview label.
- Start price snapshots now use the existing EventItemPriceService capture with
  provider-unavailable catalogue fallback; no live price fetch occurs in seeding.
- Archived roster link uses its permitted public Teams destination; Admin Draft is
  correctly refused/redirected for Archived. No route gate was loosened.
- Corrected the new guide's plain-Admin Audit label: retained audit includes hidden
  events under FUNCTIONAL_CONTRACTS §9.3 / user C36. Fixed item3 evidence's one
  blank EOF line reported by its staged diff check; no prior commit was amended.

## Final-review linked list copied from the real refresh

# UI review — final-review

Rebuilt at 2026-10-06T10:20:13.2943750+00:00. Synthetic local data only.

App: http://127.0.0.1:5310 · References: http://127.0.0.1:5320

All review accounts use the local-only password `ReviewOnly!1234`.
The disabled account is deliberately refused at sign-in. Sign out before changing accounts.
Hidden scenarios require ReviewOwner; they do not count as visible current events.
Discarded scenarios have no review link and are excluded from page lists.

## Dashboard / Events

- [One visible current event and more than eight upcoming setups](http://127.0.0.1:5310/Admin/Index) — sign in: **ReviewAdmin**
- [Events directory — discarded excluded](http://127.0.0.1:5310/Admin/Events/Index) — sign in: **ReviewAdmin**
- [Create a private draft](http://127.0.0.1:5310/Admin/Events/Create) — sign in: **ReviewAdmin**

## Identity / phases / switcher

- [Private setup [Draft]](http://127.0.0.1:5310/Admin/Events/Identity/45370d3c-65a9-46d6-9b23-f71b224cd708) — sign in: **ReviewAdmin**
- [Signups open [SignupOpen]](http://127.0.0.1:5310/Admin/Events/Identity/05dc75cd-5d73-4eed-99fa-91e1d438bfcc) — sign in: **ReviewAdmin**
- [Signups closed — finalized affiliated rosters [SignupClosed]](http://127.0.0.1:5310/Admin/Events/Identity/362b00dd-1e4e-476e-8a69-2f5f12cdf705) — sign in: **ReviewAdmin**
- [Unknown timezone [SignupClosed]](http://127.0.0.1:5310/Admin/Events/Identity/f2f6baf2-b251-4763-b956-0ec933fa5fa8) — sign in: **ReviewAdmin**
- [Upcoming setup 01 [Draft]](http://127.0.0.1:5310/Admin/Events/Identity/121a918b-642d-4767-b0e6-c4ea44251801) — sign in: **ReviewAdmin**
- [Upcoming setup 02 [Draft]](http://127.0.0.1:5310/Admin/Events/Identity/2a6118ca-f2d8-4ace-a761-d5ad9a16521d) — sign in: **ReviewAdmin**
- [Upcoming setup 03 [Draft]](http://127.0.0.1:5310/Admin/Events/Identity/180b069e-65f7-4934-8c3f-9eee1d6a8f92) — sign in: **ReviewAdmin**
- [Upcoming setup 04 [Draft]](http://127.0.0.1:5310/Admin/Events/Identity/8621dc14-6e53-4e83-89d8-6292f3d16ee2) — sign in: **ReviewAdmin**
- [Upcoming setup 05 [Draft]](http://127.0.0.1:5310/Admin/Events/Identity/b6f8ab85-2830-4b8d-9ee3-74b2132b5a65) — sign in: **ReviewAdmin**
- [Upcoming setup 06 [Draft]](http://127.0.0.1:5310/Admin/Events/Identity/a88de36b-a63d-462e-932c-672341748e15) — sign in: **ReviewAdmin**
- [Upcoming setup 07 [Draft]](http://127.0.0.1:5310/Admin/Events/Identity/9c66ca85-089a-4145-986d-edd2effec48f) — sign in: **ReviewAdmin**
- [Upcoming setup 08 [Draft]](http://127.0.0.1:5310/Admin/Events/Identity/7edafe29-64e7-4d6b-a50c-6414091777ad) — sign in: **ReviewAdmin**
- [Upcoming setup 09 [Draft]](http://127.0.0.1:5310/Admin/Events/Identity/6226f687-dd3c-4c3a-83a7-fc94ddea612d) — sign in: **ReviewAdmin**
- [Archived — affiliated roster history [Archived]](http://127.0.0.1:5310/Admin/Events/Identity/13f58c2b-c4d8-4277-9e20-0fa432e2876b) — sign in: **ReviewAdmin**
- [Archived — WOM end could not update [Archived]](http://127.0.0.1:5310/Admin/Events/Identity/6d510887-92df-4ba5-8d31-eddea870d6e3) — sign in: **ReviewAdmin**
- [Cancelled with signup history [Cancelled]](http://127.0.0.1:5310/Admin/Events/Identity/157d8e1a-65fc-4fc6-92a0-69522eb55896) — sign in: **ReviewAdmin**
- [Final review — published board correction [AwaitingFinalReview]](http://127.0.0.1:5310/Admin/Events/Identity/ba289397-0c73-48c2-9ccb-7b4393331bf3) — sign in: **ReviewAdmin**

## Hidden / Audit

- [Hidden final review — SuperAdmin hidden overview](http://127.0.0.1:5310/Admin/Events/Manage/ab5fac3b-aeb2-4089-927d-60a5d33f4aba?hidden=true) — sign in: **ReviewOwner**
- [Hidden legacy Finalized — SuperAdmin hidden overview](http://127.0.0.1:5310/Admin/Events/Manage/860c3f09-6ebe-45dd-b985-1cf03cd429e4?hidden=true) — sign in: **ReviewOwner**
- [Hidden Archived — SuperAdmin hidden overview](http://127.0.0.1:5310/Admin/Events/Manage/ab07e7ea-95ab-45b0-b169-75e5136c4651?hidden=true) — sign in: **ReviewOwner**
- [Audit including hidden events](http://127.0.0.1:5310/Admin/Audit/Index) — sign in: **ReviewOwner**
- [Plain Admin retained audit — including hidden-event history](http://127.0.0.1:5310/Admin/Audit/Index) — sign in: **ReviewAdmin**

## Schedule / Signup setup

- [Private setup — schedule](http://127.0.0.1:5310/Admin/Events/Schedule/45370d3c-65a9-46d6-9b23-f71b224cd708) — sign in: **ReviewAdmin**
- [Private setup — questions](http://127.0.0.1:5310/Admin/Events/Questions/45370d3c-65a9-46d6-9b23-f71b224cd708) — sign in: **ReviewAdmin**
- [Signups open — schedule](http://127.0.0.1:5310/Admin/Events/Schedule/05dc75cd-5d73-4eed-99fa-91e1d438bfcc) — sign in: **ReviewAdmin**
- [Signups open — questions](http://127.0.0.1:5310/Admin/Events/Questions/05dc75cd-5d73-4eed-99fa-91e1d438bfcc) — sign in: **ReviewAdmin**
- [Signups closed — finalized affiliated rosters — schedule](http://127.0.0.1:5310/Admin/Events/Schedule/362b00dd-1e4e-476e-8a69-2f5f12cdf705) — sign in: **ReviewAdmin**
- [Signups closed — finalized affiliated rosters — questions](http://127.0.0.1:5310/Admin/Events/Questions/362b00dd-1e4e-476e-8a69-2f5f12cdf705) — sign in: **ReviewAdmin**

## Overview / Participants / Teams

- [Final review — published board correction — overview](http://127.0.0.1:5310/Admin/Events/Manage/ba289397-0c73-48c2-9ccb-7b4393331bf3) — sign in: **ReviewAdmin**
- [Final review — published board correction — participants and former members](http://127.0.0.1:5310/Admin/Events/Participants/ba289397-0c73-48c2-9ccb-7b4393331bf3) — sign in: **ReviewAdmin**
- [Final review — published board correction — finalized affiliated rosters](http://127.0.0.1:5310/Admin/Events/Draft/ba289397-0c73-48c2-9ccb-7b4393331bf3) — sign in: **ReviewAdmin**
- [Signups closed — finalized affiliated rosters — overview](http://127.0.0.1:5310/Admin/Events/Manage/362b00dd-1e4e-476e-8a69-2f5f12cdf705) — sign in: **ReviewAdmin**
- [Signups closed — finalized affiliated rosters — participants and former members](http://127.0.0.1:5310/Admin/Events/Participants/362b00dd-1e4e-476e-8a69-2f5f12cdf705) — sign in: **ReviewAdmin**
- [Signups closed — finalized affiliated rosters — finalized affiliated rosters](http://127.0.0.1:5310/Admin/Events/Draft/362b00dd-1e4e-476e-8a69-2f5f12cdf705) — sign in: **ReviewAdmin**
- [Archived — affiliated roster history — overview](http://127.0.0.1:5310/Admin/Events/Manage/13f58c2b-c4d8-4277-9e20-0fa432e2876b) — sign in: **ReviewAdmin**
- [Archived — affiliated roster history — participants and former members](http://127.0.0.1:5310/Admin/Events/Participants/13f58c2b-c4d8-4277-9e20-0fa432e2876b) — sign in: **ReviewAdmin**
- [Archived — affiliated roster history — finalized affiliated rosters](http://127.0.0.1:5310/Events/ur-archived/Teams) — sign in: **ReviewAdmin**

## Public board / affiliated teams

- [Final review — published board correction — public teams](http://127.0.0.1:5310/Events/ur-current/Teams) — sign in: **ReviewParticipant**
- [Final review — published board correction — public board](http://127.0.0.1:5310/Events/ur-current/Board) — sign in: **ReviewParticipant**
- [Signups closed — finalized affiliated rosters — public teams](http://127.0.0.1:5310/Events/ur-signups-closed/Teams) — sign in: **ReviewParticipant**
- [Signups closed — finalized affiliated rosters — public board](http://127.0.0.1:5310/Events/ur-signups-closed/Board) — sign in: **ReviewParticipant**
- [Archived — affiliated roster history — public teams](http://127.0.0.1:5310/Events/ur-archived/Teams) — sign in: **ReviewParticipant**
- [Archived — affiliated roster history — public board](http://127.0.0.1:5310/Events/ur-archived/Board) — sign in: **ReviewParticipant**

## Board / Review

- [Published board with exceptional working-copy correction](http://127.0.0.1:5310/Admin/Events/Board/ba289397-0c73-48c2-9ccb-7b4393331bf3) — sign in: **ReviewAdmin**
- [Pending evidence queue](http://127.0.0.1:5310/Admin/Review/Index?eventId=ba289397-0c73-48c2-9ccb-7b4393331bf3) — sign in: **ReviewAdmin**
- [Blocked approval — approve or reject the earlier upload first](http://127.0.0.1:5310/Admin/Review/Details/e62967b2-73c4-4cea-8351-70711b41adec) — sign in: **ReviewAdmin**

## Final review / WOM

- [Current final review — pending evidence and WOM end update](http://127.0.0.1:5310/Admin/Events/Finalize/ba289397-0c73-48c2-9ccb-7b4393331bf3) — sign in: **ReviewAdmin**
- [WOM end update Pending](http://127.0.0.1:5310/Admin/Events/WiseOldMan/ba289397-0c73-48c2-9ccb-7b4393331bf3) — sign in: **ReviewAdmin**
- [WOM end update Rejected — archived history](http://127.0.0.1:5310/Admin/Events/WiseOldMan/13f58c2b-c4d8-4277-9e20-0fa432e2876b) — sign in: **ReviewAdmin**
- [WOM end could-not-update — archived history](http://127.0.0.1:5310/Admin/Events/WiseOldMan/6d510887-92df-4ba5-8d31-eddea870d6e3) — sign in: **ReviewAdmin**

## Accounts / Catalogue / Audit

- [Every global role and disabled account](http://127.0.0.1:5310/Admin/Accounts/Index) — sign in: **ReviewOwner**
- [Reviewed local catalogue](http://127.0.0.1:5310/Admin/Catalogue/Index) — sign in: **ReviewAdmin**

## Accounts / sign-in

- [ReviewOwner — SuperAdmin](http://127.0.0.1:5310/Account/MyEvents) — sign in: **ReviewOwner**
- [ReviewAdmin — plain Admin](http://127.0.0.1:5310/Account/MyEvents) — sign in: **ReviewAdmin**
- [ReviewCaptain — captain](http://127.0.0.1:5310/Account/MyEvents) — sign in: **ReviewCaptain**
- [ReviewCoCaptain — co-captain](http://127.0.0.1:5310/Account/MyEvents) — sign in: **ReviewCoCaptain**
- [ReviewParticipant — participant](http://127.0.0.1:5310/Account/MyEvents) — sign in: **ReviewParticipant**
- [ReviewWebsite — plain website account](http://127.0.0.1:5310/Account/MyEvents) — sign in: **ReviewWebsite**
- [ReviewDisabled — attempt sign-in; disabled account is refused](http://127.0.0.1:5310/Account/Login) — sign in: **ReviewDisabled** (attempt is refused)
- [ReviewFormer — former team member](http://127.0.0.1:5310/Account/MyEvents) — sign in: **ReviewFormer**
- [ReviewSecondCaptain — captain](http://127.0.0.1:5310/Account/MyEvents) — sign in: **ReviewSecondCaptain**
- [ReviewSecondCoCaptain — co-captain](http://127.0.0.1:5310/Account/MyEvents) — sign in: **ReviewSecondCoCaptain**
- [ReviewSecondMember — participant](http://127.0.0.1:5310/Account/MyEvents) — sign in: **ReviewSecondMember**

## Captain / Co-captain / Participant

- [Captain evidence workspace](http://127.0.0.1:5310/Submissions?eventId=ba289397-0c73-48c2-9ccb-7b4393331bf3) — sign in: **ReviewCaptain**
- [Co-captain evidence workspace](http://127.0.0.1:5310/Submissions?eventId=ba289397-0c73-48c2-9ccb-7b4393331bf3) — sign in: **ReviewCoCaptain**
- [Participant evidence ledger](http://127.0.0.1:5310/Submissions?eventId=ba289397-0c73-48c2-9ccb-7b4393331bf3) — sign in: **ReviewParticipant**
- [Former member history](http://127.0.0.1:5310/Account/MyEvents) — sign in: **ReviewFormer**

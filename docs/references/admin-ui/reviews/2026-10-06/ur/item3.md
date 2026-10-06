# UR item 3 — grouped linked guide

The command now prints and saves the same scenario catalogue generated from the
seed result. The gitignored copy is `artifacts/ui-review/scenarios.md`; each line
contains its full current-route URL and named synthetic review account. Disabled
sign-in is explicitly an attempted/refused case. Discarded has no link; hidden
links use the permitted SuperAdmin hidden Overview. Local passwords follow the
README Development-fixture convention.

Executed 6 October: owned refresh/live succeeded; Release build 0 warnings/errors,
67 URL/account entries across 11 groups, hidden route restrictions and discarded
link exclusion passed, diff check passed. All-URL response proof follows in item 4.
No existing tests were edited. The macOS Python shim re-exec initially changed its
executable path; the guard safely refused refresh. The process check now preserves
PID/start time and exact arguments while allowing that Python interpreter change.
The following guide is copied from the successful real run, not constructed for
evidence. Independent review and manual acceptance remain pending.

---

# UI review — live

Rebuilt at 2026-10-06T10:10:01.8599250+00:00. Synthetic local data only.

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

- [Private setup [Draft]](http://127.0.0.1:5310/Admin/Events/Identity/ba6f76f6-12d0-46dd-b6af-9e1d4aa8a123) — sign in: **ReviewAdmin**
- [Signups open [SignupOpen]](http://127.0.0.1:5310/Admin/Events/Identity/47be3330-f281-45aa-b3ba-01706c92a277) — sign in: **ReviewAdmin**
- [Signups closed — finalized affiliated rosters [SignupClosed]](http://127.0.0.1:5310/Admin/Events/Identity/a700bb29-a098-488b-8be8-d7abb3353cbb) — sign in: **ReviewAdmin**
- [Unknown timezone [SignupClosed]](http://127.0.0.1:5310/Admin/Events/Identity/3bbd4bc1-6d2e-4f16-8dad-48c420c8104d) — sign in: **ReviewAdmin**
- [Upcoming setup 01 [Draft]](http://127.0.0.1:5310/Admin/Events/Identity/3447d91d-f61f-4fc9-bc93-fa5c99d37bf6) — sign in: **ReviewAdmin**
- [Upcoming setup 02 [Draft]](http://127.0.0.1:5310/Admin/Events/Identity/74d6832b-d02c-4aa6-af13-7972e52914ab) — sign in: **ReviewAdmin**
- [Upcoming setup 03 [Draft]](http://127.0.0.1:5310/Admin/Events/Identity/7cae892a-1c87-45ef-9268-860c608e95d3) — sign in: **ReviewAdmin**
- [Upcoming setup 04 [Draft]](http://127.0.0.1:5310/Admin/Events/Identity/2b85cb15-42bc-481d-b853-bbd91ebc9b28) — sign in: **ReviewAdmin**
- [Upcoming setup 05 [Draft]](http://127.0.0.1:5310/Admin/Events/Identity/c7de5ff6-0535-4677-8281-019a2f178f44) — sign in: **ReviewAdmin**
- [Upcoming setup 06 [Draft]](http://127.0.0.1:5310/Admin/Events/Identity/38bdac01-24a9-4f15-b784-3e336cfb3323) — sign in: **ReviewAdmin**
- [Upcoming setup 07 [Draft]](http://127.0.0.1:5310/Admin/Events/Identity/e934f608-1f97-4510-8c1f-d8482cb859e9) — sign in: **ReviewAdmin**
- [Upcoming setup 08 [Draft]](http://127.0.0.1:5310/Admin/Events/Identity/d982ae4b-ed27-4fec-8b49-1e5a971d1205) — sign in: **ReviewAdmin**
- [Upcoming setup 09 [Draft]](http://127.0.0.1:5310/Admin/Events/Identity/dcb3feca-edd3-4db1-8520-f67b0ec4cf98) — sign in: **ReviewAdmin**
- [Archived — affiliated roster history [Archived]](http://127.0.0.1:5310/Admin/Events/Identity/e1cf3078-d8a9-4ea4-a5db-cc6cfcafb4f0) — sign in: **ReviewAdmin**
- [Cancelled with signup history [Cancelled]](http://127.0.0.1:5310/Admin/Events/Identity/420c4085-e726-4745-a814-6b3f84ae87b5) — sign in: **ReviewAdmin**
- [Live — published board correction [Live]](http://127.0.0.1:5310/Admin/Events/Identity/978a811a-57dc-4395-b2de-9c8d6a038212) — sign in: **ReviewAdmin**

## Hidden / Audit

- [Hidden final review — SuperAdmin hidden overview](http://127.0.0.1:5310/Admin/Events/Manage/b9711ef4-db63-4f2e-9027-82e36119926e?hidden=true) — sign in: **ReviewOwner**
- [Hidden legacy Finalized — SuperAdmin hidden overview](http://127.0.0.1:5310/Admin/Events/Manage/179923c3-c0f7-4af5-8644-85f94ee10091?hidden=true) — sign in: **ReviewOwner**
- [Hidden Archived — SuperAdmin hidden overview](http://127.0.0.1:5310/Admin/Events/Manage/cfc33cde-c16d-4fa6-8573-9f91438cdb8e?hidden=true) — sign in: **ReviewOwner**
- [Audit including hidden events](http://127.0.0.1:5310/Admin/Audit/Index) — sign in: **ReviewOwner**
- [Plain Admin audit excludes hidden events](http://127.0.0.1:5310/Admin/Audit/Index) — sign in: **ReviewAdmin**

## Schedule / Signup setup

- [Private setup — schedule](http://127.0.0.1:5310/Admin/Events/Schedule/ba6f76f6-12d0-46dd-b6af-9e1d4aa8a123) — sign in: **ReviewAdmin**
- [Private setup — questions](http://127.0.0.1:5310/Admin/Events/Questions/ba6f76f6-12d0-46dd-b6af-9e1d4aa8a123) — sign in: **ReviewAdmin**
- [Signups open — schedule](http://127.0.0.1:5310/Admin/Events/Schedule/47be3330-f281-45aa-b3ba-01706c92a277) — sign in: **ReviewAdmin**
- [Signups open — questions](http://127.0.0.1:5310/Admin/Events/Questions/47be3330-f281-45aa-b3ba-01706c92a277) — sign in: **ReviewAdmin**
- [Signups closed — finalized affiliated rosters — schedule](http://127.0.0.1:5310/Admin/Events/Schedule/a700bb29-a098-488b-8be8-d7abb3353cbb) — sign in: **ReviewAdmin**
- [Signups closed — finalized affiliated rosters — questions](http://127.0.0.1:5310/Admin/Events/Questions/a700bb29-a098-488b-8be8-d7abb3353cbb) — sign in: **ReviewAdmin**

## Overview / Participants / Teams

- [Live — published board correction — overview](http://127.0.0.1:5310/Admin/Events/Manage/978a811a-57dc-4395-b2de-9c8d6a038212) — sign in: **ReviewAdmin**
- [Live — published board correction — participants and former members](http://127.0.0.1:5310/Admin/Events/Participants/978a811a-57dc-4395-b2de-9c8d6a038212) — sign in: **ReviewAdmin**
- [Live — published board correction — finalized affiliated rosters](http://127.0.0.1:5310/Admin/Events/Draft/978a811a-57dc-4395-b2de-9c8d6a038212) — sign in: **ReviewAdmin**
- [Signups closed — finalized affiliated rosters — overview](http://127.0.0.1:5310/Admin/Events/Manage/a700bb29-a098-488b-8be8-d7abb3353cbb) — sign in: **ReviewAdmin**
- [Signups closed — finalized affiliated rosters — participants and former members](http://127.0.0.1:5310/Admin/Events/Participants/a700bb29-a098-488b-8be8-d7abb3353cbb) — sign in: **ReviewAdmin**
- [Signups closed — finalized affiliated rosters — finalized affiliated rosters](http://127.0.0.1:5310/Admin/Events/Draft/a700bb29-a098-488b-8be8-d7abb3353cbb) — sign in: **ReviewAdmin**
- [Archived — affiliated roster history — overview](http://127.0.0.1:5310/Admin/Events/Manage/e1cf3078-d8a9-4ea4-a5db-cc6cfcafb4f0) — sign in: **ReviewAdmin**
- [Archived — affiliated roster history — participants and former members](http://127.0.0.1:5310/Admin/Events/Participants/e1cf3078-d8a9-4ea4-a5db-cc6cfcafb4f0) — sign in: **ReviewAdmin**
- [Archived — affiliated roster history — finalized affiliated rosters](http://127.0.0.1:5310/Admin/Events/Draft/e1cf3078-d8a9-4ea4-a5db-cc6cfcafb4f0) — sign in: **ReviewAdmin**

## Public board / affiliated teams

- [Live — published board correction — public teams](http://127.0.0.1:5310/Events/ur-current/Teams) — sign in: **ReviewParticipant**
- [Live — published board correction — public board](http://127.0.0.1:5310/Events/ur-current/Board) — sign in: **ReviewParticipant**
- [Signups closed — finalized affiliated rosters — public teams](http://127.0.0.1:5310/Events/ur-signups-closed/Teams) — sign in: **ReviewParticipant**
- [Signups closed — finalized affiliated rosters — public board](http://127.0.0.1:5310/Events/ur-signups-closed/Board) — sign in: **ReviewParticipant**
- [Archived — affiliated roster history — public teams](http://127.0.0.1:5310/Events/ur-archived/Teams) — sign in: **ReviewParticipant**
- [Archived — affiliated roster history — public board](http://127.0.0.1:5310/Events/ur-archived/Board) — sign in: **ReviewParticipant**

## Board / Review

- [Published board with exceptional working-copy correction](http://127.0.0.1:5310/Admin/Events/Board/978a811a-57dc-4395-b2de-9c8d6a038212) — sign in: **ReviewAdmin**
- [Pending evidence queue](http://127.0.0.1:5310/Admin/Review/Index?eventId=978a811a-57dc-4395-b2de-9c8d6a038212) — sign in: **ReviewAdmin**
- [Blocked approval — approve or reject the earlier upload first](http://127.0.0.1:5310/Admin/Review/Details/13424662-37b1-463d-b19d-7cb276024794) — sign in: **ReviewAdmin**

## Final review / WOM

- [WOM end update Pending](http://127.0.0.1:5310/Admin/Events/WiseOldMan/978a811a-57dc-4395-b2de-9c8d6a038212) — sign in: **ReviewAdmin**
- [WOM end update Rejected — archived history](http://127.0.0.1:5310/Admin/Events/WiseOldMan/e1cf3078-d8a9-4ea4-a5db-cc6cfcafb4f0) — sign in: **ReviewAdmin**
- [WOM end could-not-update — hidden Archived overview](http://127.0.0.1:5310/Admin/Events/Manage/cfc33cde-c16d-4fa6-8573-9f91438cdb8e?hidden=true) — sign in: **ReviewOwner**

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
- [ReviewSecondMember — participant](http://127.0.0.1:5310/Account/MyEvents) — sign in: **ReviewSecondMember**

## Captain / Co-captain / Participant

- [Captain evidence workspace](http://127.0.0.1:5310/Submissions?eventId=978a811a-57dc-4395-b2de-9c8d6a038212) — sign in: **ReviewCaptain**
- [Co-captain evidence workspace](http://127.0.0.1:5310/Submissions?eventId=978a811a-57dc-4395-b2de-9c8d6a038212) — sign in: **ReviewCoCaptain**
- [Participant evidence ledger](http://127.0.0.1:5310/Submissions?eventId=978a811a-57dc-4395-b2de-9c8d6a038212) — sign in: **ReviewParticipant**
- [Former member history](http://127.0.0.1:5310/Account/MyEvents) — sign in: **ReviewFormer**

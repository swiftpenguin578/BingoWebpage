# OSRS Community Bingo Platform

## Product Requirements Document

**Status:** Current approved product requirements; pending implementation is labelled by ticket
**Last updated:** 2026-10-02 (whole-branch decision reconciliation; clarified summary approved)
**Audience:** Community bingo organizers, reviewers, captains, and developers

## Participants backend changes — approved 2026-09-30

This bounded follow-up supersedes conflicting Participants rules below. The user
has authorized backend implementation now, while the new reference UI, tokens and
components are developed separately. UI integration and manual acceptance are
explicitly deferred; automated behavior checks and independent review are required.

- An enabled Admin can manage the pre-team-draft signup pool in event Draft,
  SignupOpen or SignupClosed while the team draft is unlocked. Event Draft is not
  team-draft start. Existing hidden/discarded, ownership, visibility and separate
  retained-history/direct-roster boundaries remain protected.
- Explicitly confirm a selected waiter. If capacity is available, use it. When
  full, explicit confirmation to add a place increases capacity by exactly one and
  confirms only that participant. Ordinary capacity increases still promote the
  queue normally; the selected-person operation is a distinct exception.
- Move a confirmed participant to the queue's end only when full and another
  eligible waiter exists; promote that pre-existing waiter and never immediately
  re-promote the moved participant. End any current membership/leadership authority
  with retained history. The future confirmation must disclose team removal.
  Reject this operation when places remain or no eligible waiter exists.
- Keep normal capacity-driven restore with a new end-of-queue sequence. Add an
  explicitly confirmed full-event restore-and-add-one-place alternative for the
  selected participant only. Do not permit forced Waiting while a place is open.
- Admin Add uses an existing active website account and at least one of its active
  saved Playing account links, up to configured event Playing slots. Select the
  primary from those accounts, use stored EHB without WOM calls, select Paid/Unpaid
  (default Unpaid), and commit placement/payment/accounts together. No signup code,
  custom questionnaire, captain/co-captain or notes are required in this Add flow.
  Captain volunteering defaults to No; other missing answers are not fabricated.
  Placement uses normal capacity or the explicit full-event add-one-place override.
- Before team-draft lock, allow changing which current Playing account is primary.
  Exactly one supplies draft EHB; preserve each account's own EHB and all other
  accounts. Additional accounts remain bounded by configured event account slots.
- Admin event-account corrections affect event assignments/EHB only. Do not mutate
  the member's saved account links, names, preferred order or saved EHB. Editing
  an event account is not restricted to the saved-only selection used by Add.
  Shared global character links remain allowed; event-local conflicts remain errors.
- Captain answers stay boolean Yes/No. Pre-draft withdrawal still automatically
  promotes the next eligible waiter; no suppress-promotion option is introduced.
  Payment and private notes retain their broader existing administration window.
- Preserve finalized pre-Live roster Add/Remove as a separate existing workflow and
  the permanent first-Live membership/registration lock. Do not revive Live
  withdrawal/replacements, ownership transfer, or accountless creation.
- Toast Undo and general manual queue reordering are excluded. Display precision
  differences in the reference do not authorize stored-EHB rounding changes.

The implementation scope, checks and deferred UI boundary are in
[the Participants backend pass](DELIVERY_PLAN.md#participants-backend-pass--approved-2026-09-30).

## UI review decisions — approved 2026-10-02

These later user decisions supersede conflicting current-only eligibility and
pending-decision notes below; implementation remains queued, not complete.

- **Ranking (AU12):** EHB-before-score-time applies to new events only. Existing
  events retain their prior ranking rule, including those without official results.
  Official snapshots remain unchanged. Define and persist the activation boundary
  during implementation; do not infer it from this document's date.
- **WOM option 1 — subsequently approved:** external connections can disconnect
  before first Live and be replaced before/during Live with a validated matching
  event window, regardless of whether a management code has been stored. A stored
  or rejected code is not a reason to forbid local unlinking. Temporarily block
  while a provider operation is active or unresolved; safely retire the old local
  management connection so no stale write targets it. Never delete the external
  competition, and never carry its credentials to a replacement. Credential-enabled
  provider updates already exist. Conflict overwrite/re-send and scheduled-slot
  relaxation are not approved. Implementation remains pending under AU20.
- **Exact WOM window — approved 2 October:** replace the five-minute matching
  tolerance with equality of configured start and end UTC instants at every stage,
  including Final Review (WA-2). Actual instants remain eligibility/cutoff/review
  inputs; the later pending-AU20 early-end/Resume rules below apply. Different timezone
  displays of the same instant match; an actual time difference does not. Do not
  silently change website dates to fit WOM. Apply consistently at linking,
  replacement and existing window-validation boundaries. Reconcile provider and
  persistence timestamp precision explicitly, without rounding away genuine time
  differences. Existing links must not be silently disconnected or their historical
  snapshots rewritten. Application and reference updates are pending in AU20/RC09.
- **Evidence correction (AU17a):** Admin metadata correction must offer all players
  from the event pool, not only currently active Playing assignments. The concrete
  use case is correcting attribution when someone forgot to change the account.
  Do not retain the current-only restriction merely because the existing service
  enforces it. Resolve player/character identities and team attribution explicitly
  at ticket readiness, including retained/non-current pool entries; do not silently
  reinterpret the pool as only current Playing accounts or all website users.
  Preserve reason, audit, pending-correction/version rules and original evidence.
  This corrects evidence attribution; it does not rewrite saved accounts, rosters
  or grant participant access. Correct then approve remains two separate actions.

## Approved Admin simplification target — 2026-09-26

These are the current simplification requirements, with later approved refinements
identified by ticket and implementation status. They are implementation targets; retained historical
records and currently deployed behavior are not silently converted. The detailed
ticket/acceptance contract is linked from
[the delivery plan](DELIVERY_PLAN.md#admin-simplification--approved-2026-09-26).

- **Ownership and creation (EVT-01/LIF-01):** Create asks for name/timezone
  (Europe/Copenhagen default) and atomically creates a private Draft, permanent
  collision-safe slug, built-in signup structure and empty 5×5 board. Identity owns
  name, optional description, buy-in and timezone; Schedule owns the five dates.
  Creation retries use an actor-scoped durable request key: the same key and trimmed
  name/timezone return the original result; changed input conflicts, while a new key
  may create another same-name event. Check again reads that key under current Admin
  and event visibility permissions, never by name.
  Name/description/buy-in remain editable through Live/Final Review; timezone only
  before first Live, preserving UTC instants. Identity saves merge untouched fields
  with current values and reject different same-field edits until explicitly
  resolved against reviewed current values. A newer same-field change invalidates
  that resolution. A timezone review becomes stale independently when its current
  schedule consequences change; review and confirm the fresh preview before saving.
  Retire future banner editing and
  `PublicRules`, `PrizeDescription`, `ExpectedTeamCount`, `ExpectedBoardRows` and
  `ExpectedBoardColumns` use; keep `BuyInDescription` and Board `ExpectedTeamSize`
  as a manually adjustable planning estimate even after roster finalization
  (AU13 approved, pending). Actual rosters never silently replace this estimate.
- **Lifecycle (LIF-01/RES-01):** Draft → Signup Open → Signup Closed → Live →
  Final Review → Archived. Future opening time schedules opening; manual actions
  supersede their corresponding scheduled action without cancelling unrelated
  transitions. Actual transitions remain history. Draft time freezes at draft
  start; event start stays editable until actual Live. Manual start needs one
  confirmation, no reason; Live end edits require a reason. Early end gives
  30 minutes' upload grace from actual end. Resume adds a prospective Live interval
  and preserves the gap. Keep evidence codes, exceptional upload reopening,
  current-event exclusivity and quarantine. Present disposable removal as Delete
  event and protected-history retention as Cancel event. No typed quarantine
  confirmation or mandatory restore reason.
- **Identity/security (SEC-01/ACC-01/TEM-01):** Retire Emergency Captain login,
  sessions, tokens, recovery, operations, readiness and worker authority. Historical
  actors remain readable. New participation uses an existing active user-created
  website account; remove accountless/external creation, application CSV roster
  import and participant-owner transfer. Keep separate operator historical import.
  Accounts supports existing normal accounts, role/disable/restore/reset and
  Super Admin ownership transfer; no Create User, delete, merge or Admin editing
  of username/Discord/My Accounts. Preserve role hierarchy, immediate session
  invalidation and 60-minute single-use superseding reset links. Global ownership
  transfer remains distinct from retired participant-owner transfer.
- **Teams/draft (TEM-01/DRF-01):** `IncludedInDraft` alone decides draft inclusion;
  affiliation is descriptive. With 2+ included teams, use Setup → Running →
  Finalized, derived balanced sizes and active normal-account Captains at actual
  draft start/finalization. With 0/1 included team, manually assemble then Finalize
  roster without website-draft-only blockers or fabricated Running/picks. Both
  methods validate roster integrity and satisfy Board/WOM/start readiness. Live
  start no longer requires a Captain/emergency credential. Remove Pause/Resume
  and finalized Reopen. Cancel a private Running draft only after explicit latest-
  pick undos leave zero active picks; cancellation never bulk-undoes picks.
- **Signup (SGN-01/SGN-02/PAR-01):** Questions can change while open or closed
  before draft; after first signup, new questions are optional and optional cannot
  become required. Delete/create replaces format conversion; answer/account-field
  deletion confirms impact and releases only affected event registrations. First
  Playing field is required/protected, extras optional; zero Alt fields is valid
  and all configured Alt fields are optional. Keep required Captain Yes/No and
  optional volunteer co-captain text. Capacity is editable before draft but never
  below Confirmed count; increases promote earliest eligible waiters atomically.
  Waiting is always enabled while open. Code gates first signup and self-rejoin,
  not edits, withdrawal or Admin additions; rotating it affects future admissions
  only. Ordinary corrections preserve queue/status. Pre-draft withdrawal releases
  membership/registrations and promotes a waiter; Admin restore uses current
  capacity/end-of-queue. Self-rejoin is open-only. Running draft locks participant
  and roster edits; private payment/notes retain their permitted administration.
- **Finalized rosters (ROS-01):** Before first Live, separate Add and Remove
  operations update current publication while preserving earlier versions and
  original picks. Add selects an existing account, reuses event identity and
  requires the first Playing account; questionnaire may be blank. No signup code,
  signup cap, final team-size cap or automatic rebalance. Failed Add leaves a prior
  Remove committed. First actual Live permanently locks registration/membership,
  including after early end. Remove future Live replacements, vacancies,
  departures and promotion follow-ups. Account disablement removes access, not
  the participant from the fixed roster.
- **Evidence (EVD-01/EVD-02):** Participants submit for self; current Captains/
  Co-captains for their own team. Global Admin adds no submission authority;
  review/private-evidence access stays separate. Only the participant switches
  Playing account, immediately at server time with serialized attribution capture.
  All-Playing WOM aggregation is unchanged. Preserve immutable original submission
  time/account. Replace special Resubmit with ordinary new attempts; keep old links
  readable and Request Changes inactive. Pending owners retain allowed edits,
  screenshot replacement/withdrawal within the window. Approve is one click;
  Reject/Reverse require reason and one confirmation. Pending Admin metadata
  correction requires reason, never screenshot replacement or invented time.
- **Results (RES-01):** Closed upload window, zero Pending and valid calculated
  placements are mandatory. Publish an immutable official version and Archived
  state atomically; remove separate Archive, manual completion corrections,
  inspection/override and tie acknowledgement. Use all existing competitive inputs
  in priority order. Shared rank is valid only for exact equality across every
  intended input; no discretionary or name/ID/GUID tiebreak. Preserve numbering
  and old official versions. Exceptional reopen requires reason/confirmation and
  creates a new correction cycle/version.
- **Board/catalogue (BRD-01/BRD-02/CAT-01):** Preserve leases, immutable approval/
  evidence identities, derived descriptions, catalogue EHB, manual challenge EHB
  and homogeneous objective rules. Approval validates/freezes without confirmation;
  competitive edits invalidate unpublished approval. Publication requires finalized
  roster and one confirmation; corrections retain the current public snapshot
  until replacement. Protect occupied cells on shrink and dependencies on removal.
  Use plain counting language without changing mechanics. Lightweight overview and
  on-demand editors keep Draft estimates current with affected-only invalidation;
  approval always recalculates authoritatively. Retire Admin roll-group editing and
  application bulk/Wiki import surfaces while preserving domain/operator mechanics.
- **WOM (WOM-01/WOM-02):** Website dates own the schedule; no provider-date import
  or Admin sync toggle. Compare both linked-window boundaries as equal UTC instants, without a
  five-minute tolerance (later user decision above; implementation pending). Website-created/protected-credential connections allow sync and
  eligible pre-Live deletion; external ID-only links allow reads only, including
  no update-all writes; external links with protected supplied code allow sync but
  never deletion. Credential adoption never changes provenance. Validate without
  mutation if supported, otherwise on the first legitimate operation with honest
  unverified status; never redisplay secrets. Preserve anchored schedules,
  cooldowns, retry/unknown outcomes and no deletion after ever Live. Dedicated WOM
  owns operations; Overview summarizes/links. Provider failure does not block valid
  lifecycle transitions. No manual update-all feature. Manual fetch uses a normal
  Fetch now action without a typed FETCH challenge or confirmation dialog (user
  decision, 2 October 2026; AU15 pending). Preserve all server-side eligibility,
  successful-fetch cooldown, scheduled-slot/retry timing and in-flight protection.
- **Actions/feedback (ADM-01/ADM-02/ACT-01):** Shared confirmations and explicit
  outcomes follow `UI_SYSTEM.md` and `FUNCTIONAL_CONTRACTS.md`. Pending evidence and
  genuinely unresolved scheduled opening/start failures drive Admin actions and
  directory Needs attention; historical failures alone do not. Remove missing-
  Captain/vacancy/promotion-follow-up actions. Personal read state resolves no
  business condition. Dashboard was outside the original simplification; its separately approved backend
  and reference scope is defined in the Community Dashboard section below.

## Events directory data — approved AU04, 2026-10-02

The Admin directory exposes All, Current/upcoming (Setup, Signups open/closed,
Live), Past (Final review, Finished, Archived, Cancelled), name search, phase
filtering, actionable-only `attention=1` filtering and column sorts. Hidden events are a separate SuperAdmin-only population, excluded
from ordinary rows and counts. Discarded events never appear.

Default order is Live first, preparation by scheduled start (signup opening,
then closing when start is unset), unscheduled last, then one newest-first past
population across phases. Past uses actual end, falling back to the relevant
finalized/archive/scheduled end; Cancelled uses cancellation time. Missing dates
sort last and equal keys use stable event ID. Retained participation for Live and
past reuses Dashboard membership/import identity rules, including departed people;
missing actual intervals remain unavailable. Preparation uses confirmed/waiting
counts and capacity stays nullable, including imported events.

Needs attention prioritizes unresolved scheduled failure (start, then opening),
then pending review. Its +N counts additional issue categories, with the whole
review queue counting once. Existing shared inbox counts remain per submission
plus each failure. Ordinary unfinished setup is not attention. No new failure
subsystem is authorized. Query contracts are in scope; visual layout, filter URL/
navigation integration and manual acceptance remain deferred.

## Community Dashboard — approved backend scope, 2026-10-01

The Admin Dashboard presents community statistics between events. Its totals,
participation chart and history include accessible non-hidden Live, final-review,
finalized and archived events; cancelled/discarded events are excluded. Live and
final-review figures are Provisional and may change. The latest-event recap uses
only the latest ended event; official winners require the active official
finalization, with shared first place preserved. The compact event card selects
Live, then the next scheduled preparation event, then unscheduled setup.

People count once per qualifying event participation, despite multiple game
accounts or team moves. Website identity determines unique/returning people;
imported/unlinked records count event participation without invented identity.
Disabled website accounts retain their historical participation and count in
registered totals; emergency credentials do not. Returning means an earlier
actual-start cohort, not just the previous displayed event.

Approved submissions exclude reconstructed imported contributions. EHB is stored
period gain with truthful availability/account coverage, not signup EHB; loading
Dashboard never requests provider synchronization. Unknown is distinct from zero.
Community new-account figures use the latest actual event end, or the last 30 days
without an ended event; login figures use stored LastLoginAt and one request clock.

This authorizes backend preparation under DELIVERY_PLAN.md, not production UI
replacement. Earlier Dashboard exclusions apply to their earlier assignments.

## 1. Product summary

The product is a private-purpose website for running Old School RuneScape bingo events for one Discord community. The community normally holds two or three events per year. The website replaces the current Google Sheets board and Discord evidence-submission workflow while leaving Discord as the community's communication platform.

An event may also include invited teams from outside the community, such as another clan in a clan-versus-clan bingo. Those teams may arrive with an internally selected roster and do not have to participate in the website draft.

The website will manage:

- Public event boards and live team progress
- Teams, rosters, captains, and co-captains
- Evidence submission and admin review
- Rankings, placements, and player contribution statistics
- Bingo board creation and EHB balancing
- Event drafting
- Historical events and evidence

The website is not intended to be a reusable public platform for unrelated Discord communities in version one.

## 2. Product goals

1. Make every team's official progress easy to follow in one place.
2. Make drop submission fast for captains and co-captains.
3. Give admins a clear review queue and safe correction tools.
4. Calculate tile, line, board, and ranking progress automatically.
5. Support the varied drop objectives used in real OSRS bingo boards.
6. Preserve previous events, results, rosters, boards, and approved evidence.
7. Help organizers estimate and balance boards using EHB.
8. Run the team draft on the website, initially under admin control.

## 3. Out of scope for version one

- A multi-community or public event-hosting platform
- Discord role, channel, or message automation
- Automated prize distribution
- RuneLite integration
- Automated verification of in-game drops
- Captain strategy tools, assignments, or private team notes
- Fully captain-operated drafting with automated turns and timers
- Automatic import being the only way to maintain OSRS data

These remaining features may be reconsidered after the first event has been run successfully on the website. Discord authentication and participant accounts were moved into the active Planning Pass 2 functional expansion.

## 4. Expected scale

A representative event has:

- 6 teams
- 14 participants per team
- Approximately 84 participants
- Usually a captain and co-captain for each team
- A 5x5 or 6x6 board
- A duration ranging from an extended weekend to approximately six days

The architecture should comfortably support modest growth, but administrative simplicity and reliability are more important than internet-scale optimization.

## 5. Roles and permissions

**Planning Pass 2 identity direction:** The website account is the durable person/actor identity. Public username/password remains its required authentication method after onboarding; an optional current Discord association provides another method. Event participation and OSRS character names remain separate records. One website account may participate once per event and in many different events; it may register several OSRS characters, and the same character may be linked to several website accounts because borrowing and swapping are legitimate. Within one event, however, each OSRS character is assigned to exactly one participant. Never infer website authority or exclusive ownership from an OSRS character name.

### 5.1 Public visitor and participant

Public visitors do not need accounts and retain read-only access to public information.

Public visitors can:

- View the current event, rules, dates, and status
- View the event's signup board and selected public fields before draft finalization; afterward non-admin requests for that route redirect to the published team-roster view
- View every team's board and approved progress
- View tile details and publicly visible approved screenshots
- View teams, rosters, captains, and the draft
- View team and player leaderboards
- View previous events and final results

Public visitors cannot:

- Submit evidence
- View pending or rejected submissions
- Change any event data

Initial website-account creation requires Discord authentication, but Discord-server membership is not required. Onboarding adds a required password. Returning users may authenticate with Discord or public username/password, and either method opens the same website account. That account owns at most one event-participant record in the event. Participants manage their signup through the account only while signup is open and do not receive a private edit link for a new normal signup.

The first account journey after Discord authentication requires a unique website username, password, and first OSRS character. The page may recommend using the primary OSRS character as the website username, but the values are independent and need not match. Exact-spelling guidance applies to the OSRS-character field; the application trusts that entry and does not verify OSRS syntax, current availability, ownership, Wise Old Man membership, or existence. It trims surrounding whitespace and compares normalized values case-insensitively for their separate uniqueness boundaries without otherwise rewriting submitted spelling. The username becomes both the public site label and password-login username, while the separately entered character is created/linked in position 01 and is therefore preferred in My accounts. Neither the username nor link proves character ownership. Public-username uniqueness does not prevent another website account from linking or legitimately borrowing the same OSRS character; it only prevents two public profiles from presenting or logging in with the same username.

A participant may later change their public username to any valid value if the normalized public name is available. The username remains independent of linked OSRS characters and is never the event-facing participant name; event views use registered OSRS characters. A successful rename updates the current session without rewriting event assignments or invalidating unrelated sessions solely for the name change.

An authenticated participant can:

- View and manage their own open signup
- View their event and team access
- Submit evidence for themselves when the event/evidence workflow allows it
- Open a normal authenticated `/Submissions` ledger containing the complete retained history of their current authorized team, including records credited to departed teammates
- Open `/Submissions/{id:guid}` for any retained submission in that current team; only the credited owner may edit pending evidence, replace its active screenshot, or withdraw through cutoff, while every other state and teammate-owned row is read-only. A later attempt after rejection or reversal is an ordinary new submission, not a linked correction.
- Read team-visible rejection feedback and retained evidence assets when currently authorized; replaced screenshot assets remain retained history
- Manage a flexible global **My accounts** list with optional personal labels, saved per-link EHB defaults, and ordering whose first active character is the sole preferred character
- Register several available characters for an event while keeping exactly one active and drop-eligible at a time
- Answer additional admin-configured Account or support-alt questions when present
- Swap without limit among the playing accounts locked into that event signup
- Withdraw through a separate confirmed action after signup closes and before the draft begins

New Admin additions and roster assembly require an existing active website account. Accountless/external records remain readable historical data or an explicitly authorized operator import; there is no new application accountless/CSV/participant-owner-transfer workflow.

### 5.2 Captain and co-captain

Captain and co-captain are event/team roles on the participant's existing website account. Assigning or removing the role changes permissions without creating a separate normal login and without depending on an OSRS character name or current Discord ID.

They can:

- Before/during the website draft, view the confirmed draft-pool signup table with participant-submitted answer columns expanded
- Submit evidence for their own team through exactly the same team-board tile drawer/interface used by ordinary members
- Credit the drop to a player on their team through that shared submission flow
- View a complete team submission ledger covering pending, approved, rejected, withdrawn, replaced, and other retained historical states
- Filter that ledger by status, player, and tile, open submission details, and read reviewer feedback
- See pending, rejected, and approved summary counts without changing the underlying submission states
- Edit their team's pending submissions
- Withdraw their team's pending submissions
- Read admin feedback on their team's submissions
- Select or clear tiles, rows, and columns as non-authoritative team focus that is visible to the whole current team on its normal board

They cannot:

- Submit for another team
- See another team's pending or rejected submissions
- Approve or reverse submissions
- Directly change official tile progress
- Edit the event, board, catalogue, roster, or rules

Captain and co-captain have the same website permissions. Their expanded draft table includes participant-submitted answers hidden from the public board but excludes paid/unpaid status, private admin notes, identity-recovery/security data, and audit history. Draft-pool access follows current team role and draft inclusion; legacy formation type grants no special authority.

Captain and participant submission workspaces are one canonical implementation.
`/Submissions` is the authenticated team submission overview and
`/Submissions/{id:guid}` is its detail route. The overview is team-wide for every
authorized current member. Captains/co-captains see the two Captain-only top
sections—team focus and team submission status—and retain server-authorized
broader editing of eligible team submissions; ordinary participants do not see
those sections and may edit only their own eligible non-read-only submissions.
The detail composition remains visually equivalent to the approved Captain
submission detail. Submission starts from the ordinary team board and uses its
existing drawer/interface; submission review, approval, rejection, reversal,
and other reviewer controls remain Admin-only.

The `/Captain/Submit/{tileId?}` route is retained only as the shared drawer's
transport/handler endpoint and as a compatibility redirect for old direct
links. It is not a rendered submission page or a no-JavaScript acceptance
surface. Legacy `/Captain` and `/Captain/Submissions/{id:guid}` are thin
compatibility redirects/aliases to `/Submissions` and `/Submissions/{id:guid}`;
they are not separate rendered implementations. Cross-role legacy links must
resolve through the canonical route and authoritative server authorization.
Personal submission/evidence notifications, including those received by
Captains/co-captains, resolve to `/Submissions/{id:guid}`; relevant general
submission navigation resolves to `/Submissions`. Admin review notifications
remain `/Admin/Review/Details/{id}`. `/Evidence/{id}` is a protected
file-download handler consumed by evidence views, not a rendered page family.

At draft finalization, the signup page remains a signup page and stays available to enabled administrators for historical and operational use. Public, participant, and captain requests for that route redirect to published team rosters; roster pages never expose the old signup answers, including a team's expanded draft answers.

After event end and before submission cutoff, captains/co-captains may continue creating and editing evidence submissions for in-window drops, for members of their authorized team, preserving retained historical attribution. Submission cutoff closes those mutations unless an admin reopens submissions.

Team focus may target a tile, full row, or full column. It is shared by that team's captains, visible to current team members, and invisible to opponents, the public, and ordinary admins who are not team members. A participating Super Admin sees their own team's focus normally. Another team's focus stays hidden until the Super Admin explicitly enables a visibly identified, read-only inspection mode for that team; the data is not loaded before opt-in and the choice is not retained as a persistent show-all preference. Super Admin status never grants focus editing without the target team's captain/co-captain role. Focus has no effect on official board state, evidence, progress, or ranking.

### 5.3 Admin and reviewer

Admins use permanent accounts. An Admin or Super Admin may also participate in an
event or captain/co-captain a team. The global role is additive: when the same
account has a genuine event Participant, Captain, or Co-captain role, participant
navigation, team visibility, and submission authority follow that event role and
its ordinary lifecycle/cutoff. Global status alone grants none of those team
capabilities and never upgrades one event role into another. Admin review remains
a separate global capability, including review of the Admin's own team's
submissions.

They can:

- Create/configure events and publish official results atomically to Archived
- Cancel populated events that will not take place before live play begins
- Create teams and manage rosters
- Create teams using IncludedInDraft and manage permitted pre-first-Live rosters
- Assign, revoke, and recover website-account captain/co-captain roles
- Preserve historical emergency actor references without granting authentication or operational access
- Manage bosses, activities, items, drops, rates, and EHB values
- Build and deliberately arrange bingo boards
- Operate and publish the draft
- Submit or manage team evidence only through a genuine Participant, Captain, or
  Co-captain role; global Admin review does not impersonate a team member
- Review, approve, and reject evidence
- Correct submission metadata before approval
- Reverse approved submissions
- Start, close, reopen, finalize, and unfinalize events
- Correct placements and progress
- View the complete audit log
- Disable/re-enable normal User accounts; Admin-account access remains Super-Admin-only

Ordinary admins cannot grant or revoke the global Admin role. That owner-level action belongs only to the designated Super Admin.

Ordinary Admins cannot see or operate hidden events. A Super Admin may inspect
hidden events only through the clearly separated Hidden area of Events Control,
where the limited Manage surface shows retained lifecycle information and
hide/restore audit history. Super Admin status never bypasses hidden-event
protection on public routes or ordinary event workspaces.

Automatic Captain status and any new general cross-team submission inspection or
correction mode for Super Admin are deferred. This does not change the existing
explicit, read-only team-focus support view described below.

### 5.4 Super Admin

The product has exactly one active global owner-level role distinct from ordinary event administration. Super Admin is assigned to a normal website account and inherits ordinary Admin capabilities. The initial owner is selected during controlled setup or migration; first public signup, first login, and onboarding can never claim ownership.

Only the Super Admin can grant or revoke ordinary Admin access, and any normal website account may become an Admin. Emergency/legacy captain credentials cannot become Admin or Super Admin. These global roles remain independent of event participation, Discord-link state, and captain/co-captain membership.

Admin grant/revoke uses an explicit confirmation view naming the account and before/after role, plus automatic actor/time/target/before-after history, but no typed username or written reason. Revocation returns the target to `USER`; it does not disable the account or remove event participation, captain roles, characters, or history. Grant and revoke invalidate the target's existing authenticated sessions immediately. The current owner cannot be demoted or disabled through ordinary role management.

Super Admin ownership can move only through an atomic transfer to another active normal website account. The recipient becomes Super Admin and the former owner becomes ordinary Admin in the same transaction, preserving the invariant that exactly one Super Admin exists. Transfer requires the current owner's password and the typed destination public username, plus automatic history but no written reason. It invalidates affected sessions. Lost-owner recovery is an explicit deployment/operator procedure rather than a first-user election or second owner.

Cross-team focus access is an exceptional support view, not passive visibility. Another team's focus is absent by default until the Super Admin explicitly enables read-only inspection for that team. The UI must identify inspection clearly, keep it team/page-session scoped, avoid loading or broadcasting the data beforehand, and remove it when disabled. Editing still requires an ordinary captain/co-captain role on the target team.

## 6. Account lifecycle

### 6.1 Captain accounts

- Normal captain/co-captain access uses the participant's website account and event/team role, regardless of whether the current session used Discord or password.
- Role scope is restricted to one event and team and changes when the authoritative roster role changes.
- The same person may have different roles in different events.
- Character-name changes and active-character swaps do not alter the person's role.
- Submission cutoff and correction rules restrict allowed actions, not the underlying Discord identity.
- Emergency Captain authority is retired (SEC-01). Retained emergency accounts, access rows, tokens and actor references remain historical data: login, existing cookies, token consumption, creation, reset, enable/disable, evidence and Team Focus authority are unavailable. No conversion, deletion, fabricated human disable Audit or expiry processing accompanies retirement. Normal Captain/Co-captain authority remains membership-based. Live start has no Captain/credential prerequisite; actual website-draft start/finalization still requires current Captains. Event-specific rollout remains subject to the retained-data evidence gate.

### 6.2 Admin accounts

- Normal Admin and Super Admin access belongs to normal website accounts; emergency password credentials cannot receive either role.
- Admin access remains until revoked by the Super Admin.
- Admin actions that alter competitive data are recorded in the audit log.
- Global role grant, revoke, and ownership transfer use strong confirmation and automatic history.
- Admin revocation increments the account authorization version and invalidates current authenticated sessions.

Normal website-account disabling is separate from role revocation. An enabled Admin may disable a `USER`; only the Super Admin may disable or restore an `ADMIN`. Nobody may disable their own current account or the active Super Admin. Disable requires strong confirmation and a reason, immediately invalidates every session, and preserves all event roles, participants, characters, evidence, historical snapshots, username reservation, and Discord association. A disabled account cannot authenticate and its identifiers cannot be registered by another account. Re-enable requires confirmation and automatic history but no written reason; it does not restore authority that event lifecycle or membership rules have ended.

Version one provides neither website-account merging nor permanent website-account deletion. Development identities are removed through the approved clean reset/deployment policy rather than account-level deletion.

### 6.3 Normal account sign-in and recovery

- Every normal account is initially created through Discord, then requires a unique public username and password during onboarding.
- First-time Discord authentication creates only protected onboarding state valid for 15 minutes; no website account is persisted until the complete onboarding transaction succeeds.
- Discord and public-username/password login authenticate the same account. Changing the public username changes the password-login username and is explicitly confirmed.
- Passwords require at least 10 characters, permit passphrases and printable characters, and have no composition or periodic-expiry rule.
- A non-persistent sign-in ends with the browser session and has a 12-hour maximum ticket lifetime. **Remember me** has a 30-day absolute maximum that activity cannot extend indefinitely.
- Version one does not collect email for recovery.
- The Forgot password page is static, asks for no username, and directs the person to contact an administrator. After verifying them outside the site, an Admin may generate a single-use reset link valid for 60 minutes for a normal user; only the Super Admin may generate one for another Admin.
- A Super Admin recovers through their linked Discord account or the operator-controlled recovery procedure.
- Admins cannot inspect or choose the replacement password. Reset-link generation/use is automatically recorded without requiring a written reason, and completing a reset invalidates previous password sessions without unnecessarily invalidating a separately Discord-authenticated session.
- A person may unlink Discord at any time after fresh password confirmation and continue using public username/password. They may later link an unused Discord account, or change directly from the current Discord account to another unused one, after password confirmation and OAuth.
- Link, unlink, and change invalidate other sessions and preserve all event participants, roles, signups, My accounts links, evidence, and history. Super Admin receives an operator-recovery warning before unlinking but may proceed.

## 7. Event lifecycle

An event has the following states:

1. **Draft:** Admins configure the event, catalogue, players, teams, rules, and board.
2. **Signup open:** The event signup page is public and accepts registrations. Teams, draft results, and the bingo board remain private.
3. **Signup closed:** Registration no longer accepts submissions while admins prepare captains, teams, the draft, and the board.
4. **Live:** The approved board and finalized teams are public. Drops obtained within the event window may be submitted.
5. **Awaiting final review:** The official event/drop-eligibility window has ended. The derived 30-minute submission grace period may remain open for in-window evidence, while existing submissions can continue through review.
6. **Finalized (legacy only):** Retained older records remain readable; no new resting state.
7. **Archived:** Publishing official placements and statistics enters this state atomically; the event remains public history.
8. **Cancelled:** A populated pre-live event that will not take place is preserved without continuing scheduled or participant activity.

Hidden is an orthogonal, reversible administrative quarantine and is not an
event state. Only a Super Admin may hide or restore an event from Events Control
or the Manage Danger Zone. Hide and Restore use ordinary shared confirmation and complete audit entries.
Hide requires a reason; Restore accepts no reason. Neither requires typed name confirmation. Hiding
is allowed only when the lifecycle state is `AWAITING_FINAL_REVIEW`,
`FINALIZED`, or `ARCHIVED`; `DRAFT`, `SIGNUP_OPEN`, `SIGNUP_CLOSED`, `LIVE`,
`CANCELLED`, and `DISCARDED` events cannot be hidden.

While hidden, an event cannot transition lifecycle or use any ordinary event
workspace. It is absent from all public, participant, Captain/co-captain,
emergency-authority, and ordinary-Admin discovery, history, account,
submission, evidence, notification, action, and realtime surfaces.
Guessed or
direct event URLs return 404, including for Super Admins. The only exception is
the separated Super Admin Events Control Hidden area and its limited Manage
inspection, which exposes retained lifecycle details, quarantine audit history,
and Restore. Restore clears only the hiding metadata and returns the unchanged
lifecycle and data; it does not rewrite dates, snapshots, rankings, evidence,
history, assets, or storage. If restoring a current-state event would conflict
with another visible current event, restoration is rejected without changing
either event or the hiding metadata. Apply the same current-event definition and
development-fixture exclusions as Start/Resume; archived restoration does not
claim the current-event slot. Hide and Restore emit no notification. Hidden is
retention, never deletion, and because eligibility begins after Live, hidden
events have no active-event scheduler, signup, singleton/window-collision, or
active realtime processing.

Hiding removes unwanted events from the front page and ordinary Admin Events table;
concealing their details from Admin audit history is not a product requirement (C36,
user clarification 2026-09-14). Admin audit is an exception to the projection restriction
above; ordinary audit permissions and sensitive-data protections remain. This decision
closes the additional audit-cleanup ticket without authorizing a new source change.

This hidden-event slice adds no lifecycle enum value, deletion, global EF query
filter, ordinary-Admin visibility, public Super Admin bypass, or full
hidden-event tooling. The separately approved historical-event import remains
outside this slice, as does unrelated Admin/UI redesign.

The event does not finalize automatically. Admin confirmation is required.

Multiple Signup open or Signup closed events may exist in production when their half-open event windows `[start, end)` do not overlap; back-to-back windows are allowed. Signup is an unlisted exact-link journey, so opening it does not add the event to general public navigation or a current-event selector. Only Live, Awaiting final review, and Finalized remain singleton current states. Development fixtures use an internal persisted marker honored only by Development commands; no ordinary Admin input can enable it, and Production never honors it.

Official publication atomically enters Archived and previous-event history while preserving public URLs and immutable versions. There is no separate Archive action. Legacy Finalized records remain readable. Reasoned reopening is blocked if another production current event exists.

Discard remains restricted to empty experimental events. A populated event that has never entered Live may instead be cancelled after strong confirmation and a required reason. Cancellation is terminal through the ordinary UI, closes signup and all event-scoped mutations, suppresses scheduled transitions, and preserves all records. It never publishes private content. If the event was already public, its existing route shows a generic cancellation status while the written reason remains private admin/audit information. Live events use early end and finalization instead.

### 7.1 Important timestamps

Each event records:

- Scheduled signup opening, when automatic opening is used
- Signup closing
- Optional informational draft time
- Scheduled start
- Scheduled event end
- Internal normal submission cutoff, derived from event end
- Actual start, if manually controlled
- Authoritative actual/effective event end
- Actual submission closure
- Finalized time

Schedule owns the five timestamps, not capacity or an automatic-opening toggle. Unchanged stored instants preserve exact precision. Changed local values follow the existing future/timezone/order rules; passed signup boundaries remain history, draft time freezes at actual draft start, and event start/end may be repaired to future values until first actual Live even after their configured times pass. Published start/end cannot be cleared. After Live, a future end change requires reason and one consequence confirmation. Participant-facing changes use one meaningful before/after confirmation; draft-time-only and ordinary private saves do not gain a confirmation ladder. AU10 preserves exact unchanged instants; exact WOM boundary validation is pending AU20.

Admins can reopen submissions or undo finalization. These actions require a reason and create audit entries.

Schedule is optional when saving an initial private draft. A private draft may save a partial schedule: every supplied or future value is validated immediately, and a configured future automatic opening requires a future signup closing time with valid pairwise chronology and any complete event window must satisfy the operational overlap rule. Before either manual or scheduled signup opening, the event requires a future signup closing time plus valid event start and end times and the other opening-readiness prerequisites. Event end must follow event start, and signup closing cannot be later than event start.

A future opening timestamp schedules opening. Manual Open/Close/Reopen uses one confirmation showing the actual consequence and close time, with authoritative readiness rechecked on submission; no separate Prepare, warning-acknowledgement or proposed-close-acceptance workflow. A manual action supersedes its corresponding scheduled transition without suppressing unrelated future transitions. Reopen uses a valid future close or establishes a replacement while retaining prior actual transitions.

Submission cutoff is internal lifecycle data: whenever a normal event end is assigned or changed, the system derives it as exactly 30 minutes later; without an event end it remains unset. It is not an independently configurable or ordinarily displayed event setting. A separately approved submission reopening retains its own cutoff. Draft time is optional planning information and never starts the draft automatically; an admin explicitly starts it.

The configured event-start instant may trigger an automatic start attempt, but never bypasses readiness. If the draft is not finalized, the board is not published, another start invariant fails, the event remains pre-live and displays **Automatic start postponed** with the exact blockers. The scheduled instant remains historical and live eligibility is never backdated. Clearing the blockers does not trigger a delayed automatic retry; an admin uses **Start event now** with strong confirmation. Manual start requires confirmation only, whether it is before, at, or after the configured instant; no written reason is required.

The event ends automatically at its configured event-end instant and enters `AWAITING_FINAL_REVIEW`. If processing occurs late because the application was unavailable, the configured instant remains the effective end. An enabled administrator may end a live event early after strong confirmation and a required reason; the early confirmation time becomes the authoritative actual end. Early end sets the normal upload cutoff to actual end plus 30 minutes.

**Approved, pending AU20:** early end also sets the configured end and requested
WOM end to the precise click instant rounded up to the next whole minute; actual
end retains the precise click. Resume requires the Admin's validated replacement
future configured end, with no click-time rounding. Both actions succeed locally
without waiting for WOM. Compare WOM exactly against configured start/end at every
stage including Final Review; actual times still own eligibility/cutoff/review.
The AU20 ticket owns retry, post-end fetch suppression and publication fallback;
these changes are not yet implemented.

Before official finalization, an enabled administrator may resume an event that entered `AWAITING_FINAL_REVIEW` prematurely, whether the end was manual or automatic. Resume requires strong confirmation and a written reason. Current implementation reuses a future configured end and asks for a replacement only when missing/expired. **Pending AU20**, Resume instead requires the Admin to choose a validated replacement future end as specified above. The action returns the event to `LIVE` only after the ordinary singleton-current and lifecycle checks pass. The prior end transition and its effective time remain immutable history rather than being erased. The ordinary submission cutoff is re-derived from the applicable configured end, and any separate submission-reopening window must be revalidated rather than silently reused. Submissions, reviews, and contributions created during the intervening final-review period remain historical and continue through their normal workflows. Resume is unavailable directly from `FINALIZED` or `ARCHIVED`; resuming an event that has already produced official finalization history is outside this approved recovery action and fails closed unless separately approved.

### 7.2 Post-cutoff behavior

The normal submission cutoff provides a fixed 30-minute evidence-upload grace period after the event end. Only drops obtained during the official event window are valid, even when their evidence is submitted during this grace period.

The application validates immutable server submission time against the active upload cutoff. The screenshot itself is the evidence of when the drop was received: administrators visually compare its clan-event plugin UTC timestamp with the authoritative event end and relevant active-character history. The application does not request a second typed drop time, use OCR, or encode a fixed “upload within N hours” rule.

Admins can manually reopen submissions after the event or submission cutoff. Reopening requires an optional new cutoff, a reason, and an audit entry. Reopening submission access does not extend the valid in-game drop window unless an admin separately changes the official event end.

After the submission cutoff:

- Drops obtained after the official event end are invalid.
- Captains cannot submit newly obtained drops.
- Evidence submitted before the cutoff remains reviewable.
- Public boards remain visible and are marked as awaiting final review.
- Admins finalize the event after resolving relevant submissions.

### 7.4 Final-review checklist behavior

Final Review requires closed uploads, zero Pending submissions and valid calculated
placements. Every blocker links to its actual correction; no inspection checkbox,
tie acknowledgment, manual completion correction or override can bypass a gate.
Publish uses one confirmation, writes an immutable official version and Archived
state atomically. Optional WOM refresh failure does not block publication. Reopen
requires a reason and confirmation, preserves old versions and does not reopen uploads.
Current competitive-input/version checks reject stale actions. Exact ties remain
shared without discretionary resolution; AU12 changes ordering for new events only.

### 7.3 Event creation and setup

An enabled admin creates the event before signups, drafting, or board publication. Initial creation requires only an event name and timezone. Description, schedule, signup configuration, custom questions, capacity, team estimates, and board estimates may all be entered during the creation journey, but they do not block saving the initial private draft.

The system generates a permanent unique URL identifier from the event name. Renaming the event does not change that identifier; Identity exposes the permanent public entry link, not a slug editor. Duplicate event display names are allowed. Every enabled admin may continue configuring every event, and audit history identifies the actor for each change.

Name, optional description and buy-in information remain editable through Live and Final Review. The permanent slug never changes. Timezone changes are allowed only before first actual Live and preserve UTC instants. Events have no event-level banner/artwork capability.

The timezone defaults to `Europe/Copenhagen`. Admins choose another supported timezone through a controlled selector rather than entering an arbitrary identifier. A private event may change timezone normally. After signup opens and before Live, the change requires confirmation showing how every configured UTC timestamp will display in the new timezone. A timezone change never moves the stored UTC instants—changing the schedule is a separate action.

The complete setup flow contains:

1. Event identity: name, optional description, buy-in information, timezone and permanent URL identifier.
2. Schedule: signup opening/closing, draft time, event start/end, submission grace period, and timezone.
3. Event-specific settings: evidence requirements and verification code; ranking follows the event rule, not an Admin override. General public rules are maintained once on the global Rules page rather than copied into each event.
4. Signup form: required fields, optional questions, signup code, and participant-edit behavior.
5. Team configuration is completed in the team/draft workflow by creating the actual teams with explicit IncludedInDraft; event creation does not ask for a team count or roster size.
6. Preliminary board configuration: optional expected dimensions and board-EHB estimates for team count and size. Final dimensions, EHB planning inputs, and tile layout are controlled by the board editor.
7. Captain access: event/team membership roles on normal website accounts.
8. Review and publish: validate required configuration and choose which public event information becomes visible.

The event remains a private admin draft until signups are explicitly opened. Opening signup makes the signup form and unlisted signup table available through their exact links with minimum signup information such as event name, deadline, expected dates, and instructions. It does not add either route to general navigation or the public-board overview. The signup table is distinct from later team rosters.

Event setup is not a single-session process. Admins may create the event, open signups, and continue building or revising its private board throughout the signup and pre-draft period. The board does not need to be complete when signups open. Board validation and publication, rather than event creation or signup publication, determine when the competitive board becomes fixed and public.

Saving a draft and opening signups use separate readiness rules. Before signups open, the system validates the configuration required to accept and manage signups and distinguishes blocking omissions from warnings and work that is intentionally deferred until a later event phase.

Readiness is evaluated separately for saving the draft, opening signup, starting the draft, starting/publishing the event, and finalization. Each gate classifies unmet work as a blocker, an overridable warning, or a later task that is irrelevant to the current transition.

An admin may explicitly discard an accidental or experimental event when it has no participants, teams, event-scoped accounts, or evidence. Board content, signup questions, and other event-owned setup data do not prevent discard and are removed with it. Discard preserves a minimal audit/tombstone record and the old URL identifier, removes the event from active and public listings, and requires confirmation. Once protected participant, team, account, or evidence data exists, discard is blocked and the normal cancellation/archive workflow must preserve the event's history.

The following have separate publication controls and are not exposed by opening signups:

- Team assignments and rosters
- Draft order or draft progress
- Finalized draft results
- Bingo board and tile details
- Captain accounts or admin-only configuration

Finalized rosters and the effective pick order publish when the draft is finalized. After that succeeds, a popup or short follow-up page asks **Publish board?** and provides a separate **Publish board** action only when the board is grid-complete, explicitly approved, and still private. Dismissing it—or having an incomplete/unapproved board—does not affect draft finalization; the board stays private with a prominent admin action and can be approved/published later.

After finalized rosters are public, the Public boards overview becomes the discoverable event entry: before board publication its card states that the roster is available and routes to the roster; after board publication the same card routes to the read-only pre-live board. Neither publication changes the event lifecycle or enables progress, evidence, or submissions.

Pass 4.5 surfaces use one derived presentation phase without adding an event state: a pre-finalized closed signup displays **Signups closed**, a finalized draft without a public board displays **Draft finalized**, and public/participant views may display **Board published** with the scheduled start. Admin views display **Event ready · Starts …** only when the existing full start-readiness evaluation has no blockers; otherwise they display **Board published · Not ready**, or **Start postponed** for an unresolved overdue blocked scheduled-start attempt. These labels neither disclose blockers publicly nor change routing, authorization, readiness, or transitions.

Board dimensions remain editable planning configuration before board publication. Estimated team count and players per team, when needed to estimate board EHB, are entered in the board editor rather than event creation or draft setup.

- Drafted-team count is derived from the drafted teams admins actually create, and roster sizes are derived from confirmed included participants.
- Teams use IncludedInDraft; allowed manual assembly and finalized pre-first-Live corrections preserve original picks and prior publications.
- IncludedInDraft, not a formation-type label, controls draft order, turns, sizes and picks.
- First actual Live permanently locks team membership and registrations; current-member roles remain independently editable where allowed.
- The board editor may change board dimensions before board publication.
- Board-editor team estimates affect only board-EHB planning and never create teams, set roster sizes, or become draft constraints.
- Recording a pick locks the active private attempt. Before roster publication, its controller may cancel it back to Setup only after individual latest-pick undos leave zero active picks; cancellation never bulk-undoes picks. Finalized drafts cannot reopen.
- Changing board dimensions after tiles have been placed requires a preview of which positions or tiles are affected.
- Changing published board structure requires an explicit admin confirmation and audit reason. Team display metadata instead follows its separate pre-event edit lock.

Application-owned images use managed upload rather than arbitrary URL entry. Custom board/tile artwork and participant evidence are selected as local files, uploaded through the application, validated server-side, and stored as managed assets. Decorative images use their own authorization and retention rules even when they share the evidence uploader's file-selection and validation mechanics. External source-image URLs are accepted only in the global OSRS catalogue, where catalogue infrastructure fetches and caches them.

## 8. Board and ranking rules

### 8.1 Board layout

- Boards are rectangular, commonly 5x5 or 6x6. The global supported row and column range is 1–8.
- Event creation initializes an empty 5×5 board. Final board dimensions are configured in the board editor, not requested by Create event.
- Board dimensions remain changeable before publication. Resizing never relocates occupied tiles; every occupied tile must fit within the proposed row and column bounds.
- A safe growth or shrink preserves every occupied tile ID and coordinate, then records the dimension change through the existing lease/version, audit and approval-invalidation workflow.
- If any occupied tile would fall outside the proposed bounds, resizing is blocked and the admin must move or remove that tile first; the board dimensions, layout, approval/history and public snapshot remain unchanged.
- Rows and columns count as lines.
- Diagonals do not count.
- A completed tile counts only once as a tile but contributes to both its row and column.
- Overlapping completed lines count as separate completed lines.
- A 5x5 board therefore has five rows, five columns, and ten possible completed lines.

### 8.2 Placement order

Existing events retain this comparison order, including events without an official
result snapshot:

1. Full-board completion status
2. Full-board completion time, earliest first among finishers
3. Completed rows and columns
4. Completed tiles
5. Current-score completion time, earliest first
6. Credited EHB tie-break value, highest first

This retained order is separate from the AU12 target below. The event's explicit
ranking-rule boundary selects one order for the event; AU12 must not silently
rerank an existing event or its saved official snapshots.

Ranking priority is:

1. Full-board completion, ordered by immutable submission time of the final qualifying submission
2. Most completed rows and columns
3. Most completed tiles
4. Highest credited EHB tie-break value, including existing proportional partial progress
5. Earlier current-score completion time

This is the approved AU12 target, not the currently implemented ordering. Only new events created after activation adopt AU12; all existing events keep
their prior rule, including those without official results. Retain old official
snapshots without reranking. A separate points model
is deferred.

The first team to complete the entire board wins. The event and board remain open until the official event end and admin finalization. Other teams may continue for fun or for second- and third-place prizes.

Full-board completion is based on the immutable server submission time of the evidence that completed the final tile, not the time an admin reviewed it. Captains and admins do not edit this timestamp.

For teams that have not completed the full board, current-score completion time
is the latest immutable completion time among their currently complete tiles;
it is null when no tile is complete. Under the AU12 new-events-only target it
breaks ties after equal completed lines, tiles and credited EHB; the retained
comparator places score time before EHB as specified above. Reversals use surviving valid tile completions,
never the reversal/review clock. Existing full-board finish remains higher priority.
Exact equality across all competitive inputs gives shared placement; no discretionary
tie procedure or new manual timestamp correction is introduced.

EHB is used for board estimation, line balancing, player contribution statistics, and final tie-breaking. It does not otherwise award team points.

## 9. Boss, activity, item, and drop catalogue

Any enabled Admin may create, edit, deactivate, or reactivate catalogue records. Routine changes are audited without requiring a written reason. Only the Super Admin may permanently delete a catalogue record, and only after strong confirmation and a complete dependency check proves that no source drop, board, asset/cache, import review, or historical record references it. Referenced records must be deactivated instead.

**Catalogue decision, 4 October 2026 — pending AU23/CAT-1/WA-5:** ordinary Admins
enter and edit the full rate text, including `N x` roll count, on add/edit/reactivation.
`3/1024` is one roll at 3/1024; `3 x 1/1024` is three rolls at 1/1024. Remove the
operator-only roll-change refusal. Always enter the final chance of the item in
one's own name at the agreed team size; raid parent chances are resolved before
entry. Retire “Only after” from editor/panel/new input; retain its database columns
and all historical/snapshot data. No EHB parent-chance fix ticket is authorized.
Advanced roll-group editing remains SuperAdmin-only, with server-side checks,
validation, audit and concurrency. New drops use `default`; preserve existing
production groups. Legacy probability-scope/assumed-participant advanced writes
remain SuperAdmin-only until CAT-1 moves that informational context to the activity;
parent input is retired for every role.

CAT-1 gives the activity one agreed **Team size**, whole number >=1/default 1,
editable by every Admin next to Kills per hour in both Settings and Add activity.
It describes the shared efficient strategy behind rate and EHB; it never multiplies
probability or EHB. Recheck per-drop non-default/conflicting values before migration
(the user reported zero on 4 October), preserve approved/published snapshots, and
stop for a decision on unexpected values. The same user reported zero conditional
drops and existing production roll groups, which remain unchanged; no agent
production verification is claimed.

WA-5's decided “How the rate is counted” panel shows group in its header, then
chance per kill including `N x`, source and optional note. SuperAdmin edits group
there; others see it read-only. Bottom text: “Roll group can only be changed by the
Super Admin.” Remove chance-per-roll, rolls-per-kill, whose-chance, Only-after and
operator-refusal text. This intentionally supersedes the corresponding frozen
Catalogue reference when bound; no reference edit is part of this cleanup.

The application catalogue-import preview/apply interface is excluded by user decision (D03, reaffirmed 2026-09-14); existing operator tooling remains separate. Blocked catalogue deletion must protect dependencies and offer deactivation, but an individual dependency-reference list is not required (C26).

### 9.1 Boss or activity

A catalogue entry can represent a boss or a broader activity such as raids, skilling, or the Fortis Colosseum.

It contains:

- Name
- Image
- Category
- Active/inactive status
- Efficient kills or completions per hour, when applicable
- External data identifier, when available
- Notes
- Connected drops

### 9.2 Item

An item is an internal identity created and maintained only while adding or editing a boss/activity drop. It may be reused by several source drops, but it has no standalone Admin catalogue workflow.

It contains:

- Name
- Image
- Active/inactive status
- Optional external identifier
- Notes

### 9.3 Boss/activity drop

The connection between a boss/activity and an item contains:

- Display drop rate
- Optional normalized numeric probability
- Conditional-rate explanation
- Default EHB estimate
- Data source and last-updated time
- Active/inactive status

Drop rates may be conditional or unsuitable for automatic conversion. The system must support human-readable values such as `2 x 1/1,024` and allow manual notes.

Drop rate belongs to the connection between a boss/activity and an item, because the same item can have different rates from different sources. Selecting a drop for a tile therefore also selects the relevant source and its source-specific rate.

Admins can add any drop to a boss, including low-value or intentionally humorous "troll" drops. A drop does not need to be part of a unique drop table.

### 9.4 Expandable API mapping (agreed scope; implementation pending)

Existing catalogue editing surfaces expose an expandable API mapping section:

- Boss/activity: WOM metric identifier, using the existing external identifier field.
- Item: OSRS item ID for the Wiki prices API, using the existing item external identifier
  within boss/drop editing. This does not create a standalone item-management workflow.
- Suggest a mapping when adding an entry, allow Admin correction, and provide an explicit
  Validate mapping action with a clear result. Preserve shared item identity and normal
  audit behavior when editing mappings.
- Distinguish not configured, verified, unsupported and temporarily unavailable. An API
  outage must not prevent saving. Missing boss mappings explain that Luck activity is
  unavailable; missing pricing mappings explain use of the stored catalogue value.
- A verified WOM mapping establishes metric support, not the availability of every
  participant's activity. Validation must not claim otherwise.

## 10. Tile model

### 10.1 Tile fields

A tile contains:

- Name
- Public description
- Image
- Board position
- One or more requirements
- Estimated EHB
- Optional tie-break EHB value
- Active/published state

Tiles belong to one event board. The editor supports moving/swapping them but does not duplicate tiles, copy them from earlier events, import them, or expose a reusable tile-template workflow.

An empty or whitespace-only tile description is automatic: the board editor
derives its preview from the current ordered objectives and selected eligible
drops, and leaves the editable description field blank with a localized
placeholder. A nonblank description is an Admin-authored override and remains
unchanged when requirements are edited. Clearing and saving returns the tile to
automatic mode. For drop objectives, the configured target is always shown even
when selected drops carry higher contribution weights. One distinct catalogue
item uses its item name; otherwise selected source names are joined as
alternatives, while separate required objectives follow their stored order.
Manual objectives combine their authored objective descriptions in that order.
Approval freezes the rendered description and its mode with the immutable tile
snapshot; existing nonblank text and all existing published copy remain
unchanged by catalogue or draft edits.

### 10.2 Requirement builder

The normal admin flow is:

1. Choose one or more bosses or activities.
2. Choose eligible drops from those sources.
3. Set the target contribution.
4. Choose whether duplicate drops are allowed.
5. Optionally change a drop's contribution value.
6. Optionally configure a maximum contribution for a selected drop.

Most tiles contain one requirement. A tile may contain multiple requirements when separate targets must all be met. All requirements within a tile must be of the same kind: either catalogue/drop objectives or custom/manual objectives. A tile must never combine both; use separate tiles instead. Enforce this in the editor and authoritative create/update/approval validation.

Example:

- Requirement A: Obtain 5 selected Barrows pieces.
- Requirement B: Obtain 5 selected Moons of Peril pieces.
- The tile completes only when both requirements are complete.

Objective corrections preserve stable identity and evidence history (C20, approved
2026-09-14). Wording-only title/description corrections remain allowed. After any
submitted evidence exists for an objective, its substantive requirements/scoring and
removal are locked, including when evidence is pending, rejected, reversed or withdrawn.
Objectives without submitted evidence remain editable under the existing workflow.
Private corrections leave the active published board usable until a valid replacement
is published; newly arriving evidence must also be protected. See FUNCTIONAL_CONTRACTS
6.2 for the correction boundary. This does not authorize historical evidence repair.

An Admin may discard an open private board correction after confirming that all its
unpublished board edits will be lost (C20 recovery approved 2026-09-14). This restores
the working board from its current published version and closes the correction so a new
one can begin. Published rules, submissions, progress and historical snapshots remain
intact. This is not a rollback of published results or historical-data repair.

### 10.3 Duplicate behavior

Each requirement has a **Duplicates allowed** option.

- When enabled, repeated copies of the same eligible drop continue to contribute.
- When disabled, each selected drop can contribute only once unless an admin explicitly configures a different cap.

OSRS terminology must be presented carefully. A "unique drop" means an item from a unique drop table; it does not imply that duplicate copies are disallowed.

### 10.4 Contribution values

- An eligible drop contributes `1` by default.
- Each eligible drop has an optional board-defined **Higher contribution weight** setting.
- Every drop defaults to `1`. The board designer may assign a different fixed weight to individual eligible drops; for example, Theatre of Blood purples may count as `1` while Scythe of Vitur counts as `2` toward a target of `6`.
- Captains and other submitters cannot override the configured weight.
- Progress is capped at the requirement target and never carries to another tile.
- One obtained drop can contribute to only one tile.

### 10.5 Specific component objectives

For a full Voidwaker objective:

- Select the three relevant bosses.
- Select the hilt, blade, and gem.
- Set target contribution to `3`.
- Disable duplicates.

Each different component can then contribute once.

### 10.6 Manual objectives

The system should support a manual objective for tasks that cannot reasonably be represented as item drops. Manual objectives still require evidence and admin approval.

Examples include:

- Complete Theatre of Blood at four-player scale under a configured time.
- Complete an activity under a particular restriction.
- Complete a kill-count, speed, collection-log, or combat-achievement objective.

A manual objective contains its own completion description, objective-specific criteria, and target quantity rather than eligible item drops. General screenshot and submission instructions belong to the global public Rules and how-to pages. Each approved completion normally contributes `1`, so an objective may require multiple completions, such as completing three Inferno runs.

## 11. EHB and board balancing

The board editor displays:

- Estimated EHB per tile
- Total estimated board EHB
- Estimated EHB for each row
- Estimated EHB for each column
- Lowest and highest line EHB
- Absolute and percentage spread between lines
- Warnings for unusually easy or difficult lines
- A **Preview board** action that renders the actual responsive public-board treatment without admin controls and without approving or publishing

Admins deliberately position tiles to balance rows and columns. Tiles are not randomly placed after creation.

EHB and efficient-rate data may be imported from useful external sources such as Wise Old Man where technically and legally appropriate. The preferred approach is a stable API or data endpoint rather than fragile HTML scraping.

Catalogue source data is collected through the existing reviewed import/admin workflow and remains editable by admins. Separately, Slice 10 provides explicit Wise Old Man account-EHB lookup plus cached Live event-competition EHB synchronization under the bounded contract in `FUNCTIONAL_CONTRACTS.md` section 9.6 and the technical/data authorities; it does not turn catalogue data into an automatically synchronized external feed.

For a simple single-drop requirement, expected EHB pairs the reviewed final
in-name item probability with efficient completions per hour at the agreed team
size. At 100 kills/hour and 1/1,000, one drop requires 10 EHB. The agreed team size
balances speed and drops, not simply maximum kills/hour. Team-size/probability-scope
context is informational, never a calculation input. Valid numerator fractions
and `N x` repeated rolls remain explicit. Raid/points/purple-table assumptions are
resolved before entry and may be explained in notes. Pending AU23/CAT-1 implements
the 4 October input/context changes above; immutable approved/history snapshots
and existing roll groups remain untouched.

Complex requirements are estimated from possible completion outcomes rather than by blindly averaging bosses or adding rates. Weighted drops advance by their configured contribution, duplicate-restricted requirements track shared item identities, and alternative sources are chosen according to the lowest expected remaining person-hours. Multiple objectives are estimated separately and added. A catalogue-backed/drop tile must always produce an automatic estimate. Missing or ambiguous rate mechanics block board approval and must be corrected in the catalogue or requirement configuration; they are never guessed and cannot be bypassed with a manual override. AU11 permits an optional manual TOTAL tile EHB override on a valid calculated
drop tile as well. Retain the calculated baseline and allow reset to it; custom/
manual tiles still require an entered estimate. The effective tile value feeds
existing board/line estimates, proportional credited player statistics and ranking.
It is event-local, does not modify catalogue probabilities/KC/Luck, and cannot
bypass missing or ambiguous calculation inputs. Submitted evidence locks affected
scoring; approval/publication/history invariants remain. AU11 is approved, not yet
implemented.

Unapproved boards derive catalogue names, images, source-drop rates, and EHB from the current global catalogue. Relevant catalogue changes automatically invalidate and recalculate their tile, row, column, total, and per-player estimates.

**Approve board** is the snapshot boundary. Approval transactionally revalidates the complete board and captures an immutable version of the catalogue values, board configuration, rates, EHB values, artwork references, and calculations. Later catalogue changes do not alter an approved board. Unapproving or editing approved unpublished competitive content returns the board to Draft, retains the superseded approval snapshot/history, and resumes live catalogue derivation. Initial publication and publication of a corrected replacement both require explicit confirmation, and publication uses the active approval snapshot without recalculation. Corrected publication is available only while the event is Signup closed, Live, or Awaiting final review; terminal, cancelled, hidden, and discarded events reject it.

Draft preview uses current live derived data; approved preview uses the frozen approval snapshot. Preview never changes board state. Published boards remain historically stable.

Board approval is an explicit admin action available only when every grid position is filled and every tile validates. Any enabled admin may approve; a second approver is not required. An approved unpublished board may be unapproved, and editing any tile or competitive board content automatically returns it to Draft while preserving the prior approval in history. These private pre-publication changes require no written reason. Event start is blocked until an approved board has actually been published.

The application provides one permanent, global, public **Rules** page. Any enabled administrator may edit it at any time through an **Edit rules** action. Rules changes use normal authorization, validation, concurrency protection, and automatic history, but do not require a written reason, notify participants, or affect event readiness.

Public how-to pages, including **How to submit drops**, are source-controlled application content maintained through the development workflow and have no in-application editor. Event dashboards and submission surfaces may link to these global pages. Tile approval does not require or inspect per-tile evidence instructions because those fields do not exist. Custom/manual tiles retain only their objective-specific completion criteria.

## 12. Evidence submission

### 12.1 Submission contents

Each participant or captain submission represents:

- One kill or reward event
- One obtained drop
- One screenshot
- One tile requirement
- One credited participant: the authenticated participant themselves, or a current teammate selected by a captain/co-captain
- The credited participant's active playing account at server submission time
- One team, derived from current event/team membership

It records:

- Event and team
- Tile and requirement
- Boss/activity
- Drop
- Credited player
- Credited playing account
- Credited weight copied from the board-requirement snapshot, defaulting to `1` and not editable by the submitter
- Total approved contribution, capped by the remaining requirement progress and confirmed by an admin
- Submitted time, generated automatically by the server and immutable
- Screenshot
- Optional submitter note
- Status and admin feedback

Participants cannot select another player. Captains/co-captains cannot select a player who is not on their current event team.

An ordinary participant sees no credited-player or credited-account selector. A captain/co-captain selects a current teammate, not an account. The server derives and snapshots that participant's currently active/drop-eligible playing account. Submitters cannot credit an inactive registered playing account or informational account; they must submit a drop before swapping away from the account that received it.

### 12.2 Evidence expectations

A normal screenshot should show:

- Player name
- Game message identifying the drop
- Timestamp overlay when required by event rules
- Event-specific verification code when enabled by event rules

The verification code is an event setting with two modes: enabled or disabled. Admins normally enter a custom fun code, may generate one, and may activate a replacement immediately or schedule it. Configuration remains editable through Draft, Signup open, Signup closed, and Live, and during Awaiting final review only while the active submission window still accepts uploads. It is read-only after upload closure and in every terminal, cancelled, hidden, or discarded state. Every submission snapshots the code interval active at its immutable server submission time. Review is visual only; there is no OCR. An admin may approve a mismatch as an explicit exception.

The clan event plugin renders the evidence time in UTC inside the screenshot. That value is not transcribed into another form field or extracted through OCR. The reviewer visually confirms that it falls inside the official event window and an interval in which the credited playing account was active. Immutable server submission time remains authoritative for upload-cutoff validation and every ordering rule that explicitly uses submission time, and it is never editable.

When the credited participant has swapped accounts, evidence review shows the latest relevant UTC transition and provides complete swap history only as an admin/audit detail when needed.

### 12.3 Validation

Before accepting a submission, the system checks that:

- The authenticated actor's event/team role is allowed to submit or correct evidence.
- The selected tile and drop are valid for the event.
- The credited participant is the submitter or, for a captain/co-captain, a current teammate.
- The server-derived credited character is that participant's active `PLAYING` assignment at submission time and is not an informational/support account.
- The submission contains one image.
- Immutable server submission time is no later than the active upload cutoff.
- The contribution does not exceed the remaining allowed progress.
- Duplicate and per-drop cap rules are respected.
- The same approved submission cannot be allocated to multiple tiles.

For a duplicate-disabled requirement, duplicate identity is the immutable shared
catalogue item captured by the event/approval snapshot, not a source-specific
drop, image, or submission identifier. Alternative source rows for one item remain
valid. Allocation groups by team, requirement, and item identity; a missing
explicit cap means `1`, and every alias in that requirement must have the same
effective cap or board approval fails. The same item may independently satisfy a
different sibling requirement. Progress, completion, reversal, and rebalancing
remain isolated by requirement; fulfillment never carries into a sibling
objective. Retargeting Pending evidence recomputes the authoritative destination
requirement/drop weight rather than carrying the old target's weight.

## 13. Submission review lifecycle

Submission states are:

1. **Pending:** Awaiting admin review; editable and withdrawable by the credited owner when eligible, and by authorized team captains/co-captains within their broader team scope.
2. **Approved:** Contribution has been applied to official progress.
3. **Rejected:** Does not count; includes an admin reason.
4. **Withdrawn:** Removed by a captain before approval.
5. **Reversed:** Previously approved, then removed by an admin.

### 13.1 Admin review actions

An admin can:

- Approve
- Reject with a reason
- Correct pending metadata with a reason, then approve separately
- Reverse a previous approval

Editable metadata includes:

- Tile or requirement
- Credited account from the full event pool, including non-current entries, with validated participant/team attribution (AU17a approved, pending)
- Qualifying drop and its derived boss/activity

These material corrections require a written reason, revalidate the complete submission, and store the original and new values. Credited participant is not independently editable. Immutable server submission time, calculated contribution, and the submitted evidence image are not administrator-editable. Snapshot contribution weight is not manually editable; changing requirement/drop replaces it with the authoritative frozen weight of the selected destination.

Every review shows:

- **Submission time:** immutable server submission time.

Only when submission occurred after the authoritative event end, the review additionally shows the calculated number of minutes after event end and **Latest clan event time:** the authoritative end formatted in UTC. The administrator compares the timestamp visible in the screenshot with that boundary. A drop shown after it is ineligible even though the website still accepts uploads during grace. The submission cutoff is enforced by the application but need not be repeated in this compact visual comparison.

A duplicate, unusable screenshot, or other invalid attempt is rejected with the required reason. There is no request-changes or special duplicate review state. Rejected and Reversed submissions remain immutable history and cannot be directly re-approved. While the active or explicitly reopened upload window permits new evidence, every later attempt is an ordinary new submission with its own immutable server submission time, evidence asset, review history, and normal validation. It is not a correction child, does not require or create a predecessor link, and is not limited by a one-child retry rule. Historical predecessor links and the `Resubmit` review enum remain readable for old records only; they do not constrain new submissions. A closed window requires the existing reasoned Admin reopen action.

Rejection creates an idempotent in-site notification containing the reason for the credited participant and every current captain/co-captain on the team. It does not notify the whole roster. When the credited participant is unlinked, captains/co-captains remain the notification recipients. Every personal recipient is routed to `/Submissions/{id:guid}`; the destination independently authorizes the credited participant's current-team scope or the existing team-scoped Captain/co-captain authority. Relevant general submission navigation resolves to `/Submissions`, while Admin review notifications remain `/Admin/Review/Details/{id}`.

### 13.2 Reversal

Reversing an approval:

- Deducts the exact contribution created by that submission
- Recalculates tile, row, column, board, leaderboard, and placement state
- Reallocates newly available capacity to later approved evidence up to its original eligible claim
- Records the admin, time, and reason
- Preserves the submission and its history
- Writes the main immutable audit entry in the same transaction

Submission creation and eligible participant/Captain/Co-captain edits,
replacements, withdrawals, and ordinary later attempts also write the main immutable
audit entry in the same transaction. Retained ReviewAction history remains the
authoritative evidence-decision history; the shared Audit presentation renders it
once and falls back to the immutable audit entry for newer records without a
duplicate or conflicting history.

## 14. Evidence visibility

- A current member of an event team may view that team's complete retained submission history, including records credited to departed teammates, when currently authorized. Former members and cross-team viewers fail closed except that, after archive, a former credited owner may reach only their own retained Rejected/Withdrawn submission detail and evidence asset read-only through the existing account-history destination.
- Only the credited owner may mutate their own eligible submission; captains/co-captains retain their server-authorized broader editing scope for eligible submissions in their current team.
- Approved evidence metadata, credited player, and screenshot are publicly visible from the relevant tile so the community can inspect accepted evidence.
- Other teams do not see pending progress.
- Participants and captains cannot request that approved evidence or the credited player be hidden. Submitters are responsible for concealing private messages or other information before upload.
- There is no hidden-but-still-approved evidence state. If an approved image should no longer be public, an admin reverses its approval with a reason; the team may submit a corrected or redacted screenshot as an ordinary new submission through the normal cutoff while the upload window permits it.
- Version one has no public evidence-report, bug-report, or general-feedback form. Community reports and feedback use the Discord feedback channel; admins handle a valid evidence concern through reversal and an ordinary later submission.

After draft finalization, the signed-in participant's primary event destination is their published roster until the board is published, then the existing team-board view. That view retains only compact current active-account context and the authorized immediate participant account-switch control; it does not duplicate lifecycle, roster, or submission-workspace panels. Submission access is enforced without ordinarily displaying the internal cutoff. It adds the participant's own evidence actions and private read-only team-focus projection without creating a separate participant board. Captain/co-captain focus mutations belong to the Captain-only section of the canonical `/Submissions` workspace.

Before event start, the built-in primary account is only the planned starting account. It becomes active at event start. Only the participant may switch during `LIVE`; switches take effect immediately at authoritative server time and close at event end. Captain/Admin switch-on-behalf is retired. Evidence creation and pending edit/withdraw remain open through the submission cutoff for in-window drops; cutoff then makes participant history read-only while admin review continues.

Current team members see team focus read-only on the existing team board; captains/co-captains mutate it exclusively in the Captain-only section of the canonical `/Submissions` workspace. Public/opponent projections retain the approved board without focus. A participating Super Admin may explicitly opt into a clearly identified, read-only inspection of another team's focus; no cross-team focus data loads before that opt-in. If the authorized view becomes crowded, a view-only focus visibility toggle or compact summary may hide/show the private layer without changing focus state.

The public tile view should show approved drop, player, team, submission time, contribution, and evidence.

## 15. Progress calculations

When evidence is approved or reversed, the system recalculates:

- Requirement progress
- Tile completion
- Row completion
- Column completion
- Full-board completion
- Team ranking and placement
- Player contribution statistics

Pending evidence does not count toward official progress or standings.

When a team first reaches full-board completion, it is visibly marked as a finisher. The first finisher is marked as the provisional winner, but official placements remain subject to outstanding reviews and admin finalization.

### 15.1 Stats data decisions (2026-09-15)

The approved Stats prototype remains separate from production implementation. The
following data rules are agreed; the Luck model still requires concrete validation and
source-mechanics coverage before implementation:

- Submission-derived Stats history uses submission time, never approval time. Only
  approved, non-reversed evidence counts. If a later submission is approved before an
  earlier submission, subsequently approving the earlier one recalculates affected
  historical totals, completion times and milestone ordering using submission order.
- Reversals recalculate affected Stats using the remaining valid approved evidence,
  subject to the existing finalization and competitive-history rules.
- Scoring lines follow existing rows and columns, without diagonals: a 5×5 board has
  10 lines. The prototype's illustrative 12-row denominator is not a production rule.
- Luck will not add a per-objective cutoff when a tile completes. The agreed assumption
  is that teams move to unfinished objectives. This does not authorize accepting
  submissions against completed tiles.

#### Tile KC and Luck — approved 2026-09-16

The team-specific tile sidebar adds a section matching the existing team EHB/Drop
EHB sidebar. Its summary shows Team total, tile Luck and KC; an expandable
Contributors list shows Luck, participant name and KC. When several bosses or modes
are relevant, show separately labelled KC totals for each boss/mode in both the team
summary and participant rows; do not combine different metrics into one KC number.
Aggregate each participant's playing accounts and count each character/metric once,
regardless of how many eligible items or requirements reuse it. Informational alts
are excluded. Contributors display only participants with a known count greater than
zero for the displayed boss/mode; omit zero/unknown rows, empty metric groups and an
entirely empty Contributors disclosure. This presentation filter never removes accounts
from team totals or Luck calculations. Preserve missing, estimated, incomplete and
stale-data semantics in the underlying results and visible team status.

Tile Luck counts only approved, non-reversed item submissions credited to the selected
tile and team (one item per submission, independent of scoring weight). Its expected
count uses the selected tile's distinct eligible item/source outcomes, their frozen
first-approved event rates and corresponding full-event activity. Use the shared bounded probability-based Luck score below, including participant
account aggregation; do not average percentages, add a completion cutoff or divide
raid rates by team size again. Stats and tiles share the same score calculation;
tile eligibility remains narrower. Existing tie selection remains unchanged.

Reuse cached WOM activity and existing Luck rules; no provider call on tile access.
Preserve coherent activity/evidence snapshots on failures, with accurate freshness and
availability wording. Staleness retains compatible last-known KC and its contributors,
including older saved event results without tile breakdowns. Recover tile Luck only
when retained activity and the evidence revision are coherent; otherwise keep known KC
visible and explain unavailable Luck. Age alone must not clear known activity. No-drop objectives remain valid and show an applicable empty
state rather than an invented KC/Luck result. Preserve published-board visibility,
privacy, history, evidence and submission controls across enhanced and direct routes.

#### Drop value

- Each approved item submission represents one actual item/drop. Scoring weight affects
  board progress only, not item count or GP. Value the exact dropped variant.
- Use OSRS Wiki real-time prices. Freeze event values at event start from the last
  completed hour's buy/sell averages: their midpoint rounded to the nearest GP. Use
  the available side if only one exists; otherwise use the stored catalogue value.
- Populate values for existing catalogue items before deployment. New items require a
  fetched or manually supplied value; 0 is valid and is distinct from missing.
  API-backed catalogue prices can refresh, while explicit manual values remain manual.
- The initial catalogue price population will be checked by the operators and serves
  as the trusted baseline. Later API updates must detect suspicious changes against
  the last trusted catalogue value; retain that value and flag the candidate for
  checking instead of automatically accepting a suspected spike. Apply this protection
  to event-start price selection as well as catalogue refresh. Recent price history
  and trading volume may support the check; it is not a guarantee against manipulation.
  Existing frozen event values remain immutable. Accepted guard: for a positive trusted
  value, reject candidates strictly below 50% or strictly above 200% of that value;
  exact boundaries are accepted. Flag any zero-to-positive or positive-to-zero change.
  Without a trusted value, initial population still requires operator checking rather
  than claiming this guard provides protection. Preserve explicit manual/untradeable
  precedence. Use existing catalogue feedback/operator reporting with a persisted
  rejected candidate/time flag, separate from mapping validity; no new event-price editor.
  Rejected start candidates use trusted catalogue fallback with explicit rejection
  provenance. Volume/history-based detection is deferred.
- Reject adding a tile when any of its eligible drops lacks a stored catalogue GP
  value. Objective tiles with no drops are exempt and remain valid; this rule never
  requires a tile to have drops. An explicit 0 satisfies the price requirement;
  null/Missing does not. Identify the
  affected drop so its catalogue value can be supplied. Revalidate this prerequisite
  when approving/publishing board changes to prevent later edits bypassing the gate.
  This is catalogue completeness validation, not a live-provider availability check;
  a provider outage alone must not prevent use of an existing stored value.
- Untradeables default to 0 unless an admin changes their catalogue value. This includes
  components such as vestiges and the Araxyte fang: do not automatically substitute the
  sale value of an assembled tradeable item. An admin may explicitly assign a manual
  catalogue value to represent that potential value (user confirmed 2026-09-16). Catalogue
  updates affect future snapshots. No manual event-price editor or event-price correction
  workflow is wanted; ongoing and past event snapshots remain frozen.

- For any item introduced to an already-started event without an existing frozen event
  value, require its stored catalogue value and freeze that value when introduced.
  This covers newly created items and originally unused/unpriced catalogue items.
  Zero is valid; a missing value blocks introduction. Do not fetch a historical price
  to replace that introduction value. Record catalogue source and introduction time;
  do not describe it as observed at event start. Existing event snapshots never change.
- Retrospective Stats for the reconstructed Sommerbingo 2026 import are out of scope
  for this implementation. Its existing archived board/history remains intact; aggregate
  imported contribution units do not become asserted item drops, GP or Luck results.

#### Current Luck calculation

The September bounded signed-score formula is superseded by the implemented
[Luck percentile/KC contract](#luck-percentile-and-kc-comparison--approved-implementation-2026-10-01).
Use the 0–100 midrank percentile and in-container KC difference from a compatible
saved snapshot, retaining freshness through new approvals/reversals until refresh.
Do not revive the former -100…+100 display or hide every routinely stale snapshot.
Legacy stored calculations remain history; controlled conversion uses retained data.

## 16. Player contribution leaderboard

Because captains submit on behalf of players, every submission must credit the player who received the drop.

The leaderboard may display:

- Estimated EHB contribution
- Approved qualifying drops
- Approved submissions
- Tile-finishing drops
- Team

The primary player ranking is estimated EHB contribution, matching the community's existing practice. Team and player EHB use the same allocation: each approved contribution receives its proportional share of the tile's combined expected EHB snapshot. Completing a tile credits exactly its expected EHB and completing a board credits exactly the board's expected EHB. Do not sum standalone time-to-specific-drop values for alternative eligible drops, because the same kills roll those alternatives together. The product must label this clearly as an estimate; it measures credited expected objective effort rather than actual time played.

### 16.1 Boss KC leaderboard selection — approved 2026-09-17

The existing public Leaderboards page gains a metric selector. Its default option,
EHB & Drop EHB, preserves the current EHB / Drop EHB / Players views. With WOM linked,
offer the distinct relevant bingo boss/mode metrics, alphabetically; selecting one
shows Teams / Players views for that metric's full-event gained KC. Reuse the existing
cached activity and playing-account attribution; selection never fetches provider data.

Teams columns: Rank, Team, Players, Total gained, Avg. gained, MVP. Players counts
participants with known positive gained KC, and average divides their total by that
contributor count. Team expansion and the standalone Players view show only those
contributors. Keep zero-contribution teams visible without an MVP or empty expansion.
Combine each participant's playing accounts; informational accounts remain excluded.
Players columns: Rank, Player, Team, Gained, Start, End, Drops. Preserve Start/End labels
during the event and compatible cached values when stale; missing data must not become
zero. Do not display freshness/update/health status text or timestamps in leaderboard
views (including repeated per-team/account notices). Retain semantic cache safeguards,
per-account unknown values and necessary estimate distinctions without freshness labels. Separate boss/mode metrics must not duplicate
KC through repeated tile placement or be added into a heterogeneous total.

Drops displays the participant's approved event drop count attributed to that boss,
across tiles, as a coral clickable number. It navigates to the existing Drops view
with the boss name searched and that participant's team selected. The destination
intentionally includes the whole team's matching drops. Boss-name search already
works per user confirmation: no search redesign or separate investigation is in scope.

For boss KC and existing EHB/Drop EHB team tables, MVP displays the name and individual
signed contribution; equal top contributions display Multiple MVPs and the common
individual value, never their sum. No contribution means no MVP. Reuse existing signed
number formatting without unit suffixes. Positive gained values and MVP contributions
use the same existing gain-green and + prefix (raw Start/End counts remain raw). Existing
EHB/Drop EHB columns, calculations and participant visibility remain unchanged; only
MVP/Rank and the approved shared presentation/selector corrections below may change;
boss contributor-only rules do not apply to those existing views. The one additional
approved label change is Rank in both English and Danish across the existing and new
leaderboard tables; replace Danish Placering for this heading without unrelated
translation changes.

Use the existing table structure/styles/sorting/expansion, with no new table design
or separate table component. Reuse the page-header dropdown menu and interaction.
The trigger reads METRIC: [selected name] plus a downward chevron and matches the
leaderboard tab styling. In light mode its text and chevron use existing ink normally,
and existing blue when open, hovered or keyboard-focused. It shares their row above the horizontal divider, right-aligned
immediately before the standings divider when standings is expanded, or with the
horizontal divider's right end when collapsed. On insufficient width it may wrap to
a stacked arrangement only when the available content width actually requires it;
Metric is then above the tabs, and both are left-aligned above the divider. Do not
stack early at an unrelated viewport breakpoint. METRIC: is a trigger prefix only;
menu options show their names without that prefix. The menu closes on outside click,
repeated trigger click, Escape and selection. Positive gains/MVP values use the existing
green and signed format; the coral numeric Drops link has no arrow suffix. Dark-mode
column headings are consistently muted blue across all leaderboard tables (including
sortable/nested headings), with visible hover/focus behavior.

Metric switching updates the leaderboard in place without a full document navigation
or scrolling to the page top. Fetch only the selected view as needed using existing
route/render owners; do not preload every boss or fetch WOM. Keep selected controls,
standings metric, URL, Back/Forward, sorting and expansion coherent after updates.
Prevent an older response from replacing a newer selection; on failure retain the
last usable view and a usable recovery path. Direct navigation/reload remains supported.
Boss table Gained/Start/End headings are localized as Opnået/Start/Slut in Danish,
including both expanded Teams and standalone Players presentations. The nested EHB
account table uses Gained/Start/End without redundant EHB prefix/suffix, localized
accordingly. This label simplification does not apply to the Drop EHB table or other
standalone default Players metric headings. Preserve sorting and accessible labels.
User visual acceptance remains required.

## 17. Drafting

### 17.1 Version-one draft flow

An Admin records captains' selections from voice/text; captain-operated shared picks
remain deferred. Teams / Draft combines setup, running draft and finalized rosters.
`IncludedInDraft` alone determines inclusion. With two or more included teams,
derive balanced sizes from the included confirmed participants and preassignments;
at least one actual Captain per drafted team is required at start/finalization.
Multiple Captains are allowed; Co-captain remains a distinct role and occupies a
normal roster place. With zero/one included team, assemble manually and finalize
without Running or invented picks. Unplaced people are a non-blocking readiness
note repeated in that manual-finalize confirmation.

Waiting/withdrawn people and excluded teams' members stay outside the website
pick pool. Included confirmed people must be assigned before website-draft
finalization; the manual 0/1-team exception above does not weaken that rule.
Derived final sizes differ by at most one, count Captains/Co-captains as ordinary
places and skip teams with more preassigned members until others catch up; impossible
preassignments block the draft. No separate requested team count or size governs it.
Team names remain event-unique and stable URL identifiers survive renaming. Team
structure/inclusion follows the existing first-pick lock even after individual
undos; an explicitly eligible cancellation returns the private attempt to Setup.
A missed scheduled start alone does not prevent allowed pre-Live corrections while
the configured end remains future. Live start has no Captain prerequisite.

The running workspace automatically compacts the sidebar and removes unnecessary
header space. Team columns contain actual preassigned members and picks, with
current/target counts; no reserved Open place rows. The available pool shrinks as
team columns grow. Preserve readable primary account/EHB and all teams for normal
desktop drafts; larger exceptional drafts may need bounded pool scrolling. Click
a player to pick immediately; Undo reverses latest active picks individually.
Pending actions cannot double-submit. Drawing order is separate from starting;
redraw/preassignment follow existing pre-first-pick eligibility. A draw animates
waiting then the confirmed order, with reduced-motion support and no early success.

Setup uses team cards and participant search; remove the redundant participant
table and affiliation/image editing controls, preserving stored history. Role
changes remain available while Live for current members. Finalized pre-first-Live
Add/Remove republishes rosters under ROS-01; first Live permanently locks membership.
Finalize keeps the user on Teams / Draft and offers Open Board. No Pause/Resume or
finalized Reopen; cancel a private Running attempt only after explicit undos leave
zero active picks. Retain all prior picks/publications as history.

Internal control identity is hidden on the streamed board (Another admin); only
the takeover confirmation names the current controller. Public rosters and effective
pick order appear after finalization; roster publication does not publish the Board.
The active page renews its draft lease; five minutes without successful renewal
expires it, not five minutes without a pick. Concurrent mutations remain guarded
by authoritative control, version, identity and membership rules.

Existing commands already support most of this. AU14 and RC04 own uncertain recovery;
new reference presentation/search/route binding is still pending. No new draft
algorithm, direct Captain access or retired workflow is authorized.

### 17.2 Concurrent administration

- Board editing uses optimistic concurrency. Each editing form is tied to the version of the complete board aggregate it loaded, so tile and layout changes cannot silently cross.
- If another admin saves a newer version first, a stale save is rejected instead of overwriting it. The admin is told to reload and review the newer content.
- Opening the board is view-only by default so several administrators can inspect it together during voice discussions.
- An administrator explicitly enters edit mode and receives one renewable editing lease. Other administrators remain live viewers and see who is editing.
- Editing control can be released or explicitly taken over after confirmation. Navigating away attempts to release it immediately; it otherwise renews only through actual board activity and expires after five inactive minutes, including when an editor simply leaves the tab open.
- A running draft has one active controller with server-enforced authority to start, pick, undo, cancel an eligible private attempt, and finalize.
- Other administrators can follow the draft in a live read-only view.
- An administrator can explicitly take over control after a confirmation. The transfer is audited and immediately removes write authority from the previous controller.
- Concurrent requests must never create two picks for one turn, assign one participant twice, or silently overwrite board content.

### 17.3 Deferred draft enhancements

- Captain-controlled picks
- Automated turns
- Draft timers
- Alternative draft-order systems
- Automatic turn advancement
- Player availability notes

## 18. Signups and participant import

Signups were previously collected through Google Forms and stored in a Google Sheets response sheet. Version one will move the primary signup workflow onto the bingo website.

### 18.1 Website signup

Admins configure and publish a signup form for an event. New participation requires an existing active website account; returning users may be password-authenticated with Discord unlinked. Retained/imported accountless identities remain historical data, not authority for new accountless creation, merging or participant-owner transfer.

Each event has a configurable signup window:

- Signup opening date and time
- Signup closing date and time
- Event timezone
- Maximum number of confirmed participants

Before team-draft lock, Admins may change capacity but never below Confirmed count. Ordinary increases promote earliest eligible waiters atomically with audit/notifications. The explicit selected-person Confirm/Restore/Admin Add +1 exception promotes only that selected person. Waiting is always enabled while signups are open; no disable control remains. The public page shows close time, capacity, Confirmed count and current admission placement.

The standard form contains:

- One OSRS playing account, required
- EHB snapshot for that playing account, required
- Captain-volunteer choice, always present
- Event-specific custom questions

Discord identity comes from authentication and is not a participant-entered signup answer. OSRS character registrations do not grant website authority and do not assert exclusive account ownership.

The signup character selector shows the authenticated participant's globally linked characters. If one is missing, the form links to My accounts so it can be added before returning; the final account selector is not a free-text character-creation control. A character already assigned to another participant in the same event cannot be selected. This event-level uniqueness is enforced transactionally; the same character may be assigned to a different participant in another event.

Participant-facing terminology (user clarification, 2026-09-07): an Alt is a support character played alongside a main/regular character, for example to heal it or trade supplies between kills. The existing Account and Alt account question/column wording sufficiently distinguishes their signup roles; additional role labels or badges are not required. Personal My accounts labels/notes are separate and are not signup answers. This presentation decision does not change playing/informational eligibility or EHB rules.

The built-in required Account question creates the participant's first playing account and includes its required event-specific EHB snapshot. Admins may add more custom Account questions and configure whether each named answer is another playing account or informational-only account such as a support alt. Secondary Account questions are always optional. When an optional playing account is answered, its own EHB becomes required. An informational account has no EHB, cannot become active, cannot receive drop credit, and does not enter Wise Old Man standings. If only the existence of a support alt matters, the admin uses a Yes/No question instead. While signup is open, the participant may edit or clear these optional answers; the required primary account cannot be cleared. The signup presentation shows the required/system primary regular account first, then additional playing/regular Account questions in configured relative order, then informational/alt Account questions in configured relative order; this presentation ordering does not rewrite question definitions or stored answers. The built-in primary account automatically becomes the initial active/drop-eligible account at event start; there is no separate initial-active choice during signup. Closing signup freezes the account set and roles, and live swaps may then choose only among the frozen playing accounts.

Each My accounts link may store an optional personal EHB default. A regular Account control is prefilled from that value; saving the signup updates the default and captures a separate event snapshot. Later My accounts edits do not silently rewrite that event snapshot, while saving an edited signup refreshes it from the current control value. The primary Account answer's snapshot is the participant's draft sorting/balancing value. Secondary EHB values are not summed or substituted. Once the Wise Old Man integration is delivered, My accounts and every regular-account EHB control in signup/edit provide an explicit **Fetch from Wise Old Man** action using that account name; this is not an event-level option. A successful fetch in signup/edit immediately updates the authenticated participant's existing owner-linked My accounts character with the fetched EHB using normal My accounts update/timestamp semantics, while keeping the value visible in the signup form; it does not create or alter an event participant or assignment. Manual EHB entry remains available in My accounts, which is the accepted fallback when signup lookup is unavailable; a separate inline manual EHB editor on signup/edit is not required (user decision, 2026-09-07). A failed fetch leaves both the current signup value and My accounts value unchanged. The submitted value is stored as the event snapshot. The application must review and follow the current WoM API usage rules before implementing this action and must not issue requests on page render, selection, save, or every keystroke.

An event may link one existing WOM competition. Website dates own the schedule: no provider-date import or synchronize-schedule option. Linking/replacement compares both UTC boundaries exactly (AU20 pending; current code still permits five minutes). Pre-first-Live external disconnect leaves the remote competition intact; matching replacement also remains available during Live, including after a code is stored, subject to operation guards. Final Review and terminal connection configuration is read-only.

The dedicated Admin WOM surface also supports one explicit, complete creation flow
for a finalized pre-Live event. It builds one whole team payload from every
active membership and every unreleased `PLAYING` event-character assignment for
confirmed participants, including teams excluded from the website draft. It does not validate player
existence or snapshots first; plausible names are sent to WOM and the provider's
normal validation response is shown. Event names are limited to 50 characters
and team names to 30 for new or changed values, using provider-compatible
normalization. Retained overlength historical names remain readable and are
reported as a correction before creation rather than truncated.

Successful explicit creation stores the existing competition link and a
protected management credential automatically. Only links created through this
flow receive automatic management state; manually linked competitions remain
read-only and never gain imported credentials or an opt-in mode. Permitted
pre-Live schedule, roster, membership, team, and account corrections enqueue a
coalesced update through the existing provider limiter. Once an event has ever
entered Live, WOM participants are permanently locked: an already dispatched
request may finish and reconcile, but no new roster or delete dispatch/retry is
allowed. A Live end correction can send dates only. Local Live replacement and
evidence history remain authoritative and are never rewritten to match WOM.

Remote deletion is a separate confirmed Admin action available only before the
first actual Live start, with current authority, fresh link/version, protected
credentials, and an explicit target confirmation. It removes only the remote
competition and preserves all local participants, assignments, evidence,
results, and prior source history. Cancel, reopen, unlink, or ordinary lifecycle
changes never imply remote deletion. Public pages and existing statistics
continue to consume cached WOM data read-only.

While and only while the event is Live, the server uses cached competition details on fixed UTC hourly slots anchored to the event's retained actual Live start: the first normal slot is one hour after that start, followed by each successive hour. Starting the event alone does not trigger an immediate normal fetch. Delayed, manual, urgent, and retry requests do not move the anchor; retries remain separately due, and downtime permits one current due refresh before the next future slot rather than a burst of missed requests. A Live record without an actual-start anchor remains unscheduled rather than inventing a rolling cadence. The server never calls Wise Old Man per viewer, player, or team. Every registered regular account contributes to its participant total; Alt/informational accounts are excluded. If the newest result lacks any expected account, public rankings are withheld even when an older complete cache exists. Compatible cached activity remains visible when older than one hour without adding public freshness/status banners. Team average is participant-based and tied top participants share MVP. Wise Old Man availability, configuration, or completeness never blocks event progression.

To limit unwanted submissions, an admin may protect the form with an event-specific signup code distributed through Discord. Admins can close and reopen signups at any allowed pre-draft time. Reopening a form that already has responses requires confirmation and automatic history but no written reason.

The participant authenticates to a website account through Discord or public username/password before normal signup; first-time account creation itself begins through Discord. The resulting event-participant record belongs to that account, and the participant may edit it only while signup is open. Private edit links are not retained. Admins retain audited correction, withdrawal, restoration, and explicit ownership-transfer authority; no participant claim-link system is required.

The system records signup time automatically. Buy-in/payment is not collected from the public participant form. Admins manage one private binary `Unpaid`/`Paid` value on the participant record; it defaults to `Unpaid`. Payment and private Admin notes remain editable for every retained visible event lifecycle, including active draft, Live, Finalized, Archived, and Cancelled; Hidden and Discarded records remain inaccessible. Events without a buy-in may ignore payment. No `Unknown`, `Waived`, or `Not required` states remain in the target model.

Custom questions support Text, Number, Yes/No, Single choice, and Account. Text is a single multiline question type. An Account answer uses the participant's My accounts selector; a missing character must first be added through My accounts. The question configuration determines whether that answer is a user-facing **Regular account** (internally playing/drop-eligible) or **Alt account** (internally informational-only). Comments and availability are not fixed fields; organizers add Text questions when needed.

Before the draft starts, admins may add, edit, delete, and reorder custom questions while signup is open or closed. Before the first accepted/imported response, an existing question's answer shape may change; after the first response, its type, Account role, choice options, stable key, and answer shape are locked. New questions added after the first response are optional, and an optional question cannot become required. Labels, help text, and order remain editable in either pre-draft signup state. Changing a question's format is a delete-then-create operation: the explicit current-impact deletion permanently removes the old question's answers and releases only event registrations created through that question, then ordinary creation adds a new stable question. Draft start freezes ordinary form metadata.

AU05 client baselines: custom/account-field add, custom edit, account rename, move and co-captain enable submit the rendered `SignupForm.version` as `expectedFormVersion`. Missing or malformed baselines fail closed; stale baselines are rejected under the event lock before writes. Delete/disable retain their existing question-version and impact-count confirmation contract. Existing forms forward this token without introducing a new UI workflow. Each settings save returns authoritative values and the resulting event version separately from its immutable submitted baseline. Another card refresh cannot erase pending uncertainty; frontend binding is deferred. No whole-page atomic save or request-identity guarantee is introduced by AU05. Version one has no per-question public/private toggle; removing any stale implementation of that superseded capability is outside the current event-functionality correction.

AU07 response boundary: the ordinary null-to-first-accepted `FirstResponseAt`
transition alone is response metadata and preserves the editable form version.
Any simultaneous definition/settings mutation or explicit Version mark/advance
still advances it. No stale-baseline bypass is permitted. The immutable first
marker, required/type restrictions and all current guards are rechecked under the
existing Serializable event lock. A required custom add after that boundary succeeds
as optional with an explicit `CompletedAsOptional` outcome, explanation and original
committed definition; there is no rejection or backfill. Exact replay/readback uses
AU06 identity plus its uniquely linked immutable creation audit and verified original
intent, preserving the normalization and definition after later edits. Missing or
corrupt creation audit fails closed without returning a guessed definition or writing.

AU06 add retries: each custom-question or secondary Account-field add carries a nonempty
request ID bound on successful commit to the authenticated Admin, event, normalized
intended definition and original submitted form version. Identical retries return
that created field ID without another definition, audit or version change, even
when the original write advanced the form or the event later stopped accepting
new fields. A reused ID with changed intent/baseline, another actor or another
event fails closed and does not disclose the previous result. A new request still
requires the current form baseline and pre-draft write authority. Every attempt
rechecks enabled Admin authority and current event visibility; hidden/discarded
results are unavailable. A deleted/inactive created field is reported as removed,
never recreated or replaced by a same-label field. Preserve the submitted request
and baseline while its result is uncertain. Existing post-first-response optional
normalization and explicit AU07 outcome remain authoritative. These operations
create event form fields only. Ordinary forms gain request identity transport;
frontend draft/uncertainty recovery and manual UI acceptance remain deferred.

Signup forms may therefore differ between participants in the same event. Authorized Admin/private-history views must treat every retained custom answer as optional historical data. A participant who signed up before a question was added has no answer record for that question; the page must show a neutral fallback such as **Not answered** and must never fail because an answer is missing. Existing inactive legacy questions and their retained answers remain authorized private history only and are excluded from every public signup-table projection. Before draft start, deleting a custom question while signup is open or closed uses the explicit current-impact confirmation, permanently removes its answers from the signup form, table, confirmation, and ordinary question/answer views, and releases only its affected event registrations. Global My accounts links, participant status/order, required system questions, and audit/competitive history remain intact. No separate Hide action, disclosure-tracking field, or historical backfill is introduced. The retained signup-board visibility persistence field remains compatibility-only and fixed/defaulted true, not an Admin control; private and co-captain answers remain excluded from public projections.

The unlisted public signup table has separate Confirmed and Waiting list sections and uses each participant's built-in primary regular OSRS character as the event-facing name. Website username and Discord identity are never displayed there. Every regular Account answer shows its EHB; alt-account answers have no EHB. Captain volunteer and all active participant-facing custom answers are public; inactive legacy answers remain private historical data. Version one has no per-question public/private toggle or admin-only custom signup question. Payment and Admin notes remain separate private fields. Alt-account answers do not appear on team rosters, evidence, board progress, or leaderboards.

Required system fields such as primary account/EHB and captain volunteer cannot be removed. Every form-definition mutation creates a new form version and an audit entry.

Signup readiness is mode-specific. **Open now** does not require or use a scheduled-opening value and records the actual opening instant. Scheduled opening requires a valid future scheduled instant. Both modes require a public description, positive capacity, valid event start/end, a valid closing instant no later than event start, an allowed pre-draft state, configured system-wide Discord sign-in, intact system questions, valid custom-question definitions, and a signup code only when code protection is enabled. A secondary Account question must remain optional and identify its answer as playing/drop-eligible or informational-only.

Creating an event with a signup-opening time enables automatic opening by default; without an opening time it is off. Schedule exposes the persisted toggle explicitly, and it may be disabled only before the opening boundary passes. An unchanged already-enabled overdue opening remains historical rather than becoming a newly scheduled value.

A public short/long-text answer and reopening populated signup may be informative warnings in the single confirmation, without separate acknowledgement. Waiting is always enabled. Missing banner art, board setup, teams, custom questions, draft time, or signup-code protection is not a warning. Current Wise Old Man availability does not affect readiness because manual EHB entry remains available.

The scheduler reruns the same readiness contract transactionally at the scheduled instant. If configuration became invalid, signup stays closed and admins receive a visible failure alert. Temporary Discord or Wise Old Man service failure does not rewrite event state; participant-facing operations provide accurate unavailable/retry feedback.

### 18.2 Capacity and waiting list

Signup records have one of these participation states:

- **Confirmed:** Within the current participant cap and eligible for the draft.
- **Waiting list:** Valid signup received after the confirmed-participant cap was reached.
- **Withdrawn:** The participant or an admin marked the signup inactive before the draft.

When the confirmed-participant cap is reached, the signup form remains open until the signup deadline. Additional valid signups join the waiting list in signup-time order.

After a successful create or edit, the authenticated confirmation page shows whether the participant is confirmed or waiting-listed, their exact waiting-list position when applicable, regular accounts and EHB snapshots, separately identified alt accounts, captain-volunteer choice, all submitted custom answers, and whether editing or withdrawal is currently available. The public signup table is a separate projection that excludes website username, Discord identity, payment, Admin notes, security data, and audit data; it identifies waiting-listed participants separately and shows their exact numbered positions.

Normal authenticated navigation includes **My events**. It uses explicit event-participant ownership only, separates current events from history, and links each owned participant to the best destination for the event state: confirmation/edit, signup table, team roster, board, or results. Unowned retained/imported participants remain absent; no new participant-owner transfer is available.

Confirmed and waiting-list signups both reserve every named Account answer against use by another participant in that event. Withdrawal before draft start releases those reservations while preserving historical assignment records. The released character may then be registered by another participant in the same event.

Signup creation and editing are atomic across the participant, answers, event assignments, saved-EHB updates for playing answers, event EHB snapshots, first-response marker, and capacity/status decision. If any selected character is already reserved, nothing from the attempted create/edit is committed. The form marks the conflicting Account answer, asks for another account, and retains all other entered values. A failed edit leaves the last successfully saved signup unchanged.

When an admin increases the participant cap:

- The earliest waiting-listed participants are promoted automatically until the new capacity is filled or the waiting list is empty.
- Promotion order uses the original valid signup timestamp.
- Promoted participants retain their original signup data and authenticated ownership.
- Each promotion is recorded in the audit log.

Example: if the cap is 50 and seven people are waiting, increasing the cap to 60 automatically confirms all seven and leaves three additional confirmed places available.

When a confirmed participant is withdrawn before the draft is locked, the earliest waiting-listed participant is automatically promoted into the open place.

Editing a signup does not change its original signup time, deterministic sequence, confirmed/waiting status, or waiting-list position. While signup is open, a participant may cancel through a separate confirmed action. If they sign up again during the same open window, the existing record is reactivated at the end of the queue with a new signup timestamp and sequence.

After signup closes and before the draft starts, a participant may still withdraw but cannot restore themselves. An admin may restore them before draft start only if their required account reservations remain available. Restoration uses current capacity or the end of the waiting list and never displaces someone who has already been promoted.

Promotion creates a durable in-site notification for the linked participant and every enabled administrator. The admin notification identifies the event, promoted participant, and promotion trigger. The authenticated confirmation page always reflects current authoritative status. Automated Discord messages are not part of this workflow; admins may communicate promotions manually.

Admins use the same confirmed **Withdraw participant** action whether the person can no longer attend, signed up incorrectly, or is otherwise ineligible. The record remains available in the Withdrawn section rather than being permanently erased. Automatic history records the actor and timestamp; a private admin note is optional and no written reason is required.

Automatic waiting-list promotion stops at team-draft lock. During Running, roster
edits remain locked except the explicit permitted preassignment before first pick.
After finalization and before first actual Live, separate Add and Remove operations
republish the current roster without changing original picks or prior versions.
Add uses an existing website account and required Playing assignment; no signup
capacity/team-size cap, questionnaire completion, rebalance or replacement workflow.
First Live permanently locks membership/registration, including after early end.
Current-member role changes remain allowed within their lifecycle window. Disabling
an account removes access, not its roster membership. No Live withdrawal, vacancy,
replacement, promotion-follow-up or participant-owner-transfer workflow remains.

### 18.3 Retained external rosters and operator import

Application CSV import and accountless roster creation are retired. Preserve old
participant, membership, account and actor history without granting new authority.
The separate controlled historical import below is not an Admin UI feature.

### 18.4 One-time frozen historical event import

The platform must support one operator-controlled, one-time import of the archived event **Det Store Danske Sommerbingo 2026**. The imported event uses `Europe/Copenhagen`, runs from `2026-07-14 18:00 CEST` (`2026-07-14T16:00Z`) through `2026-07-19 18:00 CEST` (`2026-07-19T16:00Z`), and has `ArchivedAt` equal to the event end. It is imported directly as `Archived`; it must never pass through a temporary `Live` state and must not use ongoing synchronization.

The public event links to Wise Old Man competition `145197`. The import freezes each source account's approved start EHB, end EHB, gained EHB, and required synchronization metadata. The private mapping contains 90 participants and 93 Wise Old Man accounts across six public board-spelled teams of 15. Secondary account groupings are deterministic: `Ezzi` is primary for secondary `Also Ezzi`, `wolles` is primary for secondary `w olles`, and the unused zero-gain `Coxophobia` is attached to an existing Xen participant. The import never infers current website-account links; source account identity and current website identity remain separate.

Board standings are the official event result. Wise Old Man EHB/activity ranking is a separate supplementary ranking and does not determine board placement; it uses the complete frozen source snapshot without a normal refresh. The frozen official placement order is The Agency, Xen0%_d_rops, Touch Kids, not grass, Morytania Monkeys, Zalamalikum, and Såeh cs?; archived public team cards follow these official snapshots. The board uses the approved English 5×5 tile manifest and the exact corrected 402 counter units across 150 aggregate team/tile cells. The reviewed public manifest SHA-256 is `e5297b20fc5e4a842b6a1e5ab378128cbe1c2bad16033fc875c54607c0d49438`. The eligible-rule manifest is fixed as follows:

- All 20 named catalogue God Wars candidates are eligible.
- Pets are ordinary distinct drops and never joker progress.
- Duke and Whisperer accept any two eligible drops, including duplicates.
- Araxxor accepts only Nid (Destroy) or Jar of venom.
- Vorkath includes both visages.
- Superior Slayer remains one manual target-3 objective with the exact descriptive four-item pool and uses the explicit manual tile value of 21 EHB.
- Royal Titans retains two AND objectives: three Fire crowns and three Ice crowns.
- Wilderness retains three AND objectives: one hilt, one blade, and one gem.
- Maggot King accepts only Elder venator fang or Crimson kisten, repeated to five, and its rounded catalogue-backed tile EHB must be exactly 31.1487;
  Maggot marquess is excluded.

Historical partial units may be assigned in one deterministic fixed requirement order only to reproduce the exact approved aggregate `x/x` values. Reconstructed approved contributions have null item and evidence associations but remain included in public Recent Drops; no drop identity or evidence is fabricated. Their participant attribution is deterministic, weighted toward combined starting EHB, with timestamps spread across the event. Weighted raid counters represent contribution units, not asserted item drops.

The only historical disclosure is exactly: “Historical record — evidence image not retained; player attribution and timing reconstructed from event EHB.” No additional disclosure about multi-requirement allocation is shown. Board points and ignored CSV EHB/rate columns are never converted to EHB; the Superior Slayer value is the explicit approved manual value, not an inferred conversion.

The import is operator-only, audited, transactional, preflight-first, fail-closed, and idempotent only when the import hash exactly matches an already-applied import; a divergent hash fails closed and any apply failure rolls back without a partial event. Apply requires explicit CLI invocation, exact event-name confirmation, and an active SuperAdmin actor validated inside the locked serializable transaction. Versioned public import metadata may contain board definitions, aggregate counters, and source identifiers/hashes, but never the private roster or participant/account mapping. Production import and any later hiding or removal of the production rehearsal are separate post-deployment operations requiring explicit authorization.

## 19. Page inventory

### 19.1 Public pages

- Current event overview
- Event signup form and signup confirmation/edit page
- Bingo board with team selector
- Tile details and approved evidence
- Team overview and roster
- Published draft results
- Team leaderboard
- Player contribution leaderboard
- Event rules and evidence requirements
- Previous events and results

Public boards is an overview of the current public event and previous archived events. Selecting a current event does not automatically redirect visitors into its board; visitors choose the relevant public event surface from the overview.

Hidden events are excluded from every public listing, event history, account
history, submission/evidence view, notification/action destination, and
realtime projection. Their guessed or direct public event URLs return 404.

Archived events keep the same public board/team/tile/result routes. Signed-in current members of an archived event team may read that team's complete retained submission history through `/Submissions` and its detail route, including rows credited to departed teammates. A former credited owner may follow the existing account-history destination to only their own retained Rejected/Withdrawn submission detail and `/Evidence/{assetId}` read-only; no team ledger, teammate record, other private state, or mutation becomes available. Other former members, anonymous users, and cross-team viewers fail closed. There is no separate archived-participant dashboard.

### 19.2 Canonical authenticated submission pages

- `/Submissions` — team-wide retained submission ledger; Captain/co-captain-only
  team-focus and team-submission-status sections; owner/team-scoped editing
- `/Submissions/{id:guid}` — visually equivalent Captain submission detail with
  owner/team-scoped mutation boundaries
- `/Captain` and `/Captain/Submissions/{id:guid}` — compatibility redirects/aliases
  only, never separate rendered implementations

### 19.3 Admin pages

- Review queue
- Event editor
- Guided event creation and setup
- Signup form builder and signup manager
- Board builder and EHB balancing view
- Tile and requirement editor
- Boss/activity catalogue
- Item and drop catalogue
- EHB/rate import and change review
- Event participant manager
- Team and roster manager
- Captain account manager
- Draft control
- Retained/imported roster interpretation; no application CSV import
- Submission corrections
- Results and placement finalization
- Audit log
- Admin account manager
- Events Control Hidden area and limited hidden-event Manage inspection/Restore

## 20. Audit requirements

The audit log records at minimum:

- Submission creation and edits
- Review decisions and reasons
- Metadata corrections
- Approval reversals
- Manual progress or placement corrections
- Event state changes
- Event hide and restore, including the mandatory reason and exact confirmation
- Submission reopening and event unfinalization
- Board changes after publication
- Retained emergency actor history; no new emergency credential operations
- Permitted catalogue edits and event-local EHB overrides (AU11 pending)

Audit entries identify the acting account, time, affected record, previous value, and new value where applicable.

Automatic audit history does not imply that the administrator must type a reason. Routine configuration and lifecycle actions record structured before/after facts without demanding an explanation. A written reason is required only for exceptional competitive or historical overrides where the rationale materially matters, such as ending an event early, correcting/rejecting/reversing evidence, or reopening official results. Finalization blocker overrides and manual result edits are retired.

Historical audit data must not be casually deletable through the normal admin interface.

Every enabled Admin may read the audit log under the normal audit permissions.
Hiding is not an Admin-audit secrecy boundary (C36 closed, user clarification
2026-09-14); no additional suppression or legacy association repair is required.
The limited SuperAdmin Manage inspection still exposes quarantine audit history. The
audit log is a newest-first, server-paginated table with 25 entries per page
and filters for event, actor, action, entity, and date range. Pagination retains
filters and reaches the complete retained history; the 25-row page size is not
a retention limit. Entries cannot be edited or deleted, and version one has no
audit export.

Audit-entry details present structured before/after data in human-readable form while retaining the immutable structured record. Routine successful website-account login updates `last_login_at` but does not create a main-audit entry. Failed attempts and throttling remain security logs. Retained emergency history remains immutable; security-sensitive website password, Discord-link, role, disable/restore and ownership mutations remain durable audit events.

The global **Accounts** area provides normal website-account administration with server-side search/filtering and 25-row pagination. Emergency identities remain retained outside operational controls.

Website-account search/filtering covers public username, global role, active/disabled state, Discord linked/unlinked state, and event participation. Its rows show username, role, state, Discord-link state, last login, and current event-role summary. Details show every linked OSRS character, the last-known Discord display name clearly labelled non-authoritative, event participation, current/historical event/team roles, and disable history.

Every normal account action is projected from current server authority: ordinary Admins may manage User reset/disable, while only the Super Admin receives Admin reset/disable, global-role, and ownership-transfer actions.

The overview never exposes password/OAuth/setup/reset-token data or unnecessary raw Discord identifiers. Version one has no website-account merge or permanent account deletion.

Granting/revoking Admin and restoring an account creates a personal in-site notification for the affected website account. A disabled account receives neutral contact-an-admin guidance rather than exposing the private disable reason at login; the administrator communicates that reason separately.

Lost-owner recovery has no web action. An operator-only command may create a 60-minute owner password-reset link or atomically transfer ownership to a specified active website account when necessary. It requires explicit source/destination identifiers and confirmation and records a system audit event.

Personal notifications and unresolved Admin actions are separate. Opening a personal notification marks it read. Pending reviews and unresolved scheduled opening/start failures remain visible until the authoritative condition resolves. Vacancy, promotion-follow-up and missing-Captain action categories are retired.

## 21. User experience direction

- Every newly created page follows the approved shared UI rules immediately, including common components, density, responsive behavior, accessibility, mutation feedback, and no-JavaScript fallbacks. The later complete UI pass remains responsible for final cross-site consistency.
- Every page reachable by anonymous visitors, normal Users or captains/co-captains supports both English and Danish through the existing language switch. All visible states and feedback on such a page are localized. Pages restricted entirely to Admin/Super Admin may remain English-only.
- The bingo board is the main visual focus.
- The default public board experience uses an overview-and-zoom interaction inspired by macOS Mission Control.
- The overview displays every team's board as a compact preview together with its team name, rank, completed tiles, completed lines, and full-board status.
- Selecting a board expands it into the primary full-size board view for that team.
- The full-size view provides a clear return to the all-team overview and quick previous/next team navigation.
- The transition should preserve spatial context so users understand which team board they opened, while remaining optional or reduced for users who prefer reduced motion.
- A board's overview preview shows progress and status but does not need to make every tile label readable. Detailed tile content belongs to the expanded board.
- OSRS boss and item PNGs supply most of the game identity.
- The surrounding interface should be readable and modern rather than copying the OSRS interface exactly.
- Status colors must be consistent for not started, in progress, pending, completed, rejected, and reversed states.
- Desktop should support whole-board viewing, board editing, and rapid evidence review.
- Mobile should prioritize evidence submission, individual tile details, and standings.
- On small screens, the board may become a scrollable grid or readable tile list rather than shrinking into illegibility.
- On mobile, the overview becomes a vertical or two-column collection of team board previews. Opening a preview navigates to the full team board instead of relying on a desktop-style zoom animation.
- Important status information must not rely on color alone.

### 21.1 Board-builder interaction

- On desktop, admins arrange the board by dragging one tile onto another; dropping swaps their positions and immediately recalculates row and column EHB.
- Dragging must not be the only arrangement method. Keyboard users and touch devices need an accessible select-and-swap or move control.
- Hovering or focusing a row or column in the EHB balance summary highlights the corresponding tiles on the board.
- Hovering or focusing a row/column EHB total next to the board provides the same highlight.
- The highlight must identify the row or column with more than color alone, such as a visible outline and label.
- Tile editing should preserve board context. On wider screens, the preferred interaction is a side drawer or inspector alongside the board so admins can see how edits affect EHB totals.
- On smaller screens, the tile editor may become a full page or full-width sheet.
- A centered blocking modal is reserved for short confirmations and destructive actions rather than the complete tile editor.

Detailed visual design and wireframes will be produced after this requirements document is reviewed.

## 22. Non-functional requirements

### 22.1 Reliability and data integrity

- Approved progress must be reproducible from the submission ledger.
- Reversals must restore the correct previous state.
- Concurrent admin reviews must not apply one submission twice.
- Historical event calculations must use their published snapshots.
- Image upload failure must not create an incomplete submission.

### 22.2 Security

- Public access is read-only.
- Captain authorization is enforced on the server, not only hidden in the interface.
- Passwords are stored using an appropriate modern password hash.
- Uploads are validated and served safely.
- Sensitive admin actions require authorization and auditing.
- Hidden-event quarantine is SuperAdmin-only, fails closed on every ordinary
  event route/workspace, and never grants a public SuperAdmin bypass.
- Rate limiting protects login and upload endpoints.

### 22.3 Performance

- Public boards and team switching should feel immediate at expected event scale.
- Approval should update official progress without requiring a manual spreadsheet refresh.
- Large screenshots should be resized or optimized for viewing while preserving original evidence when needed.
- Expected event scale includes 100 simultaneous connected viewers, not only 100 registered participants.
- Live updates should avoid synchronized full-page reloads across all connected viewers.
- External integrations such as Wise Old Man must be cached or synchronized in the background rather than called once per viewer request.
- The release candidate must pass a production-sized load rehearsal covering public viewing, SignalR updates, evidence uploads, evidence viewing, and admin review.

### 22.4 Accessibility and compatibility

- Core workflows support current desktop and mobile browsers.
- Controls are usable by keyboard.
- Text and controls meet reasonable contrast standards.
- Images include meaningful alternative text where appropriate.

### 22.5 Backup and recovery

- Event data and submission metadata require regular backups.
- Evidence images require durable storage and a recovery plan.
- Recovery procedures should be tested before a live event.

## 23. Version-one acceptance criteria

Version one is ready for a live event when:

1. Admins can create an event, teams, players, captain/co-captain assignments, and a board; normal captain access uses website-account event/team roles and retained emergency credentials have no active authority.
2. Admins can model every objective on the provided example 5x5 board without custom code.
3. The editor calculates tile, line, and total EHB while arranging the board.
4. Captains can submit one screenshot and drop for a player on their team.
5. Captains cannot submit for or inspect pending evidence from another team.
6. Admins can approve, reject with a reason, make reasoned metadata corrections, and reverse approvals.
7. Approval and reversal correctly update all related progress and standings.
8. Public visitors can inspect all teams, completed tiles, and approved evidence.
9. Full-board finish time and provisional placement use immutable server submission time.
10. Submissions close at the cutoff while existing evidence remains reviewable.
11. Event finalization requires an admin action.
12. Website-account captain/co-captain roles remain historical while lifecycle authorization removes mutations; retained emergency credentials are never enabled or used for authentication or mutation.
13. Admins can operate a private snake draft and publish its completed results.
14. Admins can create and publish an event and its website signup form.
15. Participants authenticate to one website account through Discord or public username/password, submit at most one signup per event, and edit it only while signup is open; new normal signups do not receive private edit links.
16. Signups received within the cap become confirmed and later valid signups enter an ordered waiting list.
17. Increasing capacity or freeing a place automatically promotes waiting-listed participants in signup-time order before the draft is locked.
18. Admins can extend the signup window, change capacity, correct/add participants, and withdraw or restore them with confirmation and automatic history.
19. Application CSV roster import is retired; the separate controlled operator historical import remains available under its own authorization.
20. Finalized events remain publicly viewable in event history.
21. Competitive corrections and administrative changes are auditable.
22. Concurrent board edits cannot silently overwrite one another.
23. Only the active draft controller can mutate a running draft; other admins can observe or explicitly take over with an audit trail.
24. A Super Admin can hide/restore eligible post-Live events with shared confirmation, a Hide reason only, complete audit and no notification or data rewrite. No typed-name gate remains. Hidden workspaces stay restricted; ordinary sanitized Audit history remains readable under its existing exception.
25. An explicitly authorized operator can preflight and apply the approved historical event import directly as archived history, with deterministic frozen inputs, exact-hash idempotency, private roster protection, and no production mutation during implementation.

## 24. Decisions deferred to later planning

The following do not block requirements approval:

- Frontend and backend technology stack
- Database and hosting provider
- Image storage provider and retention policy
- Exact backup schedule
- Whether Wise Old Man data is available through a suitable API
- Which OSRS Wiki data should be imported and how often
- Exact visual theme, typography, and board styling
- Detailed wireframes and interaction design
- Whether two-factor authentication is mandatory for admins
- Exact EHB formula and rounding behavior
- Exact tie procedure if screenshot evidence cannot establish obtained order

## 25. Next planning deliverables

No application code should begin until the following planning work is reviewed:

1. Review and correct this product requirements document.
2. Produce low-fidelity wireframes for the public board, tile detail, captain submission, admin review queue, board builder, and draft pages.
3. Define the detailed data model and calculation rules using representative tiles from the upcoming bingo.
4. Define the technical architecture, hosting, authentication, storage, backup, and deployment approach.
5. Divide version one into implementation milestones with tests and acceptance checks.

## Luck percentile and KC comparison — approved implementation, 2026-10-01

This section supersedes the earlier signed, expectation-neutral Luck wording in
section 15.1 and the bounded-score detail in that section's tile contract. The
existing Stats container remains the presentation owner. Luck is a literal
mid-rank percentile on a fixed 0–100 scale:

`100 × (P(X < received) + 0.5 × P(X = received))`.

The calculation keeps the existing bounded binomial distributions, conditional
probabilities, reward rolls, mutual-exclusion roll groups, independent group
convolution and numerical work limits. It does not recenter around the expected
count. A deterministic sole outcome has rank 50; no-activity, missing, impossible
and numerically unsupported observations remain unavailable rather than showing a
fabricated percentile.

The existing Stats Luck container defaults to Luck % and has an in-container
toggle for KC difference. KC difference is calculated independently for every
playing character and boss/activity metric, then summed for the requested
player/team scope. For one deduplicated component, `lambda` is the expected
eligible item outcomes per kill, `r` is the approved eligible outcome count and
`k` is the recorded KC:

`KC difference = r / lambda - k`.

Each character/activity KC is subtracted once. Independent roll groups contribute
their expected outcomes; they are not collapsed to a probability of at least one
drop. Repeated board placements and repeated eligible outcomes do not duplicate
KC. Missing activity, unsupported or zero lambda, invalid mechanics and mixed
unresolved attribution are unavailable. Overall totals sum character/activity
balances; they do not adjust for kill speed or boss difficulty. The UI explains
this with: “KC totals don’t account for differences in boss kill speed.”

Both modes are projections of the same saved calculation snapshot. Approval and
reversal retain a compatible prior result and its original calculation/fetch and
upstream times; they do not recalculate, clear or routinely warn on an existing
result. A successful normal WOM fetch calculates from that returned activity and
the current approved evidence inside the existing synchronization transaction.
Reads do not fetch, rescore or publish. Final review may make one ordinary,
provider-limited refresh attempt before publish/archive; a skip or failure keeps
the prior result and does not block publication. A changed finalization/evidence
state is revalidated before publish.

Historical conversion is a bounded, explicit, idempotent exception. It rebuilds
supported v2 values only from each v1 checkpoint's retained observations, rates
and coherent attribution, preserving original times and recording conversion
provenance separately. It never fetches, applies newer evidence or rewrites
official placements. Missing legacy activity/boss/tile inputs remain unavailable.

# Admin UI — functionality and delivery register

Updated 1 October 2026. Records agreed behavior, remaining decisions and actual
delivery progress for the new Admin UI. Keep completed entries; update their state
and evidence rather than deleting them or leaving everything labelled pending.
Neither a working prototype nor inclusion here means the application supports it.

## Page progress

| Page | Reference / decisions | Application functionality | UI integration / acceptance |
| --- | --- | --- | --- |
| Participants | Approved direction; corrected reference and shared extraction delivered by Claude | F01–F06 backend technically complete; required focused checks and independent Sol 6.1/high final recheck PASS (30 September) | New UI binding and manual application acceptance deferred |
| Dashboard | Agreed composition and metrics; reference delivered | D01–D09 implemented, focused proof executed, independent Sol 6.1/high final recheck PASS (1 October); active checkout evidence is authoritative | D10 new UI binding and manual application acceptance deferred |
| Events directory / Create | Reference completed per user; source comparison 1 October identifies attention/name-limit reconciliation below | Existing capabilities mapped; backend gaps recorded; Events implementation not yet authorized | New UI integration, executed checks and manual application acceptance pending |
| Identity | Narrow left-aligned form accepted; completion source review delivered; named corrections applied/tested by Claude per latest handoff | Existing behavior and real gaps mapped below; no new implementation claimed | Named correction SOURCE PASS (2 October); production UI and manual acceptance pending |
| Event Overview | Reference source review complete; R1–R4 and README correction queued for Codex (RC01) | Existing lifecycle reused; restore-exclusivity defect queued as AU01 | New UI binding and manual application acceptance deferred |
| Signup setup | Reference source review complete; R1–R4 queued for Codex (RC02); shared reuse has no concrete finding | AU02 and AU05–AU07 queued; existing capacity/code/form capabilities retained | Route/tabs/binding and manual application acceptance deferred |
| Schedule | Sol 6.1/high source review complete: changes required; RC03 queued | AU10 source-confirmed preservation/integration gaps; not implemented | Picker binding, stay-on-page, feedback/navigation and manual acceptance deferred |
| Teams / Draft | Reference delivered; user reports latest UI refinements complete; TeamsDraft.dc.html exists | Existing commands/projections confirmed; uncertain-action transport queued as AU14 | Completion source review done; three functional corrections queued as RC04, application UI integration pending |
| Board | Full designer brief delivered after Teams / Draft; completion not reported | AU11–AU13 agreed and queued | Reference review, implementation and UI integration pending |

Status meanings: **designed** = reference exists; **implemented** = application
source changed; **executed** = named checks actually ran; **source-reviewed** =
independent reviewer cleared the named scope; **manually accepted** = user accepted
the actual page; **integrated** = new UI connected to real application behavior.
Track these separately, and distinguish worker-reported results from saved evidence.
Page-specific visual acceptance remains owned by UI_PAGE_MATRIX.md. This register
does not approve future corrections, packaging, deployment or production behavior.

## Ordered application tickets after Luck

The user requested a growing, one-at-a-time application queue on 1 October.
Execution tickets and their acceptance/proof requirements live in the active
[DELIVERY_PLAN.md](/Users/christopher/.codex/worktrees/participants-functionality/BingoWebpage/DELIVERY_PLAN.md),
section **Admin functionality queue — recorded 1 October 2026**. That is the single
execution owner; this register maps tickets to reference pages. Recording a queue
is not dispatch, implementation or approval to package. Luck is technically complete with independent PASS and user visual approval on 2 October. Its disposable demo has been cleaned up.

| ID | Page / application outcome | Current status |
| --- | --- | --- |
| AU01 | Overview: prevent restore creating two visible current events | Queued; source-confirmed defect, proposed rejection contract to confirm at handoff |
| AU02 | Signup setup: enforce 100-character code limit server-side | Queued; source-confirmed defect, no executed reproduction |
| AU03 | Events: duplicate-safe create and uncertain-outcome lookup | Queued |
| AU04 | Events: directory ordering, retained counts and attention data | Queued; reuse completed Dashboard mappings |
| AU05 | Signup setup: stale-edit protection and settings version results | Queued |
| AU06 | Signup setup: duplicate-safe question/account-field adds | Queued after AU05 |
| AU07 | Signup setup: explicit required-to-optional normalization outcome | Queued after AU05/AU06 |
| AU08 | Identity: safe field-level conflict handling | Queued |
| AU09 | Identity: uncertain-save readback without claiming request success | Queued after AU08 |
| AU10 | Schedule: unchanged-instant preservation, field-addressable lifecycle errors and full-state uncertain readback | Source-confirmed; queued, no executed proof |
| AU11 | Board: tile-local manual EHB override for all objective types | Agreed; queued, not implemented |
| AU12 | Rankings: credited EHB before score time; preserve first full-board finish and history | Agreed; queued, historical applicability boundary to resolve at handoff |
| AU13 | Board: manually adjustable planning team size after draft finalization | Agreed; queued, not implemented |
| AU14 | Teams: safe authoritative readback after uncertain actions | Queued; minimal transport contract to resolve at handoff, existing commands reused |

Retain completed tickets and their implementation, executed-proof, independent-review
and deferred UI/manual-acceptance states. Add later reviewed application gaps under
new AU IDs; do not silently turn new design ideas into approved product behaviour.
Participants and Dashboard backend work is complete, not queued for reimplementation.

### Reference correction queue — Codex; canvas sync by Claude

Ownership changed by the user on 1 October: Codex handles bounded behaviour,
validation, recovery and factual wording fixes; Claude focuses on visual design
and syncing verified corrections back to the canvas. The following RC tickets
are authorized and queued, not started. They are separate from AU backend work.
The active DELIVERY_PLAN section Reference corrections owns their scope/checks.

Preserve the approved layout, typography, colour, spacing and animation feel.
Track repository implementation, executed checks, independent review and canvas
sync separately. After review, the corrected repository files are authoritative
for the bounded canvas sync; Claude must not overwrite them with older artboards.
Keep the final changed-file/hash handoff and verify parity after synchronization.
Earlier prompts asking Claude to implement these same fixes are superseded.

- **RC01 — Overview:** correct the permanent-link destination explanation; evidence-code
  failure/stale/uncertain simulations; hidden-event failed-load Restore visibility;
  manual versus scheduled signup-opening eligibility; cancelled Stats README fact.
  Evidence: `/private/tmp/overview-source-review-20261001/`.
- **RC02 — Signup setup:** compare complete intended question values and do not resolve an
  uncertain add by label alone; retain each uncertain settings request's baseline
  across other-card saves; protect dirty inline account renames; say waiting-list
  order rather than signup order. Evidence and exact source lines:
  `/private/tmp/signup-setup-source-review-20261001/review.md` (20 captured files
  stable before/after; no runtime/browser/tests or edits by reviewer).
- **RC03 — Schedule/shared picker:** preserve unchanged exact UTC instants and
  compare full submitted schedule on uncertain readback; retain legacy overdue
  enabled-opening exception; use shared DST-safe conversion for Overview picker
  consumers; keep picker footer reachable in short viewports. Add read-failure
  recovery to readback mock and fix related README claims. Evidence:
  `/private/tmp/schedule-source-review-20261001/review.md`.
- **RC04 — Teams / Draft:** immutable pick identity for Undo/readback; no false
  attribution or safe-retry claims for picks/redraws; match team identity and all
  intended saved fields including inclusion; report actual WOM synchronization
  status separately from locally republished rosters. Queued, not fixed. Evidence:
  `/private/tmp/admin-consolidation-review-20261002/teams-review.md`.
  The reported forced 250ms feedback minimum is a pending presentation choice;
  preserve it for now given the user's preference for brief visible feedback.
- **Identity:** named correction SOURCE PASS, 2 October. Twelve files stable;
  reviewer did not execute browser/runtime checks. AU08/AU09 and UI binding remain
  pending. Evidence: `/private/tmp/admin-consolidation-review-20261002/identity-review.md`.

No concrete shared-token/component regression was found in the Overview or Signup
setup source reviews. Their tests remain Claude-reported, not independent execution.

### Deferred application UI work

Page routes/composition/binding, dirty-state navigation, modal/drawer interactions,
loading/error/recovery, responsive tables, accessibility and manual acceptance stay
with each later UI integration pass. Existing services, answer/impact counts and
state guards should be reused. The approved brief minimum saving/spinner duration
uses the prototype feel as baseline, possibly slightly shortened during integration;
no exact duration is agreed and no backend delay is required. This is separate
from the already corrected CSS-derived exit-animation timing.

## Participants — agreed scope

## Scope and authority

- Visual reference: [Participants.dc.html](Participants.dc.html); canonical canvas
  and export guidance: [README.md](README.md).
- This is the behavior and delivery register for the reference pages, not a replacement for the
  application's functional contracts, data model or delivery plan. Promote each
  accepted behavior into its existing authority before implementing it.
- Preserve the approved design while correcting functionality. No redesign,
  runtime migration or backend implementation is authorized by this document alone.
- The review used the `admin-simplification` checkout at
  `/Users/christopher/.codex/worktrees/735f/BingoWebpage`, commit
  `993c90e9835d1f2d74fc9c03ed4d8f6306a7e37c`.
  The reference lives in a different checkout. Reconfirm the implementation branch
  and current source before starting; do not assume the reference checkout contains
  the reviewed application code.
- These decisions supersede earlier prototype briefs where they conflict,
  particularly the unanswered captain state, payment-free Add flow, fixed first
  primary account, and changes to globally saved accounts.

## Backend implementation authorization — 30 September 2026

The user authorized the backend changes now, independently of Claude's UI
extraction. New UI, drawer routing/binding and manual testing are deferred until UI
integration; focused automated backend checks and independent review are required.

Implementation checkout:
`/Users/christopher/.codex/worktrees/participants-functionality/BingoWebpage`, branch
`codex/participants-functionality`, based on `admin-simplification` at `993c90e`.
Its DELIVERY_PLAN Participants backend pass and reconciled product/functional/data
contracts own execution. Planner: current UI task; orchestrator and independent
reviewer: Sol 6.1/high; implementer: Luna 5.6/max. Current implementation and proof status is recorded below.

Admin management permits event Draft before signups open as well as SignupOpen
and SignupClosed before the team draft is locked. Do not confuse event Draft with
team-draft start. Existing finalized pre-Live roster Add/Remove stays separate;
first actual Live permanently locks registration/membership. Private payment and
notes retain their existing broader lifecycle window.

The latest reference handoff reports its correction checks passed; these are
Claude-reported prototype results, not independent backend verification.

## Approved application changes

### F01 — Explicit confirmation, including when capacity is full

Allow an admin to confirm a selected waiting participant directly.

- With available capacity, confirm without increasing capacity.
- When full, offer an explicit **Confirm and add a place** action. Increase
  capacity by one and confirm that specific participant in the same operation.
- Do not also promote another waiting participant because capacity was increased.
  Preserve the relative order of everyone remaining on the waiting list.
- Keep applicable pre-draft/lifecycle restrictions. This is not permission to
  bypass locked rosters or rewrite competitive history.

Example: a full 60-place event has Alice first and Bob fifth in the queue.
Confirming Bob with the override produces 61 places and confirms Bob; Alice
remains first in the remaining queue.

### F02 — Move a confirmed participant to the waiting list

Keep the option visible but disabled while confirmed places are available.
Explain its unavailability without making it look editable.

When full, move the selected participant to the end of the waiting list and
promote the next eligible existing waiter into the freed place. Do not immediately
promote the participant just moved. Preserve other participants' queue order.

If there is no eligible existing waiter, the action is unavailable and the backend
rejects it without mutation. Moving a team member to Waiting ends current team
membership/leadership authority while retaining its history; the confirmation
must disclose that removal.

### F03 — Restore with an explicit confirmation override

Preserve normal capacity-driven restoration: an available place means Confirmed;
a full event means Waiting, using existing restored signup/queue sequencing.

Also support an explicit **Restore as confirmed and add a place** action when
full. Increase capacity by one and restore the selected participant as Confirmed
atomically, without unintentionally promoting someone else. A Waiting choice
remains visible but disabled while places are available.

### F04 — Admin Add participant without signup questions

The creation flow requires:

- An existing website account.
- At least one of that website account's saved playing accounts, with additional
  selections allowed within the event's configured account slots.
- A primary playing account. The first selection may be the default, but the
  admin can choose another selected account as primary.
- A Paid/Unpaid choice, defaulting to Unpaid, saved with participant creation.

Use stored account EHB. Do not request WOM data during this flow. Do not ask signup
questions, captain/co-captain questions or notes as part of creation. An
admin-created participant defaults to **No** for captain volunteering.

Placement follows capacity, with the explicit confirmation/capacity override from
F01 available when full. Do not allow voluntary Waiting while places remain.

The current application's required-question validation and WOM-dependent creation
path need deliberate reconciliation. Bypassing questions does not authorize
inventing answers to unrelated questions. Creation and its payment/placement
changes must succeed or fail together.

### F05 — Switch the primary playing account

Allow an admin to choose which of the participant's playing accounts is primary.
Exactly one account is primary; that account alone supplies draft EHB.

Switching preserves the accounts and their individual EHB values. Do not sum EHB,
duplicate accounts or lose an account during the switch. Reconcile the current
question-bound primary-account mapping explicitly, and retain applicable lifecycle
locks on edits.

### F06 — Edit event accounts without rewriting saved accounts

Changing an account in an event changes that event's participant assignment only.
It must not implicitly rename, overwrite, add or delete the website account's
saved account records, or overwrite their saved EHB.

Example: the member has saved accounts A and B and entered the event with A.
Switching the event entry to B leaves the saved list as A and B. It must not rename
A to B and leave two saved records with B's name.

Keep the ability to change the account used for the event. This decision does not
restrict all existing-participant editing to saved-account selection only; the
saved-only requirement applies to the Add flow. An unknown name typed during an
event edit must not automatically become a globally saved account.

## Existing rules to preserve and reference corrections

### Withdrawal and automatic promotion

Keep Withdraw and its confirmation. Remove the prototype option to suppress
promotion. Before draft lock, withdrawing a confirmed participant promotes the
next eligible waiter under the existing rules. Withdrawing a waiting participant
does not free a confirmed place. Preserve existing lifecycle restrictions.

### Captain volunteering is Yes/No

Remove Not recorded, Not asked and Not answered as selectable third states.
The supported answer is boolean. Admin-created participants default to No.
Captain volunteering and co-captain requests do not themselves assign team roles.

### Account rows are limited by event configuration

Allow additional playing accounts to be added/removed within the event's
configured account slots. The prototype must not imply unlimited accounts.
Use the real account/question-slot mapping; do not introduce an arbitrary global
limit. Preserve at least one playing account and exactly one primary.

### Saved account links are not exclusive ownership

Shared or borrowed character links can exist across website accounts. Do not add
a global rule that a character can belong to only one website account. Preserve
existing checks against duplicate accounts within a participant and conflicting
assignments within the same event. Add still selects records linked to the chosen
website account.

### EHB precision is not a mandatory visual correction

A reference display such as 12.3 rather than 12.34 is acceptable. Settle display
formatting during integration. Preserve supported input and stored precision;
do not destructively round underlying values to match the mockup.

### Prototype data and tools are not new product features

Do not implement Fail next request, Reset prototype data or placeholder navigation
as application features. Synthetic Discord/source metadata must map to real
supported data; it does not authorize new integrations or signup source types.

## Required integration behavior

These are integration requirements, not claims that production implementation is
complete. Reuse existing supported behavior where it already satisfies them.

- Stable participant IDs drive drawer URLs. Direct links and reloads show the
  Participants workspace and selected drawer together. Opening does not create
  duplicate history entries; direct-entry Close returns to the workspace.
- Close, Cancel, Escape, genuine backdrop clicks and browser navigation respect
  dirty drafts. Discard follows the pending navigation once; Keep editing retains
  the draft without history loops. Forward must not resurrect discarded edits.
- Confirmations are transient, not separate shareable routes. Closing a top
  confirmation does not also close the drawer. Restore focus appropriately and
  retain visible, consistent keyboard focus styling.
- Preserve filters, search, sort, pagination and table scroll around drawer use;
  clamp pagination after changes where necessary.
- Successful save updates the workspace and closes the drawer. Failure preserves
  the draft and presents actionable errors. Guard unload only for dirty drafts,
  acknowledging native browser limitations.
- Add success must not leave a history entry that resubmits creation. Final Add
  route/query details still need to be settled during integration.
- Handle missing records, lost permissions, stale data and changed lifecycle state
  using authoritative application responses, not prototype assumptions.
- Before the wider Admin overhaul is complete, account for old routes deliberately:
  retain, redirect or retire them; do not leave orphaned editors.

## Deferred and unresolved

| Item | Decision / boundary |
| --- | --- |
| Toast Undo | Explicitly deferred. Remove its prototype affordance/callback; do not add backend reversal now. |
| General manual waiting-list reordering | Not approved. Directly confirming a person is not approval for queue editing. Leave this separate reference item pending discussion and identify where it remains. |
| Technical account/question-slot mapping | Resolved in the completed backend pass: existing protected PrimaryRegularAccount question and answer remain the authority; UI binding must use that mapping. |
| Exact Add route shape | Resolve as part of the routing integration; no final query format is specified here. |

## Implementation and evidence tracking

F01–F06 are **technically complete, 30 September 2026** in the assigned backend
worktree. Contracts/data rules are reconciled; the same independent Sol 6.1/high
reviewer accepted the stable source and required executed proof with no remaining
findings or proof gaps. New UI integration and manual application acceptance remain
deferred. No commit, merge, push or deployment has occurred.

| ID | Scope | Contract reconciliation | Application / evidence |
| --- | --- | --- | --- |
| F01 | Confirm selected participant; explicit capacity override | Reconciled | Complete; focused checks and controlled draft-start conflict/recovery proof accepted |
| F02 | Move to Waiting with correct promotion | Reconciled | Complete; promotion/history/notification checks and final source review passed |
| F03 | Restore with explicit capacity override | Reconciled | Complete; same-context rollback, reservation conflict and retry proof accepted |
| F04 | Question-free Add, stored EHB, payment and primary choice | Reconciled | Complete; saved-link negatives, stale/conflict cases and required-answer omission accepted |
| F05 | Primary-account switching | Reconciled | Complete; secondary-primary/EHB projections, stale/no-change and invalid-slot same-context proof accepted |
| F06 | Event-only account edits | Reconciled | Complete; retained history/notifications and same-character EHB/provenance PostgreSQL proof accepted |
| Integration | Routing, dirty state, failures and authoritative validation | Reconcile with existing routes | Pending; new Participants reference not integrated and manual application testing deferred |

### Participants completed evidence — 30 September 2026

Planner reconciled the orchestrator's completion report with CURRENT_STATUS.md and
DELIVERY_PLAN.md in the assigned backend checkout. This records the completed
independent review; the planner did not rerun checks or independently review source.

- Release Web build: 0 warnings/errors, web-build-after-test-fixes.log.
- Focused PostgreSQL run: 7/7, remediation-focused-final.log/.trx.
- Controlled real draft-start conflict/recovery: 1/1,
  draft-interleaving-controlled-proof.log/.trx/.meta.
- Final secondary-primary/stale/invalid-current-slot same-context proof: 1/1,
  primary-invalid-current-slot-proof-final.log/.trx/.meta. Supersedes the earlier
  primary-projection-proof-final run for that affected case.
- Same-character EHB release-and-append history, deterministic microsecond-precise
  WOM provenance and repeat behavior: 1/1,
  ehb-correction-proof-parsed.log/.trx/.meta.
- Evidence folder: /private/tmp/participants-backend-remediation-20260930/.
  Runs overlap; do not sum them as unique coverage or claim a full-suite pass.
- Earlier scoped formatter command/exit 0 was verified from
  format-scoped-final-exact.meta. The later changed-test formatter exit 0 remains
  worker-reported; its log is quiet. Final diff check passed.
- All four original source findings and all three remaining proof gaps are closed.
  Reviewer verified stable source, logs/TRX and command/assembly identity rather
  than independently rerunning tests.
- The earlier silent build stall was generated-output write denial (MSB3371) in a
  checkout outside default writable roots. Narrow authorized escalation recovered
  it; no app restart, user-database changes, cache wipe or broad process kill.
- Implementation remains in branch codex/participants-functionality, based on
  993c90e9835d1f2d74fc9c03ed4d8f6306a7e37c; all dirty work is preserved.
- DELIVERY_PLAN.md, “Completion and UI-binding handoff”, records exact application
  methods, request/result contracts, version checks, primary mapping and refresh
  responsibilities. Next work is a separately assigned redesigned UI integration
  pass, followed by its applicable checks and manual acceptance.

Before each functional slice, update the relevant existing owners:
`FUNCTIONAL_CONTRACTS.md` / `PRODUCT_REQUIREMENTS.md` for behavior,
`DATA_MODEL.md` for affected invariants, and `DELIVERY_PLAN.md` for execution scope.
Record actual completion/evidence here with date and commit when available;
page-specific visual acceptance remains owned by `UI_PAGE_MATRIX.md`.

Use focused executable checks for changed behavior. Capacity, placement,
restoration, creation and account assignment must preserve authorization, audit,
transactions, concurrency, persistence, snapshots and lifecycle restrictions.
Exercise relevant PostgreSQL boundaries for persistence/concurrency changes.
Prototype interactions or source inspection alone do not prove these guarantees.
No broad test framework or unrelated feature work is requested.


## Dashboard — extracted application requirements

Reference: [Dashboard.dc.html](Dashboard.dc.html), shared components/behavior and
[README metric definitions](README.md#dashboard-agreed-metric-definitions-reference).
Purpose: a community statistics/history dashboard useful between infrequent
events, with a compact current/upcoming-event entry point. It replaces the planned
Admin-home composition; it is not a live-event operations console.

This pass extracts requirements only. No Dashboard production code, service,
endpoint, schema, job, provider request or database query was implemented/executed.
The existing Admin Index PageModel contains operational projections, not the new
community statistics contract. Promote the accepted definitions into existing
functional/product/data authorities and plan the implementation before dispatch.

### Dashboard technical completion — 1 October 2026

D01–D09 are implemented, executed at affected boundaries and independently
source-reviewed PASS. D10 remains deferred UI integration/manual acceptance.
Authoritative completion/evidence is in the implementation checkout's
CURRENT_STATUS.md and `/private/tmp/dashboard-remediation-20261001/dashboard-review-final.meta`.
The final focused PostgreSQL run was 8/9 with an initialization-only failure,
followed by that isolated proof passing 1/1; affected original login proof 1/1
and ordering 2/2 are separate runs, not a single clean 9/9 claim. Existing build,
scoped formatter and diff-check proof passed. No packaging is claimed.

### Historical backend planning checkpoint — 30 September 2026

The following checkpoint records pre-implementation planning and is superseded
for current delivery state by the technical-completion entry above.

Draft plan: `/Users/christopher/.codex/worktrees/participants-functionality/BingoWebpage/DELIVERY_PLAN.md`,
“Dashboard backend pass — approved, 2026-10-01”. B1 covers historical population,
people sets and aggregates; B2 adds official recap/EHB/history, current-event card
and community figures. Account-population/card/chronology decisions are approved; Live inclusion was
revised and approved 1 October. Bounded readiness precedes implementation. No implementation, executable
Dashboard proof, independent implementation review or UI integration is claimed.
The implementation checkout's CURRENT_STATUS and MANUAL_TEST_CHECKLIST carry
ownership and deferred journeys. D01–D09 must later retain completed evidence;
D10 remains deferred UI integration, not a backend-completion claim.

### Delivery items

| ID | Application requirement | Reuse / implementation boundary | Current status |
| --- | --- | --- | --- |
| D01 | One authorized historical-event population for every statistic, chart, history row and recap | Reuse Admin authorization and hidden/quarantine exclusion; Live and ended events; ended-only recap; use actual lifecycle timestamps and deterministic event ordering | Implemented; focused proof executed; independent review PASS (1 October) |
| D02 | Four headline figures: events held, unique website participants, total event participations and approved submissions | Add aggregate projection over retained event, membership, identity and submission records; preserve unknown vs zero | Implemented; focused proof executed; independent review PASS (1 October) |
| D03 | Latest-event contribution text in applicable headline cells | New tracked identities in latest event, its participants and its approved submissions; additions, not growth percentages or good/bad performance | Implemented; focused proof executed; independent review PASS (1 October) |
| D04 | Participation chart, first-tracked/returning breakdown and keyboard/touch-accessible details | Same per-event people sets as D02; imported records remain unlinked; chronological event axis; tooltip does not create new analytics data | Implemented; focused proof executed; independent review PASS (1 October) |
| D05 | Latest ended-event recap and current official winner / winner board completion | Use existing official finalization/placement snapshots and retained board information; never infer winner from a provisional live ranking | Implemented; focused proof executed; independent review PASS (1 October) |
| D06 | Optional supplementary EHB gain and account coverage | Reuse stored WOM activity projection / frozen imported snapshots; no provider call triggered by loading Dashboard; distinguish missing, partial and zero | Implemented; focused proof executed; independent review PASS (1 October) |
| D07 | Sortable six-column event history with real destinations | Event/date, players, approved submissions, winner board, EHB, winner; deterministic null-aware sorting and navigation state | Implemented; focused proof executed; independent review PASS (1 October) |
| D08 | Compact current/upcoming event card | Reuse event phase, next milestone and confirmed/capacity projections; choose a deterministic event when several qualify; link to its real workspace | Implemented; focused proof executed; independent review PASS (1 October) |
| D09 | Community account snapshots | Website accounts only, creation timestamps and last-login timestamps already exist; aggregate read required | Implemented; focused proof executed; independent review PASS (1 October) |
| D10 | Application integration and state handling | Route, shared shell, loading/error/retry, empty and limited history, responsive layout, localization, accessibility, motion and Back/reload behavior | Prototype checks passed in named scope; application integration pending |

### D01–D04: population and counting contract

- Exclude hidden/quarantined events from every Admin projection, including
  aggregate totals, returning-history sets, tooltip data and navigation choices.
  Cancelled and discarded events do not contribute to held-event history.
- Headline totals, latest-event additions, chart and history include Live,
  AwaitingFinalReview, Finalized and Archived. Live and final-review figures use
  the label Provisional only. The latest-event recap remains ended-only; the
  current-event card can retain its actual Live phase. This supersedes the
  30 September ended-only decision, approved by the user 1 October 2026.
  Community account snapshots are not restricted to event participants.
- Ended events awaiting final review contribute participation and current approved
  submissions. Mark them provisional; review corrections may change those figures.
  Finalized and archived events contribute official results.
- Count people with qualifying team membership during an event once per event,
  even with multiple playing accounts, membership rows or team moves. Do not use
  current Confirmed signup totals as a substitute for historical participation.
  Exclude membership that ended before actual participation began. Map retained
  participant identity/membership and import roster records explicitly.
- Imported history contributes event and participation totals; never invent
  website identities or match people using mutable character names.
- Unique participants are distinct participating website account IDs across the
  tracked population. Returning means appeared in an earlier tracked event, not
  necessarily the immediately previous event. First-time means first observed in
  tracked history, not a claim about participation before tracking began.
- The first tracked platform event has no returning split. Imported bars remain
  unsplit. Later bars split first-time/returning, and their sum equals each event's
  eligible linked population. Unlinked platform records, if present, need explicit
  coverage handling rather than invented identity (see decisions below).
- For comparable complete linked history, unique people equals the sum of
  first-time people, and total participations equals the sum of event bar totals.
  Hiding/excluding an event must apply consistently when recomputing these sets.
- Approved submissions use the real submission eligibility/source/status rules.
  Exclude reconstructed imported contribution rows; count records, not drop
  quantity, screenshots or reconstructed contribution units. Reversal/correction
  must update the aggregate consistently with the event's authoritative read.
- A latest-event addition describes contribution to the total: +new unique people,
  +participants, +approved submissions. Use actual values, including legitimate
  zero. Omit redundant additions when the latest event supplies the whole metric.
  Keep definitions/coverage/provisional explanations accessible through hints.
  These are not percentage comparisons or cumulative performance trend lines.

### D05–D08: recap, history, EHB and navigation

- Select the latest ended event from authoritative dates, not a sample array.
  Display name, date range, player/team totals, official winner and completed/total
  winner tiles where available; approved count and optional EHB are secondary.
- In final review, show provisional results instead of a winner or winner's board.
  The last official winner may be shown only with its own event name. Reopening
  final review must not present a retained old snapshot as a newly official result.
- Use the applicable current official snapshot, preserve historic team names and
  results, and reuse existing tie/placement authority. Missing official data is
  unknown, not an invitation to calculate an unofficial replacement.
- EHB gained is a period gain, not signup/draft EHB. Reuse retained WOM data and
  frozen imported history, with account coverage (playing accounts, not people).
  Missing or incomplete provider data must be explained, not silently defaulted
  to zero. Dashboard reads must not initiate external synchronization.
- History begins newest first; the reference Event header sorts by event date.
  Support the six shown sorts, stable tie ordering and missing values last in
  either direction. Board comparison sorts by completed/total ratio, not raw
  completed tile count; protect missing/zero denominators.
- Clicking history event names, chart bars, recap Open event or the current-event
  card must resolve the actual event's Admin Overview using its stable identity.
  Go to Events reaches the Events directory. No preview-only placeholder toast.
- Sorting and returning from an event should preserve the Dashboard context;
  choose the precise route/query contract during integration. Direct entry,
  reload and Back/Forward must work without duplicate navigation entries.
- Current/upcoming card shows phase, relevant next date, confirmed/capacity and a
  workspace link. Hide it when none qualifies. Use truthful handling for an unset
  capacity/date and label a Live event as current, not misleadingly as next.
- Sidebar selection is navigation context, not a filter that reduces the global
  dashboard totals to that single event.

### D09–D10: community, states and interaction integration

- Website account total excludes imported/synthetic identity records. Count new
  website accounts since the latest ended event's actual end. When no event has
  ended, the prototype falls back to new accounts in the last 30 days.
- Logged in during the last 30 days uses stored LastLoginAt and one server clock;
  it is not gameplay activity or a reconstructed login trend. Null login dates do
  not qualify. Keep the total/percentage denominator population consistent and
  guard zero accounts. Use supported timezone/date presentation and EN/DA labels.
- No events: one explanatory state with Go to Events, community figures and a
  current/upcoming card if applicable. Imported only: no one-bar comparison,
  expanded recap, unknown unique/submission metrics and their explanation.
  One tracked platform event: tracking-start semantics, no returning claim.
  Multiple events: chart/history/recap. Unknown is distinct from measured zero.
- Loading skeletons, failure message and Try again must connect to real reads.
  A failed load must not show zero totals or fabricate successful data. Retry is
  idempotent/read-only; discard outdated responses when a newer load wins. Reuse
  existing auth/error handling for lost permission or a now-hidden event.
- Preserve light/dark themes, standard shared focus, sidebar/menu behavior,
  keyboard chart access, Escape tooltip dismissal, touch-accessible explanations,
  sort announcements and appropriate focus recovery on errors/retry.
- Reuse shared CSS/motion and reduced-motion behavior. Sorting fades and chart
  tooltip movement are presentation, not delays or extra server operations.
  EHB can drop first at narrow widths; the table scrolls inside its surface and
  mobile navigation works. Do not ship canvas support.js/vendor runtime.
- Do not ship sample-history switches, Fail next request, future sample clocks,
  fabricated account totals, design-only toasts or placeholder navigation.

### Definition decisions and remaining implementation questions

These are bounded data/edge decisions, not reasons to redesign the reference.

1. **Population — resolved, 1 October 2026:** Live contributes to statistics,
   chart and history with Provisional labelling, as does final review. Recap is
   latest ended only. Reference copy/population synchronization belongs to Claude;
   backend implements the revised PRODUCT_REQUIREMENTS/FUNCTIONAL_CONTRACTS.
2. **Identity coverage:** unlinked people count per event without invented global
   identity. Readiness maps actual sources and reports coverage limitations.
3. **Chronology/card — approved:** actual end selects the ended recap/new-account
   boundary; actual start defines earlier returning cohorts. Card priority is
   Live, next scheduled preparation, then unscheduled setup. Stable display ties
   cannot create false returning identity or competitive winner tiebreaks.
4. **Account population — approved:** disabled website accounts remain in registered
   totals and historical distinct people; emergency credentials are excluded.
   Stored EHB and official snapshot mapping remain technical readiness work.

### Focused proof for future application work

Use existing fixtures and test boundaries; this is a risk-based scope, not a new
test framework or an exhaustive UI matrix.

- Mixed imported/platform history plus multiple accounts and team moves: exact
  player, unique/returning and submission totals agree across all projections.
- Hidden/cancelled/discarded and pre-event departed memberships excluded; archived
  and final-review records included as agreed; provisional/official reopen handled.
- Reconstructed import contributions excluded while real approved zero remains
  zero. Rejected/pending/reversed records do not inflate approved totals.
- Retained official snapshot and board denominator, missing/partial/zero EHB,
  website-vs-import account population, null logins and exact time boundaries.
- No-event/import-only/first-tracked states; multiple eligible upcoming events;
  null-last stable sorting and real event links. PostgreSQL query proof where
  grouping, membership interval or timestamp precision affects correctness.
- During UI integration: real route/Back/reload, auth failure, controlled load
  failure and retry, keyboard/touch details and narrow layout. Manual visual
  acceptance is page-specific and separate from backend correctness.

### Dashboard reference inspection — 30 September 2026

Planner inspected the local exported page at
http://127.0.0.1:5196/Dashboard.dc.html. This was a scoped design/behavior check,
not an independent source review or a production data audit.

- Viewed full history in dark mode and imported-only layout in light mode.
- Keyboard focus exposed the chart's full participant breakdown; Escape dismissed
  it. Sorting Players produced descending 96, 90, 84, 72.
- Observed full history (143 unique, 342 participations, 1,802 approved), imported
  only (90 participations, unavailable unique/submissions), no-event/community,
  and imported + one platform event after retry (72 unique, 162 participations,
  486 approved). Values matched the reference's stated fixture definitions.
- Exercised loading, simulated failure, focus on Try again and successful retry.
  These are prototype results, not proof of application recovery or persistence.
- Claude separately reported both themes at 390–1440px, Participants/Components
  regression checks and reduced motion. Those wider checks were not repeated
  independently in this pass.
- No design blocker found in the inspected states. The user considers the page
  done and is sending one additional correction; final acceptance of that
  correction is not inferred here. The observed export's SHA-256 was
  18ef82f42dc3a277b55f58c18d56927c197f1edddb259e3ca548be6226d525f6.
- No reference HTML/CSS/JS or README was modified by this documentation pass.
  Shared component extraction is delivered reference work; real Dashboard
  functionality, route binding and backend tests remain pending.

## Events — agreed design brief, 30 September 2026

Purpose: find an event, understand its situation, and open its workspace or create
a new private draft. These are design decisions, not a claim that the new reference
or application integration is finished. Claude retains composition freedom within
the Participants/Dashboard shared visual system.

### E01 — One directory table with filtered views

- All (default), Current/upcoming, and Past filter one table; do not render
  separate tables for these groups.
- Current/upcoming covers Setup, Signups open, Signups closed and Live. Past covers
  Final review, Finished, Archived and Cancelled, each honestly labelled.
- Default order: Live first, then upcoming/setup events, then past newest first.
  Exact stable date/tie handling can follow existing authority during integration.
- Search by event name, phase filtering and sorting. Selected filters, search and
  sorting are reflected in URL state and survive opening an event and Back.
- Tables remain horizontally scrollable whenever needed, including mobile. Do not
  replace this with cards or silently remove required columns to fit the viewport.
- Hidden/quarantined events use a separate SuperAdmin-only view of the same table
  component. They are excluded from ordinary views and their counts.

### E02 — Row information, attention and destinations

- Show name, phase, event dates, participants and a compact Needs attention value.
  An unset schedule is labelled Not scheduled.
- Show confirmed/capacity during preparation, with waiting count where relevant;
  use a meaningful participant count for past events rather than suggesting open
  signup. Reuse actual retained data; do not fabricate missing import capacity.
- The event name opens that event's Overview. No redundant Workspace button or
  action menu without an identified directory action. Slug need not occupy a row.
- Needs attention shows one priority summary: actual failure first (for example
  Start failed), otherwise pending review work (for example 12 to review),
  otherwise a quiet dash. Several issues may show a compact +N suffix. Define
  the unit consistently with the existing action projection; a review queue
  summary must not accidentally count each submission again as an extra category.
- Ordinary unfinished setup is not a failure. Do not stack failure, pending review
  and setup-blocker badges in each row. The event Overview explains the full set.
- Cancellation, archive and discard remain in the event workspace with appropriate
  confirmation. No Events drawer is currently required.
- Separate No events yet from No matching events. Offer Create for the former and
  Clear filters for the latter, retaining ordinary Create access.

### E03 — Create event modal and journey

- A small modal collects required name and editable timezone, initially
  Europe/Copenhagen. Other configuration follows in the event workspace.
- Create saves a private draft and opens its Overview. Preserve existing
  authorization, unique slug generation, audit and atomic creation semantics.
- Validation/save failure keeps the modal open and preserves entered values.
- Cancel, Escape and backdrop dismiss an untouched form; changed values require
  the shared discard confirmation. Discard closes; Keep editing retains values.
- While saving, prevent duplicate submission and dismissal until the outcome is
  known. Failure/timeout must release the pending state with a safe recovery path;
  uncertain outcomes must not lead to duplicate event creation.
- A direct Create URL renders the Events directory with the modal. Closing direct
  entry returns to the directory; opening from a filtered directory and cancelling
  preserves that context. Browser navigation follows the same dirty-state rules.
- After successful creation, Back/reload must not resubmit the create operation.
- No description, dates, capacity, artwork or signup questionnaire in this initial
  modal. No automatic public exposure or opening signups.

### E04 — Design and delivery boundaries

Reuse shared shell, tokens, components, keyboard focus, hover, motion, reduced
motion and overlay behaviors in both themes. New reusable components/variants may
be introduced where justified, with the same visual language and preview coverage.
Include loading/failure/retry, empty/filter-empty, permission and modal validation/
dirty/saving states. Route behavior must be described even where the canvas uses
simulated navigation. Prototype tooling is not application functionality.

| ID | Decisions | Reference | Application / verification |
| --- | --- | --- | --- |
| E01 | Agreed | Completed per user | Search/phase/sorts/Hidden partly reusable; grouped views, ordering, attention filter and authoritative URL queries pending |
| E02 | Setup-blocker removal source-confirmed applied | Completed per user | Retained player counts, nullable capacity/import provenance and category-priority attention projection pending |
| E03 | Create 50-code-point limit source-confirmed applied | Completed per user | Atomic creation in existing Create PageModel reusable; durable request-key replay/outcome lookup absent; modal/navigation binding pending |
| E04 | Agreed | Shared reference delivered | Application integration, executed proof and manual application acceptance pending |

No new Events implementation, independent implementation review or manual application
acceptance is claimed. A read-only reference/application comparison is recorded below.
Promote changed application behavior to its existing authorities before backend
implementation; this brief does not authorize packaging or a broader lifecycle
redesign.


### Events functional completion comparison — 1 October 2026

Source comparison by `/root/dashboard_backend/events_comparison`, Sol 6.1/high,
against `codex/participants-functionality`. No source edits, executable tests,
browser inspection or database access occurred. This is source evidence, not
executed application proof. Existing Participants work is preserved.

1. **Durable Create recovery — backend gap.** Create.cshtml.cs:76 generates a fresh
   event/slug on another valid POST. The reference README:358 calls for a durable
   request key and outcome lookup (Check again). PRG does not resolve a lost
   response. Preserve at-most-once creation under concurrent retries; exact
   persistence/authorization/receipt design belongs to an approved Events plan.
2. **Directory ordering — bounded projection work.** Index.cshtml.cs:217 groups
   past events by phase and puts missing preparation dates first. The approved
   reference requires past newest across phases and unscheduled preparation last.
3. **Counts and provenance — projection work.** Index.cshtml.cs:81 uses current
   Confirmed for all phases and converts null capacity to zero. Live/past players
   must use retained participation, with nullable capacity and import coverage.
   Reuse Dashboard's mapping where applicable; no new persistence assumed.
4. **Attention priority/filter — projection work.** Index.cshtml.cs:187 and
   SharedShellService.cs:414 use pending-submission count plus scheduled-failure
   count. Directory needs failure priority and +N issue categories; keep the
   shared inbox's existing units intact. WOM failure is a synthetic reference
   example, not an existing mapped directory action source. User accepted this
   illustrative mock on 1 October; no fixture replacement or new WOM attention
   capability is required.
5. **Setup blockers — unapproved reference discrepancy.** README:281 and
   Events.dc.html:513 count Teams not drafted as an attention category, unlike
   approved failure → review → quiet dash. User approved removing ordinary
   setup blockers from attention/filter counts on 1 October; retain actual
   failed-transition context. Reference correction remains pending; no new setup
   attention capability is authorized.
6. **Name limit — reference mismatch.** Events.dc.html:679 allows 200 characters;
   Create.cshtml.cs:59 effectively enforces 50 Unicode characters, consistent with
   WOM title rules. The annotation's 200 is not effective acceptance. User approved aligning reference Create and Identity
   to the existing effective 50-character limit on 1 October. Reference correction
   remains pending; raising the production limit is not approved.
7. **Query/navigation/mobile — integration work.** Existing site.js:532 only
   filters already-rendered rows, so widening a filtered/Hidden view needs an
   authoritative query/reload. Existing mobile CSS turns rows into cards; the new
   table must remain horizontally scrollable. Bind direct Create modal routing,
   dirty/saving/recovery, duplicate-name notice, distinct empty states and
   Back/share context. Sidebar intentionally starts default All. Any pagination
   proposal needs a demonstrated requirement; this comparison does not authorize
   adding it automatically.

Concrete source paths are under
`src/Bingo.Web/Pages/Admin/Events/{Create,Index}.cshtml.cs`,
`src/Bingo.Web/Navigation/SharedShellService.cs` and
`src/Bingo.Web/wwwroot/{js/site.js,css/site.transitional.application.css}`.
Reference paths are in this folder. Line numbers identify the reviewed revision.

Reference role switching, request maps, injected outcomes and simulated browser
history are intentional fixtures; never ship these as production behavior.
Keep reference completion, backend implementation, executed proof, independent
implementation review and manual application acceptance separate.


## Identity — completion and correction handoff

The user accepts the narrow left-aligned form composition. Read-only Sol 6.1/high
comparison by `/root/dashboard_backend/identity_review` found substantially sound
reuse of shared tokens/components/behavior and shared Components specimens.
No local theme colors or duplicate animation timings were found; simulated request
delays and page-specific widths are not extraction defects. This source comparison
did not execute browser checks or application tests.

### Named reference corrections — source recheck passed, 2 October 2026

Claude's latest user-supplied handoff reports canvas/repo synchronization and
passing focused corrections plus Identity/Participants/Events/Components dialog
and focus regressions. These are Claude-reported results, not independently
executed proof. No production implementation, commit or manual application
acceptance is inferred.

- Permanent link displays/copies the absolute existing /Events/{slug}/Signups
  destination. The earlier bare-route proposal is withdrawn. Use configured
  public origin in the application, never the reference's bingo.example origin;
  existing visibility and onward Teams/Board routing remain authoritative.
- Leaving an uncertain save offers Check again / Leave anyway and explains that
  saving may already have happened. It never claims changes were not saved.
- Readback confirms values present with Up to date wording, not proof that this
  request saved them. No new Identity request receipts are required for that wording.
- Schedule-only timezone conflicts retain visible stale feedback/focus and require
  fresh review of current dates, separately from field-level conflict notes.
- Active dialogs lock page scrolling. Shared focus helper handles Shift+Tab from
  the container and holds focus there while every control is disabled.
- Description/buy-in validation matches existing UTF-16 limits; event-name
  validation retains the approved 50-Unicode-code-point rule.
- Common Events/Identity heading-focus CSS moved to the shared owner.
- README no longer incorrectly claims missing unchanged-unsupported-timezone
  handling, stale-version refresh, browser departure guards or editing restrictions.

Independent Sol 6.1/high recheck confirmed all named corrections and their direct
shared consumers: SOURCE PASS, 2 October. No broad review or redesign was performed;
12 captured files stayed stable. Browser checks remain Claude-reported. Application
AU08/AU09 and UI binding remain separate queued work.

### Existing behavior versus remaining integration

Existing application already has permanent slug ownership, Live/Final Review
text editing, first-Live timezone lock, unchanged stored unsupported timezone
handling, stale version refresh and browser departure warnings. Preserve these.
New binding includes stay-on-Identity success/no-change, field-level latest-value
comparison / Use theirs, readback for Check again, shared form/readonly/copy/save
presentation and Unicode-aware name input. Existing stale retries refresh the
version but do not provide the reference's safe untouched-field merge; do not
misdescribe this as an endless stale retry. Legacy prose needs reconciliation to
approved simplification without reopening the approved state rules.

Events setup-attention removal and Create's 50-code-point limit were independently
source-confirmed in this comparison. Those two corrections are applied, while
application integration remains separate. WOM failure fixture remains accepted.

## Event Overview — agreed brief basis

Purpose: understand the event's current situation, progression and required next
action, with applicable manual lifecycle controls. Full progression can remain
visible; only relevant phase controls appear. These are behavioral requirements,
not a prescribed stepper/card/panel layout.

Preparation requirements are neutral and tied to the next transition (for example,
Before you can open signups). Completed items may show checks. Do not label normal
unfinished preparation as errors or repeat blocker counts. Actual failures and
pending review work need distinct, concise attention treatment.

Scheduled transitions and relevant manual actions are presented together. Preserve
manual open/close/reopen signups, start/end and permitted recovery operations with
existing authorization/readiness/state rules and consequence-specific confirmation.
Identity/Schedule/Participants/Draft/Board/WOM own their detailed editing;
Final Review owns official publication and reopening of results. Overview summarizes
and links. Keep relevant public destinations/copy actions, phase-appropriate counts,
milestones and visibility. Destructive controls stay secondary; legitimate recovery
does not automatically get destructive styling.

Claude has composition/copy freedom within the shared system. Give available facts
and phase examples without mandating exact cards, wording or an exhaustive visible
list. Keep it short and informative; confirmation consequences must still be clear.
New visual ideas using supported data are welcome; new data/behavior is proposed
separately. Include applicable evidence-code/upload-recovery/hidden-event capabilities
from the active contract rather than silently dropping existing permitted operations.
No Overview production changes or acceptance are claimed.


## Schedule — handoff recorded, 1 October 2026

Claude reports Schedule.dc.html, Overview picker consumers, shared picker,
Components and README synchronized with canvas version 31; no production edits or
Git publication reported. All validation, route, theme, keyboard, reduced-motion
and regression checks are Claude-reported, not independently verified. Independent source comparison is complete: **CHANGES REQUIRED**, R1–R4.
All 15 captured files remained stable; no runtime/tests/browser checks were run.
User supplies visual acceptance. Evidence: `/private/tmp/schedule-source-review-20261001/review.md`.

Track every remaining item separately:
- AU10: preserve unchanged seconds/subseconds and repeated-hour historical instants
  (existing app parser gap); map lifecycle errors to fields while retaining existing
  parsing ModelState errors; authorized full-state uncertain readback, not minute-only
  comparison. Exact expected proof and boundary live in DELIVERY_PLAN.
- RC03: four named reference fixes and qualified README/read-failure recovery; queued.
  Shared reuse otherwise confirmed; no whole-site extraction or redesign needed.
- UI integration: shared picker with unchanged event-local posting semantics and
  server DST/five-minute authority; remain on Schedule after successful save;
  navigation, confirmation, stale/error/uncertain states and modal focus binding.
- Authority reconciliation: outdated FUNCTIONAL_CONTRACTS section 4.4 must reflect
  approved editable start/end during drafting and overdue pre-Live start repair.
- Shared 600ms minimum saving feedback is Claude's proposed reference setting;
  the user approved a brief minimum, not this exact number. Keep actual network
  completion/failure truthful and separate prototype delays from application work.
- Fixed overlap fixture is acceptable illustrative data, not real validation proof.
- Review shared token/component reuse and Overview picker consequences without
  reopening unrelated RC01/RC02 findings or redesigning approved composition.

No Schedule implementation, independent PASS or manual acceptance is claimed.

## Teams / Draft — agreed design scope, 1 October 2026

One workspace adapts through Setup, Running and Finalized. An admin operates the
snake draft while streaming it; captains call out picks. No captain-controlled
collaborative draft feature is authorized. Existing admin controller/takeover
protection remains. Clicking an available player immediately picks them; retain
latest-pick Undo, no extra selection step or per-pick confirmation.

The active draft automatically compacts the sidebar and prioritizes all teams/rosters and all available players' primary
account and EHB at once. Typical events: 3–5 teams and about 50 or fewer players,
1080p/1440p screens; larger summer events exist. Fit typical drafts without scroll
while preserving stream readability; no separate fullscreen mode is requested. Use deliberate overflow for larger cases rather
than unreadable shrinking. Keep current turn, counts and Undo easy to see.

Remove the standalone Participants-style table during setup and after finalization.
Keep the actual available-player pool during drafting. Setup roster assignments
use a searchable participant selector showing primary account, EHB and Captain
volunteer status. Payment/signup timestamps and event withdrawal stay on Participants.
Remove team affiliation/image controls; retain stored historical values without
new deletion or database cleanup. Keep team name and draft inclusion settings.

Condense readiness/distribution text; keep team settings out of the active draft,
control ownership secondary, and Cancel apart from routine picking. Preserve
manual teams, derived balanced snake turns/preassignments, required usable Captains,
zero/one included-team direct roster publication, and pre-first-Live finalized
Add/Remove corrections preserving original picks/publication history. No Pause/
Resume, finalized Reopen, CSV roster imports or accountless participants. Finalize
publishes rosters, not the board or event start. Team totals are existing data and
may be shown compactly. Designer retains composition freedom within shared system.


Teams / Draft delivery checkpoint: Claude delivered the reference and the user now
reports the follow-up UI changes complete. Requested refinements remove empty
future-pick rows so actual team rosters grow as the available pool shrinks, add
visible draw/scramble feedback, and slightly shorten draft-action loading feedback.
Independent completion comparison is now complete: the reference requires three
functional corrections tracked in RC04. In-place Pick/Undo/Scramble, control renewal,
search inputs and readiness/distribution mostly already exist; new transport for
uncertain-action reconciliation is tracked as AU14. Bind existing command eligibility
and synchronization results. The fourth review item, a forced 250ms feedback minimum,
remains a presentation choice; no automatic removal is authorized. All 19 captured
sources remained stable. No browser/runtime checks were executed by this reviewer.

Teams / Draft integration classification: retain existing domain workflows; AU14
records the now-source-confirmed uncertain-action transport gap. The
layout, automatic compact sidebar, immediate row picking through the existing
Pick action, assignment selector and removal of redundant controls are new UI
integration work. The later completion review may identify concrete binding/data
gaps. Existing capabilities must not be reimplemented or retained data deleted.

## Board — agreed designer brief delivered, 1 October 2026

The full Board brief has been delivered to the user after Teams / Draft. Designer
completion has not been reported. No Board implementation or reference review is claimed. Board remains a grid workspace;
simplifying the complicated tile editor without losing functionality is a primary
objective. Choose drop/manual type once per tile, preserve homogeneous multiple
objectives, optional advanced counting/weights, automatic descriptions and manual
text overrides. Keep optional custom tile image upload/replacement/removal.
Condense control-ownership/readiness copy, retain preview and approval/publication
boundaries, preserve evidence locks and current public snapshot during correction.
Remove the actual-team EHB-per-player breakdown. Keep expected players per team
manually adjustable even after draft finalization, without additional explanatory
UI text; planning only, no roster/scoring effect.

Approved functional direction (not implemented): allow a tile-local manual total
EHB override on every tile type; use calculated EHB when absent, require manual
estimate for non-drop challenges. Effective EHB affects ranking/statistics/board
balancing. Keep calculated baseline/reset for calculated tiles without modifying
catalogue values. Separate points model deferred. Move credited EHB ahead of
current-score time after equal lines/tiles, preserve first full-board finisher wins
and immutable historical results. Existing proportional partial credit retained.
Evidence-protected scoring remains locked; overrides are board-design inputs, not
permission to rescore submitted history. Application tickets AU11–AU13 now own
these agreed changes in the active DELIVERY_PLAN, with focused acceptance/proof
and historical compatibility requirements. All are queued: no implementation,
executed proof, independent PASS or new UI acceptance is claimed.

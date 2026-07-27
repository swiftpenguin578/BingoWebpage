# Slice 3 — Event Creation and Lifecycle Plan

**Status:** Slice 3 was accepted on 2026-07-27 after independent review, manual acceptance, and the final automated gate. Visual review remains deferred to the full UI overhaul.

**Prepared:** 2026-07-27

**Depends on:** `PRODUCT_REQUIREMENTS.md`, `FUNCTIONAL_WORKFLOWS.md`, `DATA_MODEL.md`, `TECHNICAL_ARCHITECTURE.md`, `IMPLEMENTATION_ROADMAP.md`, `UI_OVERHAUL_ROADMAP.md`

## 1. Objective

Slice 3 makes the event itself a safe, multi-session administrative workflow:

- preserve the approved guided event-creation experience;
- require only event name and a valid timezone to save a private draft;
- configure identity and schedule independently after creation;
- evaluate explicit readiness gates before lifecycle transitions;
- support correct manual and scheduled signup transitions;
- attempt, postpone, and later manually complete scheduled event starts;
- enforce one production current/public operational event;
- discard empty experimental events without losing the slug tombstone;
- cancel populated pre-live events without deleting their history;
- archive finalized events into read-only public history.

Slice 3 establishes the shared lifecycle/readiness structure that later slices extend. It does not implement the final Slice 4 signup form or redesign protected board and draft surfaces.

## 2. Explicit boundary

### Included

- Preserve the current five-step guided creation interaction and review step.
- Make description, schedule, capacity, existing signup configuration/questions, buy-in information, and preliminary board dimensions optional at initial creation.
- Remove expected team count and expected roster size from event creation.
- Add optional draft time; keep submission cutoff internal and derive the normal value from event end.
- Default timezone to `Europe/Copenhagen` and use a supported-timezone selector.
- Generate a unique slug, allow editing until first public exposure, then keep it stable.
- Add optional managed event-banner upload, replacement, and removal.
- Add route-backed event identity and schedule editors, enhanced as dialogs over the event overview where appropriate.
- Add separate draft-save, signup-opening, event-start, and later-finalization readiness results with blockers, warnings, and later tasks.
- Correct manual and scheduled signup opening/closing semantics.
- Add scheduled-start attempts, postponed-start actions/notifications, and explicit manual completion.
- Enforce non-overlapping signup windows and the live/review/finalized current-event singleton in normal production transition commands.
- Add `CANCELLED` and `DISCARDED` lifecycle states and their preservation rules.
- Complete archive/current-event navigation behavior without changing official snapshots.
- Preserve Development-only multiple lifecycle scenarios through explicit IDs/slugs.
- Keep lifecycle mutations transactional, authorized, audited, concurrency-safe, and idempotent where scheduled workers may retry.

### Deferred

- Final authenticated signup selectors, dynamic Account questions, capacity/waiting-list redesign, participant administration, and signup-board redesign — Slice 4.
- Final teams, captain/co-captain authority, external-team workflows, and draft changes — Slice 5.
- Final board derivation, approval, preview, and publication workflow — Slice 6.
- Live participant/captain navigation, swaps, and focus — Slice 7.
- Evidence workflow changes — Slice 8.
- Event end, live replacements, finalization workflow changes, archived-participant evidence access, and broader closeout notifications — Slice 9.
- Wise Old Man integration — Slice 10.
- Permanent global Rules and source-controlled how-to pages — the selected public-guidance slice.
- Complete page-by-page visual overhaul and regression — Milestone 9.

### Preserved compatibility

- Slice 3 readiness consumes the currently implemented signup-question, draft, team/captain-access, board-publication, and finalization data. Later slices may extend the evaluator without replacing its structure.
- Existing public URLs, finalization snapshots, evidence history, and audit history remain authoritative.
- The existing Development scenario seeder may create several labelled lifecycle states only in Development.
- Protected public-board, board-editor, and live-draft layouts and interaction models are not redesigned.

## 3. Approved behavior

### 3.1 Guided event creation

- Keep the existing five-step guided creation interaction.
- Only a valid event name and supported timezone are required to create the private draft.
- The timezone defaults to `Europe/Copenhagen`.
- Optional creation sections remain available so an admin may configure more immediately.
- A completely blank optional section does not block creation.
- If an admin supplies part of an optional schedule, question, or planning value, the supplied data must be valid or cleared. Invalid entered data is never silently ignored.
- Creation is one transaction covering the event, any valid supplied configuration, and the required audit entry.
- A failure leaves no usable partial event.
- Successful creation redirects to the event overview/setup workspace.

The five steps remain:

1. **Event details:** name, supported timezone, optional description, optional managed banner, and generated/editable slug.
2. **Schedule and capacity:** optional signup opening/closing, optional draft time, optional event start/end, and optional participant capacity. The normal submission cutoff is internal and automatically derives as event end plus 30 minutes.
3. **Signup form:** preserve the current optional signup configuration/questions until Slice 4 replaces their final behavior.
4. **Planning details:** optional buy-in information and preliminary board rows/columns. Do not include prize information, team count, or roster size.
5. **Review and create:** clearly distinguish values saved now from later readiness work.

### 3.2 Event identity

- Duplicate display names are allowed; normalized slug uniqueness remains server-enforced.
- The slug is editable until `first_public_at` is set and immutable afterward.
- Changing the display name after publication never changes the slug.
- Description is optional while private and blocks signup opening when missing.
- Banner absence is never a blocker or warning.
- Banner images use managed upload, not external URLs.
- Identity changes are authorized and audited without storing image bytes in audit details.
- Invalid slug, timezone, or banner changes leave the last valid state intact.

Timezone changes:

- preserve every stored UTC instant;
- are ordinary edits while private;
- require a preview/confirmation of old and new local displays after signup first becomes public;
- additionally require a reason after the event starts;
- reject stale confirmations when another admin changes the underlying schedule first.

### 3.3 Schedule

All schedule instants are nullable in an incomplete private draft and stored in UTC.

- `draft_at` is optional and informational; it never starts the draft.
- Event start and event end are required before signup can open.
- Event end must be after event start.
- Submission cutoff is internal and automatically derives as exactly 30 minutes after every normal event-end assignment or change; it is unset when no event end exists.
- An approved submission reopening retains its independently governed cutoff and is not replaced by normal schedule derivation.
- Starting the draft closes and locks signup even when the configured closing time is later.

Signup opening modes:

- **Open now** ignores any stored scheduled-opening value and records the actual opening instant.
- **Schedule opening** requires a valid future opening instant.
- Both require a valid signup closing instant no later than event start.
- Manual opening preserves a valid explicit future close.
- If no valid close exists, manual opening proposes future draft time when it is no later than event start, otherwise event start, before confirmation.
- A proposed close is never persisted until every readiness, acknowledgement, confirmation, overlap, and transition check succeeds.
- Closing and reopening remain explicit pre-draft lifecycle operations; reopening a populated signup is a warning requiring acknowledgement, not a typed reason.

Schedule edits after publication require confirmation showing old and new participant-facing times. Editing a time after that time has passed additionally requires a reason and must not rewrite lifecycle history.

### 3.4 Readiness

Use one shared evaluator that returns stable blocker/warning/later-task codes plus localized/admin-readable descriptions.

Readiness levels:

1. **Draft saved**
2. **Signup ready**
3. **Draft ready**
4. **Event ready**
5. **Finalization ready**

Slice 3 implements the common structure and every condition backed by currently authoritative data.

Signup-opening blockers include:

- missing/invalid public description;
- missing/non-positive participant capacity;
- invalid event start/end/closing schedule;
- invalid requested opening mode;
- disallowed lifecycle or draft-locked state;
- unavailable system-wide Discord authentication configuration;
- damaged required system questions or invalid existing custom-question definitions;
- signup-code protection enabled without a usable code.

Signup-opening warnings include:

- waiting list disabled;
- public free-text answers;
- reopening a populated signup.

Missing banner, board work, teams, custom questions, draft time, and signup-code protection are later tasks or optional values, not warnings.

Event-start blockers include:

- another production current/public operational event;
- draft not finalized;
- board not published;
- an active team without a current Captain or enabled team-scoped emergency credential;
- any other currently authoritative start invariant.

Slice 4 may add signup-specific readiness checks; Slices 5 and 6 may refine team/draft/board checks. They must extend the shared evaluator rather than create competing transition logic.

### 3.5 Scheduled transitions and event start

- Background and manual transitions call the same application/domain services.
- The worker safely retries without duplicate transitions, audits, actions, or notifications.
- At a scheduled signup opening, the complete opening readiness contract is re-evaluated transactionally.
- Invalid configuration leaves signup closed and creates a visible admin failure/action.
- Scheduled signup closing closes signup at the configured instant.

At the configured event-start instant:

- evaluate start readiness transactionally;
- start immediately only when every blocker is clear;
- otherwise retain the pre-live state and record one unresolved **Automatic start postponed** action with current blocker codes;
- create an in-site notification for enabled administrators without making personal notification read state authoritative;
- preserve the scheduled instant and never backdate live eligibility;
- do not automatically retry after blockers clear.

An admin must then use **Start event now**:

- before the configured start: strong confirmation and a required written reason;
- at or after the configured start: strong confirmation without a written reason;
- only while current readiness succeeds;
- resolving the postponed action in the same transaction as the successful start.

### 3.6 Current event

- There is no separately editable “current event” setting.
- Production derives current/public operational status from lifecycle state.
- Multiple `SIGNUP_OPEN`/`SIGNUP_CLOSED` events are permitted only with non-overlapping half-open event windows; back-to-back windows are valid.
- Only `LIVE`, `AWAITING_FINAL_REVIEW`, and `FINALIZED` are singleton current states. Normal commands fail before mutation on overlap/current conflict.
- Signup is an unlisted exact-link journey. Unlimited private drafts and cancelled/archived history are allowed.
- A finalized event remains current until archived.
- Development/test scenarios navigate by explicit IDs/slugs and never silently choose an arbitrary current fixture.
- The Development exemption remains unreachable through ordinary product configuration or admin input.

### 3.7 Discard

- Any enabled admin may discard an accidental/experimental event after explicit confirmation.
- Discard is allowed only when there are no event participants, teams, event-scoped account access records, submissions, or evidence.
- Board/setup content, questions, and planning configuration do not block discard and are removed transactionally.
- Preserve a minimal event tombstone: ID, name, reserved slug, creator, creation time, discard actor, and discard time.
- The discarded event leaves active administration and every public listing.
- A database/audit failure leaves the event and its setup intact.
- Once protected records exist, direct the admin to cancellation instead.

### 3.8 Cancellation

- Any enabled admin may cancel an event that has never entered `LIVE`.
- Cancellation requires strong confirmation and a written reason.
- Cancellation is terminal through the ordinary UI.
- It closes signup, suppresses scheduled transitions, and disables event-scoped mutations.
- It preserves participants, character assignments, teams, draft history, board data, submissions/evidence, and audit history.
- A never-public cancelled event remains private.
- An already-public cancelled event retains its existing public route and previously published projection with a generic **Event cancelled** state.
- The private reason appears only in authorized administration/audit views.
- A cancelled event is not current.
- Live events use the approved end/finalization workflow instead.

### 3.9 Archive

- Only `FINALIZED` may become `ARCHIVED`.
- Archive requires strong confirmation but no written reason.
- Archive preserves finalization snapshots, rosters, boards, evidence, history, and every public URL.
- The event leaves the current position and appears under previous events as read-only history.
- No public competitive data becomes private.
- Slice 9 completes broader closeout/history presentation and archived-participant private evidence access.
- Existing exceptional unfinalization remains reasoned and may not create a second production current event.

### 3.10 Authoritative event-state contract

State is the broad lifecycle authority. A separate flag, timestamp, role, or publication setting may further restrict an action, but it must never re-enable an action forbidden by the event state.

The complete target state set is:

```text
DRAFT
SIGNUP_OPEN
SIGNUP_CLOSED
LIVE
AWAITING_FINAL_REVIEW
FINALIZED
ARCHIVED
CANCELLED
DISCARDED
```

Normal forward transitions:

```text
DRAFT → SIGNUP_OPEN → SIGNUP_CLOSED → LIVE
LIVE → AWAITING_FINAL_REVIEW → FINALIZED → ARCHIVED
```

Approved exceptional transitions:

| From | To | Conditions |
| --- | --- | --- |
| `SIGNUP_CLOSED` | `SIGNUP_OPEN` | Before draft lock; readiness still succeeds; populated reopening warning is acknowledged |
| `DRAFT`, `SIGNUP_OPEN`, `SIGNUP_CLOSED` | `DISCARDED` | Discard preflight finds no protected participant, team, event-access, submission, or evidence records |
| `DRAFT`, `SIGNUP_OPEN`, `SIGNUP_CLOSED` | `CANCELLED` | Event has never been Live; protected history prevents discard; strong confirmation and reason |
| `FINALIZED` | `AWAITING_FINAL_REVIEW` | Exceptional reasoned unfinalization |
| `ARCHIVED` | `AWAITING_FINAL_REVIEW` | Exceptional reasoned unfinalization; production current-event policy permits it |

`CANCELLED` and `DISCARDED` are terminal through ordinary product workflows. `LIVE` never transitions to either; it uses event end and finalization.

#### State capability matrix

| State | Visibility and current-event position | Participant/captain capabilities | Enabled-admin capabilities | Explicitly unavailable |
| --- | --- | --- | --- | --- |
| `DRAFT` | Private and not current. No event-owned public route is discoverable. | No signup, team, board, or evidence mutation through public/participant workflows. | Configure identity, schedule, signup, board, and planning data; open/schedule signup when ready; discard when empty; cancel only when protected history makes discard inappropriate. | Public signup, public roster/board, draft picks, live submissions, finalization, archive. |
| `SIGNUP_OPEN` | Exposed through its exact signup link but unlisted from general public navigation. Basic event/signup information is available; team, draft, and board publication remain separate. | Authenticated signup, edit, and confirmed cancellation while the form is open; exact rules are completed in Slice 4. No team/live evidence authority yet. | Close signup; edit allowed setup with required confirmations; manage participants; continue private board work; discard only if the protected-record preflight still passes; otherwise cancel with reason. | Draft picks before explicit draft start, live play/submissions, finalization, archive. |
| `SIGNUP_CLOSED` | Retains its exact-link signup information but is not a singleton current state. | No ordinary signup-field editing. Pre-draft participant withdrawal remains available until draft start; after draft lock, roster changes become authorized Admin exceptions. | Reopen only before draft lock; run/finalize draft and teams; prepare/approve/publish board; start event only when start readiness succeeds; cancel before Live; discard only if the protected-record preflight passes. | New ordinary signup, participant self-restore after close, live evidence before event start, finalization/archive. |
| `LIVE` | Current and public according to the independently published roster/board flags. | Current participants/captains use live navigation, swaps, focus, and evidence authority defined in Slices 7–8. External emergency credentials remain event/team scoped. | Review evidence; manage approved live roster/captain exceptions; make allowed reasoned corrections; end early with confirmation/reason. | Signup changes, discard, cancellation, archive, ordinary board/draft restructuring, participant self-withdrawal. |
| `AWAITING_FINAL_REVIEW` | Current and public. Scheduled/effective event end and submission cutoff remain distinct. | No new gameplay/drop eligibility. Eligible evidence upload/correction may continue only through the active submission cutoff; participant live mutations otherwise stop. | Review/correct evidence, use approved reasoned submission reopening, resolve finalization blockers, and finalize. | Signup/draft changes, new live eligibility, discard, cancellation, archive before finalization. |
| `FINALIZED` | Current and public until archived. Official snapshots/results are authoritative. | Read-only public/finalized results; no event mutation. | Archive; or use exceptional reasoned unfinalization when the current-event policy permits. | Signup, roster, board, evidence, progress, or result mutation outside unfinalization; cancellation/discard. |
| `ARCHIVED` | Not current. Appears in previous events with all established public URLs preserved. | Read-only public history. Signed-in former-participant access to their own rejected/withdrawn evidence is completed in Slice 9. | Read history/audit; exceptional reasoned unfinalization only when it would not create a second current event. | Every ordinary event mutation, signup, live access, cancellation, discard, repeated archive. |
| `CANCELLED` | Not current. Never-public events remain private; previously public routes show only their prior published projection plus a generic cancelled status. | No event-scoped mutation. Previously public information is read-only. | Read preserved setup/history and private cancellation reason through authorized administration/audit views. | Every scheduled transition, signup, draft, board publication, live/evidence mutation, finalization, archive, reactivation through ordinary UI. |
| `DISCARDED` | Not current, not publicly listed, and absent from active event administration. The slug remains reserved by the tombstone. | No access or mutation. | Audit/tombstone inspection only through appropriately authorized global audit tooling. | Every event workflow, restoration, slug reuse, scheduled transition, or public projection. |

#### Independent substate gates

The following values refine the matrix but never replace it:

- `first_public_at` locks the slug and determines whether a cancelled event ever had a public projection.
- Signup opening mode and schedule determine whether the worker may attempt `DRAFT → SIGNUP_OPEN`.
- `draft_locked` ends participant signup editing/reopening and protects draft history.
- Draft state/finalization determines whether event-start readiness can succeed.
- Separate participant-list, draft-result, roster, board, and results publication flags control only their own projections.
- Board publication is required for event start but does not itself change event state.
- Team membership and Captain/emergency-access records determine team-scoped authority and start readiness.
- `actual_started_at`, `actual_ended_at`, event end, submission cutoff, and any approved reopened cutoff control precise eligibility windows without inventing new event states.
- Finalization snapshots remain authoritative in `FINALIZED` and `ARCHIVED`.
- A user/account role can restrict access within an allowed state; it cannot permit a state-forbidden mutation.

Every lifecycle command, page action, scheduled worker query, public projection, and later Slice 4–9 workflow must be checked against this matrix. Later slices may add detail inside an allowed capability but must amend this contract before adding a new state transition or enabling a capability currently marked unavailable.

## 4. Target persistence and service changes

### 4.1 Event

Align `BingoEvent` with the approved nullable-draft model:

- nullable description and schedule/capacity values where required;
- `banner_asset_id`;
- `first_public_at`;
- `draft_at`;
- `actual_started_at`;
- cancellation actor/time/reason;
- discard actor/time;
- optimistic concurrency data where needed for stale identity/schedule confirmations;
- `CANCELLED` and `DISCARDED` states.

Retain authoritative UTC instants and the separate publication flags. Remove event-creation authority from expected team count/size. Preserve migration history even when active fields become obsolete.

### 4.2 Lifecycle records

- Extend `EventStateTransition` with system/scheduled attribution as required by the target model.
- Add `ScheduledEventStartAttempt` with scheduled/attempted/resolved timestamps, started state, and stable blocker codes.
- Use durable unresolved-action/notification projections rather than inferring a postponed attempt solely from logs.
- Enforce idempotency for scheduled opening, closing, start attempts, and their notifications/audits.

### 4.3 Application services

Introduce focused services rather than adding more lifecycle logic to Razor handlers:

- event creation/identity service;
- schedule service;
- readiness evaluator;
- lifecycle transition/current-event policy service;
- discard/cancel/archive service or cohesive lifecycle commands.

The exact class split is an implementation choice. Domain rules and transactions must not live only in Razor or JavaScript.

### 4.4 Migration

- Convert existing required description/schedule/capacity columns to the approved nullable model without losing retained values.
- Backfill `first_public_at` deterministically for already-public lifecycle states.
- Preserve existing finalized/archive timestamps and public routes.
- Add the new states/fields/records without rewriting competitive data.
- Rehearse clean and representative retained PostgreSQL databases.
- Development scenario reset/seed must support every new state and avoid arbitrary current-event selection.

## 5. Bounded implementation passes

### Pass 3.1 — Lifecycle persistence foundation

Implement only:

- nullable private-draft event model;
- lifecycle/cancellation/discard/start-attempt persistence;
- concurrency/idempotency boundaries;
- deterministic migration and retained-data preflight/rehearsal;
- domain and focused PostgreSQL constraint tests.

Do not change the creation/management UI or begin Pass 3.2.

**Gate**

- Domain transition invariants pass.
- Table-driven tests cover every allowed and forbidden transition in section 3.10.
- Capability-policy tests prove each state rejects representative mutations that belong to other states.
- Clean and representative retained migrations pass against PostgreSQL.
- Retained public/finalized/archived data remains unchanged except deterministic new metadata.
- EF reports no pending model changes.
- Affected Release builds, formatting, and `git diff --check` pass.

**Pass 3.1 gate result (2026-07-27):** Met. The nullable private-draft model, nine-state lifecycle transition policy, cancellation/discard tombstone fields, actual lifecycle timestamps, transition attribution, start-attempt retry boundary, event concurrency token, and deterministic retained-data migration are complete. Focused Domain `133/133` and PostgreSQL `3/3` tests, Release build, EF pending-model check, formatting, and diff validation passed. No Pass 3.2 UI, identity, or creation-workflow work was started.

### Pass 3.2 — Guided creation and event identity

Deliver:

- preserved five-step guided creation interaction;
- optional-section behavior and partial-entry validation;
- Copenhagen-defaulted supported-timezone selector;
- generated/editable pre-public slug;
- optional description and managed banner;
- step 4 with buy-in and preliminary board dimensions only;
- no prize, expected-team-count, or expected-roster-size inputs;
- route-backed identity editor enhanced as a desktop dialog where appropriate;
- identity audit, concurrency, confirmation, and feedback behavior.

Pass 3.2 is approved: the no-JavaScript creation journey, nullable “Not set” rendering, and 1–8 board-dimension boundary passed. Visual restyling remains deferred to the full UI overhaul.

**Gate**

- Focused tests cover minimal creation, optional full creation, partial invalid sections, slug collisions/lock, timezone behavior, banner transaction failure, and concurrent identity edits.
- Existing creation interaction remains recognizable and usable without JavaScript.
- Applicable creation/identity surfaces receive user visual/manual approval on desktop and narrow layouts.
- Affected Release build, formatting, and `git diff --check` pass.

**Pass 3.2 approval (2026-07-27):** The bounded functional remediation passed: native no-JavaScript creation has an ordinary submit control, optional schedule projections retain nulls and render “Not set”, board rows/columns are limited globally to 1–8, and submission cutoff is internal and automatically derived from event end plus 30 minutes. The preserved native-form five-step creation flow, route-backed identity editor, managed banner persistence/authorized preview/fallback, active-banner foreign-key integrity, transactional audit behavior, optional/partial-entry validation, slug lock and relational-race feedback, and timezone confirmation/reason paths remain in place. Visual review is deferred to the full UI overhaul; S3-06 redirect behavior and S3-07/S3-09 manual outcomes are recorded in the checklist.

### Pass 3.3 — Schedule and readiness

Deliver:

- route-backed schedule editor enhanced as a desktop dialog where appropriate;
- nullable schedule editing, draft time, cutoff default/customization, and timezone-safe display;
- manual versus scheduled signup opening;
- explicit default-close preview;
- schedule confirmation/reason rules;
- shared readiness evaluator and event-overview blocker/warning/later-task presentation;
- transactional manual signup open/close/reopen commands;
- production current-event check on opening.

Do not implement the scheduled-start worker behavior or destructive lifecycle actions.

**Pass 3.3 approval (2026-07-27):** Private/open/closed schedule editing, proposed Open-now and Reopen closing behavior, capacity/waiting-list behavior, multiple non-overlapping signup windows, overlap rejection, exact back-to-back boundaries, and exact-link/unlisted signup behavior passed. Calendar appearance remains deferred to the full UI overhaul. Scheduled execution begins only in Pass 3.4.

**Gate**

- Focused domain/application/PostgreSQL tests cover schedule invariants, mode-specific readiness, warnings/acknowledgements, stale confirmation, and current-event rejection.
- Existing signup questions and current start prerequisites are consumed without redesigning Slice 4–6 workflows.
- Manual checks cover understandable local/UTC displays, blank/partial schedules, open-now default close, warning acknowledgement, validation feedback, and dialog/page fallbacks.
- Affected Release build, formatting, and `git diff --check` pass.

### Pass 3.4 — Scheduled lifecycle and current-event operations

Deliver:

- idempotent scheduled signup opening/closing;
- transactional scheduled-start attempt;
- automatic successful start when ready;
- durable postponed-start action/admin notifications with exact blocker codes;
- no delayed surprise-start after blockers clear;
- manual early/late start confirmation/reason rules;
- current-event navigation and blocking-event links;
- lifecycle worker and manual handlers using the same services.

Do not implement discard/cancellation/archive or begin Pass 3.5.

**Pass 3.4 implementation status (2026-07-27):** A future Draft signup opening is the sole automatic-opening intent; clearing it disables the pending opening, and Open now supersedes it. The Schedule checkbox was removed. All date/time fields use the shared calendar with separate 24-hour hour and five-minute minute selectors plus native fallback. Development reset retains only TEST 13 and TEST 15 as marked fixtures. Automated gates pass; manual retests remain, so Pass 3.4 is not approved.

**Pass 3.4 approval (2026-07-27):** S3-11, S3-12, S3-13, S3-14A, and S3-14B passed. An unresolved postponed-start attempt remains historical, but its active pre-live Manage presentation uses current readiness and becomes an explicit ready-to-start prompt once blockers clear. Successful manual Start atomically resolves applicable attempts and enhanced/full Manage responses omit the active panel and Start form after Live. Pass 3.4 is approved.

**Gate**

- Focused fake-time/domain tests cover exact boundaries.
- PostgreSQL tests cover worker retry/idempotency, transition races, singleton races, and notification/action uniqueness.
- One browser-level route path covers postponed-start feedback through successful manual resolution.
- Development scenarios remain separately navigable and production commands cannot use the exemption.
- Affected Release build, formatting, and `git diff --check` pass.

### Pass 3.5 — Discard, cancellation, archive, cleanup, and final gate

**Independent-review remediation (2026-07-27):** The earlier manual acceptance and `394/394` full-suite result predate three review findings and do not clear them. A shared state/capability guard now makes Cancelled, Finalized, Archived, and Discarded direct-route behavior authoritative; Finalize is one serializable command containing its snapshots, transition, and audit; and discard uses a durable event-owned managed-banner cleanup outbox retried by the lifecycle worker. The only newly required manual check is one representative direct terminal POST/read-only route. The original reviewer must perform a restricted re-review of only these three findings before Slice 3 can be accepted. Do not claim a fresh complete-suite run.

Deliver:

- discard preflight, confirmation, transaction, cleanup, tombstone, and slug reservation;
- populated pre-live cancellation and generic public status;
- finalized-event archive and previous-event/current navigation;
- exceptional archived unfinalization current-event protection;
- obsolete lifecycle-path audit and bounded cleanup;
- final Slice 3 manual checklist/result sheet and documentation updates.

**Implementation status (2026-07-27):** Implemented with automated gates complete. Discard/cancel use one transactional protected-history boundary; archive and exceptional unfinalization extend the existing finalization service with atomic lifecycle/audit history and the production current-event lock. Discarded tombstones reserve their slug while disposable setup is removed; cancellation preserves history and public privacy; archived events remain read-only previous-event history. No migration was required because Pass 3.1 already persisted every lifecycle field. Focused PostgreSQL/policy/route/migration gates and the final `394/394` complete project test set pass. Pass 3.5 and Slice 3 manual acceptance are approved; only the bounded independent review remains before Slice 3 acceptance.

Final verification:

- focused destructive-operation and preservation tests;
- a table-driven state/capability regression covering all nine states and every lifecycle entry point;
- clean and retained PostgreSQL migration rehearsal;
- `git diff --check`;
- formatting verification;
- Release solution build;
- one complete solution test run after implementation stabilizes;
- one bounded independent Slice 3 review.

Do not stage, commit, or push unless separately authorized.

**Gate**

- Empty/setup-heavy discard succeeds while every protected-record case blocks.
- Cancellation never republishes private data or loses retained history.
- Scheduled work cannot reactivate cancelled/discarded/archived events.
- Archive preserves official snapshots and URLs while releasing the current position.
- User approves the representative manual cases.
- Independent review clears the bounded Slice 3 implementation.

## 6. Verification budget

Use tests proportionate to risk:

- domain tests for state/schedule/readiness rules;
- application tests for stable readiness classification;
- PostgreSQL integration tests for migrations, transactions, idempotency, destructive cleanup, concurrency, and the current-event policy;
- a small browser set for real routes, feedback, native forms, and high-value admin journeys;
- manual checks for the creation interaction, dialogs/page fallbacks, schedule comprehension, responsive behavior, and destructive confirmations.

Do not duplicate every readiness invariant at every layer. Run focused tests during Passes 3.1–3.4 and one complete suite in Pass 3.5 unless a failure or shared-boundary blast radius justifies another run.

## 7. UI impact map

| Surface | Slice 3 treatment |
| --- | --- |
| Event creation | Preserve guided interaction; update functionality and materially changed presentation to the active shared UI rules |
| Event overview/manage | Reuse as event context and readiness/action overview; avoid unrelated participant redesign |
| Identity editor | New route-backed page/dialog using the active shared system |
| Schedule editor | New route-backed page/dialog using the active shared system |
| Admin event list/navigation | Show private/current/cancelled/archived states and unambiguous current-event context |
| Public cancelled event | Generic cancellation status using only previously published projection |
| Previous events | Include archived events without changing public URLs |
| Signup form/board | Lifecycle visibility/availability changes only; Slice 4 owns redesign |
| Teams/live draft | Read readiness only; preserve protected layout and interactions |
| Board editor/public board | Read publication state only; preserve protected layout and interactions |
| Finalization | Preserve existing workflow; add current-event/archive protection only |

Every new or materially changed Slice 3 surface follows the active shared UI rules from its first implementation. The later Milestone 9 page-group pass still reviews every application page independently; Slice 3 does not inventory or repair earlier pages.

## 8. Handoff rule

Each implementation task must identify exactly one pass and instruct the agent to read:

1. `AGENTS.md`
2. `CURRENT_STATUS.md`
3. this plan
4. only the source-of-truth sections named by that pass

Use Terra Medium for ordinary passes, Terra High when a migration/concurrency boundary warrants it, and Luna for genuinely mechanical documentation or Git-only work. Reserve Sol High for the final independent review or a material product/architecture ambiguity.

An implementation task must not continue into the next pass. At the end it reports changed files, focused evidence, remaining pass-specific work, and whether the pass gate is met. Do not run the complete solution suite during every pass. Independent review follows the completed slice, not each pass.

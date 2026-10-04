# Functional contracts

## 1. Authority, scope, and conflict routing

This document is the active authority for final end-to-end journeys, actors, reachability, authority handoffs, failure/recovery behavior, and acceptance outcomes. It records the current version-one functional foundation and does not by itself imply release readiness.

The authority boundary is:

| Concern | Authority | Contract boundary |
| --- | --- | --- |
| Product behavior and version-one scope | `PRODUCT_REQUIREMENTS.md` | Roles, product rules, exclusions, and acceptance scope. |
| End-to-end journeys and handoffs | `FUNCTIONAL_CONTRACTS.md` | Who acts, where the journey starts/ends, what is authoritative, and how recovery works. |
| Entities, invariants, calculations, snapshots, and history | `DATA_MODEL.md` | Persistence shape, uniqueness, derived values, progress, ranking, and finalization semantics. |
| Architecture, security, storage, realtime, deployment, and operations | `TECHNICAL_ARCHITECTURE.md` | Technical boundaries and operational controls. |
| Global UI rules, page families, composition, and approval | `UI_SYSTEM.md`, `UI_PAGE_MATRIX.md` | Shared interaction, responsive, accessibility, protected composition, page-family references, exceptions, approval state, and UI gates. |
| Current checkout, work, limitations, and unresolved decisions | `CURRENT_STATUS.md` | Current state only; it does not redefine product behavior. |
| Remaining order, gates, and stop rules | `DELIVERY_PLAN.md` | Delivery sequence and release gates only. |

When documents disagree, the authority above decides the concern in its own column. A conflict that crosses boundaries is recorded and resolved in the owning source before implementation. No archived document, root tombstone, route shape, notification, or realtime message can silently override an active authority.

This contract deliberately omits detailed schema, formulas, infrastructure design, implementation history, planning method, status legends, and test inventories. Refer to the owning source for those details.

## Events directory read — AU04, 2026-10-02

Enabled Admins read visible non-discarded directory rows; only an enabled
SuperAdmin may read the separate hidden population. Search, phase and
`attention=1` (existing attention category count greater than zero) narrow the
selected All/Current/Past/Hidden population. Results expose total population size
before search/phase/attention filtering, nullable capacity, retained participant availability
and import provenance for later honest empty/filtered and participant rendering.
The existing Dashboard service provides a narrow authorized participation read
using its same retained population mapping; Dashboard statistics eligibility and
shared inbox units do not change. Failures propagate rather than becoming zero
participation. Existing directory forms/routes continue; new UI bindings remain
in the integration pass. Ordinary incomplete setup adds no attention category.

## Dashboard read journey — approved, 2026-10-01

Enabled Admins can obtain one read-only community Dashboard projection under
existing authorization. Statistics/chart/history use the population and identity
rules in PRODUCT_REQUIREMENTS, “Community Dashboard”. No saved state, provider
sync or lifecycle mutation occurs. Consistent totals and breakdowns share one
eligible population and clock; hidden events cannot leak through totals, returning
history or navigation. A read failure is not a successful zero-valued result.

Return values, availability/coverage, provisional state and stable event identity
for headline metrics and latest contributions, participation cohorts, six-column
sortable history, ended-event recap, current/upcoming card and community counts.
The latest contribution can come from an ongoing event while the recap remains
the latest ended event. Live and final-review statistics display Provisional;
the event card retains the actual phase. No provisional winner substitutes for
an active official snapshot, including after reopening final review.

Later UI integration binds actual event Overview destinations, shared URL sort/
Back/reload state, loading/error/retry, accessibility and localization. Retry is a
safe read; outdated responses must not overwrite a newer result. Lost authority
fails closed. Integration and manual acceptance remain deferred; prototype routing
and fixture data are not application proof.

### Dashboard application binding handoff — 2026-10-01

The implemented read boundary is `IAdminDashboardService.GetAsync(Guid,
CancellationToken)` with the compatibility alias `ICommunityDashboardService`.
Infrastructure registers both interfaces and returns one `AdminDashboardResult`
containing statistics, latest contributions/chart cohorts, ended recap, history,
current card and community figures. Each result preserves stable EventId values,
UTC dates, measured/unavailable metrics, EHB coverage and provisional state. The
later page can bind `OverviewPath` as `/Admin/Events/Manage/{id}` and use the
current-card, recap and history IDs without inventing route strings or changing
the existing Admin Index callers.

`DashboardHistoryOrdering.Sort` supports EventDate, Players,
ApprovedSubmissions, WinnerBoard, Ehb and Winner. Every field is null-last in
ascending and descending order, with EventId as the deterministic tie-break.
Equal actual starts share a cohort; latest recap remains ended-only and official
winner data remains snapshot-owned after reopening. Read failures, cancellation,
authorization failures and provider exceptions propagate instead of becoming
successful zero results.

The recorded backend proof is preserved in the durable H1 handoff metadata under
`docs/references/admin-ui/reviews/2026-10-03/doc-ticket-cleanup/h1/dashboard/`:
the ordering proof is 2/2 and the Dashboard integration proof is 7/7. The
source TRX files were intentionally omitted from the durable copy. Imported-only
approved-submission
coverage and incompatible/missing historical EHB remain unavailable by contract;
compatible stored bulk coverage, partial coverage and measured zero are typed.
The earlier `dashboard-remediation-r1-r5-fixturefixed.trx` (9/9) was reported
historically but is not retained in the durable copy. Named retained metadata
records `dashboard-remediation-ordering.trx` (2/2), plus the latest bounded proof
`dashboard-remediation-r2-r3-r1-r5.trx` (8/9 with one PostgreSQL
initialization-only authentication failure), its isolated
`dashboard-remediation-r5-queryshape-retry.trx` (1/1), and the affected
`dashboard-remediation-r3-original-login-boundary.trx` (1/1). The latest cases
verify interior Live departure, missing/inverted ended-boundary unavailability
with no false 30-day fallback, the independent login window, weighted published
drop plus importless AdminCreated-roster submission counting, reopened winner
suppression, frozen denominators, fixed bulk query shape, no writes, failure
propagation and a repeatable-read concurrent-change boundary. No HTTP route, UI
binding, table, migration, job, cache, provider fetch or lifecycle write was
added. UI behavior and manual acceptance remain deferred; the independent
Dashboard source review is PASS after the same reviewer resolved R1–R5 and
their direct consequences against the stable candidate and recorded evidence.
It did not rerun tests; UI integration and manual acceptance remain deferred.

## 2. Cross-cutting journey contract

### Participants backend follow-up — 2026-09-30

The [approved Participants changes](PRODUCT_REQUIREMENTS.md#participants-backend-changes--approved-2026-09-30)
refine ADM-PARTICIPANT-01 and pre-draft restore; they supersede conflicting clauses
about ordinary corrections, questionnaire requirements, capacity and saved-account
side effects only for these named operations. The new UI/HTTP binding is deferred.
Existing forms/routes must remain compatible; do not weaken public self-signup
validation or expose a new unprotected mutation merely to demonstrate a service.

An enabled Admin acts on an accessible event in Draft/SignupOpen/SignupClosed
before team-draft lock. At the application mutation boundary recheck actor,
lifecycle, current participant/account identity, configuration, capacity and stale
versions. Explicit capacity expansion is a distinct intent, never inferred from a
normal request. A repeat or stale request cannot expand capacity twice. Placement,
account reservations, payment, audit and required notifications commit atomically.
Return the resulting participant ID, status, placement/capacity outcome and useful
validation/conflict information for later UI binding without relying on success
text parsing. Follow existing localization/outcome conventions.

Confirm-selected, move-to-Waiting and override restore preserve everyone else's
relative queue order. Moving to Waiting requires full capacity and another
eligible waiting participant; end current team authority with history, enqueue the
selected participant last and fill the place with the pre-existing next waiter.
Do not clear their event account reservations as though they were withdrawn.
Normal restore retains the existing reacquisition, sequencing and failure rules.

Question-free Add resolves saved account IDs and stored EHB authoritatively for
the chosen active website account, maps selections to configured Playing slots,
defaults captain volunteering to No, and leaves unrelated questionnaire answers
absent. It makes no external WOM request and does not accept forged link ownership,
client EHB defaults, arbitrary extra slots or duplicate event assignments. Missing
saved EHB/configuration handling must be made explicit during readiness; do not
silently fabricate a zero or add an unapproved provider fallback.

Primary switching and event-account correction preserve status, signup order,
non-account answers, unrelated accounts and saved-account data. Reopening/reading
must expose the selected primary and its EHB consistently to Participants, draft
and other directly affected projections. Do not mutate historical/live account
attribution or global shared character names to represent an event correction.

The future UI confirms capacity expansion and membership loss before submitting,
shows authoritative errors without discarding a draft, and displays the persisted
result. Those presentation, route/history, animation and manual journeys belong to
the later UI integration pass; backend completion does not approve them.

### Later UI review decisions — 2026-10-02

The [approved UI review decisions](PRODUCT_REQUIREMENTS.md#ui-review-decisions--approved-2026-10-02)
qualify the contracts below: AU12 applies only to new events; AU17a/D11 option b
permits current/released Playing event assignments of current/former members of
the submission’s own team, excluding Informational accounts. B5 backend contracts
for AU17a/AU17/AU19/AU14 are implemented; external Claude review and UI binding
remain pending. WOM
option 1 is now explicitly approved: preserve local external disconnect before first
Live and matching replacement before/during Live with or without stored credentials,
subject to active/unresolved-operation guards. Never delete the external competition
or reuse its code for the replacement. The earlier mistaken approval was withdrawn
before the user approved this investigated option; AU20 is implemented and remediated; external recheck is pending.

### Approved Admin simplification precedence — 2026-09-26

The [approved product target](PRODUCT_REQUIREMENTS.md#approved-admin-simplification-target--2026-09-26)
and assigned ticket journeys supersede conflicting legacy journey clauses below,
including emergency authority, accountless creation/participant-owner transfer,
Preformed restrictions, paused/reopened draft, delayed switches, special Resubmit,
Live replacements, manual result overrides and separate Archive. Compatibility
reads do not keep retired commands authorized. The new-event, direct-roster,
signup-correction, fixed-roster, evidence, final-review, WOM recovery, multi-Admin,
quarantine and historical journeys in plan section 7 are the integrated acceptance
set. This is target authority, not executed verification or manual acceptance.

Emergency Captain authority is retired (SEC-01). Retained emergency accounts, access rows, tokens and actor references remain historical data: login, existing cookies, token consumption, creation, reset, enable/disable, evidence and Team Focus authority are unavailable. No conversion, deletion, fabricated human disable Audit or expiry processing accompanies retirement. Normal Captain/Co-captain authority remains membership-based. Live start has no Captain/credential prerequisite; actual website-draft start/finalization still requires current Captains. Event-specific rollout remains subject to the retained-data evidence gate. This records implemented behavior, not review or manual acceptance.

Every touched mutation rechecks authorization, lifecycle, dependencies and versions
at its authoritative boundary. Outcomes distinguish saved, already completed/no
change, field error, duplicate/conflict, stale edit, changed permission/lifecycle,
dependency blocker, unavailable/rate-limited/incomplete provider and unexpected
failure. Say what happened, whether local state saved and how to recover. Preserve
safe input; explicit severity and field errors must not depend on English text or
disappearing toasts. Unexpected failures use safe action-specific text and a
diagnostic reference; unknown upstream outcomes never claim nothing changed.

Successful local mutation history commits atomically with its mutation and names
actor, event where applicable, target, semantic action and useful before/after
values. Reasons are required only by the owning contract. Rolled-back attempts
are not completed changes. Reuse authoritative review/switch/domain history rather
than creating conflicting duplicate Audit records. Provider request, confirmed
outcome and unknown outcome stay distinct. Automatic successful fetches belong to
WOM operational history; controller renewal, derived cache refresh, confirmation
open/cancel and personal notification read state do not create global Audit noise.
Keep credentials, password material, tokens and signup-code values/hashes out of
new Audit payloads; preserve private-data restrictions and tolerant legacy reads.
The same human-readable Audit mapping serves full and recent views, with raw safe
details behind Technical details. Final verification records reachable mutations
and applicable outcome coverage, not only a list of pages.

### 2.1 Authentication and role authority

- A website account is the durable identity for normal website actions.
- Discord authentication and public-username/password authentication resolve to the same website account; Discord server membership is not required.
- Initial account creation starts through Discord and completes public username, password, and first OSRS-character onboarding before normal signup.
- Event participation is explicit `EventParticipant.AccountId` ownership. A Discord display name, website username, OSRS name, volunteer answer, or emergency credential never infers ownership.
- Captain and co-captain authority is an event/team role on a current website-account-linked membership. It is not attached to a character name or authentication method.
- A retained emergency captain identity is historical only and cannot authenticate, consume credential tokens or acquire operational authority.
- Super Admin is a global role with exactly one active owner. Global roles and event roles remain separate.
- Disabled accounts cannot authenticate; session invalidation and authorization version changes take effect without waiting for an ordinary cookie expiry.
- Every mutation rechecks the current authenticated account, event scope, role, lifecycle phase, and expected version at the authoritative service boundary.

### 2.2 Server authorization and validation

- Server authorization is authoritative for every page, form, fallback route, background action, and realtime-triggered refresh.
- Validation is repeated at the mutation boundary; client-side validation, rendered controls, links, notifications, and route visibility are not permission grants.
- A failed authorization or validation attempt leaves the last valid state active and returns actionable, non-secret feedback.
- Domain/application services own lifecycle, eligibility, privacy, and cross-record rules. Razor, JavaScript, and database triggers do not replace those rules.
- Admin corrections identify the actor and preserve the affected record's history. Exceptional changes that alter competitive or historical truth also require strong confirmation and a written reason.

### 2.3 Audit, history, transactions, concurrency, and idempotency

- Historical competitive records, event transitions, memberships, character assignments, submissions, evidence, board approvals, official results, and access changes are append-only or versioned as defined by `DATA_MODEL.md`.
- Routine configuration mutations record actor, time, and structured before/ after values automatically; a typed reason is not required unless this contract says it is.
- Compound mutations commit authorization, validation, reservations, derived state, audit, and required in-site notifications atomically.
- Optimistic versions and appropriate event/team/participant locks reject stale writes. A rejected stale write never overwrites a newer authoritative value.
- Repeated identical requests are safe where the owning journey declares them idempotent. Retry-sensitive durable notification producers and scheduled attempts use the owning transition or occurrence as their deduplication boundary; ordinary notifications may use fresh IDs within their accepted transaction or action.
- Reconciliation or retry re-reads the authoritative state before applying a new mutation. It never assumes that a previous page or notification is still current.
- A transaction failure returns a retryable failure and leaves no misleading success, partial reservation, partial role change, or orphaned lifecycle transition.

### 2.4 Privacy and evidence integrity

- Public projections contain only the public event, roster, board, approved evidence, standings, and participant-facing signup data allowed by the product contract.
- Website usernames, Discord identity/security data, payment, Admin notes, audit data, private evidence states, and unnecessary account identifiers are excluded from public projections.
- Ordinary participants see their own private signup/evidence scope. Captains see their current team's authorized scope. Admins see event administration scope. Admin and Super Admin capabilities compose additively with a genuine event Participant/Captain/Co-captain role but never create or upgrade that role. Super Admin adds global administration, not automatic cross-team private focus or evidence authority; its existing explicit read-only focus inspection remains separate.
- Approved evidence is authoritative for progress. An approved screenshot and its credited participant/account, submission time, board snapshot weight, and review history are not silently rewritten.
- Reversal removes the contribution through the authoritative recalculation and preserves the original approval. A Rejected or Reversed attempt remains immutable history; while the normal/reopened upload window is open, a later attempt is an ordinary new Pending submission with its own image and server time, and neither record is directly re-approved.
- Account/character sharing is trust-based and never proves real-world ownership. Event assignment uniqueness remains authoritative.

### 2.5 Notifications, realtime, and authority

- A notification is a recipient-specific destination and reminder, not the business record. The destination must resolve for the intended role.
- Opening or marking a personal notification read never resolves the underlying Admin action; that action disappears only when its authoritative condition is fixed.
- Notifications identify the event, actor-relevant subject, and direct route without disclosing private reasons or another team's evidence.
- Required notifications are written atomically with their mutation. Retry-sensitive producers use deterministic IDs at the owning transition/recipient boundary where implemented; ordinary notifications rely on their surrounding accepted transaction or action rather than a universal recipient-transition constraint.
- SignalR and other realtime messages are invalidations or non-authoritative updates. The receiving page reloads authoritative state and handles stale versions.
- A realtime invalidation never interrupts an active submission, confirmation, error, or result state. Ordinary route reload/navigation remains available.

### 2.6 Reachability and progressive fallback

- Every protected journey has a real authenticated route and ordinary server form/navigation path. JavaScript enhances the route; it does not define the business operation.
- Public board, team, tile, and approved-evidence destinations remain addressable through their real routes. Below 901px, ordinary route navigation is expected instead of overlay transitions.
- Desktop overlays/drawers may preserve context, but board/team/tile real URLs remain the recovery path for refresh, narrow screens, keyboard use, history, and failed enhancement. The Captain Submit route is only drawer transport plus a compatibility redirect for old direct links, not a rendered or no-JavaScript submission destination. No separate no-JavaScript parity journey is required.
- Manual acceptance begins from the rendered navigation and forms expected by the journey. A destination URL constructed without following its intended link does not prove reachability.
- A route that is visible but fails authorization, filtering, handler validation, or destination rendering is not a reachable journey.

### 2.7 Failure feedback and recovery

- Every mutation reports success or failure on the authoritative page or destination, with enough context to correct the problem and without leaking private data.
- A conflict names the affected field/record or blocker, preserves recoverable entered values where safe, and leaves the last committed state unchanged.
- Temporary external-service failure never rewrites event state. The user gets accurate unavailable/retry or manual-entry feedback.
- A stale confirmation is rejected and regenerated from fresh data; it is never applied to an altered schedule, role, board, or lifecycle state.
- Reversal, restoration, reopen, resume, unfinalize, ordinary later evidence attempts, and account recovery are explicit actions with the confirmation, reason, and history requirements declared by their capability below. Historical resubmission commands and enum values remain read-only display compatibility.

### 2.8 Development reset and manual-acceptance reachability

- Development reset creates only bounded labelled fixtures and the minimum accounts, roles, lifecycle states, teams, boards, signups, submissions, and cached integration data needed for documented journeys.
- Development navigation uses explicit event IDs/slugs and reset output. It never chooses an arbitrary “current” event when multiple fixtures exist.
- Production lifecycle services do not honor the Development fixture exemption; the exemption is unreachable through normal deployment input.
- The reset is idempotent and does not call Wise Old Man. Manual acceptance can use the seeded lookup event, Live competition cache, linked participant, Captain/co-captain, emergency credential, and Admin targets without real participant data.
- If a journey requires a seeded role or state, the reset output and the rendered navigation are the entry authority; a missing fixture is a setup limitation, not permission to invent a production shortcut.

## 3. Capability index

Each durable capability/journey below has one owning contract section. Shared cross-cutting rules in section 2 apply to all entries but do not create a second capability section.

| Capability or journey | Owning section |
| --- | --- |
| `WF-01` Create event through first participant access | 4.1 |
| `ADM-EVENT-01` Private event draft | 4.2 |
| `ADM-EVENT-02` Identity and public description | 4.3 |
| `ADM-EVENT-03` Schedule and timezone | 4.4 |
| `ADM-SIGNUP-01` Signup fields/readiness | 4.5 |
| `ADM-SIGNUP-02` Capacity/waiting list/code/withdrawal | 4.5 |
| `ADM-SIGNUP-03` Signup readiness check | 4.5 |
| `ADM-SIGNUP-04` Open signups | 4.5 |
| `ADM-RULES-01` Rules and `PUB-HOWTO-01` public how-to | 4.6 |
| Managed-image rule | 4.7 |
| `PART-IDENTITY-01`, `PART-PROFILE-01`, `ADM-IDENTITY-01`, `ADM-IDENTITY-02` Identity and recovery | 5.1 |
| `PART-ACCOUNT-01` My accounts and active swaps | 5.2 |
| `PART-SIGNUP-01`, `PART-SIGNUP-02` Submit, confirm, edit, cancel, rejoin, restore | 5.3 |
| `PUB-SIGNUP-01` Exact-link public signup table | 5.4 |
| `ADM-PARTICIPANT-01` Pre-draft participant administration | 5.5 |
| `ADM-PARTICIPANT-02`, `ADM-CAPTAIN-01` Post-draft roster exceptions | 5.6 |
| `ADM-DRAFT-01` Team setup, snake draft, finalize | 6.1 |
| `ADM-BOARD-01` Board build, approve, snapshot, publish/correct | 6.2 |
| Draft/board catalogue coupling and approval snapshot | 6.3 |
| `SYS-EVENT-START-01` Scheduled start readiness | 7.1 |
| `SUBMISSION-WORKSPACE-01` Canonical Captain/participant submission workspace | 7.2 |
| `PART-LIVE-01` Live participant workflow | 7.3 |
| `ADM-EVENT-END-01` End, grace period, resume | 7.4 |
| `ADM-REVIEW-01` Evidence review/correction/reversal | 7.5 |
| `ADM-FINALIZE-01` Final review/results/unfinalize | 7.6 |
| `ADM-EVENT-ARCHIVE-01`, `ADM-EVENT-CANCEL-01`, `SYS-CURRENT-EVENT-01` Lifecycle/history | 8.1 |
| `SUPERADMIN-EVENT-QUARANTINE-01` Hidden-event quarantine | 8.1a |
| `ADM-ACCOUNT-01` Disable/restore | 8.2 |
| `PART-HISTORY-01` Archived participant history | 8.2 |
| `PUB-FEEDBACK-01` External feedback | 8.3 |
| `SUPERADMIN-01` Global ownership/role administration | 9.1 |
| `ADM-CATALOGUE-01`, `ADM-CATALOGUE-IMPORT-01` Catalogue administration and import boundary | 9.2 |
| `ADM-AUDIT-01` Audit history | 9.3 |
| `ADM-ACCOUNT-OVERVIEW-01` Account overview | 9.4 |
| `ADM-INBOX-01` Personal notifications and Admin actions | 9.5 |
| Wise Old Man account lookup and cached competition activity | 9.6 |

## 4. Event setup and public entry

### 4.1 `WF-01` — Create event through first participant access

**Actors and outcome:** An enabled Admin creates and configures a private event; the system reports what is needed to open signup; a participant authenticates, submits or resumes one event signup, and receives an authoritative confirmation or the documented fallback/error outcome.

**Entry and reachability:** Start at Admin Events → Create. Continue through the rendered Identity, Schedule, Questions, participant, and exact-link signup destinations. The event remains private until signup opens. A returning account uses its authenticated event destination; a first-time account completes Discord onboarding first.

**Authoritative happy path:** Save a minimum private draft, configure identity, schedule, signup fields and capacity, pass readiness, open signup manually or on schedule, authenticate the participant, reserve valid event characters, and commit the signup. The first participant reaches confirmation with status, accounts/EHB, answers, and editing/withdrawal availability.

**Authority, history, and visibility:** Admin services own setup and opening; the authenticated website account owns the signup; server-side event-character reservations and capacity decide admission. Draft details, private Admin data, and unapproved competitive data remain hidden.

**Failure and recovery:** Invalid setup stays in the relevant form; an opening failure leaves signup closed; a character race fails atomically and identifies the conflicting account; a temporary service failure offers retry, with manual EHB entry in My accounts as the accepted signup fallback. The user resumes from the authoritative destination rather than a stale link.

**Acceptance outcome:** An enabled Admin can take a new event from private draft to a reachable first participant confirmation without creating partial records, publishing incomplete data, or bypassing identity, capacity, or privacy rules.

### 4.2 `ADM-EVENT-01` — Private event draft

**Actors and outcome:** Any enabled Admin saves a private workspace using event name and timezone; optional description, schedule, signup configuration, questions, capacity, planning values, and artwork may be continued later.

**Entry and reachability:** Admin Events → Create saves the draft and returns to the event setup workspace. Another enabled Admin can continue the same event.

**Authoritative happy path:** The server creates a unique event ID and slug, creator/time metadata, Draft state, standard signup questions, an empty 5×5 board, and safe defaults in one transaction. No participant, team, captain, or evidence record is implied.

**Permissions and history:** Every enabled Admin may create/configure every event. Duplicate display names are allowed; the slug is unique and permanent; renaming does not change public entry identity. Creation and later setup changes are audited.

**Failure and recovery:** Invalid name/timezone or lost authorization creates no usable event. A slug collision gets a different valid slug. An unprotected experimental event can be confirmed-discarded; once protected participant, team, event-access, submission, or evidence data exists, discard is blocked and the cancellation contract applies.

**Retry identity (AU03):** Each logical create submission carries a non-empty UUID,
scoped to the authenticated enabled Admin. The original trimmed name and supported
timezone are immutable request input. Concurrent/repeated submissions with that
actor/key/input commit one aggregate, creation audit and durable outcome together;
a different name or timezone with a completed key is rejected without mutation.
Different keys may create events with identical names. Invalid requests do not
consume a key. An absent/malformed key is rejected; the existing GET form supplies
one, retained across validation errors and retries. New modal binding is deferred.
Check again uses `/Admin/Events/Create?handler=CheckAgain&requestId=…` and returns
only the requesting actor's committed event ID; absence means no accessible committed
outcome at that lookup, not proof a request can never finish. Retry with the same
key remains safe. Renaming/configuring an event cannot change original request
identity. Hidden outcomes obey SuperAdmin visibility; discarded outcomes are
unavailable. Neither case frees the key or allows recreating the event. Revoked or
disabled Admins cannot create, replay or look up an outcome.

**Acceptance outcome:** A minimal private draft is independently saveable, resumable, auditable, undiscoverable publicly, and unable to accept signup or publish competitive information by itself.

### 4.3 `ADM-EVENT-02` — Identity and public description

**Actors and outcome:** An enabled Admin manages event name, optional public description,
buy-in information and supported timezone. The server-owned public slug is permanent;
there is no event banner capability. Text fields remain editable through Live and
Final Review; timezone locks permanently at the first Live transition.

**Entry and reachability:** Use the event Identity route from Admin Manage or the
setup progression. Existing forms carry original field baselines and timezone-review
state; full conflict-choice UI integration remains deferred.

**Authoritative happy path:** Compare canonical original, intended and current values
for each Identity field. Untouched fields retain another Admin's changes; an intended
change is safe when current equals original or already equals intent. Different
same-field edits block the entire save. Explicit Keep mine / Use current resolution
is valid only against its reviewed current field value; a newer differing edit
requires resolution again. Failure preserves the original baseline and draft.
Current-version legacy submissions retain their path; stale baseline-free submissions
fail closed and require reload.

**Permissions and history:** Identity changes remain Admin-authorized, phase-checked,
Serializable, concurrency-protected and audited atomically. Stored UTC instants never
move because the display timezone changes. Permanent slug, 50-code-point name and
UTF-16 description/buy-in limits remain authoritative.

**Failure and recovery:** Unsupported timezone, slug mutation, malformed baseline,
unresolved field conflict or stale timezone review leaves all prior values active.
After public exposure, a timezone change reviews its participant-facing timeline.
Confirmation binds the original/proposed zones and current timeline consequences
at persisted timestamp precision. A schedule-only change returns separate stale
feedback and fresh consequences requiring confirmation again. Recomparison occurs
inside the write transaction. Unchanged values produce no identity mutation/audit.
AU09 dispatch contract: freeze the complete canonical Name, Description,
BuyInDescription and Timezone expected tuple separately from the original edit
intent/baseline. Use current takes exactly the explicitly reviewed value; untouched
fields take the latest values actually observed and used at dispatch. Never derive
expected values from a later read. Check again uses the existing authorized Identity
read boundary and compares all four full values. A match means only Up to date now,
never that this request saved them. An unseen disjoint server merge or later edit
may therefore remain Different/uncertain even after an applied save. Different or
failed reads preserve the frozen tuple and editing draft without retry, rebase,
overwrite or discard; failed reads are Unknown. No receipt or reconstruction of
the server's effective merged tuple is introduced. Backend/transport integration
is sufficient for AU09; full new-reference UI binding and manual acceptance remain
deferred. Existing timezone confirmation transport switches an uncertain mutation
to read-only checks; it must never blindly send that mutation again.
Full Use theirs frontend binding remains deferred.

**Acceptance outcome:** Concurrent edits retain untouched current values, same-field
conflicts require an explicit current resolution, and stale schedule consequences
cannot be accepted through an old timezone confirmation.

### 4.4 `ADM-EVENT-03` — Schedule and timezone

**Actors and outcome:** An enabled Admin configures signup opening/closing, event start/end, optional draft time, and submission cutoff safely in the event timezone while authoritative instants remain UTC.

**Entry and reachability:** Use Schedule from Admin Manage. Open-now, scheduled, close, reopen, and schedule-edit actions remain route-backed form actions.

**Authoritative happy path:** A private Draft may save supplied schedule values before full opening readiness; the save validates supplied ordering, future boundaries, and any complete event-window overlap. Before opening, validate event start before end, establish a valid signup closing no later than start, use the configured future opening timestamp for scheduled mode; the retained legacy toggle is not an Admin control, or record the actual current opening for manual mode. Default submission cutoff is 30 minutes after event end and cannot precede that end. Participant capacity is owned and changed by Signup setup; Schedule does not write it or promote the waiting queue.

**Permissions and history:** An unchanged historical timestamp retains its exact UTC instant, including seconds/subseconds and valid repeated-hour history. Changed local times must be valid, unambiguous five-minute values and future. Passed signup/draft boundaries cannot be changed or cleared; before the first Live transition, event start/end may be repaired to future values even when their configured boundaries have passed. Actual signup transitions remain history; future opening timestamps schedule opening without an Admin toggle. Manual actions supersede the corresponding scheduled action. Reopening manually closed signup reuses its configured close when that close remains future; otherwise Reopen establishes and confirms a replacement future close. Published start/end cannot be cleared. Draft time is optional planning information and never starts the draft. Routine pre-Live Schedule mutations retain automatic actor/time/before/after audit evidence and require no written reason; changing a future end while Live additionally requires confirmation and a written reason.

The following matrix is the authoritative editability contract. `Edit` means the value is available through Schedule, `Action` means only the named lifecycle action may change or reuse it, and `Locked` means it is retained as read-only history.

| Schedule value | Private Draft | Signup Open | Signup Closed, draft not started | Draft Running (legacy Paused read-only) | Draft Finalized, pre-Live | Live | Final Review | Finalized / Archived / Cancelled |
| --- | --- | --- | --- | --- | --- | --- | --- | --- |
| Signup opening | Edit while future | Locked | Locked | Locked | Locked | Locked | Locked | Locked |
| Automatic signup opening | Derived from future opening timestamp; no toggle | Superseded by actual opening | Same | No control | No control | No control | No control | No control |
| Signup closing | Edit while future | Edit while future | Action: Reopen reuses the configured close when it remains future; otherwise it establishes and confirms a replacement future close | Locked | Locked | Locked | Locked | Locked |
| Draft time | Edit while future | Edit while future | Edit while future | Locked | Locked | Locked | Locked | Locked |
| Event start | Edit to a future value | Edit to a future value | Edit to a future value | Edit to a future value | Edit to a future value | Locked as actual/history | Locked | Locked |
| Event end | Edit to a future value | Edit to a future value | Edit to a future value | Edit to a future value | Edit to a future value | Edit to another future time with confirmation and reason | Action: Resume reuses the configured end when it remains future; otherwise it requires a replacement future end; confirmation and reason are always required | Locked |
| Normal submission cutoff | Derived as event end plus 30 minutes; never directly editable | Same | Same | Same | Same | Re-derived when a future event end changes | Historical | Locked |
| Reopened submission cutoff | Unavailable | Unavailable | Unavailable | Unavailable | Unavailable | Unavailable | Action: Reopen submissions with a future cutoff and reason | Locked |
| Participant capacity | Read-only; see Signup setup | Read-only; see Signup setup | Read-only; see Signup setup | Read-only; see Signup setup | Read-only; see Signup setup | Read-only; see Signup setup | Read-only; see Signup setup | Read-only; see Signup setup |

Actual signup opening/closing, actual event start/end, submission closure, finalization, archival, and cancellation timestamps are system-recorded history and are never ordinary Schedule inputs. Hidden events expose only SuperAdmin restoration, and Discarded events expose no event workspace.

While the team draft is Running, Paused or Finalized before first Live, signup dates, draft time and capacity remain locked; event start/end remain editable, including correction of overdue configured boundaries to future values. Every accepted schedule change rechecks ordering, operational-window overlap, and any linked Wise Old Man competition's exact UTC window; changing event end atomically re-derives the normal submission cutoff and commits its audit evidence in the same transaction.

**Failure and recovery:** A missing/invalid schedule blocks the opening transition while a valid partial private-Draft save may remain incomplete and show those values as unmet opening readiness. Manual-opening defaults are previewed and committed only inside the successful transaction. Schedule shows one server-recomputed confirmation containing only actual changes, derived cutoff effects (capacity is owned by Signup setup), and current warnings when the event is public or warnings exist; private warning-free changes save immediately. Scheduled readiness failure leaves signup closed and alerts Admins. A postponed start does not close its own recovery path: while the event has not actually started and its configured end remains future, Admins may complete pre-Live roster corrections, draft finalization, and board publication, then start manually without rewriting the missed configured start. If the configured end has also passed, start fails closed until the Admin repairs the pre-Live event window to future values or cancels/replaces the event. This does not change automatic scheduler readiness or execution. Complete event-window edits on private Drafts and signup-open/closed edits recheck non-overlap, and a linked Wise Old Man competition must match both UTC boundaries of the proposed event interval. Timezone changes alter display only; explicit schedule edits alter stored instants and retain before/after history.

**Schedule save recovery (AU10):** Preserve each unchanged field from the version-checked authoritative row instead of reparsing its minute-only display. Retain the enabled overdue automatic-opening exception and legacy disabled state when editing unrelated fields. Map suitable lifecycle/order errors to existing field ModelState entries; overlap stays a form error. An authorized no-store Current read exposes the full precise schedule, automatic-opening state, version, timezone, phase and editability. A transport session freezes the full submitted UTC tuple separately from its original baseline/draft. Matching current values mean Up to date, never proof that this request saved them; version and phase/editability remain visible context. An unchanged old state or competing different state retains the submission and uncertainty; a failed/unauthorized/malformed read is Unknown. Check again is read-only, with no blind retry, rebase, discard or receipt system. Backend/readback transport integration is sufficient for AU10; ordinary form enhancement, picker binding, stay-on-Schedule, confirmation table and navigation remain deferred. The existing single confirmation policy is retained.

**Acceptance outcome:** Manual and scheduled opening share one readiness contract, closing and cutoff boundaries remain valid, and delayed processing never backdates or extends competitive eligibility.

### 4.5 `ADM-SIGNUP-01`, `ADM-SIGNUP-02`, `ADM-SIGNUP-03`, `ADM-SIGNUP-04` — Fields, capacity, readiness, and opening

**Actors and outcome:** A signup Admin defines the fixed primary Account, primary EHB, captain volunteer, custom questions, capacity, waiting-list, signup-code, and withdrawal settings; the Admin or scheduler opens signup only when the requested opening mode is ready.

**Entry and reachability:** Questions and Schedule are reached from the setup workspace. The Admin chooses Open now or scheduled opening; the scheduler uses the same server readiness service.

**Authoritative happy path:** The primary regular Account is required and its EHB is required. Secondary Account questions are optional and explicitly regular (`PLAYING`) or informational (`INFORMATIONAL`); regular answers have their own EHB, while alts do not. Opening validates description, capacity, start/end/closing, Discord login configuration, form shape, and enabled signup code. It records actual manual opening or the configured scheduled opening.

**Permissions and history:** Signup definitions are versioned. Before draft start, Admins may add, edit, delete, and reorder questions while signup is open or closed. After the first accepted response, an existing question's type, Account role, options, stable key, and answer shape cannot be changed; new questions are optional and an optional question cannot become required. A format change is delete old then ordinary create new: explicit current-impact confirmation permanently removes the old answers and releases only affected event registrations, while saved My accounts links, required system questions, participant status/order, and audit/competitive history remain protected. Existing inactive legacy questions and retained answers remain authorized private history only and are excluded from every public signup-table projection. Delete is the only removal action; no disclosure-tracking field, historical backfill, or separate Hide feature is added. Draft start freezes ordinary form changes and closes signup.

**Failure and recovery:** Missing description, capacity, schedule, primary playing field/EHB, invalid question, unusable enabled code, or requested opening inconsistency blocks opening. Waiting is always enabled while signup is open. Public text answers are informational warnings shown in the same server-recomputed confirmation when confirmation is otherwise required; they never require a separate acknowledgement and do not block manual or scheduled opening. Banner, board, teams, draft time, and current Wise Old Man availability are not opening blockers. A failed scheduled attempt leaves signup closed and exposes its true blockers.

**Acceptance outcome:** Capacity and deterministic waiting-list order are authoritative, Account roles are unambiguous, and signup can open manually or on schedule without partial form/question changes.

AU05 client baselines: custom/account-field add, custom edit, account rename, move and co-captain enable submit the rendered `SignupForm.version` as `expectedFormVersion`. Missing or malformed baselines fail closed; stale baselines are rejected under the event lock before writes. Delete/disable retain their existing question-version and impact-count confirmation contract. Existing forms forward this token without introducing a new UI workflow. Capacity/code responses expose the submitted baseline separately from authoritative settings. Refreshing another card must not rebase a pending uncertain operation; client recovery binding remains deferred.

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
new fields. On Cancelled, Finalized or Archived events, the current Questions
route permits only that exact committed-add replay; every other change request
redirects to `/Admin/Events/Manage/{id}` with **This event is read-only in its
current lifecycle state.** (08-decisions.md, B5 review D16). A reused ID with
changed intent/baseline, another actor or another event fails closed and does not
disclose the previous result. A new request still
requires the current form baseline and pre-draft write authority. Every attempt
rechecks enabled Admin authority and current event visibility; hidden/discarded
results are unavailable. A deleted/inactive created field is reported as removed,
never recreated or replaced by a same-label field. Preserve the submitted request
and baseline while its result is uncertain. Existing post-first-response optional
normalization and explicit AU07 outcome remain authoritative. These operations
create event form fields only. Ordinary forms gain request identity transport;
frontend draft/uncertainty recovery and manual UI acceptance remain deferred.

### 4.6 `ADM-RULES-01` and `PUB-HOWTO-01` — Permanent guidance

**Actors and outcome:** An enabled Admin maintains one global public Rules page; any visitor reads Rules and source-controlled how-to pages, including how to submit drops.

**Entry and reachability:** Event dashboards and submission surfaces link to the global guidance. Rules is not event-owned. How-to content is delivered through normal public routes and has no in-application editor.

**Authoritative happy path:** Rules changes use normal Admin authorization, validation, concurrency, and automatic history. How-to changes use the development content workflow. General upload/submission instructions are not copied into each tile.

**Permissions and recovery:** A stale Rules edit is rejected and reloaded. The content never becomes a hidden lifecycle prerequisite. The source-controlled `/HowTo` guide is independently reachable and has no in-application editor or lifecycle dependency.

**Acceptance outcome:** Visitors have one stable guidance destination and objective-specific tile criteria remain local, while no Rules/how-to content silently blocks signup, draft, board, start, or finalization.

### 4.7 Managed-image rule

**Actors and outcome:** An authenticated Admin or participant uploads application-owned board/tile artwork or an evidence asset; retained old banner/team-image references do not authorize new controls through the shared managed storage path.

**Entry and reachability:** The owning form offers local-file selection and a server upload route. The route-backed form remains usable without the enhanced interaction.

**Authoritative happy path:** The server validates, inspects, checksums, stores, and authorizes the asset under its owning retention/privacy rule. A managed asset reference, not image bytes or an arbitrary external URL, enters the owning record.

**Failure and recovery:** Invalid type/size/content or storage failure leaves the prior asset active and shows retryable feedback. Catalogue source-image URLs remain the sole external-image exception and use catalogue fetch/cache rules.

**Acceptance outcome:** Application-owned images use one safe upload boundary; an asset failure cannot break the event identity, board, team, or evidence workflow.

## 5. Identity, signup, and roster authority

### 5.1 `PART-IDENTITY-01`, `PART-PROFILE-01`, `ADM-IDENTITY-01`, `ADM-IDENTITY-02` — Account and recovery

**Actors and outcome:** A participant creates and authenticates one website account; an Admin provides authorized account recovery; an account holder or authorized Admin recovers password or Discord access without creating a second identity.

**Entry and reachability:** Continue with Discord starts onboarding. After onboarding, Login offers Discord or public username/password. Settings exposes password, Link/Change/Unlink Discord, and My accounts. Forgot password gives neutral contact-an-Admin guidance; an authorized Admin starts an expiring reset link after out-of-band verification.

**Authoritative happy path:** Onboarding requires a unique public username, password, and first OSRS character. Password policy, throttling, ticket limits, single-use hashed reset tokens, and session invalidation follow the technical authority. A character is linked first in My accounts and is therefore preferred; it is not an ownership proof.

**Permissions and history:** Discord IDs and usernames are unique in their separate boundaries. Link/unlink/change and password reset are atomic and audited. Global SuperAdmin transfer remains separate. Event-participant ownership transfer and account merging are retired; old ownership history remains readable.

**Failure and recovery:** Expired onboarding/reset state creates no partial account. Generic login/reset feedback does not disclose account existence. Discord relinking preserves event participation, roles, characters, evidence, and history. Recovery preserves the same website identity; it does not transfer an event participant to another account.

**Acceptance outcome:** One durable website identity controls normal access, recovery, and event ownership; no display name or character match silently claims an account or participant.

### 5.2 `PART-ACCOUNT-01` — My accounts and active-character swaps

**Actors and outcome:** A participant manages an ordered set of globally linked OSRS characters, personal labels, and optional EHB defaults; the first active character in that order is the sole preferred character. The participant swaps the active event character during Live.

**Entry and reachability:** Authenticated navigation exposes My accounts and the event signup/edit flow links back to it when a needed character is missing. The Live team-board context exposes swap only when the event and participant state allow it.

**Authoritative happy path:** Global character links may be shared by multiple website accounts. My accounts position 01 is always the preferred character; reordering immediately transfers preference to the character moved into position 01, and there is no separate set-preferred action. Within one event, a character is assigned to one participant only. The primary signup Account becomes the initial active/drop-eligible character. During Live, a participant requests a swap among frozen playing accounts; it takes effect immediately at authoritative server time, serialized with submission attribution.

My Accounts saved-EHB entry, Wise Old Man fetch results, persistence, and
rendering use no more than two decimal places. Additional decimal places use
standard decimal rounding, so `3000.09582` becomes `3000.10`; existing event
snapshots remain independent and unchanged.

**Permissions and history:** Informational/alt accounts cannot become active, receive evidence credit, or enter WoM standings. Swaps are append-only and stop at event end. A global unlink never deletes historical event assignments or evidence. My-accounts EHB defaults do not rewrite event snapshots.

**Failure and recovery:** A duplicate event assignment, unavailable character or stale version fails without residue. There is no new future-effective pending-swap workflow; retained historical transitions stay readable. A corrected global character name is atomic and fails as a whole if any editable event conflicts.

Unlinking a character with an upcoming or live registration requires explicit
confirmation and fails closed when confirmation is absent. The unlink removes
the global My Accounts link only; the event registration and history remain
unchanged.

**Acceptance outcome:** Active-account authority is explicit, time-bounded, and historical; evidence uses the active account at server submission time and never offers an arbitrary credited-account selector to a participant.

### 5.3 `PART-SIGNUP-01`, `PART-SIGNUP-02` — Submit, confirm, edit, cancel, rejoin, and restore

**Actors and outcome:** An authenticated participant creates or edits one event signup, sees confirmed/waiting status, may cancel/rejoin while permitted, and receives the correct restoration outcome when an Admin restores them.

**Entry and reachability:** Public event discovery lists public signup-open events and routes them to the existing signup page; the exact-link signup route and authenticated My events destination also reach the form or confirmation. A returning owner is sent to their existing record rather than a duplicate form. Signed-out signup entry uses the existing Login route with a validated local ReturnUrl.

**Authoritative happy path:** The participant selects linked characters, enters the primary and optional custom answers, submits, and receives Confirmed or Waiting list with exact position. The signup presentation places the required/system primary regular account first, then additional playing accounts in configured order, then informational/alt accounts in configured order. On a fresh signup only the required system primary defaults to the preferred linked character; every later account question begins None/unselected and required later questions must still be answered before submission. Optional account answers expose a clear/none choice while the required primary cannot be cleared. The transaction validates answers, reserves all named characters, stores event EHB snapshots, and preserves original queue order on ordinary edits.

**Permissions and history:** Normal edit is available only while signup is open. Separate confirmed withdrawal while signup is open releases reservations and may promote the earliest waiting participant. Rejoin while open reacquires accounts with a new queue position. After close and before draft, self-withdrawal remains available but self-restore does not. Admin restoration uses current capacity and end-of-queue rules and never displaces a promoted participant.

**Failure and recovery:** A private or unpublished signup slug fails closed for non-Admins. A character conflict or capacity race rejects the whole attempt, preserves the saved record, and identifies the conflicting answer. Promotion, cancellation, withdrawal, rejoin, restoration, reservations, and status commit atomically. Required Discord onboarding and password-change handoffs preserve the validated local signup ReturnUrl. Promotion creates direct in-site destinations for the linked participant and enabled Admins; Discord contact remains manual.

**Acceptance outcome:** A participant can manage one authoritative signup through its permitted lifecycle, with deterministic capacity/queue behavior, complete confirmation, no private edit-token dependency for normal users, and preserved history after withdrawal or restoration.

### 5.4 `PUB-SIGNUP-01` — Exact-link public signup table

**Actors and outcome:** A visitor with the exact shared link reads the unlisted signup table; participants see their public-facing answers without private account, payment, or security data.

**Entry and reachability:** Admin Manage and the signup journey expose the exact link when the event phase permits. It is not a global public listing. After draft finalization, non-Admin requests for the signup-board route follow the published roster destination.

**Authoritative happy path:** The table shows confirmed and waiting profiles, primary regular OSRS character, regular-account EHB, alt headings/answers, captain volunteer, participant-facing custom answers, and exact waiting positions. Public projection rules, not the private confirmation, decide its contents.

**Permissions and history:** Website username, Discord identity, payment, Admin notes, audit/security data, inactive legacy questions/answers, private answers, and co-captain answers never enter the table. Visibility is not an invitation to edit; server signup ownership still applies.

**Failure and recovery:** A closed/draft/finalized phase returns the phase-safe destination or unavailable result. A stale form definition reloads the current version while preserving existing answers.

**Acceptance outcome:** The exact-link table is reachable when intended, useful for public signup, and unable to expose private identity, payment, security, or Admin data.

### 5.5 `ADM-PARTICIPANT-01` — Participant administration and retained private metadata

**Actors and outcome:** An enabled Admin manages confirmed, waiting, and withdrawn participants from one event workspace, including private payment and notes, pre-draft corrections and existing-account additions.

**Entry and reachability:** Admin Manage → Participants is the authoritative workspace. Detail and responsive route forms are projections of the same event-level service, not independent participant stores. Retained participant detail remains reachable to an Admin when a terminal event permits only payment or private-note maintenance; every other control on that view remains independently lifecycle-gated.

**Authoritative happy path:** Search/filter participants and inspect authorized details. F01–F06 define selected confirmation, waiting moves, restoration, question-free saved-account Add (including explicit full-capacity +1), primary selection and event-only account correction. Preserve payment/notes, account reservations and unaffected queue order. No external/accountless creation or participant-owner transfer.

**Permissions and history:** Payment is private binary Paid/Unpaid. Payment and private Admin notes remain editable and audited in every retained visible lifecycle state, including during the team draft and after Finalized, Archived, or Cancelled; Hidden and Discarded events remain inaccessible. Participant answers, registered accounts, queue, roster, evidence, and competitive history do not become editable merely because these private fields remain available. Corrections preserve queue/status; withdrawal uses one Withdrawn state with actor history. Admin action, account correction, restoration, and withdrawal notify a linked participant where the capability says so; payment, notes, and ordinary answer corrections do not.

**Failure and recovery:** Conflicting corrections fail atomically. An unavailable character or capacity limit is shown before mutation. Withdrawn records retain history and release current reservations before any promotion transaction.

**Acceptance outcome:** Admins can correct and operate the pre-draft roster and maintain retained private payment/note records without changing public privacy, queue order, account uniqueness, competitive history, or historical identities.

### 5.6 `ADM-PARTICIPANT-02` and `ADM-CAPTAIN-01` — Finalized roster corrections

Before first actual Live, an enabled Admin may Add or Remove current finalized
roster members through separate confirmed operations. Add selects an existing
website account and required Playing account, assigns Participant/Captain/Co-captain
and republishes atomically; it does not require completed signup questions, signup
capacity, a final team-size cap or automatic rebalance. Remove retains original
picks, attribution and prior publications. Failure of Add does not undo an earlier
Remove. Stale requests fail without mutation.

First Live permanently locks membership and registrations, including after early
end. No Live withdrawal, vacancy/replacement, promotion-follow-up or participant
ownership transfer. Old records remain readable without reactivating those commands.
Captain/co-captain changes for current members remain available through Live and
Final Review while uploads are open; they do not alter membership. Multiple Captains
are valid, and Live start has no Captain/credential prerequisite. Running draft
preassignment is only the explicit before-first-pick exception.

## 6. Draft, board, and catalogue-derived competition setup

### 6.1 `ADM-DRAFT-01` — Team setup, snake draft and finalization

Approved G3–G6 correction scope (3 October 2026; implementation pending):
capacity counts Confirmed event participants regardless of team inclusion; manual
team membership never frees a signup place. Manual teams still consume no draft
turns. Manual-team Add requires Confirmed status and manual membership changes
are refused while Running. Inclusion/active changes and included-team removal
must recheck the event/draft inside a transaction serialized with Start and both
Finalize paths; structural changes are refused outside Setup and retain section
4.4's postponed-start recovery window (until actual start or configured end). Live/Final Review role changes, while otherwise permitted, must update
public Teams role labels without changing membership or WOM data; previous
publications remain history and lifecycle routing is decided in the transaction.
Eligible Cancel must leave a usable Setup while retaining cancelled pick history;
G4's brief requires a proposal before changing protected history or conflicting
restart assertions. Finalized Add retains section 5.6's three-role contract.


An enabled Admin uses the version-one flow in PRODUCT_REQUIREMENTS 17.1. Existing
draft, preassignment, control renewal, latest-pick undo, manual roster, finalized
pre-Live correction and publication commands remain authoritative. No Pause/Resume,
finalized Reopen, captain-controlled shared draft or fabricated manual-roster picks.
Multiple Captains are valid; role changes remain permitted for current Live members,
while first-Live membership/registration lock remains permanent.

New UI binds existing in-place update/realtime notifications; they invalidate/read
authoritative state, never substitute for PostgreSQL. Retain pick IDs, participant/
team IDs, expected version/control and immutable intended fields through pending,
stale and uncertain outcomes. Matching pick numbers or orders cannot prove a timed-
out action's result. A read failure keeps uncertainty and does not authorize replay.
Local roster publication success is separate from actual queued/failed/unknown WOM
synchronization. AU14 provides the backend readback contract (B5, review pending);
RC04/DRF binding remains pending. Its nullable persisted draft identity, immutable
picks, retained memberships and full team/account fields never identify a request.

Public roster/pick publication never starts the event or publishes the Board.
Original/undone picks and past publications remain retained; private control identity
does not appear on the streamed board. Missing Captain/distribution/control errors
name the actual correction. Manual 0/1-team unplaced players are informational,
not an invented mandatory draft blocker. Integration stays on Teams after finalizing.

### 6.2 `ADM-BOARD-01` — Build, approve, snapshot, publish, and correct

**Actors and outcome:** An enabled Admin builds one event board, approves a complete board, publishes it separately, and performs reasoned replacement corrections without erasing prior competitive history.

**Entry and reachability:** Admin Manage → Board is the editor. Preview uses the public board treatment and inherits the public Board ecosystem's visual status; direct public board/team/tile routes remain the normal destinations after publication.

**Authoritative happy path:** Fill every grid position with valid tile/objective data. Catalogue/drop tiles require valid automatic EHB and may use the approved optional total tile override (AU11 pending); custom/manual tiles require explicit EHB. Keep the calculated baseline/reset, event-local scope, evidence scoring locks and immutable snapshots. Planning size remains manually adjustable after roster finalization without changing competitive values (AU13 pending). Approve in a transaction that rechecks completeness and creates an immutable approval snapshot. After draft finalization, use a separately confirmed Publish board action/transaction. Event start requires publication.

Tile description input is optional. Blank/whitespace-only input previews a
description derived from the current structured requirements without filling the
editable field; nonblank text remains an Admin-authored override until cleared
and saved. Drop copy preserves configured target quantities regardless of
weighting, uses a distinct selected item name when only one item is selected,
and otherwise shows selected source names as alternatives. Separate required
objectives follow stored order. Approval freezes both rendered text and mode;
catalogue or working edits do not change old approvals, and discarding a private
correction restores the prior mode and approved copy. Existing nonblank
descriptions stay manual, even if they match the former generator.

**Permissions and history:** Any enabled Admin may approve. Editing competitive content invalidates an unpublished approval and retains its history. Initial publication and publication of a corrected replacement both require server-enforced confirmation. Publication uses the active snapshot without recalculating from mutable catalogue data. Post-publication correction requires confirmation, a reason, a replacement snapshot, and preserved prior history; it is available only in SignupClosed, Live, or AwaitingFinalReview and is unavailable in terminal, Cancelled, Hidden, or Discarded states.

**Objective identity and evidence protection (C20, user approved 2026-09-14):**
Title/description corrections that do not change what players must accomplish preserve
objective and drop identities, existing evidence links and earned progress. Once any
submitted evidence references an objective, changing its substantive requirements or
scoring rules, or removing it (including through tile removal), is blocked. This
includes pending, approved, rejected, reversed and withdrawn evidence; an evidence
state change does not unlock the historical objective. Objectives with no submitted
evidence remain editable under existing lifecycle and approval/publication guards.
Descriptive edits are not permission to change an objective's meaning. During private
correction, ordinary users continue to read and submit against the active published
snapshot. Replacement approval/publication must preserve these protections, including
when evidence arrives after the private edit began; rejection must preserve the active
publication and evidence without partial changes. Do not rewrite contributions or
reconstruct broken historical references as part of this correction.

**Discard private correction (C20 recovery, user approved 2026-09-14):** An enabled
Admin may explicitly discard an open private correction from the existing Board editor.
The confirmation must clearly warn that all unpublished board edits in that correction
will be lost. On confirmed success, atomically restore the working board from its current
active immutable published approval, preserving exact original objective/drop identities,
and close the correction. The Admin may then start a fresh correction through the
existing action. Preserve the published snapshot/pointer, all submissions, contributions,
earned progress and historical snapshots; this action does not publish a replacement.
Apply existing correction lifecycle/authorization and current-version safeguards, audit
the explicit action through existing mechanisms, and fail without partial changes on
stale, invalid, missing/ambiguous restoration data or persistence failure. This is bounded
restoration of the current private copy, not reconstruction of lost historical associations,
rollback to an older publication, or deletion of evidence. New confirmation/feedback states
remain subject to user manual acceptance; general composition approval is preserved.

**Failure and recovery:** Empty positions, invalid objectives, or missing automatic catalogue EHB block approval with diagnostics; manual override cannot repair a broken standard tile. Dismissing the post-draft publication prompt leaves rosters public and the board private. Stale approval/concurrency fails without publication residue.

**Acceptance outcome:** Board approval and publication are separate, visible authority boundaries; incomplete or mutable data cannot become the competitive public snapshot by accident.

### 6.3 Draft/board catalogue coupling and approval snapshot

**Actors and outcome:** Admin catalogue edits keep unapproved board projections fresh while approved/published competitive snapshots remain immutable.

**Entry and reachability:** Catalogue edits and the board editor are independent Admin routes connected by direct invalidation/reload destinations. Approval is reached from the board editor, not from catalogue save.

**Authoritative happy path:** A draft board stores stable catalogue references and live derived names/images/rates/EHB. Relevant catalogue mutation invalidates and recalculates every affected unapproved projection. Approval locks/rechecks the referenced versions and freezes the competitive snapshot. Unapproval or a competitive edit starts a new draft/approval version while retaining superseded history.

**Permissions and history:** The catalogue owns reusable source facts; the board owns objective configuration and approval snapshots. Publication reads the active snapshot. No board copies, tile templates, or historical recalculation silently merge these ownership boundaries.

**Failure and recovery:** A stale board/catalogue version rejects approval or import and reloads authoritative values. A missing derived EHB is corrected at the catalogue/requirement source, not by a manual override on a standard tile.

**Acceptance outcome:** Draft calculation can change with catalogue data; an approved/published board cannot change without its explicit versioned correction workflow.

## 7. Live operation, evidence, and official results

### `PUB-UPDATES-01` — Participant announcements and Drops NEW state

**Approved 2026-09-12; planned, not yet implemented.** An authenticated participant
receives one queue of approved progression for their current event throughout
non-Admin pages. Offline approvals are collected; account/event acknowledgement and
the two-minute expansion cooldown survive visits/devices. Navigation preserves state;
Admin hides the banner without acknowledgement. No automatic board refresh or
interruption of active submission/result state is permitted.

Automatic expansion requires an expired cooldown and an outstanding approval newer
than the account/event's persisted last automatic-expansion boundary. Navigation or
returning later alone does not qualify. Automatic expansion selects the newest
approval; manual expansion restores selection and already-expanded interaction keeps
its selection when approvals arrive. The boundary covers only the claimed snapshot.

Banner dismissal acknowledges only queued announcements. Successfully opening the
specific approved-evidence popup through GO TO DROP or directly in Drops acknowledges
both that announcement and its NEW mark. CLEAR ALL NEW acknowledges both states for
all eligible approvals in its event snapshot; later approvals remain new. Visiting
Drops or switching/compacting the banner acknowledges neither. The coral navigation
NEW indicator follows outstanding Drops NEW state. Reversed approvals disappear;
approval of a linked corrected attempt is a new update, without allowing direct
reapproval of an immutable Reversed attempt.

Announcements continue during AwaitingFinalReview after the event timer ends. Committed
event finalization clears the entire event's banner queue, all Drops NEW marks and
the navigation NEW badge for every account, including offline accounts. Connected
and returning clients reconcile to that cleared state. Actual feed entries, approved
evidence and competitive history remain available; clearing is not deletion. Stale
clients or the existing Unfinalize flow must not resurrect acknowledged old updates.

Live feed insertion preserves filters, reading position and open interactions. Actor
eligibility and recipient state are server-authoritative. This is distinct from
personal notifications. Full approved timing/queue/presentation behaviour, boundary
cases, scope exclusions and delivery gates are in DELIVERY_PLAN's
`Drop announcements and NEW tracking` contract; UI_SYSTEM owns the countdown rule.

### 7.1 `SYS-EVENT-START-01` — Scheduled start readiness

**Actors and outcome:** The scheduler attempts start at the configured instant; an Admin clears blockers and uses Start event now when necessary.

**Entry and reachability:** The scheduler and the Admin Manage readiness action use the same lifecycle service. A postponed start exposes the blockers and a direct Admin resolution route such as Draft, Board, Schedule, or Teams.

**Authoritative happy path:** At the scheduled instant the server rechecks finalized draft, published board, valid assignments, schedule, singleton-current boundary, and all lifecycle invariants. If ready, Live begins at the actual transition time; if overdue after a postponement, an authorized Admin starts explicitly.

**Permissions and history:** Automatic start never bypasses gates or backdates eligibility. Manual start, including a start before the configured instant, requires strong confirmation but no written reason. Scheduled attempt occurrence, blocker state, actual start, and audit remain distinct.

**Failure and recovery:** A blocked attempt leaves the event pre-live, retains the configured instant, records visible failure, and does not retry into an unexpected start. Clearing blockers followed by Start event now is the recovery even after the configured start has passed, provided the configured end remains future. If the configured end has also passed, start fails closed until the Admin repairs the pre-Live event window to future values or cancels/replaces the event. This does not change automatic scheduler readiness or execution.

**Acceptance outcome:** A delayed or invalid deployment cannot silently start an unready event, and the Admin can identify and resolve every blocker.

### 7.2 `SUBMISSION-WORKSPACE-01` — Canonical Captain/participant submission workspace

**Navigation (user decision, 2026-09-14):** Public main-header Current event is visible to
everyone only for a public/non-hidden Live or Awaiting final review event with a published
board, linking to that board (Live first, then latest event start). Captain/Submissions is
an event context link after Teams rather than a main-header link. It preserves the existing
role-appropriate authorized destination for the viewed event, including scoped Captain
access, and the context row remains on authorized submissions overview/detail. Navigation
must not expose private event context or redirect a viewed event's action to another team/event.

**Actors and outcome:** Authenticated current team members use one canonical submission workspace to inspect the complete team submission history. Captains/co-captains additionally manage team focus, identify submission problems quickly, and edit eligible team submissions without gaining general Admin or review authority.

**Entry and reachability:** `/Submissions` is the canonical authenticated team submission overview and `/Submissions/{id:guid}` is its canonical detail route. The overview is team-wide for every authorized current member, including retained rows credited to departed teammates. General Participant and Captain navigation each prefer the account's sole Live team; when none is Live they use the sole Awaiting Final Review team. This Captain rule includes normal website co-captains. Finalized/Archived memberships do not suppress a current link, and genuinely ambiguous eligible teams expose no unscoped shortcut. Captains/co-captains see the two Captain-only top sections—team focus and team submission status—and retain server-authorized broader editing of eligible team submissions; ordinary participants do not see those sections and may edit only their own eligible non-read-only submissions. `/Captain` and `/Captain/Submissions/{id:guid}` are thin compatibility redirects/aliases to the canonical routes, not separate rendered implementations. Personal submission/evidence notifications resolve to the canonical detail route; general submission navigation resolves to the overview. Evidence submission still starts from the normal team-board tile and uses exactly its shared drawer/interface. `/Captain/Submit/{tileId?}` remains only the drawer transport/handler plus a compatibility redirect for old direct links. Below 901px and without JavaScript, team and tile routes remain usable directly.

**Authoritative happy path:** Before/during draft, drafted-team captains see the expanded confirmed signup projection needed for selection, excluding payment, notes, security, and audit. During Live, the canonical overview first shows Captain-only team focus controls, then Captain-only pending/rejected/approved submission status totals, then the complete team submission ledger covering pending, approved, rejected, withdrawn, replaced, and other retained historical states. Team focus is visible read-only to the whole current team on its ordinary board. Status, player, and tile filters narrow the ledger without changing authority; each result links to the canonical detail route and available reviewer feedback. The detail composition remains visually equivalent to the approved Captain submission detail. Captain-authorized submission for a current teammate continues only through the ordinary team-board drawer and shared drawer transport route, not through a second form or tile grid.

**Permissions and history:** The server derives the credited participant and active playing account. Focus is team-private, concurrency-protected, non-competitive, visible read-only to current teammates, hidden from opponents and the public, and read-only at event end. Captain and co-captain access retain the same team-management scope already granted to them, including broader editing of eligible team submissions; ordinary participants may edit only their own eligible non-read-only submissions. Every canonical detail request independently enforces current-team, owner, role, retained-state, cutoff, privacy, and Admin boundaries. Captains do not design boards, approve/reject/reverse evidence, review another team's private evidence, manage rosters, grant authority, or gain any Admin review control.

**Failure and recovery:** A stale focus or submission mutation reloads current team state. A removed role/membership loses authority immediately. An unavailable, legacy-linked, or no-longer-visible ledger record resolves through the canonical route and then fails closed without leaking another team's evidence. Relative `_EvidenceUpload` partial references must be rooted correctly so `/Submissions/{id:guid}` cannot 500. External members without explicit website ownership do not gain authority from retained emergency credentials or inferred ownership.

**Acceptance outcome:** Current team members inspect submissions from one canonical overview/detail implementation; Captains coordinate focus and resolve eligible team submission problems there, ordinary participants retain owner-only mutation, all new submissions still use the ordinary team-board experience, and review remains Admin-only.

### 7.3 `PART-LIVE-01` — Live participant workflow

**Actors and outcome:** A current participant reaches the published team roster or existing team-board view, sees compact active-account context, submits and manages own evidence, swaps active playing account within the Live window, and sees the authorized read-only team-focus projection.

**Entry and reachability:** After draft finalization, destination is the existing public `/Events/{slug}/Teams` roster until board publication; afterward it is the existing Board route. Once the Board is published, the unchanged Teams roster joins the shared Boards/Drops/Leaderboards context navigation as localized Teams/Hold and remains directly reachable from every Board-family view. Before Board publication the Teams roster remains standalone without that navigation, and Board-family routes remain unavailable. The participant's My events/current-event action resolves the same state-aware destination. Team cards navigate normally to the rendered team-board route at every viewport. The team board exposes only compact active-account context and swap when authorized; it does not render duplicate participant lifecycle or Captain focus-operation panels. The shared submission drawer is launched from the team-board tile flow; team and tile routes remain direct, reload, history, and browser-Back destinations. In the team board's main progress sidebar, all authorized current members use the canonical `Team history` navigation to `/Submissions`; legacy Captain links may redirect through `/Captain` but must not render a separate workspace. `/Submissions` shows the authorized team's complete retained submission ledger including departed credited members, and `/Submissions/{id:guid}` is its detail route.

**Authoritative happy path:** A participant submits only for themselves; the server snapshots the current active playing account at submission time. The participant view of the canonical ledger contains no Captain-only team-focus or team-submission-status sections. Only the credited owner may edit pending evidence, replace its active screenshot, or withdraw through cutoff; Captains/co-captains retain their server-authorized broader editing of eligible team submissions. A later attempt after rejection or reversal is an ordinary new submission with independent attribution and review. Approved, withdrawn, replaced, reversed, and other retained states, plus teammate-owned rows for ordinary participants, are read-only. An account that is also Admin/SuperAdmin retains exactly this genuine event-role experience in addition to separate Admin navigation/review; global status alone grants no team scope. Event end closes new-drop eligibility and swaps but leaves upload grace for in-window drops; cutoff closes participant mutation while Admin review continues.

**Permissions and history:** Current team members see the team's retained submission history, feedback, and private evidence, including rows credited to departed members. Former members, anonymous users, and cross-team viewers fail closed except that an archived former credited owner may follow the existing account-history destination to only their own Rejected/Withdrawn detail and evidence asset read-only. Approved evidence is public. Team focus is read-only to ordinary members and public board projection remains unchanged.

**Failure and recovery:** A rejected or reversed attempt remains immutable history. A later ordinary attempt through the active/reopened upload cutoff uses a new image and independently resolves credited participant/account; no one-child or predecessor-copy rule applies. Realtime invalidation does not interrupt an active submission/result. After draft start, self-withdrawal directs the participant to an Admin.

**Acceptance outcome:** Live participants can follow the existing board context without a hidden account selector, invalid post-end drop, private-evidence leak, or broken responsive route.

### 7.4 `ADM-EVENT-END-01` — End, grace period, and resume

**Actors and outcome:** The scheduler ends Live at the configured event end; an Admin may end early, review during the submission grace period, or resume a premature end before official finalization.

**Entry and reachability:** Manage exposes End event, Resume event, and cutoff state; Review and Finalize remain reachable after the transition.

**Authoritative happy path:** Scheduled end uses the configured effective end even if processing is late. Early end uses confirmation time and a written reason. Uploads for in-window drops remain valid until the separate cutoff. Resume requires strong confirmation, a reason, singleton clearance, and prospective Live eligibility. It reuses the configured end when that instant remains future; otherwise it requires a replacement future end. A supplied replacement end remains optional when the retained configured end is still future.

**Permissions and history:** Event end preserves original schedule/history; early end sets upload cutoff to actual end plus 30 minutes, without rewriting evidence, swaps, focus or roster history. Resume preserves the final-review interval and all records made during it; it is not available from Finalized or Archived.

**Failure and recovery:** A late worker catches up without extending play. A post-end screenshot timestamp fails review when it proves an out-of-window drop. Resume failure leaves the final-review state unchanged. Membership/registration remains locked after first Live; resume does not restore retired replacement actions.

**Acceptance outcome:** Competitive eligibility ends at the authoritative time, upload grace is distinct, and a premature end can be corrected without erasing history or backdating new play.

### 7.5 `ADM-REVIEW-01` — Evidence review, correction, and reversal

**Actors and outcome:** Any enabled Admin reviews pending evidence; a Captain or participant receives only the rejection/history scope their role allows, while later attempts use the ordinary submission path.

**Entry and reachability:** Admin Review navigation opens the currently selected non-hidden event’s evidence queue, including events with no evidence or outside submission-eligible lifecycle states. The queue combines live search across team, credited player and tile names with a Status filter without page reloads; it has no separate event/team/tile dropdowns. Pending submissions sort first, newest first within Pending and within the remaining rows; filtering preserves that order among visible results. Missing, invalid or hidden event context must not silently select a different event. Admin Details derives event context from the authorized submission, including direct loads, retaining the event selector and sidebar. Back to review returns to that event’s queue and preserves search/status from filtered queue entry. Participant/Captain history links open the same submission through their authorized projection.

**Authoritative happy path:** An Admin approves or rejects. Rejection requires a reason and leaves an immutable historical attempt; any later attempt through the active/reopened upload window is an ordinary new submission with a new image, immutable server time, and normal review. Before approval, a reasoned correction may change tile, requirement, drop, or credited account from current or released Playing assignments in the event belonging to current or former members of the submission’s own team when evidence supports it (AU17a; D11 option b, Informational accounts excluded); participant is derived from the account and a changed target receives its authoritative frozen weight. Approval reversal requires strong confirmation and a reason, then recalculates all affected progress/rankings. The Reversed attempt remains immutable. Historical predecessor links and the `Resubmit` enum are retained for display-only legacy history and are not used to create or constrain new attempts.

**Permissions and history:** Submission time, destination-derived board snapshot weight, calculated contribution, original image, review actions, and any historical predecessor links are preserved. Admins cannot upload or replace another user's evidence image. Retained ReviewAction history is authoritative for evidence decisions and corrections; the shared Audit presentation renders it once and uses the immutable audit entry as a fallback for records without a retained ReviewAction. Notifications reach the credited participant and current team captains where applicable, without notifying another team or exposing private evidence.

B5 AU17 backend exposes structured add/weight/remaining/used/target/completes from
the same allocation path as approval. Refined BR-1 returns its existing blocker
identity/time instead of a contribution claim. No-store readback returns coherent
current identity/version/status/full corrected fields and latest review attribution,
not a request receipt. Failed reads stay unknown; equal-time legacy actions without
reliable ordering do not invent latest-action attribution. No replay or per-account
context is added. RC07 binding and external Claude review remain pending.

Approved G1–G2 correction scope (3 October 2026; implemented and rechecked):
approval of a later upload is blocked only when it reduces the credit available
to an earlier Pending upload for the same team/objective, using existing claimed
weights, remaining room, per-drop limits and duplicate rules. Evaluate earlier
uploads in immutable SubmittedAt order with a deterministic tie-break inside the
serialized approval transaction. Return the earliest affected submission ID/time
as structured data and link it on the existing review page. Resolving that earlier
upload removes its reservation; room for all permits out-of-order approval.
Already-approved history and scoring remain unchanged. Screenshot game time
controls activity eligibility; upload time controls cutoff/order. Without trusted
screenshot time, show paused intervals with guidance to verify the screenshot,
never an ineligibility instruction inferred from SubmittedAt.

**Failure and recovery:** Optimistic concurrency rejects a stale decision. A duplicate/unusable image is Reject, not a third review state. Rejected and Reversed attempts remain history; a later attempt is ordinary and is not blocked by a one-child relationship. Historical predecessor links and enum values are display-only, so retries cannot create duplicate linked children. Direct Reversed reapproval is forbidden. Cutoff closes participant/captain mutation but not Admin review.

**Acceptance outcome:** Evidence decisions are binary and auditable, correction and reversal are reasoned, and all progress effects follow the authoritative submission/data-model rules.

### 7.6 `ADM-FINALIZE-01` — Final review, official results, and unfinalize

**Actors and outcome:** An enabled Admin resolves final-review blockers and finalizes official results; a later correction can unfinalize without reopening uploads or deleting the prior official version.

**Entry and reachability:** Finalize is reachable from Admin Manage after event end and cutoff. Review queue and blocker links are direct destinations.

**Authoritative happy path:** Closed uploads, zero Pending and valid calculated
placements are mandatory. Normal publication requires one confirmation, no reason,
and atomically stores an immutable official result version plus Archived state.
Optional final WOM refresh may fail/skip without blocking valid publication.

**Permissions and history:** No blocker override, completion-time edit, inspection
acknowledgment or manual tie resolution. Reopen needs confirmation/reason, retains
previous versions and returns to Final Review without reopening uploads. Competitive
input/version checks reject stale publication and review mutations. Historical
resolutions remain read-only and never satisfy current gates.

Current-score time is the latest immutable completion time among currently complete active-generation tiles, null when none is complete. Retained completion-time correction rows are historical only and are ignored by current readiness and ranking; existing official snapshots preserve their stored order and fields. Do not rerank old versions or reactivate time-edit/inspection controls. AU12 applies only to new events via immutable `events.placement_rule`: ordinary creation explicitly selects `CreditedEhbThenScoreTime` (1); all pre-migration rows and historical imports retain `LegacyScoreTimeThenEhb` (0). Provisional and final calculation use that persisted value, never dates.

**Failure and recovery:** Stale readiness, concurrent finalization, or an unresolved mandatory blocker fails before official mutation. Captain website roles remain historical but cannot mutate closed/finalized events. Assignment of a Captain never auto-generates a password; retained emergency credentials cannot authenticate or regain authority.

**Acceptance outcome:** Official results have one explicit immutable version at a time, every unresolved competitive blocker is visible, and correction cannot silently rewrite evidence or reopen gameplay.

## 8. Lifecycle, archive, history, and deferred feedback

### 8.1 `ADM-EVENT-ARCHIVE-01`, `ADM-EVENT-CANCEL-01`, `SYS-CURRENT-EVENT-01`

Publishing official results atomically archives the event; there is no separate
Archive action or new Finalized resting state. Legacy Finalized records and prior
snapshots remain readable. Protected pre-Live events may be cancelled with reason
and confirmation; empty unprotected experiments use Delete/discard. Cancellation
stops scheduled/mutation work, preserves history and has no ordinary resume.

Current-event exclusivity is checked transactionally for transitions, restore and
reasoned result reopening. Development fixtures are an explicit exception, never
production policy. Public/history routes remain stable; hidden-event access follows
the separate quarantine contract below. A failed/stale transition mutates nothing.

### 8.1a `SUPERADMIN-EVENT-QUARANTINE-01` — Hidden-event quarantine

**Actors and outcome:** Only the designated Super Admin may reversibly hide or
restore an event as an orthogonal administrative quarantine. Hide and Restore
are available in Events Control and the Manage Danger Zone, and each requires
ordinary shared confirmation and a complete immutable audit entry. Hide requires
a reason; Restore accepts none. Neither uses typed event-name confirmation.

**Entry and reachability:** Events Control has a clearly separated Hidden
filter/area. A hidden event's only rendered destination is the limited
SuperAdmin Manage inspection surface reached from that area; it shows retained
lifecycle information, hide/restore audit history, and Restore. No ordinary
event workspace or mutation is reachable while hidden.

**Audit scope clarification — 2026-09-14:** Hiding is intended to remove clutter
from the front page and ordinary Admin Events table, not conceal details from Admin
audit history. Admin audit is an exception to the hidden-event projection restriction;
C36 requires no extra suppression or legacy cleanup. This does not reopen event
workspaces or change existing hide/restore permissions, event routes or source filters.

**Authoritative happy path:** Hide succeeds only for `AWAITING_FINAL_REVIEW`,
`FINALIZED`, or `ARCHIVED`. It records hiding metadata and removes the event
from every ordinary discovery, history, account, submission, evidence,
notification, action, and realtime projection. Restore clears only that
metadata and returns the unchanged lifecycle and event data. Restoration of a current-state
event is rejected if another visible current event exists, using the same
current-event definition, development-fixture exclusions, and transactional lock
as Start and Resume. Rejection retains all hiding metadata, lifecycle data and
history and does not hide, end, or otherwise change the other event. Archived
events remain restorable without claiming the current-event slot.

**Permissions and history:** Draft, SignupOpen, SignupClosed, Live, Cancelled,
and Discarded events cannot be hidden. Public visitors, participants,
Captains/co-captains, emergency authority, and ordinary Admins receive 404 or
an absent projection for hidden events. Super Admin does not bypass the rule on
public routes. All database relations, snapshots, rankings, evidence, audit
history, managed assets, and storage objects remain retained. Hide and Restore
emit no notification.

**Failure and recovery:** Invalid state, non-SuperAdmin authority, missing
Hide reason, missing confirmation, stale concurrency, or an attempted ordinary
workspace access fails without mutation or disclosure. Hidden events have no
active-event scheduler, signup, singleton/window-collision, or active realtime
processing because eligibility begins after Live. Restore is the only recovery
before ordinary event operations resume.

**Acceptance outcome:** A Super Admin can quarantine and restore an eligible
post-Live event with full accountability while every other role and route is
fail-closed and the competitive record remains unchanged.

### 8.2 `ADM-ACCOUNT-01` and `PART-HISTORY-01` — Access lifecycle and archive reading

**Actors and outcome:** An authorized Admin disables/restores website access; an archived participant reads their own permitted evidence history while public visitors read preserved public results.

**Entry and reachability:** Admin Accounts exposes role-appropriate disable and restore. Archived event routes expose public boards/teams/results; My events or the event history destination exposes the participant's own rejected/withdrawn history.

**Authoritative happy path:** Admin disables a User, or Super Admin disables an Admin, with strong confirmation and written reason. Sessions invalidate and the account becomes inactive. Restore requires confirmation, records history, and restores authentication only; it does not resurrect expired event authority.

**Permissions and history:** No actor disables self or the active Super Admin. Disable preserves usernames, Discord link, characters, participants, roles, submissions, evidence, contributions, snapshots, and history. Archived participants have no mutation controls, swaps, focus changes, upload, pending edit, withdrawal, or later evidence attempts.

**Failure and recovery:** Role, self, Super Admin, or stale-state violations fail without disabling. A team losing its only usable Captain exposes the normal readiness/live warning. Re-enable never merges/deletes accounts.

**Acceptance outcome:** Access removal is immediate and reversible without destroying competitive history; archived participant access is read-only and privacy-scoped.

### 8.3 `PUB-FEEDBACK-01` — External feedback

**Actors and outcome:** Visitors and participants report bugs, evidence concerns, or general feedback through the community Discord channel; the application has no version-one public feedback/report form.

**Entry and reachability:** Rules/how-to content may identify the Discord path. There is no feedback persistence, public API, or in-application evidence-report journey to route or authorize.

**Authoritative happy path:** A reported evidence concern that is valid enters the Admin Review reversal and normal cutoff-bound ordinary submission workflows.

**Permissions and recovery:** The absence of a feedback form is a deferred scope boundary, not a failure state. No report is treated as evidence, progress, or an Admin action until an authorized workflow accepts it.

**Acceptance outcome:** Community feedback remains outside version-one product scope, while genuine evidence correction still has a reasoned, auditable path.

## 9. Global administration and supplementary integration

### 9.1 `SUPERADMIN-01` — Global ownership and role administration

**Actors and outcome:** The sole Super Admin manages global Admin grants, revocations, ownership transfer, and owner-only capabilities; ordinary Admins retain event administration but cannot manage global roles.

**Entry and reachability:** Admin Accounts exposes grant/revoke and Transfer Super Admin only when the server policy permits. Operator owner recovery has no public or ordinary web action.

**Authoritative happy path:** Grant/revoke targets an existing normal account, confirms before/after roles, records history, and invalidates affected sessions. Transfer requires the current owner's password and typed destination public username against the retained selected recipient/version, then atomically promotes the destination and demotes the previous owner, leaving exactly one active Super Admin. The typed check is the approved AU24 target, still pending; it is not required for ordinary grants/revocations.

**Permissions and history:** The current owner cannot self-demote except through a valid transfer. Admin role is independent of event membership and Captain role. Emergency credentials cannot become global roles. Cross-team focus is hidden from Super Admin by default and inspection is read-only unless team membership separately grants normal focus authority.

**Failure and recovery:** Ordinary Admin grant/revoke/transfer, invalid target, stale confirmation, or zero/multiple-owner result fails atomically. Lost-owner recovery is operator-controlled and audited, not a public first-user election.

**Acceptance outcome:** Global ownership remains unique and explicit; session authority changes immediately; global role does not bypass event/team privacy.

### 9.2 `ADM-CATALOGUE-01` — Catalogue administration

**Actors and outcome:** An enabled Admin maintains boss/activity and source-drop catalogue records; only Super Admin permanently deletes genuinely unused records where dependencies permit.

**Entry and reachability:** Admin Catalogue exposes normal CRUD, activation, deactivation, and source-image cache operations. The application catalogue-import preview/apply interface is excluded by user decision (D03, reaffirmed 2026-09-14); existing operator tooling is a separate scope, not permission to add an application route or handler.

**Authoritative happy path:** Catalogue edits are optimistic-concurrency protected
and audit before/after values. Referenced records deactivate rather than hard-delete.
Only the existing `roll_group` is editable among the retained advanced mechanics,
and only a database-checked SuperAdmin may edit it; ordinary Admins see it as
read-only. Every role may enter ordinary rate text, including `N x`, while
`probability_scope`, `conditional_on_parent`, `parent_probability`, and
`assumed_participants` are retired input and refused on every write path. B4
delivers the AU23 backend contract; WA-5 still binds the decided panel. Preserve
source-specific validation, affected Draft recalculation and approved/historical
snapshots. No separate roll-group management or application import workflow.
Shared-item adoption must be explicit and distinguish shared metadata from source
rates. Saving one independent form does not silently save/discard another.
The activity `team_size` is informational, defaults to 1, requires an integer at
least 1, and is editable by every Admin without changing EHB or historical
snapshots.

**Permissions and history:** Only genuinely unused records can be permanently deleted after a complete dependency check; blocked deletion offers deactivation. Listing individual dependency references is not required (C26, reaffirmed 2026-09-14). Catalogue source-image URLs are the sole external image exception. Preserve referenced history.

**Failure and recovery:** Stale edits abort without overwriting newer values. A board projection reloads after relevant catalogue change; approval remains the board snapshot boundary.

**Acceptance outcome:** Ordinary Admin catalogue work is safe and reversible; destructive deletion is Super-Admin-only and dependency-safe. Neither an application import UI nor a dependency-reference list is required.

### 9.3 `ADM-AUDIT-01` — Immutable audit history

**Actors and outcome:** Any enabled Admin searches retained audit history; no actor edits, deletes, or exports it in version one.

**Entry and reachability:** Admin Audit is a server-paginated, newest-first route with filters for event, actor, action, entity, and date. Target-identifier filtering (C40) is outside the current batch and requires a separately agreed Audit scope.

**Authoritative happy path:** Filter changes return to page one; pagination retains filters; entry detail renders structured before/after labels and values. The retained history is independent of the display page size.

**Permissions and history:** Audit entries are immutable. Hiding an event is not
an Admin-audit secrecy boundary (C36 closed by user clarification, 2026-09-14).
No additional suppression or legacy association repair is required for this purpose;
normal audit authorization and sensitive-data protections still apply. Passwords,
hashes, tokens, OAuth secrets, evidence credentials, and unnecessary raw
Discord IDs do not enter snapshots or request context. Security logs remain
distinct where specified.

**Failure and recovery:** Stale/invalid filters return safe empty or validation feedback without weakening authorization. Reading detail never mutates the entry or resolves a business action.

**Acceptance outcome:** Admins can trace actor/time/before-after history without turning the audit view into an editable or secret-bearing data export.

### 9.4 `ADM-ACCOUNT-OVERVIEW-01` — Account overview

**Actors and outcome:** Admins inspect normal website accounts; each action is independently authorized. Retained emergency identities are absent from operational account controls.

**Entry and reachability:** Admin Accounts provides server-side search/filter and pagination for website-account views, with details reachable from rendered rows.

**Authoritative happy path:** Website rows expose username, global role, active state, Discord link state, last login, event-role summary, linked characters, event history, and disable history.

**Permissions and history:** The overview never exposes passwords, OAuth data, setup/reset token values or hashes, or unnecessary Discord identifiers. Role, reset, disable, ownership-transfer, and event-participant transfer controls remain separately gated. No merge or permanent normal-account deletion exists.

**Emergency retirement:** Existing identities and actor history remain retained. Creation and management routes return unavailable outcomes; emergency tokens and sessions cannot restore authority. Live-start readiness no longer inspects credentials.

**Failure and recovery:** A stale row action reloads current state and refuses to apply to a changed role/credential. Lost-owner recovery remains operator-only.

**Acceptance outcome:** Account administration is inspectable without conflating global identity, event ownership, emergency access, or security secrets.

### 9.5 `ADM-INBOX-01` — Personal notifications and unresolved Admin actions

**Actors and outcome:** A website account reads recipient-specific notifications; an Admin works an action projection for pending evidence and unresolved scheduled opening/start failures. Retired vacancy, promotion-follow-up and missing-Captain categories are not new actions.

**Entry and reachability:** Notifications are reachable from the authenticated shell and each direct destination. Admin overview/action inbox is reachable from the Admin shell; notification and action links are independently server-gated.

**Authoritative happy path:** Opening a personal notification marks it read and lands on the relevant event/team/submission destination. Every personal submission/evidence notification routes to `/Submissions/{id:guid}`; relevant general submission navigation routes to `/Submissions`. The canonical detail independently authorizes the credited owner, current team member, Captain/co-captain team scope, retained state, and mutation boundary. Admin review notifications remain `/Admin/Review/Details/{id}`. An Admin action remains until its underlying lifecycle/evidence/roster condition resolves.

**Permissions and history:** Recipient, role, event scope, and privacy are checked at destination. Routine configuration and audit activity do not flood the inbox. Notification wording does not expose private reasons or hidden evidence.

**Failure and recovery:** A missing/closed destination shows the current safe state rather than a dead link. Repeated delivery is idempotent at the owning transition boundary; reading is not dismissal of the underlying action.

**Acceptance outcome:** Notifications are useful navigational reminders while Admin action state remains derived from authoritative records.

### 9.6 Wise Old Man supplemental journeys — account lookup and cached activity

**Actors and outcome:** A participant explicitly requests EHB lookup for a regular account in My accounts or signup/edit; an event optionally displays a cached competition-activity projection while Live.

**Entry and reachability:** Fetch from Wise Old Man is an explicit control after an account name is present. It never runs on render, typing, selection, save, public viewing, or event lifecycle transition. Admin competition configuration links one existing competition to an event through its event setup/Manage route; public pages read only the cached projection.

**Authoritative happy path:** A successful explicit signup/edit account lookup fills the current EHB control and immediately updates the authenticated owner's existing linked My Accounts character with the fetched EHB before signup submission; it creates or changes no event participant or assignment. Manual entry remains available, and a submitted signup stores the manual or freshly fetched event EHB snapshot. During Live, one cached competition-details synchronization fetches all relevant data no more often than the approved interval and derives participant/team activity locally.

**Permissions and history:** Wise Old Man remains read-only and supplementary
for participants, public pages and cached statistics. External ID-only
competitions are provider-read-only; supplied protected credentials permit supported
updates but never external provider deletion. A narrowly scoped enabled Admin exception may explicitly create,
automatically manage, or delete a competition created through the Admin-managed
flow described in this contract. Every regular `PLAYING` event assignment may
contribute full cached competition delta; informational/alts are excluded.
Cached activity is not official results and never changes signup snapshots,
evidence credit, lifecycle readiness, or finalization authority. Public/team
projections expose only privacy-safe matched totals, provisional/partial state,
and coverage counts; exact missing names remain Admin-only.

**Failure and recovery:** Rate limit, unavailable, malformed, not-found, or partial responses produce accurate retry/incomplete/manual-entry feedback and never clear a valid entered EHB or the owner-linked My Accounts value or block an event transition. Missing accounts have no zero or carried-forward value; zero matches show no rankings. During Live, an Admin may replace a failed competition integration only with a validated competition whose start and end already match the event exactly as UTC instants; Live replacement cannot clear the integration or synchronize the event schedule. Fetches remain eligible through Final Review and stop in terminal states, retaining readable cache. No Wise Old Man notification family or per-viewer request is introduced.

For an Admin-managed competition, local validation rejects malformed or
normalized-duplicate names, missing authoritative assignments, incompatible
schedule, and any active event team with no eligible Playing account before a
POST or PUT. The empty-team message is **Cannot create WOM competition with
empty teams.** and identifies affected teams. Syntactically valid but untracked
names do not trigger existence lookups. Provider errors preserve the complete
intended roster and become retryable, corrective, or unknown operation states;
timeouts after dispatch never cause an automatic second Create. Durable claims
coalesce permitted changes and fence stale responses. Creation saves the
existing link and protected receipt in one local persistence path. Once actual
Live has begun, a dispatched operation may reconcile but a new roster or delete
write cannot be sent. Remote deletion requires an explicit confirmed action and
is forbidden after the first actual Live start; it never removes local history.

**Acceptance outcome:** WoM provides only explicit account lookup and cached Live competition activity, with manual EHB and the Bingo event model remaining authoritative and public output privacy-safe.

### 9.7 `ADM-HISTORICAL-IMPORT-01` — Frozen historical event import

**Actors and outcome:** An explicitly authorized operator runs the one-time import of **Det Store Danske Sommerbingo 2026**. The outcome is one audited, publicly readable `Archived` event whose board result and separate Wise Old Man activity ranking reproduce the approved historical record.

**Entry and reachability:** The importer is reached only through explicit operator CLI preflight and apply controls. Preflight must complete before apply. The imported event is created or matched directly in `Archived` with `Europe/Copenhagen`, `2026-07-14 18:00 CEST` (`16:00Z`) through `2026-07-19 18:00 CEST` (`16:00Z`), and `ArchivedAt` equal to event end. No web route, ordinary Admin control, temporary `Live` state, or ongoing synchronization is introduced.

**Authoritative happy path:** The operator validates the private 90-participant/93-account mapping outside Git, the six public board-spelled teams of 15, the public WOM competition link `145197`, the complete frozen per-account start/end/gained EHB and synchronization snapshot, the approved English 5×5 manifest, and the exact corrected 402 counter units across 150 team/tile cells. The reviewed public manifest SHA-256 is `e5297b20fc5e4a842b6a1e5ab378128cbe1c2bad16033fc875c54607c0d49438`. The import applies the fixed eligibility and deterministic contribution rules in `PRODUCT_REQUIREMENTS.md` and `DATA_MODEL.md`, writes the event and retained history atomically, and records the source identifiers/hashes, actor, time, and result in audit history. Apply requires exact event-name confirmation and validates the active SuperAdmin actor inside the locked serializable transaction.

**Permissions and history:** The source-to-website-account mapping is never inferred and is not versioned in Git; its approved secondary direction is primary `Ezzi → Also Ezzi` and primary `wolles → w olles`. Board standings remain official; WOM EHB/activity ranking remains separate and uses the complete frozen source snapshot without normal refresh. Reconstructed contributions have null item/evidence associations, appear in public Recent Drops as approved historical rows, and never fabricate a drop or evidence asset; they use only the exact historical disclosure specified by the product contract. Existing event history, snapshots, audit records, and source identity are retained.

**Failure and recovery:** Preflight fails closed for missing or conflicting source data, invalid counters, unresolved account identity, non-deterministic attribution, a non-matching event, an attempted Live transition, or an unsafe persistence shape. Apply is transactional; any failure rolls back without leaving a partial event, roster, board, contribution, or audit state. A retry is a no-op only for an exact matching import hash; a different hash is not silently merged or overwritten. The operator must identify and correct the reported source/private input or retained-record conflict, then rerun preflight. No production mutation is part of implementation or rehearsal.

**Acceptance outcome:** The dormant importer can be deployed independently, can reproduce the approved archived record deterministically, exposes no private roster data through versioned metadata or public projections, and cannot turn a historical import into live synchronization or an unreviewed production mutation.

## 10. Unresolved and deferred decisions

- **F-04 — superseded by simplification/AU08:** name, description and buy-in remain editable through Live/Final Review; timezone alone locks at first Live. Schedule corrections remain separately authorized.
- **F-06 — resolved:** the source-controlled `/HowTo` guide is implemented as five anchor-linked steps covering event discovery, signup, board progress, evidence submission, and review tracking. The global Rules page remains a separate functional boundary; no in-application editor is added for How To content.
- External feedback remains deferred to the community Discord path; no version-one application feedback form is added.
- Wise Old Man availability, cache completeness, and integration configuration remain non-blocking for event lifecycle; detailed API/operational limits stay in the technical and data authorities.
- **Evidence-attempt simplification — resolved:** rejected and reversed attempts remain immutable history, while every later attempt is an ordinary submission. Historical predecessor links and `Resubmit`/related review enum values remain readable for legacy display only; no future linked-child workflow is active or required.

## 11. Whole-workflow acceptance boundary

The version-one functional foundation described here defines the following outcomes, subject to final whole-application and release regression. This section does not approve an unapproved UI page or claim that the application has passed final regression.

- An Admin can create a private event, configure identity/schedule/signup, reach readiness, and open signup without partial state.
- A participant can authenticate, complete signup, receive deterministic confirmed/waiting status, manage linked accounts, and recover access without duplicate identity or event ownership.
- Admins can correct pre-draft participants, make finalized pre-first-Live Add/Remove corrections and edit current-member Captain roles, run the draft, and preserve roster/pick history.
- Admins can build, approve, separately publish, and correct a board while catalogue changes remain live only until an approval snapshot is created.
- Scheduled start, Live account/focus/evidence operation, submission grace, review, reversal, finalization, unfinalization, archive, and cancellation preserve authoritative time, privacy, concurrency, and historical records.
- Captain/co-captain, Super Admin, ordinary Admin, participant, emergency credential, and public projections each receive only their intended scope.
- Notifications resolve to valid destinations and remain supplementary to the underlying event, roster, evidence, account, or lifecycle record.
- Development reset provides explicit, bounded manual-acceptance journeys; production does not inherit the fixture exemption.
- F-06 is resolved by the implemented source-controlled How To guide. F-04 follows the revised Identity field permissions above; F-05 is resolved by documentation reconciliation. Wise Old Man remains optional/supplementary, manual signup EHB remains authoritative, and no lifecycle action depends on it.

## Luck calculation, saved snapshots and final review — active 2026-10-01

The public Stats and selected-tile journeys use one saved Luck snapshot. A
successful normal WOM synchronization is the calculation boundary: after the
provider response is accepted, the service captures the current approved,
non-reversed evidence and calculates all scopes inside the existing event lock,
transaction, lease, generation, source and assignment fences. Provider success
does not assert that OSRS has caught up; the returned activity and upstream times
remain authoritative.

Reads project the saved snapshot only. They do not fetch, recalculate from current
evidence, or rewrite timestamps. Approval, reversal and ordinary Live → final
review → finalized → archived transitions retain a compatible prior snapshot and
its quiet Last updated time. A genuinely changed event, roster, competition,
source or assignment identity remains incompatible and cannot be presented as a
current result. Privacy, public-board visibility and historical placement
boundaries continue to apply.

Luck is the fixed mid-rank percentile `100 × (P(X < received) + 0.5 × P(X = received))`
on 0–100. The existing bounded distribution engine remains responsible for
conditional probabilities, reward rolls, mutually exclusive outcomes, independent
groups and numerical limits. KC difference is calculated per character and
boss/activity metric as `r / lambda - k`, where `lambda` is the deduplicated
expected eligible outcomes per kill, `r` is the approved eligible outcome count,
and `k` is recorded KC. Each character/activity is subtracted once; roll groups,
items and repeated tile placements never duplicate it. Team/player and tile/team/
contributor projections retain separate aggregate and activity results.

Partial provider responses retain a compatible prior complete snapshot as a whole;
new boss data is never mixed with an old aggregate under one timestamp. With no
compatible prior, independently complete scopes may be exposed while dependent
aggregates remain unavailable. Missing, unranked, estimated, zero-recorded,
unsupported and numerically bounded states remain distinct. A failed or skipped
refresh retains the last good result and does not block publication.

During AwaitingFinalReview, normal scheduling and manual-refresh limits remain in
force. Before publish/archive, an eligible event may make one normal refresh under
the same hourly/cooldown/lease rules and event-window authority. HTTP is outside
the finalization transaction; after the attempt, event/evidence/finalization
concurrency is revalidated. Duplicate publish/archive calls do not make duplicate
fetches, and no post-archive refresh is queued.

The only historical write exception is an explicit, bounded v1-to-v2 conversion.
It is idempotent, uses only retained v1 observations/rates/attribution, preserves
original calculation/fetch/upstream times, records conversion provenance, and
leaves unsupported or malformed scopes unavailable. It never changes evidence,
placements, event mechanics or provider data.


## Stats Pass 1 catalogue mapping and price contract — authorized 2026-09-15

Existing Admin Catalogue editors gain a collapsed API section. An Admin may suggest an
exact item-name mapping, edit the Wiki item ID or WOM source metric, validate and save the
mapping, or save unverified configuration during an outage. Validation shows the matched
item name/icon; unsupported, unconfigured and temporarily unavailable are distinct. Manual
value (including zero) and explicit untradeable classification remain available; a new
catalogue item needs a value. Shared-item metadata is version-checked and audited. API
refresh never replaces explicit manual values. Operator reporting is read-only by default;
applying exact-name mappings/prices requires an explicit CLI switch and audited actor.
No event prices or Stats display change in this pass. No API call occurs on ordinary page
render, field typing or selection. Requested validation/suggestion/refresh operations alone
fetch provider data, using bulk requests and the existing WOM limiter for WOM metadata.

## Shared Luck score — current contract

The former signed bounded score is superseded by the implemented
[Luck snapshot contract](#luck-calculation-saved-snapshots-and-final-review--active-2026-10-01).
Use the 0–100 percentile/KC switch and retained freshness; do not treat older
formula/display instructions as a second implementation target.

## Tile KC/Luck sidebar contract — authorized 2026-09-16

For a published team's selected tile, the initial nested TeamBoard route, enhanced
Sidebar response use the same tile KC/Luck presentation. The existing standalone
Tile scaffold is not a registered route; keep it inactive and preserve the shared
DTO/partial consumer without adding routing.
Apply PRODUCT_REQUIREMENTS.md section 15.1's tile-specific rule: received drops are
credited to this tile only; distinct relevant boss/mode KC values remain separate.
Reuse the current EHB/Drop EHB summary and expandable contributor composition, with
Luck/name/KC contributor rows and labelled per-metric values where needed.

The query uses approved publication data, frozen event outcome rates, cached full-event
activity and playing-account attribution. Repeated item/requirement placement must not
duplicate KC or expectation. The tile projection retains the last compatible saved
result, including its timestamp and known KC, until a later accepted fetch replaces
it; failed, partial, or merely stale observations do not clear or rescore that result.
Preserve missing/unranked/estimated/incomplete and stale snapshot behavior. Tile
completion does not stop the activity interval. Reversal and corrected approvals do
not invalidate a compatible saved tile result before that next accepted fetch. Empty
objectives remain usable; unavailable data is explained without false numeric results.
No new external request, persistence table, route, permission or submission behavior is
introduced. Historical imported Sommerbingo remains outside asserted Stats calculations.

Tile contributor display refinement — user directed 2026-09-16: show only rows with
known KC > 0 for their displayed boss/mode. Hide empty contributor groups/disclosure;
do not filter the underlying team totals or Luck calculation, and retain team missing/
incomplete/stale status. Existing section order and accepted EHB styling are unchanged.

Stale tile-data correction — user directed 2026-09-16: stale activity retains the
last-known compatible KC and contributors, with the stale timestamp, until the next
accepted fetch. This also applies to legacy event checkpoints that lack tile
projections. Do not reconstruct or rescore a tile Luck value on read. If the retained
payload cannot provide a coherent tile result, mark tile Luck unavailable while
awaiting a coherent update, without clearing known KC. Preserve assignment/source/
lifecycle invalidation at the next accepted fetch, team and metric scoping, and
read-only page access. No fixture/provider refresh is a substitute for this
correction. No browser inspection; use focused executable functionality tests.

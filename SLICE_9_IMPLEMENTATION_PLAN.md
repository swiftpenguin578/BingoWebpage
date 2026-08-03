# Slice 9 Implementation Plan

## Approved outcome

Slice 9 completes authoritative event ending and recovery, live participant withdrawal/replacement, final review, official result history, archive access, and operational notifications. It extends the existing lifecycle, participant, team, evidence, finalization, and notification boundaries rather than creating a parallel workflow system.

The user approved this product behavior on 2026-08-02. The single independent read-only implementation-readiness review is complete; its accepted decisions and corrections are recorded below. All product decisions are resolved and the plan is ready for implementation.

## Approved product decisions

- An event that entered `AWAITING_FINAL_REVIEW` prematurely through either manual or automatic end may resume `LIVE` only before official finalization.
- Resume requires an enabled Admin, strong confirmation, a written reason, and a replacement future event-end time.
- The prior end transition, effective time, actor/reason when applicable, evidence, reviews, contributions, swaps, focus, and other intervening history remain immutable.
- The ordinary submission cutoff is re-derived from the replacement end. A separately reopened submission window is revalidated and is not silently carried into resumed play.
- Eligibility resumes prospectively at the successful transition and is never backdated across the final-review interval.
- Resume rechecks singleton-current, lifecycle, stale-update, and retry/idempotency protections. It is unavailable directly from `FINALIZED` or `ARCHIVED`.
- Emergency credentials are not silently re-enabled. An Admin may explicitly enable a valid credential again through the existing audited action.
- Live withdrawal preserves competitive history and creates a real vacancy. Replacement is optional, Admin-selected, prospective, and grants no inherited credit.
- Captain/co-captain assignment remains a separate explicit Admin action.
- Normal finalization is confirmed without a reason; exceptional blocker overrides and unfinalization require reasons. Archive is confirmed without rewriting official history.

## Completed independent readiness review

The review completed on 2026-08-02 confirms the following implementation contract:

- Each entry into `AWAITING_FINAL_REVIEW` is a distinct final-review cycle. Acknowledgements, corrections, overrides, and snapshots are cycle-scoped; prior cycles remain immutable and cannot authorize a later cycle.
- Only teams that actually completed require the reasonless **Completion time inspected** acknowledgement. With no completed teams, no such acknowledgement is required. Corrections remain reasoned; exceptional overrides remain confirmed and reasoned.
- Resuming `AWAITING_FINAL_REVIEW → LIVE` clears `ReopenedSubmissionCutoffAt`, recalculates the ordinary cutoff from the replacement event end, and requires a later explicit audited reopen for any special extension.
- Each waiting-list promotion creates one purpose-specific Admin follow-up action. It is resolved only by **Mark follow-up complete**, storing actor and time; reading a notification never resolves it. No generic workflow/task framework is added.
- Personal/informational notifications and unresolved Admin actions remain separate projections and counts. Admin actions cover pending evidence review, promotion follow-up, postponed start, vacancy, and missing-Captain work where applicable.
- Add `EventStateTransition.EffectiveAt` and `BingoEvent.SubmissionsClosedAt`; do not add a separate premature-end recovery table.
- Add uniqueness protecting `TeamMembership.ReplacesMembershipId`, so one vacancy cannot be filled twice.
- Live withdrawal immediately revokes website authority, closes evidence eligibility at the next full UTC minute, retains reservations/history/contributions, and does not perform pre-draft waiting-list promotion.
- A replacement inherits no progress, role, or character authority. Immutable drafted/publication history remains separate from the current roster.
- A former archived participant may read only their own rejected/withdrawn evidence history; they receive no team-private access and no mutation authority.
- Extend the existing lifecycle, signup-overlap, emergency, membership, role, notification, finalization, archive, and snapshot services. Do not rebuild them.
- Finalization blockers remain limited to competitive-integrity conditions. Vacancies and missing Captains are Admin actions, not automatic finalization blockers.
- Development support is minimal and reproducible: preserve and reseed the complete approved DKL events (TEST 13 and TEST 15, including event identity/schedule/lifecycle, board and requirements, rosters/teams, account authority, evidence/publication foundations, and intentional fixture history), plus one due/premature-end journey, one linked waiting replacement account, and one archived former-participant evidence journey. Exact credentials and routes are recorded for manual acceptance. Outdated or manually created disposable events are removed by reset.

The review confirms that the three passes are independently deployable in order. Pass 9.3 records the minimum additional cycle and snapshot metadata required by the approved immutable-history behavior; all other complexity budget and explicit non-goals remain unchanged.

## Core invariants

- Scheduled end uses the configured instant as the authoritative effective end even when processing runs late; early end uses the confirmed actual instant and preserves the schedule.
- No newly obtained drop is eligible after the authoritative effective end or during a final-review interval later followed by resume.
- Submission access and drop eligibility remain separate. The active upload cutoff never changes silently.
- A participant withdrawal immediately revokes website mutation authority while the approved next-whole-UTC-minute boundary governs final competitive eligibility.
- Withdrawal never deletes draft picks, memberships, assignments, evidence, contributions, roles, or audit history.
- A replacement has one prospective membership/activation boundary, intact event-character reservations, and no predecessor contribution.
- Finalization uses the authoritative reviewed state, creates immutable versioned result snapshots, and cannot coexist with unresolved required blockers.
- Unfinalization supersedes rather than deletes official snapshots and never silently reopens uploads.
- Archive preserves public URLs and historical participant access while removing operational mutation authority.
- Operational notifications and action items are idempotent and resolve from authoritative underlying state, not merely read/unread status.

## Pass 9.1 — authoritative end and premature-end recovery

- Expand/backfill the existing persistence for effective lifecycle time and submission-close authority, with fail-closed retained-data handling where required; do not add a recovery table.
- Reconcile scheduled and manual early end through the shared lifecycle service and worker, including worker catch-up at the authoritative scheduled instant.
- Preserve scheduled/effective/actual end distinctions and the existing 30-minute ordinary submission cutoff behavior.
- Add the confirmed, reasoned `AWAITING_FINAL_REVIEW → LIVE` recovery action with a replacement future end, clearing `ReopenedSubmissionCutoffAt` and requiring a later explicit audited reopen for a special extension.
- Preserve prior lifecycle and intervening competitive history; restore only prospective eligibility.
- Re-derive the ordinary cutoff, recheck overlap, singleton-current, lifecycle, stale-update, concurrency, and retry/idempotency rules, and prevent duplicate transitions, audits, or notifications.
- Keep Finalized/Archived recovery outside this action and retain explicit emergency-credential enablement through the existing audited interaction.
- Add the smallest Development scenario and manual journey needed to exercise scheduled end, early end, resume, stale/conflicting resume, and the second authoritative end.

Pass 9.1 should be independently deployable without changing replacement or finalization behavior.

## Pass 9.2 — live withdrawal and replacement

- Complete Admin live withdrawal through the existing participant/team lifecycle boundary.
- Revoke mutation authority on commit, preserve all history, record the approved eligibility end, end active membership/role, and create a durable vacancy.
- Notify enabled Admins and remaining linked Captain/Co-captains once with private-safe context.
- Allow an Admin to leave the vacancy open, select an available waiting-list participant, or create a validated internal participant when necessary.
- Revalidate capacity, waiting status, frozen event assignments, uniqueness, team vacancy, and concurrency inside one transaction.
- Promote/create the replacement prospectively with unique `Replacement` membership linkage and active account/character history; inherit no progress, evidence, contribution, role, or draft pick.
- Notify the linked replacement and current linked team leadership once; create one purpose-specific Admin promotion follow-up action. Keep Discord availability contact manual.
- Keep Captain/co-captain assignment separate, preserve public/private projection boundaries, and adapt public roster/stat projections to the current roster without rewriting drafted/publication history.

Pass 9.2 should be independently deployable after Pass 9.1 and must not rewrite the draft ledger.

## Pass 9.3 — final review, official history, archive, and operational handoff

The restricted final-review stale/concurrency correction was completed on 2026-08-02 without a model change or new abstraction. Acknowledgement, correction, and exceptional-override forms require both the current event version and current `AwaitingFinalReview` cycle identity; the locked service rejects missing, stale, prior-cycle, and conflicting writes safely, advances the aggregate version once per actual mutation, permits only identical persisted retries idempotently, and maps serialization/unique/concurrency conflicts to localized stale feedback without audit or mutation residue. The compact PostgreSQL scenario passed `4/4`; Release build, formatting, and `git diff --check` passed. Manual acceptance, independent review, and final Slice 9 gates remain pending.

- Reconcile each cycle-scoped final-review checklist with authoritative pending evidence, completion checks, competitive-integrity blockers, and direct resolution destinations.
- Require **Completion time inspected** only for teams that actually completed; preserve reasoned corrections and confirmed/reasoned exceptional overrides without changing underlying records.
- Keep normal finalization an explicit confirmed Admin transaction that locks/rechecks state, recalculates progress/rankings, snapshots official placements/statistics, and records actor/time atomically.
- Preserve immutable, cycle-scoped versioned official history through reasoned unfinalization and re-finalization; never delete or silently reopen uploads.
- Keep confirmed `FINALIZED → ARCHIVED` history read-only on existing public routes and participant-owned historical projections.
- Recheck singleton-current boundaries when unfinalizing Finalized/Archived results, and allow an archived former participant only their own rejected/withdrawn evidence history.
- Reconcile separate personal notification and Admin action projections/inboxes so read state never resolves authoritative operational work; provide explicit resolution for pending evidence review, promotion follow-up, postponed start, vacancy, and missing-Captain actions where applicable.
- Finalize the minimal Development fixtures, exact credentials/routes, source-of-truth wording, and one compact consolidated Slice 9 manual checklist.

Pass 9.3 is independently deployable after Passes 9.1 and 9.2 and completes Slice 9.

## Manual-test reachability

Development reset must provide only the minimum reproducible support for: the complete approved TEST 13 and TEST 15 DKL events, one due/premature-end journey, one linked waiting-list replacement account, and one archived former-participant evidence journey, plus the accounts and states needed to exercise final review, explicit Admin actions, finalization, unfinalization/re-finalization, and archive history. The DKL event inventory includes identity, schedule/lifecycle state, board/requirements, rosters/teams, account authority, evidence/publication foundations, and intentional seeded history. Record exact credentials and routes in the manual checklist. Reuse existing accounts/events where practical; do not add broad demonstration fixtures or preserve obsolete disposable events.

## Complexity budget

- Persistence: reuse event transition/audit/finalization, membership, activation, vacancy/action, and notification data; add only `EventStateTransition.EffectiveAt`, `BingoEvent.SubmissionsClosedAt`, the protected unique `TeamMembership.ReplacesMembershipId` linkage, the purpose-specific `waiting_list_promotion_follow_ups` table for explicit promotion follow-up completion, cycle identity/kind/team metadata for retained final-review resolutions/corrections, versioned finalization snapshot cycle/input/result/consumed-resolution metadata, and the minimum backfill/preflight needed to make retained data authoritative. No separate premature-end recovery table, workflow/task framework, or official-results subsystem.
- Services: extend existing lifecycle, participant/team, finalization, and notification services. No generalized workflow, replacement, lifecycle, or notification framework.
- Routes/UI: reuse Manage, Participant/roster management, Finalize, public history, and notification surfaces. No parallel lifecycle console or UI redesign.
- Jobs: extend the existing lifecycle worker only where scheduled end/cutoff handling requires it. No new background-job family.

## Minimal proportional verification

- Focused lifecycle scenarios for scheduled/late/early end, resume, second end, singleton conflict, stale request, retry, and no residue.
- Focused PostgreSQL withdrawal/replacement scenarios for authority revocation, eligibility boundaries, waiting/internal replacement, uniqueness/conflict rollback, history, and idempotent recipients.
- Focused finalization/unfinalization/archive scenarios for blockers, override history, snapshot versions, public/private projections, and concurrency.
- One worker catch-up scenario covering authoritative scheduled instants and duplicate suppression.
- One authenticated route/markup scenario for each materially changed Admin journey and its direct resolution destination.
- Clean/retained migration rehearsal only if persistence changes; retained final-review snapshots must map deterministically to exactly one cycle, while zero/multiple candidates fail closed with exact event/snapshot IDs and no partial upgrade.
- Focused per-pass build/format/diff/EF checks, then the complete suite and consolidated manual acceptance only at the final Slice 9 gate.

## Explicit non-goals

- Direct resume from `FINALIZED` or `ARCHIVED`.
- Erasing or rewriting prior end/finalization history.
- Retroactive eligibility across a final-review interval.
- Automatic replacement selection or automatic Captain assignment.
- Inherited replacement evidence, contribution, membership history, or draft pick.
- Discord automation.
- New workflow/action/notification frameworks.
- A separate premature-end recovery table or generic Admin task framework.
- UI-overhaul work, the post-Slice-10 Application Atlas, or Slice 10 Wise Old Man integration.

## Change control and readiness

This plan is the authoritative Slice 9 review baseline. Any material user-approved change during implementation or manual remediation must be recorded here before implementation. The post-implementation independent review compares the exact base-to-current diff with the final updated plan and blocks missing approved scope, changed non-goals, unapproved material behavior, or unbudgeted complexity.

Current readiness: product behavior, independent review, and consolidated Slice 9 manual acceptance are complete. S9-01 through S9-07 passed; S9-05 preserved placements, metrics, and Version 1 history after unchanged TEST 84 re-finalization, and S9-06 accepted the owner-rendered archived evidence link, raw-image display, and unrelated/anonymous privacy boundaries. Viewer presentation is deferred to the UI overhaul. Final automated verification passed with Domain `162/162`, Application `83/83`, Browser `67/67`, Integration `242/242`, combined `554/554`, zero failed/skipped, and durable results at `/private/tmp/slice9-final-automated-gates-20260803`. The Release solution build, formatting, EF pending-model, `git diff --check`, clean/retained migration, Development double-reset, parity, migration-pair/snapshot, artifact/secret, and staged-state gates also passed. Slice 9 is accepted and ready for packaging; Slice 10 was not started.

## Pass 9.1 implementation handoff

Pass 9.1 was implemented on 2026-08-02 and is limited to authoritative event ending and reasoned premature-end recovery. `20260802000213_AddAuthoritativeEventEndPersistence` adds `EventStateTransition.EffectiveAt` and `BingoEvent.SubmissionsClosedAt`; retained backfill copies each transition's `PerformedAt` into `EffectiveAt` and derives ended-event closure from the normal or later reopened cutoff. No recovery table, replacement workflow, finalization behavior, Admin action inbox, or generalized lifecycle framework was added.

The shared lifecycle service now records scheduled effective time separately from late processing time, durably catches up submission closure once, and serializes manual/scheduled end and resume with the existing event-version and lifecycle-singleton boundaries. The Admin Manage recovery control requires confirmation, a written reason, and a future replacement end; it rejects stale, repeated, unsupported-state, official-history, lifecycle-singleton, and event/signup-window overlap requests without residue, preserves prior transitions/audit history, clears current actual end/closure/reopened cutoff, and re-derives the ordinary cutoff. Existing explicit emergency-credential re-enable remains separate and audited; resume does not re-enable credentials.

Verification run for this pass: Domain `EventLifecycleFoundationTests` `94/94`; Application `83/83`; Browser `EventCreationUiTests` `2/2`; PostgreSQL `Slice3ScheduledLifecycleIntegrationTests` `6/6`, `Slice3ScheduleLifecycleIntegrationTests` `2/2`, and `Slice3LifecyclePersistenceIntegrationTests` `2/2`; Release solution build `0` warnings/`0` errors; EF pending-model check clean; formatting verification clean; `git diff --check` clean. Manual acceptance and independent review remain unrun; the complete solution suite remains unrun. Pass 9.2 and Pass 9.3 have not started. Nothing is staged or committed.

## Pass 9.2 implementation handoff

Pass 9.2 was implemented on 2026-08-02 and is limited to live Admin withdrawal and replacement. Withdrawal uses the existing serializable participant/team lifecycle boundary, immediately revokes website mutation authority, stores eligibility through the next full UTC minute, ends active membership and Captain/Co-captain role history, preserves reservations, draft picks, publication history, evidence, contributions, and audit history, and leaves the derived vacancy open without pre-draft waiting-list promotion. Enabled Admins and remaining linked team leadership receive one deterministic private-safe notification. The existing `TeamMembership.ReplacesMembershipId` linkage now has a filtered unique database index.

Waiting-list and validated internal replacement use the existing signup, membership, character activation, audit, notification, and transaction boundaries. They revalidate Live state, Admin authorization, vacancy/version, waiting status, frozen assignments/reservations, participant ownership/uniqueness, active Playing authority, and concurrency. Each replacement gets one prospective `Replacement` membership linked to the ended membership and one initial activation at the next full UTC minute, with no inherited progress, evidence, contribution, role, character authority, membership history, or draft pick. Waiting-list promotion creates one purpose-specific `WaitingListPromotionFollowUp`; only explicit Mark follow-up complete stores actor/time, while notification read state does not resolve it. Current roster projections use active memberships while immutable draft/publication projections remain unchanged. Captain/co-captain assignment remains separate, Discord contact remains manual, and no Pass 9.3 inbox or generic workflow framework was added.

Migration `20260802002639_AddLiveWithdrawalReplacementPersistence` adds the follow-up record and protected replacement linkage. Verification passed: Domain `Slice9LiveWithdrawalDomainTests` `2/2`; PostgreSQL `Slice9Pass92LiveWithdrawalIntegrationTests` `2/2`; retained/clean migration plus lifecycle regression `Slice1MigrationRehearsalTests` and `Slice4ParticipantLifecycleIntegrationTests` combined `8/8`; Browser markup/route boundary `EventCreationUiTests` `2/2`; Release solution build `0` warnings/`0` errors; EF pending-model check clean; formatting verification clean; `git diff --check` clean. The first focused integration attempt found and corrected notification recipient timing and migration key-column mapping; the corrected reruns passed. Manual acceptance, independent review, and the complete solution suite were not run. Pass 9.3 was not started. Nothing is staged or committed.

## Pass 9.3 implementation handoff

Pass 9.3 was implemented on 2026-08-02. Final-review cycles use the authoritative `AwaitingFinalReview` transition identity. Completion-time acknowledgements are reasonless and required only for completed teams; corrections are reasoned; exceptional overrides are confirmed and reasoned. Resolutions/corrections are unique and cycle-scoped, and prior-cycle data cannot authorize a later cycle. Finalization locks and rechecks state/version/readiness, recalculates authoritative progress/rankings, stores consumed resolution IDs plus calculation inputs/results, appends immutable official placements/statistics, audits, and emits deterministic result notifications in one transaction. Unfinalization and archive preserve all prior snapshots/resolutions, recheck singleton-current boundaries, and never reopen uploads.

Migration `20260802005536_AddFinalReviewCyclesAndSnapshotInputs` adds the minimum retained cycle/snapshot metadata and fails closed with affected IDs and correction guidance when old final-review state is ambiguous. Archived former participants receive only own rejected/withdrawn evidence history through My Events navigation. The existing shell and notification surfaces keep personal read state separate from authoritative Admin actions for pending review, waiting-list follow-up, postponed start, vacancies, and missing Captains. Development reset remains idempotent and uses only the approved minimal fixture set plus `SeedReplacement` / `SeedReplacement!1234`.

Focused verification: Domain `EventLifecycleFoundationTests` `96/96`; PostgreSQL `Slice3FinalizationAtomicityIntegrationTests` `1/1`; Browser `EventCreationUiTests` `2/2`; the real-PostgreSQL Development double-reset regression `1/1`; and `Slice1MigrationRehearsalTests` `6/6`, including clean, deterministic retained, and fail-closed ambiguous-cycle rehearsals. The user externally completed two post-correction resets through the Release `--no-build` path with exit code `0` for both, and the Release Web build succeeded. Direct inventory confirms the exact four approved events and complete DKL event foundations: TEST 13 and TEST 15 identity/schedule/lifecycle, boards/requirements/drop snapshots, TEST 15 teams/rosters and linked waiting replacement, scoped enabled/disabled authority, evidence/publication foundations, and TEST 84 cycle-linked finalization history. Bootstrap and named Slice 9 fixture accounts are present, the final migration marker is applied, and no unrelated event remains. Independent review, consolidated manual acceptance, and final automated verification are complete; packaging, staging, commit, merge, and push are the remaining authorized handoff actions. Slice 10 was not started.

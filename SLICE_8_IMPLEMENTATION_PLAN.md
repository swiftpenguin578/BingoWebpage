# Slice 8 Implementation Plan

## Approved outcome

Slice 8 completes evidence creation, participant and Captain workflows, linked resubmission, two-decision Admin review, immutable credited-character attribution, public approved evidence, reversal/rebalancing, and removal of deprecated privacy/request-changes/duplicate behavior. It preserves existing protected TeamBoard/public-board interactions and does not add the separately selected permanent Rules/how-to work.

The user approved the product behavior and the 2026-07-31 independent read-only readiness corrections. Implementation may begin with Pass 8.1.

## Change control and final review baseline

The final independent review will compare the exact accepted-base-to-current diff with this updated plan. Implementation remains limited to the approved Pass 8.2 scope and the existing Pass 8.1 foundation. Any material product, scope, authority, persistence, route, workflow, or complexity change requires user direction and a plan update before implementation; ordinary technical clarifications do not.

## Approved product decisions

- Remove Admin creation/upload of participant or Captain evidence. Admins retain Approve, Reject, reasoned metadata correction, reversal, and complete audit access.
- Convert retained `ChangesRequested` submissions to `Rejected`, preserving their notes, timestamps, reviews, and audit history. Recovery uses the new linked-resubmission workflow.
- Preserve retained duplicate classification as historical audit metadata only. Duplicate or unusable new evidence is rejected with a reason; there is no active duplicate workflow.
- Fail closed on approved evidence hidden by legacy visibility state. Preflight reports affected identifiers for operator resolution; migration must not unexpectedly publish or automatically reverse it.
- Pending legacy privacy flags may be discarded when deprecated privacy behavior is removed.

## Lean readiness and scope contract

### Manual-test reachability

Development reset must provide the minimum reusable state needed to exercise: linked participant self-submission, Captain/Co-captain teammate submission, enabled/disabled emergency fallback, rejection and linked resubmission, approval and public evidence, reversal/rebalancing, cutoff read-only behavior, and Finalized/Archived history. Prefer adapting the existing Slice 7 event/accounts rather than creating many new events or accounts. Every manual journey must name its starting account, event state, route, and expected result before final handoff.

### Removal completeness

Pass 8.3 owns removal of deprecated evidence behavior across enum/domain state, EF persistence/migration, service commands and projections, Admin/Captain/participant routes and controls, shell notifications, finalization blockers, Development seeds, tests, translations, and source-of-truth wording. Historical values explicitly preserved as audit metadata are not active behavior.

### Complexity budget

- Persistence: extend the existing Submission/evidence model with credited-character, version, and predecessor fields plus required FKs/indexes; do not add a new workflow aggregate or table without a concrete retained-data/integrity requirement.
- Services/policies: extend existing evidence/review services and add at most one shared evidence-authority/active-character boundary; do not add generalized workflow, policy, or deduplication frameworks.
- Routes/UI: reuse TeamBoard/Tile/Evidence/Admin Review surfaces and the required standalone fallback; do not create a parallel evidence application or redesign protected interactions.
- Jobs: no new background job is expected. Existing notification and realtime invalidation mechanisms are sufficient.

### Fail-closed operator recovery

Preflight and migration diagnostics must identify every ambiguous/orphaned attribution and approved-hidden record by stable identifier. Before deployment retry, the operator resolves ambiguous retained attribution through a documented bounded data correction and handles approved-hidden evidence through the existing reasoned reversal/remediation path. The implementation must document the exact diagnosis and retry procedure; it must never guess, publish, or reverse automatically.

### Scope freeze and stop rule

Only the three passes, approved decisions, invariants, compatibility work, and verification below belong to Slice 8. Optional simplifications remain optional. Adjacent defects or improvements are recorded separately unless they prevent safe implementation or verification. Implementation stops for user direction before adding behavior, changing an approved product rule, broadening a pass, introducing an unbudgeted abstraction/table/job, or redesigning an existing protected surface.

## Core invariants

- Initial submission snapshots the server-resolved active `PLAYING` character at immutable server submission time.
- Evidence ownership follows the credited participant; `SubmittedByAccountId` records the actor for audit and does not define ownership.
- Linked resubmission creates a new submission and new asset, copies the predecessor's credited participant/character snapshots, and permits at most one direct child per predecessor.
- Pending edits cannot change credited attribution, server submission time, snapshot weight, calculated contribution, or active asset identity.
- Participant/Captain/emergency create, edit, withdraw, and resubmit authority is current, server-derived, team-scoped, and cutoff-bound.
- Only `Pending` submissions block finalization. Participant-facing evidence becomes read-only after cutoff; Admin review continues.
- A Pending submission can become Approved, Rejected, or Withdrawn. An Approved submission can become Reversed. Required reasons and history remain authoritative.
- Admin credited-account correction accepts the event character, derives its participant inside the locked transaction, and cannot independently select a contradictory participant.
- Public projections expose only active Approved evidence and its stored credited-character snapshot. Private projections enforce credited-owner, current team-leadership/emergency, or Admin scope.
- Rejection notifications commit once with rejection for the linked credited participant and current linked Captain/Co-captains; unlinked participants leave only the leadership recipients.
- Finalized and Archived evidence remains readable through its permitted projections but immutable.

## Pass 8.1 — persistence, migration, authority, and compatibility foundation

Implement the complete deployable foundation together:

- Preflight submissions, assets, contributions, reviews, legacy statuses/actions, hidden approved evidence, orphan relationships, and ambiguous credited-character attribution.
- Add immutable credited OSRS-character identity, optimistic response version, nullable resubmission predecessor, and the required foreign keys.
- Backfill each retained submission from the latest `PLAYING` activation at or before `SubmittedAt`; when no swap history exists, use only a unique valid legacy participant/primary assignment. Abort with affected identifiers when attribution is missing or ambiguous.
- Add database enforcement for one active asset per submission and one direct resubmission child per predecessor after retained-data preflight.
- Add one shared server-derived evidence authority/active-character boundary for participant self, current Captain/Co-captain, enabled scoped emergency access, and Admin review/correction authority. Do not grant emergency-style claims to ordinary website users.
- Carry expected version through every existing mutation command and form when concurrency becomes active; stale mutations fail safely. Lock the predecessor during resubmission.
- Move every Admin, Captain, participant, shell, public-board, and evidence identity projection to the stored credited-character snapshot in this pass. Do not leave current-primary-character projection until Pass 8.3.
- Retain deprecated columns/handlers temporarily so Pass 8.1 can deploy without removing user workflows before their replacements exist.

Pass 8.1 is independently deployable only with all items above. It does not add the participant/Captain workflow UI or remove deprecated behavior.

### Pass 8.1 implementation result — 2026-07-31

Pass 8.1 is implemented and independently deployable. The migration/backfill behavior is exact and fail-closed: it preflights orphaned assets, contributions, and reviews; duplicate active assets; invalid participant/event relationships; missing or ambiguous credited attribution; missing character rows; and approved-hidden evidence. It reports stable affected IDs and aborts before constraints or visibility changes. Each retained submission receives the latest `PLAYING` activation at or before `SubmittedAt`, ordered by effective time, recorded time, and ID; without swap history it receives only a unique valid active `PLAYING` assignment. The stored character ID and display name are then made required, version starts at 1, and the participant, character, predecessor, asset, review, and contribution relationships are enforced. One active asset per submission and one direct resubmission child per predecessor are database-enforced.

The implementation extends the existing Submission/evidence entities, `SubmissionService`, review transactions, `ApplicationDbContext`, evidence configurations/migration snapshot, Admin/Captain PageModels/forms, shared shell, public board evidence projections, development seed data, and focused compatibility fixtures. It adds the single `IEvidenceAuthority`/`EvidenceAuthority` boundary for participant self, current Captain/Co-captain, enabled scoped emergency, and retained Admin compatibility. Stored credited-character snapshots are consumed by all existing evidence identity displays; current-primary queries remain only for current candidate/roster management. Expected versions are carried through existing mutation commands and forms and stale writes fail safely. Existing routes, audit/review/contribution/notification/finalization behavior, and deprecated Admin creation/upload compatibility remain in place for Pass 8.3.

Verification results: PostgreSQL retained migration/backfill/preflight/cardinality tests `2/2`; PostgreSQL Slice 7 swap/approval/stale-version boundary `14/14`; Release solution build clean with zero warnings/errors; EF pending-model check reports no changes; formatting verification passes; `git diff --check` passes. The complete solution suite, independent review, and manual acceptance were not run. Pass 8.2 and Pass 8.3 were explicitly not started. No files are staged, committed, merged, or pushed.

## Pass 8.2 — participant, Captain, emergency, and private-history workflows

- Add participant self-create/edit/withdraw and linked resubmission.
- Add current Captain/Co-captain team-wide submission authority and enabled emergency fallback without inferring authority from names or unrelated account data.
- Use an evidence-specific current-team candidate query; do not reuse a projection that omits linked teammates.
- Derive the active credited character on initial submission without exposing an account selector.
- Require a new image for linked resubmission; copy credited participant/character read-only while allowing ordinary structured tile/requirement/drop corrections.
- Integrate evidence actions and results into the existing TeamBoard drawer and required standalone route without redesigning the protected interaction model.
- Provide credited-owner and authorized team-leadership private history, accurate cutoff behavior, realtime invalidation, localized feedback, and the existing required route fallback.
- Hide deprecated controls once their replacement journeys exist, but retain their data until Pass 8.3 migration/cleanup.

Pass 8.2 is independently deployable after Pass 8.1. Do not convert `ChangesRequested` data before linked resubmission exists.

### Pass 8.2 implementation result — 2026-08-01

Pass 8.2 is implemented only on the Pass 8.1 foundation. The existing evidence authority now resolves website participants from persisted current event/team membership, supports participant self and current Captain/Co-captain teammate scope, requires enabled exact-scoped emergency access, and supplies an evidence-specific current-team candidate query. Participant create/edit/withdraw/resubmit routes expose no account selector; initial credited character identity is resolved from the server-authoritative active PLAYING character. Private history is credited-owner scoped for participants and current-team scoped for leadership/emergency. Public board projections remain unchanged.

Pending edits validate structured tile/requirement/drop/note changes while preserving credited participant/character snapshots, submitted time, snapshot weight/contribution, and active asset identity. Withdrawal is cutoff-bound, versioned, audited, and history-preserving. Linked resubmission is available only from `Rejected`, never `Approved`: the predecessor is row-locked, its participant/character snapshots are copied, a new submission and new image are created, and the existing unique predecessor index limits one direct child. Replay, wrong-team, disabled/expired emergency, cross-participant, and stale-version attempts fail closed. Normal website/Captain mutations use the inclusive active cutoff; emergency mutations retain the stricter emergency cutoff. Pass 8.3 removes the retained `ChangesRequested` compatibility state and privacy/visibility request fields.

The existing TeamBoard/Tile drawer and standalone `/Captain/Submit/{tileId}` fallback were extended with server-verified event/team context, participant/leadership result feedback, and localized English/Danish strings without changing the protected overlay/sidebar hierarchy or public projections. Focused PostgreSQL SubmissionWorkflow coverage passed `16/16`; Domain EvidenceRules passed `6/6`; Release build passed with zero warnings/errors. EF pending-model check was intentionally not rerun because Pass 8.2 added no model changes. Formatting verification and `git diff --check` both pass. No complete suite, consolidated manual acceptance, independent review, packaging, staging, commit, merge, or push was run. Pass 8.3 was not started.

## Pass 8.3 — Admin review, public evidence, retained-state conversion, and cleanup

- Apply the approved retained `ChangesRequested` and duplicate-history conversions and fail closed on unresolved approved-hidden evidence.
- Restrict Pending review to Approve or reasoned Reject.
- Add reasoned metadata correction for tile/requirement, qualifying drop, or credited playing character; derive participant transactionally and preserve immutable time, weight, contribution, and image.
- Create rejection notifications transactionally and once for the approved recipient set.
- Preserve serializable approval/reversal, exact contribution deduction, capacity reallocation, progress/placement recalculation, and structured history.
- Remove Admin evidence upload, Request Changes, same-record resubmission, active duplicate classification, privacy requests, visibility toggles, hidden-player placeholders, obsolete actions, and all corresponding domain/service/UI/shell/finalization/projection/seed/test consumers.
- Finalize public and Archived Approved-only evidence projections, participant/team/Admin private history, finalization behavior, seeds, translations, focused tests, and manual acceptance documentation.

Pass 8.3 is independently deployable only after retained-data preflights are clean and its destructive schema cleanup ships with the corresponding application cleanup.

### Pass 8.3 implementation result — 2026-08-01

The forward migration `20260801160152_RemoveDeprecatedEvidenceCompatibility` first rejects unsupported submission statuses, orphaned evidence assets/contributions/review actions, and approved submissions with legacy hidden visibility, reporting stable affected IDs. It then converts only `ChangesRequested` to `Rejected`; reviewer note, review timestamp, review/audit history, assets, credited snapshots, and contribution state are not rewritten. The four deprecated submission columns are dropped only after that preflight. No approved-hidden record is published, reversed, or guessed; pending privacy flags are discarded by the column removal. Historical duplicate/request-changes/visibility action enum values and historical snapshots remain readable metadata, but no active service/page/route/seed consumer can issue them.

Admin review is now Approve or reasoned Reject for Pending only. Approval remains reasonless and serializable. Reject records the review atomically and creates deterministic, retry-safe existing `PersonalNotification` rows for the linked credited participant plus current linked Captain/Co-captains, or only eligible current leadership when the credited participant is unlinked. Pending metadata correction requires a reason and expected version, accepts only tile/requirement/drop/credited Playing character targets, derives the participant from the selected event character inside the transaction, and preserves server submission time, snapshot weight, calculated contribution, and active evidence asset. Approved reversal, contribution deduction/rebalancing, progress, placement, audit, and finalization behavior remain on the existing transaction boundaries; only Pending submissions block finalization.

Public and Archived projections expose only active Approved evidence and stored credited-character snapshots. Participant, current-team leadership/emergency, and Admin private histories retain their approved scope. Deprecated controls/routes/handlers/flags/placeholders and related seed/test/translation consumers were removed without disturbing the protected TeamBoard drawer, standalone fallback, or linked Rejected resubmission. No new table, job, policy, route family, or generalized abstraction was added; the approved `IEvidenceAuthority`/`EvidenceAuthority` remains the only evidence-specific boundary, and existing PersonalNotification persistence handles rejection delivery.

Focused results: Domain `5/5`, Application `6/6`, PostgreSQL/VSTest `34/34` (SubmissionWorkflow, Slice 7 boundary compatibility, and Slice 8 retained migration/conversion/preflight), Release solution build `0` warnings/errors, EF pending-model check clean, formatting clean, and `git diff --check` clean. The complete suite, consolidated manual acceptance, and independent review remain intentionally unrun; no material implementation risk remains from the direct focused checks, so this pass stops at its requested handoff.

### Slice 8 scope traceability and complexity handoff

- Pass 8.1: deterministic retained attribution/preflight, immutable credited snapshots, versions, foreign keys/cardinality, evidence authority, and stored-snapshot projections delivered and preserved.
- Pass 8.2: participant/Captain/Co-captain/emergency create/edit/withdraw, private history, rejected-only linked resubmission, cutoff/version/asset protections, drawer and standalone fallback delivered and preserved.
- Pass 8.3: retained conversion/fail-closed cleanup, Approve/Reject-only Admin review, rejection recipient idempotency, reasoned derived correction, Approved-only public/Archived evidence, Pending-only finalization blocker, and deprecated active cleanup delivered.
- Missing approved scope: none identified. Material additions: none; rejection idempotency uses the existing notification entity and the migration uses bounded SQL preflight explicitly mapped to Pass 8.3 cleanup.
- Explicit non-goals preserved: no permanent Rules/how-to work, no new privacy/visibility controls, no Request Changes/same-record resubmission/active duplicate state, no generalized workflow/policy/resubmission/notification framework, no TeamBoard/public-board/editor/live-draft redesign, and no Slice 9/10 behavior.
- Actual complexity versus approved budget: `0` new tables, `0` background jobs, `0` new policy systems, `0` new route families, `1` small evidence authority boundary (`IEvidenceAuthority`/`EvidenceAuthority`, already introduced in 8.1), existing SubmissionService/transactions extended, and `2` plan-mapped forward migrations with designer/model snapshots. No unapproved material item remains.

## Minimal proportional verification

- One focused domain matrix for allowed transitions, immutable fields, and required reasons.
- PostgreSQL migration rehearsal for clean data, unique legacy fallback, swaps around submission time, retained statuses/duplicate history, orphan/ambiguous rejection, and expected approved-hidden failure.
- A parameterized authority/cutoff scenario spanning participant, Captain/Co-captain, enabled/disabled emergency access, wrong team, withdrawn participant, and Admin review/correction.
- Snapshot before/at/after swap plus edit/resubmission preservation and Admin correction derivation.
- Focused concurrency for edit/withdraw, approve/reject/correct, double resubmission, and competing active assets.
- Transactional rejection-recipient, retry/idempotency, and rollback proof.
- Focused approval/reversal/cap/rebalancing proof.
- HTTP/UI coverage for owner/team/Admin privacy, cross-team denial, public Approved-only evidence, Archived history, cutoff, drawer/standalone route, and absence of deprecated controls.
- Focused per-pass build/format/diff/EF/migration checks, then the complete solution suite and consolidated manual acceptance only at the final Slice 8 gate.

## Explicit non-goals

- Permanent Rules/how-to content.
- New privacy or visibility controls.
- Request Changes or a special duplicate state.
- Same-record resubmission.
- Per-character leaderboard redesign.
- OCR, plugin timestamp extraction, or drop-time inference.
- General workflow, policy, resubmission, or notification-dedup frameworks.
- Redesign of TeamBoard, public board, board editor, or live draft interactions.
- Slice 9 event-end/replacement/finalization expansion or Slice 10 Wise Old Man behavior.

## Readiness and handoff

Overall implementation readiness is **ready** after the approved decisions and corrections above. No unresolved Slice 8 product decision is known. Each implementation task must remain inside one named pass, use existing services/entities/transactions where possible, apply the smallest proportional tests, and must not begin the following pass unless explicitly assigned.

Before Slices 9 and 10 begin, repeat the same sequence: approve product behavior, run one independent read-only implementation-readiness review against current code and source-of-truth documents, resolve concrete decisions, update that slice plan, then implement. Do not repeat planning review without a newly discovered genuine contradiction or missing product rule.

## Slice 8 independent-review remediation — ten findings

The remediation is limited to the ten named findings and does not change the approved Slice 8 scope or complexity budget. The shared evidence authority now gates private asset reads and the submit route is authentication-gated with server-derived authority, so normal website-owned Captains/Co-captains remain reachable while Admin creation is denied in both route loading and `SubmissionService`. Create/resubmit cleanup now occurs only before the database commit and uses a non-cancelled cleanup token; committed assets survive notifier cancellation/failure. Admin correction selectors and service queries accept only current, same-event/team Playing assignments. Admin Review Details loads the authoritative event end and conditionally shows minutes after end plus the UTC latest clan event time. Rejection notifications retain deterministic recipient idempotency while persisting private-safe event/tile/drop/reason context and rendering localized English/Danish title/detail text instead of the semantic key.

The 8.1 retained preflight now rejects future-only swap history, latest swap targets that are not a valid same-event/participant Playing assignment at submission time, unsupported submission statuses, and unsupported review actions with stable IDs. The 8.3 preflight repeats the unsupported review-action check before cleanup. Development reset adds only the reusable normal linked website Captain, one disabled/expired scoped emergency credential, and one Pending plus one Rejected TEST 15 review record; the manual checklist names exact credentials, routes, ordering, and expected public/private results. Stale roadmap/workflow wording now describes scoped private non-Approved evidence and Approved-only public projections.

### Executable fail-closed recovery runbook

Run these commands against a disposable restored copy, replacing `<...>` locally and keeping secrets outside the repository. `SLICE8_EF_CONNECTION` is an Npgsql/EF semicolon connection string and is used only by `dotnet ef`; `SLICE8_PG_CONNINFO` is a libpq PostgreSQL URI and is used only by `pg_dump`/`psql`. Do not pass the EF string to libpq tools. Preserve the migration log, backup, and stable-ID output with the change record; never correct production rows by guessing.

```bash
export SLICE8_EF_CONNECTION='Host=<host>;Port=<port>;Database=<copied_database>;Username=<operator>;Password=<secret-from-local-secret-store>'
export SLICE8_PG_CONNINFO='postgresql://<operator>:<url-encoded-password>@<host>:<port>/<copied_database>?sslmode=<require-or-disable>'
export SLICE8_BACKUP="slice8-before-preflight-$(date -u +%Y%m%dT%H%M%SZ).dump"
pg_dump --format=custom --file="$SLICE8_BACKUP" "$SLICE8_PG_CONNINFO"
psql "$SLICE8_PG_CONNINFO" -v ON_ERROR_STOP=1 -c "SELECT \"MigrationId\" FROM \"__EFMigrationsHistory\" ORDER BY \"MigrationId\";"
```

Use the migration-history result to choose exactly one branch. A failed EF migration is transactional; if its migration ID is absent, use the pre-8.1 branch even if the failed log mentioned transient columns.

#### Branch A — pre-8.1 or failed transactional 8.1

Run this branch only when `20260731204600_AddSlice8EvidenceFoundation` is absent. It references only legacy tables/columns. Each query emits stable IDs; after correction, rerun that exact query and require zero rows before retrying.

```bash
dotnet ef database update 20260731204600_AddSlice8EvidenceFoundation \
  --project src/Bingo.Infrastructure --startup-project src/Bingo.Web \
  --connection "$SLICE8_EF_CONNECTION" 2>&1 | tee slice8-1-preflight-attempt.log
```

```bash
# Orphan assets, contributions, and reviews.
psql "$SLICE8_PG_CONNINFO" -v ON_ERROR_STOP=1 -c "SELECT a.id, a.submission_id FROM evidence_assets a LEFT JOIN submissions s ON s.id=a.submission_id WHERE s.id IS NULL ORDER BY a.id;"
psql "$SLICE8_PG_CONNINFO" -v ON_ERROR_STOP=1 -c "SELECT c.id, c.submission_id FROM submission_contributions c LEFT JOIN submissions s ON s.id=c.submission_id WHERE s.id IS NULL ORDER BY c.id;"
psql "$SLICE8_PG_CONNINFO" -v ON_ERROR_STOP=1 -c "SELECT r.id, r.submission_id FROM review_actions r LEFT JOIN submissions s ON s.id=r.submission_id WHERE s.id IS NULL ORDER BY r.id;"

# Duplicate active assets.
psql "$SLICE8_PG_CONNINFO" -v ON_ERROR_STOP=1 -c "SELECT submission_id, array_agg(id ORDER BY id) AS active_asset_ids FROM evidence_assets WHERE active GROUP BY submission_id HAVING count(*)>1 ORDER BY submission_id;"

# Invalid participant/event relationships.
psql "$SLICE8_PG_CONNINFO" -v ON_ERROR_STOP=1 -c "SELECT s.id, s.event_id, s.credited_participant_id FROM submissions s LEFT JOIN event_participants p ON p.id=s.credited_participant_id WHERE p.id IS NULL OR p.event_id<>s.event_id ORDER BY s.id;"

# Missing character rows in legacy assignments; the transient 8.1 missing-character ID is also preserved in its exception log.
psql "$SLICE8_PG_CONNINFO" -v ON_ERROR_STOP=1 -c "SELECT a.id, a.event_id, a.event_participant_id, a.osrs_character_id FROM event_participant_characters a LEFT JOIN osrs_characters c ON c.\"Id\"=a.osrs_character_id WHERE c.\"Id\" IS NULL ORDER BY a.id;"

# Missing/ambiguous attribution when no swap history exists.
psql "$SLICE8_PG_CONNINFO" -v ON_ERROR_STOP=1 -c "SELECT s.id, s.event_id, s.credited_participant_id FROM submissions s WHERE NOT EXISTS (SELECT 1 FROM event_participant_character_swaps sw WHERE sw.event_id=s.event_id AND sw.event_participant_id=s.credited_participant_id) AND NOT EXISTS (SELECT 1 FROM event_participant_characters a WHERE a.event_id=s.event_id AND a.event_participant_id=s.credited_participant_id AND a.event_role='Playing' AND a.registered_at<=s.submitted_at AND (a.released_at IS NULL OR a.released_at>s.submitted_at)) ORDER BY s.id;"
psql "$SLICE8_PG_CONNINFO" -v ON_ERROR_STOP=1 -c "SELECT s.id, s.event_id, s.credited_participant_id FROM submissions s WHERE NOT EXISTS (SELECT 1 FROM event_participant_character_swaps sw WHERE sw.event_id=s.event_id AND sw.event_participant_id=s.credited_participant_id) AND (SELECT count(*) FROM event_participant_characters a WHERE a.event_id=s.event_id AND a.event_participant_id=s.credited_participant_id AND a.event_role='Playing' AND a.registered_at<=s.submitted_at AND (a.released_at IS NULL OR a.released_at>s.submitted_at))>1 ORDER BY s.id;"

# Future-only swap history and a latest target that is not valid same-event/participant Playing at SubmittedAt.
psql "$SLICE8_PG_CONNINFO" -v ON_ERROR_STOP=1 -c "SELECT s.id, s.event_id, s.credited_participant_id FROM submissions s WHERE NOT EXISTS (SELECT 1 FROM event_participant_character_swaps sw WHERE sw.event_id=s.event_id AND sw.event_participant_id=s.credited_participant_id AND sw.effective_at_utc<=s.submitted_at) AND EXISTS (SELECT 1 FROM event_participant_character_swaps sw WHERE sw.event_id=s.event_id AND sw.event_participant_id=s.credited_participant_id AND sw.effective_at_utc>s.submitted_at) ORDER BY s.id;"
psql "$SLICE8_PG_CONNINFO" -v ON_ERROR_STOP=1 -c "SELECT s.id, sw.id AS swap_id, sw.next_osrs_character_id FROM submissions s JOIN LATERAL (SELECT sw.* FROM event_participant_character_swaps sw WHERE sw.event_id=s.event_id AND sw.event_participant_id=s.credited_participant_id AND sw.effective_at_utc<=s.submitted_at ORDER BY sw.effective_at_utc DESC, sw.recorded_at_utc DESC, sw.id DESC LIMIT 1) sw ON true LEFT JOIN event_participant_characters a ON a.event_id=sw.event_id AND a.event_participant_id=sw.event_participant_id AND a.osrs_character_id=sw.next_osrs_character_id AND a.event_role='Playing' AND a.registered_at<=s.submitted_at AND (a.released_at IS NULL OR a.released_at>s.submitted_at) WHERE a.id IS NULL ORDER BY s.id;"

# Unsupported status/review action and Approved-hidden evidence.
psql "$SLICE8_PG_CONNINFO" -v ON_ERROR_STOP=1 -c "SELECT id, status FROM submissions WHERE status NOT IN ('Pending','ChangesRequested','Approved','Rejected','Withdrawn','Reversed') ORDER BY id;"
psql "$SLICE8_PG_CONNINFO" -v ON_ERROR_STOP=1 -c "SELECT id, submission_id, action FROM review_actions WHERE action NOT IN ('Submitted','RequestChanges','EditMetadata','Approve','Reject','MarkDuplicate','HidePublicEvidence','ShowPublicEvidence','ReverseApproval','Withdraw','Resubmit','ReplaceEvidence','RebalanceContribution') ORDER BY id;"
psql "$SLICE8_PG_CONNINFO" -v ON_ERROR_STOP=1 -c "SELECT id, event_id, team_id, credited_participant_id FROM submissions WHERE status='Approved' AND (public_evidence_hidden OR public_player_hidden) ORDER BY id;"
```

After all Branch A ID queries return zero rows, take a new backup and retry the same EF command. If the exception reported `missing_character_rows`, the legacy assignment query above is the pre-retry zero-row check; do not attempt to query the rolled-back transient snapshot column.

#### Branch B — 8.1 installed, 8.3 pending or failed

Run this branch only when `20260731204600_AddSlice8EvidenceFoundation` is present and `20260801160152_RemoveDeprecatedEvidenceCompatibility` is absent. These columns and indexes now exist, so this branch does not reference a pre-8.1-only shape.

```bash
dotnet ef database update 20260801160152_RemoveDeprecatedEvidenceCompatibility \
  --project src/Bingo.Infrastructure --startup-project src/Bingo.Web \
  --connection "$SLICE8_EF_CONNECTION" 2>&1 | tee slice8-3-preflight-attempt.log
```

```bash
# Credited snapshot and participant/event integrity, including missing character rows.
psql "$SLICE8_PG_CONNINFO" -v ON_ERROR_STOP=1 -c "SELECT s.id FROM submissions s LEFT JOIN event_participants p ON p.id=s.credited_participant_id WHERE p.id IS NULL OR p.event_id<>s.event_id ORDER BY s.id;"
psql "$SLICE8_PG_CONNINFO" -v ON_ERROR_STOP=1 -c "SELECT s.id, s.credited_osrs_character_id FROM submissions s LEFT JOIN osrs_characters c ON c.\"Id\"=s.credited_osrs_character_id WHERE c.\"Id\" IS NULL ORDER BY s.id;"
psql "$SLICE8_PG_CONNINFO" -v ON_ERROR_STOP=1 -c "SELECT id FROM submissions WHERE credited_osrs_character_id IS NULL OR credited_character_name IS NULL OR version IS NULL ORDER BY id;"

# Orphans, duplicate active assets, and duplicate direct children.
psql "$SLICE8_PG_CONNINFO" -v ON_ERROR_STOP=1 -c "SELECT a.id FROM evidence_assets a LEFT JOIN submissions s ON s.id=a.submission_id WHERE s.id IS NULL ORDER BY a.id;"
psql "$SLICE8_PG_CONNINFO" -v ON_ERROR_STOP=1 -c "SELECT c.id FROM submission_contributions c LEFT JOIN submissions s ON s.id=c.submission_id WHERE s.id IS NULL ORDER BY c.id;"
psql "$SLICE8_PG_CONNINFO" -v ON_ERROR_STOP=1 -c "SELECT r.id FROM review_actions r LEFT JOIN submissions s ON s.id=r.submission_id WHERE s.id IS NULL ORDER BY r.id;"
psql "$SLICE8_PG_CONNINFO" -v ON_ERROR_STOP=1 -c "SELECT submission_id, array_agg(id ORDER BY id) FROM evidence_assets WHERE active GROUP BY submission_id HAVING count(*)>1 ORDER BY submission_id;"
psql "$SLICE8_PG_CONNINFO" -v ON_ERROR_STOP=1 -c "SELECT resubmission_of_submission_id, array_agg(id ORDER BY id) FROM submissions WHERE resubmission_of_submission_id IS NOT NULL GROUP BY resubmission_of_submission_id HAVING count(*)>1 ORDER BY resubmission_of_submission_id;"

# Unsupported status/action and Approved-hidden evidence.
psql "$SLICE8_PG_CONNINFO" -v ON_ERROR_STOP=1 -c "SELECT id, status FROM submissions WHERE status NOT IN ('Pending','ChangesRequested','Approved','Rejected','Withdrawn','Reversed') ORDER BY id;"
psql "$SLICE8_PG_CONNINFO" -v ON_ERROR_STOP=1 -c "SELECT id, submission_id, action FROM review_actions WHERE action NOT IN ('Submitted','RequestChanges','EditMetadata','Approve','Reject','MarkDuplicate','HidePublicEvidence','ShowPublicEvidence','ReverseApproval','Withdraw','Resubmit','ReplaceEvidence','RebalanceContribution') ORDER BY id;"
psql "$SLICE8_PG_CONNINFO" -v ON_ERROR_STOP=1 -c "SELECT id FROM submissions WHERE status='Approved' AND (public_evidence_hidden OR public_player_hidden) ORDER BY id;"
```

Each Branch B command is both the ID-producing diagnostic and its matching zero-row verification when rerun after correction. For missing/ambiguous/future-only/non-Playing attribution, use the original 8.1 exception IDs and the legacy assignment/swap queries from Branch A; the 8.1 migration must be restored to a clean copy before retrying if its own transaction failed.

#### Branch C — post-8.3 verification

Run this branch only when both migration IDs are present. It verifies compatibility columns are gone, retained `ChangesRequested` state is gone, historical review facts remain readable, and final invariants hold without querying removed columns.

```bash
psql "$SLICE8_PG_CONNINFO" -v ON_ERROR_STOP=1 -c "SELECT \"MigrationId\" FROM \"__EFMigrationsHistory\" WHERE \"MigrationId\" IN ('20260731204600_AddSlice8EvidenceFoundation','20260801160152_RemoveDeprecatedEvidenceCompatibility') ORDER BY \"MigrationId\";"
psql "$SLICE8_PG_CONNINFO" -v ON_ERROR_STOP=1 -c "SELECT column_name FROM information_schema.columns WHERE table_schema='public' AND table_name='submissions' AND column_name IN ('duplicate_of_submission_id','public_evidence_hidden','public_player_hidden','public_privacy_requested') ORDER BY column_name;"
psql "$SLICE8_PG_CONNINFO" -v ON_ERROR_STOP=1 -c "SELECT id, status FROM submissions WHERE status='ChangesRequested' ORDER BY id;"
psql "$SLICE8_PG_CONNINFO" -v ON_ERROR_STOP=1 -c "SELECT s.id FROM submissions s LEFT JOIN event_participants p ON p.id=s.credited_participant_id LEFT JOIN osrs_characters c ON c.\"Id\"=s.credited_osrs_character_id WHERE p.id IS NULL OR p.event_id<>s.event_id OR c.\"Id\" IS NULL OR s.credited_character_name IS NULL OR s.version IS NULL ORDER BY s.id;"
psql "$SLICE8_PG_CONNINFO" -v ON_ERROR_STOP=1 -c "SELECT submission_id, array_agg(id ORDER BY id) FROM evidence_assets WHERE active GROUP BY submission_id HAVING count(*)>1 ORDER BY submission_id;"
psql "$SLICE8_PG_CONNINFO" -v ON_ERROR_STOP=1 -c "SELECT resubmission_of_submission_id, array_agg(id ORDER BY id) FROM submissions WHERE resubmission_of_submission_id IS NOT NULL GROUP BY resubmission_of_submission_id HAVING count(*)>1 ORDER BY resubmission_of_submission_id;"
psql "$SLICE8_PG_CONNINFO" -v ON_ERROR_STOP=1 -c "SELECT a.id FROM evidence_assets a LEFT JOIN submissions s ON s.id=a.submission_id WHERE s.id IS NULL ORDER BY a.id;"
psql "$SLICE8_PG_CONNINFO" -v ON_ERROR_STOP=1 -c "SELECT c.id FROM submission_contributions c LEFT JOIN submissions s ON s.id=c.submission_id WHERE s.id IS NULL ORDER BY c.id;"
psql "$SLICE8_PG_CONNINFO" -v ON_ERROR_STOP=1 -c "SELECT r.id FROM review_actions r LEFT JOIN submissions s ON s.id=r.submission_id WHERE s.id IS NULL ORDER BY r.id;"
```

The post-8.3 compatibility-column and `ChangesRequested` queries must return zero rows. Historical request/duplicate/visibility review-action values may remain as preserved audit metadata; their active commands/routes are removed and must not be recreated. For Approved-hidden IDs in either migration branch, use the prior-version application's reasoned Admin reversal/remediation route, preserving contribution/history. Never direct-SQL publish, clear a hidden flag as a shortcut, or automatically reverse. Restore from the backup or use the existing Admin/evidence correction boundary for all other ID-specific corrections; do not casually delete historical evidence.

After the selected branch is zero-row clean, back up and retry the exact EF command with `SLICE8_EF_CONNECTION`, then run Branch C with `SLICE8_PG_CONNINFO`:

```bash
export SLICE8_RETRY_BACKUP="slice8-before-retry-$(date -u +%Y%m%dT%H%M%SZ).dump"
pg_dump --format=custom --file="$SLICE8_RETRY_BACKUP" "$SLICE8_PG_CONNINFO"
dotnet ef database update 20260801160152_RemoveDeprecatedEvidenceCompatibility \
  --project src/Bingo.Infrastructure --startup-project src/Bingo.Web \
  --connection "$SLICE8_EF_CONNECTION"
```
### Residual re-review remediation handoff

Only the four residual findings were corrected: Administrator Tile projection now cannot expose submission creation while authorized non-Admin projection remains available; Review Details uses `ActualEndedAt ?? EventEndsAt` and the focused scenario covers scheduled and early ends; TEST 15 has distinct website-owned Captain, Co-captain, and ordinary Participant identities with exact ownership/role and double-reset assertions; and this runbook now provides bounded commands and ID queries for all migration diagnostics, including duplicate children and Approved-hidden evidence. No approved scope, non-goal, authority rule, route family, or abstraction budget changed; the complexity inventory now correctly counts the two plan-mapped forward migrations.

Focused Release VSTest passed `3/3` (Admin Tile projection, scheduled/early grace projection, and Development reset/idempotency; TRX `/tmp/slice8-residual-focused2.trx`). Release build passed with `0` warnings and `0` errors; formatting verification and `git diff --check` passed. EF pending-model verification was not rerun because no model or migration changed. The complete suite, manual acceptance, and restricted re-review remain intentionally unrun; the next gate is restricted re-review of these four residuals only.

### Latest restricted re-review residual remediation handoff

Only the two latest residual findings were corrected. Development reset now adds one compact `TEST 84 — Evidence history` Finalized fixture with a past authoritative cutoff and one Rejected history row owned by `SeedEvidenceParticipant`; the existing Admin Finalize page can archive it, while TEST 15 remains the normal Live/future-cutoff journey. The reset regression proves the four fixture slugs, TEST 84 Finalized/past-cutoff/results state, ordinary Participant ownership, Rejected history, and double-reset idempotency. The manual checklist now explicitly says End event now enters `AwaitingFinalReview` without closing the active cutoff and gives ordered exact-login/action sequences for live Participant, Co-captain, Admin review, post-cutoff read-only, Finalized, and Archived paths.

The recovery runbook now begins with `__EFMigrationsHistory`, uses separate `SLICE8_EF_CONNECTION` and `SLICE8_PG_CONNINFO` placeholders, and splits schema-safe commands into pre-8.1/failed-8.1, 8.1-installed/8.3-pending, and post-8.3 branches. It preserves the prior-version reasoned reversal path for Approved-hidden evidence and the existing ID-specific correction boundaries. No production behavior, migration, route, authority rule, scope, non-goal, or complexity budget changed; the fixture adds no table, service, policy, job, or abstraction.

Focused integration-test project build passed with `0` warnings and `0` errors. The existing Development reset regression passed `1/1` (TRX `/tmp/slice8-latest-residual-reset2.trx`); formatting verification and `git diff --check` passed. The complete suite, manual acceptance, and restricted re-review remain intentionally unrun; the next gate is restricted re-review of these two residuals only.

### Remediation verification and handoff

Focused retained migration/preflight plus affected SubmissionWorkflow scenarios passed `24/24` through the permitted direct VSTest path (`/tmp/slice8-remed-final-focused2.trx`); the Development reset/idempotency regression passed `1/1` (`/tmp/slice8-remed-reset2.trx`). The Release solution build passed with `0` warnings and `0` errors, EF pending-model check reported no changes, formatting verification passed, and `git diff --check` passed. The complete solution suite, consolidated manual acceptance, and independent re-review remain outside this remediator role; the next action is the restricted re-review of these ten corrections against this exact plan baseline.

### Consolidated manual acceptance handoff — 2026-08-01

Consolidated Slice 8 manual acceptance is approved for the implemented behavior. Team submission history navigation, shared upload/resubmission feedback, UTC review time, and Admin correction-drop scoping passed. The final Safari correction was the compatibility-only replacement of `optgroup.options` with `group.querySelectorAll("option")`; the requirement-change retest then passed. S8-04, S8-07, and S8-09 were already passed; S8-08 mutation steps remain intentionally dropped as unreasonable with public privacy checks retained; Danish manual inspection remains deferred to the approved UI overhaul boundary.

This does not claim final Slice 8 acceptance. The next gate is a narrow independent re-review of only the post-review/manual corrections and final scope delta, followed by final automated gates. No new scope, migration, route family, abstraction, or complexity item was added.

### Final post-review scoped-navigation correction — 2026-08-02

Captain history now exposes its resolved event/team scope and carries it through history → submission details → edit/resubmit/withdraw forms and redirects without changing the shared authority or fail-closed rules. The focused PostgreSQL/HTTP scenario passed `1/1` with one website participant in two events: selected-scope history/details/withdrawal succeeded, while unscoped and cross-event requests were rejected. The Release Web build passed with `0` warnings/errors; formatting verification and `git diff --check` passed. This is a bounded correction only: no new route family, migration, service, policy, abstraction, complexity item, or manual-acceptance reversal. The next gate remains narrow independent re-review of the post-review/manual corrections and final scope delta, followed by final automated gates.

# Slice 7 Implementation Plan

## Approved bounded plan

1. **Pass 7.1 — live-account and team-focus persistence/authority foundation.** Add append-only event-participant character-swap history, private team-focus marker persistence, the required relational constraints/configuration/migration, initial primary Playing-account activation in the authoritative event-start transaction, and the smallest server-side active-character resolution boundary.
2. **Pass 7.2 — participant live context/navigation and active-account swaps.** Add participant live context, navigation, and normal active-account swap workflows on top of the Pass 7.1 authority foundation.
3. **Pass 7.3 — private team focus, role-aware visibility, and final Slice 7 integration.** Add team-focus commands/projections/visibility and integrate the completed Slice 7 behavior.

## Pass 7.1 scope and dependencies

Pass 7.1 depends on the accepted Slice 6 `main` state and extends the existing event, participant, assignment, team, lifecycle, and EF persistence boundaries. It must preserve Slice 6 behavior and establish:

- append-only `EventParticipantCharacterSwap` records with nullable initial previous character, next character, effective/recorded UTC timestamps, actor, and the established correction reason;
- private `TeamFocusMarker` records for TILE/ROW/COLUMN targets with mutually exclusive target identity, focused state, optimistic version, update actor/time, and team-scoped uniqueness;
- deterministic, retry/concurrency-safe initial activation of each eligible participant's built-in primary `PLAYING` assignment at the authoritative event-start instant;
- a small shared server-side query/operation boundary for active `PLAYING` character resolution at an arbitrary UTC instant.

Focused evidence covers clean and representative retained migrations, relational target-shape/integrity constraints, event-start activation including retry/idempotency and `INFORMATIONAL` exclusion, and active-character resolution before/at/after a transition.

## Explicit exclusions

Pass 7.1 does not implement participant swap forms or commands, participant live context/navigation, active-account swaps, team-focus pages or commands, Super Admin inspection, evidence changes, live replacements, Wise Old Man behavior, or page redesign. Pass 7.2 and Pass 7.3 are not started by this implementation task. Manual acceptance and independent review are deferred until the complete Slice 7 behavior exists after Pass 7.3.

## Cutoff wording reconciliation

The accepted requirement is that the normal submission cutoff remains internal lifecycle data, defaults to event end plus 30 minutes, and is not ordinarily displayed as a cutoff timestamp on participant pages. Pages may communicate whether submission is available. The stale Slice 7 wording that says the participant page ordinarily displays the submission cutoff is superseded by this clarification; the implementation does not expose a cutoff display in Pass 7.1.

## Verification and handoff

Run only the focused Domain/PostgreSQL tests justified by Pass 7.1, the affected Release build, EF pending-model check, formatting verification, and `git diff --check`. Record exact results, migration identity, remaining uncertainty, and confirmation that Pass 7.2 was not started here and in `CURRENT_STATUS.md`.

## Verified Pass 7.1 result

Verified 2026-07-30. Pass 7.1 adds the append-only `EventParticipantCharacterSwap` and private `TeamFocusMarker` domain/persistence records, EF configuration/DbSets, target-shape and team-scoped uniqueness constraints, the retained model snapshot, and migration `20260730212304_AddSlice7LiveAccountAndTeamFocusFoundation`. The authoritative manual/scheduled event-start transaction now validates confirmed participants through the existing primary Playing authority, appends one deterministic initial activation at `ActualStartedAt`, excludes informational assignments, remains retry-safe, and fails closed without changing lifecycle state when a confirmed participant lacks valid Playing authority. `ActiveCharactersAt`/`ActiveCharacterAtAsync` provides the small UTC as-of resolution boundary.

Focused PostgreSQL evidence passed `5/5`: clean and representative-retained migration, relational focus/swap integrity, event-start activation/idempotent retry/informational exclusion, fail-closed missing authority, and before/at/after UTC active-character resolution. The Release solution build passed with `0` warnings and `0` errors; EF pending-model check reported no changes; formatting verification and `git diff --check` passed. No complete solution suite was run.

Remaining uncertainty is intentionally deferred: manual acceptance and independent review will run after Pass 7.3, when participant live context, swap UI, and private team-focus behavior are all present. Pass 7.2 and Pass 7.3 were not started in this Pass 7.1 checkpoint, and no participant swap commands/forms, team-focus commands/pages, Super Admin inspection, evidence changes, live replacements, Wise Old Man behavior, or page redesign were added there.

## Pass 7.2 scope and dependencies

Pass 7.2 depends on the Pass 7.1 transition persistence, event-start activation, and UTC as-of query. It is bounded to participant-facing live context/navigation and normal active-account swaps: participant-aware My Events routing to the existing roster/team-board destinations, planned/current account and event-end context, participant self-swaps, and captain/co-captain swaps for unlinked members of the captain's own pre-formed team. The operation uses the existing event/team/participant boundaries, the next whole UTC minute rule, a participant-scoped PostgreSQL transaction lock, expected-current optimistic validation, and append-only transition persistence.

Pass 7.2 does not add a migration or change the accepted Slice 7 persistence model. It does not implement private team-focus projections or commands, role-aware focus visibility, Super Admin inspection, evidence credit/submission changes, live replacements, Wise Old Man behavior, or page redesign. Manual acceptance and independent review remain deferred until the relevant combined Slice 7 behavior exists after Pass 7.3.

## Verified Pass 7.2 result

Verified 2026-07-30. Added `IParticipantLiveService` and its PostgreSQL implementation, participant-aware My Events/confirmation navigation, roster context, and live team-board context with normal swap forms. The authoritative operation allows only LIVE swaps among current Playing assignments, retains the previous account until the first whole UTC minute strictly after the request, blocks a second future-effective request, validates the expected current account, and permits captain/co-captain action only for unlinked members of their own pre-formed team. Informational assignments, linked teammate delegation, unrelated viewers, and non-live lifecycle states are rejected by the server boundary. No migration was required for Pass 7.2.

The focused PostgreSQL Slice 7 test filter passed `6/6` (the five retained Pass 7.1 tests plus the representative live swap/retry test). The Release solution build passed with `0` warnings and `0` errors; formatting verification and `git diff --check` passed. No complete solution suite or manual acceptance was run. Pass 7.3 was not started.

## Pass 7.3 scope and dependencies

Pass 7.3 depends on the Pass 7.1 persistence/authority foundation and the Pass 7.2 participant live context and swap boundary. It is limited to private team focus, role-aware visibility, and final Slice 7 integration on the existing team-board interaction model. It does not change the persistence model, add a migration, or add evidence/progress/ranking behavior.

The implementation adds the shared team-focus query/mutation boundary, validates current event membership and captain/co-captain authority, applies optimistic versions inside a serializable team-scoped transaction, and rejects mutation after event end. Current team members receive their own team's markers; other viewers receive an empty non-visible projection; Super Admin cross-team inspection is explicit, read-only, page-scoped, and does not persist inspection state. The existing TeamBoard route retains its responsive route-backed fallback, while team-scoped SignalR invalidation refreshes visible projections only. Public/opponent projections do not receive marker data.

Pass 7.3 excludes participant swap redesign, evidence changes, live replacements, Wise Old Man behavior, Super Admin editing, cross-team default projections, and page redesign. Manual acceptance and independent review were intentionally deferred until the complete Slice 7 behavior existed; they are now the next acceptance gate.

## Verified Pass 7.3 result

Verified 2026-07-30. Pass 7.3 is implemented within the approved scope. The focused PostgreSQL Slice 7 filter passed `7/7`, including retained migration/constraint coverage, event-start activation and UTC active-character resolution, live swap/retry behavior, and the integrated team-focus privacy/authority/concurrency/end-state scenario. The Release solution build passed with `0` warnings and `0` errors; EF reported `No changes have been made to the model since the last migration`; formatting verification and `git diff --check` passed. No new migration was required. No complete solution suite was run.

Remaining uncertainty is limited to the deferred combined Slice 7 manual acceptance and independent review of participant live context, swaps, private focus, role-aware visibility, responsive/no-JavaScript fallbacks, and realtime refresh behavior. Pass 7.3 did not add evidence, live replacements, Wise Old Man behavior, or redesign work.

## Restricted independent-review P1 remediation

Verified 2026-07-31. Normal swap availability and mutation now require `EventState.Live` plus an authoritative current time strictly before configured `EventEndsAt`; projection omits swap availability and direct stale/at-end POSTs reject without transition residue or active-account history changes. Ordinary Website initial activation now resolves the current Playing assignment linked to the active form's unique built-in primary Account system question, excluding secondary Playing assignments even when older/lower registration; missing or ambiguous ordinary authority fails before lifecycle, transition, audit, or activation persistence. External/pre-formed/Admin-created participants retain only explicit primary-question-linked or null-question/registration-order compatibility paths, and Informational assignments remain excluded. No migration was added.

The authoritative source was imported after exact HEAD, complete status path/status, and SHA-256 matching for all 35 changed/untracked regular files. Focused PostgreSQL Slice 7 coverage passed `10/10`, including exact/post-end projection and mutation rejection with no residue, built-in-primary selection over an older secondary Playing assignment, missing/ambiguous ordinary-primary atomic failure, and retained Admin-created compatibility. The Release solution build passed with `0` warnings and `0` errors; EF reported no pending model changes; formatting verification and `git diff --check` passed. No complete suite, manual testing, or re-review was run. The restricted independent re-review remains the next gate.

## Bounded focus-marker remediation

Verified 2026-07-31. Removed the erroneous team-wide `HasAlternateKey(EventId, TeamId)` and added migration `20260731170051_RemoveTeamFocusEventTeamAlternateKey` to drop the applied `AK_team_focus_markers_event_id_team_id`; filtered Tile/Row/Column uniqueness remains. TeamBoard tile forms bind the selected tile as a Tile target with no row/column identity. Completed tiles omit individual focus controls, direct completed-tile focus is rejected, and authoritative approval plus reversal/rebalance paths clear focused tile markers in the same transaction, advance their versions, and notify the team after commit. Reversal leaves the marker unfocused and does not restore it.

Focused Slice 7 PostgreSQL/PageModel coverage passed `13/13`, including retained migration upgrade, simultaneous markers/independent unfocus, authenticated tile binding, completed-tile rejection/control guard, approval clearing, and reversal/rebalance no-resurrection. The focused existing approval/reversal scenario passed within the affected workflow filter; three unrelated public-projection tests in that broader filter remain stale against the prior Slice 7 Website-primary fixture shape. Remaining manual retest is limited to multiple target/unfocus behavior and completed-tile automatic clearing/no resurrection. No staging, commit, merge, or push occurred.

## Clarified focus rendering and Clear all correction

Verified 2026-07-31. Completed tiles are excluded from all individual, row, and column focus styling/badges, while active row/column markers remain valid for incomplete members. The existing Tile control omission, server-side completed-tile rejection, authoritative approval/reversal clearing, version advancement, and no-resurrection behavior remain intact. A single authorized TeamBoard `Clear all focus` action now validates the complete per-team marker ID/version snapshot in a serializable team-scoped transaction, clears every active marker for that team, advances each changed marker version, and emits one normal team invalidation. Stale or unauthorized requests return existing failure feedback without residue; individual unfocus and overlapping marker independence remain unchanged. No migration was required for this correction.

The focused Slice 7 PostgreSQL/PageModel class passed `13/13`; the Release Web build passed with `0` warnings and `0` errors; formatting verification and `git diff --check` passed. Manual retest remains: verify completed-tile exclusion when its row/column is focused, no old tile-marker resurrection after reversal, one-team-only Clear all across overlapping markers, and accurate stale/unauthorized feedback without residue.

## Restricted-review stale completed-focus remediation

Verified 2026-07-31. Added forward migration `20260731180603_NormalizeCompletedTileFocusMarkers` without rewriting either prior Slice 7 migration. Its deterministic PostgreSQL update uses the same authoritative completion facts as current progress—`BoardRequirementSnapshots` and non-reversed `SubmissionContributions` grouped by team/requirement—to deactivate retained focused Tile markers, increment their versions, and clear their update actor/time; row and column markers remain unchanged. `TeamFocusService` defensively filters Tile markers for currently complete tiles from private projections, preventing stale data from reaching focus summaries while preserving row/column visibility and the approved completed-tile rendering exclusion.

The retained migration rehearsal and focused Slice 7 PostgreSQL/PageModel class passed `13/13`; the rehearsal inserts the complete-tile marker after the original Slice 7 foundation and before the correction chain, proves it is focused before normalization, and proves it is cleared while row/column markers remain focused afterward. EF pending-model check reported no changes; the Release Web build passed with `0` warnings and `0` errors; formatting verification and `git diff --check` passed. No unrelated behavior changed. Next step: restricted re-review, not final acceptance.

## Final acceptance

Accepted 2026-07-31. Manual S7-01 through S7-07 and the final focus-remediation retests passed. Independent review and the restricted retained-focus re-review cleared all concrete findings. Final automated results are Domain `157/157`, Application `83/83`, Browser `66/66`, and Integration `216/216`, for `522/522` passed with no failures or skipped/not-executed tests. Release solution build, repository formatting, EF pending-model, migration/reset coverage, `git diff --check`, verifier parity, staging, and bounded secret/runtime-artifact checks passed. Durable Integration evidence: `/private/tmp/slice7-final-integration-external-20260731/integration-final.trx`.

Slice 7 is ready for packaging and push. Merge to `main` remains a separate action.

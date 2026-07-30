# Slice 6 implementation plan — catalogue, board derivation, approval snapshot, preview, and publication

**Status:** Slice 6 accepted on 2026-07-30 and ready for packaging/integration; no Slice 7 work has started. The board preview is functionally accepted, while its exact visual match to the established **View bingo** Board is deferred to the UI overhaul and is not a functional blocker.
**Branch:** `codex/milestone-8a-slice-6`.
**Boundary:** preserve the established private board-editor and public-board interaction models. This slice adds only the functional data, controls, validation, and state changes required below; it is not a UI-overhaul pass.

## Catalogue correction (2026-07-30)

Rate variants and the standalone Items catalogue were not approved and are removed from the active product. A boss/activity owns individual source drops; every source drop has one authoritative displayed rate, probability, and EHB. Distinct drops, including `Nid` and `Nid (Destroy)`, remain separate source drops. Admins create and edit item identities only through those drops. Board requirements select source drops directly, Draft derivation uses the live source-drop probability, and approval freezes that source drop's values without a variant selection or variant snapshot. The retained non-web import service writes source-drop records directly. Migration `20260730160029_RemoveSlice6RateVariants` retires the committed legacy variant tables after preserving the real source-drop and approval-snapshot rows.

## Final acceptance (2026-07-30)

Manual acceptance S6-01 through S6-06 is approved, including the final mixed same-boss single-roll/multiplied-roll Zulrah-style EHB retest. Final automated results passed with zero failures/skips: Application `83/83`, Domain `157/157`, Browser `66/66`, Integration `203/203`, combined `509/509`. Release build completed with 0 warnings/errors; formatting, EF pending-model, migration rehearsal, Development reset, and `git diff --check` passed. Durable final Integration TRX: `/private/tmp/slice6-final-integration-rerun-20260730/integration-final.trx`. Accepted code/test commits include `deaec56b` and test-only gate stabilization `a3da933d`.

## Product decisions already approved

1. **Approval is private.** It freezes a valid board; it does not publish it.
2. **Publication is separate and later.** It is available only after both draft finalization and board approval. A successful finalized-draft flow offers a separate **Publish board?** choice when the board is eligible. Leaving that flow does not publish anything and leaves a later Publish action for an eligible board.
3. The website's Wiki catalogue import route, navigation, and callable web handlers are removed. The underlying import service is retained, unexpanded, for a possible future controlled use; manual catalogue maintenance is the version-one workflow.
4. Private Preview board uses deterministic demonstration data—some completed tiles, some partial progress, and representative fixed sidebar statistics—to show the public treatment without creating records, changing progress, approving, or publishing.
5. Published corrections, including while Live, are exceptional and require confirmation, written reason, immutable replacement snapshot, authoritative recalculation, audit, and retained prior history.
6. The older `DATA_MODEL.md` wording that says draft finalization publishes the board is stale. The separate-publication rule in this plan and `FUNCTIONAL_WORKFLOWS.md` section 19 is authoritative and must be reconciled during implementation.

## Pass 6.1 — persistence and retained-data foundation (implemented 2026-07-30)

- Add optimistic concurrency/version fields required for catalogue and board approval mutation.
- Add immutable approval snapshot storage for the complete board, tiles, requirements, selected catalogue/rate/drop data, public wording/artwork references, calculations, EHB, actor/time, lifecycle, and supersession history.
- Make the active approval pointer explicit and preserve historical snapshots.
- Backfill retained boards deterministically, preserving existing board/tile identities and historical competitive records; fail closed on ambiguous retained data.
- Add only the migration, model/configuration, and focused migration/domain coverage necessary to establish these invariants. No editor or public behavior redesign.

**Pass gate:** clean and representative retained PostgreSQL migration rehearsal; focused snapshot/concurrency invariant coverage; Release build, formatter, model check, and diff check.

**Verified:** Domain `12/12`; PostgreSQL clean/retained/fail-closed/concurrency coverage `3/3`; clean Release solution build; EF pending-model check; formatting and diff check. Legacy published-board actor data did not exist, so backfilled snapshots retain a null/unknown actor instead of fabricating attribution.

## Pass 6.2 — manual catalogue administration

Status: corrected. Manual boss/source-drop concurrency and audit, Super-Admin unused-record deletion safeguards, and public Wiki import route removal remain. Item identities are only created/edited through source drops; no independent item or rate-variant lifecycle remains.

- Enable Admin create, edit, deactivate, and reactivate for catalogue bosses, activities, and drops with optimistic concurrency and routine audit.
- Permit Super Admin permanent deletion only after strong confirmation and a transactional dependency check proves the record is genuinely unused; referenced records deactivate instead.
- Keep external source-image URLs exclusive to catalogue records and their existing validated cache/fetch path.
- Remove the Wiki import page, navigation, and web handlers while preserving the non-web import implementation for later controlled use. Do not expand import features.

**Pass gate:** focused authorization/concurrency/deactivate-versus-delete coverage and affected route coverage; Release build, formatter, and diff check.

## Pass 6.3 — live board derivation and editor validation (implemented 2026-07-30)

- Retain the existing board-editor interaction model, move/swap controls, and protected route fallbacks.
- While a board is Draft, derive catalogue-backed names, images, source-drop rates, and EHB from current catalogue data. Relevant catalogue mutations invalidate/recalculate affected unapproved board projections.
- Standard catalogue/drop tiles require valid automatic EHB; manual EHB is permitted only for custom/manual objectives.
- Replace custom tile image URLs with managed uploads under the established asset pipeline.
- Remove obsolete tile evidence-instruction and reusable-template behavior.
- Complete board-owned tile, resize compaction, validation, and balance-warning boundaries without adding cross-event copy/import/template flows.

**Pass gate:** focused derivation/validation and protected-editor regression coverage; Release build, formatter, and diff check.

**Verified:** managed board-tile image migration `20260729234011_AddSlice6ManagedBoardTileImages`; clean/retained PostgreSQL rehearsal `3/3`; focused PostgreSQL handler derivation/image-boundary/resize regression `3/3`; Board domain `13/13`; protected editor markup `3/3`; Release Web build with 0 warnings/errors; EF pending-model check; formatter; and diff check. The later selected-variant remediation is superseded by the catalogue correction above. Resize compaction is asserted at `Slice6CatalogueAdministrationIntegrationTests.ResizeCompactsDraftTilesFromTheTopLeftInExistingOrder`. Manual derivation coverage remains consolidated under S6-02 after later approval/publication passes.

## Pass 6.4 — private approval snapshots

- Approve only a grid-complete, valid board in one transaction that rechecks referenced catalogue versions, derives every required value, and persists one immutable snapshot.
- Approval keeps the board private and gives it the approved/validated state.
- Explicit unapproval, or an edit to competitive board content before publication, returns the board to Draft/live derivation while retaining the superseded snapshot history.
- Prevent mixed snapshots under concurrent board/catalogue edits.

**Pass gate:** focused PostgreSQL approval/unapproval/concurrency/rollback coverage and retained migration rehearsal if persistence changes; Release build, formatter, model check, and diff check.

**Verified:** Approval now performs one serializable private transaction: it rechecks a grid-complete board, active catalogue dependencies, live EHB derivation, managed-image ownership, and optimistic board version before writing one immutable snapshot tree, active pointer, validated state, and structured audit. Explicit unapproval keeps history but clears the active pointer; competitive tile, move, resize, and remove operations automatically unapprove inside their own transaction. Focused PostgreSQL coverage proves private approval/frozen snapshots, invalid/incomplete rejection without residue, explicit/history-preserving unapproval, automatic unapproval after competitive edit, and a two-context single-winner race (`6/6` within the Slice 6 catalogue/board regression). No publication behavior is introduced.

## Pass 6.5 — private preview and separate publication

- Add private Preview board using the real public board treatment without Admin controls, actual team/participant data, persistent progress, approval, or publication side effects.
- Draft preview uses live derived values; approved preview uses the active frozen snapshot.
- After successful draft finalization, offer **Publish board?** only when the board is complete and approved. The choice is a separate publish transaction; dismissing it changes nothing.
- When finalization has already occurred, retain a clear later Publish action only for an approved complete private board.
- Publish exactly the active immutable snapshot without recalculation. Board publication is required before event start.
- Post-publication corrections require confirmation and reason, create a replacement snapshot, recalculate authoritative effects, and retain prior public history. This applies during Live as an exceptional correction route.

**Pass gate:** focused finalization-to-prompt, publication, privacy, preview, correction/history, and start-block coverage; Release build, formatter, and diff check.

**Development baseline:** `--reset-test-data` leaves only TEST 13 (`test-13-dkl-board`) for private live-derivation/approval/preview, TEST 62 (`test-62-board-publication-setup`) for finalized-roster separate publication and correction, and TEST 15 (`test-15-dkl-live`) for the established public live board. The double-reset regression proves this exact set and its core publication facts.

**Final automated/review gate:** Independent review cleared. The final acceptance record above is authoritative.

**Preview scope note:** Preview navigation and deterministic, side-effect-free functionality are accepted. Exact visual parity with the established public **View bingo** Board is deferred to `UI_OVERHAUL_ROADMAP.md` and is not a Slice 6 functional blocker.

## Slice completion flow

Slice 6 acceptance is complete and the branch is ready for separately authorized packaging/integration. No Slice 7 work has started.

# AU03 independent review — 2 October 2026

Verdict: BLOCKED — one required P2 integration finding. No other required findings.
Reviewer: /root/au03_reviewer, independent read-only review; report to /root only.
Checkout: /Users/christopher/.codex/worktrees/participants-functionality/BingoWebpage
Branch/base: codex/participants-functionality / 0ec8add9314a65ab66751bf44bbb4f5195ff854f.

## Verified scope and identity

Reviewed the complete AU03 ticket diff against DELIVERY_PLAN AU03 and the promoted creation contracts, not the unrelated whole-HEAD diff. Corrected au03.patch SHA-256: fd617d662dfa2160cd8b4732340827e6e874cbe12390712944f6f0bb097d4ec4. Full live source.sha256 manifest SHA-256: c621e6232aa655703f9618955cf008ce08db20a0b4ad7a9ed2f22fc2022355d4. All 20 live hashes match before and after review; all 13 retained before-source hashes match; deleted Web slug path is absent. The original capture included one unrelated concurrent AU15 documentation hunk. provenance-correction.md and excluded-concurrent-au15.patch preserve that separation; no source changed and its exact author is unverified. The corrected AU03-only patch hash was personally verified.

Required AU03 scope delivered: actor-scoped durable UUID identity; immutable trimmed original payload; serialized identical retries with one atomic aggregate/audit/operation; changed-payload rejection; same-name/different-key creation; authoritative actor-owned CheckAgain transport; current enabled website Admin authority and hidden/discarded result access; retained keys through rename/hide/discard; validation and private-draft defaults; existing form/redirect behavior and slug collision retries. No required AU03 product behavior is missing. The reset integration below is the outstanding compatibility fix.

Approved complexity: one focused Application interface/result contract, one Infrastructure creation service extracted from the PageModel, one entity/table/configuration, complete migration/designer/snapshot, and one handler on the existing route. Pure slug utility move is namespace-only; other callers have import-only changes. No generic receipts framework, new page, job, policy, unbudgeted abstraction, or changed explicit non-goal. Modal binding/manual UI acceptance remain deferred. The empty-board wording correction matches already-existing approved behavior.

## Required finding

**P2 — Include the creation operation table in Development reset.**

The new migration adds `event_creation_operations.event_id → events.id` (src/Bingo.Infrastructure/Persistence/Migrations/20261002114016_AddEventCreationOperations.cs:33–38), but `DevelopmentScenarioSeeder.ClearWorkflowDataAsync` (src/Bingo.Web/TestData/DevelopmentScenarioSeeder.cs:2150–2171) uses an explicit `TRUNCATE TABLE ... events ... RESTART IDENTITY` list without the new table or CASCADE. PostgreSQL refuses to truncate a table referenced by a foreign key unless the referencing table is also included, even if it has no rows. Therefore applying AU03 makes the existing Development reset fail. This directly affects the migration's compatibility with the authoritative reset workflow.

Smallest fix: add `event_creation_operations` to that existing reset list, preserving restrictive production FKs. Prove the existing reset with a disposable PostgreSQL database migrated through AU03, preferably including a real creation operation, and assert the operation is cleared while reset succeeds. Do not reset the user's database or broaden seed behavior.

## Evidence assessed

Personally performed: read-only scoped source/contract/test review; before/after live manifest checks; retained-baseline hash checks; corrected patch identity; migration Up/Down and model comparison (designer/snapshot model bodies identical); namespace-only slug move comparison; direct reset and discard dependency inspection. Discard preserves the event tombstone and does not delete the operation; reset has the concrete omission above.

Reused executed evidence, not rerun by reviewer: 13 unique PostgreSQL integration cases with final passing outcomes (11 initial passes plus 2 corrected-fixture passes), Release build with 0 warnings/errors, 6 slug tests, 2 architecture guards, scoped whitespace/leak and inherited-source checks. Personally inspected the retained TRX outcomes and build output, and reviewed the tests' discriminating boundaries: real independent connections blocked on PostgreSQL advisory lock, post-commit response-loss interceptor and readback, atomic rollback, authenticated/antiforgery HTTP validation and actor isolation, changed payload/key, revoked actor, rename/hide/discard, and deterministic timestamp precision. The initial two failures were test-fixture issues with retained passing replacements; they are not pending production defects.

No new runtime tests, browser/manual review, provider calls, user database mutation, source edits, packaging, or next-ticket work. The reset failure is established from the new FK and unchanged SQL; it was not executed by this reviewer. Return to the same implementer for the named fix, then this reviewer rechecks only that finding and direct consequences.

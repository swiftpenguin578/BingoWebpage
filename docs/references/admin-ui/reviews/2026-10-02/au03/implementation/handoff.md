# AU03 implementation handoff — 2 October 2026

Role: implementer only. Stable implementation, named reset remediation and focused checks are complete; ready for the same reviewer’s named recheck. No independent-review or manual-acceptance pass is claimed.

Checkout: /Users/christopher/.codex/worktrees/participants-functionality/BingoWebpage
Branch: codex/participants-functionality
Base HEAD: 0ec8add9314a65ab66751bf44bbb4f5195ff854f
Orchestrator: /root, app chat 01a0fc60-6ac1-7fb1-a850-8f31b4074361
Implementer: /root/au03_implementer

## Stable result and source identity

- The existing inline creation transaction is extracted to focused IEventCreationService / EventCreationService; the existing minimal aggregate, audit, defaults and 10-attempt unique-slug recovery are retained.
- One event_creation_operations table has composite (actor_account_id, request_id) primary key, immutable trimmed original name/timezone and unique event_id. Restrictive account/event FKs and retained event tombstones preserve outcomes. Full migration, designer and snapshot included; no backfill.
- A namespaced actor/key PostgreSQL transaction advisory lock serializes retries and Check again. Aggregate + audit + operation commit atomically. Same actor/key/canonical input returns the same result; changed valid payload conflicts; invalid input is rejected before mutation; another key can create another same-name event.
- Current enabled website Admin/SuperAdmin authority is checked in the service. Lookup ignores arbitrary actor parameters and uses the authenticated actor. Original payload survives identity changes. Hidden results require current SuperAdmin; discarded results are unavailable. Their keys remain reserved and cannot recreate them.
- Existing GET /Admin/Events/Create supplies a hidden request UUID, preserved on errors. Missing/malformed keys fail closed. Existing POST redirects to Manage; repeat returns the same destination. CheckAgain GET returns only the actor's accessible committed event ID, 404 otherwise, with no-store. Absence does not prove an in-flight request can never finish; retry the same key. New modal/UI binding remains deferred.
- Pure slug generation moved unchanged from Web to Domain for reuse by Infrastructure. Existing callers continue using the same algorithm; only Catalogue and the slug-test imports changed.
- Accepted details were promoted before code in PRODUCT_REQUIREMENTS, FUNCTIONAL_CONTRACTS, DATA_MODEL and TECHNICAL_ARCHITECTURE. The directly affected private-draft contract's stale “no board” sentence was reconciled to the already-approved empty 5×5 board.

source.sha256 covers all 22 current changed/new sources, including contract docs and all migration artifacts. Deleted/moved old Web slug path is recorded in deleted-sources.txt and before.sha256. owned-files.txt lists all 23 affected paths.

source.sha256 SHA-256: feafacaa3ce82f6ada9c64d6cd257b23f308afcb87e61d09a20b75288a9deda7
au03.patch SHA-256: 12b1d9ba24b727eb826d0a0a32ee89e17e6d39c4c4694d91c1726c49938a0c1c

au03.patch is the complete before-AU03-to-current ticket diff, including new/deleted files. It excludes inherited AU01/AU02/planner changes by comparing with retained before/ copies for pre-existing files, and excludes the concurrent AU15 Product Requirements hunk identified during review. See provenance-correction.md and excluded-concurrent-au15.patch; the original capture is retained. The whole-file source manifest intentionally still reflects that concurrent edit, which remains in live source. Patch scope, not whole-file hashing, defines AU03 ownership. before.patch, before-status.txt and before.sha256 preserve initial identity. All eight inherited AU01/AU02 code/test sources match their retained manifests (protected-source-check.log). Their authority edits are preserved in the before baseline. CURRENT_STATUS/DELIVERY_PLAN/reference status rows were not edited by this worker.

## Executed proof

All 13 distinct focused integration cases have passing evidence: 11 passed on the initial execution; the two corrected fixture cases then passed in their targeted rerun. Do not interpret the initial 11/13 TRX alone as the final verdict; do not rerun the 11 passing cases merely to obtain a single green file. Production code did not change between those executions.

1. Command:
   `dotnet test tests/Bingo.IntegrationTests/Bingo.IntegrationTests.csproj --no-restore --filter 'FullyQualifiedName~EventCreationRetryIntegrationTests|FullyQualifiedName~Slice3CreationIdentityPersistenceIntegrationTests.Creation|FullyQualifiedName~Slice3CreationIdentityPersistenceIntegrationTests.ConcurrentCreation' --logger 'trx;LogFileName=au03-focused.trx' --results-directory /private/tmp/au03-implementation-20261002`
   Exit 1: 11 passed, two test-fixture failures, zero skipped. Exact preserved output first-execution.log (also focused.log); TRX au03-first-execution.trx (also au03-focused.trx).
   Seven existing creation regressions pass: actual authenticated minimal creation/retired wizard rejection and subsequent identity flow; different-key automatic slugs and concurrent same-name creates; minimal defaults; forced required-insert failure rolls back event/form/questions/board/audit AND operation; supported timezones; built-in questions and board defaults.
   New passing cases: two independently connected same-key requests verified blocked on the real PostgreSQL advisory lock then returning the exact same outcome with one aggregate/audit/operation; repeated replay and changed-name/timezone conflicts with full unchanged persistence snapshot; different-key same-name creation; loss of commit response injected by DbTransactionInterceptor after PostgreSQL committed, followed by authoritative readback and retry with no extra writes; disabled/demoted actor guards across create/replay/lookup with unchanged snapshots.
   Timestamps are deterministic UTC. Lost-response case deliberately supplies Now + 7 ticks and verifies PostgreSQL's microsecond round trip (Now); request identity has no timestamp dependency.

2. Targeted fixture-corrected rerun:
   `dotnet test tests/Bingo.IntegrationTests/Bingo.IntegrationTests.csproj --no-restore --filter 'FullyQualifiedName~EventCreationRetryIntegrationTests.OriginalPayloadSurvivesRenameAndUnavailableResultsNeverFreeTheirKeys|FullyQualifiedName~EventCreationRetryIntegrationTests.AuthenticatedRequestValidatesInputAndKeyAndSupportsAuthorizedCheckAgain' --logger 'trx;LogFileName=au03-remediated-fixtures.trx' --results-directory /private/tmp/au03-implementation-20261002`
   Exit 0, PASS 2/2, zero skipped. remediated-fixtures.log / au03-remediated-fixtures.trx.
   First fixture originally called Hide on Draft (domain correctly refuses). Corrected by moving the original event through legal SignupOpen/SignupClosed/Live/end states; a separate new Draft proves discard behavior. The other failure used HTTP secondary clients under Testing, which did not retain the secure login cookie. Set all clients to HTTPS. No production fix was needed.
   Proof now passes: immutable replay after name/timezone edits; normal-Admin hidden denial, creator promoted to SuperAdmin can look up hidden result; discard denial/no recreation. Actual login/antiforgery POST rejects 51 Unicode code points, blank name, unsupported timezone, missing/malformed UUID and retired inputs with full persistence unchanged; 50 astral Unicode code points succeed; duplicate POST returns identical redirect; changed-payload POST is 409; owner CheckAgain returns event ID/no-store; different Admin with forged actorId gets 404; member/anonymous/disabled lookup denied; disabled POST writes nothing.

3. `dotnet build Bingo.slnx --configuration Release --no-restore`
   Exit 0, PASS, zero warnings/errors. release-build.log.

4. `dotnet test tests/Bingo.Domain.Tests/Bingo.Domain.Tests.csproj --configuration Release --no-build --no-restore --filter FullyQualifiedName~ArchitectureTests --logger 'trx;LogFileName=au03-domain-architecture.trx' --results-directory /private/tmp/au03-implementation-20261002`
   Exit 0, PASS 1/1. domain-architecture.log / au03-domain-architecture.trx.

5. `dotnet test tests/Bingo.Application.Tests/Bingo.Application.Tests.csproj --configuration Release --no-build --no-restore --filter FullyQualifiedName~ArchitectureTests --logger 'trx;LogFileName=au03-application-architecture.trx' --results-directory /private/tmp/au03-implementation-20261002`
   Exit 0, PASS 1/1. application-architecture.log / au03-application-architecture.trx.

6. `dotnet test tests/Bingo.BrowserTests/Bingo.BrowserTests.csproj --configuration Release --no-build --no-restore --filter FullyQualifiedName~EventSlugGeneratorTests --logger 'trx;LogFileName=au03-slug.trx' --results-directory /private/tmp/au03-implementation-20261002`
   Exit 0, PASS 6/6 pure utility cases; no browser walkthrough. slug.log / au03-slug.trx.

7. Scoped `git diff --check -- <owned paths>` and added-line whitespace scan including new files: PASS (diff-check.log). Added-content private-key/provider-token/credential-URL scan: PASS (leak-check.log); credentials are explicitly synthetic disposable-test values only. Final source manifest verified 20/20 unchanged; protected-source-check.log verifies eight inherited sources.

Migration command: `dotnet ef migrations add AddEventCreationOperations --project src/Bingo.Infrastructure --startup-project src/Bingo.Web` (migration-generation-final.log: build and generation success). Actual migration executes successfully against disposable PostgreSQL 17 in every integration fixture. No user database was accessed.

Initial compile diagnostics retained: migration-generation.log plus initial-build.log show CA1859 (LockAsync return Task<int>, corrected); focused-initial.log shows generated migration IDE0161 (converted generated migration to file-scoped namespace); focused-compile-second.log shows new test helper CA1822 (marked static). These are distinct corrected conditions; no unchanged failing command was repeated. The first actual test execution and its two fixture corrections are retained above.

## Limits and next owner

No unresolved implementation finding or environment blocker. No production/user database mutation, provider calls, app restarts, full suite, browser walkthrough, staging/commit/push/merge/deployment or next-ticket work. UI modal integration and manual visual acceptance are explicitly deferred. No generic receipt framework added.

Next: orchestrator returns the named reset remediation to the same independent reviewer against reset-remediation.patch, updated au03.patch/source.sha256 and retained checks. Stable source is frozen; implementer is available for named remediation. Only orchestrator owns ticket/status reconciliation and planner callback.


## Required P2 reset remediation — 2 October 2026

Independent review /private/tmp/au03-review-20261002/review.md reported exactly one
required compatibility finding: DevelopmentScenarioSeeder's explicit TRUNCATE
list omitted event_creation_operations, so PostgreSQL would reject resetting events.
The single production correction adds that table to the existing list. Restrictive
production foreign keys and existing seeding behavior are unchanged. No code from
the original 20-source manifest changed. The new seeder baseline is retained under
before/ and included in the updated 14-source before.sha256.

One focused partial-class regression reuses the existing Slice1IdentityIntegrationTests
PostgreSQL/Testcontainers, Development environment, catalogue and evidence fixtures:
Slice1IdentityIntegrationTests.EventCreationReset.cs. It creates a real event/outcome,
executes actual ResetAndSeedAsync, verifies the old event/outcome are gone, the seeded
SuperAdmin/Admin and private Draft/signup form exist, and completes creation/readback/
replay as the seeded Admin with exactly one new outcome. Deterministic UTC fixture
time is 2026-10-02 12:00:00. No user database or running app is touched.

Command:
`dotnet test tests/Bingo.IntegrationTests/Bingo.IntegrationTests.csproj --no-restore --filter FullyQualifiedName~DevelopmentResetClearsCreationOperationsAndRetainsSeededCreationJourney --logger 'trx;LogFileName=au03-reset-remediation.trx' --results-directory /private/tmp/au03-implementation-20261002`
Final exit 0, PASS 1/1, zero skipped. reset-remediation.log and
au03-reset-remediation.trx. First execution successfully performed the reset but then
failed an incorrect test-only expectation that the existing draft-discard fixture has
a board. Removed that unrelated assumption without changing seed behavior. Initial
reset-first-execution.log and au03-reset-first-execution.trx retained. Only this one
case was rerun; the initial 13 AU03 cases and utility/architecture evidence remain
valid and were not repeated.

Applicable final Release command:
`dotnet build Bingo.slnx --configuration Release --no-restore`
Result: see reset-release-build.log; final exit recorded in reset-handoff.md.

Scoped whitespace and added-content leak checks pass, including the new test;
reset-scoped-checks.log. Pre-remediation manifest and corrected patch retained as
pre-remediation-source.sha256 and pre-remediation-au03.patch. The new full manifest
covers 22 live files plus a separately recorded moved/deleted path. The prior 20
hashes remain unchanged. AU15 remains live and excluded from AU03 patch scope.

reset-remediation.patch SHA-256:
3e556b2862c96b017d21947696cc9aa796c44e91323e2a0f1fdc2bf080fb5a4f

No unresolved implementation finding is claimed; independent recheck is pending.

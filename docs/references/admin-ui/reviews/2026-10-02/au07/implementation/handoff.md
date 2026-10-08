# AU07 implementation handoff — 2 October 2026

Role: implementer/remediator `/root/au07_implementer`, Astra/high. This is self-verification, not independent review. Next owner: orchestrator `/root` assigns one fresh Astra/high reviewer.

Checkout: `/Users/christopher/.codex/worktrees/participants-functionality/BingoWebpage`.
Branch: `codex/participants-functionality`; packaged HEAD `0ec8add9314a65ab66751bf44bbb4f5195ff854f`. All changes remain uncommitted.

## Delivered

- Added successful `CompletedAsOptional` outcome, explicit explanation, normalization flag and immutable `OriginalDefinition` to the existing add-result contract. Ordinary pre-response required additions remain `Completed`/required.
- Reused AU06 durable request identity and the exact question's atomic creation audit. Fingerprint/actor/event validation precedes audit resolution; later definitions and same-label records never substitute. Original required intent plus original optional definition determine normalization without new persistence or migration. Missing, duplicate or corrupt original snapshots fail closed. Removed fields retain the existing Removed outcome with original definition; authorization/visibility remain current and failure results disclose no prior definition.
- Existing JSON responses expose that result; ordinary form feedback explains optional normalization with informational severity and Danish text. Full frontend recovery remains deferred.
- Planner-approved narrow baseline correction: only the ordinary null-to-first-response timestamp transition without any other changed form property or changed question preserves the form version. Explicit Version marks/advances, settings and accompanying question changes retain version invalidation. Existing Serializable event locking and structural/required restrictions remain. No stale-baseline bypass.
- The planner decision was promoted to current product, functional, data, architecture and plan authorities before implementing the version correction. The specifically authorized stale architecture paragraph was reconciled. Current status, delivery row and reference register report implementation/checks and awaiting review.

No table, service, page, route, policy, job, generic receipt, backfill, model/migration change or redesign added.

## Verification and exact evidence

1. `dotnet test tests/Bingo.IntegrationTests/Bingo.IntegrationTests.csproj --configuration Release --no-restore --filter 'FullyQualifiedName~SignupQuestionCreationRetryIntegrationTests' --logger 'trx;LogFileName=au07-focused.trx' --results-directory /private/tmp/au07-implementation-20261002`
   - `focused.log`, `au07-focused.trx`: initial **20 passed / 1 failed / 0 skipped**.
   - Sole failure: simultaneous second signup produces the existing Npgsql wrapped SQLSTATE 40001, while the fixture expected an unsuccessful result. Corrected only the fixture to assert that exact underlying serialization failure and retry in a fresh context. No signup production change.
2. `dotnet test tests/Bingo.IntegrationTests/Bingo.IntegrationTests.csproj --configuration Release --no-restore --filter 'FullyQualifiedName~SimultaneousFirstSignupsRetainFirstMarkerAndDefinitionVersion|FullyQualifiedName~ConcurrentDefinitionAddStillRejectsOldBaselineWithoutPartialEffects' --logger 'trx;LogFileName=au07-contention.trx' --results-directory /private/tmp/au07-implementation-20261002`
   - `contention.log`, `au07-contention.trx`: **2 passed / 0 failed / 0 skipped**; corrected case plus the required concurrent definition-add stale rejection case.
   - **22 distinct passing cases across corrected runs**, not one final 22-case run. No production changes between these runs.
3. `dotnet build Bingo.slnx --configuration Release --no-restore`
   - `build-final.log`: **PASS, 0 warnings / 0 errors**. No production/test changes after this build.
4. Scoped complete before/current comparison and `git apply --check --whitespace=error-all au07.patch` against the captured before tree: PASS. Added-line secret-pattern scan: no matches. All **1,044 inherited non-owned identities unchanged**, including inherited deletions; all owned changes are represented in the 14-file AU07 patch. The tracked/untracked baseline was captured before changes.

Executable boundaries include a real first accepted signup between rendered question editor and add submission with observed PostgreSQL event-lock contention; optional persistence and HTTP result/replay/redirect explanation; no participant backfill; pre-response editor rejection of later required/type changes; simultaneous signups with earliest marker retained after fresh-context retry; marker-only and mixed settings/definition/explicit-version behavior; true concurrent definition change rejects stale add without extra operation/audit/version; pre-response required creation; exact request replay/lost commit response after later edits and same-label add; real nonaligned 100 ns audit timestamp PostgreSQL roundtrip; current authority/visibility/lifecycle, account/system and rejected-write guards; audit failure atomicity and corrupt snapshot fail-closed.

All tests use disposable PostgreSQL 17 Testcontainers, deterministic UTC fixture instants and a controlled validation stub. The deliberate nonaligned audit timestamp rounds through real PostgreSQL to the exact expected microsecond. No provider request, user database mutation or app restart. Existing signup serialization contention can surface as a wrapped 40001 requiring retry; that behavior is preserved, not remediated in AU07.

## Stable review artifacts

All artifacts are under `/private/tmp/au07-implementation-20261002/`:

- `before/`: initial tracked and untracked source snapshots; `before-identities.json` includes missing/deleted paths.
- `current/`: exact 14 owned current snapshots.
- `au07.patch`: complete AU07-only before-to-current diff, including additions to inherited untracked files and the new normalization test partial.
  SHA-256 `cba06fb4096196d67b307253c353c1a7544b244e4510df3c337c649ef63e6e4d`.
- `source.sha256`: 14-file final manifest.
  SHA-256 `93c76d180af2d6044bd01f11a6c86179a85257a5c27372a02daaf3214c4285ba`.
- `protected-inherited-identities.json`, `current-identities.json`, `scoped-checks.json`.

The reviewer should compare this AU07-only patch/current manifest with final DELIVERY_PLAN AU07, not the broad HEAD diff of earlier uncommitted tickets.

## Reconciliation and limits

AU06 precondition/reconciliation was supplied and retained: prior worker completed/idle; planner read the independent report and verified 23 production/test/behavior-doc identities, with only completion metadata differing. Prior reviews were not repeated. The initial baseline-policy blocker was recorded and the orchestrator's minimal callback delivery retained; planner subsequently approved the exact narrow rule implemented here.

No unresolved implementation finding or environment blocker. Fresh independent review is pending. Frontend uncertainty binding, manual UI acceptance and integration/release gates remain deferred. No full suite, browser walkthrough, staging, commit, push, merge or deployment. All narrow worktree write/build/test escalations succeeded; no automatic approval rejection.

# AU08 implementation handoff — stable for independent review

Role: implementer `/root/au08_implementer`, Astra/high. Report to orchestrator `/root` only.
Exact execution checkout `/Users/christopher/.codex/worktrees/participants-functionality/BingoWebpage`, branch `codex/participants-functionality`, packaged HEAD `0ec8add9314a65ab66751bf44bbb4f5195ff854f`. All changes remain uncommitted. No repository edits after this freeze.

## Implemented / verified

- Existing Identity form/page owner now transports original four-field baselines, current reviewed values and explicit KeepMine/UseCurrent choices. Small domain value/comparison records own canonical three-way comparison. Untouched fields retain current values; matching intended/current values are harmless; different same-field edits block the entire save. Original baselines and submitted draft stay immutable on failure; a newer differing reviewed field invalidates explicit resolution. Stale legacy posts without a complete baseline require reload and never silently gain a current Version.
- Existing Serializable transaction re-evaluates current fields and capability immediately before mutation. Event/audit commit atomically; no-op requests do not mutate/audit. Current provider concurrency wrappers, including Npgsql execution-strategy wrappers, return ordinary stale feedback plus current field comparison. No automatic mutation retry or request-success inference.
- Public timezone confirmation separately binds original/proposed zones and UTC timeline consequences at persisted microsecond precision. Schedule-only changes return separate stale feedback and a fresh preview, preserving all intended fields until confirmation. UTC instants never move.
- Existing Identity-only route filter now uses existing ConfigureIdentity capability. This fixes the discovered mismatch where direct handlers/domain allowed Live/Final Review text edits but actual HTTP used the old pre-Live combined capability. No other route/capability behavior changes; permanent first-Live timezone lock remains enforced.
- Existing Identity audit excerpts now budget their escaped JSON length against varchar(4000), retain full name/timezone/slug, cap optional text excerpts at 500 UTF-16 units without splitting runes, and mark truncation truthfully. This fixes a demonstrated persistence failure for otherwise-valid maximum Unicode text; no storage/schema/global audit change.
- Minimal hidden form/result/error transport and four Danish messages. Existing timezone JS retains failed returned editor/drafts when a concurrent update removes the preview, preserving departure guards. No new Use theirs interaction, layout redesign, AU09 readback or uncertainty workflow.
- Promoted contract in existing PRODUCT_REQUIREMENTS, FUNCTIONAL_CONTRACTS 4.3, DATA_MODEL and AU08 DELIVERY_PLAN before production edits. Narrow legacy Identity banner/slug/Live text-lock wording reconciled to the approved simplification target. Additional required integration corrections were recorded in AU08 plan before their code edits.

No service, table, route, policy, job, migration or receipt added. Domain comparison value/result records are the explicitly budgeted small addition.

## Focused execution evidence

1. Initial focused command (Release, no restore):
   `dotnet test tests/Bingo.IntegrationTests/Bingo.IntegrationTests.csproj --configuration Release --no-restore --filter 'FullyQualifiedName~IdentityFieldConflictIntegrationTests|FullyQualifiedName~Slice3CreationIdentityPersistenceIntegrationTests.Identity|FullyQualifiedName~CreationHttpAcceptsOnlyNameAndTimezoneAndCreatesAtomicDefaultAggregate' --logger 'trx;LogFileName=au08-focused.trx' --results-directory /private/tmp/au08-implementation-20261002`
   First compilation stopped at CA1861 in one new test assertion; corrected assertion only, diagnostic retained in `compile-initial.log`. First executable run **13/14 PASS**, one failure identified actual audit varchar overflow for valid Unicode limits (`focused.log`, `au08-focused.trx`).
2. Named correction run:
   `dotnet test tests/Bingo.IntegrationTests/Bingo.IntegrationTests.csproj --configuration Release --no-restore --filter 'FullyQualifiedName~ActualConcurrentClientsMergeDisjointEditsAndRejectSameField|FullyQualifiedName~ActualTransportPreservesUnicodeAndUtf16LimitsAndPermanentSlug|FullyQualifiedName~IdentityTreatsWrappedProviderConflictsAsStale' --logger 'trx;LogFileName=au08-corrections.trx' --results-directory /private/tmp/au08-implementation-20261002`
   **4/4 PASS** (`corrections.log`, `au08-corrections.trx`). Two actual controlled concurrent HTTP cases, valid-limit audit persistence/validation, wrapped provider stale behavior.
3. **14 distinct PostgreSQL 17/authenticated HTTP cases have passing outcomes across those runs**, not a final single 14-case run. The ten new cases cover disjoint/same-field actual concurrent writes; both resolution choices and stale resolution retry; schedule-only review with nonaligned timestamps persisted to microseconds; unchanged/already-matching values and preserved unknown timezone; missing/partial legacy baselines; Unicode/UTF-16 bounds and slug; actual Live/Final Review text/timezone boundaries; non-Admin authorization. Four affected existing cases cover creation-to-Identity HTTP, handler lifecycle/UTC behavior, wrapped provider conflict and retained-timezone fallback. `test-results.json` records exact identities/outcomes.
4. Specifically authorized existing controlled browser transport fixture:
   `NODE_PATH=/Users/christopher/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules /Users/christopher/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/bin/node tests/Bingo.BrowserTests/identity-timezone-confirmation.browser.js`
   **PASS**, including new preview-free error draft retention and departure guard (`timezone-transport.log`). Isolated local fixture/headless Chromium, no application database or user's running app; this is not production visual/manual acceptance.
5. `dotnet build Bingo.slnx --configuration Release --no-restore`: **PASS, zero warnings/errors** (`build.log`).
6. Scoped whitespace/diff and added-content leak checks **PASS**. Only controlled synthetic fixture credentials. All **1047 inherited non-owned path identities unchanged** (`protected-inherited-manifest.json`, `verification.json`).

## Stable review inputs

- Complete AU08-only `au08.patch`, SHA-256 `4c452804ba647e3b35a749bb4baf6fde9ba4f831be5e5cc3272b66508396ea60`.
- `13`-source `source.sha256`, manifest SHA-256 `d74176f8966ff413f0a21b2d8d4269f0f4c81f49099edac75b9865cbec6ffbc5`.
- `before/` and `current/` source snapshots plus `source-identity.json`; new files have absent before state.
- Initial `inherited-baseline.json` captures 1,058 inherited tracked/untracked path identities. `implementation-baseline.json` adjusts only orchestrator-owned AU07 reconciliation/AU08 tracking metadata in CURRENT_STATUS, DELIVERY_PLAN and reference register before AU08 behavior edits. AU08 patch excludes those prior metadata edits.
- `baseline-status.txt`, `scoped-diff-check.txt`, logs/TRX and `test-results.json` preserve the diagnostic and verification chain. No existing AU01–AU07/planner/AU15 work was reverted, staged or overwritten.

## Remaining boundaries / next owner

No known required implementation finding or environment blocker. No independent-review pass is claimed; orchestrator must dispatch the fresh independent reviewer against this exact checkout, complete AU08-only patch and final promoted plan. Worker remains available only for named remediation. CURRENT_STATUS and reference register completion tracking stay orchestrator-owned.

Frontend Use theirs integration, AU09 readback, manual UI acceptance and broader integration/release gates remain deferred. No full suite, production app walkthrough, user database/provider action, app restart/reset, staging, commit, push, merge, deployment or next-ticket work occurred. Initial approval rejection was respected; only read-only preparation occurred until explicit human clearance.

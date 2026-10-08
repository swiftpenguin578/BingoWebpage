# AU05 independent review — PASS

2 October 2026. Reviewer `/root/au05_reviewer`, fresh independent Astra/high reviewer. Report routed only to orchestrator `/root`. Repository remained read-only. No required findings.

## Reviewed identity

- Checkout: `/Users/christopher/.codex/worktrees/participants-functionality/BingoWebpage`
- Branch: `codex/participants-functionality`
- HEAD: `0ec8add9314a65ab66751bf44bbb4f5195ff854f`
- Complete AU05-only patch: `/private/tmp/au05-implementation-20261002/au05.patch`
- Patch SHA256: `7df6134957635f2f1f8e76997e1657ef6d5ce8f515e154b9f06c6d3ab588a183`
- 14-source manifest SHA256: `387426bb3709ae5d8de5d52132381c9ba59e8dad501706e2908bcaa2f3c26a45`
- Implementer handoff SHA256: `8e1779d1e1a5cf49cdab4f8faae06c5eda5c3fe27810d95b99ee8c25875d70bb`

All 14 live source hashes matched at review start and end. Independently reconstructed every changed file by applying the patch's context/add/remove lines to the supplied before snapshots in memory: 14/14 results equal current source. Independently compared the before identity manifest with all inherited non-owned paths: 34/34 unchanged. This review does not attribute the inherited AU01–AU04/planner/AU15 changes to AU05. Exact per-file hashes, reconstruction results and retained test-case outcomes are in `identity-and-evidence.json` alongside this report.

## Scope and verdict mapping

Baseline: current AGENTS workflow, CURRENT_STATUS AU05 handoff, DELIVERY_PLAN AU05 and 4.2.1, PRODUCT_REQUIREMENTS signup-question behavior, FUNCTIONAL_CONTRACTS 4.5 and DATA_MODEL 6.1, plus the explicit assigned scope.

Required scope delivered:

- Custom question add, account-field add, custom edit, account rename and move require submitted `ExpectedFormVersion`; malformed/missing submissions fail closed and current version comparison occurs inside the existing Serializable event-lock transaction before writes. Co-captain enable applies the same check in the existing service, including before the already-active shortcut.
- Existing ordinary Razor forms forward rendered tokens. Delete/disable keep their question-version/current-impact contract; their service behavior was not replaced or expanded.
- Capacity and code results distinguish immutable submitted event version from authoritative saved/current settings. The new snapshot exposes only event version, capacity, waiting-list enabled and code required/present flags. JSON is opt-in on existing endpoints; ordinary redirects remain.
- Capacity and code remain separate transactions. Successful unchanged-capacity saves advance event version as expressly accepted in the assignment. Rollback failures obtain fresh state outside the disposed transaction. Settings operations do not mutate the baseline retained by another operation.
- Serializable isolation remains. The local Questions exception boundary catches only supported concurrency/40001 conditions through provider wrappers after handler disposal, clears tracking and provides the ordinary stale redirect. Unrelated exceptions remain unhandled by this new boundary.
- FirstResponseAt locks/optional normalization, phase and role guards, targeted assignment release, audit integrity and waiting-list promotion remain protected. No answer/impact-count rebuild was introduced.

Required scope missing: none found. Unapproved material additions: none found. Explicit non-goals changed: none found. Unbudgeted tables, services, routes, policies, jobs or generalized abstractions: none. The small snapshot record and local transaction/result helpers directly support the approved result contract; they are not a generic concurrency framework.

No concrete correctness, authorization, security, privacy, concurrency or data-integrity defect was found in the scoped implementation. No optional expansion is required for this verdict.

## Evidence assessment

Reused existing executable evidence; no redundant build, test run, browser or broad audit was performed by this reviewer. Independently parsed all four retained TRX files. There are 25 distinct selected cases, each with a passing latest recorded outcome. This is combined evidence across correction runs, not a claim that a final single 25-case suite ran.

- `au05-focused-compiled.trx`: 17 passed / 4 failed; retained initial implementation/collision-observation failures.
- `au05-corrected.trx`: 13 passed / 1 failed; retained wrapped-40001 failure.
- `au05-conflict-corrected.trx`: 2/2 passed after local wrapper handling correction.
- `au05-settings-final.trx`: 4/4 passed after final unchanged-capacity version correction, including promotion and audit-failure rollback.
- `build-final.log`: Release solution build in the assigned checkout, zero warnings/errors. The earlier build from the attached Documents checkout is excluded.

Reviewed the executable assertions, not just totals. The six-operation theory uses real login/antiforgery/HTTP boundaries and compares complete event/form/question/answer/participant/assignment/audit/notification snapshots for stale, missing and malformed rejections. Current-token requests must advance form version and create the expected audit. Concurrent tests observe actual blocked PostgreSQL sessions before releasing the controlled lock, then assert one winner, ordinary loser response and no extra question/audit; settings assert identical winner/loser authoritative snapshots and committed database version. Separate settings tests cover immutable baselines, stale saves, no secret/hash response, and newer versions on accepted unchanged capacity. Existing tests verify current-impact delete/disable behavior, targeted release, deterministic promotion and rollback of capacity/version/status/audit/notifications.

The new fixture uses fixed UTC time aligned to PostgreSQL microseconds. No timestamp fingerprint contract changed. Tests use disposable synthetic PostgreSQL fixtures; the review made no database, provider or running-app changes.

## Review commands and limitations

All repository commands used the assigned checkout explicitly. Read-only identity commands:

```sh
git status --short --branch
git rev-parse HEAD
shasum -a 256 /private/tmp/au05-implementation-20261002/au05.patch /private/tmp/au05-implementation-20261002/source.sha256
shasum -a 256 -c /private/tmp/au05-implementation-20261002/source.sha256
```

The manifest check ran at both boundaries. Targeted `sed`/`rg` inspections covered all AU05 patch hunks, the current affected handlers/services, active authority sections, test assertions and implementation handoff/logs. A read-only Python parser independently replayed all unified-diff hunks against `before/`, hashed inherited paths using `before.json`, and parsed the `UnitTestResult` elements of the four named TRX files into `identity-and-evidence.json`. Its outcome was 14/14 source matches, 14/14 patch reconstruction matches, 34/34 protected matches and 25/25 latest case passes. Exact implementation test/build commands are retained in the hash-identified implementer handoff; they were assessed, not rerun.

Technical PASS only. New UI binding/per-card uncertainty behavior, AU06 retry identities, AU07 explicit normalization feedback, visual/manual acceptance and final integration/release gates remain deferred. No manual approval, full-suite pass, publication or deployment is claimed. Next owner is the orchestrator for completion metadata and the required planner callback; this reviewer did not contact the planner.

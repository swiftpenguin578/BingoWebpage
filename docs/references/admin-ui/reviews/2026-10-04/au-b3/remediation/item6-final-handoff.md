# Remediation item 6 — final checks, documentation and handoff

Authority: [supplied brief25](supplied-brief.md) item6 / [review24](supplied-review.md) Smaller fixes. Targeted24a L1,24b rounding note,24d N1/acceptance gaps,24e L1/L3/lifecycle bounds,24f R1–R4/N1–N3 were read before their fixes. Verbatim linked reports are preserved beside this file; supplied product decisions and D6 are in [supplied-decisions.md](supplied-decisions.md), linking the actual user-supplied08-decisions source. No independent/self-review or manual acceptance claimed.

## Named corrections

- Register: complete NotRequired/Pending/Succeeded/Rejected/CouldNotUpdate names, target/request times, safe rejection code and FinalReview readiness/publish-result fields in the existing single fallback row. Add always-required Resume replacement end/ceil-minute early-end row, with RC01/OS-1 ownership and open copy decision. Add the RC01 correction entry; no frozen reference or page binding performed.
- Named stale AU20 markers and adjacent contradictory summaries corrected in DELIVERY_PLAN, FUNCTIONAL_CONTRACTS and FUNCTIONALITY_CHANGES. DATA_MODEL explicitly freezes persisted end-status names; compatibility test pins them. Planner technical error classification in DELIVERY_PLAN/TECHNICAL_ARCHITECTURE supersedes the initial resolution. User D6 quote/link says first publication permanently stops retries, including reopen; the WOM basis stays unchanged. Existing implementation retained.
- Existing publication feedback, lifecycle history and existing history-label text explain skipped unmatched-end fallback rather than “no detail”; Danish text added. No new display. Publication tests assert service feedback, stored lifecycle reason and existing history description.
- Six proof gaps now executed: fetch rejects a1-second mismatch and retains prior success; scheduled/manual fetches both resume after success and were suppressed before it; replacement tests set actual Pending/Rejected state and clear all fields; manually seeded external Delete is refused at dispatch before provider access; Final Review replacement is refused;22:00:00.0004 rounds configured end to22:01 with precise actual end.
- Authorized Stats fixture-only correction ends at its configured boundary, inside the normal30-minute grace. Original assertion and all official-history checks retained; no Stats production change.
- Runbook has read-only AwaitingFinalReview count before R-3/restored-candidate execution and immediately before deployment; expected0, otherwise stop for decision. No production access/count/rehearsal/deploy was performed.

## Worker-reported execution (not independently rerun)

```
dotnet test tests/Bingo.IntegrationTests --configuration Release --no-restore --filter 'FullyQualifiedName~Au20|FullyQualifiedName~StatsPass4FiveByFiveObjectivesFinalizationArchiveAndUnfinalizationRetainOfficialHistory' --logger 'console;verbosity=minimal'
dotnet test tests/Bingo.Application.Tests --configuration Release --no-restore --filter FullyQualifiedName~Au20StoredOutcomeContractTests --logger 'console;verbosity=minimal'
dotnet build Bingo.slnx --configuration Release --no-restore
git diff --check
```

PASS: integration63/63, zero failures/skips (whole AU20 set plus the requested Stats case); application2/2, zero failures/skips; Release zero warnings/errors; diff check clean. The application test initially triggered a constant-array analyzer warning-as-error; corrected before the passing run. No whole-solution test-suite pass is claimed. Scoped document checks:12 register rows with5 columns, one fallback row, one Resume row, local links/anchors, source-copy parity, unique valid resource XML and compact CURRENT_STATUS passed. No pending-implementation AU20 markers remain in the named owners.

Previous unaffected populated migration Up/Down/backfill and old AU18 string/numeric published-outcome evidence is retained; this remediation adds no schema or migration. The final AU20 filter re-executes the populated migration tests. Real PostgreSQL exercised timing, concurrency and non-microsecond round trips; controlled HTTP/service doubles only, no live WOM/user-owned database.

## Stop / review range

Six new commits after clean baseline4b9f156:1a47b5e (F1),f441134 (F2),7be4ba2 (F3),a980609 (F4),b2c3e1c (F5), then this item6 commit. Review range `4b9f156..HEAD` after this commit. Each item has its own compact evidence and passing Release/diff gates. Initial review24 remains FAIL until Claude's external recheck; worker execution is not reviewer approval.

Planner `/root`, replacement chat01a10660-cc8a-7843-abb4-6cc1ebbb2bf2 (the brief's old planner ID is retired), owns the next handoff: one external reviewer for1/2/5, one for3/4, direct item6 check. No further implementation dispatched here. No B4/B5, new page display, UI/RC work, rehearsal execution, push, main merge, deployment or production access. Prior R1/R3 and operator migration-count release gates remain. Known out-of-scope notes from review24, such as unresolved website Create recovery and the narrow update/publication timing case, remain deferred.

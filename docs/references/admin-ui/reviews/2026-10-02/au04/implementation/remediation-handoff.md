# AU04 named attention-filter correction — 2 October 2026

Role: same implementer/remediator. Stable correction for same-reviewer recheck;
no independent pass/manual acceptance claimed. No ongoing edits.

Checkout /Users/christopher/.codex/worktrees/participants-functionality/BingoWebpage
Branch codex/participants-functionality; packaged HEAD
0ec8add9314a65ab66751bf44bbb4f5195ff854f. All changes uncommitted.

Required P2: original candidate omitted the reference's agreed authoritative
attention=1 filter. UI control/URL/navigation integration remains deferred, but
backend query state and predicate were required now.

Correction: IndexModel binds `attention` as a GET string and exposes normalized
ActiveAttention, true only for exact value "1". It intersects existing view/phase/
search results with AttentionCategoryCount > 0. PopulationCount remains captured
before phase/search/attention narrowing. No ordinary setup, hidden-access bypass,
new category, changed inbox units or UI toggle/chip/layout/navigation was added.
Existing product/functional/status/E01–E02 documentation now explicitly includes
this existing approved filter and records the review correction.

Extended one existing real PostgreSQL scenario:
AttentionPrioritizesFailuresAndCountsReviewOnceWithoutChangingInboxUnits.
It retains priority/+N/inbox proof and adds active-only output, phase/search/view
intersections, setup exclusion, unchanged population totals, Admin hidden-view
rejection, separate SuperAdmin hidden population with unchanged shared action
exclusion, and attention=0/absent deactivation. The test uses disposable controlled
fixtures and the existing deterministic microsecond-aligned clock.

Command in assigned checkout:
`dotnet test tests/Bingo.IntegrationTests/Bingo.IntegrationTests.csproj --configuration Release --no-restore --filter 'FullyQualifiedName~AttentionPrioritizesFailuresAndCountsReviewOnceWithoutChangingInboxUnits' --logger 'trx;LogFileName=au04-attention-remediation.trx' --results-directory /private/tmp/au04-implementation-20261002`

Exit 0, PASS 1/1, zero skipped. attention-remediation.log and
au04-attention-remediation.trx. This command rebuilt the affected Release Domain,
Application, Infrastructure, Web and integration-test projects successfully.
Previous eight distinct passing query/action cases and final solution Release
build remain applicable to unchanged surfaces; no redundant whole suite, solution
rebuild or browser check added.

Scoped git diff --check and remediation-added whitespace/leak scans PASS in
remediation-scoped-checks.log. All 29 inherited non-owned paths remain identical
to before AU04 (protected-source-corrected.json). No new table/service/job/provider
call, user database mutation, app restart, UI binding, commit/push/deployment.

Evidence identities:
- source-corrected.sha256 (live 12-path manifest):
  c59d2745fe4cc64ff270c465089dddb25358edf443894c0fc6bfcd131399adff
- au04-corrected.patch (COMPLETE AU04-only tracked/untracked diff against before/):
  8b1f0253e61ff740d272911786d33bf04eb1929ce166144f89fa965d84843446
- attention-remediation.patch (only this named correction against before-remediation/):
  ba6722b9fabe48c80b563000651f58696542c36e6a798237abd39e598a788a7d

Original handoff.md, au04.patch, source.sha256, logs/TRX and before/ are immutable
and retained. before-remediation/ captures the exact initial reviewer candidate.
No required finding outside this named omission was implemented or investigated.
Next owner: orchestrator returns correction/evidence to the same reviewer.

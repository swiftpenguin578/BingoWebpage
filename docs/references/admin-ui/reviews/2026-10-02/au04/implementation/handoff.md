# AU04 implementation handoff — 2 October 2026

Role: AU04 implementer/remediator, Astra/high. Implemented and directly checked;
independent review and manual approval are NOT claimed. Stable candidate for the
orchestrator's fresh reviewer. No further source edits in progress.

Checkout: /Users/christopher/.codex/worktrees/participants-functionality/BingoWebpage
Branch: codex/participants-functionality
Packaged HEAD: 0ec8add9314a65ab66751bf44bbb4f5195ff854f
All AU04 changes and inherited AU01–AU03/planner changes remain uncommitted.

## Delivered

- Existing directory query exposes All/Current/Past/Hidden populations, phase/name
  filters, population count before search/phase filtering, column sorts and stable
  ID ties. Default Live first (latest actual start), preparation next by configured
  start/signup fallback ascending and unscheduled last, past across all phases by
  actual end/lifecycle fallback or cancellation time descending and missing last.
- Capacity is nullable. Live/past ParticipantCount is a measured retained value or
  null, with the typed metric reason/availability and import provenance retained.
  Preparation still uses confirmed/waiting data. Cancelled pre-Live/missing actual
  intervals remain unavailable. The narrow method on existing IAdminDashboardService
  reuses BuildPopulations/BuildImportInfo without loading unrelated Dashboard totals.
  Existing Dashboard GetAsync and mapper bodies are unchanged.
- Read authorization rechecks persisted enabled website Admin/SuperAdmin role;
  claimed SuperAdmin alone cannot expose hidden rows. Ordinary populations/counts
  exclude hidden/discarded; hidden is separate and SuperAdmin only. New participation
  read also independently authorizes, filters hidden/discarded, and uses the existing
  repeatable-read requirement. No writes, provider calls, new service/table/job.
- Needs attention prioritizes current start/opening failures, then evidence review;
  category +N counts review queue once. Inbox Count retains per-submission plus
  per-failure units. Only the three existing categories are used; ordinary setup
  and illustrative WOM failures create no extra subsystem or attention.
- Existing product/data/functional authorities promoted, AU04 ticket and E01/E02
  checkpoint/current status updated. AU15 and inherited approvals/history preserved.
- Minimal Razor compatibility change: the two existing capacity interpolation
  arguments now tolerate null as 'Not set'. No layout/URL/navigation binding changes.
  New participant/result bindings and page integration/manual acceptance are deferred.

## Exact executable checks

All commands ran in the assigned checkout using narrow approved escalation for
its out-of-default-roots writes/builds. Tests use only disposable PostgreSQL 17
Testcontainers and controlled synthetic accounts. No user database was accessed.

1. `dotnet build Bingo.slnx --configuration Release --no-restore`
   Initial exit 1 in build-initial.log: existing Razor formatter passed nullable
   capacity into non-nullable localization arguments. Minimal two-argument fix above.

2. `dotnet test tests/Bingo.IntegrationTests/Bingo.IntegrationTests.csproj --configuration Release --no-restore --filter 'FullyQualifiedName~EventsDirectoryIntegrationTests|FullyQualifiedName~AdminActionProjectionIntegrationTests' --logger 'trx;LogFileName=au04-focused-initial.trx' --results-directory /private/tmp/au04-implementation-20261002`
   Initial exit 1 in focused-initial.log: new test-only AuditEntry constructor
   argument order, corrected before executable tests. No TRX was generated.

3. `dotnet test tests/Bingo.IntegrationTests/Bingo.IntegrationTests.csproj --configuration Release --no-restore --filter 'FullyQualifiedName~EventsDirectoryIntegrationTests|FullyQualifiedName~AdminActionProjectionIntegrationTests' --logger 'trx;LogFileName=au04-focused.trx' --results-directory /private/tmp/au04-implementation-20261002`
   Exit 1: 7 PASS / 1 FAIL / 0 skipped, focused.log and au04-focused.trx.
   All four existing action tests and the new directory ordering/hidden/attention
   cases passed. RetainedParticipationReusesDashboardRulesAndHonestImportAndMissingCoverage
   failed before query execution because controlled teams shared a name within the
   same event, violating IX_teams_event_id_name. Fixed only fixture names to be unique.

4. `dotnet test tests/Bingo.IntegrationTests/Bingo.IntegrationTests.csproj --configuration Release --no-restore --filter 'FullyQualifiedName~RetainedParticipationReusesDashboardRulesAndHonestImportAndMissingCoverage' --logger 'trx;LogFileName=au04-retained-corrected.trx' --results-directory /private/tmp/au04-implementation-20261002`
   Exit 0: 1 PASS / 0 failed / 0 skipped. retained-corrected.log and
   au04-retained-corrected.trx. No production changes after initial executable run.
   Combined direct evidence: all 8 distinct selected tests pass; not a fresh 8/8 run.

5. `dotnet build Bingo.slnx --configuration Release --no-restore`
   Final exit 0, PASS, zero warnings/errors. build-final.log.

6. Scoped `git diff --check -- <12 owned paths>` plus AU04-added-line whitespace
   scan PASS (diff-check.log). AU04-added private-key/provider-token/credential-URL
   scan PASS (leak-check.log). 29 pre-existing non-owned paths, including every
   inherited changed code/test source, hash-identical to before AU04
   (protected-source-check.json). Baseline captures deleted source identity too.

The four new real-database scenarios cover all lifecycle phase groups, unscheduled
and null data, deterministic ID ties after real sub-microsecond timestamp truncation,
name/phase/current/past filters and population totals, nullable participant sorts,
retained departures/team moves/pre-Live departure/future membership, import count
and capacity availability, actual Dashboard result parity, invalid actual interval,
stale/forged role claims, member/anonymous/disabled denial, SuperAdmin hidden-only
view and ordinary exclusion, current failure priority +N and unchanged inbox units.
Existing action regressions retain stale schedule/lifecycle failure removal and
hidden notification exclusion. No full suite/browser walkthrough was run.

## Review identity and complete diff

All paths relative to checkout; source.sha256 contains live hashes for 12 owned
files (six source/test, six authority/status). au04.patch is the COMPLETE AU04-only
diff, including the untracked new test, against captured before/ sources, NOT a
whole-branch diff that would mix inherited work. before.json contains original
hashes and absent/deleted identities; initial-status.txt/initial-head.txt and
final-status.txt preserve checkout evidence. owned-paths.json lists exact scope.

source.sha256 SHA-256: c3cbcce29cf80348d1775ecc90f9ca9c8b252878eeca59654a2915c1d784f11b
au04.patch SHA-256: bef67b74e95d432097c3891ee6be34ad93290a3c92aea8f17a43593c19f79145

No unresolved implementation finding or environment blocker. Next owner is the
orchestrator, which assigns the fresh independent reviewer using this patch and
live manifest. Implementer remains available for named remediation. No staging,
commit, push, merge, deployment, app restart, provider/user database operation,
manual acceptance, next-ticket work or planner messaging occurred.

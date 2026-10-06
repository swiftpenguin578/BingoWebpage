# U2 item 1 — Dashboard backend, checked checkpoint

Base: item0 `edd7bbcd825a9ab6a34864502faa1205c47c1dbd`; assigned initial SHA
`8355680a4eee6a74ae905c5c69a8f50c5f021dcf`. Item1 checkpoint awaits Claude review.

## Implemented candidate, not executed proof

- DB-2: expected EHB assignments now follow the WOM producer's unreleased Playing /
  Confirmed population. Strict fingerprint, generation and competition matching
  retained; no WOM producer changes.
- DB-3: unavailable imported coverage no longer poisons measured platform coverage;
  reconstructed rows remain excluded. Existing post-import measured rows remain
  included per PRODUCT_REQUIREMENTS “Approved submissions exclude reconstructed
  imported contributions” and the existing remediation test expecting 3.
- DB-4: statistics provisional count/flag and latest-contribution identity; recap
  state/provisional/import fields; chart/history import flags; first platform cohort
  tracking-start with unavailable returning classification; latest contribution
  selected using the chart's stable chronology.
- DB-5: card next-date kind/value/timezone.
- U2-1: team counts on chart/history/recap.
- D-15: Index reads IAdminDashboardService and maps UnauthorizedAccessException to
  Forbid. S14 old operations queries and inert markup removed; temporary visible WIP
  remains until item2. No completed Dashboard UI claimed.
- Only the brief-authorized AuditPresentationTests assertions were removed: old
  `/Admin` fetch and equality to recent audit (both EN/DA cases). All Audit-page
  readability/privacy/reason assertions retained. A10 before/after: former test
  lines 57–62 and 164–167 compared Dashboard recent audit to Audit; now Audit alone.
- Added U2DashboardIntegrationTests for actual WOM fingerprint with removed and
  unplaced Confirmed participants, strict stale refusal, mixed import/platform
  counts plus pending/rejected exclusions, recap/latest identity/tie/tracking/team
  metadata and four phase dates. Six passed in the focused run; mixed history
  initially failed its duplicate-RSN fixture, corrected to two distinct synthetic
  RSNs and passed in a scoped rerun. No product assertion was changed.

## Exact check results

`dotnet build Bingo.slnx --configuration Release --no-restore`: exit 1,
0 warnings / 6 errors, elapsed 64.67 seconds. Missing IndexModel members in
`tests/Bingo.IntegrationTests/UiReviewScenarioIntegrationTests.cs`:
ActiveEvents (:125), AttentionEvents (:126), LifecycleReadiness (:127),
PendingEvidence (:128), UpcomingMilestones (:129), RecentAudits (:131).

On resume the first test build reported three CA1826 errors on First/Last calls in
the new test. Corrected to direct indexing; subsequent Release test compilation
succeeded. No clean Release solution gate claimed.

Executed focused command:

```sh
dotnet test tests/Bingo.IntegrationTests/Bingo.IntegrationTests.csproj --configuration Release --no-restore --filter 'FullyQualifiedName~U2DashboardIntegrationTests|FullyQualifiedName~AdminDashboardIntegrationTests|FullyQualifiedName~AdminDashboardRemediationIntegrationTests|FullyQualifiedName~EventsDirectoryIntegrationTests|FullyQualifiedName~UiReviewScenarioIntegrationTests' --logger 'trx;LogFileName=u2-item1-focused.trx'
```

Exit1: **26 passed / 3 failed / 0 skipped**, 29 total, displayed duration 56s.
Existing Dashboard classes **16/0/0**; EventsDirectoryIntegrationTests **4/0/0**
and unchanged. New Dashboard tests **6 passed / 1 fixture failure**. Both UR
profiles reached their unchanged helper and failed at :369 requiring discarded
audit in the absent Dashboard section. That establishes the new authorized block's
service/HTTP assertions ran successfully for both roles in both profiles, not a
pass of either full UR scenario or the future item2 rendering.

Duplicate-RSN fixture correction rerun:

```sh
dotnet test tests/Bingo.IntegrationTests/Bingo.IntegrationTests.csproj --configuration Release --no-restore --filter 'FullyQualifiedName~U2DashboardIntegrationTests.MixedHistoryHeadlineExcludesReconstructionPendingAndRejectedWhileImportOnlyIsUnknown' --logger 'trx;LogFileName=u2-item1-mixed-correction.trx'
```

Exit0: **1 passed / 0 failed / 0 skipped**, displayed duration 14s. Passing earlier
evidence reused; no broad repetition. Raw repository-local ignored TRX files:
`tests/Bingo.IntegrationTests/TestResults/u2-item1-focused.trx`, run ID
`c61ee1ae-bca3-467f-8386-be4a92cf4f3f`, SHA256
`e173d77218ea0bbd24f11425edc7c01ac2ab37502ab94054126fc56c27527ba0`;
`u2-item1-mixed-correction.trx`, run ID
`4f76b2b0-5ced-41b8-9e44-bea8ff250fe1`, SHA256
`9584edea61981e9212809ca4a12b697f03b4de02b97d178d702346c6bfee4b32`.

`git diff --check`: exit0. Frozen references/tokens/components and protected
EventsDirectoryIntegrationTests have no diff from item0. No JS/browser gate,
whole BrowserTests or whole .NET suite claimed. D-15 HTTP role-loss proof and
remaining item1 checks still required; item1 is not complete or committed.

## Required planner decision

Brief67 protects current tests except explicitly authorized markup rewrites
(inventory42b §8 identifies AuditPresentationTests). UR's accepted test lines
123–131 still instantiate the old Dashboard model and assert no discarded event
in its five retired operations collections, then explicitly require the discarded
audit in Dashboard RecentAudits. S14 explicitly removes those outputs and queries.
Keeping empty compatibility properties would leave obsolete API and would not
satisfy the positive audit assertion; querying retired audit would violate S14.

Initial block rewrite authorized by **A10 + S14, ruling 68** and the user's resume
message. Before: original :123–131 inspected retired operations collections and
required discard audit in RecentAudits. After: the same block reads the real
IAdminDashboardService for ReviewAdmin/ReviewOwner, excludes discarded identity
from history/chart/recap/card/latest contribution, verifies EventsHeld and the
participation headline against chart populations, logs in both roles, checks
discarded name/ID absent from rendered `/Admin`, and asserts the retained discard
entry in rendered `/Admin/Audit?eventId=...`. Original :105–107 unchanged;
discardAudit lookup unchanged; AssertPrintedUrlsAsync call/body unchanged.

The unchanged helper has a second contradictory assertion: current
UiReviewScenarioIntegrationTests.cs:366–373 (baseline :355–362) extracts the
Dashboard section `aria-labelledby="recent-audit-heading"`, requires the
discarded name and audit ID there, then removes that section before checking
Dashboard exclusion. S14 requires that section to be absent. The helper already
checks rendered filtered Audit list, its entry link and authorized detail for
both roles at current :388–395. Ruling68 §2 says “Nothing else in the UR test
changes”; the user's resume also explicitly keeps AssertPrintedUrlsAsync unchanged.

### A10 + S14, ruling 68 addendum

User authorized the scoped helper correction. Before: `/Admin/Index` required
discarded audit in the recent-audit section and absence elsewhere. After: the
generic assertion requires discarded name absent everywhere on the rendered
Dashboard. The output sentence describes Audit retention and Dashboard exclusion.
Every actual Audit list/detail, authorization, hidden-event, cookie/session and
printed-URL assertion is unchanged. No additional obsolete assertion found.

Final focused continuation: same UR command with filter
`FullyQualifiedName~UiReviewScenarioIntegrationTests`, logger
`trx;LogFileName=u2-item1-ur-addendum.trx`: **2 passed / 0 failed / 0 skipped**,
exit0, displayed duration19s. Sandbox MSBuild pipe denial prevented the first
attempt before tests; changed environment to approved escalation and terminated
only the identified failed attempt PID81473 after replacement passed.

HTTP command: `dotnet test tests/Bingo.BrowserTests/Bingo.BrowserTests.csproj
--configuration Release --no-restore --filter
'FullyQualifiedName~U2DashboardHttpTests|FullyQualifiedName~AuditPresentationTests|FullyQualifiedName~AccessBoundaryTests.AnonymousVisitorIsRedirectedFromAdminPages'
--logger 'trx;LogFileName=u2-item1-http.trx'`: **10/0/0**, exit0. Real authenticated
HTTP pipeline with the read boundary refusing after cookie validation redirects
to `/Account/AccessDenied` (Forbid), not500; anonymous Admin redirects to Login;
retained Audit EN/DA presentation checks pass.

Release solution build `dotnet build Bingo.slnx --configuration Release
--no-restore`: exit0, **0 warnings/errors**, elapsed1.30s. History ordering
`dotnet test tests/Bingo.Application.Tests/Bingo.Application.Tests.csproj
--configuration Release --no-build --filter FullyQualifiedName~DashboardHistoryOrderingTests`:
**2/0/0**. `git diff --check` and frozen reference/CSS protection: passed.
Earlier passing PostgreSQL evidence reused after only fixture/helper changes.

Items2–5 not started at this checkpoint. No self-review, worker, push, merge or deployment.
Dashboard and Events remain awaiting Claude review, then user visual acceptance;
binding is incomplete. Next action: scoped local item1 commit, then item2 Dashboard
binding. Whole batch gates remain required at item5; full .NET final-SHA gate is
Claude's background run. No product/reference question remains for item1.

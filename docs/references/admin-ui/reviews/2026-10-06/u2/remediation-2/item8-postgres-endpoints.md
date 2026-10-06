# U2 brief72 item8 — PostgreSQL endpoint ownership and stop-boundary handoff

Parent: 71a5e6a87aa80301a0103620a81d98584c828564. Implementer checks only,
not independent review or manual acceptance.

## Cause established before changing infrastructure

On Docker Desktop 29.6.1, the original Testcontainers 4.14 wildcard publication
(0.0.0.0 and ::) and an explicit 127.0.0.1 publication can own the same numeric
host port. The UI review database uses the latter. Testcontainers' inside-container
pg_isready does not establish which PostgreSQL the host connection reaches.

Controlled, disposable synthetic containers demonstrated two different
pg_control_system system identifiers, initialized credentials and the same host
port. IPv4 with the wildcard test container's credentials reached the loopback
review-style server and returned 28P01. IPv6 reached the intended wildcard server.
This is an endpoint collision, not late credential initialization, a shared
template race, or missing readiness polling. Both historically failing classes
use dedicated containers, not the shared template.

Diagnostic port56220: review-style system7693690982281875490,
wildcard system7693691034405466146. Native queries and host Npgsql queries
distinguished the servers; 127.0.0.1 plus test credentials failed28P01,
::1 plus those credentials succeeded. All diagnostic containers disposed.

The durable deterministic probe repeated the failure after the fix:
review-style port56557/system7693693337781235752; original wildcard publication
same56557/native system7693693344115077160; explicit IPv4 test credentials
REPRODUCED28P01. Fixed automatic loopback allocation chose56564 and reached
its own system7693693350635409448. Exit0; all probe containers disposed.
Historical failed containers are gone: their particular IDs/port maps cannot
be reconstructed. This establishes the exact recurring failure mechanism under
the documented concurrent review environment, not a recovered historical port map.

Primary implementation details checked before the fix:
[Testcontainers PostgreSqlBuilder4.14](https://raw.githubusercontent.com/testcontainers/testcontainers-dotnet/4.14.0/src/Testcontainers.PostgreSql/PostgreSqlBuilder.cs)
and [PostgreSqlContainer4.14](https://raw.githubusercontent.com/testcontainers/testcontainers-dotnet/4.14.0/src/Testcontainers.PostgreSql/PostgreSqlContainer.cs).
Wildcard publication and inside-container readiness are distinct from host
endpoint ownership.

## Fix / preserved scope

- tests/PostgreSqlTestEndpoint.cs: native local Docker publishes random PostgreSQL
  ports explicitly on127.0.0.1 and connects on that same address; both changes
  are required. Remote Docker/DinD publication remains on its existing path.
- tests/PostgreSqlTestDatabase.cs: shared builder and readiness use this endpoint.
  Existing60-second readiness deadline,250ms interval, template, lifecycle,
  concurrency and credential checks unchanged.
- Existing25 dedicated exception files plus BrowserTestApplicationFactory use
  the same builder/connection helper. Mechanical protection check: all26 files
  normalize byte-for-byte to the parent after removing the helper additions and
  reversing the connection method rename. No old assertions or fixtures changed.
- Integration/Browser/parity fixture projects compile-link the one shared helper;
  parity fixture uses it too. No production code, schema, authorization, CI shard
  selection, test parallelism, retry or timeout changes.
- New PostgreSqlEndpointIsolationTests starts two owned databases concurrently,
  verifies published/connect address, different ports, host/native server
  identity, distinct credentials and an exact wrong-password28P01 rejection.
- scripts/probe-u2-postgres-endpoints.cs is the deterministic Docker Desktop
  reproduction; scripts/check-u2-postgres-concurrency.cjs is the three-run
  concurrency gate driver with no failed-class retry.

Initial probe authoring tried an artificially forced occupied loopback port:
container startup timed out, not a test-body authentication failure. That branch
was removed; the final positive proof uses production's automatic random allocation.
No timeout increase or retries were introduced. Initial compile caught nullable
HostConfig access; corrected with a strict missing-binding exception.
The running driver's console-only elapsed counter printed0; source corrected
to use its stored start instant. No suite assertion/timing changed.

## Executed item8 checks

- Real PostgreSQL focused endpoint plus the two previously failing classes:
  **7 passed /0 failed /0 skipped**.
  TRX start2026-10-07T00:57:37.1378380+02:00,
  finish2026-10-07T00:57:55.1671020+02:00; elapsed18.029264s.
  Command:
  `dotnet test tests/Bingo.IntegrationTests/Bingo.IntegrationTests.csproj --configuration Release --no-restore --filter "FullyQualifiedName~PostgreSqlEndpointIsolationTests|FullyQualifiedName~Slice3LifecyclePersistenceIntegrationTests|FullyQualifiedName~Slice8Pass81PersistenceIntegrationTests" --logger "trx;LogFileName=u2-rem2-item8-endpoints.trx"`.
  Exact TRX: /Users/christopher/.codex/worktrees/participants-functionality/BingoWebpage/tests/Bingo.IntegrationTests/TestResults/u2-rem2-item8-endpoints.trx.
- Deterministic probe command:
  `dotnet run --project /private/tmp/bingo-u2-endpoint.3qraV9/EndpointProbe.csproj --configuration Release -p:BuildProjectReferences=false`.
  Scratch project links the durable probe and references Integration; exit0,
  original28P01 and fixed distinct endpoint/server identity proved.
- `python3 scripts/test-ui-review.py`:13 passed,0 errors,0.479s.
  Initial sandbox socket-bind PermissionErrors were an environment check failure;
  the authorized native-host run passed. No product assertion weakened.
- Native parity fixture Release refresh:0 warnings/errors,2.43s.

## User-directed suite stop — 7 October2026

Latest instruction: "Do not run the entire suite, ill have the planner do it.
Wrap up if youre done." Whole .NET suite was **NOT RUN**, delegated to planner;
there is no final-SHA whole-suite TRX, count or timing to report.

Concurrent whole-Integration run1 was started on the uncommitted item8 candidate
(parent71a5e6a), alongside `python3 scripts/ui-review.py create live`.
Review creation completed successfully and rebuilt36 events/11 synthetic accounts,
app5310/reference5320. The Integration run was explicitly stopped at user direction:
verified driver/test/vstest/testhost PIDs32132/32163/32176/32178 receivedTERM;
driver exit143 and read-only process check confirmed all gone.
Last progress observed529 passed lines; this is NOT a completed suite count.
No completed TRX exists. Runs2 and3 **NOT RUN**. Concurrency gate is **INCOMPLETE**,
not passed; no failed class was rerun and no final-suite result inferred.
Transient logs:
/Users/christopher/.codex/worktrees/participants-functionality/BingoWebpage/artifacts/u2-rem2-integration/run-1/integration.log
/Users/christopher/.codex/worktrees/participants-functionality/BingoWebpage/artifacts/u2-rem2-integration/run-1/ui-review.log.
Review environment remains available for user visual inspection.

## Final scoped gates

Shared design system1–6 rerun; exact commands, exits and line counts in
[item8-design-checks.json](item8-design-checks.json):
1. Both frozen CSS copies byte-identical; no baseline change to the four frozen
   files or Dashboard/Events reference HTML.
2. Page CSS:7 selected lines, only table layout/token skeleton radii;
   forbidden color/font/shadow/literal-radius search has0 matches.
3.20 inline lines are reference/dynamic geometry; color/font/spacing search0 matches.
4.50 shared component/partial markup matches; no new shared component owner.
5.3 page timing/busy lines: search debounce and shared Create busy; shell owns
   single150/400 policy and AdminFetch.
6.34 lifecycle/abort/disposal/draft/shared-fetch lines; executable JS gate below.
git diff --check exit0.

Items1–7 retained applicable checks: Events26/0, Create10/0, Dashboard18/0 and
UR4/0 in Chromium/WebKit, no loaded-reference differences; exact paused-clock
Events14 connected cases and shell12+5 timing cases both engines.
Q-H1 item5 proof16/0 covers390/861/1280/1440, normal/narrow cards, loaded/loading
reference equality and all allowed flow, with durable paired position records.
No production page changes after these passing proofs.
Item6 real PG10/0/0, item7 real PG2/0/0; focused HTTP/markup results per-item.

Final JavaScript runner: **63 passed /0 failed /63 total**, exit0:
35 default executions,14 Chromium,14 WebKit; summed execution time485.215s.
Command: `env BINGO_ADMIN_STALE_EVIDENCE_DIRECTORY="/Users/christopher/.codex/worktrees/participants-functionality/BingoWebpage/artifacts/js-fixtures" "/Users/christopher/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/bin/node" scripts/run-browser-tests.cjs`.
Durable per-execution exits/timings: [item8-js-results.json](item8-js-results.json).
This reran actual Create/Dashboard/Events/UR browser checks and rendered parity in
both engines on the item8 candidate; no production edits followed. Q-H1's16-record
proof remains applicable (the final candidate did not change page geometry).

Clean Release: `dotnet clean Bingo.slnx --configuration Release -v:minimal`
exit0, then `dotnet build Bingo.slnx --configuration Release --no-restore -v:minimal`
exit0, **0 warnings /0 errors**,12.40s.
Initial clean invocation incorrectly included --no-restore, unsupported for clean:
MSB1001 before execution; corrected CLI options, not a source/build failure.
Final diff check and frozen baseline check exit0. Concurrency driver Node syntax0.
All26 mechanical fixture files still normalize exactly to the parent.

Whole .NET BrowserTests was not rerun as a separate final gate; the existing
focused HTTP/markup checks and full paired JS evidence are recorded, not a new
whole-Browser .NET pass. No whole .NET or three concurrent Integration pass claimed.
Exact per-item commit/file inventory: [changed-files.json](changed-files.json).
Remote Docker, DinD and Linux GitHub Actions unrun. No product/reference question
remains open. Both UI_PAGE_MATRIX rows await Claude review, then user visual
acceptance. Next permitted: planner-owned remaining whole-suite/concurrent gates,
independent Claude review and user inspection. No U3+/packaging/publication.

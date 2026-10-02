# AU10 stable implementation handoff — 2 October 2026

Implementer/remediator `/root/au10_implementer`, Astra/high, returning to `/root`. Exact checkout `/Users/christopher/.codex/worktrees/participants-functionality/BingoWebpage`, branch `codex/participants-functionality`, packaged HEAD `0ec8add9314a65ab66751bf44bbb4f5195ff854f`. Changes are uncommitted. Independent review is pending, not claimed.

## Delivered

Before material code changes, reconciled only FUNCTIONAL_CONTRACTS 4.4 with already approved pre-first-Live start/end editing through running/paused/finalized draft and overdue repair. Scheduler/readiness behavior unchanged. Added the narrow approved preservation/readback integration contract and its deferred UI limits.

Schedule POST checks the submitted version as before, then retains the authoritative original timestamp for every unchanged minute display or locked field. It parses only changed editable wall times through the existing five-minute/DST checks. This preserves seconds/microseconds and either valid repeated-hour original instant without accepting changed ambiguous/nonexistent times. Existing capacity carry-through, enabled-overdue automatic opening exception, disabled legacy flag, authorization/serializable service/version/audit path, overlap/WOM checks and scheduler remain unchanged.

Existing lifecycle errors now map to suitable ModelState fields without replacing validation or changing the service result. Ordering/future/missing-time and linked WOM errors target the appropriate fields; overlap, stale and authorization remain form errors. Confirmation preview is selected by the actual confirmation error, preventing other lifecycle errors being misclassified as confirmation. Hidden confirmation/reason fields have visible validation spans.

The existing Schedule route has a no-store Current GET handler under its existing Admin/visibility/lifecycle filters. A single query reads the event and draft state. It returns five exact UTC instants, capacity, automatic-opening state, string version (avoids JS integer loss), timezone, event/draft phase and per-time-field editability. It performs no mutation/audit/receipt operations.

The shipped event-schedule.js exposes a small frozen readback session. The caller supplies the observed full baseline and the full resolved UTC submission before dispatch; both are copied/frozen independently of the mutable UI draft. Comparisons preserve all seven .NET fraction digits rather than JS Date precision. GET-only checks return upToDate/unchanged/different/unknown with current version/context metadata. Different precise instants or automatic-opening values do not match. Matching current values carry no request attribution, including after another Admin writes identical values. Failed, unauthorized, redirected, malformed, wrong-event or regressed-version reads return unknown while the original session/draft remains intact. No mutation retry, rebase, receipt system or general service was introduced.

## Focused executable evidence

- `dotnet test tests/Bingo.IntegrationTests/Bingo.IntegrationTests.csproj --configuration Release --no-restore --filter 'FullyQualifiedName~SchedulePrecisionIntegrationTests' --logger 'trx;LogFileName=au10-focused.trx' --results-directory /private/tmp/au10-implementation-20261002`: PASS 12/12, 37 seconds; `focused.log`, `au10-focused.trx`.
- Real authenticated Razor GET/form POST/Current GET and disposable PostgreSQL: both repeated-hour offsets; all unchanged instant preservation; input with 100ns remainder and exact microsecond-normalized persisted assertions; legacy disabled opening; retained enabled overdue opening without close and overdue pre-Live start; changed invalid/ambiguous/off-step wall times; lifecycle field mapping versus overlap/stale form errors; applied/discarded response, unapplied, competing identical and one-microsecond-different state; no-write repeated reads; Admin/hidden/Live/FinalReview boundaries.
- Added the visible hidden-field spans and extended only the affected Live-path test, then ran `--filter 'FullyQualifiedName~ReadbackRetainsAdminVisibilityAndPhaseBoundaries' --logger 'trx;LogFileName=au10-live-field-errors.trx'`: PASS 1/1, 7 seconds; `live-field-errors.log`, `au10-live-field-errors.trx`. This explicitly checks rendered confirmation and reason errors. Other passing scenarios were not rerun.
- Bundled Node executed `tests/Bingo.BrowserTests/schedule-readback.transport.js`: PASS; `transport.log`. Shipped script against controlled localhost HTTP, real fetch requests, simulated dropped mutation response, unchanged/matching/different microseconds/auto-opening, versions above JS safe-integer range, phase/timezone/editability changes and failed/denied/redirected/malformed reads. Exactly one fixture mutation POST; session recovery uses GET only. Immutable original baseline/full intent and mutable draft retention asserted.
- `dotnet build Bingo.slnx --configuration Release --no-restore`: PASS, zero warnings/errors; `build.log`.
- Scoped diff/whitespace, added-content leak and inherited-source preservation checks: PASS; `scoped-checks.json`. No production infrastructure/domain/application/security/scheduler/migration file changed. Existing prior-ticket evidence was not rerun.

## Exact stable review identity

Seven owned files (also listed in `source.sha256`):

1. FUNCTIONAL_CONTRACTS.md
2. src/Bingo.Web/Pages/Admin/Events/Schedule.cshtml.cs
3. src/Bingo.Web/Pages/Admin/Events/Schedule.cshtml
4. src/Bingo.Web/wwwroot/js/event-schedule.js
5. tests/Bingo.IntegrationTests/SchedulePrecisionIntegrationTests.cs
6. tests/Bingo.IntegrationTests/SchedulePrecisionIntegrationTests.Cases.cs
7. tests/Bingo.BrowserTests/schedule-readback.transport.js

`au10.patch` is the complete AU10-only diff against the inherited dirty snapshot, not HEAD. SHA-256 `ffe0b4b63de857abe1eb769e291f07262416e043e3e535fa8e71e3a17b3b6c2b`.
`source.sha256` manifest SHA-256 `e4592b7929f4dc78869ff557e8351532e08c824055ed475c6d0f186278c8bc79`.
Initial inherited snapshots are in `baseline/` and `baseline-manifest.json`; final owned snapshots in `current/`; exact checkout/source hashes in `source-identity.json`; test counters in `test-results.json`.

All non-owned inherited source hashes remain unchanged except orchestrator-owned CURRENT_STATUS.md and DELIVERY_PLAN.md tracking metadata. Planner-added CLAUDE.md is preserved and excluded. Inherited deletion of src/Bingo.Web/Events/EventSlugGenerator.cs is preserved. No reference-picture browsing or reference source modification.

## Limits and next owner

Backend and readback transport are integrated at the approved boundary. Ordinary Schedule form enhancement/uncertain-save UI, picker binding, stay-on-Schedule success behavior, new confirmation table/navigation and reload/navigation session persistence remain deferred. The existing form redirect and single confirmation remain. The module expects full resolved UTC intent from a future caller; it does not convert local draft strings or bind the present form to a new mutation transport. Matching is current-state evidence only. Controlled lost-response/read-failure tests are not production outage or browser/manual-acceptance proof. Terminal or hidden lifecycle redirects remain governed by existing filters and therefore read as Unknown to the transport when unavailable.

No full suite, visual/manual acceptance or broader release gates claimed. No stage/commit/push/merge/deploy, app stop/restart/reset, user database/provider mutation or next-ticket work. Disposable fixtures only. Initial pre-pause write had OS PermissionError before changing any file; explicit resume authorized normal require_escalated writes, which succeeded. No automatic approval-review rejection occurred.

Next: orchestrator dispatches the fresh independent Astra/high reviewer against this exact stable patch/checkout and final AU10 approved scope. Same implementer is available only for named remediation. All execution sessions have completed. After AU10 completion/blocker report, stop; no AU11 work or preparation.

# U3 item1 — Schedule first working binding; review environment blocked

Schedule implementation and scoped checks are complete. Manual acceptance remains pending. Item2 has not started. Base commit is `f8744fc2a914dbc2fc5e9e4419f71df73139b3dc`; prior completed U3 commits are `b790906` (Identity assertion), `c94b2e4` (item0) and `f8744fc` (count-summary follow-up).

## Implemented

Reference three-section form, shared picker, field errors and overlap banner, published before/after table with UTC offsets/upload cutoff, Live end reason (2,000 max), dirty outside-click protection, stale merge/Use theirs, exact immutable full-tuple readback, uncertain Check again and session-loss draft retention. Saves stay on Schedule. Context changes refresh server-rendered locks while preserving still-editable entries. OS-5 removes capacity from page/readback; OS-4 reconciles the dead first-public decrease rule. D17 enables terminal GET/Current with read-only banners; D16 POST gate and test14 method remain unchanged (hash in JSON).

The approved summary is exactly “Times are in ‹timezone›; change it on Identity. Open, close or start by hand on Overview.” Both links remain, Danish is translated. This resolves the earlier summary decision checkpoint. Shared Q6 has no exception.

## Exact checks and results

`node` below is `/Users/christopher/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/bin/node`.

- `dotnet build tests/AdminDesignParityFixture/AdminDesignParityFixture.csproj --no-restore -c Debug`: PASS,0 warnings/errors.
- `BINGO_PARITY_CONFIGURATION=Debug PLAYWRIGHT_BROWSER=<chromium|webkit> node tests/Bingo.BrowserTests/admin-design-schedule.browser.js`: PASS10 groups each. Real PostgreSQL-backed saves/stale writes; lost-response readback; login loss; Live reason/outside-click retention; DST gap/fold conversion; short viewport/focus/history; EN/DA summary measurements. Initial test-harness route/transition/query/scrim mistakes were corrected; these are final passing runs.
- `BINGO_PARITY_CONFIGURATION=Debug BINGO_CONFORMANCE_PAGES=schedule PLAYWRIGHT_BROWSER=<chromium|webkit> node scripts/check-admin-page-conformance.cjs`: PASS5 widths each plus frame/style, no-fade, document, update/node identity/focus/scroll and Danish checks.
- `PLAYWRIGHT_BROWSER=<chromium|webkit> node tests/Bingo.BrowserTests/admin-design-shell.browser.js`: PASS both; existing shared layer/history/default confirmation behavior retained.
- `node tests/Bingo.BrowserTests/schedule-readback.transport.js`: PASS exact fractions/large versions, immutable tuple, failed/malformed/session-loss reads, context changes, no request attribution.
- `node tests/Bingo.BrowserTests/event-create-datetime.test.js`: PASS exit0; existing Overview/Create adapter preserved.
- `dotnet test tests/Bingo.Domain.Tests/Bingo.Domain.Tests.csproj --no-restore -c Debug --filter 'FullyQualifiedName~PreDraftParticipantCapCanChangeAfterPublicExposure'`: PASS1/1.
- `dotnet test tests/Bingo.BrowserTests/Bingo.BrowserTests.csproj --no-restore -c Debug --filter 'FullyQualifiedName~EventCreationUiTests|FullyQualifiedName~AdminShellUiTests'`: PASS30/30.
- Original scoped PostgreSQL command: `dotnet test tests/Bingo.IntegrationTests/Bingo.IntegrationTests.csproj --no-restore -c Debug --filter 'FullyQualifiedName~SchedulePrecisionIntegrationTests|FullyQualifiedName~Slice3ScheduleLifecycleIntegrationTests|FullyQualifiedName~TerminalEventRoutesRejectEveryAuditedAdminMutationBeforeAnySideEffect|FullyQualifiedName~LiveSchedulePostReachesEndOnlyHandlerAndIgnoresLockedFields|FullyQualifiedName~EventSignupWarningRemediationIntegrationTests|FullyQualifiedName~ScheduleHandlerLoadsMachineValuesAndPreservesUntouchedInstants'`:48 passed,2 obsolete expectations failed. Passing evidence reused.
- User-authorized corrections only: Live save redirect to Schedule (README:1172), removed scheduled warning list (U3-Q2). Rerun `dotnet test tests/Bingo.IntegrationTests/Bingo.IntegrationTests.csproj --no-restore -c Debug --filter 'FullyQualifiedName~LiveSchedulePostReachesEndOnlyHandlerAndIgnoresLockedFields|FullyQualifiedName~PublicTextWarningUsesDanishOnManualAndScheduledConfirmation'`: PASS3/3, including unchanged manual-warning companion. Thus both original failures are resolved.
- After binding exact reference notices: same project command with `--filter 'FullyQualifiedName~TerminalScheduleGetAndCurrentAreReadOnlyWithoutOpeningMutationGate'`: PASS2/2.
- Mechanical checks: no Schedule redirect to Manage, no capacity in JS readback, only service pass-through capacity in model, obsolete decrease message absent. Legacy increase method delegates to the authoritative domain setter; service increase/count guards retained. Test14 method byte-identical to HEAD.
- `git diff --check`: PASS. Full JS runner and Release build NOT RUN (item4 only). Whole .NET suite NOT RUN. No independent review or manual acceptance claimed.

## Summary measurements

Both engines agree (line-height19.575px):

| Width | Loading px | English loaded px/lines | Danish loaded px/lines |
| --- | --- | --- | --- |
|390|39.140625|39.125 /2|58.6875 /3|
|494|39.140625|39.125 /2|39.125 /2|
|860|19.5625|19.5625 /1|19.5625 /1|
|1280|19.5625|19.5625 /1|19.5625 /1|
|1440|19.5625|19.5625 /1|19.5625 /1|

Danish390 grows one line; shared reservation is unchanged. Only following content moves. Original reference measurements and approved result are preserved in `schedule-summary-decision.json`; conformance and interaction evidence is in `schedule-item1-checks.json`. Synthetic screenshots: `schedule-390.png`, `schedule-1280.png`.

## Register / approval owner

DELIVERY_PLAN bindings register: F4 Signup closing field (OS-1), AU20 start/end exact-WOM-window refusal, AU10/C-CMP-2 Schedule fetch recovery, RC03/OS-5 picker/readback, approved linked shorter summary differing from Schedule.dc.html:117 and:875. UI_PAGE_MATRIX Schedule row marks new binding awaiting manual acceptance. Review catalogue adds Live/Final review/Archived/Cancelled Schedule links from existing fixtures. Debug-only configuration selectors preserve default Release while respecting the item4-only gate.

## Environment blocker and exact recovery decision

`BINGO_UI_REVIEW_CONFIGURATION=Debug python3 scripts/ui-review.py create` stopped before mutation:

> Refusing: ownership marker exists but its container is missing.

Read-only proof: `bingo-ui-review` and saved container ID `ad09b33d909f4d3f7f87342c13138577a74aad061fe1d10763a658755269e44c` both return No such container. Ports5310/5320/54339 have no listener. `artifacts/ui-review/processes.json` is absent. Marker `artifacts/ui-review/owner.json` remains unchanged. This is the repository script's guard, not automatic approval rejection. No retry or bypass occurred.

Recommend authorizing only a reversible rename of that stale marker to `artifacts/ui-review/owner.missing-container-2026-10-07.json`, then the ordinary Debug create command. Alternative: keep the environment stopped and deliver without live links. No environment URL is claimed live. Logs: `/tmp/u3-schedule-review-environment.log`; other scoped logs `/tmp/u3-schedule-*.log`.

After recovery, verify the resulting scenario links and send the required item1 report, then continue item2 without waiting. Item3 first working commit remains the mandatory early-look stop. No laneT edits, push, merge or deploy.

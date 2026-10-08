# U9 early-look handoff — stop after item 2b

Checkout `/Users/christopher/.codex/worktrees/u9-final-wom/BingoWebpage`, branch `codex/u9-final-wom`, base `a91b29ad627dac8b5bf859b1948ea4bbea014877` (U4 including review fixes). Fresh gpt-6-astra/high implementer; dispatcher `/root`; planner is the user's Claude chat. Direct user-routed lane C only. Required 1b checkpoint was delivered and work continued without waiting.

## Local commits / scope

- 0a `7a9d59c1`: mandatory Reopen version, blocking-current-event read, structured Final outcomes/current readback.
- 0b `740967ac`: structured WOM outcomes, typed reasons/eligibility, stored matching end-update retry, cached no-store Current.
- 1a `b0882c06`: Final Review shared workspace, server readiness, placements, official/history/read-only.
- 1b `4030828d`: Final dialogs, guarded writes, honest readback, reason retention, version drawers and A15 warning.
- 2a `cf699a0c`: WOM shared workspace, authoritative eligibility and status, end states, coverage, exact windows, checklist.
- 2b: the commit containing this updated handoff (`U9 2b: bind WOM actions and early-look scenarios`); full SHA accompanies the dispatcher's completion report. Adds the six action bindings, retry/unknown handling, owned scenarios and both-page parity checklist.

Items 0a–2b are implemented and locally committed. Items 3/4, independent review and manual acceptance are **not done and not authorized yet**. No push/merge/deploy. No migrations or lane A/B production changes. No change to `UI_PAGE_MATRIX.md` approval records. C8 was untouched.

## Executed checks

Exact commands and limitations for 0a–2a are in `docs/evidence/u9-item-{0a,0b,1a,1b,2a}.md`; retain that passing evidence:

| Item | Evidence |
| --- | --- |
| 0a | 73 PostgreSQL + 4 localization checks passed |
| 0b | 264 PostgreSQL + 8 source/localization + 2 application checks passed |
| 1a | 10 source; initial 22 HTTP passed / 2 obsolete markup assertions corrected, then 5 named/direct-consequence HTTP passed; five-width conformance both engines |
| 1b | 8 source + final 4 localization passed; 7 Final interaction groups per engine; five-width conformance both engines |
| 2a | 10 source/localization + 6 PostgreSQL HTTP passed; five-width conformance both engines |

2b exact commands (run in this worktree; outputs redirected to `/private/tmp/u9-2b-*.log`):

```sh
dotnet test tests/Bingo.IntegrationTests/Bingo.IntegrationTests.csproj -c Release --filter 'FullyQualifiedName~Rc09W1Wa9AppliedCreateAndDeleteReportCompletedRatherThanQueued' --logger 'trx;LogFileName=u9-2b-completed.trx' --results-directory /private/tmp/u9-test-results
dotnet test tests/Bingo.BrowserTests/Bingo.BrowserTests.csproj -c Release --filter 'FullyQualifiedName~ManagedCompetitionUiTests|FullyQualifiedName~AdminDesignLocalizationTests|FullyQualifiedName~AdminShellUiTests' --logger 'trx;LogFileName=u9-2b-source-final.trx' --results-directory /private/tmp/u9-test-results
dotnet build tests/AdminDesignParityFixture/AdminDesignParityFixture.csproj -c Release
NODE_PATH=/Users/christopher/.codex/worktrees/participants-functionality/BingoWebpage/node_modules BINGO_CONFORMANCE_PAGES=final-review,wom PLAYWRIGHT_BROWSER=chromium /Users/christopher/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/bin/node scripts/check-admin-page-conformance.cjs
NODE_PATH=/Users/christopher/.codex/worktrees/participants-functionality/BingoWebpage/node_modules BINGO_CONFORMANCE_PAGES=final-review,wom PLAYWRIGHT_BROWSER=webkit /Users/christopher/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/bin/node scripts/check-admin-page-conformance.cjs
NODE_PATH=/Users/christopher/.codex/worktrees/participants-functionality/BingoWebpage/node_modules PLAYWRIGHT_BROWSER=chromium /Users/christopher/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/bin/node tests/Bingo.BrowserTests/admin-design-wom.browser.js
NODE_PATH=/Users/christopher/.codex/worktrees/participants-functionality/BingoWebpage/node_modules PLAYWRIGHT_BROWSER=webkit /Users/christopher/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/bin/node tests/Bingo.BrowserTests/admin-design-wom.browser.js
NODE_PATH=/Users/christopher/.codex/worktrees/participants-functionality/BingoWebpage/node_modules U9_WOM_VARIANTS=base PLAYWRIGHT_BROWSER=chromium /Users/christopher/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/bin/node tests/Bingo.BrowserTests/admin-design-wom.browser.js
NODE_PATH=/Users/christopher/.codex/worktrees/participants-functionality/BingoWebpage/node_modules U9_WOM_VARIANTS=base PLAYWRIGHT_BROWSER=webkit /Users/christopher/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/bin/node tests/Bingo.BrowserTests/admin-design-wom.browser.js
git diff --check
```

2b results: 1 focused PostgreSQL HTTP test (both real applied Create/Delete) and 10 source/localization passed, 0 failed/skipped; fixture Release build passed with 0 warnings/errors; two-page conformance passed 10 geometry cases per engine (five widths × two pages), plus frame/style/no-fade/document/update/Danish checks; 12 WOM interaction groups passed per engine. After the final coverage empty-state correction, `U9_WOM_VARIANTS=base` with the same two browser commands passed 8 interaction groups per engine, including the named empty-state assertion and regenerated settled wide/phone screenshots. The implementer inspected the Pending wide/phone images; manual visual acceptance remains the user’s. Fixture Release rebuilt before browser checks. Browser runs use their own PostgreSQL containers/random Kestrel ports. No real WOM provider requests: actual handlers persist credential/disconnect/Create/Delete outcomes; management provider requests receive an opt-in synthetic 429. Browser response interception covers lost response/session/readback and Fetch skip cases. This is not live provider acceptance. `git diff --check` passed. No full .NET suite or item4 batch gate was run.

A focused PostgreSQL HTTP check distinguishes completed Create/Delete wording from queued work. Completed outcomes say “The WOM competition was created and linked.” / “The website-created WOM competition was deleted.”

A browser-driven correction makes `Pending/Retry/Unknown` management results queued even when `Succeeded` is false: provider failure may leave a real retry operation. Tests assert the actual `Retry` JSON and resulting locked controls. Test setup also uses valid short synthetic WOM names; generic UR account display names are longer than WOM's limit. Test-selector/timing corrections and fixture limitations are recorded in `u9-item-2b.md`. No base product failures found. The earlier automatic approval timeout was explicitly retried once and resolved; no outstanding permission/environment blocker.

## Parity / shared edits

`docs/evidence/u9-parity.md` maps each reference section/action to the application and the approved difference. Automated geometry is five widths per page/engine; this is not a visual acceptance claim. Scoped leftover searches passed with documented protected/frozen exceptions: the unused legacy shared Type FETCH fallback, existing valid GUID/blocker routes, and frozen reference tolerance prose. Application WOM matching is exact UTC.

Shared edits across U9: small `admin-design-shell.js` family registration, `_AdminDesignTemplates.cshtml` loading-template registrations and new page load-state partials, `scripts/lib/admin-page-conformance-pages.cjs`, `AdminEventPagePolicies.cs` handler classification, append-only DELIVERY_PLAN difference rows, one contiguous end block of Danish additions. Page CSS is new and family-scoped. No shared CSS or `admin-lifecycle-confirm.js` modification. Server changes stay in assigned finalization/WOM services/contracts/pages; PostgreSQL state remains authoritative.

## Proposed wording for the early look

- **A15 Final Publish:** “Publishing now makes the last fetch before the end the official WOM data.” The stored unsuccessful-only per-version note records the actual skipped/failed outcome, including the retained pre-end data. Publication remains available; no extra current-event readiness row or next-eligible-time display.
- **A15 WOM:** “End update pending” / “End update rejected” / “End could not be updated.” Pending/Rejected: “Fetches are paused until the WOM end matches the event end exactly. Publishing is still available and retains the last fetch before the end.” Could-not: “The last fetch before the end is retained. This end update will not restart, even if results are reopened.” State lives in the existing main-issue priority slot and Updates card; exact target and only a stored matching next operation attempt appear there, with technical details retained.
- **Ruled Reopen blockers:** Live: “‹X› is still live. End it first, then publish its results before reopening this event.” Awaiting review: “Publish the results of ‹X› first.” Legacy Finalized: “‹X› is still the current event. Contact the Super Admin to archive it.” Each keeps the fixed “Open event” link.
- **Final read-only:** Cancelled: “This event was cancelled. Results are read-only.” Official: “Official results are read-only. Reopen only when a correction is needed.” Legacy: “Official results are read-only. Complete the legacy publication to archive the event, or reopen for corrections.”
- **WOM read-only:** “This event was cancelled. Its Wise Old Man connection is read-only.” / “Results are official. Its Wise Old Man connection is read-only.” / “This event is archived. Its Wise Old Man connection is read-only.”
- **Unknown outcomes:** “We couldn’t confirm whether new data was fetched.” / “We couldn’t confirm whether this change was saved.” Followed by “Check the current state before trying another action.” Readback reports “Current stored connection: ‹ID›. Last successful fetch on record: ‹time›. Operation status: ‹state›.” Failed readback: “The current state is unavailable. Check again before trying another action.” It never equates an old timestamp/current state with this request succeeding.
- **Queued:** “The operation is queued. Check the current state for its result.” “Sending the latest details. Not confirmed yet.” Last confirmed update uses `LastAppliedAt`; no invented payload.
- **Code/session:** “The submitted code was cleared. Enter it again if needed.” C-CMP-2 shared session-loss UI first says the changes were not saved and retains other input. Invalid ID/code and stale/refused outcomes remain in the dialog with recoverable entries.
- **Exact window:** “Start and end must match the website exactly in UTC. Linking never imports dates or reuses an old management code.” Disconnect explicitly says the remote competition is not changed/deleted; Delete explicitly identifies the website-created competition and irreversible remote removal.
- **Create/refusal:** Three reference prerequisite rows plus each further applicable server refusal. Existing string matching is retained (technical choice); no new reason-code framework. Unknown origin remains exactly “Unknown origin; contact an operator.”

Coverage with expected accounts but no available rows now says “Account coverage is not available yet”, rather than incorrectly claiming there are no teams.

Fetch reason wording:

| Reason | Text |
| --- | --- |
| WithinHour | The last successful fetch was less than an hour ago. |
| RetryDelay | An automatic retry is pending. |
| NotDue | The next fetch slot is not due yet. |
| RefreshInProgress | A fetch is already running. Its lease expires at the next eligible time. |
| EventUnavailable | Fetching is unavailable in this event state. |
| NoCompetition | Link a competition before fetching data. |
| IncompleteEventWindow | The event start and end must be recorded before fetching data. |
| ServiceUnavailable | The Wise Old Man fetch service is unavailable. |
| EndWindowUnmatched | Fetches are paused until the WOM end matches the event end exactly. |
| EndCouldNotBeUpdated | The WOM end could not be updated. The last fetch before the end is retained. |
| EventNotInFinalReview | The event is not in final review. |

Known refusals include stale event/version, invalid competition ID/code, already linked/managed, unresolved operation, invalid future/exact window, invalid team/player names, shared source, source changed/missing and credential rejection. Provider secret text is redacted. Detailed localized field/server messages are preserved rather than substituted with a generic cooldown.

No unruled product question or migration emerged. Planner's final rulings are implemented: stored next attempt only (otherwise state + target), make-due handler/test-only, state-specific Reopen blockers, `=3` and top-three reference behavior. Remaining copy choice: (A) retain the proposed wording, or (B) user-directed copy adjustments after seeing the states. Recommendation A, subject to the early look; this is not a request to alter the ruled behavior.

## Build and scenarios to serve — planner owns serving

Serve **Release from the final `codex/u9-final-wom` HEAD**, not U4 or the 1b checkpoint. Do not touch shared ports 5310/5320/54339 or stale-marker recovery without planner authorization. No shared environment was started/stopped/modified by this lane.

The owned fixture `tests/AdminDesignParityFixture` emits event IDs by slug. Open `/Admin/Events/Finalize/{id}` or `/Admin/Events/WiseOldMan/{id}`. The fixtures are explicit local scenario variations, not changes to the shared production scenario seeder:

| Fixture inputs | What to open |
| --- | --- |
| `BINGO_PARITY_UR_PROFILE=final-review` | Final `ur-current`: blocked; `ur-archived`: Reopen blocked by Awaiting review; `ur-cancelled`: read-only |
| Same + `BINGO_PARITY_U9_READY=1` | Final `ur-current`: ready → Publish → official/archived with skipped refresh → version drawer → Reopen; reason/lost response covered by browser script |
| `BINGO_PARITY_UR_PROFILE=live` | Final `ur-archived`: Live Reopen blocker |
| final-review + `BINGO_PARITY_U9_WOM=base` | WOM `ur-signups-open`: linked/credential/replace/disconnect; `ur-upcoming-01`: website-managed/delete; `ur-upcoming-02/03`: queued/unknown Create; `ur-upcoming-04`: locked Unknown origin; `ur-signups-closed`: valid Create; `ur-current`: end Pending; `ur-wom-unavailable`: could not update; `ur-cancelled`: terminal |
| final-review + `BINGO_PARITY_U9_WOM=rejected` | WOM `ur-current`: mutable end Rejected |
| live + `BINGO_PARITY_U9_WOM=rate-limited` | WOM `ur-current`: retained data/rate limit/retry; Final `ur-archived`: Live blocker |
| live + `BINGO_PARITY_U9_WOM=fetch-ready` | WOM `ur-current`: eligible Fetch, controlled lost-response/skip browser scenarios |

Screenshots/results: `artifacts/final-review-{chromium,webkit}/`, `artifacts/wom-{chromium,webkit}/`, `artifacts/page-conformance-{chromium,webkit}/`. Runtime artifacts are local and not committed. Each browser fixture is disposed by its test; no service is left serving.

**Next permitted action:** planner arranges the early look and relays acceptance/copy decisions. Stop here. Do not start items3/4, independent review, packaging, merge or publication until separately authorized.

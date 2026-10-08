# 24a — AU20 item 1 review (commit `52f0af6`): exact configured window and structured outcomes

Reviewer: Claude sub-agent, read-only, 4 October 2026. I ran no builds or tests. Everything below comes from `git show`/`git diff`/`git grep` on branch `codex/participants-functionality` (base `b126c55`, item `52f0af6`, tip `4b9f156`). "Traced" means I followed the code path myself. "Inferred" means a conclusion I reached without executing anything.

## Summary

Item 1 does what the ticket and WA-2 item 1 ask for. All three former five-minute tolerance sites now use exact `DateTimeOffset` equality against the **configured** event start and end. The Final Review "use the actual window" branch is gone. Actual instants still drive the upload cutoff and the review window. No tolerance or actual-time comparison against WOM dates is left anywhere in `src` at `52f0af6` or at `4b9f156`. The structured outcomes are built on AU18's `LeaseDecision`/`EventCompetitionRefreshSkipReason`, not on a parallel mechanism. Members 0–7 of the enum are unchanged, and later items append 8 and 9 with explicit numbers. No new user-visible display was added. I found no High or Medium issues, only small test gaps and notes.

## Checks performed (confirmed, traced)

### 1. The three former tolerance sites are now exact

| Site | Code at `52f0af6` | Before (`b126c55`) |
|---|---|---|
| Shared link / replacement / fetch validation | `EventCompetitionSynchronizationService.cs` `ScheduleMatches` (~:438-443): `authoritativeStart == competition.StartsAt && authoritativeEnd == competition.EndsAt`, where start/end = `item.EventStartsAt/EventEndsAt`. Called from `ConfigureAsync` (link and replacement, ~:121) and `FinalizeLeaseAsync` (each fetch, ~:338 and ~:379). | ±5 minutes. In Final Review it used `ActualStartedAt/ActualEndedAt` (`useActualWindow`). |
| Schedule save | `EventSignupLifecycleService.cs:450-452`: `start != expectedStart \|\| end != expectedEnd`, applied to external (non-managed) links only. | ±5 minutes. |
| Resume | `EventLifecycleService.cs:409-412` (`FindLifecycleOverlapAsync`): `start != competitionStart \|\| replacementEnd != competitionEnd`. | ±5 minutes. |

- **Final Review included:** the `useActualWindow` parameter was deleted, so every fetch stage, including `RefreshForFinalReviewAsync`, compares against the configured window. The mismatch message now prints the configured window (`DescribeScheduleMismatch`).
- **Resume at the tip:** item 2 (`2d74bde`) later removed Resume's WOM comparison completely. At `4b9f156`, `FindLifecycleOverlapAsync` (`EventLifecycleService.cs:397-416`) only checks overlap, and Resume records a pending WOM end update (`RecordPendingEndUpdateAsync`, :418-424). This follows WA-2's "Resume always succeeds locally" rule, and the item-1 evidence file says so. It is not a regression.
- **Leftover sweep:** `git grep` at both `52f0af6` and `4b9f156` for `FromMinutes(5)`, `TotalMinutes … 5`, `five minutes`, `tolerance` and `remain within` finds only unrelated uses: board edit lock, catalogue cache, claim timeout, WOM client cache, account validation, signup lookup token and the five-minute input increments.
- **Remaining WOM date comparisons at the tip,** all against the configured values:
  - `EventCompetitionManagementService.cs:1094`: `competition.StartsAt == item.EventStartsAt && competition.EndsAt == item.EventEndsAt`.
  - `:1407`: receipt check against the requested payload, which is built from `item.EventStartsAt/EventEndsAt` (:1365-1378).
  - `EventCompetitionSynchronization.HasUnmatchedEnd(item.EventEndsAt)`, used at sync service :273/:304/:345.
- `EventCompetitionUpdateAllService.cs:336` uses `ActualStartedAt` only to schedule fetch slots, not for window validation. No comparison to actual times remains.

### 2. Precision and timezone robustness (traced; conclusion inferred)

- **Timezone-equivalent inputs compare equal.** `DateTimeOffset ==` compares UTC instants and ignores the offset. The new test uses provider offsets +02:00 and −04:00 and passes.
- **Configured times are constrained to whole minutes on every production write path:**
  - The Schedule page parses `yyyy-MM-ddTHH:mm` and requires `Minute % 5 == 0` (`Schedule.cshtml.cs:156-158`). Unchanged values keep the stored instant (`PreserveOrParse`).
  - The Manage page's local date parser is the same (`Manage.cshtml.cs:485`).
  - At the tip, Resume requires `Minute % 5 == 0` and `Ticks % TicksPerMinute == 0` (`EventLifecycleService.cs:180-181`, added by item 2).
  - Early end rounds up to the whole minute (item 2).
  - The development seeder aligns `now` to :00 or :30 (`DevelopmentScenarioSeeder.cs:79`).
- **Where the compared values come from:**
  - WOM returns millisecond ISO timestamps.
  - PostgreSQL stores microseconds.
  - At every site, the configured value is read back from PostgreSQL (`FOR UPDATE` reload or `AsNoTracking`), or it is a whole-minute value parsed from the form.
- **Conclusion:** on real paths, a configured `22:00:00` never meets a WOM `22:00:00.000` with a hidden sub-second remainder, so exact equality cannot fail spuriously. Inferred from the traced paths above.
- See Note N1 for the remaining theoretical gap.

### 3. Actual times still drive cutoff, eligibility and review window

- Item 1 did not touch `BingoEvent.EndEvent`: `SubmissionCutoffAt = ActualEndedAt + 30 min`.
- `RefreshForFinalReviewAsync` still uses `ActualStartedAt/ActualEndedAt` for `IncompleteEventWindow` (sync service ~:176-178).
- Drop eligibility and review-window code is not in the diff.
- The updated `StatsPass4RealFinalReviewUsesConfiguredWindow…` test keeps `ActualEndedAt = actualEnd` while matching WOM against the configured end. It asserts that `ActualEndedAt ≠ EventEndsAt` and that the fetch is accepted only for the configured window.

### 4. Structured outcomes (WA-6) on the AU18 mechanism

- **Shared eligibility logic:** `RefreshEligibility(item, state, manual, now)` (sync service ~:280-299) was extracted from `AcquireLeaseAsync` and returns the existing `LeaseDecision`. `AcquireLeaseAsync` and the readback both call it, so there is no second mechanism.
- **Readback** (`GetAsync`, ~:31-51):
  - It loads `AsNoTracking` rows and applies `BeginReplacementGeneration`/`ReconcileNormalSlot` to the detached copy. Neither method throws; I checked the domain file.
  - It never calls WOM or `SaveChanges`.
  - It returns `RefreshSkipReason`, `NextEligibleAt` and `CanRefresh`.
- **Manual `RefreshAsync`** now returns `SkipReason` and `RetryAt`, alongside the unchanged generic message (~:226-229). An unavailable event returns `EventUnavailable` (:164-166).
- **Management view** (`IEventCompetitionManagementService.cs:46-51`, filled at `EventCompetitionManagementService.cs:89-91`) adds typed fields: `OperationId`, `OperationPhase`, `OperationType`, `NextAttemptAt` and `CredentialStatus`. These are additive, with defaults.
- **Enum `EventCompetitionRefreshSkipReason`:**
  - At `b126c55` and `52f0af6` it is identical: `EventUnavailable=0 … ServiceUnavailable=7`.
  - At `4b9f156` it appends `EndWindowUnmatched = 8, EndCouldNotBeUpdated = 9` (`IEventCompetitionSynchronizationService.cs:18-20`).
  - No member was renamed, removed or renumbered.
- **Stored format:** `EventFinalizationService.cs:23` still uses `JsonStringEnumConverter`, so names are stored. `Au18FinalRefreshHistoryIntegrationTests.cs` is unchanged across B3 (empty diff `b126c55..4b9f156`). It still covers stored string and numeric outcomes and the reordered-enum read.
- **`Au20StoredOutcomeContractTests`** (`tests/Bingo.Application.Tests/`, added in item 4 `6625617`) asserts names 0–7 by number and the values 8 and 9. This is correct.

### 5. No new user-visible display

- **Web files changed across `b126c55..4b9f156`:** `Manage.cshtml`, `Manage.cshtml.cs`, `Schedule.cshtml.cs` and `SharedResource.da.resx`.
- **Item 1 itself** changed only:
  - the error-to-field mapping string in `Schedule.cshtml.cs:170`;
  - one Danish translation of the new error text.
- **The Manage changes come from item 2,** not item 1. They simplify the existing Resume copy to "Choose a future replacement end…" and set `ResumeRequiresReplacement = true`, because Resume now always needs a replacement end. The copy changed for behaviour item 2 changed. No new AU20 state is shown.
- **WOM page Fetch** (`WiseOldMan.cshtml.cs:93-98` at the tip) still shows the generic "backend cooldown is still active…" text for every skip (D4). No page reads `RefreshSkipReason`, `NextEligibleAt`, `CanRefresh` or the new management fields (`git grep` in `src/Bingo.Web`).
- **Finalize page:** the existing AU18 mapping (`Finalize.cshtml.cs:56-67`) falls through to "Skipped" for the new members 8 and 9. That page and its mapping already existed, and nothing new is displayed. Whether items 4/5 should map them is outside item 1 (backend only, per the user decision).

## Findings

### L1 (Low): the fetch site has no test that proves a sub-five-minute mismatch is now rejected
- **What the code does:**
  - `ScheduleMatches` is shared by link, replacement and fetch.
  - The ±1 s test (`Au20ConfiguredWindowIntegrationTests.cs:14-50`, cases `1` and `-1`) checks only the link site (`ConfigureAsync`).
  - The fetch-site test (`StatsPass4RealFinalReviewUsesConfiguredWindow…`) mismatches by more than five minutes (configured vs actual end; asserted `> 5 min`). It proves "configured, not actual", but not "exact, not tolerant".
- **Required:** "each old five-minute tolerance site" (ticket Focused acceptance; brief §5).
- **Risk:** low. It is the same private function, so the link test exercises the exact comparison (traced). A future refactor that split the fetch check could reintroduce tolerance without failing any test.
- **Fix (optional):** one Live or Final Review fetch with the WOM end 1 s off the configured end, expecting `ScheduleMismatch` and no checkpoint change.

### L2 (Low): readback outcomes are asserted only for `NotDue`, and the typed management operation fields are never asserted with values
- **What the tests cover:**
  - `Au20ExactConfiguredWindow…` asserts the readback/manual parity (`NotDue`, the same `NextEligibleAt`/`RetryAt`) and that no fetch happens.
  - Readback `RefreshInProgress`, `RetryDelay` and `EventUnavailable` are not asserted. Item 4 tests add `EndWindowUnmatched` (`Au20PublicationFallbackIntegrationTests.cs:59-66`).
  - The new management-view `OperationId/Phase/Type/NextAttemptAt/CredentialStatus` fields are only asserted as `null` (`Au20ExternalReplacementIntegrationTests.cs:49, 68`).
- **Required:** WA-6 / ticket point 6 asks for "structured eligibility/reason/next-permitted-time/credential/operation outcomes".
- **Risk:** low. The code is a straight projection of the same `RefreshEligibility` and of the latest operation row (traced). A wrong mapping would still not be caught.

### N1 (Note): the domain does not enforce whole-minute configured times
- `BingoEvent.ConfigureSchedule`, `ChangeLiveEventEnd` and `ConfigureFinalizedDraftEventWindow` (`BingoEvent.cs:302-365`) accept any precision. Only the web parsers and Resume enforce minutes.
- **Scenario (hypothetical; no current production path does this):** a non-UI caller sets the end to `22:00:00.0001234`. PostgreSQL stores `22:00:00.000123`. A website-created competition sends `…00.0001234Z` (`WiseOldManCompetitionManagementClient.cs:312-322`, format `"O"`). WOM keeps milliseconds and returns `22:00:00.000`. Every later fetch is then a permanent `ScheduleMismatch`.
- No action is needed for AU20. It is worth knowing if a future import or API path writes event times.

### N2 (Note): legacy rows with a WOM window that matches the actual window will now mismatch
- Before AU20, Final Review accepted a WOM window equal to the **actual** window (±5 min).
- An event already in AwaitingFinalReview whose WOM competition was edited to the actual early end would now get `ScheduleMismatch` on its final fetch. From item 4 on, it would take the "could not be updated" fallback.
- This matches the decided rule (WA-2 item 1). It matters only if such rows exist in a database that receives this code. Inferred; I did not check any data.

### N3 (Note, unverified, outside item 1): a broad-run failure is reported as unrelated
- The item-1 evidence reports that `StatsPass4FiveByFiveObjectivesFinalizationArchiveAndUnfinalizationRetainOfficialHistory` (`StatsPass4BoundaryIntegrationTests.cs:125`) fails.
- **What I traced:** the fixture uses a fixed clock (`StatsPass4QueryIntegrationTests.cs:885`, 15 Sep 2026) that item 1 did not change. The test advances 12 h, past the configured end (+10 h). At `52f0af6`, `EndNowAsync` then sets the actual end to the configured end (`EventLifecycleService.cs:137`), so the cutoff (+30 min) has already passed when the test expects a `submission-window` blocker.
- **Conclusion:** that logic is not touched by item 1, so the failure plausibly predates item 1.
- **Not verified:** whether it fails at `b126c55`, and whether it passes at the tip after item 2 changed EndNow. The planner should confirm it with the final B3 test run.

## Acceptance coverage (item 1 scope)

| Acceptance point | Test | Executed assertion? |
|---|---|---|
| Exact configured window at link | `Au20ExactConfiguredWindowSurvivesPostgresAndTimezoneConversion` (0 / +1 s / −1 s) | Yes. ±1 s rejected with "exactly"; 0 s accepted. |
| Timezone-equivalent configured window through Final Review | Same test: provider offsets +2 h / −4 h, then `RefreshForFinalReviewAsync` succeeds (`provider.Calls == 2`) | Yes |
| Non-microsecond input through real PostgreSQL precision | Same test: configured start has +7 ticks; asserts the persisted value is µs-aligned and differs from the raw value, then matches WOM built from the persisted value | Yes, for storage truncation. It does not cover WOM millisecond truncation of a sub-ms value (see N1). |
| Former site: link/replacement | Same test, plus updated messages in `Slice10Pass102…` replacement and mismatch tests and `EventCompetitionManagementIntegrationTests.cs:633` | Yes |
| Former site: fetch (Final Review) | `StatsPass4RealFinalReviewUsesConfiguredWindowAndNoDuplicateFetchAfterPublish` (configured accepted, actual rejected with `ScheduleMismatch`) | Yes for configured vs actual. No sub-5-minute case (L1). |
| Former site: Schedule save | `ScheduleEditRejectsAMismatchWithTheLinkedCompetition` (now a 1-minute mismatch); `Slice3…` (10 min) | Yes. The 1-minute case would have passed under the old tolerance. |
| Former site: Resume | `Au20ResumeRejectsEvenOneSecond…` at `52f0af6` | Yes at item 1. Item 2 intentionally replaced it with `Au20ResumePersistsReplacementAndPendingUpdateDespiteProviderMismatch`. |
| Actual times still drive cutoff and review window | The StatsPass4 test keeps `ActualEndedAt ≠ EventEndsAt` and asserts it | Partial: asserted in that fixture. Cutoff and drop eligibility were not touched (traced, not tested here). |
| Structured reason / next time on readback and manual Fetch, readback never fetches | `Au20ExactConfiguredWindow…`: `NotDue`, `CanRefresh == false`, readback = manual reason and time, `Calls` stays 2 | Yes, for `NotDue` only (L2) |
| Credential / operation outcomes on management view | `Au20ExternalReplacementIntegrationTests` (null only) | Partial (L2) |
| Enum stability | `Au20StoredOutcomeContractTests`; `Au18FinalRefreshHistoryIntegrationTests` unchanged | Yes |
| Manual Fetch keeps generic wording; no new display | No test. Code traced (`WiseOldMan.cshtml.cs:93-98`); no Web reads of the new fields | Not tested (traced) |

The executed results above come from the implementer's evidence (`item1-exact-window.md`). I did not re-run them.

## Suggested verdict

**PASS with notes.** Every tolerance site is exact against the configured UTC window, Final Review included, and actual times still drive cutoff and review. The outcomes extend AU18 without changing enum members 0–7. No new display was added. The remaining gaps are two small test-coverage points (L1, L2) and three notes.

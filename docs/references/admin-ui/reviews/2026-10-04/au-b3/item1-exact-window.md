# AU20 item 1 — exact configured window and structured readback

Implementation checkpoint; external Claude review pending.

## Authority and attribution

- Supplied [brief](supplied-brief.md), item 1; current assignment in planner chat `01a10660-cc8a-7843-abb4-6cc1ebbb2bf2` explicitly authorizes six scoped implementation commits. Its predecessor planner ID in the supplied brief is retired.
- User-supplied decision source: `/Users/christopher/Documents/BingoWebpage/review-notes/08-decisions.md`, WA-2 items 1–7, Step 4 D4, B1/B2 D2, and B3 decisions. Findings: `06b-wom-catalogue-accounts-audit.md`, WA-2/WA-5/WA-6/WA-9 in the same source directory. These supplied source records are the attribution, not independent implementer approval.
- Baseline verified clean at `b126c556ccd2e16cb59829837581646a3179847e`. Authorized planner-only register prerequisite `2649008` was added during item 1, preserving the implementation diff.

## Change

- Shared link/replacement/fetch validation, Schedule save and Resume compare the configured UTC instants exactly. Final Review uses the same configured window; actual cutoffs are untouched.
- Manual Fetch returns AU18 SkipReason and next-eligible time while its existing page retains generic cooldown wording.
- Detached readback uses the same LeaseDecision eligibility logic without fetching or persisting; management readback exposes typed credential and current operation identity/type/phase/next attempt.
- Existing enum names/numbers 0–7 remain unchanged. No second skip-reason mechanism and no new page display.
- The Schedule handler keeps its field-level mapping for the revised exact-window message.
- Ordinary synchronization fixtures use deterministic aligned instants. Dedicated AU20 fixture passes non-microsecond input through PostgreSQL, then constructs a timezone-equivalent provider window from the persisted values.
- Item 2 replaces Resume's provider-match prerequisite with local success plus a pending update, as explicitly required by the ordered brief.

## Execution

- `dotnet build Bingo.slnx --configuration Release --no-restore`: PASS, zero warnings/errors.
- Initial integration filter `FullyQualifiedName~Au20|FullyQualifiedName~Slice10Pass102CompetitionSynchronizationTests|FullyQualifiedName~Au18PublishedVersion`: 162 passed, 3 failed, 0 skipped (165 total). This broad partial-class selection also included unrelated Stats cases.
- Two failures were superseded actual-window expectations; updated the controlled Final Review fixture to configured-window semantics and retained its transition history. Named recheck `FullyQualifiedName~StatsPass4RealFinalReviewUsesConfiguredWindow`: 2/2 passed.
- Dedicated Release run: AU20 exact/timezone/PostgreSQL precision and Resume cases 4/4 passed; all six AU18 stored string/numeric outcomes passed. Initial updated Final Review fixture lacked its transition row; fixed that fixture, then the named two-case recheck above passed.
- Schedule/Resume focused recheck (`ScheduleEditRejectsAMismatchWithTheLinkedCompetition`, `SaveScheduleAllowsManagedWindowChangesBeyondFiveMinutesForAutomaticUpdate`, `Au20Resume`): 3/3 passed. External Schedule rejects a one-minute mismatch while managed schedule edits retain their existing queued-update path.
- `git diff --check`: PASS.
- Remaining unrelated broad-run failure: `StatsPass4FiveByFiveObjectivesFinalizationArchiveAndUnfinalizationRetainOfficialHistory`, `StatsPass4BoundaryIntegrationTests.cs:125`, expects a submission-window blocker after advancing 12 hours, beyond its configured end and cutoff. Item 1 does not change EndNow/cutoff behavior; reported to planner, not silently fixed or counted as passing.

No independent review, manual acceptance, live WOM request or user-owned database access performed.

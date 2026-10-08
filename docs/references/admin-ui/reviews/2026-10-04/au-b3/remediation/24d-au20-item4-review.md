# B3 / AU20 item 4 review — unmatched-end fetch suppression and publication fallback (commit `6625617`)

Reviewer: Claude (independent reviewer sub-agent), 4 October 2026. Read-only: git show/diff/grep only. Nothing was built or run, so every "passed" below is Codex's claim unless it says "traced".

Sources checked: brief 23 §3 item 4 and §5; `08-decisions.md` WA-2 items 4–6, "B3 (AU20) brief decisions", D2, design-gaps register; ticket `DELIVERY_PLAN.md` AU20 point 5; evidence `au-b3/item4-publication-fallback.md` (unchanged in later commits). I also checked that items 5–6 (`6d29f2c`, `4b9f156`) did not change the item-4 logic. The only related later change: `Reconfigure` now resets the end-update state to NotRequired (item 5). It cannot run after the event leaves Live, so it does not affect the fallback.

All file:line references are at `6625617` unless stated.

## 1. How "unmatched" is decided

`EventCompetitionSynchronization.HasUnmatchedEnd(configuredEnd)` (`src/Bingo.Domain/Integrations/WiseOldMan/EventCompetitionSynchronization.cs:47-49`) is true when a competition is linked and either:
- the stored WOM end (`CompetitionEndsAt`, the competition metadata the website last stored) differs from the configured end, or
- the end-update status from items 2–3 is Pending, Rejected or CouldNotUpdate.

So it uses **both** the pending-update state and the stored WOM end metadata. It never compares fetched player data. Item 3 updates `CompetitionEndsAt` from WOM's write receipt and only marks Succeeded when WOM's start and end equal the configured ones exactly (`EventCompetitionManagementService.cs:1080-1087`). After a success, therefore, the metadata matches and the status is Succeeded, and fetching resumes.

Every fetch gate also requires `item.ActualEndedAt is not null`. Fetches before the actual end are never suppressed, which is correct.

## 2. Fetch entry points: are they all gated? (traced)

Only one method writes the WOM cache: `FinalizeLeaseAsync` in `EventCompetitionSynchronizationService.cs`. Every competition fetch goes through `SynchronizeOneAsync` → `AcquireLeaseAsync` → provider call → `FinalizeLeaseAsync`.

| Entry point | Path | Gate |
|---|---|---|
| Scheduled fetch | `EventCompetitionSynchronizationWorker` → `ProcessDueAsync` (`:202-222`) → `SynchronizeOneAsync(manual:false)` | `AcquireLeaseAsync:255-256`. It runs before the lease check and before any provider call. |
| Manual WOM-page Fetch | `WiseOldMan.cshtml.cs:93` → `RefreshAsync` (`:161-169`) → `SynchronizeOneAsync(manual:true)` | Same gate |
| Final fetch at publication | `EventFinalizationService.cs:136` → `RefreshForFinalReviewAsync` (`:171-181`) | Same gate. The publication transaction then overrides the outcome (see §4). |
| Development "make due" | `MakeDevelopmentRefreshDueAsync` (`:183-200`) | Only sets a due time and only for a Live dev fixture. The fetch it leads to is still gated. |
| WOM page readback | `GetAsync` (`:31-51`) → `RefreshEligibility` (`:286-287`) | Never calls WOM. It reports `CanRefresh=false` and the new skip reason. |
| Management reads (`EventCompetitionManagementService.cs:535/567/811/907`) | Receipt and verification reads | They never write participant or metric activity, so they cannot store post-end data. |
| Update-all (`EventCompetitionUpdateAllService`) | Asks WOM to update players | It runs only while the event is Live (`GetManagedLiveRootsAsync`). It stores no competition data. |
| Luck / KC | `PublicStatsService.Luck.cs:19-38` | It reads only the saved checkpoint and never calls WOM. A checkpoint is published only after an accepted fetch (`:418-420` in the sync service). |

Result: no path can store post-end WOM data while the end is unmatched.

**The gap you asked about (early end at 21:59:55 while a scheduled fetch already holds a lease): closed.** `FinalizeLeaseAsync:327-332` re-reads the event and sync rows `FOR UPDATE` inside a serializable transaction. If the event has an actual end and the end is still unmatched, it releases the lease and stores nothing ("EndWindowUnmatched"). If item 3's first attempt has already succeeded by the time the response returns, a response that still carries the old WOM end fails the exact `ScheduleMatches` check at `:355`. It is then recorded as ScheduleMismatch and not stored. The only accepted response is one that WOM served after its end was updated, which is limited to the new 22:00:00 window. That window includes 21:59:55–22:00:00, which WA-2 item 3 accepts by rounding up. A test covers the first half of this (§6, test B).

## 3. After a successful update (traced)

Item 3's receipt calls `CompleteEndUpdate` and `MakeNormalRefreshDue` (`EventCompetitionManagementService.cs:1083-1087`), and `HasUnmatchedEnd` becomes false. The next fetch must pass the exact window check (`:355`), so it uses the corrected window. Generation and cache are kept, since an end-only update does not call `Reconfigure`.

Tested only through the final-review fetch (one line added to item 3's backoff test). That test checks `RefreshForFinalReviewAsync` succeeds after the update. It does not show the scheduled or manual fetch resuming, and in the same test it does not show that fetches were suppressed before the success.

## 4. Publication fallback (traced)

`EventFinalizationService.FinalizeAsync`:
- `:131-139`: the pre-transaction final fetch runs. It is gated, so it returns Skipped/EndWindowUnmatched without calling WOM.
- `:172-181`: inside the serializable publication transaction, the sync row is locked `FOR UPDATE` and reloaded. If the end is unmatched:
  - `MarkEndCouldNotBeUpdated()` stores status CouldNotUpdate on the sync row. The status is stored as its string name (`EventCompetitionConfiguration.cs:18`).
  - The refresh outcome is replaced with Skipped / `EndCouldNotBeUpdated`.
- `:206`: the outcome is written into `CalculationInputsJson.finalWomRefresh` with the existing AU18 serializer. AU18 readback (`ReadFinalWomRefresh`, `:311-324`) accepts the new value because it is a defined enum member.
- Enum (`src/Bingo.Application/Events/IEventCompetitionSynchronizationService.cs:17-20`): members 0–7 are unchanged. `EndWindowUnmatched = 8` and `EndCouldNotBeUpdated = 9` are added at the end with explicit numbers. A contract test pins all ten names and numbers.
- Official WOM data: the publication uses `readiness.Placements` (EHB) and the cache already stored. Nothing is fetched, so the last fetch before the actual end stays official.
- **Luck: no change was needed, and none was made** (`git diff b126c55 4b9f156` touches no Luck file). `ReadLuckAsync` (`PublicStatsService.Luck.cs:19-38`):
  - It returns the saved checkpoint with its original values, `CalculatedAt` and `FetchedAt`.
  - Stale is set only when `FetchedAt` is at least 1 hour old.
  - With no checkpoint it returns "unavailable", not zeros.
  - The checkpoint's lifecycle fingerprint uses only `ActualStartedAt` (`:42-45`), so the early end does not invalidate it.
  - Luck therefore shows the age of the last pre-end fetch and invents no fresh values or zeros.
- **Publication is never blocked by the WOM state.** No `FinalReviewBlocker` refers to WOM. Readiness only reports the status (`:100-103`), and the fallback branch only records data. The publication test asserts `CanFinalize` and `Published`. A concurrent sync-row write could still cause a serialization conflict ("changed in another session"), like any concurrent edit. That is a retryable conflict, not a WOM blocker.
- **Exposure:**
  - `FinalReviewReadiness.WomEndUpdateStatus` and `FinalizationOperationResult.WomEndUpdateStatus` (`IEventFinalizationService.cs:6,20`).
  - `EventCompetitionView` already carries `EndUpdateStatus`/`TargetAt` (`EventCompetitionSynchronizationService.cs:478`) and now the new skip reasons.
  - No page or `.cshtml` file changed. The existing Finalize history label falls through to the generic "Skipped" for the new reasons (`Finalize.cshtml.cs:54-67`). That is consistent with "backend only".
  - The design-gaps register row exists (`DELIVERY_PLAN.md:1125` at `4b9f156`).

## 5. ID-only links and reopen

- **ID-only (traced):** with no management row (or one that can't write), `QueueUpdateAsync` rejects the pending end with "MissingCredential" or the credential code (`EventCompetitionManagementService.cs:255-259` and the following lines). Rejected counts as unmatched, so after an early end the fetches stay suppressed and publication always takes the fallback. If the worker never runs, the status stays Pending, which also counts as unmatched.
- **Reopen and republish (traced, untested):**
  - Once any finalization row exists, `EndUpdatesStoppedAsync` (`EventCompetitionManagementService.EndUpdate.cs:8-10`) stops all end updates, including after reopen.
  - The worker only picks up Pending rows (`EventCompetitionManagementService.cs:452-456`).
  - Resume is refused when finalization history exists (`EventLifecycleService.cs:170-171`).
  - So after reopen the status stays CouldNotUpdate. Fetches stay suppressed with reason `EndCouldNotBeUpdated`, and a republish takes the fallback again, recording reason 9 again.
  - An update cannot succeed after reopen. That matches WA-2 item 4 ("retries may continue until results are published") and keeps the official WOM data stable across republish. It is still worth a planner note (finding N3).

## 6. Findings

Confirmed (traced in code):

- **Low — L1: success that lands just before publication is still recorded as a skipped final fetch.**
  - **What the code does:** the final fetch runs before the publication transaction (`EventFinalizationService.cs:131-139`). The unmatched check runs later, inside the transaction (`:172-181`).
  - **Failure scenario:** at 22:10:00 the admin publishes while the end is Pending. The final fetch is skipped (reason 8). At 22:10:01 item 3's attempt succeeds and commits. Then the publication transaction reads the row: it is matched, so there is no CouldNotUpdate. The publication records Skipped/`EndWindowUnmatched` while the status says Succeeded.
  - **Effect:** the official WOM data is the last pre-end fetch, although a correct-window final fetch was briefly possible. No post-end data gets in. The stored record is accurate but slightly inconsistent. This is acceptable because the window is narrow.
- **Note — N1: reason text in feedback and history.** The fallback outcome has no message (`:177`). `PublicationFeedback`/`PublicationDetail` (`:287-338`) therefore write "Final-review competition refresh skipped: no detail" into the operation feedback and the lifecycle history. The structured reason is stored correctly in `CalculationInputsJson`, so the new UI can bind it. Only the free-text history line loses the reason. A message such as "WOM end could not be updated" would make the audit trail self-explanatory.
- **Note — N2: the manual Fetch message is generic.** When the WOM page Fetch is skipped for reason 8 or 9, the message is the generic cooldown text "The cached competition result is still within its refresh window." (`EventCompetitionSynchronizationService.cs:228`). This is per D4, since the structured reason is in the result. It is misleading on the current page but not in scope.
- **Note — N3: an override erases the Rejected status.** `MarkEndCouldNotBeUpdated` (`EventCompetitionSynchronization.cs:51-54`) replaces Rejected with CouldNotUpdate. `EndUpdateErrorCode` (for example `COMPETITION_START_DATE_AFTER_END_DATE` or `MissingCredential`) is kept, so the cause can still be read. Reopen never restarts retries (§5). The planner may want to state explicitly that this is the intended product behaviour.
- **Note — N4: an ordinary end with stale metadata also triggers the fallback.** `HasUnmatchedEnd` compares the stored metadata, not only the pending status. An event that ended normally whose stored WOM end differs from the configured end therefore also takes the fallback and is marked CouldNotUpdate. An example is a link saved under the old 5-minute tolerance. After item 1, such a competition would fail the exact window check at fetch time anyway, so this only changes the reason. It is arguably the correct label.

Unverified:
- None of the tests were executed by me. The evidence claims 4 PostgreSQL cases passed plus the extended item-3 test, 6 old-AU18 readback cases, the enum contract test, a clean Release build and `git diff --check`. It records no command lines or output counts per test.

## 7. Acceptance coverage

| Requirement | Test | Asserted? |
|---|---|---|
| No manual, scheduled or final fetch after the actual end while unmatched | `Au20PublicationFallbackPreservesPreEndCacheAndLuckAge` (pending/id-only/rejected): manual, `ProcessDueAsync` and `RefreshForFinalReviewAsync` all skip with reason 8, `provider.Calls == 0`, readback `CanRefresh=false` | Yes (claimed passed). There is no control showing the scheduled fetch would otherwise have been due, but 2 h past the hourly slot it should be. |
| A fetch already in flight at early end can't store post-end data | `Au20FetchAlreadyInFlightCannotOverwritePreEndCacheAfterEarlyEnd`: end committed during the provider call, ErrorKind "EndWindowUnmatched", Luck values and `FetchedAt` unchanged | Yes (claimed) |
| Success, then fetches resume with the correct window | `Au20EndUpdateBackoff…` (item 3) + one line: `RefreshForFinalReviewAsync` succeeds | Partly: final-review path only; scheduled and manual resumption not asserted |
| Publication fallback: CouldNotUpdate persisted, reason 9 stored, no provider call, cache generation and pre-end EHB official, publication not blocked | Fallback theory (3 cases) | Yes (claimed) |
| Luck freshness after fallback: same `FetchedAt`/`CalculatedAt`/values, Stale, age over 2 h, checkpoint payload unchanged | Fallback theory | Yes (claimed) |
| ID-only after early end takes the fallback | Fallback theory "id-only" (real worker → Rejected MissingCredential) | Yes (claimed) |
| Enum append-only with explicit numbers | `Au18StoredEnumNamesAndNumbersRemainStableAndAu20OnlyAppends` | Yes (claimed) |
| Old AU18 published outcome still readable | Existing readback cases (evidence: 6 passed) | Claimed, not re-run |
| Update succeeds just before publication (L1 race) | — | Not tested |
| Reopen/republish with CouldNotUpdate | — | Not tested |
| No new display on current pages | No Web/page changes in the diff (traced) | n/a (traced) |

## 8. Verdict

**PASS with notes.** Every fetch path is gated on the unmatched-end state, including a fetch already in flight. The publication fallback stores CouldNotUpdate and an appended, explicitly numbered reason, keeps the pre-end data and Luck age, and never blocks publication. The remaining points are a narrow, harmless race (L1), wording (N1, N2), and untested reopen and resumed-scheduled-fetch paths.

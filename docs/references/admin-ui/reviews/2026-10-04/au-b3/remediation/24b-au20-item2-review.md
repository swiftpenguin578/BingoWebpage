# B3 / AU20 item 2 review: early end, Resume and pending WOM end update (`2d74bde`)

Reviewer: Claude sub-agent, read-only (git show/grep only; nothing built or run). Date: 4 October 2026.
Scope: commit `2d74bde` "AU20 item 2: persist local end and Resume update requests", which adds migration `20261004112050_AddCompetitionEndUpdateState`.
Sources: brief `23-codex-brief-b3-au20.md` item 2 and section 5; ticket `DELIVERY_PLAN.md` AU20 points 2–3 (at `b126c55`); `08-decisions.md` WA-2 items 2–3 (with the corrected Resume rule) and the B3 brief decisions; evidence file `docs/references/admin-ui/reviews/2026-10-04/au-b3/item2-local-end-resume.md`.

All file:line references are at `2d74bde` unless stated otherwise.

## What the commit does

- **Early end** (`EventLifecycleService.EndNowAsync`, `src/Bingo.Infrastructure/Events/EventLifecycleService.cs:117-152`): if the click is before the configured end (or the event has no configured end), it calls the new `BingoEvent.EndEarly(now)` (`src/Bingo.Domain/Events/BingoEvent.cs:416-422`) and then `RecordPendingEndUpdateAsync` (`:140-141`). A click at or after the configured end keeps the old path (`EndEvent(configuredEnd)`), with no pending update.
- **`EndEarly`** first sets the actual end to the precise click instant (`EndEvent(clickedAt)`, which also sets the upload cutoff to actual end + 30 minutes). It then sets the configured end (`EventEndsAt`) to the click instant rounded up to the next whole minute, computed on UTC ticks: if `ticks % TicksPerMinute == 0` the value is kept, otherwise it moves to the next minute.
- **Resume** (`ResumePrematureEndAsync`, `:154-206`) no longer reuses a still-future retained end. It always requires an explicit replacement end that is (in order): present, after `now`, on a whole minute whose UTC minute is a multiple of 5 (`:182`), and after the configured start. The existing singleton, overlap, authorization and finalization-history checks are kept. The replacement is stored as `EventEndsAt` unchanged (no rounding), and `RecordPendingEndUpdateAsync` is called (`:198`). The WOM exact-window check that item 1 had put in `FindLifecycleOverlapAsync` is removed, so a WOM mismatch no longer blocks Resume.
- **`RecordPendingEndUpdateAsync`** (`:417-423`) locks the event's synchronization row with `SELECT … FOR UPDATE` inside the same transaction and calls `RequestEndUpdate(item.EventEndsAt, now)`. `RequestEndUpdate` (`src/Bingo.Domain/Integrations/WiseOldMan/EventCompetitionSynchronization.cs:38-45`) does nothing when `CompetitionId` is null. Otherwise it overwrites the target and request time, sets the status to `Pending`, and clears the error code.
- **New persisted state** on `event_competition_synchronizations`: `end_update_status` (string, max 30, not null, existing rows default `'NotRequired'`), plus nullable `end_update_target_at`, `end_update_requested_at` and `end_update_error_code` (max 100). The enum `EventCompetitionEndUpdateStatus` has explicit values 0–4 but is stored as **strings** (`HasConversion<string>()`, `EventCompetitionConfiguration.cs`).
- **Readback**: `EventCompetitionView` gains the four end-update fields, filled from the row (`EventCompetitionSynchronizationService.cs:457-462`). There is no WOM call in this commit; item 3 performs the update.
- **Pages**: `Manage.cshtml:336` replaces the two conditional Resume instruction sentences with one ("Choose a future replacement end and provide a reason…"), with a Danish translation. `Manage.cshtml.cs:425` sets `ResumeRequiresReplacement = true` unconditionally, so the existing replacement-end input is always `required`. No new state, badge or field is displayed.

## Checks against the brief's questions

| Question | Finding |
|---|---|
| Early end rounds up to the next whole minute | Yes. 21:59:55 becomes 22:00:00 (`BingoEvent.cs:420-421`). Traced and tested. |
| A click exactly on a whole minute does not move a minute | Yes, for a click exactly on a tick-zero minute (22:00:00.0000000 stays 22:00:00). Traced and tested (`Au20EarlyEndRetainsActualPrecisionAndQueuesCeilingMinute`, case `(0,0,0)`). See finding L2 for the sub-microsecond edge. |
| Sub-second click just after a minute, e.g. 22:00:00.0004 | Becomes 22:01:00. That is the literal "round up" rule: the click is not on a whole minute. Traced, **not tested**. |
| Actual end keeps the precise click time | Yes, `ActualEndedAt = clickedAt` and the transition `EffectiveAt = now`. In PostgreSQL it is truncated to microseconds, and the test asserts the truncated value. Traced and tested. |
| WOM end target equals the rounded value | Yes. `RequestEndUpdate(item.EventEndsAt!.Value, …)` is called after `EndEarly`, so the target is the rounded configured end. Tested (`pending.EndUpdateTargetAt == saved.EventEndsAt`). |
| Which clock | The injected `TimeProvider` (`time.GetUtcNow()`, `:134`). Tests use `TestClock`. |
| Timezone independence | The rounding uses UTC ticks after `ToUniversalTime()`. Every real-world offset is a multiple of 15 minutes, so a UTC whole minute is also a local whole minute. Resume's 5-minute check is on the UTC minute, while the page's existing check is on the local minute (`Manage.cshtml.cs:485`). The two agree for every offset that is a multiple of 5 minutes. A test that supplies a +02:00 replacement passes. |
| Resume uses a validated future replacement, not the click time, no rounding | Yes (`:176-184`, `:197`). A value that is missing, now, past, not on a 5-minute step, or has sub-minute ticks is refused. Tested, but only `Succeeded == false` is asserted, not which rule refused it. |
| Resume WOM target = the replacement end | Yes (`RequestEndUpdate(item.EventEndsAt)` after `ResumePrematureEnd` set it). Tested (`Au20ResumePersistsReplacementAndPendingUpdateDespiteProviderMismatch`). |
| No WOM call inside the lifecycle transaction | Confirmed. No provider client is used in `EventLifecycleService`. The tests construct it with `null!` for signup lifecycle and no WOM client, and assert that no management operation rows exist. |
| Pending update in the same transaction as the lifecycle change | Confirmed. The sync row is loaded with `FOR UPDATE` and modified through the tracked entity in the same Serializable transaction, and one `SaveChangesAsync` and one `CommitAsync` follow (`:145-146`, `:200-201`). A crash cannot leave an ended event without its pending row, or a pending row without the end. |
| Can WOM-related activity still block or fail the local action? | Yes, in a narrow race. See finding M1. |
| Repeated early end → Resume → early end | Each action overwrites target, request time and status (`Pending`, error cleared), so the row always holds the latest target. There is **no generation or version counter** for end updates. Item 3 (`fa24164`) guards against stale outcomes by comparing values: `CompleteEndUpdate(target)` and `RejectEndUpdate(target, …)` only act when the status is `Pending` and the stored target equals the attempt's target. An older attempt with a different target therefore cannot mark the newer request done. If two requests have the same target value (possible only within one minute), completing either one is correct because the WOM end would equal it. Whether an older in-flight provider write can leave WOM at a stale end after the newer target is recorded belongs to the item 3 review. Here the status stays `Pending`, so a later attempt would correct it. |
| Event with no WOM competition | No synchronization row means nothing is recorded. A row with `CompetitionId = null` is a no-op (`RequestEndUpdate` returns early). The status stays `NotRequired`. Traced, **not tested** directly. |
| ID-only external link (no verification code) | Recorded as `Pending` like any link. The early-end test uses exactly such a row (external link, no management row) and asserts `Pending`. Item 3 later turns it into `Rejected/MissingCredential` and item 4 into the fallback. Traced and tested for this commit's part. |
| Migration Up/Down | See "Migration" below. Correct. |
| Manage.cshtml change | Wording correction only, plus the input always being required. No new display. |

## Findings

### M1 (Medium): a concurrent WOM refresh can make an early end or Resume fail with an unhandled database error

- **What the code does.** `EndNowAsync` and `ResumePrematureEndAsync` run in a Serializable transaction. Its snapshot is taken at the first statement, the account authorization query (`:122` / `:160`). The transaction then waits on the global lifecycle advisory lock (`EventCurrentBoundary.LockAsync`, `:131` / `:170`), and only after that reads the synchronization row with `SELECT … FOR UPDATE` (`:419-421`). The catch blocks (`:150-152`, `:204-206`) handle `DbUpdateConcurrencyException`, `InvalidOperationException` and `DbUpdateException`. They do **not** handle `PostgresException` 40001/40P01, unlike `StartNowAsync` (`:111`).
- **Why it fails.** The WOM refresh worker updates the same row: `AcquireLeaseAsync` (`EventCompetitionSynchronizationService.cs:247-277`) and `FinalizeLeaseAsync` (`:303-…`) lock the event row and then the synchronization row, and write lease, attempt and due-time fields. From item 3 on, the management worker also writes this row. PostgreSQL behaviour under Serializable: if another transaction commits an update to that row after our snapshot, our `SELECT … FOR UPDATE` raises SQLSTATE 40001 ("could not serialize access due to concurrent update"). EF Core does not wrap exceptions from query execution in `DbUpdateException`, so the 40001 escapes the service. `OnPostEndEventAsync` (`Manage.cshtml.cs:133-140`) does not catch it either, so the admin gets an error page. The lock order is also inverted: the refresh takes the event lock, then the sync lock; the lifecycle action takes the sync lock, then writes the event row. That inversion can also produce a 40P01 deadlock, which surfaces at `SaveChanges` as `DbUpdateException` and returns a clean "could not be ended" message.
- **What is required.** WA-2 item 2 says early end and Resume "always succeed locally … never blocked on WOM", and ticket point 2 says "WOM availability … never blocks the local action". This commit introduces the coupling: before it, these actions never touched the synchronization row.
- **Failure scenario.** At 21:59:55 an admin clicks End while the hourly refresh for the same event is committing its lease (or another lifecycle action holds the advisory lock, which widens the window). The early end throws 40001, nothing is saved (the action is atomic, so data stays correct), and the admin sees an error and must click again. The retry then succeeds, but at a later click time, so the actual end and possibly the rounded minute change.
- **Traced vs inferred.** The code path and the missing catch are traced. That PostgreSQL raises 40001 here, and that EF does not wrap query exceptions, is inferred from documented PostgreSQL/EF behaviour. No test exercises it.
- **Suggested fix.** Catch `PostgresException` 40001/40P01 in both methods, as `StartNowAsync` does. Preferably also make the pending-update write immune to refresh contention, for example by taking the sync-row lock before other reads, aligning the lock order (event row first), or retrying the transaction once internally.

### L1 (Low): early end on an event with no configured end, or with an end that is not on a whole minute

- **What the code does.** The early branch (`:138`) also runs when `EventEndsAt` is null, and then *sets* a configured end. Before this commit a null end stayed null. The branch also runs for any click before a configured end that is not on a whole minute. Example: configured end 22:00:30 and a click at 22:00:10 give a new configured end and WOM target of 22:01:00, which is *later* than the original configured end.
- **Why it is Low.** Schedule save and the page enforce 5-minute local times (`Schedule.cshtml.cs:158`), so a current Live event should always have an aligned end. Legacy or imported rows are the only plausible source. Inferred; not tested.

### L2 (Low/Note): rounding uses 100-ns ticks, while the stored actual end has microsecond precision

- **What the code does.** `EndEarly` rounds on full .NET ticks (`BingoEvent.cs:420`). PostgreSQL stores microseconds and Npgsql truncates; the test confirms truncation.
- **Failure scenario.** A click at 22:00:00.0000005 stores the actual end as 22:00:00.000000 but sets the configured end and WOM target to 22:01:00. The stored data then shows an end exactly on a minute that was nonetheless rounded up by a minute.
- **Why it is Low.** The window is under 1 µs per minute. The fix is trivial: truncate the click to microseconds before rounding. Traced; not tested.

### L3 (Low): the Resume input is prefilled with an invalid default after an early end

- **What the code does.** `Manage.cshtml.cs:426` prefills `ReplacementEventEndsAt = item.EventEndsAt ?? now + 1h`. After an early end, `EventEndsAt` is the rounded past minute (for example 22:00), which is in the past and often not on a 5-minute step.
- **Result.** The admin always has to change the prefilled value, and submitting it unchanged is refused. Pre-existing pattern, but now always hit because Resume always requires a replacement. `ResumeRequiresReplacement` is now a constant `true`, which is dead conditional logic. The old conditional instruction strings remain unused in `SharedResource.da.resx`.
- **Requirement.** No requirement is violated, and no new display was added. UI integration will replace this page anyway. Traced.

### N1 (Note): the original configured end is overwritten without a record

`EndEarly` replaces `EventEndsAt` (for example 22:59 → 22:00). The audit "after" JSON (`AddTransitionAndAudit`, `:396-400`) records state, `ActualStartedAt` and `ActualEndedAt`, but neither the old nor the new configured end. The prior scheduled end is therefore no longer recoverable from history. This matches the existing Resume behaviour (which also overwrites without recording), and the rule says to "persist the same configured end locally". Flagged for awareness only. Traced.

### N2 (Note): end-update status is stored by name

The enum has explicit numbers 0–4, but the column stores the **member name** (`HasConversion<string>`, max length 30). Unlike the AU18 skip-reason concern, renumbering would be harmless here, but **renaming** a member would orphan stored rows. The evidence file's "explicit values" claim is accurate but beside the point. Recommend recording "never rename end-update status members" next to the AU18 rule in item 6 docs.

### N3 (Note): the Resume validation test is weak on reasons

`Au20ResumeRequiresExplicitFutureValidatedEndAndDoesNotRound` asserts only `Succeeded == false` for each invalid input. The `now` case would be refused by the "future" rule, which does not show which rule refused each value. The `+5 min + 7 ticks` case does exercise the sub-minute check, because the other rules pass for it.

## Migration

- **Up** (`20261004112050_AddCompetitionEndUpdateState.cs`) adds four columns to `event_competition_synchronizations` only. `end_update_status` is `character varying(30) NOT NULL DEFAULT 'NotRequired'`, which backfills existing rows to `NotRequired`. The three other columns are nullable with no backfill, and nothing else changes.
- **Down** drops exactly those four columns. Any pending request is lost on rollback; the evidence documents this. Correct.
- **Snapshot and designer** both contain the four properties with matching types, lengths, nullability and column names (designer `:3626-3644`, snapshot `+3620-3641`). The designer is attributed `[Migration("20261004112050_AddCompetitionEndUpdateState")]`, `ProductVersion` 10.0.9. The previous migration is `20261004093705_ClearLegacyDropTileEhbOverrides`, which matches the test's Down target. The database-level default `'NotRequired'` is not recorded in the model, which is normal EF behaviour for an added required column. The model's CLR default (enum 0 → "NotRequired") gives the same value, so no model drift results.
- **Population test** `Au20EndStateMigrationDownAndUpPreservePopulatedHistory` seeds an event and a linked synchronization row, migrates Down to the previous migration (the row count stays 1), migrates Up again, and asserts the `NotRequired` backfill, null fields, the unchanged fingerprint and the unchanged event end. This covers both Up on a populated database and Down. Claimed in the evidence as passed. Not re-executed by this reviewer.

## Confirmed vs unverified

- **Confirmed by tracing:** the rounding rule and its UTC tick math; the precise actual end; the target equal to the rounded value; the Resume rules and the target equal to the replacement; no provider call; one transaction; overwrite-latest semantics; item 3's value-based stale guard; no-competition no-op; ID-only pending; migration shape and consistency; Manage change limited to wording plus always-required.
- **Inferred, not executed:** M1's 40001/deadlock behaviour, and the L1/L2 edge cases.
- **Test results:** taken from the evidence file (11 focused cases passed, plus 4 race cases, plus a clean Release build). Not re-run, per reviewer rules.

## Acceptance coverage (item 2)

| Acceptance point | Executed assertion in this commit? |
|---|---|
| Deterministic sub-minute early end (21:59:55 + 7 ticks → 22:00, precise actual end, cutoff = actual + 30 min, transition `EffectiveAt` = actual) | Yes (`Au20EarlyEndRetainsActualPrecisionAndQueuesCeilingMinute` (55,7,1)) |
| Minute-boundary early end (exact 21:59:00 stays) | Yes (same theory, (0,0,0)) |
| Click just after a minute (e.g. 22:00:00.0004 → 22:01) | No |
| WOM target = rounded configured end; status Pending; no management operation created | Yes |
| Non-microsecond input through real PostgreSQL precision | Yes (7 ticks truncated; exact equality after reload) |
| Validated future Resume: missing, now, past, non-5-minute, sub-minute refused; offset-equivalent valid value stored unrounded | Yes (refusal reasons not asserted) |
| Resume target = replacement end, succeeds despite WOM window mismatch | Yes (`Au20ResumePersistsReplacementAndPendingUpdateDespiteProviderMismatch`) |
| ID-only external link: early end succeeds locally and records Pending | Yes (early-end theory uses an ID-only row) |
| Event with no WOM competition: nothing recorded | No (traced only) |
| Repeated early end → Resume → early end keeps the latest target | No |
| Early end/Resume not blocked by concurrent WOM refresh activity | No (and see M1) |
| Migration Up on a populated database + Down | Yes (`Au20EndStateMigrationDownAndUpPreservePopulatedHistory`) |
| No new display on current pages | Traced (wording-only change) |

## Suggested verdict

**PASS with notes.** The rounding, Resume, target, atomicity and migration behaviour match WA-2 and the ticket. M1 should be fixed in a remediation round, because the new sync-row lock lets concurrent WOM refresh activity turn an early end or Resume into an unhandled serialization error, contrary to "never blocked on WOM". L1–L3 and the notes are optional hardening.

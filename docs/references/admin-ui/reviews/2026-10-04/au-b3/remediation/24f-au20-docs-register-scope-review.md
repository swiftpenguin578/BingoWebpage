# 24f — B3 (AU20) review: register, item 6 docs, CURRENT_STATUS, scope and test integrity

Reviewer: Claude sub-agent, read-only (git show/diff/log/grep only; no builds or test runs).
Range: `b126c55..4b9f156` (prerequisite `2649008`, items `52f0af6`, `2d74bde`, `fa24164`, `6625617`, `6d29f2c`, `4b9f156`).
Sources: brief 23 §2–§5, `08-decisions.md` (WA-2, Known UI differences, B1/B2 D2, B3 brief decisions, Design-reference gaps register), AU20 ticket at `b126c55`, evidence under `docs/references/admin-ui/reviews/2026-10-04/au-b3/`.

**Suggested verdict: PASS with notes.** The register, docs and status are accurate in substance and the scope is clean; the findings are stale "pending" markers outside the three named docs, one register row missing for the Overview Resume difference, and labelling/recording gaps. No weakened tests.

---

## 1. Design-gaps register (`2649008` + `4b9f156`)

Location: `DELIVERY_PLAN.md` "### Bindings not shown in the design references", directly before "Proposed reference corrections — RC05–RC11" (UI integration/RC section). Columns are exactly: What it is / Page and design reference / Source ticket/decision / Binding ticket / Open decisions. The intro text records the user's standing rule and says the UI integration plan must make every row a required inventory item. This matches the 08-decisions approval.

Row-by-row comparison with `08-decisions.md` "Known UI differences" (traced):

| 08-decisions bullet | Register row | Result |
| --- | --- | --- |
| Review: blocked-approval link to earlier upload, queue/filter context | Row 1, RC07 / BR-10 | Present, accurate |
| Final Review: no "another event is current" row | Row 2, RC08 / BR-10 | Present, accurate |
| Participants: manual-team members in lists/waiting positions | Row 3, P-1 | Present, accurate |
| Audit single-entry read with "This entry isn't available" | Row 4, RC06 / WA-5 | Present, accurate |
| Audit event dropdown with hidden events marked | Row 5, RC06 / WA-5 | Present, accurate |
| Final Review WOM outcome per design (unsuccessful-only), next eligible time data only | Row 6, RC08 / BR-10 | Present, accurate |
| Board override set/change/reset + explicit change-override intent | Row 7, RC05 / BR-10 | Present, accurate |
| Catalogue layout/rate decisions (by reference) | Rows 8–9, link to "Catalogue decisions — AU23, CAT-1 and WA-5" (anchor still valid) | Present, accurate |
| WOM end could not be updated (backend only, placement open) | Row 10, WA-5 / OS-1 / BR-10 "as selected by the UI integration plan"; open decision stated | Present, accurate |
| AU20 structured outputs (added in `4b9f156`) | Row 11: fetch eligibility, AU18 skip reason, next permitted time, typed credential, current-operation identity/phase/next attempt; WA-5 / RC09 | Present, accurate |

"Checked and consistent" items in 08-decisions need no row; correctly omitted.

### R1 (Low) — No register row for the Overview Resume / early-end difference from `Overview.dc.html`
- **Code today:** `EventLifecycleService.cs:177-183` (at `4b9f156`) refuses Resume without an explicit future replacement end, even when the stored end is still in the future; `BingoEvent.EndEarly` (`BingoEvent.cs:416-422`) moves the configured end to the click time rounded up to the minute.
- **Reference:** `docs/references/admin-ui/Overview.dc.html` Resume dialog asks for a new end only when the stored end has passed (`needsUntil: e => !e.dates.ends || e.dates.ends <= TODAY`, hint "The scheduled end (…) has passed"). Its early-end action sets only the actual end.
- **Requirement:** WA-2 (corrected Resume rule), brief 23 §3 item 2, and the register rule: every intentional difference from a reference gets a row.
- **Failure scenario:** an admin ends early by mistake at 21:59:05 (configured end becomes 22:00:00) and presses Resume at 21:59:30. A UI bound faithfully to the reference hides the end field because the stored end is still in the future, so the server rejects the request with "Choose a future replacement event end." and the admin has no field to fix it. Legacy rows ended early before AU20, where the configured end is still in the future, hit the same issue. The window is small, which is why this is Low.
- **Fix:** add a row (Overview, `Overview.dc.html`; source WA-2/AU20 item 2; binding OS-1; open decision: dialog text and always-shown end field). Traced.

### R2 (Low) — The end-update states besides CouldNotUpdate are not named in the register
- **Code today:** `EventCompetitionView` exposes `EndUpdateStatus` (NotRequired/Pending/Succeeded/Rejected/CouldNotUpdate), `EndUpdateTargetAt`, `EndUpdateRequestedAt` and `EndUpdateErrorCode` (`IEventCompetitionSynchronizationService.cs`). `FinalReviewReadiness.WomEndUpdateStatus` and `FinalizationOperationResult.WomEndUpdateStatus` are new (`IEventFinalizationService.cs`).
- **Register:** row 10 names only "WOM end could not be updated" plus a generic "read-model/service output". Row 11 covers fetch/credential/operation outputs, not the end-update fields.
- **Risk:** the binding inventory may bind only CouldNotUpdate and leave out "end update pending" or "rejected (safe code)" during Live and Final Review. These are the states an admin could still act on, for example by supplying a code.
- **Fix:** widen row 10's "What it is" to list the pending, rejected and code fields and the Final Review readiness/finalize result fields. Traced.

## 2. Item 6 documentation (`4b9f156`)

The three early-end texts (DATA_MODEL §lifecycle, TECHNICAL_ARCHITECTURE early-end section, PRODUCT_REQUIREMENTS §7.1) no longer say "pending AU20". They now describe implemented behaviour. Spot-checks against the code:

| Doc claim | Code (at `4b9f156`) | Result |
| --- | --- | --- |
| Early end rounds the configured and WOM end up to the next minute; an exact minute is unchanged; actual end stays precise | `BingoEvent.EndEarly` uses ceiling ticks with a remainder of 0 kept; `EndEvent(clickedAt)` keeps the actual end; `EventLifecycleService.cs:138-143` takes this path only when `now < EventEndsAt` | Accurate |
| Resume always needs a validated future replacement end in schedule increments, with no rounding | `EventLifecycleService.cs:177-185`: null is refused, so are past values and values not on a 5-minute/whole-minute UTC boundary; the value is used as given. Test `Au20ResumeRequiresExplicitFutureValidatedEndAndDoesNotRound` covers null with a future stored end | Accurate. A 5-minute check on UTC equals a local 5-minute check for every real offset (all offsets are multiples of 5 minutes) |
| Both actions record a pending end update for a linked competition | `RecordPendingEndUpdateAsync` with row lock, then `RequestEndUpdate` (no-op if no competition) | Accurate |
| First attempt at once, then 1/2/4/8/16 min, then every 30 min; a later provider retry time is honoured; repeated passes don't reset the due time | `EndRetryAtAsync` (`…EndUpdate.cs`): `<=1→1, 2→2, 3→4, 4→8, 5→16, _→30`, max with provider RetryAt; the unchanged pending operation returns early (`EventCompetitionManagementService.cs` "must not erase a persisted backoff"); the claim checks `NextAttemptAt > now` | Accurate |
| Stops at official publication | `EndUpdatesStoppedAsync` (any `EventFinalizations` row) is checked in queue, claim (cancels), reconcile and receipt | Accurate |
| 429 retryable; missing/invalid credentials, 401/403, 404, other Validation incl. `COMPETITION_START_DATE_AFTER_END_DATE` stop with a safe code; Unknown keeps reconciliation | `RejectPendingEndAsync` on missing/unusable credential; `FailOperationAsync` calls `RejectEndUpdate(code)`; Unknown is exempt from the 3-attempt cap for pending end | Accurate (detailed status mapping is owned by the item-3 reviewer) |
| Suppress fetches after the actual end while unmatched; at publication record CouldNotUpdate and Skipped/EndCouldNotBeUpdated; keep the pre-end cache | `EventCompetitionSynchronizationService.cs:325,349`; `EventFinalizationService.cs:172-181` | Accurate |
| Enum members recorded | DATA_MODEL lists skip reasons 0–7 unchanged plus `EndWindowUnmatched=8`, `EndCouldNotBeUpdated=9`; end-update statuses listed by name | Accurate, matches `IEventCompetitionSynchronizationService.cs:17-21` |
| Migration backfills NotRequired; Down drops only the new fields | `20261004112050_AddCompetitionEndUpdateState.cs`: 4 AddColumn with default "NotRequired" / 4 DropColumn | Accurate |
| Accepted snapshot limit with no compensation logic | PRODUCT_REQUIREMENTS (WOM section): "last snapshot within the configured window … no compensation or attribution logic" | Accurate; matches WA-2 known limit |
| Retry schedule recorded as a planner technical choice | TECHNICAL_ARCHITECTURE AU20 paragraph and item-6 evidence; not labelled as user approval | Accurate |

### R3 (Low) — "Pending" AU20 markers remain outside the three named docs and contradict the new AU20 heading
At `4b9f156`, the AU20 ticket heading says "implemented and focused checks passed; external review pending", but:
- `DELIVERY_PLAN.md:1086` (AU summary table, AU20 row) still ends "…; pending implementation".
- `DELIVERY_PLAN.md:1680` WOM-01 says "AU20 exact UTC windows/code-bearing detach pending", and `:1683` WOM-02 says "AU20/RC09 pending".
- `FUNCTIONAL_CONTRACTS.md:164`: "AU20 implementation is pending."
- `docs/references/admin-ui/FUNCTIONALITY_CHANGES.md:130`: "The approved early-end/Resume/retry/fallback rules are pending AU20". `:186` "(AU20/RC09, pending)" is partly still right, because RC09 is still pending.

The brief named only DATA_MODEL, TECHNICAL_ARCHITECTURE and PRODUCT_REQUIREMENTS, so this is not a brief violation. It still leaves the owning plan contradicting itself; a later reader of the summary table would think AU20 has not started. Fix: change these to "implemented (AU20), review pending" and keep the RC09 reference part pending. Traced.

### N1 (Note) — The other-4xx classification is recorded only in TECHNICAL_ARCHITECTURE and evidence
Brief §3 item 3 told Codex to report other 4xx codes to `/root` before classifying them. The evidence (`item3-end-update-retries.md`) records the Codex planner's resolution: 429 retries, while 401/403, 404 and other Validation responses stop. It is labelled as a planner technical resolution, not as user approval, which is correct. It is not in `08-decisions.md`. The Claude planner should confirm or record it there, as was done for the retry spacing.

### N2 (Note) — End-update status is stored by name
`EventCompetitionConfiguration.cs` uses `HasConversion<string>()`, and `item2-local-end-resume.md` says "values are explicit: NotRequired=0…". The stored form is the name, so renaming a member would break stored rows. DATA_MODEL lists the names but does not state the never-rename rule that it states for skip reasons. Consider adding one sentence.

## 3. CURRENT_STATUS.md (`4b9f156`)

- Accurate: baseline `b126c55`, prerequisite `2649008`, six item commits, stop point ("Stopped after B3 item 6 for external Claude independent review"), "No separate user product approval is claimed", "not independently reviewed or manually accepted", R1/R3 gates and operator migration counts kept.
- No false Claude-review or user-approval claims. "Claude merge scope PASS per supplied B3 assignment" is backed by brief 23 §1 ("scope check PASS"). "B1 follow-ups … and B2 5545845 passed Claude source recheck" matches review 22.
- **R4 (Low):** execution results ("final AU20-focused integration run 44/44 …; populated migration Up/Down/backfill passed; Release build zero warnings/errors"; "existing Luck freshness verified") are stated as facts and not labelled worker-reported. The replaced B1/B2 text used "Worker reports …". The document is worker-authored, but the user's rule asks for explicit labelling. Fix: prefix with "Implementer reports (not independently rerun):".
- **"Unrelated Stats fixture failure" — what it is (traced):** test `StatsPass4FiveByFiveObjectivesFinalizationArchiveAndUnfinalizationRetainOfficialHistory` (`tests/Bingo.IntegrationTests/StatsPass4BoundaryIntegrationTests.cs:100-125`).
  - **How it fails:** the fixture (`StatsPass4QueryIntegrationTests.cs:882-888`) sets the event to end 10 h after the clock's "now" (actual start −1 h, duration 11 h). The test advances 12 h, so it is 2 h past the configured end, then calls `EndNowAsync` and asserts a `submission-window` blocker, which means it expects uploads still open.
  - **Why it is not AU20:** because `now >= configured end`, `EndNowAsync` takes the unchanged `else item.EndEvent(effectiveEnd)` branch with `effectiveEnd = configuredEnd`. The cutoff is configured end + 30 min, already past, so there is no blocker and line 125 fails. AU20 did not change that branch.
  - **Where it came from:** the behaviour was introduced by `444bfc4` "Fix manual event end effective boundary" (3 October, F6, an ancestor of `b126c55`), which changed `EndEvent(now)` to `EndEvent(configuredEnd)` for late manual ends. The test fixture was never adjusted.
  - **Conclusion:** the failure exists at the baseline and is unrelated to AU20. This is traced from source; neither Codex nor this review executed it at `b126c55`. The status wording "expects an open submission window after configured cutoff" is accurate. The test is currently red in the broad suite and needs a small fixture fix in its own ticket, for example ending before the configured end or asserting the late-end semantics.

## 4. Scope (`b126c55..4b9f156`, 46 files)

- **Docs (12):** CURRENT_STATUS, DATA_MODEL, DELIVERY_PLAN, PRODUCT_REQUIREMENTS, TECHNICAL_ARCHITECTURE; `au-b3/` item1–item6 evidence and `supplied-brief.md`, which is byte-identical to `review-notes/23-codex-brief-b3-au20.md`.
- **Application (3):** `IEventCompetitionManagementService.cs`, `IEventCompetitionSynchronizationService.cs`, `IEventFinalizationService.cs`. These are WOM sync/management and finalization outcome contracts.
- **Domain (3):** `BingoEvent.cs` (EndEarly, lifecycle), `EventCompetitionManagement.cs`, `EventCompetitionSynchronization.cs` (WOM).
- **Infrastructure (7):** `EventCompetitionManagementService.cs` + new `.EndUpdate.cs`, `EventCompetitionSynchronizationService.cs`, `EventFinalizationService.cs`, `EventLifecycleService.cs` (early end/Resume), `EventSignupLifecycleService.cs` (Schedule save), `EventCompetitionConfiguration.cs`.
- **Migrations (3):** `20261004112050_AddCompetitionEndUpdateState` (+Designer) and the model snapshot.
- **Web (4):** `Manage.cshtml`, `Manage.cshtml.cs`, `Schedule.cshtml.cs`, `SharedResource.da.resx`.
- **Tests (14):** 6 new (`Au20*` ×5 integration, `Au20StoredOutcomeContractTests` in Application.Tests) and 8 modified (see §5).

Everything is inside brief §4 ownership. There are no Board, scoring, catalogue, draft, Accounts, Audit or Luck production files. `git diff --check` is clean.

User-visible changes on current pages (rule: backend only, nothing new):
- `Manage.cshtml:336`: the existing Resume paragraph's two conditional texts are replaced by one text, "Choose a future replacement end and provide a reason…". `Manage.cshtml.cs:425` sets `ResumeRequiresReplacement = true`, so the existing replacement-end input (already rendered) is now always `required`. No new element or state display. This corrects existing copy so the existing handler keeps working under the new Resume rule, which is allowed.
- `Schedule.cshtml.cs:170`: only the error-string-to-field mapping is updated to the new exact-window message. No display change.
- `SharedResource.da.resx`: 3 Danish translations for the changed or new server/page strings. The old keys remain as unused orphans, which is harmless.
- No WOM page or Finalize page change. On the current Finalize page, new skip reasons 8 and 9 fall through to the existing generic "Skipped" text (`Finalize.cshtml.cs:66` `_ => "Skipped"`), so nothing new is shown, consistent with backend only.

## 5. Test integrity (modified pre-existing tests)

| Test (file) | Change | Verdict |
| --- | --- | --- |
| `Slice10Pass102CompetitionSynchronizationTests` (14 tests) | `DateTimeOffset.UtcNow` → fixed `2026-10-04 12:00Z`; "within five minutes" → "configured website UTC window exactly"; `ScheduleEditRejectsAMismatch…` mismatch reduced from +10 min to +1 min | Legitimate. With exact matching, wall-clock sub-microsecond ticks would fail after the PostgreSQL round trip; precision is covered separately by the AU20 non-microsecond test. The +1 min change strengthens the test, since 1 min would have passed the old tolerance. No assertion removed |
| `EventCompetitionManagementIntegrationTests.ManualLinkWaitsForManagedProviderWriteOnTheSameCompetition` | Fixture clock made deterministic; the update result changed from `Succeeded` to `Succeeded=false, Status="Unknown"`; message updated | Legitimate. The double deliberately returns a window shifted +15 min from what was requested, and the new receipt check (`EventCompetitionManagementService.cs` "MismatchedReceipt") correctly refuses to treat that as success. The test's purpose (manual link waits for the managed write lock; link is refused; no sync row) is kept, and the new assertion is stricter. Class made `partial`; `DeleteCalls` counter added for external-delete refusal tests |
| `EventQuarantineExclusivityIntegrationTests` (`RestoreAndStartOrResumeShareCurrentLock`) | Resume passes `other.EventEndsAt` instead of `null` | Legitimate: Resume no longer reuses the stored end. Same value as the old implicit reuse; race assertions unchanged. Evidence: 4/4 passed (worker-reported) |
| `Slice3ScheduledLifecycleIntegrationTests` | Renamed `ResumeReusesRetainedFutureEnd…` → `ResumeUsesExplicitFutureEnd…`, passing `before.EventEndsAt`; the expired-case message changed from "expired" to "future replacement" | Legitimate (WA-2 corrected Resume rule). The retained-end reuse no longer exists; refusing null with a future stored end is asserted in `Au20ResumeRequiresExplicitFutureValidatedEndAndDoesNotRound` |
| `Slice3ScheduleLifecycleIntegrationTests:803` | Message text only | Legitimate. Execution of this specific test is not named in any evidence file (unverified) |
| `StatsPass2LifecycleIntegrationTests` | Resume passes `now.AddDays(2)` (`now` = 2026-07-27 15:00Z, aligned) instead of `null` | Legitimate; frozen-field assertion unchanged. Executed per item 2 evidence (worker-reported) |
| `Slice10Pass101WiseOldManTests` | Only an added test (`Au20NamedHttp400…`) | Addition |
| `StatsPass4ReviewCorrectionsIntegrationTests` (18 lines) | Renamed to `…UsesConfiguredWindow…`; parameter meaning inverted (`matchesConfiguredWindow`); the early end through `EndNowAsync` is replaced by a direct domain `ev.EndEvent(actualEnd)` plus a manual Live→AwaitingFinalReview transition row; the mismatch message now expects the configured end | Legitimate, with a note (N3). Under AU20, `EndNowAsync` moves the configured end to ceil(actual), so the "actual ≠ configured by > 5 min" state the test needs can only be built directly; it represents a legacy early-ended row. Both branches keep all assertions: the mismatch branch still checks that payload, batch and all timestamps are unchanged; the success branch still checks one provider call, the hourly skip, publication and idempotent retry. Assertion count is unchanged |

### N3 (Note, for the item 1/4 reviewers) — The "Real" Final Review test no longer goes through the real early-end path, and its success branch covers a legacy shape
The test now builds the final-review row through the domain, so `EndNowAsync` is no longer exercised there (it is exercised in `Au20EndLifecycleIntegrationTests`). In its `true` branch the WOM end equals a configured end hours after the actual end, and the fetch one hour after the actual end succeeds and becomes the checkpoint. For a row ended early before AU20, this accepts gains after the actual end. That is consistent with WA-2 rule 1 (compare the configured window), but it is the legacy case WA-2 rule 5 tries to avoid. No production rows should exist before merge; the risk is inferred, not traced.

No modified test lost an assertion or had a check loosened. The only semantic flips (ManualLink update result; StatsPass4 branch meaning) follow directly from new AU20 rules.

## Acceptance coverage (my items)

| Check | Basis | Executed assertion? |
| --- | --- | --- |
| Register exists, right section, right columns, all 08-decisions bullets | Source comparison | n/a (doc) — PASS, R1/R2 gaps |
| AU20 rows (end state; structured outputs) | Source | n/a — PASS |
| Three docs updated; claims match code | Source trace §2 | Code claims backed by worker-reported tests (44/44, 11/11, 13+4); not rerun here |
| Retry schedule and enum members recorded | Source | Enum contract test exists (`Au20StoredOutcomeContractTests`), worker-reported pass |
| Accepted limit stated, no compensation | Source | n/a |
| No leftover pending-AU20 in the three docs | grep | PASS; R3 elsewhere |
| CURRENT_STATUS accurate, no false approvals | Source | PASS; R4 labelling |
| Stats fixture failure identified and attributed | Source trace to `444bfc4` | Not executed (by Codex or here) at baseline |
| Scope within §4, no new current-page display | Full file list and diffs | n/a — PASS |
| Modified tests not weakened | Diff review | Mostly worker-executed; `Slice3ScheduleLifecycle:803` not evidenced |

## Confirmed vs unverified
- **Confirmed by trace:** R1–R4, N1–N3; the Stats failure's cause (`444bfc4`); scope; test-diff legitimacy.
- **Unverified (not executed):** every test pass count (worker-reported); that the Stats test actually fails at `b126c55`; that `Slice3ScheduleLifecycleIntegrationTests:803` was run after its change.

**Verdict: PASS with notes.** The register and docs are accurate, scope is clean and the test changes are legitimate. The fixes needed are small doc edits: R1–R3 register/marker fixes and R4 labelling.

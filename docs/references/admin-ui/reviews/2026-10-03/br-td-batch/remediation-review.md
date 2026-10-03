# Independent review: BR/TD fix batch (G1–G6)

Reviewer: Claude (planner), with two read-only reviewer agents, 3 October 2026.
Range: `f2ea1cf..059faf5` on `codex/participants-functionality`. HEAD `059faf5ba904b4a35c54eca4021fa306a2ea0586`, clean tree at start and end. Nothing was built, run or changed in the checkout.
Detailed agent reports: [14a-g1-g2-review.md](14a-g1-g2-review.md) and [14b-g3-g6-review.md](14b-g3-g6-review.md). The planner verified the critical findings (G1-1, G1-2, G3a-1, G3b-1, G3b-2) personally in the code; the lines cited below were re-read.
Requirements: `12-codex-handoff-br-td-batch.md`, `08-decisions.md`, findings in `06a` and `11`.

## Verdict

**Two items fail and need a remediation round: G1 and G3a.** G3b needs two fixes. The rest pass.

| Item | Commit | Result |
| --- | --- | --- |
| G1 approval order (BR-1) | 24c30fc | **FAIL**: G1-1, G1-2 |
| G2 paused-interval guidance (BR-3) | 2b7ddbc | PASS with notes |
| G3a inclusion race (TD-1) | a8a6c24 | **FAIL**: G3a-1, G3a-2 |
| G3b capacity counting (TD-2) | 56020b8 | PASS with required fixes: G3b-1, G3b-2 |
| G4 restart after cancel (TD-3) | 1b5a139 | PASS with notes (new migration) |
| G5 Captain on finalized Add (TD-4) | b9bb405 | PASS |
| G6 Live role publication (TD-7) | 02d19db | PASS with notes |

## Required fixes (remediation round)

### G1-1 (High), verified: earlier uploads aren't simulated cumulatively
- `src/Bingo.Infrastructure/Evidence/SubmissionService.cs:360-365` checks each earlier pending upload against the same baseline (`used`, per-drop and per-item totals from `:347-358`) and never adds the earlier candidates' own amounts.
- Example: target 2; pending A1 10:00, A2 10:10, B 10:30, 1 item each. Approving B is allowed. Then A1 is approved and A2 has no room, so completion becomes 10:30 instead of 10:10. This is what the BR-1 decision forbids.
- Fix: walk the earlier uploads in upload order with two running states, one without and one with the current upload applied first. Compute each candidate's amount in both; return the first candidate whose amount drops; otherwise add each amount to its own state and continue.
- Tests: the A1/A2/B case; two earlier uploads sharing one per-drop cap; a room-for-one case with target above 1.

### G1-2 (Medium), verified: C33 tests now pass or fail at random
- `tests/Bingo.IntegrationTests/C33FinalizationFreshnessTests.cs:928` gives Team A's two uploads the same `SubmittedAt` on a target-1 objective (`:910`). The tie-break is by random GUID (`SubmissionService.cs:340`), so about half the time one blocks the other.
- A blocked approval returns a result instead of throwing (`SubmissionService.cs:174`), and the helper at `C33…:732` ignores it, so most affected tests go wrong silently. `:58` and `:480` fail outright when blocked.
- Fix: distinct upload times in the fixture (keeping each test's intent), then run the **whole** C33 class and record it. Also check the test helpers elsewhere that call `ApproveAsync` and ignore the new result. They should assert that the result was not blocked.

### G1-3 (Low, required because the brief's Proof asked for them): missing or weak tests
- Add a different-team test (only a different objective exists, `SubmissionWorkflowTests.cs:848`).
- The earlier-first branch of the concurrency test (`SubmissionWorkflowTests.cs:925-929`) accepts any error. Assert the specific expected outcome.

### G1-6 / G2-1 (Low): missing Danish translations
- New Review texts at `Details.cshtml:95-97`, `:110-111` and the toast at `Details.cshtml.cs:29` have no `SharedResource.da.resx` entries. Give the earlier upload's time at `Details.cshtml:96` a label. Remove the two now-unused keys (`SharedResource.da.resx:1234`, `:1429`) if nothing else uses them.

### G1-7 (Low): two notes the brief required
- Record in the handoff/`DELIVERY_PLAN.md` that RC07/UI integration must bind the same structured block data and keep queue/filter context.
- Update `FUNCTIONAL_CONTRACTS.md:838` ("implementation pending").
- The no-room wording at `SubmissionService.cs:166` is acceptable as written. It doesn't overlap BR-5.

### G3a-1 (High), verified: manual → included while Start waits still slips through
- `OnPostUpdateTeamAsync` (`src/Bingo.Web/Pages/Admin/Events/Draft.cshtml.cs:151-153`) runs ReadCommitted and locks the event and draft rows `FOR UPDATE`, but writes neither. It writes only the team row plus an audit (`AuditMutation`, `:1175`).
- Start (`:411-412`) is Serializable. Its snapshot is taken at its first statement, before it waits on the event-row lock. PostgreSQL raises a serialization failure after the wait only if the locked row was **updated**, not just locked. So Start resumes with the old snapshot. Its `OrderedDraftTeams` query (`:943-945`, `included_in_draft = TRUE … FOR UPDATE`) doesn't see the newly included team, and SSI doesn't apply because UpdateTeam isn't Serializable.
- Result: a Running draft with a team Start never checked for a Captain or balance, and an audit that leaves it out. Excluding or removing a team is safe (team-row lock conflict). The other order is safe (UpdateTeam sees Running and refuses).
- Fix: make UpdateTeam and RemoveDraftTeam actually **update** the draft row (for example, advance the draft's version) inside their transaction, so a waiting Start or Finalize gets a serialization failure. Or run them Serializable. Use the same mechanism in both handlers.

### G3a-2 (Medium), verified: the race tests can't see G3a-1
- They only exclude or remove teams, never manual → included (`DraftOperationsIntegrationTests.cs:575`, `:587`, `:639`, `:651`). They release the first transaction before the second is blocked (`:590`, `:654`, `:719`), and their if/else assertions accept either outcome.
- Fix: add manual → included vs Start, holding UpdateTeam's transaction open until Start is provably waiting on the lock (for example, by checking `pg_locks`/`pg_stat_activity`), then committing. Assert the single allowed end state. The test must fail on the current code.

### G3b-1 (Medium), verified: manual-team Remove and Move still change the pool during a Running draft
- Only Add is gated. RemoveMember checks `team.IncludedInDraft && ev.DraftLocked` (`Draft.cshtml.cs:273`), and MoveMember has no Running check (`:354` onward).
- Fix: refuse manual-team membership changes while the draft is Running, for Remove and Move too, with the same message as Add. Add tests.

### G3b-2 (Medium), verified: the capacity floor now blocks unrelated Schedule saves
- `EventSignupLifecycleService.cs:114-117` refuses any Schedule save when the posted cap is below the Confirmed count. The Schedule page always posts the current cap. Under the old exclusion, an event could legitimately hold more Confirmed participants than its cap. After option B, every Schedule save for that event fails, including a Live end change.
- Fix: apply the floor only when the cap actually changes (requested cap different from the stored cap). Add a test: an event with Confirmed above the cap can still save a schedule change. This removes the need for a production data check.

## Accepted notes (no change in this round)
- **G3a-3:** inclusion changes are allowed until the event actually starts or its configured end passes (`Draft.cshtml.cs:850-852`), not until the configured start. That matches `FUNCTIONAL_CONTRACTS.md` 4.4 ("a postponed start does not close its own recovery path"). The brief's wording was imprecise. No change.
- **G1-4:** the renamed "…LeavesTileIncomplete" test is correct under the new rule. If cheap, add a test that reversal followed by approving the earlier upload completes the tile again. `G1-evidence.md` should say that the test's assertions changed.
- **G1-5:** `ChangeTracker.Clear()` on the blocked path (`SubmissionService.cs:173`) is acceptable. A narrower reset would be nicer.
- **G2-2:** all paused intervals are listed on every submission. That's informational and acceptable.
- **G3b-3:** the Participants page counts manual-team members but hides them from its list. Goes to the Participants integration ticket (cleanup H5).
- **G4-1:** `RequiresFreshOrder` can stay true on a Finalized draft. Harmless. Tests for cancelling twice and for undo after a restart are optional.
- **G6-1:** `ChangeRoleAsync` turns serialization errors into a refusal inside the page's retry. Safe.
- **G6-2:** a role-only publication also refreshes public names, the same as before Live.

## Deploy rehearsal (R-3)
New migration `20261003184632_AllowCancelledDraftRestart`: adds `draft_sessions.requires_fresh_order` (NOT NULL DEFAULT false) and backfills TRUE where `state = 'Setup' AND first_pick_recorded_at IS NOT NULL`. Down drops the column. The rehearsal should record the backfill count.

## Remediation instructions for Codex
- Same implementer and routing as the batch brief. Fix G1-1, G1-2, G1-3, G1-6/G2-1, G1-7, G3a-1, G3a-2, G3b-1 and G3b-2 as described above. Nothing else.
- Separate commits per item group (G1, G2 translations, G3a, G3b), each naming the finding IDs. Don't amend earlier commits.
- Focused PostgreSQL tests for each fix. Also run the whole C33 class and the full `DraftOperationsIntegrationTests` and `SubmissionWorkflowTests` classes, and record the results.
- Stop after the round. Claude rechecks the fix commits only.

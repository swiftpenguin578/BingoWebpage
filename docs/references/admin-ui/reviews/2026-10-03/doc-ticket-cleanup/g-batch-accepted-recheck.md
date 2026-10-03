# G-batch accepted remediation recheck

 `059faf5..576c661`, 3 October

Reviewer: Claude (planner), read-only, done directly (the scope was small). HEAD `576c6615c64af4d0959152dbcbf7c327106f624f`, clean tree. Codex's recorded runs: C33 29/29, DraftOperations 56/56, SubmissionWorkflow 79/79, Release build clean. Not re-run by Claude.

**Verdict: PASS. G1–G6 are accepted.** Two low notes go into the cleanup batch (H6).

| Finding | Commit | Result |
| --- | --- | --- |
| G1-1 cumulative simulation | 3c2a0a6 | PASS. Two running states (without/with the current upload applied first), each earlier candidate's amount added to its own state in upload order (`SubmissionService.cs` `AddApprovalState`). Traced: target 2, A1/A2/B, approving B is refused on A2. |
| G1-2 C33 determinism | 3c2a0a6 | PASS. Distinct fixture times (`AddMinutes(count)`), and the helper asserts the approval wasn't blocked. Full class recorded 29/29. |
| G1-3 tests, G1-6/G2-1 translations, G1-7 notes | 3c2a0a6, a4a640c | PASS. "Uploaded" label added, Danish strings added, refusal toast localized. |
| G3a-1 race | 93d75f7, 4a99106 | PASS. UpdateTeam (when inclusion changes) and RemoveDraftTeam now update the draft row. A waiting Start gets a serialization failure on its draft lock, and that failure is mapped to the draft-conflict message. |
| G3a-2 test | 93d75f7 | PASS. The manual → included vs Start test proves the second transaction is blocked (`pg_blocking_pids`), requires exactly one success, and checks the end state. The old code would fail it, because both would succeed. |
| G3b-1 Remove/Move gate | 6628e4a | PASS. |
| G3b-2 capacity floor | 6628e4a | PASS. The floor applies only when the cap changes. Test: a Live end change with Confirmed above an unchanged legacy cap succeeds. |

Low notes (into cleanup H6):
- **N-1:** `Draft.cshtml.cs` UpdateTeam uses `draft!.AdvanceVersion()` when inclusion changes. If an event had a team but no `draft_sessions` row, this would be a null reference (HTTP 500), not a clean refusal. That's practically unreachable, because AddTeam, the importer and the seeder all create the draft row, but it should refuse cleanly.
- **N-2:** the new Remove and Move refusal during a Running draft reuses the text "Manual roster additions are locked while the draft is running." It should say changes, not additions.
- Version note: `AdvanceVersion()` plus the SaveChanges hook raises `draft_sessions.version` by 2. Harmless.

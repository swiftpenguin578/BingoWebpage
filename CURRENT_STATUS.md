# Current project status

## Parallel lanes to finish the branch — 8 October 2026

**Feature branch** `codex/participants-functionality` is held by the Claude planner in worktree `/Users/christopher/Documents/BingoWebpage-feature` (Codex released it). Lanes merge into it after their independent review and a green whole suite. Planner decisions: `review-notes/08-decisions.md` in the main folder ("Parallel lanes to finish by Friday 9 October", "Lane swap").

**Accepted (whole suite 2,133 passed / 0 failed / 0 skipped on `69f9cf01`, 8 October 2026):**
- U3 Schedule and Signup setup; T2 Catalogue — user visual acceptance 7 October 2026.
- U4 Overview (`Overview.dc.html` binding) — user early look 7 October; independent review 91 findings fixed at `a91b29ad` and rechecked. Merged `969d0351`.
- C8 / LKP-1 Luck mixed-outcome proof — independent review 92 passed (one wording note applied, `7c73e3a9`). [Evidence](docs/references/admin-ui/reviews/2026-10-08/lkp-1-proof.md). Merged `7ea438f4`.
- Earlier: U1 Identity, U2 Dashboard/Events, T1 Accounts/Audit. `UI_PAGE_MATRIX.md` owns page approval.

**In progress:**

| Lane | Implementer | Batch | Worktree / branch | State |
| --- | --- | --- | --- | --- |
| A | Claude (Opus) | U5 Participants, then U6 Teams/Draft | U6: `BingoWebpage-u6` / `claude/u6-teams` | U6 items 0a–1c committed; at the early-look stop (handoff `review-notes/93h-u6-handoff.md`) |
| B | Claude (Opus) | U7 Board, then U8 Review | `BingoWebpage-u7` / `claude/u7-board` | items up to the early look |
| C | Codex (`gpt-6-astra` / high) | U9 Final review + WOM | `~/.codex/worktrees/u9-final-wom` / `codex/u9-final-wom` | started from `a91b29ad` |

Then U10 retirement sweep (Claude) after all pages merge.

**Rules for every lane:** shared UI rules `review-notes/86-ui-rules.md`; briefs 87 (U5), 88 (U7), 90 (U9), 93 (U6), 94 (U8). Implementers run focused checks and a batch gate of the full JS runner plus a Release build; the planner runs the whole .NET suite per merge. One shared review environment (ports 5310/5320/54339), managed by the planner.

**U10 part 1 (branch `claude/u10-retirement`, 8 Oct 2026):** done: dead `admin-account*`/`admin-audit*`/`identity-*` and every other transitional rule no markup or script uses (about 3,700 of 7,240 CSS lines gone), `_AdminOverlayLayout`, `draft-scramble.js`, `event-manage.js`, `admin-collaboration.js`, `event-create-*.js`, `initializeAdminAccountSearch`, `CompletePromotionFollowUpAsync`, the Teams `TeamImage` handler, two unused Danish keys; sticky-column hover fade; Overview DST label and overlap window localized; `BROWSER_TEST_TIMEOUT_MS`. Kept: `/Admin/Accounts/Create` stub, shared `.thumb` (Catalogue is its only consumer; the frozen file cannot change), Catalogue's own dirty model. Part 2 after U9: `_AdminLayout`, old shell CSS, Finalize/WOM leftover checks, matrix reconciliation (list in `review-notes/98h-u10-handoff.md`).

**No push, merge to `main` or deployment is authorized.** Older handoffs are in Git history.

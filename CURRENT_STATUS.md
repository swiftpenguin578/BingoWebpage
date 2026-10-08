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
| A | Claude (Opus) | U5 Participants, then U6 Teams/Draft | `BingoWebpage-u5` / `claude/u5-participants` | items up to the early look |
| B | Claude (Opus) | U7 Board, then U8 Review | `BingoWebpage-u7` / `claude/u7-board` | items up to the early look |
| C | Codex (`gpt-6-astra` / high) | U9 Final review + WOM | `~/.codex/worktrees/u9-final-wom` / `codex/u9-final-wom` | started from `a91b29ad` |

Then U10 retirement sweep (Claude) after all pages merge.

**Rules for every lane:** shared UI rules `review-notes/86-ui-rules.md`; briefs 87 (U5), 88 (U7), 90 (U9), 93 (U6), 94 (U8). Implementers run focused checks and a batch gate of the full JS runner plus a Release build; the planner runs the whole .NET suite per merge. One shared review environment (ports 5310/5320/54339), managed by the planner.

**Known follow-ups for U10:** sticky first-column hover flicker; dead `admin-account*`/`admin-audit*`/`.admin-header-blockers` CSS; `/Admin/Accounts/Create` stub; "Rolls per {0}" key; English field label inside the Danish DST message on Overview; English-formatted overlap window in the Overview refusal; Catalogue's own dirty model vs shell `markClean`.

**No push, merge to `main` or deployment is authorized.** Older handoffs are in Git history.

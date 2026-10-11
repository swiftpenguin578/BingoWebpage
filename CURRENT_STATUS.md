# Current project status

**Production:** `main` `9794b0c1`, deployed on 10 October 2026 (docs restructure, Board preview, boss fade, audit overhaul). `docs/UI.md` §11 owns page approval. Next work comes from `BACKLOG.md`; each push to main, merge or deploy needs the user's approval.

## Working branch `ops-round` (worktree `/Users/christopher/Documents/BingoWebpage-ops`)

Merged locally, not pushed yet: security fixes (briefs 167/170, reviews 168/172: evidence images checked from the header with a capped decode and every unreadable upload refused with a specific English/Danish message; wiki image cache canonicalised, misses fetched only for referenced sources, pending downloads capped with 503; SignalR served locally) and the JS lane (briefs 165/171, review 173: Participants loading toolbar wraps like the loaded one, Evidence codes dialog clean on open, page-conformance split into five entries and timing fixes in overview, board-drawer, board-preview, WOM-connection and draft tests). The JS job stays advisory until the two Draft fixes in `BACKLOG.md` land (`admin-design-draft-running` [webkit] is intermittent); evidence `artifacts/js-gate/`. Next: push `ops-round` with a draft PR to main, full CI, user preview check.

The full JS runner and the whole .NET suite run in GitHub CI when the planner pushes the branch.

# Current project status

**Production:** `main` deployed on 10 October 2026 (the October Live release; history rewritten the same day, now `b54c421e`). `docs/UI.md` §11 owns page approval. Next work comes from `BACKLOG.md`; each push to main, merge or deploy needs the user's approval.

## Working branch `audit-docs-round` (worktree `/Users/christopher/Documents/BingoWebpage-round`)

Pushed; draft PR swiftpenguin578/BingoWebpage#15 to `main`. The user approved merging once CI is fully green; deploy needs a separate OK. All items reviewed and approved by the user:

- **Board preview** (brief 148): preview shows the board as players see it; team-board tiles use the default artwork; one shared boss-art order.
- **Boss fade + drop banner** (brief 153): multi-boss tiles fade between bosses (6 s, 1.5 s fade); drop banner open 6 s.
- **Audit overhaul** (briefs 147, 159 and the actor fix): readable entries, one-name Affected account, capped team-removal entries, actor search while typing without a filter chip, 940 px table.
- **Documentation** (briefs 146, 151): five documents under `docs/`, rewritten `AGENTS.md`/`CLAUDE.md`, README as project introduction, `.gitattributes`; design references, public reference images and the Stats prototype removed. Rule ledger: `artifacts/docs-restructure/rule-ledger.md`.

The full JS runner and the whole .NET suite run in GitHub CI when the planner pushes the branch.

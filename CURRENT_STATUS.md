# Current project status

**Production:** the October release is deployed (8 October 2026, `b7cb1ad8`). `docs/UI.md` §11 owns page approval. Next work comes from `BACKLOG.md`; each push, merge or deploy needs the user's approval.

## Working branch `docs-restructure` (worktree `/Users/christopher/Documents/BingoWebpage-docs2`)

Local commits only; nothing pushed. Contains `audit-docs-round` up to `1d31ea55`, merged in:

- **Board preview** (brief 148, review 152): preview shows the board as players see it; team-board tiles use the default artwork; one shared boss-art order. Approved by the user.
- **Audit overhaul** (brief 147, review 156): readable entries, Affected account, capped team-removal entries, actor search while typing, 940 px table. Awaiting the user's visual acceptance of the Audit page. Inventory: `artifacts/audit-overhaul/inventory.md`.
- **Documentation restructure** (brief 151): five documents (`docs/PRODUCT.md`, `docs/ARCHITECTURE.md`, `docs/UI.md`, `docs/OPERATIONS.md`, `docs/DEVELOPMENT.md`) plus a rewritten `AGENTS.md` and a short `CLAUDE.md`. The old documents, `docs/references/`, `prototypes/stats/` and the reference-only checks are removed. Rule ledger: `artifacts/docs-restructure/rule-ledger.md`. Awaiting independent review and the planner's verification.

The full JS runner and the whole .NET suite run in GitHub CI when the planner pushes the branch.

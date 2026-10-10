# Current project status

**Production:** the October release is deployed (8 October 2026, `b7cb1ad8`). Every admin page is accepted by the user; `docs/UI.md` §11 owns page approval. Next work comes from `BACKLOG.md`; each push, merge or deploy needs the user's approval.

## Documentation restructure — branch `docs-restructure` (brief 151)

Worktree `/Users/christopher/Documents/BingoWebpage-docs2`, from `audit-docs-round` `e7d143d5`. Local commits only; nothing pushed.

- The documentation is now five documents — `docs/PRODUCT.md`, `docs/ARCHITECTURE.md`, `docs/UI.md`, `docs/OPERATIONS.md`, `docs/DEVELOPMENT.md` — plus `AGENTS.md` (rewritten around the Claude planner / implementer / reviewer workflow, user decision R7a) and a shorter `CLAUDE.md`.
- Removed: the old requirements, contracts, data model, architecture, UI system, page matrix, delivery plan, runbook and topology documents; `docs/references/` (admin design references and public PNGs), `prototypes/stats/`, the rehearsal dry-run report, the parity check and the reference-only checks. App-only assertions moved into the browser tests.
- Rule ledger (every rule of the old documents with its new location or drop reason): `artifacts/docs-restructure/rule-ledger.md`.
- Awaiting independent review (separate reviewer sub-agent) and the planner's verification.

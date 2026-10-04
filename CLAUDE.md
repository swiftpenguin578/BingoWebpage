@AGENTS.md

# Claude Code instructions

`AGENTS.md` (imported above) is the shared instruction file for every agent in
this repository. Follow its project rules: sources of truth, working-tree safety,
architecture and data protections, bounded execution, scope and stop rules,
verification and handoff.

This file only translates the parts of `AGENTS.md` that are written for Codex.
It adds no project policy. If the two ever conflict on a project rule,
`AGENTS.md` and the active authority documents win; report the conflict.

## Codex-specific mechanics that do not apply to Claude

- **Model/reasoning names** (`gpt-*`, Astra, Sol, Luna, Terra, reasoning levels):
  they choose Codex workers. Claude uses the model the user selected for this session,
  and names Codex models only in the routing section of a brief.
- **Codex roles and tooling** (dispatcher, implementer, optional orchestrator, Codex
  chat and task IDs): Claude takes none of these roles and does not dispatch Codex;
  the user sends each brief. Batch independent read-only tool calls in parallel;
  keep dependent steps sequential. An independent review means a fresh session or
  an explicitly requested reviewer sub-agent, never a self-check relabelled as
  independent.

**Planner chats and agents (user decision, 3 October 2026):** the chat the user writes in acts as planner: decisions, briefs, recording decisions, and quick checks. Heavy read-only work (batch reviews, cross-ticket analysis, large sweeps) goes to a sub-agent, which writes its report to `review-notes/`. The planner personally verifies the critical findings before a pass/fail verdict. Start a fresh planner chat per phase, using `review-notes/00-index.md` as the handoff.

## Working alongside Codex

- Codex work usually lives in worktrees under `~/.codex/worktrees/`. Do not edit,
  stage, commit, reset or clean a Codex-owned checkout or branch unless the user
  assigns it. Reading it is fine.
- The checkout Claude starts in may be behind the branch where current work and
  the newest `CURRENT_STATUS.md` live. Before relying on a status or policy
  document, confirm which checkout/branch the user means and whether it is
  behind `origin/main` or the active Codex branch.
- Durable decisions go into the owning repository document (see the authority
  table in `AGENTS.md`), not only into Claude's private memory, so Codex sees them too.
- Do not change the Codex role/model policy in `AGENTS.md` without the user's request.

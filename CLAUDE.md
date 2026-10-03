# Claude Code instructions

`AGENTS.md` is the shared instruction file for every agent in this repository.
Read it first (Claude Code normally loads it automatically) and follow its
project rules: sources of truth, working-tree safety, architecture and data
protections, bounded execution, scope and stop rules, verification and handoff.

This file only translates the parts of `AGENTS.md` that are written for Codex.
It adds no project policy. If the two ever conflict on a project rule,
`AGENTS.md` and the active authority documents win; report the conflict.

## Codex-specific mechanics that do not apply to Claude

- **Model/reasoning tables** (`gpt-*`, Astra, Sol, Luna, Terra, reasoning levels):
  ignore them. Use the model the user selected for this session.
- **Codex tooling** (`functions.exec`, `Promise.allSettled`, `wait_threads`,
  `wait_agent`, `followup_task`, `send_message_to_thread`, `list_projects`, waking
  callbacks, Codex task IDs): no direct equivalent. Batch independent read-only
  tool calls in parallel instead; keep dependent steps sequential.
- **Planner → orchestrator → worker chains and visible Codex tasks:** do not
  recreate them. Take only the single role the user assigns in this session
  (default: whatever the request implies). Heavy read-only work goes to a
  sub-agent as described below. An independent review means a fresh session or
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

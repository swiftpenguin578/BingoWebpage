@AGENTS.md

# Claude Code notes

`AGENTS.md` (imported above) holds every project rule; this file adds only Claude mechanics. If they conflict, `AGENTS.md` wins; report the conflict.

- The planner chat handles decisions, briefs, recording decisions and quick checks. Heavy read-only work (batch reviews, cross-ticket analysis, large sweeps) goes to a sub-agent that writes its report to `review-notes/`. Start a fresh planner chat per phase with `review-notes/00-index.md` as the handoff.
- An independent review is a separate reviewer sub-agent or a fresh session, never a self-check relabelled as independent.
- Batch independent read-only tool calls in parallel; keep dependent steps sequential.
- The checkout you start in may be behind the working branch. Before relying on a status or policy document, confirm which checkout and branch the user means and whether it is behind `origin/main`.
- Durable decisions go into the owning repository document, not only into Claude's memory.

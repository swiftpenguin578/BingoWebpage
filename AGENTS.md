# Working on BingoWebpage

OSRS Community Bingo platform: ASP.NET Core/Razor, PostgreSQL and EF Core. These rules apply to every agent.

## Authorities

| Question | Owner |
| --- | --- |
| Current branch, work, evidence, blockers | `CURRENT_STATUS.md` |
| Product behaviour, journeys, wording, errors | `docs/PRODUCT.md` |
| Data invariants, calculations, architecture, auth | `docs/ARCHITECTURE.md` |
| UI rules and page approvals (sole approval authority) | `docs/UI.md` |
| Production topology, deploy, release gates, backup, rollback | `docs/OPERATIONS.md` |
| Local setup, commands, tests, CI | `README.md`, `docs/DEVELOPMENT.md` |
| Future work | `BACKLOG.md` |

- One subject lives in one owner. Documents describe the current state only: no dated approval blocks, pass/ticket history, change registers, roadmaps or plans. History lives in Git, PRs and the planner's review notes.
- No roadmap or plan files: future work lives in `BACKLOG.md` and is deleted when done.
- When owners disagree, the owner of that concern decides; report the conflict instead of picking the convenient rule. Current user decisions win, but safety boundaries stay.
- A durable decision goes into its owning document before implementation, not only into an agent's memory. Ordinary technical decisions inside a brief need no approval.
- `CURRENT_STATUS.md` stays around 100 lines: replace stale handoffs, link evidence. The implementer updates it at the stop boundary, and updates the owning document when behaviour, architecture, commands or verification change; a requirement gap is reported, never fixed by redefining the product to match the code. No new general-purpose inventory documents.

## Roles and workflow

- **Planner** (the Claude chat the user writes in): agrees outcomes, scope, protected behaviour and acceptance with the user; writes one brief per ticket; merges reviewed tickets into the working branch; starts CI; personally verifies critical review findings before a pass/fail verdict. Never changes production code itself (documentation-only edits with scoped checks are allowed). Asks the user questions as plain text with IDs, options and a recommendation.
- **Implementer**: one sub-agent per ticket, in its own worktree and branch, model by risk (Sonnet for CSS/text/display; Opus for calculations, auth, data and migrations). Changes only the assigned behaviour or named findings, runs the focused checks, commits each item locally, reports. Never reviews its own work; launches no extra workers unless the brief says so.
- **Reviewer**: a separate sub-agent per ticket. Reviews the stable diff read-only (never a changing diff) against the brief: derives expected journeys, invariants and failure modes from approved behaviour first, inspects unchanged entry points and consumers, and reports missing scope, unapproved additions, concrete defects, non-discriminating tests and unproven outcomes. Each finding names actor, state, sequence, expected vs observed, evidence and the smallest correction. Open product decisions are not defects; optional improvements are not requirements.
- **Fix round**: findings go back to the same implementer, which fixes the named findings, verifies at the failure boundary and checks direct consumers. The recheck covers the named findings and their direct consequences only.
- **Codex**: fallback only, being retired (about 16 October 2026). Do not edit, stage, commit or reset a Codex-owned worktree or branch (`~/.codex/worktrees/`) unless the user assigns it.
- A brief states checkout/branch, outcome, protected scope, relevant authorities, checks, stop boundary and the implementer's model. Use exactly that model; report if it is unavailable instead of substituting (difficulty alone is no reason to change it). The approved brief is the review baseline; a fix round cannot silently revise it or settle a product decision.
- Concurrent writers get disjoint ownership; create separate user-owned tasks only when the user asks. Parallel tickets need disjoint files, their own worktree and branch, and at most one adding migrations; merge each only after its review passes.
- Reporting: before each turn-ending response, immediately for a blocker or product question, and at the stop boundary with commit SHAs, checks and results, evidence, open limitations and the next permitted action. Sending a message does not prove a worker started; a timed-out wait is not completion.

## Scope

- The brief defines the outcome; repository standards constrain it but create no extra tasks. Fix the identified problem with existing code and shared components; no broad audit, redesign, cleanup or new framework.
- Inspect directly affected dependencies when correctness needs it; a file list does not excuse a required integration fix. If the brief excludes a necessary change, report the conflict first.
- Report material changes (behaviour, migration, authorization, routes, manual acceptance) before implementing; record unrelated findings without adding them. Technical details are resolved within scope; product and scope questions go to the planner and the user. A routine question is no reason to stop or skip work; an explicit stop always wins; never invent approval.
- Approval: a page or behaviour is accepted only after the user has tried it in the running preview; screenshots are a general look. Approval is page-specific; approving a composition does not cover newly revealed confirmations, errors or toasts. UI work follows the task and review contract in `docs/UI.md`: the implementer runs focused behaviour checks, the reviewer checks source (outcome wording, severity, recovery, duplication, modal visibility), the user gives visual acceptance; approved compositions and interaction models are preserved.

## Protect the repository and data

- Start with `git status --short --branch` on the assigned checkout, then read the `CURRENT_STATUS.md` handoff and the brief; read other owners only to settle a relevant question. Preserve existing changes; never revert, overwrite, stage or commit others' work. Branch names describe the purpose (e.g. `fix/button-colors`).
- Commits: after an item's checks pass, make one scoped local commit with only that item's changes and evidence before starting the next; inspect the staged diff; report the SHA; fix review findings in follow-up commits, never rewrite checkpoints. A commit is a checkpoint awaiting review, not acceptance.
- Pushing: only the planner pushes, and only the working branch plus one draft PR to main for CI; name every push. Ticket branches stay local, are merged locally into the working branch, then branch and worktree are removed. Push or merge to main and every deploy need the user's explicit OK. No destructive cleanup or branch deletion without approval; before deleting branches, inventory them read-only (merged, unique commits, active).
- Never expose secrets or commit real participant data or raw runtime output. Durable reports and compact evidence go in the repository; temporary folders are scratch.
- Never reset, seed or mutate user-owned databases; use controlled fixtures. Use your own ports and containers; never kill processes by name; leave the user's running app alone unless the task authorizes it.
- Keep Web, Application, Domain and Infrastructure boundaries; business rules live in domain/application code. Preserve authorization, audit, transactions, concurrency, privacy, evidence integrity, finalization, event snapshots and competitive history; approved evidence stays authoritative for progress; never rewrite history for convenience.
- PostgreSQL is the authority; realtime messages are notifications or invalidations only. Preserve the route and form behaviour `docs/PRODUCT.md` requires. No new fallback machinery, services, tables or abstractions without a concrete need; no deferred feature without user authorization.
- Migrations: one owner at a time; migration, designer and model snapshot together; keep historical migrations; fail-closed migrations and preflight give an exact diagnosis, a safe correction and a retry path; never infer historical identity from mutable current state.

## Execute

- A continuing worker reuses established evidence and rereads only changed context. Begin with the known gap and the next concrete edit; read in bounded batches with `rg` and targeted reads; every investigation answers a specific question. Finish a connected behaviour before opening another area.
- Do not repeat a failed command without a changed condition. Change approach after two failed attempts at the same problem, stop after three. An environment failure is not an assertion failure: stop dependent checks and report it.
- An automatic approval rejection stops that action; do not rebuild it through smaller steps or other tools.
- Stop when the outcome exists and its checks pass with no material open finding; do not continue for extra corroboration or speculative improvements.

## Verify

- Checks scale with the change and target the changed risks. Implementers run only the tests their change touches (the fix's test, the touched pages' checks, `git diff --check`), plus a Release build (no warnings, formatting verified) when C# or Razor changed. The full JS runner and the whole .NET suite run only in GitHub CI. CSS/markup-only corrections: scoped source/cascade and diff checks, plus runtime checks when a boundary can be affected. Documentation-only changes: consistency, link and diff checks, no .NET.
- Behaviour changes need executable checks that would fail for the defect they claim to prevent and reach the real actor, state and operation (no implementation-mirroring assertions, irrelevant exact counts or duplicate coverage); add coverage for concrete security, privacy, concurrency, data-integrity, migration or regression risks. Relevant tests are added or updated and pass before an item is reported complete; work is complete when its criteria are met, not because files exist. Security, persistence, concurrency and navigation checks exercise the real boundary: requests as the intended actor (including anonymous) through the real pipeline, following rendered links and forms; in-memory tests do not prove PostgreSQL behaviour; client validation and DOM interaction run in a browser; visual results need rendered evidence.
- A happy path must reach a usable result; rejecting invalid input does not prove it. Trace a changed rule through its entry points and direct consumers (progress, completion, rankings, notifications, history reads).
- Never weaken a failing test as "stale" without establishing the contract and replacement proof. If the environment blocks a required proof, report exactly what is unverified and how to run it; the user may waive a named case.
- Timestamps: PostgreSQL keeps microseconds, .NET ticks are 100 ns. Use deterministic UTC, microsecond-aligned fixtures or expected values derived at the persistence precision, with exact assertions (no broad tolerances, sleeps or second rounding). For fingerprints, stale checks, receipts, scheduling and ordering include non-aligned input and a real PostgreSQL round trip. Tests must behave the same locally and on Linux CI.
- Full JS runner and whole .NET suite: run in GitHub CI (the JS job in 9 shards) when the planner pushes the working branch, sized by risk — small (CSS, layout, text, display) once per batch or before main; medium (calculations, service rules) when the ticket is done; large (migrations, auth, storage, concurrency, deploy scripts) per ticket, plus the local rehearsal when migrations change. CI must be fully green (0 failed, 0 skipped) before main and before every deploy, together with the pre-deploy checks in `docs/OPERATIONS.md`. Implementers never run the full JS runner or the whole suite locally.

## Handoff

- State what changed, checks and results, open limitations, acceptance status and the next permitted action. Keep verified, inferred, implemented, executed, source-reviewed and manually accepted distinct, and skipped, passed, waived and deferred distinct. Never claim an unrun check passed; a build or handoff is not a review, and technical approval is not manual acceptance.
- If interrupted, record the last verified state and the exact next step.
- Data findings record source, observation time, release/schema identity where known, the aggregate count or state, and limitations; code capability or old logs are not current production counts.

# Working on BingoWebpage

OSRS Community Bingo platform: ASP.NET Core/Razor, PostgreSQL and EF Core.

## Start with the assignment

- Use the checkout and branch named in the current assignment. Run
  `git status --short --branch`; preserve all existing changes.
- Read the active handoff in `CURRENT_STATUS.md` and the assigned plan section.
  Read further authority sections only to resolve a question relevant to the task.
- Before dispatching or resuming coordination, planners and dispatchers read
  [the lean execution workflow](DELIVERY_PLAN.md#421-lean-execution-and-planner-handoff).
- A continuing worker should reuse established evidence and source knowledge.
  Reread only changed or missing context; do not restart discovery after a handoff.
- Do not use `docs/archive/` for ordinary work. It is historical evidence, not
  current authority, unless an explicit provenance investigation requires it.
  Root tombstones and the former Application Atlas are not active authorities.

## Scope and roles

The task brief defines the assigned outcome, protected behavior and checks.
Repository standards constrain the work; they do not create additional tasks.
Each worker has exactly one assigned role.

- Fix the identified problem using existing code and shared components. Do not
  turn a correction into a broad audit, redesign, cleanup or new framework.
- Inspect directly affected dependencies when necessary for correctness. A file
  list is a starting point, not permission to ignore a required integration fix.
- Report material changes to product behavior, scope or architecture before
  implementing them. Record unrelated findings without silently adding them.
- Resolve ordinary technical details within the approved scope. Implementers raise
  consequential uncertainty to the dispatcher; product and scope decisions go to
  the planner, who takes them to the user when needed.
- **Planner:** works with the user to define outcomes, scope, protected behavior,
  acceptance and the plan; writes the briefs; resolves consequential decisions and
  reconciles delivery. Does not implement production code. Documentation-only
  planning/policy edits may be completed directly with scoped checks.
- **Dispatcher:** receives the brief from the user, starts the implementer with the
  exact model/reasoning the brief names, relays reports and questions, and checks
  stalled or interrupted work. Does not implement, review or grant user approval.
- **Implementer/remediator:** changes only assigned behavior or named findings,
  runs the applicable focused checks, commits each item and reports.
  Never reviews its own work and does not launch extra workers by default.
- **Independent reviewer:** reviews the stable batch read-only against the brief and
  the findings, and rechecks its own named findings after remediation. Optional
  improvements are not requirements.
- Give concurrent writers disjoint ownership; reuse compatible workers and existing
  evidence rather than repeating discovery. Create separate user-owned tasks only
  when explicitly requested; this policy does not authorize new visible tasks.

### Active workflow and model defaults — 2026-10-05

This is the single current role/model/routing policy (user decision, 5 October
2026; it replaces the 2 October orchestrator route). Historical model assignments
remain evidence of completed work, not competing defaults.

| Role | Who / model | Owns |
| --- | --- | --- |
| Planner and independent reviewer | Claude planner chat selected by the user | Scope, briefs, decisions with the user, independent review of each batch, delivery reconciliation |
| Dispatcher | Codex planner chat; `gpt-5.6-luna` / `medium` (user decision, 6 October 2026) | Starting the implementer, relaying reports and questions |
| Implementer/remediator | Model/reasoning named in the brief (default `gpt-6-astra` / `high`) | Implementation, focused checks, per-item commits, evidence |
| Orchestrator | `gpt-6.1-sol` / `high`, only when a brief assigns one | Worker dispatch, waits and handoffs for that brief |

- Claude implements the lanes the user assigns to it (lane T, and the main lane from U4), each in its own worktree and branch, with an implementer sub-agent; review stays independent (separate reviewer agent, planner verification). Codex runs the lanes assigned to it (C8, U9) and is the fallback implementer at batch boundaries. The Claude planner holds the feature branch and merges reviewed lanes into it. (User decisions, 7 October 2026; 08-decisions.md, “Lane T implemented by Claude”, “Lane swap”, “Parallel lanes to finish by Friday 9 October”.)
- **Route:** the user sends the brief to the dispatcher → one implementer per batch
  (or per named parallel lane) works the items in order, one local commit each →
  it stops at the brief's boundary and reports → the user relays the report to the
  planner → the planner reviews independently. Findings come back as a remediation
  brief on the same route; the remediation recheck covers the named findings and
  their direct consequences, not a broad re-review. Remediation goes to the same
  implementer chat when it is available.
- Briefs name the roles (planner, dispatcher, implementer) and the implementer's exact
  model/reasoning; the user routes them. They also state the checkout/branch, assigned
  outcome, protected scope, relevant authorities, checks and stop boundary. Verify
  recipients; never infer them from pinned or recent tasks for any orchestrated
  hand-off a brief explicitly assigns.
- No orchestrator, Codex reviewer or separate verifier unless the brief assigns one.
  Never review a changing diff.
- Run batches sequentially unless the planner's approved plan explicitly names
  parallel lanes, their file ownership is disjoint, and at most one lane adds database
  migrations. Each parallel lane uses its own worktree, branch and implementer chat;
  merge it back into the feature branch only after its independent review passes.
- Set the exact model/reasoning from the brief when dispatching. Do not silently
  substitute another model/version or reasoning level. If the configured model is
  unavailable, report that precise issue. Difficulty alone is not authorization to
  change the model. Preserve compatible work/evidence when the user changes models.
- **Reporting:** the implementer reports before each turn-ending response, immediately
  for a blocker or product question, and at the stop boundary with commit SHAs,
  checks and results, evidence locations, unresolved limitations and the next
  permitted action. Queuing or sending a message does not prove a worker started; a
  timed-out wait is not completion and does not prove a worker is still running. If
  a route or tool fails, preserve the exact checkpoint and report the missing owner
  or capability.
- **Blockers:** the dispatcher resolves technical, environment and routine execution
  issues within existing authority and resumes the work. Product, scope and
  behavior-versus-design-reference questions go to the planner and the user. A
  routine question is not a reason to stop or skip unfinished work. An explicit
  stop/pause always wins; never invent approval or silently skip a blocked item.
- **If a brief assigns an orchestrator:** it dispatches workers with the exact model,
  waits on them with the supported wait tool (no busy polling of unchanged state),
  performs each next handoff itself so no ready handoff is left without an owner,
  and sends one terminal completion/blocker report to the dispatcher.

## Protect the repository and data

- Do not revert, overwrite, stage or commit existing work without authorization.
  No destructive cleanup, branch deletion, push, merge or deployment without approval.
  For branch cleanup/publication, follow `DELIVERY_PLAN.md` section 4.7.
- Do not expose secrets or include real participant data in committed artifacts.
- Do not reset, seed or mutate user-owned databases outside the authorized task.
  Use controlled fixtures for checks; leave the user's running app alone unless
  the task requires and authorizes changing it.
- Preserve architecture boundaries between Web, Application, Domain and
  Infrastructure. Business rules belong in domain/application code.
- Preserve authorization, audit, transactions, concurrency, privacy, evidence
  integrity, finalization, event snapshots and competitive history. Approved
  evidence remains authoritative for progress; do not rewrite history for convenience.
- PostgreSQL is authoritative persistence. Realtime messages are notifications
  or invalidations, not authoritative state.
- Preserve route/form behavior required by the active contracts. Do not add new
  fallback machinery, services, tables or abstractions without a concrete need.
  Do not add deferred product features without user authorization.
- Required migrations include the migration, designer and model snapshot together.

## Execute with visible progress

- Begin with the known gap and the next concrete edit. Read/search in bounded,
  useful batches using `rg`/targeted reads; batch independent reads and keep
  dependent operations and conflicting edits sequential. Every additional
  investigation must answer a specific question; record the result before
  choosing the next step.
- Complete a connected behavior before opening another area. Provide an early
  working checkpoint; repeated reading or a restated plan is not implementation.
- Do not repeat a failed command without a changed condition or different evidence.
  After a clear environment failure, stop dependent checks and report the blocker.
- Change approach after two unsuccessful attempts at the same problem. Stop after
  three with the same blocker. Allow one implementation attempt and one bounded
  continuation; do not loop through replacement workers indefinitely. An environment
  failure is not a product assertion failure or a reason to increase model reasoning.
- An automatic approval rejection stops that action immediately. Do not reconstruct
  it through smaller patches or alternate tools; report the reason and permitted
  next step.
- Continue authorized work through its checks. Stop for a genuine blocker, required
  user decision or explicit pass boundary. Do not sacrifice correctness to save time.

## Verify the change

- Checks scale with the change: small fixes run the fix's test, the touched pages' checks and git diff --check; the full JS runner and Release build run once per batch before the planner's whole-suite run (user decision, 7 October 2026; 08-decisions.md, “Checks scale with the change”).
- Choose checks that would detect the actual changed risks. Reuse applicable passing
  evidence; rerun it only after a relevant change or newly exposed defect.
- Small CSS/markup corrections normally need scoped source/cascade and diff checks.
  Add runtime/build checks when the change can affect those boundaries.
- Documentation-only changes need scoped consistency/reference/diff checks, not .NET.
- Behavior changes need executable checks. Security, persistence, concurrency and
  navigation checks must exercise the relevant boundary; source review is not a
  substitute for execution, and in-memory tests do not prove PostgreSQL behavior.
- All timestamp tests must explicitly respect the precision of the boundary being
  tested. PostgreSQL timestamps retain microseconds; .NET values can contain
  finer 100-nanosecond ticks. Do not rely on the local operating system's clock
  precision or compare an unnormalized wall-clock value exactly with its persisted
  round-trip value. Use deterministic UTC instants and microsecond-aligned fixture
  values for ordinary persistence tests, or derive the expected value using the
  actual persistence precision. Preserve exact assertions at that precision rather
  than adding broad tolerances, sleeps or rounding to seconds to make CI pass.
  For timestamp-dependent fingerprints, stale checks, receipts, scheduling and
  ordering, include non-microsecond-aligned input and a real PostgreSQL round trip
  where relevant so normalization cannot hide a production correctness defect.
  Tests must behave consistently on local runs and Linux GitHub Actions runners.
- Follow the applicable delivery procedures in `DELIVERY_PLAN.md` sections 4.1–4.6:
  planning/readiness for major functional slices, change control for material changes,
  evidence at affected boundaries, required review, slice preflight and completion.
  Read only the relevant subsection; these do not add stages to every small fix.
- For Admin popup passes, the implementer runs focused behavior checks, the reviewer
  checks source, and the user supplies final visual acceptance. General composition
  approval does not also approve newly revealed confirmations, errors or toasts.
  Check outcome wording, severity, recovery, duplication and modal visibility against
  `UI_SYSTEM.md`; this does not authorize a separate whole-site feedback audit.
- Other UI passes follow `UI_SYSTEM.md`'s task/review contract and `UI_PAGE_MATRIX.md`.
  Historical reference images apply only when explicitly reactivated for the current
  task. Preserve approved composition and interaction models. Manual approval is
  page-specific; deferred acceptance stays awaiting approval.
- Use existing setup/build/test commands from `README.md`. The implementer runs
  the assignment's scoped checks and reports; affected .NET tests apply when needed,
  and small fixes require a Release build only if C# or Razor changed. The full JS
  runner and Release build are batch gates as stated above, not per-fix gates.
  Only the planner runs the whole .NET suite once on the final SHA, in the
  background as soon as the
  report arrives; that is the batch gate before acceptance (zero failures and zero
  skipped tests). Failures return as remediation. The implementer does not run the
  whole suite (08-decisions.md, Q-S1, user decision, 7 October 2026; supersedes the
  implementer-owned whole-suite gate from B5 review D14 / brief32).

## Durable evidence and authorized checkpoints

- Put final reports, manifests and compact test evidence in the repository before
  packaging; temporary directories are scratch space, not the sole durable handoff.
  Never commit secrets, real participant data or unnecessary raw runtime output.
- The user authorizes local staging and commits for completed assigned tickets/items
  on the assigned branch (3 October 2026). After each item's applicable checks pass,
  the implementer makes a scoped local commit before the next item starts; do not
  accumulate completed items into one final batch commit. Include only that item's
  changes, required documentation and durable evidence; inspect the staged diff and
  report the commit SHA. Preserve unrelated work and use follow-up commits for review
  corrections rather than rewriting existing checkpoints.
- Preserve the assignment's review boundary. If independent review is scheduled at
  batch end, commit each implemented/tested item as a checkpoint explicitly awaiting
  independent review; a commit is not technical completion or manual acceptance.
  Otherwise satisfy the item's required review before its completion commit. A
  blocked item is not complete; report its saved state and blocker separately.
- This standing commit authority does not start unassigned work or override an
  explicit no-commit instruction. It does not authorize push, merge, deployment,
  destructive cleanup or staging unrelated existing work.
- Keep CURRENT_STATUS.md approximately 100 lines or fewer: replace stale handoffs,
  link durable evidence and leave historical pass transcripts in Git/reports. The
  implementer updates it at the stop boundary.
- The planner updates owning requirements when a product decision/ticket is made.
  Implementers update delivery/evidence and report any uncovered requirement gap;
  they do not quietly redefine the product to match implementation.

## Keep authority and handoffs clear

| Question | Authority |
| --- | --- |
| Current checkout, work, evidence or blocker | `CURRENT_STATUS.md` |
| Approved scope, pass order and delivery gates | `DELIVERY_PLAN.md` |
| Product behavior and user journeys | `PRODUCT_REQUIREMENTS.md`, `FUNCTIONAL_CONTRACTS.md` |
| Data invariants and architecture | `DATA_MODEL.md`, `TECHNICAL_ARCHITECTURE.md` |
| UI rules, page exceptions and approvals | `UI_SYSTEM.md`, `UI_PAGE_MATRIX.md` |
| Commands and local setup | `README.md`, `DEVELOPMENT_SETUP.md` |

- Report conflicts between authorities rather than silently selecting a convenient
  rule. Current user decisions take precedence; preserve required safety boundaries.
  Promote accepted behavior/scope changes to the existing authority before implementing
  them; ordinary technical decisions within the brief need no extra approval.
- Update the existing owner only when behavior, approval, policy or status materially
  changes. Keep pass history out of this file; replace stale rules instead of appending
  another exception. Do not create another general-purpose inventory document.
- Handoff: what changed, checks/results, unresolved limitations, acceptance status
  and next permitted action. Distinguish verified results, inferences, implemented,
  executed, source-reviewed and manually accepted; record skipped, passed, manually
  accepted and deferred distinctly. Follow explicit user review waivers and acceptance
  decisions. Never claim an unrun check passed; a handoff or successful build is not
  an independent review pass, and technical approval is not manual acceptance.
  If compaction or interruption forces a handoff, record the last verified state and
  exact next step. `UI_PAGE_MATRIX.md` alone owns page approval; status summaries do not.
- Stop when the assigned result and applicable gates are satisfied. Do not begin
  another page family or packaging without its authorization.

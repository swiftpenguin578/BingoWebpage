# Working on BingoWebpage

OSRS Community Bingo platform: ASP.NET Core/Razor, PostgreSQL and EF Core.

## Start with the assignment

- Use the checkout and branch named in the current assignment. Run
  `git status --short --branch`; preserve all existing changes.
- Read the active handoff in `CURRENT_STATUS.md` and the assigned plan section.
  Read further authority sections only to resolve a question relevant to the task.
- Before delegating or resuming coordination, planners read
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
- Resolve ordinary technical details independently. If one focused lookup leaves
  consequential uncertainty about intended behavior, scope or conflicting
  instructions, ask the calling planner before proceeding with that part.
- **Planner:** defines the task, selects approved workers, checks progress and
  reconciles delivery. Does not implement production code or review its own work.
- **Implementer/remediator:** changes only the assigned behavior or named findings
  and runs its focused checks. Does not independently review itself.
- **Reviewer:** remains read-only and reports concrete defects or scope deviations.
  Does not turn optional improvements into requirements.
- **Verifier:** executes assigned checks without changing production behavior.
- **Packager:** acts only under explicit staging, commit, merge or push authority.
- The planner may delegate bounded subtasks when useful independent work justifies
  it. Give concurrent writers disjoint ownership; reuse compatible workers instead
  of repeating discovery. Use a fresh independent reviewer, never the implementer.
- Create separate user-owned tasks only when explicitly requested. Workers report
  to their calling planner through collaboration tools, or return normally if those
  tools are unavailable; never infer a destination from pinned or recent tasks.
- Worker briefs name the role, checkout, outcome, protected scope, relevant authority
  sections, approved model/reasoning, checks and stop boundary. Workers do not
  dispatch additional workers or start another pass on their own.

### Model defaults

These current defaults supersede historical assignments elsewhere. Explicit user
choices take precedence; task-specific exceptions stay in the task brief and current
handoff. Do not silently substitute another model or reasoning level.

| Work | Model / reasoning |
| --- | --- |
| Planner and independent reviewer | `gpt-6-astra` / high |
| General implementation and remediation | `gpt-6-astra` / medium |
| Coupled or high-risk implementation | `gpt-6-astra` / high |
| Small, precisely diagnosed non-UI corrections | `gpt-6-astra` / low |
| Routine UI implementation and corrections | `gpt-5.6-luna` / max |
| Verification executing agreed gates | `gpt-5.6-luna` / high |

The planner sets model and reasoning explicitly before dispatch; do not rely on
inherited settings. Use a self-contained or bounded context, not a full-history fork
that overrides selection. Other models/levels require user approval. Escalate for a
concrete difficulty or risk; touching a UI/security/data file alone is not sufficient.
Assess time and usage across implementation, corrections and acceptance together.

## Protect the repository and data

- Do not revert, overwrite, stage or commit existing work without authorization.
  No destructive cleanup, branch deletion, push or deployment without approval.
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
  useful batches using `rg`/targeted reads. Batch independent tool calls with
  `Promise.allSettled`; keep dependent operations and conflicting edits sequential.
  Every additional investigation must answer a specific question; record the result
  before choosing the next step.
- Complete a connected behavior before opening another area. Provide an early
  working checkpoint; repeated reading or a restated plan is not implementation.
- The planner checks prolonged lack of progress and interrupted workers promptly.
  A timed-out wait does not establish that a worker is still running.
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

- Choose checks that would detect the actual changed risks. Reuse applicable passing
  evidence; rerun it only after a relevant change or newly exposed defect.
- Small CSS/markup corrections normally need scoped source/cascade and diff checks.
  Add runtime/build checks when the change can affect those boundaries.
- Behavior changes need executable checks. Security, persistence, concurrency and
  navigation checks must exercise the relevant boundary; source review is not a
  substitute for execution, and in-memory tests do not prove PostgreSQL behavior.
- Follow the applicable delivery procedures in `DELIVERY_PLAN.md` sections 4.1–4.6:
  planning/readiness for major functional slices, change control for material changes,
  evidence at affected boundaries, required review, slice preflight and completion.
  Read only the relevant subsection; these do not add stages to every small fix.
  Documentation-only changes need scoped consistency/reference/diff checks, not .NET.
- Run the required source review once, then recheck named corrections and their
  direct consequences. Do not repeat broader review merely for reassurance.
- For Admin popup passes, the implementer runs focused behavior checks, the reviewer
  checks source, and the user supplies final visual acceptance. General composition
  approval does not also approve newly revealed confirmations, errors or toasts.
  Check outcome wording, severity, recovery, duplication and modal visibility against
  `UI_SYSTEM.md`; this does not authorize a separate whole-site feedback audit.
- Other UI passes follow `UI_SYSTEM.md`'s task/review contract and `UI_PAGE_MATRIX.md`.
  Historical reference images apply only when explicitly reactivated for the current
  task. Preserve approved composition and interaction models. Manual approval is
  page-specific; deferred acceptance stays awaiting approval.
- Use existing setup/build/test commands from `README.md`; run only applicable gates.

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
  executed, source-reviewed and manually accepted. Never claim an unrun check passed.
  If compaction or interruption forces a handoff, record the last verified state and
  exact next step. `UI_PAGE_MATRIX.md` alone owns page approval; status summaries do not.
- Stop when the assigned result and applicable gates are satisfied. Do not begin
  another page family or packaging without its authorization.

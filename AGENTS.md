# Repository Instructions for Codex

## Start here

This repository is the OSRS Community Bingo platform. Before substantial work, read this file and `CURRENT_STATUS.md`, then read only the source-of-truth documents relevant to the task.

Do not reconstruct the project from prior chat history. Durable repository evidence is authoritative.

## Sources of truth

Use these documents by responsibility:

1. `CURRENT_STATUS.md` — canonical checkout state, blockers, verification
   limitations, current work, immediate ownership, and unresolved decisions.
   Its compact UI table is a non-authoritative snapshot derived from
   `UI_PAGE_MATRIX.md`; the matrix alone owns current page approval/status.
2. `DELIVERY_PLAN.md` — current remaining delivery, documentation passes, UI pass order, release gates, dependencies, and stop rules.
3. `PRODUCT_REQUIREMENTS.md` — product behavior, scope, roles, workflows, non-functional requirements, and version-one acceptance criteria.
4. `FUNCTIONAL_CONTRACTS.md` — active final workflow/journey contracts, actors, reachability, authority handoffs, failure/recovery behavior, and acceptance outcomes.
5. `DATA_MODEL.md` — entities, invariants, calculations, evidence/progress/ranking rules, concurrency, and finalization semantics.
6. `TECHNICAL_ARCHITECTURE.md` — architecture, persistence, storage, security, realtime behavior, deployment, operations, and test strategy.
7. `UI_SYSTEM.md` — global UI primitives, ownership, responsive/accessibility rules, and UI review contract.
8. `UI_PAGE_MATRIX.md` — page family, canonical reference, protected composition, exception, and current approval authority.
9. `README.md` — commands, local operation, and newcomer links to the active authority set.
10. `DEVELOPMENT_SETUP.md` — developer-machine setup.

Read focused sections instead of dumping whole large documents into context. Search headings and terms first, then inspect the relevant ranges.

When documents, code, tests, or current behavior conflict, do not silently choose one. Report the conflict, determine which artifact is stale where possible, and update the correct source of truth when the task authorizes it.

Archive exclusion: material under `docs/archive/` is preserved historical evidence, is non-authoritative and may be stale or contradictory, and is excluded from ordinary inventories, searches, planning, implementation, and review. Search it only for an explicitly named provenance, migration, or manual-evidence investigation. Current active documents and current user decisions win. Any recovered valid decision must be promoted into an active source before implementation.

`UI_SYSTEM.md` is the global UI authority and `UI_PAGE_MATRIX.md` is the page-family/reference/exception/approval authority. `FUNCTIONAL_CONTRACTS.md` is the active workflow/journey authority. `FUNCTIONAL_WORKFLOWS.md` is a non-authoritative root tombstone retained for historical links; its exact pre-consolidation source is preserved under `docs/archive/`. Root UI-contract and overhaul-roadmap tombstones are also non-authoritative; their exact historical content is preserved under `docs/archive/`. `DELIVERY_PLAN.md` owns order and gates.

Completed slice plans/results and the superseded implementation roadmap are archive-only under `docs/archive/`; use the active authorities above for current rules, delivery order, and acceptance status.

The former Application Atlas is archived historical evidence and is not an active inventory, route map, review surface, or source of truth. Use the active authority split above; do not recreate a replacement Atlas or route decisions through the archived presentation.

## Architecture and business-rule boundaries

- Preserve the `Web` → `Application` → `Domain`/`Infrastructure` separation and the architecture tests.
- Keep business rules explicit in domain/application code; do not hide authoritative rules in Razor, JavaScript, or database triggers.
- PostgreSQL/EF Core is authoritative persistence. Approved evidence is authoritative for progress.
- Preserve event snapshots and history. Do not rewrite historical competitive records for convenience.
- Preserve authorization, audit, transaction, concurrency, privacy, evidence-integrity, and finalization protections.
- Treat SignalR messages as invalidations/notifications, not authoritative secret state.
- Keep ordinary Razor form/navigation fallbacks when progressively enhancing interactions.
- For ordinary Admin filters and independent Admin value saves, enhancement is optional; retain existing inexpensive route/form paths, but do not add fallback-only complexity. Existing protected board, team, and tile routes remain available for deep links, reloads, history, and failed enhancement; the Captain Submit endpoint is only drawer transport plus a compatibility redirect, not a rendered/no-JavaScript page; no-JavaScript parity is not a UI acceptance or deployment gate.
- Do not add deferred post-version-one features unless the user explicitly expands scope.
- Never commit secrets, `.env` files, real participant data, or production credentials.

## Working-tree safety

- Begin with `git status --short --branch` and inspect only bounded diffs relevant to the task.
- Existing modifications belong to the user. Preserve them and do not revert, overwrite, stage, or commit them unless explicitly asked.
- Do not use destructive Git or filesystem commands without explicit authorization.
- Use concise purpose-based branch names such as `ui-overhaul`, `fix/button-colors`,
  or `release/production-prep`. Do not add a `codex/` or other agent-identifying
  prefix unless the user explicitly requests one. When practical, settle or
  rename the branch before its first push; renaming must not be treated as a
  code or history change.
- Before deleting or cleaning up old local or remote branches, produce a bounded
  read-only inventory separating fully merged branches, branches with unique
  commits, and active branches. Delete only the exact branches the user then
  authorizes. A merged branch pointer may be removed without removing its commits
  from `main`, but historical merge messages and pull-request records retain the
  original name.
- If generated migrations are required, include the migration, designer, and model snapshot consistently.
- Keep changes focused. Avoid unrelated cleanup during feature or UI work.

## Execution discipline and loop prevention

- Use bounded commands (`rg`, targeted `sed`, specific test filters, limited Git
  output). State the uncertainty before a diagnostic and record what its result
  establishes. Do not reconstruct working code from unclear history.
- Batch independent bounded reads with `Promise.allSettled` in one
  `functions.exec` call and inspect every result. Use `Promise.all` only when any
  failure should abort the batch. Keep dependent/adaptive operations, conflicting
  mutations, approvals, and waits sequential.
- Do not rerun an unchanged failing command without a relevant condition change
  or materially different evidence. After one clear environment failure, stop
  dependent checks and continue independent work. An environment failure is not
  a product assertion failure or a reason to increase model reasoning.
- After two unsuccessful attempts at the same problem, change approach. After
  three with the same blocker, report the evidence, exact blocker, and smallest
  next action. Allow one implementation attempt and one bounded continuation;
  replace a repeatedly failing worker or approach rather than extending a loop.
- Identify the critical path and use elapsed time and usage to detect an
  unproductive approach. Never omit an authorized requirement or compromise
  correctness, security, deterministic behavior, or data integrity to save time.
- Continue through authorized work; a progress update or completed substage is
  not an approval gate. Respect explicit pass boundaries and user decisions.
  Stage, commit, push, merge, release, and deploy only as separately authorized.
- Keep commentary concise and outcome-oriented. After compaction, resume from
  `CURRENT_STATUS.md`, the current plan, and the smallest relevant diff. If a
  handoff is forced, record the last verified state and exact next action.
- Keep status evidence concise. Update `CURRENT_STATUS.md` only for material
  changes; consolidate superseded checkpoints. Distinguish verified results,
  inferences, user-reported results, and unverified claims. Commit messages and
  reviewer agreement alone do not establish executed behavior.
- Use the coverage and completion rules below to decide when verification is
  sufficient. Additional review or testing must resolve a named uncertainty that
  could change correctness, scope, acceptance, or significant risk; repeated
  corroboration and stylistic expansion are not unfinished work.

## Roles, models, and task dispatch

This section owns future model/role assignments, including UI work. It supersedes
older model names in implementation plans and UI workflow descriptions, without
changing their product scope, review gates, or approval requirements. An explicit
user model choice takes precedence. Do not silently fall back to another model.

| Role or work | Default model and reasoning |
| --- | --- |
| Planner/orchestrator | GPT-6 Astra High |
| Implementation and remediation | GPT-6 Astra Medium |
| Coupled or high-risk implementation/remediation | GPT-6 Astra High |
| Small, precisely diagnosed corrections | GPT-6 Astra Low |
| Independent readiness, correctness, scope, or visual review | GPT-6 Astra High |
| Verification executing agreed gates | GPT-5.6 Luna High |

The planner selects and states the level before dispatch. Use High when correctness
requires reasoning across coupled lifecycle, authorization, concurrency, migration,
or calculation rules; touching a file in one of those areas is not sufficient by
itself. Low is for a localized correction with a known cause and bounded effects.
XHigh/Max and models outside this policy require a user choice. Set model and
reasoning explicitly; do not rely on inherited defaults. Assess usage across the
whole task through acceptance, including remediation, rather than only the first
implementation attempt. This policy does not promise a fixed usage saving.
For subagent dispatch, set `model` and `reasoning_effort` explicitly using
`gpt-6-astra` or `gpt-5.6-luna` and the level above. Use a self-contained prompt
with no history fork, or a bounded history fork that permits those overrides;
do not use a full-history fork that forces inherited model/reasoning settings.

Every delegated task declares exactly one role:

- **Planner/orchestrator:** owns scope, product decisions, journey completeness,
  pass ordering, worker selection, and evidence handoffs. It may maintain the
  authorized plan/instructions; it does not implement production behavior or
  supply independent review of its own work.
- **Implementer:** completes the assigned pass, self-checks behavior, and provides
  the agreed verification. It flags missing journeys or contract contradictions;
  it does not start another pass, independently review itself, or package work.
- **Remediator:** fixes named findings and verifies their affected behavior and
  direct consequences. It does not expand scope or change approved product rules.
- **Independent reviewer:** remains read-only, independently challenges assumptions,
  and checks behavior and scope against the final approved plan. It does not
  implement its findings or treat the implementer's explanation as proof.
- **Verifier:** executes the agreed gates and reports evidence and limitations.
  It flags an inadequate gate to the planner and reports unrelated failures
  separately; it does not change production behavior.
- **Packager:** stages, commits, merges, or pushes only the accepted state and only
  as authorized. It introduces no implementation changes.

Subagents are authorized for bounded delegated work, including Public UI work;
they do not require separate user approval or a visible worker task. Prefer
subagents for subtasks of the current request. Create a separate user-owned task
only when the user explicitly requests one. This replaces older visible-task-only
and no-subagent instructions. Delegate only when useful independent work justifies
the coordination cost; do not multiply agents or duplicate checks for confidence.

Use the authoritative checkout specified by the current user instruction or
handoff, not a separate saved checkout. Prompts state the role, checkout, explicit
model/reasoning, scope, relevant authorities, journey outcomes, verification, and
stop boundary. Give concurrent writers disjoint file ownership or run their edits
sequentially. Independent review starts in a fresh agent/task; never reuse the
implementer/remediator as its own independent reviewer. Reuse compatible workers
for roughly five to ten bounded turns while role, scope/page family, and context
remain coherent. Start fresh when those change or the approach repeatedly fails.
Follow-ups state the exact correction and require only relevant rereads. The
orchestrator remains responsible for integrating results and reporting evidence
and limitations to the user; delegation does not change scope or review gates.

## Planning and journey completeness

Before splitting a functional slice into implementation passes, the planner maps
all approved user outcomes into a compact journey/coverage table in the existing
slice plan. Carry the manual steps into `MANUAL_TEST_CHECKLIST.md`; do not create a
new inventory document or duplicate the full product specification.
For a small correction, update the affected journey or state its outcome and proof
in the task prompt; do not introduce a full slice plan solely for this rule.

For each journey record:

- The actor and effective role, starting lifecycle/data, and actual UI entry point.
- The action sequence, expected persisted result, visible result, and next reachable
  step, including the destination emitted by a notification when applicable.
- Relevant boundary/recovery cases and the invariant each protects.
- The owning pass, planned proof at the failure boundary, and manual-only or blocked
  parts. Track execution against these outcomes, not just counts of passing tests.

Select variations from the affected behavior: zero/one/multiple records and valid
ties; global plus event roles; current plus retained memberships; EN/DA client and
server input; before/at/after time boundaries; stale/repeated/concurrent requests;
partial failure and retry; retained snapshots and changed current data. These are
prompts for relevant risks, not a mandatory Cartesian product or one test per case.
A happy path must reach a usable result. Correct rejection of invalid requests does
not establish that an authorized user can complete the action.

The planner owns coverage across passes and reconciles the final checklist against
every approved outcome. A journey omitted from the checklist is not implicitly
waived. Implementers and reviewers must flag gaps they discover. Trace changed
rules through their entry points and directly affected consumers, including derived
progress, completion, rankings, notifications, and retained-history reads where
applicable; a correct write alone does not prove those results agree.

After the user approves product behavior, run exactly one independent read-only
implementation-readiness review for a major functional slice. It compares the
complete proposed slice with current code and active authorities, challenges missing
journeys and assumptions, and assesses pass ordering and independent deployability.
It must establish:

- Real UI reachability and minimum Development reset accounts, roles, states, and
  records for acceptance. Fixtures must coexist and permit the documented sequence;
  do not bypass the behavior under test or add broad demonstration data.
- For removed/replaced behavior, a bounded inventory of affected domain values,
  persistence, services, routes, controls, notifications, seeds, tests, and authority
  wording so obsolete behavior cannot survive accidentally.
- A complexity budget of concrete new tables, services, pages/routes, policies,
  jobs, dependencies, and abstractions. Each addition needs a specific persistence,
  transaction, authorization, operational, or demonstrated reuse need.
- For fail-closed migrations/preflight, the exact operator diagnosis, safe record
  correction/adjudication, and retry path. Use retained-data rehearsal where required;
  never infer historical identity from mutable current state.
- Approved scope, explicit non-goals, necessary dependencies, optional suggestions,
  verification boundaries, and outstanding product decisions.

Resolve named decisions and update the plan before implementation. Do not repeat
readiness review without a genuine contradiction or missing product decision.
Ordinary implementation defects belong to bounded remediation. Optional suggestions
do not become requirements without approval.

## Implementation and change control

Extend existing entities, services, pages, policies, and shared components before
adding abstractions. Preserve required invariants and protected interactions. Do not
add speculative frameworks, compatibility layers, or unrelated cleanup. Existing
route-backed recovery stays protected; separate no-JavaScript parity is not required.

Implementers may resolve ordinary technical details within the approved pass. Stop
for user direction before changing a product rule, broadening a pass, introducing
unbudgeted infrastructure, or fixing an adjacent issue not needed for safe delivery.
Record unrelated defects separately. A missing integration step required for an
approved journey is in scope, even when its owning file is outside the initial diff.
If the plan explicitly excludes a necessary change, report that conflict before
implementing it.

The final approved plan is the review baseline. Before implementing a user-approved
material change, update its affected pass, acceptance criteria, non-goals/complexity
budget, and relevant product/data/UI authority. A change is material when it affects
behavior, migration, authorization, routes, manual acceptance, or review conclusions.
Clarifications with no behavioral effect need no separate paperwork. Remediation
cannot silently revise the baseline or settle an unresolved product decision.

## Verification and independent review

### Choose coverage first, then the smallest sufficient checks

Derive verification from the journey outcomes and affected risks before minimizing
commands or assertions. A required journey or relevant integration boundary without
proof is a concrete uncertainty. Use existing discriminating tests where they cover
it; one scenario may prove several consecutive steps or related invariants.

Match evidence to the boundary that can fail:

| Risk | Required kind of evidence |
| --- | --- |
| Route, filter, authorization, binding, or navigation | Requests as the intended actor, including anonymous users where applicable, through the real pipeline; follow rendered links/forms and emitted destinations where required. Direct handler calls cannot prove filter or navigation reachability. |
| Client validation, localized form input, or DOM interaction | Execute the affected behavior in a browser. Source/markup and HTTP proof cover only their own layers; they cannot establish client acceptance. |
| Persistence, transaction, concurrency, or retained migration | Exercise the relevant PostgreSQL behavior, failure/retry boundary, or approved copied-data rehearsal. In-memory success cannot establish database guarantees. |
| Derived results or retained reads after mutation | Check the directly affected consumers and usable rendered result, including valid multiple-record cases where applicable. |
| Visual composition and usability | Inspect current rendered evidence under the UI protocol; source checks cannot establish visual acceptance. |

Use controlled/disposable data and existing fixtures without touching user-owned or
production data beyond authorization. If an environment prevents the required proof,
report exactly which outcome remains unverified and the smallest way to execute it.
Alternative source or lower-layer checks may narrow uncertainty but do not turn the
blocked boundary into a pass. The user may explicitly accept a substitute or waive a
named case; preserve that decision and do not revive it without new evidence.

A required test must fail for the plausible defect it claims to prevent. Check that
its setup actually reaches the relevant actor, state, and operation and its assertions
observe the outcome. Avoid assertions that merely mirror implementation, exact counts
unrelated to behavior, or duplicated coverage at every layer. Do not weaken a failed
test as “stale” without establishing the approved contract and replacement proof.
Expand coverage for concrete security, privacy, concurrency, data-integrity, migration,
or escaped-regression risks. Run focused gates per pass and the complete suite at the
documented final gate or when the blast radius warrants it.

### Independent review and bounded remediation

Use the existing required readiness/post-implementation review gates; these rules do
not add a separate reviewer per risk or require repeated model agreement. The reviewer
first derives expected journeys, invariants, and plausible failures from the approved
behavior and current authorities, then evaluates the implementation and its evidence.

Review the exact base-to-current diff against the final plan and inspect unchanged
entry points and direct consumers when needed to trace changed behavior. The diff is
the change inventory, not the limit of behavior inspection. Report:

- Required scope delivered and missing, material additions without approved mapping,
  changed non-goals, and unbudgeted tables/services/routes/policies/jobs/abstractions.
- Concrete behavior/security/privacy/authorization/concurrency/data-integrity defects,
  non-discriminating required tests, and required outcomes lacking sufficient proof.
- For each finding, the triggering actor/state/sequence, expected and observed or
  source-inferred result, evidence, and smallest necessary correction.

A plan omission is reportable; distinguish an implementation defect within approved
behavior from an unresolved product decision requiring the user. Do not invent new
requirements or demand stylistic expansion, redundant assertion syntax, or exhaustive
duplicate tests. Incidental supporting code/tests/docs need proportionate scope
justification, not a separate plan bullet for every file.
Missing approved behavior, unapproved material additions, concrete correctness
defects, and inadequate required proof block acceptance until resolved or explicitly
adjudicated by the user. Optional improvements do not block it.

A fresh remediator addresses named findings. Verify the fix at its failure boundary,
rerun the affected functional journey, and check direct consumers where the correction
can change them. Small visual corrections follow the UI exception below.
Follow any required fixes-only review gate. Reopen wider review only for a concrete
new implication, contradiction, or material scope change. Do not use repeated broad
reviews as a substitute for executing a missing journey.

### Manual-acceptance preflight and handoff

After required review/remediation clears, perform one bounded preflight against the
exact checklist and authoritative Development reset state before asking the user to
walk the slice. Reuse applicable executed evidence; do not rerun every passing check.
Follow each journey from its documented start through actual rendered navigation,
forms, and role-appropriate notification destinations, verifying a renderable result
and the next step. Use browser execution where client behavior can block the journey.
Do not manufacture destination URLs or ready database states to skip required steps.

If a journey fails, stop that journey, route the smallest authorized remediation to
the appropriate role, rerun it, and resume the remaining preflight. Subjective visual
clarity, responsive composition, and wording remain manual-only unless they prevent
completion. Hand over only when required executable journeys pass or the user has
explicitly accepted their named limitations. A recorded blocker alone is not acceptance.

Handoffs distinguish **implemented**, **source-reviewed**, **execution passed**,
**blocked/unverified**, and **manually accepted or explicitly waived**. State the exact
revision or working-tree scope, commands/scenarios and results, evidence limitations,
and next permitted action. A generic “PASS,” test count, or approved screenshot does
not establish all of these. The planner reconciles coverage before claiming the
slice complete; omitted or blocked required outcomes remain open.

Stop when approved outcomes exist, applicable verification and acceptance gates are
satisfied, and no unresolved finding could materially change correctness, security,
privacy, authorization, concurrency, data integrity, or the requested result. Do not
continue for extra corroboration, reviewer agreement, or speculative improvements.

## UI work and manual approval

Use `UI_SYSTEM.md` for tokens, controls, typography, layout, accessibility, responsive
behavior, progressive enhancement, and protected baselines. Use `UI_PAGE_MATRIX.md`
for page families, composition exceptions, and current approval. `DELIVERY_PLAN.md`
owns pass order; `CURRENT_STATUS.md` owns the handoff. Historical Board rewrite and
reference details stay in those authorities; their presence does not reactivate work
or reference images. Apply the model and task policy above to all UI roles.

- Before implementation, freeze the page family, primary user/task, protected
  behavior, states, files, scope, and non-goals. Preserve established Board, editor,
  and live-draft interaction models unless the user approves a focused redesign.
  A presentation rewrite preserves behavior/bindings, not rejected legacy geometry.
- Every new or materially changed page must satisfy current UI authority from its
  first implementation. Reuse shared owners and compact patterns; no interim local
  theme, second navigation framework, or deferred accessibility compliance.
- Historical Public UI reference pictures may be inspected or compared only when
  the user explicitly names them as relevant to the current task. This rule governs
  reference activation despite historical reactivation wording elsewhere. Use current
  supplied screenshots, named findings, and implementation evidence for corrections.
- User-supplied light/dark desktop and narrow/mobile screenshots are normal current
  visual evidence. Minor viewport variance/browser chrome is acceptable when the
  application viewport is identifiable. Review applicable keyboard, focus, empty,
  error, permission, and feedback states; no separate no-JavaScript gate is required.
- Before UI remediation, dispatch the required independent reviewer unless the user
  explicitly requests a direct correction. Review current screenshots/rendered
  evidence and scoped source against both the named findings and global UI contracts.
  A historical reference may be compared only when currently reactivated by the user.
- One fresh remediator fixes the named findings, then the user performs visual
  acceptance. User rejection overrides a reviewer pass; approval is page-specific.
- For small CSS/markup-only corrections, normally inspect source/cascade and the
  scoped diff/whitespace. Add build or behavioral checks only for actual Razor,
  runtime, or shared-contract risk or an explicit user request. Do not automatically
  run .NET tests/builds or request elevated execution; preceding applicable build
  evidence remains usable. Material visual changes still need rendered evidence.
- Static editorial copy may change to fit approved composition while preserving
  meaning, truthfulness, localization, and action destinations. Dynamic names, dates,
  counts, lifecycle facts, account data, bindings, and interaction semantics remain
  protected. Mutations need accurate feedback and server-authoritative validation.
- Stop between page families/passes unless the user authorized continuous passes.
  Explicitly deferred final acceptance does not skip implementation, current evidence,
  independent review, or remediation. Record cleared pages as `awaiting manual
  approval`; the later combined walkthrough includes shared-shell/CSS regression
  across them. Continuous authorization does not permit scope expansion, packaging,
  committing, pushing, deployment, or bypassing a blocker.

Promote accepted durable decisions to their existing owner: workflow/model/handoff
rules here; reusable visual rules in `UI_SYSTEM.md`; page exceptions and approval in
`UI_PAGE_MATRIX.md`; behavior in product/functional contracts; current evidence,
blockers, and next action in `CURRENT_STATUS.md`. Transient annotations remain evidence
until accepted. Keep UI handoffs to page/pass, any currently reactivated reference,
current screenshots, approval state, unresolved findings, and next permitted action.

## Standard commands

Prerequisites: .NET 10 and a running Docker daemon. Integration tests use Testcontainers; the application/browser paths expect PostgreSQL.

```bash
dotnet restore Bingo.slnx
docker compose up -d postgres
dotnet test Bingo.slnx --no-restore
dotnet format Bingo.slnx --no-restore --verify-no-changes
dotnet build Bingo.slnx --configuration Release --no-restore
```

Run focused tests during iteration, then the complete suite for final verification. Do not treat Docker/Testcontainers setup failures as product assertion failures, but do report that the affected suites remain unverified.

For local application and migration commands, use `README.md`.

## Definition of done

Before reporting implementation work complete:

- Every approved journey has its required result through the appropriate layers;
  the planner has reconciled coverage and reported any explicit user waiver.
- Relevant tests were added or updated and pass.
- The release build passes with no unexpected warnings.
- Formatting verification passes.
- Authorization, validation, audit, concurrency, privacy, and lifecycle effects were considered where relevant.
- UI changes satisfy the applicable roadmap checks and user approval gate.
- Database changes include consistent migrations and were exercised against PostgreSQL.
- Documentation and `CURRENT_STATUS.md` were updated if behavior, architecture, commands, roadmap position, or known verification state materially changed.
- Required independent review is clear, and executed checks are distinguished from
  source review and manual acceptance. Unrun required checks remain open unless
  explicitly waived; report all limitations using the handoff rules above.

Apply the documented small-UI exception where appropriate. Documentation-only work
uses scoped diff, consistency, and reference checks; it does not require .NET gates.

Milestones are complete only when their documented completion criteria are satisfied—not merely because corresponding files or pages exist.

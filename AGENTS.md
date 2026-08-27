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

Future agents must follow these rules:

1. Use bounded commands (`rg`, targeted `sed` ranges, specific test filters, limited Git output). Never request an unbounded combined repository dump.
2. Form a hypothesis before running a diagnostic command. Record what the result established before choosing the next command.
3. Do not rerun an unchanged failing command unless some relevant condition changed or the rerun gathers materially different evidence.
4. After one clear environment failure (for example, Docker daemon unavailable), stop repeating dependent tests. Record the blocker and continue with independent checks.
5. After two unsuccessful attempts at the same problem, change approach. After three attempts with the same blocker, stop and provide the user with the evidence, exact blocker, and smallest next action.
6. Never restart the whole investigation after context compaction. Re-read `CURRENT_STATUS.md`, current plan, and the smallest relevant diff, then continue from the last established fact.
7. Separate facts into **verified**, **inferred**, and **unverified**. Do not promote commit-message implications or prior-chat claims to verified status.
8. Do not rewrite working code merely because its history is unclear. Inspect its tests and behavior first.
9. Keep commentary concise and outcome-oriented. Do not narrate repeated status with no new evidence.
10. Update `CURRENT_STATUS.md` only when status materially changes. Keep its active handoff concise and consolidate superseded checkpoints into a short historical summary instead of accumulating a minute-by-minute log that every future task must reread.
11. Within each bounded stage, batch independent read-only tool calls available through `functions.exec` into one call, normally with `Promise.allSettled`, and inspect every result. Use `Promise.all` only when any failure should abort that batch. Keep dependent or adaptive investigations, mutations that may conflict, approvals, and wait/resume operations sequential. Do not split otherwise batchable bounded inspections across separate outer tool calls.
12. Treat elapsed time and context/token use as finite engineering budgets. Identify the critical path at the start, execute its next blocking step promptly, and parallelize independent bounded work only when the active instructions and available tooling permit it.
13. Use rough time expectations only to detect an unproductive approach. At meaningful checkpoints, compare progress with the expectation; if work is taking materially longer, change strategy or defer only work that is explicitly outside the requested scope. Never drop an authorized requirement merely to fit an estimate.
14. A progress update is informational, not a pause or approval gate while authorized work remains possible. Completing one bounded stage is not by itself a reason to end the turn: continue to the next authorized stage until the requested objective is complete, a genuine blocker is reached, or the execution environment forces a handoff. If a handoff is forced, record the exact last verified state and next action.
15. Run the smallest verification set that proves the changed behavior and protects the affected risk surface. Expand verification when failures, dependencies, or blast radius justify it; final completion still requires the repository's applicable definition-of-done gates.
16. Prefer the shortest **authorized** path through implementation, focused verification, leak checks, and handoff. Stage, commit, push, release, or deploy only when the user has authorized those actions.
17. Never trade correctness, security, deterministic behavior, or data integrity for speed.
18. Before adding another test, review, audit, investigation, evidence request, or validation step, name the unresolved uncertainty and how the result could materially change the implementation, verdict, minimum required fix, authority, fulfillment, or significant risk. If it cannot, stop; additional confidence alone does not justify more work.
19. Stop when the requested outcome exists, its smallest relevant direct verification has passed, and no unresolved finding could materially change correctness, security, privacy, authorization, concurrency, data integrity, or the requested result. Corroboration, proof-of-proof, reviewer/model agreement, speculative improvement, and unrelated defects are not unfinished work.

If blocked, useful work may include focused source inspection, independent unit tests, documentation reconciliation, or a precise handoff. Do not claim completion while required verification remains blocked.

## Task roles and lean orchestration

For Codex task model selection, use Luna with high reasoning for implementation,
remediation, review, and verification unless the user explicitly chooses another
model or the UI-specific protocol below assigns Terra High to independent visual
review. Never start or continue a task on Sol with medium or high reasoning unless
the orchestrator first explains why it is needed, requests that exact
model/reasoning combination, and the user explicitly approves it. Do not rely on
inherited or default task settings when they could select Sol medium/high; set the
approved model and reasoning explicitly before dispatch.

For delegated implementation, remediation, and verification use Luna High. Every
independent review, including readiness, scope, and visual review, uses Terra
High. Create fresh visible worker/reviewer tasks in the saved checkout with
concise self-contained prompts instead of forking the planner; reuse a compatible
task for roughly five to ten bounded turns when its role and page family remain
the same.

Every delegated task must declare exactly one role and remain within it:

- **Orchestrator/planner:** defines bounded tasks, selects the next worker, verifies handoffs, and stops repeated failures. It does not implement production behavior or review its own work. It asks the user only for product decisions, explicit permissions, environment blockers, and manual acceptance.
- **Implementer:** completes only the assigned pass or correction. It does not begin the next pass, perform independent review, package, commit, or push unless explicitly assigned.
- **Remediator:** addresses only named findings with the smallest safe change. It does not reopen the whole pass or add unrelated cleanup.
- **Independent reviewer:** remains read-only and compares the complete base-to-current implementation diff with the final approved slice/pass plan. It verifies that required scope is present, explicit non-goals remain untouched, and every material addition is approved and traceable. It blocks only on a concrete scope deviation, missing required behavior, behavior/security/privacy/authorization/concurrency/data-integrity defect, unapproved material complexity, or a genuinely non-discriminating required test. It must not demand redundant assertion syntax, exhaustive duplicate coverage, or stylistic expansion.
- **Verifier:** runs only the agreed gates, reports unrelated failures separately, and does not change production behavior.
- **Packager:** acts only after acceptance and may stage, commit, merge, and push the accepted state as authorized. It must not introduce implementation changes.

### Public UI planner/orchestrator protocol

For the active Public UI rebuild, the planner/orchestrator is the user-facing
coordination role. It plans, dispatches, verifies handoffs, records accepted
decisions, and stops repeated failures; it does not implement production UI or
review its own work.

- Create implementers, remediators, independent reviewers, and verifiers as
  separate user-owned Codex tasks that remain visible to the user; do not use
  hidden subagents for this workflow. A visible worker task may be reused for
  roughly five to ten bounded turns when its role, model/reasoning, checkout,
  and page family remain the same. Each follow-up must state the exact new
  correction and require focused authority rereads only when the relevant
  authority or scope changed. Start a fresh task when the role or page family
  changes, independent review begins, the task becomes confused or repeatedly
  fails, or accumulated context is no longer compact. Never let an implementer
  or remediator independently review its own work. Use Luna High for
  implementation, remediation, and verification, and Terra High for independent
  UI review. Do not use Terra for implementation or Sol for any worker/reviewer
  role without new explicit user approval.
- Freeze the exact page family, protected behavior, files, states, scope, and
  non-goals before implementation. Include a visual reference only when the user
  explicitly names it as relevant for the current task. A presentation rewrite
  preserves behavior and bindings, not legacy Razor composition, containers, or
  responsive geometry.
- Public UI reference pictures were first-round implementation inputs and are
  historical after that round. A planner, worker, reviewer, or remediator must
  not open, compare, or reason from one merely because it remains named in the
  matrix or repository; use it again only when the user explicitly states that
  the named picture is relevant to the current task. Manual corrections use the
  user's current finding and current supplied screenshot evidence plus the
  existing implementation/source.
- After implementation, the user normally supplies light/dark desktop and
  narrow/mobile screenshots. Slight viewport variance and visible browser chrome
  are acceptable when the application viewport is identifiable. Treat those as
  current implementation evidence and do not reopen historical reference
  pictures unless the user explicitly reactivates one.
- The user may explicitly defer final manual acceptance across several sequential
  passes. That does not skip the normal implementation, current screenshots,
  independent current-evidence/system review, and focused remediation sequence.
  After remediation, record the page as `awaiting manual approval` rather than
  approved and continue to the next authorized pass. The user may supply the
  screenshots or corrections during the sequence; retain them as current
  implementation evidence. The later combined manual walkthrough must include
  shared-shell/CSS regression across all pages awaiting approval.
- Dispatch an independent reviewer before remediation. The reviewer compares the
  user-supplied current screenshots, scoped diff, and current named findings,
  then reports concrete visual and behavior deltas. Compare a historical visual
  reference only when the user explicitly reactivates that named picture for the
  task. The reviewer also independently checks the rendered
  page and scoped source against `UI_SYSTEM.md` typography hierarchy, spacing,
  control, theme, focus, and responsive contracts; reference ambiguity does not
  excuse a global-system violation. Do not send a page directly to a
  remediator unless the user explicitly requests a direct correction.
- One fresh remediator fixes only the named findings. The user then performs the
  visual acceptance check. A user rejection overrides a passing reviewer verdict,
  and a user approval is page-specific rather than whole-pass or whole-site
  approval.
- Small CSS/markup-only visual corrections use only the checks that can detect
  their actual risk, normally targeted source/cascade inspection plus whitespace
  or scoped diff checks. Do not automatically run .NET tests, a Release build, or
  request named-pipe/elevated execution for such a correction. Add a compiled or
  behavioral gate only when the change can affect Razor compilation, runtime
  behavior, or a broader shared contract, or when the user explicitly requests
  it. An immediately preceding applicable passing build remains valid evidence.
- Static editorial copy may be shortened, reordered, or replaced to fit an
  approved composition when meaning, action destinations, localization, and
  truthfulness are preserved. Dynamic names, dates, counts, lifecycle facts,
  account data, routes, handlers, authorization, and interaction semantics remain
  protected.
- Stop between passes or page families and whenever user attention is required.
  Do not begin another family after approval, rejection, review, or remediation
  without the next user authorization. An explicit continuous multi-pass
  authorization with deferred final manual approval satisfies that gate after
  each pass completes implementation, screenshots, review, and remediation; it
  does not authorize scope expansion, packaging, committing,
  pushing, deployment, or bypassing a genuine blocker. Replace a repeatedly
  failing worker or approach instead of extending the same loop.

Promote durable UI decisions into the smallest existing authority so later
planners do not depend on chat handoffs:

- task workflow, task visibility, role/model policy, and handoff rules →
  `AGENTS.md`;
- reusable tokens, typography, control, responsive, and theme decisions →
  `UI_SYSTEM.md`;
- page/reference mappings, family exceptions, and approval state →
  `UI_PAGE_MATRIX.md`;
- current pass, blocker, evidence, and next permitted action →
  `CURRENT_STATUS.md`;
- product behavior and journey semantics → `PRODUCT_REQUIREMENTS.md` or
  `FUNCTIONAL_CONTRACTS.md`.

Transient annotations remain evidence until accepted. Promote an accepted
annotation only when it changes a reusable rule, page exception, approval state,
or protected behavior. A UI handoff should therefore be short: current page/pass,
any user-reactivated reference, current screenshot evidence, exact approval state,
unresolved findings, next permitted action, and known verification limitations.
Repository authority remains final.

Prefer the simplest implementation that preserves the required invariants:

- Extend existing services, entities, pages, policies, and shared components before adding new abstractions.
- Do not add a table, service, compatibility layer, or generalized framework without a concrete persistence, transaction, authorization, or reuse need.
- Avoid speculative future-proofing, broad cleanup during a feature pass, and no-JavaScript-only machinery for ordinary controls. Retain existing route-backed paths for protected board, draft, team, tile, and submission deep links, reloads, history, and failed enhancement; do not implement separate no-JavaScript parity work.
- Manual-test findings should receive the smallest bounded correction that fixes the demonstrated behavior.

Use risk-based, non-duplicative testing:

- Use the smallest test set that would fail if an important requirement or risk boundary broke.
- One scenario or parameterized test may prove several closely related behaviors.
- Do not create one test for every branch by default.
- Avoid repeating the same assertion across domain, handler, HTTP, browser, and migration layers unless each layer protects a distinct plausible failure.
- Expand coverage for authorization, privacy, transactions, concurrency, destructive lifecycle changes, retained migrations, and regressions that previously escaped the suite.
- Focused tests are the normal per-pass gate. Run the complete suite only at the documented final gate or when the blast radius genuinely warrants it.

Allow one implementation attempt and, when interrupted, one bounded continuation. If the same worker or approach fails repeatedly, change the approach or create a fresh task from the last verified state; do not loop indefinitely.

Before implementation begins for each remaining major functional slice, run exactly one independent read-only implementation-readiness review after the user approves the product behavior. The review must compare the complete proposed slice with the current code and source-of-truth documents, identify only concrete blockers, product decisions, compatibility work, implementation risks, and behavior-preserving simplifications, and assess pass ordering and independent deployability. Resolve the named decisions and incorporate accepted corrections into the slice plan before implementation. Do not repeat the planning review unless implementation later exposes a genuine contradiction or missing product decision; ordinary implementation defects belong to focused remediation and the normal post-implementation review.

That readiness review must also establish a lean implementation contract:

- Confirm every planned journey is reachable through existing/planned UI and that Development reset can create the minimum accounts, roles, lifecycle states, and records needed for manual acceptance. Do not add broad demonstration data.
- For removed/replaced behavior, inventory the affected domain values, persistence, services, routes, controls, notifications, seeds, tests, and source-of-truth wording so obsolete behavior cannot survive accidentally.
- Include a complexity budget listing the concrete new tables, services, pages/routes, policies, jobs, and abstractions the plan appears to require. Challenge any addition without a specific persistence, transaction, authorization, operational, or demonstrated reuse need.
- For every fail-closed migration/preflight, state how an operator identifies and corrects affected records before retrying deployment.
- Freeze the approved scope and explicit non-goals before implementation. Separate necessary dependencies from optional improvements and unrelated defects. Reviewer suggestions classified as optional do not become implementation requirements.
- Define the implementation stop rule: an implementer may proceed through ordinary technical details, but must stop for user direction before adding behavior, changing an approved product rule, broadening a pass, or resolving an adjacent issue that is not required for the pass. A discovered unrelated defect is recorded separately unless it prevents safe implementation or verification of the approved behavior.
- Review and remediation remain bounded to the approved slice/pass. Neither is permission for opportunistic cleanup, generalized frameworks, UI redesign, or fixes to adjacent features.

### Change control and post-implementation scope review

- The approved slice plan is the authoritative review baseline, not a frozen historical draft. If the user approves a material product, scope, persistence, route, authority, workflow, or complexity change during implementation or manual remediation, update the slice plan and any affected source-of-truth documents before implementing that change. Record the decision, affected pass, changed acceptance criteria, and any changed non-goals or complexity budget.
- Clarifications that do not change behavior need not create paperwork. When uncertain whether a decision is material, treat it as material if it could change implementation, migration, authorization, user-visible behavior, manual acceptance, or the independent-review verdict.
- The post-implementation independent review must compare the exact base-to-current diff against the final updated plan. It must explicitly identify required scope delivered, required scope missing, material implementation with no approved plan mapping, explicit non-goals that changed, and any unbudgeted table/service/route/policy/job/abstraction.
- An unapproved material addition or omitted approved behavior is a review blocker until it is removed, completed, or explicitly approved and added to the plan. Incidental tests, migrations, documentation, and small supporting code are judged by whether they are proportionate to approved behavior, not by requiring a one-to-one plan bullet for every file.
- Focused remediation does not silently revise the baseline. If a manual finding or review correction changes approved behavior rather than merely fixing its implementation, obtain the user's decision and update the plan first.

### Manual-acceptance preflight

After implementation review/remediation clears and before asking the user to run a slice's manual checklist, perform one bounded manual-acceptance preflight against the exact written checklist and authoritative Development reset state. This is a reachability and integration check, not another broad architecture review or a requirement for one automated test per checklist sentence.

- Walk each manual journey in order from its documented starting state. Verify the required seeded account, role, event state, record, control, and navigation path exist.
- Follow the application's real rendered links and forms through authenticated HTTP or the smallest equivalent route-level scenario. Do not prove reachability by constructing the destination URL directly when the checklist expects navigation through the site.
- Verify each request survives page filters and authorization, reaches the intended handler/service, returns a renderable destination, and leaves the next checklist step reachable.
- For notifications and Admin actions, follow the actual emitted destination and verify it resolves for the intended role. Checking only notification presence, count, or URL text is insufficient.
- One focused journey may prove several consecutive checklist steps. Add coverage only where it protects a plausible integration seam that existing focused tests do not exercise.
- Report visual clarity, responsive composition, wording preference, and subjective usability as manual-only unless they prevent the journey. Do not expand the preflight into UI redesign, exhaustive browser automation, full-suite execution, or unrelated cleanup.
- If the preflight finds a concrete defect, stop the affected manual journey, apply only the smallest authorized remediation, rerun that journey, and then resume the remaining preflight. Hand the checklist to the user only when every non-visual journey is reachable or an explicit known limitation is recorded.

## UI work

- Follow `UI_SYSTEM.md` for global UI authority and `UI_PAGE_MATRIX.md` for page-family, reference, exception, and approval authority. `CURRENT_STATUS.md` owns the current handoff and `DELIVERY_PLAN.md` owns order and gates.
- Every new page and every materially changed page must follow the applicable active authority from its first implementation. A later whole-site UI pass is not permission to introduce interim legacy styling, page-local themes, inconsistent controls, or incomplete responsive/accessibility states.
- For the user-authorized 2026-08-24 Board-family structural redesign, PUB-REF-02, PUB-REF-03, PUB-REF-04, PUB-REF-14, and PUB-REF-15 are reactivated visual targets. The user-supplied current Board screenshots are rejection evidence, not targets. The Admin workspace is an approved distinct compact operational shell that reuses the same semantic token discipline, control hierarchy, focus treatment, responsive/accessibility rules, and motion rules; it may use its own charcoal/slate operational surfaces and denser shell geometry. Do not create page-local themes or a second navigation framework.
- Use accent-outline controls as the normal primary action treatment. Use neutral outline for secondary actions, red outline for destructive actions, ghost/text for low-priority navigation, and green only when the action itself is explicitly a success action. Filled accent buttons are exceptional. A bare red `×` with a larger invisible circular hit target is approved only when the removable object is visually self-evident; preserve its accessible label and keyboard focus state.
- Preserve the approved Board behavior except for the user-authorized 2026-08-24 navigation correction: overall team cards now navigate normally to the existing route-backed team page at every viewport. Tiles replace the left sidebar through nested real URLs; captain submission attaches a drawer to that sidebar; submission success/failure stays in the drawer until the user acknowledges it; and realtime invalidations must not interrupt an active submission or result state.
- The team route is the ordinary rendered destination, not a popup fallback. Preserve direct loads, reload/history, browser Back, View all teams, adjacent-team navigation, tile URLs, and the shared Captain submission drawer transport endpoint; the Captain Submit endpoint still has no standalone no-JavaScript page acceptance requirement.
- The Board pass is a structural presentation rewrite from the reactivated references where the current composition is wrong. A palette, typography, border, or card-style reskin over the rejected masthead or team workspace does not satisfy the pass. The user explicitly protected the current team-overview grid beneath the masthead as already near target; preserve it, integrate it with the corrected masthead, and add the missing Recent Activity footer from PUB-REF-02. Replace the team-workspace composition while preserving authoritative data and the protected tile/sidebar/submission/evidence behavior.
- Treat the public board ecosystem, board editor, and live draft as protected interaction baselines. Functional slices may add or change necessary data, controls, validation, and states, but must preserve each surface's established hierarchy, density, spatial context, and primary interaction model unless an approved requirement genuinely needs a focused redesign.
- At the approved desktop reference viewport, ordinary board-editor and live-draft work should normally remain within the application viewport, with long boards, pools, lists, or panels scrolling inside their intended regions. This is not a prohibition on page scrolling: smaller viewports, zoom, translated content, and accessibility/responsive fallbacks may use normal document scrolling, and content must never be clipped merely to avoid it.
- Before changing a protected surface, identify the exact functional delta and keep unrelated layout and interaction behavior intact. If the requirement cannot fit the established interaction model, call out the proposed change for focused review instead of silently replacing the composition.
- Start by identifying the page's user and primary task; preserve approved business behavior.
- Reuse shared components and compact layout patterns.
- Review desktop, narrow/mobile, keyboard, focus, empty, error, and permission states where applicable. Do not create a separate no-JavaScript review or gate.
- Every mutation needs accurate success/failure feedback. Server authorization and validation remain authoritative.
- Obtain user approval before moving to the next roadmap pass when the roadmap requires it.

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

- The requested behavior is implemented through the appropriate layers.
- Relevant tests were added or updated and pass.
- The release build passes with no unexpected warnings.
- Formatting verification passes.
- Authorization, validation, audit, concurrency, privacy, and lifecycle effects were considered where relevant.
- UI changes satisfy the applicable roadmap checks and user approval gate.
- Database changes include consistent migrations and were exercised against PostgreSQL.
- Documentation and `CURRENT_STATUS.md` were updated if behavior, architecture, commands, roadmap position, or known verification state materially changed.
- Any unrun checks or remaining limitations are explicitly handed off.

Milestones are complete only when their documented completion criteria are satisfied—not merely because corresponding files or pages exist.

# Repository Instructions for Codex

## Start here

This repository is the OSRS Community Bingo platform. Before substantial work, read this file and `CURRENT_STATUS.md`, then read only the source-of-truth documents relevant to the task.

Do not reconstruct the project from prior chat history. Durable repository evidence is authoritative.

## Sources of truth

Use these documents by responsibility:

1. `CURRENT_STATUS.md` — current implementation position, working-tree handoff, verification results, remaining work, and uncertainties.
2. `PRODUCT_REQUIREMENTS.md` — product behavior, scope, roles, workflows, non-functional requirements, and version-one acceptance criteria.
3. `FUNCTIONAL_WORKFLOWS.md` — Planning Pass 2 workflow journeys, capability register, traceability, proposals, approvals, and open decisions.
4. `DATA_MODEL.md` — entities, invariants, calculations, evidence/progress/ranking rules, concurrency, and finalization semantics.
5. `TECHNICAL_ARCHITECTURE.md` — architecture, persistence, storage, security, realtime behavior, deployment, operations, and test strategy.
6. `IMPLEMENTATION_ROADMAP.md` — milestone order, completion criteria, release gates, risks, and deferred work.
7. `UI_OVERHAUL_ROADMAP.md` — active UI rules, pass sequence, page approval gates, accessibility, responsiveness, and progressive enhancement.
8. `README.md` — commands and local operation. Its milestone-summary sentence is currently stale; see `CURRENT_STATUS.md`.
9. `DEVELOPMENT_SETUP.md` — developer-machine setup.

Read focused sections instead of dumping whole large documents into context. Search headings and terms first, then inspect the relevant ranges.

When documents, code, tests, or current behavior conflict, do not silently choose one. Report the conflict, determine which artifact is stale where possible, and update the correct source of truth when the task authorizes it.

## Architecture and business-rule boundaries

- Preserve the `Web` → `Application` → `Domain`/`Infrastructure` separation and the architecture tests.
- Keep business rules explicit in domain/application code; do not hide authoritative rules in Razor, JavaScript, or database triggers.
- PostgreSQL/EF Core is authoritative persistence. Approved evidence is authoritative for progress.
- Preserve event snapshots and history. Do not rewrite historical competitive records for convenience.
- Preserve authorization, audit, transaction, concurrency, privacy, evidence-integrity, and finalization protections.
- Treat SignalR messages as invalidations/notifications, not authoritative secret state.
- Keep ordinary Razor form/navigation fallbacks when progressively enhancing interactions.
- For ordinary Admin filters and independent Admin value saves, no-JavaScript parity is best-effort rather than a universal acceptance gate; retain existing inexpensive fallbacks, but do not add fallback-only complexity. The responsive route fallbacks for protected board, team, tile, and submission surfaces remain required.
- Do not add deferred post-version-one features unless the user explicitly expands scope.
- Never commit secrets, `.env` files, real participant data, or production credentials.

## Working-tree safety

- Begin with `git status --short --branch` and inspect only bounded diffs relevant to the task.
- Existing modifications belong to the user. Preserve them and do not revert, overwrite, stage, or commit them unless explicitly asked.
- Do not use destructive Git or filesystem commands without explicit authorization.
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

If blocked, useful work may include focused source inspection, independent unit tests, documentation reconciliation, or a precise handoff. Do not claim completion while required verification remains blocked.

## Task roles and lean orchestration

Every delegated task must declare exactly one role and remain within it:

- **Orchestrator/planner:** defines bounded tasks, selects the next worker, verifies handoffs, and stops repeated failures. It does not implement production behavior or review its own work. It asks the user only for product decisions, explicit permissions, environment blockers, and manual acceptance.
- **Implementer:** completes only the assigned pass or correction. It does not begin the next pass, perform independent review, package, commit, or push unless explicitly assigned.
- **Remediator:** addresses only named findings with the smallest safe change. It does not reopen the whole pass or add unrelated cleanup.
- **Independent reviewer:** remains read-only and blocks only on a concrete behavior, security, privacy, authorization, concurrency, or data-integrity defect, or a genuinely non-discriminating required test. It must not demand redundant assertion syntax, exhaustive duplicate coverage, or stylistic expansion.
- **Verifier:** runs only the agreed gates, reports unrelated failures separately, and does not change production behavior.
- **Packager:** acts only after acceptance and may stage, commit, merge, and push the accepted state as authorized. It must not introduce implementation changes.

Prefer the simplest implementation that preserves the required invariants:

- Extend existing services, entities, pages, policies, and shared components before adding new abstractions.
- Do not add a table, service, compatibility layer, or generalized framework without a concrete persistence, transaction, authorization, or reuse need.
- Avoid speculative future-proofing, broad cleanup during a feature pass, and no-JavaScript-only machinery for ordinary controls. Retain required route-backed fallbacks for protected board, draft, team, tile, and submission interactions.
- Manual-test findings should receive the smallest bounded correction that fixes the demonstrated behavior.

Use risk-based, non-duplicative testing:

- Use the smallest test set that would fail if an important requirement or risk boundary broke.
- One scenario or parameterized test may prove several closely related behaviors.
- Do not create one test for every branch by default.
- Avoid repeating the same assertion across domain, handler, HTTP, browser, and migration layers unless each layer protects a distinct plausible failure.
- Expand coverage for authorization, privacy, transactions, concurrency, destructive lifecycle changes, retained migrations, and regressions that previously escaped the suite.
- Focused tests are the normal per-pass gate. Run the complete suite only at the documented final gate or when the blast radius genuinely warrants it.

Allow one implementation attempt and, when interrupted, one bounded continuation. If the same worker or approach fails repeatedly, change the approach or create a fresh task from the last verified state; do not loop indefinitely.

## UI work

- Follow the applicable pass and approval gate in `UI_OVERHAUL_ROADMAP.md`.
- Every new page and every materially changed page must follow the active shared UI system in `UI_OVERHAUL_ROADMAP.md` from its first implementation. A later whole-site UI pass is not permission to introduce interim legacy styling, page-local themes, inconsistent controls, or incomplete responsive/accessibility states.
- Treat the **View bingo** state at `/Events/test-15-dkl-live/Board` and `UI_OVERHAUL_ROADMAP.md` section 3.6 as the visual source of truth for the site-wide overhaul. Reuse its shared tokens, spacing scale, component geometry, density, states, controls, responsive transitions, and motion rules; do not create a separate admin or page-local theme.
- Use accent-outline controls as the normal primary action treatment. Use neutral outline for secondary actions, red outline for destructive actions, ghost/text for low-priority navigation, and green only when the action itself is explicitly a success action. Filled accent buttons are exceptional. A bare red `×` with a larger invisible circular hit target is approved only when the removable object is visually self-evident; preserve its accessible label and keyboard focus state.
- Preserve the approved Pass 12 desktop interaction model: overall team cards open route-backed team overlays; tiles replace the left sidebar through nested real URLs; captain submission attaches a drawer to that sidebar; and submission success/failure stays in the drawer until the user acknowledges it. Realtime invalidations must not interrupt an active submission or result state.
- Widths below `901px` deliberately use ordinary route navigation without overlay transitions. Treat the standalone team, tile, and submission routes as required responsive/no-JavaScript fallbacks, not obsolete duplicate pages.
- Treat the public board ecosystem, board editor, and live draft as protected interaction baselines. Functional slices may add or change necessary data, controls, validation, and states, but must preserve each surface's established hierarchy, density, spatial context, and primary interaction model unless an approved requirement genuinely needs a focused redesign.
- At the approved desktop reference viewport, ordinary board-editor and live-draft work should normally remain within the application viewport, with long boards, pools, lists, or panels scrolling inside their intended regions. This is not a prohibition on page scrolling: smaller viewports, zoom, translated content, and accessibility/responsive fallbacks may use normal document scrolling, and content must never be clipped merely to avoid it.
- Before changing a protected surface, identify the exact functional delta and keep unrelated layout and interaction behavior intact. If the requirement cannot fit the established interaction model, call out the proposed change for focused review instead of silently replacing the composition.
- Start by identifying the page's user and primary task; preserve approved business behavior.
- Reuse shared components and compact layout patterns.
- Review desktop, narrow/mobile, keyboard, focus, empty, error, permission, and no-JavaScript states where applicable.
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

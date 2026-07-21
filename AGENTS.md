# Repository Instructions for Codex

## Start here

This repository is the OSRS Community Bingo platform. Before substantial work, read this file and `CURRENT_STATUS.md`, then read only the source-of-truth documents relevant to the task.

Do not reconstruct the project from prior chat history. Durable repository evidence is authoritative.

## Sources of truth

Use these documents by responsibility:

1. `CURRENT_STATUS.md` — current implementation position, working-tree handoff, verification results, remaining work, and uncertainties.
2. `PRODUCT_REQUIREMENTS.md` — product behavior, scope, roles, workflows, non-functional requirements, and version-one acceptance criteria.
3. `DATA_MODEL.md` — entities, invariants, calculations, evidence/progress/ranking rules, concurrency, and finalization semantics.
4. `TECHNICAL_ARCHITECTURE.md` — architecture, persistence, storage, security, realtime behavior, deployment, operations, and test strategy.
5. `IMPLEMENTATION_ROADMAP.md` — milestone order, completion criteria, release gates, risks, and deferred work.
6. `UI_OVERHAUL_ROADMAP.md` — active UI rules, pass sequence, page approval gates, accessibility, responsiveness, and progressive enhancement.
7. `README.md` — commands and local operation. Its milestone-summary sentence is currently stale; see `CURRENT_STATUS.md`.
8. `DEVELOPMENT_SETUP.md` — developer-machine setup.

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
10. Update `CURRENT_STATUS.md` only when status materially changes; do not turn it into a minute-by-minute log.

If blocked, useful work may include focused source inspection, independent unit tests, documentation reconciliation, or a precise handoff. Do not claim completion while required verification remains blocked.

## UI work

- Follow the applicable pass and approval gate in `UI_OVERHAUL_ROADMAP.md`.
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

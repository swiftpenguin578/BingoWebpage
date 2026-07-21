# Current Project Status

**Verified:** 2026-07-22  
**Branch:** `main` at `85f7c53` (`Clarify board tile objectives and actions`)  
**Purpose:** Durable handoff for future work. This is a snapshot, not a chronological work log.

## Current objective

Milestone 9 UI overhaul and regression is the active workstream. The committed repository contains the version-one workflows through event finalization, plus the global shell and admin workflow overhaul. The current uncommitted work continues refinement of the admin board editor.

The latest focused fixes make populated tiles fill the same grid-row height as empty positions, remove automatic board-lease release during `pagehide`, and refine the tile dialog layout. Board control is now released explicitly through **Finish editing**, by five minutes of inactivity, or by administrator takeover. Each eligible drop now has a board-defined weight (default `1`), allowing cases such as ordinary Theatre of Blood purples counting for `1` and Scythe of Vitur counting for `2`; submitters and reviewers cannot override it. Catalogue probabilities and efficient-completion rates are stored as reviewed scenario pairs: group size is represented exactly once through the personal in-name probability while the completion rate remains the practical KC earned per hour. Raid-specific conversions remain outside the generic calculator. Board EHB uses a completion-outcome/state calculation for weighted, distinct, multi-roll, and alternative-source objectives. The reviewed catalogue snapshot aligns ordinary rates with the supplied DKL practical reference while using explicit group assumptions: Royal Titans `44` with duo personal rates, Hueycoatl `16` with trio personal rates, Nex `16` with five-player personal rates, normal ToB/ToB HM/ToA/Expert ToA at `3`, supplied level-300 Expert ToA probabilities, and supplied four-player ToB HM probabilities. Corp, Leviathan, and Zulrah remain unchanged; the broken Chaos Fanatic workbook formula and bosses absent from DKL are preserved. Ambiguous data requires a manual override. The development reset also creates `TEST 13 — DKL Board`, an editable 5×5 draft following the historical workbook order. The user approved the board-editor pass on 2026-07-22 after the iterative visual, state, weighting, concurrency, catalogue, and EHB checks in this workstream.

Milestones 10 (local rehearsal and production preparation) and 11 (production deployment and validation) are not complete. The application must not be described as production-ready until their documented gates are satisfied.

## Evidence-based implementation status

### Implemented in the committed repository

The commit history, source tree, migrations, pages, and tests provide implementation evidence for:

- Application foundation: .NET 10 solution, Razor Pages web application, PostgreSQL/EF Core, health endpoints, structured project boundaries, Docker Compose, and CI.
- Identity, authorization, and audit: administrator/captain accounts, setup and login flows, access policies, account administration, and audit records.
- Events and signup: event configuration, signup questions, public signup/edit links, capacity, waiting list, CSV workflow, and participant administration.
- OSRS catalogue and board building: catalogue administration/import preview, board snapshots, tile requirements, drop-rate variants, EHB calculations, board editing, publication, and editing leases.
- Teams and draft: teams, memberships, snake-draft state and picks, undo/control workflows, captain accounts, and concurrent administration notifications.
- Evidence and review: submissions, local and R2 storage providers, evidence validation, review transitions, privacy controls, reversal, and captain/admin pages.
- Public progress: public event/team/board/tile pages, approved-progress calculations, rankings, evidence visibility, SignalR invalidation, and refresh fallback.
- Event operations and finalization: scheduled lifecycle worker, final-review resolution, completion corrections, official placement snapshots, finalization/unfinalization, archive behavior, and seeded manual-test scenarios.
- Milestone 9 foundation and admin passes: localized shared navigation, event context, typed UI messages, role-based notification inbox, account settings, and redesigned admin event/catalogue/board/draft/review/finalization workflows.

This list establishes implementation coverage, not formal acceptance of every roadmap criterion. Milestone completion still requires the applicable automated, browser, accessibility, manual, and user-approval gates.

### Active uncommitted work

The working tree had eight pre-existing modified files when this status was created. They belong to the user and must not be reverted or overwritten casually:

- `IMPLEMENTATION_ROADMAP.md`: adds production-scale capacity rehearsal requirements and a post-version-one backlog.
- `PRODUCT_REQUIREMENTS.md`: adds 100-viewer load and cached/live-update requirements.
- `TECHNICAL_ARCHITECTURE.md`: requires targeted or jittered progress refresh at production scale.
- `src/Bingo.Web/Pages/Admin/Events/Board.cshtml`: refines board/tile presentation, requirement details, workload wording, and the tile editor.
- `src/Bingo.Web/Pages/Admin/Events/_BoardRequirementEditor.cshtml`: simplifies objective controls and wording.
- `src/Bingo.Web/wwwroot/css/site.css`: styling for the board-editor refinement.
- `tests/Bingo.BrowserTests/BoardEditingUiTests.cs`: updates board-editor UI assertions.
- `tests/Bingo.IntegrationTests/SignupCapacityTests.cs`: adds a direct regression test for promoting the earliest waiting participant after removal before draft lock.

`src/Bingo.Web/wwwroot/js/admin-collaboration.js` was subsequently modified to remove the fragile page-exit lease-release request while retaining explicit release, renewal, expiry, and takeover. The active changes also add board-defined per-drop weights through template links, immutable drop snapshots, board editing, and submission enforcement; submitter/reviewer forms no longer choose the credited weight.

Migrations `20260721142608_AddBoardDefinedRequirementWeight` and `20260721144023_AddPerDropCreditedWeight` introduce credited weights and place the active value on template-drop links and immutable requirement-drop snapshots. Migration `20260721150631_AddTileImageOverride` adds an optional custom image URL to tile templates and its immutable board-tile snapshot.

## Verification performed on 2026-07-21

- `dotnet build Bingo.slnx --configuration Release --no-restore`: **passed**, 0 warnings and 0 errors.
- Domain tests: **44 passed**.
- Application tests: **39 passed**.
- Browser tests: **35 passed, 1 failed**. The single failure was the database-backed home-page test because PostgreSQL was unavailable at `localhost:5432`.
- Integration tests: **3 passed, 30 failed during test setup** because the Docker daemon was not running; Testcontainers could not create PostgreSQL. These failures did not reach behavioral assertions.
- `docker compose up -d postgres` was attempted once and confirmed that the Docker daemon was not running. It was not retried.
- `dotnet format Bingo.slnx --no-restore --verify-no-changes`: **failed** on widespread pre-existing whitespace/import-order and migration-encoding findings across the repository. The formatter was run in verification-only mode and changed no files. This is repository-wide formatting debt, not evidence of a build failure and not isolated to the active board-editor changes.
- Focused `BoardEditingUiTests` after the tile-height and lease-release fixes: **3 passed**.
- Release build after those fixes: **passed**, 0 warnings and 0 errors.
- Focused board-rule tests after board-defined weighting: **10 passed**.
- Application tests after board-defined weighting: **39 passed**.
- Focused `BoardEditingUiTests` after the final tile-dialog controls: **3 passed**.
- With Docker/PostgreSQL confirmed healthy, focused submission-workflow integration tests: **12 passed**.
- Complete solution test run after the final board-editor and weighting changes: **45 domain, 39 application, 36 browser, and 33 integration tests passed**; no failures or skips.
- Complete solution test run after correcting weighting to be per eligible drop: **45 domain, 39 application, 36 browser, and 33 integration tests passed**; no failures or skips.
- Release build after the per-drop migration and Razor build-path setting: **passed**, 0 warnings and 0 errors.
- Complete solution test run after the tile-image override: **45 domain, 39 application, 36 browser, and 33 integration tests passed**; no failures or skips.
- Release build after the tile-image override: **passed**, 0 warnings and 0 errors.
- Complete solution test run after structured drop-rate mechanics and the outcome/state EHB calculator: **47 domain, 42 application, 36 browser, and 33 integration tests passed**; no failures or skips.
- Migration `20260721160457_AddStructuredDropRateMechanics` was successfully applied to the local PostgreSQL database.
- Release build after the structured rate/EHB work: **passed**, 0 warnings and 0 errors.
- Personal-rate calculation verification: **44 application and 47 domain tests passed**; focused Wiki catalogue tests **20 passed**; release build passed with 0 warnings and 0 errors. The local PostgreSQL catalogue was updated with reviewed in-your-name rates for normal ToB and CoX plus provisional raid-level-400 Expert ToA rates.
- Historical-board reference coverage now exercises all 25 tiles from `bingo dkl.xlsx`: detailed scenarios cover Royal Titans, Duke/Whisperer, Corp, weighted ToB/CoX/ToA, and separate Barrows/Moons subtotals; aggregate napkin ranges cover the remaining drop tiles; GWD is explicitly labelled a non-authoritative proxy; and Skilling Slayer verifies the manual-override path. All **25 reference cases** pass, the complete application suite has **69 passing tests**, and the release build remains clean.
- The development reset completed successfully with PostgreSQL and created `TEST 13 — DKL Board`. Direct database verification confirmed a Draft 5×5 board with 25 tiles in workbook order and exactly five weight-2 selections: Scythe of vitur, Tumeken's shadow, Elder maul, Kodai insignia, and Twisted bow. The 26 focused historical-board reference tests passed, the web-project build passed with 0 warnings and 0 errors, and the focused diff check was clean.
- Wave-based catalogue assumptions were corrected and applied locally: a full 12-wave Fortis run now estimates six Sunfire pieces at **26.67 EHB**, and Doom uses **14 counted KC/hour** with effective full-delve 1–16 rates to estimate two eligible uniques at **19.84 EHB**. Barrows equipment now records seven rolls at `1/2448` per selected piece; Barrows and Moons remain separate required objectives and total **10.74 EHB**. An uncalculable objective now makes its whole tile uncalculable instead of silently contributing zero. The DKL seed preserves the reviewed **25 EHB** Araxxor destroy-strategy estimate and the catalogue-edited Hueycoatl result of **31.5 EHB**.
- Catalogue display rates now use consistent input-friendly notation: multiple rolls use multiplier-first forms such as `7 × 1/2448`, reviewed personal rates display only the rate while their assumptions remain in the condition note, and future Wiki imports preserve multiplier-first notation. The seven calculable Expert ToA purple rates display rounded `1/x` equivalents while retaining their exact decimal probabilities. Grotesque Guardians' Adamant boots now has the missing two-roll numeric mechanics. Twenty-two active `(+1 variants)` display markers remain intentionally unresolved (27 including inactive rows); they were not flattened or guessed. All catalogue images currently hotlink OSRS Wiki `Special:Redirect/file` URLs, so image availability depends on the Wiki; local caching or application-served images remains a future reliability improvement.

The reviewed OSRS catalogue is now exported as a complete, version-controlled snapshot at `src/Bingo.Web/data/osrs-catalogue.json` (68 bosses, 311 items, 441 drops, and 479 rate variants). It includes the current local manual corrections. Twenty-two active source-drop display rates still contain an unresolved `(+1 variants)` marker (27 including inactive rows); these are preserved rather than guessed. The application provides explicit export and safe apply commands; apply updates the older catalogue created by committed migrations, adds missing records, and avoids deleting records that historical boards may reference. A PostgreSQL integration test verifies reconstruction through a freshly migrated database.

The eight previously pending migrations were consolidated into the single schema migration `20260721232916_AddBoardEditorAndCatalogueRateMechanics`. Catalogue values are no longer carried through a chain of data-only migrations; the reviewed snapshot is their deployment source of truth. The consolidated migration and snapshot were applied successfully to local PostgreSQL, the development scenarios were rebuilt, all 47 domain, 71 application, 36 browser, and 34 integration tests passed, and the release build passed with 0 warnings and 0 errors.

The complete automated suite and release build were verified after the catalogue snapshot and migration consolidation. Manual cross-application responsive, keyboard, and accessibility checks remain separate milestone-wide roadmap gates.

## Remaining work

### Milestone 9 — UI overhaul and regression

- Preserve the approved board-editor behavior while completing later UI passes.
- Verify which other UI roadmap passes beyond the board editor and committed admin overhaul have received explicit user approval; commit history alone does not prove approval gates.
- Complete remaining captain, public, authentication/error/privacy, responsive, keyboard, accessibility, and full-regression passes required by `UI_OVERHAUL_ROADMAP.md`.
- Perform the roadmap's manual browser checks with the seeded scenarios.
- Decide separately how to baseline or correct the repository-wide `dotnet format --verify-no-changes` failures; do not combine a bulk formatting rewrite with the active UI change without explicit approval.
- Reconcile the stale `README.md` statement that the repository contains only Milestones 1–7.

### Milestone 10 — Local rehearsal and production preparation

No evidence establishes completion of the release-candidate rehearsal or production preparation gate. Outstanding categories include:

- Full end-to-end local rehearsal and recorded results.
- Production image/Compose assets and controlled migration/rollback workflow.
- Backup, encrypted retention, clean restore, and recovery rehearsal.
- Monitoring/alerting and operational runbooks.
- Local R2 verification with restricted credentials.
- Production-scale load rehearsal, including 100 connected viewers, SignalR update bursts, evidence traffic, and recorded resource/latency results.
- Resolution or measurement of synchronized full-page refresh behavior and image-delivery/query bottlenecks.

### Milestone 11 — Production deployment and validation

No production deployment should occur before Milestone 10's release gate. There is no repository evidence that production provisioning, deployment, smoke testing, production backup/restore, monitoring, or rollback validation has been completed.

### Post-version-one backlog

The uncommitted roadmap explicitly defers Discord sign-in, participant submission permissions, multiple OSRS accounts and swaps, team-only board focus, Wise Old Man synchronization/leaderboards, manual signup-opening deadlines, and related regression work. Do not mix these into version one without explicit scope approval.

## Known documentation mismatch

`README.md` says the repository contains Milestones 1–7. The committed source and history also contain concurrent-administration hardening, event finalization/operations, and substantial Milestone 9 UI work. Treat the README sentence as stale until it is deliberately updated.

## Recommended next action

Commit the approved board-editor, EHB, catalogue snapshot, consolidated migration, tests, and documentation as one coherent checkpoint. Then continue with the next explicitly selected UI pass without reopening approved board-editor decisions unless a regression is found:

1. Run the seeded manual browser checks.
2. Run `dotnet format Bingo.slnx --no-restore --verify-no-changes` only when deliberately addressing the recorded repository-wide formatting debt.
3. Record any remaining failures once, with their exact cause, and update this file when the workstream materially changes.

## Uncertainties

- Which Milestone 9 page passes beyond the board editor have explicit user approval where commit messages alone are insufficient evidence.
- Whether the complete integration/browser suite passed in a prior environment; only the current run is recorded here.
- Whether manual desktop/mobile, keyboard, accessibility, backup/restore, and event-lifecycle rehearsals were previously completed outside the repository.
- Whether any production infrastructure or external accounts exist; repository contents do not prove external state.

Do not convert these uncertainties into claims without new evidence.

# Admin simplification and backend rebuild — implementation plan

**Status: execution underway — 26 September 2026. The active coordinator and repository CURRENT_STATUS.md own package progress; the predecessor coordinator remains stopped.**

This complete plan preserves the original structure and unaffected ticket content. It incorporates the seven approved revision decisions, the subsequent exact-tie clarification and the five final textual corrections. The user approved product logic, ticket decomposition/dependencies and parallel execution. Those final corrections are now incorporated; this document is ready to serve as the implementation authority. The established implementation review and release gates remain in force.

## 1. Current-state reconciliation

### Implementation baseline

[PR #11](https://github.com/swiftpenguin578/BingoWebpage/pull/11) is merged. GitHub `main` was verified at:

`22af254c893bb51e7820d84fc4154ff9af3bcc90`

The inspected checkout is:

`/private/tmp/BingoWebpage-wom-managed-20260922`

Its clean branch, `codex/admin-wom-competitions`, ends at `e05976de428c6009b5bc31f9c4ff66c8007ae54a`. Its Git tree exactly matches merged `main`:

`0ae2573fd37fc52357206a3cc71c02e8a3d22d0b`

Consequently, this plan addresses the merged implementation, including the September improvements and subsequent CI corrections. Image publication and production promotion remain separate; neither is assumed complete.

**Before implementation**, create an isolated branch named `admin-simplification` from the then-verified merged `main`. Reconcile any subsequent production corrections first. Do not branch from this task’s older baseline or reuse the release branch.

The original planning pass made no file changes, created no branch, ran no tests, and accessed no production database or private fixture inputs. This revision writes only this Markdown planning artifact; it changes no application code or repository authority files. Ranking/completion source and existing assertions were reinspected, not executed.

### Inspected ownership

The principal implementation owners are:

| Area | Current source |
|---|---|
| Event state, timing and planning fields | [BingoEvent](/private/tmp/BingoWebpage-wom-managed-20260922/src/Bingo.Domain/Events/BingoEvent.cs), [EventStatePolicy](/private/tmp/BingoWebpage-wom-managed-20260922/src/Bingo.Domain/Events/EventStatePolicy.cs) |
| Creation and identity | [CreateModel](/private/tmp/BingoWebpage-wom-managed-20260922/src/Bingo.Web/Pages/Admin/Events/Create.cshtml.cs), [IdentityModel](/private/tmp/BingoWebpage-wom-managed-20260922/src/Bingo.Web/Pages/Admin/Events/Identity.cshtml.cs) |
| Schedule and lifecycle | [EventSignupLifecycleService](/private/tmp/BingoWebpage-wom-managed-20260922/src/Bingo.Infrastructure/Events/EventSignupLifecycleService.cs), [EventLifecycleService](/private/tmp/BingoWebpage-wom-managed-20260922/src/Bingo.Infrastructure/Events/EventLifecycleService.cs) |
| Signup and participant mutations | [SignupService](/private/tmp/BingoWebpage-wom-managed-20260922/src/Bingo.Infrastructure/Signups/SignupService.cs), [ISignupService](/private/tmp/BingoWebpage-wom-managed-20260922/src/Bingo.Application/Signups/ISignupService.cs) |
| Teams and draft | [DraftModel](/private/tmp/BingoWebpage-wom-managed-20260922/src/Bingo.Web/Pages/Admin/Events/Draft.cshtml.cs), [Team](/private/tmp/BingoWebpage-wom-managed-20260922/src/Bingo.Domain/Teams/Team.cs), [DraftSession](/private/tmp/BingoWebpage-wom-managed-20260922/src/Bingo.Domain/Teams/DraftSession.cs) |
| Board editing and approval | [BoardModel](/private/tmp/BingoWebpage-wom-managed-20260922/src/Bingo.Web/Pages/Admin/Events/Board.cshtml.cs) |
| Catalogue administration | [Catalogue IndexModel](/private/tmp/BingoWebpage-wom-managed-20260922/src/Bingo.Web/Pages/Admin/Catalogue/Index.cshtml.cs) |
| Evidence authorization and mutations | [EvidenceAuthority](/private/tmp/BingoWebpage-wom-managed-20260922/src/Bingo.Infrastructure/Evidence/EvidenceAuthority.cs), [SubmissionService](/private/tmp/BingoWebpage-wom-managed-20260922/src/Bingo.Infrastructure/Evidence/SubmissionService.cs) |
| Active Playing account | [ParticipantLiveService](/private/tmp/BingoWebpage-wom-managed-20260922/src/Bingo.Infrastructure/Signups/ParticipantLiveService.cs), [active-character queries](/private/tmp/BingoWebpage-wom-managed-20260922/src/Bingo.Infrastructure/Signups/EventParticipantActiveCharacterQueries.cs) |
| Official results | [EventFinalizationService](/private/tmp/BingoWebpage-wom-managed-20260922/src/Bingo.Infrastructure/Events/EventFinalizationService.cs), [PublicProgressCalculator](/private/tmp/BingoWebpage-wom-managed-20260922/src/Bingo.Application/Boards/PublicProgressCalculator.cs) |
| WOM operations | [EventCompetitionManagementService](/private/tmp/BingoWebpage-wom-managed-20260922/src/Bingo.Infrastructure/Events/EventCompetitionManagementService.cs), [EventCompetitionSynchronizationService](/private/tmp/BingoWebpage-wom-managed-20260922/src/Bingo.Infrastructure/Events/EventCompetitionSynchronizationService.cs) |
| Account administration | [AccountAdministrationService](/private/tmp/BingoWebpage-wom-managed-20260922/src/Bingo.Web/Security/AccountAdministrationService.cs), [AccountIdentityService](/private/tmp/BingoWebpage-wom-managed-20260922/src/Bingo.Web/Security/AccountIdentityService.cs) |
| Navigation and Admin actions | [SharedShellService](/private/tmp/BingoWebpage-wom-managed-20260922/src/Bingo.Web/Navigation/SharedShellService.cs) |
| Audit and feedback | [AuditWriter](/private/tmp/BingoWebpage-wom-managed-20260922/src/Bingo.Infrastructure/Auditing/AuditWriter.cs), [Audit page](/private/tmp/BingoWebpage-wom-managed-20260922/src/Bingo.Web/Pages/Admin/Audit/Index.cshtml), [UiMessage](/private/tmp/BingoWebpage-wom-managed-20260922/src/Bingo.Web/UI/UiMessage.cs) |
| Route-level mutation restrictions | [EventMutationCapabilityPageFilter](/private/tmp/BingoWebpage-wom-managed-20260922/src/Bingo.Web/Security/EventMutationCapabilityPageFilter.cs) |

Inspection covered these owners, directly affected models and interfaces, relevant Razor/JavaScript handlers, persistence constraints, workers, notification projections, and existing test families. It was source inspection, not runtime verification of the proposed behavior.

### Important differences from the prompt’s older baseline

1. **WOM creation, protected credentials, operation tracking and automatic synchronization already exist.** Preserve their concurrency, retry, unknown-outcome reconciliation and deletion safeguards. The work is consolidation and changed product rules.

2. **Hourly fetches and four-hour update-all scheduling are already implemented.** Preserve anchoring to the first actual start, fixed slots and the separation between manual refresh and automatic scheduling.

3. **Draft sizes are already derived.** `ConfigureTargetSize` rejects configuration. Reuse `DraftRosterDistribution`; remove obsolete callers, fields and terminology only where still present.

4. **Teams already contain `IncludedInDraft`.** However, `FormationType` still restricts eligibility and direct roster operations. The existing boolean is the foundation; changing its label alone would leave the old behavior intact.

5. **Request Changes is already historical-only.** `SubmissionStatus` has no active Request Changes state, and `ReviewActionType.RequestChanges` is explicitly retained history. Verify absence of active paths; do not implement another retirement.

6. **September ranking work already supplies completion facts and `CurrentScoreReachedAt`.** Final Review nevertheless still layers manual completion corrections, inspections, overrides and tie acknowledgements over it. Remove those future workflows and reuse the current calculator.

7. **The merged ranking implementation can genuinely leave an exact competitive tie.** `Rank` orders by completion, completion time, lines, tiles, first-to-current-score time and EHB, then uses team name only for display ordering. `SameRank` explicitly assigns equal competitive inputs the same rank. Completion facts preserve evidence-derived timestamps; they do not impose cross-team uniqueness. Exact ties can remain, including two teams with no approved progress. The user's subsequent clarification permits justified ties in these edge cases: distinct official ranks wherever the competitive inputs distinguish teams, shared rank only when every intended competitive input is exactly equal. Remove generic tie acknowledgement and manual completion corrections; never invent a team-name/ID/GUID fallback or allow an Admin to override a measurable competitive difference. Section 10 records the source evidence and approved narrow exception.

8. **Automatic descriptions and important tile rules already exist.** Preserve automatic/manual description semantics, mixed-objective restrictions, catalogue-derived EHB and evidence protection. Simplify interaction and fill actual gaps.

9. **Submission authorization differs between layers.** The submission page rejects the Administrator actor kind, but `EvidenceAuthority.AuthorizeAsync` still grants it a service-level bypass. Correct the authoritative boundary.

10. **The delayed Playing-account switch is still implemented.** WOM projections aggregate current Playing assignments rather than the active attribution account. No inspected WOM dependency requires the minute boundary. Immediate switching needs ordering and concurrency protection, not a scheduling replacement.

11. **Signup-code behavior needs correction.** The current signup save checks the code for existing registrations, while the separate rejoin interface carries no signup code. Admission-only gating therefore requires coordinated service and route changes.

12. **Board loading does the expensive work described in the prompt.** It loads all editor requirements and catalogue choices and derives live estimates. Performance work needs explicit freshness ownership.

13. **Existing UI authority conflicts with the new confirmation contract.** It currently endorses some inline confirmations. The new prompt supersedes that policy for this project; update the authority before page migration.

14. **Production data presence is unverified.** Nothing in this plan assumes banners, emergency identities, paused drafts, legacy Finalized events or linked resubmissions are absent.

### Preserved merged behavior

The following remain regression boundaries:

- WOM account validation and approved provider-failure handling.
- Managed competition retry and unknown-outcome handling.
- Fixed fetch/update-all schedules.
- Tile completion facts and current-score timing.
- Admin drop NEW state and event-scoped announcements.
- Derived descriptions and publication snapshots.
- Original submission timestamps in Recent Activity.
- Shared Board/Stats masthead.
- Boss sentinel/average formatting.
- Public countdown behavior.
- Isolated PostgreSQL tests and the CI shard-coverage checks.

## 2. Target architecture and product invariants

### Product ownership

| Owner | Responsibilities |
|---|---|
| Overview | Lifecycle, readiness, status, summaries and next actions |
| Identity | Name, description, buy-in, timezone |
| Schedule | Signup opening/closing, draft time, event start/end |
| Participants / Signups | Form configuration, account fields, admission code, capacity, signup records and private administration |
| Teams / Draft | Team setup, draft participation, picks, publication and permitted roster corrections |
| Board | Board/tile editing, approval, publication and protected corrections |
| WOM | Linking, creation, synchronization, provider health and exceptional operations |
| Review | Evidence decisions and Pending metadata corrections |
| Final Review | Closed-event review, official publication and exceptional reopening |
| Accounts | Account support, roles, disable/restore and recovery |
| Audit | Human-readable administrative history |

The dashboard remains outside redesign scope.

### Backend boundaries

- Domain objects enforce local invariants.
- Application/Infrastructure operations own transactions, authorization rechecks, concurrency, persistence and provider coordination.
- Razor PageModels bind requests and render outcomes. Extract cohesive operations from large PageModels when the ticket requires it; do not create a generic workflow framework or one service per handler.
- Readiness and control availability must agree with mutation-time rules. Introduce narrowly scoped projections where needed, rather than a second permission system.
- PostgreSQL remains authoritative. Realtime messages invalidate or refresh views.
- Shared interface/result changes are limited to information needed by the affected workflows.

### Core invariants

- Normal event lifecycle: **Draft → Signup Open → Signup Closed → Live → Final Review → Archived**.
- Draft state remains separate: **Setup → Running → Finalized** for an actual website draft. With zero or one included team, use direct **Finalize roster** from setup and persist that no website draft occurred. Downstream gates require a finalized roster, not fabricated Running state or picks.
- A private Running draft may return to Setup only with zero active picks. Existing picks must first be undone one at a time through Undo last pick; finalized drafts cannot reopen.
- First actual Live start permanently closes roster membership and registration changes.
- Disabling an account removes access; it does not remove its participant from a fixed roster.
- Ordinary participant identity always belongs to a user-created website account.
- One active event registration per OSRS character, across both Playing and Alt roles.
- At least one Playing field exists; the first is required/protected and extras are optional. Zero Alt fields is valid; every configured Alt field is optional.
- Captain/Co-captain authority comes from current team membership; global Admin status adds no submission authority.
- Active-account switching changes future attribution only.
- Official placements, evidence history, published board snapshots and original draft picks remain immutable.
- New official placements use distinct ranks whenever the approved competitive inputs distinguish teams. The approved edge-case exception permits a shared rank only for exact equality across every intended competitive input. No manual tie approval/override or arbitrary fallback is introduced. Existing official snapshots remain unchanged.
- Publishing corrected versions creates new history; it does not rewrite old versions.
- Provider failure does not block otherwise valid event lifecycle transitions.
- WOM provenance and write capability are separate: website-created/protected credential supports synchronization and eligible pre-Live deletion; external ID-only links support reads only; external links with a protected credential support synchronization but never remote deletion. Neither automatic update-all nor any other upstream write is permitted for an ID-only link.
- Shared public behavior changes only where the prompt explicitly requires it.

### Common Audit and feedback contract

These requirements apply to **every ticket below**, including its handlers, service calls and background consequences.

**Audit**

- Identify actor, event where applicable, target, semantic action and useful before/after values.
- Record reasons only for the actions designated in the prompt.
- Persist successful local mutation history atomically with the mutation.
- Do not report a rolled-back attempt as a completed change.
- Keep credentials, tokens, password material and signup-code values out of Audit.
- Preserve private-data visibility restrictions in summaries and technical details.
- Where authoritative domain history is sufficient, expose or link it instead of blindly duplicating it.
- Provider operations distinguish request, confirmed outcome and unknown outcome.
- Automatic successful fetches belong to WOM operational history, not the global Audit stream.

**Feedback**

Every touched operation must explicitly handle applicable:

- success and already-completed/no-change outcomes;
- field validation;
- duplicate/conflict;
- stale edit;
- changed lifecycle;
- changed permission;
- dependency blockers;
- provider unavailability, rate limiting and incomplete responses;
- unexpected failures.

Messages state what happened, whether anything was saved, and the next useful action. Preserve safe input after failure. Use explicit severity rather than guessing it from English text. Field errors remain beside fields; durable recovery instructions must not exist only in a disappearing toast.

Unexpected failures receive a safe action-specific message and diagnostic reference. Do not expose raw exceptions or falsely promise “nothing changed” when an upstream operation has an unknown outcome.

### Scope of UI work in this plan

This project includes functional interaction changes required by the backend simplification: one confirmation primitive, useful feedback, early validation, explained locks and page ownership.

It does **not** include the later visual redesign, dashboard redesign, public-page redesign, a new frontend stack, or preservation of every existing popup as a popup.

## 3. Retired-concept inventory

“Historical compatibility” means retaining enough data and interpretation to read old records, without retaining future mutation authority.

| Concept | Actual footprint | Classification and destination |
|---|---|---|
| Emergency Captain credentials | Authentication, cookie validation, authorization, account services/pages, evidence authority, Team Focus, draft, lifecycle worker/readiness, shell and access records | **Delete active behavior; historical compatibility.** SEC-01 |
| Accountless/Admin-created participants | `SignupService`, nullable participant ownership, participant forms, external-team handlers, CSV importer, account attribution | **Replace with existing website-account selection; historical compatibility.** TEM-01, PAR-01, ROS-01 |
| Participant ownership transfer | Signup interface/service, participant handler and domain ownership mutation | **Delete future workflow.** Retain old ownership/audit records. TEM-01 |
| Preformed team type | `TeamFormationType`, `Team` constructor restrictions, draft pool calculations, direct roster handlers and projections | **Replace with `IncludedInDraft`.** Historical values may remain inert. TEM-01 |
| Application CSV roster import | `PreformedRosterCsvImportService`, Draft handlers, DI and tests | **Delete application workflow.** Preserve separate historical/operator import needs. TEM-01 |
| Manual draft target size | Legacy property/API; configuration already rejected | **Delete remaining product surface; keep harmless historical storage if needed.** DRF-01 |
| Draft Pause/Resume | Domain state/methods, Draft handlers and UI | **Delete future transitions; historical compatibility.** DRF-01 |
| Reopen finalized draft | Draft domain/handler, publication cycles | **Delete future workflow.** Direct pre-Live roster changes replace it. DRF-01, ROS-01 |
| Live withdrawal/replacement/vacancies | `SignupService`, request/result types, membership links, participant handlers, validation tokens and shell actions | **Delete future behavior; historical compatibility.** ROS-01 |
| Waiting-list promotion follow-up | Entity, replacement creation, completion handler and shell projection | **Delete workflow and actions; preserve history only where required.** ROS-01, ACT-01 |
| Special Resubmit | Submission command/service, participant handlers, predecessor relationship and review history | **Delete future workflow; retain old relationships for reading.** EVD-02 |
| Request Changes | Historical review enum only in inspected source | **Keep historical interpretation; verify no active path.** EVD-02 |
| Completion inspection and overrides | Final-review resolutions, completion corrections, service operations, page handlers | **Delete future operations; historical compatibility.** RES-01 |
| Tie confirmation | Final-review blockers layered over current ranking | **Delete generic acknowledgement.** Apply all competitive inputs; preserve a shared rank only for the user's approved exact-tie edge case, never through discretionary override or arbitrary ordering. RES-01 |
| Separate Archive action / future Finalized resting state | Event policy, finalization service, directory/navigation/current-event predicates | **Replace with atomic publish-to-Archived.** Read legacy Finalized records. RES-01 |
| Event banners | Creation/Identity, asset route, assets, cleanup records/service and destructive deletion | **Delete fully if production evidence permits.** BNR-01 |
| Dead planning fields | `BingoEvent.ConfigurePlanning`, creation, Overview and Board callers | **Delete future API/product usage.** Preserve harmless historical columns initially. EVT-01, BRD-01 |
| Scheduled-opening toggle | Event property, Schedule, readiness, worker selection | **Replace with timestamp-driven scheduling.** LIF-01 |
| Waiting-list toggle | Signup administration/admission and event configuration | **Replace with always-enabled waiting while open.** SGN-02 |
| WOM “use provider dates” | Creation and synchronization configuration | **Delete.** Website dates remain authoritative. EVT-01, WOM-01 |
| WOM editor on Manage | Manage handlers, forms and event JavaScript | **Replace with dedicated WOM owner.** WOM-02 |
| Missing-Captain/vacancy/follow-up actions | `SharedShellService`, participant pages and directory projections | **Delete these action categories.** ACT-01 |
| Browser/inline/page-specific confirmations | Razor handlers, `<details>`, inline boxes and several JS owners | **Replace product-action confirmation behavior.** Retain native `beforeunload` only for browser-level exit with genuinely unsaved edits. ADM-02 and owning tickets |
| Catalogue roll-group editing | Catalogue inputs and `SourceDrop` rate mechanics | **Delete Admin editing; keep domain/operator mechanics.** CAT-01 |
| In-application bulk/Wiki import | No active import handler found in inspected Catalogue page; operator commands exist | **Verify no reachable UI; preserve operator tooling.** CAT-01 |
| TileTemplate | Internal board persistence | **Keep unless a concrete ticket needs simplification.** No reusable-template product |
| Leases, concurrency, snapshots, provider operation records | Existing safety infrastructure | **Keep; simplify presentation.** No replacement framework |

## 4. Dependency graph and execution waves

Waves describe a conservative, conflict-safe schedule. They are not permission to start work. Within authorized execution, the persistent Sol/medium coordinator assigns ready tickets or coherent work packages to fresh orchestrators without waiting for unrelated work, provided dependencies and ownership exclusions remain satisfied. Each orchestrator advances only its assigned package through its completion boundary; it does not inherit the remaining project backlog.

| Wave | Tickets | Safe concurrency and prerequisite |
|---|---|---|
| W0 | PRE-01 | One prerequisite ticket: baseline, authority reconciliation and data preflight |
| W1 | ADM-01 ∥ ADM-02 | Two implementers: outcome/Audit contracts and interaction primitive have separate ownership |
| W2 | SEC-01 ∥ CAT-01 | Two: cross-cutting emergency retirement and isolated catalogue simplification |
| W3 | EVT-01 ∥ ACC-01 ∥ EVD-01 | Up to three after SEC-01; EVD-01 owns any migration in this wave |
| W4 | LIF-01 ∥ TEM-01 ∥ EVD-02 | Up to three, with explicitly separate event, roster and evidence ownership |
| W5 | SGN-01 ∥ DRF-01 | Two after LIF-01/TEM-01: signup-form changes and draft/roster publication; DRF-01 owns any publication-mode migration |
| W6 | SGN-02 ∥ BRD-01 | Two: admission/capacity and Board editing |
| W7 | PAR-01 ∥ RES-01 | Two: pre-draft participant administration and final-result publication, including the approved narrow exact-tie exception |
| W8 | ROS-01 ∥ BNR-01 | Two if banner preflight is satisfied; BNR-01 owns schema changes |
| W9 | WOM-01 ∥ ACT-01 | Two: WOM backend and derived Admin actions; shared-page exclusions specified below |
| W10 | BRD-02 ∥ WOM-02 | Two: Board freshness/performance and WOM workspace; BRD-02 owns schema changes |
| W11 | VER-01 | Integrated verification and complete mutation/Audit/feedback coverage |

### Serialization points

- **`SignupService` lane:** TEM-01 → SGN-01 → SGN-02 → PAR-01 → ROS-01.
- **Draft PageModel lane:** TEM-01 → DRF-01 → ROS-01.
- **Event domain lane:** SEC-01 → EVT-01 → LIF-01 → RES-01 → BNR-01.
- **Evidence service lane:** SEC-01 → EVD-01 → EVD-02.
- **Board lane:** EVT-01 creation integration → BRD-01 → BRD-02.
- **WOM lane:** LIF-01/ROS-01 → WOM-01 → WOM-02.

### Migration and shared-file ownership

Only one ticket at a time generates migrations or changes the EF model snapshot.

Expected migration owners are:

- EVD-01, if immediate switching requires a persisted ordering field.
- BNR-01 for physical banner removal.
- DRF-01 for minimal publication-mode metadata distinguishing direct roster finalization from a website draft, if existing metadata cannot express it reliably.
- WOM-01 for explicit connection provenance/credential adoption.
- BRD-02 for targeted freshness metadata.

Other tickets reuse current schema unless inspection identifies a concrete requirement. A newly necessary migration changes scheduling; it does not authorize simultaneous snapshot editing.

Localization and authority documents are also shared files. Parallel tickets own disjoint keys/sections; integrate them serially. No concurrent whole-file formatter or resource reordering.

### Integration checkpoints

Two intermediate gates provide real value:

1. **After W6:** confirm normal-account identity, team/draft eligibility, signup account structure, schedule predicates and Board publication agree.
2. **After W9:** confirm fixed rosters, final results, WOM synchronization and Admin actions consume the same current state.

The persistent coordinator owns these checkpoints: reconcile accumulated evidence, route any required integration checks or corrections through a bounded orchestrator assignment, and unlock affected downstream work only when the applicable gate passes. These are integration checks, not additional routine review layers.

## 5. Detailed implementation tickets

All tickets inherit the common Audit/feedback contract and historical-data rules. “No migration expected” does not authorize deleting old columns or rewriting old records.

### PRE-01 — Establish the implementation baseline and transition inventory

**Outcome:** Implementation has a clean approved branch, an exact production baseline and explicit treatment of retained data.

**Current problem / scope:** Planning has verified merged source but not production data presence. Current documents also retain superseded product rules.

**Target and implementation scope:**

- After plan review/approval, create `admin-simplification` from verified `main`.
- Record the release commit/image actually promoted and reconcile intervening changes.
- Perform narrowly scoped, read-only production preflight for banner assets/cleanup, emergency accounts, accountless participants, nonterminal Preformed events, Paused drafts, legacy Finalized events and pending future swaps.
- Record counts, state categories and compatibility requirements, not participant dumps or secrets.
- Promote approved product decisions into the existing authority documents before implementation.
- Confirm rollout treatment for any currently running event.

**History/migration:** No data transformation in this ticket. Unknown data presence blocks only the corresponding destructive migration or transition decision.

**Authorization/workers:** No production writes; no worker behavior changes.

**Audit/feedback:** Document the mutation-history standard and source of each data finding. Report unknown/unavailable evidence honestly.

**Verification:** Branch/tree identity, clean baseline and scoped documentation consistency.

**Non-goals:** Deployment, data repair, feature implementation, broad historical investigation.

**Scheduling:** `W0`; no parallel ticket. All later tickets depend on it.

---

### ADM-01 — Establish useful mutation outcomes and shared Audit presentation

**Outcome:** Touched workflows can return actionable outcomes, and Audit entries have one human-readable presentation across consumers.

**Current problem:** Many results expose only `Succeeded` plus a string. Some PageModels replace specific failures with generic messages. Audit primarily exposes action keys, IDs and raw structured data; `UiMessage` also guesses severity from text.

**Scope:** Existing result records, `IAuditWriter`/`AuditWriter`, `AuditEntry` readers, Audit page, Recent Admin Activity and `UiMessage`.

**Target:**

- Extend affected result types with stable outcome codes, field errors and relevant consequence data.
- Keep domain-specific result types; do not introduce a workflow/result framework.
- Provide a simple shared Audit presenter with explicit mappings for known actions and a safe legacy fallback.
- Render changed fields, actor, target and reason; put raw details behind Technical details.
- Make transaction ownership explicit: Audit helpers must not accidentally commit unrelated tracked changes.
- Allow owning tickets to add their action mappings without rebuilding the presentation layer.

**History/migration:** Read legacy JSON/action formats tolerantly. No rewrite of old Audit entries; no schema change expected.

**Authorization/workers:** Preserve quarantine filtering and private-data restrictions. Do not introduce an Audit processing job.

**Audit/feedback:** This ticket defines presentation and atomic-writing conventions. Unknown actions remain readable; malformed historic details cannot break the page. No credentials or code values enter new payloads.

**Tests/docs:** Audit atomicity, malformed/unknown legacy entries, EN/DA rendering, explicit severity and identical full/recent presentation. Update UI and technical authority.

**Non-goals:** Full mutation sweep now; domain-history duplication; new logging platform.

**Scheduling:** `W1-A`; depends PRE-01. Parallel with ADM-02. Must not overlap later Audit/result changes in the same files. Hotspots: Audit presenter, result records, resources.

---

### ADM-02 — Establish one Admin confirmation interaction

**Outcome:** All later tickets use one accessible centered confirmation system.

**Current problem:** Native confirms, inline boxes, `<details>` confirmations and page-specific dialog behavior coexist.

**Scope:** Admin layout, confirmation partial/component, existing dialog JavaScript, `admin-editor-guard`, shared toast integration and scoped Admin CSS.

**Target:**

- Consistent dimmed backdrop, focus entry/return, Escape, keyboard handling, responsive layout and Cancel-before-action order.
- Consequence-specific content, optional reason, and the exceptional WOM typed input.
- No ordinary-save confirmation.
- No stacked dialogs: an editor hands interaction to the shared confirmation and resumes with its state preserved.
- Preserve pending-request protection and safe failure recovery.
- Keep route/editor placement choices independent; this is not a mandate to make every editor a modal.
- Retain the native browser `beforeunload`/tab-close/reload/exit warning only while genuinely unsaved edits need protection at browser level. Product actions and in-application confirmations always use the shared custom system. Remove the exit warning after save/discard, avoid duplicate prompts, and do not introduce autosave to eliminate this exception.

**History/migration:** No persistence changes.

**Authorization/workers:** Confirmation is not authorization. Server checks remain mandatory; no worker changes.

**Audit:** Opening/cancelling a dialog is not a persisted mutation and creates no Audit entry.

**Feedback:** Validation and mutation feedback remain visible and announced while a modal is active; no duplicate toast, hidden failure or accidental dismissal after unsuccessful save.

**Tests/docs:** Actual interaction tests for focus, Escape, submit-once, cancellation, failed submission, narrow viewport and editor handback. Verify `beforeunload` is registered only for genuinely unsaved edits, cleared on save/discard, and is not used for product confirmations. Replace conflicting inline-confirmation policy in UI authority and explicitly record the approved browser-exit exception there during implementation.

**Non-goals:** Later visual redesign, frontend replacement, blanket conversion of editing surfaces.

**Scheduling:** `W1-B`; depends PRE-01. Parallel with ADM-01 using separate files. Must not overlap page migrations that edit these primitives. Hotspots: layout, shared JS/CSS, toast hosting.

---

### SEC-01 — Retire Emergency Captain authority end-to-end

**Outcome:** Emergency identities cannot authenticate, recover credentials, acquire team authority or mutate anything.

**Current problem:** Emergency behavior crosses roughly thirty source owners, including access checks outside visible account controls.

**Scope:** Emergency services, authentication/cookies, reset-token consumption, authorization handlers, Accounts/Create, evidence authority, Captain/submission navigation, Team Focus, Draft, lifecycle readiness/worker and DI.

**Target:**

- Remove creation/setup/reset/enable/disable operations and their routes.
- Reject existing emergency sessions and outstanding emergency credential tokens.
- Remove emergency evidence/focus/team authority.
- Remove emergency readiness and expiry-work processing.
- Normal Captain/Co-captain authority remains membership-based.
- Remove the Live-start dependency on either a Captain or emergency credential; retain Captain requirements for teams participating in an actual website draft at its start/finalization. DRF-01 owns the approved direct-roster path for fewer than two included teams and must not apply website-draft-only blockers to that path.

**History/migration:** Keep historical actor/account/access references necessary for Audit and evidence. Do not convert emergency accounts into ordinary users. No blanket historical deletion.

**Audit/feedback:** Preserve past emergency history. If rollout records retirement, use one explicit system action rather than fabricated human disables. Denied routes provide the normal safe unavailable/unauthorized outcome.

**Tests/docs:** Existing-cookie rejection, outstanding-token rejection, direct service calls, foreign-team access, normal Captain/Co-captain regression, hidden-event boundaries and Team Focus. Update access/product/contracts/data documentation.

**Non-goals:** Normal account management redesign; changing participant ownership.

**Scheduling:** `W2-A`; depends ADM-01/02. Parallel with CAT-01. Must not overlap ACC-01, EVD-01/02, TEM-01, LIF-01 or ACT-01. Hotspots: authorization, shell, Draft, lifecycle, Program.

---

### CAT-01 — Simplify catalogue editing while preserving calculation mechanics

**Outcome:** Admins edit understandable activity/drop data without managing internal roll-group or mapping machinery.

**Current problem:** Catalogue handlers expose roll groups and low-level mapping inputs; permanent deletion requires typed `DELETE`.

**Scope:** Catalogue Razor/PageModel/API partial, catalogue JavaScript, `BossActivity`, `SourceDrop`, shared items and deletion dependency checks. Minimal transaction/lock/recheck integration in directly participating Board/template reference writers is included where required to enforce deletion integrity; this does not advance broader BRD work.

**Target:**

- Normal activity fields: name, category, efficient rate and image.
- Normal drop fields: item, rate and optional image.
- Preserve existing roll mechanics; ordinary saves must not erase hidden values.
- New entries use established domain/operator defaults; complex mechanics remain operator-owned.
- Keep automatic mapping, manual value and untradeable zero; manual mapping is recovery-only.
- Super Admin permanent deletion only for unused records, with dependency impact known before confirmation and rechecked during mutation. Deletion and concurrent creation of catalogue references must share a transaction-safe coordination boundary. A reference committed first prevents deletion; a deletion committed first prevents a new dangling reference. Preserve referenced/historical snapshots. No bounded race is accepted as completion of this requirement.
- Confirm no active bulk-import UI; retain operator commands.
- No Recalculate All Boards action.

**History/migration:** Referenced/history records deactivate. Preserve snapshot identities and pricing history. No schema migration expected.

**Audit/feedback:** Record semantic catalogue changes and before/after values. Distinguish duplicate shared item, invalid rate, mapping failure, provider cooldown and protected deletion; retain valid edits after provider failure.

**Tests/docs:** Existing rate-mechanics/calculation tests, preservation of hidden mechanics, dependency races, mapping fallback, price-source behavior and Super Admin boundary. Update catalogue/UI contracts. For the 26 September CAT-01 concurrency clarification, demonstrate the regression and corrected behavior with controlled PostgreSQL concurrency checks for both delete-first and reference-first orderings, covering participating Board/template writer paths and relevant catalogue entity types. Keep transaction snapshot visibility and lock ordering correct; source inspection or in-memory tests alone are insufficient. Reuse the assigned W2 reviewer for the stable complete diff.

**26 September application clarification:** the initially rejected CAT patch remains superseded and must not be replayed. The materially corrected evidence-only proposal `/private/tmp/cat01-reconciled-proposal/CAT01_RECONCILED_PROPOSAL.patch` (SHA-256 `419c8130f2d95ee6886c5c2240b70e54b3b2d96a3a104781b00d89a2b18b802a`) is within approved scope for normal tool approval/application: preserve stored/operator metadata and history; reject unsupported submitted operator fields explicitly without saving; replace typed DELETE only with the complete shared identity/impact confirmation and retained server authorization, antiforgery, version and locked dependency checks. This reconciles scope and supplies a safer alternative; it does not waive automatic approval, establish a code-review pass or claim unrun C#/Razor/app checks. Stop on any new rejection. Merge against the current integration files without overwriting other accepted work, then complete focused checks and the assigned independent W2 review.

**Subsequent execution block:** automatic approval also rejected the corrected proposal application, describing the planner approval as untrusted and the catalogue confirmation/persistence changes as unauthorized side effects. The corrected proposal remains unapplied; do not retry, split or reroute it through another tool/context. Earlier preservation and separately authorized concurrency work remain partial implementation, not CAT-01 completion. Direct user approval of the concrete action is required to resolve this block. SEC-01 and independently eligible downstream work may continue after their own required checks/review, without claiming combined W2 or CAT-01 acceptance.

**Non-goals:** EHB formula changes, catalogue reseeding, removing operator tooling.

**Scheduling:** `W2-B`; depends ADM-01/02. Parallel with SEC-01. Must not overlap BRD-02 or another catalogue writer. Hotspots: Catalogue PageModel, source-drop fields, resources. CAT-01 temporarily owns the narrowly identified catalogue-reference writer changes in Board/template code; the W2 orchestrator must serialize any shared-file overlap with SEC-01 or accepted W1 changes. Record the participating paths and passed evidence for later BRD owners. No new ticket, wave or migration is introduced.

---

### EVT-01 — Minimal event creation and corrected Identity ownership

**Outcome:** Creating an event asks only for name/timezone and atomically creates a private Draft, stable unique slug, signup structure and empty 5×5 board.

**Current problem:** Creation bundles schedule, questions, banner, planning and WOM; Identity permits some slug editing and blocks all Live edits.

**Scope:** Create/Identity routes, `BingoEvent`, slug generator, initial form/board construction and route capability checks.

**Target:**

- Default timezone Europe/Copenhagen.
- Slug generation is server-owned, collision-safe and permanent.
- Remove creation wizard inputs and WOM date import.
- Identity owns name, optional description, buy-in and timezone.
- Name/description/buy-in remain editable through Live/Final Review.
- Timezone changes only before first Live, preserve UTC instants and require one public-time preview when exposed.
- Remove future product use of `PublicRules`, `PrizeDescription`, `ExpectedTeamCount`, `ExpectedBoardRows` and `ExpectedBoardColumns`. Keep `BuyInDescription`; retain `ExpectedTeamSize` only as the temporary Board planning input owned by BRD-01. This clarification requires no additional ticket or migration.
- Remove banner editing controls.
- Create required Playing/Captain structure without requiring optional configuration.

**History/migration:** Preserve existing slugs and boards. Do not replace populated boards or bulk manufacture boards for archived events. Physical banner removal belongs to BNR-01.

**Audit/feedback:** One coherent event-created entry; semantic identity changes with old/new values and timezone preview. Handle duplicate names/slugs, stale identity edits, invalid timezone and atomic creation failure.

**Tests/docs:** Creation rollback, concurrent slug allocation, default board, stable rename URL, Live identity versus timezone restrictions, optional fields and public-link regression.

**26 September execution block:** automatic approval rejected the Create handler replacement, describing it as an unauthorized wholesale change in another worktree. The replacement remains unapplied and must not be replayed or split to bypass that rejection. Partial EVT domain changes are not accepted: permanent-slug enforcement conflicts with the still-existing creation collision path, and the shared identity guard also governs banner mutation. The same owner completed that recovery: only unaccepted EVT hunks were unwound, preserving the accepted SEC diff; reported domain recovery checks passed 2/2 and diff checks passed. Creation/Identity PostgreSQL checks did not execute because of an EVD-owned compilation diagnostic. The complete unapplied design/acceptance proposal is `/private/tmp/evt01-recovery-evidence/EVT01_UNAPPLIED_PROPOSAL.md` (SHA-256 `f59559fef35cf9068c01499b45f6d07037e06ffa14202b0fa2b294a427b79dde`). It is scope-consistent but is not an implemented patch or independent-review pass; direct user application authorization remains pending. Future implementation turns use Luna/max. ACC/EVD may continue independently on a coherent baseline; EVT-dependent tickets remain blocked. This status does not alter the approved minimal-creation product outcome.

**Non-goals:** Schedule redesign in this ticket; new mandatory WOM connection.

**Scheduling:** `W3-A`; depends SEC-01. Parallel with ACC-01/EVD-01. Must not overlap LIF-01, BNR-01, BRD-01 or other `BingoEvent` writers. No migration expected.

---

### ACC-01 — Simplify normal account support and security actions

**Outcome:** Accounts is a support/security surface for existing normal accounts.

**Current problem:** Emergency controls share account views; some specific failures collapse into generic messages.

**Scope:** Account directory/detail/transfer pages, administration/identity services and cookie authorization versions.

**Target:**

- Preserve current directory/search/role filter; add no redundant Active/Disabled filter.
- Show Discord state, login information, linked characters, participation/team roles and disable history.
- No Admin editing of username, Discord or My Accounts; no Create User, delete or merge.
- Super Admin alone grants/revokes Admin.
- Enforce disable/restore hierarchy, self/Super Admin protections and immediate session invalidation.
- Preserve 60-minute, single-use, superseding reset links and operator/Discord Super Admin recovery.
- Ownership transfer uses current password, one confirmation and atomic role exchange.
- No typed names or ordinary reset-link confirmation.

**History/migration:** Preserve accounts and all linked records. No schema migration expected.

**Audit/feedback:** Record role, disable/restore, reset issuance and transfer without secret material. Explain wrong password, stale role, insufficient permission, disabled recipient and protected target.

**Tests/docs:** Role matrix, session invalidation, concurrent ownership transfer, reset supersession and no restoration of expired event authority. Update account/security/UI contracts.

**Non-goals:** Identity-provider redesign or account recovery policy replacement.

**Scheduling:** `W3-B`; depends SEC-01. Parallel with EVT-01/EVD-01. Must not overlap SEC-01 or account-security writers. Hotspots: account services, cookie checks, account dialog adapter.

---

### EVD-01 — Correct submission authority and make account switching immediate

**Outcome:** Submission authority follows participant/team roles, and only a participant changes their active Playing account.

**Current problem:** Service-level Administrator bypass remains; switching retains next-minute scheduling and an accountless-team exception.

**Scope:** `EvidenceAuthority`, `ParticipantLiveService`, active-character queries/history, submission snapshot creation, relevant participant/Captain controls.

**Target:**

- Participant: self only.
- Captain/Co-captain: current own-team members, selector defaulting to self.
- Nonparticipant Admin: no submission authority.
- Admin participant/leader: exactly the normal participant/leader rules.
- Keep Admin review/private-evidence access separate from submission authority.
- Immediate server-time switching; no new pending future-swap state.
- Serialize switching and attribution capture sufficiently to produce one unambiguous result.
- Store immutable participant, Playing account and submission timestamp.

**History/migration:** Preserve old transitions and evidence snapshots. Drain or explicitly handle existing pending switches during rollout. Current queries break equal timestamps using IDs; tests must cover PostgreSQL precision. If necessary, add a minimal per-participant sequence with a backfill preserving existing read order.

**Audit/feedback:** Switch history is authoritative; no duplicate global entry for every participant switch. Show chosen account/effective time or precise stale/eligibility failure.

**Tests/docs:** Direct-service authorization matrix, role changes, disabled users, simultaneous switch/submission, rapid switches and all-Playing WOM aggregation unchanged.

**Non-goals:** WOM activity formula changes; team-wide account management.

**Scheduling:** `W3-C`; depends SEC-01. Parallel with EVT-01/ACC-01. Must not overlap EVD-02. Owns any W3 migration.

---

### LIF-01 — Simplify schedule rules and operational lifecycle actions

**Outcome:** Schedule has one owner, automatic actions follow timestamps, and lifecycle controls use the requested confirmation/reason rules.

**Current problem:** Schedule combines capacity/toggles, locks dates based on draft state, and duplicates editing on Manage. The manual signup flow retains Prepare confirmation → warnings acknowledgement → proposed-close acceptance → final confirmation. Manual early start requires a reason. Early ending currently leaves the normal cutoff based on the configured end.

**Scope:** Schedule/Manage, `EventScheduleValues`, signup/lifecycle services, `BingoEvent`, readiness, worker queries and route capability checks.

**Target:**

- Schedule owns only the five timestamps.
- Pre-public schedule changes use ordinary Save. Once participants rely on the schedule, Save uses one shared modal showing meaningful old → new changes and their consequences. Draft time is exempt from this consequence confirmation; a Draft-time-only edit remains ordinary Save. Mixed edits show only the changes requiring confirmation. Remove the checkbox, preview-page and “Confirm and save schedule” ladder.
- Manual Open signups uses one shared confirmation showing the actual consequence and close time. Manual Close/Reopen uses the same shared interaction principles.
- Retire prepare-confirmation state as a product workflow, separate warning-acknowledgement steps and separate proposed-close acceptance. Show readiness blockers/warnings before the action where possible; relevant warnings may appear in the single confirmation without separate acknowledgement. Server readiness remains authoritative and is rechecked at mutation time.
- Future opening timestamp schedules opening; manual opening supersedes it. More generally, a manual lifecycle action supersedes its corresponding scheduled action: the scheduler must not later undo or repeat it. Preserve unrelated future transitions and any explicitly established new schedule.
- Actual opening/closing remain history; reopening records a new future close without rewriting prior transitions.
- Draft time freezes when draft starts.
- Event start remains editable until actual Live, including a Running/Finalized draft.
- Live end change requires a reason and the single consequence modal described above, without a separate preview step.
- Automatic start attempts surface blockers; automatic end enters Final Review.
- Manual start needs confirmation, no reason.
- Early end uses actual end plus 30 minutes for upload grace.
- Resume records a prospective new Live interval and preserves the ended gap.
- Keep evidence codes and exceptional submission reopening.
- Preserve delete-versus-cancel retention rules. Show **Delete event** when disposable; show **Cancel event** when protected history requires retention. Never make the Admin choose between technical “Discard” and “Cancel” concepts.
- Preserve Super Admin quarantine semantics; remove typed confirmations and mandatory restore reason. Hide still requires its reason; Restore accepts no reason. Both retain the ordinary shared action confirmation and immutable audit, eligibility, authorization, concurrency and hidden-event access protections. Legacy event-name parameters, if retained for compatibility, must not gate either operation. Older detailed contracts/tests requiring exact-name matching or a Restore reason are superseded by this approved simplification.
- Establish consistent “ever Live” predicates for later roster/WOM work.
- DRF-01 will adapt the existing finalized-draft readiness check to accept either valid roster-publication method, without weakening the remaining event-start requirements.

**History/migration:** Retain original UTC times and transitions. Explicitly reconcile scheduled-opening flags on existing unfinished events so removing the toggle does not accidentally activate previously disabled schedules.

**Audit/feedback:** Schedule before/after, actual transitions, required reasons and destructive impact. Surface blockers and failed scheduled attempts; never silently import WOM dates.

**Tests/docs:** DST, past dates, reopen history, worker idempotence, start/end races, early-end grace, resume gaps, quarantine direct routes and managed-date sync handoff. Verify single-confirmation Open/Close/Reopen, authoritative readiness changes after confirmation, retirement of the acknowledgement/proposed-close/prepare ladder, and manual-versus-scheduler races without duplicate or reversed transitions. Cover ordinary pre-public Save, participant-facing old → new confirmation, Draft-time-only and mixed edits, no extra confirmation ladder, and Delete/Cancel labels with matching server retention rules.

**Non-goals:** Removing unrelated current-event exclusivity or overlap rules; redesigning dashboard.

**Scheduling:** `W4-A`; depends EVT-01. Parallel with TEM-01/EVD-02. Must not overlap SGN-02, RES-01 or BNR-01. Owns event-domain and Schedule contracts.

---

### TEM-01 — Replace legacy team/participant creation concepts

**Outcome:** Teams are ordinary teams with independent draft inclusion; new participants always reference existing website accounts.

**Current problem:** Preformed type controls eligibility and mutation paths. Draft exposes external-player/CSV creation, and signup supports accountless creation and ownership transfer.

**Scope:** `Team`, Draft setup/roster handlers, `SignupService` creation/ownership paths, participant add forms, CSV service/DI and relevant projections.

**Target:**

- `IncludedInDraft` alone determines participation in website drafting.
- Affiliation is descriptive.
- Seated members are excluded from the unassigned draft pool.
- Remove CSV, accountless/external creation and participant-owner transfer.
- Any retained Admin addition selects an existing active website account and preserves the event’s unique identity.
- Zero teams is a valid setup screen with Add team.
- Do not silently create a team.
- Preserve normal role assignment through the existing captain-authority service.

**History/migration:** Keep old null-owned participants and formation values readable. Do not assign ownership by matching names. Remove behavioral reliance on `FormationType`; retaining a harmless historical column is acceptable.

**Audit/feedback:** Team creation/configuration, membership setup and draft-inclusion changes with affected participants. Explain duplicate membership, unavailable account and inclusion changes blocked by draft state.

**Tests/docs:** Included/excluded teams regardless of affiliation, seated participants, no reachable legacy creation/transfer/CSV handlers and historical roster rendering.

**Non-goals:** Finalized-roster correction or new draft algorithm.

**Scheduling:** `W4-B`; depends EVT-01/SEC-01. Parallel with LIF-01/EVD-02. Must not overlap SGN-01/02, PAR-01, DRF-01 or ROS-01. Hotspots: DraftModel, SignupService, Team.

---

### EVD-02 — Simplify evidence attempts and Admin review

**Outcome:** Rejected/Reversed evidence remains history; subsequent attempts are ordinary submissions. Review has simple, explicit decisions.

**Current problem:** Special Resubmit commands/relationships remain active; Review uses browser confirmations, including for approval.

**Scope:** Submission service/interface, participant submission detail/legacy Captain routes, Review pages, review actions and notifications.

**Target:**

- Remove future special resubmission creation and one-child workflow.
- Pending owners can edit permitted structured data, replace screenshot or withdraw while the window permits.
- Approve: one click.
- Reject: reason and shared modal.
- Reverse: reason and one confirmation, recalculating affected progress/rankings.
- Pending Admin metadata correction: reason, allowed attribution/objective fields, no timestamp fabrication or screenshot replacement.
- Verify Request Changes remains inactive.
- Preserve evidence codes and immutable original submission time.

**History/migration:** Retain old predecessor links and review enum values for display. No relationship deletion required.

**Audit/feedback:** Review-action history remains authoritative for evidence decisions and corrections; expose it consistently from Audit without duplicating conflicting histories. Distinguish decision saved from later refresh/notification failure.

**Tests/docs:** Repeated ordinary attempts, old linked history, Pending-only edits, cutoff races, double approval/reversal, contribution recalculation and public NEW/Recent Activity regression.

**Non-goals:** Public submission workspace redesign or new review states.

**Scheduling:** `W4-C`; depends EVD-01. Parallel with LIF-01/TEM-01. Must not overlap RES-01 or another SubmissionService writer.

---

### SGN-01 — Simplify signup questions and built-in account fields

**Outcome:** Form configuration remains editable before draft, with clear built-in Playing/Alt/Captain structure.

**Current problem:** Questions require closed signup, structural edits use replacement workflows, and account fields are treated as ordinary questions.

**Scope:** Questions/Participants configuration, signup form/question entities, deletion service, character assignments and participant form projection.

**Target:**

- Add/edit/delete/reorder before draft while signup is open or closed.
- After first signup: new questions optional; optional cannot become required.
- Format replacement becomes delete-old plus create-new.
- Deletion removes answers after one impact confirmation.
- First Playing field required/protected; additional Playing fields optional.
- Zero configured Alt fields is valid. Every configured Alt field is optional; none becomes a signup/readiness requirement. The first Playing field remains separately required/protected.
- Removing optional account fields releases only their event registrations.
- Required Captain Yes/No; optional co-captain text shown for volunteers when enabled.
- Disabling co-captain text with answers confirms their removal.
- Preserve public disclosure and private-field separation.

**History/migration:** Reuse existing system-field/account-role structure where possible. Keep deleted-question metadata needed by history; delete the specified answers. Never modify My Accounts. Distinguish explicit deletion from legacy structural replacement: previously participant-facing answers retained under inactive replacement questions remain visible under the existing public disclosure contract. Inactivity alone does not mean deletion or make retained public answers private. Explicitly deleted questions/answers are excluded from ordinary projections; private fields remain private. The new delete-old/create-new workflow does not retroactively delete or hide retained replacement history. This preserves PRODUCT_REQUIREMENTS.md's signup-history contract and DATA_MODEL.md's public historical-answer projection; no public-table redesign is authorized.

**Audit/feedback:** Question definition/order changes, answer-removal counts, account releases and co-captain configuration. Show exact destructive impact and stale impact changes.

**Tests/docs:** Open-signup edits, first-response boundary, zero Alt fields, all Alt fields left blank, required/protected first Playing field, exclusive Playing/Alt registration, deletion rollback, released-account reuse and public privacy.

**Non-goals:** Public signup-table redesign; admission/capacity changes.

**Scheduling:** `W5-A`; depends TEM-01/LIF-01. Parallel with DRF-01. Must not overlap SGN-02, PAR-01 or ROS-01. Hotspots: Questions, SignupService and signup entities.

---

### DRF-01 — Simplify draft execution and publication

**Outcome:** Two or more included teams use the website draft with Setup, Running and Finalized behavior, derived sizes and immutable pick history. Zero or one included team uses direct Finalize roster, with durable distinction from an actual website draft.

**Current problem:** Pause/Resume/Reopen and Preformed assumptions complicate execution despite existing derived-size logic. The distribution validator requires at least two drafted teams, finalization requires a Running/Paused session, and Cancel currently bulk-undoes active picks. Those behaviors conflict with direct roster finalization and the approved zero-active-pick recovery rule.

**Scope:** Draft PageModel/domain, distribution/order logic, controller lease, publication cycles/method, draft notifications and directly affected roster-finalization checks in event readiness, Board publication, WOM eligibility and pre-Live roster correction. Use existing services/publication records; no generic workflow or draft-run engine.

**Target:**

- Reuse balanced-size derivation for two or more included teams; remove remaining manual target-size concepts.
- Teams participating in an actual website draft need an active normal-account Captain at its start/finalization.
- Already seated members count toward final sizes and cannot be drafted again.
- Keep single-controller protection, spectator views and confirmed takeover.
- Randomize order only until first pick.
- Undo latest active pick without confirmation.
- Count active teams with `IncludedInDraft = true` at mutation time: 0 or 1 uses direct Finalize roster; 2+ uses the normal website draft. Manual assembly is required before the direct path; never auto-seat participants or fabricate picks/Running history.
- Both publication methods validate current event eligibility, participant/account exclusivity, required Playing assignments, valid team membership/roles and the roster state consumed by Board/WOM. Direct finalization does not apply minimum-two, draft-turn or balanced-draft-size requirements. Do not apply a website-draft-only Captain blocker to it.
- Persist publication method on the existing roster-publication structure, or reuse equivalent reliable metadata. Consumers check finalized roster readiness and retain the ability to distinguish direct assembly from a completed website draft. Adapting only the button while leaving `DraftState.Finalized`/Running assumptions elsewhere is insufficient.
- Finalization publishes roster and freezes original picks where a website draft occurred. Direct finalization creates no synthetic pick history.
- Remove Pause/Resume and finalized Reopen.
- Keep Cancel draft for an unpublished Running session only when zero active picks remain. If any exist, show the count and instruct Undo last pick until zero; the handler rechecks this under the controller/concurrency boundary. Cancellation must not bulk-undo picks or automatically end picked memberships.
- Preserve existing explicit undo records, return to Setup, and release/reset the existing controller state as appropriate. Do not add an abandoned-draft-run concept or cancelled-pick history. A finalized draft/roster cannot reopen through this action.

**History/migration:** Preserve picks, undone picks and publication history. Inspect existing Paused sessions; do not silently resume or fabricate finalization. DRF-01 owns any minimal publication-method migration in W5. Classify old publications only from reliable existing evidence, retaining an explicit historical unknown where necessary; absence of active picks alone does not prove no draft occurred. SGN-01 must not generate a concurrent model-snapshot change.

**Audit/feedback:** Start, order randomization, picks/undo, takeover, draft finalization, direct roster finalization and successful zero-pick cancellation. Record publication method and a truthful cancelled-to-Setup outcome; never claim cancellation itself undid picks. Controller renewal is operational, not a new Audit entry each heartbeat. Explain captain, roster balance, stale turn/controller conflicts and why cancellation is blocked by N active picks. Direct finalization reports roster blockers rather than asking the Admin to start a fake draft.

**Tests/docs:** Snake order, pre-seated balance, undo restrictions, randomized-order boundary, concurrent controllers, disabled Captain and publication immutability. Add 0/1/2+ included-team cases; valid/invalid direct rosters; direct-finalization races with team inclusion/roster changes; Board publication, WOM creation and event-start eligibility for both publication methods; preserved no-draft versus drafted history. Verify Cancel rejects active picks without changing them, succeeds only after explicit last-pick undos reach zero, rechecks a concurrent new pick, rejects finalized sessions and creates no new cancelled-pick/run history.

**Non-goals:** Post-finalization Add/Remove implementation; draft UI redesign.

**Scheduling:** `W5-B`; depends TEM-01/LIF-01 because direct publication must adapt the lifecycle/readiness owner after its schedule changes. Parallel with SGN-01 under disjoint file ownership. Sole W5 migration owner if publication-mode persistence is needed. Must not overlap ROS-01, BRD-01, WOM-01 or other Draft/readiness/publication writers.

---

### SGN-02 — Correct admission codes, capacity and waiting-list rules

**Outcome:** Capacity and admission have one predictable pre-draft model.

**Current problem:** Waiting-list disablement can increase capacity/promote everyone; capacity cannot normally decrease after publication; code checks are misaligned between edit and rejoin.

**Scope:** Signup administration/admission, participant settings, rejoin request contract, `BingoEvent` capacity/signup helpers and remaining Schedule/Manage compatibility calls.

**Target:**

- Participants/Signups owns capacity and admission code.
- Capacity can change before draft but never below Confirmed count.
- Increasing capacity promotes earliest eligible waiters transactionally.
- Waiting is always enabled while signup is open.
- Code required for first signup and self-rejoin; not edit, withdrawal or Admin addition.
- Changed code affects future admissions only; disabling it is an ordinary save.
- One authoritative code configuration, eliminating duplicated mutable ownership where present.
- Show promotions before Save, and recheck actual counts at mutation time.

**History/migration:** Preserve sequence/status history. Existing disabled-waiting settings must not trigger the old promote-all behavior during transition.

**Audit/feedback:** Capacity before/after and promotions; code enabled/changed/disabled without its value/hash. Clear full/queued status, incorrect-code field error and capacity-race feedback.

**Tests/docs:** Concurrent signups/capacity changes, queue order, code rotation, edit without code, rejoin with current code, Admin bypass and no automatic cap expansion.

**Non-goals:** Post-finalization capacity enforcement; payment changes.

**Scheduling:** `W6-A`; depends SGN-01/LIF-01. Parallel with BRD-01. Must not overlap PAR-01, ROS-01 or another event/signup writer.

---

### BRD-01 — Simplify Board lifecycle and protected tile editing

**Outcome:** Board always opens its editor and exposes straightforward approval/publication/correction behavior.

**Current problem:** Explicit Create/Unapprove/release workflows and multiple confirmation types remain; editing and protection must stay aligned.

**Scope:** Board PageModel/domain, tile requirement editor, approval snapshots, board JavaScript and image handling.

**Target:**

- New-event board comes from EVT-01; no Create Board screen.
- Safely create a missing board only for an eligible unfinished legacy event, without modifying historical boards.
- Keep lease protection; present editor identity/takeover, remove explicit Finish Editing.
- Approve performs authoritative validation and freezes the private snapshot without confirmation.
- Competitive edit of approved/unpublished Board returns it to Draft; prior snapshot remains.
- Publish after a valid finalized roster, from either website draft or direct Finalize roster, with one confirmation. Consume DRF-01 publication readiness without requiring invented draft picks.
- Published correction retains current public snapshot until replacement publication; begin requires reason, publish/discard confirmation.
- Safe resize preserves positions; occupied out-of-bounds cells block shrinking.
- Tile removal is first-class and dependency-checked before confirmation.
- Preserve existing derived descriptions, automatic catalogue EHB, manual total challenge EHB, homogeneous objectives and evidence protection.
- Use plain-language counting controls: **The same drop can count more than once**, **Some drops count as more than one**, and individual selection **Counts as 2** (with the selected count). Replace “weights”, `CreditedWeight` and “Configure individual drop weights” in user-facing interaction text; preserve the underlying counting/calculation mechanics.
- Keep `ExpectedTeamSize` only as the temporary Board planning input; derive planning team size from actual rosters after finalization. The retired fields named in EVT-01 must not reappear in the Board workflow.

**History/migration:** Preserve every referenced approval/objective/artwork identity. No snapshot rewrite; no schema migration expected.

**Audit/feedback:** Board/tile semantic changes, approval invalidation, publication versions and correction reasons. Explain invalid catalogue data and exact evidence/resize blockers.

**Tests/docs:** Approval/publication integrity, evidence-bound wording edits, automatic invalidation, shrink boundaries, stale lease/version, descriptions and derived roster sizing. Verify the plain-language counting labels map to the existing duplicate-counting and per-drop count behavior without changing calculations.

**Non-goals:** Overview performance overhaul, catalogue formula changes, removing TileTemplate for purity.

**Scheduling:** `W6-B`; depends EVT-01/DRF-01. Parallel with SGN-02. Must not overlap BRD-02 or other Board writers.

---

### PAR-01 — Simplify pre-draft participant correction and withdrawal

**Outcome:** Pre-draft administration is Edit → Save, with explicit withdrawal and predictable restoration.

**Current problem:** Legacy identity/roster paths and lifecycle restrictions complicate correction, withdrawal and reactivation.

**Scope:** Participant pages, `SignupService`, character registration service, membership termination and private payment/notes operations.

**Target:**

- Ordinary answer/account/EHB corrections need no confirmation or reason and preserve queue/status.
- Withdrawal requires one confirmation, ends membership, releases registrations and promotes the earliest eligible waiter.
- Preserve historical answers, payments and notes.
- Self-withdraw while open or closed before draft; self-rejoin only while open using SGN-02 admission rules.
- Admin restore uses current capacity and end-of-queue semantics.
- All roster/participant edits and withdrawal lock during Running draft.
- Payment/notes remain ordinary private administration wherever administratively available.

**History/migration:** Reuse existing participant identity; no cloning. Preserve released assignment history.

**Audit/feedback:** Corrections, withdrawal, restore, promotion, payment and notes changes. Protect private content; explicitly report account conflicts, queue destination and concurrent draft start.

**Tests/docs:** Atomic membership/registration release, waiter promotion, existing-account conflicts, stale response versions, draft-start races and private/public projection separation.

**Non-goals:** Finalized-roster operations; account ownership transfer.

**Scheduling:** `W7-A`; depends SGN-02/DRF-01. Parallel with RES-01. Must not overlap ROS-01 or SignupService writers.

---

### RES-01 — Publish official results directly to Archived

**Outcome:** Official publication has non-overridable integrity gates and atomically produces an Archived event. Official ranks differ whenever competitive inputs differ; a justified shared rank is allowed only under the approved exact-tie edge-case exception.

**Current problem:** Final Review allows overrides, completion inspections/corrections, tie confirmation and a separate Archive action.

**Scope:** Finalization service/interface, Final Review page, event state/capabilities, snapshots, current-event predicates, authoritative ranking/completion inputs and public result projections. Reuse the merged competitive ranking hierarchy and its exact-equality semantics; no new final-tiebreak subsystem.

**Target:**

- Require closed submission window, zero Pending and valid calculated placements. Rankings must apply every approved competitive input; the narrow exact-equality exception is a valid result, not an override of an integrity blocker.
- Remove override, completion inspection/correction and tie-confirmation commands.
- Preserve the intended hierarchy of completion, evidence-derived completion/current-score timing, lines/tiles and EHB. Distinct competitive values must produce the corresponding competitive ordering; no Admin may declare a tie across a measurable difference.
- If every intended competitive input is exactly equal, accept a shared official rank under the user's subsequent edge-case clarification. Do not require generic tie acknowledgement, a new competition, invented timestamps or arbitrary name/ID/GUID ordering to force uniqueness.
- Keep team-name ordering solely as stable presentation inside an equal-rank group. Do not change the existing shared-rank numbering convention or recalculate old official versions as part of this exception.
- Preserve public provisional ranking and display the authoritative official snapshot after publication. Existing historical official versions, including shared ranks, remain immutable.
- Publish through one confirmation into immutable official snapshot plus Archived state, in one transaction.
- Remove ordinary Archive handler.
- Exceptional reopen: reason/confirmation, new review cycle, corrected publication into a new immutable version.
- Preserve existing single-current-event restrictions unless separately changed; explain any blocked reopen.

**History/migration:** Read legacy Finalized records and old corrections/resolutions. Never recalculate already-published historical results merely because the new rules exist. Define a controlled transition for any unfinished legacy Finalized event.

**Audit/feedback:** Publication version and placement summary, reopen reason and state changes. When exact ties occur, explain the shared rank and the equal competitive inputs in the calculated result; persist the relevant ranking facts in the official version using existing snapshot data wherever sufficient. There is no Admin “approve tie” or “mark resolved anyway” action. Show actual remaining blockers, stale review cycle and concurrent review/submission changes.

**Tests/docs:** Publish-versus-submit/review races, zero-progress exact ties, equal recorded completion/current-score timestamps plus equal EHB, and ordering independence from team names/IDs/input order. Verify that earlier competitive time or a higher applicable EHB breaks equality at its intended priority, while exact equality alone may publish with shared ranks and no acknowledgement. Retain current equal-input calculator tests; remove tests requiring generic tie confirmation. Preserve reversal effects, immutable historical snapshots, repeated publication, reopen/republish and homepage/history routing.

**Non-goals:** New ranking formula, arbitrary uniqueness fallback, manual timestamp backfill, discretionary tie override or loosening current-event exclusivity.

**Scheduling:** `W7-B`; depends LIF-01/EVD-02/BRD-01. Parallel with PAR-01. The exact-tie exception is approved and adds no new decision or schema dependency. Must not overlap BNR-01 or event/finalization/calculator writers.

---

### ROS-01 — Direct finalized-roster corrections; retire Live replacements

**Outcome:** Finalized/pre-Live rosters use independent Add and Remove operations; membership is fixed after first Live.

**Current problem:** Current corrections reuse withdrawal/vacancy/replacement machinery and treat team formation types differently.

**Scope:** Signup roster operations, Draft/Participant handlers, membership/publication projections, WOM update queuing and legacy validation request fields.

**Target:**

- Remove a participant through one confirmation.
- Add from team context by selecting an existing website account.
- Reuse withdrawn/waiting identity; otherwise create normal event participation from configured Playing fields.
- First Playing account required; optional extras, questionnaire may remain blank.
- No signup code, signup-cap restriction, final team-size cap or automatic rebalance.
- Add and Remove are separate commits; failed Add does not restore a removed person.
- Publish current roster changes after either DRF-01 publication method while preserving its no-draft/drafted provenance, original picks where they exist and prior publication history.
- Queue managed WOM synchronization and refresh derived Board team-size values.
- Remove future Live departure/replacement/vacancy and promotion-follow-up creation.
- Enforce ever-Live locking in services and direct handlers, including an early-ended event.

**History/migration:** Keep old membership replacement links and historical departures readable. No new vacancy records or forced historical roster normalization.

**Audit/feedback:** Before/after roster, reused participant identity, account assignments and sync status. Distinguish local Add/Remove success from pending/failed WOM sync; clearly state a remaining short team.

**Tests/docs:** Account reuse, unlimited post-finalization additions, duplicate registrations, Live-start races, failed Add after Remove, immutable picks, current public rosters and WOM payloads.

**Non-goals:** Moving players during Live or rewriting draft picks.

**Scheduling:** `W8-A`; depends PAR-01/DRF-01/BRD-01/LIF-01. Parallel with BNR-01 if no schema change is needed. Must not overlap SignupService/Draft/WOM roster writers.

---

### BNR-01 — Remove event banners under the approved disposal policy

**Outcome:** Unused banner functionality and its infrastructure are removed end-to-end.

**Current problem:** Banner fields are unwanted. Actual production asset/cleanup state remains unknown, but the user has resolved retention: on 27 September 2026 they stated that any remaining banner data is unwanted and may be discarded. This does not establish that storage is empty.

**Scope:** Banner asset route/entities, event FK/configuration, cleanup service/outbox, destructive lifecycle cleanup, rendering, DI and tests.

**Target:**

- Treat all event-banner data as disposable under the user's explicit retention decision. Proof that production banner storage is empty and a further asset-retention decision are no longer prerequisites for implementation. No production access is required merely to resolve those questions.
- Remove banner persistence, routes, rendering and cleanup machinery using existing schema/storage ownership evidence and controlled empty/populated/pending-cleanup fixtures.
- Define the ordered banner-only cleanup and schema-removal procedure before removing the mechanism that makes cleanup reliable. Preserve required object references until cleanup can consume them. Prepare and test this procedure on controlled fixtures; executing it against production remains a separately authorized deployment/data action.
- Never delete shared evidence/team/tile storage infrastructure.
- Future editing remains retired. Do not restore banner UI or retain banner data for product reasons; preserve unrelated evidence/team/tile data and shared storage infrastructure.

**History/migration:** Own migration, designer and snapshot together. Any temporary compatibility/cleanup dependency must have an explicit removal condition based on cleanup execution, not an unresolved banner-retention question. The disposal decision authorizes the implementation target, not production deletion, production migration application, deployment, commit or push.

**Audit/feedback:** Record any operator-approved cleanup counts and outcome. Do not fabricate user banner-delete entries. Failed cleanup must remain recoverable.

**Tests/docs:** Migration from asset/cleanup fixtures, no unrelated storage deletion, event deletion regression and removed route behavior.

**Non-goals:** General storage cleanup, touching unrelated object namespaces, or treating an empty development database as evidence of production state.

**Scheduling:** `W8-B`; depends EVT-01/RES-01. The banner-retention gate is resolved by the 27 September user disposal decision; production inventory is not an implementation prerequisite. Parallel with ROS-01 only under disjoint ownership. Serialize migration/designer/snapshot ownership against current integration work when executing this delayed ticket.

---

### WOM-01 — Make connection semantics match website authority

**Outcome:** WOM backend operations follow the new ownership rules without duplicating the existing integration.

**Current problem:** Linking can import provider dates; manual links lack management credentials; connection provenance must remain distinct from write capability.

**Scope:** Synchronization/management interfaces and services, management/provenance persistence, credential protection, provider result mapping and existing workers.

**Target:**

- Remove provider-to-website schedule import and Admin sync toggle.
- Link only after comparing website/provider dates using the existing supported five-minute tolerance; return both windows on mismatch.
- Support all three approved cases. **A: website-created** has a protected credential and permits management/synchronization plus deletion only under the approved pre-Live rules. **B: externally-created, ID-only** permits fetch/display only, with no upstream write capability. **C: externally-created with a verification code** permits management/synchronization through the protected credential, but never remote deletion.
- Persist provenance independently of credential/write capability. “Manual” and “managed” are not mutually exclusive; adding or replacing a credential cannot relabel an external competition as website-created.
- Accept the supplied code through a secure input, store it protected and never return it to the UI, logs, Audit or prefilled forms. Validate the supplied credential non-destructively if WOM provides a supported mechanism. If no dedicated validation mechanism exists, validate it on the first legitimate management operation; never perform a synthetic/unrelated mutation merely to test the code. Until validated, present its status truthfully as unverified. Invalid/expired/revoked credentials produce an explicit state, never apparent successful synchronization. The [official WOM competition endpoint documentation](https://docs.wiseoldman.net/api/competitions/competition-endpoints) documents codes on management operations; this plan does not assume a dedicated credential-validation endpoint.
- Automatic website-origin schedule/roster changes use existing operations and synchronization for A/C only. B remains read-only: local changes save normally and Admin sees that remote configuration cannot synchronize. Do not repeatedly queue impossible writes.
- Scope automatic update-all and any other upstream write to authorized writable connections. ID-only links may still use the existing scheduled/manual data reads. Retain schedule anchoring and cooldown protection for every applicable operation.
- Competition creation accepts a valid finalized roster from either actual website draft or direct Finalize roster; preserve that distinction without adding synthetic draft state.
- Live changes synchronize permitted dates only.
- Never remotely delete a manually linked competition, even if writable.
- Preserve no deletion after ever Live.
- Validate full create payload and expose affected accounts from provider errors.
- Never mark a partial/unknown outcome healthy; retain reconciliation and prevent blind duplicate creation.
- Preserve current anchored fetch/update-all behavior and rate limiting.

**History/migration:** Backfill provenance from reliable operation evidence. Existing ID-only manual links remain external/read-only; adoption of a supplied credential changes capability only. Existing protected website-created links retain their proven provenance. Unknown provenance must never gain delete permission. Keep encrypted credentials protected and absent from responses/Audit. Do not auto-fetch private credentials or infer ownership merely from the presence of a management record.

**Audit/feedback:** Create/link/disconnect/delete, credential adoption/capability change without the credential itself, config changes, requested operations and confirmed outcomes. Show provenance independently from capability. ID-only links explicitly say data can be fetched but schedule/roster cannot be synchronized. Distinguish invalid credential, unavailable credential, pending, unknown, partial and rate-limited outcomes; preserve local-save versus remote-sync status.

**Tests/docs:** A/B/C connection matrix through service and direct HTTP boundaries; ID-only fetch success with every write denied; external-with-code synchronization allowed but delete denied; website-created deletion only while eligible. Test adding/replacing a code without provenance escalation, supported non-destructive validation when available, and otherwise protected storage with an unverified state until the first legitimate management operation succeeds or rejects the code. Verify no synthetic validation write, invalid/revoked credentials, secret redaction, historical backfill, both roster-finalization methods, link mismatch, lost response, partial result, retries, actor revocation, start/delete races and schedule anchoring.

**Non-goals:** Manual update-all, new job system or provider-based lifecycle blockers.

**Scheduling:** `W9-A`; depends LIF-01/ROS-01 (including DRF-01's publication contract transitively). The A/B/C credential decision is approved, not a pending gate. Parallel with ACT-01, which must not edit WOM handlers/services. Owns W9 migrations.

---

### ACT-01 — Derive Admin actions from current authoritative state

**Outcome:** Admin actions identify unresolved operational work and disappear when that work is resolved.

**Current problem:** Shell actions include vacancies, missing Captains and manually completed promotion follow-ups; directory attention uses a separate narrower projection.

**Scope:** `SharedShellService`, Notifications, event directory attention, scheduled-attempt readers and shared navigation projections.

**Target:**

- Remove retired action categories.
- Aggregate Pending evidence into useful event-level links/counts.
- Include genuinely failed/postponed scheduled opening/start work.
- Derive current action status from current event/readiness state; historical failure records alone must not keep actions alive.
- Reuse the projection for directory Needs attention.
- Reading a personal notification changes only read state.
- Keep personal notifications separate and retain their current public/participant behavior.
- Do not redesign `/Admin`.

**History/migration:** Preserve historical notifications/follow-up data when needed; no new persisted manual task model.

**Audit/feedback:** Action projection is read-only. Personal read-state changes have an explicit no-global-Audit rationale. Show accurate counts, empty states and safe retry if loading fails.

**Tests/docs:** Resolve issue without reading notification, read without resolving issue, aggregation/count consistency, hidden-event filtering and lifecycle changes.

**Non-goals:** Dashboard statistics or a new notification platform.

**Scheduling:** `W9-B`; depends RES-01/ROS-01. Parallel with WOM-01 using shell/directory ownership only. Must not overlap other shell/navigation writers.

---

### BRD-02 — Make Board reads lightweight with targeted freshness

**Outcome:** Unchanged Board overviews avoid full editor loading/recalculation while Draft estimates remain current.

**Current problem:** Board loading eagerly builds every editor and loads catalogue choices; simply caching this would risk stale EHB.

**Scope:** Board read/editor endpoints, estimate calculation extraction, catalogue mutation invalidation, affected-tile lookup, cache/fingerprint persistence and approval validation.

**Target:**

- Overview loads positions, names, images, stored estimates and status.
- Full editor data loads on demand.
- Catalogue choices can be session-cached with a revision/fingerprint.
- Tile writes recalculate the affected tile.
- Catalogue writes mark/recalculate only dependent Draft working tiles.
- Dirty estimates refresh without requiring manual tile saves; invalid data is visibly unresolved, never silently presented as fresh.
- Approved/public snapshots stay frozen.
- Approval performs a full authoritative recalculation and fingerprint check.
- Use small freshness metadata and existing persistence; no generic job/cache platform.

**History/migration:** Add only required freshness fields/indexes. Backfill unfinished working tiles as needing verification; do not rewrite approved estimates.

**Audit/feedback:** User changes retain their existing Audit owner. Derived cache refresh is explicitly exempt from global Audit. Show calculation blockers and stale editor choices with recovery.

**Tests/docs:** Unchanged-overview query/calculation counts, affected-only invalidation, concurrent catalogue/approval changes, missing mappings, protected correction estimates and session cache invalidation.

**Non-goals:** EHB formula changes, full frontend rewrite or global recalculate control.

**Scheduling:** `W10-A`; depends CAT-01/BRD-01/ROS-01. Parallel with WOM-02. Sole W10 migration owner. Must not overlap Catalogue/Board writers.

---

### WOM-02 — Establish the dedicated WOM workspace

**Outcome:** All WOM configuration and operations have one event page; Overview summarizes and links.

**Current problem:** Manage mixes lifecycle, integration configuration and operation controls.

**Scope:** New WOM Razor workspace/PageModel, migrated Manage handlers/forms, event navigation and existing WOM presentation/result projections.

**Target:**

- Available before roster finalization with an explanation that creation needs validated final teams/roster. Accept both DRF-01 publication methods; do not tell a zero/one-included-team event to run a website draft.
- Show connection provenance, write capability, management status, coverage/missing names, last/next fetch and applicable update-all information. Explicitly distinguish website-created/manageable, external/read-only and external/manageable. For ID-only links explain that fetch/display works but synchronization and update-all writes do not; do not display an impossible next write as scheduled.
- Present queued, first-fetch-pending, partial, unavailable, rate-limited and configuration states distinctly.
- **Fetch WOM data now** uses the sole typed-confirmation exception and retains backend cooldown protections. Its confirmation explicitly explains: “Manual fetches should only be used when fresh data is genuinely needed. Repeated unnecessary requests may trigger Wise Old Man rate limits and affect the integration for everyone.”
- Website-created eligible pre-Live deletion uses one consequence confirmation. Every externally-created connection exposes Disconnect only, even when it has a valid verification code and automatic synchronization.
- Provide optional secure verification-code entry when linking an external competition or enabling its write capability. Never redisplay the stored secret, including validation failures, HTML/JSON responses or browser form recovery. Management labels reflect actual capability and distinguish an unverified supplied credential from successful validation on a supported check or legitimate management operation. Preserve external provenance; do not equate “managed” with permission to delete.
- Remove duplicate Manage editing paths.
- Use shared dialogs/feedback; do not settle the later page’s final visual composition.

**History/migration:** No schema change. Old safe GET links may redirect; obsolete POST handlers cannot retain duplicate mutations.

**Audit/feedback:** Use WOM-01 operations. Distinguish local accepted request, queued work, successful remote outcome and unknown outcome.

**Tests/docs:** All A/B/C presentation/action combinations, one-time credential input without redisplay, invalid/revoked credential state, read-only sync explanation, both roster-finalization methods, stale operation forms, forged external-delete attempts, typed-fetch warning/confirmation and cooldown behavior, unverified-to-validated/rejected credential presentation, secret redaction, mobile keyboard/modal interaction and Overview ownership.

**Non-goals:** New integration capabilities, manual update-all or broad Admin redesign.

**Scheduling:** `W10-B`; depends WOM-01/ACT-01. Parallel with BRD-02. Must not overlap Manage/event-navigation writers.

---

### VER-01 — Verify the simplified product and complete coverage

**Outcome:** Every reachable Admin/Super Admin persisted mutation has deliberate Audit and feedback coverage, retired commands are unreachable, and public/history regressions are checked.

**Scope:** Entire reachable Admin mutation surface, direct services, scheduled consequences, retained history, UI interaction and required release gates.

**Target:**

- Enumerate routes/handlers, application commands and Admin-triggered provider operations.
- For each, record authorization, allowed lifecycle, concurrency, Audit owner or exemption, expected failure outcomes, confirmation/reason rule and executable evidence.
- Include less visible mutations such as evidence codes, payment/notes, team roles/focus, reset issuance, quarantine and catalogue recovery.
- Verify disabled controls and hidden actions against server behavior.
- Exercise runtime states; source-string searches alone do not prove that confirmations render as modals or feedback is visible.
- Confirm legacy commands cannot be invoked through old POSTs or direct services.
- Perform historical migration/read checks and full public regression.

**History/migration:** Rehearse all new migrations together on controlled fixtures and an appropriately protected representative restore where available. No production repair.

**Audit/feedback:** Close concrete coverage gaps through their owning ticket/worker. Document intentional exceptions such as derived-cache writes and domain-history-only actions.

**Tests/docs:** Complete .NET/Node gates, Release build, format/diff checks, CI shard completeness, browser journeys and page-specific functional acceptance. Consolidate current authority and known limitations.

**Non-goals:** Another redesign, unrelated cleanup, packaging or deployment.

**Scheduling:** `W11`; depends all applicable tickets and decisions. No concurrent production edits while reviewing the integrated result.

## 6. Historical-data and migration strategy

### Remove future authority before deleting historical storage

The default is to stop future writes and remove active workflows while preserving harmless historical columns, enum values and foreign keys.

This applies especially to:

- Emergency actors referenced by evidence/Audit.
- Accountless historical participants.
- Old formation types.
- Membership replacement links and departures.
- Resubmission predecessor links.
- Final-review resolutions and completion corrections.
- Legacy Finalized events.
- Old planning fields.

Keeping data does not retain authority. Tests must prove those records cannot reactivate retired commands.

### Treat unfinished events separately from archived history

PRE-01 must identify unfinished events containing retired concepts.

Do not automatically:

- resume Paused drafts;
- assign accountless participants to similarly named users;
- cancel pending remote operations;
- enable a previously disabled scheduled opening;
- rewrite a finalized roster;
- convert historical results using the new ranking path.

Each required transition must have a deterministic rule and appropriate evidence before rollout. Otherwise, that affected event needs a controlled operator decision.

### Preserve immutable competitive versions

- Old official results continue to use their saved placement snapshots.
- Reopening creates a new correction cycle.
- Original draft picks remain original.
- Updated current roster publication cannot erase earlier publication history.
- Approved board snapshots and evidence-attribution snapshots retain their identities and values.
- Retained historical corrections remain explainable even though no new correction command exists.

### Banner removal is conditional, not presumed safe

Physical banner deletion is preferred only after checking database references, stored objects and cleanup state.

The absence of visible banners is insufficient evidence that storage can be dropped. Conversely, an unused empty subsystem should not survive merely because its code exists.

### Migration mechanics

- One migration owner at a time.
- Migration, designer and model snapshot move together.
- Rehearse upgrade from the deployed schema; a clean database test alone is insufficient.
- Retain original migrations needed to build historical schema.
- Verify foreign keys and filtered uniqueness on real PostgreSQL.
- Normalize timestamp test expectations to PostgreSQL precision.
- Keep production/private inputs outside committed artifacts.
- Use backups and normal deployment procedures for eventual promotion; no reset-based rollout.

## 7. Final verification and regression strategy

### Focused checks during implementation

Each ticket runs checks that expose its changed risks. Reuse applicable passing evidence; do not run the entire suite for every small correction.

Security and persistence assertions must exercise server/service/PostgreSQL boundaries. Source inspection alone cannot establish authorization, transaction or race correctness.

### Required integrated journeys

1. **New-event journey:** minimal create → configure signup/schedule → signup/waiting → draft → Board approval/publication → WOM → automatic Live/end → evidence review → publish directly to Archived.
2. **Direct-roster journey:** zero and one included team both use manual assembly → Finalize roster with no fabricated picks/Running history; Board publication, WOM creation and event start accept the finalized roster. Two or more included teams follow the real draft. Cancel private Running draft is blocked until explicit Undo last pick operations reduce active picks to zero.
3. **Signup correction journey:** edit questions while open, remove an optional account field, verify release/privacy, change code, edit without code, withdraw/rejoin using current code.
4. **Roster journey:** finalized Remove succeeds, Add fails, team remains short; successful later Add reuses identity and queues WOM update; Live start closes all roster mutation routes.
5. **Evidence journey:** Admin-role matrix, active-account switching race, Pending edits, rejection/new ordinary submission, approval/reversal and current-score recalculation.
6. **Final Review journey:** open window/Pending block publication; all competitive inputs determine official ordering. Exact equality may produce a justified shared rank without acknowledgement, while any competitive difference must be honored. Reopen produces a new official version and preserves the old one, including historical shared ranks.
7. **WOM recovery journey:** all A/B/C provenance/capability combinations, optional verification-code adoption without redisplay, ID-only read success/write denial, external manageable sync without delete rights, mismatch, invalid account/credential, rate limit, partial/unknown operation, retries and forbidden remote deletion.
8. **Multi-Admin journey:** stale version, Board takeover, draft takeover, permission revocation during an open form and lifecycle change before submit.
9. **Quarantine journey:** direct routes, assets, participant/Captain access, ordinary Admin access, separate Super Admin inspection/restore.
10. **Historical journey:** archived accountless/emergency-attributed evidence, old formation types, old resubmission links, manual corrections and legacy Finalized results.

### Public regression boundary

Preserve:

- Exact-link signup table and disclosure/privacy.
- Board, Teams, Drops, Leaderboards and Stats.
- Submission workspace and normal Captain/Co-captain authority.
- Evidence codes and Team Focus.
- Drop NEW state and announcements.
- Original submission timing in Recent Activity.
- Historical routes and official snapshots.
- Existing responsive public composition.

### Audit and feedback completion evidence

The final coverage record must enumerate reachable mutations, not just pages. For each applicable outcome, it must identify evidence of:

- useful content;
- correct severity;
- correct persisted-state claim;
- recoverability;
- visibility while dialogs are open;
- no duplication;
- no secret/private-data leakage.

The user should not be responsible for discovering every forgotten state. Implementers supply a complete exercised state set; user acceptance concerns the delivered behavior and presentation.

### Release gates

Use the repository’s established commands, including complete Release solution tests, BrowserTests Node suite, Release build, formatting verification and diff checks. Preserve CI’s integration-shard discovery/completeness check and require every shard.

The production pipeline, publication, merge and deployment remain separately authorized actions.

### Review workflow

Use the user-approved execution hierarchy:

**Astra planner → persistent Sol/medium coordinator → fresh Terra/medium orchestrator per ticket or coherent work package → Luna/max implementer(s) → independent Sol/high reviewer → same-worker remediation/recheck.**

**Current implementer setting — user revision, 26 September 2026:** restore `gpt-5.6-luna` / `max` for new implementation, focused implementer checks and named remediation turns. Do not interrupt, cancel or change the model of any currently running `gpt-6-astra` / `high` implementer turn; let each finish its current turn and report its checkpoint. The change applies to its next implementation/remediation turn and all new implementer assignments. Preserve completed work and check evidence. Change the existing worker model between turns if supported; otherwise make one bounded handoff to a Luna/max worker after the Astra turn ends, retaining the same independent reviewer and avoiding repeated discovery. Coordinator, orchestrator and reviewer settings remain unchanged. The coordinator must notify every active orchestrator, update future worker briefs and reconcile the active repository workflow/status records. This supersedes the earlier temporary Astra/high implementation override; historical launch records below remain historical.

The Astra planner owns the approved scope, architecture, dependency graph, ticket boundaries, safe parallelism and migration ownership. It is consulted for consequential plan/product changes, rather than remaining active for routine execution. The **Sol/medium coordinator is the only long-lived execution context**. This project-specific user decision supersedes the earlier plan wording that combined planning and persistent coordination; PRE-01 must reconcile the applicable repository workflow authority before dispatching implementation.

- **Coordinator ownership:** read the approved plan and dependency graph; track ready, running, blocked, needs-remediation and accepted/completed tickets in the existing `CURRENT_STATUS.md` handoff. Select ready packages and concurrency within the approved waves, source ownership, migration constraints and actual agent capacity. Start fresh orchestrators, wait for their outcomes, record evidence and unlock satisfied dependencies. Own the W6/W9 integration checkpoints and final delivery reconciliation; do not implement production code or repeat independent source review.
- **Rotation boundary:** use a fresh orchestrator for each meaningful ticket or explicitly bounded coherent package. A package may group closely related work with a common delivery boundary, but must preserve dependencies, ownership exclusions and checks. Do not keep one orchestrator for all 24 tickets, or rotate for each tiny correction. Retain the same orchestrator and workers through open remediation/recheck.
- **Small dispatch context:** the coordinator supplies only assigned ticket sections, applicable shared contracts, checkout/branch and baseline, completed prerequisite evidence, ownership/migration constraints, exact coordinator/orchestrator callback identities, required checks and stop boundary. Link the complete plan for reference rather than copying the entire project history into each brief. Refresh only missing or changed facts.
- **Package ownership:** the orchestrator investigates its bounded assignment, dispatches one implementer by default, inspects the result/diff and focused-check evidence, dispatches the independent reviewer on a stable diff, requests fixes from the same implementer and sends named rechecks to the same reviewer. Multiple implementers require useful independent work and disjoint ownership within that package. The orchestrator accepts the package only after its applicable checks and independent review pass; its inspection is not a replacement review.
- **Outcome routing:** orchestrators return concise **accepted**, **needs remediation**, or **blocked** outcomes to the coordinator, with evidence, next owner and any decision needed. Routine corrections remain within the package team and proceed without another planner approval. A needs-remediation or blocked outcome cannot unlock dependents; the coordinator keeps or resumes the same team where possible. Scope/product/architecture changes or conflicts in the approved plan escalate through the coordinator to the Astra planner and user as needed.
- **Planner wake-up boundary — explicit user correction, 26 September 2026:** routine checkpoints, worker starts/resumes, test results, review dispatch/results, remediation, package completion and resolved blockers stay with the coordinator and the durable status record. Do not send these to the planner, copy the planner on callbacks, or send acknowledgement-only messages to it. Wake the planner only for an unresolved blocker the coordinator cannot resolve within existing authority, a consequential scope/product/architecture decision, or an explicit user request for planner involvement. Each escalation must identify the precise decision/action required, evidence, and recommended next step. Orchestrators report to the coordinator, not directly to the planner. This replaces any earlier assignment requesting routine or duplicate planner callbacks; propagate it to active teams and future briefs. No acknowledgement callback to the planner is needed for this routing change.
- **Durable completion:** each orchestrator supplies the exact diff/baseline, checks and evidence, unresolved findings, acceptance state and next permitted action. The coordinator consolidates these into the existing status handoff and serializes shared status-file updates across packages. Distinguish technical acceptance, integration into the baseline used by dependents, manual acceptance and release approval. After the coordinator records a completed package, retire that orchestrator context; the next package starts fresh and reuses the recorded evidence.

No additional routine verifier or management layer beyond this hierarchy. The coordinator manages execution; it does not repeat the Astra planner's repository-wide investigation. Existing packaging, publication and release authorization boundaries remain unchanged.

I recommend requesting **Astra/high in place of the ordinary reviewer for SEC-01 and RES-01**:

- SEC-01 removes authority across authentication, cookies, services and retained identities.
- RES-01 changes publication state while preserving immutable historical results and race safety.

That is a recommendation for approval, not an additional review pass or a silent model substitution.

### Task layout and required reporting

The user-approved layout uses **separate visible Codex tasks/chats for the coordinator and each orchestrator**, with **collaboration subagents inside each orchestrator task for implementation and independent review**. The persistent coordinator is `gpt-5.6-sol` / `medium`; fresh package orchestrators are `gpt-5.6-terra` / `medium`; implementers/remediators use `gpt-5.6-luna` / `max` for new turns, with currently running Astra/high turns allowed to finish uninterrupted; independent reviewers are `gpt-5.6-sol` / `high`, subject to an explicitly approved ticket-specific override. Subagents receive bounded context and explicit model/reasoning. The user mainly follows the coordinator chat; orchestrator chats remain available for inspection.

When execution is started, the coordinator creates the authorized per-package orchestrator tasks through `create_thread` under the saved BingoWebpage project, following project/worktree selection rules and PRE-01's confirmed baseline. Record each returned actual task ID and host; a pending `clientThreadId` is not a callback-ready task ID. Do not dispatch dependent work from an unverified checkout. The chosen topology is part of this approved workflow; the 26 September startup instruction authorizes coordinator launch and per-package orchestrator dispatch.

**Every orchestrator brief must contain a filled-in reporting contract:**

- Exact coordinator task ID/host and the assigned ticket/package ID. Obtain the coordinator ID from the launch handoff; never infer it from pinned/recent tasks. Record the orchestrator's actual task ID in the coordinator's status entry when available.
- Preserve the orchestrator identity returned by task creation separately from its worker/reviewer identities. A callback's source ID may identify a child worker; it must not overwrite the package's orchestrator ID. Before declaring an orchestrator idle, unreachable or in need of replacement, verify the exact parent task against the launch record and its recorded child assignments. A child-worker routing rejection is not evidence that the parent task cannot receive messages. Route ordinary worker coordination through the existing orchestrator; do not ask the user to relay directions or launch a duplicate review because the wrong task ID was selected.
- Explicit authorization for scope-limited callback messages containing repository paths, branch/revision identifiers, findings and check/evidence references needed for this assignment.
- Before ending at an outcome or decision boundary, call `send_message_to_thread` addressed to that coordinator with **accepted**, **needs remediation**, or **blocked**; include exact checkout/branch and baseline/reviewed revision (plus a diff identifier for uncommitted work), completed checks/results, independent reviewer verdict and evidence links, unresolved findings, next owner and any decision needed. A final answer in the orchestrator chat alone does not fulfill the separate-task callback contract.
- Routine implementer/reviewer handoffs stay inside the orchestrator task through collaboration tools. Resume idle subagents using `followup_task`; retain the same implementer/reviewer for named remediation and recheck. Do not end with an authorized, actionable internal handoff unassigned merely because a status callback was sent.

**Coordinator-side recovery is required as well as the callback.** Use `wait_threads` with the recorded orchestrator IDs, hosts and cursors to await results. A callback may interrupt that wait. Reconcile both channels as one package outcome, keyed by ticket/package and revision, without recording duplicate completions. A timeout is neither a pass nor proof that work is still running: inspect the affected task's status when necessary. If an orchestrator finishes without a usable report, wake that same task through `send_message_to_thread` to complete the missing handoff. If the callback fails, the orchestrator records the exact failure in its final handoff so the coordinator can recover it through the wait/read route; do not bypass an approval rejection.

**Mandatory escalation on a blocked handoff — user clarification, 26 September 2026:** stop only the blocked action. Before ending its turn, the orchestrator must notify the coordinator using the callback above. The coordinator resolves ordinary execution issues within its authority. When an automatic approval rejection concerns an already understood, scoped action, the coordinator presents the concrete action, risk and exact approval request directly to the user, including the task where approval must be entered. An automatic rejection or need for user approval does not by itself require waking the planner. Escalate to the planner only when uncertainty about intended behavior, scope, architecture, safety or conflicting authorities requires a planning decision; include evidence, recommendation and the precise unresolved question. The planner resolves that uncertainty or asks the user for the remaining decision. This supersedes earlier instructions to route every approval block through the planner. No rejection may be retried, split or bypassed without the required changed condition/authorization. A final status message in a lower-level task, an empty final answer, or an unassigned next action does not complete the required handoff. Record the next owner and successful callback; if delivery fails, report the exact failure for recovery without bypassing an approval rejection. Continue independent authorized work, and resume existing workers when the blocker is resolved. Do not poll indefinitely or require the user to discover and relay a stalled handoff.

The coordinator unlocks dependent tickets only after recording the required passed checks/review and confirming that the accepted prerequisite changes are available in the dependent task's baseline. **Needs remediation** and **blocked** never unlock dependents. A passed source review with required executable checks blocked is recorded as **source review passed; verification blocked**, not unconditional technical acceptance. Retire completed orchestrator context after this reconciliation; preserve unresolved teams. Technical acceptance does not imply manual UI acceptance, commit/push authority or production promotion.

### W11 execution efficiency — planner correction, 27 September 2026

The user's execution-speed review exposed excessive subdivision and duplicate verification. These rules supersede routine per-micro-package classification/permission and exact-count rerun requirements in earlier W11 briefs; they do not override an actual approval rejection or a consequential product decision.

- Finish the already-running D9 package without restarting its investigation. Then perform the planned single full-suite rebaseline and select further work from those current results. Previously failing counts are accounting, not a reason to repair cases already passing through shared fixes.
- Group subsequent remediation by shared cause and affected contract, prioritizing fixes that cover many failures. Do not create a delivery cycle per assertion or small arbitrary case count. Within an assigned coherent batch, the implementer diagnoses and corrects unambiguous fixtures against approved behavior without a separate read-only classification approval turn. Escalate ambiguous behavior, newly required production scope, coverage removal and actual permission blocks.
- Use one stable batch review after relevant executable checks. Retain the same implementer/reviewer through that batch's named remediation; use fresh bounded contexts at the next meaningful batch boundary where accumulated context is causing repeated compaction. Preserve established evidence instead of restarting repository discovery. Models remain Luna/max implementation and Sol/high independent review.
- Diagnostic runs are optional tools, not mandatory gates before another identical final run. A passing superset on the relevant unchanged source/build satisfies its included cases: derive original-failure membership from TRX identities, never rerun solely to obtain an exact case count. Build when required by changed inputs; reuse current artifacts and passing checks where applicable. Broaden or repeat execution only for changed risks, concrete failures or required release gates.
- Fold compact evidence/status updates into the normal handoff and coordinator reconciliation. Do not dispatch a separate implementation turn merely to transcribe an accepted result into status. Serialize shared status writes and keep one current next-action statement; historical stop-boundary text must not block currently authorized work.
- Workers hand off inside the orchestrator; the orchestrator owns acceptance and reports to the coordinator. The coordinator must not duplicate each worker instruction or echo worker evidence back as a second dispatch. Routine handoffs do not wake the planner. Preserve required callbacks and genuine-blocker recovery.

Keep meaningful authorization, transaction, persistence, concurrency, privacy and historical-evidence checks. Do not weaken assertions, delete coverage, or waive unresolved failures to improve the pass count. Consolidation/retirement proposals identify the redundant boundary and retained protection before approval. Packaging, production-data, migration-application, deployment and manual-acceptance restrictions are unchanged.

### Authorized execution startup — 26 September 2026

**Latest explicit user instruction:** after being informed of both task-creation rejections, the user requested a new coordinator and renewed authorization for visible per-package orchestrator creation, implementer/reviewer subagents, required checks, same-team remediation and scoped task-to-task reporting. The planner instructed predecessor `01a0dc81-8aee-7172-b272-b28466d7d870` to remain stopped, then requested **Admin simplification coordinator — replacement**, `gpt-5.6-sol` / `medium`, with the complete authorization and rejection history included. Replacement coordinator task ID: `01a0dc89-e5a3-7902-8f90-c0bcdd55e2bb`, host `local`. Its read-only coordinator checkout is `/Users/christopher/.codex/worktrees/cd8d/BingoWebpage`, clean/detached at `22af254c893bb51e7820d84fc4154ff9af3bcc90`; the integration branch remains `admin-simplification` in `/Users/christopher/.codex/worktrees/735f/BingoWebpage`. The verified callback identity has been delivered and PRE-01 dispatch instructed. No new rejection may be bypassed or retried through another route. The earlier stopped state below records the predecessor's history, not ownership of the replacement execution.

The user has instructed the planner to start the persistent Sol/medium coordinator with this approved plan. The planner refreshed `origin/main` to `22af254c893bb51e7820d84fc4154ff9af3bcc90` and created `admin-simplification` from that commit before coordinator launch. Preserve the unrelated changes in the saved checkout `/Users/christopher/Documents/BingoWebpage`; use an isolated worktree for this assignment. Coordinator **Admin simplification coordinator** is running as `gpt-5.6-sol` / `medium`, task ID `01a0dc81-8aee-7172-b272-b28466d7d870`, host `local`, on the saved BingoWebpage project. Its isolated execution checkout is `/Users/christopher/.codex/worktrees/735f/BingoWebpage`, verified clean on `admin-simplification` at the baseline above. The exact coordinator callback identity was delivered and PRE-01 dispatch instructed under the approved workflow. Automatic approval review rejected the coordinator’s `create_thread` action because it did not recognize trusted direct user authorization for another visible user-owned task. The coordinator initially stopped. It later reported one retry after direct user authorization in its conversation; automatic approval review rejected that too, treating the supplied authorization as quoted/untrusted transcript content and citing the stop-after-rejection rule. No further automated retry, alternate-tool dispatch or fresh-context retry is authorized as a workaround. Broader permission wording is not the identified missing requirement. Task creation remains blocked pending resolution of the approval-system issue or the user manually creating the task through the app. No PRE-01 orchestrator was created, no worker was launched and no feature implementation started. Originating planner: `01a0d414-daca-72f3-bea0-d1e9e53a3b2f`, host `local`.

The coordinator's first package is PRE-01: verify branch/baseline identity, establish the actual promoted release evidence and relevant read-only data preflight, and reconcile approved product/workflow authorities before feature implementation. Reuse the branch already created; do not recreate it from the older local `main` or this planner's stale checkout. Production promotion is not established by this plan. Carry forward existing evidence and inspect only changed or missing facts. Continue eligible approved packages under the dependency/ownership rules; pause only affected work for genuine blockers or required decisions. Existing restrictions on commits, pushes, deployment, data mutation and manual acceptance remain in force.

## 8. Ticket index

| Wave | Ticket | Depends on | Parallel with | Conflict risk | Main area |
|---|---|---|---|---|---|
| W0 | PRE-01 | Plan approval | — | Baseline/data decisions | Branch and transition readiness |
| W1 | ADM-01 | PRE-01 | ADM-02 | Shared results/Audit/resources | Outcomes and Audit |
| W1 | ADM-02 | PRE-01 | ADM-01 | Shared layout/JS/CSS | Confirmations |
| W2 | SEC-01 | ADM-01/02 | CAT-01 | High: access, draft, lifecycle, shell | Emergency retirement |
| W2 | CAT-01 | ADM-01/02 | SEC-01 | Catalogue owner | Catalogue simplification |
| W3 | EVT-01 | SEC-01 | ACC-01, EVD-01 | Event domain/creation | Minimal event setup |
| W3 | ACC-01 | SEC-01 | EVT-01, EVD-01 | Account services | Support/security |
| W3 | EVD-01 | SEC-01 | EVT-01, ACC-01 | Evidence authority; possible migration | Attribution and switching |
| W4 | LIF-01 | EVT-01 | TEM-01, EVD-02 | Event domain/Manage/workers | Schedule/lifecycle |
| W4 | TEM-01 | EVT-01, SEC-01 | LIF-01, EVD-02 | SignupService/DraftModel | Team/identity retirement |
| W4 | EVD-02 | EVD-01 | LIF-01, TEM-01 | SubmissionService | Attempts and review |
| W5 | SGN-01 | TEM-01, LIF-01 | DRF-01 | SignupService/questions | Form configuration |
| W5 | DRF-01 | TEM-01, LIF-01 | SGN-01 | Draft/readiness/publication-mode migration | Draft and direct roster finalization |
| W6 | SGN-02 | SGN-01, LIF-01 | BRD-01 | SignupService/event configuration | Admission/capacity |
| W6 | BRD-01 | EVT-01, DRF-01 | SGN-02 | BoardModel/snapshots | Board editing/lifecycle |
| W7 | PAR-01 | SGN-02, DRF-01 | RES-01 | SignupService | Pre-draft participants |
| W7 | RES-01 | LIF-01, EVD-02, BRD-01 | PAR-01 | Event domain/finalization/ranking | Official results with exact-tie exception |
| W8 | ROS-01 | PAR-01, DRF-01, BRD-01, LIF-01 | BNR-01, conditionally | SignupService/Draft/WOM payload | Finalized roster |
| W8 | BNR-01 | EVT-01, RES-01, data gate | ROS-01, conditionally | Schema/event/storage | Banner removal |
| W9 | WOM-01 | LIF-01, ROS-01 | ACT-01 | WOM services/provenance/capability schema | Three connection cases |
| W9 | ACT-01 | RES-01, ROS-01 | WOM-01 | Shell/directory/navigation | Derived Admin actions |
| W10 | BRD-02 | CAT-01, BRD-01, ROS-01 | WOM-02 | Board/catalogue/schema | Performance/freshness |
| W10 | WOM-02 | WOM-01, ACT-01 | BRD-02 | Manage/WOM/navigation | Dedicated workspace |
| W11 | VER-01 | All applicable tickets | — | Integrated result | Final verification |

All post-foundation tickets also inherit ADM-01/02.

## 9. Parallel execution map

```text
PRE-01
  │
  ├── ADM-01 ──┐
  └── ADM-02 ──┴─────────────────────────────────┐
                                                │
          SEC-01                    || CAT-01   │
             │                           │      │
     ┌───────┼────────┐                  │      │
   EVT-01  ACC-01   EVD-01                │      │
     │                │                  │      │
   LIF-01 || TEM-01 || EVD-02             │      │
      \       /  \         │             │      │
      SGN-01 || DRF-01     │             │      │
         │        │        │             │      │
      SGN-02 || BRD-01     │             │      │
         │         \       │             │      │
      PAR-01   ||   RES-01               │      │
         │             │                 │      │
      ROS-01   ||   BNR-01 [data gate]    │      │
         │                               │      │
      WOM-01   ||   ACT-01               │      │
         │             │                 │      │
      WOM-02   ||   BRD-02 ◄─────────────┘      │
         └─────────────┴────────────────────────┘
                       │
                     VER-01
```

The detailed dependency table governs where this compact diagram omits an edge. The only added ticket dependency is LIF-01 → DRF-01 for shared readiness consumers. The former WOM credential-decision gate is removed because all three connection cases are approved. The exact-tie exception is also approved and creates no new dependency. The same execution waves remain valid; DRF-01 owns any W5 publication-mode migration.

Maximum sensible implementation concurrency in the proposed schedule is **three implementers**, falling to one or two where source ownership requires it. Independent ready tickets may each have their own fresh Terra/medium orchestrator, including up to three independent packages in a wave when execution capacity permits. This is a dependency-safe ceiling, not a requirement to occupy three lanes. The Sol/medium coordinator budgets actual agent capacity across itself, orchestrators, implementers, reviewers and any active planner escalation; stagger package dispatch when necessary so orchestrators do not consume all slots and prevent their workers from starting. Independent reviews wait for stable ticket diffs, with capacity reserved for review and remediation. Fresh contexts do not relax shared-file or migration serialization.

## 10. Risks and decisions still requiring confirmation

### 1. WOM provenance and write capability — approved

Three connection cases are required:

| Case | Provenance | Capability | Credential | Remote deletion |
|---|---|---|---|---|
| A | Website-created | Fetch and authorized management/synchronization | Stored protected | Only under approved pre-Live rules; never after ever Live or Archived |
| B | Externally-created, ID-only link | Fetch/display only; no configuration sync, update-all or other write | None | Never |
| C | Externally-created, linked with verification code | Fetch and authorized management/synchronization | Supplied once and stored protected; validated non-destructively if supported, otherwise on the first legitimate management operation; never redisplayed | Never |

“Manual” and “managed” are not mutually exclusive. Credential adoption changes capability, not provenance. A valid credential must not grant permission to delete an external competition. A read-only link is a supported state, with an explicit Admin explanation. WOM-01/WOM-02 implement these approved rules; no credential-choice approval remains pending.

### 2. Alt fields — approved

Zero configured Alt fields is valid. Every configured Alt field is optional. Playing fields remain distinct: at least one exists, and the first is required/protected. SGN-01 implements this directly; no required-first-Alt interpretation is permitted.

### 3. Fewer than two included teams — approved

Zero or one team with IncludedInDraft = true uses manual roster assembly followed by Finalize roster. Two or more use the website draft. The direct path validates the finalized roster needed by Board publication, WOM creation and event start; it must not fabricate picks or a fake Running draft. Persist the distinction between no website draft and a completed website draft on the existing publication structure.

DRF-01 now depends explicitly on LIF-01 because it adapts event-start readiness after lifecycle changes. Board/WOM/roster consumers already follow DRF-01 directly or transitively. No wave renumbering or extra foundation ticket is needed.

### 4. Cancelling a private Running draft — approved

Cancel draft may return Running → Setup only with zero active picks. Otherwise the Admin must repeatedly use Undo last pick until no active picks remain. Cancellation itself does not bulk-undo anything. Preserve existing explicit undo history and do not add abandoned-run or cancelled-pick concepts. Finalized drafts remain closed to reopening. DRF-01 includes the controller/version race checks.

### 5. Native browser unsaved-page warning — approved narrow exception

The shared custom confirmation owns product actions and in-application confirmations. A native beforeunload/tab-close/reload/browser-exit warning may remain only for genuinely unsaved edits. It clears after save/discard and must not duplicate product confirmation prompts. ADM-02 records this exception in the UI authority during implementation; this planning revision does not alter that authority file. No autosave is introduced.

### 6. Official exact ties — subsequent clarification approved

The initial revision instruction asked for unique official ranks without arbitrary fallbacks. The user subsequently clarified: “In the very edge case scenarios are tie is justified”. This later decision permits a justified exact tie and supersedes the absolute uniqueness requirement only in that narrow case.

**Verified source finding, not a test-run claim:**

- [PublicProgressCalculator.Rank and SameRank](/private/tmp/BingoWebpage-wom-managed-20260922/src/Bingo.Application/Boards/PublicProgressCalculator.cs:198) apply board completion, full-board completion time, completed lines, completed tiles, current-score/completion time and EHB. SameRank compares the competitive values and can return equality; the final team-name sort is presentation only.
- [Completion calculation](/private/tmp/BingoWebpage-wom-managed-20260922/src/Bingo.Application/Boards/PublicProgressCalculator.cs:50) yields null current-score time when no tiles are complete. With no approved contributions, EHB is zero. Two such teams genuinely have equal inputs; higher timestamp precision cannot distinguish absent progress.
- [Persisted completion-fact reconciliation](/private/tmp/BingoWebpage-wom-managed-20260922/src/Bingo.Infrastructure/Boards/TileCompletionFactReconciler.cs:24) uses approved submissions and their SubmittedAt timestamps. It does not guarantee unique timestamps across teams.
- [Existing calculator assertions](/private/tmp/BingoWebpage-wom-managed-20260922/tests/Bingo.Application.Tests/PublicProgressCalculatorTests.cs:64) include CurrentScoreTimeBreaksEqualLinesAndTilesBeforeEhbAndFormsSameRank and NullCurrentScoreTimeFallsThroughToEhbAndEqualValuesRemainTied. They explicitly expect shared ranks for equal competitive values.
- [Current Final Review](/private/tmp/BingoWebpage-wom-managed-20260922/src/Bingo.Infrastructure/Events/EventFinalizationService.cs:56) still overlays manual completion corrections and tie-confirmation blockers. RES-01 removes those future workflows and uses the authoritative competitive inputs.

**Approved target:** ranks differ whenever any intended competitive input distinguishes the teams at its proper priority. Only exact equality across all those inputs permits a shared official rank. No tie confirmation, Admin discretion to ignore a difference, alphabetical/ID/GUID fallback, manual completion-time correction or extra tiebreak competition is added. The exact-tie frequency is not assumed or measured; the rule is determined by equal competitive facts.

The official snapshot and human-readable result explain any shared rank and preserve the supporting ranking values. Prior official snapshots remain immutable. RES-01 verifies both tie and non-tie cases and retains the existing rank numbering for genuine equality.

No additional product question remains from the seven revision decisions, exact-tie clarification or five final textual corrections. Product logic, ticket decomposition/dependencies and parallel execution are approved by the user. Production-data preflight and the established implementation review/release gates remain required; they are not claimed as completed.

### Implementation risks that do not require new product decisions

- **Remote create is not a database transaction.** A lost WOM response prevents an absolute guarantee that nothing was created upstream. Preserve unknown-outcome reconciliation and never present uncertain/partial state as a healthy connection.
- **Immediate switching requires deterministic ordering.** Remove the minute delay, but protect attribution under timestamp precision and concurrent submissions.
- **Archived publication changes routing and current-event selection.** Test those consequences without broadening lifecycle policy.
- **Removing restrictions must reach every layer.** PageModel, service, domain, capability filter, worker and projection must agree.
- **Production legacy data can change rollout sequencing.** Use the preflight evidence; do not solve it with a reset or inferred ownership.
- **Shared confirmations do not complete the later UI overhaul.** This pass establishes reliable functionality and interactions; page composition remains a subsequent design assignment.

**Completion boundary:** the approved plan is the implementation authority. Execution was authorized on 26 September 2026 with a temporary Astra/high implementation override, subsequently replaced by the Luna/max setting and uninterrupted-current-turn transition above. The coordinator owns PRE-01 and subsequent eligible package dispatch, required evidence and integration checkpoints; packaging/publication remain separately authorized.

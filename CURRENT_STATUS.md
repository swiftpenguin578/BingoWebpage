# Current project status

## Formatter correction and CI monitor — 2026-09-24

PR #11's first CI attempt failed at the formatting gate before the build and
tests ran. The reviewed follow-up is a formatter-only correction on top of
`07189702c53608a9181d1c9821e2d0234b58d1ed`: 20 existing source/test/migration
files, patch SHA-256
`6ddbe7baf7ca022bbbaa80c5c8b07db5840805e44b2298acd756c19c536dd22a`, and
changed-path NUL manifest SHA-256
`cfee49358ae7d8fd07c8d98af8b03d8b8d543054bff960a39671894169129d07`.

Fresh independent Sol/high review passed. The exact formatter verification
exited 0 with no output, and `git diff --check` exited 0. The delta contains
only 311 whitespace changes, 3 charset corrections and 11 import order
corrections; migration bodies/SQL, production behavior, tests/literals, UI and
CI configuration are unchanged. No full suite was rerun for this correction.
The existing user-reported complete .NET and BrowserTests passes remain valid
acceptance evidence, and the previously recorded focused remediation results
and formatter limitation remain historical context for the earlier baseline.

The next action is to monitor PR #11 CI for the corrected formatting gate and
subsequent build/test results. No preview restart, database/provider action,
merge, deployment or production mutation is authorized by this correction.

## Release candidate packaging — 2026-09-24

The user reports that the complete Release solution test gate
(`dotnet test Bingo.slnx --configuration Release`) and the complete BrowserTests
Node gate (`node --test tests/Bingo.BrowserTests/*.test.js`) both passed. This is
user-reported acceptance evidence; this packaging worker did not rerun either
complete gate and does not invent counts or agent-run logs. The earlier
full-suite remediation entry below remains historically accurate: those gates
were deferred by that remediation, whose bounded focused checks and independent
Sol/high review passed.

The user explicitly authorized packaging all accepted current feature and
remediation work from `/private/tmp/BingoWebpage-wom-managed-20260922` on
`codex/admin-wom-competitions`, including managed WOM competitions, September
improvements, the shared actual Stats masthead, test-isolation/security
corrections and maintained authority docs. Required migrations, designers and
the EF model snapshot are included. The verified GitHub repository is
`https://github.com/swiftpenguin578/BingoWebpage.git`; its `main` was verified
at `68b16242a63497fe1b0677bc48c396d9dfb1fc79`, and the target branch had no
existing GitHub PR before packaging. The checkout's `origin` remains the local
source path by design; publication uses the separately verified GitHub URL.

Bounded file-scope/privacy checks found no build outputs, `accounts.env`, dumps,
database files, private evidence, real-participant data artifacts or local-only
scripts in the candidate. Ignored `bin/` and `obj/` outputs remain untracked.
The formatter limitation remains: the completed verification exited 2 only for
accepted-baseline diagnostics, and its retry was blocked by sandbox Roslyn
named-pipe permissions; no mass formatting was applied. The focused remediation
Release build, BrowserTests/Stats/original-reproduction checks, safe-logging
recheck and `git diff --check` remain recorded as passed in the remediation
outcome; the user-reported complete suites supersede the prior pending gate.

The running preview and manual database remain outside packaging scope:
`http://127.0.0.1:5290`, database `bingo_manual_preview_wom_clean_20260923` /
`manual_preview` at loopback `55529`. No preview restart, migration, seed,
provider call or database mutation was performed. The release-candidate commit
is `e652358446daa84157cecd6db5b7b8ca893dffc0` on
`codex/admin-wom-competitions`, pushed through the verified GitHub URL while the
checkout's local `origin` remained unchanged. PR #11 is open at
https://github.com/swiftpenguin578/BingoWebpage/pull/11, targeting `main`.
The final nonmutating publication checks confirmed the pushed branch ref,
GitHub `main` ref, no duplicate existing PR, a clean release-candidate commit,
staged whitespace/privacy/file-scope checks, and the recorded focused evidence.
The next permitted action is the user's PR review and release decision; no merge,
deployment or production mutation is implied.

## New planner handoff — 2026-09-24

The user explicitly requested a NEW planner task when the current full-suite
orchestrator returns. The orchestrator has now returned its final technical PASS;
the originating planner is handing off now. The user-run complete suites remain
pending and are not a prerequisite for creating the requested planner task.
Include exact source checkout/branch and dirty-work identity, actual test/review
results and evidence, manual approvals/waivers, unresolved blockers, preview/data
details, and the exact commit/deployment state and next authorized action. Do not
equate manual approval or passing tests with production deployment or permission
to publish. Preserve the current work until its release is handled.

Reading order: `AGENTS.md` (current roles/models/routing), this handoff,
`DELIVERY_PLAN.md` 4.2.1 and applicable release sections, the current remediation
`PLAN.md`/outcome, `TICKETS_2026_09_FOLLOWUP.md`, and
`MANUAL_TEST_CHECKLIST.md`'s current walkthrough disposition. `UI_PAGE_MATRIX.md`
owns visual approval; consult `UI_SYSTEM.md` for UI changes. Read relevant sections
of product/contracts/data/architecture documents only for the next assigned scope;
`README.md` owns commands. Old pending statuses superseded by explicit acceptance
must not reopen approved work. Do not ask the user to repeat the manual checklist.

Discussed follow-ups are for AFTER the current work reaches production, not
automatic implementation authorization or an agreed priority order:

- Admin UI AND backend/workflow overhaul, including WOM controls' visual polish.
  Preserve visual identity, but do not assume the legacy frontend, dependencies or
  accumulated restrictions must survive. Repeated patching/inventories have failed
  the user; they should not bear the burden of designing UX or discovering every
  missed state. Separate state-preview/HTML ideas were discussed, but no final
  overhaul design or implementation plan has been approved.
- Reassess Luck using EHB spent versus expected EHB to receive a drop, compared
  with current KC-based calculation. This is an evaluation idea, not an approved
  formula change.
- Missing gap below “Full-event KC · Drops credited to this tile” in tile-sidebar
  KC & Luck when multiple bosses are listed.
- Deferred IP/privacy work: minimize visitor IPs in Caddy error logs and consider
  other log retention/documentation. Do not claim no IPs are stored: access logging
  was not enabled in the inspected Caddyfile, but Caddy error logs had IP/request
  metadata, cookies inspected were redacted, and UFW/journal logs retained network
  metadata. Hosting-provider logging is separate; no changes were implemented.

Real update-all/scheduled WOM fetch verification remains deferred to the live
event by the user. Section 9 manual ranking checks were waived. Single-eligible-drop
description wording is already implemented and approved, not another open ticket.

## Authorized: full-suite remediation before commits — 2026-09-24

Workflow clarified by the user: `AGENTS.md` owns the planner → Terra/medium
orchestrator → Luna/max implementer → Sol/high reviewer policy. The orchestrator
may wait on workers and owns waking the next owner; the planner does not handle
routine handoffs. The new orchestrator task is `01a0d3bd-ef5c-7b90-bf38-3eae2c42da81`.

The user's complete solution run exposed one Integration compilation error and
four BrowserTests failures: 493 tests ran, 489 passed, four failed; Integration
never executed. Prior manual feature approval remains intact, but the complete
automated suite is not passing. A NEW Terra/medium orchestrator is assigned one
Luna/max implementer and one independent Sol/high remediation reviewer. Scope,
failure evidence, test-isolation requirements and routing are in
`/Users/christopher/.codex/visualizations/2026/09/17/01a0b085-75ee-79b0-b40b-bba65b669d74/full-suite-remediation-20260924/PLAN.md`.
Use the exact source checkout `/private/tmp/BingoWebpage-wom-managed-20260922`
on `codex/admin-wom-competitions`; preserve the running preview and its database.
The bounded remediation and its independent Sol/high review are complete with a
technical **PASS**. The final remediation patch SHA-256 is
`7adca716e8d36cc25cf80385eec721ea197e821f92cdabf6b70af5358087707b`; its
sorted NUL tree manifest SHA-256 is
`85a463a81ee8210e6a16221a20a3198ca4cc005c3a89a9aa5367b9afaf6999b2`.
Review and command evidence is under `/private/tmp/full-suite-remediation-20260924-luna/`.

Focused results: BrowserTests 130/130 before the named logging correction;
the corrected safe-logging test 1/1 afterward; Stats Pass 5 22/22; direct
original-reproduction group 24/24; Release build passed with 0 warnings/errors;
and final `git diff --check` passed. The formatter's completed exit 2 reported
accepted-baseline files only; its retry was blocked by sandbox Roslyn named-pipe
permissions. A post-change Stats rerun was blocked during disposable-container
startup, while its prior 22/22 result remains recorded. These are not product
failures and are not claimed as replacement passes.

At the user's direction, the complete `dotnet test Bingo.slnx --configuration
Release` and `node --test tests/Bingo.BrowserTests/*.test.js` runs are **user-run
gates** and remain unrun by this remediation; do not claim either as passing.
The preview/manual database was not restarted, migrated, seeded or mutated; no
external provider call, stage, commit, push, deployment, or UI re-review occurred.
The originating planner now creates the requested new-planner handoff. The next
release gate is for the user to run the deferred complete suites and report results.
The receiving planner should read this handoff, acknowledge readiness, then await
those results or the user's next instruction. No UI re-review, staging, commits or
publication is authorized.

## Final manual display corrections approved — 2026-09-23

The three user-requested corrections are implemented in
`/private/tmp/BingoWebpage-wom-managed-20260922`: boss KC `-1` sentinels display
as em dashes, boss team average gains display as whole numbers, and Stats uses
the actual shared Board/Drops/Leaderboards masthead (same event facts, player
metrics and conditional actions) aligned to its own 1640px content shell. The
user rejected the earlier structure-only Stats facts; that version is superseded. Stored KC, EHB, ranking/averaging semantics and local
event data are unchanged. Focused checks passed: 29 executable Razor-expression
cases for the unchanged numeric fixes. The actual-masthead correction passed
4 database-free model tests (Archived/Live/pre-Live facts, tied rankings and missing
activity), 68 Node checks (0 failed, 2 pre-existing skips), Release Web/Razor
compilation and diff checks. Extraction equivalence checks prove the original Board
masthead markup/conditionals and ranking logic are unchanged; both pages call the
same partial, and the remaining Board body is byte-identical. Independent UI review was **skipped at the
user's explicit request**, not passed. The user then explicitly approved the current
running corrections (“everything approved”), including the actual shared Stats
masthead. UI_PAGE_MATRIX.md records this page acceptance. The broader accepted,
waived and deferred walkthrough scope is recorded in MANUAL_TEST_CHECKLIST.md's
“Current walkthrough disposition — user acceptance, 2026-09-23”; that disposition
supersedes older pending statements for the scope it names.

The same local preview is running at `http://127.0.0.1:5290` (PID 54514), using
the verified Release build and `Success Complete Real`. Homepage HTTP 200 and
`/health/ready` Healthy; database identity remains
`bingo_manual_preview_wom_clean_20260923` / `manual_preview` at loopback 55529
(32 events). Official WOM provider, fake transport disabled and automatic activity
synchronization disabled were confirmed by the launcher. No database migrations
were needed. No provider request was triggered for these corrections.

Evidence and the isolated three-fix diff are under
`/private/tmp/sommerbingo-actual-masthead-20260923/`; original numeric-fix evidence
remains in `/private/tmp/sommerbingo-final-display-fixes-20260923/`. No packaging or
publication is authorized. This correction assignment is complete; no further edits,
review or restart are pending.

## Authorized: September Astra-review remediation — 2026-09-23

The user approved remediation of the additional Astra review findings F1–F8,
including inherited Danish keys, plus bounded investigation of its two verification
concerns. The assigned GPT-6 Luna/max implementer completed the bounded corrections
and focused checks. One fresh independent GPT-6 Sol/high reviewer passed the stable
correction diff with no residual named finding or direct consequence; it reused the
recorded focused execution evidence and did not rerun the tests. Original ticket-level
passes remain historical evidence, while this supplemental correction now has its
own technical **PASS**. Manual acceptance remains pending. SEP-08 ultrawide visual
verification remains pending because a usable local browser renderer was
unavailable; SEP-01's optional-validator concern showed no supported production
bypass. No packaging or publication is authorized.

Current assignment: `/Users/christopher/.codex/visualizations/2026/09/17/01a0b085-75ee-79b0-b40b-bba65b669d74/september-astra-review-20260923/REMEDIATION_BRIEF.md`.
Original findings are in adjacent `REVIEW.md` and must remain unchanged.
The correction outcome and source identity are recorded beside the brief in
`REMEDIATION_OUTCOME.md`; orchestrator task `01a0c9bd-2e6c-7633-b9a3-ec2e21ce53d1`
completed the independent-review handoff. The reviewed tracked diff SHA-256 is
`411c4f6d73588bcd9503469f65d9759a2d05a52d49811abe544325ed079a2d43`;
the combined source manifest SHA-256 is
`392368aaaabe91d278b0971cc4f41aae27b798fe579ddf9dbc1ecfb6b6b33eac`.
The next permitted action is user manual EN/DA inspection and a separate visual
check of SEP-08 ultrawide geometry; no packaging or publication is authorized.

## Authorized: September follow-up execution — 2026-09-22

All nine September follow-up tickets are implemented and have passed their
assigned independent technical reviews. SEP-04's tie-acknowledgement correction
was implemented by a fresh GPT-6 Astra/high worker at the user's request and
passed the original GPT-6 Sol/high reviewer's named recheck on 2026-09-23.
Every ticket remains **Awaiting manual review**; no manual acceptance, packaging,
or publication is inferred. The managed WOM feature and restored leaderboards
retain their separate pending manual acceptance. The next permitted action is
user manual inspection and release planning under the existing delivery gates.

The user authorized a visible Terra medium orchestrator to execute
`TICKETS_2026_09_FOLLOWUP.md` in the six batches recorded there, starting with
SEP-02 then SEP-03. Luna max implements; a fresh Sol high independently reviews
each stable batch. Passed batches await manual review while execution continues.
The orchestrator owns worker handoffs and reports only blockers, consequential
questions or full completion to the originating planner. SEP-02 remediation and
the same-reviewer named-only recheck by `/root/sep02_review` are complete with a
technical **PASS**; SEP-03 implementation, remediation and its same assigned
independent review/recheck are also complete with a technical **PASS**. Both
batches are now **Awaiting manual review** and manual acceptance is not inferred.
SEP-01's five named findings and focused checks are complete; its same assigned
independent Sol High reviewer recheck also passed technically. SEP-01 is now
**Awaiting manual review**; manual EN/DA acceptance remains pending and is not
inferred.
SEP-05 and SEP-07 implementation, focused checks, named SEP-07 pagination
remediation and the same assigned independent review/recheck are complete with
a technical **PASS**. This batch is **Awaiting manual review**; EN/DA visual
acceptance for the affected public event pages remains pending and is not
inferred.
SEP-08 and SEP-09 implementation, focused checks, and independent technical
review are complete with a technical **PASS**. Both are **Awaiting manual
review**; Stats wide/narrow and Board countdown EN/DA visual acceptance remain
pending. The Board countdown uses rendered event state on load/refresh only;
manual page acceptance is not inferred.
The development reset helper remains limited by its known PostgreSQL
foreign-key truncate failure. No new SEP-02 or SEP-03 reviewer dispatch is
permitted.
Use the existing checkout and protected baseline below. No packaging, production
mutation or publication is authorized. Prior WOM manual acceptance stays pending.

## SEP-02 fixed-hourly remediation — 2026-09-22

The initial fixed-hourly implementation received two named review findings:
legacy future due values could replay an already-consumed current slot, and
manual success/failure on a Live row without `ActualStartedAt` could manufacture
a rolling due time. The source correction, focused migration-state tests, and
current authority/checklist updates are complete. Domain slot tests pass 6/6;
focused synchronization passes 7/7, metric-cache/publication passes 7/7,
activity projection passes 2/2, and the Release IntegrationTests build passes
with 0 warnings/errors. The development reset helper remains limited by its
known PostgreSQL foreign-key truncate failure
(`DevelopmentScenarioSeeder.ResetAndSeedAsync`). The same-reviewer named-only
recheck by `/root/sep02_review` is complete with a technical **PASS**. SEP-02
remains **Awaiting manual review**; manual acceptance is not inferred.

## SEP-03 four-hour WOM update-all — 2026-09-22

The bounded implementation, named P1 remediation and same assigned independent
Sol High source review/recheck are complete with focused checks passing and a
technical **PASS**. SEP-03 is now **Awaiting manual review**; manual acceptance
is not inferred. The
implementation adds a durable update-all slot receipt keyed by remote
competition and paired fetch slot, fixed +3h45m/+4h schedule helpers, atomic
PostgreSQL claiming and expiry-to-ambiguous handling, protected-credential-safe
`POST /competitions/:id/update-all` acknowledgement handling, and the existing
management/manual-link advisory lock. A future pending receipt now rebinds only
its mutable management-version/source-generation lineage under that lock when
the same active managed link remains compatible; terminal receipts remain
immutable, and a deletion/recreate boundary cannot revive an old receipt. The
existing hourly synchronization worker/state is not gated by update-all
outcomes.

Focused evidence:

- `EventCompetitionUpdateAllSlotTests` — 4/4 (UTC boundary, midnight, DST,
  strict end-window and terminal receipt behavior).
- `Slice10Pass101WiseOldManTests` update-all client checks — 3/3 (official POST
  body, empty 202 acknowledgement, provider error redaction, ambiguous POST).
- `EventCompetitionManagementIntegrationTests` — 11/11, including two-context
  claim/restart, expired ambiguous claim, manual-link advisory-lock race,
  update-all failure followed by an independent paired hourly fetch, future
  receipt rebinding after management/source lineage changes with exactly one
  POST, and deletion/recreate plus competition-replacement skips.
- The two new future-receipt PostgreSQL regressions — 2/2.
- `dotnet build Bingo.slnx --configuration Release --no-restore --disable-build-servers`
  — passed with 0 warnings/errors; `git diff --check` — passed.

Schema is carried by `20260922163951_AddWiseOldManUpdateAllSlots` with its
designer and updated model snapshot; the remediation requires no additional
migration. The development reset helper limitation recorded for SEP-02 remains
unchanged; no production database, real WOM request, restart or publication
occurred. The separate F5 correction now rechecks the dispatch deadline and
event/link/end eligibility after shared limiter admission; its focused controlled
HTTP/PostgreSQL regressions passed. That supplemental correction awaits the
orchestrator's fresh named-finding review; manual acceptance remains pending.

## SEP-01 active account validation — 2026-09-22

Five named SEP-01 findings across the initial review and follow-up recheck have
been remediated; the same assigned independent Sol High reviewer recheck is
complete with a technical **PASS**. SEP-01 is **Awaiting manual review**; manual
EN/DA acceptance remains pending and is not inferred. Direct `SignupService`, `MyAccountsService` and
`AccountIdentityService` account-name writes now require a validator and fail
closed when one is absent; controlled test validators keep valid direct-service
fixtures explicit, while CSV remains outside this contract. Admin internal
participant creation and vacancy replacement now return the submitted form
with an explicit same-form confirmation on WOM operational failure; Cancel
clears the one-use token while retaining the account and answer inputs. The
add-participant form is forced visible when confirmation returns even if the
request URL no longer has `addParticipant=1`. Both Cancel buttons submit to a
server handler which clears its confirmation token and returns the form with
all entered account/answer values intact; JavaScript only enhances the
response. The single-use confirmation fingerprint includes the target team
for manual pre-formed entry and the target vacancy for replacement, in addition
to the existing actor/action/event/participant/name-set/version context.

The original validation contract remains narrow: a transient five-minute exact
normalized-name success cache over the existing WOM player lookup validates
non-CSV active account writes across My Accounts create/edit/reactivate,
onboarding, authenticated signup create/edit, Admin participant create/edit,
manual pre-formed entry, rejoin/restore and vacancy replacement. Known invalid
names always block; normal users are blocked on operational failure; only an
enabled Admin can explicitly confirm an operational failure. Existing EHB
fetch semantics are unchanged, raw successful fetches only prewarm the
validation cache, and CSV/saved-EHB/payment/notes/reorder/unlink paths remain
outside the contract. No migration, background scanner, real WOM call or
production database change was introduced.

Focused evidence on controlled PostgreSQL fixtures:

- Release web build — 0 warnings/errors.
- `Slice4AuthenticatedSignupIntegrationTests` — 38/38 on the preceding pass;
  the follow-up no-JS path has its own direct regression below.
- `WiseOldManAccountValidationTests` — 4/4, including target-team and
  target-vacancy token binding.
- Direct My Accounts/onboarding missing-validator tests — 2/2; direct internal
  participant missing-validator test — 1/1.
- `InternalParticipantOutageConfirmationAndCancelWorkWithoutJavaScript` — 1/1;
  `InternalReplacementOutageConfirmationRetainsTheOriginalForm`, including
  no-JavaScript cancellation — 1/1.
- `participant-add-dialog.test.js` — passed (Node exit 0).
- The prior focused SEP-01 baseline checks remain recorded below; no full suite
  was run for this remediation.

`git diff --check` passed after the remediation and handoff update. The technical
review boundary is complete; the next permitted action for SEP-01 is manual EN/DA
acceptance. No additional review is dispatched and manual acceptance is not
inferred.

## SEP-05 + SEP-07 technical handoff — 2026-09-22

Implementation and focused checks are complete on `/private/tmp/BingoWebpage-wom-managed-20260922`,
branch `codex/admin-wom-competitions`. The same assigned reviewer
`/root/sep05_sep07_review` completed the named SEP-07 live-pagination recheck
with a technical **PASS**. SEP-05 and SEP-07 are **Awaiting manual review**;
manual EN/DA acceptance for the affected public event pages remains pending
and is not inferred.

SEP-05 reuses existing account/event/submission unread state. Active Admin and
SuperAdmin accounts are eligible for the viewed public event's existing banner
and NEW markers without participant membership; exact-event reads and mutation
eligibility preserve account/event isolation, while `/current` remains
participant-scoped. The public shared banner root is no longer role-excluded;
the client targets the event ID already rendered by the page and clears stale
state on an exact-event 404. No Admin-layout banner or new notification state
was added.

SEP-07 carries both immutable `SubmittedAt` and arrival `ReviewedAt` through the
existing public feed projection. The event Board overview top-three, Drops
ordering/date groups and last-24-hours count use submission time. Live
reconciliation still detects new approval arrivals by review time, then
deduplicates and inserts/re-groups them by submission time. The live API keeps
its 100-row request cap and now pages with an offset; reconciliation fetches
enough rows for the requested rendered `dropCount`, then caps the merged cards
back to that window after submission-time ordering. A rank-110 late approval is
reachable in a 125-row window, while a rank-50 late approval does not expand a
25-row window. Other progress ranking/history clocks and announcement
NEW/arrival semantics are unchanged.

Focused evidence (controlled PostgreSQL fixtures): `DropAnnouncementPersistenceIntegrationTests`
and `PublicRecentDropValidity` / the new
`PublicRecentDropsUseSubmittedAtForOrderingAndLast24HoursAcrossProjectionAndHttp`
filter — 11/11 passed, including active Admin/SuperAdmin, revocation/disablement,
automatic banner claim, split banner/Drops acknowledgement, clear-all and
cross-user/event state, the viewed-event HTTP/banner route, API and Board/Drops
ordering, date groups, last-24-hour projection and API offset pagination.
Related Node tests
`drop-announcement.test.js`, `drop-announcement-races.test.js` and
`public-recent-drops-live.test.js` — 23/23 passed, including exact 25/125
window boundaries, late-approved older submissions, paged rank-110 retrieval,
deduplication, correct insertion and unchanged banner arrival targeting. The
focused Release integration test build passed; no full suite, manual visual
acceptance, staging, commit, publication or production mutation was performed.

## SEP-08 + SEP-09 technical handoff — 2026-09-22

Both UI corrections, focused checks, and independent technical review are
complete on `/private/tmp/BingoWebpage-wom-managed-20260922`, branch
`codex/admin-wom-competitions`, with a technical **PASS**. Manual EN/DA visual
acceptance remains pending; no screenshots or user acceptance are claimed.

SEP-08 adds the `public-stats-masthead` hook in `Stats.cshtml`. The loaded-after-
shared `stats-integration.css` computes a nonnegative inset from the viewport
and `--public-page-wide`, applies it only to the masthead intro and divider,
and reduces divider width by the same inset so its right endpoint stays at the
viewport edge. The masthead wrapper and artwork origin remain unchanged. Its
inset is zero at or below the existing 88rem content maximum, including the
existing narrow layouts.

SEP-09 renders `Starts in` for the pre-Live `Draft`, `SignupOpen` and
`SignupClosed` states, with the existing board-blue token for its badge and
dot. Live remains coral with the existing Live wording; ended states continue
to use the existing Ended wording and coral style. The initial accessible group
label follows the rendered status. Countdown JavaScript, hidden timer label,
timestamps, units, progress and its dynamic ARIA calculation are unchanged, so
an open page still updates only its hidden timer until refreshed.

Focused evidence:

- `dotnet build src/Bingo.Web/Bingo.Web.csproj --configuration Release --no-restore --disable-build-servers` — passed, 0 warnings/errors.
- Focused Node browser tests `public-countdown.test.js` and `stats-production.test.js` — 67 passed, 2 skipped, 0 failed. The added assertions cover EN/DA pre-Live copy/color and page-scoped Stats inset/right-edge geometry.
- `git diff --check` — passed.

No app restart, manual visual acceptance, staging, commit, push, deployment or
production mutation occurred.

The independent reviewer confirmed that the scoped CSS geometry, lifecycle
wording and colors, localization, and unchanged timer behavior match the ticket
contracts; `git diff --check` passed. The recorded Release build and focused
Node results remain the implementation evidence above. Both tickets are
**Awaiting manual review**; no technical or visual evidence is being inferred
beyond those results.

## SEP-06 implementation handoff — 2026-09-22

Implementation, focused checks, and the same assigned independent reviewer
recheck are complete with a technical **PASS**. The independent reviewer
`/root/sep06_review` passed after the named P1 correction; the scoped correction
diff SHA-256 was
`acea5c53f5a03ca1b64268148d8ad4e3124cdffa74562ca7fcafb10f38fca434`.
SEP-06 is **Awaiting manual review**; EN/DA UI acceptance remains pending and
is not inferred.

The SEP-06 implementation is in `/private/tmp/BingoWebpage-wom-managed-20260922`
on `codex/admin-wom-competitions`. The user explicitly authorized deriving
legacy blank/whitespace working descriptions; nonblank working/template text
remains manual, and approval-snapshot descriptions/modes are left untouched by
the migration. The additive migration flags only blank/whitespace `tile_templates`
and `board_tiles` descriptions as automatic. It does not inspect or rewrite
frozen approval snapshots.

The board editor now stores blank input as an empty automatic description and
keeps nonblank input manual; the shared formatter uses selected catalogue drops
and ordered requirements for draft preview and approval materialization. Approval
freezes rendered text and mode; public Board/Tile consumers continue using the
frozen copy, and correction discard restores the prior mode and text. Focused
formatter, edit/approval/correction, localization, direct public-consumer, and
predecessor-migration upgrade tests have been added.

Focused verification passed:

- `dotnet test tests/Bingo.Application.Tests/Bingo.Application.Tests.csproj --no-restore --configuration Release --filter FullyQualifiedName~TileDescriptionFormatterTests --disable-build-servers -m:1` — 4/4 passed.
- `dotnet test tests/Bingo.IntegrationTests/Bingo.IntegrationTests.csproj --no-restore --configuration Release --filter FullyQualifiedName~DerivedDescriptionMigrationMarksOnlyLegacyWorkingBlanksAndLeavesFrozenTextUntouched --disable-build-servers -m:1` — 1/1 passed.
- Focused `BoardApprovalTicketBatchIntegrationTests` selection for blank/manual edit, weighted-target approval, correction discard, and EN/DA placeholder — 4/4 passed against disposable PostgreSQL Testcontainers.
- Same-reviewer PostgreSQL publication/drawer and correction-discard recheck — 3/3 passed; published projection carries frozen description/mode to the Captain drawer, and correction discard clears automatic working/template text.
- `git diff --check` — passed.

The first attempts exposed and corrected migration namespace formatting and a
nested-await compile error. Ordinary sandbox test execution also hit named-pipe
and VSTest socket-bind permission denials; scoped elevated runs then compiled
all projects and passed the focused tests above. No staging, commit, push, app
restart, or user/production database mutation was performed. Manual EN/DA
acceptance remains the next gate; no visual acceptance is claimed.

## SEP-04 implementation handoff — 2026-09-23

Implementation, focused checks, independent review and the same reviewer's
named recheck are complete with a technical **PASS**. SEP-04 is **Awaiting manual
review**; no manual acceptance is inferred. This is the final authorized backlog
batch. The checkout remains
`/private/tmp/BingoWebpage-wom-managed-20260922` on
`codex/admin-wom-competitions`; earlier WOM/leaderboard/SEP-01–09 changes remain
preserved. No staging, commit, push, deployment, application restart, or
production database mutation occurred.

SEP-04 adds durable completion facts per event/team/tile/approval generation,
with qualifying contribution and submission provenance. The calculator uses
effective approved contributions and immutable `SubmittedAt`; approval,
reversal/rebalance, and corrected-publication transactions reconcile facts.
Public board reads project facts but never write them. Nullable
`CurrentScoreReachedAt` is the latest currently complete tile time, ordered
after effective full-board finish, lines, and tiles, and before EHB; rank
equality uses the same field. Future acknowledgments and finalization inputs /
official snapshots include it. The migration reconstructs only derivable
current active-generation facts for active finalized teams; legacy official
placements and prior finalization versions are left unchanged, with the new
field null where it was not historically recorded. This does not silently
rerank old official results.

Focused checks passed:

- Release Web build — 0 warnings, 0 errors.
- Release IntegrationTests build — 0 warnings, 0 errors.
- `PublicProgressCalculatorTests` — 14/14.
- Focused `SubmissionWorkflowTests` selection — 4/4, covering distinct
  submission/review times, rebalanced still-complete evidence, nine-to-eight
  reversal, multi-requirement tile time, and linked corrected-child provenance.
- Corrected-publication generation switch and injected fact-write rollback —
  2/2 against disposable PostgreSQL/Testcontainers databases.
- `C33FinalizationFreshnessTests` focused official-history/freshness selection
  — 3/3; the expanded multi-requirement migration backfill fixture was rerun
  separately and passed 1/1 against disposable PostgreSQL/Testcontainers.
- `git diff --check` — passed after the final authority/status updates.
- After the reviewer found a stale placement-tie acknowledgement key, the
  user-requested GPT-6 Astra/high remediator included current competitive
  inputs, including `CurrentScoreReachedAt`, in the deterministic tie key.
  Release IntegrationTests build passed with 0 warnings/errors; the focused
  disposable-PostgreSQL C33 selection passed 4/4, including the tie-break,
  return and renewed-acknowledgement regression; `git diff --check` passed.
  Two-file correction diff SHA-256:
  `38145658a7c26160278390b07efbe308c470af36fa4b3d0c4feaccf9994287a7`.
  The original `/root/sep04_review` reviewer passed this named recheck.

The broader suites were not rerun. SEP-06 also remains technical **PASS** with
manual EN/DA review pending. SEP-04's manual review has not been performed.

## Awaiting manual review: Admin-managed WOM competitions — 2026-09-22

User explicitly marked this feature **Awaiting manual review**. Implementation
and the same independent Sol High review/recheck are complete: **PASS**.
No further implementation, review dispatch or publication is active. The next
permitted action for this feature is user EN/DA manual inspection of the Admin
journey; record page acceptance only in `UI_PAGE_MATRIX.md`. The separate September follow-up backlog is now authorized as recorded above.

Checkout: `/private/tmp/BingoWebpage-wom-managed-20260922`.
Branch: `codex/admin-wom-competitions`.
Base/HEAD: `ae1a8605372a613e53d87b73a8e0237c84c1faca`.
The restored leaderboard patch SHA-256 is
`22b2950c8ea05ae30d708dadbf6588684bb776a7c03e1766cbae8aaaee033bf3`.
The reviewed 42-row WOM manifest SHA-256 is
`4cbc1dd21d9d6fbe7993f046d27fff4fe2043cf0c8ad280e466232d56b29a7be`.
This status-only update follows the reviewed snapshot and does not change
production code or represent another code-review pass.

Final review: `/Users/christopher/.codex/visualizations/2026/09/17/01a0b085-75ee-79b0-b40b-bba65b669d74/wom-independent-recheck-20260922.md`,
“Final named-only recheck” verdict PASS. Durable checks and baseline evidence:
`/Users/christopher/.codex/visualizations/2026/09/17/01a0b085-75ee-79b0-b40b-bba65b669d74/wom-baseline-evidence.md`.
Release build and focused checks passed as recorded there. The earlier 143/143
synchronization result was reused; a later timed-out attempt is not a new pass.
Optional provider-ID rename recognition remains omitted. No commit, staging,
push, deployment, restart, real WOM mutation or production database change occurred.
Leaderboard restoration does not renew its deferred review/manual acceptance.

Prior checkpoints below are historical evidence only; their dispatch/resume
instructions are superseded by this awaiting-manual-review status. Follow
[lean execution and planner handoff](DELIVERY_PLAN.md#421-lean-execution-and-planner-handoff).

## Resumed implementation verification — 2026-09-22

The authorized work resumed from the saved pause checkpoint. The corrected
`EventCompetitionManagementService.cs:262` status predicate now builds
successfully. The required post-fix checks completed on the unchanged stable
source delta:

- `dotnet build Bingo.slnx --configuration Release --no-restore --disable-build-servers` — passed, 0 warnings, 0 errors.
- `WiseOldManCompetitionRulesTests` — 5/5 passed.
- `Slice10Pass101WiseOldManTests` — 11/11 passed.
- `ManagedCompetitionUiTests` — 2/2 passed.
- `git diff --check` — passed.

The WOM manifest remains 36 rows with SHA-256
`12051e438bb7d75e9eb5c45aff6d75a34e0c290c1154679f1370a63007c59f41`.
No process is running. The next permitted action is to dispatch exactly one
fresh independent `gpt-5.6-sol` / `high` reviewer against this complete
checkout and its separate restored leaderboard baseline. Manual EN/DA visual
acceptance remains pending, provider-player-ID rename recognition remains
explicitly omitted, and no publication claim is authorized.

## Paused worker checkpoint — 2026-09-22

The user requested a graceful pause after the implementation pass. This worker
has stopped dispatching stages, review, remediation and new checks. The safe
checkout is `/private/tmp/BingoWebpage-wom-managed-20260922` on branch
`codex/admin-wom-competitions`, based on
`ae1a8605372a613e53d87b73a8e0237c84c1faca`. The restored leaderboard baseline
is the exact patch
`/private/tmp/bingo-boss-leaderboards-20260917/final-visual-corrections-with-safari-paint.patch`
with SHA-256
`22b2950c8ea05ae30d708dadbf6588684bb776a7c03e1766cbae8aaaee033bf3`.

The WOM and authority delta against the baseline-only materialization
`/private/tmp/BingoWebpage-wom-baseline-20260922` is recorded as the 36-row
manifest
`/Users/christopher/.codex/visualizations/2026/09/17/01a0b085-75ee-79b0-b40b-bba65b669d74/wom-implementation-manifest-20260922.txt`.
Its current SHA-256 is
`12051e438bb7d75e9eb5c45aff6d75a34e0c290c1154679f1370a63007c59f41`. The
manifest covers the scoped authority
updates, application/domain management contracts and rules, infrastructure
management service/client/configuration/migration, web worker/credential
protector/Admin Manage flow/name limits/localization, and focused tests. No
commit, stage, push, deployment, app restart, real WOM mutation or user database
operation was performed.

Baseline restoration and source-completeness validation completed: patch
application, Release restore/build, restored leaderboard Node harness, and the
focused PostgreSQL leaderboard route/projection test passed. The WOM pass also
completed these checks before the final source edits: Release solution build
(0 warnings/errors); `WiseOldManCompetitionRulesTests` 5/5; managed WOM
adapter/credential integration slice 11/11; `ManagedCompetitionUiTests` 2/2;
`EventCreationUiTests` 27/27; `DateTimePresentationTests` 2/2; the focused
PostgreSQL schedule mismatch test 1/1; the focused finalized-roster Live-boundary
test 12/12; and `Slice10Pass102CompetitionSynchronizationTests` 143/143. The
complete synchronization slice took 5m07s and finished successfully. No process
is running now.

Immediately before the pause, the implementation added the final active-state
recovery and draft-reopen fence, then one release build exposed an EF expression
tree error for the new status filter (`CS8122` at
`EventCompetitionManagementService.cs:262`). The expression was corrected to
explicit comparisons, but the build was not rerun after that correction. Thus
the exact next permitted step on resume is to rerun the Release build and the
direct management/rules/UI checks, then refresh the manifest hash and evidence.
Only after those checks pass may this worker dispatch the single fresh Sol High
independent reviewer. The reviewer has not been dispatched. Manual visual
acceptance of the Admin Manage EN/DA journey remains pending. Provider-player
ID rename recognition is explicitly omitted and must remain disclosed.

Worker ownership remains `/root/wom_competitions_orchestrator` for reconciliation
and next-step routing; this worker owns the isolated implementation checkout and
must resume only after the orchestrator/user lifts the pause.

## Active planner handoff — 2026-09-17

This entry supersedes the historical active assignments below. User authorized
the boss KC leaderboard/MVP slice on 2026-09-17, readiness first, then bounded
implementation if readiness clears. No publication or database repair is authorized.
Follow [lean execution](DELIVERY_PLAN.md#421-lean-execution-and-planner-handoff)
and the ACTIVE temporary Sol/Luna workflow in AGENTS.md. The user reaffirmed that
this workflow supersedes the retained Astra defaults; the initial handoff's claim
that Astra defaults were reinstated was incorrect and is withdrawn.

- Working checkout: `/private/tmp/BingoWebpage-fix-wiki-image-fetch`, branch
  `codex/fix-wiki-image-fetch`, HEAD `ae1a8605372a613e53d87b73a8e0237c84c1faca`.
  Clean before this handoff documentation edit. The e420 worktree is an older
  detached checkout; `/private/tmp/BingoWebpage-drop-announcements` is no longer
  a valid Git checkout. Do not implement in either.
- PR #9 and PR #10 are merged and deployed. Current production merge is
  `68b16242a63497fe1b0677bc48c396d9dfb1fc79`; PR #10 fixes the actual Wiki image
  HttpRequestMessage to prefer HTTP/2 with HTTP/1 fallback and extends integration
  step timeout from 30 to 45 minutes. PR/main CI passed; production deployment
  run `35252206190` succeeded, public health returned 200. Image digest:
  `sha256:c532b9b0820df9910cb4358b5c91c874bedbcac7349e64981209bf1db620a14c`.
  Evidence: `/private/tmp/bingo-http2-deploy-20260917/` and
  `/private/tmp/bingo-wiki-http2-fix-20260917/`. No further deployment is pending.
- Latest completed task: fix local HTTPS7131 missing-column error
  (`events.announcement_generation`). Current build is now connected to the
  previous preview database `bingo_stats_manual` at loopback55519. Actual homepage
  and readiness both returned 200; no missing-column exception in the new log.
  App PID18909 / launcher18906 at verification (recheck identity before stopping).
  Reusable launcher:
  `/private/tmp/bingo-local-schema-repair-20260917/run-https-preview.sh`.
  It reads private allowlisted existing settings; do not print credentials.
  Development admin bootstrap and automatic WOM synchronization are disabled.
  No migration, seed or repair was performed to restore this preview.
- The old default localhost5432 database `bingo` is preserved. Backup and exact
  diagnosis: `/private/tmp/bingo-local-schema-repair-20260917/handoff.md`.
  It has 53 migrations through AddEventQuarantine; migration preflight rejected
  12 drop snapshots attached to manual objectives in two development fixtures,
  including a Live/Published approval. No migration applied and no historical
  rows were deleted. Do not bypass guards or repair that database without a new
  authorized scope. Preview DB has 66 migrations through SaveStatsGuidanceAndArtwork;
  final PopulateRetainedCatalogue data migration remains unapplied. Homepage
  success does not establish full-page acceptance or full migration completion.
- User's normal repository `/Users/christopher/Documents/BingoWebpage` is on
  `feature/boss-artwork` and has unrelated dirty documentation, TeamBoard markup,
  launchSettings, artwork guide/assets and tmp work. Preserve it. Those recent
  artwork changes are NOT all merged. Do not checkout/reset/stash or import them.
- Catalogue images resolved by user confirmation on 2026-09-17: the user updated
  the catalogue URLs and reports that every item now has an image. The previous
  recovery cached 318 of 376 images; its 58 HTTP/2 404 results are historical
  evidence, not a current outstanding list. Report:
  `/private/tmp/bingo-pr9-image-recovery-20260917/live-report.json`.
  This is user-confirmed image coverage; no fresh automated download/cache check
  was run, and repository catalogue data was not synchronized by the planner.
  No further image investigation is assigned.
- Active slice: boss KC leaderboards and EHB/Drop EHB MVP display, as approved in
  PRODUCT_REQUIREMENTS.md section 16.1 and DELIVERY_PLAN.md section 19. Readiness
  review by `/root/boss_leaderboards_readiness` (Sol High) passed; report:
  `/private/tmp/bingo-boss-leaderboards-20260917/readiness.md`. Planner reconciled
  the existing standings metric consumer and cache/route safeguards in section 19.
  Luna Max implementation and named remediation are complete. Independent Sol High
  reviewer `/root/boss_leaderboards_review` reports final PASS with no remaining
  technical findings. Report: `/private/tmp/bingo-boss-leaderboards-20260917/review.md`.
  Reviewed patch SHA-256: `8bc192ad83ea79b31c009be18738801933c56016f99a3322a2422810388a902b`.
  Evidence: `/private/tmp/bingo-boss-leaderboards-20260917/remediation-evidence.md`.
  PostgreSQL/HTTP BossLeaderboard fixture 1/1, affected Node harness, Web Release
  (0 warnings/errors), scoped four-project formatting and diff checks passed.
  Whole-solution formatting failed on unrelated baseline test-project compilation;
  it is not claimed passed. Rank localization is included. User visual/keyboard
  acceptance remains pending in UI_PAGE_MATRIX.md; no browser inspection performed.
  User visual feedback now requires section 19's named presentation/interaction
  corrections: dropdown close/options, green signed gains/MVP, no freshness text or
  drop arrows, late wrapping with Metric above left-aligned tabs, muted-blue dark
  headers, and in-place metric switching with preserved position/history. Prior
  technical PASS applies to unchanged computation/cache logic; corrections and same
  reviewer recheck are pending. `/root/boss_leaderboards_implementation` owns this
  bounded continuation; `/root/boss_leaderboards_review` remains its reviewer.
  User added Danish boss Gained/Start/End localization (Opnået/Start/Slut) and removal
  of redundant EHB wording from nested EHB headers only; worker notified and these
  are included in the same correction delta. Drop EHB headings stay unchanged.
  On 2026-09-18 user chose ink for light-mode METRIC trigger text/chevron normally,
  blue when open/hovered/keyboard-focused, replacing the earlier always-blue request.
  Worker notified; included in the same pending visual correction pass.
  The subsequent visual correction pass is now complete and stopped. Implementer
  reports trigger/outside dismissal fixed, including after metric replacement, plus
  all named styling/localization/status/responsive and in-place switching corrections.
  Latest passing worker evidence: `/private/tmp/bingo-boss-leaderboards-20260917/visual-corrections-evidence.md`.
  Correction patch SHA-256: `0d15feb6e7b91b34b8729298b922f3a72bc458382583397f1ecc9a558e8b1feb`;
  full patch SHA-256: `9ec339a4521c0793381b397961a1667bc864c1aa7b447f02d15b7d8513ae02bf`.
  Affected Node harness/syntax, PostgreSQL/HTTP fixture 1/1, Web Release 0 warnings/errors,
  scoped formatting and diff checks passed per worker. These new corrections have NOT
  received independent review or user visual acceptance. No reviewer was dispatched.
  Targeted Safari selector inspection was subsequently authorized on 2026-09-18.
  Served JS/CSS matched the checkout. Safari 26.5.2 closes the selector logically
  (hidden/display:none/zero geometry and absent options in accessibility tree), but
  leaves its pixels painted. A page-only scoped CSS experiment keeping native
  details content renderable and the closed menu grid invisible/noninteractive
  cleared trigger, outside-click and Escape ghosts with original scrolling restored.
  Removing the details-content rule reproduced the failure. Evidence:
  `/private/tmp/bingo-boss-leaderboards-20260917/safari-live-inspection.md` and
  `selector-dismissal-diagnosis.md` in the same directory. Production correction
  is assigned to the existing Luna Max implementer; no JS or shared-menu redesign.
  Keyboard/replacement and other browser validation remain pending. Broader review
  remains deferred at the user's request; this diagnostic is not a review pass.
  No packaging/publication or automatic minor-change dispatch is authorized.
  User requested build/run commands and received the existing database-safe launcher;
  the older port7131 listener was verified as this checkout's Bingo.Web PID18909
  before providing a graceful stop command. Whether the user restarted successfully
  has not been verified; do not treat prior runtime identity as current.
- Deferred until this slice is visually approved: objective wording (one eligible
  drop: "Collect X [drop name]"; multiple eligible drops in a multi-objective breakdown:
  eligible boss wording, with combined sources sharing the objective target), and
  missing gap beneath the tile KC/Luck description before multiple boss headings.
  Neither minor correction is part of the current worker assignment. Manual EHB
  restriction to custom challenges was traced to C19; restoration is not requested.
- No old image-recovery, deployment or runtime worker is active. Do not revive them.

Planner constraints: orchestrate; do not implement production code. No browser/CUA
inspection. Preserve databases, approvals and existing dirty work. Keep corrections
bounded and reuse evidence; no broad re-review, repeated suite or periodic polling.
Worker briefs must explicitly require completion, blocker and handoff callbacks to
the NEW planner's task ID using send_message_to_thread so an idle planner wakes;
do not keep targeting the old planner `01a0a975-da3d-7cf0-a6dc-8b5a07b77688`.
User wants dispatch-and-stop, with concise updates when workers report back.

Current workflow: Luna Max implementation/remediation and focused checks, then
one independent Sol High review after implementation stops. The implementer may
dispatch that one reviewer under the existing direct routing policy; corrections
return to the same reviewer. Astra worker assignments require a concrete justified
escalation. No separate routine verifier or manager pass. Set model/reasoning
explicitly with bounded context; every worker sends the planner an end-of-turn
callback, including review handoffs. Retained Astra defaults are inactive.

Next permitted action: user explicitly authorized an immediate narrow Sol High diagnosis
of the still-broken METRIC dismissal only. `/root/boss_leaderboards_review` is inspecting
cause and smallest fix, with report at
`/private/tmp/bingo-boss-leaderboards-20260917/selector-dismissal-diagnosis.md`.
Read-only local static-asset checks may distinguish served code from checkout; no
browser/CUA, preview restart or user-data mutation. This is NOT resumption of the broader
correction review, which remains deferred. Reviewer reports diagnosis to planner first;
no automatic broad review or acceptance. Visual/keyboard acceptance remains pending.
Minor changes remain
deferred until this slice's approval. Keep production and both databases untouched;
no automatic preview restart or periodic wait/poll. Planner callback task remains
`01a0b085-75ee-79b0-b40b-bba65b669d74`.

## Active: Package and push reviewed corrections for PR #9 CI — 2026-09-16

Assignment checkout `/private/tmp/BingoWebpage-drop-announcements`, branch
`drop-announcements`. Preserve all existing dirty/untracked work. The saved project
worktree is not this assignment checkout. Follow
[lean execution and planner handoff](DELIVERY_PLAN.md#421-lean-execution-and-planner-handoff).

### Current user assignment — 2026-09-16

The user authorized packaging, committing and pushing all pending accepted work
to existing draft [PR #9](https://github.com/swiftpenguin578/BingoWebpage/pull/9)
for CI. Packaging inventory is exact: all 37 pending paths are included, including
the retained-catalogue test helper; 16 CI-fixture and 14 provider manifest hashes
match the reviewed evidence. The remote branch matched starting `6acdff9` before
packaging. CI fixtures are committed as `4a43f2c`; the four user-approved UI
corrections and their approval record are committed as `28c7445`. The provider
protections and associated documentation are packaged with this status update.
No accepted production contents were changed during packaging. Scoped diff checks
pass. Evidence: `/private/tmp/bingo-pr9-package-20260916/`.

Reused verification: CI remediation full integration **886/886 in Debug** and
original failing cases **21/21**; independent review PASS. UI source review PASS,
with user acceptance owned by UI_PAGE_MATRIX.md. Provider final review PASS with
no findings; image **8/8**, importer **1/1**, price/WOM **37/37**, affected Node
**85 passed / 2 optional skips**, Web Release **0 warnings / 0 errors**. These do
not establish a full Release CI pass. Implementation evidence remains under
`/private/tmp/bingo-pr9-ci-remediation-20260916/`,
`/private/tmp/bingo-minor-ui-20260916/`, and
`/private/tmp/bingo-provider-load-20260916/`.

The packager's remaining authorized publication step is a normal non-force push,
remote-HEAD confirmation, and one bounded read of the new CI run, reported to the
root callback. PR #9 stays draft. Full Release PR CI is the next acceptance gate;
no automatic CI remediation, merge, deployment, app restart or database change is
authorized. This current handoff supersedes earlier worker ownership and publication
holds below; all implementation reviews are complete with no remaining findings.

The user explicitly accepted all four minor UI corrections and authorized proceeding
with the queued Wiki/API work. UI_PAGE_MATRIX.md records the scoped visual approval.
`/root/provider_load_corrections` (Luna Max) now owns the observed image-cache bypass,
failed-download retry/hotlink, Wiki price/importer cooldown and WOM duplicate-lookup
corrections detailed below. Preserve all existing approved dirty UI/CI-remediation
work. Evidence: `/private/tmp/bingo-provider-load-20260916/`. Mocked request-count /
timing tests and affected Release build precede one fresh independent Sol High review.
No browser inspection, real provider load tests, app restart, DB change or publication
is authorized for this pass. Callbacks at handoffs/questions replace active polling.
Provider implementation reached its stable handoff: Web Release build 0 warnings/errors,
image-cache tests 4/4, price/WOM tests 37/37 and scoped format/diff passed. Initial
Node lookup failed on PATH; root verified the existing bundled Node v24.19.0 at
`/Users/christopher/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/bin/node`
and the worker completed affected Node checks: 66+19 passed, 2 optional skips, 0 failed.
This is a resolved runtime-location issue, not absent Node or an accepted test skip.
Before dispatching review, root reconciled the handoff's image policy: four concurrent
downloads / 100ms starts was more aggressive than the existing 500ms sync pace. The
same worker now owns one bounded adjustment to one outbound image download in flight
with at least 500ms between starts; warm disk-cache hits remain unblocked. Require a
focused cross-key concurrency/pacing check and refreshed image evidence; reuse other
passing checks. That bounded adjustment is now complete: image tests 5/5 passed,
including single-flight/spacing, warm-hit bypass and canceled-waiter safety. Final
Web Release build has zero warnings/errors and scoped format/diff checks pass; other
provider/Node results were reused unchanged. Root dispatched one fresh Sol High
`/root/provider_load_review` after implementation stopped. Review requires three named
corrections, already routed to `/root/provider_load_corrections`: F1 image Retry-After
is URL-local and different keys bypass the provider pause; F2 canceled sole/all waiters
can retain completed download tasks and replay stale faults; F3 the importer's sixth
429/503 throws before retaining that response's cooldown for subsequent instances.
Price cooldown and WOM admission corrections passed source review against executable
evidence. Server-side Stats/announcement URL mapping was source-reviewed; Node harnesses
do not themselves execute that server mapping. Same implementer fixes these findings
with focused tests, then the same reviewer rechecks only named corrections/direct
consequences. Report: `/private/tmp/bingo-provider-load-20260916/review.md`.
Latest bounded recheck: F2 and F3 passed. F1a identified that ordinary image failures
without Retry-After also paused unrelated cold images. The same implementer has now
restricted the global pause to valid positive provider Retry-After values; ordinary
failures retain only their per-key negative cache. Image tests pass 8/8, Web Release
build has 0 warnings/errors, and affected format/diff checks pass. The same reviewer
completed the F1a-only recheck: PASS, no remaining findings. All 14 manifest hashes
match. Accepted F2/F3, importer 1/1, price/WOM 37/37, and Node 85 passed plus 2
optional skips are reused. Evidence and the final review are in the provider
directory above. Provider corrections are implemented and independently reviewed;
the next permitted action is planner delivery reconciliation. Packaging/publication
and preview restart have not occurred; full Release CI has not been rerun.
No publication or restart occurred.

The user supplied the requested minor-change list; `/root/minor_ui_corrections`
(Luna Max) now owns exactly four surgical changes:
1. Stats KEEPS ON DROPPING has no image/placeholder/background-artwork box when
   empty or lacking a usable item image; preserve valid artwork and text styling.
2. On Signup and Signups, give the first stacked schedule block the same left
   divider/inset as subsequent blocks; preserve existing desktop composition.
3. Event overview: the last team card retains a trailing vertical divider unless
   it occupies the rightmost grid column; preserve row separators and responsive
   3/2/1-column behavior (user examples: 8 teams versus 3 teams at 3 columns).
4. Add a literal WIP badge to the shared Stats navigation entry, identical in
   styling/placement to the existing Drops NEW badge. Preserve Drops badge behavior.
   This marks the unfinished Danish support; translation work is not in this pass.
Current user screenshots define the corrections; browser inspection remains forbidden.
Worker runs only applicable scoped CSS/markup/JS checks, then one fresh Sol High
source review. User supplies visual acceptance. Evidence is assigned under
`/private/tmp/bingo-minor-ui-20260916/`. Preserve the reviewed, uncommitted CI test
fixes. Wiki image/API corrections remain queued after this small UI pass to avoid
overlapping source changes. No packaging or PR update until these edits are settled.
All four minor UI corrections are implemented. Evidence:
`/private/tmp/bingo-minor-ui-20260916/evidence.md`. Owned files are Stats JS and its
Node test, shared public CSS, Board Razor and shared layout Razor. Focused Node
checks: 64 passed, 0 failed, 2 optional PostgreSQL skips; scoped source/cascade checks
and diff check passed. Root dispatched fresh `/root/minor_ui_review` (Sol High)
after implementation stopped because the implementer could not spawn it. Independent
review found two Stats direct consequences and returned them to the same implementer:
no-art states still reserve about 28% of text width, and `finishSettingsSave()` can
re-enable Adjust artwork after a missing/broken image. Signup/Signups divider,
mission-grid trailing divider and WIP badge passed source review. Luna owns these
two named corrections; the same Sol reviewer rechecks. Report:
`/private/tmp/bingo-minor-ui-20260916/review.md`. User visual acceptance remains
pending. Both named corrections are now implemented: no-art text width is released
and valid artwork restores the existing cascade; shared eligibility keeps Adjust
artwork disabled for missing/broken images through guidance saves. Updated Node
results: 65 passed, 0 failed, 2 optional skips (67 total); source/diff checks passed.
The same Sol reviewer completed the two-finding recheck: TECHNICAL SOURCE PASS,
with 2/2 independently rerun focused Node cases passing. All four corrections now
have source acceptance; user visual acceptance remains pending. No UI worker remains
assigned further work, and nothing has been committed/pushed. No browser inspection.
The user explicitly authorized a preview restart:
root built Bingo.Web Release (zero warnings/errors), replaced prior PID 39460 with
PID 93198 on `http://127.0.0.1:5189` using the unchanged saved command.sh environment,
and verified `/health/ready` returns HTTP 200. Runtime session 85846; logs:
`/private/tmp/bingo-ui-preview-{build,runtime}-20260916.log`. No reset, seed or migration
command was run. The subsequent Stats JS corrections and source recheck are complete.

PR #9 is open as a draft: https://github.com/swiftpenguin578/BingoWebpage/pull/9.
The user authorized push/PR creation; `drop-announcements` was pushed at
`6acdff9e0eff0a2675a0504528e57c9b23dc1238`. Complete CI run `35125849800` failed:
integration 865 passed / 21 failed / 886 total. Formatting, build, Domain 248/248,
Application 89/89 and Browser 116/116 passed. The integration wrapper step says
success because it records the exit code; the final test gate correctly failed.
Full log and exact failure list: `/private/tmp/bingo-pr9-ci-35125849800/`.

`/root/catalogue_migration` (Luna Max) owns the bounded correction: 19 historical
migration cases hit the new catalogue identity guard; 2 late-introduction theory
cases compare timestamps differing by 1–2 .NET ticks after PostgreSQL storage.
Distinguish incomplete fixtures from a real retained-upgrade defect before changing
the migration contract; preserve identity, rollback and historical test assertions.
The user additionally requested checking tests for the same GitHub/PostgreSQL
microsecond precision pattern. Limit that scan to vulnerable timestamp generation /
database round-trip equality, with deterministic precision fixes rather than broad
tolerances or application timing changes.

Focused remediation exposed one historical fixture identity discrepancy: the July
WOM seed names slug/key `nightmare` as `Nightmare`; both origin/main and the reviewed
catalogue already use `The Nightmare`. Clean bootstrap applies that catalogue after
migrations. Planner authorized only a fixture preparation correction for the exact
legacy slug/name/key combination, preserving row identity/associations and all other
fields. Production matching and historical seed migrations remain unchanged; arbitrary
identity mismatches must still fail atomically. No production row was inspected.

Worker must cover every reported failure and any confirmed precision corrections,
then run the complete integration project once to completion. Evidence belongs in
`/private/tmp/bingo-pr9-ci-remediation-20260916/`. The same independent Sol High
reviewer `/root/catalogue_migration_review` reviews only the completed correction and
direct consequences afterward. End-of-turn callbacks wake the planner; no polling.
Latest user stop boundary: notify the user when the failed-test worker finishes,
stating full integration results and whether independent review has passed. The user
wants to discuss minor changes then. Hold the queued image/API pass and further
packaging/PR updates for that discussion; continue the current test correction/review.
The failed-test worker is now FINISHED: original 21/21 failures pass, the dedicated
catalogue/pre-live class passes 5/5, and the complete integration project passes
886/886 with final TRX and exit 0. Build has zero warnings/errors; scoped format and
diff checks pass. Evidence: `checks.md`, `failure-to-fix-coverage.md`,
`timestamp-scan-summary.md`, `complete-delta.md`, `hash-manifest.tsv`, and `full/complete.trx`
under `/private/tmp/bingo-pr9-ci-remediation-20260916/`. The precision scan found no
additional confirmed vulnerable cases beyond the corrected introducedAt fixture.
Root notified the user; the same Sol reviewer has now PASSED the stable correction.
Report: `/private/tmp/bingo-pr9-ci-remediation-20260916/review.md`. The reviewer checked
all 16 worker-source hashes, the 18 prerequisite call sites covering 19 migration
failures, the exact legacy Nightmare fixture repair and the two-case timestamp fix.
No weakened target assertions or production guard/seed/runtime changes were found.
The correction checks used default Debug configuration as recorded; these are local
results, not a new Release PR CI result. No commit/push occurred for this correction.
Current stop: await the user's minor-change discussion before queued image/API work
or further packaging/PR updates. No worker remains assigned more remediation.
No merge/deployment is authorized. Earlier PASS records below describe prior bounded
reviews, not a claim that this newly exposed CI gap is resolved.

**ACTIVE temporary workflow: Luna Max implementation/remediation and own focused
checks, followed by one independent Sol High review after implementation is complete.
Astra only for a concrete escalation. The previous Astra workflow and model defaults
are CURRENTLY SUPERSEDED, retained in AGENTS.md and TICKETS.md for reference only.**
No separate routine verifier; reuse passing evidence and recheck named corrections.
Applicable final release gates remain required. This policy does not restart tickets.

User requested: a quick xK display for GP values below one million, resolving the
known release-check blockers, and a full uncommitted diff review, especially API
correctness. Publication was initially held while production GP population was
resolved; later explicit authorization allowed the completed package push and PR.
The user subsequently approved implementing and testing a bounded one-time catalogue
GP data migration through the existing deployment path, including all 68 already
verified WOM boss/mode mappings. Implementation by `/root/catalogue_migration`
(Luna Max) and independent review by `/root/catalogue_migration_review` (Sol High)
are COMPLETE/PASS. Migration `20260916100000_PopulateRetainedCatalogue` freezes the
195 API-priced items, 116 Untradeable-zero items and 68 WOM mappings in compiled
payload data. It fills eligible missing fields with atomic system audits/version
updates, preserving configured/manual/newer metadata, extra records, rates and frozen
event/submission/Luck history. Clean bootstrap retains its existing snapshot path.

Final evidence: 5/5 disposable PostgreSQL migration cases, 2/2 existing manual/scheduled
outage start cases, zero-warning/error Release integration-project build, scoped
format/diff checks and EF SQL generation passed. The one review finding was a test
gap, now closed: a persisted pre-migration SignupClosed event with its owned signup,
character, finalized draft and published board starts through the normal lifecycle
after migration, preserving identities and capturing API fallback and untradeable
zero prices. The reviewer rechecked that named correction and all six manifest hashes;
no remaining findings. No broad suite or browser run was added.

Evidence and final review: `/private/tmp/bingo-catalogue-migration-20260916/implementation-evidence.json`
and `/private/tmp/bingo-catalogue-migration-20260916/review.md`. Final TRX files reside
in the checkout's `tests/Bingo.IntegrationTests/TestResults/`; final logs give full paths.
Root handled reviewer dispatch/recheck routing because worker tools were unavailable,
not implementation or source review. Neither worker remains assigned further work.
The earlier 203-path packaging inventory was refreshed after the catalogue migration.
The complete candidate is 208 paths: 191 application/runtime/test/prototype/reference
paths, 4 retained-catalogue population paths, and 13 authority/workflow documentation
paths. The full integration release gate remains outstanding.
Latest user workflow correction: collaboration subagents remain appropriate; the
briefly proposed visible-task requirement is withdrawn. AGENTS.md, TICKETS.md and
DELIVERY_PLAN.md now specify dispatch then end the turn, with no scheduled polling.
Detailed findings/rechecks stay between implementer and reviewer. Latest refinement:
every worker sends one brief end-of-turn update, including review/recheck handoffs,
to originating task `01a0a975-da3d-7cf0-a6dc-8b5a07b77688` through
`send_message_to_thread`. Genuine questions/blockers also use that route. Handle
informational updates briefly, then end the turn again; only reconcile completion
after the independent reviewer passes. The workflow-document update is complete.
Next bounded release correction requested by the user's Wiki-load concern: make
Wiki image delivery consistently use the existing persistent same-origin cache.
Known gaps: Stats/announcement item URLs can bypass it; image endpoint failures
redirect clients to the Wiki; unsuccessful downloads have no shared cooldown.
This pass is now dispatched after completed CI remediation and user-approved UI fixes.
Preserve artwork/UI and original source URLs; use cached disk files, shared miss
deduplication and bounded download pacing, failure cooldowns/Retry-After handling,
and a truthful contact-bearing User-Agent. Remove direct-Wiki error fallback.
Verify affected price/image clients have bounded calls/retries using mocked request
counts, not live provider stress. Existing price client already uses bulk hourly
responses, serialized access, 5-minute successful caching and 30-second failure
caching; inspect only relevant rate-limit/retry gaps, not a broad integration rewrite.
Production Compose already mounts the image cache as a persistent volume. No browser
inspection, live scraping, production changes or wholesale image re-download is
authorized. Use Luna implementation/focused checks then one Sol review for this pass.
The user's follow-up requested checking other outbound APIs too. Bounded source
inspection confirmed WOM's shared singleton limiter (one in-flight request, reserve
of 3, provider rate headers, rate-limit pause, one-minute fail-closed pause), 5-minute
player-success cache, 2-hour normal competition cycles with bounded retries and DB
leases, and bundled KC/EHB metrics. Page projections use stored observations. Discord
requests occur in the OAuth callback rather than a recurring polling loop.
Named follow-ups for the queued provider-load pass: Wiki price retries ignore
Retry-After (fixed 500ms transient retry, 30s failure cache); the manual Wiki importer
caps requested Retry-After at 30s and its 250ms/six-attempt pacing is instance-scoped;
concurrent WOM lookups can both miss the player cache before serialized admission
because it is not rechecked after admission. Preserve existing WOM rate controls,
avoid redundant identical lookups, and honor provider-requested cooldowns in supported
header forms. Prove the corrections with mocked request counts/timing, not real calls.
The user authorized packaging/local commits of ALL intended tracked and untracked
changes, emphasizing that omissions could break integrated behavior. The broad
application/runtime/test/prototype/reference group is committed as `a9c6413`
(`Package reviewed application features and fixtures`), and the four-path bounded
catalogue population group is committed as `eac54db` (`Add retained catalogue
population migration`). The 13-path documentation group, including this status
update, was committed as `6acdff9`. Inventory and content-hash
evidence are under `/private/tmp/bingo-complete-packaging-20260916/`; prototypes,
reference assets and TICKETS are included. Push and PR creation are complete; merge
and production application remain prohibited. Preserve prior UI approvals and leave the explicitly
deferred sticky Luck comparison gap alone. No browser inspection was performed.

GP formatting now uses K below one million, retaining raw GP below 1,000. Focused
formatter checks and the corrected navigation fixture passed; the strict solution
Release build passed with zero warnings/errors. The full independent review then
covered all 189 tracked/untracked paths against a stable, hash-checked candidate.
Reports: `/private/tmp/bingo-release-review-20260916/{api,data,web}-review.md`.
API review found no runtime defect. Required corrections are announcement publication
metadata, discard of empty approved events, Drops subscription/reconnect reconciliation,
removal of reversed feed entries, and stale announcement response/claim fencing.

Both bounded implementers have returned FINAL. Backend corrections include the two
data findings and existing recovery translation for wrapped board conflicts and the
newly earlier submission event-lock conflict. All 92 distinct focused PostgreSQL/HTTP
cases have passing evidence (91 initial passes plus a four-case bounded recheck).
Client corrections cover all three web findings, including queuing invalidations
until pending mutation handlers settle. Its new public-validity HTTP case and both
Admin shell checks passed. Final full Node gate: 104 passed, two optional
PostgreSQL-DTO renderer cases skipped, zero failures. Domain 248/248 and Application
89/89 passed; EF reports no pending model changes. Strict Release build is clean.
Reports, exact implementation deltas and TRX files live under the review directory.

Mechanical formatting, whole-solution format verification and strict Release build
passed. Independent bounded rechecks closed every source finding after implementation
finished. One additional historical-question fixture was corrected without changing
assertions; its named PostgreSQL case passed 1/1. Final dispositions are in
`data-recheck.md`, `web-recheck.md` and `api-recheck.md` in the review directory.
No broader repeat review or browser inspection occurred.
The initial full integration run was interrupted after recording named failures;
it has no final TRX and is not a complete suite result. The full integration release
gate is enforced by PR CI against the committed candidate; the interrupted local run
is never a substitute. The refreshed 208-path candidate was staged in three exact
groups, with no ignored build/test artifacts, secrets, environment files, logs or
participant payloads included. The two tracked public-progress deletions are included.
Push and draft PR creation subsequently completed under explicit authorization.
Merge and deployment remain blocked on required release checks and authorization.

Verification incident: the verifier applied the catalogue snapshot to a pre-existing
isolated verifier database on port 55499 before establishing ownership. Further writes
were stopped; do not reuse, restore or delete that database without authorization.
Neither production nor the manual preview database on port 55519 was touched.

Retained production deployment does not apply the catalogue JSON. Its existing
`--migrate` step will run the now-reviewed bounded pricing/WOM migration when a
release is authorized; no separate administrator login is needed for this operation.
The full snapshot importer remains prohibited against retained production. README
and the runbook describe both paths. No production write/deployment occurred and
actual production population is not claimed. Local packaging is complete; publication
remains on hold, with required release checks and push/PR authorization still applicable.

### Packaging handoff — 2026-09-16

Baseline HEAD was `5354a3454034ea72544563147cda9f63355ea1e5`; it remains preserved in
the new local history. The final documentation commit will be the last commit shown
in the packaging evidence and `git log`; its self-referential hash is intentionally
not recorded here. Candidate paths and SHA-256 content hashes, staged inventories,
deletion records, preservation comparison and final status are recorded under
`/private/tmp/bingo-complete-packaging-20260916/`. Source checks reuse the previously
passed focused Domain/Application/Node/PostgreSQL/HTTP/build/format evidence; no broad
suite or browser run was added. Cached diff checks report only the preserved extra
blank lines at EOF in the newly added `stats-base.css`, `stats-density.css` and
`CataloguePopulationPayload.cs`. Next permitted action: obtain authorization for
push/PR, then use PR CI for the full integration gate; merge and deployment remain
unauthorized.

### Simple Luck visual-check fixture — user requested 2026-09-16

Created only the new isolated test event `stats-manual-luck-251-502`:
http://127.0.0.1:5189/Events/stats-manual-luck-251-502/Stats
One tile, one personal 1/251 drop rate, seven teams with one player each, all at
502 KC and 0 through 6 approved drops. Served Stats response verified against
-87.5%, -50.1%, 0%, +49.2%, +78.8%, +92.5%, +97.7% respectively.
Evidence: `/private/tmp/bingo-luck-251-fixture-verified.json`. Add-only helper under
`/private/tmp/bingo-stats-manual-20260915/luck-251-helper` refuses rerunning the
existing event and guards the isolated database identity. No application-code,
existing-fixture, runtime or browser changes. User visually accepted the reference
fixture on 2026-09-16: "yeah it looks correct". No remaining work for this check.

### Calculation change complete — user approved 2026-09-16

User selected the custom probability-ranking Luck score bounded by +/-100%, with
expected drops at zero and interpolated neutral rank for fractional expectation.
PRODUCT_REQUIREMENTS.md and FUNCTIONAL_CONTRACTS.md own the exact formula/example.
Use it for Stats and tile Luck, centralize it for later replacement, preserve UI,
positive-KC filtering, tile attribution, stale retention and original timestamps.
Workers `/root/bounded_luck_math` and `/root/bounded_luck_integration` completed
their assigned implementation and checks (Astra xhigh). Independent read-only
reviewer `/root/bounded_luck_review` (Astra high) ran after both FINAL handoffs
and reported no material findings. No browser inspection.
User reconfirmed personal own-name rates from Admin Catalogue's Team content callout;
use the saved personal opportunity model, no further team-size division or shared-
encounter gate. Integration may proceed; no pending user decision.
Numerical implementation is complete: 27 focused Release domain tests passed;
`/private/tmp/bingo-luck-score-domain-results/luck-score-domain.trx` records results.
The shared calculator uses centered binomial recurrence/convolution with bounded
omitted tail error, returning unavailable rather than exceeding its finite work limit.
Integration completed: 71 focused PostgreSQL/HTTP cases passed, zero failures/skips;
strict Web Release build passed with zero warnings/errors. Evidence:
`/private/tmp/bingo-bounded-luck-results/bounded-luck-integration.trx`; report and
exact delta `/private/tmp/bingo-bounded-luck-integration-report.md`,
`/private/tmp/bingo-bounded-luck-integration.diff`. Required source review passed.
Preview reloaded at http://127.0.0.1:5189, session73805, log
`/private/tmp/bingo-bounded-luck-runtime.log`; only verified task-owned PID27680
was stopped. All assigned implementation/checks are complete, no active workers.
No UI changes or new visual acceptance gate; no packaging/deployment authorized.
Next permitted action: user-directed work.
No live database writes, fixture refresh, packaging or deployment authorized.

### User request, verbatim

> I have one more addition before we go live with this and a question.
> There cant be 2 people or items in most versatile and keeps on dropping, right?
>
> The admins have requested that if you click a specific tile in a team we show the combined kc the team has and you can expand to show individual kc. Great timing since we just added collection of kcs for everyone. I also think we should add tile luck to that. So basically we have a section inside each tile that is almost identical to ehb/drop ehb in the team view. It shows team total - luck - kc. Then you can expand and it shows luck - player name - kc. It should visually look pretty much the same as the 2 i just mentioned and added a screenshot of.

Reference screenshot:
`/var/folders/w5/74mg_d917xg33ry8_4qc9g5w0000gn/T/codex-clipboard-8f0e7315-71ec-4cfa-a114-fb4efe6abf9b.png`.
It shows the existing Drop EHB sidebar: section heading, supporting copy, team total,
expandable Contributors, thin row separators, participant names and right-aligned values.
Latest instruction: “Nevermind. Do a handoff to a new planner make sure it includes the message i just sent aswell”.
This interrupts the old planner; do not interpret it as permission to discard the addition.

### Standing verification direction — user, 2026-09-16

**Do not browser inspect.** User explicitly stopped browser inspection. Use scoped
source/build checks and automated functionality tests; user owns UI inspection and
appearance approval. No further browser tools or walkthrough are authorized for this
work. Prior browser evidence is historical execution, not permission to repeat it.

### Stale retention and contributor filtering complete — 2026-09-16

Known positive KC contributors are displayed; zero/unknown rows and empty groups
are hidden. Three focused PostgreSQL/HTTP cases and Web Release build passed.
Compatible retained KC and original timestamps now survive stale/failed refreshes;
legacy tile Luck is reconstructed only from compatible evidence. Two focused
PostgreSQL/HTTP stale cases passed; raw/additive cases were saved but not executed.
Independent source review passed after the implementer finished, including the
partial-batch guard. No browser inspection or fixture refresh was performed.
Evidence: `/private/tmp/bingo-tile-positive-kc-report.md`,
`/private/tmp/bingo-tile-stale-kc.diff`,
`/private/tmp/bingo-tile-stale-kc-results/tile-stale-kc.trx`.

### Stale notice correction complete — 2026-09-16

User accepts the notice only when stale, but rejected UTC, oversized text and
placement between rows. Authorized correction: compact muted notice below the
section description; Europe/Copenhagen local date/time with no timezone label.
Implemented only in the partial and scoped CSS. Strict Web Release build passed
with zero warnings/errors; independent bounded source review passed after completion.
Backend, row styling and positive-KC filter are preserved.
Task preview: http://127.0.0.1:5189, runtime log
`/private/tmp/bingo-tile-stale-kc-runtime.log`, session74302. Reloaded corrected build.
No active workers or remaining implementation work; user owns appearance acceptance.
No browser inspection, extra test matrix, packaging or database changes.

### Final order correction complete — 2026-09-16

User approved corrected styling ("Looks great") and requested only Eligible drops
above KC & Luck. `_TileActivity` moved after eligible-drops conditional and before
Approved submissions in `_TileSidebar.cshtml`; exact diff is
`/private/tmp/bingo-tile-kc-order-correction.diff`. No styling/calculation changes.
Scoped order/diff checks, focused independent source recheck and Web Release/Razor
build passed (0 warnings/errors). CUA reload confirms Eligible drops → KC & Luck →
Approved submissions. Existing style approval is recorded in UI_PAGE_MATRIX.md;
no new visual redesign or broad gate was introduced. Worker/reviewer finished.

Updated preview runs in exec session52550 with original environment/script and
`--urls http://127.0.0.1:5189`; log `/private/tmp/bingo-tile-kc-order-runtime.log`.
Only verified preview PID18459 was restarted. No fixture reset/reseed/refresh. Last
order-verification load shows the existing fixture's activity waiting state, not the
previous numeric result; no new activity data were fetched. Prior numeric render and
calculation evidence remain recorded, without claiming freshly synchronized activity.
Next permitted action: user-directed work only; no packaging, deployment or next pass.

### Visual correction completed — awaiting user acceptance, 2026-09-16

User rejected the initial custom rows and directed literal EHB/Drop EHB reuse.
Fresh implementer `/root/tile_ehb_style` (Astra xhigh) changed only `_TileActivity.cshtml`
and `site.public-ui.css`. Shared team-metric and contributor row/rank/name/value rules
now apply directly; obsolete custom typography/metric rows were removed. Summary is
TEAM TOTAL / signed Luck / green +count. Contributors are signed Luck in place of
rank / player name / green +count. Zero is numeric and green; negative Luck remains
coral. No visible KC suffix, Luck subcaption or single-metric boss labels. Multiple
boss/mode totals retain group headings and separate simple rows. Calculations unchanged.

Fresh reviewer `/root/tile_ehb_review` (Astra high) cleared the bounded source/cascade
check and direct metric-order invariant; no findings. Web Release/Razor build passed
with 0 warnings/errors; scoped diff/CSS checks and 13 .NET formatting cases passed.
Prior 16 backend cases remain applicable and were not rerun. Workers are finished.
Style baseline `/private/tmp/bingo-tile-kc-style-baseline-20260916`; exact diff
`/private/tmp/bingo-tile-kc-style-correction.diff`; report/commands
`/private/tmp/bingo-tile-kc-style-correction-report.md`.

Planner restarted only verified task preview PID11078 after build. Updated preview
runs in exec session21956 with the same script/--urls/environment on 127.0.0.1:5189,
log `/private/tmp/bingo-tile-kc-style-runtime.log`. No fixture reset/reseed/refresh.
Live CUA screenshot and AX show TEAM TOTAL / green0% / green+300, contributors
coral-50% / Alice / green+200 and green+100% / Bob / green+100, no redundant row labels;
click expands. Computed-style browser evaluation timed out, so no new computed-style
measurement is claimed; exact shared rule parity passed source checks. User visual
acceptance remains pending in UI_PAGE_MATRIX.md. Next: user checks corrected section;
only named feedback/direct consequences are authorized, no packaging or next pass.

### Approved decisions and delivered behavior

The user explicitly chose **only drops credited to the selected tile** for tile Luck
and **separate KC totals for each boss/mode**. Both are implemented under
DELIVERY_PLAN.md section 18 and PRODUCT_REQUIREMENTS.md section 15.1. The tie question
was answered: Most Versatile and Keeps on Dropping each display one winner; equal
counts are possible and internal IDs break ties. No tie-display redesign was requested.

The shared tile sidebar now renders KC & Luck in the existing team EHB/Drop EHB
composition, with Team total/Luck/per-boss-or-mode KC and expandable
Luck/player/KC contributors. Playing accounts aggregate by participant; character/metric
KC and tile outcomes are deduplicated. Tile-only approved/non-reversed received counts
use frozen first-approved event rates and full-event activity, without completion cutoff
or another team-size division. Missing, unranked, estimated, incomplete, stale and no-drop
states remain explicit. Event-wide Stats calculations and prior approved UI are preserved.

Existing Luck checkpoint JSON optionally stores coherent tile/team snapshots. Legacy
checkpoints lacking tile attribution calculate from a currently usable batch or wait
during an outage; reads do not write. No new table, migration, service, job or provider
call on tile access. Initial direct nested TeamBoard tile URL and enhanced sidebar share
the presentation. Standalone Tile.cshtml is inactive with no registered route and remains so.

### Verification, review and runtime

Fresh implementer `/root/tile_kc_implement` (Astra xhigh) finished. Fresh independent
reviewer `/root/tile_kc_review` (Astra high) completed one bounded source review, found
one P2 CSS specificity conflict, then cleared the named correction. No remaining source
findings. Neither worker owns further work; user visual acceptance is pending solely in
UI_PAGE_MATRIX.md. No broad audit or completed Stats walkthrough was repeated.

16 distinct focused PostgreSQL/HTTP cases passed across scoped runs: tile/team attribution,
corrected tile/character approval, duplicate outcomes/requirements, playing accounts/alt
exclusion, separate 13/7 modes, pooled Luck, frozen rates, no completion cutoff, no-drop,
missing/unranked/estimated/incomplete, retained stale/additive/reversal/recovery and legacy
checkpoints, plus actual nested/enhanced HTTP visibility and submission-control boundaries.
Production Release compiled. Strict whole-solution build remains blocked by pre-existing
CA1310 in CaptainScopedNavigationIntegrationTests.cs:560; that file matches baseline.
Focused test compilation downgraded only CA1310. No strict whole-solution pass is claimed.

Planner CUA checks passed direct route, click expansion, Enter collapse/Space reopening,
enhanced tile switching and browser Back. Existing Pool A fixture: 300 KC/0% Luck,
Alice 200/-50%, Bob 100/+100%; other eligible tile has 300 KC/-100% with zero credited
drops. CSS correction verified through computed styles and desktop/mobile renders:
6.4px gap, 40px Luck column, normal name wrapping and no mobile row overflow. This is
bounded browser evidence, not user visual approval or a complete theme walkthrough.

Evidence:
- `/private/tmp/bingo-tile-kc-baseline-20260916` and `manifest.json`: pre-change source/test baseline.
- `/private/tmp/bingo-tile-kc-addition.diff`: exact nine-file addition, excluding prior dirty work.
- `/private/tmp/bingo-tile-kc-implementation-report.md`: exact commands/results/logs and changed files.
- `/private/tmp/bingo-tile-kc-results/latest-outcomes.json` and original TRX files: 16 passed cases.
- `/private/tmp/bingo-tile-kc-browser-evidence.md`: browser results and named correction evidence.

Original addition preview ran in exec session 77427 (superseded by session21956 above) using
`/private/tmp/bingo-stats-manual-20260915/command.sh --urls http://127.0.0.1:5189`;
log `/private/tmp/bingo-tile-kc-runtime.log`. The script requires explicit --urls.
Private app.env must never be printed. Existing isolated database is `bingo_stats_manual`
on port 55519; no reset/reseed/fixture refresh. Development WOM fake remains enabled,
automatic sync disabled. Existing unrelated running apps were left alone.

### Next permitted action

User visual acceptance of the new section at
http://127.0.0.1:5189/Events/stats-step10-luck/Board/pool-a/Tiles/d0778154-995b-4828-a6c2-53a34a258c44
(expand Contributors). Address only named feedback and direct consequences if provided.
No staging, commit, push, deployment, another page family or release-gate completion is
authorized. Preserve all prior Stats acceptance and known exceptions below (sticky Luck
gap and tiny GP values rounded to M); historical Sommerbingo retrospective Stats remains
excluded. All user links must use http://127.0.0.1:5189, not localhost.

## Completed handoff: Stats 12-step acceptance walkthrough — 2026-09-16

Checkout: `/private/tmp/BingoWebpage-drop-announcements`, branch `drop-announcements`.
The user completed step 12 and confirmed selecting **14 September 2026 at 14:10
Europe/Copenhagen**, rather than the walkthrough's suggested 15 September. This is
within the fixture event window and matches the saved correction; no date-picker
mismatch was established. The 12-step walkthrough is complete with the existing known
UI exceptions retained below. This is not release-gate completion or deployment approval.

Read-only verification of `stats-step12-finalization`
(`99e25fb7-adb8-489e-a617-d5feaaebdf32`) in isolated `bingo_stats_manual` on port 55519:
- Archived, official results published, submission window remains closed.
- Version 1 retains Finishers' original 15 September 14:00 completion and reopening
  reason; active version 2 retains the selected 14 September 14:10 completion.
- Official Stats completion and board milestone agree with version 2. Raw tile/drop
  history is unchanged, including the original final tile completion.
- 125M GP / 5 drops / +25% overall Luck remain unchanged. Finishers: 100M / 4 drops,
  4/4 tiles, +100% Luck; Chasers: 25M / 1 drop, 1/4 tiles, -50% Luck.
- Frozen price records, first-approved rate/source bindings and metric records match
  the baseline exactly. Provider fetch/upstream timestamps and activity batch are
  unchanged. Calculation time advanced legitimately through the lifecycle operations.
- Archived public Stats Data returned HTTP 200 with the same persisted results.

Private evidence under `/private/tmp/bingo-stats-manual-20260915/`:
`step12-inspection.json`, `step12-verification.json`, `step12-http-after.json`, and
initial `step12-fixture.json` / `step12-http-baseline.json`. The helper was run only in
read-only `inspect` mode after the user's actions. No production edits, build, restart,
WOM refresh or database mutations occurred during post-action verification.
Use **http://127.0.0.1:5189**, including login and Admin links; the user cannot log in
through the localhost links. No authentication-code diagnosis or fix is claimed.

Manual acceptance is complete for this walkthrough; unrelated broader checklist cases,
whole-slice release checks and the documented navigation-test limitation are not newly
passed by it. No staging, commit, push, deployment or further pass is authorized.

### Step 11 completed

Step 11 is complete in the checkout below. Live actual-client checks
passed: Wiki mapping/hourly responses and WOM competition 145197 with 71 boss metrics
plus EHB for all 93 participants. All 6,696 WOM metric triples and player-record timestamps
matched the raw response; sentinel classification passed. Those player timestamps are
record-update times, not separately identified competition-endpoint snapshot times.
Private provider evidence stays in `/private/tmp/stats-step11-prices/` and
`/private/tmp/stats-step11-wom/`; never commit the real participant response.

The durable catalogue now contains 195 exact API prices and 116 primary-Wiki-confirmed
untradeables at zero. For 22 absent bulk-hour prices, the initial population uses the
latest available completed hourly observation, retaining its timestamp and the same
midpoint rule. Runtime fallback policy is unchanged. All 68 boss mappings are verified
against returned supported keys, including added `maggot_king` and `zalcano`. All 441
drops, 479 legacy variants, existing identities/rates and non-pricing metadata remain
unchanged. Both Nid entries remain separate untradeables, per explicit user clarification.
The configured WOM User-Agent now identifies DKLegacy and the agreed Discord contact.

The user confirmed independent Hiscores KC for CoX/CM, ToB/HM, ToA/Expert,
Gauntlet/Corrupted and Nightmare/Phosani. This is operator confirmation, not an assertion
that API documentation proves exclusivity. The temporary mode gate is removed;
14 focused Release PostgreSQL checks passed, including distinct 13/7 mode totals,
missing/unsupported mapping protection and normal scheduled refresh replacing an old
gated checkpoint. Fresh independent reviewer `stats_step11_review` passed the bounded
catalogue/config/mode delta and operational helper with no actionable findings.

Initial population committed to the isolated `bingo_stats_manual` database on port 55519:
311 items and 67 bosses, with 378 audit entries and version checks. Exact existing
normalized-name/slug identity matching preserved the database's different internal IDs.
Abyssal Sire's newer `TemporarilyUnavailable` validation result was preserved in full;
its unchanged metric is present in the successful live provider response. Protected
before/after hashes matched across 68 tables, including 13 fixture items, 4 fixture bosses,
454 source drops and all 116 frozen event prices. Fresh committed readback passed.
Evidence: `/private/tmp/stats-step11-catalogue/REPORT.md` and
`/private/tmp/stats-step11-modes/REPORT.md`. The app restarted on the checked Release
build, PID 3768, port 5189, with its existing environment/development fake unchanged;
all four existing step-10 price fixture Data endpoints returned HTTP 200 afterward.
No deployed database, UI, event prices, staging, commit or publication was changed.
Step 12 completion and retained-history verification are recorded above.

### Accepted steps 1–10 and prior implementation evidence

Checkout: `/private/tmp/BingoWebpage-drop-announcements`, branch `drop-announcements`.
User accepted sections 1–6 of the concise chat checklist, subject to later discoveries.
UI_PAGE_MATRIX.md owns the UI approval and its scope. The user subsequently reported
steps 1–9 of the numbered functional walkthrough passed: login, out-of-order approvals,
reversal, new submissions, attribution, missing-data states, saved guidance, artwork
persistence/permissions and conflicting artwork saves. MANUAL_TEST_CHECKLIST.md records
that exact scope; these are manual reports, not new automated checks. The user subsequently
reported “Everything passes” for step 10, noting that the small GP amounts display in M
and are not readable. Steps 11 and 12 completion are recorded above. The user authorized step-10 preparation; dedicated fixtures
are now materialized in the existing isolated database on port 55519, served on 5189.
No production code, imported catalogue, existing test-event records or app configuration
was changed by this preparation. No app restart, database reset or migration was needed.

Step-10 evidence: `/private/tmp/bingo-stats-manual-20260915/STEP10.md` and
`STEP10-PRICE-EDGES.md` contain exact URLs/actions; helpers and evidence stay in that private
temporary directory. Actual board handlers rejected missing GP, accepted explicit zero and
accepted a no-drop objective. Actual corrected approval introduced a late item at 2500 GP
and retained the original .01 probability once despite a duplicate at .02. Controlled
catalogue handler rejected a 3M candidate, retained trusted 1M and persisted its warning.
Actual StartNowAsync calls froze 1.5M API and 1M outage-fallback prices at the last completed
hour relative to actual start, with lifecycle/audit assertions. These provider inputs were
controlled stubs, not live provider proof. Display submission rows were synthetic fixtures.

Planner independently verified running HTTP Stats data: `stats-step10-luck` 4000 GP/4 drops,
Alice -50%, Bob +100%, Pool A 0%, Cara/Pool B -75%; one retained .01 outcome after correction.
`stats-step10-late` 2500 GP/1 drop despite catalogue 9000; `stats-step10-start-api` 1.5M/1 drop;
`stats-step10-start-fallback` 1M/1 drop. Authenticated catalogue GET rendered guard trusted/rejected
values. Evidence: `step10-independent-http.json`, `price-edge-helper/guard-rendered.html`,
`step10-continued.log` and helper evidence JSON. Manual step 10 is accepted per user, with
the display limitation recorded separately; exact small GP totals were independently
verified over HTTP, not claimed visually confirmed by the user. Step 11 is complete as
recorded above, as is step 12 acceptance. No packaging is authorized.

Known unresolved issues: small GP amounts such as 4,000 are formatted in rounded millions
and become unreadable; money() lacks a thousands branch. No display correction has been made.
The user left the sticky Luck comparison top gap after unsuccessful
corrections; current CSS uses margin-top:0/top:-1px and no cover pseudo-elements. Serving
and scroll geometry were checked, but the user explicitly reported it still not working.
The later step-1 sign-in flow passed per user; the earlier Safari/localhost cookie issue
was not independently diagnosed, and no authentication code fix was made.

Stats now uses hourCycle:h23 in both production time formatters. Focused afternoon/midnight
format and syntax checks passed; port 5189 serves both exact updated scripts. No restart.

The user's authorized Pass 5 implementation and focused checks are complete. All prior
dirty work was preserved. No worker/reviewer/task creation, staging, commit, push,
deployment, live catalogue population, user-database reset or running-app restart occurred.
Independent reviewer `stats_pass5_review` (GPT-6 Astra high) completed the broad
source review and found four P2 integration defects. The original implementer
(GPT-6 Astra xhigh) completed only these corrections and focused regressions:

- F1: preserve team/player composite identities and select the actual current team.
- F2: Board progress must use official completion corrections while retaining raw tile history.
- F3: invalidate chart drawing when timestamp domain/positions change.
- F4: prevent in-flight refreshes from overwriting artwork drafts or newer artwork/guidance saves.

The SAME reviewer passed the bounded F1–F4 recheck with no remaining defects in
those corrections or their direct consequences. Planner reconciled the verdict on
2026-09-15. All five Stats implementation passes and their required independent source
reviews are complete; implementer and reviewer are stopped. The actual approved source
was ported. Production UI acceptance is recorded in UI_PAGE_MATRIX.md; detailed
functional acceptance and known exceptions remain as stated above.

The 12-step acceptance walkthrough in MANUAL_TEST_CHECKLIST.md is complete.
Provider/mode and initial catalogue work is complete as recorded above. Whole-slice release gates and the documented
navigation-test limitation remain. No packaging, deployment or user-owned database
mutation is authorized by this handoff.

### Five-screenshot responsive content correction

The original Astra xhigh implementer inspected all five supplied screenshots and completed
only the four authorized content corrections. During implementation the user said valuable
drops stacked too early; the final switch is **650px actual GP-card width**, replacing the
initially proposed 760px. Three columns remain above 650px (subject to the existing mobile
viewport rule); the 600px donut/GP stacking query is unchanged. UI_PAGE_MATRIX.md records
this user decision and the bounded Luck spacing adaptation, with acceptance still pending.

Production changes are limited to `stats-integration.css` and `stats-page.js`:

- R1: measure numeric endpoint text in its real font, reserving intermediate count-up
  decimal width, the existing 7px bar gap and 4px outer clearance on both sides. All
  entries in the full view, including comparisons, share one available bar span with
  the existing max(60, largest absolute score) denominator. Roomy tracks retain 40%;
  narrow tracks use a common smaller span. Matching row/key track minima reserve label
  space while names can ellipsize. Resize/font completion repaints the existing animation
  progress; exact numeric strings, row heights, pinned clear and unavailable states stay.
- R2: Board progress team names use one line, min-width:0 and ellipsis, with their entire
  names in the DOM/title and existing marker accessible labels. Existing name-column
  widths, horizontal scroll, row heights, counts, marker positions and inspection remain.
- R3: fit the unchanged formatted total and caption to the actual inner circle of the
  130px/150px ring using measured text bounds and 4px clearance. Keep the approved 34px
  value/9px caption when they fit; reduce value typography as needed, using an 8px compact
  caption if needed before shrinking the value below 24px. The exact GP string remains
  visible, titled and labelled. Resize/font completion preserves donut nodes, mask/sweep
  and geometry. No further abbreviation or data change.
- R4: at <=650px card width, reuse stacked rows with artwork, two-line bounded name/byline
  text and an unbroken separate GP slot. Full text remains in DOM and titles. Wider cards
  retain three columns. Treasure/list flex bases use natural content height and cannot
  shrink below it, including when the upper GP view holds its measured dimensions.
  Existing `holdGpLayout`, artwork hover and absence of drop-change animation are retained.

The approved prototype, base/density styles (including the previous container specificity
fix), adapter and assets/fonts are unchanged. All 29 protected copied functions remain
identical; UI1–UI3 and F1–F4 checks remain intact. Prior Luck name/unavailable truncation
rules are preserved. Only the two production files, existing test harness/regressions,
this status owner and the existing UI_PAGE_MATRIX correction/scale entries changed.

Executed evidence:

- `/private/tmp/stats-content-final.log`: **64/64 Node port/renderer checks pass**, including
  all previous 50 and 14 new cases. New cases use the screenshot's long names, `55.45B`,
  extreme signed percentages with decimals, 2/3/5/8/15 teams, 130px/150px supplied ring
  geometry, narrow/wide supplied track measurements, resized/loaded-font measurements,
  intermediate and reduced motion, working pinned clear, unavailable status, marker focus,
  and actual tab-driven held Teams/Players/Everyone plus team drill-down.
- The cascade probe includes narrow-card/wide-viewport cases, 650/651 boundary checks,
  natural treasure/list height rules, text bounds and held upper-view constraints. The
  previous complete nested selector-tree/container specificity regressions still pass.
- `/private/tmp/stats-content-before.log`: **all 14 new cases fail** against saved pre-fix
  renderer/integration CSS. R3 independently catches missing fitted geometry; R4 independently
  catches three columns persisting at 650px, in addition to the combined content cases.
- Scoped diff/whitespace and JavaScript syntax checks pass. Correction diff:
  `/private/tmp/stats-content-corrections.diff`. The existing source-hash manifest now
  records changes only to integration CSS, renderer and test file; original-to-production
  renderer mapping updated at `/private/tmp/stats-pass5-renderer-port.diff`.
- `/private/tmp/stats-content-served.json`: isolated port 5189 returns HTTP 200 and exact
  workspace bytes for changed CSS/renderer and unchanged base/density styles. The served
  CSS has the revised 650px query and no 760px query. No restart was needed.
  Integration CSS SHA-256: `196c7e1934b0783cc7b1ff41713b1cc3e769ad71a7742f8d64de6b15d3a984e4`.
  Renderer SHA-256: `01ab3d582bc124ded5cdaf4fa2db5e95babbc27a6a0bff99fb8f52da48ab7c3a`.

Limits and next permitted action: screenshots establish the original visible defects.
New checks execute production measurement/animation/DOM code with controlled text/box
metrics and check scoped source cascade; they do not measure the browser's real font,
container layout or final panel containment. No browser automation or visual acceptance
is claimed. The two saved PostgreSQL DTO cases reused earlier files; no .NET/build/PG run,
provider/fixture/data mutation, other-app change, worker/task creation or packaging occurred.
Implementer is stopped, ready for the SAME independent fidelity reviewer's bounded R1–R4
recheck and direct consequences, followed by the user's visual recheck. No unrelated
endpoint-label overlap, page family, broader review or new implementation pass is authorized.

### User-reported Drop value responsive mismatch

User reports production Drop value does not stack like the approved prototype.
Planner identified unscoped child selectors in `stats-density.css` @container blocks
(600px card width), while ordinary selectors carry `.public-stats-page .stats-page`.
This added two classes of specificity only to desktop rules: the unscoped narrow-card
rules lost the cascade even when their container query matched. Prior declaration-only
port checks missed the changed selectors and therefore missed the behavior difference.

The original Astra xhigh implementer completed the bounded port correction:

- Added only the missing `.public-stats-page .stats-page` scope to 12 selectors in all
  five existing `@container(max-width:600px)` blocks. The additional share-list row-size
  block at line 1122 had the same omission and is included. Corrected owners remain at
  lines 455–461, 1122, 1224, 1234 and 1352–1355.
- Exact declarations, order, query kind/conditions and the approved layout remain
  intact: one-column stacked GP at <=600px card width, the 130px donut/list arrangement,
  heading and share-content rules, and held chart/list bounds. Above 600px retains the
  original side-by-side layout. No viewport substitute or new breakpoint was added.
- Full base/density rule-tree comparison found no other missing selector scopes in
  nested groups. Base CSS needed no edit. Approved prototype, renderer/UI1–UI3 fixes,
  adapter, assets/fonts and preceding Luck overflow CSS remain unchanged.
- Added two regressions to the existing `stats-production.test.js`: complete nested
  selector/query/declaration trees against the approved prototype (with only the
  established root/theme/asset mapping), and a focused GP cascade probe. The probe
  checks winning selectors/declarations, query ancestry and uniform two-class scope
  specificity at card widths 599/600/601, immediately below/at/above every existing
  viewport width threshold, both ordinary and held layout states. It covers visuals,
  share/heading/ring/list/content, trend/chart bounds and the GP legend.

Executed evidence:

- `/private/tmp/stats-responsive-before.log`: **both new regressions fail** against
  saved pre-fix CSS. The cascade failure specifically observes desktop
  `grid-template-columns:minmax(0,1fr) 215px` winning at a 599px card width.
- `/private/tmp/stats-responsive-final.log`: **50/50 existing and new Node port/renderer
  tests pass**, including unchanged baseline hashes, declarations/order, all 29 copied
  interaction functions, UI1–UI3 and prior F1–F4 checks. The two saved PostgreSQL DTO
  cases reuse earlier fixture files; no .NET/build/PostgreSQL execution or mutation.
- Scoped correction diff and whitespace checks pass:
  `/private/tmp/stats-responsive-correction.diff`. Of the 20 recorded production/test
  source hashes, only `stats-density.css` and the test file changed; manifest updated
  at `/private/tmp/stats-pass5-source-hashes.txt`. This status owner is the only other
  repository file changed during this correction.
- `/private/tmp/stats-responsive-served.json`: isolated port 5189 returns HTTP 200 and
  exact workspace bytes for updated density CSS plus unchanged base CSS, integration
  CSS and renderer. Density SHA-256:
  `c1cdd80743ec238c4ae1e08e486b5cfdae94e6a44fee1886f8a1aac80f7a4094`.
  No restart, fixture/provider/catalogue work, other-app/database changes or packaging.

Limits and next permitted action: these are parsed source/query/specificity and
explicit-declaration cascade checks, not a browser CSS engine, measured container
sizes, resolved custom properties, or rendered proof. Browser automation was not used;
user visual acceptance remains pending in UI_PAGE_MATRIX.md. The SAME independent
fidelity reviewer passed the bounded missing-scope correction recheck with no residual
findings. Reviewed the recorded 50/50 pass and both pre-fix failures; independently
verified all 20 hashes and scoped whitespace. Tests were not rerun during review.
Implementer and reviewer are stopped. Next is the user's responsive visual recheck.
No broader review/redesign, new implementation pass or packaging is authorized.

### User-authorized report-only UI comparison

After the Luck overlap report, the user reported missing Drop value refresh animations
and requested another approved-UI versus production-UI review. Explicit boundary:
**report gaps only; do not fix them until the user chooses the next work.** The Luck CSS
correction below finished before the pause and is already served; it remains in place.
Original implementer is stopped. Fresh read-only reviewer `stats_ui_fidelity_review`
(Astra high) completed the comparison of all six sections, markup/style integration,
controls, responsive rules, artwork and animation triggers. No further fixes were made.

Three source-supported P2 finding groups (browser timing/rendering not verified):

1. Startup/theme/live-refresh callbacks interrupt entry motion. App site.js writes the
   theme on DOMContentLoaded; Stats observer redraws the donut even for an unchanged
   theme. WatchEvent immediately refreshes Live data; generatedAt changes cause full
   rerender, shortening GP/Luck entry. Background refresh also replays Luck/Board progress
   and drops Board progress inspection/focused markers. Milestones retain their unchanged
   signature guard. Owners: stats-page.js theme957, renderRace663/742, refresh1041,
   observer1075, WatchEvent1084; site.js141/149.
2. GP morph uses changing-length timestamp paths. stats-adapter.js20 builds the current
   timestamp set; approvals/reversals can change path command count, while stats-page.js
   346/356 passes incompatible paths to the copied SVG interpolation. Tab/team switches
   with the same timestamp set are not implicated. Prototype had fixed14positions.
3. Theme callback updates GP chart/donut but not inline legend swatches. stats-page.js957
   skips renderLegend383; old theme colors remain until another action rebuilds the legend.

No additional concrete source deviations were found in the reviewed paths. This is not a
rendered UI comparison or visual approval. Optional DOM harness was not executed because
node was not on reviewer's PATH; no test failure is inferred. Recent Luck ellipsis source
correction is narrow and preserves pinned controls, but awaits user visual recheck.
Bottom valuable-drop change animation is intentionally absent per approved prototype.

The user subsequently authorized fixing all three groups. Original implementer Astra
xhigh has completed only UI1 startup/refresh lifecycle, UI2 compatible GP morph paths and
UI3 theme legend synchronization, plus direct consequences and focused executable checks.
The SAME fidelity reviewer passed the bounded UI1–UI3 recheck with no remaining
actionable findings in the named corrections or direct consequences. The reviewer
inspected all 12 new regressions and the recorded 48/48 passing suite, independently
verified all 20 manifest hashes and correction whitespace; no renderer rerun or browser
verification was performed. Implementer and reviewer are stopped.
Production visual acceptance is still pending in UI_PAGE_MATRIX.md. No provider/catalogue/
fixture mutations, other-app changes, packaging or further implementation are authorized.

### UI1–UI3 implementation checkpoint and evidence

Only `src/Bingo.Web/wwwroot/js/stats-page.js`, the existing
`tests/Bingo.BrowserTests/stats-production.test.js` harness/regressions, and this status
owner changed during this correction. The adapter, approved prototype files, scoped
base/density styles, assets/fonts and prior Luck overflow CSS remain unchanged.

- UI1: unchanged theme notifications return immediately. An initial clock-only GET
  preserves entry nodes through the existing 1400 ms entrance window, then fetches
  anew through the existing draft/save/revision fence. Refresh compares section inputs;
  unchanged Luck and donut are retained, chart time-domain changes still redraw, and
  background race updates render without replaying the 1200 ms entrance. Race controls
  retain an absolute inspection time and a stable team/tile/timestamp marker key;
  refresh restores inspection and focused markers when still valid. Race callbacks
  retain their own data snapshot so an unrelated refresh cannot mix coordinate domains.
- UI2: chart transitions resample old/new polylines onto compatible x coordinates,
  retaining vertical segments from close timestamps. Only animation geometry is
  resampled; the underlying final path and hover dates retain exact authoritative
  points. Interrupted transitions start from the current 240 ms spline position,
  captured before detaching the prior SVG. Reduced motion ends at the final data and
  clears transition state. Approved entry durations and SVG interpolation remain intact.
- UI3: real theme changes recolor chart paths/dots/end labels, donut segments/share
  dots and GP legend dots in place, including generated team/player colors. Reveal
  masks, focused controls, pinned comparisons and scroll positions remain intact.

Executed checks:

- `/private/tmp/stats-fidelity-final.log`: **48/48 Node renderer checks pass**, including
  12 focused fidelity regressions and all 36 prior cases. The two PostgreSQL DTO cases
  reuse the previously captured controlled fixtures in
  `/private/tmp/stats-pass5-corrections-fixtures`; no PostgreSQL run/mutation occurred.
- Actual observer, immediate WatchEvent GET, window focus, visibility and reconnect
  callbacks are exercised. Checks cover clock-only deferral, actual data during entry,
  unchanged section calls/nodes, race inspection/focus/scroll, fresh data after deferred
  refresh/save fences, insertion/removal/domain changes, interrupted morphs, coincident
  timestamp geometry, reduced-motion changes, and 5/15-team theme/pinned states.
- `/private/tmp/stats-fidelity-before.log`: 11 of the 12 new fidelity regressions fail
  against the saved pre-correction renderer; the extra coincident/reduced-motion case
  already passed before. The prior baseline hashes, CSS declarations/order and all 29
  copied interaction-function assertions remain intact and pass.
- Scoped diff/whitespace checks pass. Of the 20 previously recorded production/test
  source hashes, only the renderer and its test file changed. Updated manifest:
  `/private/tmp/stats-pass5-source-hashes.txt`. Correction-only diff:
  `/private/tmp/stats-fidelity-corrections.diff`. The existing original-to-production
  renderer mapping diff was refreshed at `/private/tmp/stats-pass5-renderer-port.diff`.
- `/private/tmp/stats-fidelity-served.json`: isolated port 5189 returned HTTP 200 and
  exact workspace bytes for renderer, unchanged adapter and preserved Luck CSS.
  Renderer SHA-256: `5753b31e85b707b949ad3cdbc89905e4dda58a797abb279fad53afe6f16f8cb4`.
  No restart was necessary. Existing apps and databases were untouched.

Limits and next permitted action: these are executable DOM/clock/geometry stand-ins,
not browser timing/layout/SMIL painting or visual acceptance. No .NET/PG/build gate was
rerun for this JavaScript-only correction; prior backend evidence is reused. The original
implementer and SAME reviewer are stopped after the passing bounded recheck. Next is
the user's production visual recheck (including the preceding Luck overflow correction).
Do not reopen other page families,
provider/catalogue work, packaging or broader review from this handoff.

### User-reported visual defect — Luck row overflow

The user's quick sweep screenshot showed long Luck names wrapping into following rows
and unavailable status text escaping its available width. The bounded correction is
implemented in `src/Bingo.Web/wwwroot/css/stats-integration.css` only:

- Ordinary team-button and player-span names use `min-width:0`, one line, hidden
  overflow and ellipsis inside the existing name column.
- Pinned rows are excluded from wrapper clipping: their existing truncated text child,
  flex gap and nonshrinking clear button remain intact, including its focus outline.
- Unavailable labels are bounded by both edges of the existing track, with one-line
  ellipsis. Existing full label `title`, full row/interaction accessible labels and
  full player text remain in the actual renderer DOM; no name/status string was cut.
- No panel/row height, grid, font, visible-row count, timing, prototype, renderer or
  calculation changed. No fixture/activity refresh, provider call, database write or
  app restart was performed. Production visual acceptance remains pending.

Focused evidence:

- Scoped cascade/load-order and diff/whitespace checks passed. Base/density CSS and the
  other 19 reviewed production/test source hashes are unchanged. No new persistent
  tests or .NET build were needed for this CSS-only change.
- `/private/tmp/stats-luck-overflow-check.log`: **30 renderer cases pass** — 2/3/5/8/15
  teams × 320/560/1150 harness widths × numeric/unavailable data, including long ordinary
  button/span names, title/full accessible text and pinned name/operational clear.
  `/private/tmp/stats-luck-overflow-check.cjs` reuses the existing actual-renderer harness.
  These are DOM stand-ins and scoped source/cascade checks, not browser layout evidence.
- The isolated app's `http://127.0.0.1:5189/css/stats-integration.css` returned 200 and
  exactly matched workspace bytes without restart. Receipt:
  `/private/tmp/stats-luck-overflow-served.json`; CSS SHA-256:
  `c2f2641e2f5c7a128ac0d888b1a8c83ecefd8659965ccbdb01c5bd8e7a45d5b0`.
- Scoped before/after diff: `/private/tmp/stats-luck-overflow.diff`.
  `/private/tmp/stats-pass5-source-hashes.txt` now carries the new integration-CSS hash.

This correction was included in the report-only fidelity review above and remains
unchanged through UI1–UI3 remediation. User visual recheck of Luck remains pending.

### Isolated manual-test setup ready — 2026-09-15

The user subsequently authorized step 1 of acceptance: a NEW disposable PostgreSQL
store and separate local app. This setup is complete; existing apps/databases were not
reset, changed or restarted. Production implementation was not reopened. No agents,
review pass, staging, commit, catalogue export or live provider request was made.

- Main five-team page: `http://127.0.0.1:5189/Events/stats-manual-teams-5/Stats`.
  The checklist's original six-team event is also available at
  `http://127.0.0.1:5189/Events/test-15-dkl-live/Stats`.
- Private local credentials, full URL table, provenance, logs and stop/restart commands:
  `/private/tmp/bingo-stats-manual-20260915/SETUP.md` (mode 0600 in a 0700 directory).
  Roles: StatsOwner (Super Admin), SeedAdminTwo (Admin), SeedEvidenceCaptain,
  SeedEvidenceCoCaptain and SeedEvidenceParticipant. Passwords stay outside the repo.
- Requested 2/3/4/5/8/15-team variants are reachable with 7 players per team, long names,
  a complete frozen 5×5 board, varied approved progress and 57/81/102/120/177/324 item
  drops respectively. Each populated variant also has pending evidence. Empty-evidence,
  missing-GP/activity and waiting-activity pages are ready. Hidden/private/unpublished/
  excluded/unknown routes return 404; the cancelled-public route renders cancellation.
- All five role logins and Stats permissions passed HTTP checks for the five-team and
  existing six-team pages. Only Super Admin receives artwork editing. Health ready is
  Healthy. Served Stats JS/CSS, font and artwork matched checkout hashes. Evidence is in
  `http-roles-five.log`, `http-roles-six.log`, `http-variants.log` and `fixture-results.json`
  under the private setup directory. No browser automation or manual visual approval.
- New container: `bingo-stats-manual-20260915`, ID
  `ae9ef2b907925e8ecccbb3993dc94b9001b297ab063fc74faa653c04b5a012f1`;
  PostgreSQL binds only `127.0.0.1:55519`, database `bingo_stats_manual`.
  New app PID **85963**, URL `http://127.0.0.1:5189`; evidence/cache are inside the private
  setup directory. Stop only this setup with `kill -TERM 85963` then
  `docker stop bingo-stats-manual-20260915`; no automatic deletion is scheduled.
- Setup used the existing migrations, saved catalogue apply, owner bootstrap and
  development seeder. Additional fixture code is outside production in the private
  `fixture-helper/` directory. Interrupted empty/spare setup records remain isolated and
  are excluded from the walkthrough. No production behavior/source file changed.
- Seven new **TEST** items carry arbitrary fixed GP values, and a TEST source supplies
  controlled rates/metric responses. The imported 311-item catalogue is unchanged.
  Development WOM fake is enabled, automatic synchronization disabled; controlled
  observations are not provider validation. The original six-team workflow still needs
  reviewed real catalogue prices before real Drop value acceptance.
- Planner separately fetched live Wiki mapping/hourly candidates for `2026-09-15T18:00Z`:
  189 exact items with current prices, 6 exact without current prices, 116 unmatched.
  `/private/tmp/stats-initial-price-review/review.md` and `catalogue-price-proposals.json`
  are **unapplied proposals**. No unmatched item was set to 0. Provider proof and reviewed
  initial population must precede real Drop value acceptance.

Next: planner/user perform the consolidated acceptance walkthrough against this isolated
setup. Existing manual-start/private-board fixture `test-62-board-publication-setup` and
other development workflow stages are present; scheduled-start/provider and finalization
acceptance remain unexecuted. All manual checkboxes and production page approval remain
pending. The setup worker is stopped; the separate app/container remain running.

### F1–F4 correction handoff

Only `Stats.cshtml.cs`, `stats-adapter.js`, `stats-page.js`, the existing Pass 5
Node/PG test files, and this status owner changed in the correction pass.

- **F1:** presentation keys now combine team and participant IDs for GP and Luck,
  including search, preview, hide, pin, drop attribution and milestone lookup.
  The route returns the authenticated account's current confirmed membership in a
  public Stats team; historical contribution order cannot choose “My team.” A departed
  participant retains evidence but has no current-membership shortcut.
- **F2:** raw tile markers/count history remain unchanged. A published official
  completion controls the finish marker. If its timestamp has no tile marker, a
  separate “Official completion” marker uses that authoritative time and counts.
  An official incomplete result suppresses the raw finish; unfinalization restores
  the raw finish. The existing race renderer, geometry and sweep remain in use.
- **F3:** chart invalidation includes timestamp coordinates and timezone; refresh
  invalidation also includes the advancing live clock, current membership and saved
  preference. Numeric GP arrays can stay identical while paths, date labels and
  inspection move together after an end-time or submission-time correction.
- **F4:** each GET captures a local edit/save revision. A response crossing an editor
  session or settings mutation is discarded and refetched after editing/saving ends.
  Saves sharing the account version cannot run concurrently. Draft cancellation,
  save failure and a closed dialog during a pending save release the fence correctly;
  focus remains usable and a later save error is visible outside the closed dialog.
  A fresh preference response also synchronizes its checkbox.

Executed correction evidence:

- **3 PostgreSQL/HTTP tests passed**: the two new correction journeys (team move and
  finalization → archive → unfinalization), plus the affected full Stats HTTP journey.
  `/private/tmp/stats-pass5-corrections-postgres.log`. The fixture exports are controlled,
  temporary data in `/private/tmp/stats-pass5-corrections-fixtures/`; no real participants
  or user database were used.
- **35 Node/DOM/source tests passed, none skipped**, including 15 new correction cases
  and both real PostgreSQL DTOs through the actual production renderer.
  `/private/tmp/stats-pass5-corrections-dom.log`. After the final F4 cancellation/focus/
  error-state correction, **all 9 focused F4 cases passed** (including one additional
  case), `/private/tmp/stats-pass5-corrections-refresh-final.log`. Thus 36 distinct
  cases are covered across those two runs; no claim of a single 36-case run.
- Before/after reproduction used a saved pre-correction copy of the actual JS:
  all 13 then-existing synthetic correction regressions failed against that copy,
  `/private/tmp/stats-pass5-corrections-before.log`, and subsequently passed with the
  fixes. It did not restore or overwrite the working sources.
- Web Release build: **0 warnings / 0 errors**,
  `/private/tmp/stats-pass5-corrections-release.log`. Integration build succeeded with
  only the established two protected-test CA1310 warnings,
  `/private/tmp/stats-pass5-corrections-integration-build.log`.
- All 20 owned source files passed scoped whitespace checks. Refreshed source hashes:
  `/private/tmp/stats-pass5-source-hashes.txt`; focused correction diff:
  `/private/tmp/stats-pass5-corrections.diff`; original/production renderer diff:
  `/private/tmp/stats-pass5-renderer-port.diff`. Approved prototype/assets/CSS hashes
  and the 29 unchanged interaction functions passed the port test.

Reproduction order: build the integration tests using the established CA1310 exception;
run the `StatsPass5Correction` and full Stats HTTP filters with
`STATS_PASS5_FIXTURE_DIRECTORY=/private/tmp/stats-pass5-corrections-fixtures`; run the
Node test file with the same variable. Without it, only the two exported-DTO Node
cases are skipped. `STATS_PASS5_SCRIPT_ROOT` optionally selects the saved original
scripts for failure reproduction.

Unaffected prior PG, Pass 4, migration and model evidence below is retained. There
were no schema, CSS, markup or provider changes during corrections. This is an
implementer evidence, now independently source-reviewed; it is not manual acceptance.

### Actual approved-source mapping (review the code/diff, not screenshots alone)

All destinations below are relative to `src/Bingo.Web/` unless stated otherwise.

| Approved input | Production destination and retained implementation |
| --- | --- |
| `prototypes/stats/outputs/stats-page-prototype.html`, `.gp-panel` | `Pages/Events/Stats.cshtml`: same section, title/help, tabs, divider/compact search, trend/share grid, pie list, comparison and valuable-drop DOM. Fixture placeholder rows are replaced by the existing renderer's real input. |
| Same HTML, `.luck-panel` / `.repeat-drop-card` | Same Razor page: original title/back/help, tabs/search, ranking scroll host and repeat-item composition. Names/counts/item are populated from the DTO. |
| Same HTML, `.timeline-panel` / `.versatile-card` / `.race-panel` | Same Razor page: original milestone filter/rail, blue card, race axis/scroll/tooltip/help. Real team choices and timestamps replace fixture options/dates. |
| Same HTML, `#artwork-editor` | Same Razor page, Super Admin only: original dialog, drag/keyboard preview, size-view tabs, ranges and Save/Cancel/Reset controls. Shared persistence wording replaces browser-storage wording. |
| Same HTML embedded `<style>` | `wwwroot/css/stats-base.css`: original declaration blocks and order, including Barlow/Geist, scoped to `.public-stats-page .stats-page`; only selector namespace/root theme and asset URLs change. |
| `stats-density.css` | `wwwroot/css/stats-density.css`: every original declaration block/order and breakpoint retained; same namespace/asset-URL adaptation. |
| `stats-prototype.js` | `wwwroot/js/stats-page.js`: original section renderers, event handlers and animation mechanisms. The executable port test compares 29 unchanged named interaction/motion functions literally against the baseline. Data-dependent date/index/count/identity code is adapted as described below. |
| Linked fonts/item art/watermark | `wwwroot/stats/assets/`: byte-identical Barlow ExtraBold/SemiBold, Geist Variable, seven approved item images, and `branding/login-artwork-dark.svg`. Item image selection uses stable Wiki IDs when mapped; otherwise it uses the actual DTO image, or an honest missing-image state. No label-based identity guessing. |

Original baseline hashes (rechecked unchanged):

- HTML: `fcd542de6afb2c0889435423b15d461ec601566dee8bee755cc5ca7b9112ed26`
- Density CSS: `88d4c40780c5c675600e736ec80c1e8f6975cb5a7c2ff83693e9f43f70eff95d`
- JS: `50d6f6512117cd863c4c0a4f9e7a2d3a9d1dcbd2e13e365b3379b656cd598b24`

### Bounded integration changes and owners

- `Pages/Events/Stats.cshtml(.cs)` owns `/Events/{slug}/Stats`, safe JSON/bootstrap,
  `?handler=Data` refresh, owner guidance POST and Super Admin artwork POST. The existing
  public Stats service is the access/data authority. No preferred-event fallback or WOM
  call per page visit. Cancelled published events use `_EventCancelled`.
- `_Layout.cshtml` adds the Stats event-navigation link; `SharedShellService` explicitly
  recognizes the route for Captain/submissions context. The masthead reuses the existing
  Teams masthead markup/classes. Prototype header/masthead and footer/demo controls are
  absent; section controls remain. Only Adjust artwork and the separately approved saved
  guidance control appear below the sections.
- Planner confirmed the fidelity-preserving wrapper resolution: app header/masthead stay
  unchanged; the Stats root offsets the app main gutter and contains the original `.shell`
  (max 1640px, 3.6% padding; 5% at ≤850px). The app's 88rem cap does not constrain it and
  padding is applied once. `stats-integration.css` also neutralizes existing `.panel`
  margin-top and Bootstrap `.toast` width/border/hidden defaults; otherwise they would
  alter the original composition or hide its announcements. Other app pages are unaffected.
- `stats-adapter.js` aligns authoritative GP snapshots to real timestamps, adapts stable
  teams/players/item identities, server shares, complete Luck results, progress snapshots
  and server-ordered milestones. `PublicEventStats.Tiles` now carries names/artwork from
  the already queried approved public Board projection, so pending draft edits cannot leak
  through labels. No competing GP/Luck/completion calculation is introduced in JavaScript.
- Renderer changes replace fixture-only counts, roster/valuable-drop generation, dates,
  sample completion arrays, milestone cutoffs and bootstrap. GP uses actual timestamp
  positions; race uses the same normalized coordinate span and unchanged 1200ms sweep.
  Missing prices show known-GP context, unavailable Luck shows concise status without a
  false 0% bar, and stale Luck retains original result/times/revision. Existing chart,
  search, comparison, help and interruption mechanisms remain.
- User-approved Luck adaptation: one denominator is `max(60, largest absolute finite
  score in the full current view, including comparison)`. Keep 40% half-track extent,
  proportional bars, score labels and synchronized motion. Scrolling does not rescale.
  UI_PAGE_MATRIX.md owns this decision.
- Existing Account owns `StatsGuidanceHidden` (false by default); the current authenticated
  account and its version govern saves. Existing CatalogueItem owns nullable bounded
  X/Y/width/height/scale/rotation fields using the original editor's ranges. All six are
  absent for the default fit or present/valid together. Save/reset validate the actual
  displayed item, event visibility, active Super Admin, item/account concurrency and
  antiforgery; settings and existing AuditEntry write commit/roll back together. Cancel
  changes only the preview. Readers use the saved shared appearance. No new table/framework.
- Migration `20260915192848_SaveStatsGuidanceAndArtwork`, designer and model snapshot
  are complete. Existing rows/price data survive the isolated downgrade/upgrade rehearsal;
  PostgreSQL enforces the all-or-none transform bounds. No reset FK-list change is needed.
- Refresh uses the existing progress hub, the shell's connection where present, and the
  Stats data handler. Focus/revisit also refreshes. Scope, comparison, searches, inspected
  GP time and page/list scroll are retained; an open artwork draft defers refresh.
- `MANUAL_TEST_CHECKLIST.md` now has the consolidated final Stats walkthrough: actual
  entry points/roles/controlled fixtures, all Pass 1–2 catalogue/API/price/start feedback,
  every Stats interaction, guidance/artwork persistence, deferred provider gates, and
  finalization last. Every manual checkbox remains unexecuted.

### Executed focused evidence

- **20 new PostgreSQL/HTTP cases pass**, `/private/tmp/stats-pass5-postgres-final.log`:
  actual Razor route/nav/direct refresh and JSON encoding; anonymous/ordinary/Super Admin
  authorization and antiforgery; owner-only preference/session reload and stale token;
  artwork save/read/reset/range validation/concurrent catalogue or actor-role change;
  audit rollback; migration preservation; hidden/private/unpublished/unknown/excluded
  access and cancelled presentation. The final full HTTP journey was rerun after the
  wrapper and scoped role-link fixture additions: `/private/tmp/stats-pass5-http-final.log`.
- **20 Node/DOM/source cases pass**, `/private/tmp/stats-pass5-dom-final.log`, in
  `tests/Bingo.BrowserTests/stats-production.test.js`. Actual production bootstrap with
  excluded elements absent; 0/2/3/4/5/8/15 teams, long/escaped labels, missing/stale states,
  top-five/sixth comparison/search/no page jump, real time/count/marker focus, motion
  interruption, guidance and artwork draft/save/reset, preserved refresh state, 320/560/
  850/1150 harness widths, original CSS declaration/order and font/asset hashes, and
  explicit app CSS collision/outer-width checks. These are controlled DOM stand-ins and
  source/cascade checks, not browser rendering or user visual acceptance.
- **14 affected Pass 4 regressions pass**, including complete query, weighted/later-before-
  earlier approval, tied-time aggregate order, scheduled-end capture and additive stale
  retention/reversal cases. `/private/tmp/stats-pass5-regressions.log` also records the
  one older navigation test failure below; it is not an all-green regression run.
- Web Release build: **0 warnings / 0 errors**, `/private/tmp/stats-pass5-release-final.log`.
  EF reports no pending model changes, `/private/tmp/stats-pass5-model-check.log`.
  Integration builds use only the previously established CA1310 exception for the two
  warnings in protected `CaptainScopedNavigationIntegrationTests.cs:560`; no edits to it.
- Scoped diff/owned-source whitespace checks pass. Source hashes are recorded in
  `/private/tmp/stats-pass5-source-hashes.txt`. A direct original/production renderer diff
  is `/private/tmp/stats-pass5-renderer-port.diff` (regenerate for final line positions).

### Remaining limits and next permitted action

- `CurrentEventHeaderAndEventScopedRoleNavigation` fails with a Board GET **404** at
  lines 539/556, before navigation assertions. Source evidence: lines 526–530 publish
  1×1 boards without tiles/requirements; `BoardApprovalFixture` defaults these to empty,
  whereas `PublicBoardService` requires a complete frozen tree. Those owners and this
  protected test were not changed in Pass 5. This indicates a fixture limitation; no
  pre-Pass-5 comparison was executed, so it is not conclusively labelled preexisting.
  The complete-fixture Stats HTTP journey separately exercises the modified event link,
  direct refresh and current-event Participant/Captain/Co-captain submission links.
- Independent source review found F1–F4 above. No browser automation, visual acceptance, live 69-metric
  request, mode-pair verification or catalogue population occurred. Unverified provider
  modes remain unavailable; the saved catalogue JSON's 311 missing IDs remain an operator
  gate. Earlier provider deferrals are preserved, not waived.
- Whole-slice release/format/acceptance gates remain required before release; this pass
  executed only its affected gates and reused unaffected earlier evidence. Packaging,
  publication, a user-owned database change or app restart is not authorized here.
- **Next:** consolidated user acceptance and deferred provider/operator gates above.
  F1–F4 implementation and same-reviewer recheck are complete. No new broad review,
  implementation pass or packaging is required/authorized by this handoff.

### Pass 4 completed implementation/review handoff
The original Pass 4 implementer (GPT-6 Astra xhigh) completed the bounded remediation
assigned by planner task `01a0a29f-5382-7120-96f5-69d422a832f1`. Prior dirty work is
preserved. No new agent/task/reviewer, broad source review, Pass 5, staging, commit,
push, deployment, user-owned database action or running-app change occurred.

Fresh reviewer `stats_pass4_review` (GPT-6 Astra high) previously completed the one
Pass 4 source review and reported F1–F3. Only those findings and their direct
consequences were changed. The SAME reviewer passed the F1–F3 recheck with no remaining
named or direct-consequence defects. The planner reconciled this verdict on 2026-09-15.
Implementation and independent source review are complete; implementer and reviewer are
stopped. Visual acceptance and provider checks remain deferred, not passed.

Named corrections:

- **F1 — cumulative aggregate order:** `PublicStatsService.Progress` carries each team's
  existing history sequence into the merge. Equal timestamps sort by stable team ID and
  that sequence, so cumulative snapshots cannot be reversed by submission-ID order.
  Complete-query proof uses four identical timestamps with deliberately opposing
  submission/contribution IDs; aggregate approved totals are 1,2,3,4, tile/line totals
  stay monotonic, final totals agree with the team, and reached milestones retain stable order.
- **F2 — automatic-end capture:** `EventLifecycleService.ExecuteScheduledEndAsync` now
  captures the compatible checkpoint after the state save and before the existing
  Serializable transaction commits, matching manual end. Actual scheduler proofs cover
  fresh batch → scheduled end → beyond two-hour freshness (same full result and original
  times, labelled stale), expired Live batch (no resurrection), and forced checkpoint-write
  failure (end transition, revision/invalidation boundary and checkpoint roll back together).
- **F3 — additive stale retention:** the existing event owns
  `StatsLuckInvalidatedAtRevision`, recording its last non-additive Stats revision. Pure
  approval advances only `StatsEvidenceRevision`; reversal, correction and other existing
  Stats revision writes advance both. Reads may retain an older full Luck calculation only
  from at/after that boundary, through the current revision, with all other compatibility
  checks unchanged. An older retained result is always stale and preserves its old numerator,
  denominator, nested results, evidence revision and original times. Writes STILL require
  the exact current evidence revision. Tests cover outage then approval, reversal of either
  the retained or newly added drop, no resurrection after another approval, completion
  correction, and rejection of an older writer even with the same fresh activity batch.
- Additive migration `20260915190625_RetainLuckAfterAdditiveApproval`, designer and model
  snapshot add only that event field and its bounded-revision check. Existing events initialize
  the boundary to their known current evidence revision; no older compatibility is guessed.
  PostgreSQL rehearsals preserve a matching checkpoint and reject an older uncertain one.
  DATA_MODEL.md describes this distinction and scheduled-end capture.

Correction evidence (all passing; 23 distinct cases):

- **10 correction PostgreSQL cases** plus **13 directly affected existing regressions**.
  `/private/tmp/stats-pass4-review-corrections.log`: 9/9 initial cases.
  `/private/tmp/stats-pass4-review-consequences.log`: 15/15, including the two migration
  variants (one repeats the initial matching-checkpoint case) and the 13 regressions.
  Existing checks cover source/assignment/team/competition fences, mid-query/fetch evidence,
  stale writers, aggregate team milestones, checkpoint rollback, 5×5 finalization/archive/
  unfinalization, and prior additive migrations. Unaffected original evidence below is reused.
- Final Web Release: zero warnings/errors, `/private/tmp/stats-pass4-review-release.log`.
  EF: no pending model changes, `/private/tmp/stats-pass4-review-model-check.log`.
  Integration builds use only the established CA1310 exception for the two preexisting
  CaptainScopedNavigationIntegrationTests.cs:560 warnings; that file remains untouched.
- Scoped diff check passes. Correction source hashes:
  `/private/tmp/stats-pass4-review-source-hashes.txt`. Approved HTML/CSS/JS prototype hashes
  remain exactly the three values recorded below.

The live provider/mode proofs and consolidated visual acceptance remain deferred under the
existing user decision. Pass 5 and packaging remain unauthorized. The one broad review
and bounded same-reviewer recheck are complete; no further review is needed without a
relevant new change. Next implementation requires the user's Pass 5 authorization.

Implemented owners:

- `Bingo.Application/Stats/IPublicStatsService` and the focused Infrastructure
  `PublicStatsService` projection: exact event access without preferred-event fallback;
  one consistent database snapshot; approved/non-reversed submission-time GP and histories,
  complete team/player/source lists, shares and valuable/repeated-item highlights using
  retained exact item IDs/artwork and frozen prices. Missing historical prices remain null.
- Luck deduplicates event outcome bases, counts actual submissions once, applies retained
  personal probabilities/parent mechanics/rolls and pools expected/received counts. It uses
  the existing regular Playing full-competition-delta approximation, excludes informational
  accounts, preserves evidence-character attribution, and exposes source/player waiting,
  incomplete, zero-recorded/estimated, stale and original timestamp states. A required public
  roster member with an unavailable primary/activity assignment is not silently omitted.
- Progress reuses `PublicProgressCalculator`, including caps and submission chronology.
  DTOs include team and aggregate hover totals, real row/column dimensions, scoped milestones,
  all-team thresholds and distinct contributed tiles. Official completion comes from existing
  finalization snapshots; Stats introduces no alternative official placement calculation.
- `BingoEvent.StatsEvidenceRevision`, `EventStatsLuckCheckpoint`, configuration and additive
  migration `20260915183337_AddEventStatsLuckCheckpoint` with designer/model snapshot.
  Existing evidence, synchronization and lifecycle/final-review owners transact revisions
  and compatible checkpoint capture. Full public JSON calculations are versioned and bounded;
  source/basis, activity batch/generation, competition, assignment/public-roster and lifecycle
  keys are compared under the event lock before writing. Old writers cannot replace newer
  revisions. The existing reset's explicit dependency list includes the new table.
- A successful compatible activity batch must still be inside its two-hour freshness window
  to create a replacement calculation. Outages/partial observations may retain only a
  compatible prior full result with old times and a stale label. Reversal or incompatible
  source/assignment/competition/lifecycle changes invalidate that presentation. When no usable
  replacement exists, including an expired Live batch after finalization, Luck is waiting or
  incomplete. Prices, first-approved mechanics and retained official history remain intact.

Initial implementation evidence (55 unique PostgreSQL cases; reuse only where unaffected by F1–F3):

- **35 new Pass 4 cases**: 28 scenarios observe 46 complete query DTO results, six query access
  denials, and one additive-migration-only scenario. Coverage includes all three required
  formula examples; later then earlier approval/reversal; weights/duplicate eligibility;
  two distinct items at one completion time; regular accounts/informational alt/team pooling;
  missing/zero/-1/mode-unverified states; retained price/artwork identity; complete and partial
  checkpoints; mid-query evidence commits; evidence/board/new-objective/mapping changes during
  fetch; stale writers; assignment/team/competition replacement; collective milestones and
  ended unreached states; real 5×5 completion correction/finalization/archive/unfinalization;
  schema/payload bounds and checkpoint-failure rollback of the approval/revision together.
- **20 directly affected existing regressions**: EHB/lifecycle, lease/generation/source fences,
  finalization-versus-review races, transaction rollback and the prior Pass 3 additive-migration
  proof. The latter's fixture is now created before its intentional downgrade, so the current
  event model does not attempt to insert its new column into an older schema.
- `/private/tmp/stats-pass4-connected.log`: 52/52 (33 new + 19 regressions).
  `/private/tmp/stats-pass4-checkpoint-final.log`: 3/3 (one new bound check plus two relevant
  reruns after final serialization). `/private/tmp/stats-pass4-prior-migration.log`: 1/1.
  `/private/tmp/stats-pass4-atomicity-final.log`: 2/2 (new atomicity proof + extended lifecycle).
- Final Web Release: **zero warnings/errors**, `/private/tmp/stats-pass4-release-final.log`.
  EF model consistency: no pending changes, `/private/tmp/stats-pass4-model-check.log`.
  Integration builds retain only the established `-p:WarningsNotAsErrors=CA1310` exception
  for the two preexisting CaptainScopedNavigationIntegrationTests.cs:560 warnings.
  That unrelated file was not changed by this pass. Scoped `git diff --check` passes.
  Scoped final source hashes: `/private/tmp/stats-pass4-source-hashes.txt`.
- Approved prototype hashes still match HTML
  `fcd542de6afb2c0889435423b15d461ec601566dee8bee755cc5ca7b9112ed26`, CSS
  `88d4c40780c5c675600e736ec80c1e8f6975cb5a7c2ff83693e9f43f70eff95d`, and JS
  `50d6f6512117cd863c4c0a4f9e7a2d3a9d1dcbd2e13e365b3379b656cd598b24`.

Acceptance and next permitted action:

- Initial implementation and bounded F1–F3 remediation/executable verification are complete.
  The same reviewer's named recheck is pending. No passing recheck or visual acceptance
  is claimed; do not start another broad review or Pass 5.
- The user's explicit deferral of the live 69-metric WOM request and five mode-pair proofs
  remains in effect until final acceptance. Affected modes remain unavailable even when raw
  counts exist. Controlled provider fixtures are not real-provider sign-off. Existing deferred
  gates remain in MANUAL_TEST_CHECKLIST.md; this pass did not retry the external gate.
- All visual acceptance stays consolidated after all five passes under UI_PAGE_MATRIX.md.
  Pass 5, packaging and deployment still require their own authorization. No user database or
  running app action is a next step for this implementer.

### Pass 3 implementation/checks handoff — 2026-09-15

Implemented in `/private/tmp/BingoWebpage-drop-announcements`, branch `drop-announcements`.
Existing dirty work is preserved. No subagents, staging, commit, push, deployment,
user-owned database population/reset, app restart or later pass occurred. Implementation
and focused behavior checks are complete; final Release Web build passes with zero warnings/errors.
Independent source review passed. The remaining work is the named real-provider
verification gate, separately from any later-pass authorization. No visual acceptance
or full provider verification is claimed.

Implemented owners:

- Existing WOM client/interface: one repeated-plural `metrics` request for EHB and distinct
  bound required boss metrics. Per-metric parsing ignores total/deprecated progress; malformed
  or duplicate boss data cannot erase usable EHB. Existing case-insensitive EHB parsing and
  the shared limiter remain intact. Missing provider timestamps remain missing.
- `EventLuckOutcomeBasis` and `BoardPublicationQueries.Luck`: first approved outcome mechanics,
  retained approval/drop/boss identities and time, one validated metric binding and source
  revision. Duplicate placements share the first basis; later rate/mapping edits preserve it.
  Missing identities/conflicting earliest approvals are explicitly unavailable. No guessed
  historical backfill; existing rows initialize lazily only from proven retained approvals.
  Approval/publication capture is atomic with the existing snapshot/audit transaction.
- Existing board publication projection and public DTO now propagate the frozen roll/parent/
  personal-mechanics fields. Actual public DTO execution confirms the propagation.
- `EventCompetitionCharacterMetricActivity`: raw counts, coverage/estimate state, latest issue,
  original fetch/upstream time, source fingerprint and batch. Bad/missing/failed observations
  keep usable raw data with its actual origin. Initial invalid observations expose their issue
  with null activity/fetch time. Approved-source-drop presence is an explicit availability input;
  the agreed -1/zero cases do not invent kills or a Luck score.
- Existing synchronization owner: event-lock and lease/expiry/generation/competition/assignment
  fencing plus pre-fetch source fingerprint and commit recheck. Re-read tracked bases after
  another transaction binds a mapping in flight. Complete EHB can coexist with incomplete
  boss activity. Raw-cache reads use a consistent snapshot, recheck compatibility, and expose
  incomplete/stale state without HTTP or score calculation.
- Additive `20260915174600_CacheEventCompetitionBossActivity`, designer and model snapshot.
  PostgreSQL keys/checks and an immutable-basis UPDATE trigger protect storage, permitting
  only the first metric binding. Existing EHB storage remains unchanged.
- Direct integration correction: Development reset's explicit transaction/table list includes
  both Pass 3 tables and the previously omitted Pass 2 `event_item_prices` dependency. No CASCADE
  or authorization change. Only isolated fixtures were reset for tests.

Executed evidence (passing union; unaffected passing cases are reused):

- **13 pure rules tests**: `/private/tmp/stats-pass3-domain.log`.
- **7 synthetic HTTP tests**: `/private/tmp/stats-pass3-http-final.log`; full known metric request,
  multiple/missing/malformed/duplicate metrics, heterogeneous-total exclusion and preserved EHB.
- **28 new PostgreSQL cases** across the focused runs: 26 passed in
  `/private/tmp/stats-pass3-postgres.log`, plus the mapping-binding-during-fetch and oversized
  metric cases passed in `/private/tmp/stats-pass3-regressions.log`. These cover raw replacement/
  retention, every agreed count case, multiple regular accounts/swaps/informational alts,
  lease/generation/competition/assignment changes, board/new objective/mapping changes during
  fetch, read fencing, actual approval rollback/first basis/duplicate outcomes/later rates,
  ambiguous legacy history, unverified modes, and additive migration preserving existing EHB.
- **34 existing EHB/approval regressions passed** across the 41-pass regression run and the
  three corrected reset/render cases. `/private/tmp/stats-pass3-regressions.log` originally
  had three explicit-reset FK failures; `/private/tmp/stats-pass3-final-corrections.log`
  passes all three plus the extended real public DTO mechanics assertion (4/4).
- Final Web Release build passes with **zero warnings/errors**: `/private/tmp/stats-pass3-release.log`.
- Migration/model consistency passes: `/private/tmp/stats-pass3-model-check.log`. Scoped diff
  checks pass. Integration builds use the established `-p:WarningsNotAsErrors=CA1310` for
  the two preexisting warnings at CaptainScopedNavigationIntegrationTests.cs:560; that file
  was not changed by this pass. No broad unchanged Pass 2 suite rerun was needed.
- Stats prototype hashes still exactly match HTML `fcd542de6afb2c0889435423b15d461ec601566dee8bee755cc5ca7b9112ed26`,
  CSS `88d4c40780c5c675600e736ec80c1e8f6975cb5a7c2ff83693e9f43f70eff95d`, and
  JS `50d6f6512117cd863c4c0a4f9e7a2d3a9d1dcbd2e13e365b3379b656cd598b24`.

Provider execution gate remains separate and unresolved:

- [Official competition endpoint documentation](https://docs.wiseoldman.net/api/competitions/competition-endpoints)
  documents repeated plural metrics. Current official provider source at
  `adde6ac93b8a631e58f02384c28e8d98f0061812` verifies plural query validation, per-requested-metric
  delta generation, direct named Jagex score mapping, and Maggot King/Zalcano candidates.
  The rates list alone was not used as request/semantic proof.
- The full 69-metric browser request (EHB plus 68 saved/candidate catalogue metrics) failed
  with `net::ERR_BLOCKED_BY_CLIENT`; no competition participant payload was saved. Full live
  request coverage is still unverified. Each actual batch checks every required character/
  metric rather than treating a supported metric name as observed coverage.
- CoX/CM, ToB/HM, ToA/Expert, Gauntlet/Corrupted and Nightmare/Phosani remain explicitly
  `ModeSemanticsUnverified`. Focused primary searches and provider-owned hiscores fixtures
  did not prove independence/overlap; the fixture mode counts were unranked. No subtraction,
  invented split or permanent unsupported classification is implemented. Further primary
  evidence is required before enabling those source pairs.

Pass 4 evidence revisions, Luck calculations/checkpoints and Stats queries are now implemented;
see the active Pass 4 handoff above for execution evidence and the pending independent review.
All page visual acceptance remains deferred to the user's consolidated post-Pass-5 checklist,
owned by UI_PAGE_MATRIX.md. This handoff does not authorize the next pass or packaging.

### Pass 2 implementation and verification handoff — 2026-09-15

Implemented directly in `/private/tmp/BingoWebpage-drop-announcements`, branch
`drop-announcements`; prior dirty work is preserved. No subagents, staging, commit,
push, deployment, user-database population, running-app change or later pass occurred.
Implementation and focused execution are complete. Planner assigned one fresh read-only
independent reviewer, `stats_pass2_review` (GPT-6 Astra high), for Pass 2 and its direct
integration consequences. One broad review is complete with two bounded findings:
F1 clear rejected-candidate metadata when the API mapping identity changes, preserving
unchanged-mapping outage/missing flags; F2 localize the named missing-price start-readiness
blocker at its actual presentation boundary. No other scoped defects were reported.
The existing implementer completed both corrections and focused handler/presentation
checks. The SAME reviewer passed the bounded F1/F2 recheck with no remaining named or
direct-consequence findings. The planner reconciled the verdict on 2026-09-15.
Implementation and independent source review are complete; prior unaffected passing
evidence remains valid. No further broad review or subsequent pass has started.

F1/F2 corrections accepted by the same reviewer:

- F1: `CatalogueItem.ConfigureApi` clears rejection metadata only after a real normalized
  identity change, including removal. Unchanged IDs (including equivalent leading-zero
  formatting) preserve the flag through missing-price/outage responses. Existing
  save/validate handlers persist the correction and its audit; accepted-value behavior
  remains unchanged.
- F2: the named DROP_PRICE_MISSING blocker carries its affected names as description
  arguments. Manage localizes only that blocker at presentation, for readiness/overview
  and failed-start feedback, using the existing Danish resource. Other blocker wording,
  lifecycle rules and persisted attempt/audit semantics remain unchanged. No markup,
  CSS, JS, provider, schema or migration change was needed for these corrections.
- **12 focused PostgreSQL/HTTP checks passed**: 8 F1 mapping save/validate cases, an actual
  Danish Manage GET plus failed-start POST/toast case, and 3 direct guard/start regressions.
  Filter: StatsPass2Review; StatsPass2GuardRetainsTrustedValue;
  StatsPass2GuardAuditFailure; StatsPass2MissingPriceAfterPublication.
  Log: `/private/tmp/stats-pass2-review-corrections.log`. The Danish check asserts the
  rendered named readiness text and the exact translated toast span, not merely a localizer.
- Release Web build passes with **zero warnings/errors**
  (`/private/tmp/stats-pass2-review-release.log`). Integration build retains the same two
  preexisting CA1310 warnings under the established exception. Scoped diff checks pass;
  the prior 75 integration/28 pure/migration/provider evidence is reused where unaffected.
  The independent reviewer verified these logs/source checks and passed the named
  recheck. No additional suite rerun or visual acceptance was claimed.


Final approved behavior:

- Starts capture available catalogue-wide values from the last completed UTC hour before
  ActualStartedAt, using safe midpoint/one-side/zero handling and catalogue fallback.
  Unused entries with neither value receive no invented row and do not block start.
- Missing eligible-drop prices are rejected at tile add/edit, approval/publication and
  start readiness. Stored zero and objective-only tiles with no drops are valid. Live
  corrections accept the existing frozen item value even if the catalogue later changes.
- Any first introduction without an existing event value requires and freezes its current
  catalogue value at successful published correction approval. This covers new identities
  and originally unused/unpriced identities. No late historical fetch or unpriced-ID set.
- The user approved rejecting later API candidates strictly outside 0.5×–2× of a trusted
  positive catalogue value and positive↔zero transitions. Exact boundaries and zero→zero
  pass; a missing baseline supplies no comparison. Initial population still needs operator
  checking. Frozen event values never reprice. Volume/history detection and scheduled
  catalogue refresh were not authorized.

Implemented owners:

- Domain EventItemPrice has immutable integer value, original selected hour, actual capture
  time, exact API identity and observed/catalogue fallback provenance. BingoEvent owns the
  start-capture marker. CatalogueItem owns rejected candidate/observation-hour metadata,
  separate from mapping validity, with safe ratio arithmetic and manual/untradeable
  precedence. Accepted API/manual values clear the flag; missing/outage leaves it.
- CatalogueApiClient uses explicit `/1h?timestamp=...`, verifies response hour, preserves
  the exact User-Agent and bounds the client/hour cache. EventItemPriceService prepares
  outside transactions and captures inside existing lifecycle/publication boundaries.
  Manual/scheduled start uses the actual start clock after preparation; a changed bucket
  falls back without mislabelling old API data. Event row locks/version and serializable
  transactions preserve singleton/readiness/audit/activation rollback and uniqueness.
- Board create/edit/approval/publication guards use real frozen item identities. Successful
  published correction inserts first-use values under the event lock. No-op/retry/resume
  preserves rows. Catalogue rebind retains an old item identity referenced by event prices.
- Approved guard is applied to explicit catalogue API validation, operator report/apply and
  start candidates. Start rejection uses PriceMoveRejected fallback; flag and audit writes
  roll back with the start. Existing API panel/operator feedback explains retained values;
  snapshot v2 round-trips the optional rejection metadata, with v1 compatibility retained.
- Additive migrations `20260915165204_FreezeEventItemPrices` and
  `20260915170124_GuardSuspiciousPriceCandidates`, designers and model snapshot are present.
  Composite PK, nonnegative/zero/provenance constraints and immutable UPDATE trigger protect
  price rows. No historical backfill: prior starts (including reconstructed Sommerbingo)
  keep null capture marker and no asserted price history. Existing imported history remains.

Executed checks:

- **28 pure tests passed**: CataloguePricingTests, EventPriceHourTests, EventItemPriceTests;
  UTC offsets/exact rollover, overflow-safe midpoint and ratio boundaries, one side, zero,
  missing, provenance, fixed-value preservation and rejected-flag lifecycle.
  `/private/tmp/stats-pass2-domain.log`.
- **75 focused integration tests passed** in the final run: 28 new Pass 2 cases, all 26
  CataloguePriceHttpTests, plus the selected existing lifecycle/board/catalogue regressions.
  `/private/tmp/stats-pass2-final-integration.log`. Filter: StatsPass2; all
  Slice3ScheduledLifecycleIntegrationTests; CataloguePriceHttpTests;
  PrivateApprovalFreezesOneSnapshot; FinalizedRosterPublishesTheActiveApproval;
  ConcurrentPublicationHasOneWinner; ConcurrentApprovalCreatesOneCoherentSnapshot;
  DraftBoardUsesCurrentCatalogueRates; StatsPass1AdditiveMigrationKeepsLegacyValueMissing;
  StatsPass1SharedItemSave; StatsPass1ConcurrentItemEdit.
  This executes real disposable Testcontainers PostgreSQL for manual/late scheduled start,
  HTTP-hour crossing, outage fallback, concurrent/repeated start, snapshot/audit rollback,
  catalogue edits/resume, zero/no-drop objectives, later cleared prices, catalogue mutation
  racing tile add, real late published introductions, missing/zero and concurrent/retry
  corrections, candidate rejection/rollback/stale edit, operator report read-only behavior,
  snapshot round-trip and additive migration/immutable constraints. Historical HTTP tests
  use synthetic responses. Earlier 21-case core run and 25/26 guard run are superseded by
  this final pass; the latter's only failure was an overbroad fixture report assertion.
- Web Debug and Release builds pass with **zero warnings/errors**. Integration build uses
  the established `-p:WarningsNotAsErrors=CA1310` only for two preexisting errors at
  CaptainScopedNavigationIntegrationTests.cs:560; that unrelated file is untouched by
  this pass. Commands use `/usr/local/share/dotnet/dotnet`, `--disable-build-servers -m:1`.
  Logs: `/private/tmp/stats-pass2-web-build.log`, `/private/tmp/stats-pass2-release.log`,
  `/private/tmp/stats-pass2-integration-build.log`.
- EF reports **no pending model changes** (`/private/tmp/stats-pass2-model.log`). Scoped
  diff/whitespace and resource XML checks pass. All three approved Stats HTML/CSS/JS hashes
  match the assignment baseline. Saved catalogue still has **311 items / 311 missing IDs**;
  no deployed coverage is claimed. Local test-runner sockets and Docker required successful
  sandbox escalation; all persistence checks used disposable stores.

Limitations/next permitted action:

Independent review and planner reconciliation are complete. Implementer and reviewer
are stopped. Pass 3 needs its own authorization; packaging and deployment are not
included. The price guard is a simple approved sanity check, not
proof of authentic market pricing. Initial catalogue population/operator review remains
outstanding. Admin API-panel/outcome visual acceptance remains pending; the new candidate
message is not manually accepted by prior panel composition approval. UI_PAGE_MATRIX.md
owns approvals; approved Stats production UI remains untouched and no UI port is included.

The user accepted the completed Stats prototype: "I think that just might be the UI
completed". UI_PAGE_MATRIX.md owns final page approval. This includes all sections,
exploration controls, guidance, animations, the final narrow header/milestone spacing
corrections and "All teams completed the board" wording. The composition and interactions
are now locked. This acceptance supersedes earlier pending visual/trial statuses below.

Explicit user requirement: production must port the actual approved Stats markup,
CSS and rendering/interaction/animation code, not recreate the page using it as a visual
reference. UI_PAGE_MATRIX.md owns the Stats production port contract; DELIVERY_PLAN.md
§17.1/Pass 5 now require it and source-to-production verification. Exclude the prototype
header/masthead and bottom demo controls; use the existing app shell. Adjust artwork is
the only existing bottom control retained, for Super Admins. In-section controls stay.
The later explicit Pass 1 authorization below starts only catalogue implementation.

The approved artifact remains the standalone prototype in this checkout:
`prototypes/stats/outputs/stats-page-prototype.html` with its CSS/JS, previewed at
http://127.0.0.1:8766/stats-page-prototype.html. Existing focused executable checks used
DOM/animation stand-ins; user review supplies visual acceptance. Sample data remain
illustrative, not calculation or persistence contracts.

PRODUCT_REQUIREMENTS.md §15.1 records the agreed data/calculation decisions:
submission-time history (including out-of-order approvals), reversal recalculation,
rows/columns without diagonals and no post-completion cutoff for Luck.
Drop value pricing rules are agreed in §15.1. The user superseded the probability-based
Luck score with rate-based overall luck (ahead/on/behind rate), with plain-language
guidance. The combined formula is agreed: sum expected eligible item counts and compare
with approved item-submission count via (received / expected - 1) × 100. Two items from
one kill count twice; progress weight and rarity do not multiply received count. Concrete
math examples have been checked locally; source-mapping coverage remains a readiness item. Missing/zero
activity handling is now user-agreed in §15.1: explicit no-activity/waiting states,
last successful results with update time on sync failure, and incomplete team results
when required player data are missing. The catalogue Team content information bar confirms rates already represent
personal own-name probabilities under the configured team-size/raid assumptions; reuse
those rates and roll counts without another team-size adjustment. The
WOM documentation supports multi-metric competition deltas; our client currently only
extracts EHB and uses singular metric=ehb, whose compatibility needs verification.
The user also approved an expandable catalogue API mapping section (§9.4): existing
boss WOM metric and item pricing ID fields, suggested/editable mappings, explicit validation,
clear status and graceful fallback. The bounded catalogue API sections/backend are implemented in Pass 1; manual visual acceptance remains pending.
Direct terminal requests are blocked by Cloudflare. The user supplied a successful browser
JSON response for competition 145197 (Sommerbingo), requested with metrics=ehb,
metrics=vorkath and metrics=zulrah. Verified 93/93 participants each have deltas for all
three plus total. One Zulrah metric is -1/-1 with gained 0. User-approved unranked handling
is recorded in §15.1: both ranked use gain; -1 start/ranked end assumes zero baseline
and marks estimated; -1/-1 without source drops is zero recorded activity (not a team
failure), with an approved drop waits for source activity; ranked/-1 retains last usable
result and flags the issue. These are explicit approximations, not proof of zero kills. This validates the three-metric response shape, not all
catalogue metrics or the existing singular parameter. No participant payload is committed.

DELIVERY_PLAN.md §17 contains the five-pass plan and §17.6 now records the user-authorized
readiness source inspection and concrete first-pass brief. Catalogue JSON has 311 items
with no pricing IDs and 68 sources (66 mapped), so ID population is a real first-pass
prerequisite. The user's price response has been parsed: 2,885 entries, 1,765 two-sided,
1,120 one-sided, hourly timestamp 2026-09-15 11:00 UTC. This proves shape, not catalogue
coverage. The price client must send the exact agreed User-Agent including @chrisschmidt.

Account activity attribution is resolved by FUNCTIONAL_CONTRACTS.md §9.6: every regular
PLAYING assignment may contribute its full competition delta; informational alts are
excluded. Do not add per-swap WOM snapshots. Full requested-metric coverage and exact
mode semantics remain Pass 3 contract checks; docs plus the successful three-metric
response do not establish unlimited request size or all mode mappings.

The user resolved the two product choices: retrospective Stats for the reconstructed
Sommerbingo import are excluded; an entirely new catalogue identity introduced after start
uses its original start-hour price if available, otherwise its catalogue value when added,
frozen once with accurate provenance. Existing snapshots remain unchanged. These decisions
are recorded in PRODUCT_REQUIREMENTS.md §15.1 and DELIVERY_PLAN.md §17.

### Pass 1 implementation and verification handoff

Checkout: `/private/tmp/BingoWebpage-drop-announcements`, branch `drop-announcements`.
The existing dirty work is preserved. Implementer task:
`01a0a573-d620-7133-9f6b-8ef59d96719b` (GPT-6 Astra xhigh), continuing the authorized
catalogue mapping/pricing pass. No implementation subagents were used.

Implemented:

- Existing item/source identifiers with explicit exact suggestions/validation and mapping
  status/time. Changed item IDs invalidate verification and old API values; manual and
  untradeable values remain explicit. New items require a fetched/manual value or an
  explicit untradeable zero; existing shared items retain their value.
- Bulk cached Wiki mapping/hourly prices, exact raw User-Agent, safe midpoint/one-sided
  fallback, bounded timeout/retry and malformed/outage handling. WOM reads boss metric
  keys only through the existing limiter; numerical efficiency rates are untouched.
- Collapsed API sections inside existing Admin Catalogue editors, exact matched identity,
  Danish strings, shared image cache, explicit submitter actions and stale-item recovery.
- Serializable audited operator report/apply service and documented commands. Report is
  read-only; apply requires an active Super Admin. Snapshot v2 exports and v1-compatible
  imports preserve appropriate metadata and personal rate/roll fields.
- Additive `20260915142649_AddCatalogueApiMappingAndPrices` migration, designer and model
  snapshot. Legacy GP stays nullable/Missing, with database price invariants. Existing
  unrelated model-snapshot changes remain intact.

Executed evidence (2026-09-15):

- Focused Domain pricing tests: **9 passed** (safe rounding, one-sided/missing/zero,
  mapping invalidation and fixed-price precedence).
- Synthetic provider HTTP tests: **15 passed** (exact header, caching, malformed/partial
  responses, failure retry, cancellation and WOM exact keys/shared limiter).
- Isolated Testcontainers PostgreSQL: **11 focused persistence cases passed** for shared
  item/rate preservation, stale edit and concurrent rebind rejection, editor/operator
  audit rollback, report read-only behavior, actor authorization, snapshot v1/v2 and
  additive migration/constraints. The later rendered HTTP fixture separately passes
  authorization/antiforgery, no page-fetch behavior, primitive binding, suggestions,
  unsupported modes, new-item values and shared-value preservation. The final named recheck
  for malformed optional add-item numeric binding also passes. Total: **12 distinct
  focused PostgreSQL/HTTP cases passed**, with applicable earlier evidence reused.
- Web Debug and Release builds pass, zero warnings/errors. Integration build passes with
  `-p:WarningsNotAsErrors=CA1310`; the ordinary solution build is blocked by **two existing
  CA1310 errors at CaptainScopedNavigationIntegrationTests.cs:560**. That unrelated file
  is unchanged by this pass. Logs: `/private/tmp/stats-pass1-build.log`,
  `/private/tmp/stats-pass1-release.log`, `/private/tmp/stats-pass1-http-forms.log`.
- Existing Catalogue Node checks pass in normal and direct-load-failure modes; expanded
  tests execute explicit suggestion, late-result protection and submitter serialization.
  A fresh headless Chrome run using synthetic form markup plus actual production scripts
  passes collapsed-section interaction, no typing/render fetch, native numeric validation,
  suggestion, antiforgery form value, submitter and informational-feedback checks.
  Script: `/private/tmp/stats-pass1-browser-check.cjs`. This is behavior evidence, not
  production visual acceptance. Bundled Chromium was absent; installed Chrome ran in a
  fresh disposable profile without using the user's browser session.
- EF reports no pending model changes. Added Razor string localization, resource XML,
  scoped diff/whitespace checks pass. Approved Stats HTML/CSS/JS SHA-256 values remain
  exactly those recorded by the task brief. Saved JSON still has 68 bosses, 311 items,
  441 drops and **311 missing item IDs**; this is not deployed coverage evidence.

Authorization/next boundary:

The prior single read-only readiness reviewer passed its bounded plan recheck; do not
repeat readiness. The user now explicitly authorizes one separate **GPT-6 Astra xhigh**
independent code review after focused checks. Send findings/blockers only to the implementer
above, remediate named findings and return to that SAME reviewer until pass. Send a passed
verdict only to originating planner `01a0a29f-5382-7120-96f5-69d422a832f1`. These task-specific
handoffs are authorized by the user's current instruction. Apply
[the lean handoff procedure](DELIVERY_PLAN.md#421-lean-execution-and-planner-handoff).
The independent Astra xhigh reviewer is task `01a0a58b-0316-7ce0-962c-60db1c81e61c`.
Its one broad source review returned **Requires remediation**, with three named findings:
F1 bulk source matching used historical slug after a rename; F2 item-ID invalidation could
claim the old API value was unchanged; F3 fetch-and-add hid failed provider attempts and
manual fallback provenance. All three named corrections are implemented:

- F1 uses a unique exact match against the current activity name when no explicit metric
  exists; explicit configured metrics remain authoritative for validation.
- F2 derives cleared/retained-value feedback from the actual before/after state, including
  save-without-validation and missing/outage outcomes, with a manual-value/retry next step.
- F3 retains the attempted mapping status/time for new items with an ID, reports mapping
  outage/unsupported ID/no unique match/price outage/missing price distinctly, and explains
  a saved fixed manual fallback or why no item was added. Plain Add/shared-item paths keep
  their no-provider-fetch behavior. New outcome strings are Danish-localized.

Focused remediation evidence: **21 distinct PostgreSQL/HTTP cases pass**: 2 renamed-source
cases, 8 cleared/retained-value cases, 10 real HTTP fetch-and-add outcome cases, and the
existing explicit-fetch/plain-Add/shared-item form regression. The initial run passed 19;
its two F1 fixtures collided with the seeded Zulrah slug before exercising product code.
Those fixtures now rename the existing controlled seed row, and the named two-case rerun
passes. Logs: `/private/tmp/stats-pass1-remediation-tests.log` and
`/private/tmp/stats-pass1-remediation-f1.log`. Final Web Release build passes, zero warnings
or errors (`/private/tmp/stats-pass1-remediation-release.log`). Integration build passes
with the same two preexisting CA1310 warnings under the established flag
(`/private/tmp/stats-pass1-remediation-build.log`). Scoped diff/whitespace and resource XML
checks pass. Prior provider/Domain/browser/migration evidence is reused where unaffected;
no JS, Razor, CSS, Domain, migration, snapshot or approved Stats source changed in this
remediation. Named six-file diff: `/private/tmp/stats-pass1-remediation.diff`, SHA-256
`f932aa9f615d11ada65249613ea64354d1dba403cc3551c655a25b0ae1a85266`.

The same reviewer passed the bounded F1–F3 recheck with no unresolved scoped findings.
After the user explicitly approved the previously rejected handoff in the reviewer task,
the completed verdict reached originating planner `01a0a29f-5382-7120-96f5-69d422a832f1`.
The planner reconciled that passed verdict on 2026-09-15; the delivery blocker is resolved.
Implementation and independent source review are complete. No second broad review is
needed. Implementer and reviewer have stopped at the assigned Pass 1 boundary.
User visual acceptance of the
expanded API sections/outcomes remains pending; UI_PAGE_MATRIX.md owns approval. No
Pass 2/Stats production UI, user DB apply, running-app change, staging, commit, push or
deployment occurred. Stop after review/handoff; do not start another pass.

### Prior trial evidence (superseded by final UI acceptance)

- User clarified the narrow milestone issue was spacing between stops and approved
  the incidental header alignment correction. At ≤850px, stops now size to their
  headings with a 170px minimum rather than a fixed 250px desktop slot. Long headings
  retain their single line; detail text truncates within that width. The final stop
  still has no extra trailing slot. CSS-only; visual spacing awaits user review.

- Narrow stacked header correction (≤560px): Drop value uses a left-aligned title
  above tabs, with the compact search icon beside the tabs and no divider stub.
  Event milestones keeps its selector directly beneath the title at the left;
  the hint shares the control row when space permits. CSS-only; wide layout and
  interaction scripts are unchanged. Scoped cascade/whitespace checks passed;
  user visual acceptance is pending.

- Board progress now reveals lines/markers along one shared 1200ms time sweep, with
  tile/row totals advancing at their actual sample completions. Load and event-size
  changes animate; hover/focus, scrolling and reduced motion finish the entrance before
  exploration. Guidance starts open, dismisses/reopens like Luck, and adds no layout row.
  All three info icons share Luck's font, muted closed color and accent open/hover color.
- Focused executable DOM stand-in checks pass for sweep/count synchronization, completion,
  interruption, hover/focus, reduced motion, help lifecycle and 2/3/4/5/8/15 teams.
  Existing Drop value, Luck and milestones checks also pass. Syntax/source/cascade checks
  passed; actual browser appearance and timing await user review. No browser automation.

- User-requested pacing refinement: initial Drop value chart 1100ms and donut 1200ms
  with gentler easing; initial Luck bars/counts 1000ms (later interactions retain 420ms).
  Event milestones rail draws linearly from the first point over 1400ms on load/team
  switch, filling dots as it reaches them and revealing the latest ring afterward.
  Team-switch fade is 280ms. Focused timing, state, interruption and reduced-motion
  harnesses pass; visual timing remains pending user review.

- Renamed the visible section Event milestones and footer selector Milestone stage.
  Added a 480ms initial completed-rail draw, 180ms team-switch fade, 280ms position
  transitions, reached-dot fill and forward rail/ring transitions. Stage changes retain
  scroll; scope changes return to the start. Identical renders do not replay motion,
  newer renders cancel old animations and reduced motion applies final states.
  Focused milestone geometry/motion and Luck/Drop value regressions, syntax and whitespace
  checks pass with DOM/animation stand-ins. Visual timing remains awaiting user review.

- Ended timelines now label unfinished milestones Not reached with a muted × inside
  the circle; live-event milestones still say Upcoming. Focused tests cover both scopes,
  ended/live transitions, accessible state text and no fabricated timestamps.
- Timeline selector sizes to the selected content with a tighter 2px focus offset and
  right-aligned mobile wrapper. Scoped CSS/cascade, timeline checks and whitespace pass;
  user visual review pending.

- Timeline refinement: submission drop artwork is now a 16px inline icon after the
  player/team detail, with item-name hover/accessible text and no extra line. Team scope
  says Board completed for both reached/pending states; event scope retains First board
  completed. Focused syntax, timeline regression and CSS/diff checks pass; visual review pending.

- Timeline edge/artwork correction: scroll-snap padding now matches the dot/ring gutter,
  preserving the first marker at initial scroll. The last stop sizes to its content and
  snaps at the end, removing the unused fixed-width tail. Removed the detached timestamp
  item image. Scoped CSS/cascade, timeline executable checks and whitespace pass; user
  visual review remains pending.

- Latest timeline refinement checks pass: all four collective completion thresholds,
  missing-team prevention, 11/7 slot counts, stage/scope sorting, latest-point marking,
  Upcoming timestamp semantics and artwork above the rail. Protected script/CSS source
  comparison and existing Drop value/Luck checks pass. Visual review remains pending.

- Timeline trial implemented: eleven default milestone slots (seven per team), Start
  fixed first, completed timestamps ordered chronologically, pending slots muted in
  default order. The same-time tie order is stable; End can precede unreached slots.
  Approved-submission firsts use sample players/artwork; tile/row/halfway/board sample
  times align with the existing Board progress snapshots. Each detail is one truncated
  line. Updated 250px stops fit collective titles; the 24px timestamp/artwork row and
  52px copy area preserve the prior total strip height. Dot centers share the text anchor: solid
  reached dots, outer ring on the latest, hollow upcoming dots, and muted future rail.
  Four collective milestones cover approved submissions, tiles, rows and boards.
  Footer Timeline stage options exercise start, early, first week, current and ended
  states without changing the other charts. These remain illustrative sample states.
- Checks: focused `/private/tmp/check-stats-timeline.cjs` covers all stages/scopes/team
  counts, chronological/default/tie order, all-teams threshold, end-before-pending,
  timestamp semantics and artwork. Existing Luck/Drop value regressions, syntax, protected
  renderer comparisons and whitespace checks pass. DOM stand-ins only; visual review pending.

- Luck now has ⓘ beside its heading with the user-approved Teams and Players copy.
  The matching help overlay starts open, dismisses with ×/Escape and reopens with ⓘ.
  Guidance opens on every page load; individual dismissals last for the current page
  visit only. Old localStorage dismissal is ignored. View changes update text without
  reopening it. The future shared Stats Hide tooltips preference remains deferred. Help/search dismiss each other to avoid overlapping overlays. Focused
  syntax, Luck help/interaction and Drop value regression checks passed; visual review pending.

- Removed the Luck search input’s focus outline at the user’s request. Existing field
  border remains. Scoped CSS/cascade and whitespace checks passed.

- Luck search now matches Drop value’s compact popover: 34px field, 11px type,
  matching border/padding/focus, up to 320px wide and anchored below the heading.
  Results appear in a separate matching popup; empty results create no blank panel.
  Scoped CSS/cascade and whitespace checks passed; visual review pending.

- Comparison removal now fades/collapses over 220ms, then removes the row while
  preserving list scroll. Repeated clicks do not restart it; a replacement/view change
  cancels stale removal callbacks. Reduced motion removes immediately. Focused syntax,
  Luck interaction/motion and whitespace checks pass. User is considering a Luck info
  popover, now implemented as described above. Latest removal motion awaits visual review.

- Latest Luck motion trial: existing-result selection uses native smooth list scrolling
  (instant for reduced motion), preserving the complete-row bottom target. Team heading
  changes fade/slide over 180ms. New comparison rows expand/fade over 220ms within the
  fixed viewport; replacement comparisons fade without expanding. Only the new comparison
  bar/percentage count from zero. Focused syntax, Luck/Drop value motion and whitespace
  checks passed using DOM stand-ins; user visual acceptance pending.

- Luck search scroll correction: row offsets were already relative to the positioned
  list; subtracting the list’s own offset mixed coordinate systems. Selection now
  bottom-aligns the complete result row using list-local coordinates. Regression checks
  cover a nonzero panel offset, compact viewport, early results and pinned selection.
  Syntax, focused Luck harness and whitespace checks passed; visual review pending.

- Luck comparison now stays sticky at the top of its scrolling rankings. Its opaque
  surface matches the card (no tinted highlight) so rows scroll underneath cleanly.
  Removal stays beside the name. Scoped CSS/cascade and Luck interaction checks pass;
  user visual review pending.

- User-requested Drop value correction: removed the pinned contributor row’s left
  inset accent while preserving its background highlight. Scoped CSS/diff checks passed.

- Luck refinement: search placeholder now says “Search players or teams…”; removed
  duplicate guidance/count text (no-match feedback remains). The comparison row keeps
  its separator without a background. Its × now sits in the name column using flex
  centering and a fixed control width; long names truncate, keeping bars/values clear.
  Focused Luck and Drop value interaction checks, syntax and whitespace pass; visual review pending.

- Luck exploration implemented for visual review: team names/bars open the full eight-player
  sample roster with back navigation; Players shows low/high five with a gap, plus one
  replaceable/removable searched comparison. Search is a header popover beside Players,
  with six results and focus-safe selection. Existing rows are highlighted and scrolled
  within the card. Event-size changes clear team/pin state; visible height keeps the
  existing three-to-six-row bounds. Bars and signed percentages grow/count together over
  420ms from Expected. Reduced motion and interrupted animations settle immediately.
- Checks: JS syntax, HTML IDs/assets, existing Drop value interaction/motion harnesses,
  new `/private/tmp/check-stats-luck.cjs`, protected-section comparison and whitespace
  checks passed. Luck harness executes drill-down/back, unique extremes/small rosters,
  search/pin/replace/remove, internal scrolling, event-size resets, synchronized counts,
  animation cancellation and reduced motion using DOM stand-ins. User visual review pending.

- Others donut slice/list dot now use a dedicated lighter grey (#a8a9b3) in light
  mode; dark mode retains its existing muted tone. Text colors are unaffected.
  Scoped source, syntax, interaction harness and whitespace checks passed; now user-approved.

- User rejected the Most valuable drops change animation and approved its hover.
  Removed the staggered fade; the row now updates immediately on scope changes.
  The 3px artwork hover lift remains. Syntax, focused motion/interaction checks and
  whitespace checks passed. No section geometry or locked content changed.

- User accepted the previous animation batch. Added a one-time 700ms clockwise donut
  reveal on initial load; reduced motion and unavailable animation APIs show the final
  ring immediately. Later scope morphs remain. Removed rectangular slice focus outlines;
  mouse clicks retain existing highlighting and keyboard focus thickens the slice.
  Focused executable checks cover reveal setup, completion cleanup and no replay, plus
  existing interaction/motion regressions. These refinements are now user-approved.

- Latest authorized trial: Drop value animations, preserving the approved search
  animation and layout. Initial chart lines draw over 600ms once; scope changes morph
  chart paths and donut segments over 240ms, with 200ms fades for team label, total,
  contributor list. Valuable drops updates immediately. Donut hover/focus thickens/dims over 160ms.
  Incoming comparison fades in over 180ms, outgoing line fades out over 150ms; pinning
  pulses its legend entry once. Help opens over 180ms and dismisses over 140ms.
- Redundant chart renders now reuse existing SVG so hover/initial ResizeObserver calls
  do not replay animations. Donut paths use equal cubic structures for SVG interpolation.
  Temporary exiting lines are noninteractive and removed after the fade. Motion uses
  native SVG/Web Animations; unavailable animation APIs fall back to final state.
  Reduced-motion users get immediate changes and no CSS entry/hover motion.
- Checks: JS syntax, whitespace and focused motion harness passed at
  `/private/tmp/check-stats-motion.cjs` (one-time intro, morph paths, preview/pin motion,
  repeated hovers, exiting cleanup, rapid view changes, help lifecycle and reduced motion).
  Existing `/private/tmp/check-stats-search-click.cjs` also passed, including persistent
  sixth-player selection, focus/viewport preservation and search placement/scope.
  These execute logic with DOM/animation stand-ins; the previous batch was user-approved.
  Locked-section renderers/editor and approved search CSS remain unchanged.

- Latest styling refinement: inline divider search has no underline; the magnifier
  changes to theme ink on hover/open with no background. Narrow popover border and
  keyboard focus indication remain. Scoped CSS/cascade and whitespace checks passed.

- Latest correction: inline divider search had been forced into the popover whenever
  the whole panel was <=900px, even with enough divider space. Removed that breakpoint.
  The actual divider width now chooses inline expansion when it fits the 180px field
  plus 28px icon; otherwise it uses the existing header popover. ResizeObserver keeps
  this current as available width changes. Focus/scroll preservation and search scope
  are unchanged. Syntax/whitespace and executable threshold/transition checks passed,
  along with existing click/pin/scroll regressions; user visual review remains pending.

- Latest user correction: search is available only in the event-wide Players tab.
  Both the team overview and team detail views have no search action; programmatic
  opening is guarded too. Its labels say Search players. The personal shortcut is
  Show me because it highlights the current illustrative player rather than searching.
- User clarified the reported jump occurs on CLICKING a player in SEARCH RESULTS,
  not on hover or in the chart legend. The result click now captures the viewport,
  pins the player, transfers focus to the search toggle with preventScroll while the
  input/results are still enabled/visible, then closes search and restores that captured
  viewport immediately. Escape likewise transfers focus before disabling input.
  Hover preview behavior is unchanged. The earlier in-place legend/control retention
  stays in place, but was not sufficient to resolve the user's reported search click.
- Executable regression checks cover mouse/keyboard result activation from a scrolled
  viewport, ordering of focus versus field disabling, retained sixth-player pin and
  restoration after a simulated browser scroll jump. These passed with existing checks.
  This is handler/DOM-stand-in evidence; actual browser confirmation remains with user.
- Guidance is open by default on page load, with an × dismiss button and the header
  info icon to reopen it. It overlays content and does not add a row or increase size.
  Dismissal remains during view changes; opening search also closes guidance.
- Search-result click correction: the old focusout handler closed results when input
  blur supplied no new focus target, potentially before the result received click.
  A focused DOM-stub regression reproduced that premature closure. Results now retain
  input focus on mousedown, ignore ambiguous null-target focusout, and still close on
  an outside pointerdown or explicit keyboard focus departure. Clicking a search result
  sets the sixth pin, rather than toggling an already selected pin off. Hover remains
  temporary. Escape and result selection return focus to the search toggle.
- Executable checks passed in `/private/tmp/check-stats-search-click.cjs`: regression
  sequence, persistent/repeated selection, mouse default prevention, keyboard departure,
  guidance dismissal, updated search scope, and previous comparison/event-size checks.
  Syntax, markup and scoped cascade/whitespace checks passed. User visual confirmation
  remains pending; no browser automation/layout acceptance is claimed.
- Divider field animation and narrow header popover remain. The separate controls row
  stays removed; back arrow, info, personal shortcut and pin × use existing header,
  context and legend positions. Held dimensions and bounded breakdown lists are unchanged.

- Latest feedback rejected the larger fixed layout: the approved proportions must stay.
  Removed the 350px chart preset, reserved toolbar/help rows, larger mobile rows and
  valuable-drop minimum. Navigation now holds the dimensions already rendered in the
  starting view (visual area, legend and narrow stacked chart/share rows), with longer
  lists scrolling inside that space. Resizing/event-size changes release the hold so
  existing responsive CSS can size the page. Help is a compact Info disclosure that
  opens over the content. No larger preset or empty help rows. This correction awaits
  visual feedback; JS syntax, scoped source/cascade and executable DOM-stub checks passed,
  including retaining a captured height across navigation and releasing on size changes.
  Follow-up: contributor-list overflow came from the share-content flex wrapper's
  automatic minimum height. The held-layout wrapper now has min-height:0, matching
  the list's existing shrink/scroll rules. No dimensions or row spacing changed.
  Scoped cascade/whitespace checks passed; visual confirmation remains with the user.

- Teams: click a donut segment or breakdown row to explore a team; smaller muted team
  name beside DROP VALUE, All teams return, scoped donut/list, total, count and top drops.
- Team view: top five contributors by final GP, all contributors in the bounded list.
  Hover/focus previews one additional player; click/tap pins that sixth slot. Another
  preview temporarily replaces the pin, then leaving restores it. Top-five hover highlights
  existing lines. Totals and valuable drops stay scoped to the team during previews.
- Players: event-wide top five, top-five + Others donut, team names beneath player names,
  bounded name/team search (up to six matches) to preview/pin an additional player.
  Others focuses search. My team / Find me use the illustrative signed-in Maya identity.
- Compact Info disclosure content changes with the view. Player endpoint labels spread vertically
  with connectors when clustered. Everyone retains the aggregate chart/team breakdown;
  the valuable-drop strip uses the same event-wide illustrative drop pool as Teams.
- Eight sample contributors per team partition team GP at each date. Only Drop value
  consumes these fixtures; Luck keeps its original player fixtures. Event sizes remain
  2/3/4/5/8/15. Theme changes keep scope/pin; event-size changes clear scope/comparison.
- JS syntax and focused executable Node VM checks with DOM stand-ins passed. Checked all
  sizes, contributor sums and cumulative curves, short rosters, top-five/one-comparison
  transitions, pin replacement/restoration, scoped stable totals/drops, search bounds,
  Others totals/highlighting, keyboard segment and list handlers, label spacing, theme
  and size changes, markup IDs and assets. Harness: `/private/tmp/check-stats-exploration.cjs`.
- Scoped source/cascade checks confirm locked markup/renderers/editor and prior CSS were
  preserved. No browser automation, visual acceptance, .NET or production checks claimed.
  Pre-trial source copies: `/private/tmp/stats-before-exploration/`.

Next permitted action: user refreshes the existing preview and supplies Drop value trial
feedback. Refine that trial as directed, then continue the remaining content discussion.

### Checkout, files, preview and authority

- Work exclusively in `/private/tmp/BingoWebpage-drop-announcements`, branch
  `drop-announcements` (HEAD was 5354a34). Run `git status --short --branch` and preserve
  every existing change. The saved project `/Users/christopher/Documents/BingoWebpage`
  and old thread cwd `/Users/christopher/.codex/worktrees/aac0/BingoWebpage` are NOT the
  assigned checkout. Use explicit working directories. No staging, commits or push.
- Main artifact: `prototypes/stats/outputs/stats-page-prototype.html`.
  Styling: `prototypes/stats/outputs/stats-density.css`.
  Demo behavior/data: `prototypes/stats/outputs/stats-prototype.js`.
  Local assets under `prototypes/stats/outputs/assets/`; README in `prototypes/stats/`.
- Preview: http://localhost:8766/stats-page-prototype.html . Existing loopback Python
  HTTP server serves `prototypes/stats/outputs` (original session 15484); leave it running.
  If genuinely unavailable, serve that directory with `python3 -m http.server 8766
  --bind 127.0.0.1 --directory /private/tmp/BingoWebpage-drop-announcements/prototypes/stats/outputs`.
- HTML has original inline CSS followed by stats-density.css. That stylesheet contains
  historical trial overrides; later rules win. Read targeted selectors near its end.
  Do not perform an unsolicited cleanup. HTML sections are often single long lines.
- UI_PAGE_MATRIX.md owns page approval and records the accepted refined prototype;
  production implementation and acceptance remain deferred. Original chosen image `docs/references/public-ui/stats-reference.png` is
  background inspiration; iterative user-approved HTML has evolved substantially.

### Working agreement for THIS pass

The planner makes these small standalone HTML/CSS refinements directly. The user
explicitly requested that the next planner know this: do NOT spawn subagents for this
pass. This is prototype refinement, not production code implementation. No independent
reviewer, browser automation/screenshots, .NET build, broad tests, app restart, database
work or API integration for CSS tweaks. Check affected source/cascade and edits only;
user supplies visual feedback by refreshing the preview. Preserve demo controls.
Start edits with one short commentary; final response should briefly describe the change
and link the preview. Don't repeatedly ask permission once the user says to try a tweak.
Do not create goals, automations or further tasks without a new explicit request.

### Current composition (accepted overall)

User said the page was great and moved to section-by-section refinement.
Top: Drop value on the left (~70%); Luck above Keeps on dropping on the right (~30%).
Keeps on dropping and GP panel bottom edges align. Full-width compact horizontal Event
timeline separates top and bottom. Bottom: blue Most versatile card left (~30%), Board
progress right (~70%), at the same width as the GP panel. Keep this arrangement.

Typography is local Barlow Condensed (family Barlow, 600/800) for uppercase utility
headings, navigation, names and prominent figures; Geist for body/data/metadata.
Light mode cream canvas #fff9ec and surface #fffdf6, ink #111528, royal blue #293d91.
Dark mode canvas #1b1a1d, surface #222124, cream ink, lavender blue, muted sage/gold/purple.
User preferred dark mode but approved richer teal/gold/purple in light mode. Dark palette
must stay as is. Thin borders and small corner radii. Avoid giant text/spacing, dark
item backplates, more charts/filler, detached metrics, or gratuitous color changes.

### Drop value: user accepted, including size/height refinements

- Heading renamed from GP over time to DROP VALUE; thick blue divider extending from
  heading to Teams/Players/Everyone tabs. Selected tab uses color, NO underline.
- Cumulative GP chart plus team-share donut; GP BY TEAM uppercase Geist.
  Total 3.84B within donut, 169 drops recorded above chart, team legend/values adjacent.
- Most valuable drops is a compact strip inside same panel. Heading sits at left of
  thin divider. Twisted bow 1.20B, Tumeken's shadow 980M, Torva platebody 310M.
  Values vertically centered beside name plus contributor/team metadata. GP panel now
  uses a flex column; treasure and its list fill remaining space, with align-content:center
  centering the item row below its heading. User accepted the resulting full-page appearance.
- Light artwork uses silhouette shadows only, accepted after dark rectangular backplates
  were rejected. Effective filter: drop-shadow(0 1px 1.5px #11152880)
  drop-shadow(0 2px 4px #11152840). Don't change dark artwork styling.

Latest authorized Drop value trial: the existing fixed chart heights become minima
(220px normally,230px at >=1550px,210px at <=560px). The trend is a flex column; its
chart wrapper grows to fill the shared GP visuals row and the SVG fills that wrapper.
Actual donut/list content can therefore make the chart taller. Removed the GP breakdown's
reserved five-row minimum; retained the six-row scroll maximum. With fewer teams, the
chart stays at its minimum and the valuable-drop heading follows the shorter upper row.
Follow-up screenshot showed the chart legend labels slightly above the last breakdown
row. Both legends now share a24px row minimum (26px for the stacked breakdown), removing
unused space under the shorter chart-legend buttons and allowing the chart to fill it.
Scroll content remains start-aligned so every entry remains reachable.
Latest follow-up: GP BY TEAM stays anchored at the top; `.gp-share-content` groups
the donut and team list and centers them vertically in the remaining column height.
This responds to available space for any team count, without count-specific offsets.
At <=600px container width, display:contents preserves the existing side-by-side
mobile donut/list grid. Scoped HTML structure/cascade/whitespace checks passed. User
approved the first section on 2026-09-15, including these refinements; UI_PAGE_MATRIX.md
owns this manual prototype acceptance.
Existing chart ResizeObserver handles redraws; no JS changes. Scoped source/cascade and
whitespace checks passed. Preserve the accepted Drop value section while refining Luck.

### Luck: user accepted, including two-team centering

User approved Luck, including the two-team centering correction, on 2026-09-15.
The section reserves three team rows, grows to six and scrolls beyond that; Keeps on
dropping absorbs the remaining right-column height. Preserve the accepted sizing,
heading, colors and bar treatment.

Current trial: center the two rows vertically inside the existing three-row minimum,
keeping the heading/axis and both right-column card heights unchanged. `.luck-rows`
is now a flex column with non-shrinking rows and auto outer margins on its first/last
rows. Spare space is shared above/below; overflowing lists retain start access and the
six-row scroll maximum. Applies to Teams/Players. Scoped source/cascade/whitespace
checks passed; user supplied manual acceptance. Drop value remains untouched.

- Blue uppercase Barlow heading, Teams/Players with NO selected underline. No divider
  extending from heading (tried and explicitly removed). Subtitle below header.
- Unlucky / Expected / Lucky axis labels Geist600 11px. Expected centered on zero line.
- Uppercase Barlow team names 15px (14 mobile), sufficient plot width; gold negative
  and green positive bars. Sample values -32,-18,+8,+24,+46. Calculation not agreed.

### Event timeline: accepted

Heading uses var(--blue), matching Drop value and Luck. Native `#timeline-filter`
retains all six options and JS behavior. Its borderless uppercase Barlow text is
right-aligned close to the chevron. Wrapper `:after` uses a currentColor SVG mask,
centered with inset-block:0 and margin-block:auto; no manual vertical offset.
Existing hover/focus treatment remains. The selector and exploration hint sit in
`.timeline-heading-actions`, a vertically centered, right-aligned wrapping flex group.
The hint is immediately to the selector's right; removing its bottom row reduces height.
User explicitly accepted this arrangement.

Retain compact panel padding10px 20px 8px, milestone width180px, rail20px, horizontal
scrolling, dates above the rail, uppercase titles and team/item below, plus first-DWH art.
Scoped source/cascade and diff whitespace checks passed; user provided visual feedback.
No automated browser/build checks were run.

### Both highlight cards: accepted

Most versatile is a royal-blue card with cream text, faint oversized clipped existing
logo at right. MOST VERSATILE heading; MAYA with smaller uppercase THE AGENCY tucked
below. Contributed to sits immediately above a single large 14 spanning two lines
DIFFERENT / TILES. Metric means submissions across the most distinct tiles, NOT total
submissions. User disliked earlier art/filler and approved this card.

Keeps on dropping uses the same blue with heading top-left and a single large 18 next
to stacked DRAGON WARHAMMER / DROPS. Item name only is softer #e1e5fb, count/drop label
cream. Quiet cropped monotone item silhouette at right (NOT full-color artwork).
Effective :after mask uses assets/items/dragon-warhammer-detail.png; width140px,
height162px,right-8px,bottom-30px,background#a9b3e0,opacity.13. Actual img opacity0.
User called this sizing/placement perfect. Gold/other backgrounds and full-color DWH
were repeatedly rejected. Keep accepted text spacing and silhouette.
DWH asset is the user-requested wiki detail image, already local:
https://oldschool.runescape.wiki/images/Dragon_warhammer_detail.png?7f65a .
All active DWH references updated; old small file remains unused, don't clean it up.

### Board progress: user accepted and locked

User approved Board progress as part of final Stats UI acceptance. The accepted layout
reserves four
rows, centers two/three-row lists within that space, and grows to six rows before
scrolling vertically. This bounds Most versatile's height at larger event sizes. Rows
remain non-shrinking. Latest correction centers the Current totals/date axis and team
tracks together, using auto margins before the axis and after the tracks inside the
original four-row space. This replaces row-only centering; the section heading stays
fixed and the Most versatile logo is unchanged. The six-row cap remains on the tracks,
with both axes of scrolling retained and the date axis outside vertical scrolling.
Narrow panels retain a680px chart minimum. Scoped CSS/cascade/whitespace checks passed;
user supplied final UI acceptance. All section refinements are complete.

Heading uses var(--blue). The three top-right legend symbols (text glyphs wrapped in
`.race-key-icon`) use var(--ink): ink in light mode, cream in dark mode. Label text
retains its muted color.

Horizontal thin colored team tracks, uppercase Barlow names, circles per tile, diamonds
for completed rows with +2/+3 labels when appropriate, hollow ROUND marker for complete
board (square was rejected). Compact right summary: large tile total, smaller rows
beneath, e.g. 19/25 and 4/12 rows. Two equal big total columns were rejected.
Timeline/hover cursor supports demo exploration; retain it. Team names must remain
uppercase and readable; thick track lines were rejected. Current data includes 12-row
mock denominators, but USER CLARIFIED DIAGONALS DO NOT COUNT. Correct production rules
later; don't treat demo fixture denominators or old First bingo sample wording as
contracts. Requested language is tile completed / rows completed / board completed.

### Demo behavior, data and future boundary

Working demo: GP Teams/Players/Everyone, legend toggles, date tooltip via pointer/tap/
keyboard; Luck Teams/Players; timeline team filter; board completion hover; theme switch;
responsive CSS. All data illustrative. Teams/GP: Agency1400M, Saeh1100M, Morytania640M,
Xeno420M, Zalamikum280M. Counts and dates are mock. No real stats services exist here.
User's intended process: decide content, design reference, HTML proof of concept, THEN
functional implementation. Boss KC is input for luck calculation, not displayed content.
Actual luck method, price API, valuation time, eligible submissions and ties deferred.
Don't invent agreed formulas or new functionality during styling.

### Artwork adjustment editor — user accepted

User authorized an Adjust artwork editor: drag, zoom, rotate, Save/Cancel/Reset and
per-item settings, with desktop/mobile previews. Footer Adjust artwork now opens a
native dialog for the selected Drop preview item. It contains a cloned card, a pointer/
keyboard drag surface, Horizontal/Vertical/Zoom/Rotation sliders and preview width tabs.
Changes stay in a draft until Save. Cancel, close and native Escape discard the draft.
Reset to default is also a draft change; Save removes only that item's override.

Overrides use normalized x/y/width/height percentages plus scale/rotation, stored per
sample item in browser localStorage key `bingo-stats-artwork-v1`. They apply across
sample event sizes, themes and reloads. Original images are unchanged. Default fitting
preserves the accepted original card; CSS default dimensions feed the editor's initial
relative fit. Shared/production persistence and actual Superadmin authorization remain
explicitly deferred; this is a local prototype control, not an implemented admin feature.

Checks passed: JS syntax, scoped source/cascade/whitespace, HTML ID/label associations,
and executable actual editor handlers with DOM/storage stubs covering proportional drag,
keyboard movement, zoom/rotation, preview tabs, per-item isolation, save/reload, cancel,
reset/cancel, reset/save, malformed storage and failed-save recovery. Harness:
`/private/tmp/check-stats-artwork-editor.cjs`. Existing event/drop fixture harness passed
again after the editor integration. No automated browser layout/focus/pointer checks,
build, independent reviewer, app restart or DB work. User accepted the artwork editor
on 2026-09-15 ("perfection."); UI_PAGE_MATRIX.md owns this manual prototype acceptance.

### Prototype action row: event sizes and drop previews — user accepted

Footer now groups theme control with Event size (2/3/4/5/8/15 teams) and Drop preview
(Dragon warhammer18, Twisted bow3, Tumeken’s shadow7, Torva platebody12, Abyssal whip27,
Bandos chestplate14, Berserker ring36). User expects 3–5 teams most often. Defaults remain
five teams and the accepted warhammer card. Drop preview changes the card name, sample
count and existing local silhouette asset independently of event size and theme.

Deterministic sampleEvent(size) updates team/player comparison lists, GP totals/donut,
Luck, timeline data/options and Board progress together. Everyone chart scales to the
sample total. Returning to five restores the original team series and milestones.
Extra team colors adapt to theme; all figures and fixture generation remain illustrative.
Switching clears hidden-series/hover state, resets scrolling and retains only valid team
filters. Race rerenders abort old shared event listeners. No production integration.

Trial visible capacities: GP breakdown actual rows up to6; Luck3–6; Board progress4–6; chart
legend1–2 lines. Excess rows scroll with headings outside; blue cards absorb available
height and Board progress stretches alongside Most versatile. Existing mobile horizontal
board scrolling remains. These sizing limits and controls are included in final UI acceptance.

Checks: JS syntax, scoped source/cascade/whitespace and Node VM fixture/handler checks
passed, including repeated 2/3/4/5/8/15 transitions, actual board-row generation with a DOM
stub, totals/axis bounds, filter/scroll resets and all seven drop handlers/assets. Harness:
`/private/tmp/check-stats-samples.cjs`. This is executable logic evidence, not automated
browser or visual acceptance. No browser automation, build, app restart or DB changes.

No tests needed for ordinary CSS. Prior JS syntax/local assets/server checks passed;
rerun only if relevant. Node isn't on PATH; available executable:
`/Users/christopher/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/bin/node`.
Production banner/drops/navigation were previously accepted. Many production files are
dirty and unrelated; preserve them and leave the user's running ASP.NET app alone.
User accepted the refined full-page appearance on 2026-09-15; UI_PAGE_MATRIX.md records
the acceptance. Latest user-provided dark-mode screenshot:
`/var/folders/w5/74mg_d917xg33ry8_4qc9g5w0000gn/T/codex-clipboard-be092c91-9c05-4fb2-a94a-4df2c9ec9bed.png`.
Responsive follow-up: user reported stacked mobile timeline controls and overcrowded
Board progress markers. Implemented a shared filter/hint row at <=560px, plus a
`.race-scroll` wrapper with a 680px minimum chart width and horizontal scrolling.
Tooltip remains outside the scroll wrapper; scroll resets transient hover/cursor state.
Scoped structure/cascade/whitespace checks and JS syntax passed; browser checks not run.
Next action when the user resumes: discuss exactly what Drop value and Event timeline
display BEFORE implementing either the page or functionality. All other sections are
locked. Port the accepted prototype implementation under the current Stats production port contract; retain data/calculation
uncertainties rather than inferring product decisions from sample fixtures. Nothing
remains pending for this UI refinement pass. See UI_PAGE_MATRIX.md for final acceptance.

## Current event navigation accepted — 2026-09-15

User approves replacing public desktop/mobile header Captain/Submissions entries with
Current event, visible to everyone (including anonymous spectators) only for a public,
non-hidden event in Live or Awaiting final review. Reuse public event ordering: Live first,
then latest scheduled start, and link to its published board. Move role-appropriate
Captain/Submissions into the viewed event's navigation after Teams; keep event navigation
on authorized submissions overview/detail and never link to a different event's team.
Preserve routes/authorization, EN/DA, active indicators, responsive access and accepted
banner/drops UI. Event-navigation implementer (Astra/medium) owns bounded shell/layout/
context/localization changes and relevant executable navigation checks; no broad suite,
browser review, app restart, database reset, packaging or deployment.
Checkout /private/tmp/BingoWebpage-drop-announcements, branch drop-announcements.
Implemented in SharedShellService.cs, _Layout.cshtml and Danish resources (Aktuelt event),
with an updated focused integration case. Submissions context comes from authorized page
models; role queries are scoped to the viewed event/team. Source/diff/localization checks
passed. Executable navigation checks did not run: MSBuild named-pipe socket permission
denied (log /private/tmp/event-navigation-check.log). No retry/broad suite/reviewer run.
Hung test PID 92439 was stopped and confirmed exited by planner; user app left untouched.
User's Release build exposed CA1068 on two new role-navigation method signatures.
Implementer moved CancellationToken last and updated both call sites; scoped diff checks
passed. User subsequently reports "Yeah looks and works great"; navigation is manually
accepted. Automated checks remain unexecuted; no automated success claimed.

## Banner and drops UI complete — user accepted, 2026-09-14

User confirms: "Yep both are good now. That SHOULD be the banner and drops thing completed".
Banner and drops work is complete for the agreed scope; no further visual acceptance,
review, build or testing pass is pending. This supersedes earlier pending/active notes below.

Accepted result: plain coral navigation NEW and label-width view underlines; drop-row NEW
at 0.81rem using actual flex centering, without manual vertical offsets; Clear all NEW
at 1rem within the first time-group divider, its padded container aligned to the statistics
separator. Preserve 0.55rem side padding, opaque page-colored mask on interaction, ink
normal/muted hover text in light mode, and existing dark-mode treatment. EN/DA and existing
acknowledgment behavior are retained. User accepts dismissible banner placement and artwork
handling. Small UI edits received scoped source/diff checks and user visual acceptance;
no new independent review/build/test run was claimed.

Changes remain uncommitted in /private/tmp/BingoWebpage-drop-announcements on
`drop-announcements`. No commit/push/deployment or cleanup is authorized by this acceptance.

## Announcement artwork handling accepted — 2026-09-14

User accepts the handling, noting tiles should have artwork. Keep item-to-tile fallback
and existing text-only guard; no further changes or testing requested. The user previously
said the banner itself works as intended. Missing-artwork follow-up is complete and no
additional visual/check/review pass is pending. Announcement work remains uncommitted;
no new commit/push/deploy authority inferred from acceptance.


## Announcement artwork fallback implemented — user observation pending

Missing/broken image correction complete in drop-announcement.js and scoped CSS:
distinct nonblank item/tile candidates, bounded image-error fallback, then slide-local
text-only mode with no thumbnail/reserved column/gap at desktop and narrow widths.
Existing banner dimensions/copy/controls/state unchanged. Evidence:
`/private/tmp/announcement-artwork-fallback-evidence/`. JS syntax/scoped source/whitespace
checks passed. No build/test/browser/reviewer/runtime restart/commit. User refreshes the
running selected branch to observe fallback. All ticket manual acceptance remains complete
(46 Done,5 Closed,2 Deferred); this small announcement fallback awaits user observation.


## CSV accepted; announcement missing-artwork correction active

User explicitly accepted skipped CSV ("Pass on CSV") and resumed announcement work.
C13 is Done by manual-test waiver plus previously accepted executable/review evidence,
not a claim CSV was manually exercised. Ledger:46 Done,5 Closed — no change,2 Deferred
(C40/D05); no ticket remains Awaiting manual acceptance. Do not start new sweep tickets.

User says banner itself works as intended; current pass is only missing artwork. Known
client gap: buildSlide always renders empty thumb when no URL and doesn't handle failed
image load. Agreed implementation direction: item artwork then distinct tile artwork;
on missing/exhausted images use text-only slide, remove empty frame/reserved column, keep
banner dimensions/typography/controls/countdown/navigation. No evidence screenshot or
invented placeholder icon. Preserve frozen identity/service behavior. Same Luna/max
implementer cancelled_ui now assigned JS+scoped CSS in selected drop-announcements;
evidence /private/tmp/announcement-artwork-fallback-evidence. User waived extra UI review/
build/browser checks; no runtime reset/restart or source commit. User observes final result.


## Discord manual acceptance complete — 2026-09-14

User reports Discord checks1–3 passed on selected branch using prepared DB/7131:
linking, ordinary Discord sign-in to existing account and updated Last login. User lacks
a second Discord account and explicitly requests all Discord checks marked pass. Replacement
check4 accepted by explicit manual-test waiver using existing passing automated/review
evidence; not claimed executed with a second identity. C06/C07 are Done. MR-03 accepted.
No further provider testing, review or follow-up required for these tickets.
Ledger:45 Done,1 Awaiting manual acceptance (C13 skipped CSV),5 Closed — no change,
2 Deferred (C40/D05). This approval concerns Discord; it does not silently waive the
separately skipped CSV check. Local commit5354a34 and preserved announcement overlay
unchanged; no push/deploy or cleanup authorized by this acceptance.


## Discord startup environment correction — 2026-09-14

User ran supplied commands: Web build passed in13.2s. Compose attempted another postgres
on occupied5432; migration against existing legacy DB stopped at20260905221344 because
historical manual objectives retain drop snapshots. App then ran against incomplete schema
and reported missing announcement_generation. Do not reinterpret as current code build
failure or perform legacy repair/reset. Existing historical-data repair remains deferred.
Prepared `/private/tmp/ticket-integration-evidence/manual/run-discord-check.sh` selects
existing isolated bingo-ticket-manual-20260914 DB via private runtime.env, applies pending
migrations with --no-build (stops on failure), and runs selected drop-announcements on7131
for normal Discord callback. Existing secrets provide OAuth credentials; no secret config
mutation. Test accounts/data retained, no reset. Launcher prepared only, not executed by
planner; user stops their failed7131 process and runs it. No build/review/test rerun.


## Ticket commit and drop-announcements integration complete — 2026-09-14

Source branch `codex/ticket-integration-20260914` and selected working branch
`drop-announcements` now point to commit `5354a3454034ea72544563147cda9f63355ea1e5`.
Normal active checkout: `/private/tmp/BingoWebpage-drop-announcements`.
Exactly125 accepted src/tests paths committed (original accepted inventory plus user-
approved cancellation r2); three stale candidate authority snapshots excluded/preserved.
Target fast-forwarded; all original54 dirty paths restored and non-overlap bytes preserved.
Index empty, no unmerged paths; existing announcement/docs work remains uncommitted.
Three textual conflicts resolved (FinalizationService, Board page model, Danish resources).
Named overlap corrections preserve published metadata/targets in announcement feed and
completion helper plus combine latest C21 migration designer with announcement snapshot.
These overlap edits remain in target's uncommitted announcement overlay; prior isolated
reviews do not claim to cover them. No new runtime/build/test/review was run for packaging;
combined announcement overlay has not been runtime-verified. Git/hash/preservation checks
passed. No push/deploy/cleanup or app/database changes.

Recovery stash retained: `d224e3fc0cb08a3fe9b39ff67d464197664fc65b`.
Report/backups: `/private/tmp/ticket-packaging-evidence/20260914-190056/packaging-report.md`.
Saved Documents `feature/boss-artwork` working files and branch remain untouched.
User-secrets lookup confirms DiscordAuthentication ClientId and ClientSecret are both
present for shared UserSecretsId BingoWebpage-Slice1-Local-Configuration. Target https
profile is https://localhost:7131 (HTTP5164), callback /Account/DiscordCallback. No secrets
printed/changed, provider registration inspected or real sign-in performed; configured
presence is not proof of a successful OAuth round trip.

Ledger remains43 Done,3 Awaiting manual acceptance (C06/C07 Discord,C13 CSV),5 Closed,
2 Deferred. Packaging/selected local merge objective complete. Next user-facing action:
use selected working checkout and perform pending Discord sign-in when desired; paused
announcement implementation/acceptance remains separate. No automatic new batch or review.


## Ticket packaging and local merge authorized — 2026-09-14

User explicitly authorized committing the assembled ticket changes and bringing them
into the normal branch, then selected `drop-announcements` at
`/private/tmp/BingoWebpage-drop-announcements`. Preserve its uncommitted announcement
implementation, authority updates and local configuration. Saved Documents checkout
`feature/boss-artwork` is outside scope and must stay untouched. No push, deployment,
branch/worktree deletion, DB reset, app start/restart or extra UI review is authorized.

Packager will freeze backups, commit only accepted integrated src/tests files (original
128-file source inventory less three worker authority snapshots, plus accepted UI delta),
then fast-forward target while safely preserving/reapplying its existing dirty overlay.
Current central AGENTS/CURRENT_STATUS/TICKETS and other docs remain authoritative and
must not be overwritten by stale candidate snapshots. Leave unrelated announcement/docs
work uncommitted as before. Resolve only merge overlaps, preserving both implementations;
no new feature/remediation scope. Keep recovery stash/backups until completion and do not
remove original worktrees. Reuse prior evidence; no new scan/full-suite/UI review merely
for packaging. Required Git integrity/overlap checks are packaging checks.

Current accepted state: 43 Done, Discord C06/C07 and skipped CSV C13 pending manual
acceptance; five Closed, two Deferred. Cancellation r2 user approved without added review.
Planner checks OAuth configuration presence without exposing credentials or performing
provider sign-in. Packager reports final commits/branch/status/conflicts/remaining limits.


## Cancellation UI approved; ready for packaging decision

User explicitly approved the final cancellation UI after running the app themselves.
The final r2 changes use the existing error-page shell/spacing, remove the mark/divider
and horizontally center the copy. Approval covers this requested visual correction;
no additional source review/browser/build/test pass is required or authorized. Evidence
of edits remains `/private/tmp/cancelled-event-ui-evidence/r2/`; do not reuse the earlier
128-file manifest as if it included this subsequent delta. Assistant-started HTTPS7147
process73695/session21530 was stopped on user request; user owns current runtime.

Next phase is packaging the cumulative candidate into the intended normal branch/checkout
while preserving separate paused announcement work and other existing changes. No staging,
commit, merge, push, cleanup or deployment has yet been authorized by this UI approval.
Ticket ledger remains 43 Done, 3 Awaiting manual acceptance (C06/C07 real Discord with
configured provider; C13 skipped CSV), 5 Closed — no change, 2 Deferred (C40/D05).
No new scan, ticket batch, speculative cleanup or UI review is pending. Discord can be
checked later in the configured environment; CSV skipped acceptance is not silently waived.


## Cancellation r2 — exact error-page shell/spacing

User reiterates literal existing error-page structure/spacing with left column and
divider removed, right copy horizontally centered. r2 removes custom cancellation section
height/padding and uses existing public-ui-pass1 status shell via PublicEventCancelled.
Existing 404 vertical spacing remains authority; no invented padding/vertical alignment.
Same implementer makes direct bounded markup/CSS/layout correction and builds actual
candidate. No independent review or browser/visual pass per explicit user instruction.
Planner diagnosis of reported persistent X: host and candidate partial/compiled hashes
match; current served7147 cancellation HTML already has no X. Fresh navigation after
r2 will load current markup; do not claim old browser content is user's fault. EN/DA keys
remain unchanged. All user fixture/data and existing noncancelled UI retained.


## Small UI review explicitly waived by user

User explicitly overrides workflow: "DONT DO A FUCKING REVIEW ON A SMALL UI CHANGE".
No further independent review for this small UI change; user owns visual acceptance.
Before instruction arrived, source reviewer had already identified residual mobile top
border from existing <=42rem CSS. Implementer finishes only cancellation-specific all-
border/separator-padding reset. CSS-only: no rebuild/test/review/browser pass. Stop after
that edit and let user refresh. Do not impose repository review policy against this user
override or open a new review/ticket chain for routine small visual corrections.


## Cancellation r1 implemented, user visual review pending

r1 implementation complete: X removed; existing localized copy/CTA retained. Scoped
cancellation CSS centers the copy, removes divider/padding and extends container/main/
status canvas across viewport with light/dark background. No _Layout or 404 changes.
Evidence `/private/tmp/cancelled-event-ui-evidence/r1/` holds preimages/deltas/hashes.
Web Release build zero warnings/errors, source/cascade/diff passed. Same reviewer named
source recheck underway. Isolated7147 app restarted successfully through existing launcher
(session21530), existing DB/storage retained;7131 untouched. Per explicit user instruction
no browser/visual/language execution checks performed after correction. User refreshes
existing cancellation page to judge full-page layout and English/Danish.


## Cancellation correction visual checks delegated to user

User explicitly says "No dont check it, i can do that." Stop further browser/visual/
language execution checks. Complete scoped source correction, build and named source
recheck; refresh isolated app only to serve the correction. User owns final EN/DA,
full-viewport and no-X/divider observation. No screenshot or page inspection rerun.


## Cancellation styling correction — active

User rejected the first cancellation styling. Exact accepted correction direction:
remove the X and divider; preserve the right-hand copy/typography/button as-is, center
that group horizontally, and fill the entire viewport below the header with the page
background rather than a constrained cream panel on black. Support English and Danish.
Observed browser root cause: body only public-ui-page-canvas with black computed background;
main.pb-3 and status section constrained to x84/width696 at viewport864. Existing Danish
strings render correctly. Reuse same Luna/max implementer `cancelled_ui` for this named
correction; minimal cancellation-only layout class/CSS is authorized. Preserve all route/
privacy behavior, 404 pages and current fixtures. Scope recheck to this correction with
same reviewer; planner verifies actual browser EN/DA after rebuilt isolated7147 app.
First styling is NOT manually accepted. No broader UI pass or new ticket batch.


## Cancelled-event status styling — PASS, visual approval pending

Implemented and independently reviewed PASS: one shared `_EventCancelled.cshtml`
markup change reuses status-editorial layout, existing typography/responsive behavior and
coral-outline Home CTA. Decorative cancellation mark × is aria-hidden; localized message,
encoded event name, accessible heading and data-event-cancelled marker retained. No CSS
or product/route/privacy behavior change. Current partial SHA256 `dc3baad13eb845f261326f604b470867ddb81aacb9db9ce573953328d6a5bdfc`.
Delta/preimages/review: `/private/tmp/cancelled-event-ui-evidence/`; reviewer
`cancelled_ui_review` Astra/high found no required defects. Web Release Razor build
passed zero warnings/errors; source/cascade/diff checks passed. Prior accepted integrated
baseline remains evidence, with this explicitly recorded one-file delta on top.

Prepared HTTPS7147 app restarted through its existing launcher, same isolated DB/storage
and fixture state. Sandbox could not reach host process/Docker/network; approved escalated
restart and single HTTP read succeeded. Served HTML contains new cancellation layout.
Runtime session41010 is active. HTTPS7131 and user DB untouched. User should refresh
https://localhost:7147/Events/ticket-review-cancelled/Board for visual acceptance.
No full-suite/browser/manual reruns, staging, commits or additional scope. C37 function
remains accepted; new visual delta awaits user's approval.


## Cancelled-event status styling — active, 2026-09-14

User requests a quick visual correction to cancelled public event destinations using
the existing 404 status-page structure. Approved scope: shared `_EventCancelled.cshtml`
composition/typography/return CTA and only necessary scoped CSS, reusing `status-editorial`
from `Pages/Errors/StatusCode.cshtml`. Preserve localized cancellation meaning, public
event name, data-event-cancelled marker, heading accessibility, Home link, privacy guards
and existing HTTP/route behavior. Actual 404 pages and finalized/archived views unchanged.

Implement directly on cumulative `/private/tmp/BingoWebpage-ticket-integration-20260914`,
branch `codex/ticket-integration-20260914`; preserve prior accepted uncommitted candidate
and all manual fixture state. Bounded implementer subagent `cancelled_ui`, Luna/max;
planner handles authority documentation concurrently. Capture preimages/scoped delta in
`/private/tmp/cancelled-event-ui-evidence`. Source/cascade/diff and Web/Razor Release build,
then one independent Astra/high review of this small delta only; no full suite, repeated
manual walkthrough or extra fixtures. Existing C37 functional acceptance stays accepted;
new styling awaits user visual approval. No staging, commit, push, database reset or
unrelated app restart. Next: scoped implementation/checks/review, then show user the
same cancelled-event URL after the candidate is rebuilt/restarted through normal setup.


## Steps 13–15 accepted by explicit waiver — 2026-09-14

User explicitly approved closing chat steps 13, 14 and 15 (MR-12, MR-13 and
MR-14) without further manual testing: "just pass 13 14 15" and "These are likely
never gonna be used anyway". Record accepted manual-test waiver and reliance on existing
passing executable/integration/review evidence, not a claim these manual actions ran.
This clears C11/C17/C18/C19/C25/C38. No further fixture preparation or testing is needed
for those checks. Ledger: 43 Done, 3 Awaiting manual acceptance (C06/C07 Discord;
C13 CSV), 5 Closed — no change, 2 Deferred. All prior accepted observations and
integration evidence remain unchanged; no publication/packaging authority is implied.


## Manual results reconciled — 2026-09-14

User checkpoint — 2026-09-14, second walkthrough response. Chat step 12/MR-11
passed; steps 16–25/MR-15,16,17,19,20,21,22,23,24,25 passed; step 26/MR-26 accepted
recorded internal evidence. Earlier nine passes remain recorded. Chat step 13/MR-12:
user hit replacement instructions ambiguity and explicitly declined further manual effort.
Existing signup should be selected from "Waiting-list participant (optional)", not added
again through "Internal owner username"; no production failure established from this
report. Chat step 14/MR-13: user reports other teams also need emergency credentials
and declines setup; guide/fixture readiness was incomplete, not a passed manual scenario.
Chat step 15/MR-14: "Manual approval example" was a requested new title, not a seeded
item; planner clarified the misleading guide. This check remains unexecuted/unaccepted.
Discord MR-03 remains pending later configuration; CSV MR-09 remains skipped. No
additional manual effort on the declined checks, fixture expansion, remediation, workers,
new tests or review is dispatched. Do not infer a passing manual result for skipped cases.
C37 clarification: existing Board/Teams/team/tile routes were retained; ticket added a
shared generic cancellation display/guards. Ticket-review event records are disposable
fixture data created for this walkthrough, not new product pages.

Current ledger: 37 Done (31 newly accepted plus the existing six), 9 Awaiting manual
acceptance (C06/C07/C11/C13/C17/C18/C19/C25/C38), 5 Closed — no change, 2 Deferred.
C17 retains its unobserved departure-notification portion despite passing MR-11.
Skipped/unexecuted portions remain named acceptance limitations; no code failure is
inferred. Page-specific observed acceptance is recorded in UI_PAGE_MATRIX. Integration
candidate/frozen manifest remains unchanged. Packaging/publication is not authorized.


## User manual acceptance underway — 2026-09-14

User checkpoint — 2026-09-14, chat guide steps 1–11 (chat step order differs from MR IDs):
steps 1, 2, 4, 5, 6, 7, 8, 9 and 10 passed, mapping respectively to MR-01, MR-02,
MR-04, MR-18, MR-05, MR-06, MR-07, MR-08 and MR-10. User described step 4 as
"a little funky UI but functionality works" and step 6 as "a bit weird UI"; record
those observations without inventing a defect or authorizing UI redesign. Step 3/MR-03
cannot currently run; user expects real Discord testing later with suitable configuration.
Step 11/MR-09 CSV is optional for continuation and may be skipped; manual acceptance
remains open, not waived. The original prepared preformed roster supports later checks
without applying the CSV. Continue at chat step 12/MR-11. MR-26 remains unacknowledged;
no group-wide or untested-state acceptance is inferred.


## Integration PASS reconciled; user walkthrough ready — 2026-09-14

Independent integration reviewer `01a0a090-00ce-74e1-9252-7342be4fc6a7` returned PASS
with no blocking findings. Report:
`/private/tmp/ticket-integration-evidence/integration-review/independent-review-result.md`.
Accepted candidate `/private/tmp/BingoWebpage-ticket-integration-20260914`, branch
`codex/ticket-integration-20260914`, HEAD/base `c165bbcb321547637d03b4e9dc3d2e206e5944b3`.
The uncommitted candidate is pinned by all 128 file hashes in
`/private/tmp/ticket-integration-evidence/final-review/manifest.json`, SHA256
`ced7502b5deae58679b2a78cc530e948a9ed0894cf5c30f8deaee29f19afbaa8`;
full patch SHA256 `209e94a7d8d4f23ed4f5c342801ebd73127dfe9a6d85685b8862b8c0a85757b7`.
Reviewer verified actual inventory/hashes, source provenance, conflicts/direct joins,
reverse-apply and diff checks; nothing staged. Prior ticket reviews reused.

All 1,022 original .NET cases have passing evidence after named corrections/rechecks;
19 initial failures were resolved, not represented as a clean second full-suite run.
Release build zero warnings/errors, full format, 21 Node harnesses and applicable real
PostgreSQL migration/concurrency/history/rollback evidence pass. Reviewer did not rerun
manual/visual/runtime checks. T05 provenance and C41 isolated-capture limits retained.

Prepared commands/accounts/scenarios are in
`/private/tmp/ticket-integration-evidence/manual-handoff.md`; central checklist now
identifies the integrated candidate. HTTPS URL https://localhost:7147. Use the supplied
manual/run-candidate.sh launcher for its isolated DB/storage/WOM fixtures. No reset is
needed; resetting would remove supplemental scenarios. Preflight app stopped, fixtures
retained. Prepared dates expire 16 September 2026; later use needs only scoped fixture
refresh. MR-03 real Discord provider acceptance remains pending setup/sign-in. No
manual/page acceptance is claimed. Ledger remains 40 Awaiting manual acceptance,
6 Done, 5 Closed — no change, 2 Deferred. No further worker batch or review is active
under this completed assignment. Next action: user's step-by-step walkthrough and
recording actual results; no new scan, speculative scope, packaging or publication.
Earlier integration/preparation-pending entries below are historical checkpoints.


## Older T05 source recovered for integration — 2026-09-14

Combined Node gate exposed the Done T05 repair missing from the recent batch packages.
Planner recovered the original reviewed diff from reviewer task
`01a086ed-6df8-7c02-90c4-e39ab0e42454` and the exact named localization correction from
implementer `01a0870b-ed27-7b80-9d0b-cef6bbb165e9`. Evidence directory:
`/private/tmp/ticket-integration-evidence/t05-recovery/`. The pre-correction reviewed
postimage matches its recorded Git blob; final source uses only the exact logged
correction and matches the original 87 insertions/28 deletions. Final recovered blob
`6719d5f51be13f110406bd8fefd42e955b2e2a11`, SHA256
`ed3b47ae5218800e48dee6783c35c8f75014fab88c20887f58d202255b7d475a`.
The base test matches c165bbc blob 84ae3433e7df32fb8d2dab3debb4c5387792c45b.
Original final handoff and passing targeted-test output preserved. No independent final
hash/recheck is invented; provenance distinguishes the initial review and its recorded
one-line correction. Source supplied to integrator for the existing candidate and Node
gates; site.js/product behavior unchanged for T05. No new review cycle or user decision.

## Post-integration PASS user handoff request — 2026-09-14

User will be away while integration runs. On receipt of the independent integration
PASS, planner must reconcile the exact accepted candidate and rewrite the manual
checklist directly in this chat as an unambiguous numbered do-this/do-that guide.
Include exact initial cd, data reset only if needed, build and HTTPS run commands,
verified against the actual accepted candidate and its supported setup. Specify scenario/
account/starting page/expected result for each journey; do not leave fixture placeholders
or require guessed routes. Integrator has been asked to provide this preparation data
with its reviewer handoff. Distinguish tested commands from source-verified ones and
preserve actual fixture state. A request for reset commands is not permission to reset
the user's existing database or restart their running app. No extra review cycle or
feature work is added. Await final PASS through existing exclusive reviewer routing.

MR-03 preparation limitation: real Discord linking/replacement/login requires the user's
real provider sign-in and a valid OAuth callback for the isolated candidate (worker
currently identifies HTTPS port 7147; final URL must be verified in handoff). Planner
retains that portion as pending manual acceptance. Existing controlled callback/session
evidence and prepared local Settings/last-login inspection may be reused with their
limits stated; they do not substitute for completed real-provider manual acceptance.
No provider credential/callback change or real identity linking is authorized by this
preparation. This prerequisite does not block integration review/PASS or delivery of the
remaining walkthrough. Include exact entry/callback details and a skip/pending instruction
if provider setup is unavailable; no additional user decision is needed now.

## Ticket source integration authorized — 2026-09-14

User approved gathering/reconstructing the reviewed work, combining it in one integration
worktree, resolving overlaps and running combined checks before manual review. This
supersedes earlier no-source-integration boundaries for this task only. Existing source
folders and evidence remain untouched; no user DB/app restart, commit, PR, branch merge,
push, deployment, deletion or new product work is authorized.

Prepared clean integration worktree `/private/tmp/BingoWebpage-ticket-integration-20260914`,
branch `codex/ticket-integration-20260914`, base `c165bbcb321547637d03b4e9dc3d2e206e5944b3`.
Git metadata creation required filesystem escalation and succeeded under the user's
explicit integration authorization. Reuse direct Astra xhigh implementer task
`01a0a019-1abe-7bd2-ba0d-72f2acb39982` for integration; its prior C33 worktree is protected.
Planner owns authority/status reconciliation and missing C05/C09 provenance lookup;
implementer owns verified source assembly, conflict/integration fixes and combined checks.

C05/C09 provenance lookup is COMPLETE. Their original source folder has only build
artifacts remaining, but the original independent review session retained its complete
four-file diff. Recovered exact output at
`/private/tmp/ticket-integration-evidence/c05-c09-recovery/reviewer-original-diff-output.txt`;
source session/tool-call evidence and original verdict are in that directory. All four
preimages match original commit `86e7dc3f5910cd332123c5e951b1c0b4eb75c654`; reconstruction
from exact Git blobs plus unchanged patch gives all four matching reviewed postimage
blob prefixes. `reconstruction-manifest.json` records full reconstructed hashes. This
is 246 insertions/7 deletions, not rewritten implementation. Recovered source supplied
to the integrator, which must reconcile later c165bbc baseline changes. Original source/
evidence remains unchanged; no user data or integration production was edited by planner.

Account C06/C07/C08 provenance question resolved, 2026-09-14. Original independent
reviewer is `01a09b85-6f91-7c90-823f-932a4f98c479`; the original report is
`/Users/christopher/Documents/Codex/2026-09-13/account-ticket-batch-review/outputs/account-batch-independent-review.md`.
Recovered intact original reviewer source/test diff outputs into
`/private/tmp/ticket-integration-evidence/account-review-provenance/`. All six current
production and three test files match reviewed postimage Git blob prefixes, and both
current diffs against c165bbc match recovered reviewer diff bytes exactly. The comparison
manifest pins current full hashes, original tool-call/session provenance and report hash.
This adds comparison evidence, not a new review or runtime rerun. Historical report-delivery
rejection remains recorded; current integration sharing is explicitly authorized. The
integrator received this resolved source authority and may use its matching frozen copy.
No known source-provenance question remains pending with planner at this checkpoint.

Integrate only the 40 reviewed acceptance-pending tickets and required passing test
repairs, including older C05/C09 (old-data cleanup explicitly deferred). C26/C27/C34/C36/
D03 closed; C40/D05 deferred; WOM Stats and separate paused drop-announcement changes
excluded. Central dirty production source must not be used as an integration baseline.
Central current ticket scope/UI approval/manual checklist remains authority; do not
replace it with stale worker document snapshots while applying code patches.

Verify each source snapshot against final review manifests, including untracked files.
Reconstruct the four missing folders from final saved complete patches in separate
scratch trees without altering evidence. Keep snapshot/hash provenance for every applied
batch and document manual conflict resolutions. Use one cumulative integration worktree
and retain a recovery checkpoint/evidence patch after coherent integration steps; no
new independent branch per added batch. Ordinary technical conflict resolutions are
authorized; preserve all approved behavior, transaction/lock order, audit, privacy,
immutable history and migrations. C20/C21 complete managed-artwork recovery must be
proved; reconcile C20/C33/C31/C32/C35, roster/signup/auth and shared resources carefully.
No silent feature loss, broad redesign, legacy repair or source decompilation.

Use applicable existing focused tests to prove overlap resolutions, and the agreed
combined build/test/format/PostgreSQL migration gates once after assembly. Do not rerun
every isolated suite at every step or create new tests mirroring trivial merges. New
failures get bounded direct fixes and affected rechecks. After one lookup, unresolved
source provenance or consequential scope ambiguity goes to planner while independent
assembly continues. No missing-source implementation should be silently recreated.
One fresh independent Astra xhigh reviewer may review only integration-specific changes,
conflict resolutions and evidence coverage after checks; reuse prior ticket reviews rather
than re-audit all tickets. Exclusive routing stays implementer -> reviewer, findings ->
implementer, final PASS -> planner. Standing user sharing approval covers necessary exact
private paths/diffs/hashes/evidence. No subagents/reviewer chains or extra permission loop.
Manual approval remains unclaimed. Stop at integrated candidate/verification handoff;
manual fixture/runtime preparation can follow in the same candidate when permitted.

## Integration source inventory observation — 2026-09-14

User asked whether batches were stacked. Read-only check of the sixteen recent batch
paths confirms all twelve still-present worktrees have HEAD c165bbcb321547637d03b4e9dc3d2e206e5944b3
and their own uncommitted changes. They are independent, not a cumulative chain.
Four recorded checkout folders are absent and no matching git worktree registration
was returned: 0373 (C19/C23/C25), ffd6 (C04/C15/C22), 3ae0 (C28/C29/C30),
0e93 (C31/C32/C35). Cause/time of absence is unknown; do not infer source loss or
claim all sixteen folders remain present. Saved review artifacts still exist:

- C19/C23/C25: /private/tmp/board-approval-batch-evidence/complete.diff
- C04/C15/C22: /private/tmp/audit-batch-review/complete.diff
- C28/C29/C30: /private/tmp/participant-evidence-batch-evidence/complete.diff
- C31/C32/C35: /private/tmp/final-review-integrity-batch-evidence/full-base-to-current.patch

Integration preparation must verify final reviewed artifacts, reconstruct missing batch
sources against their recorded base and verify full file inventories/hashes before use,
including untracked files. Artifact existence alone is not a reconstruction test. Include
the older C05/C09 trial source/evidence in that inventory too. No patches were applied,
worktrees reconstructed, staging/commit/merge performed, or production source modified
by this read-only check. Existing evidence of completed review remains; runnable combined
source is still pending, as stated in the manual checklist.

## Grouped ticket acceptance checklist prepared — 2026-09-14

User requested the manual review checklist. MANUAL_TEST_CHECKLIST now contains the
current ticket walkthrough: 25 visible checks in seven journeys plus one evidence
acknowledgment, with coverage of all 40 Awaiting manual acceptance tickets. All remain
unchecked/unaccepted. Candidate URL/revision/accounts/fixtures and exact expected values
are explicitly pending an authorized integration and bounded preflight. C20/C21 complete
managed-artwork recovery must be proven there; isolated C20 safe refusal is not that proof.
The user is not tasked with reproducing transaction/security races or fixing fixtures.
Existing evidence is reused; older slice/announcement checklists are not automatically
added. No source integration, runtime, data reset, new worker or manual approval occurred.

## Ticket implementation and independent reviews complete — 2026-09-14

C33 final independent PASS is reconciled. There are **no active implementation batches
or remaining eligible tickets to dispatch** under the current scope. Current ledger:
**51/53 handled = 40 Awaiting manual acceptance + 6 Done + 5 Closed — no change**;
C40 and D05 remain Deferred/outside this batch. C05/C09 old-data inspection/repair is
explicitly deferred under the user's delegated disposition; C26/D03/C36 are closed.
The following earlier dispatch/policy checkpoints are history, not active instructions.

Next phase requires separate source-integration authority: assemble the reviewed isolated
changes, reconcile their named overlaps/dependencies, run the combined applicable gates
once, then provide the grouped manual walkthrough. No integration, staging/commit/PR/
push/merge/deployment, new feature (including WOM Stats), user-data cleanup or running-
app restart is authorized by this review. Existing worktrees and central dirty source
remain preserved. Do not start another batch or repeat passing review/tests merely to
change status. Ticket manual acceptance and package integration/release remain distinct.

### C33 accepted review checkpoint

Sole reviewer `01a0a02a-05db-7380-b704-67522978e9c5` cleared P2 R1 in the same-reviewer
bounded recheck; no required finding remains. [Final report](/private/tmp/c33-evidence/reviewer/recheck-pass.md),
SHA256 `f66d9d677724abba4d283c111618a1e0ed48a01b653ab6257600cd32ee287324`.
Implementer `01a0a019-1abe-7bd2-ba0d-72f2acb39982`, actual checkout
`/Users/christopher/.codex/worktrees/6771/BingoWebpage`, branch
`codex/finalization-freshness-c33`, HEAD/base `c165bbc`, unstaged/uncommitted/unintegrated.
The accepted `/private/tmp/c33-evidence/r1/full.patch` SHA256 is
`bf1d6079a31d6312957111250cc005c81743a03782c667d27f0dc9b7353baec6`;
manifest SHA256 `cc400b799794e730a17b9ad2e008688a1e3b2b419436c9fdff0f245057f80401`.
Reviewer verified 15 current repository files including both untracked files, five R1
delta files, 32 R1 artifacts, 22 original artifacts and 14 preserved original snapshots;
reconstructed full/source/delta patches byte-for-byte without changing repository files.

C33 guards finalization freshness and immutable per-cycle/team inspection identity,
current-input acknowledgment replay, retained resolutions, correction invalidation and
atomic finalization. R1 makes losing review concurrency requests show an explicit unsaved/
reload/latest-state/retry Error message through existing Review Details.Execute; existing
authority/reasons/confirmation/routes/filters/lock order/composition remain protected.

Evidence supports **36 distinct cases**, reusing original 29 passes plus seven R1 cases;
not a fresh combined run. Four R1 cases exercise real migrated disposable PostgreSQL/
authenticated-CSRF losing-review races and explicit fresh retries; three exercise exception
shapes through HTTP. Original R1 6-pass/1-failed success-style assertion was corrected
and its named rerun passed; the failed attempt remains recorded. Unchanged reversal
success Information styling is an unrelated limitation, not a new task. Original solution
Release and R1 Web/Razor Release builds have zero warnings/errors; applicable format/
diff checks pass. No full suite, new browser walkthrough or manual visual pass claimed.
Planner reuses the independent evidence without runtime/review reruns.

UI_PAGE_MATRIX preserves page composition approvals and marks C33 Finalize freshness/
reinspection/error and Review concurrent-error/retry states Awaiting manual acceptance.
Historical correction ambiguity fails closed without reconstruction. No silent official
result republication. Later authorized integration must reconcile C31/C35/C32/C20 and
C04/C15/C22 overlaps; C20/C21 full managed-artwork recovery remains a named obligation.
Sixteen recent reviewed batch worktrees remain isolated, in addition to the older C05/C09
trial evidence; do not assume any source has been integrated. Final PASS delivered only
to planner; no duplicate implementer handoff or new reviewer is needed.

## C05/C09 old-data obligations deferred; review blockers cleared — 2026-09-14

User explicitly delegated the C05/C09 decision to the planner, noting disproportionate
time/usage on edge cases. Planner retains the existing independently reviewed fixes
and defers old-data inspection/repair for both. C05 and C09 are **Awaiting manual
acceptance**, not Blocked. Existing user records remain unchanged. No production scan,
backfill, notification rewrite, worker/reviewer or additional test run is authorized or
needed for this disposition. Only disposable synthetic mismatches were demonstrated;
actual affected-user counts remain unknown. This does not assert there are zero affected
records, that retained data is repaired, or that the isolated fixes are already integrated.
Revisit a concrete reported failure if one occurs; no automatic cleanup follow-up.

C05 preserves corrected character selection through subsequent rejoin/restoration;
C09 fixes the owned-record destination in newly created lifecycle notifications. Their
previous implementation/review evidence remains applicable and is reused. Manual
acceptance and authorized integration remain separate. C36 is Closed — no change;
C26/D03 are also closed, C40 remains outside this batch, D05 deferred. Current totals:
**50/53 handled = 39 Awaiting manual acceptance + 6 Done + 5 Closed — no change**;
only C33 In progress and C40/D05 Deferred remain outside those dispositions.

## C36 scope resolved; C05/C09 choices remain — 2026-09-14

User clarified hiding on 2026-09-14: its purpose is to keep unwanted/test/abandoned
events off the front page and ordinary Admin Events table, not conceal their existence
or details from Admin audit history. C36 is closed without implementation. No legacy
audit-association cleanup is required for hiding, and hidden-event audit visibility is
not an additional secrecy boundary. Preserve normal audit authorization, immutable
history and sensitive-data exclusions, as well as the existing event-list/workspace
visibility and hide/restore controls. This scope decision does not itself remove existing
audit filters or authorize a visibility patch, source integration or historical-data repair.

C05's reviewed fix prevents new mismatches once integrated; postponing old-data work
would leave existing rows uninspected/unrepaired, not defer the code fix. No deferral
or production scan has yet been approved. Planner recommends one bounded read-only
check for C05. C09's reviewed new-notification fix remains; planner recommends leaving
old malformed notification links unchanged because the limited navigation benefit does
not justify cleanup. That recommendation is not yet an explicit user disposition.
C33 continues independently. Current totals: 48/53 handled (37 Awaiting manual
acceptance + 6 Done + 5 Closed), C33 In progress, C05/C09 Blocked, C40/D05 Deferred.

## C33 next batch dispatch — 2026-09-14

C20 final pass is reconciled below. C33 technical freshness/inspection invariants are
frozen in FUNCTIONAL_CONTRACTS 7.6 / DATA_MODEL 17.1 and the active TICKETS brief.
Next direct saved-project Astra xhigh implementation task is queued.
C33 creation accepted on local host with queued client ID
`client-new-thread:4efdf848-10cb-4f33-8262-e614e28d652f`; saved project
`local-7212f354a970dffa7d8b5520bdec2dfc`, gpt-6-astra/xhigh. Actual runtime ID and
checkout await setup; do not use the queued client ID for runtime tools. No reviewer yet.

 C36 remains
under relevance discussion; no legacy-data scan/repair is dispatched. Current totals:
47/53 handled (37 Awaiting manual acceptance + 6 Done + 4 Closed — no change),
C33 In progress, C36 Proposed, C05/C09 Blocked, C40/D05 Deferred. No integration,
manual acceptance or release claimed. Standing exclusive routing remains authorized.

## C20 final independent PASS — 2026-09-14

C20 is **Awaiting manual acceptance** after the sole reviewer
`01a09ffc-f686-7a01-a528-dbb393d4005e` cleared R1–R4 in the bounded recheck.
[Final report](/private/tmp/c20-evidence/reviewer/recheck-pass.md), SHA256
`29bb27ff3adced058e28522b4bfdf80674c5a30f6ab5890d03fbc1139206cbac`.
Actual implementation `/Users/christopher/.codex/worktrees/51a1/BingoWebpage`, branch
`codex/objective-identity-c20`, HEAD/base `c165bbc`, unstaged/uncommitted/unintegrated.
Reviewer verified 28 source files and 99 evidence artifacts and the 10-file remediation
delta. Full patch SHA256 `aed54b14c75b9fa008739751487e785fb608fc07f8b6b47d2b16e3657d9be6a8`;
manifest `3490a275daba44f91245afbc47ef7d8bf6cf84aa8b9b0085a0cf93052ff2ee2b`.
Evidence-lock/stable IDs, active published submission/progress and confirmed discard
are source-reviewed. R1 protects shared scoring denominator/EHB; R2 uses active approval
dimensions for completion/focus; R3 safely refuses expected artwork storage failures;
R4 places unchanged recovery authority in FUNCTIONAL_CONTRACTS 6.2.

Recorded 11/11 focused remediation and 17/17 affected passes overlap: not 28 unique
cases. Applicable original 27/27 core, 17 unique recovery, 3/3 source contracts and
Chrome binding/confirmation checks are reused. Release solution build zero warnings/
errors, scoped format/verify and reviewer diff checks pass. No planner runtime rerun.
C20 isolated PASS does NOT prove recovery after managed-artwork removal: missing,
deleted, unreadable or ambiguous required artwork refuses discard atomically. C21
retention/FK migration remains unmerged; later authorized integration must prove full
artwork recovery. Inherited active-image FK cycle and duplicate-source corrupt-editor
rendering limit remain recorded. No historical repair or combined release claim.
UI_PAGE_MATRIX preserves composition approvals and marks new C20 states awaiting
manual acceptance. Final PASS delivery succeeded; no duplicate implementer handoff.

## Remaining-scope clarification — 2026-09-14

User reaffirmed C26 dependency-reference lists and D03 application catalogue import
are not to be built; both are Closed — no change, not unfinished future work. C40
is explicitly outside the current batch sequence and remains Deferred. Owning
catalogue contracts were reconciled to remove the stale feature requirements.
C36 relevance is under discussion: it concerns ordinary Admin audit visibility of
hidden-event details, not public participant access. Existing C04/C15/C22 writer fixes
remain preserved; do not dispatch legacy scanning/repair from this relevance question.
C20 continues its current implementation/review sequence without interruption.
Totals: 46/53 handled = 36 Awaiting manual acceptance + 6 Done + 4 Closed — no change;
remaining: C20 In progress, C33/C36 Proposed, C05/C09 Blocked, C40/D05 Deferred.

## C20 recovery approved; implementation resumed — 2026-09-14

User explicitly approved the explained Discard private correction action: warn and
confirm loss of all unpublished board edits in the open correction, restore the working
board from current publication using exact identities, and close the correction so a new
one can begin. Published rules/evidence/progress/history remain intact. Central
FUNCTIONAL_CONTRACTS 6.2 / PRODUCT_REQUIREMENTS 10.2 now own this bounded behavior.
Existing implementer `01a09fbe-a7da-7d02-890f-559b0860920f` is authorized to complete
it in `/Users/christopher/.codex/worktrees/51a1/BingoWebpage`, branch
`codex/objective-identity-c20`, gpt-6-astra/xhigh. No policy wait remains for this action.

**C20/C21 artwork dependency resolved — 2026-09-14:** C20's isolated baseline still
removes managed artwork metadata/bytes with a privately removed tile. C21 already has
independently reviewed retention and its required FK migration, but remains unmerged.
The approved missing/ambiguous-restoration-data rule is C20's bounded stop boundary:
discard must fail clearly and atomically if required artwork cannot be restored, retaining
the open correction and all existing publication/evidence/progress/history. No success
claim, silent missing-artwork substitution, source import, migration or reconstruction.
Execute the missing-artwork rejection and unaffected recovery paths, then proceed to
C20's one independent review. The report must name this known incomplete recovery
case; passing this isolated scope is not proof of complete artwork recovery. Later
explicitly authorized C20/C21 integration must reconcile the shared Board removal path
and prove managed-artwork removal -> discard restoration. C21 manual acceptance and
combined integration/release remain outstanding. No new user decision is required for
the already-approved fail-closed boundary; this does not defer the dependency silently.

Core pre-recovery checkpoint is worker-reported complete: 27 focused PostgreSQL/HTTP
cases, three source checks, bounded browser checks, Release build and formatting pass.
[Recorded checks](/private/tmp/c20-evidence/checks.md). This is not independent review
or proof of the newly approved recovery action. Reuse unaffected evidence, execute
focused confirmed/cancelled/stale/unauthorized recovery, exact restored identities,
unchanged publication/evidence/progress, atomic failure and restart-correction checks.
Then implementer creates exactly one fresh saved-project Astra xhigh read-only reviewer;
review request ONLY to reviewer, required findings ONLY to implementer, final PASS ONLY
to this planner. One bounded same-reviewer remediation/recheck. No extra review gate,
source integration, schema/history repair, packaging or user-app/database changes.
Manual acceptance remains deferred, including the new confirmation/feedback states.

## C20 implementation active; persistence resolved — 2026-09-14

User approved the explained evidence-lock rule: wording-only corrections remain allowed;
any submitted evidence locks substantive requirements/scoring and removal, regardless
of evidence status. Central FUNCTIONAL_CONTRACTS 6.2 / PRODUCT_REQUIREMENTS 10.2 and
TICKETS active C20 contract are frozen. New isolated saved-project Astra xhigh direct
implementer was queued for C20 only, followed by one fresh Astra xhigh reviewer.
Implementer runtime `01a09fbe-a7da-7d02-890f-559b0860920f`, actual checkout
`/Users/christopher/.codex/worktrees/51a1/BingoWebpage`, branch
`codex/objective-identity-c20`, HEAD/base `c165bbc`; no reviewer yet. At this scope
question, only AGENTS/TICKETS and approved policy clauses changed, no production edits.

**C20 persistence resolution — 2026-09-14:** Planner authorizes the implementer's
schema-free retention of original approved requirement-drop rows as immutable identity
links. DATA_MODEL 10.6 owns the bounded contract. Exact scoped association only,
missing/ambiguous fail closed, snapshot-authoritative rules and consistent transaction/
lock ordering; no duplicate/reused identity for substantive replacement or historical
reconstruction. Execute private no-evidence removal/replacement -> new active-publication
submission -> blocked incompatible replacement publication, plus stable wording IDs and
failure/concurrency proof. No new preliminary review or user decision is needed.

Standing private handoff approval and implementer -> reviewer -> planner routing apply.
Core checks are recorded above; recovery implementation and independent review remain. C33/C36 remain Proposed, C05/C09 Blocked and
C40/D05 Deferred; C26/D03 closed without implementation. Current totals are in the scope clarification above.
Manual acceptance, source integration, packaging and user-data repair remain separate.

## C11 final review complete — 2026-09-14

C11 is **Awaiting manual acceptance** after the same independent reviewer
`01a09f79-5131-74e2-89f6-f07b219f22af` passed the bounded F01/F02 recheck.
[Final report](/private/tmp/c11-evidence/final-code-recheck.md); implementer
`01a09f71-ea16-7ad2-8620-ad5de1823736`, checkout
`/Users/christopher/.codex/worktrees/c4b5/BingoWebpage`, branch
`codex/finalized-prelive-roster-c11`, base/HEAD `c165bbc`. Work remains unstaged,
uncommitted and unintegrated. Final PASS delivery to this planner succeeded.

The finalized-pre-Live Admin departure, optional vacancy and explicit replacement
journey updates current publication while preserving old picks, snapshots and
reservations. Required readiness R1–R4 integrations are covered. F01 scopes the new
Captain publication audit inside its transaction; F02 gives new pre-Live notices
accurate EN/DA wording while preserving existing Live notices and routing.
Reviewer verified all 17 repository files and 58 evidence artifacts, including the
complete tracked/untracked patch SHA256
`d26572cd64e72799f676c41c2761fd5c3b8de463b2d5f2ab2f2e41484a881c81`.

Evidence supports **51 current distinct passing cases (42 C11 + 9 existing)**,
reusing unaffected earlier results rather than claiming a fresh 51-case run.
All 16 selected correction/regression cases now pass: 14 in the correction run,
then two rerun after fixing an invalid hide fixture; original failures remain recorded.
Final Web/Razor Release has zero warnings/errors; scoped format and reviewer diff
checks pass. Planner reused the independent report without runtime or review reruns.
UI_PAGE_MATRIX retains prior composition approvals and marks the new Participants,
Teams/Draft and public Teams states awaiting manual acceptance. Running/Paused
departures remain explicitly deferred; no all-U3-02 closure, retained-data repair,
combined integration/release, user-app change or packaging is claimed.

Total **44/53 handled**: 36 Awaiting manual acceptance, 6 Done, 2 closed without
change; 3 Proposed, 2 Blocked, 4 Deferred. Fourteen reviewed checkouts remain isolated.
C20 was subsequently approved; the active dispatch above supersedes the previous policy wait.
Manual acceptance, source integration/publication and user-data repair remain separate.

## C11 notification prerequisite resolved — 2026-09-14

Planner verified C17's accepted destinations and baseline WithdrawLiveAsync's current
Admin-only routing. Minimal recipient-role split is authorized ONLY for new finalized-
pre-Live withdrawal: enabled Admin -> existing Admin participant context, ordinary
remaining leadership -> existing published Teams page, using current role and existing
recipient/notification mechanisms. Preserve atomicity/deduplication/privacy and active
publication; follow actual links as each recipient type in focused proof. No old Live
path change, C17 source import or retained-message rewrite. Central C11 contract updated.
Include in the one readiness plan/review; no production edits before READY/go-ahead.

## C41 review complete — 2026-09-14

C41 is centrally **Awaiting manual acceptance** after [independent PASS](/private/tmp/c41-evidence/independent-review.md)
from reviewer `01a09f67-594b-7830-ab56-9d005d9ef592`. Implementer
`01a09f4f-0ecb-7383-8574-03121ed5416a`, actual checkout
`/Users/christopher/.codex/worktrees/764b/BingoWebpage`, branch
`codex/public-font-loading-c41`, base/HEAD `c165bbc`, unstaged/uncommitted. Reviewer
verified 102 hashes/full/source diff and five-file boundary; original fonts/licenses/
Admin and final typography preserved, Medium WOFF2 37,880 versus 103,360 bytes.
40 actual-app captures/20 pairs cover cold/slowed/warm/blocked at two widths. Earlier
font discovery and zero measured Signup CLS come with **340–552ms later slowed FCP**;
ordinary timings vary. No successful duplicate requests/preload warnings; actual 304
body revalidation and readable blocked controls. One local headless Chrome sample/
condition and one locale/theme/reduced-motion setting; not production-cache, faster
paint or universal no-swap proof. Web/Razor Release evidence zero warnings/errors
reused; no planner runtime reruns/manual approval. Exact authorized pass delivery
succeeded. Layout/CSS/C37 integration overlap remains for later authorized work.

## C10/C38 review complete — 2026-09-14

[Passed F01 recheck](/private/tmp/c10-c38-evidence/r1/independent-recheck.md) from reviewer
`01a09f3d-c7ed-71b0-8aac-142630432b49` clears required findings. Implementer
`01a09f2a-cc4a-7f60-aff3-4786ca721e65`, actual checkout
`/Users/christopher/.codex/worktrees/c68a/BingoWebpage`, branch
`codex/draft-start-readiness-c10-c38`, base/HEAD `c165bbc`, unstaged/uncommitted.
Reviewer verified 12 hashes/full patch and original reviewed boundary. F01 restores
source-based MemberView.External without changing actual-preformed-membership pool
selection; no C11 departure/handler/publication policy. Pre-fix control regression
failed; new and affected C10 cases pass 2/2. Total 23 distinct cases pass overall;
unchanged C38 and original source/full-solution build evidence reused. Affected
Web/Razor Release zero warnings/errors; scoped format/whitespace clear. No planner
runtime rerun. C38 recording storage double is not binary processing or a new
cross-transaction event/access race test. Exact pass delivery succeeded after the
user's "send it" instruction; original rejected attempt is historical.

Total **42/53 handled**: 34 Awaiting manual acceptance, 6 Done, 2 closed without
change; C41 Ready, 4 Proposed, 2 Blocked, 4 Deferred. Twelve reviewed checkouts remain
isolated/uncommitted/unintegrated. Later overlaps include Draft/C12, lifecycle/C15/
C16/C18, Accounts/C39, evidence/C28/C32 and shared authorities. Continuous execution
remains active; manual acceptance and integration/release/publication/data repair
remain separate. Do not silently resolve remaining product/data decisions.

## C21/C37 review complete — 2026-09-14

C21/C37 are centrally **Awaiting manual acceptance**. [Passed R1 recheck](/private/tmp/published-content-c21-c37-evidence/r1/reviewer-recheck.md)
from reviewer `01a09f18-1c57-7672-88e9-71c359f079b5` clears all required findings.
Implementer `01a09f06-ecb1-7882-a95f-a12285d2f8cf`, actual checkout
`/Users/christopher/.codex/worktrees/e588/BingoWebpage`, branch
`codex/published-content-c21-c37`, base/HEAD `c165bbc`, unstaged/uncommitted.
All 23 hashes/exact final full diff verified by reviewer. Seven distinct PostgreSQL/
storage/real HTTP cases pass; R1's three reexecuted cases are a subset, not extra.
R1's stronger nav assertion failed before fix; final cancellation/archived/finalized
controls pass. Existing full-solution and affected Web/Razor Release evidence reused,
zero warnings/errors, format/whitespace clear. No planner reruns/manual/cumulative claim.

C21 retains approval-referenced assets, with scoped current/retained access, bounded
unreferenced cleanup and necessary FK-removal migration/designer/snapshot. Local
DATA_MODEL owns fail-closed rollback limitation after a retained image outlives its
tile; no missing-asset repair, backup restore or that rollback is claimed. C37's R1
removes cancellation context tabs and secondary-nav spacing using existing layout.
Later authorized integration must reconcile C19/C22 Admin Board overlap; C20 is separate.
Exact pass delivery succeeded after user payload/destination approval; no retry needed.

Total **40/53 handled**: 32 Awaiting manual acceptance, 6 Done, 2 closed without
change. Remaining 7 Proposed, 2 Blocked, 4 Deferred. Eleven reviewed implementation
checkouts remain isolated/uncommitted/unintegrated. Standing continuous authorization
remains active, manual acceptance deferred; no release/packaging/data-repair authority.

C10/C38 is the next authorized batch; the user decision is settled. Current base
creates emergency ActiveFrom at scheduled start and enables only when uploads are
already open; the bounded approved policy resolves both without early submissions.
No required unresolved C38 product decision remains at dispatch.

## UI test-repair/F01 review complete — 2026-09-14

[Passed independent recheck](/private/tmp/ui-test-repair-t01-t04-evidence/independent-recheck.md)
from reviewer `01a09ee6-1aef-7eb1-80b9-51d015861ca3` clears all required findings.
Implementer `01a09ed4-3378-7610-828a-12527278488c`, actual checkout
`/Users/christopher/.codex/worktrees/0168/BingoWebpage`, branch
`codex/ui-test-repair-t01-t04`, base/HEAD `c165bbc`. Eight hashes/exact final patch
verified by reviewer; source remains unstaged/uncommitted. Three distinct C# cases
and all five distinct Node files pass. User explicitly authorized F01 after initial
review: the sole production edit points Players panel aria-labelledby to its existing
unique localized view link. Unchanged reference safeguard passes; affected Web/Razor
build zero warnings/errors (not Release solution build). Unaffected evidence/review
reused, no planner reruns. No manual/browser/live-AT or cumulative release claim.
Initial incomplete review remains historical. The zoom-authority wording discrepancy
remains recorded and did not change the explicit accepted batch contract.

Total **38/53 handled**: 30 Awaiting manual acceptance, 6 Done, 2 closed without
change. C21/C37 Ready; 7 Proposed, 2 Blocked, 4 Deferred. Ten reviewed batch checkouts
remain isolated/uncommitted/unintegrated. Continue after pass under standing workflow;
manual acceptance, integration/release, publication and data repair remain separate.

## T03 bounded test remainder clarification — 2026-09-14

Implementer reported the corrected toggle passes expanded/collapsed interactions but
exposes at least 11 old source assertions in the same leaderboard file. Planner
confirmed stale markup/localization expectations and authorizes the bounded same-file
remainder update against accepted production composition. The implementer corrected
its initial inference: table markup remains in Board.cshtml with changed panel/header
anatomy; no partial extraction is established. TICKETS T03 and
active contract are updated; no production changes, weak assertions, extra reviewer
or passing-result claim. Implementer continues its existing batch and full-file checks.
T01 named fixes appear already present and await focused verification; do not force
edits for already-correct tests. Central UI_SYSTEM's reported old zoom wording conflicts
with D04's completion record; current explicit accepted T02 contract governs this
batch, and the inconsistency remains recorded without a documentation audit.

## C24/C39 review complete — paused for the night — 2026-09-13

C24/C39 are **Awaiting manual acceptance**, reconciled from the passed independent
[review report](/private/tmp/admin-stale-change-batch-evidence/independent-review.md).
No required defects, scope deviations or missing focused proof remain. Reviewer
`01a09cb1-c492-7860-aa2f-00eed3defab4` sent its pass only to this planner; delivery
succeeded. Implementer `01a09c9f-16f7-78a0-af80-00252b943d7d` owns isolated checkout
`/Users/christopher/.codex/worktrees/0a19/BingoWebpage`, branch
`codex/admin-stale-change-ticket-batch`, base/HEAD `c165bbc`. All work remains
unstaged/uncommitted. Reviewer verified all 13 hashes and exact full tracked/untracked
patch against that checkout. [Implementation evidence](/private/tmp/admin-stale-change-batch-evidence/implementation-report.md).

Applicable evidence reused: 18 distinct passing PostgreSQL/HTTP integration cases
across focused commands; four Chrome confirmation cases; recorded silent Node/scoped
format exits 0; Release solution build zero warnings/errors; whitespace clear.
The initial 17/18 result retains its fixture-query failure; final affected C24 cases
passed subsequently. Browser checks used shipped scripts/captured HTML and controlled
transport, not a separate live browser-to-PostgreSQL session or manual acceptance.
Snapshot/identity preservation was source-reviewed, not a recreated approved-board
history fixture. No planner reruns, migration, retained-data repair or release claim.

Total: **34/53 handled** (26 Awaiting manual acceptance, 6 Done, 2 closed without
change); remaining 13 Proposed, 2 Blocked, 4 Deferred. All nine reviewed batch
implementations remain isolated/uncommitted and unintegrated. Manual acceptance
remains user-deferred; no combined release/integration or packaging/publication.

**Latest user instruction overrides automatic continuation:** record this batch and
stop for the night. This is now done. No batch is active. Do not dispatch, schedule
or begin another batch until the user resumes tomorrow/later; elapsed time is not
resume authorization. Standing handoff approval and exact routing remain: implementer
review/recheck only to reviewer; reviewer pass only to planner, findings/incomplete
only to implementer. Continuous batch workflow may resume on user instruction.

## Final-review integrity review complete — 2026-09-13

C31/C32/C35 are **Awaiting manual acceptance**, reconciled after the user directly
provided the passed [independent review](/private/tmp/final-review-integrity-batch-evidence/independent-review.md).
Reviewer `01a09c96-559d-7490-b652-efcfaa426c05` found no required findings, missing
focused proof, scope deviation or established unresolved data obligation.
Implementer `01a09c89-371d-75a1-979c-00f27f17e7da` worked in
`/Users/christopher/.codex/worktrees/0e93/BingoWebpage`, branch
`codex/final-review-integrity-ticket-batch`, base/HEAD `c165bbc`; source remains
unstaged/uncommitted and isolated. Reviewer matched the full tracked/untracked
diff and all eight file hashes against the actual checkout.

Applicable evidence: 13 distinct passing cases across runs (8 PostgreSQL/HTTP,
5 domain), zero-warning/error Release solution build and scoped format/whitespace.
The review clarifies that C32's future ordinary-cutoff defect is proved by the two
PostgreSQL actor cases; domain cases supply other lifecycle/cutoff controls. The
PostgreSQL submission fixture uses EF-created disposable schema, not a retained-DB
migration rehearsal; no migration is part of this batch. No planner reruns.

Automatic approval review rejected the reviewer's one tool delivery to this exact
planner for unverified destination trust despite standing authorization. No retry
or successful tool delivery is claimed. The user then supplied the report directly
in this planner task; receipt and central reconciliation are now complete. The
report's pending-receipt/reconciliation note is historical; no further send needed.

Total: **32/53 handled** (24 awaiting manual acceptance, 6 Done, 2 closed without
change); remaining 15 Proposed, 2 Blocked, 4 Deferred. All eight reviewed batch
implementations remain isolated/uncommitted. Manual acceptance is user-deferred;
combined integration/release, packaging, real-data repair and deployment remain
separate and unperformed. No batch is active; await next explicit assignment.
Routing remains implementer review/recheck only to reviewer; reviewer pass only
to planner, remediations/incomplete only to implementer. No checkpoint/dispatch
notice from implementer to planner and no automatic next batch.

## Participant-evidence review complete / standing handoffs — 2026-09-13

C28/C29/C30 are **Awaiting manual acceptance**, reconciled centrally after the
same reviewer's passed R1/R2 recheck. Implementation remains isolated, unstaged and
uncommitted in `/Users/christopher/.codex/worktrees/3ae0/BingoWebpage`, branch
`codex/participant-evidence-ticket-batch`, base `c165bbc`; implementer
`01a09c59-5e5b-75d1-89ce-072b5d4477b5`, reviewer
`01a09c68-a1fd-74a2-a253-c62a2b8c5dce`. [Passed recheck](/private/tmp/participant-evidence-batch-evidence/independent-recheck.md)
and [remediation report](/private/tmp/participant-evidence-batch-evidence/remediation-report.md)
own evidence: 14/14 R1 affected cases plus separate exact R2 pass (15 distinct),
zero-warning/error Release build, scoped format/whitespace; unchanged C29/C30
service/HTTP/Node evidence reused. No required findings remain; no planner reruns.
Reviewer verified full tracked/untracked diff and 23 file hashes in the actual checkout.

Passed delivery reached planner `01a09c57-1f38-7191-94de-e11647ffbcf1` after the
explicitly approved retry; the report's prior delivery-block note is now historical.
User further clarified standing authorization for batch-relevant implementer/reviewer/
planner handoffs without repeated approval requests. AGENTS/TICKETS own that rule;
future briefs must include it and exact routing IDs. Automatic rejection still stops
that action; record/report it once without bypass, retries or renewed approval requests.

Total: **29/53 handled** (21 awaiting manual acceptance, 6 Done, 2 closed without
change); remaining 18 Proposed, 2 Blocked, 4 Deferred. Manual acceptance is user-deferred.
All seven reviewed batch implementations remain isolated and uncommitted; no combined
integration/release gates, packaging, data repair or deployment is claimed or authorized.
No batch is active. Await next explicit assignment; do not auto-dispatch.

## Planner handoff — 2026-09-13

User requested a fresh planner for context compaction only. New project-associated
“BingoWebpage ticket planner” task is queued; setup ID
`client-new-thread:8780bf30-4e29-4c21-839b-02f896b37e33` (not a runtime task ID).
It has the ledger, isolated batch locations, latest passed review and current
workflow. This checkout remains the coordination ledger. No next batch or other
execution was authorized. Future reviewers send passes to the new planner's actual
runtime ID once known; findings still go only to their implementer.

## Audit-atomicity review complete — 2026-09-13

C04/C15/C22 are **Awaiting manual acceptance**, reconciled in TICKETS.md.
Implementation: `/Users/christopher/.codex/worktrees/ffd6/BingoWebpage`, branch
`fix/audit-atomicity-ticket-batch`, base `c165bbc`; implementer
`01a09c30-c4eb-7892-a02c-d3253a7d4d89`. Astra xhigh reviewer
`01a09c49-cfcc-7d42-8b9d-f6f2f07cc4aa` passed after its bounded C22 R1 recheck.
The correction captures original board state before removal auto-unapproval/lease
renewal. Its PostgreSQL case reproduced the defect then passed 1/1; initial 44/44
focused results were reused for unaffected behavior, not rerun as a whole.
Release compilation and scoped format/whitespace evidence are recorded in the
[review report](/private/tmp/audit-batch-review/reviewer-report.md).
No required findings remain. Manual acceptance is user-deferred; historical-data
repair and combined release/package gates remain separate. Total: 26/53 handled
(18 awaiting manual acceptance, 6 Done, 2 closed with no change).
No next batch, additional checks or publication started. Older handoffs below are
historical and their then-current counts/assignments do not supersede this result.

## Board-approval review complete — 2026-09-13

C19/C23/C25 are **Awaiting manual acceptance**, reconciled in this TICKETS ledger.
Implementation is isolated in `/Users/christopher/.codex/worktrees/0373/BingoWebpage`,
branch `fix/board-approval-ticket-batch`, base `c165bbc`; task
`01a09c0b-f326-7ba0-9054-5f60de7bfdbb`. Reviewer
`01a09c21-4907-71f3-8701-ec41e44350ac` cleared the batch after a bounded historical
importer correction preserving the reviewed drop kind and snapshot EHB. Its focused
PostgreSQL regression passed 1/1; initial 21 new/five consumer cases and applicable
controls/build/format evidence were reused without reviewer reruns. [Passed recheck](/private/tmp/board-approval-batch-evidence/r1-recheck.md).
No required review findings remain. User-deferred manual acceptance, retained-data
policy/actions and combined release gates remain separate. Fifteen tickets now await
manual acceptance, six are Done and two closed with no change: 23/53 handled.
No further batch, check, remediation or publication was started. Future tasks stay
under BingoWebpage; reviewer findings only to implementer and pass only to planner.

## Participant-flow review complete — 2026-09-13

C13/C14/C17 are **Awaiting manual acceptance**. Reviewer
`01a09bf8-fde0-7803-9378-37f434a46a59` passed the batch after the bounded C17
published-board/reopened-roster privacy correction; no required findings remain.
[Final review](/Users/christopher/Documents/Codex/2026-09-13/participant-flow-ticket-batch/outputs/independent-recheck.md)
reused 24 distinct initial passing cases across focused commands plus two affected
ordinary-owner cases after correction (30 notice journeys), and the zero-warning
Release build. Do not misstate this as one combined passing run or reviewer reruns.
Retained unread notification URLs may still need separately authorized inspection
or recovery if present; no affected user rows were established or rewritten.
No historical CSV identity inference/repair. Manual walkthroughs remain deferred;
combined package/release gates remain unrun. No new batch or other work authorized.

Twelve tickets now await manual acceptance; 6 are Done and 2 closed with no change
(20/53 handled). Other implementations and announcements remain isolated. Future
implementer/reviewer tasks belong under the saved BingoWebpage project. Required
review findings go only to the implementer; passed reviews only to this planner.
No task was created or moved as part of recording this passed result.

## Event-setup review complete — 2026-09-13

Next-batch routing correction: reviewers return required findings only to the
implementer; only passed independent reviews are sent to this planner. No dual
delivery. AGENTS and TICKETS record the rule; future task briefs must include it.
No new batch was dispatched by this routing update.

C01/C02/C03 are **Awaiting manual acceptance**, reconciled into this checkout's
TICKETS ledger from the implementation handoff and final review. C01/C02 cleared
original review. C03 R1 stale-warning scope and R2 Danish rendering were remediated
under separate user authorization and cleared by Astra xhigh recheck
`01a09bd5-4658-7480-804c-c83fd5657781`. [Final recheck](/Users/christopher/Documents/Codex/2026-09-13/event-setup-ticket-batch/outputs/remediation-review.md)
reused four original failures, 31 passing affected cases and the zero-warning
Release build; no reviewer reruns. No required code/check/decision blocker remains
in this scope. Nine tickets now await the user-deferred combined manual walkthrough.
This supersedes older pending-review directions below; no new review, remediation,
batch or packaging/deployment is authorized by receipt of this result.
Implementation remains isolated on `fix/event-setup-ticket-batch` in
`/private/tmp/BingoWebpage-event-setup-ticket-batch-20260913`.

## Active event-setup batch handoff — 2026-09-13

User authorized C01/C02/C03 under the same Astra xhigh workflow. Task
`01a09bb1-0844-7d90-b284-bc079da778ef` owns direct implementation in
`/private/tmp/BingoWebpage-event-setup-ticket-batch-20260913`, branch
`fix/event-setup-ticket-batch`, base `c165bbc`. Its TICKETS contract and outcomes
own execution until handoff. After focused checks, it creates one fresh visible
Astra xhigh read-only reviewer in that checkout; the reviewer reports to the
implementer and this planner. No implementation subagents, Ponytail, repeated
reviews/checks by default, wait/poll loop or commit/push/PR/deployment.
Clear review means Awaiting manual acceptance under the user's deferred walkthrough
decision. Six earlier tickets already have that status; previous implementations
remain isolated. Announcement work is paused. Older dispatch directions below are
history, not authorization to rerun an earlier batch.

## Manual acceptance deferred — 2026-09-13

The user will perform the combined manual walkthrough after ticket implementation
and review work finishes. C06/C07/C08 and C12/C16/C18 are **Awaiting manual acceptance**:
implementation, focused checks and independent review passed; no required remediation.
This supersedes earlier pending-review/dispatch directions below. See TICKETS for
completed outcomes and the deferred-acceptance policy. Do not repeat passing checks
or create another reviewer just to close the status. Keep actual blockers/data
obligations explicit; C12's retained-data proposal/limitations and C05/C09 exclusions
are unchanged. Combined release gates and packaging/deployment remain separate.
No additional batch is authorized by this acceptance deferral alone.

## Active roster batch handoff — 2026-09-13

User authorized C12/C16/C18. Astra xhigh task
`01a09b93-dc09-7a12-84bd-211b395aef11` owns direct implementation in
`/private/tmp/BingoWebpage-roster-ticket-batch-20260913`, branch
`fix/roster-ticket-batch`, base `c165bbc`. Its TICKETS batch contract owns scope.
After focused checks it is explicitly authorized to create one fresh visible
Astra xhigh read-only reviewer task and have it report to the implementer and
this originating planner. No implementation subagents, Ponytail, default repeated
checks, commit/push/PR/deployment or further tickets. The implementer reports its
reviewer handoff without polling. Announcement work remains paused.
C06/C07/C08 cleared independent review with no findings; the reviewer confirmed
recorded 20/20 tests without rerunning them. Those tickets remain Review pending
separate acceptance; their uncommitted implementation remains in its own checkout.

## Ticket workflow handoff — 2026-09-13

Announcement work is paused. The recovered
[TICKETS.md](TICKETS.md#approved-ticket-workflow--2026-09-13) now holds the sweep
ledger and approved Astra xhigh trial: direct implementation/checks/remediation,
fresh Astra xhigh review at the completed ticket or coherent batch boundary,
and no automatic worker/subagent delegation or model fallback. AGENTS records
the matching override. User authorized C06/C07/C08; their source gaps were confirmed
against fetched origin/main `c165bbc`. Astra xhigh task
`01a09b77-fc1b-7892-b2b6-09e6cb25932c` owns direct implementation in
`/private/tmp/BingoWebpage-account-ticket-batch-20260913`, branch
`fix/account-ticket-batch`. Its local TICKETS ledger owns execution outcomes until
handoff; reconcile them here afterward. The task must finish the batch and focused
checks, report back, and leave it at Review for a later independent assignment.
No subagents, commit, push, PR or deployment authorized. Ponytail was uninstalled
at the user's request and must not be used. Other ticket outcomes retain their
recorded 2026-09-09 evidence; reconcile selected tickets before future execution.

## Active drop-announcement implementation — 2026-09-12

2026-09-13 live-feed timestamp regression corrected: inserted Drops cards derive
elapsed time from approval timestamps; existing cards retain their times. A fixed
initial (ApprovedAt, SubmissionId) boundary excludes unloaded history, including
same-time lower-ID rows, and overlapping invalidations queue without duplication.
Empty feeds use the request-start time captured before the initial Board query.
Luna `drop_live_timestamp_fix` and named finisher `drop_feed_boundary_finish` completed
the correction; Terra `drop_live_timestamp_review` cleared the final tuple fix.
Both announcement Node harnesses pass, including 25 visible vs 100 fetched/history,
age/order/same-time/sequential/overlap cases. Web Release build zero warnings/errors
passed before final JS-only fix; scoped diff checks pass. Broader
`public-recent-drops.test.js` fails at its closed-masthead assertion (line173);
scoped source comparison shows this correction does not alter asserted masthead
markup, but the old baseline was not executed. Before copies:
`/private/tmp/drop-announcements-before/`. User visual check pending; restart/refresh,
no migration or reset. No preflight/commit/push. Separate pre-existing finding,
outside this correction: notification-inbox reconnect subscribes without emitting
the feed's progress browser event, so missed feed updates may wait for next approval.

2026-09-13 quick label correction complete: progression kicker now includes existing
ProgressAfter/Target (`New tile progression: 2 / 5`; user-corrected `Nyt tile fremskridt: 2 / 5`).
Completed kicker remains green and counter-free. Shared partial/JS/DA resource only,
plus existing harness EN/DA assertions. Both Node harnesses and Web Release build
(zero warnings/errors) pass; scoped diff check passes. No backend/migration change.
User will inspect English/Danish presentation next; manual approval remains pending.
Follow-up direct corrections complete: Danish `Leaderboards` and its help reference
retain that term. CLEAR ALL NEW uses the shared `showBingoToast` owner for localized
success and failure feedback, preserving clearing semantics. Existing harness covers
EN/DA success and failed requests without success toast; both Node harnesses, Web
Release build (zero warnings/errors) and scoped diff checks pass. No integration
rerun, preflight, reset, commit or push.

2026-09-13 follow-up complete: persisted `LastAutomaticExpansionOrdinal` and additive migration
`20260913121927_AddDropAnnouncementExpansionBoundary` implement newer-approval gating;
automatic expansion selects newest and manual selection persists. Terra High
`banner_expansion_review` clears boundary/selection and the named localization fixes:
banner position plus live-added Drops card ARIA/relative-time text. Luna High
`banner_localization_finish` reports both Node harnesses (including Danish output)
pass, Web Release build zero warnings/errors, EF no pending model changes, and focused
PostgreSQL persistence tests 7/7 under sanctioned socket-enabled execution. This
supersedes the interrupted/permission-blocked initial test attempt. Domain suite was
not rerun; no domain test changes in this follow-up. Scoped diff checks pass.
Apply the additive migration to the user's Development DB before restarting; no reset
is needed or performed. User manual acceptance remains pending. DELIVERY_PLAN owns
scope; root owns docs. No ordinary browser preflight, packaging or deployment.
NEW mark design/clearing review is the user's next pass (wording only was included).
Before-pass tracked diff/status: `/private/tmp/drop-announcements-before-pass.*`;
localization remediation copies: `/private/tmp/banner-localization-baseline.jnvUgm`.

Latest manual correction — 2026-09-13: user accepts background and outlines on
non-Board pages, including the neutral 1px top underlay beneath the coral countdown.
The font-arrow alignment adjustment failed manual review; navigation now uses centred
inline SVG chevrons. Remaining banner colours now use scoped tokens with Board-matched
light/dark values across public pages. Accepted surfaces, outlines, control geometry,
X hover and motion remain intact. Luna High scoped source/cascade and diff checks pass;
user visual acceptance remains pending. Static partial changed: restart the app and
refresh. No build/preflight, backend or runtime-data changes for this small correction.

2026-09-13 visual rejection: the user reports the approved prototype animations and
drop-switch controls were not preserved in the integrated banner. The exact prototype
HTML named in DELIVERY_PLAN is explicitly reactivated for a direct comparison.
Terra High `banner_prototype_comparison` completed read-only source/cascade comparison.
Confirmed: missing staged entry/delayed exit and measured expanded/compact transition;
prototype sequential 320ms switch stages replaced with concurrent 240ms motion;
missing counter styling class; nav min-height makes controls taller than prototype;
different X hover, mobile sizing, duplicated counter, shadow/rule/artwork treatments.
Current countdown retains a static coral border beneath its shrinking stripe, which
may dilute the draining effect. Runtime animation was not re-previewed. The
differences were presented to the user. The user then authorized restoring every
reported visual/motion difference to the prototype EXCEPT current X hover (blue icon,
no background), while retaining agreed functional additions and header stacking.
DELIVERY_PLAN's Approved prototype restoration section owns this bounded correction.
Luna High `banner_prototype_restoration` completed the banner partial/CSS/JS port and
focused client tests. Both runnable Node harnesses and JS syntax check pass; Web Release
build has zero warnings/errors and scoped diff check passes. Terra High scoped
comparison and fixes-only recheck are now clear: later-arrival claims animate an already
visible compact banner through the measured transition, and dark shadow matches the
prototype. Both Node harnesses/syntax/diff checks pass after these JS/CSS-only corrections.
No worker remains active; only user visual acceptance is outstanding. Restart the user's
app with the usual HTTPS run command to load changed Razor markup, then refresh.
Before-pass copies are in
`/private/tmp/bingo-banner-restoration-baseline-20260913`.
Additional user finding included in this pass: background and TOP edge become transparent
outside Board/views. Correct shared token scope for opaque surface/coral edge across
non-Admin pages and both themes; the other outlines were not reported transparent.
Root owns authority updates. Resolve only concrete findings from that comparison;
leave ordinary browser preflight and visual acceptance to the user.
Prior passing functional review/tests do not establish visual or animation acceptance.

2026-09-13 banner correction: shared header stacking level 1100 obscured the banner
at 20. Direct user-authorized CSS fix sets `.drop-announcement` to 1101; native dialog
top-layer priority remains intact. Scoped cascade/diff checks pass; user visual recheck
pending. No build, broader preflight or runtime data changes were needed.

2026-09-13 follow-up complete: `test-15-dkl-live` now seeds 15 pending submissions.
Focused PostgreSQL approval proof confirms whole-tile completion for Superior Slayer,
linked Araxxor, Vorkath and Alchemical Hydra, with Phosani's Nightmare remaining partial.
Existing retained review states and approved progress are preserved. Only the seeder,
its existing focused test and corresponding plan/checklist changed. Focused test 1/1,
Release integration build (zero warnings/errors), scoped format and diff check pass.
Current runtime/manual data was not reset; new fixtures take effect on the next existing
Development reset. No browser preflight or announcement behaviour changes; no active worker.

Use `/private/tmp/BingoWebpage-drop-announcements`, branch `drop-announcements`,
base `c165bbcb321547637d03b4e9dc3d2e206e5944b3` (PR #8 merge from fetched main).
The co-captain/recovery checkout and packaging instructions below are historical.
Preserve other checkouts and their unapproved sweep/artwork work.

Approved behaviour and implementation contract are in DELIVERY_PLAN,
`Drop announcements and NEW tracking`. The standalone prototype's design/animations
are accepted; implementation is now authorized. Latest correction: the thin
coral top border drains over 10 seconds, refills/holds on focus, and starts a fresh
10 seconds on leaving interaction. It is separate from green `Tile completed` status
and the persisted two-minute account/event automatic-expansion cooldown.

Current authorization: complete the three implementation passes in the approved
contract, focused review/remediation and acceptance preflight, then user visual review.
No new load-test gate (existing 100-viewer production capacity evidence accepted).
One Terra High read-only readiness review is complete; outcome recorded in the
DELIVERY_PLAN contract. Technical corrections: stable tracking initialization,
immutable approval-time completion fact, unique/concurrent independent acknowledgements,
focused incremental query and new exact-drop parameter on the existing Drops route.
Existing `test-15-dkl-live` seed is the acceptance basis with one controlled finishing
contribution; checklist DA-01..09 is planned, not yet executable. Application and
planning changes are uncommitted; manual reachability and visual acceptance remain.

User resolved the lifecycle boundary: announcements continue through AwaitingFinalReview;
finalization clears every account's banner queue, Drops NEW marks and navigation badge,
including offline accounts, while preserving actual drop/evidence history. The contract,
workflow authority and DA-09 acceptance step now include this requirement.

Pass 1 handoff complete from Luna High `drop_backend_finish`: migration
`20260912175815_AddDropAnnouncements` (designer/snapshot included), two tracking tables,
tracking start/generation and immutable completion facts, eligibility, bounded query/
paging/NEW lookup, atomic cooldown and independent acknowledgement/finalization rules.
The initial worker's patch was retained; a fresh finisher corrected EF projection
translation, active-team eligibility, a concurrent-claim loser and bounded API gaps.
Focused `DropAnnouncementRulesTests`: 3/3 pass; PostgreSQL
`DropAnnouncementPersistenceIntegrationTests`: 4/4 pass, including snapshot dismissal
beyond 100 approvals while retaining later arrivals. Infrastructure/Web no-restore
builds: zero warnings/errors; scoped diff check passes. Test-host socket restrictions
were resolved through approved escalation. No independent implementation review yet.

API source: `src/Bingo.Application/Announcements/IDropAnnouncementService.cs`.
GetCurrentAsync/GetAsync expose queueLimit/queueOffset (bounded), queue/NEW totals and
generation/cooldown; GetNewSubmissionIdsAsync checks up to 100 requested IDs. Entries
include item/tile artwork references, identity, approved evidence metadata and completion.
SnapshotSequence uses committed approval ordinals under the existing event lock;
whole-queue dismissal preserves later approvals. Finalization emits the existing
generic invalidation after commit. Migration application/model consistency passes.
Pass-2 shared banner/transport is implemented; Web build and runnable fake DOM/clock
Node checks passed for focus countdown, switching/bursts/cooldown/navigation.
Pass 3 implementation is complete: exact popup links, NEW feed/nav controls, bounded
live feed and deterministic Development evidence. Real HTTP login/CSRF/account-scope
test passes (1/1); successful popup-load acknowledgement and feed hooks have Node/source
checks, but actual browser interaction and rendered navigation remain unverified.
`test-15-dkl-live` has pending non-drop evidence on `Superior Slayer`, target/claimed
contribution 4, note `DA-07 pending non-drop completion: Superior Slayer.` Accounts:
SeedEvidenceCaptain, SeedEvidenceCoCaptain and SeedEvidenceParticipant. Including the
two new tables in the existing explicit reset list fixed the reset FK failure;
`DevelopmentDklManualObjectiveHasNoEventOrApprovalDropSnapshots` passes (1/1).
Web and integration-test project builds have no warnings/errors; diff check passes.
All implementers released their files. Root owns planning/authority documentation.

Legacy Refresh now cleanup is now complete after explicit informed user approval
(`Approve removal`) resolved the automatic approval block. Old Board/TeamBoard blocks,
script includes, `_PublicProgressScripts.cshtml`, `public-progress.js` and unused notice
CSS were removed through the normal patch tool; independent notification-inbox live
consumer remains. No workaround used. Scoped old-reference check/diff check and Web
build pass. Pass-2 files released for pass 3: exact popup links, NEW feed/nav controls,
bounded live feed and focused HTTP/interaction verification. No current approval blocker.
Fresh Terra High full-diff review and fixes-only recheck are clear. The fresh Luna
remediator fixed event-wide receipt (formerly self-only), stale rendered NEW labels
after acknowledgement, and submission drawer/result protection including the async
claim race. Focused PostgreSQL integration 6/6 and both runnable Node files
(`drop-announcement.test.js`, `drop-announcement-reconciliation.test.js`) pass.
Release Web build has no warnings/errors; scoped formatting and diff check pass.
The final race correction was JS-only. No broader repeat review is required.
DATA_MODEL's bulk-acknowledgement description was corrected to match snapshot-bound
acknowledgement rows rather than fields on the cooldown entity.
Luna High `drop_verification` completed initial gates/preparation: full solution Release
build passed with zero warnings/errors after restore. Scoped format verification found
migration whitespace/charset and imports in DI, BoardModel, Program and the announcement
integration test; these exact corrections are included in `drop_remediation` ownership.
Isolated Development runtime: `http://127.0.0.1:5169`, PostgreSQL container
`bingo-drop-announcements-verifier-pg` on loopback port 55499. Migration, catalogue
snapshot and Development reset succeeded; live/ready/login return 200 and browser login
form is reachable. No user-owned database was reset. Keep this isolated runtime for
the verifier continuation. User explicitly waived ordinary agent browser preflight:
"Skip the preflight UNLESS its something i cant easily do myself". Keep the passing
automated authorization/persistence/concurrency/race checks; leave ordinary browser
and visual acceptance to the user. No screenshot artifacts were captured.
Before stopping, the verifier followed real Admin -> Review -> Superior Slayer Details
-> Approve successfully (4 contribution). That fixture is now approved in the isolated
runtime. Its participant queue appeared empty because the restarted app loaded old
Debug assemblies; root verified their paths/timestamps against the corrected Release
build. Runtime correction is complete: PID 75881 was verified loading the corrected
Release Web/Infrastructure DLLs on port 5169. The final authenticated queue reread was
not performed: the sandbox refused curl and automatic approval review rejected the
escalated recheck because the user had waived preflight. Do not retry it indirectly.
The queue response on this running instance remains unverified for the user's own
walkthrough; the corrected recipient query has passing PostgreSQL/HTTP evidence.
No worker is active. Next action is user acceptance at `http://127.0.0.1:5169` using
MANUAL_TEST_CHECKLIST DA-01..09; do not resume ordinary preflight. Evidence storage is
`/private/tmp/bingo-drop-announcements-evidence-20260912`; keep the isolated runtime
and database available. Follow DELIVERY_PLAN 4.2.1 when resuming.
No commit, push, merge or deployment authorized.


## Active co-captain request — 2026-09-10

For this task use `/private/tmp/BingoWebpage-admin-co-captain-20260910` on
`codex/admin-co-captain`, isolated from committed Admin base `86e7dc3`. The older
checkout instructions below are historical for this task. Preserve the original
recovery checkout and its unapproved C05/C09/sweep work; do not import it here.
Approved scope, model assignments and boundaries are in DELIVERY_PLAN under Co-captain
signup request. Sol Medium manager task `01a08c77-c46c-7502-82d9-9ce9aecf6fd8`
completed implementation, review and the user-requested manual-review corrections.

- Implemented the permanent optional Co-captain system question for new and existing
  signup forms through the existing question/answer model. The additive, collision-safe
  migration preserves custom questions, historical answers and blank existing responses.
  Public, owner and Admin flows hide/disable the input unless Captain volunteer is
  selected; server enforcement ignores forged unchecked values and clears saved answers.
- Admin Participants shows sortable Captain volunteer before sortable Team, with Yes/No
  and the co-captain name. The redundant Live-only Manage live participant link was
  removed; the pencil remains. Public/non-owner projections remain private.
- Manual-review refinements: single-line co-captain controls; Captain and Co-captain render
  together before existing custom text questions without rewriting stored positions;
  empty co-captain validation space collapses while real errors remain visible; signup-
  question delete confirmation uses the full row; Review queue UTC secondary text uses
  the normal Admin table font.
- Fresh Astra Medium review found two P2 defects (Admin header/cell order and migration
  custom-key collision); focused remediation and reviewer recheck cleared both. Initial
  Web/Integration/Domain builds passed with zero warnings/errors; focused co-captain,
  migration/default, privacy/Admin and domain tests passed. Later Web Release build and
  repeated `git diff --check` passed. Bundled Node is available at
  `/Users/christopher/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/bin/node`;
  prior changed browser-JavaScript checks were simply not executed.
- After the user's full .NET run reported four failures, a bounded test-only remediation
  changed exactly `BoardEditingUiTests.cs`, `AccountsUiTests.cs`,
  `EventCreationUiTests.cs` and `Slice2MigrationRehearsalTests.cs`. The three stale
  BrowserTests contracts now assert current route/dialog/safety behavior, and the
  migration rehearsal asserts all three defaults plus blank retained co-captain answers
  and deterministic append-after-history behavior. Each formerly failing test passed
  its exact isolated filter; scoped format/diff checks passed; fresh Astra Medium review
  found no blocker. No production file changed in that remediation. The user judged a
  second full-suite run redundant because their run had proved the remainder, so the
  verifier was stopped before migration or tests began; its disposable container and
  temporary storage were removed.
- The user completed the current manual-review corrections, confirmed the final spacing
  result and on 2026-09-10 explicitly authorized packaging the complete accepted current
  work, committing it, pushing `codex/admin-co-captain` and opening one pull request to
  `main`. This supersedes the earlier packaging prohibition for this isolated branch.
- Nothing was staged or committed before that authorization; HEAD remains `86e7dc3` at
  handoff to the packager. Package only the accepted isolated checkout inventory and the
  proportional authorities/migration/tests. Exclude recovery-checkout C05/C09, sweep/T05
  and untracked tickets. No merge or deployment is authorized.

Next permitted action: one Luna Max packager performs bounded inventory/format/leak/diff
checks, commits the exact accepted work, pushes `codex/admin-co-captain` and opens one
ready-for-review PR to `main`, then stops without merge or deployment.


**Active handoff:** 2026-09-09. Authoritative checkout:
`/private/tmp/BingoWebpage-admin-popup-recovery-20260908`, branch
`codex/admin-consistency`; use Git status/log for current commit state. Do not use the
Documents checkout or damaged old event-functionality checkout.
New planners follow [DELIVERY_PLAN section 4.2.1](DELIVERY_PLAN.md#421-lean-execution-and-planner-handoff)
and resume the recorded next action without repeating completed investigation/checks.

## Accepted Admin batch — local packaging 2026-09-09

- User authorizes packaging/local commit of accepted Admin popup and Review work;
  artwork excluded, Finalize stays deferred. No push, merge or deployment authorized.
- Inventory: 53 text files (47 modified/6 new): eight documentation files,23 backend/
  Razor/shell/localization/CSS files,eight JavaScript files and14 focused test files.
  No artwork/binary/config assets in the changes; ignored catalogue image cache and
  build artifacts remain excluded. No changes from other checkouts imported.
- Packaging checks: all10 changed Node test files passed; all12 changed C# files pass
  scoped whitespace formatting; diff check and bounded added-content credential scan
  clear. Existing accepted build/HTTP/backend review evidence retained below.
- Full-solution format verification could not finish: missing xUnit Fact/Theory references
  in untouched Application/Domain test projects. This was not repaired or represented
  as passing; scoped changed-file formatting passed. No full-suite/release claim.
- Package only the explicit53-file inventory. Keep all artwork out, preserve Finalize,
  and stop after the authorized local commit. Future publication needs user direction.

## Admin Review details — manually approved 2026-09-09

- User approves final follow-up: pre-trial content-driven sizing, equal-height contained
  evidence, compact gap, and Approve text/border `#78B86A` with unfilled background.
  Open rejection hides approval copy/normal row, leaving reason/hint/Reject/Cancel;
  Cancel restores content. Scoped CSS includes initially expanded server-error state.
  Source/diff checks passed; final visual acceptance supplied by user.
- User approves Review Details after the named corrections. Review queue also approved;
  UI_PAGE_MATRIX owns exact approval. DELIVERY_PLAN sections15–16 complete. Finalize deferred.
- Accepted Details: image-first workspace; desktop evidence card matches combined facts/
  actions height with contained image in the three-row header/image/footer layout; natural
  mobile stack; Admin fonts/tokens; all fact values including UTC match heading colour;
  muted labels; full-width action copy/rejection panel and compact Cancel recovery.
  Status sits right of tile heading. Approve uses the page-approved leaf green
  `#78B86A` for text/border. Claimed/Contribution display and
  duplicate copy removed; both submission times retained. Event/sidebar/filter-preserving
  Back and action redirects, review rules/version/authorization/history/viewer protected.
- Evidence: Astra Low backend source review clear; controlled PostgreSQL rendered queue/
  Details/rejection/redirect/Back journey plus event/filter/direct/mismatched/hidden/denied
  boundaries passed; focused binding tests2/2, Node filter checks and Release builds passed.
  Later small CSS changes used source and scoped diff checks, then user visual acceptance.
  Earlier isolated layout fixtures missed actual resize/cascade defects and are not proof
  of final geometry. User stopped browser/fixture work and excess coordination; keep future
  corrections code-focused with only checks detecting their actual risk. Safari inspection
  was unavailable because Computer Use permission was not granted; do not repeat it.
- No active workers or unresolved findings. Next permitted action: agree next user task;
  approval does not authorize another family, audit, packaging or publication.
- Before snapshots remain under `/private/tmp/review-details-before-20260909/`,
  `/private/tmp/review-details-journey-before-20260909/`,
  `/private/tmp/review-details-visual-before-20260909/`,
  `/private/tmp/review-details-geometry-before-20260909/`.
- Preserve all dirty work in authoritative checkout above. No stage/commit/push/deploy,
  user DB mutation or restart of HTTPS7131 authorized/performed.

## Admin Review queue — manually approved 2026-09-09

- User approves the Review queue after the named visual corrections. UI_PAGE_MATRIX
  owns exact approval scope; Review details and Finalize remain deferred.
- DELIVERY_PLAN section 15 complete: selected-event context across lifecycle states,
  live combined team/player/tile search plus Status, Pending-first/newest-within-group
  ordering, no masthead/Administration button, established Admin font/neutral tokens,
  right-aligned Status and table weight 400. Preserve accepted behavior/composition.
- Verification: Release Web build (0 warnings/errors), focused queue-binding test,
  authenticated HTTP event isolation/lifecycle/hidden-context and mixed-row ordering
  tests, Node live-filter test, XML and scoped CSS/cascade/diff checks passed.
  Astra Low backend/functionality review clear; visual acceptance supplied by user.
  No full-suite result claimed; no independent visual review was requested/performed.
- No active workers or unresolved findings. Next permitted action: agree the next
  user task; no further page family, audit or implementation authorized by approval.
- Before snapshots: `/private/tmp/review-queue-before-20260909/`,
  `/private/tmp/review-visual-before-20260909/`, `/private/tmp/review-pending-before-20260909/`,
  `/private/tmp/review-table-weight-before-20260909/`.
- Task exception: inspector/reviewer Astra Low; visual implementer/remediator Luna Max.
  Authoritative checkout/branch above; all changes remain uncommitted. No packaging,
  stage/commit/push, deployment, user DB mutation or HTTPS 7131 app restart authorized.

## Overview lifecycle confirmations — approved; work paused 2026-09-08

- User manually approves the lifecycle corrections. User reports approximately 50%
  of weekly usage spent today, mainly on popup work. Stop further execution; discuss
  reducing actual workflow steps before any new dispatch. Agreed 2026-09-09:
  pinpoint observed problems, expected results, protected parts and focused checks
  before implementation; if unknown, one bounded inspection produces the fix list.
  Durable workflow updated in DELIVERY_PLAN section 4.2.1; AGENTS already links it.
- Six authorized findings corrected: Close skips opening-only acknowledgements;
  rejected actions retain confirmation/typed fields; explicit refreshed-version
  retry notice preserves concurrency; pending Cancel/Escape blocked using existing
  transport state; all branches reveal/focus; Cancel-first order and return-to-
  trigger fragments; explicit signup success/error/information severity. EN/DA
  retry copy present. Existing layout, domain/services/rules/routes unchanged.
- Astra High source review and three named correction rechecks clear. Web/Razor
  build passes (0 warnings/errors); final resource-only entry passes XML/diff/key
  checks. Scoped Node guard/syntax and Participants UI/Manage/roster checks pass.
- Focused EventCreationUiTests: 26 pass, 1 unrelated stale Participant source
  assertion at line269 expects initializeOwnerAccountPicker(content); missing in
  both before/current script. No extra Participant remediation authorized.
  Browser test restore succeeded; test-runner socket needed approved escalation.
- Verification limit: no browser rendering, real lifecycle mutation or database
  handler fixture executed. Changed handlers are compiled/source-reviewed; source
  tests do not prove persisted lifecycle behavior. User app/data left untouched.
- Cumulative patch `/private/tmp/lifecycle-confirmation-fix.patch`; before snapshots
  `/private/tmp/lifecycle-confirmation-fix-before/`. Durable patch/current files,
  executable guard check, source verdict and results in task visualization
  `lifecycle-confirmation-pass/`. All work uncommitted; no active workers.
- Identity confirmation manually approved 2026-09-09. Next action is owned by the
  fresh planner handoff above; Review/Finalize remain deferred.

## Event-settings confirmations — Identity approved 2026-09-09

- Schedule change/warning review and Super Admin ownership-transfer confirmation
  manually approved; matrix records exact scope.
- Astra High read-only Overview lifecycle review found: close-signup warning dead
  end, failed-input loss, pending Cancel links, missing reveal/keyboard handling,
  destructive button order and misleading signup severity. No lifecycle code edited
  or runtime/manual proof claimed. Remediation now authorized above; full source report
  in task visualization `event-settings-confirmations/lifecycle-source-review.md`.
- Identity timezone confirmation fixed with existing data-confirmation-box marker,
  event-manage.js include and scoped compact paragraph/title CSS. Source review
  clears; targeted Node execution confirms focus/scroll/bind-once, wiring/cascade
  and diff checks pass. No new JS implementation, browser/build/DB/app restart.
- Identity patch `/private/tmp/identity-confirmation-fix.patch`; durable patch,
  changed files and results in visualization `event-settings-confirmations/`.
  No active workers. A possible new-script interaction was ruled unreachable from
  Identity; no unrelated correction made.
- Lifecycle remediation is completed above; Identity confirmation manually approved
  2026-09-09.
  Review/Finalize remain deferred; no broader rollout, packaging,
  commit, push or deployment authorized.

## Participant confirmation visibility — approved 2026-09-08

- Catalogue Add/Edit and Participant Manage manually approved; matrix updated.
  Participant approval includes Remove, Restore and Transfer ownership reveal.
- Existing shared Participant toggle handler now scrolls the opened confirmation
  into view before focusing Cancel with preventScroll. Layout, typed state,
  Cancel/Escape, guards and backend behavior unchanged.
- Focused Participant regression covers all three reveal/Cancel paths and passes;
  syntax/diff checks pass. Existing test mock now implements removeEventListener,
  resolving the previously recorded null restore-confirmation fixture failure.
  Astra High focused source review clears. No browser, build, DB or app restart.
- Evidence `/private/tmp/participant-confirmation-reveal.patch` and before source
  snapshots; durable patch/current changed files/results in this task visualization
  `participant-confirmation-reveal/`. No active workers.
- Stop until the user authorizes the next work. Review/Finalize remain deferred;
  no other rollout, packaging, commit or push authorized.

## Bundled popup walkthrough — corrections approved 2026-09-08

- User approves inspected Board tile details/removal, toolbar popovers/confirmations,
  and Teams/Draft roster/member/move/draft confirmation surfaces. Matrix owns scope.
- Fixed false dirty prompts: shared guard ignores unstable timestamps on empty-name,
  zero-size upload placeholders; real file metadata, including named zero-byte files,
  remains tracked. Focused Board/roster tests now model changing empty-file timestamps.
- Page-level Remove team SVG now uses the existing compact roster dimensions;
  button hit target, focus, padding and accepted layouts remain unchanged.
  User manually approves both corrections, including clean roster/tile closure.
- Eight existing consumer checks pass: Accounts, Board, Catalogue, Add team, roster,
  Add participant, Participants UI, Signup questions. Participant Manage test fails
  at line 245 (null restore-confirmation fixture); same failure reproduced with the
  before-change guard, so it is recorded separately and not remediated in this pass.
  Astra High focused source review clears; no browser, build, DB or app restart.
- Incremental patch `/private/tmp/popup-clean-close-fix.patch`; before snapshots
  `/private/tmp/popup-clean-close-fix-before/`; durable correction files/evidence in
  this task's visualization `popup-clean-close-fix/`. No active workers.
- Review and Finalize need general page overhauls; user explicitly tables both.
  Subsequent Catalogue approval and Participant visibility correction are recorded
  in the active entry above. No overhaul, packaging, commit, push or deploy authorized.

## Repository instruction cleanup — complete 2026-09-08

- User-approved concise AGENTS guide is active: assignment scope, roles/model table,
  repository/data protections, bounded execution, verification and authority links.
  Approved model defaults preserved; Board-only Astra Low completion is not a global
  UI model change. No pass history or dated model exceptions in the startup guide.
- Unique planning/readiness, change-control, evidence/review, slice-preflight,
  completion and branch procedures now live in DELIVERY_PLAN sections 4.1–4.7.
  UI protocol remains in UI_SYSTEM; commands remain in README. Only relevant
  subsections are required for a task. This does not waive delivery gates.
- Section 4.2.1 now records lean dispatch, early worker uncertainty escalation to
  the calling planner, progress intervention, the brief template and planner
  handoff. AGENTS links it and keeps only the universal escalation rule.
  Current worker ownership and next action are recorded in the active entry above.
- Documentation-only change; no application edits, test/build reruns, app restart,
  commit or rollout. Before files and scoped change evidence are in this task's
  visualization `agents-rework/`. Board approval is recorded below.

## Board Create/Edit tile popup — approved 2026-09-08

- User's manual finding: discard confirmation text was oversized. Typography-only
  scoped CSS correction now uses compact body/supporting text and an emphasized
  body title; buttons, spacing and behavior unchanged. Focused cascade/source
  recheck and diff check pass; patch `/private/tmp/board-confirmation-type.patch`.
  User manually approves the corrected popup; no repeated build or behavior checks.
- Accepted composition/flow preserved. DELIVERY_PLAN section 14 behavior pass is
  implemented: dirty/dynamic/file guards, pending/duplicate protection, retained
  failed inputs and guarded stale/unknown recovery, explicit committed-with-warning
  outcome, fresh full Board response/context and one truthful toast; compact local
  objective/tile confirmations, narrow fullscreen and collaboration protection.
- Browser Back/reload/leave uses native unsaved-change protection, not a new popup
  history system. New-tab Catalogue links retain their original behavior. No
  backend rules, schema, route, service or shared-guard rewrite.
- Luna Max close and CSS/collaboration changes retained. User-approved Astra Low
  completed remaining work and five named fixes: new-tab/modifier navigation,
  clean completed-save recovery, genuine backdrop gesture, reused failure controls,
  and truthful stale wording. Astra High source-only review/recheck clears all.
- Focused Board Node tests with real shared guard and executable failure/recovery
  checks pass; syntax/diff checks pass. Final Razor build passed (0 warnings/errors)
  before JS-only review fixes; no redundant rebuild. No browser or persisted
  mutation/concurrency test claimed. User manual acceptance is now recorded in the matrix.
- User app on HTTPS7131 left running. No agent DB mutation/reset, app restart,
  commit or deploy.
- Combined pass patch `/private/tmp/board-popup-final.patch`, original baseline
  `/private/tmp/board-pass-baseline`; durable evidence and complete modified-file
  backup in this task visualization `board-popup-pass/`. Incomplete prior attempts
  preserved outside checkout and restored before final work. AGENTS records the
  accepted lean workflow; the Board-only model exception remains in this handoff
  and DELIVERY_PLAN section 14. All work uncommitted.
- Stop until the user authorizes another pass; no packaging/push/deployment. Roster
  guard and other page approval states remain unchanged.

## Teams/Draft roster guards — ready for manual check 2026-09-08

- User-approved roster composition, role-at-add/readiness corrections, compact
  removal glyph/button and small positioned removal confirmation are preserved.
  The initially missed outside-click and dirty/pending paths are now implemented;
  DELIVERY_PLAN section 13 guard completion is the final scope authority.
- Shared guards cover dirty X/Escape/outside/Back/application navigation, switching
  editors and cross-form submission; Keep retains edits/focus, Discard resets them.
  Pending blocks duplicates/dismissal; failed saves retain inputs and dirty baseline.
  File-only image/CSV edits are detected. Native modal guards work across widths.
- Mutations require explicit success evidence; roster and sibling participant data
  refresh with view/scroll state. Shared toast host stays connected and visible;
  completed-save/failed-refresh recovery avoids false save failure or resubmission.
- Luna Max implementation/remediation complete. Astra High source-only review clears
  all five named findings and final file-only dirty-state question. Focused roster
  test, five affected guard consumers, syntax/diff checks and Razor build pass
  (0 warnings/errors). No browser harness, application DB work, app restart, commit,
  push or deployment. New guard states await user check; prior visual approval remains.
- Evidence `/private/tmp/roster-guards.patch`, `/private/tmp/roster-guard-fixes.patch`
  and their before snapshots; durable patches, verification and complete current
  modified-file backup in this task visualization `roster-guards/`. Earlier role/
  visual/backdrop evidence remains in sibling `roster-corrections/`.
- User subsequently authorized Board Create/Edit tile; its current implementation
  handoff is above. This does not broaden roster guard manual acceptance.

## Teams/Draft Add team popup — approved 2026-09-08

- DELIVERY_PLAN section 12 complete with overall composition preserved. Add-only
  shared dirty/pending guards, actual discard/reset, retained failed inputs,
  finalized inline confirmation visibility/order/focus/keyboard behavior and
  resize scroll lock implemented. Blank-name error is now accurate in EN/DA;
  backend validation/lifecycle/transaction rules and roster dialogs are unchanged.
- Corrected initial source findings: success notice carried through existing
  pending-toast owner before real reload; errors use shared native-modal toast
  opt-in; misleading test mock removed. Astra High source-only review clears all
  named fixes; Luna Max handled implementation/remediation and focused execution.
- Add team test, directly affected Participant checks, syntax/diff and Razor/
  resource build pass (0 warnings/errors). No reviewer visual inspection or browser
  harness; no real team creation/database writes, app restart or full-suite rerun.
  User now approves Add team, accepting minor behavior differences from the other
  popups without further remediation. This does not change the general popup rules.
- Evidence `/private/tmp/add-team-popup-pass.patch`, `/private/tmp/add-team-fixes.patch`;
  durable patches/working-file backup in current task visualization
  `add-team-popup-pass/`. Same recovery checkout/branch, all changes uncommitted.
  Stop before roster or other popup rollout, packaging, commit, push or deployment;
  the next pass requires user authorization.

## Add Participant popup — approved 2026-09-08

- Admin -> event -> Participants -> Add participant (DELIVERY_PLAN section 11)
  now uses shared guards and the same modal at all widths, fullscreen <=900.
  Dirty/pending protection, load retry, retained failure inputs and truthful
  creation feedback preserve the existing form and backend rules.
- User approves visuals; Astra High source-only correctness review clears after
  Luna Max fixes current filter/sort context and successful-creation/failed-refresh
  recovery. Completed creation cannot be resubmitted; Close/Escape/Back really
  reloads Participants without a discard prompt when refresh fails.
- Add/Participants/Participant Manage Node regressions, syntax and diff checks
  pass. Razor build passed with 0 warnings/errors before JS-only corrections.
  Client responses mocked; no browser harness, persisted creation test, production
  authentication proof, app restart or database mutation by the agent.
- Patches `/private/tmp/participant-add-popup-pass.patch` and
  `/private/tmp/participant-add-fixes.patch`; durable patches/working-file backup
  in the current task visualization directory `participant-add-popup-pass/`.
  Same recovery checkout/branch, all changes uncommitted. Stop before another
  popup family or packaging. Participant Manage/Catalogue approvals unchanged;
  Teams/Draft was not part of this pass.
- Worker mistakenly sent its completion to old pinned Planner Orchestrator v7.
  That triggered turn is interrupted/idle with no recorded turn items. AGENTS now
  explicitly requires parent-only subagent reporting; no further cross-task work.

## Current-popup confirmation trigger correction — manually approved 2026-09-08

- User authorizes hiding the initiating button while its inline action confirmation
  is open across Questions, Participant Manage, Accounts Create/Manage and Catalogue
  Add/Edit. Restore the trigger before Cancel/Escape focus; preserve values, discard
  recovery, pending safeguards and layouts. General rule recorded in UI_SYSTEM;
  bounded correction scope recorded at the end of DELIVERY_PLAN section 10.
- Correction complete: scoped CSS hides open native confirmation summaries;
  Accounts and Catalogue dynamic triggers hide/restore through existing adapters.
  Focus restoration, typed values and discard recovery remain protected.
  Accounts/Catalogue/Participant/Questions Node regressions and diff check pass.
  Astra High source-only correction review clears; no browser/build/database
  actions or app restart. User manually approves the trigger-hiding correction.
  Other page approval states remain unchanged.
  Exact correction evidence: `/private/tmp/admin-confirmation-trigger-correction.patch`;
  pre-correction copies: `/private/tmp/admin-confirmation-trigger-before/`.
- User updates reported popup usage to roughly 15% of weekly budget. Agree a
  substantially leaner workflow before any further family rollout; that discussion
  is now active. User chooses Luna Max for routine UI implementation/corrections;
  AGENTS records this override. User agrees independent review stays read-only and
  source-focused on behavior/correctness, with no reviewer visual inspection;
  implementer owns focused execution and user owns visual acceptance. Existing
  correctness review gates remain; broader workflow reductions are not yet agreed.
  No packaging, commit, push or deployment authorized.

## Catalogue Add/Edit — approved 2026-09-08

- DELIVERY_PLAN section 10 implemented with existing activity/drop composition
  preserved. Shared dirty/pending/discard guards, one all-width modal, inline
  confirmation visibility/focus and parent freshness now apply to Add/Edit.
- Success/error/warning toasts use the shared owner and truthful outcome text;
  failures retain values. Stale/missing records explicitly require guarded reload;
  deleted activities return to the catalogue. Standalone completed deletion carries
  one success toast without an unsaved-change prompt. Typed DELETE, duplicate-item
  choices, rate parsing and all backend/version/permission protections unchanged.
- Source-only independent review clears. Focused Catalogue normal/direct-failure
  Node regressions and diff check pass; compiled Razor/resources build passed with
  0 warnings/errors. Final small JS corrections have focused regression coverage.
  Root Chrome checked dirty Close/failed input retention, pending resize/Escape,
  native modal after parent refresh, one visible success toast, confirmation reveal/
  Escape, refreshed Edit -> Add handoff and standalone Edit. Two POST responses were
  simulated, zero actual catalogue writes or JS errors. Backend deletion/concurrency
  mutations were not executed; their handlers remain unchanged and source-reviewed.
- Evidence `/private/tmp/catalogue-popup-pass/results.json` and `checks.cjs`, copied
  to durable task visualization `catalogue-popup-pass/` with working-source backup.
- User manually approves Add/Edit 2026-09-08. Earlier budget/workflow discussion is
  superseded by the accepted lean procedure in DELIVERY_PLAN section 4.2.1.
  No additional Catalogue work or packaging authorized.
- Same authoritative checkout `/private/tmp/BingoWebpage-admin-popup-recovery-20260908`,
  branch `codex/admin-consistency`; accepted prior-family changes preserved.

## Accounts Create/Manage — manually approved 2026-09-08

- User approves both Create and Manage after the confirmation auto-scroll correction.
- DELIVERY_PLAN section 9 behavior rollout implemented; existing composition
  preserved. Accounts now uses the shared editor guard, compact inline action
  confirmations and one modal across widths. Required reasons/one-time link
  disclosure and all PageModel/backend permissions/handlers remain unchanged.
- Manual visibility correction: opening Disable/Generate link brings the revealed
  inline confirmation into view with native nearest scrolling; Cancel focus is
  preserved. No layout, save or permission changes.
- Dirty/cross-form/scope-change, pending/duplicate/dismissal and failed-save
  retention protections added. Manage refresh preserves native modal connection,
  current link response, parent filters/page/scroll. Create returns to refreshed
  directory. Failed refresh after a save reports truthful reload recovery.
- Source-only independent review clears with no open findings. Named fixes: standalone actions/reason binding,
  connected dialog during parent refresh, restoring typed reason after discard
  Cancel, and standalone Create scope GET protection. These are corrected. Current
  selector also reflects authoritative loaded event after scope changes.
- Accounts/Participant/Questions focused Node checks and diff check pass. Root
  compiled Razor/resources/preview with 0 warnings/errors. Chrome passes 11 focused
  groups covering inline reason/failed save, pending resize/Escape, directory refresh,
  dirty Create, emergency/reload handoff, reason cancellation, native modal/link
  retention, Create scope/success, and standalone actions/scope cancellation.
  Four POST responses were simulated; zero actual account writes or JS errors.
  No real password/reset link was generated (one-time response used synthetic text).
  Test Admin preview disables background workers; persisted security mutations and
  production authentication were not re-tested, and the full suite was not rerun.
- Evidence `/private/tmp/accounts-popup-pass/{results,followup-results,scope-results}.json`;
  durable results/scripts plus working-file backup under the task visualization
  directory `accounts-popup-pass/`. Independent reviewer did not use a browser.
- Authoritative checkout remains `/private/tmp/BingoWebpage-admin-popup-recovery-20260908`,
  branch `codex/admin-consistency`; prior accepted changes preserved, no commit/push.
  Next action: agree the next bounded Admin family or packaging step; neither is
  authorized yet. AGENTS records the agreed source-only rollout policy.

## Participant-management popup — approved 2026-09-08

- Participant pass and latest user refinements implemented: compact paired summary,
  answers -> notes -> actions; Save shares the last answer row when space permits;
  only action triggers form a right-aligned wrapping row. Section headings are
  consistently smaller than the popup title. No backend participant rules changed.
- Participant and accepted Questions physically share dirty/pending/discard guards.
  Both refresh affected Participants data after successful saves while preserving
  filters/sort/scroll and the open editor. Failed parent refresh explicitly reports
  that saving succeeded; Close reloads the canonical list (including #players).
  UI_SYSTEM records parent freshness as a general popup contract.
- Manual follow-up corrected: Manage -> reload -> close -> Edit signup form now
  binds the restored Questions trigger through the existing content-update event.
  Combined-script regression verifies one binding; Chrome executes that exact
  sequence and stays modal with parent sort/filter URL retained, no writes.
- Focused Participant/Questions Node tests and Razor/resource build pass (0 warnings/
  errors); independent review corrected parent freshness, refresh-failure feedback,
  stale Add-dialog listeners, section heading specificity and real reload fallback.
  Current desktop/mobile/action-confirmation and light-setting rendering reviewed.
- Chrome preview passes dirty cross-form/Back/discard, failed-note retention,
  pending resize/dismissal guard, successful parent refresh for both editors,
  explicit standalone route, and actual reload after failed parent refresh for both.
  Six POSTs were simulated across primary/fallback checks; zero persisted writes or
  browser errors. Preview uses a local test Admin and disables background workers;
  this proves client behavior, not production authentication or persisted lifecycle.
- Authoritative checkout `/private/tmp/BingoWebpage-admin-popup-recovery-20260908`,
  branch `codex/admin-consistency`; prior accepted changes remain uncommitted.
  Evidence `/private/tmp/participant-popup-pass/{results,fallback-results}.json`;
  durable evidence/source backup in the task visualization directory under
  `participant-popup-pass/`. No package, push or next page family authorized.
  Next action: user checks current Participant spacing/action flow and save/close
  on local 7131. Questions prior manual acceptance remains; freshness follow-up is
  verified and available for the same spot-check.

## Admin popup pilot — 2026-09-08

- Active branch `codex/admin-consistency` in isolated checkout
  `/private/tmp/BingoWebpage-admin-popup-recovery-20260908`, based on
  deployed main `42b2a7d`; preserve local rollout notes. User prioritizes Admin
  component/behavior reuse while preserving public UI. Read-only source discovery
  and one independent bounded behavior review complete. Pilot implemented; focused
  Node regression and compilation pass; independent source/visual review clears.
  User manually approves the complete pilot and code-settings/full-screen follow-up
  on 2026-09-08 after trying it locally (DELIVERY_PLAN section 7).
- Existing shared shell/tokens/toasts are real owners. Accounts/Catalogue/Questions
  dialog lifecycle and confirmation handling were duplicated. Questions failure
  recovery is corrected and browser-verified. The separate Catalogue focus-callback
  source finding remains outside this pilot; its rendered impact is unverified.
- User approved Signup Questions as the first popup pilot, including compact inline
  delete/discard confirmations, unsaved-edit protection, Admin typography hierarchy,
  consistent lifecycle/failure recovery; latest decision replaces narrow switching
  with the same modal at every width, full-screen at <=900.
  DELIVERY_PLAN section 7 freezes scope and proof; UI_SYSTEM owns reusable rules.
- Pilot implementation is complete and manually approved; another Admin page
  family/rollout and packaging require separate user authorization.
  Existing source review remains applicable; no additional broad readiness audit.
  Verification complete: web build 0 warnings/errors, focused Node regression and
  diff check pass. Real Chrome rendered-preview checks passed 8 scenario groups:
  unchanged focus/close, dirty Close/Back cancellation, confirmed discard after
  narrowing, transport and validation retention, pending duplicate/dismissal guard,
  inline deletion/Escape, and narrow-route failure/retry. Five question POSTs were
  intercepted; no persisted question/answer writes were exercised. Preview used an
  Admin test identity and disabled background workers, not production authentication
  proof. Direct Admin -> Events -> editable event -> Participants -> Signup form
  navigation was executed against current compiled Razor.
  Two independent findings (dirty narrow close and standalone save recovery) were
  corrected and cleared by fixes-only review. Desktop/narrow/current theme-setting
  screenshots clear visual review; user manual acceptance is now recorded.
  No commit, push, shared rollout or other page-family work performed.
- Environment recovery: around local midnight, `.git` and project files disappeared
  from `/private/tmp/BingoWebpage-admin-event-functionality` during verification.
  Cause unverified; no agent deletion. Preserved four changed docs and six source/test
  files in a persistent recovery archive, cloned the exact base `42b2a7d` into the
  new checkout above, restored those files, and verified the expected 10-file diff
  with `git diff --check`. The old checkout and saved project remain untouched.
  Archive: `/Users/christopher/.codex/visualizations/2026/09/07/01a07c97-2f5a-7352-8c13-184837121f0b/admin-popup-recovery-20260908.tar.gz`.
- Current evidence: `/private/tmp/questions-popup-pilot/results.json` and
  `checks.cjs`; durable screenshot/results copies under
  `/Users/christopher/.codex/visualizations/2026/09/07/01a07c97-2f5a-7352-8c13-184837121f0b/admin-popup-pilot/`.
  Follow-up verification: Node regression, web build (0 warnings/errors), diff check
  and independent scoped source/visual review pass. Chrome passed six changed-flow
  groups: mobile modal entry/reload, clean/dirty/pending resize, code visibility and
  required state without autosave, failed-code retention, modeled existing-code
  blank replacement, mobile Close/Back/discard, and explicit standalone route.
  Three question POSTs were simulated; stored-code persistence was not re-exercised
  and its existing handler semantics remain unchanged. Settled mobile scroll evidence
  confirms title/Close stay visible. No further findings.
  Evidence: `/private/tmp/questions-popup-pilot/followup-results.json`, `followup.cjs`,
  and screenshots copied to the durable evidence directory above.
  Next permitted action: agree the next bounded Admin popup rollout, using this
  accepted behavior as the target. Participant rollout is now authorized above;
  commit/push remain unauthorized.

## PR #7 production rollout — complete 2026-09-07

- User authorized merge and deployment after green local/PR CI. PR #7 merged
  normally as `42b2a7dac0f5fd56e627359cba989a5c63c383de`; feature branch retained.
- Exact main CI run `34160464984` passed all jobs and published the verified
  `linux/amd64` candidate for that source SHA. Production image:
  `ghcr.io/swiftpenguin578/bingowebpage@sha256:b2450a791eb22efdce61d185dd6f630810a1aab170a797916db293f99b4739f7`.
- Authorized production workflow `34161651143` completed successfully, including
  candidate validation and the host backup/migration/preflight/replacement/health
  procedure. A success receipt was written on the host; its log path is partially
  redacted by GitHub. Receipt contents were not separately fetched over SSH.
- Post-deployment read-only smoke passed: `/health/live`, `/`, `/Account/Login`
  and Danish `/HowTo` all returned HTTP 200. Login contains its Discord entry;
  HowTo serves the new Danish wording including the corrected Regular-account
  meaning. No live signup/participant test records were created.
- Local evidence: `/private/tmp/pr7-production-candidate/candidate.json`,
  `/private/tmp/pr7-production-smoke.json`, `/private/tmp/pr7-production-deployment.log`.
  CI: https://github.com/swiftpenguin578/BingoWebpage/actions/runs/34160464984.
  Deploy: https://github.com/swiftpenguin578/BingoWebpage/actions/runs/34161651143.
  This release handoff update is local documentation only and uncommitted;
  earlier unmerged/unpushed/pending-test notes below are historical checkpoints.

## PR #7 CI formatting correction — 2026-09-07

- User reports the final local full suite all green. Commits `f04432a` and
  `619eeb3` were pushed to `origin/admin-event-functionality`; PR #7 is open
  into main: https://github.com/swiftpenguin578/BingoWebpage/pull/7.
- CI reports only Program.cs import ordering from `dotnet format Bingo.slnx
  --no-restore --verify-no-changes`. Earlier scoped whitespace-only checks did
  not cover imports. Bounded correction reorders imports only, then runs the
  exact complete formatting command before committing/pushing the correction.
  Program.cs import ordering is corrected; the exact complete formatting command
  passed (exit 0), and diff checks pass. No behavioral change or test-suite rerun
  required. This correction is authorized for commit/push to PR #7; its new CI
  result is pending.
  Earlier pending-test/unpushed notes below are historical checkpoints.

## Danish HowTo copy correction — 2026-09-07

- User requests natural Danish wording throughout `/HowTo` before pushing.
  Scope: Danish resource values consumed by the existing five-step guide,
  including headings/help/accessibility wording where useful. Preserve English,
  page composition/navigation, established OSRS glossary, exact action labels,
  and signup/evidence/privacy/review rules. No UI redesign or behavior change.
  Rewrite complete: 42 existing Danish values updated, including player-focused
  prose and references matching actual Danish controls/statuses. XML/key/placeholder/
  consumer and diff checks pass; all changed keys are specific to HowTo. Independent
  Astra High wording/meaning review clears the final text after preserving optional
  Regular accounts and general tile requirements in simplified wording. No build,
  test-suite or layout change. User accepts the wording and authorizes local
  packaging/commit. This copy commit contains the accepted resource correction
  and handoff only. User is rerunning the full suite before any push; its result
  is pending. No push/deploy authorized in this step.
  Signup corrections are committed locally as `f04432a`; no push/deploy performed.

## Signup readiness corrections — approved 2026-09-07

- Local packaging/commit authorized and gates clear; push/merge/deploy remain
  unauthorized. User's full Release suite reported 755 total: 754 passed and one
  failed because the direct ConfirmationModel test supplied a null localizer.
  That test setup now uses the existing passthrough-localizer pattern; production
  behavior and assertions are unchanged. The previously failing test passes 1/1
  in a focused Release rerun; scoped formatting and diff checks pass. The entire
  suite was not repeated after this isolated fixture fix. This supersedes earlier
  source-test environment blockers for the user's completed full run.
  Accepted packaging scope is the pending onboarding/signup corrections,
  question-deletion migration, regression tests, authority updates and approved
  AGENTS workflow rewrite. Explicit 29-file inventory and added-content secret/
  private-key/path checks are clean. This local packaging commit contains the
  accepted state; no push, merge, deployment or user-database mutation is included.
  Timestamp review found no unsafe raw-clock-to-reloaded equality: the deletion
  test normalizes to microseconds; other comparisons use DB reads or the same
  tracked value. No timing fix needed. Earlier per-pass uncommitted/no-packaging
  notes below are historical checkpoints superseded by this authorization.
- Manual smoke check accepted by user, 2026-09-07: user reports the recommended
  question-deletion views, withdraw -> delete -> reopen -> rejoin sequence,
  conflicting-account feedback and real Discord cancellation/retry destination
  all work correctly. This records user-reported acceptance of these journeys,
  not whole-site approval or independently observed browser evidence. Remaining
  implemented recovery cases retain their passing focused-test evidence; no
  repeat manual walkthrough requested. Changes remain uncommitted; no push or
  deployment is authorized by this acceptance.
- User authorized findings 4/5 correction, 2026-09-07. Identify conflicting
  submitted account answers using existing field validation while preserving
  other entered values and atomic rejection; do not expose another participant's
  private data. Preserve validated local event destinations through Discord
  failure/retry and mandatory password-change recovery, with safe fallbacks for
  absent/invalid state and unchanged account-linking/session/security semantics.
  No new schema/routes/services, auth bypass, visual redesign or unrelated audit.
  Existing gap review is readiness evidence. One Astra Medium implementer handles
  signup then authentication corrections sequentially. Focused
  regression checks and one independent correction-diff review only. Baseline:
  `/private/tmp/signup-gap45-before`. No commit/push/deploy authorized.
  Findings 4/5 complete. Conflict create/edit checks pass 2/2. Independent review
  caught missing failure properties on provider exceptions; the new exception
  regression failed before correction and passes after protected-state recovery.
  Affected auth checks pass 3/3 (exception/cancellation recovery and unsafe-state
  fallbacks), including forced-password invalid/retry to the intended event.
  Release compilation, scoped formatting and diff checks pass. Independent review
  clears both fixes and the exception-path correction. No whole-suite/browser
  matrix run or live OAuth change. Database-race conflict fallback is source-reviewed,
  not separately race-tested. Diff: `/private/tmp/signup-gap45-implementation.diff`.
  Changes remain uncommitted and undeployed; question deletion is completed below.
- Approved question deletion correction, 2026-09-07: Delete removes a custom
  question from form, table, confirmation and ordinary question/answer views,
  including never-answered questions, and deletes all its answers. User explicitly
  confirms optional Regular/Alt assignments must also be released, freeing event
  reservations; rejoin/Admin restore must not resurrect them. Preserve My accounts
  links, participant status/order, required system questions, audit/competitive
  history, and existing private/closed pre-draft gates. No Hide feature. Structural
  replacement remains a distinct history-preserving operation. Product, functional
  and data authorities updated before implementation. Read-only bounded readiness
  checks deletion/replacement/history and transaction seams; implement smallest
  correction with focused deletion/reservation/restoration regression coverage,
  scoped formatting/diff checks and one independent correction review. Readiness
  cleared with a deletion tombstone (retaining definition/assignment history),
  event-lock lifecycle recheck, physical answer removal, versioning and audit in
  one transaction. Astra Medium implementation complete. The data-only upgrade will
  repair old null-reason/non-replacement custom removals in Draft/SignupOpen/
  SignupClosed events before draft lock; exclude system/retained-conversion/
  legacy questions. Already draft-locked/later history is unchanged. No new
  schema/table/service/route/framework or Hide action. No broader audit, commit,
  push or deployment authorized.
  Verification: focused Release tests pass 3/3 (Regular/Alt rendered Admin deletion,
  affected views, reservation reuse, rejoin/Admin restore, audit-failure rollback,
  authority/system/lifecycle/repeat guards; populated old-row migration checks
  ten eligible/excluded classifications). Release compilation, scoped formatting
  and final diff checks pass. Independent Astra High correction review clears
  the approved delta. Data-only migration `20260907185521_RepairDeletedSignupQuestions`
  includes matching generated metadata; model snapshot/schema unchanged. Exact
  delta: `/private/tmp/signup-question-delete-implementation.diff`; test output:
  `/private/tmp/signup-question-delete-tests.log`. No live concurrent execution,
  real database mutation, application restart, commit, push or deployment performed.
  Existing old deletions are repaired when the migration runs on app update.
- User authorized one independent Astra High onboarding-to-signup gap review.
  Derive journeys from active product contracts and current implementation, then
  compare existing tests/manual evidence; neither is the completeness checklist.
  Review is read-only and bounded to entry, onboarding, event signup, confirmation,
  editing/withdrawal/rejoin and their failure/recovery/state boundaries. Follow
  with only focused checks that resolve concrete material gaps; no visual matrix,
  whole-site audit or redundant full-suite run. Review complete: removed-Alt
  rejoin resurrection reproduced by one temporary PostgreSQL diagnostic (1 failed
  as expected, diagnostic removed); five more source-confirmed gaps cover retained
  character validation retry, missing manual signup EHB editor, unidentified
  conflicting answer, lost event destination on authentication recovery, and
  incomplete confirmation answers. A signup/event-update serialization risk is
  unverified. No production remediation was performed during review. Report:
  `/private/tmp/onboarding-signup-gap-review-2026-09-07.md`. Next: prioritize these
  findings with the user; prior manual/test evidence is not blanket completeness.
- Review adjudication: user accepts My Accounts as the manual EHB fallback;
  finding 3 is closed and active product/functional wording updated. No inline
  signup EHB editor is required. For finding 6, personal My Accounts labels/notes
  are excluded; user also accepts existing Account/Alt account question/column
  wording as sufficient, closing the additional role-label finding. Alt means a
  support character alongside the main; no extra labels/badges are required.
  User explicitly approved correcting the omitted captain-volunteer answer:
  confirmation must always show the stored choice as localized Yes or No.
  Corrected in the existing confirmation projection using persisted boolean and
  existing EN/DA localization. Release Web build passed (zero warnings/errors)
  and scoped diff check passed; no browser/test rerun.
- User now authorizes remediation of gap findings 1 and 2 only. Preserve the
  saved account selection at withdrawal: an optional character cleared earlier
  must not be restored or cause a false reservation conflict on rejoin. Protect
  shared Admin restore behavior, legitimate retained assignments, queue and
  transaction/history semantics. Invalid edit postbacks must retain eligible
  registered-but-globally-unlinked options while preserving submitted values,
  validation errors and stale-version protections; ownership remains server-derived.
  Add/run only discriminating regression journeys for these fixes and directly
  affected behavior. No new schema/service/route, unrelated cleanup or UI redesign.
  Existing gap review is the readiness baseline; one bounded independent review
  of this correction delta follows implementation, not another broad gap audit.
  Pre-correction snapshots: `/private/tmp/signup-gap12-before`. Implementation
  complete in SignupService, Signup page model and existing integration tests.
  Four new regression cases failed at intended assertions before fixes; focused
  Release run now passes 6/6 including existing EN/DA selected-Alt journeys.
  Tests cover participant rejoin/Admin restore, claimed cleared Alt, fixed-time
  releases, retained assignments and real Unlink -> Edit -> invalid -> corrected
  POST. Scoped diff check passed; batch diff is
  `/private/tmp/signup-gap12-implementation.diff`. Bounded independent Astra High
  review passed with no blockers; migrated disabled-Alt fallback was source-checked,
  not separately executed. Scoped formatting passed after four test-whitespace
  corrections; final diff check passed. No repeat tests/build/review after whitespace.
  Findings 1/2 are complete. Findings 4/5 and unverified concurrency risk remain
  outside this batch; changes are uncommitted and undeployed.
- Current direct onboarding correction: match the EHB fetch-button surface to
  its input in both themes; hover changes only text/SVG to theme ink (cream in
  dark, ink in light), retaining focus and busy/disabled behavior. Blank character
  validation must not shift the neighboring inputs. Scope is the existing right
  onboarding controls and their cascade; no authentication, lookup, or signup
  behavior changes. Current user screenshots supplied 2026-09-07 are evidence.
  CSS correction complete: composite uses the input surface token, global filled
  hover excludes the existing fetch control so theme-ink text/SVG hover wins,
  and character fields align at the top to avoid validation-induced stretching.
  Source/cascade and diff checks pass. No rendered check, build or tests; user
  visual acceptance remains pending. Authentication and lookup behavior unchanged.
- Use only `/private/tmp/BingoWebpage-admin-event-functionality`, branch
  `admin-event-functionality`, base `4f58407de2ddee079b5a2a4ceba2dff612fd1a51`.
  The separate saved checkout remains excluded. Preserve the uncommitted,
  user-approved `AGENTS.md` workflow rewrite. No packaging or deployment is
  authorized for these corrections.
- User approved subagent delegation, including Public UI, on 2026-09-07.
  `AGENTS.md` now replaces the visible-worker-only rule with bounded subagents,
  explicit existing model/reasoning assignments, independent reviewer separation,
  and coordinated file ownership. Separate user-owned tasks require an explicit
  user request; the model table and existing acceptance gates remain unchanged.
- User handoff establishes PR #6 merged at
  `b53ff904f4fdba199d3d94e8174f71f32d9041d1`; the user now reports deployment.
  Older unpushed/unmerged/manual-pending statements below are historical and
  superseded by that handoff. This task has not verified the deployment receipt.
- Approved bounded scope: Alt/Informational signup works without EHB metadata;
  Regular/Playing behavior remains protected. Withdrawn confirmation offers
  Rejoin when eligible, not Edit; stale/direct edits cannot mutate or reacquire
  reservations. Prove a second user can register a character released by withdrawal
  and the first user's later Rejoin respects that real reservation.
- Confirmation copy/composition: neutral “Your signup” kicker, event name as
  the main heading, one prominent status with Waiting-list position shown once,
  no repeated small status badge or redundant confirmation sentence. English
  withdrawn status reads “Withdrawn”. Step 03 explains eligible Rejoin without
  promising the old place, Admin contact after close/before draft, and unavailable
  self-service after draft. Preserve EN/DA, themes, narrow layout, accessibility,
  existing routes, permissions, queue rules, and history. No other page redesign.
- Additional approved masthead correction: the existing Signup create/edit and
  Signups table metadata row shows grouped bingo start/end, signup close, and
  draft start only when configured, with HH:mm in the event timezone. Remove
  metadata capacity while retaining the large x/x. Preserve natural narrow
  wrapping and EN/DA; do not add a metadata row to Confirmation. Applied after
  direct worker approval; worker reports Release build and diff checks passed.
  Current user screenshot shows dates wrapping within the title column. Approved
  follow-up: move metadata beneath the masthead columns so it can use the full
  available width while retaining natural item widths, left alignment and gaps;
  do not stretch or distribute items. Wrap only when needed. Implemented in both
  signup Razor pages and scoped CSS; worker reports responsive source/cascade
  and diff checks pass. User then rejected the upper masthead layout: title
  narrowed and capacity shifted left (current screenshot ending `dcecbe1558e2.png`).
  Corrected by `contain: inline-size` on metadata: controlled same-content/width
  rendering proved its intrinsic contribution inflated the capacity track from
  198px to 511px; containment restores the prior upper column widths while keeping
  metadata's full-width wrapping room. Metadata now follows event flow: signup
  close, optional draft start, bingo start/end. Worker verified a rendered masthead
  copy with current CSS at desktop and 390px (no horizontal overflow), plus diff
  check. This is isolated rendering evidence, not a rebuilt app walkthrough. No
  build/tests rerun. User accepted layout/order, then requested only less space
  above metadata. CSS-only follow-up sets masthead row gap to 1rem and removes
  metadata's extra 1rem top margin; narrow gap remains 1.25rem. Scoped diff check
  passed; no additional verification. Changes remain uncommitted and undeployed.
- Current screenshot evidence is the user's confirmed-state image at
  `/var/folders/w5/74mg_d917xg33ry8_4qc9g5w0000gn/T/codex-clipboard-5800ac86-1b34-4ea4-94b5-d93f0b6321a1.png`.
  It is current evidence of repetition, not a historical reference reactivation.
- Implementation is complete. EN/DA selected-Alt HTTP 500 was reproduced; the
  withdrawn-edit diagnostic identified retained registration-order uniqueness,
  not proof of a live reservation leak. All 24 scoped PostgreSQL integration
  tests pass, including a second user claiming the released character and the
  first user's subsequent Rejoin conflict. Browser Alt create/edit and all three
  confirmation states executed; Release build (zero warnings/errors), scoped
  formatting, and diff checks pass. Separate SignupUiTests execution was blocked
  by host named-pipe permissions and not retried.
- User explicitly narrowed verification for usage budget: stop repeated browser
  theme/viewport/language coverage, reuse completed and applicable prior manual
  evidence, and perform one narrow read-only correction review without test reruns.
  No whole-site audit, new readiness cycle, or repeated acceptance of unaffected
  behavior. The narrow independent review found one blocker: saved Alt -> None
  -> Alt edits reused retained registration order. Bounded Astra Low remediation
  now allocates across all historical assignments, following the existing Rejoin
  pattern; the extended EN/DA PostgreSQL journey passes 2/2, with required
  compilation, scoped formatting, and diff checks passing. No new broad review
  or browser run was performed. The reviewer otherwise found the approved scope
  delivered without material unapproved changes.
- Implementation handoff and current screenshots:
  `/Users/christopher/Documents/Codex/2026-09-07/signup-readiness-corrections/outputs/handoff.md`.
  Disposable preview is running at `http://localhost:5187`; database/container
  `signup-readiness-browser` uses port 55487. No real participant data. The preview
  was not restarted after the final service-only order fix; refresh its process
  from the current build before any further functional walkthrough. Existing
  confirmation screenshots remain applicable because that fix changed no UI.
- Next: user visual acceptance of the changed confirmation only; reuse prior
  acceptance for unaffected behavior. Implementation and the named review
  remediation are complete. Separate source-contract test execution remains
  environment-blocked; post-draft copy is source-reviewed, not separately executed.
  All corrections are uncommitted and production remains unchanged by this task.

## Admin event-functionality correction handoff — 2026-09-04

- Isolated working tree: `/private/tmp/BingoWebpage-admin-event-functionality`,
  branch `admin-event-functionality` from `origin/main`. The dirty saved
  `feature/boss-artwork` checkout remains untouched.
- The approved four-pass implementation contract is in `DELIVERY_PLAN.md`
  section 5. The one independent Sol High readiness review is complete; its
  five required plan corrections are incorporated, no product decision remains,
  and the zero-new-infrastructure complexity budget holds.
- Pass 1 is implemented and stops at its planned boundary: Live identity and
  timezone lock, the authoritative schedule matrix, Live end correction,
  retained-future Resume/reopen behavior, postponed-start recovery, atomic
  capacity/audit behavior, durable promotion notification routing, and the
  bounded TEST 05 fixture.
- Focused verification passed: Web and BrowserTests Release builds with zero
  warnings/errors; 99 domain, 10 identity, 14 schedule, 11 scheduled-lifecycle,
  8 capacity/promotion, 15 application-contract, and 8 Wise Old Man
  synchronization tests; `git diff --check` also passes.
- The user-approved risk-based independent Sol High Pass 1 review completed on
  2026-09-04. Its two blockers are remediated: the aggregate draft lock again
  prevents Wise Old Man synchronization from bypassing the schedule matrix, and
  promotion-notification proof now follows the stored Confirmation destination
  as the promoted account. The affected Release build, 11 Wise Old Man tests,
  one promotion-destination test, and `git diff --check` pass. The fixes-only
  independent Sol High re-review passed with no remediation-local defect or
  scope expansion.
- Pass 1 is committed locally as `8f85ac9` (`fix: correct admin event schedule
  and recovery controls`) and has not been pushed.
- Pass 2 is implemented and stops at its planned boundary: payment status and
  Admin notes remain editable across retained visible lifecycles; terminal
  participant detail exposes only those permitted controls; ownership transfer
  is confirmed, concurrency-protected, and available through Awaiting Final
  Review with immediate authority transfer and recipient-safe notifications;
  stale and direct lifecycle-inappropriate mutations remain blocked. No new
  infrastructure or Pass 3/4 scope was added.
- Pass 2 focused verification passed: Web, IntegrationTests, and BrowserTests
  Release builds with zero warnings/errors; 18 authenticated-signup integration
  tests; one browser/source contract test; and `git diff --check`. Manual browser
  acceptance was not run. The independent Sol High review found one terminal
  navigation blocker. The focused remediation now admits the existing
  Participants GET workspace for retained terminal events and proves the real
  rendered detail link while keeping terminal mutations fail-closed; its Release
  build, targeted integration test, and `git diff --check` pass. The fixes-only
  independent Sol High re-review passed with no mutation bypass or scope
  expansion. The user authorized local Pass 2 packaging and Pass 3 start on
  2026-09-04. Nothing has been pushed, merged, or deployed.
- Pass 2 is committed locally as `c00b5d1` (`fix: extend admin participant
  lifecycle controls`) and has not been pushed.
- Pass 3 is implemented and stops at its planned boundary: evidence-code and
  captain/co-captain changes follow the active submission-window lifecycle;
  stale event and membership versions are enforced; reopen-submissions and
  evidence-code mutations are atomic with exactly one audit; and the existing
  Live Wise Old Man replacement behavior is preserved with regression proof.
  No new infrastructure or Pass 4 scope was added.
- Pass 3 focused verification passed: Web, DomainTests, IntegrationTests, and
  BrowserTests Release builds with zero warnings/errors; 100 lifecycle-domain,
  one Pass 3 atomicity, 6 captain-authority, 12 Wise Old Man synchronization,
  5 compatibility-filter, and one scheduled-lifecycle test; `git diff --check`
  also passes. The complete suite, browser execution/inspection, and manual
  acceptance were not run. The independent Sol High review found one
  event-history/privacy blocker: the three new Pass 3 audit writes persisted a
  null `EventId`. Focused remediation now uses the existing event-aware audit
  overload and asserts the correct event association for all three successful
  actions; the filtered Release integration test and `git diff --check` pass.
  The fixes-only independent Sol High re-review passed. All required Pass 3
  scope is cleared with no scope expansion or unbudgeted infrastructure. The
  user authorized local Pass 3 packaging on 2026-09-04. Nothing has been pushed,
  merged, or deployed.
- Pass 3 is committed locally as `0197e90` (`fix: harden admin event operational
  controls`) and has not been pushed.
- Pass 4 is implemented and stops at its planned boundary: initial and corrected
  board publication require popup and server confirmation; corrected publication
  revalidates lifecycle and stale workspaces; snapshot/audit atomicity and
  superseded history are preserved; and late initial publication continues to
  use Pass 1 postponed-start recovery. No new infrastructure or board-editor
  redesign was added.
- Pass 4 focused verification passed: IntegrationTests and BrowserTests Release
  builds; 12 publication/correction integration cases; one scheduled-lifecycle
  regression; one confirmation UI contract test; and `git diff --check`. The
  independent Sol High review found one inert Cancel-button blocker because the
  Board page did not load the unrelated shared handler. Focused remediation wired
  both buttons through the existing Board script and strengthened the source
  contract; 3 focused Release BrowserTests and `git diff --check` pass. The
  fixes-only independent Sol High re-review passed with no scope expansion.
  Pass 4 is committed locally as `a33415d` (`fix: safeguard admin board
  publication`) and has not been pushed.
- The final independent Sol High four-pass review and both follow-up reviews are
  complete. Its lifecycle, authorization, schedule, concurrency, publication,
  and history findings were remediated without expanding the approved scope or
  complexity budget; the final fixes-only review passed.
- The user explicitly waived the manual-acceptance preflight after its TEST 05
  attempt used captains without usable account authority and encountered the
  reset's intentionally retained current Live event; those were verification-
  setup blockers, not product failures.
- The complete suite ran once on 2026-09-04: 711 total, 708 passed, and 3 failed.
  The failures were one hidden-event direct-handler expectation and two blocker-
  label cases. Focused remediation restored the event-list label and aligned the
  hidden direct-handler assertions with the existing fail-closed boundaries;
  the affected BrowserTests theory passes 9/9, the affected IntegrationTests
  theory passes 10/10, and `git diff --check` passes. The accepted remediation
  and final handoff are committed locally as `63ecd9a` (`fix: complete admin
  event lifecycle controls`). Nothing has been pushed, merged, or deployed.
- The Admin-test follow-up investigation is complete and the user-approved
  implementation contract is frozen in `DELIVERY_PLAN.md` section 6. It covers
  invariant Wise Old Man form transport, additive Admin/SuperAdmin plus genuine
  event roles, primary-character draft finalization, immutable catalogue-item
  duplicate identity, the complete submission/review lifecycle matrix and audit,
  linked correction after reversal, objective-isolated retargeting, retained-history
  privacy, bounded navigation/numbering fixes, and accessible evidence-dialog
  zoom/pan. Automatic SuperAdmin Captain status and any new general cross-team
  submission inspection/correction mode are deferred; the existing explicit
  read-only team-focus inspection is unchanged.
- The single independent read-only readiness review initially returned **NOT
  READY** on four bounded contract gaps. The user approved objective-scoped
  shared-item caps, and section 6.4 plus the active product/data/architecture
  authorities now resolve all four: consistent effective alias caps, a
  hash/fingerprint-confirmed same-connection migration mapping procedure, minimum
  pass-owned Development fixtures and a compact manual journey, and the narrow
  archived former-owner `/Evidence/{assetId}` boundary. The planning gate is now
  **READY**.
- The user authorized Section 6 Pass 1. Its implementation, independent review,
  and fixes-only remediation are complete: Signup uses invariant hidden EHB
  transport with localized EN/DA editing and validation; My Accounts Add/Edit/fetch
  renders and binds in request culture; genuine Participant/Captain/Co-captain
  membership now composes with Admin/SuperAdmin while global-only accounts remain
  outside team scope; and Development reset has the minimum additive-role fixtures.
  The reviewer found only missing Danish parser resources and non-discriminating
  Onboarding proof; both are corrected. Web, BrowserTests, and IntegrationTests
  Release builds passed; the implementer's three browser and six focused integration
  cases passed; final remediation's two Signup/My Accounts plus two Onboarding
  culture cases pass. A fresh fixes-only closure review found one remaining test-only
  gap: the two Danish parser resources lacked direct localization assertions. Those
  exact assertions are now present, their focused test passes 1/1, and
  `git diff --check` passes. AF-01 then exposed two real-browser/reachability gaps:
  My Accounts and Onboarding still emitted the invariant numeric client validator,
  and retained Finalized/Archived memberships suppressed current Participant header
  navigation. Focused remediation now gives My Accounts and Onboarding the same
  localized text/decimal boundary, keeps Signup's hidden invariant transport, and
  resolves Participant/Captain navigation to the sole Live team or, when none is
  Live, the sole Awaiting Final Review team. The four EN/DA culture cases and the
  focused navigation integration test pass; `git diff --check` passes. The user
  manually accepted the reachable Signup, My Accounts, additive-role, and header
  journeys on 2026-09-05. Local Discord OAuth cannot reach Onboarding because its
  callback is not localhost; the user accepted its discriminating EN/DA rendered
  control plus persisted-comma integration proof in place of that manual step.
  **Pass 1 is complete.** Do not begin Pass 2, package, commit, push, merge, or
  deploy without the next authorized gate.
- Pass 1 was packaged locally as `5144c0c` (`fix: correct culture and additive
  role boundaries`) and has not been pushed. The user then authorized Pass 2.
- Section 6 Pass 2 implementation completed on 2026-09-06 and is committed locally
  as `039bd77` (`fix: enforce immutable item contribution caps`).
  It adds immutable catalogue-item identity across both snapshot families,
  item-scoped duplicate caps through publication, approval, submission, and the
  existing operator preflight/`--migrate` same-connection temporary mapping flow.
  The one generated migration is
  `20260905221344_AddImmutableCatalogueItemIdentity` with its designer and model
  snapshot; no retained mapping table, new service, route, policy, job, dependency,
  or Pass 3 behavior was added.
- Pass 2's Release Web build, migration generation, idempotent migration-script
  generation, and `git diff --check` passed. Focused test execution was blocked
  before running by the host MSBuild named-pipe permission error; the copied-
  database migration rehearsal was blocked by Docker socket permission. Do not
  call the retained-database migration path release-verified yet. The initial
  independent Sol High review found two P1 correctness defects: reversal
  rebalancing could create false headroom and exceed an already-full item/source
  cap, and retained aliases could still double-count public progress/final
  ranking. Focused remediation corrected both and passed 8 public-progress tests,
  3 targeted submission tests, the Release Web build, and `git diff --check`.
  Concurrent fixes-only and complete-pass reviews then found the same remaining
  P1 ordering defect: raw contributions still drove Recent Drops and EHB before
  cap allocation. The bounded continuation now reuses one cap-aware effective
  allocation before Recent Drops, completion time, EHB, player totals, and
  rankings; its 8 calculator tests, PostgreSQL-backed alias-A/alias-B/distinct-C
  integration regression, Release Web build, and `git diff --check` pass. The
  final fixes-only Sol High re-review passed with no local scope expansion. The
  complete-pass review otherwise found the planned scope present, migration
  structure coherent, no unapproved material additions, no changed non-goals,
  and no Pass 3/4 leakage. The copied-database rehearsal then exposed 12 legacy
  drop snapshots attached to manual objectives (8 event and 4 approval), all
  fabricated by the Development DKL seeder and unreferenced by submissions or
  contributions. Focused remediation now prevents those snapshots at the source,
  reports and excludes them as structural preflight errors, refuses mapping while
  they exist, and makes the migration independently fail closed. Its Release
  build, three focused tests, and `git diff --check` passed; the independent
  fixes-only review passed with no blocker. After deleting only those 12 rows from
  the disposable `bingo_pass2_rehearsal` copy, preflight reported 2,080 rows,
  zero flagged mappings, and zero structural errors. The migration applied there
  with zero null immutable identities in either snapshot family. The original
  local `bingo` database remains unmigrated and unchanged with its 8+4 rows.
- The user authorized Pass 3 on 2026-09-06. Its submission/review correctness
  implementation is complete and committed locally as `fix: harden submission
  review lifecycle`. It adds atomic main audit history and
  rollback proof, authoritative retargeted weights, the one-child Reversed
  correction path, lifecycle/status/idempotency enforcement, the narrow archived
  former-owner detail/evidence exception, lifecycle-sensitive Admin controls, and
  only the required Development fixtures. Review remediation completed the audit
  note/reason payloads, removed stale concurrency data, and proved database-failure
  rollback. Maximum-length remediation keeps complete operation text exactly once
  in `AuditEntry.Details`, records bounded note-presence facts in snapshots, and
  stores valid rejection-notification JSON within 1,000 characters while retaining
  the canonical submission-detail route. Its elevated PostgreSQL regression
  `MaximumLengthNotesAndReasonsFitAuditAndNotificationBoundaries` passed 1/1;
  the affected Release build passed with zero warnings/errors and `git diff
  --check` passes. The final remediation-only and complete-pass independent reviews
  both passed with no findings, no missing scope, no changed non-goals, and no
  unbudgeted artifact. The Pass 2 rehearsal remediation remains committed locally as
  `d0c3cfa7f7f7f9dbe149be346bd5976c54132056` (`fix: reject manual objective drop
  snapshots`). Nothing has been pushed, merged, or deployed.
- The user resolved the Pass 4 additions and Teams ambiguity on 2026-09-06 and
  authorized implementation. Pass 4 implementation is complete, including the
  planned primary-character finalization, orphaned Signup `03`, Admin-logo
  destination, accessible evidence zoom/pan, fresh-signup primary-only
  preferred-account defaulting, Onboarding's joined WOM control treatment, and
  corrected public Teams/Hold reachability. The fixes-only remediation for the
  independent review's four non-discriminating test findings is also complete:
  three focused integration tests pass, the evidence script's runtime reset
  harness passes, and both affected test projects build cleanly. The fixes-only
  review passed. The fresh complete-pass review found only one localization defect:
  the intended English `Teams` / Danish `Hold` pair was rendered as a literal slash
  label. The final bounded remediation now uses the existing localized `Teams` key;
  its focused BrowserTests Release build passes with zero warnings/errors, its exact
  test passes 1/1, and the user-requested remediation-only re-review passes with no
  finding. AF-04's preflight then found two blockers: the test-22 secondary
  Playing fixture conflicted with its informational registration order, and an
  evidence UI test still rejected the now-intended shared evidence script. The
  bounded remediation assigns order 2 only to the test-22 secondary Playing
  fixture, preserves the shared helper's order-1 default for test-15, and updates
  only that obsolete assertion. The focused finalization test, affected
  EvidenceWorkflowUiTests, and the fresh disposable `bingo_slice10_pass103`
  Development-reset harness pass; the final fixture-scope remediation-only review
  also passes. AF-04's executable non-visual checks are therefore clear. A separate
  disposable app launch did not reach a database connection and was stopped, so
  rendered Admin navigation to test-22 Draft/finalize remains manual-only. Pass 4
  is packaged locally as `fix: complete admin follow-up corrections`; nothing has
  been pushed, merged, or deployed. Next action is the remaining manual Pass 4
  acceptance.
- Manual rehearsal on 2026-09-06 exposed three bounded blockers after packaging:
  the shared Admin route filter rejected the contract-approved Live end-time
  correction before Schedule could enforce its end-only rules; public result
  projection assumed exactly one official first-place team although confirmed
  ties are valid; and the reset retained two redundant Live scenarios, including
  one non-fixture current event that blocked Development lifecycle work. Focused
  remediation now lets only Live Schedule POSTs reach the existing locked-field,
  confirmation, reason, and transactional service boundary; represents every
  official first-place team truthfully as a tie; and removes
  `test-88-live-access-blocker` plus `test-90-current-public-event`. Reset now has
  21 scenarios, all Development fixtures, with `Vinterbingo 2026`
  (`test-15-dkl-live`) as the sole Live event. The production singleton and
  global-only/no-membership boundaries remain covered by test-owned setup and the
  existing additive-role fixtures. Both affected Release builds, six focused
  tests, and `git diff --check` pass; the independent remediation-only review
  passes with no findings. A broader reset inventory test still reaches an
  unchanged, unrelated `test-21-final-review` frozen-roster/current-membership
  assertion failure after proving the corrected inventory. This remediation is
  uncommitted; nothing has been pushed, merged, or deployed. The current manual
  database must be reset before the obsolete Live events disappear. The first
  manual rerun then exposed impossible signup-close ordering in four ordinary
  lifecycle fixtures. The shared historical-fixture dates and Vinterbingo's
  custom Live dates now place signup close before event start, while the explicit
  schedule-negative fixtures remain unchanged. The existing reset test now
  checks that invariant across every scheduled non-negative fixture; its focused
  Release run and `git diff --check` pass, and the remediation-only re-review
  passes. Reset the manual database again before continuing acceptance.
- The subsequent manual rehearsal passed every listed journey except three bounded
  Pass 4 defects: the published Teams art stopped below the secondary navigation,
  evidence dialogs exposed unwanted control bars without image-click magnifier
  zoom, and valid Development WOM competition `1516` was rejected by unrelated
  page-wide model-state errors. Focused remediation now extends the art through the
  full navigation row, removes the four viewer control bars and adds click/tap
  fit/2x zoom with drag suppression, and scopes competition binding validation to
  `CompetitionId`. The Release Web build and `git diff --check` pass; no additional
  tests were run. `Det Store Danske Sommerbingo 2027` remains the deliberately
  unpublished missing-Playing-assignment negative fixture, so its readiness blocker
  and public-board 404 are expected. Manual acceptance still must exercise the two
  high-risk journeys omitted from the prior checklist: duplicate-disabled shared-item
  enforcement and finalizing `Det Store Danske Forårsbingo 2026`, the valid primary
  plus secondary Playing-character scenario.
- The user manually accepted the corrected controls-free, click-focused evidence
  zoom and pan behavior on 2026-09-07. The same manual run also accepted the
  duplicate-disabled submission behavior, including removal of an already
  approved item from the eligible-drop selector, and the Chrome submission-drawer
  control-height and select-chevron correction.
- The user reported that every other remaining manual journey passed, including
  finalizing `Det Store Danske Forårsbingo 2026` with its valid primary plus
  secondary Playing-character setup. Ordinary Development WOM linking is
  accepted; the moving-window `1516` Live-replacement timing case is explicitly
  waived as a super-edge-case. No further manual journey blocks packaging.
- The pre-PR timestamp audit found three test-only exact comparisons that used
  sub-microsecond `UtcNow` values across a PostgreSQL round trip. Their inputs or
  expectations now use PostgreSQL microsecond precision. The four focused cases
  pass in Release configuration, and `git diff --check` passes; no production
  code changed in this follow-up.
- The first pre-PR full-suite attempt reached 741 IntegrationTests with 731
  passing and 10 failing. Every failure reproduced in isolation and mapped to
  stale setup/expectations after the accepted passes. The bounded remediation
  initializes the direct Teams page-model context, finalizes the public-board
  team fixture, tests submission closure at the retained cutoff rather than an
  early end, asserts the localized English Teams label, supplies immutable item
  identity to the retained migration fixture, preserves frozen departed roster
  members, and removes the obsolete exact count of owned Live-team fixtures.
  Development completed-board evidence now selects distinct immutable items when
  duplicates are disabled, so re-finalization agrees with authoritative progress.
  All 10 originally failing cases pass across focused Release reruns and
  `git diff --check` passes. The complete suite has not yet been rerun after this
  remediation.

## Canonical checkout

- Path: `/Users/christopher/Documents/BingoWebpage`
- Branch: `production-release-pipeline`
- Production launch: PR #5 is merged and deployed. The deployed source SHA is
  `1f893133edc26455c41535807633225fdee36292` and the immutable image digest is
  `sha256:5de9882be6cd63e170b6d68fc1b869ea134e9b67f3bfab6a1b7eb042ed4c1a20`.
  CI, deployment, and focused production smoke passed.
- Tracking: `origin/production-release-pipeline`; the current branch matches
  its remote tracking branch. Preserve the local developer-only changes listed
  below.
- `50077fd` includes the manually accepted `/HowTo` guide in English and Danish,
  light/dark themes, responsive layout, sticky desktop rails, full-client
  evidence guidance and image, Board countdown-colon alignment, landing
  logged-in auto-scroll removal, and landing lifecycle-label wrapping.
- `62c4ff6` contains the reviewed backup/disk Better Stack heartbeat integration
  and runbook/architecture changes. The production monitoring resources and
  host timers are now active; private heartbeat URLs remain outside Git in
  root-owned `/etc/bingo/monitoring.env`.
- The UI overhaul is merged and pushed to `main` at `52ec8494c6792f1f1f4ccd5893ac0cdb11cb74a3`.
- Preserve the local developer-only `launchSettings.json` override and `tmp/`,
  including the quarantined duplicate files moved under
  `tmp/quarantine-untracked-duplicates-20260827`. They are outside the release
  candidate and must remain excluded from authority searches, staging, and
  review.

## Active production-release handoff

PR #7 was merged and deployed on 2026-09-07. Current deployed source SHA is
`42b2a7dac0f5fd56e627359cba989a5c63c383de`, with immutable image digest
`sha256:b2450a791eb22efdce61d185dd6f630810a1aab170a797916db293f99b4739f7`.
Main CI run `34160464984`, production deployment `34161651143`, and focused
public health/login/Danish HowTo smoke checks passed. This supersedes the old
PR #5 production version; retained infrastructure/import history follows.

Better Stack production monitoring is active: the public `/health/live` monitor,
quarter-hour disk heartbeat, and nightly backup heartbeat are configured, with
both timers active. Disk success, failure, and recovery paths were verified.
A scheduled encrypted backup succeeded with snapshot
`715e2ee745e1fb51f10c510b6b2995aefb5109ea9703dd7f349e8fa37aac70d9`, and
retention was applied. Heartbeat URLs and credentials remain outside Git.

The production historical import succeeded for **Det Store Danske Sommerbingo
2026** (`det-store-danske-sommerbingo-2026`). Preflight found 6 teams, 90
participants, 93 WOM accounts, 25 tiles, and 150 counters. The reviewed
combined hash is
`c8ef4ec0a01ef1779ec3ea358100b91968c942d6f875ec15a7504014b2684df8`.
Production landing, Board, and Teams returned 200, and the user manually
accepted the imported event. Private host/staging input copies were removed;
the ignored local operator input remains outside Git.

The rehearsal event remains Hidden, not deleted, and is reachable only to
SuperAdmin through the direct `/Admin/Events?filter=hidden` route.

Known non-blocking defect, explicitly deferred by the user: selecting Hidden in
the Events dropdown (and potentially switching other server-filtered states)
performs client-only filtering/history replacement, so hidden rows absent from
the normal DOM do not appear. Directly loading `?filter=hidden` works; there is
no data loss.

Next operational stage: run the production Admin test event. It has not run.

## Hidden-event quarantine implementation handoff — 2026-08-31

Implementation is complete, manually accepted by the user after focused
remediation, and included in the deployed `c3e43bb` baseline. Focused gates
passed: affected Web Release build; domain
quarantine 10; destination-policy 20; quarantine integration initially 2 and
then focused remediation suite 6; migration rehearsal 1; architecture;
Bash-syntax; and `git diff --check`.

An independent Sol High review initially found four blockers: Hide
reachability/rendering, emergency-credential access/audit, realtime
access/invalidation, and legacy notification backfill/index. Focused Luna
xhigh remediation closed them. Follow-up review found and closed two
migration-only emergency-audit classification issues. Final independent
closure verdict: PASS.

The final contract remains: only `AwaitingFinalReview`, `Finalized`, and
`Archived` are eligible; SuperAdmin access is limited to the separated Hidden
area and limited Manage inspection; all other paths fail closed. The production
rehearsal event remains hidden, and the separate historical import was completed
on 2026-09-01 as recorded in the active launch handoff above.

The bounded rendered navigation and Hide/Restore journeys were manually
inspected and accepted by the user after focused remediation. The production
rehearsal event remains hidden; the separate historical import was subsequently
completed and manually accepted by the user.

The full regression was accepted at `c3e43bb` after the user's sustained-site
manual acceptance and the recorded automated gates. The production launch was
then completed at the source SHA and image digest recorded above. The planned
Admin test event remains the real-world follow-up safety net.

Next permitted action: run the production Admin test event. It has not run.

## Submission workspace consolidation handoff — 2026-08-31

The user approved and accepted consolidating the Captain and participant
submission workspaces into one canonical implementation. `/Submissions` is the
authenticated, team-wide overview and `/Submissions/{id:guid}` is the detail
route. Captains/co-captains see the Captain-only team-focus and team-submission-
status sections and retain server-authorized broader editing of eligible team
submissions; ordinary participants see neither section and may edit only their
own eligible non-read-only submissions. The detail remains visually equivalent
to the approved Captain detail.

`/Captain` and `/Captain/Submissions/{id:guid}` are thin compatibility
redirects/aliases, never separate rendered implementations. Personal
submission/evidence notifications, including Captain/co-captain recipients,
resolve to `/Submissions/{id}`; relevant general submission navigation resolves
to `/Submissions`; Admin review notifications remain
`/Admin/Review/Details/{id}`. The shared team-board drawer and
`/Captain/Submit` transport remain unchanged. Authorization, retained-state
read-only, cutoff, privacy, evidence-integrity, and Admin authority remain
protected. The relative `_EvidenceUpload` partial reference that could cause a
`/Submissions/{id}` 500 is fixed and covered by acceptance.

The canonical implementation was independently reviewed, remediated, manually
accepted, and committed in
`88cd8f8d6014e947e2a5e97717be460ca2ea9d66`. Verification included Release
builds, 7/7 navigation/integration tests, notification workflow, UI assertions,
focus helper, ledger JavaScript, diff checks, independent review, and user
acceptance. Current teammates may read retained teammate details while
former/cross-team users receive 404; owner/Captain/co-captain/emergency
mutation boundaries remain server-authorized.

The reported absence of a linked resubmission from Admin Evidence Review was a
reader/reviewer misinterpretation, not an additional release gate. The existing
linked-resubmission behavior and tests remain protected; no query change is
approved.

The user approved the following concrete Pass 5/6 closure gates on 2026-08-30:

- Merge only a green pull request, let `main` CI publish the immutable
  `linux/amd64` candidate and receipt, and deploy only that exact digest
  through the explicit manual `mode: deploy` workflow.
- Before candidate deployment, verify key-only `bingo-deploy` access, disabled
  password/direct-root SSH, Docker and the backup timer after reboot, controlled
  PostgreSQL/Caddy image identities, and one naturally scheduled backup whose
  receipt reports successful retention.
- Change the bootstrap owner's initial password before public signup. Keep
  rehearsal data separate; take it through `Live -> AwaitingFinalReview ->
  Finalized -> Archived` to preserve history. The hidden-event quarantine
  implementation is included in the deployed baseline, and the production
  rehearsal event remains hidden. The separate historical import was completed
  and accepted on 2026-09-01.
- Resolve the evidence-storage protection wording before launch: confirm the
  actual R2 accidental-deletion/versioning behavior or explicitly accept and
  document another recovery path. The integrity command detects loss but is not
  by itself a recovery mechanism.
- Decide and verify the production edge contract: Cloudflare DNS-only versus
  proxied traffic, end-to-end TLS, the apex hostname, and whether
  `www.dklegacy.dk` redirects to the apex.
- Better Stack public-health, scheduled-backup, and low-disk monitoring is
  active. The real heartbeat URLs remain uncommitted and are entered only in
  root-owned `/etc/bingo/monitoring.env`.
- Complete the provider-backed rehearsal against the deployed candidate,
  including Discord, R2 evidence round trip/integrity, SignalR and intended-load
  measurements, application journeys, backup/full restore, rollback,
  interruption timing, and post-recovery smoke checks. Then follow the frozen
  whole-application review, bounded remediation, and Pass 6 release gate.

Retain the complete infrastructure and operational checklist, including
optional but prudent safety items. At the deployment step where an item becomes
relevant, present the available providers and tiers, current costs, tradeoffs,
the recommendation for this hobby project, and the consequence of deferring or
omitting it. Do not silently remove an optional item. No external account,
subscription, purchase, paid tier, credential, DNS change, or production
mutation beyond the already approved setup above is authorized without the
user's explicit approval.

Current remaining sequence:

1. Run the production Admin test event. It has not run.

The R2 deletion/versioning or accepted-recovery decision remains an explicit
known operational risk; it is not silently treated as solved by the existing
integrity evidence.

## Pre-commit audit and direct-to-main integration plan

The user approved the following plan on 2026-08-27 for integrating the current
overhaul. The sibling `codex/admin-ui-overhaul-v2` branch is comparison evidence,
not a required merge step. Its later commit
`637a92e` (`refactor(ui): centralize admin event state presentation`) must not be
merged blindly: the current dirty branch already contains the shared state
presentation work, and a direct branch merge avoids redundant conflict work.

Before any packaging or merge:

1. Run one read-only Danish-language audit over all first-party user-visible
   copy. Classify missing translations with proposed Danish wording, existing
   translations that should return to English, terms intentionally retained in
   English, uncertain terms requiring user choice, and technical localization
   defects. Include dialogs, drawers, notifications, validation, empty/error
   states, toasts, buttons, and accessible labels; exclude logs, code, test data,
   user-generated content, vendored assets, generated output, and `docs/archive/`.
2. Run one read-only full Ponytail audit over the active first-party codebase.
   Report ranked concrete opportunities to delete, shrink, or replace custom
   machinery with native/platform behavior. Exclude generated migrations and
   designers, vendored libraries, build output, assets, and `docs/archive/`.
   Correctness, security, and release readiness remain outside that audit and
   must not be conflated with simplification findings.
3. Complete the approved localization remediation in bounded Luna High passes.
   Non-Admin L1 and the Danish decimal-range correction are complete; Admin L2
   is active. After L2, correct the demonstrated Landing `/` label `VIS BRÆT` to
   retain product `Board`, and expand the bounded terminology check to include
   `bræt` as well as `plade`/`bingoplade`.
4. Run one fresh Terra High read-only localization-completeness review over all
   Public, account, participant, Captain, Submissions, and Admin surfaces. It
   checks only whether the accepted English/Danish glossary, resource coverage,
   visible server/JavaScript copy, placeholders, and accessibility text were
   missed; it must not reopen approved visual composition or broaden into the
   final correctness review.
5. Run a fresh Terra High whole-repository Ponytail audit after localization is
   complete. Judge only current active first-party code and the same exclusions
   as the first audit; do not treat correctness, security, or release findings as
   simplification findings.
6. Apply only localization or simplification corrections the user explicitly
   accepts, using bounded Luna High remediation and the smallest risk-based
   verification.
7. Run a separate Terra High read-only correctness and release-gap review of the
   complete post-remediation base-to-current implementation. It owns bugs,
   authorization/privacy gaps, missing approved behavior, accidental scope
   additions, migration/data risks, test discrimination, and commit blockers.
   It must specifically compare the final semantic Admin event display behavior
   with `637a92e`, including whether detailed display labels such as event-ready
   and starts-in states remain available where required; enum/domain lifecycle
   values must not be inferred from presentation labels.
8. Apply only correctness corrections the user explicitly accepts. Repeat only
   the review or gate whose material finding changed; do not restart all audits
   for confidence.
9. Inventory the final commit scope because this dirty checkout contains both UI
   and non-UI work. Compare the final branch semantically with `637a92e` and
   bring over only a genuinely missing desirable behavior. Do not merge the
   sibling branch as a matter of ancestry or bookkeeping.
10. As the last read-only readiness gate before packaging, run one final Terra
    High whole-repository Ponytail audit against the exact candidate tree. If it
    reports a concrete cut, the branch is not ready until the user accepts or
    rejects it; an accepted cut receives bounded Luna High remediation and only
    the smallest focused recheck needed for that changed area.
11. Before the first push, rename the current branch to a concise purpose-based
    name without the `codex/` prefix. Commit only after user authorization and
    accepted verification. Then update local `main` from `origin/main` and merge
    the renamed overhaul branch directly into updated `main`. Push or deploy only
    with separate explicit authorization. The sibling branch may be retained or
    removed after integration; it is not part of the required path. Old branch
    cleanup requires the merged/unique/active inventory and exact user approval
    recorded in `AGENTS.md`.

The localization-completeness review and post-localization Ponytail audit run
only after L2 and the Landing terminology correction. The final correctness
review and final Ponytail gate use the later post-remediation candidate tree. No
staging, commit, merge, push, branch deletion, or deployment is authorized by
this plan update.

The final Terra High correctness and release-gap review completed on 2026-08-27.
It found no missing approved behavior, correctness, authorization, privacy,
concurrency, data-integrity, or migration blocker. The current tree preserves
the required detailed Admin event display phases and contains no desirable
behavior missing from sibling commit `637a92e`; that commit still must not be
merged. The review initially classified `/Admin/UiReferences` as unapproved
material scope. The user explicitly approved retaining it on 2026-08-27, and
`UI_PAGE_MATRIX.md` now records it as a direct-link internal historical-reference
gallery that is not a product-page approval target or active authority source.
That finding is resolved. Pass 4 and its bounded release-blocker corrections are
committed; provider-backed launch, monitoring, smoke, and historical-import
evidence are recorded above. The production Admin test event remains the next
operational stage.

The read-only commit-scope inventory and final Ponytail gate also completed on
2026-08-27. The accepted candidate includes the active authority consolidation,
archive moves, approved historical UI references and `/Admin/UiReferences`, all
accepted application/domain/infrastructure/Web changes, complete migration/
designer/snapshot sets, localization, assets and their license notices, and the
proportionate tests. It excludes `tmp/**`, ignored runtime evidence/uploads/
caches, build output, local data, and `src/Bingo.Web/Properties/launchSettings.json`;
that tracked developer-only file remains a working-tree modification but must not
be staged because its WOM-fake override is outside the accepted candidate. The
missing Geist notice was resolved by adding the official OFL 1.1 text beside the
bundled font, with no UI or runtime change. The final fresh Terra High whole-repo
Ponytail audit reported `Lean already. Ship.` No simplification remediation or
repeat audit is required. The user authorized renaming the branch to
`ui-overhaul` on 2026-08-27; the rename preserved the same HEAD and dirty tree.
The user authorized a Packager on 2026-08-27 to stage only the inventoried
candidate, inspect its manifest, and create one commit on `ui-overhaul`. That
authorization does not include merging, pushing, deployment, branch cleanup, or
production release; each remains separately unauthorized.

The two read-only pre-commit audits completed on 2026-08-27. The language audit
found 31 Danish resource values that incorrectly translate `Board`, 537 of
1,071 used literal localization keys without a Danish resource entry, 226 raw
server-side user-message call sites, 39 raw JavaScript visible/accessibility
copy call sites, and 202 raw Razor label/accessibility lines. The user approved
remediating every named localization class. The accepted glossary keeps
`Board`, `tile`/`tiles`, `drop`/`drops`, `draft`, `Live`, OSRS/EHB/DEHB, Discord,
Wise Old Man, MVP, and product names in English; uses `Admin` for the product
role/UI title and Danish `administrator` only for a person in prose; and requires
uppercase `WOM` everywhere rather than `WoM`. The Ponytail audit found one
approved simplification, applied in a bounded Luna High remediation pass; no
audit modified production code. The bounded non-Admin L1 remediation now has
complete scoped literal-key coverage and a passing Web Release build; Admin L2
is active. The first-request Danish decimal-range parsing defect is also fixed
across My Accounts and Onboarding with invariant hard-coded limit parsing and a
passing Danish-first focused regression. The demonstrated Landing `VIS BRÆT`
residual remains queued until L2 completes. The additional review/audit order is
frozen in steps 4–10 above.

The initial live-release policy is recorded in `TECHNICAL_ARCHITECTURE.md`
§12.1: merging to `main`, building a release, and deploying production are
separate operations. Passes 1–4 are committed below; merging to `main` still
does not update live production without the explicit manual `mode: deploy`
dispatch, which is the user's production approval.

## Pass 4 repository handoff — 2026-08-28

Pass 4 is committed through `329e04adaa98a444f69d20aa41385b4ca7426bd3`; the
bounded release-blocker correction was accepted and committed through `aa1af77`.
It closes exact PostgreSQL restore, `none`/`none` baseline recovery,
post-backup failure classification, and contradictory `new` marker/history
handling. The separate bootstrap-password correction is also complete: the
password is bootstrap-only and root-file supplied, absent from the long-running
web container and durable backup/config payloads, retained on failed
initialization, and removed after successful initialization or safe resume. The
later local commits add the production routing/live-control correction, drawer
controls, canonical submission workspace, and notification/progress notices;
their current branch and deployment status are recorded in the active
production handoff above. The revised remaining order is owned by the
production/release section of `DELIVERY_PLAN.md`.

The whole-application regression and local release gates are complete. The
provider-evidence release-risk review follows the completed submission
correction and capacity sub-gate so real VPS, provider, backup/restore, load,
Discord, R2, SignalR, and health evidence replaces assumptions. It must not
reopen approved UI or become an unfocused line-by-line audit. The preserved
developer-only `launchSettings.json` modification and `tmp/` remain outside
the release handoff.

The bounded correction touches only the approved release surface: Compose and
production env examples; host operations/backup/restore/deploy/validation;
production topology/runbook; technical architecture; README; and this status/
delivery handoff. Bash syntax, YAML parsing, physical-volume/database-authority
checks, exact database reset/history ordering, baseline state handling,
post-failure receipt/status preservation, marker/history contradiction checks,
special-character connection construction, state cases, ordering,
manifest/tamper/mixed-set checks, Caddy/PostgreSQL preservation, cleanup/trust,
documentation links, secret scan, and `git diff --check` pass. Docker-dependent
restore/Compose rendering remains unverified because the local Docker API
denied the socket; no dependent command was retried.
Provider-backed restore duration, certificate issuance, password compatibility,
and interruption measurement remain Pass 5 evidence.

## Functional position

The accepted functional foundation and active Milestone 9 UI-overhaul baseline
remain in the dirty checkout. Business rules, persistence, authorization,
audit, transaction, concurrency, privacy, evidence-integrity, SignalR
invalidation, and historical-record protections remain unchanged by this
documentation-only pass. The current dirty checkout also contains the bounded
Recent Drops and public-board masthead implementation described below plus the
behavior-neutral stylesheet ownership split described here; no application
behavior or tests were changed by this handoff update.

Active CSS now loads in the prior preserved order as transitional foundation,
Public UI, then transitional application rules. `site.public-ui.css` is the
sole destination for reusable Public UI overhaul primitives, the transitional
files retain mixed existing rules until their owning surfaces are migrated and
verified, and `site.css` is a compatibility marker only.

The user approved a replacement Public UI identity on 2026-08-22. It replaces
the prior charcoal/glass public appearance and the earlier visual-protection
claim for the public Board, but it does not reopen or change Board, team, tile,
submission, evidence, navigation, route, authorization, validation, realtime,
or business behavior. The exact pass order, theme invariant, complexity budget,
and approval gates are recorded in `DELIVERY_PLAN.md`, `UI_SYSTEM.md`, and
`UI_PAGE_MATRIX.md`. Admin remains strictly out of scope for this experiment.

The first Pass 1 implementation was manually rejected on 2026-08-22. The
current rendered implementation is preserved in
`/Users/christopher/Library/Mobile Documents/com~apple~CloudDocs/Downloads/ThisOne.pdf`;
the approved landing contract remains the named landing reference in
`UI_PAGE_MATRIX.md`. The rejected implementation retained legacy/generic public
Razor composition and layered a scoped theme over it. Do not apply its narrow
signup-select remediation and do not extend that approach to another family.
The active correction is one atomic landing presentation rewrite: preserve
handlers, models, routes, authentication, localization, data, and interaction
semantics, but replace the landing markup, layout primitives, typography,
links, actions, icon treatment, event ledger, and responsive composition.
Static landing editorial copy is composition-flexible for this pass: it may be
rewritten in equivalent natural English and Danish while preserving truthful
product meaning, dynamic event facts, destinations, and action semantics.

The post-review remediation is materially closer but remains manually
unapproved as of 2026-08-22. Its current visual evidence is
`/Users/christopher/Library/Mobile Documents/com~apple~CloudDocs/Downloads/Here.pdf`.
No font metadata was recovered from the AI-generated reference. The active
landing-only correction compares the current Barlow Condensed, Bebas Neue, and
at most one genuinely closer license-safe condensed candidate with the exact
headline/event-title/numeral specimen. A clearly closer face may be adopted only
as an implementation approximation; otherwise the pass stops with a compact
A/B/C specimen for user choice. The correction also normalizes major blueberry
numerals, rebuilds feature/event rule geometry, corrects palette and DK-art
treatment, normalizes the three feature icons, and rebalances the ledger
columns. This is manual visual remediation within the approved direction, not a
new design.

The bounded local specimen compared Barlow Condensed and Bebas Neue and selected
Bebas Neue Regular as the current implementation approximation; no third local
candidate was available. This does not identify the unknown reference font or
approve the page. Fresh evidence awaits user review at
`tmp/public-ui-pass1a/manual-refine-light-1586x992.png`,
`tmp/public-ui-pass1a/manual-refine-dark-1586x992.png`, and
`tmp/public-ui-pass1a/manual-refine-mobile-390x844.png`; the specimen is
`tmp/public-ui-pass1a/font-candidate-specimen-ABC-1200x480.png`.

On 2026-08-22 the user authorized one quick typography-only reconsideration
after identifying `https://outbid.website` as a much closer hierarchy example.
The bounded experiment compares the current Bebas Neue 400 display roles with a
real locally bundled Barlow Condensed 800 face at natural width. It may change
only the landing display face/weights and the smallest font asset/license
inventory needed for the comparison. Geist body copy, utility roles, copy,
colors, layout, spacing, responsive geometry, behavior, and every other family
remain frozen. No horizontal scaling or synthesized weight is allowed. This is
an implementation approximation test, not reference-font provenance or page
approval.

The trial is now applied to the landing display selectors. The browser resolved
`Barlow Condensed ExtraBold` at weight 800 from the bundled official static
face, while Barlow Condensed SemiBold 600 utility roles and Geist body roles
remain unchanged. The focused release build passed with zero warnings/errors.
Exact rendered evidence is
`tmp/public-ui-pass1a/barlow-800-trial/exact-app-attempt-1586x992.jpg`;
the common-content comparison source is
`tmp/public-ui-pass1a/barlow-800-trial/typography-ab.html`. The user later
selected Barlow 800 and accepted the final landing.

The user accepted the Barlow 800 direction, especially for numerals, and named
one ledger/icon correction from four 2026-08-22 screenshots. Current-event names
are optically too large and must be reduced without changing the accepted
number/heading hierarchy. Current-event vertical date dividers should be more
inset/shorter; neutral row hairlines should extend slightly farther left toward
or just before the divider as in the target; and the last current-event row has
no bottom hairline. Previous/archived events must not reuse the full
date/status/details ledger: use a compact distinct row with event name/state at
left and View history at right while preserving dynamic archive data/routes.
The three feature SVGs are also materially too small and underweighted. Enlarge
their real rendered optical box and redraw/tune them to match the target family:
firm coral broadcast, full ink document with distinct sage badge, and fuller
bronze four-bar chart with baseline. Keep comparable size, stroke presence, and
alignment; do not change feature copy, colors, or overall strip composition.
The Current events and Previous events Barlow 800 section headings also reduce
slightly from the current render while retaining their hierarchy and rule
alignment.

Workflow clarification from the user: detailed rendered self-inspection occurs
on the first coherent page-family pass, when explicitly requested again, or when
a demonstrated visual uncertainty can materially change the implementation. Do
not repeat heavy visual inspection on every small remediation. For surgical
corrections, implement the named findings, run focused technical checks, capture
evidence only when it is already inexpensive, and return the result for the
user's visual inspection without iterative subjective tuning.

The bounded ledger/archive/icon correction is now implemented only in
`Pages/Index.cshtml` and `site.public-ui.css`. The focused Release Web build
passed with zero warnings/errors and diff checks passed. Light handoff captures
are under `tmp/public-ui-pass1a/remediation-20260822-final/`; dark/mobile
captures were intentionally skipped under the updated small-remediation
inspection rule. The later SVG-only correction completed before final user
acceptance.

The user accepted that correction except for one final SVG-only issue. The
newest target crop is
`/var/folders/w5/74mg_d917xg33ry8_4qc9g5w0000gn/T/codex-clipboard-6b6ef49d-3d5d-4675-a26c-7fe09eb622de.png`;
the current crop is
`/var/folders/w5/74mg_d917xg33ry8_4qc9g5w0000gn/T/codex-clipboard-bf7d3ea9-f480-4c45-889d-ad40f3ca8b30.png`.
Reproduce the target broadcast, document/check, and four-bar chart geometry
literally rather than drawing another approximation, and keep every stroke plus
cap/join inside a safe 32×32 viewBox margin so no icon clips. Do not change the
accepted icon size, feature layout/text, colors, typography, ledger, or behavior.

The final SVG-only pass is implemented in `Pages/Index.cshtml` with no CSS or
layout changes. All three paths retain safe viewBox padding, `git diff --check`
passes, and the focused Release Web build passes with zero warnings/errors. Per
the small-remediation workflow, no screenshot/self-review loop was run; the user
owns visual acceptance.

Public landing Pass 1A is manually accepted as complete by the user on
2026-08-22. The accepted working-tree result uses Barlow Condensed ExtraBold 800
display/numerals, Barlow Condensed SemiBold 600 utility roles, Geist body copy,
target-matched feature SVGs, compact current/archive ledgers, explicit semantic
action tones, and the corrected light/dark token split. Focused Release Web and
diff checks pass. Landing dark/mobile coverage remains part of final regression,
not a reason to keep Pass 1B paused or to reopen the accepted composition.

On 2026-08-23 the user narrowly reopened only the Landing hero masthead artwork.
The completed correction replaced its old PNG/clipped-container/rotated-line
treatment with the approved Login light/dark logo SVGs and the same
percentage-painted diagonal background idea, added the target bottom divider
from the left content inset to the right edge, and hid the artwork when the hero
stacks. After a final dark headline-separation correction, the user manually
reapproved the Landing on 2026-08-23. Its approved copy, actions, feature strip,
event ledgers, shared navigation header, behavior, and remaining composition
stay frozen.

Public UI Pass 1B was manually rejected by the user on 2026-08-22 after its
implementation, independent review, and narrow remediation. The behavior checks
remain useful, but the rendered Signup, Confirmation, Login, Onboarding,
AccessDenied, Error, and StatusCode bodies retained the legacy widths,
containers, DOM flow, and generic composition under the new identity. They are
not a visually acceptable baseline. The next task is a fresh structural
presentation rewrite from PUB-REF-05/06/09 with substantial scoped Razor
replacement. The shared public header remains one `_Layout.cshtml`
implementation, but the user has since rejected its pale/near-black
Signup/Login treatment: those pages must use the approved blueberry light
masthead and high-contrast dark shell without duplicating navigation or changing
header behavior. The accepted landing composition remains frozen, except for the
user-authorized discovery fix that must list public signup-open TEST 16 without
requiring a roster or board. Do not begin Pass 2 before fresh review and user
manual acceptance.

The attempted fresh Pass 1B structural rewrite was also manually rejected on
2026-08-22. The evidence roles are explicit: the TEST 16 PDF in the user's
Downloads folder is the current failed render; PUB-REF-05 is the approved Signup
target. The implementation still retained the old narrow/vertical form flow
instead of the target's wide masthead/capacity/status band, three compact ruled
rows, persistent right summary rail, and bottom action row. Broad Pass 1B work is
stopped. Recover one atomic slice at a time: Signup and standalone Login first,
with actual 1586×992, mobile, and dark renders returned for user inspection
before Confirmation, Onboarding, status pages, or Pass 2. User-supplied desktop,
dark, and mobile screenshots now block both atomic pages: Signup still lacks the
compact full worksheet and real capacity/waiting focal projection; Login remains
a narrow central island rather than the reference split page. The shared header
Sign in link correctly navigates to standalone `/Account/Login`; the remaining
header work is visual-only on Signup/Login, with explicit event/signup dialog
launch points unchanged. Static copy may change in localized English/Danish to
fit the references while preserving meaning, dynamic facts, and actions.

The bounded Luna-high remediation is now implemented and source-verified. It
uses the existing shared header with approved Signup/Login light/dark treatment,
projects real confirmed/capacity/waiting values, restores confirmed/capacity as
the Signup masthead focal metric, compacts the worksheet and action row, and
expands Login into the reference-owned form/art split with the repository DK
mark. Release build, focused Signup/Login/header checks, and `git diff --check`
pass. No live visual inspection was performed by the worker. The Signup route,
including its responsive account-row correction, was manually approved by the
user on 2026-08-22. Confirmation was also manually approved on 2026-08-22.
Standalone Login, including its rebuilt DK artwork and light/dark responsive
composition, was manually approved on 2026-08-23. AccessDenied/403,
StatusCode/404, and general Error/500 were manually approved on 2026-08-23
after the shared code/divider geometry and ExtraBold 800 title role were
corrected. Onboarding was manually approved on 2026-08-23 after its bounded
responsive field-width, divider, dark-label, and WOM-control corrections. Pass
1B is manually accepted; Pass 2 remains gated by its own user-authorized start.

The user then clarified the shell root cause: the accepted Landing header is not
a page-specific reference to imitate; it is the single header that every public
route must render from `_Layout.cshtml`. The current conditional Landing versus
`public-live-header-*` class tree is therefore superseded. The next bounded
implementation must make `landing-shell-*` the global public header, preserve
all dynamic shell behavior and subordinate event/account navigation, remove the
new Signup/Login-only header skin, and leave the separate Admin layouts
untouched. This shared-shell correction is now implemented: `_Layout.cshtml`
emits one `landing-shell-*` header path for every non-overlay public route, the
`public-live-header-*` runtime path and Signup/Login-only skin are removed, and
the shell light/dark tokens resolve globally without applying Landing body
geometry elsewhere. Bounded header/popover tests, Release build, and
`git diff --check` pass; Admin layouts were not changed by this task. Replacement
screenshots are the next gate.

## Launch order and dates

Production is live at source SHA
`1f893133edc26455c41535807633225fdee36292` with immutable image digest
`sha256:5de9882be6cd63e170b6d68fc1b869ea134e9b67f3bfab6a1b7eb042ed4c1a20`.
The full regression, CI, deployment, focused smoke, monitoring activation, and
historical import are recorded in the active production-release handoff above.
The planned Admin test event remains the real-world follow-up safety net.

## Current UI approval snapshot (non-authoritative)

This compact snapshot is derived from [`UI_PAGE_MATRIX.md`](UI_PAGE_MATRIX.md),
which is the sole current page approval/status authority. `CURRENT_STATUS.md`
remains authoritative for checkout state, blockers, limitations, current work,
and immediate ownership.

| Surface | State |
| --- | --- |
| Admin shell; Event Create; Identity; Schedule; Manage/Overview; Events directory | Approved |
| Public UI foundation catalogue / `/Admin/PublicUi` | Historical public specimen retained; not a gate for the approved replacement identity and not in implementation scope |
| Participants and accepted participant-detail dialog states | Approved |
| Catalogue; Accounts/Roles Index/Create/Manage/Transfer | Approved |
| Board | Approved — user manual approval, 2026-08-14 |
| Signup Questions route/dialog | Approved — user manual approval as the Participants/signup-form popup, 2026-08-24; CSV is not owned by this route |
| Teams/Draft, including advanced pre-formed-roster CSV import | Approved — user manual approval, 2026-08-16 |
| Canonical submission workspace / `/Submissions`, `/Submissions/{id}` | Approved — independently reviewed, remediated, manually accepted, committed in `88cd8f8`, and accepted through local whole-application regression on 2026-09-01 |
| Admin evidence review | Deployment ready, not approved — user decision, 2026-08-26 |
| Public board/evidence | Approved — Board overview, TeamBoard, nested Tile view, attached submission drawer, evidence lightbox, Recent Drops, Leaderboards, and final TeamBoard corrections manually approved by 2026-08-26 |
| Finalize/closeout | Deployment ready, not approved — user decision, 2026-08-26 |
| Audit | Deployment ready, not approved — user decision, 2026-08-26 |
| Public landing | Approved — user manual acceptance, 2026-08-22 |
| Public signup/confirmation | Approved — Signup and Confirmation user manual acceptance, 2026-08-22 |
| Public Signups directory | Approved — user manual acceptance, 2026-08-24 |
| Public Teams/roster | Approved — user manual acceptance after bounded masthead, roster, and draft-results corrections, 2026-08-24 |
| Authentication/errors and Account Settings/My Accounts/My Events | Approved — page-specific manual acceptance by 2026-08-24 |
| Change/Forgot/Reset Password, Notifications, and Privacy | Approved — user manual acceptance, 2026-08-24 |
| Setup | Approved — user manual approval, 2026-08-26 |
| How To | Approved — user manual acceptance, 2026-09-01 |
| Dashboard/action inbox | Deployment ready — intentional shell-owned WIP presentation, 2026-08-24 |

The matrix records page-specific approval. The complete public Board ecosystem—
masthead, View bingo, Recent Drops, leaderboards, team overlay/grid, main and
tile-detail sidebars, approved submissions/lightbox, submission drawer/form, and
responsive behavior—was manually accepted on 2026-08-20 and remains the
protected behavior/interaction baseline. Its former visual identity is
superseded by the approved Public UI rebuild and will be migrated only in the
ordered Board pass. The canonical submission workspace is approved and
deployment ready; its consolidation is complete, independently reviewed,
remediated, manually accepted, and committed. Current unapproved
Admin UI visual debt is non-blocking for launch because its functionality works;
it must not be described as UI-approved or as whole-application production
readiness. Only security, authorization, privacy/data-loss/data-integrity, or
workflow-blocking Admin defects may interrupt launch-critical work.

## Accepted Recent Drops and public-board masthead handoff

The live public Board Recent Drops surface is implemented and was iteratively
accepted against real seeded development data and screenshots on 2026-08-18.
Its current behavior is:

- The feed groups approved drops into Last hour, Last 24 hours, and Older drops;
  each card keeps the historical progression captured by that drop
  (`ProgressAfter/Target`) rather than rereading current tile progress.
- Approved evidence opens in the existing lightbox. The feed starts with 25
  items and loads 25 more incrementally, with a Back to latest fragment link
  after expansion.
- The right rail contains the statistics card (total drops, last-24-hour
  drops, total Drop EHB, unique players with a drop, highest Drop EHB team, and
  most individual drops team) and a sticky search/team-filter sidebar on wide
  screens. Search covers drops, players, teams, and tiles.
- The accepted responsive layout keeps the feed and right rail in two columns
  where they fit, then moves the toolbar above the feed in the one-column
  responsive fallback. Search and team changes reset the visible count,
  preserve focus/scroll during enhanced navigation, and synchronize
  `dropCount`, `dropSearch`, and `dropTeam` in the URL. Ordinary GET
  form/navigation remains available as the ordinary route path when enhancement
  is unavailable.

The shared rightmost masthead component follows the event lifecycle: it shows
Submit drop while submissions are open; shows the provisional In the lead
state during `AwaitingFinalReview`; and shows the official winner from the
official placement snapshot for `Finalized`/`Archived` when published results
are available (otherwise Results pending). On 2026-08-18 the user manually
confirmed that the first-place `#1` renders in the correct gold color and that
the rightmost masthead component shows the correct information for each
lifecycle state.

The Board family approval is now formal in `UI_PAGE_MATRIX.md`. Whole-
application regression and production release gates remain separate and do not
claim that the whole application is production-ready.

## Active authority consolidation

Documentation consolidation is complete as of 2026-08-15. The UI authority
consolidation pass and workflow authority consolidation pass are complete. The
active documents are:

- [`UI_SYSTEM.md`](UI_SYSTEM.md) — global primitives, exact ownership,
  responsive/accessibility rules, protected baselines, and review contract.
- [`UI_PAGE_MATRIX.md`](UI_PAGE_MATRIX.md) — page family, canonical
  reference, protected composition, exception, approval, and next gate.
- [`FUNCTIONAL_CONTRACTS.md`](FUNCTIONAL_CONTRACTS.md) — final end-to-end
  journeys, actors, reachability, authority handoffs, failure/recovery
  behavior, and acceptance outcomes.
- [`DELIVERY_PLAN.md`](DELIVERY_PLAN.md) — remaining documentation/UI order
  and release gates.

`ADMIN_UI_CONTRACT.md` and `UI_OVERHAUL_ROADMAP.md` are retained as short
non-authoritative tombstones. Their exact pre-consolidation bytes are archived
at the paths indexed in [`docs/archive/INDEX.md`](docs/archive/INDEX.md).
`FUNCTIONAL_WORKFLOWS.md` is likewise a short non-authoritative tombstone; its
exact pre-consolidation bytes are archived and indexed there. Archived material
is evidence only and cannot approve scope or override active documents.

The archive-promotion pass is complete as of 2026-08-15. The superseded
implementation roadmap, completed Slice 1–10 plans, and Slice 1–3 manual result
records are preserved as exact working-tree copies under `docs/archive/` with
their SHA-256 values in [`docs/archive/INDEX.md`](docs/archive/INDEX.md). No
durable product, workflow, data, architecture, or UI rule was promoted from
these historical documents. F-05 notification source-of-truth reconciliation
was resolved by documentation-only edits on 2026-08-15. The Application Atlas
retirement and durable-finding routing pass is also complete: the exact dirty
Markdown and HTML bytes are preserved under `docs/archive/superseded-assessments/`
and indexed with matching hashes. The bounded active core-document boundary
reconciliation is complete as of 2026-08-15: authority boundaries, stale
status framing, and active cross-routing were corrected without changing
product/UI behavior or promoting archive material. Focused replacement-
link/content verification is complete as of 2026-08-15: 15 active root
Markdown files and 62 local links were checked with no broken targets or
anchors; 20 archived files/hashes match `docs/archive/INDEX.md`; 3 root
tombstones are short, rule-free, non-authoritative, and correctly linked; and
the manual checklist has 24 headings and 180 checkbox items with a valid
archive-evidence link. No stale retired-root links, stale phrases, duplicate
authority entries, or Atlas-as-active wording remain, and documentation/archive
`git diff --check` passed. F-03 was classified against the current dirty
Manage baseline: its readiness rows, blocker destinations, stage-scoped
lifecycle controls, and authoritative projections already represent the
approved behavior, so it creates no new active requirement. Do not treat these
passes as product or UI approval.

## Verification limitations

- The checked-in DKL Development fixtures/reset use fictional identities and do
  not seed the frozen historical event or a real historical roster. The
  operator-private historical input remains external to Git and is not touched
  by Development reset.

- The complete public Board ecosystem and its responsive team-board/submission
  interaction model are manually accepted. The canonical submission workspace
  is independently reviewed, remediated, manually accepted, and committed in
  `88cd8f8d6014e947e2a5e97717be460ca2ea9d66`. The full regression and PR #5
  production launch were accepted with passing CI, deployment, and focused
  production smoke.
- Provider-backed production evidence is recorded against the deployed source
  SHA and immutable image digest above. The production rehearsal event remains
  hidden, the historical import succeeded, and the production Admin test event
  has not yet run.
- Archive hashes match the captured pre-consolidation sources:
  `ADMIN_UI_CONTRACT.md` / archive `be0679c744604c0e1a75f26244e75e38631ea28b0e8462f15bb4e77f979f3360`;
  `UI_OVERHAUL_ROADMAP.md` / archive `d3a93ee2b4e67a690820d5a2875cf20454e5483c37e250cf0613308b453ac950`;
  `FUNCTIONAL_WORKFLOWS.md` / archive `b9a0fd439e69aebfdcc52905d6d0af51ad85039745b4bbe78a2507077fef0a45`.
- Atlas archive hashes match the captured 2026-08-15 working-tree sources:
  Markdown source / archive `f7b31fd1177cf2374fc5d10ec27aa767cda5c3e7f2bc40f05d3fd5010afc5101`;
  HTML source / archive `a6ea62317a82515d395e9fde8f32328f27782bff1bce53a9790f578550cc3ab5`.
- The active authority documents were checked for stale boundary/status
  wording, archive-as-authority routing, unresolved decisions, links, and
  protected source-file changes.
- The 2026-08-15 archive promotion preserved all 14 candidate working-tree
  files exactly; individual SHA-256 values and archive destinations are in the
  archive index.

## Explicit unresolved decisions

- **F-04:** resolved; event identity and display timezone are read-only in Live.
  The separately approved Live event-end correction belongs to Schedule and
  does not reopen identity editing.
- **F-06:** resolved by the approved five-step `/HowTo` guide; no Rules editor,
  sixth step, or in-application HowTo editor is in scope.
## Immediate ownership and stop rules

1. UI planner/orchestrator: Landing, Signup, Confirmation, standalone Login,
   Onboarding, AccessDenied, Error, and StatusCode are complete and manually approved.
   Preserve their accepted target-owned page structures, shared Landing
   navigation header, normal standalone Login navigation, and rebuilt DK
   artwork. Pass 1B is complete. The user authorized Pass 2 on 2026-08-23 and
   its bounded account/public-utilities implementation is complete. Account
   Settings is manually approved. My Accounts has completed independent review
   and its bounded Luna High remediation against PUB-REF-10 is complete: equal
   Add-row fields, horizontal Saved EHB/Fetch composites, one masthead divider,
   input-aligned actions/reorder controls, one short registered-event label, and
   a localized native confirmation prompt now replace the visible unlink
   checkbox while the server remains fail-closed. Focused source, responsive
   cascade, localization XML, confirmation, and diff-hygiene checks pass; the
   established MSBuild blocker prevented executable .NET verification. My
   Accounts was manually approved by the user on 2026-08-24 after its responsive
   Add-form, EHB precision, and move-to-position-01 corrections. The
   current Fetch/Correct/account-action behavior drift remains a separate
   functional blocker not closed by that visual approval. Remaining Pass 2
   pages still await their own actual-route evidence/review. On 2026-08-24 the
   user explicitly deferred that remaining manual acceptance and authorized the
   remaining Public UI passes to proceed sequentially in this exact dirty tree.
   Completed but unseen pages remain `implemented; manual acceptance deferred`;
   they are not approved. The accepted production/test changes are included in
   the deployed PR #5 candidate; preserve the developer-only
   `launchSettings.json` modification and `tmp/` outside packaging. The next
   operational action is the production Admin test event, which has not run.
   The latest My Accounts manual correction makes position 01 the sole preferred
   character and removes the separate set-preferred action; it also shortens the
   linked Fetch label, all visible EHB labels, and the registration warning. The
   bounded remediation and preferred-order data migration are now present. The
   migration's namespace analyzer failure was corrected, and a focused Release
   build passes with zero warnings and zero errors. The intermediate two-by-two
   Add form and standard two-decimal My Accounts saved-EHB rounding are now
   implemented. The false move-to-position-01 conflict was traced to transferring
   the partial-unique preferred flag in one database save; the service now clears
   the old flag before assigning the new position-01 preference inside the same
   transaction. Focused Release builds and source/diff checks pass. The focused
   PostgreSQL integration scenario remains unrun because the worker could not
   access Docker. The user then supplied My Events light desktop, dark desktop,
   and 390px narrow screenshots. The user stopped the independent reviewer after
   its concrete PUB-REF-10 finding: replace inherited full-width semantic status
   bands with compact dot/label status followed by a neutral vertical divider,
   preserving association across desktop and narrow layouts. Landing is not a
   My Events reference; only already-existing assets or selectors may be reused
   where the similar—but not 1:1—structure genuinely matches. The bounded Razor/
   CSS remediation and focused Release build completed, and the user manually
   accepted My Events on 2026-08-24 despite a remaining non-blocking visual
   imperfection. The user next authorized one bounded shared secondary-navigation
   correction: relocate the existing event/account context links below the blue
   masthead and style them as PUB-REF-02 content-level tabs while preserving all
   routes, visibility, active state, localization, focus, and narrow access. The
   approved account page bodies and Board behavior remain frozen. The bounded
   shared-layout/CSS implementation and focused navigation test, Release Web
   build, and diff checks pass. During manual inspection the user directly
   reopened one My Events detail: retain dividers between rows but remove the
   final row's bottom divider in each Current Events or History list. One fresh
   Luna High remediator removed the terminal border, and its bounded continuation
   removed the remaining bottom padding while preserving top/inter-row spacing.
   The same manual check
   named a separate shared-shell remediation: substantially reduce page top
   padding where secondary navigation is present, thicken primary and secondary
   active underlines, and place the primary underline beneath its text with the
   secondary row's internal-padding geometry rather than on the masthead bottom.
   The fresh Luna High remediator completed those shared layout/CSS corrections.
   The focused navigation test, scoped diff check, and Release Web build pass with
   zero warnings/errors. The user then named one shared interaction-state finding:
   secondary tabs need the primary header's text-color-only hover cue, and dark
   mode currently lacks a visible hover color change in the header. The fresh
   Luna High CSS/state remediator completed that correction; the focused
   public-dialog navigation test and scoped diff checks pass. A Release build was
   not repeated for this CSS/test-only follow-up; the immediately preceding
   shared-navigation Release build passed. The user rejected the hover direction:
   all primary/secondary labels in light and dark must share the same full-strength
   resting color whether selected or not, and only inactive hover fades the text;
   selected labels stay full-strength and underlined. The fresh Luna High CSS/state
   remediator completed that reversed mapping; the focused bundled-Node navigation
   test and scoped diff checks pass. The user accepted the corrected shared
   navigation by moving to the next-page gate. The user then authorized one
   bounded `/Account/ChangePassword` dark-mode correction under PUB-REF-08:
   `Account security`, `Current password`, and `New password` use cream rather
   than violet, and resting inputs reuse the approved neutral dark border. The
   fresh Luna High remediator completed that page-isolated correction; scoped
   selector/isolation and diff checks pass. A Release build was not repeated due
   the documented MSBuild sandbox limitation. Change Password light/dark user
   acceptance is deferred under the continuous-run authorization; do not start
   another Public UI family without explicit authorization.
2. Verifier/reviewer: keep approval, regression, and environment limitations
   explicit; do not promote historical evidence to current verification.
3. UI owner: Account overview, Notifications, Guidance/editorial, public Signups,
   Recent Drops, and Tile/Evidence references are now recorded as PUB-REF-10
   through PUB-REF-15. The user approved the generated Public Teams/roster
   composition as PUB-REF-16 on 2026-08-24. Its slightly uneven spacing and
   detached-looking generated DK mark are directional artifacts: implementation
   should integrate the existing Landing-family diagonal artwork, retain real
   event timing, remove team images and role icons, group teams with whitespace,
   and render two draft picks per row at large widths. Pass 3 Signups and Teams
   implementation, current evidence, strict independent review, and bounded
   Teams remediations are complete. Signups is manually approved. The current
   Teams result uses `FINAL TEAMS` and `DRAFT RESULTS` label-owned rules with
   cream dark-mode labels, plus one PICK/TEAM/PLAYER header triplet per
   large-width draft column that collapses to one triplet when narrow. The
   Development reset now publishes frozen roster
   snapshots for every finalized positive fixture while preserving the explicit
   missing-playing-assignment negative; after reset, both test-15 and test-101
   Teams routes are valid manual-review targets. The current correction keeps the
   masthead/logo full-bleed and the roster/draft body separately constrained. The
   masthead owns its full-bleed bottom rule through an out-of-flow pseudo-element;
   the content wrapper does not own or stretch for that rule, and its two
   section-heading rules remain at body-content width. Teams now matches `/Signup`
   exactly with `1rem` metadata top margin and the canonical `1.35rem` masthead
   bottom padding, without duplicated space below the divider. The masthead uses the documented ordinary no-view-navigation top gap
   (`clamp(2.25rem, 5vw, 5rem)`, `2rem` narrow). The masthead artwork itself
   now uses a Teams-local shrink-to-fit composition: the diagonal run, accent
   stripe, light/dark mark size, and crop follow the actual content-driven
   masthead height but never grow beyond Landing's live viewport-clamped geometry.
   The dark Final Rosters kicker is cream, the event H1 no longer adds `TEAMS`,
   and Draft Results now matches Final Teams top spacing with header tracks aligned
   to both row columns; the browser's default ordered-list inset is explicitly
   reset. The Teams art field widens to `43%` and the mark's horizontal crop is
   reduced to `8%`, moving the complete background and lower-height mark materially
   left while retaining the shrink-only cap; team sublabels show formation type only rather than affiliation
   plus formation. Landing itself remains unchanged. Historical first-round reference pictures are not active
   worker/reviewer inputs unless the user explicitly reactivates a named picture.
   Teams received user manual approval on 2026-08-24; preserve the accepted page
   and do not begin another page family without explicit authorization. The shared Pass
   1–3 width regression also
   applied the approved 54rem Standard, 64rem Structured, 88rem Wide, and shared
   responsive-gutter contract while preserving Landing, Login, and 403/404/405.
   Focused source/diff checks and both Release Web builds passed with zero
   warnings or errors. The latest CSS/Razor correction passes focused
   source/cascade and whitespace checks. Its Release build/test was not rerun:
   the user clarified that small visual corrections should not trigger unrelated
   .NET gates or named-pipe escalation unless their actual risk requires one.
   The `/HowTo` guide is implemented and approved in `50077fd`, including its
   localized five-step content contract; F-06 is resolved and does not create a
   future content-replacement gate. The accepted Captain
   focus controls, status totals, complete filtered/paged ledger, scoped
   Captain/co-captain/emergency authority, and PUB-REF-17 detail composition are
   the baseline for the canonical submission workspace. The submission workspace
   is approved and deployment ready; its route/ownership consolidation
   was independently reviewed, remediated,
   manually accepted, and committed in `88cd8f8`. Setup, Change
   Password, Forgot Password, Reset Password, Notifications, and Privacy are
   manually approved. Admin evidence
   review now has its compact queue/detail implementation, rule-based independent
   review, and bounded spacing/action/backdrop remediation complete; Release and
   scoped diff checks pass, with one unrelated stale compact-drop test assertion
   recorded separately. The user marked it deployment ready but not approved on
   2026-08-26. Finalize now
   uses the Admin detail/table system with protected closeout behavior intact;
   Audit now uses the Admin full-width filter/table system with its query contract
   intact. Both completed strict Admin-rule review and bounded remediation, pass
   Release/scoped diff checks; the user marked both deployment ready but not
   approved on 2026-08-26. The Admin
   landing route now intentionally renders a shell-owned WIP surface while
   retaining its prior dashboard markup inertly; the user marked that presentation
   deployment ready. Pass 4 Board behavior corrections and two bounded visual
   remediation cycles are present in the dirty tree, but the user rejected the
   resulting visual composition on 2026-08-24 because it still reads as a legacy
   reskin and its masthead does not match the approved reference. On 2026-08-24
   the user authorized a fresh structural redesign, reactivated PUB-REF-02,
   PUB-REF-03, PUB-REF-04, PUB-REF-14, and PUB-REF-15 for this family, and
   approved replacing the team-board popup with ordinary navigation to the
   existing TeamBoard page at every viewport. Current screenshots are rejection
   evidence only, except that the user explicitly identified the current
   team-overview grid beneath the masthead as already close to target. Preserve
   that grid, integrate it with the corrected masthead, and add the missing
   PUB-REF-02 Recent Activity footer. The structural rewrite must replace the
   rejected masthead and team workspace rather than merely restyling them; tile routes
   still replace the left rail, submission remains its attached drawer, and
   evidence remains a focused modal viewer. A fresh Terra High read-only
   readiness review cleared on 2026-08-24 with no remaining product decision.
   Pass 4 is now bounded as 4A Board masthead plus missing Recent Activity footer,
   4B atomic regular-TeamBoard/popup-retirement/workspace rewrite, and 4C
   secondary Board views. The next action is one fresh Luna High implementer for
   4A only; it must preserve the current overview grid and all team-workspace
   behavior, then stop for current screenshots. Fresh Luna High task
   `01a035c7-c623-7f11-ba2c-29eb38d2cb90` completed 4A on 2026-08-25: only
   `Board.cshtml` and the Board-owned `site.public-ui.css` cascade changed; the
   masthead now has one reference-owned event/status/countdown/leader/metric/
   action/legend hierarchy, and a real three-item Recent Activity footer uses
   the existing approved `RecentDrops` projection beneath the untouched mission
   grid. Focused source/cascade and whitespace checks pass, and the focused Web
   Release build passes with zero warnings or errors. No TeamBoard, Tile,
   popup, drawer, evidence, or secondary-view owner changed in 4A. The user
   rejected the rendered 4A result on 2026-08-25. Current evidence shows a giant
   two-storey title and equal-column dashboard rather than PUB-REF-02's compact
   balanced masthead; it also renders the team count as the reference-like giant
   identity numeral, boxes the page inside the generic 88rem cap, and expands
   Recent Activity into a large section instead of the compact footer strip. The
   user then corrected the Board overview width decision to shared Wide with the
   normal responsive gutter; Landing hero height and artwork remain out of scope.
   Fresh Terra High task `01a035d2-a839-7a03-839b-5075bf37bf71`
   confirmed those blocking findings and required a compact balanced masthead,
   removal of the synthetic team-count artwork, a Board-only Landing-width
   field, and a one-strip Recent Activity footer. Fresh Luna High task
   `01a035d5-8c0f-7750-9181-f4ee02f9e45f` completed only that remediation: the
   invalid team-count artwork is removed, the event/status/countdown/leader/
   metric/action/legend hierarchy is compact, the shared Wide width uses the
   normal responsive gutter, the fact/action rail is content-driven, and Recent
   Activity is a short footer strip.
   Targeted source/cascade and whitespace checks pass, and the focused Web
   Release build passes with zero warnings or errors. After iterative manual
   masthead, overview-grid, metric, divider, and compact Recent Activity
  corrections, the user manually approved the Board overview/Pass 4A on
  2026-08-25. Preserve that approved page. During the subsequent TeamBoard
  manual review, the user found that the already-overhauled ordinary TeamBoard
  route was still being intercepted and rendered through the superseded popup.
  The bounded remediation now removes the Board popup host, interception,
  popup-only restoration scripts/styles, and redirects while preserving normal
  route navigation, the route-owned submission drawer, evidence viewer, nested
  tile routes, and history behavior. The focused ordinary-navigation contract,
  `git diff --check`, and the Web Release build pass with zero warnings or
  errors. Pass 4B TeamBoard, nested Tile view, attached submission drawer, and
  evidence lightbox received user manual approval on 2026-08-25. The temporary
  local boss-art fallback is removed; only the three exact-name local mappings
  remain, while ordinary bosses use their existing authoritative artwork again.
  The current TeamBoard correction removes duplicate participant and focus-operation panels and TeamBoard focus mutation endpoints, while retaining compact active-account/swap context, read-only tile focus projection, explicit Super Admin inspection, and Captain-only focus mutation. Focused source assertions, diff checks, and the requested Web Release build pass. Recent Drops and Leaderboards received user manual approval on 2026-08-26; Leaderboards retains the final Drops-matched standings-heading spacing correction.
  The prior separate Captain team-operations and participant-submission plans
  are superseded by the completed 2026-08-31 canonical-workspace decision
  recorded above.
  The accepted Captain detail composition remains the visual baseline, but
  `/Submissions` and `/Submissions/{id:guid}` now own both roles' overview/detail
  behavior. The Admin Evidence Review linked-resubmission behavior remains
  protected; the relative `_EvidenceUpload` 500 regression is fixed and covered
  by acceptance. The full regression was accepted at baseline `c3e43bb`, and no
  linked-resubmission manual confirmation is not pending. Production launch is
  recorded above; the planned Admin test event remains the next operational
  safety-net step and has not yet run.
4. Packager: stage, commit, push, or deploy only after acceptance and explicit
   authorization.

For later Public UI planning, reuse approved screenshots by visual family rather
than demanding a separate reference for every route. A settings reference may
govern related account forms and states when their composition genuinely matches.
Before implementing a materially different page structure, hierarchy, or
interaction geometry that existing references do not resolve, stop and request a
new picture reference from the user instead of inventing the composition.

The Board behavior approval and replacement-identity decision do not imply
whole-application UI approval. F-04 is resolved by retaining Live identity and
display timezone as read-only. F-06 is resolved by the approved
five-step `/HowTo` guide committed in `50077fd`; no future content replacement
or Rules editor is a current gate. Production launch is recorded above; the
production Admin test event remains the next operational stage and has not run.

Stats highlight trial: replaced lower-left artwork with a royal-blue Most versatile card,
cream typography and a faint existing logo watermark. Sample Maya / The Agency / 14
different tiles represents breadth across distinct tiles, not total submission count.
User also clarified that diagonal rows do not count; production rules must preserve that.
Card is standalone prototype content and awaits visual acceptance.

Stats repeat-drop highlight trial: added a compact muted-gold Keeps on dropping card
beneath Luck, using existing Dragon warhammer artwork and an illustrative 18-drop count.
It represents the most frequently recorded item, with no live data integration yet.

Stats typography refinement trial: standardized main panel headings to uppercase Barlow,
comparison tabs to one type scale, and chart labels/secondary text to consistent Geist
sizes. Increased the smallest team/item metadata for readability and used tabular numerals
for data. Accepted colors, section arrangement and both highlight-card compositions retained.
CSS-only prototype pass; user supplies visual feedback. No app build or browser review.

## WOM remediation checkpoint — 2026-09-22

The authorized Admin-managed WOM implementation remains in the isolated checkout
`/private/tmp/BingoWebpage-wom-managed-20260922` on branch
`codex/admin-wom-competitions`, based on exact commit
`ae1a8605372a613e53d87b73a8e0237c84c1faca`. The restored leaderboard baseline is
the separately recorded patch
`/private/tmp/bingo-boss-leaderboards-20260917/final-visual-corrections-with-safari-paint.patch`
with SHA-256
`22b2950c8ea05ae30d708dadbf6588684bb776a7c03e1766cbae8aaaee033bf3`. The baseline-only
comparison checkout is `/private/tmp/BingoWebpage-wom-baseline-20260922`.
No commit, staging, push, merge, deployment, application restart, production database
mutation, or real WOM write was performed.

The current WOM/authority delta is the 42-row manifest
`/Users/christopher/.codex/visualizations/2026/09/17/01a0b085-75ee-79b0-b40b-bba65b669d74/wom-implementation-manifest-20260922.txt`
with SHA-256
`7f5110ca615288987cc668907cf069bafbfba79cdd1ebd843e00f1bf71fec20c`.
It excludes this status file. Baseline evidence remains in
`/Users/christopher/.codex/visualizations/2026/09/17/01a0b085-75ee-79b0-b40b-bba65b669d74/wom-baseline-evidence.md`;
the independent review remains at
`/Users/christopher/.codex/visualizations/2026/09/17/01a0b085-75ee-79b0-b40b-bba65b669d74/wom-independent-review-20260922.md`.

The named review findings were remediated in the active diff: managed schedule saves
retain the manual-link five-minute rule while allowing active managed windows; Create
receipts seed remote and acknowledged-roster baselines; Unknown and expired Sending
operations have durable bounded read-only reconciliation, while an unresolved Create
remains persisted and blocks any second POST; Create/Update/Delete dispatches
revalidate current authority, state, version, fingerprint, source and shared-reference
conditions; stale known Updates rebuild the current desired projection and the Live
boundary sends current dates-only state; competition-scoped PostgreSQL advisory locks
serialize manual linking with managed provider writes and deletes; roster-bearing
automatic updates refuse shared remote IDs; receipt persistence retries reload mutable
records; dates-only PUT payloads omit roster fields; provider credentials are protected
before the application result and provider error text is redacted; metadata-only title
edits use the synchronization metadata path and preserve the existing stats assignment
fingerprint, generation and cadence; and managed validation/recovery feedback is
localized through stable codes or safe localized fallbacks. Provider-ID rename
recognition remains explicitly omitted.

The latest named P1 is also remediated: `EventCompetitionSynchronizationService`
now acquires the requested competition's PostgreSQL advisory lock before the manual
provider GET and holds it through the serialized local save. The two-context
integration case changes the provider title and window during the managed write,
proves the manual read remains blocked until that write releases the lock, and then
proves the manual link safely refuses the current window mismatch rather than saving
stale metadata.

Final remediation checks passed:

- `dotnet build Bingo.slnx --configuration Release --no-restore` — passed with 0 warnings and 0 errors.
- `WiseOldManCompetitionRulesTests` — 5/5.
- `EventCompetitionSynchronizationTests` — 2/2.
- `ManagedCompetitionUiTests` — 2/2.
- `Slice10Pass101WiseOldManTests` — 13/13.
- `SaveScheduleAllowsManagedWindowChangesBeyondFiveMinutesForAutomaticUpdate` — 1/1 against disposable PostgreSQL/Testcontainers data.
- `EventCompetitionManagementIntegrationTests` — 5/5 against disposable PostgreSQL/Testcontainers data, covering persisted Unknown Create blocking, 429/edit convergence, stale retry/current Live payload and fingerprint, the two-context competition-reference race with changed provider title/window and safe manual refusal, and receipt-save retry.
- `git diff --check` — passed.

The existing full `Slice10Pass102CompetitionSynchronizationTests` evidence remains
143/143 from the prior stable run and is reused for unrelated synchronization behavior.
A post-remediation rerun of that class was stopped after roughly five minutes with no
test output while its test host remained active; it produced no test result and is
recorded as an infrastructure timeout rather than a product verdict. The directly
affected managed schedule case passed independently before and after the final edits.

The first independent Sol/high review and its same-reviewer recheck returned CHANGES
REQUIRED with the named findings recorded in the reports above. The implementation is
now stable after named-only remediation and the focused checks above. The exact next
step is a named-only recheck by the same reviewer task
`/root/wom_competitions_orchestrator/wom_competitions_review`; reviewer dispatch/recheck
routing is pending because this worker currently has no callable `followup_task`
collaboration tool. The parent orchestrator must wake that same reviewer with the
checkout, manifest, reports, and results in this checkpoint.
The latest recheck report is
`/Users/christopher/.codex/visualizations/2026/09/17/01a0b085-75ee-79b0-b40b-bba65b669d74/wom-independent-recheck-20260922.md`
with SHA-256
`70b802ff04ee5559222b7417292c800333ec31c477412dda355d7d8574465719`.
Manual EN/DA visual acceptance remains pending; no publication claim is made.

## Named recheck follow-up — 2026-09-22

The same-reviewer recheck report at
`/Users/christopher/.codex/visualizations/2026/09/17/01a0b085-75ee-79b0-b40b-bba65b669d74/wom-independent-recheck-20260922.md`
has SHA-256
`70b802ff04ee5559222b7417292c800333ec31c477412dda355d7d8574465719`. Its remaining
P1 is remediated in exactly these files: `src/Bingo.Infrastructure/Events/EventCompetitionSynchronizationService.cs`
and `tests/Bingo.IntegrationTests/EventCompetitionManagementIntegrationTests.cs`.
Manual linking now acquires the requested competition advisory lock before provider
validation and retains it through the serializable local save. The two-context test
changes provider title/window during the managed write, asserts the manual provider
read is blocked, then asserts the manual link safely refuses the current window
mismatch. The active source hashes are `1646aea2eae61bbc1229b6b104e357f19bd46b5fd47fbeb73c2cb7035a546fea`
and `811d3397f0bc54c9a2d19e3d73463a3bed3fe443dc2b7a272e7aed9949f5accb`.

The bounded validation passed: Release build 0 warnings/0 errors; disposable
PostgreSQL `EventCompetitionManagementIntegrationTests` 5/5; domain
`EventCompetitionSynchronizationTests` 2/2; and `git diff --check`. The refreshed
42-row WOM manifest is mirrored at `/private/tmp/wom-implementation-manifest-20260922.txt`
and its evidence copy has SHA-256
`4cbc1dd21d9d6fbe7993f046d27fff4fe2043cf0c8ad280e466232d56b29a7be`. No commit,
stage, push, deployment, restart, production DB write, or real WOM mutation occurred.
The parent orchestrator is the next owner to route the same Sol/high reviewer for
named-only recheck. Manual EN/DA visual acceptance remains pending; rename remains
omitted; no publication claim is made.

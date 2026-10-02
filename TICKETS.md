> Historical pre-simplification ticket ledger; not an active queue or authority.
> Preserve recorded outcomes as evidence. Later simplification and approved UI-era
> changes supersede conflicting requirements; use PRODUCT_REQUIREMENTS,
> FUNCTIONAL_CONTRACTS and DELIVERY_PLAN. Model/routing/authorization statements
> below applied to those old assignments only and do not dispatch new work.

# Application sweep tickets

## Historical workflow — temporary Sol/Luna reinstatement, 2026-09-16

**The Astra workflow recorded below is CURRENTLY SUPERSEDED and INACTIVE.
Its original text is preserved for reference, not execution.** Follow
[the active model workflow](AGENTS.md#active-temporary-solluna-workflow--2026-09-16):
Luna Max implements, corrects and runs focused checks; one fresh Sol High reviewer
reviews only after implementation is complete. Astra is reserved for a concrete
escalation. No separate routine verifier; reuse passing checks and recheck named
corrections. Applicable final release gates remain required.

Use collaboration subagents; visible tasks are not required. After dispatch, end
the turn without periodic waiting/polling. Findings and named rechecks stay between
implementer and reviewer. Every worker sends the planner one brief update when it
ends a turn, including review/recheck handoffs, and reports genuine questions or
blockers. Handoff updates do not imply a passed review. The planner handles the
update and ends its turn again; no active waiting. Include the exact originating
task ID and callback route in briefs,
as specified in AGENTS.md. This replaces the briefly proposed visible-task requirement.

This temporary policy overrides historical assignments throughout this ledger.
It does not reopen completed/deferred tickets, resume continuous execution or
authorize publication. CURRENT_STATUS.md owns the current authorized assignment.

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


Created 2026-09-09 against `admin-consistency` at
`86e7dc3f5910cd332123c5e951b1c0b4eb75c654` in
`/private/tmp/BingoWebpage-admin-popup-recovery-20260908`.

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

## Finalization freshness batch C33 — completed contract/history, 2026-09-14

C33 creation accepted on local host with queued client ID
`client-new-thread:4efdf848-10cb-4f33-8262-e614e28d652f`; saved project
`local-7212f354a970dffa7d8b5520bdec2dfc`, gpt-6-astra/xhigh. Actual runtime ID and
checkout await setup; do not use the queued client ID for runtime tools. No reviewer yet.

C20 final PASS is recorded. C33 is the next eligible correction under standing continuous
execution. C36 is closed without implementation; C05/C09 retained-data obligations are explicitly deferred; their reviewed fixes await manual acceptance; C26/D03 closed; C40/D05 deferred. Do not add those tickets to this batch.
Implement C33 only in a new saved-project isolated worktree, direct gpt-6-astra/xhigh,
then exactly one fresh saved-project independent gpt-6-astra/xhigh reviewer.
FUNCTIONAL_CONTRACTS 7.6 / DATA_MODEL 17.1 freeze the technical invariants: existing
event version advances atomically with relevant review mutations; completion inspection
has a non-reusable per-team/cycle competitive-input identity using existing immutable
mutation facts. Preserve older acknowledgments/history, detect change-and-return, avoid
self-invalidation by acknowledgment, and retain unaffected team inspections. Bind actual
rendered forms to the inspected input and reject stale attempts before partial mutations.
Record the concrete identity inputs in the existing local owner before implementation;
ordinary schema-free details are delegated. Consequential ambiguity/new persistence goes
to planner after one focused lookup. No preliminary reviewer or redesign.

Starting boundary: Submission review transactions, EventFinalizationService readiness/
acknowledgment/correction/finalize and direct Razor binding/integration. Preserve guards,
review reasons/confirmation, history, snapshots, scoring/privacy and transaction lock order.
Execute real PostgreSQL/authenticated HTTP inspect -> review approval/reversal affecting
completion/rank -> stale finalization/inspection denial -> reinspection -> successful
finalization, including complete/incomplete/complete in one cycle, unaffected team,
duplicate/stale acknowledgment, concurrent review/finalize and atomic fault rollback.
Use controlled disposable fixtures, applicable build/format/diff only; reuse evidence.
No user-data scan/repair, previous-result republication, full-suite/browser loops by default.

Fifteen reviewed batches remain isolated/unmerged, particularly C20/C31/C32/C35 and
C04/C15/C22 overlaps. No source import or assumption their fixes exist in baseline c165bbc.
Bootstrap central AGENTS/TICKETS only and focused C33 authority clauses locally. Give
reviewer actual checkout/full tracked+untracked diff plus immutable evidence manifest.
Implementer review/recheck ONLY to reviewer; required findings/incomplete ONLY to
implementer; FINAL PASS ONLY to planner 01a09c57-1f38-7191-94de-e11647ffbcf1.
Standing user authorization covers private paths/diffs/hashes/evidence for exact assigned
tasks. One bounded named remediation/SAME reviewer recheck; no repeated handoff
approval, duplicate planner checkpoint, subagents or model substitution. Rejected action
stops without bypass/retry. Manual acceptance remains separate. No staging/commit/push/
PR/merge/deploy, source integration, user-owned DB mutation or running-app restart.

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

## Objective identity batch C20 — approved contract and completed execution

Implementer `01a09fbe-a7da-7d02-890f-559b0860920f`, isolated saved-project checkout
`/Users/christopher/.codex/worktrees/51a1/BingoWebpage`, branch
`codex/objective-identity-c20`, base/HEAD `c165bbc`, gpt-6-astra/xhigh. No reviewer yet.

**Recovery approved — 2026-09-14:** User explicitly approved the explained confirmed
Discard private correction action. All unpublished edits in that correction are discarded
only after clear confirmation; working board restores from current active publication
with exact identities, correction closes and normal correction can restart. Publication,
evidence/progress/history remain intact. Owning scope: FUNCTIONAL_CONTRACTS 6.2 and
PRODUCT_REQUIREMENTS 10.2. Existing implementer continues bounded implementation and
focused recovery checks, then the one independent review. Reuse unaffected core evidence;
no extra readiness review or scope discovery. No historical repair/source integration.

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

**C20 persistence resolution — 2026-09-14:** Planner authorizes the implementer's
schema-free retention of original approved requirement-drop rows as immutable identity
links. DATA_MODEL 10.6 owns the bounded contract. Exact scoped association only,
missing/ambiguous fail closed, snapshot-authoritative rules and consistent transaction/
lock ordering; no duplicate/reused identity for substantive replacement or historical
reconstruction. Execute private no-evidence removal/replacement -> new active-publication
submission -> blocked incompatible replacement publication, plus stable wording IDs and
failure/concurrency proof. No new preliminary review or user decision is needed.

User approved the explained evidence-lock recommendation. Implement C20 ONLY; C33
freshness and C36 legacy audit/integration remain separate. FUNCTIONAL_CONTRACTS 6.2
and PRODUCT_REQUIREMENTS 10.2 now freeze the policy: wording-only corrections preserve
objective/drop IDs, contributions and evidence; any submitted evidence locks substantive
requirements/scoring and removal, regardless of later status. No-evidence objectives
remain editable. Private corrections preserve ordinary access/submission against active
publication, with authoritative checks for evidence arriving during editing/publication.
Keep prior approval history and reject stale/forged/concurrent changes atomically.

Use one new saved-project worktree and direct gpt-6-astra/xhigh implementer, then exactly
one fresh saved-project gpt-6-astra/xhigh read-only reviewer. No preliminary reviewer,
subagents, verifiers or model substitution. Bootstrap only central AGENTS/TICKETS;
promote only the focused approved clauses above locally. Source base last verified
c165bbc. Existing Board editor/approval snapshot/PublicBoardService/Submission consumers
are the starting boundary; reuse current mechanisms. Preserve architecture, authorization,
confirmation/reason, transaction/concurrency, privacy, scoring/history and accepted UI.
Ordinary technical choices are delegated; a necessary new persistence mechanism or
consequential ambiguity after one focused lookup goes to planner before dependent work.

Execute focused real PostgreSQL/authenticated HTTP: approved and pending evidence ->
private wording correction -> ordinary detail/submit/progress -> replacement publication;
unchanged objective/drop identity and history; blocked rule/removal attempts for retained
evidence states; no-evidence valid changes; new evidence racing an edit/publication;
stale/unauthorized/invalid attempts with no partial state. Exercise directly affected
browser behavior only if client interaction changes require it. Applicable build/format/
diff checks, no full suite/screenshots/manual walkthrough by default. Record actual
tracked and untracked diff, focused commands/results and immutable evidence manifest.
No retained-data scan/repair or invented association reconstruction.

Fourteen prior source batches are isolated/unmerged. C19/C21/C22 and C24 Admin Board,
C28/C29/C30 submission consumers and C31/C32/C35 final-review overlaps must remain
explicit; no source import, combined integration or duplicate broad repairs. A concrete
prerequisite goes to planner. Do not silently assume accepted fixes are in this base.
Review once, allow one bounded named correction and SAME-reviewer recheck; reuse
unaffected evidence. Implementer sends review/recheck ONLY to reviewer. Required
findings/incomplete ONLY to implementer; final PASS ONLY to originating planner
01a09c57-1f38-7191-94de-e11647ffbcf1. No checkpoint or reviewer-dispatch message to
planner. User explicitly authorizes necessary private paths/diffs/hashes/evidence
between these assigned tasks; no repeat permission request. Rejected action stops,
no alternate-tool bypass/retry; report the specific block. Worker stops at handoff.
Planner records final pass as Awaiting manual acceptance and continues eligible work.
No stage/commit/PR/push/merge/deploy, source integration, user database mutation or
restart of the running user app. New UI states remain awaiting manual acceptance.

## Ledger handoff — 2026-09-13

Latest result: **C11 passed final independent review** after the same-reviewer F01/F02
recheck and is **Awaiting manual acceptance**. Actual checkout
`/Users/christopher/.codex/worktrees/c4b5/BingoWebpage`, branch
`codex/finalized-prelive-roster-c11`, unstaged/uncommitted/unintegrated. Running/Paused
departures remain explicitly deferred. C20 implementation task is queued; its objective-change policy is now approved.
See active C20 contract above.
Total: **50/53 handled = 39 Awaiting manual acceptance + 6 Done + 5 closed without
change**; other statuses: 1 In progress, 2 Deferred. Prior assignment notes are
historical; CURRENT_STATUS owns current evidence and worker routing.

Current assignment: C19/C23/C25 in a NEW BingoWebpage project worktree, branch
`fix/board-approval-ticket-batch`. The implementer records the generated checkout
path and owns local execution outcomes until handoff. Older active notes are history.

Current assignment: C13/C14/C17 in
`/private/tmp/BingoWebpage-participant-flow-ticket-batch-20260913`; that copy owns
execution outcomes until handoff. Earlier active-batch notes below are provenance.

Current C01/C02/C03 execution is owned by the copy in
`/private/tmp/BingoWebpage-event-setup-ticket-batch-20260913`. Previous batch
references below are provenance; the current contract and handoff take precedence.

Active C12/C16/C18 execution is owned by the copy in
`/private/tmp/BingoWebpage-roster-ticket-batch-20260913`. The account batch
C06/C07/C08 has cleared independent review; its implementation remains isolated.
The older account-specific handoff below is provenance, not the current assignment.

Recovered from the original checkout above into
`/private/tmp/BingoWebpage-drop-announcements/TICKETS.md` so the backlog is available
with the active repository. The original copy is historical. Active C06/C07/C08 execution uses the ledger in
`/private/tmp/BingoWebpage-account-ticket-batch-20260913`; reconcile its reported
outcomes back into the shared ledger after the batch.
The workflow below is updated; ticket dispositions, outcomes and source references
are preserved as recorded on 2026-09-09. Reconcile selected tickets against current
code and subsequent merged fixes before resuming them; no new implementation or
status transition is claimed by this documentation update.

## Purpose and authority

This is the backlog from the eight-unit application sweep, including accepted dispositions:
**41 correction tickets, 7 authority/decision tickets and 5 test tickets**.
C41 is a subsequent user-reported public font-loading ticket, separate from the
original 45 numbered sweep findings.
All 45 numbered findings map exactly once to a primary ticket; related impacts are
cross-references, not additional untracked fixes. U4-07 is owned by D03 until the
import product decision is made. Authority questions and all nine failed test-file/
case dispositions are also accounted for.

The user authorized the dispositions recorded below and the corresponding documentation
reconciliation and the first execution trial below. **The index records current
status; CURRENT_STATUS records active worker ownership.** Priority labels preserve
reviewer severity; they are not deployment urgency or proof of a reproduced incident.
Source-established findings, executed tests and unresolved contract questions remain
distinct. Do not convert a proposal into a new product rule merely by assigning it.

- TICKETS owns these bounded briefs, dependencies, acceptance criteria and ticket
  status. The index below is the single ticket-status record; update it in place.
- [DELIVERY_PLAN section17](DELIVERY_PLAN.md#17-application-journey-sweep--approved-2026-09-09)
  owns approved execution order/gates; [CURRENT_STATUS](CURRENT_STATUS.md) holds only
  active ownership, blockers and next action. A ticket approval must be explicit
  and may cover a coherent batch; routine implementation choices need no new approval.
- FUNCTIONAL_CONTRACTS/PRODUCT_REQUIREMENTS, DATA_MODEL/TECHNICAL_ARCHITECTURE and
  UI_SYSTEM/UI_PAGE_MATRIX retain their existing behavior, data and UI authority.
  This file is a repair backlog, not a replacement application specification/Atlas.
- The sweep evidence/index remains in DELIVERY_PLAN17.8. Detailed temporary reports
  are linked per ticket as supplementary evidence; the problem, scope and acceptance
  below must remain usable if those temporary artifacts disappear. Source references
  are starting points at the recorded commit, not exhaustive permitted-file lists.

## Execution and completion rules

Statuses: **Proposed → Ready → In progress → Review → Awaiting manual acceptance → Done**. Use **Blocked** with
an exact dependency/action; use **Deferred** or **Closed — no change** only with a
recorded decision. Ready means scope/decisions/dependencies and execution approval
are settled. Done requires recorded evidence, not simply a patch or passing review.
The user approved starting tickets under the batch workflow on 2026-09-09. Batch 1
is C05 then C09, frozen in DELIVERY_PLAN17.12. Further batches require their own
recorded membership and execution boundary; this is not blanket approval of every
Proposed ticket or unresolved product decision. Record
approval/date, owner, check results, review and remaining manual/data limitations in
each ticket's Outcome when work begins/completes.

Use existing implementation and one independent review per coherent batch against
every included ticket's final scope, including interactions between the fixes. Follow
the approved role/model assignments below. The implementer works directly; do not
add a planning reviewer to every small correction.
A major functional addition follows the existing one-readiness-review rule after its
behavior is agreed. C11/C20/C27/C33/C38 call out consequential scope questions. Freeze the bounded
contract before dispatch; only unresolved product or material persistence choices
need user direction. Ordinary implementation details stay with the worker. This
is not permission for five broad application audits.

- Prove the changed boundary with the focused checks below. Where a behavior test
  is needed, make it fail for the reported defect before the fix when practicable;
  do not add duplicate reproductions solely to corroborate an already clear finding.
- Preserve authorization, privacy, atomicity, deterministic behavior, history and
  current accepted UI composition. No generalized framework, unrelated cleanup,
  historical-reference visual review or separate no-JavaScript parity work.
- Freeze material behavior/schema/route/retention changes in the relevant ticket and
  owning authority before implementation. A missing proof is a limit, not a pass.
- Run controlled disposable database/storage/provider scenarios. No user DB mutation,
  HTTPS7131 restart, commit/push/merge/deployment or live external messages are implicit.
- Existing-data notes identify obligations, not authorization to scan or repair
  production. If retained-data repair is necessary, split and approve that action;
  do not close the obligation as fixed merely because new writes are corrected.
- A ticket is complete when acceptance, focused checks and required review clear,
  required manual acceptance is recorded, and data obligations are resolved or
  explicitly deferred by the user. Report unrelated discoveries without expanding it.
- The full baseline was already run once: build/full format passed; C# project runs
  756 pass/3 fail, Node14 files pass/6 fail. Repair failed tests with their accepted
  invariants; later assertions in early-failing files remain unexecuted. Run broader
  final integration gates once for the approved combined change set, not per ticket.

## Deferred manual acceptance — approved 2026-09-13

The user will perform the combined manual walkthrough after all ticket implementation
and independent review work is complete. Do not pause each reviewed ticket for that
walkthrough or schedule another AI review/test run merely to change its status.

Use **Awaiting manual acceptance** only when required implementation, focused checks
and independent review are complete with no required remediation or other unresolved
ticket blocker. Record any actual code, test, decision or required data obligation
separately; do not conceal it under this status. Done follows user acceptance.
Package-level combined regression/release gates and commit/merge/deployment authority
remain separate; this status does not claim they have run or authorize publication.

C06/C07/C08 and C12/C16/C18 now meet this implementation/review checkpoint. Manual
acceptance is their remaining ticket acceptance step, deferred by the user. C12's
proposed retained-data inspection/recovery and known retained-row read limitation
remain explicitly recorded; no production scan or repair is claimed or authorized.
C05/C09's separately blocked data obligations are unchanged. This decision does not
by itself authorize another batch; resume only the user-assigned scope.

## CURRENTLY SUPERSEDED — Approved ticket workflow — 2026-09-13

**INACTIVE FOR NOW: this entire former workflow, including its continuous
execution, model, dispatch and routing instructions, is retained as historical
text. Its claims to replace or supersede other workflows are themselves
SUPERSEDED by the active 2026-09-16 policy above. Do not execute it unless the
user explicitly reinstates it.**

### Continuous planner execution — authorized 2026-09-13

**Resumed 2026-09-14 by explicit user instruction**, with the same standing approvals
and corrected routing. The overnight pause is over; after each passed batch, reconcile
it and dispatch the next coherent eligible batch unless user attention is required.

The user directs the planner to keep working through the existing ticket list:
receive each passed batch review, reconcile the central ledger/evidence to Awaiting
manual acceptance, select the next coherent eligible batch, and dispatch a fresh
visible saved-project Astra xhigh implementer under the established workflow.
No further per-batch user approval is needed. This is explicit standing authorization
for successive implementer tasks and each brief's one fresh Astra xhigh reviewer,
including bounded named remediation and same-reviewer recheck. Do not launch another
batch merely on implementation completion; wait for the independent verdict.

This supersedes older instructions requiring a new user instruction after each pass
or saying no automatic next batch, for the planner only. Workers still stop at their
assigned boundary: implementer review/recheck request ONLY to reviewer; reviewer pass
ONLY to planner, remediations/incomplete ONLY to implementer. No implementer checkpoint
or dispatch notice to planner. Existing handoff approvals stand.

Involve the user only for a genuine blocker, consequential scope/product decision,
approval outside existing authority, or other necessary attention. Resolve routine
technical choices independently and progress independent eligible work where possible.
Do not treat unresolved decisions as approved, or silently include Blocked, Deferred
or Closed tickets. Manual acceptance stays user-deferred until implementation/review
work finishes. This does not authorize combined integration/release work, packaging,
commit/push/PR/merge/deployment, running-app restarts or real-data repair. Once no
eligible work remains, report the remaining decisions/blockers and acceptance needs.


This user-approved Astra xhigh trial replaces the 2026-09-09 Sol/Luna batch
workflow. For these tickets it supersedes conflicting model, delegation and
orchestration instructions in AGENTS, DELIVERY_PLAN, CURRENT_STATUS or historical
outcomes. Product scope, acceptance criteria and required release gates still apply.

| Role | Model / reasoning | Assignment |
| --- | --- | --- |
| Implementer / remediator | `gpt-6-astra` / xhigh | Directly implement the assigned ticket or coherent batch, run focused checks and fix valid findings. |
| Independent reviewer | `gpt-6-astra` / xhigh | In a fresh task, review the complete agreed diff against the final ticket scope and relevant behavior. Remain read-only. |

There is no separate execution manager or routine verifier. Do not automatically
spawn subagents or worker tasks, including Luna, Terra or Sol. Delegation or new
task creation requires an explicit user request. Set the approved model/reasoning
explicitly when dispatch is authorized; do not silently substitute a model or
reasoning level. An implementer may create one fresh Astra xhigh reviewer task after completion
only when the user explicitly authorizes that handoff, as for the explicitly authorized batches
below. This never permits implementation subagents or model substitution. Historical
worker names and models below record completed work, not future assignments.

### Project organization — future tasks

User correction, 2026-09-13: create all future implementer/remediator/reviewer tasks
under the saved **BingoWebpage** project, identified through list_projects. Do not
choose a projectless target solely to use an isolated checkout. Implementation
remains isolated in a worktree; a reviewer must inspect the exact implementation
checkout/diff, not assume its own initial checkout contains those changes. This
supersedes projectless examples in older handoffs for future creation only. Do not
move existing tasks or change their code/checkouts as part of this preference.

### Review delivery — applies to the next and subsequent batches

User correction, 2026-09-13, applies to the current and future ticket batches:

1. Implementer sends the review request, complete diff and evidence **only to its
   assigned reviewer**, then stops. No implementation checkpoint, dispatch notice
   or duplicate handoff is sent to the planner.
2. Reviewer sends a **passed review only to the originating planner**. Required
   remediations or incomplete/blocked review go **only to the implementer**.
3. Implementer performs authorized named remediation and sends the recheck request
   **only to the same reviewer**, then stops. The same verdict routing applies.

Verify exact destination IDs and include this sequence in task briefs. Sending
an implementation checkpoint to the planner is not an exception merely because it
is not a review verdict. Prior contrary handoff language is superseded. Scope,
bounded remediation and acceptance gates remain unchanged.

### Standing handoff authorization — clarified 2026-09-13

The user explicitly authorizes batch-relevant handoffs among the assigned implementer,
its assigned reviewer and originating planner without further user approval requests.
This covers repository paths, branches, diff hashes, findings and check/evidence results
needed for implementation, review, named remediation/recheck and central reconciliation.
Verify exact destination IDs and carry this authorization into future briefs. Preserve
findings/incomplete-verdict-to-implementer and pass-to-planner routing; never dual-deliver
a verdict. It does not authorize unrelated recipients, another batch or publication.
If automatic approval review still rejects a handoff, stop that action and record/report
the block once; do not bypass it, retry or ask the user again for this standing scope.

### Assignment and review cycle

1. **Select the bounded work:** record approved ticket IDs, dependency order,
   protected behavior, acceptance criteria, relevant checks and stop boundary.
   Reuse existing briefs; do not repeat the sweep or replan the entire backlog.
   Record the checkout, starting diff baseline and unrelated existing changes.
2. **Implement directly:** Astra xhigh completes the assigned ticket or coherent
   batch and runs the smallest checks that protect the changed behavior. Do not
   add a full-suite run, screenshot loop or browser walkthrough by default.
   Existing required acceptance and release checks remain applicable.
3. **Record the checkpoint:** update each ticket's Outcome with changes, actual
   check results and remaining limitations. Implemented does not mean independently
   reviewed or Done. Stop for material product/scope decisions or genuine blockers;
   do not add workers or retry loops to conceal them.
4. **Review once at the completed ticket or coherent PR-batch boundary:** use a
   fresh Astra xhigh review task when authorized. Supply the final scopes, complete
   base-to-current diff and focused verification evidence. Review included fixes
   and their interactions; no separate review for every intermediate edit.
5. **Fix valid findings:** the Astra xhigh implementer checks reported issues and
   applies only required corrections. Rerun affected checks and recheck unresolved
   findings when necessary; do not restart broad review or implement optional ideas.
6. **Close and hand off:** record acceptance, remaining manual/data obligations and
   the next permitted action. Compare total usage and correction effort through
   acceptance for the trial. Do not automatically expand to the entire backlog.
   Run applicable broader final gates for the agreed combined change set.

### Resume with a new task

Read AGENTS, the active CURRENT_STATUS handoff, this workflow and selected ticket
briefs/relevant DELIVERY_PLAN sections. Preserve existing evidence and outcomes.
Record a compact handoff:

```text
Checkout / branch / starting diff baseline and pre-existing changes:
Approved ticket IDs, scope, order and stop boundary:
Implementer or reviewer task ID, role, explicit model/reasoning and ownership:
Completed implementation / executed checks / independent review / manual acceptance:
Unresolved named findings, blockers and existing-data obligations:
Evidence paths and complete review diff boundary:
Exact next permitted action or required user decision:
```

Check any recorded active task before starting replacement work. Do not infer
permission to delegate from this handoff or from the old manager/worker workflow.
Deferred/closed tickets remain excluded, including the separate contract stop
before the C40 Audit overhaul. No production data repair, app restart, packaging,
publication or deployment is authorized by this workflow.

## Active account batch — approved 2026-09-13

User authorized selecting a suitable batch and handing it to one Astra xhigh task
which reports back when the whole batch is finished. Approved membership is
**C06 → C07 → C08**, limited to the ticket briefs and acceptance criteria below.

- Checkout: `/private/tmp/BingoWebpage-account-ticket-batch-20260913`.
- Branch: `fix/account-ticket-batch`; fetched `origin/main` base
  `c165bbcb321547637d03b4e9dc3d2e206e5944b3`. The code checkout was clean before
  this planning handoff. Initial uncommitted edits are AGENTS, CURRENT_STATUS,
  DELIVERY_PLAN and this newly recovered TICKETS ledger; preserve them.
- Role: implementer/remediator, `gpt-6-astra` / `xhigh`. Work directly without
  subagents, managers or delegated verifiers. Ponytail was uninstalled at the
  user's request; do not invoke it or apply its prior skill instructions.
- C06 and C07 share Settings/Discord callback authentication and persistence;
  C08 completes this account-integrity batch with stale My Accounts edit protection.
  Source inspection at this base confirmed BeginLink uses an unpopulated POST
  property, linking creates a Discord principal, existing Discord login has no
  RecordLogin call, and MyAccounts UpdateAsync has no expected-version argument.
- Preserve session invalidation, callback intent/collision/replay guards, owner
  authorization, signup/history boundaries, EN/DA feedback and existing UI layout.
  Extend existing account/link versions and authentication persistence paths.
- Starting sources: Account/Settings.cshtml.cs, DiscordCallback.cshtml.cs,
  MyAccounts.cshtml(.cs), Security/MyAccountsService.cs and existing account
  authentication/principal services. Follow direct callers and necessary tests;
  this is not permission for adjacent feature work.
- Contract references: FUNCTIONAL_CONTRACTS 5.1, 5.2 and 9.4, applicable identity
  and session sections of TECHNICAL_ARCHITECTURE, and existing Account/link
  invariants in DATA_MODEL. Read only relevant sections.
- Complexity budget: no new tables, services, routes, policies, jobs, dependencies
  or generalized abstractions are expected. Existing version fields should suffice;
  stop for a concrete need to expand this budget or change approved product rules.
- Verification: controlled Settings POST/callback and successful/rejected login
  scenarios; sequential stale/current My Accounts edits and wrong-owner denial.
  Reuse existing identity integration fixtures and focused tests. Use disposable
  test resources only; never real Discord or user-owned databases. Run relevant
  compile/format/diff checks. No default full-suite or screenshot/preflight loop.
- Stop after implementing all three tickets and their focused verification, or on
  a genuine blocker. Record actual check results and unresolved limitations per
  ticket. Leave successful implementation at Review, not Done: a fresh independent
  Astra xhigh review is a later assignment. Do not start that review yourself.
- Report back to the originating task through the normal completion return or
  available task-coordination mechanism; include all three outcomes, checks,
  exact checkout/branch/base, blockers and files for review. Do not message people
  or unrelated tasks. Do not commit, push, open a PR, deploy, restart the user's
  app, repair historical data or begin another ticket.

## Active roster batch — approved 2026-09-13

The user authorized a new Astra xhigh batch and explicitly instructed the implementer
to send the completed work to a new Astra xhigh reviewer task itself.
Approved membership: **C12 → C16 → C18**. The briefs below own acceptance criteria.

- Checkout: `/private/tmp/BingoWebpage-roster-ticket-batch-20260913`.
- Branch: `fix/roster-ticket-batch`; base `origin/main` at
  `c165bbcb321547637d03b4e9dc3d2e206e5944b3`. Code started clean; initial planning
  changes in AGENTS, CURRENT_STATUS, DELIVERY_PLAN and TICKETS are intentional.
- This checkout excludes the uncommitted account and announcement implementations.
  Prior ticket outcomes are evidence from their named checkouts, not proof that
  those changes are in this branch. Do not import or reimplement them here.
- Source inspection confirmed C12 MoveMember loads source membership/team without
  route-event ownership checks, C16 finalization lacks the start-time Captain check,
  and C18 live replacement chooses the first Playing assignment by registration order.
- Implement C12's same-event membership/team/participant/target and lifecycle guards
  before mutation. Preserve authorized same-event correction and publication/history.
- Implement C16's current Captain check at finalization and usable correction route.
  Blocked finalization must leave no publication residue. Keep account-usability
  checks as the separate event-start rule; do not invent another Captain policy.
- Implement C18 using the saved authoritative system primary for prospective live
  activation, independent of registration order. Preserve secondary/informational
  roles, historical eligibility and evidence. Use the normal signup-edit journey
  in the regression; the separately recorded C05 My Accounts answer defect remains
  outside this batch and must not silently expand it.
- Starting files: Admin/Events/Draft.cshtml.cs, SignupService.ReplaceVacancyAsync,
  existing primary-character query and relevant roster/draft/signup integration tests.
  Authority: FUNCTIONAL_CONTRACTS 2.2, 5.6, 6.1 and directly applicable data/lifecycle
  invariants. Follow direct dependencies only as necessary.
- Complexity budget: no new tables, services, routes, policies, jobs, dependencies,
  generalized abstractions or migrations expected. Reuse existing queries,
  transactions, validation and localization. No UI redesign or historical repair.
- Focused verification: authenticated cross-event rejection with both events
  unchanged plus a valid move; start/demote/blocked finalize/reassign/success with
  no failed-publication residue; edited primary A/B to C followed by replacement
  selecting C with historical credit unchanged. Use existing disposable fixtures;
  run relevant compile/format/diff checks. No full-suite or screenshot/preflight
  loop by default. Preserve applicable final release/manual gates.
- One implementer (`gpt-6-astra`, `xhigh`) works directly, with no implementation
  subagents, manager or verifier tasks. Ponytail is removed and must not be used.
- After the whole batch and focused checks finish, freeze the diff and create
  exactly one NEW visible independent reviewer task (`gpt-6-astra`, `xhigh`).
  This is the explicitly authorized exception to the no-delegation default.
  Reviewer role is read-only; review the complete base-to-current diff, all new
  files, final ticket scope and existing evidence. Do not rerun passing checks
  merely for confidence or start another readiness review. No reviewer subagents.
- The user authorizes sharing this batch's local paths, ticket scopes, test evidence
  and findings between implementer, this new reviewer and originating planner
  `01a086a5-788c-7600-89ef-b24de23bd1fd`. Include the verified implementer task ID
  in the reviewer prompt for return delivery; never infer another destination.
  Ask the reviewer to send its final report to the implementer and originating
  planner, and return its own normal completion response. If delivery is blocked,
  report it once with the saved report; do not repeatedly retry.
- Update this checkout's TICKETS outcomes/index and CURRENT_STATUS with task IDs,
  actual checks, scope and review evidence. Leave tickets at Review pending final
  acceptance. Report the completed batch/reviewer handoff; do not wait/poll for
  review unless asked. Material decisions/blockers require a report, not scope
  expansion. No automatic remediation/review loops, other tickets, commit, push,
  PR, deployment, real provider actions, user DB mutation or app restart.

## Active event-setup batch — approved 2026-09-13

The user authorized a new batch under the same direct implementation and fresh-review
workflow. Approved membership: **C01 → C02 → C03**; existing briefs own acceptance.

- Checkout: `/private/tmp/BingoWebpage-event-setup-ticket-batch-20260913`.
- Branch: `fix/event-setup-ticket-batch`, clean code base `origin/main` at
  `c165bbcb321547637d03b4e9dc3d2e206e5944b3`, fetched before setup. Initial AGENTS,
  CURRENT_STATUS, DELIVERY_PLAN and new TICKETS edits are the approved handoff.
- Earlier account/roster and announcement changes remain isolated. Their ledger
  outcomes do not mean those implementations exist here. Do not import them.
- Current source confirms Create validates/configures schedule outside its recoverable
  mutation handler, blank-slug generation rejects duplicate names, and shared signup
  readiness has no public-text-answer warning. These are bounded existing-contract
  corrections, not new major functionality requiring another readiness review.
- C01: validate optional entered and competition-derived dates against existing
  future/order rules, preserve safe inputs with actionable form feedback and no
  failed-create residue. Preserve valid minimal/future creation and the existing
  policy for unchanged historical Schedule values. Document the existing recovery
  path for already-created drafts; do not automatically change dates or user data.
- C02: use the existing slug generation/persistence boundary to allocate distinct
  automatic slugs for identical display names. Preserve explicit-conflict validation,
  stable existing URLs and atomic collision/retry behavior. No new allocation service.
- C03: add the applicable public-text-answer warning to existing manual and scheduled
  signup readiness/acknowledgment flows. No false warning with no applicable text
  question; retain current-readiness acknowledgment scope. Reuse warning controls,
  localization and schedule semantics. Never close or republish already-open events.
- Starting sources: Admin/Events/Create.cshtml.cs, EventSlugGenerator,
  EventSignupLifecycleService/EventReadinessEvaluator and existing creation/lifecycle
  tests. Read FUNCTIONAL_CONTRACTS 4.1–4.5, relevant public-answer requirements and
  schedule/slug invariants only as needed. Follow direct dependencies/consumers.
- Complexity budget: no new tables, services, routes, policies, jobs, dependencies,
  migrations or generalized abstractions expected. Preserve authorization, event
  transactions/audits, uploads, competition handling and EN/DA UI. No redesign,
  speculative cleanup, unrelated ticket fixes or historical repair.
- Focused checks: existing creation HTTP cases with past and invalid-order input
  plus valid creation and no residue; duplicate automatic names, explicit conflict
  and a controlled collision/retry; shared readiness/opening tests for text/no-text
  and manual/scheduled acknowledgment. Use disposable fixtures/mocked providers and
  relevant compile/format/diff checks. No default full-suite, load-test project or
  screenshot/preflight loop. Passing evidence is reusable by the reviewer.
- Use one new implementer `gpt-6-astra` / `xhigh`, directly with no implementation
  subagents, manager/verifier tasks, model substitution or Ponytail.
- When all three implementations and focused checks finish, freeze the complete
  base-to-current diff including untracked files and create exactly ONE fresh visible
  independent reviewer task `gpt-6-astra` / `xhigh`, read-only in this SAME checkout.
  This is explicitly user-authorized. Supply final scopes, complete diff and evidence;
  no redundant check reruns, reviewer subagents or automatic further review loops.
- The user authorizes sharing this batch's paths, scope, evidence and findings with
  that reviewer, the verified implementation task and originating planner
  `01a086a5-788c-7600-89ef-b24de23bd1fd` on local. Obtain the implementer task ID
  from runtime context and include both return destinations in the reviewer prompt.
  Reviewer reports to both and returns its normal final response. If automatic
  delivery is blocked, save/report it once; do not repeatedly retry.
- Update local ticket outcomes/index and CURRENT_STATUS with task IDs, actual checks,
  evidence and limitations. Implementation completion is Review; a clear independent
  verdict becomes Awaiting manual acceptance, with only deferred manual acceptance
  remaining. Keep actual blockers/data obligations separate. Manual walkthroughs are
  deferred until all ticket work finishes; release gates remain separate.
- Return the batch completion/reviewer handoff without waiting/polling. Stop for a
  genuine blocker or material decision. No automatic remediation loop, additional
  tickets, commit/push/PR/deploy, user DB mutation, provider action or app restart.

## Active participant-flow batch — approved 2026-09-13

User authorized the next new batch with the corrected review routing.
Approved membership: **C13 → C14 → C17**; the existing briefs own acceptance.

- Checkout: `/private/tmp/BingoWebpage-participant-flow-ticket-batch-20260913`.
- Branch: `fix/participant-flow-ticket-batch`; refreshed `origin/main` base
  `c165bbcb321547637d03b4e9dc3d2e206e5944b3`. Code started clean. Initial changes
  in AGENTS, CURRENT_STATUS, DELIVERY_PLAN and new TICKETS are planning handoff.
  Earlier account/roster/event-setup/announcement code remains in other checkouts;
  do not import it or assume the ledger's historical fixes exist in this branch.
- Source checks confirmed the CSV parser filters blank first-account cells before
  shape validation, strict UTF-8 decoding has no preview recovery, public waiting
  rank uses mixed-list indexes, vacancy notifications give all recipients an Admin
  URL and Captain role changes always link to the public Teams page.
- C13: a blank required primary column must fail even when later accounts exist;
  preserve optional columns and regular/informational roles. Invalid UTF-8 must
  produce safe existing preview feedback with no apply/partial records. Keep valid
  optional-column imports, nonce/collision protections and current CSV contracts.
- C14: count waiting participants independently in authoritative queue order;
  preserve public visibility, confirmed counts, admission/capacity rules and history.
- C17: choose existing notification destinations by recipient authorization and
  publication phase. Admin vacancy follow-up retains Admin context; ordinary team
  leadership receives permitted public/team/event context. Prepublication role
  notices must resolve without exposing an unpublished roster; published/live
  destinations retain existing authority. Preserve notification recipients,
  transaction/deduplication/privacy rules. Inspect/report retained unread-link
  recovery needs; do not silently rewrite notifications or build new navigation.
- Starting sources: PreformedRosterCsvImportService, Admin/Events/Draft upload
  preview handler, Events/Signups.cshtml.cs, SignupService vacancy notification
  path and TeamCaptainAuthorityService role notifications. Read relevant contracts
  5.4, 5.6, 6.1, 9.5 and roster CSV/visibility requirements; follow direct consumers.
- Complexity budget: no new tables, services, routes, policies, jobs, dependencies,
  migrations, navigation framework or UI redesign expected. Reuse current error
  handling, queries, transaction boundaries and EN/DA resources. No adjacent fixes
  such as C05/C09/C12/C16/C18, history repair or unsolicited data scans.
- Focused checks: blank-primary/nonblank-alt and invalid UTF-8 preview rejection
  without records, valid optional import applies once; waiter/preformed/waiter shows
  consecutive ranks without changed admission; follow actual emitted notification
  destinations as Admin and ordinary leadership before/after publication and Live.
  A URL-string assertion alone is insufficient for C17's recipient reachability.
  Use existing disposable integration fixtures/mocked providers and proportionate
  compile/format/diff checks. No real recipients/providers, user DB mutation,
  app restart, default full-suite or screenshot/preflight loop.
- One new Astra xhigh implementer works directly without implementation subagents,
  manager/verifier tasks, model substitution or Ponytail. After completing the batch
  and focused checks, freeze the complete diff including new files and create exactly
  one fresh visible Astra xhigh independent reviewer in this SAME checkout, read-only.
  Supply approved scopes, evidence and exact base-to-current boundary. Reuse passing
  check evidence; do not add another readiness review or reviewer subagents.
- **Reviewer routing:** required fixes/incomplete verdict go ONLY to the verified
  implementer; a passed review goes ONLY to originating planner
  `01a086a5-788c-7600-89ef-b24de23bd1fd` on local. Never send a verdict to both.
  This overrides all older dual-delivery batch wording for this assignment.
  The user authorizes sharing this batch's paths, findings and saved evidence with
  the selected destination. Read the actual implementer ID from runtime context.
- Required corrections within these approved briefs belong to the implementer.
  Fix only valid named findings and run affected checks; request a bounded recheck
  from the same independent reviewer for those corrections/direct consequences.
  No broad repeated review or automatic expansion/new reviewer chains. Follow the
  repository's failed-attempt limits; stop for genuine blockers or material decisions.
- Record local ticket outcomes/status, evidence, limitations and reviewer task ID.
  Implementation is Review; after a clear verdict the planner records Awaiting
  manual acceptance. Actual data/code/check blockers remain explicit. User deferred
  manual walkthroughs until all tickets finish; package/release gates remain separate.
  Return the implementation/reviewer handoff without polling/waiting. No additional
  tickets, commit/push/PR/deploy, real messages to participants or user-data repairs.

## Active board-approval batch — approved 2026-09-13

User authorized the next batch. Approved membership: **C19 → C23 → C25**.
Existing ticket briefs and this scope govern the work. This is a bounded correction
batch, not a new major slice requiring another planning/readiness review.

- Create a NEW task under the saved BingoWebpage project using a Codex worktree.
  Within its clean generated worktree create `fix/board-approval-ticket-batch` from
  fetched `origin/main` at `c165bbcb321547637d03b4e9dc3d2e206e5944b3`. Record the
  actual generated checkout path in the local CURRENT_STATUS/TICKETS handoff.
  Preserve unexpected existing changes; do not operate in the saved Documents
  checkout or any earlier batch/announcement checkout. Do not copy prior code.
- Bootstrap only this ledger and the current AGENTS from
  `/private/tmp/BingoWebpage-drop-announcements`. Retain the generated checkout's
  base CURRENT_STATUS/DELIVERY_PLAN and prepend/append a compact assignment handoff
  overriding historical checkout/task/model directions. These are intentional
  planning changes, not an import of announcement behavior or other batch code.
- Current source confirms standard templates pass ManualEhbOverride to approval
  summation, approval checks BoardVersion without displayed catalogue freshness,
  and known InvalidOperationException diagnostics collapse to a generic message.
- C19: enforce the existing manual-objective versus standard-catalogue distinction
  on the server and directly affected input/display paths. Standard missing-derived
  EHB must not be bypassed with a posted manual override; valid standard and manual
  objectives remain approvable. Protect mixed requirement semantics per the current
  contract rather than inventing a new scoring policy. User resolved mixed tiles
  on 2026-09-13: prohibit manual/drop combinations in the editor and authoritative
  create/update/approval paths; require separate tiles. Preserve valid same-kind
  multi-objective tiles and the current all-manual estimate path. Existing invalid
  configurations get actionable correction guidance, never automatic splitting,
  deletion, conversion or historical recalculation. Preserve public/frozen data.
  Classify possible retained approved overrides using isolated evidence/proposal only;
  any user-data scan or historical correction needs separate authority.
- C23: retain the exact displayed approval input scope through the existing form
  and final transaction. Relevant intervening catalogue changes require reload/review
  with no approval snapshot; refreshed coherent inputs succeed. Choose/document a
  minimal version/fingerprint or existing invalidation approach. Cover direct
  approval consumers including published corrections as applicable to the same gate.
  Do not retroactively invalidate existing snapshots or build a cache framework.
- C25: expose known safe actionable validation for position/source/identity/estimate
  problems through existing localized feedback; unexpected failures remain generic.
  Preserve rollback and no-approval-residue behavior. Do not expose raw internal
  exception details or redesign the protected board editor.
- Starting sources: Admin/Events/Board.cshtml(.cs), existing EhbCalculator and
  approval/snapshot queries, relevant catalogue/domain versions and board approval
  tests. Read FUNCTIONAL_CONTRACTS 6.2–6.3 and directly applicable EHB/snapshot,
  catalogue, security and UI authority only as needed. Follow direct consumers.
- Complexity budget: no new tables, service classes, routes, policies, jobs,
  dependencies or migrations expected. Small local validation/fingerprint helpers
  supporting the exact requirement are allowed; no speculative generalization,
  unrelated C20/C21/C22/C24 work, history repair or UI composition changes.
- Focused checks: standard missing-rate plus manual override rejected, valid standard
  and manual pass; rendered board → relevant catalogue edit → stale POST rejected
  without snapshot → refreshed approval; distinct safe diagnostics and unexpected
  failure boundary. Use existing disposable database/HTTP fixtures and proportional
  compile/format/diff checks. No user DB mutations, real providers, app restart,
  default full-suite, screenshot/preflight loop or redundant reviewer reruns.
- One new Astra xhigh implementer works directly with no implementation subagents,
  manager/verifier tasks, model substitution or Ponytail. When complete and checked,
  freeze the entire base-to-current diff including new files and create exactly one
  NEW visible Astra xhigh reviewer task under the SAME saved BingoWebpage project.
  Reviewer is read-only and must inspect the actual implementation checkout/diff,
  never assume a freshly generated reviewer worktree contains the uncommitted patch.
- Give the reviewer final scope, exact implementation path, full diff and passing
  evidence. Required findings/incomplete verdict return ONLY to the actual
  implementer; passed review goes ONLY to originating planner
  `01a086a5-788c-7600-89ef-b24de23bd1fd` on local. Never notify both.
  Obtain the implementer ID from runtime context. User authorizes sharing relevant
  batch paths/findings/report with the selected destination. Save/report blocked
  delivery once, without repeated retries. Reviewer returns its normal final too.
- Required in-scope corrections belong to the implementer; fix only valid named
  findings, run affected checks and request a bounded recheck from that same
  independent reviewer. No broad repeated audits, further reviewer chains or
  unbounded loops; stop for material scope/behavior decisions or genuine blockers.
- Record actual checks/outcomes, evidence, baseline, ownership and reviewer ID.
  Implementation is Review. Only a clear independent verdict with no other ticket
  blocker permits Awaiting manual acceptance. Manual walkthroughs are user-deferred;
  data/decision/check blockers and combined release gates remain separate.
  Return implementation/reviewer handoff without waiting/polling. No other tickets,
  commit/push/PR/deploy, historical repair or moving existing tasks.

## Active audit-atomicity batch — approved 2026-09-13

User authorized another new batch after recording the board-approval result.
Approved membership: **C04 → C15 → C22**. Keep each ticket's scope and outcome
separate; this is one coherent batch of audit-transaction corrections, not one
unbounded audit rewrite. These writer fixes are prerequisites for the later C36
privacy ticket; C36 itself and all retained-data repair remain excluded.

- Create one NEW Astra xhigh implementer task under the saved BingoWebpage project,
  using a Codex worktree. In its clean generated checkout create branch
  `fix/audit-atomicity-ticket-batch` from refreshed `origin/main` at
  `c165bbcb321547637d03b4e9dc3d2e206e5944b3`. Record actual path/base/ownership.
  Copy only current AGENTS/TICKETS from `/private/tmp/BingoWebpage-drop-announcements`
  as planning bootstrap; add a focused CURRENT_STATUS/DELIVERY_PLAN handoff locally.
  Do not import previous batch source, overwrite unexpected changes or touch other
  checkouts. Historical ledger outcomes do not mean those fixes are in this branch.
- Source inspection confirms C04 saves signup-code configuration before AuditWriter,
  C15 contains Save/Commit then Audit patterns, and C22 commits ordinary board changes
  before WriteAudit. Existing atomic operations remain protected and need no rewrite.
- C04: Questions SignupCode configuration and its accurate event-scoped before/after
  audit must commit/rollback together. Preserve enable/disable/replacement behavior;
  never put raw signup codes, hashes or other secrets in audit payloads.
- C15 named sites: team metadata changes and empty drafted-team removal; draft
  start/scramble/pause/resume/pick/undo; acquire/takeover/release draft control.
  Move mandatory audit into each existing atomic save/transaction boundary and set
  EventId with truthful before/after context. Preserve lifecycle, controller leases,
  pick history, authorization and concurrency. Existing atomic finalization/cancel,
  role and preformed-roster corrections retain their behavior.
- C22 named sites: board creation, editor acquire/takeover/release, tile creation/
  editing/add/remove/move/swap, board resize/compaction and planning/team-size changes.
  Ordinary mutation and event-scoped accurate audit must commit/rollback together.
  Preserve position uniqueness/two-stage moves, versions/leases, editor interaction,
  storage cleanup and post-commit invalidation behavior. Already atomic approval,
  unapproval/publication/correction paths are preserved, not redesigned.
- Starting files: Admin/Events/Questions.cshtml.cs, Draft.cshtml.cs, Board.cshtml.cs,
  existing IAuditWriter/AuditWriter and relevant transaction tests. Contracts 2.3,
  4.5, 6.1, 6.2 and directly relevant transaction/audit/lease/storage invariants.
  Supplementary original site inventories are U3-06 in
  `/private/tmp/bingo-sweep-unit3-20260909.md` and U4-04 in
  `/private/tmp/bingo-sweep-unit4-20260909.md`; current symbols, not old line numbers,
  govern implementation. Inventory the named handlers briefly before edits so none
  are silently omitted. Directly required consumer changes stay within this scope.
- Complexity budget: no new tables, service classes, audit framework, routes,
  policies, jobs, dependencies or migrations expected. Extend existing transaction
  and audit patterns; small local helpers are allowed only for concrete repetition.
  No opportunistic reformatting, UI changes, other tickets or audit-history repair.
  Do not fabricate missing past before/after values or associate old rows by guess.
- Focused checks: success with correct EventId and safe before/after for each covered
  operation family, and injected audit failure proving state rollback for each
  distinct persistence pattern. Protect multi-save move/resize and explicit-commit
  draft operations; one parameterized scenario may cover related handlers. Reuse
  existing draft ledger/board mutation/signup-code tests. Do not duplicate every
  assertion across layers; exact existing atomic paths need only relevant regression.
  Use disposable fixtures, proportional compile/format/diff checks, no user DB or
  real providers, default full-suite, screenshot/preflight loop or app restart.
- One direct Astra xhigh implementer; no implementation subagents, manager/verifier
  tasks, model substitution or Ponytail. After the whole batch and focused checks
  finish, freeze complete base-to-current diff including new/planning files and
  create exactly one NEW visible Astra xhigh independent reviewer under BingoWebpage.
  Reviewer remains read-only in the actual implementation checkout; a fresh initial
  reviewer worktree must not substitute for the real uncommitted diff. Supply final
  scopes, full diff, named-site inventory and reusable passing evidence.
- Reviewer findings/incomplete verdict go ONLY to the verified implementer; pass
  ONLY to planner `01a086a5-788c-7600-89ef-b24de23bd1fd` on local. User authorizes
  sharing relevant batch paths/evidence/report with the selected destination, not
  dual delivery. Get the actual implementer ID from runtime context. Save/report
  blocked delivery once; no repeated retries. Reviewer returns a normal final too.
- Fix only valid named in-scope findings, run affected checks, and request a bounded
  recheck from the same reviewer; no further reviewer chains, broad audits or endless
  loops. Stop for material scope/product/complexity decisions or genuine blockers.
- Record ticket outcomes, actual checks/limits and reviewer ID; return handoff without
  polling/waiting. Implementation is Review; clear independent review with no other
  blocker permits Awaiting manual acceptance. Manual walkthrough is user-deferred;
  data/decision/check blockers and package/release gates remain separate. No additional
  tickets, staging/commit/push/PR/deploy, user-data repair or moving existing tasks.

## Active participant-evidence batch — approved 2026-09-13

**Completed implementation/review checkpoint:** same reviewer
`01a09c68-a1fd-74a2-a253-c62a2b8c5dce` cleared R1/R2; passed delivery was received
by planner after the explicitly approved retry. The recheck report's prior blocked
routing note is historical; delivery is resolved. [Independent recheck](/private/tmp/participant-evidence-batch-evidence/independent-recheck.md)
and [remediation evidence](/private/tmp/participant-evidence-batch-evidence/remediation-report.md)
record 15 distinct passing remediation cases across two runs, final Release build
with zero warnings/errors and scoped format/whitespace results. Unchanged C29/C30
service/HTTP/Node evidence and review conclusions were reused. No planner reruns.
All three tickets await user-deferred manual acceptance; no active batch remains.
The original approved contract follows as provenance, not renewed authorization.

User authorized a new coherent batch under the established workflow: **C28/C29/C30**.
Use the existing three ticket briefs as the scope and acceptance contract. Implement
C28 current submission authority, C29 Live replacement-only competition configuration,
then C30 ledger status filtering; check interactions where applicable. No recorded
prerequisite blocks this batch. Source gaps were confirmed against origin/main
`c165bbcb321547637d03b4e9dc3d2e206e5944b3`; prior six reviewed batches and paused
announcement changes remain isolated and are not dependencies imported here.

Fresh visible `gpt-6-astra` / `xhigh` implementer works directly in its new saved-project
worktree. It may bootstrap only central AGENTS/TICKETS. Preserve the approved ledger
composition, authorization, historical evidence, pre-Live competition rules and cache
safeguards. Focused checks are the three briefs' PostgreSQL/concurrency, service/HTTP
and changed-client checks using controlled fixtures; no default full-suite or visual
walkthrough. Existing focused evidence may be reused when applicable.

After implementation/checks, the implementer is authorized to create exactly one fresh
visible `gpt-6-astra` / `xhigh` independent reviewer under the saved BingoWebpage project.
Review the actual implementation checkout and full diff including new files. Required
findings/incomplete verdict go only to the implementer; pass only to planner
`01a09c57-1f38-7191-94de-e11647ffbcf1`. One bounded remediation and recheck by the same
reviewer is allowed. Record evidence locally; planner reconciles the central ledger.
Stop after handoff. Passed tickets become Awaiting manual acceptance, never automatically
Done. No implementation subagents, separate verifier, repeated broad review/checks,
packaging/publication, app restart, real-data repair or automatic next batch.

## Active final-review integrity batch — approved 2026-09-13

**Completed implementation/review checkpoint:** independent Astra xhigh reviewer
`01a09c96-559d-7490-b652-efcfaa426c05` passed with no required findings or established
unresolved data obligation. [Independent review](/private/tmp/final-review-integrity-batch-evidence/independent-review.md)
verified full tracked/untracked diff and eight hashes in the actual checkout.
[Implementation report](/private/tmp/final-review-integrity-batch-evidence/implementation-report.md)
records focused evidence reused by review: 13 distinct passing cases across runs
(8 PostgreSQL/HTTP, 5 domain), Release solution build with zero warnings/errors,
scoped format/whitespace. Review clarifies C32 future-cutoff coverage comes from
PostgreSQL actor cases; domain cases cover other lifecycle/cutoff controls. No
retained-database migration rehearsal is claimed and no migration was required.
Implementer `01a09c89-371d-75a1-979c-00f27f17e7da`; actual checkout
`/Users/christopher/.codex/worktrees/0e93/BingoWebpage`, branch
`codex/final-review-integrity-ticket-batch`, unchanged base `c165bbc`, isolated and
uncommitted. User directly supplied the passed report after rejected tool delivery;
central reconciliation is complete without retry, duplicate review or planner reruns.
All three tickets await user-deferred manual acceptance. No active batch remains.
The original authorized contract follows as provenance, not renewed authorization.

User authorized **C31/C32/C35** and batch-relevant handoffs between the direct
implementer, its one assigned reviewer and planner. Use the existing three ticket
briefs as the scope/check contract. Order: C31 event-scoped pending-review navigation,
C32 closed uploads after unfinalization until explicit valid reopen, C35 atomic main
audit of reversal-rebalanced child contributions. The source gaps were confirmed
against origin/main `c165bbcb321547637d03b4e9dc3d2e206e5944b3`. No recorded dependency
requires importing prior isolated source. C33 competitive-input freshness and C20
objective identity remain separate, unassigned design scopes.

A fresh visible saved-project `gpt-6-astra` / `xhigh` implementer directly completes
the batch and its proportional focused checks, then creates exactly one fresh visible
saved-project `gpt-6-astra` / `xhigh` independent reviewer. Bootstrap only central
AGENTS/TICKETS; preserve all prior dirty work and keep this implementation isolated.
Use existing domain/service/history boundaries; preserve ordinary inclusive versus
emergency exclusive cutoffs, valid reopening, evidence/history, event authorization,
contribution caps and local reversal history. Check C32 retained-row distinguishability
and C35 historical audit limits without scanning/repairing user data; any required
unresolved obligation is a blocker, never silently treated as manual acceptance.

Checks: follow C31's actual blocker link through authorized isolated HTTP Review and
assert event isolation; execute C32 early-finalize/unfinalize denial at create/edit/
resubmit boundaries and valid explicit reopen with relevant cutoff controls; extend
existing C35 PostgreSQL rebalance evidence for child main-audit data and atomic rollback
on audit failure. Run only applicable build/format gates; reuse valid evidence.

User explicitly authorizes sharing relevant private repository paths, branches, hashes,
diffs, findings and check results among the verified assigned implementer, reviewer and
planner `01a09c57-1f38-7191-94de-e11647ffbcf1`. No repeated approval requests for these
handoffs. Findings/incomplete verdicts go only to implementer; pass only to planner.
Review the actual implementation checkout/full diff including new files. One bounded
named correction and same-reviewer recheck; no implementation subagents, extra verifier,
reviewer chains, default broad reruns or visual preflight. Stop after handoff, without
poll loops. Passed tickets await user-deferred manual acceptance. No automatic next
batch, staging/commit/push/PR/merge/deploy, app restart or real-data repair.

## Active Admin stale-change batch — approved 2026-09-13

**Completed implementation/review checkpoint:** C24/C39 are Awaiting manual
acceptance. Reviewer `01a09cb1-c492-7860-aa2f-00eed3defab4` passed without required
findings and delivered only to planner. [Independent review](/private/tmp/admin-stale-change-batch-evidence/independent-review.md)
verified all 13 file hashes and the full patch against the actual isolated checkout
`/Users/christopher/.codex/worktrees/0a19/BingoWebpage`, branch
`codex/admin-stale-change-ticket-batch`, base `c165bbc`, unstaged/uncommitted.
Implementer: `01a09c9f-16f7-78a0-af80-00252b943d7d`.
[Implementation evidence](/private/tmp/admin-stale-change-batch-evidence/implementation-report.md):
18 distinct passing PostgreSQL/HTTP cases across commands, four Chrome confirmation
cases, silent Node/scoped format exits 0, zero-warning/error Release build and clear
whitespace. Review reused those results; no planner reruns. Controlled browser
transport is not a separate live browser-to-DB or manual visual acceptance; approved
snapshot/identity preservation is source-reviewed, not reconstructed history.
No retained-data repair, migration, prior-batch integration or publication. User
directed recording this batch and stopping for the night; no successor is dispatched.
Original authorized contract follows as provenance, not renewed work authorization.

User authorized **C24/C39**, retaining standing handoff approval and the corrected
implementer/reviewer/planner sequence. Existing ticket briefs own scope/acceptance.
Order: C24 shared catalogue item version and atomic item audit; C39 freshness across
existing disable/restore/grant/revoke account confirmations. Source gaps confirmed
against origin/main `c165bbcb321547637d03b4e9dc3d2e206e5944b3`. No recorded prerequisite
requires importing earlier isolated source. Prior eight reviewed batches remain
uncommitted and unintegrated; C23 catalogue-approval inputs and C08 self-account edits
are separate reviewed changes, not assumed present here.

Fresh visible saved-project `gpt-6-astra` / `xhigh` implementer works directly in its
isolated worktree. Bootstrap ONLY central AGENTS/TICKETS; preserve all dirty source.
Use current version, confirmation, authorization and audit boundaries. Preserve
catalogue/item identity and approved snapshots, account self/owner/role rules,
session invalidation, confirmation count and accepted Admin composition. No C26/C40
redesign, broader Admin audit, new withdrawal/credential policy or retained-data repair.

Focused checks: C24 two drops sharing an item, intervening rename/image mutation,
stale form POST with unchanged drop/item, current save and atomic item before/after
audit. C39 actual two-stage confirmation with intervening completed target-state or
role change, stale POST rejected without effects, valid confirmed action and session
invalidation with applicable authorization controls. Exercise real PostgreSQL and
HTTP boundaries using controlled disposable fixtures; reuse existing discriminating
tests. No default full-suite, screenshot/manual preflight or confidence-only reruns.
Run applicable focused build/format gates. Report material uncertainty after one
focused lookup; do not invent policy or silently import prior fixes.

Implementer is authorized to create exactly one fresh visible saved-project Astra
xhigh independent reviewer and sends review request/full diff/evidence ONLY to it,
then stops. Review the ACTUAL implementation checkout including new/untracked files.
Reviewer sends PASS ONLY to planner `01a09c57-1f38-7191-94de-e11647ffbcf1`; required
remediations or incomplete verdict ONLY to verified implementer. One bounded named
remediation and SAME-reviewer recheck is authorized; implementer sends that request
ONLY to reviewer. No implementation checkpoint, dispatch notice, acknowledgment or
duplicate handoff to planner. Standing user approval covers necessary private paths,
branches, hashes and evidence to those exact assigned destinations; no repeated
permission requests. Automatic rejection stops that delivery, with one report and
no bypass/retry. No implementation subagents, extra verifiers or reviewer chains.
Passed tickets await user-deferred manual acceptance. No automatic next batch,
staging/commit/push/PR/merge/deploy, running-app restart or real-data repair.

## Active UI test-repair batch — approved 2026-09-14

**Completed checkpoint:** reviewer `01a09ee6-1aef-7eb1-80b9-51d015861ca3` passed
same-reviewer F01 recheck; no required findings remain. [Independent recheck](/private/tmp/ui-test-repair-t01-t04-evidence/independent-recheck.md)
verified all eight hashes and exact final patch in the actual checkout. Implementer
`01a09ed4-3378-7610-828a-12527278488c`, checkout
`/Users/christopher/.codex/worktrees/0168/BingoWebpage`, branch
`codex/ui-test-repair-t01-t04`, base `c165bbc`, isolated and unstaged/uncommitted.
Three distinct C# cases and five distinct Node files pass (case and file counts are
not interchangeable). F01's sole production exception was explicitly user-authorized
in the review task: repair the dangling Players panel aria-labelledby to the existing
unique localized view link. Unchanged final reference-integrity safeguard now passes;
affected Web/Razor build has zero warnings/errors, not a Release solution build.
Other evidence/source review reused; no planner reruns or manual/browser/assistive-
technology/cumulative release claim. Initial incomplete verdict/evidence preserved.
This records the authorized exception to the original test-only contract below;
it does not permit further production changes. All four await manual acceptance.

Planner clarification 2026-09-14: T03 also owns its same-file stale assertion/source
loading remainder exposed after the mock fix. This is bounded test-only correction
against accepted masthead/metrics, localization and Board.cshtml table/panel/header anatomy, not new
product behavior. The implementer corrected its initial partial-extraction inference: tables remain in Board.cshtml; no extraction is established. Preserve meaningful invariants, record replacement rationale and run
the full file; real product defects still remain outside scope. The existing single
reviewer includes this clarified contract, without a second review or confidence runs.
Implementer also reported central UI_SYSTEM still contains stale zoom wording despite
D04's recorded completion; this authority inconsistency is recorded, not silently
resolved by copying code. The current batch's explicit accepted no-control-bar contract
is controlling for T02. No separate UI documentation audit is authorized here.

User resumed continuous batch execution with standing approvals/routing. Assign
**T01/T02/T03/T04** as one test-only batch using their existing briefs. T02's D04
prerequisite is Done: accepted evidence interaction uses click-focused magnifier,
keyboard/pointer/touch pan with no separate control bar. Existing source and the
current central UI authority govern the revised assertions; do not assert a desired
redesign. Base last verified origin/main `c165bbcb321547637d03b4e9dc3d2e206e5944b3`.
All named tests exist; T03's forced-only toggle fixture remains at that base.
Prior nine reviewed batches and paused announcement source remain isolated; do not
import them or pretend this restores a cumulatively integrated release gate.

Fresh saved-project Astra xhigh implementer directly repairs the three named T01
C# tests and five named T02/T03/T04 Node files. Bootstrap ONLY central AGENTS/TICKETS
into the isolated worktree; read focused central accepted authority as needed. Keep
production source unchanged. Preserve meaningful behavioral assertions and run
previously blocked remainders; do not mask real product defects or remove safeguards
just to get green. New runtime defects are reported separately with exact evidence,
not silently fixed under this test-only assignment. Do not add a DOM framework.

Run the three corrected C# cases, the five corrected Node files and only directly
affected shared test helper consumers. Reuse passing evidence; no full suite, live
browser/screenshot loop, manual preflight, user DB action or app restart. Test compile
and scoped format/whitespace checks only as applicable. Record actual distinct test
results, original failures/reproductions, newly exposed failures and limits.

One fresh visible saved-project Astra xhigh reviewer is authorized after focused
checks. Implementer review request/full diff/evidence goes ONLY to reviewer, then
stops. Reviewer inspects ACTUAL implementation checkout/full tracked/untracked diff;
pass ONLY to planner `01a09c57-1f38-7191-94de-e11647ffbcf1`, required remediation or
incomplete verdict ONLY to verified implementer. One bounded named correction and
SAME-reviewer recheck; no subagents/verifiers/reviewer chains. No checkpoint/dispatch
notice to planner. User approval covers necessary private paths/hashes/findings and
evidence to the assigned exact destinations; no repeated permission requests. Rejected
delivery stops that action without bypass/retry. Workers never start another batch;
planner may after reconciling pass under the resumed standing authorization.
Passed status follows existing acceptance policy; no manual or combined release claim.
No stage/commit/push/PR/merge/deploy, source integration or retained-data repair.

## Active published-content batch — authorized 2026-09-14

**Completed checkpoint:** sole reviewer `01a09f18-1c57-7672-88e9-71c359f079b5`
cleared R1 after bounded same-reviewer recheck; pass delivery to planner succeeded
after explicit exact-payload/destination approval. [Final cohesive review](/private/tmp/published-content-c21-c37-evidence/r1/reviewer-recheck.md).
Implementer `01a09f06-ecb1-7882-a95f-a12285d2f8cf`; actual checkout
`/Users/christopher/.codex/worktrees/e588/BingoWebpage`, branch
`codex/published-content-c21-c37`, base/HEAD `c165bbc`, unstaged/uncommitted.
Reviewer verified all 23 hashes/exact full patch; unchanged files outside R1 match
original review. Seven distinct PostgreSQL/storage/real auth+antiforgery HTTP cases
pass; R1 reproduces cancellation context-nav leakage before correction, then passes
three affected cases (part of seven, not extra cases). Full-solution Release evidence
reused; affected Web/Razor Release has zero warnings/errors, scoped format/whitespace
clear. No planner runtime reruns or manual/browser/cumulative release claim.
C21 includes the necessary image-to-working-tile FK removal migration, designer and
snapshot; local DATA_MODEL records preserved identity and fail-closed Down after an
asset outlives its working tile. No backup/restore/real-data repair or post-retention
rollback is claimed. C19/C22 Admin Board overlap requires later authorized integration;
C20 remains unassigned. Both tickets await manual acceptance.

Planner selects **C21/C37** under user-resumed continuous authorization. Existing
briefs own scope/acceptance: C21 retain artwork referenced by current/retained approval
snapshots through private working-tile/image removal; C37 generic safe cancellation
at old public Board/Teams/team/tile destinations, retaining archived/finalized history.
Gaps confirmed at origin/main `c165bbcb321547637d03b4e9dc3d2e206e5944b3`: tile removal
deletes asset rows and stored files; published cancelled events enter the full public
board projection. No recorded dependency requires importing another isolated batch.
C20 objective identity remains a separate policy/design scope. Earlier C19/C22 source
is unmerged and must not be assumed present or overwritten during later integration.

Fresh saved-project Astra xhigh implementer directly completes both tickets in its
isolated worktree, bootstrapping ONLY central AGENTS/TICKETS. Use existing managed-asset
retention, authorization and public-state rendering. Preserve snapshots, all evidence,
roster/history, current/retained authorized image access, event isolation and bounded
unreferenced cleanup. Cancellation must not expose private reasons or competitive
workspace controls. Do not erase publication/history to implement hiding. No broad
public redesign, objective conversion/removal policy, historical reconstruction or
source integration. A demonstrated missing stored object is a separate recovery
obligation; no backup/restore/data scan/repair is authorized. Ordinary technical choices
stay with implementer; consequential new persistence/product scope goes to planner.

Focused executable checks: real authorized published image URL survives private tile
removal and image replacement/supersession while referenced, authorized retained access
and cross-event/private denial; publish/cancel and follow actual old Board/Teams/team/
tile URLs and relevant enhanced/read endpoints, asserting generic state/no private
reason/no competitive controls, plus archived/finalized visibility controls. Use
controlled PostgreSQL/storage/HTTP fixtures and existing commands; no default full-suite,
screenshot/manual preflight or user app restart. Preserve cancellation image access
rules without assuming all retained assets should be public. Run applicable focused
build/format and reuse valid evidence. No mere source-only privacy proof.

Exactly one fresh saved-project Astra xhigh reviewer after focused checks; actual
implementation checkout/full diff including untracked files. Implementer request only
to reviewer; reviewer PASS only to planner `01a09c57-1f38-7191-94de-e11647ffbcf1`,
remediation/incomplete only to implementer. One bounded named correction/SAME-reviewer
recheck. Standing user approval covers necessary private paths/hashes/findings/evidence;
no repeated requests, checkpoint/dispatch notice to planner, subagents, extra verifiers,
reviewer chains or broad restarts. Automatic rejection stops that send without retry
or bypass. Workers stop after handoff; planner continues only after passed review.
Manual acceptance remains deferred. No stage/commit/push/PR/merge/deploy, integration,
user-data repair or running-app restart.

## Active draft/start-readiness batch — authorized 2026-09-14

**Completed checkpoint:** original reviewer `01a09f3d-c7ed-71b0-8aac-142630432b49`
cleared F01 with no required findings; planner pass delivery succeeded after explicit
user instruction. [Independent recheck](/private/tmp/c10-c38-evidence/r1/independent-recheck.md).
Implementer `01a09f2a-cc4a-7f60-aff3-4786ca721e65`, actual checkout
`/Users/christopher/.codex/worktrees/c68a/BingoWebpage`, branch
`codex/draft-start-readiness-c10-c38`, base/HEAD `c165bbc`. Reviewer verified 12
final hashes/exact full patch and original snapshot; all source remains unstaged/
uncommitted. F01 restores source-based MemberView.External for existing post-Setup
removal-control eligibility; active preformed membership still controls pool/counts.
No C11 departure/handler/publication policy introduced. New regression failed before
fix at unwanted website-member control; after-run 2/2 passes without destructive removal.
23 distinct cases pass overall; C38 source/evidence unchanged, prior source review/full
solution Release reused, affected Web/Razor Release zero warnings/errors, format and
whitespace clear. No planner runtime reruns. Real migrated PostgreSQL/auth/HTTP fixture;
C38 storage is a recording double, not binary processing or new event/access race proof.
Later integration must reconcile Draft/C12, lifecycle/C15/C16/C18, Accounts/C39,
evidence/C28/C32 and shared authorities. Both tickets await manual acceptance.

Assign **C10/C38** under resumed continuous authorization. C38 user decision is
explicit: enable fully set-up emergency accounts after draft finalization while
submissions wait for actual start, including authorized early start. Owning policy
is now FUNCTIONAL_CONTRACTS 9.4 and DATA_MODEL 8.2. Explicit pre-start enablement
records its current grant instant for ActiveFrom; no scheduled-start floor, no
retroactive eligibility and no bulk history/grant rewrite. Current/end/visibility/
team-scope and terminal restrictions apply; existing Live/cutoff/explicit reopen/
re-enable behavior remains. Initial creation/setup never auto-enables an account.

C10: eligible confirmed internal participants enter ordinary pool/pick/preassignment/
finalization; actual preformed membership remains excluded and correctly counted.
C38: real Admin creation/setup/enable -> scheduled, early manual or postponed manual
start works for an emergency-only team. Readiness rejects unusable or uninitialized,
disabled, expired/wrong-scope credentials and actual mutation stays closed pre-start.
Use existing draft/capacity/authorization/temporal services and scoped fixtures. Keep
C11 departure policy separate. C12/C15/C16/C18/C28/C32/C39 earlier fixes are isolated,
unmerged; do not import them or assume presence. Record directly overlapping paths
for later integration without reimplementing unrelated fixes.

Fresh saved-project Astra xhigh implementer works directly in isolated worktree,
bootstrapping ONLY central AGENTS/TICKETS; read the newly approved focused policy
above in central owning authorities, and record equivalent focused clauses locally
if needed, never copy entire dirty authority files. Base last verified c165bbc.
Run focused PostgreSQL/HTTP draft pool/pick/preassign/finalized-roster controls with
actual preformed membership; real emergency creation/setup/enable/start and denied
unauthorized/unset-password/pre-start use, scheduled/early/postponed start and existing
cutoff/reopen behavior. Test actual relevant auth/transaction boundaries, not source
only. Applicable scoped build/format; no default full-suite/screenshots/manual preflight,
new frameworks, user app restart or DB scan/repair. Genuine consequential uncertainty
after one focused lookup goes to planner; ordinary technical choices are delegated.

Exactly one fresh saved-project Astra xhigh independent reviewer after focused
checks. Implementer review/recheck request ONLY to reviewer, reviewer PASS ONLY to
planner `01a09c57-1f38-7191-94de-e11647ffbcf1`, findings/incomplete ONLY to implementer.
Review actual implementation checkout/full tracked/untracked diff with exact evidence.
One bounded named remediation/SAME-reviewer recheck. No checkpoint/dispatch notice to
planner, subagents/verifiers/model substitutions/reviewer chains. Standing user approval
covers private paths/diffs/hashes/findings/evidence to assigned verified destinations;
no repeat handoff approvals. Automatic rejection stops delivery, no bypass/retry.
Workers stop at handoff; planner continues after pass. Manual acceptance remains
deferred; no stage/commit/push/PR/merge/deploy/integration/data repair.

## Active font-loading batch C41 — authorized 2026-09-14

**Completed checkpoint:** reviewer `01a09f67-594b-7830-ab56-9d005d9ef592` passed
with no required findings, scope deviations or missing proof; delivered only to planner
under explicit user approval. [Independent review](/private/tmp/c41-evidence/independent-review.md).
Implementer `01a09f4f-0ecb-7383-8574-03121ed5416a`; actual checkout
`/Users/christopher/.codex/worktrees/764b/BingoWebpage`, branch
`codex/public-font-loading-c41`, base/HEAD `c165bbc`, unstaged/uncommitted. Reviewer
verified 102 source/protected/evidence hashes, exact full/source patches, five-file
boundary and lossless Medium metrics/outlines/codepoints/license. New WOFF2 37,880
versus original 103,360 bytes. Existing typography/Admin/fonts/licenses unchanged.
40 actual-app captures form 20 baseline/candidate pairs at two widths; final scoped
geometry matches, no overflow/duplicate successful font request/preload warning,
warm bodies validated with 304 and blocked controls readable/interactive. Slowed
Geist discovery improves ~8.7–8.8s to ~0.165s; Signup Medium ~8.84s to ~0.171s;
measured Signup CLS becomes zero. Material tradeoff: slowed FCP is 340–552ms later,
ordinary timings vary. One local headless Chrome sample/condition, one locale/theme/
reduced-motion shell, Development no-cache/ETag; not production caching, universal
no-swap, faster overall paint or successful Signup/WoM proof. Baseline/candidate
focused Web/Razor Release zero warnings/errors reused; no planner reruns. C41 awaits
user visual acceptance. Shared layout/CSS/C37 overlaps remain for later integration.

Assign **C41** under continuous execution. This single-ticket batch is deliberate:
C11/C20/C33 still need bounded policy/design decisions and C36 needs writer integration
and legacy-row policy; no unrelated ticket is bundled merely to increase batch size.
C41's existing detailed brief owns exact scope and browser measurement acceptance.
Baseline c165bbc still preloads Barlow 600/800 only, Geist is unpreloaded and Medium
500 is TTF-only. No cache-policy defect or exact user-visible flash cause is claimed.

Fresh saved-project Astra xhigh implementer directly adds matching public Geist
preload and Signup-only Medium 500 WOFF2/preload generated from the existing licensed
face. Preserve TTF/license, aliases, typography, readable swap/fallback, existing
600/800 preloads and deployment asset conventions. No loader, hidden text, Admin
font change, blanket preloads, typography/cache redesign or new dependency framework.
Measure first; another measured cause is a scope question, not permission to expand.

Unlike default small UI checks, this ticket explicitly REQUIRES before/after runtime
measurements on actual rendered landing/Signup from exact baseline/candidate apps:
cold ordinary, one slowed cold, warm navigation/reload and one blocked-font case;
request start, duplicate/preload warnings, real cache responses and desktop/narrow
wrapping/shift. An isolated app with controlled disposable data/ports is authorized;
no synthetic HTML-only substitute or restart of user's HTTPS7131 app. Generate only
scoped evidence/artifacts; visual user acceptance remains deferred. Focused Razor
compilation/scoped checks as needed; no full .NET suite or repeated confidence runs.
Real measurement/environment limits must remain explicit, never claim universal no-swap.

User's explicit batch-specific approval: "I approve messages between threads for
this batch youre about to send." It covers C41 assigned implementer, its one reviewer
and planner `01a09c57-1f38-7191-94de-e11647ffbcf1`, including necessary private paths,
diffs/hashes, findings and evidence. Same directions apply: implementer review/recheck
ONLY to reviewer; reviewer PASS ONLY to planner, findings/incomplete ONLY to implementer.
No checkpoint/dispatch notice to planner. Create exactly one fresh saved-project Astra
xhigh reviewer to inspect actual implementation/full tracked+untracked diff including
font binary/license and evidence. One bounded named correction/SAME-reviewer recheck;
no subagents/verifiers/reviewer chains. No renewed handoff permission requests; rejected
delivery stops that action with recorded block, no bypass/retry/false success.
Bootstrap ONLY central AGENTS/TICKETS, never earlier dirty source. Layout/CSS overlaps
with prior unmerged batches stay explicit for later integration. Workers stop after
handoff; planner continues under standing authority. No stage/commit/push/PR/merge/
deploy/source integration, real-data repair or running-user-app restart.

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
No active worker or successor batch dispatched. Continuous execution and the approved
implementer -> reviewer -> planner routing remain authorized. Next: obtain the pending
C20 decision on changes/removal of objectives with submitted evidence, freeze its
bounded contract, then dispatch eligible competitive-history work. C33 needs its
smallest freshness/inspection contract; C36 needs legacy policy and writer integration.
Manual acceptance, source integration/publication and user-data repair remain separate.

## Finalized-pre-Live roster batch C11 — approved contract and readiness history

**Readiness cleared; implementation go-ahead — 2026-09-14.** Sole reviewer
`01a09f79-5131-74e2-89f6-f07b219f22af` returned READY after the bounded R1–R4
plan recheck. [Readiness recheck](/private/tmp/c11-evidence/readiness-recheck.md) and
[frozen technical plan](/private/tmp/c11-evidence/readiness-plan.md) (SHA256
`947a95795c660eafd0e82171ad99fa7605f73fb02b28d05d40c70148c80008be`) are the final
implementation/review baseline alongside the approved product contract below.
Implementer `01a09f71-ea16-7ad2-8620-ad5de1823736` continues in
`/Users/christopher/.codex/worktrees/c4b5/BingoWebpage`, branch
`codex/finalized-prelive-roster-c11`, base `c165bbc`. At READY, production/test diff
was empty and 25 manifest hashes/exact readiness patch were verified; no product
check was executed. Status at this readiness checkpoint was In progress; the final result above supersedes it.

R1–R4 are required direct-journey details: atomic non-overwriting private-note append
with existing limit/privacy/replay/concurrency handling; reserved current identity
separate from exact retained pick identity; existing finalized-pre-Live Captain POST
and current roster republication in one transaction; departed-owner own-status notice
separate from leadership vacancy alerts and independent Admin overlap. Use existing
mechanisms under the zero-new-infrastructure budget. No C09/C17 import or Live-path
repair, Running/Paused extension, new role policy or retroactive history synthesis.
The exact focused PostgreSQL/HTTP/concurrency/fault checks in the frozen plan must
be executed and evidenced, not claimed from this readiness pass. Send final full diff
and evidence ONLY to this SAME reviewer; no new readiness/final reviewer or broad
restart. Planner proceeds only on a subsequent FINAL pass, not this READY verdict.

**Planner dependency resolution — 2026-09-14:** C17 already establishes Admin-safe
versus ordinary-leadership Teams destinations. Implementer source lookup confirms the
baseline Live withdrawal still sends both recipient classes to Admin-only context.
For C11's NEW finalized-pre-Live withdrawal branch only, apply the minimal current
recipient-role distinction using existing mechanisms: enabled Admins (including
leadership/Admin overlap) -> existing event/participant Admin route; ordinary remaining
Captain/co-captain -> existing published Teams route. Validate the destination through
actual recipient HTTP navigation and preserve atomic/deduplicated notification creation,
privacy, event scope and active-publication guard. No new service/table/page/dependency.
Keep existing Live C17 path untouched; do not import its isolated source or rewrite old
notifications. Later integration reconciles shared routing. This is the necessary
C11 integration of already-approved destination behavior, not a new notification policy.
Include it in the pending one-time readiness plan/review; production still waits for
planner go-ahead after READY. No other source import/scope expansion is authorized.

User approved the planner recommendation: enabled Admin departure -> visible vacancy
-> optional explicit validated waiting-list/internal replacement after draft FINALIZED
and before ACTUAL Live start; update current published roster while preserving prior
snapshots/picks. Running/Paused departures are explicitly deferred; no all-U3-02 closure
claim. FUNCTIONAL_CONTRACTS 5.6 and PRODUCT_REQUIREMENTS 18.2 now own this policy.
Preserve normal visibility/terminal/future-end restrictions, account reservations,
current authority revocation, Captain readiness/warnings, notification routing/privacy
and immutable history. Pre-Live changes are prospective at confirmation; actual start
still gates evidence. Keep Live whole-minute behavior unchanged. No self-withdrawal,
automatic promotion/Captain selection or draft reopening/history erase as a shortcut.

Fresh saved-project Astra xhigh implementer directly owns this single connected
journey. Existing C11/DELIVERY_PLAN 4.1 requires EXACTLY ONE independent readiness
review before coding: first make a short local technical plan/contract covering real
UI/service/snapshot/notification path, controlled fixtures, transaction/authorization/
publication history, stale/concurrent/failure cases and integration dependencies.
Initial complexity budget: reuse existing entities, services, routes and notification/
publication mechanisms; zero new framework/service/page/job/dependency by default.
Any demonstrated necessary addition must be explained to planner before implementation.
No bulk retained-data scan/repair or undocumented policy workaround.

Create exactly ONE fresh saved-project Astra xhigh reviewer at that point. That
reviewer first performs the required read-only readiness review, not code acceptance.
Findings/incomplete go ONLY to implementer; READY verdict goes ONLY to planner
`01a09c57-1f38-7191-94de-e11647ffbcf1`. Planner records readiness and sends existing
implementer go-ahead. Do not implement production before that go-ahead. Same reviewer
later reviews the final full diff; no second reviewer/task chain. Do not repeat broad
readiness after it passes; ordinary defects use one bounded named remediation/recheck.
A readiness pass is not the final batch pass and must not trigger a successor batch.

After readiness go-ahead: implement direct existing SignupService/Participant/Draft
reachability and required current roster publication/authority integration. Focused
real auth/antiforgery/PostgreSQL/HTTP journey: finalized/pre-Live departure, visible
vacancy, waiting-list and internal fill, optional unfilled vacancy, preserved old picks/
snapshots/reservations, accurate new published roster, privacy/notification destinations,
Captain/start readiness and valid current eligibility. Check stale/double fill and
required atomic failure boundaries, normal Live regression and denied Running/Paused/
terminal/unauthorized actions. Use controlled disposable fixtures; no user app reset,
screenshots/manual preflight or full suite by default. Manual acceptance stays deferred.

Prior thirteen reviewed batches are isolated/unmerged; bootstrap ONLY central AGENTS/
TICKETS, and promote only focused approved authority clauses locally. Last verified
base c165bbc. Especially C10/C12/C15/C16/C17/C18/C28/C38 overlaps must be named in
readiness; no unapproved dirty-source integration. Existing behavior may be reused
where present, not assumed merged. If a safe journey truly depends on prior source,
report that dependency instead of silently duplicating or importing it.

Implementer sends readiness/review/recheck ONLY to its one reviewer, no checkpoint or
dispatch notice to planner. Final PASS only to planner; required findings/incomplete
only to implementer. Standing sharing approval covers necessary private paths/hashes/
findings/evidence to exact assigned destinations. No repeat handoff permission request;
automatic rejection stops delivery without bypass/retry/false success. No subagents/
extra verifiers/model fallback/Ponytail. Worker stops after handoff; planner continues
after FINAL pass. No stage/commit/push/PR/merge/deploy/integration/data repair.

## Dependencies and proposed planning order

This is a proposal to approve through DELIVERY_PLAN, not an instruction to start work.

1. D03 is closed without implementation; D05 is deferred; D07 and D01/D02/D04/D06 are resolved. Address bounded
   scope questions on C11/C20/C33/C38 when selecting those tickets. C27 is closed.
   Unrelated corrections need not wait for a deferred feature or decision.
2. Prioritize currently exposed signup integrity C05/C06/C08/C09 and authorization/
   privacy C12/C28/C39. C36 is closed; retain already-approved C04/C15/C22 writer fixes. Those lower-severity
   prerequisites may therefore move ahead of unrelated P1 tickets.
3. Address competitive/history and lifecycle blockers C10/C11/C16/C18/C19/C20/C21/
   C23/C32/C33/C38 in an approved dependency-aware batch. C20/C21 and C15/C16 share
   files; serialize writers rather than implementing overlapping edits concurrently.
4. Schedule remaining corrections by affected workflow and readiness, keeping tickets
   individually reviewable. Do not combine all auditing or all concurrency into one
   large implementation ticket. T01–T05 should restore their relevant gates before
   accepting changes that rely on those tests; they need not block unrelated work.
5. Close cross-ticket regression and retained-data obligations for the agreed package.
   Packaging/publication require their own explicit authorization.

## Ticket index

| ID | Kind / priority | Finding(s) / source | Status | Title |
| --- | --- | --- | --- | --- |
| [C01](#c01) | Correction / P2 | U1-01, U1-02 | Done | Recover invalid event-creation schedules |
| [C02](#c02) | Correction / P2 | U1-05 | Done | Allocate collision-free generated event slugs |
| [C03](#c03) | Correction / P2 | U1-03 | Done | Require the public-text signup opening warning |
| [C04](#c04) | Correction / P2 | U1-04 | Done | Commit signup-code changes with event-scoped audit |
| [C05](#c05) | Correction / P1 | U2-01 | Done | Preserve corrected characters through signup restoration |
| [C06](#c06) | Correction / P2 | U2-03, U2-04 | Done | Preserve Discord link and replacement session semantics |
| [C07](#c07) | Correction / P2 | U8-03 | Done | Record successful Discord logins |
| [C08](#c08) | Correction / P2 | U2-05 | Done | Reject stale My Accounts edits |
| [C09](#c09) | Correction / P2 | U2-02 | Done | Keep signup lifecycle notifications on the owned record |
| [C10](#c10) | Correction / P1 | U3-01 | Done | Make internal participants reachable in the ordinary draft |
| [C11](#c11) | Correction / P1 | U3-02 | Done | Enable the contracted pre-Live post-draft departure journey |
| [C12](#c12) | Correction / P1 | U3-03 | Done | Reject cross-event roster moves |
| [C13](#c13) | Correction / P2 | U3-04, U3-11 | Done | Make CSV validation preserve the primary column and recover errors |
| [C14](#c14) | Correction / P2 | U3-05 | Done | Number waiting participants independently of preformed members |
| [C15](#c15) | Correction / P2 | U3-06 | Done | Commit draft and team mutations with scoped audit |
| [C16](#c16) | Correction / P2 | U3-07 | Done | Recheck Captain presence at draft finalization |
| [C17](#c17) | Correction / P2 | U3-08, U3-10 | Done | Route team notifications by recipient and publication phase |
| [C18](#c18) | Correction / P2 | U3-09 | Done | Activate the authoritative primary character for live replacements |
| [C19](#c19) | Correction / P1 | U4-01 | Done | Restrict manual EHB to manual objectives |
| [C20](#c20) | Correction / P1 | U4-02 | Done | Preserve objective identity through board corrections |
| [C21](#c21) | Correction / P1 | U4-03 | Done | Retain artwork referenced by approval snapshots |
| [C22](#c22) | Correction / P2 | U4-04 | Done | Commit ordinary board mutations with event-scoped audit |
| [C23](#c23) | Correction / P2 | U4-05 | Done | Reject approval of stale displayed catalogue inputs |
| [C24](#c24) | Correction / P2 | U4-06 | Done | Protect shared catalogue item edits and record their changes |
| [C25](#c25) | Correction / P2 | U4-08 | Done | Return actionable board approval validation |
| [C26](#c26) | Correction / P2 | U4-09 | Closed — no change | Show bounded dependency references when catalogue deletion is blocked |
| [C27](#c27) | Correction / P2 | U5-01 | Closed — no change | Make unknown-outcome upload retry safe |
| [C28](#c28) | Correction / P2 | U5-02 | Done | Recheck submission creation authority inside its mutation boundary |
| [C29](#c29) | Correction / P2 | U5-03 | Done | Enforce the Live competition replacement-only rule |
| [C30](#c30) | Correction / P2 | U5-04 | Done | Add the contracted submission status filter |
| [C31](#c31) | Correction / P2 | U6-01 | Done | Keep finalization pending blocker links event-scoped |
| [C32](#c32) | Correction / P1 | U6-02 | Done | Keep submissions closed after unfinalization |
| [C33](#c33) | Correction / P1 | U6-03 | Done | Invalidate stale finalization inputs and completion inspections |
| [C34](#c34) | Correction / P2 | U6-04 | Closed — no change | Enforce the required reversal confirmation boundary |
| [C35](#c35) | Correction / P2 | U6-05 | Done | Audit contribution changes caused by reversal rebalance |
| [C36](#c36) | Correction / P1 | U7-01 | Closed — no change | Protect hidden-event audit visibility, including retained rows |
| [C37](#c37) | Correction / P2 | U7-02 | Done | Show generic cancellation on all public event destinations |
| [C38](#c38) | Correction / P1 | U8-01 | Done | Remove the emergency-only event-start dependency loop |
| [C39](#c39) | Correction / P2 | U8-02 | Done | Reject stale account administration confirmations |
| [C40](#c40) | Correction / P2 | U8-04 | Deferred | Add target-identifier filtering to audit |
| [C41](#c41) | Correction / P3 | FONT-01: user-reported public font swap | Done | Finish early loading of used public fonts |
| [D01](#d01) | Authority/decision / Authority | U1-C1 | Done | Reconcile Rules wording with the accepted HowTo decision |
| [D02](#d02) | Authority/decision / Authority | Unit 3 authority clarification | Done | Document the unpublished-draft cancellation exception |
| [D03](#d03) | Authority/decision / Decision | U4-07 | Closed — no change | Decide the catalogue import surface |
| [D04](#d04) | Authority/decision / Authority | Unit 5 zoom authority conflict | Done | Reconcile evidence zoom authority with accepted interaction |
| [D05](#d05) | Authority/decision / Decision | U7-C1 | Deferred | Decide restoration when another event is current |
| [D06](#d06) | Authority/decision / Authority | Unit 8 inbox scope remnant | Done | Reconcile the accepted Admin inbox WIP boundary |
| [D07](#d07) | Authority/decision / Decision | public-header-popover.test.js failure | Done | Decide whether whole-script repeat evaluation is supported |
| [T01](#t01) | Test / Test | Three failing BrowserTests C# assertions | Done | Repair three stale C# UI source assertions |
| [T02](#t02) | Test / Test | Node public-evidence, public-recent-drops, team-board-overlay failures | Done | Replace superseded public UI assertions with current invariants |
| [T03](#t03) | Test / Test | public-leaderboards.test.js failure | Done | Correct the leaderboard classList test double |
| [T04](#t04) | Test / Test | transient-toast.test.js failure | Done | Load a complete toast test context |
| [T05](#t05) | Test / Test | public-header-popover.test.js failure | Done | Test the agreed shared-header initialization contract |

## Accepted dispositions — 2026-09-09

These decisions supersede the earlier recommendations below; no application
implementation is dispatched by this record.

- **C36 closed without implementation**, user clarification 2026-09-14: hiding is not an Admin-audit secrecy boundary. No legacy cleanup is required.

- Closed with no product change: **C34** (extra reversal mechanism) and **C27**
  (uncertain upload receipt can be checked in `/Submissions`).
- Documentation reconciled and complete: **D01/D02/D04/D06**. D06 explicitly
  preserves Admin actions already on Notifications; only `/Admin` is WIP.
- Closed without implementation (user clarification 2026-09-14): **C26** reference-list convenience and **D03** application import. Neither is remaining work.
- Deferred: **D05** extra restore/current-event handling and **C40** outside this batch, pending separately agreed Audit scope.
- Retained planning direction: **C30** status filter at the right of the submissions
  divider where suitable, using only the supplied Notifications screenshot's
  placement pattern; **C11** a good, narrowly scoped pre-Live departure solution.
- **D07** subsequently accepted: component reinitialization is the supported
  behavior; **T05** approved for a bounded test-only repair. Font work is separate.

Current totals including C41: 18 Awaiting manual acceptance, 2 Blocked, 2 Closed — no change, 4 Deferred, 6 Done, 21 Proposed (53 tickets). Numbered sweep findings remain traceable; a closed ticket is not
reported as an implemented fix. Existing page approvals remain unchanged; batch
implementation and evidence are recorded in the ticket Outcomes.

## Disposition triage — recommendations 2026-09-09

User requested a challenge of whether each ticket should actually be implemented,
including stale functionality contracts. This was a bounded review of the backlog
and relevant accepted decisions, not another sweep or test run. These earlier recommendations are retained as decision provenance. The accepted
dispositions above and index now own current status; they do not authorize unrelated
implementation or product changes.

[Detailed correction triage](/private/tmp/bingo-ticket-triage-20260909.md) records
supporting source/authority. The recommendations needed for planning are retained
here so that temporary evidence is not the only record.

| Tickets | Classification | Proposed disposition and reason |
| --- | --- | --- |
| C34 | Unsupported remedy on present evidence | Close with no product change. Review already requires native confirmation, reason, current version and authorization. Contract7.5 does not explicitly require a second server confirmation flag (unlike board publication); no ordinary-flow bypass was shown. A posted boolean is not an independent security boundary. Preserve existing safeguards. |
| C11 | Genuine missing journey; proposed remedy too broad | Keep, but first bound the missing finalized-pre-Live departure/replacement path. Do not automatically expand it into a complete Running/Paused/Finalized roster redesign. Current product/functional authority explicitly supports post-draft departure. |
| C27 | Genuine uncertain-response recovery gap; stronger guarantee not specified | Keep honest unknown-outcome feedback and usable history/reconciliation as the minimum. Decide separately whether automatic retry needs persistent attempt identity. Intentional pending copies are allowed; duplicate pending records alone do not prove duplicate approved credit. |
| C26 | Contracted diagnostics feature | Optional for a defect-first batch: blocked deletion already protects data and offers deactivation. Decide whether reference labels/links are wanted now; defer only with an explicit scope decision. |
| C30 | Contracted ledger feature | Optional for a defect-first batch: full history/player/search work; status filtering is explicitly retained in the current contract and matrix. It is a feature omission, not evidence the ledger authority is broken. |
| C40 | Contracted operational convenience | Optional for a defect-first batch: target-ID filtering is specified but existing audit searches work. Keep separate from C36's privacy issue. |
| D01, D02, D04, D06 | Later accepted decisions already exist | Documentation-only reconciliation: HowTo replaces Rules-editor scope; unpublished-draft cancellation is a narrow lock exception; click-focused evidence zoom has no separate control bar; Admin inbox/dashboard WIP is explicitly accepted. Do not implement old contract wording. |
| D03 | Product scope decision | Recommend deferring a new application catalogue-import interface unless it is actually needed. Existing operator tooling does not fulfill that app contract, but the mismatch is not permission to build a new feature. User must choose/reconcile the intended surface. |
| D05 | Genuine unresolved product invariant | Decide what Restore does when another event is current. Recommend a clear fail-closed conflict response rather than changing either event's lifecycle automatically; this is a proposal, not current authority. |
| D07 / T05 | Unsupported robustness requirement needs disposition | Recommend testing supported component reinitialization, since current layouts load site.js once and use content-updated hooks. No normal trigger for whole-script repeat evaluation was established. Resolve the requirement before changing the test; add no production loader machinery by assumption. |
| T01–T04 | Test repairs still warranted | Correct stale assertions and inaccurate/incomplete fixtures while retaining real current behavior checks. Their early failures leave later assertions unexecuted; do not delete tests simply to get green. |

Other correction tickets remain proposed repairs on the available evidence; this is
not a new execution/reproduction claim. In particular C03 (public-text warning),
C16 (Captain check), C19 (standard EHB), C23 (catalogue freshness), C29 (Live no-clear)
and C37 (generic cancelled state) have explicit current policy support. They may be
changed by a new product decision, but the triage found no superseding decision that
would justify calling them stale. Preserve clear loss-of-progress, character/history,
authorization, privacy, atomicity and broken-destination findings.

D07 is resolved and T05 is complete. Next planning action is to choose the next
bounded repair batch; C41 is a proposed font ticket. Do not require unrelated product decisions before a clear,
independent correction can be approved. No application or source-of-truth behavior
has changed through this triage.

## Ticket briefs

Common protections and completion rules above apply to every active ticket. Accepted dispositions in Outcome supersede a closed/deferred ticket’s originally proposed implementation acceptance.

<a id="c01"></a>
### C01 — Recover invalid event-creation schedules

- **Current acceptance — 2026-09-14: Done.** User passed the mapped manual checks and accepted MR-26 internal evidence on the independently passed integrated candidate. Earlier outcome paragraphs retain historical per-batch evidence and limits; their pending-manual wording is superseded. This does not authorize packaging or publication.

- **Evidence:** U1-01, U1-02; [unit 1 report](/private/tmp/bingo-sweep-unit1-20260909.md). Priority/type: P2.
- **Problem:** Create accepts past dates and calls domain schedule validation outside recoverable handling; valid-format signup close after start can escape to the generic error page.
- **Scope / authority:** Admin/Events/Create.cshtml.cs; existing schedule/domain validation. FUNCTIONAL_CONTRACTS §§4.1–4.4.
- **Acceptance:** New optional timestamps, including competition-derived inputs, meet current future/order rules. Invalid close/start or passed dates retain form inputs, show actionable validation, and create no event. Valid minimal and future-dated creation still work; unchanged historical Schedule values keep their existing policy.
- **Focused checks:** Extend the existing isolated creation HTTP scenario with the two invalid branches and a valid result; assert no failed-create residue.
- **Dependencies / decisions:** None
- **Existing data:** Existing drafts with passed dates may exist. Identify their supported recovery path; do not change historical timestamps or repair drafts automatically.
- **Outcome:** Awaiting manual acceptance — implemented 2026-09-13 by Astra xhigh task `01a09bb1-0844-7d90-b284-bc079da778ef`. Create checks all five effective optional timestamps after mocked/real competition lookup, returns localized field feedback for passed dates and existing domain ordering errors, and preserves posted inputs before any persistence/upload. Focused authenticated HTTP cases passed for five passed boundaries, three ordering failures, four competition-derived failures, minimal/future creation and no failed-create event/form/question/audit/competition/banner residue. Existing valid creation and historical Schedule regression checks passed. Existing drafts keep passed Schedule boundaries locked and unchanged. Still-future values remain editable under the existing lifecycle rules. If a different passed boundary is necessary, use the existing confirmed Discard for an unprotected draft, or Cancel where protected data prevents discard, then create a replacement. No existing-data scan, timestamp rewrite or automatic repair was performed. Independent reviewer `01a09bc3-7d7b-7040-b6b8-1295f20197f7` (Astra xhigh, local) was created after direct user approval resolved the initial automatic approval block. Independent review complete with no required finding for C01. C03 R1/R2 also cleared independent recheck; the coherent batch now awaits the user-deferred manual acceptance. [Review report](/Users/christopher/Documents/Codex/2026-09-13/event-setup-ticket-batch/outputs/independent-review.md). Evidence: [implementation handoff](/Users/christopher/Documents/Codex/2026-09-13/event-setup-ticket-batch/outputs/implementation-handoff.md). No full-suite/package/release gate or publication is claimed.

- **Remaining acceptance:** Awaiting manual acceptance only at the ticket level. C01/C02 original review and the [C03 fixes-only recheck](/Users/christopher/Documents/Codex/2026-09-13/event-setup-ticket-batch/outputs/remediation-review.md) are clear; implementation and focused checks are complete, with no required remediation. Recheck reused the 31 passing affected cases and zero-warning Release build without reruns. Package/release gates remain separate and unrun.

<a id="c02"></a>
### C02 — Allocate collision-free generated event slugs

- **Current acceptance — 2026-09-14: Done.** User passed the mapped manual checks and accepted MR-26 internal evidence on the independently passed integrated candidate. Earlier outcome paragraphs retain historical per-batch evidence and limits; their pending-manual wording is superseded. This does not authorize packaging or publication.

- **Evidence:** U1-05; [unit 1 report](/private/tmp/bingo-sweep-unit1-20260909.md). Priority/type: P2.
- **Problem:** Creating a duplicate display name with blank slug generates the existing slug and returns an error instead of choosing another valid generated slug.
- **Scope / authority:** Admin/Events/Create.cshtml.cs and existing EventSlugGenerator. Contract §4.2.
- **Acceptance:** Two identical display names with blank slug create distinct stable URLs. Explicit conflicting slugs retain clear validation. Concurrent automatic allocation cannot overwrite or expose a partial event.
- **Focused checks:** Focused database creation scenario for duplicate automatic names and explicit conflict; cover allocation retry under a collision without a load-test project.
- **Dependencies / decisions:** None
- **Existing data:** No retained-data rewrite expected; preserve existing records.
- **Outcome:** Awaiting manual acceptance — implemented 2026-09-13 by the same Astra xhigh task. Automatic names allocate length-safe numeric suffixes without changing existing URLs. Explicit conflicts remain validation errors. The existing serializable creation transaction rolls back rows/uploads on the exact PostgreSQL slug unique-index violation and retries automatic allocation up to three persistence attempts. Passing disposable-PostgreSQL cases cover duplicate/long names, explicit conflicts, an intervening winning creation, explicit-race rejection and exhausted-retry cleanup; six generator cases passed. Independent reviewer `01a09bc3-7d7b-7040-b6b8-1295f20197f7` (Astra xhigh, local) was created after direct user approval resolved the initial automatic approval block. Independent review complete with no required finding for C02. C03 R1/R2 also cleared independent recheck; the coherent batch now awaits the user-deferred manual acceptance. [Review report](/Users/christopher/Documents/Codex/2026-09-13/event-setup-ticket-batch/outputs/independent-review.md). Evidence: [implementation handoff](/Users/christopher/Documents/Codex/2026-09-13/event-setup-ticket-batch/outputs/implementation-handoff.md). No full-suite/package/release gate or publication is claimed.

- **Remaining acceptance:** Awaiting manual acceptance only at the ticket level. C01/C02 original review and the [C03 fixes-only recheck](/Users/christopher/Documents/Codex/2026-09-13/event-setup-ticket-batch/outputs/remediation-review.md) are clear; implementation and focused checks are complete, with no required remediation. Recheck reused the 31 passing affected cases and zero-warning Release build without reruns. Package/release gates remain separate and unrun.

<a id="c03"></a>
### C03 — Require the public-text signup opening warning

- **Current acceptance — 2026-09-14: Done.** User passed the mapped manual checks and accepted MR-26 internal evidence on the independently passed integrated candidate. Earlier outcome paragraphs retain historical per-batch evidence and limits; their pending-manual wording is superseded. This does not authorize packaging or publication.

- **Evidence:** U1-03; [unit 1 report](/private/tmp/bingo-sweep-unit1-20260909.md). Priority/type: P2.
- **Problem:** Shared signup readiness omits the required warning that text-question answers will be public.
- **Scope / authority:** EventSignupLifecycleService/EventReadinessEvaluator and existing warning controls/resources. Contract §4.5; PRODUCT_REQUIREMENTS public-answer rules.
- **Acceptance:** Applicable public text questions produce the existing acknowledgment flow for manual and scheduled opening. No text question means no false warning; accepted acknowledgments remain scoped to current readiness.
- **Focused checks:** One parameterized readiness/opening scenario covering text/no-text and manual/scheduled acknowledgment; no duplicate visual redesign.
- **Dependencies / decisions:** None
- **Existing data:** Previously opened events are not closed or republished by this correction.
- **Outcome:** Awaiting manual acceptance — initial implementation and independent review completed; reviewer `01a09bc3-7d7b-7040-b6b8-1295f20197f7` identified R1/P2 (stale first warning acknowledgment) and R2/P2 (unlocalized disclosure). The user then directly authorized remediation. Both were corrected by Astra xhigh implementer `01a09bb1-0844-7d90-b284-bc079da778ef`: displayed codes now remain explicit through cached acknowledgment and final transactional Open/Reopen readiness; both warning lists use the shared localizer. Four HTTP regressions reproduced the original failures. After correction, 31 affected checks and the zero-warning Release build, scoped formatting and diff checks passed. No known remaining implementation/check blocker is recorded, and fresh independent Astra xhigh recheck `01a09bd5-4658-7480-804c-c83fd5657781` cleared R1/R2 and their direct consequences with no required findings; the coherent batch now awaits the user-deferred manual acceptance. [Remediation handoff](/Users/christopher/Documents/Codex/2026-09-13/event-setup-ticket-batch/outputs/remediation-handoff.md). No other ticket implementation, user-data change, full-suite/package/release gate, publication or additional review task was performed.

- **Remaining acceptance:** Awaiting manual acceptance only at the ticket level. C01/C02 original review and the [C03 fixes-only recheck](/Users/christopher/Documents/Codex/2026-09-13/event-setup-ticket-batch/outputs/remediation-review.md) are clear; implementation and focused checks are complete, with no required remediation. Recheck reused the 31 passing affected cases and zero-warning Release build without reruns. Package/release gates remain separate and unrun.

<a id="c04"></a>
### C04 — Commit signup-code changes with event-scoped audit

- **Current acceptance — 2026-09-14: Done.** User passed the mapped manual checks and accepted MR-26 internal evidence on the independently passed integrated candidate. Earlier outcome paragraphs retain historical per-batch evidence and limits; their pending-manual wording is superseded. This does not authorize packaging or publication.

- **Evidence:** U1-04; [unit 1 report](/private/tmp/bingo-sweep-unit1-20260909.md). Priority/type: P2.
- **Problem:** Questions saves code configuration before writing audit, and the selected audit overload leaves EventId unset.
- **Scope / authority:** Admin/Events/Questions.cshtml.cs SignupCode handler; existing AuditWriter transaction pattern. Contracts §§2.3,4.5.
- **Acceptance:** Configuration and accurate before/after audit commit together with EventId. Audit failure rolls back code changes; normal enabled/disabled code behavior remains unchanged.
- **Focused checks:** Reuse isolated signup-code POST with an audit-save failure and a successful event-scoped audit.
- **Dependencies / decisions:** None
- **Existing data:** Existing missing or unassociated audit evidence is handled by C36; never fabricate missing history.
- **Outcome:** **Awaiting manual acceptance.** Implemented in `/Users/christopher/.codex/worktrees/ffd6/BingoWebpage`, branch `fix/audit-atomicity-ticket-batch`, base `c165bbc`. Astra xhigh reviewer `01a09c49-cfcc-7d42-8b9d-f6f2f07cc4aa` passed the batch after its bounded C22 R1 recheck; no required findings remain. Initial 44/44 focused cases remain applicable to unaffected behavior; the corrected removal case passed 1/1 after reproducing the defect. Release compilation and scoped formatting/whitespace evidence recorded. [Review report](/private/tmp/audit-batch-review/reviewer-report.md). Manual acceptance is deferred; no historical-data repair or release/packaging acceptance implied.

<a id="c05"></a>
### C05 — Preserve corrected characters through signup restoration

- **Current acceptance — 2026-09-14: Done.** User passed the mapped manual checks and accepted MR-26 internal evidence on the independently passed integrated candidate. Earlier outcome paragraphs retain historical per-batch evidence and limits; their pending-manual wording is superseded. This does not authorize packaging or publication.

- **Evidence:** U2-01; [unit 2 report](/private/tmp/bingo-sweep-unit2-20260909.md). Priority/type: P1.
- **Problem:** My Accounts correction updates assignment character IDs but leaves saved SignupAnswer IDs stale. Withdrawal then rejoin/restore can succeed without the required primary assignment.
- **Scope / authority:** MyAccountsService.ApplyCharacterCorrectionAsync and SignupService.ReacquireAssignmentsAsync; existing answer/response version updates. Contracts §§5.2–5.3.
- **Acceptance:** Editable assignments and their saved account answers stay consistent atomically. Withdraw/rejoin and Admin restore reacquire the corrected selections; history, released reservations and non-editable event snapshots remain protected. Conflict leaves all related state unchanged.
- **Focused checks:** A normal signup containing real answer rows → character correction → withdrawal → rejoin/Admin restore, with a conflicting reservation case. Existing assignment-only fixture is insufficient.
- **Dependencies / decisions:** None
- **Existing data:** Determine whether an unambiguous mismatch detector can identify affected retained signups. Report counts/recovery proposal using isolated data first; any repair or production scan requires separate authorization.
- **Outcome:** Implemented and independently reviewed clear 2026-09-09 under the user-authorized first trial, DELIVERY_PLAN17.12. The Luna Max implementer updated saved account answers atomically with editable assignments and added real-answer owner rejoin/Admin restore plus conflict rollback proof. Four focused PostgreSQL cases and scoped format/diff checks pass; fresh Astra Medium review found no code blocker or named remediation. Disposable mismatch detection found the one synthetic affected row, but production count is unknown. The existing same-name correction returns early and is not a retained-data repair. A separately authorized recovery must transactionally set answers to the unique still-editable active assignment and increment response version after rechecking eligibility/conflict, leaving released, historical and ambiguous rows untouched. User delegated disposition to the planner on 2026-09-14; old-data inspection/repair is explicitly deferred to avoid speculative cleanup. Awaiting manual acceptance, retaining the existing independent code PASS. No production scan, history rewrite or additional verification; actual affected-user count remains unknown. Revisit only a concrete reported failure, not an automatic cleanup task.

<a id="c06"></a>
### C06 — Preserve Discord link and replacement session semantics

- **Current acceptance — 2026-09-14: Done.** User passed real Discord linking, existing-account login and Last login checks. Second-account replacement manually waived and accepted explicitly because user has no second Discord account; automated/review evidence accepted. Earlier pending-manual wording is superseded.

- **Evidence:** U2-03, U2-04; [unit 2 report](/private/tmp/bingo-sweep-unit2-20260909.md). Priority/type: P2.
- **Problem:** Settings BeginLink reads an unpopulated DiscordLinked property on POST; replacement is labeled Link. Callback also issues a Discord session after password-reauthenticated settings work.
- **Scope / authority:** Account/Settings.cshtml.cs, DiscordCallback.cshtml.cs and existing principal factory. Contract §5.1; TECHNICAL_ARCHITECTURE identity/session rules.
- **Acceptance:** Purpose derives from the verified account; link versus replacement feedback/history are correct. Successful password-reauthenticated linking/replacement returns the prescribed password session, so later password change/reset invalidates it. Ordinary Discord login retains its independent policy.
- **Focused checks:** Follow Settings POST through controlled callback; assert purpose/audit and resulting session behavior after password-version change. Preserve collision/replay guards.
- **Dependencies / decisions:** None
- **Existing data:** Do not retroactively relabel past audit entries or invalidate unrelated existing sessions as part of this ticket.
- **Outcome:** Implemented 2026-09-13 by the assigned Astra xhigh implementer, directly without delegation. Settings derives link/replace intent from the password-verified account; successful callbacks issue password principals with the current password/authorization versions. Two controlled HTTP Settings → OAuth → callback cases passed, including wrong-password denial, purpose, transition/audit/feedback, prior-session invalidation, and rejection of the new password session after a password-version change while an ordinary Discord principal remains valid. Existing intent/collision/replay and password-session checks passed in the 20/20 batch run. No historical relabeling or real provider/account actions. Independent Astra xhigh review cleared 2026-09-13 with no actionable findings or scope deviations; reviewer confirmed the recorded 20/20 test results without rerunning them. Manual acceptance deferred by the user until the combined walkthrough; no manual visual or release acceptance claimed. Exact commands and diff boundary are in CURRENT_STATUS's active account handoff.

- **Remaining acceptance:** Awaiting manual acceptance — implementation, required focused checks and independent review are complete; no required remediation. User deferred the manual walkthrough until all ticket work is complete (2026-09-13). Package-level release gates remain separate.

<a id="c07"></a>
### C07 — Record successful Discord logins

- **Current acceptance — 2026-09-14: Done.** User passed real Discord linking, existing-account login and Last login checks. Second-account replacement manually waived and accepted explicitly because user has no second Discord account; automated/review evidence accepted. Earlier pending-manual wording is superseded.

- **Evidence:** U8-03; [unit 8 report](/private/tmp/bingo-sweep-unit8-20260909.md). Priority/type: P2.
- **Problem:** Existing-account Discord callback issues authentication without persisting RecordLogin, leaving Accounts last-login blank or stale.
- **Scope / authority:** Account/DiscordCallback.cshtml.cs and existing account authentication persistence path. Contract §9.4.
- **Acceptance:** A successful Discord login records the authoritative login time before completion. Failed/rejected login does not advance it; password login behavior remains intact.
- **Focused checks:** Controlled callback persistence test for successful and rejected login; verify the Accounts projection observes the saved value.
- **Dependencies / decisions:** None
- **Existing data:** Historical unknown login times remain unknown; no invented backfill.
- **Outcome:** Implemented 2026-09-13 by the assigned Astra xhigh implementer. Existing-account Discord callbacks use the existing authentication service to reject unavailable accounts and persist RecordLogin with its TimeProvider before issuing the principal. Four PostgreSQL callback cases passed: first known login, advancing an existing timestamp, disabled-account rejection, and provider-ticket failure. The Accounts projection reads the persisted expected value; rejected cases retain the old timestamp and issue no principal. No routine-login audit or historical timestamp backfill added; existing password-session checks passed in the 20/20 batch run. Independent Astra xhigh review cleared 2026-09-13 with no actionable findings or scope deviations; reviewer confirmed the recorded 20/20 test results without rerunning them. Manual acceptance deferred by the user until the combined walkthrough; no real Discord or user-owned database actions. Exact commands and diff boundary are in CURRENT_STATUS's active account handoff.

- **Remaining acceptance:** Awaiting manual acceptance — implementation, required focused checks and independent review are complete; no required remediation. User deferred the manual walkthrough until all ticket work is complete (2026-09-13). Package-level release gates remain separate.

<a id="c08"></a>
### C08 — Reject stale My Accounts edits

- **Current acceptance — 2026-09-14: Done.** User passed the mapped manual checks and accepted MR-26 internal evidence on the independently passed integrated candidate. Earlier outcome paragraphs retain historical per-batch evidence and limits; their pending-manual wording is superseded. This does not authorize packaging or publication.

- **Evidence:** U2-05; [unit 2 report](/private/tmp/bingo-sweep-unit2-20260909.md). Priority/type: P2.
- **Problem:** My Accounts posts no expected link version, so an older tab can overwrite a newer completed edit.
- **Scope / authority:** Account/MyAccounts.cshtml(.cs), MyAccountsService.UpdateAsync, existing AccountOsrsCharacter.Version. Contract §5.2.
- **Acceptance:** Rendered edits round-trip current version and reject stale label/EHB/character changes without residue, preserving safe entered data and a clear reload path. Owner authorization and correction rules remain intact.
- **Focused checks:** Two sequential stale forms, not merely simultaneous EF writes; successful current edit and cross-owner denial.
- **Dependencies / decisions:** None
- **Existing data:** No attempt to infer or reconstruct previously overwritten values.
- **Outcome:** Implemented 2026-09-13 by the assigned Astra xhigh implementer. List projections and edit forms round-trip AccountOsrsCharacter.Version; UpdateAsync checks it inside the existing owner-scoped transaction before creating/correcting a character or changing preferences/assignments. Missing versions fail safely. Failed updates and WOM fetch round-trips retain the submitted version and safe entered values; localized existing conflict text and a Reload current values link provide recovery. English/Danish HTTP cases passed for two sequential forms, stale fetch/save/retry, unchanged winner and history, no created loser character, unaffected other row, explicit reload, and successful fresh-version correction. Existing owner-denial, correction/conflict, ordering, onboarding, and localized signup/WOM checks also passed in the 20/20 batch run. No schema/history repair or UI redesign. Independent Astra xhigh review cleared 2026-09-13 with no actionable findings or scope deviations; reviewer confirmed the recorded 20/20 test results without rerunning them. Manual acceptance deferred by the user until the combined walkthrough; actual browser/visual acceptance was not run. Exact commands and diff boundary are in CURRENT_STATUS's active account handoff.

- **Remaining acceptance:** Awaiting manual acceptance — implementation, required focused checks and independent review are complete; no required remediation. User deferred the manual walkthrough until all ticket work is complete (2026-09-13). Package-level release gates remain separate.

<a id="c09"></a>
### C09 — Keep signup lifecycle notifications on the owned record

- **Current acceptance — 2026-09-14: Done.** User passed the mapped manual checks and accepted MR-26 internal evidence on the independently passed integrated candidate. Earlier outcome paragraphs retain historical per-batch evidence and limits; their pending-manual wording is superseded. This does not authorize packaging or publication.

- **Evidence:** U2-02; [unit 2 report](/private/tmp/bingo-sweep-unit2-20260909.md). Priority/type: P2.
- **Problem:** Admin withdrawal/restore producers omit participantId and Confirmation falls back home instead of opening the recipient record.
- **Scope / authority:** SignupService notification Route calls; existing Confirmation owner route. Contracts §§5.3,9.5.
- **Acceptance:** Both notification destinations include the owned participant context and resolve for the recipient after withdrawal/restoration. Wrong-owner access remains denied.
- **Focused checks:** Follow the actual emitted links using the intended account through HTTP; checking URL text alone is insufficient.
- **Dependencies / decisions:** None
- **Existing data:** Decide whether existing unread malformed links need a bounded compatibility/recovery action; do not rewrite notification history silently.
- **Outcome:** Implemented and independently reviewed clear 2026-09-09 under the user-authorized first trial, DELIVERY_PLAN17.12. The Luna Max implementer added participant context to both lifecycle notifications and proved the actual emitted withdrawal/restore links through owner-authenticated HTTP with wrong-owner denial. One focused PostgreSQL/Testcontainers case and scoped format/diff checks pass; fresh Astra Medium review found no code blocker or named remediation. Disposable malformed-link detection found the one synthetic candidate. The filtered unique event/account ownership rule makes matching retained rows unambiguous; zero-match and unrecognized routes remain untouched. Production count is unknown. User delegated disposition to the planner on 2026-09-14; old-data inspection/repair is explicitly deferred to avoid speculative cleanup. Awaiting manual acceptance, retaining the existing independent code PASS. No production scan, history rewrite or additional verification; actual affected-user count remains unknown. Revisit only a concrete reported failure, not an automatic cleanup task.

<a id="c10"></a>
### C10 — Make internal participants reachable in the ordinary draft

- **Current acceptance — 2026-09-14: Done.** User passed the mapped manual checks and accepted MR-26 internal evidence on the independently passed integrated candidate. Earlier outcome paragraphs retain historical per-batch evidence and limits; their pending-manual wording is superseded. This does not authorize packaging or publication.

- **Evidence:** U3-01; [unit 3 report](/private/tmp/bingo-sweep-unit3-20260909.md). Priority/type: P1.
- **Problem:** Internal Admin-created participants consume ordinary capacity but blanket AdminCreated-source filters exclude them from draft pool, pick and preassignment.
- **Scope / authority:** Admin/Events/Draft.cshtml.cs pool/distribution/pick/preassignment queries; existing actual preformed-membership distinction. Contracts §§5.5,6.1.
- **Acceptance:** Eligible confirmed internal participants enter ordinary draft paths; actual preformed members remain excluded from that pool and capacity calculation. Assignment/finalization cannot silently omit eligible internal participants.
- **Focused checks:** Admin internal add → visible draft pool → pick/preassign → finalized roster, alongside a preformed member proving the distinction.
- **Dependencies / decisions:** None
- **Existing data:** Do not reconstruct prior picks or historical rosters. Identify any currently omitted participant recovery requirement before altering finalized events.
- **Outcome:** Implemented and independently passed after same-reviewer F01 recheck 2026-09-14; Awaiting manual acceptance. Eligible internal participants enter ordinary pool/counts/distribution/preassignment/picks/finalization using actual active preformed membership exclusion. Setup withdrawal and preassignment return-to-pool remain usable. F01 restores original signup-source-based MemberView.External removal-control flag; no new C11 departure/handler/publication behavior. Actual pre-fix HTTP regression failed at unwanted website-member removal control, then passed with external-member positive control and affected C10 journey. No destructive removal executed or historical roster repair. See completed batch checkpoint/recheck.

<a id="c11"></a>
### C11 — Enable the contracted pre-Live post-draft departure journey

- **Current acceptance — 2026-09-14: Done by explicit user waiver.** User approved closing chat steps 13–15 without further manual execution, accepting existing passing test/review evidence. C17 also has observed MR-11 acceptance. Unrun manual portions are waived, not claimed executed. Earlier pending-manual wording is superseded; no further checks or packaging are authorized by this record.

- **Evidence:** U3-02; [unit 3 report](/private/tmp/bingo-sweep-unit3-20260909.md). Priority/type: P1.
- **Problem:** After draft lock and before Live, ordinary withdrawal is forbidden and exceptional departure/replacement accepts only Live, leaving no route to record a departure.
- **Scope / authority:** Existing SignupService withdrawal/vacancy operations and Participant controls. Contract §5.6; preserve draft/publication/eligibility semantics.
- **Acceptance:** User agrees to retain the missing journey if it can be handled well (2026-09-09). Start with the smallest finalized-pre-Live departure/vacancy path, preserving publication, reservations, Captain readiness and history. Resolve consequential handling for other drafted states explicitly rather than automatically redesigning Running/Paused/Finalized flows; do not claim all U3-02 states fixed until addressed or explicitly deferred. No reopening to erase picks or retroactive eligibility.
- **Focused checks:** One applicable major-slice readiness review after behavior is frozen; focused rendered departure→vacancy→replacement paths for the approved states, retained pick/publication checks, and Live regression.
- **Dependencies / decisions:** User approved finalized-pre-Live Admin departure/explicit replacement on 2026-09-14, with current publication updated and historical snapshots/picks preserved; Running/Paused departures explicitly deferred. Frozen behavior is in FUNCTIONAL_CONTRACTS 5.6/PRODUCT_REQUIREMENTS 18.2 and the active C11 contract. The required one-time readiness review must pass before production edits; use the same single reviewer for readiness and final review. No other departure policy may be invented. Planner approved the C17-consistent recipient-role destination distinction solely for C11's new pre-Live withdrawal branch; existing Live C17 code/source and old notifications remain untouched. See active contract.
- **Existing data:** Keep old roster/pick/eligibility history. Existing affected events need an explicit prospective recovery action, not automatic rewrites.
- **Outcome:** Implemented and independently passed final review and same-reviewer F01/F02 recheck on 2026-09-14; Awaiting manual acceptance. Approved finalized-pre-Live departure/vacancy/explicit replacement preserves snapshots/picks/reservations and republishes the current roster. Running/Paused departures remain explicitly deferred, so no all-U3-02 closure. Evidence supports 51 current distinct passing cases (42 C11 + 9 existing), including 16 selected correction/regression passes with the two invalid-hide-fixture failures corrected and rerun; not a fresh 51-case run. Final Web/Razor Release zero warnings/errors; scoped format/reviewer diff checks pass. New UI states await manual acceptance in UI_PAGE_MATRIX. No prior source integration, retained-data repair or packaging. See the completed checkpoint and final recheck report.

<a id="c12"></a>
### C12 — Reject cross-event roster moves

- **Current acceptance — 2026-09-14: Done.** User passed the mapped manual checks and accepted MR-26 internal evidence on the independently passed integrated candidate. Earlier outcome paragraphs retain historical per-batch evidence and limits; their pending-manual wording is superseded. This does not authorize packaging or publication.

- **Evidence:** U3-03; [unit 3 report](/private/tmp/bingo-sweep-unit3-20260909.md). Priority/type: P1.
- **Problem:** MoveMember scopes its target team but not the source membership/participant to the route event, allowing an Admin request to cross event boundaries.
- **Scope / authority:** Admin/Events/Draft.cshtml.cs OnPostMoveMemberAsync; reuse same-event guards. Contract §§2.2,5.6.
- **Acceptance:** Source membership, source team, participant and target all belong to the route event and permitted lifecycle. Mismatch fails before any mutation; an authorized same-event move still preserves history and publication.
- **Focused checks:** Isolated authenticated mismatched-event POST and valid same-event move; assert both events unchanged on failure.
- **Dependencies / decisions:** None
- **Existing data:** If mismatched retained memberships are possible, propose a read-only invariant check and safe correction process; do not move historical memberships automatically.
- **Outcome:** Approved 2026-09-13 for this direct Astra xhigh batch and one fresh Astra xhigh independent reviewer. Implemented and focused-check complete 2026-09-13 by Astra xhigh implementer `01a09b93-dc09-7a12-84bd-211b395aef11`. MoveMember now requires active source and target teams in the route event and verifies participant event ownership before any mutation; existing lifecycle and published-correction confirmation remain in force. Three authenticated source/participant/target mismatch cases prove both events and all roster/publication/history/audit/notification state unchanged; the authenticated valid published move retains replacement history and republishes successfully. Included in the passing 33-case batch. A read-only retained-membership invariant query and record-specific recovery proposal are recorded in the evidence; neither was executed against user data. Independent Astra xhigh review cleared with no actionable findings or scope deviations; existing 33/33 PostgreSQL results and Node/compile/format evidence were reused, with no checks rerun.

- **Remaining acceptance:** Awaiting manual acceptance — implementation, required focused checks and independent review are complete; no required remediation. User deferred the manual walkthrough until all ticket work is complete (2026-09-13). Package-level release gates remain separate.

<a id="c13"></a>
### C13 — Make CSV validation preserve the primary column and recover errors

- **Current acceptance — 2026-09-14: Done by explicit user waiver.** User said "Pass on CSV"; skipped manual import accepted using existing passing test/review evidence. No manual execution claimed. Earlier pending-manual wording superseded.

- **Evidence:** U3-04, U3-11; [unit 3 report](/private/tmp/bingo-sweep-unit3-20260909.md). Priority/type: P2.
- **Problem:** Dropping blank CSV account cells promotes a secondary column to primary; strict UTF-8 decoding failure escapes normal preview feedback.
- **Scope / authority:** PreformedRosterCsvImportService parser/ValidateShape and existing upload-preview handler. Contract §6.1 and PRODUCT_REQUIREMENTS roster CSV rules.
- **Acceptance:** Blank required first Account is an error even when later accounts exist. Optional empty columns remain optional; regular/informational roles cannot shift. Invalid encoding returns safe preview validation with no apply or partial records.
- **Focused checks:** Existing preview tests with blank-primary/nonblank-alt and invalid UTF-8; valid optional-column import still applies once under existing nonce checks.
- **Dependencies / decisions:** None
- **Existing data:** Do not infer intended primary identity for already imported rows; flag any demonstrated repair need separately.
- **Outcome:** Implemented directly by Astra xhigh task `01a09be5-0ef2-76c3-92c2-6c3890af9266` on 2026-09-13. Required primary column identity is retained; invalid strict UTF-8 returns the existing safe preview error with EN/DA recovery. All 14 CSV service tests and four authenticated EN/DA upload cases pass, including optional roles, no residue, collision/nonce boundaries and one-time apply. Independent review passed; no historical identity inference or data edits. Evidence: /Users/christopher/Documents/Codex/2026-09-13/participant-flow-ticket-batch/outputs/implementation-report.md.

- **Remaining acceptance:** Awaiting manual acceptance. Implementation, focused checks and [independent review/recheck](/Users/christopher/Documents/Codex/2026-09-13/participant-flow-ticket-batch/outputs/independent-recheck.md) are complete. Reviewer reused execution evidence without reruns. User-deferred manual walkthrough remains; package/release gates are separate. Any retained-data limitations above remain explicit and are not a claim of historical repair.

<a id="c14"></a>
### C14 — Number waiting participants independently of preformed members

- **Current acceptance — 2026-09-14: Done.** User passed the mapped manual checks and accepted MR-26 internal evidence on the independently passed integrated candidate. Earlier outcome paragraphs retain historical per-batch evidence and limits; their pending-manual wording is superseded. This does not authorize packaging or publication.

- **Evidence:** U3-05; [unit 3 report](/private/tmp/bingo-sweep-unit3-20260909.md). Priority/type: P2.
- **Problem:** Public waiting position uses index in a mixed participant list, so interleaved confirmed preformed members create gaps.
- **Scope / authority:** Events/Signups.cshtml.cs waiting-position calculation. Contract §5.4; existing ordinary-pool capacity rules.
- **Acceptance:** Waiting ranks are contiguous in authoritative queue order despite interleaved preformed additions. Do not change which members appear publicly or redefine confirmed counts/capacity in this ticket.
- **Focused checks:** Waiter A → preformed addition → waiter B; public table shows consecutive waiting positions and unchanged admission results.
- **Dependencies / decisions:** None
- **Existing data:** Projection-only correction; no queue sequence/history rewrite.
- **Outcome:** Implemented by the same Astra xhigh task on 2026-09-13. Waiting positions increment independently in existing queue order. Executed waiter/CSV-preformed/waiter HTML shows 01/02, unchanged public confirmed counts/capacity and ordinary admission; withdrawal promotes the first waiter and leaves contiguous rank 01 with original sequences retained. Independent review passed; projection-only, no persisted queue/history change. Evidence: /Users/christopher/Documents/Codex/2026-09-13/participant-flow-ticket-batch/outputs/implementation-report.md.

- **Remaining acceptance:** Awaiting manual acceptance. Implementation, focused checks and [independent review/recheck](/Users/christopher/Documents/Codex/2026-09-13/participant-flow-ticket-batch/outputs/independent-recheck.md) are complete. Reviewer reused execution evidence without reruns. User-deferred manual walkthrough remains; package/release gates are separate. Any retained-data limitations above remain explicit and are not a claim of historical repair.

<a id="c15"></a>
### C15 — Commit draft and team mutations with scoped audit

- **Current acceptance — 2026-09-14: Done.** User passed the mapped manual checks and accepted MR-26 internal evidence on the independently passed integrated candidate. Earlier outcome paragraphs retain historical per-batch evidence and limits; their pending-manual wording is superseded. This does not authorize packaging or publication.

- **Evidence:** U3-06; [unit 3 report](/private/tmp/bingo-sweep-unit3-20260909.md). Priority/type: P2.
- **Problem:** Start/pick/undo and ordinary team/controller operations can commit before audit, leaving changed state after a failed audit request.
- **Scope / authority:** Named Draft.cshtml.cs handlers in U3-06; existing transactions/Audit helper. Contract §§2.3,6.1.
- **Acceptance:** Each named mutation and its event-scoped before/after audit share an atomic boundary. Existing correctly atomic finalization/cancel/roster corrections retain behavior; do not replace the audit framework.
- **Focused checks:** Review each named transaction site, then use discriminating fault injection covering each distinct save/transaction pattern and successful event association; reuse draft ledger tests.
- **Dependencies / decisions:** None
- **Existing data:** C36 owns retained unassociated event audit handling; missing historical audit facts cannot be reconstructed by guesswork.
- **Outcome:** **Awaiting manual acceptance.** Implemented in `/Users/christopher/.codex/worktrees/ffd6/BingoWebpage`, branch `fix/audit-atomicity-ticket-batch`, base `c165bbc`. Astra xhigh reviewer `01a09c49-cfcc-7d42-8b9d-f6f2f07cc4aa` passed the batch after its bounded C22 R1 recheck; no required findings remain. Initial 44/44 focused cases remain applicable to unaffected behavior; the corrected removal case passed 1/1 after reproducing the defect. Release compilation and scoped formatting/whitespace evidence recorded. [Review report](/private/tmp/audit-batch-review/reviewer-report.md). Manual acceptance is deferred; no historical-data repair or release/packaging acceptance implied.

<a id="c16"></a>
### C16 — Recheck Captain presence at draft finalization

- **Current acceptance — 2026-09-14: Done.** User passed the mapped manual checks and accepted MR-26 internal evidence on the independently passed integrated candidate. Earlier outcome paragraphs retain historical per-batch evidence and limits; their pending-manual wording is superseded. This does not authorize packaging or publication.

- **Evidence:** U3-07; [unit 3 report](/private/tmp/bingo-sweep-unit3-20260909.md). Priority/type: P2.
- **Problem:** Captain readiness is checked at start but can change before finalization; finalized publication currently omits a fresh Captain check.
- **Scope / authority:** Draft.cshtml.cs finalize/readiness; existing current-Captain rule. Contract §6.1.
- **Acceptance:** Demoting the last required Captain after draft start blocks finalization with a usable correction destination. Reassignment permits finalization; usable-account readiness remains the separate event-start rule.
- **Focused checks:** Start → demote → blocked finalize → assign → successful finalize; no publication residue on blocker.
- **Dependencies / decisions:** None
- **Existing data:** No automatic role grants or edits to past publication snapshots.
- **Outcome:** Approved 2026-09-13 for this direct Astra xhigh batch and one fresh Astra xhigh independent reviewer. Implemented and focused-check complete 2026-09-13 by the same Astra xhigh implementer. Finalization rechecks current Captain presence on every active drafted team inside its serializable transaction before creating any publication state. The localized error names the teams and opens the first affected team through the existing rosterTeamId route. The unchanged roster-dialog markup is shared with that active-draft recovery route. Authenticated start/picks/demotion to Co-captain/blocked finalize/reassignment/success passed, including full no-residue comparison; unowned Captains still permit draft finalization, while existing event-start account/emergency readiness checks remain separate and pass. Publication atomicity, reopen and concurrency cases pass in the 33-case batch. Independent Astra xhigh review cleared with no actionable findings or scope deviations, reusing the recorded passing evidence; no manual visual acceptance or historical publication edits.

- **Remaining acceptance:** Awaiting manual acceptance — implementation, required focused checks and independent review are complete; no required remediation. User deferred the manual walkthrough until all ticket work is complete (2026-09-13). Package-level release gates remain separate.

<a id="c17"></a>
### C17 — Route team notifications by recipient and publication phase

- **Current acceptance — 2026-09-14: Done by explicit user waiver.** User approved closing chat steps 13–15 without further manual execution, accepting existing passing test/review evidence. C17 also has observed MR-11 acceptance. Unrun manual portions are waived, not claimed executed. Earlier pending-manual wording is superseded; no further checks or packaging are authorized by this record.

- **Evidence:** U3-08, U3-10; [unit 3 report](/private/tmp/bingo-sweep-unit3-20260909.md). Priority/type: P2.
- **Problem:** Live vacancy notifications send ordinary leadership to an Admin-only page; initial Captain-role notifications send owners to an unpublished public roster.
- **Scope / authority:** SignupService vacancy recipients and TeamCaptainAuthorityService role destinations. Contracts §§5.6,9.5.
- **Acceptance:** Admin recipients receive authorized Admin context; ordinary leadership receive permitted team/event context. Before publication, role notices use a reachable phase-appropriate existing destination; after publication, roster routes resolve. Preserve privacy and atomic notification creation.
- **Focused checks:** Follow actual links as Admin and ordinary leadership in prepublication and published/live states, including a recipient lacking Admin role.
- **Dependencies / decisions:** None
- **Existing data:** Report whether retained unread links need recovery; no silent history rewrite or new navigation framework.
- **Outcome:** Implemented by the same Astra xhigh task on 2026-09-13. Admin vacancy notices keep Admin follow-up; ordinary leadership receives Teams. Role notices use owner-safe/Admin context before publication and Teams with active publication. Actual rendered notification links and destinations passed for ordinary User/Admin across private, closed, published, reopened and Live phases, including role demotion; live Admin/Super Admin/Captain/Co-captain navigation, privacy, overlap deduplication, rollback and existing concurrency checks pass. Independent reviewer `01a09bf8-fde0-7803-9378-37f434a46a59` returned one P2 for board-published roster reopening. The bounded correction now prevents pre-Live owner confirmation redirects without an active roster. A real approved/published-board and authenticated Admin-reopen HTTP regression failed before the fix; both affected ordinary-owner cases pass afterward, including private notice destinations, outsider denial, unpublished-peer exclusion and preserved published/Live redirects. Release build, two-file format and diff checks pass. Same-reviewer bounded recheck passed with no required findings remaining; see /Users/christopher/Documents/Codex/2026-09-13/participant-flow-ticket-batch/outputs/c17-remediation-report.md. Retained unread Admin URLs for ordinary leadership may still need separately authorized recovery; retained role Teams URLs remain unavailable until publication. No retained-data scan, route backfill or history rewrite. Evidence: /Users/christopher/Documents/Codex/2026-09-13/participant-flow-ticket-batch/outputs/implementation-report.md.

- **Remaining acceptance:** Awaiting manual acceptance. Implementation, focused checks and [independent review/recheck](/Users/christopher/Documents/Codex/2026-09-13/participant-flow-ticket-batch/outputs/independent-recheck.md) are complete. Reviewer reused execution evidence without reruns. User-deferred manual walkthrough remains; package/release gates are separate. Any retained-data limitations above remain explicit and are not a claim of historical repair.

<a id="c18"></a>
### C18 — Activate the authoritative primary character for live replacements

- **Current acceptance — 2026-09-14: Done by explicit user waiver.** User approved closing chat steps 13–15 without further manual execution, accepting existing passing test/review evidence. C17 also has observed MR-11 acceptance. Unrun manual portions are waived, not claimed executed. Earlier pending-manual wording is superseded; no further checks or packaging are authorized by this record.

- **Evidence:** U3-09; [unit 3 report](/private/tmp/bingo-sweep-unit3-20260909.md). Priority/type: P2.
- **Problem:** Replacement selects first Playing assignment by registration order, which can be an unchanged secondary after the primary was edited.
- **Scope / authority:** SignupService.ReplaceVacancyAsync; reuse authoritative PrimaryCharacters selection used at event start. Contract §5.6.
- **Acceptance:** A replacement activates its saved current system primary, independent of assignment creation order. Eligibility starts prospectively; secondary/informational roles and past credit remain unchanged.
- **Focused checks:** Signup A/B → edit primary to C → live replacement; C activates and old evidence remains untouched.
- **Dependencies / decisions:** None
- **Existing data:** No retroactive activation or contribution repair without a separately approved record-specific plan.
- **Outcome:** Approved 2026-09-13 for this direct Astra xhigh batch and one fresh Astra xhigh independent reviewer. Implemented and focused-check complete 2026-09-13 by the same Astra xhigh implementer. ReplaceVacancyAsync now uses the existing PrimaryCharacters authority query used by event start rather than registration order. The normal authenticated signup-edit regression saves A/B plus an informational account, changes the system primary to C, and proves replacement activates C at its prospective whole-minute boundary while retaining the secondary/informational assignment rows, departed participant eligibility and prior approved submission/contribution/pick/activation history. The defect was reproduced before this query change. Waiting/internal replacement and one-winner checks pass in the 33-case batch; the existing disposable fixture now links website assignments to its primary system question. C05 remains excluded. No historical activation/credit repair. Independent Astra xhigh review cleared with no actionable findings or scope deviations, reusing the recorded passing evidence.

- **Remaining acceptance:** Awaiting manual acceptance — implementation, required focused checks and independent review are complete; no required remediation. User deferred the manual walkthrough until all ticket work is complete (2026-09-13). Package-level release gates remain separate.

<a id="c19"></a>
### C19 — Restrict manual EHB to manual objectives

- **Current acceptance — 2026-09-14: Done by explicit user waiver.** User approved closing chat steps 13–15 without further manual execution, accepting existing passing test/review evidence. C17 also has observed MR-11 acceptance. Unrun manual portions are waived, not claimed executed. Earlier pending-manual wording is superseded; no further checks or packaging are authorized by this record.

User decision, 2026-09-13: all requirements within a tile/template must have one
kind, either catalogue/drop or custom/manual. Mixed configurations are invalid and
require separate tiles. Enforce this in editor controls, authoritative create/update
validation and new approval of retained mixed drafts. Preserve same-kind multiple
objectives and the existing all-manual tile estimate. Never automatically split,
delete or convert retained records, or change approved snapshots/historical weights.

- **Evidence:** U4-01; [unit 4 report](/private/tmp/bingo-sweep-unit4-20260909.md). Priority/type: P1.
- **Problem:** Standard catalogue objectives can use a manual override that bypasses missing derived EHB during approval.
- **Scope / authority:** Admin/Events/Board markup/handler and existing EHB approval calculation. Contract §§6.2–6.3.
- **Acceptance:** Server rejects manual totals as a substitute for standard objective derivation; incomplete standard estimates block approval with guidance. Manual objectives retain their legitimate estimate path; public/frozen results are unchanged.
- **Focused checks:** Standard missing-rate objective plus posted override cannot approve; valid standard and legitimate manual objectives can.
- **Dependencies / decisions:** None
- **Existing data:** Existing approved snapshots may include overrides. Detect/classify safely and seek explicit correction policy; never silently recalculate historical competitive weights.
- **Outcome:** Implemented and independently reviewed; Awaiting manual acceptance, 2026-09-13. Standard create/update requests reject manual totals; template writes enforce the distinction; live estimates and new approvals derive standard EHB even for retained overrides. Mixed creates/updates and new approval of retained mixed drafts fail with separate-tile guidance. Same-kind multiple drop objectives sum derived estimates; same-kind manual objectives retain the existing explicit tile total. Published/frozen readers and historical import arithmetic are unchanged. Focused disposable HTTP checks cover forged standard/mixed create/update without mutation, missing derived EHB despite retained/posted totals, valid standard/manual multiple objectives and retained mixed rejection in EN/DA. Native isolated Chrome and Node checks cover control restriction, native validity, posted-field omission and recovery. Manual visual acceptance remains deferred; initial independent review and R1 disposition follow below; evidence is [batch evidence](/private/tmp/board-approval-batch-evidence/README.md).
- **Independent review / R1 remediation:** Reviewer `01a09c21-4907-71f3-8701-ec41e44350ac` completed the initial source review and found one P2 direct-caller regression: the historical importer passed Araxxor's reviewed 25 EHB into a standard template. The importer now supplies manual estimates only to manual templates and preserves resolved historical EHB in tile/approval snapshots. The exact disposable `HistoricalImportIntegrationTests.AppliesFictionalOperatorInputAndExactRerunIsNoOp` case passed with new assertions that Araxxor remains a drop objective, has no template override, and retains 25 EHB in both snapshots. The reviewed manifest/hash and historical arithmetic are unchanged. Scoped compilation/format/diff checks passed. [R1 evidence](/private/tmp/board-approval-batch-evidence/r1-remediation.md); same-reviewer fixes-only recheck pending, so status remains Review.
- **Retained-data classification/recovery proposal (not executed):** A separately authorized read-only inspection should classify each immutable approval's requirement kinds as all-manual, all-catalogue or mixed, retaining approval/calculation versions, source identities and stored competitive EHB. A present-day template override does not prove how an old snapshot was approved. An all-catalogue snapshot whose frozen inputs cannot reproduce its estimate is a candidate for investigation, not proof of a manual override; historical calculator versions, rounding and missing retained source facts must be distinguished. Mixed retained snapshots are classified separately, without changing their historical weights.
- **Recovery boundary:** For unapproved working data, Admins explicitly repair catalogue/requirements, remove standard overrides on save, and place manual/drop work in separate tiles. Never automatically split or delete rows. Approved/published/finalized records remain unchanged; any future inspection, source restoration or reasoned replacement/correction requires separate data scope and policy approval. No user-data scan, count, diagnosis of actual retained records or retrospective recalculation was performed. This proposal is a separately deferred data action, not an unfulfilled implementation check.

- **Final review / remaining acceptance:** Same independent Astra xhigh reviewer `01a09c21-4907-71f3-8701-ec41e44350ac` cleared R1 and the batch with no required findings. [Passed recheck](/private/tmp/board-approval-batch-evidence/r1-recheck.md). The historical importer now omits a manual override on standard templates while preserving the historical drop kind and 25 EHB tile/approval snapshot. Its focused PostgreSQL regression passed 1/1; original 21 new and five direct-consumer passing cases plus applicable controls/build/format evidence were reused without reviewer reruns. Status is Awaiting manual acceptance, not Done. Retained-data policy/actions and combined release gates remain separately unperformed; no historical recalculation or publication is claimed. This final verdict supersedes earlier pending-recheck wording in the recorded history.

<a id="c20"></a>
### C20 — Preserve objective identity through board corrections

- **Current acceptance — 2026-09-14: Done.** User passed the mapped manual checks and accepted MR-26 internal evidence on the independently passed integrated candidate. Earlier outcome paragraphs retain historical per-batch evidence and limits; their pending-manual wording is superseded. This does not authorize packaging or publication.

- **Evidence:** U4-02; [unit 4 report](/private/tmp/bingo-sweep-unit4-20260909.md). Priority/type: P1.
- **Problem:** Editing only a tile title regenerates requirement IDs, losing contribution matches; deleted working rows also break canonical detail and private-correction submission consumers.
- **Scope / authority:** Board.cshtml.cs requirement/drop replacement, approval snapshot construction and directly affected PublicBoardService/Submission consumers. Contracts §§6.2–6.3,7.2,7.5.
- **Acceptance:** Title-only and other non-objective edits preserve objective identity, approved progress and readable submissions. While correction is private, ordinary users retain the active published objective contract. User-approved evidence lock blocks substantive rule changes/removal after any submitted evidence; preserve all referenced history.
- **Focused checks:** Board with approved and pending evidence → private title edit → ordinary submit/detail/progress → replacement publication; unchanged identity/progress/history. Add only scenarios needed for the approved genuine-objective policy.
- **Dependencies / decisions:** User approved the explained evidence-lock policy on 2026-09-14; frozen in FUNCTIONAL_CONTRACTS 6.2 / PRODUCT_REQUIREMENTS 10.2 and the active C20 batch. Do not solve by rewriting contributions or importing isolated source.
- **Existing data:** Identify any already broken references using unambiguous evidence. Repair/migration and live-record application require separate approval; existing lost associations may not be reconstructible.
- **Outcome:** Awaiting manual acceptance — independent final PASS after same-reviewer R1–R4 recheck on 2026-09-14. Stable evidence identities, scoring protection, active-publication projections and confirmed discard are source-reviewed; recorded 11/11 remediation and 17/17 affected runs overlap, with applicable original checks reused. See final checkpoint/report. Complete recovery after managed-artwork removal awaits authorized C20/C21 integration; no historical repair/source integration/release claim.

<a id="c21"></a>
### C21 — Retain artwork referenced by approval snapshots

- **Current acceptance — 2026-09-14: Done.** User passed the mapped manual checks and accepted MR-26 internal evidence on the independently passed integrated candidate. Earlier outcome paragraphs retain historical per-batch evidence and limits; their pending-manual wording is superseded. This does not authorize packaging or publication.

- **Evidence:** U4-03; [unit 4 report](/private/tmp/bingo-sweep-unit4-20260909.md). Priority/type: P1.
- **Problem:** Removing a tile during private correction deletes managed assets still needed by the current public or retained approval.
- **Scope / authority:** Board.cshtml.cs tile/image removal, PublicBoardImageService and existing managed-asset retention. Contract §§4.7,6.2.
- **Acceptance:** Snapshot-referenced assets survive working-tile removal and remain readable through authorized current/retained projections. Unreferenced cleanup remains bounded to the owning object; no cross-event deletion.
- **Focused checks:** Published illustrated tile → private removal → original public image still loads; superseded referenced asset retained; ordinary access boundaries stay enforced.
- **Dependencies / decisions:** None
- **Existing data:** Already deleted objects cannot be recovered from DB pointers alone. Report restoration needs without fabricating assets or invoking backup operations.
- **Outcome:** Implemented and independently passed after batch R1 recheck 2026-09-14; Awaiting manual acceptance. Snapshot-referenced image rows/files survive working-tile removal; unreferenced deletion is event/tile-bounded. Current public access resolves active approval; retained access uses scoped Admin handler with Hidden/Discarded guards and narrowly qualified terminal exception. Necessary migration removes restrictive image-to-working-tile FK with matching designer/snapshot; local DATA_MODEL records identity and fail-closed Down limitation. PostgreSQL/storage/HTTP evidence reviewed; no missing-user-asset restoration, post-retention rollback or data repair claimed. C19/C22 overlaps remain for later integration. See final cohesive review and batch checkpoint.

<a id="c22"></a>
### C22 — Commit ordinary board mutations with event-scoped audit

- **Current acceptance — 2026-09-14: Done.** User passed the mapped manual checks and accepted MR-26 internal evidence on the independently passed integrated candidate. Earlier outcome paragraphs retain historical per-batch evidence and limits; their pending-manual wording is superseded. This does not authorize packaging or publication.

- **Evidence:** U4-04; [unit 4 report](/private/tmp/bingo-sweep-unit4-20260909.md). Priority/type: P2.
- **Problem:** Board/tile/control/resize/planning changes have save-before-audit paths outside the atomic mutation boundary.
- **Scope / authority:** Named Board.cshtml.cs handlers and Audit helper from U4-04. Contract §§2.3,6.2.
- **Acceptance:** Every named change and event-scoped audit commit/rollback together; approval/publication/correction paths already atomic retain their behavior. No generalized auditing layer.
- **Focused checks:** Inspect named sites and fault-inject distinct persistence patterns; retain move/swap/resize behavior and assert successful EventId association.
- **Dependencies / decisions:** None
- **Existing data:** C36 owns legacy association; never fill missing before/after history with invented values.
- **Outcome:** **Awaiting manual acceptance.** Implemented in `/Users/christopher/.codex/worktrees/ffd6/BingoWebpage`, branch `fix/audit-atomicity-ticket-batch`, base `c165bbc`. Astra xhigh reviewer `01a09c49-cfcc-7d42-8b9d-f6f2f07cc4aa` passed the batch after its bounded C22 R1 recheck; no required findings remain. Initial 44/44 focused cases remain applicable to unaffected behavior; the corrected removal case passed 1/1 after reproducing the defect. Release compilation and scoped formatting/whitespace evidence recorded. [Review report](/private/tmp/audit-batch-review/reviewer-report.md). Manual acceptance is deferred; no historical-data repair or release/packaging acceptance implied.

<a id="c23"></a>
### C23 — Reject approval of stale displayed catalogue inputs

- **Current acceptance — 2026-09-14: Done.** User passed the mapped manual checks and accepted MR-26 internal evidence on the independently passed integrated candidate. Earlier outcome paragraphs retain historical per-batch evidence and limits; their pending-manual wording is superseded. This does not authorize packaging or publication.

- **Evidence:** U4-05; [unit 4 report](/private/tmp/bingo-sweep-unit4-20260909.md). Priority/type: P2.
- **Problem:** Board version alone does not detect a completed catalogue change after the Admin viewed the board, so approval silently adopts unseen values.
- **Scope / authority:** Board displayed/posted approval inputs and existing catalogue version boundary. Contract §6.3.
- **Acceptance:** An intervening relevant catalogue edit requires reload/review before approval; unchanged inputs still approve coherently. Select the smallest version/fingerprint or invalidation approach and document it; no broad cache framework.
- **Focused checks:** A views board → B changes relevant rate/item → A approves; stale approval fails without snapshot, then refreshed approval succeeds.
- **Dependencies / decisions:** None
- **Existing data:** Existing snapshots remain frozen; do not retroactively invalidate or recalculate them.
- **Outcome:** Implemented and independently reviewed; Awaiting manual acceptance, 2026-09-13. The existing approval forms carry a SHA-256 fingerprint of sorted referenced boss/drop/item identities and versions. Rendered catalogue choices, estimates and fingerprint share a repeatable-read snapshot; final serializable approval checks the fingerprint and holds SHARE locks on those catalogue rows through commit. This covers corrected publication and ignores unrelated catalogue entries. Existing board version/confirmation/history boundaries remain. Disposable rendered-form tests passed for boss rate, drop rate and shared-item changes, stale rejection with no new snapshot, refreshed approval, missing token and unrelated edits. A final-save barrier proved relevant boss/drop/item writes cannot slip through approval's locks. See [batch evidence](/private/tmp/board-approval-batch-evidence/README.md). Initial independent review found no additional C23 defect; the C19 R1 recheck also passed and the batch now awaits manual acceptance. Manual acceptance is deferred.

- **Final review / remaining acceptance:** Same independent Astra xhigh reviewer `01a09c21-4907-71f3-8701-ec41e44350ac` cleared R1 and the batch with no required findings. [Passed recheck](/private/tmp/board-approval-batch-evidence/r1-recheck.md). The historical importer now omits a manual override on standard templates while preserving the historical drop kind and 25 EHB tile/approval snapshot. Its focused PostgreSQL regression passed 1/1; original 21 new and five direct-consumer passing cases plus applicable controls/build/format evidence were reused without reviewer reruns. Status is Awaiting manual acceptance, not Done. Retained-data policy/actions and combined release gates remain separately unperformed; no historical recalculation or publication is claimed. This final verdict supersedes earlier pending-recheck wording in the recorded history.

<a id="c24"></a>
### C24 — Protect shared catalogue item edits and record their changes

- **Current acceptance — 2026-09-14: Done.** User passed the mapped manual checks and accepted MR-26 internal evidence on the independently passed integrated candidate. Earlier outcome paragraphs retain historical per-batch evidence and limits; their pending-manual wording is superseded. This does not authorize packaging or publication.

- **Evidence:** U4-06; [unit 4 report](/private/tmp/bingo-sweep-unit4-20260909.md). Priority/type: P2.
- **Problem:** A source-drop form carries only drop version, allowing stale shared-item values to overwrite edits through another source; main audit omits item changes.
- **Scope / authority:** Admin/Catalogue/Index form and handler; existing shared item Version/audit transaction. Contract §9.2.
- **Acceptance:** Round-trip relevant shared-item version and reject a stale edit without changing drop/item. Include item before/after in the same audit transaction while preserving immutable item identity policy.
- **Focused checks:** Two drops sharing one item: intervening rename/image change then stale save; unchanged edit succeeds with item audit.
- **Dependencies / decisions:** None
- **Existing data:** Preserve catalogue identity and approved snapshots; unknown overwritten metadata is not backfilled.
- **Outcome:** Implemented and independently passed 2026-09-13; Awaiting manual acceptance. Shared item freshness round-trips with drop version, rejects stale displayed state and guards mutation-time races even on drop-only saves. Item/drop before/after audit is atomic and records persisted versions. Final C24 PostgreSQL/HTTP evidence is 3/3, including shared-item edits, audit rollback and a concurrent item edit during drop-only save. Existing identity/snapshot/sharing/deletion behavior preserved in source; no historical reconstruction or metadata backfill. See the completed batch checkpoint and independent review report.

<a id="c25"></a>
### C25 — Return actionable board approval validation

- **Current acceptance — 2026-09-14: Done by explicit user waiver.** User approved closing chat steps 13–15 without further manual execution, accepting existing passing test/review evidence. C17 also has observed MR-11 acceptance. Unrun manual portions are waived, not claimed executed. Earlier pending-manual wording is superseded; no further checks or packaging are authorized by this record.

- **Evidence:** U4-08; [unit 4 report](/private/tmp/bingo-sweep-unit4-20260909.md). Priority/type: P2.
- **Problem:** Specific approval diagnostics are collapsed to a generic error, hiding the repair needed for positions, sources, identity or estimates.
- **Scope / authority:** Board.cshtml.cs approval catch/status path and existing safe validation messages/resources. Contract §6.2.
- **Acceptance:** Known validation failures identify the relevant repair without leaking internal exceptions. Unexpected failures remain generic; no approval residue.
- **Focused checks:** Representative distinct missing-position/source/EHB validation responses plus an unexpected exception boundary; reuse rollback checks.
- **Dependencies / decisions:** None
- **Existing data:** No retained-data rewrite expected; preserve existing records.
- **Outcome:** Implemented and independently reviewed; Awaiting manual acceptance, 2026-09-13. Only local known approval validation carries localized safe messages; positions, missing source, changed item identity, automatic/manual estimates and mixed-kind recovery have distinct EN/DA feedback. Unexpected persistence failures remain generic errors and roll back snapshot/audit writes. A failure sending the post-commit notification truthfully reports that approval was saved and suggests reload; it never claims rollback after commit. Disposable HTTP fault injection passed both generic failure/rollback cases and the saved-notification-failure case. Existing frozen approval/unapproval, publication/correction and concurrency/history checks passed after direct fixtures were updated to supply the displayed token and Razor URL context. See [batch evidence](/private/tmp/board-approval-batch-evidence/README.md). Initial independent review found no additional C25 defect; the C19 R1 recheck also passed and the batch now awaits manual acceptance. Manual walkthrough is deferred.

- **Final review / remaining acceptance:** Same independent Astra xhigh reviewer `01a09c21-4907-71f3-8701-ec41e44350ac` cleared R1 and the batch with no required findings. [Passed recheck](/private/tmp/board-approval-batch-evidence/r1-recheck.md). The historical importer now omits a manual override on standard templates while preserving the historical drop kind and 25 EHB tile/approval snapshot. Its focused PostgreSQL regression passed 1/1; original 21 new and five direct-consumer passing cases plus applicable controls/build/format evidence were reused without reviewer reruns. Status is Awaiting manual acceptance, not Done. Retained-data policy/actions and combined release gates remain separately unperformed; no historical recalculation or publication is claimed. This final verdict supersedes earlier pending-recheck wording in the recorded history.

<a id="c26"></a>
### C26 — Show bounded dependency references when catalogue deletion is blocked

- **Evidence:** U4-09; [unit 4 report](/private/tmp/bingo-sweep-unit4-20260909.md). Priority/type: P2.
- **Problem:** Referenced catalogue deletion offers deactivation but does not identify references as contracted.
- **Scope / authority:** Existing catalogue PrepareDeletion/deletion response and paginated/contextual UI. Contract §9.2.
- **Acceptance:** Blocked SuperAdmin deletion shows bounded relevant reference labels/destinations and deactivation guidance; referenced history remains intact and no extra data is disclosed. No generic dependency-browser framework.
- **Focused checks:** Referenced source/boss deletion response with permitted references; unused deletion and ordinary Admin denial remain protected.
- **Dependencies / decisions:** None
- **Existing data:** No retained-data rewrite expected; preserve existing records.
- **Outcome:** Closed — no change, clarified by the user 2026-09-14: dependency-reference lists are not needed and are not future batch work. Existing dependency blocking and deactivation guidance remain required; do not build the optional reference list.

<a id="c27"></a>
### C27 — Make unknown-outcome upload retry safe

- **Evidence:** U5-01; [unit 5 report](/private/tmp/bingo-sweep-unit5-20260909.md). Priority/type: P2.
- **Problem:** A committed upload with lost response returns the UI to Retry; resubmission creates another pending record and asset.
- **Scope / authority:** team-board-drawer.js, Captain/Submit binding and SubmissionService.CreateAsync. Contract §§2.3,7.2.
- **Acceptance:** The same uncertain attempt resolves to its existing outcome or safely reconciles before retry. Intentional separate pending copies remain allowed. Freeze the minimal request-identity/reconciliation design, ownership, lifetime and persistence need before implementing; preserve drawer result acknowledgment.
- **Focused checks:** Deterministic commit-then-response-loss/retry scenario and a deliberate separate submission; assert no unintended second record/asset and no cross-owner replay.
- **Dependencies / decisions:** Needs bounded persistence/interaction design accepted before implementation; any new durable key or schema change must be justified.
- **Existing data:** Do not deduplicate existing evidence by image/content automatically; intentional copies and unknown retries cannot safely be conflated.
- **Outcome:** User declines this correction (2026-09-09): if receipt is uncertain, users can check `/Submissions`. No new retry reconciliation/deduplication machinery or feedback work is required. Functional contract7.2 records the accepted boundary.

<a id="c28"></a>
### C28 — Recheck submission creation authority inside its mutation boundary

- **Current acceptance — 2026-09-14: Done.** User passed the mapped manual checks and accepted MR-26 internal evidence on the independently passed integrated candidate. Earlier outcome paragraphs retain historical per-batch evidence and limits; their pending-manual wording is superseded. This does not authorize packaging or publication.

- **Evidence:** U5-02; [unit 5 report](/private/tmp/bingo-sweep-unit5-20260909.md). Priority/type: P2.
- **Problem:** Create authorizes Captain scope before its transaction and does not reread the actor role before committing teammate evidence.
- **Scope / authority:** SubmissionService.CreateAsync and existing EvidenceAuthority/role lock boundaries. Contract §§2.1–2.3,7.2.
- **Acceptance:** Authority changes serialized before creation invalidate stale teammate permission; ordinary self-submission remains allowed only under current scope. Define lock/order using existing role mutation boundaries, avoiding deadlocks or stale authorization reuse.
- **Focused checks:** Controlled demotion between initial request and mutation; assert no unauthorized teammate submission/asset and valid current Captain/self behavior.
- **Dependencies / decisions:** None
- **Existing data:** No retrospective ownership/credit changes; investigate any demonstrated prior misuse separately.
- **Outcome:** Implemented and independently cleared 2026-09-13 in C28/C29/C30; Awaiting manual acceptance. Creation refreshes authority/time inside Serializable after event-before-membership locks and before storage; direct Captain role mutation shares the event-first order with actual live withdrawal. R1 actual withdrawal/self/teammate and affected authority/atomicity/concurrency checks passed (14/14); no unauthorized submission, asset, create audit or storage call remains in the raced cases. Same-reviewer bounded recheck cleared R1; intermediate SQL-text observer failures are disclosed, not claimed as old runtime deadlock reproduction. No retrospective repair performed. See the active batch checkpoint and independent recheck report.

<a id="c29"></a>
### C29 — Enforce the Live competition replacement-only rule

- **Current acceptance — 2026-09-14: Done.** User passed the mapped manual checks and accepted MR-26 internal evidence on the independently passed integrated candidate. Earlier outcome paragraphs retain historical per-batch evidence and limits; their pending-manual wording is superseded. This does not authorize packaging or publication.

- **Evidence:** U5-03; [unit 5 report](/private/tmp/bingo-sweep-unit5-20260909.md). Priority/type: P2.
- **Problem:** Manage and service permit reasoned Live clear although current contract permits validated replacement only.
- **Scope / authority:** Manage competition controls and EventCompetitionSynchronizationService.ConfigureAsync. Contract §9.6; DELIVERY_PLAN section5 accepted integration policy.
- **Acceptance:** Live clear is rejected server-side and absent as an action; validated matching replacement remains supported. Pre-Live configuration and retained readable cache follow existing rules.
- **Focused checks:** Live null clear rejected with state/cache intact; valid replacement and failed replacement preserve current safeguards.
- **Dependencies / decisions:** None
- **Existing data:** Do not restore unknown cleared cache/configuration from guesses; record any proven affected-event recovery need.
- **Outcome:** Implemented and independently cleared 2026-09-13 in C28/C29/C30; Awaiting manual acceptance. Live clear is rejected in service and removed from Manage; validated replacement, pre-Live setup and cache safeguards remain. Original service/HTTP evidence and review conclusions were reused because production source was unchanged in remediation. Exact R2 initial-link-plus-clear generation assertion reproduced expected 3/actual 2, was corrected to 2 and passed; separate preseeded fixture retains 3. No retained-data recovery performed. See the active batch checkpoint and independent recheck report.

<a id="c30"></a>
### C30 — Add the contracted submission status filter

- **Current acceptance — 2026-09-14: Done.** User passed the mapped manual checks and accepted MR-26 internal evidence on the independently passed integrated candidate. Earlier outcome paragraphs retain historical per-batch evidence and limits; their pending-manual wording is superseded. This does not authorize packaging or publication.

- **Evidence:** U5-04; [unit 5 report](/private/tmp/bingo-sweep-unit5-20260909.md). Priority/type: P2.
- **Problem:** Canonical submission ledger supports player/search but lacks status filtering; sorting the visible page does not provide that query.
- **Scope / authority:** Submissions/Index.cshtml(.cs), existing enhanced route/query transport. Contract §7.2.
- **Acceptance:** Status filters the full authorized query before pagination, combines with player/search and survives links/reload. User placement direction (2026-09-09): consider the right end of the submissions divider, like the right-aligned Open admin tools action in the supplied Notifications screenshot, preserving reading flow and usable narrow layout. Preserve current ledger composition, history and role scope; this is a focused addition, not a page redesign.
- **Focused checks:** Isolated ledger HTTP query with multiple statuses/pages and role boundaries; focused client check for the changed filter interaction.
- **Dependencies / decisions:** None
- **Existing data:** No retained-data rewrite expected; preserve existing records.
- **Outcome:** Implemented and independently cleared 2026-09-13 in C28/C29/C30; Awaiting manual acceptance. Authorized ledger status filtering applies before pagination, composes with player/search and survives enhanced navigation/reload. Existing composition and divider placement direction are preserved; original HTTP/Node evidence and source review were reused because C30 source was unchanged in remediation. User-deferred page/manual acceptance remains outstanding; no record rewrite. See the active batch checkpoint and independent recheck report.

<a id="c31"></a>
### C31 — Keep finalization pending blocker links event-scoped

- **Current acceptance — 2026-09-14: Done.** User passed the mapped manual checks and accepted MR-26 internal evidence on the independently passed integrated candidate. Earlier outcome paragraphs retain historical per-batch evidence and limits; their pending-manual wording is superseded. This does not authorize packaging or publication.

- **Evidence:** U6-01; [unit 6 report](/private/tmp/bingo-sweep-unit6-20260909.md). Priority/type: P2.
- **Problem:** Finalize links to Review with Pending status but omits eventId, so the destination has no selected queue.
- **Scope / authority:** EventFinalizationService readiness link; existing Admin Review route. Contracts §§7.5–7.6.
- **Acceptance:** Following Pending submissions resolves to the intended event and status for authorized Admin; no ambient-event fallback is introduced.
- **Focused checks:** Follow the actual rendered/emitted blocker link from Finalize to the isolated Review queue; assert event isolation.
- **Dependencies / decisions:** None
- **Existing data:** No retained-data rewrite expected; preserve existing records.
- **Outcome:** Implemented and independently passed 2026-09-13; Awaiting manual acceptance. Pending blocker carries eventId and Pending status. Actual rendered Finalize link was followed through isolated authorized HTTP Review with event-scoped queue, unscoped and anonymous controls. No composition/client changes. See the active batch completed checkpoint and independent review report.

<a id="c32"></a>
### C32 — Keep submissions closed after unfinalization

- **Current acceptance — 2026-09-14: Done.** User passed the mapped manual checks and accepted MR-26 internal evidence on the independently passed integrated candidate. Earlier outcome paragraphs retain historical per-batch evidence and limits; their pending-manual wording is superseded. This does not authorize packaging or publication.

- **Evidence:** U6-02; [unit 6 report](/private/tmp/bingo-sweep-unit6-20260909.md). Priority/type: P1.
- **Problem:** Early finalization with override followed by unfinalize before the ordinary cutoff permits uploads because eligibility ignores the recorded closure.
- **Scope / authority:** BingoEvent.Unfinalize/AcceptsNewSubmissions and directly affected upload/edit/resubmit gates. Contract §7.6.
- **Acceptance:** Unfinalize starts a new review cycle with submissions closed regardless of the old future cutoff. Only explicit valid ReopenSubmissions restores upload eligibility; preserve normal inclusive cutoff and emergency-window distinctions.
- **Focused checks:** Early end → override/finalize → unfinalize before cutoff → denied create/edit/resubmit → explicit reopen succeeds.
- **Dependencies / decisions:** None
- **Existing data:** Determine whether existing reopened review cycles are distinguishable from authorized reopen history; do not reject or remove past evidence automatically.
- **Outcome:** Implemented and independently passed 2026-09-13; Awaiting manual acceptance. Existing FinalizedAt and explicit reopen cutoff keep unfinalized review uploads closed until valid reopening, with ordinary inclusive/emergency exclusive boundaries preserved. Two PostgreSQL actor cases prove early-finalization/future-cutoff denial at create/edit/resubmit, retained history and successful explicit reopen; domain cases cover other lifecycle/cutoff controls. Lifecycle-written markers distinguish current states without migration. No real-data scan or evidence reclassification; no concrete required recovery established. See the active batch completed checkpoint and independent review report.

<a id="c33"></a>
### C33 — Invalidate stale finalization inputs and completion inspections

- **Current acceptance — 2026-09-14: Done.** User passed the mapped manual checks and accepted MR-26 internal evidence on the independently passed integrated candidate. Earlier outcome paragraphs retain historical per-batch evidence and limits; their pending-manual wording is superseded. This does not authorize packaging or publication.

- **Evidence:** U6-03; [unit 6 report](/private/tmp/bingo-sweep-unit6-20260909.md). Priority/type: P1.
- **Problem:** Evidence review changes rankings/completion without advancing the finalization freshness boundary; team-only acknowledgments remain valid after the inspected facts change.
- **Scope / authority:** Submission review transaction, EventFinalizationService readiness/inspection/version/cycle. Contract §7.6 and DATA_MODEL finalization inputs.
- **Acceptance:** An intervening relevant review mutation makes an older finalization attempt/affected inspection stale. Fresh inspection and finalization succeed; retained prior acknowledgments/results remain historical. Freeze the smallest competitive-input version/acknowledgment identity design before coding.
- **Focused checks:** Inspect/display → approve or reverse affecting completion/rank → stale finalization rejected → reinspect/refinalize; include complete→incomplete→complete in the same cycle.
- **Dependencies / decisions:** Needs the version/inspection contract frozen within ticket before implementation; coordinate C20 if changed objective inputs share this boundary.
- **Existing data:** Do not silently republish prior official results. Identify any evidenced stale inspection outcomes and seek explicit recovery direction.
- **Outcome:** Awaiting manual acceptance — final independent PASS after sole-reviewer R1 recheck on 2026-09-14. Implemented atomic finalization freshness, immutable per-cycle/team inspection identity and preserved resolution history; R1 adds actionable explicit losing-review concurrency feedback. Evidence supports 36 distinct cases (29 original reused + seven R1), not a fresh combined run; Release/format/diff gates pass. Exact checkout, hashes, failed-attempt disclosure and integration limits are in the accepted checkpoint/report. No source integration, historical repair or manual acceptance claimed.

<a id="c34"></a>
### C34 — Enforce the required reversal confirmation boundary

- **Evidence:** U6-04; [unit 6 report](/private/tmp/bingo-sweep-unit6-20260909.md). Priority/type: P2.
- **Problem:** Approved reversal uses JavaScript confirm plus reason, but no server-confirmed state reaches the handler/service.
- **Scope / authority:** Admin/Review/Details reversal form/handler and SubmissionService.ReverseAsync. Contract §7.5; current UI confirmation conventions.
- **Acceptance:** Use the existing confirmation convention to enforce explicit confirmed reversal at the authoritative request boundary, with current version/reason/role. Missing confirmation changes nothing. Preserve approved Review composition and ordinary feedback; do not add redundant confirmation stages.
- **Focused checks:** Authorized reversal missing confirmation is refused; confirmed current reversal succeeds; stale or invalid reason still fails. User checks only changed confirmation interaction if needed.
- **Dependencies / decisions:** None
- **Existing data:** No retained-data rewrite expected; preserve existing records.
- **Outcome:** User agrees to close (2026-09-09). Existing confirmation, reason, version and authorization remain; no extra server confirmation mechanism is required. This supersedes the original proposed acceptance.

<a id="c35"></a>
### C35 — Audit contribution changes caused by reversal rebalance

- **Current acceptance — 2026-09-14: Done.** User passed the mapped manual checks and accepted MR-26 internal evidence on the independently passed integrated candidate. Earlier outcome paragraphs retain historical per-batch evidence and limits; their pending-manual wording is superseded. This does not authorize packaging or publication.

- **Evidence:** U6-05; [unit 6 report](/private/tmp/bingo-sweep-unit6-20260909.md). Priority/type: P2.
- **Problem:** Reversal changes later submissions and contribution amounts, but their before/after state appears only in local history rather than the main immutable audit.
- **Scope / authority:** SubmissionService.RebalanceLaterContributions and existing reversal audit transaction. Contract §7.5.
- **Acceptance:** Main audit captures affected child IDs and before/after amounts, either bounded aggregate or existing entries, atomically with reversal. Preserve caps, original evidence and local history.
- **Focused checks:** Existing rebalance case asserts child main-audit data and rollback on audit failure; avoid duplicating progress arithmetic tests.
- **Dependencies / decisions:** None
- **Existing data:** Do not invent historical child amounts where prior audit lacks them; identify retained evidence limits explicitly.
- **Outcome:** Implemented and independently passed 2026-09-13; Awaiting manual acceptance. Main audit records each changed child contribution with actor/event, child/reversal/contribution identity and bounded sanitized before/after amounts inside the existing reversal transaction. PostgreSQL success and child-audit-failure cases prove committed audit and complete rollback; applicable cap controls reused. Old parent-only audit cannot reconstruct missing child history; no fabricated backfill/data scan/repair and no concrete recovery obligation established. See the active batch completed checkpoint and independent review report.

<a id="c36"></a>
### C36 — Protect hidden-event audit visibility, including retained rows

- **Evidence:** U7-01; [unit 7 report](/private/tmp/bingo-sweep-unit7-20260909.md). Priority/type: P1.
- **Problem:** Board/Draft audit writers omit EventId; ordinary Audit includes null-event rows, leaking names/details after event quarantine.
- **Scope / authority:** Event-scoped AuditWriter call sites, Audit projection and existing retained-data migration/preflight mechanisms. Contract §8.1a/§9.3.
- **Acceptance:** New event-scoped rows carry EventId; hiding removes their details from ordinary Admin audit while permitted global audit remains visible. Establish a deterministic strategy for existing null-associated rows before closure. Ambiguous rows must not be guessed into an event or simply expose hidden data.
- **Focused checks:** Create audit through real Board/Draft writers → hide → ordinary/Admin versus SuperAdmin permitted audit projections; isolated legacy-row cases for the approved strategy.
- **Dependencies / decisions:** C04, C15, C22 establish event association at affected writer families. Freeze retained-row identification/recovery policy before any migration.
- **Existing data:** A migration/preflight must state how operators identify and correct ambiguous records before retry. Production inventory/repair is separately authorized; preserve immutable audit facts.
- **Outcome:** Closed — no change, user clarification 2026-09-14. Hiding declutters the front page and ordinary Admin Events table; it is not intended to conceal event details from Admin audit history. No C36 legacy cleanup or visibility change is required. Existing writer/atomicity fixes remain valid and retained; no audit/history rewrite or source integration authorized.

<a id="c37"></a>
### C37 — Show generic cancellation on all public event destinations

- **Current acceptance — 2026-09-14: Done.** User passed the mapped manual checks and accepted MR-26 internal evidence on the independently passed integrated candidate. Earlier outcome paragraphs retain historical per-batch evidence and limits; their pending-manual wording is superseded. This does not authorize packaging or publication.

- **Evidence:** U7-02; [unit 7 report](/private/tmp/bingo-sweep-unit7-20260909.md). Priority/type: P2.
- **Problem:** Published cancelled events still render full Board/Teams/team/tile projections while the contract requires generic cancellation state.
- **Scope / authority:** PublicBoardService and Board-family/Teams destination handling. Contract §8.1.
- **Acceptance:** Old public URLs of a cancelled published event show the consistent safe cancellation state, without private reasons or competitive workspace controls. Existing archived/finalized public history remains available; records are retained.
- **Focused checks:** Publish then cancel → follow old Board/Teams/team/tile links; verify generic outcome and archived-event regression.
- **Dependencies / decisions:** None
- **Existing data:** Read/projection change only; do not erase retained publication, evidence or roster history.
- **Outcome:** Implemented and independently passed after same-reviewer R1 correction 2026-09-14; Awaiting manual acceptance. Cancelled public destinations suppress competitive projection/page/sidebar and managed images while preserving history. R1 propagates PublicEventCancelled through Board/TeamBoard/Teams and gates existing shared layout, removing context tabs and secondary spacing. Actual pre-fix assertion failed; three affected HTTP cancellation/finalized/archived cases pass, included in seven distinct original batch cases. Normal/terminal history positive controls preserved. No manual acceptance, CSS/JS redesign or data deletion. See final cohesive review and batch checkpoint.

<a id="c38"></a>
### C38 — Remove the emergency-only event-start dependency loop

- **Current acceptance — 2026-09-14: Done by explicit user waiver.** User approved closing chat steps 13–15 without further manual execution, accepting existing passing test/review evidence. C17 also has observed MR-11 acceptance. Unrun manual portions are waived, not claimed executed. Earlier pending-manual wording is superseded; no further checks or packaging are authorized by this record.

- **Evidence:** U8-01; [unit 8 report](/private/tmp/bingo-sweep-unit8-20260909.md). Priority/type: P1.
- **Problem:** Start requires enabled emergency fallback but enabling requires an already-started upload window, so an emergency-only team cannot reach Live.
- **Scope / authority:** EmergencyCredentialService, AccountAdministrationService.SetEmergencyEnabledAsync and EventLifecycleService readiness. Contracts §§7.1,9.4.
- **Acceptance:** Freeze the allowed setup-complete pre-start enablement phase and ActiveFrom behavior, including scheduled/early manual start. Then support setup→enable→start while actual evidence mutation remains independently time/role gated. Cutoff disablement and explicit re-enable rules remain intact.
- **Focused checks:** Real Admin creation/setup/enable→event start with no usable linked Captain; unauthorized/unset-password/early-use denial; ordinary cutoff/reopen behavior.
- **Dependencies / decisions:** User approved 2026-09-14: Admin may explicitly enable a fully set-up credential after draft finalization; submissions remain blocked until actual start, including authorized early start. ActiveFrom for this pre-start grant is the explicit enablement instant, not a future scheduled-start floor; no retroactive eligibility. Normal team/event/visibility/end-window restrictions and Live/cutoff/reopen/re-enable rules remain. FUNCTIONAL_CONTRACTS 9.4 and DATA_MODEL 8.2 now own the policy. Readiness must count usable scoped initialized credentials, not flags alone.
- **Existing data:** Retain credentials and access history; no bulk enabling or resetting existing emergency accounts.
- **Outcome:** Implemented to explicit approved finalized-draft pre-start enablement/actual-start-gated mutation policy; independently passed 2026-09-14, Awaiting manual acceptance. Every C38 source/test/owning policy remained unchanged through F01 recheck; applicable original checks and review reused. Combined batch has 23 distinct passing cases, migrated disposable PostgreSQL/real auth HTTP; storage recording double is not binary processing or new cross-transaction event/access race proof. No bulk grants/history rewrite, user DB action or app restart. See completed batch checkpoint/recheck.

<a id="c39"></a>
### C39 — Reject stale account administration confirmations

- **Current acceptance — 2026-09-14: Done.** User passed the mapped manual checks and accepted MR-26 internal evidence on the independently passed integrated candidate. Earlier outcome paragraphs retain historical per-batch evidence and limits; their pending-manual wording is superseded. This does not authorize packaging or publication.

- **Evidence:** U8-02; [unit 8 report](/private/tmp/bingo-sweep-unit8-20260909.md). Priority/type: P2.
- **Problem:** Disable/restore and role confirmations submit no expected target state/version, allowing an old tab to act after intervening completed state changes.
- **Scope / authority:** Admin/Accounts/Manage form/handler and AccountAdministrationService; existing account authorization/state versions. Contracts §§9.1,9.4.
- **Acceptance:** Round-trip target freshness through existing two-stage confirmation and reject stale disable/restore/grant/revoke without effects. Preserve self/owner/role rules and existing confirmation count.
- **Focused checks:** Open confirmation → another session disables/restores or changes role → stale POST refused; current confirmed action succeeds with session invalidation.
- **Dependencies / decisions:** None
- **Existing data:** No reversal of prior legitimate account actions or fabricated past state.
- **Outcome:** Implemented and independently passed 2026-09-13; Awaiting manual acceptance. Original AuthorizationVersion survives all four existing confirmations; stale completed state/role cycles fail without effects and reload current controls with fresh confirmation required. Existing authorization, session invalidation, audit/notification atomicity and composition preserved. Four strengthened controls, eight cycle HTTP cases, three atomicity/session controls and four Chrome confirmation cases passed; browser transport was controlled, not manual visual acceptance. No reversal of past legitimate actions or fabricated history. See the completed batch checkpoint and independent review report.

<a id="c40"></a>
### C40 — Add target-identifier filtering to audit

- **Evidence:** U8-04; [unit 8 report](/private/tmp/bingo-sweep-unit8-20260909.md). Priority/type: P2.
- **Problem:** Audit displays target IDs but provides no target-identifier query filter.
- **Scope / authority:** Admin/Audit/Index.cshtml(.cs), existing server-paginated query. Contract §9.3.
- **Acceptance:** A bounded target ID filter combines with event/actor/action/type/date, resets page and persists in pagination. Invalid input is safe; hidden-event restrictions and immutable read-only behavior remain intact.
- **Focused checks:** Focused authenticated filtered pagination including event visibility and invalid filter input; no export or general search framework.
- **Dependencies / decisions:** None
- **Existing data:** No retained-data rewrite expected; preserve existing records.
- **Outcome:** Deferred outside this batch, reaffirmed 2026-09-14. Do not implement target-ID filtering or an Audit page overhaul in the current batch sequence. Any later work requires a separately agreed scope.

<a id="c41"></a>
### C41 — Finish early loading of used public fonts

- **Current acceptance — 2026-09-14: Done.** User passed the mapped manual checks and accepted MR-26 internal evidence on the independently passed integrated candidate. Earlier outcome paragraphs retain historical per-batch evidence and limits; their pending-manual wording is superseded. This does not authorize packaging or publication.

- **Evidence:** FONT-01, user report 2026-09-09 of a small remaining public font swap after the Barlow improvement; [bounded source review](/private/tmp/bingo-public-font-ticket-review-20260909.md). Priority: P3 (presentation/loading). This is outside the original 45 sweep findings.
- **Verified source:** `_Layout.cshtml:65–66` preloads Barlow Condensed 600/800 WOFF2. `site.public-ui.css:21–24` defines Geist variable WOFF2 (69,652 bytes) without a preload and Barlow 500 as TTF-only (103,360 bytes), also unpreloaded. Geist supplies public body/control copy; signup capacity numerals use Barlow 500 (`:2856–2858`). All use `font-display: swap`. Shared CSS links are direct, with no inspected import chain. `MapStaticAssets()` serves assets; no font-cache defect is established.
- **Problem / uncertainty:** The used Geist and signup 500 faces are discovered later than the already-preloaded 600/800 faces. This plausibly explains remaining visible font replacement, but exact request timing and the user's observed flash are not yet reproduced. Preload cannot guarantee zero fallback frames on every connection.
- **Scope / authority:** Public `_Layout.cshtml`, the existing 500 face's source declaration in `site.public-ui.css`, and a matching Barlow Medium WOFF2 generated from the already-bundled licensed face using available tooling. Reuse the existing native preload pattern: one matching Geist preload on public layouts, and a 500 WOFF2 preload only for the existing Signup page condition. Retain 600/800 preloads, original TTF fallback/license, all font families/weights/sizes and readable fallback behavior. UI_SYSTEM typography remains authoritative. Source aliases share URLs and need no duplicate preload; unused Bebas declarations do not justify loading that font.
- **Acceptance:** Used common font requests begin from the document head with matching URL/type/CORS and no duplicate request/preload mismatch; signup additionally gets its 500 WOFF2. Final typography and layout stay unchanged. User confirms reduced noticeable swapping on representative cold navigation. Warm loads reuse caching as supported by measured responses, and blocked/failed font requests leave text and controls readable and usable. Do not claim universally eliminating swap.
- **Focused checks:** On actual rendered landing and signup pages from the exact candidate app, compare cold/ordinary, one slowed cold, warm navigation/reload, and one blocked-font case before/after. Inspect request start, duplicates/preload warnings and actual cache responses, plus visible wrapping/shift at desktop and narrow size. Use an authorized running app or an isolated instance with controlled data; no synthetic layout-only fixture or restart of HTTPS7131 by assumption. Run a focused Razor compile check if the conditional layout edit requires it; no full .NET suite for asset loading.
- **Dependencies / decisions:** None from D07/T05; font delivery is unrelated to header JavaScript initialization. Implementation is not authorized by creation of this ticket. If measured timing identifies another cause, report it before expanding scope or changing fallback metrics/cache policy.
- **Non-goals:** Admin font changes, typography redesign, font/alias cleanup, new dependencies/frameworks, JavaScript font loaders, waiting to reveal the page/text, invisible text as a solution, blanket preloads of unused weights, or cache-policy changes without evidence.
- **Existing data:** Static assets only; no database/history effects. Preserve asset licensing and deployment cache-busting conventions.
- **Outcome:** Implemented and independently passed 2026-09-14; Awaiting manual acceptance. Public Geist and Signup-only Medium preload plus licensed lossless WOFF2 preserve final typography/fallback. Actual-app 20-pair/40-capture matrix and independent source/font/evidence checks passed. Slowed font discovery improves and measured Signup CLS becomes zero, with 340–552ms later slowed FCP; ordinary timings vary. User visual confirmation remains deferred; no universal no-swap/faster paint/production-cache or Signup submission claim. Five-file source boundary is isolated/uncommitted; detailed evidence/limits and build results are in the completed C41 batch checkpoint and independent report.

<a id="d01"></a>
### D01 — Reconcile Rules wording with the accepted HowTo decision

- **Evidence:** U1-C1; [unit 1 report](/private/tmp/bingo-sweep-unit1-20260909.md). Priority/type: Authority.
- **Problem:** Active product/data/technical/functional documents still require editable Rules, while the recorded accepted F-06 decision supplies HowTo and excludes that editor.
- **Scope / authority:** Named Rules/HowTo authority sections and accepted F-06 evidence; no application change.
- **Acceptance:** Promote the existing accepted decision consistently into the smallest owning sections. If provenance does not establish it, ask only that unresolved decision. Do not create a Rules feature from stale wording.
- **Focused checks:** Focused cross-document reference/meaning check and diff check.
- **Dependencies / decisions:** None
- **Existing data:** No data/schema/editor changes; do not remove retained records based on wording.
- **Outcome:** User approved documentation-only reconciliation (2026-09-09). Functional/product/data/technical guidance now matches accepted HowTo scope; no runtime Rules entity/editor is required. Scoped documentation checks completed; no application/schema changes.

<a id="d02"></a>
### D02 — Document the unpublished-draft cancellation exception

- **Evidence:** Unit 3 authority clarification; [sweep evidence index](DELIVERY_PLAN.md#178-consolidated-review-outcome--2026-09-09). Priority/type: Authority.
- **Problem:** Specific accepted private-draft cancellation permits editable Setup, while blanket wording says recorded picks never unlock structure.
- **Scope / authority:** PRODUCT_REQUIREMENTS specific cancellation versus general lock clauses; existing CancelPrivateDraft test.
- **Acceptance:** State the existing narrow unpublished-cancellation exception while preserving history and ordinary undo locks. No behavior change.
- **Focused checks:** Focused authority consistency check; do not rerun existing passing tests just for prose.
- **Dependencies / decisions:** None
- **Existing data:** No retained-data rewrite expected; preserve existing records.
- **Outcome:** User approved the existing narrow cancellation exception (2026-09-09). Product/data/functional/technical wording now preserves ordinary undo/published-reopen locks while allowing explicit unpublished-attempt cancellation with retained history. Documentation only.

<a id="d03"></a>
### D03 — Decide the catalogue import surface

- **Evidence:** U4-07; [unit 4 report](/private/tmp/bingo-sweep-unit4-20260909.md). Priority/type: Decision.
- **Problem:** Contract requires SuperAdmin preview/hash/confirmed apply; the inspected app has CRUD and separate operator CLI import rather than that journey.
- **Scope / authority:** Contract §9.2, product scope and current operator import boundary.
- **Acceptance:** User chooses whether application preview/apply is required now, deferred, or superseded by an explicitly defined operator workflow. Record actors, preview freshness, authorization, atomicity and non-goals. Close as reconciled/deferred only on an explicit decision; if implemented, create bounded child tickets before dispatch.
- **Focused checks:** Compare the proposed outcome with the existing implementation evidence only; no new whole-app review or CLI execution.
- **Dependencies / decisions:** Product decision required.
- **Existing data:** No import, migration, catalogue deletion or production mutation authorized by this decision record.
- **Outcome:** Closed — no change, clarified by the user 2026-09-14: do not build the application catalogue-import preview/apply interface. Existing operator tooling remains separate. This is a resolved scope exclusion, not unfinished implementation.

<a id="d04"></a>
### D04 — Reconcile evidence zoom authority with accepted interaction

- **Evidence:** Unit 5 zoom authority conflict; [sweep evidence index](DELIVERY_PLAN.md#178-consolidated-review-outcome--2026-09-09). Priority/type: Authority.
- **Problem:** Older UI wording requires separate zoom controls; later accepted interaction explicitly uses click-focused zoom and keyboard/pointer/touch without a control bar.
- **Scope / authority:** UI_SYSTEM evidence viewer clauses and accepted DELIVERY_PLAN6.1 evidence interaction.
- **Acceptance:** Align wording with the existing accepted interaction and accessibility requirements; no historical-reference comparison or redesign.
- **Focused checks:** Scoped authority consistency check; T02 uses the resolved invariant.
- **Dependencies / decisions:** None
- **Existing data:** No retained-data rewrite expected; preserve existing records.
- **Outcome:** User approved documentation-only reconciliation (2026-09-09). UI_SYSTEM now states the accepted click-focused magnifier/keyboard/pan interaction without a separate control bar. No viewer or test changes.

<a id="d05"></a>
### D05 — Decide restoration when another event is current

- **Evidence:** U7-C1; [unit 7 report](/private/tmp/bingo-sweep-unit7-20260909.md). Priority/type: Decision.
- **Problem:** Hidden events do not block current-event selection; restoring one can conflict with the general singleton rule, while the specific restore contract says clear metadata only.
- **Scope / authority:** Quarantine and lifecycle/product/data authorities; existing restore behavior.
- **Acceptance:** User decides eligibility/recovery when restoration would produce conflicting current states, including operator/Admin feedback and preservation of original lifecycle/history. Update owning rules and create a bounded implementation ticket if behavior changes; no silent lifecycle transition.
- **Focused checks:** Decision table for no conflict versus conflict, then exact acceptance conditions for any child ticket.
- **Dependencies / decisions:** Product decision required.
- **Existing data:** No hide/restore/archive or lifecycle changes applied to existing events as part of planning.
- **Outcome:** User defers added restore/collision handling (2026-09-09); hiding is used for unwanted/abandoned or test events kept off the front page. This operational intent does not alter allowed lifecycle states or the distinct Discarded enum. Functional8.1a records the limitation.

<a id="d06"></a>
### D06 — Reconcile the accepted Admin inbox WIP boundary

- **Evidence:** Unit 8 inbox scope remnant; [sweep evidence index](DELIVERY_PLAN.md#178-consolidated-review-outcome--2026-09-09). Priority/type: Authority.
- **Problem:** General contract describes a complete inbox/action projection, while later accepted Admin dashboard remains WIP with existing routes as operational entry.
- **Scope / authority:** Current accepted dashboard/WIP scope and contract §9.5.
- **Acceptance:** Document the existing Admin actions projection on Notifications separately from the accepted WIP `/Admin` dashboard. Preserve working actions and direct operational routes. No new dashboard/inbox feature is implied.
- **Focused checks:** Focused accepted-decision reconciliation; no new dashboard work.
- **Dependencies / decisions:** None
- **Existing data:** No retained-data rewrite expected; preserve existing records.
- **Outcome:** User approved reconciliation (2026-09-09). Current screenshot and Notifications source establish an existing Admin actions section. Functional/product/technical wording now distinguishes it from the separately WIP `/Admin` dashboard; no existing actions are deferred or removed.

<a id="d07"></a>
### D07 — Decide whether whole-script repeat evaluation is supported

- **Evidence:** public-header-popover.test.js failure; [sweep evidence index](DELIVERY_PLAN.md#178-consolidated-review-outcome--2026-09-09). Priority/type: Decision.
- **Problem:** Test forbids lexical declarations to permit evaluating all of site.js twice, but inspected production layouts load once and reinitialize through content-updated events.
- **Scope / authority:** Shared-script loading/reinitialization contract and test expectation.
- **Acceptance:** Resolve whether the supported contract includes whole-script repeated loading or only component reinitialization. Preserve a discriminating gate for the chosen supported behavior; absence of a found trigger alone does not justify deleting the test.
- **Focused checks:** Use existing review evidence and relevant runtime contract; investigate only a named unresolved loader path that could change the decision.
- **Dependencies / decisions:** Contract decision required before T05.
- **Existing data:** No retained-data rewrite expected; preserve existing records.
- **Outcome:** User accepted supported component reinitialization (2026-09-09). UI_SYSTEM records one shared script load plus idempotent content-updated initialization; no whole-script repeated-loading requirement or font change. T05 may repair only the test.

<a id="t01"></a>
### T01 — Repair three stale C# UI source assertions

- **Current acceptance — 2026-09-14: Done.** User passed the mapped manual checks and accepted MR-26 internal evidence on the independently passed integrated candidate. Earlier outcome paragraphs retain historical per-batch evidence and limits; their pending-manual wording is superseded. This does not authorize packaging or publication.

- **Evidence:** Three failing BrowserTests C# assertions; [sweep evidence index](DELIVERY_PLAN.md#178-consolidated-review-outcome--2026-09-09). Priority/type: Test.
- **Problem:** Board test bans accepted navigation, Accounts test demands old desktop gating, Participant test expects an obsolete initializer target spelling.
- **Scope / authority:** BoardEditingUiTests.TileActionsSurviveBoardCellSwaps; AccountsUiTests.AccountsMarkupKeepsTheTwoDatasetsAndDirectSafetyBoundaries; EventCreationUiTests.ParticipantActionsKeepRouteFallbackAndShareDesktopDialogContract.
- **Acceptance:** Assert current tile delegation/recovery, every-width route-backed modal behavior, and initialization on the inserted editor. Do not remove meaningful assertions simply to get green; production source stays unchanged unless a real defect is separately ticketed.
- **Focused checks:** Run the three corrected tests and any affected test helper consumers only; record further failures instead of masking them.
- **Dependencies / decisions:** None
- **Existing data:** Test-only; no app/database changes. Any required HTTP fixture uses disposable DB/storage.
- **Outcome:** Verified existing corrections without new C# edits; three distinct named cases pass. Independent review and bounded F01 recheck cleared the batch 2026-09-14; Awaiting manual acceptance under existing policy. No cumulative release claim. See completed batch checkpoint/recheck report.

<a id="t02"></a>
### T02 — Replace superseded public UI assertions with current invariants

- **Current acceptance — 2026-09-14: Done.** User passed the mapped manual checks and accepted MR-26 internal evidence on the independently passed integrated candidate. Earlier outcome paragraphs retain historical per-batch evidence and limits; their pending-manual wording is superseded. This does not authorize packaging or publication.

- **Evidence:** Node public-evidence, public-recent-drops, team-board-overlay failures; [sweep evidence index](DELIVERY_PLAN.md#178-consolidated-review-outcome--2026-09-09). Priority/type: Test.
- **Problem:** Early source assertions target the old zoom-control bar, masthead rank composition and tile overline; later runtime assertions have not executed.
- **Scope / authority:** The three named Node files and current accepted Board/evidence contracts.
- **Acceptance:** Tests protect accepted evidence interaction and public result/team/tile behavior without requiring retired classes/composition. Run previously blocked remainder and report actual newly exposed defects separately.
- **Focused checks:** Run those three files after focused assertion corrections; inspect discriminating behavior, not redundant markup spelling.
- **Dependencies / decisions:** D04 for evidence zoom wording.
- **Existing data:** Test-only; preserve all approved production composition.
- **Outcome:** Replaced superseded assertions in three assigned Node files with accepted evidence/public result/team/tile invariants. All three files pass; applicable evidence and initial independent review reused during F01 recheck. Awaiting manual acceptance. Explicit no-control-bar contract governed despite recorded central zoom wording discrepancy. See completed checkpoint/recheck report.

<a id="t03"></a>
### T03 — Correct the leaderboard classList test double

- **Current acceptance — 2026-09-14: Done.** User passed the mapped manual checks and accepted MR-26 internal evidence on the independently passed integrated candidate. Earlier outcome paragraphs retain historical per-batch evidence and limits; their pending-manual wording is superseded. This does not authorize packaging or publication.

- **Evidence:** public-leaderboards.test.js failure; [sweep evidence index](DELIVERY_PLAN.md#178-consolidated-review-outcome--2026-09-09). Priority/type: Test.
- **Problem:** Mock toggle implements only forced toggle; production correctly uses native one-argument toggle, causing a false collapse failure.
- **Scope / authority:** Existing RuntimeElement.classList mock and the newly exposed stale source-loading/assertion remainder in public-leaderboards.test.js. Planner bounded clarification 2026-09-14 under the authorized test-repair batch permits updating retired masthead/metric assertions, localization-aware expectations and the existing table/panel/header anatomy in Board.cshtml. No production/UI policy change or expansion to other test files.
- **Acceptance:** Mock matches one-argument and forced toggle semantics, including return value as used. Existing expanded/collapsed interaction assertions execute. Update the same file’s newly exposed stale remainder to protect the existing accepted composition, localization and table/panel/header anatomy while retaining meaningful interaction, accessibility, sorting/pagination, links and data-scope assertions. Record each obsolete expectation and its replacement rationale; no blanket regex weakening or removal of safeguards, no production workaround.
- **Focused checks:** Run the corrected leaderboard file; no new DOM framework.
- **Dependencies / decisions:** None
- **Existing data:** Test-only.
- **Outcome:** Corrected native toggle semantics and bounded stale same-file source assertions against existing Board.cshtml composition/localization. Reference-integrity safeguard exposed F01, a real dangling Players panel label. User explicitly authorized its bounded production remediation; panel now references the existing unique localized view link with reciprocal controls. Unchanged normal leaderboard file now passes; same reviewer cleared F01, affected Web/Razor build zero warnings/errors. Awaiting manual acceptance; no visible composition change or assistive-technology/manual approval. See completed checkpoint/recheck report.

<a id="t04"></a>
### T04 — Load a complete toast test context

- **Current acceptance — 2026-09-14: Done.** User passed the mapped manual checks and accepted MR-26 internal evidence on the independently passed integrated candidate. Earlier outcome paragraphs retain historical per-batch evidence and limits; their pending-manual wording is superseded. This does not authorize packaging or publication.

- **Evidence:** transient-toast.test.js failure; [sweep evidence index](DELIVERY_PLAN.md#178-consolidated-review-outcome--2026-09-09). Priority/type: Test.
- **Problem:** Extracted site.js slice references the earlier initializePublicHeaderPopovers definition absent from the fixture, so tests stop before toast behavior.
- **Scope / authority:** Existing transient-toast extraction/context fixture.
- **Acceptance:** Evaluate the required existing dependencies in a bounded faithful fixture. Preserve toast severity/duration/dismissal assertions and surface failures in the previously unexecuted remainder.
- **Focused checks:** Run transient-toast file and affected shared fixture consumers if any; no production stub just for the test.
- **Dependencies / decisions:** None
- **Existing data:** Test-only.
- **Outcome:** Corrected bounded toast test context so existing toast behavior assertions execute; normal Node file passes. Independent review and F01 recheck reuse its unchanged passing source/evidence. Awaiting manual acceptance. No production toast change. See completed checkpoint/recheck report.

<a id="t05"></a>
### T05 — Test the agreed shared-header initialization contract

- **Evidence:** public-header-popover.test.js failure; [sweep evidence index](DELIVERY_PLAN.md#178-consolidated-review-outcome--2026-09-09). Priority/type: Test.
- **Problem:** The failing repeated-whole-script expectation has no established ordinary production trigger and requires D07 disposition.
- **Scope / authority:** public-header-popover.test.js and only the contract-approved initializer scope.
- **Acceptance:** Verify supported initialization/reinitialization without duplicate handlers or broken disclosure behavior. If D07 requires production repeated-load support, split a separately approved correction from this test ticket; do not silently alter site.js here.
- **Focused checks:** Run header file under the agreed contract and relevant fixture consumers.
- **Dependencies / decisions:** D07.
- **Existing data:** Test-only unless a separately approved child correction is created.
- **Outcome:** Done 2026-09-09. Only public-header-popover.test.js changed: unsupported whole-script repeat assertion replaced with current component reinitialization coverage, accurate disclosure/listener fixture, and localized catalogue assertion. Independent Astra Medium review cleared scope and named the localized-assertion correction; implementer applied it. Bundled Node targeted run passed (1 file, 0 failures); scoped diff check passed. No production script/font changes, full-suite rerun or manual UI acceptance claimed.

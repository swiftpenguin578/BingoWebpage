# Delivery plan

**Active plan:** 2026-09-01. This document owns remaining delivery,
documentation/UI order, and release gates. Current page approval/status is
owned solely by [`UI_PAGE_MATRIX.md`](UI_PAGE_MATRIX.md).
[`CURRENT_STATUS.md`](CURRENT_STATUS.md) remains the authority for checkout
state, blockers, limitations, current work, and immediate ownership. Global UI
rules and implementation ownership are defined by [`UI_SYSTEM.md`](UI_SYSTEM.md).

## 1. Documentation consolidation

1. **UI authority pass — complete:** `UI_SYSTEM.md` holds active global UI
   rules and exact implementation ownership; `UI_PAGE_MATRIX.md` holds active
   page families, protected composition, exceptions, approvals, and gates. The
   old Admin contract and UI roadmap are preserved as exact archive copies
   behind root tombstones.
2. **Workflow authority pass — complete:** `FUNCTIONAL_CONTRACTS.md` holds
   final end-to-end journeys, actors, reachability, authority handoffs,
   failure/recovery behavior, and acceptance outcomes. The old
   `FUNCTIONAL_WORKFLOWS.md` is a non-authoritative root tombstone and its exact
   source is preserved under `docs/archive/`.
3. **Archive promotion — complete (2026-08-15):** the superseded
   implementation roadmap, completed Slice 1–10 implementation plans, and
   Slice 1–3 manual result records were preserved as exact working-tree copies
   under `docs/archive/`. Their source paths, archive paths, SHA-256 values,
   source working-tree/base identification, and current destinations are
   recorded in [`docs/archive/INDEX.md`](docs/archive/INDEX.md). No durable
   rule was missing from the active authorities, and no candidate root files
   remain.
4. **Core-document reconciliation — F-05 subpass complete (2026-08-15):**
   active notification terminology, persistence shape, privacy/read-state,
   destination, authority, and selective idempotency wording was reconciled
   against accepted code. This was documentation-only; no product decision or
   implementation change remains.
5. **Application Atlas retirement and durable-finding routing — complete
   (2026-08-15):** the exact Markdown/HTML working-tree bytes are preserved in
   `docs/archive/superseded-assessments/`, all durable items are either routed
   to an active owner or explicitly archive-only, and F-04/F-06 are resolved.
   No broader compression or further archive promotion is
   included.
6. **Active core-document boundary reconciliation — complete (2026-08-15):**
   active authority boundaries, stale status framing, and cross-document
   routing were reconciled without changing product/UI behavior or promoting
   archive material. F-04, F-05, and F-06 are resolved.
7. **Replacement-link/content verification — complete (2026-08-15):** 15
   active root Markdown files and 62 local links were checked with no broken
   targets or anchors; 20 archived files/hashes match
   `docs/archive/INDEX.md`; 3 root tombstones are short, rule-free,
   non-authoritative, and correctly linked; and the manual checklist has 24
   headings and 180 checkbox items with a valid archive-evidence link. No stale
   retired-root links, stale phrases, duplicate authority entries, or
   Atlas-as-active wording remain. Documentation consolidation is complete;
   `git diff --check` passed for the documentation/archive changes. The current
   UI order is owned by section 2 below. No release-readiness claim is included.

8. **Hidden-event quarantine implementation — complete, manually accepted, and
   included in the deployed PR #5 candidate (2026-08-31):** migration
   `20260831142836_AddEventQuarantine` is included in the production candidate;
   the affected Web Release build, domain quarantine (10),
   destination policy (20), quarantine integration (initially 2, then focused
   remediation suite 6), migration rehearsal (1), architecture, Bash syntax,
   and `git diff --check` gates passed. An independent Sol High review found
   four initial blockers—Hide reachability/rendering, emergency-credential
   access/audit, realtime access/invalidation, and legacy notification
   backfill/index—which focused Luna xhigh remediation closed. Follow-up review
   closed two migration-only emergency-audit classification issues; final
   independent closure verdict is PASS. The user manually accepted the bounded
   rendered navigation and Hide/Restore journeys after that remediation.

   The contract remains limited to `AwaitingFinalReview`, `Finalized`, and
   `Archived` eligibility, the separated SuperAdmin Hidden area/limited Manage
   inspection, and fail-closed ordinary paths. The production rehearsal event
   remains Hidden and is reachable only to SuperAdmin through
   `/Admin/Events?filter=hidden`; the separate historical import succeeded on
   2026-09-01. The accepted manual and automated whole-application regression
   and production launch are recorded below.

## 2. Launch and UI order

The user approved a complete Public UI identity replacement on 2026-08-22.
The public Board ecosystem's accepted behavior remains protected, except for
the user-approved 2026-08-24 correction that makes team cards navigate to the
ordinary team-board page rather than opening a team-board popup. Its old visual
identity is superseded and is reopened only for the ordered structural migration
below. Admin is outside this experiment. The frozen pass order is:

The user-authorized bounded masthead normalization prerequisite for the existing
left-side text roles in Signup create/edit, Signups, Teams, the three Board
views (View Bingo, Recent Drops, and Leaderboards), and Captain operations/detail
was completed before the accepted Board and canonical submission work. Use Privacy as the shared
implementation authority for kicker/title/support typography, `.65rem` internal
stack rhythm, and the ordinary versus Board-only reduced shell gap. Preserve
each family's right-side additions, metadata, artwork, rails, actions, and lower
geometry. This normalization did not change the frozen pass order or grant
manual approval; page-specific approvals remain authoritative.

1. **Pass 0 — authority and non-breaking foundation:** reconcile the active
   public visual authorities and define the reusable light/dark tokens, theme
   hook, typography, stable family layouts, outline controls, focus, motion,
   and stylesheet ownership. Do not globally alter an unmigrated family's
   geometry.
2. **Pass 1A — atomic landing rewrite, complete and accepted 2026-08-22:**
   replaced `/` presentation markup and
   responsive composition structurally from the approved landing reference.
   Preserve the landing PageModel, event projections, destinations, standalone
   Login navigation and validated local `ReturnUrl`, localization, and routes;
   do not preserve legacy/generic public
   Razor composition, old containers, generic component geometry, typography,
   links, buttons, icons, or responsive CSS. Static landing editorial copy is
   composition-flexible: it may be rewritten, shortened, reordered, or replaced
   in natural English and Danish to serve the approved hierarchy, rhythm, and
   line lengths. Preserve product meaning and CTA destination/action semantics;
   dynamic event facts and backend behavior remain frozen. The user manually
   accepted the final landing result.
3. **Pass 1B — launch-critical signup/auth, complete and accepted:** Signup,
   Confirmation, Login, Onboarding, AccessDenied, Error, and StatusCode are
   complete from their approved authentication/status contracts. Preserve their
   accepted routes, handlers, localization, and standalone Login navigation.
4. **Pass 2 — account and public utilities, complete and accepted:** Settings,
   Change Password, My Accounts, My Events, Forgot/Reset Password, Setup,
   Notifications, and Privacy are complete. The `/HowTo` guide was subsequently
   implemented and approved in `50077fd`; preserve its five-step content and
   ordinary anchor fallback.
5. **Pass 3 — roster/event family, complete and accepted:** exact-link Signups and
   Teams/roster pages,
   including phase-safe, privacy-safe, empty, and permission states. The user
   approved PUB-REF-16 for Teams/roster on 2026-08-24: use a left title/back/time
   masthead with the existing Landing-family diagonal DK artwork integrated on
   the right; omit team images and role icons; distinguish Captain/Co-captain by
   text color; group teams with whitespace rather than an inter-team divider
   grid; and show two draft picks per row at large widths. The generated image's
   uneven spacing and detached-looking mark are directional artifacts rather
   than pixel targets. This reference approval does not itself authorize Pass 3
   implementation.
6. **Pass 4 — public Board ecosystem, complete and accepted:** the structural
   presentation rewrite of the Board masthead, the ordinary TeamBoard page, Tile/sidebar, approved
   Evidence/lightbox, and the shared submission drawer. PUB-REF-02,
   PUB-REF-03, PUB-REF-04, PUB-REF-14, and PUB-REF-15 are explicitly reactivated
   targets for this pass; current user screenshots are rejection evidence only.
   Team cards navigate normally to the existing TeamBoard route at every
   viewport; remove the popup interception and popup-only restoration behavior
   without creating a new route or navigation framework. Preserve every
   board/team/tile route, ordinary history, focus, tile-sidebar, submission,
   evidence, authorization, and realtime contract. The current team-overview
   grid beneath the masthead is explicitly protected as already near target;
   preserve it and add the missing PUB-REF-02 Recent Activity footer rather
   than rewriting that grid. The shared drawer remains
   the only participant/Captain submission interface. Light composition is
   canonical; dark changes tokens only and must not change dimensions,
   placement, tile geometry, or outlined tile numbers. A legacy-composition
   reskin does not satisfy this pass.

   Pass 4 is implemented in three bounded subpasses:

   - **4A — Board masthead and overview completion:** structurally replace the
     rejected equal-panel masthead from PUB-REF-02, preserve the current
     near-target team-overview grid exactly except for required masthead
     integration, and add the missing real Recent Activity footer beneath the
     grid. The Board overview uses the shared 88rem Wide page width and normal
     responsive gutter; this does not copy Landing hero height or artwork. Do
     not change team navigation,
     TeamBoard, Tile, submission, Recent Drops, or Leaderboards in this subpass.
   - **4B — ordinary TeamBoard workspace:** atomically change team-card
     navigation to the ordinary TeamBoard page, remove popup interception and
     restoration behavior, rebuild TeamBoard from PUB-REF-03, and retain/adapt
     the real tile-rail, shared submission drawer, evidence viewer, focus,
     history, and realtime behavior. The popup must not be retired in a separate
     earlier change that leaves those interactions without an owner.
   - **4C — secondary Board views:** structurally reconcile Recent Drops,
     Leaderboards, their empty/filter/narrow states, and shared evidence
     presentation against PUB-REF-04, PUB-REF-14, and PUB-REF-15 without data,
     query, service, Admin Preview, overview-grid, or TeamBoard behavior changes.
7. **Pass 5 — canonical submission workspace consolidation, complete:** the
   Captain and participant submission workspaces are consolidated into one
   implementation owned by `/Submissions` and `/Submissions/{id:guid}`. The
   accepted implementation preserves the complete retained ledger, Captain-only
   focus/status sections, server-authorized role boundaries, canonical
   notification destinations, compatibility aliases, drawer transport, and
   Admin authority. It was independently reviewed, remediated, manually
   accepted, and committed in `88cd8f8d6014e947e2a5e97717be460ca2ea9d66`.
8. **Remaining Admin UI families** — deployment-ready implementation remains
   subject to the recorded page-specific manual-approval state, but that manual
   approval is not a gating task before whole-application regression or release.
   Currently unapproved Admin visual debt is non-blocking while functionality
   works; security, authorization, privacy/data-loss/data-integrity, and
   workflow-blocking defects may still interrupt launch-critical work.
9. **Dashboard/action inbox** — deployment-ready intentional shell-owned WIP
   presentation; it is not a gating manual-approval task and does not precede
   whole-application regression or release.
10. **Whole-application regression and production release gates — complete:**
    the user accepted manual whole-application regression based on sustained
    site use, and the recorded automated local regression passed. PR #5 was
    merged and deployed with passing CI, deployment, and focused production
    smoke. The planned Admin test event remains the real-world follow-up safety
    net and has not run.

### Pass 5 completion record — canonical submission workspace

**Approved outcome and completion — 2026-08-31.** This was an implementation
consolidation and routing/authorization correction, not a visual redesign. The
canonical overview is `/Submissions` and the canonical detail is
`/Submissions/{id:guid}`. The overview is team-wide for authorized current
members and includes retained rows credited to departed teammates. Captains and
co-captains see team focus and team submission status as the two Captain-only top
sections and retain server-authorized broader editing of eligible team
submissions. Ordinary participants do not see those sections and may edit only
their own eligible non-read-only submissions. The detail remains visually
equivalent to the approved Captain submission detail.

**Compatibility and destinations.** `/Captain` and
`/Captain/Submissions/{id:guid}` are thin compatibility redirects/aliases to the
canonical routes and never separate rendered implementations. Cross-role legacy
links must resolve through the canonical route and authoritative server
authorization. Personal submission/evidence notifications, including those
received by Captains/co-captains, resolve to `/Submissions/{id:guid}`. Relevant
general submission navigation resolves to `/Submissions`. Admin review
notifications remain `/Admin/Review/Details/{id}`. `/Captain/Submit/{tileId?}`
remains only the shared drawer transport/handler plus compatibility redirect.

**Protected boundaries and clarification.** The implementation did not redesign
accepted submission UI, add a second
workspace, weaken owner/team/captain authorization, alter retained-state
read-only or cutoff rules, change privacy/evidence-integrity/Admin authority,
change persistence or submission query semantics, add no-JavaScript-only parity,
or broaden into unrelated cleanup. The reported absence of a linked resubmission
from Admin Evidence Review was a reader/reviewer misinterpretation; source
inspection found no query exclusion. Existing behavior and tests remain
protected, and no query change or additional manual release gate is required.

**Verification and acceptance.** The implementation was independently reviewed,
remediated, manually accepted, and committed in
`88cd8f8d6014e947e2a5e97717be460ca2ea9d66`. Verification included Release
builds, 7/7 navigation/integration tests, notification workflow, UI assertions,
focus helper, ledger JavaScript, diff checks, independent review, and user
acceptance. The relative `_EvidenceUpload` partial 500 is fixed. The existing
linked-resubmission behavior and tests remain protected; the reported absence
from Admin Evidence Review was a reader/reviewer misinterpretation, not an
additional release gate.

**Complexity budget and stop rule.** The completed pass added zero tables,
migrations, jobs, NuGet dependencies, navigation frameworks, generalized
abstractions, or new rendered page families. Reuse the existing submission services, persistence,
drawer transport, notification model, policies, and approved detail composition;
the canonical route owner, compatibility aliases, role-conditioned sections, and
directly required focused tests/source fixes were the only additions in budget.
Future changes stop for user direction before changing any approved product
rule, persistence/query semantics, Admin authority, or this scope.

### Pass 1B implementation contract — signup, authentication, and status

**Approved outcome and references.** Rebuild the scoped public presentation
structurally from PUB-REF-05 (Signup), PUB-REF-06 (Confirmation), and PUB-REF-09
(Authentication/status). The accepted landing shell, typography roles, light and
dark token discipline, focus treatment, and restrained motion are reusable; the
accepted landing composition itself is frozen. The condensed PUB-REF-09 sheet
defines hierarchy and relationships, not literal panel dimensions.

**In scope.** `/Events/{slug}/Signup` create/edit, `/Events/{slug}/Confirmation`,
`/Account/Login`, the shared Login form partial, `/Account/Onboarding`,
`/Account/AccessDenied`, `/Error`, and
`/Errors/StatusCode`. Implement new family-owned Signup, Confirmation,
Authentication, and Status markup/primitives in `site.public-ui.css`; replace
the scoped pages' generic `public-ui-*` surface/masthead/data/action composition
rather than stacking another override layer over it. The standalone Login route
is the sole runtime login surface. Anonymous signup links navigate there with a
validated local `ReturnUrl`; the superseded route-backed login popup/dialog and
its exclusive runtime machinery are removed. Remove only superseded rules proven
to belong exclusively to these scoped pages.

**Frozen behavior.** Preserve every PageModel, handler, Razor route,
authorization boundary, validated local `ReturnUrl`, Discord callback/onboarding
state, login throttling and generic disclosure, password/remember-me semantics,
signup question and account binding, Wise Old Man fetch behavior, response
version, validation/error focus, capacity and queue outcome, edit/withdraw/rejoin
permissions, confirmation projection, and ordinary route/form fallback. Dynamic
event names, descriptions, dates, questions, answers, status, queue positions,
and account facts remain authoritative. Static copy may change only where the
reference composition needs it, without changing meaning, claims, or actions.

**Localization and states.** Every new public string is localized through the
existing English-default/Danish system. Any visible JavaScript value is passed
through rendered localized markup/data. Cover create/edit, unavailable and
cancelled signup, validation and lookup failure, confirmed/waiting/read-only
confirmation, Login validation, onboarding validation/lookup/expiry, 403, 404,
and 500/request-ID states. Light and dark use
identical geometry. Desktop, narrow/mobile, zoom/translation, keyboard/focus,
reduced motion, empty/error/permission states, and disabled controls must remain
usable.

**Reachability.** Development reset TEST 16 (`test-16-signup-lookup`) contains
six confirmed and three waiting participants at capacity six; `SeedAdminTwo`
provides the deterministic new waiting signup, edit, and Wise Old Man path. The
same account owns a confirmed read-only TEST 13 signup reachable through Account
→ My events. Signed-out landing/signup entry exercises Login with the real local
return path. Onboarding validation/expiry needs a real configured Discord
callback state; 403/404 use existing authorization/missing-route handling; 500
is an exception-handler state outside Development rather than a reset journey.
Do not add demonstration data solely to make these visual states convenient.
The landing must list TEST 16 as a public signup-open event even though it has no
published roster or board; its destination remains the existing Signup route.

**Complexity budget.** Zero new tables, migrations, services, routes, policies,
jobs, JavaScript frameworks, stylesheets, navigation systems, or generalized UI
abstractions. Use the existing pages, shared shell/dialog, localization, scripts,
and `site.public-ui.css`. The smallest directly affected resources and focused
tests may change. A PageModel/backend change, new reusable framework, or fourth
stylesheet exceeds this budget and requires a new user decision.

The user-authorized TEST 16 correction is the sole PageModel exception: adjust
only the landing discovery predicate in `Index.cshtml.cs` so a public
signup-open event (`FirstPublicAt` set) is eligible without a roster or board,
and retain the existing focused integration coverage. Do not change landing
markup, CSS, ordering, destinations, or private-event fail-closed behavior.

**Explicit non-goals.** Do not touch Admin, the accepted Landing composition,
Board, Captain, the public
Signups table, Settings/Setup, password recovery pages, My Accounts/My Events,
Notifications, Privacy/HowTo, global business/data rules, or later public
families. Do not resolve F-04 or F-06. Do not redesign the global header,
navigation, dialog loading protocol, or authentication workflow.

**Readiness decisions — 2026-08-22.** The user approved preserving the existing
immediate `/Account/DiscordComplete` processing redirect. Its progress panel in
PUB-REF-09 is not a runtime acceptance state, and `DiscordComplete.cshtml` plus
its handler remain outside the presentation rewrite. Update `SignupUiTests` to
retain semantic field names, validation, lookup hooks, and fallback-form
assertions while removing assertions for the generic composition this pass
replaces; keep public Signups-table assertions and that page untouched.

**Verification and approval gate.** The implementer performs one deliberate
first-pass comparison of each reference family at its intended desktop viewport,
then checks dark desktop and narrow/mobile recomposition; small corrections do
not require repeated screenshot forensics unless explicitly requested or a
material visual uncertainty remains. Run `git diff --check`, the focused Web
Release build, the smallest affected signup/dialog/onboarding tests, an EN/DA
visible-string scan, and a scoped Admin/later-family leak check. One fresh Terra
High independent reviewer then compares the complete scoped result and visual
evidence with this final contract. Only concrete findings receive a fresh,
bounded Luna High remediation and focused verification. Stop for the user's
manual visual approval after review/remediation and before Pass 2.

**Implementation stop rule.** Stop for user direction before changing product
behavior, backend authority, a route or handler, the complexity budget, the
accepted landing, the shared dialog protocol, or an out-of-scope family; also
stop if the references cannot resolve a materially different composition.
Record unrelated defects separately unless they prevent safe Pass 1B work.

**Pass 1B review/remediation checkpoint — 2026-08-22.** The fresh independent
review found three bounded presentation defects: shared controls/actions still
depended on rejected `body.public-ui-pass1` styling, two Signup section numbers
were blank, and Onboarding displayed an empty validation panel. A fresh focused
remediation made the family tokens/controls/actions/dialog styling independent,
restored `01`/`02`, and hides only the empty validation target while preserving
populated/focusable errors. Release Web build, focused Signup UI, public-dialog
and onboarding-WOM tests, diff/conflict, and scope checks pass. The user manually
accepted Signup and Confirmation on 2026-08-22 and Login, Onboarding,
AccessDenied, StatusCode, and Error on 2026-08-23 after bounded visual
remediation. Pass 1B is complete. Do not begin Pass 2 until its separate
user-authorized gate.

### Pass 2 implementation contract — user authorized 2026-08-23

**Scope and order.** Pass 2 structurally rebuilds the presentation of
`/Account/Settings`, `/Account/ChangePassword`, `/Account/ForgotPassword`,
`/Account/ResetPassword`, `/Account/Setup`, `/Account/MyAccounts`,
`/Account/MyEvents`, `/Notifications`, and `/Privacy`. Use PUB-REF-07 and
PUB-REF-08 for the account-form families, PUB-REF-10 for account overview,
PUB-REF-11 for Notifications, and the real-content Privacy composition in
  PUB-REF-12. The approved `/HowTo` guide resolves F-06 and remains outside this
  pass. The accepted shared public header and every accepted Pass 1 page are
  frozen. Admin, Board, Captain, event
roster pages, dashboard/Admin-actions specimens, and later families remain out
of scope.

**Presentation replacement.** Preserve PageModels, handlers, routes, field
names, validation, password and recovery rules, safe-return behavior, account
identity and character-management semantics, Wise Old Man fetch hooks, event
facts and destinations, notification read/unread behavior and destinations,
and authoritative Privacy meaning. Replace page-specific legacy/generic Razor
containers, surface stacks, mastheads, row/card geometry, form groups, and
responsive flow wherever they conflict with the applicable reference. A new
wrapper around the old composition or a page-scoped override layer is not a
Pass 2 implementation. Static explanatory copy may be shortened, reordered, or
rewritten in natural English and Danish to serve the reference hierarchy, but
must not invent capabilities or alter dynamic, account, event, notification,
security, or legal facts.

**Shared UI and dark theme.** Reuse the accepted shared shell, typography roles,
control/action semantics, focus treatment, and `site.public-ui.css`; do not add
a stylesheet, navigation framework, or generalized component system. Light
references own geometry and responsive composition. Dark mode changes tokens
only: charcoal fields/canvas, cream primary text, warm-gray secondary text and
neutral dividers remain dominant. Smoky indigo/violet is a restrained identity
accent for selected links, focus, utility accents, or deliberate large display
details; it must not take over headings, body copy, form labels, borders, rules,
or whole page regions. Coral, sage, and bronze retain their semantic roles.

**Reference and screenshot workflow.** The implementer inspects the current
bindings and each applicable reference once before its first implementation
pass, then builds the reference-owned content structure directly. It does not
enter repeated screenshot/render investigations for small corrections. After
the implementation handoff, the user supplies actual-route screenshots at the
requested desktop and narrow widths. A fresh independent review compares the
code and those screenshots with the references; only concrete findings receive
bounded remediation. Source checks and tests protect behavior but cannot approve
visual fidelity. On 2026-08-24 the user deferred final manual approval and
authorized the remaining Public UI passes to proceed sequentially in the current
dirty tree. Each pass still completes implementation, current screenshots,
independent reference/screenshot review, and bounded remediation. It is then
recorded as `awaiting manual approval`, never approved, before the next pass
begins. The user may supply screenshots and corrections during the sequence.
The accumulated manual walkthrough occurs after the implementation sequence,
including a shared-shell/CSS regression check.
Continue without a page-by-page manual stop unless a real product decision,
security/authorization/privacy/data-integrity issue, environment blocker, or
scope conflict requires the user.

**Complexity budget and stop rule.** Add no table, migration, service, route,
policy, job, JavaScript framework, stylesheet, navigation system, or speculative
UI abstraction. Expected changes are the scoped Razor pages,
`site.public-ui.css`, and the smallest affected resources, existing-script
hooks, selectors, and focused tests. Stop for a user decision before changing
backend behavior, security or password policy, routes/handlers, notification
semantics, authoritative Privacy meaning, the shared header, an accepted page,
or this complexity budget. A missing reference is blocking only when no approved
sibling reference resolves the required composition.

**Verification limits.** Run bounded source/diff/localization/scope checks and
the smallest relevant tests that the environment supports. The existing
MSBuild named-pipe sandbox denial is an environment limitation: after one clear
failure, do not repeat the unchanged command or its dependent build/test path.
Record the unrun gate and continue with independent checks.

**My Accounts review/remediation checkpoint — 2026-08-23.** The independent
PUB-REF-10 review requires one bounded My Accounts remediation: make the Add row
three equal field tracks plus a separate aligned Add action; keep both Saved EHB
and Fetch-from-WOM pairs as one horizontal composite at every viewport; remove
the redundant section-heading rules while retaining the masthead divider; and
align the linked-character action band with reorder controls immediately after
the editable fields. The user superseded the visible registered-character
warning/confirmation checkbox with a short semantic label above the row and the
existing localized native confirmation-prompt pattern on Unlink. Accepting that
prompt may submit the existing confirmation value; dismissal, unavailable
JavaScript, or bypass remains fail-closed on the server, and unlinking never
changes the event registration. One fresh Luna High remediator may change only
`MyAccounts.cshtml`, `site.public-ui.css`, the smallest existing public-script
hook, directly affected EN/DA resources, and one focused selector/behavior test.
It must not change the PageModel, service, route, handler, authorization, or
registration semantics. The bounded Luna High remediation is complete in the
three permitted Razor/CSS/resource files; focused structure, responsive-cascade,
localization XML, fail-closed confirmation, and diff-hygiene checks pass. No
MSBuild/test command was run because the established host blocker is unchanged.
The user manually approved the My Accounts page on 2026-08-24 after its bounded
manual corrections.
The reviewer separately found that the current pass appears to change Fetch-WOM
persistence, fold/remove the Correct handler, and rename other account actions;
reconcile that protected behavior against the functional contract separately
before final My Accounts functional acceptance.

**My Accounts manual correction — 2026-08-23.** During manual acceptance the
user replaced the separate preferred-character action with one order-derived
rule: the active character in position 01 is always the sole preferred
character, and reordering transfers preference to the new first character. The
same correction shortens linked-row Fetch-from-WOM to the existing sync icon
plus `Fetch`, changes visible `Saved EHB` labels to `EHB`, and changes the short
warning to `Registered for an event`. This is an approved behavior correction,
not visual-only remediation; one bounded remediator may remove the obsolete
Preferred handler/control/service entry point, make normalization follow the
first active ordered link, update directly affected tests and localization, and
touch no unrelated account or event behavior.

The next manual correction gives the Add-character form an intermediate two-by-
two layout at the established `1200px` family breakpoint before its existing
single-column mobile stack. It also limits My Accounts saved-EHB entry, fetched
values, persistence, and rendering to two decimal places using standard decimal
rounding (`3000.09582` becomes `3000.10`) without
changing historical event snapshots or the wider EHB-calculation contract.
The bounded remediation is complete: the Add form now uses the approved
intermediate grid, My Accounts EHB mutation/fetch/rendering applies standard
two-decimal rounding, and reorder clears the old persisted preferred flag before
assigning position 01 inside the same transaction. Focused Release builds and
source/diff checks pass; the PostgreSQL integration scenario remains unrun
because this worker could not access the Docker socket.
The user subsequently approved the corrected My Accounts page. Stop here; do
not begin My Events or another Pass 2 page without the user's next authorization.

**My Events review checkpoint — 2026-08-24.** The user next authorized My
Events by supplying light desktop, dark desktop, and 390px narrow current-route
screenshots. One fresh Terra High reviewer compares that evidence and the
complete scoped My Events diff with PUB-REF-10. The reviewer remains read-only;
only concrete findings may receive a fresh bounded Luna High remediation. Do
not begin another Pass 2 page.

The user stopped that reviewer after its concrete row-composition finding.
PUB-REF-10 remains the sole visual target: current semantic backgrounds form
full-width bands where the reference uses a compact dot/label status followed by
a neutral vertical divider. One fresh bounded Luna High remediator may correct
only that finding and its necessary responsive composition. Existing Landing
assets or selectors may be reused where the similar structure genuinely matches,
but Landing is not a reference and its date gutter, columns, or composition must
not be copied. Preserve all current classification, ordering, privacy, destination
behavior, and approved Landing code; do not modify another family.

The bounded My Events correction is complete in its Razor/CSS selectors and its
focused Release build passed with zero warnings/errors. The user manually
accepted the resulting page on 2026-08-24 despite a remaining non-blocking visual
imperfection. Stop here; do not begin another Pass 2 page without the user's next
authorization.

The user then authorized one shared secondary-navigation correction. Existing
event-view and account-view links currently read as an extension of the colored
header; move that shared navigation below the masthead and apply PUB-REF-02's
content-level tab treatment. Preserve destinations, visibility, active state,
localization, focus, and narrow access. Do not change the approved account page
bodies, Board behavior, link inventory, or another page family. One fresh Luna
High implementer performs this bounded shared-shell/CSS correction and stops for
user screenshots and manual acceptance.

During that manual check, the user named one direct My Events correction: keep
row dividers between event entries but remove the bottom divider from the final
row in each Current Events or History list. The first selector-only correction
left the terminal bottom padding behind; the bounded continuation removed that
bottom padding while preserving top/inter-row spacing. No further My Events row
work is authorized before the same manual acceptance gate.

The shared-navigation manual check also requires one fresh bounded Luna High
remediator after the My Events selector task completes. Reduce the excessive top
padding only on pages that render secondary navigation; make both primary and
secondary active underlines thicker; and position the primary underline beneath
the text with the same internal padding as the secondary tabs rather than at the
masthead bottom. Preserve header height, alignment, mobile menu behavior, routes,
active-state semantics, page bodies, and all unrelated shell styling.

That bounded shared-navigation remediation is complete. It uses one layout-owned
body context for pages that actually render the secondary row, reduces Account
and Board top spacing at desktop and narrow widths, uses matched 3px active
underlines, and keeps the primary rule beneath its label without affecting mobile
menu dividers. The focused navigation test, scoped diff check, and Release Web
build pass. Stop for user visual acceptance.

The next manual finding is one shared navigation-state correction: give the
content-level account/event tabs the primary header's text-color-only hover cue,
and restore a distinct dark-mode resting color so primary and secondary hover
states remain visible. One fresh Luna High remediator may touch only the shared
navigation CSS and its focused assertion; do not change geometry, active
underlines, links, routes, page bodies, or other states.

That hover-state correction is complete in shared CSS and its focused assertion.
The focused public-dialog navigation test and scoped diff checks pass. No Release
build was repeated for this CSS/test-only follow-up; retain the preceding passing
shared-navigation Release build and stop for user visual acceptance.

The user rejected the hover color direction. One fresh Luna High remediator must
reverse only that state mapping across primary and secondary navigation in light
and dark: inactive and selected labels share the same full-strength resting
color; hovering an inactive label uses the faded color. Selected labels remain
full-strength and underlined. Preserve focus visibility, geometry, spacing,
mobile menu rows, links, routes, and all page content.

That reversed color-state mapping is complete in shared CSS and its focused
assertions. The focused bundled-Node navigation test and scoped diff checks pass;
no Release build was repeated for this CSS/test-only correction. Stop for user
visual acceptance.

The user accepted the corrected shared navigation by moving to the next-page
gate. Resume the declared Pass 2 order with `/Account/ChangePassword` under
PUB-REF-08. Do not begin it until the user authorizes that page task; Forgot and
Reset Password follow as siblings in the same narrow-form family.

The user authorized the Change Password manual correction and identified its
only current visual blocker. One fresh Luna High remediator changes only its
dark-mode presentation: `Account security`, `Current password`, and `New password`
use cream rather than violet, and resting inputs use the approved neutral dark
border. Preserve repeated named heading/label instances, focus/error states,
light mode, bindings, handlers, password policy, safe return, shared navigation,
and every sibling page. Stop for user acceptance; do not begin Forgot/Reset.

That Change Password dark-mode correction is complete in one page marker and
page-isolated dark selectors. Scoped selector/isolation and diff checks pass;
the Release build was not repeated because of the documented MSBuild sandbox
limitation. Stop for user light/dark acceptance.

**Manual rejection and replacement baseline — 2026-08-22.** The user rejected
the complete rendered Pass 1B result after the checkpoint above. Passing tests
and the prior review remain behavior evidence only. The rejected pages retained
too much of the legacy DOM, width constraints, containers, information flow,
form geometry, and generic surface/action composition, then applied the new
identity through page selectors. This is the same prohibited hybrid failure as
the original rejected landing attempt; it is not eligible for narrow CSS
remediation or incremental preservation.

The next work is one fresh Pass 1B presentation implementer, not the previous
implementer or a small remediator. Substantial structural replacement of the
scoped Razor pages is required. Preserve only functional bindings, form names,
validation, routes, handlers, authorization, ReturnUrl safety, WOM and dialog
hooks, localization, and progressive-enhancement semantics. Replace the
page-specific legacy/generic containers and rejected Pass 1B composition with
the structures owned by PUB-REF-05, PUB-REF-06, and PUB-REF-09. Signup must
visibly implement the large event masthead, capacity/status composition,
numbered `01`/`02`/`03` ruled sections, open form layout, summary rail, and
bottom action row. Confirmation must implement the reference-owned outcome
hierarchy for confirmed, waiting, withdrawn/rejoin, and read-only states.
Login, Onboarding, AccessDenied, Error, and StatusCode must each be editorial
route compositions rather than restyled generic panels. Static explanatory copy
may be shortened, reordered, or replaced when meaning and localization remain
correct.

The shared public header rendered by `_Layout.cshtml` and its existing approved
CSS are frozen. Reference headers provide surrounding context only. The rewrite
must render beneath that header and must not duplicate, replace, restyle, or add
a page-local header/navigation system. Preserve its logo, navigation,
account/notification area, responsive behavior, localization, focus behavior,
and login/navigation protocol exactly. Remove only rejected page-specific
selectors after their markup no longer uses them; do not alter shared header or
accepted landing selectors.

Acceptance requires fresh actual-route screenshots at the canonical desktop
viewport and narrow/mobile width for Signup create/edit/unavailable,
Confirmation confirmed/waiting/read-only, standalone Login, Onboarding,
AccessDenied, 403, 404, and 500/request-ID where the environment can render it.
Compare those directly with the applicable approved reference and explicitly
show that the one existing shared header remains unchanged. Source checks,
builds, and focused behavior tests cannot substitute for visual fidelity. Stop
after fresh independent review and any bounded correction for the user's manual
approval; do not begin Pass 2.

**Second rejected rewrite and atomic recovery — 2026-08-22.** The fresh Pass 1B
rewrite above also failed manual visual review. Evidence must not be inverted:
`/Users/christopher/Library/Mobile Documents/com~apple~CloudDocs/Downloads/TEST 16 — Signup lookup - OSRS Community Bingo.pdf`
is the current rejected implementation; PUB-REF-05
(`/Users/christopher/.codex/generated_images/01a028f7-6a50-7c81-bee7-f0a26392f948/exec-12716d59-b32e-4027-9b5c-7947c885c2db.png`)
is the approved Signup target. The rejected render remains a tall, narrow legacy
form flow with an oversized wrapped title, stacked generic fields, and a
detached/redundant summary. The target is a wide horizontal editorial worksheet:
a compact event masthead with title, capacity and lifecycle status sharing the
top band; three compact ruled form rows with number/section label, controls, and
one persistent right summary rail; then a deliberate bottom action row. Dynamic
TEST 16 values replace mock values, but its silhouette, column relationships,
density, hierarchy, and responsive recomposition must come from the target, not
from the rejected DOM.

Stop broad Pass 1B implementation. Recover atomically: rebuild only Signup and
standalone Login first, render their actual routes at 1586×992 plus narrow/mobile
and dark, and return them for user inspection before Confirmation, Onboarding,
or any status page continues. The Signup page-specific Razor body must be
replaced from a clean target-owned skeleton; do not preserve or restyle the
rejected `signup-page`/hero/form/summary structure merely because it contains
working bindings. Reattach the existing bindings and hooks to the new skeleton.

The accepted Landing header is the one global public header implementation
owned by `_Layout.cshtml`; do not duplicate it or add page-local navigation.
Remove the layout branch that emits `public-live-header-*` markup/classes for
non-Landing public pages and use the accepted `landing-shell-*` composition on
every public route. Header Sign in navigates normally to standalone
`/Account/Login` without dialog-open attributes. Preserve dynamic navigation,
the DK mark, mobile menu, notifications, authenticated account/settings
behavior, popovers, localization, focus, event/account context navigation, and
explicit event/signup-entry dialog launch points. Admin keeps its separate
layout and is out of scope. Standalone Login must itself be rebuilt from the
Login panel in PUB-REF-09, not from the rejected login body or dialog
composition.

**Atomic screenshot-review correction — 2026-08-22.** User-supplied actual-route
screenshots at 1586×992 and 390×844, compared with PUB-REF-05 and the Login panel
of PUB-REF-09, block both atomic pages. Signup must be compacted into the target's
horizontal worksheet so its action row is visible at the desktop reference
viewport, and its masthead must show real confirmed/capacity and waiting values.
The complexity budget therefore permits one read-only `SignupModel` projection
addition for those existing facts; it does not authorize persistence, workflow,
capacity-rule, or handler changes. Login must own the available desktop width,
use a broad form column with an unbroken desktop headline, and carry its
diagonal DK artwork field through the right side rather than leaving a narrow
central island. Static Signup/Login copy is composition-flexible: it may be
shortened, reordered, or replaced in natural English and Danish while dynamic
event/account/capacity/date data, action meaning, destinations, validation, and
authentication/signup behavior remain authoritative. The next worker is one
fresh bounded remediator; it performs source/build checks without another live
browser-forensics loop, then stops for user replacement screenshots.

**Rejected implementation record — 2026-08-22:** the first Pass 1 result is a
failed visual approach, not a partially accepted baseline. It retained generic
classes such as `public-ui-component-header`, `public-ui-action-list`,
`public-ui-data-group`, `public-ui-page-masthead`, `public-ui-surface`,
`public-ui-sectioned-surface`, and `public-ui-event-directory-row`, then layered
`body.public-ui-pass1` overrides above the legacy composition. The user rejected
the complete result. Do not dispatch the reviewer's narrow signup remediation,
reuse the failed implementer/reviewer chats, or treat passing source/tests as
visual acceptance.

Each public pass uses one bounded Luna Max implementation, one fresh Sol High
independent review, focused remediation/verification only when a named
finding requires it, and user manual visual acceptance before the next pass.
After acceptance, remove only that family's superseded public residue from the
transitional files; preserve all unrelated and Admin rules.

The approved Admin baseline remains the shell, Event Create, Identity, Schedule,
Manage/Overview, Events directory, Participants with accepted detail-dialog
states, Catalogue, Accounts/Roles, Board, and Teams/Draft. No Admin page is
implicitly UI-approved by the launch decision; this UI-approval boundary does
not change the recorded production launch.

## 3. Production readiness and release gates

Production is live from PR #5 at source SHA
`1f893133edc26455c41535807633225fdee36292` with immutable image digest
`sha256:5de9882be6cd63e170b6d68fc1b869ea134e9b67f3bfab6a1b7eb042ed4c1a20`.
CI, deployment, and focused production smoke passed. The remaining operational
stage is the production Admin test event; it has not run. Launch-critical
release work takes precedence over deferred UI polish.

Keep the complete infrastructure and operational checklist through release,
including optional but prudent safety items. Evaluate each item when its
deployment step approaches and present provider/tier options, current costs,
tradeoffs, a hobby-project recommendation, and the consequence of deferring or
omitting it. Optional does not mean silently removed. Do not create an external
account, purchase a service, enable a paid tier, accept a credential, change DNS,
or mutate production without the user's explicit approval. Keep repository-side
automation provider-portable where practical until a choice is required.

The release checklist below is retained as the operational contract; its launch
steps are now recorded as completed evidence, with the Admin test event the
next stage:

- Whole-application desktop/mobile, keyboard, permission, error, accessibility,
  and functional regression was accepted at baseline `c3e43bb` and carried into
  the deployed PR #5 candidate.
- Production image/Compose, CI, controlled deployment, restricted storage,
  health reporting, backup/restore, monitoring, and operator runbook work was
  completed and exercised as recorded below.
- The production historical import and its public landing/Board/Teams smoke
  checks succeeded on 2026-09-01; the rehearsal event remains Hidden and
  separate.
- The production Admin test event is the next operational stage and has not
  run.

**Production Release Pass 1 — provider-neutral topology, complete 2026-08-27.**
The single-VPS Compose contract now defines Caddy, one ASP.NET Core web replica,
and private PostgreSQL networking; persistent PostgreSQL, data-protection,
catalogue-cache, and Caddy state/config volumes; immutable image input; the
R2/Discord/Wise Old Man/bootstrap configuration names; and clean-start/operator
assumptions. CI/image publication, application operations and health components,
deployment automation, provider setup, backup/restore, rehearsal, and release
were completed in the later passes recorded below.

**Production Release Pass 2 — application operations, complete and
independently cleared 2026-08-27.** Persist and startup-validate the configured Production
data-protection key ring; retain the private-Caddy forwarded-header model; use
the built-in Production JSON console logger; keep public `/health/live` cheap;
make container-internal `/health/ready` gate PostgreSQL, configured R2 bucket
reachability, and timely heartbeats from both existing hosted workers; keep Wise
Old Man non-blocking; add explicit migration and read-only production-preflight
commands without normal-startup migration; and initialize non-root ownership of
the writable data-protection and catalogue-cache volumes. Clean setup runs
migrate, catalogue snapshot, owner bootstrap, preflight, then web/Caddy. Retained
data runs the legacy Slice 1 preflight only when crossing that boundary, then
migrate, production preflight, and replacement; never apply the catalogue
snapshot to retained data.

The Pass 2 complexity budget is zero tables, schema migrations, product routes,
policies, jobs, NuGet dependencies, CI/deployment/provider work, or generalized
frameworks. Extend existing startup, health, worker, storage, command, and
Compose code; add only the smallest validator, heartbeat state, R2 availability
probe, and volume-permission/health-probe wiring demonstrated necessary by the
readiness review. Preserve Development and all product/UI/domain behavior. CI,
deployment automation, provider setup, backup/restore/rollback runbooks,
rehearsal, final release, asset work, and monitoring-vendor selection were
completed in the later passes recorded below.

Release Web and IntegrationTests builds, Production Compose rendering, scoped
diff/secret checks, and the built-in .NET readiness-probe command pass. Focused
test execution and immutable-image execution remain unverified because the host
denies the test runner listener and local Docker API respectively; neither is a
known failure. No production or provider resource was changed.

**Production Release Pass 3 — release-candidate publication and non-mutating
promotion, implementation-ready 2026-08-27.** Preserve the existing PR/main CI
job and check name. After that job succeeds on a `main` push, publish one
`linux/amd64` image from the current Dockerfile to a fixed GHCR package with a
trace-only full-commit tag, while making the immutable digest authoritative.
Record image name, digest, source SHA, platform, workflow run identity/URL, and
timestamp in a small candidate artifact. Use job-scoped least privilege, no PAT
or PR secrets, and full-commit pins for trusted actions.

Add one manual `production-promotion.yml` workflow that runs only from `main`,
accepts the source SHA, digest, and CI run ID, validates their syntax, proves the
successful main run's candidate artifact binds the exact values, and emits a
promotion receipt. Its explicit manual `workflow_dispatch` with `mode: deploy`
is the user's production approval; no GitHub Environment, required reviewer, or
environment secret is used. It performs no SSH, image pull, migration, Compose
operation, or other production mutation in `promote` mode.

Actual VPS deployment belongs to Pass 4 after a pre-migration backup, tested
restore, and rollback contract exist. Pass 3's complexity budget is one extended
CI workflow, one new promotion workflow, and the two small JSON receipts. Add no
deployment scripts, third-party deploy action, application/schema change,
provider account, secret, SSH code, SBOM/signing/provenance framework, build
cache, multi-architecture image, or production resource. GHCR remains private
by default, the repository remains private on GitHub Free, and no Environment
reviewer or environment secret is required.

**Production Release Pass 4 — repository-side deployment and recovery contract,
committed through `329e04adaa98a444f69d20aa41385b4ca7426bd3` on 2026-08-28.**
The existing promotion workflow has explicit `promote` and `deploy` modes.
Candidate validation runs before either action; the explicit manual
`workflow_dispatch` selecting `mode: deploy` is the user's production approval.
The workflow uses non-cancelling `concurrency: production` and transports only
SSH data plus validated release metadata. Native OpenSSH invokes the narrowly
sudoable root-owned host command.

Add only the minimal host deploy, encrypted restic backup, isolated restore
verification, and database-stored evidence-integrity scripts; root-only host
configuration examples; one systemd backup service/timer; and the deployment
runbook. Deploys back up before every replacement, use the exact image digest,
preserve Caddy/PostgreSQL, run the existing clean/retained migration and
preflight commands, verify internal and public health, and emit a secret-free
receipt. Changed migration history never permits automatic image rollback.
No application behavior, schema, provider, account, DNS, secret, or production
resource was changed. The exact operator procedure is in
`docs/PRODUCTION_RUNBOOK.md`.

Pass 4 complexity budget is one modified promotion workflow, minimal host
scripts/config examples, one systemd backup service/timer, and concise
deployment/recovery/evidence documentation. Focused checks are limited to
script syntax, input rejection, workflow/action pin and permission inspection,
Compose rendering, receipt/secret-leak checks, documentation links, and a
disposable restore exercise only if Docker is available.

The user-approved Sol High integrated review identified six Pass 4 cross-pass
blockers: self-contained host-loss recovery, write quiescence/migration safety,
single database authority, physical Compose volume identity, clean-vs-retained
bootstrap state, and clean-host Caddy startup. That bounded correction is now
followed by a four-finding release-blocker remediation: exact PostgreSQL
database restore and migration-history verification, `none`/`none` baseline
recovery, post-backup failure classification, and `new` marker/history
contradiction rejection. The separate bootstrap-password correction is also
complete: the password is bootstrap-only and root-file supplied, absent from
the long-running web container and durable backup/config payloads, retained on
failed initialization, and removed after successful initialization or safe
resume. Passes 2–3 otherwise cleared, and no P0, secret, or unapproved-scope
issue was found. The correction is committed and pushed through `aa1af77`. Later
application corrections are committed locally on the release branch: `182b84f`
fixed production routing/live controls, `cd83c36` polished submission drawer
controls, `88cd8f8` consolidated the canonical submission workspace, and
  `e75ec57` polished notification-popup and Board-family progress notices. The
accepted final regression/package work is included in the deployed PR #5
candidate recorded below.

**Production launch evidence — complete 2026-09-01.** PR #5 was merged and
deployed at source SHA
`1f893133edc26455c41535807633225fdee36292` and immutable image digest
`sha256:5de9882be6cd63e170b6d68fc1b869ea134e9b67f3bfab6a1b7eb042ed4c1a20`.
CI, deployment, and focused production smoke passed. The production rehearsal
event remains Hidden, not deleted, and is reachable only to SuperAdmin through
`/Admin/Events?filter=hidden`.

Better Stack production monitoring is active: public `/health/live`, quarter-
hour disk heartbeat, and nightly backup heartbeat, with both timers active.
Disk success/failure/recovery was verified. A scheduled encrypted backup
succeeded with snapshot
`715e2ee745e1fb51f10c510b6b2995aefb5109ea9703dd7f349e8fa37aac70d9`; retention
was applied. Heartbeat URLs and credentials remain outside Git.

The production import of **Det Store Danske Sommerbingo 2026** succeeded with
slug `det-store-danske-sommerbingo-2026`. Preflight found 6 teams, 90
participants, 93 WOM accounts, 25 tiles, and 150 counters. The reviewed
combined hash is
`c8ef4ec0a01ef1779ec3ea358100b91968c942d6f875ec15a7504014b2684df8`.
Production landing, Board, and Teams returned 200, and the user manually
accepted the imported event. Private host/staging input copies were removed;
ignored local operator input remains outside Git.

Known non-blocking Admin defect, explicitly deferred by the user: selecting
Hidden (and potentially other server-filtered states) in the Events dropdown
performs client-only filtering/history replacement, so rows absent from the
normal DOM do not appear. Directly loading `?filter=hidden` works; there is no
data loss.

- **Capacity sub-gate complete (2026-08-30):** the production rehearsal held
  100/100 concurrent SignalR viewers and completed 400/400 public Board, team,
  tile, and evidence requests with zero failures. Full-run HTTP latency was p50
  702.4 ms, p95 2516.4 ms, p99 3066.3 ms, and max 3749.8 ms; SignalR connection
  latency was p50 250.5 ms, p95 344.1 ms, p99 377.5 ms, and max 410 ms. A short
  repeat produced HTTP p50 266.0 ms, p95 1027.9 ms, p99 1278.3 ms, and max
  1451.1 ms. Brief 2-vCPU saturation during synchronized arrival caused no
  request failures, swap pressure, or persistent health issue. This clears the
  documented 100-connected-viewer launch target for the 2-vCPU/4-GB tier. Do
  not repeat this capacity run unless infrastructure or performance-sensitive
  behavior changes materially. This evidence covers anonymous public reads and SignalR
  subscriptions; it does not replace the authenticated application-journey
  evidence required elsewhere in this gate.

The current production/release order is frozen:

1. Run the production Admin test event. It has not run.

The R2 deletion/versioning or accepted-recovery decision remains an explicit
known operational risk; existing integrity evidence does not silently solve it.
The deferred Events dropdown defect is non-blocking and does not cause data loss.

### Approved historical-event import — complete in production 2026-09-01

The product/data slice is the one-time operator-controlled import of **Det Store
Danske Sommerbingo 2026**. Its approved behavior, frozen source inputs,
deterministic allocation, privacy boundary, and exact historical disclosure are
owned by `PRODUCT_REQUIREMENTS.md`,
`FUNCTIONAL_CONTRACTS.md`, and `DATA_MODEL.md`. The event is imported directly
as `Archived` for `Europe/Copenhagen`, from `2026-07-14 18:00 CEST` through
`2026-07-19 18:00 CEST`, with `ArchivedAt` equal to the end; no temporary Live
state or ongoing synchronization is allowed.

The implementation is one narrow service plus explicit CLI preflight and apply
control, with zero new tables, pages, routes, policies, jobs, dependencies, or
generalized frameworks. Versioned metadata may include the public board
definition, exact corrected 402 counter units across 150 team/tile cells,
source identifiers, and hashes, but never the private 90-participant/93-account
mapping. The reviewed public manifest SHA-256 is
`e5297b20fc5e4a842b6a1e5ab378128cbe1c2bad16033fc875c54607c0d49438`.

Preflight is mandatory, apply is audited and transactional, and an already-
applied exact import hash is the only no-op case; a divergent hash fails closed
and any apply failure rolls back without partial state. Apply requires explicit CLI
invocation, exact event-name confirmation, and an active SuperAdmin actor
validated inside the locked serializable transaction; it is a separate user
authorization from implementation or release packaging. Failed preflight and
divergence identify the affected input or retained record and the operator
correction required before retrying.

The initial implementation review identified six concrete blockers. The final
independent Sol High closure review identified three additional blockers:
Production CLI reachability, the reviewed public manifest pin, and active-
SuperAdmin authorization inside the locked serializable apply transaction.
All nine were remediated before local commit
`c4130f437b82cf5ceb5f130cf8a77a3a05ae2079` (`Add historical 2026 event import`).
Focused verification passed: `HistoricalBoardReferenceTests` 25/25;
`Bingo.IntegrationTests` Release compilation with 0 warnings/errors;
`HistoricalImportIntegrationTests.AppliesFictionalOperatorInputAndExactRerunIsNoOp`
1/1 using isolated Docker/Testcontainers; and
`Slice10Pass103ActivityProjectionTests.DevelopmentTest15DueControlIsIdempotentAndResetRemainsCachedOnly`
1/1 using isolated Docker/Testcontainers. The user applied the corrected import
only to the local Development database, manually inspected it, and reported
that everything looks good.

The local Development inspection preserved the exact approved disclosure,
itemless reconstructed approved rows in Recent Drops without fabricated drops
or evidence, deterministic within-team EHB-weighted attribution/timing, the
complete source WoM snapshot without normal refresh, and the approved Maggot
King and Superior Slayer rules. Production preflight then found 6 teams, 90
participants, 93 WOM accounts, 25 tiles, and 150 counters; the reviewed
combined hash was
`c8ef4ec0a01ef1779ec3ea358100b91968c942d6f875ec15a7504014b2684df8`.
The imported production event was manually accepted after landing, Board, and
Teams returned 200. Private host/staging input copies were removed; ignored
local operator input remains outside Git.

## 4. Dependencies, approvals, and stop rules

- Use `UI_SYSTEM.md` for global UI rules and `UI_PAGE_MATRIX.md` for page
  family/reference/exception/approval decisions. Do not recover authority from
  archived documents or root tombstones.
- The public Board/evidence family's behavior remains protected except for the
  approved regular-page navigation correction. Its old appearance is superseded;
  structurally rewrite it only in Pass 4 from the reactivated references while
  preserving the current near-target team-overview grid, routes, browser history, team/tile relationships, focus,
  tile-sidebar behavior, shared-drawer submission-result acknowledgement,
  evidence viewing, and realtime non-interruption. Preserving the rejected
  legacy masthead or team-workspace composition and changing only its skin is
  an explicit failure. The overview grid itself needs only corrected masthead
  integration and the missing Recent Activity footer.
- The rebuild complexity budget is zero new tables/migrations, services,
  policies, jobs, product routes, generalized frameworks, stylesheet files, or
  navigation systems. Reuse `_Layout.cshtml`, `site.public-ui.css`, the current
  transitional owners, existing page markup/partials and JavaScript modules,
  and the localization pipeline. A licensed local font or supplied artwork is
  allowed only when actually needed by the accepted identity; add no theme
  persistence service.
- No font metadata was recovered from the AI-generated landing reference, so no
  candidate may be called the original/reference font. Pass 1A may compare the
  current Barlow Condensed, Bebas Neue, and at most one genuinely closer
  license-safe condensed display face using the exact headline, event-title,
  and numeral specimen at a common crop/scale. Choose by silhouette, cap height,
  width, stroke density, punctuation, numeral shapes, and legibility. If no
  candidate is clearly closer, stop with a compact A/B/C specimen for user
  choice. A selected face is an implementation approximation for hero, section
  headings, event names, feature numbers, and event-date numerals; Barlow
  Condensed SemiBold remains utility/navigation/status and the bundled Geist
  variable face remains body/control only while it continues to match. Do not
  synthesize proportions with `scaleX` or another transform. Bundle only the
  selected license-safe face and its license, and verify browser-resolved faces.
  The current bounded comparison selected Bebas Neue Regular over the locally
  available Barlow candidate as the implementation approximation; no third local
  candidate was available. This provenance record does not identify the unknown
  reference font and does not substitute for user visual approval.
- User-authorized typography-only A/B (2026-08-22): compare the current Bebas
  Neue 400 landing display roles with a genuine locally bundled Barlow Condensed
  800 face, untransformed and at the same composition/viewport. Preserve Geist
  body copy, utility roles, colors, copy, spacing, layout, responsive geometry,
  and behavior. Bundle only the OFL-licensed static face and license inventory
  needed for the experiment; do not synthesize 800 from the current 600 file.
  The result remains subject to user visual choice and does not reopen other
  landing findings or another page family.
- User manual typography/ledger/icon correction (2026-08-22): retain Barlow
  Condensed ExtraBold 800, especially for major numerals and headings, while
  reducing current-event name size to match the target. Current-event vertical
  date dividers become more vertically inset; neutral row hairlines extend to
  the target junction near/slightly before that divider and are absent after the
  final current row. Previous events use the target's compact separate archive
  composition—name/state left, history action right—rather than the full
  date/status/details grid. Enlarge and normalize all three feature SVGs to the
  target's optical size/mass, with a full sheet plus distinct sage approval
  badge and a fuller four-bar bronze chart. Preserve dynamic facts, routes,
  semantics, themes, responsive behavior, and all other landing composition.
  Reduce the Current events and Previous events Barlow 800 heading size slightly
  without changing their strong-rule relationship.
- Render-effort rule (2026-08-22): perform detailed implementer visual
  inspection for a page family's first coherent implementation, an explicit
  user request, or a concrete uncertainty whose result can materially change
  the fix. Do not repeat heavy self-inspection for every small remediation.
  Surgical follow-ups receive focused implementation and technical checks, with
  a render only when immediately inexpensive, then return to the user for visual
  acceptance rather than iterative implementer tuning.
- Final SVG-only correction (2026-08-22): use the newest target/current feature
  crops recorded in `CURRENT_STATUS.md` to reproduce the actual target paths
  for the broadcast, sheet/check badge, and four-bar chart. Preserve the accepted
  rendered icon size and strip layout. Keep full stroke extents inside the 32×32
  viewBox so no cap, join, badge, or baseline clips. Run focused technical checks
  and return directly for user inspection without another heavy render loop.
- The approved landing screenshot is a visual contract. Review must compare
  silhouette/major regions, typeface/weight/scale, header geometry, hero
  diagonal/logo, feature strip, event-ledger density/alignment, links/actions,
  colors, whitespace, and responsive recomposition. Tokens, selectors, tests,
  or source inspection alone cannot approve the pass. Review static editorial
  copy for truthful product meaning, correct action/destination semantics,
  natural English/Danish parity, and composition-appropriate rhythm; exact
  preservation of the rejected implementation's wording is not required.
- Manual visual remediation record (2026-08-22): the current evidence at
  `/Users/christopher/Library/Mobile Documents/com~apple~CloudDocs/Downloads/Here.pdf`
  is much closer but unapproved. The next bounded landing correction must use
  the bounded display-candidate comparison above; unify the optical weight of all blueberry
  numerals; use inset feature dividers and date-gutter-aligned event-row rules;
  remove the duplicate rule below the Current events heading; correct the
  blueberry header, DK mark, coral/sage/bronze state application, and inactive
  Captain navigation color; normalize the three feature SVGs as one restrained
  stroke family; keep the hero ending near y=443 while restoring target headline
  height/vertical rhythm and feature-strip breathing room; and rebalance the
  ledger so details begin near the target position without crude long-name
  truncation. Preserve real event counts/data. This is remediation within the
  approved design, not a new page or direction.
- Dark mode is a P1 manual blocker in the same landing remediation. The evidence
  at `/var/folders/w5/74mg_d917xg33ry8_4qc9g5w0000gn/T/codex-clipboard-a94f92be-bc92-4b1b-a8b3-65120651581b.png`
  shows content accents inheriting the near-black shell token. Split landing
  tokens into shell background, content accent, primary text, muted text, rule,
  art field, and semantic states. In dark mode keep `#111216` shell,
  `#1B1A1D` canvas, `#242326` field, `#F4EEDF` primary, `#B8B1A8` muted,
  `#4B494B` rules, and an accessibility-safe smoky-indigo content accent instead
  of shell black. Preserve distinct coral/sage/bronze states. Approval requires
  a fresh 1586×992 dark render, computed-color/contrast audit for visible text
  and controls including interaction states, and exact light/dark geometry
  parity; source token declarations alone cannot clear this blocker.
- Latest surgical landing remediation (2026-08-22): evidence at
  `/Users/christopher/Library/Mobile Documents/com~apple~CloudDocs/Downloads/ThisThisThis.pdf`
  remains unapproved only for feature-icon, divider, and date-gutter geometry.
  Feature 01 is the 32×32 optical authority; redraw feature 02 as a clean sheet
  with restrained lines and a non-colliding sage badge, and feature 03 as a
  compact bronze chart/podium with comparable mass. Use exactly three rule
  roles: strong blueberry section rule, neutral 1px inset vertical divider, and
  neutral 1px event hairline. Narrow and optically center the date stack, tighten
  day/month rhythm, and derive the hairline start from the date-divider geometry
  rather than a separate magic offset. Preserve the accepted composition,
  typography direction, palette, content, behavior, responsive/dark geometry,
  and all other families.
  Give each event row one simple semantic tone class so its action label and
  arrow match its status/dot: coral live, sage signup, bronze postponed/upcoming,
  and subdued bronze archived. Do not use generic all-blue or nth-child styling;
  verify accessible hover/focus contrast in both themes.
- Major functional slices still require the formal readiness review in
  `AGENTS.md`. Ordinary UI passes use the lean sequence: agree page/result,
  bounded implementation, one independent review when appropriate, focused
  remediation, and manual acceptance.
- F-04 is resolved by retaining Live identity and display timezone as read-only.
  The separate Live event-end correction remains a Schedule capability. F-06 is
  resolved by the approved five-step `/HowTo` guide in `50077fd`; no Rules
  editor, sixth step, or in-application HowTo editor is in scope.
- Stop before adding product behavior, changing an approved rule, adding
  unbudgeted persistence/routes/policies/jobs/abstractions, or fixing an
  unrelated defect. Preserve existing routes for deep links, reload/history,
  authorization, audit, concurrency, privacy, and historical records; no
  separate no-JavaScript parity work is planned or gated.
- Stage, commit, push, deploy, or archive additional legacy documents only
  after the relevant acceptance and explicit authorization.

## 5. Admin event-functionality correction — approved pass plan (2026-09-03)

### 5.1 Scope, baseline, and stop rule

This is one event-scoped functional slice, implemented on
`admin-event-functionality`. It corrects what an Admin can do to one event and
its retained records across the lifecycle. It is not an Admin UI redesign;
markup or interaction changes are permitted only when required to expose an
approved action, confirmation, reason, validation result, or recovery path.

The complete base-to-current implementation is reviewed against this section
and the authoritative contracts. Preserve authorization, audit history,
optimistic concurrency, privacy, transactional mutation, immutable competitive
history, and existing route-backed recovery. Do not change global Admin
behavior, public-page composition, artwork, catalogue/tile behavior, account
management outside an event, event-state vocabulary, or post-draft signup
answer/account editing. Version-one question-visibility toggles remain a
superseded capability and are not implemented, repaired, or removed in this
slice. Admin-created participants remain blocked after draft start.

An implementer may resolve ordinary technical details inside a pass, but must
stop before changing an approved product rule, adding an adjacent fix, or
introducing unbudgeted persistence, routes, policies, jobs, services, or
abstractions. Each pass must remain independently testable and leave the branch
in a coherent state.

### 5.2 Hard schedule requirement

The following matrix is an implementation and final-review requirement, not an
illustrative summary. `SignupClose` reopening follows the same retained-future
boundary rule as resuming an event: reuse an existing future value; require and
confirm a replacement only when the retained value is missing or expired.

| Event lifecycle | Signup opening | Signup closing | Draft time | Event start | Event end | Capacity |
| --- | --- | --- | --- | --- | --- | --- |
| Private Draft | Editable while future | Editable while future | Editable while future | Editable while future | Editable while future | Editable |
| Signup Open | Locked history | Editable while future | Editable while future | Editable while future | Editable while future | Editable |
| Signup Closed, draft not started | Locked history | Reopen action only | Editable while future | Editable while future | Editable while future | Editable |
| Draft Running or Paused | Locked | Locked | Locked | Locked | Locked | Locked |
| Draft Finalized, pre-Live | Locked | Locked | Locked | Editable while future | Editable while future | Locked |
| Live | Locked | Locked | Locked | Locked history | Editable to a future value with confirmation and reason | Locked |
| Awaiting Final Review | Locked | Locked | Locked | Locked history | Resume reuses a retained future end; otherwise requires a confirmed replacement future end | Locked |
| Finalized, Archived, or Cancelled | Locked | Locked | Locked | Locked | Locked | Locked |

Every changed timestamp must be future; passed boundaries cannot be changed or
cleared, while an unchanged historical value remains valid. Signup close must
remain between opening and start, end must follow start, and published start/end
cannot be cleared. Overlap and linked Wise Old Man schedule matching are
revalidated. Changing event end atomically derives the normal submission cutoff
as end plus 30 minutes; the cutoff is never directly edited. Reopening
submissions remains the separate Final Review action.

### 5.3 Pass 1 — schedule and postponed lifecycle recovery

**Outcome:** Schedule editing and start/resume recovery exactly match section
5.2 and `FUNCTIONAL_CONTRACTS.md` section 4.4.

- Enforce the approved Live identity boundary end to end: identity and display
  timezone are read-only in the domain, rendered Admin surface, and direct POST
  handling. Remove the obsolete post-start timezone-reason path and update only
  its directly affected tests and resources. This is not an Identity redesign.
- Replace the broad post-draft schedule lock with lifecycle- and draft-state
  permissions from the matrix. Running and Paused retain the complete lock;
  Draft Finalized pre-Live permits only still-future event start/end changes.
- Preserve the current signup-reopen behavior that reuses a retained future
  close and requires confirmation of a replacement only when it has expired or
  is absent.
- Permit a Live event-end change only to another future time, with server-side
  confirmation and a required reason. Revalidate all schedule invariants and
  atomically derive the ordinary submission cutoff.
- Make Resume reuse a retained future end. Require a replacement future end
  only when the retained value is missing or expired; always require strong
  confirmation and a reason.
- Recover a postponed scheduled start using actual lifecycle facts rather than
  `now < EventStartsAt`: while the event has never actually started and its end
  remains future, allow required roster correction, draft
  finalization/reopening/refinalization, initial board publication, and manual
  start. Preserve the missed scheduled start as history. Reject start when the
  configured end has passed and direct the Admin to cancel or replace the event.
- Make capacity mutation and its audit record one transaction and one audit
  entry; do not retain a service commit followed by a separate fallible audit.
  Every resulting promotion notification must include the promoted participant
  ID so its recipient can follow the durable Confirmation destination without
  transient `TempData`.
- Update only the bounded Development reset fixture needed for acceptance:
  TEST 05 must be genuinely pre-Live and never started, with a configured start
  in the past, an end in the future, and unresolved draft/board blockers. The
  Admin must clear those blockers through existing rendered routes before
  manually starting the event.

Focused proof must discriminate the matrix boundaries, delayed-start recovery
through the real Admin handlers, retained-future and expired-boundary reopen and
resume cases, Live end/cutoff behavior, rejection after end, overlap/Wise Old
Man revalidation, Live identity/timezone rejection, capacity rollback/audit
uniqueness, and a promotion notification followed as its recipient. A direct database
fixture that silently creates a ready draft or published board does not prove
the postponed-start recovery path.

### 5.4 Pass 2 — participant metadata and ownership

**Outcome:** Administrative corrections remain available for as long as their
business purpose remains valid without reopening unrelated participant edits.

- Keep private payment status and Admin notes editable in every retained visible
  lifecycle, including Draft Running/Paused, Live, Awaiting Final Review,
  Finalized, Archived, and Cancelled. Hidden and Discarded records remain
  inaccessible.
- Narrowly allow the terminal participant-detail route and only the handlers
  needed for payment/notes. Continue hiding and server-blocking answer, account,
  status, roster, and other lifecycle-inappropriate controls.
- Allow participant ownership transfer through Awaiting Final Review with
  strong UI and server confirmation, concurrency protection, atomic history,
  access revocation, and reachable notifications. The new owner retains the
  participant Confirmation destination; the former owner receives the existing
  account-owned My Events destination because participant access has already
  been revoked. Reject transfer in Finalized, Archived, Cancelled, Hidden, and
  Discarded.
- Preserve all existing correction, withdrawal, restoration, internal-add, and
  account/answer freeze rules not explicitly changed above.

Focused proof covers each newly reachable lifecycle, direct-POST rejection for
unapproved controls/states, concurrency, atomic audit, preserved record history,
immediate authority transfer, and both notifications followed as their intended
recipients. No new participant table, service, page,
route, or generalized terminal-event framework is budgeted.

### 5.5 Pass 3 — operational integrations, evidence authority, and atomic audit

**Outcome:** Event-scoped operational settings remain editable only while their
effects can still be used, and every accepted mutation is atomic with history.

- Preserve and regression-check the existing reachable Live Wise Old Man
  competition replacement rather than reimplementing it. It must reference an
  existing competition whose boundaries match within five minutes, cannot clear
  the link, cannot synchronize the schedule, invalidates the prior displayed
  cache only after success, and leaves state unchanged on failure. Change it
  only if focused proof exposes a concrete contract gap.
- Permit evidence-code configuration in Private Draft, Signup Open, Signup
  Closed, and Live. In Awaiting Final Review, permit it only while the active
  submission window accepts uploads. Freeze it after upload closure and in
  terminal, cancelled, hidden, and discarded states; retain interval snapshots.
- Permit captain/co-captain assignment, promotion, demotion, and revocation
  after draft and through Live. In Awaiting Final Review, permit changes only
  while the active submission window accepts uploads. Freeze them afterward,
  while preserving history, notifications, and withdrawn-member behavior.
- Make reopen-submissions, evidence-code mode/value changes, and evidence-code
  creation atomic with their audit entries. An audit failure must roll back the
  business mutation, and one successful action must produce one history record.

Focused proof covers each positive and negative lifecycle/window boundary, Wise
Old Man failure-without-mutation, interval preservation, captain authorization,
and mutation/audit rollback. Reuse the existing event policy and services; no
new table, page, route, policy, job, service, or abstraction is budgeted.

### 5.6 Pass 4 — board publication and correction safeguards

**Outcome:** Publishing public competitive state is deliberate and cannot be
continued from an obsolete or terminal Admin workspace.

- Require an explicit popup confirmation and an independently validated
  server-side confirmation value for both initial publication and publication
  of a corrected replacement board. A missing or stale confirmation produces
  no mutation.
- Retain the existing confirmation/reason boundary for starting board
  correction.
- Permit corrected publication only in Signup Closed, Live, and Awaiting Final
  Review. Reject Cancelled, Finalized, Archived, Hidden, and Discarded events at
  the handler/service boundary and prevent an already-open correction workspace
  from bypassing the current state.
- Keep the previously published snapshot authoritative until the replacement
  publication succeeds, and preserve every superseded snapshot and audit record.
- Use Pass 1's postponed-start recovery for late initial publication; do not
  duplicate schedule policy here.

Focused proof covers no-confirmation/no-mutation, allowed lifecycle states,
direct POST and stale-workspace rejection, snapshot continuity, and history.
No board-editor redesign, catalogue behavior, tile semantics, artwork, new page,
or new route is in scope.

### 5.7 Execution, review, and release gates

1. The one independent Sol High implementation-readiness review completed on
   2026-09-03. It checked real UI reachability, Development reset states and
   accounts, obsolete behavior inventory, pass ordering, independent
   deployability, and the complexity budget. Its five required corrections are
   incorporated in Passes 1–3 and the active question-visibility wording; no
   unresolved product decision or architecture blocker remains.
2. Do not repeat the readiness review unless implementation exposes a genuine
   contradiction or missing product decision.
3. Implement passes sequentially with bounded Luna Max implementer tasks. Each
   pass stops after its scoped implementation and focused tests; it does not
   begin the next pass, review itself, remediate unrelated defects, package, or
   publish. The planner checks scope and test evidence before requesting the
   user's next-pass authorization.
4. Because Pass 1 materially crossed lifecycle, authorization, transaction,
   notification, Admin-handler, and recovery-test boundaries, the user approved
   one risk-based independent Sol High review of the complete Pass 1 diff before
   Pass 2. The review completed on 2026-09-04 and found two blockers: Wise Old
   Man schedule synchronization could bypass the draft lock, and the promotion
   notification test did not follow its stored destination. Bounded Luna Max
   remediation restored the aggregate guard and added discriminating locked-
   draft synchronization plus authenticated notification-destination coverage.
   The affected Release build, 11 Wise Old Man tests, one promotion-destination
   test, and `git diff --check` pass. A fixes-only Sol High re-review then passed
   with both findings resolved and no remediation-local defect or scope
   expansion. This does not establish an automatic per-pass review requirement.
5. After all four passes, run one independent Sol High base-to-current review
   against the final updated plan. Any remediation uses a fresh bounded Luna
   Max task and changes only named findings.
6. Run one manual-acceptance preflight through the real rendered Admin links and
   forms using authoritative Development reset data. Every required account,
   role, event state, record, control, notification destination, and next step
   must be reachable without constructing hidden destination URLs.
7. Per-pass verification is focused and non-duplicative. Run the complete test
   suite once, after all passes and remediation, before packaging. Staging,
   committing, pushing, opening/updating a PR, merging, and production promotion
   each require their own later user authorization.

**Complexity budget:** zero new database tables or migrations; zero new pages or
routes; zero new authorization policies, background jobs, services, compatibility
layers, or generalized state/control frameworks. Extend existing event entities,
domain/application services, state policy, Admin pages/handlers, dialogs, audit
transactions, and focused tests. Any discovered need to exceed this budget is a
stop condition for user review, not an implementation detail.

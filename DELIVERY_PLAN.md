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
  section 4.1 below. Ordinary UI passes use the lean sequence: agree page/result,
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

The detailed procedures below are relocated from `AGENTS.md`; this is their active
owner. Use the subsection relevant to the assignment, not the entire checklist for
every correction. Sections 4.1 and 4.5 govern major functional-slice planning and
acceptance; sections 4.2–4.4 govern the changed scope and risk boundaries; section 4.6
retains applicable completion gates and small-UI/documentation exceptions. Explicit
user-approved pass gates, verification substitutes and waivers remain in force.
`UI_SYSTEM.md` owns the UI task/review protocol; the Admin popup source-only reviewer
exception in `AGENTS.md` remains applicable. Model/role defaults stay in `AGENTS.md`.


### 4.1 Functional-slice planning and readiness

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

### 4.2 Implementation and change control

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

#### 4.2.1 Lean execution and planner handoff

This is the reusable coordination procedure. Read it before dispatching work or
resuming as planner; apply the assigned pass's gates without creating extra stages.

1. Resume from `CURRENT_STATUS.md`, the assigned pass and the smallest relevant
   diff. Confirm the checkout, existing work and next authorized action. Reuse
   recorded checks and findings; changing planners does not restart discovery,
   implementation or review. Resolve only missing, changed or conflicting evidence.
2. Before implementation, turn the user's goal into a short, agreed fix list:
   observed problems, expected results, protected working behavior/composition,
   and the smallest checks that demonstrate the fixes. The planner owns this
   translation; the user need not identify files or technical causes. If problems
   are not yet known, perform one bounded inspection and return the concrete list
   before dispatching implementation. An already explicit user correction needs
   no extra approval round. Name the existing implementation to reuse; do not add
   a preliminary reviewer by default. Report additional discoveries separately
   unless they directly block the agreed fixes.
3. The worker implements a connected change and runs its focused checks. After one
   focused lookup, unresolved consequential uncertainty goes to the calling planner:
   what is unclear, the relevant evidence, and the recommended choice. Pause only
   the dependent work and continue independent assigned work where useful.
4. The planner answers from the approved scope and evidence. Ask the user only for
   a new product/scope decision, required permission, environment blocker or manual
   acceptance. Check promptly when progress stalls or a worker is interrupted;
   repeated reading without a concrete result requires a narrower next step or a
   changed approach, not another open-ended continuation.
5. Run the required independent review once; remediate and recheck named findings
   and direct consequences. For Admin popups, review is source-only and the user
   supplies final visual acceptance. Stop at the assigned acceptance boundary.
6. Before a planner handoff, consolidate the active `CURRENT_STATUS.md` entry with
   the checkout/branch, assigned pass, completed work/checks and evidence locations,
   unresolved findings, active worker ownership (if any), acceptance state and exact
   next permitted action. Link this procedure; do not copy it into the handoff or
   rely on chat history. Preserve approval authority in `UI_PAGE_MATRIX.md`.

Use this compact worker brief; include only relevant facts and authority sections:

```text
Role and approved model/reasoning:
Checkout / branch:
Observed problems and expected results (agreed fix list):
Starting files/helper and established evidence:
Protected behavior / non-goals / relevant authority sections:
Required focused checks and completion boundary:
Escalation: after one focused lookup, ask the calling planner about consequential
uncertainty with evidence and a recommendation; continue independent assigned work.
Return: changed files, checks/results, unresolved findings and next needed action.
```

### 4.3 Verification at affected boundaries

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

### 4.4 Independent review and bounded remediation

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
can change them. Small visual corrections follow the UI exception in `AGENTS.md` and `UI_SYSTEM.md`.
Follow any required fixes-only review gate. Reopen wider review only for a concrete
new implication, contradiction, or material scope change. Do not use repeated broad
reviews as a substitute for executing a missing journey.

### 4.5 Functional-slice preflight and handoff

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

### 4.6 Completion gates

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

### 4.7 Branch cleanup and publication

Use concise purpose-based branch names such as `ui-overhaul`, `fix/button-colors`,
or `release/production-prep`; do not add an agent-identifying prefix unless the user
requests it. When practical, settle the branch name before its first push; renaming
is not a code/history change. Preserve an explicitly assigned checkout/branch.

Before deleting old local or remote branches, produce a bounded read-only inventory
separating fully merged branches, branches with unique commits and active branches.
Delete only the exact branches then authorized by the user. Removing a merged branch
pointer does not remove its commits or rename historical merge/PR records. Staging,
committing, merging, pushing and deploying each require applicable authorization.


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

## 6. Admin-test follow-up corrections — approved implementation contract (2026-09-05)

The read-only investigation is complete. It source-traced the complete
submission/review lifecycle, actor, state, duplicate, progress, privacy, audit,
notification, and realtime paths plus every recorded Admin-test observation. One
disposable Testcontainers reproduction stopped after Docker could not access the
Docker socket; no user-owned database was touched. The user approved the product
decisions below. Implementation remains unauthorized until the required single
independent readiness review clears this final contract.

### 6.1 Final authority and investigation findings

- Global Admin and SuperAdmin roles are additive to genuine event roles. An
  Admin/SuperAdmin with an ordinary Participant, Captain, or Co-captain role has
  exactly that role's team visibility and submission authority, subject to the
  ordinary lifecycle and cutoff. Global role alone never grants team access,
  submission creation/replacement/withdrawal/resubmission, focus mutation, or
  another team's private evidence. Admin review remains a separate capability.
  Automatic Captain status and any new general cross-team submission inspection
  or correction mode are explicitly deferred; the existing explicit, read-only
  SuperAdmin team-focus inspection remains unchanged.
- A Reversed submission remains immutable and cannot be directly re-approved.
  While the ordinary or explicitly reopened upload window is open, it may have
  exactly one linked corrected child with a new image. The child is Pending and
  follows ordinary review. Its later approval creates a new contribution while
  the reversed contribution stays inactive; predecessor, approval, reversal,
  replacement, and review history remain linked. A closed upload window requires
  the existing reasoned Admin reopen action.
- Duplicate-disabled drop requirements are keyed by immutable shared catalogue-item
  identity within the requirement, not by boss/source-specific `SourceDropId`,
  screenshot checksum, or submission ID. The same item may legitimately satisfy
  separate sibling requirements. Multiple eligible source rows for one item remain
  valid alternatives within one requirement. Allocation groups by `(TeamId,
  RequirementId, ItemIdSnapshot)`: a missing explicit cap has effective value `1`,
  every alias must have the same effective cap, and inconsistent aliases block
  board approval. Duplicate-enabled requirements continue to count eligible copies
  until the requirement target or source-specific explicit cap is reached.
- One approved migration is budgeted to freeze catalogue-item identity into the
  event and approval drop snapshots. Existing rows are backfilled only when frozen
  snapshot facts identify one safe item. A preflight must fail closed on ambiguous
  or mismatched rows and report event, requirement, source-drop ID, frozen item
  name, and current mapping so an operator can correct the record before retrying.
  Reading mutable current catalogue identity as historical truth is forbidden.
  Extend the existing operator preflight/migrate surfaces rather than add a service:
  preflight emits a deterministic external mapping template and database
  fingerprint; the operator supplies an adjudicated catalogue-item ID for each
  flagged row and confirms the mapping-file hash. The migration command validates
  the fingerprint, row IDs, and item IDs, loads them into a connection-scoped
  temporary table, and runs the one EF migration on that same open connection. The
  migration auto-fills only unambiguous rows, consumes required staged mappings,
  verifies both snapshot families are complete, and then makes the columns
  non-null. The temporary table disappears with the session; neither frozen names,
  current `SourceDrop` mappings, nor retained schema are altered by adjudication.
- Submission creation/editing and every review, correction, rejection, approval,
  reversal, and linked-resubmission action require the main immutable `AuditEntry`
  in the same transaction as the existing submission-local `ReviewAction` and any
  required notification.
- Each objective on a tile remains isolated by `RequirementId`. Completing,
  approving, reversing, or rebalancing one requirement cannot contribute to or
  close a sibling. Retargeting a Pending submission must recompute and store the
  selected requirement/drop's authoritative weight; it must never carry the old
  target's weight into the sibling objective.
- Wise Old Man HTTP/JSON parsing and typed decimal caching are already invariant.
  Signup and the complete My Accounts Add/Edit/fetch journey must use invariant
  machine transport with localized display and form input. Onboarding remains in
  Danish/English regression proof even though source inspection found its current
  path culture-consistent. Admin competition synchronization is not part of this
  browser form-culture defect.
- Draft finalization must use the existing system-owned primary Playing-character
  authority. A valid primary plus another regular Playing character is not
  ambiguous and must not block finalization.
- The existing evidence popup remains. Board/Team Board and Admin Review evidence
  images gain click-focused toggle magnifier zoom plus pan inside that popup, including
  keyboard, pointer/trackpad, and practical touch support. Do not render a separate
  zoom-control bar. Focus, Escape/backdrop close, responsive sizing, themes, and
  reset-on-close remain accessible. No media dependency or generalized viewer
  framework is permitted.

The authoritative lifecycle matrix is:

| Boundary | Participant/Captain mutation | Admin review | Retained history |
| --- | --- | --- | --- |
| Pre-Live | Closed | Closed | Preserved when present |
| Live before official end | Open for role-authorized current-team evidence | Open | Preserved |
| After end through upload cutoff | Only in-window drops may be uploaded/edited | Open | Preserved |
| After cutoff | Closed | Open while Live/Awaiting Final Review | Preserved |
| Explicitly reopened uploads | Open only until the new cutoff; official drop-acquisition end is unchanged | Open | Preserved |
| Awaiting Final Review | Controlled only by the active/reopened upload window | Open | Preserved |
| Finalized | Closed | Closed | Read-only |
| Unfinalized | Returns to Awaiting Final Review; uploads remain closed until separately reopened | Open | Read-only except approved review actions |
| Archived/Cancelled/Hidden/Discarded | Closed | Closed through ordinary review routes | Retained under lifecycle/privacy rules |

Only Pending evidence may be edited, corrected, approved, or rejected; only
Approved evidence may be reversed. Withdrawn and Replaced predecessors are
read-only. Rejected and Reversed predecessors may each have at most one direct
linked Pending child while uploads are open. Stale or repeated review requests
must not create a second contribution, child, audit, notification, or progress
effect. An archived former credited owner may see only their own retained
Rejected/Withdrawn history; former teammates and cross-team viewers remain denied.

### 6.2 Implementation passes

1. **Shared culture and additive-role boundaries.** Correct Signup and My Accounts
   decimal transport once at the owning rendered/postback boundaries, preserving
   localized editing and the invariant WOM client. Make `EvidenceAuthority`, the
   shared header, Team Board, canonical `/Submissions` routes, and submission
   handlers compose genuine Participant/Captain/Co-captain capability with global
   Admin/SuperAdmin capability. Prove Admin-only, Admin-plus-Participant,
   Admin-plus-Captain, and Admin-plus-Co-captain behavior without cross-team access
   or lifecycle bypass. Extend Development reset with only the stable global-only
   and additive-role accounts needed for the route-ordered manual journey.
2. **Immutable item identity and duplicate enforcement.** Add the one approved
   snapshot migration and fail-closed operator preflight. Carry immutable item
   identity through Board editing, event publication, corrected publication,
   approval snapshots, submission validation, approval caps, reversal/rebalancing,
   public progress, and rankings. Preserve legitimate source alternatives, validate
   one consistent effective item cap before approval, and prevent excess
   contribution at the server boundary while preserving duplicate-enabled
   requirements and the same item in separate sibling objectives. Exercise the
   operator preflight/adjudicated-map/migration/retry path against a copied retained
   database, never the user's working database.
3. **Submission and review correctness.** Add atomic main-audit entries; recompute
   authoritative weight on participant/Captain and Admin retargeting; implement the
   approved one-child Reversed correction path; preserve Pending/Approved review
   boundaries, immutable attempts/assets, stale/concurrent idempotency, rejection
   notifications, progress and focus invalidation, and predecessor history. Add the
   narrow archived credited-owner read path and hide lifecycle-invalid Admin Review
   controls while retaining service rejection. Include `/Evidence/{assetId}` in the
   same status-aware authorization boundary. Seed only the Reversed predecessor and
   genuine archived former credited-owner records needed for manual acceptance.
   This is not a submission/review UI redesign.
4. **Draft and bounded presentation corrections.** Reuse the primary-character
   query in finalization and add the minimal not-yet-finalized Development reset
   state for a valid primary plus secondary Playing character. Suppress orphaned
   Signup `03`; on a fresh signup default only the required system primary account
   to the preferred character while every later account question starts at
   None/unselected. Route the Admin logo to public home. Keep the existing public
   `/Events/{slug}/Teams` roster as the pre-board landing destination, then, only
   after Board publication, add it as the localized Teams/Hold sibling of
   Boards/Drops/Leaderboards without copying the Board masthead or otherwise
   changing the approved Teams page. Use the shared reduced navigation-to-masthead
   spacing and keep Board routes unavailable before publication. Align Onboarding's
   joined EHB/Wise Old Man control with My Accounts' text-color-only fetch hover and
   apply invalid styling to the complete joined control. Enhance the existing
   evidence dialogs with accessible zoom/pan.

Each pass stops at its named boundary, receives focused discriminating tests, an
independent review, and fixes-only remediation for concrete findings. Tests must
cover English/Danish round trips; additive versus implicit authority; shared-item
duplicates under both flag values; migration preflight; one tile with fulfilled
and incomplete sibling objectives; retargeted weight; lifecycle/state/actor
submission and review boundaries; audit atomicity; reversal/resubmission history;
primary-character finalization; and the exact markup/navigation/zoom behavior.

### 6.3 Complexity budget, gates, and stop rules

- Budget: zero new tables, pages/routes, services, authorization policies, jobs,
  dependencies, compatibility layers, or generalized frameworks; exactly one EF
  migration plus its designer/model snapshot for immutable catalogue-item identity.
- Reuse current role facts, routes, submission/review service, audit domain,
  notification path, progress calculators, primary-character query, Development
  reset, native evidence dialogs, existing operator preflight, and `--migrate`
  surface. A connection-scoped temporary mapping table is migration mechanics and
  leaves no retained table; no new operator service is introduced.
- No general identity/authorization redesign, Admin/SuperAdmin implicit team access,
  new SuperAdmin submission-inspection mode, Board redesign, submission/review UI
  redesign, new image storage, translation overhaul, OCR, or evidence-report system
  is in scope.
- Before implementation, run exactly one independent read-only readiness review of
  this final contract against current code, real route reachability, Development
  reset, migration/preflight operability, pass ordering, and complexity budget.
- Implementation stops for user direction before changing these product rules,
  deriving historical item identity from mutable catalogue state, adding an
  unbudgeted artifact, or correcting an unrelated issue. Packaging, commit, push,
  merge, deployment, and release each remain separately unauthorized.

### 6.4 Independent readiness review — corrections resolved (2026-09-05)

The single required independent read-only review initially returned **NOT READY**
on four bounded contract gaps. The user then approved objective-scoped shared-item
caps, and the final contract now resolves every named blocker without repeating the
one-time review:

1. Duplicate-disabled caps are frozen per `(team, requirement, item snapshot)`;
   alternative sources remain valid, null means `1`, inconsistent effective alias
   caps block board approval, and sibling objectives remain independent.
2. The existing preflight and migrate surfaces own deterministic external mapping,
   hash/fingerprint confirmation, same-connection temporary staging, migration
   consumption, and post-backfill verification without altering frozen/current
   catalogue facts or leaving a table.
3. `MANUAL_TEST_CHECKLIST.md` now owns one compact pending Section 6 journey, and
   each implementation pass owns only its minimum missing reset fixtures.
4. Active product/data/architecture authority now includes the narrow archived
   former-owner Rejected/Withdrawn detail and asset exception, Reversed predecessor,
   and status-aware `/Evidence/{assetId}` compatibility boundary.

The planning gate is therefore **READY**. Implementation still requires the user's
separate authorization and begins only with Pass 1.

### 6.5 Pass 1 implementation/review checkpoint — 2026-09-05

The user authorized Pass 1. The bounded implementation now uses request-culture
display/binding with invariant machine transport for Signup and My Accounts,
resolves genuine event membership before the global Admin fallback in the existing
evidence authority, preserves global-only denial and separate Admin/SuperAdmin
capabilities, and adds only the minimum deterministic additive-role Development
fixtures. No new table, migration, service, route, policy, job, dependency, binder,
framework, or generalized abstraction was added.

The independent reviewer accepted the role/navigation/privacy/lifecycle scope and
found two culture-proof defects: missing Danish messages for the invariant Signup
parser and a markup-only Onboarding assertion. Fixes-only remediation added the two
shared Danish resources and a real English/Danish Onboarding fetch/render/postback
test. After replacing a contaminated long-test validation sequence with the simpler
focused boundary, both Signup/My Accounts culture cases and both Onboarding cases
pass. Earlier Pass 1 verification also passed the Web, BrowserTests, and
IntegrationTests Release builds, three focused browser tests, six focused
integration cases, and `git diff --check`. A fresh fixes-only closure review found
one remaining test-only gap: the two Danish parser resources lacked direct
localization assertions. The existing Danish localizer test now asserts both exact
keys and values; that focused test passes 1/1 and `git diff --check` passes.

Pass 1 implementation/review/remediation closure is complete. AF-01's manual-
acceptance preflight and user walkthrough remain outstanding. Stop here; do not
begin Pass 2 or package/commit/push/merge/deploy without the next authorized gate.

Manual AF-01 subsequently exposed two browser/reachability gaps that direct HTTP
coverage had missed. My Accounts and Onboarding now use the same localized
text/decimal boundary without the invariant client number validator; Signup retains
its separate visible-localized/hidden-invariant transport. General Participant and
Captain header navigation each prefer the sole Live team, otherwise the sole
Awaiting Final Review team; Captain includes co-captain and valid enabled Emergency
Captain access. Retained Finalized/Archived memberships no longer hide a current
link, while genuinely ambiguous eligible teams expose no shortcut. The four EN/DA
culture cases, focused navigation integration test, and `git diff --check` pass.

The user manually accepted the reachable Signup, My Accounts, additive-role, and
header journeys on 2026-09-05. Local Discord OAuth cannot reach Onboarding because
its callback is not localhost; the user accepted the discriminating EN/DA rendered
control and persisted-comma integration proof in place of that manual step. These
were fixes to approved Pass 1 behavior, not added scope. **Pass 1 is complete.**
Stop here; Pass 2 and package/commit/push/merge/deploy remain separately gated.


## 7. Admin consistency — approved signup-question popup pilot (2026-09-07)

The user prioritizes Admin UI consistency and actual reuse of components and
behavior. Preserve the accepted public UI, except focused regression where an
Admin-owned change has public consumers. Discovery is complete; the user approved the popup behavior, confirmation and
typography contract and this first implementation pilot. Work in the isolated checkout on
`codex/admin-consistency`, based on merged/deployed `42b2a7d`.

Use the existing UI_SYSTEM primitive registry and UI_PAGE_MATRIX family map;
do not replace them with a new inventory/specimen system. A bounded route scan
found 23 Admin Razor page entries excluding the two historical reference/specimen
routes; this includes the existing WIP dashboard and compatibility/preview entries,
not 23 independently verified workflows. Existing page approvals remain intact.

| Pattern / area | Source evidence | Next relevant coverage |
| --- | --- | --- |
| Shell, tokens and feedback | Physical shared Admin layouts, Admin CSS tokens, shared toast partial/site.js already exist | Preserve ownership; inspect affected rendering/focus only when changed |
| Directory controls / tables | Events and Accounts reuse CSS but render their own toolbars/table markup; Audit and Review compose filters separately | Compare realistic populated/empty/filter states; choose actual repeated subcontrols before extracting; keep page-specific columns |
| Fields / action groups | `.admin-field` and button CSS reused across Identity, Schedule and Questions; page composition owns markup | Validate consistent grouping, validation placement, pending/locked controls on the selected pilot |
| Route dialogs | Three lifecycle owners: Questions, Catalogue, Accounts | Shared close/focus/scroll/history behavior; preserve route/host distinctions and desktop-to-narrow navigation |
| Save / recovery | site.js has existing enhanced POST machinery; Catalogue uses it, Accounts/Questions implement transport separately | Preserve entered values on failure and usable retry; prevent repeated submission; keep cross-path routing guard |
| Specialized workspaces / review / closeout | Board, Draft, Catalogue, Review, Finalize have distinct tasks; existing matrix owns their geometry/status | Later workflow-specific passes; preserve workspace spatial context, role/state locks, evidence/history and public result consumers |

Read-only independent behavior findings:
- Catalogue close callback reads `opener` after it is cleared (`catalogue-admin.js`
  284–285); source defect verified, actual focus loss may be masked by native dialog
  restoration and requires rendered verification.
- Questions POST transport failure replaces the editor with an alert, removing
  entered controls and the visible close/retry controls (`signup-questions-overlay.js`
  138–155). DOM loss is source-confirmed; no live fault was injected.
- Shared details-confirmation mechanics are copied across three modules; Accounts
  moves focus to Cancel while Catalogue/Questions do not.
- Generic POST helper already handles submitter values, duplicate-submit guards,
  control restoration and content updates. Questions responses have a different
  path from their host, so blindly applying `data-update-targets` would conflict
  with the existing same-path guard in site.js. Preserve that boundary.

The user approved a single Signup Questions pilot after agreeing intended behavior;
existing pages are evidence, not automatic design targets. UI_SYSTEM owns the
accepted reusable popup rules. Later rollout and shared behavior extraction await
manual acceptance of this pilot.

### Approved pilot scope and outcomes

Actor/entry: authorized event Admin, existing editable Development event, navigate
through Admin Events -> event Participants -> Signup form. Preserve actual route,
authorization, handler names, stored question/answer semantics and locked states.

| Journey | Required result and proof |
| --- | --- |
| Open, close, reopen; direct route and reload | Predictable dialog/fallback, correct URL, focus and body scroll; browser proof |
| Add/edit/save valid question | Keep this multi-question editor open, update list/count, one result notice, next edit reachable |
| Invalid input or failed request -> correct/retry | Preserve all unsaved editor values, usable localized feedback and retry, no duplicate save |
| Close/Cancel/Escape/Back/outside click with unsaved edits | Compact inline discard choice; cancel retains edits and open URL, discard leaves expected parent; unchanged close has no prompt |
| Save pending -> attempted dismissal | Keep editor present until request settles; do not imply aborting a request cancels a server write |
| Delete question -> cancel/confirm | Compact inline confirmation, Cancel before Delete question, consequence copy specific to Account vs other types; Escape dismisses only the active confirmation |
| Other dirty question forms -> save/reorder/delete | Updating one question must not silently erase unrelated unsaved edits; preserve them or obtain the same discard decision before destructive replacement |
| Narrow screen, keyboard, light/dark, long content | Same modal at every width, full-screen at <=900; resize preserves editor/URL/values/pending state, meaningful focus, visible close, no clipping; direct standalone URLs retained |

Typography: shared Admin family/scale, popup title above section headings, normal
body/control text and smaller muted help. Confirmation title is emphasized body
text, never a competing page heading. Fix the current oversized warning and use
Delete for permanent question deletion. EN/DA wording preserves actual consequences.
Unsaved discard protection is approved; no prompt when unchanged. Reload/navigation
away may use the browser's native unsaved warning; do not build custom infrastructure.

Files: signup-questions-overlay.js, Questions.cshtml, its existing Admin CSS owners,
necessary scoped layout/localization bindings, and focused existing test surfaces.
Do not modify Accounts/Catalogue lifecycles, public UI behavior, backend deletion
semantics, migrations or protected Board/Draft workspaces. Zero new tables, services,
routes, dependencies, jobs or generic overlay/dirty-state frameworks. Reuse current
owners; extraction across pages follows acceptance, not this pilot.

Verification: execute focused browser journeys including save failure, validation,
dirty dismissal/history, duplicate-save prevention and scoped rendered theme/narrow
checks. Use disposable local data or a controlled browser harness with real rendered
markup; distinguish harness behavior from authenticated server proof. Run compilation
if Razor changes, affected existing tests where assertions cover changed bindings,
and diff checks. No full suite or broad Admin audit. One independent scoped review
then bounded remediation; user performs final visual acceptance. No commit/push,
production mutation, further page family or rollout is authorized by this pilot.

### Approved pilot follow-up — 2026-09-08

User accepts the current visual direction and authorizes two final corrections:
1. Signup code flow: toggle controls code-field visibility without saving. Enabling
   without an existing code requires a new code; with an existing code show
   Replacement code and Leave blank to keep the current code. Save code settings
   explicitly persists both toggle and code. Disabling hides the irrelevant input;
   preserve server hashing, validation, permission and disable/clear semantics.
2. Responsive popup: remove viewport-dependent closing/navigation for this editor.
   Opening from Participants at any width uses the same route-backed modal; <=900
   becomes full-screen through CSS. Resizing either way never closes/reloads it or
   discards values, alters URL or cancels a pending write. Keep standalone direct
   routes for deliberate navigation/recovery. Existing Close/Back/discard/focus and
   pending guards apply at every width.

Extend only Questions markup/model non-secret presentation state if needed, its
existing JS/CSS/localization and focused tests. No database schema/rule changes,
no other popup rollout. Verify code off/on/existing-code blank-retain/failed save,
small-screen open/reload, resize clean/dirty/pending, close/Back/discard and direct
route retention. Prior unrelated passing pilot checks remain applicable.

Pilot and follow-up manually accepted by the user on 2026-09-08 after local use.
The accepted target can guide the next separately authorized Admin popup pass;
this acceptance does not authorize rollout, extraction, packaging or push.

## 8. Participant-management popup — approved next pass (2026-09-08)

User approves continuing the Admin popup work and supplies current Participant
management screenshots showing a spread-out summary and disconnected lower actions.
This pass targets the pictured Participant editor, reached from Participants via
Edit; website Accounts Create/Manage is not the pictured surface and remains later.
Use the accepted Signup Questions pilot as the behavior/typography target.

### Scope, composition and protected behavior

- Compact signup summary: keep all current event/status/date/order/source/team/
  owner/payment/status-note information, with labels near their values and a
  readable responsive grouping. Preserve payment's actual toggle form/handler.
- Signup answers follow the summary, then private Admin notes with their own clear
  save action. Remove duplicated static help/placeholder wording and excess space.
  Preserve all question types, optional Regular/Alt distinctions, EHB, validation,
  previous/readonly answers and optimistic version inputs.
- User refinement (2026-09-08): place Save changes beside the final answer
  when width and field shape allow; wrap naturally for full-width answers/mobile.
  Right-align the lower action triggers on one wrapping row, with expanded
  confirmations retaining readable available width.
- Group ownership transfer and withdrawal/restoration into a named participant
  actions area. Keep confirmations compact and inline, Cancel before commit action.
  Retain the exact conditional availability and bindings for owner search, transfer,
  remove/restore, vacancy replacement and promotion follow-up. Do not collapse these
  distinct authority/lifecycle operations into one generic confirmation configuration.
- Use the same modal at every width with full-screen presentation at <=900.
  Resizing preserves URL/content/dirty/pending state. Deliberate standalone/direct
  routes and reload remain usable; preserve Participants filters/scroll on close.
- Apply accepted close/Back/discard/focus/scroll rules, pending duplicate protection,
  input retention on failed/invalid saves and safeguards against losing another
  dirty form when payment/notes/details/actions replace content. Successful independent
  saves stay in this participant workspace with accurate updated state/feedback;
  existing authoritative redirects after lifecycle actions remain meaningful.
  After a successful mutation, closing must return to refreshed Participants data,
  preserving filters and scroll; refresh failure must not silently present stale
  data as current.
  User follow-up explicitly includes the accepted Signup Questions editor: its
  question-count update alone leaves parent table columns/answers stale. Apply
  the same freshness rule there after question changes/deletion, with focused
  regression; no unrelated Questions layout changes.

Preserve authorization, privacy, concurrency tokens, audit, ownership handoff,
retained signup history, lifecycle and capacity rules exactly. No backend rule,
route, schema, data migration, public-page or Board/Draft change. Directory table
composition stays intact apart from necessary trigger/refresh bindings.

### Ownership and complexity budget

Primary files: Participant.cshtml, participant dialog section of event-manage.js,
existing Admin CSS/localization/layout script bindings and focused existing tests.
Make common editor guard/lifecycle/feedback behavior physically shared with Signup
Questions where both need it; retain explicit page-specific URL/response adapters.
Budget at most one small Admin-only shared JS owner and, only if useful to eliminate
actual repeated discard/feedback markup, one shared Razor partial. Prefer existing
owners where they already fit. No new dependency, service, route, table, policy,
job, generic overlay framework or declarative action system. Touch the accepted
Questions consumer only to adopt identical shared behavior; protect its accepted
composition/code settings with a focused regression. Avoid changing public site.js
transport unless a concrete integration requirement proves it necessary.

### Journeys and proof

| Journey | Required outcome / focused proof |
| --- | --- |
| Admin Participants -> Edit, close/reopen/reload/direct URL | Same participant/context and correct URL/focus; real rendered navigation/browser |
| Compact populated summary, long names, read-only/withdrawn states | All facts/allowed actions still visible, readable at desktop and narrow; scoped source + current rendering |
| Edit answers or notes -> invalid/failing save -> correct/retry | Values remain, validation/feedback usable, successful state updated; browser intercepted responses + existing binding tests |
| Dirty answers -> payment/notes/transfer/lifecycle action | No silent loss of other unsaved forms; pending duplicate guard and explicit discard decision; browser guard proof |
| Close/Back/Escape with dirty state or nested confirmation | Only relevant confirmation dismissed; cancel preserves content, discard leaves expected context |
| Resize clean/dirty/pending; narrow open and scrolling | Modal stays present; visible Close, focus, scroll containment and readable action layout |
| Shared Questions consumer | Accepted code visibility/save and modal/dirty/pending behavior unaffected; focused existing Node regression and one browser smoke |

Use controlled local read-only preview and mocked writes for UI proof, explicitly
separating that from persisted lifecycle verification. Backend unchanged means no
full integration-suite rerun. Compile Razor; run relevant existing Node regressions,
scoped diff checks and one independent source/visual review, then bounded named
remediation. Final user acceptance is page-specific. Stop before any next family,
commit, push, deployment or unrelated cleanup. A concrete product contradiction or
unbudgeted owner requires planner/user direction before implementation.

## 9. Accounts Create/Manage popup behavior — approved 2026-09-08

User authorizes the next Accounts/Roles pass and considers its general appearance
already good. Preserve directory/detail composition; focus on behavior and the
content revealed by action buttons. Create is the existing emergency-credential
creation route; Manage includes website accounts and emergency credentials.

- Reuse the accepted Admin editor guard; same modal at all widths (fullscreen
  <=900), clean close without prompting, dirty Close/Back/cancel protection,
  pending duplicate/dismissal guard, failures retaining inputs and clear retry.
- Replace confirmation-modal-inside-editor with compact inline confirmation,
  one visible confirmation at a time, Cancel before action, body-sized wording,
  Escape/cancel returning focus to the trigger. Preserve required reason fields,
  every action's real handler/data/availability, and one-time link disclosure.
- Preserve create scope/event/team semantics and deliberate directory success
  navigation. Scope changes must not silently lose typed credential details.
  Manage saves retain meaningful current state/feedback. Both paths refresh the
  affected Accounts directory including filters/pagination/scroll; failure after
  successful save reports that truth and reloads instead of exposing stale data.
- Reload/direct overlay restoration must restore all new trigger bindings;
  deliberate standalone routes/recovery remain usable. Preserve action-specific
  transfer/navigation destinations rather than forcing all actions into a popup.
- Protect role gating, website/emergency distinction, authorization, concurrency,
  disable/reset/restore history, credential/token handling and cutoff rules.
  No backend/product/route/schema/security changes or unrelated page rollout.

Files: Accounts Create/Manage Razor, account-manage-dialog.js, scoped existing
Admin CSS/localization if needed, existing account dialog tests. Shared guard may
be extended only for an actual common need, with focused consumer regression.
No new generic framework, dependency, service, database object or route.

Implementation Astra Low, independent source-only review Astra High (user-approved
lean rollout policy). Implementer performs focused existing Node checks and Razor
build only for compiled markup changes. One short real-browser check by root for
nested confirmation, dirty/pending/failure, parent refresh and reload handoff;
intercept mutations, protect local data, explicitly separate UI proof from persisted
account/security behavior. User provides final visual acceptance. No independent
reviewer browser pass or broad audit/full suite. Stop before next family or package.

Accounts Create/Manage manual acceptance complete on 2026-09-08 after the bounded
Disable/Generate-link confirmation visibility correction. No next-family or
packaging authorization is implied.

## 10. Catalogue Add/Edit popup behavior — approved 2026-09-08

User accepts the general Catalogue appearance and authorizes behavior-first Add/Edit
rollout, including action-revealed text/font/confirmation defects and suspected stale
parent data. Preserve its specialized activity/drop composition and existing forms.

- Adopt the accepted shared Admin dirty/pending/discard/failure safeguards and same
  modal across widths (fullscreen <=900). Clean close is immediate; dirty Close,
  Back and cross-form actions require an explicit decision; failed/invalid saves
  retain inputs and usable retry. Resize never changes route or abandons edits.
- Keep confirmations compact and inline, only one decision visible, Cancel first,
  normal Admin body typography, meaningful focus and revealed content scrolled into
  view. Cancelling discard restores the same typed confirmation/visible editor.
  Preserve typed DELETE requirements and duplicate-item choice semantics exactly.
- Successful Add/Edit/drop/state/delete operations refresh affected parent catalogue
  data before return, preserving search/filter/pagination/scroll and native modal
  connection. Refresh failure after success is truthful and offers actual reload;
  deleted activity/create redirects remain meaningful. No stale previous editor.
- User clarification: include correct/missing toast messages across Add/Edit/drop,
  state/deletion and failure/recovery paths. Preserve one truthful outcome message
  with correct severity, visible during the modal; do not confuse save failure with
  saved-but-parent-refresh failure or show duplicate success notices.
- Preserve expanded drop editing, item-name duplicate selection, probability/rate
  parsing and labels, image cache actions, readonly/server error states, every
  hidden ID/version binding and existing action-specific redirects. Direct/reload
  overlay restoration rebinds new triggers; explicit standalone routes remain usable.
- Protect Admin/SuperAdmin gates, audit/concurrency, dependency-safe delete versus
  deactivate, immutable catalogue identity, board snapshots/history and image source
  security. No backend/domain/schema/import/Board/public behavior changes.

Primary files Catalogue/Index.cshtml, catalogue-admin.js, scoped existing Admin CSS
and localization, existing catalogue-admin.test.js. Reuse admin-editor-guard.js;
extend it only for a demonstrated common need and check affected consumers. No new
framework, dependency, route, service, persistence or generalized configuration.

Astra Low implementation; Astra High source-only independent review, no reviewer
browser inspection. Focused existing Node tests and Razor build only as appropriate.
Root executes one bounded browser chain for changed interactions: dirty/failure and
confirmation recovery, native modal/parent refresh, Add/Edit reload handoff and
narrow resize; use mocked writes/read-only local fixtures, no catalogue mutations.
User final visual/action-message acceptance remains separate. No broad audit/full
suite, next-family rollout, packaging, commit or push is authorized.

### Current-popup confirmation trigger correction — approved 2026-09-08

Apply to the existing Questions, Participant Manage, Accounts Create/Manage and
Catalogue Add/Edit adapters: hide the initiating action button while its inline
confirmation is visible; restore it before Cancel/Escape focus return and when
switching/dismissing that confirmation. Preserve typed values, discard recovery,
pending/duplicate protection and existing layouts/handlers. Adopt the general rule
in UI_SYSTEM; no other family rollout or whole-site audit is included.
Use Astra Low for the bounded correction and Astra High for one source-only review
of this correction, without reopening the completed popup passes. Verify the
changed visibility/focus paths and affected guards with focused existing checks;
no repeat build or browser harness work unless a concrete changed risk requires it.
Prior page approvals remain page-specific; the user manually approves this
correction on 2026-09-08. Catalogue's broader approval remains separately recorded.
Now agree the leaner
workflow before further rollout; no packaging, commit, push or deployment.

## 11. Add Participant popup behavior — authorized 2026-09-08

User authorizes the next popup pass after agreeing Luna Max implementation,
focused implementer verification and read-only correctness review without reviewer
visual inspection. Next bounded surface: Participants -> Add participant, including
its existing `addParticipant=1` reload/direct entry. Preserve the existing form
composition, owner picker, question/account inputs, lifecycle availability and
CreateInternalParticipant handler/capacity/waiting-list rules (FUNCTIONAL_CONTRACTS
5.5). Participant Manage and Catalogue approval states remain separately owned.

- Same modal at every width, fullscreen <=900; resize preserves values, URL and
  pending work. Retain existing deliberate route/recovery behavior.
- Reuse shared editor guard for dirty Close/Cancel/Escape/Back and pending duplicate/
  dismissal protection; discard Cancel preserves values/focus. Inline confirmations
  follow the accepted trigger hiding, focus and visibility rule where applicable.
- Failed load has usable close/retry; validation/transport failure retains submitted
  values and usable localized recovery. Preserve owner-picker and form bindings.
- Successful creation returns to fresh Participants data with retained filter/sort
  context and appropriate scroll, one truthful success notice and no false dirty
  prompt. Existing route-backed navigation is acceptable; no new partial-refresh
  infrastructure. Saved-but-refresh-failed feedback must not imply creation failed.
- Check correct/missing toast outcome, severity, duplication and modal visibility
  within this pass. No separate toast audit.

Files: Add-dialog region and necessary initialization hooks in event-manage.js,
_InternalParticipantForm.cshtml, necessary Participants.cshtml/layout/localization
bindings, existing Admin CSS and focused Participants tests. Shared guard changes
only for a demonstrated common need. No new backend rule, domain/schema/service,
route, dependency or generic framework. No Board/Draft or unrelated family work.

Luna Max implementer owns changes and focused runnable checks for all-width open/
resize, dirty dismissal, pending duplicate/dismissal, retained failure input and
successful fresh return/reopen. Reuse existing test infrastructure; compile only
when Razor/resources require it. Use controlled mocked writes, never mutate/reset
the user's database or restart their 7131 app. A browser check is only justified
by a named changed risk not covered by existing executable checks; no repeated
harness debugging. One Astra High source-only correctness review of this exact
pass; no reviewer browser/screenshots or duplicate passing checks. User supplies
visual acceptance. Stop after this pass for feedback; no packaging/commit/push/
deployment or automatic next family.

Add Participant pass complete 2026-09-08: user visual approval and source-only
correctness review clear after fixing current filter/sort context and successful
creation/failed-refresh reload recovery. Add/Participants/Participant Manage Node
regressions, syntax and diff checks pass; Razor build passed with 0 warnings/errors
before JS-only corrections. No backend changes or persisted creation tests; client
responses were mocked in focused tests. Stop before another popup family.

## 12. Teams/Draft Add team popup — authorized 2026-09-08

User says Add team's appearance is already good; preserve the current native modal,
field order, typography and Draft workspace geometry. Scope is only the Add team
trigger/dialog/form (`#add-team`) on Admin -> event -> Teams/Draft, in Setup and
the permitted finalized pre-formed correction state. Team roster dialogs, Draft
controller/turns, Board, imports and other popup families are excluded.

General-look approval covers composition, not blanket approval of revealed
confirmations/content, typography or feedback states. Inspect applicable details
against UI_SYSTEM; the user notes no extra revealed surfaces requiring an added
render for this popup. Future passes may use targeted renders for concrete gaps,
without duplicate reviewer visual inspection.

- Reuse admin-editor-guard in the existing Add team adapter: clean dismissal,
  dirty Close/Escape/outside-click with compact discard/keep choice, native
  navigation/Back unsaved protection where applicable, pending duplicate/dismissal
  protection. Keep this modal at all widths; resizing never abandons entered data.
- Preserve name/formation/affiliation and all existing AddTeam bindings, permissions,
  state/confirmation rules, audit and transaction semantics. Finalized inline
  confirmation follows Cancel-first, hidden trigger, reveal/focus and typed-value
  restoration rules. No new route marker or generic modal framework is required.
- Intercept only this form as necessary to retain typed inputs on failed POST;
  distinguish rendered success/error outcomes (both currently redirect). Success
  returns to fresh Draft state with one accurate toast and retained useful context;
  no false dirty prompt or completed-operation resubmission. Reuse existing shared
  toast and navigation owners; do not rewrite global site.js POST transport.
- Check Add outcome text/severity/recovery/visibility while modal. Correct the
  demonstrated blank-name error currently mislabeled as locked draft, by separating
  presentation messages only; validation/lifecycle behavior stays unchanged.

Files: Add-specific Draft.cshtml markup, event-manage.js Add-only initialization,
existing scoped CSS/layout/localization if required, focused Add team test using
existing popup test infrastructure. Draft.cshtml.cs may change only AddTeam status
wording/selection for blank name; no other handler/backend-rule change. Zero new
route/service/schema/dependency/abstraction. Snapshot only edited files for review.

Luna Max implementer: one focused runnable Add test covering dirty/confirmation,
pending/failure and success feedback/reopen, plus directly affected existing
event-manage consumer regression and syntax/diff checks. Compile once for Razor/
resource changes; add only a focused check if server-message branch changes.
No browser harness, DB mutation/reset, app restart or repeated verification.
Astra High read-only source correctness review; user provides visual acceptance.
Stop after Add team for feedback; no roster pass, packaging, commit/push or deploy.

Named review remediation stays within this pass: preserve the consumed success
notice through the existing pending-toast owner; make Discard reset fields/baseline;
fix confirmation hidden/inline CSS, order, keyboard/focus/reveal and resize scroll
locking; add the missing Danish blank-name resource. The shared site.js toast-host
selector may receive a minimal Add-native-dialog opt-in to keep toasts in the
modal without adding layout classes; its POST transport stays unchanged. Correct
the test that fabricated a success toast on navigation so it checks actual notice
handoff instead. No backend/lifecycle expansion.

Implementation/remediation and source-only review complete 2026-09-08. All named
findings corrected; focused Add team/affected Participant checks, syntax/diff and
Razor/resource build pass (0 warnings/errors). No browser or persisted team writes.
User manually approves the completed Add team pass, accepting minor behavior
differences from other popups without further remediation. No next pass authorized.


## 13. Teams/Draft roster corrections — authorized 2026-09-08

User protects the roster popup's general composition and requests two changes:
show truthful Captain readiness after a Captain is assigned, and choose a role
while manually adding a participant. This bounded correction does not reopen the
whole roster workspace or import workflow.

- Add Participant (default), Captain and Co-captain selection to existing manual
  AddMember and AddExternalMember forms, including permitted pre-formed correction
  variants. Existing post-add role editing remains. CSV role semantics are unchanged.
- Persist membership creation and selected role atomically. Invalid roles or failed
  assignment leave no partial membership/participant/character changes. Reuse the
  existing Captain authority rules, role history, audit and owned-account
  notification behavior; do not implement two client requests or duplicate those
  rules in JavaScript. Preserve lifecycle, permissions, version/concurrency,
  source/capacity/character-reservation rules and finalized correction publication.
- Distinguish a current Captain role (draft-start requirement) from usable owned
  website-account access (event-start requirement, with existing emergency option).
  Once a Captain exists, do not still claim that a Captain must be assigned. If
  website access is missing, name that issue accurately; Co-captain alone does not
  satisfy the Captain requirement. Keep the warning synchronized with server state.
- Preserve composition, subsequent role changes, existing popup handling and other
  completed passes. New controls/feedback follow UI_SYSTEM localization, typography
  and toast rules. No generalized popup rewrite, CSV expansion, new schema, route,
  service, dependency or standalone framework. A small extension to the existing
  authority service for atomic caller-owned transactions is allowed if needed.

Luna Max implementation with a self-contained bounded prompt; Astra High independent
source-only correctness review; user visual acceptance. Focused isolated PostgreSQL
integration tests prove selected/default roles, relevant rollback/history/authority
and truthful readiness; use existing suites rather than a browser harness. Compile
Razor/resources and run only directly affected checks. Docker is available through
approved sandbox escalation; never use or reset the user's application database.
Stop after these corrections and review, without another family or packaging.

Section 13 implementation and bounded remediation complete 2026-09-08. Independent
source-only review clears all findings: truthful badge, malformed-role model-binding
rejection, external selected-role finalized publication/rollback and rendered
readiness proof. Focused isolated PostgreSQL checks and Razor builds pass; final
binding test fixes the lifecycle clock and asserts role-specific rejection. No
application database mutation or browser harness. Await user visual acceptance of
the changed states; general composition approval and other page statuses remain.

User manually accepts section 13 on 2026-09-08 with one small visual correction:
reduce the roster participant remove SVG glyph size while preserving its clickable
area, focus, accessible label, confirmation and existing composition. This authorizes
only a scoped CSS correction with focused cascade/whitespace checks, not another
behavior pass, backend test/build cycle or popup family.

Further section 13 manual correction: user explicitly retains the existing small
positioned roster-member removal confirmation (no move inline) and requests compact
Remove member action typography consistent with Cancel. Match its scoped button typography, padding and height to Cancel; the follow-up
screenshot confirms font-only correction leaves an undersized button. Confirmation
placement, behavior and other controls are protected.
Focused cascade/whitespace inspection suffices for this CSS-only correction.

User manually approves the completed Teams/Draft roster popup on 2026-09-08,
including the remove glyph and confirmation button typography/padding corrections.
Section 13 accepted; no next popup/family or packaging authorized.

Section 13 dismissal correction: user reports main roster popup fails to close on
outside click. Add bounded backdrop handling through the existing close/history/
focus path, preserving entered values and applicable pending protection; clicks
inside the dialog or its small removal confirmation must not dismiss it. Add a
focused executable regression for that missing event path, without another popup
family, layout/backend rewrite, full suite or browser harness. Prior approval
remains for visuals and role/readiness corrections; dismissal awaits correction.

### Section 13 guard completion — authorized 2026-09-08

User explicitly requires completing the missing roster dirty/pending safeguards
before moving on. Reuse admin-editor-guard and existing roster/add-team transport
patterns; preserve all approved composition, role/backend behavior and the small
positioned removal confirmation. Cover all editable forms within this roster.

- Dirty X, Escape, outside dismissal, Back/navigation and switching team/editor
  require Discard/Keep. Keep restores focus and entered values; Discard actually
  restores baseline fields before completion. Other-form submission cannot silently
  lose unrelated edits. No false dirty warning after a completed mutation.
- Pending saves block duplicates and dismissal/navigation until resolved, while
  retaining submitted values and correct submitter/form data. Failure restores
  usable controls and retains inputs with truthful visible feedback. Successful
  saves refresh authoritative parent/roster content, preserve useful route/scroll
  context and produce one accurate outcome toast. Completed mutation with failed
  refresh must be distinguished from failed saving and must not be resubmitted.
- Guards remain effective across widths/resizing; use existing all-width modal
  treatment as needed, without abandoning inputs or redesigning roster layout.
  Existing form routes, authorization, versions, role history, CSV preview/apply
  semantics and finalized publication remain unchanged. No new backend/route/schema
  or generalized popup framework; extend only existing page adapter/guard as needed.

Luna Max implementation, smallest direct runnable guard/response regressions plus
directly affected consumers, one Razor build only if markup/resources change;
Astra High source-only review. No browser harness, app restart, application database
mutation, full suite or repeated passing checks. Planner identifies the next popup
from bounded repository evidence in parallel, but does not start it.

Guard completion implemented and source-reviewed 2026-09-08. All five findings
resolved: preserve submitted dirty baseline through cross-form discard/failure;
require explicit success evidence; refresh sibling participant data; guard native
modal/application navigation across widths; retain visible connected toast host.
File-only changes are serialized with file metadata and covered by the roster
regression. Focused roster and five affected consumer checks pass; Razor build
0 warnings/errors. User check of new guard states remains; no next pass started.


## 14. Board tile popup behavior — authorized 2026-09-08

User accepts current Create/Edit tile composition and flow as shown in the two
current screenshots. Proposed spacing changes, sticky footer, optional-detail or
counting-option collapse, picker search, objective summaries/collapse and wizard
work are NOT authorized. Preserve layout, field order, density and information.
Apply the usual Admin popup behavior and inspect leftover/revealed UI (including
confirmations, expanded controls, type hierarchy, focus and outcome feedback).

Scope: Board Create/Edit tile shared editor, its objective-removal confirmation,
and existing tile-detail/removal popup states affected by the same dialog owner.
Preserve Board canvas/sidebar/toolbar/drag-drop/collaboration, objective semantics,
weights/counting/source selection, image behavior, EHB, versions/permissions,
lifecycle and publication rules. No backend/domain/schema redesign or other family.

- Adopt shared admin-editor-guard with page-specific adapter. Clean Close/Cancel/
  Escape/outside dismiss; dirty in-page dismissal, application navigation and
  switching tile/editor require Discard/Keep. Browser Back/reload/leave use native
  unload protection; do not introduce Back-closes-popup or a new history machine. Keep retains typed state/focus; Discard resets original fields,
  dynamically added/removed objectives and file selection. Initial Edit is clean.
- Same modal across widths, fullscreen on narrow screens; resize preserves typed
  or pending state. Outside-click uses actual bounds/gesture checks and never
  dismisses from inside padding or child confirmations. Restore opener focus.
- Pending mutation blocks duplicates/dismissal/navigation and conflicting actions.
  Capture FormData, submitter and contiguous requirement names before disabling
  controls. Retain all submitted fields/objectives/files on validation, transport,
  permission or concurrency failure; show truthful localized recovery and restore
  controls. Stale board versions require explicit recovery, not silent data loss.
- Objective-removal uses compact local confirmation rather than a nested editor
  modal; preserve local-only removal semantics. Existing tile-removal confirmation
  uses the shared compact pattern while keeping its handler/version semantics.
  Cancel first, hide initiating action while shown, restore trigger before focus,
  Escape cancels only the topmost confirmation, and reveal scrolls into view.
  Use existing complete button primitives (type, padding/height, focus and severity),
  not isolated typography overrides that leave undersized controls.
- Successful mutations return to freshly authoritative Board context: canvas,
  tile/editor data, statistics, version/actions and related summaries all refresh,
  retaining useful scroll/context. Use one correctly severe visible toast; shared
  native-modal toast host must stay connected while needed. Success is explicit,
  not inferred from HTTP200/no errors. Distinguish committed save with image/other
  warning and failed post-save refresh from failed saving; completed work cannot
  be submitted twice. Do not let a background refresh discard active edits/results.

Implementer may extend Board.cshtml scoped markup/inline adapter (or extract only
that existing adapter to a focused JS file if necessary for maintainability and
execution), existing shared guard/layout/CSS/localization and focused tests. Keep
shared changes minimal with existing callers' defaults unchanged. PageModel changes
are limited to minimal presentation/outcome evidence if necessary to distinguish
committed-with-warning from failed POST; no business-rule or persistence change.
No new route, service, schema, dependency or generic modal framework.

Luna Max implementer; Astra High independent source-only reviewer; user final visual
acceptance of changed/revealed states. Reuse board-dialog.test.js and current popup
Node infrastructure. Direct regressions cover dynamic/file dirty state, every
close/navigation path, topmost confirmation/focus/trigger restoration, pending
serialization/blocking, retained failures, explicit success/warning distinction,
fresh parent and failed-refresh recovery. Run affected consumers only if shared
code changes and one Razor/resource build where required. No browser harness,
application database mutation/reset, app restart, full suite, packaging/commit/push
or deployment. Stop after this family for acceptance. The supplied screenshots
are accepted current composition evidence, not authorization to redesign it.

### Section 14 implementation safety boundary

User-approved completion assignment: Astra Low replaces Luna Max only for the
remaining Board save recovery, navigation guards and compact confirmations after
prolonged turnaround. Keep tested close/fullscreen/collaboration corrections; one
completion assignment with focused checks, then Astra High source-only review and
manual acceptance. No new preliminary review or broad discovery.

Lean resumption — user authorized continuation after accepting the gap-first
workflow. Existing source assessment supplies four concrete gaps; do not repeat
discovery: unconditional dismissal bypasses dirty/pending protection; native POST
loses the current form/files on failure; objective/tile confirmations need the
compact local behavior; narrow fullscreen and collaboration's dialog-owner query
need focused correction. Existing editor population, Board data/rules and layout
remain the starting point. Implement these gaps, then one focused verification and
source review; no new preliminary review.

Automatic edit review rejected the initial broad navigation/history/submission and
generic baseline-restoration rewrite as excessive regression/data-loss risk. The
incomplete Board-only edits were restored to their canonical pre-pass snapshots;
all earlier working changes are preserved. The rejected attempt remains evidence,
not an implementation baseline. Do not reconstruct it through incremental patches.

Independent read-only assessment identifies a materially smaller implementation:
reuse existing resetTileForm/addRequirement/populateRequirement/initializeRequirements
and cached tileEditorData to reconstruct the original selected tile/create position
on Discard. Extract only existing open/populate blocks as needed; initialize the
shared guard after population. Replace unconditional close listeners in place with
one guarded close/switch/application-navigation path. No generic DOM snapshot/state
framework and no new history machine. Native browser-owned departure protection is
the existing UI_SYSTEM exception; within-page guards remain required.

Retain form DOM during a narrow async POST adapter (including File inputs on error),
use minimal consumed outcome markers to distinguish commit/warnings/stale recovery,
and reuse existing full-response navigation installation/context preservation on
success. Do not add a Board fragment refresh framework or shared-guard rewrite.
Add only local confirmation wiring and scoped fullscreen/feedback CSS, and correct
collaboration's open-dialog query to the owning Board page. If automatic review
rejects this different smaller architecture too, report the exact rejection and
stop edits immediately; do not retry with smaller pieces or alternate edit tools.

### Section 14 completion checkpoint — manually approved 2026-09-08

Accepted layout preserved. Existing close/fullscreen/collaboration corrections are
retained; user-approved Astra Low completed save recovery, navigation and local
confirmations. Astra High source-only review cleared five focused corrections:
preserve new-tab/modifier links, clean committed recovery, genuine backdrop gesture,
reuse failure feedback/reload controls and truthful stale-response wording. Focused
Board Node tests and syntax/diff checks pass; final Razor build had 0 warnings and
0 errors before JS-only fixes. No browser/DB or persisted concurrency claim. The
user manually approved the popup after a typography-only correction to the discard
confirmation; focused CSS source review and diff checks passed. Approval is recorded
in UI_PAGE_MATRIX. No next-family rollout without user authorization.


### Bundled Board/Teams manual corrections — authorized 2026-09-08

Preserve approved Board tile details/toolbar and inspected Teams/Draft popup
composition. Fix only the false dirty/discard prompt when untouched roster/tile
editors close and the oversized page-level Remove team X. Reuse shared guard and
existing compact removal glyph styling; preserve genuine input/file changes,
pending protection, focus and click targets. Verify the demonstrated clean/dirty
boundary and affected shared consumers, then one focused source review. No backend
or layout redesign. Review and Finalize are deferred for separately scoped page
overhauls; stop after these named corrections for user acceptance.

Correction implemented: empty upload placeholder timestamp normalization and compact
page-level team-removal glyph. Board/roster regressions and eight direct consumer
checks pass; Participant Manage fixture failure reproduces with the previous guard
and is outside this correction. Focused independent source review clears; user
manually approves both corrections 2026-09-08. No backend or markup change, browser
harness, build or DB work. Stop before further implementation without authorization.


### Participant confirmation reveal correction — authorized 2026-09-08

User approves Catalogue Add/Edit. Participant Manage Remove, Restore and Transfer
ownership reveal inline confirmations out of view. Bring newly opened confirmations
into view using the existing shared Participant binding; retain focus restoration,
input state, guards and composition. Scope is this defect, its focused executable
check (including directly required test fixture correction), and one source review.
Stop for user acceptance; Review/Finalize overhauls remain deferred.

Implemented with one scroll call in the existing shared toggle handler. All three
confirmation reveals/Cancel paths pass the focused Participant regression; its
missing document.removeEventListener mock was corrected. Syntax/diff and independent
source review clear. User manually approves Participant Manage including the reveal
correction, 2026-09-08; no build/browser/DB work.


### Event-settings confirmation follow-up — authorized 2026-09-08

User approves Schedule change/warning and Accounts ownership-transfer confirmations.
Overview signup/start/end/resume/cancel/discard confirmations receive one bounded
read-only source review because manual lifecycle-state walkthrough is impractical.
Report concrete rendered-flow/handler/recovery/feedback findings; do not mutate data
or equate source review with execution/manual acceptance. Identity timezone review
receives only auto-scroll using existing reveal wiring and compact confirmation
fonts; preserve preview values, button padding, routes and other page composition.
Use focused checks and source review for that correction. Review and Finalize stay
deferred; no further rollout or backend redesign authorized.

Source review completed with six bounded Overview lifecycle findings, recorded in
CURRENT_STATUS and the linked source report; remediation not yet authorized.
Identity correction uses existing reveal marker/helper script plus scoped typography;
helper execution/wiring/cascade/diff checks and independent source review clear.
User check pending. No broader initializer or backend changes were made.


### Overview lifecycle confirmation remediation — authorized 2026-09-08

Fix the six named source findings as one bounded pass:

- Close signup must not require opening-only warning/proposed-close acknowledgements;
  preserve existing server confirmation, permission, state and version checks.
- Failed Start/End/Resume/Cancel (and directly related confirmation failure paths)
  retain entered reasons/replacement end and confirmation context. Do not expose
  reasons in query strings or erase concurrency protection on retry.
- Pending lifecycle mutation blocks duplicate submission and dismissal/navigation
  through its confirmation controls until the result is known. Reuse existing post
  navigation state; scope changes to Overview confirmations instead of rewriting
  global navigation or adding a history framework.
- All lifecycle confirmation branches reveal/scroll and focus consistently. Cancel
  or Escape returns to the initiating control and follows the existing route;
  pending cannot be bypassed. Cancel precedes the semantic/destructive action.
- Signup outcomes set explicit correct severity. Intermediate acknowledgement steps
  are informative, concurrency/rejection is an error, successful mutation is success.

Preserve Overview composition and backend lifecycle/domain/service rules, routes,
permissions, versions, audit and persistence. Prefer local handler/markup/adapter
fixes; no new service/schema/route or general framework. Identity user acceptance
remains separate; Review/Finalize stay deferred. One focused handler/interaction
verification set plus affected compilation, then one source review against this
scope; do not run a full suite or manipulate user data/lifecycle states. Stop after
named corrections and checks, without packaging, commit, push or deployment.

Completion: all six named corrections implemented; scoped source review and three
named fixes cleared. Concurrency failure now refreshes only version binding, retains
other posted fields and explicitly requests review of fresh details before retry.
Cancel-first/focus-return completed for every scoped branch. Focused JS and compiled
Web/Razor checks pass. EventCreationUiTests has 26 passes and one unrelated stale
Participant source assertion (also absent before this pass). Final Danish notice
entry passes XML/diff checks. No real lifecycle/database handler execution or
browser proof claimed; see CURRENT_STATUS/evidence. User manually approves these
corrections. Further work is paused for budget/workflow discussion; no new dispatch.


## 15. Admin Review queue — agreed scope 2026-09-09

- Reuse established Admin implementation/styles (including Participants search/status
  controls), not screenshot approximation. Replace Review queue legacy typography,
  outlines and dividers; preserve overall table composition and information.
- User visual correction: Review table text uses weight 400, including remaining
  explicit bold table descendants; retain the separate heading/toolbar hierarchy.
- Remove the queue masthead and Administration button; shared header retains Review.
  Capitalize Submissions and use established Admin table-heading typography.
- Replace Event/Team/Tile dropdowns and Apply filters with search on the left and
  Status on the right. Search team name, credited player name and tile name;
  combine search/status and update results without page reloads.
- User follow-up 2026-09-09: Pending submissions always sort before other statuses,
  newest first within Pending and within the remaining rows. Search/Status may hide
  rows but preserve this order among visible results. Verify ordering with mixed
  statuses/timestamps using the existing focused queue test.
- Review navigation always targets the current selected event, including events
  with no evidence or unable to receive evidence. Preserve authorization and
  hidden-event boundaries. No cross-event queue through the removed filter.
- Protect status semantics, table data, Details destinations, evidence integrity,
  review actions and linked-resubmission behavior. Details presentation/workflows,
  Finalize, global Admin redesign and unrelated cleanup are outside this pass.
- Focused checks: event-scoped navigation/queue including empty/ineligible states,
  combined search/status across the three fields, no-reload updates and empty
  results; applicable Razor compilation and scoped diff checks. Reuse current tests.
- User-approved task exception: bounded source inspection and fresh independent
  review use GPT-6 Astra Low. Independent review covers backend/functionality only,
  with no UI/visual review; user owns visual acceptance. Routine UI implementation
  uses the AGENTS default GPT-5.6 Luna Max. No new dependencies, tables, services or
  generalized frameworks are planned. Stop for consequential scope uncertainty.
- No packaging, stage/commit/push, deployment, user database mutation or restart of
  the user's HTTPS 7131 application. Stop at user visual acceptance.

Section 15 checkpoint: implemented and backend/functionality-only source-reviewed
2026-09-09. Release Web build, focused authenticated HTTP event-scope test, existing
queue binding test, Node live-filter test, XML and scoped diff checks passed. One
fixture expectation was corrected without production changes. User then rejected the
visual styling: the named correction adds Review queue to the existing Admin font/token
scope and fixes desktop Status alignment; scoped cascade/diff checks pass. Pending-first
ordering is implemented with a passing mixed-row HTTP test and clear Astra Low functional
delta review. User manually approves the Review queue on 2026-09-09 after the final
table-weight-400 correction. Details and Finalize remain deferred; no user-app restart,
packaging or deployment performed.


## 16. Admin Review details — approved 2026-09-09

- Purpose: lean evidence inspection and approve/reject flow. Desktop: large uncropped
  image left with existing enlarged viewer; compact facts and decision controls right.
  Narrow screens: facts, image, then actions. Remove masthead, oversized headings,
  repeated explanatory copy and padding; compact Back to review link alone. User
  follow-up places the status badge at the right of the tile heading in the facts card.
- Facts: credited player, team as secondary context, tile, drop, submission time in
  chosen timezone and UTC, verification code or explicit disabled state. Keep tile
  requirement, submission note and eligibility warnings visible. User correction:
  remove Claimed and Contribution display only; calculations and stored values unchanged.
- Approve/Reject together below facts. Final user approval selects leaf green
  `#78B86A` for both Approve text and border, with unfilled background.
  Rejection mode hides the normal action row and approval explanation while showing
  the full-width reason and Reject/Cancel; Cancel restores normal content and focus.
  Reject reveals required reason and confirmation;
  preserve validation and recovery. Retain existing approval/rejection/reversal rules,
  confirmation protections, action eligibility and post-decision navigation.
- User visual correction 2026-09-09: remove the duplicate player/team sentence under
  the tile heading; label local submission time simply Submitted (retain local and
  UTC values). Fix effective Admin font/weights/colours and neutral borders/dividers.
  Tighten facts/actions gap to established spacing, align Approve/Reject on one line,
  and make collapsed secondary sections content-height. User rejected stretched image
  and increased sidebar gap: eliminate image/grid-driven excess height, keep facts and
  actions together with a normal fixed gap, and retain uncropped/natural responsive media.
  User follow-up: desktop image card must exactly match the combined right-card
  height including gap; the right column sets row height, the contained image must
  not enlarge it. Rejection expansion/cancel resizes naturally; mobile stays stacked.
  User rejected the taller desktop minimum experiment: restore pre-trial content-driven
  sizing without stretched facts, preserving equal-height contained image/right column
  and compact gap. Mobile remains natural.
  Team-name value uses the same value colour as other facts. Rejection reason expands
  full-width below the action row; explanatory content also spans full width, with
  only the Approve/Reject buttons sharing a row. Cancel restores compact geometry with no retained
  expansion. Verify real rendered dimensions at baseline/reveal/cancel and narrow
  width in an isolated fixture using actual styles; this is implementation regression
  verification, not independent UI review or replacement for user visual acceptance.
- Metadata correction remains collapsed. Prior evidence, evidence versions and review
  history become compact expandable sections, preserving contents and access.
- Use existing Admin font/neutral tokens and control styling, normal-weight values,
  and queue styling lessons; no blue legacy borders or page-local theme. Preserve
  approved queue layout, live filtering and Pending-first order.
- Resolve event context from the authorized submission on Details, including direct
  loads, so event selector and event sidebar remain present. Back to review targets
  that event and preserves search/status when entered from a filtered queue.
- Preserve routes, bindings, evidence viewer, immutable timestamps/assets/history,
  authorization/hidden-event boundaries, concurrency, correction/reversal/audit and
  contribution semantics. Missing image in supplied screenshot is local fixture data,
  explicitly outside scope. Finalize, unrelated pages and new review behavior deferred.
- Existing-model session choice continues: bounded source inspection and independent
  backend/functionality-only review Astra Low; routine UI implementer Luna Max. User
  owns visual acceptance; no separate UI review. No new tables/services/dependencies
  or general frameworks budgeted. One bounded source inspection before assignment.
- Focused checks: Web/Razor build; authenticated route navigation Details event/sidebar
  and queue return (direct and filtered entry, protected boundaries); required reason
  reveal/validation/recovery and retained review bindings using existing tests. Check
  localized strings, scoped cascade/diff. Do not repeat unaffected queue tests or full
  suite; add executable proof only for changed functional boundaries.
- No user database mutation, HTTPS 7131 restart, packaging, stage/commit/push or deployment.
  Stop for consequential scope uncertainty; otherwise finish checks and one functional
  review, then hand to user for visual acceptance.

Section16 checkpoint 2026-09-09: implemented; Astra Low backend/functionality source
review found no production defect. Missing authenticated journey proof was supplied
with a passing controlled PostgreSQL rendered queue/Details/rejection/Back scenario,
including direct/mismatched context and hidden/unauthorized boundaries. Release build,
focused binding tests2/2, Node queue-link/filter and mocked-DOM rejection checks, scoped
source/cascade/diff checks pass. No actual browser visual verdict; awaiting user manual
acceptance. User app was not restarted; no packaging/deployment/user database mutation.

Section16 completion: user manually approves Review Details on 2026-09-09 after named
corrections. Desktop evidence card matches the combined facts/actions height through a
contained three-row header/image/footer layout; mobile stays natural. Fact values/UTC use
heading colour; labels stay muted. Status sits beside tile heading. Approve uses the final page-approved
`#78B86A` for text/border and remains unfilled. Full-width action
copy/rejection and compact Cancel recovery retained. Later fixes were source-scoped and
manually accepted; earlier isolated geometry fixtures did not establish final correctness.
No further Review or Finalize work, packaging or deployment authorized by this approval.

Final follow-up manually approved: taller-height experiment removed; pre-trial content-driven
facts/image sizing restored. Rejection mode hides normal approval copy/buttons, retaining
reason/hint/Reject/Cancel; Cancel restores them. Final leaf-green Approve `#78B86A` accepted.
Source/diff checks and user acceptance close the pass; no further work authorized.

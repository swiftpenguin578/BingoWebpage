# Current project status

**Active handoff:** 2026-09-03. This is the concise current-state handoff;
historical material is preserved separately and is non-authoritative.

## Admin event-functionality correction handoff — 2026-09-03

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
- Manual/browser acceptance was intentionally not run. Pass 1 is cleared; the
  next permitted implementation action is Pass 2 only after user authorization.
  Nothing is staged, committed, pushed, merged, or deployed.

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

PR #5 was merged and deployed on 2026-09-01. The deployed source SHA is
`1f893133edc26455c41535807633225fdee36292`, and the immutable image digest is
`sha256:5de9882be6cd63e170b6d68fc1b869ea134e9b67f3bfab6a1b7eb042ed4c1a20`.
CI, deployment, and focused production smoke passed.

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

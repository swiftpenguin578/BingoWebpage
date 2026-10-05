# UI Page Matrix

This matrix is the sole current page approval/status authority for the active
page-family, canonical-reference, exception, and approval record. A canonical
reference demonstrates composition; it does not approve another page or the
whole regression. Current status is explicit for every row.

## New Admin design references — visually accepted, 2026-10-02

The user reports inspecting every page and accepts the visual design of the following
new Admin references: Participants, Dashboard, Events, Identity, Overview, Signup
setup, Schedule, Teams / Draft, Board, Audit, Review, Final review, WOM and Accounts,
as well as Catalogue. Reference
files are the corresponding `.dc.html` pages in the frozen in-repository
[Admin references](docs/references/admin-ui/README.md), synced unchanged from Claude's
`/Users/christopher/Documents/BingoWebpage/docs/references/admin-ui/` directory.
The full [design manifest](docs/references/admin-ui/reviews/2026-10-02/design-source.sha256)
records the consolidated file identities; [evidence provenance](docs/references/admin-ui/reviews/2026-10-02/README.md)
retains original review scope, hashes and historical wording.

Shared search-input correction complete per the user's forwarded designer handoff:
the clear X is centred, including Participants after entering search text. Claude
reports publication/sync to canvas version 42 and equal stylesheet hashes across
canvas, local and repository copies. Codex independently recorded the Documents
copy of `ui/components.css` SHA-256 as
`0334b7dd5c8683b9178c68f56a4d6164d65aa8e7b36b225d5dbf06cf6c3daba6`;
canvas equality and interaction checks are designer-reported, not rerun by Codex.
Canvas version **42**, published artifact version **1790965722-e7ad**, is the
reported final design target; its complete in-repository file manifest is frozen
in the linked checkpoint evidence. Comparison against the seven-page review manifest confirms
all 13 other captured files are unchanged; only `ui/components.css` differs, by
the added rule `.search .clear .ic{position:static;color:inherit}`. The review
snapshot therefore predates this final search correction; do not describe it as
a review of that exact final stylesheet. The active implementation worktree now
contains the unchanged final design files; its newer functionality register was preserved.
Claude's artifact design work is complete per the user. This acceptance covers
the inspected references, not future unseen changes, unresolved functional choices,
source-review defects, or the production application after integration. Technical
corrections and integration/runtime verification remain separately tracked; existing
production approvals below are not replaced by prototype acceptance.

### Scoped visual corrections approved — 2026-09-16

The user explicitly approved all four corrections after checking the restarted
preview: Stats KEEPS ON DROPPING renders no artwork or reserved artwork space for
empty/missing/broken images; Signup and Signups give the first stacked schedule
block the same left divider/inset; event-overview cards retain a trailing vertical
divider unless in the rightmost column; shared Stats navigation has the existing
Drops NEW badge styling with literal WIP text. Both Stats review findings are closed.
Technical source review: `/private/tmp/bingo-minor-ui-20260916/review.md`.
This acceptance covers these four deltas and preserves unrelated page approval states.

On detail/form pages, an information rail may appear on wide desktop. It is
removed, not relocated, at constrained widths. Full-width/table pages never
inherit the rail rule.

| Surface / route | User / primary task | Layout family | Canonical reference / components | Protected composition | Approved exceptions | Current approval state | Next gate or owner |
| --- | --- | --- | --- | --- | --- | --- | --- |
| Participant announcement / non-Admin shell, Drops NEW state | Event participant notices approved progress and opens its evidence | Shared non-modal expanded/compact banner | User-accepted `drop-announcement-prototype.html` at the absolute path in DELIVERY_PLAN, explicitly reactivated 2026-09-13 | Accepted prototype geometry/motion; existing page composition, board/submission state and evidence popup; UI_SYSTEM countdown rule | Thin coral countdown independently of green completion; minimal NEW controls; retain current blue/no-background X hover as explicit 2026-09-13 exception | Prototype restoration implemented; awaiting user visual approval — 2026-09-13 | Scoped prototype/source-cascade comparison and named fixes clear; focused client checks and Web build pass. Opaque surface/coral edge outside Board restored; functional additions and stacking retained. Ordinary agent preflight waived; wider Drops redesign outside scope |
| Admin shell / `/Admin/*` | Admin navigates event and global work | Admin shell | `_AdminLayout.cshtml`; Admin shell CSS; `site.js` menu | Header/context, event selector, sidebar, drawer, scrim, focus order | Compact operational charcoal shell | Approved | Preserve baseline; whole-app regression later |
| Opt-in Admin reference shell / U1 Identity | Admin navigates global and event work | Shared reference shell | `_AdminDesignLayout`, new sidebar/topbar, frozen tokens/components | Theme, language switch, eligible event switcher, menus, focus/layers, dirty guard and in-page navigation | Existing routes; full-load fallback for unbound pages; manual theme preference; no sidebar counts, blocker chip or page description; U-B bell count | U1 review49 failed; brief50 remediation implemented, awaiting Claude named recheck and user visual acceptance — 2026-10-05 | Claude independent review and user light/dark, EN/DA, phone-width acceptance; legacy shell remains for unbound pages |
| Public UI foundation catalogue / `/Admin/PublicUi` | Admin inspects the retained historical public specimen | Internal design-system catalogue | `Admin/PublicUi.cshtml`; legacy specimen markup | Direct-link only; no Admin navigation entry | Not an approval prerequisite for the replacement Public UI; Admin route is outside this experiment | Historical specimen retained; replacement identity supersedes it for product pages | Preserve unchanged; no Admin work |
| UI reference gallery / `/Admin/UiReferences` | Admin inspects retained historical UI reference assets | Internal reference gallery | `Admin/UiReferences.cshtml`; bounded reference-image handler | Direct-link only; no Admin navigation entry or product workflow | Historical reference material only; not a product-page implementation, approval target, or authority source | Approved to retain — user decision, 2026-08-27 | Preserve as an internal read-only reference route; do not use it to reopen approved pages |
| Event Create / `/Admin/Events/Create` | Admin creates an event | Detail/form | `Create.cshtml`; event-create fields/actions | Five-step route-backed creation flow and validation orientation | Date-time picker and progressive enhancement | Approved | Teams/Draft owner keeps navigation compatible |
| Identity / `/Admin/Events/Identity/{id}` | Admin edits public event identity | Reference form card, no rail | Frozen `Identity.dc.html`; `_AdminDesignLayout`; `Identity.cshtml`; shared form/banner/layer components | Public details, timezone, permanent Signups link, save bar, field conflicts and Check again | A-Identity-1 unchanged legacy name; A-Identity-2 scheduled timezone moments; U-F unset summary; A14 versioned outcomes; D17 terminal read-only | U1 review49 failed; brief50 remediation implemented, awaiting Claude named recheck and user visual acceptance — 2026-10-05 | Claude independent review; user checks light/dark, EN/DA, phone width, confirmations/errors/toasts; final whole .NET suite by user/Claude |
| Schedule / `/Admin/Events/Schedule/{id}` | Admin configures dates, automatic opening status, and warnings | Detail/form + optional rail | `Schedule.cshtml`; `.schedule-editor-layout` | Schedule groups, honest read-only boundaries, no capacity editor or opening toggle, combined change/warning confirmation, combined date-time controls | Rail is wide-only and page-local | Approved — composition retained; schedule change/warning confirmation manually approved 2026-09-08 | Preserve accepted schedule confirmation; capacity belongs to Signup setup and automatic opening uses the configured timestamp; separate Overview lifecycle source review does not expand Schedule scope |
| Manage/Overview / `/Admin/Events/Manage/{id}` | Admin reads readiness and runs lifecycle actions; SuperAdmin inspects/restores a hidden event | Detail/form + optional rail | `Manage.cshtml`; `.event-overview-section`, `.event-overview-row` | Operational summary, readiness rows, dates, controls, lifecycle actions; limited retained-lifecycle/quarantine-audit inspection and Restore only when reached from the separated Hidden area; managed WOM preview/Create/update/delete controls reuse this section and remain scoped to the managed source | Information rail is a page-local owner; hidden events expose no ordinary workspace or mutation | Existing composition approved; managed WOM addition awaiting user visual acceptance — 2026-09-22 | Technical source/build checks clear; manually inspect EN/DA empty-team, pending/unknown, Live lock and confirmed pre-Live delete states. Preserve existing manual-link, cache and lifecycle boundaries |
| Events directory / `/Admin/Events/Index` | Admin finds an event and opens its workspace; SuperAdmin separates hidden events for limited control | Full-width data/table | `Events/Index.cshtml`; directory toolbar/table CSS | Search, state filter, sortable table, empty state, Workspace action; clearly separated SuperAdmin-only Hidden filter/area with Restore/limited Manage destination | `1100px` label/value cards; hidden area is not ordinary Admin visibility or a public bypass | Approved | Retain as full-width table reference; preserve the scoped Hidden area |
| Participants / `/Admin/Events/Participants/{id}` | Admin manages signup settings and participants | Full-width data/table + accepted detail dialog | `Participants.cshtml`, `Participant.cshtml`; participant table/dialog classes | Compact settings, search/status controls, current/history groups, route fallback | Accepted participant-detail dialog states; page-local table markup | Approved — Manage and Add Participant user manual approval, 2026-09-08; C11 changes awaiting manual acceptance | Manage Remove/Restore/Transfer ownership auto-scroll correction accepted; focused regression/source review clear. Preserve guards, layout and separate page approvals C11 finalized-pre-Live departure, optional note, vacancy/replacement and follow-up states await user manual acceptance; existing composition approval remains. |
| Signup questions / `/Admin/Events/Questions/{id}` | Admin edits the standard and custom questions shown on an event's public signup form | Detail/form + route dialog | `Questions.cshtml`; `_AdminLayout.cshtml`; `signup-questions-overlay.js` | Real route, Participants/signup-form dialog enhancement, add/edit/remove/reorder controls, compact confirmation, focus/history path | This route does not own CSV import; its ordinary presentation is the popup launched inside Participants | Approved — user manual acceptance of popup pilot and follow-up, 2026-09-08 | Scoped lifecycle/confirmation/typography, code-settings and fullscreen-responsive pilot source/browser/visual review clear; preserve accepted behavior and backend semantics; next-family rollout requires authorization |
| Catalogue / `/Admin/Catalogue/Index` | Admin manages catalogue activities and drops | Full-width directory + route editor | `Catalogue/Index.cshtml`; `catalogue-admin.js` | Directory search, Add, editor dialog/route, drop sections | Specialized activity/drop composition; shared popup guards and inline confirmations | Existing Add/Edit approved 2026-09-08; Stats Pass 1 API sections and action outcomes awaiting manual acceptance, 2026-09-15 | Existing composition approval retained. Catalogue mapping/pricing implementation and independent source review passed, including F1–F3 recheck; expanded API panels and their new success/fallback/error outcomes, including Pass 2 suspicious-price feedback, require user visual acceptance. |
| Accounts/Roles / `/Admin/Accounts/*` | Admin manages accounts and emergency credentials | Full-width data/table + detail/form/dialog | `Accounts/Index.cshtml`, Create, Manage, Transfer; `account-manage-dialog.js` | Separate datasets, role/search controls, route-backed forms, strong confirmations | Create/Manage all-width modal with inline confirmations; deliberate standalone routes retained | Approved — Create/Manage and ownership-transfer confirmation user manual approval, 2026-09-08 | Existing composition and security behavior preserved; shared guards, parent freshness and scope recovery retained |
| Board / `/Admin/Events/Board/{id}` | Admin builds, approves, previews, and publishes a board | Workspace/canvas | `Events/Board.cshtml`; `.board-page` CSS and inline board script | Toolbar, canvas/sidebar, tile dialogs, collaboration, preview, lifecycle states | Board-local divider ownership; drag handle + `.drop-target` swap boundary only. `/Admin/Events/{id}/Preview/{teamSlug?}/{tileId?}` mirrors the public Board ecosystem, is not independently owned by this approved Admin page family, and inherits the public family's awaiting structural-remediation status; it has no independent visual overhaul or approval. | Approved — Create/Edit tile, existing tile details/removal and inspected toolbar popovers/confirmations, 2026-09-08; C20 guidance, evidence-lock and Discard private correction confirmation/feedback Awaiting manual acceptance; managed-artwork recovery remains a C20/C21 integration obligation. | Accepted composition/flow retained; false untouched-editor discard corrected; focused checks/source review clear; user approved correction 2026-09-08 |
| Teams/Draft / `/Admin/Events/Draft/{id}` | Admin forms teams and runs the snake draft | Workspace/canvas | Existing event workspace shell and Draft route | Team roster, Captain/co-captain distinction, draft controls, finalization/publish readiness, and advanced pre-formed-roster CSV preview/apply flow | The CSV importer is an advanced state inside this approved workspace, not a separate Questions page. No new public board or alternate draft model. User explicitly retains the small positioned roster-removal confirmation (2026-09-08) | Approved — Add team, inspected roster/member/move and draft confirmation surfaces and named corrections, 2026-09-08; C11 changes awaiting manual acceptance | Preserve small positioned roster confirmation; page-level Remove team X and false untouched-roster discard corrected; focused checks/source review clear; user approved correction 2026-09-08. Other approval scope unchanged C11 finalized-pre-Live participant action and Captain-recovery publication states await user manual acceptance; existing composition approval remains. |
| Canonical submission workspace / `/Submissions`, `/Submissions/{id:guid}` | Authenticated current team members inspect the complete retained team ledger; Captains/co-captains and valid enabled emergency authority also coordinate focus and manage eligible team submissions | Public/participant operations + detail | PUB-REF-17 and Functional contracts 7.2–7.3 | One canonical overview/detail implementation: Captain-only team focus and team submission status sections above the complete ledger; status/player/tile filters; detail and reviewer feedback; owner-only participant mutation; broader server-authorized Captain/co-captain/emergency mutation of eligible team submissions; read-only retained states | Team-wide current-member scope includes departed credited members. Ordinary participants do not see the two Captain-only sections and may edit only their own eligible non-read-only submissions. The detail composition remains visually equivalent to the approved Captain submission detail. `/Captain` and `/Captain/Submissions/{id:guid}` are thin compatibility redirects/aliases only; they must resolve through canonical server authorization and never render separate implementations. `/Captain/Submit/{tileId?}` remains drawer transport/handler plus compatibility redirect, not a rendered or no-JavaScript page. No Admin review controls. Personal submission/evidence notifications resolve to `/Submissions/{id}`; relevant general submission navigation resolves to `/Submissions`; Admin review notifications remain `/Admin/Review/Details/{id}`. The relative `_EvidenceUpload` partial reference that could make `/Submissions/{id:guid}` 500 is fixed and covered by acceptance. Existing linked-resubmission Admin Review behavior remains protected and was confirmed by source/tests; no query change is authorized. | Approved — user manual acceptance, 2026-08-31; independent review and bounded remediation complete; accepted through local whole-application regression on 2026-09-01; C20 private-correction active-publication behavior Awaiting manual acceptance. | Preserve accepted visual language and behavior; preserve the completed canonical routing/authorization correction through separately authorized push/green CI/candidate publication |
| Admin evidence review / `/Admin/Review/*` | Admin reviews, resolves, and corrects evidence | Full-width queue/detail | Current review routes and evidence detail markup | Queue context, evidence inspection, approve/reject/reverse, immutable timestamps | Protected `/Evidence/{id}` is a file-download handler consumed by evidence views/lightboxes, not a rendered page or visual family. Lightbox and correction confirmations remain bounded | Approved — Review queue and Details including final follow-up, user acceptance 2026-09-09; C33 concurrent-review error/retry state Awaiting manual acceptance | DELIVERY_PLAN sections15–16 complete. Preserve accepted queue filtering/Pending-first order and Details evidence/facts/actions composition, matching-height contained image, full-width rejection/Cancel flow, primary-colour values, heading status badge and page-approved `#78B86A` outlined/text Approve (unfilled), compact rejection-only mode and restored pre-trial sizing. Submission-derived event context/filter-preserving Back and protected review semantics verified. Earlier isolated geometry fixtures were superseded by user corrections and final manual acceptance |
| Finalize/closeout / `/Admin/Events/Finalize/{id}` | Admin resolves final-review blockers, publishes official results (which atomically archives the event), and reopens corrections | Detail/form + full-width placements ledger | Current Finalize route; approved Admin detail/form and table components | Blockers, review-cycle/version concurrency, immutable official placement snapshots and retained history; no placement-correction control | Accounts/Roles approval does not approve closeout | Approved — user visual acceptance of the Final review reference, 2026-10-02; C33 freshness/reinspection/error states Awaiting manual acceptance | Preserve the accepted Final review composition and finalization/reopen boundaries; there is no separate Archive action or placement-correction flow, and application integration remains deferred |
| Audit / `/Admin/Audit/*` | Admin filters and inspects administrative history | Full-width data/table | Current Audit route; approved Admin directory/table components | Audit ordering, filters, structured before/after context, ownership and authorization protections | Accounts/Roles and Finalize approval do not approve Audit | Deployment ready, not approved — user decision, 2026-08-26 | Preserve deployment-ready implementation; manual approval remains deferred |
| Public landing / `/` | Visitor discovers current and previous events and follows the correct destination | Editorial landing | Approved target: `docs/references/public-ui/pub-ref-01-landing.png`; accepted implementation evidence under `tmp/public-ui-pass1a/` | Blue masthead; open diagonal hero and DK artwork; Barlow Condensed ExtraBold 800 display/numerals, Barlow Condensed SemiBold 600 utilities, Geist body; target-matched 32×32 icons; strong section rules, inset dividers, compact current ledger and distinct archive rows; semantic coral/sage/bronze tones; token-only dark composition | Preserve PageModel, real dynamic event counts/facts/data/destinations, localization, routes, CTA semantics, accepted composition/typography/palette/copy; no other family or behavior change. The standalone `/Account/Login` route is the only runtime login surface: anonymous signup links navigate there with their validated local `ReturnUrl`, and no login popup/dialog is retained. The 2026-08-23 masthead correction reuses the approved Login logo SVG variants and percentage-painted diagonal field, adds the target bottom divider, and hides artwork when the hero stacks. | Approved — user manual acceptance retained after masthead correction, 2026-08-23 | Preserve approved Landing; remaining Pass 1B pages resume only at the next user-authorized gate |
| Public signup / `/Events/{slug}/Signup`, `/Events/{slug}/Confirmation` | Visitor signs up, edits through the Signup route state, and reads the outcome | Public/participant surface | PUB-REF-05 and PUB-REF-06; existing route/forms. The TEST 16 PDF recorded in DELIVERY_PLAN is rejected current evidence, never a target | Reference-owned wide horizontal event masthead with real capacity/waiting status, three compact ruled form rows, one persistent right summary rail, deliberate bottom action row, and distinct confirmation outcomes; waiting list, edit/read-only boundary, light/dark geometry parity | The current repository has no separate EditSignup Razor page; Signup owns create/edit states. Preserve bindings/behavior, not either rejected DOM, width, container, or flow. Use the global accepted Landing header from `_Layout` | Approved — Signup and Confirmation user manual acceptance, 2026-08-22 | Preserve approved Signup and Confirmation; remaining Pass 1B pages resume only after the current stop gate |
| Public Signups directory / `/Events/{slug}/Signups` | Visitor reads confirmed and waiting participants | Public/participant directory | PUB-REF-13; current Signups route | Counts, capacity/status summary, privacy-safe participant facts, phase-safe groups, empty states and narrow rows | Exact-link/Discord-link directory; no sibling navigation added from the reference | Approved — user manual acceptance, 2026-08-24 | Preserve approved directory, responsive rows, and copied signup-masthead behavior |
| Public Teams/roster / `/Events/{slug}/Teams` | Visitor reads final teams, member roles, event timing, and draft order | Public/participant roster | PUB-REF-16; existing Teams route and authoritative roster/draft projections | Left title/back plus compact Event starts/Event ends/Players metadata row, where Players is the frozen published-roster count; full-bleed Landing-family diagonal DK artwork on the right with one masthead-bottom rule beneath text and artwork; because this masthead is content-height rather than Landing-fixed, its diagonal, stripe, mark size, and crop shrink together from the actual masthead height but are capped at Landing's live viewport geometry; keep the separate Final teams heading-owned rule; three-column final-team roster without team images or inter-team divider grid; each team sublabel shows formation type only, not affiliation; each published roster orders Captain first, Co-captain second, then participants by effective draft-pick number; Captain and Co-captain use semantic text color without icons; two-picks-per-row draft results on large widths and one-per-row when constrained | Before Board publication this remains the standalone roster destination selected by public event routing and exposes no links into the unavailable Board family. After Board publication the same route joins the shared context row as localized Teams/Hold beside Boards/Drops/Leaderboards, marks Teams current, and uses the shared reduced navigation-to-masthead gap. Do not copy the Board overview masthead or change any other approved Teams composition, facts, ordering, privacy, route, theme, or responsive behavior. Development reset gives finalized positive fixtures active frozen publication snapshots; `test-98-missing-playing-assignment` remains the intentional unpublished negative. | Approved page composition; bounded lifecycle-aware navigation addition authorized 2026-09-06; C11 changes awaiting manual acceptance | Preserve the approved Teams body and independently constrained masthead geometry; add only the conditional shared navigation and spacing C11 current roster, retained pick display and empty-team vacancy state await user manual acceptance; existing composition approval remains. |
| Public board/evidence / `/Events/{slug}/Board`, `/Events/{slug}/Board/{teamSlug}`, `/Events/{slug}/Board/{teamSlug}/Tiles/{tileId}`, `/Admin/Events/{id}/Preview/{teamSlug?}/{tileId?}` | Visitor compares teams, progress, and approved evidence; Admin previews the same public ecosystem | Public/participant workspace | Reactivated PUB-REF-02, PUB-REF-03, PUB-REF-04, PUB-REF-14, and PUB-REF-15; existing authoritative Board/TeamBoard/Tile routes and projections | Structural rewrite: reference-owned event tabs and masthead using the shared Wide page width and normal responsive gutter; preserve the current near-target team-overview grid beneath the masthead and add the missing PUB-REF-02 Recent Activity footer; compact content-driven fact/action rail with tight countdown, leader, selected metric, and action cluster; ordinary full-page TeamBoard with summary header, statistics rail, dominant tile grid, View all teams, and adjacent-team navigation; tile routes replace the left rail; submission remains its attached drawer; evidence remains a focused modal viewer. Preserve authoritative data, route/history, focus, authorization, responsive, and realtime behavior. Protected `/Evidence/{id}` remains only the file-download dependency for evidence images/lightboxes. | The current screenshots are rejection evidence, not targets except that the user explicitly protected the overview grid beneath the masthead as already close to target. The prior popup decision is superseded: ordinary team-card clicks navigate to the TeamBoard page at every viewport. Dark is token-only and keeps outlined tile numbers. Admin Preview inherits this family's redesign status and is not an independent visual pass. A reskin of the rejected masthead or team workspace is explicitly non-conforming. Board overview uses shared Wide with the normal shared gutter; this does not authorize Landing hero height or artwork. Once published, Board, TeamBoard, and nested tile routes receive shared Boards/Drops/Leaderboards/Teams navigation; Teams/Hold targets the unchanged public roster route. The tile sidebar retains its route-backed/enhanced Team overview action and the normal View all teams route remains in the masthead. | Approved Board family; bounded Teams/Hold navigation and accessible evidence zoom/pan additions authorized 2026-09-06 | Preserve the approved Board/evidence composition and behavior outside those additions |
| Authentication and errors / Login, Onboarding, AccessDenied, Error, StatusCode | User signs in and understands access or application failures | Editorial authentication/status compositions | PUB-REF-09; shared shell/focus rules | Standalone Login split, Onboarding steps, and shared numbered status composition | Discord completion remains an immediate non-rendering redirect | Approved — user manual acceptance, 2026-08-23 | Preserve approved page bodies, routes and behavior |
| Account settings and overview / `/Account/Settings`, `/Account/MyAccounts`, `/Account/MyEvents` | User manages identity and linked characters and revisits events | Standard settings + account overview | PUB-REF-07 and PUB-REF-10 | Approved forms, ordered-character behavior, account/event rows and account secondary navigation | My Accounts and My Events use their broader reference-owned compositions | Approved — page-specific user manual acceptance by 2026-08-24 | Preserve approved pages and shared navigation |
| Account security and recovery / `/Account/ChangePassword`, `/Account/ForgotPassword`, `/Account/ResetPassword`, `/Account/Setup` | User changes, recovers, or establishes credentials | Narrow account form | PUB-REF-08 and the approved Settings family | Existing password/security rules, validation, safe return, setup authority and handlers | Setup remains operator/security scoped; no password-policy change | Approved — page-specific user manual acceptance completed by 2026-08-26 | Preserve approved security, recovery, and Setup pages |
| Notifications / `/Notifications` | User reads and manages personal notifications | Public inbox | PUB-REF-11; notification inbox behavior | Read/unread state, semantic status, timestamps, wrapping, mark-all-read, empty state and destinations | Personal read state remains independent | Approved — user manual acceptance, 2026-08-24 | Preserve approved page and behavior |
| Privacy / `/Privacy` | User reads authoritative privacy information | Guidance/editorial | PUB-REF-12 real-content composition | Authoritative privacy meaning and real content | Static hierarchy may adapt without changing legal meaning | Approved — user manual acceptance, 2026-08-24 | Preserve approved page and authoritative copy |
| How To / `/HowTo` | User follows a five-step guide from finding an event to tracking evidence review | Guidance/editorial | Reactivated PUB-REF-12 Guidance and editorial reference | Five stable hash-linked steps, left step rail, active article, right progress/help rail, localized evidence schematic and complete-client example image; narrow layout uses compact horizontal step navigation and active content | Preserve the five-step content contract, ordinary anchor fallback, page-local hash enhancement, existing public shell/tokens, internal destinations, and evidence/privacy wording; no Rules editor, Admin documentation, sixth step, or standalone Captain Submit page | Approved — user manual acceptance, 2026-09-01 | Preserve the approved interaction and content contract |
| Dashboard/action inbox / `/Admin` and notification destinations | Admin enters the operational workspace | Admin shell + temporary content surface | Shell-owned WIP surface; existing dashboard markup retained inertly | Admin authorization, shell navigation, notification owner and recoverable dashboard source | `/Admin` intentionally renders only the Admin-background `WIP` content surface | Deployment ready — intentional WIP presentation, user decision, 2026-08-24 | Preserve until the dashboard is explicitly resumed |
| Whole-application regression / all routes and states | Release owner verifies the complete application | All families | `UI_SYSTEM.md` plus this matrix and manual/release gates | Desktop/mobile, keyboard, permissions, errors, realtime, privacy, functional regression | No page approval substitutes for this gate | Complete — manual regression accepted by the user after sustained site use; automated local regression accepted after remediation on 2026-09-01 | Separately authorized push/green CI/candidate publication; the planned Admin test event remains the real-world follow-up safety net |

GP display correction authorized 2026-09-16: in Stats, amounts currently rendered
in M below one million use K instead. Preserve existing raw-GP formatting below
one thousand and existing M/B formatting at higher values. No styling change.

## Tile KC/Luck sidebar addition — authorized 2026-09-16

Reference Luck fixture visually accepted by the user on 2026-09-16 ("yeah it looks
correct"): `stats-manual-luck-251-502`, one 1/251 outcome, seven teams at 502 KC
with 0–6 drops. This records acceptance of the displayed reference scores only.

Shared Luck calculation replacement authorized 2026-09-16: Stats and tile Luck
use the bounded probability-based score in PRODUCT_REQUIREMENTS.md. This changes
values only; existing typography, rows, colors, placement and disclosure remain
unchanged. No new subjective UI review or browser inspection is authorized.

Stale notice correction authorized 2026-09-16: show it only when stale, as compact
muted metadata immediately below the section description. Display the retained
timestamp in Europe/Copenhagen local time, including daylight saving, with no
visible timezone label. Preserve all summary and contributor styling.

Contributor filtering authorized 2026-09-16: display only known positive-KC rows per
boss/mode; omit empty groups/disclosure. Accepted styles and order stay unchanged.
This is a functional display filter; no new subjective UI acceptance gate.

**Latest correction:** initial styling was rejected; direct shared EHB/Drop EHB row
reuse is now implemented. Summary TEAM TOTAL/Luck/green value; contributor
Luck/name/green value, with no sign, KC suffix or extra Luck label. Values below 50
are coral and values at or above 50 are green. Multiple metric totals use group
headings outside rows.
Fresh focused source review, Razor build and formatting checks passed; live screenshot
and disclosure behavior checked. **Styling approved by user, 2026-09-16** ("Looks great"). User requested only
section ordering: Eligible drops above KC & Luck, all styling unchanged. Bounded
reorder is implemented and verified by source recheck, Razor build and live route:
Eligible drops → KC & Luck → Approved submissions. Styling is unchanged; no renewed
styling approval required.

Existing Board-family approval is retained. The new tile KC/Luck section is implemented
and independently source-reviewed, including its named CSS correction. Sixteen focused
PostgreSQL/HTTP cases passed; CUA verified contributor click/keyboard, tile switching/Back
and desktop/mobile spacing. **On 4 October 2026 the user confirmed that they visually inspected the tile
Luck section on 2 October and it looked correct. This is visual acceptance only;
functional correctness rests on automated tests.** This confirmation is separate
from the Stats-demo approval text; production binding remains separately tracked. Use the current
TeamBoard EHB/Drop EHB summary and expandable Contributors markup as the reference;
the user-supplied screenshot is
`/var/folders/w5/74mg_d917xg33ry8_4qc9g5w0000gn/T/codex-clipboard-8f0e7315-71ec-4cfa-a114-fb4efe6abf9b.png`.
Team summary shows Team total/Luck/KC; expanded rows show Luck/player name/KC.
Multiple relevant bosses/modes have separately labelled KC values. Preserve the
existing type, separators, colors, row rhythm and responsive sidebar. Changes are
scoped to the new section; initial and enhanced nested tile views share it. The
standalone Tile scaffold stays inactive; no new route is introduced.
DELIVERY_PLAN.md section 18 defines the bounded functional checks and stop boundary.

## Stats prototype UI approved — 2026-09-15

### Luck mode contract — active implementation authority, 2026-10-01

The existing Stats Luck container remains the approved composition. Its default
mode is Luck % on a fixed 0–100 scale; an in-container toggle selects KC
difference. The two modes share the saved snapshot, timestamps, team/player
comparison, search, pinning and drill-down. Sort and extreme selection use the
active mode's unrounded value; presentation rounds to at most one decimal. KC is
signed and zero-centred, while Luck has no signed or zero-as-expected centre.

Tile views retain one aggregate result and show separate boss/activity results;
contributor rows use the matching activity result. Missing or unsupported results
remain unavailable; calculable estimates and recorded zero retain meaningful labels.
Ordinary age or evidence changes do not invalidate a compatible saved result;
quiet Last updated metadata describes its age. Shared Stats/tile help uses
localized plain language and includes “KC totals don’t account for differences in
boss kill speed.” This is a behavior/data binding correction within the existing
page family; it does not approve a new container, boss selector, EHB mode or a
broader visual redesign. The Luck redesign is technically complete and independently
reviewed. **Manually approved by the user, 2026-10-02:** “Luck is approved”, after
inspection of the populated real Stats demo with Luck % and KC difference for teams
and players. This closes the new Luck binding visual-acceptance gate; existing
composition approval is retained. It does not grant approval to unrelated pages
or constitute merge, deployment or production-data verification.

User approved the complete Stats UI after the section-by-section responsiveness pass:
"I think thats the UI approved." This final manual acceptance supersedes earlier
pending Stats visual/trial statuses. The accepted artifact is
`prototypes/stats/outputs/stats-page-prototype.html`, with `stats-density.css` and
`stats-prototype.js`; preview http://localhost:8766/stats-page-prototype.html.

### Stats production port contract — explicit user requirement, 2026-09-15

**Acceptance timing — user decision, 2026-09-15:** After all five Stats passes are
implemented, prepare one consolidated user-facing visual-acceptance checklist. Include
the connected Stats page and all new catalogue API controls, price warnings and action
outcomes from earlier passes. Earlier visual acceptance remains deferred, not implicitly
approved, and does not block implementation of later passes. Existing prototype design
approval remains authoritative; final review verifies its faithful production port.
Use the existing Stats section in MANUAL_TEST_CHECKLIST.md as the checklist owner.

The approved Stats implementation must be carried into production, not recreated from
visual reference. Port the actual Stats section markup, `stats-density.css`, relevant
styles embedded in `stats-page-prototype.html`, and the rendering/interaction/animation
code in `stats-prototype.js`, including required assets. These files are implementation
inputs. A visually similar replacement or a fresh implementation from screenshots does
not satisfy acceptance.

Preserve section structure, typography, spacing, chart geometry, responsive breakpoints,
minimum/maximum heights, overflow, transitions/timing, hover/focus, drilldowns, search,
comparison rows and artwork behavior. Adapt fixture data to real DTOs and connect
persistence/authorization; allow necessary Razor, localization and scoped CSS integration
without silently changing the approved appearance or interactions. Any necessary visible
or interaction deviation must be identified and agreed before changing it.

Integration resolution, manually approved 2026-09-23: the app header retains its
shell, while Stats renders the actual shared Board/Drops/Leaderboards event masthead,
including lifecycle status, result, player metric selector and conditional actions.
The masthead and Stats content align to the approved 1640px border-box shell with
3.6% inline padding (5% at ≤850px), without an inherited 88rem cap or doubled gutters.
Other pages and Stats body behavior are unchanged. The earlier structure-only
masthead with Stats-specific dates/player count was rejected and is superseded.
The user approved the corrected running result (“everything approved”); independent
UI review for this correction was explicitly skipped by the user, not passed.

Explicit exclusions: do not port the prototype header or masthead; use the application's
existing shell. Remove the prototype footer/demo strip, sample labels/dates, event-size,
milestone-stage, drop-preview and standalone theme controls. **Adjust artwork is the only
existing bottom control retained**, with its approved editor and Super Admin authorization.
This exclusion does not remove in-section tabs, searches, filters or help controls. The
separately agreed saved guidance preference does not authorize retaining the demo toolbar.

The implementation handoff must map each approved source section/style/script to its
production destination and list integration-only changes. Review that mapping and actual
code for omitted behavior or broad rewrites, then obtain production visual acceptance
against the approved UI. Passing data tests or merely linking the prototype as a reference
is insufficient evidence of a faithful port.

### Approved real-data adaptation: Luck scale — 2026-09-15

The user approved expanding the shared Luck bar scale when real percentages exceed the
prototype's ±60% range. Use max(60, largest absolute score in the current view, including
its comparison); use the full current view rather than only the rows visible while
scrolling. Preserve proportional lengths, exact percentage labels, row heights and animation behavior.
The screenshot correction below reserves full numeric labels at both edges: retain the
40% half-track extent where it fits, otherwise use one smaller shared available span for
the entire view. Scrolling alone must not rescale the bars. Missing/unavailable results retain honest status text rather than a
fabricated zero. This bounded adaptation does not authorize other UI redesign.

### Production implementation handoff — 2026-09-15

The six approved sections are ported to `Pages/Events/Stats.cshtml`, with original scoped
styles in `stats-base.css` / `stats-density.css`, original renderer code in `stats-page.js`,
and real-input adaptation in `stats-adapter.js`. Source mapping, exact baseline hashes,
executable evidence and limitations are in CURRENT_STATUS.md. Saved guidance and Super
Admin artwork controls are implemented. Independent production source review and the
subsequent UI1–UI3 fidelity-correction recheck passed. The user accepted the production
UI on 2026-09-16 as recorded below. The consolidated checklist is in
MANUAL_TEST_CHECKLIST.md.
The original prototype approval is unchanged; passing stand-ins do not approve visual fidelity.

### Production Stats UI acceptance — 2026-09-16

User: “UI looks fine so i guess 1-6 is passed unless i discover something i missed”.
This accepts sections 1–6 of the chat's concise checklist: layout/appearance, Drop value,
Luck, Event milestones, Board progress/highlights, and guidance/artwork. It does not mark
sections 1–6 of the longer repository checklist as executed. Newly discovered issues may
be reported later. Stats timestamps now use 24-hour time while retaining the event timezone.

Known exception: the user reported the sticky Luck comparison's top gap still present
and chose to leave it; do not describe the attempted correction as visually successful.
The user subsequently passed functional walkthrough steps 1–9, including sign-in, saved
guidance, artwork persistence/permissions and conflicting saves; MANUAL_TEST_CHECKLIST.md
records the full manual scope. The earlier Safari/localhost cookie cause was not independently
diagnosed. The user subsequently accepted the prepared step-10 pricing/Luck cases. A new display
limitation remains: small GP totals (for example 4,000) are rounded in millions and cannot
be read accurately. No formatting correction is claimed. Live-provider checks and the
finalization/correction/archive walkthrough subsequently completed on 2026-09-16;
MANUAL_TEST_CHECKLIST.md records their distinct functional scope. Existing UI exceptions
remain unchanged. Use 127.0.0.1:5189 for the local test app; localhost login still failed
for the user during step 12, and no authentication-code correction is claimed.

### Production responsive content corrections — accepted with known exception, 2026-09-16

The user's five screenshots identify Luck values overlapping names, Board progress names
wrapping across rows, a compact donut total crossing its ring, and valuable drops cramped
or overflowing at narrow card widths. Bounded corrections preserve the approved wide
composition: reserve Luck label space with proportional bars, ellipsize long Board progress
names with full-name access, fit donut text to its hole, and use stacked valuable-drop rows
when the GP card is narrow. The user then said the strip stacked too early: use a 650px
actual GP-card threshold, retaining three columns longer than the proposed 760px switch.
Existing animation and container-query fixes stay intact. These corrections and the
threshold decision are included in the production UI acceptance below.

### Drop value exploration and polish approved — 2026-09-15

Subsequent user-requested correction removes the pinned contributor row’s left inset
accent; its background highlight remains. Scoped CSS/diff checks passed.

User manual acceptance: "Yep i think this section is done." This approves the final
Drop value prototype, including drill-down, player comparison, search, help, responsive
containment, colors and motion, and supersedes earlier pending trial statuses.

- Teams opens scoped top-five player charts and all-contributor breakdowns, team totals
  and valuable drops; All teams returns. Everyone retains its aggregate view.
- Global Players provides top five plus one hover/focus/pinned comparison and an Others
  breakdown. Search exists only here, inside the divider or a narrow header popover.
  Search-result selection retains its pin and page position; legend controls keep focus.
- Guidance starts expanded, dismisses with ×, and reopens with Info. Existing proportions
  remain; longer lists scroll within the held chart area during view changes.
- Initial chart draw and clockwise donut reveal, scope morphs, comparison entry/exit,
  pin pulse, search and help transitions are accepted. Reduced motion shows final states.
  Slice clicks use highlighting; keyboard focus follows the slice without a rectangle.
- Others uses a lighter grey in light mode. Most valuable drops updates immediately
  when switching scope; only its artwork lifts on hover. No row change animation.

Focused syntax, source and executable DOM/animation stand-in checks passed for the
implemented interactions, motion lifecycle, viewport/focus handling, fixtures and sample
sizes. User acceptance supplies visual approval; these checks do not establish production
integration or persistence. Luck, Keeps on dropping, Most versatile and Board progress
remain locked. Event timeline content is the remaining discussion; production work is deferred.

### Luck exploration and polish approved — 2026-09-15

User manual acceptance: "I think thats luck done." This approves the final Luck
prototype and supersedes earlier pending Luck trial/visual statuses.

- Teams shows all teams; a team name/bar opens its complete player roster with back
  navigation and a transitioning team label beside Luck. The header has no divider.
- Players shows five unluckiest and five luckiest with a gap; ten or fewer players
  appear once. Search by player/team uses the compact Drop value-style popover beside
  Players, with no input focus outline or redundant guidance below the field.
- Existing search results highlight and smoothly scroll fully into view. A player
  outside the extremes gets one replaceable sticky comparison above the rankings.
  The comparison has no tinted background; its × is centered beside the player name.
  Entry expands/fades, removal collapses/fades, replacement fades. The card stays steady.
- Bars grow from Expected while percentages count from zero and follow their ends.
  Motion respects reduced-motion preferences and handles interrupted interactions.
- ⓘ explains team exploration and player comparison using the agreed text. Guidance
  opens on every page load; ×/Escape dismisses for that visit and ⓘ reopens it.
  The shared Hide tooltips control and account persistence are implemented in the
  production port; the baseline prototype remains unchanged. Production acceptance is pending.

Focused syntax, source and executable DOM/animation stand-in checks passed for roster
navigation, extremes, search/pin/remove, scrolling, motion lifecycle, reduced motion and
help. User acceptance supplies visual approval. Percentages remain illustrative;
calculation and production integration are deferred. Drop value, Luck, Keeps on dropping,
Most versatile and Board progress are locked. Event timeline trial is described below.

### Event milestones approved — 2026-09-15

User manual acceptance: "much better i think event milestone is done too." This approves
Event milestones and its final animation pacing, superseding earlier pending milestone
trial/visual statuses. The latest shared pacing pass is accepted: initial Drop value
chart/donut 1100/1200ms, initial Luck bars/counts 1000ms, milestone rail 1400ms.

- Eleven event-wide stops: start, first approved submission, all teams submitted, first
  tile, all teams completed a tile, halfway, first row, all teams completed a row, first
  board, all teams completed a board, end. Team scope omits the four collective stops
  and labels its board milestone Board completed.
- Start stays first. Reached milestones sort by occurrence; ties and unreached stops
  follow default order. Active unreached stops say Upcoming; after the event ends they
  say Not reached with a muted × in the circle. No fabricated occurrence timestamps.
- Solid reached dots, a ring on the latest, hollow unreached dots; muted rail afterward.
  Dot centers align with timestamp/title left edges. Timestamp above, title and one
  detail line below; submission artwork is a small inline icon after the contributor.
- Compact height and horizontal scrolling; initial marker is protected by snap padding,
  and the last stop sizes to content. Team selector uses compact content-sized focus.
- Initial/team-switch rail draws from the first point at a steady pace, dots fill as it
  arrives and the latest ring appears afterward. Team switching fades; order changes
  slide. Interrupted/repeated renders and reduced motion are handled.

Focused executable DOM/animation-stand-in checks passed for scopes, stages, collective
thresholds, ordering, missed states, rendering and motion. User acceptance supplies visual
approval; sample values remain illustrative and production functionality is deferred.
The subsequent Board progress polish is covered by final Stats UI acceptance below.

### Final Stats prototype UI approval — 2026-09-15

The user accepted the completed UI: "I think that just might be the UI completed".
This accepts every Stats prototype section and supersedes earlier pending visual/trial
statuses, including narrow Drop value/header corrections, milestone spacing and the
"All teams completed the board" label. Production functionality remains deferred.

User approved the entrance animations and Board progress guidance, with all section
info icons matching Luck. Existing hover/focus and Current totals/date exploration stay
as approved. Shared 1200ms time sweep reveals lines/markers and advances tile/row totals;
interaction or reduced motion applies the final entrance state immediately. Guidance
starts open with ×/Escape dismissal and ⓘ reopening. The compact overlay preserves the
approved card dimensions. Focused DOM-stand-in behavior checks and existing section
regressions pass; the user has accepted visual timing and appearance.

Narrow Event milestones header alignment is user-approved (2026-09-15). A subsequent
CSS-only trial sizes narrow milestone stops to their headings (170px minimum), keeping
long labels on one line and the final stop compact. Entry spacing is now user-approved.

### Accepted composition and responsive behavior

- Drop value (~70%) beside Luck and Keeps on dropping (~30%); compact full-width Event
  timeline; Most versatile (~30%) beside Board progress (~70%). Preserve Barlow utility
  headings, Geist data, cream/blue light palette, existing dark palette and blue cards
  with quiet silhouette artwork.
- Drop value chart retains its existing responsive height as a minimum and expands to
  match taller actual donut/team content. No reserved empty team-breakdown rows; the
  list scrolls beyond six. Chart legend occupies one/two lines with scrolling and shared
  row sizing. GP BY TEAM stays fixed; donut/list center together below it. Valuable drops
  move upward when the upper row is shorter, with item content vertically centered.
  Existing narrow-screen donut/list grid is retained.
- Luck reserves three rows and grows to six before scrolling. Short lists center beneath
  the fixed heading/axis. Keeps on dropping absorbs the remaining right-column height.
- Board progress reserves four rows and grows to six before scrolling. Current totals/
  date axis and tracks center together for shorter lists. Heading, row spacing and logo
  remain unchanged. Most versatile follows the bounded height. Narrow charts scroll
  horizontally with a680px minimum; tooltip remains outside the scroller. Legend symbols
  use ink in light mode and cream in dark mode, with muted labels.
- Event timeline retains its compact horizontal rail, borderless right-aligned native
  selector and automatically centered chevron. Exploration hint sits to the selector's
  right; mobile filter/hint share one row. No bottom hint row.
- Footer offers 2/3/4/5/8/15-team event sizes, seven drop previews and theme switching;
  default is five teams/Dragon warhammer. Expected common event size is 3–5 teams.
- Adjust artwork is accepted: per-item drag, zoom, rotation, Save/Cancel/Reset and
  desktop/mobile previews. Prototype overrides persist in this browser's localStorage;
  original images and untouched default fitting are preserved. Actual Superadmin
  authorization and shared production persistence remain unimplemented.

### Next discussion and implementation boundary

Before implementing the page or functionality, the user wants to agree exactly what
**Drop value** and **Event timeline** display. These are the only sections open for
content discussion. **Luck, Keeps on dropping, Most versatile and Board progress are
locked**, including their displayed content and accepted UI. Do not reopen them or
infer new formulas from illustrative fixtures. The earlier clarification that diagonals
do not count still applies; mock denominators and milestone wording are not contracts.
Production implementation/acceptance remains deferred pending that discussion.

### Evidence

Scoped HTML/CSS structure, cascade and whitespace checks passed. JS syntax and focused
executable fixture/editor-handler checks with DOM/storage stubs passed, covering event
sizes, rerendering, totals/axis bounds, filter/scroll reset, drop assets, per-item editor
save/reload, cancel/reset and storage failures. The user supplied final visual acceptance.
No automated browser layout/interaction acceptance, .NET build or production verification
is claimed for this standalone prototype pass. The original
[Stats reference](docs/references/public-ui/stats-reference.png) remains background
inspiration; the final accepted HTML owns the prototype composition.

## Ticket candidate manual checkpoint — 2026-09-14

Current-event navigation change authorized: public header Current event for all visitors
in Live/Awaiting final review only; Captain/Submissions moves after Teams in event context,
retained on submissions overview/detail with correct scope. EN/DA and desktop/mobile.
Implemented and manually accepted by user, 2026-09-15. Focused executable checks blocked before execution
by local MSBuild socket permissions; existing banner/drops acceptance retained.

NEW markers and view-navigation underline corrections accepted by user, 2026-09-14.
Subsequent row-only marker refinement: 0.81rem (about 40% larger), layout-centered with title
without manual vertical offsets as explicitly requested by user;
implemented in CSS and visually accepted by user, 2026-09-14. Navigation marker unchanged.
Final NEW is plain coral text with no background/outline and accepted raised navigation
placement. User explicitly accepts existing dismissible banner overlap of header navigation.

Clear all NEW visually accepted by user, 2026-09-14: 1rem text action in first time-group divider,
padded container right edge aligned with the vertical separator left of Drop statistics,
removed from filters. Preserve current vertical centering and 0.55rem horizontal padding.
Implemented in Board.cshtml and CSS; behavior, visibility and EN/DA preserved. Opaque mask
persists on interaction; light-mode text is ink normally and muted on hover.
User confirms banner and drops scope complete; no further acceptance/review pass pending.

Announcement missing-artwork follow-up accepted by user2026-09-14; tiles are expected
to have artwork. Retain item→tile→text-only fallback as implemented; no additional visual
testing or changes requested. This supersedes pending fallback observation below.

CSV MR-09 accepted by explicit user waiver; all ticket manual acceptance now resolved.
Announcement follow-up only: user says banner itself works as intended; preserve current
composition/motion and correct missing/broken artwork with tile fallback then text-only
(no empty frame/icon), same banner size/controls. Fallback accepted; banner/drops work complete;
no independent review/build/browser pass is requested.

Discord Settings/link/login feedback and Admin Last login manually accepted by user
2026-09-14 on7131. Second-account replacement accepted by explicit waiver (no second
Discord account available); not claimed visually executed. C06/C07 acceptance complete.
This supersedes earlier pending Discord configuration/manual-check notes.

Final cancellation UI APPROVED by user — 2026-09-14. User ran the candidate and
explicitly approved the corrected error-page shell/spacing with no mark/divider and
horizontally centered copy. This supersedes all pending/rejected cancellation styling
checkpoints below. No further UI checks/review for this delta. Functional C37 acceptance
remains intact; r2 edits are additional to the original integrated manifest.

User explicitly waived independent review for this small cancellation UI change.
No further review; final narrow-width separator reset is implementer-owned and user
supplies visual acceptance. This overrides earlier review/recheck workflow for this pass.

Cancellation r1 implemented: mark/divider removed, original copy centered and full
viewport canvas styled for light/dark; existing EN/DA localizer keys retained. Web build
passed; user explicitly supplies visual/language checks. New styling still awaiting user
approval. r1 evidence at `/private/tmp/cancelled-event-ui-evidence/r1/`.

Cancellation visual feedback supersedes first styling: user rejected the giant X/divider
and constrained cream panel on black. Remove mark/divider, retain right-hand copy/button
styling, center the group horizontally and extend canvas across the full viewport below
the header. English/Danish required. Correction pending; prior style not accepted.

User-authorized follow-up implemented: cancelled Board/Teams/team/tile views reuse existing
404 `status-editorial` structure, typography and return CTA with a decorative cancellation
mark. One shared partial changed; no CSS/404 or route/privacy changes. Web Release build
and independent scoped review passed; served at HTTPS7147. C37 functionality remains
accepted; this new styling delta awaits user visual approval. Evidence:
`/private/tmp/cancelled-event-ui-evidence/review.md`. No other page family is reopened.

Latest user decision: chat steps 13–15 / MR-12/13/14 accepted by explicit waiver
of further manual testing. Existing automated/review evidence accepted; their unobserved
UI states are not described as visually tested. These checks impose no further manual
acceptance work. This supersedes their pending/declined wording in earlier checkpoints.

Second user checkpoint: MR-11, MR-15/16/17/19/20/21/22/23/24/25 passed; MR-26
internal evidence accepted. Observed draft leadership/move feedback, catalogue conflicts,
board wording/Discard/artwork/error states, changed Captain/review feedback, audit/reversal,
finalization freshness, unfinalization closure, cancellation destinations, font presentation
and public interactions are accepted for the supplied walkthrough. MR-03 remains pending,
MR-09 skipped, MR-12/13 declined/incomplete, MR-14 unexecuted after guide clarification.
No unobserved state or whole-site redesign is approved by this checkpoint. Earlier
MR-04/MR-05 UI observations remain notes without a requested correction.

Candidate: `/private/tmp/BingoWebpage-ticket-integration-20260914`, frozen manifest
`ced7502b5deae58679b2a78cc530e948a9ed0894cf5c30f8deaee29f19afbaa8`.
User passed MR-01/02 (My Accounts/signup recovery), MR-04 (Admin stale account action),
MR-05/06 (event creation), MR-07 (signup warning/code), MR-08 (Live competition
replacement), MR-10 (draft pool/waiting list) and MR-18 (submission filters).
These accept the reported scenarios only; existing page approvals and untested new
states retain their prior scope. On MR-04 user noted "a little funky UI but functionality
works"; on MR-05 "a bit weird UI". No specific UI correction was requested or inferred.
Discord MR-03 remains pending configuration; CSV MR-09 may be skipped for continuation
and remains unaccepted. Other new ticket states await the remaining walkthrough.

## Public UI reference inventory

This is the maintainable reference library for the replacement Public UI.
Update these tables when a reference is added, superseded, split, or assigned
to another compatible page family. Canonical screenshots define composition;
dark references define tokens only unless a row explicitly says otherwise.
Exploratory/rejected generations and boss artwork are not page references.

### Available canonical reference groups

| ID | Reference | Pages/states covered | Reuse boundary |
| --- | --- | --- | --- |
| PUB-REF-01 | Landing: `docs/references/public-ui/pub-ref-01-landing.png` | `/`, global public header, editorial masthead, feature strip, event-status/action rows | Landing/event-directory composition only; shell and typography roles may be reused |
| PUB-REF-02 | Live event overview: `docs/references/public-ui/pub-ref-02-live-event-overview.png` | `/Events/{slug}/Board` default view, masthead, team summaries, tile legend, recent activity | Reactivated for the 2026-08-24 Board pass. The current team-overview grid below the masthead is already near target and is protected; the reference owns the corrected masthead integration and missing Recent Activity footer. Final light canvas is `#FFF9EC`; existing protected Board behavior remains authoritative. |
| PUB-REF-03 | Team-specific board: `docs/references/public-ui/pub-ref-03-team-board.png` | `/Events/{slug}/Board/{teamSlug}`, ordinary TeamBoard page, statistics, contributors, 5×5 tiles | Reactivated for the 2026-08-24 structural rewrite. Boss experiment `exec-f8a2115a-29d5-4198-b191-f33851d0c756.png` demonstrates artwork placement only |
| PUB-REF-04 | Event leaderboard: `docs/references/public-ui/pub-ref-04-leaderboard.png` | Board `?view=leaderboards`; EHB, Drop EHB and Players views; expandable teams and side standings | Table/standings family; does not define evidence/recent-drops composition |
| PUB-REF-05 | Event signup: `docs/references/public-ui/pub-ref-05-signup.png` | Signup create/edit states, numbered sections, account selection, questions, capacity and summary rail | Signup route owns both create and edit states |
| PUB-REF-06 | Signup confirmation: `docs/references/public-ui/pub-ref-06-signup-confirmation.png` | `/Events/{slug}/Confirmation`; confirmed/waiting/read-only outcomes | Reuses signup family shell but owns outcome/read-only composition |
| PUB-REF-07 | Account settings: `docs/references/public-ui/pub-ref-07-account-settings.png` | `/Account/Settings`, `/Account/Setup`, structurally similar account-management forms | May govern sibling forms only when composition and task hierarchy match |
| PUB-REF-08 | Change password: `docs/references/public-ui/pub-ref-08-change-password.png` | `/Account/ChangePassword`, Forgot Password and Reset Password narrow-form structure | Password/recovery form family only |
| PUB-REF-09 | Authentication and status family: `docs/references/public-ui/authentication-status-reference.png` | Login, Onboarding, AccessDenied/403, StatusCode/404, and general Error/500 | The composite defines shared hierarchy, composition, form density, and state relationships. Its condensed panels are directional rather than literal pixel-scale targets. The Discord-completion panel is visual direction only: the existing route processes and redirects immediately and remains non-rendering. |
| PUB-REF-10 | Account overview family: `docs/references/public-ui/account-overview-reference.png` | `/Account/MyAccounts`, `/Account/MyEvents`, character/event rows, controls, warnings, history and empty states | Use the nearest approved stored light/dark palette tokens rather than sampling slightly shifted colors from this composite. The sheet owns the two account-overview compositions and their empty states. |
| PUB-REF-11 | Notifications: `docs/references/public-ui/notifications-reference.png` | `/Notifications`, read/unread rows, semantic states, timestamps, wrapping, mark-all-read and empty inbox | The Admin-actions specimen is later dashboard/action-inbox direction only and does not authorize Admin pages or workflows during the Public UI passes. |
| PUB-REF-12 | Guidance and editorial: `docs/references/public-ui/guidance-editorial-reference.png` | `/HowTo` structural family, Privacy real-content structure, editorial typography, local navigation, callouts, media and narrow recomposition | HowTo uses the reactivated structure with the approved five-step content contract; Privacy uses its authoritative real content. The reference owns hierarchy and responsive behavior, while routes, workflow wording, and evidence visibility remain authoritative in the active contracts. |
| PUB-REF-13 | Public Signups directory: `docs/references/public-ui/public-signups-directory-reference.png` | `/Events/{slug}/Signups`; confirmed/waiting tables, counts, capacity/status summary, empty groups and narrow participant rows | This is an exact-link/Discord-link directory: the pictured sibling navigation is non-authoritative and must not be added by this reference. The directory may inform table/rule treatment but does not define the different `/Events/{slug}/Teams` roster composition. |
| PUB-REF-14 | Recent Drops: `docs/references/public-ui/recent-drops-reference.png` and `docs/references/public-ui/recent-drops-states-reference.png` | Board `?view=drops`; latest approval, earlier approvals, metrics, search/team filters, pagination, no-drops, no-match and narrow/mobile states | Reuses the protected Board/event navigation and behavior; the references own the Recent Drops composition only. Dynamic evidence facts and permissions remain authoritative. |
| PUB-REF-15 | Tile detail and evidence viewer: `docs/references/public-ui/tile-detail-evidence-reference.png` | `/Events/.../Tiles/{tileId}` and protected `/Evidence/{assetId}` file downloads consumed by the lightbox | Preserve the protected route-backed tile/sidebar, overlay, focus/history and submission behavior. `/Evidence/{assetId}` is a file-download dependency, not a rendered page. Artwork/evidence is content; the reference owns placement and hierarchy, not specific sample drops. |
| PUB-REF-16 | Public Teams/roster: `docs/references/public-ui/public-teams-roster-reference.png` | `/Events/{slug}/Teams`; event masthead, final-team groups, member-role hierarchy, draft results and large-width density | The accepted image is compositional direction: its sibling navigation is non-authoritative for this exact-link/Discord-link directory. Integrate the existing Landing-family diagonal DK artwork rather than reproducing its slightly detached generated placement, and tune spacing against the real content. No team images or role icons. Use whitespace instead of an inter-team divider grid; distinguish Captain/Co-captain by semantic text color; show two draft picks per row at large widths and one when constrained. Preserve authoritative event timing, roster/draft ordering, privacy, permissions, routes and empty states. |
| PUB-REF-17 | Canonical Captain submission-workspace detail language: `docs/references/public-ui/captain-team-operations-reference.png` | `/Submissions`, `/Submissions/{id:guid}`; Captain-only team focus/status sections, complete team ledger, filters, detail/feedback access and responsive states | Reference owns the approved Captain detail language and the Captain-only overview hierarchy; it deliberately excludes a duplicate board, submission form and review controls. `Replaced` is a derived presentation from the existing resubmission relationship, not a new stored status. The open upper-right balance is directional: implementation may place one compact existing authoritative team fact, but must not invent a decorative dashboard feature. Legacy `/Captain` routes are compatibility aliases only. |
| PUB-THEME-01 | Dark live event `/Users/christopher/.codex/generated_images/01a028f7-6a50-7c81-bee7-f0a26392f948/exec-b286e33e-d71d-4b1b-a16e-e4512333c204.png`; dark signup `/Users/christopher/.codex/generated_images/01a028f7-6a50-7c81-bee7-f0a26392f948/exec-b58a260f-4b02-4f4c-8e71-93c34d94ffde.png`; dark settings `/Users/christopher/.codex/generated_images/01a028f7-6a50-7c81-bee7-f0a26392f948/exec-72b0b0fd-21e0-4f3e-9493-f093d878a9fe.png`; dark team-board palette `/Users/christopher/.codex/generated_images/01a028f7-6a50-7c81-bee7-f0a26392f948/exec-251a70d5-3558-45cb-9249-1758aea16118.png` | Shared dark token treatment across matching light compositions | Dark changes tokens only; light references own geometry, placement and outlined numbers |

### Canonical reference groups still needed

None. All currently planned Public UI reference groups are recorded. A reference
does not approve its implementation or remove the functional gaps documented in
`CURRENT_STATUS.md` and `DELIVERY_PLAN.md`.

The Participants persistent-setting checkbox is an approved page-local
durable-setting pattern. It is not a lifecycle acknowledgement or a generic
globally shared checkbox component.

## Board protection notes

The approved Board composition keeps the workspace/canvas relationship, board
toolbar, tile grid, sidebar statistics, route-backed tile dialogs, collaboration
state, preview route, and lifecycle actions. Board-local panel boundaries and
dividers are owned by `.board-page`, `.board-canvas`, `.board-sidebar`, and the
existing board CSS/markup; a generic Admin row divider or pseudo-element must
not add a second boundary.

The only approved rearrangement boundary is the existing drag handle and
drag/swap state owned by `Events/Board.cshtml` and its `.board-cell.dragging`,
`.board-cell.drop-target`, and line-highlight behavior. Do not introduce tile
duplication, import, templates, or a new keyboard/touch rearrangement model in
this documentation pass.

## Approval and reference notes

Signup Questions, Admin evidence review, Finalize/Audit, and the remaining Admin
UI families are not approved by the approved Admin
baseline. Public Board behavior/interactions remain accepted, while its old
appearance is superseded. Atomic landing Pass 1A is manually accepted as of
2026-08-22. Signup and Confirmation are manually accepted as of 2026-08-22;
standalone Login, Onboarding, and the AccessDenied, StatusCode, and general Error
pages are manually accepted as of 2026-08-23. The `/HowTo` implementation is
complete and approved after user manual acceptance on 2026-09-01.
The user-approved Captain/participant submission-workspace consolidation is
complete: `/Submissions` and `/Submissions/{id}` are the sole canonical
overview/detail routes, with Captain-only sections and server-authorized broader
Captain/co-captain editing. Legacy Captain routes are compatibility aliases only;
personal submission/evidence notifications resolve to the canonical detail route.
The implementation was independently reviewed, remediated, manually accepted,
and committed on 2026-08-31; it was accepted through local whole-application
regression on 2026-09-01. Existing linked-resubmission Admin Review behavior
remains protected and no query change is authorized; no separate manual
confirmation gate is pending.
F-04 is resolved by retaining Live identity and display timezone as read-only;
the separate Live event-end correction belongs to Schedule. The How To F-06
sequencing decision is resolved by the implemented five-step guide.

Landing typography-only change control (2026-08-22): the user authorized a
current Bebas Neue 400 versus genuine Barlow Condensed 800 A/B, using
`https://outbid.website` only as a typography-hierarchy reference. All landing
colors, copy, spacing, layout, responsive geometry, behavior, and non-landing
families remain frozen. The experiment must use a real bundled 800 face at
natural width and returns to the user for visual choice; it is not approval or
reference-font provenance.

The user selected and accepted the Barlow Condensed ExtraBold 800 direction,
including the final ledger/divider/archive and exact-SVG corrections. The final
landing is build-clean and manually approved as of 2026-08-22. Later work may
reuse its shell and typography roles but must not reopen its accepted
composition.

References are assigned by visual family, not mechanically one screenshot per
route. Sibling pages may reuse an approved family reference when their composition,
hierarchy, and interaction geometry match. A materially distinct unresolved page
requires a new user picture reference before implementation rather than an
invented layout; the matrix must record whichever mapping or new reference applies.

Account Settings approval (2026-08-23): `/Account/Settings` is the approved
standard left-aligned account-page baseline for ordinary account pages, with a
54rem shared Standard desktop content column. Standard page display headings use the approved
Login-page display scale unless a special page composition justifies an
exception. Dark mode uses restrained smoky-indigo/violet as an accent; large
section numerals may use it, but it is not the dominant page color. Resting
dark-mode input borders remain neutral and use the established focus treatment.
The initial approval applied only to Account Settings; My Accounts and My Events
were separately approved on 2026-08-24. Change Password, Forgot/Reset Password,
Setup, Notifications, and Privacy are implemented and awaiting intentionally
deferred manual approval. My
Accounts and My Events are reference-owned broad account
overview compositions under PUB-REF-10; they do not inherit the 54rem
left-aligned standard-page column merely because they share the Account shell.

**Change Password dark-mode correction — 2026-08-24:** the user found the page
otherwise acceptable but rejected its violet-heavy dark treatment. On this page,
the visible `Account security`, `Current password`, and `New password` roles use
the established cream primary text in dark mode, including repeated heading/label
instances of those named strings. Resting input borders use the same neutral
dark-mode rule as the approved account inputs; focus, validation, light mode, and
password behavior remain unchanged. This correction does not pre-approve the
Forgot/Reset sibling pages.

My Accounts correction checkpoint (2026-08-23): the independent PUB-REF-10
review required the equal-width Add fields, horizontal Saved EHB/Fetch
composites, single masthead divider, input-aligned action band, and reorder
controls immediately after the editable fields. The user approved a short
registered-for-upcoming/live-event label above the controls and replacement of
the visible unlink-confirmation checkbox with the existing localized native
confirmation prompt. The bounded remediation is complete and preserves the
server's fail-closed confirmation plus unchanged registration/history; focused
source/cascade/localization/diff checks pass. The separate
Fetch/Correct/account-action behavior drift remains a functional blocker and is
not closed by visual approval.

The 2026-08-23 manual correction further fixes My Accounts position 01 as the
sole preferred character and removes the separate set-preferred control. Linked
rows use sync icon plus `Fetch`; all visible field labels use `EHB`; and the
registered-character label reads `Registered for an event`. These corrections
are included in the user's page-specific approval.

The Add-character form uses a two-by-two intermediate layout at `<=1200px`:
OSRS Character and Personal Label form the first row, while the horizontal
EHB/Fetch composite and Add Character action form the second. At the existing
mobile breakpoint it stacks to one column. This prevents the Add-row Fetch label
from clipping without changing the linked-row compact Fetch treatment. The
bounded correction is implemented and included in the user's page-specific
approval.

**Approval — 2026-08-24:** the user manually approved My Accounts after the
responsive Add-form, EHB precision, and move-to-position-01 corrections. This
approval is specific to the My Accounts Public UI page; it does not approve My
Events, the rest of Pass 2, or the separately recorded functional behavior
drift.

**My Events review evidence — 2026-08-24:** the user supplied current-route
light desktop, dark desktop, and 390px narrow screenshots showing waiting-list
and live current rows plus empty history. PUB-REF-10 remains the sole visual
target. The stopped independent review found one concrete delta: inherited
full-width semantic backgrounds replace the reference's compact dot/label status
followed by a neutral vertical divider. A bounded remediation may reuse existing
Landing assets or selectors where the similar row structure genuinely matches,
but Landing is not a reference and its date gutter, columns, or composition must
not be copied into My Events. Preserve dynamic facts, routes, and responsive
association. The bounded correction passed its focused Release build and the
user manually accepted the resulting My Events page on 2026-08-24 despite a
remaining non-blocking visual imperfection. This approval is page-specific. The
user subsequently reopened only the event-list terminal rule: rows retain
dividers between entries, but the last row in each Current Events or History
list must not render a bottom divider or retain the terminal bottom padding that
belonged with that divider. All other My Events decisions remain accepted.

**Shared secondary navigation correction — 2026-08-24:** the user superseded
the header-extension treatment for sibling account and event views. Preserve the
existing links, routes, visibility, and active-state semantics, but render the
shared row below the colored masthead using PUB-REF-02's content-level tab
treatment. The already approved account page bodies remain frozen; this shared
navigation correction requires its own manual acceptance.

The first shared-navigation manual check named three bounded corrections: reduce
the page-body top padding substantially when a secondary row is present; increase
the current underline thickness in both primary and secondary navigation; and
move the primary current underline beneath its label with the same internal
padding as the secondary row instead of anchoring it to the masthead bottom.
These shared-shell corrections do not reopen page bodies, link inventories, or
route behavior.

The follow-up manual check adds one shared interaction-state correction, then
clarifies its direction: across primary and secondary navigation in both themes,
inactive and selected labels use the same full-strength resting color; hovering
an inactive label fades its text. The selected label remains full-strength and
underlined. Geometry, routes, and active-state semantics remain frozen.

**Shared secondary navigation approval — 2026-08-24:** after the spacing,
underline, terminal-row, and reversed hover-state corrections, the user moved to
the next-page gate. Preserve the accepted shared primary/secondary navigation
geometry and interaction states across light, dark, desktop, and narrow layouts.

**Shared masthead text-stack normalization — 2026-08-25:** the Privacy
masthead is the implementation authority for the ordinary left-side Public UI
text stack. The shared semantic kicker/title/support roles and `.65rem` internal
grid gap now apply to the existing left roles in `/Events/{slug}/Signup`
(create/edit), `/Events/{slug}/Signups`, `/Events/{slug}/Teams`, the Board
`View Bingo`, `Recent Drops`, and `Leaderboards` views, and the canonical
`/Submissions` plus `/Submissions/{id:guid}` routes. Existing right-side facts, controls, artwork,
lifecycle/capacity columns, metadata, rails, actions, and lower page geometry
remain named family ownership. Landing, Signup Confirmation, TeamBoard, Tile,
Login, Onboarding, AccessDenied/Error/StatusCode, and the implemented HowTo guide are
explicit exceptions. This is a bounded prerequisite before Board work resumes;
it changes no family approval state, and all affected pages remain subject to
user visual recheck rather than being newly approved.

The submission-workspace consolidation must preserve the neutral “Team
workspace” terminology where already accepted. This is an implementation
ownership correction, not a new visual direction; Captain-only readiness language
remains explicit.


## Boss KC leaderboard addition — authorized 2026-09-17

PRODUCT_REQUIREMENTS.md section 16.1 and DELIVERY_PLAN.md section 19 own this bounded
addition to Board's Leaderboards view. Existing Board-family approval remains intact.
Reuse the existing tables and masthead dropdown. The new metric trigger matches tab
styling and aligns immediately before expanded standings or at the right end of the
horizontal divider when collapsed; narrow layouts may wrap above the divider.
EHB/Drop EHB table changes are limited to MVP names/tie wording and individual values,
plus the user-approved Rank heading in both English and Danish (replacing Placering).
Final independent review must compare tables for unintended UI differences; user
visual/interaction acceptance is pending. No agent browser/CUA inspection is authorized.
Deferred objective wording/sidebar spacing are not part of this pass.

Final numeric corrections manually approved 2026-09-23: boss KC `-1` sentinels
render as em dashes like null in expanded Teams and Players, and boss team average
gains display rounded whole numbers. Stored values, calculation/sort precision,
contributor-only averaging and EHB/Drop EHB formatting remain unchanged. This
approval covers the current running corrections together with the actual shared
Stats masthead above. Independent UI review of this final display delta was skipped
at the user's explicit request; focused checks passed. The broader walkthrough
acceptance/waiver/deferral disposition is owned by MANUAL_TEST_CHECKLIST.md's
“Current walkthrough disposition — user acceptance, 2026-09-23”.


Boss KC leaderboard technical acceptance checkpoint — 2026-09-17: implementation and
named remediation passed independent Sol High review; no remaining technical findings.
Report `/private/tmp/bingo-boss-leaderboards-20260917/review.md`; frozen reviewed patch
SHA-256 `8bc192ad83ea79b31c009be18738801933c56016f99a3322a2422810388a902b`.
Existing EHB/Drop EHB structure/styles/interactions were source-compared; only agreed
MVP/Rank changes are accepted technically. Source and automated checks do not establish
visual/keyboard acceptance. **User manual acceptance remains pending.** Minor objective
wording/sidebar spacing remain deferred; no publication occurred.


Section 19 user visual feedback — 2026-09-17: acceptance is NOT granted. Correct selector
close behavior and option prefix, existing green/signed gains and MVP values, no boss
Drops arrow or leaderboard freshness/status text, late stacking with Metric above tabs
and both left-aligned, and in-place metric switching without scroll-to-top. User selected
muted blue for every dark-mode leaderboard header. These decisions supersede earlier
stacked alignment/visible freshness instructions; cache integrity remains protected.
Same-worker corrections and same-reviewer bounded recheck precede renewed user inspection.

Active section 19 correction addition: Danish boss nested Teams/Players headings are
Opnået/Start/Slut. Nested EHB Gained/Start/End headings omit redundant EHB wording;
Drop EHB headings remain unchanged. User acceptance is still pending.

User decision 2026-09-18: light-mode METRIC trigger text and chevron use existing ink
normally, and existing blue when open, hovered or keyboard-focused. This replaces the
earlier always-blue request. Included in the current correction pass; acceptance pending.

User retest 2026-09-18: selector dismissal remains rejected at the observed preview;
only option selection hides the menu. Reclicking METRIC/selected name/chevron and an
outside click must close it without selecting a value, including after in-place metric
replacement. Implementer notified while still running. Preview build identity is not
verified, so no claim is made that the observed runtime includes the latest source fix.
Actual menu visibility, not just open/ARIA state, remains a required acceptance check.


Visual correction implementation checkpoint — 2026-09-18: worker reports all named
corrections complete with focused automated checks passing; evidence at
`/private/tmp/bingo-boss-leaderboards-20260917/visual-corrections-evidence.md`.
No independent review of this correction delta or user visual acceptance is claimed.
User explicitly deferred review until tomorrow; reviewer has not been dispatched.


## Admin-managed WOM competitions — awaiting manual review, 2026-09-22

User explicitly marked this addition **Awaiting manual review**. The scoped
implementation and final independent Sol High recheck passed; this is technical
acceptance only. User EN/DA manual inspection remains pending for the Admin
creation preview/validation, automatic link and management feedback, errors and
recovery, before-Live deletion confirmation, and affected event/team name inputs.
Existing page approvals remain intact; this does not grant acceptance of the new
states. Optional provider-ID rename recognition is omitted. No further worker
pass or publication is active. Final technical evidence is the “Final named-only
recheck” in `/Users/christopher/.codex/visualizations/2026/09/17/01a0b085-75ee-79b0-b40b-bba65b669d74/wom-independent-recheck-20260922.md`.

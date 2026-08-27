# UI Page Matrix

This matrix is the sole current page approval/status authority for the active
page-family, canonical-reference, exception, and approval record. A canonical
reference demonstrates composition; it does not approve another page or the
whole regression. Current status is explicit for every row.

On detail/form pages, an information rail may appear on wide desktop. It is
removed, not relocated, at constrained widths. Full-width/table pages never
inherit the rail rule.

| Surface / route | User / primary task | Layout family | Canonical reference / components | Protected composition | Approved exceptions | Current approval state | Next gate or owner |
| --- | --- | --- | --- | --- | --- | --- | --- |
| Admin shell / `/Admin/*` | Admin navigates event and global work | Admin shell | `_AdminLayout.cshtml`; Admin shell CSS; `site.js` menu | Header/context, event selector, sidebar, drawer, scrim, focus order | Compact operational charcoal shell | Approved | Preserve baseline; whole-app regression later |
| Public UI foundation catalogue / `/Admin/PublicUi` | Admin inspects the retained historical public specimen | Internal design-system catalogue | `Admin/PublicUi.cshtml`; legacy specimen markup | Direct-link only; no Admin navigation entry | Not an approval prerequisite for the replacement Public UI; Admin route is outside this experiment | Historical specimen retained; replacement identity supersedes it for product pages | Preserve unchanged; no Admin work |
| UI reference gallery / `/Admin/UiReferences` | Admin inspects retained historical UI reference assets | Internal reference gallery | `Admin/UiReferences.cshtml`; bounded reference-image handler | Direct-link only; no Admin navigation entry or product workflow | Historical reference material only; not a product-page implementation, approval target, or authority source | Approved to retain — user decision, 2026-08-27 | Preserve as an internal read-only reference route; do not use it to reopen approved pages |
| Event Create / `/Admin/Events/Create` | Admin creates an event | Detail/form | `Create.cshtml`; event-create fields/actions | Five-step route-backed creation flow and validation orientation | Date-time picker and progressive enhancement | Approved | Teams/Draft owner keeps navigation compatible |
| Identity / `/Admin/Events/Identity/{id}` | Admin edits public event identity | Detail/form + optional rail | `Identity.cshtml`; `.identity-editor-layout`; Manage hierarchy | Form groups, banner upload, timezone confirmation, public context rail | Rail is wide-only and page-local | Approved | Preserve; final accessibility gate |
| Schedule / `/Admin/Events/Schedule/{id}` | Admin configures dates, capacity, warnings | Detail/form + optional rail | `Schedule.cshtml`; `.schedule-editor-layout` | Schedule groups, warning acknowledgement, combined date-time controls | Rail is wide-only and page-local | Approved | Preserve; final accessibility gate |
| Manage/Overview / `/Admin/Events/Manage/{id}` | Admin reads readiness and runs lifecycle actions | Detail/form + optional rail | `Manage.cshtml`; `.event-overview-section`, `.event-overview-row` | Operational summary, readiness rows, dates, controls, lifecycle actions | Information rail is a page-local owner | Approved | Later pages may link here; no redesign |
| Events directory / `/Admin/Events/Index` | Admin finds an event and opens its workspace | Full-width data/table | `Events/Index.cshtml`; directory toolbar/table CSS | Search, state filter, sortable table, empty state, Workspace action | `1100px` label/value cards | Approved | Retain as full-width table reference |
| Participants / `/Admin/Events/Participants/{id}` | Admin manages signup settings and participants | Full-width data/table + accepted detail dialog | `Participants.cshtml`, `Participant.cshtml`; participant table/dialog classes | Compact settings, search/status controls, current/history groups, route fallback | Accepted participant-detail dialog states; page-local table markup | Approved | Preserve accepted states; Questions remains separate |
| Signup questions / `/Admin/Events/Questions/{id}` | Admin edits the standard and custom questions shown on an event's public signup form | Detail/form + route dialog | `Questions.cshtml`; `_AdminLayout.cshtml`; `signup-questions-overlay.js` | Real route, Participants/signup-form dialog enhancement, add/edit/remove/reorder controls, compact confirmation, focus/history path | This route does not own CSV import; its ordinary presentation is the popup launched inside Participants | Approved — user manual approval, 2026-08-24 | Preserve approved route/dialog states and behavior |
| Catalogue / `/Admin/Catalogue/Index` | Admin manages catalogue activities and drops | Full-width directory + route editor | `Catalogue/Index.cshtml`; `catalogue-admin.js` | Directory search, Add, editor dialog/route, drop sections | Page-local cards/editor and confirmation states | Approved | Preserve; broader data/import work is separate |
| Accounts/Roles / `/Admin/Accounts/*` | Admin manages accounts and emergency credentials | Full-width data/table + detail/form/dialog | `Accounts/Index.cshtml`, Create, Manage, Transfer; `account-manage-dialog.js` | Separate datasets, role/search controls, route-backed forms, strong confirmations | Desktop progressive dialogs; narrow route paths | Approved | Preserve approved Index/Create/Manage/Transfer |
| Board / `/Admin/Events/Board/{id}` | Admin builds, approves, previews, and publishes a board | Workspace/canvas | `Events/Board.cshtml`; `.board-page` CSS and inline board script | Toolbar, canvas/sidebar, tile dialogs, collaboration, preview, lifecycle states | Board-local divider ownership; drag handle + `.drop-target` swap boundary only. `/Admin/Events/{id}/Preview/{teamSlug?}/{tileId?}` mirrors the public Board ecosystem, is not independently owned by this approved Admin page family, and inherits the public family's awaiting structural-remediation status; it has no independent visual overhaul or approval. | Approved — user manual approval, 2026-08-14 | Preserve; remaining Admin UI resumes only after Captain |
| Teams/Draft / `/Admin/Events/Draft/{id}` | Admin forms teams and runs the snake draft | Workspace/canvas | Existing event workspace shell and Draft route | Team roster, Captain/co-captain distinction, draft controls, finalization/publish readiness, and advanced pre-formed-roster CSV preview/apply flow | The CSV importer is an advanced state inside this approved workspace, not a separate Questions page. No new public board or alternate draft model | Approved — user manual approval, 2026-08-16 | Preserve; remaining Admin UI resumes only after Captain |
| Captain team operations / `/Captain` | Captain, co-captain, or valid emergency captain coordinates focus and resolves team submission problems | Public/participant operations | PUB-REF-17 and Functional contract 7.2 | Ordered composition: current team focus and controls; pending/rejected/approved summary counts; complete team submission ledger with status/player/tile filters and links to submission details and reviewer feedback | Do not duplicate the board or submission form. Captains submit through exactly the same team-board tile drawer/interface as ordinary members. `/Captain/Submit/{tileId?}` is only drawer transport/handler plus a compatibility redirect for old direct links, not a rendered or no-JavaScript page. No evidence-review controls. Preserve existing role and emergency-captain scope. The reference's empty upper-right space may receive one restrained existing authoritative fact such as the current evidence code or cutoff; do not invent another dashboard widget. | Partially approved, deployment ready — user decision, 2026-08-26 | Preserve deployment-ready Captain operations; complete remaining manual approval in whole-application regression |
| Captain submission detail / `/Captain/Submissions/{id:guid}` | Captain, co-captain, or valid emergency captain inspects one team submission and its permitted team-scoped actions | Public/participant detail | Existing submission-detail presentation; PUB-REF-17 family; Functional contract 7.2 | Submission status and evidence, immutable submission facts, reviewer feedback, pending correction/withdrawal, rejected resubmission, and return destination | Captain/co-captain/emergency scope remains team-bound. No Admin review controls. Ordinary participants use the neutral participant routes below. | Partially approved, deployment ready — user decision, 2026-08-26 | Preserve deployment-ready Captain submission detail; complete remaining manual approval in whole-application regression |
| Participant submissions / `/Submissions`, `/Submissions/{id:guid}` | Authenticated current team member inspects the complete retained team ledger; the credited owner manages their own eligible rows | Public/participant operations + detail | Captain ledger and submission-detail presentation as a visual sibling; Functional contracts 7.3 and 9.5 | Complete current-team submission ledger including departed credited members, status/player/tile/drop search or filtering as applicable, detail and feedback access, owner-only pending edit/screenshot replacement/withdrawal, owner-only rejected linked resubmission, read-only retained states, and normal authenticated navigation | The ledger omits the Captain page's first two Captain-only sections: current team focus and team submission status. Current members may read teammate-owned rows and private evidence; former members, anonymous users, and cross-team viewers fail closed. Neutral mutations never gain Captain/co-captain/emergency team-wide authority. Credited-owner rejection notifications use `/Submissions/{id}`; current linked Captain/co-captain notifications use `/Captain/Submissions/{id}`, with owner precedence for a recipient holding both roles. | Partially approved, deployment ready — user decision, 2026-08-26 | Preserve deployment-ready participant ledger and detail; complete remaining manual approval in whole-application regression |
| Admin evidence review / `/Admin/Review/*` | Admin reviews, resolves, and corrects evidence | Full-width queue/detail | Current review routes and evidence detail markup | Queue context, evidence inspection, approve/reject/reverse, immutable timestamps | Protected `/Evidence/{id}` is a file-download handler consumed by evidence views/lightboxes, not a rendered page or visual family. Lightbox and correction confirmations remain bounded | Deployment ready, not approved — user decision, 2026-08-26 | Preserve deployment-ready implementation; manual approval remains deferred |
| Finalize/closeout / `/Admin/Events/Finalize/{id}` | Admin resolves final-review blockers, publishes official results, archives, and reopens corrections | Detail/form + full-width placements ledger | Current Finalize route; approved Admin detail/form and table components | Blockers, review-cycle/version concurrency, placement corrections, immutable snapshots and retained history | Accounts/Roles approval does not approve closeout | Deployment ready, not approved — user decision, 2026-08-26 | Preserve deployment-ready implementation; manual approval remains deferred |
| Audit / `/Admin/Audit/*` | Admin filters and inspects administrative history | Full-width data/table | Current Audit route; approved Admin directory/table components | Audit ordering, filters, structured before/after context, ownership and authorization protections | Accounts/Roles and Finalize approval do not approve Audit | Deployment ready, not approved — user decision, 2026-08-26 | Preserve deployment-ready implementation; manual approval remains deferred |
| Public landing / `/` | Visitor discovers current and previous events and follows the correct destination | Editorial landing | Approved target: `docs/references/public-ui/pub-ref-01-landing.png`; accepted implementation evidence under `tmp/public-ui-pass1a/` | Blue masthead; open diagonal hero and DK artwork; Barlow Condensed ExtraBold 800 display/numerals, Barlow Condensed SemiBold 600 utilities, Geist body; target-matched 32×32 icons; strong section rules, inset dividers, compact current ledger and distinct archive rows; semantic coral/sage/bronze tones; token-only dark composition | Preserve PageModel, real dynamic event counts/facts/data/destinations, localization, routes, CTA semantics, accepted composition/typography/palette/copy; no other family or behavior change. The standalone `/Account/Login` route is the only runtime login surface: anonymous signup links navigate there with their validated local `ReturnUrl`, and no login popup/dialog is retained. The 2026-08-23 masthead correction reuses the approved Login logo SVG variants and percentage-painted diagonal field, adds the target bottom divider, and hides artwork when the hero stacks. | Approved — user manual acceptance retained after masthead correction, 2026-08-23 | Preserve approved Landing; remaining Pass 1B pages resume only at the next user-authorized gate |
| Public signup / `/Events/{slug}/Signup`, `/Events/{slug}/Confirmation` | Visitor signs up, edits through the Signup route state, and reads the outcome | Public/participant surface | PUB-REF-05 and PUB-REF-06; existing route/forms. The TEST 16 PDF recorded in DELIVERY_PLAN is rejected current evidence, never a target | Reference-owned wide horizontal event masthead with real capacity/waiting status, three compact ruled form rows, one persistent right summary rail, deliberate bottom action row, and distinct confirmation outcomes; waiting list, edit/read-only boundary, light/dark geometry parity | The current repository has no separate EditSignup Razor page; Signup owns create/edit states. Preserve bindings/behavior, not either rejected DOM, width, container, or flow. Use the global accepted Landing header from `_Layout` | Approved — Signup and Confirmation user manual acceptance, 2026-08-22 | Preserve approved Signup and Confirmation; remaining Pass 1B pages resume only after the current stop gate |
| Public Signups directory / `/Events/{slug}/Signups` | Visitor reads confirmed and waiting participants | Public/participant directory | PUB-REF-13; current Signups route | Counts, capacity/status summary, privacy-safe participant facts, phase-safe groups, empty states and narrow rows | Exact-link/Discord-link directory; no sibling navigation added from the reference | Approved — user manual acceptance, 2026-08-24 | Preserve approved directory, responsive rows, and copied signup-masthead behavior |
| Public Teams/roster / `/Events/{slug}/Teams` | Visitor reads final teams, member roles, event timing, and draft order | Public/participant roster | PUB-REF-16; existing Teams route and authoritative roster/draft projections | Left title/back plus compact Event starts/Event ends/Players metadata row, where Players is the frozen published-roster count; full-bleed Landing-family diagonal DK artwork on the right with one masthead-bottom rule beneath text and artwork; because this masthead is content-height rather than Landing-fixed, its diagonal, stripe, mark size, and crop shrink together from the actual masthead height but are capped at Landing's live viewport geometry; keep the separate Final teams heading-owned rule; three-column final-team roster without team images or inter-team divider grid; each team sublabel shows formation type only, not affiliation; each published roster orders Captain first, Co-captain second, then participants by effective draft-pick number; Captain and Co-captain use semantic text color without icons; two-picks-per-row draft results on large widths and one-per-row when constrained | This is an exact-link/Discord-link directory and intentionally has no event sibling-view navigation; the navigation shown in PUB-REF-16 is non-authoritative chrome. Reference spacing and artwork attachment are directional, not pixel-perfect. Preserve frozen published roster facts and ordering, real event/team/member/role/draft facts, participant visibility and routes. Dark changes tokens only; narrow layouts recompose without clipping. Development reset gives finalized positive fixtures active frozen publication snapshots; `test-98-missing-playing-assignment` remains the intentional unpublished negative. | Approved — user manual acceptance after bounded masthead, roster, and draft-results corrections, 2026-08-24 | Preserve the approved Teams page and its independently constrained masthead/content geometry; wait for explicit authorization before another page family |
| Public board/evidence / `/Events/{slug}/Board`, `/Events/{slug}/Board/{teamSlug}`, `/Events/{slug}/Board/{teamSlug}/Tiles/{tileId}`, `/Admin/Events/{id}/Preview/{teamSlug?}/{tileId?}` | Visitor compares teams, progress, and approved evidence; Admin previews the same public ecosystem | Public/participant workspace | Reactivated PUB-REF-02, PUB-REF-03, PUB-REF-04, PUB-REF-14, and PUB-REF-15; existing authoritative Board/TeamBoard/Tile routes and projections | Structural rewrite: reference-owned event tabs and masthead using the shared Wide page width and normal responsive gutter; preserve the current near-target team-overview grid beneath the masthead and add the missing PUB-REF-02 Recent Activity footer; compact content-driven fact/action rail with tight countdown, leader, selected metric, and action cluster; ordinary full-page TeamBoard with summary header, statistics rail, dominant tile grid, View all teams, and adjacent-team navigation; tile routes replace the left rail; submission remains its attached drawer; evidence remains a focused modal viewer. Preserve authoritative data, route/history, focus, authorization, responsive, and realtime behavior. Protected `/Evidence/{id}` remains only the file-download dependency for evidence images/lightboxes. | The current screenshots are rejection evidence, not targets except that the user explicitly protected the overview grid beneath the masthead as already close to target. The prior popup decision is superseded: ordinary team-card clicks navigate to the TeamBoard page at every viewport. Dark is token-only and keeps outlined tile numbers. Admin Preview inherits this family's redesign status and is not an independent visual pass. A reskin of the rejected masthead or team workspace is explicitly non-conforming. Board overview uses shared Wide with the normal shared gutter; this does not authorize Landing hero height or artwork. TeamBoard and nested tile routes now receive the shared Boards/Drops/Leaderboards navigation with Boards current, while the tile sidebar retains its route-backed/enhanced Team overview action; the normal View all teams route remains in the masthead. | Approved — Board overview, TeamBoard, nested Tile view, attached submission drawer, evidence lightbox, Recent Drops, Leaderboards, and the final TeamBoard sidebar/masthead corrections manually approved by 2026-08-26 | Preserve the approved Board/evidence family and behavior |
| Authentication and errors / Login, Onboarding, AccessDenied, Error, StatusCode | User signs in and understands access or application failures | Editorial authentication/status compositions | PUB-REF-09; shared shell/focus rules | Standalone Login split, Onboarding steps, and shared numbered status composition | Discord completion remains an immediate non-rendering redirect | Approved — user manual acceptance, 2026-08-23 | Preserve approved page bodies, routes and behavior |
| Account settings and overview / `/Account/Settings`, `/Account/MyAccounts`, `/Account/MyEvents` | User manages identity and linked characters and revisits events | Standard settings + account overview | PUB-REF-07 and PUB-REF-10 | Approved forms, ordered-character behavior, account/event rows and account secondary navigation | My Accounts and My Events use their broader reference-owned compositions | Approved — page-specific user manual acceptance by 2026-08-24 | Preserve approved pages and shared navigation |
| Account security and recovery / `/Account/ChangePassword`, `/Account/ForgotPassword`, `/Account/ResetPassword`, `/Account/Setup` | User changes, recovers, or establishes credentials | Narrow account form | PUB-REF-08 and the approved Settings family | Existing password/security rules, validation, safe return, setup authority and handlers | Setup remains operator/security scoped; no password-policy change | Approved — page-specific user manual acceptance completed by 2026-08-26 | Preserve approved security, recovery, and Setup pages |
| Notifications / `/Notifications` | User reads and manages personal notifications | Public inbox | PUB-REF-11; notification inbox behavior | Read/unread state, semantic status, timestamps, wrapping, mark-all-read, empty state and destinations | Personal read state remains independent | Approved — user manual acceptance, 2026-08-24 | Preserve approved page and behavior |
| Privacy / `/Privacy` | User reads authoritative privacy information | Guidance/editorial | PUB-REF-12 real-content composition | Authoritative privacy meaning and real content | Static hierarchy may adapt without changing legal meaning | Approved — user manual acceptance, 2026-08-24 | Preserve approved page and authoritative copy |
| How To / `/HowTo` | User reaches the temporary guidance placeholder | Guidance/editorial | Current route and ordinary Public UI masthead text stack | Masthead-only placeholder with `HOW TO PAGE` kicker, `WORK IN PROGRESS` title, localized supporting copy, route, and shared shell | Future permanent Rules/how-to content remains a separate F-06 product decision and does not block deploying the placeholder | Deployment ready, not approved — user decision, 2026-08-26 | Preserve the masthead-only placeholder until F-06 is separately reopened |
| Dashboard/action inbox / `/Admin` and notification destinations | Admin enters the operational workspace | Admin shell + temporary content surface | Shell-owned WIP surface; existing dashboard markup retained inertly | Admin authorization, shell navigation, notification owner and recoverable dashboard source | `/Admin` intentionally renders only the Admin-background `WIP` content surface | Deployment ready — intentional WIP presentation, user decision, 2026-08-24 | Preserve until the dashboard is explicitly resumed |
| Whole-application regression / all routes and states | Release owner verifies the complete application | All families | `UI_SYSTEM.md` plus this matrix and manual/release gates | Desktop/mobile, keyboard, permissions, errors, realtime, privacy, functional regression | No page approval substitutes for this gate | Pending release gate | Release verifier after participant submissions |

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
| PUB-REF-12 | Guidance and editorial: `docs/references/public-ui/guidance-editorial-reference.png` | `/HowTo` structural family, Privacy real-content structure, editorial typography, local navigation, callouts, media and narrow recomposition | HowTo is structure-only pending F-06 and does not authorize invented final copy or workflows. Privacy uses its authoritative real content; examples demonstrate hierarchy and responsive behavior. |
| PUB-REF-13 | Public Signups directory: `docs/references/public-ui/public-signups-directory-reference.png` | `/Events/{slug}/Signups`; confirmed/waiting tables, counts, capacity/status summary, empty groups and narrow participant rows | This is an exact-link/Discord-link directory: the pictured sibling navigation is non-authoritative and must not be added by this reference. The directory may inform table/rule treatment but does not define the different `/Events/{slug}/Teams` roster composition. |
| PUB-REF-14 | Recent Drops: `docs/references/public-ui/recent-drops-reference.png` and `docs/references/public-ui/recent-drops-states-reference.png` | Board `?view=drops`; latest approval, earlier approvals, metrics, search/team filters, pagination, no-drops, no-match and narrow/mobile states | Reuses the protected Board/event navigation and behavior; the references own the Recent Drops composition only. Dynamic evidence facts and permissions remain authoritative. |
| PUB-REF-15 | Tile detail and evidence viewer: `docs/references/public-ui/tile-detail-evidence-reference.png` | `/Events/.../Tiles/{tileId}` and protected `/Evidence/{assetId}` file downloads consumed by the lightbox | Preserve the protected route-backed tile/sidebar, overlay, focus/history and submission behavior. `/Evidence/{assetId}` is a file-download dependency, not a rendered page. Artwork/evidence is content; the reference owns placement and hierarchy, not specific sample drops. |
| PUB-REF-16 | Public Teams/roster: `docs/references/public-ui/public-teams-roster-reference.png` | `/Events/{slug}/Teams`; event masthead, final-team groups, member-role hierarchy, draft results and large-width density | The accepted image is compositional direction: its sibling navigation is non-authoritative for this exact-link/Discord-link directory. Integrate the existing Landing-family diagonal DK artwork rather than reproducing its slightly detached generated placement, and tune spacing against the real content. No team images or role icons. Use whitespace instead of an inter-team divider grid; distinguish Captain/Co-captain by semantic text color; show two draft picks per row at large widths and one when constrained. Preserve authoritative event timing, roster/draft ordering, privacy, permissions, routes and empty states. |
| PUB-REF-17 | Captain team operations: `docs/references/public-ui/captain-team-operations-reference.png` | `/Captain`; team focus controls, submission-health totals, complete team ledger, filters, detail/feedback access and responsive states | Reference owns the three-part operations hierarchy and deliberately excludes a duplicate board, submission form and review controls. `Replaced` is a derived presentation from the existing resubmission relationship, not a new stored status. The open upper-right balance is directional: implementation may place one compact existing authoritative event/team fact there, preferably current evidence code or cutoff when applicable, but must not invent a decorative dashboard feature. |
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

Signup Questions, Captain team operations, Admin evidence review, Finalize/Audit,
and the remaining Admin UI families are not approved by the approved Admin
baseline. Public Board behavior/interactions remain accepted, while its old
appearance is superseded. Atomic landing Pass 1A is manually accepted as of
2026-08-22. Signup and Confirmation are manually accepted as of 2026-08-22;
standalone Login, Onboarding, and the AccessDenied, StatusCode, and general Error
pages are manually accepted as of 2026-08-23. The current `/HowTo` route is
deployment ready; F-06 governs only a future permanent content replacement.
The newly approved participant-submission route split is the remaining
Public/participant implementation family before final whole-application
regression: ordinary current-team history uses `/Submissions` and
`/Submissions/{id}`; evidence notification routing is recipient-specific as
defined in the Participant submissions row. F-04 and the future F-06 decision
remain unresolved.

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
`View Bingo`, `Recent Drops`, and `Leaderboards` views, and `/Captain` plus
`/Captain/Submissions/{id:guid}`. Existing right-side facts, controls, artwork,
lifecycle/capacity columns, metadata, rails, actions, and lower page geometry
remain named family ownership. Landing, Signup Confirmation, TeamBoard, Tile,
Login, Onboarding, AccessDenied/Error/StatusCode, and the masthead-only HowTo placeholder are
explicit exceptions. This is a bounded prerequisite before Board work resumes;
it changes no family approval state, and all affected pages remain subject to
user visual recheck rather than being newly approved.

The Captain workspace pass should review the neutral “Team workspace”
terminology raised by historical F-07. This is an unapproved terminology review
item, not a source-UI change; Captain-only readiness language remains explicit.

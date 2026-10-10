# Delivery plan

This document owns the delivery procedures, release and deploy gates, the standing
catalogue and WOM-window decisions, and the register of bindings the design
references do not show. It describes only what is in force. History lives in Git,
PRs and the planner's review notes. Page approval is owned solely by
[`UI_PAGE_MATRIX.md`](UI_PAGE_MATRIX.md); the current checkout, blockers and next
action by [`CURRENT_STATUS.md`](../CURRENT_STATUS.md); global UI rules by
[`UI_SYSTEM.md`](UI_SYSTEM.md).

## Standing decisions

### WOM window and end synchronization (AU20)

Decided 4 October 2026 (WA-2, WA-6, WA-9). The former actual-window tolerance
would make exact final-review matching fail; the following is its approved
replacement.

1. Compare WOM start/end exactly with the website's **configured** UTC start/end
   at every stage, including Final Review. Actual instants still drive upload
   cutoff, drop eligibility and the review window; never substitute them for this check.
2. Early end and Resume always succeed locally within ordinary lifecycle rules;
   WOM availability or a provider rejection never blocks the local action.
3. Early end sets WOM end to the click instant rounded **up** to the next whole
   minute (21:59:55 → 22:00:00), and persists the same configured end locally.
   Actual end retains the precise click instant; no lead time is required.
   Resume requires the Admin's validated replacement future end, using existing
   schedule increments; store/use that end, never the resume-click time, with no
   click-time rounding. The three former tolerance sites are shared linking/
   replacement/fetch validation, Schedule save, and **Resume**.
4. Retry temporary update failures with spaced backoff, never a fast loop. WOM
   accepts edits ending in the past provided end is not before start; retries may
   continue until official results publication. HTTP 400
   `COMPETITION_START_DATE_AFTER_END_DATE` is a non-transient rejection.
5. While WOM end is unmatched, run no fetch after actual end: that could count
   post-end gains. After successful end update, normal fetches resume with the
   correct window. If still unmatched at publication (prolonged outage, absent/
   invalid verification code or non-transient rejection), mark the event
   **“WOM end could not be updated”**, skip the final fetch and use the last fetch
   before actual end as official WOM data. Luck freshness shows that fetch's age;
   no invented fresh values or zeros. ID-only external links without a verification
   code always take this fallback after early end.
6. Preserve external deletion prohibition and existing cooldown, scheduled slot,
   lease, operation and history guards. Provide structured eligibility/reason/
   next-permitted-time/credential/operation outcomes; AU15 retains generic cooldown
   wording. External replacement before/during Live and disconnection before first
   Live follow approved option 1 regardless of stored/rejected code, exact configured
   window and active/unresolved guards; never reuse the old code or delete externally.
   WA-9: external linking also waits for Sending/Unknown website Create, so its late
   completion cannot overwrite a newly saved link. Readback never performs a fetch
   and old success does not prove a new operation.
7. Accepted limit: WOM end values use each player's last snapshot inside the window;
   precision depends on player updates. Users know to log out immediately before
   the end. This limit is accepted, not a new compensation or attribution algorithm.

Focused acceptance: deterministic sub-minute early end, minute-boundary end,
validated future Resume, exact/timezone-equivalent configured windows through
Final Review, non-microsecond input through real PostgreSQL precision, each old
five-minute tolerance site, transient retry spacing and non-transient rejection,
unmatched-end fetch suppression/success/fallback/publication, ID-only credentials,
external delete refusal, Sending/Unknown Create-link race and late responses after
replacement. Use controlled provider doubles, never live WOM. The early-end texts
in DATA_MODEL, TECHNICAL_ARCHITECTURE and PRODUCT_REQUIREMENTS describe the
implemented behavior.

AU20 error classification is a **planner technical decision**: 5xx, timeouts, network failures,
408 and 429 are temporary. Named 400, missing/invalid code, 401/403/404 and other
validation rejections are permanent. For an end-only unknown write, a read of the
unchanged old window proves not applied and permits spaced resend; the target
window confirms success. Other unknown operations keep their existing protections.

**User decision D6 (option a):** “publication stops the WOM end update for good,
including after a reopen.” Reopened/republished results keep the same WOM basis;
no retry restarts. Source: `08-decisions.md`, B3 review decisions (planner review notes).
with the verbatim section in supplied decisions.


### Catalogue decisions (AU23, CAT-1, WA-5)

Source: 4 October user decisions in the durable remediation `decisions.md`,
superseding earlier proposals in `06b-wom-catalogue-accounts-audit.md` WA-5 and
Catalogue C7 / AU23 mapping.

- **AU23 / final chance:** always enter the final chance for the specific item in
  one's own name at the agreed team size; conditional raid-table chances must be
  resolved before entry. Retire “Only after” from new input, editor and panel;
  keep `conditional_on_parent`/`parent_probability` columns, existing records and
  immutable snapshots/history. **No EHB parent-chance fix ticket.** The user queried
  production on 4 October and reported zero conditional rows; this is operator
  evidence, not our verification. Existing runtime differences for legacy conditional
  records are not rewritten by this ticket; stop if the pre-change check finds any. The pre-check was user-approved on 4 October 2026.
- Rate text, including `N x`, is ordinary Admin add/edit/reactivation input. B4
  removed the current refusal at `Catalogue/Index.cshtml.cs` while retaining
  validation/concurrency/audit. `default` is the group
  for every new drop, including on newly created activities. Existing production
  groups (`barrows-equipment`, `purple table`, `doom-1-16-aggregate`,
  `fortis-full-run-unique`; 56 active rows reported by the user) remain unchanged.
  Roll groups are calculation inputs, SuperAdmin-only. The legacy probability-scope/
  assumed-participant columns remain historical context after CAT-1 moves the
  informational value to the activity; changed per-drop context input is refused
  rather than rewritten. Parent fields are retired from new input, not opened to
  either role. No separate roll-count input.
- **CAT-1 — move informational agreed team size to activity (approved, A1 after
  AU23; before WA-5):** one integer >=1, default 1, for the boss/activity, beside
  efficient kills per hour. The rate and EHB use the same agreed efficient strategy;
  this field is explanatory and never enters a calculation. Every Admin can edit it.
  Move the context from per-drop `assumed_participants`/`probability_scope` without
  changing existing approved/published snapshots or history. The user reported zero
  non-default per-drop contexts on 4 October; re-query immediately before migration
  for set values and conflicts within each activity. A new set/conflict requires a
  user decision, never silent loss/coalescing. Required migration/designer/snapshot
  travel together. Verify default, validation, Admin edit, no calculation effect,
  retained groups/history and rollback shape with controlled PostgreSQL fixtures.
- **WA-5 binding:** the “How the rate is counted” header displays the actual group
  (`default`, `purple table`, etc.). Rows are chance per kill (including `N x`),
  source, and note only if present. Bottom: “Roll group can only be changed by the
  Super Admin.” SuperAdmin edits group in that panel; ordinary Admin sees it read-only.
  Remove chance-per-roll, rolls-per-kill, whose-chance and Only-after rows, the
  operator sentence, and the rate-roll refusal in frozen `Catalogue.dc.html:378`
  and `:801` when binding. Put editable informational **Team size** next to **Kills
  per hour** in Settings and Add activity, with the same fields/validation. This
  deliberately supersedes those portions of the frozen reference. Do not edit that
  artifact in this cleanup; RC10/WA-5 later implement the decision.

Recorded observations have no separate ticket: X-5 is recorded here for the
integration owners (PageModel mutations rely on the per-request cookie recheck and
must not be treated as persisted authorization), while BR-11's 3 October reported production
zero must be re-run with the corrected query; it is not release-pass evidence. These observations do not start
implementation or widen any ticket.

### Bindings not shown in the design references

Established by the user's 4 October instruction: “From now on, every ticket that adds
functionality the references don't show adds its row.” Seeded from `review-notes/08-decisions.md`,
“Known UI differences from the frozen design references”, with Catalogue decisions
owned by the section linked below. This register includes missing bindings and
intentional differences from a reference; it does not authorize implementation,
reference edits or new displays on the current pages. All AU work targets the new UI.

Every ticket that adds functionality the design references do not show must add or
update its row here, including backend-only outputs awaiting binding. The UI
integration plan must make every row a required item in the affected page's
route/handler/binding inventory, resolve its open decisions before binding, and
include it in binding review. Keep decisions in their existing owner and link them
here; do not silently omit a row because the reference has no corresponding control.

| What it is | Page and design reference | Source ticket/decision | Binding ticket | Open decisions |
| --- | --- | --- | --- | --- |
| Catalogue loading counts use the exact shared tab-count placeholders; loading summary has no row gap and reserves two phone lines / one wider line. Loaded summary geometry and every fixed word remain unchanged. Generic save timing checks declared POST counts and busy-wrapped save paths, excluding search debounce | Catalogue loading-header markup/row gap standardized under Q6/Q7; no loaded-reference difference | User/planner CI1–3, 7 October2026, 08-decisions “Catalogue integration boundary after the T2 merge” | U3/T2 integration correction | None; all five widths in both engines, Accounts save assertions retained |
| Unknown stored event timezone IDs are retained; Schedule and Signup setup explicitly label UTC fallback dates and link to Identity. Schedule remains read-only until the timezone is supported. Any page initialization failure shows page recovery without a connection toast and releases shared navigation | Identity.cshtml:142 precedent; Schedule/Signup references do not show unsupported stored IDs or initialization failure | User 7 October 2026, 08-decisions “Bug, Schedule on an unknown-timezone event” | U3 post-batch item 3 | None; no stored instant, history or save-policy change |
| Links targeting a different registered page use data-shell-link and shared navigation/skeletons. Same-page query/sort/drawer links retain their existing in-page handlers and tests; old-layout Overview remains a normal link | Schedule.dc.html:117; SignupSetup.dc.html:127; cross-page binding not shown in static references | User7 October2026 D resolution, 08-decisions | U3 second-look follow-up1 | None; generic assertion excludes only same-page targets |
| Schedule and Signup visible timestamps use non-padded day, localized abbreviated month and fixed comma/HH:mm separators, including reason notes, comparisons, derived cutoff and first-response note. Confirmed counts use “N of M”; capacity number input keeps reference .ss-num sizing while only its column flexes | Restores Schedule date consistency; SignupSetup.dc.html first-response note at1388 shows day only, now full timestamp by user ruling | User second-look item5, 7 October2026, 08-decisions | U3 follow-up5 | None; EN/DA covered; transport values/precision unchanged |
| Every shared read-only box shows the existing lock glyph in the date-picker calendar slot, with reserved right padding for wrapping. Field reason notes keep their text but lose the duplicate lock; “A code is set” keeps its list lock | Extends U3-Q13(a); ui/components.css .ro-value and field-lock notes differ intentionally | U3-Q14, user7 October2026, 08-decisions | U3 follow-up4 | None; applies fully readonly/past/imported and drawer values; app/reference CSS byte-identical |
| Shared .ro-value uses the readonly date-picker box: control height, border/radius, surface-2 background, full-color text and no hover change; multiline values grow and .is-empty remains muted. App/reference shared CSS remain byte-identical | Intentional difference from ui/components.css:695–697; follows .dtp.is-readonly at:873 | U3-Q13(a), user7 October2026, 08-decisions; explicit shared/frozen CSS authorization | U3 second-look remediation F; Identity/Schedule/Signup setup/drawer | None; readonly states at allfive widths in both engines |
| Signup setup capacity card places the input and unchanged waiting-list hint in the remaining-width left column, beside a top-aligned 210px count box with24px gap; narrow widths stack counts below the left column. Code card unchanged; page-family CSS only | Differs from SignupSetup.dc.html:171–185 capacity-card layout | U3-Q12(b), user7 October2026, 08-decisions “Signup setup early look” | U3 second-look remediation E | None; measure390/494/860/1280/1440 in both engines |
| Signup setup maximum players is capped at10,000 in the domain and every service changing path, including Participants add-one-place overrides; page-only Range copies are removed | SignupSetup.dc.html:740 has no upper bound; this is an approved reference difference | A-SignupSetup-1 / U3-Q1(a), user7 October2026; brief80 item2 | U3 item2 / OS-1 | None; server boundary and clear error required |
| Imported events without a SignupForm, including after reopening/finalizing, show stored Settings read-only and “No signup form was recorded for this imported event.” on the form tab, with Danish; Current explicitly reports absence, no historical data created | SignupSetup.dc.html:519–520 assumes standard form fields; absent historical-form state differs from reference | U3-Q11(a), user7 October2026, 08-decisions “U3-Q11”; review84 B-L1 absence guard | U3 item3 / OS-1 | None; D17 read-only access preserved |
| Signup setup keeps only “Dates are on Schedule; opening and closing signups on Overview.” with both links and Danish translation; shared Q6 reservation unchanged | Differs from SignupSetup.dc.html:127 and:1551 | U3-Q9(a) / Q10; user7 October2026, 08-decisions “Summary reservation for every page” | U3 item3 / OS-1 | None; no page override |
| Signup setup capacity, code, Current, adds and form operations classify session loss and retain entries; immutable card attempts, stable-ID readback and exact add receipt replay | SignupSetup.dc.html; reference labels do not prove an add | C-CMP-2 / AU05 / AU06; RC02 R1–R4; AU07 | U3 item3 / OS-1 | None; optional normalization retained |
| Schedule exact-WOM-window refusal is shown on Event start and Event end without rounding the configured instants | Schedule.dc.html does not show these service refusals | AU20; brief80 item1 planner ruling | U3 item1 / OS-1 | Bound to existing field errors; exact precision unchanged; Schedule accepted by the user 7 October 2026 (U3, `UI_PAGE_MATRIX.md`) |
| Schedule save POST and full-tuple Current GET classify login redirects and retain unsent local date/time entries; failed read remains Unknown, matching read is Up to date without attributing the earlier request | Schedule.dc.html recovery plus fetch-boundary behavior not shown | AU10 / C-CMP-2; brief80 item1 | U3 item1 / OS-1 | Focused browser and transport proof; Schedule accepted by the user 7 October 2026 (U3, `UI_PAGE_MATRIX.md`) |
| Schedule shared picker retains exact unchanged instants, rejects ambiguous/skipped changed times, and fits short viewports; capacity is absent from the six-value readback | Schedule.dc.html; RC03 R1–R4; direct Overview consumers remain U4 | RC03 / OS-5 / brief80 | U3 item1 / OS-1 | Scoped browser, precision and transport proof; Schedule accepted by the user 7 October 2026 (U3, `UI_PAGE_MATRIX.md`) |
| Schedule summary drops the event-stage sentence and uses one item: “Times are in ‹timezone›; change it on Identity. Open, close or start by hand on Overview.” Both destinations remain links; Danish is localized. Shared Q6 stays two lines ≤640px / one wider, with no Schedule override | Intentionally differs from Schedule.dc.html:117 and :875 | User 7 October2026, 08-decisions.md “Summary reservation for every page” | U3 item1 / OS-1 | None; measure wrapping at390/494/860/1280/1440 |
| Count summaries retain their fixed words during loading and use the same number-sized skeleton markup as Events tab counts. Events shows “[bar] live” and “[bar] upcoming or in setup”; attention and no-attention content appears only after data arrives. Q6 height remains two phone lines / one wider. Identity and Dashboard keep empty data summaries | Events.dc.html loading summary is empty: this intentionally differs from the reference. Optional conformance declaration applies to every count-summary page | User decision 7 October 2026, 08-decisions.md “Count summaries load like the tab counts” | U3 item0 follow-up / later registered pages | None; no lane T edits |
| Shared phase badge / dot palette intentionally differs from reference page maps: Setup neutral / grey (`tone-draft`, unchanged); Signups open success / green; Signups closed info / blue; Live accent / teal; Final review warning / amber; Finished done / violet; Archived and Cancelled outline / grey. R1 Final review dot takes warning foreground; R2 Setup retains `--dk-text-3`; R3 Dashboard Finalized uses done, Provisional stays warning, Imported · archived stays neutral (provenance); R4 `.dc.html` files/local grey maps remain unchanged, U3+ uses `AdminDesignPhasePresentation.For(EventState)` rather than copying them. Shape, size, position, labels and non-phase tones unchanged | Events, shared sidebar and Dashboard; `Events.dc.html` phase map and `Dashboard.dc.html` recap remain reference differences. Frozen-file byte identity is kept for both reference/app token and component CSS pairs | Q-C1(c), Q-C2(a), brief78, 7 October 2026. Starting values unchanged. Text contrast (badge on page / badge on card / page / card): blue light 5.9736 / 5.9736 / 6.4351 / 6.9676; blue dark 7.8471 / 7.2260 / 9.5633 / 8.9351; violet light 6.1804 / 6.1804 / 6.7025 / 7.2571; violet dark 7.5819 / 6.9858 / 9.1618 / 8.5599; all ≥4.5:1, translucent dark backgrounds alpha-composited over each host | Brief78 shared palette; U3 and all later new-layout phase bindings reuse helper. Values and measured evidence | None; direct planner commit check then light/dark Events and sidebar visual check pending |
| Every new-layout loading header reserves two summary lines at the shared phone breakpoint (≤640px), one above it. Data summaries stay empty; count summaries use the fixed words and numeric placeholders declared by the 7 October count-summary decision. The loaded reference header keeps its real height; following content settles by the resulting height difference. No page exceptions | Differs from the reference loading-summary height: Identity.dc.html and other page loading headers | User U3-Q6(a), 7 October 2026 phone addendum to “Summary reservation for every page”; supersedes U3-Q5 all-width one-line rule | U3 item0 / shared page conformance; every later page | Bound for Identity, Dashboard and Events; loaded reference geometry retained |
| Loaded page/cards and Events in-page results swap without load fades. Remove page-load fade-in markup and suppress the fade-in/results load markers plus loaded empty states, page notices and the static unsupported-timezone note in page-family CSS; fast loads are atomic, slow loads keep the shared skeleton. Menus, modals, drawers/scrims, new-event row flash, rows.swap-a/b sort and inline errors retain their interaction animations. Applies to every new-layout page now and later; future page-conformance checks carry this rule | components.css:452 (page fade-in), :337 (results fadeIn), :257/:264 (empty/notice fadeIn), :662 (static note); Dashboard.dc.html:156–346, Identity.dc.html:136. Intentional load-animation exception; frozen tokens/components remain unchanged | User “No load fades”, 08-decisions.md, 7 October2026; brief76 item5 addendum | U2 remediation round4 item5; future new-layout families | None; both engines inspect content animations at insertion/native frames, fast/slow page and Events-result loads; interaction rules remain active |
| Skeleton text bars sit in typography-sized one-line rows in Dashboard, Events and Identity. Dashboard headline reserves the loaded one-line label/value/note total; reference bar sizes and visible gaps remain unchanged. The first body card aligns; the participation section moves only by real label/note wrapping, not an intrinsic skeleton-height shortfall. Non-text chart/recap/input/badge/meter blocks retain their reference geometry; fresh text reservations never use remembered data | Dashboard.dc.html:130–139; Events loading rows; Identity.dc.html:118–120; components.css:463–475 and corresponding text typography. Intentionally differs from reference skeleton row heights; frozen files unchanged | User Q-SK2(a), 08-decisions.md “Q-SK2”, 7 October2026; brief76 item4 correction | U2 remediation round4 item4 | None; first-card/participation-section body proof at390/494/860/1280/1440, Chromium/WebKit, exact independently measured text-wrap growth |
| Every page stylesheet is family-scoped with a zero-specificity :where([data-page-family]) prefix; loaded page and fresh skeleton carry a declared fixed untranslated PageFamily key. Remembered skeletons retain stylesheet hrefs; destination CSS loads active at navigation start and is awaited before showing that family's skeleton or swapping its loaded region. If CSS is not ready at150ms the current page remains visible;400ms starts at actual skeleton display (inherited shown holds transfer unchanged). An unknown family uses the shared-only generic skeleton. Old styles stay until old content is gone, identical loaded href nodes stay in place; stale null-sheet links reload, errors use the existing full-load fallback. CSS wait belongs to total A16 timing; language uses the same readiness boundary without a skeleton | Dashboard/Events/Identity reference page rules retain their declarations and cascade; prefixes isolate application page ownership without changing appearance | 08-decisions.md “Brief 74 item 5b approach”; brief76 items1–2, 7 October2026 | U2 remediation round3 item5b / round4 items1–2 | None; page-selector guard, native-frame/exact-clock CSS-readiness, reload and cancellation proofs both engines; selector mapping in item5b-selector-bindings.json |
| Shared loading timing: A16 navigation and asynchronous in-page updates keep current content visible for150ms, then show the reversible skeleton for at least400ms. Fast updates replace content atomically without loading UI. Full A16 navigation temporarily makes its region inert; in-page query controls stay interactive and retain typed input. Failure/dirty/address-bar rules unchanged. Dashboard server-order sort is synchronous and never reaches the threshold | Shared shell / all opted-in pages — references show loading immediately without these shared thresholds | User decision C; brief70 item6; brief72 item1 query-control preservation | U2 remediation item6 / round2 item1 | Values are single named settings beside busy minimums; exact fake-clock boundaries in Chromium/WebKit; tuning requires the user's visual check |
| Events in-page query changes use shared results-only update: rows/empty state, pager, chips, attention banners and counts update from the server; chosen tab, Phase value, sort indicator and pager state paint immediately from the latest intent, persist on failure and retry that same intent; header, summary, tabs/counts, toolbar/search and table scroller retain their nodes. Summary/count values stay visible during loading and refresh in place. Skeleton affects results only after150ms for at least400ms. Focus/caret/selection/typed input and vertical/horizontal scroll retained; superseded reads aborted; URL replace and Create query unchanged | Events.dc.html:541–578; A16 per-page transition choice. Full navigation into Events uses the fixed count-summary words and numeric skeletons approved on 7 October | User visual checks round2/3; brief72 item1 and brief74 items1–4 | U2 remediation rounds2/3 / C-CMP-2 | None; exact fake-clock/identity/focus/scroll proof in Chromium/WebKit |
| Linked-but-incompatible stored EHB remains unavailable (—), with localized “No compatible stored EHB coverage is available.” rather than claiming WOM was unlinked. HasLinkedCompetition describes an actual stored competition link independently of strict fingerprint/coverage compatibility | Dashboard.dc.html history EHB hint does not show incompatible stored coverage | DB-2/D-8; 71a F4/F6; brief72 item6 | U2 Dashboard / round2 item6 | None; linked/unlinked PostgreSQL round-trip proof; compatibility unchanged |
| Unknown platform approval remains unavailable (—), with localized “Published board approval or a usable event interval is unavailable.” in headline, recap and history as applicable; unknown is not zero and is not imported reconstruction | Dashboard.dc.html assumes usable board approval and dates | DB-3/D-8; brief70 item3 retained states; 71a F4; brief72 item6 | U2 Dashboard | None; preserves existing approved unavailable-state binding |
| Unknown actual lifecycle interval uses localized “A usable actual lifecycle interval is unavailable. Unknown is not zero.” for unique/total participation hints instead of a fabricated count or raw unavailable reason | Dashboard.dc.html assumes usable actual lifecycle intervals | DB-4/D-8; 71a F4; brief72 item6 | U2 Dashboard | None; existing unknown metric contract preserved |
| Chart accessibility label represents unavailable participant measurements as unavailable, never as zero; no measured bar segments are invented | Dashboard.dc.html chart fixtures have measured participants | D-8/D-14; 71a F4; brief72 item6 | U2 Dashboard | None; same localized unavailable value as visible bar total |
| Latest approved-submissions hint ends with localized “Figures are provisional and may still change.” for Live, instead of the ended Final review wording; Live has no actual ended date | Dashboard.dc.html approved-submissions hint only shows ended review history | D-8; accepted U2 Live wording; brief70 item3; 71a F4; brief72 item6 | U2 Dashboard | None; existing Live/Final review distinction retained |
| Dashboard history sort reorders existing rows in place from all server-computed orders, retaining focus and scroll, and replaces the query URL. No page fetch, remembered measurements or skeleton; this synchronous update completes below the shared loading threshold. Other page transitions remain shell swaps | Dashboard.dc.html history sort; A16 transition choice is per page | Brief70 item3, 69a M3; page-local re-render of server data explicitly authorized | U2 remediation item3 | None; server owns null-last ordering and tie breaks; browser proves focus, scroll, no document request and replace semantics |
| English/Danish changes without reload: highlight after the dirty guard, save through existing language POST, swap translated shell/page with no skeleton, retain sidebar/scroll/focus; theme-duration cross-fade (none for reduced motion), reload fallback | Shared Admin shell — language control absent from frozen references | 5 October U1 visual check, Language switch without reload; brief55 item0/2 | U1 shared shell | None; Keep editing retains draft and old language; server POST behavior unchanged |
| Dashboard A16 loading header follows the reference layout with the shared U3-Q6 empty-summary reservation (two lines ≤640px, one wider) and a fresh card-sized .sk placeholder, desktop beside title and ≤860px full width below. Loaded header follows the reference exactly, with wrapping summary and data-sized card. Movement derives only from real summary height/card size, including bottom-aligned title, card position and following content; no fixed anchors or layout exception. Failure has no card/placeholder; never remembered data | Dashboard.dc.html:106–124 and :722 retain the real card during loading/failure; components.css:104 bottom-aligns header groups | User option(b), Q-H1 option(a), corrected by 08-decisions.md “Brief 72 item 5, Q-H1 geometry”, 7 October 2026 | U2 item2 / brief70 item3 / brief72 item5 | None; loaded and loading reference parity plus flow proof at390/861/1280/1440, normal/narrow fixtures, Chromium/WebKit; frozen reference unchanged |
| Events A16 loading/failure keep query-bound tabs, toolbar and table headings. Loading uses same-size .sk count slots; the summary is empty with the shared U3-Q6 reservation (two lines ≤640px, one wider), no skeleton bar. On load it takes its real height; following content moves only by that height difference. Only the loading header has width/height reservations; loaded header follows the natural reference layout and flex wrapping, including attention. Failure clears counts and summary without placeholders and shows Try again. Every visit is fresh, no remembered counts | Events.dc.html:135–169, :223–234 and :993–1006 retain in-memory counts and query controls; first server visit has no counts; components.css:108 owns loaded summary wrapping | User loading/failure decision; brief70 item5 decision A; brief74 item5a; Q-SK1(a), 08-decisions “U2 remediation round3 recheck”, 7 October2026 | U2 item3 / EI-2; remediation rounds1/3/4 | None; reference geometry and exact summary-growth flow at390/640/860/861/1280, two count presentations, Chromium/WebKit; frozen references unchanged |
| Remember the last accessible Admin event in a browser-session cookie containing only its ID; community pages retain its sidebar/navigation without an event breadcrumb; revalidate every render and clear on sign-out or loss of access | Shared Admin shell — prototype retains selected event in page state | User, 5 October 2026, Last event stays in the sidebar; brief60 item2 | U1 / future bound community pages | None; past events retained; old-layout composition unchanged |
| Theme and EN/DA controls move into the hamburger navigation only at the existing max-width 860px mobile breakpoint; bell stays in topbar, wider layouts unchanged | Shared Admin shell — approved mobile placement difference, preserving readable event breadcrumb | User, 5 October 2026: “Move the light switch and language switch into the hamburger”; “This is obviously only on mobile widths.” | U1 round3 item7 | None; preserve theme/language transitions, dirty guards and keyboard access |
| Narrow-screen toasts sit above the sticky save bar and never cover Save; desktop placement and finite lifetime follow the reference | Shared toast / Identity — intentional phone placement difference | 5 October U1 visual check Q5; brief55 item0/5 | U1 shared components | None |
| Sidebar and account-menu header use the signed-in account's public username and role; menu header includes name and @handle · role; the logo slot holds the site's masthead SVG in white (U10 part 2 item 5, superseding the DK Legacy mark) | Shared shell — Identity.dc.html / Participants.dc.html use sample organization/account data | 5 October U1 visual check Q1/Q2; brief55 item0/3 | U1 shell | None; Administrator / Super admin localized from real account data |
| AU19 approval bindings group per-position `board-incomplete` issues into the reference’s single “(N empty)” item, jumping to and highlighting the first empty position; `Working` is the publication projection during a correction | Board — [Board.dc.html](docs/references/admin-ui/Board.dc.html) | AU19, `08-decisions.md` “AU phase plan”; review36c F6/F8 and brief38 item6 | RC05 / BR-10 | Bound in U7 (brief 88); reviewed (report 95), fixes rechecked at `d7a9e272`; merged `5ec88e93`; user accepted 8 October 2026 (provisional rulings and proposed wordings accepted; page approval in `UI_PAGE_MATRIX.md`). The two open points are resolved: U7-Q1 (rate issues carry the drop names) and U7-Q2 (Publish returns every refusal together) |
| Blocked approval/contribution state replaces any numerical approval claim; direct link to the earlier pending upload preserves queue/filter context | Review — [Review.dc.html](docs/references/admin-ui/Review.dc.html) does not show it | BR-1/G1 and B5 AU17; shared allocation returns structured blocking submission ID/upload time; `08-decisions.md` “Step 3 decisions” | RC07 / BR-10 | None; preserve the approved navigation and return context |
| Cap-limited Contribution wording: an exhausted or partial drop/item cap can limit Add while the objective still has remaining work | Review — [Review.dc.html](docs/references/admin-ui/Review.dc.html), Contribution line | B5 review B2; brief35 item 3; shared approval allocation | RC07 | Backend numbers verified; wording remains a binding decision, no current-page display added |
| Correction picker includes released Playing accounts/former team members, marked Released/Left team/Current; Informational never selectable | Review — [Review.dc.html](docs/references/admin-ui/Review.dc.html) shows a current-team account list without these markers | AU17a; D11 option b in 08-decisions.md; B5 `au17a.md` | RC07 / BR-10 | Scope decided; bind the markers and derived participant without independent participant editing |
| Uncertain Teams recovery retains immutable pick/team/member IDs and all intended fields, including inclusion/image/role/account; same order or reusable pick number never proves a request. Failed/unavailable reads remain unknown | Teams — [TeamsDraft.dc.html](docs/references/admin-ui/TeamsDraft.dc.html) predicates use reusable numbers, partial fields and changed order | AU14; Teams source findings 1–2; B5 `au14.md` | RC04 / DRF | Bind current-state wording; unavailable new-team creation identity remains uncertain; no automatic replay or safe-retry claim **Bound in U6 (1a/1b): every Teams command keeps its intended ids/fields and verifies by the no-store readback (pick by participant, Undo by the shown latest pick id — the server refuses a stale id, draw by positions, control by controller, team/member/role by id); “A team named X now exists. It isn’t known whether this request created it.”; a failed read stays unknown with Check again; reviewed (report 99); merged `2d83651e`; user accepted 8 October 2026** |
| Local roster publication (PublishedAt) is separate from existing last recorded WOM management/operation/local-queue outcomes; only operation CreatedAt >= roster PublishedAt establishes that the operation was created at or after the current roster publication; UpdatedAt cannot establish current-roster relevance (an older in-flight update can finish after republish); a merged pending update keeps its older CreatedAt and conservatively reads as older; queued/failed/unknown never means today’s roster is synchronized | Teams — [TeamsDraft.dc.html](docs/references/admin-ui/TeamsDraft.dc.html) claims WOM is updated after local republishing | AU14; Teams source finding 3; B5 `au14.md` | RC04 / DRF | Bind the existing outcome statuses honestly; no new provider operation or permission **Bound in U6 (1b): corrections and finalize show “Rosters republished.” only for a newer publication cycle than the page showed, then a separate Wise Old Man line (went through / waiting to be sent / failed / isn’t known / couldn’t be queued; nothing when the event has no managed group); no provider text; reviewed (report 99); merged `2d83651e`; user accepted 8 October 2026** |
| Omit the “another event is current” readiness row despite the reference; retain server lifecycle/finalization guards | Final Review — [FinalReview.dc.html](docs/references/admin-ui/FinalReview.dc.html) shows the row | Narrowed AU18 / Step 0 decision; no additional current-event readiness UI authorized | RC08 / BR-10 | None; omission is decided |
| Manual-team members in participant lists and waiting positions | Participants — compare with [Participants.dc.html](docs/references/admin-ui/Participants.dc.html) during binding | TD-2 option B; G3b-3 | P-1 | Verify the existing reference against the decided membership behavior during P-1 |
| Authorized single-entry Audit read, including “This entry isn't available” | Audit — check [Audit.dc.html](docs/references/admin-ui/Audit.dc.html) | AU16; B1/B2 decision D4 | RC06 / WA-5 | Bound in T1 item 4: `?entry=` opens the drawer over the first page, read by id with the list’s visibility and filters; “This entry isn’t available” otherwise; user visual acceptance 7 October 2026 (T1, `UI_PAGE_MATRIX.md`) |
| Audit event dropdown filter with hidden events marked | Audit — check [Audit.dc.html](docs/references/admin-ui/Audit.dc.html) | AU16; B1/B2 decision D4 | RC06 / WA-5 | Bound in T1 items 2/4: event menu ordered as on Events with state hints, hidden events marked “Hidden · state”, Discarded omitted (Q7); hidden-event entries listed with a Hidden pill (AU16 supersedes Audit.dc.html:390, :560); the menu scrolls when long; user visual acceptance 7 October 2026 (T1, `UI_PAGE_MATRIX.md`) |
| Audit areas (S11, Q5 (a)): nine areas instead of the reference's seven. Accounts, Events, Signups, Participants, Teams, Draft, Board, Evidence, Catalogue; areas are prefix sets plus explicit key lists (event.signup_*/capacity_increased/signup_code_changed/signup_administration_updated → Signups; team.member_*/membership_role_changed and roster.finalized_* → Participants); evidence_code.* and historical_import.* sit in Events (planner to confirm). Query names follow the reference (event id, action, actor, type, from, to, page, entry); C-AUD-3 actor ignores case and a leading “@”; C-AUD-4 invalid or unknown link parts are dropped with a notice; Discarded events are omitted from the event menu (Q7) | Audit — Audit.dc.html:425 (seven AREAS), README :2023–2026 | S11/Q5, C-AUD-3/4, Q7 in 08-decisions.md “T1 brief decisions” | T1 item 2 (lane T) | Area for evidence_code.*/historical_import.* not in Q5; user visual acceptance 7 October 2026 (T1, `UI_PAGE_MATRIX.md`) |
| Audit page bindings beyond the reference: Automated pill and “System” actor derived from a null actor account; drawer context shows reopening explanations and plain recorded details; sensitive actions (code, password, credential) show no field changes, before or after values; record name from the entry’s before/after snapshot, otherwise the record type (C-AUD-5 display only, Q6); every recorded action key labelled in English and Danish with a completeness test (S12); change labels sentence-cased; record types Tile, Signup question, WOM competition added | Audit — Audit.dc.html:190–194, :746, :759; README :2190–2200 | S12, C-AUD-5/Q6 in 08-decisions.md | T1 items 3/4 (lane T) | Writer-side record names are a main-lane item (Q6); user visual acceptance 7 October 2026 (T1, `UI_PAGE_MATRIX.md`) |
| Audit Changes table is human-readable (T1-8 (a), user): ids, versions, editing leases and concurrency markers (one named policy, `AuditPresenter.IsTechnicalField`) appear only in Technical details; rows that read the same before and after (including empty → empty) are omitted; decimals are rounded like the site's EHB values (“0.##”, current culture); ISO instants use the drawer's “When” format (Copenhagen, with UTC offset, English and Danish); the field count counts only the shown rows; when every change was technical-only the drawer says “Only technical fields changed. See Technical details.” instead of “No field changes were recorded…” (T1 review L1). Display only; the shared presenter also shapes Review Details history (`_AuditEntry`), which gains the same filtering and formatting | Audit — Audit.dc.html Changes section (sample entries have only readable fields); Review Details (U8) via the shared presenter | T1-8 (a) in 08-decisions.md | T1 (lane T) | None; user visual acceptance 7 October 2026 (T1, `UI_PAGE_MATRIX.md`) |
| Search fields avoid browser/password-manager username autofill (T1-9 (a), user): Accounts search reads “Search accounts”; the Audit actor field reads “Actor” / “Filter by actor” with the hint “Matches actors whose name contains what you type…”; Danish wording avoids “brugernavn”; both inputs keep autocomplete="off" and add data-1p-ignore, data-lpignore="true", data-bwignore and data-form-type="other"; no id or name contains “user” | Accounts — Accounts.dc.html “Search usernames”; Audit — Audit.dc.html “Actor username” / “Filter by actor username” | T1-9 (a) in 08-decisions.md | T1 (lane T) | None; user visual acceptance 7 October 2026 (T1, `UI_PAGE_MATRIX.md`) |
| Audit More filters panel protects unapplied edits (T1 review M2, decision B / A12): with edited fields, Escape, an outside click (which then does not act on what it hit) or the More filters button opens the shared discard confirmation; Keep editing keeps the panel and its values, Discard resets it to the applied filters; leaving the page uses the shared dirty guard. With no edits it closes at once. The panel also moves up when the space below the button is short (A4) | Audit — Audit.dc.html:668, :674, :793 close the panel and drop edits silently | Decision B / A12; “T1 review” in 08-decisions.md | T1 (lane T) | None; awaiting recheck |
| Audit header summary is only “Times in Copenhagen time (UTC±hh:mm)”: the page-describing sentence “Administrative history, newest first.” is dropped (loaded and loading; U3-Q10 planner ruling, the 390 px loading summary exceeded the two-line reservation). The loading header keeps this fixed text while loading; it is data-independent, so it is shown, not reserved (T1 review L2, applying “Count summaries load like the tab counts”). To be declared when Audit registers in the U3 page-conformance check | Audit — Audit.dc.html:129 (summary sentence; loading state not shown) | “T1 review” L2 and U3-Q10 in 08-decisions.md | T1 (lane T) | None |
| WOM refresh outcome follows the reference's unsuccessful-only note; stored next eligible time remains data only | Final Review — [FinalReview.dc.html](docs/references/admin-ui/FinalReview.dc.html) does not display next eligible time | AU18; known UI differences recorded after B1/B2 review | RC08 / BR-10 | No additional next-eligible-time display approved |
| Tile EHB override set/change/reset with calculated baseline; submit explicit change-override intent | Board — bind the control in [Board.dc.html](docs/references/admin-ui/Board.dc.html) to the reviewed backend contract | AU11 and its explicit-intent remediation | RC05 / BR-10 | None; verify the intent mapping during binding |
| Catalogue rate entry and “How the rate is counted” panel follow the decided fields and permissions | Catalogue — intentional differences from [Catalogue.dc.html](docs/references/admin-ui/Catalogue.dc.html) | AU23 backend delivered in B4; [Catalogue decisions — AU23, CAT-1 and WA-5](#catalogue-decisions--au23-cat-1-and-wa-5-4-october-b4-backend-delivered-wa-5-binding-pending); `08-decisions.md` “Drop-rate mechanics” / “Catalogue layout” | RC10 / WA-5 | Bound in T2 (lane T, items 1–2); user visual acceptance 7 October 2026 (`UI_PAGE_MATRIX.md`; whole suite green on `69f9cf01`, 8 October 2026) |
| Informational activity Team size in Settings and Add activity beside Kills per hour | Catalogue — intentional difference from [Catalogue.dc.html](docs/references/admin-ui/Catalogue.dc.html) | CAT-1 backend delivered in B4; [Catalogue decisions — AU23, CAT-1 and WA-5](#catalogue-decisions--au23-cat-1-and-wa-5-4-october-b4-backend-delivered-wa-5-binding-pending); `08-decisions.md` “Catalogue layout” | RC10 / WA-5 | Bound in T2 (lane T, items 1–2); user visual acceptance 7 October 2026 (`UI_PAGE_MATRIX.md`; whole suite green on `69f9cf01`, 8 October 2026) |
| Structured confirmation naming every affected activity before a shared item rename or image change, including Add-drop adoption with a different normalized image; no per-activity split | Catalogue — the reference only shows a generic “also used by” indication and does not show the named-activity confirmation | AU21/D7 option (a) and D9 option (a); 08-decisions.md, B4 review decisions; B4 remediation item 1 | RC10 / WA-5 | Bound in T2 (lane T, items 1–2); user visual acceptance 7 October 2026 (`UI_PAGE_MATRIX.md`; whole suite green on `69f9cf01`, 8 October 2026); the list travels in the JSON response (brief 82), TempData key removed |
| WOM end-update status NotRequired/Pending/Succeeded/Rejected/CouldNotUpdate, target end and sanitized failure. A15: WOM shows Pending/Rejected/could-not-update with target end and corrects paused-fetch wording; Overview shows Needs attention in Final review for Pending/Rejected linking WOM; Final Review publish confirmation warns that the last fetch becomes official and retains the per-version note after publication. Publication is never blocked | WOM — Wom.dc.html; Overview — Overview.dc.html; Final Review — FinalReview.dc.html (existing component patterns) | WA-2 / AU20; Plan 42 A15 | WOM U9 / WA-5; Overview U4 / OS-1; Final Review U9 / BR-10 | None for A15 placement; other WOM fetch outcomes remain the U9 group-B question; Overview part Bound in U4 (brief 85); user early look passed 7 October 2026; reviewed (report 91), fixes rechecked at `a91b29ad`; merged `969d0351`; user accepted 8 October 2026 |
| Structured fetch eligibility, AU18 skip reason and next permitted time; typed credential/current-operation identity, phase and next attempt | WOM — [Wom.dc.html](docs/references/admin-ui/Wom.dc.html); these backend outputs exceed the generic Fetch wording. Final Review retains its separate AU18 data-only next-time decision above | WA-6 / AU20; Step 4 D4 and B1/B2 D2 in B3 source attribution; brief 23 items 1 and 6 | WA-5 / RC09, as settled in the UI integration plan | Decide which structured WOM outcomes are displayed and their binding before integration; keep current-page generic Fetch wording and no new Final Review next-time display |
| Resume always requires a validated future replacement end, even when the retained configured end is future; early end stores the ceiling-minute configured end and precise actual end | Overview — [Overview.dc.html](docs/references/admin-ui/Overview.dc.html) currently conditionally asks for the replacement only after the retained end has passed | WA-2 corrected Resume rule; AU20 item2; remediation brief item6 / review24f R1 | RC01 / OS-1 | Bound in U4 (brief 85); user early look passed 7 October 2026; reviewed (report 91), fixes rechecked at `a91b29ad`; merged `969d0351`; user accepted 8 October 2026. Dialog text (Resume always asks for the new end; early end states the ceiling-minute end) accepted 8 October 2026 |
| Cancelled/Finalized/Archived admin pages open read-only; each page binding removes its view redirect from EventMutationCapabilityPageFilter (baseline view redirect `:90-101`); all changes remain refused under D16 | All event admin pages — Identity, Schedule, Signup setup, WOM, Final Review, Participants references | D17 / D16 in 08-decisions.md | Each page integration inventory | Identity GET/Current bound in U1; remaining page removals stay with their assigned bindings |
| Signup answers shows every custom question as question: answer, editable until draft start then read-only; drop Participant’s note and its row flag | Participants — Participants.dc.html | S1 in 08-decisions.md | P-1 | Decided future binding; no new field |
| Start checklist and postponed automatic start, when another event is still current (Live or in Final review): “Publish the results of ‹other event› first”, linking to that event | Overview — Overview.dc.html | S2 in 08-decisions.md | OS-1 | Bound in U4 (brief 85); user early look passed 7 October 2026; reviewed (report 91), fixes rechecked at `a91b29ad`; merged `969d0351`; user accepted 8 October 2026; also in the Resume dialog (U4-Q4 (b)); legacy Finished reads “‹X› is still the current event. Contact the Super Admin to archive it.” (U4-Q5 (a)) |
| Failed automatic signup opening attention: “Signups didn’t open automatically: ‹reason›”, styled like “Automatic start postponed”, with normal Open signups now and existing checks | Overview — Overview.dc.html | S3 in 08-decisions.md | OS-1 | Bound in U4 (brief 85); user early look passed 7 October 2026; reviewed (report 91), fixes rechecked at `a91b29ad`; merged `969d0351`; user accepted 8 October 2026; reason wording accepted 8 October 2026 |
| “‹account› left ‹team› at ‹time›. Drops before that time count for the team; later ones don’t.” Drop active-account-since/switched-from content | Review — Review.dc.html | S9; remediation item3 supplies nullable credited participant team-leave time in 08-decisions.md | RC07 | Backend read implemented; warning binding pending |
| Deactivate confirmation names affected draft boards with an event Board link for each; approved/published snapshots stay unaffected | Catalogue — Catalogue.dc.html | S10 in 08-decisions.md | RC10 / WA-5 | Bound in T2 (lane T, items 1–2); user visual acceptance 7 October 2026 (`UI_PAGE_MATRIX.md`; whole suite green on `69f9cf01`, 8 October 2026); T2-Q3 (a) / T2-Q3b (a); wording approved by the user at the early look; T2-1 (a): for the Super Admin a hidden event links to Overview’s limited view (`/Admin/Events/Manage/{id}?hidden=true`, as A7), not its Board page; T2-2 (a) server refuses unconfirmed deactivation; T2-3 event selection as built |
| Remove Move up/Move down in queue; manual queue reordering is not approved | Participants — Participants.dc.html | S13 in 08-decisions.md | P-1 | Decision closed; reference/binding change pending |
| Reset panel: “The link stops working after 60 minutes, after one use, or if this account’s role, status or ownership changes.” | Accounts — Accounts.dc.html | C-ACC-1 in 08-decisions.md | AU22 / Accounts binding | Bound in T1 (lane T); user visual acceptance 7 October 2026 (T1, `UI_PAGE_MATRIX.md`) |
| AU24 ownership-change confirmation includes typed username | Accounts — Accounts.dc.html | C-ACC-2 / AU24, 33c sweep; brief35 item8 | AU24 / Accounts binding | Bound in T1 on the Transfer confirmation step (“Type the new owner’s username”, case ignored, server-checked), after recipient + password and the change table; user visual acceptance 7 October 2026 (T1, `UI_PAGE_MATRIX.md`) |
| Accounts lost permission/session (C-CMP-2): today's sign-out is kept; every drawer action, reset link and transfer shows what wasn't sent (action, account, reason; never the password) before Sign in. The reference's in-place “You no longer have permission … Refresh” banner is superseded. A refusal while still signed in (e.g. an Admin target for an ordinary Admin) shows “That couldn’t be done.” with the server message | Accounts — Accounts.dc.html:658, :690, :727 | C-CMP-2 in 08-decisions.md; brief 79 planner default | T1 (lane T) | None; wording binding-time, user visual acceptance 7 October 2026 (T1, `UI_PAGE_MATRIX.md`) |
| Accounts RC11 corrections in the binding: uncertain outcomes gate further actions and “Check current status” reports only what the re-read proves (unchanged version = “It wasn’t changed.”; matching state = “The account shows this change now … may be yours or another administrator’s”; a disable with this admin and reason = “It went through.”) (A2); a stale disable keeps the typed reason for the next confirmation and typed reasons use the shared discard choice (A3); revoking a disabled Admin says it stays disabled (A5); the transfer confirmation keeps the selected recipient's version/role (A6); an unconfirmed transfer's “Check ownership” uses the readback sign-in notice when the session ended (T1-2, planner: sign-out is not proof of transfer) | Accounts — Accounts.dc.html:661, :668–673, :735–740, :623–625, :541, :838, :857–859, :710–724 | RC11 A2/A3/A5/A6 (accounts-review.md) | T1 (lane T) | None for behaviour; wording binding-time, user visual acceptance 7 October 2026 (T1, `UI_PAGE_MATRIX.md`) |
| Accounts retired routes: `/Admin/Accounts/Manage/{id}` redirects to `/Admin/Accounts?account={id}` (unknown or non-website accounts stay Not Found); `/Admin/Accounts/Transfer` redirects to `/Admin/Accounts`; query names follow the reference (`q`, `role`, `page`, `account`) with no legacy-name mapping (no inbound link outside these pages used the old names) | Accounts — README routing table :3108–3116 | Brief 79 planner defaults | T1 (lane T) | T1-1 (planner, 7 October 2026): `/Admin/Accounts/Create` stub kept (deleting it turns POST into 405 through app-wide status re-execution and breaks DraftStartReadinessIntegrationTests:124); the 42f §3.10 leftover check `test ! -e Create.cshtml` is intentionally not met; retirement may come in U10 |
| Accounts directory Global role is always a pill: User `badge-neutral` (as the drawer header), Admin `badge-outline`, Super Admin `badge-accent`; Status keeps the reference (Active plain text, Disabled `badge-danger`) | Accounts — Accounts.dc.html:784, :791 (User as plain text) | T1-4 (b), user, “T1 Accounts early look” in 08-decisions.md | T1 (lane T) | None; user visual acceptance 7 October 2026 (T1, `UI_PAGE_MATRIX.md`) |
| Accounts header summary uses Events-style separate items with tabular numbers (`<b class="tnum">N</b> accounts`, `<b class="tnum">M</b> disabled` only, as on Events) instead of one plain “N accounts · M disabled” string; the scope text “Website accounts and their global access” is removed (user, T1-5 addition); the loading summary shows the fixed words with a number-sized `.sk` bar in place of each number (“[bar] accounts”, “[bar] disabled”, as the Events tab-count placeholders), replacing the empty line and the reference’s “Loading accounts” (Accounts.dc.html:797; user, “Count summaries load like the tab counts”); it reserves one line, two at phone width (U3-Q6) | Accounts — Accounts.dc.html:161, :797; pattern Events.dc.html:113–114 | T1-5 and its addition, user, “T1 Accounts early look” | T1 (lane T) | None; user visual acceptance 7 October 2026 (T1, `UI_PAGE_MATRIX.md`) |
| Open/Reopen checklist includes every applicable server refusal with repair links (Discord configuration, usable code, valid questions, public-event overlap, future close); confirmation warns text answers are public when applicable, and the “reopening keeps existing signups” warning is dropped; Live WOM-sync-failed attention links WOM and says lifecycle actions aren’t affected | Overview — Overview.dc.html | A-Overview-3/6/7 in 08-decisions.md | OS-1 | Bound in U4 (brief 85); user early look passed 7 October 2026; reviewed (report 91), fixes rechecked at `a91b29ad`; merged `969d0351`; user accepted 8 October 2026; no Events-directory attention item; a blocked Reopen lists its rows under “Before you can reopen signups” |
| Readiness includes any missing server blocker, including Submission cutoff required and Final-review cycle is missing, with server text | Final Review — FinalReview.dc.html | B-Final-1 in 08-decisions.md | RC08 / BR-10 | Decided future binding; preserve narrowed AU18 omission |
| Create checklist retains its three rows and adds each further applicable server refusal | WOM — Wom.dc.html | C-WOM-2 in 08-decisions.md | RC09 / WA-5 | Decided future binding |
| U4-Q1 (c): “Automatic start postponed” is a Needs-attention item in every pre-Live phase with the phase reason (“Open and close signups first.”, “Close signups first.”, or the remaining start requirements) | Overview — Overview.dc.html shows it only in Signups closed (`:1104-1107`) | U4-Q1 (c) in 08-decisions “U4 brief decisions” | U4 / OS-1 | Bound in U4 (brief 85); user early look passed 7 October 2026; reviewed (report 91), fixes rechecked at `a91b29ad`; merged `969d0351`; user accepted 8 October 2026 |
| U4-Q2 (c): Overview’s Final review checklist shows the three reference rows (placements row from the server’s calculated-placements blocker) plus one “N more on Final review” row for the server’s other blockers | Overview — Overview.dc.html `:688-695` | U4-Q2 (c) | U4 / OS-1 | Bound in U4 (brief 85); user early look passed 7 October 2026; reviewed (report 91), fixes rechecked at `a91b29ad`; merged `969d0351`; user accepted 8 October 2026 |
| U4-Q3 (c): the Super Admin’s limited hidden view opens on the plain event URL (legacy `?hidden=true` still accepted) and keeps the quarantine history list; only Restore can be posted; ordinary admins get Not Found | Overview — Overview.dc.html hidden view `:152-162` has no history list | U4-Q3 (c); C-CMP-1 | U4 / OS-1 | Bound in U4 (brief 85); user early look passed 7 October 2026; reviewed (report 91), fixes rechecked at `a91b29ad`; merged `969d0351`; user accepted 8 October 2026 |
| U4-E2: Overview loading header shows the event name only (no phase badge, empty reserved summary line) while the page loads; badge and summary arrive with the page | Overview — Overview.dc.html keeps name, badge and summary during loading | Summary reservation (U3-Q5) | U4 / OS-1 | Bound in U4 (brief 85); user early look passed 7 October 2026; reviewed (report 91), fixes rechecked at `a91b29ad`; merged `969d0351`; user accepted 8 October 2026 |
| U4-E1: an unknown or inaccessible event id on Overview returns the shared admin 404 page instead of an in-page “Event not found” card | Overview — Overview.dc.html shows an in-page “Event not found” card | Brief 85 planner default; Test #14 and ordinary-admin privacy for hidden events (U4-Q3) | U4 / OS-1 | Bound in U4 (brief 85); user early look passed 7 October 2026; reviewed (report 91), fixes rechecked at `a91b29ad`; merged `969d0351`; user accepted 8 October 2026 |
| Stale lifecycle dialog names the latest recorded change as “‹audit action label› (‹actor›)” | Overview — Overview.dc.html prototype text (“@mia closed signups”) | README “Action states: Stale”; brief 85 technical choice | U4 / OS-1 | Bound in U4 (brief 85); user early look passed 7 October 2026; reviewed (report 91), fixes rechecked at `a91b29ad`; merged `969d0351`; user accepted 8 October 2026; wording confirmed at the early look 7 October 2026 |
| Legacy Finished (Finalized) events show the last progression stage as “Finished” with the archived-style results panel; Hide is offered (server allows Finished) | Overview — Overview.dc.html has no Finished phase | README “Verified rules” (legacy Finished); D17 | U4 / OS-1 | Bound in U4 (brief 85); user early look passed 7 October 2026; reviewed (report 91), fixes rechecked at `a91b29ad`; merged `969d0351`; user accepted 8 October 2026 |
| Overview lifecycle dialogs and evidence codes post through AdminFetch with JSON outcomes (applied/stale/invalid/refused) and a no-store `GET ?handler=Current` read; plain form posts keep the PRG fallback | Overview — Overview.dc.html | Brief 85 planner default “Transport”; 42c §1.5 item 14; C-CMP-2 | U4 / OS-1 | Bound in U4 (brief 85); user early look passed 7 October 2026; reviewed (report 91), fixes rechecked at `a91b29ad`; merged `969d0351`; user accepted 8 October 2026 |
| The header blocker count and its readiness anchor are retired from the old Admin layout; Overview’s Needs attention and checklists own readiness | Old Admin layout (not in references) | Brief 85 “Retired”; 42c §1.3 | U4 / OS-1 | Bound in U4 (brief 85); user early look passed 7 October 2026; reviewed (report 91), fixes rechecked at `a91b29ad`; merged `969d0351`; user accepted 8 October 2026 |
| Every bound fetch/JSON endpoint detects session loss (302/login HTML or enhanced navigation), keeps and shows unsaved input before sign-in, and distinguishes route refusal from an uncertain handler outcome | Events/Create, Overview, Identity, Schedule, Participants/detail, Signup setup, Board, Teams, Review, Catalogue, Accounts and shell notifications; form/fetch consumers WOM, Final Review, Audit and Transfer; respective frozen page references (42a §3.2) | C-CMP-2; B5 remediation item7 authentication correction; AU17a picker; plan42/brief43 | U1 shared helper and Identity; AU17a picker and B5 owners RC07 / BR-10 / RC04–DRF; each later owning U2–U9/T1–T2 binding | None; authentication stays unchanged; 302 is not an HTTP403 claim Teams / Draft bound in U6 (all Draft fetch paths through AdminFetch). |
| Participant-list Paid/Unpaid `Payment` handler delegates to the existing service policy in every state except Discarded; list display remains unchanged | Participants — Participants.dc.html | S4 option (a) in 08-decisions.md; brief40 S4 | P-1 | **Server part done (brief40); bound in U5 (payment editable in every state except Discarded, review 96 M2); merged `42a7716e`; user accepted 8 October 2026** |
| WOM `FetchCompetition` is available in Live and Awaiting Final Review, while WA-2 unmatched-end and all existing synchronization guards remain authoritative; development due stays Live-only | WOM — Wom.dc.html | S6 option (a) and WA-2 in 08-decisions.md; brief40 S6 | WA-5 / RC09 | **Server part done (brief40); binding pending** |
| Direct Teams roster setup allows a missing configured end before actual start and refuses only a configured past end, with action messages naming the start and end | Teams — TeamsDraft.dc.html | S8 option (a) in 08-decisions.md; brief40 S8 | RC04 / DRF | **Server part done (brief40); bound in U6 (refusals show the server message; readiness row U6-Q2); reviewed (report 99); merged `2d83651e`; user accepted 8 October 2026** |
| Catalogue activity edits use the add form’s validation rules and messages before any write or audit | Catalogue — Catalogue.dc.html | brief40 F3; add/edit validation decision | RC10 / WA-5 | Bound in T2 (lane T, items 1–2); user visual acceptance 7 October 2026 (`UI_PAGE_MATRIX.md`; whole suite green on `69f9cf01`, 8 October 2026) |
| Schedule refuses clearing `Signup close` while signups are open, while a different valid future close remains allowed | Schedule — Schedule.dc.html | brief40 F4; decided Part 3 F4 in 08-decisions.md | OS-1 (U3) | Bound U3 item1; A-M1 fixes the missing Danish F4 resource: localized error on Signup closing, with Danish render/refusal coverage; scoped PostgreSQL coverage retained; Schedule accepted by the user 7 October 2026 (U3, `UI_PAGE_MATRIX.md`) |
| B-Board-1: players per team is editable only before Live; read-only from Live, including corrections (clean refusal, no write; lock-icon read-only box) | Board — Board.dc.html | B-Board-1 | U7 / BR-10 | Bound in U7 (brief 88); reviewed (report 95), fixes rechecked at `d7a9e272`; merged `5ec88e93`; user accepted 8 October 2026 (provisional rulings and proposed wordings accepted; page approval in `UI_PAGE_MATRIX.md`); the HTTP 500 during a Live/AwaitingFinalReview correction reproduced before the fix |
| B-Board-2: enforce correction reason ≤ 2,000, tile name ≤ 80 (new or changed names only, planner ruling 2) and weight 1–10,000 on the server, with matching client limits | Board — Board.dc.html | B-Board-2 | U7 / BR-10 | Bound in U7 (brief 88); reviewed (report 95), fixes rechecked at `d7a9e272`; merged `5ec88e93`; user accepted 8 October 2026 (provisional rulings and proposed wordings accepted; page approval in `UI_PAGE_MATRIX.md`) |
| U7-Q3 (corrected): Preview opens the reference modal with its mode label, but its tile area is the plain background with “Not supported yet”; the legacy BoardPreview page (demonstration teams/progress) is retired and its route redirects to the Board | Board — Board.dc.html preview shows tiles (`:487-494`, `:1653`) | U7-Q3 (corrected) | U7 / BR-10 | Bound in U7 (brief 88); reviewed (report 95), fixes rechecked at `d7a9e272`; merged `5ec88e93`; user accepted 8 October 2026 (provisional rulings and proposed wordings accepted; page approval in `UI_PAGE_MATRIX.md`) |
| D17: Board page, EditorData and Readback load read-only on Cancelled/Finalized/Archived; every POST keeps the D16 refusal; a terminal event without a board shows “No board recorded.” and never creates one | Board — Board.dc.html has no terminal or no-board state | D17, D16 | U7 / BR-10 | Bound in U7 (brief 88); reviewed (report 95), fixes rechecked at `d7a9e272`; merged `5ec88e93`; user accepted 8 October 2026 (provisional rulings and proposed wordings accepted; page approval in `UI_PAGE_MATRIX.md`) |
| Board commands answer in place (JSON outcome + no-store readback) for page-module requests; non-script posts keep PRG | Board — Board.dc.html | README item 1; C-CMP-2 | U7 / BR-10 | Bound in U7 (brief 88); reviewed (report 95), fixes rechecked at `d7a9e272`; merged `5ec88e93`; user accepted 8 October 2026 (provisional rulings and proposed wordings accepted; page approval in `UI_PAGE_MATRIX.md`) |
| “N tiles differ” uses the server count and names removed tiles (“N tiles differ from the published board, M of them removed.”) | Board — Board.dc.html counts working tiles only | Planner ruling 3 (brief 88) | U7 / BR-10 | Bound in U7 (brief 88); reviewed (report 95), fixes rechecked at `d7a9e272`; merged `5ec88e93`; user accepted 8 October 2026 (provisional rulings and proposed wordings accepted; page approval in `UI_PAGE_MATRIX.md`); wording accepted 8 October 2026 |
| Take-over confirmation says the other administrator’s unsaved edits stay in their browser (not “lost”) | Board — Board.dc.html takeover text (“anything they haven’t saved is lost”) | RC05 B2 | U7 / BR-10 | Bound in U7 (brief 88); reviewed (report 95), fixes rechecked at `d7a9e272`; merged `5ec88e93`; user accepted 8 October 2026 (provisional rulings and proposed wordings accepted; page approval in `UI_PAGE_MATRIX.md`) |
| Tile editor shows the server’s calculated baseline for the stored objectives and “Calculated when you save” after objective changes (no client EHB calculation); missing-rate notes come from catalogue data | Board — Board.dc.html recalculates live in the browser | RC05 B7 | U7 / BR-10 | Bound in U7 (brief 88); reviewed (report 95), fixes rechecked at `d7a9e272`; merged `5ec88e93`; user accepted 8 October 2026 (provisional rulings and proposed wordings accepted; page approval in `UI_PAGE_MATRIX.md`) |
| Default tile name follows the server rule (boss names joined, or “New tile”), not the reference’s “a / b / c” suggestion | Board — Board.dc.html `suggestName` | Server behaviour unchanged (brief 88: no product change) | U7 / BR-10 | Bound in U7 (brief 88); reviewed (report 95), fixes rechecked at `d7a9e272`; merged `5ec88e93`; user accepted 8 October 2026 (provisional rulings and proposed wordings accepted; page approval in `UI_PAGE_MATRIX.md`) |
| Retired: per-team workload panel and per-team EHB breakdown; ±25% line markers are advisory only | Old Board page (not in references) | Brief 88 “Retired”; AU13 | U7 / BR-10 | Bound in U7 (brief 88); reviewed (report 95), fixes rechecked at `d7a9e272`; merged `5ec88e93`; user accepted 8 October 2026 (provisional rulings and proposed wordings accepted; page approval in `UI_PAGE_MATRIX.md`) |
| U7-E1 (c): the new Board layout has no presence list or live re-read (no SignalR on the design layout); while the admin holds the edit lease, page and drawer activity renews it over HTTP (`POST ?handler=RenewEditing`, at most once a minute, no version or audit change), and another admin’s change is caught on the next action as a stale refusal with the draft kept | Board — Board.dc.html shows no presence or live updates | U7-E1 (c), planner provisional ruling in 08-decisions “U7 early-look stop” (user to confirm; SignalR in the design layout, option b, stays open) | U7 / BR-10 | Bound in U7 (brief 88); reviewed (report 95), fixes rechecked at `d7a9e272`; merged `5ec88e93`; user accepted 8 October 2026 (provisional rulings and proposed wordings accepted; page approval in `UI_PAGE_MATRIX.md`) |
| U7-E2 (a): at 390 px the loaded Board header actions (editing chip, Preview, primary action, More) wrap to a second row, so the header grows 42 px beyond the summary change (measured 22.4 px total vs −19.6 px summary change, Chromium and WebKit); page-specific conformance exemption `headerGrowth: { 390: 42 }` in `scripts/lib/admin-page-conformance-pages.cjs`, every other width keeps the generic check | Board — Board.dc.html hides its actions while loading, same shift | U7-E2 (a), planner provisional ruling in 08-decisions “U7 early-look stop” (page exceptions are the user’s call; user to confirm) | U7 / BR-10 | Registered in U7 (brief 88); reviewed (report 95); merged `5ec88e93`; user accepted 8 October 2026 (provisional rulings and proposed wordings accepted; page approval in `UI_PAGE_MATRIX.md`) |
| After tile edits the description text reads “Written automatically … when you save” (the automatic text is recomputed by the server) | Board — Board.dc.html shows a static automatic-description text | Server-side automatic description (AU11); review U7 L1 | U7 / BR-10 | Bound in U7 (brief 88); reviewed (report 95), fixes rechecked at `d7a9e272`; merged `5ec88e93`; user accepted 8 October 2026 (provisional rulings and proposed wordings accepted; page approval in `UI_PAGE_MATRIX.md`) |
| Drawer eyebrow reads “Working copy” while a published board is being corrected | Board — Board.dc.html has no correction state in the drawer | D19; review U7 L1 | U7 / BR-10 | Bound in U7 (brief 88); reviewed (report 95), fixes rechecked at `d7a9e272`; merged `5ec88e93`; user accepted 8 October 2026 (provisional rulings and proposed wordings accepted; page approval in `UI_PAGE_MATRIX.md`) |
| Drawer shows “This tile isn’t on the board any more”, and the page shows “That tile isn’t on this board”, when a `?tile=` reference or an open tile no longer exists | Board — Board.dc.html has no such states | B4; review U7 L1 | U7 / BR-10 | Bound in U7 (brief 88); reviewed (report 95), fixes rechecked at `d7a9e272`; merged `5ec88e93`; user accepted 8 October 2026 (provisional rulings and proposed wordings accepted; page approval in `UI_PAGE_MATRIX.md`) |
| Take-over confirmation omits the reference’s extra point and its “from X” title (“Take over editing?” with the other administrator’s name in the text only) | Board — Board.dc.html takeover dialog (title “…from X”, extra bullet) | RC05 B2 (the “lost” wording is a separate row above); review U7 L1 | U7 / BR-10 | Bound in U7 (brief 88); reviewed (report 95), fixes rechecked at `d7a9e272`; merged `5ec88e93`; user accepted 8 October 2026 (provisional rulings and proposed wordings accepted; page approval in `UI_PAGE_MATRIX.md`) |
| U7-E3: the terminal read-only Board (Cancelled/Finalized/Archived) shows no working artwork; retained published artwork still shows | Board — Board.dc.html has no terminal state | D17; Board `TileImage` stays refused on terminal events; review U7 L1 | U7 / BR-10 | Bound in U7 (brief 88); reviewed (report 95), fixes rechecked at `d7a9e272`; merged `5ec88e93`; user accepted 8 October 2026 (provisional rulings and proposed wordings accepted; page approval in `UI_PAGE_MATRIX.md`) |
| Approval and publication issue codes the reference has no wording for reuse the server’s issue text | Board — Board.dc.html lists only its demonstration issues | AU19 / U7-Q2; review U7 L1 | U7 / BR-10 | Bound in U7 (brief 88); reviewed (report 95), fixes rechecked at `d7a9e272`; merged `5ec88e93`; user accepted 8 October 2026 (provisional rulings and proposed wordings accepted; page approval in `UI_PAGE_MATRIX.md`) |
| B-Review-2: refuse no-op correction with “Change at least one detail, or cancel.” | Review — Review.dc.html | B-Review-2 | U8 / BR-10 | None |
| B-Final-2: Reopen requires expected version; RL-1/BR-12 must not bypass checks when missing | Review / Final Review — Review.dc.html / FinalReview.dc.html | B-Final-2; RL-1/BR-12; plan42 §5 | U8 Review / U9 Final Review | None for mandatory version; U9 reopen-with-another-current-event is group B |
| A-SignupSetup-1: enforce player-cap maximum 10,000 in the service with a clear message | Signup setup — SignupSetup.dc.html | A-SignupSetup-1 | U3 / OS-1 | None |
| A-Events-3: drop invalid link parts (filter/view/phase, including Hidden for an ordinary Admin) with a short notice; the reference silently drops invalid non-hidden parts | Events — Events.dc.html | A-Events-3; brief67 item0 | U2 / EI-2 | None for behavior; EN/DA wording proposed at binding and approved at visual acceptance |
| Partial Needs-attention-unavailable banner in the reference banner style; event rows remain usable | Events — Events.dc.html shows whole-page failure, not partial attention failure | U2-5; brief67 item0/3 | U2 / EI-2 | None; retained partial state bound; user visual acceptance 7 October 2026 (U2, `UI_PAGE_MATRIX.md`) |
| Live chart/history/statistics are provisional; recap selects only the latest ended event; Live row and provisional-count wording includes Live | Dashboard — Dashboard.dc.html models Final review provisional rows only; its stale sample metric definitions remain frozen | DB-4; D-8; brief67 item1/2 | U2 / DB-1 | Behavior decided; EN/DA wording proposed at binding and approved at visual acceptance |
| Current label for Live; overdue preparation visibly overdue; unset capacity/date remain unknown rather than a fabricated limit or future date | Dashboard — Dashboard.dc.html card assumes Next event and a set future date/capacity | DB-5; D-9…D-11; brief67 item1/2 | U2 / DB-1 | Behavior decided; EN/DA wording proposed at binding and approved at visual acceptance |
| All shared first-place winners appear in recap/history instead of one sample winner | Dashboard — Dashboard.dc.html shows one winner | D-12; approved Dashboard metric definitions; brief67 item2 | U2 / DB-1 | Behavior decided; EN/DA wording proposed at binding and approved at visual acceptance |
| Signup opening failed attention label; Start postponed replaces the sample Start failed label | Events — Events.dc.html has no signup-opening-failure sample | AU04; E-8; brief67 item3 | U2 / EI-2 | Behavior decided; EN/DA wording proposed at binding and approved at visual acceptance |
| CheckAgain or same-key retry 404 says the event was not found and may have been removed; fresh Create uses a new request key and retains typed values | Events/Create — Events.dc.html assumes not found means not created and permits same-key retry | AU03; E-14; brief67 §2/item4 | U2 / EI-2 | Recovery decided by brief67; EN/DA wording proposed at binding and approved at visual acceptance |
| Every editable modal/drawer closes on outside click when clean; edited input uses the same shared discard/uncertain guard as Cancel/Escape. Pending saves refuse dismissal; confirmations never close on outside click. Create close removes create=1 by URL replace only, no navigation/skeleton/reload | Shared shell / Events/Create — Events.dc.html:317–319 requestCloseCreate; A12 refined | User decision B and bug2; brief70 item5 / E-16 | U2 remediation item5; shared shell once | None; both clean/dirty paths proven in Chromium/WebKit; URL close preserves other query state |
| Unknown Create departure offers Check again / Leave anyway and warns that the event may already have been created; ordinary dirty input retains Keep editing / Discard | Events/Create — Events.dc.html:699–702, :949–950 claims values have not been saved even for an uncertain outcome | User U-A and accepted Identity I-3; planner ruling 6 October 2026, “U2 item 4: leaving the Create dialog after an uncertain create” | U2 item4 / E-16 | None; shared guard/confirmation, no automatic resend or undo claim |
| A-Identity-1: 50-character name limit applies only when the name changes (intentional difference) | Identity — Identity.dc.html | A-Identity-1 | U1 / EI-2 | None |
| C-WOM-3: locked Unknown origin safety state says “Unknown origin; contact an operator”; R-3 must confirm zero Unknown management rows after migration | WOM — Wom.dc.html | C-WOM-3 | U9 / WA-5 (R-3 separately authorized) | None |
| S11: Audit adds Participants and Signups areas | Audit — Audit.dc.html | S11 | T1 / WA-5 | Group B: exact action-prefix mapping |
| B-Participants-3: withdrawn participant — accounts and signup answers read-only (“Restore to edit”); payment and private note still editable (S4) | Participants — Participants.dc.html | B-Participants-3 | U5 / P-1 | None |
| B-Participants-5: list search includes all account RSNs, username and Discord name; Add search includes username, Discord name or RSN; no recently-joined list | Participants — Participants.dc.html | B-Participants-5 | U5 / P-1 | None |
| Saving an existing team preserves stored affiliation | Teams — TeamsDraft.dc.html; reference README:1679 | Plan42 §5; inventory42d | U6 / TD-6 | None |
| S5 service path: retire list Withdraw→roster removal in SignupService.cs:1793-1803; lock Withdraw/Restore after draft starts; finalized roster removal belongs to Teams confirmation | Participants / Teams — Participants.dc.html / TeamsDraft.dc.html | S5; plan42 §5 | U5 / P-1 (Teams UI U6) | None |
| Participants drawer failures (U5-Q3): a definite refusal shows the server’s reason and keeps the edits; a lost response re-reads the participant (no-store `GET ?handler=Current`) and says “We couldn’t confirm whether your changes were saved …” with the current details; Add and row actions never resubmit after a lost response. The reference’s “Couldn’t save. Your edits are still here.” / “Couldn’t {verb} {name}. Nothing was changed.” are replaced | Participants — Participants.dc.html:909, :441 | U5-Q3 (a) in 08-decisions.md; 86-ui-rules 13 | U5 / P-1 | Bound in U5 (brief 87); reviewed (report 96), fixes rechecked at `05c4798b`; merged `42a7716e`; user accepted 8 October 2026 (provisional rulings and proposed wordings accepted; page approval in `UI_PAGE_MATRIX.md`) |
| U5-E1: the drawer does not show the participant’s Discord display name (“Discord: Linked / Not linked”); the reference shows “name · linked”. Add search results still show the Discord name because B-Participants-5 searches it (the old detail page never showed it; a test pins it as private) | Participants — Participants.dc.html:1280, :1336 | U5-E1 (a), planner ruling 8 October 2026, provisional; user to confirm | U5 / P-1 | Bound in U5 (brief 87); reviewed (report 96), fixes rechecked at `05c4798b`; merged `42a7716e`; user accepted 8 October 2026 (provisional rulings and proposed wordings accepted; page approval in `UI_PAGE_MATRIX.md`) |
| U5-E2: answers to deactivated (retired) signup questions stay readable in the drawer as read-only “question: answer” rows and are never part of a save; the reference has no such rows | Participants — Participants.dc.html:403-431 | U5-E2 (a), planner ruling 8 October 2026 | U5 / P-1 | Bound in U5 (brief 87); reviewed (report 96), fixes rechecked at `05c4798b`; merged `42a7716e`; user accepted 8 October 2026 (provisional rulings and proposed wordings accepted; page approval in `UI_PAGE_MATRIX.md`) |
| U5-E4: with unsaved edits in the drawer, Withdraw… / Restore… first ask to discard (shared discard dialog); the reference opens the confirmation over the edits | Participants — Participants.dc.html:420-424, :886 | Decision B (edited layers); 86-ui-rules 11 | U5 / P-1 | Bound in U5 (brief 87); reviewed (report 96), fixes rechecked at `05c4798b`; merged `42a7716e`; user accepted 8 October 2026 (provisional rulings and proposed wordings accepted; page approval in `UI_PAGE_MATRIX.md`) |
| U5-E5: synthetic reference data replaced by stored values: Source shows “Website signup / CSV import / Added by an admin” (no “Discord /signup command”, no “Added by admin (@name)”, which is not stored); no numeric “ID n” beside the website account | Participants — Participants.dc.html:653, :1277-1280, :1374 | Brief 87 “Synthetic reference data” | U5 / P-1 | Bound in U5 (brief 87); reviewed (report 96), fixes rechecked at `05c4798b`; merged `42a7716e`; user accepted 8 October 2026 (provisional rulings and proposed wordings accepted; page approval in `UI_PAGE_MATRIX.md`) |
| U5-E6: the reference’s single “Alt account” field maps to the event’s Informational account slots: one slot keeps “Alt account”, several show one field each labelled by the slot’s question, none hides the section; typed RSNs are accepted as event-only accounts (hint “Not one of @user’s saved accounts. Used for this event only.”), never saved to the player | Participants — Participants.dc.html:395-402, :353 | F05/F06; U5-Q1 (a) | U5 / P-1 | Bound in U5 (brief 87); reviewed (report 96), fixes rechecked at `05c4798b`; merged `42a7716e`; user accepted 8 October 2026 (provisional rulings and proposed wordings accepted; page approval in `UI_PAGE_MATRIX.md`); wording accepted 8 October 2026 |
| U5-E8: promotion appears in the toast (“X withdrawn · Y confirmed from the waiting list”, “… · Y confirmed” after Move to waiting list); the reference says “X withdrawn · Y confirmed” | Participants — Participants.dc.html:919, :950 | Brief 87 “Withdraw/promotion” | U5 / P-1 | Bound in U5 (brief 87); reviewed (report 96), fixes rechecked at `05c4798b`; merged `42a7716e`; user accepted 8 October 2026 (provisional rulings and proposed wordings accepted; page approval in `UI_PAGE_MATRIX.md`); wording accepted 8 October 2026 |
| Participants fetch paths (row actions, Paid/Unpaid, drawer read and Save, Add search/pick/submit, list re-read) detect session loss (302/login HTML), keep the drawer draft and show what wasn’t sent (action, participant, accounts, payment) before Sign in | Participants — Participants.dc.html (no session-loss state) | C-CMP-2; 86-ui-rules 14 | U5 / P-1 | Bound in U5 (brief 87); reviewed (report 96), fixes rechecked at `05c4798b`; merged `42a7716e`; user accepted 8 October 2026 (provisional rulings and proposed wordings accepted; page approval in `UI_PAGE_MATRIX.md`) |
| Participants Add leaves no history entry (`?add=1` is replaced, never pushed) and is never resubmitted; Back/Forward reopen the participant drawer per decision B; stale list data is re-read behind an open drawer without the shell’s discard prompt | Participants — Participants.dc.html (in-memory prototype) | FC “Required integration behavior” :582-606 | U5 / P-1 | Bound in U5 (brief 87); reviewed (report 96), fixes rechecked at `05c4798b`; merged `42a7716e`; user accepted 8 October 2026 (provisional rulings and proposed wordings accepted; page approval in `UI_PAGE_MATRIX.md`) |
| Dead participant-dialog code in `wwwroot/js/event-manage.js` (Add/Edit dialogs, editor guard hooks) is no longer reachable from any page; removing it also touches `admin-confirmation-navigation.browser.js` (its “participant” owner) and the other pages that load the file | Participants — n/a (code) | U5-E3 (a): goes in U10 | U10 | Done in U10 item 2: `event-manage.js` and `admin-confirmation-navigation.browser.js` deleted (replaced by `admin-design-participants.browser.js`) |
| S10 server read: supply draft boards using the activity/drop for deactivate confirmation with event Board links | Catalogue — Catalogue.dc.html | S10; plan42 §5 | T2 / WA-5 | Bound in T2 (lane T, items 1–2); user visual acceptance 7 October 2026 (`UI_PAGE_MATRIX.md`; whole suite green on `69f9cf01`, 8 October 2026) (`OnGetDeactivationImpact`); T2-Q3 (a), T2-Q3b (a) |
| C-CAT-2 leftovers: category membership validation and drop-edit item-name length check | Catalogue — Catalogue.dc.html | C-CAT-2; plan42 §5 | T2 / WA-5 | Bound in T2 (lane T, items 1–2); user visual acceptance 7 October 2026 (`UI_PAGE_MATRIX.md`; whole suite green on `69f9cf01`, 8 October 2026); T2-Q4 (a) |
| Catalogue S10 confirmation composition: the drop’s **Deactivate drop…** opens the same confirmation as the activity (the reference toggles a drop at once, `Catalogue.dc.html:836-840`); both read the S10 impact first (“Checking what uses it…”), list the affected draft boards with Board links, mark correction copies “(correction)”, the drop’s button reads “Deactivate drop…” because it now opens a confirmation (reference “Deactivate drop”, `:1116`), and replace the reference text that contradicts S10 (activity points `:1126`, toast `:925`, Availability row `:1067`). The server refuses a deactivation without `confirmed=true` and returns the impact instead (reactivation needs none) | Catalogue — Catalogue.dc.html | S10; brief 82 planner default (wording for the user’s early look) | T2 / WA-5 | Wording approved by the user (T2 early-look stop); T2 accepted 7 October 2026 |
| Catalogue roll-group refusal says “Roll group can only be changed by the Super Admin.”; retired per-drop fields are refused with “These rate settings can’t be changed here. Your changes were not saved. Reload the editor.” (rules unchanged) | Catalogue — Catalogue.dc.html `:378`, `:801` | 42f §2.5 item 13; brief 82 | T2 / WA-5 | — |
| Catalogue fetch endpoints (C-CMP-2): every save, suggest, validate, impact and deletion read goes through the shared fetch helper; a lost session (login redirect or HTML answer) shows what wasn’t saved and keeps the entries; an unknown outcome offers **Check current values** (readback), never a repeat. Script requests from an ordinary Admin to the Super Admin delete endpoints get a refusal outcome instead of a bare 403 | Catalogue — Catalogue.dc.html | C-CMP-2; brief 82 | T2 / WA-5 | — |
| Catalogue header summary: separate count items with highlighted numbers (“**68** active activities”, “**425** drops”) plus the reference sentence, instead of one “68 active activities · 425 drops” string (`Catalogue.dc.html:176`, `:1005`); while loading the fixed words show with number-sized bars; one reserved line, two at phone width | Catalogue — Catalogue.dc.html | “Count summaries load like the tab counts”; U3-Q5/Q6; T1-5 pattern | T2 / WA-5 | — |
| Catalogue loading: no load fades on loaded content; skeleton bars in rows as tall as the text line they stand for; drawer read shows the drawer skeleton after 150 ms (shared update helper) | Catalogue — Catalogue.dc.html `:207`, `:280` | “No load fades”; Q-SK2; decision C | T2 / WA-5 | — |
| Catalogue thumbnails show the item’s or activity’s cached Wiki image (or an empty tile); the reference’s coloured initial tiles are prototype placeholders | Catalogue — Catalogue.dc.html `:506` | WA-5 binding (real data) | T2 / WA-5 | — |
| Catalogue thumbnails show the whole sprite: `background-size:contain`, centred, 3px inset in the same 32px / 44px boxes, track background and inset border kept (family-scoped page CSS); the reference `.thumb` uses `center/cover` (`components.css:1328`), which crops or zooms OSRS sprites of different proportions. The loading header’s Add activity is a usable link from the first frame, as Events’ Create event | Catalogue — Catalogue.dc.html; ui/components.css | 08 “T2 Catalogue early look” (planner rulings on the user’s findings) | T2 / WA-5 | — |
| Catalogue wording forced by case-insensitive resource keys: drop line value “untradeable, 0 gp” (reference “untradeable”), calculated EHB “Unavailable” (reference “Not available”); Team size hint “The group size the rates assume. It isn’t used in calculations.” (no reference text) | Catalogue — Catalogue.dc.html `:1079`, `:1095` | A5 Danish text; CAT-1 | T2 / WA-5 | Wording accepted with T2 (user, 7 October 2026) |
| U8-Q1: Review times lead with UTC (the OSRS event plugin stamps UTC); the secondary time is the event's own timezone named by its city (`BingoEvent.Timezone`), in the queue's Uploaded column, the workspace facts and the header “Times in UTC, with {city} time below” | Review — [Review.dc.html](docs/references/admin-ui/Review.dc.html) `:476`, `:1141`, `:1188` lead with fixed Copenhagen time | User decision U8-Q1 (b), 7 October 2026; planner ruling 3 (queue included) | U8 | None; no Copenhagen string remains in Review |
| Queue per-row check warnings (After end, Paused period, Same image, No screenshot, Left team) and drop/boss (“Manual objective” when none) from a bounded projection of existing data, one after-end boundary `ActualEndedAt ?? EventEndsAt` for queue and workspace | Review — Review.dc.html queue rows (R-R15, R-R12) | RC07 R-R15; README Review notes 3–4 | U8 | None; no new rule |
| Wording replaced for truth or brevity (R-R19): “Active asset”, “Resolve submission”, “Correct metadata before approval”, “Ineligible final-review interval” become short labels; paused-period title “Check the in-game time: the event was paused.” (RC07 corrects `:1171`; the screenshot's game time is judged, not upload time); “Mark … as a duplicate” becomes “reject it with a reason”; the always-visible “No Captain note” is dropped (the note shows only when present) | Review — Review.dc.html `:1169-1172` | RC07 R-R12, R-R19 | U8 | None |
| Closed-state texts per lifecycle (the reference has one sentence, `:1229`): Draft/Signups “Review starts when the event is Live.”; Finalized “{event} is finished, so review is read-only.”; Archived “{event} is archived, so review is read-only.”; Cancelled “{event} was cancelled, so review is read-only.”; queue summary “Finished/Archived/Cancelled · review is read-only”; terminal empty queue “No evidence was submitted in {event}.”; Approve/Reject/Correct/Reverse and the Contribution line hidden when review is closed (R-R5) | Review — Review.dc.html `:1229` | D16/D17; planner wording at the U8 early look | U8 | Wording proposed by the implementer; accepted 8 October 2026 (U8 proposed wordings in 94h) |
| Cap-limited Contribution wording: Add < Weight with room left “Approving adds {Add}” + “Worth {W}, but the drop's limit allows only {Add} more.”; Add = 0 with work remaining “The drop's limit is reached” + “This drop can't add more to the objective ({used} of {target}). If the submission doesn't count, reject it with a reason.” | Review — Review.dc.html `:1178-1182` would state a false remaining count | AU17 cap-limited row above (`:1218`); R-R3 | U8 | Wording proposed by the implementer; accepted 8 October 2026 (U8 proposed wordings in 94h) |
| B-Review-2 bound: a correction that changes none of tile, requirement, drop and credited character is refused server-side before any `ReviewAction` or audit write, with the reference text “Change at least one detail, or cancel.” (English and Danish), shown in the correction form | Review — Review.dc.html `:948` shows the text client-side only | B-Review-2 (row above) | U8 | None |
| RL-1 / BR-4 bound: Reject and Reverse need an explicit `confirmed` flag on the server (refusal “Confirm this decision before it is saved. Nothing was changed.”, nothing written). Reverse keeps its confirmation dialog; the inline Reject reason panel's submit is the confirmation, so the old Reject confirmation dialog is gone | Review — Review.dc.html Reject is an inline required-reason panel with no dialog | RL-1 / BR-4; planner ruling 2 on brief 94 | U8 | None |
| RL-1 / BR-12 bound: Approve, Reject, Reverse and Edit are refused before any write when the expected version is missing or zero (“This decision wasn't saved because the page didn't say which version you reviewed. Reload the submission and try again.”); player Withdraw/Correct are unchanged | Review — not shown | RL-1 / BR-12; planner ruling 1 on brief 94 | U8 | None; B-Final-2 stays U9's |
| Hidden event or unknown submission is Not Found: the queue with a hidden/unknown `eventId` answers 404 (“This event isn't available”), no `eventId` shows “Choose an event”, Readback answers 404 for hidden events and unknown ids (the client treats any non-200 as unknown), Details 404 shows “This submission isn't available” with a way back | Review — Review.dc.html shows neither | C-CMP-1; planner ruling 4 on brief 94 | U8 | None |
| Review viewer: the inline viewer (Fit, 100%, zoom level, keyboard/pointer, load-failure retry, Original opens `/Evidence/{assetId}`) is ported into `admin-review.js` with the gesture model of `public-evidence.js` instead of changing that dialog-bound file shared by Board, TeamBoard and Captain | Review — Review.dc.html viewer | U8-E1, planner ruling 8 October 2026 (`08-decisions.md` “U8 part 1”) | U8 | None; a later shared extraction is optional |
| Correction objective picker labels options per objective, grouped by tile, under the field label “Objective” (the reference says “Tile”) | Review — Review.dc.html correction form | U8-E2; RC07 R7 | U8 | None |
| The Decision card carries no box-shadow (the reference style block sets one on `.rv-side .decide`); page CSS cannot carry shadows (UI rule 20) | Review — Review.dc.html | U8-E3 | U8 | None; could move into a shared component later |
| History lines use the stored audit presenter wording, not the reference's sample strings (“Approved · added 1”, “Approval reversed · removed 1”, “Details corrected: …”); approvals show the stored detail line (for example “Approved contribution: 1.”) as the reason line, which the reference shows none for | Review — Review.dc.html History | U8-E4, provisional | U8 | User's call: keep or hide the stored approval detail line |
| Outcome and recovery wording is truthful to what is known: the result card reads “Approved” + “{N} added to {team}.” (the reference's toast “Approved · N added to team.”); after Check status the unchanged case reads “It wasn't saved. The submission is unchanged, so you can decide again.” (the reference's “Nothing changed, so you can decide again.” is used only for a definite server refusal, “Couldn't {action}.” + the server's reason); a changed record reads “We couldn't confirm your request, but the submission is now {status}.”; notices other than the uncertain one carry Dismiss | Review — Review.dc.html `:1052`, `:959`, `:1004` | RC07 R1/R2; UI rule 13 | U8 | None |
| Blocked approval (G1) and AU17/AU17a/S9 bindings as built: blocked shows “Waiting for an earlier upload” with “Open the earlier upload ({UTC})” preserving queue search/status; Contribution via `SubmissionContributionNumbers`; reversed shows “Removed N”; S9 warning from `creditedParticipantLeftTeamAt` and refreshed after a credited-account correction; “active account since” dropped | Review — Review.dc.html `:1169-1184` | Rows `:1217`, `:1218`, `:1219`, `:1244` above | U8 | None |
| Review decisions answer in place (JSON outcomes saved/refused/stale/blocked), the uncertain dialog never leaves on backdrop or Escape, Previous/Next follow the list the admin came from (order as at opening), the event window (actual start/end UTC, paused interval) is a workspace fact | Review — Review.dc.html | RC07 R1–R4, R-R16, R-R17; C-CMP-2 row above | U8 | None |
| U6-Q1: the finalized-roster correction Add keeps the role choice (Participant / Captain / Co-captain, one publication); the reference forces Participant | Teams — TeamsDraft.dc.html `:1142`, `:1505`, `:1512` | U6-Q1 (a), user decision 7 Oct | U6 / 1a–1b | **Bound in U6; reviewed (report 99); merged `2d83651e`; user accepted 8 October 2026** |
| U6-Q2: Teams readiness (setup and manual finalize) shows “Set the event end in Schedule” with a Schedule link when the configured end is missing or past; the Finalize rule is unchanged | Teams — TeamsDraft.dc.html (readiness `:166-179`) | U6-Q2 (a), user decision 7 Oct | U6 / 1a | **Bound in U6; reviewed (report 99); merged `2d83651e`; user accepted 8 October 2026** |
| T-24: Teams on Awaiting final review and Cancelled/Finalized/Archived events: the same workspace with the final rosters read-only and a state banner (“Rosters are final.” / “This event is finished/cancelled/archived. Rosters are shown as they were.”), no edit controls; in Final review the role menu stays only while uploads are open | Teams — TeamsDraft.dc.html (no such state) | Brief 93 T-24, planner ruling 1 (8 Oct) | U6 / 1a, 1c | **Accepted by the user 8 October 2026** (T-24 composition ruling; alternative of reusing the Live “locked” layout not taken) |
| Historical paused draft (Pause retired): read-only banner “Historical paused draft. Pausing was retired, so this draft is read-only and no roster changes are available.” | Teams — TeamsDraft.dc.html (no paused state) | Planner ruling 8 Oct (U6 part 1) | U6 / 1a | **Wording accepted by the user 8 October 2026** |
| Teams loading: family skeleton (readiness card, Teams bar, three team cards, real text-line heights), summary empty while loading, fresh on every visit; loaded content swaps in one step (no `fade-in`) | Teams — TeamsDraft.dc.html `:146-149`, `:166`, `:191` | Rules 2–7 (86-ui-rules) | U6 / 1a, 1c | None |
| Open Board (F1): finalizing stays on Teams; the header and the finalize toast offer “Open Board” only when the event has a board; otherwise the header button is inert with “No board yet. Create one on the Board page.” | Teams — TeamsDraft.dc.html `:1016`, `:1595` | F1; planner ruling 2 (8 Oct) | U6 / 0b, 1b | None |
| Running draft control: chip says “Another admin has control” (the controller’s name only in the take-over confirmation, which has no “last action N min ago” because the lease renewal is not an action); banners for “Another admin took control of the draft.”, “Your control lapsed.” and a lost live connection while in control (“Live updates stopped. … Control lapses 5 minutes after its last renewal.”, Reload) | Teams — TeamsDraft.dc.html `:1414-1419`, `:1549`, `:879`, `:1205-1209` | Brief 93 (control lease, hub loss) | U6 / 1b | Lost-connection banner wording accepted by the user 8 October 2026 |
| Running draft live updates: SignalR draftChanged re-reads the page state (not while a command or check is pending); control renewed every 120 s while held; sidebar auto-collapses while running and the admin’s own toggle is kept until the draft ends | Teams — TeamsDraft.dc.html `:1332`, `:1603-1608` | Brief 93 T-1..T-7 | U6 / 1b | None |
| Undo names the latest pick it shows (`pickId`); the server refuses a different latest pick (“The latest pick changed. Nothing was undone; the current board is shown.”) | Teams — TeamsDraft.dc.html `:915-929` | AU14 (Undo by id) | U6 / 1b | None |
| Turn strip/column pick number uses the scoped key `AdminDesign.Pick {0}` (Danish “Valg {0}”); the unscoped “Pick {0}” already means “choose” (“Vælg {0}”) | Teams — TeamsDraft.dc.html `:1405`, `:1371` | Rule 17 | U6 / 1b | None |
| Teams conformance: geometry on the setup workspace; the in-place update probe is the running draft’s pool sort on a fixture seeded only for this page (`BINGO_PARITY_DRAFT`, runner `fixtureEnv`, `update.url`, reference opened on its no-teams event) | Teams — TeamsDraft.dc.html | Planner ruling 8 Oct (option a) | U6 / 1c | None |
| Teams page state is embedded as `<template data-draft-state>` (a JSON `<script>` made every shell navigation to Teams a full page load: the shell accepts only page/shell scripts) | Teams — n/a | U6 1c finding | U6 / 1c | None |
| Removing a populated team (S7): the dialog says “Its {0} members go back to having no team. Nothing about the players changes; they stay signed up.” (the reference words the removal differently); the server refuses a populated team without the confirmation intent with “{0} now has members. Nothing was removed; check the team before trying again.” (a state the reference does not have) | Teams — TeamsDraft.dc.html `:1551` | S7 / planner ruling (U6 review L4) | U6 / remediation | Both wordings accepted by the user 8 October 2026 |
| U9-Q1: Reopen reads the other current event and disables the button with state-specific Live / Awaiting final review / legacy Finalized guidance plus fixed “Open event” link; no current-event publish-readiness row | Final Review — FinalReview.dc.html:711–713, :939–941 | Brief90, U9-Q1 / U4-L1; planner rulings 7 October 2026 | U9 0a / 1a / 1b | Bound in U9; reviewed (report 101); merged `cc039faa`; user accepted 8 October 2026 |
| B-Final-2 / RL-1 / BR-12 Final Review part: missing, zero or stale expected version refuses Reopen with no transition or audit write; Publish and Reopen return structured state and retain legacy redirect compatibility | Final Review — FinalReview.dc.html | Brief90; B-Final-2, RL-1 / BR-12 | U9 0a / 1b; Review remains U8 | Bound; no Review mutation code changed |
| B-Final-1 / AU18 / A15: readiness shows every applicable server blocker and uses domain SubmissionWindowOpen; only non-success per-version WOM notes, no next-eligible-time display; Pending/Rejected end update adds the last-pre-end-data warning to Publish, never a blocker | Final Review — FinalReview.dc.html:696, :930, :936 | Brief90; B-Final-1 / AU18 / A15 | U9 1a / 1b | Bound in U9; warning wording accepted 8 October 2026 (handoff wordings) |
| RC08 / A2 / C-CMP-2: event-scoped pending intent and reason survive reload; lost-response readback reports current state without attributing it; version query supports Back/Forward, invalid-drop and unavailable-version states; shared ranks are computed before the top-three slice | Final Review — FinalReview.dc.html | Brief90; RC08 F1–F6 / A2 / C-CMP-2 | U9 1b | Bound in U9 with shared Admin layers/fetch/update/busy; reviewed (report 101); merged `cc039faa`; user accepted 8 October 2026 |
| U9-Q2 / WA-6 / RC09: detailed Fetch skip reason and eligible time, lease-in-progress state, typed current operation and credential, last confirmed update from LastAppliedAt | WOM — Wom.dc.html | Brief90 final planner rulings; AU20 / WA-5 | U9 0b / 2a / 2b | Bound in U9; reviewed (report 101); merged `cc039faa`; user accepted 8 October 2026 |
| A15 / U9-Q2: unmatched end occupies the existing prioritized issue and Updates card; exact target and only a stored matching end-update operation retry; fetch paused, publishing available, no restart after publication/reopen | WOM — Wom.dc.html existing components | Brief90 final planner rulings; WA-2 / AU20 | U9 2a / 2b | Bound in U9; wording from docs/evidence/u9-handoff.md accepted 8 October 2026; no computed end-update retry |
| AU20 / C-WOM-2 / C-WOM-3 / WA-9: exact UTC window, service-owned replacement/disconnect/Create/credential/delete eligibility, three Create rows plus further concrete refusals, Unknown origin locked | WOM — Wom.dc.html | Brief90; 08-decisions; retired tolerance/capability wording | U9 0b / 2a / 2b | Bound; no external delete/code reuse/conflict recovery; string matching retained for preview refusals |
| RC09 W1–W6 / C-CMP-2: event-scoped drafts/pending state, clear submitted code, shared guarded dialogs, honest no-store readback, old fetch time never proof, unknown never Up to date; persisted Retry shown as queued | WOM — Wom.dc.html | Brief90; RC09 / WA-9 / C-CMP-2 | U9 2b | Bound; synthetic PostgreSQL/browser scenarios, no provider calls on GET |
| WOM Cancelled/Finalized read-only text, generic queued “latest details,” and handler-only development make-due | WOM — Wom.dc.html | Brief90 final planner rulings; D16/D17 | U9 2a / 2b | Wording accepted 8 October 2026; no visible make-due control or invented payload |
| Sidebar top row: the logo is its own link to the public front page `/` (full page load, no `data-shell-link`) beside the account button, which alone carries the hover highlight; collapsed, the logo stays and opens the account popout (the name/role button is hidden). The brand box holds the site's high-detail masthead SVG coloured white | Shared shell — every `*.dc.html` reference puts the house-icon logo inside the account button (`#user-btn`), which opens the account menu in both states | `08-decisions.md` “U10 part 2 additions” (U10-Q1, U10-Q2 a) and U10-E1 (user, 8 October 2026) | U10 part 2 items 4–5 (`claude/u10-part2`) | None; accessible names “Go to the public site” (expanded link) and “name · role” (collapsed button), Danish entries present |
| Topbar notification panel: header “Notifications” with the unread count, the newest unread personal notifications (unread dot) and unresolved Admin actions (no dot) merged newest first, at most 8 rows of title, one detail line and relative time, whole row a menu item; empty state; footer “All notifications” and “Mark all as read” through the existing `/notifications` handler (personal notifications only; it leaves Admin for the Notifications page) | Shared shell — the references show only a bell button with no popout | `08-decisions.md` “U10 part 2 additions” (U10-Q3 b) | U10 part 2 item 6 (`claude/u10-part2`) | Bound in U10 part 2 (merged `1eec9731`); user accepted 8 October 2026 (bell count rule U10 L3 (b); “Mark all as read” alignment fixed, merged `2dccc075`). The inbox projection lists only unread personal notifications (newest six), so every personal row carries the dot; the separate “Admin actions overview” link was dropped for the single footer |

These sweep rows are future binding obligations, not implemented UI claims.
The five brief40 server fixes are complete with binding pending. F1 belongs to Teams binding.


## Release and deploy gates

### Release-readiness gate

This is the release gate. The "recorded evidence" column holds sanitized
user/operator evidence from 3 October 2026; the agent did not access production or
run these queries. Before a release, the operator records the candidate SHA,
exact deployed migration history, and the check timestamp, then runs every
read-only check with separately authorized operator access and preserves the
baseline on an isolated restored copy. Mutating migration/conversion/Down checks
require the approved isolated procedure; never infer live database authority.
Aggregate counts do not establish the migration baseline.

| Check | Query or required step | Evidence recorded 3 October 2026 (before the October release; rerun for each release) | If the result differs |
| --- | --- | --- | --- |
| Banner cleanup (X-1/F1) | Verification only: exact migration history, zero `event_banner_assets`, zero `event_banner_cleanups`, zero events referencing `banner_asset_id` before retirement; if already retired, verify history and absent schema. See [runbook checklist](PRODUCTION_RUNBOOK.md#release-readiness-checklist--3-october-2026). | One-time targeted cleanup/object deletion reported complete by the user on 3 October; not agent-verified. | Stop on unexpected rows/schema; seek an operator decision. Do not repeat deletion or contact object storage from rehearsal. |
| Luck v1 conversion (LK-2/R-1) | Exact candidate `--convert-luck-checkpoints` stage after `--migrate`, before preflight/web, under approved isolated harness; require exit 0 and `Could not convert=0`. | R-1 remains blocking; a passing final-candidate restored-data conversion is not established. | Preserve v1 rows/failure reason; stop before preflight/web. No newer provider-data shortcut. |
| All retained completion corrections (BR-7) | `SELECT COUNT(*) FROM team_completion_corrections;` No global cycle-time parameter. | User's earlier 3 October zero is not a verified result for this corrected all-rows query; re-run required. | Any row stops release for investigation; readiness/ranking ignore retained corrections. Do not rewrite history/snapshots. |
| Published board without active finalized roster publication (BR-11) | `SELECT COUNT(*) FROM boards b WHERE b.state = 'Published'   AND NOT EXISTS (     SELECT 1     FROM draft_publication_cycles c     JOIN draft_sessions d ON d.id = c.draft_session_id     WHERE d.event_id = b.event_id       AND d.state = 'Finalized'       AND c.superseded_at IS NULL       AND EXISTS (         SELECT 1 FROM draft_publication_rosters r         WHERE r.draft_publication_cycle_id = c.id       )   );` | The user's earlier 3 October zero must be re-run with this corrected query; not a verified pass. | Any result stops release. Reconcile the existing publication lifecycle; event boolean alone is insufficient. |
| Future-effective account switches | At recorded check time, `SELECT COUNT(*) FROM event_participant_character_swaps WHERE effective_at_utc > :check_time_utc;`; inspect timestamps/attribution privately. | Unknown/unverified. | Stop for deterministic operator decision preserving submitted attribution/history; no silent cancellation/backdating. |
| G4 `20261003184632_AllowCancelledDraftRestart` | Before migration on restored baseline: count `draft_sessions WHERE state = 'Setup' AND first_pick_recorded_at IS NOT NULL`; after, same eligible set/count must have `requires_fresh_order = true`, ineligible rows retain default false. Separately verify Down removes column/history entry without draft/first-pick loss and re-Up backfills again. | Count and Down/Up unexecuted. If already applied in backup, report that instead of claiming a fresh backfill. | Stop on mismatch; use separate disposable clone and approved predecessor/tooling. No production downgrade. |
| R-3 final-candidate rehearsal | **Procedure and coverage limits approved by the user on 4 October 2026 after Claude’s review, as recorded in the planner decisions; attribution corrected by the user's Step 0 assignment.** Follow the [isolated runbook procedure](PRODUCTION_RUNBOOK.md#r-3-isolated-rehearsal-procedure--approved-4-october-2026): disposable VM/restored DB, denied external access, local WOM/HTTPS S3 fixtures, exact candidate restore/history → migrate → Luck conversion → preflight → web/health. Never host `bingo-deploy` as rehearsal. | Tooling not built/tested; R3 unexecuted; release blocked. Accepted limits: no production wrapper, real provider/restic/GHCR integration, public DNS/TLS or production-key recovery verification. Approval is documentation-only; Claude source recheck accepted with Step 0 attribution corrections. | Obtain separate tooling/backup-transfer/execution authority. Preserve R-1 failure blocking and every final-candidate check. Stop on any failure; no deployment until R3 passes and production deployment is separately approved. |

The gate is complete only when each release-time query/step has a recorded
candidate identity and passing result. The runbook owns the operator sequence;
this section owns the release decision and the explicit stop conditions.

Keep the complete infrastructure and operational checklist through release,
including optional but prudent safety items. Evaluate each item when its
deployment step approaches and present provider/tier options, current costs,
tradeoffs, a hobby-project recommendation, and the consequence of deferring or
omitting it. Optional does not mean silently removed. Do not create an external
account, purchase a service, enable a paid tier, accept a credential, change DNS,
or mutate production without the user's explicit approval. Keep repository-side
automation provider-portable where practical until a choice is required.

### Deployment contract

**Topology.** The single-VPS Compose contract defines Caddy, one ASP.NET Core web replica,
and private PostgreSQL networking; persistent PostgreSQL, data-protection,
catalogue-cache, and Caddy state/config volumes; immutable image input; the
R2/Discord/Wise Old Man/bootstrap configuration names; and clean-start/operator
assumptions. 
**Application operations.** Persist and startup-validate the configured Production
data-protection key ring; retain the private-Caddy forwarded-header model; use
the built-in Production JSON console logger; keep public `/health/live` cheap;
make container-internal `/health/ready` gate PostgreSQL, configured R2 bucket
reachability, and timely heartbeats from both existing hosted workers; keep Wise
Old Man non-blocking; add explicit migration and read-only production-preflight
commands without normal-startup migration; and initialize non-root ownership of
the writable data-protection and catalogue-cache volumes. Clean setup restores
the reviewed database backup after migration, then runs owner bootstrap,
preflight, and web/Caddy. Retained data runs the legacy Slice 1 preflight only
when crossing that boundary, then migrate, production preflight, and replacement.
The catalogue snapshot loader is reserved for CI, Development, and manual-test
data and is never applied by production deployment.

**Release-candidate publication and promotion.** Preserve the existing PR/main CI
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

**Deployment and recovery.** The promotion workflow has explicit `promote` and `deploy` modes.
Candidate validation runs before either action; the explicit manual
`workflow_dispatch` selecting `mode: deploy` is the user's production approval.
The workflow uses non-cancelling `concurrency: production` and transports only
SSH data plus validated release metadata. Native OpenSSH invokes the narrowly
sudoable root-owned host command.

The repository carries only the minimal host deploy, encrypted restic backup, isolated restore
verification, and database-stored evidence-integrity scripts; root-only host
configuration examples; one systemd backup service/timer; and the deployment
runbook. Deploys back up before every replacement, use the exact image digest,
preserve Caddy/PostgreSQL, run the existing clean/retained migration and
preflight commands, verify internal and public health, and emit a secret-free
receipt. Changed migration history never permits automatic image rollback.
The exact operator procedure is in [`PRODUCTION_RUNBOOK.md`](PRODUCTION_RUNBOOK.md).

## 4. Dependencies, approvals and stop rules

- Use `UI_SYSTEM.md` for global UI rules and `UI_PAGE_MATRIX.md` for page
  family/reference/exception/approval decisions.
- Major functional slices still require the formal readiness review in
  section 4.1 below. Ordinary UI passes use the lean sequence: agree page/result,
  bounded implementation, one independent review when appropriate, focused
  remediation, and manual acceptance.
- Live identity and display timezone stay read-only; the Live event-end
  correction is a Schedule capability. The approved five-step `/HowTo` guide is
  the scope: no Rules editor, sixth step, or in-application HowTo editor.
- Stop before adding product behavior, changing an approved rule, adding
  unbudgeted persistence/routes/policies/jobs/abstractions, or fixing an
  unrelated defect. Preserve existing routes for deep links, reload/history,
  authorization, audit, concurrency, privacy, and historical records; no
  separate no-JavaScript parity work is planned or gated.
- Stage, commit, push and deploy only after the relevant acceptance and
  explicit authorization.

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
slice plan. Carry the manual steps into the brief or the slice plan; do not create a
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

The current roles, models and routing are owned by
[AGENTS.md](../AGENTS.md#active-workflow-and-model-defaults--2026-10-05): the Claude
planner chat defines the work, writes the brief and reviews each batch independently;
the Codex planner chat named in the brief dispatches one implementer per batch with
the model the brief names. No orchestrator, Codex reviewer or verifier is used unless
a brief assigns one. Historical assignments elsewhere do not override this policy.

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
3. The dispatcher starts the implementer with the approved brief and exact model.
   The implementer completes a connected change and runs its checks. After one
   focused lookup, consequential uncertainty goes to the dispatcher with evidence and
   a recommendation; product and scope questions go on to the planner and the user.
   Continue independent authorized work if useful.
4. The dispatcher resolves ordinary execution questions and checks stalls and
   interrupted work. Repeated reading without progress calls for a narrower next
   step, not another open-ended continuation.
5. The implementer commits each item, runs the affected .NET tests, full JS runner,
   Chromium/WebKit checks, required Release build and scoped design/diff checks,
   and stops at the brief's boundary with its report. Only the planner runs the
   whole .NET suite once on the final SHA, in the background as soon as that report
   arrives: the batch gate requires zero failures and zero skipped tests before
   acceptance (08-decisions.md, Q-S1, user decision, 7 October 2026). Gate failures
   return as remediation. The planner reviews the stable batch independently;
   findings return as a remediation brief to the same
   implementer, followed by a named recheck. Honor explicit user review waivers; do
   not claim skipped checks passed. For Admin popups, applicable review is source-only
   and visual acceptance is the user's.
6. At the stop boundary the implementer consolidates the active `CURRENT_STATUS.md`
   entry with the checkout/branch, assigned pass, completed work/checks and durable
   repository evidence locations (not only temporary paths), unresolved findings,
   acceptance state and exact next permitted action. Link this procedure; do not
   copy it into the handoff or rely on chat history. Preserve approval authority in
   `UI_PAGE_MATRIX.md`.

Use this compact worker brief; include only relevant facts and authority sections:

```text
Role and approved model/reasoning:
Planner and dispatcher chat IDs; worker identity if known:
Checkout / branch:
Observed problems and expected results (agreed fix list):
Starting files/helper and established evidence:
Protected behavior / non-goals / relevant authority sections:
Required checks, batch gate and stop boundary:
Escalation: after one focused lookup, send consequential uncertainty to the
dispatcher with evidence and a recommendation; product and scope questions go to
the planner and the user.
Return: commit SHAs, changed files, checks/results, unresolved findings and next
permitted action.
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

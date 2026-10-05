# UI System

## Authority, scope, and precedence

This is the active global UI authority for the OSRS Community Bingo platform.
It defines shared visual primitives, ownership, responsive behavior, and the
review contract. It does not approve a page by implication.

Current page approval and status are owned solely by
[`UI_PAGE_MATRIX.md`](UI_PAGE_MATRIX.md). This document defines global UI
rules, exact implementation ownership, and protected composition; it does not
create a page approval ledger.

Authority is resolved in this order:

1. `CURRENT_STATUS.md` owns canonical checkout state, blockers, verification
   limitations, current work, immediate ownership, and unresolved decisions.
   Its compact UI table is a non-authoritative snapshot derived from
   `UI_PAGE_MATRIX.md`.
2. `UI_PAGE_MATRIX.md` owns page families, canonical references, protected
   composition, exceptions, current approval/status, and next gate.
3. `UI_SYSTEM.md` owns global primitives and rules shared by those pages.
4. `DELIVERY_PLAN.md` owns documentation/UI pass order and release gates.
5. `docs/archive/` is historical evidence only and is never authority.

Archived documents, root tombstones, old pass notes, selector presence, and
page-local CSS cannot override these active sources. A canonical reference is
not the same thing as approval in the current regression.

## Approved Admin reference direction — 2 October 2026

For the new Admin overhaul, `UI_PAGE_MATRIX.md` names the accepted pages and
`docs/references/admin-ui/` holds their committed design. Canvas 42 / published
artifact `1790965722-e7ad` is frozen in the 2 October manifest. The user explicitly
selected this design over older Admin visual rules, including filled primary
buttons. Its light/dark typography, spacing, surfaces, focus, motion, responsive
tables and component hierarchy are the target; Public UI rules are unchanged.

The reference's `ui/tokens.css`, `ui/components.css`, `ui/behavior.js` and Components
preview are the shared design owners. Reuse them as the design contract during
application integration; map runtime ownership deliberately into the existing app.
`support.js`, vendor canvas support, fixture data and simulated requests/URLs are
reference runtime machinery, not production services or mandatory dependencies.
New reusable components may be added when needed, consistent with this visual
language and documented in the gallery. Page-specific composition need not become
a generic component. Underlined actions already accepted are intentional exceptions.

Older Admin dimensions, fonts, outline-only/button prohibitions, forced table-to-
card transitions and no-stacked-dialog rules below describe the prior application,
not competing targets for these accepted pages. Keep wide tables horizontally
scrollable. Layered confirmation is permitted with the underlying editor inert,
appropriately dimmed, scroll locked and focus returned. Keep existing accessibility,
dirty/pending/conflict/uncertainty and security contracts. New-reference visual
acceptance is not application binding or acceptance of newly exposed feedback.

Approved production shell decisions (plan42 group A, 5 October 2026):

- A3/A4: account menu contains Account settings, Change password, View public site
  and Sign out. Notifications bell is in the top bar; its Admin actions overview
  opens that section of `/notifications`. English/Danish is a two-state switch
  beside the theme switch. Drop the blocker chip and the old admin header description;
  retain the reference summary line under each page title (5 October clarification).
- A6: theme initially follows the OS; remember a manual choice per browser and
  apply it before first paint. Keep the switch in the reference top-bar position.
- A7/A11: switcher includes only the current Live/Final-review event and upcoming
  Draft/Signups-open/Signups-closed events, soonest start first. Exclude Cancelled,
  finished, Archived and Discarded. A past event still names the button but is not
  listed. SuperAdmins additionally see qualifying hidden events marked Hidden;
  those open Overview's limited view. Other choices keep the current page. Scroll
  after about eight rows; All events stays below the list.
- A12: outside click closes only a layer without input. Confirmations and layers
  with unsaved input stay open. Everything that can lose progress must be a modal
  that does not close on an outside click. If a reference shows editable input in
  a layer that closes on an outside click, bind it as not closing and report that
  reference exception. Preserve layered focus trapping and restoration.
- A13: no sidebar item counts.
- Product brand (brief43 item3.5, not A13): DK Legacy.
- A16: new-shell links use a shared dirty guard, target-page skeleton, normal HTML
  fetch and content/title/breadcrumb/nav/script swap, with Back/Forward and a
  failed-load Try again state. Unexpected responses and old-layout targets fall
  back to a full load. One site-wide setting, on by default, disables swaps.
  Page scripts provide `init(root)` and `dispose()` removing listeners, timers and
  connections; repeated event switches must leave no stale work running.

U1 review decisions (user approval, 5 October 2026; brief50 / decisions08):

- U-A: Check again encountering session loss says signed out / could not check
  whether the change went through / sign in and Check again. Keep the draft;
  never say not saved for an unknown write outcome. Ordinary save-session loss
  retains its separate not-saved notice. Both show localized field labels.
- U-B: the top-bar bell shows the existing unread-item number badge and announces
  that count to screen readers. This does not introduce sidebar counts.
- U-C: switcher stage · when follows the reference: closes plus signup closing date
  while signups are open, starts plus event start before play, ends plus event end
  in Live/final review, or not announced when unset. Unsupported stored timezone
  uses UTC for that date text.
- U-E: event breadcrumb is plain text; failed loads show the reference icon/title;
  menus use reference placement and exit animation; collapse labels toggle. Pages
  may provide their own loading skeleton, with a generic fallback. Use the existing
  DK Legacy mark image in the reference logo position. The sidebar header shows
  the signed-in public username, localized role and chevron; the account menu adds
  the name / @handle · role header (visual-check Q1/Q2). Hide event-specific nav
  links when no event is selected or remembered. The reference nav label is Teams / Draft (R17).
  User decision, 5 October (brief60 item2): a browser-session cookie stores only the
  last Admin event ID, revalidated against current visibility/access on every render.
  Community pages retain its sidebar navigation without an event breadcrumb; public
  navigation keeps it, selecting another replaces it, and sign-out clears it. Past
  events remain selectable context; discarded/deleted/inaccessible context is dropped.
- U-F: Identity makes unchanged Save inert with `aria-disabled`, remaining focusable
  with the No changes to save tooltip and no visible no-change status. Reverting
  typed edits restores the clean status. Timezone
  review uses Cancel and Show this event’s times in ‹zone›? / Save with ‹zone› and names both
  zones. Scheduled moments are rows; unset moments share one Not scheduled yet:
  … line (or Nothing is scheduled yet, so no shown times change.). The quiet note
  after successful readback is Up to date, without attributing a save request.
  Follow the complete reference presentation, including title summary, icons,
  shared busy spinner/labels, field notes/counters, dialog structure and banners.
  The timezone select shows zone ID with current UTC offset (Q4); Description
  and Buy-in accept over-limit typing with counter/error, while server refusal
  remains mandatory (Q6).
- U-G: Danish new-shell/Identity terminology is event/events (Events, Alle events),
  not Bingoer. Scoped resource keys preserve legacy-page translations.

U1 visual-check clarifications (5 October; brief55): language changes save through
existing `/language` POST then swap translated page/shell without a skeleton,
retaining sidebar, scroll and focus. Highlight after the dirty guard; use the theme
segment transition and a `--dk-dur-theme` text cross-fade (none with reduced motion).
Keep editing preserves draft and language; unavailable swaps fall back to reload.
Loading/failed navigation already shows the destination event in switcher/crumb
(Q7, as the reference); Back and cancelled guards restore the correct context.
Toasts use finite reference lifetime and animated exit, without hover pause; on
phones they sit above the sticky save bar (Q5). Every save/action uses shared
`AdminUI.busy` (600 ms, quick 250 ms only for one-click live-draft actions).

User decision, 5 October 2026: “Move the light switch and language switch into the hamburger”, clarified “This is obviously only on mobile widths.” At the existing mobile/off-canvas breakpoint (max-width: 860px), both controls live inside navigation; the bell remains in the topbar. Above that breakpoint their topbar placement is unchanged. Theme/language behavior, dirty guards and keyboard access remain unchanged. This approved mobile placement gives the event breadcrumb at least the reference reading width.

There is no U-D decision. Shared navigation retains sidebar DOM/collapse state,
updates current/event-dependent links, closes clean layers before swaps and guards
layers with unsaved input. Fragment-only history changes stay native; widening
past 860 px closes mobile navigation and releases content interaction.

U1 page-script contract: each opted-in page supplies a same-origin ES module with
`<script type="module" data-admin-page-script src="…">`. It exports
`init(root, ui)` and `dispose()`; top-level module evaluation must not initialize
the page. `root` is `[data-page-region]` and `ui` is `window.AdminUI`. Register
owned drafts through `registerDraft(owner, { isDirty, discard, isPending })` or
`trackForm(form)`; dispose the returned registration as well as all owned event
listeners, timers and connections. Async work must be aborted or ignored after
disposal. Optional page CSS links carry `data-admin-page-style`. The shell keeps
its own controls/listeners alive, disposes the previous page before fetch/swap,
and initializes only the new page. Configuration `AdminUi:InPageNavigation`
defaults to `true`; `false` retains the shared dirty guard but uses full loads.
Filter/search updates use `AdminUI.setUrl` (replace); record opening passes
`{ record: true }` (push). Query schemas validate and omit defaults. Page owners
supply a `<template data-page-loading-template="identity">` (using their page
kind) for their loading composition. The shell remembers those templates and
uses a generic skeleton when none is available. A discarded draft is never stored in browser history.

New pages use `AdminFetch.request(url, { expect, draft, labels, readback, …fetchOptions })`
for their handler traffic. Field-keyed draft values and a separate localized labels
map populate the session notice; readback selects U-A's uncertain-outcome wording.
Non-2xx responses are unknown with their status before HTML/session classification;
only successful HTML in place of expected JSON can indicate a lost session.
Handle `handler`, `session-lost`, `refused` and `unknown` separately; unknown writes
require the owning page's readback contract, never automatic resend. Only an
explicit same-page PRG destination may be allowed through `allowRedirectTo`, and
the page still validates the returned data before showing a result.

Exit timing derives from computed shared CSS animation and completes even when no
animation runs. U1's selected shared busy minimum is 600 ms for saves and 250 ms
for one-click actions; reduced motion removes the wait (implemented and
proved by U1 item4). It never delays the backend operation or shows success before its
response. Block repeat submission while pending. No toast Undo is approved.

## Rule promotion and ownership

Page-specific is the default. A page rule becomes global only through an
explicit user decision or by correcting an already physically shared
primitive. Replace active wording in place; Git and the archive preserve the
history.

Active stylesheet ownership is explicit and preserves the prior cascade in
this order:

1. `src/Bingo.Web/wwwroot/css/site.transitional.foundation.css` owns the
   existing foundation and shell rules that have not yet been assigned to a
   narrower semantic owner.
2. `src/Bingo.Web/wwwroot/css/site.public-ui.css` is the sole destination for
   reusable Public UI overhaul tokens and semantic primitives represented in
   `/Admin/PublicUi`.
3. `src/Bingo.Web/wwwroot/css/site.transitional.application.css` owns the
   remaining existing Public, Admin, and page/application rules until their
   surfaces are migrated and verified.

The transitional files are mixed ownership, not a claim that every contained
rule is legacy. `src/Bingo.Web/wwwroot/css/site.css` is a compatibility marker
only and must not receive rules. Split transitional rules by semantic owner
only when their consuming surfaces have been migrated and verified.

“Global component” means an exact implementation owner, not similar prose.
Ownership is recorded at three levels:

- Atomic appearance belongs to the matching active stylesheet owner in the
  ownership map above.
- Repeated markup or behavior belongs to a shared partial, helper, or JS
  module only when that file physically renders or owns it.
- A repeated page-local composition is a canonical pattern, not a shared
  component. It must be named as such until a real owner exists.

The Admin shell is physically owned by
`src/Bingo.Web/Pages/Shared/_AdminLayout.cshtml` and its Admin CSS in
the transitional stylesheet set. Questions uses the static dialog host in `_AdminLayout.cshtml`
plus its `signup-questions-overlay.js` module. Accounts and Catalogue
create/manage their dialog hosts through their page-specific JavaScript
modules, `account-manage-dialog.js` and `catalogue-admin.js`.
`_AdminOverlayLayout.cshtml` is the standalone/fallback presentation shell
only, not the shared dialog host. These implementations follow the canonical
route-dialog behavior and must not spawn new variants. Toast markup is
physically shared by `_TransientToast.cshtml`; toast behavior is in `site.js`.

## Tokens, typography, hierarchy, and surfaces

- Admin tokens are the `--admin-*` variables in the transitional stylesheet
  set, activated by
  `body.admin-shell-body`. Surface, soft-surface, raised-surface, divider,
  text, muted, status, danger, navigation-accent, and focus-accent tokens are
  the only source for shared Admin appearance.
- Admin uses the Plus Jakarta Sans family loaded by the shared Admin layouts.
  Public pages may use their own public hierarchy while remaining within the
  public dark design language.
- The Admin shell reference is a 64px header, a 232px wide desktop sidebar,
  and a 24px content gutter. The shell owns these relationships.
- Components use the compact operational hierarchy: page context, component
  title, support text, field/row label, value, and low-priority explanation.
  Do not promote a page heading or support size from a single page selector.
- Admin pages whose purpose is not obvious provide shell description text
  through the existing mechanism and do not duplicate it inside page content.
- Surfaces are tonal and borderless when the owning primitive says so. A
  nested surface must explain a distinct concern; do not box every field.
- Shared spacing follows the existing Admin primitives: 20px component/row
  spacing, 20px component padding where the operational surface calls for it,
  12px component/row radius, 36px fields, 32px neutral/danger actions, and
  26px state pills. Page exceptions are matrix entries.
- Primary content and actions align to the owning layout. Final actions sit
  at the end of their row; low-priority navigation does not compete with the
  primary action.
- New Admin primary actions use the accepted shared reference treatment, including
  filled primaries. Neutral and destructive variants retain their distinct roles.
  Use shared theme tokens, not page-local color substitutions. The older outline
  treatment remains existing application code until the authorized integration.

## Layout families

### Full-width data/table

The physical CSS owners are `.admin-events-directory-controls`,
`.admin-events-toolbar`, `.admin-search-field`, `.admin-filter-select`,
`.admin-events-table-wrap`, `.admin-events-table-scroll`, and the
`.admin-events-page` table rules in the transitional stylesheet set. The canonical rendered
reference is `Pages/Admin/Events/Index.cshtml`.

The toolbar and data surface span the remaining Admin canvas. Wide tables use
a tonal header and borderless rows. At `max-width: 1100px`, headers become
label/value cards; the table is not squeezed into an unreadable desktop grid.
Standalone table search fields use a canonical `18rem` desktop maximum and may
become full-width only at constrained widths.
Accounts and Participants reuse the family’s CSS primitives but retain their
own table markup and are not one hidden generalized table component. When a
table has a named group/title, its heading and support text belong inside the
table’s owning surface immediately above the column headings; the search/filter
toolbar remains its own surface above it. Do not invent toolbar row-count
badges unless a page-specific approved reference explicitly requires one.
Named table-record management uses a visible `Actions` heading and the
established record-action pattern; the removable-object X is not used for
table record management.

### Detail/form with optional information rail

The retained rendered rail references are Manage/Overview and Schedule.
`.event-manage-layout` and `.schedule-editor-layout` are page-local compositions
with CSS owners in the transitional stylesheet set; no shared rail partial exists.
U1 Identity uses the frozen reference form-card composition in the opt-in Admin
shell; it has no information rail. Its binding awaits the matrix acceptance gate.

On wide desktop, a detail/form page may use a sticky information rail. At
constrained widths the rail is removed, not moved below or into the form.
Full-width/table pages do not gain an information rail.

### Workspace/canvas

The canonical Admin reference is `Pages/Admin/Events/Board.cshtml`: the
page-local `.board-workspace`, `.board-canvas`, `.board-sidebar`, toolbar,
tile dialogs, and drag/swap states own the composition. It is not a table
family and must retain spatial context for editing.
The protected Board desktop reference viewport is `1280 × 720`.

The public workspace reference is the `View bingo` state in
`Pages/Events/Board.cshtml`, with public team/tile routes as responsive
fallbacks. Its public overlay/drawer behavior is separate from Admin.

### Public/participant surface

Public board, team, tile, signup, and evidence surfaces use their physical
page markup and public CSS classes. The approved replacement identity is an
editorial competition system: condensed display typography, strong mastheads,
open layouts, thin rules, compact activity strips, restrained geometry,
outline-first controls, and readable hierarchy. Participant surfaces may add
authenticated controls without changing the public visitor hierarchy.

The Public UI rebuild protects behavior, not legacy public presentation
composition. A migrated family may preserve handlers, PageModels, routes,
authorization, localization, data, and interaction semantics while replacing
its Razor structure, layout containers, geometry, typography, links, buttons,
icons, and responsive CSS. Retaining generic legacy composition and adding a
family body class plus overrides is not a completed migration.

Light uses canvas `#FFF9EC`, blueberry `#243B8F`, ink `#111528`, coral
`#F05A3E`, sage `#5E806B`, and bronze approximately `#A86F45`. Dark uses canvas
`#1B1A1D`, header `#111216`, field `#242326`, cream `#F4EEDF`, warm gray
`#B8B1A8`, divider `#4B494B`, smoky indigo `#6F789F` sparingly, plus the same
coral/sage/bronze semantic states. Light is the default. Dark mode changes
theme tokens only and must preserve composition, dimensions, spacing, tile
geometry, outlined tile numbers, content and artwork placement, typography
hierarchy, navigation, and interactions with no theme-switch layout shift.

Landing theme tokens must separate semantic roles rather than reuse a color by
coincidence. At minimum define shell background, content accent, primary text,
muted text, rule, art field, and the coral/sage/bronze states. Dark shell remains
`#111216`; it must never feed content accents. Dark identity accents use the
approved smoky-indigo direction, with a lighter derived value where small text
needs contrast. Ordinary hero/event text uses cream or warm gray as appropriate;
large numerals use cream or intentionally contrasted smoky indigo consistently.
Large structural/display numerals may consistently use smoky indigo in dark mode;
this does not authorize violet for ordinary structural rules or input borders.
Every visible dark text/control and hover/focus/disabled state requires rendered
computed-color contrast verification, and dark geometry must match light exactly.

Pages using `_Layout.cshtml` use the accepted Landing `landing-shell-*`
header/navigation as their one shared public header owner. `_Layout.cshtml` must
not branch to a second `public-live-header-*` composition for non-Landing pages.
The public header's Current event shortcut is available to everyone, including anonymous
visitors, only when a public, non-hidden Live or Awaiting final review event has a published
board. It links to the current board, following the public listing's Live-first/latest-start
ordering. It replaces the main-header Captain/Submissions entry on desktop and mobile.
Captain/Submissions instead appears after Teams in the event context navigation, only for
the user's eligible scope in that viewed event. Authorized submissions overview/detail
retain the same event navigation and active indication. EN/DA labels are required.
The shared owner retains the DK mark, dynamic navigation, notification/account
and settings behavior, route behavior, mobile menu, focus treatment, and
localization. Navigation between sibling views inside one
section is not an extension of the colored masthead. Event and account context
links render immediately below it as a content-level tab row matching PUB-REF-02:
condensed uppercase utility labels, transparent canvas, generous horizontal
spacing, and a clearly weighted accent underline on the current view. Pages with
this row reduce their ordinary masthead-to-content top gap so the secondary row
and page heading read as one composition. This reduced-gap variant is a global
composition rule and supersedes ambiguous spacing in a page reference; pages
must not restore ordinary masthead padding through a page-local override. The
ordinary top gap for a page without this view-navigation row is
`clamp(2.25rem, 5vw, 5rem)` on desktop and `2rem` at `680px` and below. The
reduced top gap for a page with the row is `1rem` on desktop and `0.75rem` at
`680px` and below. Full-bleed masthead artwork may reach the viewport edge, but
must not widen the owning page-content container or its section rules. A
full-bleed masthead-bottom divider belongs to the masthead itself and is removed
from page-content flow; it must not be implemented as the content wrapper's top
edge or as a grid item that forces the content to full viewport width.

The public Teams/Hold roster uses this sibling row only after the event Board is
published. Its existing roster masthead remains page-owned and unchanged beneath
the row; it does not inherit or duplicate the Board overview masthead. Before
Board publication the roster remains a standalone destination with the ordinary
top gap and no links into the unavailable Board family.

Stats renders the actual shared Board/Drops/Leaderboards masthead partial for
the same event, including its Event overview title, lifecycle countdown/status,
result, player metric selector and conditional submission/WOM actions. It uses
the same cached activity, roster ranking and authorized submission destination
preparation; there is no Stats-specific replacement fact/action set.
Its masthead and dashboard share the Stats border-box content shell: `1640px`
maximum width, centered, with `3.6%` side padding (`5%` at `850px` and below).
Shared Board-family artwork and rule behavior apply; the Board's `88rem` page
width must not constrain Stats. Boss KC leaderboard account values render WOM's
`-1` missing sentinel as an em dash, like null, in both Teams details and Players;
missing start/end values do not enter numeric aggregate sort keys. Boss team
average gains display rounded whole numbers while retaining signed green gains,
contributor-only averaging and full calculation/sort precision. EHB and Drop EHB
formatting remain unchanged.

The Privacy masthead is the implementation authority for the ordinary Public
UI masthead text stack. Its present roles map to a shared semantic copy role:
the kicker is Barlow Condensed Public UI 600 at `1.1rem/1` with `.045em`
tracking and uppercase text; the title is Barlow Condensed Public UI 800 at
`clamp(3.6rem, 5.2vw, 5.8rem)/.82` with `.01em` tracking and uppercase text;
and supporting copy is Geist Public UI 400 at `1rem/1.42`, with no margin and a
`48rem` maximum width. The text stack is a grid with an internal `.65rem` gap,
zero top padding, and `1.35rem` bottom padding when it owns the masthead's
bottom edge. The ordinary masthead kicker uses the accent/blue in light mode
and primary cream/ink in dark mode; dark kicker text must not use violet/smoky
indigo. This implementation contract is distinct from the `20px` Barlow
Condensed SemiBold Utility lead row used only for reference generation.

The ordinary shell-to-masthead gap is `clamp(2.25rem, 5vw, 5rem)` on desktop
and `2rem` at `680px` and below. The content-level view-navigation variant is
`1rem` on desktop and `.75rem` at `680px` and below; among the normalized
families, only Board uses that reduced variant. Page-specific facts, controls,
artwork, metadata, rails, and actions remain outside the shared text stack.
Primary masthead navigation uses the
same label-owned underline geometry: the rule sits beneath the text with internal
padding rather than on the masthead's bottom edge, and its thickness matches the
secondary current-view rule. Primary and secondary navigation use the same
text-color-only hover cue in both themes: inactive and selected labels share the
same full-strength resting text color, while hovering an inactive label fades its
text. The selected label remains full-strength and underlined. Hover must not move
geometry, add a background, or show an active underline on inactive links. Preserve the
existing destinations, conditional visibility, `aria-current`, keyboard focus,
and narrow horizontal access; do not introduce a second navigation framework.
The rebuilt shell uses stable public page-family width/layout primitives rather
than the old problematic responsive containers. Public rendering covers
desktop, narrow/tablet, mobile, zoom/translation, keyboard/focus, empty, error,
and permission states. Subtle useful transitions respect reduced motion.
The shared public-page width contract is:

```css
--public-page-gutter: clamp(1rem, 4.25vw, 4.25rem);
--public-page-standard: 54rem;
--public-page-structured: 64rem;
--public-page-wide: 88rem;
```

Standard owns ordinary forms, settings, and password pages; Structured owns
onboarding and substantial multi-column workflows; Wide owns Signup,
Confirmation, account overviews, inboxes, public directories, and the public
Board overview. Board uses the normal shared responsive gutter. The approved
Landing, Login, and 403/404/405 compositions are protected exceptions: do not
change their existing widths, gutters, or alignment while introducing this
shared contract. Component
and dialog widths remain component-owned rather than becoming another page-width
category. The gutter controls viewport-edge spacing only, while page-specific
alignment is an intentional part of each approved composition and must not be
normalized by the shared width primitives.

### Public section, form, and action rhythm

My Accounts and Settings are the implementation authorities for the shared
Public UI content rhythm. They use one typographic, spacing, field, focus, and
action system with two peer-section delimiter variants plus a compact secondary
summary-rail composition. A migrated page chooses the composition that expresses
its information architecture; it does not mix peer-section variants in one
sequence or invent a near-match.

The shared content rhythm is:

- A new major content region starts `2rem` after the preceding masthead or
  peer region. The region heading is followed by supporting copy at roughly
  `.45rem`. Section content begins `1rem` after the final intro line: after the
  supporting copy when it exists, or directly after the heading when it does
  not. The next peer region returns to the `2rem` major-region gap. Do not use
  arbitrary empty height to align neighboring columns.
- A section body uses `.8rem` as its normal vertical rhythm. Sibling fields
  use an `.85rem` gap; a roomy desktop-only horizontal form may use `1rem`.
  Field labels sit `.32rem` above their controls. These values remain when a
  layout changes columns; stacking must not double the vertical rhythm.
- Major region headings use Barlow Condensed Public UI 600 at `1.45rem/1`,
  `.045em` tracking, and uppercase text. Field and compact data labels use the
  same family and weight at `1rem/1.1`, `.025em` tracking, and uppercase text.
  Standard explanatory copy uses Geist Public UI 400 at `1rem/1.42`, with a
  `48rem` readable maximum. Compact support, metadata, and field help use
  Geist Public UI 400 at `.9rem`; validation copy uses `.82rem` and the danger
  token.
- Text inputs, textareas, and selects are square, full-width controls with a
  `2.55rem` minimum height, `.5rem .65rem` padding, Geist Public UI 400 at
  `1rem/1.2`, the semantic field background, and a `1px` neutral rule border.
  Icons, clear controls, and select chevrons may increase only the affected
  inline padding. Focus uses a `2px` accent outline with a `3px` offset and
  must not change geometry. Dark mode may set `color-scheme: dark` but does
  not alter control dimensions.
- Adjacent controls align by their control boxes, not their labels. An action
  beside a field matches the `2.55rem` control height and aligns to the bottom
  of the field. A separate action row begins `1.15rem` after its form content,
  uses a `1.25rem` gap, and wraps rather than compressing controls.
- Normal Public UI actions are square accent-outline controls using Barlow
  Condensed Public UI 600 at `1.08rem/1`, `.045em` tracking, uppercase text,
  a `2.5rem` minimum height, `.65rem 1rem` padding, and a `.55rem` internal
  gap. Hover may fill the accent while retaining contrast. Destructive actions
  use the same geometry with the danger token. Low-priority text actions use
  the same type hierarchy, a `1.8rem` minimum hit height, and no inline padding
  or button-like border; they must not reserve button-sized whitespace inside
  data rows.

The approved section compositions are:

1. **Numbered workflow** — use for ordered, task-oriented sections such as
   Settings. Each section is a two-column row with a `5.8rem` index track, a
   `1.1rem` gutter, `1.45rem` top padding, `1.6rem` bottom padding, and a `1px`
   neutral bottom rule. The index uses Barlow Condensed Public UI 800 at
   `3.35rem/.85`; the body uses the shared `.8rem` rhythm. At `680px` and below,
   the index track becomes `3.8rem`, the gutter `.65rem`, and the index
   `2.7rem`. Do not add a ruled heading to the same peer-level section.
2. **Ruled ledger** — use for directories, filters, editable lists, and data
   groups such as My Accounts. The heading and its rule share one flex row with
   a `.8rem` gap; the label never shrinks and the rule fills all remaining
   inline space at `2px` thickness. Do not add a leading full-width rule above
   the same heading. On light canvas the label and rule use the content accent;
   in dark mode the label uses primary text and the rule uses the neutral
   divider. Row separators inside the region remain `1px` neutral rules and do
   not reuse the major heading color.
3. **Summary rail** — use for compact secondary facts beside a primary workflow,
   as on Signup. This is not another ruled-ledger section. At desktop, separate
   the rail with an inset `1px` neutral vertical rule. Its heading uses Barlow
   Condensed Public UI 600 at `1.2rem/.9`, with a short `3px` underline belonging
   only to the heading text; do not extend that underline across the rail. Facts
   begin `1rem` below the heading and form compact two-column label/value rows
   with a `.55rem` rhythm, `.45rem` top padding, and `1px` neutral row rules.
   Labels use the compact support hierarchy and values use Geist Public UI 600
   at `.85rem/1.25`; reserve display numerals for genuine headline metrics, not
   every fact in a summary rail. When stacked, remove the vertical rule and begin
   the rail with a `1px` neutral top rule plus `1.2rem` top padding.

All compositions preserve the same hierarchy and theme semantics: light uses
blue for major structure and focus; dark uses cream for major heading text,
neutral rules and field borders, and the approved smoky indigo only for
intentional data emphasis. Page-local selectors may arrange columns and data,
but may not redefine this rhythm, control geometry, action hierarchy, or theme
mapping without a recorded page-family exception.

English is the default and every user-visible Public, account, participant,
Captain, and Admin string is localized in English and Danish through the
existing localization system. This includes visible copy, validation and
mutation feedback, notifications, dialogs/drawers, empty/error states, toasts,
confirmation prompts, titles, labels, and accessibility text. JavaScript
receives localized visible values through server-rendered markup/data rather
than owning a second client-side translation catalogue.

The accepted bilingual product glossary retains `Board`, `tile`/`tiles`,
`drop`/`drops`, `draft`, `Live`, `OSRS`, `Old School RuneScape`, `EHB`, `DEHB`,
`Discord`, `Wise Old Man`, `MVP`, `DK Legacy`, and `OSRS Community Bingo` in
English. `WOM` is the only approved abbreviation for Wise Old Man; user-visible
`WoM` is incorrect. Capitalized `Admin` names the product role and UI title;
Danish `administrator` is used only for a person in ordinary prose. Danish
sentence structure may inflect surrounding words but must not translate
`Board` to `plade` or `bingoplade`.

The AI-generated landing reference has no recovered font metadata. Its display
face must be implemented with a bounded, rendered candidate comparison rather
than identified by assumption: current Barlow Condensed, Bebas Neue, and at most
one genuinely closer license-safe condensed face using the exact headline,
event-title, and numeral specimen at the same crop/scale. Compare silhouette,
cap height, width, stroke density, punctuation, numeral shapes, and legibility.
If no candidate is clearly closer, stop with a compact A/B/C specimen for user
choice. The selected implementation approximation owns hero display, section
headings, event names, feature numbers, and event-date numerals; Barlow Condensed
SemiBold owns utility/navigation/status; and Geist owns body/control copy only
while it continues to match. Bundle only selected faces and licenses. Browser
computed styles must resolve them; fallbacks cannot satisfy visual acceptance.
Do not transform a substitute face to imitate the approved proportions.

The current bounded landing comparison selected Bebas Neue Regular over the
locally available Barlow candidate as the implementation approximation; no third
local candidate was available. Treat that name as implementation provenance,
never as recovered reference metadata or page approval.

The user subsequently authorized one typography-only A/B against the hierarchy
observed at `https://outbid.website`: current Bebas Neue 400 versus a real
Barlow Condensed 800 display face at natural width. The experiment changes no
body face, utility role, color, spacing, layout, copy, responsive geometry, or
behavior. It must use an actual bundled 800 font file with its license rather
than browser-synthesized weight, and must not use scale transforms. User visual
choice remains the only adoption gate.

### Public reference-screenshot font hierarchy

Use this exact hierarchy when creating new Public UI visual references. Desktop
values assume the approved 1586×992 reference viewport and a 16px root. Preserve
the role relationships when responsive composition requires clamping.

| Reference role | Face / weight | Desktop size / line-height | Tracking / case | Current uses |
| --- | --- | --- | --- | --- |
| Display XXL | Barlow Condensed ExtraBold 800 | 96px / 0.92 | -0.015em, uppercase | Landing hero headline |
| Standard page display | Barlow Condensed ExtraBold 800 | `clamp(3.6rem, 5.2vw, 5.8rem)` / 0.82 (about 82.5px at 1586px) | 0.01em, uppercase | Default large headline for standard Public UI pages |
| Display XL | Barlow Condensed ExtraBold 800 | 29.6px / 1 | 0.045em, uppercase | Current/Previous section headings |
| Display L | Barlow Condensed ExtraBold 800 | 25.6px / 0.98 | 0.01em, uppercase | Current event names |
| Display M | Barlow Condensed ExtraBold 800 | 21.6px / 1 | 0.04em, uppercase | Feature headings |
| Display S | Barlow Condensed ExtraBold 800 | 20px / 1.1 | 0.01em, uppercase | Compact archive name/state |
| Major numeral | Barlow Condensed ExtraBold 800 | 36.8px / 1; 0.85 when stacked | -0.01em only for stacked dates | Feature 01/02/03 and event days |
| Utility lead | Barlow Condensed SemiBold 600 | 20px / 1 | 0.08em, uppercase | Kicker |
| Utility action | Barlow Condensed SemiBold 600 | 19.2px / 1 | 0.04–0.07em, uppercase | CTAs and event actions |
| Utility navigation | Barlow Condensed SemiBold 600 | 18.4px / 1 | 0.03–0.055em, uppercase | Header navigation/account |
| Utility status | Barlow Condensed SemiBold 600 | 17.6px / 1 | 0.04em, uppercase | Event state labels |
| Utility micro | Barlow Condensed SemiBold 600 | 12.8px / 1 | 0.04em, uppercase | Event months |
| Body lead | Geist Public UI 400 | about 20px / 1.25 | normal, sentence case | Hero supporting copy |
| Body standard | Geist Public UI 400 | 15.2px / 1.3 | normal, sentence case | Event details |
| Body compact | Geist Public UI 400 | 14.7px / 1.25 | normal, sentence case | Feature descriptions |

Do not use Bebas Neue, Instrument Sans, synthetic bold, `scaleX`, or manual
font stretching in new reference screenshots. Barlow 800 owns editorial display
and competition numerals; Barlow 600 owns navigation/status/action utility;
Geist 400 owns explanatory copy. Use size, weight, whitespace, and case—not a
fourth font—to create additional hierarchy.

Use the Login headline scale as the default for new standard Public UI pages.
Left-aligned standard Public UI pages default to the semantic
`--public-ui-standard-page-width` content column (`47rem`); boards, event
overviews, and other reference-owned compositions may use a focused width
exception when their approved visual contract requires it.
Page families whose composition genuinely requires another scale—such as a
board overview, Landing hero, or outcome-focused Confirmation masthead—may use
a focused page-family exception. Content length alone should recompose or wrap
within the standard scale before introducing an exception.

### Public UI foundation and catalogue

Every visible element introduced by the approved Public UI rebuild must trace
to a global public token/primitive in `site.public-ui.css` or a named page-family
composition there. Page-local markup may compose those primitives but may not
create a second visual system. `/Admin/PublicUi` remains an unlinked historical
specimen and is not an approval prerequisite for this replacement identity;
the Admin route itself is outside the rebuild and must not be changed.

The reusable public token owner remains `src/Bingo.Web/wwwroot/css/site.public-ui.css`,
but its existing generic semantic classes are legacy until a family explicitly
redesigns and accepts them. They are not mandatory markup for a rebuilt page.
The catalogue page uses
`public-ui-catalogue-*` only for its retained demonstration layout; rebuilt
product pages trace to the replacement semantic primitives and page-family
compositions, never to catalogue wrappers or demo markup. The route remains
unlinked and Admin-authorized, but it neither approves nor blocks product-page
work in this experiment.

The replacement light/dark canvas tokens and theme hook supersede the old
single charcoal page canvas. During bounded migration, scope new identity rules
to migrated families so global tokens do not prematurely change an unmigrated
Board or page family. Remaining public pages retain their matrix approval gates.

For the landing, new page-family class names must have one appearance owner and
must not inherit legacy surface/action/event-row geometry from the transitional
stylesheets. Replace the landing markup rather than composing it from the old
generic classes. Once the replacement is rendered and verified, remove only
selectors proven exclusive to the superseded landing; all Admin and later-family
rules remain untouched.

Landing-only static editorial copy is not a protected legacy behavior. Headline,
eyebrow, supporting copy, feature names/descriptions, section labels, and CTA or
link labels may be rewritten, shortened, reordered, or replaced when needed for
the approved composition. Copy must preserve truthful product meaning and the
existing destination/action semantics, use equivalent natural English and
Danish through the localization system, and must not change dynamic event facts,
account data, routes/handlers, standalone Login behavior or validated local
`ReturnUrl` handling, or backend behavior. The standalone Login route is the
only runtime login surface; do not add a login popup/dialog.

Landing rule and icon geometry are part of the visual contract. Feature
separators are vertically inset; current-event row rules meet the date-divider
region and may extend slightly before the divider where the approved target does;
the section-heading blueberry rule is the single strong separator and aligns to
the title's visual center; all remaining rules are consistently thin and
neutral. Feature icons share one view box, stroke weight, cap/join treatment,
alignment, and optical size. Landing major numerals share the same display face
and optical weight. Palette application must not use blending that washes out
the DK mark or the coral, sage, and bronze state accents, and inactive navigation
items never inherit an accent color.

For the accepted landing direction, the target's feature icons define a larger,
firmer 32×32 outlined family than the current render. Feature 01 is a full coral
broadcast mark; feature 02 is a full ink sheet with restrained content lines and
a distinct sage approval badge; feature 03 is a fuller bronze four-bar chart
with baseline. All three share comparable rendered optical size, stroke
presence, alignment, caps, and joins without path collisions. Landing
rules have exactly three roles: strong blueberry section rules, neutral 1px
vertically inset structural dividers, and neutral 1px event-row hairlines. The
date-gutter width, centered day/month stack, shorter vertically inset divider,
and row-hairline start near/slightly before that divider share one geometry so
no independent offset can drift. The last current-event row has no bottom
hairline. Current-event names use Barlow 800 at a restrained target-matching
size; Current events and Previous events headings use a slightly smaller Barlow
800 scale while retaining their rule alignment. Previous events use a separate compact archive row—name/state left and
history action right—with no current-ledger date/status/details columns or
terminal hairline.

The final icon target is the user crop
`codex-clipboard-6b6ef49d-3d5d-4675-a26c-7fe09eb622de.png`, compared with the
current crop `codex-clipboard-bf7d3ea9-f480-4c45-889d-ad40f3ca8b30.png`.
Implement the target geometry literally within the existing 32×32 family:
broadcast dot/arcs, folded sheet/content lines with separate circular check, and
four outlined rising bars with baseline. Every stroked coordinate must retain
enough viewBox padding for its full stroke, cap, and join; `overflow: visible`
is not a substitute for geometry that fits.
Event-row status/dot and action label/arrow share one explicit semantic row tone:
coral live, sage signup, bronze postponed/upcoming, and subdued bronze archived.
The mapping is semantic, never positional, and its hover/focus contrast must pass
in both themes.

## Existing application primitive registry — migration sources

Each entry names the semantic role, exact owner, canonical rendered reference,
current uses, permitted variants, and forbidden legacy residue.

| Role | Exact owner and reference | Uses and permitted variants | Forbidden residue |
| --- | --- | --- | --- |
| Create/Add | CSS `.admin-button-create` in the transitional stylesheet set; Events directory markup is the canonical reference | Events, Accounts emergency credential, Catalogue Add, and page-approved Add actions; label and icon vary | Bootstrap/local border, background, outline, padding, or geometry layered over the class |
| Primary action | CSS `.admin-button-primary` in the transitional stylesheet set; Board and Events directory are canonical action references | Existing outline implementation; new Admin target uses reference primary variants | Duplicated focus or local overrides outside the approved shared design |
| Secondary action | CSS `.admin-button-secondary` and `.event-create-cancel` in the transitional stylesheet set; Board/Identity are references | Neutral cancel, edit, navigation, and independent save actions | Ad hoc neutral button geometry or a disabled-looking fake terminal action |
| Destructive action | CSS `.action-danger-outline` in the transitional stylesheet set; lifecycle controls in Manage and Board are references | Danger-outline delete/remove/reversal actions, normally behind confirmation | Red filled action, generic danger text without consequence, or empty danger panel |
| Ghost/low-priority | CSS `.event-overview-row-action` is the canonical quiet route action; other page-local quiet links remain patterns | Read-only Workspace/route links and low-priority navigation | Treating every text link as a global component or adding a pill/button shell |
| Fields | CSS `.admin-field` plus `.form-control`/`.form-select` Admin rules; Identity/Schedule are references | Text, select, textarea, file, validation, and read-only variants | Bootstrap defaults, page-local label rhythm, or unlabeled controls |
| Search/filter toolbar | CSS `.admin-events-directory-controls`/`.admin-events-toolbar`/`.admin-search-field`/`.admin-filter-select`; Events directory is canonical | Full-width data pages; Accounts, Catalogue, and Participants compose it locally | A second toolbar surface, filters hidden in unrelated cards, or rail behavior |
| Tables/responsive cards | Events directory `.admin-events-table-wrap`/`.event-table` and its `1100px` card rules; Accounts/Participants are named page-local references | Wide table, server empty state, and label/value card transition | Row separators/outlines, squeezed desktop table, or claiming Accounts markup is shared |
| State/lifecycle pills | `.admin-status-pill` and `.is-*` modifiers in the transitional stylesheet set; Manage/Events directory are references | Lifecycle, role, setup, cutoff, and readiness states; text accompanies color | Color-only meaning, invented modifier semantics, or page-local pill geometry |
| Headings/support | Shared Admin typography tokens; markup owners are each page heading/component heading; Manage is the hierarchy reference | Page, component, row label, and support roles | Promoting a page selector to global or duplicating headings inside nested surfaces |
| Rows/panels/callouts | `.event-overview-section`, `.event-overview-row`, `.event-confirmation-box`, and `.information-callout` CSS; Manage/Board references | Operational row, tonal panel, compact confirmation, and informational note | Unnamed nested boxes, legacy card wrappers, or decorative dividers without owner |
| Information rail | No shared markup owner; Manage `.event-overview-dates-panel` and Schedule `.schedule-event-information-rail` are canonical patterns | Detail/form only on wide desktop; page-local content may differ | Adding it to full-width/table pages or relocating it below constrained forms |
| Route dialogs | Questions uses `_AdminLayout.cshtml` + `signup-questions-overlay.js`; Participant uses the dialog section of `event-manage.js`; both use `admin-editor-guard.js` for dirty/pending/failure safeguards. Accounts/Catalogue retain their existing owners | Questions/Participant share the accepted modal policy at every width with fullscreen at <=900; explicit routes/reload/recovery remain. Other owners retain their current behavior until authorized rollout | A second page shell, opaque host surface, generalized overlay framework, or new route-dialog variant |
| Admin confirmations | ADM-02 establishes the shared confirmation owner using existing dialog/guard/toast components; current `event-confirmation-box` and inline owners are migration sources | One centered consequence-specific confirmation, dimmed backdrop, Cancel before semantic action; editor hands off and resumes preserved state | Product-action native confirms, inline/`details` confirmations, stacked dialogs, duplicate prompts, unsupported reason fields |
| Backdrop/focus/scroll/history | Admin route CSS plus `site.js`, `signup-questions-overlay.js`, `account-manage-dialog.js`, and `catalogue-admin.js`; public Board retains its evidence viewer and submission drawer but no team-board popup | Modal/drawer backdrop, body-scroll lock, trigger focus restore where applicable, route history, bounded overlay content scroll | A page-local duplicate modal policy, lost focus, background scrolling, or realtime interrupting an active submission/result |
| Toasts | `_TransientToast.cshtml`, `.app-toast` CSS, and `site.js` transient-toast layer | Success, warning, error, information; live region, dismiss button, timeout pause, reduced motion | Inline duplicate toast systems, non-announced mutation feedback, or toast-only authorization/validation |
| Removable-object X | `.action-remove-x` for self-evident removable objects, using the standard inline two-stroke crossed-line SVG; shared close glyph path is `.admin-route-dialog-close` | Muted neutral Board-style default and neutral hover treatment for banner/evidence/objective removal, with accessible label and focus state | Pink/red default, bare unlabeled font/text `×`, oversized invisible target without focus, or using X for non-removal actions |

The registry distinguishes CSS sharing from markup sharing. A class reused by
several pages is physically shared CSS; the surrounding composition remains a
page-local exception unless a partial/helper/module owns it.

## Existing application Create/Add rule — superseded as redesign target

`.admin-button-create` owns the approved outline-free Create/Add appearance.
It affects consumers of that class, not every action whose label says
“Create”, “Add”, or “New”. The page matrix and local action hierarchy decide
which actions use it. A page may use `.admin-button-primary` or
`.admin-button-secondary` for a Create/Add-labeled action when its role is
different.

The class is outline-free through a transparent background and transparent
border in its shared rule. No Bootstrap `.btn` rule or page-local border,
background, padding, radius, shadow, or geometry may layer over it. Adopting
the primitive includes removing conflicting local rules and wrappers.

## Responsive, accessibility, and progressive enhancement

- `max-width: 1100px`: full-width data tables use label/value cards; detail
  information rails are removed; no drawer activation is established here.
- `max-width: 900px`: the Admin sidebar becomes the route-backed drawer with
  scrim and focus handling in `_AdminLayout.cshtml`, `site.js`, and the
  transitional stylesheet set.
  Desktop Admin route dialogs become ordinary pages. Public team cards navigate
  to the ordinary TeamBoard page at every viewport; direct loads,
  reload/history, browser Back, and modified clicks use that same route. Public
  tile routes remain real destinations; the
  Captain Submit route is drawer transport plus a compatibility redirect, not a
  rendered submission destination or no-JavaScript acceptance surface.
- `max-width: 600px`: the narrow Admin shell and component transitions apply;
  content remains readable and may document-scroll when necessary.

### Admin action feedback — rollout rule accepted 2026-09-08

Every Admin pass checks correct and missing outcome feedback as part of its normal
scope. An action message identifies what happened (and the affected object where
useful), uses the correct success/information/warning/error severity, and gives a
usable next step on failure. Avoid vague success notices, missing feedback, duplicate
toasts and messages contradicted by the visible state. In particular, distinguish
save failure from a successful save followed by refresh failure. Messages remain
visible and announced while a modal is open, using the existing shared toast owner;
field validation and durable recovery actions remain beside the relevant inputs.
Native Admin dialogs may opt into the shared toast layer with `data-toast-host`,
preserving their own layout classes. Add team uses this opt-in; route dialogs
retain their existing shared toast behavior.
Apply this during each page's authorized pass; it does not require a separate
whole-site audit or imply unvisited pages already conform.

User clarification (2026-09-08): saying a page/popup's general appearance is good
approves its overall composition only. It does not approve every action-revealed
popup, confirmation, expanded section, font/typography, error or toast state.
Preserve that composition while checking these details against the shared system;
the implementer/planner may use a targeted render where a concrete visual
uncertainty warrants it. This does not add duplicate reviewer visual inspection.

### Admin popup and confirmation contract — target updated 2026-09-26

The approved Admin simplification contract supersedes earlier inline/page-specific
confirmation requirements. It does not revoke prior visual acceptance or claim
the new primitive/pages are implemented or manually accepted. ADM-02 establishes
one accessible centered shared confirmation; owning tickets migrate their actions.
Editor placement remains independent, and later visual redesign is out of scope.

Ordinary Admin popups inherit the shared Admin type family and size hierarchy:
popup title, smaller section headings, normal body/label/input/button sizes and
smaller muted help. Confirmation headings use emphasized body text; warning
copy must not dominate the item heading or inherit public/page-introduction sizing.
Use semantic shared Admin styling; do not invent a page-local typography scale.

Use one centered shared confirmation with a dimmed backdrop, meaningful focus
entry/return, Escape cancellation, keyboard support and responsive layout. Cancel
precedes the semantic action. Wording describes the actual consequence; use Delete
for permanent deletion and Remove for unlinking. An editor hands interaction to
the confirmation and resumes with values preserved; never stack active dialogs.
Preserve pending-request guards and submit-once behavior. No ordinary-save
confirmation. The only typed product confirmation is **Fetch WOM data now**;
reasons appear only where the owning action requires them. Schedule changes after
public reliance use one old → new consequence confirmation, except Draft-time-only
edits; manual Open/Close/Reopen has one consequence modal without a prepare/warning/
proposed-close acknowledgement ladder. Server readiness is always rechecked.
Approval of evidence and a private Board is one click; the owning contracts define
destructive/publication/reopen confirmation and reason requirements.

Unchanged editors dismiss without confirmation. Closing actual unsaved changes
requires the shared discard choice that preserves edits when cancelled. Close,
Cancel, Escape, Back and outside-click paths must not bypass this protection or
leave URL/focus/scroll state inconsistent. Native `beforeunload` protection is
allowed only for genuinely unsaved browser-level exit/tab-close/reload; clear it
after save/discard and avoid duplicate prompts. In-application confirmations use
the shared system. No autosave is introduced. While a save is pending, keep the editor present
until it settles; prevent duplicate submissions. Failures retain values and usable
localized retry controls. Replacing content after any action must not silently
discard edits in another form in the same editor.

Successful ordinary saves return to and update their originating context with one
result notice. Every popup must refresh all affected parent data before the user
returns, including table columns, rows, counts and action state; updating only a
summary is insufficient. Preserve the parent filters and scroll. Failed or
unchanged edits do not require a mutation refresh, and refresh failure must not
silently present stale data as current. Signup Questions is an explicit multi-question-editor exception:
saving keeps the editor open and updates its list and parent summary. For the user-approved 2026-09-08 follow-up, Participants opens this same modal
at every width; <=900 uses a full-screen dialog. Resizing never closes, reloads or
changes its URL, unsaved values or pending request. Deliberately loaded standalone
URLs remain available for direct navigation/recovery. This supersedes the prior
width-based standalone fallback for Signup Questions only. Long content must leave
closing reachable; focus is meaningful and contained while modal, background scroll
is locked, and close restores focus to the originating context. Direct routes and
reloads remain usable.

Signup Questions is manually accepted. The user-authorized 2026-09-08 Participant
editor pass now adopts the same modal/full-screen, dirty/pending/recovery and
typography rules with real shared ownership where appropriate. This does not claim
other Admin popups conform or authorize their rollout. Participant-specific authority
and lifecycle actions remain explicit and protected.

Accounts Create/Manage adopts this same shared guard, shared-confirmation and
all-width modal contract for the 2026-09-08 behavior rollout. Its existing compact
composition and account-specific action/secret disclosure semantics remain intact.
The general Accounts narrow standalone switch is superseded; deliberate standalone
routes still provide direct navigation and recovery. Independent rollout review is
source-only by user decision; changed client behavior receives one targeted browser
check and the user provides final visual acceptance.

Catalogue Add/Edit adopts the same all-width modal/shared guard/shared confirmation
and parent-freshness contract for the user-approved 2026-09-08 rollout. Preserve the
specialized activity/drop layout and duplicate-item decisions. The former typed-delete
requirement is superseded by the sole WOM typed-confirmation exception. Revealed
confirmations must scroll into view, with ordinary Admin body typography and no
second modal layer. Explicit standalone routes remain usable.

Add Participant adopts the same all-width modal/shared guard contract on 2026-09-08,
superseding its narrow-width route switch. Preserve its form composition and
existing direct/reload route. Successful creation returns to fresh Participants
context; confirmed creation with failed refresh blocks resubmission and offers
actual reload without unsaved-change prompts for the completed form.

### Route-backed popup lifecycle

For every route-backed popup, dialog, or drawer, the route or query marker is
authoritative for which popup is open. Direct load, reload, validation failure,
and successful actions that remain in context must preserve or return with that
marker; an intentional close may remove it. On enhanced desktop,
initialization idempotently upgrades the server-rendered route fallback to the
modal or drawer; for a native `<dialog>`, close/remove modeless server-rendered
open state before calling `showModal()`.

At constrained widths, the same route remains a usable normal inline or
standalone destination when enhancement is unavailable. Close button, Cancel, native
Escape/cancel, and browser Back use one close lifecycle that keeps URL/history,
focus restoration, backdrop, and scroll lock synchronized. Partial content
updates may reinitialize the popup, but must not duplicate listeners, modal
state, or history entries. Every form or action inside it must preserve its
route marker when the response keeps the popup open. Server authorization and
validation remain authoritative; enhancement must not replace the ordinary
route/form fallback.

Manual acceptance for a materially changed popup covers direct URL/open,
reload, successful action, validation failure, close button, Cancel when
applicable, Escape, Back, and constrained width. No separate no-JavaScript
acceptance case is required.

Whole-application regression explicitly covers both shared masthead popups:
notifications and account. Check light/dark desktop and constrained widths,
trigger/text visibility, viewport containment and stacking, keyboard traversal,
focus-visible treatment, outside-click and Escape closure, focus return, and
reopening without duplicate state. Notification selection must preserve the
read transition and reach the authorized stored destination; personal evidence
 personal submission/evidence notifications, including those received by current
 linked Captain/co-captain recipients, use `/Submissions/{id:guid}`. Relevant
 general submission navigation uses `/Submissions`; Admin review notifications
 remain `/Admin/Review/Details/{id}`.

- Protected board, team, tile, and draft routes remain real paths for direct
  navigation, reload/history, and failed enhancement. The shared submission
  drawer remains attached to the team-board tile flow; `/Captain/Submit` is
  only its transport/handler plus a compatibility redirect. `/Captain` and
  `/Captain/Submissions/{id:guid}` are compatibility redirects/aliases to the
  canonical `/Submissions` routes, never separate rendered implementations.
  Ordinary Admin
  filter and independent value-save enhancements may degrade to the existing
  form/navigation path; no separate no-JavaScript parity work is required.
- Every interactive control has an accessible name, visible `:focus-visible`
  treatment, and a logical source order. Dialog open focuses meaningful
  content.
- Long dialog content scrolls inside its content region.
- Evidence-image dialogs preserve their existing open/close and focus contract and
  provide labeled zoom-in, zoom-out, and reset controls plus panning while zoomed.
  Wheel/trackpad and practical touch gestures may enhance the same state; keyboard
  users can operate every required control. Transforms reset whenever the dialog
  closes or its image changes, never move controls off-screen, and preserve
  responsive containment, Escape/backdrop closure, themes, and reduced motion.
- `prefers-reduced-motion` disables or minimizes transitions and overlay
  animation. Toasts pause while hovered or focused and dismiss by Escape or
  their labeled button.
- SignalR and fetch updates are invalidations/notifications. They must not
  replace authoritative server state or interrupt an active submission or
  submission-result acknowledgement.

### Planned participant announcement countdown — approved 2026-09-12

For the drop-announcement contract in DELIVERY_PLAN, the container's thin coral top
border is a visual ten-second compaction countdown, full initially and draining to
empty. Focus/interaction resets it to full and holds it there; leaving focus starts
a fresh ten seconds rather than resuming the previous remainder. Preserve pointer
hover protection; the countdown runs only outside both hover and keyboard focus.
Internal focus moves do not release it. No focus stealing on arrival; touch and
keyboard interactions can keep it expanded. Reduced-motion treatment retains timer
semantics without requiring animated movement. The top countdown is separate from
green `Tile completed` status and from the server-saved two-minute expansion cooldown.
Accepted prototype geometry/motion stays protected; new product behaviour must not
copy its demo state or create a second global toast/dialog system.

## Replace, do not layer

Adopting a primitive means removing superseded markup, CSS, JS, aliases,
wrappers, dividers, nested surfaces, duplicate headings, and local variants
within the approved scope. The implementation must leave one named owner for
every visible surface, border, divider, shadow, pseudo-element, and nested box;
the page matrix records a deliberate exception.

Do not keep old UI underneath a new shared rule “for compatibility”. If a
Bootstrap/local class remains in markup, it must be behavior-only or have no
conflicting computed appearance. A new shared selector does not erase a
page-local override; the override must be removed or recorded as an exception.

## UI task and review contract

Use the role/model policy in `AGENTS.md`; this section owns visual-work procedure,
not a second model assignment. The source-only reviewer exception for approved
Admin popup behavior passes remains in `AGENTS.md` and the assigned pass plan.
The major visual-pass workflow below does not add a rendered reviewer or readiness
review to those behavior passes or to tiny corrections.

Every UI task states the exact page/result, reference markup, classes, states,
files, and non-goals before implementation.

Canonical screenshots may govern a coherent visual family rather than only one
route. Reuse a settings, signup, confirmation, board, or other approved reference
for sibling pages when the same composition, hierarchy, and interaction geometry
actually apply; document the mapping in `UI_PAGE_MATRIX.md`. A distinct structure
or unresolved hierarchy requires a new user-supplied picture reference before
implementation. Do not invent a composition merely because no route-specific
screenshot exists, and do not request redundant screenshots for siblings already
covered by a clear family reference.

Detailed implementer self-inspection is required for the first coherent
implementation of a page family, when the user explicitly requests another
inspection, or when a demonstrated visual uncertainty could materially change
the fix. It is not repeated automatically for every small remediation. A
surgical follow-up implements the named finding, runs focused technical checks,
and may capture a render when immediately inexpensive, but it does not iterate
on subjective self-review; the user performs the visual acceptance check. This
does not remove the page-family user approval gate.

User-supplied screenshots are the normal post-implementation visual evidence.
They may include browser chrome or slight viewport-size variance when the
application viewport remains identifiable. Historical reference pictures are targets only when the user explicitly
reactivates them for the current task. Otherwise use current supplied screenshots,
named findings and the existing implementation. The reviewer compares applicable
current evidence and reactivated references plus the scoped code diff; it does not require
the implementer to repeat route rendering or broad reference inspection for a
named small correction. Static editorial copy is not frozen unless the matrix
says otherwise: it may change to serve the approved hierarchy while preserving
meaning, truthful claims, localization, dynamic facts, and action semantics.

Major Admin visual passes without a canonical screenshot reference do not require
a screenshot-to-reference gate. Their independent review inspects the rendered
routes and scoped diff against the approved Admin shell, tokens, typography,
component owners, layout-family rules, responsive transitions, focus treatment,
and protected behavior. It must reject legacy or Public UI visual residue such
as literal blue outlines, Bootstrap-primary styling, or page-local themes; a
screenshot package alone cannot establish or replace that rule compliance.

- **Must use:** the applicable matrix family, exact shared owners, approved
  focus/feedback behavior, route fallbacks, and server-authoritative forms.
- **Must remove:** superseded visual residue in the bounded page scope,
  duplicate wrappers/headings, local variants that conflict with a primitive,
and unowned borders/dividers/shadows.

Visual review of a structural rewrite must use rendered evidence at the named
reference viewport. It compares silhouette/major regions, resolved typefaces and
type scale, header geometry, artwork/diagonal placement, repeated-strip rhythm,
row density/alignment, links/actions/icons, color, whitespace, and responsive
recomposition. A source-only, token-only, selector-only, or test-only verdict
cannot approve visual fidelity.
- **May remain:** a named page-local pattern or behavior-only compatibility
  class that the matrix explicitly permits and that does not conflict with
  shared appearance.

For each major unapproved UI page/pass, use this workflow:

1. Before first implementation, one independent read-only
   rendered-page readiness/residue review compares the relevant rendered states
   with this document, `UI_PAGE_MATRIX.md`, and the named canonical references.
   It produces a numbered implementation contract stating the exact residue to
   remove; exact shared owner, primitive, or reference to adopt; states, files,
   and selectors involved; protected behavior; explicit non-goals; and
   objective visual acceptance criteria. The review covers desktop,
   narrow/mobile, keyboard/focus, empty, error, permission, dialog, and toast
   states as applicable. No separate no-JavaScript state review is required.
2. The user resolves and approves genuine design decisions before
   implementation.
3. The assigned implementer implements the smallest complete change against that frozen
   checklist; “smallest” means the smallest complete implementation, not the
   smallest diff.
4. The user supplies current light/dark desktop and narrow/mobile screenshots as
   applicable. One independent post-implementation review checks every
   numbered item against those screenshots, the complete scoped diff, and the
   canonical reference, reporting each item as satisfied or unsatisfied with
   concrete evidence.
5. If needed, one fresh remediation addresses only named failures,
   followed by the user's focused confirmation rather than another broad review
   unless the composition materially changed.
6. User manual visual acceptance remains the page-specific approval gate, and
   the workflow stops before the next page family.

A tiny visual correction retains the shorter focused workflow and does not
require this full cycle; run only the focused verification needed to prove the
correction.

Explicit continuous multi-pass authorization may defer final manual acceptance;
it does not skip the required implementation, current evidence, review or named
remediation. Record cleared pages as awaiting manual approval. The later combined
walkthrough includes shared-shell/CSS regressions across those pages. This does
not authorize scope expansion, packaging, commits, deployment or bypassing a blocker.

## Luck display contract — active 2026-10-01

Stats keeps its existing Luck container and team/player comparison flow. The
default mode is **Luck %** on a fixed 0–100 scale; an in-container toggle selects
**KC difference**. Do not add a second container, boss selector or EHB mode. Both
modes use the same saved snapshot, timestamp and scope. Sort, pin and extreme
selection use the active mode's unrounded value, while display rounds to at most
one decimal. Signed KC values normalize rounded negative zero.

Luck uses the fixed percentile scale, so it has no plus signs, negative values or
zero-as-expected centre. KC difference is signed and uses a zero-centred scale.
Missing, unranked, estimated, zero-recorded, stale and numerically unavailable
states remain explicit rows without fabricated values. A neutral Last updated
label may accompany a retained snapshot; ordinary age is not shown as a routine
error. Provider failure and estimate wording remains meaningful without exposing
private diagnostics.

One localized explanation is shared by Stats and tile help: Luck compares approved
drops with modeled outcomes at the same recorded activity and retained rates;
higher percentages mean luckier outcomes and expectation is not forced to the
midpoint. KC mode explains the rate-equivalent balance and uses the short tooltip
“KC totals don’t account for differences in boss kill speed.” Tile projections
show one aggregate result plus each relevant boss/activity result, and contributor
rows use their matching activity result. Single-boss views do not repeat identical
aggregate and boss rows unnecessarily. Preserve reduced-motion, keyboard-focus,
responsive and EN/DA localization rules from this document.

## Protected baselines

The protected Admin baseline is the shell plus Event Create, Identity,
Schedule, Manage/Overview, Events directory, Participants with accepted
detail-dialog states, Catalogue, Accounts/Roles, and Board. Its page-specific
approval/status is recorded only in `UI_PAGE_MATRIX.md`; Questions/CSV,
Teams/Draft, evidence review, Finalize/Audit, and later surfaces remain
governed by that matrix.

The public `View bingo` and team-board states retain their manually accepted
business behavior from 2026-08-20 except for the user-approved 2026-08-24
regular-page navigation correction. Their old visual identity and legacy
composition are superseded. PUB-REF-02, PUB-REF-03, PUB-REF-04, PUB-REF-14, and
PUB-REF-15 are reactivated for the ordered structural Board rewrite; current
screenshots demonstrate the rejected reskin and are not targets. Ordinary
team-card clicks navigate to the rendered TeamBoard page at every viewport.
Tiles replace the sidebar by real URLs, captain submission attaches a drawer to
that sidebar, and success or failure stays in the drawer until acknowledged.
Direct loads, reload/history, browser Back, View all teams, adjacent-team
navigation, and tile routes remain real destinations. The masthead Submit drop action
resolves the authenticated actor's own team-board route and opens the same
drawer with an authoritative Tile selector; the server scopes offered tiles to
that actor's current event/team.
The canonical submission workspace remains a separate approval unit; legacy
Captain routes are compatibility aliases rather than a standalone workspace.
This Board approval does not claim whole-application production readiness.

### U1 Identity page lifecycle and uncertain departure

Identity is the first `[AdminDesign]` page. Its ES module exports asynchronous
`init(root, ui)` and `dispose()`; disposal aborts requests, unregisters its draft
and awaits closure of its layer before another page initialises. Saves and Current
reads use AdminFetch. A registered dirty draft may supply `confirmLeave()` to use
its page-specific uncertain-save warning; the shared guard still refuses pending
navigation and owns sidebar, switcher, breadcrumb and history transitions. Identity
uses Check again / Leave anyway while uncertain, otherwise the normal discard
confirmation. This binding changes no other page's guard or layout.

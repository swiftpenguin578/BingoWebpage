# UI

Owns the global visual and interaction rules for Admin and Public pages and the page approvals; it is the sole page-approval authority.

## 1. Authority and scope

- This document is the global UI authority: shared primitives, ownership, responsive behaviour, feedback and the task/review contract. No rule here approves a page by implication; page approval and status live only in §11.
- The approved pages as they are in the running app are the visual baseline (light/dark typography, spacing, surfaces, focus, motion, responsive tables, component hierarchy, filled Admin primary buttons). No design reference files exist; a composition seen elsewhere approves nothing.
- Admin and Public are separate visual systems with separate rules below.
- Selector presence and page-local CSS cannot override these rules.
- New pages follow the shared rules for components, density, responsive behaviour, accessibility, mutation feedback and fallbacks.

## 2. Tokens, typography, surfaces

The app's CSS is the authority for every value: `src/Bingo.Web/wwwroot/css/admin-design-tokens.css` for Admin, `src/Bingo.Web/wwwroot/css/site.public-ui.css` for Public. This section keeps only usage rules.

### Admin tokens

- Theme: `dk-theme` on the page root plus `theme-dark` for dark; both themes define the same semantic token names.
- Token families: surfaces (bg, sidebar, surface, surface-2, raised, field, track), borders (default/subtle/strong), text (text, text-2 secondary, text-3 muted), accent/focus, actions (primary, control-hover, danger), status success/warning/neutral bg/fg, state tokens (hover, pressed, row-hover, selected, segment-on, nav-current) and overlay tokens (scrim, shadows, toast). Teal is the only accent.
- Light `--dk-warning-fg` is amber: warning text keeps ≥ 4.5:1 on page, surfaces and warning-bg; indicator dots (pending work, unsaved changes) read amber, clearly distinct from danger red.
- Type: `--dk-font-sans` (Geist) and `--dk-font-mono` (Geist Mono); sizes by role via `--dk-fs-*` (caption up to page title); weights 400/500/550/600; `--dk-fs-stat`/`--dk-fs-stat-sm` for statistic values.
- Spacing `--dk-space-*` in 4px steps; control heights default/md/sm; radii xs…dialog plus pill. Layout sizes (sidebar 252px / 64px collapsed, top bar, page max, drawer, modal, menu) are tokens.
- Breakpoints 1260/1080/860 (and 640 for phone layouts) are literals in media queries; variables are not used in media queries.
- Layer order: sticky header < drawer < mobile nav < menu < modal < toast.
- Charts use only chart-1 (emphasised), chart-2 (lighter) and chart-muted (outside the tracked set): a one-hue ramp from the accent, monotone lightness, light end ≥ 2:1 on surface in both themes; no other chart hues.
- Motion: one ease-out curve; entrances ease out, exits ease in about 25–30% faster; durations per layer (drawer, modal, menu, toast, content, theme) and live-board reorder durations (shuffle, shuffle minimum, reorder, stagger) are tokens. `prefers-reduced-motion` makes motion effectively instant, slows the spinner and removes skeleton shimmer.
- Use tokens for every colour, size, radius, layer and duration. Add a token only when a value carries meaning or repeats, never for a one-off nudge.
- Phase badges/dots come from `AdminDesignPhasePresentation.For(EventState)`: Setup neutral grey (`tone-draft`), Signups open green, Signups closed blue, Live teal, Final review amber (warning-foreground dot), Finished violet, Archived and Cancelled outline grey. Dashboard history: Finalized done, Provisional warning, Imported · archived neutral. All badge text ≥ 4.5:1.
- Status colours are consistent everywhere for not started, in progress, pending, completed, rejected and reversed. OSRS boss/item PNGs supply game identity; the interface stays modern and readable, not an OSRS clone.
- Compact operational hierarchy: page context, component title, support text, label, value, low-priority explanation. Never promote a heading or support size from a single page selector.
- Surfaces are tonal/borderless where the primitive says so; a nested surface must express a distinct concern; do not box every field.

### Public tokens and type

- Stylesheet cascade: `site.transitional.foundation.css` (foundation/shell not yet assigned), then `site.public-ui.css` (the sole destination for reusable Public tokens and primitives), then `site.transitional.application.css` (remaining Public/page rules). Transitional files are mixed ownership; split their rules only after consumers are migrated and verified. `site.css` is a compatibility marker only and receives no rules.
- Every visible public element traces to a token, primitive or named page-family composition in `site.public-ui.css`; there is no second visual system. Existing generic semantic classes there are legacy until a family adopts them; product pages never use `public-ui-catalogue-*` wrappers or demo markup. `/Admin/PublicUi` is an unlinked historical specimen, not an approval prerequisite; its route stays unchanged.
- Identity: an editorial competition system with condensed display type, strong mastheads, open layouts, thin rules, compact activity strips, restrained geometry and outline-first controls. Participant surfaces may add authenticated controls without changing the visitor hierarchy.
- Palette roles: light is the default (cream canvas, blueberry structure/accent, ink text, coral/sage/bronze semantic accents); dark uses charcoal canvas, near-black header, cream text, warm-grey secondary, neutral dividers and smoky indigo sparingly. Dark changes tokens only: same geometry, outlined tile numbers, no layout shift on theme switch.
- Theme tokens keep semantic roles separate (shell bg, content accent, primary/muted text, rule, art field, coral/sage/bronze). The dark shell colour never feeds content accents; dark accents are smoky indigo (lighter variant for small text); large display numerals may use smoky indigo, ordinary rules and input borders may not. Every dark text/control state needs computed contrast verification.
- Fonts: Barlow Condensed 800 for display and numerals, Barlow Condensed 600 for utility text (region headings, labels, actions, navigation, status), Geist 400 for copy. No fourth font, no Bebas Neue or Instrument Sans, no synthetic bold, `scaleX` or manual stretching.
- Type roles: region headings and field/data labels are uppercase Barlow 600; body Geist 400 at 1rem with 48rem max line length; compact support/help Geist at .9rem; validation text .82rem in the danger token. Display sizes (Landing hero, standard page display, section headings, event names, feature headings, archive rows, numerals, utility sizes) are set in `site.public-ui.css`.
- Bundle only the selected font faces with their licences; browser computed styles must resolve them (fallbacks never satisfy acceptance); never transform a substitute face to imitate proportions.

## 3. Admin shell

- Owners: `_AdminDesignLayout.cshtml` (the only Admin layout), `_AdminDesignSidebar.cshtml`, `_AdminDesignTopbar.cshtml`, `admin-design-*` CSS and `admin-design-shell.js`. Layers open through `AdminUI.openLayer` with shared hosts in `_AdminDesignHosts.cshtml` (Accounts: `admin-accounts.js`; Catalogue: `admin-catalogue.js`). Do not create new route-dialog variants. Public toast markup is `_TransientToast.cshtml`, behaviour in `site.js`.
- Structure: app › sidebar (brand/account row, nav, event switcher) › main › top bar (breadcrumbs, theme and language switches, notification bell) › scroller › page. Above 860px the sidebar collapses to an icon strip; at ≤ 860px it is an off-canvas panel with scrim.
- Sidebar top row: the logo (the site masthead SVG in white, high-detail) links to the public front page `/` with a full load (no `data-shell-link`), beside the account button, which alone has the hover highlight. Collapsed, the logo opens the account popout. Accessible names "Go to the public site" and "name · role" (Danish present).
- The account button shows the signed-in public username, localized role and a chevron; the account-menu header shows name and "@handle · role". Account menu: Account settings, Change password, View public site, Sign out.
- Page header: the h1 title is focusable (`tabindex="-1"`) so route changes move focus to it, with a keyboard-only focus ring; then a summary line, head actions and a page banner. No blocker chip and no header description.
- No sidebar item counts. Event-specific nav links are hidden when no event is selected or remembered. The nav label is "Teams / Draft".
- Notification bell sits in the top bar with an unread-number badge announced to screen readers. Its panel: header "Notifications" with unread count; the newest unread personal notifications (dot, newest six) and unresolved Admin actions (no dot) merged newest first, at most 8 rows (title, detail, relative time; whole row is a menu item); empty state; footer "All notifications" and "Mark all as read" (personal only) via `/notifications`. The Admin actions overview opens that section of `/notifications`.
- Theme follows the OS initially; a manual choice is remembered per browser and applied before first paint. The EN/DA two-state switch sits beside the theme switch.
- At ≤ 860px the theme and language switches move into the hamburger navigation; the bell stays in the top bar; wider layouts, behaviour, guards and keyboard access are unchanged.
- Event switcher lists only the current Live/Final-review event and upcoming Draft/Signups-open/Signups-closed events, soonest start first; it excludes Cancelled, finished, Archived and Discarded events. A past current event still names the button but is not listed. SuperAdmins also see qualifying hidden events marked Hidden (opening Overview's limited view). Choosing another event keeps the current page. The list scrolls after about 8 rows; All events stays below it.
- Switcher "stage · when" text: "closes" + signup close date while signups are open; "starts" + event start before play; "ends" + event end in Live/Final review; omitted when unset; an unsupported stored timezone uses UTC; Finalized/Archived use "ended" + end date; Cancelled uses "on" + CancelledAt in the event timezone even when the planned end is in the future.
- Remembered event: a browser-session cookie holds only the last Admin event ID, revalidated against visibility/access on every render. Community pages keep its sidebar nav without the event breadcrumb; public navigation keeps it; selecting another event replaces it; sign-out or lost access clears it; past events stay selectable; discarded, deleted or inaccessible context is dropped.
- The event breadcrumb is plain text; on phones the middle breadcrumb truncates instead of wrapping the bar. Failed loads show an icon and title. Menus use the shell's placement and exit animation; collapse labels toggle. Pages may provide their own loading skeleton, with a generic fallback.
- In-app navigation: links to another registered Admin page use `data-shell-link` and shared navigation/skeletons; same-page query, sort and drawer links keep their in-page handlers. Shared navigation keeps sidebar DOM and collapse state, updates current/event-dependent links, closes clean layers before swapping and guards layers with unsaved input; fragment-only history stays native. Widening past 860px closes mobile navigation and releases content interaction.
- Loading or failed navigation already shows the destination event in switcher and breadcrumb; Back and cancelled guards restore the correct context.
- Language change (after the dirty guard) saves through the existing `/language` POST, then swaps the translated page and shell without a skeleton, keeping sidebar, scroll and focus, with a `--dk-dur-theme` text cross-fade (none under reduced motion) and no card entrance animation. If a swap is unavailable it falls back to a reload. "Keep editing" keeps the draft and the old language.

## 4. Components and interaction contracts

### Component ownership

- "Global component" means an exact implementation owner: atomic appearance → its stylesheet owner; repeated markup/behaviour → a shared partial, helper or JS module only if it physically renders/owns it; repeated page-local composition is a named canonical pattern, not a shared component.
- Assemble pages from the shared component classes; page CSS is layout only (e.g. a table's `--table-cols`, a page grid). Route overlay behaviour (focus, trap, close timing, toasts) through the shared Admin behaviour script.
- Before creating a component, check the shared set and extend an existing one with a modifier (`.btn-…`, `.badge-…`, `.is-…`). New reusable components may be added when needed, consistent with the Admin visual language; page-specific composition need not become generic.
- New components cover every applicable state (hover, pressed, keyboard focus, selected, disabled with reason where helpful, read-only, busy, invalid, empty, error) and are checked in both themes.

### Buttons, fields and status

- Buttons: secondary default plus primary, outline-danger, danger, quiet, quiet-danger and small size. Admin primary actions are filled; neutral and destructive variants keep distinct roles; use shared theme tokens, never page-local colour substitutions. Busy keeps the button and its colour and shows a spinner. Icon buttons have an open state. Text actions and link buttons change colour on hover with no underline (already accepted underlined actions are intentional exceptions). Row-level "Delete…" and "Turn off…" use quiet-danger.
- Unavailable buttons use `aria-disabled` and stay focusable (e.g. Save with nothing to save keeps focus after a successful save). A permission note explains why an action is not available to the signed-in admin instead of showing a disabled control. A disabled menu item shows a hint and a title explaining why.
- Primary content/actions align to the owning layout; final actions sit at the end of their row; low-priority navigation does not compete with the primary action.
- Fields: label (optional marker), input/textarea/select, invalid state with field error, field hint, field meta (hint plus character count with near-limit and over-limit states), search with a vertically centred clear button, read-only value list, quote, copy field (read-only value with Copy).
- Read-only values: shared `.ro-value` looks like the read-only date-picker box (control height, border/radius, surface-2 background, full-colour text, no hover change); multiline grows; `.is-empty` is muted. Every read-only box shows the lock glyph in the calendar-button slot with reserved right padding. A field-lock note says why a field is locked, without a duplicate lock glyph; "A code is set" keeps its list lock.
- Search fields avoid password-manager autofill: `autocomplete="off"`, `data-1p-ignore`, `data-lpignore="true"`, `data-bwignore`, `data-form-type="other"`, and no id/name containing "user". Labels avoid username wording (Accounts "Search accounts"; Audit "Actor"/"Filter by actor" with hint "Matches actors whose name contains what you type…"; Danish avoids "brugernavn").
- Selection: tabs (with counts), segmented control (large, on, disabled, optional count), choices, check rows, searchable picker, searchable multi-select combobox with chips and listbox (`role="combobox"`, `aria-activedescendant`, check on chosen options, empty line). Page tabs are a real tablist (arrows, Home, End); a tab whose panel holds unsaved edits shows a dot, because switching tabs keeps them. A role-gated option (Hidden) is separated inside the same tabs radiogroup with a separator and icon. At ≤ 640px tabs scroll sideways instead of overflowing.
- Status: badges in success/warning/neutral/danger tones (each page maps its statuses to tones; danger for rejected/reversed), chips, pills, payment toggle (paid/busy), shared dirty indicator. Phase badges: accent for Live, outline for quiet past states (Archived, Cancelled). Status badges carry no dot; dots are reserved for indicators (attention, unsaved changes).
- Thumbnails use the shared `.thumb` contain rule. Images render via `background-image: var(--art)` so template placeholders are not fetched before rendering.

### Tables, menus and filters

- Tables: card › table wrapper that scrolls sideways only when columns do not fit; the page sets column template/min width; sticky header row; sortable headers expose `aria-sort`; selected and flash row states; numeric/action columns; footer pager; skeleton rows, empty and error-empty states.
- Sticky-first-column tables keep the first column in place while scrolling sideways, with an edge shadow when scrolled; names that are real links use link elements styled as name buttons.
- Attention cell: one priority item (failure icon or pending dot + text) with an optional "+N" hint listing the rest; a quiet dash when none; never a stack of badges; failure uses an icon, not only colour.
- History tables use two-line cells (time under date, record under action), a tags line and an actor cell with a gear icon for automated/system actions. A read-only history timeline shows what, when and reason.
- Menus: up-placement and closing states, danger items, separators and headings; placement end/start/side; arrow keys wrap, Home/End jump, Escape or Tab close and restore focus.
- Filter button opens a single-choice menu and shows the current value (active state), or opens a filter panel and shows its active-filter count. Filter panel: small anchored non-modal panel for less-used filters with presets, paired fields and Reset / Apply footer, with popover exit animation. A filter row lists every active filter as a removable chip with Clear all; a filter chip shows an applied filter not visible elsewhere and removes in one click. A summary button is a figure in the page summary that doubles as a filter toggle (`aria-pressed`, e.g. "4 need attention").

### Drawers, modals and confirmations

- Drawer: scrim + drawer (closing state) › head (eyebrow, title, badges) › body (banners, sections with title/aside, disclosure key-value lists, repeated-field add rows) › footer. A wide drawer (640px) serves editors that need more room.
- Admin editors open as the same overlay at every width (full width on narrow screens); resizing never closes, reloads or changes URL, values or a pending request. Long content scrolls inside the content region and keeps Close reachable. Deliberately loaded standalone URLs remain usable for direct navigation and recovery.
- Tile editing keeps board context: side drawer/inspector on wide screens, full page/sheet on small screens. A centred modal is only for short confirmations and destructive actions, never the tile editor.
- Modal: `role="alertdialog"` with title, body, extra and actions; the dialog container has `tabindex="-1"` so a click on its padding keeps focus (and Escape) inside.
- Form dialog: head/body/foot, fields with label row and character count (over-limit state), non-blocking field note, full-size select with chevron, disabled select. Confirmations keep the plain modal.
- One shared centred confirmation: dimmed backdrop, focus entry/return, Escape cancels, keyboard and responsive. Cancel precedes the semantic action. Wording states the consequence; Delete means permanent deletion, Remove means unlinking. Confirmations share a consequence list and an in-dialog banner component. Confirmations are transient (never shareable routes).
- Confirmation headings use emphasized body text; popups use the shared type hierarchy (title, smaller section headings, normal body/label/input/button, smaller muted help); warning copy never dominates; no page-local typography scale.
- A confirmation over an editor is allowed only with the editor inert, dimmed and scroll-locked, and focus returned afterwards. Stacked dialogs have one backdrop level: the top scrim is transparent but catches clicks, the layer underneath (drawer `is-behind` or form dialog) is dimmed and not interactive, only the top dialog is bright. Closing the top confirmation does not close the underlying drawer. An editor hands off to a confirmation and resumes with values preserved; never two active dialogs side by side.
- Before/after review confirmation: wide modal with compare table (label, old, new, off); at phone width rows stack with inline "Now / After" labels; the dialog scrolls when taller than the screen.
- No confirmation for ordinary saves. The only typed product confirmation is "Fetch WOM data now". Reasons only where the owning action requires them.
- Native Admin dialogs may opt into the shared toast layer with `data-toast-host`, keeping their layout classes (Add team uses it).

### Close, unsaved changes and save lifecycle

- Escape or backdrop click acts on the top layer only. A clean layer closes; a layer with unsaved input asks to discard in a nested dialog whose default focus is "Keep editing" (Cancel keeps edits); a pending save refuses dismissal; confirmations never close on outside click. Anything that can lose progress is a modal that does not close silently.
- Close, Cancel, Escape, Back and outside click share one close lifecycle and cannot bypass the discard choice or desync URL, history, focus, backdrop or scroll lock. Closing Create removes `create=1` by URL replace only.
- Unchanged editors dismiss without confirmation. "Keep editing" preserves draft and language and returns focus to the last edited control, closing mobile navigation first to release its inert state.
- `beforeunload` only for a genuine browser-level exit with real unsaved changes, cleared after save/discard, no duplicate prompts. No autosave.
- A pending save keeps the editor until settled with no duplicate submits. Failures keep the values with a localized retry. Replacing content never silently discards edits in another form of the same editor.
- Successful saves return to or update the originating context with one result notice. Every popup refreshes all affected parent data (columns, rows, counts, action state), preserving filters and scroll; failed or unchanged edits need no refresh; a refresh failure must not present stale data as current.

### Route-backed popups

- The route/query marker is authoritative for which popup is open; direct load, reload, validation failure and in-context success preserve or return it; intentional close may remove it. Forms keep the marker while staying open.
- Without enhancement the route remains a usable inline/standalone page; enhancement upgrades the server-rendered fallback idempotently; partial updates never duplicate listeners, state or history. A native dialog clears modeless open state before `showModal()`. Server authorization and validation stay authoritative.

### Busy, loading and skeletons

- Every Admin save/action uses shared `AdminUI.busy` with a 600 ms presentation minimum, 250 ms for one-click live-draft actions (picks, Undo). Busy never delays the backend or shows success before the response; repeat submission is blocked while pending; reduced motion removes the busy wait. No toast Undo.
- Exit timing derives from the computed shared CSS animation and completes even when there is no animation.
- Shared loading timing: navigation and in-page updates keep current content for 150 ms, then show a reversible skeleton for at least 400 ms; fast updates replace atomically. Full navigation makes its region inert; in-page query controls stay interactive and keep input. Thresholds are named settings.
- No load fades: loaded pages, cards and results swap without fade-in (empty states, notices and unsupported-timezone notes included). Menus, modals, drawers/scrims, new-row flash, sort swaps and inline errors keep their interaction animations.
- Every Admin loading header reserves two summary lines at ≤ 640px and one above. Data summaries stay empty while loading; count summaries keep their fixed words with number-sized placeholders (e.g. "[bar] live", "[bar] upcoming or in setup"); attention content appears only after data. The loaded header keeps its real height; content shifts only by the difference.
- Skeleton text bars sit in typography-sized one-line rows; non-text blocks keep their geometry; skeletons never use remembered data.

### Date/time picker

- One field (date, separator, time, calendar button; open/invalid/disabled/read-only states) with its own popup replacing the browser `datetime-local` picker: month grid plus hour/minute lists, footer Today, optional Clear, Done. Time lists stack below the calendar under 440px. Days show outside-month subdued, today dot, selected accent fill and disabled; hover and keyboard focus look distinct.
- Value is a local wall time `YYYY-MM-DDTHH:mm` in the event timezone; the page owns the value, the picker only UI state. Options: min disables earlier days; clearable adds Clear; a default time is used when a day is picked first; today is marked in the event timezone.
- Typing accepts dates like "27 Jun 2027", "27/6/2027", "2027-06-27" and times "18:30", "18.30", "1830". Valid entries normalise on blur; invalid entries stay as typed and show their error on leaving the field, count as an error and an unsaved change, while the stored value stays the last valid one.
- Keyboard: arrows by day/week, Page Up/Down by month (Shift: year), Home/End week ends, Enter picks the day and moves to the hour list; hour/minute lists Up/Down/Home/End; minutes in five-minute steps; Tab stays in the popup; Escape or Done closes and returns focus to the calendar button; Escape is handled before any surrounding dialog; a pointer press outside closes.
- The popup is fixed-positioned below (or above) the field, kept on screen, follows scroll/resize; its footer stays reachable in short viewports.
- Local→UTC conversion reports invalid (skipped by a clock change) or ambiguous (occurs twice) times so pages can explain them; UTC→local and today-in-zone conversions are shared and DST-safe.

### Focus and keyboard

- Opening an overlay moves focus into meaningful content without scrolling. Focus rings show only for keyboard use, never as an outline around the container.
- Focus is trapped in drawers, modals and mobile nav and returns to the opening control (stable fallback). Tab/Shift+Tab from the dialog container wraps to the first/last control; with no enabled controls (e.g. while saving) focus stays on the container. Background scroll is locked while a drawer, modal or mobile nav is open.

### Toasts

- Admin toasts: at most 3 visible; success toasts close after 4.5 s, toasts with an action after 7 s; finite lifetime, animated exit, no hover pause. Variants: error, action button, close button. On narrow screens toasts sit above the sticky save bar and never cover Save.
- Public toasts pause while hovered/focused and dismiss by Escape or a labelled button.

### Other shared components

- Page form card (max 760px) with a banner area for save outcomes and stale notices, sections with title/sub, a field grid (narrow fields for short values such as timezone). A sticky form action bar at the bottom of long forms shows the dirty indicator or a saved note, then the primary action. A page with several separately saved cards gives each card its own inline (non-sticky) save bar.
- Field list rows (one per form field: label, answer type and status, then choices or help text; number only on reorderable rows; off and flash states; inline rename; empty section with add button); below 640px row actions move under the text. Option-list editor (numbered rows with input and remove) and a locked read-only option list.
- Data panels (head, title, aside, foot, note). The stat strip (label/value/note, empty state, compact variant) is one surface with hairline dividers, not a grid of cards. A stat delta (arrow + bold figure + muted text, e.g. "+38 new in the latest event") shows what the latest event added to a cumulative total, in accent colour (not success green), with no percentages; omit it where the plain note says more; definitions stay in the hint.
- Hint tooltip on hover or keyboard focus for "why is this missing" and coverage notes; script places it against the viewport (above, else below, kept on screen) so cards and scrolling tables never clip it; CSS placement is the no-script fallback.
- Charts: legend and series fills (s-1, s-2, muted) plus a provisional hatch; bar chart with gridlines/ticks where each bar column is a button with total and stacked segments; tiny x labels at phone width; the chart tooltip leads with values, one key per line, beside the bar.
- Meter (partial fill shows an incomplete part in amber; fill is `display:block` so it works inside a span), facts list (secondary variant; rows space-4 apart measured from the end of each row's content, supporting text tight under its value, natural row heights), highlight (muted).
- Compact record list: each row is a button (`aria-expanded`) with thumbnail, name, sub, flags and a figure; one item opens at a time into an inline editor inside the item; inactive items dimmed; flags hide below 640px; square thumbnail from `--art` on a neutral tile.
- Change list (field, previous, new; empty and clamped values with show-more); below 440px of its own width it stacks into labelled Before / After lines.
- Code block shows only safe, already-sanitised text, scrolls past 200px, note variant for withheld/unreadable values; inline code for keys.
- Record subheader for a record opened from a list: back, title and status, Previous / position / Next pager.
- Image viewer: toolbar (zoom out/in with percentage, Fit and 100% as pressed buttons, Original) and a focusable stage with loading/failed/missing states; + and − zoom, 0 fits, 1 actual size, arrows pan; drag pans, wheel zooms.
- Reorder animation: measure before the order-changing state commits, play after; items matched by id travel old→new with a small lift, staggered by DOM order using the reorder tokens. It only presents an order the page already has and never chooses one; reduced motion skips it.

### Public controls

- Inputs/textareas/selects: square, full width, min height 2.55rem, padding .5rem .65rem, Geist 400 1rem/1.2, field background, 1px neutral border; icons/clear/chevrons only widen inline padding; focus is a 2px accent outline at 3px offset with no geometry change; dark may set `color-scheme: dark` without changing dimensions.
- Adjacent controls align by control box; an action beside a field matches 2.55rem and bottom-aligns; a separate action row starts 1.15rem after content with a 1.25rem gap and wraps instead of compressing.
- Actions: square accent outline, Barlow Condensed 600 uppercase, min height 2.5rem; hover may fill accent with contrast; destructive actions keep the geometry with the danger token; low-priority text actions have a min 1.8rem hit height, no padding/border and no button-sized whitespace in rows.

## 5. Layout families

### Admin

- Wide Admin tables stay horizontally scrollable; no forced table-to-card transition.
- Table pages keep their own table markup (no hidden generalized table component). A table's group heading/support text sit inside the table surface right above the column headings; the search/filter toolbar is its own surface above; no toolbar row-count badges unless a page requires one.
- Table record management uses a visible "Actions" heading and the record-action pattern; the removable-object X is not used for table records.
- Detail/form pages may use a sticky information rail on wide desktop; at constrained widths the rail is removed, not moved below or into the form. Full-width and table pages never get a rail.
- Admin Board is a workspace/canvas family (page-local workspace, canvas, sidebar, toolbar, tile dialogs, drag/swap states), not a table family, and keeps spatial context while editing; protected desktop viewport 1280×720.

### Public

- All `_Layout` pages use the `landing-shell-*` header as the one shared public header (no second live-header composition). It keeps the DK mark, dynamic navigation, notification/account/settings behaviour, routes, mobile menu, focus treatment and localization.
- Primary nav uses the same label-owned underline as the secondary row (under the text with padding, same thickness). In both rows inactive and selected labels share full-strength resting colour; hover fades inactive text only; selected stays full-strength and underlined; hover never moves geometry, adds background or shows an underline. Keep destinations, visibility, `aria-current`, focus and narrow horizontal access; no second nav framework.
- Sibling-view navigation (event or account context) is a content-level tab row directly below the masthead: condensed uppercase utility labels, transparent canvas, generous spacing, weighted accent underline on the current view; not a masthead extension.
- The public workspace is the Board "View bingo" state with public team/tile routes as fallbacks; its overlay/drawer behaviour is separate from Admin.
- Widths: `--public-page-gutter` is edge spacing only; Standard 54rem (forms, settings, password); Structured 64rem (onboarding, multi-column workflows); Wide 88rem (Signup, Confirmation, account overviews, inboxes, directories, Board overview). Board uses the normal gutter. Landing, Login and 403/404/405 keep their own widths; component and dialog widths are component-owned; page alignment is not normalized.
- Shell-to-masthead top gap: clamp(2.25rem, 5vw, 5rem) on desktop, 2rem at ≤ 680px; with the view-navigation row 1rem / .75rem. It is global; no page-local override restores ordinary padding. Board and post-publication Teams use the reduced gap.
- Full-bleed masthead artwork may reach the viewport edge but never widens the page-content container; the masthead-bottom divider belongs to the masthead, not the content wrapper or a full-width grid item.
- Ordinary masthead text stack (Privacy is the example): kicker Barlow 600 uppercase in accent blue (light) or cream (dark), never violet; title Barlow 800 uppercase at the standard page display size; support Geist 400 1rem/1.42, no margin, 48rem max; grid gap .65rem, no top padding, 1.35rem bottom when it owns the bottom edge. Page-specific facts, controls, artwork, metadata, rails and actions stay outside the text stack.
- The shared masthead text stack applies to Signup (create/edit), Signups, Teams, Board View Bingo/Recent Drops/Leaderboards and `/Submissions` + `/Submissions/{id}`. Landing, Signup Confirmation, TeamBoard, Tile, Login, Onboarding, AccessDenied/Error/StatusCode and HowTo are exceptions.
- The Login headline scale is the default for standard pages; Board overview, Landing hero and Confirmation masthead use focused exceptions; long content recomposes or wraps first.
- Content rhythm (defined by My Accounts and Settings): one type/spacing/field/focus/action system with two peer-section delimiter variants and a summary rail; a page picks one composition and never mixes peer variants or invents near-matches. A major region starts 2rem after the masthead or previous peer; heading → support about .45rem; content 1rem after the last intro line; next peer 2rem; no empty height to align columns. Section body rhythm .8rem; sibling fields .85rem apart (1rem on roomy desktop horizontal forms); labels .32rem above controls; values unchanged when columns stack.
- Numbered workflow (e.g. Settings): two-column row with an index track, 1px neutral bottom rule, Barlow 800 index; narrower track and index at ≤ 680px; no ruled heading on the same peer section.
- Ruled ledger (e.g. My Accounts): heading and a 2px rule share a flex row (.8rem gap), the label never shrinks, the rule fills the rest; no leading full-width rule; light: label and rule in content accent; dark: label in primary text, rule neutral; row separators 1px neutral.
- Summary rail (e.g. Signup): inset 1px neutral vertical rule on desktop; Barlow 600 heading with a short 3px underline under the text only; two-column label/value fact rows with 1px rules; values Geist 600; display numerals only for headline metrics; stacked: no vertical rule, 1px top rule and top padding instead.
- All compositions: light uses blue for structure and focus; dark uses cream headings, neutral rules/field borders and smoky indigo only for data emphasis. Page-local selectors may arrange columns and data but not redefine rhythm, control geometry, action hierarchy or theme mapping without a recorded exception.

## 6. Feedback and wording

- Every Admin action message says what happened (and the object where useful), uses the correct success/information/warning/error severity and gives a usable next step on failure. No vague success, missing feedback, duplicate toasts or messages contradicted by visible state. Distinguish save failure from save-then-refresh failure.
- Messages stay visible and announced while a modal is open, through the shared toast owner. Field validation and recovery actions stay beside the inputs.
- Feedback components: info/error/warning banners with a banner action, toast stack. The warning banner is used for an unknown outcome ("check before retrying").
- Pending actions disable their controls and keep the busy button's colour with a spinner. Failures keep entered data, show a banner or error toast with Retry and move focus to the retry control.
- Unknown write outcome: never say "not saved". "Check again" hitting session loss says the user is signed out, the app could not check whether the change went through, and to sign in and Check again; the draft is kept. Ordinary save-session loss keeps its separate not-saved notice. Both show localized field labels.

## 7. Responsive, accessibility, progressive enhancement

- Important status never relies on colour alone.
- Every interactive control has an accessible name, visible `:focus-visible` and logical source order.
- Motion is symmetric, uses the shared curve and durations, and respects `prefers-reduced-motion` (transitions and overlay animation disabled or minimized).
- Admin tables scroll sideways only when needed; Participants and Dashboard drop secondary columns at 1260/1080/860; the Events directory keeps every column and scrolls with a sticky name column.
- Desktop supports whole-board view, board editing and rapid review; mobile prioritizes submission, tile details and standings. A small-screen board may become a scrollable grid or tile list; the mobile overview is vertical/two-column previews that navigate to the full board without zoom animation.
- Public pages use stable page-family width/layout primitives and cover desktop, narrow/tablet, mobile, zoom/translation, keyboard/focus, empty, error and permission states.
- Admin filter and independent value-save enhancements may degrade to the form/navigation path; there is no separate no-JS parity work.

## 8. Language

- Every page reachable by anonymous users, Users or Captains supports English (default) and Danish through the language switch with all states and feedback localized; Admin/Super Admin-only pages may stay English-only. Every user-visible string (copy, validation, feedback, notifications, dialogs, empty/error states, toasts, confirmations, titles, labels, accessibility text) goes through the localization system. JavaScript gets localized values from server-rendered markup/data; no client-side catalogue.
- Glossary kept in English in both languages: Board, tile(s), drop(s), draft, Live, Leaderboards, OSRS, Old School RuneScape, EHB, DEHB, Discord, Wise Old Man, MVP, DK Legacy, OSRS Community Bingo. WOM is the only abbreviation (never WoM). Capitalized Admin is the role/title; Danish "administrator" only for a person in prose. Never translate Board as plade/bingoplade.
- Danish Admin terminology is event/events (Events, Alle events), not Bingoer. Scoped resource keys preserve page-specific translations, e.g. `AdminDesign.Pick {0}` (Danish "Valg {0}") because unscoped "Pick {0}" means "choose".
- Danish progression wording: `Nyt tile fremskridt: {0} / {1}`.
- Leaderboard tables: boss Gained/Start/End are Opnået/Start/Slut in Danish (expanded Teams and Players); nested EHB account tables use Gained/Start/End without an EHB prefix/suffix; Drop EHB and other default Players headings are unchanged; the rank heading is "Rank" in both languages.
- Decimal EHB input on Signup and My Accounts round-trips identically in English and Danish (invariant culture for stored/posted values).
- The submission workspace uses the neutral "Team workspace" terminology; Captain-only readiness language stays explicit.

## 9. Page-specific UI rules

### Admin Identity

- Unchanged Save is inert with `aria-disabled`, stays focusable with a "No changes to save" tooltip and no visible no-change status; reverting typed edits restores the clean status.
- The leave guard uses "Check again / Leave anyway" while a save outcome is uncertain, otherwise the normal discard confirmation.

### Admin Overview

- Loading header shows only the event name (no phase badge, empty reserved summary).
- Stage progression: done stages show actual moments ("Opened 10 May, 18:00"); upcoming show scheduled time ("· scheduled") or "Not scheduled"; a due-but-missed transition is overdue (dashed danger ring, danger text), never styled done; horizontal on wide screens, vertical list at ≤ 860px.
- Transition block shows a scheduled transition with its manual control, or a none state.
- Requirement checklist is neutral (requirements for the next transition, not failures) with optional links; items can be wait (a normal timed wait) or blocked (needs action elsewhere, link button); a later-note covers preparation not yet relevant.
- Issue rows: failure has an icon and danger title, pending an amber dot; one row per actual failure or pending item.
- Action groups list secondary/exceptional actions each with its consequence; recovery uses ordinary buttons, removal outline-danger buttons.
- Schedule pickers use the shared DST-safe conversion; picker footers stay reachable in short viewports.

### Admin Events directory

- Query changes update only results (rows, empty state, pager, chips, attention banners, counts) from the server; the chosen tab/Phase/sort/pager paint immediately and persist/retry on failure; header, tabs, toolbar, search and scroller keep their nodes; focus, caret, typed input and scroll are kept; superseded reads are aborted; the URL is replaced.
- Loading and failure keep query-bound tabs, toolbar and headings; loading uses same-size count slots and an empty reserved summary; failure clears counts/summary and shows Try again; every visit is fresh (no remembered counts).

### Admin Dashboard

- Loading header: shared empty-summary reservation plus a fresh card-sized placeholder (beside the title on desktop, full width below at ≤ 860px); movement comes only from the real summary/card size; failure shows no card or placeholder; never remembered data.
- Keyboard chart access, Escape dismisses tooltips, touch-accessible explanations, sort announcements, focus recovery on error/retry, both themes. Sort fades and tooltip motion are presentation only; the EHB column may drop first at narrow widths; the history table scrolls inside its surface.

### Admin Participants

- Filters, search, sort, pagination and table scroll are preserved around drawer use; pagination clamps after changes.
- Drawer save success updates the workspace and closes the drawer; failure keeps the draft with actionable errors; the unload guard applies only to dirty drafts.
- Add Participant success returns to a fresh Participants context and leaves no history entry that resubmits creation; confirmed creation with a failed refresh blocks resubmission and offers a real reload without unsaved-change prompts.
- The persistent-setting checkbox is a page-local durable-setting pattern, not a lifecycle acknowledgement or generic checkbox.

### Admin Signup setup and Schedule

- Timestamps use non-padded day, localized abbreviated month, comma and HH:mm (including reason notes, comparisons, derived cutoff and full first-response timestamp); confirmed counts read "N of M".
- Capacity card: input and waiting-list hint in the flexible left column beside a top-aligned 210px count box with 24px gap; narrow widths stack counts below; the capacity input keeps `.ss-num` sizing, only its column flexes.
- Keyboard/focus: tabs move with arrow keys; the question drawer opens on the question field, traps Tab, closes on Escape and returns focus to the opener; saving closes the drawer and focus goes to the row's Edit; moves keep focus on the move button (or the opposite one at the ends) and announce the new position; confirmations open on Cancel; after deleting, focus goes to the section's add button.

### Admin Catalogue

- Summary: separate count items with highlighted numbers ("68 active activities", "425 drops") plus a sentence; loading shows fixed words with number-sized bars and no row gap; tab counts use the shared count placeholders; one line, two at phone width.
- Thumbnails show the whole sprite (`background-size: contain`, centred, 3px inset in 32px/44px boxes, track background and inset border). The loading header's Add activity is a usable link from the first frame.
- Editor confirmations scroll into view, use ordinary Admin body type and add no second modal layer; the specialized activity/drop layout and duplicate-item decisions stay.

### Admin Accounts

- Global role is always a pill: User `badge-neutral`, Admin `badge-outline`, Super Admin `badge-accent`. Status: Active plain text, Disabled `badge-danger`.
- Summary: separate count items with tabular numbers ("N accounts", "M disabled"), no scope sentence; loading shows fixed words with number-sized bars; one reserved line, two at phone width.
- A large avatar heads a person's drawer; a success pill marks a confirmed state; role changes reuse the change list for Now → After.
- One-time secret result (reset link) in a focusable panel beside the action: title, expiry, copy field, notes, Done; held only in memory while shown; an unconfirmed (warning) state never recovers or silently regenerates the secret.

### Admin Board and Preview

- Board keeps workspace/canvas, toolbar, tile grid, sidebar statistics, route-backed tile dialogs, collaboration state, preview route and lifecycle actions. Board-local panel boundaries are owned by board CSS/markup; no generic Admin row divider or pseudo-element adds a second boundary. Rearrangement is only via the drag handle and drop-target swap.
- Grid: each row ends with a line total and a last row holds column totals; a line 25% or more from the average is marked high/low with an arrow and percentage; partial lines are marked.
- Tiles (art, name, EHB, flags) or empty cells (read-only variant); issue and changed states; source/target states while moving; tiles under 132px wide drop artwork and pills; a move bar explains keyboard moving.
- Planning figures: headline total and rows with an optional inline number field, saving spinner and saved tick.
- Tile editor: objective cards (single-drop chrome, collapsed, locked), drop groups with counts and drop rows (on, locked, missing rate, weight), a "Collect [n] …" count line with one-line summary, an EHB box where a replaced calculated value is struck through with a warning note and link, an artwork picker, automatic values read-only.
- At 390px the loaded header actions wrap to a second row (+42px), recorded as `headerGrowth: { 390: 42 }` in `scripts/lib/admin-page-conformance-pages.cjs`.
- The Board Preview modal renders the board like the team board (board only, not interactive; tile look in `src/Bingo.Web/wwwroot/css/admin-board-preview.css`). The legacy `/Admin/Events/{id}/Preview/…` route redirects to Board.

### Admin Teams / Draft

- Loading: family skeleton (readiness card, Teams bar, three team cards, text-line heights), empty summary, fresh every visit; one-step swap without fade.
- Team cards show the team with its members as main content (warning, empty, new-member states, footer); one column below 640px.
- Running draft: the sidebar collapses automatically (toggle remembered until the draft ends); no page header, only a visually hidden h1. The live draft scales 1.0–1.5× with the window and fits without page scrolling or smaller text at the start, middle and end for 5 teams × 50 and 8 teams × 96 players at 1440×900 and 1920×1080; the player pool may scroll slightly inside its card for large drafts.
- Turn strip above the board: pick number and round, team picking now (name fades in on change), next team ("again" on a snake turn), message when there is no turn, Undo #n, control chip and draft menu. The control indicator shows who may act on the shared board with one quiet secondary text action, plus someone-else (amber dot) and no-one states.
- Board: one column per team in draw order; head shows draft position, name, size against final size ("4 / 12"), EHB total and turn highlight. Columns list only actual members (preassigned "Pre", Captains first with co-captain badge, then numbered picks), start compact and grow with no reserved height; pending pick and confirmed-new entrance/flash. Under 210px column width, size/EHB move under the name and member EHB hides; below 860px two columns.
- Available players: name (primary account) and EHB; sort by EHB or name; `/` focuses search, Enter drafts a single match, Escape clears; the grid is one tab stop with roving focus (arrows, Home, End); chips act on click with no selection state; the pool is inert while a request runs or picking is not allowed; empty line.
- Immediate actions: controls that could repeat the action are inert while it runs and the pending item shows an in-place spinner; a confirmed result updates the view from the server; a failure leaves the view untouched with an error toast; an uncertain outcome keeps the old view and blocks repeats until "Check again" reports what happened.
- Drawing the order: while the request runs, columns wait-shuffle in place, positions read "–" and the strip says "Shuffling the teams…", for at least the shuffle minimum duration; on confirmation each column travels once to its confirmed position (staggered, small lift), then positions and turn highlight appear. The animation never decides the order. Draw/Redraw/picks/Undo stay disabled until settled; a failed draw stops with the old order and an error toast; an uncertain draw keeps the old order behind Check again; a readback or another admin's live draw plays the same move; reduced motion switches at once.
- The pre-formed-roster CSV import is an advanced state inside the Draft workspace, not a separate page. The small positioned roster-removal confirmation is kept.

### Admin Review

- Pending-first queue order with filtering and filter-preserving Back; matching-height contained image; full-width rejection/Cancel flow; heading status badge; Approve is `#78B86A` outlined/text (unfilled); compact rejection-only mode. `/Evidence/{id}` is a file-download handler, not a page.
- Review facts show what to compare with the evidence (plain variant for long values, warn variant for a boundary to check), a code tag for a code to match, and a facts note for notes/feedback.
- Decision panel: actions, link, note, form, weight, and a success result after a decision; the Decision card has no box-shadow.
- Screenshot viewer (left, sticky while the side column scrolls): Fit, 100%, zoom out/in with level, Original (full image in a new tab); drag pans, wheel zooms at the pointer, double-click toggles fit/2×; focusable stage with +/− zoom, 0 fit, 1 actual size, arrows pan; loading, failed (Try again, Open original) and missing states.

### Admin Final review

- Rank marker for top places: 1st tinted; an exact shared tie is outlined and shown as "=3".
- Table values are muted when not applicable; a decider highlight marks the competitive input that separates a row from its neighbour, explained in the sub-line.

### Admin WOM and Audit

- WOM: Link and Replace are dialogs. The capability chip is accent when the website owns the WOM connection, plain otherwise. Status rows show label, good/bad state with sub-text and an optional action; one column below 760px; secrets show only their verification state.
- Audit filter actions remain reachable in short viewports.

### Public Landing

- Static editorial copy may be rewritten or reordered if it keeps truthful meaning, destination/action semantics, EN/DA, dynamic facts, routes and backend behaviour.
- The masthead reuses the Login logo SVG variants and the percentage-painted diagonal field, has the bottom divider and hides artwork when the hero stacks. Anonymous signup links go to `/Account/Login` with a validated `ReturnUrl`.
- Icons: a 32×32 outlined family sharing view box, stroke, caps/joins and optical size (01 coral broadcast, 02 ink sheet with sage approval badge, 03 bronze four-bar chart with baseline); strokes with caps/joins fit inside the viewBox (`overflow: visible` is no substitute).
- Rules have three roles: blueberry section rules (the single strong separator, aligned to the title's visual centre), 1px vertically inset feature dividers, 1px event-row hairlines that meet the shared date-gutter region; the last current-event row has no bottom hairline.
- Event names Barlow 800, restrained; section headings slightly smaller Barlow 800; numerals share face/weight. Previous events use a compact archive row (name/state left, history action right) without date/status/details columns or terminal hairline.
- Event-row status/dot and action label/arrow share one semantic tone: coral live, sage signup, bronze postponed/upcoming, subdued bronze archived; semantic, not positional; hover/focus contrast passes in both themes. No blending that washes out the DK mark or state accents; inactive nav never takes an accent colour.

### Public Signup, Signups and event overview

- The Signup route owns both create and edit states (no separate edit page): wide horizontal masthead with real capacity/waiting status, three compact ruled form rows, a persistent right summary rail and a bottom action row. Confirmation has distinct outcomes. Light/dark geometry parity.
- The Signups directory is reached by exact link/Discord link and has no sibling navigation: counts, capacity/status summary, privacy-safe facts, phase-safe groups, empty states, narrow rows.
- Signup and Signups give the first stacked schedule block the same left divider/inset as the others. Public event-overview cards keep a trailing vertical divider unless in the rightmost column.

### Public Teams

- Uses the sibling nav row only after the Board is published, with its own page-owned roster masthead (no Board masthead); before publication it is a standalone destination with the ordinary gap and no links into the Board family.
- Masthead: title/back plus Event starts/Event ends/Players metadata (Players = frozen published-roster count); diagonal DK artwork scaled from the actual masthead height, capped at Landing geometry. Three-column roster, no team images, role icons or inter-team divider grid; the team sublabel shows formation type only; two draft picks per row on large widths, one when constrained.

### Public Board family (Board, TeamBoard, Tile)

- The Board is the main focus, with an overview-and-zoom model: compact previews (team name, rank, completed tiles, lines, full-board status; tile labels need not be readable); selecting expands to the full team board with return and previous/next team navigation; the transition preserves spatial context and respects reduced motion.
- Board overview: event tabs and masthead in the shared Wide width with normal gutter, team-overview grid, Recent Activity footer, compact fact/action rail (countdown, leader, selected metric, actions).
- TeamBoard is a full page with summary header, statistics rail, dominant tile grid, View all teams and adjacent-team navigation; the team board fills the window when scrolled, with a wider sidebar, larger sidebar/tile-view text and three-column stats at 721–900px. "Playing as" sits in the header; the nav label is "Submissions".
- Tile routes replace the left rail; the tile sidebar keeps its Team overview action. Submission is an attached drawer; evidence is a modal viewer. Tile shows "Points: N" / "Point: N" at the old label size.
- Published Board/TeamBoard/tile routes get Boards/Drops/Leaderboards/Teams navigation. Dark mode is token-only with outlined tile numbers.
- Evidence-image dialogs: labelled zoom in/out/reset and panning while zoomed; wheel/trackpad/touch may enhance; all keyboard-operable; transforms reset on close or image change and never push controls off-screen; containment, Escape/backdrop close, themes and reduced motion kept.
- Tile sidebar section order: Eligible drops → KC & Luck → Approved submissions.
- Tile KC & Luck reuses the TeamBoard EHB/Drop EHB row markup: team summary Team total / Luck / KC, expanded contributor rows Luck / player / KC, with separately labelled KC values for multiple bosses/modes and group headings outside rows; no sign, KC suffix or extra Luck label; Luck below 50 coral, 50 and above green. Contributors show only known positive-KC rows per boss/mode; empty groups and disclosures are omitted. Keep existing type, separators, colours, rhythm and responsive sidebar; initial and enhanced nested tile views share it; the standalone Tile scaffold stays inactive.
- Cancelled Board/Teams/team/tile views reuse the 404 status-editorial structure, typography and return CTA through one shared partial (`_EventCancelled.cshtml`): no decorative mark or divider, horizontally centred copy.

### Public Leaderboards

- Boss KC metric selector reuses the existing tables and the masthead dropdown: trigger "METRIC: [name]" + chevron styled like tabs (light: ink, blue when open/hovered/keyboard-focused); options without prefix. It sits in the same row above the divider, right-aligned (immediately before expanded standings, at the divider end when collapsed); only when width actually requires it does it wrap, with Metric above the tabs and both left-aligned.
- The menu closes on outside click, reclicking the trigger/selected name/chevron (without selecting, including after in-place replacement), Escape or selection; actual menu visibility (not just ARIA) is what counts. Metric switching happens in place without scrolling to top.
- Existing green signed gains and MVP values; no boss Drops arrow and no leaderboard freshness/status text; the Drops link is coral without arrow; every dark-mode leaderboard header is muted blue with visible hover/focus.
- WOM's -1 sentinel renders as an em dash like null in Teams details and Players; missing start/end values do not enter numeric aggregate sort keys. Boss team average gains display rounded whole numbers, keeping signed green gains, contributor-only averaging and full calculation/sort precision; EHB/Drop EHB formatting unchanged.
- EHB/Drop EHB tables differ only in MVP names/tie wording and individual values.

### Public Drops and drop announcement

- NEW row marker .81rem, layout-centred with the title (no manual offsets); the final NEW is plain coral text without background/outline; navigation NEW keeps its raised placement.
- "Clear all NEW": 1rem text action in the first time-group divider (not in filters), container right edge aligned with the separator left of Drop statistics, vertically centred with .55rem horizontal padding; opaque mask persists on interaction; light mode ink, muted on hover.
- Announcement banner is a shared non-modal expanded/compact banner on every non-Admin page. Its thin coral top border is a 10-second compaction countdown draining full → empty, independent of the green "Tile completed" status and of the server-saved two-minute expansion cooldown. Hover or focus inside resets and holds it full; leaving starts a fresh 10 s; moving between inner controls does not restart it; no focus stealing on arrival; touch/keyboard can keep it expanded; reduced motion keeps the timer semantics; no per-tick server writes; no second global toast/dialog system.
- Banner motion: staged 320 ms entrance, delayed animated dismissal for both shapes, measured 640 ms expanded/compact height transitions with 320 ms content phases, sequential directional slide exit/entrance.
- The expanded countdown replaces the static coral edge over a thin neutral top outline matching the other edges; banner header stacking level 1101. Surface, coral edge and all text/control/status colours render opaque and Board-matched in both themes on every non-Admin page (no Board-only variables).
- Controls are minimal: square switching controls, a styled, centred, non-wrapping counter in its original place (no duplicate by the kicker), centred inline-SVG chevrons; the X hover is a blue icon without background (kept exception). Real Drops anchor destinations and localization kept. The banner overlapping header navigation when dismissible is an accepted exception.
- Artwork fallback: item → tile → text-only, with no empty frame or icon and the same banner size/controls.

### Public Stats

- Stats renders the shared Board/Drops/Leaderboards masthead partial (`_EventMasthead.cshtml`: Event overview title, lifecycle countdown/status, result, player metric selector, conditional submission/WOM actions) with the same cached preparation; no Stats-specific fact/action set. Masthead and dashboard share a 1640px max, centred border-box shell with 3.6% side padding (5% at ≤ 850px); the Board's 88rem width does not constrain Stats.
- Layout: Drop value (~70%) beside Luck and Keeps on dropping (~30%); full-width compact Event timeline; Most versatile (~30%) beside Board progress (~70%). Barlow utility headings, Geist data, existing palettes, blue cards with quiet silhouette artwork. "Keeps on dropping" renders no artwork and reserves no space for empty/missing/broken images.
- Timestamps use 24-hour time in the event timezone. GP amounts that would show below one million in M use K; raw GP below one thousand and M/B above stay unchanged.
- Guidance (ⓘ) starts open unless the account's shared Hide tooltips setting is on, overlays content without a help row, dismisses with × or Escape and reopens from Info; section info icons match Luck; compact overlays keep card dimensions.
- Drop value: Teams opens scoped top-five player charts, all-contributor breakdown, totals and valuable drops, with All teams to return; Everyone keeps the aggregate view; totals and valuable drops stay within the selected team/event while previewing players. Global Players shows the top five plus one hover/focus/pinned comparison and an Others slice (lighter grey in light mode); player search exists only there (divider field on wide panels, popover on narrow; Escape closes; results capped at six; Show me highlights the viewer); selection keeps the pin and position. Longer lists scroll in the held chart area. Slice clicks highlight; keyboard focus follows the slice without a rectangle. The pinned contributor row keeps its background highlight without a left inset accent. Most valuable drops updates on scope switch; only artwork lifts on hover; no row change animation.
- Drop value sizing: the chart keeps its responsive height as minimum and grows with content; no reserved empty rows; the list scrolls beyond six; legend one/two lines with scrolling; GP BY TEAM fixed, donut/list centred; the donut total fits its hole; valuable drops move up when the upper row is shorter and stack when the GP card is narrower than 650px (actual card width).
- Luck keeps its single container: default mode Luck % on a fixed 0–100 scale with an in-container toggle to KC difference; no second container, boss selector or EHB mode. Both modes share snapshot, timestamp and scope; sort, pin and extremes use the unrounded active-mode value; display rounds to ≤ 1 decimal; signed KC normalizes rounded negative zero. Luck % has no plus signs, negatives or zero-centre; KC difference is signed on a zero-centred scale.
- Luck rows: missing, unranked, estimated, zero-recorded, stale and unavailable stay explicit rows without fabricated values; a neutral "Last updated" may accompany a retained snapshot (age is not an error); provider failure/estimate wording is meaningful without private diagnostics.
- Luck views: Teams shows all teams; a team opens its full roster with back navigation and a transitioning label, no header divider. Players shows the five unluckiest and five luckiest with a gap (ten or fewer shown once), scrolling inside the card; search popover by player or team; results highlight and scroll into view; an outsider adds one replaceable sticky comparison above the rankings (no tint, centred ×), which keeps its top gap. Bars and values grow from zero together (1000 ms initially, 420 ms on re-render; immediate under reduced motion).
- Luck sizing: reserves label space with proportional bars; reserves three rows, grows to six, then scrolls; short lists centre under the heading/axis; Keeps on dropping takes the remaining height.
- One localized Luck explanation is shared by Stats and tile help (approved drops versus modeled outcomes at the same activity and retained rates; higher = luckier; expectation not forced to the midpoint). KC mode tooltip: "KC totals don't account for differences in boss kill speed." Tile projections show one aggregate plus each relevant boss/activity, contributor rows use the matching activity, and single-boss views do not repeat identical rows.
- Board progress reserves four rows, grows to six, then scrolls; long names ellipsize with full-name access; Most versatile follows the bounded height; narrow charts scroll horizontally with a 680px minimum and the tooltip outside the scroller; legend symbols ink (light) / cream (dark) with muted labels.
- Event timeline: compact horizontal rail with a borderless right-aligned native selector with centred chevron and the hint to its right; on mobile filter and hint share one row. Milestones: solid reached dots, ring on the latest, hollow unreached, muted rail after; dots align with text; timestamp above, title and one detail line below, small inline submission icon; compact height with horizontal scroll and snap padding; narrow stops sized to headings (170px minimum), long labels on one line, last stop compact. The rail draws from the first point, team switches fade, order changes slide.
- Animation pacing: initial Drop value chart/donut 1100/1200 ms, Luck bars/counts 1000 ms, milestone rail 1400 ms; a shared 1200 ms time sweep reveals lines/markers and advances tile/row totals; interaction or reduced motion jumps to the final state.

### Public account pages

- My Accounts and My Events use broad account-overview compositions and do not inherit the 54rem standard column.
- My Accounts: equal-width Add fields, horizontal Saved EHB/Fetch composites, a single masthead divider, an input-aligned action band, reorder controls right after the editable fields, a short "registered for an event" label above the controls. The Add-character form is 2×2 at ≤ 1200px (Character + Personal Label; EHB/Fetch + Add Character) and one column at the mobile breakpoint. Unlink uses the localized native confirmation prompt (no checkbox) with server-side fail-closed confirmation.
- My Events: rows keep dividers between entries, but the last row of Current Events and History has no bottom divider or terminal bottom padding.
- Change Password dark mode: "Account security", "Current password" and "New password" use cream primary text; resting input borders are neutral.

## 10. UI task and review contract

- Page-specific is the default. A page rule becomes global only by explicit user decision or by correcting an already physically shared primitive. Propose any departure from the visual language before applying it. Replace wording in place; Git keeps history.
- Every UI task states the exact page/result, markup, classes, states, files and non-goals before implementation. A visual family may share one composition across sibling pages; a materially distinct new structure needs a user-supplied picture before implementation; never invent a composition.
- Keep existing accessibility, dirty/pending/conflict/uncertainty and security contracts. Public UI work protects behaviour (handlers, PageModels, routes, authorization, localization, data, interaction semantics), not legacy presentation; legacy composition plus a body class and overrides is not a migration.
- Replace, do not layer: adopting a primitive removes superseded markup, CSS, JS, aliases, wrappers, dividers, nested surfaces and duplicate headings. Every visible surface, border, divider, shadow and pseudo-element has one named owner (exceptions recorded). Remaining Bootstrap/local classes must be behaviour-only. A new shared selector does not erase a page-local override; remove or record it. May remain: a named page-local pattern or behaviour-only compatibility class that is explicitly permitted and does not conflict with shared appearance.
- Major unapproved page: first one independent read-only rendered readiness/residue review produces a numbered contract (residue to remove, owner to adopt, states/files/selectors, protected behaviour, non-goals, objective acceptance) covering desktop, narrow, keyboard, empty, error, permission, dialog and toast states. Then the user resolves design decisions; the implementer makes the smallest complete change; the user supplies light/dark desktop and narrow screenshots; one independent review checks every numbered item against screenshots and diff; at most one remediation of named failures plus the user's focused confirmation; user manual acceptance is the page gate; stop before the next page family.
- Major Admin visual passes are reviewed on rendered routes plus the scoped diff against shell, tokens, typography, component owners, layout families, responsive, focus and behaviour. Reject legacy/Public residue (blue outlines, Bootstrap-primary, page-local themes). Must use the approved family, exact shared owners, focus/feedback behaviour, route fallbacks and server-authoritative forms; must remove superseded residue, duplicate wrappers/headings, conflicting local variants and unowned borders/dividers/shadows.
- Visual review of a structural rewrite uses rendered evidence (silhouette, typefaces/scale, header geometry, artwork, rhythm, density, actions/icons, colour, whitespace, responsive); source, token, selector or test-only verdicts cannot approve visual fidelity, and screenshots alone do not establish compliance.
- User screenshots are the normal post-implementation evidence (browser chrome or slight viewport variance is fine); historical pictures are targets only when the user reactivates them; the reviewer compares current evidence plus scoped diff without a duplicate visual inspection. Static editorial copy is not frozen unless an approval says so (keep meaning, truth, localization, dynamic facts, action semantics).
- Detailed implementer self-inspection only for the first implementation of a page family, on user request, or for a material visual uncertainty; surgical follow-ups implement the finding and run focused checks without subjective iteration. A tiny visual correction uses the short focused workflow with only the verification needed to prove it. Admin popup behaviour passes and tiny corrections get no rendered readiness review. Role/model policy lives in ../AGENTS.md.
- Feedback is checked during each page's authorized pass; there is no separate whole-site audit and no implied conformance of unvisited pages.
- Approving a page or popup's general appearance approves only its overall composition, not every revealed popup, confirmation, expanded section, typography, error or toast state, and visual acceptance is not acceptance of binding. A targeted render is allowed for a concrete uncertainty.
- Manual acceptance of a materially changed popup covers direct URL, reload, success, validation failure, close button, Cancel, Escape, Back and constrained width; no no-JS case.
- Whole-app regression covers the notifications and account masthead popups: light/dark, desktop/constrained, visibility, containment/stacking, keyboard, focus-visible, outside-click/Escape close, focus return, reopen without duplicate state; selecting a notification keeps the read transition and reaches its authorized destination. No page approval substitutes for the whole-application regression gate.
- Continuous multi-pass authorization may defer manual acceptance but not implementation, evidence, review or remediation; cleared pages are recorded here as awaiting manual approval; a later combined walkthrough covers shared-shell/CSS regressions; it allows no scope expansion, packaging, commits, deployment or blocker bypass.

## 11. Page approvals

Every page has an explicit state in this table; a page or state without an approved/accepted row is not approved. Approval is page-specific and given only by the user after trying the page in the running preview; approving one page approves no other.

| Page | State | Date |
| --- | --- | --- |
| Admin shell (`/Admin/*`: sidebar, top bar, notification panel) | approved | 2026-10-08 |
| Admin shell behaviour (theme, language switch, event switcher, menus, dirty guard, in-page navigation) | accepted | 2026-10-06 |
| Admin Dashboard | accepted | 2026-10-07 |
| Admin Events directory | accepted | 2026-10-07 |
| Admin Event Create (`/Admin/Events/Create`) | retired; replaced by the Events directory Create dialog | 2026-10-08 |
| Admin Identity | accepted | 2026-10-07 |
| Admin Overview (Manage) | approved | 2026-10-07 |
| Admin Schedule | approved | 2026-10-07 |
| Admin Signup setup | approved | 2026-10-07 |
| Admin Signup questions (`/Admin/Events/Questions`) | retired; replaced by Signup setup | — |
| Admin Participants | approved | 2026-10-08 |
| Admin Participants: finalized-pre-Live departure, optional note, vacancy/replacement, follow-up states | awaiting manual acceptance | — |
| Admin Teams/Draft | approved | 2026-10-08 |
| Admin Teams/Draft live-draft scaling and pick highlight | approved in preview | 2026-10-10 |
| Admin Teams/Draft: finalized-pre-Live participant action and Captain-recovery publication states | awaiting manual acceptance | — |
| Admin Board | approved | 2026-10-08 |
| Admin Board Preview (board as players see it, boss artwork, default team-board artwork) | approved | 2026-10-10 |
| Admin evidence review (queue + Details) | approved | 2026-10-08 |
| Admin Final review (Finalize) | approved | 2026-10-08 |
| Admin WOM page | approved | 2026-10-08 |
| Admin WOM competitions on Overview (creation preview/validation, automatic link, management feedback, errors/recovery, before-Live deletion, event/team name inputs) | awaiting manual review | 2026-09-22 |
| Admin Catalogue | approved | 2026-10-07 |
| Admin Accounts | approved | 2026-10-07 |
| Admin Audit (readable sentences, Affected account column and drawer row, search while typing, 940 px table) | awaiting the user's visual acceptance | — |
| Discord Settings/link/login feedback and Admin Last login | manually accepted | 2026-09-14 |
| Public UI catalogue `/Admin/PublicUi` | historical specimen, direct link only, not an approval target | — |
| UI reference gallery `/Admin/UiReferences` | retired; redirects to `/Admin` | 2026-10-08 |
| Public Landing | approved | 2026-08-23 |
| Shared public primary/secondary navigation | approved | 2026-08-24 |
| Public header Current event / Captain-Submissions navigation | manually accepted | 2026-09-15 |
| Public Signup | approved | 2026-08-22 |
| Signup Confirmation | approved | 2026-08-22 |
| Stats / Signup / Signups / public event-overview scoped visual corrections | approved | 2026-09-16 |
| Public Signups directory | approved | 2026-08-24 |
| Public Teams/roster | approved; lifecycle-aware navigation addition authorized | 2026-09-06 |
| Public Teams: current roster, retained pick display, empty-team vacancy states | awaiting manual acceptance | — |
| Public Board family (Board, TeamBoard, Tile, Admin Preview) | approved; Teams navigation and evidence zoom/pan additions authorized | 2026-09-06 |
| Public Board Live-readiness changes (points label, team board layout, Playing as, Submissions label) | approved in preview | 2026-10-10 |
| Tile KC & Luck section | visually accepted | 2026-10-04 |
| Board Leaderboards boss KC metric (incl. em dash, whole-number gains) | manually approved | 2026-09-23 |
| Cancelled event views | approved | 2026-09-14 |
| Drops NEW markers and view-navigation underline | accepted | 2026-09-14 |
| Drops Clear all NEW | visually accepted | 2026-09-14 |
| Participant drop-announcement banner (non-Admin shell, Drops NEW state) | accepted | 2026-09-14 |
| Submission workspace (`/Submissions`, `/Submissions/{id}`) | approved | 2026-08-31 |
| Submission workspace private-correction active-publication behaviour | awaiting manual acceptance | — |
| Production Stats UI (layout, Drop value, Luck, milestones, Board progress, guidance/artwork) | accepted | 2026-09-16 |
| Stats masthead integration (shared event masthead, 1640px shell) | approved | 2026-09-23 |
| Stats Luck (Luck % and KC difference, teams and players) | approved | 2026-10-02 |
| Authentication and errors (Login, Onboarding, AccessDenied, Error, StatusCode) | approved | 2026-08-23 |
| Account Settings | approved | 2026-08-23 |
| My Accounts | approved | 2026-08-24 |
| My Events | approved | 2026-08-24 |
| Account security and recovery (Change Password, Forgot Password, Reset Password, Setup) | approved | 2026-08-26 |
| Notifications | approved | 2026-08-24 |
| Privacy | approved | 2026-08-24 |
| How To | approved | 2026-09-01 |
| Whole-application regression | complete | 2026-09-01 |

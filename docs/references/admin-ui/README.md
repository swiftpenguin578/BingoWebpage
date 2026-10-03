# DK Legacy Admin — design reference

This folder holds the **current, approved visual direction** for the admin UI
and the reusable UI system extracted from it. Where it disagrees with written
UI rules (for example the primary-button treatment in `AGENTS.md`), this design
wins until those documents are updated.

- Canonical design canvas "DK Legacy Admin" (edited from Claude):
  https://claude.ai/artifact/SDEM5UkPePuWPP8JHBEPRk
- The files here are copies of that canvas, refreshed after each design change.
  If they ever differ, the canvas is newer.
- Functional decisions live in [FUNCTIONALITY_CHANGES.md](FUNCTIONALITY_CHANGES.md),
  not here.

## What's here

| File | What it is | Canvas equivalent |
| --- | --- | --- |
| `ui/tokens.css` | Design tokens: colors (light + dark, same names), type, spacing, control sizes, radii, layout, layers, motion | `project/ui/tokens.css` |
| `ui/components.css` | Shared components and all their states, built only from tokens | `project/ui/components.css` |
| `ui/behavior.js` | Shared interaction behaviour (`window.DKAdmin`): focus, dismissal, keyboard, menu placement, toasts, exit timing | `project/ui/behavior.js` |
| `Participants.dc.html` | The Participants page. Consumes the three files above; keeps only page layout and participant logic | `Main.dc.html` ("Participants") |
| `Dashboard.dc.html` | The main admin Dashboard (community statistics). Consumes the three shared files; keeps only page layout and its sample data | `Dashboard.dc.html` |
| `Events.dc.html` | The Events directory and the Create event dialog, with a simulated browser for URL state and history. Consumes the three shared files | `Events.dc.html` |
| `Identity.dc.html` | The event Identity page: a page form for name, description, buy-in information and timezone, plus the permanent event link. Simulated browser as in Events | `Identity.dc.html` |
| `Overview.dc.html` | The event Overview: progression, what needs attention, the current stage with its scheduled transition, manual lifecycle controls and confirmations, requirements, public links, evidence codes and other actions. Independent phase samples | `Overview.dc.html` |
| `Schedule.dc.html` | The event Schedule page: signup, team-draft and event times with the shared date/time picker, editability by phase, published and Live confirmations, and the derived upload deadline. Independent event samples; simulated browser | `Schedule.dc.html` |
| `SignupSetup.dc.html` | The event Signup setup page: Settings (capacity, signup code) and Signup form (account fields, captain fields, custom questions) tabs, with a question drawer and counted confirmations. Independent event samples; simulated browser | `SignupSetup.dc.html` |
| `TeamsDraft.dc.html` | The event Teams / Draft page: team and captain setup with readiness, the admin-run snake draft (compact live board, player pool, control), manual rosters, finalization and pre-Live corrections. Independent event samples; simulated browser | `TeamsDraft.dc.html` ("Teams / Draft") |
| `Board.dc.html` | The event Board page: board workspace with line totals, planning figures, the tile editor drawer, moving and resizing, approval, publication, corrections and preview. Independent event samples; simulated browser | `Board.dc.html` |
| `Audit.dc.html` | The Audit page: read-only administrative history with filters (event, action, actor, record type, dates), newest-first pages of 25 and an entry drawer with changes, reasons and technical details. Simulated browser | `Audit.dc.html` |
| `Review.dc.html` | The event Review page: a searchable evidence queue and a submission workspace with the screenshot viewer, the facts to compare, warnings and the decision panel (approve, reject, correct, reverse). Independent event samples; simulated browser | `Review.dc.html` |
| `FinalReview.dc.html` | The event Final review page: publication readiness with the one publish action, provisional or official standings with what separates each team, publication history with retained versions, and exceptional reopening. Independent event samples; simulated browser | `FinalReview.dc.html` ("Final review") |
| `Wom.dc.html` | The event WOM page: the Wise Old Man connection (capability, data freshness, account coverage, Fetch now), updates to Wise Old Man and the management code, create or link when not connected, and occasional management. Independent event samples; simulated browser | `Wom.dc.html` ("WOM") |
| `Catalogue.dc.html` | The Catalogue page: a directory of bosses and activities, and an activity drawer with its settings, Wise Old Man metric, a compact drop list with one inline editor at a time (rate, value and item mapping, read-only rate mechanics), adding drops and availability. Simulated browser | `Catalogue.dc.html` |
| `Accounts.dc.html` | The Accounts page: a directory of website accounts (search, global role, pages of 25) and an account drawer with access, permitted actions (Admin access, reset link, disable or restore, ownership), characters, event roles and disable history, plus confirmations and ownership transfer. Simulated browser | `Accounts.dc.html` |
| `Components.dc.html` | Lightweight preview of the shared components, light and dark side by side, with live overlays | `Components.dc.html` |
| `support.js`, `vendor/` | Runtime that renders the `.dc.html` pages. **Not** part of the UI system | provided by the canvas |

## Previewing

- **Canvas:** open the link above. It launches on Participants; the canvas view
  shows the Dashboard, Events, Overview, Identity, Schedule, Signup setup,
  Teams / Draft, Board, Audit, Review, Final review, WOM, Catalogue, Accounts and Components artboards beside it.
- **Locally:** serve this folder (`python3 -m http.server`) and open
  `Participants.dc.html`, `Dashboard.dc.html`, `Events.dc.html`, `Identity.dc.html`,
  `Overview.dc.html`, `Schedule.dc.html`, `SignupSetup.dc.html`,
  `TeamsDraft.dc.html`, `Board.dc.html`, `Audit.dc.html`, `Review.dc.html`, `FinalReview.dc.html`, `Wom.dc.html`, `Catalogue.dc.html`, `Accounts.dc.html` or `Components.dc.html`. Opening over `file://` may be
  blocked. Fonts load from Google Fonts; without network the page falls back to
  system fonts.

## Tokens

Put `dk-theme` on the page root, plus `theme-dark` for dark. Both themes define
the same semantic names:

- **Surfaces:** `--dk-bg`, `--dk-bg-sidebar`, `--dk-surface`, `--dk-surface-2`,
  `--dk-surface-raised`, `--dk-field`, `--dk-track`.
- **Borders:** `--dk-border`, `--dk-border-subtle`, `--dk-border-strong`.
- **Text/icons:** `--dk-text`, `--dk-text-2` (secondary), `--dk-text-3` (muted).
- **Accent/focus:** `--dk-accent`, `--dk-accent-soft`, `--dk-accent-border`,
  `--dk-focus`, `--dk-focus-ring`. Teal is the only accent.
- **Actions:** `--dk-primary(-hover)`, `--dk-on-primary`, `--dk-control-hover`,
  `--dk-danger(-hover)`, `--dk-on-danger`, `--dk-danger-fg`, `--dk-danger-soft`.
- **Status:** `--dk-success-*`, `--dk-warning-*`, `--dk-neutral-*` (`-bg`/`-fg`).
  Light `--dk-warning-fg` is amber `#985a00`: warning text stays ≥ 4.5:1 on
  the page, surfaces and `--dk-warning-bg`, and indicator dots (pending work,
  unsaved changes) read as amber, clearly apart from danger red.
- **States:** `--dk-hover`, `--dk-pressed`, `--dk-row-hover`, `--dk-selected`,
  `--dk-segment-on`, `--dk-nav-current`.
- **Overlays:** `--dk-scrim`, `--dk-shadow-sm`, `--dk-shadow-lg`, `--dk-toast-*`.
- **Type:** `--dk-font-sans` (Geist), `--dk-font-mono`; sizes by role
  (`--dk-fs-caption` 11.5 → `--dk-fs-page-title` 23); weights 400/500/550/600.
- **Size and shape:** `--dk-space-1…8` (4px steps to 32), `--dk-control-h` 34 /
  `-md` 32 / `-sm` 28, radii `--dk-radius-xs…dialog`, `--dk-radius-pill`.
- **Layout:** sidebar 252 (64 collapsed), top bar 54, page max 1320, drawer 560,
  modal 440, menu 236. Breakpoints 1260 / 1080 / 860 are literals (CSS can't use
  variables in media queries).
- **Layers:** sticky header < drawer < mobile nav < menu < modal < toast.
- **Data display:** `--dk-chart-1` (emphasised part), `--dk-chart-2` (lighter
  part), `--dk-chart-muted` (data outside the tracked set). A one-hue ramp from
  the accent, validated in both themes (monotone lightness, light end ≥ 2:1 on
  the surface). No other chart hues. `--dk-fs-stat` / `--dk-fs-stat-sm` for
  statistic values.
- **Motion:** one ease-out curve; entrances ease out, exits ease in and are
  ~25–30% faster (drawer .22/.16s, modal .18/.14s, menu .12/.10s, toast .2/.16s,
  content .18/.14s). Reordering a live board uses `--dk-dur-shuffle` (one beat
  of the waiting shuffle), `--dk-dur-shuffle-min` (its shortest showing),
  `--dk-dur-reorder` and `--dk-dur-reorder-stagger` (items travelling to the
  confirmed order). `prefers-reduced-motion` makes motion effectively instant,
  slows the spinner and removes the skeleton shimmer.

## Components

All are CSS classes in `ui/components.css`; the markup to copy is in
`Components.dc.html` (generic) and `Participants.dc.html` (in context).

- **Shell:** `.app` › `.side` (brand/account button, `.nav` + `.nav-item`,
  `.event-switch`, collapse to icon strip ≥861px, off-canvas panel ≤860px with
  `.nav-scrim`) › `.main` › `.topbar` (`.crumbs`, `.theme-seg`) › `.scroller` ›
  `.page`.
- **Page header:** `.page-head`, `.h1` (focusable with `tabindex="-1"` for
  route changes; keyboard-only focus ring), `.summary`, `.head-actions`,
  `.lock-chip`, `.page-banner`.
- **Buttons:** `.btn` (secondary) with `.btn-primary`, `.btn-outline-danger`,
  `.btn-danger`, `.btn-quiet`, `.btn-quiet-danger`, size `.btn-sm`, state
  `.is-busy` (+ `.spin`). `.icon-btn` (`.is-open`). Text actions: `.crumb-btn`
  style (color change on hover, no underline).
- **Fields:** `.lbl` (+ `.opt`), `.input`, `.textarea`, `.select`,
  `.is-invalid` + `.field-err`, `.field-hint`, `.search` (+ `.has-clear`,
  `.clear`), `.note-meta` (character count), `.ro-list` (read-only values),
  `.quote`.
- **Selection:** `.tabs`/`.tab` (+ `.tab-count`), `.seg`/`.seg-opt` (`.seg-lg`,
  `.is-on`, `.is-disabled`), `.choices`/`.choice`, `.check-row`, searchable
  picker `.results`/`.res` + `.picked`.
- **Status:** `.badge` with `.badge-success` / `.badge-warning` /
  `.badge-neutral` (the page maps its statuses to tones), `.chip`, `.pill`,
  `.dot.tone-*`, payment toggle `.pay` (`.is-paid`, `.is-busy`), `.dirty`.
- **Table:** `.card` › `.tbl-wrap` (`.is-scroll` when columns don't fit) ›
  `.tbl` (page sets `--table-cols` and `--table-min`) › `.tr.th-row` (sticky,
  `.th-btn` sort with `aria-sort`) / `.tr.row` (`.is-selected`, `.is-flash`) ›
  `.td` (`.c-name`, `.num`, `.c-act`), `.name-btn`, `.sub`, `.flag`. Footer
  `.tfoot` + `.pager`/`.pg`. States: `.sk-row`/`.sk` skeleton, `.empty`
  (`.is-error`), view-change fade `.rows.swap-a/b`.
- **Menu:** `.catcher` + `.menu` (`.is-up`, `.is-closing`), `.menu-item`
  (`.is-danger`, disabled with `.menu-hint` and a `title` explaining why),
  `.menu-sep`, `.menu-head`.
- **Drawer:** `.scrim` + `.drawer` (`.is-closing`) › `.dr-head` (`.eyebrow`,
  `.dr-title`, `.dr-badges`) › `.dr-body` (`.dr-banners`, `.sec`, `.sec-head`,
  `.sec-title`, `.sec-aside`, disclosure `.disc-btn` + `.kv`, repeated fields
  `.add-row`) › `.dr-foot`.
- **Modal:** `.m-scrim` + `.m-wrap` › `.modal` (`role=alertdialog`) › `.m-title`,
  `.m-body`, `.m-extra`, `.m-actions`. Dialogs use `tabindex="-1"` on the
  container so a click on their padding keeps focus (and Escape) inside.
  Stacked dialogs (a confirmation over a form dialog): the top scrim gets
  `.is-stacked` (transparent, still catches clicks) and the dialog underneath
  gets `.is-behind` (dimmed by the same scrim, not interactive), so the page
  keeps one backdrop level and only the top dialog is bright.
- **Feedback:** `.banner.is-info` / `.is-error` (+ `.banner-btn`), `.toasts` ›
  `.toast` (`.is-error`, `.toast-act`, `.toast-x`).
- **Data display** (added with the Dashboard):
  - `.card.panel` with `.panel-head`, `.panel-title`, `.panel-aside`,
    `.panel-foot` and `.panel-note`
  - `.stat-strip` › `.stat` (`.stat-label`, `.stat-value` with `.is-empty`,
    `.stat-note`), with an `.is-compact` variant. This is one surface with
    hairline dividers, not a grid of cards.
  - `.stat-delta` inside `.stat-note`: trend arrow + bold figure + muted text
    ("+38 new in the latest event") for what the latest event added to a
    cumulative total. Accent, not success green (a total that only grows is
    not good/bad), and no percentages. Leave it out where the plain note says
    more (e.g. "1 awaiting final review"); definitions stay in the `.hint`.
  - `.hint` (+ `.hint-tip`, `.tip-start`/`.tip-end`): a short explanation on
    hover or keyboard focus, used for "why is this missing" and coverage notes.
    behavior.js places the open tip against the viewport (above, or below
    when there's no room, kept inside the screen), so cards and scrolling
    tables never clip it; the CSS placement remains the no-script fallback.
  - `.legend` and the series fills `.s-1` / `.s-2` / `.s-muted`, plus the
    `.is-provisional` hatch
  - `.chart` › `.chart-plot`, gridlines and ticks, `.bars` › `.bar-col`
    (a button) › `.bar` › `.bar-total` + `.bar-stack` › `.bar-seg`, with
    `.chart-x` labels (`.chart-xtiny` at phone width)
  - `.chart-tip`: values lead, line keys; placed beside the bar with
    `DKAdmin.tipBeside`
  - `.meter`, `.facts`/`.fact` (`.is-secondary`), `.highlight` (`.is-muted`).
    `.fact-sub` is optional. Rows are `--dk-space-4` apart, measured from the end
    of a row's full content, and the supporting text sits tight under its value
    (2px, tight line height). Rows keep their natural heights.
  - `.next-event`
  - `.pill.is-neutral` / `.pill.is-warning`
  - `.fade-in`
- **Directory and forms** (added with Events):
  - Phase badges: `.badge-accent` (Live) and `.badge-outline` (quiet past
    states: Archived, Cancelled), alongside the existing tones. Like
    Participants' status badges, they carry no dot; dots are reserved for
    indicators (attention, unsaved changes).
  - `.tab-sep` separates a role-gated option (Hidden) inside the same `.tabs`
    radiogroup; `.tab .ic` for its icon. At ≤640px `.tabs` scroll sideways
    instead of overflowing the page (this also fixes Participants' tabs at
    phone width).
  - `.filter-btn` (+ `.fb-lbl`, `.is-active`): opens a single-choice filter
    menu and shows the current value. `.filter-chip`: an applied filter that
    isn't visible elsewhere, removable in one click.
  - `.summary-btn` (`aria-pressed`): a figure in the page summary that doubles
    as a filter ("4 need attention").
  - `.attn` (`.is-failure` / `.is-pending`) › `.attn-main`
    (icon or dot + `.attn-text`) + optional `.attn-more` (a `.hint` listing
    the rest); `.attn-none` for the quiet dash. One priority item, never a
    stack of badges. Failure uses an icon, not only color.
  - `.tbl.sticky-first`: the first column stays in place while the table
    scrolls sideways; `.tbl-wrap.is-scrolled` adds the edge shadow.
    `a.name-btn` for names that are real links.
  - `.banner.is-warning` (outcome unknown; check before retrying), `.banner b`.
  - Form dialog: `.modal.modal-form` › `.mf-head` / `.mf-body` / `.mf-foot`,
    `.field`, `.lbl-row` + `.field-count` (`.is-over`), `.field-note`
    (non-blocking note), full-size `.select.select-field` in `.select-wrap`
    (chevron), `.select:disabled`. Confirmations keep the plain `.modal`.
- **Page forms** (added with Identity):
  - `.card.form-card` (max 760px) › `.form-banners` (save outcome and stale
    notices) › `.form-sec` (`.is-last`) with `.form-sec-head`,
    `.form-sec-title`, `.form-sec-sub` › `.form-fields` of `.field`
    (`.field-narrow` for short values such as a timezone).
  - `.form-bar`: the action bar, sticky at the bottom of long forms, with the
    shared `.dirty` indicator or `.saved-note`, then the primary action.
  - `.btn[aria-disabled=true]`: unavailable but still focusable (Save with
    nothing to save keeps focus after a successful save).
  - `.ro-value` (+ `.is-empty`): read-only values in full text colour, so
    locked forms stay readable. `.field-lock`: why a field is locked.
  - `.copy-field`: a read-only value with a Copy action.
  - `.text-btn`: an inline text action; hover changes the text colour only.
  - `.modal.is-wide` + `.compare` (`.compare-head`, `.compare-row`,
    `.compare-label`, `.compare-old`, `.compare-new`, `.compare-off`,
    `.compare-foot`): a confirmation that reviews before/after values. At
    phone width rows stack with inline "Now / After" labels (`.compare-k`),
    and the dialog scrolls when it is taller than the screen.
- **Event overview** (added with Overview):
  - `.stages` › `.stage` (`.is-done` / `.is-current` / `.is-overdue` /
    `.is-ended`) › `.stage-mark` + `.stage-dot`, `.stage-text`
    (`.stage-name`, `.stage-when`). The page sets `--stage-count`.
    - Done stages show actual moments ("Opened 10 May, 18:00").
    - Upcoming stages show scheduled ones ("· scheduled") or "Not scheduled".
    - A transition that was due but didn't happen is `.is-overdue` (dashed
      danger ring, danger text), never styled as done.
    - Horizontal on wide screens, a vertical list at ≤860px.
  - `.transition` (`.is-none` when nothing is scheduled) › `.transition-ic`,
    `.transition-text`, `.transition-actions`: a scheduled transition shown
    with its manual control.
  - `.checklist` › `.check-item` (`.is-done`) › `.check-mark`, `.check-label`,
    `.check-sub`, plus an optional `.text-btn` link. Neutral by design: these
    are requirements for the next transition, not failures. `.later-note`
    covers preparation that isn't relevant yet.
  - `.issue` (`.is-failure` icon and danger title, `.is-pending` amber dot)
    for actual failures and pending work, one row each.
  - `.act-group` / `.act-group-label` / `.act-row` (`.act-text`,
    `.act-title`, `.act-sub`): secondary and exceptional actions, each with
    its consequence. Recovery uses ordinary buttons; removal uses
    `.btn-outline-danger`.
  - Breadcrumbs: on event pages the middle crumb (`.crumb-mid`) truncates on
    phones instead of wrapping the bar.
- **Date/time picker** (added with Schedule; also used by Overview's dialogs):
  - `.dtp` is one field: `.dtp-date`, `.dtp-sep`, `.dtp-time` and a calendar
    button (`.dtp-btn`). States: `.is-open`, `.is-invalid`, `.is-disabled`
    and `.is-readonly`.
  - `.dtp-pop` is the popup, replacing the browser's datetime-local picker.
    - `.dtp-main` holds the month grid (`.dtp-cal`, `.dtp-head`,
      `.dtp-month`, `.dtp-grid`) and the time lists (`.dtp-times`,
      `.dtp-tcol`, `.dtp-col`, `.dtp-opt`).
    - `.dtp-foot` holds Today, optional Clear, and Done.
    - `.is-stacked` puts the time lists below the calendar under 440px;
      `.is-up` opens it above the field.
  - Days: `.dtp-day` with `.is-outside` (adjacent months, subdued),
    `.is-today` (a dot), `.is-selected` (accent fill) and `.is-disabled`.
    Hover and keyboard focus are distinct.
  - Markup is repeated per instance, like the other overlays: the canvas
    can't share child components. Its behaviour and view model come from
    `DKAdmin.dtp.view` (see Interaction contracts).
- **Page tabs and field lists** (added with Signup setup):
  - Page tabs: `.tabs` with `button.tab` and `role="tab"`, as a real tablist
    (arrow keys, Home and End move between tabs). A tab whose panel holds
    unsaved edits shows `.tab-dot`, because switching tabs keeps them.
  - `.form-bar.is-inline`: a card's own save bar when a page has several
    separately saved cards (not sticky).
  - `.q-list` › `.q-row` (`.is-off`, `.is-flash`) › `.q-num` (only on rows
    that can be reordered), `.q-text` (`.q-title`, `.q-meta`, `.q-sub`) and
    `.q-actions`. One row per form field: label, answer type and status, then
    choices or help text. `.q-rename` is the inline rename state, `.q-empty`
    an empty section and `.q-add` its add button. Below 640px the actions move
    under the text.
  - `.ch-list` › `.ch-row` (`.ch-n`, input, remove button): the option list
    editor. `.ch-ro` is the locked, read-only list.
  - `.drawer.is-behind`: a confirmation over a drawer dims the drawer, like
    `.modal.is-behind`, instead of adding a second backdrop
    (`.m-scrim.is-stacked`).
  - Existing `.btn-quiet-danger` is used for row-level Delete… and Turn off….
- **Draft board and team cards** (added with Teams / Draft):
  - `.turn`: the compact status bar above a live board. `.turn-pick` (pick
    and round), `.turn-now` › `.turn-team` (the team picking now),
    `.turn-next`, `.turn-msg` (in place of the team when there's no turn) and
    `.turn-acts`. The page alternates `.is-swap-a` and `.is-swap-b` so the
    team name fades in on each change.
  - `.ctl`: who may act on a shared live board, with one quiet secondary
    action (`.text-btn`). `.is-other` (someone else, amber dot) and
    `.is-none` (no one).
  - `.dboard` (`--team-count`) › `.dteam` (`.is-turn`) › `.dteam-head`
    (`.dteam-ord`, `.dteam-name`, `.dteam-meta`) and `.dteam-list` ›
    `.dmem`. Rows: `.dmem-tag` (pick number or Pre), `.role-badge`
    (`.is-co`), `.dmem-name` and `.dmem-ehb`. Columns list only actual
    members, so they start short and grow with each pick; no height is
    reserved for the final roster (the head keeps "4 / 12"). `.is-pending`
    is a pick still saving and `.is-new` a confirmed one (shared row entrance
    and flash). Columns are a container query: under 210px, size and EHB
    move under the name and member EHB hides. Below 860px the board has two
    columns.
  - Drawing the order: `.dboard.is-shuffling` wait-shuffles the columns in
    place while the draw saves (each `.dteam` carries `--i` for the
    stagger; positions read "–", so no order is shown), then
    `DKAdmin.reorder` moves them to the confirmed order while
    `.dboard.is-reordering` is set. Reduced motion removes both.
  - `.pool` (`.is-inert` while a request runs or picking isn't allowed) ›
    `.pool-head` (`.pool-title`, `.pool-count`) and `.pool-grid` › `.pchip`
    (`.pchip-name`, `.pchip-ehb`, `.is-pending`, `aria-disabled`). Chips act
    on click, with no selection state, and the grid is one tab stop with
    roving focus. `.pool-empty` is its empty line.
  - `.tcards` › `.tcard` (`.tcard-head`, `.tcard-name`, `.tcard-sub`,
    `.tcard-warn`, `.tcard-empty`, `.tcard-list` › `.tmem` with `.tmem-name`,
    `.tmem-tag`, `.tmem-ehb`, `.is-new`, and `.tcard-foot`): a team with
    its members as the main content. One column below 640px.

- **Board grid and tile editor** (added with Board):
  - `.bgrid` (`--cols`, `--tile-h`) with `.bgrid-row` rows (`display:
    contents`). Each row ends with a `.bline` total, and a last row holds the
    column totals. `.bline`: `.is-high`, `.is-low` (25% or more from the
    average line, with an arrow and percentage), `.is-partial` (incomplete),
    `.is-col`, `.is-corner`.
  - `.bcell` positions hold `.btile` (`.btile-top`, `.btile-art`,
    `.btile-name`, `.btile-foot`, `.btile-ehb`, `.btile-flags`) or `.bempty`
    (`.is-readonly`). States: `.is-issue`, `.is-changed`, and while moving
    `.is-src` and `.is-target` on the cell (`.is-outside`, `.is-cut` are
    available for size previews). Tiles under 132px wide drop the artwork and
    pills (container query). `.bmove-bar` explains keyboard moving.
  - `.plan`: a headline total (`.plan-total`) and `.plan-list` rows with an
    optional inline number field, saving spinner and saved tick
    (`.plan-ok`).
  - `.combo`: a searchable multi-select with `.combo-chips`/`.combo-chip`, a
    `.combo-pop` listbox of `.combo-opt` (`.is-active`, a check when chosen)
    and `.combo-empty`. `role="combobox"` with `aria-activedescendant`.
  - `.obj` objective cards (`.obj-head`, `.obj-num`, `.obj-sum`,
    `.obj-body`; `.is-single` drops the chrome; `.is-collapsed`,
    `.is-locked`). `.drop-group` › `.drop-group-head`
    (`.drop-group-count`) and `.drop-list` › `.drop-row` (`.is-on`,
    `.is-locked`, `.drop-rate` with `.is-missing`, `.drop-weight`).
  - `.count-line` ("Collect [n] …"), `.count-sum` (one-line summary with a
    text action) and `.count-opts`.
  - `.ehb-box` › `.ehb-row` (`.ehb-val`, `.is-struck` for a replaced
    calculated value) and `.ehb-note` (warning with a link).
  - `.art-pick` (`.art-thumb`, `.art-img`, `.art-acts`) and `.auto-text`
    (an automatic value shown read-only).
  - `.drawer.is-wide` (640px) for editors that need more room.
  - Dialog details shared by confirmations: `.m-points` (consequence list)
    and `.m-banner` (a banner inside a dialog). Teams / Draft now uses these
    instead of its page-local copies.
  - Images use `background-image: var(--art)` rather than `<img src>`, so
    template placeholders aren't fetched before rendering.

- **History table and change details** (added with Audit):
  - Two-line cells: `.cell-main` with `.cell-sub` below (the time under the
    date, the record under the action). `.cell-tags` puts a label and small
    pills on one line. `.actor` (`.is-system` with a gear for automated
    actions).
  - `.fpanel`: a small anchored, non-modal panel for less-used filters, with
    `.fpanel-presets`, `.fpanel-pair` and `.fpanel-foot` (Reset / Apply).
    `.is-closing` plays the popover exit.
  - `.filter-count` on a `.filter-btn` counts the panel's active filters;
    `.filter-row` lists every active filter as a `.filter-chip`, with Clear
    all.
  - `.chg`: field changes as a list of label, previous value and new value
    (`.chg-head`, `.chg-row`, `.chg-field`, `.chg-val` with `.is-before`,
    `.is-empty`, `.is-clamped`; `.chg-more` toggles a clamped value). Below
    440px of its own width it stacks into labelled Before / After lines
    (`.chg-k`), so it fits drawers and phones.
  - `.code-block` (safe, already-sanitised text; scrolls past 200px;
    `.is-note` for a withheld or unreadable value) and `.code-inline` for
    keys.

- **Evidence inspection** (added with Review):
  - `.subhead`: a record opened from a list, with back, the title and status,
    and `.subhead-pager` (Previous, `.pager-pos`, Next).
  - `.iv` image viewer: `.iv-bar` (zoom out/in with `.iv-pct`, Fit and 100%
    as pressed buttons, Original) and a focusable `.iv-stage` holding
    `.iv-img` (scaled and translated, image from `--art`; `.is-anim` for
    button zooms) or `.iv-state` (loading, failed, missing). Keys: + and −
    zoom, 0 fits, 1 is actual size, arrows move; drag pans, the wheel zooms.
  - `.facts`: what to compare with the evidence. `.fact` (label, value,
    `.fact-sub`; `.is-plain` for longer values, `.is-warn` for a boundary to
    check), `.code-tag` for a code to match, and `.facts-note` for notes and
    feedback.
  - `.decide`: the decision panel (`.decide-acts`, `.decide-link`,
    `.decide-note`, `.decide-form`, `.decide-weight`, and `.decide-result`
    with `.is-success` after a decision).
  - `.tl`: a read-only history timeline (`.tl-item`, `.tl-what`, `.tl-when`,
    `.tl-reason`).
  - `.badge-danger` (rejected, reversed) and `.seg-count` (a count in a
    segmented option).

- **Results and publication** (added with Final review):
  - `.place`: a rank marker (`.is-top`, `.is-1` tinted, `.is-shared`
    outlined for an exact tie, shown as "=3").
  - `.tval`: a table value (`.is-muted` for not applicable, `.is-decider` for
    the competitive input that separates a row from its neighbour; the
    explanation goes in `.cell-sub`).
  - Checklist states on Overview's `.check-item`: `.is-wait` (a normal timed
    wait) and `.is-blocked` (needs action elsewhere, with a link button).

- **Integration status** (added with WOM):
  - `.chip.is-managed`: a capability chip (accent when the website owns the
    connection; a plain `.chip` otherwise).
  - `.meter-fill.is-partial`: an incomplete part in amber. `.meter-fill` is
    now `display:block`, so it works inside a `<span>` too.
  - `.status-rows` › `.status-row`: label, state (`.status-val`, `.is-good`,
    `.is-bad`) with `.status-sub`, and an optional action; one column below
    760px. Secrets show only their verification state.
  - `a.btn` keeps the button look (no underline).

- **Compact record list with an inline editor** (added with Catalogue):
  - `.dlist` › `.dlist-item` › `.dlist-row`: a button (`aria-expanded`)
    summarising a record with `.thumb`, `.dlist-name`, `.dlist-sub`,
    `.dlist-flags` and a figure (`.dlist-fig`, `.dlist-ehb`,
    `.dlist-fig-k`). One item opens at a time (`.is-open`) into `.dlist-ed`
    inside the same item. `.is-inactive` dims it; flags hide below 640px.
  - `.thumb` (`.is-lg`): a square image from `--art` on a neutral tile.

- **Account access** (added with Accounts):
  - `.secret-result`: a one-time secret (a reset link) in a focusable panel
    beside the action that made it: `.secret-head` (`.secret-title`,
    `.secret-exp`), a `.copy-field`, `.secret-notes` and `.secret-foot` with
    Done. Held only in memory while shown. `.is-warn` is the unconfirmed
    state, which never recovers or silently regenerates the secret.
  - `.perm-note`: why an action isn't available to the signed-in admin,
    instead of a disabled control.
  - `.field-meta` with `.field-count` (`.is-near`): a hint and a character
    count under a field.
  - `.avatar.is-lg` heads a person's drawer; `.pill.is-success` marks a
    confirmed state. Role changes reuse `.chg` for Now → After.

## Interaction contracts (`ui/behavior.js`)

- **Opening an overlay** moves focus into it without scrolling. The focus ring
  appears only for keyboard use, never as an outline around the container.
  Background scrolling is locked while a drawer, modal or mobile nav is open.
- **Focus** is trapped in drawers, modals and the mobile nav (`trapTab`) and
  returns to the control that opened them (`restoreFocus`, with a stable
  fallback). Tab or Shift+Tab from the dialog container itself wraps to the
  first or last control. When no enabled controls remain (for example
  while saving), focus stays on the container.
- **Dismissal:** Escape or a backdrop click closes the top layer only. A drawer
  with unsaved edits asks to discard, in a nested dialog that gets focus on
  "Keep editing".
- **Menus:** arrows wrap, Home/End jump, and Escape or Tab close and restore focus
  (`menuKeydown`). Placement follows `placeMenu` ('end' / 'start' / 'side').
- **Closing is symmetric:** `closeLayer` plays the exit animation, then unmounts
  once it has run. Exit timing is read from the element's computed CSS animation
  (`afterExit`), so `tokens.css` is the only source of durations. With reduced
  motion, layers close almost instantly, and if no animation runs they close
  immediately.
- **Pending actions** disable their controls and keep the busy button's color
  with a spinner. Failures keep entered data, show a banner or error toast with
  Retry, and move focus to the retry control.
- **Toasts:** at most 3. Success toasts close after 4.5s, toasts with an action
  after 7s.
- **Immediate actions on a live board** (Teams / Draft):
  - An action that runs on a single click (a pick, Undo, take control,
    release) keeps a shorter presentation minimum,
    `settle(cmp, startedAt, fn, motion.busyMinQuick)` (250ms), instead of the
    600ms saving minimum. Saving elsewhere is unchanged.
  - The minimum only delays showing a result that is already confirmed;
    nothing is shown as done before the response. Prototype latency is
    separate (`mock.quickLatency`, 300ms here, versus 650ms for saves).
  - While it runs, the controls that could repeat it are inert, and the
    pending item shows a spinner in place.
  - A confirmed result updates the view from the server.
  - A failure leaves the view untouched and shows an error toast.
  - An uncertain outcome keeps the old view and blocks repeat actions until
    a readback ("Check again") reports what happened.
- **Reorder** (`DKAdmin.reorder`, with `DKAdmin.tokenMs`): `measure(selector)`
  before the state that changes the order is committed, then
  `play(cmp, before, selector, done)` after it.
  - Items are matched by id, so it works whether the runtime moves nodes or
    rewrites them in place. Each travels from its old position to its new
    one with a small lift, staggered by DOM order, using the reorder tokens
    and the shared curve.
  - It presents an order the page already has and never chooses one.
  - With reduced motion it doesn't animate and calls `done` at once.
- **Tables** scroll sideways only when needed (`watchOverflowX`). Participants
  and the Dashboard drop secondary columns at 1260 / 1080 / 860; the Events
  directory keeps every column and scrolls with a sticky name column instead.
- **URL state:** `toQuery(values)` builds a query string that leaves out
  defaults, so shared links stay short; `fromQuery(search)` reads one back.
  Pages validate every value and fall back to defaults.
- **Saving feedback:** `settle(cmp, startedAt, fn)` keeps a busy state
  ("Saving…") visible for at least `motion.busyMin` (600ms), so a fast
  response still reads as a deliberate step. That is product behaviour, and
  reduced motion skips the wait. `DKAdmin.mock.request` is prototype-only
  simulated latency, not a timing rule; the application simply awaits its
  request.
- **Date/time picker** (`DKAdmin.dtp`):
  - **Value:** a local wall time `YYYY-MM-DDTHH:mm` in the event timezone,
    the same string `datetime-local` produced, so existing value handling is
    unchanged. The page owns the value, and the picker's UI state lives in
    `state.dtp[id]`.
  - **Typing:**
    - Dates such as "27 Jun 2027", "27/6/2027" or "2027-06-27"; times such as
      "18:30", "18.30" or "1830".
    - Valid entries are normalised on blur. Invalid ones stay as typed and
      show their error once you leave the field.
    - `dtp.status()` reports a typed-but-invalid entry as `pending`, so the
      page counts it as an error and as an unsaved change. The stored value
      stays the last valid one.
  - **Calendar:**
    - Arrows move by day and week, Page Up/Down by month (with Shift, by
      year), and Home/End to the ends of the week. Enter picks the day and
      moves focus to the hour list.
    - The hour and minute lists move with Up/Down and Home/End. Minutes are
      in five-minute steps.
    - Tab stays inside the popup. Escape or Done closes it and returns focus
      to the calendar button. Escape is handled before any surrounding dialog
      sees it, and a pointer press outside closes it.
    - The popup is fixed-positioned against the field: below it, or above
      when there's no room, and kept on screen. It follows scroll and resize.
  - **Options:** `min` disables earlier days; `clearable` adds Clear;
    `defaultTime` is used when a day is picked before any time; `today` marks
    today in the event timezone.
  - **Time zones:** `localToUtc(value, tz)` reports `invalid` (skipped by a
    clock change) or `ambiguous` (happens twice) so pages can explain it.
    `utcToLocal` and `todayIn` convert the other way.

## Dashboard: agreed metric definitions (reference)

These are the definitions the Dashboard prototype follows. The Dashboard
itself is new functionality: nothing here claims the application provides
these figures yet. They should be promoted into the functional authorities
before implementation.

- **Which events count:**
  - An event counts once it has gone Live: states Awaiting final review,
    Finalized and Archived.
  - Cancelled events (only possible before Live) and Discarded events never
    happened.
  - Hidden is quarantine, not a lifecycle state. Hidden events are excluded
    because admin projections must exclude them.
- **Players:** people with a team membership during the event, counted once
  per event even with several playing accounts or a team change. Total
  participations are the sum across all events, including imported history.
- **Unique and returning participants:**
  - Counted by website account only; identities are never guessed from
    character names.
  - Imported participants have no account link, so tracked history starts
    with the first platform event. That event is marked "Tracking starts" and
    has no returning split.
  - Unique participants equal the sum of first-time people by actual-start
    cohort, so the headline and chart agree. Events with equal actual starts
    share one cohort and a person is counted once in that cohort; do not sum
    per-event first-time rows across mixed cohorts.
- **Approved submissions:** platform events only; reconstructed import rows
  are excluded. For an event in final review the count is current and may
  change.
- **Official results:** a winner and board completion are shown only once an
  event is finalized. Live events and ended events awaiting review count towards
  participation and are marked Provisional; provisional figures do not become
  official winners or board-completion results.
- **EHB gained:** secondary. Shown only where Wise Old Man data exists, always
  with account coverage (frozen snapshot for the imported event).
- **Community:** current snapshots only (total accounts, created since the
  last event ended, and "Logged in during the last 30 days" from the last
  login).
- **Not shown because it can't be rebuilt from retained data:** peak waiting
  list, how fast an event filled, and everyone who was ever waitlisted.

The prototype uses sample data with a future "today" so that several events
exist. The account menu switches between sample histories (full history,
imported plus one platform event, imported only, none) and can simulate a load
failure. These are prototype-only tools.

## Events: agreed behaviour and the reference contract

The agreed brief is **Events — agreed design brief** (E01–E04) in
FUNCTIONALITY_CHANGES.md. This section records how the reference realises it
and what production needs. Nothing here claims the application does this yet.

- **Directory:** one table with the views All (default), Current & upcoming
  (Setup, Signups open, Signups closed, Live) and Past (Final review, Finished,
  Archived, Cancelled). Hidden (quarantine) is a separate SuperAdmin-only view
  of the same table, excluded from every other view, count and summary. A
  non-SuperAdmin opening a Hidden link sees All with an explanation.
- **Default order:** Live first, then upcoming and setup events by start date
  (unscheduled last), then past events by end date, newest first. This is
  stricter than today's page, which groups past events by state before date.
- **Columns:**
  - Event: the name is a link to its Overview, with an Imported pill for
    imported history.
  - Phase.
  - Dates: the range, or "Not scheduled", plus one line of context. Live:
    "Ends in 4 days". Signups open: "Signups close 15 Jun". Setup: "Signups
    open 1 Oct" or "Starts in …". Signups closed: "Starts in …", or "Start was
    due …" after a failed start. Final review: "Ended 10 days ago". Cancelled:
    "Didn't take place". A timezone suffix is shown when it isn't
    Europe/Copenhagen.
  - Participants: confirmed / capacity with a meter while preparing, plus
    "N waiting", "Full" or "N spots left". Without a capacity it reads "No
    capacity set". Live events show "N players", finished events "N played",
    and cancelled events "N confirmed when it was cancelled". Missing import
    capacity is never invented.
  - Needs attention.
  - No slug column, no Workspace button, and no row menu.
- **Needs attention:** one priority item.
  - A failure (for example Start failed or WOM sync failed) comes first.
  - Otherwise pending review work: "18 to review" counts as ONE item however
    many submissions it holds.
  - Otherwise a quiet dash.
  - The rest collapse into "· +N", a hint that lists them; the Overview
    explains everything.
  - Ordinary unfinished setup is never attention, not even quietly (setup
    blockers removed 1 October). A failed transition, like Start failed,
    still is.
  - Sorting by Needs attention puts failures first, then the review count.
- **Attention filter** (agreed addition): "N need attention" in the page
  summary toggles a filter (`attention=1`), shown as a removable chip.
- **Sorting:** name, phase (lifecycle order), dates (unscheduled always last),
  participants, needs attention. The first click on a column uses its natural
  direction. "Default order" in the footer returns to the default.
- **States:** loading skeleton, load failure with Try again (focus moves to
  it), No events yet (with Create event), No matching events (with Clear
  filters), empty Current / Past views, and an empty Hidden view.

### URL contract

`/admin/events?view=&phase=&search=&attention=1&sort=&direction=&page=`. Only
non-default values appear. The values are:

- `view`: `current`, `past` or `hidden`
- `phase`: the existing state keys, e.g. `draft`, `signupopen`,
  `awaitingfinalreview`
- `sort`: `identity`, `state`, `dates`, `signups` or `attention`
- `direction`: `asc` or `desc`

Unknown values fall back to defaults, and a phase outside the selected view is
dropped. Legacy links map across: `?filter=<state>` becomes `phase`, and
`?filter=hidden` becomes `view=hidden`. Search is by event name only (the slug
isn't visible, so matching it would be confusing).

- Filter, search and sort changes **replace** the current history entry: Back
  leaves the page rather than undoing a filter.
- Opening an event pushes `/admin/events/{slug}`. Back restores the directory
  from its URL, with scroll position and focus on the row's link.
- `/admin/events/new?<directory query>` is the Create dialog over the
  directory:
  - Opening it from the directory pushes that URL; closing it goes Back, so
    the filtered list is preserved.
  - A direct visit renders the directory underneath. Closing then replaces
    the entry with `/admin/events`.
  - Back, Forward, reload or leaving follows the dialog's rules: untouched
    forms close, changed values ask to discard, and nothing happens while
    saving. In production, add a `beforeunload` prompt for reload and tab
    close while the form has changes.
- **Success** replaces `/admin/events/new` with the new event's Overview. Back
  then returns to the directory, where the new row is highlighted once, and
  neither Back nor reload can submit again.

### Create event

- The dialog asks for the event name (required, 50 characters max, the
  application's effective limit; counted in characters, with a
  counter near the limit) and the timezone, which starts as Europe/Copenhagen.
  It offers the supported list; today that's Europe/Copenhagen and UTC.
  Saving creates a private draft and opens its Overview. Description, dates,
  capacity, artwork and signup configuration all happen in the event
  afterwards. Existing authorization, unique slug generation, audit and
  atomic creation stay as they are.
- **Duplicate-name notice** (agreed addition): a non-blocking note if an event
  with the same name already exists. Hidden events are included only for
  SuperAdmins.
- **Validation:** on submit, errors stay with the field, focus moves to it,
  and values are kept.
- **Saving:** the fields, Cancel, Escape and backdrop are disabled, and the
  primary button shows "Creating…".
- **Failure (server says nothing was saved):** an error banner, values kept,
  and Create event available again.
- **Uncertain (timeout or lost connection):** a warning banner, with the
  fields locked so a retry can't change what was sent. The primary action
  becomes **Check again**. If the event exists, the dialog opens its Overview
  (with the toast). If not, "It wasn't created" appears and Create event is
  available again.
- **Request key — backend requirement.** Guaranteeing "never twice" needs a
  one-time key:
  - The dialog generates a request key once per open.
  - Create sends the key, and the server creates at most one event per key
    (a unique constraint, which also returns the existing event on a retry).
  - "Check again" asks the server about that key.
  - Without this, the only fallback is a heuristic lookup by name, creator
    and recent time, which same-name events can fool.

The canvas can't change the real browser URL, so the Events artboard simulates
the browser with an in-memory history. The dashed **Prototype URL** bar
(Back, Forward, Reload and the current URL) and the account menu's
**Simulated browser** items (open a shared filtered link, open the Create link
directly) exercise the contract. Other prototype-only controls:

- the Role switch (Administrator / SuperAdmin)
- sample data: the full directory with a Live current event, the same
  directory with the current event in Final review, or with a legacy Finished
  current event, or no events. Each holds at most one event in the exclusive
  current slot (Live, Final review, Finished). Hidden samples are post-Live
  (a Final review event and an archived import copy).
- Fail next load
- the outcome of the next Create request: succeeds, fails, or times out
  (either created or not)

## Identity: reference behaviour and integration notes

Identity edits an event's public-facing details on a dedicated page form (no
drawer). The prototype works end to end with synthetic data. The application
already has the unchanged-unsupported-timezone handling, the stale-version
refresh, browser departure warnings and the agreed lifecycle editing
restrictions. Three items remain **integration work**: field-level conflict
handling, the readback behind "Check again", and staying on Identity after
saving.

- **Fields:**
  - Event name: required, up to the approved 50 Unicode characters
    (consistent with WOM titles); an emoji counts once.
  - Description: optional, up to 4,000.
  - Buy-in information: optional, up to 2,000. Free text explaining the entry
    payment, not a price or a payment.
  - Timezone.
  - Description and buy-in are counted the way the application's
    `StringLength` validation counts them (string length of the value as
    entered, so an emoji counts as 2). Counters appear near a limit.
- **Permanent event link** (decided 1 October; supersedes the earlier
  proposal for a bare `/Events/{slug}` route, which is withdrawn):
  - Identity shows and copies the absolute URL of the existing
    `/Events/{slug}/Signups` page, built from the server-generated slug. It
    never changes when the name changes, and there's no editable slug.
  - Codex's source review found that this page already moves public visitors
    on to Teams and the Board as publication progresses, so no new route is
    introduced.
  - Existing visibility restrictions apply unchanged: a private event isn't
    publicly available through this link.
  - The prototype uses `https://bingo.example` as a stand-in origin; the
    application uses its configured public base URL. Other specific public
    links stay on Overview.
- **Not here:** banner or artwork (retired), dates (Schedule), lifecycle
  actions and readiness (Overview).
- **Timezone:**
  - The choices are the supported list (today Europe/Copenhagen and UTC).
  - An existing stored timezone that's no longer offered is shown truthfully
    as "· current" and kept while other fields are saved. Once changed, it
    can't be chosen again.
  - The application already accepts an unchanged stored value that isn't in
    the list.
  - Changing the timezone never moves the stored UTC moments.
- **State rules:**
  - Name, description and buy-in are editable in Setup, Signups open, Signups
    closed, Live and Final review.
  - Timezone is editable only before the first Live transition, and stays
    locked in every later phase, including a reopened one.
  - Finished, archived and cancelled events show a read-only page with one
    sentence explaining why.
  - Read-only values keep full text colour.
  - These are the agreed lifecycle editing restrictions, which the
    application already enforces.
- **Saving:**
  - An explicit Save changes action; no autosave.
  - Save is `aria-disabled` when there's nothing to save.
  - While saving, fields and Save are disabled and navigation waits.
  - Success keeps you on Identity, updates the saved state, shows the shared
    toast and a quiet "Saved" note, and adds no history entry.
    **Integration:** post-redirect-get back to Identity (today's page
    redirects to Manage).
  - Validation errors stay with their fields (with a summary when several
    fail), focus moves to the first one, and values are kept.
  - A failed save shows an error banner and keeps the edits.
- **Stale edits:**
  - The server rejects a save made against an older version, and the page
    refreshes to the latest version (both exist today).
  - Fields you didn't touch take the other admin's value.
  - Fields you both changed keep yours, with theirs shown beside the field and
    a "Use theirs" action.
  - Nothing is saved until you review and save again against the latest
    version.
  - The banner always explains what happened, even when no Identity field
    differs (for example only the schedule changed under a timezone review).
  - **Integration:** the field-level conflict handling (keep yours, show
    theirs, "Use theirs") is new; the latest values need to reach the form
    alongside your edits.
- **Uncertain outcome** (timeout or lost connection):
  - A warning banner says the save wasn't confirmed, the fields lock, and Save
    becomes **Check again**.
  - Checking reads back the event's current values:
    - If they match what you entered, it says the event now has those values
      ("Up to date"). It can't tell which request stored them, so it
      doesn't claim this save did. No request receipts are introduced just
      for stronger wording.
    - If the event still has its earlier values, your changes weren't
      applied; your edits are kept and you can save again.
    - If someone else changed it, the stale flow runs.
  - It's never shown as success, and it never overwrites blindly.
  - Navigating away while the outcome is unknown doesn't say the changes
    "haven't been saved". A dialog offers **Check again** (the default
    focus) or **Leave anyway**, with an explicit warning that the save may
    already have happened.
  - **Integration:** the readback that "Check again" relies on.
- **Timezone review after public exposure:**
  - Once the event has been public, saving a timezone change opens a wide
    confirmation.
  - It lists each *scheduled* moment in the old and proposed timezone, with
    UTC offsets at that moment (DST-aware). Unset dates are listed as "Not
    scheduled yet", never invented.
  - Other edits stay in the form, and the dialog names them as saved together.
  - Cancel returns to the form without saving; confirming saves the reviewed
    change.
  - The confirmation carries the version it was based on. If the event
    changed, the save is rejected, the dialog closes, the stale flow runs (its
    banner says saving again opens a fresh review), and the next save shows
    the review again with the latest times.
  - Before public exposure, a timezone change saves normally, with a note
    under the field.
- **Navigation:** leaving with unsaved changes, via the sidebar, breadcrumbs,
  event switcher, Back/Forward or reload, opens the shared "Discard unsaved
  changes?". Keep editing preserves the form; Discard continues the navigation.
  If it opens over the timezone review, the stacked-dialog treatment applies.
  Background scrolling is locked while either dialog is open. Browser
  departure warnings for reload and closing the tab already exist in the
  application.
- **URLs:** `/admin/events/{slug}/identity`. The event switcher opens the
  other event's Identity.

The prototype controls live in the account menu: the next save's outcome
(succeeds, fails, times out saved or not saved, another admin saves first,
another admin changes the schedule first), "Another admin edits this event
now", "Another admin changes the schedule now", Fail next load, and the simulated
browser (Back, Forward, Reload, open this page directly). Switch events to see
each state:

- Autumn: public, timezone review.
- Winter: never public, so the timezone saves directly.
- Clan Cup: UTC, draft time unset.
- Nordic Night: a stored timezone no longer offered.
- Midsummer (Live) and Summer (Final review): text editable, timezone locked.
- Spring: finished, read-only.

## Overview: reference behaviour and integration notes

The Overview is the landing page for an event. It answers where the event
stands, what needs attention, and what you can do next. The community Dashboard
keeps cross-event statistics.

It follows the approved Admin simplification: the `admin-simplification`
branch, `PRODUCT_REQUIREMENTS` "Approved Admin simplification target —
2026-09-26", and the 1 October Overview brief in FUNCTIONALITY_CHANGES. The
working tree on `feature/boss-artwork` predates that simplification; don't
design from it.

### What the reference implements

- **Progression:**
  - Setup → Signups open → Signups closed → Live → Final review → Archived.
  - Actual moments are shown for done stages; scheduled moments, or "Not
    scheduled", for upcoming ones.
  - A postponed automatic start shows the Live stage as overdue ("Was due …
    · postponed"), never as done.
  - A cancelled event ends the progression at a "Cancelled" stage.
- **Needs attention:** only actual failures and pending work, each once:
  - Automatic start postponed (with what's still missing, or "ready now").
  - Submissions to review while Live.
  - A failed Wise Old Man sync. This is the same illustrative example as the
    Events directory; it never blocks lifecycle actions.
  - In final review, outstanding reviews are a publish requirement, so they
    appear there instead.
- **Current stage panel:** one panel per phase, with a short lead.
  - **Next transition:** the scheduled moment is shown together with its
    manual control, for example "Signups close automatically on 15 Jun" and
    Close signups now. When nothing is scheduled, it says so with a Schedule
    link.
  - **Requirements:** listed neutrally, each linking to its owning page.
    - Setup: "Before you can open signups": description (Identity),
      capacity (Signup setup), event window and closing time when opening is
      automatic (Schedule), signup form (Signup setup).
    - Signups closed: "Before you can start the event": draft finalized,
      board published, drop values, playing accounts, end still ahead.
    - Final review: "Before you can publish official results": uploads
      closed, every submission reviewed, placements calculated.
  - Later preparation is a quiet one-line note.
  - A control whose requirements aren't met is `aria-disabled`, with the
    checklist as its description.
- **Manual controls and confirmations:**
  - Only controls that apply to the current phase are shown, and every
    confirmation lists its actual consequences:
    - **Open signups:** makes the event public; when it closes (scheduled, or
      the proposed fallback); turns automatic opening off; later timezone
      changes need review.
    - **Close signups:** places kept; the scheduled close is no longer needed;
      can reopen until the draft is locked.
    - **Reopen signups:** only before the draft is locked; places and order
      kept; when it closes.
    - **Start event:** one confirmation, no reason. Explains an early or
      postponed start, the upload window, fixed drop values and the first WOM
      refresh.
    - **End event:** a reason only before the scheduled end. Uploads stay open
      30 minutes after the actual end; the event moves to final review and
      can be resumed.
  - **Other actions** sit beneath the stage panel:
    - **Recovery** (ordinary buttons, final review only):
      - Resume event: reason required; a replacement end only when the stored
        end has passed.
      - Reopen uploads: future time and reason required.
    - **Remove** (secondary, outlined in danger):
      - Delete event: only an empty setup with no protected history.
      - Cancel event: reason required; history kept; players notified.
    - **Quarantine, SuperAdmin:** Hide event, after Live, with a reason and no
      typed name. A hidden event opens in a limited inspection view with
      Restore; other admins get "Event not found".
- **Action states:**
  - **Pending:** Working…, with the dialog holding focus and dismissal
    blocked.
  - **Failure:** inputs kept, Try again.
  - **Stale:** the version read when the dialog opened is checked. If the
    event changed and the action still applies, the dialog refreshes its
    consequences and asks to confirm again. If it no longer applies (for
    example signups were already closed), it says so and offers only Close.
  - **Uncertain:** fields lock and the button becomes "Check again", which
    reads the event back and reports what it now shows. Closing re-reads the
    page.
  - **Success:** a toast, the page updates to the new phase, and focus moves
    to the stage heading.
  - Controls are recomputed from the current state, so a phase change never
    leaves a stale control actionable.
- **At a glance:**
  - Participants (or teams and players), board, approved submissions, uploads
    (open, closed or reopened, with the time), evidence codes, Wise Old Man.
  - Row labels link to the owning page. Nothing listed in Needs attention or
    the requirements is repeated here.
- **Evidence codes:**
  - Turn them on or off, see the code history (active, scheduled, ended), and
    add a code (generate, active from, optional note).
  - Codes chain: each is retired when the next starts.
  - Available from Setup through Live, and in final review while uploads are
    accepted.
- **Date and time inputs** (reopen uploads, resume with a replacement end, and
  a code's "Active from") use the shared date/time picker. Their values,
  validation and outcomes are unchanged; a typed but invalid entry now counts
  as that field's error.
- **Public links:** they follow `EventDestinationPolicy` and the public page
  handlers in the current implementation. Publication facts decide what's
  shown, not the phase.
  - **Never public** (`FirstPublicAt` not set): a plain note and no links. A
    cancelled event that was never public shows no panel. Every public route
    returns not found for it.
  - **Permanent link** (`/Events/{slug}/Signups`): shown whenever the event
    has been public. Its note says what a visitor gets:
    - Published board or results: taken to the board.
    - Otherwise, an active published roster: taken to the teams.
    - Otherwise: the signup list.
    - Admins always see the signup list.
  - **Signup form** (`/Events/{slug}/Signup`): only while signups are
    accepted (open, with a future closing time) and nothing has been
    published yet. Once something is published, visitors are redirected
    away from the form.
  - **Teams** (`/Events/{slug}/Teams`): whenever an active roster
    publication exists.
  - **Board** (`/Events/{slug}/Board`): whenever the board is published,
    including before Live. Raids Week shows this, as does a signups-closed
    event after "Another admin changes this event now" publishes its board.
  - **Cancelled, previously public:** only the permanent link, noted "Visitors
    see that the event was cancelled". The handlers keep `/Signups` (the list
    with a Cancelled status, or a redirect), `/Teams` and `/Board` reachable
    and render a cancelled notice there. Stats returns not found.
  - **Hidden:** no public pages. The hidden view replaces this panel.
  - Stats isn't listed separately; it's reached from the board.
  - The fields show the path; Copy copies the absolute URL.
- **States:** loading skeleton, failed read with Try again, not found or not
  visible, read-only results (Archived) and the cancelled state with the
  private reason.
- **URLs:**
  - `/admin/events/{slug}`. Links to other event pages push
    `/admin/events/{slug}/{page}`.
  - Back/Forward, reload and direct entry work through the prototype URL
    bar.
  - A dialog open over the page closes first on Back; a pending action blocks
    leaving.
- **Samples:**
  - The sidebar event switcher lists independent phase samples (two setups,
    signups open, signups closed, a postponed start, a signups-closed event
    ready to start with its teams and board already public, Live, final
    review, archived, cancelled, and a hidden event for SuperAdmins).
  - They aren't one consistent world. For example, current-event exclusivity
    isn't modelled across samples.
  - The account menu sets the role, the next action's outcome (succeeds,
    fails, event changes first, times out either way), "Another admin changes
    this event now", Fail next load and the simulated browser.

### What the application already supports (admin-simplification)

Every lifecycle action and its eligibility, inputs, side effects and audit
already exists:

- Open, close and reopen signups; start; end; resume; reopen submissions.
- Delete, cancel, hide and restore.
- Evidence codes.

Also already in place:

- Readiness evaluation with codes and messages.
- Scheduled transitions, with postponed and failed attempts shown only while
  current.
- Version tokens with "refreshed, entries kept" on stale posts.
- Hidden-event limited inspection.
- Final review on its own page: publishing goes straight to Archived, and
  results can be reopened.

### What needs integration or backend work

- The new composition: progression, single-mention attention, phase panels,
  and requirement checklists mapped from readiness codes to labels and owning
  pages. This replaces "Readiness checks" and the repeated blocker counts.
- Showing only applicable controls, with `aria-disabled` and the checklist
  as the reason.
- Phase-specific consequence copy in each confirmation.
- Asking for the end reason only before the scheduled end. The service already
  works this way; today's UI always asks.
- Re-evaluating a stale confirmation inside the dialog (refresh or "no longer
  applies").
- Reading back an uncertain outcome.
- Evidence codes in a dialog instead of inline forms.
- Public-link visibility: today's Overview always shows the signup links.
  The page needs the facts the policy uses: first public time, active roster
  publication, board publication, published results, hidden, and whether
  signups are accepted. Alternatively, the server can return the decided
  destinations.
- Capacity on Signup setup: capacity readiness and editing belong to Signup
  setup (agreed). Today the player cap is edited on Manage. The Signup setup
  reference page isn't built yet.

### Verified rules this reference follows

Confirmed against the current implementation and the approved simplification
(`PRODUCT_REQUIREMENTS.md` approved admin simplification target, 2026-09-26):

- Publishing official results on Final review moves the event straight to
  Archived. The separate Archive action is retired (`OnPostArchive` returns
  the retired-action response).
- Quarantine (Hide) needs a reason and no typed event name. Restore needs
  neither.
- Hide is allowed only after Live: Final review, legacy Finished
  (`Finalized`) or Archived.
- Live, Final review and legacy Finished share one exclusive current-event
  slot among visible events.
- Capacity belongs to Signup setup. Schedule owns dates.

### Proposed or needing a decision

None open for Overview. Older wording in the application's authority
documents still conflicts with the rules above; that's for Codex to
reconcile.

## Schedule: reference behaviour and integration notes

Schedule is where admins set when an event's stages happen and see what follows
from those times. It edits five times: signups open, signups close, team
draft, event starts and event ends. The page links to the pages that own
related settings and repeats none of them:

- Signup setup owns capacity, signup access and the form.
- Identity owns the timezone. Schedule shows it.
- Overview owns opening, closing and reopening signups, and starting, ending
  and resuming the event.

Verified against the current implementation (the participants-functionality
checkout):

- `Admin/Events/Schedule` and its page model (editability,
  `RestoreLockedValues`, the automatic-opening rule, local parsing)
- `EventSignupLifecycleService.SaveScheduleAsync` and
  `ValidateScheduleChangeAsync`
- `BingoEvent.ConfigureSchedule`, `ConfigureFinalizedDraftEventWindow` and
  `ChangeLiveEventEnd`
- `FUNCTIONAL_CONTRACTS.md` §4.4

### What the reference implements

- **Layout:** one focused form card in three sections: Signups (open, close),
  Team draft (optional) and Event (start, end).
  - Fields sit in pairs no wider than 300px each, and stack on narrow
    screens.
  - Below the end time, a derived line shows when uploads close: the end plus
    30 minutes. It isn't a setting.
  - The summary names the event timezone and its current offset, and links to
    Identity and Overview.
- **Entry:** every time uses the shared date/time picker, in the event
  timezone and in five-minute steps.
- **Locked fields** show the scheduled time as read-only text, with the reason
  in place. For example: "Signups opened 1 Jun 2027, 18:00.", "Locked while
  the team draft is underway.", or "Signups closed … To set a new closing
  time, reopen signups on Overview."
- **Editability follows the page model:**
  - **Signups open:** in a private draft, while still in the future.
  - **Signups close:** in a draft or while signups are open, while in the
    future, and not during or after the team draft.
  - **Team draft time:** while in the future, before the team draft starts.
  - **Event start and end:** until the event goes Live, including during and
    after the team draft. An overdue start can be repaired.
  - **While Live:** only the end.
  - **Final review, archived and cancelled:** read-only, with a banner
    explaining why.
- **Validation:**
  - **Picker errors:** unreadable dates or times, and times that aren't in
    five-minute steps.
  - **Clock changes:** local times skipped or repeated by a clock change get
    their own messages.
  - **Changed times must be in the future;** unchanged past times are kept.
  - **Order:** the end must follow the start. Signups must close after they
    open (actual or planned) and no later than the start. The draft time has
    no ordering rule.
  - **Published events:** start and end can't be cleared.
  - **Automatic opening on:** a closing time is required.
- **Field guidance:**
  - Each field has a short hint.
  - An overdue start shows a warning: "This start time has passed and the
    event hasn't started. Choose a new time, or start it on Overview."
  - Scheduled starting is described as conditional on readiness, never
    promised.
- **Automatic opening** follows the application's rule:
  - In a private draft, a newly set future opening turns it on.
  - An older schedule with it off stays off when other fields change, and the
    field says so ("Planned only…"). Choosing a new time turns it on, and the
    hint changes as you type.
  - There's no toggle.
- **Save:** one Save for the whole schedule, with the shared saving feedback.
  - **Private draft:** saves directly.
  - **Published event, opening, closing, start or end changed:** a wide
    confirmation shows each changed time before and after, in the event
    timezone with its UTC offset. If the end changes, it also shows the new
    upload deadline.
  - **Draft time only:** no confirmation.
  - **Live end change:** a confirmation with the before and after end and
    upload deadline, plus a required reason (up to 2,000 characters).
  - **Cancelling** any confirmation keeps every entered value, including the
    reason.
- **States:**
  - Loading skeleton, and failed read with Try again.
  - Failure: times are kept.
  - Stale: another admin saved first. Untouched fields take their values;
    fields you both changed keep yours, with "Use theirs".
  - Uncertain: "Check again" reads back and reports "Up to date" without
    claiming this request saved.
  - Not saved.
  - Server rejection: for example, an event window that overlaps another
    public event.
  - Discard and uncertain-leave confirmations on navigation.
  - After a successful save, you stay on Schedule.
- **URLs:** `/admin/events/{slug}/schedule`.
  - Back or Forward closes an open confirmation or picker first.
  - Reload and direct entry go through the prototype URL bar.
- **Samples:** independent scenarios, like Overview's.
  - Autumn: signups open, published.
  - Winter: private, automatic opening on, no closing time.
  - Nordic Night: private, an older schedule with automatic opening off.
  - Community: empty.
  - Clan Cup: signups closed, UTC.
  - Raids Week: draft running.
  - Boss Rush: draft finalized, overdue start.
  - Midsummer: Live.
  - Summer: final review.
  - Easter: cancelled.
  - Overlap is checked against a fixed list of other public events
    (Halloween Bingo 2027), because the samples overlap each other.
- **Prototype controls:** the account menu sets the next save's outcome,
  simulates another admin saving the schedule, makes the next load fail, and
  opens the page directly.

### What the application already supports

- **Rules:** every editability, ordering, future-time, clock-change,
  five-minute, published-clearing and automatic-opening rule above, plus the
  overlap check and the Wise Old Man competition window check.
- **One save:** Schedule saves in one request with the event version.
- **Confirmations:** published consequence changes require confirmation
  (`ConfirmChanges`), and a Live end change requires confirmation and a
  reason.
- **Upload deadline:** the normal upload deadline is re-derived from the end.

### What needs integration or backend work

1. **Stay on Schedule after saving.** The application currently redirects to
   Manage.
2. **Use the shared picker** instead of the separate date and time inputs.
   Keep posting the existing `yyyy-MM-ddTHH:mm` local strings. The server
   stays the authority for clock changes and five-minute steps.
3. **Return field-level errors.** Order, future-time and overlap errors are
   returned as one message today. The reference attaches them to fields
   (overlap stays a form banner).
4. **Uncertain-outcome readback** needs a way to read the event back after a
   timeout.
5. **Confirmation copy and the before/after table** replace today's
   confirmation ladder. The page model already builds `ChangePreview`.

### For Codex to reconcile (not edited)

- `FUNCTIONAL_CONTRACTS.md` §4.4 matrix, "Draft Running/Paused" column: event
  start and end are "Locked". The page model and service allow them (the
  service only locks signup, draft-time and capacity values while the draft
  runs), and the brief keeps them editable.
- `FUNCTIONAL_CONTRACTS.md` §4.4: "a passed boundary cannot be changed or
  cleared", and for a finalized draft, start and end are "Edit while its
  existing boundary remains future". The service lets a pre-Live event repair
  a passed start or end (`mayCorrectPreLiveBoundary`), which the brief
  requires.
- `BingoEvent.ConfigureSchedule` still rejects lowering capacity after signup
  has been public. Schedule passes capacity through unchanged, so it doesn't
  affect this page, but it matches the capacity wording already reported for
  Signup setup.

## Signup setup: reference behaviour and integration notes

Signup setup configures registration settings and the signup form players fill
in. It owns capacity, signup-code protection and form configuration. Schedule
owns dates, Overview owns readiness and opening or closing signups, and
Participants owns registered people; the page links to them and doesn't repeat
their editing.

Verified against the current implementation (the participants-functionality
checkout):

- the Signup form page (`Admin/Events/Questions`)
- the capacity handler on Manage (`OnPostCapacity`) and
  `SignupService.UpdateSignupAdministrationAsync`
- the signup-code handler on Participants (`OnPostSignupCode`)
- `SignupService.ApplyQuestionMutationAsync` and `EnableCoCaptainAsync`
- `BingoEvent` (`SetParticipantCap`, `AcceptsWaitingList`, `ConfigureSignup`)
- signup readiness
- the public signup list
- `PRODUCT_REQUIREMENTS.md` (signup sections)

### What the reference implements

- **Tabs:** Settings and Signup form, as a real tablist.
  - `?tab=form` replaces the current URL, so Back leaves the page rather than
    stepping through tabs.
  - Unsaved edits stay with their tab, and the tab shows a dot while it
    holds them.
  - Leaving the page with unsaved edits asks first.
- **Settings:** two cards, each with its own Save. They are separate
  operations in the application.
  - **Capacity:** "Maximum confirmed players".
    - Validation: at least 1, and never below the confirmed count.
    - Beside the field: confirmed (of capacity), the waiting list, and a link
      to Participants.
    - The consequence is shown before saving:
      - Raising capacity with players waiting: "Saving confirms the first N
        players on the waiting list, in signup order. They're told their
        place is confirmed."
      - The button then reads "Save and confirm N".
      - Raising capacity with nobody waiting: how many places will be open.
      - Lowering capacity: no one loses their place, and new signups join the
        waiting list once it's full.
    - The success toast reports the number the server actually promoted.
    - The waiting list is always on, stated plainly. There is no toggle.
  - **Signup code:** a "Require a signup code" toggle.
    - Once a code is stored, the card says "A code is set". It's never shown
      or copied, because it's stored as a hash.
    - When a code is stored, the field is "New code" and can be left empty to
      keep the current one.
    - Turning protection on without a stored code needs a code.
    - Turning protection off says the stored code is deleted, so a new one
      is needed to turn it on again.
    - Codes are up to 100 characters.
    - The card says signup codes aren't the evidence codes managed on
      Overview.
  - **Save states:**
    - Failed: entries are kept.
    - Uncertain: the button becomes "Check again", which reads the event back
      and shows "Up to date" without claiming this request saved.
    - Not saved: entries are kept.
    - Stale: both cards share the event version. The card shows the latest
      capacity and counts, and keeps the entry.
    - A code readback can't tell which code is stored, so after an uncertain
      replacement the card asks to save the code again.
    - After one card saves, the page re-reads the event. The other card keeps
      its unsaved entry.
- **Signup form:** four sections.
  - **Playing accounts:** the first account, then optional extra fields.
    Playing accounts count towards the event's account and EHB rules.
  - **Alt accounts:** information only, with no EHB, and never on teams,
    evidence or the board.
  - **Captains:** captain volunteer (yes or no, required) and the optional
    co-captain field.
  - **Custom questions.**
  - **Each row** shows the label, answer type and required or optional
    status, then its choices or help text. Once players have signed up it
    also shows answer counts. Only custom questions are numbered and
    reorderable; the section text says the other fields keep their places.
  - **Standard fields** are marked "Always asked" and have no actions: the
    first playing account and captain volunteer.
  - **Account fields:** proportionate actions.
    - Add creates the field in one step with its default label, then opens an
      inline rename with the label selected.
    - Rename edits the label only.
    - Delete… opens a counted confirmation.
  - **Co-captain:** Turn on applies immediately. Turn off… opens a counted
    confirmation.
  - **Custom questions:** Add question and Edit open the drawer. Delete…
    opens a counted confirmation, and up and down buttons move a question one
    step.
  - **Public visibility** is stated where it applies:
    - Playing accounts appear on the public signup list with their EHB.
    - Alt accounts appear there without EHB.
    - Captain volunteer and custom answers are public.
    - Co-captain answers aren't shown there.
  - **After the first response,** a banner says that answer types, choices
    and required settings are locked, that labels and help text can still
    change, that custom questions can still be reordered, and that new
    questions are optional.
- **Question drawer:** the form stays visible behind it.
  - **Fields:** question, help text, answer type, choices and "Players must
    answer".
    - Answer types: Text, Number, Yes or no, Single choice.
    - Choices use a list editor (Enter adds the next) for Single choice.
  - **Validation:**
    - Question: required, up to 300 characters.
    - Help text: up to 1,000 characters.
    - Choices: at least one; unique (ignoring case); blank rows dropped; up
      to 4,000 characters in total.
  - **After the first response:**
    - Editing: the type, choices and required setting are read-only, each
      with its reason. To change a type, delete the question and add a new
      one.
    - Adding: the question is forced optional, with the explanation that
      earlier players won't have an answer.
  - **Outcomes:**
    - Failed: entries are kept.
    - Stale: another admin changed the form. The form behind refreshes, the
      entries are kept, and saving again works.
    - Gone: the question was deleted. Nothing is saved and only Close is
      offered.
    - Draft started: nothing is saved.
    - Players signed up meanwhile: structural settings go back to their
      stored values, and the label and help text are kept.
    - Uncertain: the fields lock and "Check again" reads the form back.
      - Adding: it looks for a new question with the same label. If it finds
        one, it says "A question called X is now on the form. It may have
        come from your request", offers Close, and moves focus to that row's
        Edit button.
      - Otherwise it says the question wasn't added and the entries are kept.
        Retrying can't silently duplicate.
  - **Closing** with unsaved changes asks to discard. The discard dialog
    dims the drawer instead of adding a second backdrop.
- **Counted confirmations:** delete a custom question, delete an account
  field, or turn off co-captain.
  - **Effects:**
    - How many saved answers are removed.
    - For account fields, how many event account registrations are released.
    - That players keep the accounts saved on their profiles.
    - That signups, participant status and other answers stay as they are.
    - That it can't be undone, or for co-captain that it can be turned back
      on but removed answers don't come back.
  - **Changed numbers:** if the counts change before confirming (for example
    a player signs up), nothing happens. The dialog shows the new numbers
    against the old ones and asks to confirm again.
  - **Other outcomes:** gone (already deleted elsewhere), failed, and
    uncertain. After an uncertain result, Check again reports either that
    it's still on the form, with current numbers, or that it's no longer on
    the form.
  - Label edits and reordering never ask for confirmation.
- **Immediate operations:** add an account field, rename, move, turn
  co-captain on.
  - A failure shows a toast and changes nothing.
  - An uncertain result shows a form-level banner with "Check the form". It
    re-reads and reports what the form now shows, never that the request
    succeeded. Add buttons stay disabled until then.
  - A stale move refreshes the order and asks to move again if needed.
- **Read-only:** once the team draft starts, or for a cancelled event,
  everything stays readable with a banner giving the reason ("The team draft
  started on 26 May…"). There are no controls.
- **States:**
  - loading skeleton
  - failed read with Try again
  - empty alt accounts and custom questions
  - unset capacity ("Not set")
- **Keyboard and focus:**
  - Tabs move with arrow keys.
  - The drawer opens on the question field, traps Tab, closes on Escape and
    returns focus to its opener. After adding, focus goes to the new row's
    Edit button.
  - Moves keep focus on the move button, or the opposite one at the ends,
    and announce the new position.
  - Confirmations open on Cancel. After deleting, focus goes to the
    section's add button.
  - Reduced motion uses the shared rules.
- **URLs:**
  - `/admin/events/{slug}/signup` and `?tab=form`.
  - Back or Forward closes an open drawer or confirmation first, and waits
    while a request is in flight.
  - Leaving with an unconfirmed outcome offers "Check again" or "Leave
    anyway".
  - Reload and direct entry work through the prototype URL bar.
- **Samples:**
  - Autumn: signups open, players have signed up, code set.
  - Nordic Night: signups open, nobody yet. "A player signs up now" locks
    formats mid-edit.
  - Winter: setup, every question editable.
  - Community: setup, no capacity, no custom questions, co-captain off.
  - Clan Cup: signups closed, full with 6 waiting, for the promotion
    preview.
  - Midsummer: draft started, read-only.
  - Easter: cancelled, read-only.
  - The account menu sets the next request's outcome and simulates a player
    signing up, another admin raising capacity, adding or deleting a
    question, a failed load and direct entry.

### Existing application functionality

- **Capacity:** saved with the event version. It must be at least 1 and never
  below the confirmed count. A genuine increase promotes waiting players in
  waiting-list order, with per-player notifications, and returns the promoted
  count. The waiting list is always enabled. Read-only once the draft locks or
  the event moves past signups.
- **Signup code:** saved with the event version.
  - Enabling needs a code; an empty field keeps the stored code.
  - Turning off clears the hash.
  - Codes are hashed and up to 100 characters.
  - Saving advances the version, so signup forms that are already open must
    reload.
- **Signup form:**
  - Add custom questions (Text, Number, Yes/no, Single choice).
  - Add Playing or Alt account fields (default labels, always optional).
  - Rename account fields (label only).
  - Edit custom questions: everything before the first response; label and
    help text only after it.
  - Move custom questions one step at a time.
  - Delete with expected answer and release counts plus the question version.
    The server rejects a mismatch and returns the current impact.
  - Turn co-captain on, or off with counts.
  - Standard fields can't be removed or renamed.
  - New questions are optional after the first response.
  - Everything locks when the draft starts.
- **Public signup list:** captain volunteer and active custom answers are
  public. Co-captain isn't.

### Reference-only interactions

- One Signup setup page with two tabs. Today capacity is on Manage, the
  signup code is on Participants, and the form is on its own page or overlay.
- The capacity consequence preview and "Save and confirm N".
- One-step account-field add with inline rename.
- The question drawer and the choice list editor. The application takes
  choices as newline-separated text.
- Answer counts in rows.
- Readback after uncertain outcomes.
- Showing changed impact numbers in place, rather than the raw "Reload this
  question before confirming…" message.
- Edits kept across tabs, plus page-level discard protection.

### What needs integration or backend work

1. **A Signup setup route** that combines the three existing handlers.
   Overview's capacity requirement already links there. Retire capacity from
   Manage and stop carrying it on Schedule.
2. **Return the new event version** from capacity and code saves. They share
   the event version, so the other card needs it to stay current without a
   false stale result.
3. **Version checks** on add, edit, rename, move and co-captain enable. Today
   only delete and disable check counts and version; edits are last write
   wins.
4. **Idempotent adds** for custom questions and account fields (a request
   key), so a retried uncertain request can't create a duplicate. Return the
   new question's id so the page can focus its rename or Edit button.
5. **Adding a required question after the first response:** today the server
   silently stores it as optional. It should reject the request or report the
   change. The reference shows a message and sets it to optional.
6. **Answer counts per question** for the rows. The Signup form page already
   loads them for its delete impacts.
7. **Text label:** the application calls Text "Short text answer", while the
   public form shows a two-row text area. Use one name for the single
   multiline Text type.

### For Codex to reconcile (not edited)

- `PRODUCT_REQUIREMENTS.md:1387` says that "once signup has first been
  public, the cap may only increase", and that waiting-list support is
  "editable… but cannot be disabled while participants are waiting". The code
  and the agreed brief allow lowering capacity to the confirmed count, and
  keep the waiting list always on.
- `UI_PAGE_MATRIX.md:31` still gives capacity to Schedule (reported earlier).
- `Questions.cshtml.cs` `FormatType` shows "Short text answer" for the
  multiline Text type.

## Teams / Draft: reference behaviour and integration notes

Teams / Draft is one workspace that changes with the stage:

- **Setup:** teams and captains. Teams and their members are the main content.
- **Running:** an admin-run snake draft. The admin streams the board, captains
  call their picks, and the admin clicks them in.
- **Finalized:** the published rosters, with the corrections allowed before the
  event first goes Live.

The URL is `/admin/events/{slug}/teams` in every stage.

Verified against the current implementation (the participants-functionality
checkout):

- `Admin/Events/Draft` and its page model: start, scramble, pick, undo,
  cancel, finalize, control, teams, members, roles and finalized corrections.
- `SnakeDraftOrder` (`GetTurn`, `GetNextEligibleTurn`,
  `ProjectFinalRosterSizes`) and `DraftRosterDistribution`.
- `TeamCaptainAuthorityService.ChangeRoleAsync`.
- `AdminCollaborationHub` (`WatchDraft`, `RenewDraftControl`) and
  `DraftControlLease`.
- The PRODUCT_REQUIREMENTS simplification (TEM-01, DRF-01).

### What the reference implements

- **Rules ported from the application:**
  - `IncludedInDraft` alone decides whether a team is drafted.
  - The draft pool is every confirmed participant who isn't on a
    manual-roster team. Preassigned members of drafted teams count toward
    their team's size.
  - Sizes are derived from players and drafted teams: everyone gets the
    smaller size and the remainder get one more. There is no team-count or
    target-size setting.
  - Turns use the application's `GetNextEligibleTurn`, ported line for line.
    A team with more preassigned members than the smallest roster is skipped
    until the others catch up, and a full team is skipped.
  - Final sizes ("4 / 12") and the pick total in the turn strip come from
    `ProjectFinalRosterSizes`.
- **Setup** (two or more drafted teams):
  - **Readiness card**, short and actionable:
    - Signups closed, with a link to Overview.
    - Every drafted team has a Captain, with Assign.
    - Team sizes work out (the distribution validation).
    - The size summary, for example "44 players across 4 drafted teams:
      4 teams of 11", noting manual-roster teams that aren't drafted.
  - **Start draft…** stays unavailable until every check passes.
  - **Team cards** show the name, "Website draft" or "Manual roster", the size
    (with the final size for drafted teams), the EHB total, members with role
    badges (C, CC) and EHB, and a warning line (no captain, or too many
    preassigned).
  - **Footer actions:** Assign captain (while a team has none), and
    Preassign player or Add member.
  - **Team menu:** Edit team… (name of up to 30 characters, unique, and
    "Takes part in the website draft"), Add captain… (always; a team can
    have several Captains, as the application allows), and Remove team…
    with a confirmation.
  - **Member menu:** Make captain, Make co-captain, Make participant, Move to
    another manual-roster team, and Remove from team.
- **Participant selector:** a drawer that searches by primary account and
  shows EHB and a "Captain volunteer" chip.
  - Assigning a captain lists volunteers first. Members of other teams are
    shown but disabled.
  - Adding a member asks for the role (Participant, Captain, Co-captain).
    Volunteering never assigns a role, and Captain and Co-captain stay
    separate.
- **Manual rosters** (zero or one drafted team):
  - The readiness card covers closed signups and at least one member per team.
    Confirmed players who aren't on a team are reported as a non-blocking
    note ("4 confirmed players aren't on a team…").
  - **Finalize rosters…** publishes directly, with no draft run and no
    fabricated picks. When players are unplaced, the confirmation says how
    many, and that they can be added afterwards as corrections.
- **Starting:** Start draft… confirms the team sizes and what locks, then starts
  the draft and takes control. Drawing the order is a separate step: **Draw
  order** in the turn strip, and Redraw in the draft menu until the first pick
  is recorded (`FirstPickRecordedAt`, which persists even if picks are undone).
- **Drawing the order:**
  - While the request runs, the columns wait-shuffle in place, positions read
    "–" and the turn strip says "Shuffling the teams…". The shuffle shows for
    at least `--dk-dur-shuffle-min`, so a fast draw still reads.
  - When the server confirms, each column travels to its confirmed position
    with a small lift, staggered left to right, and the new positions
    appear. Only then does the turn highlight return.
  - The animation never decides the order: it starts from what's on screen
    and ends on the confirmed result.
  - Draw, Redraw, picks and Undo stay unavailable from the click until the
    columns have settled.
  - A failed draw stops the shuffle with the old order unchanged and an error
    toast.
  - An uncertain draw keeps the old order behind Check again. A readback, or
    a draw by another admin arriving live, plays the same move.
  - With reduced motion, the columns switch at once with no shuffle.
- **Running** (compact, for streaming):
  - **Layout:**
    - The sidebar collapses automatically. Toggling it during the draft is
      remembered until the draft ends.
    - There is no page header, only a visually hidden h1.
    - **Turn strip:** pick number and round, the team picking now (its name
      fades in on each change), the next team ("again" on a snake turn),
      Undo #n, the control chip and a draft menu.
  - **Board:** one column per team in draw order.
    - The head shows draft position, size against the final size, and the EHB
      total.
    - Columns list only actual members: preassigned members ("Pre", Captains
      first), then picks with their number. They start compact and grow as
      players are picked, with no space reserved for the final roster, so
      the player pool has the most room early and the teams take more as it
      shrinks. The head keeps the size against the final size ("4 / 12").
    - The team picking now is highlighted. In narrow columns (large events),
      size and EHB move under the name and member EHB hides.
  - **Available players:** name (primary account) and EHB.
    - Sorted by EHB or name.
    - Search with `/`. Enter in search drafts a single match, and Escape clears
      it.
    - One tab stop with arrow keys, Home and End.
  - **Picking:**
    - A click (or Enter) drafts the player straight away. There is no
      selection step or confirmation.
    - While the request runs, the chip and a row in the team show a spinner,
      and the whole pool is inert so nothing can be sent twice.
    - A confirmed pick appears with the shared row entrance and flash, and
      focus moves to the next chip.
    - Undo removes the latest active pick only.
    - Picks and Undo use the short draft presentation minimum (see
      Interaction contracts). In the prototype, a pick's spinner shows for
      about 0.3s, down from 0.65s.
  - **Draft menu:** Draw or Redraw order, Preassign a player…, Finalize
    draft… (once every player is placed) and Cancel draft….
  - **Preassign a player…** is available until the first pick is recorded,
    including after Start and before or after drawing the order, matching
    `AddMember`'s eligibility: a drafted team, the first pick not yet
    recorded, before the event starts, a confirmed participant who isn't on
    a team. It opens the participant selector with a team choice and a
    role, and notes that preassigning counts toward the team's size and can
    change whose turn is next. After the first pick it's shown as
    unavailable, with the reason.
  - **Fit,** checked at the beginning, middle and end of each draft with no
    page scrolling at either size and no smaller text:
    - 5 × 50 at 1440×900: the teams take 96px, 226px and 330px; the pool
      612px, 482px and 378px.
    - 8 × 96 at 1440×900: at the very start, all 88 players are in view
      (the pool scrolls 4px inside its card). From pick 23 to the end, the
      board takes 191px, 295px and then 414px, and the pool 517px, 413px and
      then 293px.
    - At 1920×1080 every stage fits, including all 88 players at the start
      of 8 × 96.
- **Control** (one admin at a time):
  - The chip reads "You have control" with Release, "Another admin has
    control" with Take over…, or "No one has control" with Take control.
  - The main workspace says "Another admin" and never names them; the take
    over confirmation names them.
  - Losing control (a takeover, or a pick refused because someone else holds
    control) shows a banner, and the pool goes inert with the reason. A
    lapsed lease has its own banner.
  - Picks by the controlling admin arrive live on everyone's board.
- **Cancel draft…** is in the draft menu, away from picking.
  - With active picks, its dialog explains that cancelling never removes picks
    and that they must be undone one at a time first. There is no confirm
    button.
  - With none, it returns to setup, keeps teams, captains and preassigned
    members, says whether the drawn order is kept, and releases control.
- **Finalize:**
  - The confirmation says it publishes rosters and draft results (or only
    rosters, for manual rosters), doesn't publish the board or start the
    event, allows individual corrections until the first Live, and can't be
    reopened.
  - Finalized rosters list drafted teams in draw order.
  - After finalizing you stay on Teams / Draft. The success toast and the
    page header both offer **Open Board**, and the header keeps it while the
    rosters are published.
- **Corrections** (finalized, before the first Live):
  - A banner explains the limits.
  - Add member opens the selector for a signed-up player, or "Other website
    accounts" (playing account and EHB). Adds are Participant only, saved
    with "Add and republish".
  - Remove asks first: the team's new size, that the original pick stays in
    history, that a Captain's removal leaves the team without one, and that
    moving a player means adding them on the other team afterwards.
  - Role changes and renames are available.
  - Tags show the pick number, Pre, or Added.
  - The toast reports republishing and the Wise Old Man update.
- **Live:** membership is locked. There are no add or remove actions, and a
  banner explains that Captain and Co-captain roles can still change.
  - Member menus offer only Make captain, Make co-captain and Make
    participant.
  - The team menu offers Add captain… for a current member (other players
    are shown but disabled).
  - Role changes follow `ChangeRoleAsync`, which allows them until the
    submission window closes.
- **States:**
  - Loading skeleton, and a failed load with Try again.
  - Empty event (no teams).
  - Every change has saving, failed (toast or in-dialog banner) and
    concurrent-change states. In a dialog, "another admin changed this team
    first" reloads the roster.
  - **Uncertain:** the view isn't updated. A banner with Check again blocks
    repeat actions until a readback reports what happened.
  - **Navigation:** leaving while a change is saving waits. Leaving while one
    is uncertain asks first ("Check again" or "Leave anyway"). Back or Forward
    closes an open dialog, drawer or menu first, and a typed team name or a
    chosen player asks before being discarded.
- **Removed from this page:** the Participants-style table, affiliation/clan
  and team-image controls, payment and signup-time columns, and withdrawal
  actions. Stored affiliation and image values aren't touched. The reference
  simply doesn't show them.
- **Samples:**
  - Raids Week: mid-draft, 4 × 44, with one manual-roster team.
  - Nordic Night: running, 5 × 50, order not drawn.
  - Solstice Mega Bingo: running, 8 × 96.
  - Clan Cup: setup, one team without a captain.
  - Autumn: signups open, so Start is blocked.
  - Clan vs Clan Showdown: manual rosters only.
  - Boss Rush: finalized, with an earlier correction.
  - Midsummer: Live, locked.
  - Community: no teams.
- **Prototype controls:** in the account menu.
  - Next change: succeeds, fails, times out (went through or not), or another
    admin acts first.
  - Another admin takes control, the controlling admin makes a pick, my
    control lapses, another admin edits a team.
  - Fail next load, open this page directly, and show the browser bar.

### What the application already supports

- **Lifecycle and turns:** every rule above, including the snake turn order,
  derived sizes and distribution validation.
- **Start and order:** start requirements, and order changes only before the
  first pick.
- **Undo and cancel:** latest-pick Undo, and Cancel only with zero active
  picks.
- **Finalize:** direct finalization for 0–1 drafted teams, and publication
  cycles.
- **Corrections:** finalized Add (participant or website account) and Remove
  with expected versions, and role changes that republish.
- **Control and live updates:** the control lease with takeover and release,
  hub renewal while the page is open, and live notifications.

### What needs integration or backend work

1. **Integrate the existing in-place updates rather than replacing them.**
   - Pick, Undo and Scramble already post as enhanced partial updates
     (`data-update-targets="#app-notice-region, .draft-live-shell"`, applied
     by `site.js`, which raises `bingo:content-updated`).
   - Other admins' changes already arrive through the hub's `draftChanged`
     notification (`admin-collaboration.js`, via `WatchDraft`), which reloads
     the board.
   - **Pending and confirmed presentation:** add the reference's pending
     chip and row, the inert pool and the short presentation minimum
     (`busyMinQuick`) around those posts.
   - **Scramble animation:** today `draft-scramble.js` animates after the
     saved order arrives by passing cards through several random
     intermediate orders with temporary rank numbers. The reference instead
     wait-shuffles in place while the request runs (no ranks shown), then
     moves each team once from its old position to the confirmed one. Adapt
     the existing hook to that (`DKAdmin.reorder`, the reorder tokens).
2. **Uncertain-outcome readback.** A timed-out pick, undo, draw or change
   needs a way to read the draft back, without retrying blindly.
3. **One page for all stages.** Remove the Participants table, the
   affiliation and image inputs, the payment and signup columns and the
   withdrawal forms from the Draft page. Keep the stored data as it is.
4. **Participant selector data.** The selector needs search results with
   primary account, EHB, captain-volunteer status and current team, plus a
   team choice for Preassign a player…. Today's forms use plain selects.
5. **Readiness summary.** Expose the size summary, the distribution
   validation messages and the unplaced-player count for the card and the
   manual finalize confirmation.
6. **Stay after finalizing.** The application redirects to Board when an
   approved board is ready. The reference stays on Teams / Draft and offers
   Open Board, as decided.
7. **Roles while Live on this page.** `ChangeRoleAsync` already allows it.
   The Draft page needs to offer the role actions while Live, without
   membership actions.

### Decisions applied

- **Several Captains:** the application's allowance is kept, with no
  one-Captain limit. Add captain… is always in the team menu, and the member
  menu can make anyone Captain.
- **Roles while Live:** Captain, Co-captain and Participant role changes stay
  available. Membership stays locked.
- **Preassigning before the first pick:** available from setup and, once the
  draft has started, from the draft menu's Preassign a player…, with the
  application's exact eligibility.
- **Admin names:** they never appear in the main workspace. The take over
  confirmation names the other admin.
- **Unplaced players with manual rosters:** they're reported clearly and
  included in the finalize confirmation. Finalizing isn't blocked.
- **After finalizing:** stay on Teams / Draft, with Open Board.
- **Control expiry:** control lapses when the page can't renew it for 5
  minutes (for example after a lost connection), not after 5 minutes without
  a pick.

## Board: reference behaviour and integration notes

Board is where admins build an event's bingo board, set each tile's
objectives, balance estimated effort, preview what players will see, and take
the board through approval, publication and corrections. The board is the main
workspace. Detailed editing happens in one drawer that opens beside it, so the
page isn't permanently filled with controls. Catalogue owns bosses,
activities, drops, rates and GP values; Board only links to it.

The URL is `/admin/events/{slug}/board`. An open tile adds `?tile=B3` (row
letter and column number), so a tile can be linked directly.

Verified against the current implementation (the participants-functionality
checkout):

- `Admin/Events/Board` and its page model: create, edit, move, resize, team
  size, approve, unapprove, publish, start correction, discard correction,
  remove, editing control and editor data.
- `_BoardRequirementEditor`, `BoardEstimateService`, `EhbCalculator` and
  `TileDescriptionFormatter`.
- `FUNCTIONAL_CONTRACTS.md` 6.2 and 6.3.
- `PRODUCT_REQUIREMENTS.md` sections 8, 10 and 11, and BRD-01/BRD-02.

### What the reference implements

- **Header:**
  - Title with a status badge: Draft · private, Approved · private,
    Published, or Correction · private.
  - Size (with Resize) and how many positions are filled, plus a one-line
    state note: when the board was approved or published, and what changes
    return it to draft.
  - Actions: the editing-control chip, Preview, one primary action (Approve
    board, Publish board…, or Publish correction…), and a menu for the less
    common actions (Board size…, Return to draft, Correct published board…,
    Discard correction…).
- **Board grid:**
  - Tiles show the name, effective EHB and small flags: optional artwork,
    "Manual" when an EHB override is in use, "2 parts" for several
    objectives, a clipboard for manual challenges, a lock when evidence was
    submitted, a warning when there's no estimate, and a dot for tiles
    changed in an open correction.
  - Empty positions read "Add tile" (or "Empty" in view mode).
  - Row totals sit at the end of each row and column totals below. Complete
    lines 25% or more from the average line are marked ↑ or ↓ with the
    percentage; incomplete lines are muted.
- **Moving:**
  - Drag a tile onto another position, or use the keyboard: arrow keys move
    between positions, M picks a tile up, the arrow keys choose a target,
    Enter places it and Escape cancels. A bar explains the mode.
  - An empty target is a move and an occupied one is a swap. Tiles travel to
    their new places (`DKAdmin.reorder`; instant with reduced motion).
  - Enter edits or adds; Delete removes, with a confirmation.
- **Resize:** a small dialog with steppers from 1 to 8.
  - It describes the result as you go ("Adds 11 empty positions. Every tile
    keeps its row and column.").
  - Shrinking past occupied positions is blocked, with the tiles to move or
    remove named.
  - On an approved board it says the board returns to draft.
- **Planning:** the total EHB on the board, players per team (editable, and
  still editable after rosters are finalized), EHB per player, per player each
  day (with the event length) and line spread (absolute and percentage).
  - "More figures" holds tiles filled, average tile, lowest and highest
    line, tiles without an estimate and event length.
  - The per-team EHB breakdown is removed.
- **Tile editor** (one drawer for creating, editing and viewing):
  - **Tile:**
    - Name, which falls back to the suggested source names.
    - Description: the automatic text (ported from
      `TileDescriptionFormatter`) with "Write your own"; the override offers
      "Use automatic description" to return to it.
    - Artwork: upload, replace and remove (PNG, JPEG or WebP).
  - **Objectives:**
    - The kind is chosen once per tile: Collect drops or Manual challenge.
      Switching keeps what you entered for the other kind until you save.
    - A single objective has no card chrome. Several objectives become
      numbered, collapsible cards headed by their own description and
      estimate, with "All 2 must be completed".
    - **Drops:**
      - Bosses or activities: a searchable multi-select, shown as chips.
      - Eligible drops grouped by source. Sources that already have chosen
        drops show only those, with "Show all"; new sources show every drop.
        Each row shows its rate or "No rate".
      - "Collect [n] of the chosen drops", then a one-line counting summary
        with Change. Change reveals "The same drop can count more than once"
        and "Some drops count as more than one". The second adds a "counts
        as" field beside each chosen drop.
    - **Manual challenge:** "What must be completed?" and "Complete [n]
      times". Evidence and approval are still required.
  - **Estimate:**
    - Drop tiles show the calculated EHB, with plain reasons when it can't be
      calculated (a missing catalogue rate links to Catalogue; a target that
      can't be reached without repeats).
    - "Set EHB manually" adds an override that replaces the effective EHB
      for this event's tile only. The calculated value stays visible (struck
      through), and "Use calculated" returns to it.
    - Manual challenges require an EHB estimate.
  - **Validation** happens on save and focuses the first problem: target
    1–10,000, at least one source and drop, weights 1–10,000, manual text,
    EHB above 0 up to 100,000, and a description of up to 4,000 characters.
  - **Locks:**
    - Evidence-locked objectives show the lock reason in place, and their
      sources, drops, target and counting can't change. The kind, the
      override and adding or removing objectives are locked for that tile,
      and Remove tile is replaced by "Can't be removed: evidence was
      submitted".
    - Name, description and artwork stay editable.
  - **Footer:** Remove tile…, the unsaved-changes indicator, Cancel and Save.
    Ctrl/⌘+Enter saves.
  - **View mode** (someone else editing, or a published board): the same
    drawer, read-only.
- **Approval and publication:**
  - **Approve board** runs without a confirmation and validates the whole
    board. Failures appear as one list with links that open each tile:
    empty positions, a drop tile without automatic EHB (the missing rate is
    named), a manual tile without EHB, and drops without a catalogue GP
    value.
  - **Approved:** any tile, position or size change returns the board to
    draft. The editor says so before saving and the toast confirms it.
    Return to draft is in the menu.
  - **Publish board…** needs finalized rosters, the event not yet started
    and its end still in the future. When blocked, the button explains why
    and a banner links to Teams / Draft. One confirmation says publishing
    doesn't start the event.
  - **Published:** read-only, with Correct published board… in the menu.
- **Corrections:**
  - Correct published board… needs a reason (up to 2,000 characters).
    There's no separate checkbox; the dialog is the confirmation.
  - During a correction, a banner shows the reason and the evidence rule, and
    changed tiles carry a dot.
  - **Publish correction…** validates the corrected copy and replaces the
    public board. The confirmation counts the changed tiles and repeats the
    reason.
  - **Discard correction…** restores the working copy from the published
    board, without touching results or evidence.
- **Preview:** a player-facing view (names, descriptions, artwork; no admin
  controls) labelled with what it shows: the current draft, the frozen
  approved version, what players see now, or your private correction.
  Opening it changes nothing.
- **Editing control:**
  - The chip reads "You're editing" (Stop), "Mia Kowalski is editing" (Take
    over…, with a confirmation) or "Viewing" (Start editing).
  - Losing control while the editor is open keeps your entries in a
    read-only drawer with "Take over…". Taking back control makes them
    editable again.
- **States:**
  - Loading skeleton and failed load with Try again; a failed editor load
    inside the drawer with Try again.
  - Empty and partly built boards.
  - Saving uses the shared minimum; moves, team size and editing control use
    the short one.
  - Failures are reported in the dialog or a toast.
  - "Another admin changed the board first" keeps your entries and loads the
    latest board.
  - Uncertain outcomes wait for Check again.
  - Navigation: unsaved tile changes ask before discarding, and an uncertain
    change asks before leaving. Back or Forward closes the top dialog,
    drawer, menu or move first.
  - Direct entry with `?tile=` opens that tile after loading.
- **Samples:**
  - Raids Week: draft, 18 of 25.
  - Autumn: empty.
  - Clan Cup: full, but approval fails on a missing rate and a missing GP
    value.
  - Nordic Night: approved, rosters not finalized.
  - Boss Rush: approved, ready to publish.
  - Midsummer: published and Live, with evidence.
  - Summer Bingo: an open correction.
- **Prototype controls:** in the account menu.
  - Next change outcome.
  - Mia takes over editing, or edits the board.
  - Catalogue adds the missing rates; evidence arrives for a tile.
  - Fail the next load or the next editor load, open the page or tile B3
    directly, and show the browser bar.

### What the application already supports

- **Board and layout:** dimensions 1–8, create, move or swap, resize that
  blocks shrinking past occupied positions, and tile removal blocked by
  evidence.
- **Tiles:** homogeneous objective kinds, drop objectives with sources, drops,
  target, repeats and weights, manual objectives, automatic descriptions with
  overrides, artwork upload, replace and remove, and the EHB calculator.
- **Workflow:** approval validation and snapshot, invalidation by competitive
  edits, unapprove, publish with confirmation and readiness, correction with
  reason and confirmation, publishing a corrected replacement, and discarding
  a correction.
- **Collaboration:** the editing lease with takeover and release, presence, and
  live board notifications.
- **Stored caps:** maximum-contribution caps are kept in data but have no
  editor control. None is added here.

### Approved changes the backend doesn't support yet

- **Manual EHB override on drop tiles.** `CreateTile`/`EditTile` currently
  reject a manual estimate on catalogue tiles ("Catalogue tiles use automatic
  EHB"). The approved change lets any tile carry an override that replaces its
  effective EHB for board balancing, credited contribution statistics and the
  ranking tie-break. It belongs to the event's tile only; catalogue rates and
  other events are untouched. Proportional credit for partial progress stays.
- **Players per team after finalization.** `OnPostTeamSize` refuses once a
  roster is published ("The finalized roster determines team size"), and
  the statistics use roster sizes. The approved change keeps the planning
  estimate editable and uses it for the planning figures.
- **Ranking.** Credited EHB comes before current-score completion time after
  equal completed lines and tiles, with full-board completion still first. No
  Board UI is needed; `PRODUCT_REQUIREMENTS.md` 8.2 still lists the old
  order.

### What needs integration

1. **Editor and actions without full reloads.**
   - The current editor posts and redirects. The reference saves in place
     with the shared saving feedback and keeps entries on failure, conflict
     or loss of control.
   - The page already loads editor data on demand (`OnGetEditorData`);
     opening the drawer should use it.
2. **Keyboard moving and `?tile=` routes.** Today moving is drag-only, and
   tile details open in a dialog with no URL.
3. **Line deviation markers.** The application shows the line spread only;
   the ±25% markers need the line totals plus a threshold. The threshold is
   a reference choice; confirm it.
4. **Approval failure list.** Approval currently returns one message. The
   reference lists every problem with a link to the tile, which needs the
   validation to report all issues.
5. **Changed-tile markers during corrections**, from comparing the working
   copy with the active published snapshot.
6. **Uncertain-outcome readback** after a timeout.
7. **Remove** the per-team EHB breakdown and the permanent explanation
   panels.

### Decisions to confirm

- **Override vs missing rates.** The contracts say a manual estimate can't
  repair a broken standard tile. The reference allows an override on any drop
  tile but still blocks approval when the calculation itself fails (missing
  rate), so catalogue data must still be fixed. Confirm, or allow the override
  to stand in.
- **Evidence and the override.** Once evidence exists for any objective in a
  tile, the override is locked, as the tile's EHB is part of its scoring (the
  application already blocks changing `ManualEhbOverride` then).
- **Artwork on evidence-locked tiles** stays editable as presentation. The
  contracts only name title and description; confirm.
- **Optional tie-break EHB value** (PRODUCT_REQUIREMENTS 10.1) has no editor
  field in the current application or here. The effective EHB serves the
  tie-break.

## Audit: reference behaviour and integration notes

Audit is the read-only history of administrative actions. It answers four
questions: what happened, who did it and when, which event or record was
affected, and what changed (with the reason, if one was recorded). It is a
focused history page: a table for browsing, a drawer for inspecting one entry,
and no statistics or dashboard. History is immutable, so the page has no
editing, deleting, Undo, export or bulk actions, and no save feedback.

The URL is `/admin/audit`. Filters, the page and an open entry live in the
query: `?event={slug}&action={key or area}&actor={text}&type={record
type}&from={yyyy-mm-dd}&to={yyyy-mm-dd}&page={n}&entry={id}`. Defaults are left
out, so the unfiltered first page is just `/admin/audit`.

Verified against the current implementation (the participants-functionality
checkout):

- `Admin/Audit/Index` and its page model: the filters, ordering and
  pagination.
- `Shared/_AuditEntry` (also used by the Dashboard's recent activity).
- `AuditPresenter` (labels, reasons, changes, safe technical details) and
  `DateTimePresentation` (default zone Europe/Copenhagen).
- `FUNCTIONAL_CONTRACTS.md` 9.3.

### What the reference implements

- **Header:** "Audit", "Administrative history, newest first." and the time
  zone: "Times in Copenhagen time (UTC+02:00)". The offset follows summer and
  winter time.
- **Filters**, by how often they're used:
  - In the toolbar: an actor search (applies on Enter or when leaving the
    field, up to 100 characters, a leading `@` is ignored), an **Event**
    menu listing events by name, and an **Action** menu with All actions, the
    seven areas (Accounts, Events, Catalogue, Board, Teams, Draft, Evidence)
    and "Specific action…".
  - In **More filters** (a small anchored panel): a specific action (grouped
    by area, by readable label), the record type, and a date range with
    Today / Last 7 days / Last 30 days presets and From / To dates. Both days
    are included and are Copenhagen dates. Validation: "Enter dates like 27
    May 2027." and "The start date must be on or before the end date.";
    focus goes to the first wrong field. Reset these clears the panel's
    fields; Apply applies them. A count on the button shows how many panel
    filters are active.
  - Every active filter appears as a removable chip, with Clear all.
  - Any filter change returns to page 1. Paging keeps the filters.
- **Table** (newest first, scrolls sideways when narrow, with the When column
  pinned):
  - When: date, with the time below.
  - Action: the readable label (never the internal key), an Automated pill for
    system actions, and the record below (type and name, e.g. "Board · Raids
    Week 2027").
  - Event: the event's name, or "No event" for community-wide or older
    entries.
  - Actor: `@username`, or System with a gear icon.
  - Recorded: small pills for what the entry holds: "2 changes", "Added",
    "Deleted", "Reason". A dash when there's nothing more.
  - The whole row and the action label open the entry.
- **Pagination:** 25 per page. Newer / Page N / Older. Older is enabled only
  when the server says a next page exists. No totals or page counts are shown,
  because the application doesn't supply them. The footer reads "Showing 25
  entries" (plus "matching your filters" when filtered).
- **Drawer** (read-only, 640px wide):
  - Head: the date, the action label, and badges for the record type and
    Automated.
  - Summary: When (full date and time to the second with the UTC offset),
    Actor, Event and Record.
  - **Reason and context:** the recorded reason as a quote and any lifecycle
    explanation (for example "Publication cycle 2." or "Until 25 May 2027
    20:00."). The section is left out when there's neither.
  - **Changes** (the main content): field label, previous value and new value.
    Empty values show as a dash. Long values are clamped to four lines with
    Show full values. In a narrow drawer it stacks to labelled Before / After
    lines. Entries without changes say why: a record was added, a record was
    deleted, details are withheld (codes, passwords, credentials), or no field
    changes were recorded.
  - **Technical details** (collapsed): the action key, the target identity,
    and the sanitised details, before and after values, as the presenter
    already prepares them. Unreadable historic details show "Historic details
    could not be interpreted."; sensitive actions show "Sensitive details
    withheld" and nothing else.
  - Footer: Newer / Older step through the current page; Close.
- **States:** loading skeleton rows (the previous rows are not kept, as the
  query may have changed). Load failure with "Your filters are kept" and Try
  again (focus moves to it). "No history yet" when there is nothing at all,
  "No entries match these filters" with Clear filters when filters exclude
  everything, and "No entries on this page" with Go to the first page for a
  stale page link. "This entry isn't available" for an entry link that can't
  be shown. A notice when a link holds filters that aren't recognised. Hover,
  focus and selected rows use the shared table states; the drawer and panel
  use the shared motion, and reduced motion removes it.
- **Samples:** about 50 visible entries from January to 2 June 2027 across
  seven events: ordinary changes, automated starts, price refreshes and
  account creation, reasons (structured and plain), a long description change,
  a sensitive signup-code change, draft and submission reopenings with their
  explanations, a legacy "Board updated" with only a note, an unknown older
  action with malformed details, and old entries with no event or snapshot.
  Two entries for the hidden Easter Mini Bingo exist in the data but are
  excluded, as the application does (see Known discrepancy).
- **Prototype controls** (account menu): fail the next load, empty history,
  open an entry directly, open a filtered link, open a link with a bad
  filter, and show the browser bar.

### Routing: prototype and application

The artboard simulates the browser with an in-memory history and the
prototype URL bar. In the application these are real URLs and real history
entries.

- **Filters and pages** push a history entry, so Back returns to the previous
  filters or page and the list reloads. A link opens with its filters
  applied. Unknown or invalid values (an unknown event or record type, an
  impossible date, a start after the end, an invalid page) are dropped and the
  URL is replaced, with a notice.
- **Opening an entry** pushes `entry={id}` on top of the current filters and
  page. Back closes the drawer; Forward reopens it. Close and Escape step back
  over that entry when it's the previous one, otherwise replace the URL
  without `entry`. Newer / Older replace the URL, so they don't fill the
  history. Focus returns to the row's action.
- **Direct entry:** `/admin/audit?entry={id}` opens the first page with the
  drawer over it; the entry is read by id even when it's not on that page.
  An entry that doesn't exist, or that the query would exclude, shows "This
  entry isn't available".
- The browser's own Back always closes an open menu or the filter panel
  first.

### What the application already supports

- Filters for event (id), actor, action, entity type and From / To, with
  Filter and Clear.
- Newest first by occurrence time, then id.
- 25 per page with previous / next (it fetches 26 to know whether there's a
  next page).
- `AuditPresenter`: readable action and target labels with a fallback
  ("Recorded administrative action"), structured and plain reasons, the
  draft-reopening and submission-reopening explanations, "Added" / "Deleted"
  lifecycle, field changes with readable labels and sensitive keys dropped,
  withheld details for sensitive actions, malformed-details handling, and the
  target identity line.
- Dates in the default zone (Europe/Copenhagen).

### What needs integration

1. **Event lookup instead of an Event ID field.** The menu needs the list of
   events (id, name and, for display, the lifecycle status), ordered as on
   Events. Hidden events are left out today (see Known discrepancy). The URL
   uses the slug in the reference; the application may keep the id, as long
   as the control shows names.
2. **Area and specific action filters.** The current `Action` filter is a
   substring match on the stored key. Area filters (`event.`) work with that
   as prefixes, but a specific action such as `event.started` also matches
   `event.started_automatically`. A specific action needs an exact match (or
   prefix and exact as separate parameters). The specific-action list needs
   the presenter's labels exposed.
3. **Labels for keys the presenter doesn't name yet.** The reference labels
   `board.approved`, `board.resized`, `board.published_correction_started`,
   `board.published_corrected`, `team.membership_role_changed` and
   `board.expected_team_size_changed`. Today they show "Recorded
   administrative action".
4. **Actor matching.** The reference matches case-insensitively and ignores
   a leading `@`. `Contains` on PostgreSQL is case-sensitive unless the query
   lowers both sides or uses `ILIKE`.
5. **Date bounds.** The reference treats From / To as whole Copenhagen days,
   both included. The page model binds `DateTimeOffset`s; the bounds should
   be computed from local dates in the display zone (end exclusive at the next
   local midnight).
6. **Single-entry read** for `?entry=` when the entry isn't on the loaded
   page, applying the same visibility rules as the list.
7. **URL state:** filters, page and entry as query parameters with
   validation, and the Back / Forward behaviour above.
8. **Change labels.** The reference sentence-cases split names ("Team role",
   "Completions per hour"). `Humanize` currently splits camelCase without
   changing case.
9. **Record names.** The presenter takes a record's name from a `Name` field
   in the before / after values. Many entries (boards, evidence, memberships,
   drafts) have none, so today they show just the type ("Board"). The
   reference shows names for them ("Board · Raids Week 2027", "Evidence ·
   Rune Lyra · Vorkath heads"), which needs either the name recorded with the
   entry or a lookup by target id. Without it, the table falls back to the
   type and the event column still identifies the event.

### Known discrepancy (for application reconciliation)

`FUNCTIONAL_CONTRACTS.md` 9.3 says hiding an event is not an audit secrecy
boundary. The current query excludes entries whose event is hidden
(`HiddenAt != null`), so their history (including the hide itself) disappears
from Audit. The reference reproduces the application as it is: the Easter Mini
Bingo entries are in the sample data but excluded from the list, from the
event menu and from direct entry links. It does not choose a fix. Either the
query or the contract should change; if hidden events stay visible here, the
event menu would need to include them (marked Hidden).

### Decisions to confirm

- **Case-insensitive actor search** and the leading `@` (item 4 above).
- **Area filters in the toolbar** with specific actions in the panel. This
  relies on the prefix behaviour already present.
- **Date presets** (Today, Last 7 days, Last 30 days) count days in
  Copenhagen time.
- **Long values** clamp at four lines in the drawer; the full value is one
  click away, and the raw values remain in Technical details.

## Review: reference behaviour and integration notes

Review is where admins check submitted evidence and decide on it. The task is
comparison: a readable screenshot beside exactly what it has to show, then a
decision. Final review (event-wide completion and official results) is a
separate page; Review has no finalization controls or readiness summary.

Two routes, both for the selected event:

- Queue: `/admin/events/{slug}/review?q={search}&status={status}`.
- Submission: `/admin/events/{slug}/review/{id}?q=…&status=…`. The queue's
  search and status travel with the submission so returning keeps them.

Verified against the current implementation (the participants-functionality
checkout):

- `Admin/Review/Index` and `Details` with their page models, and
  `admin-review-queue.js`.
- `SubmissionService` (create, approve, reject, reverse, correct, rebalance,
  concurrency) and `EvidenceAuthority`.
- `public-evidence.js` (zoom and pan in the current lightbox).
- `FUNCTIONAL_CONTRACTS.md` 2.4 and 7.4–7.5; `PRODUCT_REQUIREMENTS.md` 12–14.

### What the reference implements

- **Queue:**
  - Header: "Review", how many are pending, the event state ("Live · ends …",
    or "Ended … · uploads closed, review continues"), and "Times in Copenhagen
    time".
  - Search across team, credited account and tile, live as you type. Status as
    a segmented filter with counts (All, Pending, Approved, Rejected,
    Withdrawn, Reversed); a status menu on phones. Both replace the URL, as
    the application does. The event comes from the event selector only.
  - Pending first, newest first within Pending and within the rest.
  - Columns: Uploaded (Copenhagen time, UTC below), Team, Account, Tile (with
    the drop and boss, or "Manual objective"), Status, Check. Check shows
    short warnings: After end, Paused period, Same image, No screenshot, Left
    team. The expected code is not a column. The table scrolls sideways with
    Uploaded pinned.
  - "No submissions yet" (with when evidence can arrive) is distinct from "No
    submissions match" (with Clear search and status). Loading skeleton, and
    load failure with Try again (search and status kept).
  - Returning from a submission restores the search and status and puts focus
    on the row you came from, briefly highlighted.
- **Submission workspace** (one page, no drawer):
  - Header: back to Review, the tile name with the status, and Previous /
    position / Next within the list you came from.
  - **Screenshot viewer** on the left, kept in view (sticky) while the side
    column scrolls. Fit, 100%, zoom out/in with the level shown, and Original
    (opens the full image in a new tab). Drag to move, wheel to zoom at the
    pointer, double-click toggles fit and 2×. The stage is focusable: + and −
    zoom, 0 fits, 1 is actual size, arrows move. Loading, failed (Try again,
    Open original) and missing states.
  - **Side column**, scrolling on its own:
    - Warnings that need a closer look, each phrased as a check: uploaded
      after the end (minutes after, the end in UTC, and that grace uploads
      are allowed but the drop must be before the end), uploaded during a
      paused interval, the credited player left the team (with the time),
      the same image on other submissions (with links), and no screenshot.
    - **Compare with the screenshot:** Account (team, and when the active
      account last changed), Drop (and boss), Objective, Code (the code kept
      with the submission, only when codes are enabled), Uploaded (UTC first,
      as the plugin stamps UTC, with Copenhagen time below), Event window
      (with any paused interval), and Contribution (what approving adds,
      partial amounts, progress, or what was added or removed). The tile name
      appears only in the header. A submitter's note and existing reviewer
      feedback appear when present; there are no empty placeholders.
    - **History** (decisions, corrections and reasons, newest first),
      **Earlier approvals for this objective** (same team, with links) and
      **Files** (current and earlier versions, size, dimensions, upload time,
      checksum, Open) are collapsed below.
  - **Decision panel**, pinned at the bottom of the side column (and sticky
    at the bottom of the screen on narrow layouts):
    - Pending: **Approve** (direct, no confirmation), **Reject…** (an inline
      reason, required, up to 4,000 characters; says who is notified) and
      **Correct details…** (tile, drop and credited account in a card above
      the facts so the screenshot stays visible; the new weight is shown; a
      reason is required; "Change at least one detail" if nothing changed).
    - Approved: who approved and when, and **Reverse approval…**, a
      confirmation that explains the contribution is removed, progress is
      recalculated, later evidence may take up the amount, and the record
      stays in history. A reason is required.
    - Rejected, Withdrawn, Reversed: a note that the attempt is history and a
      new upload is a separate submission. No reapproval.
    - Event not Live or in final review: "Review starts when the event is
      Live" (or the final-review wording).
    - After a decision: the outcome ("Approved · 2 added to Team Masori")
      with **Next submission** and **Back to the queue**; focus moves to the
      outcome, and a toast confirms.
- **Requests and failures:**
  - One request at a time: the pressed button shows a spinner, every decision
    control is disabled, and leaving waits. Loading feedback uses the shared
    minimum durations.
  - Failure: "Couldn't approve this submission. Nothing changed. Try again."
    in the panel.
  - Rule refusal: approving when nothing is left to add shows the reason and
    suggests rejecting with a reason.
  - Stale decision (another admin decided, or the team withdrew): nothing is
    saved, the current record is shown, and a banner says who changed it and
    asks you to check again. An unsaved reason is kept if the record is still
    pending.
  - Uncertain outcome (timeout): a banner with **Check status**; decisions
    are disabled until checked, and leaving asks first. The check shows the
    outcome or "It wasn't saved. Nothing changed, so you can decide again."
  - Leaving with an unsaved reason or correction asks first.
- **Samples:**
  - Midsummer Skilling Sprint (Live, codes on): a partial contribution (worth
    3, only 2 remain), a duplicate image of an approved submission, a manual
    objective with a note, an account changed shortly before upload with an
    older code in the screenshot, an image that fails to load once, a
    submission with no screenshot, an objective that's already complete, a
    corrected submission with a replaced (earlier) screenshot, and approved,
    rejected, withdrawn and reversed records.
  - Summer Bingo 2027 (in final review, codes off): an upload 43 minutes after
    the end, an upload during a paused interval, and a player who left the
    team.
  - Other events show the empty queue.
- **Prototype controls** (account menu): the next decision's outcome
  (succeeds, fails, times out either way, another admin decides first),
  "Mia decides the open submission", "The team withdraws the open
  submission", fail the next load or screenshot load, open a submission
  directly, a filtered queue link, a link with the wrong event, and the
  browser bar.

### Routing: prototype and application

The artboard simulates the browser. In the application these are real URLs.

- Search and status replace the queue URL as they change (no history entry
  per keystroke).
- Opening a submission pushes its URL with the queue's search and status.
  Back returns to the queue with them, with focus on that row. Review in the
  header steps back when the previous entry is that queue, otherwise opens it.
- Previous / Next replace the URL, so Back always returns to the queue rather
  than through every submission viewed.
- Direct entry and refresh load the submission by id. The event comes from
  the submission, and a link with another event's slug is corrected. Previous
  / Next then use the queue order for the search and status in the link.
- An unknown id shows "This submission isn't available".

### What the application already supports

- The queue for the selected event with live search (team, credited player,
  tile) and status, Pending first then newest; search and status kept into
  Details and back.
- Approve (contribution capped by remaining progress, drop cap and weight;
  refused when nothing is left), Reject with a reason and notifications to the
  credited participant and current captains/co-captains, Reverse with a reason
  and rebalancing of later approvals, and the reasoned correction of tile,
  requirement, drop and credited playing account with the destination weight.
- Review while Live or Awaiting final review, including after uploads close.
- Version checks on every decision; conflicts and refusals come back as
  messages.
- The retained expected code, minutes after the end with the end in UTC, the
  paused-interval check, checksum matches, earlier approvals for the team and
  requirement, asset versions with checksums, and review history.
- The lightbox zoom/pan logic in `public-evidence.js`.

### What needs integration

1. **Decisions without a full reload.** The current page posts and redirects
   with a status message. The reference keeps you on the page, shows the
   outcome with Next, and handles conflict, refusal and timeout in place.
   After a timeout, read the submission back (status and version) instead of
   retrying.
2. **Contribution projection** for a pending submission: what approving would
   add, partial amounts and objective progress. This is the calculation
   `ApproveAsync` already makes (remaining requirement amount, drop cap,
   weight), read without saving. Approved records show
   `ApprovedContribution`; reversed records need the removed amount from
   history.
3. **Queue warnings.** Today the queue only flags uploads after the end.
   Same image, paused interval, missing screenshot and left team need
   per-row projections, all from existing data. The queue's after-end flag
   uses `EventEndsAt` while Details uses `ActualEndedAt ?? EventEndsAt`; one
   boundary should be used.
4. **Drop and boss in the queue**, from the published drop snapshot.
5. **Account context.** `PRODUCT_REQUIREMENTS` 12.2 asks review to show the
   latest relevant account change in UTC. The data exists
   (`EventParticipantCharacterSwap`), but Details doesn't project it. The
   reference shows "active account since …, switched from …". The player's
   withdrawal time (already used by `IsEligibleTeamCreditAsync`) is shown as
   "left the team at …".
6. **Event window** (actual start and end in UTC, and any paused interval)
   in Details.
7. **Previous / Next.** Compute the neighbours from the event, search and
   status in the URL with the queue's ordering, or pass the list. The
   reference uses the list as it was when the submission was opened.
8. **Inline viewer.** Replace the lightbox with the in-page viewer (Fit, 100%,
   zoom level, keyboard and pointer controls). `public-evidence.js` already
   has the zoom, pan and pinch logic. Original stays `/Evidence/{assetId}`.
   Add an image load failure with retry.
9. **Routes.** The reference uses `/admin/events/{slug}/review[/{id}]`; the
   application uses `/Admin/Review?eventId=` and
   `/Admin/Review/Details/{id}?eventId&search&status`. Either works if search
   and status are kept and the event comes from the submission.
10. **Wording.** Short labels replace "Active asset", "Resolve submission",
    "Correct metadata before approval", "Ineligible final-review interval"
    and the always-visible "No Captain note".

### Proposed additions (need new functionality or a rule decision)

- **Block Approve when there is no screenshot.** The application allows
  approving a submission without an active asset. The reference keeps Approve
  available and shows a warning.
- **Disable Approve when nothing is left to add.** The application refuses on
  submit; the reference shows "Nothing left to add" under Contribution and
  keeps the button enabled, matching the application.

### Disagreements between code and requirements

- The refusal for a complete objective says "Mark the submission as a
  duplicate or reject it", but there is no duplicate state (contracts 7.5 and
  requirements 13: a duplicate is a Reject). The reference says "reject it
  with a reason".
- The paused-interval check compares the **upload** time and says "Treat it
  as outside the authoritative live eligibility intervals". The requirements
  make the screenshot time decisive, and an earlier drop uploaded later is
  normally fine (as in grace). The reference phrases it as a check.
- Correction eligibility: `ValidateTarget` checks team credit at the current
  time although its message says "at the evidence time", and the account list
  only offers current members with unreleased playing assignments. A
  correction to an account that was active at upload but has since left or
  been released isn't possible.
- Requirements 13.1 lists "Correct metadata and approve" as one action; the
  application corrects, then approves separately. The reference follows the
  application.

### Decisions to confirm

- Approving without a screenshot, and approving when nothing remains (above).
- Previous / Next order: the list at the time of opening (reference) or the
  live order.
- Workspace times: UTC first (to match the screenshot stamp) with Copenhagen
  time below; the queue leads with Copenhagen time.
- The paused-interval and correction-eligibility questions above.

## Final review: reference behaviour and integration notes

Final review is where admins see the standings, resolve anything preventing
publication, and publish official results. After publication it presents the
official results and the retained publication history. It owns event-wide
completion and publication only; individual evidence decisions belong to
Review.

The URL is `/admin/events/{slug}/final`. A retained version opens over the
page with `?version={n}`.

Verified against the current implementation (the participants-functionality
checkout):

- `Admin/Events/Finalize` and its page model (publish and reopen handlers;
  resolve, acknowledge, correct and archive are retired non-actions).
- `EventFinalizationService` (readiness, publication with the final Wise Old
  Man refresh, reopening, current-event exclusivity, idempotent retry of an
  archived publication).
- `PublicProgressCalculator.Rank`, `BingoEvent.Unfinalize` and
  `AcceptsNewSubmissions`.
- `PRODUCT_REQUIREMENTS.md`, "Approved Admin simplification" (RES-01), which
  supersedes older final-review descriptions.

### What the reference implements

- **Header:** "Final review" with a state badge (Provisional, Provisional ·
  correction, Official · version n, Live, or the pre-live phase), the end and
  upload-close times or the publication time, and "Times in Copenhagen time".
- **Publication** (Final review only), one card:
  - A headline: "n things to resolve before publishing", "Waiting for uploads
    to close", or "Ready to publish", with what publishing does ("makes these
    standings official as version n and archives the event"). In a correction
    cycle it says when and by whom the results were reopened and which version
    stays in history.
  - **Publish official results…**, unavailable (with the reason beside it)
    until every requirement is met.
  - The requirements, each with its state and a destination: in final review;
    uploads closed (an open window is a normal, timed wait: "They close at
    10:10 (in 10 min)… this is expected"); no pending submissions (**Open
    Review** goes to the event's queue filtered to Pending); placements
    calculated from the published board (**Open Board** otherwise); and, when
    another event is current, that event (see integration). Nothing can be
    acknowledged or overridden.
- **Standings:** place and team, full board (completion time), lines, tiles,
  credited EHB and score reached ("At completion" for full boards). Each row
  says in a few words what separates it from its neighbour ("First to complete
  the board", "Same lines; fewer tiles than Bandos Bros (18 vs 19)", "Shares
  3rd with Arma Squad: equal on every input") and highlights that value.
  Exact ties share a place ("=3"; numbering continues 1, 2, 3, 3, 5); names
  only order tied rows. **How placements are decided** opens the order, the
  tie rule and the approved-but-not-implemented EHB change. Provisional
  standings are calculated; official ones are the stored snapshot. The table
  scrolls sideways with the team column pinned.
- **Official results:** a stamp with the version, time and admin, "The event is
  archived", and any Wise Old Man refresh note retained from publication.
- **Publication history** (secondary): each version published (Official or
  Retained) and each reopening with its admin, time and reason; **View
  results** opens a version's stored results in a drawer, unchanged.
- **Correct official results** (secondary, below the history): **Reopen
  results…**, or the reason it isn't available ("Midsummer Skilling Sprint is
  the current event. Archive it before reopening these results.").
- **Confirmations:**
  - Publish: the top three as they will be published, then what happens
    (official version n, archived in the same step, the Wise Old Man refresh
    and what a skip means, participants notified). One confirmation, no
    reason.
  - Reopen: returns the event to provisional final review and makes it current
    again; the version stays in history and the next publication creates a
    new one; it doesn't restart the event or reopen uploads. A reason is
    required (up to 2,000 characters).
- **Outcomes:**
  - Published: the page becomes the official view, focus moves to it and a
    toast confirms. Published with a skipped or failed refresh: the same, with
    a warning banner that says the results are published and why the refresh
    didn't run. Never shown as a failed publication.
  - Failed: the dialog stays open with "Nothing changed. Try again."
  - Stale (standings changed, or another admin published or reopened first):
    nothing more is done, the current state is shown with a banner, and
    changed rows are highlighted.
  - Access removed: the dialog says so and offers only Close.
  - Uncertain (timeout): a banner says the request may have gone through;
    **Check status** reads the state back ("The results are official: version
    2 was published at 10:04…" or "The results weren't published. Nothing
    changed."). Publishing and reopening stay unavailable until checked.
    Reloading or returning to the event is also a readback.
- **Other states:** loading skeleton, load failure with Try again, Live ("Final
  review starts when the event ends", live standings), and no calculated
  results (pre-live, or no teams on the published board) with Open Board.
- **Samples:** Summer Bingo 2027 (3 pending), Boss Rush Weekend (a reopened
  correction cycle with a shared 3rd place, ready to publish), Clan Cup
  (uploads still open), Spring Bingo 2027 (official version 2 with a retained
  version 1, a reopening and a refresh note), Midsummer (Live), Autumn and
  Raids (no results yet). These are independent of the other artboards'
  samples.
- **Prototype controls** (account menu): the next publish and reopen outcome,
  "Another event is current", decide Summer Bingo's pending submissions, close
  Clan Cup's upload window, fail the next load, open Spring version 1
  directly, and the browser bar.

### Routing: prototype and application

The artboard simulates the browser; the application uses real URLs.

- Switching events keeps you on Final review for that event. Open Review and
  Open Board push their pages; Back returns here.
- **View results** pushes `?version=n`. Back closes the drawer; Forward
  reopens it; Close and Escape step back over it. A direct link opens the
  version over the page. An unknown version shows "This version isn't
  available"; an invalid value is dropped.
- Dialogs aren't in the URL; Back closes an idle dialog first and waits while
  a request is running.

### What the application already supports

- Readiness: final-review state, the upload window, Pending count with a link
  to the filtered Review queue, published board and calculated placements, and
  the retained review cycle; publication allowed only with no blockers.
- Publication: one confirmation, an immutable official snapshot (inputs,
  results and exact-tie explanations) and Archived state in one transaction,
  result notifications, the final Wise Old Man refresh with its outcome kept
  as feedback (not a failure), version checks, and an idempotent result when a
  timed-out publication is retried after it went through.
- Reopening with confirmation and reason, returning to provisional final
  review without reopening uploads, retaining the version, and refusing while
  another event is current.
- Retained history: every version with its placements and its reopening time
  and reason.
- Ranking: full board and completion time, lines, tiles, score time, EHB;
  shared rank only for exact equality.

### Approved changes awaiting implementation

- **EHB before score time.** RES-01 and the approved ranking put credited EHB
  ahead of the current-score time for teams without a full board. Today
  `Rank` compares score time first, and the current page's explanation says
  so. The reference calculates with today's order and states the change in
  How placements are decided. Older official versions keep their stored order.

### What needs integration

1. **Publish and reopen without a full reload.** Today both post and redirect
   with a status message (the refresh outcome becomes a warning message). The
   reference keeps you on the page, shows the outcome in place, and after a
   timeout reads the event back (state, version, latest finalization) instead
   of retrying. The service already treats a retried archived publication as
   success.
2. **Structured refresh outcome.** `FinalizeAsync` returns the Wise Old Man
   result as free text (`Feedback`). The reference needs succeeded / skipped /
   failed and a short reason to word the banner, and the retained note per
   version (it's in the lifecycle detail and audit today).
3. **Current-event exclusivity in readiness.** Publishing is refused when
   another event is Live, in Final review or Finalized, but readiness doesn't
   report it, so the button looks available until the request fails. Add it
   as a blocker (with the other event) and expose the same check for
   reopening.
4. **Readiness wording and destinations.** Short titles and reasons replace
   "Final-review checklist", "Results ledger", "Open records" and the
   remaining-count badge; an open upload window reads as a timed wait with the
   close time.
5. **Version history projection:** who published each version (the
   finalization keeps the actor id) and who reopened it.
6. **Row explanations and the decisive value** are computed from the same
   placement inputs; no new data.
7. **Routes:** `/admin/events/{slug}/final` and `?version=n`; the application
   uses `/Admin/Events/Finalize/{id}`.
8. **Legacy Finalized state:** the reference doesn't show it. The page should
   keep reading it for old records as it does today.

### Proposals and questions

- **Row explanations** are a presentation addition; confirm the wording.
- **Shared-place marker** "=3": confirm, or show "3" with a "Shared" label.
- **Reopen exclusivity:** the page shows the blocking event's name; confirm
  admins may see it (they can see all events).
- The publish confirmation lists the top three; confirm that's enough
  context, or show the whole table.

## WOM: reference behaviour and integration notes

WOM manages an event's Wise Old Man connection. It answers three questions
first: is the connection working, how current is the data, and does an admin
need to do anything. Technical diagnostics and occasional management sit
below. The website's event dates are always the schedule; this page never
edits dates or rosters.

The URL is `/admin/events/{slug}/wom`. Loading or reloading the page reads
what the website holds and never contacts Wise Old Man.

Verified against the current implementation (the participants-functionality
checkout):

- `Admin/Events/WiseOldMan` and its page model.
- `EventCompetitionSynchronizationService` (link and validation, the five-minute
  window check, manual and scheduled fetches, lease and retry rules),
  `EventCompetitionManagementService` (create, credential, queued updates,
  delete, reconciliation and conflict codes) and
  `EventCompetitionActivityProjection` (coverage and freshness states).
- `PRODUCT_REQUIREMENTS.md`, "Approved Admin simplification" (WOM-01/WOM-02),
  including the 2 October 2026 manual-fetch decision (AU15).

### What the reference implements

- **Header:** "Wise Old Man", a state badge (Connected, Needs attention, Not
  connected), the read-only phase when relevant ("Inspection only: the event
  is archived"), "The website's dates are the schedule", and "Times in
  Copenhagen time".
- **One main issue**, when there is one, as a single banner above everything
  else, in this priority: an operation whose outcome is unknown, a queued
  deletion, a conflict (changed on Wise Old Man, missing, shared with another
  event), a rejected management code (with Replace code…), a missing
  competition (the last data is kept; in final review, "Final review and
  publishing can continue"), temporary rate limiting or unavailability (the
  last data is still shown, with the automatic retry time), or data more than
  an hour old. Other sections refer to it rather than repeating it.
- **Connected competition:**
  - Competition number, title, **Open on Wise Old Man**, and a capability
    chip with one line: Website-managed ("Created by the website, which keeps
    it up to date"), Linked · read only ("reads its data but doesn't change
    it"), or Linked · can update ("its management code lets the website update
    it; it can't be deleted from here").
  - Three facts: **Latest data** (the last successful fetch, how long ago,
    and Wise Old Man's own update time), **Accounts found** (matched of
    expected playing accounts, and participants separately), and **Next
    scheduled fetch** (or the automatic retry and attempt).
  - **Fetch now**: a normal button with no confirmation and nothing to type.
    It shows "Fetching…" and can't be pressed again while running. When the
    server wouldn't allow it, it's unavailable with the time it opens and why
    ("Available from 11:02. Fetches are at least an hour apart.", "An
    automatic retry is due then.", "The next scheduled fetch is due then."),
    or "Fetching starts when the event goes live" / "Inspection only".
- **Account coverage:** a team table (accounts found with a meter, amber when
  incomplete; participants; names not found), from the last successful fetch,
  and a disclosure listing every account not found with its team and what to
  do (the website adds them with its next update, or add them on Wise Old Man
  for a read-only link, or check names after the roster is fixed at Live).
  Before the first fetch: "Coverage appears after the first successful fetch"
  with the expected count.
- **Updates to Wise Old Man:** the state of website-to-WOM synchronization
  (Up to date with the last confirmed update; Update queued, not confirmed
  yet; Updates paused; Not sent for an ID-only link; No more updates after the
  event) and the **management code** state (Validated, Saved · not checked
  yet, Rejected, Saved · couldn't be checked, Not added, or held by the website
  for a website-created competition) with Add code… / Replace code…. After the
  event first goes live, teams are fixed on Wise Old Man; name and dates still
  update.
- **Not connected:** two options side by side.
  - **Create it on Wise Old Man**, with what it needs and where to fix it:
    before the event starts; teams finalized (from a published draft or direct
    finalization; **Open Teams / Draft**); dates set and in the future (**Open
    Schedule**). Create is unavailable until all are met. A queued creation
    shows "Creating on Wise Old Man · queued 10:25 … isn't on Wise Old Man
    until it's confirmed"; an unknown outcome shows that it's being checked
    without sending again. Create and link are unavailable meanwhile.
  - **Link an existing competition** by ID, with validation ("Enter the
    competition ID", "Use numbers only") and the server's answers (dates more
    than 5 minutes apart, with both times and **Open Schedule**; not found;
    couldn't reach Wise Old Man).
- **Manage the connection** (secondary): Replace link (external, before or
  during Live), Disconnect (external, before Live; otherwise why not), Delete
  competition (website-created, before Live, nothing in progress; otherwise
  why not; external competitions can't be deleted).
- **Technical details** (collapsed): the window on Wise Old Man and whether it
  matches, the last attempt and result, the last error, retries, the request
  budget, and Wise Old Man's last update.
- **Dialogs:**
  - Management code: a password field, cleared as soon as it's sent, never
    shown again or echoed in feedback. Says that the code allows updates but
    doesn't change the competition's origin or allow deletion, and that it may
    be checked now or on the first update.
  - Replace link: the new ID with the same validation as linking.
  - Disconnect: removes the link; the competition on Wise Old Man isn't
    changed or deleted; optional reason (as today).
  - Delete competition: names the competition, says it's permanent and
    removes its history on Wise Old Man, that the event stays, and that the
    deletion is queued until confirmed. Focus starts on Cancel.
- **Outcomes:** fetched (toast, focus on the new time); refused within the
  hour; another fetch running; rate limited or not found (a notice that
  replaces the standing banner rather than repeating it); timed out (Check
  status reads the stored state back and never fetches again). Queued
  operations stay "queued" until confirmed; unknown outcomes are never
  repeated; "the event changed first" sends nothing and shows the current
  details.
- **Samples:** Midsummer (healthy, website-managed), Boss Rush (rate limited,
  partial coverage, read-only link), Winter Classic (rejected code, awaiting
  the first fetch), Summer Bingo (final review, competition missing, older
  data kept), Clan Cup (creation queued), Iron League (deletion outcome
  unknown), Raids Week and Autumn Bingo (not connected, teams not finalized),
  Spring Bingo (archived, inspection only). Independent of the other
  artboards' samples.
- **Prototype controls** (account menu): the next fetch, link or disconnect,
  code, and create or delete outcomes; process the event's queued operation;
  queue a website update; the competition changes on Wise Old Man; fail the
  next load; open Boss Rush directly; and the browser bar. The application's
  Development-only "make refresh due" control isn't shown.

### Routing: prototype and application

The artboard simulates the browser. Switching events stays on WOM; links to
Teams / Draft and Schedule push those pages and Back returns. There are no
sub-routes: dialogs and disclosures aren't in the URL, and Back closes an
idle dialog first. In the application the route is `/Admin/Events/WiseOldMan/{id}`.

### What the application already supports

- Linking by ID with validation against Wise Old Man and the five-minute window
  check; replacing; disconnecting an external link before Live (optional
  reason); clearing refused while Live and all changes refused after Live.
- Website-created competitions: creation preview and its errors, queued
  create, update and delete with reconciliation (unknown outcomes are checked
  without another write), conflict codes (changed outside, missing, shared),
  and the roster boundary after first Live.
- Credentials: protected storage, unverified until checked, validated,
  rejected, revoked and unavailable states, never redisplayed.
- Fetching: scheduled hourly slots anchored to the actual start, retries, the
  lease against concurrent fetches, the hour after a successful fetch, and
  manual fetch while Live or in final review.
- Coverage: matched and expected accounts per team, participants, missing
  names, and freshness states (waiting, partial, incomplete, temporarily
  unavailable, stale, complete).

### Approved changes awaiting implementation

- **AU15: Fetch now without a challenge.** Today the fetch form asks for a
  confirmation and the typed word FETCH. The approved change is a normal
  button. Every server rule stays.

### What needs integration

1. **Actions without a full reload.** Today every action posts and redirects
   with a status message. The reference keeps you on the page, shows pending
   feedback, and handles refusal, failure and timeout in place.
2. **Manual fetch availability.** The page can't tell when Fetch now will
   work: the server combines the hour after the last success, a pending
   retry, the next scheduled slot (once anything has been attempted) and the
   lease. Expose one "available from" time and an "in progress" flag in the
   view. The reference computes it from the first three; the lease isn't
   visible today.
3. **Structured fetch result.** `RefreshAsync` reports a skip without saying
   which guard applied. Return the reason (within the hour, retry pending,
   slot not due, running) and the time, so the message can be specific.
4. **Fetch in final review.** The service allows a manual fetch while Live or
   in final review; the current page shows the button only while Live. The
   reference follows the service.
5. **Last confirmed update and queued change.** Show the last applied update
   time (`LastAppliedAt`) and what a queued update carries.
6. **Wording.** Plain labels replace "Provenance / capability", "Website-created
   · manageable", "Operation: Pending", "Automatic update-all" and the
   explanation paragraphs; one main issue replaces repeated warnings.
7. **Missing-account guidance** depends on capability and phase (see above).

### Proposals and questions

- **Manual fetch rarely opens once fetching has started.** Because the next
  scheduled slot also blocks it after any attempt, Fetch now is mostly useful
  before the first scheduled fetch or when the scheduler is behind. Confirm
  this is intended, or relax the slot guard for manual fetches while keeping
  the hour and lease.
- **Conflict recovery.** For a changed, missing or shared competition the
  application only pauses updates; there is no recovery action on this page.
  The reference explains it and offers Open on Wise Old Man. Decide whether an
  admin action (for example re-sending after review) is wanted.
- **Data more than an hour old** is shown as a quiet notice while Live; confirm
  the threshold matches the stale state (1 hour).
- **Disconnect reason** stays optional, as today.

## Catalogue: reference behaviour and integration notes

Catalogue maintains the shared bosses and activities, their drops, and the
rates used to estimate EHB on event boards. The everyday task is finding an
activity and correcting a setting or a drop, so the page is a plain directory
and the work happens in one drawer per activity.

The URL is `/admin/catalogue?q=&cat=&status=`. The drawer adds
`activity={slug}`, an open drop editor adds `drop={id}`, and a new activity
is `new=1`, so every editor can be linked.

Verified against the current implementation (the participants-functionality
checkout):

- `Admin/Catalogue/Index` and its page model, and `Index.Api` (suggest,
  validate and price handlers).
- `BossActivity`, `CatalogueItem`, `SourceDrop`, `DropRateParser` and the
  catalogue refresh in `BoardEstimateService`.
- `PRODUCT_REQUIREMENTS.md` section 9 and CAT-01 in the approved
  simplification.

### What the reference implements

- **Directory:** search across activity and drop names (a match by drop says
  so: "matches Uncut onyx"), a category filter, and Active / Inactive tabs
  with counts; all three are kept in the URL and restored when the drawer
  closes. Columns: activity (image, name, category), rate ("35 kills/hr" or
  "Not set"), drops (with inactive ones counted separately), needs attention
  (Rate missing, n without probability, n without value, No drops) and
  status. "The catalogue is empty" (with Add activity) is distinct from "No
  activities match". Loading and load failure with Try again.
- **Activity drawer** (wide), in the order of everyday use:
  - **Settings:** name, category, kills or runs per hour (the label follows
    the category), and image URL with a preview. Changing the rate says what
    saving recalculates ("this activity's 8 drops and any draft boards using
    them. Approved boards keep their snapshot"). The footer shows "Activity
    settings changed" with Discard and Save activity only when something
    changed; otherwise it's just Close.
  - **Wise Old Man metric** (collapsed, status beside it: Verified,
    Unsupported, Couldn't check, Not checked, Not set): the metric, Suggest,
    Validate and save, Save without validating, and the result in place. "A
    verified metric means Wise Old Man tracks it, not that every player has
    data."
  - **Drops:** a compact list. Each row shows the item, the rate with its
    plain chance ("2 x 1/1,024 · 1 in 512 per kill"), the value, flags
    (Inactive, No probability, No value, Shared) and the calculated EHB (or
    why there isn't one). Rows open one at a time into an editor below the
    row.
  - **Drop editor:**
    - Item name (says when the item is shared and that renaming affects the
      other activities), drop rate with a live preview of the chance and EHB,
      image URL, and the calculated EHB with what it was and why it might be
      unavailable ("The activity has no kills per hour", "The rate can't be
      turned into a probability").
    - **Save drop** says what it saves ("the name, rate and image") and is
      unavailable until something changed; Discard resets the fields.
    - Renaming to an existing shared item shows that item (where it's used
      and its value) and requires ticking "Point this drop at the shared item
      instead"; a name already used by this activity is refused.
    - A rate that changes the number of rolls is refused with "Changing the
      number of rolls needs an operator."
    - **How the rate is counted** (collapsed, read-only): chance per roll,
      rolls, chance per kill, whose chance (your own, or your own in a team
      of n), "only after" for conditional drops, note and source.
    - **Value and item mapping** (collapsed, its own buttons): the stored value
      and where it comes from, the item ID and its status, any rejected
      unusual price, the price source (Hourly price, Manual, Untradeable), the
      manual value, the Wiki item ID with Suggest, **Validate and fetch
      price** and **Save without validating**. It says these belong to the
      shared item and which activities share it.
    - Deactivate / Reactivate drop, and Delete permanently… (Super Admin).
  - **Add drop:** a compact form at the top of the list (item name, rate with
    preview, image). A name that matches a shared item shows that item and
    requires "Use the shared item for this drop"; otherwise the new item needs
    a value: Enter value (zero is fine), Fetch price (looks the item up when
    adding; if that fails nothing is added and you can enter a value), or
    Untradeable. A drop already on this activity is refused.
  - **Availability:** Deactivate (with a short confirmation of what it does
    and doesn't affect) or Reactivate, and Delete… for the Super Admin.
- **Delete:** the dependency check runs first ("Checking what uses it…"). In
  use: "can't be deleted" with Deactivate instead. Unused: what's removed and
  what's kept (shared items, prices, Audit history), then Delete permanently.
  Changed meanwhile: says so. No cascade.
- **Unsaved work:** closing the drawer, switching to another drop, opening
  Add drop, Back/Forward or reload ask first and list exactly what would be
  discarded ("your changes to Tanzanite fang", "the activity settings", "the
  new drop").
- **Outcomes:** failure keeps entries; another admin's change shows the
  current values with "Your changes weren't saved"; a timeout offers **Check
  current values** (a readback), never a blind repeat. Provider failures say
  what was saved and what wasn't ("Saved. The price service isn't
  responding, so the ID wasn't checked. The stored value was kept.").
- **Samples:** Zulrah (two-roll uniques, a rejected unusual price, a joke
  drop), Vorkath, Nex (team chance and conditional drops), Tempoross, Fortis
  Colosseum (a drop without probability or value, unsupported metric),
  Kalphite Queen (rate missing), Corporeal Beast, Giant Mole (inactive) and
  Tormented Demons (no drops; deletable). Shared items: Uncut onyx, Clue
  scroll (elite), Dragon chainbody.
- **Prototype controls** (account menu): viewing as Super Admin or Admin, the
  next save and provider outcomes, "Mia edits Zulrah", an empty catalogue,
  fail the next load, open Tanzanite fang directly, and the browser bar.

### Routing: prototype and application

The artboard simulates the browser. Search and filters replace the URL;
opening an activity pushes `activity=`; opening a drop editor replaces it with
`drop=`; closing steps back to the directory with its filters (or replaces the
URL after a direct link). Back closes the drawer, asking first if something is
unsaved. The application uses `/Admin/Catalogue?bossId=&dropId=&addBoss=`;
either works if the filters are kept.

### What the application already supports

- Add, edit, deactivate and reactivate activities and drops; Super Admin
  deletion with a dependency check; audited changes.
- Displayed rates with the parser (numerators, separators, explicit rolls),
  calculated EHB from completions per hour and per-completion chance,
  recalculation of drops and draft boards when a rate changes, and approved
  snapshots that don't change.
- Shared items with explicit "use the existing item" on rename, new items with
  a manual value, fetched price or untradeable classification.
- WOM metric and Wiki item mapping with suggestion, validation, the four
  statuses, saving during outages, manual values that refreshes keep, the
  rejected-candidate safeguard, and cleared prices when an item ID changes.
- Version checks against concurrent edits.

### What needs integration

1. **Editing without full reloads.** Every handler posts and redirects with a
   status message. The reference keeps the drawer, the open editor and the
   result in place.
2. **Explicit shared-item choice when adding.** `OnPostBossDrop` silently
   reuses an existing item with the same name. The reference shows the item
   and asks first, as editing already does.
3. **Live rate preview** (chance and EHB as you type) is calculated in the page
   from the same parser and formula; no server change.
4. **Where items are shared:** the list of activities using an item, for the
   shared notes. The data exists (source drops by item).
5. **Readback after a timeout** for activity, drop and price saves.
6. **Delete impact** already exists for Super Admin (`OnGetDeletionImpact`);
   the reference shows it before the confirm button.
7. **Directory flags** (no probability, no value) from the existing drop and
   item data.

### Decisions and proposals

- **Advanced mechanics are operator-only today.** Team scope, assumed
  participants, conditional probability, roll changes, notes and data source
  are refused by the admin handlers ("operator-managed fields"). The brief
  asks to preserve them; the reference shows them read-only in "How the rate
  is counted" with a note that an operator changes them. Decide whether
  admins should edit any of them (that needs new handlers and validation).
- **Rate terminology:** the application uses "Kills per hour" for bosses and
  skilling bosses and "Runs per hour" for minigames. Consider "Completions
  per hour" for skilling bosses.
- **Deactivated activities** are described as hidden from new tiles; confirm
  how draft boards that already use one behave.
- **Image URLs** stay URLs (no upload), as today.

## Accounts: reference behaviour and integration notes

Accounts manages existing website accounts and their global access: finding
someone, understanding their account and event roles, and the support actions
the signed-in admin is allowed to take. It isn't participant management or a
profile editor. The navigation label is now **Accounts** (previously
"Accounts / Roles") on every artboard; the internal nav key and prototype
route changed from `roles` to `accounts` with it. Role management is still
part of the page.

The URL is `/admin/accounts?q=&role=&page=`. The drawer adds
`account={id}`, so an account can be linked directly.

Verified against the current implementation (the participants-functionality
checkout):

- `Admin/Accounts/Index`, `Manage` and `Transfer` (and `Create`, which is
  retired and returns Not Found) with their page models and
  `account-transfer.js`.
- `AccountAdministrationService`, `AccountIdentityService.GenerateResetLinkAsync`
  and `Account` (authorization version, disable and enable).
- `PRODUCT_REQUIREMENTS.md` lines 439–480 (role management, ownership,
  disabling, password reset).

### What the reference implements

- **Directory:** search on website username (up to 100 characters), a global
  role filter (All, User, Admin, Super Admin) and pages of 25 ordered by
  username, with Previous and Next. All three are in the URL and restored
  when the drawer closes. Columns: account (initials, username, Discord as
  "Discord · last known name", "Discord linked" or "No Discord link"), global
  role (Super Admin and Admin as badges, User as plain text), status (Active
  as text, Disabled as a red badge), current event roles (up to three, as
  "Summer Bingo 2027: Captain"; "No event participation" otherwise) and last
  login ("Never" when there isn't one). "No accounts match" says search
  covers usernames only; a page beyond the results offers "Go to the first
  page". Loading skeleton, and load failure with Try again.
- **Account drawer** (wide), most important first:
  - A disabled account shows one amber notice under the header with when,
    by whom and the reason. The rest of the drawer stays normal.
  - **Access:** global role with what it means (Event Captain roles are
    called out as separate when the person has one), status, last login,
    sign-in methods (Password, Discord, or both) and Discord ("Linked", with
    the last known display name marked as possibly out of date, or "Not
    linked").
  - **Manage access:** only the actions the signed-in admin can take, each
    with a line on what it does. When something isn't available, a
    `.perm-note` says why instead of showing a disabled control: your own
    account, the protected Super Admin account, Admin accounts for an
    ordinary Admin, and "Only the Super Admin can grant or revoke Admin
    access".
    - **Admin access** (Super Admin only): Grant Admin… for an active User,
      Revoke Admin… for an Admin. A disabled User shows "Restore the account
      before granting Admin access" with no button.
    - **Password-reset link:** Create reset link (active accounts only; a
      disabled account explains why not). Its text says the link is single
      use, valid for 60 minutes, replaces any earlier link, and doesn't
      change the password or contact the person. For a Discord-only account
      it says the link sets a password.
    - **Disable account** / **Restore account.**
    - **Ownership** (only on the Super Admin's own account): Transfer
      ownership….
  - **Linked OSRS characters:** read-only, with Preferred and Inactive.
  - **Events and team roles:** each event with its signup status
    (Confirmed, Waiting list, Withdrawn) and team roles with Captain and
    Co-captain badges and dates, including roles that ended.
  - **Disable and restore history:** a timeline with actor, time and the
    disable reason; "Never disabled" otherwise.
  - A closing note says username, Discord and characters belong to the
    account holder, and team roles are managed on each event's Teams page.
- **Confirmations** (alert dialogs over the drawer; focus starts on Cancel,
  or on the reason for disabling):
  - Grant and revoke name the account and show the role change as Now →
    After (`.chg`). No written reason or typed username. Grant says their
    sessions end and they'll see a website notification; revoke says the
    account stays active with its events, team and Captain roles and
    characters.
  - Disable shows Active → Disabled, says nothing is deleted and roster
    places, team roles, evidence and results are unchanged, and needs a
    reason (1–500 characters, with a counter). Empty reason: "Enter a
    reason."
  - Restore shows Disabled → Active and says only the website account is
    restored, not withdrawn signups or team roles that events have since
    ended.
- **Outcomes** stay in the drawer as one banner under the header, so the
  context is kept and the change is explicit ("arma_sky is now an Admin.
  Their sessions were ended…"). Failures keep the dialog open with "Nothing
  changed". Another admin's change gives the application's message ("This
  record was changed by another administrator. Current values are shown;
  review them before trying again.") with the current values. Lost
  permission says nothing changed and offers Refresh. An unconfirmed change
  offers **Check current status**, which re-reads the account and reports
  "It went through" or "It wasn't changed"; it never repeats the action.
- **Reset link result:** a `.secret-result` panel directly under the reset
  row, focused when it appears: whose link it is, its expiry time, the link
  in a copy field (Copy → Copied, or select-and-copy if the clipboard isn't
  available), what it does and doesn't do, and Done. It exists only in the
  drawer's memory: not in the URL, history, a toast, the samples or
  storage, and it's cleared by Done, closing or switching the account. An
  unconfirmed creation says a link may exist but can't be shown again, and
  offers **Generate a new link** as a deliberate choice (which replaces any
  earlier link) or Not now. A failure says any earlier link still works.
- **Transfer ownership** (Super Admin only; page header and the owner's own
  drawer): a dialog explaining it's exceptional and different from granting
  Admin, with the new Super Admin (active accounts other than yours) and
  your current password. Choosing someone shows the before and after for
  both accounts. Continue leads to a confirmation step ("mia_holm becomes
  Super Admin; your account remains an Admin. Both accounts must sign in
  again."), with Back. A wrong password returns to the form with "The
  current password is incorrect." and an empty password field; a recipient
  that changed or was disabled asks to choose another account. An
  unconfirmed transfer can't be dismissed with Escape, says not to repeat it
  and offers **Check ownership**. Success shows Before → Now and "Sign in
  again", after which the former owner sees the Admin view. The password is
  cleared whenever the dialog closes or steps back.
- **Prototype menu:** Sign in as the other admin (nils, the Super Admin, or
  mia_holm, an Admin), Next action (Succeeds, Fails, Another admin changed
  it first, Your access changed, Times out, Wrong password for transfer),
  Fail next load, Open rune_nora directly, Reset sample data and Show
  browser bar. Samples cover active and disabled Users and Admins, the
  owner, accounts with and without Discord, a Discord-only account, no
  characters or events, a Captain who isn't an Admin and an Admin who is a
  Captain, ended team roles and a repeated disable history.

### Routing: prototype and application

| Prototype | Application today |
| --- | --- |
| `/admin/accounts?q=&role=&page=` | `/Admin/Accounts?WebsiteSearch=&WebsiteRole=&WebsitePage=` |
| `…&account={id}` (drawer) | `/Admin/Accounts/Manage/{id}` (separate page) |
| Transfer dialog | `/Admin/Accounts/Transfer` (separate page) |

Opening an account pushes a history entry; closing returns to the filtered
directory (Back closes the drawer, Forward reopens it). Search and the role
filter replace the entry; page changes push one. A direct link opens the
drawer over its directory.

### What the application already supports

- Search, role filter, 25 per page and the row information shown.
- Account details: username, role, active, Discord linked and last known
  display name, last login, disabled time, whether a password exists,
  characters (Preferred, Active), event participation with signup status
  and team roles with joined and left times, disable and restore history
  from Audit.
- Grant (owner, active User), revoke (owner, Admin), disable (reason 1–500)
  and restore, with the self, Super Admin-target and Admin-target rules and
  the authorization-version stale check. Each change increments the version,
  ending the account's sessions; grant, revoke and restore add a website
  notification.
- Reset links: active accounts, Admin for Users only, Super Admin for others
  except a Super Admin target; earlier unused links superseded; 60 minutes;
  Audit records creation without the secret.
- Transfer: owner only, current password, active non-self non-owner
  destination; recipient becomes Super Admin and the former owner Admin.

### What needs integration

- **Drawer instead of pages:** Manage and Transfer are separate pages today.
  The drawer needs the details as a fragment or JSON keyed by account id,
  with the authorization version for the stale check.
- **Outcome states:** the handlers redirect with a status message. The
  reference needs a distinguishable result for success, stale, permission
  lost and refusal, and a way to re-read the account after an unconfirmed
  request (a normal details fetch is enough).
- **Reset link delivery:** today the link travels through TempData across a
  redirect. The reference expects it in the response to the generate request
  and shown once. See the question below.
- **Unconfirmed transfer:** "Check ownership" needs to read who's the Super
  Admin now; there's no dedicated endpoint, but reading the account or the
  directory filtered to Super Admin works.
- **Notification wording:** the reference says grant, revoke and restore
  leave a website notification, which matches `Notify`; disable doesn't.

### Questions and a conflict to resolve

- **Transfer confirmation (documentation vs implementation):**
  `PRODUCT_REQUIREMENTS.md` line 441 requires "the current owner's password
  and the typed destination public username". The implementation uses a
  destination select, the password and a confirmation dialog, without a typed
  username. The reference shows the implemented contract (select, password,
  explicit confirmation step) and doesn't add the typed username. If the
  requirement stands, a "Type the new owner's username" field belongs on the
  confirmation step.
- **Reset link in TempData:** with the default cookie TempData provider the
  link sits in an encrypted cookie until the Manage page reads it. Confirm
  that's acceptable, or return it directly in the generate response.
- **Disable confirmation:** the requirements call for "strong confirmation".
  The reference uses a confirmation with the consequences and a required
  reason, matching the implementation; no typed username is added.
- **Reset-link history:** the drawer doesn't list reset links created,
  because the brief keeps them out of account history; they remain in Audit
  ("One-time password-reset link created.").

## Building a new page

1. Copy the head block from `Participants.dc.html`: the runtime scripts, then
   `ui/tokens.css`, `ui/components.css` and `ui/behavior.js`. Keep the Geist
   font `<link>` and `body{margin:0}` in `<helmet>`.
2. Start from the shell markup (sidebar, top bar, `.page`) and the page header.
   Only change the nav item's `is-current`.
3. Assemble content from the component classes. Page CSS should be layout only,
   such as a table's `--table-cols` or a page-specific grid.
4. Route overlay behaviour through `DKAdmin` (focus, trap, close timing, toasts)
   rather than re-implementing it.
5. Keep business rules in the page's logic. For example, capacity and
   primary-account rules stay in Participants.

## Adding a component or variant without drift

- Check `Components.dc.html` first. Extend an existing component with a
  modifier (`.btn-…`, `.badge-…`, `.is-…`) before creating a new one.
- Use tokens for every color, size, radius, layer and duration. Don't add a
  token for a one-off internal nudge. Add a token only when a value carries
  meaning or repeats.
- Cover every state that applies: hover, pressed, keyboard focus, selected,
  disabled (with a reason where it helps), read-only, busy, invalid, empty,
  error. Check both themes.
- Motion must be symmetric, use the shared curve and durations, and respect
  reduced motion.
- Add the new component to `Components.dc.html` in both themes, then sync the
  canvas and this folder.
- Propose any departure from the visual language before applying it.

## Limits of the reference environment

- **No slots for child components:** the canvas runtime's child components
  can't receive page content as a slot. So the shell, drawer and modal are
  shared as CSS + behaviour + documented markup that each page repeats, rather
  than as wrapper components. Production should turn them into Razor
  partials/tag helpers backed by the same CSS contract and a small script.
- **Not an application library:** `support.js`/`vendor/` render the reference
  and should not ship. `ui/behavior.js` is reference code for the
  interaction contracts, not an application library.

## Simulated or deferred

- **Simulated:** all data (generated, resets on reload), persistence, server
  validation, authorization, concurrency and lifecycle enforcement. The account
  menu's "Fail next request" and "Reset prototype data" are prototype-only.
- **Routing:** Participants and the Dashboard don't implement URL state. The
  Events, Identity, Overview, Schedule, Signup setup, Teams / Draft, Board, Audit, Review, Final review, WOM, Catalogue and Accounts artboards simulate it with an
  in-memory history (see "URL contract" and each page's notes above); the
  application must implement it with real URLs.
- **Deferred or unresolved:** toast Undo (removed), manual waiting-list
  reordering (still in the prototype; undecided), and the account-slot limits
  (fixture values: 3 for Autumn/Summer, 2 for Winter).
- **Lifecycle:** mirrors the application. Participant administration is allowed
  in event states Draft, SignupOpen and SignupClosed while the team draft isn't
  locked (`SignupService.CanAdministerParticipants`), and is locked once the
  event has gone Live. Payment and the private note follow the wider
  private-metadata rule.

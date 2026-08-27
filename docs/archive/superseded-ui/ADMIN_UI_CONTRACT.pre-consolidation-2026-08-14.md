# Admin UI Contract — Manage-Derived General Hierarchy

This is the clean implementation contract for the shared Admin shell and the
left-side operational components demonstrated by the rendered Manage audit.
It is a contract of rendered primitives, geometry, semantic states, and
verification boundaries—not a transcription of page CSS.

Every normative visual rule below has a provenance marker. `[RM-*]` means a
specific fact confirmed by the amended rendered Manage audit. `[N-TITLE]`
means the explicit normalization supplied with that audit: component title is
17px / 600. No other value is normative here unless it has one of those
markers.

## Authority and scope

- This contract governs the shared Admin shell and the left-side operational
  components in Manage. `[RM-SCOPE]`
- The four metric cards are outside this contract. `[RM-EXCLUDE-METRICS]`
- Events Directory and Schedule are reserved for later family addenda; this
  contract does not define their full-width-table or detail-form families.
  `[RM-RESERVE-FAMILIES]`
- `UI_OVERHAUL_ROADMAP.md`, existing page-local CSS values, and legacy
  presentation values are non-authoritative for this contract. A selector's
  existence is not evidence of a contract value. `[RM-PROVENANCE]`

## Rendered primitives

### Shell and content canvas

- The Admin shell uses a 64px header. `[RM-SHELL-64]`
- The desktop shell uses a 232px sidebar and a 24px content gutter. `[RM-SHELL-232-24]`
- The content canvas is the receiving surface for the operational components;
  its relationship to the sidebar is part of the shell, not a page-local
  layout invention. `[RM-CANVAS]`

### Wide rail and responsive removal

- At the wide desktop state, the sidebar is the rail relating the shell to the
  content canvas. `[RM-RAIL-RELATION]`
- At the audited responsive transitions, the wide rail is removed or replaced
  by the rendered drawer/rail state; implementations must preserve that
  transition rather than retain a compressed desktop rail. `[RM-RAIL-REMOVAL]`

### Component surface and spacing

- An operational component is a distinct surface with 20px internal padding
  and 20px component/row gaps. `[RM-COMPONENT-20]`
- Component and operational-row corners use a 12px radius. `[RM-RADIUS-12]`
- The surface, spacing, and radius are shared primitives; do not create
  component-specific replacements from legacy selectors. `[RM-COMPONENT-SHARED]`

### Tonal/readiness rows

- Readiness and lifecycle information is represented as a compact tonal row:
  state, supporting explanation, and the available action remain visually
  related. `[RM-READINESS-ROW]`
- Readiness rows use the same operational-row geometry as other Manage rows;
  readiness does not justify a larger or separate control treatment.
  `[RM-READINESS-GEOMETRY]`

### Labels and support text

- Component titles normalize to 17px / 600. `[N-TITLE]`
- Compact operational-row labels use 12px / 500. `[RM-ROW-LABEL]`
- Generic supporting text has no proven exact size in this audit; it must
  inherit the approved component relation or remain subject to verification.
  `[RM-SUPPORT-TEXT]`
- The current 14px heading selectors are excluded from the contract; they are
  not promoted from the rendered Manage audit. `[RM-HEADING-EXCLUSION]`

### Rows, fields, actions, and pills

- An operational row uses the audited 14.4px row treatment with 16px padding.
  `[RM-ROW-14-4-16]`
- Fields use 36px height. `[RM-FIELD-36]`
- Neutral and danger actions use 32px height. `[RM-ACTION-32]`
- State pills use 26px height. `[RM-PILL-26]`
- Neutral and danger are semantic action roles. Their exact colors and any
  unobserved interaction styling are not defined by this contract.
  `[RM-ACTION-SEMANTICS]`

### Persistent-setting toggle

- A persistent boolean setting is one labelled control composition, not an
  action-acknowledgement control: a native checkbox marker followed by one
  copy block containing its setting label and optional supporting sentence.
  `[PST-SCOPE]`
- The marker is a `1rem` square checkbox, uses the shared Admin navigation
  accent when checked, and retains the shared visible focus treatment. It has
  no extra badge, container, or independently positioned check glyph.
  `[PST-MARKER]`
- The setting composition is a two-column `1rem minmax(0, 1fr)` layout with a
  `0.6rem` marker-to-copy gap. Its alignment is `center` against the **whole
  copy block**: when the copy has a label and support line, the marker centres
  vertically against both lines together, never only against the label.
  `[PST-ALIGNMENT]`
- The copy block is a compact vertical stack with a `0.2rem` internal gap.
  The setting label is the primary cue; optional support is muted secondary
  copy. Do not substitute a page title, a pill, or a standalone alert for the
  setting label. `[PST-COPY-HIERARCHY]`
- Use this pattern only for a durable on/off setting, such as Participants'
  “Waiting list enabled”. Do not use it for destructive or lifecycle
  acknowledgements; those require explicit confirmation actions. `[PST-USAGE]`

### Shared-row setting checkbox

- In the approved Participants composition, the normal field heading and the
  checkbox setting heading are peers in the first row; the field input and the
  checkbox with its support copy share the second row.
- The checkbox marker aligns with the smaller support copy, not the heading.
- Use the standard `.admin-field` / `.event-create-field` `0.25rem`
  label-to-control/support rhythm, and stack the composition naturally in
  source order at narrow widths. `[PST-SHARED-ROW]`

### Lifecycle and locked states

- Locked and lifecycle states retain the audited component and operational-row
  relation; this audit does not establish a reusable general feedback family.
  `[RM-LIFECYCLE-LOCKED]`
- Terminal pages remove unavailable actions instead of rendering fake disabled
  controls. `[RM-TERMINAL-REMOVE-ACTIONS]`
- General success/error feedback styling and confirmation behavior were not
  established under GET. They remain required future verification, not
  inferred contract values. `[RM-FEEDBACK-UNOBSERVED]`

## Responsive contract

The amended rendered audit established these transition points and the
corresponding rail/drawer transition behavior:

- `1100px`: the audited rail removal / one-column transition applies; no
  drawer activation is established at this breakpoint. `[RM-BREAKPOINT-1100]`
- `900px`: the audited responsive drawer activates. `[RM-BREAKPOINT-900]`
- `600px`: the audited narrow/mobile shell and component transition applies.
  `[RM-BREAKPOINT-600]`

These markers record the audited breakpoint facts. Other breakpoint behavior
remains unverified. `[RM-RESPONSIVE-DRAWER-RAIL]`

## Interaction and verification boundaries

- Focus was observed in the rendered audit and is a required state to preserve;
  this contract does not invent a focus value that was not recorded.
  `[RM-FOCUS-OBSERVED]`
- Hover behavior was not verified and remains a future verification
  requirement. `[RM-HOVER-UNVERIFIED]`
- Sequential keyboard movement was not verified and remains a future
  verification requirement. `[RM-KEYBOARD-UNVERIFIED]`
- No hover, keyboard, confirmation, or other unobserved behavior may be
  promoted to a normative rule from selector inspection. `[RM-UNOBSERVED-NOT-RULES]`
- The linked WoM composition is excluded from this contract. `[RM-WOM-EXCLUDE]`

## Contract-to-implementation process

1. Implement only the rendered approved primitives named in this contract:
   shell/canvas, rail transition, component surface, spacing, rows, fields,
   actions, pills, lifecycle/locked states, and responsive drawer/rail states.
   `[RM-IMPLEMENT-PRIMITIVES]`
2. Use the semantic names and roles above. Do not import a value merely because
   a selector contains it. `[RM-SEMANTIC-PRIMITIVES]`
3. For later provenance review, trace each implemented primitive to its
   corresponding rendered evidence; selector presence alone is not evidence,
   and legacy presentation values do not become rules.
   `[RM-RENDERED-EVIDENCE-TRACE]`
4. Treat every item marked as a future verification requirement as unverified
   until it is observed in the relevant rendered state. `[RM-FUTURE-VERIFY]`

## Provenance appendix

The markers below are deliberately compact so an independent reviewer can
check every subsection against the two authorized inputs: the amended rendered
Manage audit and the explicit title normalization.

| Marker | Authorized input fact |
| --- | --- |
| `RM-SCOPE`, `RM-EXCLUDE-METRICS`, `RM-RESERVE-FAMILIES`, `RM-PROVENANCE` | Audit scope and authority boundary: shared Admin shell plus left-side Manage operational components; metric cards excluded; Directory/Schedule family rules deferred; rendered evidence outranks legacy presentation. |
| `RM-SHELL-64`, `RM-SHELL-232-24`, `RM-CANVAS`, `RM-RAIL-RELATION`, `RM-RAIL-REMOVAL` | Rendered shell/content-canvas geometry and the observed wide-rail relation/removal across responsive states. |
| `RM-COMPONENT-20`, `RM-RADIUS-12`, `RM-COMPONENT-SHARED` | Rendered component surface, 20px padding/gaps, and 12px component/row radii. |
| `RM-READINESS-ROW`, `RM-READINESS-GEOMETRY` | Rendered tonal/readiness row and its reuse of operational-row geometry. |
| `N-TITLE` | Explicit normalization: component title is 17px / 600. |
| `RM-ROW-LABEL`, `RM-SUPPORT-TEXT`, `RM-HEADING-EXCLUSION` | Rendered compact row label role, the absence of a proven generic support size, and the explicit exclusion of current 14px heading selectors. |
| `RM-ROW-14-4-16`, `RM-FIELD-36`, `RM-ACTION-32`, `RM-PILL-26`, `RM-ACTION-SEMANTICS` | Rendered operational-row, field, neutral/danger action, and state-pill geometry and semantics. |
| `RM-LIFECYCLE-LOCKED`, `RM-TERMINAL-REMOVE-ACTIONS`, `RM-FEEDBACK-UNOBSERVED` | Rendered lifecycle/locked/terminal behavior; no reusable general feedback family was established, and success/error/confirmation styling was not observed under GET. |
| `RM-BREAKPOINT-1100`, `RM-BREAKPOINT-900`, `RM-BREAKPOINT-600`, `RM-RESPONSIVE-DRAWER-RAIL` | Rendered 1100px rail-removal/one-column behavior, 900px drawer activation, and the audited 600px narrow/mobile transition. |
| `RM-FOCUS-OBSERVED`, `RM-HOVER-UNVERIFIED`, `RM-KEYBOARD-UNVERIFIED`, `RM-UNOBSERVED-NOT-RULES` | Rendered interaction verification status: focus observed; hover and sequential keyboard movement not verified. |
| `RM-WOM-EXCLUDE` | Explicit exclusion of the linked WoM composition from the Manage-derived hierarchy. |
| `RM-IMPLEMENT-PRIMITIVES`, `RM-SEMANTIC-PRIMITIVES`, `RM-RENDERED-EVIDENCE-TRACE`, `RM-FUTURE-VERIFY` | Explicit contract-to-implementation and provenance process: rendered primitives first, rendered evidence for later review, and no promotion of unobserved values. |
| `RM-DIALOG-MODE`, `RM-DIALOG-SURFACE` | Questions route/dialog delivery: `Questions.cshtml:12–18` renders the same detail/form component with `admin-dialog-page event-overview-section`, while `_AdminLayout.cshtml:261–265` supplies a transparent native route-dialog; route-dialog CSS at `site.css:3232–3237` constrains and centers the presentation container without making it a full Admin canvas or nested page shell. Ordinary Identity/Schedule detail/form widths and surfaces remain the comparison hierarchy. |
| `RM-DIALOG-HEADING` | Questions dialog output keeps title/support inside `.event-overview-section-heading.admin-dialog-component-heading` at `Questions.cshtml:15–16`; dialog-specific heading rules at `site.css:5028–5032` use component-scale title/support treatment, while the ordinary Manage/Schedule headings establish the component hierarchy. |
| `RM-DIALOG-CONTENT` | Questions uses `.admin-dialog-page-body` inside the component at `Questions.cshtml:31`; route-dialog content owns bounded scrolling at `site.css:3237` and narrow route-dialog CSS is present at `3288–3289`. `signup-questions-overlay.js` uses `canEnhance()`/route fallback at `7, 135–141, 180–194`, and keeps form sections/actions in the loaded component. |
| `RM-DIALOG-CLOSE` | Questions provides a top-right close control at `Questions.cshtml:15–17`; the shared search-clear x path is rendered at `Events/Index.cshtml:17` and its color/hover/focus rules are at `site.css:4207–4212`. `signup-questions-overlay.js:146–147, 163–170` focuses the dialog close on open and restores the trigger on close. The contract requires the route close to consume that shared x treatment; it does not claim the current page-local close CSS is already identical. |
| `DECISION-DIALOG-SECTIONS` | Approved visual decision: an independent administrative concern inside a route dialog is a tonal section panel, while related fields and local actions stay together within that panel. |
| `DECISION-DIALOG-DESTRUCTIVE-ACTION` | Approved exception: a final destructive lifecycle action with no persistent settings or fields may be one lone danger-outline footer control rather than an empty tonal panel; confirmation remains a compact centered dialog above the route dialog. |
| `DECISION-DIALOG-NONDESTRUCTIVE-ACTION` | Approved non-destructive final-lifecycle exception: when no persistent settings or fields exist, the action uses an accent-outline footer control alongside other final lifecycle actions, without an otherwise-empty panel; its confirmation is compact and centered with a neutral Cancel, accent-outline confirmation, and no invented persistent fields. |
| `RM-DIALOG-EXCLUSIONS` | No exact overlay color or separate focus family is promoted here; unproven behavior remains excluded under the existing rendered-evidence boundary. |

No other source—especially page CSS, legacy roadmap values, or selector
presence—is an authority for this contract.

### Shared route-dialog presentation mode

- A desktop Admin route-dialog is a presentation mode for a normal detail/form
  component. It is centered and constrained to the ordinary detail/form
  main-column width; it does not expand to the full Admin content canvas.
  `[RM-DIALOG-MODE]`
- The shared desktop route-dialog default cap is 41rem, bounded by the
  viewport; a future page may justify a specific override.
  `[DECISION-DIALOG-41]`
- The dialog is one component surface. The native route-dialog container and
  dialog body remain transparent presentation plumbing; they do not create a
  separate page background, nested page shell, or second component surface.
  `[RM-DIALOG-SURFACE]`
- The title and support text belong inside that component surface and use the
  ordinary component heading/support hierarchy. Route-dialog content must not
  switch to the Admin page-heading hierarchy. `[RM-DIALOG-HEADING]`
- Sections, fields, rows, and actions continue to use the existing component
  rules. Long dialog content scrolls within the dialog content region. At
  narrow/mobile route mode remains the ordinary full-page route rather than a
  compressed desktop dialog. `[RM-DIALOG-CONTENT]`
- Within that one dialog component, each independent administrative concern
  is a tonal section panel using the shared component/operational-surface
  treatment. A panel owns its heading, supporting copy, related fields or
  metadata, and local actions. Do not leave unrelated concern headings,
  controls, or destructive actions loose on the dialog background merely by
  separating them with whitespace. Keep tightly related fields and actions in
  the same panel; do not create a card for every field. `[DECISION-DIALOG-SECTIONS]`
- A final destructive lifecycle action that has no persistent settings or
  fields is the narrow exception: render one explicitly named danger-outline
  control at the dialog's bottom-right, without retaining an otherwise-empty
  section panel or heading. Its confirmation remains a compact centered dialog
  above the route dialog, with a concise consequence, the existing optional
  note only where the handler supports it, a neutral Cancel control, and a
  danger confirmation control. `[DECISION-DIALOG-DESTRUCTIVE-ACTION]`
- A final non-destructive lifecycle action that has no persistent settings or
  fields uses the same narrow footer exception: render its explicitly named
  accent-outline control at the dialog's bottom-right alongside other final
  lifecycle actions, without retaining an otherwise-empty section panel or
  heading. Its confirmation remains a compact centered dialog above the route
  dialog with a neutral Cancel control and an accent-outline confirmation
  control. `[DECISION-DIALOG-NONDESTRUCTIVE-ACTION]`
- Compact confirmations use the Participants geometry: a maximum width of
  `min(24rem, calc(100vw - 2rem))`, `1rem` padding, the standard Admin surface
  and border, and one shared `rgb(0 0 0 / 45%)` black backdrop. A nested native
  confirmation keeps its own backdrop transparent so it does not visibly add a
  second dim layer. `[DECISION-DIALOG-CONFIRMATION]`
- The close control belongs at the top-right of the component heading. It uses
  the same x glyph/path, color, stroke/weight, hover treatment, and focus
  treatment as the shared search-clear control. Only the visible glyph is
  modestly larger; the control retains a comfortable accessible hit target.
  Closing must restore focus to the trigger. `[RM-DIALOG-CLOSE]`
- Exact overlay color, a new focus family, and unrelated overlay behavior are
  not established as general values by this clarification. They remain
  excluded unless separately proven.
  `[RM-DIALOG-EXCLUSIONS]`

## Addendum — Full-width Admin data-page family

This addendum defines the rendered family for full-width Admin directory/data
pages. It extends the current general contract; it does not revise the
Manage-derived hierarchy. `[FDP-SCOPE]`

### Canvas and toolbar

- The page uses the full remaining Admin content canvas. It has no page-local
  max-width or outer card; the toolbar and table span the available canvas.
  `[FDP-FULL-CANVAS]`
- Exactly one 12px-radius, borderless toolbar surface owns the sequence
  search → State filter → Create New Event. At wide size, the search/filter
  form is on the left and Create is on the right; at `≤1100px`, the form is
  one column and Create is right-aligned below it. `[FDP-ONE-TOOLBAR]`
- Search, State, and Create reuse the general contract's field, action, and
  focus primitives by reference. This addendum does not redefine their
  values or focus geometry. `[FDP-INHERIT-FIELD-ACTION-FOCUS]`
- With a non-empty server-filtered search, the visible clear action is
  “Clear event search”; when there is no search, that action is not rendered.
  `[FDP-CLEAR-SEARCH]`

### Directory table surface and context

- At wide size, one 12px-radius, borderless table surface contains the six
  columns Event identity, State, Event dates, Signups, Needs attention, and
  Actions. `[FDP-TABLE-SURFACE]`
- The table header is a distinct tonal band. Rows are borderless and have no
  row separators or outlines. `[FDP-HEADER-ROWS]`
- Single-line `<th>` cells use `.6rem 1rem` padding with `.6875rem / 500 / 1.25` text; height is content-derived, not fixed.
- Where Directory context is needed, a title, supporting text, and count may
  appear above the table; that sequence is optional and is not required for
  every table. The Participants evidence authorizes only the sequence
  “Current signups (61)” followed by “Signup history (0)”; it supplies no
  settings, checkbox, or legacy-style authority. `[FDP-TABLE-CONTEXT]`
- Event identity is the strongest cell content. The slug is muted monospace
  support text; dates and signups use primary and support lines; attention is
  muted or yellow; and Workspace is a quiet trailing action. All cells,
  including Actions, align left at wide size. `[FDP-CELL-HIERARCHY]`
- State uses the general contract's semantic state-pill primitive by reference;
  this addendum does not redefine pill values. `[FDP-INHERIT-PILLS]`

### Sorting and empty state

- The first five headers—Event identity, State, Event dates, Signups, and
  Needs attention—are sortable links. `[FDP-SORTABLE-HEADERS]`
- Only the active sort exposes the current `aria-sort` direction, the sort
  icon, and a current-sort label such as “currently sorted…”. The observed
  State example uses `ascending`. Actions is unsortable, is not a link, and
  has no `aria-sort`. `[FDP-ACTIVE-SORT]`
- A server-empty filtered result replaces the table with a centered status
  message inside the same 12px-radius tonal surface, with 24px padding.
  `[FDP-EMPTY-SURFACE]`

### Responsive family behavior

- The general shell's responsive rail, drawer, and narrow-shell rules remain
  inherited from the current contract by reference; this family does not
  redefine those shell transitions. `[FDP-INHERIT-SHELL-RESPONSIVE]`
- At `≤1100px`, table headers are hidden and each event becomes a 12px-radius
  card. Each field becomes a two-column label/value row with a 112px label
  track, and Workspace is right-aligned at the end of the card.
  `[FDP-CARD-1100]`
- The card model remains intact through the audited 900px and 600px states;
  the shared shell transitions remain inherited, while the directory cards
  remain full-width and readable. `[FDP-CARD-900-600]`

### Verification boundary

- Extreme unbroken slugs remain unverified and are not a contract rule.
  Sort-header focus remains unverified and is not a contract rule. Hover
  remains unverified and is not a contract rule. `[FDP-UNVERIFIED-EXACT]`

## Full-width data-page provenance appendix

| Marker | Authorized input fact |
| --- | --- |
| `FDP-SCOPE`, `FDP-FULL-CANVAS` | Audit scope: the Directory uses the shared Admin canvas, has no page-local max-width or outer card, and its toolbar/table span the available canvas. |
| `FDP-ONE-TOOLBAR`, `FDP-CLEAR-SEARCH` | Audit toolbar facts: one borderless 12px-radius surface owns search → State filter → Create New Event; wide and `≤1100px` arrangements; conditional “Clear event search”. |
| `FDP-INHERIT-FIELD-ACTION-FOCUS`, `FDP-INHERIT-PILLS` | Audit classification marks search/filter/create and state pills as inherited general primitives; the current general contract owns their values and focus treatment. |
| `FDP-TABLE-SURFACE`, `FDP-HEADER-ROWS` | Audit table facts: one borderless 12px-radius six-column surface, distinct tonal header band, and rows without separators or outlines. |
| `FDP-TABLE-CONTEXT` | Audit’s exact Directory-context boundary: title/support/count may appear above a table but are not required for every table; Participants contributes only “Current signups (61)” then “Signup history (0)” and no settings, checkbox, or legacy-style authority. |
| `FDP-CELL-HIERARCHY` | Audit data hierarchy and alignment: identity strongest; muted monospace slug support; primary/support date and signup lines; muted/yellow attention; quiet Workspace action; all cells left-aligned. |
| `FDP-SORTABLE-HEADERS`, `FDP-ACTIVE-SORT` | Audit sort/ARIA facts: first five headers are links; active State sort rendered `ascending`, an icon, and a current-sort label; Actions is unsortable and has no `aria-sort`. |
| `FDP-EMPTY-SURFACE` | Audit filtered-empty fact: the table is replaced by a centered message in the same tonal surface with 24px padding. |
| `FDP-INHERIT-SHELL-RESPONSIVE`, `FDP-CARD-1100`, `FDP-CARD-900-600` | Audit responsive facts: shell transitions at 900px/600px are inherited general behavior; at `≤1100px` headers disappear, records become cards with 112px label tracks and right-aligned Workspace; the card model remains intact at 900px/600px. |
| `FDP-UNVERIFIED-EXACT` | Audit verification boundary: extreme unbroken slugs, sort-header focus, and hover were not verified and are not promoted to rules. |

No normalization item was found in the Directory output. The Participants
evidence boundary recorded above remains the applicable authority boundary.
`[FDP-TABLE-CONTEXT]`

## Addendum — Event-scoped Schedule detail/form family

This addendum defines the normal rendered family for
`/Admin/Events/Schedule/{id}`. It extends the Manage-derived hierarchy for an
event-scoped detail/form page with the reusable Event information rail; it
does not revise the Manage hierarchy or the full-width Directory family.
`[SD-SCOPE]`

### Canvas, main/rail relationship, and responsive removal

- The Schedule page is a page-local canvas with no outer card: the normal
  `.schedule-editor-page` is full width with `0.5rem` top and `2rem` bottom
  padding. At `>1100px` it is capped at `min(100%, 76rem)` and centered.
  `[SD-CANVAS]`
- The normal wide layout is two columns: the form/main side is
  `minmax(0, 2fr)` and the Event information rail is
  `minmax(18rem, 1fr)`, with a `1.25rem` gap. The layout starts as one column
  before that wide media query. `[SD-MAIN-RAIL]`
- The rail is a separate `<aside>` after the main form, not a form subsection.
  At `≤1100px`, `.schedule-event-information-rail` is `display: none`; it is
  removed rather than relocated below or inside the form. The form becomes
  full width. `[SD-RAIL-REMOVAL]`
- On the wide state the rail retains the shared Event information surface and
  sticky/scrolling behavior. The Schedule-specific winning top offset is
  `calc(var(--admin-header-height) + 0.5rem)`; the shared panel still owns its
  `max-height` and vertical overflow. `[SD-RAIL-STICKY]`
- At `≤600px`, the page top padding becomes `0.25rem` and the main form panel
  reduces to `0.75rem` padding. `[SD-NARROW-CANVAS]`

### Normal grouped form components

- The form owns one `.schedule-editor-panel` surface with
  `1.25rem` padding, the Admin surface background, no border, and a `0.75rem`
  radius. Its normal `.event-schedule-groups` are one vertical stack with
  `0.75rem` gaps. `[SD-PANEL-GROUPS]`
- Each normal `.event-schedule-group` owns a soft nested surface with
  `0.9rem` padding, no border, and a `0.6rem` radius. For the Signup window,
  Event schedule, and Registration capacity groups, the heading is a single
  heading/support unit: the rendered `h3` and support `p` are inside the same
  heading wrapper, with the final Schedule rules making the heading `0.75rem`
  / `650` / `1.25` and support text `0.65625rem` / `1.35`, muted, with
  `0.2rem` top margin. The unconditional retroactive-reason group is also an
  `.event-schedule-group`, but its heading contains its field label only; it is
  a Schedule-specific exception and has no heading support paragraph.
  `[SD-GROUP-SURFACE]` `[SD-GROUP-HEADING]`
- The normal field region is a two-column grid of
  `minmax(0, 1fr)` tracks with `0.75rem` gaps. Each `.admin-field`/
  `.event-create-field` is a small grid with a `0.25rem` label-control/support
  gap. The shared Admin field rule supplies the normal control minimum height
  of `2.25rem` and the field label hierarchy of `0.75rem` / `500` / `1.25`.
  `[SD-FIELD-GRID]` `[SD-FIELD-PRIMITIVES]`
- A normal `.event-create-field.full-field` spans both columns at the wide
  field state. At `≤600px`, the field grid is one column and the winning
  `full-field` rule returns to `grid-column: auto`, so it remains full width by
  the one-column geometry rather than by a special mobile span.
  `[SD-FULL-FIELD]`
- The datetime control is a normal field-level control composition: when the
  adapter is present, the visible date/time parts are a two-column grid with
  `0.625rem` gap and the canonical datetime input is visually hidden. At
  `≤480px`, those visible parts also stack to one column. `[SD-DATETIME]`
- The outer `.schedule-capacity-warning-component` and its
  `.schedule-capacity-warning-grid` with the capacity label/control are always
  rendered in the Schedule output. The warning-label, warning-row, and
  acknowledgement action children are conditional on signup warnings. The
  capacity label/control grid—including its `>600px` split arrangement—is
  Schedule-specific and neither that split nor the conditional warning variant
  is a generic detail/form-family pattern. `[SD-CAPACITY-SCOPE]`
- The schedule draft field adds `0.75rem` top spacing within its group. The
  retroactive-reason group is separated by `1rem`; its label-only heading uses
  the same rendered field-label scale. These are Schedule-specific placements,
  not a universal form-section rhythm. `[SD-SECONDARY-RHYTHM]`

### Labels, support, locked state, validation, and actions

- Labels are the primary field cue; controls follow immediately; field support
  and field-level validation remain inside the field. The normal Schedule
  output renders each `asp-validation-for` as a `.field-error` span and the
  form-level `asp-validation-summary` as the first form child before the panel.
  Empty validation nodes are hidden by the winning shared rule. This records
  ownership only; it does not establish a generic Admin alert/feedback family.
  `[SD-FEEDBACK-OWNERSHIP]`
- When signup opening is locked, the edit field is replaced in the output by
  `.schedule-locked-value`: a two-line grid with `0.25rem` internal gap and
  `0.35rem` vertical padding; its label is muted `0.625rem` text and its value
  is soft `0.75rem` / `600` text. This is the evidenced Schedule locked-state
  pattern, not a rule that every read-only control must use this markup.
  `[SD-LOCKED-VALUE]`
- The normal form action row is owned by `.schedule-editor-actions`. Its
  winning composition is a wrapping flex row, end-aligned with `0.5rem` gap,
  `0.75rem` top margin, `0.25rem` top padding, and no top border because the
  more-specific `.schedule-editor-panel .schedule-editor-actions` rule wins
  those spacing/border properties. At `≤600px`, it starts at the inline start;
  its children retain a `2.25rem` minimum height. `[SD-ACTIONS]`
- The Event information rail reuses the general Event overview surface,
  heading, timeline/date rows, status pill, and Public pages link-row
  hierarchy by reference. The rail also carries
  `.identity-event-information-rail` for compatibility; its more-specific
  `.identity-event-information-rail .event-public-pages` rule duplicates only
  the shared `0.75rem` margin and creates no reusable family rule. The
  Schedule addendum does not copy Manage's four metric cards or its
  page-specific lifecycle/control rows. `[SD-INHERIT-MANAGE]`

### Explicit exclusions and non-goals

- The `.schedule-capacity-warning-component` and its capacity label/control
  grid are always-rendered, state-specific Schedule composition. Its `>600px`
  split label/control layout, plus the conditional warning-label, warning-row,
  warning-list, and acknowledgement action variant, are excluded from the
  generic form-family pattern. `[SD-EXCLUDE-WARNING]`
- The conditional `.schedule-confirmation-box` participant-facing preview is
  also state-specific and is not a generic confirmation, alert, or feedback
  family. `[SD-EXCLUDE-PREVIEW]`
- No metric-card rules, generic alert/feedback-family rules, new design tokens,
  new breakpoints, new process mandates, or copied legacy styles are added by
  this family contract. `[SD-EXCLUDE-NON-GOALS]`
- Hover, sequential keyboard movement, successful-save notice rendering, and
  any Schedule state not present in the normal audited output are unproven and
  remain excluded rather than inferred. `[SD-UNPROVEN]`

## Schedule-derived provenance appendix

| Marker | Schedule DOM/output and winning CSS evidence |
| --- | --- |
| `SD-SCOPE` | `Schedule.cshtml` output: `.schedule-editor-page` → `.schedule-editor-layout` → main form plus Event information `<aside>`; family boundary is the normal `/Admin/Events/Schedule/{id}` page. |
| `SD-CANVAS`, `SD-NARROW-CANVAS` | `.schedule-editor-page` at `site.css:4754`, and its `>1100px`/`≤600px` overrides at `4780` and `4792`; output root is `section.schedule-editor-page`. |
| `SD-MAIN-RAIL`, `SD-RAIL-REMOVAL`, `SD-RAIL-STICKY` | `.schedule-editor-layout`, `.schedule-event-information-rail`, and `@media (min-width:1101px)/(max-width:1100px)` rules at `4755–4777` and `4780–4788`, combined with shared `.event-overview-dates-panel` at `3101` and `3404`; output sibling main/aside relationship at `Schedule.cshtml:16–18, 162`. |
| `SD-PANEL-GROUPS`, `SD-GROUP-SURFACE` | `.schedule-editor-panel`, `.event-schedule-groups`, and final `.event-schedule-group` rules at `4758–4761`; output panel/groups at `Schedule.cshtml:21–23, 64, 101, 142`. |
| `SD-GROUP-HEADING` | Final `.event-schedule-group-heading`, `h3`, `p`, and child-width rules at `4848–4850`; output single wrapper containing `h3` + `p` for Signup window, Event schedule, and Registration capacity at `Schedule.cshtml:24–25, 65–66, 102–103`; the retroactive-reason heading at `142–145` contains only its label. |
| `SD-FIELD-GRID`, `SD-FULL-FIELD` | Admin `.event-create-fields` at `4645`, Schedule `≤600px` override at `4795`, shared `.event-create-field.full-field` at `2391` and narrow override at `2800/4911`; output normal fields and reason `full-field` at `Schedule.cshtml:27, 68, 88, 146`. |
| `SD-FIELD-PRIMITIVES` | Shared `.admin-field`/`.event-create-field` label, gap, control, focus, disabled, and readonly declarations at `4149–4182`; output `.admin-field event-create-field` nodes and labels/controls at `Schedule.cshtml:30–96, 146–149`. |
| `SD-DATETIME` | Base `.event-datetime-visual`, canonical-input, part, and input rules at `4804–4812`, with the responsive one-column adapter override at `4920` under `@media (max-width:480px)`; output date/time visual parts plus canonical input at `Schedule.cshtml:30–36, 44–50, 69–85, 89–96`. |
| `SD-CAPACITY-SCOPE` | Unconditional outer `.schedule-capacity-warning-component`, `.schedule-capacity-warning-grid`, `.schedule-capacity-label`, and `.schedule-capacity-control` at `Schedule.cshtml:101–120`; conditional `.schedule-warning-label`, `.schedule-warning-row`, and `.schedule-warning-action` at `105–130`; base/split grid rules at `site.css:4765–4772` and `4800–4802`. |
| `SD-SECONDARY-RHYTHM` | `.schedule-draft-field`, `.schedule-reason-field`, and reason-label rules at `4764, 4773–4774, 4861`; output classes at `Schedule.cshtml:88, 142–148`. |
| `SD-FEEDBACK-OWNERSHIP` | Output form-level `.validation-summary app-notice notice-error` at `Schedule.cshtml:18–20` and field `.field-error` spans throughout; shared empty-node rule at `site.css:2192–2199`. No success notice is rendered in this normal GET output. |
| `SD-LOCKED-VALUE` | Conditional `.schedule-locked-value` output at `Schedule.cshtml:40–42`; final rules at `site.css:4855–4857`. |
| `SD-ACTIONS` | Output `.schedule-editor-actions` with Cancel + Save at `Schedule.cshtml:153–157`; base/final rules at `4862–4863` and more-specific panel overrides at `4775, 4796–4797`. |
| `SD-INHERIT-MANAGE` | Schedule rail output reuses `.event-overview-section`, `.event-overview-section-heading`, `.event-overview-dates`, `.event-overview-information-summary`, `.admin-status-pill`, and `.event-public-pages` at `Schedule.cshtml:162–190`; shared winning selectors are at `site.css:3099–3148`. The output also carries `.identity-event-information-rail`, whose more-specific `.identity-event-information-rail .event-public-pages` selector at `4739–4742` duplicates only the shared `0.75rem` margin; it is compatibility styling, not a reusable family rule. Manage metric/control markup is absent from Schedule output. |
| `SD-EXCLUDE-WARNING`, `SD-EXCLUDE-PREVIEW` | Warning component and its conditional `has-schedule-warning` areas at `Schedule.cshtml:101–130`; preview is conditional on `Model.PublicPreview.Count > 0` at `132–140`. Their state-specific CSS is at `site.css:4765–4772, 4858–4860` and shared confirmation rules at `3156–3166`. |
| `SD-EXCLUDE-NON-GOALS`, `SD-UNPROVEN` | Negative evidence from the audited Schedule output and this selector-scoped audit: no new tokens, breakpoints, process rules, metric cards, generic feedback family, or unobserved interaction state is promoted. |

The Schedule addendum is intentionally selector-scoped: generic legacy CSS was
used only where the rendered Schedule selector actually inherits it and the
winning rule is named above. Unproven behavior is excluded, not inferred.

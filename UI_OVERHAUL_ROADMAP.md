# UI Overhaul Roadmap

**Status:** Approved functionality is frozen; visual and workflow refinement begins after Milestone 8.

**Related documents:** `IMPLEMENTATION_ROADMAP.md`, `PRODUCT_REQUIREMENTS.md`

## 1. Purpose

The application now contains the required version-one workflows. The overhaul will improve clarity, navigation, consistency, accessibility, and mobile use without silently changing approved bingo rules.

The work is split into small passes so each page can be reviewed, tested, and approved before the next pass begins.

## 2. Rules for the overhaul

- Preserve approved business rules unless a usability review reveals a genuine workflow defect.
- Do not redesign several unrelated workflows at once.
- Start every pass by reviewing the current page with the user before making substantial visual decisions.
- Reuse shared components rather than solving the same layout or control differently on every page.
- Keep important actions available without exposing every advanced option at once.
- Prefer a compact desktop density: buttons, inputs, cards, tables, and whitespace should use only the space required by their content and importance.
- Align related labels, values, controls, and actions to a consistent grid instead of allowing individually sized boxes to dictate the page layout.
- Design each page from narrow/mobile widths upward; horizontal scrolling is reserved for content such as full bingo boards where it is genuinely useful.
- Treat intermediate/tablet widths as a distinct responsive state, not merely a transition between desktop and phone. Multi-column layouts must collapse before their controls, cards, tables, or sidebars become compressed, clipped, overlapping, or horizontally scrollable.
- Routine actions should update the affected component in place instead of reloading the full page.
- Keep ordinary Razor forms and routes as progressive-enhancement fallbacks so the application remains reliable when JavaScript fails or accessibility tools use standard navigation.
- Use short, community-friendly language instead of implementation terms. Rewrite developer-facing names such as internal states, data-model terms, and technical actions whenever they appear in the interface.
- Review every visible label, hint, empty state, validation message, confirmation, and success/error message as part of each page pass.
- Prefer an interface that makes the next action obvious. If ordinary use requires a long explanation, first simplify the control or workflow; use help text only for rules and details that cannot be made clear through the interface itself.
- Codex may make straightforward wording improvements during an approved UI pass without waiting for separate approval, and will list notable wording changes in the handoff.
- Danish translations should preserve familiar English OSRS and community terms when a literal translation would sound unnatural. Ambiguous terms will be called out during the relevant page review so the community wording can be confirmed.
- Show the active event and, where relevant, active team prominently.
- Every mutation must provide success or failure feedback.
- Every pass must cover desktop, intermediate/tablet, narrow/mobile, keyboard, empty, loading, error, and permission states where applicable.
- A pass is complete only after focused regression tests and user approval.

## 3. Standard page-pass workflow

Each pass follows the same sequence:

1. Open and inspect the current page.
2. Confirm the page's primary user and primary task.
3. List what should remain visible, move behind secondary controls, or be removed, and identify technical or overly explanatory text that should be rewritten.
4. Agree on a compact layout or wireframe.
5. Identify which interactions navigate intentionally and which should update in place.
6. Implement shared components first, then the page-specific layout.
7. Verify desktop, intermediate/tablet, and mobile behavior, including widths immediately before and after layout breakpoints.
8. Verify keyboard focus, plain-language labels, concise confirmations and feedback, and the no-JavaScript fallback.
9. Run focused automated and manual regression tests.
10. Obtain explicit approval before starting the next pass.

## 3.1 Global density and alignment baseline

The existing interface is visually spacious but often inefficient. Pass 1 will establish a shared compact baseline before individual pages are redesigned:

- Use smaller default button heights and padding; reserve large buttons for the single primary action on a page.
- Size inputs according to their data rather than stretching every field across the available width.
- Use compact cards/panels and avoid placing every small setting in its own large box.
- Use consistent label, field, value, and action columns for desktop forms.
- Collapse to a logical single-column reading order on narrow screens.
- Keep primary actions aligned predictably; place rare/destructive actions in a secondary menu or clearly separated area.
- Reduce table row height while preserving readable click/touch targets.
- Avoid large decorative headings consuming most of the initial viewport on operational pages.
- Default admin workflow and operational subpages to a compact heading row with a small yellow page label. Put explicit return links at the far right of the breadcrumb row instead of letting them consume page-heading space. Keep the large title-and-description treatment only on landing pages, overviews, or pages where it materially helps orientation.
- Prevent footers, sticky panels, dialogs, and toolbars from overlapping content at short viewport heights.

## 3.2 In-page interaction baseline

The overhaul will not turn the application into a separate single-page frontend. It will progressively enhance high-frequency interactions using small JavaScript modules, `fetch`, partial responses/JSON, and existing SignalR updates.

**Normally update in place**

- Search, filtering, sorting, tabs, accordions, dialogs, and expandable details.
- Board tile swapping, tile editing, line highlighting, statistics, and lease status.
- Draft picks, undo, pause/resume, control status, and roster summaries.
- Evidence queue decisions, visibility changes, request-change feedback, and queue counts.
- Captain submission status cards where the user remains in the same workflow.
- Capacity/waiting-list counts and other small event-management changes.

**Normally allow navigation or a deliberate refresh**

- Moving to a genuinely different page or workflow.
- Authentication and logout.
- Large lifecycle transitions such as finalizing a draft/event or archiving an event, although the resulting page should return with clear feedback and preserved context.
- Recovery fallback when an in-page update fails.

Every enhanced action must prevent double submission, show progress, show success/failure, update only after the server accepts the action, and preserve the standard server-side authorization and validation path.

### Compact action rules

- Save simple, independent settings automatically when a dropdown, toggle, or small numeric value changes. Use a short debounce when several nearby values belong to one setting.
- Remove redundant **Save** and **Update** buttons once reliable in-place saving and clear success/error feedback are available.
- Use a compact × removal control when the item being removed is unmistakable from context. Provide an accessible label, tooltip, and confirmation before destructive removal.
- Keep explicit action buttons for multi-field forms, creation flows, major lifecycle transitions, and destructive actions whose meaning would not be clear from an × icon.
- Preserve a standard Razor form fallback for automatically saved controls, and never display a successful local change until the server has accepted it.

## 3.3 Feedback and notification baseline

The current shared status box treats every message as success. Pass 1 will replace it with a typed notification model used by both full-page responses and in-page actions:

- **Success:** green treatment for an action that completed successfully.
- **Error:** red treatment for validation failures, rejected operations, and unexpected failures.
- **Warning:** amber treatment for risky states or actions that need attention but did not fail.
- **Information:** neutral/blue treatment for explanatory state changes and non-error notices.
- Use a visible label or icon as well as colour so meaning does not depend on colour perception.
- Use `role="status"` for non-urgent success/information and an appropriate alert treatment for errors.
- Keep field-specific validation next to the relevant control and use the page notification for the concise overall result.
- Preserve the notification type across redirects and return the same semantic type from in-page endpoints.

## 3.4 Role-based action inbox

Pass 1 will reserve a compact top-right action-inbox control for authenticated users. It is an outstanding-work list, not a permanent social notification feed.

**Admin inbox**

- Show the count of submissions awaiting review for the active event.
- List the newest actionable submissions with team, player, tile/drop, and submission time.
- Link each item directly to its review details.
- Link to the complete review queue.
- Do not mix completed events into the active-event count.

**Captain/co-captain inbox**

- Show the count of own-team submissions in `ChangesRequested` state.
- Show the tile/drop and concise reviewer note for each item.
- Link directly to the correction/resubmission page.
- Never reveal another team's private submission state.

**Shared behavior**

- Update counts and items through SignalR when submission/review state changes, with a lightweight refresh fallback.
- Use a visible badge and accessible text; do not rely on colour alone.
- An item remains outstanding until its underlying workflow state changes. Version one does not add a separate read/unread state or “mark all read” behavior.
- Keep the active event/team visible in the menu so notifications cannot be mistaken for another event.
- Empty state clearly says that nothing currently needs attention.

## 3.5 Page approval checklist

Apply this checklist to every page before it is approved. During the final regression, apply it again to pages approved earlier in the overhaul.

**State and data consistency**

- After an accepted change, every visible copy of the affected value, count, status, badge, card, table row, dropdown option, and summary updates together.
- A change made inside a dialog is reflected on the page behind it without requiring a manual refresh.
- Controls appear, disappear, lock, and unlock at the correct lifecycle state; the server enforces the same rules as the interface.
- Empty, partially configured, completed, and corrected data render safely, including records created before later fields or questions existed.
- Dates and times use the event timezone, 24-hour display, and the expected rounding rules without browser-specific shifts.

**Interaction continuity**

- Routine changes do not reload the full page when an in-place update is practical.
- Scroll position, open dialogs, expanded sections, selected tabs, search/filter/sort state, and useful keyboard focus survive an update where appropriate.
- A dialog remains open after adding, removing, or editing one of its items unless completing the action intentionally ends that workflow.
- Browser Back returns to the previous meaningful page rather than replaying each POST or local edit.
- Loading state prevents duplicate submissions without leaving a control permanently disabled after failure.

**Actions and feedback**

- Every mutation provides accurate success or failure feedback that remains visible while scrolled, can be dismissed, and uses the correct semantic type.
- Field validation appears beside the relevant field, preserves entered values, and keeps the user in the same context.
- Simple independent values save on change; redundant Save/Update buttons are removed. Multi-field and major lifecycle actions retain an explicit button.
- A compact red × is used only when the item being removed is unmistakable. It has an accessible name, tooltip, confirmation, and the correct server-side safeguards.
- Confirmation text states meaningful side effects, such as promoting a waiting-list player or removing a team.

**Language and presentation**

- Enum and implementation names never leak into visible text; labels such as “Co-captain” are formatted consistently everywhere.
- Wording is short, casual, and clear to community members, while OSRS/community terminology remains familiar.
- Buttons, inputs, cards, headings, spacing, alignment, and action placement match the shared patterns used on adjacent pages.
- Review every button during final regression: equivalent actions use the same height, padding, typography, border treatment and alignment; primary, secondary, destructive and compact actions follow one consistent hierarchy across the site.
- Desktop, intermediate/tablet, and mobile layouts do not clip, overlap, jump unexpectedly, or create avoidable horizontal scrolling.
- Resize continuously between representative widths instead of checking only fixed desktop and phone sizes; no narrow range may retain a desktop grid after its children no longer fit.
- Long realistic names, zero results, full lists, and large test datasets remain readable.

**Accessibility and fallback**

- Keyboard navigation follows the visual order; Space/Enter operate only the focused control and do not accidentally toggle a parent section.
- Focus is visible but not oversized, and opening/closing a dialog returns focus sensibly without leaving a stray page outline.
- Icons and colour are never the only explanation of status or action.
- The standard Razor form fallback remains usable when enhanced JavaScript is unavailable or fails.
- Permission failures, stale/concurrent changes, network failures, and unexpected errors produce a useful page or message rather than a blank screen.

## 3.6 Site-wide flagship visual system

Pass 12 establishes the visual language for the entire application, not only the public event pages. The approved reference surface is the **View bingo** state at `/Events/test-15-dkl-live/Board`. Its implemented rules are the source of truth for every later public, captain, signup, authentication, error, account, and administration pass. Pages may use a denser composition when their task requires forms, tables, or operational controls, but they must remain recognizably part of this same product.

The reference is a system, not a screenshot to imitate loosely. Future work must reuse its colour roles, typography, content width, spacing rhythm, outlines, radii, control sizing, state treatments, interaction feedback, responsive transitions, and motion rules. Do not preserve the older brown/gold visual language on pages merely because they have not yet been migrated. Do not create a second visual system for admin pages. Administrative density is a layout variant within this system.

The exact implemented values remain centralized under `body.public-event-shell` and the related shared selectors in `wwwroot/css/site.css`. As the overhaul moves site-wide, promote reusable values and components to shared tokens/selectors rather than copying `public-*` declarations into page-specific CSS. If the roadmap text and implementation drift, compare the page with the TEST 15 View bingo reference, resolve the discrepancy deliberately, and update both the shared implementation and this section.

### Reference composition and density

At the reference desktop viewport of `1280 × 720`, the shared content region is `1116px` wide. The layout leaves approximately `82px` at each side through the existing responsive container. This is the wide application canvas for content-rich screens; narrower reading and form widths may be used intentionally, but arbitrary one-off widths are not allowed.

- The application body is a full-height flex column with no outer margin, `16px` root text, `1.5` default line height, and no decorative strip behind the centered content.
- The primary page canvas is continuous `--page`. Navigation and panels sit on that canvas; a page must not introduce a competing full-width background band without a semantic reason.
- The reference dashboard uses `1.25rem` (`20px`) top padding and `5rem` (`80px`) bottom padding. On screens at or below `760px`, top padding reduces to `0.5rem` (`8px`).
- The event masthead is compact: minimum `5.75rem` (`92px`) high with `0.75rem 0 1rem` (`12px 0 16px`) padding and a `0.85rem` (`13.6px`) identity gap. At or below `760px`, it loses the fixed minimum and uses `0.85rem 0 1rem` padding.
- Major view navigation sits directly below the masthead with a `2rem` (`32px`) item gap, `1.5rem` (`24px`) bottom margin, and a single one-pixel divider. Tabs use `0.8rem 0.1rem` (`12.8px 1.6px`) padding. At or below `760px`, tabs become a contained horizontal scroller with a `1.25rem` gap and `0.7rem 0.05rem` padding; at or below `540px`, the gap becomes `1rem`.
- The reference team-card grid uses three equal columns with a `0.8rem` (`12.8px`) gap. It changes to two columns at `1100px` and one column at `760px`. Equivalent repeated-content layouts should use the same three/two/one rhythm unless their content has a documented minimum width requiring an earlier transition.
- Standard feature cards use `0.85rem` (`13.6px`) internal padding, a `0.6rem` (`9.6px`) internal gap, a one-pixel outline, and `0.8rem` (`12.8px`) radius. Dense nested regions use the established `0.3rem`–`0.65rem` gaps rather than adding large empty zones.
- Ordinary page regions should generally separate related cards by `0.75rem`–`1rem` (`12px`–`16px`). Major page transitions may use `1.5rem`–`2rem` (`24px`–`32px`). Avoid oversized `3rem+` spacing unless it communicates a genuine section boundary.
- Panel content should begin close to its outline. Do not reproduce the earlier oversized boxes, padded wrappers within padded wrappers, or large headings that push the primary task below the initial viewport.

### Spacing scale

Use the following reference scale before introducing another value:

| Token role | Value | Typical use |
| --- | --- | --- |
| Hairline | `1px` | Panel, field, table, tab, and separator outlines |
| Micro | `0.15rem`–`0.2rem` | Board inset, progress details, icon/text adjustment |
| Tight | `0.3rem`–`0.4rem` | Dense metadata, inline controls, tile gaps |
| Compact | `0.55rem`–`0.65rem` | Card internals, compact control groups, mobile gaps |
| Standard | `0.75rem`–`0.85rem` | Card padding, related panels, header identity gaps |
| Section | `1rem`–`1.5rem` | View headings, section transitions, tab separation |
| Major | `2rem` | Major view separation only |
| Page end | `4rem`–`5rem` | Safe bottom breathing room |

Values between these steps are acceptable when an existing shared component requires them. A new page must not invent an unrelated spacing scale. Align repeated edges and use the same padding for equivalent cards, rows, fields, and actions.

### Colour roles

| Role | Token/value | Use |
| --- | --- | --- |
| Page canvas | `--page: #101114` | Continuous near-black background across the full viewport. Do not introduce centered strips with a different page colour. |
| Navigation and panels | `--surface: #1a1c21` | Global header, standard cards, menus, tables, and dialogs. |
| Raised controls | `--surface-raised: #22252b` | Inputs, selected/raised regions, medallions, and subtle nested surfaces. |
| Primary text | `--text: #f2eee7` | Titles, important values, labels, and active navigation. |
| Secondary text | `--muted: #a6a7ab` | Supporting copy, timestamps, metadata, and inactive navigation. It must remain readable and must not carry essential meaning alone. |
| Outline | `--border: #354153` | The shared one-pixel blue-grey outline for navigation, cards, fields, tables, dialogs, and separators. |
| Interactive accent | `--accent: #8299c2` | Active tabs, links, focus indicators, primary public actions, and progress bars. Use sparingly so it remains meaningful. |
| Complete/success | `--success: #57bd71` | Completed tiles and confirmed success only. |
| In progress | `--progress: #c6924f` | Partially completed tiles and active work only. |
| Destructive/error | `--danger: #e47f76` | Destructive actions, rejected/error state, and validation failures. |
| Warning | `--warning: #e4b85f` | Risk, attention, and irreversible lifecycle warnings. |

- The blue-grey system is the site-wide product identity; do not restore the old brown/gold palette on pages that are migrated through the overhaul.
- Status cannot rely on colour alone. Full boards use text/counts and accessible labels; compact overview boards may use colour alone only as a deliberately simplified preview that links to the detailed board.
- First, second, and third place use restrained gold `#d8b65d`, silver `#c4c1ba`, and bronze `#c58a62` text on otherwise identical medallions. Lower ranks use the normal steel accent.
- Private evidence uses the same surface family with a subtle striped treatment; never expose the hidden image or player through background art, labels, or accessible text.

### Typography and hierarchy

- Use `Inter`, followed by `ui-sans-serif`, `system-ui`, `-apple-system`, `BlinkMacSystemFont`, `"Segoe UI"`, and `sans-serif`. Typography is part of the visual identity; do not substitute a display, serif, condensed, or game-themed font unless the user approves a new site-wide identity.
- Use the following type scale as the default vocabulary. A component may interpolate responsively through the documented `clamp()` values, but it must not introduce an unrelated size, weight, or line height merely to fit a local layout.

| Role | Size | Weight | Line height/tracking | Reference use |
| --- | --- | --- | --- | --- |
| Root/body | `1rem` (`16px`) | `400` | `1.5` (`24px`) | Ordinary prose, form values, default controls |
| Event/page title | `clamp(1.55rem, 2.4vw, 2.1rem)` | `800` | `1.08`, `-0.025em` | Event and team identity; `1.92rem`/`30.72px` at the 1280px reference viewport |
| Major section title | `clamp(1.55rem, 2.5vw, 2.15rem)` | `700`–`800` | approximately `1.1`, `-0.025em` | One primary heading for a major view region |
| Subsection title | `1.25rem`–`1.5rem` | `700`–`800` | `1.15`–`1.25` | Dialog, panel group, or major table heading |
| Card title | `0.92rem`–`0.95rem` | `700` | approximately `1.2`–`1.5` | Team, recent-drop, and compact panel titles |
| Standard control/row text | `0.76rem`–`0.82rem` | `400`–`750` | `1.5` | Tabs, buttons, table rows, compact navigation |
| Supporting metadata | `0.7rem`–`0.76rem` | `400`–`700` | `1.45`–`1.5` | Dates, status detail, secondary labels |
| Dense statistic label | `0.66rem` | `400`–`700` | `1.5` | Card footer labels and compact summaries |
| Eyebrow/feature kicker | `0.66rem` | `800` | `1.5`, `0.1em`, uppercase | Short categorical context only |
| Dense microcopy | `0.6rem`–`0.65rem` | `400`–`700` | at least `1.4` | Secondary board or ranking detail; never primary instructions |

- At or below `540px`, the reference event title becomes `1.45rem`; event metadata becomes `0.7rem`. Other headings should step down with the same restraint and preserve their hierarchy.
- Use only purposeful weights: `400` for ordinary text, `700` or `750` for actionable/structural emphasis, and `800` for primary identity, ranks, and kickers. Avoid scattered intermediate weights that make equivalent components look different.
- Primary text uses `--text`; supporting text uses `--muted`; interactive or categorical text may use `--accent`. Never use reduced font size and muted colour together when the information is essential to completing a task.
- Keep line length comfortable: explanatory prose generally stays below `65ch`–`75ch`; compact metadata truncates with an ellipsis only when the full value remains available through context, title, expansion, or its destination.
- Single-line card identities use `overflow: hidden`, `text-overflow: ellipsis`, and `white-space: nowrap` so long names do not change repeated-card geometry. Multi-line titles may clamp deliberately, normally to two lines.
- Use primary text for the current task and key values, muted text for explanations, and accent text for interaction or ranking—not for decorative paragraphs.
- Keep copy short. A heading should not be repeated immediately below the tabs when the tab and page content already make the view clear.
- Reference event metadata and primary tabs use `0.82rem`; team names use `0.92rem`; compact card footer labels use `0.66rem`; their values use `0.76rem` and weight `700`.
- Rank medallions on overview cards are `2.1rem` square with `0.78rem`, weight-`800` text. Larger page-level identity medallions may use `3rem`; do not use oversized badges as decoration.
- Uppercase eyebrow/kicker text uses `0.66rem`, weight `800`, `0.1em` letter spacing, and the accent colour. Use it for short categorical context, not paragraphs.
- Preserve hierarchy through weight, size, colour, and spacing together. Avoid relying on huge headings, all-caps body text, or repeated boxed headings.

### Numeric formatting

- Public summary EHB is a scan value, not a calculation worksheet. Round it to the nearest whole EHB with no decimal places: `741.08` displays as `741 EHB` and `185.65` displays as `186 EHB`.
- Apply whole-number EHB consistently to overview cards, standings, public leaderboards, team summaries, and other compact visitor/captain statistics. Do not mix `741`, `741.1`, and `741.08` for the same metric across neighboring views.
- Preserve the underlying decimal value for ranking, comparison, storage, and calculations. Formatting must never change the authoritative value or create artificial ties.
- Detailed board-editor, catalogue, audit, diagnostic, and calculation-explanation views may show up to two or four decimal places when that precision helps an administrator verify inputs or reproduce a calculation.
- Use grouping separators for values of `1,000` or more where locale-aware formatting supports them. Keep units adjacent and explicit (`741 EHB`, `25 tiles`, `10 lines`) and never rely on column position alone to explain an unfamiliar number.

### Surfaces, outlines, and spacing

- Standard public panels use `--surface`, a one-pixel `--border` outline, a restrained radius around `0.65rem`–`0.8rem`, and only a faint inset highlight. Nested surfaces use `--surface-raised` or a nearby mixed tone.
- Shadows communicate elevation only for overlays, dialogs, menus, or a hovered clickable card. Ordinary panels should be defined primarily by the shared outline.
- The global navigation uses the standard surface colour. Event mastheads stay on the page canvas; do not create a second full-width coloured header block.
- Dividers stay inside the same content width as the elements they separate. Avoid unexplained full-viewport rules.
- Prefer compact, repeatable spacing: approximately `0.6rem`–`0.85rem` inside dense cards and `0.8rem`–`1rem` between related cards. Larger spacing is reserved for transitions between major page regions.
- Header popovers are part of the shared shell and must render above every page-local stacking context. Page cards, transforms, dialogs, and sticky controls must not obscure them.
- The reference card background is a restrained diagonal mix from the raised surface into the standard surface. Use gradients only to add depth within the same surface family; do not introduce unrelated colourful gradients.
- Standard feature-card hover raises the card by `3px`, strengthens the outline toward the accent, and adds a restrained `0 0.8rem 2rem rgba(0,0,0,.24)` shadow. Static information panels do not lift.
- The normal panel radius is approximately `0.65rem`–`0.8rem`. Compact nested controls may use `0.4rem`–`0.6rem`. Pills are reserved for true progress tracks, statuses, and compact filters—not ordinary rectangular controls.
- The reference overview board preserves its exact row/column aspect ratio and uses a `0.18rem` gap. Its tile corners are `0.24rem`. Detailed boards and content cards may use larger gaps and radii while retaining the same underlying geometry.

### Buttons and interactive controls

The site-wide system must provide the following reusable hierarchy even when a particular page has no buttons:

1. **Primary:** one clear next action per region. Use a filled `--accent` treatment with high-contrast text. Hover may lighten the border/background slightly; pressed may darken slightly. Never flash to solid white or unrelated Bootstrap blue.
2. **Secondary:** transparent or surface-filled with a `--border` outline and primary text. Hover uses a subtle accent-tinted background and accent-strengthened border.
3. **Quiet/text:** navigation, back, cancel, reveal, or low-priority actions. Use text/accent colour with no permanent filled block; hover adds only a faint tint.
4. **Success:** reserved for an action whose meaning is explicitly successful/complete, not as a generic primary colour.
5. **Warning:** amber outline or restrained fill for risky lifecycle decisions. Warning is not a substitute for primary emphasis.
6. **Danger:** transparent or restrained red tint with `--danger` text/border. Destructive actions require plain-language confirmation and must never become a solid white hover state.
7. **Icon-only:** square, compact controls only when the icon is conventional and context is clear. Always provide an accessible name and visible focus treatment.

- Regular buttons share one height, padding, radius, weight, and alignment within a region; compact buttons use one smaller shared size. Do not size equivalent buttons from their label length.
- Aim for a visual height near `2.35rem`–`2.5rem`; at touch-oriented narrow widths the target must be at least `44px` high or receive equivalent surrounding hit area.
- Use a `150ms` colour/border/background transition. Hover is subtle, pressed is visibly but briefly darker, and focus uses the shared two-pixel accent-mixed outline with a two-pixel offset.
- Disabled buttons keep their semantic colour family at reduced contrast, do not respond to hover/press, and remain distinguishable from enabled actions.
- Button labels describe the result (`Submit for review`, `Open team board`, `Save changes`) rather than generic `Continue` or icon-only wording.
- Reuse these variants in links, forms, dialogs, empty states, team switching, evidence controls, and leaderboard actions. Do not create one-off page button CSS when a shared variant applies.
- Tabs are navigation, not buttons. The reference treatment uses muted text, no filled container, and a two-pixel accent underline for the active state. Use filled segmented controls only when switching data modes inside a contained panel.
- Inputs, selects, textareas, menus, and dialogs must use the same raised surface, one-pixel blue-grey outline, text hierarchy, radius family, and focus treatment as buttons and panels.
- Tables use the same panel boundary and one-pixel internal separators. Header rows use a subtle raised-surface mix rather than a strongly contrasting band. On narrow screens, adapt columns intentionally instead of shrinking text below the shared readable sizes.
- Destructive confirmation, validation, empty, loading, permission, stale-concurrency, and no-JavaScript states are part of the system. They must use the same surfaces and spacing rather than falling back to browser-default or legacy Bootstrap presentation.

### Board and ranking components

- Compact overview boards preserve the board's true square grid. Cells remain square at every board dimension; 4×4 boards must not stretch cells vertically to fill a card.
- Untouched cells use the standard dark tile surface and outline. In-progress cells use the restrained amber mix; completed cells use the restrained green mix.
- The overview intentionally omits per-cell numeric progress. Detailed team/tile views carry exact progress, requirements, evidence, and credited-player information.
- Team cards use the same structure for internal and external teams. Do not label a team “external” unless the affiliation is directly useful in that view.
- Rank, team identity, board, completion bar, and the compact tiles/lines/Drop EHB footer remain aligned and use identical geometry across all cards.
- Overview card headers use an auto/minmax/auto grid with a `0.7rem` (`11.2px`) gap. Long identity text truncates instead of changing card geometry.
- The compact board, header, progress bar, and footer share the same centered maximum width (`min(100%, 26rem)`) so their edges align.
- The overview completion track is `0.22rem` high with a fully rounded end. Footer statistics use three equal columns with a `0.3rem` gap and remain on one line where the card width permits.
- The first three ranks use identical medallion geometry; only their restrained gold, silver, and bronze text colour changes. Rank must not distort the surrounding card or add redundant labels such as `Leading` when `#1` already conveys the state.

### Responsive and motion rules

- The public overview uses three team cards per row when they fit comfortably, two at intermediate widths, and one on narrow screens. Change layout before labels, controls, or cards overflow; do not preserve a broken desktop grid until the phone breakpoint.
- Test continuous resizing immediately before and after each breakpoint, with six teams, 5×5 boards, long names, empty data, and mixed progress states.
- Tables may become stacked records or contained horizontal scrollers on narrow screens, but the page itself must not scroll horizontally.
- Motion is short and functional: subtle card lift/focus and, when implemented, an overview-to-team transition that preserves spatial context. Respect `prefers-reduced-motion` and provide an immediate transition.
- Hover-only information must also be available through focus, touch, visible text, or the detailed destination.
- Breakpoints are content-driven shared thresholds, not device names. The current baseline uses `1100px` for three-to-two card columns and leaderboard stacking, `900px` for the team-board summary transition, `760px` for single-column cards and compact navigation, and `540px` for the narrowest summary/navigation refinements.
- Test at the breakpoint, immediately above it, immediately below it, and during continuous resizing. Passing a single desktop screenshot and a single phone screenshot is insufficient.
- The page itself must never acquire horizontal scrolling. Only deliberately contained regions such as a detailed bingo board, wide table, or tab row may scroll horizontally, and those regions need keyboard focus visibility and inline overscroll containment.
- Standard colour, border, and background transitions use `150ms`. Reference card movement uses `140ms`–`150ms`. Do not add long, decorative animation that delays work.
- Under `prefers-reduced-motion: reduce`, remove card and tile transforms and nonessential transitions while keeping all state changes immediately understandable.

### Site-wide migration rules

- Every remaining UI pass must begin by applying the shared flagship shell and tokens, then choose an appropriate composition: public showcase, compact workflow, dense table, form, or dialog. Those are density variants, not separate themes.
- Existing approved functionality, authorization, validation, lifecycle protections, and progressive enhancement remain fixed while the visual layer migrates.
- Reuse or extract shared components for page mastheads, tabs, cards, notices, buttons, fields, tables, dialogs, empty states, progress, ranks, and responsive containers. Do not duplicate the TEST 15 CSS under new page-specific class names.
- New one-off colour, spacing, radius, shadow, type size, breakpoint, or control pattern requires a reason that the shared system cannot express. If that reason is valid and reusable, add it to the shared system and document it here.
- Public and captain boards should be closest to the reference composition. Signup and authentication pages may use a narrower centered column. Admin pages may use denser cards, tables, and multi-column workspaces. All retain the same canvas, surfaces, borders, typography, controls, state colours, focus treatment, spacing scale, and interaction timing.
- A page is not approved merely because it uses the right colours. It must also match the reference density, alignment discipline, component geometry, feedback quality, responsive transitions, keyboard behavior, and absence of unnecessary visual bulk.
- The TEST 15 View bingo page remains the visual comparison baseline until the user explicitly approves a replacement reference. Later refinements to that reference must be reflected in shared tokens/components and this roadmap before they are propagated.

## 4. Overhaul sequence

### Pass 1 — Global shell and event context

**Pages/components**

- `Pages/Shared/_Layout.cshtml`
- Shared navigation, breadcrumbs, status messages, dialogs, form controls, tables, and page headers
- `Pages/Admin/Index.cshtml`
- `Pages/Admin/Events/Index.cshtml`

**Goals**

- Establish the final visual system before editing individual workflows.
- Separate global navigation from event-specific navigation.
- Make the selected event and its lifecycle state visible throughout admin workflows.
- Provide a clear route back to the event overview.
- Define consistent page widths, spacing, headings, buttons, notices, tabs, tables, and empty states.
- Establish compact sizing tokens and responsive grid primitives used by every later pass.
- Establish the shared in-page action pattern for loading, success, validation errors, conflicts, and fallback navigation.
- Replace the single green status box with shared success, error, warning, and information notifications.
- Establish the top-right role-based action inbox shell and empty/loading/error states.
- Add role-aware breadcrumbs below the main navigation on deeper pages. Admin paths show the full hierarchy, such as `Admin tools → Events → Event name → Board editor`; captain and public paths use shorter trails appropriate to their shallower workflows.
- Make every previous breadcrumb step clickable and render the current page as plain text. Do not show breadcrumbs on landing pages where they add no useful context.
- On narrow screens, prioritize the immediate parent and current page while keeping the full path accessible.
- Keep the skip link accessible without allowing it to obscure focused navigation.
- Reduce the aggressive focus outline while retaining clear keyboard focus.
- Use the concise signed-out navigation label “Sign in” instead of “Admin / captain sign in”.

**Approval gate**

- User approves the global shell, admin landing page, event list, event context pattern, and shared component direction.

### Pass 2 — Event creation

**Pages**

- `Pages/Admin/Events/Create.cshtml`

**Goals**

- Reduce the current large-box/bloated presentation.
- Group essential event information, signup settings, schedule, and optional planning information logically.
- Generate the slug silently from the event name.
- Use the combined date-time picker for signup and event dates.
- Keep times easy to choose in 30-minute increments while allowing precise corrections elsewhere.
- Hide or disable the signup code field when code protection is off.
- Explain private signup editing with a compact information control.
- Include standard signup fields and make custom questions clearly secondary.
- Make it clear that publishing/opening an event only exposes event information and signup.

**Approval gate**

- User can create a correctly configured event without needing developer terminology or scrolling through unnecessarily large controls.

### Pass 3 — Event overview and operations

**Pages**

- `Pages/Admin/Events/Manage.cshtml`

**Goals**

- Turn the current all-in-one manager into a clear event overview.
- Show lifecycle state, important dates, signup numbers, board state, draft state, submission state, and items requiring attention.
- Move secondary operations into clearly labelled sections or event navigation destinations.
- Make start, end, reopen, and final-review actions easy to locate and hard to trigger accidentally.
- Show explicit success feedback for every operation.
- Keep evidence-code management understandable without dominating the page.
- Make links name the destination event rather than relying on surrounding context.

**Approval gate**

- User can understand the event's current state and next likely admin action at a glance.

### Pass 4 — Signup administration

**Pages**

- `Pages/Admin/Events/Questions.cshtml`
- `Pages/Admin/Events/Participant.cshtml`
- `Pages/Admin/Events/Csv.cshtml`
- Signup-management portion of `Pages/Admin/Events/Manage.cshtml`

**Goals**

- Visually separate confirmed participants and waiting-list entries.
- Make capacity changes and automatic promotions clear.
- Make participant removal, withdrawal, payment state, captain volunteering, comments, and secondary account details readable.
- Replace question-type implementation language with plain-language choices and examples.
- Explain “choice per line” or replace it with a clearer editing interaction.
- Keep the fixed signup fields visible and non-removable.
- Treat CSV as a low-priority advanced import path with a documented expected format.
- Keep website signups and externally created roster members conceptually separate.

**Approval gate**

- User can manage signup capacity, waiting lists, participants, and questions without confusing website participants with external team members.

### Pass 5 — OSRS catalogue

**Pages**

- `Pages/Admin/Catalogue/Index.cshtml`

**Goals**

- Keep “Add boss or activity” as the clear catalogue entry point.
- Make source search and drop management compact and fast.
- Keep “Add drop” within its boss/activity context and use a focused dialog.
- Display efficient completions per hour, drop rate, probability, and calculated EHB clearly.
- Remove the legacy standalone tile-template workflow. The catalogue remains the reusable OSRS data source, while event tiles are created directly in the board editor.
- Remove the obsolete catalogue CSV importer. Keep the reviewed Wiki maintenance workflow unlinked from the everyday catalogue until it is placed under advanced administration tools.
- Make deactivate/reactivate actions distinct from ordinary editing.
- Reserve a consistent image area for every boss or activity while the catalogue UI is reviewed.
- Reserve a compact image area for every drop and allow its shared catalogue item name to be corrected without cluttering the card.
- After this page is approved, import the matching OSRS Wiki images for the existing catalogue and replace the temporary image placeholders.
- Catalogue images may be supplied by URL or uploaded from a device. Store only the resulting image URL/storage key in PostgreSQL; local development uses local object storage and production uses Cloudflare R2 rather than storing image bytes in the database.
- Rebuild the seeded catalogue from a reviewed OSRS Wiki dry-run: preserve boss names and clan EHB rates, replace seeded drop rows with selected special/unique rewards, retain Wiki images and attribution, and require manual review for conditional or variable rates.
- Before applying the reviewed Wiki import, add structured per-drop rate variants (label/context, displayed rate, numeric probability and condition) so delve, raid-scale and other conditional rates are not flattened into one misleading value.
- After the Wiki catalogue pull is complete, add a duplicate-name correction flow. If an item is renamed to an existing shared item, show a confirmation popup that can move the boss-specific drop connection and rate data to the existing item, then remove the misspelled item only when it is no longer used.

**Approval gate**

- User can find a boss, add or edit a drop, and understand its EHB calculation. Reusable tile records remain an internal board-editor detail rather than a separate admin workflow.

### Pass 6 — Board editor

**Pages/components**

- `Pages/Admin/Events/Board.cshtml`
- `Pages/Admin/Events/_BoardRequirementEditor.cshtml`

Tile reuse should be provided through practical board actions such as duplicating a tile or copying one from a previous bingo, rather than a separate template catalogue.

**Goals**

- Keep the board itself as the primary workspace.
- Create a tile from its empty board position rather than from a detached global form.
- Edit a populated tile by selecting it while preserving board context.
- Revisit popup versus side-inspector behavior after testing both at realistic desktop widths.
- Keep boss selection as a searchable chooser and group eligible drops by boss.
- Provide select-all/deselect-all drop controls.
- Make objective wording community-friendly.
- Keep manual quantities, duplicate rules, higher weighting, multiple bosses, and multiple requirement groups understandable.
- Make drag-on-tile swapping feel immediate without a disruptive full-page refresh.
- Preserve an accessible non-drag alternative for keyboard and touch users.
- Keep row/column hover highlighting, EHB values, total EHB, EHB per expected player, and balancing warnings readable.
- Review the tile EHB calculations themselves, not only how the values are displayed.
- Verify EHB behavior for quantities, multiple bosses, combined drop selections, duplicate restrictions, weighted drops, manual objectives, and requirement groups such as Voidwaker pieces and Barrows plus Moons.
- Make it clear which catalogue boss rates and drop probabilities produced a tile's EHB, when an admin has overridden a value, and why a tile cannot be calculated automatically.
- Confirm that tile, row, column, total-board, and per-player EHB remain consistent after editing or rearranging tiles.
- Make board resizing explain and block tile loss clearly.
- Keep editing leases visible without overwhelming admins who only want to inspect the board together.

**Approval gate**

- User can build and rebalance the canonical edge-case board without referring to documentation.

**Status:** Approved by the user on 2026-07-22 after iterative desktop, narrow-width, dialog-state, weighting, editing-lease, catalogue-rate, and historical-board checks. Cross-application keyboard, accessibility, and final responsive regression remain part of the milestone-wide closing pass.

### Pass 7 — Teams and snake draft

**Pages**

- `Pages/Admin/Events/Draft.cshtml`

**Goals**

- Separate draft setup, pre-formed teams, live draft, and finalized rosters into understandable states.
- Always show whether team counts include or exclude pre-formed teams.
- Keep all participants visible during drafting, with clear drafted/available status and EHB ordering.
- Make confirmed participants and waiting-list members visually distinct.
- Keep external clan rosters separate from the website signup pool.
- Revisit the external-team import workflow, including the source format, field mapping, validation, duplicate handling, roster roles, and a clear preview before anything is added to the event.
- Place the rare “use an internal participant in a pre-formed team” action behind an advanced path.
- Make scramble, snake order, current pick, undo, pause, takeover, and finalization status obvious.
- Show who controls the draft and make observers clearly read-only.
- Make post-draft manual team additions understandable without suggesting they alter draft history.

**Approval gate**

- Two admins can observe/control a draft and explain the state of every participant and team without ambiguity.

**Status:** Approved by the user on 2026-07-22 after reviewing setup, active-control, finalized-roster, button-state, desktop, intermediate-width, and narrow-width scenarios. Cross-application keyboard, accessibility, and final responsive regression remain part of the milestone-wide closing pass.

### Pass 8 — Captain workspace and submission

**Sequencing note:** Complete and approve Pass 12's public board visual treatment before this pass. The captain/team board should reuse that treatment rather than establishing a separate board design; this pass then adds the authorized submission and team-workspace states around it.

**Pages**

- `Pages/Captain/Index.cshtml`
- `Pages/Captain/Submit.cshtml`
- `Pages/Captain/Submission.cshtml`

**Goals**

- Always show event, team, captain account mode, submission cutoff, and correction-only state.
- Make team progress and pending/changes-requested submissions easy to scan.
- Make pending submissions clickable for inspection and editing.
- Populate the captain action inbox with own-team changes-requested submissions and direct correction links.
- Streamline screenshot paste, drag/drop, and file selection.
- Eliminate the drop-zone focus behavior that accidentally opens Finder.
- Keep credited player, tile, requirement, drop, weight, privacy request, and note readable.
- Explain duplicate rules and higher weight only when relevant.
- Make request-change feedback and resubmission history clear.
- Provide clear success feedback for submit, edit, withdraw, and replacement actions.

**Approval gate**

- A captain can submit a Discord screenshot with minimal file handling and understand every submission state on desktop and mobile.

### Pass 9 — Admin evidence review

**Pages**

- `Pages/Admin/Review/Index.cshtml`
- `Pages/Admin/Review/Details.cshtml`
- `Pages/Admin/Review/Submit.cshtml`
- `Pages/Evidence.cshtml`

**Goals**

- Keep the current active event visible throughout review.
- Populate the admin action inbox with the active event's pending-review submissions and live counts.
- Default to newest submissions first without an unnecessary event filter.
- Make queue cards/rows clickable outside the action button.
- Show credited player alongside boss/drop/requirement information, not only in the heading.
- Make the screenshot large enough to inspect and clickable into a full-size lightbox.
- Keep evidence code at immutable submission time visible when enabled.
- Distinguish pending, changes requested, approved, rejected, withdrawn, reversed, duplicate, grace-period, and privacy states.
- Show immediate confirmation after request changes, approve, reject, reverse, duplicate marking, or visibility changes.
- Keep the one-click resolution path efficient while protecting destructive/reversal actions with confirmation.

**Approval gate**

- An admin can work through the queue quickly without losing event, player, tile, or evidence context.

### Pass 10 — Event closeout and account administration

**Pages**

- `Pages/Admin/Events/Finalize.cshtml`
- `Pages/Admin/Accounts/Index.cshtml`
- `Pages/Admin/Accounts/Create.cshtml`
- `Pages/Admin/Accounts/Manage.cshtml`
- `Pages/Admin/Audit/Index.cshtml`

**Goals**

- Make final-review blockers, click-through destinations, overrides, provisional placements, official snapshots, archive, and unfinalization easy to distinguish.
- Keep original and corrected completion times visible.
- Use the combined date-time picker consistently.
- Group accounts by event and role instead of presenting unrelated event accounts together.
- Clearly show captain/co-captain team scope, active period, correction-only state, expiry, disabled state, and credentials lifecycle.
- Keep permanent admin accounts visually separate from temporary captain accounts.
- Give audit entries enough event and actor context for non-developer admins.

**Approval gate**

- User can finalize an event and inspect the correct event's accounts/history without cross-event ambiguity.

### Pass 11 — Public signup journey

**Pages**

- `Pages/Index.cshtml`
- `Pages/Events/Signup.cshtml`
- `Pages/Events/Confirmation.cshtml`
- `Pages/Events/EditSignup.cshtml`

**Goals**

- Make upcoming, signup-open, live, final-review, finalized, and archived events visually distinct.
- Replace the oversized home-page hero with a compact introduction that leaves event content visible in the initial viewport.
- Reduce the main heading size and remove excessive vertical whitespace above the event list.
- Replace “Mission Control” wording with the community-facing action “View bingo”.
- Use a consistent accessible event-state colour system: teal for signup open, blue for upcoming, green for live, amber/orange for final review, gold for finalized results, and grey for archived history.
- Apply state meaning through text/badges as well as colour.
- Give signup a participant-friendly explanation of required fields and privacy editing.
- Keep primary account, EHB, optional secondary account, captain volunteer, and custom questions ordered logically.
- Make confirmed versus waiting-list outcome unmistakable.
- Make private edit links and their limitations understandable.
- Avoid exposing admin terminology in public messaging.

**Approval gate**

- A participant unfamiliar with the implementation can find an event, sign up, understand their status, and edit their signup.

### Pass 12 — Public bingo dashboard and evidence

**Pages/components**

- `Pages/Events/Board.cshtml`
- `Pages/Events/Teams.cshtml`
- `Pages/Events/TeamBoard.cshtml`
- `Pages/Events/Tile.cshtml`
- `Pages/Shared/_PublicProgressScripts.cshtml`

**Goals**

- Treat this as the flagship visitor experience: it may use richer hierarchy, typography, motion, and visual polish than the compact administrator surfaces while retaining the established dark OSRS-inspired design language.
- Use one breadcrumb-free event dashboard with top-level views for **View bingo**, **Recent drops**, and **Leaderboards**.
- Preserve the overview-to-team-board concept without using “Mission Control” as public-facing copy.
- Make team ranking, board completion, line count, tile count, and EHB understandable at a glance.
- Populate Recent drops only from approved contributions and preserve evidence/player privacy choices.
- Keep official bingo standings visible beside the leaderboard at desktop widths and in a compact leading position at narrower widths.
- Separate **Drop EHB** (the proportional share of combined expected tile EHB earned by approved credited contributions) from deferred **Activity EHB** (Wise Old Man event gains); team and player Drop EHB must use the same allocation, a completed tile must equal its tile EHB, and a completed board must equal its board EHB. Do not sum standalone time-to-specific-drop values for alternative eligible drops, and do not imply live Wise Old Man data exists until the cached synchronization is implemented.
- Avoid unnecessary “external clan” labelling unless affiliation itself is useful.
- Make opening and returning from a team board preserve spatial and navigation context.
- Keep team switching and previous/next navigation clear.
- Make tile progress, requirements, approved evidence, privacy-hidden evidence, and credited player information readable.
- Keep evidence lightbox behavior consistent.
- Retain fast SignalR updates with unobtrusive fallback refresh behavior.
- Provide a practical mobile layout rather than shrinking the desktop board beyond usability.

**Approval gate**

- Public visitors can move between the three dashboard views, compare teams, inspect approved public activity, understand standings and EHB, inspect a tile, and return to the overview comfortably on desktop and mobile.

### Pass 13 — Authentication, errors, privacy, and final accessibility pass

**Pages**

- `Pages/Account/Login.cshtml`
- `Pages/Account/Setup.cshtml`
- `Pages/Account/ChangePassword.cshtml`
- `Pages/Account/AccessDenied.cshtml`
- `Pages/Error.cshtml`
- `Pages/Errors/StatusCode.cshtml`
- `Pages/Privacy.cshtml`

**Goals**

- Provide a deliberate access-denied page instead of blank or confusing output.
- Distinguish invalid credentials, expired accounts, correction-only accounts, and missing permission.
- Keep first-admin setup clearly development/installation oriented.
- Make error pages useful without leaking implementation details.
- Replace placeholder privacy content with the actual version-one data/evidence policy.
- Complete keyboard-only navigation, focus order, reduced-motion, screen-reader label, contrast, and responsive checks across the full application.

**Approval gate**

- All fallback states are understandable, and the complete application passes the accessibility and responsive checklist.

## 5. Final Milestone 9 regression

After all thirteen passes are approved:

1. Reset and regenerate canonical test scenarios.
2. Repeat every Milestone 1–8 manual test against the new interface.
3. Run the complete automated suite.
4. Test current Safari/Chromium desktop, intermediate/tablet, and representative mobile widths, including the transition around shared breakpoints.
5. Recheck two-admin board and draft collaboration.
6. Recheck captain paste/drag/upload on desktop and mobile.
7. Recheck public live updates, evidence privacy, and finalized history.
8. Record remaining defects by severity.
9. Fix all critical/high defects and repeat affected passes.
10. Freeze the approved UI as the Milestone 10 rehearsal candidate.

## 6. First overhaul boundary

Begin with **Pass 1 — Global shell and event context**. Individual workflow pages should not be visually finalized before the shared navigation, event context, status feedback, page header, form, table, and dialog patterns are approved.

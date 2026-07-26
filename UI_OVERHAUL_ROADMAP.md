# UI Overhaul Roadmap

**Status:** Paused at the current Pass 12 checkpoint while Planning Pass 2 defines and delivers functional expansion under Milestone 8A.

**Last updated:** 2026-07-25

**Related documents:** `IMPLEMENTATION_ROADMAP.md`, `PRODUCT_REQUIREMENTS.md`

## 1. Purpose

The application contains the originally required version-one workflows and a partially completed UI overhaul. Planning Pass 2 has reopened functional scope, so affected pages will not be visually finalized until the selected functionality is designed and implemented under `IMPLEMENTATION_ROADMAP.md` Milestone 8A.

The approved visual system and completed page decisions remain valuable constraints. The remaining work is still split into small passes, but their order and content must be impact-reviewed before the overhaul resumes.

## 2. Rules for the overhaul

- Preserve approved business rules unless an explicitly approved Milestone 8A feature changes them.
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
- Every page added during Milestone 8A or later must use the approved shared visual system, controls, spacing, feedback, responsive, accessibility, and progressive-enhancement rules from its first implementation. Milestone 9 still revisits those pages for final cross-site polish; it is not permission to ship an interim page with a separate or legacy design.
- Every route available to an anonymous visitor, normal User, captain/co-captain, or emergency captain must provide complete English and Danish presentation through the existing language switch. This includes headings, controls, help, validation, confirmations, empty/loading/error/permission states, and server/client feedback. A route restricted entirely to Admin/Super Admin may remain English-only.
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
9. Inventory the pass's mutation endpoints and verify that each uses the shared success/failure, focus, scroll, and no-JavaScript contract. Do not rely on the user to discover every missing feedback path manually.
10. Run focused automated tests for the shared mechanism and high-risk exceptions, plus representative manual regression checks rather than duplicating every success/failure combination by hand.
11. Obtain explicit approval before starting the next pass.

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
- A mutation must never look like a silent reload. After a full-page form post or redirect, return the user to the affected control or result instead of jumping to an unrelated page position.
- When new success, error, or validation feedback is outside the viewport, move focus and scroll it into view. Do not move the page when the feedback is already visible, and respect reduced-motion preferences.
- Progressive enhancement should preserve the user's useful scroll position and local context. The ordinary Razor fallback may navigate, but must still expose the result immediately through an adjacent message, validation summary, or deliberate fragment target.
- Repeated enhanced mutations that keep the user on the same logical page replace the current browser-history entry. After any number of same-page changes, Back returns to the route visited before that page. Genuine route changes retain normal history. No-JavaScript forms remain functional even where the browser preserves native POST/redirect entries.

## 3.4 Role-based action inbox

Pass 1 will reserve a compact top-right action-inbox control for authenticated users. It is an outstanding-work list, not a permanent social notification feed.

**Admin inbox**

- Show the count of submissions awaiting review for the active event.
- List the newest actionable submissions with team, player, tile/drop, and submission time.
- Link each item directly to its review details.
- Link to the complete review queue.
- Do not mix completed events into the active-event count.

**Captain/co-captain notifications**

- Rejected submissions do not become an outstanding correction state in this action inbox.
- Deliver the durable rejection notification, reason, and submission link to current linked team captains/co-captains through the normal notification surface.
- Never reveal another team's private submission state.

**Shared behavior**

- Update counts and items through SignalR when submission/review state changes, with a lightweight refresh fallback.
- Use a visible badge and accessible text; do not rely on colour alone.
- An admin review item remains outstanding until its underlying pending state changes. Durable rejection notifications retain the normal notification read/unread behavior.
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
- Full-page and in-place mutations return focus to the affected region or newly rendered feedback without an unexplained jump to the top of the document.
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
- Focus is visible but not oversized, and opening/closing a dialog returns focus sensibly without leaving a stray page outline. Shared input-modality handling shows focus rings for keyboard navigation while suppressing programmatic or mouse/touch focus decoration; never remove the keyboard-visible state to solve a pointer-only visual issue.
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
- Compact internal scrollers use the reference board's macOS-like auto-hiding scrollbar treatment on every operating system. Their scrollbar stays hidden while idle, becomes a thin `--border` thumb while hovered, keyboard-focused, or actively scrolling, and hides shortly after scrolling stops. Apply this to board grids, board tabs, tile sidebars, contribution/evidence lists, submission drawers, and similarly constrained nested regions. Keep the primary document/page scrollbar native and visible according to the user's operating-system preference; never hide it. Scrolling must remain available through wheel, trackpad, touch, keyboard, and pointer dragging.

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

1. **Primary / accent outline:** the default for clear next actions such as Add, Save, Submit drop, Submit for review, and Okay. Use light/accent-mixed text on a transparent or raised-surface background with a one-pixel `--accent` outline. Hover adds a restrained semi-transparent accent fill; it does not become a solid pale-blue block. Filled primary buttons are exceptional rather than the default and require a specific reason.
2. **Secondary / neutral outline:** transparent or surface-filled with a `--border` outline and primary text. Hover uses a subtle accent-tinted background and accent-strengthened border.
3. **Quiet/text:** navigation, back, cancel, reveal, or low-priority actions. Use text/accent colour with no permanent filled block; hover adds only a faint tint.
4. **Success outline:** reserved for an action whose meaning is explicitly successful/complete, not as a generic primary colour. A success message normally still uses the accent-outline action because the green status mark already communicates success.
5. **Warning:** amber outline or restrained fill for risky lifecycle decisions. Warning is not a substitute for primary emphasis.
6. **Danger / red outline:** transparent or restrained red tint with `--danger` text/border. Hover adds a semi-transparent red fill. Use for Remove, Delete, Reject, and comparable destructive actions; require plain-language confirmation and never use a solid white hover state.
7. **Icon-only:** square, compact controls only when the icon is conventional and context is clear. Always provide an accessible name and visible focus treatment.

- A **bare remove ×** is an approved compact danger variant when the removable object is visually self-evident, such as an attached screenshot, image thumbnail, chip, or compact nested row. Only the red × is visible; retain an invisible circular hit target larger than the glyph, a precise accessible label, pointer hover feedback, and a keyboard-visible danger-coloured focus ring. Do not use the bare × for ambiguous destructive actions, permanent record deletion, or actions that need explanatory wording.
- Regular buttons share one height, padding, radius, weight, and alignment within a region; compact buttons use one smaller shared size. Do not size equivalent buttons from their label length.
- Aim for a visual height near `2.35rem`–`2.5rem`; at touch-oriented narrow widths the target must be at least `44px` high or receive equivalent surrounding hit area.
- Use a `150ms` colour/border/background transition. Hover is subtle, pressed is visibly but briefly darker, and focus uses the shared two-pixel accent-mixed outline with a two-pixel offset.
- Disabled buttons keep their semantic colour family at reduced contrast, do not respond to hover/press, and remain distinguishable from enabled actions.
- Button labels describe the result (`Submit for review`, `Open team board`, `Save changes`) rather than generic `Continue` or icon-only wording.
- Reuse these variants in links, forms, dialogs, empty states, team switching, evidence controls, and leaderboard actions. Do not create one-off page button CSS when a shared variant applies.
- Tabs are navigation, not buttons. The reference treatment uses muted text, no filled container, and a two-pixel accent underline for the active state. Use filled segmented controls only when switching data modes inside a contained panel.
- Inputs, selects, textareas, menus, and dialogs must use the same raised surface, one-pixel blue-grey outline, text hierarchy, radius family, and focus treatment as buttons and panels.
- Image fields use one shared managed-upload control modeled on evidence submission: a clearly labelled drop/select area supports file selection, drag/drop, and paste where the browser permits it; after selection it shows a real thumbnail preview, filename/validation state, and explicit replace/remove actions. Uploading, processing, success, and failure states remain inside the control and preserve the previously saved image when a replacement fails.
- Event banners, team images, custom board/tile artwork, profile images, and evidence never present an arbitrary image-URL text box. The global OSRS catalogue is the sole exception and may expose a clearly labelled external source-URL field alongside its catalogue-specific caching status.
- The shared image control uses the normal accent-outline action hierarchy and the approved bare red remove `×` on a self-evident preview. It must have an accessible label, keyboard-operable file selection/removal, server validation feedback, a no-JavaScript file-input fallback, and a compact single-column layout before its preview or actions become compressed on narrow screens.
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
- Existing approved functionality, authorization, validation, lifecycle protections, and progressive enhancement remain fixed except where an explicitly approved Milestone 8A feature defines and tests a deliberate change.
- Reuse or extract shared components for page mastheads, tabs, cards, notices, buttons, fields, tables, dialogs, empty states, progress, ranks, and responsive containers. Do not duplicate the TEST 15 CSS under new page-specific class names.
- New one-off colour, spacing, radius, shadow, type size, breakpoint, or control pattern requires a reason that the shared system cannot express. If that reason is valid and reusable, add it to the shared system and document it here.
- Public and captain boards should be closest to the reference composition. Signup and authentication pages may use a narrower centered column. Admin pages may use denser cards, tables, and multi-column workspaces. All retain the same canvas, surfaces, borders, typography, controls, state colours, focus treatment, spacing scale, and interaction timing.
- A page is not approved merely because it uses the right colours. It must also match the reference density, alignment discipline, component geometry, feedback quality, responsive transitions, keyboard behavior, and absence of unnecessary visual bulk.
- The TEST 15 View bingo page remains the visual comparison baseline until the user explicitly approves a replacement reference. Later refinements to that reference must be reflected in shared tokens/components and this roadmap before they are propagated.

## 3.7 Functional-change pause and resume gate

Planning Pass 2 interrupts the page-pass sequence at the current Pass 12 checkpoint because new functionality may change roles, navigation, submission, event operations, public data, and responsive layouts.

During the pause:

- Preserve completed and user-approved UI work; do not roll it back merely because affected workflows may evolve.
- Treat the flagship tokens, density, component geometry, button hierarchy, route-backed overlay pattern, responsive fallbacks, feedback behavior, and accessibility rules as reusable design constraints rather than proof that every current screen is final.
- Limit UI changes to those required to make an approved Milestone 8A vertical slice understandable, accessible, and testable.
- Build every new page and every materially changed page with the section 3.6 shared system from its first implementation. The later complete UI pass revisits cross-site consistency and polish; it does not excuse an interim legacy theme, page-local control language, or missing responsive/accessibility states.
- Run focused checkpoint regression, not the full final overhaul regression.
- Record which existing pages and approval gates each selected feature affects.

### Protected interaction baselines during Milestone 8A

Functional expansion may add or change fields, commands, status states, validation, warnings, and workflow steps. It must not incidentally redesign the strongest existing work. The protected baseline is the established information hierarchy, density, spatial context, navigation model, feedback behavior, responsive fallback, and accessibility behavior—not the exact number of controls or the assumption that functionality is frozen.

**Public board ecosystem**

- Preserve the approved overview, route-backed team overlay, nested tile-sidebar replacement, captain submission drawer, persistent submission result, and ordinary narrow/no-JavaScript route fallbacks.
- Public board, team board, tile, evidence, and submission changes must behave as one connected system. Adding a capability to one route must not regress Back/Escape behavior, active drawer state, realtime invalidation safety, team privacy, or the finished public projection.
- New functionality should normally extend the existing sidebar, drawer, dialog, or compact board components instead of adding a competing shell or parallel visual language.

**Board editor**

- Preserve the compact visual workspace in which the board, tile context, EHB information, validation, and primary editor controls remain spatially understandable together.
- At the `1280 × 720` desktop reference viewport, ordinary editing should normally fit within the application viewport. Large boards, catalogues, lists, and contextual panels use deliberate contained scrolling rather than making routine work traverse a long document.
- This is not a fixed-height or no-scroll requirement. Browser zoom, translated text, smaller screens, accessibility settings, unusually large content, and no-JavaScript fallbacks may use normal document scrolling; content and actions must never be clipped merely to preserve a one-screen appearance.
- Add approved functionality through the established panels, compact controls, dialogs, drawers, previews, and contained regions where they remain appropriate. A functional requirement that genuinely cannot fit this model receives a focused interaction review before the core composition is replaced.

**Live draft**

- Preserve the operational one-screen relationship between the available pool, teams and capacities, current turn/order, pick history, and draft controls. Admins should not lose situational awareness when using an added command.
- At the desktop reference viewport, ordinary draft operation should normally remain within the application viewport. Long participant pools, team rosters, and pick histories scroll within clearly bounded regions; responsive and accessibility states may use ordinary page scrolling.
- Keep realtime state, controller/observer feedback, pending confirmations, scramble, repeated undo, and draft order visually connected to the authoritative draft state. New functionality must not hide or displace the information needed to make the next pick safely.

For any protected surface, a functional slice must identify its exact UI delta before editing. Unaffected composition remains intact. When the delta requires a genuine interaction-model change, record the reason and obtain focused approval rather than allowing a broad redesign to arrive as incidental feature work.

Before resuming the ordered passes:

1. Complete the Milestone 8A planning and implementation gates.
2. Approve the feature-to-page impact map.
3. Reorder or expand Passes 8–13 where the stabilized workflows require it.
4. Reconfirm whether earlier approved passes are unaffected or need a focused revisit.
5. Resume at the earliest affected pass, then complete the milestone-wide regression once against the revised product.

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
- Require only event name and timezone to save the initial private draft; allow description, signup settings, schedule, questions, and planning information to be entered immediately without making them initial-save requirements.
- Group optional setup sections logically and show their later readiness state without implying that the administrator must finish them in one session.
- Generate the slug from the event name, allow editing until first public exposure, and explain that it remains stable afterward.
- Include optional event-banner upload during creation and allow add, replace, and remove actions later; never present missing artwork as an incomplete-event warning.
- Default timezone to Copenhagen and use a supported-timezone selector with friendly labels instead of the current raw text field.
- Use the combined date-time picker for signup and event dates.
- Keep times easy to choose in 30-minute increments while allowing precise corrections elsewhere.
- Support scheduled and manual signup opening. Manual opening preserves a valid explicit close or clearly proposes the earlier of three months later and event start; never silently replace an invalid explicit close.
- Include optional draft time as planning information without implying that it starts the draft automatically.
- Show the default 30-minute submission grace period and keep the cutoff visibly tied to, but editable separately from, event end.
- Hide or disable the signup code field when code protection is off.
- Explain private signup editing with a compact information control.
- Include standard signup fields and make custom questions clearly secondary.
- Make it clear that publishing/opening an event only exposes event information and signup.
- Provide an explicitly confirmed discard action for accidental or experimental events that contain no participants, teams, event-scoped accounts, or evidence; board/setup work does not block discard.
- When protected records block discard, offer the separate pre-live **Cancel event** workflow with strong confirmation/reason and explain that it preserves history.

**Approval gate**

- User can save a minimal private draft, optionally continue setup immediately, understand what remains before signup can open, and discard an unused experimental event without needing developer terminology or scrolling through unnecessarily large controls.

### Pass 3 — Event overview and operations

**Pages**

- `Pages/Admin/Events/Manage.cshtml`
- Target `Pages/Admin/Rules.cshtml`

**Goals**

- Turn the current all-in-one manager into a clear event overview.
- Show lifecycle state, important dates, signup numbers, board state, draft state, submission state, and items requiring attention.
- Move secondary operations into clearly labelled sections or event navigation destinations.
- Make start, end, reopen, and final-review actions easy to locate and hard to trigger accidentally.
- When a scheduled start is blocked, show **Automatic start postponed** as a prominent actionable state with every current blocker and a **Start event now** action that becomes enabled only when readiness succeeds. Clearing blockers never silently starts the event; starting after schedule needs confirmation but no reason, while starting early requests the required reason.
- During live play, distinguish the scheduled event end, authoritative effective end, and separate submission cutoff. Explain that ending play closes new-drop eligibility but leaves eligible uploads open through the cutoff.
- Keep **End event early** exceptional: require strong confirmation and a written reason, show that the scheduled end remains preserved, and do not imply that the submission cutoff will move.
- After event end, show `AWAITING_FINAL_REVIEW` and the remaining grace-period time independently from review counts and finalization blockers.
- Keep one production current/public operational event. When an opening/publication/unfinalization action is blocked by another event, name that event and link to its archive/cancel/current-operation route.
- Show cancellation only before live play, require the approved confirmation/reason, and distinguish the generic public cancellation status from the private admin reason.
- Show explicit success feedback for every operation.
- Keep evidence-code management understandable without dominating the page.
- Make links name the destination event rather than relying on surrounding context.
- Provide one focused editor for the permanent global Rules page. It remains editable by enabled administrators at any time and is not presented as event configuration.
- Do not provide an in-application editor for source-controlled how-to pages.

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
- Ordinary Admins may create, edit, deactivate, and reactivate catalogue records. Only the Super Admin may permanently delete a genuinely unused record after confirmation and a dependency check; referenced rows must remain available to history and use deactivation instead.
- Remove the obsolete catalogue CSV importer. Place the reviewed bulk catalogue preview/apply workflow under Super-Admin-only advanced administration, with exact change/conflict preview and stale-preview protection.
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

Do not add tile duplication, copy-from-event, import, or reusable-template actions. Tiles are configured for the current event board and repositioned through the already approved move/swap interactions.

**Goals**

- Keep the board itself as the primary workspace.
- Create a tile from its empty board position rather than from a detached global form.
- Edit a populated tile by selecting it while preserving board context.
- Revisit popup versus side-inspector behavior after testing both at realistic desktop widths.
- Keep boss selection as a searchable chooser and group eligible drops by boss.
- Provide select-all/deselect-all drop controls.
- Make objective wording community-friendly.
- Keep general evidence/upload instructions out of tile forms. Custom/manual tiles show only their objective-specific completion criteria; link to the global Rules or relevant how-to page when general submission guidance is useful.
- Keep manual quantities, duplicate rules, higher weighting, multiple bosses, and multiple requirement groups understandable.
- Make drag-on-tile swapping feel immediate without a disruptive full-page refresh.
- Preserve an accessible non-drag alternative for keyboard and touch users.
- Keep row/column hover highlighting, EHB values, total EHB, EHB per expected player, and balancing warnings readable.
- While the board is Draft, derive catalogue-backed names, images, rates, variants, and all EHB estimates from the current catalogue and refresh affected values after catalogue changes.
- Make **Approve board** the visible snapshot boundary. Approved values remain frozen despite later catalogue edits; unapproval or competitive editing returns the board to Draft and resumes live derivation.
- Retain **Preview board** inside the editor. It must use the actual responsive public-board renderer, show live derived values while Draft and the frozen active snapshot while approved, omit admin-only editing/EHB controls, and never approve or publish.
- Review the tile EHB calculations themselves, not only how the values are displayed.
- Verify EHB behavior for quantities, multiple bosses, combined drop selections, duplicate restrictions, weighted drops, manual objectives, and requirement groups such as Voidwaker pieces and Barrows plus Moons.
- Make it clear which catalogue boss rates and drop probabilities produced a standard tile's automatic EHB. A missing value is a blocking error with actionable catalogue/requirement diagnostics, never a manual-override field. Only a clearly labelled custom/manual objective exposes manual EHB entry.
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
- Use the shared managed-image upload control for team artwork; never ask for a team image URL.
- Keep the compact `current/final` roster counter on each drafted-team card. Its denominator uses that team's derived final size, so the counter itself identifies teams that will finish one player smaller without adding a large warning panel.
- Draft-start readiness distinguishes the required Captain role from optional co-captains and clearly identifies every drafted team still missing its captain.
- Keep all participants visible during drafting, with clear drafted/available status and EHB ordering.
- Make confirmed participants and waiting-list members visually distinct.
- Keep external clan rosters separate from the website signup pool.
- Revisit the external-team import workflow, including the source format, field mapping, validation, duplicate handling, roster roles, and a clear preview before anything is added to the event.
- Place the rare “use an internal participant in a pre-formed team” action behind an advanced path.
- Make scramble, snake order, current pick, undo, pause, takeover, and finalization status obvious.
- After draft finalization succeeds, open a compact **Publish board?** confirmation or route-backed follow-up page. Show the **Publish board** action only when every board position is filled and the board is approved; otherwise show the exact completion/approval blocker and board-editor route. Closing or leaving this step never reverses roster finalization.
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

- Before/during the draft, reuse the familiar signup table for drafted-team captains/co-captains with additional participant-answer columns. Keep payment, admin notes, identity/security details, and audit data absent.
- At draft finalization, keep the signup page as a private admin page and redirect non-admin requests for its route to published team rosters; never render signup answers on roster pages.
- Always show event, team, captain account mode, submission cutoff, and whether submission mutations are currently open.
- Treat normal captain/co-captain access as an event/team role on the participant's website account, independent of whether they used Discord or password; distinguish explicitly enabled emergency credentials when they are in use.
- Make team progress and pending/rejected submissions easy to scan.
- Make pending submissions clickable for inspection and editing.
- Make rejected reasons and **Resubmit** easy to find for notified participants and captains/co-captains. Prefill the rejected attempt's ordinary values, show its locked credited player/account, require a new screenshot, and clearly state when cutoff has disabled the action.
- Streamline screenshot paste, drag/drop, and file selection.
- Eliminate the drop-zone focus behavior that accidentally opens Finder.
- Keep credited player/account, tile, requirement, drop, weight, and note readable. Do not show an evidence-privacy request.
- Explain duplicate rules and higher weight only when relevant.
- Keep submission and pending-edit controls available during the post-end grace period, including for external-team captains, and remove them only at cutoff.
- Let captains/co-captains toggle simple tile/row/column focus. Show it normally only to current team members; ordinary admins receive no cross-team focus visibility.
- Layer authorized team focus onto the existing approved team-board view rather than adding another participant board or reopening the public-board design. If the private layer becomes crowded, use a view-only **Show team focus** toggle or compact summary.
- Let ordinary participants submit only for themselves and manage only their own pending/rejected evidence. Do not show a credited-account selector; display the server-derived current active account.
- On another team's page, keep focus absent for the Super Admin until they deliberately enable a clearly labelled, read-only **Inspect team focus** toggle. Make the inspection state obvious, do not remember it as a show-all preference, and stop showing/subscribing to the data when disabled.
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
- Every evidence review shows immutable **Submission time**. Only for a post-end submission, add calculated minutes after event end and **Latest clan event time** using the authoritative event end formatted in UTC.
- Do not add an editable or separately entered drop-time field. For a post-end submission, the reviewer reads the plugin timestamp from the screenshot and compares it with **Latest clan event time**.
- Present only the approved review states: pending, approved, rejected, withdrawn, and reversed, plus contextual grace-period indicators.
- Keep **Approve** and **Reject** as the only review decisions. Rejection requires a reason; duplicates and unusable screenshots use that path.
- Do not expose a direct credited-participant editor. Changing the credited playing account derives the participant automatically.
- Require a reason for material tile/drop/credited-account corrections and keep the server submission time, calculated contribution, and evidence image read-only.
- Show immediate confirmation after approve, reject, reverse, or metadata correction.
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
- Archive only finalized events, require confirmation without a typed reason, and explain that public URLs/results remain while the event moves to Previous events.
- Show scheduled end, effective end, submission closure, and any approved reopening as separate timestamps so admins can explain why a drop or upload was eligible.
- Keep original and corrected completion times visible.
- Use the combined date-time picker consistently.
- Group Discord-linked identities and event roles by event/team instead of presenting unrelated access records together.
- Clearly show participant linkage, captain/co-captain team scope, active period, submission-window state, disabled state, and identity-recovery status.
- Present ordinary Admin and the sole Super Admin as global roles on normal website accounts, independent of Discord-link state. Keep disabled-by-default emergency captain credentials visually separate and ineligible for those roles.
- Show one unmistakable current-owner state. Only the Super Admin sees grant/revoke Admin and **Transfer Super Admin** controls; ordinary admins see role state without mutation controls.
- Use strong confirmation for grant, revoke, and ownership transfer, explain immediate access/session effects, and show success/failure without requesting a typed reason.
- Prevent the current owner from being demoted/disabled through ordinary controls. Transfer selects an existing eligible active normal website account and explains that the former owner remains Admin; a Discord link is not required because password access is durable.
- Let Admins disable Users and let only the Super Admin disable/restore Admins. Disable requires a reason and explains immediate session invalidation/history preservation; re-enable is confirmed without implying that expired event authority returns.
- Let authorized admins generate a password-reset link without seeing or setting the password. Distinguish normal-user reset authority from the Super Admin-only action for another Admin, and provide no in-product reset-link action for the Super Admin.
- Do not present website-account captain roles as expiring accounts. Show emergency password credentials as explicitly created fallbacks that auto-disable at submission cutoff and require deliberate re-enablement.
- Give audit entries enough event and actor context for non-developer admins.
- Present audit history as a newest-first paginated table with 25 rows per page and persistent filters for event, actor, action, entity, and date range. The complete retained history remains reachable; do not add export.
- Provide one Accounts area with clearly separated website-account and emergency-credential datasets; the page pass chooses the best responsive tabs/sections without visually mixing unlike records.
- Website-account details show every linked OSRS character, non-authoritative last-known Discord display name, event participation, current/historical event-team roles, and disable history. Emergency rows use their distinct event/team, setup, enablement, last-login, and cutoff fields.
- Use compact server-side search, filters, and 25-row pagination for both datasets. Keep secrets and unnecessary raw identity values out, and do not add account merge or permanent deletion.
- Keep personal notification read/unread state separate from the Admin action inbox: opening a notification marks only that message read, while pending reviews, postponed starts, waiting-list follow-up, vacancies, and missing-captain conditions remain until resolved.

**Approval gate**

- User can finalize an event and inspect the correct event's accounts/history without cross-event ambiguity; the Super Admin can safely manage Admin access and transfer ownership without creating zero or two owners.

### Pass 11 — Public signup journey

**Pages**

- `Pages/Index.cshtml`
- `Pages/Events/Signup.cshtml`
- `Pages/Events/Confirmation.cshtml`
- `Pages/Events/EditSignup.cshtml`

**Goals**

- Make upcoming, signup-open, live, final-review, finalized, archived, and cancelled events visually distinct.
- Replace the oversized home-page hero with a compact introduction that leaves event content visible in the initial viewport.
- Reduce the main heading size and remove excessive vertical whitespace above the event list.
- Replace “Mission Control” wording with the community-facing action “View bingo”.
- Use a consistent accessible event-state colour system: teal for signup open, blue for upcoming, green for live, amber/orange for final review, gold for finalized results, and grey for archived history.
- Use a clearly labelled muted/red cancellation treatment without suggesting that private cancellation reasons are public.
- In production show one current event and a separate Previous events collection. Development/manual-test builds may list several clearly labelled seeded scenarios and must navigate them through explicit slugs.
- Keep archived boards/results on their existing public routes. Signed-in former participants may reach their own read-only rejected/withdrawn history without introducing a separate post-event dashboard.
- Apply state meaning through text/badges as well as colour.
- Require Discord for initial website-account creation without requiring Discord-server membership; returning users may sign in with Discord or public username/password.
- Add first-account onboarding that creates the public/password-login username, required password, and preferred linked-character records, while clearly distinguishing the website account, Discord association, and OSRS character.
- Give signup a participant-friendly explanation of required fields, identity linkage, and privacy.
- Add a global **My accounts** surface with an ordered flexible list, optional personal labels, and one preferred character; do not force game-mode sections.
- Support several OSRS characters without presenting one character name as the website identity. The selector may show globally shared/trusted links, but it must make event availability clear and prevent a character from being assigned to two participants in the same event.
- During signup, show the required primary playing account with its EHB, the always-present captain-volunteer choice, and any admin-added optional Account questions. Each additional playing account gets its own conditional EHB field; explain whether it is playing/drop-eligible or informational-only.
- Place an explicit **Fetch from Wise Old Man** action beside each playing-account EHB field, preserve manual entry, and show accurate loading, not-found, rate-limited, unavailable, and successful states without clearing an existing value on failure.
- Distinguish event-registered playing accounts from the single active/drop-eligible character and make unlimited live swaps and their UTC effective time explicit.
- Show the primary account as planned before event start, enable swaps only while live, and explain that the participant must submit a drop before swapping because new evidence uses the current active account.
- Keep OSRS characters, EHB, captain volunteer, and custom questions ordered logically.
- Once responses exist, distinguish safe question edits from locked structural fields and provide a clear disable-and-replace path that explains historical-answer preservation.
- Make confirmed versus waiting-list outcome unmistakable.
- Add a public signup board with clearly separated confirmed and waiting-list sections, using unique public usernames and exposing only fields approved by their visibility settings.
- When draft finalization publishes rosters, keep the signup page and its content admin-only; redirect public, participant, and captain requests for that route to the separate team-roster route.
- Let authenticated participants edit their signup only while signup is open; do not issue private edit links for new normal signups.
- Provide a clear read-only state after signup closes.
- Avoid exposing admin terminology in public messaging.

**Approval gate**

- A participant unfamiliar with the implementation can find an event, sign up, understand their status, and edit their signup.

### Pass 12 — Public bingo dashboard and evidence

**Pages/components**

- `Pages/Events/Board.cshtml`
- `Pages/Events/Teams.cshtml`
- `Pages/Events/TeamBoard.cshtml`
- `Pages/Events/Tile.cshtml`
- Target `Pages/Rules.cshtml`
- Target source-controlled public pages such as `Pages/HowTo/SubmitDrops.cshtml`
- `Pages/Shared/_PublicProgressScripts.cshtml`

**Goals**

- Treat this as the flagship visitor experience: it may use richer hierarchy, typography, motion, and visual polish than the compact administrator surfaces while retaining the established dark OSRS-inspired design language.
- Use one breadcrumb-free event dashboard with top-level views for **View bingo**, **Recent drops**, and **Leaderboards**.
- Preserve the overview-to-team-board concept without using “Mission Control” as public-facing copy.
- Make team ranking, board completion, line count, tile count, and EHB understandable at a glance.
- Populate Recent drops only from approved contributions. Approved evidence and credited players are public.
- Keep official bingo standings visible beside the leaderboard at desktop widths and in a compact leading position at narrower widths.
- Separate **Drop EHB** (the proportional share of combined expected tile EHB earned by approved credited contributions) from deferred **Activity EHB** (Wise Old Man event gains); team and player Drop EHB must use the same allocation, a completed tile must equal its tile EHB, and a completed board must equal its board EHB. Do not sum standalone time-to-specific-drop values for alternative eligible drops, and do not imply live Wise Old Man data exists until the cached synchronization is implemented.
- Avoid unnecessary “external clan” labelling unless affiliation itself is useful.
- Make opening and returning from a team board preserve spatial and navigation context.
- Keep team switching and previous/next navigation clear.
- Make tile progress, requirements, approved evidence, and credited player information readable.
- Keep evidence lightbox behavior consistent.
- Make the permanent global Rules page and relevant source-controlled how-to pages easy to reach from event and submission workflows without presenting them as event-owned content or repeating general instructions on every tile.
- Show **Edit rules** to enabled administrators on the Rules page. How-to pages have no in-application edit control.
- Retain fast SignalR updates with unobtrusive fallback refresh behavior.
- Provide a practical mobile layout rather than shrinking the desktop board beyond usability.

**Approval gate**

- Public visitors can move between the three dashboard views, compare teams, inspect approved public activity, understand standings and EHB, inspect a tile, and return to the overview comfortably on desktop and mobile.

### Pass 13 — Authentication, errors, privacy, and final accessibility pass

**Pages**

- `Pages/Account/Login.cshtml`
- `Pages/Account/Setup.cshtml`
- `Pages/Account/ChangePassword.cshtml`
- `Pages/Account/ForgotPassword.cshtml`
- `Pages/Account/ResetPassword.cshtml`
- `Pages/Account/Discord.cshtml`
- `Pages/Account/AccessDenied.cshtml`
- `Pages/Error.cshtml`
- `Pages/Errors/StatusCode.cshtml`
- `Pages/Privacy.cshtml`

**Goals**

- Present **Continue with Discord** and public username/password as two routes into the same account, with generic failure messaging and accessible throttling feedback.
- Make Forgot password direct the user to contact an admin; the reset page accepts only a valid single-use link and never asks for email.
- In account settings, always show the applicable **Link Discord**, **Unlink Discord**, or **Change linked Discord** action. Require fresh password confirmation, use OAuth for link/change, and explain that the website account, roles, signups, and history do not move.
- After unlink, continue with a reissued password-authenticated session. Warn the Super Admin that password loss would require operator recovery, but do not forbid unlinking.
- When changing public username, state that the new name also becomes the password-login username.
- Provide a deliberate access-denied page instead of blank or confusing output.
- Distinguish invalid credentials, expired accounts, closed submission windows, and missing permission.
- Keep initial-Super-Admin setup and lost-owner recovery clearly operator/deployment oriented and unreachable from public signup or onboarding.
- Make error pages useful without leaking implementation details.
- Replace placeholder privacy content with the actual version-one data/evidence policy.
- Complete keyboard-only navigation, focus order, reduced-motion, screen-reader label, contrast, and responsive checks across the full application.

**Approval gate**

- All fallback states are understandable, and the complete application passes the accessibility and responsive checklist.

## 5. Final Milestone 9 regression

This UI roadmap is executed only when the big roadmap reaches **Milestone 9**, after all ten Milestone 8A functional slices are complete. Milestone 9 requires a complete site-wide UI pass, not only a review of pages directly affected by functional work. Its internal Pass 11 is merely the public-signup page pass; it is not the milestone-wide overhaul gate. Reuse the approved flagship system and interaction checkpoints, then inspect every public, participant, captain, Admin, Super-Admin, authentication, error, privacy, empty, loading, permission, and failure state for compliance with the late shared rules.

After all revised passes are approved:

1. Reset and regenerate canonical test scenarios.
2. Repeat every Milestone 1–8A manual test against the new interface.
3. Run the complete automated suite.
4. Test current Safari/Chromium desktop, intermediate/tablet, and representative mobile widths, including the transition around shared breakpoints.
5. Recheck two-admin board and draft collaboration.
6. Recheck captain paste/drag/upload on desktop and mobile.
7. Recheck public live updates, approved-evidence visibility, reversal/resubmission, and finalized history.
8. Record remaining defects by severity.
9. Fix all critical/high defects and repeat affected passes.
10. Freeze the approved UI as the Milestone 10 rehearsal candidate.

## 6. First overhaul boundary

The original overhaul began with **Pass 1 — Global shell and event context** and has reached the current Pass 12 checkpoint. Planning Pass 2 now follows section 3.7 during functional implementation. Once Milestone 8A stabilizes, the complete Milestone 9 UI pass revisits Passes 1–13 as needed and applies the shared rules across the whole product rather than merely continuing from Pass 13.

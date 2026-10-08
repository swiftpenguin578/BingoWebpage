# U6 Teams / Draft — element-by-element parity checklist

Reference: `docs/references/admin-ui/TeamsDraft.dc.html` (frozen). App: `Pages/Admin/Events/Draft.cshtml` (labels, embedded
state, menu host, icons), `wwwroot/js/admin-draft.js` (the whole workspace), `admin-design-draft.css` (family-scoped layout),
`_AdminDraftSkeleton.cshtml` / `_AdminDraftLoadStates.cshtml`, `Draft.State.cs` (page state), `Draft.Readback.cs` (AU14
readback), `Draft.Outcome.cs` (JSON outcomes). Shared: shell header/summary, `_AdminDesignIcon`, shell menu/modal/drawer/toast
layers, `AdminFetch`, `AdminUI.busy`, frozen `components.css` classes (`.turn`, `.ctl`, `.dboard`, `.dteam`, `.dmem`, `.pool`,
`.pchip`, `.tcard`, `.tmem`, `.role-badge`).

Legend: **=** matches the reference; **R** registered difference (DELIVERY_PLAN “Bindings not shown in the design references”,
named); **Q** open for the user at the early look. Measured: conformance at 390/494/860/1280/1440 px in Chromium and WebKit
(`BINGO_CONFORMANCE_PAGES=draft`); real-browser flows `admin-design-draft.browser.js` (setup) and
`admin-design-draft-running.browser.js` (running, finalize, corrections, Live, Final review, terminal), both engines; review
screenshots `ur/` (`scripts/check-u6-ur.cjs`).

## Page header and banners

| Reference | App | Status |
| --- | --- | --- |
| `:138` h1 “Teams / Draft” | layout h1 (Danish “Hold / draft”) | = |
| `:139` summary: setup sentence by state; “Published rosters for {event}.”; none while running | same sentences (`summaryText`, server `Summary` for the first paint) | = |
| `:141` head action “Start draft…” / “Finalize rosters…”, inert (`aria-disabled`) until ready, title “Finish the steps above first” | same; inert click moves focus to the readiness title | = |
| `:1595` head “Open Board” on finalized/Live | shown on final/Live/Final review/terminal; a link (`data-shell-link`) only when a board exists, otherwise inert with “No board yet. Create one on the Board page.” | R Open Board (F1) |
| `:144` running: page head replaced by a visually hidden h1 | page head gets `sr` while running | = |
| `:162` `#td-banner` (icon, bold title, text, optional action) | same element and id; info/warning icon by tone | = |
| `:1576` “Rosters published {date}.” + corrections text | same | = |
| `:1577` “Rosters are locked.” (Live) | same | = |
| (no Final review / terminal state) | “Rosters are final.” (uploads open/closed variants), “This event is finished/cancelled/archived. Rosters are shown as they were.” | R T-24 (**Q** to confirm) |
| (no paused state) | “Historical paused draft. Pausing was retired, …” | R paused-draft banner (**Q** wording) |
| `:870-878` uncertain banner “We couldn’t confirm…” with Check again; after the check the outcome text | same flow; after the check: “We couldn’t confirm the response.” + what is now true (never “went through” for this request) | = / R AU14 row |
| `:879` lost-control notice “Another admin took control of the draft.” + “Take over…” | same | = |
| `:1205-1209` lapse notice “Your control lapsed.” + “Take control” | same (detected when control leaves this admin without their release) | = |
| (no hub-loss banner) | “Live updates stopped. …” + Reload while in control | R running control row (**Q** wording) |

## Setup: readiness, teams bar, cards

| Reference | App | Status |
| --- | --- | --- |
| `:166-179` readiness card: title by state, sizes sentence, checks with check-mark, sr “(done)/(to do)”, inline action/link | same structure and ids (`ready-title`, `ready-checks`); “Close signups first” links to Overview, “(note)” rows for unplaced players | = |
| (no end-of-event row) | “Set the event end in Schedule” + Schedule link (`data-shell-link`) when the end is missing or past | R U6-Q2 |
| `:181-185` “Teams {n}” bar + “Add team” (plus icon) | same | = |
| `:189` empty card “No teams yet” + text + Add team | same | = |
| `:193-221` team card: name, badge (Website draft / Manual roster / Drafted (needs 2 teams)), size text, EHB, menu, warning, empty text, member rows (role badge, name, tag, EHB, menu), foot (Assign captain, Add member/Preassign player) | same elements, ids (`card-{id}`, `t-{id}-menu`, `m-{id}-menu`, `t-{id}-add`) and conditions | = |
| `:1463` tags `#pick` / Pre / Added (title “Added as a correction…”) | same (Added = joined after the first publication) | = |
| (affiliation/image controls in the old page) | none (B6-c1: kept server-side when not sent) | = (brief) |

## Menus

| Reference | App | Status |
| --- | --- | --- |
| `:1270-1272` team menu: Edit team… / Rename team…, Add captain…, Remove team… (danger) | same, one shared menu element filled per opener | = |
| `:1280-1282` member menu: header (name, role · team), Make captain / co-captain / participant, Move to {team} (manual-to-manual setup), Remove from team / Remove from team… | same; finalized removal only through “Remove from team…” (S5); Move retired after finalize | = |
| `:1255-1264` More menu: Draw/Redraw order (hint), Preassign a player…, Finalize draft… (hint “N players left” / “Needs control”), Cancel draft… (danger, “N active picks”) | same items, hints and disabled states | = |

## Dialogs and drawer

| Reference | App | Status |
| --- | --- | --- |
| `:413-419` team form: “Team name”, placeholder “e.g. Team Torva”, counter after 22 chars, “Up to 30 characters…”, inclusion choice | same; Escape/outside click with edits opens the shared discard dialog (decision B) | = |
| (reference duplicate check is case-sensitive) | “Another team already has this name.” ignoring case, also server-side (B-Teams-5) | = (brief) |
| `:351-388` selector drawer: eyebrow/title by mode, Signed-up players / Other website accounts, search “Search players by account name”, results with Captain volunteer pill and notes, picked row + Change, role radios, hint “Volunteering to captain…” | same | = |
| `:1142`, `:1505`, `:1512` correction Add forces Participant | role choice kept (one publication) | R U6-Q1 |
| `:1546-1548` finalize confirmations (manual / draft) | same texts; points add “Team sizes: …” (brief: size summary) | = (brief) |
| `:1550` Remove {name} from {team}? with disclosures | same disclosures; “short vs target” sentence **not** added | **Q** short-vs-target wording |
| `:1551` Remove team: “Its N members go back to having no team. Nothing about the players themselves changes.” / “It has no members.” | “… Nothing about the players changes; they stay signed up.” (S7 wording) | = (brief S7) |
| `:1543-1545` Cancel: blocked variant (no confirm, Close) / confirm (Cancel draft, Keep drafting) | same | = |
| `:1549` Take over from {name}? with “last action N min ago” | name kept; “last action” omitted (lease renewal is not an action) | R running control row |

## Running draft

| Reference | App | Status |
| --- | --- | --- |
| `:230` turn strip: “Pick N of M” / “Round R”, dot + “Picking now:” + team, “then {team}” / “{team} again” | same (`AdminDesign.Pick {0}` for the pick number) | = / R scoped key |
| `:1388-1401` states: Drawing order / Shuffling… / Moving the teams…; Order not drawn + Draw order; Draft complete + Finalize…; Can’t continue + first blocker | same texts and conditions | = |
| `:238` Undo button: undo icon, “Undo #N”, title “Undo pick N: {name} to {team}”, busy “Undoing…” | same; posts the shown latest pick id | = / R Undo by id |
| `:240` control chip: “You have control · Release” / “Another admin has control · Take over…” / “No one has control · Take control” | same | = |
| `:241` `#draft-more` icon button | same | = |
| `:244-257` board columns: order badge (– while shuffling), name, “size / final”, EHB, Pre/#pick rows with role badges, pending row with spinner, `is-turn` | same | = |
| `:259-276` pool: “Available players”, “N left” / “N of M”, search “Find a player”, EHB/Name segmented sort, empty texts, chips with name/EHB/spinner, roving tabindex, sr help | same; persistent controls (search keeps focus while typing; sort keeps identity, conformance update probe) | = |
| `:700-706` “/” focuses the search; `:1346-1360` arrow/Home/End keys, Enter on a single search match drafts it, Escape clears | same | = |
| `:1381` `is-shuffling` while the draw saves; `:718-724` FLIP to the confirmed order, picks wait until settled | same (shared busy minimum 600 ms; `--dk-dur-reorder` and stagger tokens) | = |
| `:281` manual-team note “Not in the draft: …, assembled by hand.” | same | = |
| `:1332` sidebar collapsed while running, own toggle kept | same; restored when the draft ends or the page is left | = |

## Outcomes and toasts

| Reference | App | Status |
| --- | --- | --- |
| `:1016` finalize toast “Rosters (and draft results) published.” + Open Board action | same text; action only when a board exists; plus the Wise Old Man line when the event has a managed group | R Open Board, R WOM row |
| `:1029`, `:1160` “Rosters republished; the Wise Old Man group is updated.” | “Rosters republished.” only for a newer publication cycle, then a separate WOM outcome line (went through / waiting / failed / unknown / couldn’t be queued) | R WOM row (1221) |
| `:925`, `:943`, `:953` toasts for draw/control/release | same texts | = |
| `:858-866` refusals in the open layer, else an error toast | same, with the server’s reason; stale/refused never shown as unsure | = |

## Loading and failure

| Reference | App | Status |
| --- | --- | --- |
| `:146-149` loading cards | family skeleton: readiness card, Teams bar, three cards; real text-line heights; summary empty | R loading row |
| `:150-158` “Couldn’t load the teams” + Try again | same (failure template and in-page) | = |

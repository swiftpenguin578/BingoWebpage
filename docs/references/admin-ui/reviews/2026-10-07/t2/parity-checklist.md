# T2 Catalogue — element-by-element parity checklist (item 2 first working version)

Reference: `docs/references/admin-ui/Catalogue.dc.html` (frozen, D20). App: `Pages/Admin/Catalogue/Index.cshtml`
(directory), `_CatalogueDrawer.cshtml` (drawer template), `Pages/Shared/_AdminCatalogueLoadStates.cshtml` (loading /
failure), `wwwroot/js/admin-catalogue.js` (dialogs, previews, outcomes), `admin-design-catalogue.css` (reference style
block lines 22–95, family-scoped). Shared components: `_AdminDesignIcon`, `_AdminDesignFieldError`, banner/toast/
confirmation templates, the shell drawer layer (`dr-head` / `dr-body` / sticky `dr-foot`) and `AdminUI.busy`.

Legend: **=** matches the reference; **R** registered difference (DELIVERY_PLAN “Bindings not shown in the design
references”, or a recorded decision named); **Q** open question in the T2 report. Measured where noted
(Chromium, 1280 × 860 and 390 × 800, UR `live` fixture).

## Page header and toolbar

| Reference | App | Status |
| --- | --- | --- |
| `:175` h1 “Catalogue” | layout h1, `LocalizedTitle` | = |
| `:176` summary “N active activities · N drops” + “Shared by every event’s board estimates” | three items: “**N** active activities”, “**N** drops”, sentence; “No activities yet” when empty | R (count-summary row) |
| `:178` Add activity, `btn btn-primary`, plus icon | `#add-activity` link (`?new=1`), `_AdminDesignIcon("plus")` | = (link so it can be opened in a new tab, as Accounts) |
| `:181` toolbar `role=search` “Find activities and drops” | same | = |
| `:183-186` search: icon, placeholder/label “Search activities and drops”, clear × only with text | same; maxlength 100 | = |
| `:188` category seg All/Boss/Skilling boss/Minigame | same radios, `is-on` | = |
| `:190` status tabs Active/Inactive with counts | same, `tab-count` | = |
| `:193-195` notice banner with Dismiss | not rendered (the reference never sets it) | = |

## Directory table

| Reference | App | Status |
| --- | --- | --- |
| `:203-204` table “Activities”, columns Activity / Rate / Drops / Needs attention / Status, `ct-num` right-aligned | same | = |
| `:207` loading rows: 32 px thumb, name bar 60/45/70/52/66/40 %, cells 60/30/50/50 % | load-state template, same widths; bars in text-line-height rows | = / R (Q-SK2 skeleton row) |
| `:199` failure “Couldn’t load the catalogue” / “Nothing was changed. Try again.” / Try again, error icon | failure template | = |
| `:212` row: thumb, name button, sub “Category · matches X” | thumb `--art`, name link (`<a>`), same sub; row height 61.4 px = reference (measured) | = |
| `:213` rate “35 kills/hr”, “Not set” muted; Minigame “runs/hr” | same | = |
| `:214` drops “N” / “N + M inactive” | same | = |
| `:215` flags Rate missing / N without probability / N without value / No drops; “—” | same pills and tones | = |
| `:216` status badge Active (success) / Inactive (neutral) | same | = |
| `:506` placeholder art (coloured initial tile) | cached Wiki image or empty tile | R (thumbnail row) |
| `:224` empty: box icon, titles (empty / no match / no inactive / no active), texts, Clear search and filters, Add activity | same four states and both buttons | = |
| `:226` footer “N activities” / “N activities match” / “1 activity” | same | = |
| horizontal scroll classes `is-scroll` / `is-scrolled` (`:202`, `:1161`) | resize observer + scroll listener | = |
| selected row while its drawer is open | `is-selected` | = |
| search/filter replace the URL; filter in memory (`:654`, `:1006`) | in place, URL replaced (search after 250 ms) | = |

## Drawer — head, loading, missing, banner

| Reference | App | Status |
| --- | --- | --- |
| `:269` wide drawer, scrim, `aria-labelledby=drawer-title` | shell drawer layer, `is-wide`, family `catalogue` | = |
| `:271-277` thumb is-lg, eyebrow (category[· inactive] / Catalogue), title, Inactive badge, close × | same | = |
| `:280` loading: 50 % / 80 % (14 px) / 65 % (12 px) bars | `data-catalogue-drawer-pending` | = |
| `:281` missing: “This activity isn’t available” / “It may have been deleted, or the link is incomplete.” title “Not found” | same | = |
| `:283` banner with optional action button (Check current values) | `ct-dr-banner` + shared banner template | = |
| outside click / Escape with edits closes silently in the prototype | discard confirmation listing parts; clean drawer closes | R (decision B) |

## Drawer — Settings and Wise Old Man metric

| Reference | App | Status |
| --- | --- | --- |
| `:288` “Settings” / “Activity” (new) + “Changed” dot | same | = |
| `:290` Name, maxlength 200, field error | same | = |
| `:291` Category select with chevron | same (labels translated, values fixed) | = |
| `:292` Kills/Runs per hour + hint per category, error | same | = |
| Team size | beside Kills per hour, hint “The group size the rates assume. It isn’t used in calculations.” | R (CAT-1; hint wording row) |
| `:293` Image URL · optional, thumb, placeholder | full-width fifth field | = / R (CAT-1 layout) |
| `:295` recalculation note with info icon (rate changed) | same texts | = |
| `:298` disclosure “Wise Old Man metric” + status Verified/Unsupported/Couldn’t check/Not checked/Not set, chevron | same | = |
| `:301-304` help, Metric, placeholder “e.g. zulrah”, Suggest, hint + last checked, result, Validate and save, Save without validating | same; result texts `:761-773` | = |

## Drops list, Add drop, drop editor

| Reference | App | Status |
| --- | --- | --- |
| `:313` “Drops N”, Add drop (plus icon, aria-expanded) | same | = |
| `:314` new activity help | same | = |
| `:345` “No drops yet…” dashed box | same | = |
| `:349-355` row: thumb, name, sub “rate · 1 in N per kill · value”, flags Inactive/No probability/No value/Shared, EHB figure + “EHB / no rate / no EHB”, chevron | same | = / R (“untradeable, 0 gp” wording row) |
| `:318-341` Add drop: “New drop”, Item name (placeholder e.g. Tanzanite fang), Drop rate (placeholder, hint/preview), shared box + tick text + error, Catalogue value · for the new item, seg Enter value / Fetch price / Untradeable, GP input + unit, hints, Image URL, error banner, Add drop / Fetch price and add, Cancel | same | = |
| `:358` editor banner + Check current values | same | = |
| `:360-361` Item name + hint (shared/rename), Drop rate + preview | same, live preview incl. “N rolls of 1 in N” | = |
| `:363-367` merge box + “Point this drop at the shared item instead…” | same | = |
| `:369` Calculated EHB + note | same; “Unavailable” instead of “Not available” | R (wording row) |
| `:370` Image URL · shared item/optional | same | = |
| `:371` Save drop (disabled until changed), Discard/Close, “Saves the name, rate and image.” / “No changes yet.” | same; Super Admin text adds “and roll group” | = / R (decided panel) |
| `:374-378` “How the rate is counted”: summary rolls/team/conditional, rows chance per roll / rolls / whose chance / only after / note / source, operator sentence | summary “Roll group: X”; rows chance per kill (incl. rolls), source, note if present, roll group (Super Admin field); “Roll group can only be changed by the Super Admin.” | R (decided panel row) |
| `:383-393` Value and item mapping: summary, help (shared with), Stored value, Item ID status + checked, rejected price, Price source seg + hint, Manual value, Wiki item ID + Suggest, result, Validate and fetch price / Validate and save, Save without validating | same; outcome texts from the server’s structured result mapped to `:857-873` | = |
| `:398` Deactivate drop / Reactivate drop | “Deactivate drop…” (opens the S10 confirmation) / Reactivate drop | R (S10 composition row) |
| `:399` Delete permanently… (Super Admin) | same | = |

## Availability and footer

| Reference | App | Status |
| --- | --- | --- |
| `:411-413` Availability; Deactivate/Reactivate title + text + button; Delete permanently text + Delete… (Super Admin) | same; Deactivate text replaced (S10) | R (S10 row) |
| `:419-422` footer: dirty note (“Activity settings changed” / “Not added yet”), Close/Discard/Cancel, Save activity / Add activity (only when needed), spinner | sticky shared `dr-foot`, same states | = |

## Confirmations, outcomes, toasts

| Reference | App | Status |
| --- | --- | --- |
| `:1124` discard: “Discard unsaved changes?”, “Discards …” points, Keep editing / Discard (danger) | same | = |
| `:1126` activity deactivate: title, three points, Deactivate | S10 points with Board links, “(correction)”, Hidden pill / hidden count; drop gets the same composition | R (S10 rows); hidden link **Q1** |
| `:1128-1132` delete: Checking what uses it…; stale banner; “can’t be deleted” + It’s in use + Deactivate instead; deletable points, Delete permanently (danger) | same; banners carry `m-banner` | = |
| D7/D9 named-activity confirmation | “Change the shared item?” with the activity names | R (AU21/D7/D9 row) |
| `:739-743`, `:822-825`, `:906-909` save outcomes (fail / stale / uncertain + Check current values / success toasts) | same texts; lost session shows the shared “not saved” notice with the draft | = / R (C-CMP-2 row) |
| `:925`, `:839` deactivate toasts contradicting S10 | replaced | R (S10 row) |
| `:947-948` delete toasts | same | = |

## Roles and states

| State | App | Status |
| --- | --- | --- |
| Admin vs Super Admin | Delete controls and the roll-group field only for the Super Admin (server checks too) | = (`:399`, `:413`; decided panel) |
| Inactive activity | eyebrow “· inactive”, Inactive badge, Reactivate in Availability | = |
| Inactive drop | row `is-inactive`, Inactive flag, Reactivate drop | = |
| Phone width ≤ 640 | single-column grid, full-width search, scrolling seg (reference media block); document does not scroll (measured 390 px) | = |
| Danish | all strings in `AdminCommunityResource.da.resx`; localisation test passes | = |

Mismatches found while writing this list and fixed before the report: drop-row text gap (inline link baseline; now
61.4 px rows as the reference), dialog banners missing `m-banner`, drawer skeleton third bar gap (12 px), table
`is-scrolled`, drop-saved toast always “EHB recalculated”, provider and suggest outcome texts (now the reference’s).

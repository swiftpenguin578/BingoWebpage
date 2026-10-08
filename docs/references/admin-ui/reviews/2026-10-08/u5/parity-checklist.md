# U5 Participants — element-by-element parity checklist

Reference: `docs/references/admin-ui/Participants.dc.html` (frozen, D20). App: `Pages/Admin/Events/Participants.cshtml`
(list, menu, drawer and Add templates), `wwwroot/js/admin-participants.js`, `admin-design-participants.css` (family-scoped),
`ParticipantsListReader.cs`, `ParticipantDrawerReader.cs`. Shared components: shell header/summary, `_AdminDesignIcon`, the
shell drawer/modal/toast layers, `AdminFetch`, `AdminUI.busy`.

Legend: **=** matches the reference; **R** registered difference (DELIVERY_PLAN “Bindings not shown in the design
references”, named); **Q** open question for the user in the U5 report. Measured: Chromium and WebKit conformance at
390/494/860/1280/1440 px pass (`BINGO_CONFORMANCE_PAGES=participants`); real-browser flows in `admin-design-participants.browser.js`
(both engines); review scenarios in `scripts/check-u5-ur.cjs` (screenshots in `ur/`).

## Page header, banner, toolbar

| Reference | App | Status |
| --- | --- | --- |
| `:117` h1 “Participants” | layout h1 | = |
| `:118-125` summary “N of M confirmed · N waiting · N unpaid”; “M spots · no signups yet” when empty | `HeaderSummary` section, same words, tabular numbers; loading shows the fixed words with number-sized bars | = (rule 4/5) |
| `:128-131` “Roster locked” chip with the lock reason as title | `head-actions` chip, same icon and text | = |
| `:133` “Add participant”, primary, plus icon, disabled when locked with a title | `#add-btn`, same; opens the Add drawer (`?add=1`) | = |
| `:137-139` info banner “{reason} Accounts, signup answers and statuses can’t be changed. Payments and private notes can still be updated.” | same sentence from the roster policy; `info` icon (reference uses the lock/info glyph of `page-banner`) | = |
| `:143-146` status tabs “Participation status”: Confirmed, Waiting list, Withdrawn, All with counts | same order, labels, counts (`tab-count`); in-page update keeps focus | = |
| `:151` search placeholder “Search RSN, account or Discord”, clear × when text | same placeholder, label “Search participants”; clear × | = |
| (reference search matches RSN only in the prototype) | matches every account RSN, website username and Discord name (B-Participants-5) | R B-Participants-5 |
| `:157-160` Payment filter Any / Paid / Unpaid | same | = |
| (reference has no Discord/captain/source/team filters) | none (B-Participants-6 retired them) | R B-Participants-6 |

## Table

| Reference | App | Status |
| --- | --- | --- |
| `:165-177` columns Participant / Status / Draft EHB / Payment / Captain / Team / Signed up / Actions; sortable except Actions; `aria-sort` | same columns and classes; sort buttons are `data-participants-url` links in the query | = |
| `:196-200` name button opens the drawer | `a.name-btn` to `?participant={id}` (a link so it opens in a new tab, Accounts pattern) | = (link instead of button) |
| `:201-202` flags: private note, “Participant left a note” | private-note flag kept; participant-note flag dropped | R S1 |
| `:204` sub line “@user · +N accounts · alt X” | same words, singular/plural | = |
| `:206` status badge | same; waiting shows “Waiting · #N” | = |
| `:207` Draft EHB right-aligned | same; formatting only, stored values never rounded | = |
| `:208-216` pay toggle (paid/unpaid icon, aria, busy spinner), disabled when private edits are not allowed | same; works in every state except Discarded | R S4 |
| `:217` captain “Volunteer / No” + “with {co-captain}” | same | = |
| `:218` team name / “Unassigned” / “—” (withdrawn) | same; manual-team members included | R G3b-3 |
| `:219` “#order” + date | same, event time zone | = |
| `:220-222` actions button (`more`), menu | same; menu below | = |
| `:224-253` loading skeleton rows, failed (“Couldn’t load participants … Your filters are kept. Try again”), empty states per tab, “No matches” + “Clear search and filters” | same texts, `pa-sk-*` skeleton classes (no inline styles); first-ever empty state keeps “Add participant” when editable | = |
| `:256-269` footer: Rows per page 10/25/50/100, range, pager with gaps | same | = |

## Row menu (`:798-832`)

| Reference | App | Status |
| --- | --- | --- |
| Open details | same | = |
| Mark as paid / unpaid (disabled when private edits not allowed) | same | = |
| Confirm (“N left”) / Confirm and add a place… (full; title explains +1) | same; Locked hint when the roster is locked | = |
| Move up / Move down in queue | removed | R S13 |
| Move to waiting list… (hints Places open / No one waiting / title) | same | = |
| Withdraw… (danger) / Restore… | same | = |

## Confirmations (`:1395-1436`)

| Reference | App | Status |
| --- | --- | --- |
| Withdraw: waiting vs confirmed body, team removal, paid note, “You can restore them later.” | same words; the confirmed body adds a variant when nobody is waiting; promotion is automatic, no suppress option | = / R U5-E8 (toast) |
| Confirm and add a place: “{event} is full (N/N). Capacity increases to N+1 …” | same | = |
| Move to waiting list: end-of-list position, next waiter takes the place, team removal | same | = |
| Restore with “Restore to” choice (Waiting list / Confirmed or Confirm and add a place), capacity text | same, incl. “Restore and add a place” label | = |
| Discard unsaved changes? “Your changes to {name} haven’t been saved.” / Keep editing / Discard | shared discard dialog (decision B), page-wide wording | = (shared) |
| Failure “Couldn’t {verb} {name}. Nothing was changed.” (`:909`) | server’s refusal reason for a definite refusal; “We couldn’t confirm whether this happened …” for a lost response | R U5-Q3 |
| Locked: “Roster changes aren’t allowed: {reason}” | server refusal shown with its reason | = (via server reason) |

## Toasts

| Reference | App | Status |
| --- | --- | --- |
| `:859` “X marked as paid/unpaid” | same | = |
| `:873/:938` “X confirmed [· capacity now N]” | same | = |
| `:919` “X withdrawn · Y confirmed” | “X withdrawn · Y confirmed from the waiting list” | R U5-E8 (wording, Q) |
| `:929` “X restored to confirmed / the waiting list (#n) [· capacity now N]” | same | = |
| `:950` “X moved to the waiting list (#n) · Y confirmed” | same | = |
| `:1071` “Changes to X saved” | same | = |
| `:1119` “X added to confirmed / the waiting list (#n) [· capacity now N]” + Show | same; Show filters the list to the person | = |
| Lost session | the shell shows what wasn’t sent (action, participant) before Sign in | R C-CMP-2 |

## Participant drawer (`:303-431`)

| Reference | App | Status |
| --- | --- | --- |
| head: eyebrow “Participant · @user”, name, status badge, team chip, Close | same | = |
| banners: save error, error summary, locked info | locked info; withdrawn “read-only … Restore to edit” (B-Participants-3); outcomes via `drawerMessage` | R B-Participants-3, U5-Q3 |
| Entry payment Paid/Unpaid, “Was X” aside when changed | same | = |
| Playing accounts: header Primary/RSN/EHB, rows (radio, RSN with owner datalist, EHB number, remove), “Add playing account”, “N of M account slots”, “Draft value N EHB” | same; EHB max 100,000; typed RSN accepted as an event-only account (hint “Not one of @user’s saved accounts. Used for this event only.”); RSN rule 1–12 letters, digits, spaces, `-`, `_` | R U5-Q1, U5-Q4, B-Participants-7 |
| read-only list for locked | same, with Primary pill | = |
| Alt account (single optional field, no EHB) | one field per Informational slot, labelled by the slot question (“Alt account” when one slot); section hidden with none | R U5-E6 (Q: wording) |
| Signup answers: captain Yes/No, co-captain text, “Participant’s note” quote | every custom question as “question: answer” editable until draft start; captain default No; Participant’s note dropped | R S1 |
| (not in reference) answers to retired questions | shown read-only | R U5-E2 |
| Private note textarea, counter 500 | counter 2,000 | R B-Participants-7 |
| Signup details: order, signed up, Source, Website account “· ID n”, Member since, Discord “name · linked” | order, signed up, Source (Website signup / CSV import / Added by an admin), Website account (@username, no numeric ID), Member since, Discord “Linked / Not linked” | R U5-E1, U5-E5 |
| foot: Withdraw… / Restore… (disabled when locked), Unsaved changes, Cancel, Save changes | same; one Save for the whole drawer; status buttons ask to discard first when the drawer is dirty | R U5-Q2, U5-E4 |
| dirty Close/Escape/backdrop | shared discard dialog; Back/Forward per decision B | = |
| (not in reference) drawer URL, missing participant, loading, failed read | `?participant=`; “Participant not found”; skeleton; “Couldn’t load this participant … Try again” | R FC “Required integration behavior” |

## Add drawer (`:433-519`)

| Reference | App | Status |
| --- | --- | --- |
| head: event name eyebrow, “Add participant”, Close | same | = |
| Website account search “Search by username, Discord or RSN”, results label, result rows (avatar, @user, “Owns X” / saved accounts · Discord, note) | same; results label “N matches”; note “N saved account(s)”, “Already in event”, “Withdrawn · restore instead” (disabled) | = (search scope R B-Participants-5) |
| result sub shows Discord name | same (Add search shows the Discord display name; the drawer does not) | R U5-E1 (Q) |
| picked row with Change; sub “Discord · ID n” | picked row with Change; sub is the Discord name or “Discord not linked” (no numeric ID) | R U5-E5 |
| Playing accounts: placeholder, picks with checkbox, EHB, Primary radio, reasons (“No saved EHB — update the account first”, “Already in this event”), error text, hint, “Draft value” aside | same; slot limit from the event’s Playing slots (“All N account slots are used”) | = |
| Add to: Confirmed (N of M spots left) / Waiting list; full event: “Confirm and add a place” + capacity hint | same; full event defaults to the waiting list | = |
| Entry payment Unpaid/Paid | same | = |
| error banner “Couldn’t add the participant. Nothing was saved.” + Retry | server refusal reason; stale: “The event changed while you were adding …”; lost response: “We couldn’t confirm whether @user was added …” (never resubmitted) | R U5-Q3 |
| foot Cancel / Add participant (busy “Adding…”) | same | = |
| (not in reference) history | `?add=1` uses replaceState: no history entry that could resubmit | R FC “Required integration behavior” |

## Registered differences added or confirmed by U5

B-Participants-3, B-Participants-5, B-Participants-6, B-Participants-7, G3b-3, S1, S4, S5, S13, U5-Q1, U5-Q2, U5-Q3, U5-Q4, U5-E1, U5-E2, U5-E4, U5-E5, U5-E6, U5-E8, C-CMP-2 (Participants paths). All rows are in `DELIVERY_PLAN.md` “Bindings not shown in the design references”.

## Mismatches without a decision

None open. Items marked **Q** (U5-E1 Discord name in Add results, U5-E6 alt-slot wording, U5-E8 promotion toast wording) are in the U5 report with options.

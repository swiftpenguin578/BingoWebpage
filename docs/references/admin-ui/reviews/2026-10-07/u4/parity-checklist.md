# U4 Overview — element-by-element parity checklist (item 1 first working version)

Reference: `docs/references/admin-ui/Overview.dc.html` (frozen, D20) and README `:830-1042`. App:
`Pages/Admin/Events/Manage.cshtml` (route kept, A2), `Manage.cshtml.cs` (load, handlers, `GET ?handler=Current`),
`OverviewPresenter.cs` (vmStages / vmIssues / vmNow / vmGlance / vmLinks / vmMore / dialog models),
`Pages/Shared/_AdminOverviewLoadStates.cshtml` (header + skeleton), `wwwroot/js/admin-overview.js` (dialogs, evidence
codes, copy, in-place refresh), `wwwroot/css/admin-design-overview.css` (reference style block `:21-54`, family-scoped).
Shared: `_AdminDesignIcon`, confirmation template, toasts, `AdminUI.openLayer/busy/update/refreshContext`,
`AdminFetch`, `mountDateTime` (`admin-date-time.js`).

Legend: **=** matches; **R** registered difference or recorded decision (named); **Q** open question in the report.
Executed: Chromium and WebKit at 390/494/860/1280/1440 (conformance), 1280 × 900 (`admin-design-overview.browser.js`),
1440 × 1000 screenshots of every parity-fixture phase (scratch, not committed).

## Header, loading, failure

| Reference | App | Status |
| --- | --- | --- |
| `:125` `.ov-title` h1 event name + `badge {phase}` | `HeaderTitle` layout section: `.ov-title` h1 `#page-h1` + badge; phase colours `AdminDesignPhasePresentation` | = (colours: phase-colours decision) |
| `:126` summary “range · tz · Private draft” | same text; range “26 Nov – 5 Dec 2027”, year only off-year | = |
| crumbs “Events / ‹event› / Overview” | topbar `CrumbTitle` = “Overview”; skeleton crumb “Overview” | = |
| `:131-134` loading: stages card (6 × circle + 2 bars) and two panel cards | `_AdminOverviewLoadStates`: same bars; bars wrapped in `.stage-text` / `.ov-sk-*` rows so phone layout keeps line heights (conformance text rows) | = (structure note) |
| loading head keeps name + badge + summary | name only (event switcher name), summary reserved empty, no badge | R (row “Overview loading header”, U3-Q5) |
| `:135-142` “Couldn’t load this event / Check your connection… / Try again” | shared `load-failure` template, same texts | = |
| `:143-150` “Event not found / It doesn’t exist, or it isn’t available to you. / Back to Events” | HTTP 404 (shared admin Not Found, C-CMP-1) | Q (see report) |
| fade-in on cards | none (“No load fades”) | R (decision) |

## Hidden event (U4-Q3 (c))

| Reference | App | Status |
| --- | --- | --- |
| `:154` warning banner “Hidden event · limited inspection.” + text, eye-off icon | same, `_AdminDesignIcon("hidden")` | = |
| `:156-161` “Quarantine” facts: Retained stage, Hidden “‹time› by ‹actor›”, Reason | same (actor from the `event.hidden` audit entry) | = |
| `:162` act row “Restore event” + sub + `Restore…` | same; a real `<form handler=RestoreHidden>` (no-JS fallback; quarantine tests) intercepted by the Restore dialog | = |
| no history list | “Quarantine history” `ro-list` (hidden/restored, time, actor, reason) | R (U4-Q3 (c)) |
| plain event URL opens it for SuperAdmins | plain URL; `?hidden=true` accepted; admins 404 | R (U4-Q3 (c)) |

## Progression (`vmStages`)

| Reference | App | Status |
| --- | --- | --- |
| six stages Setup → Archived, `--stage-count` | same | = |
| done: actual moment (“Opened 1 Jun, 18:00”) / current / upcoming “Opens … · scheduled”, “Planned … · opens manually”, “Not scheduled” | same texts from the event’s actual/scheduled fields | = |
| overdue Live “Was due … · postponed” `is-overdue` + sr text | same (unresolved postponed start attempt) | = |
| Archived “Results published ‹date›” | same (`ArchivedAt`) | = |
| cancelled: stages reached + “Cancelled ‹time›” `is-ended` × icon | same (`ended` icon added to `_AdminDesignIcon`, reference path) | = |
| — legacy Finished | last stage “Finished”, results panel | R (row “Legacy Finished”) |

## Needs attention (`vmIssues`)

| Reference | App | Status |
| --- | --- | --- |
| “Automatic start postponed” only in Signups closed, “It was due … and won’t retry. N requirements below still need doing / Everything is ready now…” | same text; also in Setup (“Open and close signups first.”) and Signups open (“Close signups first.”), plus the S2 sentence | R (U4-Q1 (c)) |
| — | “Signups didn’t open automatically”, “It was due ‹time›: ‹reasons›.”, `Open signups now` (opens the Open dialog, inert with the checklist) | R (S3; wording proposed) |
| “Wise Old Man sync failed” (Live), “Last attempt ‹time›. Lifecycle actions aren’t affected.”, `Open WOM` | same; rule: LastError, missing accounts or Complete = false | = (A-Overview-7) |
| “N submissions to review” (Live, pending dot) “Teams are waiting…”, `Review submissions` | same | = |
| — | Final review + WOM end Pending: “Wise Old Man end not updated yet” (pending); Rejected: “Wise Old Man rejected the new end” (failure); `Open WOM` | R (A15; wording proposed) |

## Stage panel (`vmNow`)

| Reference | App | Status |
| --- | --- | --- |
| Setup “Private draft” lead; transition variants (automatic / planned / none + Schedule button); `Open signups now` | same three variants and texts | = |
| “Before you can open signups”: description, capacity, window, closing time (auto), signup form | same rows, owning-page links (Identity, Signup setup, Schedule, Signup setup `?tab=form`) | = |
| — | A-Overview-3 rows when they apply: Signup code (Signup setup), Discord sign-in (no page fixes it → no link), overlap (Schedule), future close (Schedule) | R (A-Overview-3) |
| later note “Later, before the start: …” | same | = |
| Signups open lead “N of M places confirmed, W waiting. Opened …”; close transition; later note + draft time | same | = |
| Signups closed “Getting ready to start”; transition auto / postponed / none; Reopen (if draft unlocked) + Start | same | = |
| start checklist: draft, board (“N of M tiles configured”), drop values, playing accounts, end still ahead | same; drop row sub = missing item names; playing row grouped “N participants need one” | = |
| — | S2 row “Publish the results of ‹X› first” linking the other event; Finished: “‹X› is still the current event…” | R (S2, U4-Q5) |
| — | blocked Reopen: “Before you can reopen signups” with the failing signup rows | R (A-Overview-3 row) |
| Live title, aside “Ends in N days”, lead teams/players, end transition | same | = |
| Final review lead, upload transition variants, “Before you can publish official results”: uploads closed, every submission reviewed, placements calculated | same; placements row from the server’s calculated-placements blocker | = |
| — | “N more on Final review” row | R (U4-Q2 (c)) |
| foot “Publishing makes the results official…” `Open Final review` | same (link) | = |
| Archived: “Results are official”, highlight trophy “Official winner”, placings “1st · team — N of M tiles”, foot `Final review & results` | same (official placements of the active finalization; `trophy` icon added) | = |
| Cancelled: title, lead, facts “Cancelled ‹time› by ‹actor›”, “Private admin reason” | same (actor from the `event.cancelled` audit entry) | = |
| control `aria-disabled` + `aria-describedby=checklist` + title “Finish the requirements below first”; click → toast “Finish the requirements listed first.” | same (Reopen points at its own list) | = |

## Other actions (`vmMore`)

| Reference | App | Status |
| --- | --- | --- |
| Recovery: Resume (not after finalization history, OS-3), Reopen uploads | same | = |
| Remove: Delete (empty setup) / Cancel (protected history) `btn-outline-danger` | same (server’s CanDiscard rule) | = |
| Quarantine · SuperAdmin: Hide (Final review / Archived) | same + legacy Finished | R (row “Legacy Finished”) |

## At a glance (`vmGlance`)

| Reference | App | Status |
| --- | --- | --- |
| Participants “N / cap”, sub waiting / confirmed / No capacity set → Participants | same | = |
| Teams “N teams”, “N players” → Teams | same (players = active memberships) | = |
| Board published / “N / M tiles” / Not set up; hidden in Signups closed | same | = |
| Approved submissions → Review (not linked when archived) | same | = |
| Uploads Open/Closed/Reopened + time | same | = |
| Evidence codes value/sub, `Manage evidence codes` opens dialog | same | = |
| Wise Old Man Linked/Not linked, “Synced 12 min ago” | same; “Synced N min/h ago” from LastSuccessfulAt | = |
| label link chevron `.ov-fact-btn` | same | = |

## Public links (`vmLinks`, RC01 R1)

| Reference | App | Status |
| --- | --- | --- |
| never public: “Not public yet…” (none when cancelled) | same | = |
| permanent link + note by destination (board / teams / signups / cancelled) | same facts: FirstPublicAt, board published, active roster publication, results (Archived/Finished) | = |
| Signup form only while accepting and nothing published; Teams; Board | same | = |
| path shown, Copy copies absolute URL, “Copied” 2 s, toast “Link copied.” / error toast | same; absolute base = request origin | = |

## Lifecycle dialogs (`actions`, `vmDlg`)

| Reference | App | Status |
| --- | --- | --- |
| `modal is-wide` alertdialog, title, banner, `ov-effects`, fields, Cancel/confirm | shared layer + confirmation template, same classes | = |
| Open / Close / Reopen / Start / End / Reopen uploads / Delete / Cancel / Hide / Restore titles, effects, confirm labels, classes | same texts (server-rendered, Danish) | = |
| Open/Reopen “Answers to text questions will be public…” when it applies; no “keeps existing signups” | added when `PUBLIC_FREE_TEXT` | R (A-Overview-6) |
| Start “first WOM refresh runs in 2 hours” | “…in 1 hour” | R (F5) |
| End reason only before the scheduled end | same | = |
| End early effect | adds “Its end time becomes ‹ceiling minute›” and “resume … with a new end” | R (AU20; wording proposed) |
| Resume replacement end only when the end has passed | always asked (default: old end if still ahead, else +2 days), hint proposed | R (AU20; wording proposed) |
| — | Resume requirement banner (U4-Q4), confirm disabled | R (U4-Q4 (b)) |
| until hint “In 5-minute steps, ‹tz›.”; errors: choose / future / after start / 5-minute step | same client checks (after-start is the server’s refusal, shown on the field) + DST invalid/ambiguous messages (RC03 R3) | = |
| states: busy “Working…”, failed, stale (refreshed, confirm again), gone (only Close), uncertain “Check again”, checking, notApplied | same states; stale “what” = latest audit action (actor) | R (row “Stale … names the latest recorded change”) |
| refusal reason | server reason shown as a refusal (U3 review B-M1) | = |
| success toast, page update, focus `#now-title` | same; in-place `AdminUI.update` (scroll kept) | = |
| closing after uncertain/gone re-reads the page | same | = |
| scrim click closes | Decision B / A12: dialogs with entries (reason, time) close untouched and ask to discard when edited; input-free confirmations do not close on an outside click; Cancel/Close use the same discard check | R (decision B, A12) |
| session loss | C-CMP-2 notice lists the unsent reason/time; dialog kept | R (C-CMP-2 row) |

## Evidence codes dialog (RC01 R2)

| Reference | App | Status |
| --- | --- | --- |
| `modal modal-form`, “Evidence codes” + description | same | = |
| `check-row` “Require evidence codes” + sub; toggle saves at once | same; chosen state shows at once while saving | = |
| `ro-list` rows: code (mono), “Active since / Starts / From”, note, pill Active/Scheduled/Ended | same | = |
| New code + Generate; “Active from · tz” picker; Note “· optional, admins only” | same | = |
| errors: “Enter or generate a code.”, “Choose when it becomes active.”, “Another code already starts at that exact time.”, “Turn on evidence codes first.” | same + server duplicate refusal on the field | = |
| fail / uncertain banners | same texts; no blind retry | = |
| Close / Add code (“Saving…”) | same | = |

## Not ported (prototype only)

Proto URL bar, account-menu outcome simulator, “Another admin changes this event now”, “Fail next load”, phase-sample
switcher list (A7 switcher is the shell’s).

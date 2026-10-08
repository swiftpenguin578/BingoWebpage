# U9 early-look parity checklist — items 1a–2b

This is the implementer's binding/source checklist against the frozen `FinalReview.dc.html` and `Wom.dc.html`, with the brief90 final planner rulings. It is not independent review or manual visual acceptance. Chromium/WebKit conformance and interaction results are recorded in the item evidence. References remain frozen; obsolete inventory expectations are reconciled below.

| Reference element / behavior | Application binding | Result |
| --- | --- | --- |
| Final header, badge, lifecycle summary, relevant dates | `Finalize.cshtml`, presentation read model | Bound; authoritative lifecycle and event timezone |
| Final readiness rows / links | All server blockers plus domain submission-window state | B-Final-1; additional concrete blockers, no invented current-event publish row (narrowed AU18) |
| Standings, score, decisive tie-break, row explanations | Placement-rule-specific presenter, authoritative placements | Bound; shared ranks computed before top-three slice, `=3` retained |
| How placements are decided disclosure | Shared disclosure behavior and rule explanation | Bound |
| Official stamp, version, publisher, retained WOM note | Current stored snapshot and non-success final-refresh note | AU18; success note omitted here, no next eligible time |
| History and retained published/reopened facts | Stored actors, times, reasons and versions | Bound; no rewritten history |
| Version drawer, direct link, Back/Forward | Positive `?version`, shared layer/URL state | A2 / RC08; invalid dropped, unknown unavailable |
| Publish confirmation and top three | Shared confirmation; full-list ranks first; final refresh explanation | A15 adds nonblocking last-pre-end-data sentence |
| Publish outcome | In-place success/skipped/failed notice and current stored snapshot | RC08; uncertainty never attributes another request's publication |
| Reopen card and reason | Mandatory expected version; reason retained per event, maximum 2,000 | B-Final-2 / RL-1 / BR-12 |
| Reopen another-current-event refusal | Disabled button + state-specific explanation + fixed Open event link | U9-Q1; Live, Awaiting review, legacy Finalized ruled strings |
| Final loading, failed, cancelled, official states | Shared skeleton/failure and truthful read-only presentation | Legacy publication completion and authorized Reopen retained |
| WOM header, badge, event facts | Service-backed connection, issue and lifecycle | Bound; Cancelled/Finalized truthful additions |
| One prioritized WOM issue | Unknown operation, queued delete, unmatched end, conflicts, credential, source, rate/unavailable, old data | A15 end status in existing priority slot; stale-data information retained |
| Connected card, provider link, origin/capability badge | External/Website-managed/Unknown from services | AU20 / C-WOM-3; no external delete, Unknown locked |
| Latest data / account coverage / next fetch stats | Last successful fetch, actual activity projection and eligibility | RC09 W3; timestamps explicitly historical, terminal/pre-live/end-paused next fetch empty |
| Fetch now, busy, reason/time | Ordinary click, shared busy, typed skip reason and eligible time | AU15 / U9-Q2 / WA-6; no typed confirmation, no make-due control |
| Coverage table, meters, missing accounts and guidance | Actual teams, expected/matched accounts, missing names, phase/capability | Bound; no synthetic provider data |
| Updates to Wise Old Man | Typed current operation, credential and LastAppliedAt | Generic “latest details” because payload is unavailable; unknown never Up to date |
| End update Pending/Rejected/could-not-update | Main issue and Updates card, exact target; stored matching operation retry only | A15 / U9-Q2; no computed retry time; stop permanently after publication |
| Create's three reference checks | Before Live, finalized teams, future schedule | C-WOM-2; every additional applicable server refusal appended using existing string mapping |
| Create/Link unavailable during unresolved work | Service flags and current operation | WA-9; includes Retry/Unknown, no duplicate resend |
| Link/Replace dialog | Exact UTC window requirement, event version, validation, service CanLink | AU20; available external replacement before/during Live, no reuse of old code |
| Disconnect dialog / optional reason | Service CanDisconnect, normal version guard, local removal only | AU20; before first Live even with stored/rejected code; remote unchanged |
| Management code | Service eligibility; protected server storage, unsent local draft only | RC09 W4; submitted code clears before awaiting response and is absent from session-loss draft |
| Delete dialog | Explicit website-created target, version and service CanDelete | Bound; no external delete; persisted Retry is queued rather than a no-work refusal |
| Technical details | Exact UTC windows, attempts, errors, request budget, end state, operation type/phase/id/time | U9-Q2 added typed outputs in reference disclosure pattern |
| Unsure outcome / check status | No-store local readback; per-event pending intent; explicit failure when read unavailable | RC09 W2/W3/W5; never uses old LastSuccessfulAt as proof or retries a write automatically |
| Modal/switch/reload behavior | Shared layers, scroll lock, dirty/pending guards and sessionStorage per event | RC08 / RC09 / C-CMP-2; no shared modal framework changes |
| Both page loading/frame/responsive geometry/Danish | Registered families, page CSS, shared primitives and translations | Automated conformance at five widths in both engines; manual acceptance pending |

## Inventory leftover reconciliation (42e Final Review and 42f WOM only)

Executed searches find no hidden-inspection code, typed Fetch on these pages, optional expected-version bypass, recorded-next-eligible-time Final UI, generic cooldown, retired WOM labels, approximate window copy, or lifecycle-confirm usage on the two bound pages. `admin-lifecycle-confirm.js` still contains its legacy fallback “Type FETCH”; it is explicitly protected by this brief and these pages no longer load it. Other five-minute references concern Schedule input precision/cache duration; they are outside WOM window matching. The frozen WOM reference still contains its old tolerance copy, while application copy and server validation require exact equality; the brief's app-only mechanical rule controls.

The earlier inventory's route-removal expectations are superseded by brief90's instruction to retain the GUID route URLs. `/Admin/Events/Finalize/{id}` is used only as the route or inbound navigation. Finalization blocker destinations `/Admin/Events/Board/{id}`, `/Admin/Review?eventId=…&status=Pending`, `/Admin/Audit` still resolve to the current Razor pages; no lane A/B files were changed. Focused PostgreSQL checks from 0a/0b and 2a establish hidden 404, terminal GET/read-only and refused writes; browser scenarios additionally exercise Live and Awaiting-review Reopen blockers.

No new unruled product behavior was needed. Exact-window, extra refusal rows, typed outcomes, status placement and terminal wording differences are recorded in the U9 DELIVERY_PLAN register rows. Recommendation: review these bound wordings and states in the planner-served early look; retain the ruled behaviors and adjust copy only if the user requests it.

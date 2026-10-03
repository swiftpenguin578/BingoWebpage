# Admin UI — functionality and delivery register

Updated 2 October 2026. Records agreed behavior, remaining decisions and actual
delivery progress for the new Admin UI. Keep completed entries; update their state
and evidence rather than deleting them or leaving everything labelled pending.
Neither a working prototype nor inclusion here means the application supports it.

## Page progress

Reference composition acceptance is owned by `UI_PAGE_MATRIX.md`: all 15 named
pages are visually accepted. Source-review defects remain open; visual acceptance
does not approve false outcomes, lost drafts or missing authorization safeguards.
The reference freeze and AU01–AU10 evidence are committed at `1e8d457`;
[durable evidence](reviews/2026-10-02/README.md) records the exact identities.

| Area | Application state | Remaining work |
| --- | --- | --- |
| Participants | F01–F06 technically complete; executed proof and independent PASS | New reference binding and application acceptance |
| Dashboard | D01–D09 technically complete; executed proof and independent PASS | D10 binding and application acceptance |
| Events | AU03/AU04 technically complete; executed proof and independent PASS | Modal, filters, routing and reference binding |
| Identity | AU08/AU09 technically complete; executed proof and independent PASS; named reference recheck SOURCE PASS | Conflict choices, readback, stay-on-page and reference binding |
| Overview | Existing lifecycle plus AU01 technically complete, executed and independently passed | RC01 and application binding |
| Signup setup | AU02/AU05–AU07 technically complete, executed and independently passed | RC02 and combined-page binding |
| Schedule | AU10 technically complete, executed and independently passed | RC03 and picker/form/navigation binding |
| Teams / Draft | Existing draft/roster commands retained | AU14, RC04 and application binding |
| Board | Existing board commands retained; AU11–AU13 approved but not implemented | B1–B7 → RC05; additional capability scope proposed as AU19 |
| Audit | Existing audit service/presenter retained | A1–A4 → RC06; query/read gaps proposed as AU16 |
| Review | Existing evidence commands retained | R1–R8 → RC07; projection/outcome gaps proposed as AU17 |
| Final Review | Existing publication/reopen/snapshot services retained | F1–F6 → RC08; readiness/outcome gaps proposed as AU18; ranking AU12 |
| WOM | Existing management/fetch protections retained; AU15 approved but not implemented | W1–W6 → RC09; projection/recovery proposed as AU20; option 1 / exact UTC matching approved, implementation pending |
| Catalogue | Existing CRUD/mapping/value services retained | C1–C8 → RC10; explicit adoption/recovery proposed as AU21; approved SuperAdmin mechanics target AU23 |
| Accounts | Existing role/status/reset/transfer services retained | A1–A8 → RC11; transport/recovery proposed as AU22; approved typed transfer target AU24 |

AU11 onward remains stopped. New ticket records allocate scope, not execution.
Implemented, executed, independently reviewed, reference-accepted and application-
accepted are separate states. Original source reports are immutable evidence of
what was reviewed then; later user decisions below supersede their recommendations.

## Reference decisions — 2 October 2026

- **Catalogue advanced drop mechanics:** ordinary Admins see read-only values;
  SuperAdmin may edit them. Includes team chance, participant assumptions,
  conditional probability, reward-roll settings, mechanics notes and source already
  presented in the advanced section. Server authorization, validation, concurrency,
  audit and approved/historical snapshot protection are required. No separate
  roll-group editor or import workflow. Approved scope; not implemented or tested.
- **Ownership transfer:** retain the current owner's password and require typing
  the destination public username as strong confirmation. This resolves the existing
  requirement/implementation discrepancy in favour of PRODUCT_REQUIREMENTS 5.4.
  Preserve atomic sole-owner transfer, eligibility, version checks and session
  invalidation. No written reason or new typed confirmation for ordinary role
  grants/revocations. Approved correction; not implemented or tested.
- WOM option 1 and exact UTC matching, and full-event-pool evidence correction,
  are approved below. Conflict re-send and schedule-slot relaxation remain deferred.

## Review reconciliation and requirements summary — 2 October 2026

**Approved product target for Claude's whole-branch review.** This summary records the
intended product, not a specification reverse-engineered to match implementation.
Existing explicit approvals remain valid. Proposed delivery scopes do not authorize
execution; accepting the summary does not silently resume AU11 or later tickets.
Base `22af254c893bb51e7820d84fc4154ff9af3bcc90`; implementation/reference checkpoint
`1e8d457416a275514f4a7f0822dda7a192bbff3f`. A later documentation commit must fix
the review requirements. The short handoff names exact commits and known unfinished
work; Claude chooses its own review procedure.

The rows below state the material conditions and exceptions explicitly. Review
them with the linked detailed contracts, which also define validation, concurrency,
privacy and failure handling. An omitted technical detail is not permission to drop
an existing safeguard; a disagreement between summary and contract must be reported.
This wording clarification changes neither approved behavior nor delivery status.

| Area | What it should do / change from the previous application | Delivered versus pending |
| --- | --- | --- |
| Participants — Admin Add | Before team-draft lock, use an existing active website account and at least one active saved Playing account, within configured slots; choose a primary, use stored EHB without WOM calls and save Paid/Unpaid (default Unpaid). No signup code or questionnaire; captain volunteering defaults to No. Normal Add confirms when space exists and waits when full. **The admin can instead add directly to Confirmed when full: explicitly increase capacity by exactly one and confirm only the newly added person, without promoting another waiter.** Creation, placement, accounts and payment succeed or fail together | F01–F06 backend complete; reference binding pending |
| Participants — confirm, restore and waiting list | Confirming a selected waiter uses an available place or, when full, explicitly adds one place for that person only. Normal restore confirms if space exists, otherwise joins the end of the waiting list; **Restore as confirmed when full** explicitly adds one place for the restored person only. Neither override promotes another waiter. Move a confirmed person to the queue's end only when full **and another eligible waiter exists**; promote that existing waiter, end the moved person's current team/leadership membership with history retained, and disclose team removal. Disable this action when space exists or there is no eligible waiter. Ordinary capacity increases and confirmed withdrawal still promote eligible waiters normally | Backend complete; applicable pre-team-draft locks remain. No toast Undo, general queue reordering or option to suppress withdrawal promotion |
| Participants — account edits and lifecycle | Pre-team-draft signup-pool management is allowed in event Draft, SignupOpen and SignupClosed; event Draft does not mean the team draft has started. Exactly one Playing account supplies draft EHB; switching primary preserves the others and their EHB. Event assignment/EHB edits never rewrite saved account records. Existing-participant account editing is not restricted to the saved-only picker used by Add. Payment/private notes keep their broader permitted window. Finalized pre-first-Live roster Add/Remove is a separate Teams workflow; first Live permanently locks membership/registration | Backend complete; UI binding/application acceptance pending. No Live withdrawal or replacement workflow |
| Dashboard | Totals/chart/history include accessible non-hidden Live, final-review, finalized and archived events; exclude cancelled/discarded events. Live/final-review figures are Provisional; the latest-event recap uses only the latest **ended** event and official winners use the active official snapshot. Count each person once per event despite multiple characters; distinguish imported/unlinked identity and unavailable values from zero. Reads never fetch WOM | D01–D09 backend complete; D10 UI binding pending |
| Events / Overview | One directory with normal filtered views and a separate SuperAdmin-only Hidden view; show actionable failures/review counts without treating every unfinished setup task as a failure. Create a private Draft from name/timezone with a permanent slug; the same actor/request key and input cannot duplicate it, while deliberately new same-name events remain allowed. Overview offers only phase-relevant manual signup/start/end/resume controls; manual actions supersede their corresponding scheduled transition, not unrelated ones. Early end gives 30 minutes' upload grace. Hiding/restoring respects current-event exclusivity; distinguish disposable Delete from protected-history Cancel | AU01/AU03/AU04 complete; RC01 and integration pending. Capacity belongs to Signup setup |
| Identity / Schedule | Name/description/buy-in remain editable through Live/Final Review; timezone only before first Live, preserving UTC instants. Merge untouched fields but require explicit resolution for conflicting same-field edits; newer changes invalidate that resolution. Preserve unchanged schedule instants exactly; changed values still pass timezone/order/phase rules. Timezone confirmation reviews current consequences and becomes stale if the schedule changes. Live end changes require confirmation/reason. Uncertain saves compare complete intended values and report current state, not proof this request saved it; read failure stays uncertain and does not trigger a blind retry. Keep permanent `/Events/{slug}/Signups` entry and stay on the edited page after save | AU08–AU10 complete; Identity reference recheck passed; RC03 and UI binding pending |
| Signup setup | Capacity, signup code and form configuration share the page. Before team-draft lock, capacity cannot fall below Confirmed count; ordinary increases promote earliest eligible waiters. Waiting remains enabled while signups are open. Code gates first signup/self-rejoin, not edits, withdrawal or Admin Add. After first response, new questions are optional and existing optional questions cannot become required; format changes use confirmed delete/create, preserving unrelated answers/registrations. Keep at least the protected required first Playing field, optional extra/Alt fields and boolean Captain answer. Versions protect edits; duplicate-safe creation reports when requested Required was normalized to Optional | AU02/AU05–AU07 complete; RC02 and combined-page binding pending |
| Teams / Draft | Admin records captain selections in a compact board: actual members in team columns, available primary accounts/EHB in the shrinking pool; click to pick, Undo and confirmed-order scramble. Control renews while the page can renew it, not only on picks. With **2+ included teams**, use a balanced website draft and at least one actual Captain per drafted team; multiple Captains are allowed. With **0–1 included team**, manually assemble/finalize without picks; only this manual flow permits unplaced people with an explicit note. Before first Live, finalized roster Add/Remove republishes without signup-cap/team-size-cap enforcement or automatic rebalance. First Live locks membership permanently; role changes for current members remain allowed. Stay on Teams after finalizing | Existing commands largely support this; AU14/RC04 and UI binding pending. No captain-operated draft, Pause/Resume or finalized Reopen; cancel a private Running attempt only after individual undos leave zero active picks |
| Board / rankings | Keep custom/manual tiles and current objective mechanics. A calculated drop tile may have an optional **total tile** EHB override with baseline/reset; manual tiles still require an entered estimate. An override cannot bypass missing rate mechanics, alter catalogue/KC/Luck or change evidence-bound scoring; effective EHB still feeds existing proportional contribution statistics. Full-board finishers lead, ordered by earliest full-board finish; otherwise compare completed lines, completed tiles, credited EHB, then score time, preserving exact ties and historical results. Planning players-per-team remains manually adjustable **after draft finalization**, affects planning only and is not replaced by actual roster sizes | AU11–AU13 approved, not implemented; new-events-only activation approved. RC05/AU19 proposed; existing leases, approval/publication and late-evidence protections retained |
| Review / Final Review | One-click approval; rejection/reversal require reason and confirmation; pending metadata correction requires reason and is separate from approval. Never replace the submitter's image or invent a timestamp. Show the credited account's own relevant context; screenshot game time determines activity eligibility, upload time cutoff/order. Publication requires **closed uploads, zero Pending and valid placements**; no override of those gates. Publish immutable results and Archived state atomically; preserve ties/history. An optional normal WOM refresh may be skipped/fail without blocking publication. Reasoned confirmed reopening creates a new correction cycle, retains earlier versions and does not reopen uploads | Existing commands retained; RC07/RC08 and AU17/AU18 proposed. Full-event-pool correction approved; AU17 implementation pending; no separate Archive action or manual result/time override |
| WOM | Website dates own the schedule. External ID-only connections are read-only; protected valid/unverified credentials can permit sync without changing provenance. **External competitions are never deleted by the app**; website-created deletion retains eligible credentials, pre-first-Live and operation guards. Fetch now needs no typed challenge or confirmation, but all server cooldown/retry/scheduled-slot/lease rules still apply. Status/readback never fetches, old success is not proof of a new fetch, and unknown outcomes stay unresolved | AU15 approved; RC09/AU20 proposed. Credential-enabled edits already supported; replacement/disconnection after credentials approved under option 1; implementation pending; external provider deletion stays forbidden |
| Catalogue | Keep the activity drawer and progressive common/advanced settings. Shared-item adoption/metadata changes must be explicit; source-specific rate changes must not silently rewrite shared items. Ordinary Admins can do normal CRUD/deactivation but advanced mechanics are read-only; only SuperAdmin edits those fields. **Permanent deletion is also SuperAdmin-only**, requires strong confirmation and a fresh complete dependency check; referenced records deactivate instead. Preserve calculation validation, audit, versions and immutable approved/history snapshots | Existing CRUD retained; RC10/AU21 proposed; AU23 records approved advanced editing. No import/upload or separate roll-group editor |
| Accounts | Manage existing normal accounts with per-action permissions; **only SuperAdmin grants/revokes Admin or transfers ownership**. Role changes do not remove event membership; revoking a disabled Admin leaves it disabled. Password-reset links stay transient, bound to the correct account, expire after 60 minutes, are single-use and supersede unused links; never read a lost secret back. Ownership transfer requires current owner's password plus typed destination username, preserves exactly one owner atomically and invalidates affected sessions | Existing services retained; RC11/AU22 proposed; AU24 records approved typed confirmation. No create/delete/merge/impersonation or Admin editing of members' saved identity/accounts |
| Audit | Enabled Admins read sanitized immutable history, with filters and direct entry detail. Hidden-event audit remains visible under the existing exception without granting hidden-workspace access. Specific actions match exactly; recognized areas use prefixes. Whole-day date filters use calendar boundaries in the display timezone, including DST. Preserve truthful historical labels and sensitive-data redaction; no editing/deleting/exporting history | Existing query violates hidden-history contract; RC06/AU16 proposed. Keep reference filter conveniences; do not invent missing historical names |
| Luck / KC | Existing container defaults to 0–100 Luck percentile and switches to KC difference, using the same saved snapshot. KC difference is expected KC for approved eligible outcomes minus recorded KC, counted once per character/activity; totals do not compensate for boss speed. New approvals/reversals retain a compatible previous result and original freshness until a successful WOM fetch recalculates it; unavailable/unsupported data is not zero. Reads never fetch or rescore. Final refresh remains provider-limited and failure does not block publication; historical conversion uses retained inputs, never newer evidence | Implemented, independently reviewed and user accepted; no new Luck work in this reconciliation |
| Shared UI | Accepted Admin light/dark styling includes filled primaries and horizontal table scrolling; reuse shared tokens/components/behavior and add consistent new primitives only when needed. Protect dirty drafts, pending targets, stale conflicts, focus and route/history recovery. Reduced motion/no-animation closing must work. A brief spinner minimum is presentation-only; it neither delays backend work nor shows success before confirmation, and repeat submission stays blocked | References frozen at canvas 42 / artifact `1790965722-e7ad`; final search-X centering included. Production integration pending; no exact spinner duration newly approved |

Detailed review anchors: [Participants F01–F06](#approved-application-changes),
[Dashboard](../../../PRODUCT_REQUIREMENTS.md#community-dashboard--approved-backend-scope-2026-10-01),
[Events directory](../../../PRODUCT_REQUIREMENTS.md#events-directory-data--approved-au04-2026-10-02),
[lifecycle/signup/results/WOM decisions](../../../PRODUCT_REQUIREMENTS.md#approved-admin-simplification-target--2026-09-26),
[Identity/Schedule contracts](../../../FUNCTIONAL_CONTRACTS.md#43-adm-event-02--identity-and-public-description),
[Teams](../../../PRODUCT_REQUIREMENTS.md#171-version-one-draft-flow),
[Board/ranking tickets](../../../DELIVERY_PLAN.md#au11--tile-local-ehb-override-for-every-objective-type),
[Catalogue](../../../PRODUCT_REQUIREMENTS.md#9-boss-activity-item-and-drop-catalogue),
[ownership](../../../PRODUCT_REQUIREMENTS.md#54-super-admin),
[Audit](../../../FUNCTIONAL_CONTRACTS.md#93-adm-audit-01--immutable-audit-history),
[Luck](../../../PRODUCT_REQUIREMENTS.md#luck-percentile-and-kc-comparison--approved-implementation-2026-10-01)
and [shared UI](../../../UI_SYSTEM.md#approved-admin-reference-direction--2-october-2026).

### Decisions resolved — 2026-10-02

- **AU12:** new events only; existing events keep the prior rule, including events
  without official results. Official snapshots stay unchanged.
- **AU20 option 1 now approved:** after reading the comparison below, the user
  explicitly chose artifact parity: pre-first-Live disconnect and matching-date
  replacement before/during Live, with or without stored/rejected credentials.
  Preserve active/unresolved-operation guards, never delete the external competition
  and require a new code for a replacement. Implementation pending.
- **AU17:** Admin correction selects from the full event pool, not only current
  Playing assignments, to fix mistakes such as forgetting to change the account.
  Map retained/non-current player and character identities explicitly at readiness;
  preserve evidence history, reasons and separate approval. No saved-account or
  roster rewrite is implied.

AU12/AU17/AU20 decisions are approved, not implemented. Conflict overwrite/re-send and
scheduled-slot relaxation for WOM remain deferred, not implicitly approved.

### Exact WOM time matching — approved, not implemented

Both WOM boundaries must equal the website's corresponding UTC instant; timezone
formatting may differ, actual times may not. Remove the five-minute tolerance in
AU20 and update reference validation/copy in RC09, including directly affected
Schedule consumers. Do not confuse this with the separate schedule input's
five-minute increments. Keep timestamp precision explicit; no silent date import,
link deletion, historical rewrite or invented zero activity on mismatch.

### WOM source comparison — 2 October 2026, option 1 subsequently approved

Read-only source comparison; no runtime/provider calls or production changes.
This describes the current five-minute matching implementation; the later exact-
window decision above changes that target and is not implemented yet.

| Action on an externally created competition | Frozen reference | Current application |
| --- | --- | --- |
| Link by ID without a code | Yes, validate matching dates | Yes, validates ID and matching event window |
| Add/replace management code for supported updates | Yes; before/during Live | Already supported; stored protected, valid/unverified enables supported writes |
| Replace external link before/during Live | Yes, even with a code; replacement dates match; queued/unknown operation blocks its control | Works without a management record; any non-deleted management record blocks it, including one created by adding an external code |
| Disconnect before Live, leaving remote competition intact | Yes, even with a code | External Disconnect can remain visible, but service rejects it after a management record exists |
| Disconnect during Live | No; offers matching replacement instead | No |
| Delete external competition on WOM | Never | Never |

Concrete example: link #123 by ID, then save its code. The app creates a management
record even if validation rejects the code. ConfigureAsync rejects subsequent
replace/disconnect while that record is non-deleted. This is broader than refusing
an action while an operation is running. The reference's doLink resets the new
connection to code=null; credentials must not carry across to a new competition.

Sources: Wom.dc.html:608-610,648-695,881-886;
EventCompetitionSynchronizationService.cs:62-110;
EventCompetitionManagementService.cs:92-160;
WiseOldMan.cshtml:13-16,81-96; EventCompetitionManagement.cs:99-102.

Investigated options: (1) retain the reference capability and implement safe
local detach/replacement of external credential-bearing connections, excluding
active/unresolved provider operations and keeping existing lifecycle/date rules;
(2) retain the application's restriction and deliberately change the reference to
explain/disable those actions after credentials. Option 1 is the planner's
recommendation, subsequently explicitly approved by the user. Pre-Live-only replacement would be another deliberate
restriction versus both the current ID-only flow and the reference, not parity.

Known credential-display bugs remain RC09 (including unavailable-code capability
and website-origin alone implying write access); they are separate from this policy
question. No new general provider editor or conflict-overwrite flow is implied.

Creation scheduling check: neither the reference nor the application offers a
user-selected future creation time. Create competition submits work now; the
competition's future start/end come from Schedule. The reference simulates a queued
background operation. The application records an operation and immediately calls
ExecuteAsync, with background processing for pending/retry/reconciliation. Scheduled
fetches and future event dates do not schedule competition creation. Sources:
Wom.dc.html:698-707,805-810,899-905; EventCompetitionManagementService.cs:201-246,421-437.

### Differences from the approved artifacts

One intentional behaviour change from the frozen artifact is now approved:
WOM start/end must match exactly as UTC instants, replacing its five-minute
allowance (AU20/RC09, pending). This changes validation and explanatory text, not
layout or the available connection actions. WOM option 1 preserves those actions. The list below
is provenance for earlier user-requested changes already incorporated in the design,
not a proposal to remove additional final-artifact features.


- No artifact feature removal is authorized merely to match the old application.
  This reconciliation changes documents only; frozen design files are unchanged.
- Earlier user-requested removals/refinements: toast Undo deferred; non-boolean
  Captain answer removed; implicit saved-account overwrite rejected; redundant
  Teams Participants table and affiliation/image controls removed; actual-team
  EHB-per-player breakdown removed; empty draft places/future pick rows removed.
  These describe prior decisions, not new removals in this pass.
- Proposed RC fixes retain legitimate actions but correct invalid states, such as
  evidence-locked Board changes, completed-objective correction, ineligible WOM
  deletion and false success after unknown outcomes. Each remains a named finding,
  not a blanket feature reduction.
- Full-pool evidence correction and WOM option 1 are approved. The initial mistaken
  WOM approval was withdrawn; this approval follows the requested investigation.
  Preserve reference controls and implement AU20 only when the queue is resumed.
- Ordinary Admin advanced Catalogue fields remain read-only, with SuperAdmin
  editing; ownership transfer gains the required typed recipient confirmation.
  These are explicit user-approved permission/confirmation requirements.

Other visual choices (compact navigation, filter presets, row presentation,
advisory balance highlighting and previous/next navigation) belong to the accepted
reference/integration scope, not repeated product approval questions. The Board's
reference 25% advisory threshold is not a new approval blocker. Keep missing-image/
zero-contribution review affordances aligned with current commands unless the user
requests a behavior change. Current Accounts reset rechecks permission under lock;
do not claim or invent expected-version semantics it does not have.

### Disposition of all 47 findings

Every entry remains **open, not fixed or runtime-verified by this reconciliation**.
RC scopes preserve accepted visuals; AU scopes cover actual server/projection work.
Existing AU11–AU15 are reused, not duplicated. Report IDs are namespaced because
Audit and Accounts both use A1–A8. The report links retain detailed reproduction,
source anchors and evidence limits. Catalogue's later SuperAdmin decision overrides
the report's earlier all-read-only recommendation; ownership confirmation is settled.

| Report / finding | Required outcome | Delivery owner |
| --- | --- | --- |
| Board B1 | Retain full save intent and draft; truthful uncertain readback, including publish/discard | RC05; AU19 transport |
| Board B2 | Preserve dirty drafts/version across takeover; no silent overwrite | RC05; existing lease/version integration |
| Board B3 | Preserve late evidence locks on correction publication/discard | RC05; existing production guard |
| Board B4 | Unknown route gives unavailable state, no crash | RC05 |
| Board B5 | Back/Forward/direct tile route restores and closes correct layer | RC05 |
| Board B6 | One complete decimal normalization, reject junk | RC05; existing application bounds |
| Board B7 | Faithful weighted calculation/limits and short-event planning values | RC05; reuse existing calculator, AU11/AU13 remain separate |
| Audit A1 | Retain hidden-event audit under existing authorization/redaction | RC06 + AU16 existing-contract correction |
| Audit A2 | Exact specific action versus recognized area prefix | RC06 + AU16 |
| Audit A3 | Next local midnight for inclusive date upper bound, including DST | RC06 + AU16 |
| Audit A4 | Short-viewport filter actions reachable | RC06; shared panel consumer check |
| Review R1 | No uncertain request/actor/reason attribution from status alone | RC07 + AU17 |
| Review R2 | Backdrop never means Leave anyway | RC07 |
| Review R3 | Protect cancel/stale correction and reason drafts | RC07 |
| Review R4 | Reversal mock reflects real contribution reallocation | RC07; existing service |
| Review R5 | Unknown event never selects another event's queue | RC07 |
| Review R6 | Credited account correction refreshes its own context | RC07 + AU17 |
| Review R7 | Objective selectors distinguish multiple objectives on a tile | RC07; existing data |
| Review R8 | Completed manual objective rejects correction like other types | RC07; existing validation |
| Final Review F1 | Negative readback cannot claim no historical change | RC08 + AU18 |
| Final Review F2 | Capture operation identity; guard pending navigation/reason draft | RC08 |
| Final Review F3 | Failed direct-version load retry restores requested drawer | RC08 |
| Final Review F4 | Confirmation preserves complete tie metadata | RC08; AU12 rule remains separate |
| Final Review F5 | Distinguish stale archived state from actual reopening | RC08 |
| Final Review F6 | Modal locks background scrolling | RC08 |
| WOM W1 | Use actual credential capability and deletion eligibility | RC09 + AU20 projection |
| WOM W2 | Pending completion cannot target another event/dialog | RC09 |
| WOM W3 | Old cache success is not proof of this fetch; read failure stays unknown | RC09 + AU20 |
| WOM W4 | Protect unsent dialog drafts; submitted secrets still clear | RC09 |
| WOM W5 | Unknown update never appears Up to date | RC09 + AU20 |
| WOM W6 | Modal scroll lock | RC09 |
| Catalogue C1 | Creation readback cannot crash or infer identity by partial match | RC10 + AU21 |
| Catalogue C2 | Independent form drafts survive other saves/close decisions | RC10 |
| Catalogue C3 | Capture target/pending identity for nested operations | RC10 |
| Catalogue C4 | Immutable baselines, retained stale drafts and final delete recheck | RC10; preserve existing server concurrency |
| Catalogue C5 | Mapping/value changes invalidate old checked/rejected state | RC10; existing domain behavior |
| Catalogue C6 | Explicit retained or deliberately changed shared image; honest save scope | RC10 + AU21 |
| Catalogue C7 | Reactivation preserves/validates roll mechanics | RC10; ordinary Admin guard plus AU23 authority |
| Catalogue C8 | Failed direct-link retry restores editor and missing-target state | RC10 |
| Accounts A1 | Reset secret rendered/copied only for captured matching account | RC11 + AU22 transient delivery |
| Accounts A2 | Readback describes current role/state/owner without false attribution | RC11 + AU22 |
| Accounts A3 | Protect reason/recipient intent on cancel/stale; never persist secrets | RC11 |
| Accounts A4 | Restore direct drawer after retry and stable focus when row disappears | RC11 |
| Accounts A5 | Revoke preserves disabled status and says so | RC11; existing service |
| Accounts A6 | Transfer confirmation retains selected recipient/version | RC11; existing version guard; typed check AU24 |
| Accounts A7 | Normalized search and strict query/page handling | RC11 + AU22 binding |
| Accounts A8 | Header Transfer locks background scrolling | RC11 |

Reports: [Board/Audit](reviews/2026-10-02/remaining-seven/board-audit-review.md),
[Review/Final Review](reviews/2026-10-02/remaining-seven/review-finalreview-review.md),
[WOM/Catalogue](reviews/2026-10-02/remaining-seven/wom-catalogue-review.md),
[Accounts](reviews/2026-10-02/remaining-seven/accounts-review.md).
Their small route-integer, actual-action-label, range-validation and calendar-date
notes travel with the same page correction, not extra tickets. Mock provider/font
failures and deliberately simulated URLs are not missing production features.

## Ordered application tickets after Luck

Execution scope/checks live in DELIVERY_PLAN.md, Admin functionality queue.
AU01–AU10 are technically complete and committed; AU11 onward is stopped by the
user. AU16–AU24 and RC05–RC11 record the agreed outcomes/finding disposition for review,
not dispatch. Technical mapping still belongs to each future assignment.
The checkpoint preserves prior failures, remediation and callback limitations.
Luck, Participants and Dashboard backend work is complete, not queued to rebuild.

| ID | Page / application outcome | Current status |
| --- | --- | --- |
| AU01 | Overview: prevent restore creating two visible current events | Technically complete — focused PostgreSQL/HTTP checks and independent Astra review PASS; committed at the checkpoint; UI/manual acceptance deferred |
| AU02 | Signup setup: enforce 100-character code limit server-side | Technically complete — focused authenticated PostgreSQL checks 5/5 and Release build PASS; independent Astra/high review PASS, no findings; committed at the checkpoint; new UI/manual acceptance deferred |
| AU03 | Events: duplicate-safe create and uncertain-outcome lookup | Technically complete — focused PostgreSQL/request 13 cases + reset 1/1, Release build and independent Astra/high review PASS; committed at the checkpoint; modal/UI/manual acceptance deferred |
| AU04 | Events: directory ordering, retained counts and attention data | Technically complete — 8 distinct PostgreSQL cases, Release build and independent Astra/high review/recheck PASS; attention filter P2 resolved; committed at the checkpoint; UI/manual acceptance deferred |
| AU05 | Signup setup: stale-edit protection and settings version results | Technically complete; 25 distinct focused PostgreSQL/HTTP cases PASS across corrected runs, final Release build and independent Astra/high review PASS (cfb67191); committed at the checkpoint; UI binding/manual acceptance deferred |
| AU06 | Signup setup: duplicate-safe question/account-field adds | Technically complete; 12 distinct focused PostgreSQL/HTTP cases PASS across corrected runs, final Release build and independent Astra/high review PASS (bcf9392b); committed at the checkpoint; frontend recovery/manual acceptance deferred |
| AU07 | Signup setup: explicit required-to-optional normalization outcome | Technically complete; 22 distinct focused PostgreSQL/HTTP cases PASS across corrected runs, final Release build and independent Astra/high review PASS (ca110d17); committed at the checkpoint; completion callback rejected; planner reconciled directly; frontend uncertainty binding/manual acceptance deferred |
| AU08 | Identity: safe field-level conflict handling | Technically complete; 14 distinct PostgreSQL/HTTP cases PASS across corrected runs, controlled timezone fixture, final Release build and independent Astra/high review PASS (912763bd); committed at the checkpoint; conflict-choice frontend integration/manual acceptance deferred; AU09 complete |
| AU09 | Identity: uncertain-save readback without claiming request success | Technically complete; 7 distinct PG/HTTP cases PASS across corrected fixture runs, readback/timezone/multiline transport PASS, Release build 0 warnings/errors; independent Astra/high review PASS after one P2 correction (f5547bcd); committed at the checkpoint; full ordinary-save/new-reference UI binding/manual acceptance deferred |
| AU10 | Schedule: unchanged-instant preservation, field-addressable lifecycle errors and full-state uncertain readback | Technically complete; 12/12 PG/HTTP plus named Live 1/1, controlled transport and Release build PASS; independent Astra/high review PASS (24f0870c); committed at the checkpoint; explicit stop after AU10, full UI/manual acceptance deferred |
| AU11 | Board: tile-local manual EHB override for all objective types | Agreed; queued, not implemented |
| AU12 | Rankings: credited EHB before score time; preserve first full-board finish and history | Agreed; queued, new events only; existing events keep prior rule |
| AU13 | Board: manually adjustable planning team size after draft finalization | Agreed; queued, not implemented |
| AU14 | Teams: safe authoritative readback after uncertain actions | Queued; minimal transport contract to resolve at handoff, existing commands reused |
| AU15 | WOM: remove typed FETCH confirmation; preserve refresh protections | Approved 2 October; queued after the existing AU01–AU14 / RC01–RC04 sequence, not dispatched |

Retain completed tickets and their implementation, executed-proof, independent-review
and deferred UI/manual-acceptance states. Add later reviewed application gaps under
new AU IDs; do not silently turn new design ideas into approved product behaviour.
Participants and Dashboard backend work is complete, not queued for reimplementation.

### Reference correction queue — Codex; canvas sync by Claude

Ownership changed by the user on 1 October: Codex handles bounded behaviour,
validation, recovery and factual wording fixes; Claude focuses on visual design
and syncing verified corrections back to the canvas. The following RC tickets
are authorized and queued, not started. They are separate from AU backend work.
The active DELIVERY_PLAN section Reference corrections owns their scope/checks.

Preserve the approved layout, typography, colour, spacing and animation feel.
Track repository implementation, executed checks, independent review and canvas
sync separately. After review, the corrected repository files are authoritative
for the bounded canvas sync; Claude must not overwrite them with older artboards.
Keep the final changed-file/hash handoff and verify parity after synchronization.
Earlier prompts asking Claude to implement these same fixes are superseded.

- **RC01 — Overview:** correct the permanent-link destination explanation; evidence-code
  failure/stale/uncertain simulations; hidden-event failed-load Restore visibility;
  manual versus scheduled signup-opening eligibility; cancelled Stats README fact.
  Evidence: `docs/references/admin-ui/reviews/2026-10-02/earlier-pages/overview-status-extract.md`.
- **RC02 — Signup setup:** compare complete intended question values and do not resolve an
  uncertain add by label alone; retain each uncertain settings request's baseline
  across other-card saves; protect dirty inline account renames; say waiting-list
  order rather than signup order. Evidence and exact source lines:
  `docs/references/admin-ui/reviews/2026-10-02/earlier-pages/signup-setup/review.md` (20 captured files
  stable before/after; no runtime/browser/tests or edits by reviewer).
- **RC03 — Schedule/shared picker:** preserve unchanged exact UTC instants and
  compare full submitted schedule on uncertain readback; retain legacy overdue
  enabled-opening exception; use shared DST-safe conversion for Overview picker
  consumers; keep picker footer reachable in short viewports. Add read-failure
  recovery to readback mock and fix related README claims. Evidence:
  `docs/references/admin-ui/reviews/2026-10-02/earlier-pages/schedule/review.md`.
- **RC04 — Teams / Draft:** immutable pick identity for Undo/readback; no false
  attribution or safe-retry claims for picks/redraws; match team identity and all
  intended saved fields including inclusion; report actual WOM synchronization
  status separately from locally republished rosters. Queued, not fixed. Evidence:
  `docs/references/admin-ui/reviews/2026-10-02/earlier-pages/teams-identity/teams-review.md`.
  The reported forced 250ms feedback minimum is a pending presentation choice;
  preserve it for now given the user's preference for brief visible feedback.
- **Identity:** named correction SOURCE PASS, 2 October. Twelve files stable;
  reviewer did not execute browser/runtime checks. AU08/AU09 are technically complete; UI binding remains
  pending. Evidence: `docs/references/admin-ui/reviews/2026-10-02/earlier-pages/teams-identity/identity-review.md`.

No concrete shared-token/component regression was found in the Overview or Signup
setup source reviews. Their tests remain Claude-reported, not independent execution.

### Deferred application UI work

Page routes/composition/binding, dirty-state navigation, modal/drawer interactions,
loading/error/recovery, responsive tables, accessibility and manual acceptance stay
with each later UI integration pass. Existing services, answer/impact counts and
state guards should be reused. The approved brief minimum saving/spinner duration
uses the prototype feel as baseline, possibly slightly shortened during integration;
no exact duration is agreed and no backend delay is required. This is separate
from the already corrected CSS-derived exit-animation timing.

## Participants — agreed scope

## Scope and authority

- Visual reference: [Participants.dc.html](Participants.dc.html); canonical canvas
  and export guidance: [README.md](README.md).
- This is the behavior and delivery register for the reference pages, not a replacement for the
  application's functional contracts, data model or delivery plan. Promote each
  accepted behavior into its existing authority before implementing it.
- Preserve the approved design while correcting functionality. No redesign,
  runtime migration or backend implementation is authorized by this document alone.
- The review used the `admin-simplification` checkout at
  `/Users/christopher/.codex/worktrees/735f/BingoWebpage`, commit
  `993c90e9835d1f2d74fc9c03ed4d8f6306a7e37c`.
  The reference lives in a different checkout. Reconfirm the implementation branch
  and current source before starting; do not assume the reference checkout contains
  the reviewed application code.
- These decisions supersede earlier prototype briefs where they conflict,
  particularly the unanswered captain state, payment-free Add flow, fixed first
  primary account, and changes to globally saved accounts.

## Backend implementation authorization — 30 September 2026

The user authorized the backend changes now, independently of Claude's UI
extraction. New UI, drawer routing/binding and manual testing are deferred until UI
integration; focused automated backend checks and independent review are required.

Implementation checkout:
`/Users/christopher/.codex/worktrees/participants-functionality/BingoWebpage`, branch
`codex/participants-functionality`, based on `admin-simplification` at `993c90e`.
Its DELIVERY_PLAN Participants backend pass and reconciled product/functional/data
contracts own execution. Planner: current UI task; orchestrator and independent
reviewer: Sol 6.1/high; implementer: Luna 5.6/max. Current implementation and proof status is recorded below.

Admin management permits event Draft before signups open as well as SignupOpen
and SignupClosed before the team draft is locked. Do not confuse event Draft with
team-draft start. Existing finalized pre-Live roster Add/Remove stays separate;
first actual Live permanently locks registration/membership. Private payment and
notes retain their existing broader lifecycle window.

The latest reference handoff reports its correction checks passed; these are
Claude-reported prototype results, not independent backend verification.

## Approved application changes

### F01 — Explicit confirmation, including when capacity is full

Allow an admin to confirm a selected waiting participant directly.

- With available capacity, confirm without increasing capacity.
- When full, offer an explicit **Confirm and add a place** action. Increase
  capacity by one and confirm that specific participant in the same operation.
- Do not also promote another waiting participant because capacity was increased.
  Preserve the relative order of everyone remaining on the waiting list.
- Keep applicable pre-draft/lifecycle restrictions. This is not permission to
  bypass locked rosters or rewrite competitive history.

Example: a full 60-place event has Alice first and Bob fifth in the queue.
Confirming Bob with the override produces 61 places and confirms Bob; Alice
remains first in the remaining queue.

### F02 — Move a confirmed participant to the waiting list

Keep the option visible but disabled while confirmed places are available.
Explain its unavailability without making it look editable.

When full, move the selected participant to the end of the waiting list and
promote the next eligible existing waiter into the freed place. Do not immediately
promote the participant just moved. Preserve other participants' queue order.

If there is no eligible existing waiter, the action is unavailable and the backend
rejects it without mutation. Moving a team member to Waiting ends current team
membership/leadership authority while retaining its history; the confirmation
must disclose that removal.

### F03 — Restore with an explicit confirmation override

Preserve normal capacity-driven restoration: an available place means Confirmed;
a full event means Waiting, using existing restored signup/queue sequencing.

Also support an explicit **Restore as confirmed and add a place** action when
full. Increase capacity by one and restore the selected participant as Confirmed
atomically, without unintentionally promoting someone else. A Waiting choice
remains visible but disabled while places are available.

### F04 — Admin Add participant without signup questions

The creation flow requires:

- An existing website account.
- At least one of that website account's saved playing accounts, with additional
  selections allowed within the event's configured account slots.
- A primary playing account. The first selection may be the default, but the
  admin can choose another selected account as primary.
- A Paid/Unpaid choice, defaulting to Unpaid, saved with participant creation.

Use stored account EHB. Do not request WOM data during this flow. Do not ask signup
questions, captain/co-captain questions or notes as part of creation. An
admin-created participant defaults to **No** for captain volunteering.

Normal Admin Add places the new participant in Confirmed when capacity is available
and in Waiting when full. When full, also offer an explicit **Add as confirmed and
add a place** option: increase capacity by exactly one and add that new participant
directly to Confirmed in the same operation. Do not promote any other waiter as a
side effect; preserve the remaining queue order. Do not allow voluntary Waiting
while places remain. Applicable pre-team-draft/lifecycle restrictions still apply;
this is distinct from finalized pre-first-Live roster Add on Teams.

The current application's required-question validation and WOM-dependent creation
path need deliberate reconciliation. Bypassing questions does not authorize
inventing answers to unrelated questions. Creation and its payment/placement
changes must succeed or fail together.

### F05 — Switch the primary playing account

Allow an admin to choose which of the participant's playing accounts is primary.
Exactly one account is primary; that account alone supplies draft EHB.

Switching preserves the accounts and their individual EHB values. Do not sum EHB,
duplicate accounts or lose an account during the switch. Reconcile the current
question-bound primary-account mapping explicitly, and retain applicable lifecycle
locks on edits.

### F06 — Edit event accounts without rewriting saved accounts

Changing an account in an event changes that event's participant assignment only.
It must not implicitly rename, overwrite, add or delete the website account's
saved account records, or overwrite their saved EHB.

Example: the member has saved accounts A and B and entered the event with A.
Switching the event entry to B leaves the saved list as A and B. It must not rename
A to B and leave two saved records with B's name.

Keep the ability to change the account used for the event. This decision does not
restrict all existing-participant editing to saved-account selection only; the
saved-only requirement applies to the Add flow. An unknown name typed during an
event edit must not automatically become a globally saved account.

## Existing rules to preserve and reference corrections

### Withdrawal and automatic promotion

Keep Withdraw and its confirmation. Remove the prototype option to suppress
promotion. Before draft lock, withdrawing a confirmed participant promotes the
next eligible waiter under the existing rules. Withdrawing a waiting participant
does not free a confirmed place. Preserve existing lifecycle restrictions.

### Captain volunteering is Yes/No

Remove Not recorded, Not asked and Not answered as selectable third states.
The supported answer is boolean. Admin-created participants default to No.
Captain volunteering and co-captain requests do not themselves assign team roles.

### Account rows are limited by event configuration

Allow additional playing accounts to be added/removed within the event's
configured account slots. The prototype must not imply unlimited accounts.
Use the real account/question-slot mapping; do not introduce an arbitrary global
limit. Preserve at least one playing account and exactly one primary.

### Saved account links are not exclusive ownership

Shared or borrowed character links can exist across website accounts. Do not add
a global rule that a character can belong to only one website account. Preserve
existing checks against duplicate accounts within a participant and conflicting
assignments within the same event. Add still selects records linked to the chosen
website account.

### EHB precision is not a mandatory visual correction

A reference display such as 12.3 rather than 12.34 is acceptable. Settle display
formatting during integration. Preserve supported input and stored precision;
do not destructively round underlying values to match the mockup.

### Prototype data and tools are not new product features

Do not implement Fail next request, Reset prototype data or placeholder navigation
as application features. Synthetic Discord/source metadata must map to real
supported data; it does not authorize new integrations or signup source types.

## Required integration behavior

These are integration requirements, not claims that production implementation is
complete. Reuse existing supported behavior where it already satisfies them.

- Stable participant IDs drive drawer URLs. Direct links and reloads show the
  Participants workspace and selected drawer together. Opening does not create
  duplicate history entries; direct-entry Close returns to the workspace.
- Close, Cancel, Escape, genuine backdrop clicks and browser navigation respect
  dirty drafts. Discard follows the pending navigation once; Keep editing retains
  the draft without history loops. Forward must not resurrect discarded edits.
- Confirmations are transient, not separate shareable routes. Closing a top
  confirmation does not also close the drawer. Restore focus appropriately and
  retain visible, consistent keyboard focus styling.
- Preserve filters, search, sort, pagination and table scroll around drawer use;
  clamp pagination after changes where necessary.
- Successful save updates the workspace and closes the drawer. Failure preserves
  the draft and presents actionable errors. Guard unload only for dirty drafts,
  acknowledging native browser limitations.
- Add success must not leave a history entry that resubmits creation. Final Add
  route/query details still need to be settled during integration.
- Handle missing records, lost permissions, stale data and changed lifecycle state
  using authoritative application responses, not prototype assumptions.
- Before the wider Admin overhaul is complete, account for old routes deliberately:
  retain, redirect or retire them; do not leave orphaned editors.

## Deferred and unresolved

| Item | Decision / boundary |
| --- | --- |
| Toast Undo | Explicitly deferred. Remove its prototype affordance/callback; do not add backend reversal now. |
| General manual waiting-list reordering | Not approved. Directly confirming a person is not approval for queue editing. Leave this separate reference item pending discussion and identify where it remains. |
| Technical account/question-slot mapping | Resolved in the completed backend pass: existing protected PrimaryRegularAccount question and answer remain the authority; UI binding must use that mapping. |
| Exact Add route shape | Resolve as part of the routing integration; no final query format is specified here. |

## Implementation and evidence tracking

F01–F06 are **technically complete, 30 September 2026** in the assigned backend
worktree. Contracts/data rules are reconciled; the same independent Sol 6.1/high
reviewer accepted the stable source and required executed proof with no remaining
findings or proof gaps. New UI integration and manual application acceptance remain
deferred. No commit, merge, push or deployment has occurred.

| ID | Scope | Contract reconciliation | Application / evidence |
| --- | --- | --- | --- |
| F01 | Confirm selected participant; explicit capacity override | Reconciled | Complete; focused checks and controlled draft-start conflict/recovery proof accepted |
| F02 | Move to Waiting with correct promotion | Reconciled | Complete; promotion/history/notification checks and final source review passed |
| F03 | Restore with explicit capacity override | Reconciled | Complete; same-context rollback, reservation conflict and retry proof accepted |
| F04 | Question-free Add, stored EHB, payment and primary choice | Reconciled | Complete; saved-link negatives, stale/conflict cases and required-answer omission accepted |
| F05 | Primary-account switching | Reconciled | Complete; secondary-primary/EHB projections, stale/no-change and invalid-slot same-context proof accepted |
| F06 | Event-only account edits | Reconciled | Complete; retained history/notifications and same-character EHB/provenance PostgreSQL proof accepted |
| Integration | Routing, dirty state, failures and authoritative validation | Reconcile with existing routes | Pending; new Participants reference not integrated and manual application testing deferred |

### Participants completed evidence — 30 September 2026

Planner reconciled the orchestrator's completion report with CURRENT_STATUS.md and
DELIVERY_PLAN.md in the assigned backend checkout. This records the completed
independent review; the planner did not rerun checks or independently review source.

- Release Web build: 0 warnings/errors, web-build-after-test-fixes.log.
- Focused PostgreSQL run: 7/7, remediation-focused-final.log/.trx.
- Controlled real draft-start conflict/recovery: 1/1,
  draft-interleaving-controlled-proof.log/.trx/.meta.
- Final secondary-primary/stale/invalid-current-slot same-context proof: 1/1,
  primary-invalid-current-slot-proof-final.log/.trx/.meta. Supersedes the earlier
  primary-projection-proof-final run for that affected case.
- Same-character EHB release-and-append history, deterministic microsecond-precise
  WOM provenance and repeat behavior: 1/1,
  ehb-correction-proof-parsed.log/.trx/.meta.
- Durable evidence folder: `reviews/2026-10-03/doc-ticket-cleanup/h1/participants/`.
  Runs overlap; do not sum them as unique coverage or claim a full-suite pass.
- Earlier scoped formatter command/exit 0 was verified from
  format-scoped-final-exact.meta. The later changed-test formatter exit 0 remains
  worker-reported; its log is quiet. Final diff check passed.
- All four original source findings and all three remaining proof gaps are closed.
  Reviewer verified stable source, logs/TRX and command/assembly identity rather
  than independently rerunning tests.
- The earlier silent build stall was generated-output write denial (MSB3371) in a
  checkout outside default writable roots. Narrow authorized escalation recovered
  it; no app restart, user-database changes, cache wipe or broad process kill.
- Implementation remains in branch codex/participants-functionality, based on
  993c90e9835d1f2d74fc9c03ed4d8f6306a7e37c; all dirty work is preserved.
- DELIVERY_PLAN.md, “Completion and UI-binding handoff”, records exact application
  methods, request/result contracts, version checks, primary mapping and refresh
  responsibilities. Next work is a separately assigned redesigned UI integration
  pass, followed by its applicable checks and manual acceptance.

Before each functional slice, update the relevant existing owners:
`FUNCTIONAL_CONTRACTS.md` / `PRODUCT_REQUIREMENTS.md` for behavior,
`DATA_MODEL.md` for affected invariants, and `DELIVERY_PLAN.md` for execution scope.
Record actual completion/evidence here with date and commit when available;
page-specific visual acceptance remains owned by `UI_PAGE_MATRIX.md`.

Use focused executable checks for changed behavior. Capacity, placement,
restoration, creation and account assignment must preserve authorization, audit,
transactions, concurrency, persistence, snapshots and lifecycle restrictions.
Exercise relevant PostgreSQL boundaries for persistence/concurrency changes.
Prototype interactions or source inspection alone do not prove these guarantees.
No broad test framework or unrelated feature work is requested.


## Dashboard — extracted application requirements

Reference: [Dashboard.dc.html](Dashboard.dc.html), shared components/behavior and
[README metric definitions](README.md#dashboard-agreed-metric-definitions-reference).
Purpose: a community statistics/history dashboard useful between infrequent
events, with a compact current/upcoming-event entry point. It replaces the planned
Admin-home composition; it is not a live-event operations console.

This pass extracts requirements only. No Dashboard production code, service,
endpoint, schema, job, provider request or database query was implemented/executed.
The existing Admin Index PageModel contains operational projections, not the new
community statistics contract. Promote the accepted definitions into existing
functional/product/data authorities and plan the implementation before dispatch.

### Dashboard technical completion — 1 October 2026

D01–D09 are implemented, executed at affected boundaries and independently
source-reviewed PASS. D10 remains deferred UI integration/manual acceptance.
Authoritative completion/evidence is in the implementation checkout's
CURRENT_STATUS.md and `reviews/2026-10-03/doc-ticket-cleanup/h1/dashboard/dashboard-review-final.meta`.
The final focused PostgreSQL run was 8/9 with an initialization-only failure,
followed by that isolated proof passing 1/1; affected original login proof 1/1
and ordering 2/2 are separate runs, not a single clean 9/9 claim. Existing build,
scoped formatter and diff-check proof passed. No packaging is claimed.

### Historical backend planning checkpoint — 30 September 2026

The following checkpoint records pre-implementation planning and is superseded
for current delivery state by the technical-completion entry above.

Draft plan: `/Users/christopher/.codex/worktrees/participants-functionality/BingoWebpage/DELIVERY_PLAN.md`,
“Dashboard backend pass — approved, 2026-10-01”. B1 covers historical population,
people sets and aggregates; B2 adds official recap/EHB/history, current-event card
and community figures. Account-population/card/chronology decisions are approved; Live inclusion was
revised and approved 1 October. Bounded readiness precedes implementation. No implementation, executable
Dashboard proof, independent implementation review or UI integration is claimed.
The implementation checkout's CURRENT_STATUS and MANUAL_TEST_CHECKLIST carry
ownership and deferred journeys. D01–D09 must later retain completed evidence;
D10 remains deferred UI integration, not a backend-completion claim.

### Delivery items

| ID | Application requirement | Reuse / implementation boundary | Current status |
| --- | --- | --- | --- |
| D01 | One authorized historical-event population for every statistic, chart, history row and recap | Reuse Admin authorization and hidden/quarantine exclusion; Live and ended events; ended-only recap; use actual lifecycle timestamps and deterministic event ordering | Implemented; focused proof executed; independent review PASS (1 October) |
| D02 | Four headline figures: events held, unique website participants, total event participations and approved submissions | Add aggregate projection over retained event, membership, identity and submission records; preserve unknown vs zero | Implemented; focused proof executed; independent review PASS (1 October) |
| D03 | Latest-event contribution text in applicable headline cells | New tracked identities in latest event, its participants and its approved submissions; additions, not growth percentages or good/bad performance | Implemented; focused proof executed; independent review PASS (1 October) |
| D04 | Participation chart, first-tracked/returning breakdown and keyboard/touch-accessible details | Same per-event people sets as D02; imported records remain unlinked; chronological event axis; tooltip does not create new analytics data | Implemented; focused proof executed; independent review PASS (1 October) |
| D05 | Latest ended-event recap and current official winner / winner board completion | Use existing official finalization/placement snapshots and retained board information; never infer winner from a provisional live ranking | Implemented; focused proof executed; independent review PASS (1 October) |
| D06 | Optional supplementary EHB gain and account coverage | Reuse stored WOM activity projection / frozen imported snapshots; no provider call triggered by loading Dashboard; distinguish missing, partial and zero | Implemented; focused proof executed; independent review PASS (1 October) |
| D07 | Sortable six-column event history with real destinations | Event/date, players, approved submissions, winner board, EHB, winner; deterministic null-aware sorting and navigation state | Implemented; focused proof executed; independent review PASS (1 October) |
| D08 | Compact current/upcoming event card | Reuse event phase, next milestone and confirmed/capacity projections; choose a deterministic event when several qualify; link to its real workspace | Implemented; focused proof executed; independent review PASS (1 October) |
| D09 | Community account snapshots | Website accounts only, creation timestamps and last-login timestamps already exist; aggregate read required | Implemented; focused proof executed; independent review PASS (1 October) |
| D10 | Application integration and state handling | Route, shared shell, loading/error/retry, empty and limited history, responsive layout, localization, accessibility, motion and Back/reload behavior | Prototype checks passed in named scope; application integration pending |

### D01–D04: population and counting contract

- Exclude hidden/quarantined events from every Admin projection, including
  aggregate totals, returning-history sets, tooltip data and navigation choices.
  Cancelled and discarded events do not contribute to held-event history.
- Headline totals, latest-event additions, chart and history include Live,
  AwaitingFinalReview, Finalized and Archived. Live and final-review figures use
  the label Provisional only. The latest-event recap remains ended-only; the
  current-event card can retain its actual Live phase. This supersedes the
  30 September ended-only decision, approved by the user 1 October 2026.
  Community account snapshots are not restricted to event participants.
- Ended events awaiting final review contribute participation and current approved
  submissions. Mark them provisional; review corrections may change those figures.
  Finalized and archived events contribute official results.
- Count people with qualifying team membership during an event once per event,
  even with multiple playing accounts, membership rows or team moves. Do not use
  current Confirmed signup totals as a substitute for historical participation.
  Exclude membership that ended before actual participation began. Map retained
  participant identity/membership and import roster records explicitly.
- Imported history contributes event and participation totals; never invent
  website identities or match people using mutable character names.
- Unique participants are distinct participating website account IDs across the
  tracked population. Returning means appeared in an earlier tracked event, not
  necessarily the immediately previous event. First-time means first observed in
  tracked history, not a claim about participation before tracking began.
- The first tracked platform event has no returning split. Imported bars remain
  unsplit. Later bars split first-time/returning, and their sum equals each event's
  eligible linked population. Unlinked platform records, if present, need explicit
  coverage handling rather than invented identity (see decisions below).
- For comparable complete linked history, unique people equals the sum of
  first-time people, and total participations equals the sum of event bar totals.
  Hiding/excluding an event must apply consistently when recomputing these sets.
- Approved submissions use the real submission eligibility/source/status rules.
  Exclude reconstructed imported contribution rows; count records, not drop
  quantity, screenshots or reconstructed contribution units. Reversal/correction
  must update the aggregate consistently with the event's authoritative read.
- A latest-event addition describes contribution to the total: +new unique people,
  +participants, +approved submissions. Use actual values, including legitimate
  zero. Omit redundant additions when the latest event supplies the whole metric.
  Keep definitions/coverage/provisional explanations accessible through hints.
  These are not percentage comparisons or cumulative performance trend lines.

### D05–D08: recap, history, EHB and navigation

- Select the latest ended event from authoritative dates, not a sample array.
  Display name, date range, player/team totals, official winner and completed/total
  winner tiles where available; approved count and optional EHB are secondary.
- In final review, show provisional results instead of a winner or winner's board.
  The last official winner may be shown only with its own event name. Reopening
  final review must not present a retained old snapshot as a newly official result.
- Use the applicable current official snapshot, preserve historic team names and
  results, and reuse existing tie/placement authority. Missing official data is
  unknown, not an invitation to calculate an unofficial replacement.
- EHB gained is a period gain, not signup/draft EHB. Reuse retained WOM data and
  frozen imported history, with account coverage (playing accounts, not people).
  Missing or incomplete provider data must be explained, not silently defaulted
  to zero. Dashboard reads must not initiate external synchronization.
- History begins newest first; the reference Event header sorts by event date.
  Support the six shown sorts, stable tie ordering and missing values last in
  either direction. Board comparison sorts by completed/total ratio, not raw
  completed tile count; protect missing/zero denominators.
- Clicking history event names, chart bars, recap Open event or the current-event
  card must resolve the actual event's Admin Overview using its stable identity.
  Go to Events reaches the Events directory. No preview-only placeholder toast.
- Sorting and returning from an event should preserve the Dashboard context;
  choose the precise route/query contract during integration. Direct entry,
  reload and Back/Forward must work without duplicate navigation entries.
- Current/upcoming card shows phase, relevant next date, confirmed/capacity and a
  workspace link. Hide it when none qualifies. Use truthful handling for an unset
  capacity/date and label a Live event as current, not misleadingly as next.
- Sidebar selection is navigation context, not a filter that reduces the global
  dashboard totals to that single event.

### D09–D10: community, states and interaction integration

- Website account total excludes imported/synthetic identity records. Count new
  website accounts since the latest ended event's actual end. When no event has
  ended, the prototype falls back to new accounts in the last 30 days.
- Logged in during the last 30 days uses stored LastLoginAt and one server clock;
  it is not gameplay activity or a reconstructed login trend. Null login dates do
  not qualify. Keep the total/percentage denominator population consistent and
  guard zero accounts. Use supported timezone/date presentation and EN/DA labels.
- No events: one explanatory state with Go to Events, community figures and a
  current/upcoming card if applicable. Imported only: no one-bar comparison,
  expanded recap, unknown unique/submission metrics and their explanation.
  One tracked platform event: tracking-start semantics, no returning claim.
  Multiple events: chart/history/recap. Unknown is distinct from measured zero.
- Loading skeletons, failure message and Try again must connect to real reads.
  A failed load must not show zero totals or fabricate successful data. Retry is
  idempotent/read-only; discard outdated responses when a newer load wins. Reuse
  existing auth/error handling for lost permission or a now-hidden event.
- Preserve light/dark themes, standard shared focus, sidebar/menu behavior,
  keyboard chart access, Escape tooltip dismissal, touch-accessible explanations,
  sort announcements and appropriate focus recovery on errors/retry.
- Reuse shared CSS/motion and reduced-motion behavior. Sorting fades and chart
  tooltip movement are presentation, not delays or extra server operations.
  EHB can drop first at narrow widths; the table scrolls inside its surface and
  mobile navigation works. Do not ship canvas support.js/vendor runtime.
- Do not ship sample-history switches, Fail next request, future sample clocks,
  fabricated account totals, design-only toasts or placeholder navigation.

### Definition decisions and remaining implementation questions

These are bounded data/edge decisions, not reasons to redesign the reference.

1. **Population — resolved, 1 October 2026:** Live contributes to statistics,
   chart and history with Provisional labelling, as does final review. Recap is
   latest ended only. Reference copy/population synchronization belongs to Claude;
   backend implements the revised PRODUCT_REQUIREMENTS/FUNCTIONAL_CONTRACTS.
2. **Identity coverage:** unlinked people count per event without invented global
   identity. Readiness maps actual sources and reports coverage limitations.
3. **Chronology/card — approved:** actual end selects the ended recap/new-account
   boundary; actual start defines earlier returning cohorts. Card priority is
   Live, next scheduled preparation, then unscheduled setup. Stable display ties
   cannot create false returning identity or competitive winner tiebreaks.
4. **Account population — approved:** disabled website accounts remain in registered
   totals and historical distinct people; emergency credentials are excluded.
   Stored EHB and official snapshot mapping remain technical readiness work.

### Focused proof for future application work

Use existing fixtures and test boundaries; this is a risk-based scope, not a new
test framework or an exhaustive UI matrix.

- Mixed imported/platform history plus multiple accounts and team moves: exact
  player, unique/returning and submission totals agree across all projections.
- Hidden/cancelled/discarded and pre-event departed memberships excluded; archived
  and final-review records included as agreed; provisional/official reopen handled.
- Reconstructed import contributions excluded while real approved zero remains
  zero. Rejected/pending/reversed records do not inflate approved totals.
- Retained official snapshot and board denominator, missing/partial/zero EHB,
  website-vs-import account population, null logins and exact time boundaries.
- No-event/import-only/first-tracked states; multiple eligible upcoming events;
  null-last stable sorting and real event links. PostgreSQL query proof where
  grouping, membership interval or timestamp precision affects correctness.
- During UI integration: real route/Back/reload, auth failure, controlled load
  failure and retry, keyboard/touch details and narrow layout. Manual visual
  acceptance is page-specific and separate from backend correctness.

### Dashboard reference inspection — 30 September 2026

Planner inspected the local exported page at
http://127.0.0.1:5196/Dashboard.dc.html. This was a scoped design/behavior check,
not an independent source review or a production data audit.

- Viewed full history in dark mode and imported-only layout in light mode.
- Keyboard focus exposed the chart's full participant breakdown; Escape dismissed
  it. Sorting Players produced descending 96, 90, 84, 72.
- Observed full history (143 unique, 342 participations, 1,802 approved), imported
  only (90 participations, unavailable unique/submissions), no-event/community,
  and imported + one platform event after retry (72 unique, 162 participations,
  486 approved). Values matched the reference's stated fixture definitions.
- Exercised loading, simulated failure, focus on Try again and successful retry.
  These are prototype results, not proof of application recovery or persistence.
- Claude separately reported both themes at 390–1440px, Participants/Components
  regression checks and reduced motion. Those wider checks were not repeated
  independently in this pass.
- No design blocker found in the inspected states. The user considers the page
  done and is sending one additional correction; final acceptance of that
  correction is not inferred here. The observed export's SHA-256 was
  18ef82f42dc3a277b55f58c18d56927c197f1edddb259e3ca548be6226d525f6.
- No reference HTML/CSS/JS or README was modified by this documentation pass.
  Shared component extraction is delivered reference work; real Dashboard
  functionality, route binding and backend tests remain pending.

## Events — agreed design brief, 30 September 2026

Purpose: find an event, understand its situation, and open its workspace or create
a new private draft. These are design decisions, not a claim that the new reference
or application integration is finished. Claude retains composition freedom within
the Participants/Dashboard shared visual system.

### E01 — One directory table with filtered views

- All (default), Current/upcoming, and Past filter one table; do not render
  separate tables for these groups.
- Current/upcoming covers Setup, Signups open, Signups closed and Live. Past covers
  Final review, Finished, Archived and Cancelled, each honestly labelled.
- Default order: Live first, then upcoming/setup events, then past newest first.
  Exact stable date/tie handling can follow existing authority during integration.
- Search by event name, phase filtering and sorting. Selected filters, search and
  sorting are reflected in URL state and survive opening an event and Back.
- Tables remain horizontally scrollable whenever needed, including mobile. Do not
  replace this with cards or silently remove required columns to fit the viewport.
- Hidden/quarantined events use a separate SuperAdmin-only view of the same table
  component. They are excluded from ordinary views and their counts.

### E02 — Row information, attention and destinations

- Show name, phase, event dates, participants and a compact Needs attention value.
  An unset schedule is labelled Not scheduled.
- Show confirmed/capacity during preparation, with waiting count where relevant;
  use a meaningful participant count for past events rather than suggesting open
  signup. Reuse actual retained data; do not fabricate missing import capacity.
- The event name opens that event's Overview. No redundant Workspace button or
  action menu without an identified directory action. Slug need not occupy a row.
- Needs attention shows one priority summary: actual failure first (for example
  Start failed), otherwise pending review work (for example 12 to review),
  otherwise a quiet dash. Several issues may show a compact +N suffix. Define
  the unit consistently with the existing action projection; a review queue
  summary must not accidentally count each submission again as an extra category.
- Ordinary unfinished setup is not a failure. Do not stack failure, pending review
  and setup-blocker badges in each row. The event Overview explains the full set.
- Cancellation, archive and discard remain in the event workspace with appropriate
  confirmation. No Events drawer is currently required.
- Separate No events yet from No matching events. Offer Create for the former and
  Clear filters for the latter, retaining ordinary Create access.

AU04 backend implementation checkpoint, 2 October: directory query exposes the
approved population/phase/name/`attention=1` filters and stable sorts; preparation uses scheduled
start then signup dates, unscheduled last; past uses actual end/lifecycle fallback
or cancellation time newest across phases. Retained participant reads reuse the
Dashboard mapper, keep missing intervals unavailable and capacity nullable, and
include import provenance. Attention prioritizes current start/opening failure,
then review, with +N counting categories once per review queue; inbox units remain
unchanged. Eight focused PostgreSQL cases pass after a fixture-only correction.
Independent review found the missing backend attention filter; its named correction
passes its focused PostgreSQL case 1/1; same-reviewer recheck subsequently passed as recorded under AU04. Layout/filter
URL/navigation binding and manual acceptance remain deferred. Original evidence:
`reviews/2026-10-02/au04/implementation/handoff.md`; corrected evidence:
`reviews/2026-10-02/au04/implementation/remediation-handoff.md`.

### E03 — Create event modal and journey

- A small modal collects required name and editable timezone, initially
  Europe/Copenhagen. Other configuration follows in the event workspace.
- Create saves a private draft and opens its Overview. Preserve existing
  authorization, unique slug generation, audit and atomic creation semantics.
- Validation/save failure keeps the modal open and preserves entered values.
- Cancel, Escape and backdrop dismiss an untouched form; changed values require
  the shared discard confirmation. Discard closes; Keep editing retains values.
- While saving, prevent duplicate submission and dismissal until the outcome is
  known. Failure/timeout must release the pending state with a safe recovery path;
  uncertain outcomes must not lead to duplicate event creation.
- A direct Create URL renders the Events directory with the modal. Closing direct
  entry returns to the directory; opening from a filtered directory and cancelling
  preserves that context. Browser navigation follows the same dirty-state rules.
- After successful creation, Back/reload must not resubmit the create operation.
- No description, dates, capacity, artwork or signup questionnaire in this initial
  modal. No automatic public exposure or opening signups.

### E04 — Design and delivery boundaries

Reuse shared shell, tokens, components, keyboard focus, hover, motion, reduced
motion and overlay behaviors in both themes. New reusable components/variants may
be introduced where justified, with the same visual language and preview coverage.
Include loading/failure/retry, empty/filter-empty, permission and modal validation/
dirty/saving states. Route behavior must be described even where the canvas uses
simulated navigation. Prototype tooling is not application functionality.

| ID | Decisions | Reference | Application / verification |
| --- | --- | --- | --- |
| E01 | Agreed | Completed per user | AU04 grouped views, ordering, phase/name/attention filters and authoritative query data technically PASS; URL/control/navigation binding and manual acceptance deferred |
| E02 | Setup-blocker removal source-confirmed applied | Completed per user | AU04 retained participation, nullable capacity/import provenance and category-priority attention technically PASS; participant rendering integration/manual acceptance deferred |
| E03 | Create 50-code-point limit and durable actor/request-key retry contract applied | Completed per user | AU03 atomic creation/replay/authorized outcome lookup technically PASS with PostgreSQL/request/reset proof and independent review; modal/navigation binding and manual acceptance pending |
| E04 | Agreed | Shared reference delivered | Application integration, executed proof and manual application acceptance pending |

AU03/AU04 backend implementation and independent technical review/recheck PASS are
recorded above. New UI integration and manual application acceptance remain deferred.
The original read-only reference/application comparison is retained below as dated evidence.
Promote changed application behavior to its existing authorities before backend
implementation; this brief does not authorize packaging or a broader lifecycle
redesign.


### Events functional completion comparison — 1 October 2026

Source comparison by `/root/dashboard_backend/events_comparison`, Sol 6.1/high,
against `codex/participants-functionality`. No source edits, executable tests,
browser inspection or database access occurred. This is source evidence, not
executed application proof. Existing Participants work is preserved.

1. **Durable Create recovery — backend gap.** Create.cshtml.cs:76 generates a fresh
   event/slug on another valid POST. The reference README:358 calls for a durable
   request key and outcome lookup (Check again). PRG does not resolve a lost
   response. Preserve at-most-once creation under concurrent retries; exact
   persistence/authorization/receipt design belongs to an approved Events plan.
2. **Directory ordering — bounded projection work.** Index.cshtml.cs:217 groups
   past events by phase and puts missing preparation dates first. The approved
   reference requires past newest across phases and unscheduled preparation last.
3. **Counts and provenance — projection work.** Index.cshtml.cs:81 uses current
   Confirmed for all phases and converts null capacity to zero. Live/past players
   must use retained participation, with nullable capacity and import coverage.
   Reuse Dashboard's mapping where applicable; no new persistence assumed.
4. **Attention priority/filter — projection work.** Index.cshtml.cs:187 and
   SharedShellService.cs:414 use pending-submission count plus scheduled-failure
   count. Directory needs failure priority and +N issue categories; keep the
   shared inbox's existing units intact. WOM failure is a synthetic reference
   example, not an existing mapped directory action source. User accepted this
   illustrative mock on 1 October; no fixture replacement or new WOM attention
   capability is required.
5. **Setup blockers — unapproved reference discrepancy.** README:281 and
   Events.dc.html:513 count Teams not drafted as an attention category, unlike
   approved failure → review → quiet dash. User approved removing ordinary
   setup blockers from attention/filter counts on 1 October; retain actual
   failed-transition context. Reference correction remains pending; no new setup
   attention capability is authorized.
6. **Name limit — reference mismatch.** Events.dc.html:679 allows 200 characters;
   Create.cshtml.cs:59 effectively enforces 50 Unicode characters, consistent with
   WOM title rules. The annotation's 200 is not effective acceptance. User approved aligning reference Create and Identity
   to the existing effective 50-character limit on 1 October. Reference correction
   remains pending; raising the production limit is not approved.
7. **Query/navigation/mobile — integration work.** Existing site.js:532 only
   filters already-rendered rows, so widening a filtered/Hidden view needs an
   authoritative query/reload. Existing mobile CSS turns rows into cards; the new
   table must remain horizontally scrollable. Bind direct Create modal routing,
   dirty/saving/recovery, duplicate-name notice, distinct empty states and
   Back/share context. Sidebar intentionally starts default All. Any pagination
   proposal needs a demonstrated requirement; this comparison does not authorize
   adding it automatically.

Concrete source paths are under
`src/Bingo.Web/Pages/Admin/Events/{Create,Index}.cshtml.cs`,
`src/Bingo.Web/Navigation/SharedShellService.cs` and
`src/Bingo.Web/wwwroot/{js/site.js,css/site.transitional.application.css}`.
Reference paths are in this folder. Line numbers identify the reviewed revision.

Reference role switching, request maps, injected outcomes and simulated browser
history are intentional fixtures; never ship these as production behavior.
Keep reference completion, backend implementation, executed proof, independent
implementation review and manual application acceptance separate.


## Identity — completion and correction handoff

The user accepts the narrow left-aligned form composition. Read-only Sol 6.1/high
comparison by `/root/dashboard_backend/identity_review` found substantially sound
reuse of shared tokens/components/behavior and shared Components specimens.
No local theme colors or duplicate animation timings were found; simulated request
delays and page-specific widths are not extraction defects. This source comparison
did not execute browser checks or application tests.

### Named reference corrections — source recheck passed, 2 October 2026

Claude's latest user-supplied handoff reports canvas/repo synchronization and
passing focused corrections plus Identity/Participants/Events/Components dialog
and focus regressions. These are Claude-reported results, not independently
executed proof. No production implementation, commit or manual application
acceptance is inferred.

- Permanent link displays/copies the absolute existing /Events/{slug}/Signups
  destination. The earlier bare-route proposal is withdrawn. Use configured
  public origin in the application, never the reference's bingo.example origin;
  existing visibility and onward Teams/Board routing remain authoritative.
- Leaving an uncertain save offers Check again / Leave anyway and explains that
  saving may already have happened. It never claims changes were not saved.
- Readback confirms values present with Up to date wording, not proof that this
  request saved them. No new Identity request receipts are required for that wording.
- Schedule-only timezone conflicts retain visible stale feedback/focus and require
  fresh review of current dates, separately from field-level conflict notes.
- Active dialogs lock page scrolling. Shared focus helper handles Shift+Tab from
  the container and holds focus there while every control is disabled.
- Description/buy-in validation matches existing UTF-16 limits; event-name
  validation retains the approved 50-Unicode-code-point rule.
- Common Events/Identity heading-focus CSS moved to the shared owner.
- README no longer incorrectly claims missing unchanged-unsupported-timezone
  handling, stale-version refresh, browser departure guards or editing restrictions.

Independent Sol 6.1/high recheck confirmed all named corrections and their direct
shared consumers: SOURCE PASS, 2 October. No broad review or redesign was performed;
12 captured files stayed stable. Browser checks remain Claude-reported. Application
AU08/AU09 and UI binding remain separate queued work.

### Existing behavior versus remaining integration

Existing application already has permanent slug ownership, Live/Final Review
text editing, first-Live timezone lock, unchanged stored unsupported timezone
handling, stale version refresh and browser departure warnings. Preserve these.
New binding includes stay-on-Identity success/no-change, field-level latest-value
comparison / Use theirs, readback for Check again, shared form/readonly/copy/save
presentation and Unicode-aware name input. Existing stale retries refresh the
version but do not provide the reference's safe untouched-field merge; do not
misdescribe this as an endless stale retry. Legacy prose needs reconciliation to
approved simplification without reopening the approved state rules.

Events setup-attention removal and Create's 50-code-point limit were independently
source-confirmed in this comparison. Those two corrections are applied, while
application integration remains separate. WOM failure fixture remains accepted.

## Event Overview — agreed brief basis

Purpose: understand the event's current situation, progression and required next
action, with applicable manual lifecycle controls. Full progression can remain
visible; only relevant phase controls appear. These are behavioral requirements,
not a prescribed stepper/card/panel layout.

Preparation requirements are neutral and tied to the next transition (for example,
Before you can open signups). Completed items may show checks. Do not label normal
unfinished preparation as errors or repeat blocker counts. Actual failures and
pending review work need distinct, concise attention treatment.

Scheduled transitions and relevant manual actions are presented together. Preserve
manual open/close/reopen signups, start/end and permitted recovery operations with
existing authorization/readiness/state rules and consequence-specific confirmation.
Identity/Schedule/Participants/Draft/Board/WOM own their detailed editing;
Final Review owns official publication and reopening of results. Overview summarizes
and links. Keep relevant public destinations/copy actions, phase-appropriate counts,
milestones and visibility. Destructive controls stay secondary; legitimate recovery
does not automatically get destructive styling.

Claude has composition/copy freedom within the shared system. Give available facts
and phase examples without mandating exact cards, wording or an exhaustive visible
list. Keep it short and informative; confirmation consequences must still be clear.
New visual ideas using supported data are welcome; new data/behavior is proposed
separately. Include applicable evidence-code/upload-recovery/hidden-event capabilities
from the active contract rather than silently dropping existing permitted operations.
No Overview production changes or acceptance are claimed.


## Schedule — handoff recorded, 1 October 2026

Claude reports Schedule.dc.html, Overview picker consumers, shared picker,
Components and README synchronized with canvas version 31; no production edits or
Git publication reported. All validation, route, theme, keyboard, reduced-motion
and regression checks are Claude-reported, not independently verified. Independent source comparison is complete: **CHANGES REQUIRED**, R1–R4.
All 15 captured files remained stable; no runtime/tests/browser checks were run.
User supplies visual acceptance. Evidence: `docs/references/admin-ui/reviews/2026-10-02/earlier-pages/schedule/review.md`.

Track implementation and remaining items separately:
- AU10 technically complete (2 October): unchanged exact/repeated-hour UTC preservation,
  lifecycle field errors and authorized immutable full-state readback. Focused PG/HTTP
  12/12, affected Live 1/1, controlled transport, Release build and scoped checks PASS;
  fresh independent Astra/high review PASS, no findings. Evidence
  `reviews/2026-10-02/au10/review/review.md`. Full UI binding remains deferred.
- RC03: four named reference fixes and qualified README/read-failure recovery; queued.
  Shared reuse otherwise confirmed; no whole-site extraction or redesign needed.
- UI integration: shared picker with unchanged event-local posting semantics and
  server DST/five-minute authority; remain on Schedule after successful save;
  navigation, confirmation, stale/error/uncertain states and modal focus binding.
- Authority reconciliation completed in AU10: FUNCTIONAL_CONTRACTS section 4.4
  reflects approved start/end editing during drafting and overdue pre-Live repair.
- Shared 600ms minimum saving feedback is Claude's proposed reference setting;
  the user approved a brief minimum, not this exact number. Keep actual network
  completion/failure truthful and separate prototype delays from application work.
- Fixed overlap fixture is acceptable illustrative data, not real validation proof.
- Review shared token/component reuse and Overview picker consequences without
  reopening unrelated RC01/RC02 findings or redesigning approved composition.

AU10 implementation and independent technical PASS are verified; reference RC03,
full application UI integration and manual acceptance remain pending. User explicitly
requires stopping after AU10 reporting; no next-ticket work without instruction.

## Teams / Draft — agreed design scope, 1 October 2026

One workspace adapts through Setup, Running and Finalized. An admin operates the
snake draft while streaming it; captains call out picks. No captain-controlled
collaborative draft feature is authorized. Existing admin controller/takeover
protection remains. Clicking an available player immediately picks them; retain
latest-pick Undo, no extra selection step or per-pick confirmation.

The active draft automatically compacts the sidebar and prioritizes all teams/rosters and all available players' primary
account and EHB at once. Typical events: 3–5 teams and about 50 or fewer players,
1080p/1440p screens; larger summer events exist. Fit typical drafts without scroll
while preserving stream readability; no separate fullscreen mode is requested. Use deliberate overflow for larger cases rather
than unreadable shrinking. Keep current turn, counts and Undo easy to see.

Remove the standalone Participants-style table during setup and after finalization.
Keep the actual available-player pool during drafting. Setup roster assignments
use a searchable participant selector showing primary account, EHB and Captain
volunteer status. Payment/signup timestamps and event withdrawal stay on Participants.
Remove team affiliation/image controls; retain stored historical values without
new deletion or database cleanup. Keep team name and draft inclusion settings.

Condense readiness/distribution text; keep team settings out of the active draft,
control ownership secondary, and Cancel apart from routine picking. Preserve
manual teams, derived balanced snake turns/preassignments, required usable Captains,
zero/one included-team direct roster publication, and pre-first-Live finalized
Add/Remove corrections preserving original picks/publication history. No Pause/
Resume, finalized Reopen, CSV roster imports or accountless participants. Finalize
publishes rosters, not the board or event start. Team totals are existing data and
may be shown compactly. Designer retains composition freedom within shared system.


Teams / Draft delivery checkpoint: Claude delivered the reference and the user now
reports the follow-up UI changes complete. Requested refinements remove empty
future-pick rows so actual team rosters grow as the available pool shrinks, add
visible draw/scramble feedback, and slightly shorten draft-action loading feedback.
Independent completion comparison is now complete: the reference requires three
functional corrections tracked in RC04. In-place Pick/Undo/Scramble, control renewal,
search inputs and readiness/distribution mostly already exist; new transport for
uncertain-action reconciliation is tracked as AU14. Bind existing command eligibility
and synchronization results. The fourth review item, a forced 250ms feedback minimum,
remains a presentation choice; no automatic removal is authorized. All 19 captured
sources remained stable. No browser/runtime checks were executed by this reviewer.

Teams / Draft integration classification: retain existing domain workflows; AU14
records the now-source-confirmed uncertain-action transport gap. The
layout, automatic compact sidebar, immediate row picking through the existing
Pick action, assignment selector and removal of redundant controls are new UI
integration work. The later completion review may identify concrete binding/data
gaps. Existing capabilities must not be reimplemented or retained data deleted.

## Board — agreed designer brief delivered, 1 October 2026

The full Board brief has been delivered to the user after Teams / Draft. Designer
completion has not been reported. No Board implementation or reference review is claimed. Board remains a grid workspace;
simplifying the complicated tile editor without losing functionality is a primary
objective. Choose drop/manual type once per tile, preserve homogeneous multiple
objectives, optional advanced counting/weights, automatic descriptions and manual
text overrides. Keep optional custom tile image upload/replacement/removal.
Condense control-ownership/readiness copy, retain preview and approval/publication
boundaries, preserve evidence locks and current public snapshot during correction.
Remove the actual-team EHB-per-player breakdown. Keep expected players per team
manually adjustable even after draft finalization, without additional explanatory
UI text; planning only, no roster/scoring effect.

Approved functional direction (not implemented): allow a tile-local manual total
EHB override on every tile type; use calculated EHB when absent, require manual
estimate for non-drop challenges. Effective EHB affects ranking/statistics/board
balancing. Keep calculated baseline/reset for calculated tiles without modifying
catalogue values. Separate points model deferred. Move credited EHB ahead of
current-score time after equal lines/tiles, preserve first full-board finisher wins
and immutable historical results. Existing proportional partial credit retained.
Evidence-protected scoring remains locked; overrides are board-design inputs, not
permission to rescore submitted history. Application tickets AU11–AU13 now own
these agreed changes in the active DELIVERY_PLAN, with focused acceptance/proof
and historical compatibility requirements. All are queued: no implementation,
executed proof, independent PASS or new UI acceptance is claimed.

## Wise Old Man — manual fetch decision, 2 October 2026

The user approved a normal Fetch now button without typing FETCH or a confirmation
modal. This supersedes the prepared designer brief's instruction to retain the
current confirmation requirement. Preserve existing server-side cooldown, normal
schedule/retry timing and in-flight protections; a failed attempt does not bypass
them. AU15 in DELIVERY_PLAN owns the handler/markup correction and focused proof.
Reference design and production implementation are pending; no tests, independent
review or manual acceptance claimed. No other WOM functionality change approved.

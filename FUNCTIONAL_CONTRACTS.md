# Functional contracts

## 1. Authority, scope, and conflict routing

This document is the active authority for final end-to-end journeys, actors, reachability, authority handoffs, failure/recovery behavior, and acceptance outcomes. It records the current version-one functional foundation and does not by itself imply release readiness.

The authority boundary is:

| Concern | Authority | Contract boundary |
| --- | --- | --- |
| Product behavior and version-one scope | `PRODUCT_REQUIREMENTS.md` | Roles, product rules, exclusions, and acceptance scope. |
| End-to-end journeys and handoffs | `FUNCTIONAL_CONTRACTS.md` | Who acts, where the journey starts/ends, what is authoritative, and how recovery works. |
| Entities, invariants, calculations, snapshots, and history | `DATA_MODEL.md` | Persistence shape, uniqueness, derived values, progress, ranking, and finalization semantics. |
| Architecture, security, storage, realtime, deployment, and operations | `TECHNICAL_ARCHITECTURE.md` | Technical boundaries and operational controls. |
| Global UI rules, page families, composition, and approval | `UI_SYSTEM.md`, `UI_PAGE_MATRIX.md` | Shared interaction, responsive, accessibility, protected composition, page-family references, exceptions, approval state, and UI gates. |
| Current checkout, work, limitations, and unresolved decisions | `CURRENT_STATUS.md` | Current state only; it does not redefine product behavior. |
| Remaining order, gates, and stop rules | `DELIVERY_PLAN.md` | Delivery sequence and release gates only. |

When documents disagree, the authority above decides the concern in its own column. A conflict that crosses boundaries is recorded and resolved in the owning source before implementation. No archived document, root tombstone, route shape, notification, or realtime message can silently override an active authority.

This contract deliberately omits detailed schema, formulas, infrastructure design, implementation history, planning method, status legends, and test inventories. Refer to the owning source for those details.

## 2. Cross-cutting journey contract

### 2.1 Authentication and role authority

- A website account is the durable identity for normal website actions.
- Discord authentication and public-username/password authentication resolve to the same website account; Discord server membership is not required.
- Initial account creation starts through Discord and completes public username, password, and first OSRS-character onboarding before normal signup.
- Event participation is explicit `EventParticipant.AccountId` ownership. A Discord display name, website username, OSRS name, volunteer answer, or emergency credential never infers ownership.
- Captain and co-captain authority is an event/team role on a current website-account-linked membership. It is not attached to a character name or authentication method.
- An emergency captain credential is individual, event/team-scoped, disabled by default, and never a global Admin or Super Admin identity.
- Super Admin is a global role with exactly one active owner. Global roles and event roles remain separate.
- Disabled accounts cannot authenticate; session invalidation and authorization version changes take effect without waiting for an ordinary cookie expiry.
- Every mutation rechecks the current authenticated account, event scope, role, lifecycle phase, and expected version at the authoritative service boundary.

### 2.2 Server authorization and validation

- Server authorization is authoritative for every page, form, fallback route, background action, and realtime-triggered refresh.
- Validation is repeated at the mutation boundary; client-side validation, rendered controls, links, notifications, and route visibility are not permission grants.
- A failed authorization or validation attempt leaves the last valid state active and returns actionable, non-secret feedback.
- Domain/application services own lifecycle, eligibility, privacy, and cross-record rules. Razor, JavaScript, and database triggers do not replace those rules.
- Admin corrections identify the actor and preserve the affected record's history. Exceptional changes that alter competitive or historical truth also require strong confirmation and a written reason.

### 2.3 Audit, history, transactions, concurrency, and idempotency

- Historical competitive records, event transitions, memberships, character assignments, submissions, evidence, board approvals, official results, and access changes are append-only or versioned as defined by `DATA_MODEL.md`.
- Routine configuration mutations record actor, time, and structured before/ after values automatically; a typed reason is not required unless this contract says it is.
- Compound mutations commit authorization, validation, reservations, derived state, audit, and required in-site notifications atomically.
- Optimistic versions and appropriate event/team/participant locks reject stale writes. A rejected stale write never overwrites a newer authoritative value.
- Repeated identical requests are safe where the owning journey declares them idempotent. Retry-sensitive durable notification producers and scheduled attempts use the owning transition or occurrence as their deduplication boundary; ordinary notifications may use fresh IDs within their accepted transaction or action.
- Reconciliation or retry re-reads the authoritative state before applying a new mutation. It never assumes that a previous page or notification is still current.
- A transaction failure returns a retryable failure and leaves no misleading success, partial reservation, partial role change, or orphaned lifecycle transition.

### 2.4 Privacy and evidence integrity

- Public projections contain only the public event, roster, board, approved evidence, standings, and participant-facing signup data allowed by the product contract.
- Website usernames, Discord identity/security data, payment, Admin notes, audit data, private evidence states, and unnecessary account identifiers are excluded from public projections.
- Ordinary participants see their own private signup/evidence scope. Captains see their current team's authorized scope. Admins see event administration scope. Admin and Super Admin capabilities compose additively with a genuine event Participant/Captain/Co-captain role but never create or upgrade that role. Super Admin adds global administration, not automatic cross-team private focus or evidence authority; its existing explicit read-only focus inspection remains separate.
- Approved evidence is authoritative for progress. An approved screenshot and its credited participant/account, submission time, board snapshot weight, and review history are not silently rewritten.
- Reversal removes the contribution through the authoritative recalculation and preserves the original approval. A Rejected or Reversed attempt may have exactly one direct linked Pending correction with a new image and server time while the normal/reopened upload window is open; neither predecessor is mutated or directly re-approved.
- Account/character sharing is trust-based and never proves real-world ownership. Event assignment uniqueness remains authoritative.

### 2.5 Notifications, realtime, and authority

- A notification is a recipient-specific destination and reminder, not the business record. The destination must resolve for the intended role.
- Opening or marking a personal notification read never resolves the underlying Admin action; that action disappears only when its authoritative condition is fixed.
- Notifications identify the event, actor-relevant subject, and direct route without disclosing private reasons or another team's evidence.
- Required notifications are written atomically with their mutation. Retry-sensitive producers use deterministic IDs at the owning transition/recipient boundary where implemented; ordinary notifications rely on their surrounding accepted transaction or action rather than a universal recipient-transition constraint.
- SignalR and other realtime messages are invalidations or non-authoritative updates. The receiving page reloads authoritative state and handles stale versions.
- A realtime invalidation never interrupts an active submission, confirmation, error, or result state. Ordinary route reload/navigation remains available.

### 2.6 Reachability and progressive fallback

- Every protected journey has a real authenticated route and ordinary server form/navigation path. JavaScript enhances the route; it does not define the business operation.
- Public board, team, tile, and approved-evidence destinations remain addressable through their real routes. Below 901px, ordinary route navigation is expected instead of overlay transitions.
- Desktop overlays/drawers may preserve context, but board/team/tile real URLs remain the recovery path for refresh, narrow screens, keyboard use, history, and failed enhancement. The Captain Submit route is only drawer transport plus a compatibility redirect for old direct links, not a rendered or no-JavaScript submission destination. No separate no-JavaScript parity journey is required.
- Manual acceptance begins from the rendered navigation and forms expected by the journey. A destination URL constructed without following its intended link does not prove reachability.
- A route that is visible but fails authorization, filtering, handler validation, or destination rendering is not a reachable journey.

### 2.7 Failure feedback and recovery

- Every mutation reports success or failure on the authoritative page or destination, with enough context to correct the problem and without leaking private data.
- A conflict names the affected field/record or blocker, preserves recoverable entered values where safe, and leaves the last committed state unchanged.
- Temporary external-service failure never rewrites event state. The user gets accurate unavailable/retry or manual-entry feedback.
- A stale confirmation is rejected and regenerated from fresh data; it is never applied to an altered schedule, role, board, or lifecycle state.
- Reversal, resubmission, restoration, reopen, resume, unfinalize, and account recovery are explicit actions with the confirmation, reason, and history requirements declared by their capability below.

### 2.8 Development reset and manual-acceptance reachability

- Development reset creates only bounded labelled fixtures and the minimum accounts, roles, lifecycle states, teams, boards, signups, submissions, and cached integration data needed for documented journeys.
- Development navigation uses explicit event IDs/slugs and reset output. It never chooses an arbitrary “current” event when multiple fixtures exist.
- Production lifecycle services do not honor the Development fixture exemption; the exemption is unreachable through normal deployment input.
- The reset is idempotent and does not call Wise Old Man. Manual acceptance can use the seeded lookup event, Live competition cache, linked participant, Captain/co-captain, emergency credential, and Admin targets without real participant data.
- If a journey requires a seeded role or state, the reset output and the rendered navigation are the entry authority; a missing fixture is a setup limitation, not permission to invent a production shortcut.

## 3. Capability index

Each durable capability/journey below has one owning contract section. Shared cross-cutting rules in section 2 apply to all entries but do not create a second capability section.

| Capability or journey | Owning section |
| --- | --- |
| `WF-01` Create event through first participant access | 4.1 |
| `ADM-EVENT-01` Private event draft | 4.2 |
| `ADM-EVENT-02` Identity and public description | 4.3 |
| `ADM-EVENT-03` Schedule and timezone | 4.4 |
| `ADM-SIGNUP-01` Signup fields/readiness | 4.5 |
| `ADM-SIGNUP-02` Capacity/waiting list/code/withdrawal | 4.5 |
| `ADM-SIGNUP-03` Signup readiness check | 4.5 |
| `ADM-SIGNUP-04` Open signups | 4.5 |
| `ADM-RULES-01` Rules and `PUB-HOWTO-01` public how-to | 4.6 |
| Managed-image rule | 4.7 |
| `PART-IDENTITY-01`, `PART-PROFILE-01`, `ADM-IDENTITY-01`, `ADM-IDENTITY-02` Identity and recovery | 5.1 |
| `PART-ACCOUNT-01` My accounts and active swaps | 5.2 |
| `PART-SIGNUP-01`, `PART-SIGNUP-02` Submit, confirm, edit, cancel, rejoin, restore | 5.3 |
| `PUB-SIGNUP-01` Exact-link public signup table | 5.4 |
| `ADM-PARTICIPANT-01` Pre-draft participant administration | 5.5 |
| `ADM-PARTICIPANT-02`, `ADM-CAPTAIN-01` Post-draft roster exceptions | 5.6 |
| `ADM-DRAFT-01` Team setup, snake draft, finalize/reopen | 6.1 |
| `ADM-BOARD-01` Board build, approve, snapshot, publish/correct | 6.2 |
| Draft/board catalogue coupling and approval snapshot | 6.3 |
| `SYS-EVENT-START-01` Scheduled start readiness | 7.1 |
| `SUBMISSION-WORKSPACE-01` Canonical Captain/participant submission workspace | 7.2 |
| `PART-LIVE-01` Live participant workflow | 7.3 |
| `ADM-EVENT-END-01` End, grace period, resume | 7.4 |
| `ADM-REVIEW-01` Evidence review/correction/reversal | 7.5 |
| `ADM-FINALIZE-01` Final review/results/unfinalize | 7.6 |
| `ADM-EVENT-ARCHIVE-01`, `ADM-EVENT-CANCEL-01`, `SYS-CURRENT-EVENT-01` Lifecycle/history | 8.1 |
| `SUPERADMIN-EVENT-QUARANTINE-01` Hidden-event quarantine | 8.1a |
| `ADM-ACCOUNT-01` Disable/restore | 8.2 |
| `PART-HISTORY-01` Archived participant history | 8.2 |
| `PUB-FEEDBACK-01` External feedback | 8.3 |
| `SUPERADMIN-01` Global ownership/role administration | 9.1 |
| `ADM-CATALOGUE-01`, `ADM-CATALOGUE-IMPORT-01` Catalogue administration and import boundary | 9.2 |
| `ADM-AUDIT-01` Audit history | 9.3 |
| `ADM-ACCOUNT-OVERVIEW-01` Account overview | 9.4 |
| `ADM-INBOX-01` Personal notifications and Admin actions | 9.5 |
| Wise Old Man account lookup and cached competition activity | 9.6 |

## 4. Event setup and public entry

### 4.1 `WF-01` — Create event through first participant access

**Actors and outcome:** An enabled Admin creates and configures a private event; the system reports what is needed to open signup; a participant authenticates, submits or resumes one event signup, and receives an authoritative confirmation or the documented fallback/error outcome.

**Entry and reachability:** Start at Admin Events → Create. Continue through the rendered Identity, Schedule, Questions, participant, and exact-link signup destinations. The event remains private until signup opens. A returning account uses its authenticated event destination; a first-time account completes Discord onboarding first.

**Authoritative happy path:** Save a minimum private draft, configure identity, schedule, signup fields and capacity, pass readiness, open signup manually or on schedule, authenticate the participant, reserve valid event characters, and commit the signup. The first participant reaches confirmation with status, accounts/EHB, answers, and editing/withdrawal availability.

**Authority, history, and visibility:** Admin services own setup and opening; the authenticated website account owns the signup; server-side event-character reservations and capacity decide admission. Draft details, private Admin data, and unapproved competitive data remain hidden.

**Failure and recovery:** Invalid setup stays in the relevant form; an opening failure leaves signup closed; a character race fails atomically and identifies the conflicting account; a temporary service failure offers retry/manual entry. The user resumes from the authoritative destination rather than a stale link.

**Acceptance outcome:** An enabled Admin can take a new event from private draft to a reachable first participant confirmation without creating partial records, publishing incomplete data, or bypassing identity, capacity, or privacy rules.

### 4.2 `ADM-EVENT-01` — Private event draft

**Actors and outcome:** Any enabled Admin saves a private workspace using event name and timezone; optional description, schedule, signup configuration, questions, capacity, planning values, and artwork may be continued later.

**Entry and reachability:** Admin Events → Create saves the draft and returns to the event setup workspace. Another enabled Admin can continue the same event.

**Authoritative happy path:** The server creates a unique event ID and slug, creator/time metadata, Draft state, and safe defaults in one transaction. No participant, team, board, captain, or evidence record is implied.

**Permissions and history:** Every enabled Admin may create/configure every event. Duplicate display names are allowed; the slug is unique and editable until first public exposure. Creation and later setup changes are audited.

**Failure and recovery:** Invalid name/timezone or lost authorization creates no usable event. A slug collision gets a different valid slug. An unprotected experimental event can be confirmed-discarded; once protected participant, team, event-access, submission, or evidence data exists, discard is blocked and the cancellation contract applies.

**Acceptance outcome:** A minimal private draft is independently saveable, resumable, auditable, undiscoverable publicly, and unable to accept signup or publish competitive information by itself.

### 4.3 `ADM-EVENT-02` — Identity and public description

**Actors and outcome:** An enabled Admin manages event name, optional banner, public description, supported timezone, and pre-exposure slug; signup receives the required public introduction before it opens.

**Entry and reachability:** Use the event Identity route from Admin Manage or the setup progression. The same server contract supports normal form fallback.

**Authoritative happy path:** Before Live, select a supported timezone (Copenhagen is the default), save the public description, manage a stored banner, and retain the stable slug after first public exposure. Display-name changes preserve the URL. Identity and timezone become read-only when the event enters Live.

**Permissions and history:** Identity changes are Admin-authorized and audited. Banner absence is optional and never a readiness blocker. Stored UTC instants do not change merely because the display timezone changes.

**Failure and recovery:** Unsupported timezone, slug conflict, stale confirmation, or banner failure leaves the prior valid value active and returns retryable feedback. A post-signup timezone change previews participant-facing local times. Live identity and timezone corrections are intentionally unavailable; explicit schedule edits remain a separate capability.

**Acceptance outcome:** Admins can identify an event and make it understandable before signup without requiring decorative artwork, changing historical instants, or exposing a private draft.

### 4.4 `ADM-EVENT-03` — Schedule and timezone

**Actors and outcome:** An enabled Admin configures signup opening/closing, event start/end, optional draft time, and submission cutoff safely in the event timezone while authoritative instants remain UTC.

**Entry and reachability:** Use Schedule from Admin Manage. Open-now, scheduled, close, reopen, and schedule-edit actions remain route-backed form actions.

**Authoritative happy path:** Validate event start before end, establish a valid signup closing no later than start, use a configured future opening plus the persisted automatic-opening toggle for scheduled mode, or record the actual current opening for manual mode. Default submission cutoff is 30 minutes after event end and cannot precede that end. Capacity uses the posted value and an eligible increase atomically promotes the waiting queue with its ordinary audit and notifications.

**Permissions and history:** An unchanged historical timestamp is accepted, but a passed boundary cannot be changed or cleared and every newly entered or changed timestamp must be future. Signup opening and automatic-opening enablement lock once that boundary passes. Reopening manually closed signup reuses its configured close when that close remains future; otherwise Reopen establishes and confirms a replacement future close. Published start/end cannot be cleared. Draft time is optional planning information and never starts the draft. Routine pre-Live Schedule mutations retain automatic actor/time/before/after audit evidence and require no written reason; changing a future end while Live additionally requires confirmation and a written reason.

The following matrix is the authoritative editability contract. `Edit` means the value is available through Schedule, `Action` means only the named lifecycle action may change or reuse it, and `Locked` means it is retained as read-only history.

| Schedule value | Private Draft | Signup Open | Signup Closed, draft not started | Draft Running/Paused | Draft Finalized, pre-Live | Live | Final Review | Finalized / Archived / Cancelled |
| --- | --- | --- | --- | --- | --- | --- | --- | --- |
| Signup opening | Edit while future | Locked | Locked | Locked | Locked | Locked | Locked | Locked |
| Automatic signup opening | Edit before the opening boundary | Locked/off | Locked/off | Locked | Locked | Locked | Locked | Locked |
| Signup closing | Edit while future | Edit while future | Action: Reopen reuses the configured close when it remains future; otherwise it establishes and confirms a replacement future close | Locked | Locked | Locked | Locked | Locked |
| Draft time | Edit while future | Edit while future | Edit while future | Locked | Locked | Locked | Locked | Locked |
| Event start | Edit while future | Edit while future | Edit while future | Locked | Edit while its existing boundary remains future | Locked as actual/history | Locked | Locked |
| Event end | Edit while future | Edit while future | Edit while future | Locked | Edit while its existing boundary remains future | Edit to another future time with confirmation and reason | Action: Resume reuses the configured end when it remains future; otherwise it requires a replacement future end; confirmation and reason are always required | Locked |
| Normal submission cutoff | Derived as event end plus 30 minutes; never directly editable | Same | Same | Same | Same | Re-derived when a future event end changes | Historical | Locked |
| Reopened submission cutoff | Unavailable | Unavailable | Unavailable | Unavailable | Unavailable | Unavailable | Action: Reopen submissions with a future cutoff and reason | Locked |
| Participant capacity | Edit | Edit; an increase may promote waiting participants | Edit before draft start | Locked | Locked | Locked | Locked | Locked |

Actual signup opening/closing, actual event start/end, submission closure, finalization, archival, and cancellation timestamps are system-recorded history and are never ordinary Schedule inputs. Hidden events expose only SuperAdmin restoration, and Discarded events expose no event workspace.

While the team draft is Running or Paused, the complete schedule is locked. Once the draft is Finalized but before Live, signup dates, draft time, and capacity remain locked, while a still-future event start or end may change. Every accepted schedule change rechecks ordering, operational-window overlap, and any linked Wise Old Man competition's five-minute window tolerance; changing event end atomically re-derives the normal submission cutoff and commits its audit evidence in the same transaction.

**Failure and recovery:** A missing/invalid schedule blocks the transition; manual-opening defaults are previewed and committed only inside the successful transaction. Schedule shows one server-recomputed confirmation containing only actual changes, derived cutoff/capacity-promotion effects, and current warnings when the event is public or warnings exist; private warning-free changes save immediately. Scheduled readiness failure leaves signup closed and alerts Admins. A postponed start does not close its own recovery path: while the event has not actually started and its configured end remains future, Admins may complete pre-Live roster corrections, draft finalization, and board publication, then start manually without rewriting the missed configured start. If the configured end has also passed, start fails closed and the event must be cancelled or replaced. Signup-open/closed edits recheck non-overlap, and a linked Wise Old Man competition must remain within five minutes of the proposed event interval. Timezone changes alter display only; explicit schedule edits alter stored instants and retain before/after history.

**Acceptance outcome:** Manual and scheduled opening share one readiness contract, closing and cutoff boundaries remain valid, and delayed processing never backdates or extends competitive eligibility.

### 4.5 `ADM-SIGNUP-01`, `ADM-SIGNUP-02`, `ADM-SIGNUP-03`, `ADM-SIGNUP-04` — Fields, capacity, readiness, and opening

**Actors and outcome:** A signup Admin defines the fixed primary Account, primary EHB, captain volunteer, custom questions, capacity, waiting-list, signup-code, and withdrawal settings; the Admin or scheduler opens signup only when the requested opening mode is ready.

**Entry and reachability:** Questions and Schedule are reached from the setup workspace. The Admin chooses Open now or scheduled opening; the scheduler uses the same server readiness service.

**Authoritative happy path:** The primary regular Account is required and its EHB is required. Secondary Account questions are optional and explicitly regular (`PLAYING`) or informational (`INFORMATIONAL`); regular answers have their own EHB, while alts do not. Opening validates description, capacity, start/end/closing, Discord login configuration, form shape, and enabled signup code. It records actual manual opening or the configured scheduled opening.

**Permissions and history:** Signup definitions are versioned. After the first accepted response, answer shape cannot be changed; safe metadata changes and optional later questions preserve old answers. Draft start freezes ordinary form changes and closes signup.

**Failure and recovery:** Missing description, capacity, schedule, primary playing field/EHB, invalid question, unusable enabled code, or requested opening inconsistency blocks opening. Disabled waiting list and public text answers are warnings requiring acknowledgement. Banner, board, teams, draft time, and current Wise Old Man availability are not opening blockers. A failed scheduled attempt leaves signup closed and exposes its blockers.

**Acceptance outcome:** Capacity and deterministic waiting-list order are authoritative, Account roles are unambiguous, and signup can open manually or on schedule without partial form/question changes.

### 4.6 `ADM-RULES-01` and `PUB-HOWTO-01` — Permanent guidance

**Actors and outcome:** An enabled Admin maintains one global public Rules page; any visitor reads Rules and source-controlled how-to pages, including how to submit drops.

**Entry and reachability:** Event dashboards and submission surfaces link to the global guidance. Rules is not event-owned. How-to content is delivered through normal public routes and has no in-application editor.

**Authoritative happy path:** Rules changes use normal Admin authorization, validation, concurrency, and automatic history. How-to changes use the development content workflow. General upload/submission instructions are not copied into each tile.

**Permissions and recovery:** A stale Rules edit is rejected and reloaded. The content never becomes a hidden lifecycle prerequisite. The source-controlled `/HowTo` guide is independently reachable and has no in-application editor or lifecycle dependency.

**Acceptance outcome:** Visitors have one stable guidance destination and objective-specific tile criteria remain local, while no Rules/how-to content silently blocks signup, draft, board, start, or finalization.

### 4.7 Managed-image rule

**Actors and outcome:** An authenticated Admin or participant uploads an application-owned banner, team image, board/tile artwork, or evidence asset through the shared managed storage path.

**Entry and reachability:** The owning form offers local-file selection and a server upload route. The route-backed form remains usable without the enhanced interaction.

**Authoritative happy path:** The server validates, inspects, checksums, stores, and authorizes the asset under its owning retention/privacy rule. A managed asset reference, not image bytes or an arbitrary external URL, enters the owning record.

**Failure and recovery:** Invalid type/size/content or storage failure leaves the prior asset active and shows retryable feedback. Catalogue source-image URLs remain the sole external-image exception and use catalogue fetch/cache rules.

**Acceptance outcome:** Application-owned images use one safe upload boundary; an asset failure cannot break the event identity, board, team, or evidence workflow.

## 5. Identity, signup, and roster authority

### 5.1 `PART-IDENTITY-01`, `PART-PROFILE-01`, `ADM-IDENTITY-01`, `ADM-IDENTITY-02` — Account and recovery

**Actors and outcome:** A participant creates and authenticates one website account; an Admin resolves event-scoped ownership errors; an account holder or authorized Admin recovers password or Discord access without creating a second identity.

**Entry and reachability:** Continue with Discord starts onboarding. After onboarding, Login offers Discord or public username/password. Settings exposes password, Link/Change/Unlink Discord, and My accounts. Forgot password gives neutral contact-an-Admin guidance; an authorized Admin starts an expiring reset link after out-of-band verification.

**Authoritative happy path:** Onboarding requires a unique public username, password, and first OSRS character. Password policy, throttling, ticket limits, single-use hashed reset tokens, and session invalidation follow the technical authority. A character is linked first in My accounts and is therefore preferred; it is not an ownership proof.

**Permissions and history:** Discord IDs and usernames are unique in their own boundaries. Link/unlink/change, reset, and event-participant ownership transfer are atomic and audited. An Admin may transfer an event participant only to an account without an existing participant in that event; the global accounts are not merged.

**Failure and recovery:** Expired onboarding/reset state creates no partial account. Generic login/reset feedback does not disclose account existence. Discord relinking preserves event participation, roles, characters, evidence, and history. A duplicate or mistaken event link loses the old event access and grants the destination the same preserved event scope only after confirmation.

**Acceptance outcome:** One durable website identity controls normal access, recovery, and event ownership; no display name or character match silently claims an account or participant.

### 5.2 `PART-ACCOUNT-01` — My accounts and active-character swaps

**Actors and outcome:** A participant manages an ordered set of globally linked OSRS characters, personal labels, and optional EHB defaults; the first active character in that order is the sole preferred character. The participant swaps the active event character during Live.

**Entry and reachability:** Authenticated navigation exposes My accounts and the event signup/edit flow links back to it when a needed character is missing. The Live team-board context exposes swap only when the event and participant state allow it.

**Authoritative happy path:** Global character links may be shared by multiple website accounts. My accounts position 01 is always the preferred character; reordering immediately transfers preference to the character moved into position 01, and there is no separate set-preferred action. Within one event, a character is assigned to one participant only. The primary signup Account becomes the initial active/drop-eligible character. During Live, a participant requests a swap among frozen playing accounts; it takes effect at the next whole UTC minute strictly after request.

My Accounts saved-EHB entry, Wise Old Man fetch results, persistence, and
rendering use no more than two decimal places. Additional decimal places use
standard decimal rounding, so `3000.09582` becomes `3000.10`; existing event
snapshots remain independent and unchanged.

**Permissions and history:** Informational/alt accounts cannot become active, receive evidence credit, or enter WoM standings. Swaps are append-only and stop at event end. A global unlink never deletes historical event assignments or evidence. My-accounts EHB defaults do not rewrite event snapshots.

**Failure and recovery:** A duplicate event assignment, unavailable character, stale version, or second pending swap fails without residue. While a future swap is pending the old character remains active. A corrected global character name is atomic and fails as a whole if any editable event conflicts.

Unlinking a character with an upcoming or live registration requires explicit
confirmation and fails closed when confirmation is absent. The unlink removes
the global My Accounts link only; the event registration and history remain
unchanged.

**Acceptance outcome:** Active-account authority is explicit, time-bounded, and historical; evidence uses the active account at server submission time and never offers an arbitrary credited-account selector to a participant.

### 5.3 `PART-SIGNUP-01`, `PART-SIGNUP-02` — Submit, confirm, edit, cancel, rejoin, and restore

**Actors and outcome:** An authenticated participant creates or edits one event signup, sees confirmed/waiting status, may cancel/rejoin while permitted, and receives the correct restoration outcome when an Admin restores them.

**Entry and reachability:** Public event discovery lists public signup-open events and routes them to the existing signup page; the exact-link signup route and authenticated My events destination also reach the form or confirmation. A returning owner is sent to their existing record rather than a duplicate form. Signed-out signup entry uses the existing Login route with a validated local ReturnUrl.

**Authoritative happy path:** The participant selects linked characters, enters the primary and optional custom answers, submits, and receives Confirmed or Waiting list with exact position. The signup presentation places the required/system primary regular account first, then additional playing accounts in configured order, then informational/alt accounts in configured order. On a fresh signup only the required system primary defaults to the preferred linked character; every later account question begins None/unselected and required later questions must still be answered before submission. Optional account answers expose a clear/none choice while the required primary cannot be cleared. The transaction validates answers, reserves all named characters, stores event EHB snapshots, and preserves original queue order on ordinary edits.

**Permissions and history:** Normal edit is available only while signup is open. Separate confirmed withdrawal while signup is open releases reservations and may promote the earliest waiting participant. Rejoin while open reacquires accounts with a new queue position. After close and before draft, self-withdrawal remains available but self-restore does not. Admin restoration uses current capacity and end-of-queue rules and never displaces a promoted participant.

**Failure and recovery:** A private or unpublished signup slug fails closed for non-Admins. A character conflict or capacity race rejects the whole attempt, preserves the saved record, and identifies the conflicting answer. Promotion, cancellation, withdrawal, rejoin, restoration, reservations, and status commit atomically. Required Discord onboarding and password-change handoffs preserve the validated local signup ReturnUrl. Promotion creates direct in-site destinations for the linked participant and enabled Admins; Discord contact remains manual.

**Acceptance outcome:** A participant can manage one authoritative signup through its permitted lifecycle, with deterministic capacity/queue behavior, complete confirmation, no private edit-token dependency for normal users, and preserved history after withdrawal or restoration.

### 5.4 `PUB-SIGNUP-01` — Exact-link public signup table

**Actors and outcome:** A visitor with the exact shared link reads the unlisted signup table; participants see their public-facing answers without private account, payment, or security data.

**Entry and reachability:** Admin Manage and the signup journey expose the exact link when the event phase permits. It is not a global public listing. After draft finalization, non-Admin requests for the signup-board route follow the published roster destination.

**Authoritative happy path:** The table shows confirmed and waiting profiles, primary regular OSRS character, regular-account EHB, alt headings/answers, captain volunteer, participant-facing custom answers, and exact waiting positions. Public projection rules, not the private confirmation, decide its contents.

**Permissions and history:** Website username, Discord identity, payment, Admin notes, audit/security data, and private answers never enter the table. Visibility is not an invitation to edit; server signup ownership still applies.

**Failure and recovery:** A closed/draft/finalized phase returns the phase-safe destination or unavailable result. A stale form definition reloads the current version while preserving existing answers.

**Acceptance outcome:** The exact-link table is reachable when intended, useful for public signup, and unable to expose private identity, payment, security, or Admin data.

### 5.5 `ADM-PARTICIPANT-01` — Participant administration and retained private metadata

**Actors and outcome:** An enabled Admin manages confirmed, waiting, and withdrawn participants from one event workspace, including private payment and notes, pre-draft corrections and internal additions, and explicit ownership actions.

**Entry and reachability:** Admin Manage → Participants is the authoritative workspace. Detail and responsive route forms are projections of the same event-level service, not independent participant stores. Retained participant detail remains reachable to an Admin when a terminal event permits only payment or private-note maintenance; every other control on that view remains independently lifecycle-gated.

**Authoritative happy path:** Search/filter status, queue, source, identity-link state, team state, accounts/EHB, captain volunteer, public answers, payment, and notes. Correct answers/accounts with the same validation and reservations. Add an internal participant through Admin authorization using ordinary capacity and waiting-list rules; keep external/pre-formed roster members outside this pool.

**Permissions and history:** Payment is private binary Paid/Unpaid. Payment and private Admin notes remain editable and audited in every retained visible lifecycle state, including during the team draft and after Finalized, Archived, or Cancelled; Hidden and Discarded events remain inaccessible. Participant answers, registered accounts, queue, roster, evidence, and competitive history do not become editable merely because these private fields remain available. Corrections preserve queue/status; withdrawal uses one Withdrawn state with actor history. Admin action, account correction, restoration, and withdrawal notify a linked participant where the capability says so; payment, notes, and ordinary answer corrections do not.

**Failure and recovery:** Conflicting corrections fail atomically. An unavailable character or capacity limit is shown before mutation. Withdrawn records retain history and release current reservations before any promotion transaction.

**Acceptance outcome:** Admins can correct and operate the pre-draft roster and maintain retained private payment/note records without changing public privacy, queue order, account uniqueness, competitive history, or the separate external-team boundary.

### 5.6 `ADM-PARTICIPANT-02` and `ADM-CAPTAIN-01` — Post-draft exceptions

**Actors and outcome:** An enabled Admin withdraws a drafted/live participant, optionally fills a vacancy, and assigns or changes Captain/co-captain roles; the affected team and linked accounts receive the correct destinations.

**Entry and reachability:** Draft/Manage exposes participant withdrawal, waiting-list replacement, internal replacement, vacancy, and role controls. The action is route-backed and available during Live subject to lifecycle rules.

**Authoritative happy path:** After draft start, self-withdrawal is unavailable. Admin withdrawal revokes current website event/team mutation authority and preserves membership, account reservations, evidence, and contribution history. Replacement is selected explicitly from the available waiting list or created as a validated internal replacement. It joins the chosen team prospectively; the draft ledger is not rewritten.

**Permissions and history:** Captain/co-captain changes apply to current active memberships immediately and remain available through Live and Final Review only while the active submission window still accepts uploads. Every team needs a Captain or enabled emergency credential for event-start readiness; co-captain alone does not satisfy it. An explicit participant-ownership transfer is available through Final Review, requires strong confirmation, preserves competitive history, and remains unavailable in Finalized, Archived, Cancelled, Hidden, or Discarded. Role, ownership, withdrawal, vacancy, replacement, and effective eligibility history is preserved. There is no automatic post-draft promotion.

**Failure and recovery:** A stale vacancy, unavailable replacement, duplicate account, or invalid role target fails without changing the roster. A live withdrawal ends drop eligibility at the approved whole-minute boundary; a live replacement begins prospectively at its own boundary, leaving any gap real. Missing final Captain creates an urgent action but does not pause Live.

**Acceptance outcome:** Drafted rosters remain historically truthful while Admins can handle real departures and authority changes without inherited credit, silent promotion, or accidental global role grants.

## 6. Draft, board, and catalogue-derived competition setup

### 6.1 `ADM-DRAFT-01` — Team setup, snake draft, finalize, and reopen

**Actors and outcome:** An enabled Admin creates drafted and pre-formed teams, assigns Captain/co-captain, runs a controlled snake draft, and publishes a finalized roster/pick order; an Admin may reopen before event start for a reasoned correction.

**Entry and reachability:** Admin Manage → Draft is the controller route. Other Admins receive the live read-only view; public roster destinations appear only after finalization.

**Authoritative happy path:** Drafted-team count and roster distribution derive from active drafted teams and confirmed internal participants. Pre-formed teams, waiting/withdrawn participants, and external rosters are outside the pool. The controller scrambles before the first active pick, records immutable active snake picks, supports pause/resume and repeated latest-pick undo, then finalizes balanced teams with a Captain on every drafted team.

**Permissions and history:** The controller lease, pick ledger, team metadata, formation type, membership, and roster publication are server-authoritative. Finalization publishes rosters and effective pick order, not undone attempts or internal controller data. Team name/image/affiliation changes remain available until the event actually starts under ordinary history rules.

**Failure and recovery:** Missing teams, Captain, participant assignment, balanced distribution, or stale controller lease blocks start/finalization and names the direct resolution. A finalized draft can be reopened before the event actually starts with strong confirmation and written reason; structure remains locked, history is retained, and re-finalization republishes the corrected projection. A missed configured start does not block roster correction, draft finalization/reopening, or re-finalization while the event remains pre-Live and its configured end remains future.

**Acceptance outcome:** Draft setup has no independent team-count/target-size authority, snake order and undo are deterministic, pre-formed rosters stay separate, and roster publication does not publish the board or start the event.

### 6.2 `ADM-BOARD-01` — Build, approve, snapshot, publish, and correct

**Actors and outcome:** An enabled Admin builds one event board, approves a complete board, publishes it separately, and performs reasoned replacement corrections without erasing prior competitive history.

**Entry and reachability:** Admin Manage → Board is the editor. Preview uses the public board treatment and inherits the public Board ecosystem's visual status; direct public board/team/tile routes remain the normal destinations after publication.

**Authoritative happy path:** Fill every grid position with valid tile/objective data and automatic EHB for catalogue/drop tiles or explicit EHB for custom/manual objectives. Approve in a transaction that rechecks completeness and creates an immutable approval snapshot. After draft finalization, use a separately confirmed Publish board action/transaction. Event start requires publication.

**Permissions and history:** Any enabled Admin may approve. Editing competitive content invalidates an unpublished approval and retains its history. Initial publication and publication of a corrected replacement both require server-enforced confirmation. Publication uses the active snapshot without recalculating from mutable catalogue data. Post-publication correction requires confirmation, a reason, a replacement snapshot, and preserved prior history; it is available only in SignupClosed, Live, or AwaitingFinalReview and is unavailable in terminal, Cancelled, Hidden, or Discarded states.

**Failure and recovery:** Empty positions, invalid objectives, or missing automatic catalogue EHB block approval with diagnostics; manual override cannot repair a broken standard tile. Dismissing the post-draft publication prompt leaves rosters public and the board private. Stale approval/concurrency fails without publication residue.

**Acceptance outcome:** Board approval and publication are separate, visible authority boundaries; incomplete or mutable data cannot become the competitive public snapshot by accident.

### 6.3 Draft/board catalogue coupling and approval snapshot

**Actors and outcome:** Admin catalogue edits keep unapproved board projections fresh while approved/published competitive snapshots remain immutable.

**Entry and reachability:** Catalogue edits and the board editor are independent Admin routes connected by direct invalidation/reload destinations. Approval is reached from the board editor, not from catalogue save.

**Authoritative happy path:** A draft board stores stable catalogue references and live derived names/images/rates/EHB. Relevant catalogue mutation invalidates and recalculates every affected unapproved projection. Approval locks/rechecks the referenced versions and freezes the competitive snapshot. Unapproval or a competitive edit starts a new draft/approval version while retaining superseded history.

**Permissions and history:** The catalogue owns reusable source facts; the board owns objective configuration and approval snapshots. Publication reads the active snapshot. No board copies, tile templates, or historical recalculation silently merge these ownership boundaries.

**Failure and recovery:** A stale board/catalogue version rejects approval or import and reloads authoritative values. A missing derived EHB is corrected at the catalogue/requirement source, not by a manual override on a standard tile.

**Acceptance outcome:** Draft calculation can change with catalogue data; an approved/published board cannot change without its explicit versioned correction workflow.

## 7. Live operation, evidence, and official results

### 7.1 `SYS-EVENT-START-01` — Scheduled start readiness

**Actors and outcome:** The scheduler attempts start at the configured instant; an Admin clears blockers and uses Start event now when necessary.

**Entry and reachability:** The scheduler and the Admin Manage readiness action use the same lifecycle service. A postponed start exposes the blockers and a direct Admin resolution route such as Draft, Board, Schedule, or Teams.

**Authoritative happy path:** At the scheduled instant the server rechecks finalized draft, published board, valid assignments, Captain/emergency access, schedule, singleton-current boundary, and all lifecycle invariants. If ready, Live begins at the actual transition time; if overdue after a postponement, an authorized Admin starts explicitly.

**Permissions and history:** Automatic start never bypasses gates or backdates eligibility. Early manual start requires strong confirmation and a written reason. Scheduled attempt occurrence, blocker state, actual start, and audit remain distinct.

**Failure and recovery:** A blocked attempt leaves the event pre-live, retains the configured instant, records visible failure, and does not retry into an unexpected start. Clearing blockers followed by Start event now is the recovery even after the configured start has passed, provided the configured end remains future. If the configured end has also passed, start fails closed and the event must be cancelled or replaced.

**Acceptance outcome:** A delayed or invalid deployment cannot silently start an unready event, and the Admin can identify and resolve every blocker.

### 7.2 `SUBMISSION-WORKSPACE-01` — Canonical Captain/participant submission workspace

**Actors and outcome:** Authenticated current team members use one canonical submission workspace to inspect the complete team submission history. Captains/co-captains and valid enabled EmergencyCaptain access additionally manage team focus, identify submission problems quickly, and edit eligible team submissions without gaining general Admin or review authority.

**Entry and reachability:** `/Submissions` is the canonical authenticated team submission overview and `/Submissions/{id:guid}` is its canonical detail route. The overview is team-wide for every authorized current member, including retained rows credited to departed teammates. General Participant and Captain navigation each prefer the account's sole Live team; when none is Live they use the sole Awaiting Final Review team. This Captain rule includes co-captains and valid enabled Emergency Captain access. Finalized/Archived memberships do not suppress a current link, and genuinely ambiguous eligible teams expose no unscoped shortcut. Captains/co-captains see the two Captain-only top sections—team focus and team submission status—and retain server-authorized broader editing of eligible team submissions; ordinary participants do not see those sections and may edit only their own eligible non-read-only submissions. `/Captain` and `/Captain/Submissions/{id:guid}` are thin compatibility redirects/aliases to the canonical routes, not separate rendered implementations. Personal submission/evidence notifications resolve to the canonical detail route; general submission navigation resolves to the overview. Evidence submission still starts from the normal team-board tile and uses exactly its shared drawer/interface. `/Captain/Submit/{tileId?}` remains only the drawer transport/handler plus a compatibility redirect for old direct links. Below 901px and without JavaScript, team and tile routes remain usable directly.

**Authoritative happy path:** Before/during draft, drafted-team captains see the expanded confirmed signup projection needed for selection, excluding payment, notes, security, and audit. During Live, the canonical overview first shows Captain-only team focus controls, then Captain-only pending/rejected/approved submission status totals, then the complete team submission ledger covering pending, approved, rejected, withdrawn, replaced, and other retained historical states. Team focus is visible read-only to the whole current team on its ordinary board. Status, player, and tile filters narrow the ledger without changing authority; each result links to the canonical detail route and available reviewer feedback. The detail composition remains visually equivalent to the approved Captain submission detail. Captain-authorized submission for a current teammate continues only through the ordinary team-board drawer and shared drawer transport route, not through a second form or tile grid.

**Permissions and history:** The server derives the credited participant and active playing account. Focus is team-private, concurrency-protected, non-competitive, visible read-only to current teammates, hidden from opponents and the public, and read-only at event end. Captain, co-captain, and valid emergency-captain access retain the same team-management scope already granted to them, including broader editing of eligible team submissions; ordinary participants may edit only their own eligible non-read-only submissions. Every canonical detail request independently enforces current-team, owner, role, retained-state, cutoff, privacy, and Admin boundaries. Captains do not design boards, approve/reject/reverse evidence, review another team's private evidence, manage rosters, grant authority, or gain any Admin review control.

**Failure and recovery:** A stale focus or submission mutation reloads current team state. A removed role/membership or disabled emergency credential loses authority immediately. An unavailable, legacy-linked, or no-longer-visible ledger record resolves through the canonical route and then fails closed without leaking another team's evidence. Relative `_EvidenceUpload` partial references must be rooted correctly so `/Submissions/{id:guid}` cannot 500. External pre-formed members may be represented by enabled emergency captain access, not by inferred account ownership.

**Acceptance outcome:** Current team members inspect submissions from one canonical overview/detail implementation; Captains coordinate focus and resolve eligible team submission problems there, ordinary participants retain owner-only mutation, all new submissions still use the ordinary team-board experience, and review remains Admin-only.

### 7.3 `PART-LIVE-01` — Live participant workflow

**Actors and outcome:** A current participant reaches the published team roster or existing team-board view, sees compact active-account context, submits and manages own evidence, swaps active playing account within the Live window, and sees the authorized read-only team-focus projection.

**Entry and reachability:** After draft finalization, destination is the existing public `/Events/{slug}/Teams` roster until board publication; afterward it is the existing Board route. Once the Board is published, the unchanged Teams roster joins the shared Boards/Drops/Leaderboards context navigation as localized Teams/Hold and remains directly reachable from every Board-family view. Before Board publication the Teams roster remains standalone without that navigation, and Board-family routes remain unavailable. The participant's My events/current-event action resolves the same state-aware destination. Team cards navigate normally to the rendered team-board route at every viewport. The team board exposes only compact active-account context and swap when authorized; it does not render duplicate participant lifecycle or Captain focus-operation panels. The shared submission drawer is launched from the team-board tile flow; team and tile routes remain direct, reload, history, and browser-Back destinations. In the team board's main progress sidebar, all authorized current members use the canonical `Team history` navigation to `/Submissions`; legacy Captain links may redirect through `/Captain` but must not render a separate workspace. `/Submissions` shows the authorized team's complete retained submission ledger including departed credited members, and `/Submissions/{id:guid}` is its detail route.

**Authoritative happy path:** A participant submits only for themselves; the server snapshots the current active playing account at submission time. The participant view of the canonical ledger contains no Captain-only team-focus or team-submission-status sections. Only the credited owner may edit pending evidence, replace its active screenshot, withdraw through cutoff, or create its one permitted linked correction after rejection or reversal; Captains/co-captains retain their server-authorized broader editing of eligible team submissions. Approved, withdrawn, replaced, reversed, and other retained states, plus teammate-owned rows for ordinary participants, are read-only. An account that is also Admin/SuperAdmin retains exactly this genuine event-role experience in addition to separate Admin navigation/review; global status alone grants no team scope. Event end closes new-drop eligibility and swaps but leaves upload grace for in-window drops; cutoff closes participant mutation while Admin review continues.

**Permissions and history:** Current team members see the team's retained submission history, feedback, and private evidence, including rows credited to departed members. Former members, anonymous users, and cross-team viewers fail closed except that an archived former credited owner may follow the existing account-history destination to only their own Rejected/Withdrawn detail and evidence asset read-only. Approved evidence is public. Team focus is read-only to ordinary members and public board projection remains unchanged.

**Failure and recovery:** A rejected or reversed attempt may use its linked correction path through the active/reopened upload cutoff, which requires a new image and preserves credited participant/account read-only. The predecessor stays immutable, cannot be directly re-approved, and permits at most one direct child under retries/concurrency. Realtime invalidation does not interrupt an active submission/result. After draft start, self-withdrawal directs the participant to an Admin.

**Acceptance outcome:** Live participants can follow the existing board context without a hidden account selector, invalid post-end drop, private-evidence leak, or broken responsive route.

### 7.4 `ADM-EVENT-END-01` — End, grace period, and resume

**Actors and outcome:** The scheduler ends Live at the configured event end; an Admin may end early, review during the submission grace period, or resume a premature end before official finalization.

**Entry and reachability:** Manage exposes End event, Resume event, and cutoff state; Review and Finalize remain reachable after the transition.

**Authoritative happy path:** Scheduled end uses the configured effective end even if processing is late. Early end uses confirmation time and a written reason. Uploads for in-window drops remain valid until the separate cutoff. Resume requires strong confirmation, a reason, singleton clearance, and prospective Live eligibility. It reuses the configured end when that instant remains future; otherwise it requires a replacement future end. A supplied replacement end remains optional when the retained configured end is still future.

**Permissions and history:** Event end does not rewrite the original schedule, submission cutoff, evidence, swaps, focus, or prior roster history. Resume preserves the final-review interval and all records made during it; it is not available from Finalized or Archived.

**Failure and recovery:** A late worker catches up without extending play. A post-end screenshot timestamp fails review when it proves an out-of-window drop. Resume failure leaves the final-review state unchanged. Live withdrawal and replacement use their separate approved whole-minute boundaries.

**Acceptance outcome:** Competitive eligibility ends at the authoritative time, upload grace is distinct, and a premature end can be corrected without erasing history or backdating new play.

### 7.5 `ADM-REVIEW-01` — Evidence review, correction, and reversal

**Actors and outcome:** Any enabled Admin reviews pending evidence; a Captain or participant receives only the rejection/resubmission scope their role allows.

**Entry and reachability:** Admin Review queue opens pending submissions with event/team/tile filters. Participant/Captain history links open the same submission through their authorized projection.

**Authoritative happy path:** An Admin approves or rejects. Rejection requires a reason and leaves an immutable historical attempt with a linked correction path through cutoff. Before approval, a reasoned correction may change tile, requirement, drop, or credited playing account only when evidence supports it; participant is derived from the account and a changed target receives its authoritative frozen weight. Approval reversal requires strong confirmation and a reason, then recalculates all affected progress/rankings. The Reversed attempt remains immutable; through the active/reopened upload window it may have one linked Pending correction with a new image and normal review.

**Permissions and history:** Submission time, destination-derived board snapshot weight, calculated contribution, original image, review actions, and predecessor chain are preserved. Admins cannot upload or replace another user's evidence image. Submission/review mutations write the main immutable audit entry atomically; local review history remains supplementary. Notifications reach the linked credited participant and current team captains where applicable, without notifying another team or exposing private evidence.

**Failure and recovery:** Optimistic concurrency rejects a stale decision. A duplicate/unusable image is Reject, not a third review state. One direct child per Rejected or Reversed attempt prevents duplicate correction; a child that is later rejected may itself have one child while the window is open. Direct Reversed reapproval is forbidden. Cutoff closes participant/captain mutation but not Admin review.

**Acceptance outcome:** Evidence decisions are binary and auditable, correction and reversal are reasoned, and all progress effects follow the authoritative submission/data-model rules.

### 7.6 `ADM-FINALIZE-01` — Final review, official results, and unfinalize

**Actors and outcome:** An enabled Admin resolves final-review blockers and finalizes official results; a later correction can unfinalize without reopening uploads or deleting the prior official version.

**Entry and reachability:** Finalize is reachable from Admin Manage after event end and cutoff. Review queue and blocker links are direct destinations.

**Authoritative happy path:** Cutoff closes new uploads. Admins resolve or explicitly override every blocker, then confirm finalization. The transaction recalculates authoritative progress/rankings, stores immutable result and placement snapshots, records actor/time, and publishes official results.

**Permissions and history:** Pending submissions block finalization. Overrides require strong confirmation and written reason and do not mutate the blocker. Normal finalization needs confirmation but no reason. Unfinalize requires confirmation/reason, supersedes the official snapshot, and returns to a new final-review state without reopening submissions.

**Failure and recovery:** Stale readiness, concurrent finalization, or a missing blocker resolution fails before official mutation. Captain website roles remain historical but cannot mutate closed/finalized events. Assignment of a Captain never auto-generates a password; emergency credentials are explicit fallbacks, disabled by default, and disabled at cutoff until explicitly re-enabled.

**Acceptance outcome:** Official results have one explicit immutable version at a time, every unresolved competitive blocker is visible, and correction cannot silently rewrite evidence or reopen gameplay.

## 8. Lifecycle, archive, history, and deferred feedback

### 8.1 `ADM-EVENT-ARCHIVE-01`, `ADM-EVENT-CANCEL-01`, `SYS-CURRENT-EVENT-01`

**Actors and outcome:** An Admin archives finalized results, cancels a protected pre-Live event, and operates within the production current-event boundary while Development retains explicit labelled scenarios.

**Entry and reachability:** Archive and cancel appear only in their permitted state-specific Admin destinations. Archived public routes and participant history remain reachable through normal event navigation.

**Authoritative happy path:** Archive requires Finalized and confirmation, preserves snapshots, rosters, board, evidence, URLs, and history, and removes the event from current operations. Cancel requires pre-Live state, confirmation, and written reason; it closes signup, suppresses schedulers, disables event mutation, and preserves all records. A previously public cancelled event shows only a generic public cancellation state.

**Permissions and history:** Empty unprotected experiments use discard; protected records use cancellation. Production prevents overlapping singleton operational windows through application transition policy; development fixture exemption is explicit and not a production setting. Archived and cancelled records are read-only for ordinary participant/captain mutations.

**Failure and recovery:** Invalid state, concurrent lifecycle mutation, or current-event conflict fails before mutation and names the direct recovery. Cancellation has no ordinary resume. Archived unfinalization is exceptional, reasoned, and subject to current-event rules; scheduled workers ignore cancelled events.

**Acceptance outcome:** Official history remains public and stable, populated events are never discarded, production current-event selection is unambiguous, and Development can expose multiple explicit scenarios safely.

### 8.1a `SUPERADMIN-EVENT-QUARANTINE-01` — Hidden-event quarantine

**Actors and outcome:** Only the designated Super Admin may reversibly hide or
restore an event as an orthogonal administrative quarantine. Hide and Restore
are available in Events Control and the Manage Danger Zone, and each requires
exact ordinal event-name confirmation, a mandatory reason, and a complete
immutable audit entry.

**Entry and reachability:** Events Control has a clearly separated Hidden
filter/area. A hidden event's only rendered destination is the limited
SuperAdmin Manage inspection surface reached from that area; it shows retained
lifecycle information, hide/restore audit history, and Restore. No ordinary
event workspace or mutation is reachable while hidden.

**Authoritative happy path:** Hide succeeds only for `AWAITING_FINAL_REVIEW`,
`FINALIZED`, or `ARCHIVED`. It records hiding metadata and removes the event
from every ordinary discovery, history, account, submission, evidence,
notification, action, audit, and realtime projection. Restore clears only that
metadata and returns the unchanged lifecycle and event data.

**Permissions and history:** Draft, SignupOpen, SignupClosed, Live, Cancelled,
and Discarded events cannot be hidden. Public visitors, participants,
Captains/co-captains, emergency authority, and ordinary Admins receive 404 or
an absent projection for hidden events. Super Admin does not bypass the rule on
public routes. All database relations, snapshots, rankings, evidence, audit
history, managed assets, and storage objects remain retained. Hide and Restore
emit no notification.

**Failure and recovery:** Invalid state, non-SuperAdmin authority, missing
reason, inexact confirmation, stale concurrency, or an attempted ordinary
workspace access fails without mutation or disclosure. Hidden events have no
active-event scheduler, signup, singleton/window-collision, or active realtime
processing because eligibility begins after Live. Restore is the only recovery
before ordinary event operations resume.

**Acceptance outcome:** A Super Admin can quarantine and restore an eligible
post-Live event with full accountability while every other role and route is
fail-closed and the competitive record remains unchanged.

### 8.2 `ADM-ACCOUNT-01` and `PART-HISTORY-01` — Access lifecycle and archive reading

**Actors and outcome:** An authorized Admin disables/restores website access; an archived participant reads their own permitted evidence history while public visitors read preserved public results.

**Entry and reachability:** Admin Accounts exposes role-appropriate disable and restore. Archived event routes expose public boards/teams/results; My events or the event history destination exposes the participant's own rejected/withdrawn history.

**Authoritative happy path:** Admin disables a User, or Super Admin disables an Admin, with strong confirmation and written reason. Sessions invalidate and the account becomes inactive. Restore requires confirmation, records history, and restores authentication only; it does not resurrect expired event authority.

**Permissions and history:** No actor disables self or the active Super Admin. Disable preserves usernames, Discord link, characters, participants, roles, submissions, evidence, contributions, snapshots, and history. Archived participants have no mutation controls, swaps, focus changes, upload, pending edit, withdrawal, or resubmission.

**Failure and recovery:** Role, self, Super Admin, or stale-state violations fail without disabling. A team losing its only usable Captain exposes the normal readiness/live warning. Re-enable never merges/deletes accounts.

**Acceptance outcome:** Access removal is immediate and reversible without destroying competitive history; archived participant access is read-only and privacy-scoped.

### 8.3 `PUB-FEEDBACK-01` — External feedback

**Actors and outcome:** Visitors and participants report bugs, evidence concerns, or general feedback through the community Discord channel; the application has no version-one public feedback/report form.

**Entry and reachability:** Rules/how-to content may identify the Discord path. There is no feedback persistence, public API, or in-application evidence-report journey to route or authorize.

**Authoritative happy path:** A reported evidence concern that is valid enters the Admin Review reversal and normal cutoff-bound resubmission workflows.

**Permissions and recovery:** The absence of a feedback form is a deferred scope boundary, not a failure state. No report is treated as evidence, progress, or an Admin action until an authorized workflow accepts it.

**Acceptance outcome:** Community feedback remains outside version-one product scope, while genuine evidence correction still has a reasoned, auditable path.

## 9. Global administration and supplementary integration

### 9.1 `SUPERADMIN-01` — Global ownership and role administration

**Actors and outcome:** The sole Super Admin manages global Admin grants, revocations, ownership transfer, and owner-only capabilities; ordinary Admins retain event administration but cannot manage global roles.

**Entry and reachability:** Admin Accounts exposes grant/revoke and Transfer Super Admin only when the server policy permits. Operator owner recovery has no public or ordinary web action.

**Authoritative happy path:** Grant/revoke targets an existing normal account, confirms before/after roles, records history, and invalidates affected sessions. Transfer atomically promotes the destination and demotes the previous owner, leaving exactly one active Super Admin.

**Permissions and history:** The current owner cannot self-demote except through a valid transfer. Admin role is independent of event membership and Captain role. Emergency credentials cannot become global roles. Cross-team focus is hidden from Super Admin by default and inspection is read-only unless team membership separately grants normal focus authority.

**Failure and recovery:** Ordinary Admin grant/revoke/transfer, invalid target, stale confirmation, or zero/multiple-owner result fails atomically. Lost-owner recovery is operator-controlled and audited, not a public first-user election.

**Acceptance outcome:** Global ownership remains unique and explicit; session authority changes immediately; global role does not bypass event/team privacy.

### 9.2 `ADM-CATALOGUE-01` and `ADM-CATALOGUE-IMPORT-01` — Catalogue administration and import boundary

**Actors and outcome:** An enabled Admin maintains boss/activity and source-drop catalogue records; only Super Admin performs bulk import preview/apply and permanent deletion where dependencies permit.

**Entry and reachability:** Admin Catalogue exposes normal CRUD, activation, deactivation, and source-image cache operations. Import preview/apply has no ordinary Admin route, control, or callable handler.

**Authoritative happy path:** Catalogue edits are optimistic-concurrency protected and audit before/after values. Referenced records deactivate rather than hard-delete. Super Admin import previews exact additions/changes and revalidates the preview hash/version before an atomic, confirmed apply.

**Permissions and history:** Only genuinely unused records can be permanently deleted after a complete dependency check; blocked deletion lists references and offers deactivation. Catalogue source-image URLs are the sole external image exception. Import never deletes referenced history.

**Failure and recovery:** Stale edit/import aborts without overwriting newer values. Invalid/unresolved rows appear in preview and block apply. A board projection reloads after relevant catalogue change; approval remains the board snapshot boundary.

**Acceptance outcome:** Ordinary Admin catalogue work is safe and reversible; bulk import and destructive deletion are Super-Admin-only and dependency-safe.

### 9.3 `ADM-AUDIT-01` — Immutable audit history

**Actors and outcome:** Any enabled Admin searches retained audit history; no actor edits, deletes, or exports it in version one.

**Entry and reachability:** Admin Audit is a server-paginated, newest-first route with filters for event, actor, action, entity, identifier, and date.

**Authoritative happy path:** Filter changes return to page one; pagination retains filters; entry detail renders structured before/after labels and values. The retained history is independent of the display page size.

**Permissions and history:** Audit entries are immutable. Event-linked audit
records for hidden events are omitted from ordinary-Admin projections and are
available only through the limited SuperAdmin quarantine inspection. Passwords,
hashes, tokens, OAuth secrets, evidence credentials, and unnecessary raw
Discord IDs do not enter snapshots or request context. Security logs remain
distinct where specified.

**Failure and recovery:** Stale/invalid filters return safe empty or validation feedback without weakening authorization. Reading detail never mutates the entry or resolves a business action.

**Acceptance outcome:** Admins can trace actor/time/before-after history without turning the audit view into an editable or secret-bearing data export.

### 9.4 `ADM-ACCOUNT-OVERVIEW-01` — Account overview

**Actors and outcome:** Admins inspect normal website accounts and emergency credentials as separate logical datasets; each action is independently authorized.

**Entry and reachability:** Admin Accounts provides server-side search/filter and pagination for website-account and emergency-credential views, with details reachable from rendered rows.

**Authoritative happy path:** Website rows expose username, global role, active state, Discord link state, last login, event-role summary, linked characters, event history, and disable history. Emergency rows expose event/team scope, setup/enabled/cutoff state, last login, and permitted reset/enable/disable actions.

**Permissions and history:** The overview never exposes passwords, OAuth data, setup/reset token values or hashes, or unnecessary Discord identifiers. Role, reset, disable, ownership-transfer, and event-participant transfer controls remain separately gated. No merge or permanent normal-account deletion exists.

**Failure and recovery:** A stale row action reloads current state and refuses to apply to a changed role/credential. Lost-owner recovery remains operator-only.

**Acceptance outcome:** Account administration is inspectable without conflating global identity, event ownership, emergency access, or security secrets.

### 9.5 `ADM-INBOX-01` — Personal notifications and unresolved Admin actions

**Actors and outcome:** A website account reads recipient-specific notifications; an Admin works an action projection for pending evidence, postponed start, waiting-list follow-up, vacancies, and missing Captains.

**Entry and reachability:** Notifications are reachable from the authenticated shell and each direct destination. Admin overview/action inbox is reachable from the Admin shell; notification and action links are independently server-gated.

**Authoritative happy path:** Opening a personal notification marks it read and lands on the relevant event/team/submission destination. Every personal submission/evidence notification routes to `/Submissions/{id:guid}`; relevant general submission navigation routes to `/Submissions`. The canonical detail independently authorizes the credited owner, current team member, Captain/co-captain/emergency team scope, retained state, and mutation boundary. Admin review notifications remain `/Admin/Review/Details/{id}`. An Admin action remains until its underlying lifecycle/evidence/roster condition resolves.

**Permissions and history:** Recipient, role, event scope, and privacy are checked at destination. Routine configuration and audit activity do not flood the inbox. Notification wording does not expose private reasons or hidden evidence.

**Failure and recovery:** A missing/closed destination shows the current safe state rather than a dead link. Repeated delivery is idempotent at the owning transition boundary; reading is not dismissal of the underlying action.

**Acceptance outcome:** Notifications are useful navigational reminders while Admin action state remains derived from authoritative records.

### 9.6 Wise Old Man supplemental journeys — account lookup and cached activity

**Actors and outcome:** A participant explicitly requests EHB lookup for a regular account in My accounts or signup/edit; an event optionally displays a cached competition-activity projection while Live.

**Entry and reachability:** Fetch from Wise Old Man is an explicit control after an account name is present. It never runs on render, typing, selection, save, public viewing, or event lifecycle transition. Admin competition configuration links one existing competition to an event through its event setup/Manage route; public pages read only the cached projection.

**Authoritative happy path:** A successful explicit signup/edit account lookup fills the current EHB control and immediately updates the authenticated owner's existing linked My Accounts character with the fetched EHB before signup submission; it creates or changes no event participant or assignment. Manual entry remains available, and a submitted signup stores the manual or freshly fetched event EHB snapshot. During Live, one cached competition-details synchronization fetches all relevant data no more often than the approved interval and derives participant/team activity locally.

**Permissions and history:** Wise Old Man is read-only and supplementary. Every regular `PLAYING` event assignment may contribute full cached competition delta; informational/alts are excluded. Cached activity is not official results and never changes signup snapshots, evidence credit, lifecycle readiness, or finalization authority. Public/team projections expose only privacy-safe matched totals, provisional/partial state, and coverage counts; exact missing names remain Admin-only.

**Failure and recovery:** Rate limit, unavailable, malformed, not-found, or partial responses produce accurate retry/incomplete/manual-entry feedback and never clear a valid entered EHB or the owner-linked My Accounts value or block an event transition. Missing accounts have no zero or carried-forward value; zero matches show no rankings. During Live, an Admin may replace a failed competition integration only with a validated competition whose window already matches the event within five minutes; Live replacement cannot clear the integration or synchronize the event schedule. Sync stops outside Live, retains readable cache, and resumes only on a legitimate return to Live. No Wise Old Man notification family or per-viewer request is introduced.

**Acceptance outcome:** WoM provides only explicit account lookup and cached Live competition activity, with manual EHB and the Bingo event model remaining authoritative and public output privacy-safe.

### 9.7 `ADM-HISTORICAL-IMPORT-01` — Frozen historical event import

**Actors and outcome:** An explicitly authorized operator runs the one-time import of **Det Store Danske Sommerbingo 2026**. The outcome is one audited, publicly readable `Archived` event whose board result and separate Wise Old Man activity ranking reproduce the approved historical record.

**Entry and reachability:** The importer is reached only through explicit operator CLI preflight and apply controls. Preflight must complete before apply. The imported event is created or matched directly in `Archived` with `Europe/Copenhagen`, `2026-07-14 18:00 CEST` (`16:00Z`) through `2026-07-19 18:00 CEST` (`16:00Z`), and `ArchivedAt` equal to event end. No web route, ordinary Admin control, temporary `Live` state, or ongoing synchronization is introduced.

**Authoritative happy path:** The operator validates the private 90-participant/93-account mapping outside Git, the six public board-spelled teams of 15, the public WOM competition link `145197`, the complete frozen per-account start/end/gained EHB and synchronization snapshot, the approved English 5×5 manifest, and the exact corrected 402 counter units across 150 team/tile cells. The reviewed public manifest SHA-256 is `e5297b20fc5e4a842b6a1e5ab378128cbe1c2bad16033fc875c54607c0d49438`. The import applies the fixed eligibility and deterministic contribution rules in `PRODUCT_REQUIREMENTS.md` and `DATA_MODEL.md`, writes the event and retained history atomically, and records the source identifiers/hashes, actor, time, and result in audit history. Apply requires exact event-name confirmation and validates the active SuperAdmin actor inside the locked serializable transaction.

**Permissions and history:** The source-to-website-account mapping is never inferred and is not versioned in Git; its approved secondary direction is primary `Ezzi → Also Ezzi` and primary `wolles → w olles`. Board standings remain official; WOM EHB/activity ranking remains separate and uses the complete frozen source snapshot without normal refresh. Reconstructed contributions have null item/evidence associations, appear in public Recent Drops as approved historical rows, and never fabricate a drop or evidence asset; they use only the exact historical disclosure specified by the product contract. Existing event history, snapshots, audit records, and source identity are retained.

**Failure and recovery:** Preflight fails closed for missing or conflicting source data, invalid counters, unresolved account identity, non-deterministic attribution, a non-matching event, an attempted Live transition, or an unsafe persistence shape. Apply is transactional; any failure rolls back without leaving a partial event, roster, board, contribution, or audit state. A retry is a no-op only for an exact matching import hash; a different hash is not silently merged or overwritten. The operator must identify and correct the reported source/private input or retained-record conflict, then rerun preflight. No production mutation is part of implementation or rehearsal.

**Acceptance outcome:** The dormant importer can be deployed independently, can reproduce the approved archived record deterministically, exposes no private roster data through versioned metadata or public projections, and cannot turn a historical import into live synchronization or an unreviewed production mutation.

## 10. Unresolved and deferred decisions

- **F-04 — resolved:** event identity and display timezone remain read-only once an event enters Live. The separately authorized Live event-end correction is a Schedule capability and does not reopen identity editing.
- **F-06 — resolved:** the source-controlled `/HowTo` guide is implemented as five anchor-linked steps covering event discovery, signup, board progress, evidence submission, and review tracking. The global Rules page remains a separate functional boundary; no in-application editor is added for How To content.
- External feedback remains deferred to the community Discord path; no version-one application feedback form is added.
- Wise Old Man availability, cache completeness, and integration configuration remain non-blocking for event lifecycle; detailed API/operational limits stay in the technical and data authorities.
- **Linked-resubmission Admin Review clarification — resolved:** a report that linked resubmission was absent was a reader/reviewer misinterpretation; source inspection found no query exclusion. Existing linked-resubmission behavior and tests remain protected, and no query change or additional manual release gate is required.

## 11. Whole-workflow acceptance boundary

The version-one functional foundation described here defines the following outcomes, subject to final whole-application and release regression. This section does not approve an unapproved UI page or claim that the application has passed final regression.

- An Admin can create a private event, configure identity/schedule/signup, reach readiness, and open signup without partial state.
- A participant can authenticate, complete signup, receive deterministic confirmed/waiting status, manage linked accounts, and recover access without duplicate identity or event ownership.
- Admins can correct pre-draft participants, handle post-draft vacancies and Captain roles, run the draft, and preserve roster/pick history.
- Admins can build, approve, separately publish, and correct a board while catalogue changes remain live only until an approval snapshot is created.
- Scheduled start, Live account/focus/evidence operation, submission grace, review, reversal, finalization, unfinalization, archive, and cancellation preserve authoritative time, privacy, concurrency, and historical records.
- Captain/co-captain, Super Admin, ordinary Admin, participant, emergency credential, and public projections each receive only their intended scope.
- Notifications resolve to valid destinations and remain supplementary to the underlying event, roster, evidence, account, or lifecycle record.
- Development reset provides explicit, bounded manual-acceptance journeys; production does not inherit the fixture exemption.
- F-06 is resolved by the implemented source-controlled How To guide. F-04 is resolved by retaining Live identity/timezone read-only behavior; F-05 is resolved by documentation reconciliation. Wise Old Man remains optional/supplementary, manual signup EHB remains authoritative, and no lifecycle action depends on it.

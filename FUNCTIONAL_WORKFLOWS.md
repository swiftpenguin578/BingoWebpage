# Functional Workflow Specification

**Status:** Planning Pass 2 selected capability contracts recorded; unresolved items remain explicitly marked

**Started:** 2026-07-24

**Last updated:** 2026-07-25

**Related documents:** `PRODUCT_REQUIREMENTS.md`, `DATA_MODEL.md`, `TECHNICAL_ARCHITECTURE.md`, `IMPLEMENTATION_ROADMAP.md`, `UI_OVERHAUL_ROADMAP.md`

## 1. Purpose

This document connects end-to-end user journeys to precise functional capabilities. It exists to prevent functionality from being lost when roles, workflows, and UI are revised.

Planning Pass 2 may keep, change, replace, or remove implemented behavior and may add new behavior. Existing implementation is evidence about the current product, not a constraint on the target workflow.

It does not replace the product requirements, data model, technical architecture, or UI roadmap:

- This document defines who is trying to achieve what, in which order, and which capabilities the workflow requires.
- `PRODUCT_REQUIREMENTS.md` remains authoritative for approved product behavior and scope.
- `DATA_MODEL.md` remains authoritative for entities, invariants, calculations, and historical semantics.
- `TECHNICAL_ARCHITECTURE.md` remains authoritative for implementation boundaries and operational design.
- `UI_OVERHAUL_ROADMAP.md` remains authoritative for visual and interaction rules after the target workflow is approved.

When a workflow decision changes one of those sources, update the affected source explicitly. Do not treat a proposal in this document as approved behavior.

## 2. Planning method

### 2.1 Evidence labels

Every material statement uses one of these labels:

- **Verified current:** demonstrated by current source code, tests, or recorded verification.
- **Required current:** stated by an existing source-of-truth document but not necessarily verified in the current implementation.
- **Proposed target:** a Planning Pass 2 recommendation awaiting user approval.
- **Approved target:** explicitly approved during Planning Pass 2 and ready to propagate to the other source documents.
- **Open decision:** a question that must be resolved before the capability is approved.

### 2.2 Capability status

Each capability is classified as one of:

- **Keep:** current behavior remains the target.
- **Change:** current behavior exists but the target differs.
- **New:** target behavior does not exist.
- **Remove:** current behavior should no longer exist.
- **Defer:** recognized but excluded from the active functional-expansion scope.
- **Undecided:** awaiting workflow review.

### 2.3 Completeness sources

The capability register is assembled from:

1. Product, data, architecture, implementation, and UI requirements.
2. Razor routes and page handlers.
3. Application services and domain state transitions.
4. Persistence, migrations, scheduled workers, and external integrations.
5. Domain, application, integration, and browser tests.
6. Existing behavior observed in seeded scenarios.
7. New functionality explicitly proposed by the user.

A page is not a capability. One page may expose several capabilities, and scheduled or automatic behavior may have no page.

### 2.4 Capability contract

Every approved capability must define:

- Stable ID and name.
- Actor and user goal.
- Preconditions and allowed event phases.
- Inputs and authoritative source of each input.
- Normal behavior and resulting state.
- Information visibility and privacy.
- Authorization and scope.
- Validation and prohibited behavior.
- Audit requirements.
- Concurrency and idempotency behavior.
- Failure, retry, reversal, and recovery behavior.
- Cross-role handoff or notification.
- Current implementation evidence.
- Acceptance scenarios and required test levels.
- Affected product, data, architecture, and UI sections.

Routine audit history and a required written explanation are separate controls. Ordinary configuration changes record the actor, time, and before/after values automatically but do not ask the admin to justify the action. A typed reason is reserved for exceptional actions that alter or override competitive or historical truth, such as ending an event early, overriding an unresolved finalization blocker, or reversing approved evidence.

## 3. Admin responsibility catalogue

The technical `Admin` role currently covers several operational responsibilities. Planning reviews them separately even if they continue to share one authorization role:

1. Installation and security administration.
2. Event organization and scheduling.
3. Signup and participant administration.
4. Catalogue and board design.
5. Team and draft operation.
6. Participant identity and access administration.
7. Live-event operation.
8. Evidence review.
9. External synchronization administration.
10. Finalization and historical-record administration.
11. Audit, correction, and recovery.

## 4. Workflow WF-01 — Create event through first participant access

### 4.1 Outcome

An administrator creates a private event, completes the configuration required to accept signups, opens signups deliberately or by an approved schedule, and a participant submits or links a signup through an authenticated website account.

### 4.2 Workflow boundary

**Starts:** An enabled administrator decides to prepare a new bingo event.

**Ends:** The first valid participant has an event signup linked to the correct authenticated identity, or has received the approved unauthenticated/fallback outcome.

**Does not include yet:** participant-cap management after signup, waiting-list operations, draft execution, board publication, team membership, live-event submissions, or account swapping. Those appear in later workflows.

### 4.3 Capability register

| ID | Capability | Responsibility | Current evidence | Initial status |
| --- | --- | --- | --- | --- |
| `ADM-EVENT-01` | Create a private event draft | Event organizer | Implemented through the event creation page and `BingoEvent` constructor | Change — approved |
| `ADM-EVENT-02` | Configure event identity and public description | Event organizer | Required by PRD 7.3; partly handled during current creation | Change — approved |
| `ADM-EVENT-03` | Configure schedule and timezone | Event organizer | Implemented during current creation and event management | Change — approved |
| `ADM-SIGNUP-01` | Configure standard signup fields and custom questions | Signup administrator | Implemented during creation and on the questions page | Change — approved |
| `ADM-SIGNUP-02` | Configure capacity, waiting list, signup code, and withdrawal behavior | Signup administrator | Implemented during creation and event management | Change — approved |
| `ADM-IDENTITY-01` | Configure hybrid account creation and Discord-link readiness | Identity administrator | New; backlog defines direction but not complete workflow | New — approved |
| `ADM-SIGNUP-03` | Check whether signup is ready to open | Signup administrator | Partial validation exists; the target readiness contract is approved below | Change — approved |
| `ADM-SIGNUP-04` | Open signups manually or by schedule | Signup administrator/system | Implemented; manual-opening target rules are in the backlog | Change — approved |
| `ADM-PARTICIPANT-01` | Manage the pre-draft participant workspace | Signup/participant administrator | Current actions are split across event management, participant detail, and draft pages | Change — approved |
| `ADM-PARTICIPANT-02` | Withdraw and optionally replace a participant after draft start | Participant/team administrator | Current draft lock stops automatic promotion; complete replacement workflow is new | New — approved |
| `ADM-CAPTAIN-01` | Assign or revoke captain/co-captain after draft start | Team/access administrator | Current finalization provisions generated captain accounts | Change — approved |
| `ADM-DRAFT-01` | Set up, run, and finalize the website draft | Draft administrator | Implemented with manually configured team count/target size and generated captain credentials | Change — approved |
| `ADM-BOARD-01` | Build, approve, and publish the competitive board | Board administrator | Implemented board builder currently permits manual EHB overrides for catalogue-backed tiles | Change — approved |
| `ADM-RULES-01` | Maintain the permanent public Rules page | Enabled administrator | Event has free-text public rules; submission guidance is currently distributed across pages/tile instructions | Change — approved |
| `PUB-HOWTO-01` | Read permanent public how-to pages | Any visitor | Submission guidance is currently distributed across pages/tile instructions | Change — approved; content remains source-controlled |
| `SYS-EVENT-START-01` | Attempt scheduled event start through readiness gates | System/event administrator | Event timestamps exist; complete delayed-start behavior needs an explicit target contract | Change — approved |
| `ADM-EVENT-END-01` | End live play and administer the submission grace period | Event administrator/system | Scheduled end, cutoff, reopening, and final review exist; the target transition contract is approved below | Change — approved |
| `ADM-EVENT-CANCEL-01` | Cancel a populated event that will not take place | Event administrator | No preserved cancelled-event state exists | New — approved |
| `ADM-EVENT-ARCHIVE-01` | Move finalized results from current event to public history | Event administrator | Archive exists but lacks a complete workflow contract | Change — approved |
| `SYS-CURRENT-EVENT-01` | Keep one unambiguous production current event while preserving development scenarios | System/event administrator | Public and review pages currently tolerate several seeded lifecycle states | Change — approved |
| `ADM-REVIEW-01` | Review, correct, approve, reject, or reverse evidence | Evidence administrator | Implemented with additional request-changes and duplicate actions | Change — approved |
| `ADM-FINALIZE-01` | Resolve final-review blockers, finalize, and unfinalize results | Event administrator | Implemented with generated-captain expiry behavior that must change for website-account roles | Change — approved |
| `PART-SIGNUP-01` | Submit a valid event signup | Participant | Implemented without an authenticated participant account | Change — approved |
| `PART-IDENTITY-01` | Create an account, sign in with Discord or password, and recover/relink access | Participant | New | New — approved |
| `ADM-IDENTITY-02` | Resolve duplicate, missing, or incorrect identity links | Identity administrator | New | New — approved |
| `PART-SIGNUP-02` | View confirmation, manage signup, and withdraw | Participant | Implemented through authenticated event-participant ownership | Change — approved |
| `PART-PROFILE-01` | Complete Discord-backed account creation with public username/password | Participant | New | New — approved |
| `PART-ACCOUNT-01` | Manage My accounts and active-character swaps | Participant/captain/admin | New | New — approved |
| `PART-LIVE-01` | Use the team board, swap active account, and manage own evidence through cutoff | Participant | Participant evidence submission is not implemented | New — approved |
| `PART-HISTORY-01` | Read personal and public event history after archive | Participant | Public archived boards exist; private participant history lacks a complete target contract | Change — approved |
| `PUB-SIGNUP-01` | View the unlisted public signup table | Visitor with the exact link | Existing requirements hide the participant list during signup | Change — approved |
| `PUB-FEEDBACK-01` | Report evidence, bugs, or general feedback | Public visitor | No dedicated public reporting route | Defer — use the community Discord feedback channel |
| `CAPTAIN-WORKSPACE-01` | Use the draft-information, team-focus, and team-submission workspace | Captain/co-captain | Current captain access uses generated credentials and has no expanded signup/focus workflow | Change — approved |
| `SUPERADMIN-01` | Hold global owner powers distinct from ordinary event administration | Super Admin | Current admin role is undifferentiated | New — approved |
| `ADM-ACCOUNT-01` | Disable and restore normal website-account access | Admin/Super Admin | Account disable/enable exists around the legacy role model | Change — approved |
| `ADM-CATALOGUE-01` | Maintain the global OSRS catalogue safely | Admin/Super Admin | Admin CRUD/deactivation exists; deletion and board-refresh boundaries are incomplete | Change — approved |
| `ADM-CATALOGUE-IMPORT-01` | Preview and apply a bulk catalogue import | Super Admin | Current Wiki dry-run/apply route is available to every Admin | Change — approved |
| `ADM-AUDIT-01` | Search immutable global/event audit history | Admin | Current page filters actor/action and returns only the newest 250 records | Change — approved |
| `ADM-ACCOUNT-OVERVIEW-01` | Inspect website-account, identity, and event-role state | Admin/Super Admin | Current account page is based on legacy standalone roles | Change — approved |
| `ADM-INBOX-01` | Separate personal notifications from unresolved admin actions | Admin | Shared notification shell exists; operational-action semantics are incomplete | Change — approved |

### 4.4 Cross-role handoffs

1. Event organizer creates the private draft.
2. Organizer or another administrator completes signup configuration.
3. The system reports readiness blockers and warnings.
4. An administrator opens signups, or the scheduler opens them at the approved time.
5. A participant authenticates at the approved point in the journey and submits a signup.
6. The system links the authenticated website account to exactly the permitted event-participant record.
7. Identity conflicts enter an explicit participant recovery or admin-resolution workflow.

## 5. Trial capability ADM-EVENT-01 — Create a private event draft

### 5.1 Actor and goal

**Actor:** Enabled administrator.

**Goal:** Establish a private event workspace that can be configured safely over more than one session without exposing incomplete information publicly.

### 5.2 Current behavior

**Verified current**

- Only an authenticated account satisfying the administrator policy can use the creation handler.
- The current UI is a five-step client-side form covering event details, schedule and capacity, signup settings/questions, planning estimates, and review.
- The server does not save the event until the complete form passes validation.
- Name, description, timezone, four schedule timestamps, and participant capacity are required at creation.
- The server generates a unique slug from the event name, adding a numeric suffix when necessary.
- The event is created in `Draft`.
- Signup options and custom questions are created in the same database save.
- Creation records an `event.created` audit entry.
- Successful creation redirects to the event-management page.

**Required current**

- Event setup must support work across multiple sessions.
- The event remains private until signups are opened.
- Opening signups does not publish participant lists, teams, draft state, the board, or captain configuration.
- Board completion is not required before signups open.

### 5.3 Approved target contract

**Approved target — classification:** Change

Create a minimal private draft first, then configure it through separately saved workflow sections.

**Approved required inputs**

- Event name.
- Event timezone.

**Approved optional inputs at creation**

- Public description.
- Schedule, signup configuration, signup questions, capacity, and planning estimates may remain available during the creation journey, but none is required to save the initial private draft.

**Approved generated values**

- Stable event ID.
- Unique URL identifier derived from the name.
- Creator and creation time.
- `Draft` lifecycle state.
- Safe default planning values where a value is technically required before its dedicated configuration step.

**Approved result**

- One private event draft exists.
- Nothing about the event is public.
- No signup is accepted.
- No participant, team, draft, board, captain account, or evidence record is created.
- The administrator enters an event setup workspace with explicit incomplete/ready state.
- Another authorized administrator can continue configuration.

### 5.4 Approved rules and protections

- Creation and subsequent setup saves are separate capabilities.
- A failure before the creation transaction commits leaves no partial event.
- Duplicate event names are allowed; the URL identifier remains unique.
- The generated URL identifier may be edited until the event first becomes public. It is stable afterward.
- Every enabled administrator may create and continue configuring every event. Audit history identifies the actor for each change.
- Creation records actor, time, event ID, name, and generated URL identifier without secrets.
- Creating a draft does not schedule or imply public signup access until the schedule capability is saved and the signup-opening gate is satisfied.
- Current board, draft, evidence, privacy, finalization, and audit protections remain unaffected.
- Page layout, wizard presentation, and autosave behavior remain open for the later UI pass.

### 5.5 Approved failure and recovery behavior

- Blank or invalid name: remain on creation with field-specific feedback.
- Unknown or invalid timezone: remain on creation with field-specific feedback.
- URL collision: generate or confirm a unique identifier without overwriting another event.
- Database or audit transaction failure: create neither a usable event nor a misleading success result.
- Authorization lost before submission: reject creation and create no event.
- Accidental or experimental event: allow an explicitly confirmed discard when the event has no participants, teams, event-scoped accounts, or evidence.
- Board content, planning configuration, signup questions, and other event-owned setup data do not block discard. They are removed with the discarded event.
- Discard preserves a minimal audit/tombstone record and reserves the old URL identifier; the event disappears from active administration and public listings.
- Once protected participant, team, account, or evidence data exists, discard is blocked. The later cancellation/archive workflow must preserve that history.

### 5.6 Approved acceptance scenarios

1. An enabled administrator creates a private draft using only the approved minimum inputs.
2. A public visitor cannot discover or open the draft.
3. Another enabled administrator can continue its setup.
4. An invalid name or timezone creates no event and identifies the field to correct.
5. Two events may share a display name but never an identical URL identifier.
6. Creating the draft alone cannot accept signups or publish competitive information.
7. Creation produces the required audit record.
8. An administrator may optionally enter schedule, signup questions, and planning values during creation without those values becoming requirements for the initial save.
9. An experimental event containing board work but no participants, teams, accounts, or evidence can be discarded after confirmation.
10. An event with any protected participant, team, account, or evidence record cannot be discarded.

### 5.7 Traceability

**Requirements**

- `PRODUCT_REQUIREMENTS.md` sections 5.3, 7, and 7.3.
- `IMPLEMENTATION_ROADMAP.md` Milestones 3 and 8A.

**Current implementation**

- `src/Bingo.Web/Pages/Admin/Events/Create.cshtml`
- `src/Bingo.Web/Pages/Admin/Events/Create.cshtml.cs`
- `src/Bingo.Domain/Events/BingoEvent.cs`
- `src/Bingo.Infrastructure/Persistence/Configurations/BingoEventConfiguration.cs`

**Affected sources**

- `PRODUCT_REQUIREMENTS.md`: event-creation inputs and multi-session setup.
- `DATA_MODEL.md`: only if incomplete draft defaults or cancellation semantics change persisted invariants.
- `TECHNICAL_ARCHITECTURE.md`: transaction/audit boundary if event and audit cannot currently commit atomically.
- `IMPLEMENTATION_ROADMAP.md`: acceptance and implementation slice.
- `UI_OVERHAUL_ROADMAP.md`: later event-creation pass.

## 6. Resolved decision block DB-ADM-EVENT-01

Approved by the user on 2026-07-24:

1. Only event name and timezone are required for initial creation. Other setup information and custom questions may still be entered immediately but are optional.
2. Generate the URL identifier from the name, allow editing until first public exposure, then keep it stable.
3. Every enabled administrator can create and continue configuring every event.
4. Duplicate event display names are allowed; the URL identifier is unique.
5. An event may be discarded when it has no participants, teams, event-scoped accounts, or evidence. Experimental board content and other event-owned setup data do not prevent discard.

## 7. Approval gate for the trial

`ADM-EVENT-01` is **Approved target**. Its behavior has been reconciled into the affected planning sources. Implementation remains part of the later Milestone 8A delivery sequence.

## 8. Capability ADM-EVENT-02 — Configure event identity and public description

### 8.1 Actor and goal

**Actor:** Enabled administrator.

**Goal:** Configure the event information admins use to recognize it and the information participants need before signup, without making decorative or later-phase configuration block draft creation.

### 8.2 Current behavior

**Verified current**

- Event name, description, and timezone are entered only during the current all-in-one creation form.
- Description is currently required at creation.
- Timezone defaults to `Europe/Copenhagen` but is entered through a raw text field.
- The slug is generated from the name and cannot be edited through the current admin UI.
- The current management page does not edit name, description, slug, timezone, or banner.
- The current persisted event/domain model has no implemented event-banner relationship even though the planning data model already lists an optional banner asset.

### 8.3 Approved target contract

**Approved target — classification:** Change

**Identity fields**

- Event name is required to save a draft.
- Public description/signup introduction is optional while private and required before signup opens.
- Event banner/artwork is always optional, may be supplied during creation, and may be added, replaced, or removed afterward.
- The slug is generated from the name, may be edited until first public exposure, and is immutable afterward.
- Timezone is required but defaults to `Europe/Copenhagen`, satisfying the initial requirement without admin input.

**Editing**

- Every enabled administrator may edit event identity.
- The display name may change after publication with an audit entry; the stable slug does not change.
- Banner absence never creates a readiness blocker or warning.
- Banner changes do not alter historical competitive data.
- The timezone is freely editable while the event is private.
- After signup opens, changing timezone requires confirmation that previews how every configured timestamp will display in the new timezone.
- After the event starts, a timezone correction also requires an audit reason.
- Changing timezone never changes stored UTC instants. Changing the actual schedule is a separate capability.

**Timezone selection**

- Admins choose from supported timezones rather than entering an arbitrary raw identifier.
- `Europe/Copenhagen` is preselected and shown first.
- The interface shows a friendly place/offset label while persisting the canonical timezone ID.
- An existing stored timezone that remains valid must still be selectable when editing an event.

### 8.4 Approved readiness model

Use separate readiness levels rather than treating every configuration value as an event-creation requirement:

1. **Draft saved:** name and timezone are valid.
2. **Signup ready:** every field required to accept and manage signups is configured.
3. **Draft ready:** participant/team and draft prerequisites are configured.
4. **Event ready:** teams and board can be published and the live event can start safely.
5. **Finalization ready:** every competitive blocker is resolved.

Each readiness level distinguishes:

- **Blocker:** the transition cannot proceed.
- **Warning:** the transition may proceed after explicit acknowledgement.
- **Later task:** intentionally irrelevant to the current transition.

For event identity:

- Invalid or missing name/timezone blocks saving the draft.
- Missing public description blocks opening signup.
- Missing banner is always a later optional enhancement and never blocks a transition.
- Board, draft, evidence, and finalization settings do not affect identity readiness.

### 8.5 Authorization, audit, and failure behavior

- Server authorization remains authoritative for every identity change.
- Audit records creation and changes to name, slug, description, banner reference, and timezone.
- Audit snapshots do not contain uploaded image bytes.
- Slug uniqueness is enforced server-side; a collision cannot overwrite or alias another event.
- A rejected slug or timezone change leaves the previous value active.
- Banner upload failure leaves the previous banner active and provides an accurate retryable error.
- A timezone confirmation uses fresh event data; if another admin changes the schedule first, the stale confirmation is rejected and regenerated.

### 8.6 Acceptance scenarios

1. A new draft defaults to Copenhagen without requiring the admin to type a timezone identifier.
2. An admin can select another supported timezone.
3. A draft can be saved without description or banner.
4. Signup cannot open without a public description.
5. Signup can open without a banner.
6. An admin can add, replace, or remove the banner before or after initial creation.
7. Changing the public display name after signup opens leaves the public URL unchanged.
8. Changing timezone previews new local displays but preserves the stored UTC schedule.
9. A timezone correction after event start requires a reason and is audited.
10. Invalid slug, timezone, or banner input leaves the previous valid identity active.

### 8.7 Resolved decision block DB-ADM-EVENT-02

Approved by the user on 2026-07-24:

1. Use the five separate readiness levels and blocker/warning/later-task classifications.
2. Require the public description before signup opens, but not before saving the draft.
3. Allow changing the display name after publication while keeping the URL stable.
4. Banner/artwork is always optional, is available during creation, and remains editable afterward.
5. Default timezone to `Europe/Copenhagen`; replace the target raw text field with a supported-timezone selector. Preserve the previously approved confirmation and audit protections for later changes.

### 8.8 Approved media source

Event banners use managed decorative-image upload through the shared asset-storage workflow. Arbitrary external image URLs are not supported. When no banner exists, use the shared event visual fallback; absence of artwork must not make the page appear broken.

## 9. Capability ADM-EVENT-03 — Configure schedule and timezone-dependent dates

### 9.1 Actor and goal

**Actor:** Enabled administrator.

**Goal:** Configure and safely change the dates that control signup, draft planning, the competitive event window, and evidence submission without surprising participants or silently moving authoritative instants.

### 9.2 Current behavior and conflict

**Verified current**

- The current creation form requires signup opening, signup closing, event start, and event end.
- Submission cutoff is automatically created as 30 minutes after event end.
- The current admin management page can change the signup window.
- Manual opening records actual opening at now and retains a valid explicit future close. When none is valid, it previews future draft time when it is no later than event start, otherwise event start; no proposal is persisted before all checks pass.
- Draft time is described in product requirements but is not part of the implemented event schedule.

**Required current**

- The roadmap backlog says manual opening should close at the earlier of three months after opening and event start.
- Starting the draft closes and locks signup.

The verified implementation and roadmap target conflict. The approved target below resolves the conflict in favor of the event-start cap while preserving an explicit valid closing time.

### 9.3 Approved target contract

**Approved target — classification:** Change

**Draft save**

- Schedule is optional when saving the initial private event draft.
- Any schedule values supplied early are validated and saved independently.

**Signup readiness**

- Event start and event end are required.
- Signup closing time is required, but it may be system-supplied by the manual-opening default.
- Event end must be after event start.
- Signup closing must be after opening and no later than event start.
- A scheduled opening time is required only when the admin chooses scheduled opening.
- Draft time is optional.

**Opening signup**

- Support scheduled and manual opening.
- Scheduled opening uses the configured opening instant.
- Manual opening records the opening instant as now at the system's standard precision.
- If a valid explicit future closing time no later than event start exists, manual opening preserves it.
- If closing is absent or expired, manual opening supplies future draft time when it is no later than event start, otherwise event start, and shows that value before confirmation.
- The proposed closing time is applied only inside the final successful lifecycle transaction.

**Event and submission window**

- Event start and end define the authoritative in-game eligibility window.
- Submission cutoff defaults to 30 minutes after event end.
- Admins may edit the cutoff, but it cannot be before event end.
- Reopening submissions does not extend the event eligibility window.

**Draft time**

- Draft time is optional and informational.
- It may be shown to admins and participants as planning information.
- Reaching draft time never starts the draft automatically.
- An authorized admin explicitly starts the draft.

### 9.4 Schedule changes

- All schedule instants are stored in UTC and displayed in the event timezone.
- Changing timezone alone does not change any schedule instant.
- Schedule changes after signup opens require confirmation showing the old and new participant-facing times and create an audit entry.
- Changes after the affected time has passed require an audit reason and must follow the applicable lifecycle rule rather than rewriting history.
- Starting the draft closes and locks signup even if the configured signup closing time has not arrived.
- Background transitions and manual actions use the same domain rules and cannot produce an impossible state.

### 9.5 Acceptance scenarios

1. A private draft can be saved with no schedule.
2. Signup readiness blocks when event start, event end, or a valid closing time cannot be established.
3. Scheduled signup opens at its configured instant.
4. Manual opening preserves a valid explicit closing time.
5. Manual opening with no valid closing time proposes future draft time or event start.
6. The proposal is never persisted on a failed attempt.
7. Submission cutoff defaults to 30 minutes after event end and cannot be moved earlier than event end.
8. Draft time is displayed when supplied but never starts the draft.
9. Starting the draft closes and locks signup.
10. A timezone change changes local display only, while an explicit schedule edit changes authoritative instants.

### 9.6 Resolved decision block DB-ADM-EVENT-03

Approved by the user on 2026-07-24:

1. Use managed upload only for the optional event banner.
2. Support scheduled and manual signup opening; manual opening records the actual opening time.
3. Require signup closing and event start/end before signup opens, with a system-supplied closing time allowed.
4. Default submission cutoff to 30 minutes after event end, allow editing, and never allow it before event end.
5. Keep draft time optional and informational; an admin always starts the draft explicitly.

## 10. Cross-cutting identity impact register

Discord participant identity and the replacement of generated captain credentials affect admin workflows as well as participant workflows. The following admin capabilities must be revisited when the identity contract is approved:

| Admin area | Current behavior to revisit | Likely target impact |
| --- | --- | --- |
| Signup configuration/readiness | Discord name is an optional text field; participant accounts are absent | Require authentication before normal signup while allowing explicit external/imported roster records without inferred ownership |
| Participant management | Admin corrects, creates, and transfers participant ownership | Authenticated ownership is authoritative; no private edit-link fallback remains |
| Identity recovery | No participant account-link conflict workflow | Add link status, duplicate/mistaken-link resolution, unlink/relink protections, and audit |
| Captain/co-captain assignment | Finalized roles provision generated event accounts and passwords | Role assignment should normally grant team-scoped permissions to the participant's website account |
| Account administration | Admin resets/disables temporary captain credentials | Reframe around identity access, role revocation, and explicitly retained emergency fallback accounts |
| Evidence submission/review | Team is derived from a captain account; participants cannot submit | Derive participant scope from authenticated event identity/team membership while preserving captain team-wide and admin event-wide authority |
| Audit/security | Actions identify the generated captain/admin account | Preserve the authenticated website-account actor, authentication method, effective event role, and admin overrides without exposing OAuth secrets |

The participant identity workflow decides the normal signup-edit and recovery path. Until then:

- Do not treat the current private edit link as approved target behavior.
- Do not remove it prematurely if it is still needed for migration or emergency recovery.
- Do not design new admin pages around generated captain passwords as the long-term primary model.
- Flag every affected capability for a focused revisit after `PART-IDENTITY-01` and `ADM-IDENTITY-02` are approved.

## 11. Identity capability group — Discord person, event participation, and OSRS characters

### 11.1 Approved conceptual model

Keep four concepts separate:

1. **Website account:** the global person and authoritative actor for website actions.
2. **Authentication methods:** that account's current Discord link and its public-username/password credential.
3. **Event participant:** that website account's signup or roster participation in one event.
4. **OSRS character:** a playable account name that may be associated with one or more website accounts globally but is assigned to only one participant within a particular event.

Approved relationships:

- Initial account creation starts with Discord authentication, then requires public-profile and password onboarding.
- After creation, Discord login and public-username/password login authenticate the same website account.
- Discord-server membership is not required.
- One website account may own at most one event-participant record in a particular event.
- The same website account may participate in multiple different events.
- A website account may link multiple OSRS characters generally and assign multiple characters to its participant record for one event.
- The same OSRS character may be linked to multiple website accounts globally because borrowing and swapping are trust-based.
- Within one event, an OSRS character may be assigned to exactly one event-participant record. A character assigned to Christopher in Event 1 may be assigned to Alexander in Event 2, but it cannot appear on both participant records in the same event.
- A global website-account/character link must not imply exclusive real-world ownership.
- Captain/co-captain authority attaches to the website account's participant role for a specific event and team, never to an OSRS character name or current Discord ID.

### 11.2 Approved normal signup and editing path

- A first-time participant creates the website account through Discord and completes public-username/password onboarding before submitting the public signup form. A returning participant may authenticate with Discord or public username/password.
- The created event-participant record is linked to that authenticated website account.
- The OSRS-character selector shows characters already linked to the authenticated website account.
- When a character is missing, the signup form links to **My accounts** so the participant can add the trusted or borrowed character before returning to signup. The final Slice 4 interaction must preserve the participant's work or make the return path explicit; it does not create a character inline through an account-name text field.
- A character already assigned to another participant in the same event is unavailable. The server rejects a conflicting assignment even if two signups or edits race.
- A new normal signup does not receive a private edit link.
- The participant may edit their own signup only while the event is `SIGNUP_OPEN`.
- Closing signup immediately removes participant editing authority, even if the participant still has an authenticated session.
- Admin correction/removal authority remains separate and audited.
- The normal path must reject a second participant/signup for the same website account and event without overwriting the first.

Withdrawal remains a separate confirmed action after field editing closes; the complete cancellation, rejoin, and restoration behavior is defined in section 14.

### 11.3 Approved captain access

- Assigning captain or co-captain changes the participant's event/team role and permissions.
- The participant continues using the same website account and may authenticate through either normal login method.
- Changing the OSRS character name or active character does not move or remove captain authority.
- Removing or changing the role updates effective authorization without creating a new normal login.
- Temporary captain credentials remain available only as an explicitly enabled emergency fallback and are disabled by default.

### 11.4 External and pre-formed teams

External or pre-formed team members are not required to use the public signup workflow merely to exist on a roster.

**Approved target**

- An admin creates the external team and its event-participant roster records. Slice 5 may populate one selected pre-formed team from CSV: each data row is one member, column one is the primary account, column two its EHB, and later columns are additional accounts without EHB for that member. CSV never imports ordinary draft-pool signup, ownership, Discord, Admin-private fields, signup answers, or Captain/Co-captain roles.
- External roster records contain the OSRS characters needed for visible rosters and Wise Old Man tracking, but they do not require individual website-account ownership.
- One or more explicitly enabled emergency captain credentials provide team-scoped submission access. Each credential is individually auditable and is not linked to an OSRS character or participant record.
- External members without website accounts rely on those captains for submissions.
- External teams do not receive participant-only shared focus/highlight controls through roster names alone and may continue coordinating focus manually outside the application.
- A later normal website-account relationship is created only through an explicit authenticated/admin workflow; Slice 2 does not add claim invitations or infer it from roster data.

This proposal preserves external-team operation without requiring another clan's full roster to sign up publicly.

### 11.5 Identity recovery and security

**Approved target**

- Never auto-link records by Discord display name or OSRS character name.
- Admins can inspect linked/unlinked status without seeing OAuth credentials.
- A normal account may unlink its current Discord association at any time after fresh password confirmation, including while participating in an event or holding a global/event role. The required password remains a valid login method, so unlinking never makes the account credentialless.
- An unlinked account may later attach any Discord ID not already used by another website account after fresh password confirmation and OAuth. A linked account may use the same flow as a one-step **Change linked Discord** action.
- Link, unlink, and replace each update the association atomically, invalidate other authenticated sessions, and record automatic actor/time/before-after history without requiring a typed reason. They do not change event participants, signups, roles, My accounts, evidence, or history.
- A Super Admin may also unlink, but the UI warns that losing the remaining password would require operator recovery.
- An admin may transfer one event-participant record to another website account only when it was linked to the wrong or duplicate global account.
- The destination account cannot already own a participant record in that event. Transfer revokes the previous account's event access immediately while preserving signup order/status, event-character assignments, team membership, evidence, and history.
- Transfer is strongly confirmed and automatically records old/new identity references, actor, and time, but requires no typed reason.
- This event-scoped transfer does not merge the two global website accounts or infer ownership from their OSRS-character links.
- The private edit-token path was removed by Slice 4 compatibility cleanup; no claim-link system replaces it.

### 11.6 OSRS character assignment consequences

Global sharing and event assignment are separate:

- Several website accounts may retain the same OSRS character in their trusted character lists.
- Each event has exactly one participant assignment for that character, which supplies the participant/team scope for evidence and Wise Old Man activity in that event.
- A later event may assign the character to a different participant without changing the earlier event.
- Exactly one registered character is active and drop-eligible for a participant at any instant.
- Changing the active character is an explicit swap recorded in append-only history.
- How participants release or replace character assignments while signup is open.
- How administrators correct a mistaken assignment after signup closes without rewriting historical eligibility or activity periods.

Enforce one assignment per `(event, OSRS character)` transactionally. Do not infer real-world ownership or website authority from an OSRS character name.

### 11.7 Resolved decision block DB-IDENTITY-01

Approved by the user on 2026-07-24:

1. Initial website-account creation requires Discord authentication; after onboarding, normal signup may use either Discord or public-username/password authentication.
2. Discord-server membership is not required.
3. Authenticated participants edit their signup through their account only while signup is open; new normal signups do not receive private edit links.
4. One website account owns at most one participant record per event but may participate in several events.
5. A website account may link/register multiple OSRS characters, and the same OSRS character may be linked to different website accounts, including for borrowing or swapping during an event.
6. Captain/co-captain is an event-specific role on the participant's website account, not on its current Discord ID or an OSRS character name.
7. Emergency temporary captain accounts remain available but disabled by default.
8. An OSRS character may be linked to several website accounts globally, but it may be assigned to only one event participant in the same event. It may be assigned to a different person in a later event.

### 11.8 Resolved decision block DB-IDENTITY-02

Approved by the user on 2026-07-24:

1. External/pre-formed roster members do not need public signup or individual website accounts. One or more explicitly enabled, team-scoped emergency captain credentials provide submission access without claiming roster records.
2. Signup fields become read-only after signup closes, but a participant may withdraw through a separate confirmed action until the draft starts. Normal waiting-list promotion follows.
3. A participant may register several OSRS characters, but exactly one is active and drop-eligible at a time. Changing it uses the account-swap workflow and append-only history.
4. Private edit links are removed; authenticated ownership and Admin ownership transfer are the approved access paths.

### 11.9 My accounts

**Approved direction**

- Provide a global **My accounts** area for an authenticated website account.
- Show one flexible ordered list rather than mandatory sections for Main, IM, HCIM, UIM, GIM, or UGIM.
- Allow an optional personal label such as `Main`, `Alt`, `Borrowed`, or another user-entered description.
- Allow one character to be marked preferred so signup selectors can offer it first.
- Store an optional saved EHB default on each website-account/character link. This is personal default metadata rather than a global value on the shared character.
- Game mode is not required participant-entered classification. It may be shown later as non-authoritative synchronized metadata if an integration provides it.
- Removing a global link never deletes historical event assignments, evidence, eligibility, or activity.
- Correcting a misspelled linked character atomically relinks the My accounts entry and every currently participant-editable signup that uses it. A conflict in any affected event rejects the whole correction. Closed, locked, live, and historical assignments remain unchanged.
- In an event, visually separate registered characters, the one currently active/drop-eligible character, and globally linked characters that are available or already assigned elsewhere.

### 11.10 Account creation, public username, and password

**Approved direction**

- The website account is the durable identity. Public username/password is always available after onboarding; a current Discord association, when present, is an additional login method for that same account.
- Initial account creation begins with Discord authentication and then requires a website username, password, and first OSRS character. The page may recommend using the primary OSRS character as the website username, but the two values are independent and need not match.
- The UI states that the OSRS-character spelling must be correct, while the application performs no OSRS syntax, availability, ownership, Wise Old Man membership, or existence lookup. The character is created or reused, linked in My accounts, and marked preferred.
- The public username is also the password-login username. Public usernames have surrounding whitespace trimmed, retain the user's internal spelling/spacing, and are compared case-insensitively for uniqueness.
- A username collision produces an explicit validation error and asks the new user to choose another public username; it never takes over or merges the existing website account.
- Public-username uniqueness does not assert OSRS ownership and does not reserve the underlying character. A second website account may still link that character globally or use it in another event, but cannot use the same public username.
- Discord display names remain non-authoritative metadata and are not the normal public label.
- A participant may later change their public username to any valid value whose normalized form is unused by another website account. The username remains independent of linked OSRS characters.
- Changing the public username also changes the username used for password login. The confirmation UI must state that consequence.
- A successful rename reissues the current session but does not invalidate unrelated sessions solely for a name change. Other sessions receive the new name after their next authentication refresh.
- Changing the public username never changes an event's registered accounts, active account, team role, evidence, or current Discord association.
- Event-facing participant displays use registered OSRS characters rather than the website username.

### 11.11 Hybrid login, password recovery, and Discord relinking

- Login offers **Continue with Discord** and public username/password. Both resolve to one website account and cannot create parallel identities.
- Password storage, comparison, throttling, cookie/session behavior, and generic failure messages use the platform's approved modern authentication controls.
- Password creation and change require at least 10 characters, allow passphrases/printable characters, and impose neither character-class composition nor periodic expiry.
- A normal browser-session login has a 12-hour maximum ticket lifetime. **Remember me** has a 30-day absolute maximum and cannot be renewed indefinitely through activity.
- No email address is collected for version-one recovery.
- **Forgot password** is a static page with no username form. It explains that the participant must contact an administrator and cannot disclose whether an account exists.
- After verifying the requester outside the website, an enabled Admin may generate a reset link for a normal user. Only the Super Admin may generate one for an Admin. A Super Admin uses their Discord login or operator recovery rather than an ordinary admin-issued reset.
- The reset token is cryptographically random, stored only as a hash, single-use, valid for 60 minutes, and superseded by a newly generated link. The admin can neither see nor choose the new password.
- Generating and completing a reset records automatic actor, target, and time history without a typed reason. Completing it invalidates existing password-authenticated sessions but not an unrelated Discord-authenticated session.
- Account settings always expose **Unlink Discord** when linked and **Link Discord** when unlinked. A linked account may instead use **Change linked Discord** without spending time in an unlinked state.
- Every link/unlink/change requires fresh current-password verification. Linking/changing then completes Discord OAuth, and the new Discord ID must be unused by another website account.
- The mutation commits atomically, invalidates other sessions, and preserves the complete website account, event participation, roles, OSRS links, evidence, and history.

**Acceptance scenarios**

1. Initial creation through Discord requires a unique public username and password before the account can submit signup.
   Protected onboarding state expires after 15 minutes and expiry creates no partial account.
2. Discord and public-username/password login open the same account and event participation.
3. A public-username rename makes the new name the password-login username without changing event-facing OSRS-character assignments.
4. Generic password failure does not reveal whether the public username exists, and repeated attempts are throttled.
5. An Admin generates a single-use expiring reset link for a normal user without seeing or setting the password.
6. An ordinary Admin cannot generate a reset link for another Admin or the Super Admin.
7. Using or replacing a reset link invalidates it according to the approved lifecycle and completing reset invalidates prior password sessions.
8. A linked user unlinks Discord after fresh password confirmation, continues through a reissued password-authenticated session, and retains every role and event record.
9. An unlinked user links an unused Discord account, and a linked user can directly change to an unused replacement without transferring any event participant.
10. Linking/changing to a Discord ID already attached to another website account fails without changing either account.
11. A Super Admin sees the operator-recovery warning before unlinking but is not prevented from proceeding after fresh password confirmation.

## 12. PART-ACCOUNT-01 — Manage accounts and active-character swaps

### 12.1 Account questions and event roles

The standard signup form contains exactly one built-in required **Account** question. Its answer is a regular account. Additional OSRS accounts are collected only when an admin adds another custom question of type **Account**.

An Account question has an event-use setting:

- **Regular account:** May be selected as the participant's active account and may receive drop credit.
- **Alt account:** Records a named account for signup information, but can never be activated, swapped to, credited with a drop, or included in Wise Old Man standings.

The existing internal `PLAYING`/`INFORMATIONAL` values may remain persistence terminology, but participant-facing UI uses **Regular account** and **Alt account**.

If an organizer only needs to know whether someone has a support alt, they add a Yes/No question instead. No support-alt question exists by default.

The optional global My accounts label is only personal organization and does not determine an Account question's event use. All named regular and alt accounts remain subject to the rule that an OSRS character appears on only one participant in an event.

### 12.2 Signup lock

- While signup is open, a participant may change their Account-question answers. The required primary regular account automatically becomes active when the event starts; signup has no separate initial-active selector.
- Closing signup freezes the participant's set of event-assigned characters and their regular/alt roles.
- Reopening signup restores normal participant editing while it is open.
- After closing, only an audited admin correction may change the registered set or roles.
- A global My accounts link created later does not add that character to the closed event.

### 12.3 Live swap

- During the live event, the participant may swap only to a playing account already registered to them for that event.
- Alt accounts, globally linked but unregistered characters, and characters assigned to another participant are never valid swap targets.
- A normal swap request is recorded immediately at the authoritative server UTC instant and cannot be scheduled or backdated.
- The old/new evidence-eligibility boundary follows the whole-minute rule still to be approved in section 12.8.
- Swaps are unlimited and have no cooldown.
- A linked participant swaps their own active account.
- A captain/co-captain may swap for an unlinked member of their own external/pre-formed team.
- An admin may correct any participant's active-account history; a backdated correction requires an audit reason.
- Concurrent requests from the same current account cannot both succeed.

### 12.4 Evidence and UTC

- Evidence identifies both the credited event participant and the credited playing account.
- An ordinary participant is locked to themselves; a captain selects a current teammate. The application derives and displays that participant's current active playing account rather than accepting an account choice.
- The submitter does not transcribe the clan-event plugin timestamp into a separate field. The screenshot is the evidence of when the drop occurred.
- The immutable website submission time is generated by the server. It is never editable by the submitter, captain, or administrator.
- Evidence review displays the official event-end boundary and account-swap information in UTC so the administrator can compare them directly with the plugin timestamp visible in the screenshot. Event schedules may continue using the event timezone elsewhere.
- Approval is the administrator's visual attestation that the screenshot timestamp falls inside the official event window and an interval in which the selected playing account was active for that participant.
- An alt account cannot be selected for evidence.
- When a participant has swapped, evidence review shows the latest relevant account transition in UTC and provides the full transition history as an admin/audit detail when needed.
- The immutable website submission time governs ordering rules that explicitly use submission time.
- The application does not impose a maximum elapsed upload duration; the configured submission cutoff alone controls how late a new upload may be accepted.
- Review remains visual; the system does not use OCR to read the screenshot timestamp.

### 12.5 History and presentation

Every swap remains in append-only history even though ordinary pages do not show the whole list. Historical transitions are required to validate evidence uploaded after later swaps, reconstruct eligibility during disputes, preserve corrections, and support later Wise Old Man interval attribution.

Normal participant/captain evidence views show only the transition relevant to that evidence. An admin may inspect the full history through an audit/details view.

### 12.6 Approved acceptance cases

1. A participant with regular accounts A and B and alt account C may swap A → B → A without a cooldown, but can never swap to C.
2. A globally linked character omitted from the event signup is not a swap target after signup closes.
3. A playing account added during reopened signup becomes eligible; closing signup freezes the revised set again.
4. Two simultaneous swaps from A cannot both append a valid next transition.
5. A captain can swap for an unlinked external teammate but not for a linked participant on another team.
6. Evidence received while B was active remains visually validatable after the participant has swapped back to A.
7. Evidence displays the latest relevant account transition in UTC, with complete swap history available only when an admin needs it.
8. An admin backdated correction preserves the original audit history and requires a reason.

### 12.7 Wise Old Man and public display

- Wise Old Man synchronization includes every event account internally marked `PLAYING` (shown to users as a regular account), even when it is not currently active.
- Alt accounts and Yes/No support-alt answers are never sent to Wise Old Man and never appear in participant/team activity standings.
- Alt-account answers appear only on the unlisted public signup table. They do not appear on later team rosters, evidence, board progress, or leaderboards.
- The signup board is separate from finalized team rosters and makes signup participation publicly visible. Exact fields and waiting-list visibility are defined in `ADM-SIGNUP-01`/`PUB-SIGNUP-01`.

### 12.8 Approved whole-minute boundary

The clan plugin displays whole-minute UTC in `DD/MM/YYYY HH:mm UTC` format with no seconds. A normal swap becomes evidence-effective at the first full UTC minute after the request:

```text
requested at 08:16:35 UTC
old account eligible through displayed minute 08:16 UTC
new account eligible from 08:17 UTC
```

The UI immediately records the request, keeps the old account visibly active until the boundary, and shows when the new account will become active. A participant cannot submit another normal swap while this short transition is pending. Admin corrections use whole-minute UTC and require a reason when backdated.

## 13. Next capability — ADM-SIGNUP-01 Configure signup fields and public board

The following signup-table behavior is approved:

- The unlisted public table has clearly separated **Confirmed** and **Waiting list** sections and displays people in both.
- Waiting-listed people display their exact numbered positions.
- The built-in primary regular OSRS account is the event-facing identity. Website username is never displayed.
- All participant-facing custom answers are public on this exact-link table.
- Payment and Admin notes remain private and separate from signup questions.
- Version one exposes no public/private toggle, admin-only custom signup question, or post-draft question-privacy action. The retained visibility value is forced/defaulted to public for future compatibility.

### 13.1 Fixed system questions

Every signup form contains exactly these participant-facing system questions:

1. **Primary OSRS regular account**, required.
2. **EHB for that account**, required as part of the regular-account answer.
3. **Captain volunteer**, always present.

There is no built-in support-alt, secondary-account, comments, or availability question. Admins add those through the normal custom-question workflow when needed.

### 13.2 Additional Account questions

- An admin may add one or more secondary Account questions.
- Every secondary Account question is optional; the admin cannot make it required.
- A regular Account answer includes its own EHB field. The account may be left blank, but once supplied its EHB is required.
- My accounts stores an optional saved EHB default for each linked character. Saving a regular Account answer updates that default and captures a separate event-specific EHB snapshot; later My accounts edits never silently rewrite the submitted snapshot.
- An alt Account answer has no EHB field and is excluded from swapping, evidence, and Wise Old Man.
- A Yes/No question remains the correct choice when the organizer only needs to know whether an alt exists.

### 13.3 EHB behavior

- The participant's draft sorting and balancing EHB is the EHB snapshot belonging to the built-in primary Account answer.
- Secondary playing-account EHB values are never summed into or substituted for that draft value, even if a secondary account is stronger or becomes active later.
- Every regular Account question shown on the public signup table displays its paired EHB.
- The built-in primary account and primary EHB are always public.
- Captain volunteer is always public on the signup table.
- Generated headings are **Account**, **Account 1**, **Account 2**, and so on for regular accounts, and **Alt account**, **Alt account 1**, **Alt account 2**, and so on for alt accounts.

**Required behavior when WoM-assisted entry is delivered**

- Beside each regular-account EHB field, provide **Fetch from Wise Old Man** after the account name is present.
- This is a system feature, not an event-level or admin-configurable option: when the WoM integration is available, every eligible regular-account EHB control provides it.
- The fetch uses that normalized OSRS character name and populates the EHB field on success.
- Manual EHB entry remains available when the character is missing, WoM is unavailable, or the response is rate-limited. A failed fetch shows an accurate unavailable/retry message and does not clear an existing value.
- Fetch is an explicit user action, never triggered on every keystroke, form render, or public page view.
- Before implementation, review the then-current official Wise Old Man API documentation and usage/rate-limit rules. Add the required server-side caching, throttling, `Retry-After` handling, and accurate unavailable/retry feedback.
- Persist the submitted event EHB snapshot plus whether its last value was manually entered or fetched and the fetch time when applicable. Do not treat the external profile as authoritative after signup closes.

### 13.4 Approved question lifecycle

- A published form must be closed before any definition change. Private unpublished forms may be edited normally.
- Before the first accepted website or Admin participant response exists, an admin may edit or remove custom questions freely while the form is private or closed.
- The first accepted website or Admin participant response permanently locks the structural meaning of every question that existed for that response.
- After that boundary, a newly added participant-facing question must be optional.
- While signup is closed and the draft has not started, an admin may change an answered question's label, help text, and order.
- Once answers exist, the question type, Account role, choice options, and other answer-shape rules cannot change.
- A structural replacement disables the old question and creates a new optional question with a new stable key.
- Disabling stops the old question from appearing on new/edit forms but preserves all existing answers, label snapshots, form versions, and audit history.
- Existing answers to a disabled question remain available to admins and remain understandable on the public signup table.
- Participants who answered an earlier form version are never forced into an invalid state because a later optional question has no answer. The UI renders **Not answered** where that historical distinction must be shown.
- Every definition change increments the form version and records the actor, timestamp, and before/after metadata.
- Once the draft starts, ordinary changes to question definitions, labels, help text, and order are frozen.

### 13.5 Approved signup-readiness gate

Readiness is evaluated for the requested opening mode. A stored scheduled-opening value is irrelevant when an admin chooses **Open now**; manual opening records the actual opening instant. A scheduled opening requires its own valid future opening instant.

Opening is blocked unless:

- The event has a public description and a positive participant capacity.
- Event start and end are valid, and a valid signup closing instant exists or can be supplied by the approved manual-opening default.
- The requested opening mode has a valid opening instant and produces `opening < closing <= event start`.
- The event is in an allowed pre-draft state and the draft has not started.
- The deployment's normal Discord sign-in path is configured. This means the application has its required Discord OAuth client/callback configuration and participant login is enabled; it is not a per-event setting and does not require a live Discord API probe when signup opens.
- The built-in primary playing-account/EHB and captain-volunteer fields are intact.
- Every active custom question has a valid type and definition. Participant-facing types are Text, Number, Yes/No, Single choice, and Account. A single-choice question has valid choices. An Account question is explicitly either a regular account (`PLAYING`) or alt account (`INFORMATIONAL`) and, because it is secondary, remains optional.
- Signup-code protection has a usable code when that protection is enabled.

The following are warnings rather than blockers:

- Waiting-list support is disabled, because additional participants will be rejected when capacity is reached.
- A Text answer is present on the public signup table.
- An admin reopens signup after responses already exist. Reopening requires confirmation and automatic history, but no typed reason.

Missing custom questions, banner art, board setup, teams, draft time, or signup-code protection is neither a blocker nor a warning. WoM-assisted EHB entry is required functionality when that integration is delivered, but current WoM API availability is never an opening blocker because manual EHB entry remains available.

At a scheduled opening instant, the scheduler reruns the same readiness rules transactionally. If configuration became invalid, signup remains closed, the failed transition is recorded, and admins receive a visible alert. A temporary Discord or WoM outage does not rewrite event state; affected participant actions instead receive accurate service-unavailable feedback and a retry or manual-entry path where applicable.

### 13.6 Acceptance scenarios

1. **Open now** succeeds without a scheduled-opening value when every manual-opening prerequisite is valid.
2. A scheduled opening is blocked when its opening instant is absent, no longer future at configuration time, or inconsistent with closing/event start.
3. Enabling signup-code protection without a usable code blocks opening; leaving protection disabled does not warn.
4. An Account question without a playing/informational role blocks opening.
5. A WoM outage does not block opening and does not prevent manual EHB entry.
6. A scheduled transition that became invalid leaves signup closed and alerts admins.
7. Reopening a populated signup requires confirmation but no written explanation.
8. After draft start, ordinary form editing and visibility changes are unavailable.

## 14. PART-SIGNUP-01/02 — Submit and confirm an event signup

### 14.1 Entry and identity

- A participant opens the event signup and authenticates to their website account through Discord or public username/password before entering the normal form.
- If they do not yet have a website account, Discord creation and public-username/password onboarding complete first and create the approved preferred linked OSRS character.
- If the website account already owns a participant record for this event, the route opens that signup's view/edit state instead of creating a duplicate.
- Event-facing signup identity is supplied by the registered OSRS-character answers, not the website username.

### 14.2 Account selection and initial active account

- The built-in primary Account answer is required and always has the internal role `PLAYING`, shown as **Regular account**.
- The participant selects a linked character. If the character is missing, a **My accounts** link lets them add it before returning to signup. An existing event-assigned character remains selectable for editing even if its global link was later unlinked or transferred. Replacing it requires a currently linked, available character. Additional Account questions behave according to their regular (`PLAYING`) or alt (`INFORMATIONAL`) role.
- A regular-account EHB control is prefilled from the selected link's saved EHB. Saving the signup updates both that saved default and the event snapshot; an alt answer neither requires nor copies EHB.
- The built-in primary regular account automatically becomes the participant's initial active/drop-eligible account. Signup does not contain a separate initial-active selector.
- The participant may change the primary and other registered accounts while signup remains open. Closing signup freezes the final account set and roles.
- At event start, the system creates the initial activation for the frozen primary account. Later active-account changes use the approved swap workflow.

### 14.3 Atomic acceptance and account reservation

- Confirmed and waiting-list signups both reserve every named Account answer within the event. Capacity status does not weaken the one-current-assignment-per-character rule.
- A participant- or admin-initiated withdrawal before draft start releases those account reservations. Historical assignment rows remain available, but the released character may then be assigned to another participant in that event.
- Creating or editing a signup is one transaction covering the participant record, answers, event-character assignments, saved-EHB updates for regular-account answers, event EHB snapshots, status/capacity decision, and first-response marker.
- Every requested current event-character assignment must be acquired before the transaction commits.
- If any character is already reserved by another confirmed or waiting-list participant, the whole create/edit attempt fails. No partial participant, answer, link, reservation, EHB, or status change commits.
- The form identifies the conflicting Account answer as already in use for this event, preserves all other entered values in the returned form, and asks the participant to enter or select another account.
- A failed edit leaves the previously saved signup and its existing reservations unchanged until a corrected edit succeeds.
- Capacity calculation and the confirmed/waiting-list result occur inside the same transaction, with `signed_up_at` and `signup_sequence` providing deterministic order when submissions race.

### 14.4 Confirmation and management

After a successful create or edit, the authenticated participant sees:

- `Confirmed` or `Waiting list` status.
- Exact waiting-list position when applicable.
- Primary and secondary regular accounts with their event EHB snapshots.
- Alt Account answers clearly separated from regular accounts.
- Captain-volunteer choice and all of their submitted custom answers.
- Whether signup editing is currently available.
- Whether the separate withdrawal action is currently available.

The participant-facing confirmation shows their complete submitted record. The public signup table remains a different projection: all participant-facing answers are public, while website username, Discord identity, payment, Admin notes, security data, and audit data remain excluded.

### 14.5 Editing, cancellation, rejoin, and restoration

- A successful edit keeps the participant's original signup time, signup sequence, confirmed/waiting status, and waiting-list position. Editing answers or accounts never moves someone forward or backward in the queue.
- While signup is open, a participant may use a separately confirmed **Cancel signup** action. Cancellation releases account reservations and may promote the earliest waiting-listed participant.
- If that participant signs up again while signup is still open, the existing participant record is reactivated with a new signup timestamp and sequence at the end of the current queue. It never reclaims the former position.
- After signup closes and before draft start, the participant may still withdraw through the approved separate action but cannot restore themselves.
- An admin may restore a withdrawn participant before draft start only when the required accounts are still available. Restoration uses current capacity: confirm them when a place is open, otherwise place them at the end of the waiting list. It never demotes or displaces somebody who was already promoted.
- Cancellation, withdrawal, rejoin, restoration, reservation release/acquisition, capacity status, and any waiting-list promotion commit atomically.

### 14.6 Promotion notification

- Promotion from waiting list to confirmed creates a durable in-site notification for a linked participant account.
- The same promotion creates an in-site notification for every enabled administrator. It identifies the event, promoted participant, and whether promotion followed a capacity increase, cancellation/withdrawal, or admin restoration.
- The notification links to the event signup confirmation and is supplementary; the confirmation page always shows authoritative current status.
- The website does not promise an automated Discord message. Admins may notify the participant manually through the community's normal Discord workflow.
- An unlinked imported/external participant has no website notification recipient; admins handle any necessary communication.

### 14.7 Event-participant ownership transfer

- This workflow corrects a participant attached to the wrong or duplicate website account; losing a Discord login normally uses self-service Discord relinking instead.
- An authorized admin selects the existing event participant and the destination website account.
- The destination account must not already own another participant record in that event.
- A confirmed transfer changes only event-participant ownership and derived event access. It does not merge global website accounts or move global My accounts links.
- The prior identity immediately loses access to that event participant. The destination identity gains the same participant scope.
- Signup timestamp/order, status, account reservations, team/role, evidence, and competitive history remain unchanged.
- The transfer is strongly confirmed and automatically records actor, time, and old/new identities; it does not require a written explanation.

### 14.8 Acceptance scenarios

1. A first-time Discord user completes profile onboarding and returns to the intended event signup.
2. A signup with one valid primary account becomes confirmed below capacity and plans that primary account as the initial active account.
3. A valid signup at capacity becomes waiting-listed and reserves its named accounts just like a confirmed signup.
4. Two participants race for the same character; exactly one commits and the other receives an account-specific conflict without losing other entered values.
5. A failed edit does not partially replace the participant's previously saved accounts or answers.
6. Withdrawing a participant before draft start releases their account reservations and promotes the correct waiting-list participant where applicable.
7. The confirmation page shows the exact waiting-list position and the participant's complete private submission while the public board continues to use its narrower visibility rules.
8. Editing a waiting-list signup preserves its original queue position.
9. Cancelling and rejoining while signup is open places the participant at the end with a new ordering key.
10. A post-close participant withdrawal cannot be self-restored.
11. Admin restoration never displaces a promoted participant and fails if a required account is no longer available.
12. Promotion notifies the linked participant and every enabled admin in site, but creates no automated Discord-message dependency.
13. Identity transfer fails when the destination already participates in the event and otherwise preserves all event history while revoking the old identity's access.

### 14.9 My events

- **My events** is always present in normal authenticated account navigation.
- It uses explicit `EventParticipant.AccountId` ownership only and never infers participation from website username, Discord, or OSRS-character links.
- It separates current participation from historical participation.
- Every owned participant record appears with its authoritative status and the best state-specific destination: signup confirmation/edit, signup table, team roster, board, or results.
- Imported/external unowned records do not appear until an authorized ownership transfer.

## 15. ADM-PARTICIPANT-01 — Manage participants before draft

### 15.1 Authoritative workspace

Participant administration uses one event-level workspace rather than treating the existing event-management, participant-detail, and draft-pool pages as separate sources of truth.

The workspace provides:

- Separate `Confirmed`, `Waiting list`, and `Withdrawn` sections with counts.
- Search plus filters for status, paid/unpaid, linked/unlinked Discord identity, captain volunteer, signup source, and current team assignment.
- Current website username and Discord-link status for Admin support only; Discord display name is non-authoritative metadata.
- Signup source, original/current queue time and sequence, status, and exact waiting-list position.
- Regular accounts with per-account EHB, alt accounts separately, and the automatic initial primary account.
- Captain-volunteer answer and all current/historical custom answers. Participant-facing answers are public; payment and Admin notes remain separate private fields.
- Private paid/unpaid state and private admin notes.
- Team/draft state and links to the approved identity-transfer and participant actions.

### 15.2 Payment tracking

- Payment is a private binary `Unpaid`/`Paid` value, defaulting to `Unpaid`.
- The public signup form never asks for it.
- It is never shown on the public signup table, team roster, draft result, evidence, or leaderboard.
- Events that do not use a buy-in may ignore the field; no `Unknown`, `Waived`, or `Not required` participant states remain in the target model.
- Changing payment is a routine admin update with automatic before/after history and no participant notification.

### 15.3 Pre-draft corrections

- Before draft start, an admin may correct participant-entered answers and Account selections even after public signup closes.
- Corrections use the same question validation, regular/alt roles, EHB requirements, and atomic event-character reservation rules as participant edits.
- A correction keeps signup time, queue sequence, confirmed/waiting status, and waiting-list position.
- A conflict rejects the complete attempted correction and leaves the last saved participant record unchanged.
- Routine pre-draft corrections require no written reason. Actor/time and structured before/after values are recorded automatically.

### 15.4 Manual internal participant

- Before draft start, an admin may add an internal participant even while public signup is closed.
- Admin entry bypasses the public opening window and signup code, but not required system fields, question validation, account uniqueness, or transaction rules.
- Linking a website account is optional for Admin-created external/imported records. A later Admin identity transfer may supply access without changing the participant's event history; Slice 2 adds no claim invitation.
- An admin-created internal participant follows the same current capacity and waiting-list rules as a public website signup. `ADMIN_CREATED` is provenance, not a capacity bypass.
- External/pre-formed team members remain in the separate external-roster workflow and do not consume the public/internal draft-pool capacity.

### 15.5 One inactive participant status

- The target signup statuses are `CONFIRMED`, `WAITING_LIST`, and `WITHDRAWN`.
- The application does not distinguish participant cancellation from admin removal through separate statuses. Both use the same confirmed withdrawal action.
- The automatic transition history identifies the actor and timestamp. A private admin note may be added when useful, but no written reason is required.
- Pre-draft withdrawal preserves the participant record, releases current account reservations, and promotes the earliest eligible waiting participant atomically.
- Restoration follows the already approved availability, capacity, and end-of-queue rules.
- Existing legacy `REMOVED` records migrate to `WITHDRAWN` while retaining their original timestamps and history.

### 15.6 Participant notifications for admin actions

A linked participant receives an in-site notification when an admin:

- Withdraws their signup.
- Restores their signup.
- Changes one or more of their registered event accounts.

Payment updates, private admin notes, and ordinary non-account answer corrections do not notify the participant. Notifications are supplementary to the authoritative confirmation page and do not create an automated Discord-message dependency.

### 15.7 Acceptance scenarios

1. An admin can find confirmed, waiting, or withdrawn participants from one workspace and inspect the complete approved detail projection.
2. Payment offers only `Unpaid` and `Paid` and never appears publicly.
3. A post-close/pre-draft admin correction succeeds without changing queue position.
4. A conflicting account correction fails atomically and preserves the prior record.
5. A manually added internal participant obeys required fields, character uniqueness, capacity, and waiting-list order even though the admin bypasses the signup window/code.
6. An external-roster member does not consume internal signup capacity.
7. Admin and participant withdrawal use the same `WITHDRAWN` state; history still identifies the actor.
8. Withdrawal promotes the correct participant and emits the already approved participant/admin promotion notifications.
9. Admin withdrawal, restoration, or account correction notifies the linked participant once; payment, notes, and ordinary answer edits do not.

## 16. ADM-PARTICIPANT-02/ADM-CAPTAIN-01 — Post-draft roster exceptions

### 16.1 Withdrawal authority

- Participant self-service withdrawal ends when the draft starts. The participant-facing page tells them to contact an administrator.
- An admin may withdraw a drafted participant after draft start, including during a live event.
- The action uses strong confirmation and automatic structured history but no required written explanation.
- The command immediately revokes the participant's website event/team mutation authority. Its separate drop-eligibility boundary remains the approved first-full-UTC-minute boundary below so plugin timestamps can be compared consistently.
- Existing draft picks, former team membership, account registrations, approved/pending evidence, and contribution history are retained.
- The former membership receives an end/effective timestamp and disappears from the current roster without being deleted from historical draft/team views.
- During a live event, the participant and their accounts stop being eligible for new drop times at the withdrawal boundary. Evidence for a qualifying drop received before that boundary remains reviewable after withdrawal.

### 16.2 No automatic post-draft promotion

- Draft start permanently disables automatic capacity/waiting-list promotion for that event.
- Withdrawing a drafted or live participant creates a visible team vacancy and an admin notification/action item; it does not choose a replacement.
- Administrators contact waiting-list participants through their normal Discord workflow to confirm continued availability.
- The replacement control presents the waiting list in original order, but the admin explicitly selects the available person. The application does not force selection of an unavailable earlier person.

### 16.3 Normal replacement source and accounts

- The normal replacement is an existing `WAITING_LIST` participant.
- That participant already has frozen playing/informational account registrations and EHB snapshots. Replacement does not ask them to register accounts again.
- Their existing event-character reservations remain valid; the transaction changes their status to `CONFIRMED` and creates a current membership directly on the vacant team.
- The departed participant's post-draft account registrations remain reserved for historical integrity and cannot be reused by the replacement or another participant in that event.
- The completed draft is not rerun or rewritten. The new membership is marked as a manual roster replacement and links to the membership it replaced.
- Filling the vacancy is optional. A team may continue with fewer participants.
- If no waiting-list participant is available, an admin may optionally create a new internal replacement. The admin supplies the same required playing account/EHB and required participant data, account uniqueness still applies, and the participant is placed directly on the vacant team as an admin-created replacement rather than entering the waiting-list queue.

### 16.4 Live replacement

- The same admin-only withdrawal and manual replacement workflow is available while the event is live.
- The withdrawn participant retains every contribution and evidence record from their eligible period.
- The replacement joins the selected team prospectively and receives no credit or eligibility for time before the replacement became effective.
- Withdrawal eligibility ends at the first full UTC minute after the admin confirms it. The former participant remains drop-eligible through the displayed request minute.
- A live replacement's primary playing account becomes active at the first full UTC minute after the replacement is confirmed.
- If replacement happens later than withdrawal, the interval between those two effective instants is an intentional eligibility vacancy with no participant occupying the roster slot.

### 16.5 Captain and co-captain changes

- An admin may assign, promote, demote, or revoke captain/co-captain for a current team member after the draft and during the live event.
- Authority remains attached only to the active event/team membership plus explicit `EventParticipant.AccountId` ownership of an active website account. Discord is an authentication/linking method, never role authority; OSRS names, Discord names, volunteer answers, and emergency credentials do not infer ownership.
- A withdrawn member immediately loses captain/co-captain authority.
- The system never automatically chooses a new captain. If the final captain/co-captain leaves, the team receives a prominent missing-captain warning until an admin assigns another current member or explicitly enables an emergency credential.
- Role changes take effect immediately for future website actions, preserve role history, and notify the affected linked participant.
- Event-start readiness requires every current team to have at least one active Captain or an explicitly enabled team-scoped emergency captain credential. A co-captain alone does not satisfy readiness.
- Losing the last captain during a live event does not stop or pause the event, but creates an urgent admin/team warning.

### 16.6 Notifications

- Creating a post-draft vacancy notifies every enabled admin and each remaining linked captain/co-captain on that team.
- Confirming a replacement notifies the linked replacement and the team's current linked captains/co-captains.
- A captain/co-captain role change notifies the affected linked participant.
- Notifications are in-site only and are idempotent; admins continue availability coordination through Discord manually.

### 16.7 Captain-role direction

The focused captain workflow starts from these approved differences:

- A participant submits evidence only for themselves; a captain/co-captain may choose any current member of their own team as the credited participant.
- Before and during the website draft, a captain/co-captain assigned to a drafted team may view the expanded signup-table projection for the confirmed draft pool. This reuses the public table with additional response columns rather than creating a separate participant-management interface.
- Draft-captain visibility includes participant-submitted signup answers hidden from the public board, but excludes paid/unpaid state, private admin notes, Discord identity-recovery/security data, and audit history.
- After draft finalization, the signup page remains intact and admin-authorized. Public, participant, and captain requests for that route redirect to published team rosters, which do not include expanded signup responses.
- A captain/co-captain may highlight tiles, complete rows, and complete columns as the team's current focus. Highlighting is team coordination state, not official completion/progress and not an admin board edit.
- Captain/co-captain does not otherwise grant general event, roster, board-design, review, or administration authority.

### 16.8 Acceptance scenarios

1. A drafted participant cannot self-withdraw and is directed to contact an admin.
2. Admin withdrawal after draft creates a vacancy without automatically promoting anyone or rewriting the pick.
3. The admin can review the ordered waiting list, contact people externally, and select an available candidate rather than being forced to use the first unavailable person.
4. Replacing from the waiting list reuses that participant's frozen registered accounts and EHB without another signup.
5. The former participant's event accounts and historical membership remain reserved/preserved after draft start.
6. A live withdrawal preserves earlier evidence and contribution while ending future eligibility.
7. A replacement joins the vacant team prospectively and does not inherit the departed participant's evidence.
8. Withdrawing a captain revokes their authority and displays a missing-captain warning.
9. An admin can promote another current member to captain/co-captain during the live event without creating a separate normal login.
10. An admin may leave a vacancy unfilled.
11. When the waiting list has no available replacement, an admin may create a valid unique-account replacement directly on the team.
12. Live withdrawal and replacement use separate next-full-UTC-minute boundaries and preserve any gap between them.
13. Event start is blocked when any team lacks a current Captain or enabled emergency captain credential; co-captain alone is insufficient.
14. Losing the final captain during live play creates an urgent warning but does not stop the event.
15. Vacancy, replacement, and captain-role notifications reach the approved in-site recipients exactly once.

## 17. ADM-DRAFT-01 — Set up, run, and finalize the website draft

### 17.1 Draft pool

- The website draft includes every `CONFIRMED` internal participant who is not currently assigned to a pre-formed team.
- `WAITING_LIST` and `WITHDRAWN` participants are excluded.
- A confirmed website participant manually assigned to a pre-formed team is excluded from the available pool.
- An external/invited pre-formed roster member remains outside normal signup capacity and is also excluded.
- A participant already assigned to a drafted team before the first pick, such as a preassigned captain, remains part of the drafted-team participant total but is no longer an available pick.

### 17.2 Derived team count and roster sizes

The real draft has no separately entered team-count or target-team-size settings:

- Drafted-team count is the number of active teams whose formation type is `DRAFTED`.
- Pre-formed teams do not contribute to that count.
- Drafted-team participant count includes all confirmed internal participants who are either eligible for the website draft or already assigned to a drafted team.
- Captain and co-captain memberships occupy ordinary roster positions.
- Board-editor team-count and team-size values remain planning estimates used for board EHB. They neither create teams nor constrain the eventual draft.

For `P` drafted-team participants and `T` active drafted teams:

```text
larger_size = ceiling(P / T)
smaller_size = floor(P / T)
larger_team_count = P mod T
smaller_team_count = T - larger_team_count
```

When `P` divides evenly by `T`, every team has the same size. Otherwise, the setup preview uses wording such as **Team size 15; 2 teams will have 14 players**. Each team card retains its compact `current/final` counter; the denominator shows whether that named team is projected to finish at the larger or smaller size. The final partial snake round, current preassignments, and randomized order determine which named teams receive the larger roster. The system plans/skips turns as needed so final roster sizes differ by no more than one. Draft start is blocked if existing manual assignments make that balanced result impossible.

### 17.3 Captain and co-captain assignment

- The captain-volunteer answer is a visual aid, not an automatic role assignment.
- An admin chooses captains/co-captains and may change those roles before, during, or after the draft.
- Assigning an available participant directly to a drafted team before the first pick consumes a normal roster position and removes that participant from the available pool.
- Every drafted team must have at least one member with the actual `CAPTAIN` role before the draft starts. A `CO_CAPTAIN` is optional and cannot satisfy this blocker by itself.
- Every preassigned captain/co-captain occupies an ordinary roster position. When teams begin with unequal numbers of those preassigned members, teams with more are skipped until the lower-count teams catch up; only then may the larger starting roster receive another pick.
- After the first pick, an admin may promote or demote a current team member but may not bypass the pick ledger by directly placing another available participant.
- The separately approved event-start readiness gate requires every team, including pre-formed teams, to have an active `CAPTAIN` or enabled emergency credential. A co-captain alone is insufficient.

### 17.4 Team creation, metadata, and pre-formed teams

- Drafted teams are added individually. Each has a required event-unique display name, a stable generated URL identifier, an optional managed image asset, and an optional affiliation/clan label.
- Team images use the shared authenticated image-upload workflow. Admins select and upload a local image file; arbitrary image-URL input is not supported.
- An empty drafted team may be removed in Setup, including after the controller cancels an unpublished private draft back to Setup.
- Team name, image, and affiliation remain editable before event start, including after draft finalization. The stable team URL does not change.
- Event start locks ordinary team-metadata editing. Automatic structured history is sufficient for permitted pre-start metadata changes; no typed reason is required.
- Internal and external/invited pre-formed teams receive no website-draft turns.
- They do not affect drafted-team count, derived roster-size calculations, or pick ownership.
- Their manually assigned members are excluded from the available draft pool.
- External/invited members may be created by an admin without public signup or Discord linking and remain outside normal signup capacity.
- A pre-formed roster CSV is scoped to one selected team. Each data row is one member: the first column is the required primary account, the second is its required EHB, and later columns are additional accounts without EHB. Import previews validation and applies atomically; Captain/Co-captain remains a separate manual Admin assignment.
- A pre-formed team may be created before or after website-draft finalization, but not after event start.
- Its roster may be freely corrected before event start using ordinary validation and automatic history.
- After event start, roster changes use the approved withdrawal/replacement workflow rather than unrestricted roster editing.
- Active private picks lock drafted-team structure. Before publication, the controller may cancel the attempt to Setup, undoing active picks while retaining their history and restoring setup editing. Team metadata remains editable until event start.

### 17.5 Start and visibility

- At least two active drafted teams are required.
- Draft start closes and locks signup, even when the configured closing instant has not arrived.
- Every included participant must pass the ordinary confirmed/account-reservation rules, and the derived balanced distribution must be possible.
- Every drafted team must already have an assigned `CAPTAIN`.
- The draft and provisional assignments remain admin-only until finalization.
- One admin holds the renewable controller lease while other admins receive a live read-only view.
- The controller explicitly scrambles the drafted teams. They may scramble again until the first active pick exists.
- Captains continue making selections through the community's normal voice/text workflow; the controller records them on the website.

### 17.6 Picks, undo, pause, and resume

- Snake direction reverses on alternating rounds.
- Turn eligibility first skips any team whose current roster is larger than the smallest drafted-team roster, allowing teams with fewer preassigned captains/co-captains to catch up. Eligible turns then continue through the authoritative randomized snake order.
- All participants remain visible. Available players and their primary-account EHB are visually distinct from players already assigned to a team.
- A pick atomically records the immutable pick entry and creates the active membership.
- Undo always targets the latest active pick, marks that pick undone, ends its membership, and returns the participant to the available pool.
- Undo is repeatable without an arbitrary one-action limit. The controller may work backward one latest active pick at a time, including back to zero picks, then continue from the restored turn.
- The controller may pause and resume without changing team order, picks, or available participants.

### 17.7 Finalization

- Every confirmed internal participant included in the drafted-team total must have an active drafted-team membership.
- Every drafted team must match its derived larger or smaller final size, with a maximum difference of one.
- No confirmed draft-pool participant may remain unassigned.
- Finalization locks the completed draft ledger and publishes all team rosters plus the final active pick order together. Public order includes pick number, participant, and receiving team; undone attempts, controller identity, internal timestamps, and correction history remain admin-only.
- Draft finalization completes and publishes rosters/pick order first. If the board is grid-complete, approved, and still private, the success flow then opens a **Publish board?** prompt or dedicated follow-up page with a **Publish board** button. Board publication is a separate action and transaction. An incomplete/unapproved board still leaves a prominent outstanding-board action.
- Pre-formed rosters remain separate from the immutable website-draft order and pick history.

### 17.8 Reopening an accidentally finalized draft

- Before event start, an admin may reopen a finalized website draft through strong confirmation and a required written reason.
- Reopening temporarily removes the finalized team rosters and active pick order from public view and returns the draft to controlled correction mode.
- The drafted-team set and formation types remain locked. The controller may repeatedly undo latest active picks, record replacements, and finalize again.
- Each finalize/reopen cycle, actor, timestamp, written reason, superseded public projection, and pick history is preserved.
- A board already published from its own approved immutable snapshot remains published because reopening team picks does not alter board content.
- Re-finalization republishes the corrected rosters and active pick order. If an eligible board has not already been published, the same separate publication prompt/page follows.

### 17.9 Acceptance scenarios

1. Creating or removing a drafted team immediately recalculates the projected roster distribution without editing a team-count field.
2. With 73 drafted-team participants and five drafted teams, setup reports three teams of 15 and two teams of 14.
3. Board-editor planning estimates do not create teams or set draft roster limits.
4. Waiting, withdrawn, and pre-formed-team participants receive no draft turns.
5. A preassigned captain occupies a normal roster position and cannot also be picked.
6. Draft start is blocked until every drafted team has an assigned Captain; co-captain alone does not satisfy the gate.
7. Scrambling is repeatable before the first active pick and blocked afterward unless the unpublished attempt is cancelled back to Setup.
8. Repeated undo can unwind several picks in reverse order and restores the correct next snake turn each time.
9. Finalization is blocked while any confirmed draft-pool participant is unassigned or the derived balanced distribution is not satisfied.
10. If teams start with two, one, and one preassigned captains/co-captains, the larger roster is skipped until the other two have caught up.
11. Finalization publishes all completed rosters and the final active pick order while preserving pre-formed teams outside the draft ledger.
12. After draft finalization succeeds, a complete approved private board produces a separate publication prompt/page; an unfinished, unapproved, or dismissed board remains private without affecting the completed draft.
13. Public draft results show the effective pick order but never undone attempts or internal controller/audit data.
14. Reopening a finalized draft before event start requires a written reason, temporarily hides rosters/pick order, preserves all history, and permits stack undo/repicking without unlocking team structure.
15. Team name, uploaded image, and affiliation may change after draft finalization but lock at event start.
16. Ordinary undo alone does not reopen drafted-team editing; cancelling an unpublished private attempt returns to Setup and restores it while retaining undone-pick history.

## 18. Cross-cutting managed-image rule

- Every application-owned image supplied by a participant or administrator uses authenticated file upload and the shared stored-asset pipeline. This includes event banners, team images, custom board/tile artwork, and evidence.
- The interaction follows the existing evidence-submission pattern: choose a local file, upload it through the application, validate it server-side, and show accurate success/failure feedback.
- Decorative assets and evidence may share upload mechanics, storage abstraction, media inspection, checksum generation, and safe storage keys, while retaining their different authorization and retention rules.
- No normal event, team, signup, board, tile, profile, or evidence form accepts an arbitrary image URL.
- The global OSRS catalogue is the only product area allowed to store an external source-image URL. Catalogue images are fetched/cached through the catalogue infrastructure and are not a precedent for URL fields elsewhere.

## 19. ADM-BOARD-01 — Build, approve, and publish the competitive board

### 19.1 Board availability and scope

- An event has one competitive board.
- Admins may create and edit its private draft at any time after event creation.
- Board completion is not required to open signup or start the participant draft.
- A published board is required before the competitive event can enter `LIVE`.
- Tiles are created for and owned by this board. There is no duplicate-tile, copy-from-event, reusable-template, or tile-import workflow.
- Existing move/swap controls are the way admins reposition configured tiles within the board.

### 19.2 Complete-board validation

A board is complete only when:

- every grid position contains an active tile;
- every tile has the required public wording, objective structure, and eligible data;
- every catalogue-backed/drop tile produces a valid automatic EHB estimate; and
- every custom/manual objective contains its explicitly configured manual EHB value.

A missing automatic EHB value on a non-custom catalogue/drop tile is a validation error. The admin must correct the underlying source-drop probability/rate or tile requirement. The page must never treat a manual override as the repair for a broken standard tile. Manual EHB entry exists only for a custom/manual objective.

### 19.3 Approval and invalidation

- **Approve board** is an explicit action after complete-board validation succeeds.
- Any enabled admin may approve; a second independent approver is not required.
- An approved but unpublished board may be explicitly unapproved.
- Editing any tile or other competitive board content returns an approved unpublished board to `DRAFT` and invalidates the current approval. Prior approval actor/time remains in history.
- Because the board is still private, unapproval or pre-publication invalidation requires no written reason.

### 19.4 Post-finalization publication prompt

- Draft finalization always publishes the finalized rosters and effective pick order.
- After finalization succeeds, the admin receives a popup or is redirected to a short follow-up page.
- When the board is both grid-complete and approved, that follow-up asks **Publish board?** and provides a **Publish board** button.
- Board publication is a separate mutation and transaction from draft finalization.
- When any grid position is empty or the board is not approved, the follow-up contains no enabled publication button and instead states the exact blocker plus the route back to board preparation.
- Dismissing or leaving the follow-up keeps the approved board private and creates/retains a clear admin action to publish it later.
- Draft finalization never publishes an incomplete or unapproved board automatically.

### 19.5 Later publication and corrections

- When the draft has already finalized, an approved complete board exposes a separate **Publish board** action.
- Event start remains blocked until board publication succeeds.
- After publication, the immutable snapshot and existing exceptional correction rules apply. A correction requires confirmation, a written reason, a replacement snapshot, and recalculation without erasing prior history.

### 19.6 Acceptance scenarios

1. Signup and participant drafting may proceed while the board is still incomplete.
2. Approval fails when even one grid position is empty.
3. A catalogue/drop tile with no automatic EHB blocks approval and offers diagnostics rather than a manual override.
4. A custom/manual objective accepts its explicit manual EHB value.
5. Editing an approved unpublished tile immediately returns the board to Draft and retains the superseded approval in history.
6. A successful draft finalization leads to a separate **Publish board?** prompt/page only when the board is complete and approved.
7. Dismissing the prompt publishes nothing further: rosters/pick order remain public and the approved board remains private.
8. A board approved after draft finalization can be published separately.
9. Event start is blocked while the board is unpublished.
10. The editor offers move/swap but no tile duplication, cross-event copy, or reusable-template action.
11. Missing tile-level evidence instructions never block approval because general submission guidance belongs to the global public Rules and how-to pages.

## 20. SYS-EVENT-START-01 — Scheduled start readiness

- Reaching the configured event-start instant never bypasses readiness rules.
- Automatic start is postponed when the draft is not finalized, the board is not published, any team lacks its required Captain/emergency access, or another event-start invariant fails.
- The event remains in its pre-live state. Admin UI and in-site notifications say **Automatic start postponed** and list each current blocker.
- The original scheduled instant remains visible; the system does not pretend the event started on time and does not backdate live eligibility.
- After a postponed scheduled start, clearing the blockers does not cause a delayed automatic transition. An authorized admin must use **Start event now**.
- When the scheduled instant has passed and readiness now succeeds, **Start event now** needs strong confirmation but no written reason.
- Starting before the configured instant remains an exceptional early start and requires a written reason.

## 21. ADM-RULES-01 and PUB-HOWTO-01 — Permanent public guidance

- Evidence-submission instructions do not belong to individual tiles.
- The application exposes one permanent, global, public **Rules** page. It is not attached to an event and remains available regardless of the current event lifecycle.
- Any enabled administrator may open **Edit rules** and update the Rules page at any time. The update uses normal authorization, validation, concurrency protection, and automatic change history, but requires no written reason and sends no participant notification.
- Public how-to pages, including **How to submit drops**, are source-controlled application content. They are authored and changed through the development workflow and have no in-application editor.
- Event dashboards and submission surfaces may link to the global Rules and relevant how-to pages.
- A custom/manual tile still contains the objective-specific completion description and criteria needed to understand that tile; it does not duplicate the general screenshot/upload/submission process.
- Rules and how-to content never block signup, draft finalization, board approval/publication, scheduled start, or any other event transition.

## 22. ADM-EVENT-END-01 — End live play and use the submission grace period

### 22.1 Scheduled and early end

- The event ends automatically at its configured event-end instant. The transition from `LIVE` to `AWAITING_FINAL_REVIEW` closes eligibility for newly obtained drops but does not close the separately configured submission window.
- If the application is unavailable at that instant, the next request or scheduled check catches up using the configured event-end instant as the authoritative effective end; eligibility is never extended merely because processing ran late.
- An enabled administrator may end a live event before its configured end only after strong confirmation and a required written reason. The authoritative effective end is the confirmation time, while the original scheduled end remains historical.
- The early-end action does not silently move the configured submission cutoff. Upload access continues until that existing cutoff unless an administrator separately performs an approved cutoff/reopening action.

### 22.2 Submission grace period and review

- Until the active submission cutoff, an authorized submitter may upload evidence claiming a drop received on or before the authoritative event end.
- The application validates immutable server submission time against the upload cutoff. It does not ask for a second typed drop time, read the UTC overlay from the screenshot, or enforce a configurable “upload within N hours” rule.
- Reviewers visually compare the clan-event plugin's UTC timestamp in the screenshot with the official event end and applicable account-transition history.
- Reopening or extending submission access never extends the official obtained/drop-eligibility window.
- Existing submissions, evidence review, contribution recalculation, and final-review work continue after event end. Finalization remains an explicit administrator action.

### 22.3 Live roster consequences

- An administrator may withdraw a participant during live play. Website event/team mutation access is revoked when the command commits; the approved whole-minute rule determines the final drop-eligibility boundary.
- Historical membership, character assignments, swaps, submissions, evidence, and contributions remain intact. Post-draft character assignments remain reserved for the rest of that event and cannot be reassigned.
- Replacement is optional and never automatic. The normal replacement is an administrator-selected waiting-list participant whose frozen event accounts/EHB are reused and who joins the selected vacant team prospectively without inheriting credit.
- Losing a team's only Captain does not pause the event. It creates the approved urgent admin/team warning until an administrator assigns another current member or enables emergency captain access.

### 22.4 Acceptance scenarios

1. Reaching the scheduled end closes new-drop eligibility at the configured instant while uploads remain available until the cutoff.
2. A delayed background check records the scheduled instant rather than extending play until the check ran.
3. Early end requires strong confirmation and a reason, preserves the original schedule, and does not silently change the submission cutoff.
4. Evidence for an in-window drop may be uploaded during grace; a screenshot showing a post-end clan-event time must be rejected.
5. No screenshot OCR or fixed upload-hours rule is required; an administrator performs the visual plugin-timestamp review.
6. Live withdrawal preserves all prior competitive history and reserves the former participant's event characters.
7. An optional waiting-list replacement joins prospectively with their existing accounts and no inherited contribution.
8. Withdrawing the sole Captain leaves the event live and raises the missing-captain warning.

## 23. ADM-REVIEW-01 — Review evidence

### 23.1 Queue and authority

- Any enabled administrator may review evidence. Submissions are not assigned to individual reviewers.
- The queue defaults to pending submissions for the current active event, newest first, with event/team/tile filters available when needed.
- Review mutations use optimistic concurrency. If another administrator or captain changes the submission first, a stale review fails and shows the newer authoritative state.

### 23.2 Two review decisions

- A pending submission has only two administrator decisions: **Approve** or **Reject**.
- Approval requires no written reason.
- Rejection requires a written reason. A duplicate, invalid screenshot, or evidence needing replacement is rejected rather than entering a special duplicate or changes-requested state.
- A rejected attempt remains historical. While the active upload window permits new submissions, its detail view offers **Resubmit**. The action prefills ordinary structured values and note, requires a new image, and creates a linked submission with its own server submission time and review history.
- Resubmission copies the rejected attempt's credited participant and playing-account snapshots as read-only, including when that participant has since swapped. The submitter may correct ordinary structured selections such as tile/requirement or drop; an administrator may instead use the existing reasoned metadata correction while a submission is pending.
- One rejected record may have only one direct resubmission. Concurrent or repeated requests cannot create duplicate corrected attempts; if the corrected child is rejected, that child may itself be resubmitted.
- Administrators cannot upload or replace a captain/participant's evidence image on that submission.

### 23.3 Metadata correction

- Before approval, an administrator may correct the tile/requirement, qualifying drop, or credited playing account when the evidence supports the correction.
- The credited participant is never edited independently. The application derives them from the event-unique credited playing-account assignment.
- These corrections affect competitive eligibility or allocation and therefore require a written reason plus structured before/after history.
- The immutable server submission time, board-snapshot contribution weight, and calculated contribution are never directly editable.

### 23.4 Rejection notifications

- Rejection creates an idempotent in-site notification for the credited participant when linked and for every current linked captain/co-captain on that team.
- The notification includes the event, tile/drop, rejection reason, and a route to the rejected submission without exposing another team's evidence.
- The whole team is not notified. An unlinked credited participant is represented by the captain/co-captain recipients.
- When the upload window is still open, the rejection notification exposes the same authorized **Resubmit** route. After cutoff it shows the reason and history without an enabled resubmission action.

### 23.5 Approval reversal

- Reversing an approval requires strong confirmation and a written reason.
- The submission and original approval remain historical. Reversal removes its contribution and recalculates affected requirement, tile, row, column, board, leaderboard, and placement state.
- Newly freed requirement capacity is reallocated to later eligible approved evidence according to the authoritative progress rules.

### 23.6 Acceptance scenarios

1. Two administrators cannot decide the same pending submission differently; the stale action fails with the newer result.
2. Approval succeeds without a typed reason.
3. Rejection cannot succeed without a reason and notifies the linked credited participant plus current linked team captains/co-captains exactly once.
4. Duplicate or unusable evidence is rejected without creating an intermediate review state.
5. A corrected attempt is a linked new submission rather than a transition on the rejected record, requires a new screenshot, and rejection does not create post-cutoff submission access.
6. Selecting a different credited playing account derives its participant and cannot create an inconsistent account/participant pair.
7. A material metadata correction requires a reason, preserves before/after values, and revalidates the complete submission.
8. An administrator cannot change the immutable server submission time, snapshot weight, calculated contribution, or evidence image.
9. Reversal requires a reason and deterministically recalculates every affected projection.
10. If a participant swaps after the original upload but before rejection, **Resubmit** preserves the rejected attempt's credited account as read-only while a normal fresh submission uses the currently active account.
11. Two racing resubmission requests create no more than one direct corrected child.

Every submission shows immutable **Submission time**. Only when that time is after the authoritative event end does the compact summary additionally show the calculated number of minutes after event end and **Latest clan event time** using that end formatted in UTC. The screenshot remains beside this information so the administrator can visually compare its plugin timestamp. The submission cutoff is enforced by the application but is not repeated in this compact comparison.

Approved evidence metadata, credited player, and screenshot are public so community members can inspect accepted evidence. There is no submitter privacy-request or hidden-but-still-approved state. If a screenshot must cease being public, an admin reverses approval with a reason and the team uses the normal corrected/redacted resubmission path while cutoff permits it.

## 24. ADM-FINALIZE-01 — Final review and official results

### 24.1 Submission closure

- Reaching the active submission cutoff automatically records submission closure and rejects new uploads.
- Existing pending submissions remain reviewable after cutoff. Enabled administrators may still approve or reject them.
- Every pending submission is a finalization blocker. Approved, rejected, withdrawn, and reversed submissions are historical resolved states and do not block merely because of their status.

### 24.2 Blockers and override

- Other genuine competitive-integrity blockers, such as an invalid progress calculation, unresolved completion-time inspection, or open manual placement correction, remain derived checklist items.
- The underlying record should normally be resolved. **Mark resolved anyway** is an exceptional override that requires strong confirmation and a written reason, preserves the underlying data, and records the affected count/identifiers.
- Finalization remains unavailable until every blocker is cleared or explicitly overridden.

### 24.3 Finalize and unfinalize

- Normal finalization requires strong confirmation but no written reason.
- The transaction revalidates blockers, recalculates authoritative progress/rankings, stores immutable official placement/statistic snapshots, records actor/time, and publishes official results.
- Unfinalization requires strong confirmation and a written reason. It supersedes rather than deletes the official snapshot, returns results to provisional/final-review state, and does not reopen submissions.

### 24.4 Captain identity and emergency credentials

- Normal captain/co-captain access is a role on a website-account-linked event participant. The role and its history do not expire or disappear after the event; event/submission lifecycle rules remove mutation authority.
- No password account is generated automatically when a captain is assigned or a draft is finalized.
- An administrator may explicitly create and enable a team-scoped password-based emergency captain credential only when Discord linking is impractical. It is disabled by default and remains an audited fallback.
- Each emergency credential represents one intended captain rather than a shared identity. Any enabled Admin may create several for the same event/team, each with a globally unique login username.
- Creation exposes one hashed, single-use setup link valid for 60 minutes. The intended captain uses it to choose a password meeting the normal policy while the credential remains disabled. Reset supersedes prior setup/reset links and follows the same flow; the Admin never chooses or sees the lasting password.
- Password setup and access enablement are separate actions. The credential cannot authenticate until explicitly enabled.
- Reaching submission cutoff automatically disables every enabled emergency captain credential for that event. Reopening submissions does not silently re-enable one; an administrator must explicitly enable it again if still required.
- Existing generated captain accounts from the current implementation require migration into disabled legacy/emergency records rather than continued automatic provisioning or 24-hour post-finalization expiry.

### 24.5 Acceptance scenarios

1. Submission cutoff closes new uploads but leaves pending evidence reviewable.
2. A pending submission blocks finalization until approved or rejected.
3. Resolved statuses do not block solely because historical records exist.
4. A genuine blocker override requires confirmation/reason and does not mutate its underlying evidence.
5. Normal finalization requires confirmation but no reason and snapshots the recalculated official result.
6. Unfinalization requires confirmation/reason, preserves the old snapshot, and does not reopen uploads.
7. Website-account captain roles remain historical but cannot mutate a closed/finalized event.
8. Captain assignment/finalization never auto-generates a password credential.
9. Submission cutoff disables every enabled emergency credential; a later reopening still requires explicit audited re-enablement.

## 25. CAPTAIN-WORKSPACE-01 — Captain and co-captain workflow

### 25.1 Shared role permissions

- Captain and co-captain have the same website permissions. Their distinction is organizational and remains visible in rosters/history.
- Authority starts when the website-account-linked participant receives the event/team role and ends immediately when the membership/role is revoked.
- Neither role creates a normal password login.

### 25.2 Draft-information table

- Before and during the website draft, captains/co-captains of drafted teams see the same confirmed signup table used publicly, enhanced with all participant-submitted answer columns needed for selection.
- The signup table already includes the primary and other registered regular accounts/EHB, alt accounts, captain volunteer, and all participant-facing custom answers. Website username is not the participant's event-facing name. Captains do not receive a separate private-answer projection.
- It excludes waiting-list and withdrawn people from the draft pool and never exposes paid/unpaid status, private admin notes, identity-recovery/security metadata, or audit history.
- Captains of external/pre-formed teams do not receive the internal draft-pool expansion.
- The website draft controller/order/pick ledger remains admin-only; the expanded table does not expose private draft-control state.

### 25.3 Draft-finalization handoff

- When draft finalization publishes team rosters, the signup-board page remains the signup-board page; it does not replace its content with a roster.
- Enabled administrators retain access to that private historical/operational signup page. A non-admin request for the event signup-board route redirects to the event's published team-roster route.
- Public visitors, participants, and captains can therefore no longer browse the event signup table or its response columns after finalization.
- Captains do not retain opponent or own-team signup answers through the roster page. The roster shows only approved roster information.
- Enabled administrators retain the private participant workspace and historical responses because administration, replacement, and recovery still require them.

### 25.4 Evidence authority and grace

- An ordinary linked participant may submit only for themselves.
- A captain/co-captain may submit for any current member of their own team, including an unlinked external teammate.
- The participant is locked to themselves, while a captain/co-captain selects a current teammate. The application derives and displays that participant's current active playing account; inactive and informational/support accounts cannot be credited by the submitter.
- A captain/co-captain cannot delegate team-wide submission authority to an ordinary participant.
- After event end and before submission cutoff, captain/co-captain submission and pending-submission editing remain available for evidence claiming an in-window drop. This is the required external-team grace-period path.
- At submission cutoff, new submissions and pending edits close for normal and external teams alike. An approved admin reopening restores the window, while an emergency credential still requires separate explicit re-enablement.

### 25.5 Private team focus

- Captains/co-captains may toggle one shared focused/not-focused state on individual tiles, complete rows, and complete columns.
- Every current member of that team can see its focus state. Opponents and the public cannot.
- Ordinary administrator authority does not bypass this privacy boundary, including when that admin participates on another team.
- An ordinary admin who is also a member of the team sees that team's focus through membership, not through the admin role.
- The Super Admin sees their own team's focus through membership. Another team's focus remains hidden by default and is not included in the page, API projection, or realtime payload until the Super Admin explicitly enables **Inspect team focus** for that team.
- Cross-team inspection is read-only, visibly identified as an inspection state, and scoped to the current team/page session rather than saved as a persistent show-all preference. Disabling inspection removes that team's focus from the view.
- The Super Admin cannot edit another team's focus unless that account is also a captain/co-captain of that team.
- Multiple captains/co-captains edit the same authoritative state; each mutation uses concurrency protection and emits only team-scoped realtime invalidation.
- Focus has no color, note, timer, weight, progress, evidence, ranking, or public-history effect.
- Focus becomes read-only when the event ends; its retained state is historical team context.

### 25.6 Acceptance scenarios

1. Captain and co-captain receive the same permissions.
2. A drafted-team captain sees the confirmed draft pool in the familiar signup table with expanded answer cells but no admin-only operational data.
3. An external-team captain cannot inspect the internal draft pool.
4. Draft finalization leaves the signup page intact for admins but redirects public/participant/captain requests for that route to team rosters.
5. Admin participant management retains historical signup answers after that public handoff.
6. A participant submits only for themselves; a captain chooses any current teammate's playing account and the participant is derived.
7. An external captain can submit for an unlinked teammate during the grace period.
8. Submission cutoff blocks new/edited evidence until an explicit reopening.
9. Team members see their focus; opponents, the public, and ordinary cross-team admins do not.
10. The Super Admin sees no cross-team focus by default, may explicitly enable read-only inspection for one team, and cannot edit that team's focus solely because of the global role.
11. Two captains toggling the same focus cannot silently overwrite a newer state.

## 26. SUPERADMIN-01 — Global owner and administrator management

### 26.1 Role model and provisioning

- Exactly one active Super Admin exists at a time.
- Super Admin is a global role on a normal website account and inherits every ordinary Admin capability.
- The initial Super Admin is assigned to the owner's specified account during controlled setup or migration. Public signup, first login, and first completed onboarding can never claim the role.
- Any normal website account may later receive ordinary Admin access. Emergency/legacy captain credentials cannot become Admin or Super Admin.
- Global Admin/Super Admin roles are independent of event participation, team membership, and captain/co-captain roles.

### 26.2 Ordinary Admin management

- Only the Super Admin may grant or revoke the global Admin role. Ordinary admins cannot manage global administrator access.
- Grant and revoke use an explicit confirmation view naming the target and before/after role, then write automatic actor, timestamp, target, and before/after history. They require neither a typed username nor a written reason.
- Revoking Admin returns the target to User without disabling them or changing any event participation, captain role, character, or history. Grant and revoke invalidate the affected account's existing authenticated sessions; a stale page or cookie cannot retain the old authority.
- The current Super Admin cannot be demoted, disabled, or stripped of owner authority through ordinary Admin management.

### 26.3 Ownership transfer

- Ownership moves only through one atomic **Transfer Super Admin** action to another active, normal website account.
- The transaction promotes the destination to Super Admin and changes the previous owner to ordinary Admin. It must never commit with zero or more than one Super Admin.
- Transfer requires the current owner's password and typed destination public username, plus automatic before/after history but no written reason. Affected sessions are invalidated.
- Setup/recovery for a lost owner account is an explicit deployment/operator recovery path, not a public first-user election or a second in-product owner.

### 26.4 Cross-team focus inspection

- Super Admin membership in a team grants the same ordinary focus view as any other team member.
- For every other team, focus is hidden by default. The Super Admin must explicitly enable a read-only **Inspect team focus** mode for that team.
- Inspection is visibly identified, scoped to that team/page session, and not stored as a persistent show-all preference.
- Focus data is not loaded, embedded, or broadcast to the Super Admin before inspection is enabled. Turning the mode off removes it from the current view.
- Super Admin status alone never grants focus mutation. The account must separately be a current captain/co-captain of the target team.
- Event end makes retained focus read-only for all roles.

### 26.5 Acceptance scenarios

1. Controlled setup/migration assigns exactly one initial Super Admin; a public user cannot claim it by signing up first.
2. The Super Admin grants Admin to an existing normal website account after strong confirmation, and the automatic history identifies both accounts.
3. An ordinary Admin cannot grant, revoke, or transfer global roles.
4. Emergency captain credentials cannot become Admin or Super Admin.
5. Revoking Admin invalidates an already-issued authenticated admin session immediately.
6. Ownership transfer atomically leaves one Super Admin, makes the former owner an Admin, and records the change without requiring a typed reason.
7. The current owner cannot remove their own owner role except through a valid transfer.
8. A participating Super Admin sees only their own team's focus by default.
9. Another team's focus is absent until the Super Admin explicitly enables that team's read-only inspection mode, and disappears again when it is disabled.
10. Cross-team inspection never grants focus mutation authority.

## 27. PART-LIVE-01 — Post-draft and live participant workflow

### 27.1 Event destination and context

- After draft finalization, a signed-in participant's primary event action opens their published team roster while the board is unavailable.
- Once the board is published, the primary action opens the existing team-board view. This does not create a separate participant dashboard or redesign the approved public board.
- The participant view identifies event/team, participant or captain role, lifecycle state, planned/current playing account, and event end. The normal submission cutoff remains internal lifecycle data; pages may communicate submission availability without ordinarily displaying the cutoff timestamp.

### 27.2 Own evidence authority

- An ordinary participant may create evidence only for themselves and their current team. A captain/co-captain may instead select any current teammate.
- A new submission never offers a credited-account selector. The server snapshots the selected participant's active/drop-eligible playing account at submission time; informational accounts and inactive registered accounts cannot be chosen.
- The expected participant order is **submit the drop, then swap accounts**. A fresh submission after a swap is credited to the new active account.
- Submitter edits to a pending submission preserve its original credited participant/account snapshot. Only the approved reasoned admin-correction command may change credited account before review resolution.
- A rejected submission offers **Resubmit** through cutoff. The new form is prefilled, requires a new screenshot, and preserves the rejected attempt's credited participant/account as read-only even after a swap; ordinary tile/requirement/drop/note values may be corrected.
- Participant and captain submission creation/editing remain available after event end through the submission cutoff for screenshots showing an in-window drop. New-drop eligibility still ends at event end.

### 27.3 Submission visibility and mutation

- An ordinary participant sees their own pending, withdrawn, and rejected submissions plus rejection feedback. They may edit or withdraw their own pending submissions until cutoff.
- Approved evidence remains available through the public board.
- An ordinary participant cannot see another teammate's pending, withdrawn, or rejected evidence. Captains/co-captains retain the complete team submission view.
- Submission cutoff closes participant/captain creation and pending edit/withdraw actions. Existing history remains read-only, and admins may continue review.

### 27.4 Active-account lifecycle

- Before event start, the built-in primary playing account is displayed as the planned starting account; no participant swap is available.
- Event start activates that primary account. During `LIVE`, the participant may swap without limit among their frozen playing accounts under the approved next-whole-UTC-minute transition rule.
- While a future-effective swap is pending, the old account remains active, a second swap is blocked, and new evidence snapshots whichever account is active at the server submission instant.
- Event end closes participant/captain swaps. The final active-account state and complete history remain available for evidence review.

### 27.5 Team focus and existing board

- Current team members see their team's focus read-only on the existing team-board view. Captains/co-captains receive the controls on that same view.
- Public visitors, opponents, and ordinary cross-team admins receive the already approved public-board projection without focus data or layout changes.
- If focus markings make the board visually crowded, authorized team members receive a view-only **Show team focus** toggle and/or compact focus summary. This fallback changes only presentation and never changes the shared focus state.
- Focus becomes read-only at event end.

### 27.6 Withdrawal and notifications

- After draft start, self-withdrawal is unavailable and the participant is directed to contact an administrator.
- Admin withdrawal immediately removes future website mutation authority while preserving historical contributions/evidence under the approved eligibility boundary.
- Promotion, replacement, captain/co-captain role changes, and evidence rejection create the already approved in-site notifications with direct links to the relevant event/team/submission.

### 27.7 Acceptance scenarios

1. A drafted participant reaches their roster before board publication and their existing team board after publication.
2. A participant sees event/team, role, planned/current account, and event end; submission availability may be communicated without displaying the internal cutoff timestamp.
3. A participant submits only for themselves; the server credits their current active account without presenting an account selector.
4. A captain selects a teammate and the server derives that teammate's current active account.
5. A participant cannot submit a drop to an earlier registered account after swapping away from it.
6. A participant sees and may edit/withdraw only their own pending evidence through cutoff; teammate-private evidence remains hidden.
7. Swaps exist only during live play, use the approved effective-minute rule, and close at event end.
8. Team members see focus read-only on the existing team board while captains can mutate it; the public projection remains unchanged.
9. Event end leaves evidence upload open through cutoff while closing new-drop eligibility, swaps, and focus mutation.
10. Submission cutoff makes participant evidence history read-only without preventing admin review.
11. After draft start, self-withdrawal is rejected and directs the participant to an admin.
12. A rejected attempt can be resubmitted only through cutoff; it creates linked history, requires a new image, and preserves the original credited account after a later swap.

### 27.8 Rejected-submission recovery

- **Resubmit** is a recovery path for the same claimed drop, not a way to credit a normal new drop to an inactive account.
- The form copies the rejected submission's event/team, credited participant/account, structured selections, and note. Participant/account are displayed read-only; ordinary evidence selections may be corrected and a new screenshot is mandatory.
- The new submission references the rejected predecessor, receives a new server submission time, and is subject to the normal cutoff, authorization, board/drop, and image validation.
- The predecessor remains rejected and immutable. A uniqueness guard allows one direct child; a rejected child may start the next link in the append-only chain.

## 28. Completeness-audit lifecycle and account decisions

### 28.1 ADM-EVENT-ARCHIVE-01 — Archive finalized results

- Only a `FINALIZED` event may be archived. Archive requires strong confirmation but no written reason because it changes navigation/lifecycle presentation without rewriting competitive truth.
- Archive preserves the finalization snapshot, rosters, board, approved evidence, rejected/withdrawn evidence, participant/account history, and every public event URL.
- The event leaves the current-event position and appears under previous events as read-only public history. No public competitive data becomes private.
- Unfinalizing an archived event remains the exceptional correction path and requires the existing strong confirmation and written reason. In production it is blocked while another current/public operational event exists.

### 28.2 ADM-EVENT-CANCEL-01 — Cancel a populated pre-live event

- Empty experimental events continue to use the approved discard action. Once protected participant, team, account-access, submission, or evidence records exist, discard remains unavailable.
- Any enabled admin may cancel an event that has never entered `LIVE`, including after signup or draft preparation. The action requires strong confirmation and a written reason.
- Cancellation is terminal through the normal UI. It closes signup, suppresses every scheduled open/start/end action, disables event-scoped mutation access, and preserves all event-owned records and history.
- Cancellation never publishes content that was still private. If the event had already become public, its existing public route remains available with a clear **Event cancelled** status and only the information previously published; the private reason remains admin/audit data.
- A cancelled event is not the current event. A live event uses the approved early-end/finalization path rather than cancellation.

### 28.3 SYS-CURRENT-EVENT-01 — Production singleton and seeded scenarios

- Production may contain unlimited private drafts and unlimited archived/cancelled historical events. Multiple signup-open/closed events are permitted only for non-overlapping half-open event windows; only `LIVE`, `AWAITING_FINAL_REVIEW`, and `FINALIZED` are singleton current states.
- Signup opening/reopening fails before mutation for an overlapping operational window, while exact back-to-back windows are allowed. Signup remains discoverable only through its exact shared link.
- This is an application transition policy rather than a database uniqueness constraint. Development scenarios use an internal fixture marker which Production commands never honor.
- Development/test scenarios use explicit event IDs or slugs for navigation, review, scheduled behavior, and mutations. Code must not silently choose an arbitrary “current” scenario when several fixtures exist.
- The production exemption is unreachable from ordinary deployment configuration or public/admin input; the existing seeder remains hard-blocked outside the Development environment.

### 28.4 PUB-FEEDBACK-01 — External feedback channel

- Version one has no in-application evidence-report, bug-report, or general-feedback form.
- A community Discord feedback channel is the operational path for questionable approved evidence, bugs, and general feedback. Static Rules/how-to content may identify that channel later without creating a new application workflow.
- When reported evidence is actually wrong, admins use the approved reasoned reversal and cutoff-bound resubmission workflows.

### 28.5 ADM-ACCOUNT-01 — Disable and restore website accounts

- An enabled Admin may disable a normal `USER`; only the Super Admin may disable or restore an `ADMIN`.
- No actor may disable their own current account, and the active Super Admin cannot be disabled. Emergency captain credentials continue to use their separate event-scoped controls.
- Disable requires strong confirmation and a written reason, sets the account inactive, increments its authorization version, and invalidates every existing session immediately.
- Disabling access never deletes or transfers event participants, roles, character links, submissions, evidence, contributions, historical display snapshots, the reserved public/login username, or the current Discord association. The disabled account cannot authenticate and another account cannot claim either identifier. If it removes the only usable Captain access for a team, the normal readiness blocker or live missing-captain warning applies.
- Re-enabling requires confirmation and automatic before/after history but no written reason. It restores authentication only; event lifecycle and current membership/role rules still determine authority.
- Version one does not merge or permanently delete normal website accounts. Development/test identities are removed through the clean reset/deployment process.

### 28.6 PART-HISTORY-01 — Participant access after archive

- Archived public boards, teams, standings, approved evidence, and final results remain available through their normal public routes.
- A signed-in participant may additionally read their own rejected and withdrawn submission history and rejection feedback for an archived event.
- All participant/captain mutation controls, swaps, focus changes, pending edits, withdrawals, resubmission, and uploads are absent.
- No separate post-event participant dashboard is required; event history and the existing public board remain the primary destination.

### 28.7 Acceptance scenarios

1. Archiving a finalized event moves it from current navigation to previous events without changing its official results or URLs.
2. Production refuses to publish/open a second operational event and identifies the existing blocker.
3. Development seeding still creates and exposes several labelled lifecycle scenarios through explicit routes.
4. A populated pre-live event can be cancelled without deleting participant, draft, board, or audit history; no scheduled transition later reactivates it.
5. Cancelling a never-public event publishes nothing, while cancelling an already-public event retains a generic public cancellation page without exposing the private reason.
6. The application contains no public feedback/report form; a reported bad approval is handled through reversal and resubmission.
7. An Admin can disable a User but not an Admin, self, or Super Admin; the Super Admin can disable another Admin.
8. Account disable invalidates sessions while preserving every historical event record, and re-enable does not resurrect expired event authority.
9. An archived participant can read their own rejected/withdrawn evidence but cannot mutate any event state.

## 29. Global administration outside one event lifecycle

### 29.1 ADM-CATALOGUE-01 — Maintain the OSRS catalogue

- Any enabled Admin may create, edit, deactivate, or reactivate a boss/activity or source-specific drop. Item identities are created and edited through their source drops; they have no standalone workflow. Routine mutations record actor/time/before-after history without requiring a typed reason.
- Referenced catalogue records are never hard-deleted. Deactivation prevents new selection while preserving current draft references and every approved/published historical board.
- Only the Super Admin receives a permanent **Delete** action. It requires strong confirmation and succeeds only when the complete dependency check finds no source-drop, board, asset/cache, import-review, or historical reference. A blocked deletion lists the references and offers deactivation instead. No written reason is required for deletion of a genuinely unused record.
- Catalogue mutations use optimistic concurrency. A stale edit/import returns the current values instead of overwriting them.
- External image-source URLs remain exclusive to catalogue bosses/items and continue through the validated cache/fetch path.

### 29.2 Live draft-board catalogue coupling and approval snapshot

- A `DRAFT` board stores stable catalogue identifiers and board-owned objective configuration, but catalogue names, images, efficient rates, source-drop rates, and calculated EHB remain live derived values.
- Editing relevant catalogue data invalidates/recalculates every affected unapproved board projection. An open board editor receives an ordinary invalidation and reloads the authoritative tile/row/column/total EHB; it never silently keeps a stale draft calculation.
- **Approve board** locks/rechecks the complete board and referenced catalogue versions in one transaction, calculates every EHB value, and creates the first immutable competitive approval snapshot. The board then becomes `VALIDATED`.
- While `VALIDATED`, later catalogue changes do not alter the approved snapshot. Explicit unapproval or editing competitive board content returns the board to `DRAFT`, retains the superseded approval snapshot/history, and resumes live catalogue derivation. A later approval creates a new immutable version.
- Publication uses the currently active approval snapshot without recalculating from the catalogue. Post-publication corrections retain the already approved exceptional history/replacement rules.
- The board editor retains **Preview board**. Preview renders the actual public-board visual treatment without admin-only EHB/edit controls and never approves or publishes. Draft preview uses current live derived catalogue data; validated preview uses the frozen approval snapshot.

### 29.3 ADM-CATALOGUE-IMPORT-01 — Bulk import

- Bulk catalogue preview and apply are Super-Admin-only. Ordinary Admins receive no route, control, or callable handler for either operation.
- Preview is read-only and reports exact additions, edits, reactivations, deactivations, unresolved conflicts, and blocked deletions.
- Apply requires strong confirmation but no written reason. It revalidates the preview version/hash and aborts when catalogue data changed after preview rather than applying a stale plan.
- Import never hard-deletes a referenced record. It may deactivate obsolete rows and records complete automatic before/after audit history.

### 29.4 ADM-AUDIT-01 — Audit log

- Every enabled Admin may view audit history. Entries are immutable and cannot be edited or deleted through the application.
- The page is a newest-first server-paginated table with exactly 25 entries per page and navigation to older/newer pages.
- Filters cover event, actor, action, entity type/identifier, and date range. Changing filters returns to page one; pagination retains active filters.
- Audit retention is independent of the 25-row display size. The full retained history remains queryable.
- Version one has no audit export. Snapshots and request context continue to exclude passwords, hashes/tokens, OAuth secrets, evidence credentials, and unnecessary raw Discord identifiers.
- Opening an entry renders its structured before/after values as readable labels and values without replacing or mutating the underlying structured record.
- Routine successful website-account login updates last-login state but does not flood the main audit table. Failed attempts/throttling remain security logs. Successful emergency-credential use and security-sensitive password, Discord, role, disable/restore, ownership, and emergency-access mutations remain durable audit events.

### 29.5 ADM-ACCOUNT-OVERVIEW-01 — Account overview

- One **Accounts** area separates normal website accounts from emergency credentials as two logical datasets; their eventual tab/section presentation follows the shared UI pass. Each uses server-side search/filtering and 25-row pagination.
- Website-account search/filters cover public username, global role, active/disabled state, Discord linked/unlinked state, and event participation. Rows show username, role, state, Discord-link state, last login, and current event-role summary.
- Website-account details include every linked OSRS character, last-known Discord display name labelled non-authoritative, event participation, current/historical event/team roles, and disable history.
- Emergency-credential rows show login username, event, team, setup state, enabled state, last login, cutoff state, and authorized setup/reset/enable/disable actions.
- The overview never exposes password data, OAuth data, setup/reset-token values/hashes, or unnecessary raw Discord identifiers.
- Role, reset, disable, ownership-transfer, and participant-ownership actions appear only when the current actor has the separately approved authority. The list itself grants no additional mutation permission.
- Version one has no website-account merge or permanent account deletion. Wrong participant ownership uses the event-scoped transfer workflow; Discord loss uses same-account relinking.
- Grant/revoke Admin and account restoration notify the affected account in-site. Disablement exposes only neutral contact-an-admin guidance to that account; the private reason remains Admin/audit data.
- Lost-owner recovery is operator-only with no web action. It may generate a 60-minute owner reset link or atomically transfer ownership using explicit source/destination identifiers and confirmation, and it records a system audit event.

### 29.6 ADM-INBOX-01 — Notifications and unresolved actions

- Personal notifications are durable recipient-specific messages with unread state and a direct route. Opening a notification marks it read; unread counts update without changing the underlying business record.
- The Admin overview separately projects unresolved operational actions such as pending evidence, postponed starts, waiting-list promotions needing follow-up, team vacancies, and missing captains.
- Marking or opening a personal notification never dismisses an unresolved action. An action disappears only when its authoritative underlying condition is resolved.
- Audit activity and routine configuration changes do not flood either surface.

### 29.7 Acceptance scenarios

1. An Admin edits a boss rate and every affected unapproved board recalculates; an approved board remains unchanged.
2. Approving a board captures a versioned snapshot, and unapproving it restores live catalogue derivation without deleting the superseded snapshot.
3. Preview matches the public board treatment but neither approves nor publishes.
4. An Admin can deactivate a referenced catalogue record but cannot delete it; the Super Admin can delete a genuinely unused record after confirmation.
5. Ordinary Admins cannot access bulk import preview/apply; the Super Admin cannot apply a stale preview.
6. The audit table shows 25 newest entries, preserves filters through pagination, reaches older records, renders structured details readably, and offers no edit/delete/export.
7. Account overview separates website and emergency records, exposes all linked OSRS characters and useful identity/role state without security secrets or account merge/delete actions, and authorizes every action independently.
8. Reading a notification clears its unread state while a corresponding unresolved admin action remains until the business condition is fixed.

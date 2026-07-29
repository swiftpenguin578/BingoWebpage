# Slice 4 — Signup System Plan

**Status:** Accepted on 2026-07-29. Passes 4.1–4.7, consolidated manual acceptance, independent review and restricted re-reviews, clean/retained migration and Development-reset rehearsals, obsolete-path audit, Release/EF/format/diff gates, and the complete `432/432` automated suite passed. External/pre-formed roster CSV remains a future Slice 5 requirement and is not implemented here.

**Depends on:** Accepted Slice 3 (`codex/milestone-8a-slice-3`), `PRODUCT_REQUIREMENTS.md`, `FUNCTIONAL_WORKFLOWS.md`, `DATA_MODEL.md`, `TECHNICAL_ARCHITECTURE.md`, `IMPLEMENTATION_ROADMAP.md`, and `UI_OVERHAUL_ROADMAP.md`.

## 1. Objective

Slice 4 replaces the fixed, largely unauthenticated signup workflow with one versioned and authenticated signup system:

- admins configure a reusable event signup form;
- normal participants sign up through their website account and select OSRS characters from My accounts;
- every registered regular account has an event-specific EHB snapshot;
- capacity, waiting-list, withdrawal, restoration, and promotion rules are transactional;
- an unlisted public signup table shows confirmed and waiting participants without exposing website usernames or private administration data;
- participants reach current and historical event destinations through **My events**;
- admins manage participants, ownership, payment, notes, and corrections from one workspace;
- temporary fixed-form and private-edit compatibility is removed only after the replacement paths are active.

The participant's event-facing identity is their primary registered OSRS character. Website username remains an account/login concern and is never a public event identity.

## 2. Explicit boundary

### Included

- Versioned signup forms and typed questions.
- Built-in primary regular account/EHB and captain-volunteer fields.
- Additional regular-account and alt-account questions.
- Authenticated signup creation, editing, confirmation, cancellation, and withdrawal.
- My Accounts selection and saved-EHB integration.
- Event-specific account reservations and concurrency handling.
- Capacity, waiting list, deterministic promotion, Admin withdrawal/restoration, and notifications.
- Unlisted public signup table.
- **My events** current/history navigation.
- Admin participant workspace, ownership transfer, corrections, payment, and notes.
- Final removal of fixed-form/private-edit compatibility.
- Development TEST 13 and TEST 15 fixture updates.

### Deferred

- Wise Old Man EHB fetching — Slice 10.
- Team/captain/external-roster redesign and final draft behavior — Slice 5.
- Board derivation/publication changes — Slice 6.
- Live active-account swapping — Slice 7.
- Evidence changes — Slice 8.
- Post-draft replacements and broader finalization behavior — Slice 9.
- Whole-site visual overhaul — Milestone 9.

### Preserved

- Slice 3 lifecycle/readiness services and event-state policy.
- Event-character uniqueness and history from Slice 2.
- Explicit unowned imported/external participants.
- Protected public-board, board-editor, and live-draft interaction models.
- Ordinary Razor/no-JavaScript fallbacks.
- Historical migrations and competitive history.

## 3. Approved product rules

### 3.1 Question types and terminology

Participant-facing question types are:

- **Text** — one multiline text type;
- **Number**;
- **Yes/No**;
- **Single choice**;
- **Account**.

Account questions have one event role:

- **Regular account** — may later be active, receive drop credit, and participate in Wise Old Man workflows; requires an EHB value when answered.
- **Alt account** — signup information only; has no EHB and can never be active, credited, or synchronized.

The internal persisted role may retain its existing Playing/Informational enum names. Those implementation names must not appear in participant-facing UI.

Every secondary Account question is optional.

### 3.2 Built-in fields

Every form contains:

1. required primary **Regular account**, with its required EHB;
2. **Captain volunteer**, always present.

There is no built-in second account, alt, Discord name, comments, availability, or support-alt question.

The primary regular account automatically becomes the participant's initial active account at event start. Signup contains no separate initial-account selector.

### 3.3 Public-answer contract

All participant-facing custom answers are public on the unlisted signup table.

- Do not expose a public/private toggle in version one.
- Do not add admin-only custom signup questions.
- Do not add a post-draft question-privacy workflow.
- Keep the underlying visibility field only as a future-compatible value fixed/defaulted to public.
- Payment and Admin notes remain separate private administration fields.

Account columns use generated headings rather than organizer-authored labels:

- one regular account: **Account**;
- multiple regular accounts: **Account 1**, **Account 2**, and so on;
- one alt account: **Alt account**;
- multiple alt accounts: **Alt account 1**, **Alt account 2**, and so on.

Regular-account EHB appears with its account. Website username and Discord identity never appear on the public signup table.

### 3.4 Form versioning and locking

- A private or closed form may be edited.
- Before the first accepted website or Admin participant response, custom questions may be freely edited, reordered, or deleted.
- The first accepted response permanently locks the answer shape of every existing question.
- After that boundary, newly added participant questions must be optional.
- Type, account role, choices, stable key, system identity, and other answer-shape rules are immutable.
- Label, help text, and order may change while signup is closed and the draft has not started.
- A structural replacement disables the old question and creates a new optional question with a new stable key.
- Existing answers and historical form versions remain readable.
- Missing answers to later optional questions render **Not answered**.
- Ordinary question editing freezes when the draft starts.
- Every definition change increments the form version and records structured history.

### 3.5 Account selection and EHB

- Account answers select from the authenticated user's My accounts links; they are not free-text OSRS names.
- If an account is missing, the form links to My accounts and preserves or clearly restores the signup journey.
- Active linked characters and characters already registered to this participant in the event are valid edit options.
- Unlinking or transferring a My Accounts link never removes an existing event assignment.
- Replacing an existing assignment requires a currently linked, available character.
- The preferred linked character is offered first for the primary account.
- A regular account's EHB is prefilled from that website account's saved My Accounts EHB.
- Saving a regular-account answer updates that saved default and captures an independent event snapshot atomically.
- Later My Accounts EHB edits do not rewrite the signup snapshot.
- Editing and saving the signup uses the current saved EHB for the selected regular account unless the participant supplies another valid value.
- Alt accounts never capture EHB.
- Confirmed and waiting participants reserve every selected character equally.
- One normalized OSRS character may belong to only one current participant in the same event, regardless of spelling/case or global links.

### 3.6 Participant status and capacity

- One website account may own at most one participant record per event.
- Below capacity, a valid signup becomes Confirmed.
- At capacity with waiting list enabled, it becomes Waiting and receives an exact deterministic position.
- At capacity with waiting list disabled, the submission fails atomically.
- Confirmed and Waiting participants reserve accounts.
- While signup is open, a confirmed participant may cancel; reservations are released. Rejoining reuses the participant history but receives a new timestamp/sequence at the end.
- After signup closes and before draft lock, a participant may withdraw but cannot self-restore.
- After draft start, there is no participant self-withdrawal; later exceptions belong to Slice 5.
- Capacity can only increase once signup is public.
- Increasing capacity promotes the earliest waiting participants.
- A pre-draft confirmed cancellation/withdrawal promotes the earliest waiting participant.
- Automatic promotion stops at draft lock.
- Admins may withdraw or restore pre-draft.
- Restoration reacquires every account, uses current capacity or the end of the queue, and never displaces an earlier promotion.
- Participant status has one inactive **Withdrawn** state. Actor, time, and history distinguish participant/Admin actions; no written reason is required.

### 3.7 Signup and table routes

- Normal signup requires an authenticated website account.
- Login/onboarding returns to the intended event.
- Signup remains an exact-link/unlisted journey.
- A separate stable public route exposes the signup table, for example `/Events/{slug}/Signups`.
- The table is not listed on the homepage, public-board overview, or general navigation.
- Admins can copy both the form link and table link.
- Signup and confirmation pages link to the table.
- The table has separate Confirmed and Waiting sections and exact waiting positions.
- Before signup publication, the table is unavailable.
- During signup/open/closed/draft, the table remains available by exact link.
- After draft finalization/publication, non-Admin signup/table requests redirect to the team roster; Admins retain the table.
- Cancelled visibility follows Slice 3.
- Live/final/archived destinations favor roster, board, and results.
- The public-board route remains the event overview; it does not automatically redirect to a live board.

### 3.8 My events

**My events** is always present in normal authenticated account navigation.

- It uses explicit `EventParticipant.AccountId` ownership only.
- It contains **Current events** and **History**.
- It shows every owned participant record and accurate status.
- It links to the best available state-specific destination: confirmation/edit, signup table, roster, board, or results.
- Imported/external unowned participants do not appear until an explicit ownership transfer.
- It is the long-term entry point for both active and historical participation.

### 3.9 Admin participant workspace

The Admin workspace provides:

- Confirmed, Waiting, and Withdrawn counts and sections;
- filters for status, Paid/Unpaid, Discord link, captain volunteer, source, and team;
- Admin-only current website username and Discord-link state;
- signup time/order, queue position, accounts/EHB, alt accounts, public answers, payment, notes, and team/draft state;
- pre-draft corrections using the same validation, reservation, and transaction rules without changing queue order;
- internal participant creation that bypasses the public window/code but not validation, uniqueness, capacity, or waiting-list rules;
- separate handling for external rosters.

Payment is a binary private **Unpaid/Paid** value. Admin notes are private.

Ownership transfer requires strong confirmation:

- destination is an active normal website account not already participating in the event;
- participant, status, order, answers, assignments, team, evidence, and history stay unchanged;
- previous access is revoked and new access granted immediately;
- My Accounts links are neither moved nor created;
- old/new account notifications are created when applicable;
- structured before/after history is retained without a written reason.

### 3.10 External-roster CSV scope

Ordinary participant CSV import is intentionally outside Slice 4. CSV, if delivered, belongs to Slice 5's external/pre-formed team roster workflow; its approved fields and detailed behavior remain to be defined there. Retained legacy/compatibility CSV paths are not repurposed or removed by this plan.

## 4. Interaction rules

Every new or materially changed page follows `UI_OVERHAUL_ROADMAP.md` from its first implementation. Full visual rework remains Milestone 9.

Save on change where one independent value can be safely committed:

- payment status;
- filters;
- question order;
- waiting-list toggle, with confirmation when its consequences require it;
- explicit private Admin-note saves with visible Saving/Saved/Error state.

Use explicit actions for:

- signup create/edit;
- account corrections;
- question creation or multi-field definition edits;
- signup-code changes;
- withdrawal/restoration;
- ownership transfer;
- other consequential, destructive, or multi-field workflows.

Every enhanced action waits for server acceptance, prevents duplicate submission, gives accurate feedback, preserves useful context/history, and retains a standard Razor fallback.

Public/User participant pages and feedback are English/Danish. Admin-only pages may remain English.

## 5. Implementation passes

### Pass 4.1 — Persistence foundation and retained conversion

**Implementation status:** Implemented. Migration `20260727223107_AddSlice4SignupPersistenceFoundation` creates and backfills forms/questions/answers/assignment links deterministically, preserves legacy compatibility data, and adds relational/trigger boundaries. No participant workflow replacement is included.

Implement:

- one versioned signup form per event;
- question system identity, type, role, help, stable key, order, forced-public visibility, disabled/replaced history, and optimistic concurrency;
- Account answers referencing `OsrsCharacter`;
- assignment-to-Account-question relationship;
- first-response/structural-lock marker;
- binary payment;
- unified Withdrawn status/history.

Retained conversion:

- create built-in primary Account and captain-volunteer fields;
- link primary assignments to the primary question;
- preserve secondary assignments through a disabled legacy alt/informational Account question;
- preserve typed Discord/comments as disabled historical answers;
- convert custom questions, collapsing short/long text into Text;
- Paid maps to Paid; every other old payment value maps to Unpaid;
- Removed maps to Withdrawn;
- never infer website ownership.

Gate:

- no visible workflow change;
- clean and representative retained PostgreSQL migrations pass;
- constraints cover one form/event, stable keys, answer type/reference rules, assignment question linkage, and concurrency;
- no historical record or ID is silently rewritten.

### Pass 4.2 — Authenticated signup and My events

**Implementation status:** Approved. The authenticated renderer creates or updates the account-owned participant atomically without issuing a private token. It preserves status/order/timestamps on edit, snapshots regular-account EHB and updates the active My Accounts default, permits a still-current event assignment after its My Accounts link is unlinked, and restricts replacement choices to active links. Confirmation projects only owned participant/signup data; My Accounts preserves safe local signup/confirmation return routes; and My events uses only explicit ownership with lifecycle-aware destinations.

Implement the versioned participant renderer and My events before the full builder can create every supported definition.

Gate:

- authenticated creation/edit confirmation works;
- intended return after login/onboarding works;
- My Accounts selection, preferred default, event reservations, EHB default/snapshot update, duplicate-account rejection, and conflict rollback are transactional;
- form input survives expected failure;
- editing exists only while SignupOpen;
- new signups receive no private token;
- My events current/history and state destinations use explicit ownership;
- English/Danish, narrow, keyboard, feedback, and no-JavaScript paths are manually accepted.

### Pass 4.3 — Admin form builder and readiness

Implement the complete builder, preview, signup code, waiting-list setting, versions, structural locking/replacement, generated public headings, and signup-readiness extensions.

**Implementation handoff:** The Questions workflow presents persisted system/custom questions and a non-mutating preview, normalizes choice lists, supports Account roles and code settings without disclosing a hash, and evaluates malformed definitions safely. Definition changes are transactional and audited with one form-version increment. Before the first response custom questions may be edited/reordered/deactivated; afterwards additions are optional and only presentation/order edits are direct. The replacement action, while signup is closed and before draft start, retains the original and answers as disabled history and creates a new optional custom question with a new key.

Gate:

- every supported question definition renders and validates through the participant path;
- unsupported/stale definitions fail readiness safely;
- first-response locking applies to website and Admin participant responses;
- forced-public answer behavior and private payment/notes boundary are verified;
- no hash/secret is exposed;
- visual/manual acceptance covers pre-response edits, post-response locks, replacement history, preview, and feedback.

### Pass 4.4 — Capacity, waiting list, and participant lifecycle

**Implementation status:** Approved. The service locks the event row for every capacity/lifecycle mutation, derives waiting order from immutable signup time/sequence, releases/reacquires assignment history atomically, promotes only pre-draft confirmed vacancies, and writes durable participant/audit/notification records in the same transaction. Participant confirmation and the existing Admin participant page expose explicit lifecycle confirmation controls. No migration was required.

Gate:

- queue order and exact position are deterministic;
- promotion is atomic and idempotent;
- every reservation is released/reacquired correctly;
- participant/Admin permissions match event state;
- promotion and Admin lifecycle notifications are delivered once;
- no post-draft mutation is introduced.

### Pass 4.5 — Public signup table and My events destinations

Implement the exact-link table and state-aware route handoff.

**Implementation status:** Approved. `/Events/{slug}/Signups` projects only active confirmed/waiting event-facing OSRS accounts, frozen regular-account EHB, captain volunteering, and question-answer history. A shared destination policy keeps the table available from first publication through pre-live drafting, sends normal users to an actual finalized-team roster/board afterward, and preserves current enabled Admin/Super Admin historical-table access. The discoverable Public boards overview uses the same roster/board facts: roster-first before board publication, board-first after it, without changing event lifecycle. A shared presentation-only phase keeps Public boards, My events, and Admin cards aligned without introducing a lifecycle state: Signups closed, Draft finalized, safe Board published wording, or the Admin-only full-readiness / postponed-start result. My events uses the same policy and explicit website-account ownership.

Gate:

- public output contains only approved OSRS/event data;
- website usernames, Discord identity, payment, notes, security data, and audit data never appear;
- historical versions and disabled questions remain understandable;
- waiting positions and headings are correct;
- signup/table/roster/board/results redirects match lifecycle and Admin authority;
- My events destinations match the same policy.

### Pass 4.6 — Admin workspace and transfer

Implement Admin projections, filters, immediate payment/notes behavior, corrections, internal creation, and ownership transfer.

Gate:

- every dataset/filter is server-authoritative and paginated where needed;
- corrections and transfers are authorized, audited, atomic, and concurrency-safe;
- notifications follow the approved mutation matrix;
- sensitive values remain private;
- enhanced and no-JavaScript paths agree.

### Pass 4.7 — Compatibility removal and final acceptance

**Second restricted-review remediation (not accepted):** retained legacy Discord questions normalize to disabled custom history (`SystemField.None`) while retaining their answer/key/order and remaining non-public. Participant answer/assignment forms require a persisted participant-response revision, distinct from form definition version; stale or missing authenticated/Admin tokens fail with localized reload feedback and no mutation. Manage and Draft use the single lifecycle withdrawal transaction, including enabled Admin/Super Admin promotion notices. Captain volunteer is projected as canonical `true`/`false`. Final runtime question vocabulary is Text, Number, Yes/No, Single choice, and Account. My Events chooses signup/confirmation before roster, roster before a published board/results destination, then board/results for live through archived history. These repairs require another restricted re-review and do not reopen Pass 4.7 compatibility scope.

Remove:

- private edit-token field/index/service/issuance/replacement/route;
- `Allow private signup editing`;
- fixed primary/second account request model;
- typed Discord/comments signup fields;
- obsolete payment values;
- Removed participant state/handlers;
- fixed-form services and projections superseded by the new system.

Retain:

- historical migrations;
- explicit unowned imported/external participants;
- historical answers and released assignments;
- protected board/editor/draft surfaces;
- no-JavaScript behavior;
- explicitly deferred functionality.

Final gate:

- bounded obsolete-path search;
- clean and retained PostgreSQL migration rehearsal;
- focused authorization, transaction, concurrency, privacy, localization, route, and lifecycle coverage;
- durable manual checklist;
- independent full-Slice review and remediation;
- one complete suite only after review stabilizes;
- Release build, formatting, EF model check, diff/leak checks;
- logical commits and push only after acceptance.

## 6. Development fixtures

Development reset remains idempotent and creates only TEST 13 and TEST 15.

**TEST 13**

- far-future Draft/pre-signup event with times derived from reset time;
- representative final Slice 4 form using every supported question type and both Account roles;
- useful My Accounts data and explicit ownership;
- no obsolete fixed-form/private-token data.

**TEST 15**

- remains the protected Live public-board visual baseline;
- active window is derived relative to reset time rather than hardcoded dates;
- retains the internal Development marker so it does not create production lifecycle conflicts;
- data is updated to final Slice 4 ownership, questions, assignments, teams, and evidence semantics.

Fixture compatibility may be updated during each pass. Pass 4.7 performs the final semantic cleanup.

## 7. Review and execution discipline

- Implement one pass per task.
- Use Terra Medium for ordinary implementation; use Terra High only where migration, concurrency, security, or transaction risk warrants it.
- Run the smallest focused verification proving each pass.
- Do not repeatedly expand tests without a changed risk surface or concrete failure.
- Do not run a complete solution suite at every pass.
- After each pass, update `CURRENT_STATUS.md`, provide exact focused results, and obtain the required manual approval before starting the next pass.
- Perform one independent review after Pass 4.7, restricted follow-ups for concrete findings, then one final complete automated gate.
- Do not stage, commit, or push before Slice 4 acceptance.

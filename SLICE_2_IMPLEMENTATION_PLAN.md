# Slice 2 — My Accounts and Event Character Assignment Plan

**Status:** Slice 2 implementation in progress; Passes 2.1–2.3 are complete.

**Prepared:** 2026-07-26

**Depends on:** `PRODUCT_REQUIREMENTS.md`, `FUNCTIONAL_WORKFLOWS.md`, `DATA_MODEL.md`, `TECHNICAL_ARCHITECTURE.md`, `IMPLEMENTATION_ROADMAP.md`, `UI_OVERHAUL_ROADMAP.md`

## 1. Objective

Slice 2 completes the character foundation introduced by Slice 1:

- a global **My Accounts** list for each website account;
- trust-based links to OSRS characters without exclusive ownership claims;
- an optional saved EHB default on each website-account/character link;
- case-insensitive OSRS-character and website-username uniqueness;
- event-specific playing and informational character assignments;
- one current participant assignment per OSRS character in an event;
- deterministic migration of the current primary/secondary account fields;
- an independent website-username rename workflow.

Slice 2 changes the data structure underneath existing event workflows. It does not deliver the final authenticated signup form or redesign protected event surfaces.

## 2. Explicit boundary

### Included

- Add, relink, unlink, label, order, and prefer My Accounts characters.
- Separate the existing onboarding username input from the required first OSRS-character input.
- Store an optional saved EHB default per website-account/character link.
- Correct a misspelled linked character through an atomic relink.
- Propagate a spelling correction into that account's currently editable signups.
- Preserve closed, locked, live, and historical event assignments.
- Add event participant ownership and event character assignments.
- Migrate current primary account/EHB and optional secondary account values.
- Move existing fixed signup, edit, CSV, participant-admin, draft, roster, and affected evidence projections to the new assignment authority without redesigning them.
- Change a website username independently of OSRS characters.
- Preserve the public board, board editor, live draft, and Pass 12 interaction baselines.

### Deferred

- Authenticated signup ownership, final account dropdowns, and removal of private edit links — Slice 4.
- Dynamic Account questions and public signup-board redesign — Slice 4.
- Wise Old Man EHB fetching — Slice 10.
- Active/drop-eligible account swaps and swap history — Slice 7.
- External-team roster redesign, captain authority, and draft changes — Slice 5.
- Evidence workflow changes — Slice 8.
- Full site-wide UI overhaul and regression — Milestone 9.

### Rejected for Slice 2

- Global Main/Alt/Ironman/game-mode classifications.
- Participant claim invitations or claim links.
- Automatic participant ownership inference from website username, OSRS name, or typed Discord text.
- Linking an emergency team credential to a participant or OSRS character.
- Inline creation of a My Accounts character inside the final signup transaction.

## 3. Approved behavior

### 3.1 Global character identity

- OSRS names are trusted user input. Trim surrounding whitespace and compare normalized values case-insensitively without syntax, ownership, availability, membership, or existence lookup.
- `Calm Chris` and `calm chris` resolve to one global `OsrsCharacter`.
- Several website accounts may link the same global character because borrowing is trust-based.
- One website account has at most one link to a particular character.
- My Accounts is a flat ordered list. Optional labels such as `Main`, `Alt`, or `Borrowed` are personal presentation metadata only.
- At most one active link is preferred. Preferred controls only the initial selection in future signup account selectors.
- A website account may have zero active links.

Initial Discord onboarding collects two separate required values:

- a website username used for profile/login identity;
- a first OSRS character added to My Accounts and marked preferred.

The UI may explain that most people use their primary OSRS character as the website username, but the values are not required to match. Exact-OSRS-name guidance applies only to the character field.

### 3.2 Saved EHB default

- Every My Accounts link has an optional saved EHB value.
- Saved EHB belongs to the website-account/character link, not the shared global character, so one borrower's changes do not mutate another person's default.
- A playing Account answer copies the current saved EHB into an event-specific snapshot.
- An informational Account answer records no EHB even when the My Accounts link has a saved value.
- Editing saved EHB in My Accounts never silently updates an already submitted signup.
- When a signup is edited, its playing EHB control is prefilled from the latest saved My Accounts value; if that value is empty, the existing event snapshot is the fallback.
- Opening the edit form does not mutate data. Saving it updates both the saved My Accounts EHB and the event snapshot in one transaction.
- Signup closure freezes the event snapshot for participant editing.

The final dropdown and edit-form behavior is delivered in Slice 4. Slice 2 provides the storage, event snapshot, and compatibility foundation.

### 3.3 Linking, unlinking, and spelling correction

- Adding a character creates or reactivates the account/character link.
- Unlinking removes it from future My Accounts selectors but never deletes the character or an event assignment.
- If an unlink affects an upcoming or live registration, show a confirmation that the event registration remains.
- Signup editing and later live account selection must distinguish frozen registered characters from currently linked My Accounts characters.
- Historical assignments, evidence, eligibility, and activity never cascade from a global unlink.

A spelling correction is not a global rename because another account or historical event may legitimately reference the previous character row. It is an atomic relink:

1. Normalize and find or create the corrected global character.
2. Preflight every affected signup that is currently participant-editable.
3. Reject the complete correction if the corrected character is already assigned to another participant in any affected event.
4. Preserve the link's label, order, preferred state, and saved EHB.
5. Replace the character in every affected open signup assignment.
6. Leave closed, draft-locked, live, and historical assignments unchanged.
7. Commit the link and eligible event updates together.

No partial correction is allowed. Conflict feedback identifies the affected event without exposing another participant's private information.

### 3.4 Event assignments

- An event participant may have several registered characters.
- Assignments are `PLAYING` or `INFORMATIONAL` only for that event; My Accounts has no corresponding permanent type.
- The built-in primary assignment is playing and supplies draft EHB.
- A secondary playing assignment has its own EHB snapshot but never replaces or adds to the primary draft value.
- An informational assignment has no EHB and is excluded from swaps, evidence credit, and Wise Old Man.
- One current assignment per normalized OSRS character is allowed in an event across confirmed and waiting-list participants.
- Withdrawal before draft start releases current reservations while retaining assignment history.
- Event assignments survive a later global unlink.

### 3.5 Website account and event ownership

- Website username and OSRS character are independent identities.
- Normal authenticated signup in Slice 4 sets the event participant's website-account ID directly.
- Existing/imported rows with no verified website-account ID remain valid unowned roster records.
- Migration never guesses ownership from a matching website username, OSRS name, or free-text Discord value.
- External roster members may remain unowned permanently.
- External team submission uses one or more explicit team-scoped emergency credentials; those credentials do not own roster participants and do not receive participant-only focus functionality.
- Participant identity transfer and broader participant administration remain Slice 4.

### 3.6 Website username rename

- A user may change the website username to any valid unused value; it does not need to match a linked OSRS character.
- Normalize username uniqueness case-insensitively, so `Calm Chris` and `calm chris` cannot belong to separate website accounts.
- Require the current password and explicitly state that the new value becomes the password-login username.
- Reissue the current browser session with the new name.
- Do not invalidate unrelated sessions solely for a username rename; they remain authorized and receive the new display/login name after their next authentication refresh.
- Audit the old and new normalized-safe values without recording the password.
- Do not alter My Accounts links, event assignments, team roles, evidence, or event-facing names.
- Event pages display registered OSRS characters, not the website username.

## 4. Target persistence changes

### 4.1 AccountOsrsCharacter

Complete the Slice 1 foundation with:

- `linked_at`
- `linked_by_account_id`
- `unlinked_at`
- `personal_label`
- `sort_order`
- `preferred`
- `saved_ehb`, nullable
- timestamps/concurrency data required by the implementation

Retain one row per account/character pair and reactivate it rather than creating duplicate history.

### 4.2 EventParticipant

Add:

- nullable `account_id`;
- a partial/nullable uniqueness boundary for one participant per `(event_id, account_id)`;
- relational ownership suitable for later account-based signup authorization.

Do not add claim-token persistence.

### 4.3 EventParticipantCharacter

Add the event assignment described in `DATA_MODEL.md`, including:

- event, participant, and character IDs;
- registration order and actor/time;
- source signup question;
- `PLAYING` or `INFORMATIONAL` role;
- nullable EHB snapshot plus source/fetch metadata;
- release actor/time;
- the event-level current-character uniqueness boundary.

Do not create active-character swap rows in Slice 2.

### 4.4 Migration

- Backfill the current primary account as the primary `PLAYING` assignment with its existing EHB.
- Backfill the current optional second account as `INFORMATIONAL` with no EHB.
- Do not create My Accounts links or participant ownership by guessing names.
- Preserve account and participant IDs.
- Retain old columns only until all active reads and writes move to the new authority.
- Remove obsolete columns in the same slice once compatibility conversion is verified.

Production will start with a clean database while retaining the catalogue. Retained migration rehearsal remains required for deterministic engineering safety, but Slice 2 must not build a claim system for disposable development participant data.

## 5. Bounded implementation passes

### Pass 2.1 — Persistence foundation

Implement only:

- the completed global link model and saved EHB;
- event participant ownership;
- event character assignments and constraints;
- deterministic migration/backfill;
- focused domain and PostgreSQL migration tests.

Do not add My Accounts pages or alter visible signup workflows.

**Gate**

- Character/link invariants pass.
- Case-insensitive character uniqueness passes against PostgreSQL.
- Event current-assignment uniqueness passes against PostgreSQL.
- Clean and representative retained migrations succeed.
- Release build for affected projects passes.

### Pass 2.2 — Existing workflow authority transition

Move the current fixed UI and services to the new authoritative assignments:

- current signup creation and private-link editing;
- Admin participant correction;
- CSV import;
- signup/roster projections;
- draft primary name and EHB;
- affected evidence/account projections.

Keep the current visible primary/second-account form until Slice 4. Map it internally to primary `PLAYING` plus optional `INFORMATIONAL`. Preserve private edit links until Slice 4.

Remove the superseded participant account-name/EHB fields after bounded search proves there are no active reads or writes. Preserve migration history.

**Gate**

- Focused create/edit/import/admin/draft/projection regressions pass.
- The protected public board, board editor, and live draft retain their current composition and interaction behavior.
- No active code reads the removed legacy authority.
- Release build and formatting pass.

### Pass 2.3 — My Accounts

Deliver:

- separate website-username and first-OSRS-character onboarding fields;
- list, add/reactivate, unlink, label, reorder, preferred, and saved-EHB actions;
- transactional spelling correction with open-signup propagation;
- active/upcoming-event unlink warning;
- accurate empty, validation, conflict, success, and failure states;
- ordinary Razor form/navigation fallbacks;
- English and Danish coverage for every User-facing state.

Follow the shared UI rules from first implementation. Do not introduce page-local themes.

**Gate**

- Focused domain/application/integration tests cover invariants and the correction transaction.
- One browser-level path covers the complete normal My Accounts journey and no-JavaScript fallback.
- Manual checks cover desktop/narrow layout, keyboard/focus, warning, empty, and conflict states.
- Release build and formatting pass.

### Pass 2.4 — Independent website username rename

Deliver:

- password-confirmed rename to any case-insensitively unique valid username;
- safe relational-race handling;
- current-session reissue without global authorization-version invalidation;
- audit history;
- English/Danish feedback.

**Gate**

- Focused tests cover success, wrong password, case-insensitive collision/race, current-session name refresh, unrelated-session continuity, and unchanged event/My Accounts data.
- Release build and formatting pass.

### Pass 2.5 — Slice cleanup and final gate

- Audit obsolete fields, methods, projections, routes, and compatibility code.
- Remove only paths made obsolete by Slice 2; keep the private edit-token path explicitly for Slice 4.
- Rehearse clean and retained PostgreSQL migrations.
- Add/update `MANUAL_TEST_CHECKLIST.md` and a layman-oriented result sheet.
- Run `git diff --check`, formatting, Release build, and the complete solution suite once after the implementation stabilizes.
- Update `CURRENT_STATUS.md` with exact evidence.
- Obtain a bounded independent review of Slice 2 changes.

Do not stage, commit, or push unless separately authorized.

## 6. Verification budget

Use tests proportionate to risk:

- domain tests for normalization and aggregate invariants;
- PostgreSQL integration tests for partial uniqueness, migrations, transactions, and concurrency;
- a small browser set for real routes, localization, native forms, and feedback;
- manual checks for visual/responsive behavior and understandable workflow.

Do not duplicate the same invariant at every layer. Do not rerun the complete suite after each pass; use focused checks during passes and one complete automated gate in Pass 2.5 unless failures or blast radius justify another run.

## 7. UI impact map

| Surface | Slice 2 treatment |
| --- | --- |
| New My Accounts page | New shared-system page; English/Danish; responsive and no-JS complete |
| Account settings | Add independent username rename using existing shared account UI |
| Existing signup/edit pages | Preserve visible form until Slice 4; change storage only |
| Signup/roster projections | Adapt names/EHB to assignment authority without redesign |
| Admin participant page | Adapt fixed fields to assignment authority without broader Slice 4 redesign |
| CSV import | Internal mapping change only |
| Live draft | Preserve protected layout/interaction; change data projection only |
| Public board/team/tile/submission | Preserve protected Pass 12 interaction baseline |
| Board editor | Unaffected |
| Admin Accounts/security pages | Unaffected except shared username display naturally reflects rename |

## 8. Handoff rule

Each implementation task should say exactly which pass to implement and instruct the agent to read:

1. `AGENTS.md`
2. `CURRENT_STATUS.md`
3. this plan
4. only the source-of-truth sections named by that pass

An implementation task must not continue into the next pass. At the end it reports changed files, focused evidence, remaining pass-specific work, and whether the pass gate is met. A separate review follows the completed slice, not every small commit.

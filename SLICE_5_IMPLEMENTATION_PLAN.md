# Slice 5 — Teams, Captains, External Rosters, and Website Draft

**Status:** Slice 5 is accepted. S5-01 through S5-10 and the final TEST 52 Public boards/Board-route retests are approved. Final automated acceptance is complete: Domain `153/153`, Application `82/82`, Browser `66/66`, Integration `188/188`, combined `489/489`; Release solution build, repository formatting, `git diff --check`, and EF pending-model verification passed; 21 relevant migration/reset tests passed with 35 migrations, no missing designers, and one snapshot. Browser TRX: `/private/tmp/slice5-final-gates-20260729/browser-final/Bingo.BrowserTests-final.trx`. Integration TRX: `/private/tmp/slice5-integration-inventory-20260729/retry/Bingo.IntegrationTests-inventory-retry.trx`. Audit found no staged files, secrets, generated runtime artifacts, or unrelated modified paths. Ready for packaging/merge/push; those actions remain unperformed.

**Depends on:** Accepted Slice 4 (`codex/milestone-8a-slice-4`), `PRODUCT_REQUIREMENTS.md`, `FUNCTIONAL_WORKFLOWS.md`, `DATA_MODEL.md`, `TECHNICAL_ARCHITECTURE.md`, `IMPLEMENTATION_ROADMAP.md`, and `UI_OVERHAUL_ROADMAP.md`.

## 1. Objective

Slice 5 replaces the retained team/draft compatibility behavior with one authoritative workflow for:

- individually configured website-drafted teams;
- internal or external pre-formed teams whose rosters do not enter the website draft;
- manually assigned Captain and Co-captain roles;
- explicit emergency-captain fallback access;
- a private, balanced, controller-operated snake draft;
- atomic roster/pick publication and exceptional pre-event reopening.

The real drafted-team count and roster distribution are derived from current teams and included participants. Board-editor planning estimates remain separate and never create teams or constrain the real draft.

## 2. Explicit boundary

### Included

- Team persistence, stable event-scoped slugs, managed images, affiliation, formation type, history, and concurrency.
- Drafted and pre-formed team creation and pre-event roster management.
- External/pre-formed roster CSV using the approved column-oriented format.
- Manual Captain/Co-captain assignment and history.
- Draft-pool visibility for authorized drafted-team captains/co-captains.
- Explicit emergency captain credentials through the existing audited account workflow.
- Derived balanced roster sizes and captain-seat-aware turn planning.
- Private draft control, scramble, picks, pause/resume, repeated latest-pick undo, and controller lease.
- Draft finalization, public roster/effective-pick publication, and exceptional pre-event reopening.
- Retained-data conversion, Development fixture updates, and removal of superseded active draft/team compatibility.

### Deferred

- Board derivation, approval, and publication changes — Slice 6.
- Live account swaps and captain focus — Slice 7.
- Evidence changes — Slice 8.
- Post-draft/live withdrawals, vacancies, and roster replacements — Slice 9.
- Wise Old Man synchronization — Slice 10.
- Whole-site visual overhaul — Milestone 9.

Slice 5 may establish persistence/history later slices need, but it does not implement those deferred workflows.

### Preserved

- Authenticated signup ownership and event-character authority from Slices 2 and 4.
- Explicit unowned external participants; no name or Discord ownership inference.
- Existing emergency-credential security, cutoff, audit, and enable/disable rules.
- Slice 3 lifecycle/readiness authority.
- Protected live-draft interaction hierarchy and collaboration lease behavior.
- Protected public-board, board-editor, team, tile, and submission routes.
- Historical migrations and competitive records.

## 3. Approved product rules

### 3.1 Team types and identity

- A team is either **Drafted** or **Pre-formed**.
- Drafted teams are added individually. Their active count is the website-draft team count.
- Pre-formed teams may contain linked internal participants, unowned invited/external participants, or both.
- Pre-formed teams never receive website-draft turns and never affect drafted-team sizes.
- Team display name is event-unique and editable before event start.
- Team URL slug is generated once and remains stable after renames.
- Affiliation/clan is optional.
- Team images use managed authenticated upload. Arbitrary image URLs are removed from active behavior.
- Team metadata remains editable after draft finalization but locks at event start.
- Active picks lock the current private attempt. Before publication, its controller may cancel it to Setup, retaining undone history while restoring drafted-team editing.

### 3.2 External/pre-formed roster CSV

CSV is scoped to one selected pre-formed team and is never an ordinary signup or drafted-pool import.

Each data row represents one roster member:

```csv
Account,EHB,Account,Account,Account
Iron Chris,250,Main Chris,Iron Chris Alt 2,
Player Two,120,Player Two Alt,,
Player Three,75,,,
```

- Column 1 is required and contains the member's primary regular/playing OSRS account.
- Column 2 is required and contains that primary account's valid non-negative EHB value.
- Columns 3 and later are optional additional-account columns for the same member.
- Additional-account columns have no EHB values and do not create additional participants.
- Empty trailing account cells are ignored.
- There are no CSV fields for website username, Discord, payment, notes, Captain, Co-captain, team metadata, or signup answers.
- Captain and Co-captain roles are assigned manually by an Admin after import.
- Imported participants are unowned unless an Admin later performs the existing explicit participant-ownership transfer.
- The primary account supplies the member's event-facing identity and playing/EHB assignment. Additional accounts are registered without EHB.
- The preview validates column/header shape, duplicate/case-insensitive character use, existing event reservations, EHB values, and conflicts before applying.
- Apply is one transaction and is replay-safe. A failed row means no participant, assignment, membership, or audit from that import is committed.

### 3.3 Captain and Co-captain

- Participant membership is the default and is not presented as a special assignable authority.
- Admins may assign **Captain** or **Co-captain** to a current team member.
- Captain volunteer is only a selection aid.
- Captain and Co-captain occupy normal roster seats.
- Every drafted team needs a current Captain before draft start. Co-captain alone is insufficient.
- A linked Captain/Co-captain receives event/team authority through their website account, independent of current Discord association.
- An unowned external Captain receives no website authority from their OSRS name or roster role.
- External/pre-formed teams use an explicitly created and enabled team-scoped emergency credential when no linked Captain can operate the website.
- No normal or emergency account is generated automatically on role assignment or draft finalization.
- Role assignment, promotion, demotion, revocation, and withdrawal retain append-only history and relevant notifications.
- Before/during the website draft, linked captains/co-captains of drafted teams may view the expanded confirmed draft pool. Pre-formed-team captains may not.
- Event-start readiness remains stricter across all teams: a linked current Captain or enabled emergency captain fallback is required.

### 3.4 Derived roster distribution

For `P` included drafted-team participants and `T` active drafted teams:

```text
larger_size = ceiling(P / T)
smaller_size = floor(P / T)
larger_team_count = P mod T
smaller_team_count = T - larger_team_count
```

- There is no team-count or target-size input.
- Included participants are confirmed internal participants not assigned to pre-formed teams, including participants already preassigned to drafted teams.
- Waiting, withdrawn, and pre-formed members are excluded.
- Preassigned Captain/Co-captain seats count normally.
- Setup previews the larger size and how many teams will finish one smaller.
- Each team card shows its current/projected final size.
- Existing manual assignments that make a balanced result impossible block draft start.

### 3.5 Private website draft

- At least two active drafted teams are required.
- Draft start locks the active private attempt; publication is the ordinary irreversible roster boundary.
- Every included participant and reservation is revalidated transactionally.
- Every drafted team requires an actual Captain.
- The controller explicitly randomizes team order and may randomize again until the first active pick; cancelling an unpublished attempt returns to Setup.
- Unequal preassigned rosters cause larger teams to be skipped until smaller teams catch up.
- Eligible turns then follow the authoritative randomized snake order.
- All included participants remain visible with clear available/assigned state; the default sort is primary Regular-account EHB.
- A pick atomically creates the immutable pick and active membership.
- Undo targets only the latest active pick, records reversal, ends its membership, and restores the correct next turn.
- Undo may repeat back to zero without reopening setup; cancelling the unpublished attempt explicitly returns to Setup and retains the undone history.
- Pause/resume preserves order, picks, memberships, and pool.
- One renewable controller lease allows mutation; other Admins see a live read-only view.
- Draft and provisional assignments remain private until finalization.

### 3.6 Finalization and reopening

- Finalization fails if any included participant is unassigned, duplicated, or assigned outside the permitted balanced distribution.
- It publishes all current team rosters and the effective active pick order together.
- Public results exclude undone attempts, controller identity, private timestamps, and correction/audit data.
- Pre-formed rosters publish with the event roster but remain outside draft order/history.
- Board publication is not part of the finalization transaction. Slice 5 exposes only the appropriate handoff/outstanding action for Slice 6.
- Before event start, an Admin may reopen a finalized draft using strong confirmation and a required written reason.
- Reopening hides the public drafted rosters/effective order, preserves every publication cycle and pick attempt, and permits controller-based latest-pick undo/repicking.
- Reopening never unlocks the drafted-team set or formation types.
- A separately published board remains published.
- Re-finalization creates a new authoritative public projection without deleting the superseded one.

## 4. Interaction and implementation discipline

- Preserve the protected live-draft layout, density, controller/read-only model, and realtime invalidation behavior.
- New or materially changed pages use the shared UI system immediately; full visual normalization remains Milestone 9.
- Independent metadata values may save on change where safe. Team creation, image upload, CSV apply, role changes, draft start/finalize/reopen, and other consequential operations remain explicit.
- Enhanced interactions wait for server acceptance and provide accurate feedback without adding redundant custom JavaScript or unnecessary no-JavaScript-only machinery.
- Normal route-backed fallbacks remain for protected team/draft interactions.
- Admin-only pages may remain English. Any participant/captain-visible additions require English and Danish.
- Use focused verification proportional to risk: domain calculation/state matrices, one representative PostgreSQL scenario per transactional/concurrency boundary, and route/markup checks only for meaningful authorization or interaction contracts.
- Do not multiply tests that prove the same invariant through equivalent paths.
- Independent review is performed once after Pass 5.5 rather than after every pass unless a concrete high-risk finding requires earlier review.

## 5. Implementation passes

### Pass 5.1 — Persistence and retained conversion

Implement:

- managed team-image assets and active reference;
- team concurrency, stable metadata, formation and active-state invariants;
- membership source/replacement/history fields;
- append-only role transitions;
- active-attempt first-pick marker, cleared only by private-draft cancellation while retained picks preserve history;
- derived-size/finalization publication-cycle and reopen history;
- constraints for event/team/participant consistency, one current membership per event participant, unique event team names/slugs, pick uniqueness, and publication integrity.

Retained conversion:

- preserve team, membership, pick, role, and participant IDs/history where valid;
- deterministically map current drafted/pre-formed membership sources;
- never fetch or continue serving retained arbitrary team-image URLs;
- fail closed on duplicate current memberships, invalid cross-event relationships, impossible active picks, or ambiguous finalized projections;
- retain historical migrations while removing obsolete active columns only when the forward conversion is proven.

Gate:

- no visible workflow redesign;
- clean and representative retained PostgreSQL migration rehearsal passes;
- EF model, focused domain constraints, formatting, diff, and affected Release build pass.

### Pass 5.2 — Team and pre-formed roster management

Implement:

- individual drafted/pre-formed team creation/removal rules;
- stable names/slugs, affiliation, managed image add/replace/remove;
- pre-event manual pre-formed roster add/remove/move/correction;
- column-oriented CSV preview/apply exactly as defined in section 3.2;
- drafted-team count and roster-distribution preview derived on every authoritative load;
- active-attempt structural-lock enforcement with controller-authoritative private cancellation before publication;
- automatic structured audit without routine typed reasons.

Gate:

- drafted teams affect distribution immediately; pre-formed teams never do;
- manual and CSV roster paths share reservation and event-consistency rules;
- CSV is atomic, replay-safe, and cannot import ordinary drafted-pool signups;
- focused desktop/narrow/keyboard/error/empty manual review passes.

### Pass 5.3 — Captain/Co-captain authority

Implement:

- Admin assignment, promotion, demotion, and revocation of Captain/Co-captain for current members;
- role history and affected-account notification;
- immediate website authority from linked participant ownership;
- explicit existing emergency-credential workflow for unowned/external captains, with no automatic provisioning;
- drafted-team Captain gate and all-team event-start Captain/emergency gate;
- drafted-team captain/co-captain expanded signup-table projection with privacy boundary;
- missing-captain warning/action projection.

Gate:

- Co-captain alone cannot satisfy either applicable gate;
- pre-formed captains cannot inspect the internal draft pool;
- role/session/access changes are immediate and concurrency-safe;
- no participant, character, Discord name, or emergency credential is treated as implicit website ownership.

**Automated verification (2026-07-29):** `CaptainAuthorityIntegrationTests` passed `4/4` against Testcontainers PostgreSQL, with the role-service transaction, readiness, Live remediation, signup-table, and finalization-boundary coverage included in the consolidated final acceptance above.

### Pass 5.4 — Derived private snake draft

Implement:

- transactional readiness and derived distribution;
- controller acquisition/takeover/renewal/release;
- randomization before first-ever pick;
- captain-seat catch-up and snake-turn calculation;
- atomic pick, repeated latest-pick undo, pause, and resume;
- all-player visibility with authoritative EHB and assignment status;
- structural locks and live read-only collaboration invalidations;
- removal of obsolete team-count/target-size setup input.

Gate:

- calculation matrix covers uneven totals and unequal captain preassignments;
- concurrent pick/control/undo races have exactly one valid outcome;
- undo to zero restores the correct turn; controller cancellation before publication returns the private attempt to editable Setup without deleting history;
- the protected live-draft interaction remains operable from the existing workspace.

**Implementation gate (2026-07-29):** Met and manually approved. Active draft sizing is derived from current Drafted teams and included confirmed internal participants; the legacy target-size column is inert read compatibility only. Explicit acquisition, confirmed takeover, release, lease-guarded start, and controller-authoritative private cancellation are in place. Scramble is blocked after the active attempt's first pick; cancelling before publication returns to editable Setup while retaining undone pick history. Finalization/publication remains the ordinary irreversible boundary.

**Pass 5.5 implementation gate (2026-07-29):** Met and manually approved. Frozen publication, audit rollback, reopening/re-finalization history, finalization concurrency, public privacy/destination coverage, and the Public boards/Board-route retests are included in the consolidated final acceptance above.

### Pass 5.5 — Finalization, public roster, reopening, and cleanup

Implement:

- atomic finalization readiness, public roster/effective-pick projection, and history;
- public team roster and draft-results routes with correct privacy;
- separate Slice 6 board-publication handoff only;
- strong-confirmed, reasoned pre-event reopen and repeat finalization cycles;
- current/history destinations for participants/captains/Admins;
- Development TEST 13/15 team/draft fixtures;
- bounded obsolete-path audit and removal of superseded active team/draft/provisioning compatibility.

Gate:

- public projection appears only after finalization and disappears during reopen;
- every superseded publication and pick attempt remains Admin-readable;
- pre-formed rosters never enter effective pick order;
- board publication remains independent;
- final automated gates, clean/retained PostgreSQL rehearsal, EF pending-model check, formatting, Release build, and complete suite are complete as recorded in the final status above.

## 6. Manual acceptance outline

### Pass 5.3 — Captain/Co-captain authority

1. In a current operational event, promote, demote, and revoke a member. Confirm one role-history entry, one generic in-site notification for an explicitly owned active website account, and none for an unowned member.
2. Confirm neither matching OSRS/Discord names nor the volunteer answer grant access. Change Discord authentication/link state and confirm the explicitly owned current Captain/Co-captain remains authorized; revoke the role and confirm the next request is denied.
3. Try draft start with Captain, Co-captain-only, and emergency-only drafted teams. Only the actual Captain case proceeds. Try event start with an owned Captain, an enabled team-scoped emergency credential, and Co-captain-only; only the first two satisfy readiness.
4. During Live, revoke the final usable Captain. Confirm Live remains Live and the Admin roster/readiness warning persists until an owned Captain is assigned or emergency access is explicitly enabled.
5. As a linked Captain/Co-captain of a Drafted team before/during draft, open the signup table and confirm confirmed participant answers are visible without waiting-list/payment/Admin/security/Discord/audit data. Confirm a Pre-formed captain is denied and the route redirects after finalization.

**Automated/covered:** S5-07 uses the existing focused CSV tests as its canonical comma, multi-row, and optional additional `Account`-column evidence; do not repeat it manually.

### Pass 5.2A/5.2B — teams and external roster import

**Manual seed:** Reset Development data and use `TEST 52 — Team and CSV setup` (`test-52-team-csv-setup`). It is a far-future SignupClosed Development fixture with a published board, draft Setup/no first pick, no teams or memberships, and five confirmed eligible participants. The tester creates the two Drafted teams (for 3 + 2) and then three Drafted teams (for 2 + 2 + 1), plus Pre-formed teams, through the UI. Use `TEST 15 — DKL Live` only for the event-start metadata-lock check.

**Seed/reset verification (2026-07-29):** The real PostgreSQL double-reset regression passed `1/1` after injecting publication roster/cycle, managed image, membership-role transition, and legacy image-reference rows before the second reset. It asserts exactly TEST 13/15/52 and the complete TEST 52 setup contract after each reset; the affected Release build, formatting verification, and `git diff --check` passed.

- Create Drafted and Pre-formed teams, confirm only Drafted teams change the derived distribution, and verify a Pre-formed team remains outside the website pool.
- Add, remove, and move an external member manually; confirm it is unowned, has one Playing account/EHB and optional Informational accounts without EHB.
- From one selected Pre-formed team, download the minimal `Account,EHB` template; preview a valid multi-row file with optional repeated `Account` columns, then reselect and apply the exact file. Confirm all members are created only once and shared OSRS catalogue characters are reused.
- Confirm invalid headers, EHB, duplicate accounts, current-event reservations, a different team/admin/file, expiry, refresh/replay, and a concurrent reservation all return safe feedback with no partial roster writes.
- Confirm the CSV control is absent from Drafted teams and no Captain/Co-captain controls or ordinary signup CSV path are introduced.

**Pass 5.2 manual-remediation batch (2026-07-29):** Duplicate-team/external-member feedback, managed-image persistence/rendering, and Numbers CSV handling were remediated and are superseded by the approved S5-01–S5-07 checklist results and consolidated final acceptance above.

**CSV usability correction (2026-07-29):** Preview retains its validated parsed rows in the existing short-lived, actor/event/team-bound nonce state. Apply now requires only that nonce and imports exactly those rows; it does not render or accept a second file upload. Expiry or process-loss asks the Admin to preview again. No durable preview persistence or migration was added.

The detailed checklist is added with the implementing pass. The consolidated journeys must at minimum prove:

1. Add/remove drafted teams and observe derived balanced sizes.
2. Add a pre-formed team manually and through CSV; confirm it receives no draft turn.
3. Assign Captain/Co-captain and verify the Captain-only readiness distinction.
4. Verify linked and emergency captain authority without ownership inference.
5. Randomize, record several snake rounds, pause/resume, and undo repeatedly.
6. Confirm unequal preassigned captain seats produce catch-up turns.
7. Finalize and inspect public rosters/effective pick order and privacy.
8. Reopen with confirmation/reason, correct picks, and re-finalize without losing history or changing the separately published board.
9. Confirm team metadata locks at event start; before publication, Cancel draft returns an active private attempt to editable Setup while retaining history.

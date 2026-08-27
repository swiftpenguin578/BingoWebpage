# Slice 2 Manual Test Results

Use `MANUAL_TEST_CHECKLIST.md` for the authoritative test steps and expected behavior.

## Test run

- Date: 2026-07-27 remediation verification; prior manual checks were accepted during Passes 2.3–2.4
- Revision / working tree: `a149125` plus the uncommitted Pass 2.5 working tree
- Tester: Codex remediation checks; user for the recorded Pass 2.3–2.4 manual checks
- Browser / device: Recorded with the approved Pass 2.3–2.4 manual checks
- Database / scenario: Testcontainers PostgreSQL 17 plus the approved disposable Development scenarios
- Overall result: Passed. The independent review and restricted re-reviews cleared every finding, the final durable Integration result is `95/95`, and all manual or automated acceptance rows are complete.

Use `Not run`, `Passed`, `Failed`, `Automated`, `Postponed`, or `Blocked` in the Result column.

- `Automated` means the completed automated suite or a bounded static/EF check already covers the case; no manual repetition is needed.
- Rows already marked `Passed` record the Pass 2.3 or Pass 2.4 manual acceptance preserved in `CURRENT_STATUS.md`.
- Run the remaining `Not run` rows for final Slice 2 manual acceptance.
- The detailed steps and exact expected behavior remain in `MANUAL_TEST_CHECKLIST.md`.

| ID | Test | Result | Notes |
|---|---|---|---|
| S2-01 | Upgrade a clean and an existing database safely | Automated | PostgreSQL rehearsal covers clean and retained Slice 1 data, including inactive releases, deterministic collision resolution, and a clear failure for ambiguous Playing reservations. |
| S2-02 | Share or borrow a character without duplicate links | Automated | PostgreSQL integration coverage verifies normalized uniqueness, trust-based sharing, and concurrent onboarding of two users to one new character. |
| S2-03 | Keep My accounts history, order, and one preferred character | Automated | Domain and PostgreSQL coverage verify retained pair history and preferred-link constraints. |
| S2-04 | Reserve characters once per event and apply EHB rules | Automated | Domain and PostgreSQL constraints cover current event uniqueness and playing/informational EHB shape, including a concurrent fixed-form assignment race with one winner and no partial loser. |
| S2-05 | Keep old signup, edit, import, and Admin forms on the new authority | Automated | Focused integration coverage verifies assignment-backed creation, editing/history, import, Admin correction, and released historical authority in Admin management/detail views. |
| S2-06 | Preserve participants that do not own a website account | Automated | Migration and ownership tests verify nullable ownership and prohibit inferred ownership. |
| S2-07 | Create separate website and OSRS identities during onboarding | Automated | Native-form onboarding coverage verifies separate values, atomic preferred links, and a concurrent shared-character creation without misreporting it as a username/Discord conflict. |
| S2-08 | Manage the My accounts list safely | Automated | Native-form and account-scoping coverage exercise add/reactivate, metadata, order, preferred state, and cross-account rejection. |
| S2-09 | Keep each person's saved EHB separate | Automated | PostgreSQL coverage verifies per-link isolation and independent event snapshots. |
| S2-10 | Understand the unlink warning | Passed | Approved during Pass 2.3 manual acceptance at desktop and narrow widths. |
| S2-11 | Correct a spelling and understand conflicts | Passed | The conflict and successful-correction states were approved in Pass 2.3; rollback/propagation are also automated. |
| S2-12 | Rename the website username safely | Passed | All six requested Pass 2.4 manual checks were approved; password, collision, and persistence outcomes are also automated. |
| S2-13 | Keep sessions and event data unchanged after rename | Automated | Integration coverage verifies current/unrelated-session continuity, absolute expiry, and unchanged Slice 2/event data. |
| S2-14 | Keep emergency credentials outside My accounts and rename | Automated | Route/command boundaries and the shared normalized login reservation are covered automatically. |
| S2-15 | Keep the temporary private signup edit route working | Passed | The user approved the token-based edit journey after its route-specific success feedback was corrected. Automated HTTP coverage also proves the private POST/redirect renders the notice. Removal remains deferred to Slice 4. |
| S2-16 | Prove removed participant fields are no longer active | Automated | Bounded remediation searches cover runtime authority separately from migration history and the deliberately retained Slice 4 fixed-form view models. |
| S2-17 | Read all Slice 2 states in English and Danish | Passed | Representative navigation, empty, validation, conflict, and success states were approved during Passes 2.3–2.4; localization assertions also run automatically. |
| S2-18 | Use My accounts and Settings on different widths and by keyboard | Passed | Desktop/intermediate/narrow layout, keyboard/focus, empty, warning, and conflict states were approved in Pass 2.3. |
| S2-19 | Keep local account forms usable without client enhancement | Automated | Discord's external authorization page requires JavaScript and is outside the application fallback boundary. Existing native-form coverage verifies local onboarding, My accounts, website-username rename, sign-out, and new-username sign-in behavior. |
| S2-20 | Smoke-test protected draft and public-board interactions | Passed | Recorded as passed; the protected layouts, Pass 12 interactions, responsive routes, and no-JavaScript fallbacks remain unchanged by Slice 2 remediation. |

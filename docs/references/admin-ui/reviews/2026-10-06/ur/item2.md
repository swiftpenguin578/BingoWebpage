# UR item 2 — realistic synthetic scenarios

Added `UiReviewScenarioSeeder`, independent of `DevelopmentScenarioSeeder` and
`--reset-test-data` (both unchanged). It seeds 20 named events and 10 invented
website accounts into an empty migrated database after the catalogue snapshot.
Review passwords use `ReviewOnly!1234`, without forced password changes.

The live/final-review profiles each have exactly one **visible** current event;
none uses `IsDevelopmentFixture`. Domain transitions build signup opening/closing,
Live, Final review, legacy Finalized, Archived, Cancelled, Discarded, roster
publication, board publication/correction and WOM end outcomes. FirstPublicAt
matches first signup opening. Dates are relative to each run and normalized to
PostgreSQL microseconds. Published board and direct-roster approvals retain their
immutable snapshot trees. Two real evidence uploads compete for one objective;
approving the later upload is blocked through the real submission service.

Per planner ruling 63 (source labelled 7 October; relayed/accepted on the client
6 October), the Finalized scenario is hidden. Hide permits only Final review,
Finalized or Archived; all three are present with audit entries. No hidden upcoming
setup is fabricated. A7 wording is an observation for U2, not changed here.

Executed 6 October: focused Testcontainers/PostgreSQL theories for both profiles
**2 passed / 0 failed / 0 skipped**. They exercise real current-event readiness and
blocked approval, exact persistence precision including non-microsecond-aligned
clock input, timestamps, accounts, rosters, correction and WOM outcome coverage.
Release command build **0 warnings / 0 errors**. The owned create command succeeded:
`live`, built at `2026-10-06T10:05:22.4233500+00:00`, app 5310 and references 5320
answered readiness GETs. `git diff --check` passed; frozen CSS and old seeder unchanged.

Initial command attempt failed safely on PostgreSQL's temporary initialization
socket, before migration/seeding. Readiness now uses TCP; incomplete initialization
can only finish with verified local marker, exact container ID/label/name, empty
application database and absent/matching PostgreSQL marker. A subsequent scenario
attempt exposed the account-answer database trigger; the new answer value was
corrected to the existing character-reference-only convention, then both tests and
create passed. No existing test assertions were edited and no user data touched.

Independent review/user walkthrough pending. Printed grouped list is item 3;
create-to-refresh rebuild and all-URL proof are item 4. Whole .NET gate belongs to
Claude/user on the final SHA.

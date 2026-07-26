# Current Project Status

**Verified:** 2026-07-26
**Branch:** `codex/milestone-8a-slice-2` (Slice 2 implementation branch)
**Planning decision:** 2026-07-26

## Active handoff

**Slice 1 checkpoint:** Slice 1 passed independent review, manual acceptance, formatting, Release build, and the complete automated suite. It is committed and pushed on `codex/milestone-8a`; the checkpoint working tree was clean before Slice 2 planning began.

**Slice 2 planning:** Planning Pass 2 approved the five bounded implementation passes in `SLICE_2_IMPLEMENTATION_PLAN.md`. Slice 2 covers My Accounts, optional per-link saved EHB defaults, event character assignments and uniqueness, deterministic legacy-field migration, compatibility conversion, and independent website-username rename. It deliberately excludes participant claim links, final authenticated signup/dropdowns, Wise Old Man fetching, live swaps, evidence changes, and broader participant administration.

**Slice 2 Pass 2.1:** The persistence foundation is implemented and its gate is met. `AccountOsrsCharacter` now retains link/unlink history, actor, label, ordering, preferred state, per-link saved EHB, timestamps, and optimistic concurrency. `EventParticipant` has nullable website-account ownership with one owned participant per account/event. `EventParticipantCharacter` now provides playing/informational event assignments, EHB metadata rules, release history, event-consistent participant relationships, and current event-character uniqueness. Migration `20260726195230_AddSlice2PersistenceFoundation` deterministically preserves Slice 1 links and backfills retained primary/secondary participant accounts without guessing ownership or My Accounts links.

**Pass 2.1 approval:** The user approved the completed Pass 2.1 gate without a separate pass-level independent review. The approved Slice 2 process keeps the independent review for the complete slice after Pass 2.5.

**Next implementation action:** Implement only `SLICE_2_IMPLEMENTATION_PLAN.md` Pass 2.2. Preserve all Pass 12/protected UI behavior and do not continue into Pass 2.3 in the same task.

## Verification

2026-07-26 final remediation verification: `git diff --check` passed. `dotnet format Bingo.slnx --no-restore --verify-no-changes` passed. `dotnet build Bingo.slnx --configuration Release --no-restore` passed with 0 warnings and 0 errors. One complete `dotnet test Bingo.slnx --no-restore --results-directory /private/tmp/slice1-final-remediation-trx-20260726-final2 --logger trx` run completed with durable TRX counters: Domain `total=44, passed=44, failed=0, skipped/notExecuted=0`; Application `total=81, passed=81, failed=0, skipped/notExecuted=0`; Browser `total=45, passed=45, failed=0, skipped/notExecuted=0`; and Integration `total=72, passed=72, failed=0, skipped/notExecuted=0`. The integration run used Testcontainers PostgreSQL successfully.

2026-07-26 Slice 2 Pass 2.1 verification: focused new domain tests passed `2/2`; focused new PostgreSQL persistence/migration tests passed `5/5`; affected existing `AccountAccessTests` plus new domain tests passed `3/3`; affected existing `AccountOverviewTests` passed `2/2`. Clean and retained PostgreSQL migrations both succeeded. `dotnet ef migrations has-pending-model-changes` reported none. `dotnet format Bingo.slnx --no-restore --verify-no-changes` passed. Release builds for the affected Domain-test and Integration-test project graphs passed with 0 warnings and 0 errors. `git diff --check` passed.

## Remaining work

- Implement `SLICE_2_IMPLEMENTATION_PLAN.md` Pass 2.2 only.

## Historical summary

2026-07-25 through 2026-07-26 Slice 1 remediation addressed protected Discord state, retained-owner gates, membership-verified emergency scope migration, reset concurrency, authentication lifetimes/throttles, cutoff lifecycle, audit/notifications, account datasets, Danish safe failures, and persisted access projection. The final independent review cleared all findings. Detailed Slice 1 evidence remains in `SLICE_1_IMPLEMENTATION_PLAN.md` and `SLICE_1_MANUAL_TEST_RESULTS.md`.

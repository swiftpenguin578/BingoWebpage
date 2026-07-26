# Current Project Status

**Verified:** 2026-07-26
**Branch:** `codex/milestone-8a` (Milestone 8A checkpoint branch)
**Planning decision:** 2026-07-25

## Active handoff

**Final Slice 1 gate (2026-07-26):** manual retests passed for S1-16, S1-19, S1-20, and S1-35. Focused automated coverage now also closes S1-08 (Discord-onboarding username collision retains values, leaves the occupied account untouched, creates no duplicate, and permits a retry) and S1-22 (grant/revoke and ownership-transfer stale-session invalidation, transfer password/destination confirmation, and access-changed sign-in-again feedback). Both rows are recorded as `Automated` in `SLICE_1_MANUAL_TEST_RESULTS.md`.

**Final verification (2026-07-26):** focused remediation tests passed 6/6 integration and 7/7 domain. `git diff --check` and `dotnet format Bingo.slnx --no-restore --verify-no-changes` passed; `dotnet build Bingo.slnx --configuration Release --no-restore` passed with 0 warnings and 0 errors. The complete suite wrote durable TRX artifacts to `/private/tmp/slice1-final-remediation-trx-20260726-final2`: Domain 44/44, Application 81/81, Browser 45/45, and Integration 72/72, with zero failed, skipped, or not-executed tests in every project.

**Final independent-review clearance (2026-07-26):** the original reviewer’s restricted re-review cleared all three final findings with no remaining material risk. Emergency credentials now require the active original-or-reopened window plus explicit re-enablement, including a strict emergency boundary at the cutoff instant; the operator-only OwnerRecovery reset-link command has exact active-owner targeting, a 60-minute single-use token, and secret-free system audit; and real PostgreSQL username/Discord uniqueness races are translated into endpoint-safe localized conflicts without partial persistence. S1-38 is accepted as fully passed by the user. Slice 1 is approved as safe to commit and push.

Slice 1's final independent-review remediation is in the uncommitted working tree. It now includes Discord user-info ticket claims, protected onboarding state, account mutation concurrency, explicit emergency creation, retained-data scope checks, independent login throttles, reset-link serialization, and personal-notification routing. Preserve all existing Planning Pass 2 and Pass 12 work. Do not stage or commit without explicit authorization.

The final implementation-owned gate removed the obsolete transient `Account` role/scope API and its active reads. Emergency event/team/lifecycle authority is now exclusively `AccountEventAccess`; unfinalization does not silently re-enable an emergency credential. Migration history remains intact.

**Intentional compatibility:** Slice 1 retains only the minimal `OsrsCharacter`/preferred `AccountOsrsCharacter` onboarding foundation. Per the approved Slice 1 plan, its full My accounts management, labels, additional characters, event assignments, swaps, and legacy participant claiming are Slice 2 work. The legacy/imported private edit-token fallback remains until the approved participant-claim migration removes it.

## Verification

2026-07-26 final remediation verification: `git diff --check` passed. `dotnet format Bingo.slnx --no-restore --verify-no-changes` passed. `dotnet build Bingo.slnx --configuration Release --no-restore` passed with 0 warnings and 0 errors. One complete `dotnet test Bingo.slnx --no-restore --results-directory /private/tmp/slice1-final-remediation-trx-20260726-final2 --logger trx` run completed with durable TRX counters: Domain `total=44, passed=44, failed=0, skipped/notExecuted=0`; Application `total=81, passed=81, failed=0, skipped/notExecuted=0`; Browser `total=45, passed=45, failed=0, skipped/notExecuted=0`; and Integration `total=72, passed=72, failed=0, skipped/notExecuted=0`. The integration run used Testcontainers PostgreSQL successfully.

## Remaining work

- Push the approved Milestone 8A checkpoint branch.
- Create the Slice 2 implementation branch from this clean checkpoint after Planning Pass 2 selects its bounded implementation passes.

## Historical summary

2026-07-25 remediation addressed independent-review findings around protected Discord state, retained-owner gates, membership-verified emergency scope migration, reset concurrency, fixed authentication lifetimes/throttles, cutoff lifecycle/idempotency, audit/notifications, account dataset separation, Danish safe failures, and persisted access projection. Superseded minute-by-minute checkpoints were consolidated here; detailed contracts remain in `SLICE_1_IMPLEMENTATION_PLAN.md`, `FUNCTIONAL_WORKFLOWS.md`, and the source-of-truth requirements.

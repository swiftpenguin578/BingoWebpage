# Review26 follow-up 2 — preserve commit conflicts through rollback

Second and final user-authorized follow-up commit, following R1 `19865e3`. Assignment source: [review26 R2/Fixes](26-b3-remediation-recheck.md), [26b I4-1](26b-b3-remediation-items-3-4.md), supplied verbatim. Review26 was source-only; its rollback claim was an inference until this execution. Planner is `/root`, replacement chat `01a10660-cc8a-7843-abb4-6cc1ebbb2bf2`; sole implementer `/root/au_b3_implementer`, gpt-6-astra/high. Review boundary is `45a09ad0b2aa982c8603cd116a6e5d9bfbfa89c6..HEAD` containing exactly these two follow-up commits.

All three assigned conflict catches (Start, End, Resume) now call the same small tolerant rollback helper. Cleanup failure cannot replace the original40001/40P01. End/Resume retain their three-attempt retry wrapper, original click time, fresh tracking/snapshot, lock order, authorization and submitted-version check. Start retains its existing normal try-again result. Other error paths are unchanged; no lifecycle redesign or schema changes.

## Executed observation

The new test uses two real Serializable transactions in each isolated PostgreSQL17 Testcontainer. Immediately before the lifecycle transaction commits, a transaction interceptor establishes a read/write dependency cycle on two test-owned probe rows and commits the competing transaction. It returns normally; the actual Npgsql COMMIT raises PostgreSQL40001. EF's transaction failure callback records and asserts the actual exception and commit boundary. No synthetic PostgreSQL exception is thrown.

With Npgsql10.0.3 / EF Relational10.0.9 (resolved project dependencies), the subsequent real RollbackAsync **throws InvalidOperationException containing `This NpgsqlTransaction has completed`**, with zero completed rollback callbacks. This is observed and asserted for all three actions, not inferred from an assembly string. The tolerant helper allows End/Resume to retry successfully and Start to return its normal try-again response. Separate controlled rollback-failure variants prove arbitrary cleanup failure also cannot mask the real database conflict. Three-conflict End/Resume exhaustion returns try-again with unchanged event version/state and no new transition.

Successful End/Resume retries preserve the original request instant after the controlled clock advances two minutes during the conflict. End assertions use a non-microsecond-aligned click and the exact microsecond PostgreSQL round-trip value for actual end, transition and pending request time; the configured end remains the click's ceiling minute. Exactly one successful transition is persisted. Existing real query-time conflict/recovery/exhaustion cases also pass.

Initial execution:4/8 passed. Two new natural-rollback cases disproved the initial test expectation that rollback would succeed (zero completed callbacks); the final assertion now checks the actual exception, without weakening the conflict/outcome checks. Two Start cases did not reach COMMIT because the new test fixture lacked the required primary-account question link; that fixture alone was completed and a readiness assertion added. No production workaround or unrelated fixture change.

Commands (assigned worktree):

```sh
dotnet test tests/Bingo.IntegrationTests --configuration Release --no-restore --filter 'FullyQualifiedName~Au20FollowupRealCommitConflict|FullyQualifiedName~Au20RemediationLifecycleSerializationRetryKeepsOriginalClick' --logger 'console;verbosity=minimal'
# PASS12/12, 29 s: eight commit-conflict cases plus four existing query-conflict cases.
dotnet build Bingo.slnx --configuration Release --no-restore
# PASS, zero warnings/errors.
git diff --check
# PASS.
```

Scoped documentation checks: all three supplied review26 copies byte-identical to the supplied Documents sources; CURRENT_STATUS remains below100lines; exact two-commit boundary and per-fix evidence retained. Previous whole-AU20/Stats63/63, compatibility2/2, migration and unaffected acceptance evidence reused without rerunning unrelated suites. No broad-suite pass or independent follow-up acceptance claimed.

Stopped for Claude direct recheck, without new agents. Review26's no-action notes remain deferred. D6/user first-publication stop and existing release gates remain. No B4/B5, UI/RC implementation, rehearsal/operator count execution, live WOM, user database, production, push, main merge or deployment.

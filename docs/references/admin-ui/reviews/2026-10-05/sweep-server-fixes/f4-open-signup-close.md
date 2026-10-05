# F4 — open-signup closing time

Status: server part complete in brief40; EI-2/Schedule binding remains pending.

Authority: brief40 F4, the decided Part 3 F4 outcome in
`review-notes/08-decisions.md`, and the signup lifecycle contract. The Schedule
field mapping now routes the refusal to `Input.SignupClosesLocal`.

Implementation commit: `4fa8b8ae2552ad14099b5f937992d4d2c54cac88`.

The required PostgreSQL reproduction was run before restoring the guard:

- `dotnet test tests/Bingo.IntegrationTests/Bingo.IntegrationTests.csproj --configuration Release --no-restore --filter 'FullyQualifiedName~Slice3ScheduleLifecycleIntegrationTests.OpenSignupsCannotClearSignupCloseAndValidReplacementRemainsAllowed' --logger 'trx;LogFileName=/tmp/bingo-brief40-f4-reproduction.trx'` — **passed 1, failed 0, skipped 0** with the old expectation, proving that clearing `SignupClosesAt` persisted while the event stayed `SignupOpen`.
- After the decided fix, the same focused test with `--logger 'trx;LogFileName=/tmp/bingo-brief40-f4-fixed.trx'` — **passed 1, failed 0, skipped 0**. Clearing is refused with `Signups are open, so they need a closing time. Set a new closing time or close signups now.`, version/close/audit state stays unchanged, and a different valid future close saves and audits.

Test-change mapping: old assertion `Succeeded == true` plus persisted null close
was changed to `Succeeded == false`, the exact refusal, unchanged persisted
close/version, and a separate valid replacement-close success. This expectation
change is explicitly authorized by brief40 F4 after the PostgreSQL reproduction;
the new assertions are more precise and no assertion was deleted or loosened.

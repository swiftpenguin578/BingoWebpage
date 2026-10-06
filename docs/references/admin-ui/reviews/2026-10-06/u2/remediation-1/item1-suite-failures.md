# U2 brief70 remediation item1 — suite failures

6 October 2026; implementer evidence, awaiting independent review. Started clean
on `codex/participants-functionality` at exactly
`96bc5b3c3c5b182abd0490a1c3a260878567ef06`. No workers or self-review.

## Authorized A10 / S14 before → after

- `AdminDesignShellIntegrationTests.BoundIdentityUsesNewLayoutAndRendersLocalAssetsTokenAndTempData`:
  before, `/Admin/Events/Index` was asserted to exclude `data-admin-design` and
  include `admin-shell-body`. After, both bound Directory and Dashboard must
  include the new marker and exclude the legacy marker. The exact legacy-marker
  assertions remain on the still-legacy Schedule URL for the same event. All
  Identity, local assets, authorization, menu, antiforgery and toast checks remain.
- `DanishIdentityAndShellUseEventTerminologyWithoutChangingLegacyPages` (defined
  in `AdminDesignIdentityIntegrationTests.cs`, same partial shell class): before,
  Directory was expected to say Bingoer and exclude the new marker. After,
  Directory must have the new marker and Events heading. Original Bingoer/legacy
  assertions remain unchanged on Schedule. Original Identity terminology checks
  remain unchanged. The first corrective run also added an unnecessary new
  whole-Directory absence assertion for Bingoer; a legacy-localized breadcrumb
  made it fail. Removed only that newly added assertion, not an existing contract.
- `Slice3CreationIdentityPersistenceIntegrationTests.CreationHttpAcceptsOnlyNameAndTimezoneAndCreatesAtomicDefaultAggregate`:
  before, GetStringAsync on `/Admin/Events/Create` expected200. After, assert the
  decided302 and exact `/Admin/Events?create=1` location, then fetch that URL for
  the same token/key. All native POST validation, atomic aggregate, audit,
  identity/timezone confirmation, retired input and stale-version checks remain.

Only brief70-authorized new-layout/Create setup expectations changed. No
authorization, audit, cookie/session, hidden-event or data invariant weakened.

## Cache synchronization race

The handler's ResponseReturned signal occurs before SendAsync returns to the
cache. The old50ms sleep was not a cache-completion barrier: under suite load the
fake clock could advance before DownloadAndRememberAsync recorded RetryAt,
giving a future cooldown and unexpected503 on the healthy request.

Both original waiters are still cancelled and asserted cancelled. A non-cancelled
observer attaches to the already-existing download before release; await its
exact503 failure and assert the provider was called only once. Only then advance
the fake clock and change the handler to healthy. No resend/retry/sleep added,
no assertion relaxed and no production cache change. Shared task removal is
observed by the unchanged healthy request/file/provider-count assertions.

## Checks

- `dotnet build tests/Bingo.BrowserTests/Bingo.BrowserTests.csproj --configuration Release --no-restore -v:minimal`:0 warnings,0 errors,1.86s.
- Focused final PostgreSQL/HTTP: `dotnet test tests/Bingo.IntegrationTests/Bingo.IntegrationTests.csproj --configuration Release --no-restore --filter 'FullyQualifiedName~AdminDesignShellIntegrationTests|FullyQualifiedName~AdminDesignIdentityIntegrationTests|FullyQualifiedName~Slice3CreationIdentityPersistenceIntegrationTests' --logger 'trx;LogFileName=u2-remediation-item1-final.trx' -v:minimal`:42 passed,0 failed,0 skipped,17s. Exact TRX: `tests/Bingo.IntegrationTests/TestResults/u2-remediation-item1-final.trx` in this checkout.
- Earlier focused run:41 passed/1 failed/0 skipped,16s, `u2-remediation-item1.trx`; superseded only after the bounded new-assertion correction above.
- Required cache class proof command:

```sh
for iteration in {1..50}; do dotnet test tests/Bingo.BrowserTests/Bingo.BrowserTests.csproj --configuration Release --no-build --filter FullyQualifiedName~OsrsWikiImageCacheTests --logger "trx;LogFileName=u2-cache-run-${iteration}.trx" -v:quiet || exit $?; done
```

Cache50 result:50 consecutive passed runs,450 passed,0 failed,0 skipped. The
fail-fast loop exited0; every TRX counter was also checked for9 passed/0 failed/
0 notExecuted. Each run contains all9
class cases; fail-fast loop, not retrying a failed test. TRX directory:
`tests/Bingo.BrowserTests/TestResults/`, filenames `u2-cache-run-1.trx`…`u2-cache-run-50.trx`.

Whole final-SHA .NET and shared UI gates belong to this remediation batch's final
checkpoint; not yet run. No independent pass/manual acceptance claimed.

## Changed files / next permitted action

`tests/Bingo.IntegrationTests/AdminDesignShellIntegrationTests.cs`,
`tests/Bingo.IntegrationTests/AdminDesignIdentityIntegrationTests.cs`,
`tests/Bingo.IntegrationTests/Slice3CreationIdentityPersistenceIntegrationTests.cs`,
`tests/Bingo.BrowserTests/OsrsWikiImageCacheTests.cs`, this evidence.
`git diff --check`:exit0. Item1 is ready for its scoped local checkpoint;
proceed to item2 after committing. Final batch gates remain pending.

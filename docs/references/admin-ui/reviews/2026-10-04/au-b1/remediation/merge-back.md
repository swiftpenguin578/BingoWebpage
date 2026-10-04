# B1 merge-back evidence

## Provenance and scope

- Target branch: `codex/participants-functionality`.
- Target parent before merge: `43ce865e4f891fdda4b6955b28f9e8a26dc678f0`.
- B1 source: `codex/au-b1-small-fixes` at `a9399b27e0e76e69ed75d8d6412ba2ce5dc78759`.
- Merge base: `3ce941b6718fd27fa514c309eabeb74d4ba17c3c`.
- Merge commit: `4065cb004252a788ea39e2060ec70efb80bdc809`.
- The local `git merge --no-ff --no-commit codex/au-b1-small-fixes` completed
  without conflicts. The merge tree retained the target's B2 placement-rule
  constructor arguments and migrations, and added the reviewed B1 Accounts,
  Audit, WOM fixture, ownership, translation, tests and evidence changes.
- `git diff --check HEAD^1..HEAD` passed. The merge commit's first-parent diff
  contains only the B1 lane plus its owned `DELIVERY_PLAN.md` and evidence.

## Focused checks on the merged result

All commands ran from the merged feature checkout with controlled Testcontainers
fixtures where applicable; no production or user-owned database was changed.

```text
dotnet test tests/Bingo.IntegrationTests/Bingo.IntegrationTests.csproj --configuration Release --no-restore --filter 'FullyQualifiedName~Slice10Pass103ActivityProjectionTests.DevelopmentTest15DueControlIsIdempotentAndResetRemainsCachedOnly' --verbosity minimal -m:1 -nodeReuse:false
PASS 1/1; the corrected AU15 fixture reached the non-Live refusal/reset path.

dotnet test tests/Bingo.IntegrationTests/Bingo.IntegrationTests.csproj --configuration Release --no-restore --filter 'FullyQualifiedName~AuditHistoryIntegrationTests' --verbosity minimal -m:1 -nodeReuse:false
PASS 5/5; malformed From/entry feedback, hidden-event redaction, DST edges and authorization covered.

dotnet test tests/Bingo.IntegrationTests/Bingo.IntegrationTests.csproj --configuration Release --no-restore --filter 'FullyQualifiedName~AccountOverviewTests' --verbosity minimal -m:1 -nodeReuse:false
PASS 3/3; target-bound reset projection and matching success message covered.

dotnet test tests/Bingo.IntegrationTests/Bingo.IntegrationTests.csproj --configuration Release --no-restore --filter 'FullyQualifiedName~TypedOwnershipTransferRequiresMatchingDestinationUsernameAndPreservesAtomicity|FullyQualifiedName~OwnershipTransferHttpRejectsMissingOrMismatchedConfirmationWithoutMutation|FullyQualifiedName~AccountSupportTransferRejectsWrongPasswordDisabledAndStaleRecipientWithoutAudit|FullyQualifiedName~OwnershipTransferAndOperatorRecoveryKeepExactlyOneOwner|FullyQualifiedName~RoleChangesAndOwnershipTransferInvalidateSessionsAndDirectStaleSessionsToSignInAgain|FullyQualifiedName~OwnershipTransferRaceRetainsExactlyOneOwner' --verbosity minimal -m:1 -nodeReuse:false
PASS 6/6; ownership service, HTTP server-boundary, role/session and concurrency regression set.

dotnet test tests/Bingo.IntegrationTests/Bingo.IntegrationTests.csproj --configuration Release --no-restore --filter 'FullyQualifiedName~Slice1IdentityIntegrationTests.WebsiteResetLinkIsVisibleAfterAuthorizedGenerationAndCanBeConsumed' --verbosity minimal -m:1 -nodeReuse:false
PASS 1/1; authenticated reset-link generation, target suppression and consumption.

dotnet test tests/Bingo.BrowserTests/Bingo.BrowserTests.csproj --configuration Release --no-restore --filter 'FullyQualifiedName~AccountsUiTests' --verbosity minimal -m:1 -nodeReuse:false
PASS 2/2.

env NODE_PATH=/Users/christopher/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules /Users/christopher/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/bin/node tests/Bingo.BrowserTests/account-manage-dialog.test.js
PASS (exit 0).

env NODE_PATH=/Users/christopher/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules /Users/christopher/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/bin/node tests/Bingo.BrowserTests/account-support.browser.js
PASS; Chromium reached all account-management and AU24 transfer assertions with no page errors.

dotnet build Bingo.slnx --configuration Release --no-restore --disable-build-servers
PASS; 0 warnings, 0 errors.
```

## Review boundary and next action

Claude's direct recheck of the two B1 follow-up commits was source-only and
covered `ea60b64..a9399b2`; it reported PASS without executing these checks.
The results above are post-merge runtime evidence from the packager. The next
permitted action is Claude's merge-only scope check. No push, main merge,
deployment, B3 work or new production changes were performed.

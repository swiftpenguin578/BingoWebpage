# B4 item 2 — AU23 rate text and roll mechanics

Status: implemented by the B4 worker in `00dfd40` (`fix(catalogue): apply admin rate roll text`); external Claude review and new-UI/manual acceptance are pending.

Remediation status: the D8 snapshot/deployment decision and its checks are
recorded in [`remediation/item3-d8-snapshot.md`](remediation/item3-d8-snapshot.md)
and committed in `c2b00b2`; the initial AU23 checkpoint above remains historical.

The 4 October decision makes the complete rate text ordinary Admin input: `3/1024` means one roll at `3/1024`, while `3 x 1/1024` means three rolls at `1/1024`, including add, edit and reactivation. New drops use `default`. Only a SuperAdmin whose role is checked from the database may change a non-default roll group; existing named groups remain unchanged. `conditional_on_parent` and `parent_probability` are retired from new input for every role while their columns, records and snapshots remain, and no EHB parent-chance fix is added. See [08-decisions.md](/Users/christopher/Documents/BingoWebpage/review-notes/08-decisions.md), “AU23 roll count”, “Drop-rate mechanics” (final Option 1), and “B4 (Catalogue) brief decisions”, together with the [authorized B4 brief](/Users/christopher/Documents/BingoWebpage/review-notes/27-codex-brief-b4-catalogue.md) lines 50–59.

The Catalogue handlers now apply parser-derived roll count and probability for ordinary add/edit/reactivation, remove the old reward-roll refusal, default new drops to `default`, retain existing groups, and require a database-checked SuperAdmin for a group change. Retired conditional fields remain refusal paths with no write. Validation, optimistic concurrency, audit and affected-draft refresh paths remain in place.

Worker-executed PostgreSQL checks:

```text
dotnet test tests/Bingo.IntegrationTests/Bingo.IntegrationTests.csproj --configuration Release --no-build --filter FullyQualifiedName~Au23
Passed 2, Failed 0, Skipped 0, Total 2

dotnet test tests/Bingo.IntegrationTests/Bingo.IntegrationTests.csproj --configuration Release --no-build --filter FullyQualifiedName~Cat01OrdinaryRateSave
Passed 2, Failed 0, Skipped 0, Total 2

dotnet test tests/Bingo.IntegrationTests/Bingo.IntegrationTests.csproj --configuration Release --no-build --filter FullyQualifiedName~Cat01RetiredMetadataSubmission
Passed 1, Failed 0, Skipped 0, Total 1

dotnet test tests/Bingo.IntegrationTests/Bingo.IntegrationTests.csproj --configuration Release --no-build --filter FullyQualifiedName~Au21
Passed 2, Failed 0, Skipped 0, Total 2
```

The AU23 cases exercise ordinary role add/edit/reactivation, `N x` parsing, database role refusal/acceptance and forged-role refusal. The user's 4 October `conditional_on_parent` count of zero is quoted in the source decisions and brief; it was not run by this worker. No production or user-owned database was accessed. Build and documentation checks are recorded in [item4-docs-register.md](item4-docs-register.md).

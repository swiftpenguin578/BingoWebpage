# B4 remediation item 2 — CAT-1 ordinary Admin binding

Status: worker-implemented in `c2d2220`; external Claude direct recheck remains
pending. The activity `team_size` field remains informational, defaults to 1,
and is never used in EHB/Luck calculations. Missing input preserves the stored
value. Sources are [08-decisions.md, Catalogue layout](/Users/christopher/Documents/BingoWebpage/review-notes/08-decisions.md#catalogue-layout-decided-4-october),
[08-decisions.md, B4 brief decisions](/Users/christopher/Documents/BingoWebpage/review-notes/08-decisions.md#b4-catalogue-brief-decisions),
and the [review-28 remediation brief](/Users/christopher/Documents/BingoWebpage/review-notes/28-b4-catalogue-review.md).

The read contract now carries `BossRow.TeamSize`. Ordinary-Admin direct Add and
update paths are covered. Real HTTP binding distinguishes an omitted field from
supplied `abc`, `2.5`, empty, and `0`; each invalid value returns the normal error
path without changing the activity or its version, while omitted input preserves
the stored value. No current-page control was added.

Worker-executed PostgreSQL checks:

```text
dotnet test tests/Bingo.IntegrationTests/Bingo.IntegrationTests.csproj --configuration Release --no-build --filter FullyQualifiedName~Cat1TeamSize
Passed 2, Failed 0, Skipped 0, Total 2

dotnet test tests/Bingo.IntegrationTests/Bingo.IntegrationTests.csproj --configuration Release --no-build --filter FullyQualifiedName~Cat1TeamSizeHttpBindingRejectsInvalidValuesWithoutMutationForOrdinaryAdmin
Passed 1, Failed 0, Skipped 0, Total 1
```

No production or user-owned database/provider was accessed. Current-page
Team-size binding and manual UI acceptance remain deferred to WA-5/RC10.

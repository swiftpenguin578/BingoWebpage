# B4 remediation item 1 — AU21 shared-item scope

Status: worker-implemented in `c4c52a4`; external Claude direct recheck remains
pending. This remediation follows D7 option (a) and D9 option (a): the
structured affected-activity ID/name set is required for shared rename/image
changes on edit and on Add-drop adoption. Sources are [08-decisions.md, B4
brief decisions](/Users/christopher/Documents/BingoWebpage/review-notes/08-decisions.md#b4-catalogue-brief-decisions),
[08-decisions.md, review decisions](/Users/christopher/Documents/BingoWebpage/review-notes/08-decisions.md#b4-catalogue-review-decisions),
and the [review-28 remediation brief](/Users/christopher/Documents/BingoWebpage/review-notes/28-b4-catalogue-review.md).

The handlers normalize both stored and submitted image URLs, refuse missing or
stale structured confirmation without writes, and recheck the current affected
set inside the serializable transaction. Rate-only edits preserve legacy stored
image forms. Tests cover rename/image refusal and confirmation, partial and
new-dependency confirmation sets, Add-drop adoption with different/equivalent
normalized images, single-activity rename, and no audit/version mutation on
every refusal.

Worker-executed PostgreSQL check:

```text
dotnet test tests/Bingo.IntegrationTests/Bingo.IntegrationTests.csproj --configuration Release --no-build --filter FullyQualifiedName~Au21
Passed 7, Failed 0, Skipped 0, Total 7
```

No production or user-owned database/provider was accessed. Current-page
confirmation binding and manual UI acceptance remain deferred to WA-5/RC10.

# Item 5 — C2 publication comparison

Backend remediation implemented; Claude recheck and RC05 binding pending.
Authority: supplied brief35 section 2 item 5 and report31c M2. Comparison uses the
replacement publication's inputs: retained objectives' frozen drop/boss facts,
current catalogue inputs for new objectives, authored/current tile facets and
manual override. Automatic description and tile-EHB projection share the approval
helpers. Publishing retained objectives also uses their frozen names for automatic
descriptions, so a catalogue rename alone does not alter an untouched tile.
Projection performs read-only queries and rounds EHB to the actual PostgreSQL
snapshot precision (numeric 14,4). It neither saves a preview nor takes write locks.
Every compared field remains present; JSON value equality avoids false differences
from equal decimal numbers with different serialized scales.

Proof: catalogue rate change to 13 and item rename after publication both produce
0 changed tiles for an untouched private correction, and real replacement
publication preserves its content. Two real EditTile commands produce two changed
tiles; a real Remove command retains the removed identity in the difference set.
Existing six-facet comparison checks still pass.

Existing-test setup change only:
`B5BoardCorrectionComparisonCoversFullTileContent("ehb")` sets the effective
`TileTemplate.ManualEhbOverride=4` instead of only the derived
`BoardTile.EstimatedEhbSnapshot=4`. Every assertion is unchanged (including exactly
one differing tile). Brief35 item5 explicitly requires comparing actual publish
values rather than stale cached EHB; an effective override is what approval freezes.
No existing expectation/case/assertion removed or weakened.

```
dotnet build src/Bingo.Web/Bingo.Web.csproj --no-restore -c Release -v minimal
dotnet test tests/Bingo.IntegrationTests/Bingo.IntegrationTests.csproj --no-restore -c Release --filter 'FullyQualifiedName~B5RemediationCorrectionIgnores|FullyQualifiedName~B5RemediationRealEdits|FullyQualifiedName~B5BoardCorrectionComparison|FullyQualifiedName~BoardApprovalBatchRejectsStaleRenderedCatalogueThenApprovesRefreshedInputs|FullyQualifiedName~BlankTileDescriptionDerives' --logger 'trx;LogFileName=item5.trx' --results-directory /tmp/au-b5-remediation -v minimal
dotnet test tests/Bingo.IntegrationTests/Bingo.IntegrationTests.csproj --no-restore -c Release --filter 'FullyQualifiedName~B5RemediationCorrectionIgnores|FullyQualifiedName~B5RemediationRealEdits|FullyQualifiedName~B5BoardCorrectionComparison' --logger 'trx;LogFileName=item5-comparison-corrected.trx' --results-directory /tmp/au-b5-remediation -v minimal
git diff --check
```

Build passed, 0 warnings/errors. First execution: **6 passed, 8 failed, 0 skipped**;
eight zero-difference assertions exposed textual decimal-scale inequality after
projection. Corrected comparison execution: **9 passed, 0 failed, 0 skipped**.
The five unaffected approval-freshness/description cases passed in the first run
and are reused. Diff check passed. Final whole-suite gate remains pending.

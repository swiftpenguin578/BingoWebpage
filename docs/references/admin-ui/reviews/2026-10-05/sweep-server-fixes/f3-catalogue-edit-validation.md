# F3 — catalogue activity edit validation

Status: server part complete in brief40; Catalogue binding remains pending.

Authority: brief40 F3 and the add path in `Catalogue/Index.cshtml.cs`.
`OnPostUpdateBossAsync` now validates a `BossInput` through the same
DataAnnotations path as `OnPostBossAsync` before changing an entity or creating
an audit entry.

Implementation commit: `4a0c3ecdc1a606b5f02b8ada80434df0fcca9aab`.

Evidence command (real PostgreSQL):

`dotnet test tests/Bingo.IntegrationTests/Bingo.IntegrationTests.csproj --configuration Release --no-restore --filter 'FullyQualifiedName~Slice6CatalogueAdministrationIntegrationTests.CatalogueActivityEditUsesAddValidationAndWritesOnlyValidChanges' --logger 'trx;LogFileName=/tmp/bingo-brief40-f3-rerun6.trx'` — **passed 1, failed 0, skipped 0**.

The test compares add/edit messages for a negative rate and a 201-character
name, verifies the activity, drop EHB, version and audit state remain unchanged,
then verifies a valid edit saves and recalculates EHB at the persisted four-place
precision.

Test-change mapping: this is a new proof; no existing expectation was changed.
The bounded development attempts had these reported failures before the final
pass: `CS1061` from using `EntityId` instead of the model's `TargetId`; a direct
PageModel `TryValidateModel` fixture `NullReferenceException`; an incorrect
initial audit-count assertion; and an unnormalized EHB assertion. Each was
corrected in the test/setup or assertion without weakening the required checks.

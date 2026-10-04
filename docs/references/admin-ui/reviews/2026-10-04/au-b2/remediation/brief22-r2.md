# Brief 22 — B2 item 3 / R2 midpoint rounding

Assignment/source: [brief 22 recheck and follow-up](brief22-recheck.md), copied unchanged from the supplied `review-notes/22-b1-b2-remediation-recheck.md`. Claude source-rechecked the preceding B2 range and passed it with R2; Claude did not build or run it. This record covers implementation verification only. New planner: `01a10660-cc8a-7843-abb4-6cc1ebbb2bf2`.

Started clean at `c557139`; preserved planner-only status commit `0417b53` while working. The scoped commit follows `0417b53`. Production changes are exactly four `MidpointRounding.AwayFromZero` arguments: new-rule ranking, correction discard, edit-time published scoring protection, and approval-time published scoring protection. The legacy ranking branch remains full precision. No wider rounding changes, abstraction, migration, SharedResource or CURRENT_STATUS edits.

New PostgreSQL midpoint tests reproduced three failures before the fix: discard manufactured an override of 1.0313; both new-rule ranking cases placed midpoint credit below equal stored credit. The two legacy cases passed. After the fix, all five pass within the focused 19-case PostgreSQL run. Discard freezes the exact automatic estimate 33/32 = 1.03125, writes it through numeric storage, confirms 1.0313 on reload, then discards without creating an override. Ranking derives 1.03125 and 1.0313 from frozen tiles; the new rule uses score time or shares rank, while legacy retains its original comparison. Both stored official EHB values reload as exactly 1.0313, and public/readiness/official ranks agree.

Executed checks:

- `dotnet build Bingo.slnx --configuration Release --no-restore`: passed, 0 warnings/errors.
- `dotnet test tests/Bingo.IntegrationTests --configuration Release --no-build --filter 'FullyQualifiedName~R2Midpoint|FullyQualifiedName~Au11A3|FullyQualifiedName~Au11SubmittedEvidence|FullyQualifiedName~ProvisionalAndOfficialUsePersistedRuleAndPostgresPrecision|FullyQualifiedName~FractionalCreditPublicAndOfficialRanksAgree'`: 19 passed, 0 failed/skipped.
- `dotnet test tests/Bingo.Application.Tests --configuration Release --no-build --filter 'FullyQualifiedName~Au12PlacementRuleTests'`: 8 passed, 0 failed/skipped.
- Brief byte comparison, exact production argument-only scope check and `git diff --check`: passed.

All database execution used controlled Testcontainers fixtures. No production count/query or provider call was made; existing migration count and nonrestorable Down notes remain future operator obligations. Prior snapshot-format limitations remain unchanged. No independent/self review or new manual acceptance claimed. Stop for Claude's direct R2 recheck; no merge, push, deployment or later lane work.

# Item 2 — D12 correction eligibility

Backend remediation implemented; external Claude recheck and RC07 binding pending.
Authority: supplied `review-notes/08-decisions.md`, B5 review decisions D12
(line 173), D11 Playing-only rule, and brief35 item 2 / report31a L2.
The shared CorrectionCharactersAsync pool is used both by the picker and the
serializable correction command. Current members qualify; former members must
have joined by upload and not left before upload. Latest assignment role governs,
including when both the older Playing and newer Informational rows are released.
Existing ambiguity refusal and Current/Released/LeftTeam markers are retained.

Wording conflict reported to planner: brief35's parenthetical says "after" upload,
whereas D12 says "not ended before". Following the explicit authority precedence,
LeftAt equal to SubmittedAt qualifies. The exact boundary has an executable case.

Existing-test change (setup only, every assertion unchanged):
`B5CorrectionRetainsFormerPlayingIdentityThroughReadinessAndResults` membership
leave time `now - 1 minute` → `now + 1 minute`. D12 requires a former member to
belong to the team at upload (`now`). The test still proves released identity,
left-team marker, correction, preserved assets/upload, approval, readiness and
published results. No existing expectation was loosened or removed.

Five new PostgreSQL cases prove ended-before refusal, equal-time acceptance,
left-after acceptance, current-member acceptance and latest-released-Informational
refusal. Refusals assert the exact existing message and a fresh-context full
Submissions/Assets/ReviewActions/Audits/Events snapshot is unchanged. Accepted
cases assert participant/character/team/upload/version exactly.

```
dotnet test tests/Bingo.IntegrationTests/Bingo.IntegrationTests.csproj --no-restore -c Release --filter 'FullyQualifiedName~B5RemediationCorrection|FullyQualifiedName~B5Correction|FullyQualifiedName~ReleasedPlayingAssignmentCanBeUsedForAdminCorrectionAfterReassignment' --logger 'trx;LogFileName=item2.trx' --results-directory /tmp/au-b5-remediation -v minimal
git diff --check
```

Result: 12 passed, 0 failed, 0 skipped; diff check passed. Initial compilation
failed CS0246 for the new file's missing Bingo.Domain.Access import; corrected
before the passing execution. Final unfiltered suite remains pending.

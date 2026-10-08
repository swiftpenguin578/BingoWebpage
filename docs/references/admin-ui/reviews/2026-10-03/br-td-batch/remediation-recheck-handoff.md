# G-batch remediation named-recheck handoff

Date: 3 October 2026

This handoff records implementation and executable evidence for the named
Claude recheck. It is not an independent review or manual product acceptance.

## Checkout and scope

- Checkout: `/Users/christopher/.codex/worktrees/participants-functionality/BingoWebpage`
- Branch: `codex/participants-functionality`
- Verified starting HEAD: `059faf5ba904b4a35c54eca4021fa306a2ea0586`
- Activation documentation: `711d794`
- Scope: G1, G2, G3a and G3b remediation findings from the sanitized review
  copy `remediation-review.md`. G4, G5 and G6 retain their prior implementation
  commits and review notes; no new scope was opened.

## Stable implementation commits

| Group | Commit | Named result |
| --- | --- | --- |
| G1 | `3c2a0a665daf70a89f88c317c2aa72c43cfdee97` | Cumulative approval-order simulation, deterministic C33 approval outcomes, team/concurrency proof, Danish strings and RC07 notes. |
| G2 | `a4a640c` | Danish paused-interval, upload-time label, earlier-review link and refusal guidance; verified-unused stale keys removed. |
| G3a | `93d75f7` | Draft version writes for inclusion/removal, Start lock-boundary conflict handling, and PostgreSQL race proof for Start/direct Finalize. |
| G3a probe | `4a99106` | Boundary interceptor matches generated `events ... FOR UPDATE` SQL without depending on an EF alias. |
| G3b | `6628e4a` | Manual Remove/Move Running gate and unchanged-cap-only capacity floor. |

## Executed checks

Final scoped Release build (0 warnings, 0 errors):

```text
dotnet build tests/Bingo.IntegrationTests/Bingo.IntegrationTests.csproj --no-restore --configuration Release --disable-build-servers
```

Required full PostgreSQL-backed classes, all passing:

```text
dotnet test tests/Bingo.IntegrationTests/Bingo.IntegrationTests.csproj --no-build --configuration Release --filter 'FullyQualifiedName~DraftOperationsIntegrationTests' --logger 'console;verbosity=minimal'
# Passed 56/56
dotnet test tests/Bingo.IntegrationTests/Bingo.IntegrationTests.csproj --no-build --configuration Release --filter 'FullyQualifiedName~SubmissionWorkflowTests' --logger 'console;verbosity=minimal'
# Passed 79/79
dotnet test tests/Bingo.IntegrationTests/Bingo.IntegrationTests.csproj --no-build --configuration Release --filter 'FullyQualifiedName~C33FinalizationFreshnessTests' --logger 'console;verbosity=minimal'
# Passed 29/29
```

Focused changed-boundary checks also passed:

- G1 focused SubmissionWorkflow and C33 checks, recorded in `G1-evidence.md`.
- G2 review details/paused interval checks, recorded in `G2-evidence.md`.
- G3a `TeamInclusionChangeAndStartSerializeInBothOrders`: 2/2 orderings;
  `TeamInclusionChangeAndDirectFinalizeSerializeInBothOrders`: 2/2;
  `RemovingDraftTeamAndStartSerializeInBothOrders` and running/finalized
  refusal coverage: 2/2. The first operation held the real draft row lock;
  `pg_blocking_pids(waiting_pid)` proved the second event-lock operation was
  waiting before release. Evidence is in `G3a-evidence.md`.
- G3b `ManualRosterRemoveAndMoveAreLockedWhileDraftRuns`,
  `ManualRosterAdditionRequiresConfirmedParticipantAndStopsWhenDraftRuns` and
  `LiveEndChangeAllowsUnchangedLegacyCapacityBelowConfirmedCount`: 3/3.
  Evidence is in `G3b-evidence.md`.
- `git diff --check` and staged diff checks passed for each local commit.

The first full DraftOperations attempt timed out in the existing
`DraftStartInterleavesWithSelectedCapacityAndAccountMutationAtTheRealBoundary`
test because its interceptor required the provider's old `FROM events AS e`
alias spelling. The probe was corrected in `4a99106`; the focused test then
passed 1/1 and the complete class passed 56/56. This was test-boundary text,
not a product assertion failure.

## Review boundary

All evidence uses controlled Testcontainers PostgreSQL fixtures. No production
database, provider API, user-owned database, deployment, push, merge or
rehearsal was used. R-3 remains deferred and R-1 remains the existing release
conversion blocker. Claude owns the independent named recheck of the five
remediation commits above; the implementer is stopped at that review boundary.

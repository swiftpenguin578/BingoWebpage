# G1 approval order evidence

This record is scoped to G1 (BR-1). It contains only sanitized fixture and test
evidence; no production identifiers, participant data, provider calls, or user
database access are included.

## Baseline and implementation

- Source baseline for the remediation: `059faf5ba904b4a35c54eca4021fa306a2ea0586`.
- Activation checkpoint: `711d794`.
- This record covers the named G1 remediation checkpoint; Claude's independent
  named recheck remains pending.
- The existing Serializable transaction and event-row `FOR UPDATE` boundary
  continue to serialize review decisions. The room-order check runs after that
  lock and before any submission, contribution, audit, notification, or event
  statistics write.

## Behavior proved

`ApproveAsync` still computes the current submission's amount from target room,
claimed weight, drop caps, and duplicate rules. Before mutating the decision, it
walks earlier pending submissions in immutable `SubmittedAt`, then ID order,
through two cumulative reservation states: one without the current upload and
one with its amount applied first. Each candidate is added to both running states
when it fits; the first candidate whose possible amount drops is returned as
structured `SubmissionApprovalBlock(SubmissionId, SubmittedAt)` data. A refusal
returns `SubmissionApprovalResult` with zero contribution and leaves the
transaction without writes. A no-room approval with no earlier pending blocker
uses the message that the objective has no remaining eligible contribution under
the published rules.

The Review Details page stores only the structured blocking ID in TempData,
rechecks the same event/team/tile/objective and earlier-order relationship, and
renders the upload time plus a direct Details link while preserving event,
search, and status route context. The UI does not parse service message text.

## Controlled PostgreSQL checks

The following checks passed against Testcontainers PostgreSQL:

- `LaterApprovalSimulatesMultipleEarlierPendingUploadsCumulatively`: a target-2
  objective with two earlier one-contribution uploads identifies the second
  earlier upload when the later upload would consume its room.
- `LaterApprovalSimulatesEarlierUploadsSharingOneDropCapCumulatively`: two
  earlier uploads sharing one per-drop cap are simulated cumulatively and the
  second earlier upload is returned as the blocker.
- `LaterApprovalBlocksWithOnlyOneRoomLeftAtTargetAboveOne`: a target-3
  objective with two approved contributions blocks the later upload against the
  one remaining room and returns the earlier pending submission.
- `ApprovalOrderBlockIsScopedToTheSameTeam`: an earlier upload for another team
  does not block approval of the current team's upload for the same objective.

- `LaterApprovalReturnsEarlierPendingBlockAndUnblocksAfterResolution`: target
  one; later approval returns the earlier ID/time, persists no decision,
  contribution, audit, or event-version change; rejecting the earlier upload
  unblocks the later approval.
- `LaterApprovalUsesRoomAndUploadOrderWithoutBlockingWhenBothFit`: both
  contributions fit; later approval succeeds and completion provenance retains
  the existing upload-time calculator behavior.
- `LaterApprovalBlocksWhenItConsumesAnEarlierDropCap`: a shared per-drop cap
  returns the earlier structured blocker without a contribution.
- `ApprovalOrderBlockIsScopedToTheSameObjective`: a pending upload for another
  objective is not treated as a blocker.
- `ConcurrentApprovalInBothLockOrdersRespectsEarlierUpload`: independent
  PostgreSQL connections are held at the event-row lock in each order. A later
  first attempt returns the earlier blocker and the earlier attempt then wins;
  an earlier first attempt wins and the waiting later attempt receives the
  expected PostgreSQL serialization failure. Exactly one contribution and one
  approved submission remain in each race.
- `ReviewApprovalRefusalRendersEarlierSubmissionLink`: the real HTTP Review
  Details flow renders the blocker message and direct earlier-review link, with
  no approval write.
- The full `C33FinalizationFreshnessTests` class passed 29/29 after updating the
  shared fixture to deterministic microsecond-aligned upload times and asserting
  successful nonblocked helper approvals. The focused `SubmissionWorkflowTests`
  set passed 7/7.

Build and diff checks:

```text
dotnet build tests/Bingo.IntegrationTests/Bingo.IntegrationTests.csproj --no-restore /p:UseSharedCompilation=false
git diff --check
```

Both passed. No production migration or schema change is part of G1.

# G1 approval order evidence

This record is scoped to G1 (BR-1). It contains only sanitized fixture and test
evidence; no production identifiers, participant data, provider calls, or user
database access are included.

## Baseline and implementation

- Source baseline: `f2ea1cffb8f4d9c0b23dd68dcf3f6675d1bbb5d5`.
- Activation checkpoint: `14d88b0`.
- This implementation checkpoint is the local G1 commit; Claude's independent
  read-only review remains pending.
- The existing Serializable transaction and event-row `FOR UPDATE` boundary
  continue to serialize review decisions. The room-order check runs after that
  lock and before any submission, contribution, audit, notification, or event
  statistics write.

## Behavior proved

`ApproveAsync` still computes the current submission's amount from target room,
claimed weight, drop caps, and duplicate rules. Before mutating the decision, it
compares that amount calculation for earlier pending submissions in immutable
`SubmittedAt`, then ID order, both before and after consuming the current amount.
The first earlier submission whose possible amount would decrease is returned as
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
  an earlier first attempt wins and the waiting later attempt is refused by the
  serialized boundary. Exactly one contribution and one approved submission
  remain in each race.
- `ReviewApprovalRefusalRendersEarlierSubmissionLink`: the real HTTP Review
  Details flow renders the blocker message and direct earlier-review link, with
  no approval write.
- The scoped `SubmissionWorkflowTests` class passed all 75 existing tests after
  updating only fixtures whose equal timestamps had relied on creation order;
  those fixtures now use deterministic microsecond-aligned upload times.

Build and diff checks:

```text
dotnet build tests/Bingo.IntegrationTests/Bingo.IntegrationTests.csproj --no-restore /p:UseSharedCompilation=false
git diff --check
```

Both passed. No production migration or schema change is part of G1.

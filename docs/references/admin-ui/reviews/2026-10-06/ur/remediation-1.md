# UR review65 remediation — 6 October 2026

Started verified clean at `0ec7e9c3204bb2ddf74b1dbb839fbaa605728f9e` in the assigned
participants-functionality worktree. Review65 and its clarification are planner
sources labelled 7 October; accepted/relayed on actual client date 6 October.
No production output/behavior change. Original UR failures remain in item4.

## R1 — seeded history matches production writers

Used review65's permitted exact-row option in the separate review seeder. No
production helper, schema or old reset seed changes. Event rows use entity `event`
and event.Id; Board rows use `board`, board.Id and EventId per the accepted
clarification (real AddBoardAudit at Board.cshtml.cs:1313).

| Row family | Real writer and corrected output |
| --- | --- |
| Creation | EventCreationService: null before; Name/Slug/Description/Timezone/State JSON after; private-draft detail. Description uses its null default. |
| Signup open/close | EventSignupLifecycleService: singular signup action names; state JSON before; state plus ActualSignupOpenedAt/ActualSignupClosedAt after. |
| Start/end | EventLifecycleService: state before; state plus ActualStartedAt/ActualEndedAt after. |
| Publication | EventFinalizationService: state before/after and publication detail. Legacy hidden Finalized remains the explicitly permitted ruling63 historical destination. |
| Cancel/discard | EventDestructiveLifecycleService: state before/after, real cancellation reason or Empty event setup discarded detail. |
| Hide | EventQuarantineService: State/Version/HiddenAt/HiddenByAccountId/HiddenReason before/after; after captured following SaveChanges so Version is authoritative. Hide changes metadata, not lifecycle state, and its real writer adds no state transition. |
| Board approval/publication/correction | Board AddBoardAudit and callers601/899/651: approved/published actions and details, string board state and approval ID/version JSON; correction snapshot ID/workingCopy/reason JSON. |

Each state-changing operation now writes EventStateTransition with the real
from/to state, actor, reason, performed/effective instants. All synthetic operations
are manual (Scheduled=false, EffectiveAt=PerformedAt); the review-cycle snapshot
continues to reference the matching end transition. PostgreSQL timestamps remain
microsecond-aligned, including the existing nonaligned clock-input test.

New PostgreSQL assertions under R1, with no deleted/skipped/baseline tests:
allowed actions are derived from ldstr operands in the **compiled production writer
methods and async state machines**, excluding the seed and with no copied action
list. Readback checks exact event/board target types/IDs, JSON field sets and values,
real timestamp/approval IDs/version/details, required approval/publication/correction
row coverage, audit-to-transition actors/reasons/instants and complete chains from
Draft to every final persisted event state. Prior R1's blanket event entity
assertion is precisely changed to event/board by the accepted clarification; no
other assertion was relaxed.

Executed R1 focused PostgreSQL/HTTP suite **2 passed / 0 failed / 0 skipped**, both
profiles, including all prior 69/70 route and current/hidden/discard/audit checks.
Release compilation had **0 warnings/errors**; final clean gate follows R2. Diff
and frozen reference CSS/old seeder comparisons pass.
Scratch log `/private/tmp/bingo-ur-r1-final.log`; TRX
`/private/tmp/bingo-ur-r1-final-trx/_Christophers-MacBook-Air_2026-10-06_13_11_46_net10.0.trx`.

Earlier partial R1 execution was **0/2/0** on the creation Description comparison
after discard clears setup fields. Creation now uses the actual service's null
default; the final run above is a distinct passing result. The Board entity conflict
was reported, clarified and resolved before committing R1; no production change.

## R2 and stop gates — pending

R2 will align the fixed owned review runtime with postgres:17-alpine, preserving
all ownership/refusal boundaries. Required create→refresh→stop, whole BrowserTests,
clean Release and diff checks remain pending R2; JS only if covered behavior changes.
No whole .NET execution or independent self-review here. Claude/user owns final-SHA
independent recheck and unfiltered whole .NET **0 failed / 0 skipped**:

```sh
dotnet test Bingo.slnx --configuration Release --no-restore --results-directory /private/tmp/bingo-ur-final-suite-trx --logger "trx"
```

User walkthrough/CI remain pending. Owned review environment remains stopped from
original UR; user databases/processes untouched. No push/merge/deploy, U2 or lane T.

# UR review65 remediation — 6 October 2026

Started verified clean at `0ec7e9c3204bb2ddf74b1dbb839fbaa605728f9e` in the assigned
participants-functionality worktree. Review65 and its clarification are planner
sources labelled 7 October; accepted/relayed on actual client date 6 October.
No production output/behavior change. Original UR failures remain in item4.

## R1 — seeded history matches production writers (`471b94e`)

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

## R2 — PostgreSQL17 and completed stop gates

The fixed image is now **postgres:17-alpine**. Create/refresh rejects another
existing image before process/container mutation. Stop still uses exact ownership
checks. Existing owned16 was disposed only after local storage/name/label/container
ID/fixed port and **live PostgreSQL owner-token verification**; image17 availability
was checked first. Only its disposable review volume and local marker were removed;
create established a fresh owned17 container/marker. User containers/databases and
foreign processes were untouched. README documents version and image refusal.

Actual owned runtime readback: **PostgreSQL 17.10**, 21 synthetic events in both
profiles. Successful command create/live then refresh/final-review then matching
stop (the failed first refresh described below is a distinct attempt):

| Proof | create/live | refresh/final-review |
| --- | --- | --- |
| Built at UTC | 2026-10-06T11:18:53.4230750+00:00 | 2026-10-06T11:21:47.9565680+00:00 |
| Printed links | 69 | 70 |
| Event audit rows | 59 | 60 |
| Board audit rows | 21 | 21 |
| Lifecycle transition rows | 35 | 36 |

PostgreSQL readback proves **21 creation dates / 20 starts** rebuilt by exactly
**174,533,493 microseconds**. App5310/reference5320 readiness succeeded during
create/refresh. Final matching stop succeeded; container is stopped, owned process
records cleared and all three fixed ports free. Generated final guide remains in
gitignored artifacts/ui-review/scenarios.md.

First refresh safely stopped owned processes, then refused port5320. No foreign
listener was present; a controlled freshly closed socket reproduced the original
plain-bind false positive from TIME_WAIT. The port probe now uses SO_REUSEADDR
(with no SO_REUSEPORT), permitting closed sockets while refusing active listeners.
This is the narrow command-boundary correction required to finish the assigned
create→refresh→stop gate, with no foreign-process signals or timeout increases.
Changed-condition retry succeeded. Original foreign-bound-port assertion is
**unchanged**; separate active reusable-listener and recently closed-socket proofs
were added under R2's preserved safety requirement. The old-image refusal proof
also asserts no mutation. Final safety result **10 passed** (original seven plus
three additions); no safety assertion was removed/relaxed.

Final required checks: extended PostgreSQL/HTTP R1 **2/0/0** above; whole
Bingo.BrowserTests **150 passed / 0 failed / 0 skipped**; clean non-incremental
Release solution build **0 warnings / 0 errors**. Build and BrowserTests executed
on the R2 working candidate, with .NET sources identical to final R1; later R2
changes affect only command/tests/docs. Diff check and frozen CSS/legacy reset-seed
comparisons pass. JS runner **not rerun per review65's conditional gate**: no covered
JS/frontend/legacy fixture behavior changed. Original UR's 51/0 evidence is retained
as its own prior result, not a new remediation execution. No whole .NET run, CI or
independent self-review here.

Scratch proof: `/private/tmp/bingo-ur-r2-cycle-proof.json`, create/refresh summaries
and dates/guide snapshots beside it; command logs bingo-ur-r2-create.log,
bingo-ur-r2-refresh.log (safe failure), bingo-ur-r2-refresh-fixed.log and
bingo-ur-r2-stop.log. Release log `/private/tmp/bingo-ur-remediation-release.log`;
Browser log `/private/tmp/bingo-ur-remediation-browser.log` and TRX
`/private/tmp/bingo-ur-remediation-browser-trx/_Christophers-MacBook-Air_2026-10-06_13_17_25_net10.0.trx`.
The scratch date parser initially rejected .NET's seven-digit timestamp format;
its correction explicitly requires the seventh tick digit to be zero, then compares
exact microseconds. No product timestamps/tolerances changed and commands were not
rerun for that parser error.

Stop after this scoped R2 local commit. Claude/user owns fresh independent UR
recheck, final-SHA unfiltered whole .NET **0 failed / 0 skipped**, and user walkthrough:

```sh
dotnet test Bingo.slnx --configuration Release --no-restore --results-directory /private/tmp/bingo-ur-final-suite-trx --logger "trx"
```

All three are pending; implementation/gate completion is not acceptance. Accepted
U1 remains unchanged. No push/merge/deploy, U2, lane T or migration rehearsal.

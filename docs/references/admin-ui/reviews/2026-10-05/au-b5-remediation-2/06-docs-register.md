# Item 6 — contracts, register and final checkpoint

Authority: brief38 item6; review36/36a–d; `review-notes/08-decisions.md`, D19(a),
AU19/AU17 Decision1, S2/S3/S9, A-Overview-6 and “B5 remediation, item7” planner
wording correction. External Claude recheck and binding remain pending.

## Required corrections

- FUNCTIONAL_CONTRACTS Board publication now records D19: unchanged objectives'
  automatic descriptions keep frozen item names; catalogue renames never change
  untouched tiles. Existing implementation is retained; no behavioral edit here.
- E1 register and prior item6/item8 evidence now use **CreatedAt >= RosterPublishedAt**
  solely as evidence of operation creation at/after that publication. UpdatedAt
  cannot establish relevance: an older operation already Claimed/Sending retains
  its payload and finishes after republish (`EventCompetitionManagementService`,
  in-flight branch). ReplaceDesired preserves CreatedAt on merged pending updates,
  conservatively classifying them as older. No synchronization claim is invented.
  Original real PostgreSQL roster/operation proof3/3 remains applicable; this is a
  documentation correction, not a new WOM/provider change.
- Removed the blank line splitting the register. S2 includes the Live/Final review
  condition; S3 includes “Automatic start postponed” styling; A-Overview-6 drops
  the reopening-keeps-signups warning. Session-loss correction is attributed to
  the planner, not the user; existing HTTP302/handler Forbid behavior is retained.
- AU17 restores “nothing outside Review.dc.html”; the excluded per-account context
  is the prior-approved count, while S9's decided team-leave warning remains in
  scope. Matching functionality wording is aligned.
- AU19 has a new binding row: group empty-position issues into one “(N empty)”
  item targeting/highlighting the first empty position. Missing per-drop rate
  notes and first-only publish refusal reasons are explicitly open BR-10 points.
  Prior item8's obsolete no-row assessment is replaced with why it changed; the
  earlier item4 gap note is aligned so it cannot remain competing guidance.
- AU14/AU17a/AU17/AU19 statuses, including EVD-01/DRF-01/BRD-01/BRD-02 and the four
  named FUNCTIONALITY_CHANGES rows, now say **backend implemented, remediation
  round 2 done, Claude recheck pending, binding pending**. CURRENT_STATUS replaces
  the old checkpoint and stays below100 lines. Sweep rows remain obligations,
  not implementation claims. No scope/design decision has been invented.

## Executed focused evidence and test-change provenance

| Item | Local commit | Final focused evidence |
| --- | --- | --- |
| 1 board GP message | `aae31f4` | Integration2 passed,0 failed,0 skipped |
| 2 collected issues | `81de7ca` | Integration3 passed,0 failed,0 skipped |
| 3 grid safety | `7a36303` | Integration2 passed,0 failed,0 skipped |
| 4 AU19 gaps | `2ac69f5` | Three new proofs pass:2 initial passes reused plus corrected projection1/1 |
| 5 D12 join boundary | `4080e61` | Integration2 passed,0 failed,0 skipped |

[Item1](01-board-price-message.md), [item2](02-collected-issue-precedence.md),
[item3](03-grid-safety.md), [item4](04-au19-proofs.md) and
[item5](05-former-member-join-boundary.md) contain exact commands and provenance.
The item4 initial decimal-format failure and item5 missing-import compiler failure
are recorded honestly; no previously committed test expectation changed. Item4's
new fractional fixture preserves all assertions. Prior null→1 Position expectation
is now cited to recorded AU19, with its old→new→decision mapping retained.

Item6 scoped checks executed before commit: `git diff --check` PASS; Python
consistency assertions PASS for the contiguous31-row five-column register, every
named qualifier/open point, all12 referenced design files, exact named statuses,
D19 contract, removal of obsolete guidance, AU17/S9 scope and handoff length.
No .NET test rerun is needed for these documentation-only edits. Prior applicable
PostgreSQL evidence is retained, separate from the final whole-suite gate.

## Gate to execute on this final commit, then stop

```sh
dotnet clean Bingo.slnx -c Release -v minimal
dotnet build Bingo.slnx -c Release --no-restore -v minimal
git diff --check
dotnet test Bingo.slnx -c Release --no-build --no-restore --logger trx --results-directory /tmp/au-b5-remediation-2/final -v minimal
git status --short --branch
git rev-parse HEAD
```

The clean build and unfiltered whole-suite run are **pending at this committed
checkpoint**; focused results are not that gate. Run once on this sixth commit,
report its exact SHA and per-project counts afterward. No seventh commit/amend
merely to package final results. Stop for Claude's independent recheck; no push,
merge, deployment, UI binding, migrations, sweep fixes or rehearsal is authorized.
Claude's separate Integration run on `a4f8463` remains pending/unreported. Prior
worker-reported1975/1975 pass is reused as baseline evidence, not rerun or claimed
as this round's gate. An environment failure stops dependent execution for the
user-terminal route with exact command and checkpoint, without retries/workarounds.

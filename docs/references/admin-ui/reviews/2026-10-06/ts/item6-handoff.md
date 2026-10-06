# TS item6 — provisioning note and final-SHA gate handoff

README now explains the collection-owned container/template, clean per-test clone,
teardown, dedicated exceptions, real-credential readiness and database ownership.
CURRENT_STATUS is consolidated around this TS checkpoint and retained release
constraints. No production behavior, assertion, input, expected value or test was
changed. This checkpoint awaits Claude independent review.

| Item | Local checkpoint |
| --- | --- |
| 1 baseline | `a67aac9ab8943a4232c569ab7fdb7b1cec615017` |
| 2 shared fixture | `1d63f144e09739791a0069d3a7c21521e231dc7a` |
| 3 exceptions | `c4e00391ee6af4e065831bc11b26038813ecbdd5` |
| 4 readiness | `63b5a48fb0802fac14ca8162db08ed6e73ddda5e` |
| 5 CI/discovery | `d8cad2e11a2bfc439c3704cfa3dd12c6e4436919` |
| 6 docs/gate handoff | This documentation checkpoint; exact SHA in the post-commit record. |

Candidate results are retained in item1–item5. Discovered counts and identities are
unchanged: Domain 265, Application 118, Browser 150, Integration 1510 (2043 total).
Baseline Integration wall clock is **2135.72 seconds**. 25 dedicated exceptions
are listed in [item3](item3-exceptions.md); xUnit parallelism and CI's ten-shard
partition are unchanged. Scoped documentation/reference checks and diff checks
passed before this local commit. No packaging or later work is authorized here.

## Final commit execution

After this commit, run:

```sh
bash docs/references/admin-ui/reviews/2026-10-06/ts/run-final-gates.sh
```

The checked-in runner verifies checkout/branch/clean status, captures HEAD, performs
a clean Release build with 0 warnings/errors on that exact SHA, then executes
every test project in Bingo.slnx twice in succession. It checks the same HEAD and
unchanged tracked source before every command. Every project must execute/pass its
exact discovered count with 0 failed / 0 skipped. Each command is independently
timed with `/usr/bin/time -p`; the Integration times are exact process wall clocks,
not rounded runner durations or estimates from a solution-wide timer.

Per-project command in each run:

```sh
/usr/bin/time -p dotnet test tests/<project>/<project>.csproj --configuration Release --no-build --results-directory docs/references/admin-ui/reviews/2026-10-06/ts/artifacts/<final-sha>/run<1-or-2> --logger 'trx;LogFileName=<project>.trx'
```

Raw results/logs remain local under the ignored `artifacts/<final-sha>/` directory.
After execution, retain compact exact commands, counts, process timings, TRX IDs,
hashes, clean-build result and final SHA in `final-sha-results.json` beside this
report. That post-commit execution record is deliberately left uncommitted: making
another commit for it would move the final SHA after the two required runs. It is
repository-local handoff evidence, not a precommit result mislabeled as final-SHA
verification; retain it with this checkout when handing off the task.

Stop after the two complete passes and report. Claude's independent review and
one independent final-SHA .NET suite run are the next permitted actions. GitHub
Actions/Linux CI is unrun. No U2, lane T, UR observation fix, push, merge or deploy.

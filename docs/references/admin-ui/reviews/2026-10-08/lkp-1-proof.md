# C8 / LKP-1 — executed PostgreSQL proof, awaiting independent review

Brief89; implementer only, `gpt-6.1-sol` / high. Checkout `/Users/christopher/.codex/worktrees/c8-luck-proof/BingoWebpage`, branch `codex/c8-luck-proof`, clean base `4b4828220e9d8ab98c1368d831b2c17c015e42ef`. Tested source is item1 commit `16162245c6780ad3deb5e5cf950b633b836922f8`; item2 changes documentation only. Execution crossed midnight on 7–8 October2026 (Europe/Copenhagen).

The existing fixture now optionally assigns the second outcome to a separate boss. One unsupported outcome alongside supported activity gives **Incomplete** player, team and event results, with unavailable expected/percentile/KC totals. A subsequent compatible mixed batch replaces that incomplete checkpoint (unchanged source/assignment fingerprints, new batch/payload and updated supported KC). Its later first verified metric binding permits a complete replacement; a separate missing-outcome case also proves incomplete → complete replacement with the same source/assignment identity.

Both later-partial cases (missing and unexpected-unranked second metric) accept new supported KC900 while retaining the previous complete checkpoint as UTF-8 byte-identical PostgreSQL payload, batch/evidence/fingerprints and original calculation/fetch/upstream times. Approved evidence then changes from one drop to two. Repeated Stats and selected-tile reads, before and after real publication/archive, still equal the saved event/player/team/tile results; tile metrics and provenance also remain saved. The counting provider used by the original accepted fetch stays at one validation and one metric call throughout those reads and finalization (the controlled early-end window skips another fetch).

The strengthened existing state theory distinguishes missing and unexpected-unranked issues, estimated and zero-recorded flags, ranked zero with/without evidence, and supported modes. The mixed test distinguishes unsupported source basis (`Unmapped` / `SourceBasisUnavailable`). Existing executed bounded PMF checks retain KC/expected while withholding percentile; contradictory-mechanics checks retain explicit Incomplete values. Deterministic UTC fixture clocks are microsecond-aligned. A non-aligned upstream timestamp (`+7` ticks) crosses real PostgreSQL persistence and is compared exactly to its microsecond-truncated value in both the checkpoint and saved public projection, without tolerance or sleeps. The price-hour fixture input is separately normalized to its required whole hour.

Added methods (all in `Slice10Pass102CompetitionSynchronizationTests`):

- `StatsPass4Lkp1MixedUnsupportedOutcomeIsIncompleteAndReplaceableUntilComplete`
- `StatsPass4Lkp1MixedMissingOutcomeIsReplacedByCompatibleCompleteBatch`
- `StatsPass4Lkp1PartialBatchRetainsWholeCompleteSnapshotThroughPublicationAndArchive` (`unranked: false/true`)
- Added `unexpected-unranked` case and persistence/flag assertions to `StatsPass4CompleteQueryPreservesUnavailableZeroAndEstimatedStates`.

| Executed check | Passed / failed / skipped | Result |
| --- | --- | --- |
| Initial new proofs + state theory | 9 / 3 / 0 | Invalid fixture price-hour input for the three fractional-clock cases; corrected test support only |
| Corrected LKP-1 cases | 4 / 0 / 0 | PostgreSQL proof; later full run includes stronger entire-tile-team assertions |
| Exactly six authorized files: 57 methods / 92 cases | 92 / 0 / 0 | Final test source; PostgreSQL Testcontainers, exit0 |
| Final Release test-project build | n/a | exit0, 0 warnings / 0 errors, 3.97s |
| `git diff --check` and staged diff check | n/a | exit0 |

The initial build also caught two xUnit2031 analyzer errors in the new assertions; both were corrected. These construction failures were not known base failures or production defects. No base failure or production defect was observed in the completed scoped run. Source changes are only the two existing StatsPass4 test files; no production, UI, migration or test-framework change. The shared review environment was untouched; review-environment needs: **none**. Whole .NET and JS suites were not run. Independent review and acceptance are pending; next permitted action is to wait for the planner's review, not start U9.

Exact commands, from the assigned checkout (output captured then tailed):

```sh
dotnet test tests/Bingo.IntegrationTests/Bingo.IntegrationTests.csproj --configuration Release --filter 'FullyQualifiedName~StatsPass4Lkp1|FullyQualifiedName~StatsPass4CompleteQueryPreservesUnavailableZeroAndEstimatedStates' --logger 'trx;LogFileName=c8-new.trx' --results-directory /tmp/c8-test-results > /tmp/c8-new.log 2>&1
tail -28 /tmp/c8-new.log

dotnet test tests/Bingo.IntegrationTests/Bingo.IntegrationTests.csproj --configuration Release --filter 'FullyQualifiedName~StatsPass4Lkp1' --logger 'console;verbosity=normal' --logger 'trx;LogFileName=c8-lkp1.trx' --results-directory /tmp/c8-test-results > /tmp/c8-lkp1.log 2>&1
tail -28 /tmp/c8-lkp1.log

python3 - <<'FILTER'
import re
from pathlib import Path
files = ['StatsPass4ReviewCorrectionsIntegrationTests.cs',
         'StatsPass4QueryIntegrationTests.cs',
         'StatsPass4BoundaryIntegrationTests.cs',
         'StatsPass3LuckBasisIntegrationTests.cs',
         'TileActivityIntegrationTests.cs',
         'Au20PublicationFallbackIntegrationTests.cs']
names = []
for name in files:
    names += re.findall(r'public async Task (\w+)\(',
                        (Path('tests/Bingo.IntegrationTests') / name).read_text())
Path('/tmp/c8-fast.filter').write_text('|'.join('FullyQualifiedName~.' + n for n in names))
FILTER
c8_filter="$(cat /tmp/c8-fast.filter)"
dotnet test tests/Bingo.IntegrationTests/Bingo.IntegrationTests.csproj --configuration Release --filter "$c8_filter" --logger 'console;verbosity=normal' --logger 'trx;LogFileName=c8-fast.trx' --results-directory /tmp/c8-test-results > /tmp/c8-fast.log 2>&1
tail -28 /tmp/c8-fast.log

dotnet build tests/Bingo.IntegrationTests/Bingo.IntegrationTests.csproj --configuration Release > /tmp/c8-build-gate.log 2>&1
tail -12 /tmp/c8-build-gate.log
git diff --check
git diff --cached --check
```

The filename-derived filter avoids running unrelated methods in the shared partial classes. Completed fast TRX: `/tmp/c8-test-results/c8-fast.trx`, 23:55:00.617622+02:00 on7October → 00:17:53.947033+02:00 on8October; SHA256 `b018a632ad23fa8bf7ef8aefc098865aa055e1c2f578b5211e809b58a7b1c6d5`. Corrected proof TRX SHA256 `75e4b09f8d2138ee1481f448f573e308947a1bd89ffbfc28a6b0ddf7d972120c`; initial fixture-failure TRX SHA256 `a9375ede277ac76e2cc22e7f3e448d7cbc10ad745937cfdbb5ebe8f8fb0d1768`. Raw runtime logs/TRX remain scratch artifacts; this note is the durable compact execution record, not independent review.

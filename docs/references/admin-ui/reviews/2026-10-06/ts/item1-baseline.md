# TS item1 — unchanged baseline

Assignment: brief66; implementer only, awaiting Claude independent review.
Machine: macOS 26.5.2, .NET SDK 10.0.301, Docker Engine 29.6.1.
Checkout: `/Users/christopher/.codex/worktrees/participants-functionality/BingoWebpage`.
Branch: `codex/participants-functionality`.
Verified clean starting HEAD: `8fd559c8125592cdefa76c75ea06ab8e667860e9`.

Before setup changes, `dotnet build Bingo.slnx --configuration Release --no-restore`
passed with 0 warnings / 0 errors (MSBuild elapsed 25.31 seconds).

Exact discovery command, once for each project below:

```sh
dotnet test tests/<project>/<project>.csproj --configuration Release --no-build --list-tests --logger 'console;verbosity=quiet'
```

| Project | Discovered cases | Exit |
| --- | ---: | ---: |
| Bingo.Domain.Tests | 265 | 0 |
| Bingo.Application.Tests | 118 | 0 |
| Bingo.BrowserTests | 150 | 0 |
| Bingo.IntegrationTests | 1510 | 0 |
| Total | 2043 | |

Scratch discovery identities: `/private/tmp/bingo-ts-baseline.hQXteN/<project>.discovery.log`.
Canonical case identity is SHA-256 of the indented `Bingo.*` case lines with the
four-space indent removed and sorted with `LC_ALL=C`:

| Project | Canonical discovered-case SHA-256 |
| --- | --- |
| Domain | `5d13b73808dbe26c759ea739a64c5a26cd8f18863bee16aa895954acb13b62dc` |
| Application | `fe07d230c6c831b493bd31278a7c6096f7e0d9bdf4181c18d6bb983ea4c4585a` |
| Browser | `bbdb454770334ac6ce49d4a935a4ac17405aa069d69d587418ca08a9971e6d20` |
| Integration | `f68798b1541271000b8a1bd5fe0195ff8eb3f6c2e576534dfb1bdb5a807ff3bc` |

Baseline command (unchanged test infrastructure, Release, no build):

```sh
/usr/bin/time -p dotnet test tests/Bingo.IntegrationTests/Bingo.IntegrationTests.csproj --configuration Release --no-build --results-directory /private/tmp/bingo-ts-baseline.hQXteN/results --logger 'trx;LogFileName=integration-baseline.trx'
```

Baseline passed: **1510 passed / 0 failed / 0 skipped**, exit **0**.
Measured process wall-clock: **2135.72 seconds (35m 35.72s)**; runner-reported
duration: 35m 34s. `/usr/bin/time` user/system: 1871.76 / 567.74 seconds.
TRX run ID: `0139d326-9746-428d-85bd-674b4acfc411`.
TRX start/finish: `2026-10-06T14:23:46.4559210+02:00` /
`2026-10-06T14:59:21.3861480+02:00`.
TRX SHA-256: `91dff9b616727d665d76a5caffe7395f1c35a8295e7ca3fd42c81773aa841d57`.
The 64 MiB raw TRX is scratch evidence; this compact durable report retains its
identity and exact counters instead of committing unnecessary runtime SQL output.

Before-change CI partition command:

```sh
bash .github/scripts/partition-integration-tests.sh tests/Bingo.IntegrationTests/Bingo.IntegrationTests.csproj Release 10
```

Exit 0: 1510 cases / 74 classes / 10 nonempty shards. Shard counts:
`212, 169, 142, 141, 141, 141, 141, 141, 141, 141`.
Partition JSON identity: `/private/tmp/bingo-ts-baseline.hQXteN/partition-before.json`,
SHA-256 `5d39650a85db0748336c72c63eeb1b8cd9594f9944fdf2a36c9bebda082b75da`.

All of the above was executed before any setup change. No assertion/input/expected-value
changes, production changes or user-owned database access occurred. This is the
starting-SHA baseline, not final-SHA verification.

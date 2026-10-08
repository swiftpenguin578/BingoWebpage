# TS item5 — unchanged discovery and CI partition

After code checkpoint `63b5a48fb0802fac14ca8162db08ed6e73ddda5e`, repeated the
exact item1 Release/no-build discovery command for each project. All exited 0.

| Project | Before | After | Canonical case identity |
| --- | ---: | ---: | --- |
| Domain | 265 | 265 | `5d13b73808dbe26c759ea739a64c5a26cd8f18863bee16aa895954acb13b62dc` |
| Application | 118 | 118 | `fe07d230c6c831b493bd31278a7c6096f7e0d9bdf4181c18d6bb983ea4c4585a` |
| Browser | 150 | 150 | `bbdb454770334ac6ce49d4a935a4ac17405aa069d69d587418ca08a9971e6d20` |
| Integration | 1510 | 1510 | `f68798b1541271000b8a1bd5fe0195ff8eb3f6c2e576534dfb1bdb5a807ff3bc` |
| Total | 2043 | 2043 | Every sorted discovered case line is identical. |

Canonicalization is the item1 four-space indent removal and `LC_ALL=C` sorting,
followed by SHA-256. After logs:
`/private/tmp/bingo-ts-baseline.hQXteN/<project>.after-discovery.log`.

```sh
bash .github/scripts/partition-integration-tests.sh tests/Bingo.IntegrationTests/Bingo.IntegrationTests.csproj Release 10
cmp /private/tmp/bingo-ts-baseline.hQXteN/partition-before.json /private/tmp/bingo-ts-baseline.hQXteN/partition-after.json
git diff 8fd559c8125592cdefa76c75ea06ab8e667860e9 --exit-code -- .github/workflows/ci.yml .github/scripts/partition-integration-tests.sh
```

All exited 0. **1510 cases / 74 classes / 10 nonempty shards**; exact before/after
counts `212, 169, 142, 141, 141, 141, 141, 141, 141, 141`. The script validates each
shard's class filter and case count, class ownership, no overlap/omission and total
coverage. Before/after matrix JSON is byte-identical; SHA-256
`5d39650a85db0748336c72c63eeb1b8cd9594f9944fdf2a36c9bebda082b75da`.

Decision: **ci.yml unchanged; partition script unchanged; shard count unchanged**.
The shared helper is a relative linked C# compile item, with no platform-specific
runtime paths or new dependencies. Existing class-based shard filtering gives the
native fixture its lifetime for the selected cases. This is local partition and
source compatibility evidence; GitHub Actions/Linux CI was not executed.
No xUnit parallelism settings or collection assignments changed. Final-SHA suite
timings will be recorded separately after the documentation checkpoint, and will
not be relabeled from these candidate discovery checks. `git diff --check` passed.

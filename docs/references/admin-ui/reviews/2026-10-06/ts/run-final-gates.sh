#!/usr/bin/env bash
set -uo pipefail

ts_repo=$(git rev-parse --show-toplevel)
ts_sha=$(git rev-parse HEAD)
[[ "$ts_repo" == /Users/christopher/.codex/worktrees/participants-functionality/BingoWebpage ]] || exit 64
[[ $(git branch --show-current) == codex/participants-functionality ]] || exit 64
[[ "$ts_sha" =~ ^[0-9a-f]{40}$ ]] || exit 64
[[ -z $(git status --porcelain) ]] || exit 64
ts_artifacts="$ts_repo/docs/references/admin-ui/reviews/2026-10-06/ts/artifacts/$ts_sha"
[[ ! -e "$ts_artifacts/commands.tsv" ]] || exit 64
mkdir -p "$ts_artifacts"

dotnet clean Bingo.slnx --configuration Release --verbosity quiet > "$ts_artifacts/clean.log" 2>&1 || exit 1
dotnet build Bingo.slnx --configuration Release --no-restore > "$ts_artifacts/build.log" 2>&1 || { tail -n 20 "$ts_artifacts/build.log"; exit 1; }
tail -n 7 "$ts_artifacts/build.log"
rg -q '^    0 Warning\(s\)' "$ts_artifacts/build.log" || exit 1
rg -q '^    0 Error\(s\)' "$ts_artifacts/build.log" || exit 1

# Exactly all four test projects in Bingo.slnx, twice consecutively. Individual
# process timing gives the Integration project an exact no-build wall clock.
ts_projects=(Bingo.Domain.Tests Bingo.Application.Tests Bingo.BrowserTests Bingo.IntegrationTests)
ts_expected=(265 118 150 1510)
for ts_run in 1 2; do
    ts_results="$ts_artifacts/run$ts_run"
    mkdir -p "$ts_results"
    for ts_index in 0 1 2 3; do
        ts_project=${ts_projects[$ts_index]}
        [[ $(git rev-parse HEAD) == "$ts_sha" ]] || exit 64
        git diff --quiet && git diff --cached --quiet || exit 64
        /usr/bin/time -p dotnet test "tests/$ts_project/$ts_project.csproj" --configuration Release --no-build --results-directory "$ts_results" --logger "trx;LogFileName=$ts_project.trx" > "$ts_results/$ts_project.log" 2>&1
        ts_exit=$?
        printf '%s\t%s\t%s\n' "$ts_run" "$ts_project" "$ts_exit" >> "$ts_artifacts/commands.tsv"
        printf 'Run %s: %s, exit %s\n' "$ts_run" "$ts_project" "$ts_exit"
        tail -n 7 "$ts_results/$ts_project.log"
        [[ "$ts_exit" == 0 ]] || exit "$ts_exit"
        ruby - "$ts_results/$ts_project.trx" "${ts_expected[$ts_index]}" <<'RUBY'
line = File.foreach(ARGV[0]).find { |value| value.include?('<Counters ') } or abort 'Missing TRX counters'
counters = line.scan(/(\w+)="(\d+)"/).to_h.transform_values(&:to_i)
expected = ARGV[1].to_i
abort 'Unexpected failed/skipped/missing cases' unless %w[total executed passed].all? { |key| counters[key] == expected } && %w[failed error timeout aborted notExecuted notRunnable inconclusive].all? { |key| counters[key] == 0 }
RUBY
        [[ $? == 0 ]] || exit 1
    done
done
printf 'Both complete final-SHA suite passes succeeded at %s\n' "$ts_sha"

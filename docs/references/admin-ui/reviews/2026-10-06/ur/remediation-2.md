# UR R3 — Command Line Tools Python ownership

Implemented on 6 October 2026 from clean `311d02471923156bb8932217061b5770588232d0`, assigned checkout/branch `participants-functionality` / `codex/participants-functionality`. Authority: [review65 R3](/Users/christopher/Documents/BingoWebpage/review-notes/65-ur-review.md), “R3 found in the user's walk-through (6 October 2026)”, explicitly authorized through the dispatcher. The review65 document's earlier 7 October source label remains attributed; this R3 finding and executions occurred on the client date 6 October. One scoped local commit; no push or production behavior change.

## Change and refusal boundary

`start_process` now retains the exact launched argument list alongside its PID/start-time snapshot. After both existing HTTP readiness checks, `capture_ready_processes` saves the settled identity only if the same PID/start time and launched command arguments still match. `stop_processes` uses that comparison for new records. The existing Python shim/framework normalization remains; arbitrary executables, changed arguments/start times and legacy abbreviated snapshots are not accepted. Full legacy snapshots retain the original strict comparison. No readiness/stop timeout increase, forced kill, foreign-port takeover or ownership-marker relaxation.

The original ten safety tests and assertions are unchanged. Two focused tests add (1) changed owned arguments/start time refusal without signalling or rewriting the saved snapshot, plus refusal to adopt a legacy `(python3.9)` snapshot; (2) a real `/usr/bin/python3` HTTP child on a fresh temporary directory/ephemeral port, with its initial snapshot forced to the exact observed transient command. The latter proves readiness capture, settled identity and matching stop. Authority for these added assertions is review65 R3; no existing assertion was removed or weakened. An intermediate passing fixture execution emitted a Popen resource warning; retaining/reaping only the fixture child corrected its cleanup. Final execution is clean.

## Verified legacy recovery

The user's live environment was inspected before mutation. App PID **64692** and reference PID **64694** had saved start time `Tue Oct 6 13:34:42 2026`. App command matched exactly; reference saved command was `(python3.9)`, while its settled command was the CLT framework Python below with the exact owned server arguments. Both start times matched; `lsof` identified exactly those PIDs as listeners on 5310/5320. Container name/ID/label/fixed loopback binding, local marker and live PostgreSQL owner token were verified; image was `postgres:17-alpine`. Symlink/local-appsettings guards also passed.

Only after all those checks, the legacy local process record was repaired with the independently verified complete identities/arguments. That repair signalled no process. Normal create then stopped those verified owned processes. No generic abbreviated-record adoption was added to the command; no user container/database/process was touched. Ownership tokens and real participant data are omitted here.

## Actual interpreter and command cycle

All commands ran in `/Users/christopher/.codex/worktrees/participants-functionality/BingoWebpage`:

```sh
/usr/bin/python3 scripts/test-ui-review.py
/usr/bin/python3 scripts/ui-review.py create live
/usr/bin/python3 scripts/ui-review.py refresh final-review
/usr/bin/python3 scripts/ui-review.py stop
```

The focused safety command passed **12 tests / 0 failures**, exit 0. It exercises actual foreign bound/reusable listeners, PID identity mismatch, new argument/start-time mismatch and the real CLT child. Foreign refusal paths did not signal any process. The three review commands each exited **0**, in that order, with no retry. Create printed **69** scenario links; refresh printed **70**. Both create and refresh builds reported **0 warnings / 0 errors**. Each completed its original readiness checks; independent GETs to `http://127.0.0.1:5310/Account/Login` and `http://127.0.0.1:5320/` returned **200** after each command. Container/local/database ownership was independently verified while running.

Requested interpreter: `/usr/bin/python3`; version **3.9.6**, build May 22 2026, Clang 21.0.0. `sys.executable` resolved to `/Library/Developer/CommandLineTools/usr/bin/python3`. Saved and independently observed reference executable after readiness:

```text
/Library/Developer/CommandLineTools/Library/Frameworks/Python3.framework/Versions/3.9/Resources/Python.app/Contents/MacOS/Python
```

Exact reference arguments were `-m http.server 5320 --bind 127.0.0.1 --directory /Users/christopher/.codex/worktrees/participants-functionality/BingoWebpage/docs/references/admin-ui`. App command remained `dotnet /Users/christopher/.codex/worktrees/participants-functionality/BingoWebpage/src/Bingo.Web/bin/Release/net10.0/Bingo.Web.dll`.

| Phase | App PID / start time from ps | Reference PID / start time from ps | Saved identity equals running identity |
| --- | --- | --- | --- |
| create live | 66126 / 6 Oct 13:55:32 2026 | 66128 / 6 Oct 13:55:32 2026 | yes, both |
| refresh final-review | 66399 / 6 Oct 13:57:49 2026 | 66401 / 6 Oct 13:57:50 2026 | yes, both |

Matching stop printed “Owned UI review app, reference server and PostgreSQL stopped.” Independent post-stop checks verified all four listed PIDs absent, `processes.json` absent, the same owned container stopped, and fixed ports **5310, 5320, 54339 free**. The owned gitignored evidence/storage/markers remain for the next authorized create.

## Retained gates and stop boundary

`git diff --check` and protected-source comparisons passed: `DevelopmentScenarioSeeder.cs`, frozen `tokens.css`/`components.css`, .NET implementation and JS behavior unchanged. Staged scope is only the command, its safety tests, this evidence and active status. Prior [R1/R2 evidence](remediation-1.md) remains applicable: extended PostgreSQL/HTTP **2/0/0**, whole BrowserTests **150/0/0**, clean nonincremental Release **0 warnings/errors**; prior full JS **51/0** Chromium/WebKit is retained execution evidence. These broad suites were not rerun for R3; R3's real commands performed the builds reported above. No whole .NET or CI execution here.

R3 is implemented and executed, awaiting Claude's independent named recheck and the user's renewed walkthrough. U1 acceptance remains retained. Claude/user must run the unfiltered whole .NET gate on the final reported R3 SHA, requiring **0 failed / 0 skipped**:

```sh
dotnet test Bingo.slnx --configuration Release --no-restore --results-directory /private/tmp/bingo-ur-final-suite-trx --logger "trx"
```

Stop after the single R3 commit. No U2, lane T, merge, deployment or push.

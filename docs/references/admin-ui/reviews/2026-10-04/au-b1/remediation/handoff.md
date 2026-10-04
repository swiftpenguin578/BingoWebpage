# B1 remediation lane handoff

Branch: `codex/au-b1-small-fixes`

Starting checkpoint: `b38749dd47740b2267c6f2ed33c28b28dcf72e75`

Final checkpoint at handoff: `f49bf651bf4e1ef8185d6f9a20c38a513e612957`

The worktree was clean at handoff. No reset, rebase, amend, push or merge was performed during this remediation round.

## Commits and evidence

| Item | Commit | Evidence | Result | Residual risk |
| --- | --- | --- | --- | --- |
| AU24 F1 | `c32acea` | `AU24-F1.md` | 6 focused PostgreSQL/HTTP tests passed; Release web build passed with 0 warnings/errors; diff check passed | Public transfer now requires the confirmed destination-ID method; old overload call sites were updated. |
| AU16 D4 | `95d9fdb` | `AU16-D4.md` | 5 AuditHistory PostgreSQL/HTTP tests passed; hidden selection/redaction, missing/filter-excluded entry, spring/autumn DST, non-Admin refusal and HTTP date validation covered; Release build passed with 0 warnings/errors; diff check passed | Existing Admin policy remains the page authorization boundary; EventId remains the query parameter while the UI presents names. |
| AU15 setup | `8c59d41` | `AU15-setup.md` | Exact PostgreSQL/HTTP test passed 1/1, including the previously unreachable non-Live refusal/reset assertions; Release build passed with 0 warnings/errors; diff check passed | Fixture now shares the seeder's half-hour precision; production synchronization code is unchanged. |
| AU22 D5 | `d4b2631` | `AU22-D5.md` and updated `AU22.md` | AccountOverview Release suite passed 3/3; authenticated reset-link HTTP test passed 1/1; Release build passed with 0 warnings/errors; diff check passed | D5 accepts one redirect in the encrypted HttpOnly TempData cookie. |
| Browser rerun | `f49bf65` | `browser-account-support.md` | Bundled Chromium launched but stopped at `account-support.browser.js:62`, expected hidden version `8`, observed `7` | Four AU24 browser assertions remain unverified: submitted version `9`, recipient in confirmation, typed values after cancel and no JS errors. No browser PASS is claimed. |

## Independent recheck boundary

The lane is complete for the authorized remediation scope and is ready for Claude's independent B1 commit-by-commit recheck. Merge-back into `codex/participants-functionality` remains pending that recheck and planner routing.

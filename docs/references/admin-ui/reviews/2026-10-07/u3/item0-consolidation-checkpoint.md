# U3 item0 consolidation — historical approval checkpoint

Resolved by the user’s explicit steps1–5 authorization and Q6 phone rule.
Current outcome: [item0 report](item0-report.md). The text below records the
pre-approval checkpoint and is not the current reservation rule.

HEAD: `b7909064864f93b4b0b0e715afa449274fd23a6f` (Identity U3-Q5
one-line-summary correction). See identity-one-line-summary.md for its separately
committed evidence: destination headers 12/0 Chromium and 12/0 WebKit.

## Verified current untracked conformance files

`PLAYWRIGHT_BROWSER=<chromium|webkit> <node>
tests/Bingo.BrowserTests/admin-design-page-conformance.browser.js` passed in
both engines, exit 0 each: **25 geometry cases per engine** (Identity 5, Events
5, Dashboard 15 including one-line and deliberately wrapped stats). Bundled
Node path, source hashes and compact measurements are in
item0-validation-checkpoint.json. Full local measurements are in
artifacts/page-conformance-{chromium,webkit}/positions.json. Temporary logs:
/tmp/u3-conformance-{chromium,webkit}.log.

The current gate checks 390/494/860/1280/1440 widths, one empty loading-summary
line, header/first-body flow and text-row geometry, native frames/stylesheet
readiness on slow navigation, absence of load fades with interaction animations
retained, document viewport bounds at 1000px height, control/node/focus/scroll
retention, and Danish language/full-load/shell navigation. Events sorts an
unchanged result population so strict scroll retention is physically testable.
Its style probe follows the existing 640px breakpoint (900px table minimum on
narrow screens, 990px otherwise). An initial search probe made the list shorter
than the viewport and naturally clamped scroll; that test setup was replaced,
not product behavior or the strict assertion.

Node syntax checks on all four files and git diff --check passed. Existing
tracked duplicate tests remain untouched. No full JS runner, Release build or
.NET suite was run. This is partial implementer evidence, not item0 completion
or independent review. No new visual acceptance is claimed.

Loaded-summary measurements (normal fixture): Identity and Dashboard use two
lines at 390px and one at all four larger widths; Events uses about 2.2 line
heights at 390px because its inline action wraps, and one at larger widths.
All loading summaries reserve one line; no page exception is introduced.

## Exact proposed consolidation for approval

All paths below are relative to the assigned checkout
`/Users/christopher/.codex/worktrees/participants-functionality/BingoWebpage`.
This proposal preserves distinct cases and is narrower than the rejected
command, which would have removed the entire family and document-scroll files.

1. Delete `tests/Bingo.BrowserTests/admin-design-body-geometry.browser.js`
   (one-line wrapper) and `scripts/check-u2-body.cjs`. Their complete geometry
   and Dashboard text-wrap/bar assertions were moved into the passing generic
   `scripts/check-admin-page-conformance.cjs` and run from one new JS entry.
   There are no other executable callers of check-u2-body.cjs.
2. Trim `tests/Bingo.BrowserTests/admin-design-page-family.browser.js`:
   remove the per-family path/check definitions and three-page full/shell/culture
   loop (current lines 13–23 and 25–32). Keep its real Danish culture setup and
   archived Identity read-only/pre-wrap case (current lines 24, 33–35), plus
   fixture/cleanup. Generic checkDanish owns registered-page repetitions;
   preserve its document-retention marker in the generic helper before trimming.
3. Trim `tests/Bingo.BrowserTests/admin-design-load-animation.browser.js`:
   move the whole-page fast/slow family loop and animation-preservation probe
   (current lines 56–79) into the generic path; keep Events result/empty-result
   cases (current lines 80–89), fixture and cleanup. Generic slow/frame and
   interaction assertions already pass; add and pass the fast path before
   removing its old coverage. Preserve exact 149/150/399/400 timing coverage.
4. Trim `tests/Bingo.BrowserTests/admin-design-document-scroll.browser.js`:
   retain Events filtered-Live/filter-reset checks, including screen-reader-row
   assertions. Move the three-family full-load 342px-height document-bound and
   attempted-scroll checks into the generic width loop before deleting those
   repetitions. Do not delete the whole file; the current generic only proves
   1000px-height bounds, so this move is still pending.
5. Finish only the four new generic files: put text-row/source/module metadata
   with each page's single registration; add the pending fast-path/short-viewport
   and source design-check coverage described above. Keep frozen primitives,
   real Razor fixtures and existing assertions. No production code changes.

No change to scripts/run-browser-tests.cjs is proposed: it automatically
finds admin-design-page-conformance.browser.js and runs both engines. Any
execution-time issue will be reported rather than silently increasing limits.
Production pageKind and _AdminDesignTemplates.cshtml remain unchanged, so lane
T's shell registration is unaffected. Specialized header-reference parity,
stylesheet failures/cancellation, Events update behavior and remaining native
shared-shell tests stay.

## Approval boundary

Automatic approval review rejected the attempted removal/harness-rewrite
command: “Although the conformance consolidation is authorized, this command
also deletes several existing tracked test/scripts and rewrites the active
harness; that broad cleanup was not explicitly approved and should not proceed
without narrower authorization.” Nothing in that command executed. No retry or
workaround was attempted. Subsequent work only corrected and executed the
existing untracked probes; tracked duplicates were preserved.

Next permitted action: obtain explicit narrow approval for the above
consolidation, then complete and verify it in both engines and commit item0.
Schedule and Signup setup have not started. The review environment remains
stopped. No push, merge or deployment.

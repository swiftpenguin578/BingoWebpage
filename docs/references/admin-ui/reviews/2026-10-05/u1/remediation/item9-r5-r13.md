# Item9 — R5 / R13 precise tests and evidence correction

Before → after → authority:

- identity-readback.transport.js originally checked exactly one read after one
  Check again; item7 replaced it with `reads >= 4`. This weakened the invariant
  without authority. Now exact counts are 0 before checking, 1 after unavailable
  read, 2 after departure Check again, 3 after matching/moved, 4 after unchanged
  version, 5 after differing/moved, and exactly 5 at the end. All POST counts,
  newline/tuple/version/malformed/refused assertions remain. R5 / brief50 item9
  restores one read per explicit action; A14 changes outcomes, never this invariant.
- identity-timezone-confirmation.browser.js: broad row-count/partial-text checks
  are supplemented by exact full dialog text, including U-F title/confirm zone,
  public-exposure consequence, five controlled rows and other changed fields.
  fixtures/identity.cjs adopts the U-F consequence text for that exact proof.
  The preview-free stale path again checks the exact validation-summary sentence.
  Existing pending/refusal/version/draft/one-POST protections remain. R13/U-F/A10.
- Former `Further unsaved edit` after a read while uncertain was removed in item7.
  I-3 / brief48 locks controls while uncertain, so that old editable-while-unknown
  scenario is obsolete. The locked-field and immutable tuple proofs remain; a new
  exact later-draft check covers editing after unchanged-version readback unlocks
  the form, then Keep editing on departure. It proves the later draft remains and
  neither POST nor read count changes. R13 / I-3 / A14 / brief48.
- Slice3CreationIdentityPersistenceIntegrationTests originally asserted Signups
  opened/closed; item7 changed these to Signups open/close substrings, which also
  matched the old words. They now require exact scheduled rowheader markup/text,
  retaining every persistence/version/audit assertion. R13 / A10 / I-6 /
  A-Identity-2, with U-F's scheduled-row composition.
- item7-identity.md and item7-test-change-map.md now acknowledge these omissions
  and weakened checks. The earlier preservation claim was incorrect.

Focused timezone/readback JS PASS; affected Slice3 controlled PostgreSQL/HTTP
**1 passed / 0 failed / 0 skipped**. Logs:
`/private/tmp/bingo-u1-remediation-item9-{timezone,readback,http}.log`.

Initial checkpoint (before brief51): full runner **40 passed / 1 failed / 41 total**, solely the unchanged
shell busy timing assertion at admin-design-shell.browser.js:160 (expected false
at 599ms, actual true). All Identity checks passed. Log:
`/private/tmp/bingo-u1-remediation-item9-fulljs.log`; ignored machine results under
`artifacts/js-tests/`. The fake clock is installed but not paused, allowing wall
elapsed time between evaluate and fastForward calls. Installed Playwright API
states that pauseAt freezes timers until explicit advancement. No assertion or
production timing has been changed to bypass this failure; deterministic fixture
correction has been raised to dispatcher before editing. Item9 was held uncommitted at that checkpoint; no gate pass was claimed.


## Brief51 resolution and completed item9 gate

Planner authority supplied by the user:
`/Users/christopher/Documents/BingoWebpage/review-notes/51-u1-rem-item9-clock.md`.
Before: `page.clock.install()` leaves simulated time advancing between calls.
After: install at a fixed instant, let the three existing navigation/disposal
steps finish, then pause at a fixed later instant before recording tick baseline.
All subsequent time advances are explicit. Production timing is unchanged; the
599 false / 600 true, 249 false / 250 true, response prerequisite, reduced-motion
0, exact disposed-timer and toast lifetime assertions are unchanged. No sleeps,
tolerances, retries or timeout increases were added.

Exactly five planned stability runs: **PASS, PASS, PASS, PASS, PASS**, each exit0.
Logs `/private/tmp/bingo-u1-remediation-item9-clock-{1,2,3,4,5}.log`.
Then full JS runner: **41 passed / 0 failed / 41 total**, exit0, log
`/private/tmp/bingo-u1-remediation-item9-fulljs-paused.log`.
Affected .NET UI contracts/localization: **34 passed / 0 failed / 0 skipped**,
log `/private/tmp/bingo-u1-remediation-final-ui.log`.
Complete Release candidate build with `--no-incremental`: **0 warnings / 0 errors**,
log `/private/tmp/bingo-u1-remediation-release.log`. Frozen CSS comparisons and
`git diff --check` clean. Whole .NET suite remains user/Claude's final-SHA gate.

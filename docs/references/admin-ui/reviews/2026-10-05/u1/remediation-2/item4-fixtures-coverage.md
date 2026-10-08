# Round 2 item4 — valid Identity fixtures and named coverage

Authority: brief53 item4 / 52b N6 (fixture correctness), 52a T1 (Check again
helper option), 52a T2 (valid non-UTC switcher date). No production changes in
this item; no server assertions removed or relaxed.

Before → after → authority:
- `fixtures/identity.cjs`: preview/error/stale/conflict responses previously
  combined HasReviewedValues=false with nonempty reviewed values. They now
  require reviewed current values; initial/success states without reviewed values
  leave reviewed fields empty. Description/current description both remain empty
  when no description edit occurred. The existing named conflict scenario retains
  its explicit remote Name/Description values. Baseline description fields are
  parameters instead of a consumer's HTML string replacement. Authority N6.
- `identity-binding.browser.js`: saved response uses originalDescription parameter
  instead of replacing a hidden field in generated HTML. Assertions unchanged.
- `identity-timezone-confirmation.browser.js`: exact complete dialog expectation
  previously included Also saved: Name, Description; now Also saved: Name, because
  the admin changed Name and Timezone, not Description. The dialog already names
  Timezone separately. Added exact true reviewed flag, empty reviewed/current
  Description and exact Also saved text. All five moments, Cancel/focus, pending,
  reason absence, one-POST, stale basis and newline assertions retained. Authority N6.
- `identity-readback.transport.js`: wraps and forwards the real AdminFetch request
  for actual Identity Check again, recording the Current call. Adds exact one-call
  `readback: true`, four localized labels and retained draft Name assertions. This
  fails if Identity omits readback even though the helper's isolated tests pass.
  Existing exact read counts, immutable tuple, lost-save, newline, no automatic
  read/retry and three version-outcome protections unchanged. Authority T1.
- `AdminDesignShellIntegrationTests.cs`: added real PostgreSQL proof with fixed,
  microsecond-aligned 2027-03-27/28 22:30 UTC instants and Europe/Copenhagen.
  Persisted UTC days remain exactly 27/28; projected text is exactly
  `starts 27 Mar|starts 29 Mar` across the spring DST transition. Existing invalid
  stored-timezone UTC fallback and HTTP assertions are unchanged. Authority T2.

Executed evidence:
- Identity binding, timezone and readback focused JS: all PASS. Logs
  `/private/tmp/bingo-u1-r2-item4-{binding,timezone,readback}.log`.
- Controlled PostgreSQL tests SwitcherConvertsCopenhagenDatesAcrossSpringDst and
  SwitcherInvalidStoredTimezoneUsesUtcAndIdentityStillRenders: **2 passed / 0
  failed / 0 skipped**, exit0. Log `/private/tmp/bingo-u1-r2-item4-pg.log`; TRX
  `/private/tmp/bingo-u1-r2-item4-trx/`.
- Full JS runner: **41 files passed / 0 failed**, exit0. Log
  `/private/tmp/bingo-u1-r2-fulljs.log`.
- Non-incremental Release build: **0 warnings / 0 errors**, exit0. Log
  `/private/tmp/bingo-u1-r2-release.log`.

- Affected .NET UI checks (EventCreationUiTests, ManagedCompetitionUiTests,
  AdminDesignLocalizationTests): **34 passed / 0 failed / 0 skipped**, exit0.
  Log `/private/tmp/bingo-u1-r2-ui.log`; TRX `/private/tmp/bingo-u1-r2-ui-trx/`.
- `git diff --check` and both frozen CSS byte comparisons: clean.

The final whole .NET suite remains user/Claude execution at the reported final
SHA, zero failed/zero skipped required. Recheck52's failed whole-suite result
and separate 15/15 class rerun are recorded separately in CURRENT_STATUS, not
called a full pass. CI, Claude named recheck and user visual acceptance are pending.

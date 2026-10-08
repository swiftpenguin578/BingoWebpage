# Item6 — R4 / U-F / R16 Identity

Editing after Use theirs resets UseCurrent to KeepMine (None when returning to
the original). Save is disabled with No changes to save when canonical fields
match the current baseline. Check again's quiet note is Up to date; the explanatory
readback notice still avoids save attribution. Timezone review uses the reference
title/confirm zone IDs, before/after column zone IDs, scheduled rows and one unset
summary. Successful saves refresh the breadcrumb/switcher context without reloading.
The shared context refresh retains literal server href attributes (the retained
sidebar integration exposed an unnecessary relative-to-absolute conversion).

Assertion/fixture changes under brief50 item6, R4/U-F/R16:
- identity-binding.browser.js adds initial/no-op disabled/title checks, UseCurrent
  then edit -> KeepMine and exact submitted value/one POST, original -> None, and
  updated breadcrumb/switcher in the same document. Existing assertions retained.
- fixtures/identity.cjs uses U-F title/action/quiet/no-change strings; adds shell
  event context needed to assert R16. Existing timezone five-row fixture retained.
- AdminDesignIdentityIntegrationTests scheduled-time assertion 5 rows -> exactly
  4 scheduled rows + exactly 1 unset line `Not scheduled yet: Team draft.`. All five
  moment labels, offset values and permanent-link assertions retained. U-F expressly
  moves unset moments into that line. Added Now/After zone labels.
- Added real HTTP/PostgreSQL merge: concurrent name -> conflict -> edited KeepMine
  request, exact saved name, untouched description/timezone, version +2 and 2 audits.

Executed binding, existing timezone and existing readback JS: PASS; real PG/HTTP
**2 passed / 0 failed / 0 skipped**; Danish coverage **1 / 0 / 0**. Logs:
`/private/tmp/bingo-u1-remediation-item6-{binding,timezone,readback,http,localization}.log`.
During implementation the wide-dialog class initially used the wrong shared-layer
property; the unchanged pending proof caught it and passed after correction. The
relative href correction was also verified by the unchanged timezone navigation
selectors. No safety assertions were removed in this item. R5/R13's older changes
are explicitly addressed in item9, not waived by these passes.
`git diff --check` clean. Independent named recheck/manual acceptance pending.

# Item5 — R7 / U-C switcher

Switcher dates use the existing DateTimePresentation UTC fallback for unsupported
stored zones (also UTC for blank values). SignupOpen uses SignupClosesAt; Live and
final review use EventEndsAt; other phases use EventStartsAt. Localized text is
closes / starts / ends plus reference day/month, or not announced when unset.
Existing Starts/Ends resources are reused with a lowercase initial; no duplicate
case-insensitive resource names remain.

Changed assertion: unscheduled When `Not scheduled yet` → exact `not announced`,
authorized by U-C / brief50 item5. Added exact per-phase text/date assertions,
retaining eligibility/order/hidden-event/route assertions. Added a real PostgreSQL
invalid-zone round trip, exact UTC date and actual Identity HTTP200 proof.

Initial compile found the explicit format-provider requirement; initial resource
variants collided with existing uppercase keys. Both corrected before this gate.
Final focused PostgreSQL/HTTP: **2 passed / 0 failed / 0 skipped**; Danish coverage:
**1 passed / 0 failed / 0 skipped**. No build warnings in these final logs:
`/private/tmp/bingo-u1-remediation-item5-final.log` and
`/private/tmp/bingo-u1-remediation-item5-localization.log`.
`git diff --check` clean. Named recheck/final gates pending.

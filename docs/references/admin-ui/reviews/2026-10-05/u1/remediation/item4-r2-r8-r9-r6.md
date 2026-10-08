# Item4 — R2 / R8 / R9 / R6 shell navigation

Fragment-only history changes within the current document leave the shell alone.
Navigation consults dirty layer input and closes clean layers before swapping,
including Back/Forward. Widening past 860 px closes mobile navigation and restores
content interaction. Sidebar DOM/collapsed state is retained; current links and
event context update from the response.

Added browser assertions, without removing existing protections: skip link with
clean and dirty form produces only its native history entry, no fetch/prompt;
sidebar identity and collapse survive navigation; Back closes a clean drawer;
Back with edited drawer supports Keep editing then Discard; mobile widening clears
scrim/inert state. Authority: brief50 item4 and R2/R8/R9/R6. Fixture gains a real
fragment target and request counter; no prior assertion expectation changed.

Executed focused admin-design-shell.browser.js: PASS. Log:
`/private/tmp/bingo-u1-remediation-item4.log`. `git diff --check` clean.
Named independent recheck and final gates remain pending.

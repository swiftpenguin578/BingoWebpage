# Item3 — R3 / U-A / R10 fetch outcomes and notices

Non-2xx responses are unknown with their status before HTML/auth interpretation.
The existing successful HTML/auth redirect checks remain. Identity Check again
passes an explicit readback context: session loss now says signed out/could not
check/sign in and Check again, never not saved. Mutation refusal keeps the existing
not-saved notice. Identity supplies localized labels separately from its frozen
field-keyed tuple; notices never need to expose internal names in the bound page.

Test additions (no existing assertion removed): admin-design-fetch.browser.js now
asserts HTML404/429/500 unknown+exact status, save/readback notice differences and
all four English/Danish labels, draft retention and safe text rendering. Its result
projection adds status to enable these exact assertions. Authority brief50 item3,
R3/R10/U-A. New server-rendered notice keys have Danish entries.

Executed: fetch browser proof PASS; existing Identity readback browser proof PASS;
AdminDesignLocalizationTests **1 passed / 0 failed / 0 skipped**. Logs:
`/private/tmp/bingo-u1-remediation-item3-{js,readback,localization}.log`.
No server auth or persistence behavior changed; prior HTTP302 evidence is reused.
`git diff --check` clean. Named recheck and final gates remain pending.

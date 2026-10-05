# Item7 — R11 / U-B / R17 / U-E shell details

Bell shows the existing inbox count in a visible number badge and includes the
count in its accessible name. Zero has no visible badge but remains announced.
Teams / Draft, plain-text event breadcrumb, existing DK Legacy mark image, toggled
collapse/expand labels, reference failed-load icon/title/body, menu exit animation
and side/start/end placement are bound. Identity provides its reference-shaped
loading template; the shell caches page-provided templates and otherwise uses a
generic skeleton. Event-specific links remain absent with no selected event.
Frozen reference/component CSS is unchanged; small logo/badge adapters are local.

Assertions: existing shell menu-hidden assertion now waits for exit completion
before asserting hidden (U-E explicitly adds animation); focus expectation stays.
New browser assertions cover closing class before hiding, collapsed placement,
collapse labels, provided/generic skeleton and exact failed-load content/icon.
New controlled PG/HTTP assertions cover unread count/zero accessibility, logo,
plain breadcrumb, Teams / Draft, Identity loading template and no-event nav.
The new logo check was corrected from a literal unversioned path to a bounded
regex accepting only that image's normal/fingerprinted URL, as ASP.NET emits it.
No existing server assertion was removed or broadened.

Executed shell browser proof PASS; controlled PG/HTTP **2 / 0 / 0**; Danish coverage
**1 / 0 / 0**. Logs `/private/tmp/bingo-u1-remediation-item7-{js,http,localization}.log`.
`git diff --check` clean. Independent recheck and user visual acceptance pending.

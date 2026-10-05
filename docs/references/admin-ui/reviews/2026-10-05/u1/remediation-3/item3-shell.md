# Round 3 item 3 — shell parity

Authority: brief55 item3 / comparison54 V1–V16, V18; user Q1–Q3 and existing A7. Shared icon geometry and bold menu headers landed in item1; this commit wires the remaining shell bindings and reference markup.

The header reads the signed-in account's public website username from PostgreSQL and displays its localized Administrator/Super admin role and reference chevron. The DK Legacy image stays in the logo slot. The account menu uses the shared name/@handle · role header. Tooltips appear only when the sidebar is collapsed; collapse title/arrow track its state. Reference div/flex metadata restores the status dot and eight-pixel spacer. Switcher rows have radio semantics and an accent check after the stage; opening focuses/scrolls the checked row without changing A7 ordering or routes. Reference breadcrumb classes replace adapter caps; theme/language buttons retain accessible button semantics with equivalent reference focus ring and zero UA padding. Document title matches the reference suffix/separator.

Terminal events and past-end Live/Final review use the persisted end date and localized “ended” wording. Other phase date rules, timezone conversion/fallback, eligibility and hidden-event routing remain. The service uses its injected clock to decide whether the end has passed.

## Executed checks

- Controlled PostgreSQL/HTTP shell class: **19 passed / 0 failed / 0 skipped**, `/private/tmp/bingo-u1-r3-item3-http.log` and `-http-trx/`.
- After adding exact terminal-state assertions and final metadata div: affected switcher/actual-layout checks **2/0/0**, `/private/tmp/bingo-u1-r3-item3-final-http.log` and `-final-http-trx/`.
- Danish coverage **2/0/0**, `/private/tmp/bingo-u1-r3-item3-localization.log`.
- Focused complete shell script **PASS in Chromium and WebKit**, `/private/tmp/bingo-u1-r3-item3-final-shell-{chromium,webkit}.log`. Added exact collapsed tooltip/title/arrow and current-row scrolling/focus proofs; existing pending/history/dirty/disposal/timing protections remain.
- Diff check and frozen CSS byte comparisons passed. Build stages of the focused .NET checks produced no warnings/errors.

## Precise test changes

`AdminDesignShellIntegrationTests.cs`: old expected “ends” for already-ended Live/Final review → exact “ended” plus the same persisted date (brief55 V9); added exact selected terminal-state end dates. A deterministic clock fixes the fixture's “now” at its established 2026-10-05 instant, and its localizer double strips the scoped AdminDesign prefix for formatted resource keys. No date tolerances or eligibility assertions changed. Old `.design-event-crumb` assertions → exact `.crumb-mid`, including the retained assertion that it is not a link (V11–V14). Added exact real-account header, document title, breadcrumb classes, radio/check assertions (V1/V2/V8/V18).

`admin-design-shell.browser.js`: fixture and selector `.design-event-crumb` → `.crumb-mid` (V13); every prior early-context/Back assertion retained. New collapsed/selected-row proofs are supplemental fixture checks. Actual served rendered style/screenshot parity remains item7, not claimed by these tests. No independent review or visual acceptance is claimed. Whole .NET remains user/Claude at final SHA.

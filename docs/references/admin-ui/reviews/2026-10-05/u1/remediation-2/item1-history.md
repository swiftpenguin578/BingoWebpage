# Round2 item1 — 52a N1 history

Native fragment entries are adopted with a distinct shell index using replaceState,
without adding history entries. Later switcher/Back navigation uses the normal
indexed dirty guard and restores the current URL before asking. A foreign stateless
entry that reaches fallback reloads the current document after the guard, never
location.assign of its already-current fragment URL.

Added exact browser scenarios: skip → switcher B → Back clean (one A load, one
pop); dirty Keep editing (draft/URL retained, no load, two restoration pops), then
Discard (one A load, five total requested/restoration pops). History length remains
unchanged and no modal remains. A forced pre-existing stateless fragment proves
one fallback document reload and unchanged history length. Existing assertions stay.
The request counter now counts document/fetch only; diagnostic output proved the
initial extra count was a font request on reload, not a second page navigation.
No assertion tolerance, sleep or timeout change. Authority brief53 item1 / 52a N1.

Executed admin-design-shell.browser.js PASS; log `/private/tmp/bingo-u1-r2-item1.log`.
Diff check clean. No independent review/visual acceptance claimed.

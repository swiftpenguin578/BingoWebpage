# U2 brief72 item3 — Create close and literal duplicate names

Parent:f1caca5. Match Events.dc.html:704–711: Create opened from directory
consumes its pushed history entry on close; direct entry replaces its URL.
Shared shell backUrl performs tracked travel without fetch/swap. A disposable
page URL-state handler handles only same-directory Create Back/Forward after the
existing dirty/pending guard, without reinitializing or replacing the directory.
Layer closure awaits its callback so history travel finishes before close resolves.
Forward reopens with the directory origin preserved; another close consumes that
entry too. Success still replaces Overview and retains one-time Back highlight.

Duplicate notice interpolates only the template's0/1 tokens in one regex callback
pass. Inserted names are never interpreted as a second placeholder or replacement
metacharacter. The owned fixture name includes literal{1},$&,$$,$',$` and the exact
rendered sentence is asserted in both engines. No duplicate-name product behavior
or case/culture matching changed.

Changed:admin-design-shell.js,admin-event-create.js,scripts/check-u2-create.cjs,
this evidence. No reference/components/tokens CSS changes.

Checks7 October2026:
- Create rendered/behavior runner:10passed/0failed, Chromium/WebKit;1440/390,
  light/dark composition0differences. Includes pushed close state index decrement,
  unchanged history length,zero HTTP requests/JS fetches/page-changed events,
  same directory node,no skeleton,Forward reopen/close,and one Back leaves Events.
  Direct entry retains length/index and also zero fetches/swaps/same region.
  Existing dirty/discard,uncertain Check again/Leave anyway,session notice,culture,
  real write/readback/Overview/one-time highlight assertions remain executed.
- U2EventCreateHttpTests:3passed/0failed/0skipped,1s. TRX:
  tests/Bingo.BrowserTests/TestResults/u2-rem2-item3-http.trx.
  An earlier U2CreateModalHttpTests filter matched no tests, not a pass; corrected
  to the actual class above and overwrote the empty TRX.
- Unchanged shared-shell runner PASS Chromium/WebKit after async callback change.
- Fixture Release rebuild0warnings/0errors,0.79s. Node --check all three changed
  scripts exit0; git diff --check exit0.

Authoring checkpoints:two test-script syntax mistakes (literal replacement-string
metacharacters and a missing callback block) were repaired with explicit literals
and syntax checks. No production/test expected behavior was loosened to pass.
Awaiting Claude independent review,then user visual acceptance; not self-reviewed.
Whole final-SHA suite/final gates and item8 concurrency still required.

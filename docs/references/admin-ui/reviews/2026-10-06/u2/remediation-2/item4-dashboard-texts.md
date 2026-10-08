# U2 brief72 item4 — exact Dashboard text and sort presentation

Parent:5555c25. Recap footer/chart provisional note now use d MMM yyyy in event
timezone. Percent uses Floor(value*100+0.5):Math.round ties toward positive
infinity (including negative.5);percent text is an invariant whole integer.
Continuous chart-grid CSS positions remain fractional, as the reference doesn't
use pct there. Remove only the EHB partial-coverage suffix; preserve covered/total
and frozen-import disclosure. Counts use the existing culture-aware N0 helper in
summary/notes,card meters/titles,teams,board tiles,EHB coverage and chart aria text.
History Players cell has only its numeric value, no teams tooltip or extra tab stop.
Sort alternates rows.swap-a/swap-b; shared reduced-motion CSS remains unchanged.

Danish gate explicitly extracts Dashboard tuple Label keys when D[stat.Label] is
present, asserts extraction nonempty and checks each nonempty resource entry.
Rendered Danish gate now asserts all four headline labels, using real language
switch rather than a fake culture mechanism.

Changed:Index.cshtml/Index.cshtml.cs,admin-dashboard.js,
AdminDesignLocalizationTests.cs,U2DashboardPresentationTests.cs,
scripts/check-u2-dashboard.cjs,this evidence.

Executed7 October2026:
- Focused BrowserTests filter U2DashboardPresentationTests|AdminDesignLocalizationTests|
  U2DashboardHttpTests:13passed/0failed/0skipped,7s.
  TRX:tests/Bingo.BrowserTests/TestResults/u2-rem2-item4.trx.
  Includes32.5→33,31.5→32,33.333→33,-32.5→-32,date-year,1,234 counts,
  1,000of1,234 EHB coverage without suffix,and auth/rendered HTTP tests.
- Dashboard rendered/behavior runner:18passed/0failed,Chromium/WebKit.
  Light/dark and loaded reference1280/860/390:0differences. Added exact recap
  footer comparison,absence of history teams hint,swap-a/b,and four Danish labels.
  Existing keyboard chart/Escape,sort focus/scroll/URL,retry/disposal/A16 repeated
  switches/Back/Forward all executed. Item5 stricter widths/title/narrow-content
  rule remains next; existing old bounded-header assertions are not its proof.
- Fixture Release rebuild0warnings/0errors,0.86s. git diff --check exit0.
  No frozen tokens/components/reference modifications.

Authoring checkpoints:not passes:initial raw-string delimiter compile error;
new fixture assertion erroneously expected1Apr although Point ends2Apr,corrected
to the deterministic actual event-zone date;dynamic-label regex escaping error
caught by new nonempty-extraction assertion and replaced with simple literal-key
matcher. Final focused13/0/0 and rendered18/0 reruns pass; no production assertion
or existing expected behavior relaxed.

Awaiting independent Claude review,then user visual acceptance. No self-review,
whole suite pass or final gate claimed. Item8 concurrent runs remain required.

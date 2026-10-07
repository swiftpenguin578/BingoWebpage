# U3 item0 accepted-page correction: Identity one-line summary

Authority: user U3-Q5(a), 7 October 2026, review-notes/08-decisions.md
“Summary reservation for every page”. Every new-layout loading header reserves
one empty summary line at every width. A loaded summary may grow; no page
exceptions are authorized. This explicitly resolves the earlier approval block.

The new conformance assertion measured Identity at 390 px: the original loading
summary was 39.125 px (two lines) against a 19.575 px line height. Its loaded and
reference summaries were also two lines. The shared shell now leaves its
summary-template slot as an empty text line instead of inserting event text.
Destination titles and normal loaded summaries are unchanged. DELIVERY_PLAN.md
register records the intentional reference loading-summary-height difference.

Focused regression: `PLAYWRIGHT_BROWSER=<engine> <node>
tests/Bingo.BrowserTests/admin-design-destination-header.browser.js`:
Chromium **12 passed**, WebKit **12 passed**, exit 0 each. Tests exercise EN/DA,
real culture switch, destination titles, slow success/failure, exact 150/400 ms,
and Identity's one-line reservation at 390 px. The test switches language at
1280 px before entering the 390 px case (an initial attempt timed out on the
correctly hidden mobile language control; setup corrected, not product behavior).
`git diff --check`: exit 0. No C#/Razor changes; no build or .NET suite run.

Shared design checks 1–6: frozen styles/markup untouched; no new CSS or inline
styles; existing header/template reused; action busy timing and fetch/guard
behavior unchanged. Only shared loading-header text assembly changes.

This is an implementer checkpoint awaiting independent review. Item0's generic
conformance gate remains in progress; its files are not part of this correction
commit. No new visual acceptance, push, merge or deployment.

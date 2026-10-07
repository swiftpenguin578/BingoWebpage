# Brief76 item4 — Q-SK2(a) text-line skeleton reservations

Parent d66422a0e3caa1465a08bd73b11c343dce06a429. The user/planner ruling
Q-SK2 resolves item4-body-question.md; its earlier diagnostic rectangles remain
identified as pre-ruling evidence, not the completed proof.

Before: the first headline card aligns, but its reference-shaped bars reserve
less height than real one-line statistics, putting participation30.21875px too
high at494. After: the card's headline text slots reserve actual label/value/note
typography. Bars retain reference widths/heights and12px visible gaps; unused
line-row height is deducted from the following margin so enlarging the row does
not enlarge the bar gap. The outer line reservation equals the loaded one-line
sum, including its6/7px text margins. No stored data or fixed section positioning.

Row heights come only from family-scoped page CSS; no frozen file changes:

| Skeleton text | Existing bar | Real one-line reservation / source |
| --- | --- | --- |
| Dashboard statistic label/value/note | 10/20/8px;46/38/62%;12px gaps | small×body line-height; stat×1.1; max(17px,meta×body line-height); components.css:463–475 |
| Dashboard panel title / fact rows | Existing title/fact bars | control×body line-height; .panel-title/.fact |
| Dashboard next-card text | Existing bars in nbsp-backed rows | Already uses .next-label/.next-name/.next-meta typography; unchanged |
| Events name | Existing width-percentage bar | body×body line-height; inherited .name-btn typography |
| Events dates-main / participants / attention | Existing bars | control-sm×body line-height; .cell-main/.meter-label/.attn |
| Events dates secondary | Existing8px bar | small×body line-height; .sub. Visible main→secondary bar gap remains8px |
| Events tab counts / summary | Existing count bars / empty summary | Existing same-size real/count slots and Q-SK1 one-line summary; unchanged |
| Identity section title | Existing title bars | control×body line-height; .form-sec-title |

Non-text chart200px, recap64px, meter, badge, input34px and textarea120px
placeholders keep their reference geometry. Header and failure rules are unchanged.
DELIVERY_PLAN adds a Q-SK2 register row explicitly differing from reference
skeleton row heights. Identity reference proof applies only that exception to its
in-memory loading reference DOM; original bar dimensions/gaps and every loaded/
failure/authorization/form assertion remain. No frozen reference file edits.

## Executed item4 proof

- New runner-gated admin-design-body-geometry.browser.js →25/0 Chromium and
  25/0 WebKit, actual authenticated owned PostgreSQL/Kestrel.
- Five widths390/494/860/1280/1440; Dashboard normal/one-line/forced-wrapped
  labels+notes (15), Events normal(5), Identity normal(5).
- First body card x/y/width match after the already-approved header growth.
  Dashboard participation x/width match; y difference equals independently
  measured label/note wrapping. One-line content has zero movement within the
  unchanged0.1px precision bound. Actual grid subpixel accumulation is at most
  0.03125px; no tolerance was enlarged. At494 normal, the30px shortfall is gone.
- One-line baseline is an invisible same-width copy with only label/note line
  heights/nowrap constrained; values must retain their actual height. This
  independently separates real wrapping from skeleton geometry. Normal390
  measured wrap growth70.78125px; normal other widths0. Forced wrapping is
  positive at every width. Values wrapping is rejected.
- Exact reference bar width/height and12px gaps asserted. Every skeleton text
  row's height equals its declared real typography within0.1px; the bar fits.
  Events toolbar/table tops and Identity first-card starts also match.
- No sleeps, retries or increased timeouts; paused fake150ms/remaining400ms
  hold. Native stylesheet readiness is separately gated by item2/full runner.
- Durable paired rectangles: item4-qsk2-positions-chromium.json and
  item4-qsk2-positions-webkit.json,25 records each; text-row representatives
  retained per family/width, repeated identical row classes omitted only from
  the durable manifest (execution asserts every row).

## Authoring/environment record

Initial focused body measurements passed25 per engine before the exact row-height
guard was added. The first final JS attempt exposed an unloaded Events stylesheet:
both body row checks failed (10px vs20.3px), and both Create comparisons detected
the stylesheet's absent class rules. The solution clean/build had regenerated
static assets without refreshing the separately built fixture's copied manifest.
No assertion or production behavior was weakened.

Refreshed AdminDesignParityFixture Release build0 warnings/errors7.50s;
a fresh owned native probe returned Events CSS200,3159 bytes, both new text-row
and table rules present, and table min-width990px. The invalid runner attempt was
stopped (driver exit143; no completed batch count/pass), after verifying its exact
checkout/process group. Only test-owned processes were stopped. The full JS gate
was restarted under this changed condition; completed results belong to
final-gates.md/final-js-results.json, not the stopped attempt.

The restarted attempt then exposed a distinct old header-proof race in WebKit:
after150ms it called getComputedStyle(null) while item2 legitimately waited for
CSS before inserting the skeleton (normal390/861 had passed; next width had not
yet inserted its skeleton). scripts/measure-u2-header.cjs now uses its existing
bounded native/microtask checkpoint for element existence and the exact same
grid/flex predicates, rather than paused-clock RAF polling. All geometry/
reference assertions,0.1px tolerance,150/400ms timing and timeout settings stay
unchanged. This failed attempt was also stopped (exit143); no completed full
batch count/pass is claimed for either stopped attempt. Focused header proof
and the final complete runner execute the corrected checkpoint.

Focused HTTP34/0/0; exact final TRX and timing in final-gates.md. Whole .NET
suite NOT RUN under Q-S1/Q-S2. No independent review or manual acceptance claim;
UI_PAGE_MATRIX retains awaiting Claude review, then user visual acceptance.

## Shared design checks1–6

Exact commands/exits and inspected matches: final-design-checks.json.

1. Both application tokens/components match the frozen copies by cmp; git baseline
   diff for all four CSS copies and three reference HTML files is empty.
2. No new literal colour/font-family/shadow/literal-radius declaration (rg exit1,
   no matches). Page CSS scope parser checks all selectors in both engines.
   The previous reference→prefix bindings remain; Q-SK2 adds these eight
   reference-height-exception selectors, all with the same zero-specificity prefix:

   - :where([data-page-family="dashboard"]) .dash-sk-stat-lines
   - :where([data-page-family="dashboard"]) .dash-sk-stat-label
   - :where([data-page-family="dashboard"]) .dash-sk-stat-value>.sk
   - :where([data-page-family="dashboard"]) .dash-sk-panel-title
   - :where([data-page-family="events"]) .events-sk-name
   - :where([data-page-family="events"]) .events-sk-main
   - :where([data-page-family="events"]) .events-sk-end
   - :where([data-page-family="identity"]) .identity-sk-title

   Existing scoped stat-value/note/fact/fact-first/events-start selectors gain the
   approved line geometry. Numeric1.1,17px,6/7px and bar10/20px are exact frozen
   typography/bar bindings, not new theme values. Spacing12/8px uses shared tokens.
3.24 inline source matches inspected: only retained dynamic/chart/meter,
   reference bar widths/heights, display-contents/flex and header min-width.
   Identity's inherited14/18px input spacing and8px radius remain exactly its
   accepted reference. All new production text-row heights live in page CSS.
4.66 shared markup matches: existing card/panel/table/form/skeleton/icon owners
   composed, not replaced. New wrappers only reserve text lines; no new primitive.
5.23 timing/busy source matches include the shell's existing named150/400
   settings and readiness scheduler, page search debounce, shared busy transport
   and copy-label reset. Q-SK2 adds no timing, spinner or mutation behavior.
6.42 shared lifecycle/transport matches: shell still owns disposal, guards,
   authorization-aware reads, aborts, updates and stylesheet readiness. The full
   JS gate executes these boundaries; item4 changes no production JS.

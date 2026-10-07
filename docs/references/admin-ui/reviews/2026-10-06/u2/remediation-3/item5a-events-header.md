# Brief74 item5a — loading-only Events reservations

Parent2827204. Implementer evidence; independent review/manual acceptance pending.

Both the summary's min-height and the loading title group's forced width are now
restricted to the page skeleton. Loaded flex lines have their natural reference
height/width. Existing failure empty-height rule remains. Register updated.
No frozen CSS/reference change.

Executed:
- Native fixture Release0 warnings/errors,0.91s; git diff --check0.
- admin-design-events-header.browser.js Chromium5/0, WebKit5/0:
  loaded header/title/summary/attention/Create/toolbar rectangles equal the
  reference at390/640/860/861/1280 (0.1px layout precision). Real authorized UR
  live-profile attention; equal synthetic1000-count stress data at390 forces
  wrapping without changing seed populations. Empty loading summary still
  reserves one/two lines. Exact150ms show/399+1ms minimum, no sleeps/retries.
- admin-design-events.browser.js Chromium13/0, WebKit13/0:
  loaded light/dark390/1440 composition, query loading flow at six widths,
  filters/sort/DA/failure/retry, repeated switches/Back/Forward,
  partial-attention/hidden rendering. Old full-width/min-height reference
  normalization removed from loaded comparisons; empty failure normalization
  retained. Title/summary widths can naturally change after loading; content
  follows the real header-height delta, rather than a fixed anchor.
- Durable rectangles: item5a-positions-chromium.json and
  item5a-positions-webkit.json. Runtime comparison details/screenshots:
  artifacts/u2-events/; query position JSON records each before/after flow.

Authoring failures recorded: first loaded test exposed the second loading-only
width leak, then synthetic comma formatting was corrected to reference data;
CSS min-height comparisons use the same0.1px precision as other geometry (43.15
computed vs43.140625 rendered). Callback-stalled attempts were stopped, not
passed. Instrumented diagnostic localized the second-width stall to a paused
clock inherited by a new page's requestAnimationFrame settling. Resuming between
pages fixed it; elapsed-time assertions remain paused/exact.

Whole .NET NOT RUN, Q-S1. Full final focused gates follow item6.
Next:item5b stylesheet readiness before swap.

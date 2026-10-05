# Round3 item1 — one reference component source

Reference Identity.dc.html lines42–328 were ported by element: one keyed icon
partial holds the 15 navigation glyphs, shared field/banner/lock/check/copy/close
icons, theme/menu/collapse geometry, and the approved existing bell. The collapse
arrow remains a separate path. The server sidebar/topbar use that partial.

Typed Razor partials own labels, notes, errors, lock, banners, save bar, toast,
menu header and confirmation structure. Shared templates render those same
partials; shell confirmations/toasts and Identity conflict/timezone builders clone
them through AdminUI.template. Item4 binds the remaining field/save/banner
partials into the complete Identity state machine; this checkpoint does not claim
those states already match. Toast lifetime/placement remain item5.

Geist Mono is self-hosted, system-mono override removed; page-local sk-block CSS
ported unchanged. Font/OFL source: official vercel/geist-font commit
10dc7658f13c38a474cde201bb09a4617267545b, fonts/GeistMono/webfonts/GeistMono[wght].woff2.
SHA256 font afaacc4c5fbba89d2ebf7a02dc4070208540874592a5504d57175782fe893101;
Upstream OFL c683bfbcc7e087f5d37a54ef628f10387c451a83ddc459b151403a164ac46c90.
The vendored license trims one upstream trailing space (no text change), SHA256
942560b236adfa83745b2c64e5fc09ebaf91cb331751b1157eb92187e5d6e930.

Test changes, before → after → authority:
- Supplemental shell/fetch/Identity fixtures previously omitted shared templates;
  now include admin-design-templates.html exported by the actual Razor/HTTP page
  with a controlled PostgreSQL fixture. New integration test renders and requires
  an exact match with the committed snapshot. This fixture is not served-page
  navigation or visual proof (item7). Authority brief55 item1 / A10.
- Timezone full-dialog exact string gains precisely three Razor whitespace newline
  characters (after heading, before Also saved, after actions). No word, row or
  behavioral assertion changed; no normalization or loose matching substituted.
  Authority A10/template markup port. Initial exact-string failure is retained in
  /private/tmp/bingo-u1-r3-item1-timezone.log; corrected pass has -corrected suffix.
- Added real-rendering assertions for exactly9 templates, check/error icons and
  m-body/no m-sub. All existing server assertions unchanged.

Executed: controlled PG/HTTP components+layout **2/0/0**; localization **2/0/0**;
focused Chromium shell, timezone, binding, readback and fetch JS all PASS. Logs
/private/tmp/bingo-u1-r3-item1-{pg,localization,shell,timezone-corrected,binding,readback,fetch}.log.
An initial build caught CA1875 in the new test; Regex.Count corrected it, subsequent
builds in both .NET check commands passed. Diffcheck/frozen CSS byte checks clean.
Full WebKit/Chromium batch runner, real served parity/baseline failure, computed
styles and screenshot table remain required in item7; no visual acceptance claimed.

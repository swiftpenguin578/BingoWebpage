# Catalogue CI1–3 correction — 7 October 2026

Explicit authority: 08-decisions “Catalogue integration boundary after the T2 merge”. One correction commit follows clean9c1756e3; prior remediation/merge commits remain intact.

- CI1: generalized the existing save-only check. Every registration declares its actual POST transport count (Accounts3; Catalogue/Signup/Schedule/Identity1 each; Audit/Dashboard/Events0). Direct saves must await the busy-wrapped transport. Concise Catalogue helper returns that same busy promise and every caller must await it. Signup's conditional GET/POST helper has every write caller checked inside awaited ui.busy; the explicit Current/null-body read is not a save. No family exception; incorrect counts, missing busy boundary and local save timers fail. Existing Accounts negative tests remain intact. Production JS/search debounce unchanged.
- CI2: both Catalogue header count placeholders use the exact shared span/tab-count/data-pending-count/sk markup and Accounts/Events sizing. Unused ct-sk-count rules removed. The first scoped run correctly caught missing wrapper dimensions; transferred the complete existing2ch inline-flex pattern and reran.
- CI3: only loading summary row-gap becomes0. Existing one-line wide/two-line phone reservations now hold exactly within subpixel tolerance. Loaded summary and fixed words unchanged.

Executed: Accounts save-timing source tests PASS; generic all-eight-registration positive/negative save tests PASS; rebuilt Release parity fixture0 warnings/errors; Catalogue conformance **5/5 per engine** at390/494/860/1280/1440, including source scope/tokens, no-fade, atomic frames, summary/first-body geometry, mandatory blocks, navigation, retained update/focus/scroll, document bounds and actual Danish ResourceNotFound audit. [Exact geometry](ci-conformance-results.json). `git diff --check` PASS.

The only T-owned files changed are `_AdminCatalogueLoadStates.cshtml` and `admin-design-catalogue.css`, exactly as authorized. Full combined gate follows on this committed implementation; whole.NET remains planner-owned.

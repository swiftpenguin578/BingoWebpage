# U1 item4 — shared interactions and page navigation

Authority: brief43 item4, reference interaction contracts, approved A12/A16. Frozen behavior.js was used as a contract, not shipped. UI_SYSTEM now records the concrete module/configuration API.

- `admin-design-shell.js`: keyboard focus, menu arrows/Home/End/Escape/Tab and opener restoration; 860px mobile sidebar trap; layered drawers/modals with top-layer Escape, input/confirmation outside-click refusal, dirty Escape confirmation and computed-CSS exit timing.
- Toast host: maximum3,4500ms,7000ms with action, hover pause, no Undo. Busy600ms save/250ms quick, reduced-motion0; waits only after actual completion.
- Shared draft registration and FormData baseline helper; beforeunload only while dirty/pending. Sidebar/breadcrumb/switcher navigation asks Keep editing/Discard. History restores the current entry before asking, then travels to the target only after consent; discarded client drafts are not stored in history.
- Normal server HTML fetch, target skeleton, page region/title/navigation/breadcrumb/token/module/style swap, Back/Forward, focused heading, failed-load retry and full-load fallback for old/unexpected targets. Default-on `AdminUi:InPageNavigation=false` uses full loads after the same guard. Page ES modules expose init(root,ui)/dispose and own cleanup. Filters/search replace URL state; opening records pushes; schemas validate query values and omit defaults.

## Executed proof

- Full JS runner: **39 files passed / 0 failed / 39 total**, exit0. [All results](item4-js-results.json). Existing controlled PostgreSQL-produced stale fixtures reused.
- New browser regression executes menus; drawer focus wrap; nested confirmation and opener restoration; input/confirmation outside-click refusal and no-input close; mobile trap; all three link kinds; cancelled/discarded Back and Forward; no resurrected drafts; repeated module switches with one live listener and one live interval; query validation/history policy; loading skeleton; failed fetch retry; old/unexpected target fallback; off switch; save/quick/reduced timing and response barrier; toast cap/hover/lifetimes.
- Release Web build: **0 warnings / 0 errors**, `/private/tmp/bingo-u1-item4-build.log`. Syntax and diff checks clean. No existing test assertions changed.

No production page is bound yet. Item5 fetch/session helper next. Independent review, visual acceptance and final whole .NET suite (user/Claude executor) remain pending; CI unrun. Optional setup-node pin note remains carried.

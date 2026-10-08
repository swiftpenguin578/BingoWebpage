# Brief76 item1 — fixed untranslated page family

Exact clean start1e0f85ac5470cc76148dbb0f8419dc215d5e2315 on the assigned
participants-functionality checkout/branch. No workers/reviewer/new branch/
worktree/push/merge/deploy/self-review.

Before: loaded .page and .page-head derive family from translated Title,
so Danish Identity is identitet and none of its identity-scoped CSS matches.
After: the three AdminDesign pages declare literal PageFamily dashboard/events/
identity; layout requires that key (throws if missing), never falls back to Title.
Localized title/description remain unchanged. Shell pageKind/skeleton keys and
CSS prefixes already use these untranslated keys and are preserved.

Changed: Admin/Index.cshtml, Admin/Events/Index.cshtml, Identity.cshtml,
_AdminDesignLayout.cshtml; AdminDesignLocalizationTests.cs and new
admin-design-page-family.browser.js.

Executed checks:

- Fixture Release build0 warnings/0 errors,33.23s.
- Focused AdminDesignLocalizationTests3 passed/0 failed/0 skipped,138ms,
  including reflection discovery of every AdminDesign page and a mandatory literal
  lowercase family assignment. Missing/translated assignment and title-derived
  layout regressions fail. Exact TRX:
  /Users/christopher/.codex/worktrees/participants-functionality/BingoWebpage/tests/Bingo.BrowserTests/TestResults/u2-rem4-item1-localization.trx.
- Real owned PostgreSQL/Kestrel browser fixture:10/0 Chromium,10/0 WebKit.
  Each page in Danish on full load, shell navigation and actual EN→DA culture
  switch; loaded root/header keys and computed page-CSS sentinel apply.
  Identity132px description and hidden empty banner; Dashboard grid; Events990px
  table minimum. Extra Danish readonly Identity retains pre-wrap. Language swap
  keeps the document. No page errors. Archived fixture key corrected before run.
- git diff --check0. Final full JS/Release/rendered/design gates follow item4.

No product/reference question. Accepted U1 behavior unchanged; scoped implementer
checks are not independent review/visual acceptance. Whole .NET suite NOT RUN
under Q-S1. Next item2: await family CSS before showing skeleton and prove stale
link/cancellation behavior.

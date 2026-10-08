# Brief76 item2 — family CSS before skeleton, no stale-link hang

Parent371c52e. Shared shell remembers CSS links with skeleton content, removes
those links from body clones, and starts active family-scoped CSS loading when
navigation starts. Before150ms no skeleton; after150ms readiness can defer it,
keeping the current page. The400ms minimum starts when the skeleton really shows.
Already-shown holds transfer their existing timestamp. Unknown family generic
skeleton needs no page CSS. Language retains its no-skeleton CSS wait.

Existing null-sheet head links are replaced from the valid source and reloaded,
not awaited for an event they may never emit; load error uses full-load fallback.
Cancellation uses a captured per-navigation controller. Staged CSS ownership
transfers to a successor; a shown skeleton retains its loaded CSS while that
successor waits, until content is replaced. Same loaded href nodes remain.
Failure UI also waits for its known family CSS. No new timeout/retry/media toggle.

Changed: shared shell, DELIVERY_PLAN existing readiness row, new
admin-design-skeleton-styles.browser.js.

Executed checks:

- New native frame/MutationObserver proof16/0 Chromium and16/0 WebKit:
  Dashboard↔Events CSS at50/149/151/650ms; cached150/400; unknown generic;
  cancellation while CSS pending, ready-stage same-family reuse, and visible
  skeleton retained while successor CSS pending; stale null-sheet reload success
  and reload-error full-load fallback. Actual CSS, saved native RAF plus paused
  fake clock; no sleeps. No skeleton/loaded frame without ready family CSS.
- Existing loaded-page native CSS proof20/0 each engine (six directions,
  uncached/cached/same-href/error/language), shared loading12+5 exact cases each,
  actual EN/DA destination headers12/0 each including loading/failure.
- Focused AdminShellUiTests2/0/0,680ms; exact TRX:
  /Users/christopher/.codex/worktrees/participants-functionality/BingoWebpage/tests/Bingo.BrowserTests/TestResults/u2-rem4-item2-shell.trx.
- Fixture Release build0 warnings/0 errors,254.64s. Read-only process check showed
  compiler CPU activity, not a hang; no user process altered. Initial sandbox ps
  permission failure corrected with authorized native read.
- Node syntax and git diff --check0. Final combined gates follow item4.

Authoring before/after: a native aborted CSS request was initially used to create
the stale null-sheet fixture. Both engines instead leave a non-null empty CSSOM,
so two attempts stopped at that fixture's exact-null setup check (not a production
assertion failure/pass). Changed approach: native unsupported-type link produces
the specified null sheet with no future load/error, then reloads from the valid
remembered source. No sheet property stub/assertion relaxation/timeout increase.
Real load-error fallback remains covered separately. An invalid patch context was
corrected; no source change from that failed patch.

No product/reference question. Scoped checks only, not independent review or
visual acceptance. Whole .NET suite NOT RUN per Q-S1. Next item3: one-line Events
loading summary and growth-only flow proof.

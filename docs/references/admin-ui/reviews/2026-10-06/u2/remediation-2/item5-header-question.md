# U2 brief72 item5 — reference-first header geometry

Parent: a6d208268db444bb4b8cb511fa88c91b94de2d7c. Implementer checks, not independent review or manual acceptance.

## Resolved question and implementation

The preserved diagnostic asked whether fixed positions or the frozen reference
take precedence. Planner ruling “Brief 72 item 5, Q-H1 geometry”, 7 October 2026,
corrects the earlier wording: exact reference flow wins, including bottom-aligned
title, card and following-content movement caused by summary height/card size.
No fixed anchors, cached data or layout exception. DELIVERY_PLAN register updated.

Loading title group now uses the reference's intrinsic flex sizing/min-width:0,
not the loading-only flex:1 override. Current-event metadata has separate phase
and date spans, matching the reference interpolation's flex children. This fixes
the narrow card's 383.015625→389.03125px intrinsic width.

The first diagnostic sampled styles before they were applied, producing the
Chromium861 shrink and WebKit zero/incorrect title-group width. The executable
proof now awaits stylesheet application (computed card flex:0 0 auto), forces
layout before font readiness, and advances two fake frames. No arbitrary sleeps,
timing tolerances, changed frozen CSS or production width overrides were added.
An initial test-authoring :scope lookup error was corrected; failed diagnostics
are not claimed as passes.

## Executed proof

- Fixture Release build: 0 warnings, 0 errors, 3.57s.
- Bundled Node scripts/measure-u2-header.cjs: **16 passed / 0 failed**:
  Chromium/WebKit ×390/861/1280/1440 ×normal/narrow Cup fixtures, real owned
  PostgreSQL/Razor and production shell.
- Loading at fake150ms; response at182ms; exact remaining368ms hold to550ms.
  Strict0.1px reference comparisons for head/group/title/summary/card in both
  loading and loaded states. Reference loading DOM changes only to the approved
  empty one-line summary/placeholder; frozen source stays unchanged.
- Flow equations additionally prove header height=max(group,card) on desktop,
  stacked group+24+card on mobile, title bottom alignment, summary spacing,
  card position and following content at header bottom+20. No unrelated movement.
- [Durable compact positions](item5-header-positions.json) contains all16 records,
  loading/loaded and independent reference rectangles. At861 narrow both engines:
  loading card420×76.34375; loaded389.03125×76.34375; title105.1875→82;
  following content178.34375→213.84375, explained by summary19.5625→78.25.
- Dashboard regression runner after replacing obsolete fixed-shift assertions
  with reference-flow equations: **18 passed / 0 failed**, both engines;
  light/dark and1280/860/390 loaded comparisons all0 differences, keyboard/chart,
  Danish labels, sort swaps, retry/disposal and repeated A16/Back/Forward pass.
- git diff --check: exit0. Frozen tokens/components app/reference cmp: exit0;
  no baseline diff in any of the four frozen files.

## Acceptance and remaining gates

Both pages remain awaiting Claude review, then user visual acceptance.
Items6–8 and final gates are not claimed here. No push/merge/deploy.

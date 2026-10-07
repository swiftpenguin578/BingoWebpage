# T2 summary: Catalogue (lane T)

- **Branch:** `claude/lane-t2-catalogue` (from `d03fc7a`).
- **Worktree:** `BingoWebpage-lane-t2`.
- **Head at the batch gate:** `f59144d`.

This file was written by the planner from the implementer's batch-gate report, because the sub-agent's file tool refused to create report files. It is implementer evidence. It is not an independent review or user acceptance.

**Details:**
- [items1-2-catalogue.md](items1-2-catalogue.md): checks and every changed test, with before, after and the named decision.
- [parity-checklist.md](parity-checklist.md).

## Commits

| SHA | What |
| --- | --- |
| `56a98be` | Item 0: empty Catalogue load-state partial |
| `cc6b0c0` | Item 0: shell registration of the catalogue page family. One `pageKind` line and one `<partial>` line (U3-Q4 (a)). |
| `ee4e0c2` | Item 1, server: <ul><li>JSON outcomes for script requests</li><li>D7/D9 affected list in the response, exact-set rule kept</li><li>S10 impact read and server refusal of unconfirmed deactivation (T2-2)</li><li>C-CAT-2 checks (T2-Q4)</li><li>item 13 wording</li></ul> |
| `f2d9c6e` | Item 2: page binding, UR scenarios, register rows, parity checklist |
| `f140481` | T2-1 (a): Super Admin hidden-event link to `/Admin/Events/Manage/{id}?hidden=true`. Also records the S10 wording, T2-2 and T2-3. |
| `f59144d` | User early-look fixes: <ul><li>"Add activity" usable while loading (the loading header had a disabled button)</li><li>thumbnails `contain` with a 3 px inset (family CSS, register row)</li></ul> |

## Batch gate (as reported by the implementer)

- **Release build:** 0 warnings, 0 errors. The parity fixture was rebuilt explicitly. A solution build does not refresh it.
- **Full JS runner:** 89 passed, 0 failed, out of 89. The `admin-design-*` files run in both Chromium and WebKit.
- **Affected integration classes** (42f §2.8: Slice6 incl. Au21/Au23/Cat1/StatsPass1/T2, AdminStaleChange, CataloguePriceHttp, UiReviewScenario): 263 passed, 0 failed, 0 skipped.
- **Browser-project C# tests:** 28 passed, 0 failed, 0 skipped.
- **WebKit repeats of the Catalogue browser test:** 5 of 5 passed. A single earlier timeout was not reproduced.
- **`git diff --check`:** clean.
- **Whole .NET suite:** not run (the planner runs it).

## Open items

- **Main-lane shell need:** the drawer's dirty baseline is recorded only when the drawer opens; it needs `layer.markClean()`. A page workaround keeps decision B. What remains is the browser "leave site?" prompt on reload when a drawer is open but unedited.
- **For U10:**
  - the unused `SharedResource.da.resx` key "Rolls per {0}";
  - the old `catalogue-*` transitional CSS.
- **For U7:** promote the thumbnail `contain` rule into the shared `.thumb`.

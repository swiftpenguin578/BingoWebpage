# T2 Catalogue — items 0–2 evidence (lane T, first working version, awaiting the early look)

Worktree `/Users/christopher/Documents/BingoWebpage-lane-t2`, branch `claude/lane-t2-catalogue` from `d03fc7a`.
Brief: review-notes `82-claude-brief-t2-catalogue.md`. Parity checklist: [parity-checklist.md](parity-checklist.md).
Status: implemented and checked by the implementer; **not** independently reviewed, **not** user-accepted.

## Item 0 — family registration
- Placeholder load-state partial (lane-owned), then the shell commit `shell: register catalogue page family (lane T)`:
  one `pageKind` line (`/admin/catalogue`, `/admin/catalogue/index` → `catalogue`) in `admin-design-shell.js` and one
  `<partial name="_AdminCatalogueLoadStates" />` line in `_AdminDesignTemplates.cshtml`. One `pageKind` line covers
  both paths (the brief said two lines; T1 needed two because it registered two pages).

## Item 1 — server
- JSON outcomes for every handler when the request is a script request (`Accept: application/json` +
  `X-Requested-With`); form posts unchanged (redirect + status message).
- D7/D9: affected activities in the response (`confirm-shared`, `activities[]`); TempData key removed; exact current set,
  rechecked before commit (unchanged).
- S10: `OnGetDeactivationImpact(recordType, recordId, expectedVersion)`; toggles refuse a deactivation without
  `confirmed=true` and return the impact. Query: board requirement snapshots (boss eligibility and drops; an activity
  covers its drops) on current tiles of Draft boards, plus Published boards with a correction in progress whose objective
  is **not** in the active approval (verified in `Board.cshtml.cs` `CreateApprovalSnapshotAsync`: correction approval
  checks `source-inactive`/`drop-inactive` only for requirements outside the prior approval), excluding Finalized /
  Archived / Cancelled / Discarded events. Hidden events: names only for the Super Admin, otherwise `hiddenCount`.
- C-CAT-2: fixed category list on add and edit; 200-character item name on drop edit.
- Item 13 wording; retired-field refusal reworded (keeps “were not saved”, test Cat01).
- Delete / DeletionImpact: a script request from an ordinary Admin gets `refused` JSON (form posts still `Forbid()`).

## Item 2 — page
- Directory filtered in place (all activities load), URL `?q=&cat=&status=`, drawer `?activity={id}`, editor `&drop={id}`,
  `?new=1`; bound from the query string only. Old `bossId`/`dropId`/`addBoss` are not mapped (no inbound link uses them).
- Drawer, drop editor, Add drop, Value and item mapping, Wise Old Man metric, S10 and D7/D9 confirmations, delete with
  dependency check, readback (“Check current values”) for unknown outcomes, C-CMP-2 through the shared fetch helper.
- Provider and suggest outcomes now carry a structured reason/result so the page shows the reference texts.
- UR scenarios: a catalogue drop (Zulrah · Tanzanite fang) used by the draft board of “Upcoming setup 01”, by the current
  event’s correction copy and by “Hidden final review”’s correction copy (with its correction audit), plus scenario links.

## Test changes (A10 / named decisions)
| Test | Before | After | Decision |
| --- | --- | --- | --- |
| `Au21CatalogueRemediationIntegrationTests.AssertAffected` | read TempData key | read the response list (model), same ids and names | brief 82 D7/D9 transport |
| `Au23…RollGroupChange…` (3 asserts) | “operator-managed” | equals “Roll group can only be changed by the Super Admin.” | item 13 |
| `Slice6…CatalogueAdministrationEnforcesDeleteAuthority…` | old drop forms / `delete-drop-` ids | drawer drop templates; delete control only for the Super Admin | A10 |
| `AdminStaleChange…DropForm` | old form inputs | the drop template’s data (ids, versions, name, rate, image) | A10 |
| `StatsPass1HttpForms…` | “API mapping and price” | `data-catalogue-price` panel, `?activity=` | A10 |
| `StatsPass1ReviewF3…` | `data-catalogue-status-*` attributes | server toast text and error tone | A10 |
| `catalogue-admin.test.js`, `catalogue-deletion.browser.js` | retired script | removed; replaced by `admin-design-catalogue.browser.js` | A10 |

## Checks (executed)
- Release build, whole solution: 0 warnings, 0 errors.
- `dotnet test tests/Bingo.IntegrationTests -c Release --no-build --filter …` (Slice6 partial class incl. T2, Au21, Au23,
  Cat1, StatsPass1, CatalogueSimplification; AdminStaleChange; CataloguePriceHttp; UiReviewScenario): full run 250/13/0,
  the 13 were markup-scraping tests and the UR audit check, fixed and rerun 13/0/0; final focused rerun 103/0/0.
- `dotnet test tests/Bingo.BrowserTests … AdminDesignLocalizationTests|AdminShellUiTests|Catalogue|UiReview`: 28/0/0, then 5/0/0.
- `admin-design-catalogue.browser.js`: Chromium pass; WebKit 4 passes in 5 runs (one timeout, not reproduced).
- `admin-design-ur.browser.js` (U2 UR check): Chromium 2/0, WebKit 2/0.
- Design checks 1–6: tokens/components byte-identical; page CSS family-scoped, no colour/font/shadow literals (radii only
  via tokens inside reference rules); inline styles only `--art` image URLs and skeleton widths (dynamic); one
  `setTimeout` (250 ms URL write, reference) and `aria-busy` on dialogs while a request runs; busy via `AdminUI.busy`.
- 42f §2.10: no matches in `src/Bingo.Web/Pages/Admin/Catalogue` / page script; remaining hits are the frozen reference
  (D20), `CatalogueSnapshotService` import refusal (D8) and an unused `SharedResource.da.resx` key “Rolls per {0}”
  (shared file, not edited).
- `git diff --check`: clean.
- Not run: full JS runner and whole .NET suite (batch gate, item 3 / planner).

## Early-look stop decisions applied (planner relay; 08 “T2 Catalogue early-look stop”)
- S10 wording approved as proposed (no change).
- T2-1 (a): the Super Admin’s hidden event links to `/Admin/Events/Manage/{id}?hidden=true`. Integration test
  `T2DeactivationImpact…` asserts the URL; the browser test opens the activity confirmation as ReviewOwner and checks the
  link (and that no hidden count is shown).
- T2-2 (a), T2-3: unchanged.
- WebKit: `admin-design-catalogue.browser.js` run 5 more times after the change: 5/5 passed (16–18 s each); Chromium 1/1.
  The earlier single timeout did not recur (0 of 9 later WebKit runs). Note: the fixture project must be rebuilt
  explicitly (`dotnet build tests/AdminDesignParityFixture -c Release`); a run against a stale fixture build fails the
  new Super Admin step.

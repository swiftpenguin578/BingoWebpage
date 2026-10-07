# T1 summary — Accounts + Audit (lane T)

- **Branch:** `claude/lane-t1-accounts-audit` from `5938b08`, in the worktree `BingoWebpage-lane-t`. Head at the batch gate: `c946016`.
- **Status:** implementer evidence, written by the planner from the implementer's batch-gate report (the sub-agent's file tool refused to create a report file). It is not an independent review or user acceptance.
- **Details:** [item1-accounts.md](item1-accounts.md), [items2-4-audit.md](items2-4-audit.md).

## Commits

- **`e5741e6`:** empty Accounts and Audit load-state partials, so the shell commit builds on its own.
- **`8da7ec7`:** registers the two page families in the shell: two `pageKind` lines and two `<partial>` lines. The user approved this, U3-Q4 (a): lane T keeps the commit, and the planner adapts it at merge.
- **`195f380`:** item 1, Accounts:
  - the directory and the account drawer;
  - confirmations and the reset link;
  - the Transfer dialog.
- **`308d562`:** early-look fixes:
  - the avatar shows at once;
  - role pills (T1-4 (b));
  - an Events-style summary (T1-5).
- **`2216f50`:** the summary shows only the two counts (T1-5 addition); two lines reserved while loading at phone width (U3-Q6).
- **`2d291e9`:** item 2, Audit server rules:
  - S11 areas;
  - C-AUD-3 actor search and C-AUD-4 dropped link filters;
  - the event menu;
  - the reference query names.
- **`a48869b`:** bars in place of the numbers while the Accounts summary loads ("Count summaries load like the tab counts").
- **`6e94059`:** item 3, S12 labels and the completeness test.
- **`0dcb173`:** item 4, the Audit page.
- **`153316e`:** query names bound from the query string only. This fixes paging on both pages.
- **`9288c4c`:** human-readable Audit Changes (T1-8 (a)).
- **`12b5b4b`:** the search fields avoid username autofill (T1-9 (a)).
- **`864b70d`:** batch gate: Danish header counts. Resource names clashed by case, warning MSB3568. Adds a guard test against such duplicates.
- **`c946016`:** batch gate: `scripts/check-u2-ur.cjs` uses `?event=` and the drawer template. That script belongs to the main lane; the planner accepts the edit because it follows directly from T1's query names (A10).

## Decisions applied (`08-decisions.md`)

- **"T1 brief decisions":** Q5 (a), Q6, Q7.
- **Reference sweep:** C-ACC-1, C-AUD-3, C-AUD-4, C-CMP-2, S11, S12.
- **D4.** **D5:** the reset link is a direct no-store response.
- **UI integration plan:** A5, A10, A12, A16.
- **Shared rules:** "Shared design system checks", "Process for U3 onwards", "No load fades", Q-SK2.
- **"T1 Accounts early look":**
  - T1-1 to T1-9;
  - the avatar ruling;
  - "Count summaries load like the tab counts";
  - U3-Q6.
- **Brief 79 planner defaults:**
  - query names and redirects;
  - AU24 placement;
  - the Automated pill;
  - the date presets and the four-line clamp;
  - C-CMP-2 sign-out.

## Register rows (`DELIVERY_PLAN.md`, "Bindings not shown in the design references")

- **Accounts:**
  - C-ACC-1;
  - AU24 placement;
  - C-CMP-2;
  - RC11 corrections, with T1-2;
  - retired routes and the Create stub (T1-1);
  - role pills (T1-4);
  - the header summary and its loading counts (T1-5).
- **Audit:**
  - AU16 single entry;
  - AU16 event filter;
  - S11 with C-AUD-3/4 and Q7;
  - page additions beyond the reference: Automated, context, sensitive actions, the C-AUD-5 display, S12 and the new record types;
  - readable Changes (T1-8);
  - the search wording (T1-9).

## Test changes under A10

- **`AccountsUiTests`:** rewritten for the new page.
- **Account HTTP and page-model tests:** moved to `/Admin/Accounts?account=` and `?handler=`. Their server assertions are unchanged.
  - The reset-link test now asserts the stricter D5 rule.
  - `AccountsHttpIntegrationTests` replaces the TempData projection test.
- **Deleted with their scripts:** `account-manage-dialog.test.js` and `account-support.browser.js`. `admin-stale-change.browser.js` now replays the captured pages through the new page.
- **`AuditHistoryIntegrationTests`:** invalid dates now expect "dropped with a notice" (C-AUD-4).
- **Moved to `event=` and the drawer markup:**
  - Slice1, C11 and the UI review scenario tests;
  - `AuditPresentationTests`;
  - `scripts/check-u2-ur.cjs`.
- **New tests:**
  - `AccountsHttpIntegrationTests`;
  - `AuditLabelCompletenessTests`;
  - `AuditUiTests`;
  - `AuditReadableChangesTests`;
  - `admin-design-accounts.browser.js` and `admin-design-audit.browser.js`;
  - the paging tests on both pages;
  - the resource-duplicate guard.

## Batch-gate checks (as reported by the implementer)

- **Release build** (`--no-incremental`): 0 warnings, 0 errors, after `864b70d`. `AdminDesignParityFixture` also builds.
- **Full JS runner** at `864b70d`: 87 passed, 2 failed out of 89.
  - Both failures were `admin-design-ur.browser.js`.
  - After `c946016` that file passes in both engines.
- **Selected browser tests:** 50 passed, 0 failed, 0 skipped.
- **Selected integration tests** (42f §3.8/§4.8 plus every class touched): 222 passed, 0 failed, 0 skipped.
- **Stale-change fixtures:** regenerated, 8 passed, 0 failed, 0 skipped.
- **`git diff --check`:** clean.
- **Whole .NET suite:** not run; the planner runs it after the review.

## Known limitations

- **Create stub:** `/Admin/Accounts/Create` is kept (T1-1), so the 42f leftover check `test ! -e` is intentionally not met.
- **Dead code for U10:**
  - the `admin-account*` and `admin-audit*` rules in `site.transitional.application.css`;
  - `initializeAdminAccountSearch` in `site.js`.

  `_AuditEntry.cshtml` stays, because Review Details still uses it.
- **Conformance check:** the U3 page-conformance check is not adopted yet. It is adopted when U3 merges.
- **Review data:** no paging scenario.
- **Empty-state text:** the Accounts empty state still says "website usernames" (reference text).
- **C-AUD-5 writers:** recording names in new audit entries is a main-lane item.
- **Danish dates:** parsing typed Danish dates ("27 maj 2027") has no test.
- **Tooling:** Node and Playwright were used read-only from Codex's runtime and `node_modules`.

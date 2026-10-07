# T1 item 1 — Accounts binding (first working version, early-look stop)

Lane T, branch `claude/lane-t1-accounts-audit`, worktree `BingoWebpage-lane-t`. Implementer evidence only; not an independent review, not user acceptance.

## What is bound
- `/Admin/Accounts` (new layout, family `accounts`): reference directory (`q`, `role`, `page`; 25 per page; strict positive-integer page; normalized username search, A7), drawer on `?account={id}` (pushes history; Back closes, Forward reopens; direct link opens over its directory, also after an A16 retry), confirmations (grant, revoke, disable with reason 1–500, restore), reset link, Transfer dialog (SuperAdmin; header and owner's own drawer).
- Handlers on the Accounts page: XHR → JSON `completed | stale | refused | invalid | recipient`; plain posts re-render the drawer with the outcome (refusal/stale) or redirect back to it (success). Services unchanged; no migration.
- D5: technical choice **direct no-store response**. The reset secret is only in the generate response (JSON for the drawer; re-rendered page without script). TempData delivery removed. A1: the drawer renders a link only for the account id it requested, and the drawer stays pending (no close/Back/navigation) while the request runs.
- Retired routes: `Manage/{id}` → 302 `/Admin/Accounts?account={id}` (unknown/non-website accounts 404, no handlers); `Transfer` → 302 `/Admin/Accounts`; `Create` stub **kept** (see open question). Old scripts `account-manage-dialog.js`, `account-transfer.js` and their tests deleted.
- RC11: A1, A2 (gated actions, proof-only readback wording), A3 (shared discard for reason/transfer input; stale reason kept), A4 (direct drawer after retry; focus falls back to search when the row is gone), A5, A6 (fixed recipient/version basis), A7, A8 (shell scroll lock). C-ACC-1 text; AU24 typed username on the confirmation step; C-CMP-2 via `AdminFetch` session notice (no password in the notice).
- Register rows added/updated in `DELIVERY_PLAN.md` (C-ACC-1, AU24 placement, Accounts C-CMP-2, RC11 corrections, retired routes).
- Review scenarios: `UiReviewScenarioCatalogue` "Accounts" group (11 links); `UiReviewAccount` gained `Id`.

## Test changes under A10 (before → after)
- `AccountsUiTests`: old Index/Manage/Transfer markup and transitional CSS assertions → new-page binding markers, shared design checks 1–3, Danish completeness, DST last-login and initials.
- `AdminStaleChangeIntegrationTests` (account helpers), `Slice1IdentityIntegrationTests` (reset link, Discord last-login overview), `.AccountTransfer` (HTTP), `.AccountSupport` (disable history projection), `AccountOverviewTests`, `EventQuarantineIntegrationTests` (Manage 404): URLs/page model moved to `/Admin/Accounts?account=` and `?handler=`; server assertions unchanged except the reset link test, which now asserts the stronger D5 contract (200 no-store response, absent from later reads of both accounts) instead of TempData redirect + reread.
- `AccountOverviewTests.ManageResetLinkProjectionIsConsumedOnlyForMatchingAccount` removed (TempData path retired); replaced by `AccountsHttpIntegrationTests.ResetLinkTravelsOnlyInTheNoStoreResponseForItsOwnAccount`.
- `account-manage-dialog.test.js`, `account-support.browser.js` deleted with their scripts; `admin-stale-change.browser.js` rewritten to replay the captured fixtures through the new page.

## Checks (executed)
- Release build `dotnet build Bingo.slnx -c Release`: 0 warnings, 0 errors (plus `AdminDesignParityFixture`).
- BrowserTests `AccountsUiTests|AdminDesignLocalizationTests|AdminShellUiTests`: 18 passed / 0 failed / 0 skipped.
- IntegrationTests `AccountsHttpIntegrationTests|AccountOverviewTests|AdminStaleChangeIntegrationTests|Slice1IdentityIntegrationTests|DraftStartReadinessIntegrationTests|EventQuarantineIntegrationTests|UiReviewScenarioIntegrationTests`: 125 passed / 0 failed / 0 skipped (Testcontainers PostgreSQL).
- JS: `admin-design-accounts.browser.js` Chromium + WebKit PASS (UR live fixture, PostgreSQL); `admin-stale-change.browser.js` PASS; shell/page-family set (`page-family`, `css-scope`, `styles`, `skeleton-styles`, `loading`, `shell`) Chromium + WebKit PASS.
- `git diff --check` clean. Full JS runner and whole .NET suite not run (batch gate / planner).

## Early-look follow-up (08-decisions "T1 Accounts early look")
- Drawer shell shows the avatar at once with the row's initials (empty circle for a direct link); T1-4 (b) Global role always a pill (User `badge-neutral`); T1-5 Events-style summary items with `b.tnum`; register rows added; T1-1 Create stub and T1-2 signed-out notice recorded.
- Checks: Release build 0 errors; BrowserTests `AccountsUiTests|AdminDesignLocalizationTests` 16/0/0; `admin-design-accounts.browser.js` Chromium + WebKit PASS (now asserts the avatar during a held drawer read, role pills and summary items); `git diff --check` clean.

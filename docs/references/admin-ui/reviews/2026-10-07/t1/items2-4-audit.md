# T1 items 2–4 — Audit server rules, S12 labels, Audit binding (early-look stop)

Lane T, branch `claude/lane-t1-accounts-audit`. Implementer evidence only; not an independent review, not user acceptance.

## Item 2 — server rules (`2d291e9`)
- Query names follow the reference: `event` (id), `action`, `actor`, `type`, `from`, `to`, `page`, `entry`. No legacy mapping: no link outside these pages used the old names (tests and `scripts/check-u2-ur.cjs` did; tests updated, the script still passes `eventId`, which is now dropped with the notice).
- S11 / Q5 (a): nine areas as prefix sets plus explicit key lists (`Pages/Admin/Audit/AuditAreas.cs`); `evidence_code.*` and `historical_import.*` placed in Events (not covered by Q5; open question).
- RC06 A2 exact specific action; A3 whole Copenhagen days (next local midnight); strict page parsing kept.
- C-AUD-3: ILIKE with escaped wildcards, leading "@" ignored. C-AUD-4: unknown keys and invalid values (event, action, actor > 100, type, dates, reversed range keeps the start, page) are dropped with a notice; the rest applies.
- Event menu ordered as on Events with state; hidden marked; Discarded omitted (Q7), its entries still listed and filterable by a link.
- Tests: `AuditHistoryIntegrationTests` (C-AUD-4 changes the out-of-range / invalid date expectations: dropped with a notice instead of a validation error and an empty list) plus a new S11/C-AUD-3/C-AUD-4/Q7 test; `Slice1IdentityIntegrationTests`, `C11FinalizedRosterIntegrationTests`, `UiReviewScenarioIntegrationTests` moved to `event=` (A10).

## Item 3 — S12 labels (`6e94059`)
- 104 new labels (English, Danish) and three record types; Danish board term aligned with the existing "Plade".
- `AuditLabelCompletenessTests`: scans `src/**/*.cs` (not obj/Migrations) for audit-area key literals plus `ReviewActionType` keys; seven validation operation names excluded with their reason; fails on a key without a label, without a Danish entry, or not in exactly one area. Shown failing by deleting one label.
- Change labels sentence-cased (README integration 8); three affected Danish keys renamed.

## Item 4 — page binding
- `/Admin/Audit` on the new layout (family `audit`): header with Copenhagen offset, toolbar (actor search on Enter/leave, Event and Action menus, More filters with count), chips with Clear all, dropped-link notice, table (When / Action with Automated pill / Event with Hidden pill / Actor or System / Recorded pills), 25 per page Newer / Page N / Older, empty states, load and failure templates.
- Filters and pages push history; Back/Forward re-read; the shared update helper owns skeleton timing, C-CMP-2 and focus/scroll.
- More filters panel: specific action grouped by area, record type, Today / Last 7 / Last 30 presets in Copenhagen days, From/To text dates (current language or English, or ISO), validation messages, Reset these, Apply; bounded to the viewport and scrollable (A4). The event menu scrolls when long.
- Entry drawer from server-rendered templates: summary, reason and context, changes with four-line clamp and Show full values, Technical details; Newer/Older replace the URL; `?entry=` pushes on top of filters; direct off-page entries open over the first page; unknown ones show "This entry isn’t available".
- Presenter: `Context` (reopening explanations and plain details) and `Sensitive` added; sensitive actions now return no field changes (reference).
- Review scenarios: "Audit" group (7 links).

## Checks (executed)
- `dotnet build Bingo.slnx -c Release`: 0 errors (plus `AdminDesignParityFixture`).
- BrowserTests `Audit*|AdminDesignLocalizationTests|AdminShellUiTests|AccountsUiTests`: 31 passed / 0 failed / 0 skipped.
- IntegrationTests (item 2/4 set) `AuditHistoryIntegrationTests|AuditFiltersAndPaginates|C11FinalizedRosterIntegrationTests|EventQuarantineIntegrationTests|UiReviewScenarioIntegrationTests|C33FinalizationFreshnessTests|AccountSupport`: 130 passed / 0 failed / 0 skipped; UR + Audit history rerun after the scenario links: 8 / 0 / 0.
- JS: `admin-design-audit.browser.js` Chromium + WebKit PASS; `admin-design-accounts.browser.js`, `page-family`, `css-scope`, `styles`, `skeleton-styles`, `loading`, `shell` Chromium + WebKit PASS.
- Leftover checks: `ActorUsername.Contains` 0; seven-prefix list 0; "Recorded administrative action" only as fallback.
- `git diff --check` clean. Full JS runner and whole .NET suite not run (batch gate / planner).

## Early-look round (Audit)
- `153316e` query names bound from the query string only (`[FromQuery]`, as Events): "page" is also a Razor Pages route value, so plain URLs showed the dropped-link notice and `?page=2` showed page 1 on Audit and Accounts. Missed because model-level tests set the bound properties directly and the browser checks never paged or opened a plain URL expecting no notice; new HTTP tests on both pages fail without the fix.
- `9288c4c` T1-8 (a) readable Changes table (`AuditPresenter.IsTechnicalField`, `AuditPresenter.When`); applies to Review Details history too.
- T1-9 (a): search fields avoid username autofill (wording and password-manager ignore markers).
- Checks: Release build 0 errors; BrowserTests `AccountsUiTests|AuditUiTests|AdminDesignLocalizationTests|AuditReadableChangesTests` 37/0/0; IntegrationTests `AccountsHttp|AuditHistory|UiReviewScenario` 15/0/0; earlier this round 177/0/0 (binding fix set) and 211/0/0 (presenter consumers incl. Review); `admin-design-audit` and `admin-design-accounts` Chromium + WebKit PASS; `git diff --check` clean.

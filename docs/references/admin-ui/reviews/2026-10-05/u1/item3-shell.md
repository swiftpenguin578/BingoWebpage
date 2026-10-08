# U1 item3 — opt-in shell foundation

Authority: brief43 item3, plan42 A2–A7/A11/A16. Implementer: Codex `/root/u1_implementer`, Astra/high. Started from clean `374b8832d8a8beaceb7ddd294b6a8dea4a8f929c`.

New layout/partials are selected by `AdminDesignAttribute` metadata in Admin/_ViewStart. No production page has the attribute yet; the HTTP test applies it to Identity through a test-only page convention. Every unbound page retains the old layout. Sidebar, breadcrumbs, topbar, local theme/language controls, event/account/notification menus, TempData toast, modal/drawer hosts and antiforgery emitter are present. Menu/overlay/navigation behavior follows in item4.

New SharedShellService projection preserves the old service path and lists current/setup phases in ascending start order (null last, ID tie-break). Selected past event remains on its button but outside the list; hidden eligible events are visible only to SuperAdmins and switch to limited Overview. Real domain hiding is available only after Live, so the hidden eligible test uses Final review, not an impossible hidden Draft. Normal switcher links retain the event page, including Review's query route. Nav keeps existing routes/reference labels and order. Notifications reuse the existing inbox and point the Admin-actions overview link to `/notifications#admin-actions-heading`.

Frozen tokens/components copied byte-for-byte to admin-design CSS names; both `cmp` checks passed. Local existing Geist file; Geist Mono deliberately falls back to `ui-monospace` (no additional font download). No shared/public/transitional CSS modified. Theme follows OS until manual browser choice, runs before stylesheets and persists through localStorage. No reference file changed, no prototype runtime, external font/CSS, Bootstrap, jQuery or flatpickr in the new layout.

## Checks

- Controlled PostgreSQL/HTTP `AdminDesignShellIntegrationTests`: 2 passed / 0 failed / 0 skipped. Proves switcher eligibility/order/current selection/hidden policy, real metadata layout rendering vs old-layout page, local assets, token and server TempData message. After final navigation markup correction, the affected HTTP test rerun: 1 passed / 0 failed / 0 skipped. TRX files `/private/tmp/bingo-u1-item3/item3-shell.trx` and `item3-layout-final.trx`.
- Existing unchanged `AdminShellUiTests|TransientToastUiTests`: 4 passed / 0 failed / 0 skipped; `/private/tmp/bingo-u1-item3/item3-existing-ui.trx`.
- Full JS runner: **38 files passed / 0 failed / 38 total**, exit0, including theme OS/manual/no-flash check; [results](item3-js-results.json). Existing controlled fixtures reused. Final nav markup did not alter JS tested by that run.
- Release Web and integration-test builds completed without warnings/errors as part of the focused runs. Early local compiler/analyzer and fixture errors were corrected before the passing runs: nullable descriptor/import, analyzer syntax, HTTPS secure cookies, hideable phase, Razor's omitted null class value.
- `git diff --check` clean. No existing tests changed in item3.

## Provenance and remaining gates

Claude implemented item2c `63ce639` and item2 `374b883`. Its durable item2 evidence records a precommit same-content 37/0 run; user/Claude additionally reports a 37/0 final-commit rerun at374b883. Neither run was performed by this implementer. CI itself remains unrun. Optional setup-node tag pinning note is carried unchanged; not part of this item.

Item4 next; Danish coverage in item6 and Identity binding in item7. Independent review and user visual acceptance pending. Whole .NET suite remains pending user/Claude execution at eventual final U1 SHA; no whole-suite result claimed here.

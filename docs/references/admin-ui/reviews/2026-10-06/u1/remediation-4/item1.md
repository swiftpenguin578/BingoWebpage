# Round4 item1 checkpoint — model-level error banner

Authority: brief60 item1 / A10. Baseline clean4aca5a119d91cf811b405382fc14c8aae0c4e4bf. No production changes and no item commit yet.

Before: EventCreationUiTests expected `asp-validation-summary="ModelOnly"` in Identity. After: exact `_AdminDesignBanner` use, is-error/error model, joined `ViewData.ModelState[string.Empty]` ErrorMessage values and hidden-only-when-no-model-errors expression. This preserves the asserted model-level server-error contract on the approved shared markup.

New real PostgreSQL/HTTP test `ModelLevelRefusalRendersInVisibleSharedErrorBannerWithoutSaving` posts a retired Input.Banner field, requires200 via the existing helper, a visible role=alert shared error banner with the exact refusal text, retained attempted Name, unchanged persisted Name/version and zero identity-update audits. Final execution **1 passed /0 failed /0 skipped** (banner3.log). Initial new-test development used missing OriginalName and assumed one error; the real handler adds the incomplete-baseline message twice through two existing paths. The intermediate exact-text test exposed that assumption. The final fixture uses the existing single retired-input refusal; no production behavior was altered. Empty HTML data-attribute serialization is parsed explicitly; exact message equality is retained.

Whole Bingo.BrowserTests after the named correction: **149 passed /1 failed /0 skipped**,150 total. It now reaches a second stale assertion in the same method: `Permanent event link`, while approved brief55 V25 renders `@T["AdminDesign.Event link"]`. One bounded scan of the remaining exact Identity/source assertions found one further stale selector: `asp-validation-for="Input.ConfirmTimezoneChange"`, now the shared `_AdminDesignFieldError` bound to that exact ModelState field. All other checked exact source strings match. Neither newly exposed assertion was changed. Proposed before→after: exact scoped Event link heading; exact ConfirmTimezoneChange shared-error field/model-state binding. Routed to dispatcher for A10/prior-reference-authority resolution before further changes.

Whole .NET was not launched. Diff check passes. Current item1 drafts are preserved; items2–7 have no implementation changes. Next action: resolve the two named stale assertions, rerun the whole BrowserTests gate after the authorized correction, then scoped item1 commit and continue brief60 in order. Final independent review/user acceptance remain pending.


## Completed under planner ruling61

`EventCreationUiTests.cs`: exact `Permanent event link` assertion → exact `@T["AdminDesign.Event link"]` (A10/61/brief55 V25). Existing assertions for `/Events/Signups` and `data-copy-url="@signupTableUrl"` remain. Exact `asp-validation-for="Input.ConfirmTimezoneChange"` → exact `_AdminDesignFieldError` model bound to that field's ModelState error (A10/61). No other assertions in the method changed beyond the previously approved model-banner correction.

Added `UnconfirmedTimezoneRendersSharedFieldErrorAndPermanentCopyLink`: actual PostgreSQL/HTTP public-event timezone POST without confirmation renders the exact shared field error at `error-ConfirmTimezoneChange`, the Event link heading, permanent Signups URL and copy-control URL; persisted timezone/version and audit remain unchanged. Executed **1/0/0**. Reused accepted model-banner proof **1/0/0**; whole Bingo.BrowserTests now **150 passed / 0 failed / 0 skipped**. Final logs copied beside this evidence. Diff check passed.

Observation only (61): duplicate incomplete-baseline message is added at `src/Bingo.Web/Pages/Admin/Events/Identity.cshtml.cs:95` by OnPostAsync transport completeness and at line183 by Prepare's missing-original-field check. Neither path nor the displayed messages were changed or hidden.

Item1 complete in this scoped commit. Next: brief60 item2 session-only remembered event; final whole .NET remains Claude execution on final SHA.

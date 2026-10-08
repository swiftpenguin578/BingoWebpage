# Brief74 item5 — destination headers

Parent3e82e14. Implementer evidence; independent review/manual acceptance pending.

The shared shell now builds each new-layout loading/failure header from its own
localized metadata. Identity's static description is filled from the authorized
event menu metadata; its existing skeleton moved to a shared partial, available
before the first Identity response. Dashboard/Events retain fresh empty reserved
summaries. Breadcrumb title changes with the destination. No cached result data,
business rules, routes, authorization, or frozen CSS/reference changes.

Checks:
- admin-design-destination-header.browser.js: Chromium12/0, WebKit12/0.
  Dashboard→Identity, Identity→Events, Events→Dashboard; loading/success/failure;
  EN/DA via real culture switch. Exact150ms show/400ms hold, no sleeps/retries.
- Focused AdminDesignLocalizationTests + EventCreationUiTests:30 passed,
  0 failed/0 skipped,7s. TRX:
  tests/Bingo.BrowserTests/TestResults/u2-rem3-item5-http.trx.
- Fixture Release0 warnings/errors,0.81s; git diff --check0.
- Authoring checkpoints: Razor template extraction initially included a section
  opener (compile error), corrected in place. Two initial browser setups assumed
  a selected event's Identity nav link existed on Dashboard; both failed before
  navigation. Changed approach to the fixture's authorized event URL and shared
  navigation API; all24 outcomes then passed. No assertion weakening.
- Full paired JS/parity/design gates follow remaining items. Whole .NET suite
  NOT RUN: planner-owned under Q-S1.

Changed: shared shell, Identity markup/shared loading partial/templates, browser
test and this evidence. Next:item5a loaded summary, then5b stylesheet handoff.

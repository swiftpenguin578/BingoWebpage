# Post-batch items 1–2 — 7 October 2026

Item 1 merged exactly verified T head `c4b6abd9a9decb3c0f640b957cedcdd4cbd52d2e` with `--no-ff`: merge `3cbf654ef8b87b4312cdddec8357590fecc1a028`. Three expected commits only (Accounts count markup, radius assertions, Audit Q10 summary); no conflicts. Register rows retained.

Item 2 rebuilt the Release solution (0 warnings/errors), then ran exactly `BINGO_ADMIN_STALE_EVIDENCE_DIRECTORY="$PWD/artifacts/js-fixtures" dotnet test tests/Bingo.IntegrationTests -c Release --no-build --filter FullyQualifiedName~AccountConfirmationRejectsCompletedInterveningChangesThenAcceptsFreshAction`: **8 passed / 0 failed / 0 skipped**. The eight HTML files actually read by the JavaScript runner now contain the current drawer. Compact hashes only are committed; raw synthetic HTML remains in ignored artifacts.

Separately rebuilt `tests/AdminDesignParityFixture/AdminDesignParityFixture.csproj -c Release --no-restore` (0 warnings/errors); the solution does not build that project. Browser checks explicitly used `BINGO_PARITY_CONFIGURATION=Release`, so they consumed the rebuilt fixture.

`admin-stale-change.browser.js` with the same evidence directory: **4 action groups passed**. `scripts/check-admin-page-conformance.cjs`, Chromium and WebKit: **45 cases per engine passed**, all seven registered pages at 390/494/860/1280/1440, plus frame/style, no-fade, document, update and Danish checks. Only Audit registration text was aligned with the merged approved Q10 sentence removal; generic checks unchanged. All previously reported T conformance and stale-fixture failures are resolved. Original ea336943 full-run result remains historical and unchanged. No repeated full JS or whole .NET suite.

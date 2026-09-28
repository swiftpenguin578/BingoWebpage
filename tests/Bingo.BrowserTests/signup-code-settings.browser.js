// Controlled Chromium fixture for the signup-code settings initializer. It does
// not start the application or touch an application database.
const assert = require("node:assert/strict");
const path = require("node:path");
const { chromium } = require("playwright");

(async () => {
  const browser = await chromium.launch({ headless: true, channel: process.env.PLAYWRIGHT_CHANNEL || "chrome" });
  try {
    const page = await browser.newPage();
    await page.setContent(`<!doctype html><html><body>
      <form id="signup-code-settings-form"><input type="checkbox" data-signup-code-toggle><div data-signup-code-control hidden><input data-signup-code-input data-has-signup-code="false"></div></form>
    </body></html>`);
    await page.evaluate(() => { window.watchAdminUnsavedChanges = () => () => {}; });
    await page.addScriptTag({ path: path.resolve("src/Bingo.Web/wwwroot/js/event-manage.js") });
    const form = page.locator("#signup-code-settings-form");
    const toggle = form.locator("[data-signup-code-toggle]");
    const control = form.locator("[data-signup-code-control]");
    const input = form.locator("[data-signup-code-input]");
    assert.equal(await control.isHidden(), true);
    assert.equal(await input.isDisabled(), true);
    assert.equal(await input.getAttribute("required"), null);
    await toggle.check();
    assert.equal(await control.isHidden(), false);
    assert.equal(await input.isDisabled(), false);
    assert.equal(await input.getAttribute("required"), "");
    await toggle.uncheck();
    assert.equal(await control.isHidden(), true);
    assert.equal(await input.isDisabled(), true);
    assert.equal(await input.getAttribute("required"), null);

    await page.evaluate(() => {
      document.querySelector("#signup-code-settings-form").outerHTML = `<form id="signup-code-settings-form"><input type="checkbox" data-signup-code-toggle checked><div data-signup-code-control hidden><input data-signup-code-input data-has-signup-code="true"></div></form>`;
      document.dispatchEvent(new CustomEvent("bingo:content-updated"));
    });
    const refreshed = page.locator("#signup-code-settings-form");
    assert.equal(await refreshed.locator("[data-signup-code-control]").isHidden(), false);
    assert.equal(await refreshed.locator("[data-signup-code-input]").isDisabled(), false);
    assert.equal(await refreshed.locator("[data-signup-code-input]").getAttribute("required"), null, "existing codes retain blank replacement semantics");
    console.log("PASS signup-code settings: initial off state, enabled required input, disabled off state, and partial-refresh existing-code state");
  } finally {
    await browser.close();
  }
})().catch(error => { console.error(error); process.exitCode = 1; });

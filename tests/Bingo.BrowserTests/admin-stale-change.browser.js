// Uses controlled HTML captured by AdminStaleChangeIntegrationTests and the shipped
// dialog scripts. HTTP/PostgreSQL assertions remain in that integration fixture.
const assert = require("node:assert/strict");
const fs = require("node:fs");
const path = require("node:path");
const { chromium } = require("playwright");
const confirmationPartial = fs.readFileSync("src/Bingo.Web/Pages/Shared/_AdminConfirmation.cshtml", "utf8").replace(/@T\["([^"]+)"\]/g, "$1");

(async () => {
  const fixtureDirectory = process.env.BINGO_ADMIN_STALE_EVIDENCE_DIRECTORY;
  assert.ok(fixtureDirectory, "Set BINGO_ADMIN_STALE_EVIDENCE_DIRECTORY to the integration test HTML fixtures.");
  const browser = await chromium.launch({ headless: true, channel: process.env.PLAYWRIGHT_CHANNEL });
  try {
    for (const action of ["Disable", "Restore", "GrantAdmin", "RevokeAdmin"]) {
      const opened = fs.readFileSync(path.join(fixtureDirectory, `account-${action}-opened.html`), "utf8");
      const stale = fs.readFileSync(path.join(fixtureDirectory, `account-${action}-stale.html`), "utf8");
      const accountPath = opened.match(/action="(\/Admin\/Accounts\/Manage\/[^?"\s]+)/)[1];
      const page = await browser.newPage();
      const requests = [];
      const errors = [];
      page.on("pageerror", error => errors.push(error.message));
      await page.route("https://bingo.test/**", async route => {
        const request = route.request();
        const url = new URL(request.url());
        if (request.method() === "POST") {
          requests.push({ url, body: request.postData() });
          return route.fulfill({ contentType: "text/html", body: stale });
        }
        if (url.pathname === accountPath) return route.fulfill({ contentType: "text/html", body: opened });
        if (url.pathname.startsWith("/js/")) return route.fulfill({ contentType: "text/javascript", body: fs.readFileSync(`src/Bingo.Web/wwwroot${url.pathname}`, "utf8") });
        return route.fulfill({ contentType: "text/html", body: `<html><body data-admin-account-action-error="Account request failed."><main id="main-content"><section class="admin-accounts-page"><a data-account-manage-trigger="true" href="${accountPath}">Manage</a></section></main>${confirmationPartial}<script src="/js/admin-confirmation.js"></script><script src="/js/admin-editor-guard.js"></script><script src="/js/account-manage-dialog.js"></script></body></html>` });
      });
      await page.goto("https://bingo.test/Admin/Accounts");
      await page.locator("[data-account-manage-trigger]").click();
      const modal = page.locator("dialog.account-manage-route-dialog");
      await modal.waitFor({ state: "visible" });
      const originalForm = modal.locator(`form[action*="handler=${action}"]`);
      const expected = await originalForm.locator('[name="ExpectedAuthorizationVersion"]').inputValue();
      const openConfirmation = async () => {
        await modal.locator(`[data-account-final-action][data-account-handler="${action}"]`).click();
        const dialog = page.locator("[data-admin-confirmation]");
        await dialog.waitFor({ state: "visible" });
        return { dialog, form: modal.locator(`form[action*="handler=${action}"]`) };
      };
      let confirmation = await openConfirmation();
      assert.equal(requests.length, 0, `${action}: revealing confirmation has no side effects`);
      assert.equal(await confirmation.form.locator('[name="ExpectedAuthorizationVersion"]').inputValue(), expected);
      if (action === "Disable") await confirmation.dialog.locator("textarea").fill("Browser fixture reason");
      await confirmation.dialog.locator("[data-admin-confirmation-action]").click();
      await modal.locator('[data-account-change-stale="true"]').waitFor();
      assert.equal(requests.length, 1, `${action}: one confirmation posts once`);
      assert.equal(requests[0].url.searchParams.get("handler"), action);
      assert.match(requests[0].body, new RegExp(`name="ExpectedAuthorizationVersion"\\r?\\n\\r?\\n${expected}\\r?\\n`));
      assert.equal(await modal.evaluate(element => element.open), true, `${action}: stale recovery retains the Manage dialog`);
      const feedback = modal.locator("[data-account-editor-feedback]");
      assert.equal(await feedback.isVisible(), true);
      assert.match(await feedback.innerText(), /changed by another administrator/);
      assert.equal(await modal.locator(".validation-summary").evaluate(element => element.classList.contains("visually-hidden")), true, "the replacement summary does not duplicate feedback");
      assert.equal(await confirmation.dialog.evaluate(element => element.open), false, "shared stale confirmation closes before a new decision");
      assert.equal(await modal.locator(".admin-account-inline-confirmation:not([hidden]), details[open]").count(), 0, "stale confirmation closes before a new decision");
      confirmation = await openConfirmation();
      const refreshed = await confirmation.form.locator('[name="ExpectedAuthorizationVersion"]').inputValue();
      assert.notEqual(refreshed, expected, `${action}: new confirmation uses current target freshness`);
      assert.equal(requests.length, 1, "reopening does not auto-submit the action");
      assert.deepEqual(errors, []);
      console.log(`PASS ${action}: two-stage freshness, one stale POST, visible recovery, fresh reconfirmation`);
      await page.close();
    }
  } finally {
    await browser.close();
  }
})().catch(error => { console.error(error); process.exitCode = 1; });

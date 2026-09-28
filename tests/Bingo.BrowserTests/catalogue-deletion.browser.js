// Focused CAT-01 browser boundary. All writes are intercepted fixtures; no app,
// database or provider is touched.
const assert = require("node:assert/strict");
const fs = require("node:fs");
const path = require("node:path");
const { chromium } = require("playwright");

const root = "src/Bingo.Web/";
const partial = fs.readFileSync(`${root}Pages/Shared/_AdminConfirmation.cshtml`, "utf8").replace(/@T\["([^"]+)"\]/g, "$1");

(async () => {
  const browser = await chromium.launch({ headless: true, channel: "chrome" });
  try {
    const page = await browser.newPage();
    const errors = [];
    page.on("pageerror", error => errors.push(error.message));
    let posts = 0;
    let blocked = false;
    let failSave = false;
    let releaseSave = null;
    const editor = (type = "", message = "") => `<section data-catalogue-page data-catalogue-editor-route="true" data-catalogue-status-type="${type}" data-catalogue-status-message="${message}"><section data-catalogue-editor><h1 id="catalogue-editor-title">Fixture activity</h1><p id="catalogue-editor-description">Fixture</p><p data-catalogue-editor-feedback hidden></p><button data-catalogue-reload hidden>Reload</button><form action="https://bingo.test/Admin/Catalogue?handler=Delete"><input name="recordType" type="hidden" value="drop"><input name="recordId" type="hidden" value="fixture"><input name="expectedVersion" type="hidden" value="1"><input name="__RequestVerificationToken" type="hidden" value="fixture-token"><button type="button" data-catalogue-delete>Delete permanently</button></form></section></section>`;
    await page.route("https://bingo.test/**", async route => {
      const url = new URL(route.request().url());
      if (url.pathname.startsWith("/js/")) {
        const file = url.pathname.endsWith("catalogue-admin.js") && process.env.CAT01_PROPOSAL_DIR
          ? path.join(process.env.CAT01_PROPOSAL_DIR, "catalogue-admin.js") : `${root}wwwroot${url.pathname}`;
        return route.fulfill({ contentType: "text/javascript", body: fs.readFileSync(file, "utf8") });
      }
      if (url.searchParams.get("handler") === "DeletionImpact") return route.fulfill({ json: { canDelete: !blocked, title: "Delete Fixture item permanently?", message: blocked ? "Referenced; deactivate instead." : "No dependencies. The unused record is permanently deleted; shared prices and history are retained." } });
      if (route.request().method() === "POST") {
        posts++;
        assert.match(route.request().postData(), /name="confirmed"\r\n\r\ntrue/);
        assert.match(route.request().postData(), /fixture-token/);
        await new Promise(resolve => { releaseSave = resolve; });
        return route.fulfill({ contentType: "text/html", body: editor(failSave ? "Warning" : "Success", failSave ? "This record became referenced. Deactivate it instead." : "Unused record deleted.") });
      }
      return route.fulfill({ contentType: "text/html", body: `<!doctype html><html><body data-signup-questions-save-error="Save failed."><main id="main-content">${editor()}</main>${partial}<script src="/js/site.js"></script><script src="/js/admin-confirmation.js"></script><script src="/js/admin-editor-guard.js"></script><script src="/js/catalogue-admin.js"></script></body></html>` });
    });
    await page.goto("https://bingo.test/Admin/Catalogue?bossId=fixture");
    const confirmation = page.locator("[data-admin-confirmation]");
    await page.locator("[data-catalogue-delete]").click();
    await confirmation.waitFor({ state: "visible" });
    assert.match(await confirmation.textContent(), /Fixture item/);
    assert.equal(await confirmation.locator("[data-admin-confirmation-typed-field]").isVisible(), false);
    await confirmation.locator("[data-admin-confirmation-cancel]").click();
    assert.equal(posts, 0);
    assert.equal(await page.locator("[data-catalogue-delete]").evaluate(element => element === document.activeElement), true);
    blocked = true;
    await page.locator("[data-catalogue-delete]").click();
    await page.waitForFunction(() => document.querySelector('[data-catalogue-editor-feedback]').textContent.includes('Referenced'));
    assert.equal(await confirmation.isVisible(), false); assert.equal(posts, 0);
    blocked = false; failSave = true;
    await page.locator("[data-catalogue-delete]").click();
    await confirmation.locator("[data-admin-confirmation-action]").click();
    await page.waitForFunction(() => document.querySelector('[data-admin-confirmation-form]').getAttribute('aria-busy') === 'true');
    await page.evaluate(() => document.querySelector('[data-admin-confirmation-form]').dispatchEvent(new Event('submit', { bubbles: true, cancelable: true })));
    await page.keyboard.press("Escape");
    assert.equal(posts, 1); assert.equal(await confirmation.isVisible(), true);
    releaseSave();
    await page.waitForFunction(() => document.querySelector('[data-admin-confirmation-feedback]').textContent.includes('became referenced'));
    assert.equal(await confirmation.isVisible(), true);
    await confirmation.locator("[data-admin-confirmation-cancel]").click();
    failSave = false;
    await page.locator("[data-catalogue-delete]").click();
    await confirmation.locator("[data-admin-confirmation-action]").click();
    await page.waitForFunction(() => document.querySelector('[data-admin-confirmation-form]').getAttribute('aria-busy') === 'true');
    releaseSave();
    await confirmation.waitFor({ state: "hidden" });
    assert.equal(posts, 2); assert.deepEqual(errors, []);
    console.log("PASS catalogue deletion: current impact, identity, cancel, role-independent fixture rejection, single pending POST, stale dependency feedback and success refresh.");
  } finally { await browser.close(); }
})().catch(error => { console.error(error); process.exitCode = 1; });

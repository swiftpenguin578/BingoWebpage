// Replays HTML captured by AdminStaleChangeIntegrationTests through the shipped Accounts page
// (T1, A10: rewritten for the drawer binding). HTTP/PostgreSQL assertions stay in that fixture.
const assert = require("node:assert/strict");
const fs = require("node:fs");
const path = require("node:path");
const { chromium } = require("playwright");
const asset = pathname => {
  const file = pathname.replace(/\.[a-z0-9]{10}\.(js|css)$/, ".$1");
  const full = path.join("src/Bingo.Web/wwwroot", file);
  return fs.existsSync(full) ? full : null;
};

(async () => {
  const fixtureDirectory = process.env.BINGO_ADMIN_STALE_EVIDENCE_DIRECTORY;
  assert.ok(fixtureDirectory, "Set BINGO_ADMIN_STALE_EVIDENCE_DIRECTORY to the integration test HTML fixtures.");
  const browser = await chromium.launch({ headless: true, channel: process.env.PLAYWRIGHT_CHANNEL });
  try {
    for (const action of ["Disable", "Restore", "GrantAdmin", "RevokeAdmin"]) {
      const opened = fs.readFileSync(path.join(fixtureDirectory, `account-${action}-opened.html`), "utf8");
      const stale = fs.readFileSync(path.join(fixtureDirectory, `account-${action}-stale.html`), "utf8");
      const accountId = opened.match(/data-account-drawer data-account-id="([^"]+)"/)[1];
      const page = await browser.newPage();
      const posts = [], errors = [];
      let current = opened;
      page.on("pageerror", error => errors.push(error.message));
      await page.route("https://bingo.test/**", async route => {
        const request = route.request(), url = new URL(request.url());
        if (request.method() === "POST") {
          posts.push({ url, body: request.postData() });
          current = stale; // the server's current state after the intervening change
          return route.fulfill({ contentType: "application/json", body: JSON.stringify({ outcome: "stale", message: "This record was changed by another administrator. Current values are shown; review them before trying again." }) });
        }
        const file = asset(url.pathname);
        if (file) return route.fulfill({ contentType: file.endsWith(".css") ? "text/css" : file.endsWith(".js") ? "text/javascript" : "application/octet-stream", body: fs.readFileSync(file) });
        if (/^\/admin\/accounts$/i.test(url.pathname)) return route.fulfill({ contentType: "text/html", body: current });
        return route.fulfill({ status: 404, body: "" });
      });
      await page.goto(`https://bingo.test/Admin/Accounts?account=${accountId}`);
      const drawer = page.locator(".drawer");
      await drawer.waitFor({ state: "visible" });
      const form = () => drawer.locator(`form[data-account-action="${action}"]`);
      const expected = await form().locator('[name="ExpectedAuthorizationVersion"]').inputValue();
      await form().locator("button").click();
      const modal = page.locator(".modal[role=alertdialog]");
      await modal.waitFor({ state: "visible" });
      assert.equal(posts.length, 0, `${action}: revealing the confirmation has no side effects`);
      if (action === "Disable") await modal.locator("textarea").fill("Browser fixture reason");
      await modal.locator("[data-account-confirm-accept]").click();
      await drawer.locator("#dr-banner").waitFor();
      assert.match(await drawer.locator("#dr-banner").innerText(), /changed by another administrator/);
      assert.equal(posts.length, 1, `${action}: one confirmation posts once`);
      assert.equal(posts[0].url.searchParams.get("handler"), action);
      assert.equal(posts[0].url.searchParams.get("account"), accountId);
      assert.match(posts[0].body, new RegExp(`name="ExpectedAuthorizationVersion"\\r?\\n\\r?\\n${expected}\\r?\\n`));
      assert.equal(await modal.count(), 0, `${action}: the stale confirmation closes before a new decision`);
      const refreshed = await form().locator('[name="ExpectedAuthorizationVersion"]').inputValue();
      assert.notEqual(refreshed, expected, `${action}: the drawer shows the current version`);
      await form().locator("button").click();
      await modal.waitFor({ state: "visible" });
      if (action === "Disable") assert.equal(await modal.locator("textarea").inputValue(), "Browser fixture reason", "A3: the stale reason draft is kept");
      assert.equal(posts.length, 1, "reopening does not auto-submit the action");
      assert.equal(new URL(page.url()).searchParams.get("account"), accountId);
      assert.deepEqual(errors, []);
      console.log(`PASS ${action}: one stale POST, visible recovery, current version, fresh reconfirmation`);
      await page.close();
    }
  } finally {
    await browser.close();
  }
})().catch(error => { console.error(error); process.exitCode = 1; });

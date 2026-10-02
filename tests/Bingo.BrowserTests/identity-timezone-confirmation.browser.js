// Controlled identity confirmation fixture using the shipped shared modal,
// editor guard and timezone owner. No application database is started.
const assert = require("node:assert/strict");
const fs = require("node:fs");
const http = require("node:http");
const { chromium } = require("playwright");
const root = "src/Bingo.Web/";
const partial = fs.readFileSync(`${root}Pages/Shared/_AdminConfirmation.cshtml`, "utf8").replace(/@T\["([^"]+)"\]/g, "$1");
const identityScript = fs.readFileSync(`${root}wwwroot/js/event-identity.js`, "utf8");

const identityMarkup = ({ identityUrl, version = "7", name = "Preview event", error = "", preview = true }) => `
<main id="main-content"><section data-identity-editor data-identity-event-id="fixture" data-identity-save-error="Identity save failed.">
  <form method="post" action="${identityUrl}">
    <div class="validation-summary">${error}</div>
    <input name="Input.Name" value="${name}">
    <input name="Input.Timezone" value="UTC">
    <input name="Input.Version" value="${version}">
    <button type="submit" data-identity-save>Save identity</button>
    ${preview ? `<section data-identity-timezone-preview data-title="Confirm timezone change" data-action-label="Confirm timezone change" aria-labelledby="review-heading">
      <h2 id="review-heading">Review timezone change</h2>
      <p data-identity-timezone-consequence>Stored UTC instants stay fixed.</p>
      <ul><li data-identity-timezone-row>Signups opened UTC ${version === "8" ? "13:00" : "12:00"} to Copenhagen 14:00</li><li data-identity-timezone-row>Signups closed UTC 13:00 to Copenhagen 15:00</li></ul>
      <button type="submit" data-identity-timezone-confirm>Confirm timezone change</button>
      <a data-identity-timezone-cancel data-identity-cancel href="/manage">Cancel</a>
    </section>` : ""}
    <a data-identity-cancel href="/manage">Cancel</a>
  </form>
</section></main>`;

(async () => {
  let postCount = 0;
  let failedOnce = false;
  let failureWithoutPreview = false;
  const postBodies = [];
  let serve;
  const server = http.createServer((request, response) => serve(request, response));
  await new Promise(resolve => server.listen(0, "127.0.0.1", resolve));
  const origin = `http://127.0.0.1:${server.address().port}`;
  const identityUrl = `${origin}/Admin/Events/Identity/fixture`;
  const browser = await chromium.launch({ headless: true, channel: process.env.PLAYWRIGHT_CHANNEL || "chrome" });
  const readBody = request => new Promise(resolve => {
    const chunks = [];
    request.on("data", chunk => chunks.push(chunk));
    request.on("end", () => resolve(Buffer.concat(chunks).toString("utf8")));
  });
  try {
    serve = async (request, response) => {
      const url = new URL(request.url, origin);
      const fulfill = ({ status = 200, headers = {}, body = "" }) => {
        response.writeHead(status, { "Content-Type": "text/html", ...headers });
        response.end(body);
      };
      if (url.pathname === "/js/admin-confirmation.js") return fulfill({ headers: { "Content-Type": "text/javascript" }, body: fs.readFileSync(`${root}wwwroot/js/admin-confirmation.js`, "utf8") });
      if (url.pathname === "/js/admin-editor-guard.js") return fulfill({ headers: { "Content-Type": "text/javascript" }, body: fs.readFileSync(`${root}wwwroot/js/admin-editor-guard.js`, "utf8") });
      if (url.pathname === "/js/event-identity.js") return fulfill({ headers: { "Content-Type": "text/javascript" }, body: identityScript });
      if (request.method === "POST") {
        postBodies.push(await readBody(request));
        postCount++;
        if (!failedOnce) {
          failedOnce = true;
          return fulfill({ body: `<!doctype html><html><body class="admin-shell-body">${identityMarkup({ identityUrl, version: "8", name: "Authoritative event", error: "This event changed while you were editing it.", preview: !failureWithoutPreview })}${partial}<script src="/js/admin-confirmation.js"></script><script src="/js/admin-editor-guard.js"></script><script src="/js/event-identity.js"></script></body></html>` });
        }
        return fulfill({ status: 303, headers: { location: "/Admin/Events/Manage/fixture" } });
      }
      if (url.pathname === "/Admin/Events/Manage/fixture") return fulfill({ body: "<!doctype html><html><body><h1>Saved</h1></body></html>" });
      if (url.pathname === "/before") return fulfill({ body: "<!doctype html><html><body><h1>Before identity</h1></body></html>" });
      if (url.pathname === "/manage") return fulfill({ body: "<!doctype html><html><body><h1>Manage</h1></body></html>" });
      const preview = url.searchParams.get("mode") !== "ordinary";
      return fulfill({ body: `<!doctype html><html lang="en"><head><link rel="stylesheet" href="/css/admin-confirmation.css"></head><body class="admin-shell-body">${identityMarkup({ identityUrl, preview })}${partial}<script src="/js/admin-confirmation.js"></script><script src="/js/admin-editor-guard.js"></script><script src="/js/event-identity.js"></script></body></html>` });
    };

    const page = await browser.newPage();
    const errors = [];
    page.on("pageerror", error => errors.push(error.message));
    await page.goto(identityUrl);
    const modal = page.locator("[data-admin-confirmation]");
    const review = page.locator("[data-identity-timezone-preview]");
    const save = page.locator("[data-identity-save]");
    await modal.waitFor({ state: "visible" });
    assert.equal(await page.locator("dialog[open]").count(), 1, "timezone preview uses only the shared modal");
    assert.equal(await review.isHidden(), true, "the page review is handed to the shared modal");
    assert.equal(await modal.locator("#admin-confirmation-description").textContent(), "Stored UTC instants stay fixed. Signups opened UTC 12:00 to Copenhagen 14:00 Signups closed UTC 13:00 to Copenhagen 15:00");
    assert.equal(await modal.locator("[data-admin-confirmation-cancel]").evaluate(element => document.activeElement === element), true, "shared modal receives focus");
    await page.keyboard.press("Escape");
    await modal.waitFor({ state: "hidden" });
    assert.equal(await review.isHidden(), true, "Escape keeps the JS confirmation single-surface");
    assert.equal(await page.locator("[data-identity-timezone-confirm]").isDisabled(), true, "the inline submit stays unusable after shared cancellation");
    assert.equal(await save.evaluate(element => document.activeElement === element), true, "Escape returns focus to the editor opener");
    assert.equal(await page.locator("input[name='Input.Name']").inputValue(), "Preview event");

    const identityCancel = page.locator("[data-identity-cancel]").last();
    await identityCancel.click();
    await modal.waitFor({ state: "visible" });
    await modal.locator("[data-admin-confirmation-cancel]").click();
    await modal.waitFor({ state: "hidden" });
    assert.match(page.url(), /Identity\/fixture/);
    assert.equal(await page.locator("input[name='Input.Name']").inputValue(), "Preview event", "discard cancellation preserves the proposed values");
    await identityCancel.click();
    await modal.waitFor({ state: "visible" });
    await modal.locator("[data-admin-confirmation-action]").click();
    await page.waitForURL(`${origin}/manage`);
    assert.equal(await page.locator("h1").textContent(), "Manage");

    await page.goto(identityUrl);
    await modal.waitFor({ state: "visible" });
    await modal.locator("[data-admin-confirmation-action]").click();
    await page.waitForFunction(() => document.querySelector("[data-identity-editor] input[name='Input.Version']")?.value === "8");
    assert.equal(postCount, 1, "the first confirmation reaches the server once");
    assert.equal(await modal.isVisible(), true, "the shared modal remains available for a corrected retry");
    assert.match(await page.locator(".validation-summary").textContent(), /event changed while you were editing it/);
    assert.match(await modal.locator("#admin-confirmation-description").textContent(), /Signups opened UTC 13:00/);
    assert.equal(await page.locator("[data-identity-timezone-confirm]").isHidden(), true, "the returned inline confirmation remains hidden");
    assert.equal(await page.locator("[data-identity-timezone-confirm]").isDisabled(), true, "the returned inline submit remains unusable");
    await modal.locator("[data-admin-confirmation-action]").click();
    await page.waitForURL(`${origin}/Admin/Events/Manage/fixture`);
    assert.equal(postCount, 2, "the corrected confirmation submits once");
    assert.match(postBodies[0], /name="Input\.Version"[\s\S]*?\r\n\r\n7\r\n/);
    assert.match(postBodies[1], /name="Input\.Version"[\s\S]*?\r\n\r\n8\r\n/, "retry uses the authoritative returned version");

    failedOnce = false;
    failureWithoutPreview = true;
    await page.goto(identityUrl);
    await modal.waitFor({ state: "visible" });
    await modal.locator("[data-admin-confirmation-action]").click();
    await page.waitForFunction(() => document.querySelector("input[name='Input.Name']")?.value === "Authoritative event");
    assert.equal(await page.locator("[data-identity-timezone-preview]").count(), 0);
    assert.equal(page.url(), identityUrl, "failed response without preview must not reload away the returned draft");
    assert.match(await page.locator(".validation-summary").textContent(), /event changed/);
    await page.keyboard.press("Escape");
    await modal.waitFor({ state: "hidden" });
    assert.equal(await page.locator("input[name='Input.Name']").inputValue(), "Authoritative event");
    await page.locator("[data-identity-cancel]").last().click();
    await modal.waitFor({ state: "visible" });
    await modal.locator("[data-admin-confirmation-cancel]").click();
    await modal.waitFor({ state: "hidden" });
    assert.equal(page.url(), identityUrl, "returned failed draft remains guarded after the preview disappears");

    const backPage = await browser.newPage();
    const backErrors = [];
    backPage.on("pageerror", error => backErrors.push(error.message));
    await backPage.goto(`${origin}/before`);
    await backPage.goto(identityUrl);
    const backModal = backPage.locator("[data-admin-confirmation]");
    await backModal.waitFor({ state: "visible" });
    await backPage.keyboard.press("Escape");
    await backModal.waitFor({ state: "hidden" });
    await backPage.goBack();
    await backModal.waitFor({ state: "visible" });
    await backModal.locator("[data-admin-confirmation-cancel]").click();
    await backModal.waitFor({ state: "hidden" });
    assert.match(backPage.url(), /Identity\/fixture/);
    assert.equal(await backPage.locator("input[name='Input.Name']").inputValue(), "Preview event", "Back discard cancellation preserves the proposed values");
    await backPage.goBack();
    await backModal.waitFor({ state: "visible" });
    await backModal.locator("[data-admin-confirmation-action]").click();
    await backPage.waitForURL(`${origin}/before`);
    assert.equal(await backPage.locator("h1").textContent(), "Before identity");

    const ordinary = await browser.newPage();
    const ordinaryErrors = [];
    ordinary.on("pageerror", error => ordinaryErrors.push(error.message));
    await ordinary.goto(`${origin}/before`);
    await ordinary.goto(`${identityUrl}?mode=ordinary`);
    await ordinary.locator("input[name='Input.Name']").fill("Dirty ordinary edit");
    const ordinaryModal = ordinary.locator("[data-admin-confirmation]");
    await ordinary.locator("[data-identity-cancel]").last().click();
    await ordinaryModal.waitFor({ state: "visible" });
    assert.equal(await ordinary.locator("[data-identity-timezone-preview]").count(), 0, "ordinary identity pages still have no inline timezone confirmation");
    await ordinaryModal.locator("[data-admin-confirmation-cancel]").click();
    await ordinaryModal.waitFor({ state: "hidden" });
    assert.match(ordinary.url(), /mode=ordinary/);
    assert.equal(await ordinary.locator("input[name='Input.Name']").inputValue(), "Dirty ordinary edit");
    await ordinary.goBack();
    await ordinaryModal.waitFor({ state: "visible" });
    await ordinaryModal.locator("[data-admin-confirmation-action]").click();
    await ordinary.waitForURL(`${origin}/before`);
    assert.equal(await ordinary.locator("h1").textContent(), "Before identity");
    assert.deepEqual(errors, []);
    assert.deepEqual(ordinaryErrors, []);
    assert.deepEqual(backErrors, []);
    console.log("PASS identity timezone confirmation: single shared surface, authoritative retry, dirty cancel/back navigation, preview-free error draft retention and server-confirmed submit");
  } finally {
    await browser.close();
    await new Promise(resolve => server.close(resolve));
  }
})().catch(error => { console.error(error); process.exitCode = 1; });

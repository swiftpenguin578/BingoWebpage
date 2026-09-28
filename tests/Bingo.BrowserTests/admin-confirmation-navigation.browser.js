// Controlled HTTP fixtures with the real route owners and shared confirmation.
// No application database, participant record or provider is touched.
const assert = require("node:assert/strict");
const fs = require("node:fs");
const http = require("node:http");
const { chromium } = require("playwright");
const root = "src/Bingo.Web/";
const partial = fs.readFileSync(`${root}Pages/Shared/_AdminConfirmation.cshtml`, "utf8").replace(/@T\["([^"]+)"\]/g, "$1");

(async () => {
  let serve;
  const server = http.createServer((request, response) => serve(request, response));
  await new Promise(resolve => server.listen(0, "127.0.0.1", resolve));
  const origin = `http://127.0.0.1:${server.address().port}`;
  const base = `${origin}/Admin/Events/Participants/fixture`;
  const browser = await chromium.launch({ headless: true, channel: process.env.PLAYWRIGHT_CHANNEL || "chrome" });
  try {
    for (const owner of ["questions", "participant"]) {
      const page = await browser.newPage();
      const errors = [];
      const nativePrompts = [];
      page.on("pageerror", error => errors.push(error.message));
      page.on("dialog", async dialog => { nativePrompts.push(dialog.type()); await dialog.accept(); });
      await page.addInitScript(() => {
        const add = window.addEventListener.bind(window);
        const remove = window.removeEventListener.bind(window);
        const listeners = new Set();
        window.unloadListenerCount = () => listeners.size;
        window.addEventListener = (type, listener, options) => {
          if (type === "beforeunload") listeners.add(listener);
          add(type, listener, options);
        };
        window.removeEventListener = (type, listener, options) => {
          if (type === "beforeunload") listeners.delete(listener);
          remove(type, listener, options);
        };
        add("pagehide", () => sessionStorage.setItem("fixture:unload-listeners", String(listeners.size)));
      });
      const editorPath = owner === "questions" ? "/Admin/Events/Questions/fixture" : "/Admin/Events/Participant/fixture";
      const fieldOwner = owner === "questions" ? "signup-questions" : "participant-editor";
      const closeAttribute = owner === "questions" ? "data-signup-questions-close" : "data-participant-edit-close";
      const editorAttribute = owner === "questions" ? "data-signup-questions-editor" : "data-participant-edit-page";
      const editorMarkup = `<section ${editorAttribute} data-participants-url="${base}"><h1 id="participant-edit-dialog-title">Edit</h1><p id="participant-edit-dialog-description">Edit fixture values</p><button type="button" ${closeAttribute}>Close editor</button><form method="post" action="${editorPath}"><label>Title <input name="title" value="Original"></label><button type="submit">Save</button></form><p data-${fieldOwner}-feedback role="alert" hidden></p></section>`;
      let postBranch = "header";
      let postCount = 0;
      serve = (request, response) => {
        const fulfill = ({ status = 200, contentType = "text/html", headers = {}, body = "" }) => {
          response.writeHead(status, { "Content-Type": contentType, ...headers });
          response.end(body);
        };
        const url = new URL(request.url, origin);
        if (url.pathname.startsWith("/js/")) return fulfill({ contentType: "text/javascript", body: fs.readFileSync(`${root}wwwroot${url.pathname}`, "utf8") });
        if (url.pathname.startsWith("/css/")) return fulfill({ contentType: "text/css", body: fs.readFileSync(`${root}wwwroot${url.pathname}`, "utf8") });
        if (request.method === "POST") {
          postCount++;
          if (postBranch === "redirect") return fulfill({ status: 303, headers: { location: "/saved/redirect" } });
          return fulfill({ contentType: "text/html", headers: { "X-Bingo-Post-Navigation": "/saved/header" }, body: "Saved" });
        }
        if (url.pathname.startsWith("/saved/")) return fulfill({ contentType: "text/html", body: "<!doctype html><html><body><h1>Saved</h1></body></html>" });
        if (url.pathname === editorPath) return fulfill({ contentType: "text/html", body: editorMarkup });
        const trigger = owner === "questions" ? `data-signup-questions-trigger="true"` : `class="participant-edit-action"`;
        return fulfill({ contentType: "text/html", body: `<!doctype html><html lang="en"><head><meta name="viewport" content="width=device-width"><link rel="stylesheet" href="/css/site.transitional.foundation.css"><link rel="stylesheet" href="/css/site.transitional.application.css"><link rel="stylesheet" href="/css/admin-confirmation.css"></head>
          <body class="admin-shell-body" data-signup-questions-save-error="Save failed."><main id="main-content"><div id="app-notice-region"></div><section class="${owner === "participant" ? "event-participants-page" : "questions-fixture"}"><a id="edit-trigger" ${trigger} href="${editorPath}">Edit</a></section></main>
          <dialog class="admin-route-dialog" data-signup-questions-dialog><div class="admin-route-dialog-content" data-signup-questions-content></div></dialog>
          ${partial}<script src="/js/site.js"></script><script src="/js/admin-confirmation.js"></script><script src="/js/admin-editor-guard.js"></script><script src="/js/${owner === "questions" ? "signup-questions-overlay" : "event-manage"}.js"></script></body></html>` });
      };
      await page.goto(base);
      const editor = page.locator(owner === "questions" ? "[data-signup-questions-dialog]" : "#participant-edit-dialog");
      const confirmation = page.locator("[data-admin-confirmation]");
      await page.locator("#edit-trigger").click();
      await editor.waitFor({ state: "visible" });
      const editorUrl = page.url();
      await editor.locator("input[name=title]").fill("Unsaved route edit");
      await page.goBack();
      await page.waitForURL(editorUrl);
      await confirmation.waitFor({ state: "visible" });
      assert.equal(await page.locator("dialog[open]").count(), 1, `${owner}: only the shared confirmation remains active after internal Forward`);
      await confirmation.locator("[data-admin-confirmation-cancel]").click();
      await editor.waitFor({ state: "visible" });
      assert.equal(page.url(), editorUrl, `${owner}: Cancel retains the editor URL`);
      assert.equal(await editor.locator("input[name=title]").inputValue(), "Unsaved route edit");
      assert.equal(await editor.locator("input[name=title]").evaluate(element => document.activeElement === element), true, `${owner}: Cancel returns focus to the edited field`);
      assert.equal(await page.evaluate(() => unloadListenerCount()), 1);
      await page.goBack();
      await page.waitForURL(editorUrl);
      await confirmation.waitFor({ state: "visible" });
      await confirmation.locator("[data-admin-confirmation-action]").click();
      await page.waitForURL(base);
      await editor.waitFor({ state: "hidden" });
      await page.waitForFunction(() => document.activeElement?.id === 'edit-trigger');
      assert.equal(await page.evaluate(() => unloadListenerCount()), 0, `${owner}: Discard clears browser-exit protection`);
      assert.equal(postCount, 0, `${owner}: Back/Cancel/Discard causes no mutation`);
      assert.deepEqual(nativePrompts, [], `${owner}: in-app history uses no native warning`);
      console.log(`PASS ${owner}: Back confirmation survives Forward; Cancel preserves URL/values/focus; Discard returns URL/focus and clears exit protection`);

      if (owner === "participant") {
        for (postBranch of ["header", "redirect"]) {
          await page.goto(base);
          await page.locator("#edit-trigger").click();
          await editor.waitFor({ state: "visible" });
          await editor.locator("input[name=title]").fill(`Saved via ${postBranch}`);
          assert.equal(await page.evaluate(() => unloadListenerCount()), 1);
          await editor.getByRole("button", { name: "Save", exact: true }).click();
          await page.waitForURL(`${origin}/saved/${postBranch}`);
          assert.equal(await page.evaluate(() => sessionStorage.getItem('fixture:unload-listeners')), "0", `${postBranch}: saved baseline is committed before leaving`);
          assert.deepEqual(nativePrompts, [], `${postBranch}: successful participant POST has no false native unsaved prompt`);
          console.log(`PASS participant ${postBranch}: successful POST navigation commits saved baseline before browser exit`);
        }
        assert.equal(postCount, 2);
      }
      assert.deepEqual(errors, []);
      await page.close();
    }
  } finally {
    await browser.close();
    await new Promise(resolve => server.close(resolve));
  }
})().catch(error => { console.error(error); process.exitCode = 1; });

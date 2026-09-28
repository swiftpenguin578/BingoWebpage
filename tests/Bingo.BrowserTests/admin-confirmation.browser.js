// Actual Chromium interaction against the shipped partial, CSS, guard and toast
// owner. No app/database/provider is started or mutated by these controlled fixtures.
const assert = require("node:assert/strict");
const fs = require("node:fs");
const { chromium } = require("playwright");
const root = "src/Bingo.Web/";
const partial = fs.readFileSync(`${root}Pages/Shared/_AdminConfirmation.cshtml`, "utf8")
  .replace(/@T\["([^"]+)"\]/g, "$1");

(async () => {
  const browser = await chromium.launch({ headless: true, channel: process.env.PLAYWRIGHT_CHANNEL || "chrome" });
  try {
    const page = await browser.newPage();
    const errors = [];
    page.on("pageerror", error => errors.push(error.message));
    await page.addInitScript(() => {
      window.unloadListeners = new Set();
      const add = window.addEventListener.bind(window);
      const remove = window.removeEventListener.bind(window);
      window.addEventListener = (type, listener, options) => {
        if (type === "beforeunload") window.unloadListeners.add(listener);
        add(type, listener, options);
      };
      window.removeEventListener = (type, listener, options) => {
        if (type === "beforeunload") window.unloadListeners.delete(listener);
        remove(type, listener, options);
      };
    });
    await page.route("https://bingo.test/**", route => {
      const url = new URL(route.request().url());
      if (url.pathname.startsWith("/js/")) return route.fulfill({ contentType: "text/javascript", body: fs.readFileSync(`${root}wwwroot${url.pathname}`, "utf8") });
      if (url.pathname.startsWith("/css/")) return route.fulfill({ contentType: "text/css", body: fs.readFileSync(`${root}wwwroot${url.pathname}`, "utf8") });
      return route.fulfill({ contentType: "text/html", body: `<!doctype html><html lang="en"><head><meta name="viewport" content="width=device-width"><link rel="stylesheet" href="/css/site.transitional.foundation.css"><link rel="stylesheet" href="/css/site.public-ui.css"><link rel="stylesheet" href="/css/site.transitional.application.css"><link rel="stylesheet" href="/css/admin-confirmation.css"></head>
        <body class="admin-shell-body"><main id="main-content"><div id="app-notice-region"></div><button id="open-editor">Edit</button></main>
        <dialog id="editor" data-toast-host><form><label>Title <input name="title" value="Original"></label><button type="button" id="close-editor">Close editor</button></form><p data-fixture-feedback role="alert" hidden></p></dialog>
        ${partial}<script src="/js/site.js"></script><script src="/js/admin-confirmation.js"></script><script src="/js/admin-editor-guard.js"></script>
        <script>
        window.editor = document.querySelector('#editor');
        window.guard = createAdminEditorGuard({ editor: () => editor, prefix: 'fixture', saveError: () => 'Save failed.' });
        guard.initialize();
        watchAdminUnsavedChanges(() => guard.dirtyForms().length > 0);
        document.querySelector('#open-editor').onclick = () => editor.showModal();
        document.querySelector('#close-editor').onclick = () => {
          if (guard.dirtyForms().length) guard.confirmDiscard(() => { editor.querySelector('form').reset(); guard.initialize(); editor.close(); });
          else editor.close();
        };
        </script></body></html>` });
    });
    await page.goto("https://bingo.test/Admin/fixture");
    const modal = page.locator("[data-admin-confirmation]");
    const cancel = page.locator("[data-admin-confirmation-cancel]");
    const action = page.locator("[data-admin-confirmation-action]");
    const unloadCount = () => page.evaluate(() => window.unloadListeners.size);
    assert.equal(await unloadCount(), 0, "clean editor has no browser-exit listener");
    await page.locator("#open-editor").click();
    await page.locator("#editor input").fill("Unsaved title");
    assert.equal(await unloadCount(), 1, "dirty values register one listener");
    await page.locator("#close-editor").click();
    await modal.waitFor({ state: "visible" });
    assert.equal(await page.locator("dialog[open]").count(), 1, "editor hands off without stacked dialogs");
    assert.equal(await modal.evaluate(element => getComputedStyle(element, '::backdrop').backgroundColor), "rgba(5, 7, 11, 0.72)");
    assert.equal(await cancel.evaluate(element => document.activeElement === element), true);
    await page.keyboard.press("Tab");
    assert.equal(await action.evaluate(element => document.activeElement === element), true, "Cancel precedes semantic action");
    await page.locator("#open-editor").evaluate(element => element.focus());
    assert.equal(await modal.evaluate(element => element.contains(document.activeElement)), true, "background focus remains inert");
    await page.keyboard.press("Escape");
    await modal.waitFor({ state: "hidden" });
    assert.equal(await page.locator("#editor").evaluate(element => element.open), true);
    assert.equal(await page.locator("#editor input").inputValue(), "Unsaved title");
    assert.equal(await page.locator("#close-editor").evaluate(element => document.activeElement === element), true, "meaningful trigger focus restored");
    assert.equal(await unloadCount(), 1, "cancel retains unsaved protection");
    await page.locator("#close-editor").click();
    await action.click();
    await modal.waitFor({ state: "hidden" });
    assert.equal(await page.locator("#editor").evaluate(element => element.open), false);
    assert.equal(await unloadCount(), 0, "discard clears browser-exit warning");

    await page.locator("#open-editor").click();
    await page.locator("#editor input").fill("Saved title");
    await page.evaluate(() => guard.initialize());
    assert.equal(await unloadCount(), 0, "save baseline clears warning");
    await page.evaluate(() => { window.finishSave = guard.begin(editor.querySelector('form')); });
    assert.equal(await unloadCount(), 0, "clean pending request is not unsaved work");
    await page.evaluate(() => finishSave());

    const openAction = async options => page.evaluate(options => {
      window.calls = 0;
      window.answer = null;
      window.adminConfirmation.open({ title: "Remove registration?", description: "This removes the registration from this event.", actionLabel: "Remove", requireReason: true, ...options,
        onConfirm: values => { window.calls++; window.received = values; return new Promise(resolve => { window.settle = resolve; }); }
      }).then(value => { window.answer = value; });
    }, options);
    await openAction({});
    await page.locator("#admin-confirmation-reason").fill("Duplicate registration");
    await action.click();
    await page.evaluate(() => document.querySelector('[data-admin-confirmation-form]').dispatchEvent(new Event('submit', { bubbles: true, cancelable: true })));
    await page.keyboard.press("Escape");
    assert.equal(await page.evaluate(() => calls), 1, "pending submit cannot repeat");
    assert.equal(await modal.evaluate(element => element.open), true, "pending request cannot dismiss");
    await page.evaluate(() => settle({ succeeded: false, message: 'This registration changed. Reload it before trying again.' }));
    const feedback = modal.locator("[data-admin-confirmation-feedback]");
    await feedback.waitFor({ state: "visible" });
    assert.equal(await feedback.getAttribute("role"), "alert");
    assert.equal(await feedback.evaluate(element => document.activeElement === element), true);
    assert.equal(await page.locator("#admin-confirmation-reason").inputValue(), "Duplicate registration");
    assert.equal(await page.locator("[data-transient-toast]").count(), 0, "failure has one announced result, not a duplicate toast");
    await page.evaluate(() => showBingoToast('Independent warning', 'warning'));
    await modal.locator("[data-transient-toast]").waitFor({ state: "visible" });
    assert.equal(await page.locator("#app-notice-region").count(), 1, "shared toast host stays unique and visible in modal layer");
    await modal.locator("[data-dismiss-toast]").focus();
    await page.keyboard.press("Escape");
    assert.equal(await modal.evaluate(element => element.open), true, "dismissing a toast does not also cancel the confirmation");
    await action.click();
    await page.evaluate(() => settle({ succeeded: true }));
    await modal.waitFor({ state: "hidden" });
    assert.equal(await page.evaluate(() => answer), true);
    assert.equal(await page.locator("#editor").evaluate(element => element.open), true, "successful action hands back editor");
    assert.equal(await page.evaluate(() => received.reason), "Duplicate registration");

    await page.setViewportSize({ width: 320, height: 568 });
    await openAction({ description: "A long consequence. ".repeat(75), requireReason: false });
    const bounds = await modal.boundingBox();
    assert.ok(bounds.x >= 0 && bounds.width <= 320 && bounds.y >= 0 && bounds.height <= 568, "narrow modal stays inside viewport");
    await cancel.scrollIntoViewIfNeeded();
    await cancel.click();
    await modal.waitFor({ state: "hidden" });
    assert.equal(await page.evaluate(() => calls), 0, "cancellation never invokes mutation");
    await page.setViewportSize({ width: 1000, height: 800 });
    await openAction({ wom: { value: "FETCH", label: "Type FETCH to fetch WOM data now." } });
    await page.locator("#admin-confirmation-reason").fill("Provider recovery");
    await page.locator("#admin-confirmation-typed").fill("wrong");
    await action.click();
    assert.equal(await page.evaluate(() => calls), 0, "WOM exact input is enforced");
    await page.locator("#admin-confirmation-typed").fill("FETCH");
    await action.click();
    await page.evaluate(() => settle(true));
    await modal.waitFor({ state: "hidden" });
    assert.equal(await page.evaluate(() => calls), 1);
    assert.deepEqual(errors, []);
    console.log("PASS shared confirmation: focus/Escape, cancel, handback, submit-once, failed/successful submission, modal toast, narrow viewport, WOM and beforeunload save/discard lifecycle");
  } finally { await browser.close(); }
})().catch(error => { console.error(error); process.exitCode = 1; });

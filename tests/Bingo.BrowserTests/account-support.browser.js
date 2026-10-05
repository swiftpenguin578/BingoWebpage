// Controlled Chromium fixtures exercising the shipped account adapters and shared dialog.
const assert = require("node:assert/strict");
const fs = require("node:fs");
const { chromium } = require("playwright");
const root = "src/Bingo.Web/";
const partial = fs.readFileSync(`${root}Pages/Shared/_AdminConfirmation.cshtml`, "utf8").replace(/@T\["([^"]+)"\]/g, "$1");

(async () => {
  const browser = await chromium.launch({ headless: true, channel: process.env.PLAYWRIGHT_CHANNEL || "chrome" });
  try {
    const page = await browser.newPage();
    const errors = [];
    page.on("pageerror", error => errors.push(error.message));
    let posts = [];
    let failure = false;
    let stale = false;
    let releasePost;
    let editable = false;
    const action = (handler, reason = false) => `<form method="post" action="/Admin/Accounts/Manage/target?handler=${handler}"><input type="hidden" name="ExpectedAuthorizationVersion" value="${stale ? 8 : 7}"><button type="submit" data-account-final-action="true" data-account-confirmation-title="${handler}?" data-account-confirmation-support="Account access changes." data-account-confirmation-label="${handler}" data-account-confirmation-reason="${reason}">${handler}</button></form>`;
    const manage = () => `<section data-account-dialog-page data-account-dialog-kind="manage" data-account-manage-page data-account-dialog-overlay="true" data-account-change-stale="${stale}" data-account-validation-message="${failure ? "Account changed. Review current values." : ""}" data-account-status-message="${failure ? "" : "Account updated."}" aria-labelledby="manage-title"><h2 id="manage-title">Support account</h2><button data-account-dialog-close>Close</button><p data-account-editor-feedback hidden></p><form method="post" action="/Admin/Accounts/Manage/target?handler=GenerateResetLink"><button>Generate reset link</button></form>${editable ? `<form method="post" action="/Admin/Accounts/Manage/target?handler=Note"><input name="Note" value="Original note"></form>` : ""}${action("GrantAdmin")}${action("Disable", true)}${action("Restore")}</section>`;
    const directory = `<section class="admin-accounts-page"><a data-account-manage-trigger="true" href="/Admin/Accounts/Manage/target">Manage</a></section>`;
    const shell = (body, script) => `<!doctype html><html><body data-admin-account-action-error="Try again."><main id="main-content">${body}</main>${partial}<script>window.showBingoToast=(message,type)=>{window.lastToast={message,type,editorOpen:!!document.querySelector('dialog.account-manage-route-dialog[open]')};};</script><script src="/js/admin-confirmation.js"></script><script src="/js/admin-editor-guard.js"></script><script src="/js/${script}"></script></body></html>`;
    await page.route("https://bingo.test/**", async route => {
      const request = route.request();
      const url = new URL(request.url());
      if (url.pathname.startsWith("/js/")) return route.fulfill({ contentType: "text/javascript", body: fs.readFileSync(`${root}wwwroot${url.pathname}`, "utf8") });
      if (request.method() === "POST") {
        posts.push({ handler: url.searchParams.get("handler"), body: request.postData() });
        if (releasePost) await new Promise(resolve => { releasePost = resolve; });
      }
      if (url.pathname.endsWith("Transfer")) return route.fulfill({ contentType: "text/html", body: shell(`<form method="post" data-account-transfer data-confirm-title="Transfer?" data-confirm-description="Both accounts must sign in again." data-confirm-action="Transfer ownership"><select name="Input.DestinationId" required><option value="">Choose</option><option value="recipient-id" data-authorization-version="9">Recipient</option></select><input type="hidden" name="Input.ExpectedAuthorizationVersion"><input name="Input.DestinationUsernameConfirmation" required><input type="password" name="Input.CurrentPassword" required><button>Transfer ownership</button></form>`, "account-transfer.js") });
      return route.fulfill({ contentType: "text/html", body: shell(url.pathname.includes("/Manage/") ? manage() : directory, "account-manage-dialog.js") });
    });
    await page.goto("https://bingo.test/Admin/Accounts/Index");
    await page.getByText("Manage", { exact: true }).click();
    const editor = page.locator("dialog.account-manage-route-dialog");
    const modal = page.locator("[data-admin-confirmation]");
    const confirm = page.locator("[data-admin-confirmation-action]");
    await editor.waitFor({ state: "visible" });
    await editor.getByText("Generate reset link", { exact: true }).click();
    await page.waitForFunction(() => window.lastToast?.message === "Account updated.");
    assert.equal(posts[0].handler, "GenerateResetLink");
    assert.equal(await modal.isVisible(), false, "reset posts without a confirmation");

    await editor.getByText("GrantAdmin", { exact: true }).click();
    await modal.waitFor({ state: "visible" });
    assert.equal(await page.locator("dialog[open]").count(), 1, "shared modal suspends the editor");
    await page.keyboard.press("Escape");
    await editor.waitFor({ state: "visible" });
    assert.equal(posts.length, 1, "cancel does not mutate");

    await editor.getByText("Disable", { exact: true }).click();
    await modal.locator("textarea").fill("Support request");
    failure = true;
    stale = true;
    await confirm.click();
    await modal.locator("[data-admin-confirmation-feedback]").waitFor({ state: "visible" });
    assert.match(posts[1].body, /Support request/);
    assert.equal(await modal.isVisible(), true, "failure remains visible in the active modal");
    assert.equal(await modal.locator("textarea").inputValue(), "Support request");
    await page.locator("[data-admin-confirmation-cancel]").click();
    await editor.waitFor({ state: "visible" });
    assert.equal(await editor.locator("input[name=ExpectedAuthorizationVersion]").first().inputValue(), "8", "stale values refresh behind confirmation");

    failure = false;
    await editor.getByText("Disable", { exact: true }).click();
    await modal.locator("textarea").fill("Reviewed change");
    releasePost = true;
    await confirm.click();
    await page.waitForFunction(() => document.querySelector('[data-admin-confirmation-action]').disabled);
    await page.evaluate(() => document.querySelector('[data-admin-confirmation-form]').dispatchEvent(new Event("submit", { bubbles: true, cancelable: true })));
    await page.waitForTimeout(50);
    assert.equal(posts.length, 3, "pending confirmation posts once");
    await page.evaluate(() => history.back());
    await page.waitForURL(/overlay=1/);
    assert.equal(await modal.isVisible(), true, "Back cannot abandon a pending mutation");
    const release = releasePost;
    releasePost = null;
    release();
    await editor.waitFor({ state: "visible" });
    assert.equal(await modal.isVisible(), false);
    await page.waitForFunction(() => window.lastToast?.editorOpen);

    await page.goto("https://bingo.test/Admin/Accounts/Transfer");
    await page.locator("select").selectOption("recipient-id");
    await page.locator("input[name='Input.DestinationUsernameConfirmation']").fill("Recipient");
    await page.locator("input[type=password]").fill("current-password");
    await page.getByText("Transfer ownership", { exact: true }).first().click();
    await modal.waitFor({ state: "visible" });
    assert.match(await modal.textContent(), /Recipient/);
    await page.locator("[data-admin-confirmation-cancel]").click();
    assert.equal(await page.locator("input[type=password]").inputValue(), "current-password");
    assert.equal(await page.locator("input[name='Input.DestinationUsernameConfirmation']").inputValue(), "Recipient");
    await page.getByText("Transfer ownership", { exact: true }).first().click();
    await confirm.click();
    await page.waitForLoadState();
    assert.match(posts.at(-1).body, /Input.ExpectedAuthorizationVersion=9/);
    assert.match(posts.at(-1).body, /Input.DestinationId=recipient-id/);
    assert.match(posts.at(-1).body, /Input.DestinationUsernameConfirmation=Recipient/);

    // Brief47: the confirmation's hidden Reason is transport only. After an
    // ordinary failure and Cancel, the next action reaches its own confirmation;
    // a genuinely edited editor field still asks to discard unsaved changes.
    editable = true;
    failure = true;
    stale = false;
    await page.goto("https://bingo.test/Admin/Accounts/Index");
    await page.getByText("Manage", { exact: true }).click();
    await editor.waitFor({ state: "visible" });
    const transportPosts = posts.length;
    await editor.getByText("Disable", { exact: true }).click();
    await modal.locator("textarea").fill("Transport reason");
    await confirm.click();
    await modal.locator("[data-admin-confirmation-feedback]").waitFor({ state: "visible" });
    assert.equal(posts.length, transportPosts + 1, "failed Disable posts once");
    assert.match(posts.at(-1).body, /Transport reason/);
    await page.locator("[data-admin-confirmation-cancel]").click();
    await editor.waitFor({ state: "visible" });
    assert.equal(await editor.locator("form[action*='handler=Disable'] [name=Reason]").count(), 0, "failed confirmation leaves no transport Reason in the editor form");
    await editor.getByText("Disable", { exact: true }).click();
    await modal.waitFor({ state: "visible" });
    assert.equal(await modal.locator("#admin-confirmation-title").textContent(), "Disable?", "next action opens its own confirmation, not the discard prompt");
    assert.equal(await modal.locator("textarea").isVisible(), true, "next action's confirmation shows the reason field");
    await page.locator("[data-admin-confirmation-cancel]").click();
    await editor.waitFor({ state: "visible" });
    await editor.locator("input[name=Note]").fill("Edited note");
    await editor.getByText("Disable", { exact: true }).click();
    await modal.waitFor({ state: "visible" });
    assert.equal(await modal.locator("#admin-confirmation-title").textContent(), "Discard unsaved changes?", "a genuinely edited field still triggers the discard prompt");
    assert.equal(await modal.locator("textarea").isVisible(), false, "discard prompt has no reason field");
    await page.locator("[data-admin-confirmation-cancel]").click();
    await editor.waitFor({ state: "visible" });
    assert.equal(await editor.locator("input[name=Note]").inputValue(), "Edited note", "cancelling the discard prompt keeps the edit");
    assert.equal(posts.length, transportPosts + 1, "cancelled confirmations do not post");
    assert.deepEqual(errors, []);
    console.log("Account support Chromium checks passed.");
  } finally { await browser.close(); }
})().catch(error => { console.error(error); process.exitCode = 1; });

// Controlled DOM fixture for lifecycle confirmation transport. It does not start
// the application or touch an application database.
const assert = require("node:assert/strict");
const fs = require("node:fs");
const http = require("node:http");
const { chromium } = require("playwright");

const root = "src/Bingo.Web/";
const partial = fs.readFileSync(`${root}Pages/Shared/_AdminConfirmation.cshtml`, "utf8").replace(/@T\["([^"]+)"\]/g, "$1");

(async () => {
  let requests = [];
  const server = http.createServer((request, response) => {
    const url = new URL(request.url, "http://127.0.0.1");
    const fulfill = ({ status = 200, contentType = "text/html", body = "" }) => {
      response.writeHead(status, { "Content-Type": contentType });
      response.end(body);
    };
    if (request.method === "POST") {
      let body = "";
      request.on("data", chunk => { body += chunk; });
      request.on("end", () => {
        requests.push(new URLSearchParams(body));
        fulfill({ body: "<!doctype html><html><body>saved</body></html>" });
      });
      return;
    }
    if (url.pathname.startsWith("/js/")) return fulfill({ contentType: "text/javascript", body: fs.readFileSync(`${root}wwwroot${url.pathname}`, "utf8") });
    if (url.pathname.startsWith("/css/")) return fulfill({ contentType: "text/css", body: fs.readFileSync(`${root}wwwroot${url.pathname}`, "utf8") });
    return fulfill({ body: `<!doctype html><html><body class="admin-shell-body">
      <form id="finalized" method="post" data-lifecycle-confirm data-confirm-title="Reopen finalized" data-confirm-description="Reopen finalized" data-confirm-action="Reopen" data-confirm-field="ConfirmLifecycleAction" data-confirm-reason-field="Reason" data-confirm-require-reason="true">
        <input type="hidden" name="ExpectedVersion" value="17"><input type="hidden" name="Reason"><input type="hidden" name="ConfirmLifecycleAction" value="false"><button type="submit">Finalized reopen</button>
      </form>
      <form id="archived" method="post" data-lifecycle-confirm data-confirm-title="Reopen archived" data-confirm-description="Reopen archived" data-confirm-action="Reopen" data-confirm-field="ConfirmLifecycleAction" data-confirm-reason-field="Reason" data-confirm-require-reason="true">
        <input type="hidden" name="ExpectedVersion" value="23"><input type="hidden" name="Reason"><input type="hidden" name="ConfirmLifecycleAction" value="false"><button type="submit">Archived reopen</button>
      </form>
      ${partial}
      <script src="/js/admin-confirmation.js"></script><script src="/js/admin-lifecycle-confirm.js"></script>
    </body></html>` });
  });
  await new Promise(resolve => server.listen(0, "127.0.0.1", resolve));
  const origin = `http://127.0.0.1:${server.address().port}`;
  const browser = await chromium.launch({ headless: true, channel: process.env.PLAYWRIGHT_CHANNEL || "chrome" });
  try {
    const page = await browser.newPage();
    const errors = [];
    page.on("pageerror", error => errors.push(error.message));
    await page.goto(origin);
    for (const [id, version, reasonText] of [["finalized", "17", "Finalized correction"], ["archived", "23", "Archived correction"]]) {
      const form = page.locator(`#${id}`);
      const reason = form.locator("input[name=Reason]");
      const confirmation = form.locator("input[name=ConfirmLifecycleAction]");
      assert.equal(await reason.getAttribute("type"), "hidden");
      assert.equal(await confirmation.inputValue(), "false");
      assert.equal(await form.locator("input[name=ExpectedVersion]").inputValue(), version);
      await form.getByRole("button").click();
      const modal = page.locator("[data-admin-confirmation]");
      await modal.waitFor({ state: "visible" });
      assert.equal(await page.locator("dialog[open]").count(), 1);
      await modal.locator("#admin-confirmation-reason").fill(reasonText);
      await Promise.all([page.waitForNavigation(), modal.locator("[data-admin-confirmation-action]").click()]);
      const posted = requests.at(-1);
      assert.equal(posted.get("ExpectedVersion"), version);
      assert.equal(posted.get("Reason"), reasonText);
      assert.equal(posted.get("ConfirmLifecycleAction"), "true");
      await page.goto(origin);
    }
    assert.deepEqual(errors, []);
    assert.equal(requests.length, 2);
    console.log("PASS lifecycle confirmation: Finalized and Archived reopen forms open the shared reason modal with hidden false state and post exact reason, version, and true confirmation once");
  } finally {
    await browser.close();
    await new Promise(resolve => server.close(resolve));
  }
})().catch(error => { console.error(error); process.exitCode = 1; });

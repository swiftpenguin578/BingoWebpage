// V55: a real pointer click while the text field is still focused must save once.
const assert = require('node:assert/strict');
const http = require('node:http');
const { chromium, webkit } = require('playwright');
const fixture = require('./fixtures/identity.cjs');
(async () => {
  let name = 'Original', posts = 0, version = 7;
  const bodies = [], errors = [];
  const server = http.createServer(async (req, res) => {
    const send = (body, status = 200, headers = {}) => { res.writeHead(status, { 'Content-Type': 'text/html', ...headers }); res.end(body); };
    const url = new URL(req.url, origin);
    if (url.pathname.startsWith('/js/')) return send(fixture.script(url.pathname), 200, { 'Content-Type': 'text/javascript' });
    if (req.method === 'POST') {
      const chunks = []; for await (const chunk of req) chunks.push(chunk);
      const form = await new Request(url, { method: 'POST', headers: req.headers, body: Buffer.concat(chunks) }).formData();
      bodies.push(Object.fromEntries(form)); posts++; name = form.get('Input.Name'); version++;
      return send('', 303, { Location: url.pathname });
    }
    return send(fixture.page(fixture.editor({ url: route, preview: false, reviewed: false, name, originalName: name, timezone: 'UTC', originalTimezone: 'UTC', version })));
  });
  await new Promise(resolve => server.listen(0, '127.0.0.1', resolve));
  const origin = `http://127.0.0.1:${server.address().port}`, route = `${origin}/Admin/Events/Identity/${fixture.eventId}`;
  const browser = await (process.env.PLAYWRIGHT_BROWSER === 'webkit' ? webkit.launch({ headless: true }) : chromium.launch({ headless: true, channel: process.env.PLAYWRIGHT_CHANNEL || 'chrome' }));
  try {
    const page = await browser.newPage({ reducedMotion: 'reduce' }); page.on('pageerror', error => errors.push(error.message));
    await page.goto(route);
    const input = page.locator('[name="Input.Name"]'), save = page.locator('[data-identity-save]');
    for (const [index, value] of ['First edit', 'Second edit'].entries()) {
      await input.fill(value);
      assert.equal(await input.evaluate(element => element === document.activeElement), true);
      const mutations = await page.evaluate(() => {
        const button = document.querySelector('[data-identity-save]');
        const records = [], observer = new MutationObserver(items => records.push(...items));
        observer.observe(button, { subtree: true, childList: true, attributes: true, characterData: true });
        document.querySelector('[name="Input.Name"]').dispatchEvent(new Event('change', { bubbles: true }));
        const count = observer.takeRecords().length; observer.disconnect(); return count;
      });
      assert.equal(mutations, 0, 'text-field change leaves Save DOM untouched');
      await save.click();
      await page.waitForFunction(expected => document.querySelector('[data-identity-current-version]').dataset.identityCurrentVersion === String(expected), 8 + index);
      assert.equal(posts, index + 1, 'one pointer click makes exactly one POST');
      assert.equal(bodies[index]['Input.Name'], value);
      assert.equal(bodies[index]['Input.Version'], String(7 + index));
      assert.equal(await input.inputValue(), value);
      assert.equal(await save.evaluate(element => element === document.activeElement), true);
      assert.equal(page.url(), route);
    }
    assert.deepEqual(errors, []);
    console.log('PASS first and second focused-field pointer Save: unchanged blur DOM, exactly one POST each, fresh version, retained Identity route and Save focus');
  } finally { await browser.close(); await new Promise(resolve => server.close(resolve)); }
})().catch(error => { console.error(error); process.exitCode = 1; });

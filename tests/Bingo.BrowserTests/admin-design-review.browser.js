// U8 review M3: the Review workspace's client flows (admin-review.js) on the controlled "live" fixture, in both engines.
// Server answers that need a specific failure (refusal, lost response, lost session) are injected at the network edge;
// the stale correction is real: a second tab saves first, so the first tab's save is answered stale by the server.
const assert = require('node:assert/strict'), fs = require('node:fs'), path = require('node:path');
const { chromium, webkit } = require('playwright');
const { startFixture, login } = require('../../scripts/lib/admin-parity-fixture.cjs');

(async () => {
  const engine = process.env.PLAYWRIGHT_BROWSER === 'webkit' ? 'webkit' : 'chromium';
  const out = path.join(process.cwd(), 'artifacts/admin-review-' + engine), checks = [];
  const fixture = await startFixture(process.cwd(), out, { BINGO_PARITY_UR_PROFILE: 'live' });
  let browser;
  try {
    browser = await (engine === 'webkit' ? webkit.launch({ headless: true }) : chromium.launch({ headless: true, channel: process.env.PLAYWRIGHT_CHANNEL || 'chromium' }));
    const context = await browser.newContext({ viewport: { width: 1280, height: 900 }, reducedMotion: 'reduce' });
    const page = await login(context, fixture), errors = [];
    page.on('pageerror', e => errors.push(e.message));
    page.setDefaultTimeout(15000);
    const eventId = fixture.events['ur-current'];
    const queueUrl = fixture.origin + '/Admin/Review?eventId=' + eventId;
    const decisionRoute = handler => new RegExp('/Admin/Review/Details/[^?]+\\?[^#]*handler=' + handler + '(&|$)', 'i');
    const readbackRoute = /handler=Readback/i;
    const json = (body, status = 200) => ({ status, contentType: 'application/json', body: JSON.stringify(body) });
    const open = async (target, href) => { await target.goto(fixture.origin + href); await target.locator('[data-review-workspace]').waitFor(); };
    const notice = target => target.locator('[data-review-notice]');
    // The account list marks each option (· Current / Released / Left team); picks made before it loads would become the baseline.
    const accountsLoaded = target => target.waitForFunction(() => [...document.querySelectorAll('[data-cf-acct] option')].some(option => option.textContent.includes('·')));
    const settled = async target => { await target.waitForFunction(() => !document.querySelector('[data-review-decide][aria-busy="true"]')); };

    /* queue filter updates in place: no document reload, rows follow the filter */
    await page.goto(queueUrl); await page.locator('[data-review-queue]').waitFor();
    const hrefsFor = async () => page.locator('a[href*="/Admin/Review/Details/"]').evaluateAll(links => links.map(link => link.getAttribute('href')));
    const all = await hrefsFor();
    await page.evaluate(() => { window.__sameDocument = true; });
    await page.locator('[data-review-status][value="Pending"]').check({ force: true });
    await page.waitForFunction(() => new URL(location.href).searchParams.get('status') === 'Pending');
    await page.waitForFunction(count => document.querySelectorAll('a[href*="/Admin/Review/Details/"]').length < count, all.length);
    assert.equal(await page.evaluate(() => window.__sameDocument), true, 'the queue filter must update in place');
    const pending = await hrefsFor();
    assert.ok(pending.length >= 4, 'the live fixture offers at least four pending submissions');
    assert.equal(await page.locator('[data-review-status][value="Pending"]').isChecked(), true);
    checks.push('queue status filter updates the rows in place without a document reload');

    /* a definite refusal shows the server's reason and keeps the written reason */
    await open(page, pending[0]);
    const reasonText = 'The drop message is not visible in the screenshot.';
    await page.locator('[data-review-start="reject"]').click();
    await page.locator('#dec-reason').fill(reasonText);
    await page.route(decisionRoute('Reject'), route => route.fulfill(json({ outcome: 'refused', kind: 'reject', message: 'Synthetic server refusal reason.' })));
    await page.locator('[data-review-form="reject"] [type="submit"]').click();
    await page.locator('[data-review-decide-error]').getByText('Synthetic server refusal reason.').waitFor();
    assert.equal(await page.locator('#dec-reason').inputValue(), reasonText);
    assert.equal(await page.locator('[data-review-check]').count(), 0, 'a definite refusal is not an uncertain outcome');
    await page.unroute(decisionRoute('Reject'));
    checks.push('refused answer shows the server reason and keeps the reason text');

    /* a lost response is uncertain: Check status re-reads; still unknown keeps the uncertainty; unchanged says so */
    await page.route(decisionRoute('Reject'), route => route.abort('failed'));
    await settled(page);
    await page.locator('[data-review-form="reject"] [type="submit"]').click();
    await page.locator('[data-review-check]').waitFor();
    await page.unroute(decisionRoute('Reject'));
    assert.equal(await page.locator('#dec-reason').inputValue(), reasonText);
    await page.route(readbackRoute, route => route.abort('failed'));
    await page.locator('[data-review-check]').click();
    await notice(page).getByText('We still couldn’t check').waitFor();
    await page.locator('[data-review-check]').waitFor();
    await page.unroute(readbackRoute);
    await page.locator('[data-review-check]').click();
    await notice(page).getByText('It wasn’t saved.').waitFor();
    assert.equal(await page.locator('[data-review-check]').count(), 0);
    checks.push('lost response: uncertain banner, Check status re-reads, still-unknown keeps the banner, unchanged state says it was not saved');

    /* a changed state read back after a lost response is shown as the current state */
    await settled(page);
    await page.route(decisionRoute('Reject'), route => route.abort('failed'));
    await page.locator('[data-review-form="reject"] [type="submit"]').click();
    await page.locator('[data-review-check]').waitFor();
    await page.unroute(decisionRoute('Reject'));
    const labels = await page.locator('[data-review-workspace]').evaluate(root => JSON.parse(root.dataset.statusLabels || '{}'));
    const current = await page.locator('[data-review-workspace]').evaluate(root => ({ version: Number(root.dataset.version), status: Number(root.dataset.statusCode) }));
    const other = Object.keys(labels).map(Number).find(code => code !== current.status);
    await page.route(readbackRoute, route => route.fulfill(json({ state: { version: current.version + 1, status: other } })));
    await page.locator('[data-review-check]').click();
    await notice(page).getByText('the submission is now ' + labels[other]).waitFor();
    await page.unroute(readbackRoute);
    checks.push('lost response: a changed read-back is announced as the current status');

    /* the leave dialog on an uncertain outcome: Escape keeps the page and the uncertainty */
    await open(page, pending[0]);
    await page.locator('[data-review-start="reject"]').click();
    await page.locator('#dec-reason').fill(reasonText);
    await page.route(decisionRoute('Reject'), route => route.abort('failed'));
    await page.locator('[data-review-form="reject"] [type="submit"]').click();
    await page.locator('[data-review-check]').waitFor();
    await page.unroute(decisionRoute('Reject'));
    const here = page.url();
    await page.locator('#back-btn, [data-review-back]').first().click();
    await page.locator('[data-confirm-title]').waitFor();
    assert.match(await page.locator('[data-confirm-title]').innerText(), /Leave before checking/);
    await page.keyboard.press('Escape');
    await page.locator('[data-confirm-title]').waitFor({ state: 'detached' });
    assert.equal(page.url(), here);
    assert.equal(await page.locator('[data-review-check]').count(), 1);
    assert.equal(await page.locator('#dec-reason').inputValue(), reasonText);
    checks.push('Escape on the leave dialog keeps the page, the reason and the uncertain banner');

    /* a lost session keeps the reason */
    await open(page, pending[1]);
    await page.locator('[data-review-start="reject"]').click();
    await page.locator('#dec-reason').fill(reasonText);
    await page.route(decisionRoute('Reject'), route => route.fulfill({ status: 200, contentType: 'text/html', headers: { 'X-Bingo-Post-Navigation': '/Account/Login' }, body: 'Sign in' }));
    await page.locator('[data-review-form="reject"] [type="submit"]').click();
    await page.getByRole('heading', { name: 'Your changes were not saved' }).waitFor();
    assert.equal(await page.locator('#dec-reason').inputValue(), reasonText);
    await page.getByRole('button', { name: 'Keep editing', exact: true }).click();
    await page.unroute(decisionRoute('Reject'));
    assert.equal(await page.locator('#dec-reason').inputValue(), reasonText);
    checks.push('lost session keeps the written reason');

    /* a stale correction keeps the mode, the reason and the picks (M1): a second tab saves first */
    const target = pending[2];
    await open(page, target);
    await page.locator('#dec-correct').click();
    await page.locator('[data-review-form="correct"]').waitFor();
    await accountsLoaded(page);
    const options = await page.locator('[data-cf-req] option').evaluateAll(list => list.map(option => option.value));
    const currentRequirement = await page.locator('[data-cf-req]').inputValue();
    const alternatives = options.filter(value => value !== currentRequirement);
    assert.ok(alternatives.length >= 2, 'the fixture offers two other objectives for a correction');
    const second = await context.newPage();
    await open(second, target);
    await second.locator('#dec-correct').click();
    await accountsLoaded(second);
    await second.locator('[data-cf-req]').selectOption(alternatives[0]);
    await second.locator('#cf-reason').fill('The other administrator corrected it first.');
    await second.locator('#cf-save').click();
    await second.locator('[data-review-workspace]').waitFor();
    await second.waitForFunction(req => document.querySelector('[data-review-workspace]')?.dataset.requirement === req, alternatives[0]);
    await second.close();
    const myPick = alternatives[1], myReason = 'My own reason for this correction.';
    const myAccount = await page.locator('[data-cf-acct]').inputValue();
    await page.locator('[data-cf-req]').selectOption(myPick);
    await page.locator('#cf-reason').fill(myReason);
    await page.locator('#cf-save').click();
    await notice(page).getByText('changed this submission while you were reviewing it').waitFor();
    await page.waitForFunction(() => !document.querySelector('[data-review-form="correct"]').hidden);
    await page.waitForFunction(pick => document.querySelector('[data-cf-req]').value === pick, myPick);
    assert.equal(await page.locator('#cf-reason').inputValue(), myReason);
    assert.equal(await page.locator('[data-cf-req]').inputValue(), myPick, 'the Objective pick survives the stale refresh');
    assert.notEqual(await page.locator('[data-review-workspace]').getAttribute('data-requirement'), myPick, 'the page now shows the other administrator\'s saved record');
    await page.waitForFunction(account => document.querySelector('[data-cf-acct]').value === account, myAccount);
    assert.equal(await page.locator('#cf-save').isEnabled(), true);
    checks.push('stale correction keeps the correction mode, reason and Objective / account picks after the refresh');

    assert.deepEqual(errors, []);
    console.log('PASS ' + checks.length + ' Review workspace flow groups [' + engine + ']');
  } finally {
    await browser?.close(); await fixture.close();
    fs.writeFileSync(path.join(out, 'checks.json'), JSON.stringify({ engine, checks }, null, 2));
  }
})().catch(error => { console.error(error); process.exitCode = 1; });

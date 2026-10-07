// T2 review M1 (RC10 C2, decision B): every unsaved part of the Catalogue drawer keeps its entry or asks, and a
// save of one part keeps the others' entries. Real page, controlled UR fixture, both engines.
const assert = require('node:assert/strict');
const path = require('node:path');
const { chromium, webkit } = require('playwright');
const { startFixture, login } = require('../../scripts/lib/admin-parity-fixture.cjs');
(async () => {
  const engine = process.env.PLAYWRIGHT_BROWSER === 'webkit' ? webkit : chromium;
  const fixture = await startFixture(process.cwd(), path.join(process.cwd(), 'artifacts/t2-catalogue-drafts-' + process.env.PLAYWRIGHT_BROWSER), { BINGO_PARITY_UR_PROFILE: 'live' });
  let browser;
  try {
    browser = await engine.launch({ headless: true, ...(engine === chromium ? { channel: process.env.PLAYWRIGHT_CHANNEL || 'chromium' } : {}) });
    const context = await browser.newContext({ viewport: { width: 1280, height: 860 }, reducedMotion: 'reduce' });
    await context.route(url => url.pathname.startsWith('/media/osrs-wiki'), route => route.fulfill({ status: 200, contentType: 'image/svg+xml', body: '<svg xmlns="http://www.w3.org/2000/svg" width="1" height="1"/>' }));
    const page = await login(context, fixture);
    const errors = []; page.on('pageerror', error => errors.push(error.message));
    await page.goto(fixture.origin + '/Admin/Catalogue?q=Zulrah');
    const open = page.locator('[data-activity-row]:not([hidden]) [data-catalogue-open]', { hasText: /^Zulrah$/ });
    await open.click();
    const drawer = page.locator('.drawer');
    await drawer.locator('#a-name').waitFor();
    const discard = page.locator('.modal[role=alertdialog]').last();
    const keepEditing = async () => { await discard.locator('button', { hasText: 'Keep editing' }).click(); await page.waitForFunction(() => document.querySelectorAll('.modal').length === 0); };
    const openEditor = async () => { if (!await drawer.locator('#e-name').count()) await drawer.locator('.dlist-row', { hasText: 'Tanzanite fang' }).click(); await drawer.locator('#e-name').waitFor(); };

    // 1. A typed Wise Old Man metric is protected on an outside click.
    await drawer.locator('#map-btn').click();
    await drawer.locator('#m-metric').fill('zulrah_typed');
    await page.mouse.click(120, 420);
    await discard.locator('.m-points', { hasText: 'Discards the Wise Old Man metric.' }).waitFor();
    await keepEditing();
    assert.equal(await drawer.locator('#m-metric').inputValue(), 'zulrah_typed', 'case 1: metric kept');

    // 2. The editor's Close asks when only value/mapping changed.
    await openEditor();
    await drawer.locator('#price-btn').click();
    await drawer.locator('#p-id').fill('99999');
    assert.equal(await drawer.locator('#e-cancel').innerText(), 'Close');
    await drawer.locator('#e-cancel').click();
    await discard.locator('.m-points', { hasText: 'Discards the value and mapping changes for Tanzanite fang.' }).waitFor();
    await keepEditing();
    assert.equal(await drawer.locator('#p-id').inputValue(), '99999', 'case 2: mapping entry kept, editor open');
    await drawer.locator('#p-id').fill(await drawer.locator('#p-id').getAttribute('value'));

    // 3. Saving the activity settings keeps an edited drop editor (and the typed metric).
    await drawer.locator('#e-rate').fill('2 x 1/700');
    await drawer.locator('#a-rate').fill('37');
    await drawer.locator('#dr-save').click();
    await page.locator('.toast', { hasText: 'Zulrah saved.' }).waitFor();
    assert.equal(await drawer.locator('#a-rate').inputValue(), '37');
    assert.equal(await drawer.locator('#e-rate').inputValue(), '2 x 1/700', 'case 3: drop entry kept after the settings save');
    assert.equal(await drawer.locator('#m-metric').inputValue(), 'zulrah_typed', 'case 3: metric kept after the settings save');

    // 4. Saving the drop keeps the unsaved metric.
    await drawer.locator('#e-save').click();
    await page.locator('.toast', { hasText: 'Tanzanite fang saved.' }).waitFor();
    assert.equal(await drawer.locator('#m-metric').inputValue(), 'zulrah_typed', 'case 4: metric kept after the drop save');

    // 5. "Save without validating" (value and mapping) keeps an unsaved rate.
    await openEditor();
    await drawer.locator('#e-rate').fill('2 x 1/800');
    await drawer.locator('#price-btn').click();
    await drawer.locator('#p-save').click();
    await drawer.locator('#p-result:not([hidden])').waitFor();
    assert.equal(await drawer.locator('#e-rate').inputValue(), '2 x 1/800', 'case 5: rate kept after the mapping save');
    // Leave the editor: the rate is still unsaved, so Escape asks, then discard everything.
    await page.keyboard.press('Escape');
    await discard.locator('button', { hasText: 'Discard' }).click();
    await page.waitForFunction(() => !document.querySelector('.drawer'));

    assert.deepEqual(errors, []);
    console.log('admin-design-catalogue-drafts: five unsaved-part cases passed');
  } finally {
    await browser?.close();
    await fixture.close();
  }
})().catch(error => { console.error(error); process.exitCode = 1; });

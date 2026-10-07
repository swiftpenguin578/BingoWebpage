// T2 review M2 (RC10 C1): "Check current values" reports Saved only when every submitted field matches.
// Real page, controlled UR fixture, both engines.
const assert = require('node:assert/strict');
const path = require('node:path');
const { chromium, webkit } = require('playwright');
const { startFixture, login } = require('../../scripts/lib/admin-parity-fixture.cjs');
(async () => {
  const engine = process.env.PLAYWRIGHT_BROWSER === 'webkit' ? webkit : chromium;
  const fixture = await startFixture(process.cwd(), path.join(process.cwd(), 'artifacts/t2-catalogue-readback-' + process.env.PLAYWRIGHT_BROWSER), { BINGO_PARITY_UR_PROFILE: 'live' });
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

    // M2: an unknown outcome is "Saved" only when every submitted field matches.
    await page.route(url => url.searchParams.get('handler') === 'UpdateBoss', route => route.abort());
    await drawer.locator('#a-team').fill('3');
    await drawer.locator('#dr-save').click();
    await drawer.locator('.banner', { hasText: 'We couldn’t confirm the save.' }).waitFor();
    await page.unrouteAll({ behavior: 'wait' });
    await drawer.locator('.banner button', { hasText: 'Check current values' }).click();
    await drawer.locator('.banner', { hasText: 'It wasn’t saved.' }).waitFor();
    assert.equal(await drawer.locator('#a-team').inputValue(), '3', 'M2: team size (not name/category) differs: kept, not "Saved"');
    // An applied save whose answer was lost: every field matches, so it is reported as saved.
    await page.route(url => url.searchParams.get('handler') === 'UpdateBoss', async route => { await route.fetch(); await route.abort(); });
    await drawer.locator('#a-team').fill('4');
    await drawer.locator('#a-img').fill('https://oldschool.runescape.wiki/w/File:Zulrah_(serpentine).png');
    await drawer.locator('#dr-save').click();
    await drawer.locator('.banner', { hasText: 'We couldn’t confirm the save.' }).waitFor();
    await page.unrouteAll({ behavior: 'wait' });
    await drawer.locator('.banner button', { hasText: 'Check current values' }).click();
    await drawer.locator('.banner', { hasText: 'Saved.' }).waitFor();
    assert.equal(await drawer.locator('#a-team').inputValue(), '4');
    assert.equal(await drawer.locator('#dr-cancel').innerText(), 'Close', 'the saved values are the baseline');

    await openEditor();
    await page.route(url => url.searchParams.get('handler') === 'UpdateDrop', route => route.abort());
    await drawer.locator('#e-img').fill('https://oldschool.runescape.wiki/images/Not_saved_image.png');
    await drawer.locator('#e-save').click();
    await drawer.locator('.banner', { hasText: 'We couldn’t confirm the save.' }).waitFor();
    await page.unrouteAll({ behavior: 'wait' });
    await drawer.locator('.banner button', { hasText: 'Check current values' }).click();
    await drawer.locator('.banner', { hasText: 'It wasn’t saved.' }).waitFor();
    assert.equal(await drawer.locator('#e-img').inputValue(), 'https://oldschool.runescape.wiki/images/Not_saved_image.png', 'M2: image (not rate/name) differs: kept, not "Saved"');
    await drawer.locator('#e-cancel').click();

    assert.deepEqual(errors, []);
    console.log('admin-design-catalogue-readback: full-tuple readback passed');
  } finally {
    await browser?.close();
    await fixture.close();
  }
})().catch(error => { console.error(error); process.exitCode = 1; });

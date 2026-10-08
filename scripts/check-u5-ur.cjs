// U5 Participants: the review scenarios (UR "live" profile, ReviewAdmin) render what the scenario list promises.
// Writes screenshots and a result list to docs/references/admin-ui/reviews/2026-10-08/u5/ur/.
const assert = require('node:assert/strict');
const fs = require('node:fs'), path = require('node:path');
const { chromium, webkit } = require('playwright');
const { startFixture, login } = require('./lib/admin-parity-fixture.cjs');
const root = process.cwd(), output = path.join(root, 'docs/references/admin-ui/reviews/2026-10-08/u5/ur');
(async () => {
  fs.mkdirSync(output, { recursive: true });
  const fixture = await startFixture(root, path.join(root, 'artifacts/u5-ur'), { BINGO_PARITY_UR_PROFILE: 'live' });
  const engine = process.env.PLAYWRIGHT_BROWSER === 'webkit' ? webkit : chromium;
  const results = []; let browser;
  try {
    browser = await engine.launch({ headless: true, ...(engine === chromium ? { channel: process.env.PLAYWRIGHT_CHANNEL || 'chromium' } : {}) });
    const context = await browser.newContext({ viewport: { width: 1280, height: 860 }, reducedMotion: 'reduce' });
    const page = await login(context, fixture), errors = [];
    page.on('pageerror', error => errors.push(error.message));
    page.setDefaultTimeout(15000);
    const open = fixture.events['ur-signups-open'], base = fixture.origin + '/Admin/Events/Participants/';
    const check = async (name, url, ready, assertions) => {
      await page.goto(url); await page.locator(ready).first().waitFor();
      if (assertions) await assertions();
      await page.screenshot({ path: path.join(output, name + '.png') }); results.push(name);
    };
    await check('list-full', base + open, '[data-participant-row]', async () => {
      assert.match(await page.locator('.page-head .summary').innerText(), /4 of 4 confirmed.*2 waiting.*\d+ unpaid/s);
      assert.equal(await page.locator('[data-participant-row]').count(), 4);
      assert.equal(await page.locator('#add-btn').isEnabled(), true);
    });
    await check('list-waiting', base + open + '?tab=waiting', '[data-participant-row]', async () => assert.equal(await page.locator('[data-participant-row]').count(), 2));
    await check('list-withdrawn', base + open + '?tab=withdrawn', '[data-participant-row]', async () => assert.equal(await page.locator('[data-participant-row]').count(), 1));
    await check('list-search-none', base + open + '?q=nobody-here&pay=paid', '.empty-title', async () => assert.equal(await page.locator('.empty-title').innerText(), 'No matches'));
    // Rows menu on a waiting participant of a full event.
    await page.goto(base + open + '?tab=waiting'); await page.locator('[data-participant-menu]').first().click();
    await page.locator('#participant-menu').getByText('Confirm and add a place').waitFor();
    assert.equal(await page.locator('#participant-menu').getByText('Move up').count(), 0, 'S13: no queue reordering');
    await page.screenshot({ path: path.join(output, 'menu-waiting.png') }); results.push('menu-waiting'); await page.keyboard.press('Escape');
    // Drawer for the first confirmed participant (id from the list), waiting and withdrawn.
    const idOf = async tab => { await page.goto(base + open + '?tab=' + tab); const link = page.locator('[data-participant-open]').first(); await link.waitFor(); return link.getAttribute('data-participant-open'); };
    const confirmed = await idOf('confirmed'), waiting = await idOf('waiting'), withdrawn = await idOf('withdrawn');
    await check('drawer-confirmed', base + open + '?participant=' + confirmed, '.drawer [data-d-content]:not([hidden])', async () => {
      assert.equal(await page.locator('.drawer [data-d-note]').inputValue(), 'Synthetic private note: paid in game.');
    });
    await check('drawer-waiting', base + open + '?participant=' + waiting, '.drawer [data-d-content]:not([hidden])');
    await check('drawer-withdrawn', base + open + '?participant=' + withdrawn, '.drawer [data-d-withdrawn]:not([hidden])');
    await check('drawer-missing', base + open + '?participant=00000000-0000-0000-0000-000000000001', '.drawer .empty-title');
    await check('add-drawer', base + open + '?add=1', '.drawer #add-search', async () => {
      await page.locator('#add-search').fill('website');
      await page.locator('#add-results button.res', { hasText: '@ReviewWebsite' }).click();
      await page.locator('[data-ad-picked]:not([hidden])').waitFor();
      assert.equal(await page.locator('[data-ad-place][value="waiting"]').isChecked(), true, 'full event: waiting list is the default');
    });
    // Old URL redirect.
    await page.goto(fixture.origin + `/Admin/Events/Participant/${open}/Participants/${confirmed}`);
    await page.waitForFunction(id => new URL(location.href).searchParams.get('participant') === id, confirmed); results.push('old-url-redirect');
    // Read-only and terminal events (Cancelled, Archived, Live): the page opens; the roster is locked.
    for (const slug of ['ur-cancelled', 'ur-archived', 'ur-current']) {
      await check('readonly-' + slug, base + fixture.events[slug], '.page-head', async () => {
        assert.equal(await page.locator('#add-btn').isDisabled(), true, slug + ': Add is disabled');
        assert.match(await page.locator('.lock-chip').innerText(), /Roster locked/);
      });
    }
    assert.deepEqual(errors, [], 'no page errors');
    fs.writeFileSync(path.join(output, 'results.txt'), results.join('\n') + '\n');
    console.log('PASS U5 review scenarios (' + (engine === webkit ? 'WebKit' : 'Chromium') + '): ' + results.join(', '));
  } finally { if (browser) await browser.close(); await fixture.close(); }
})().catch(error => { console.error(error); process.exit(1); });

// U6 Teams / Draft: review scenarios on the controlled parity fixture (setup roster from
// BINGO_PARITY_PARTICIPANTS, running draft and Final review event from BINGO_PARITY_DRAFT).
// Writes screenshots and a result list to docs/references/admin-ui/reviews/2026-10-08/u6/ur/.
// Hidden events answer 404 (server tests, C-CMP-1); no screenshot.
const assert = require('node:assert/strict');
const fs = require('node:fs'), path = require('node:path');
const { chromium, webkit } = require('playwright');
const { startFixture, login } = require('./lib/admin-parity-fixture.cjs');
const root = process.cwd(), output = path.join(root, 'docs/references/admin-ui/reviews/2026-10-08/u6/ur');
(async () => {
  fs.mkdirSync(output, { recursive: true });
  const fixture = await startFixture(root, path.join(root, 'artifacts/u6-ur'), { BINGO_PARITY_PARTICIPANTS: '1', BINGO_PARITY_DRAFT: '1' });
  const engine = process.env.PLAYWRIGHT_BROWSER === 'webkit' ? webkit : chromium;
  const results = []; let browser;
  try {
    browser = await engine.launch({ headless: true, ...(engine === chromium ? { channel: process.env.PLAYWRIGHT_CHANNEL || 'chromium' } : {}) });
    const context = await browser.newContext({ viewport: { width: 1280, height: 860 }, reducedMotion: 'reduce' });
    const page = await login(context, fixture), errors = [];
    page.on('pageerror', error => errors.push(error.message));
    page.setDefaultTimeout(15000);
    const url = slug => fixture.origin + '/Admin/Events/Draft/' + fixture.events[slug];
    const shot = async name => { await page.screenshot({ path: path.join(output, name + '.png') }); results.push(name); };
    const banner = page.locator('#td-banner');

    await page.goto(url('autumn-bingo-2027')); await page.locator('.tcard').first().waitFor();
    await shot('setup-signups-open');
    await page.locator('.tcard .tcard-head .icon-btn').first().click(); await page.locator('#draft-menu .menu-item').first().waitFor();
    await shot('setup-team-menu'); await page.keyboard.press('Escape');

    await page.goto(url('clan-cup-pvm-week')); await page.locator('.td-live').waitFor();
    await shot('running-no-control');
    await page.locator('#ctl-btn').click(); await page.locator('.ctl', { hasText: 'You have control' }).waitFor();
    await page.locator('.pchip').first().click(); await page.locator('.dmem:not(.is-pending) .dmem-tag', { hasText: '#1' }).waitFor();
    await shot('running-in-control-after-pick');
    await page.locator('#draft-more').click(); await page.locator('#draft-menu .menu-item').first().waitFor();
    await shot('running-more-menu'); await page.keyboard.press('Escape');
    await page.route('**/Admin/Events/Draft/*?handler=Pick', async route => { await route.fetch(); await route.abort(); }, { times: 1 });
    await page.locator('.pchip').first().click(); await banner.filter({ hasText: 'We couldn’t confirm the response.' }).waitFor();
    await shot('running-uncertain-pick');
    await page.locator('#draft-more').click(); await page.locator('#draft-menu .menu-item', { hasText: 'Cancel draft…' }).click();
    await page.locator('.modal .m-title', { hasText: 'Undo picks before cancelling' }).waitFor();
    await shot('running-cancel-blocked'); await page.locator('#cx-cancel').click();
    for (let left = await page.locator('.pchip').count(); left > 0; left--) {
      const name = (await page.locator('.pchip .pchip-name').first().innerText()).trim();
      await page.locator('.pchip').first().click(); await page.locator('.dmem:not(.is-pending)', { hasText: name }).waitFor();
    }
    await shot('running-complete');
    await page.locator('#turn-primary').click(); await page.locator('.modal .m-title', { hasText: 'Finalize the draft?' }).waitFor();
    await shot('finalize-confirmation');
    await page.locator('#cx-confirm').click(); await page.locator('[data-toast-host]', { hasText: 'Rosters and draft results published.' }).waitFor();
    await shot('finalized-toast');
    await page.locator('.tcard .tmem .icon-btn').first().click(); await page.locator('#draft-menu .menu-item', { hasText: 'Remove from team…' }).click();
    await page.locator('.modal .m-title').waitFor();
    await shot('finalized-remove-dialog'); await page.locator('#cx-cancel').click();
    await page.locator('.tcard button', { hasText: 'Add member' }).first().click(); await page.locator('.dr-title', { hasText: 'Add to the published roster' }).waitFor();
    await shot('finalized-correction-add'); await page.locator('#ps-cancel').click();

    for (const [slug, name, text] of [['midsummer-skilling-sprint', 'live-locked', 'Rosters are locked.'], ['draft-review', 'final-review-uploads-closed', 'Rosters are final.'], ['spring-finalized', 'terminal-finished', 'This event is finished.'], ['spring-cancelled', 'terminal-cancelled', 'This event is cancelled.'], ['spring-archived', 'terminal-archived', 'This event is archived.']]) {
      await page.goto(url(slug)); await banner.filter({ hasText: text }).waitFor(); await shot(name);
    }
    // Danish (read-only view).
    await page.locator('[name=culture][value=da]').click(); await page.waitForFunction(() => document.documentElement.lang === 'da');
    await page.goto(url('spring-finalized')); await banner.waitFor(); await shot('terminal-finished-da');
    await page.locator('[name=culture][value=en]').click(); await page.waitForFunction(() => document.documentElement.lang === 'en');
    assert.deepEqual(errors, [], 'no page errors');
    fs.writeFileSync(path.join(output, 'results.txt'), results.map(name => 'PASS ' + name).join('\n') + '\n');
    console.log('PASS U6 review scenarios: ' + results.length + ' (' + (process.env.PLAYWRIGHT_BROWSER || 'chromium') + ')');
  } finally { await browser?.close(); await fixture.close(); }
})().catch(error => { console.error(error); process.exitCode = 1; });

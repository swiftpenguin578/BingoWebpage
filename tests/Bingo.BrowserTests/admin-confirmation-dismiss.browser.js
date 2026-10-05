// Brief45: a completed stale result may dismiss without success; pending user
// dismissal and ordinary failure still preserve the existing protection/draft.
const assert = require('node:assert/strict');
const fs = require('node:fs');
const { chromium } = require('playwright');
const partial = fs.readFileSync('src/Bingo.Web/Pages/Shared/_AdminConfirmation.cshtml', 'utf8').replace(/@T\["([^"]+)"\]/g, '$1');
(async () => {
  const browser = await chromium.launch({ headless: true, channel: process.env.PLAYWRIGHT_CHANNEL || 'chrome' });
  try {
    const page = await browser.newPage();
    const errors = [];
    page.on('pageerror', error => errors.push(error.message));
    await page.setContent(`<dialog id="editor"><input value="retained"><button id="opener">Confirm</button></dialog>${partial}`);
    await page.addScriptTag({ path: 'src/Bingo.Web/wwwroot/js/admin-confirmation.js' });
    const dialog = page.locator('[data-admin-confirmation]');
    const editor = page.locator('#editor');
    await page.evaluate(() => {
      document.querySelector('#editor').showModal();
      window.answer = null;
      window.calls = 0;
      window.adminConfirmation.open({ title: 'Change account?', description: 'Review the current account.', actionLabel: 'Confirm', requireReason: true,
        opener: document.querySelector('#opener'),
        onConfirm: () => { window.calls++; return new Promise(resolve => { window.settle = resolve; }); }
      }).then(answer => { window.answer = answer; });
    });
    await dialog.locator('textarea').fill('Retained reason');
    await dialog.locator('[data-admin-confirmation-action]').click();
    assert.equal(await page.evaluate(() => window.adminConfirmation.cancel()), false, 'API cancellation stays refused while pending');
    await page.keyboard.press('Escape');
    await page.evaluate(() => window.dispatchEvent(new PopStateEvent('popstate')));
    assert.equal(await dialog.evaluate(element => element.open), true, 'Escape and Back do not dismiss pending request');
    assert.equal(await editor.evaluate(element => element.open), false, 'editor stays suspended while pending');
    assert.equal(await page.evaluate(() => window.answer), null);
    await page.evaluate(() => window.settle({ succeeded: false, message: 'Try again.' }));
    await dialog.locator('[data-admin-confirmation-feedback]').waitFor({ state: 'visible' });
    assert.equal(await dialog.evaluate(element => element.open), true, 'ordinary failure keeps confirmation open');
    assert.equal(await dialog.locator('textarea').inputValue(), 'Retained reason');
    assert.equal(await dialog.locator('[data-admin-confirmation-feedback]').textContent(), 'Try again.');
    assert.equal(await page.evaluate(() => window.answer), null, 'ordinary failure does not resolve dismissal');
    await dialog.locator('[data-admin-confirmation-action]').click();
    await page.evaluate(() => window.settle({ succeeded: false, dismiss: true }));
    await dialog.waitFor({ state: 'hidden' });
    assert.equal(await page.evaluate(() => window.answer), false, 'dismissal never reports success');
    assert.equal(await editor.evaluate(element => element.open), true, 'dismissal resumes suspended editor');
    assert.equal(await page.locator('#opener').evaluate(element => document.activeElement === element), true, 'opener focus returns');
    assert.equal(await editor.locator('input').inputValue(), 'retained');
    assert.equal(await page.evaluate(() => window.calls), 2);
    assert.deepEqual(errors, []);
    console.log('PASS completed dismissal/resume/focus/false, ordinary failure draft, pending cancel/Escape/Back refusal');
  } finally { await browser.close(); }
})().catch(error => { console.error(error); process.exitCode = 1; });

const assert = require('node:assert/strict');
const fs = require('node:fs');
const { chromium, webkit } = require('playwright');
(async () => {
  const browser = await (process.env.PLAYWRIGHT_BROWSER === 'webkit' ? webkit.launch({headless:true}) : chromium.launch({ headless: true, channel: process.env.PLAYWRIGHT_CHANNEL || 'chrome' }));
  try {
    const page = await browser.newPage({ colorScheme: 'dark' });
    const script = fs.readFileSync('src/Bingo.Web/wwwroot/js/admin-design-theme.js', 'utf8');
    await page.route('https://bingo.test/**', route => route.fulfill({ contentType: 'text/html', body: `<!doctype html><html class="dk-theme"><head><script>${script}</script><script>window.beforeBodyDark=document.documentElement.classList.contains('theme-dark');</script></head><body><button data-theme="light">Light</button><button data-theme="dark">Dark</button></body></html>` }));
    await page.goto('https://bingo.test/');
    assert.equal(await page.evaluate(() => window.beforeBodyDark), true, 'OS theme applied before body/first paint');
    await page.getByText('Light', { exact: true }).click();
    assert.equal(await page.locator('html').evaluate(el => el.classList.contains('theme-dark')), false);
    await page.reload();
    assert.equal(await page.evaluate(() => window.beforeBodyDark), false, 'manual choice persists before first paint');
    await page.emulateMedia({ colorScheme: 'light' });
    await page.getByText('Dark', { exact: true }).click();
    await page.emulateMedia({ colorScheme: 'dark' });
    await page.emulateMedia({ colorScheme: 'light' });
    assert.equal(await page.locator('html').evaluate(el => el.classList.contains('theme-dark')), true, 'manual choice wins over OS changes');
    assert.equal(await page.locator('[data-theme="dark"]').getAttribute('aria-pressed'), 'true');
    console.log('PASS admin theme OS default, pre-paint manual persistence and accessible selection');
  } finally { await browser.close(); }
})().catch(error => { console.error(error); process.exitCode = 1; });

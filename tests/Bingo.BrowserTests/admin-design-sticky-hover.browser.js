// U10: the sticky first column fades its hover tint with the row (no instant drop to the surface colour on leaving a row).
const assert = require('node:assert/strict'), fs = require('node:fs'), path = require('node:path');
const { chromium, webkit } = require('playwright');
(async () => {
  const engine = process.env.PLAYWRIGHT_BROWSER === 'webkit' ? webkit : chromium;
  const browser = await engine.launch({ headless: true, ...(engine === chromium ? { channel: process.env.PLAYWRIGHT_CHANNEL || 'chromium' } : {}) });
  try {
    const css = ['tokens', 'components', 'layout'].map(name => fs.readFileSync(path.join('src/Bingo.Web/wwwroot/css', `admin-design-${name}.css`), 'utf8')).join('\n');
    assert.match(css, /--sticky-tint/, 'layout CSS carries the sticky hover tint rule');
    const page = await browser.newPage({ viewport: { width: 600, height: 400 } });
    // Same structure as the Events/Accounts directories: a horizontally scrolling table with a sticky name column.
    await page.setContent(`<!doctype html><html><head><style>${css}</style></head><body data-admin-design class="dk-theme"><div class="tbl-wrap is-scroll" style="width:300px;overflow:auto"><div class="tbl sticky-first" style="width:900px"><div class="row" id="r" style="display:flex"><div class="td c-name" id="c" style="width:200px">Name</div><div class="td" style="width:700px">Other</div></div></div></div></body></html>`);
    const bg = () => page.evaluate(() => getComputedStyle(document.getElementById('c')).backgroundColor + '|' + getComputedStyle(document.getElementById('c')).backgroundImage);
    const resting = await bg();
    await page.hover('#r'); await page.waitForTimeout(400);
    const hovered = await page.evaluate(() => getComputedStyle(document.getElementById('c')).getPropertyValue('--sticky-tint').trim());
    assert.notEqual(hovered, 'transparent', 'hover sets the tint on the sticky cell');
    await page.mouse.move(500, 380);
    const running = await page.evaluate(() => document.getElementById('c').getAnimations().some(a => a.transitionProperty === '--sticky-tint'));
    assert.equal(running, true, 'leaving the row starts a fade on the sticky cell');
    await page.waitForTimeout(400);
    assert.equal(await bg(), resting, 'the cell returns to its resting background');
    console.log('PASS sticky first column fades its hover tint with the row');
  } finally { await browser.close(); }
})().catch(error => { console.error(error); process.exitCode = 1; });

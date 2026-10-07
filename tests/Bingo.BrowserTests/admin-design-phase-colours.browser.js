// Brief78: measured sRGB text contrast, including translucent badges over both host surfaces.
const assert = require('node:assert/strict');
const fs = require('node:fs');
const { chromium, webkit } = require('playwright');
const rgb = value => value.match(/[\d.]+/g).map(Number);
const luminance = values => values.slice(0, 3).map(value => {
  const channel = value / 255;
  return channel <= .04045 ? channel / 12.92 : ((channel + .055) / 1.055) ** 2.4;
}).reduce((sum, value, i) => sum + value * [.2126, .7152, .0722][i], 0);
const contrast = (a, b) => (Math.max(luminance(a), luminance(b)) + .05) / (Math.min(luminance(a), luminance(b)) + .05);
(async () => {
  const engine = process.env.PLAYWRIGHT_BROWSER || 'chromium';
  const browser = await (engine === 'webkit' ? webkit : chromium).launch({ headless: true });
  const results = [];
  try {
    const page = await browser.newPage();
    const css = ['tokens', 'components'].map(name => fs.readFileSync(`src/Bingo.Web/wwwroot/css/admin-design-${name}.css`, 'utf8')).join('\n');
    for (const theme of ['light', 'dark']) {
      await page.setContent(`<html class="dk-theme ${theme === 'dark' ? 'theme-dark' : ''}"><head><style>${css}</style></head><body><div style="background:var(--dk-bg)" id="page"></div><div style="background:var(--dk-surface)" id="surface"></div></body></html>`);
      for (const [tone, dot] of [['info', 'closed'], ['done', 'done']]) {
        const values = await page.evaluate(({ tone, dot }) => {
          const span = document.createElement('span'); span.className = 'badge badge-' + tone; span.textContent = 'Phase'; document.body.append(span);
          const marker = document.createElement('span'); marker.className = 'dot tone-' + dot; document.body.append(marker);
          const style = getComputedStyle(span);
          const result = { foreground: style.color, background: style.backgroundColor, dot: getComputedStyle(marker).backgroundColor, page: getComputedStyle(document.querySelector('#page')).backgroundColor, surface: getComputedStyle(document.querySelector('#surface')).backgroundColor };
          span.remove(); marker.remove(); return result;
        }, { tone, dot });
        assert.equal(values.dot, values.foreground);
        const foreground = rgb(values.foreground), background = rgb(values.background), alpha = background[3] ?? 1;
        const ratios = {};
        for (const host of ['page', 'surface']) {
          const base = rgb(values[host]);
          ratios[host] = contrast(foreground, base);
          ratios['badgeOn' + host] = contrast(foreground, background.slice(0, 3).map((value, i) => alpha * value + (1 - alpha) * base[i]));
        }
        for (const [target, ratio] of Object.entries(ratios)) assert.ok(ratio >= 4.5, `${theme} ${tone} ${target}: ${ratio}`);
        results.push({ engine, theme, tone, values, ratios });
      }
      for (const [badge, dot] of [['neutral', 'draft'], ['success', 'open'], ['accent', 'live'], ['warning', 'review'], ['outline', 'draft']]) {
        const same = await page.evaluate(({ badge, dot }) => {
          const marker = document.createElement('span'); marker.className = 'dot tone-' + dot; document.body.append(marker);
          const label = document.createElement('span'); label.className = 'badge badge-' + badge; document.body.append(label);
          if (badge === 'neutral') label.className = 'badge badge-outline'; // Setup intentionally retains --dk-text-3.
          const expected = getComputedStyle(label).color;
          const same = getComputedStyle(marker).backgroundColor === expected;
          marker.remove(); label.remove(); return same;
        }, { badge, dot });
        assert.equal(same, true, `${theme} ${dot} matches badge foreground (Setup retains outline grey)`);
      }
    }
    fs.mkdirSync('artifacts/phase-colours', { recursive: true });
    fs.writeFileSync(`artifacts/phase-colours/${engine}-contrast.json`, JSON.stringify(results, null, 2));
    console.log(`PASS ${engine}: 16 contrast ratios >= 4.5:1; new and retained phase dot colours in both themes`);
  } finally { await browser.close(); }
})().catch(error => { console.error(error); process.exitCode = 1; });

// Standalone geometry check of the shared "!" icons with the real admin CSS. Usage: PLAYWRIGHT_BROWSER=chromium|webkit node measure.cjs <label>
const path = require('node:path'), fs = require('node:fs');
const { chromium, webkit } = require(path.resolve(__dirname, '../../node_modules/playwright'));
const { execFileSync } = require('node:child_process'), os = require('node:os');
const repo = path.resolve(__dirname, '../..'), before = process.env.BEFORE === '1';
const cssDir = before ? fs.mkdtempSync(path.join(os.tmpdir(), 'icon-before-css-')) : path.join(repo, 'src/Bingo.Web/wwwroot/css');
const show = rel => execFileSync('git', ['show', 'HEAD:' + rel], { cwd: repo, maxBuffer: 1 << 26 }).toString();
const cssFiles = ['admin-design-tokens.css', 'admin-design-components.css', 'admin-design-final-review.css', 'admin-design-wom.css'];
if (before) for (const f of cssFiles) fs.writeFileSync(path.join(cssDir, f), show('src/Bingo.Web/wwwroot/css/' + f));
const css = ['admin-design-tokens.css', 'admin-design-components.css', 'admin-design-final-review.css', 'admin-design-wom.css']
  .map(f => `<link rel="stylesheet" href="file://${path.join(cssDir, f)}">`).join('');
const icons = before ? show('src/Bingo.Web/Pages/Shared/_AdminDesignIcon.cshtml') : fs.readFileSync(path.join(repo, 'src/Bingo.Web/Pages/Shared/_AdminDesignIcon.cshtml'), 'utf8');
function svg(name) {
  const m = new RegExp('case "' + name + '":\\s*([\\s\\S]*?)\\s*break;').exec(icons);
  return `<svg class="ic" viewBox="0 0 16 16" aria-hidden="true" data-icon="${name}">${m[1]}</svg>`;
}
const cases = [
  ['check-mark mark-alert (Final review + WOM checklists)', () => `<ul style="list-style:none;margin:0;padding:0"><li class="check-item is-blocked"><span class="check-mark">${svg('mark-alert')}</span></li></ul>`],
  ['warning (WOM banner, Final review notes)', () => `<div class="banner is-warning">${svg('warning')}<span>x</span></div>`],
  ['error', () => `<div class="banner is-error">${svg('error')}<span>x</span></div>`],
  ['uncertain', () => `<div class="banner is-warning">${svg('uncertain')}<span>x</span></div>`],
];
(async () => {
  const engine = process.env.PLAYWRIGHT_BROWSER || 'chromium';
  const browser = await (engine === 'webkit' ? webkit : chromium).launch();
  const out = [], tmp = path.join(os.tmpdir(), 'icon-measure-' + process.pid + '.html');
  const DPR = 8;
  for (const dark of [false, true]) {
    const ctx = await browser.newContext({ deviceScaleFactor: DPR });
    const page = await ctx.newPage();
    for (const [label, mk] of cases) {
      fs.writeFileSync(tmp, `<!doctype html><html><head>${css}<style>*{background:transparent !important;border-color:transparent !important;box-shadow:none !important}</style></head><body><div class="dk-theme${dark ? ' theme-dark' : ''}" style="padding:20px;width:300px">${mk()}</div></body></html>`);
      await page.goto('file://' + tmp); await page.waitForLoadState('load');
      const host = await page.evaluate(() => { const v = document.querySelector('svg'), a = v.getBoundingClientRect(), c = v.closest('.check-mark') || v.parentElement, b = c.getBoundingClientRect(); return { w: a.width, left: a.left, dx: (a.left + a.width / 2) - (b.left + b.width / 2) }; });
      const buf = await page.locator('svg').first().screenshot({ omitBackground: true });
      const m = await page.evaluate(async b64 => {
        const img = new Image(); img.src = 'data:image/png;base64,' + b64; await img.decode();
        const c = document.createElement('canvas'); c.width = img.width; c.height = img.height; const g = c.getContext('2d'); g.drawImage(img, 0, 0);
        const d = g.getImageData(0, 0, c.width, c.height).data, w = c.width, h = c.height;
        // The mark and dot live in the central column (|x - centre| <= 1.6 css px); the triangle/circle outline sits further out.
        const centre = w / 2, rows = [], r = 1.6 * (w / (w / 8 === 0 ? 1 : 1));
        let sa = 0, sx = 0;
        for (let y = 0; y < h; y++) { let mx = 0; for (let x = 0; x < w; x++) { const a = d[(y * w + x) * 4 + 3]; if (Math.abs(x + .5 - centre) <= 1.6 * 8) { mx = Math.max(mx, a); sa += a; sx += a * (x + .5); } } rows.push(mx); }
        return { w, h, cx: sx / sa, rows };
      }, buf.toString('base64'));
      const inkRows = m.rows.map((v, i) => v > 40 ? i : -1).filter(i => i >= 0);
      let best = 0, run = 0; const first = inkRows[0], last = inkRows[inkRows.length - 1];
      // For triangle/circle icons the central column also holds nothing but the mark and dot (outline is outside it), except apex/base crossings; ignore runs touching the first/last ink row.
      for (let y = first; y <= last; y++) { if (m.rows[y] <= 40) { run++; best = Math.max(best, run); } else run = 0; }
      out.push({ engine, theme: dark ? 'dark' : 'light', label, svg: host.w + 'px', svgLeft: host.left, svgInHostDx: +host.dx.toFixed(2),
        markDotCentreOffsetPx: +((m.cx - m.w / 2) / DPR).toFixed(2), markToDotGapPx: +(best / DPR).toFixed(2), inkTopToBottomPx: +((last - first + 1) / DPR).toFixed(2) });
    }
    await ctx.close();
  }
  await browser.close();
  for (const r of out) console.log(JSON.stringify(r));
  fs.writeFileSync(path.resolve(__dirname, `measure-${process.argv[2] || 'run'}-${engine}.jsonl`), out.map(r => JSON.stringify(r)).join('\n') + '\n');
})();

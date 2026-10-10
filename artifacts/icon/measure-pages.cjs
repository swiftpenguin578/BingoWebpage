// Real-page geometry check (Admin Final review + WOM) on the owned parity fixture. Usage: PLAYWRIGHT_BROWSER=chromium|webkit node measure-pages.cjs <label>
const path = require('node:path'), fs = require('node:fs');
const root = path.resolve(__dirname, '../..');
const { chromium, webkit } = require(path.join(root, 'node_modules/playwright'));
const { startFixture, login } = require(path.join(root, 'scripts/lib/admin-parity-fixture.cjs'));
const label = process.argv[2] || 'run', engine = process.env.PLAYWRIGHT_BROWSER || 'chromium';
const out = path.join(__dirname, `pages-${label}-${engine}`); fs.mkdirSync(out, { recursive: true });
const alertInner = /case "mark-alert":\s*([\s\S]*?)\s*break;/.exec(fs.readFileSync(path.join(root, 'src/Bingo.Web/Pages/Shared/_AdminDesignIcon.cshtml'), 'utf8'))[1];
const ICONS = ['mark-alert', 'warning', 'error', 'uncertain'];
(async () => {
  const fixture = await startFixture(root, out, { BINGO_PARITY_UR_PROFILE: 'final-review', BINGO_PARITY_U9_READY: '1' });
  const browser = await (engine === 'webkit' ? webkit : chromium).launch({ headless: true });
  const rows = [], shot = {};
  try {
    for (const theme of ['light', 'dark']) {
      const context = await browser.newContext({ viewport: { width: 1280, height: 1000 }, deviceScaleFactor: 1, colorScheme: theme });
      const page = await login(context, fixture);
      await page.addInitScript(t => { try { localStorage.setItem('theme', t); localStorage.setItem('bingo-theme', t); } catch { } }, theme);
      for (const [pageName, base] of [['Finalize', '/Admin/Events/Finalize/'], ['WiseOldMan', '/Admin/Events/WiseOldMan/']]) {
        for (const [slug, id] of Object.entries(fixture.events).filter(([k]) => pageName === 'Finalize' ? ['ur-current', 'ur-archived'].includes(k) : ['ur-current', 'ur-archived', 'ur-upcoming-01', 'ur-signups-open', 'ur-wom-unavailable'].includes(k))) {
          await page.goto(fixture.origin + base + id); await page.waitForLoadState('networkidle').catch(() => { });
          if (theme === 'dark') await page.evaluate(() => { document.querySelectorAll('.dk-theme').forEach(e => e.classList.add('theme-dark')); });
          if (pageName === 'Finalize') await page.evaluate(inner => { // fixture has no blocked Final review; clone a real check item and give it the blocked state + shared mark-alert icon
            const li = document.querySelector('.check-item'); if (!li) return; const c = li.cloneNode(true); c.className = 'check-item is-blocked'; c.querySelector('svg').setAttribute('data-icon', 'mark-alert'); c.querySelector('svg').innerHTML = inner; li.parentElement.appendChild(c); }, alertInner);
          const f = await page.evaluate(names => { const s = [...document.querySelectorAll('svg[data-icon]')].find(v => names.includes(v.dataset.icon) && v.getBoundingClientRect().width > 0); if (!s) return null; s.setAttribute('data-measure', '1'); const r = s.getBoundingClientRect(), h = s.closest('.check-mark, .fr-pub-ic') || s.parentElement, hr = h.getBoundingClientRect(); return { icon: s.dataset.icon, left: +r.left.toFixed(2), w: r.width, dx: +((r.left + r.width / 2) - (hr.left + hr.width / 2)).toFixed(2), host: h.className }; }, ICONS);
          if (!f) continue;
          // Page-level DPR1 raster (not snapped to the element box): ink centroid relative to the icon box centre, plus the blank-row gap between mark and dot.
          const svgBox = await page.evaluate(() => { const r = document.querySelector('svg[data-measure]').getBoundingClientRect(); return { l: r.left, t: r.top + scrollY, w: r.width, h: r.height }; });
          const mid = Math.round(svgBox.l + svgBox.w / 2), x0 = mid - 6, y0 = Math.floor(svgBox.t) - 1, cw = 12, ch = Math.ceil(svgBox.h) + 3; // symmetric about the (integer) icon centre, inside the circle border
          const buf = await page.screenshot({ fullPage: true, clip: { x: x0, y: y0, width: cw, height: ch } });
          const m = await page.evaluate(async ({ b64, x0, cx }) => { const img = new Image(); img.src = 'data:image/png;base64,' + b64; await img.decode(); const c = document.createElement('canvas'); c.width = img.width; c.height = img.height; const g = c.getContext('2d'); g.drawImage(img, 0, 0); const d = g.getImageData(0, 0, c.width, c.height).data; const bg = [d[0], d[1], d[2]]; let sa = 0, sx = 0; const rowInk = []; for (let y = 0; y < c.height; y++) { let ri = 0; for (let x = 0; x < c.width; x++) { const i = (y * c.width + x) * 4; const a = Math.abs(d[i] - bg[0]) + Math.abs(d[i + 1] - bg[1]) + Math.abs(d[i + 2] - bg[2]); sa += a; sx += a * (x0 + x + .5); ri += a; } rowInk.push(ri); } return { cxInk: sx / sa, rowInk }; }, { b64: buf.toString('base64'), x0, cx: 0 });
          f.inkCentroidOffsetPx = +(m.cxInk - (svgBox.l + svgBox.w / 2)).toFixed(2);
          f.rowInk = m.rowInk.map(v => Math.round(v / 10)).join(',');
          if (!shot[pageName + theme]) { shot[pageName + theme] = 1; const box = await page.evaluate(() => { const s = document.querySelector('svg[data-measure]'); const c = s.closest('.banner, ul, .fr-pub-head') || s.parentElement; const r = c.getBoundingClientRect(); return { x: r.left, y: r.top + scrollY, width: r.width, height: r.height }; }); await page.screenshot({ path: path.join(out, `${pageName}-${theme}-${engine}-${label}.png`), fullPage: true, clip: { x: Math.max(0, box.x - 8), y: Math.max(0, box.y - 8), width: Math.min(box.width + 16, 700), height: Math.min(box.height + 16, 420) } }); }
          rows.push({ engine, theme, page: pageName, event: slug, ...f });
          if (pageName === 'Finalize' && shot[pageName + theme] && rows.filter(r => r.page === 'Finalize' && r.theme === theme).length >= 3) break;
        }
      }
      await context.close();
    }
  } finally { await browser.close(); await fixture.close(); }
  fs.writeFileSync(path.join(out, 'results.jsonl'), rows.map(r => JSON.stringify(r)).join('\n') + '\n');
  for (const r of rows) console.log(JSON.stringify(r));
})().catch(e => { console.error(e); process.exitCode = 1; });

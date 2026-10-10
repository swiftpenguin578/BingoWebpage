// Multi-boss tiles on the team board cross-fade one boss at a time (6 s per boss, 1.5 s fade, staggered per tile).
// Real site.public-ui.css and boss-art-fade.js against a synthetic board; the page clock is faked so no real 6 s waits. Both engines.
const assert = require('node:assert/strict'), fs = require('node:fs'), path = require('node:path');
const { chromium, webkit } = require('playwright');
const web = file => fs.readFileSync(path.resolve(__dirname, '../../src/Bingo.Web/wwwroot', file), 'utf8');
const sheets = ['lib/bootstrap/dist/css/bootstrap.min.css', 'css/site.transitional.foundation.css', 'css/site.public-ui.css', 'css/site.transitional.application.css'].map(web);
const script = web('js/boss-art-fade.js');
const swatch = colour => 'data:image/svg+xml,' + encodeURIComponent(`<svg xmlns="http://www.w3.org/2000/svg" width="80" height="80"><rect width="80" height="80" fill="${colour}"/></svg>`);
const colours = ['#c0392b', '#2980b9', '#27ae60', '#8e44ad'];
const art = (kind, n) => kind === 'own'
  ? `<img class="public-ui-team-board-tile__art-image" src="${swatch('#f39c12')}" alt="" loading="lazy"/>`
  : `<span class="public-ui-team-board-tile__boss-art-grid public-ui-team-board-tile__boss-art-grid--count-${n}">${colours.slice(0, n).map(c => `<img src="${swatch(c)}" alt="" loading="lazy"/>`).join('')}</span>`;
// Tile order: 0 three bosses, 1 one boss, 2 own image, 3 two bosses, 4 four bosses.
const tiles = [['boss', 3], ['boss', 1], ['own', 0], ['boss', 2], ['boss', 4]];
const html = `<div class="public-team-board" style="--public-board-columns:5"><div class="public-board-scroll"><div class="public-full-board">${tiles.map(([kind, n], i) =>
  `<a class="public-tile public-ui-nested-container public-ui-team-board-tile public-ui-team-board-tile--in-progress" href="#"><span class="public-ui-team-board-tile__number" aria-hidden="true">0${i + 1}</span><span class="public-ui-team-board-tile__art" aria-hidden="true">${art(kind, n)}</span><span class="public-ui-team-board-tile__shade" aria-hidden="true"></span><span class="public-ui-team-board-tile__copy"><strong class="public-ui-team-board-tile__title">Tile ${i + 1}</strong></span></a>`).join('')}</div></div></div>`;
const grids = [0, 3, 4], expectedCounts = { 0: 3, 3: 2, 4: 4 };

const snapshot = () => [...document.querySelectorAll('.public-ui-team-board-tile')].map(tile => {
  const grid = tile.querySelector('.public-ui-team-board-tile__boss-art-grid');
  return { active: grid ? grid.dataset.bossActive ?? null : null, fade: grid ? grid.hasAttribute('data-boss-fade') : false, activeClass: grid ? [...grid.querySelectorAll('img')].map(i => i.classList.contains('is-active')) : [], opacity: [...tile.querySelectorAll('img')].map(i => +getComputedStyle(i).opacity), transition: grid ? getComputedStyle(grid.querySelector('img')).transitionDuration : '' };
});
const setVisibility = state => page => page.evaluate(s => {
  Object.defineProperty(document, 'visibilityState', { configurable: true, get: () => s });
  Object.defineProperty(document, 'hidden', { configurable: true, get: () => s === 'hidden' });
  document.dispatchEvent(new Event('visibilitychange'));
}, state);
const open = async (browser, reducedMotion) => {
  const context = await browser.newContext({ viewport: { width: 1280, height: 900 }, reducedMotion: reducedMotion ? 'reduce' : 'no-preference' });
  const page = await context.newPage();
  await page.clock.install();
  await page.route('http://bingo.test/**', route => route.fulfill({ contentType: 'text/html', body: '<!doctype html><html lang="en"><head><meta name="viewport" content="width=device-width, initial-scale=1"></head><body></body></html>' }));
  await page.goto('http://bingo.test/');
  for (const content of sheets) await page.addStyleTag({ content });
  await page.evaluate(h => { document.body.className = 'public-event-shell public-ui-pass1'; document.body.innerHTML = h; }, html);
  await page.waitForFunction(() => [...document.images].every(i => i.complete));
  await page.clock.pauseAt(await page.evaluate(() => Date.now() + 1000)); // frozen from here: only runFor moves the clock
  await page.addScriptTag({ content: script });
  return { context, page };
};
const shots = path.join(process.cwd(), 'artifacts/boss-fade');
fs.mkdirSync(shots, { recursive: true });
const realWait = ms => new Promise(resolve => setTimeout(resolve, ms));

(async () => {
  for (const [name, engine] of [['chromium', chromium], ['webkit', webkit]]) {
    const browser = await engine.launch({ headless: true, ...(engine === chromium ? { channel: process.env.PLAYWRIGHT_CHANNEL || 'chromium' } : {}) });
    try {
      // Shared timing values.
      let { context, page } = await open(browser, false);
      assert.deepEqual(await page.evaluate(() => [window.BossArtFade.displayMs, window.BossArtFade.fadeMs]), [6000, 1500], `${name} 6 s per boss, 1.5 s fade`);
      // Start: exactly one boss on show per multi-boss tile (the first); single-boss and own-image tiles are not touched.
      let s = await page.evaluate(snapshot);
      for (const i of grids) {
        assert.equal(s[i].fade, true, `${name} tile ${i} fade started`);
        assert.equal(s[i].active, '0', `${name} tile ${i} starts on the first boss`);
        assert.equal(s[i].activeClass.length, expectedCounts[i], `${name} tile ${i} keeps every boss image in place`);
        assert.equal(s[i].opacity.filter(o => o === 1).length, 1, `${name} tile ${i} exactly one visible image at start: ${s[i].opacity}`);
        assert.equal(s[i].opacity[0], 1, `${name} tile ${i} first boss visible`);
      }
      assert.equal(s[1].fade, false, `${name} single-boss tile has no fade`); assert.deepEqual(s[1].opacity, [1], `${name} single-boss tile stays visible`);
      assert.deepEqual(s[2].opacity, [1], `${name} own-image tile unchanged`);
      // No layout shift: every stacked image has the same box as the grid, and tile boxes do not move across a switch.
      const boxes = () => page.evaluate(() => [...document.querySelectorAll('.public-ui-team-board-tile')].map(t => { const g = t.querySelector('.public-ui-team-board-tile__boss-art-grid'), r = e => { const b = e.getBoundingClientRect(); return [b.x, b.y, b.width, b.height].map(v => Math.round(v * 10) / 10).join(','); }; return { tile: r(t), imgs: g ? [...g.querySelectorAll('img')].map(r) : [] }; }));
      const before = await boxes();
      await page.locator('.public-ui-team-board-tile').nth(0).screenshot({ path: path.join(shots, `team-board-3-boss-tile-before-${name}.png`) });
      for (const i of grids) assert.equal(new Set(before[i].imgs).size, 1, `${name} tile ${i} stacked images share one box ${before[i].imgs}`);
      // Stagger: tile k first switches at 6000 + (k % 4) * 700 + floor(k / 4) * 250 ms -> tile 0 at 6000, 3 at 8100, 4 at 6250.
      await page.clock.runFor(5999); s = await page.evaluate(snapshot);
      assert.deepEqual(grids.map(i => s[i].active), ['0', '0', '0'], `${name} nothing switches before 6 s`);
      await page.clock.runFor(2); s = await page.evaluate(snapshot);
      assert.deepEqual(grids.map(i => s[i].active), ['1', '0', '0'], `${name} tile 0 switches at 6 s, the others have not (staggered)`);
      await page.clock.runFor(300); s = await page.evaluate(snapshot);
      assert.deepEqual(grids.map(i => s[i].active), ['1', '0', '1'], `${name} tile 4 switches ~250 ms later`);
      await page.clock.runFor(1900); s = await page.evaluate(snapshot);
      assert.deepEqual(grids.map(i => s[i].active), ['1', '1', '1'], `${name} tile 3 switches at 8.1 s`);
      // The cross-fade itself is CSS (real time, 1.5 s): after it, exactly one image is visible and it is the new boss.
      await realWait(1800); s = await page.evaluate(snapshot);
      for (const i of grids) {
        assert.equal(s[i].opacity.filter(o => o === 1).length, 1, `${name} tile ${i} exactly one visible image after the fade: ${s[i].opacity}`);
        assert.equal(s[i].opacity[1], 1, `${name} tile ${i} second boss visible`);
        assert.equal(s[i].transition, '1.5s', `${name} tile ${i} fade is 1.5 s opacity`);
      }
      await page.locator('.public-ui-team-board-tile').nth(0).screenshot({ path: path.join(shots, `team-board-3-boss-tile-after-${name}.png`) });
      assert.deepEqual(await boxes(), before, `${name} switching moves nothing`);
      // Loops in tile order: tile 0 has three bosses, so 6 s -> second, 12 s -> third, 18 s -> first again.
      await page.clock.runFor(12000 - (5999 + 2 + 300 + 1900)); // 12 s since start
      s = await page.evaluate(snapshot); assert.equal(s[0].active, '2', `${name} tile 0 third boss at 12 s`);
      await page.clock.runFor(6000); s = await page.evaluate(snapshot); assert.equal(s[0].active, '0', `${name} tile 0 loops to the first boss at 18 s`);
      // Hidden tab pauses; resuming continues cleanly.
      const hiddenAt = await page.evaluate(snapshot);
      await setVisibility('hidden')(page); await page.clock.runFor(60000);
      s = await page.evaluate(snapshot); assert.deepEqual(s.map(x => x.active), hiddenAt.map(x => x.active), `${name} hidden tab: no switching`);
      await setVisibility('visible')(page); await page.clock.runFor(10);
      s = await page.evaluate(snapshot); assert.deepEqual(s.map(x => x.active), hiddenAt.map(x => x.active), `${name} resuming does not jump straight to the next boss`);
      await page.clock.runFor(6000); s = await page.evaluate(snapshot);
      for (const i of grids) assert.notEqual(s[i].active, hiddenAt[i].active, `${name} tile ${i} continues after resume`);
      await context.close();

      // Reduced motion: no fade, first boss static.
      ({ context, page } = await open(browser, true));
      await page.clock.runFor(120000); s = await page.evaluate(snapshot);
      for (const i of grids) {
        assert.equal(s[i].active, '0', `${name} reduced motion tile ${i} stays on the first boss`);
        assert.equal(s[i].opacity.filter(o => o === 1).length, 1, `${name} reduced motion tile ${i} one visible image`);
        assert.equal(s[i].opacity[0], 1);
        assert.ok(parseFloat(s[i].transition) < 0.001, `${name} reduced motion: no fade transition (${s[i].transition})`);
      }
      await context.close();
      console.log(`PASS ${name} team board boss fade: one boss at a time, 6 s / 1.5 s, staggered, loops, paused when hidden, reduced motion static, no layout shift`);
    } finally { await browser.close(); }
  }
})().catch(error => { console.error(error); process.exitCode = 1; });

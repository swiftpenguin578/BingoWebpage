// Board Preview modal shows the board as players see it (brief 148): tile art, names, "Points: N",
// grid size and order for the version being viewed; every tile in its not-started state; no
// sidebar/progress/switcher; tiles are not interactive. The fixture board is rewritten in flight
// (data-view only) so draft and published, 3x3 and 5x5 are deterministic. Real page, both engines.
const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const { chromium, webkit } = require('playwright');
const { startFixture, login } = require('../../scripts/lib/admin-parity-fixture.cjs');

(async () => {
  const name = process.env.PLAYWRIGHT_BROWSER || 'chromium';
  const engine = name === 'webkit' ? webkit : chromium;
  const shots = path.join(process.cwd(), 'artifacts/board-preview');
  fs.mkdirSync(shots, { recursive: true });
  const fixture = await startFixture(process.cwd(), path.join(process.cwd(), 'artifacts/board-preview-' + name));
  let browser;
  try {
    browser = await engine.launch({ headless: true, ...(engine === chromium ? { channel: process.env.PLAYWRIGHT_CHANNEL || 'chromium' } : {}) });
    const boardPath = '/Admin/Events/Board/' + fixture.events['autumn-bingo-2027'];
    const names = ['Vorkath', 'Chambers of Xeric', 'Araxxor', 'Zulrah', 'Tombs of Amascut', 'Nex', 'Barrows', 'Gauntlet', 'Hespori', 'Inferno', 'Wintertodt', 'Tempoross', 'Phantom Muspah', 'Duke Sucellus', 'Leviathan', 'Whisperer', 'Vardorvis', 'Corp', 'Kraken', 'Cerberus', 'Sarachnis', 'Zalcano', 'Mimic', 'Scurrius', 'Hunllef'];
    const scenarios = [
      { mode: 'draft', rows: 3, cols: 3, positions: [0, 1, 2, 3, 4, 5, 6, 7, 8], eyebrow: /current draft values/ },
      { mode: 'published', rows: 5, cols: 5, positions: [...Array(25).keys()].filter(p => p !== 12), eyebrow: /what players see now/ },
    ];
    // Boss fade (brief 153): every scenario runs with reduced motion (first boss static); one extra run keeps motion on with a frozen page clock.
    const runs = [...[false, true].flatMap(dark => scenarios.map(scenario => ({ dark, scenario, motion: false }))), { dark: false, scenario: scenarios[0], motion: true }];
    for (const { dark, scenario, motion } of runs) {
      const context = await browser.newContext({ viewport: { width: 1280, height: 860 }, reducedMotion: motion ? 'no-preference' : 'reduce', colorScheme: dark ? 'dark' : 'light' });
      const page = await login(context, fixture);
      if (motion) await page.clock.install();
      const errors = []; page.on('pageerror', error => errors.push(error.message));
      // Rewrite the page's own view data before its script reads it (no network interception: WebKit drops subresources of fulfilled documents).
      await page.addInitScript(({ scenario, names }) => {
        const patch = () => {
          const root = document.querySelector('[data-board]');
          if (!root || root.dataset.pvPatched) return !!root;
          const view = JSON.parse(root.dataset.view);
          Object.assign(view, { mode: scenario.mode, rows: scenario.rows, cols: scenario.cols, readOnly: false });
          view.tiles = scenario.positions.map((pos, i) => {
          const bosses = ['/images/public-board/bosses/vorkath.png', '/images/public-board/bosses/araxxor.png', '/images/public-board/bosses/chambers-of-xeric.png', '/images/public-board/bosses/vorkath.png?4'];
          return { id: 'tile-' + pos, pos, name: names[i], desc: '', ehb: i === 0 ? 0.2 : 1 + i * 0.7, noEstimate: false, needsVerification: false, overridden: false, manual: false, parts: 1, locked: false, changed: false,
            art: i % 3 === 0 ? '/images/public-board/bosses/vorkath.png' : null, bossArt: i % 3 === 1 ? bosses.slice(0, 1 + (i % 4)) : [] };
        });
          root.dataset.view = JSON.stringify(view); root.dataset.pvPatched = '1';
          return true;
        };
        const observer = new MutationObserver(() => { if (patch()) observer.disconnect(); });
        observer.observe(document, { childList: true, subtree: true });
      }, { scenario, names });
      const requests = [];
      page.on('request', request => { if (request.method() !== 'GET') requests.push(request.method() + ' ' + request.url()); });
      await page.goto(fixture.origin + boardPath);
      await page.locator('#preview-btn').waitFor();
      requests.length = 0;
      if (motion) await page.clock.pauseAt(await page.evaluate(() => Date.now() + 1000)); // frozen until the fade checks are done
      await page.locator('#preview-btn').focus(); // WebKit does not focus a button on click, so open by keyboard
      await page.keyboard.press('Enter');
      const layer = page.locator('.bd-pv');
      await layer.waitFor();
      assert.match(await layer.locator('.eyebrow').innerText(), scenario.eyebrow);
      const board = layer.locator('[data-pv-board]');
      const count = scenario.positions.length;
      assert.equal(await board.locator('[data-pv-tile]').count(), count, 'one tile per placed position');
      assert.equal(await board.locator('.bd-pv-empty').count(), scenario.rows * scenario.cols - count, 'empty positions stay empty');
      const columns = await board.evaluate(el => getComputedStyle(el).gridTemplateColumns.split(' ').length);
      assert.equal(columns, scenario.cols, 'grid has the board\'s column count');
      // Order, names and points follow the board; the first tile's 0.2 EHB shows the 1-point minimum.
      const tiles = await board.locator('[data-pv-tile]').evaluateAll(list => list.map(el => ({ pos: +el.dataset.pvTile, title: el.querySelector('.public-ui-team-board-tile__title').textContent, points: el.querySelector('.public-ui-team-board-tile__position').textContent, number: el.querySelector('.public-ui-team-board-tile__number').textContent })));
      assert.deepEqual(tiles.map(x => x.pos), scenario.positions);
      assert.deepEqual(tiles.map(x => x.title), names.slice(0, count));
      assert.equal(tiles[0].points, 'Points: 1');
      assert.equal(tiles[1].points, 'Points: 2');
      assert.equal(tiles[0].number, '01');
      // Art follows the team board: own image, else the boss-art grid (count class = image count), else the placeholder.
      for (let i = 0; i < count; i++) {
        const tile = board.locator('[data-pv-tile]').nth(i);
        if (i % 3 === 0) { assert.equal(await tile.locator('img.public-ui-team-board-tile__art-image').count(), 1, 'own image'); assert.equal(await tile.locator('.public-ui-team-board-tile__boss-art-grid').count(), 0); }
        else if (i % 3 === 1) {
          const n = 1 + (i % 4);
          assert.equal(await tile.locator(`.public-ui-team-board-tile__boss-art-grid--count-${n} img`).count(), n, 'boss-art grid with ' + n + ' images');
          assert.equal(await tile.locator('img.public-ui-team-board-tile__art-image').count(), 0);
        } else assert.equal(await tile.locator('img').count(), 0, 'placeholder tile has no image');
      }
      await page.waitForFunction(() => [...document.querySelectorAll('.bd-pv-board img')].every(img => img.complete && img.naturalWidth > 0), null, { timeout: 10000 });
      // Boss fade: tiles with several bosses show exactly one at a time; single-boss and own-image tiles are unchanged.
      const fadeState = () => board.evaluate(el => [...el.querySelectorAll('[data-pv-tile]')].map(tile => { const g = tile.querySelector('.public-ui-team-board-tile__boss-art-grid'); return { n: tile.querySelectorAll('img').length, boss: !!g, fade: g ? g.hasAttribute('data-boss-fade') : false, active: g ? g.dataset.bossActive ?? null : null, visible: [...tile.querySelectorAll('img')].filter(i => +getComputedStyle(i).opacity === 1).length, sameBox: g ? new Set([...g.querySelectorAll('img')].map(i => { const r = i.getBoundingClientRect(); return [r.x, r.y, r.width, r.height].join(','); })).size === 1 : true }; }));
      let fade = await fadeState();
      fade.forEach((t, i) => {
        if (!t.boss) return;
        assert.equal(t.visible, 1, `tile ${i} shows exactly one boss`); assert.equal(t.sameBox, true, `tile ${i} bosses are stacked in place`);
        assert.equal(t.fade, t.n > 1, `tile ${i} fades only with several bosses`);
        assert.equal(t.active, t.n > 1 ? '0' : null, `tile ${i} starts on the first boss`);
      });
      if (!motion) {
        assert.equal(await page.evaluate(() => window.BossArtFade.displayMs), 6000);
      } else {
        fs.mkdirSync(path.join(process.cwd(), 'artifacts/boss-fade'), { recursive: true });
        await board.locator('[data-pv-tile]').nth(7).screenshot({ path: path.join(process.cwd(), 'artifacts/boss-fade', `preview-4-boss-tile-before-${name}.png`) });
        const multi = fade.map((t, i) => (t.n > 1 && t.boss ? i : -1)).filter(i => i >= 0); // draft 3x3: tiles 1 (2 bosses) and 7 (4 bosses)
        assert.deepEqual(multi, [1, 7], 'fixture has a 2-boss and a 4-boss tile');
        // The clock was frozen just before the preview opened, so its schedule starts at 0: tile 1 switches at 6000 + 700 ms, tile 7 at 6000 + 2350 ms (staggered).
        await page.clock.runFor(6699); fade = await fadeState();
        assert.deepEqual(multi.map(i => fade[i].active), ['0', '0'], 'nothing switches before 6 s');
        await page.clock.runFor(2); fade = await fadeState();
        assert.deepEqual(multi.map(i => fade[i].active), ['1', '0'], 'tile 1 switches first');
        await page.clock.runFor(1700); fade = await fadeState();
        assert.deepEqual(multi.map(i => fade[i].active), ['1', '1'], 'tile 7 switches later (staggered)');
        await new Promise(resolve => setTimeout(resolve, 1800)); fade = await fadeState();
        assert.deepEqual(multi.map(i => fade[i].visible), [1, 1], 'one boss visible after the cross-fade');
        await board.locator('[data-pv-tile]').nth(7).screenshot({ path: path.join(process.cwd(), 'artifacts/boss-fade', `preview-4-boss-tile-after-${name}.png`) });
        // Hidden tab pauses; closing the preview stops its timers (no errors, nothing left to switch).
        await page.evaluate(() => { Object.defineProperty(document, 'visibilityState', { configurable: true, get: () => 'hidden' }); document.dispatchEvent(new Event('visibilitychange')); });
        const frozen = multi.map(i => fade[i].active); await page.clock.runFor(60000); fade = await fadeState();
        assert.deepEqual(multi.map(i => fade[i].active), frozen, 'hidden tab: no switching');
        await page.evaluate(() => { Object.defineProperty(document, 'visibilityState', { configurable: true, get: () => 'visible' }); document.dispatchEvent(new Event('visibilitychange')); });
        await page.clock.runFor(6000); fade = await fadeState();
        assert.notDeepEqual(multi.map(i => fade[i].active), frozen, 'resumes after the tab is visible again');
        await page.clock.resume();
      }
      // Board only: no progress, state labels, links, buttons or focusable tiles.
      assert.equal(await board.locator('.public-ui-team-board-tile__progress-row, .public-ui-team-board-tile__progress, a, button, [tabindex], [role=button]').count(), 0, 'tiles are plain, with no progress or interaction');
      assert.equal(await layer.locator('.public-team-sidebar, .public-team-switcher, .public-team-board-legend, .bd-ed').count(), 0, 'no sidebar, switcher, legend or tile view');
      const box = await board.boundingBox(), modal = await layer.boundingBox();
      assert.ok(box.x >= modal.x && box.x + box.width <= modal.x + modal.width + 1 && box.y + box.height <= 860, 'board fits the modal and window');
      await board.locator('[data-pv-tile]').first().click();
      assert.equal(await page.locator('.bd-ed').count(), 0, 'clicking a tile opens nothing');
      assert.equal(await layer.count(), 1, 'preview stays open after a tile click');
      await page.screenshot({ path: path.join(shots, `${scenario.rows}x${scenario.cols}-${scenario.mode}-${dark ? 'dark' : 'light'}-${name}.png`) });
      // Narrow window: like the team board, the board keeps its 37rem minimum (tiles stay legible) and the preview body scrolls sideways.
      await page.setViewportSize({ width: 390, height: 800 });
      await page.evaluate(() => new Promise(resolve => requestAnimationFrame(() => requestAnimationFrame(resolve))));
      const narrow = await board.evaluate(el => ({ width: el.getBoundingClientRect().width, tile: el.querySelector('[data-pv-tile]').getBoundingClientRect().width, scrolls: el.parentElement.scrollWidth > el.parentElement.clientWidth, body: el.parentElement.clientWidth, panel: document.querySelector('.bd-pv').getBoundingClientRect().width, vw: innerWidth, rem: parseFloat(getComputedStyle(document.documentElement).fontSize) }));
      assert.ok(narrow.width >= 592 && narrow.scrolls, 'board keeps the team board minimum width and scrolls sideways ' + JSON.stringify(narrow));
      assert.ok(narrow.tile >= (scenario.cols === 3 ? 160 : 95), 'narrow tiles stay as large as on the team board: ' + narrow.tile);
      await page.screenshot({ path: path.join(shots, `${scenario.rows}x${scenario.cols}-narrow-${dark ? 'dark' : 'light'}-${name}.png`) });
      await page.setViewportSize({ width: 1280, height: 860 });
      await page.keyboard.press('Escape');
      await layer.waitFor({ state: 'detached' });
      await page.waitForFunction(() => document.activeElement?.id === 'preview-btn', null, { timeout: 5000 }).catch(async () => assert.fail('focus returns to Preview, found: ' + await page.evaluate(() => document.activeElement?.outerHTML.slice(0, 120))));
      await page.locator('#preview-btn').click(); await layer.waitFor();
      await page.mouse.click(4, 4);
      await layer.waitFor({ state: 'detached' });
      assert.deepEqual(requests.filter(r => !/RenewEditing/.test(r)), [], 'preview sends no write request');
      assert.deepEqual(errors, []);
      await context.close();
    }
    console.log('PASS board preview: board only, names/points/order/art, not-started look, no interaction, fits 3x3/5x5/narrow, light and dark, closes with Escape/outside click, no writes, boss fade one-at-a-time/staggered/paused/reduced motion (' + name + ').');
  } finally {
    if (browser) await browser.close();
    await fixture.close();
  }
})().catch(error => { console.error(error); process.exitCode = 1; });

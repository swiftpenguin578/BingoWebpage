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
    for (const dark of [false, true]) for (const scenario of scenarios) {
      const context = await browser.newContext({ viewport: { width: 1280, height: 860 }, reducedMotion: 'reduce', colorScheme: dark ? 'dark' : 'light' });
      const page = await login(context, fixture);
      const errors = []; page.on('pageerror', error => errors.push(error.message));
      // Rewrite the page's own view data before its script reads it (no network interception: WebKit drops subresources of fulfilled documents).
      await page.addInitScript(({ scenario, names }) => {
        const patch = () => {
          const root = document.querySelector('[data-board]');
          if (!root || root.dataset.pvPatched) return !!root;
          const view = JSON.parse(root.dataset.view);
          Object.assign(view, { mode: scenario.mode, rows: scenario.rows, cols: scenario.cols, readOnly: false });
          view.tiles = scenario.positions.map((pos, i) => ({ id: 'tile-' + pos, pos, name: names[i], desc: '', ehb: i === 0 ? 0.2 : 1 + i * 0.7, noEstimate: false, needsVerification: false, overridden: false, manual: false, parts: 1, locked: false, art: i % 2 === 0 ? '/images/public-board/bosses/vorkath.png' : null, changed: false }));
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
      assert.ok(await board.locator('img.public-ui-team-board-tile__art-image').count() >= 1, 'art renders');
      // Board only: no progress, state labels, links, buttons or focusable tiles.
      assert.equal(await board.locator('.public-ui-team-board-tile__progress-row, .public-ui-team-board-tile__progress, a, button, [tabindex], [role=button]').count(), 0, 'tiles are plain, with no progress or interaction');
      assert.equal(await layer.locator('.public-team-sidebar, .public-team-switcher, .public-team-board-legend, .bd-ed').count(), 0, 'no sidebar, switcher, legend or tile view');
      const box = await board.boundingBox(), modal = await layer.boundingBox();
      assert.ok(box.x >= modal.x && box.x + box.width <= modal.x + modal.width + 1 && box.y + box.height <= 860, 'board fits the modal and window');
      await board.locator('[data-pv-tile]').first().click();
      assert.equal(await page.locator('.bd-ed').count(), 0, 'clicking a tile opens nothing');
      assert.equal(await layer.count(), 1, 'preview stays open after a tile click');
      await page.screenshot({ path: path.join(shots, `${scenario.rows}x${scenario.cols}-${scenario.mode}-${dark ? 'dark' : 'light'}-${name}.png`) });
      // Narrow window: the board still fits.
      await page.setViewportSize({ width: 390, height: 800 });
      const narrow = await board.boundingBox();
      assert.ok(narrow.x >= 0 && narrow.x + narrow.width <= 391, 'board fits a narrow window');
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
    console.log('PASS board preview: board only, names/points/order/art, not-started look, no interaction, fits 3x3/5x5/narrow, light and dark, closes with Escape/outside click, no writes (' + name + ').');
  } finally {
    if (browser) await browser.close();
    await fixture.close();
  }
})().catch(error => { console.error(error); process.exitCode = 1; });

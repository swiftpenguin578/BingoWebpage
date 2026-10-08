// U7-L1: editing in the open Board tile drawer changes only local draft state (rule 9;
// Board.dc.html). Typing, leaving a field, picking a boss, choosing a drop, toggling
// the count options and setting a weight keep the drawer's own elements (no input is
// removed or replaced), keep focus and typed values, and re-read nothing (no EditorData,
// Readback or page view request; the background lease renewal POST is allowed).
// Save still creates the tile; a lost lease keeps the drawer's inputs and draft. Real page against the controlled parity fixture, both engines.
const assert = require('node:assert/strict');
const path = require('node:path');
const { chromium, webkit } = require('playwright');
const { startFixture, login } = require('../../scripts/lib/admin-parity-fixture.cjs');
(async () => {
  const engine = process.env.PLAYWRIGHT_BROWSER === 'webkit' ? webkit : chromium;
  const fixture = await startFixture(process.cwd(), path.join(process.cwd(), 'artifacts/u7-board-drawer-' + (process.env.PLAYWRIGHT_BROWSER || 'chromium')));
  let browser;
  try {
    browser = await engine.launch({ headless: true, ...(engine === chromium ? { channel: process.env.PLAYWRIGHT_CHANNEL || 'chromium' } : {}) });
    const context = await browser.newContext({ viewport: { width: 1280, height: 860 }, reducedMotion: 'reduce' });
    const page = await login(context, fixture);
    const errors = []; page.on('pageerror', error => errors.push(error.message));
    const boardPath = '/Admin/Events/Board/' + fixture.events['autumn-bingo-2027'];
    await page.goto(fixture.origin + boardPath);
    await page.locator('#be-0').waitFor();
    assert.match(await page.locator('[data-board-acts]').innerText(), /You’re editing/, 'the fixture admin holds the edit lease');

    await page.locator('#be-0').click();
    await page.locator('#ed-save').waitFor();
    await page.waitForLoadState('networkidle');
    const reads = [];
    page.on('request', request => {
      const url = new URL(request.url());
      if (url.pathname !== boardPath) return;
      const handler = url.searchParams.get('handler');
      if (handler === 'RenewEditing' && request.method() === 'POST') return; // background lease renewal, not a re-read
      reads.push(request.method() + ' ' + (handler || '(view)'));
    });
    await page.evaluate(() => {
      const content = document.querySelector('.bd-ed');
      window.__drawer = { content, parts: [...content.children], removed: [] };
      new MutationObserver(records => {
        for (const record of records) for (const node of record.removedNodes) {
          if (node.nodeType !== 1) continue;
          const lost = node.matches('input,textarea') ? [node] : [...node.querySelectorAll('input,textarea')];
          for (const control of lost) window.__drawer.removed.push(control.id || control.type);
        }
      }).observe(content, { childList: true, subtree: true });
    });
    const probes = () => page.evaluate(() => {
      window.__keep = id => { (window.__kept ||= {})[id] = document.getElementById(id); };
      window.__same = id => window.__kept[id] === document.getElementById(id) && window.__kept[id].isConnected;
    });
    await probes();
    const keep = id => page.evaluate(id => window.__keep(id), id);
    const same = id => page.evaluate(id => window.__same(id), id);
    const active = () => page.evaluate(() => { const a = document.activeElement; return a && a !== document.body && document.querySelector('.bd-ed').contains(a) ? (a.id || a.type) : null; });

    // Name: type, then leave the field (change) with Tab.
    await keep('ed-name');
    await page.locator('#ed-name').click();
    await page.keyboard.type('Drop hunt');
    await page.keyboard.press('Tab');
    assert.equal(await same('ed-name'), true, 'the name input is the same element after leaving it');
    assert.equal(await page.locator('#ed-name').inputValue(), 'Drop hunt');
    // WebKit's Tab skips buttons, so the next stop differs by engine; it must not be the page.
    assert.ok(['ed-desc-write', 'ed-art-btn'].includes(await active()), 'Tab moves focus on to the next drawer control, not to the page');

    // Boss: search and pick with Enter; the combobox stays the same focused element.
    await keep('ed-o0-src');
    await page.locator('#ed-o0-src').click();
    await page.keyboard.type('Barrows');
    await page.keyboard.press('Enter');
    await page.locator('.combo-chip', { hasText: 'Barrows Chests' }).waitFor();
    assert.equal(await same('ed-o0-src'), true, 'the boss search is the same element after a pick');
    assert.equal(await active(), 'ed-o0-src', 'focus stays in the boss search after a pick');
    await page.keyboard.press('Escape');

    // Drop: choose one, open the count options, turn on weights and set one.
    await keep('ed-name'); await keep('ed-o0-src'); await keep('ed-o0-target');
    const drop = page.locator('[id^="ed-o0-d-"]').first();
    const dropId = await drop.getAttribute('id');
    await keep(dropId);
    // A click focuses a checkbox in Chromium; WebKit (like Safari) neither focuses it on click
    // nor tabs to buttons or checkboxes, so those focus checks are Chromium's; identity is checked in both.
    const clickFocuses = engine === chromium;
    await drop.check();
    assert.equal(await same(dropId), true, 'the drop checkbox is the same element after choosing it');
    if (clickFocuses) assert.equal(await active(), dropId, 'focus stays on the chosen drop');
    await page.locator('#ed-o0-count-btn').click();
    await page.locator('.count-opts input[type=checkbox]').nth(1).check();
    if (clickFocuses) assert.equal(await active(), 'checkbox', 'focus stays on the count option');
    const weight = page.locator('[id^="ed-o0-w-"]').first();
    const weightId = await weight.getAttribute('id');
    await keep(weightId);
    await weight.fill('3');
    await page.keyboard.press('Tab');
    assert.equal(await same(weightId), true, 'the weight input is the same element after leaving it');
    assert.equal(await weight.inputValue(), '3');
    assert.notEqual(await active(), null, 'focus stays in the drawer after the weight');

    // Target: type and leave.
    await page.locator('#ed-o0-target').fill('2');
    await page.keyboard.press('Tab');
    assert.equal(await page.locator('#ed-o0-target').inputValue(), '2');
    // WebKit's Tab reaches no further text field after the target, so it leaves the drawer.
    if (clickFocuses) assert.notEqual(await active(), null, 'focus stays in the drawer after the target');

    for (const id of ['ed-name', 'ed-o0-src', 'ed-o0-target', dropId]) assert.equal(await same(id), true, id + ' is still the same element');
    const drawer = await page.evaluate(() => ({ parts: window.__drawer.parts.every((part, i) => window.__drawer.content.children[i] === part), removed: window.__drawer.removed }));
    assert.equal(drawer.parts, true, 'the drawer head, body and footer were never replaced');
    assert.deepEqual(drawer.removed, [], 'no drawer input was removed or replaced while editing');
    assert.equal(await page.locator('#ed-name').inputValue(), 'Drop hunt', 'typed values are kept');
    assert.match(await page.locator('.dr-foot').innerText(), /Unsaved changes/);
    assert.deepEqual(reads, [], 'editing re-reads nothing (no EditorData, Readback or view request)');

    // Manual challenge (the fixture catalogue has no GP values, so a drops tile can't be
    // saved): switch the kind, type the challenge and the EHB; the name input stays.
    await page.locator('.seg-opt', { hasText: 'Manual challenge' }).click();
    await page.locator('#ed-o0-text').fill('Finish a Barrows run');
    await keep('ed-o0-text');
    await page.keyboard.press('Tab');
    assert.equal(await same('ed-o0-text'), true, 'the challenge text is the same element after leaving it');
    await page.locator('#ed-ehb').fill('1.5');
    await page.keyboard.press('Tab');
    assert.equal(await same('ed-name'), true, 'switching the kind keeps the name input');
    assert.equal(await page.locator('#ed-name').inputValue(), 'Drop hunt');
    assert.deepEqual(reads, [], 'still no re-read');

    // Save still works: one command, then the board re-reads and shows the tile.
    // U7-L2: while the save is in flight the footer has exactly one Cancel and one Save (busy,
    // same size and place as before), and nothing else sits on top of or behind them.
    const footState = () => page.evaluate(() => {
      const foot = document.querySelector('.dr-foot'), box = id => { const r = document.getElementById(id).getBoundingClientRect(); return [Math.round(r.left), Math.round(r.width)]; };
      const covered = ['ed-cancel', 'ed-save'].map(id => { const r = document.getElementById(id).getBoundingClientRect(); return [[r.left + 3, r.top + 3], [r.left + r.width / 2, r.top + r.height / 2], [r.right - 3, r.bottom - 3]].map(([x, y]) => document.elementsFromPoint(x, y)[0].closest('button')?.id || document.elementsFromPoint(x, y)[0].className); });
      return { foots: document.querySelectorAll('.dr-foot').length, children: [...foot.children].map(c => c.id || c.className), cancels: [...document.querySelectorAll('button')].filter(b => /^Cancel$/.test(b.textContent.trim())).length, cancel: box('ed-cancel'), save: box('ed-save'), covered, cancelDisabled: document.getElementById('ed-cancel').disabled, saveBusy: document.getElementById('ed-save').classList.contains('is-busy'), spins: foot.querySelectorAll('.spin').length, saveText: document.getElementById('ed-save').textContent };
    });
    const idle = await footState();
    await page.route(url => new URL(url).pathname === boardPath, async route => {
      if (route.request().method() === 'POST' && new URL(route.request().url()).searchParams.get('handler') === 'CreateTile') await new Promise(resolve => setTimeout(resolve, 1500));
      return route.fallback();
    });
    await page.locator('#ed-save').click();
    await page.locator('#ed-save.is-busy').waitFor();
    const busy = await footState();
    assert.equal(busy.foots, 1, 'one drawer footer while saving');
    assert.deepEqual(busy.children, ['spacer', 'dirty', 'ed-cancel', 'ed-save'], 'the footer holds only its spacer, marker, one Cancel and one Save');
    assert.equal(busy.cancels, 1, 'exactly one Cancel button while saving');
    assert.equal(busy.cancelDisabled, true); assert.equal(busy.saveBusy, true);
    assert.equal(busy.spins, 1, 'one spinner'); assert.match(busy.saveText, /Saving/);
    assert.deepEqual(busy.cancel, idle.cancel, 'Cancel does not move when saving starts');
    assert.deepEqual(busy.save, idle.save, 'Save keeps its size and place when saving starts');
    assert.deepEqual(busy.covered, [['ed-cancel', 'ed-cancel', 'ed-cancel'], ['ed-save', 'ed-save', 'ed-save']], 'nothing overlaps the footer buttons');
    await page.locator('.bd-ed').waitFor({ state: 'detached', timeout: 10000 }).catch(async error => { throw new Error(error.message + '\nDrawer: ' + await page.locator('.bd-ed').innerText().catch(() => '') + '\nRequests: ' + reads.join(', ')); });
    await page.locator('[id^="bt-"]', { hasText: 'Drop hunt' }).waitFor();
    assert.ok(reads.includes('POST CreateTile'), 'Save sends the create command: ' + reads.join(', '));
    await page.unroute(() => true).catch(() => {});
    // After the save the reopened drawer has the normal footer again.
    await page.locator('[data-drawer-host] .scrim').waitFor({ state: 'detached' });
    await page.locator('[id^="bt-"]', { hasText: 'Drop hunt' }).click();
    await page.locator('#ed-save').waitFor();
    const normal = await footState();
    assert.deepEqual(normal.children, ['ed-remove', 'spacer', 'ed-cancel', 'ed-save'], 'normal footer: Remove, Cancel, Save');
    assert.equal(normal.cancels, 1); assert.equal(normal.cancelDisabled, false); assert.equal(normal.saveBusy, false); assert.equal(normal.spins, 0);
    await page.locator('#ed-cancel').click();
    await page.locator('.bd-ed').waitFor({ state: 'detached' });
    await page.locator('[data-drawer-host] .scrim').waitFor({ state: 'detached' });

    // A genuinely lost lease (renewal answers renewed:false; the re-read view shows another
    // admin editing): the open drawer keeps its inputs and the typed draft, disabled in
    // place, with the existing "is editing now" banner.
    let lose = false;
    await page.route(url => new URL(url).pathname === boardPath, async route => {
      const request = route.request(), handler = new URL(request.url()).searchParams.get('handler');
      if (lose && handler === 'RenewEditing') return route.fulfill({ contentType: 'application/json', body: JSON.stringify({ renewed: false }) });
      if (lose && !handler && request.method() === 'GET') {
        const response = await route.fetch(), html = await response.text();
        return route.fulfill({ response, body: html.replace(/(&quot;|")who\1:\1me\1,\1name\1:\1parity-admin\1/, '$1who$1:$1other$1,$1name$1:$1Other Admin$1') });
      }
      return route.fallback();
    });
    await page.reload();
    await page.locator('[id^="bt-"]', { hasText: 'Drop hunt' }).click();
    await page.locator('#ed-save').waitFor();
    await page.waitForLoadState('networkidle');
    await probes(); await keep('ed-name');
    lose = true;
    await page.locator('#ed-name').click();
    await page.keyboard.type(' 2');
    await page.locator('#ed-banner', { hasText: 'Other Admin is editing now.' }).waitFor();
    assert.equal(await same('ed-name'), true, 'the name input survives the lost lease');
    assert.equal(await page.locator('#ed-name').inputValue(), 'Drop hunt 2', 'the typed draft is kept');
    assert.equal(await page.locator('#ed-name').isDisabled(), true, 'inputs are disabled, not replaced');
    assert.equal(await page.locator('#ed-save').isDisabled(), true);
    assert.deepEqual(errors, []);
    console.log(`PASS U7-L1 Board drawer edits keep the drawer in place and re-read nothing; Save creates the tile [${process.env.PLAYWRIGHT_BROWSER || 'chromium'}]`);
  } finally {
    await browser?.close();
    await fixture.close();
  }
})().catch(error => { console.error(error); process.exit(1); });

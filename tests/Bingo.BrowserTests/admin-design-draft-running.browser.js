// U6 Teams / Draft (items 1b/1c): running draft, finalize (F1), corrections and the read-only
// compositions, real page against the controlled parity fixture (BINGO_PARITY_DRAFT: a running
// draft on "clan-cup-pvm-week", order drawn, no picks, nobody in control), both engines.
// Covers control (take, release), redraw, click-to-pick, search + Enter, Undo by the latest
// pick's id, uncertain outcomes (an answer without an outcome and a lost response are never
// success; the readback says what is true), a refused Undo, Cancel blocked by active picks,
// the sidebar collapsed while running, finalize staying on Teams, a correction Remove and an
// Add with a role (U6-Q1) with the republish line, Live roles, Final review and terminal (T-24).
const assert = require('node:assert/strict');
const path = require('node:path');
const { chromium, webkit } = require('playwright');
const { startFixture, login } = require('../../scripts/lib/admin-parity-fixture.cjs');
(async () => {
  const engine = process.env.PLAYWRIGHT_BROWSER === 'webkit' ? webkit : chromium;
  const fixture = await startFixture(process.cwd(), path.join(process.cwd(), 'artifacts/u6-draft-running-' + process.env.PLAYWRIGHT_BROWSER), { BINGO_PARITY_DRAFT: '1' });
  let browser;
  try {
    browser = await engine.launch({ headless: true, ...(engine === chromium ? { channel: process.env.PLAYWRIGHT_CHANNEL || 'chromium' } : {}) });
    const context = await browser.newContext({ viewport: { width: 1280, height: 860 }, reducedMotion: 'reduce' });
    const page = await login(context, fixture);
    const errors = []; page.on('pageerror', error => errors.push(error.message));
    const draftUrl = slug => fixture.origin + '/Admin/Events/Draft/' + fixture.events[slug];
    const modal = page.locator('.modal').last();
    const toast = text => page.locator('[data-toast-host]', { hasText: text }).waitFor().catch(async error => { throw new Error(error.message + '\nToasts: ' + await page.locator('[data-toast-host]').innerText() + '\nBanner: ' + await banner.innerText().catch(() => '') + '\nModal: ' + await page.locator('.modal').last().innerText().catch(() => '')); });
    const banner = page.locator('#td-banner');
    const column = name => page.locator('.dteam', { has: page.locator('.dteam-name', { hasText: name }) });
    const chip = name => page.locator('.pchip', { hasText: name });
    const more = async label => { await page.locator('#draft-more').click(); await page.locator('#draft-menu .menu-item', { hasText: label }).click(); };
    const sidebarCollapsed = () => page.locator('[data-shell-sidebar]').evaluate(e => e.classList.contains('is-collapsed'));

    await page.goto(draftUrl('clan-cup-pvm-week'));
    await page.locator('.td-live').waitFor();
    assert.equal(await sidebarCollapsed(), true, 'the sidebar collapses while the draft runs');
    assert.equal(await page.locator('.page-head').evaluate(e => e.classList.contains('sr')), true, 'compact running layout');
    assert.equal(await page.locator('.td-manual-note').innerText(), 'Not in the draft: Bank Standers (1), assembled by hand.');
    assert.equal(await chip('Zulrah Fan').getAttribute('aria-disabled'), 'true', 'no picks without control');
    assert.match(await page.locator('#pool-help').innerText(), /Take control to pick\./);

    // ---- control, redraw ----
    await page.locator('#ctl-btn', { hasText: 'Take control' }).click();
    await toast('You have control of the draft.');
    await page.locator('.ctl', { hasText: 'You have control' }).waitFor();
    await more('Redraw order');
    await page.locator('[data-toast-host]', { hasText: /Order redrawn\. .+ picks first\./ }).waitFor();
    const first = (await page.locator('.turn-team').innerText()).trim();

    // ---- click-to-pick, search + Enter, sort ----
    await chip('Zulrah Fan').click();
    await column(first).locator('.dmem:not(.is-pending)', { hasText: 'Zulrah Fan' }).locator('.dmem-tag', { hasText: '#1' }).waitFor();
    assert.equal(await page.locator('.pool-count').innerText(), '6 left');
    await page.locator('#pool-search').fill('chin');
    await page.keyboard.press('Enter');
    await page.locator('.dmem:not(.is-pending)', { hasText: 'Chin Chomp' }).locator('.dmem-tag', { hasText: '#2' }).waitFor();
    assert.equal(await page.locator('#pool-search').inputValue(), '', 'Enter on a single match clears the search');
    const search = await page.locator('#pool-search').elementHandle();
    await page.locator('#pool-sort-name-opt').click();
    assert.equal(await page.locator('.pchip-name').first().innerText(), 'Barrows Bro', 'name sort');
    assert.equal(await search.evaluate(e => e.isConnected), true, 'sorting keeps the search control');

    // ---- Undo the latest pick by its id ----
    await page.locator('#undo-btn', { hasText: 'Undo #2' }).click();
    await chip('Chin Chomp').waitFor();
    assert.equal(await page.locator('.dmem:not(.is-pending)', { hasText: 'Chin Chomp' }).count(), 0);
    // The command is fully settled (onOk ran: live line set) before the next Undo, so the click cannot be ignored while blocked.
    await page.locator('p.sr[aria-live="polite"]', { hasText: /Pick 2 undone/ }).waitFor({ state: 'attached' });
    await page.locator('#undo-btn:not([disabled]):not(.is-busy)', { hasText: 'Undo #1' }).waitFor();
    // A refused Undo (the latest pick changed) is shown with the server's reason, not as unsure.
    await page.route('**/Admin/Events/Draft/*?handler=Undo', route => route.fulfill({ contentType: 'application/json', body: JSON.stringify({ outcome: 'stale', tone: 'error', message: 'The latest pick changed. Nothing was undone; the current board is shown.' }) }), { times: 1 });
    await page.locator('#undo-btn').click();
    await toast('Couldn’t undo pick 1. The latest pick changed. Nothing was undone; the current board is shown.');
    assert.equal(await banner.count(), 0, 'a definite refusal is not an uncertain outcome');

    // ---- uncertain outcomes ----
    await page.route('**/Admin/Events/Draft/*?handler=Pick', route => route.fulfill({ contentType: 'application/json', body: '{}' }), { times: 1 });
    await chip('Vorki Main').click();
    await banner.filter({ hasText: 'We couldn’t confirm the response.' }).waitFor();
    assert.match(await banner.innerText(), /Vorki Main wasn’t drafted\./);
    await page.route('**/Admin/Events/Draft/*?handler=Pick', async route => { await route.fetch(); await route.abort(); }, { times: 1 });
    await chip('Vorki Main').click();
    await banner.filter({ hasText: /Vorki Main is on .+\./ }).waitFor();
    assert.doesNotMatch(await banner.innerText(), /went through/, 'a matching state never claims this request made it');
    await page.locator('.dmem:not(.is-pending)', { hasText: 'Vorki Main' }).waitFor();

    // ---- Cancel is blocked while picks are active ----
    await more('Cancel draft…');
    await modal.locator('.m-title', { hasText: 'Undo picks before cancelling' }).waitFor();
    assert.equal(await modal.locator('#cx-confirm').count(), 0, 'no confirm while picks are active');
    await modal.locator('#cx-cancel', { hasText: 'Close' }).click();

    // ---- release and take back ----
    await page.locator('#ctl-btn', { hasText: 'Release' }).click();
    await toast('Control released. Any admin can take it.');
    await page.locator('.ctl.is-none').waitFor();
    assert.equal(await banner.count(), 0, 'an own release is not reported as a lapse');
    await page.locator('#ctl-btn', { hasText: 'Take control' }).click();
    await page.locator('.ctl', { hasText: 'You have control' }).waitFor();

    // ---- complete the draft and finalize (F1: stays on Teams) ----
    for (let left = await page.locator('.pchip').count(); left > 0; left--) {
      const name = (await page.locator('.pchip .pchip-name').first().innerText()).trim();
      await page.locator('.pchip').first().click();
      await page.locator('.dmem:not(.is-pending)', { hasText: name }).waitFor();
    }
    await page.locator('.turn-msg', { hasText: 'Every player is on a team.' }).waitFor();
    await page.locator('#turn-primary', { hasText: 'Finalize…' }).click();
    await modal.locator('.m-title', { hasText: 'Finalize the draft?' }).waitFor();
    await modal.locator('#cx-confirm').click();
    await toast('Rosters and draft results published.');
    assert.match(page.url(), /\/Admin\/Events\/Draft\//, 'finalizing stays on Teams');
    await banner.filter({ hasText: /Rosters published/ }).waitFor();
    assert.equal(await sidebarCollapsed(), false, 'the sidebar comes back when the draft ends');
    assert.equal(await page.locator('#head-action').getAttribute('aria-disabled'), 'true', 'Open Board waits for a board');

    // ---- corrections: Remove with disclosures, then Add with a role (U6-Q1), each republishes ----
    const card = name => page.locator('.tcard', { has: page.locator('.tcard-name', { hasText: name }) });
    const bronzeSize = await card('Bronze Line').locator('.tmem').count();
    assert.ok(bronzeSize >= 3, 'the drafted size is 3 or 4 whichever team picked first');
    await card('Bronze Line').locator('.tmem', { hasText: 'Bronze Liner' }).locator('.icon-btn').click();
    await page.locator('#draft-menu .menu-item', { hasText: 'Remove from team…' }).click();
    await modal.locator('.m-title', { hasText: 'Remove Bronze Liner from Bronze Line?' }).waitFor();
    assert.match(await modal.innerText(), /Bronze Line will have no captain until you assign one\./);
    // U6-E2 (a): under the drafted size, the dialog says by how much.
    // (Which team is drawn first is random, so the sentence is built from the team's actual size.)
    assert.match(await modal.innerText(), new RegExp('Bronze Line will have ' + (bronzeSize - 1) + ' members, 1 below the drafted size of ' + bronzeSize + '\\.'));
    await modal.locator('#cx-confirm').click();
    await toast('Bronze Liner removed from Bronze Line. Rosters republished.');
    await card('Bronze Line').locator('button', { hasText: 'Add member' }).click();
    const drawer = page.locator('.drawer');
    await page.locator('.dr-title', { hasText: 'Add to the published roster' }).waitFor();
    await drawer.locator('.res', { hasText: 'Bronze Liner' }).click();
    await drawer.locator('.choice[data-role="CC"]').click();
    assert.equal(await drawer.locator('#ps-save').innerText(), 'Add and republish');
    await drawer.locator('#ps-save').click();
    await toast('Bronze Liner added to Bronze Line as co-captain. Rosters republished.');
    // (The "Added" tag needs a join time after the publication; the fixture clock is fixed.)
    assert.equal(await card('Bronze Line').locator('.tmem', { hasText: 'Bronze Liner' }).locator('.role-badge [aria-hidden]').innerText(), 'CC');

    // ---- Live: roles only; Final review with uploads closed and terminal: read-only (T-24) ----
    await page.goto(draftUrl('midsummer-skilling-sprint'));
    await banner.filter({ hasText: 'Rosters are locked.' }).waitFor();
    assert.equal(await page.locator('.tcard-foot').count(), 0, 'no membership actions while Live');
    await page.locator('.tmem .icon-btn').first().click();
    assert.deepEqual(await page.locator('#draft-menu .menu-item').allInnerTexts(), ['Make captain', 'Make co-captain']);
    await page.keyboard.press('Escape');
    for (const [slug, text] of [['draft-review', 'Rosters are final.'], ['spring-finalized', 'This event is finished.'], ['spring-cancelled', 'This event is cancelled.'], ['spring-archived', 'This event is archived.']]) {
      await page.goto(draftUrl(slug));
      await banner.filter({ hasText: text }).waitFor();
      assert.equal(await page.locator('.tcard .icon-btn, .tcard-foot button, #add-team').count(), 0, slug + ': no edit controls');
    }

    assert.deepEqual(errors, [], 'no page errors');
    console.log('PASS Teams / Draft running, finalize, corrections, Live/review/terminal (' + (process.env.PLAYWRIGHT_BROWSER || 'chromium') + ')');
  } finally { await browser?.close(); await fixture.close(); }
})().catch(error => { console.error(error); process.exitCode = 1; });

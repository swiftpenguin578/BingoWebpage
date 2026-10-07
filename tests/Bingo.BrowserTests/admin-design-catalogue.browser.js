// T2 Catalogue: real page against the controlled UR fixture (PostgreSQL), both engines.
// Directory filtering in place, drawer URL/history, decision B (outside click / Escape), the drop editor's
// live rate preview, saves with the shared busy helper, S10 deactivate confirmation with board links,
// reactivation and C-CMP-2 session loss keeping the draft. Replaces catalogue-admin.test.js and
// catalogue-deletion.browser.js, which tested the retired route editor (A10).
const assert = require('node:assert/strict');
const path = require('node:path');
const { chromium, webkit } = require('playwright');
const { startFixture, login } = require('../../scripts/lib/admin-parity-fixture.cjs');
(async () => {
  const engine = process.env.PLAYWRIGHT_BROWSER === 'webkit' ? webkit : chromium;
  const fixture = await startFixture(process.cwd(), path.join(process.cwd(), 'artifacts/t2-catalogue-' + process.env.PLAYWRIGHT_BROWSER), { BINGO_PARITY_UR_PROFILE: 'live' });
  let browser;
  try {
    browser = await engine.launch({ headless: true, ...(engine === chromium ? { channel: process.env.PLAYWRIGHT_CHANNEL || 'chromium' } : {}) });
    const context = await browser.newContext({ viewport: { width: 1280, height: 860 }, reducedMotion: 'reduce' });
    // Thumbnails come from the OSRS Wiki image cache; the fixture has no network, so serve a blank image.
    await context.route(url => url.pathname.startsWith('/media/osrs-wiki'), route => route.fulfill({ status: 200, contentType: 'image/svg+xml', body: '<svg xmlns="http://www.w3.org/2000/svg" width="1" height="1"/>' }));
    const page = await login(context, fixture);
    const errors = []; page.on('pageerror', error => errors.push(error.message));
    await page.goto(fixture.origin + '/Admin/Catalogue?cat=Raid&status=gone');
    await page.locator('[data-catalogue-directory]').waitFor();
    assert.equal(new URL(page.url()).search, '', 'invalid link parts canonicalize to the plain directory');
    assert.equal(await page.locator('h1.h1').innerText(), 'Catalogue');
    const summary = await page.locator('.page-head .summary > span').allTextContents();
    assert.match(summary[0], /^\d+ active activities$/); assert.match(summary[1], /^\d+ drops$/);
    assert.equal(summary[2], 'Shared by every event’s board estimates');
    assert.equal(await page.locator('.page-head .summary b.tnum').count(), 2);
    const total = await page.locator('[data-activity-row]:not([hidden])').count();
    assert.equal(await page.locator('[data-catalogue-count="active"]').innerText(), String(total));
    assert.equal(await page.evaluate(() => document.scrollingElement.scrollHeight <= innerHeight), true, 'the document never scrolls');

    // In place: search by a drop name says so; the header and search keep their nodes and focus.
    const header = await page.locator('.page-head').elementHandle();
    await page.locator('#ct-search').fill('onyx');
    await page.waitForFunction(() => new URL(location.href).searchParams.get('q') === 'onyx');
    assert.ok(await page.locator('[data-activity-row]:not([hidden])').count() >= 1);
    assert.match(await page.locator('[data-activity-row]:not([hidden]) .cell-sub').first().innerText(), / · matches Uncut onyx$/);
    assert.equal(await page.evaluate(() => document.activeElement.id), 'ct-search');
    assert.equal(await header.evaluate(node => node.isConnected), true);
    await page.locator('.seg-opt', { hasText: 'Minigame' }).click();
    await page.waitForFunction(() => new URL(location.href).searchParams.get('cat') === 'Minigame');
    await page.locator('#empty-clear').waitFor();
    assert.equal(await page.locator('[data-catalogue-empty-title]').innerText(), 'No activities match');
    await page.locator('#empty-clear').click();
    await page.waitForFunction(() => location.search === '');
    assert.equal(await page.locator('[data-activity-row]:not([hidden])').count(), total);
    await page.locator('.tab', { hasText: 'Inactive' }).click();
    await page.waitForFunction(() => new URL(location.href).searchParams.get('status') === 'inactive');
    await page.locator('.tab', { hasText: 'Active' }).first().click();
    await page.waitForFunction(() => location.search === '');

    // Drawer: pushes ?activity=, Back closes it, Forward reopens it; a clean drawer closes on Escape (B).
    await page.locator('#ct-search').fill('Zulrah');
    const open = page.locator('[data-activity-row]:not([hidden]) [data-catalogue-open]', { hasText: /^Zulrah$/ });
    const activityId = await open.getAttribute('data-catalogue-open');
    await open.click();
    const drawer = page.locator('.drawer');
    await drawer.locator('#drawer-title', { hasText: 'Zulrah' }).waitFor();
    await drawer.locator('#a-name').waitFor();
    assert.equal(new URL(page.url()).searchParams.get('activity'), activityId);
    assert.equal(await drawer.getAttribute('data-page-family'), 'catalogue');
    assert.equal(await drawer.locator('#a-team').inputValue(), '1', 'CAT-1 Team size beside Kills per hour');
    await page.goBack();
    await page.waitForFunction(() => !document.querySelector('.drawer'));
    await page.goForward();
    await drawer.locator('#a-name').waitFor();
    await page.keyboard.press('Escape');
    await page.waitForFunction(() => !document.querySelector('.drawer'));
    assert.equal(await page.locator('.modal').count(), 0, 'no discard question without edits');
    await open.click();
    await drawer.locator('#a-name').waitFor();

    // Activity settings: Discard appears with edits; Escape with edits asks and names the part.
    await drawer.locator('#a-rate').fill('41');
    assert.equal(await drawer.locator('#dr-cancel').innerText(), 'Discard');
    assert.ok(!(await drawer.locator('[data-catalogue-recalc]').isHidden()), 'saving the rate says what it recalculates');
    await page.keyboard.press('Escape');
    const discard = page.locator('.modal[role=alertdialog]').last();
    await discard.locator('.m-points', { hasText: 'Discards the activity settings.' }).waitFor();
    await discard.locator('button', { hasText: 'Keep editing' }).click();
    assert.equal(await drawer.locator('#a-rate').inputValue(), '41');
    await drawer.locator('#dr-cancel').click();
    assert.equal(await drawer.locator('#dr-cancel').innerText(), 'Close');

    // Drop editor: one at a time, &drop= replaced; live preview including "N x" rolls; Save waits for a change.
    const dropRow = drawer.locator('.dlist-row', { hasText: 'Tanzanite fang' }); // the UR S10 scenario drop
    const dropId = await dropRow.getAttribute('data-catalogue-drop-toggle');
    const itemName = (await dropRow.locator('.dlist-name').innerText()).trim();
    await dropRow.click();
    await drawer.locator('#e-name').waitFor();
    assert.equal(new URL(page.url()).searchParams.get('drop'), dropId);
    assert.equal(await drawer.locator('#e-save').isDisabled(), true);
    await drawer.locator('#e-rate').fill('2 x 1/500');
    assert.match(await drawer.locator('#e-rate-hint').innerText(), /^2 rolls of 1 in 500 · 1 in 250 per kill/);
    assert.equal(await drawer.locator('#e-save').isDisabled(), false);
    // How the rate is counted: the decided panel (no chance-per-roll / whose-chance rows; Super Admin only line).
    await drawer.locator('#mech-btn').click();
    const mech = await drawer.locator('#mech-body').innerText();
    assert.match(mech, /Roll group can only be changed by the Super Admin\./);
    assert.doesNotMatch(mech, /Whose chance|Chance per roll|Only after|needs an operator/);
    assert.equal(await drawer.locator('#e-group').count(), 0, 'an ordinary Admin sees the roll group read-only');

    // C-CMP-2: a lost session shows what wasn't saved and keeps the entries.
    await page.route(url => url.searchParams.get('handler') === 'UpdateDrop', route => route.fulfill({ status: 200, contentType: 'text/html', body: '<!doctype html><title>Sign in</title>' }));
    await drawer.locator('#e-save').click();
    const notice = page.locator('.modal', { hasText: 'Your changes were not saved' });
    await notice.waitFor();
    assert.match(await notice.innerText(), /2 x 1\/500/);
    await notice.locator('button', { hasText: 'Keep editing' }).click();
    await page.unrouteAll({ behavior: 'wait' });
    assert.equal(await drawer.locator('#e-rate').inputValue(), '2 x 1/500');

    // Save: the server's values replace the editor; a toast confirms.
    await drawer.locator('#e-save').click();
    await page.locator('.toast', { hasText: `${itemName} saved. EHB recalculated.` }).waitFor();
    await drawer.locator(`[data-drop-id="${dropId}"] .dlist-sub`, { hasText: /^2 x 1\/500 · 1 in 250 per kill/ }).waitFor();

    // S10: Deactivate drop… names the draft boards (with Board links) and counts the hidden event.
    await drawer.locator(`[data-drop-id="${dropId}"] .dlist-row`).click();
    await drawer.locator('#e-toggle').click();
    const confirm = page.locator('.modal[role=alertdialog]').last();
    await confirm.locator('.m-points').waitFor();
    const body = await confirm.innerText();
    assert.match(body, new RegExp(`Deactivate ${itemName}\\?`));
    assert.match(body, /Draft boards that use it can’t be approved until it’s reactivated or those tiles change:/);
    assert.match(body, /Upcoming setup 01/);
    assert.match(body, /\(correction\)/);
    assert.match(body, /and 1 hidden event/);
    assert.doesNotMatch(body, /Hidden final review/, 'T2-Q3 (a): no hidden event name for an ordinary Admin');
    assert.match(await confirm.locator('a', { hasText: 'Upcoming setup 01' }).getAttribute('href'), /^\/Admin\/Events\/Board\/[0-9a-f-]{36}$/);
    await confirm.locator('button', { hasText: /^Deactivate$/ }).click();
    await page.locator('.toast', { hasText: `${itemName} deactivated. Draft boards that use it can’t be approved until it’s reactivated.` }).waitFor();
    await drawer.locator(`[data-drop-id="${dropId}"].is-inactive`).waitFor();
    await drawer.locator('#e-toggle', { hasText: 'Reactivate drop' }).click();
    await page.locator('.toast', { hasText: `${itemName} reactivated. It can be picked for tiles again.` }).waitFor();
    await drawer.locator(`[data-drop-id="${dropId}"]:not(.is-inactive)`).waitFor();

    // Add activity: ?new=1, client validation before any request, Team size defaults to 1.
    await page.keyboard.press('Escape');
    await page.waitForFunction(() => !document.querySelector('.drawer'));
    await page.locator('#add-activity').click();
    await drawer.locator('#drawer-title', { hasText: 'Add activity' }).waitFor();
    assert.equal(new URL(page.url()).searchParams.get('new'), '1');
    await drawer.locator('#dr-save').click();
    assert.equal(await drawer.locator('#error-a-name').innerText(), 'Enter a name.');
    await drawer.locator('#dr-cancel').click();
    await page.waitForFunction(() => !document.querySelector('.drawer') && !new URL(location.href).searchParams.has('new'));

    assert.deepEqual(errors, []);
    console.log('admin-design-catalogue: directory, drawer, editor, S10, C-CMP-2 and Add activity checks passed');
  } finally {
    await browser?.close();
    await fixture.close();
  }
})().catch(error => { console.error(error); process.exitCode = 1; });

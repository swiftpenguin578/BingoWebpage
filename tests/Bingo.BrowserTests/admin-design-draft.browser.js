// U6 Teams / Draft: real page against the controlled parity fixture, both engines. Replaces the
// fake-DOM tests of the retired event-manage.js Teams dialogs (draft-add-team-dialog.test.js,
// draft-roster-dialog.test.js; A10). Setup (item 1a): readiness with shell links, Add/Edit team
// (discard guard, duplicate name, server refusal kept in the dialog), participant selector
// (captain), member menu, S7 removal confirmation, uncertain outcomes (lost response and an
// answer without an outcome are never success; readback says what is true), rosterTeamId focus.
const assert = require('node:assert/strict');
const path = require('node:path');
const { chromium, webkit } = require('playwright');
const { startFixture, login } = require('../../scripts/lib/admin-parity-fixture.cjs');
(async () => {
  const engine = process.env.PLAYWRIGHT_BROWSER === 'webkit' ? webkit : chromium;
  const fixture = await startFixture(process.cwd(), path.join(process.cwd(), 'artifacts/u6-draft-' + process.env.PLAYWRIGHT_BROWSER), { BINGO_PARITY_PARTICIPANTS: '1' });
  let browser;
  try {
    browser = await engine.launch({ headless: true, ...(engine === chromium ? { channel: process.env.PLAYWRIGHT_CHANNEL || 'chromium' } : {}) });
    const context = await browser.newContext({ viewport: { width: 1280, height: 860 }, reducedMotion: 'reduce' });
    const page = await login(context, fixture);
    const errors = []; page.on('pageerror', error => errors.push(error.message));
    const eventId = fixture.events['autumn-bingo-2027'];
    const base = fixture.origin + '/Admin/Events/Draft/' + eventId;
    const modal = page.locator('.modal').last();
    const toast = text => page.locator('[data-toast-host]', { hasText: text }).waitFor();
    const card = name => page.locator('.tcard', { has: page.locator('.tcard-name', { hasText: name }) });
    const banner = page.locator('#td-banner');

    await page.goto(base);
    await card('Salt Mines').waitFor();
    // ---- readiness: shell links, inert head action ----
    const overview = page.locator('#ready-checks a', { hasText: 'Overview' });
    assert.equal(await overview.getAttribute('data-shell-link'), '', 'readiness links navigate inside the shell');
    assert.equal(await page.locator('#head-action').getAttribute('aria-disabled'), 'true', 'Finalize waits for the readiness steps');

    // ---- Add team: untouched Escape closes; edited Escape asks (decision B) ----
    await page.locator('#add-team').click();
    await page.locator('#tf-name').waitFor();
    await page.keyboard.press('Escape');
    await page.waitForFunction(() => !document.querySelector('#tf-name'));
    await page.locator('#add-team').click();
    await page.locator('#tf-name').fill('Draft');
    await page.keyboard.press('Escape');
    await page.locator('.modal', { hasText: 'Discard' }).waitFor();
    await page.locator('.modal button', { hasText: 'Keep editing' }).click();
    assert.equal(await page.locator('#tf-name').inputValue(), 'Draft', 'Keep editing keeps the typed name');
    // B-Teams-5 in the dialog: case-insensitive duplicate.
    await page.locator('#tf-name').fill('salt MINES');
    await page.locator('#tf-save').click();
    assert.equal(await page.locator('#tf-name-err').innerText(), 'Another team already has this name.');
    // A definite refusal stays in the dialog with the server's reason (rule 13).
    await page.route('**/Admin/Events/Draft/*?handler=AddTeam', route => route.fulfill({ contentType: 'application/json', body: JSON.stringify({ outcome: 'refused', tone: 'error', message: 'Team structure is locked.' }) }), { times: 1 });
    await page.locator('#tf-name').fill('Iron Pact');
    await page.locator('#tf-save').click();
    await page.locator('.modal [role=alert]', { hasText: 'Team structure is locked.' }).waitFor();
    assert.equal(await page.locator('#tf-name').inputValue(), 'Iron Pact', 'the refused dialog keeps its draft');
    await page.locator('#tf-save').click();
    await toast('Iron Pact added.');
    await card('Iron Pact').waitFor();

    // ---- an answer without an outcome is never success; the readback decides ----
    await page.route('**/Admin/Events/Draft/*?handler=AddTeam', route => route.fulfill({ contentType: 'application/json', body: '{}' }), { times: 1 });
    await page.locator('#add-team').click();
    await page.locator('#tf-name').fill('Ghost Team');
    await page.locator('#tf-save').click();
    await banner.filter({ hasText: 'We couldn’t confirm the response.' }).waitFor();
    assert.match(await banner.innerText(), /No team named Ghost Team exists\./);
    assert.equal(await card('Ghost Team').count(), 0);
    // ---- a lost response: the change happened; the page says so without claiming it as ours ----
    await page.route('**/Admin/Events/Draft/*?handler=AddTeam', async route => { await route.fetch(); await route.abort(); }, { times: 1 });
    await page.locator('#add-team').click();
    await page.locator('#tf-name').fill('Lost Team');
    await page.locator('#tf-save').click();
    await banner.filter({ hasText: 'A team named Lost Team now exists. It isn’t known whether this request created it.' }).waitFor();
    await card('Lost Team').waitFor();

    // ---- Assign captain through the selector (volunteers first) ----
    await card('Iron Pact').locator('button', { hasText: 'Assign captain' }).click();
    const drawer = page.locator('.drawer');
    await page.locator('.dr-title', { hasText: 'Add captain' }).waitFor();
    await drawer.locator('#ps-search').fill('mossy');
    await drawer.locator('.res', { hasText: 'Mossy Rock' }).click();
    assert.equal(await drawer.locator('#ps-save').innerText(), 'Add as captain');
    await drawer.locator('#ps-save').click();
    await toast('Mossy Rock added to Iron Pact as captain.');
    assert.equal(await card('Iron Pact').locator('.tmem', { hasText: 'Mossy Rock' }).locator('.role-badge [aria-hidden]').innerText(), 'C');

    // ---- member menu: role change, setup removal ----
    await card('Iron Pact').locator('.tmem', { hasText: 'Mossy Rock' }).locator('.icon-btn').click();
    await page.locator('#draft-menu .menu-item', { hasText: 'Make co-captain' }).click();
    await toast('Mossy Rock is now co-captain of Iron Pact.');
    await card('Iron Pact').locator('.tmem', { hasText: 'Mossy Rock' }).locator('.icon-btn').click();
    assert.equal(await page.locator('#draft-menu .menu-head').innerText(), 'Mossy Rock\nCo-captain · Iron Pact');
    await page.keyboard.press('Escape');

    // ---- S7: removing a populated team asks first and names what happens ----
    await card('Iron Pact').locator('.tcard-head .icon-btn').click();
    await page.locator('#draft-menu .menu-item', { hasText: 'Remove team…' }).click();
    await modal.locator('.m-title', { hasText: 'Remove Iron Pact?' }).waitFor();
    assert.match(await modal.innerText(), /Its 1 member goes back to having no team\. Nothing about the player changes; they stay signed up\./);
    await page.mouse.click(5, 5);
    assert.equal(await modal.locator('.m-title').count(), 1, 'a confirmation never closes on an outside click');
    await modal.locator('#cx-confirm').click();
    await toast('Iron Pact removed.');
    assert.equal(await card('Iron Pact').count(), 0);
    // An empty team says so.
    await card('Lost Team').locator('.tcard-head .icon-btn').click();
    await page.locator('#draft-menu .menu-item', { hasText: 'Remove team…' }).click();
    assert.match(await modal.locator('[data-confirm-description]').innerText(), /It has no members\./);
    await modal.locator('#cx-confirm').click();
    await toast('Lost Team removed.');
    await card('Ghost Team').count();

    // ---- rosterTeamId (stored notification links) focuses that team; unknown ids are ignored ----
    const saltId = (await card('Salt Mines').getAttribute('id')).slice('card-'.length);
    await page.goto(base + '?rosterTeamId=' + saltId);
    await page.waitForFunction(id => document.getElementById('card-' + id)?.contains(document.activeElement), saltId);
    await page.goto(base + '?rosterTeamId=00000000-0000-0000-0000-000000000001');
    await card('Salt Mines').waitFor();

    assert.deepEqual(errors, [], 'no page errors');
    console.log('PASS Teams / Draft setup (' + (process.env.PLAYWRIGHT_BROWSER || 'chromium') + ')');
  } finally { await browser?.close(); await fixture.close(); }
})().catch(error => { console.error(error); process.exitCode = 1; });

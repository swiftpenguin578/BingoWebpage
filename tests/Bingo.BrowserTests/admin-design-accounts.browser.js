// T1 Accounts: real page against the controlled UR fixture (PostgreSQL), both engines.
// Directory updates, drawer URL/history, confirmations with the shared dirty guard,
// reset-link handling (D5/A1), uncertain outcome gating (A2) and C-CMP-2 session loss.
const assert = require('node:assert/strict');
const path = require('node:path');
const { chromium, webkit } = require('playwright');
const { startFixture, login } = require('../../scripts/lib/admin-parity-fixture.cjs');
(async () => {
  const engine = process.env.PLAYWRIGHT_BROWSER === 'webkit' ? webkit : chromium;
  const fixture = await startFixture(process.cwd(), path.join(process.cwd(), 'artifacts/t1-accounts-' + process.env.PLAYWRIGHT_BROWSER), { BINGO_PARITY_UR_PROFILE: 'live' });
  let browser;
  try {
    browser = await engine.launch({ headless: true, ...(engine === chromium ? { channel: process.env.PLAYWRIGHT_CHANNEL || 'chromium' } : {}) });
    const context = await browser.newContext({ viewport: { width: 1280, height: 860 }, reducedMotion: 'reduce' });
    const page = await login(context, fixture);
    const errors = []; page.on('pageerror', error => errors.push(error.message));
    await page.goto(fixture.origin + '/Admin/Accounts?page=junk&role=nobody');
    await page.locator('[data-accounts-directory]').waitFor();
    assert.equal(new URL(page.url()).search, '', 'invalid link parts canonicalize to the plain directory');
    assert.equal(await page.locator('h1.h1').innerText(), 'Accounts');
    // T1-5: only the two count items with tabular numbers, as on Events.
    const summaryItems = await page.locator('.page-head .summary > span').allTextContents();
    assert.equal(summaryItems.length, 2, 'the summary is only the two count items');
    assert.match(summaryItems[0], /^\d+ accounts$/); assert.equal(summaryItems[1], '1 disabled');
    assert.equal(await page.locator('.page-head .summary b.tnum').count(), 2);
    // T1-4 (b): Global role is always a pill; Status keeps the reference (Disabled badge only).
    assert.equal(await page.locator('[data-account-row] .td:nth-child(2) > span:not(.badge)').count(), 0);
    assert.ok(await page.locator('[data-account-row] .td:nth-child(2) .badge-neutral').count() > 0);
    assert.equal(await page.locator('#transfer-btn').count(), 0, 'an ordinary Admin has no Transfer ownership');
    assert.equal(await page.evaluate(() => document.scrollingElement.scrollHeight <= innerHeight), true, 'the document never scrolls');

    // In-page search: results only; the input keeps focus and the URL is replaced.
    const header = await page.locator('.page-head').elementHandle();
    await page.locator('#ac-search').fill('reviewcap');
    await page.waitForFunction(() => new URL(location.href).searchParams.get('q') === 'reviewcap' && document.querySelectorAll('[data-account-row]').length === 1);
    assert.equal(await page.evaluate(() => document.activeElement.id), 'ac-search');
    assert.equal(await header.evaluate(node => node.isConnected), true, 'the header node is kept');
    await page.locator('.seg-opt', { hasText: 'Admin' }).first().click();
    await page.waitForFunction(() => new URL(location.href).searchParams.get('role') === 'admin');
    await page.locator('#empty-clear').click();
    await page.waitForFunction(() => location.search === '' && document.querySelectorAll('[data-account-row]').length > 5);
    assert.equal(await page.locator('#ac-search').inputValue(), '');

    // Drawer: pushes ?account=, Back closes it, Forward reopens it.
    const rowLink = page.locator('[data-account-open]', { hasText: 'ReviewWebsite' });
    const id = await rowLink.getAttribute('data-account-open');
    const rowInitials = (await page.locator(`[data-account-row="${id}"] .avatar`).innerText()).trim();
    // Avatar appears with the drawer at once: hold the drawer read so only the shell shows.
    let releaseRead; const held = new Promise(resolve => { releaseRead = resolve; });
    await page.route(url => url.searchParams.get('account') === id && url.pathname === '/Admin/Accounts', async route => { await held; await route.continue(); });
    await rowLink.click();
    const drawer = page.locator('.drawer');
    await drawer.locator('.dr-head .avatar').waitFor();
    assert.equal((await drawer.locator('.dr-head .avatar').innerText()).trim(), rowInitials);
    assert.equal(await drawer.locator('#drawer-title').innerText(), 'ReviewWebsite');
    releaseRead(); await page.unrouteAll({ behavior: 'wait' });
    await drawer.locator('#drawer-title', { hasText: 'ReviewWebsite' }).waitFor();
    assert.equal(new URL(page.url()).searchParams.get('account'), id);
    assert.equal(await drawer.getAttribute('data-page-family'), 'accounts');
    await page.goBack();
    await page.waitForFunction(() => !document.querySelector('.drawer'));
    assert.equal(new URL(page.url()).searchParams.get('account'), null);
    await page.goForward();
    await drawer.locator('#drawer-title', { hasText: 'ReviewWebsite' }).waitFor();

    // Reset link: shown once in the drawer only; never in the URL or browser storage.
    await drawer.locator('#a-reset').click();
    const secret = drawer.locator('.secret-result');
    await secret.waitFor();
    const link = await secret.locator('#sr-link').innerText();
    assert.match(link, /\/Account\/ResetPassword\/[A-F0-9]{64}$/);
    assert.match(await secret.innerText(), /The link stops working after 60 minutes, after one use, or if this account’s role, status or ownership changes\./);
    const token = link.split('/').at(-1);
    assert.equal(await page.evaluate(value => location.href.includes(value) || JSON.stringify({ ...localStorage }).includes(value) || JSON.stringify({ ...sessionStorage }).includes(value) || JSON.stringify(history.state).includes(value), token), false);
    await secret.locator('#sr-done').click();
    assert.equal(await drawer.locator('.secret-result').count(), 0);

    // Disable: the reason is protected by the shared discard choice (A3, A12).
    await drawer.locator('#a-status').click();
    const modal = page.locator('.modal[role=alertdialog]').last();
    await modal.locator('textarea').waitFor();
    await modal.locator('[data-account-confirm-accept]').click();
    assert.match(await modal.locator('#cm-reason-err').innerText(), /Enter a reason\./);
    await modal.locator('textarea').fill('Synthetic browser check');
    await page.keyboard.press('Escape');
    const discard = page.locator('.modal', { hasText: 'Discard unsaved changes?' });
    await discard.waitFor();
    await discard.locator('[data-confirm-cancel]').click();
    assert.equal(await modal.locator('textarea').inputValue(), 'Synthetic browser check');
    await modal.locator('[data-account-confirm-accept]').click();
    await drawer.locator('#dr-banner', { hasText: 'Account disabled.' }).waitFor();
    await drawer.locator('.dr-badges .badge-danger').waitFor();
    assert.equal(await page.locator(`[data-account-row="${id}"] .badge-danger`).count(), 1, 'the row behind the drawer is refreshed');

    // Unknown outcome: never repeated, actions gated until the status is checked (A2).
    await page.route('**/Admin/Accounts?*handler=Restore*', route => route.abort());
    await drawer.locator('#a-status').click();
    await page.locator('.modal[role=alertdialog] [data-account-confirm-accept]').click();
    await drawer.locator('#dr-banner', { hasText: 'We couldn’t confirm the change.' }).waitFor();
    assert.equal(await drawer.locator('#a-status').isDisabled(), true);
    await page.unroute('**/Admin/Accounts?*handler=Restore*');
    await drawer.locator('#dr-banner button', { hasText: 'Check current status' }).click();
    await drawer.locator('#dr-banner', { hasText: 'It wasn’t changed.' }).waitFor();
    assert.equal(await drawer.locator('#a-status').isDisabled(), false);

    // C-CMP-2: a lost session shows what wasn't saved before sign-in.
    await page.route('**/Admin/Accounts?*handler=Restore*', route => route.fulfill({ status: 200, contentType: 'text/html', headers: { 'X-Bingo-Post-Navigation': '/Account/Login?accessChanged=true' }, body: '<html>Sign in</html>' }));
    await drawer.locator('#a-status').click();
    await page.locator('.modal[role=alertdialog] [data-account-confirm-accept]').click();
    const notice = page.locator('.modal', { hasText: 'Your changes were not saved' });
    await notice.waitFor();
    assert.match(await notice.innerText(), /Restore ReviewWebsite/);
    assert.match(await notice.locator('a.btn-primary').getAttribute('href'), /accessChanged=true/);
    await notice.locator('button', { hasText: 'Keep editing' }).click();
    await page.unroute('**/Admin/Accounts?*handler=Restore*');
    await page.locator('.modal[role=alertdialog] [data-account-confirm-accept]').click();
    await drawer.locator('#dr-banner', { hasText: 'Account restored.' }).waitFor();

    // Closing returns to the directory entry and focuses the row link.
    await drawer.locator('#dr-cancel').click();
    await page.waitForFunction(() => !document.querySelector('.drawer') && !new URL(location.href).searchParams.has('account'));
    assert.equal(await page.evaluate(() => document.activeElement?.id), 'open-' + id);
    assert.deepEqual(errors, []);
    console.log('PASS accounts directory, drawer, reset link, confirmations, A2 gating and C-CMP-2');

    // Loading header: fixed words with a number-sized bar per count (Events tab-count pattern).
    const loadingSummary = await page.evaluate(() => { const t = document.querySelector('template[data-page-header-template="accounts"]').content; return { bars: t.querySelectorAll('.summary .ac-sk-count > .sk').length, text: t.querySelector('.summary').textContent.replace(/\s+/g, ' ').trim() }; });
    assert.deepEqual(loadingSummary, { bars: 2, text: 'accounts disabled' });
    // A16: sidebar swap Dashboard -> Accounts -> Dashboard -> Back leaves one live module.
    await page.locator('a[data-shell-link][href="/Admin/Index"], a[data-shell-link][href="/Admin"]').first().click();
    await page.waitForFunction(() => location.pathname.toLowerCase().startsWith('/admin') && !location.pathname.toLowerCase().includes('accounts') && !document.querySelector('[data-page-skeleton]'));
    await page.locator('a[data-shell-link][href="/Admin/Accounts/Index"]').click();
    await page.locator('[data-accounts-directory]').waitFor();
    assert.equal(await page.locator('[data-page-family="accounts"].page').count(), 1);
    await page.goBack(); await page.waitForFunction(() => !document.querySelector('[data-accounts-directory]') && !document.querySelector('[data-page-skeleton]'));
    await page.goForward(); await page.locator('[data-accounts-directory]').waitFor();
    await page.locator('[data-account-open]', { hasText: 'ReviewCaptain' }).click();
    await page.locator('.drawer #drawer-title', { hasText: 'ReviewCaptain' }).waitFor();
    assert.equal(await page.locator('.drawer').count(), 1, 'one drawer after repeated swaps');
    await page.keyboard.press('Escape'); await page.waitForFunction(() => !document.querySelector('.drawer'));
    assert.deepEqual(errors, []);
    console.log('PASS A16 swap and Back/Forward');

    // Transfer ownership (SuperAdmin): recipient + password, confirmation with typed username (AU24).
    const owner = await context.browser().newContext({ viewport: { width: 1280, height: 860 }, reducedMotion: 'reduce' });
    const ownerPage = await owner.newPage(); ownerPage.on('pageerror', error => errors.push(error.message));
    await ownerPage.goto(fixture.origin + '/Account/Login');
    await ownerPage.locator('#Input_Username').fill('ReviewOwner'); await ownerPage.locator('#Input_Password').fill(fixture.password);
    await Promise.all([ownerPage.waitForURL(url => !url.pathname.endsWith('/Account/Login')), ownerPage.locator('button[type=submit]').click()]);
    await ownerPage.goto(fixture.origin + '/Admin/Accounts');
    await ownerPage.locator('#transfer-btn').click();
    const dialog = ownerPage.locator('.modal.modal-form');
    await dialog.locator('#tm-to').waitFor();
    assert.equal(await ownerPage.evaluate(() => document.querySelector('.scroller').classList.contains('is-locked')), true, 'A8: the page behind the dialog is locked');
    await dialog.locator('[data-transfer-confirm]').click();
    assert.match(await dialog.innerText(), /Choose an account\./);
    const optionValue = await dialog.locator('#tm-to option', { hasText: 'ReviewWebsite' }).getAttribute('value');
    await dialog.locator('#tm-to').selectOption(optionValue);
    await dialog.locator('#tm-pw').fill('wrong-password');
    assert.match(await dialog.innerText(), /ReviewWebsite[\s\S]*Super Admin/);
    await dialog.locator('[data-transfer-confirm]').click();
    await dialog.locator('#tm-name').waitFor({ state: 'visible' });
    assert.match(await dialog.locator('.m-title').innerText(), /Transfer ownership to ReviewWebsite\?/);
    await dialog.locator('#tm-name').fill('ReviewOther');
    await dialog.locator('[data-transfer-confirm]').click();
    assert.match(await dialog.locator('#tm-name-err').innerText(), /This doesn’t match ReviewWebsite\./);
    await dialog.locator('#tm-name').fill('reviewwebsite');
    await dialog.locator('[data-transfer-confirm]').click();
    await dialog.locator('#tm-pw-err', { hasText: 'The current password is incorrect.' }).waitFor();
    assert.equal(await dialog.locator('#tm-pw').inputValue(), '', 'the password is cleared after a wrong password');
    assert.equal(await dialog.locator('#tm-to').inputValue(), optionValue, 'A6: the selected recipient is kept');
    await ownerPage.keyboard.press('Escape');
    await ownerPage.locator('.modal', { hasText: 'Discard unsaved changes?' }).waitFor();
    await ownerPage.locator('.modal', { hasText: 'Discard unsaved changes?' }).locator('[data-confirm-accept]').click();
    await ownerPage.waitForFunction(() => !document.querySelector('.modal'));
    assert.equal(await ownerPage.evaluate(() => document.querySelector('.scroller').classList.contains('is-locked')), false);
    await owner.close();
    assert.deepEqual(errors, []);
    console.log('PASS transfer dialog steps, AU24 typed username, wrong password and discard guard');
  } finally { await browser?.close(); await fixture.close(); }
})().catch(error => { console.error(error); process.exitCode = 1; });

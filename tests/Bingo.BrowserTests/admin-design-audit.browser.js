// T1 Audit: real page against the controlled UR fixture (PostgreSQL), both engines.
// Filters push history, menus and the More filters panel (A4), chips, the dropped-link notice
// (C-AUD-4), actor matching (C-AUD-3), hidden events marked (AU16) and the entry drawer.
const assert = require('node:assert/strict');
const path = require('node:path');
const { chromium, webkit } = require('playwright');
const { startFixture, login } = require('../../scripts/lib/admin-parity-fixture.cjs');
(async () => {
  const engine = process.env.PLAYWRIGHT_BROWSER === 'webkit' ? webkit : chromium;
  const fixture = await startFixture(process.cwd(), path.join(process.cwd(), 'artifacts/t1-audit-' + process.env.PLAYWRIGHT_BROWSER), { BINGO_PARITY_UR_PROFILE: 'live' });
  let browser;
  try {
    browser = await engine.launch({ headless: true, ...(engine === chromium ? { channel: process.env.PLAYWRIGHT_CHANNEL || 'chromium' } : {}) });
    const context = await browser.newContext({ viewport: { width: 1280, height: 860 }, reducedMotion: 'reduce' });
    const page = await login(context, fixture);
    const errors = []; page.on('pageerror', error => errors.push(error.message));
    const search = () => new URL(page.url()).searchParams;

    // C-AUD-4: unrecognised link parts are dropped with a notice; the URL is canonical.
    await page.goto(fixture.origin + '/Admin/Audit?type=spaceship&from=2027-13-40&actor=@ReviewOwner&EventId=x');
    await page.locator('[data-audit-directory]').waitFor();
    await page.locator('#au-notice', { hasText: 'Some filters in the link weren’t recognised.' }).waitFor();
    assert.equal(new URL(page.url()).search, '?actor=ReviewOwner');
    assert.equal(await page.locator('#actor-input').inputValue(), 'ReviewOwner');
    assert.equal(await page.locator('.filter-chip').count(), 1);
    assert.equal(await page.evaluate(() => document.scrollingElement.scrollHeight <= innerHeight), true, 'the document never scrolls');
    // U3-Q10: the summary is only the time zone line.
    assert.match(await page.locator('.page-head .summary').innerText(), /^Times in Copenhagen time \(UTC[+-]\d\d:\d\d\)$/);
    assert.equal(await page.locator('.page-head .summary > span').count(), 1);

    // Clear all, then the actor search (C-AUD-3: "@" ignored, case ignored), pushed to history.
    await page.locator('#clear-all').click();
    await page.waitForFunction(() => location.search === '' && !document.querySelector('.filter-chip') && !document.querySelector('[data-update-skeleton]') && document.querySelectorAll('[data-audit-row]').length > 5);
    assert.ok(await page.locator('[data-audit-row] .pill', { hasText: 'Automated' }).count() > 0, 'automated entries are marked');
    assert.ok(await page.locator('.actor.is-system', { hasText: 'System' }).count() > 0);
    // Brief 147: the record line is a sentence and Affected account is a column ("—" when none).
    assert.deepEqual(await page.locator('.au-tbl .th-row [role="columnheader"]').allInnerTexts(), ['When', 'Action', 'Event', 'Actor', 'Affected account', 'Recorded']);
    assert.equal(await page.locator('[data-audit-row]').first().locator('[role="cell"]').count(), 6);
    assert.ok(await page.locator('[data-audit-row] [role="cell"]:nth-child(5)', { hasText: '—' }).count() > 0, 'entries without an affected account show a dash');
    // Brief 147 item 4: the search applies while typing (250 ms debounce, no Enter); focus stays in
    // the field and fast typing sends only the last query.
    const actorRequests = [];
    page.on('request', request => { const url = new URL(request.url()); if (url.pathname === '/Admin/Audit' && url.searchParams.has('actor')) actorRequests.push(url.searchParams.get('actor')); });
    await page.locator('#actor-input').pressSequentially('@reviewowner', { delay: 25 });
    await page.waitForFunction(() => new URL(location.href).searchParams.get('actor') === 'reviewowner' && document.querySelector('.filter-chip') && !document.querySelector('[data-update-skeleton]'));
    assert.deepEqual(actorRequests, ['reviewowner'], 'fast typing sends only the last query');
    assert.equal(await page.evaluate(() => document.activeElement?.id), 'actor-input', 'focus stays in the field while the list updates');
    assert.equal(await page.locator('#actor-input').inputValue(), '@reviewowner', 'the typed text is kept');
    const actorRows = await page.locator('[data-audit-row]').count();
    assert.ok(actorRows > 0);
    assert.equal(await page.locator('[data-audit-row] .actor', { hasText: '@ReviewOwner' }).count(), actorRows);
    await page.goBack();
    await page.waitForFunction(() => location.search === '' && !document.querySelector('.filter-chip'));
    assert.equal(await page.locator('#actor-input').inputValue(), '');
    await page.goForward();
    await page.waitForFunction(() => new URL(location.href).searchParams.get('actor') === 'reviewowner' && document.querySelector('.filter-chip'));

    // Event menu: ordered as on Events with state hints; hidden events marked (AU16).
    await page.locator('.filter-chip').click();
    await page.waitForFunction(() => location.search === '' && !document.querySelector('.filter-chip') && !document.querySelector('[data-update-skeleton]'));
    await page.locator('#event-filter').click();
    const hidden = page.locator('#audit-event-menu .menu-item', { hasText: 'Hidden final review' });
    assert.match(await hidden.locator('.menu-hint').innerText(), /^Hidden · /);
    assert.equal(await page.locator('#audit-event-menu .menu-item', { hasText: 'Discarded' }).count(), 0, 'Q7: Discarded events are not in the menu');
    await hidden.click();
    await page.waitForFunction(() => new URL(location.href).searchParams.has('event') && document.querySelector('.filter-chip') && !document.querySelector('[data-update-skeleton]'));
    assert.match(await page.locator('#event-filter').innerText(), /Hidden final review/);
    assert.ok(await page.locator('[data-audit-row] .pill', { hasText: 'Hidden' }).count() > 0, 'hidden-event entries are listed and marked');

    // Action menu: an area (S11 adds Signups and Participants).
    await page.locator('#action-filter').click();
    assert.deepEqual(await page.locator('#audit-action-menu .menu-item .grow').allTextContents(), ['All actions', 'Accounts', 'Events', 'Signups', 'Participants', 'Teams', 'Draft', 'Board', 'Evidence', 'Catalogue', 'Specific action…']);
    await page.locator('#audit-action-menu .menu-item', { hasText: 'Events' }).click();
    // The URL is pushed before the read and the Event chip already exists, so wait for the
    // server render of this filter: clicking earlier can lose the press when the patch
    // replaces Clear all between mousedown and mouseup.
    await page.waitForFunction(() => new URL(location.href).searchParams.get('action') === 'event.' && new URL(document.querySelector('[data-audit-directory]').dataset.directoryCanonical, location.href).searchParams.get('action') === 'event.' && !document.querySelector('[data-update-skeleton]'));
    assert.deepEqual(await page.locator('.filter-chip').allInnerTexts(), ['Event: Hidden final review', 'Action: Events actions']);
    await page.locator('#clear-all').click();
    await page.waitForFunction(() => location.search === '' && !document.querySelector('.filter-chip') && !document.querySelector('[data-update-skeleton]'));

    // T1 review M2: unapplied panel edits are protected by the shared discard confirmation.
    await page.locator('#more-filters').click();
    await page.locator('#fpanel').waitFor();
    await page.keyboard.press('Escape');
    await page.waitForFunction(() => document.querySelector('#fpanel').hidden);
    assert.equal(await page.locator('.modal', { hasText: 'Discard unsaved changes?' }).count(), 0, 'no edits: Escape closes at once');
    await page.locator('#more-filters').click();
    await page.locator('#fp-type').selectOption('board');
    await page.keyboard.press('Escape');
    const discardDialog = page.locator('.modal', { hasText: 'Discard unsaved changes?' });
    await discardDialog.waitFor();
    await discardDialog.locator('[data-confirm-cancel]').click();
    await page.waitForFunction(() => !document.querySelector('[data-modal-host] .modal'));
    assert.equal(await page.locator('#fpanel').isVisible(), true, 'Keep editing keeps the panel open');
    assert.equal(await page.locator('#fp-type').inputValue(), 'board');
    const before = page.url();
    await page.locator('[data-audit-open]').first().click();
    await discardDialog.waitFor();
    assert.equal(await page.locator('.drawer').count(), 0, 'the outside click did not also open an entry');
    assert.equal(page.url(), before);
    await discardDialog.locator('[data-confirm-accept]').click();
    await page.waitForFunction(() => document.querySelector('#fpanel').hidden && !document.querySelector('[data-modal-host] .modal'));
    assert.equal(await page.locator('#fp-type').inputValue(), '', 'Discard resets the panel to the applied filters');

    // More filters: validation, presets, Apply; A4 reachable on a short viewport.
    await page.setViewportSize({ width: 1280, height: 360 });
    await page.locator('#more-filters').click();
    const panel = page.locator('#fpanel');
    await panel.waitFor();
    await panel.evaluate(node => Promise.all(node.getAnimations().map(animation => animation.finished)));
    const box = await panel.boundingBox();
    assert.ok(box.y + box.height <= 360 + 1, 'the panel stays inside a short viewport');
    await panel.locator('#fp-from').fill('31 Feb 2027');
    await panel.locator('#fp-apply').scrollIntoViewIfNeeded();
    await panel.locator('#fp-apply').click();
    assert.match(await panel.locator('#fp-date-err').innerText(), /Enter dates like 27 May 2027\./);
    await panel.locator('[data-audit-preset]', { hasText: 'Last 30 days' }).click();
    assert.equal(await panel.locator('[data-audit-preset]', { hasText: 'Last 30 days' }).getAttribute('aria-pressed'), 'true');
    await panel.locator('#fp-apply').click();
    await page.waitForFunction(() => { const p = new URL(location.href).searchParams; return p.has('from') && p.has('to') && document.querySelector('.filter-chip') && !document.querySelector('[data-update-skeleton]'); });
    assert.equal(await page.locator('#more-filters .filter-count').innerText(), '1');
    await page.setViewportSize({ width: 1280, height: 860 });

    // Entry drawer: pushes ?entry=, Back closes, Forward reopens, Newer/Older replace.
    const first = page.locator('[data-audit-open]').first();
    const firstId = await first.getAttribute('data-audit-open');
    await first.click();
    const drawer = page.locator('.drawer');
    await drawer.locator('#drawer-title').waitFor();
    assert.equal(search().get('entry'), firstId);
    assert.equal(await drawer.locator('#dr-newer').isDisabled(), true);
    const length = await page.evaluate(() => history.length);
    await drawer.locator('#dr-older').click();
    assert.notEqual(search().get('entry'), firstId);
    assert.equal(await page.evaluate(() => history.length), length, 'Newer/Older replace the URL');
    await drawer.locator('#dr-tech').click();
    assert.equal(await drawer.locator('#dr-tech-body').isVisible(), true);
    await page.goBack();
    await page.waitForFunction(() => !document.querySelector('.drawer') && !new URL(location.href).searchParams.has('entry'));
    await page.goForward();
    await page.locator('.drawer #drawer-title').waitFor();
    await page.keyboard.press('Escape');
    await page.waitForFunction(() => !document.querySelector('.drawer'));

    // Direct entry links: off-page entries open over the first page; unknown ones say so.
    await page.goto(fixture.origin + '/Admin/Audit?entry=00000000-0000-0000-0000-000000000001');
    await page.locator('.drawer', { hasText: 'This entry isn’t available' }).waitFor();
    assert.deepEqual(errors, []);
    console.log('PASS audit filters, history, menus, panel (A4), chips, notice, drawer and direct links');
  } finally { await browser?.close(); await fixture.close(); }
})().catch(error => { console.error(error); process.exitCode = 1; });

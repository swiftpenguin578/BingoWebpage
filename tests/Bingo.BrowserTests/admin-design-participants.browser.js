// U5 Participants: real page against the controlled parity fixture with the participant roster
// (BINGO_PARITY_PARTICIPANTS=1: cap 4, four confirmed = full, two waiting, one withdrawn), both engines.
// Replaces the fake-DOM tests of the retired event-manage.js dialogs (A10): the Add drawer (search, pick,
// eligibility, full-event capacity choice, discard guard, lost response, success toast + Show, no history
// entry), the participant drawer (URL, Back/Forward, withdrawn read-only, Discord name hidden, dirty guard,
// one Save) and the old detail URL redirect.
const assert = require('node:assert/strict');
const path = require('node:path');
const { chromium, webkit } = require('playwright');
const { startFixture, login } = require('../../scripts/lib/admin-parity-fixture.cjs');
(async () => {
  const engine = process.env.PLAYWRIGHT_BROWSER === 'webkit' ? webkit : chromium;
  const fixture = await startFixture(process.cwd(), path.join(process.cwd(), 'artifacts/u5-participants-' + process.env.PLAYWRIGHT_BROWSER), { BINGO_PARITY_PARTICIPANTS: '1' });
  let browser;
  try {
    browser = await engine.launch({ headless: true, ...(engine === chromium ? { channel: process.env.PLAYWRIGHT_CHANNEL || 'chromium' } : {}) });
    const context = await browser.newContext({ viewport: { width: 1280, height: 860 }, reducedMotion: 'reduce' });
    const page = await login(context, fixture);
    const errors = []; page.on('pageerror', error => errors.push(error.message));
    const eventId = fixture.events['autumn-bingo-2027'];
    const base = fixture.origin + '/Admin/Events/Participants/' + eventId;
    const drawer = page.locator('.drawer');
    const discard = page.locator('.modal', { hasText: 'Discard unsaved changes?' });
    const search = () => new URL(page.url()).search;

    await page.goto(base);
    await page.locator('[data-participant-row]').first().waitFor();
    assert.equal(await page.locator('[data-participant-row]').count() > 0, true);

    // ---- Add drawer: direct URL, replaceState, no history entry ----
    await page.goto(base + '?add=1');
    await drawer.locator('#drawer-title', { hasText: 'Add participant' }).waitFor();
    assert.equal(await drawer.locator('[data-ad-place][value="waiting"]').isChecked(), true, 'a full event defaults to the waiting list');
    assert.equal(await drawer.locator('[data-ad-cap-hint]').isHidden(), true);
    await page.keyboard.press('Escape');
    await page.waitForFunction(() => !document.querySelector('.drawer'));
    assert.equal(search(), '', 'closing an untouched Add drawer leaves the plain directory');

    // ---- Add drawer from the button ----
    await page.locator('#add-btn').click();
    await drawer.locator('#add-search').waitFor();
    assert.equal(new URL(page.url()).searchParams.get('add'), '1');
    await drawer.locator('#add-search').fill('zzznomatch');
    await drawer.locator('.res-empty').waitFor();
    await drawer.locator('#add-search').fill('dragon');
    const result = drawer.locator('#add-results button.res', { hasText: '@dragon.skim' });
    await result.waitFor();
    assert.match(await result.innerText(), /dragonskim/, 'B-Participants-5: results carry the Discord name');
    // A member already in the event is listed but disabled.
    await drawer.locator('#add-search').fill('kiwi');
    const member = drawer.locator('#add-results button.res', { hasText: '@kiwi.crab' });
    await member.waitFor(); assert.equal(await member.isDisabled(), true); assert.match(await member.innerText(), /Already in event/);
    await drawer.locator('#add-search').fill('dragon');
    await result.click();
    await drawer.locator('#add-accounts').waitFor();
    const skim = drawer.locator('#add-accounts label, #add-accounts .pick').filter({ hasText: 'Dragon Skim' }).first();
    assert.equal(await drawer.locator('[data-ad-username]').innerText(), '@dragon.skim');
    assert.match(await drawer.locator('#add-accounts').innerText(), /No saved EHB/, 'an account without saved EHB is ineligible with its reason');
    assert.equal(await drawer.locator('#add-accounts input[type=checkbox]:checked').count(), 1, 'the single eligible account is preselected');
    assert.equal(await drawer.locator('input[name="add-primary"]:checked').count(), 1, 'and is the primary');
    // Full event: choose the new place -> capacity hint; dirty close asks first.
    await drawer.locator('[data-ad-place][value="confirmed"]').check({ force: true });
    assert.match(await drawer.locator('[data-ad-cap-hint]').innerText(), /from 4 to 5/);
    await page.keyboard.press('Escape');
    await discard.waitFor();
    await discard.locator('[data-confirm-cancel]').click();
    assert.equal(await drawer.locator('[data-ad-place][value="confirmed"]').isChecked(), true, 'Keep editing keeps the choices');
    // Lost response: nothing is claimed; the current list is re-read.
    await drawer.locator('[data-ad-pay][value="paid"]').check({ force: true });
    await page.route(url => url.search.includes('handler=Add'), route => route.abort());
    await drawer.locator('[data-ad-submit]').click();
    await drawer.locator('[data-d-messages]', { hasText: 'We couldn’t confirm whether @dragon.skim was added' }).waitFor();
    await page.unrouteAll({ behavior: 'wait' });
    assert.equal(await drawer.locator('[data-ad-submit]').isDisabled(), false, 'the draft is kept and can be retried');
    // Discard really closes.
    await page.keyboard.press('Escape'); await discard.waitFor();
    await discard.locator('[data-confirm-accept]').click();
    await page.waitForFunction(() => !document.querySelector('.drawer'));
    assert.equal(search(), '');
    // Success: Confirm and add a place, Paid.
    await page.locator('#add-btn').click();
    await drawer.locator('#add-search').fill('dragon');
    await drawer.locator('#add-results button.res', { hasText: '@dragon.skim' }).click();
    await drawer.locator('[data-ad-picked]:not([hidden])').waitFor();
    await drawer.locator('[data-ad-place][value="confirmed"]').check({ force: true });
    await drawer.locator('[data-ad-pay][value="paid"]').check({ force: true });
    const historyBefore = await page.evaluate(() => history.length);
    await drawer.locator('[data-ad-submit]').click();
    const toast = page.locator('.toast', { hasText: 'Dragon Skim added to confirmed · capacity now 5' });
    await toast.waitFor();
    await page.waitForFunction(() => !document.querySelector('.drawer'));
    assert.equal(search(), '', 'no add parameter remains');
    assert.equal(await page.evaluate(() => history.length), historyBefore, 'Add leaves no history entry that could resubmit');
    await toast.locator('button', { hasText: 'Show' }).click();
    await page.waitForFunction(() => new URL(location.href).searchParams.get('q') === 'Dragon Skim');
    await page.locator('[data-participant-row]', { hasText: 'Dragon Skim' }).first().waitFor();

    // ---- Participant drawer ----
    await page.goto(base);
    await page.locator('[data-participant-row]').first().waitFor();
    const kiwi = page.locator('[data-participant-open]', { hasText: /kiwi/i }).first();
    const kiwiId = await kiwi.getAttribute('data-participant-open');
    await kiwi.click();
    await drawer.locator('#drawer-title').waitFor();
    await drawer.locator('[data-d-content]:not([hidden])').waitFor();
    assert.equal(new URL(page.url()).searchParams.get('participant'), kiwiId);
    const text = await drawer.innerText();
    assert.doesNotMatch(text, /kiwicrab/, 'U5-E1: the Discord display name is not shown in the drawer');
    assert.match(text, /When can you play\?/);
    const values = await drawer.locator('input, textarea').evaluateAll(nodes => nodes.map(node => node.value));
    assert.ok(values.includes('Evenings CET'), 'S1: custom answers are listed with their saved value');
    assert.ok(!values.some(value => /kiwicrab/i.test(value)), 'U5-E1: no input carries the Discord name');
    await page.goBack(); await page.waitForFunction(() => !document.querySelector('.drawer'));
    await page.goForward(); await drawer.locator('[data-d-content]:not([hidden])').waitFor();
    // Dirty: private note -> Escape asks; Keep editing; one Save closes with a toast.
    await drawer.locator('[data-d-note]').fill('Checked in the U5 browser test.');
    assert.equal(await drawer.locator('[data-d-save]').isDisabled(), false);
    await page.keyboard.press('Escape'); await discard.waitFor();
    await discard.locator('[data-confirm-cancel]').click();
    assert.equal(await drawer.locator('[data-d-note]').inputValue(), 'Checked in the U5 browser test.');
    await drawer.locator('[data-d-save]').click();
    await page.locator('.toast', { hasText: 'Changes to Kiwi Crab saved' }).waitFor();
    await page.waitForFunction(() => !document.querySelector('.drawer'));
    // Reopen: the saved note is read back from the server.
    await page.goto(base + '?participant=' + kiwiId);
    await drawer.locator('[data-d-content]:not([hidden])').waitFor();
    assert.equal(await drawer.locator('[data-d-note]').inputValue(), 'Checked in the U5 browser test.');
    // Unknown participant: a clear missing state, no crash.
    await page.goto(base + '?participant=00000000-0000-0000-0000-000000000001');
    await drawer.locator('.empty-title', { hasText: 'Participant not found' }).waitFor();
    // Withdrawn: read-only with the Restore hint.
    await page.goto(base + '?tab=withdrawn');
    const lumby = page.locator('[data-participant-open]').first();
    await lumby.waitFor(); await lumby.click();
    await drawer.locator('[data-d-withdrawn]:not([hidden])').waitFor();
    assert.equal(await drawer.locator('[data-d-account-rows] input[data-a-rsn]').count(), 0, 'accounts are read-only on a withdrawn participant');
    assert.equal(await drawer.locator('[data-d-add-account]').isHidden() || await drawer.locator('[data-d-accounts-edit]').isHidden(), true);
    assert.equal(await drawer.locator('[data-d-note]').isEnabled(), true, 'payment and the private note stay editable');

    // ---- Old detail URL redirects to the drawer ----
    await page.goto(fixture.origin + `/Admin/Events/Participant/${eventId}/Participants/${kiwiId}`);
    await page.waitForFunction(id => new URL(location.href).searchParams.get('participant') === id && /\/Admin\/Events\/Participants\//.test(location.pathname), kiwiId);
    await drawer.locator('[data-d-content]:not([hidden])').waitFor();
    assert.deepEqual(errors, [], 'no page errors');
  } finally {
    if (browser) await browser.close();
    await fixture.close();
  }
})().catch(error => { console.error(error); process.exit(1); });

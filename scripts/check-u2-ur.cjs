const assert = require('node:assert/strict');
const fs = require('node:fs'), path = require('node:path');
const {chromium, webkit} = require('playwright');
const {startFixture, login} = require('./lib/admin-parity-fixture.cjs');
const {settle} = require('./lib/admin-parity-compare.cjs');
const root = process.cwd(), output = path.join(root, 'artifacts/u2-ur-browser');
(async () => {
 const results = [];
 for (const profile of ['live', 'final-review']) {
  const fixture = await startFixture(root, path.join(output, profile), {BINGO_PARITY_UR_PROFILE:profile});
  let browser;
  try {
   for (const engine of (process.env.BINGO_PARITY_ENGINES || 'chromium,webkit').split(',')) {
    browser = await (engine === 'webkit' ? webkit.launch({headless:true}) : chromium.launch({headless:true,channel:process.env.PLAYWRIGHT_CHANNEL || 'chromium'}));
    const context = await browser.newContext({viewport:{width:1440,height:1000},reducedMotion:'reduce'});
    const page = await login(context, fixture), errors = [];
    page.on('pageerror', error => errors.push(error.message));
    page.setDefaultTimeout(10000);
    const navigate = async url => { assert.equal(await page.evaluate(url => window.AdminUI.navigate(url),url),true); await settle(page); };
    const ids = () => page.locator('[data-events-directory] .rows [data-event-id]').evaluateAll(rows => rows.map(row => row.dataset.eventId));
    const count = profile === 'live' ? 28 : 27;
    await page.goto(fixture.origin + '/Admin/Events?view=current');
    await page.waitForFunction(() => window.AdminUI && document.querySelector('[data-events-directory]'));
    const first = await ids(); assert.equal(first.length,25);
    await page.getByRole('button',{name:'Page 2',exact:true}).click();
    await page.waitForURL(url => url.searchParams.get('page') === '2'); await settle(page);
    const second = await ids(); assert.equal(second.length,count-25);
    assert.equal(new Set([...first,...second]).size,count);
    await navigate('/Admin/Events?view=past&phase=cancelled');
    const cancelled = page.locator('[data-events-directory] .rows [data-event-id]');
    assert.equal(await cancelled.count(),1); assert.match(await cancelled.textContent(),/3 confirmed.*when it was cancelled/s);
    await navigate('/Admin/Events?view=current&attention=1');
    assert.equal(await page.locator('[data-event-id="'+fixture.events['ur-draft']+'"] .attn-text').textContent(),'Start postponed');
    assert.equal(await page.locator('[data-event-id="'+fixture.events['ur-opening-failed']+'"] .attn-text').textContent(),'Signup opening failed');
    await navigate('/Admin/Events?view=current&search=No');
    assert.match(await page.locator('[data-events-directory]').textContent(),/No capacity set/);
    assert.match(await page.locator('[data-events-directory]').textContent(),/Not scheduled/);
    for (const culture of ['en','da']) {
     await page.locator('[name=culture][value='+culture+']').click();
     await page.waitForFunction(culture => document.documentElement.lang === culture,culture);
     await navigate('/Admin/Events?view=current&sort=identity&direction=asc');
     const names1 = await page.locator('[data-events-directory] .rows .name-btn').allTextContents();
     await page.getByRole('button',{name:culture === 'da' ? 'Side 2' : 'Page 2',exact:true}).click();
     await page.waitForURL(url => url.searchParams.get('page') === '2'); await settle(page);
     const names = names1.concat(await page.locator('[data-events-directory] .rows .name-btn').allTextContents());
     for (const name of ['alpha','Alpha','Ægir','Ørn','År']) assert.ok(names.includes(name));
     const alpha = names.indexOf('alpha'), Alpha = names.indexOf('Alpha'); assert.equal(Math.abs(alpha-Alpha),1);
     if (culture === 'da') assert.ok(names.indexOf('Ægir') < names.indexOf('Ørn') && names.indexOf('Ørn') < names.indexOf('År'));
     assert.equal(names.length,count);
    }
    await page.locator('[name=culture][value=en]').click();await page.waitForFunction(() => document.documentElement.lang === 'en');
    await navigate('/Admin');
    const current = page.locator('[data-history-event="'+fixture.events['ur-current']+'"]');
    assert.match(await current.textContent(),profile === 'live' ? /Live · provisional/ : /Final review/);
    assert.match(await page.locator('[data-history-event="'+fixture.events['ur-wom-unavailable']+'"] .name-line').textContent(),/Imported/);
    const sharedFirst = await page.locator('[data-history-event="'+fixture.events['ur-archived']+'"]').textContent();
    assert.ok(sharedFirst.includes('Amber Owls') && sharedFirst.includes('Silver Foxes'));
    const identity = '/Admin/Events/Identity/' + fixture.events['ur-draft'];
    for (let cycle=0;cycle<2;cycle++) {
     await navigate('/Admin/Events?view=current');
     const oldSearch = await page.locator('[data-directory-search]').elementHandle();
     const oldCreate = await page.locator('[data-create-event]').first().elementHandle();
     await navigate(identity);
     const before = page.url();
     await oldSearch.evaluate(input => {input.value='detached listener must be disposed';input.dispatchEvent(new KeyboardEvent('keydown',{key:'Enter',bubbles:true}));});
     await oldCreate.evaluate(button => {button.addEventListener('click',event => event.preventDefault(),{once:true});button.click();}); await settle(page);
     assert.equal(page.url(),before); assert.equal(await page.locator('#cm-name').count(),0);
     await navigate('/Admin');
     await page.goBack(); await page.waitForURL(url => url.pathname === identity);await settle(page);
     await page.goBack(); await page.waitForURL(url => url.pathname === '/Admin/Events');await settle(page);
     assert.equal((await ids()).length,25);
     await page.goForward();await page.waitForURL(url => url.pathname === identity);await settle(page);
     await page.goForward();await page.waitForURL(url => url.pathname === '/Admin');await settle(page);
     assert.equal(await page.locator('[data-dashboard]').count(),1);
     assert.equal(await page.locator('[data-admin-page-script]').count(),1);
     assert.equal(await page.locator('#cm-name').count(),0);
    }
    assert.deepEqual(errors,[]);
    results.push({profile,engine,passed:true,currentRows:count,a16Cycles:2,pageErrors:errors});
    console.log('PASS U2 UR '+profile+' '+engine+': paging, Confirmed cancellation, culture, attention, missing settings, import, shared first, phase and A16/disposal');
    await context.close();await browser.close();browser=null;
   }
  } finally {if(browser)await browser.close();await fixture.close();}
 }
 fs.mkdirSync(output,{recursive:true});fs.writeFileSync(path.join(output,'results.json'),JSON.stringify(results,null,2)+'\n');
 console.log('U2 UR: '+results.length+' passed, 0 failed');
})().catch(error => {console.error(error);process.exitCode=1;});

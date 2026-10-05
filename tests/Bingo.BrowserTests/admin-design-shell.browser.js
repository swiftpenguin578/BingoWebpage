const assert = require('node:assert/strict');
const fs = require('node:fs');
const { chromium } = require('playwright');
const root = 'src/Bingo.Web/wwwroot';
const shell = name => `<!doctype html><html class="dk-theme"><head><title>${name}</title><link rel="stylesheet" href="/css/admin-design-tokens.css"><link rel="stylesheet" href="/css/admin-design-components.css"><link rel="stylesheet" href="/css/admin-design-layout.css"></head><body data-admin-design data-navigation-enabled="${name === 'off' ? 'false' : 'true'}" data-close="Close" data-discard-title="Discard unsaved changes?" data-discard-description="Continuing will discard unsaved changes in this editor." data-keep-editing="Keep editing" data-discard="Discard" data-loading="Loading" data-load-error="The page could not be loaded." data-retry="Try again"><a id="skip" href="#main-content">Skip to main content</a><div class="app"><aside class="side" data-shell-sidebar><button data-menu-target="events-menu" id="event-opener" aria-expanded="false">Switch event</button><a data-shell-link id="sidebar-link" href="/Admin/Events/Identity/b">Other event</a></aside><button class="nav-scrim" data-side-scrim>Close nav</button><div class="main"><header class="topbar" data-shell-topbar><button class="menu-toggle" data-side-toggle>Open nav</button><a data-shell-link id="crumb-link" href="/Admin/Events/Identity/b">Event crumb</a></header><main id="main-content" class="scroller" data-page-region><div class="page"><h1 class="h1" tabindex="-1">${name}</h1><form id="draft"><input name="name" value="Original ${name}"></form><button id="layer-opener">Layer</button></div></main></div></div><div class="menu" id="events-menu" data-shell-menu hidden><a role="menuitem" data-shell-link id="switcher-link" href="/Admin/Events/Identity/b">B</a><button role="menuitem" id="last-menu">Last</button></div><div data-modal-host></div><div data-drawer-host></div><div class="toasts" data-toast-host></div><form hidden data-shell-antiforgery><input name="__RequestVerificationToken" value="fixture"></form><script src="/js/admin-design-shell.js" defer data-admin-shell-script></script><script type="module" data-admin-page-script src="/fixture-page.mjs"></script></body></html>`;
const pageModule = `let controller, timer, draft;
export function init(root, ui) { window.inits=(window.inits||0)+1;window.activeModules=(window.activeModules||0)+1;controller=new AbortController();document.addEventListener('fixture:ping',()=>window.pings=(window.pings||0)+1,{signal:controller.signal});timer=setInterval(()=>window.ticks=(window.ticks||0)+1,1000);draft=ui.trackForm(root.querySelector('form'));window.fixtureReady=true; }
export function dispose() { window.disposes=(window.disposes||0)+1;window.activeModules--;controller.abort();clearInterval(timer);draft.dispose();window.fixtureReady=false; }`;
(async () => {
  const browser = await chromium.launch({ headless: true, channel: process.env.PLAYWRIGHT_CHANNEL || 'chrome' });
  try {
    const page = await browser.newPage();
    const errors = [];
    page.on('pageerror', error => errors.push(error.message));
    let release;
    let failed = true, pageRequests = 0;
    await page.route('https://bingo.test/**', async route => {
      const url = new URL(route.request().url());
      if (url.pathname.startsWith('/js/') || url.pathname.startsWith('/css/')) return route.fulfill({ contentType: url.pathname.endsWith('.js') ? 'text/javascript' : 'text/css', body: fs.readFileSync(root + url.pathname, 'utf8') });
      if (url.pathname === '/fixture-page.mjs') return route.fulfill({ contentType: 'text/javascript', body: pageModule });
      if (url.pathname.endsWith('/unexpected')) return route.fulfill({ contentType: 'text/html', body: shell('unexpected').replace('<form hidden data-shell-antiforgery>', '<form hidden>') });
      if (url.pathname.endsWith('/old')) return route.fulfill({ contentType: 'text/html', body: '<title>Old layout</title><h1>Old layout</h1>' });
      if (url.pathname.endsWith('/failure') && failed) return route.fulfill({ status: 503, contentType: 'text/html', body: 'Unavailable' });
      if (url.pathname.endsWith('/slow')) await new Promise(resolve => { release = resolve; });
      pageRequests++;
      return route.fulfill({ contentType: 'text/html', body: shell(url.pathname.split('/').at(-1)) });
    });
    const start = async (name = 'a') => { await page.goto(`https://bingo.test/Admin/Events/Identity/${name}`); await page.waitForFunction(() => window.fixtureReady); };
    const navigate = async name => { await page.evaluate(name => window.AdminUI.navigate(`/Admin/Events/Identity/${name}`), name); await page.waitForFunction(() => window.fixtureReady); };
    const action = label => page.getByRole('alertdialog').getByRole('button', { name: label, exact: true });
    for (const dirty of [false, true]) {
      await start(); if (dirty) await page.locator('#draft input').fill('Fragment draft');
      const requests = pageRequests, length = await page.evaluate(() => { window.fragmentPops=0; addEventListener('popstate',()=>window.fragmentPops++); return history.length; });
      await page.locator('#skip').click(); await page.waitForFunction(()=>location.hash==='#main-content' && window.fragmentPops===1);
      await page.evaluate(()=>new Promise(resolve=>requestAnimationFrame(()=>requestAnimationFrame(resolve))));
      assert.equal(pageRequests, requests, 'skip link does not reload'); assert.equal(await page.getByRole('alertdialog').count(),0);
      assert.equal(await page.evaluate(()=>history.length),length+1,'only the native fragment entry exists');
      assert.equal(await page.locator('#draft input').inputValue(),dirty?'Fragment draft':'Original a');
    }
    await start(); await page.evaluate(()=>{window.retainedSide=document.querySelector('[data-shell-sidebar]');retainedSide.classList.add('is-collapsed');});
    await navigate('b');assert.equal(await page.evaluate(()=>retainedSide===document.querySelector('[data-shell-sidebar]')),true);assert.equal(await page.locator('[data-shell-sidebar]').evaluate(el=>el.classList.contains('is-collapsed')),true);
    await page.evaluate(()=>{const content=document.createElement('p');content.textContent='Read only';AdminUI.openLayer({kind:'drawer',title:'Read only',content});});
    await page.goBack();await page.waitForFunction(()=>document.title==='a'&&window.fixtureReady);assert.equal(await page.getByRole('dialog').count(),0);assert.equal(await page.locator('.main').evaluate(el=>el.inert),false);
    await navigate('b');await page.evaluate(()=>{const content=document.createElement('div');content.innerHTML='<input id="layer-draft" value="Original">';AdminUI.openLayer({kind:'drawer',title:'Draft',content});});await page.locator('#layer-draft').fill('Unsaved layer');
    await page.goBack();await action('Keep editing').click();await page.getByRole('alertdialog').waitFor({state:'hidden'});assert.equal(await page.locator('#layer-draft').inputValue(),'Unsaved layer');assert.ok(page.url().endsWith('/b'));
    await page.goBack();await action('Discard').click();await page.waitForFunction(()=>document.title==='a'&&window.fixtureReady);assert.equal(await page.getByRole('dialog').count(),0);
    await start();
    await page.locator('#event-opener').click();
    assert.equal(await page.locator('#switcher-link').evaluate(el => el === document.activeElement), true);
    await page.keyboard.press('End'); assert.equal(await page.locator('#last-menu').evaluate(el => el === document.activeElement), true);
    await page.keyboard.press('ArrowDown'); assert.equal(await page.locator('#switcher-link').evaluate(el => el === document.activeElement), true);
    await page.keyboard.press('Home'); await page.keyboard.press('Tab');
    assert.equal(await page.locator('#events-menu').isVisible(), false);
    assert.equal(await page.locator('#event-opener').evaluate(el => el === document.activeElement), true);

    for (const selector of ['#sidebar-link', '#crumb-link', '#switcher-link']) {
      await start();
      await page.locator('#draft input').fill('Retained draft');
      if (selector === '#switcher-link') await page.locator('#event-opener').click();
      await page.locator(selector).click();
      await action('Keep editing').waitFor({ state: 'visible' });
      assert.equal(await action('Keep editing').evaluate(el => el === document.activeElement), true);
      await action('Keep editing').click();
      await page.getByRole('alertdialog').waitFor({ state: 'hidden' });
      assert.equal(await page.locator('#draft input').inputValue(), 'Retained draft');
      assert.ok(page.url().endsWith('/a'));
      if (selector === '#switcher-link') await page.locator('#event-opener').click();
      await page.locator(selector).click(); await action('Discard').click();
      await page.waitForURL('**/b'); await page.waitForFunction(() => window.fixtureReady);
      assert.equal(await page.locator('.h1').evaluate(el => el === document.activeElement), true);
      await page.goBack(); await page.waitForFunction(() => document.title === 'a' && window.fixtureReady);
      assert.equal(await page.locator('#draft input').inputValue(), 'Original a', 'discarded draft cannot be resurrected by Back');
      await page.goForward(); await page.waitForFunction(() => document.title === 'b' && window.fixtureReady);
    }

    await start(); await navigate('b');
    await page.locator('#draft input').fill('Back draft');
    await page.evaluate(() => history.back()); await action('Keep editing').waitFor({ state: 'visible' });
    assert.ok(page.url().endsWith('/b'), 'Back URL restored before discard decision');
    await action('Keep editing').click(); await page.getByRole('alertdialog').waitFor({ state: 'hidden' });
    assert.equal(await page.locator('#draft input').inputValue(), 'Back draft');
    await page.evaluate(() => history.back()); await action('Discard').click();
    await page.waitForFunction(() => document.title === 'a' && window.fixtureReady);
    await page.locator('#draft input').fill('Forward draft');
    await page.evaluate(() => history.forward()); await action('Keep editing').click();
    await page.getByRole('alertdialog').waitFor({ state: 'hidden' });
    assert.ok(page.url().endsWith('/a'));
    assert.equal(await page.locator('#draft input').inputValue(), 'Forward draft');
    await page.evaluate(() => history.forward()); await action('Discard').click();
    await page.waitForFunction(() => document.title === 'b' && window.fixtureReady);
    assert.equal(await page.locator('#draft input').inputValue(), 'Original b');
    for (let i = 0; i < 4; i++) await navigate(i % 2 ? 'b' : 'a');
    const lifecycle = await page.evaluate(() => { const before = window.pings || 0; document.dispatchEvent(new Event('fixture:ping')); return { active: window.activeModules, balance: window.inits-window.disposes, listeners: window.pings-before }; });
    assert.deepEqual(lifecycle, { active: 1, balance: 1, listeners: 1 }, 'repeated swaps dispose old modules/listeners');

    await page.evaluate(() => { const schema={q:{default:'',valid:v=>v.length<20},page:{default:1,valid:v=>/^[1-9][0-9]*$/.test(v),parse:Number}}; window.queryProof={parsed:AdminUI.query.parse('?q=hello&page=bad&unknown=x',schema),built:AdminUI.query.build({q:'hello',page:1,unknown:'x'},schema)}; AdminUI.setUrl({q:'hello',page:1},schema); });
    assert.deepEqual(await page.evaluate(() => window.queryProof), { parsed: { q: 'hello', page: 1 }, built: '?q=hello' });
    const beforeLength = await page.evaluate(() => history.length);
    await page.evaluate(() => AdminUI.setUrl({record:'abc'},{record:{default:'',valid:v=>/^[a-z]+$/.test(v)}},{record:true}));
    assert.equal(await page.evaluate(() => history.length), beforeLength+1);

    await start();
    await page.locator('#layer-opener').focus();
    await page.evaluate(() => { const content=document.createElement('div');content.innerHTML='<button id="first">First</button><input value="kept"><button id="last">Last</button>';window.layer=AdminUI.openLayer({kind:'drawer',title:'Editor',content}); });
    await page.locator('#last').focus(); await page.keyboard.press('Tab'); assert.equal(await page.locator('#first').evaluate(el => el===document.activeElement),true);
    await page.keyboard.press('Shift+Tab'); assert.equal(await page.locator('#last').evaluate(el => el===document.activeElement),true);
    await page.locator('.scrim').dispatchEvent('click'); assert.equal(await page.getByRole('dialog').isVisible(),true,'outside click refuses any input layer');
    await page.evaluate(() => { window.confirmed=null;AdminUI.confirm({title:'Confirm',description:'Review',actionLabel:'Accept',cancelLabel:'Cancel'}).then(answer=>window.confirmed=answer); });
    await page.locator('.m-scrim').dispatchEvent('click'); assert.equal(await page.getByRole('alertdialog').isVisible(),true,'outside click never confirms/dismisses confirmation');
    await page.keyboard.press('Escape'); await page.getByRole('alertdialog').waitFor({state:'hidden'});
    assert.equal(await page.evaluate(()=>window.confirmed),false); assert.equal(await page.getByRole('dialog').isVisible(),true,'Escape closes top layer only');
    await page.keyboard.press('Escape'); await page.getByRole('dialog').waitFor({state:'hidden'});
    assert.equal(await page.locator('#layer-opener').evaluate(el=>el===document.activeElement),true);
    await page.evaluate(()=>{const content=document.createElement('p');content.textContent='Read only';AdminUI.openLayer({title:'Read only',content});});
    await page.locator('.m-scrim').dispatchEvent('click'); await page.getByRole('dialog').waitFor({state:'hidden'});

    await page.setViewportSize({width:390,height:844});
    await page.getByRole('button',{name:'Open nav',exact:true}).click();
    await page.locator('#sidebar-link').focus(); await page.keyboard.press('Tab');
    assert.equal(await page.locator('#event-opener').evaluate(el=>el===document.activeElement),true,'mobile sidebar traps focus');
    await page.keyboard.press('Escape');
    assert.equal(await page.getByRole('button',{name:'Open nav',exact:true}).getAttribute('aria-expanded'),'false');
    await page.getByRole('button',{name:'Open nav',exact:true}).click();
    await page.setViewportSize({width:1280,height:800});
    await page.waitForFunction(()=>!document.querySelector('.main').inert);
    assert.equal(await page.locator('[data-shell-sidebar]').evaluate(el=>el.classList.contains('is-mobile-open')),false);
    assert.equal(await page.locator('[data-side-scrim]').evaluate(el=>el.classList.contains('is-on')),false);

    await page.evaluate(()=>{void AdminUI.navigate('/Admin/Events/Identity/slow');});
    await page.locator('[data-page-skeleton="identity"]').waitFor({state:'visible'});
    while (!release) await new Promise(resolve=>setTimeout(resolve,10)); release();
    await page.waitForFunction(()=>document.title==='slow' && window.fixtureReady);
    await page.evaluate(()=>AdminUI.navigate('/Admin/Events/Identity/failure'));
    await page.getByRole('button',{name:'Try again',exact:true}).waitFor({state:'visible'});
    failed=false; await page.getByRole('button',{name:'Try again',exact:true}).click(); await page.waitForFunction(()=>document.title==='failure'&&window.fixtureReady);
    await page.evaluate(()=>{void AdminUI.navigate('/Admin/Events/Identity/old');}); await page.waitForURL('**/old');
    assert.equal(await page.locator('h1').textContent(),'Old layout','old-layout target falls back to full load');
    await start();
    await page.evaluate(()=>{void AdminUI.navigate('/Admin/Events/Identity/unexpected');}); await page.waitForURL('**/unexpected'); await page.waitForFunction(()=>window.fixtureReady);
    assert.equal(await page.evaluate(()=>window.inits),1,'unexpected response falls back to full load');
    await start('off'); await page.locator('#sidebar-link').click(); await page.waitForURL('**/b'); await page.waitForFunction(()=>window.fixtureReady);
    assert.equal(await page.evaluate(()=>window.inits),1,'off switch performs full page load');

    await page.clock.install();
    for(let i=0;i<3;i++) { await page.evaluate(()=>document.body.dataset.navigationEnabled='true'); await navigate(i%2?'a':'b'); }
    const ticksBefore=await page.evaluate(()=>window.ticks||0);
    await page.clock.fastForward(1000);
    assert.equal(await page.evaluate(()=>window.ticks)-ticksBefore,1,'disposed page timers stop after repeated swaps');
    await page.evaluate(()=>{window.busyDone=false;void AdminUI.busy(()=>Promise.resolve()).then(()=>window.busyDone=true);});
    await page.clock.fastForward(599); assert.equal(await page.evaluate(()=>window.busyDone),false);
    await page.clock.fastForward(1); assert.equal(await page.evaluate(()=>window.busyDone),true);
    await page.evaluate(()=>{window.busyDone=false;void AdminUI.busy(()=>Promise.resolve(),true).then(()=>window.busyDone=true);});
    await page.clock.fastForward(249); assert.equal(await page.evaluate(()=>window.busyDone),false);
    await page.clock.fastForward(1); assert.equal(await page.evaluate(()=>window.busyDone),true);
    await page.evaluate(()=>{window.busyDone=false;void AdminUI.busy(()=>new Promise(resolve=>window.finishRequest=resolve)).then(()=>window.busyDone=true);});
    await page.clock.fastForward(1000); assert.equal(await page.evaluate(()=>window.busyDone),false,'busy minimum never substitutes for response');
    await page.evaluate(()=>window.finishRequest()); assert.equal(await page.evaluate(()=>window.busyDone),true);
    await page.emulateMedia({reducedMotion:'reduce'});
    assert.equal(await page.evaluate(async()=>{const start=performance.now();await AdminUI.busy(()=>Promise.resolve());return performance.now()-start;}),0);
    await page.evaluate(()=>{for(let i=0;i<4;i++)AdminUI.toast(`Message ${i}`);});
    assert.equal(await page.locator('[data-toast]').count(),3);
    await page.locator('[data-toast]').last().dispatchEvent('mouseenter');
    await page.clock.fastForward(5000); assert.equal(await page.locator('[data-toast]').count(),1,'hover pauses toast expiry');
    await page.locator('[data-toast]').dispatchEvent('mouseleave'); await page.clock.fastForward(4501); assert.equal(await page.locator('[data-toast]').count(),0);
    await page.evaluate(()=>AdminUI.toast('With action',{actionLabel:'Open',action:()=>{}}));
    await page.clock.fastForward(6999); assert.equal(await page.locator('[data-toast]').count(),1);
    await page.clock.fastForward(1); assert.equal(await page.locator('[data-toast]').count(),0);
    assert.deepEqual(errors,[]);
    console.log('PASS shell focus/layers/menus, dirty sidebar/crumb/switcher/Back/Forward, module disposal, URL state, swap/failure/fallback/off, busy/motion/toasts');
  } finally { await browser.close(); }
})().catch(error=>{console.error(error);process.exitCode=1;});

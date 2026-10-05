const assert = require('node:assert/strict');
const fs = require('node:fs');
const { chromium, webkit } = require('playwright');
const root = 'src/Bingo.Web/wwwroot';
const templates = fs.readFileSync('tests/Bingo.BrowserTests/fixtures/admin-design-templates.html','utf8');
const shell = name => `<!doctype html><html class="dk-theme" lang="en"><head><title>${name}</title><link rel="stylesheet" href="/css/admin-design-tokens.css"><link rel="stylesheet" href="/css/admin-design-components.css"><link rel="stylesheet" href="/css/admin-design-layout.css"></head><body data-admin-design data-navigation-enabled="${name === 'off' ? 'false' : 'true'}" data-close="Close" data-discard-title="Discard unsaved changes?" data-discard-description="Continuing will discard unsaved changes in this editor." data-keep-editing="Keep editing" data-discard="Discard" data-loading="Loading" data-load-error-title="Couldn’t load this event" data-load-error="Check your connection and try again. Nothing was changed." data-collapse-navigation="Collapse sidebar" data-expand-navigation="Expand sidebar" data-close-navigation="Close navigation" data-retry="Try again"><a id="skip" href="#main-content">Skip to main content</a><div class="app"><aside class="side" data-shell-sidebar><nav data-shell-event-context data-selected-event-id="${name}"><span class="ev-name">${name}</span><span class="ev-meta-text">Draft · not announced</span></nav><button data-menu-target="events-menu" id="event-opener" aria-expanded="false">Switch event</button><a data-shell-link id="sidebar-link" href="/Admin/Events/Identity/b">Other event</a></aside><button class="nav-scrim" data-side-scrim>Close nav</button><div class="main"><header class="topbar" data-shell-topbar><span class="crumb-mid">${name}</span><form data-shell-language method="post" action="/language"><input type="hidden" name="returnUrl" value="/Admin/Events/Identity/${name}"><button type="submit" class="theme-opt is-on" name="culture" value="en" aria-pressed="true">EN</button><button type="submit" class="theme-opt" name="culture" value="da" aria-pressed="false">DA</button></form><button class="collapse-btn" data-side-toggle id="collapse-side">Collapse</button><button class="menu-toggle" data-side-toggle>Open nav</button><a data-shell-link id="crumb-link" href="/Admin/Events/Identity/b">Event crumb</a></header><main id="main-content" class="scroller" data-page-region><template data-page-loading-template="identity"><div class="card form-card" data-fixture-skeleton><div class="sk-block"><div class="sk"></div></div></div></template><div class="page"><header class="page-head"><h1 class="h1" tabindex="-1">${name}</h1><p class="summary">Fixture summary</p></header><form id="draft"><input name="name" value="Original ${name}"></form><button id="layer-opener">Layer</button></div></main></div></div><div class="menu" id="events-menu" data-shell-menu hidden><a role="menuitem" data-shell-link id="switcher-link" data-event-id="b" data-event-name="b" data-event-stage="Draft" data-event-when="not announced" data-event-tone="tone-draft" href="/Admin/Events/Identity/b">B</a><a role="menuitem" data-shell-link data-event-id="failure" data-event-name="failure" data-event-stage="Draft" data-event-when="not announced" data-event-tone="tone-draft" href="/Admin/Events/Identity/failure">Failure</a><button role="menuitem" id="last-menu">Last</button></div>${templates}<div data-modal-host></div><div data-drawer-host></div><div class="toasts" data-toast-host></div><form hidden data-shell-antiforgery><input name="__RequestVerificationToken" value="fixture"></form><script src="/js/admin-design-shell.js" defer data-admin-shell-script></script><script type="module" data-admin-page-script src="/fixture-page.mjs"></script></body></html>`;
const pageModule = `let controller, timer, draft;
export function init(root, ui) { window.inits=(window.inits||0)+1;window.activeModules=(window.activeModules||0)+1;controller=new AbortController();document.addEventListener('fixture:ping',()=>window.pings=(window.pings||0)+1,{signal:controller.signal});timer=setInterval(()=>window.ticks=(window.ticks||0)+1,1000);draft=ui.trackForm(root.querySelector('form'));window.fixtureReady=true; }
export function dispose() { window.disposes=(window.disposes||0)+1;window.activeModules--;controller.abort();clearInterval(timer);draft.dispose();window.fixtureReady=false; }`;
(async () => {
  const browser = await (process.env.PLAYWRIGHT_BROWSER === 'webkit' ? webkit.launch({headless:true}) : chromium.launch({ headless: true, channel: process.env.PLAYWRIGHT_CHANNEL || 'chrome' }));
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
      if (['document','fetch'].includes(route.request().resourceType())) pageRequests++;
      return route.fulfill({ contentType: 'text/html', body: shell(url.pathname.split('/').at(-1)) });
    });
    const start = async (name = 'a') => { await page.goto(`https://bingo.test/Admin/Events/Identity/${name}`); await page.waitForFunction(() => window.fixtureReady); };
    const navigate = async name => { await page.evaluate(name => window.AdminUI.navigate(`/Admin/Events/Identity/${name}`), name); await page.waitForFunction(() => window.fixtureReady); };
    const action = label => page.getByRole('alertdialog').getByRole('button', { name: label, exact: true });
    await start();
    await page.evaluate(() => {
      const link=document.querySelector('#sidebar-link');link.dataset.collapsedTitle='Overview';
      const arrow=document.createElementNS('http://www.w3.org/2000/svg','path');arrow.dataset.collapseArrow='';document.querySelector('#collapse-side').append(arrow);
    });
    await page.locator('#collapse-side').click();
    assert.equal(await page.locator('#sidebar-link').getAttribute('title'),'Overview');
    assert.equal(await page.locator('#collapse-side').getAttribute('title'),'Expand sidebar');
    assert.equal(await page.locator('[data-collapse-arrow]').evaluate(el=>el.classList.contains('is-flip')),true);
    await page.locator('#collapse-side').click();
    assert.equal(await page.locator('#sidebar-link').getAttribute('title'),null);
    assert.equal(await page.locator('#collapse-side').getAttribute('title'),'Collapse sidebar');
    assert.equal(await page.locator('[data-collapse-arrow]').evaluate(el=>el.classList.contains('is-flip')),false);
    await page.evaluate(() => {
      const menu=document.querySelector('#events-menu'), list=document.createElement('div');list.className='design-event-options';
      for(let i=0;i<20;i++){const row=document.createElement('button');row.className='menu-item';row.role='menuitemradio';row.setAttribute('aria-checked',String(i===19));row.textContent='Event '+i;list.append(row);}
      menu.replaceChildren(list);
    });
    await page.locator('#event-opener').click();
    assert.equal(await page.locator('.design-event-options').evaluate(el=>el.scrollTop>0),true);
    assert.equal(await page.evaluate(()=>document.activeElement.textContent),'Event 19');
    for (const dirty of [false, true]) {
      await start(); if (dirty) await page.locator('#draft input').fill('Fragment draft');
      const requests = pageRequests, length = await page.evaluate(() => { window.fragmentPops=0; addEventListener('popstate',()=>window.fragmentPops++); return history.length; });
      await page.locator('#skip').click(); await page.waitForFunction(()=>location.hash==='#main-content' && window.fragmentPops===1);
      await page.evaluate(()=>new Promise(resolve=>requestAnimationFrame(()=>requestAnimationFrame(resolve))));
      assert.equal(pageRequests, requests, 'skip link does not reload'); assert.equal(await page.getByRole('alertdialog').count(),0);
      assert.equal(await page.evaluate(()=>history.length),length+1,'only the native fragment entry exists');
      assert.equal(await page.locator('#draft input').inputValue(),dirty?'Fragment draft':'Original a');
    }
    for (const dirty of [false,true]) {
      await start();await page.locator('#skip').click();await page.waitForFunction(()=>location.hash==='#main-content');
      await page.locator('#event-opener').click();await page.locator('#switcher-link').click();await page.waitForFunction(()=>document.title==='b'&&window.fixtureReady);
      if(dirty)await page.locator('#draft input').fill('Fragment Back draft');
      const requests=pageRequests,length=await page.evaluate(()=>{window.backPops=0;addEventListener('popstate',()=>window.backPops++);return history.length;});
      await page.goBack();
      if(dirty){
        await action('Keep editing').waitFor();assert.equal(await page.getByRole('alertdialog').count(),1);assert.ok(page.url().endsWith('/b'));
        await action('Keep editing').click();await page.getByRole('alertdialog').waitFor({state:'hidden'});
        assert.equal(await page.locator('#draft input').inputValue(),'Fragment Back draft');assert.ok(page.url().endsWith('/b'));assert.equal(pageRequests,requests);assert.equal(await page.evaluate(()=>history.length),length);assert.equal(await page.evaluate(()=>window.backPops),2);
        await page.goBack();await action('Discard').waitFor();assert.equal(await page.getByRole('alertdialog').count(),1);await action('Discard').click();
      }
      await page.waitForFunction(()=>document.title==='a'&&window.fixtureReady&&location.hash==='#main-content');
      await page.evaluate(()=>new Promise(resolve=>requestAnimationFrame(()=>requestAnimationFrame(resolve))));
      assert.equal(pageRequests,requests+1,'Back loads the first event exactly once');assert.equal(await page.evaluate(()=>window.backPops),dirty?5:1,'only requested traversals occur');assert.equal(await page.evaluate(()=>history.length),length);assert.equal(await page.getByRole('alertdialog').count(),0);assert.equal(await page.locator('#draft input').inputValue(),'Original a');
    }
    // A pre-existing foreign stateless fragment also reloads once, never assigns its current URL.
    await start();await page.locator('#skip').click();await page.evaluate(()=>history.replaceState(null,'',location.href));await navigate('b');
    const foreignRequests=pageRequests,foreignLength=await page.evaluate(()=>history.length);
    await page.goBack();await page.waitForFunction(()=>document.title==='a'&&window.fixtureReady&&location.hash==='#main-content');
    assert.equal(pageRequests,foreignRequests+1);assert.equal(await page.evaluate(()=>history.length),foreignLength);
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
    await page.locator('#events-menu').waitFor({state:'hidden'});
    assert.equal(await page.locator('#events-menu').isVisible(), false);
    assert.equal(await page.locator('#event-opener').evaluate(el => el === document.activeElement), true);

    await page.locator('#collapse-side').click();assert.equal(await page.locator('#collapse-side').getAttribute('aria-label'),'Expand sidebar');
    await page.locator('#event-opener').click();
    const sidePosition=await page.evaluate(()=>{const r=document.querySelector('#event-opener').getBoundingClientRect(),m=document.querySelector('#events-menu');return{actual:[m.style.left,m.style.top],expected:[`${Math.round(Math.max(8,Math.min(r.right+8,innerWidth-m.offsetWidth-8)))}px`,`${Math.round(Math.max(8,r.top))}px`]};});assert.deepEqual(sidePosition.actual,sidePosition.expected);
    const closing=await page.evaluate(()=>{document.querySelector('#event-opener').click();const m=document.querySelector('#events-menu');return{closing:m.classList.contains('is-closing'),hidden:m.hidden};});assert.deepEqual(closing,{closing:true,hidden:false});await page.locator('#events-menu').waitFor({state:'hidden'});
    await page.locator('#collapse-side').click();assert.equal(await page.locator('#collapse-side').getAttribute('aria-label'),'Collapse sidebar');

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
    assert.equal(await page.locator('[data-page-skeleton] [data-fixture-skeleton]').count(),1);assert.equal(await page.locator('[data-page-skeleton]').getAttribute('data-skeleton-layout'),'page');
    while (!release) await new Promise(resolve=>setTimeout(resolve,10)); release();
    await page.waitForFunction(()=>document.title==='slow' && window.fixtureReady);
    release=null;await page.evaluate(()=>{void AdminUI.navigate('/Admin/Events/Schedule/slow');});await page.locator('[data-page-skeleton="schedule"]').waitFor({state:'visible'});assert.equal(await page.locator('[data-page-skeleton]').getAttribute('data-skeleton-layout'),'generic');assert.equal(await page.locator('[data-page-skeleton] .sk').count(),7);while(!release)await new Promise(resolve=>setTimeout(resolve,10));release();await page.waitForFunction(()=>window.fixtureReady&&!document.querySelector('[data-page-skeleton]'));
    await page.evaluate(()=>AdminUI.navigate('/Admin/Events/Identity/failure'));
    await page.getByRole('button',{name:'Try again',exact:true}).waitFor({state:'visible'});
    assert.equal(await page.locator('.empty-title').textContent(),'Couldn’t load this event');assert.equal(await page.locator('.empty-text').textContent(),'Check your connection and try again. Nothing was changed.');assert.equal(await page.locator('.empty-ic svg circle').count(),1);
    failed=false; await page.getByRole('button',{name:'Try again',exact:true}).click(); await page.waitForFunction(()=>document.title==='failure'&&window.fixtureReady);
    await start('a');await navigate('b');assert.equal(await page.locator('[data-shell-event-context]').getAttribute('data-selected-event-id'),'b');failed=true;
    await page.evaluate(()=>AdminUI.navigate('/Admin/Events/Identity/failure'));
    assert.equal(await page.locator('.ev-name').textContent(),'failure');assert.equal(await page.locator('.crumb-mid').textContent(),'failure');
    assert.equal(await page.locator('#draft input').inputValue(),'Original b','failed overlay retains original page');
    await page.goBack();await page.waitForFunction(()=>document.title==='a'&&!document.querySelector('[data-page-skeleton]'));
    assert.equal(await page.locator('.ev-name').textContent(),'a');assert.equal(await page.locator('.crumb-mid').textContent(),'a');assert.equal(await page.locator('#draft input').inputValue(),'Original a');
    await navigate('b');release=null;await page.evaluate(()=>{void AdminUI.navigate('/Admin/Events/Identity/slow');});await page.locator('[data-page-skeleton]').waitFor();
    await page.goBack();await page.waitForFunction(()=>document.title==='a'&&!document.querySelector('[data-page-skeleton]'));
    assert.equal(await page.locator('.ev-name').textContent(),'a');assert.equal(await page.locator('#draft input').inputValue(),'Original a');
    while(!release)await new Promise(resolve=>setTimeout(resolve,10));release();release=null;
    await start('a');
    await page.evaluate(()=>{window.retained=document.querySelector('#draft');window.beforeDispose=window.disposes||0;void AdminUI.navigate('/Admin/Events/Identity/slow');});
    await page.locator('[data-page-skeleton]').waitFor();
    assert.equal(await page.evaluate(()=>window.retained.isConnected),true,'loading retains original DOM');
    assert.equal(await page.evaluate(()=>window.disposes||0),await page.evaluate(()=>window.beforeDispose),'loading does not dispose');
    assert.equal(await page.locator('#draft').evaluate(el=>!!el.closest('[inert][aria-hidden="true"]')),true);
    assert.equal(await page.locator('[data-page-skeleton] .page-head').count(),1);
    while(!release)await new Promise(resolve=>setTimeout(resolve,10));release();
    await page.waitForFunction(()=>!document.querySelector('[data-page-skeleton]'));
    assert.equal(await page.evaluate(()=>window.retained.isConnected),false);assert.equal(await page.evaluate(()=>window.disposes),1);
    await start('a');
    await page.evaluate(()=>window.addEventListener('beforeunload',()=>sessionStorage.setItem('fallbackProof',JSON.stringify({disposed:window.disposes||0,content:!!document.querySelector('#draft'),hidden:!!document.querySelector('#draft').closest('[hidden],[inert],[aria-hidden="true"]'),overlay:!!document.querySelector('[data-page-skeleton]'),name:document.querySelector('.ev-name').textContent}))));
    await page.evaluate(()=>{void AdminUI.navigate('/Admin/Events/Identity/old');}); await page.waitForURL('**/old');
    assert.equal(await page.locator('h1').textContent(),'Old layout','old-layout target falls back to full load');
    assert.deepEqual(await page.evaluate(()=>JSON.parse(sessionStorage.getItem('fallbackProof'))),{disposed:0,content:true,hidden:false,overlay:false,name:'a'});
    await page.goBack();await page.waitForFunction(()=>window.fixtureReady);assert.equal(await page.locator('#draft input').inputValue(),'Original a');
    await page.evaluate(()=>{window.restoreMarker=true;window.dispatchEvent(new PageTransitionEvent('pageshow',{persisted:true}));});await page.waitForFunction(()=>window.fixtureReady&&!window.restoreMarker);assert.equal(await page.locator('#draft input').inputValue(),'Original a');
    await start();
    await page.evaluate(()=>{void AdminUI.navigate('/Admin/Events/Identity/unexpected');}); await page.waitForURL('**/unexpected'); await page.waitForFunction(()=>window.fixtureReady);
    assert.equal(await page.evaluate(()=>window.inits),1,'unexpected response falls back to full load');
    await start('off'); await page.locator('#sidebar-link').click(); await page.waitForURL('**/b'); await page.waitForFunction(()=>window.fixtureReady);
    assert.equal(await page.evaluate(()=>window.inits),1,'off switch performs full page load');

    await page.clock.install({time:new Date("2026-10-05T00:00:00Z")});
    for(let i=0;i<3;i++) { await page.evaluate(()=>document.body.dataset.navigationEnabled='true'); await navigate(i%2?'a':'b'); }
    await page.clock.pauseAt(new Date("2026-10-05T01:00:00Z"));
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

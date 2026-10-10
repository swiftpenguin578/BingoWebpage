process.env.BINGO_PARITY_ENGINES = process.env.PLAYWRIGHT_BROWSER || 'chromium';
// Real authenticated Razor + owned PostgreSQL.
const assert=require('node:assert/strict'),fs=require('node:fs'),path=require('node:path');
const {chromium,webkit}=require('playwright');
const {startFixture,login}=require('../../scripts/lib/admin-parity-fixture.cjs');
const {settle,classInventory}=require('../../scripts/lib/admin-parity-compare.cjs');
const root=process.cwd(),output=path.join(root,'artifacts/u2-events');
(async()=>{
 const fixture=await startFixture(root,output),results=[];let browser;
 try{
  const directory=fixture.directory;
  for(const engine of(process.env.BINGO_PARITY_ENGINES||'chromium,webkit').split(',')){
   browser=await(engine==='webkit'?webkit.launch({headless:true}):chromium.launch({headless:true,channel:process.env.PLAYWRIGHT_CHANNEL||'chromium'}));
   const context=await browser.newContext({viewport:{width:1440,height:1000},reducedMotion:'reduce'});
   const app=await login(context,fixture),errors=[];
   app.on('pageerror',e=>errors.push(e.message));app.setDefaultTimeout(10000);
   await app.goto(fixture.origin+'/Admin/Events');await app.waitForFunction(()=>window.AdminUI&&document.querySelector('[data-events-directory]'));await app.evaluate(()=>document.fonts.ready);
   // Brief78: phase colours.
   const badgeClasses={1:'badge-neutral',2:'badge-success',3:'badge-info',4:'badge-accent',5:'badge-warning',6:'badge-done',7:'badge-outline',8:'badge-outline'};
   const dotTones={1:'tone-draft',2:'tone-open',3:'tone-closed',4:'tone-live',5:'tone-review',6:'tone-done',7:'tone-draft',8:'tone-draft'};
   for(const event of directory.Events){
    assert.equal(await app.locator('.row[data-event-id="'+event.Id+'"] .badge').getAttribute('class'),'badge '+badgeClasses[event.State]);
    const option=app.locator('[data-event-tone][data-event-id="'+event.Id+'"]');
    if(event.State<=5)assert.equal(await option.getAttribute('data-event-tone'),dotTones[event.State]);
   }
   results.push({name:engine+'-phase-badge-and-switcher-tones',passed:true});
   for(const width of[1440,390])for(const theme of['light','dark']){
    await app.setViewportSize({width,height:1000});
    if(width<=860)await app.locator('.menu-toggle[data-side-toggle]').click();await app.locator('[data-theme="'+theme+'"]').click();if(width<=860)await app.locator('.collapse-btn[data-side-toggle]').click();
    assert.deepEqual((await classInventory(app)).undefined,[],engine+' '+width+' '+theme+': every class used on the page is defined in shipped CSS');
   }
   await app.setViewportSize({width:1440,height:1000});
   for(const width of[1440,1280,861,860,640,390]){
    await app.setViewportSize({width,height:1000});
    await app.goto(fixture.origin+'/Admin/Events/Identity/'+fixture.events['autumn-bingo-2027']);await app.locator('[data-identity-editor]').waitFor();
    const target=fixture.origin+'/Admin/Events?view=past&phase=archived&search=Spring&sort=identity&direction=desc&page=1';
    let release;const held=new Promise(resolve=>release=resolve);
    await app.route(target,async route=>{await held;await route.continue();});
    await app.evaluate(url=>{void window.AdminUI.navigate(url)},target);
    await app.locator('[data-events-pending]').waitFor();
    await app.waitForFunction(()=>getComputedStyle(document.querySelector('.ev-tbl')).getPropertyValue('--table-min').trim()==='990px'||getComputedStyle(document.querySelector('.ev-tbl')).getPropertyValue('--table-min').trim()==='900px');
    await settle(app);
    const boxes=()=>app.locator('[data-page-region]').evaluate(main=>{
     const scope=main.querySelector('[data-page-skeleton]')||main;
     return ['.page-head','.h1','.summary','.head-actions','.toolbar','.tabs','.search','.filter-btn','.th-row','.tabs .tab-count'].flatMap(selector=>[...scope.querySelectorAll(selector)].map((e,i)=>({key:selector+i,...Object.fromEntries(['x','y','width','height'].map(k=>[k,e.getBoundingClientRect()[k]]))})));
    });
    assert.deepEqual(await app.locator('[data-page-skeleton] .summary > span').allTextContents().then(items=>items.map(text=>text.trim())),['live','upcoming or in setup'],'approved count summary keeps fixed words during loading');
    assert.equal(await app.locator('[data-page-skeleton] .summary .tab-count[data-pending-count] > .sk').count(),2);
    assert.equal(await app.locator('[data-page-skeleton] .summary button').count(),0,'attention state waits for data');
    const before=await boxes();assert.equal(await app.locator('[data-pending-view=past]').isChecked(),true);
    assert.equal(await app.locator('[data-pending-search]').inputValue(),'Spring');assert.equal(await app.locator('[data-pending-column=identity]').getAttribute('aria-sort'),'descending');
    assert.equal(await app.locator('[data-pending-count]').evaluateAll(nodes=>nodes.every(e=>!e.textContent.trim()&&e.querySelector('.sk'))),true);
    release();await app.locator('[data-page-skeleton]').waitFor({state:'detached'});
    await app.waitForFunction(()=>getComputedStyle(document.querySelector('.ev-tbl')).getPropertyValue('--table-min').trim()==='990px'||getComputedStyle(document.querySelector('.ev-tbl')).getPropertyValue('--table-min').trim()==='900px');
    await settle(app);const after=await boxes();
    // Brief74 5a: reservation belongs only to loading; following content follows
    // the real loaded summary height, not a fixed position.
    const delta=after[0].height-before[0].height;
    assert.equal(before.length,after.length);for(let i=0;i<before.length;i++)for(const k of['x','y','width','height']){
     if(k==='height'&&['.page-head0','.summary0'].includes(before[i].key))continue;
     if(k==='width'&&['.h10','.summary0'].includes(before[i].key))continue; // fixed count loading group vs natural loaded width
     const flow=k==='y'&&!['.page-head0','.h10','.summary0'].includes(before[i].key)?delta:0;
     assert.ok(Math.abs(before[i][k]+flow-after[i][k])<=1,engine+' '+width+' '+before[i].key+' '+k+': '+JSON.stringify({before,after,delta}));
    }
    fs.writeFileSync(path.join(output,engine+'-'+width+'-query-positions.json'),JSON.stringify({before,after,delta},null,2));
    results.push({name:engine+'-'+width+'-loading-query-position-parity',passed:true});await app.unroute(target);
   }
   await app.setViewportSize({width:1440,height:1000});await app.goto(fixture.origin+'/Admin/Events');await app.locator('[data-events-directory]').waitFor();
   await app.locator('.th-btn').first().click();await app.waitForFunction(()=>document.querySelector('[data-sort-status]')?.textContent.includes('Event'));
   assert.equal(new URL(app.url()).searchParams.get('sort'),'identity');assert.equal(await app.locator('[aria-sort=ascending]').count(),1);
   await app.locator('#search-input').fill('DOES NOT MATCH');await app.locator('.empty-title').waitFor();assert.equal(await app.locator('.empty-title').textContent(),'No matching events');
   await app.getByRole('button',{name:'Clear filters',exact:true}).click();await app.locator('.row').first().waitFor();
   await app.locator('#phase-btn').click();await app.getByRole('menuitemradio',{name:'Live',exact:true}).click();await app.waitForFunction(()=>new URL(location.href).searchParams.get('phase')==='live');assert.equal(await app.locator('.row').count(),1);
   await app.goto(fixture.origin+'/Admin/Events?view=past&phase=live&page=999&bad=value');await app.locator('.page-banner').waitFor();assert.equal(await app.locator('.row').count(),3);
   await app.locator('[name=culture][value=da]').click();await app.waitForFunction(()=>document.documentElement.lang==='da');
   assert.equal(await app.locator('#search-input').getAttribute('placeholder'),'Søg efter eventnavne');assert.doesNotMatch(await app.locator('[data-events-directory]').textContent(),/No matching|Finished|No capacity|Sorted by/);
   await app.locator('[name=culture][value=en]').click();await app.waitForFunction(()=>document.documentElement.lang==='en');
   let fail=true;await app.route('**/Admin/Events?fail=1',route=>fail?route.abort():route.continue());
   await app.evaluate(url=>window.AdminUI.navigate(url),fixture.origin+'/Admin/Events?fail=1');
   await app.locator('[data-load-retry]').waitFor();assert.equal(await app.locator('.empty-title').last().textContent(),'Couldn’t load events');
   assert.equal(await app.locator('[data-page-skeleton] .sk').count(),0);assert.equal(await app.locator('[data-page-skeleton] .summary').textContent(),'');
   assert.equal(await app.locator('[data-pending-count]').evaluateAll(nodes=>nodes.every(e=>!e.textContent.trim())),true);assert.equal(await app.locator('[data-pending-search]').isEnabled(),true);
   fail=false;await app.locator('[data-load-retry]').click();await app.waitForFunction(()=>!document.querySelector('[data-page-skeleton]'));
   const identity=fixture.origin+'/Admin/Events/Identity/'+fixture.events['autumn-bingo-2027'];
   for(let i=0;i<3;i++){
    await app.evaluate(url=>window.AdminUI.navigate(url),fixture.origin+'/Admin');await app.locator('[data-dashboard]').waitFor();
    await app.evaluate(url=>window.AdminUI.navigate(url),identity);await app.locator('[data-identity-editor]').waitFor();
    await app.evaluate(url=>window.AdminUI.navigate(url),fixture.origin+'/Admin/Events');await app.locator('[data-events-directory]').waitFor();
   }
   await app.goBack();await app.locator('[data-identity-editor]').waitFor();await app.goBack();await app.locator('[data-dashboard]').waitFor();await app.goForward();await app.locator('[data-identity-editor]').waitFor();await app.goForward();await app.locator('[data-events-directory]').waitFor();
   assert.equal(await app.locator('[data-page-skeleton]').count(),0);assert.deepEqual(errors,[]);results.push({name:engine+'-filters-sort-da-retry-A16',passed:true});
   await browser.close();browser=null;
  }
 }finally{await browser?.close();await fixture.close();fs.writeFileSync(path.join(output,'results.json'),JSON.stringify(results,null,2));}
 const faultFixture=await startFixture(root,path.join(output,'attention-hidden'),{BINGO_PARITY_DIRECTORY_FAULT:'1'});
 try{
  for(const engine of(process.env.BINGO_PARITY_ENGINES||'chromium,webkit').split(',')){
   browser=await(engine==='webkit'?webkit.launch({headless:true}):chromium.launch({headless:true,channel:process.env.PLAYWRIGHT_CHANNEL||'chromium'}));
   const context=await browser.newContext({viewport:{width:1440,height:1000},reducedMotion:'reduce'}),app=await login(context,faultFixture);
   await app.goto(faultFixture.origin+'/Admin/Events');await app.locator('[data-events-directory]').waitFor();
   assert.match(await app.locator('.page-banner').textContent(),/Some attention counts could not be loaded/);assert.equal(await app.locator('.row').count(),22);
   assert.doesNotMatch(await app.locator('.summary').textContent(),/Nothing needs attention/);
   await app.goto(faultFixture.origin+'/Admin/Events?filter=hidden');await app.locator('[data-events-directory]').waitFor();
   assert.deepEqual(await app.locator('.name-btn').allTextContents(),['Alpha newer hidden','Zulu older hidden']);
   assert.match(await app.locator('.row .sub').first().textContent(),/^Hidden /);
   assert.equal(await app.locator('.name-btn').first().getAttribute('href'),'/Admin/Events/Manage/'+faultFixture.events['hidden-new']+'?hidden=true');
   assert.equal(new URL(app.url()).searchParams.get('view'),'hidden');assert.equal(await app.locator('[data-admin-events-control]').count(),0);
   results.push({name:engine+'-partial-attention-hidden-rendered',passed:true});await browser.close();browser=null;
  }
 }finally{await browser?.close();await faultFixture.close();fs.writeFileSync(path.join(output,'results.json'),JSON.stringify(results,null,2));}
 const failed=results.filter(r=>!r.passed);console.log('U2 Events: '+(results.length-failed.length)+' passed, '+failed.length+' failed');process.exitCode=failed.length?1:0;
})().catch(e=>{console.error(e);process.exitCode=1;});

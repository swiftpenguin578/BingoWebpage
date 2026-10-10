// The repository JS runner executes this proof in both Chromium and WebKit.
process.env.BINGO_PARITY_ENGINES = process.env.PLAYWRIGHT_BROWSER === 'webkit' ? 'webkit' : 'chromium';
// Owned PostgreSQL + real authenticated Razor rendering. Never calls a user database.
const assert=require('node:assert/strict'),fs=require('node:fs'),path=require('node:path');
const {chromium,webkit}=require('playwright');
const {startFixture,login}=require('../../scripts/lib/admin-parity-fixture.cjs');
const {settle,classInventory}=require('../../scripts/lib/admin-parity-compare.cjs');
const root=process.cwd(),output=path.join(root,'artifacts/u2-dashboard');
(async()=>{
 const fixture=await startFixture(root,output),results=[];let browser;
 try{
  const data=fixture.dashboard;
  for(const engine of (process.env.BINGO_PARITY_ENGINES||'chromium,webkit').split(',')){
   browser=await(engine==='webkit'?webkit.launch({headless:true}):chromium.launch({headless:true,channel:process.env.PLAYWRIGHT_CHANNEL||'chromium'}));
   const context=await browser.newContext({viewport:{width:1440,height:1000},reducedMotion:'reduce'});
   const app=await login(context,fixture),errors=[];
   app.on('pageerror',error=>errors.push(error.message)); app.setDefaultTimeout(10000);
   await app.goto(fixture.origin+'/Admin');await app.waitForFunction(()=>window.AdminUI&&document.querySelector('[data-dashboard]'));await app.evaluate(()=>document.fonts.ready);
   for(const theme of ['light','dark']){
    await app.locator('[data-theme="'+theme+'"]').click();
    assert.deepEqual((await classInventory(app)).undefined,[],engine+' '+theme+': every class used on the page is defined in shipped CSS');
   }
   const sortLength=await app.evaluate(()=>history.length), sortRequests=[];
   app.on('request',request=>{if(request.resourceType()==='document'&&new URL(request.url()).pathname==='/Admin')sortRequests.push(request.url());});
   const bar=app.locator('[data-chart-bar]').first();await bar.focus();await app.locator('[data-chart-tip].is-on').waitFor();
   assert.match(await app.locator('[data-chart-tip].is-on').textContent(),/1 teams/);await app.keyboard.press('Escape');assert.equal(await app.locator('[data-chart-tip].is-on').count(),0);
   await bar.focus();await app.keyboard.press('ArrowRight');assert.equal(await app.locator('[data-chart-bar]').nth(1).evaluate(e=>e===document.activeElement),true);
   await app.locator('[data-dashboard-sort]').nth(1).focus();
   await app.evaluate(()=>document.querySelector('[data-page-region]').scrollTop=200);
   const sortPosition=await app.evaluate(()=>document.querySelector('[data-page-region]').scrollTop);
   await app.keyboard.press('Enter');await app.waitForFunction(()=>document.querySelector('[data-sort-status]')?.textContent.includes('Players'));
   assert.equal(await app.evaluate(()=>history.length),sortLength,'sort replaces URL, never pushes');
   assert.equal(sortRequests.length,0,'sort never full-loads');
   assert.equal(await app.locator('#dashboard-sort-Players').evaluate(e=>e===document.activeElement),true);
   assert.equal(await app.evaluate(()=>document.querySelector('[data-page-region]').scrollTop),sortPosition);
   assert.equal(await app.locator('.tbl.hist .rows').evaluate(e=>e.classList.contains('swap-a')),true);
   assert.equal(await app.locator('.tbl.hist .row .td:nth-child(2) .hint').count(),0,'no history teams tooltip');
   assert.equal(new URL(app.url()).searchParams.get('sort'),'Players');assert.equal(await app.locator('[aria-sort=descending]').textContent(),'Players');
   await app.locator('#dashboard-sort-Winner').click();
   assert.equal(await app.locator('.tbl.hist .rows').evaluate(e=>e.classList.contains('swap-b')&&!e.classList.contains('swap-a')),true);
   assert.equal(new URL(app.url()).searchParams.get('direction'),'asc','Winner starts ascending');
   assert.equal(await app.locator('#dashboard-sort-Winner').evaluate(e=>e.closest('.th').getAttribute('aria-sort')),'ascending');
   assert.equal(await app.locator('.tbl.hist .row .td:nth-child(2) [tabindex]').count(),0,'Players adds no tab stop');
   assert.equal(await app.locator('.tbl.hist .row .td:nth-child(3) [tabindex]').count(),data.History.filter(row=>!row.ApprovedSubmissions.IsAvailable).length,'only unavailable Approved values are tab stops');
   assert.equal(await app.locator('.tbl.hist .c-ehb .hint.muted[tabindex="0"]').count(),data.History.length,'EHB hints and muted values remain');
   assert.match(await app.locator('.chart + .panel-foot').textContent(),/^Each person counted once per event\. Tracking by website account starts with/);
   assert.doesNotMatch(await app.locator('.recap .fact-sub').allTextContents().then(values=>values.join(' ')),/measured evidence only/);
   assert.equal(await app.locator('.tbl.hist .c-ehb .hint').first().getAttribute('aria-label'),'Wise Old Man wasn’t linked to this event.');
   await app.locator('[name=culture][value=da]').click();await app.waitForFunction(()=>document.documentElement.lang==='da');
   assert.equal(await app.locator('#part-title').textContent(),'Deltagelse pr. event');assert.deepEqual(await app.locator('[aria-label="Nøgletal"] .stat-label').allTextContents(),['Afholdte events','Unikke deltagere','Samlede deltagelser','Godkendte indsendelser']);
   assert.doesNotMatch(await app.locator('[data-dashboard]').textContent(),/Approved submissions|Not recorded|First time|measured evidence only/);
   await app.locator('[name=culture][value=en]').click();await app.waitForFunction(()=>document.documentElement.lang==='en');
   // First-visit and repeated A16 loading/retry; no read JSON endpoint is used.
   let fail=true;
   await app.route('**/Admin?probe=1',async route=>{if(fail)return route.abort();return route.continue();});
   await app.evaluate(async url=>{window.retiredBar=document.querySelector('[data-chart-bar]');await window.AdminUI.navigate(url);},fixture.origin+'/Admin?probe=1');
   await app.locator('[data-page-skeleton=dashboard] [data-load-retry]').waitFor();assert.equal(await app.locator('.empty-title').last().textContent(),'Couldn’t load statistics');
   assert.equal(await app.locator('[data-page-skeleton] .next-event').count(),0,'failure has neither real card nor placeholder');
   fail=false;await app.locator('[data-load-retry]').click();await app.waitForFunction(()=>!document.querySelector('[data-page-skeleton]'));
   await app.evaluate(()=>{window.retiredBar.click();});assert.equal(new URL(app.url()).pathname,'/Admin','disposed bar cannot navigate');
   const identity=fixture.origin+'/Admin/Events/Identity/'+fixture.events['autumn-bingo-2027'];
   const headerBox=()=>app.evaluate(()=>{
    const page=document.querySelector('[data-page-skeleton]')||document.querySelector('[data-page-region] > .page:not([hidden])');
    const rect=selector=>{const b=page.querySelector(selector).getBoundingClientRect();return {x:b.x,y:b.y,width:b.width,height:b.height};};
    return {head:rect('.page-head'),title:rect('.h1'),summary:rect('.summary'),card:rect('.next-event'),first:rect('.card')};
   });
   for(const width of [1280,860,390]){
    await app.setViewportSize({width,height:1000});await app.goto(identity);await app.waitForFunction(()=>window.AdminUI);
    await app.evaluate(()=>document.querySelector('[data-page-region]').scrollTop=0);
    let proceed;const waiting=new Promise(resolve=>proceed=resolve),target=fixture.origin+'/Admin?headerProbe='+width;
    await app.route(target,async route=>{await waiting;await route.continue();});
    await app.evaluate(url=>{window.headerNavigation=window.AdminUI.navigate(url);},target);
    await app.locator('[data-dashboard-loading-card]').waitFor();
    await app.waitForFunction(()=>getComputedStyle(document.querySelector('[data-page-skeleton] .dash-grid')).display==='grid');
    await settle(app);const loading=await headerBox();proceed();
    await app.evaluate(()=>window.headerNavigation);await app.locator('[data-dashboard]').waitFor();await settle(app);const loaded=await headerBox();
    // Q-H1: no fixed anchors. Geometry follows the summary/card flow.
    for(const [state,value] of Object.entries({loading,loaded})){
      const groupHeight=value.title.height+6+value.summary.height;
      const height=width<=860?groupHeight+24+value.card.height:Math.max(groupHeight,value.card.height);
      const close=(actual,expected)=>assert.ok(Math.abs(actual-expected)<=0.1,engine+' '+width+' '+state+' header flow');
      close(value.head.height,height);close(value.title.y,value.head.y+(width<=860?0:height-groupHeight));
      close(value.card.y,value.head.y+(width<=860?groupHeight+24:height-value.card.height));
      close(value.first.y,value.head.y+height+20);
    }
    results.push({name:engine+'-header-loading-loaded-'+width,passed:true,loading,loaded});
    await app.unroute(target);
   }
   await app.setViewportSize({width:1440,height:1000});
   for(let i=0;i<3;i++){await app.evaluate(url=>window.AdminUI.navigate(url),identity);await app.locator('[data-identity-editor]').waitFor();await app.evaluate(url=>window.AdminUI.navigate(url),fixture.origin+'/Admin');await app.locator('[data-dashboard]').waitFor();}
   await app.goBack();await app.locator('[data-identity-editor]').waitFor();await app.goForward();await app.locator('[data-dashboard]').waitFor();
   assert.equal(await app.locator('[data-page-skeleton]').count(),0);assert.deepEqual(errors,[]);results.push({name:engine+'-keyboard-sort-da-retry-disposal-history',passed:true});
   await browser.close();browser=null;
  }
 }finally{await browser?.close();await fixture.close();fs.writeFileSync(path.join(output,'results.json'),JSON.stringify(results,null,2));}
 const failed=results.filter(r=>!r.passed);console.log('U2 Dashboard: '+(results.length-failed.length)+' passed, '+failed.length+' failed');process.exitCode=failed.length?1:0;
})().catch(error=>{console.error(error);process.exitCode=1;});

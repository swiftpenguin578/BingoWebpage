// Owned PostgreSQL + real authenticated Razor rendering. Never calls a user database.
const assert=require('node:assert/strict'),fs=require('node:fs'),path=require('node:path');
const {chromium,webkit}=require('playwright');
const {startFixture,referencePage,login}=require('./lib/admin-parity-fixture.cjs');
const {comparator,settle}=require('./lib/admin-parity-compare.cjs');
const root=process.cwd(),output=path.join(root,'artifacts/u2-dashboard');
const date=at=>at?.slice(0,10);
const P=(name,selector,options={})=>[name,selector,selector,{required:true,box:{x:1,y:1,width:1,height:1},...options}];
(async()=>{
 const fixture=await startFixture(root,output),results=[];let browser;
 try{
  const data=fixture.dashboard;
  const events=Object.fromEntries(data.Chart.map(point=>{
   const row=data.History.find(row=>row.EventId===point.EventId);
   return [point.EventId,{id:point.EventId,name:point.EventName,short:point.EventName,start:date(point.ActualStartedAt),end:date(point.ActualEndedAt||data.AsOf),source:point.IsHistoricalImport?'imported':'platform',state:point.State===4?'live':point.Provisional?'review':'finalized',players:point.Participants.Value,returning:point.ReturningWebsiteParticipants.Value,teams:point.TeamCount,approvedMeasured:row.ApprovedSubmissions.IsAvailable,approved:row.ApprovedSubmissions.IsAvailable?row.ApprovedSubmissions.Value:0,winner:row.Winners.map(w=>w.TeamName).join(' · ')||'Not recorded',board:null,ehb:null}];
  }));
  const card=data.CurrentEvent;
  const next=card?{name:card.EventName,phase:'Live',tone:'live',when:'ends '+new Date(card.NextDate).toLocaleDateString('en-GB',{day:'numeric',month:'short',timeZone:card.Timezone}),count:card.ConfirmedParticipants,cap:card.Capacity}:null;
  const dataset={full:{label:'Controlled U2 fixture',today:date(data.AsOf),events:data.Chart.map(p=>p.EventId),next,accounts:{total:data.Community.WebsiteAccounts.Value,newSince:data.Community.NewWebsiteAccounts.Value,loggedIn:data.Community.LoggedInWebsiteAccounts.Value}}};
  // Only sample-data inputs and approved D8 latest-ended semantics are normalized
  // in memory; the frozen reference file and all its styles/layout remain untouched.
  const transform=source=>source.replace(/const EVENTS = \{[\s\S]*?\n\};/, 'const EVENTS = '+JSON.stringify(events)+';')
    .replace(/const DATASETS = \{[\s\S]*?\n\};/,'const DATASETS = '+JSON.stringify(dataset)+';')
    .replace("provisional: e.state === 'review'","provisional: e.state === 'review' || e.state === 'live'")
    .replace("const e = evs[evs.length - 1]; if (!e) return { facts: [] };","evs = evs.filter(e => e.state !== 'live'); const e = evs[evs.length - 1]; if (!e) return { facts: [] };")
    .replace("label: 'Next event'","label: 'Current event'")
    // D14 unknown platform measurements reuse the reference's unavailable
    // metric primitive; data-only availability is absent from its sample model.
    .replace("hasHint: !!x.hint, plain: !x.hint", "hasHint: x.label === 'Approved submissions' && !e.approvedMeasured || !!x.hint, plain: !(x.label === 'Approved submissions' && !e.approvedMeasured) && !x.hint")
    .replace("const f = facts.map", "if (!e.approvedMeasured) Object.assign(facts[1], { value: '—', sub: 'not available' }); const f = facts.map")
    .replace(/<div class="meter" title="\{\{next.meterTitle\}\}">[\s\S]*?<\/div>\s*<button class="btn btn-sm"/, '<span class="meter-label">'+card.ConfirmedParticipants+' confirmed · No capacity set</span><button class="btn btn-sm"');
  for(const engine of (process.env.BINGO_PARITY_ENGINES||'chromium,webkit').split(',')){
   browser=await(engine==='webkit'?webkit.launch({headless:true}):chromium.launch({headless:true,channel:process.env.PLAYWRIGHT_CHANNEL||'chromium'}));
   const context=await browser.newContext({viewport:{width:1440,height:1000},reducedMotion:'reduce'}),refContext=await browser.newContext({viewport:{width:1440,height:1000},reducedMotion:'reduce'});
   const app=await login(context,fixture),ref=await referencePage(refContext,fixture,root,'Dashboard.dc.html',transform),errors=[];
   app.on('pageerror',error=>errors.push(error.message)); app.setDefaultTimeout(10000);
   await app.goto(fixture.origin+'/Admin');await app.waitForFunction(()=>window.AdminUI&&document.querySelector('[data-dashboard]'));await app.evaluate(()=>document.fonts.ready);
   const compare=comparator(output,results);
   const pairs=[P('headline card','[aria-label="Headline statistics"]',{required:false}),P('stat strip','.stat-strip'),P('stat label','.stat-label',{text:true}),P('stat value','.stat-value',{box:{x:1,y:1,height:1}}),P('participation card','.dash-grid > .card:first-child',{box:{x:1,y:1,width:1}}),P('panel title','#part-title',{text:true}),P('chart','.chart'),P('chart plot','.chart-plot'),P('bar','.bar-col'),P('stack','.bar-stack'),P('x labels','.chart-x'),P('recap','.recap',{box:{x:1,y:1,width:1}}),P('recap name','.recap-name',{text:true}),P('recap date','.recap-when',{text:true}),P('history head','.hist-head'),P('table','.tbl.hist',{box:{x:1,y:1,width:1}}),P('table header','.th',{text:false}),P('sort control','.th-btn',{text:true}),P('community section','.section-head')].filter(p=>p[0]!=='headline card');
   for(const theme of ['light','dark']){
    await app.locator('[data-theme="'+theme+'"]').click();await ref.evaluate(theme=>new Promise(resolve=>window.__parityReference.setState({theme},resolve)),theme);
    await compare(engine+'-'+theme,app,ref,pairs);
   }
   const bar=app.locator('[data-chart-bar]').first();await bar.focus();await app.locator('[data-chart-tip].is-on').waitFor();
   assert.match(await app.locator('[data-chart-tip].is-on').textContent(),/1 teams/);await app.keyboard.press('Escape');assert.equal(await app.locator('[data-chart-tip].is-on').count(),0);
   await bar.focus();await app.keyboard.press('ArrowRight');assert.equal(await app.locator('[data-chart-bar]').nth(1).evaluate(e=>e===document.activeElement),true);
   await app.locator('[data-dashboard-sort]').nth(1).click();await app.waitForFunction(()=>document.querySelector('[data-sort-status]')?.textContent.includes('Players'));
   assert.equal(new URL(app.url()).searchParams.get('sort'),'Players');assert.equal(await app.locator('[aria-sort=descending]').textContent(),'Players');
   await app.locator('[name=culture][value=da]').click();await app.waitForFunction(()=>document.documentElement.lang==='da');
   assert.equal(await app.locator('#part-title').textContent(),'Deltagelse pr. event');assert.equal(await app.locator('.stat-label').first().textContent(),'Afholdte events');
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
    return {head:rect('.page-head'),summary:rect('.summary'),card:rect('.next-event'),first:rect('.card')};
   });
   for(const width of [1440,1280,861,860,390]){
    await app.setViewportSize({width,height:1000});await app.goto(identity);await app.waitForFunction(()=>window.AdminUI);
    await app.evaluate(()=>document.querySelector('[data-page-region]').scrollTop=0);
    let proceed;const waiting=new Promise(resolve=>proceed=resolve),target=fixture.origin+'/Admin?headerProbe='+width;
    await app.route(target,async route=>{await waiting;await route.continue();});
    await app.evaluate(url=>{window.headerNavigation=window.AdminUI.navigate(url);},target);
    await app.locator('[data-dashboard-loading-card]').waitFor();
    await app.waitForFunction(()=>getComputedStyle(document.querySelector('[data-page-skeleton] .dash-grid')).display==='grid');
    await settle(app);const loading=await headerBox();proceed();
    await app.evaluate(()=>window.headerNavigation);await app.locator('[data-dashboard]').waitFor();await settle(app);const loaded=await headerBox();
    for(const key of ['head','summary','card'])for(const coordinate of ['x','y','width','height'])assert.ok(Math.abs(loaded[key][coordinate]-loading[key][coordinate])<=1,engine+' '+width+' '+key+'.'+coordinate+' '+JSON.stringify({loading,loaded}));
    for(const coordinate of ['x','y','width'])assert.ok(Math.abs(loaded.first[coordinate]-loading.first[coordinate])<=1);
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

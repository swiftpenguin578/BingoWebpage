// Q-H1 reference-first loaded/loading parity and flow proof with durable positions.
const assert=require('node:assert/strict');
const fs=require('node:fs'),path=require('node:path');
const {chromium,webkit}=require('playwright');
const {startFixture,login,referencePage}=require('./lib/admin-parity-fixture.cjs');
const {settle}=require('./lib/admin-parity-compare.cjs');
async function until(page,fn){for(let i=0;i<200;i++)if(await page.evaluate(fn))return;throw Error('Microtask checkpoint not reached');}
(async()=>{
 const records=[];
 const engineNames=(process.env.BINGO_PARITY_ENGINES||'chromium,webkit').split(',');
 assert.ok(engineNames.length&&engineNames.every(name=>['chromium','webkit'].includes(name)),'Explicit supported header-proof engines required');
 const engines=engineNames.map(name=>name==='webkit'?webkit:chromium);
 const runKey=engineNames.join('-');
 for(const narrow of(process.env.HEADER_DIAG==='1'?[true]:[false,true])){
  const fixture=await startFixture(process.cwd(),path.join(process.cwd(),'artifacts/u2-header-'+runKey+'-'+narrow),narrow?{BINGO_PARITY_NARROW_CARD:'1'}:{});
  try{for(const engine of engines){
   const browser=await engine.launch({headless:true,...(engine===chromium?{channel:process.env.PLAYWRIGHT_CHANNEL||'chromium'}:{})});
   try{
    const data=fixture.dashboard,date=at=>at?.slice(0,10),card=data.CurrentEvent;
    const events=Object.fromEntries(data.Chart.map(point=>{
      const row=data.History.find(row=>row.EventId===point.EventId);
      return[point.EventId,{id:point.EventId,name:point.EventName,short:point.EventName,start:date(point.ActualStartedAt),end:date(point.ActualEndedAt||data.AsOf),source:point.IsHistoricalImport?'imported':'platform',state:point.State===4?'live':point.Provisional?'review':'finalized',players:point.Participants.Value,returning:point.ReturningWebsiteParticipants.Value,teams:point.TeamCount,approved:0,winner:'Not recorded',board:null,ehb:null}];
    }));
    const dataset={full:{label:'Controlled U2 fixture',today:date(data.AsOf),events:data.Chart.map(p=>p.EventId),next:{name:card.EventName,phase:'Live',tone:'live',when:'ends '+new Date(card.NextDate).toLocaleDateString('en-GB',{day:'numeric',month:'short',timeZone:card.Timezone}),count:card.ConfirmedParticipants,cap:card.Capacity},accounts:{total:data.Community.WebsiteAccounts.Value,newSince:data.Community.NewWebsiteAccounts.Value,loggedIn:data.Community.LoggedInWebsiteAccounts.Value}}};
    const transform=source=>source
      .replace("label: 'Next event'","label: 'Current event'")
      .replace(/const EVENTS = \{[\s\S]*?\n\};/,'const EVENTS = '+JSON.stringify(events)+';')
      .replace(/const DATASETS = \{[\s\S]*?\n\};/,'const DATASETS = '+JSON.stringify(dataset)+';')
      .replace("const ready = !S.loading && !S.loadError;\n    const last = evs[evs.length - 1];","const ready = !S.loading && !S.loadError;\n    const last = evs.filter(e => e.state !== 'live').sort((a,b)=>a.end.localeCompare(b.end)).at(-1);")
      .replace(/<div class="meter" title="\{\{next.meterTitle\}\}">[\s\S]*?<\/div>\s*<button class="btn btn-sm"/,'<span class="meter-label">'+card.ConfirmedParticipants+' confirmed · No capacity set</span><button class="btn btn-sm"');
    const referenceContext=await browser.newContext({viewport:{width:1440,height:1000},reducedMotion:'reduce'});
    const ref=await referencePage(referenceContext,fixture,process.cwd(),'Dashboard.dc.html',transform);
    const context=await browser.newContext({viewport:{width:1440,height:1000},reducedMotion:'reduce'});
    const signed=await login(context,fixture);await signed.close();
    const html=await(await context.request.get(fixture.origin+'/Admin')).text();
    for(const width of(process.env.HEADER_DIAG==='1'?[861]:[390,861,1280,1440])){
     const page=await context.newPage();await page.setViewportSize({width,height:1000});
     await page.goto(fixture.origin+'/Admin/Events/Identity/'+fixture.events['autumn-bingo-2027']);
     await page.evaluate(async()=>{await import(document.querySelector('script[data-admin-page-script]').src);await document.fonts.ready;});
     await page.clock.install({time:new Date('2030-01-01T00:00:00Z')});await page.clock.pauseAt(new Date('2030-01-01T00:01:00Z'));
     await page.evaluate(html=>{
      window.timerLog=[];const timer=window.setTimeout;window.setTimeout=(fn,ms,...args)=>{timerLog.push(ms);return timer(fn,ms,...args);};
      const fetch=window.fetch;window.fetch=(url,options)=>String(url).includes('headerMeasure')?new Promise(resolve=>{window.waiting=true;window.fulfill=()=>resolve(new Response(html,{headers:{'Content-Type':'text/html'}}));}):fetch(url,options);
      window.done=false;AdminUI.navigate('/Admin?headerMeasure=1').then(()=>done=true);
     },html);
     await until(page,()=>waiting);await page.clock.runFor(150);
     await page.waitForFunction(()=>getComputedStyle(document.querySelector('[data-page-skeleton] .dash-grid')).display==='grid');
     const styles=()=>page.evaluate(async()=>{
      await Promise.all([...document.querySelectorAll('link[rel=stylesheet]')].map(link=>link.sheet?Promise.resolve():new Promise((resolve,reject)=>{link.addEventListener('load',resolve,{once:true});link.addEventListener('error',()=>reject(Error('Stylesheet failed')),{once:true});})));
      void document.body.offsetHeight;await document.fonts.ready;
     });
     await styles();await page.waitForFunction(()=>getComputedStyle(document.querySelector('[data-dashboard-loading-card]')).flex==='0 0 auto');await page.clock.runFor(32);
     const box=()=>page.evaluate(()=>{
      const root=document.querySelector('[data-page-skeleton]')||document.querySelector('[data-page-region] > .page:not([hidden])');
      return Object.fromEntries([['head','.page-head'],['group','.page-head > div:first-child'],['title','.h1'],['summary','.summary'],['card','.next-event'],['first','.card']].map(([key,selector])=>{const r=(selector===':scope'?root:root.querySelector(selector)).getBoundingClientRect();return[key,{x:r.x,y:r.y,width:r.width,height:r.height}];}));
     });
     const loading=await box();await page.evaluate(()=>fulfill());await until(page,()=>timerLog.includes(368));await page.clock.runFor(368);await until(page,()=>done);
     await styles();await page.waitForFunction(()=>getComputedStyle(document.querySelector('[data-page-region] .next-event')).flex==='0 0 auto');await page.clock.runFor(32);const loaded=await box();await ref.setViewportSize({width,height:1000});await settle(ref);
     const originalHead=await ref.locator('.page-head').evaluate(e=>e.innerHTML);
     const reference=await ref.locator('.page-head').evaluate(root=>Object.fromEntries([['head',':scope'],['group',':scope > div:first-child'],['title','.h1'],['summary','.summary'],['card','.next-event']].map(([key,selector])=>{const r=(selector===':scope'?root:root.querySelector(selector)).getBoundingClientRect();return[key,{x:r.x,y:r.y,width:r.width,height:r.height}];})));
     await ref.locator('.page-head').evaluate(root=>{
       root.querySelector('.summary').innerHTML='<span>&nbsp;</span>';
       const card=root.querySelector('.next-event');card.style.width='420px';
       card.innerHTML='<div class="grow"><div class="next-label">&nbsp;</div><div class="next-name">&nbsp;</div><div class="next-meta">&nbsp;</div></div><span style="width:56px;height:6px;flex:none" class="sk"></span><span style="width:52px;height:28px;flex:none" class="sk"></span>';
       if(innerWidth<=860)card.style.width='100%';
     });
     await settle(ref);
     const referenceLoading=await ref.locator('.page-head').evaluate(root=>Object.fromEntries([['head',':scope'],['group',':scope > div:first-child'],['title','.h1'],['summary','.summary'],['card','.next-event']].map(([key,selector])=>{const r=(selector===':scope'?root:root.querySelector(selector)).getBoundingClientRect();return[key,{x:r.x,y:r.y,width:r.width,height:r.height}];})));
     if(process.env.HEADER_DIAG==='1') console.log(JSON.stringify({loading,loaded,referenceLoading,reference}));
     const close=(actual,expected,label)=>assert.ok(Math.abs(actual-expected)<=0.1,label+' '+actual+' != '+expected);
     for(const key of Object.keys(reference))for(const axis of ['x','y','width','height'])close(loaded[key][axis],reference[key][axis],engine.name()+' '+narrow+' '+width+' loaded '+key+'.'+axis);
     for(const key of Object.keys(referenceLoading))for(const axis of ['x','y','width','height'])close(loading[key][axis],referenceLoading[key][axis],engine.name()+' '+narrow+' '+width+' loading '+key+'.'+axis);
     for(const [kind,value]of Object.entries({loading,loaded})){
       const groupHeight=value.title.height+6+value.summary.height;
       const headHeight=width<=860?groupHeight+24+value.card.height:Math.max(groupHeight,value.card.height);
       close(value.head.height,headHeight,kind+' header height from summary/card');
       close(value.title.y,value.head.y+(width<=860?0:headHeight-groupHeight),kind+' bottom-aligned title');
       close(value.summary.y,value.title.y+value.title.height+6,kind+' summary flow');
       close(value.card.y,value.head.y+(width<=860?groupHeight+24:headHeight-value.card.height),kind+' card flow');
       close(value.first.y,value.head.y+headHeight+20,kind+' following content flow');
     }
     close(loading.head.x,loaded.head.x,'page x retained');close(loading.head.y,loaded.head.y,'page y retained');close(loading.head.width,loaded.head.width,'page width retained');
     await ref.locator('.page-head').evaluate((e,html)=>e.innerHTML=html,originalHead);
     console.log('PASS '+engine.name()+' '+(narrow?'narrow':'normal')+' '+width+' loaded/loading reference + flow');
     const detail=async(surface)=>surface.locator('.page-head').last().evaluate(root=>({html:root.outerHTML,nodes:[root,root.firstElementChild,...root.querySelectorAll('.next-event,.next-event .grow,.next-meta,.btn,.meter-label')].map(e=>({tag:e.tagName,cls:e.className,width:getComputedStyle(e).width,flex:getComputedStyle(e).flex,gap:getComputedStyle(e).gap,font:getComputedStyle(e).font,text:e.textContent}))}));
     records.push({engine:engine===chromium?'chromium':'webkit',narrow,width,loading,loaded,reference,referenceLoading,...(process.env.HEADER_DIAG==='1'?{appDetail:await detail(page),refDetail:await detail(ref)}:{})});await page.close();
    }
    await context.close();await referenceContext.close();
   }finally{await browser.close();}
  }}finally{await fixture.close();}
 }
 const output=path.join(process.cwd(),'artifacts/u2-header-positions-'+runKey+'.json');fs.writeFileSync(output,JSON.stringify(records,null,2));console.log('Header parity: '+records.length+' passed, 0 failed; '+output);
})().catch(error=>{console.error(error);process.exitCode=1;});

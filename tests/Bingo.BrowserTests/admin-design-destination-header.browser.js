const assert=require('node:assert/strict'),path=require('node:path');
const {chromium,webkit}=require('playwright');
const {startFixture,login}=require('../../scripts/lib/admin-parity-fixture.cjs');
async function until(page,predicate){for(let i=0;i<200;i++)if(await page.evaluate(predicate))return;throw Error('Microtask checkpoint not reached');}
(async()=>{
 const engine=process.env.PLAYWRIGHT_BROWSER==='webkit'?webkit:chromium;
 const fixture=await startFixture(process.cwd(),path.join(process.cwd(),'artifacts/u2-rem3-destination-'+process.env.PLAYWRIGHT_BROWSER));
 let browser,passed=0;
 try{
  browser=await engine.launch({headless:true,...(engine===chromium?{channel:process.env.PLAYWRIGHT_CHANNEL||'chromium'}:{})});
  for(const culture of ['en','da']){
   const context=await browser.newContext({viewport:{width:1280,height:900},reducedMotion:'reduce'}),errors=[];let page=await login(context,fixture);await page.goto(fixture.origin+'/Admin');
   if(culture==='da'){await page.locator('[name=culture][value=da]').click();await page.waitForFunction(()=>document.documentElement.lang==='da');}
   const identity='/Admin/Events/Identity/'+fixture.events['autumn-bingo-2027'];
   const paths={dashboard:'/Admin',identity,events:'/Admin/Events/Index'},html={},headers={};
   for(const [kind,url]of Object.entries(paths)){
    const response=await context.request.get(fixture.origin+url);assert.equal(response.status(),200);html[kind]=await response.text();
    headers[kind]=await page.evaluate(source=>{const doc=new DOMParser().parseFromString(source,'text/html');return{lang:doc.documentElement.lang,title:doc.querySelector('.h1').textContent,summary:doc.querySelector('.summary')?.textContent.trim()||''};},html[kind]);assert.equal(headers[kind].lang,culture);
   }
   for(const [from,to]of[['dashboard','identity'],['identity','events'],['events','dashboard']])for(const fail of[false,true]){
    await page.close();page=await context.newPage();await page.setViewportSize({width:390,height:900});page.on('pageerror',e=>errors.push(e.message));await page.goto(fixture.origin+paths[from]);
    await page.evaluate(async()=>{await import(document.querySelector('script[data-admin-page-script]').src);await document.fonts.ready;});
    await page.waitForFunction(()=>window.AdminUI&&getComputedStyle(document.querySelector('.page-head')).display==='flex');
    await page.clock.install({time:new Date('2030-01-01T00:00:00Z')});await page.clock.pauseAt(new Date('2030-01-01T00:01:00Z'));
    await page.evaluate(({url,source})=>{
     window.requests=[];window.timers=[];const original=fetch,timer=setTimeout;
     window.setTimeout=(fn,ms,...args)=>{timers.push(ms);return timer(fn,ms,...args);};
     window.fetch=(target,options)=>new URL(target,location.href).href===new URL(url,location.href).href?new Promise((resolve,reject)=>{
      requests.push({fail:()=>reject(new TypeError('Offline')),fulfill:()=>resolve(new Response(source,{headers:{'Content-Type':'text/html'}}))});
     }):original(target,options);
     void window.AdminUI.navigate(url);
    },{url:paths[to],source:html[to]});
    await until(page,()=>requests.length===1);await page.clock.runFor(150);
    const check=async()=>{assert.equal(await page.locator('[data-page-skeleton] .h1').textContent(),headers[to].title);assert.equal((await page.locator('[data-page-skeleton] .summary').textContent()).trim(),'');assert.equal(await page.locator('.crumb-cur').textContent(),headers[to].title);};
    await check();
    if(to==='identity'){const summary=await page.locator('[data-page-skeleton] .summary').evaluate(e=>({height:e.getBoundingClientRect().height,line:parseFloat(getComputedStyle(e).lineHeight)}));assert.ok(Math.abs(summary.height-summary.line)<0.1,'Identity loading summary reserves exactly one line');}
    if(fail)await page.evaluate(()=>requests[0].fail());else await page.evaluate(()=>requests[0].fulfill());
    await until(page,()=>timers.includes(400));await page.clock.runFor(399);await check();await page.clock.runFor(1);
    if(fail){await until(page,()=>!!document.querySelector('[data-load-retry]'));await check();assert.equal(await page.locator('[data-page-skeleton]').getAttribute('aria-busy'),'false');if(to==='dashboard')assert.equal(await page.locator('[data-dashboard-loading-card]').count(),0);}
    else{await until(page,()=>!document.querySelector('[data-page-skeleton]'));assert.equal(await page.locator('.h1').textContent(),headers[to].title);}
    passed++;
   }
   assert.deepEqual(errors,[]);await context.close();
  }
  console.log('PASS '+passed+' destination header transitions, EN/DA real culture switch, slow load/failure, exact150/400');
 }finally{await browser?.close();await fixture.close();}
})().catch(error=>{console.error(error);process.exitCode=1;});

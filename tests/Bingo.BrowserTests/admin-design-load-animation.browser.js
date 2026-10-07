// Page-specific result/empty-result animation states. Full-page cases moved to the generic gate.
const assert=require('node:assert/strict'),path=require('node:path');
const {chromium,webkit}=require('playwright');
const {startFixture,login}=require('../../scripts/lib/admin-parity-fixture.cjs');
async function until(page,fn){for(let i=0;i<200;i++)if(await page.evaluate(fn))return;throw Error('Microtask checkpoint not reached');}
(async()=>{
 const engine=process.env.PLAYWRIGHT_BROWSER==='webkit'?webkit:chromium;
 const fixture=await startFixture(process.cwd(),path.join(process.cwd(),'artifacts/u2-rem4-animation-'+engine.name()));
 let browser,passed=0;
 try{
  browser=await engine.launch({headless:true,...(engine===chromium?{channel:process.env.PLAYWRIGHT_CHANNEL||'chromium'}:{})});
  let context=await browser.newContext({viewport:{width:1280,height:900},reducedMotion:'no-preference'});
  let page=await login(context,fixture);const errors=[];page.on('pageerror',e=>errors.push(e.message));
  const paths={events:'/Admin/Events?view=current'};
  const all=await context.request.get(fixture.origin+'/Admin/Events?view=all');assert.equal(all.status(),200);const allHTML=await all.text();
  const empty=await context.request.get(fixture.origin+'/Admin/Events?view=all&search=no-fixture-match');assert.equal(empty.status(),200);const emptyHTML=await empty.text();
  const storageState=await context.storageState();
  async function reset(from,next){
   // Clock installation is context-wide. A fresh context gives every case the
   // real native RAF before clock installation, never a saved previous fake RAF.
   await context.close();context=await browser.newContext({storageState,viewport:{width:1280,height:900},reducedMotion:'no-preference'});
   page=await context.newPage();page.on('pageerror',e=>errors.push(e.message));
   await page.goto(fixture.origin+paths[from]);await page.evaluate(async()=>{await import(document.querySelector('script[data-admin-page-script]').src);await document.fonts.ready;});
   await page.waitForFunction(()=>!!document.querySelector('head link[data-admin-page-style]')?.sheet);
   await page.evaluate(()=>window.nativeFrame=requestAnimationFrame.bind(window));
   await page.clock.install({time:new Date('2030-01-01T00:00:00Z')});await page.clock.pauseAt(new Date('2030-01-01T00:01:00Z'));
   await page.evaluate(next=>{
    window.pending=[];window.timers=[];window.badAnimations=[];window.loadedInspections=0;window.nextHTML=next;
    const timer=setTimeout;window.setTimeout=(fn,ms,...a)=>{timers.push(ms);return timer(fn,ms,...a);};
    const fetch=window.fetch;window.fetch=(url,options)=>new URL(url,location.href).pathname.startsWith('/Admin')?new Promise((resolve,reject)=>{
     pending.push({fulfill:()=>resolve(new Response(nextHTML,{headers:{'Content-Type':'text/html'}}))});
     options?.signal?.addEventListener('abort',()=>reject(new DOMException('Aborted','AbortError')),{once:true});
    }):fetch(url,options);
    window.inspectLoads=()=>{
     if(document.querySelector('[data-page-skeleton],[data-update-skeleton]'))return;
     const root=document.querySelector('[data-page-region]>.page');
     if(!root)return;
     const content=root.matches('[data-page-family="events"]')?[root.querySelector('[data-directory-results]'),root.querySelector('[data-directory-banners]')]:
      [...root.querySelectorAll(':scope>.card,:scope>.dash-grid,:scope>.dash-section,:scope>.page-banner')];
     for(const node of content.filter(Boolean)){
      loadedInspections++;
      for(const animation of node.getAnimations({subtree:true}))badAnimations.push({name:animation.animationName||animation.constructor.name,target:animation.effect?.target?.className});
     }
    };
    window.loadObserver=new MutationObserver(inspectLoads);loadObserver.observe(document.documentElement,{childList:true,subtree:true,attributes:true});
    window.framesActive=true;const frame=()=>{if(framesActive){inspectLoads();nativeFrame(frame);}};nativeFrame(frame);
   },next);
  }
  async function check(){
   await page.evaluate(()=>new Promise(resolve=>nativeFrame(()=>nativeFrame(resolve))));
   assert.deepEqual(await page.evaluate(()=>badAnimations),[]);assert.ok(await page.evaluate(()=>loadedInspections)>0);
   assert.equal(await page.locator('[data-page-region] .fade-in').count(),0);
   await page.evaluate(()=>{framesActive=false;loadObserver.disconnect();});passed++;
  }
  for(const slow of[false,true]){
   await reset('events',allHTML);await page.evaluate(()=>document.querySelector('.tabs input').click());await until(page,()=>pending.length===1);
   await page.clock.runFor(slow?150:149);assert.equal(await page.locator('[data-update-skeleton]').count(),slow?1:0);
   await page.evaluate(()=>pending[0].fulfill());
   if(slow){await until(page,()=>timers.includes(400));await page.clock.runFor(399);assert.equal(await page.locator('[data-update-skeleton]').count(),1);await page.clock.runFor(1);}
   await until(page,()=>new URL(location.href).searchParams.get('view')==='all'&&!document.querySelector('[data-update-skeleton]'));await check();
  }
  // Empty-result content has its own reference fade, independent of .results.
  await reset('events',emptyHTML);await page.locator('#search-input').fill('no-fixture-match');await page.clock.runFor(250);await until(page,()=>pending.length===1);
  await page.clock.runFor(149);await page.evaluate(()=>pending[0].fulfill());await until(page,()=>new URL(location.href).searchParams.get('search')==='no-fixture-match');assert.equal(await page.locator('[data-directory-results] .empty').count(),1);await check();
  assert.deepEqual(errors,[]);await context.close();
  console.log('PASS '+passed+' insertion/native-frame no-load-animation checks and preserved interaction rules (motion enabled; exact149/150/399/400ms)');
 }finally{await browser?.close();await fixture.close();}
})().catch(error=>{console.error(error);process.exitCode=1;});

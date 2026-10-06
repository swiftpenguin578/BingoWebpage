const assert=require('node:assert/strict');
const {chromium,webkit}=require('playwright');
const {startFixture,login}=require('../../scripts/lib/admin-parity-fixture.cjs');
const path=require('node:path');
async function until(page,predicate){for(let i=0;i<200;i++)if(await page.evaluate(predicate))return;throw Error('Microtask checkpoint not reached');}
(async()=>{
 const engine=process.env.PLAYWRIGHT_BROWSER==='webkit'?webkit:chromium;
 const fixture=await startFixture(process.cwd(),path.join(process.cwd(),'artifacts/u2-rem2-update-'+process.env.PLAYWRIGHT_BROWSER),{BINGO_PARITY_UR_PROFILE:'live'});
 let browser;
 try{
  browser=await engine.launch({headless:true,...(engine===chromium?{channel:process.env.PLAYWRIGHT_CHANNEL||'chromium'}:{})});
  const context=await browser.newContext({viewport:{width:390,height:600},reducedMotion:'reduce'});
  let page=await login(context,fixture);const errors=[];page.on('pageerror',e=>errors.push(e.message));
  const base='/Admin/Events?view=current';
  const urls={search:base+'&search=alpha&page=1',all:'/Admin/Events?view=all',phase:base+'&phase=signupopen',sort:base+'&sort=identity&direction=asc',page:base+'&page=2',attention:base+'&attention=1',late:base+'&search=alphaX&page=1',dashboard:'/Admin'};
  const html={};
  for(const [key,url]of Object.entries(urls)){const response=await context.request.get(fixture.origin+url);assert.equal(response.status(),200);html[key]=await response.text();}
  const reset=async key=>{
   await page.close();page=await context.newPage();page.on('pageerror',e=>errors.push(e.message));
   await page.goto(fixture.origin+base);
   await page.evaluate(async()=>{await import(document.querySelector('script[data-admin-page-script]').src);});
   await page.evaluate(()=>document.fonts.ready);
   await page.waitForFunction(()=>parseFloat(getComputedStyle(document.querySelector('.ev-tbl')).getPropertyValue('--table-min'))>0&&document.querySelector('[data-directory-wrap]').scrollWidth>document.querySelector('[data-directory-wrap]').clientWidth);
   await page.clock.install({time:new Date('2030-01-01T00:00:00Z')});
   await page.clock.pauseAt(new Date('2030-01-01T00:01:00Z'));
   await page.evaluate(next=>{
    const selectors=['.page-head','.summary','.toolbar','.tabs','#search-input','#phase-btn','[data-directory-wrap]'];
    window.retained=selectors.map(selector=>({selector,node:document.querySelector(selector)}));
    window.initialSummary=document.querySelector('.summary').textContent;
    window.initialCounts=[...document.querySelectorAll('.tab-count')].map(e=>e.textContent);
    window.oldResults=document.querySelector('[data-directory-results] .rows');window.sawSkeleton=false;
    window.badFrames=[];window.requests=[];window.timers=[];window.nextHTML=next;
    const timer=window.setTimeout;window.setTimeout=(fn,ms,...args)=>{timers.push(ms);return timer(fn,ms,...args);};
    const fetch=window.fetch;
    window.fetch=(url,options)=>(String(url).includes('/Admin/Events?')||['/Admin','/Admin/Index'].includes(new URL(url,location.href).pathname))?new Promise((resolve,reject)=>{
     const request={url:String(url),aborted:false,classified:new Headers(options.headers).get('X-Requested-With')==='XMLHttpRequest'};
     requests.push(request);request.fail=()=>reject(new TypeError('Offline'));request.session=()=>resolve(new Response('<html>Sign in</html>',{headers:{'Content-Type':'text/html','X-Bingo-Post-Navigation':'/Account/Login'}}));request.fulfill=()=>resolve(new Response(window.nextHTML,{headers:{'Content-Type':'text/html'}}));
     options.signal.addEventListener('abort',()=>{request.aborted=true;reject(new DOMException('Aborted','AbortError'));},{once:true});
    }):fetch(url,options);
    new MutationObserver(()=>{if(document.querySelector('[data-update-skeleton]'))sawSkeleton=true;
     if(sawSkeleton&&oldResults?.isConnected&&oldResults.checkVisibility())badFrames.push('old results reappeared');
     for(const saved of retained)if(!saved.node.isConnected||saved.node!==document.querySelector(saved.selector)||!saved.node.checkVisibility())badFrames.push(saved.selector);}).observe(document.querySelector('[data-page-region]'),{childList:true,subtree:true,attributes:true});
   },html[key]);
  };
  const identities=async()=>assert.deepEqual(await page.evaluate(()=>badFrames),[]);
  for(const slow of[false,true]){
   await reset('search');
   await page.locator('#search-input').focus();await page.locator('#search-input').pressSequentially('alpha');
   await page.evaluate(()=>document.querySelector('#search-input').setSelectionRange(1,4,'backward'));
   await page.clock.runFor(249);assert.equal(await page.evaluate(()=>requests.length),0);
   await page.clock.runFor(1);await until(page,()=>requests.length===1);
   await page.clock.runFor(149);assert.equal(await page.locator('[data-update-skeleton]').count(),0);
   if(slow){
    await page.clock.runFor(1);
    assert.equal(await page.locator('[data-update-skeleton]').count(),1);
    assert.equal(await page.locator('[data-page-skeleton]').count(),0);
    assert.equal(await page.locator('[data-update-skeleton] .sk').count()>0,true);
    assert.equal(await page.locator('.summary').textContent(),await page.evaluate(()=>initialSummary));
    assert.deepEqual(await page.locator('.tab-count').allTextContents(),await page.evaluate(()=>initialCounts));
    await page.clock.runFor(1);await page.evaluate(()=>requests[0].fulfill());
    await until(page,()=>timers.includes(399));
    await page.clock.runFor(398);assert.equal(await page.locator('[data-update-skeleton]').count(),1);
    await page.clock.runFor(1);
   }else await page.evaluate(()=>requests[0].fulfill());
   await until(page,()=>new URL(location.href).searchParams.get('search')==='alpha');
   assert.equal(await page.locator('[data-update-skeleton]').count(),0);
   assert.equal(await page.evaluate(()=>requests.length),1);
   assert.equal(await page.evaluate(()=>requests[0].classified),true);
   assert.deepEqual(await page.locator('#search-input').evaluate(e=>({focused:document.activeElement===e,value:e.value,start:e.selectionStart,end:e.selectionEnd,direction:e.selectionDirection})),{focused:true,value:'alpha',start:1,end:4,direction:'backward'});
   await identities();
  }
  for(const [key,selector]of[['all','.tabs input'],['phase','#directory-phase-menu button[data-directory-url*="signupopen"]'],['sort','#directory-sort-identity'],['page','.pager button[aria-label="Page 2"]'],['attention','.summary button']]){
   await reset(key);
   if(key==='phase')await page.locator('#phase-btn').click();
   await page.evaluate(selector=>{const e=document.querySelector(selector);e.focus({preventScroll:true});window.started=e;window.startedLabel=e.getAttribute('aria-label');e.click();},selector);
   await until(page,()=>requests.length===1);
   if(key==='phase') {
    await page.evaluate(()=>{window.started=document.querySelector('#phase-btn');window.startedLabel=null;});
    assert.equal(await page.locator('#phase-btn').evaluate(e=>document.activeElement===e&&e.getAttribute('aria-expanded')==='false'),true);
    assert.equal(await page.locator('#directory-phase-menu').evaluate(e=>e.hidden),true);
    await page.clock.runFor(150);
    assert.equal(await page.locator('#phase-btn').evaluate(e=>document.activeElement===e),true);
    await page.evaluate(()=>requests[0].fulfill());await until(page,()=>timers.includes(400));await page.clock.runFor(400);
   } else { await page.clock.runFor(149);await page.evaluate(()=>requests[0].fulfill()); }
   await until(page,()=>document.querySelector('[data-events-directory]').dataset.directoryCanonical!== '/Admin/Events?view=current');
   await until(page,()=>!document.querySelector('[data-update-skeleton]')&&new URL(location.href).search!== '?view=current');
   assert.equal(await page.evaluate(()=>document.activeElement===started||(startedLabel&&document.activeElement.getAttribute('aria-label')===startedLabel)),true,key+' starting control keeps focus');
   await identities();
  }
  // Browser scrolling is preserved; no shorter-results clamp in this view change.
  await reset('all');
  const scroll=await page.evaluate(()=>{const main=document.querySelector('[data-page-region]'),wrap=document.querySelector('[data-directory-wrap]');main.scrollTop=100;wrap.scrollLeft=140;return{top:main.scrollTop,left:wrap.scrollLeft};});
  assert.ok(scroll.top>0&&scroll.left>0);
  await page.evaluate(()=>{const e=document.querySelector('.tabs input');e.focus({preventScroll:true});e.click();});
  await until(page,()=>requests.length===1);await page.clock.runFor(150);
  assert.deepEqual(await page.evaluate(()=>({top:document.querySelector('[data-page-region]').scrollTop,left:document.querySelector('[data-directory-wrap]').scrollLeft})),scroll);
  await page.evaluate(()=>requests[0].fulfill());await until(page,()=>timers.includes(400));
  await page.clock.runFor(400);await until(page,()=>new URL(location.href).searchParams.get('view')!=='current');
  assert.deepEqual(await page.evaluate(()=>({top:document.querySelector('[data-page-region]').scrollTop,left:document.querySelector('[data-directory-wrap]').scrollLeft})),scroll);await identities();
  for(const kind of ['second-shown','abort-hold','failure-hold']) {
   await reset('all');await page.evaluate(()=>document.querySelector('.tabs input').click());await until(page,()=>requests.length===1);
   await page.clock.runFor(150);await page.clock.runFor(50);
   if(kind==='failure-hold'){await page.evaluate(()=>requests[0].fail());await until(page,()=>timers.includes(350));}
   else {
    if(kind==='abort-hold'){await page.evaluate(()=>requests[0].fulfill());await until(page,()=>timers.includes(350));}
    await page.evaluate(()=>{const button=document.querySelector('#directory-sort-identity');button.focus();button.click();});await until(page,()=>requests.length===2);
    assert.equal(await page.evaluate(()=>requests[0].aborted),true);
    assert.equal(await page.locator('[data-update-skeleton]').count(),1,'second results skeleton is immediate');
    await page.evaluate(next=>{nextHTML=next;requests[1].fulfill();},html.sort);await until(page,()=>timers.includes(350));
   }
   await page.clock.runFor(349);assert.equal(await page.locator('[data-update-skeleton]').count(),1);
   assert.equal(await page.locator('[data-load-retry]').count(),0);
   await page.clock.runFor(1);await until(page,()=>!document.querySelector('[data-update-skeleton]'));
   if(kind==='failure-hold')assert.equal(await page.locator('[data-load-retry]').count(),1);
   else assert.equal(await page.locator('#directory-sort-identity').evaluate(e=>document.activeElement===e),true);
   await identities();
  }
  // Input aborts immediately; the old response cannot reveal rows during debounce.
  await reset('search');await page.locator('#search-input').fill('alpha');await page.clock.runFor(250);await until(page,()=>requests.length===1);
  await page.clock.runFor(150);await page.clock.runFor(50);
  await page.locator('#search-input').press('End');await page.locator('#search-input').pressSequentially('X');
  assert.equal(await page.evaluate(()=>requests[0].aborted),true);
  await page.evaluate(()=>requests[0].fulfill());
  await page.clock.runFor(249);assert.equal(await page.evaluate(()=>requests.length),1);
  assert.equal(await page.locator('[data-update-skeleton]').count(),1);await identities();
  await page.clock.runFor(1);await until(page,()=>requests.length===2);
  await page.evaluate(next=>{nextHTML=next;requests[1].fulfill();},html.late);await until(page,()=>timers.includes(100));
  await page.clock.runFor(99);assert.equal(await page.locator('[data-update-skeleton]').count(),1);
  await page.clock.runFor(1);await until(page,()=>new URL(location.href).searchParams.get('search')==='alphaX');await identities();
  // Sidebar A16 navigation immediately inherits the shown results hold.
  await reset('dashboard');await page.evaluate(()=>document.querySelector('#directory-sort-identity').click());await until(page,()=>requests.length===1);
  await page.clock.runFor(150);await page.clock.runFor(50);
  await page.evaluate(()=>{document.querySelector('[data-shell-link][href="/Admin"]').click();});
  await until(page,()=>requests.length===2);
  assert.equal(await page.evaluate(()=>requests[0].aborted),true);
  assert.equal(await page.locator('[data-page-skeleton]').count(),1);
  assert.equal(await page.locator('[data-update-skeleton]').count(),0);
  assert.deepEqual(await page.evaluate(()=>badFrames.filter(value=>value==='old results reappeared')),[]);
  await page.evaluate(()=>requests[1].fulfill());await until(page,()=>timers.includes(350));
  await page.clock.runFor(349);assert.equal(await page.locator('[data-page-skeleton]').count(),1);
  await page.clock.runFor(1);await until(page,()=>['/Admin','/Admin/Index'].includes(location.pathname));
  assert.deepEqual(await page.evaluate(()=>badFrames.filter(value=>value==='old results reappeared')),[]);
  // A new settled query cancels the old request; characters typed mid-flight survive.
  await reset('search');await page.locator('#search-input').fill('alpha');await page.clock.runFor(250);await until(page,()=>requests.length===1);
  await page.locator('#search-input').press('End');await page.locator('#search-input').pressSequentially('X');await page.clock.runFor(250);await until(page,()=>requests.length===2);
  assert.equal(await page.evaluate(()=>requests[0].aborted),true);
  await page.evaluate(next=>{window.nextHTML=next;requests[1].fulfill();},html.late);
  await until(page,()=>new URL(location.href).searchParams.get('search')==='alphaX');
  assert.equal(await page.locator('[data-update-skeleton]').count(),0,'aborted-before-show request cannot start a stale skeleton');
  assert.equal(await page.locator('#search-input').inputValue(),'alphaX');assert.equal(await page.locator('#search-input').evaluate(e=>document.activeElement===e),true);await identities();
  await reset('search');await page.locator('#search-input').fill('alpha');await page.locator('#search-input').press('Enter');await until(page,()=>requests.length===1);
  await page.evaluate(()=>requests[0].fail());await until(page,()=>!!document.querySelector('[data-directory-results] [data-load-retry]'));
  assert.equal(await page.locator('[data-update-skeleton]').count(),0);await identities();
  assert.equal(new URL(page.url()).searchParams.has('search'),false);
  await page.locator('[data-directory-results] [data-load-retry]').focus();
  await page.locator('[data-directory-results] [data-load-retry]').press('Enter');await until(page,()=>requests.length===2);
  await page.evaluate(()=>requests[1].fulfill());await until(page,()=>new URL(location.href).searchParams.get('search')==='alpha');
  assert.equal(await page.locator('[data-load-retry]').count(),0);assert.equal(await page.evaluate(()=>document.activeElement===document.querySelector('.th-btn')),true,'removed Retry falls back to results heading');await identities();
  await reset('search');await page.locator('#search-input').fill('alpha');await page.locator('#search-input').press('Enter');await until(page,()=>requests.length===1);
  await page.evaluate(()=>requests[0].session());await page.getByRole('alertdialog').waitFor();
  assert.equal(new URL(page.url()).searchParams.has('search'),false);assert.equal(await page.locator('#search-input').inputValue(),'alpha');
  assert.equal(await page.locator('[data-update-skeleton]').count(),0);await page.getByRole('button',{name:'Keep editing',exact:true}).click();await identities();
  assert.deepEqual(errors,[]);
  console.log('PASS Events results-only: exact fast/slow/debounce, retained nodes/focus/caret, all controls,390px scroll, superseded request and typed characters');
  await context.close();
 }finally{await browser?.close();await fixture.close();}
})().catch(error=>{console.error(error);process.exitCode=1;});

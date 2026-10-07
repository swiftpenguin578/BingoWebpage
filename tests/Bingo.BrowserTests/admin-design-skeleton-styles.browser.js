// Brief76 item2: native CSS frames, exact scheduling clock, no sleeps.
const assert=require('node:assert/strict'),fs=require('node:fs');
const {chromium,webkit}=require('playwright');
const {root,shell}=require('./fixtures/admin-design-shell-fixture.cjs');
const paths={dashboard:'/Admin',events:'/Admin/Events/Index',identity:'/Admin/Events/Identity/a'};
const css=kind=>fs.readFileSync(root+'/css/admin-design-'+kind+'.css','utf8')+'\n[data-page-family="'+kind+'"]{--fixture-family:'+kind+'}';
const href=kind=>'/page-'+kind+'.css';
function html(kind){
 const templates=Object.keys(paths).map(f=>'<template data-page-loading-template="'+f+'"><link rel="stylesheet" href="'+href(f)+'"><div class="card" data-body-block><div class="sk"></div></div></template>').join('');
 return shell(kind).replace('</head>','<link rel="stylesheet" data-admin-page-style href="'+href(kind)+'"></head>')
  .replace('<div class="page">','<div class="page" data-page-family="'+kind+'" data-fixture-page="'+kind+'">').replace('</body>',templates+'</body>');
}
async function until(page,fn){for(let i=0;i<200;i++)if(await page.evaluate(fn))return;throw Error('Microtask checkpoint not reached');}
async function nativeUntil(page,fn){for(let i=0;i<200;i++){if(fn())return;await page.evaluate(()=>0);}throw Error('Native request checkpoint not reached');}
(async()=>{
 const engine=process.env.PLAYWRIGHT_BROWSER==='webkit'?webkit:chromium,browser=await engine.launch({headless:true,...(engine===chromium?{channel:process.env.PLAYWRIGHT_CHANNEL||'chromium'}:{})});let passed=0;
 async function fixture(from,held=[]){
  const context=await browser.newContext({reducedMotion:'reduce'}),page=await context.newPage(),waiting=new Map(),requests=[],errors=[];
  page.on('pageerror',e=>errors.push(e.message));
  await page.route('https://bingo.test/**',route=>{
   const p=new URL(route.request().url()).pathname;
   if(p.startsWith('/page-')){requests.push(p);if(held.includes(p)){waiting.set(p,route);return;}return route.fulfill({contentType:'text/css',body:css(p.slice(6,-4))});}
   if(p==='/fixture-page.mjs')return route.fulfill({contentType:'text/javascript',body:'export function init(){window.fixtureReady=true;}export function dispose(){}'});
   if(p.startsWith('/js/')||p.startsWith('/css/'))return route.fulfill({contentType:p.endsWith('.js')?'text/javascript':'text/css',body:fs.readFileSync(root+p)});
   return route.fulfill({contentType:'text/html',body:html(from)});
  });
  await page.goto('https://bingo.test'+paths[from]);await until(page,()=>window.fixtureReady);
  await page.evaluate(()=>window.nativeFrame=requestAnimationFrame.bind(window));
  await page.clock.install({time:new Date('2030-01-01T00:00:00Z')});await page.clock.pauseAt(new Date('2030-01-01T00:01:00Z'));
  await page.evaluate(()=>{
   window.bad=[];window.shows=[];window.timers=[];window.pending=[];window.old=document.querySelector('[data-fixture-page]');
   const timer=setTimeout;window.setTimeout=(fn,ms,...a)=>{timers.push(ms);return timer(fn,ms,...a);};
   window.fetch=(url,options)=>new Promise((resolve,reject)=>{
    const request={url,resolve:(source,status=200)=>resolve(new Response(source,{status,headers:{'Content-Type':'text/html'}}))};pending.push(request);
    options?.signal?.addEventListener('abort',()=>reject(new DOMException('Aborted','AbortError')),{once:true});
   });
   const inspect=frame=>{
    const sk=document.querySelector('[data-page-skeleton]'),loaded=document.querySelector('[data-fixture-page]');
    for(const e of[sk,loaded])if(e&&e.checkVisibility()){
     if(e===sk&&e.dataset.skeletonLayout==='generic')continue;
     const family=e.dataset.pageFamily,link=[...document.querySelectorAll('head link[data-admin-page-style]')].find(l=>new URL(l.href).pathname==='/page-'+family+'.css');
     if(!link?.sheet||(frame&&getComputedStyle(e).getPropertyValue('--fixture-family').trim()!==family))bad.push({family,frame,skeleton:e===sk,ready:!!link?.sheet});
    }
    if(sk&&!shows.some(x=>x.element===sk))shows.push({element:sk,at:performance.now()});
   };
   new MutationObserver(()=>inspect(false)).observe(document.documentElement,{childList:true,subtree:true});
   const frame=()=>{inspect(true);nativeFrame(frame);};nativeFrame(frame);
  });
  return{page,context,waiting,requests,errors,async check(){await page.evaluate(()=>new Promise(resolve=>nativeFrame(()=>nativeFrame(resolve))));assert.deepEqual(await page.evaluate(()=>bad),[]);assert.deepEqual(errors,[]);}};
 }
 try{
  for(const [from,to]of[['dashboard','events'],['events','dashboard']])for(const at of[50,149,151,650]){
   const f=await fixture(from,[href(to)]),p=f.page;
   await p.evaluate(url=>{window.done=false;void AdminUI.navigate(url).then(()=>done=true);},paths[to]);await until(p,()=>pending.length===1);await nativeUntil(p,()=>f.waiting.has(href(to)));
   await p.clock.runFor(Math.min(at,150));assert.equal(await p.locator('[data-page-skeleton]').count(),0);assert.equal(await p.evaluate(()=>old.checkVisibility()),true);
   if(at>150)await p.clock.runFor(at-150);
   await f.waiting.get(href(to)).fulfill({contentType:'text/css',body:css(to)});
   await until(p,()=>[...document.querySelectorAll('head link[data-admin-page-style]')].some(l=>l.href.endsWith('/page-'+(old.dataset.pageFamily==='dashboard'?'events':'dashboard')+'.css')&&l.sheet));
   await p.evaluate(source=>pending[0].resolve(source),html(to));
   if(at<150){await until(p,()=>done);assert.equal(await p.locator('[data-page-skeleton]').count(),0);assert.equal(await p.evaluate(()=>shows.length),0);}
   else{await until(p,()=>timers.includes(400));assert.deepEqual(await p.evaluate(()=>shows.map(x=>x.at)),[await p.evaluate(()=>performance.now())]);await p.clock.runFor(399);assert.equal(await p.evaluate(()=>done),false);await p.clock.runFor(1);await until(p,()=>done);}
   await f.check();assert.equal(await p.locator('head link[data-admin-page-style]').count(),1);passed++;await f.context.close();
  }
  // Round5 M1: fast HTTP failures retain previously uncached family CSS.
  for(const [from,to]of[['dashboard','events'],['events','dashboard']]){
   const f=await fixture(from,[href(to)]),p=f.page;
   assert.equal(await p.locator('head link[data-admin-page-style]').evaluateAll((links,path)=>links.filter(l=>new URL(l.href).pathname===path).length,href(to)),0);
   const started=await p.evaluate(()=>performance.now());
   await p.evaluate(url=>{window.done=false;void AdminUI.navigate(url).then(()=>done=true);},paths[to]);
   await until(p,()=>pending.length===1);await nativeUntil(p,()=>f.waiting.has(href(to)));
   await p.clock.runFor(50);await f.waiting.get(href(to)).fulfill({contentType:'text/css',body:css(to)});
   await until(p,()=>[...document.querySelectorAll('head link[data-admin-page-style]')].some(l=>l.href.endsWith('/page-'+(old.dataset.pageFamily==='dashboard'?'events':'dashboard')+'.css')&&l.sheet));
   await p.clock.runFor(50);await p.evaluate(()=>pending[0].resolve('HTTP failure',500));await until(p,()=>done);
   const failed=p.locator('[data-page-skeleton][aria-busy="false"]');
   assert.equal(await failed.getAttribute('data-page-family'),to);
   assert.equal(await failed.locator('[data-load-retry]').count(),1);
   assert.equal(await failed.evaluate(e=>getComputedStyle(e).getPropertyValue('--fixture-family').trim()),to);
   assert.equal(await p.locator('head link[data-admin-page-style]').evaluateAll((links,path)=>!!links.find(l=>new URL(l.href).pathname===path)?.sheet,href(to)),true);
   assert.equal(await p.evaluate(()=>performance.now())-started,100);
   assert.equal(await p.evaluate(()=>timers.includes(400)),false);
   await f.check();passed++;await f.context.close();
  }
  // Cached family CSS is known before the response and uses the normal150/400.
  for(const [from,to]of[['dashboard','events'],['events','dashboard']]){
   const f=await fixture(from),p=f.page;
   await p.evaluate(href=>new Promise((resolve,reject)=>{const l=document.createElement('link');l.rel='stylesheet';l.href=href;l.dataset.adminPageStyle='';l.onload=resolve;l.onerror=reject;document.head.append(l);window.cached=l;}),href(to));
   await p.evaluate(url=>{window.done=false;void AdminUI.navigate(url).then(()=>done=true);},paths[to]);await until(p,()=>pending.length===1);await p.clock.runFor(149);assert.equal(await p.locator('[data-page-skeleton]').count(),0);await p.clock.runFor(1);assert.equal(await p.locator('[data-page-skeleton]').count(),1);
   await p.evaluate(source=>pending[0].resolve(source),html(to));await until(p,()=>timers.includes(400));await p.clock.runFor(399);assert.equal(await p.evaluate(()=>done),false);await p.clock.runFor(1);await until(p,()=>done);assert.equal(await p.evaluate(()=>cached===document.querySelector('head link[data-admin-page-style]')),true);await f.check();passed++;await f.context.close();
  }
  // No remembered family: generic shared-only skeleton requests no page CSS.
  {
   const f=await fixture('dashboard'),p=f.page;await p.evaluate(()=>void AdminUI.navigate('/Admin/Events/Overview/a'));await until(p,()=>pending.length===1);await p.clock.runFor(150);assert.equal(await p.locator('[data-page-skeleton]').getAttribute('data-skeleton-layout'),'generic');assert.deepEqual(f.requests,[href('dashboard')]);await f.check();passed++;await f.context.close();
  }
  // Cancel while native CSS is still pending; only the successor survives.
  {
   const f=await fixture('dashboard',[href('events')]),p=f.page;
   await p.evaluate(()=>{window.first=null;void AdminUI.navigate('/Admin/Events/Index').then(v=>first=v);});await nativeUntil(p,()=>f.waiting.has(href('events')));await p.clock.runFor(650);assert.equal(await p.locator('[data-page-skeleton]').count(),0);
   await p.evaluate(()=>{window.second=false;void AdminUI.navigate('/Admin/Events/Identity/a').then(()=>second=true);});await until(p,()=>pending.length===2);await p.evaluate(source=>pending[1].resolve(source),html('identity'));await until(p,()=>second&&first===false);
   await f.waiting.get(href('events')).fulfill({contentType:'text/css',body:css('events')});await p.clock.runFor(1000);assert.deepEqual(await p.locator('head link[data-admin-page-style]').evaluateAll(ns=>ns.map(l=>new URL(l.href).pathname)),[href('identity')]);assert.equal(await p.locator('[data-page-skeleton]').count(),0);await f.check();passed++;await f.context.close();
  }
  // Cancellation after CSS is ready: same-family successor retains staged node.
  {
   const f=await fixture('dashboard'),p=f.page;await p.evaluate(()=>{window.first=null;void AdminUI.navigate('/Admin/Events/Index').then(v=>first=v);});await until(p,()=>pending.length===1&&!![...document.querySelectorAll('head link[data-admin-page-style]')].find(l=>l.href.endsWith('/page-events.css')&&l.sheet));await p.evaluate(()=>window.kept=[...document.querySelectorAll('head link[data-admin-page-style]')].find(l=>l.href.endsWith('/page-events.css')));
   await p.evaluate(()=>{window.second=false;void AdminUI.navigate('/Admin/Events/Index?new=1').then(()=>second=true);});await until(p,()=>pending.length===2);await p.evaluate(source=>pending[1].resolve(source),html('events'));await until(p,()=>second&&first===false);assert.equal(await p.evaluate(()=>kept===document.querySelector('head link[data-admin-page-style]')),true);await f.check();passed++;await f.context.close();
  }
  // A shown skeleton retains its CSS while a successor waits for another family.
  {
   const f=await fixture('dashboard',[href('identity')]),p=f.page;
   await p.evaluate(()=>{window.first=null;void AdminUI.navigate('/Admin/Events/Index').then(v=>first=v);});await until(p,()=>pending.length===1&&!![...document.querySelectorAll('head link[data-admin-page-style]')].find(l=>l.href.endsWith('/page-events.css')&&l.sheet));await p.clock.runFor(150);assert.equal(await p.locator('[data-page-skeleton]').getAttribute('data-page-family'),'events');
   await p.evaluate(()=>{window.second=false;void AdminUI.navigate('/Admin/Events/Identity/a').then(()=>second=true);});await nativeUntil(p,()=>f.waiting.has(href('identity')));await p.clock.runFor(500);assert.equal(await p.locator('[data-page-skeleton]').getAttribute('data-page-family'),'events');await f.check();
   await f.waiting.get(href('identity')).fulfill({contentType:'text/css',body:css('identity')});await p.evaluate(source=>pending[1].resolve(source),html('identity'));await until(p,()=>second&&first===false);assert.deepEqual(await p.locator('head link[data-admin-page-style]').evaluateAll(ns=>ns.map(l=>new URL(l.href).pathname)),[href('identity')]);await f.check();passed++;await f.context.close();
  }
  // Native stale null-sheet link: unsupported type cannot emit load/error.
  // Aborted requests create empty non-null CSSOMs in these engines, so are not L1.
  for(const fail of[false,true]){
   const f=await fixture('dashboard',[href('events')]),p=f.page;let full=0;
   await p.route('https://bingo.test'+paths.events,route=>{full++;return route.fulfill({contentType:'text/html',body:'Full-load fallback'});});
   await p.evaluate(href=>{const l=document.createElement('link');l.rel='stylesheet';l.type='text/plain';l.dataset.adminPageStyle='';l.href=href;document.head.append(l);window.stale=l;},href('events'));assert.equal(await p.evaluate(()=>stale.sheet===null),true);assert.equal(f.requests.filter(x=>x===href('events')).length,0);
   await p.evaluate(()=>{window.done=false;void AdminUI.navigate('/Admin/Events/Index').then(()=>done=true);});await nativeUntil(p,()=>f.waiting.has(href('events')));assert.equal(await p.evaluate(()=>stale.isConnected),false);assert.equal(f.requests.filter(x=>x===href('events')).length,1);
   if(fail){await f.waiting.get(href('events')).abort();await p.waitForURL('**/Admin/Events/Index');assert.equal(full,1);}
   else{await f.waiting.get(href('events')).fulfill({contentType:'text/css',body:css('events')});await p.evaluate(source=>pending[0].resolve(source),html('events'));await until(p,()=>done);await f.check();}
   passed++;await f.context.close();
  }
  console.log('PASS '+passed+' native skeleton/CSS cases: uncached/cached timing, generic, pending/ready cancellation, stale-link reload/failure');
 }finally{await browser.close();}
})().catch(error=>{console.error(error);process.exitCode=1;});

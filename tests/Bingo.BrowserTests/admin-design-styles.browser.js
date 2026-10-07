// Brief74 5b: real native stylesheet load/error with exact fake-clock boundaries.
const assert=require('node:assert/strict'),fs=require('node:fs');
const {chromium,webkit}=require('playwright');
const {root,shell}=require('./fixtures/admin-design-shell-fixture.cjs');
const routes={dashboard:'/Admin',events:'/Admin/Events/Index',identity:'/Admin/Events/Identity/a'};
const moduleSource='export function init(){window.fixtureReady=true;}export function dispose(){window.disposes=(window.disposes||0)+1;}';
function html(kind,culture='en'){
 return shell(kind).replace('<html class="dk-theme" lang="en">','<html class="dk-theme" lang="'+culture+'">')
  .replace('</head>','<link rel="stylesheet" data-admin-page-style href="/page-'+kind+'-'+culture+'.css"></head>')
  .replace('<div class="page">','<div class="page" data-destination="'+kind+'" data-page-family="'+kind+'" data-fixture-culture="'+culture+'">');
}
async function until(page,predicate){for(let i=0;i<200;i++)if(await page.evaluate(predicate))return;throw Error('Microtask checkpoint not reached');}
async function nativeUntil(page,predicate){for(let i=0;i<200;i++){if(predicate())return;await page.evaluate(()=>0);}throw Error('Native request checkpoint not reached');}
(async()=>{
 const engine=process.env.PLAYWRIGHT_BROWSER==='webkit'?webkit:chromium;
 const browser=await engine.launch({headless:true,...(engine===chromium?{channel:process.env.PLAYWRIGHT_CHANNEL||'chromium'}:{})});let passed=0;
 try{
  for(const [from,to]of[['dashboard','events'],['events','dashboard'],['events','identity'],['identity','events'],['dashboard','identity'],['identity','dashboard']])for(const endAt of[149,151,650]){
   const context=await browser.newContext({reducedMotion:'reduce'}),page=await context.newPage(),requests=[],errors=[];let waiting;
   page.on('pageerror',e=>errors.push(e.message));
   await page.route('https://bingo.test/**',route=>{
    const path=new URL(route.request().url()).pathname;
    if(path==='/page-'+to+'-en.css'){requests.push(path);waiting=route;return;}
    if(path.startsWith('/page-'))return route.fulfill({contentType:'text/css',body:fs.readFileSync(root+'/css/admin-design-'+from+'.css','utf8')+'\n[data-page-family="'+from+'"]{--fixture-page:'+from+'}'});
    if(path.startsWith('/js/')||path.startsWith('/css/'))return route.fulfill({contentType:path.endsWith('.js')?'text/javascript':'text/css',body:fs.readFileSync(root+path,'utf8')});
    if(path==='/fixture-page.mjs')return route.fulfill({contentType:'text/javascript',body:moduleSource});
    return route.fulfill({contentType:'text/html',body:html(from)});
   });
   await page.goto('https://bingo.test'+routes[from]);await until(page,()=>window.fixtureReady);
   await page.evaluate(()=>window.nativeFrame=requestAnimationFrame.bind(window));
   await page.clock.install({time:new Date('2030-01-01T00:00:00Z')});await page.clock.pauseAt(new Date('2030-01-01T00:01:00Z'));
   await page.evaluate(({source,target,to})=>{
    window.badFrames=[];window.timerLog=[];window.old=document.querySelector('[data-destination]');window.oldStyle=document.querySelector('[data-admin-page-style]');window.done=false;window.started=performance.now();
    const properties=['display','position','color','backgroundColor','fontSize','lineHeight','width','height','paddingTop','paddingBottom','marginTop','marginBottom','gap','gridTemplateColumns','flex'];
    const presentation=()=>[old,...old.querySelectorAll('*')].map(e=>{const css=getComputedStyle(e);return properties.map(key=>css[key]);});
    const leavingPresentation=JSON.stringify(presentation());
    const timer=setTimeout;window.setTimeout=(fn,ms,...args)=>{timerLog.push(ms);return timer(fn,ms,...args);};
    window.fetch=async()=>new Response(source,{headers:{'Content-Type':'text/html'}});
    const inspect=(frame=false)=>{const region=document.querySelector('[data-destination="'+to+'"]'),link=[...document.querySelectorAll('head link[data-admin-page-style]')].find(link=>new URL(link.href).pathname==='/page-'+to+'-en.css');
     if(region&&(!link?.sheet||link.media==='not all'||(frame&&getComputedStyle(region).getPropertyValue('--fixture-page').trim()!==to)))badFrames.push({reason:'destination without active ready stylesheet',frame,from:old.dataset.destination,to,css:getComputedStyle(region).getPropertyValue('--fixture-page'),media:link?.media,sheet:!!link?.sheet,disabled:link?.sheet?.disabled,sheetMedia:link?.sheet?.media?.mediaText,lastRule:link?.sheet?.cssRules?.item(link.sheet.cssRules.length-1)?.cssText,styles:[...document.querySelectorAll('head link[data-admin-page-style]')].map(e=>e.outerHTML)});
     if(old.isConnected&&old.checkVisibility()&&(!oldStyle.isConnected||getComputedStyle(old).getPropertyValue('--fixture-page').trim()!==old.dataset.destination))badFrames.push('old page rendered under destination CSS');};
    const inspectPresentation=()=>{if(old.isConnected&&old.checkVisibility()&&JSON.stringify(presentation())!==leavingPresentation)badFrames.push('leaving page presentation changed');};
    new MutationObserver(()=>inspect()).observe(document.documentElement,{childList:true,subtree:true,attributes:true});
    const frame=()=>{inspect(true);inspectPresentation();nativeFrame(frame);};nativeFrame(frame);
    void AdminUI.navigate(target).then(()=>done=true);
   },{source:html(to),target:routes[to],to});
   await nativeUntil(page,()=>!!waiting);assert.equal(requests.length,1,'uncached first visit CSS requested once');
   assert.equal(await page.evaluate(()=>oldStyle.isConnected&&old.checkVisibility()),true);
   await page.clock.runFor(Math.min(endAt,149));assert.equal(await page.locator('[data-page-skeleton]').count(),0);assert.equal(await page.locator('[data-destination="'+to+'"]').count(),0);
   if(endAt>=150){await page.clock.runFor(1);assert.equal(await page.locator('[data-page-skeleton]').count(),1);await page.clock.runFor(endAt-150);}
   await waiting.fulfill({contentType:'text/css',body:fs.readFileSync(root+'/css/admin-design-'+to+'.css','utf8')+'\n[data-page-family="'+to+'"]{--fixture-page:'+to+'}'});
   if(endAt<150)await until(page,()=>done);
   else if(endAt<550){await until(page,()=>timerLog.includes(550-(performance.now()-started)));await page.clock.runFor(550-endAt-1);assert.equal(await page.evaluate(()=>done),false);await page.clock.runFor(1);await until(page,()=>done);}
   else await until(page,()=>done);
   await page.evaluate(()=>new Promise(resolve=>nativeFrame(()=>nativeFrame(resolve))));assert.deepEqual(await page.evaluate(()=>badFrames),[]);assert.deepEqual(errors,[]);
   assert.equal(await page.locator('head link[data-admin-page-style]').count(),1);assert.equal(await page.evaluate(()=>oldStyle.isConnected),false);
   // Same href stays in place with no second request or flash.
   await page.evaluate(source=>{window.keptStyle=document.querySelector('[data-admin-page-style]');window.fetch=async()=>new Response(source,{headers:{'Content-Type':'text/html'}});window.again=false;void AdminUI.navigate(location.href+'?same=1').then(()=>again=true);},html(to));
   await until(page,()=>again);assert.equal(await page.evaluate(()=>keptStyle===document.querySelector('[data-admin-page-style]')),true);assert.equal(requests.length,1);
   passed++;await context.close();
  }
  // Stylesheet error restores the old page, then takes the existing full-load fallback.
  {
   const context=await browser.newContext({reducedMotion:'reduce'}),page=await context.newPage();let waiting,fullLoads=0;
   await page.route('https://bingo.test/**',route=>{
    const path=new URL(route.request().url()).pathname;
    if(path==='/page-events-en.css'){waiting=route;return;}
    if(path==='/page-dashboard-en.css')return route.fulfill({contentType:'text/css',body:'[data-page-family="dashboard"]{--fixture-page:dashboard}'});
    if(path.startsWith('/js/')||path.startsWith('/css/'))return route.fulfill({contentType:path.endsWith('.js')?'text/javascript':'text/css',body:fs.readFileSync(root+path,'utf8')});
    if(path==='/fixture-page.mjs')return route.fulfill({contentType:'text/javascript',body:moduleSource});
    if(path===routes.events){fullLoads++;return route.fulfill({contentType:'text/html',body:'<h1>Full-load fallback</h1>'});}
    return route.fulfill({contentType:'text/html',body:html('dashboard')});
   });
   await page.goto('https://bingo.test/Admin');await until(page,()=>window.fixtureReady);
   await page.evaluate(source=>{window.addEventListener('beforeunload',()=>sessionStorage.setItem('fallback',JSON.stringify({disposed:window.disposes||0,old:!!document.querySelector('[data-destination=dashboard]'),style:!!document.querySelector('head link[href="/page-dashboard-en.css"]'),overlay:!!document.querySelector('[data-page-skeleton]')})));window.fetch=async()=>new Response(source,{headers:{'Content-Type':'text/html'}});void AdminUI.navigate('/Admin/Events/Index');},html('events'));
   await nativeUntil(page,()=>!!waiting);await waiting.abort();await page.waitForURL('**/Admin/Events/Index');assert.equal(fullLoads,1);
   assert.deepEqual(await page.evaluate(()=>JSON.parse(sessionStorage.getItem('fallback'))),{disposed:0,old:true,style:true,overlay:false});passed++;await context.close();
  }
  // Language swap waits for its own newly uncached stylesheet, without a skeleton.
  {
   const context=await browser.newContext({reducedMotion:'reduce'}),page=await context.newPage();let waiting;
   await page.route('https://bingo.test/**',route=>{
    const path=new URL(route.request().url()).pathname;
    if(path==='/page-identity-da.css'){waiting=route;return;}
    if(path.startsWith('/page-'))return route.fulfill({contentType:'text/css',body:'[data-page-family="identity"][data-fixture-culture="en"]{--fixture-page:en}'});
    if(path.startsWith('/js/')||path.startsWith('/css/'))return route.fulfill({contentType:path.endsWith('.js')?'text/javascript':'text/css',body:fs.readFileSync(root+path,'utf8')});
    if(path==='/fixture-page.mjs')return route.fulfill({contentType:'text/javascript',body:moduleSource});
    return route.fulfill({contentType:'text/html',body:html('identity')});
   });
   await page.goto('https://bingo.test'+routes.identity);await until(page,()=>window.fixtureReady);
   await page.evaluate(source=>{window.old=document.querySelector('[data-destination]');window.done=false;void AdminUI.navigate(location.href,{language:true,response:new Response(source,{headers:{'Content-Type':'text/html'}})}).then(()=>done=true);},html('identity','da'));
   await nativeUntil(page,()=>!!waiting);assert.equal(await page.evaluate(()=>old.isConnected&&document.documentElement.lang==='en'&&getComputedStyle(old).getPropertyValue('--fixture-page').trim()==='en'),true);assert.equal(await page.locator('[data-page-skeleton]').count(),0);
   await waiting.fulfill({contentType:'text/css',body:'[data-page-family="identity"][data-fixture-culture="da"]{--fixture-page:da}'});await until(page,()=>done);assert.equal(await page.evaluate(()=>!old.isConnected&&document.documentElement.lang==='da'&&getComputedStyle(document.querySelector('[data-destination]')).getPropertyValue('--fixture-page').trim()==='da'),true);passed++;await context.close();
  }
  console.log('PASS'+passed+' native stylesheet readiness cases:6 directions×149/151/650ms, same-href identity, first visit, error fallback, language');
 }finally{await browser.close();}
})().catch(error=>{console.error(error);process.exitCode=1;});

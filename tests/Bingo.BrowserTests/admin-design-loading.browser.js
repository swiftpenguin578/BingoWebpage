const assert = require('node:assert/strict'), fs = require('node:fs');
const { chromium, webkit } = require('playwright');
const { root, shell } = require('./fixtures/admin-design-shell-fixture.cjs');
const moduleSource = 'let draft;export function init(root,ui){draft=ui.trackForm(root.querySelector(\"form\"));window.fixtureReady=true;}export function dispose(){draft.dispose();window.fixtureReady=false;}';
// All elapsed-time assertions use the browser's paused fake clock. This bounded
// CDP/microtask drain waits for native module import completion, never elapsed time.
async function until(page, predicate) {
  for (let attempt = 0; attempt < 200; attempt++) if (await page.evaluate(predicate)) return;
  throw Error('Browser microtask/import checkpoint was not reached');
}
(async () => {
  const engine = process.env.PLAYWRIGHT_BROWSER === 'webkit' ? webkit : chromium;
  const browser = await engine.launch({ headless:true, ...(engine === chromium ? {channel:process.env.PLAYWRIGHT_CHANNEL || 'chromium'} : {}) });
  const outcomes=[];
  try {
    for (const mode of ['push','replace']) for (const endAt of [0,149,151,549,550,650]) {
      const page=await browser.newPage({reducedMotion:'reduce'}),errors=[];
      page.on('pageerror', error=>errors.push(error.message));
      await page.clock.install({time:new Date('2030-01-01T00:00:00Z')});
      await page.route('https://bingo.test/**', route=>{
        const url=new URL(route.request().url());
        if (url.pathname.startsWith('/js/') || url.pathname.startsWith('/css/')) return route.fulfill({contentType:url.pathname.endsWith('.js')?'text/javascript':'text/css',body:fs.readFileSync(root+url.pathname,'utf8')});
        if (url.pathname==='/fixture-page.mjs') return route.fulfill({contentType:'text/javascript',body:moduleSource});
        return route.fulfill({contentType:'text/html',body:shell('a')});
      });
      await page.goto('https://bingo.test/Admin/Events/Identity/a');
      await until(page,()=>window.fixtureReady);
      await page.clock.pauseAt(new Date('2030-01-01T00:01:00Z'));
      const target=mode==='push'?'/Admin/Events/Identity/b':'/Admin/Events?sort=signups&direction=desc';
      await page.evaluate(({html,target,mode})=>{
        window.original=document.querySelector('#draft');window.originalUrl=location.href;window.transitions=[];window.timerLog=[];
        const timer=window.setTimeout;
        window.setTimeout=(fn,ms,...args)=>{window.timerLog.push(ms);return timer(fn,ms,...args);};
        const fetch=window.fetch;
        window.fetch=(url,options)=>String(url).includes(target)?new Promise((resolve,reject)=>{
          window.fetchWaiting=true;window.fulfill=()=>resolve(new Response(html,{headers:{'Content-Type':'text/html'}}));
          options.signal.addEventListener('abort',()=>reject(new DOMException('Aborted','AbortError')),{once:true});
        }):fetch(url,options);
        window.startAt=performance.now();
        let skeleton=false;
        new MutationObserver(()=>{
          const next=!!document.querySelector('[data-page-skeleton]');
          if(next!==skeleton){window.transitions.push({shown:next,at:performance.now()-window.startAt});skeleton=next;}
        }).observe(document.querySelector('[data-page-region]').parentNode,{childList:true,subtree:true});
        window.navigationDone=false;window.navigationPromise=AdminUI.navigate(target,{mode}).then(value=>{window.navigationDone=true;return value;});
      },{html:shell('b'),target,mode});
      await until(page,()=>window.fetchWaiting);
      const snapshot=()=>page.evaluate(()=>({skeleton:!!document.querySelector('[data-page-skeleton]'),done:window.navigationDone,url:location.href,originalVisible:window.original.checkVisibility(),connected:window.original.isConnected,inert:document.querySelector('[data-page-region]').inert}));
      assert.deepEqual(await snapshot(),{skeleton:false,done:false,url:'https://bingo.test/Admin/Events/Identity/a',originalVisible:true,connected:true,inert:true});
      if(endAt>0) {
        await page.clock.runFor(Math.min(endAt,149));
        assert.equal((await snapshot()).skeleton,false,'no loading before150ms');
      }
      if(endAt>=150) {
        await page.clock.runFor(1);
        assert.equal((await snapshot()).skeleton,true,'pending load shows at150ms');
        assert.equal((await snapshot()).url,'https://bingo.test/Admin/Events/Identity/a','URL not changed by skeleton');
        if(endAt>150) await page.clock.runFor(endAt-150);
      }
      await page.evaluate(()=>window.fulfill());
      if(endAt<150) {
        await until(page,()=>window.navigationDone);
        assert.deepEqual(await page.evaluate(()=>window.transitions),[],'fast completion never inserts skeleton, even transiently');
      } else if(endAt<550) {
        await until(page,()=>window.timerLog.includes(550-(performance.now()-window.startAt)));
        assert.equal((await snapshot()).done,false);
        const remaining=550-endAt;
        if(remaining>1) await page.clock.runFor(remaining-1);
        assert.equal((await snapshot()).skeleton,true,'shown skeleton remains through399ms');
        assert.equal((await snapshot()).done,false,'no early replacement');
        await page.clock.runFor(1);
        await until(page,()=>window.navigationDone);
      } else await until(page,()=>window.navigationDone);
      assert.equal(await page.evaluate(()=>window.navigationPromise),true);
      assert.equal((await snapshot()).skeleton,false);
      assert.equal((await snapshot()).inert,false,'new content is interactive');
      assert.equal((await snapshot()).connected,false);
      assert.equal((await snapshot()).url,'https://bingo.test'+target);
      const transitions=await page.evaluate(()=>window.transitions);
      if(endAt>=150) assert.deepEqual(transitions,[{shown:true,at:150},{shown:false,at:Math.max(550,endAt)}]);
      assert.deepEqual(errors,[]);
      if (mode === 'push' && endAt === 0) {
        await page.evaluate(target=>{window.staleNavigation=AdminUI.navigate(target);},target);
        await until(page,()=>document.querySelector('[data-page-region]').inert);
        await page.clock.runFor(100);
        assert.equal(await page.evaluate(()=>AdminUI.navigate('/Admin/Events/Identity/c')),true);
        assert.equal(await page.evaluate(()=>window.staleNavigation),false);
        await page.clock.runFor(550);
        assert.equal(await page.locator('[data-page-skeleton]').count(),0,'cancelled delayed navigation cannot later insert its skeleton');
        assert.equal(await page.locator('[data-page-region]').evaluate(el=>el.inert),false,'cancellation cannot leave the current page inert');
      }
      outcomes.push({mode,endAt,transitions});
      await page.close();
    }
    for (const kind of ['failure-hold','abort-hold','second-shown','back-shown','cancel-back']) {
      const page=await browser.newPage({reducedMotion:'reduce'}),errors=[];
      page.on('pageerror',e=>errors.push(e.message));
      await page.clock.install({time:new Date('2030-01-01T00:00:00Z')});
      await page.route('https://bingo.test/**',route=>{
        const url=new URL(route.request().url());
        if(url.pathname.startsWith('/js/')||url.pathname.startsWith('/css/'))return route.fulfill({contentType:url.pathname.endsWith('.js')?'text/javascript':'text/css',body:fs.readFileSync(root+url.pathname,'utf8')});
        if(url.pathname==='/fixture-page.mjs')return route.fulfill({contentType:'text/javascript',body:moduleSource});
        return route.fulfill({contentType:'text/html',body:shell('a')});
      });
      await page.goto('https://bingo.test/Admin/Events/Identity/a');await until(page,()=>window.fixtureReady);
      await page.locator('input[name="name"]').focus();
      if(kind==='back-shown') {
        await page.evaluate(()=>{const main=document.querySelector('[data-page-region]');const wrap=document.createElement('div');wrap.id='fixture-scroll';wrap.dataset.adminScrollRegion='';wrap.style.cssText='width:50px;height:50px;overflow:auto';wrap.innerHTML='<div style="width:500px;height:500px"></div>';main.append(wrap);wrap.scrollLeft=80;wrap.scrollTop=50;});
        await page.evaluate(()=>AdminUI.navigate('/Admin/Events/Identity/c'));
        await page.locator('input[name="name"]').focus();
      }
      await page.clock.pauseAt(new Date('2030-01-01T00:01:00Z'));
      await page.evaluate(html=>{
        window.requests=[];window.timerLog=[];window.old=document.querySelector('#draft');window.flickers=[];
        const timer=window.setTimeout;window.setTimeout=(fn,ms,...args)=>{timerLog.push(ms);return timer(fn,ms,...args);};
        window.fetch=(_url,options)=>new Promise((resolve,reject)=>{
          const entry={resolve:()=>resolve(new Response(html,{headers:{'Content-Type':'text/html'}})),reject:()=>reject(new TypeError('Offline')),aborted:false};
          requests.push(entry);options.signal.addEventListener('abort',()=>{entry.aborted=true;reject(new DOMException('Aborted','AbortError'));});
        });
        window.started=performance.now();
        new MutationObserver(()=>{if(old.isConnected&&old.checkVisibility()&&performance.now()-started>=150)flickers.push(performance.now()-started);}).observe(document.querySelector('[data-page-region]').parentNode,{childList:true,subtree:true,attributes:true});
        window.first=AdminUI.navigate('/Admin/Events/Identity/b');
      },shell('b').replace('</main>','<div id="fixture-scroll" data-admin-scroll-region style="width:50px;height:50px;overflow:auto"><div style="width:500px;height:500px"></div></div></main>'));
      await until(page,()=>requests.length===1);await page.clock.runFor(150);
      assert.equal(await page.locator('[data-page-skeleton]').count(),1);
      await page.clock.runFor(50);
      if(kind==='failure-hold') {await page.evaluate(()=>requests[0].reject());await until(page,()=>timerLog.includes(350));}
      else if(kind==='cancel-back') {
        await page.evaluate(()=>{history.pushState({adminDesignIndex:0},'',location.href);history.back();});
        await until(page,()=>!document.querySelector('[data-page-skeleton]'));
        assert.equal(await page.evaluate(()=>document.activeElement===document.querySelector('input[name="name"]')),true,'cancel restores pre-inert input');
        assert.equal(await page.evaluate(()=>document.querySelector('[data-page-region]').inert),false);
        assert.equal(await page.evaluate(()=>requests[0].aborted),true);
        await page.clock.runFor(400);assert.equal(await page.locator('[data-page-skeleton]').count(),0);
        await page.close();continue;
      } else {
        if(kind==='abort-hold'){await page.evaluate(()=>requests[0].resolve());await until(page,()=>timerLog.includes(350));}
        if(kind==='back-shown')await page.evaluate(()=>history.back());
        else await page.evaluate(()=>{window.second=AdminUI.navigate('/Admin/Events/Identity/c');});
        await until(page,()=>requests.length===2);
        assert.equal(await page.evaluate(()=>requests[0].aborted),true);
        assert.equal(await page.evaluate(()=>window.first),false);
        assert.equal(await page.locator('[data-page-skeleton]').count(),1,'replacement is immediate, not delayed another150ms');
        await page.evaluate(()=>requests[1].resolve());await until(page,()=>timerLog.includes(350));
      }
      await page.clock.runFor(349);assert.equal(await page.locator('[data-page-skeleton]').count(),1);
      assert.equal(await page.locator('[data-load-retry]').count(),0);
      await page.clock.runFor(1);
      if(kind==='failure-hold')await until(page,()=>!!document.querySelector('[data-load-retry]'));
      else await until(page,()=>!document.querySelector('[data-page-skeleton]'));
      assert.deepEqual(await page.evaluate(()=>flickers),[],'old content never reappears between skeletons');
      if(kind==='back-shown') {
       assert.equal(await page.evaluate(()=>document.activeElement===document.querySelector('input[name="name"]')),true,'Back restores saved input focus');
       assert.deepEqual(await page.locator('#fixture-scroll').evaluate(e=>({left:e.scrollLeft,top:e.scrollTop})),{left:80,top:50});
      }
      assert.deepEqual(errors,[]);await page.close();
    }
    // Fast failure is the same decided failure UI, not a flashed loading state.
    const page=await browser.newPage({reducedMotion:'reduce'});
    await page.route('https://bingo.test/**',route=>{
      const url=new URL(route.request().url());
      if(url.pathname.startsWith('/js/')||url.pathname.startsWith('/css/'))return route.fulfill({contentType:url.pathname.endsWith('.js')?'text/javascript':'text/css',body:fs.readFileSync(root+url.pathname,'utf8')});
      if(url.pathname==='/fixture-page.mjs')return route.fulfill({contentType:'text/javascript',body:moduleSource});
      return route.fulfill({contentType:'text/html',body:shell('a')});
    });
    await page.goto('https://bingo.test/Admin/Events/Identity/a');await until(page,()=>window.fixtureReady);
    await page.evaluate(()=>{window.fetch=()=>Promise.reject(new TypeError('Offline'));});
    assert.equal(await page.evaluate(()=>AdminUI.navigate('/Admin/Events/Identity/failure')),false);
    assert.equal(await page.locator('[data-page-skeleton] .sk').count(),0);
    assert.equal(await page.locator('[data-load-retry]').count(),1);
    assert.equal(new URL(page.url()).pathname,'/Admin/Events/Identity/a');
    assert.equal(await page.locator('[data-page-region]').evaluate(el=>el.inert),false);
    console.log('PASS shared loading fake clock: '+outcomes.length+' exact fast/slow/boundary cases plus5 hold/abort/Back/focus cases, failure state/URL retained');
  } finally {await browser.close();}
})().catch(error=>{console.error(error);process.exitCode=1;});

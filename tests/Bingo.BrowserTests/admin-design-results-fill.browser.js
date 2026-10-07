// Final-look fix: long All results retain height/scroll with a filled skeleton.
const assert=require('node:assert/strict'),fs=require('node:fs'),path=require('node:path');
const {chromium,webkit}=require('playwright');
const {startFixture,login}=require('../../scripts/lib/admin-parity-fixture.cjs');
async function until(page,fn){for(let i=0;i<200;i++)if(await page.evaluate(fn))return;throw Error('Microtask checkpoint not reached');}
(async()=>{
 const name=process.env.PLAYWRIGHT_BROWSER||'chromium',engine=name==='webkit'?webkit:chromium;
 const output=path.join(process.cwd(),'artifacts/u2-final-look-fill-'+name),records=[];
 const fixture=await startFixture(process.cwd(),output);let browser;
 try{
  browser=await engine.launch({headless:true,...(name==='chromium'?{channel:process.env.PLAYWRIGHT_CHANNEL||'chromium'}:{})});
  const context=await browser.newContext({viewport:{width:390,height:600},reducedMotion:'reduce'}),page=await login(context,fixture),errors=[];
  page.on('pageerror',e=>errors.push(e.message));
  const past=await context.request.get(fixture.origin+'/Admin/Events?view=past');assert.equal(past.status(),200);const html=await past.text();
  for(const width of[390,1280]){
   await page.setViewportSize({width,height:600});await page.goto(fixture.origin+'/Admin/Events?view=all');
   await page.evaluate(async()=>{await import(document.querySelector('script[data-admin-page-script]').src);await document.fonts.ready;});
   const original=await page.evaluate(()=>{
    const results=document.querySelector('[data-directory-results]'),rows=results.querySelector('.rows'),templates=[...rows.children];
    // Expand only this owned browser fixture; reuse real Razor row markup/CSS.
    rows.replaceChildren(...Array.from({length:54},(_,i)=>templates[i%templates.length].cloneNode(true)));
    document.querySelector('.tab.is-on .tab-count').textContent='54';
    const main=document.querySelector('main.scroller'),wrap=document.querySelector('[data-directory-wrap]');main.scrollTop=600;wrap.scrollLeft=140;
    return{rows:rows.children.length,height:results.getBoundingClientRect().height,top:main.scrollTop,left:wrap.scrollLeft,minHeight:results.style.minHeight};
   });assert.equal(original.rows,54);assert.ok(original.top>0);
   await page.clock.install({time:new Date('2030-01-01T00:00:00Z')});await page.clock.pauseAt(new Date('2030-01-01T00:01:00Z'));
   await page.evaluate(html=>{
    window.requests=[];window.timers=[];const timer=setTimeout;window.setTimeout=(fn,ms,...a)=>{timers.push(ms);return timer(fn,ms,...a);};
    const fetch=window.fetch;window.fetch=(url,options)=>new URL(url,location.href).pathname==='/Admin/Events'?new Promise((resolve,reject)=>{
     const request={aborted:false,resolve:()=>resolve(new Response(html,{headers:{'Content-Type':'text/html'}}))};requests.push(request);
     options.signal.addEventListener('abort',()=>{request.aborted=true;reject(new DOMException('Aborted','AbortError'));},{once:true});
    }):fetch(url,options);
    const past=[...document.querySelectorAll('.tabs input')].find(e=>new URL(e.dataset.directoryUrl,location.href).searchParams.get('view')==='past');past.focus({preventScroll:true});past.click();
   },html);await until(page,()=>requests.length===1);await page.clock.runFor(149);assert.equal(await page.locator('[data-update-skeleton]').count(),0);await page.clock.runFor(1);
   const measure=()=>page.evaluate(()=>{
    const results=document.querySelector('[data-directory-results]'),sk=results.querySelector('[data-update-skeleton]'),rows=[...sk.querySelectorAll('.sk-row')],bounds=results.getBoundingClientRect(),last=rows.at(-1).getBoundingClientRect();
    return{height:bounds.height,placeholderHeight:sk.getBoundingClientRect().height,rowCount:rows.length,rowHeight:last.height,gap:Math.max(0,bounds.bottom-last.bottom),top:document.querySelector('main.scroller').scrollTop,left:document.querySelector('[data-directory-wrap]').scrollLeft};
   });
   const check=async phase=>{
    const sample=await measure();records.push({engine:name,width,phase,original,sample});
    assert.equal(sample.height,original.height,'locked height unchanged');assert.equal(sample.placeholderHeight,original.height,'skeleton fills the locked height');
    assert.ok(sample.rowCount>7,'long list repeats skeleton rows');assert.ok(sample.gap<sample.rowHeight,'gap under one row height');
    assert.deepEqual({top:sample.top,left:sample.left},{top:original.top,left:original.left},'scroll unchanged during skeleton');
   };
   await check('initial');
   // A successor inherits the same height and replaces the pending rows again.
   await page.clock.runFor(50);await page.evaluate(()=>document.querySelector('#directory-sort-identity').click());await until(page,()=>requests.length===2);
   assert.equal(await page.evaluate(()=>requests[0].aborted),true);await check('inherited');
   await page.evaluate(()=>requests[1].resolve());await until(page,()=>timers.includes(350));await page.clock.runFor(349);assert.equal(await page.locator('[data-update-skeleton]').count(),1);
   await page.clock.runFor(1);await until(page,()=>!document.querySelector('[data-update-skeleton]'));
   const loaded=await page.locator('[data-directory-results]').evaluate(e=>({height:e.getBoundingClientRect().height,minHeight:e.style.minHeight,rows:e.querySelectorAll('.row').length}));
   assert.equal(loaded.minHeight,original.minHeight);assert.ok(loaded.height<original.height,'short results return to real height');assert.ok(loaded.rows>0&&loaded.rows<50);
   assert.equal(new URL(page.url()).searchParams.get('view'),'past');assert.deepEqual(errors,[]);
   records.push({engine:name,width,phase:'loaded',loaded});console.log('PASS '+name+' '+width+' long All54 → Past: filled/locked/scroll, inherited, real height');
   await page.clock.resume();
  }
 }finally{await browser?.close();await fixture.close();fs.writeFileSync(path.join(output,'measurements.json'),JSON.stringify(records,null,2)+'\n');}
})().catch(error=>{console.error(error);process.exitCode=1;});

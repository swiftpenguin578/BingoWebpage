const assert=require('node:assert/strict');
const {chromium,webkit}=require('playwright');
const path=require('node:path');
const {startFixture,login}=require('../../scripts/lib/admin-parity-fixture.cjs');
async function until(page,predicate){for(let i=0;i<200;i++)if(await page.evaluate(predicate))return;throw Error('Microtask checkpoint not reached');}
(async()=>{
 const engine=process.env.PLAYWRIGHT_BROWSER==='webkit'?webkit:chromium;
 let passed=0;
 for(const owner of[false,true]){
  const fixture=await startFixture(process.cwd(),path.join(process.cwd(),'artifacts/u2-rem3-views-'+process.env.PLAYWRIGHT_BROWSER+'-'+owner),owner?{BINGO_PARITY_DIRECTORY_FAULT:'1'}:{});
  let browser;
  try{
   browser=await engine.launch({headless:true,...(engine===chromium?{channel:process.env.PLAYWRIGHT_CHANNEL||'chromium'}:{})});
   const context=await browser.newContext({reducedMotion:'reduce'}),errors=[];let page=await login(context,fixture);
   page.on('pageerror',e=>errors.push(e.message));
   const views=owner?['all','current','past','hidden']:['all','current','past'],html={};
   for(const view of views){const response=await context.request.get(fixture.origin+'/Admin/Events?view='+view);assert.equal(response.status(),200);html[view]=await response.text();}
   for(const from of views)for(const to of views)if(from!==to){
    await page.close();page=await context.newPage();page.on('pageerror',e=>errors.push(e.message));
    await page.goto(fixture.origin+'/Admin/Events?view='+from);
    await page.evaluate(async()=>{await import(document.querySelector('script[data-admin-page-script]').src);await document.fonts.ready;});
    await page.waitForFunction(()=>window.AdminUI&&parseFloat(getComputedStyle(document.querySelector('.ev-tbl')).getPropertyValue('--table-min'))>0);
    await page.clock.install({time:new Date('2030-01-01T00:00:00Z')});await page.clock.pauseAt(new Date('2030-01-01T00:01:00Z'));
    const expected=await page.evaluate(source=>{
     const doc=new DOMParser().parseFromString(source,'text/html');
     return[...doc.querySelectorAll('#directory-phase-menu button')].map(node=>({text:node.textContent.trim(),url:node.dataset.directoryUrl,checked:node.getAttribute('aria-checked')}));
    },html[to]);
    await page.evaluate(({source,to})=>{
     window.phaseOpener=document.querySelector('#phase-btn');window.requests=[];
     const original=window.fetch;
     window.fetch=(url,options)=>String(url).includes('/Admin/Events?')?new Promise(resolve=>requests.push({url:String(url),fulfill:()=>resolve(new Response(source,{headers:{'Content-Type':'text/html'}}))})):original(url,options);
     const input=[...document.querySelectorAll('.tabs input')].find(node=>new URL(node.dataset.directoryUrl,location.href).searchParams.get('view')===to);
     window.chosen=input;input.focus();input.click();
    },{source:html[to],to});
    await until(page,()=>requests.length===1);
    await page.evaluate(()=>requests[0].fulfill());
    for(let i=0;i<200;i++){if(await page.evaluate(view=>new URL(location.href).searchParams.get('view')===view,to))break;if(i===199)throw Error(from+' -> '+to+' did not commit');}
    assert.deepEqual(await page.locator('#directory-phase-menu button').evaluateAll(nodes=>nodes.map(node=>({text:node.textContent.trim(),url:node.dataset.directoryUrl,checked:node.getAttribute('aria-checked')}))),expected,from+' -> '+to+' full server menu');
    assert.equal(await page.locator('[data-load-retry]').count(),0);
    assert.equal(await page.evaluate(()=>document.querySelector('#phase-btn')===phaseOpener&&chosen.isConnected&&document.activeElement===chosen),true);
    assert.equal(await page.evaluate(()=>requests.length),1);
    passed++;
   }
   assert.deepEqual(errors,[]);await context.close();
  }finally{await browser?.close();await fixture.close();}
 }
 console.log('PASS '+passed+' view transitions: exact server option lists/URLs, no failure, retained opener/tab focus');
})().catch(error=>{console.error(error);process.exitCode=1;});

// Brief74 5a: loaded reference geometry with real attention + loading-only reservation.
const assert=require('node:assert/strict'),fs=require('node:fs'),path=require('node:path');
const {chromium,webkit}=require('playwright');
const {startFixture,login,referencePage}=require('../../scripts/lib/admin-parity-fixture.cjs');
const {settle}=require('../../scripts/lib/admin-parity-compare.cjs');
async function until(page,fn){for(let i=0;i<200;i++)if(await page.evaluate(fn))return;throw Error('Microtask checkpoint not reached');}
(async()=>{
 const name=process.env.PLAYWRIGHT_BROWSER||'chromium',engine=name==='webkit'?webkit:chromium,records=[];
 const output=path.join(process.cwd(),'artifacts/u2-rem3-events-header-'+name),fixture=await startFixture(process.cwd(),output,{BINGO_PARITY_UR_PROFILE:'live'});let browser;
 try{
  browser=await engine.launch({headless:true,...(name==='chromium'?{channel:process.env.PLAYWRIGHT_CHANNEL||'chromium'}:{})});
  const context=await browser.newContext({reducedMotion:'reduce'}),refContext=await browser.newContext({reducedMotion:'reduce'});
  const signed=await login(context,fixture);await signed.goto(fixture.origin+'/Admin/Events');await signed.locator('.summary-btn').waitFor();
  const counts=await signed.locator('.summary b').allTextContents();assert.equal(counts.length,3);
  const ref=await referencePage(refContext,fixture,process.cwd(),'Events.dc.html');
  await ref.evaluate(counts=>{const c=__parityReference,original=c.renderVals;c.renderVals=function(){const v=original.call(this);return {...v,sum:{...v.sum,live:+counts[0],upcoming:+counts[1],attention:+counts[2],hasAttention:true,calm:false}};};return new Promise(resolve=>c.setState({theme:'dark'},resolve));},counts);
  const html=await(await context.request.get(fixture.origin+'/Admin/Events')).text();
  const selectors=['.page-head','.h1','.summary','.summary-btn','.head-actions','.head-actions .btn','.toolbar'];
  const box=page=>page.evaluate(selectors=>Object.fromEntries(selectors.map(s=>{const e=[...document.querySelectorAll(s)].find(e=>e.checkVisibility()),r=e.getBoundingClientRect();return[s,{x:r.x,y:r.y,width:r.width,height:r.height,minHeight:getComputedStyle(e).minHeight}];})),selectors);
  await signed.close();
  for(const width of[390,640,860,861,1280]){
   console.log('Checking '+name+' Events header '+width);
   const page=await context.newPage();await page.setViewportSize({width,height:1000});await ref.setViewportSize({width,height:1000});
   await page.goto(fixture.origin+'/Admin/Events');await page.evaluate(async()=>{await import(document.querySelector('script[data-admin-page-script]').src);await document.fonts.ready;});await settle(page);await settle(ref);
   // Large-count presentation fixture deliberately forces wrapping without
   // changing production seed populations or rules. Keep reference/app data equal.
   const wrapCounts=width===390?['1000','1000','1000']:counts;
   await page.locator('.summary b').evaluateAll((nodes,values)=>nodes.forEach((e,i)=>e.textContent=values[i]),wrapCounts);
   await ref.evaluate(values=>{const c=__parityReference,original=c.renderVals;c.renderVals=function(){const v=original.call(this);return {...v,sum:{...v.sum,live:+values[0],upcoming:+values[1],attention:+values[2]}};};return new Promise(resolve=>c.setState({viewKey:(c.state.viewKey||0)+1},resolve));},wrapCounts);await settle(page);await settle(ref);
   const loaded=await box(page),reference=await box(ref);
   for(const selector of selectors)for(const axis of['x','y','width','height'])assert.ok(Math.abs(loaded[selector][axis]-reference[selector][axis])<=0.1,name+' '+width+' '+selector+'.'+axis+' '+JSON.stringify({loaded,reference}));
   assert.equal(loaded['.summary'].minHeight,'0px');if(width===390)assert.ok(loaded['.summary-btn'].y>loaded['.summary'].y,'attention wraps at390');
   console.log('Loaded reference matched '+width);await page.clock.install({time:new Date('2030-01-01T00:00:00Z')});await page.clock.pauseAt(new Date('2030-01-01T00:01:00Z'));
   await page.evaluate(html=>{window.timers=[];const timer=setTimeout;window.setTimeout=(fn,ms,...args)=>{timers.push(ms);return timer(fn,ms,...args);};window.fetch=()=>new Promise(resolve=>window.fulfill=()=>resolve(new Response(html,{headers:{'Content-Type':'text/html'}})));window.done=false;void AdminUI.navigate('/Admin/Events?header=1').then(()=>done=true);},html);
   await until(page,()=>!!window.fulfill);await page.clock.runFor(150);
   const loading=await page.locator('[data-page-skeleton] .summary').evaluate(e=>({height:e.getBoundingClientRect().height,minHeight:parseFloat(getComputedStyle(e).minHeight),line:parseFloat(getComputedStyle(e).lineHeight),gap:parseFloat(getComputedStyle(e).gap),text:e.textContent}));
   assert.equal(loading.text,'');assert.ok(Math.abs(loading.height-loading.minHeight)<=0.1,'layout pixel quantization');assert.ok(Math.abs(loading.height-(width<=860?2*loading.line+loading.gap:loading.line))<=0.1);
   console.log('Loading reservation matched '+width);await page.evaluate(()=>fulfill());await until(page,()=>timers.includes(400));await page.clock.runFor(399);assert.equal(await page.locator('[data-page-skeleton]').count(),1);await page.clock.runFor(1);await until(page,()=>done);
   records.push({engine:name,width,loaded,reference,loading});await page.clock.resume();await page.close();
  }
  console.log('PASS5 loaded Events headers match reference, attention present/wrapping; loading-only reservation, exact150/400');
 }finally{await browser?.close();await fixture.close();fs.writeFileSync(path.join(output,'positions.json'),JSON.stringify(records,null,2));}
})().catch(error=>{console.error(error);process.exitCode=1;});

// Page-specific archived Identity presentation; registered-family cases moved to the generic gate.
const assert=require('node:assert/strict'),path=require('node:path');
const {chromium,webkit}=require('playwright');
const {startFixture,login}=require('../../scripts/lib/admin-parity-fixture.cjs');
(async()=>{
 const engine=process.env.PLAYWRIGHT_BROWSER==='webkit'?webkit:chromium;
 const fixture=await startFixture(process.cwd(),path.join(process.cwd(),'artifacts/u2-rem4-family-'+engine.name()));
 let browser,passed=0;
 try{
  browser=await engine.launch({headless:true,...(engine===chromium?{channel:process.env.PLAYWRIGHT_CHANNEL||'chromium'}:{})});
  const context=await browser.newContext({viewport:{width:1280,height:900},reducedMotion:'reduce'});
  const page=await login(context,fixture),errors=[];page.on('pageerror',e=>errors.push(e.message));
  await page.goto(fixture.origin+'/Admin');await page.locator('[name=culture][value=da]').click();await page.waitForFunction(()=>document.documentElement.lang==='da');
  await page.goto(fixture.origin+'/Admin/Events/Identity/'+fixture.events['spring-archived']);
  await page.waitForFunction(()=>document.querySelector('.ro-value'));assert.equal(await page.locator('[data-page-region]>.page').getAttribute('data-page-family'),'identity');
  assert.equal(await page.locator('.ro-value').first().evaluate(e=>getComputedStyle(e).whiteSpace),'pre-wrap');passed++;
  assert.deepEqual(errors,[]);await context.close();
  console.log('PASS '+passed+' archived Identity Danish/read-only presentation check');
 }finally{await browser?.close();await fixture.close();}
})().catch(error=>{console.error(error);process.exitCode=1;});

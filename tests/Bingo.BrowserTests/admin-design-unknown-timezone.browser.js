const assert=require('node:assert/strict'),fs=require('node:fs'),path=require('node:path');
const {chromium,webkit}=require('playwright');
const {startFixture,login}=require('../../scripts/lib/admin-parity-fixture.cjs');
(async()=>{
 const engine=process.env.PLAYWRIGHT_BROWSER||'chromium',output=path.join(process.cwd(),'artifacts/unknown-timezone-'+engine);
 const fixture=await startFixture(process.cwd(),output,{BINGO_PARITY_UNKNOWN_TIMEZONE:'1'});let browser;const results=[];
 try{
  browser=await(engine==='webkit'?webkit:chromium).launch({headless:true});const context=await browser.newContext(),page=await login(context,fixture),errors=[];page.on('pageerror',e=>errors.push(e.message));
  const id=fixture.events['unknown-timezone'],schedule='/Admin/Events/Schedule/'+id,signup='/Admin/Events/SignupSetup/'+id,identity='/Admin/Events/Identity/'+id;
  for(const language of ['en','da']){
   await page.goto(fixture.origin+identity);await page.locator('[data-identity-editor]').waitFor();
   if(language==='da'){await page.locator('[name=culture][value=da]').click();await page.waitForFunction(()=>document.documentElement.lang==='da');}
   await page.evaluate(url=>AdminUI.navigate(url),schedule);await page.locator('[data-timezone-fallback]').waitFor();
   const current=await page.locator('[data-schedule-editor]').getAttribute('data-current').then(JSON.parse);
   assert.equal(current.timezone,'Review/Unknown');assert.equal(current.displayTimezone,'UTC');assert.ok(Object.values(current.editable).every(v=>!v),'unsupported timezone schedule remains read-only');
   assert.equal(await page.locator('input[name="Input.EventStartsLocal"]').inputValue(),'2027-06-14T18:00');
   assert.match(await page.locator('[data-timezone-fallback]').textContent(),/Review\/Unknown.*UTC/);
   assert.match(await page.locator('.page-head .summary').textContent(),/UTC/);assert.match(await page.locator('[data-uploads-close]').textContent(),/22:30/);
   assert.equal(await page.locator('[data-toast],[data-page-init-failure]').count(),0);
   const observed=await(await context.request.get(fixture.origin+schedule+'?handler=Current')).json();assert.equal(observed.timezone,current.timezone);assert.equal(observed.version,current.version);assert.deepEqual(observed.values,current.values);
   await page.locator('[data-shell-sidebar] a[data-shell-link][href="'+identity+'"]').click();await page.locator('[data-identity-editor]').waitFor();assert.equal(await page.locator('[name="Input.Timezone"]').inputValue(),'Review/Unknown');
   await page.evaluate(url=>AdminUI.navigate(url+'?tab=form'),signup);await page.locator('[data-signup-setup]').waitFor();
   assert.match(await page.locator('[data-timezone-fallback]').textContent(),/Review\/Unknown.*UTC/);assert.match(await page.locator('[data-response-text]').textContent(),/12:00/);
   assert.equal(await page.locator('[data-toast],[data-page-init-failure]').count(),0);
   await page.locator('[data-timezone-fallback] a').click();await page.locator('[data-identity-editor]').waitFor();
   results.push(language+': UTC schedule/derived/reason/response display, stored ID/version/instants preserved, no toast, sidebar and explanatory links recover');
  }
  assert.deepEqual(errors,[]);console.log('PASS '+results.join('; '));
 }finally{await browser?.close();await fixture.close();fs.writeFileSync(path.join(output,'checks.json'),JSON.stringify({engine,results},null,2));}
})().catch(error=>{console.error(error);process.exitCode=1;});

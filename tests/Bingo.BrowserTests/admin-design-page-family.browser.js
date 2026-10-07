// Real Danish pages: full load, shell navigation and the actual culture switch.
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
  const paths={dashboard:'/Admin',events:'/Admin/Events/Index',identity:'/Admin/Events/Identity/'+fixture.events['autumn-bingo-2027']};
  const check=async kind=>{
   await page.waitForFunction(kind=>document.documentElement.lang==='da'&&document.querySelector('[data-page-region]>.page')?.dataset.pageFamily===kind,kind);
   assert.equal(await page.locator('[data-page-region]>.page>.page-head').getAttribute('data-page-family'),kind);
   await page.waitForFunction(kind=>{
    if(kind==='identity')return getComputedStyle(document.querySelector('.id-desc')).minHeight==='132px'&&getComputedStyle(document.querySelector('.form-banners')).display==='none';
    if(kind==='dashboard')return getComputedStyle(document.querySelector('.dash-grid')).display==='grid';
    return getComputedStyle(document.querySelector('.tbl.ev-tbl')).getPropertyValue('--table-min').trim()==='990px';
   },kind);
   if(kind==='identity')assert.equal(await page.locator('.h1').textContent(),'Identitet');
  };
  await page.goto(fixture.origin+'/Admin');await page.locator('[name=culture][value=da]').click();await page.waitForFunction(()=>document.documentElement.lang==='da');
  for(const [kind,url]of Object.entries(paths)){
   await page.goto(fixture.origin+url);await check(kind);passed++;
   const from=kind==='dashboard'?'events':'dashboard';await page.goto(fixture.origin+paths[from]);
   await page.waitForFunction(()=>window.AdminUI);await page.evaluate(url=>AdminUI.navigate(url),url);await check(kind);passed++;
   await page.locator('[name=culture][value=en]').click();await page.waitForFunction(()=>document.documentElement.lang==='en');
   await page.evaluate(()=>window.languageDocument='retained');await page.locator('[name=culture][value=da]').click();await check(kind);
   assert.equal(await page.evaluate(()=>languageDocument),'retained');passed++;
  }
  await page.goto(fixture.origin+'/Admin/Events/Identity/'+fixture.events['spring-archived']);
  await page.waitForFunction(()=>document.querySelector('.ro-value'));assert.equal(await page.locator('[data-page-region]>.page').getAttribute('data-page-family'),'identity');
  assert.equal(await page.locator('.ro-value').first().evaluate(e=>getComputedStyle(e).whiteSpace),'pre-wrap');passed++;
  assert.deepEqual(errors,[]);await context.close();
  console.log('PASS '+passed+' real Danish family/style checks:3 pages×full/shell/language + readonly Identity');
 }finally{await browser?.close();await fixture.close();}
})().catch(error=>{console.error(error);process.exitCode=1;});

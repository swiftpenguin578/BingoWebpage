// U10 part 2 item 4 (user rulings "U10 part 2 additions" and U10-E1): the sidebar top row.
// Expanded: the logo links to the public front page (no shell navigation) and only the account button highlights on hover.
// Collapsed: the logo stays visible and opens the account popout; Escape returns focus to it.
const assert=require('node:assert/strict'),path=require('node:path');
const {chromium,webkit}=require('playwright');
const {startFixture,login}=require('../../scripts/lib/admin-parity-fixture.cjs');
(async()=>{
 const name=process.env.PLAYWRIGHT_BROWSER==='webkit'?'webkit':'chromium',engine=name==='webkit'?webkit:chromium;
 const fixture=await startFixture(process.cwd(),path.join(process.cwd(),'artifacts/u10-sidebar-top-'+name));
 let browser;
 try{
  browser=await engine.launch({headless:true,...(engine===chromium?{channel:process.env.PLAYWRIGHT_CHANNEL||'chromium'}:{})});
  for(const culture of ['en','da']){
   const context=await browser.newContext({viewport:{width:1280,height:900},reducedMotion:'reduce'}),errors=[];
   const page=await login(context,fixture);page.on('pageerror',e=>errors.push(e.message));await page.goto(fixture.origin+'/Admin');
   if(culture==='da'){await page.locator('[name=culture][value=da]').click();await page.waitForFunction(()=>document.documentElement.lang==='da');}
   await page.waitForFunction(()=>window.AdminUI);
   const link=page.locator('.side-top a.design-logo-link'),logoButton=page.locator('.side-top button.design-logo-account'),account=page.locator('.side-top .account-btn');
   // Expanded: the logo is a plain link to "/" and the logo button is not rendered visibly.
   assert.equal(await link.getAttribute('href'),'/');
   assert.equal(await link.getAttribute('data-shell-link'),null);
   assert.equal(await link.getAttribute('aria-label'),culture==='da'?'Gå til det offentlige site':'Go to the public site');
   assert.equal(await link.isVisible(),true);assert.equal(await logoButton.isVisible(),false);assert.equal(await account.isVisible(),true);
   assert.equal(await account.locator('.logo').count(),0,'the logo is no longer inside the account button');
   const surface=()=>account.evaluate(e=>getComputedStyle(e).backgroundColor);
   const rest=await surface();
   await link.hover();assert.equal(await surface(),rest,'hovering the logo does not highlight the account button');
   await account.hover();await page.waitForFunction(([before])=>getComputedStyle(document.querySelector('.side-top .account-btn')).backgroundColor!==before,[rest]);
   // Collapsed: the logo stays, the name/role button hides, and the logo opens the account popout.
   await page.locator('[data-side-toggle].collapse-btn').click();
   await page.waitForFunction(()=>document.querySelector('[data-shell-sidebar]').classList.contains('is-collapsed'));
   assert.equal(await link.isVisible(),false);assert.equal(await logoButton.isVisible(),true);assert.equal(await account.isVisible(),false);
   const accessible=await logoButton.getAttribute('aria-label');assert.match(accessible,/ · /);
   assert.equal(await logoButton.getAttribute('aria-haspopup'),'menu');
   const box=await logoButton.boundingBox();assert.ok(box.width>=27&&box.width<=29&&box.height>=27&&box.height<=29,'collapsed logo keeps the 28px brand box');
   await logoButton.click();
   await page.waitForFunction(()=>!document.querySelector('#admin-account-menu').hidden);
   assert.equal(await logoButton.getAttribute('aria-expanded'),'true');
   assert.equal(page.url(),fixture.origin+'/Admin','the collapsed logo does not navigate');
   await page.keyboard.press('Escape');
   await page.waitForFunction(()=>document.querySelector('#admin-account-menu').hidden);
   assert.equal(await logoButton.evaluate(e=>e===document.activeElement),true,'Escape returns focus to the logo');
   assert.equal(await logoButton.getAttribute('aria-expanded'),'false');
   // Expanded again: the logo navigates to the public front page.
   await page.locator('[data-side-toggle].collapse-btn').click();
   await page.waitForFunction(()=>!document.querySelector('[data-shell-sidebar]').classList.contains('is-collapsed'));
   await Promise.all([page.waitForURL(url=>url.pathname==='/',{waitUntil:'commit'}),link.click()]);
   assert.deepEqual(errors,[]);
   await context.close();
  }
  console.log(`PASS admin-design-sidebar-top [${name}]`);
 }finally{await browser?.close();await fixture.close();}
})().catch(error=>{console.error(error);process.exit(1);});

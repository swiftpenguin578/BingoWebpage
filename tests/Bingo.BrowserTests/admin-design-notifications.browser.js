// U10 part 2 item 6 (user, U10-Q3 b): the topbar notification panel.
// Header with unread count, rows with unread dot/title/detail/relative time (whole row a menu item), "All notifications",
// "Mark all as read" through the existing handler, empty state; Danish text; keyboard roles, Escape returns focus to the bell.
const assert=require('node:assert/strict'),path=require('node:path');
const {chromium,webkit}=require('playwright');
const {startFixture,login}=require('../../scripts/lib/admin-parity-fixture.cjs');
const words={
 en:{heading:'Notifications',unread:'9 unread',first:'1 minute ago',title:'Admin access granted',all:'All notifications',mark:'Mark all as read',empty:'No notifications',bell:'Notifications, 0 unread'},
 da:{heading:'Notifikationer',unread:'9 ulæste',first:'for 1 minut siden',title:null,all:null,mark:null,empty:'Ingen notifikationer',bell:'Notifikationer, 0 ulæste'}
};
(async()=>{
 const name=process.env.PLAYWRIGHT_BROWSER==='webkit'?'webkit':'chromium',engine=name==='webkit'?webkit:chromium;
 const fixture=await startFixture(process.cwd(),path.join(process.cwd(),'artifacts/u10-notifications-'+name),{BINGO_PARITY_NOTIFICATIONS:'1'});
 let browser;
 try{
  browser=await engine.launch({headless:true,...(engine===chromium?{channel:process.env.PLAYWRIGHT_CHANNEL||'chromium'}:{})});
  const context=await browser.newContext({viewport:{width:1280,height:900},reducedMotion:'reduce'}),errors=[];
  const page=await login(context,fixture);page.on('pageerror',e=>errors.push(e.message));
  const bell=page.locator('[data-shell-topbar] [data-menu-target=admin-notifications-menu]'),panel=page.locator('#admin-notifications-menu');
  const open=async()=>{await bell.click();await page.waitForFunction(()=>!document.querySelector('#admin-notifications-menu').hidden);await panel.evaluate(e=>Promise.all(e.getAnimations().map(animation=>animation.finished)));};
  const close=async()=>{await page.keyboard.press('Escape');await page.waitForFunction(()=>document.querySelector('#admin-notifications-menu').hidden);assert.equal(await bell.evaluate(e=>e===document.activeElement),true,'Escape returns focus to the bell');};
  const setCulture=async culture=>{await page.goto(fixture.origin+'/Admin');await page.waitForFunction(()=>window.AdminUI);if(await page.evaluate(()=>document.documentElement.lang)!==culture){await page.locator(`[name=culture][value=${culture}]`).click();await page.waitForFunction(c=>document.documentElement.lang===c,culture);}};
  for(const culture of ['da','en']){
   const w=words[culture];await setCulture(culture);
   await open();
   assert.equal(await panel.getAttribute('role'),'menu');
   assert.equal(await panel.getAttribute('aria-label'),w.heading);
   assert.equal((await panel.locator('.design-notif-head b').textContent()).trim(),w.heading);
   assert.equal((await panel.locator('[data-notification-unread]').textContent()).trim(),w.unread);
   const rows=panel.locator('[data-notification-row]');
   const count=await rows.count();assert.ok(count>=6&&count<=8,`5–8 rows (${count})`);
   const unread=panel.locator('[data-notification-row][data-unread=true]');assert.equal(await unread.count(),6,'the newest six unread personal notifications');
   const first=unread.first();
   assert.equal(await first.getAttribute('role'),'menuitem');
   assert.equal(await first.locator('.design-notif-dot.dot').isVisible(),true);
   assert.equal((await first.locator('.design-notif-time').textContent()).trim(),w.first);
   if(w.title)assert.equal((await first.locator('.design-notif-title').textContent()).trim(),w.title);
   assert.equal(await first.locator('.design-notif-detail').evaluate(e=>getComputedStyle(e).whiteSpace),'nowrap','one detail line');
   // Every row is one link: its box covers title, detail and time.
   const box=await first.boundingBox(),time=await first.locator('.design-notif-time').boundingBox();assert.ok(time.x+time.width<=box.x+box.width+0.5);
   assert.match(await first.getAttribute('href'),/^\/notifications\?read=[0-9a-f-]+$/);
   // Untranslated text: the Danish panel must not contain the English phrases.
   if(culture==='da'){const text=await panel.textContent();for(const english of ['Notifications','unread','minute ago','All notifications','Mark all as read','Admin access granted','Event cancelled'])assert.equal(text.includes(english),false,`Danish panel shows "${english}"`);}
   // Keyboard: the first control is focused, ArrowDown moves through the rows, Escape closes and returns focus to the bell.
   assert.equal(await page.evaluate(()=>document.activeElement.closest('#admin-notifications-menu')!==null),true);
   await page.keyboard.press('ArrowDown');assert.equal(await page.evaluate(()=>document.activeElement.matches('#admin-notifications-menu [role=menuitem]')),true);
   await close();
   // Panel stays inside the viewport at a phone width too.
   await page.setViewportSize({width:390,height:844});await page.waitForFunction(()=>innerWidth===390&&matchMedia('(max-width: 860px)').matches);await open();
   const pb=await panel.boundingBox();assert.ok(pb.x>=0&&pb.x+pb.width<=390,'panel fits the phone width');
   await close();await page.setViewportSize({width:1280,height:900});await page.waitForFunction(()=>innerWidth===1280);
  }
  // English footer and the existing Mark all as read handler.
  await open();
  const all=panel.locator('.design-notif-foot a[role=menuitem]');assert.equal((await all.textContent()).trim(),words.en.all);assert.equal(await all.getAttribute('href'),'/notifications');
  const mark=panel.locator('.design-notif-foot button[role=menuitem]');assert.equal((await mark.textContent()).trim(),words.en.mark);
  await Promise.all([page.waitForURL(url=>url.pathname==='/notifications',{waitUntil:'commit'}),mark.click()]);
  for(const culture of ['en','da']){
   const w=words[culture];await setCulture(culture);
   assert.equal(await bell.getAttribute('aria-label'),w.bell);
   await open();
   assert.equal(await panel.locator('[data-notification-row][data-unread=true]').count(),0);
   assert.equal(await panel.locator('.design-notif-foot button').count(),0,'no Mark all as read without unread notifications');
   if(await panel.locator('[data-notification-row]').count()===0)assert.equal((await panel.locator('[data-notification-empty] b').textContent()).trim(),w.empty);
   await close();
  }
  assert.deepEqual(errors,[]);
  await context.close();
  console.log(`PASS admin-design-notifications [${name}]`);
 }finally{await browser?.close();await fixture.close();}
})().catch(error=>{console.error(error);process.exit(1);});

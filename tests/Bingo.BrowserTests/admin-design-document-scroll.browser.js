// Events filtered/reset result states; generic full-load bounds now use the registered-page gate.
const assert=require('node:assert/strict'),fs=require('node:fs'),path=require('node:path');
const {chromium,webkit}=require('playwright');
const {startFixture,login}=require('../../scripts/lib/admin-parity-fixture.cjs');
const {settle}=require('../../scripts/lib/admin-parity-compare.cjs');
(async()=>{
 const name=process.env.PLAYWRIGHT_BROWSER||'chromium',engine=name==='webkit'?webkit:chromium;
 const output=path.join(process.cwd(),'artifacts/u2-rem5-document-scroll-'+name),records=[];
 const fixture=await startFixture(process.cwd(),output);let browser;
 try{
  browser=await engine.launch({headless:true,...(name==='chromium'?{channel:process.env.PLAYWRIGHT_CHANNEL||'chromium'}:{})});
  const context=await browser.newContext({viewport:{width:494,height:342},reducedMotion:'reduce'}),page=await login(context,fixture),errors=[];
  page.on('pageerror',e=>errors.push(e.message));page.setDefaultTimeout(10000);
  const routes={events:'/Admin/Events'};
  const snapshot=()=>page.evaluate(()=>{
   const doc=document.documentElement,main=document.querySelector('main.scroller');
   return{viewport:{width:innerWidth,height:innerHeight},document:{width:doc.scrollWidth,height:doc.scrollHeight,x:scrollX,y:scrollY},
    main:{clientHeight:main.clientHeight,scrollHeight:main.scrollHeight,scrollTop:main.scrollTop,position:getComputedStyle(main).position},
    screenReader:[...main.querySelectorAll('.sr')].map(e=>{const r=e.getBoundingClientRect();return{right:r.right,bottom:r.bottom};})};
  });
  async function check(family,width,phase){
   await page.evaluate(()=>document.fonts.ready);await settle(page);
   const before=await snapshot(),record={engine:name,family,width,height:342,phase,before};records.push(record);
   const expected={width,height:342,x:0,y:0};
   assert.deepEqual(before.viewport,{width,height:342});
   assert.deepEqual(before.document,expected,family+' '+width+' '+phase+' document must equal viewport');
   await page.locator('main.scroller').evaluate(e=>{e.scrollTop=e.scrollHeight;});
   await page.evaluate(()=>window.scrollTo(9999,9999));
   const after=record.after=await snapshot();
   assert.deepEqual(after.document,expected,'attempting document scroll must leave it bounded');
   if(after.main.scrollHeight>after.main.clientHeight)assert.ok(after.main.scrollTop>0,'content still scrolls inside main');
   assert.deepEqual(errors,[]);console.log('PASS '+family+' '+width+'×342 '+phase+' document '+width+'×342');
  }
  for(const width of[494,390,860,1280]){
   await page.setViewportSize({width,height:342});
   for(const [family,url]of Object.entries(routes)){
    await page.goto(fixture.origin+url);await page.waitForFunction(()=>window.AdminUI&&window.AdminFetch);
    if(family==='events'){
     assert.ok(await page.locator('.ev-tbl .row .sr').count(),'fixture exercises wide-row screen-reader text');
     await page.locator('#phase-btn').click();await page.getByRole('menuitemradio',{name:'Live',exact:true}).click();
     await page.waitForFunction(()=>new URL(location.href).searchParams.get('phase')==='live');
     assert.equal(await page.locator('.row').count(),1);await check(family,width,'filtered-live');
     await page.locator('#phase-btn').click();await page.getByRole('menuitemradio',{name:'All phases',exact:true}).click();
     await page.waitForFunction(()=>new URL(location.href).searchParams.get('phase')==='all');
     assert.ok(await page.locator('.ev-tbl .row .sr').count(),'filter reset inserts wide-row screen-reader text again');
     await check(family,width,'filter-reset');
    }
   }
  }
  console.log('PASS '+records.length+' rendered document-scroll checks');
 }finally{await browser?.close();await fixture.close();fs.writeFileSync(path.join(output,'measurements.json'),JSON.stringify(records,null,2)+'\n');}
})().catch(error=>{console.error(error);process.exitCode=1;});

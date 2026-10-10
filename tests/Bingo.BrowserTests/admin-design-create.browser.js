process.env.BINGO_PARITY_ENGINES=process.env.PLAYWRIGHT_BROWSER||'chromium';
const assert=require('node:assert/strict'),fs=require('node:fs'),path=require('node:path');
const {chromium,webkit}=require('playwright');
const {startFixture,login}=require('../../scripts/lib/admin-parity-fixture.cjs');
const {settle,classInventory}=require('../../scripts/lib/admin-parity-compare.cjs');
const root=process.cwd(),output=path.join(root,'artifacts/u2-create');
(async()=>{
 const dollarName="Literal {1} $& $$ $' $`";const fixture=await startFixture(root,output,{BINGO_PARITY_DOLLAR_NAME:dollarName}),results=[];let browser;
 try{
  for(const engine of(process.env.BINGO_PARITY_ENGINES||'chromium,webkit').split(',')){
   browser=await(engine==='webkit'?webkit.launch({headless:true}):chromium.launch({headless:true,channel:process.env.PLAYWRIGHT_CHANNEL||'chromium'}));
   const context=await browser.newContext({viewport:{width:1440,height:1000},reducedMotion:'reduce'});
   const app=await login(context,fixture),errors=[],responses=[];
   app.on('response',r=>{if(r.url().includes('/Admin/Events/Create')||r.url().includes('/Account/Login'))responses.push({url:r.url(),status:r.status()});});
   app.on('pageerror',e=>errors.push(e.message));app.setDefaultTimeout(10000);
   await app.goto(fixture.origin+'/Admin');await app.waitForFunction(()=>window.AdminUI);await app.evaluate(()=>AdminUI.navigate('/Admin/Events'));await app.waitForFunction(()=>window.AdminUI&&document.querySelector('[data-events-directory]'));
   const open=async()=>{await app.locator('[data-create-event]').first().click();await app.locator('#cm-name').waitFor();};
   await open();assert.equal(new URL(app.url()).searchParams.get('create'),'1');
   assert.deepEqual((await classInventory(app)).undefined,[],'every class used on the page is defined in shipped CSS');
   await app.setViewportSize({width:1440,height:1000});
   const closeLength=await app.evaluate(()=>history.length),closeState=await app.evaluate(()=>history.state.adminDesignIndex),closeRequests=[];
   app.on('request',r=>{if(r.url().startsWith(fixture.origin+'/Admin'))closeRequests.push(r.url());});
   await app.evaluate(()=>{window.directoryBeforeClose=document.querySelector('[data-events-directory]');window.closeFetches=0;window.closeSwaps=0;const fetch=window.fetch;window.fetch=(...args)=>{closeFetches++;return fetch(...args);};document.addEventListener('admin:page-changed',()=>closeSwaps++);});
   await app.locator('.m-scrim').click({position:{x:5,y:5},force:true});await app.locator('#cm-name').waitFor({state:'detached'});
   await app.waitForFunction(()=>!new URL(location.href).searchParams.has('create'));
   assert.equal(await app.evaluate(()=>history.state.adminDesignIndex),closeState-1,'close consumes pushed Create entry');
   assert.equal(await app.evaluate(()=>history.length),closeLength,'Back does not add or remove history entries');
   assert.equal(closeRequests.length,0,'close sends no HTTP request');
   assert.deepEqual(await app.evaluate(()=>({fetches:closeFetches,swaps:closeSwaps})),{fetches:0,swaps:0});
   assert.equal(await app.evaluate(()=>document.querySelector('[data-events-directory]')===window.directoryBeforeClose),true);
   assert.equal(await app.locator('[data-page-skeleton]').count(),0);
   await app.goForward();await app.locator('#cm-name').waitFor();assert.deepEqual(await app.evaluate(()=>({fetches:closeFetches,swaps:closeSwaps})),{fetches:0,swaps:0});
   await app.locator('#cm-cancel').click();await app.waitForFunction(()=>!new URL(location.href).searchParams.has('create'));
   assert.equal(await app.evaluate(()=>history.state.adminDesignIndex),closeState-1);
   assert.deepEqual(await app.evaluate(()=>({fetches:closeFetches,swaps:closeSwaps})),{fetches:0,swaps:0});
   await app.goBack();await app.locator('[data-dashboard]').waitFor();assert.equal(await app.locator('#cm-name').count(),0,'one Back after close leaves Events');
   await app.evaluate(()=>AdminUI.navigate('/Admin/Events'));await app.locator('[data-events-directory]').waitFor();
   await open();
   await app.locator('#cm-submit').focus();await app.keyboard.press('Tab');assert.equal(await app.locator('#cm-name').evaluate(e=>e===document.activeElement),true,'shared keyboard trap wraps to name');
   await app.locator('#cm-submit').click();assert.equal(await app.locator('#cm-name').getAttribute('aria-invalid'),'true');assert.equal(await app.locator('#cm-name').evaluate(e=>e===document.activeElement),true);
   await app.evaluate(()=>{window.createEvents=[];for(const type of['input','change','click'])document.querySelector('.modal-form').addEventListener(type,e=>window.createEvents.push({type,id:e.target.id,length:Array.from(document.querySelector('#cm-name').value.trim()).length,error:document.querySelector('#cm-name-err').textContent.trim()}));});
   await app.locator('#cm-name').fill('😀'.repeat(51));await settle(app);await app.locator('#cm-submit').click();assert.equal(await app.locator('[data-create-count]').textContent(),'51 / 50');assert.match(await app.locator('#cm-name-err').textContent(),/50/,JSON.stringify(await app.evaluate(()=>({events:window.createEvents,disabled:document.querySelector('#cm-submit').disabled,active:document.activeElement.id}))));
   await app.locator('#cm-name').fill(dollarName);
   assert.equal((await app.locator('#cm-name-dup [data-component-text]').textContent()),'An event called “'+dollarName+'” already exists (Setup). You can still create another; it gets its own link.');
   await app.locator('.m-scrim').click({position:{x:5,y:5},force:true});await app.getByRole('alertdialog').waitFor();await app.getByRole('button',{name:'Keep editing',exact:true}).click();assert.equal(await app.locator('#cm-name').inputValue(),dollarName);
   await app.locator('#cm-name').fill('autumn bingo 2027');assert.match(await app.locator('#cm-name-dup').textContent(),/already exists/);
   await app.keyboard.press('Escape');await app.getByRole('alertdialog').waitFor();await app.getByRole('button',{name:'Keep editing',exact:true}).click();assert.equal(await app.locator('#cm-name').inputValue(),'autumn bingo 2027');
   await app.goBack();await app.getByRole('button',{name:'Discard',exact:true}).click();await app.locator('#cm-name').waitFor({state:'detached'});assert.equal(new URL(app.url()).searchParams.has('create'),false);
   await app.goto(fixture.origin+'/Admin/Events/Create');await app.locator('#cm-name').waitFor();assert.equal(new URL(app.url()).searchParams.get('create'),'1');const direct=await app.evaluate(()=>({length:history.length,state:history.state.adminDesignIndex}));await app.evaluate(()=>{closeFetches=0;closeSwaps=0;window.directoryBeforeClose=document.querySelector('[data-events-directory]');});await app.locator('#cm-cancel').click();await app.waitForFunction(()=>!new URL(location.href).searchParams.has('create'));assert.deepEqual(await app.evaluate(()=>({length:history.length,state:history.state.adminDesignIndex})),direct);assert.deepEqual(await app.evaluate(()=>({fetches:closeFetches,swaps:closeSwaps})),{fetches:0,swaps:0});assert.equal(await app.evaluate(()=>document.querySelector('[data-events-directory]')===directoryBeforeClose),true);
   // No write is sent for an intercepted unknown outcome; readback404 unlocks a fresh key with values retained.
   await open();await app.locator('#cm-name').fill('Unknown retained draft');
   let sent,checkingKey,finishWrite;const waiting=new Promise(resolve=>finishWrite=resolve);
   await app.route('**/Admin/Events/Create',async route=>{sent=new URLSearchParams(route.request().postData());await waiting;return route.abort();});
   await app.locator('#cm-submit').click();await app.locator('.modal[aria-busy=true]').waitFor();assert.equal(await app.locator('#cm-cancel').isDisabled(),true);assert.equal(await app.locator('#cm-name').isDisabled(),true);
   assert.equal(await app.evaluate(()=>window.AdminUI.navigate('/Admin')),false,'pending create refuses leaving');await app.keyboard.press('Escape');assert.equal(await app.locator('#cm-name').count(),1);
   finishWrite();await app.getByRole('button',{name:'Check again',exact:true}).waitFor();assert.equal(await app.locator('#cm-name').isDisabled(),true);
   await app.route('**/Admin/Events/Create?handler=CheckAgain*',route=>{checkingKey=new URL(route.request().url()).searchParams.get('requestId');return route.fulfill({status:404,body:''});});
   await app.locator('#cm-submit').click();await app.locator('#cm-not-found').waitFor();assert.equal((await app.locator('#cm-not-found [data-component-text]').textContent()),"We couldn't find it. It may not have been created, or it was removed since. You can create it again with these details.");assert.equal(checkingKey,sent.get('Input.RequestId'));assert.equal(await app.locator('#cm-name').inputValue(),'Unknown retained draft');assert.equal(await app.locator('#cm-name').isEnabled(),true);
   let fresh;await app.unroute('**/Admin/Events/Create');await app.route('**/Admin/Events/Create',route=>{fresh=new URLSearchParams(route.request().postData()).get('Input.RequestId');return route.abort();});
   await app.locator('#cm-submit').click();await app.getByRole('button',{name:'Check again',exact:true}).waitFor();assert.notEqual(fresh,checkingKey);
   await app.keyboard.press('Escape');const unknownLeave=app.getByRole('alertdialog');await unknownLeave.waitFor();assert.match(await unknownLeave.textContent(),/may already have been created/);assert.doesNotMatch(await unknownLeave.textContent(),/haven.t been saved/);
   assert.equal(await unknownLeave.getByRole('button',{name:'Check again',exact:true}).evaluate(e=>e===document.activeElement),true);
   await unknownLeave.getByRole('button',{name:'Check again',exact:true}).click();await app.locator('#cm-not-found').waitFor();assert.equal(await app.locator('#cm-name').inputValue(),'Unknown retained draft');
   await app.locator('#cm-submit').click();await app.getByRole('button',{name:'Check again',exact:true}).waitFor();await app.locator('#cm-cancel').click();await app.getByRole('button',{name:'Leave anyway',exact:true}).click();await app.locator('#cm-name').waitFor({state:'detached'});await app.unroute('**/Admin/Events/Create');await app.unroute('**/Admin/Events/Create?handler=CheckAgain*');
   // Playwright cannot intercept a redirect's next hop (established U1 evidence).
   // Use the helper's equivalent navigation-header classification here; HTTP tests
   // prove actual lost-session302 and the shared helper tests prove followed responses.
   await open();await app.locator('#cm-name').fill('Session retained name');
   await app.route('**/Admin/Events/Create',route=>route.fulfill({status:204,headers:{'X-Bingo-Post-Navigation':'/Account/Login?accessChanged=true'}}));
   await app.locator('#cm-submit').click();await app.waitForFunction(()=>document.querySelector('[role=alertdialog]')||!document.querySelector('#cm-uncertain').hidden);
   assert.equal(await app.getByRole('alertdialog').count(),1,JSON.stringify({responses,body:await app.locator('[data-modal-host]').textContent()}));assert.match(await app.getByRole('alertdialog').textContent(),/Session retained name/);assert.match(await app.getByRole('alertdialog').textContent(),/Europe\/Copenhagen/);
   await app.getByRole('button',{name:'Keep editing',exact:true}).click();assert.equal(await app.locator('#cm-name').inputValue(),'Session retained name');await app.unroute('**/Admin/Events/Create');
   await app.route('**/Admin/Events/Create?handler=CheckAgain*',route=>route.fulfill({status:204,headers:{'X-Bingo-Post-Navigation':'/Account/Login?accessChanged=true'}}));
   await app.locator('#cm-submit').click();await app.getByRole('alertdialog').waitFor();assert.match(await app.getByRole('alertdialog').textContent(),/could not check/);assert.match(await app.getByRole('alertdialog').textContent(),/Session retained name/);
   await app.getByRole('button',{name:'Keep editing',exact:true}).click();await app.goBack();await app.getByRole('button',{name:'Leave anyway',exact:true}).click();await app.locator('#cm-name').waitFor({state:'detached'});await app.unroute('**/Admin/Events/Create?handler=CheckAgain*');
   await app.locator('[name=culture][value=da]').click();await app.waitForFunction(()=>document.documentElement.lang==='da');await open();assert.equal(await app.locator('label[for=cm-name]').textContent(),'Eventnavn');await app.locator('#cm-submit').click();assert.equal((await app.locator('#cm-name-err').textContent()).trim(),'Indtast et eventnavn.');await app.locator('#cm-name').fill('Dansk usikkert');
   await app.route('**/Admin/Events/Create',r=>r.abort());await app.locator('#cm-submit').click();await app.waitForFunction(()=>!document.querySelector('#cm-uncertain').hidden);
   await app.route('**/Admin/Events/Create?handler=CheckAgain*',r=>r.fulfill({status:404,body:''}));await app.locator('#cm-submit').click();await app.locator('#cm-not-found').waitFor();
   assert.equal(await app.locator('#cm-not-found [data-component-text]').textContent(),'Vi kunne ikke finde det. Det er måske ikke blevet oprettet, eller det er blevet fjernet siden. Du kan oprette det igen med disse oplysninger.');
   await app.locator('#cm-cancel').click();await app.getByRole('button',{name:'Kassér',exact:true}).click();await app.locator('#cm-name').waitFor({state:'detached'});await app.unroute('**/Admin/Events/Create');await app.unroute('**/Admin/Events/Create?handler=CheckAgain*');
   await app.locator('[name=culture][value=en]').click();await app.waitForFunction(()=>document.documentElement.lang==='en');
   // Real atomic create, Overview replacement, Back consumes one highlight and cannot resubmit.
   for(const lost of[false,true]){
    await app.goto(fixture.origin+'/Admin/Events?view=current');await app.locator('[data-events-directory]').waitFor();await app.emulateMedia({reducedMotion:'no-preference'});await open();const prior=await app.evaluate(()=>history.length);const name='Created '+engine+' '+(lost?'readback ':'direct ')+Date.now();await app.locator('#cm-name').fill(name);
    if(lost)await app.route('**/Admin/Events/Create',async route=>{const committed=await route.fetch();assert.equal(committed.ok(),true);await route.abort();});
    await app.locator('#cm-submit').click();if(lost){await app.getByRole('button',{name:'Check again',exact:true}).waitFor();await app.unroute('**/Admin/Events/Create');await app.locator('#cm-submit').click();}
    await app.waitForURL(/\/Admin\/Events\/Manage\//);assert.equal(await app.evaluate(()=>history.length),prior);
    await app.goBack();await app.locator('[data-events-directory]').waitFor();await app.getByRole('link',{name,exact:true}).waitFor();assert.equal(await app.locator('.row.is-flash').count(),1);assert.equal(await app.evaluate(()=>sessionStorage.getItem('admin-event-created')),null);
    await app.reload();await app.locator('[data-events-directory]').waitFor();assert.equal(await app.locator('.row.is-flash').count(),0);assert.equal(await app.getByRole('link',{name,exact:true}).count(),1);
   }
   assert.deepEqual(errors,[]);results.push({name:engine+'-validation-A12-dirty-history-readback-session-success',passed:true});await browser.close();browser=null;
  }
 }finally{await browser?.close();await fixture.close();fs.writeFileSync(path.join(output,'results.json'),JSON.stringify(results,null,2));}
 const failed=results.filter(r=>!r.passed);console.log('U2 Create: '+(results.length-failed.length)+' passed, '+failed.length+' failed');process.exitCode=failed.length?1:0;
})().catch(e=>{console.error(e);process.exitCode=1;});

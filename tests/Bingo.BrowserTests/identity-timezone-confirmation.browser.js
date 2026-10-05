// U1/A10/A14/I-1: real shared shell + fetch + init/dispose module, controlled HTTP.
const assert=require('node:assert/strict'),http=require('node:http');
const {chromium}=require('playwright'),fixture=require('./fixtures/identity.cjs');
(async()=>{
 let handler,posts=0,fail=true,noPreview=false,hold=null,release=null,saved=false; const bodies=[];
 const server=http.createServer((req,res)=>handler(req,res)); await new Promise(resolve=>server.listen(0,'127.0.0.1',resolve));
 const origin=`http://127.0.0.1:${server.address().port}`,url=`${origin}/Admin/Events/Identity/${fixture.eventId}`;
 const browser=await chromium.launch({headless:true,channel:process.env.PLAYWRIGHT_CHANNEL||'chrome'});
 try{
  handler=async(req,res)=>{
   const path=new URL(req.url,origin); const send=(body,status=200,headers={})=>{res.writeHead(status,{'Content-Type':'text/html',...headers});res.end(body);};
   if(path.pathname.startsWith('/js/'))return send(fixture.script(path.pathname),200,{'Content-Type':'text/javascript'});
   if(req.method==='POST'){
    const chunks=[];for await(const chunk of req)chunks.push(chunk);bodies.push(Buffer.concat(chunks).toString());posts++;
    if(hold)await hold;
    if(fail){fail=false;return send(fixture.page(fixture.editor({url,version:8,name:'Authoritative event',preview:!noPreview,stale:true,reviewed:true,error:'This event changed while you were editing it.'})));}
    saved=true;return send('',303,{Location:new URL(url).pathname});
   }
   if(path.pathname==='/before')return send(fixture.page('',{identity:false,title:'Before identity'}));
   return send(fixture.page(fixture.editor({url,preview:!saved&&path.searchParams.get('mode')!=='ordinary',name:saved?'Saved event':'Preview event',originalName:saved?'Saved event':'Original',timezone:saved?'UTC':'Europe/Copenhagen'})));
  };
  const page=await browser.newPage({reducedMotion:'reduce'}),errors=[];page.on('pageerror',e=>errors.push(e.message));
  const modal=page.getByRole('alertdialog'),confirm=page.locator('[data-identity-review-confirm]'),cancel=page.locator('[data-identity-review-cancel]'),save=page.locator('[data-identity-save]');
  await page.goto(`${origin}/before`);await page.locator(`a[href="/Admin/Events/Identity/${fixture.eventId}"]`).click();await modal.waitFor();assert.equal(await modal.count(),1);assert.equal(await page.locator('input[name="TimezoneReason"]').count(),0,'no new reason field');
  assert.equal(await modal.locator('[data-identity-timezone-row]').count(),5);assert.match(await modal.textContent(),/Also saved: Name/);
  assert.equal(await cancel.evaluate(el=>document.activeElement===el),true);await page.keyboard.press('Escape');await modal.waitFor({state:'hidden'});assert.equal(await save.evaluate(el=>document.activeElement===el),true);
  assert.equal(await page.locator('[name="Input.Name"]').inputValue(),'Preview event');
  await page.locator('a[href="/before"]').click();await modal.waitFor();await modal.getByRole('button',{name:'Keep editing',exact:true}).click();await modal.waitFor({state:'hidden'});assert.equal(page.url(),url);
  await save.click();await modal.waitFor();
  hold=new Promise(resolve=>release=resolve);await confirm.click();await page.waitForFunction(()=>document.querySelector('[data-identity-save]').disabled);
  await cancel.click();await page.keyboard.press('Escape');assert.equal(await modal.isVisible(),true,'pending cancel/Escape refused');await confirm.click();assert.equal(posts,1,'pending remains one POST');await page.goBack();await page.waitForURL(url);assert.equal(await modal.isVisible(),true,'pending Back stays on Identity');assert.equal(posts,1);
  release();hold=null;await page.waitForFunction(()=>document.querySelector('[data-identity-current-version]')?.dataset.identityCurrentVersion==='8');await modal.waitFor({state:'hidden'});
  assert.match(await page.locator('.validation-summary').textContent(),/event changed/);assert.equal(await page.locator('[name="Input.Name"]').inputValue(),'Authoritative event');
  await save.click();await modal.waitFor();assert.match(await modal.textContent(),/UTC 13:00/);await confirm.click();await page.waitForFunction(()=>document.querySelector('[name="Input.Name"]')?.value==='Saved event');
  assert.equal(page.url(),url,'I-1 PRG stays on Identity');assert.equal(posts,2);assert.match(bodies[0],/name="Input.Version"[\s\S]*?\r\n\r\n7\r\n/);assert.match(bodies[1],/name="Input.Version"[\s\S]*?\r\n\r\n8\r\n/);assert.match(bodies[1],/name="Input.ConfirmTimezoneChange"[\s\S]*?\r\n\r\ntrue\r\n/);
  // A preview-free error still retains its draft and navigation guard.
  saved=false;fail=true;noPreview=true;await page.goto(url);await modal.waitFor();await confirm.click();await page.waitForFunction(()=>document.querySelector('[name="Input.Name"]')?.value==='Authoritative event');await modal.waitFor({state:'hidden'});
  await page.locator('a[href="/before"]').click();await modal.waitFor();await modal.getByRole('button',{name:'Keep editing',exact:true}).click();assert.equal(page.url(),url);assert.equal(await page.locator('[name="Input.Name"]').inputValue(),'Authoritative event');
  // Back/Forward use the shared in-page history and module lifecycle.
  await page.locator('a[href="/before"]').click();await modal.getByRole('button',{name:'Discard',exact:true}).click();await page.waitForURL(`${origin}/before`);
  await page.locator(`a[href="/Admin/Events/Identity/${fixture.eventId}"]`).click();await modal.waitFor();await page.keyboard.press('Escape');await modal.waitFor({state:'hidden'});
  await page.goBack();await modal.waitFor();await modal.getByRole('button',{name:'Keep editing',exact:true}).click();await modal.waitFor({state:'hidden'});assert.equal(page.url(),url);assert.equal(await page.locator('[name="Input.Name"]').inputValue(),'Preview event');
  await page.goBack();await modal.waitFor();await modal.getByRole('button',{name:'Discard',exact:true}).click();await page.waitForURL(`${origin}/before`);
  // Ordinary dirty form uses the same guard, without requiring a timezone preview.
  await page.evaluate(next=>window.AdminUI.navigate(next),`${url}?mode=ordinary`);await page.locator('[name="Input.Name"]').fill('Dirty ordinary edit');await page.locator('a[href="/before"]').click();await modal.waitFor();await modal.getByRole('button',{name:'Keep editing',exact:true}).click();assert.equal(await page.locator('[name="Input.Name"]').inputValue(),'Dirty ordinary edit');
  await page.goBack();await modal.waitFor();await modal.getByRole('button',{name:'Discard',exact:true}).click();await page.waitForURL(`${origin}/before`);
  assert.equal(posts,3,'navigation and cancellation never retry a save');assert.deepEqual(errors,[]);
  console.log('PASS timezone: single shared surface, five rows, saved-together fields, no reason, pending one-POST/cancel/Escape protection, stale closes and fresh basis, Identity PRG, preview-free error draft, ordinary and Back dirty guards, init/dispose.');
 }finally{await browser.close();await new Promise(resolve=>server.close(resolve));}
})().catch(error=>{console.error(error);process.exitCode=1;});

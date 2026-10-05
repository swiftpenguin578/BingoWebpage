// U1/A10/A14/I-1: real shared shell + fetch + init/dispose module, controlled HTTP.
const assert=require('node:assert/strict'),http=require('node:http');
const {chromium,webkit}=require('playwright'),fixture=require('./fixtures/identity.cjs');
(async()=>{
 let handler,posts=0,fail=true,noPreview=false,hold=null,release=null,saved=false; const bodies=[];
 const server=http.createServer((req,res)=>handler(req,res)); await new Promise(resolve=>server.listen(0,'127.0.0.1',resolve));
 const origin=`http://127.0.0.1:${server.address().port}`,url=`${origin}/Admin/Events/Identity/${fixture.eventId}`;
 const browser=await (process.env.PLAYWRIGHT_BROWSER === 'webkit' ? webkit.launch({headless:true}) : chromium.launch({headless:true,channel:process.env.PLAYWRIGHT_CHANNEL||'chrome'}));
 try{
  handler=async(req,res)=>{
   const path=new URL(req.url,origin); const send=(body,status=200,headers={})=>{res.writeHead(status,{'Content-Type':'text/html',...headers});res.end(body);};
   if(path.pathname.startsWith('/js/')) {
    let script=fixture.script(path.pathname);
    if(process.env.BINGO_HISTORY_TRACE && path.pathname.includes('admin-design-shell')) script=script.replace('async function onPop(event) {', `async function onPop(event) { console.log('HISTORY_INTERNAL', JSON.stringify({kind:'onPop',url:location.href,state:event.state,index,activeUrl,handlingPop,travel:travel?.target,dirty:isDirty(),pending:isPending()}));`).replace('function goTo(target) {', `function goTo(target) { console.log('HISTORY_INTERNAL',JSON.stringify({kind:'goTo',target,index,state:history.state}));`).replace("url = new URL(url, location.href).href;", "console.log('HISTORY_INTERNAL',JSON.stringify({kind:'navigate',url,mode,index,handlingPop,travel:travel?.target})); url = new URL(url, location.href).href;");
    return send(script,200,{'Content-Type':'text/javascript'});
   }
   if(req.method==='POST'){
    const chunks=[];for await(const chunk of req)chunks.push(chunk);bodies.push(Buffer.concat(chunks).toString());posts++;
    if(hold)await hold;
    if(fail){fail=false;return send(fixture.page(fixture.editor({url,version:8,name:'Authoritative event',preview:!noPreview,stale:true,reviewed:true,error:'This event changed while you were editing it.'})));}
    saved=true;return send('',303,{Location:new URL(url).pathname});
   }
   if(path.pathname==='/before')return send(fixture.page('',{identity:false,title:'Before identity'}));
   return send(fixture.page(fixture.editor({url,preview:!saved&&path.searchParams.get('mode')!=='ordinary',name:saved?'Saved event':'Preview event',originalName:saved?'Saved event':'Original',timezone:saved?'UTC':'Europe/Copenhagen'})));
  };
  const page=await browser.newPage({reducedMotion:'reduce'}),errors=[];if(process.env.BINGO_HISTORY_TRACE)page.on('console',message=>{if(message.text().startsWith('HISTORY_INTERNAL'))console.log(message.text());});page.on('pageerror',e=>errors.push(e.message));
  // Ruling58-3 diagnosis: keep Back, dialog assertion and 30s timeout unchanged.
  if (process.env.BINGO_HISTORY_TRACE) {
    await page.addInitScript(() => {
      const capture = kind => { window.__historyTrace ||= []; window.__historyTrace.push({kind,url:location.href,length:history.length,state:history.state,layers:[...document.querySelectorAll('.m-wrap [role=alertdialog]')].map(el=>({title:el.querySelector('h2')?.textContent,connected:el.isConnected,hidden:el.hidden,display:getComputedStyle(el).display,classes:el.className}))}); };
      window.addEventListener('popstate',()=>{capture('popstate');setTimeout(()=>capture('after-popstate'),0);});
      window.addEventListener('DOMContentLoaded',()=>new MutationObserver(()=>capture('layer-mutation')).observe(document.querySelector('[data-modal-host]')||document.body,{childList:true,subtree:true,attributes:true,attributeFilter:['class','hidden','inert']}));
      window.__captureHistory=capture;
    });
  }
  const modal=page.getByRole('alertdialog'),confirm=page.locator('[data-identity-review-confirm]'),cancel=page.locator('[data-identity-review-cancel]'),save=page.locator('[data-identity-save]');
  await page.goto(`${origin}/before`);await page.locator(`a[href="/Admin/Events/Identity/${fixture.eventId}"]`).click();await modal.waitFor();assert.equal(await modal.count(),1);assert.equal(await page.locator('input[name="TimezoneReason"]').count(),0,'no new reason field');
  assert.equal(await modal.locator('[data-identity-timezone-row]').count(),5);assert.equal(await modal.textContent(),'Show this event’s times in Europe/Copenhagen?\nOriginal has been public, so players may already have seen its times. Every scheduled moment stays the same; only how it’s shown changes. Your other changes (event name) are saved at the same time.Signups open UTC 12:00 to Copenhagen 14:00Signups close UTC 12:00 to Copenhagen 14:00Team draft UTC 12:00 to Copenhagen 14:00Event starts UTC 12:00 to Copenhagen 14:00Event ends UTC 12:00 to Copenhagen 14:00\nCancelSave with Europe/Copenhagen\n');
  assert.equal(await page.locator('[name="Input.HasReviewedValues"]').inputValue(),'true');assert.equal(await page.locator('[name="Input.ReviewedDescription"]').inputValue(),'');assert.equal(await page.locator('[name="Input.Description"]').inputValue(),'');assert.equal(await modal.locator('[data-saved-together]').textContent(),' Your other changes (event name) are saved at the same time.');
  assert.equal(await cancel.evaluate(el=>document.activeElement===el),true);await page.keyboard.press('Escape');await modal.waitFor({state:'hidden'});assert.equal(await save.evaluate(el=>document.activeElement===el),true);
  assert.equal(await page.locator('[name="Input.Name"]').inputValue(),'Preview event');
  await page.locator('a[href="/before"]').click();await modal.waitFor();await modal.getByRole('button',{name:'Keep editing',exact:true}).click();await modal.waitFor({state:'hidden'});assert.equal(page.url(),url);
  await save.click();await modal.waitFor();
  hold=new Promise(resolve=>release=resolve);await confirm.click();await page.waitForFunction(()=>document.querySelector('[data-identity-save]').getAttribute('aria-busy')==='true');
  assert.equal(await save.textContent(),'Saving…');assert.equal(await save.locator('.spin').getAttribute('hidden'),null);assert.equal(await page.locator('[data-identity-dirty]').isVisible(),false);assert.equal(await save.getAttribute('aria-disabled'),'false');assert.equal(await confirm.textContent(),'Saving…');assert.equal(await cancel.isDisabled(),true);assert.equal(await confirm.isDisabled(),true);await cancel.dispatchEvent('click');await page.keyboard.press('Escape');assert.equal(await modal.isVisible(),true,'pending cancel/Escape refused');await confirm.dispatchEvent('click');assert.equal(posts,1,'pending remains one POST');await page.goBack();await page.waitForURL(url);assert.equal(await modal.isVisible(),true,'pending Back stays on Identity');assert.equal(posts,1);
  release();hold=null;await page.waitForFunction(()=>document.querySelector('[data-identity-current-version]')?.dataset.identityCurrentVersion==='8');await modal.waitFor({state:'hidden'});
  assert.match(await page.locator('.validation-summary').textContent(),/event changed/);assert.equal(await page.locator('[name="Input.Name"]').inputValue(),'Authoritative event');
  await save.click();await modal.waitFor();assert.match(await modal.textContent(),/UTC 13:00/);await confirm.click();await page.waitForFunction(()=>document.querySelector('[name="Input.Name"]')?.value==='Saved event');
  assert.equal(page.url(),url,'I-1 PRG stays on Identity');assert.equal(posts,2);assert.match(bodies[0],/name="Input.Version"[\s\S]*?\r\n\r\n7\r\n/);assert.match(bodies[1],/name="Input.Version"[\s\S]*?\r\n\r\n8\r\n/);assert.match(bodies[1],/name="Input.ConfirmTimezoneChange"[\s\S]*?\r\n\r\ntrue\r\n/);
  // A preview-free error still retains its draft and navigation guard.
  saved=false;fail=true;noPreview=true;await page.goto(url);await modal.waitFor();await confirm.click();await page.waitForFunction(()=>document.querySelector('[name="Input.Name"]')?.value==='Authoritative event');await modal.waitFor({state:'hidden'});
  assert.equal(await page.locator('.validation-summary').textContent(),'This event changed while you were editing it.');
  await page.locator('a[href="/before"]').click();await modal.waitFor();await modal.getByRole('button',{name:'Keep editing',exact:true}).click();assert.equal(page.url(),url);assert.equal(await page.locator('[name="Input.Name"]').inputValue(),'Authoritative event');
  // Back/Forward use the shared in-page history and module lifecycle.
  await page.locator('a[href="/before"]').click();await modal.getByRole('button',{name:'Discard',exact:true}).click();await page.waitForURL(`${origin}/before`);
  await page.locator(`a[href="/Admin/Events/Identity/${fixture.eventId}"]`).click();await modal.waitFor();await page.keyboard.press('Escape');await modal.waitFor({state:'hidden'});
  await page.goBack();await modal.waitFor();await modal.getByRole('button',{name:'Keep editing',exact:true}).click();await modal.waitFor({state:'hidden'});assert.equal(page.url(),url);assert.equal(await page.locator('[name="Input.Name"]').inputValue(),'Preview event');
  await page.goBack();await modal.waitFor();await modal.getByRole('button',{name:'Discard',exact:true}).click();await page.waitForURL(`${origin}/before`);
  // Ordinary dirty form uses the same guard, without requiring a timezone preview.
  await page.evaluate(next=>window.AdminUI.navigate(next),`${url}?mode=ordinary`);await page.locator('[name="Input.Name"]').fill('Dirty ordinary edit');await page.locator('a[href="/before"]').click();await modal.waitFor();await modal.getByRole('button',{name:'Keep editing',exact:true}).click();assert.equal(await page.locator('[name="Input.Name"]').inputValue(),'Dirty ordinary edit');
  if(process.env.BINGO_HISTORY_TRACE)await page.evaluate(()=>window.__captureHistory('before-final-back'));
  await page.goBack();
  try { await modal.waitFor(); } catch(error) { if(process.env.BINGO_HISTORY_TRACE)console.error(JSON.stringify(await page.evaluate(()=>{window.__captureHistory('timeout');return window.__historyTrace;}),null,2)); throw error; }
  await modal.getByRole('button',{name:'Discard',exact:true}).click();await page.waitForURL(`${origin}/before`);
  assert.equal(posts,3,'navigation and cancellation never retry a save');assert.deepEqual(errors,[]);
  console.log('PASS timezone: single shared surface, five rows, saved-together fields, no reason, pending one-POST/cancel/Escape protection, stale closes and fresh basis, Identity PRG, preview-free error draft, ordinary and Back dirty guards, init/dispose.');
 }finally{await browser.close();await new Promise(resolve=>server.close(resolve));}
})().catch(error=>{console.error(error);process.exitCode=1;});

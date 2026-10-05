const assert=require('node:assert/strict');
const {chromium}=require('playwright'),fixture=require('./fixtures/identity.cjs');
(async()=>{
 const browser=await chromium.launch({headless:true,channel:process.env.PLAYWRIGHT_CHANNEL||'chrome'});
 try{
  const page=await browser.newPage({reducedMotion:'reduce'}),errors=[];page.on('pageerror',e=>errors.push(e.message));
  const first=fixture.eventId,second='22222222-2222-2222-2222-222222222222',base='https://bingo.test/Admin/Events/Identity/';let mode='ordinary';
  await page.route('https://bingo.test/**',route=>{
   const url=new URL(route.request().url());if(url.pathname.startsWith('/js/'))return route.fulfill({contentType:'text/javascript',body:fixture.script(url.pathname)});
   const id=url.pathname.split('/').at(-1),name=id===second?'Second event':mode==='legacy'?'L'.repeat(60):'First event';
   let root=fixture.editor({url:url.href,preview:false,name,originalName:name,timezone:'UTC',originalTimezone:'UTC',reviewed:mode==='conflict',conflicts:mode==='conflict'?'Name':''});
   if(mode==='conflict')root=root.replace('value="First event"','value="Mine"');
   root=root.replaceAll(`data-identity-event-id="${first}"`,`data-identity-event-id="${id}"`);
   return route.fulfill({contentType:'text/html',body:fixture.page(root)});
  });
  const name=page.locator('[name="Input.Name"]'),description=page.locator('[name="Input.Description"]'),save=page.locator('[data-identity-save]');
  await page.goto(base+first);await page.waitForFunction(()=>document.querySelector('[name="Input.Name"]').hasAttribute('aria-invalid'));
  await name.fill('😀'.repeat(50));assert.equal(await name.evaluate(el=>el.checkValidity()),true);assert.equal(await page.locator('[data-count-for="Name"]').textContent(),'50 / 50');
  await name.fill('😀'.repeat(51));assert.equal(await name.evaluate(el=>el.checkValidity()),false);assert.equal(await name.getAttribute('aria-invalid'),'true');
  await description.fill('😀'.repeat(2000));assert.equal(await description.evaluate(el=>el.checkValidity()),true);assert.equal(await page.locator('[data-count-for="Description"]').textContent(),'4000 / 4000');await description.fill('😀'.repeat(2001));assert.equal(await description.evaluate(el=>el.checkValidity()),false);assert.equal(await page.locator('[data-client-validation]').textContent(),'2 fields need attention');await name.fill('');assert.equal(await name.evaluate(el=>el.validationMessage),'Enter a name');
  const errorHtml=fixture.page(fixture.editor({url:base+first,preview:false,name:'Server rejected draft',timezone:'UTC',originalTimezone:'UTC',reviewed:true,error:'Review the field'}).replace('id="f-Name"','id="f-Name" class="input-validation-error"'));
  await name.fill('Valid draft');await description.fill('Valid description');
  await page.evaluate(html=>{window.AdminFetch.request=async()=>({kind:'handler',data:html,response:{redirected:false}});},errorHtml);await save.click();await page.waitForFunction(()=>document.activeElement?.name==='Input.Name');assert.equal(await name.inputValue(),'Server rejected draft');assert.equal(await name.getAttribute('aria-invalid'),'true');
  mode='legacy';await page.goto(base+first);await page.waitForFunction(()=>document.querySelector('[name="Input.Name"]').hasAttribute('aria-invalid'));assert.equal(await name.evaluate(el=>el.checkValidity()),true,'unchanged legacy name is allowed');await name.fill('L'.repeat(59)+'X');assert.equal(await name.evaluate(el=>el.checkValidity()),false);
  mode='conflict';await page.goto(base+first);await page.waitForFunction(()=>document.querySelector('[name="Input.NameResolution"]').value==='KeepMine');assert.equal(await name.inputValue(),'Mine');assert.equal(await description.inputValue(),'Their description','untouched fields take current server values');await page.locator('[data-use-theirs="Name"]').click();assert.equal(await name.inputValue(),'Their name');assert.equal(await page.locator('[name="Input.NameResolution"]').inputValue(),'UseCurrent');
  // Page consumes classified refusal/session loss without navigating or claiming success.
  mode='ordinary';await page.goto(base+first);await name.fill('Retained draft');await page.evaluate(()=>{window.calls=[];window.AdminFetch.request=async(url,options)=>{window.calls.push({url,method:options.method});return{kind:'refused'};};});await save.click();await page.waitForFunction(()=>document.querySelector('[data-identity-feedback]').textContent.includes('Read-only'));assert.equal(await name.inputValue(),'Retained draft');assert.equal(page.url(),base+first);assert.deepEqual(await page.evaluate(()=>window.calls.map(x=>x.method)),['POST']);
  await page.evaluate(()=>{window.AdminFetch.request=async()=>({kind:'session-lost',destination:'/Account/Login'});});await save.click();await page.getByRole('alertdialog').waitFor();assert.match(await page.getByRole('alertdialog').textContent(),/Retained draft/);assert.equal(await name.inputValue(),'Retained draft');await page.getByRole('button',{name:'Keep editing',exact:true}).click();
  // Repeated swaps + Back/Forward use current event data and dispose old submit listeners.
  await page.goto(base+first);await page.evaluate(()=>{window.oldForms=[];window.calls=[];window.AdminFetch.request=async(url,options)=>{window.calls.push({url,method:options.method});return{kind:'refused'};};});
  for(const id of [second,first,second]){await page.evaluate(async url=>{window.oldForms.push(document.querySelector('[data-identity-editor] form'));await window.AdminUI.navigate(url);},base+id);assert.equal(await name.inputValue(),id===second?'Second event':'First event');}
  await page.goBack();await page.waitForFunction(()=>document.querySelector('[name="Input.Name"]')?.value==='First event');await page.goForward();await page.waitForFunction(()=>document.querySelector('[name="Input.Name"]')?.value==='Second event');
  await page.evaluate(()=>window.oldForms.forEach(form=>form.dispatchEvent(new Event('submit',{bubbles:true,cancelable:true}))));assert.equal(await page.evaluate(()=>window.calls.length),0,'disposed forms cannot submit');
  await name.fill('Second event edited');await save.click();await page.waitForFunction(()=>window.calls.length===1);assert.equal((await page.evaluate(()=>window.calls))[0].url,base+second);assert.deepEqual(errors,[]);
  console.log('PASS Identity binding: Unicode name/UTF-16 text counts, unchanged legacy limit, untouched and KeepMine/UseCurrent fields, refusal/session draft preservation, repeated event swaps and Back/Forward, disposed listeners, single current-event POST.');
 }finally{await browser.close();}
})().catch(error=>{console.error(error);process.exitCode=1;});

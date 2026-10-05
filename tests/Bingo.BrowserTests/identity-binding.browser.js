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
  assert.equal(await save.isDisabled(),true);assert.equal(await save.getAttribute('title'),'No changes to save');
  assert.equal(await save.evaluate(el=>el.disabled),false,'inert Save remains focusable');assert.equal(await save.getAttribute('aria-disabled'),'true');assert.equal(await page.locator('[data-identity-state]').textContent(),'');
  await page.evaluate(()=>{window.inertPosts=0;window.AdminFetch.request=async()=>{window.inertPosts++;return{kind:'refused'};};});await save.focus();assert.equal(await save.evaluate(el=>el===document.activeElement),true);await page.keyboard.press('Enter');await save.evaluate(el=>el.click());assert.equal(await page.evaluate(()=>window.inertPosts),0,'keyboard and pointer activation cannot submit unchanged values');
  await name.fill('Temporary edit');assert.equal(await page.locator('[data-identity-state]').textContent(),'Unsaved changes');assert.equal(await save.getAttribute('aria-disabled'),'false');await name.fill('First event');assert.equal(await page.locator('[data-identity-state]').textContent(),'');assert.equal(await save.getAttribute('aria-disabled'),'true');

  await name.fill('😀'.repeat(50));assert.equal(await name.evaluate(el=>el.checkValidity()),true);assert.equal(await page.locator('[data-count-for="Name"]').textContent(),'50 / 50');
  await name.fill('😀'.repeat(51));assert.equal(await name.evaluate(el=>el.checkValidity()),false);assert.equal(await name.getAttribute('aria-invalid'),'true');
  await description.fill('😀'.repeat(2000));assert.equal(await description.evaluate(el=>el.checkValidity()),true);assert.equal(await page.locator('[data-count-for="Description"]').textContent(),'4000 / 4000');await description.fill('😀'.repeat(2001));assert.equal(await description.evaluate(el=>el.checkValidity()),false);assert.equal(await page.locator('[data-client-validation]').textContent(),'2 fields need attention');await name.fill('');assert.equal(await name.evaluate(el=>el.validationMessage),'Enter a name');
  const errorHtml=fixture.page(fixture.editor({url:base+first,preview:false,name:'Server rejected draft',timezone:'UTC',originalTimezone:'UTC',reviewed:true,error:'Review the field'}).replace('id="f-Name"','id="f-Name" class="input-validation-error"'));
  await name.fill('Valid draft');await description.fill('Valid description');
  await page.evaluate(html=>{window.AdminFetch.request=async()=>({kind:'handler',data:html,response:{redirected:false}});},errorHtml);await save.click();await page.waitForFunction(()=>document.activeElement?.name==='Input.Name');assert.equal(await name.inputValue(),'Server rejected draft');assert.equal(await name.getAttribute('aria-invalid'),'true');
  mode='legacy';await page.goto(base+first);await page.waitForFunction(()=>document.querySelector('[name="Input.Name"]').hasAttribute('aria-invalid'));assert.equal(await name.evaluate(el=>el.checkValidity()),true,'unchanged legacy name is allowed');await name.fill('L'.repeat(59)+'X');assert.equal(await name.evaluate(el=>el.checkValidity()),false);
  mode='conflict';await page.goto(base+first);await page.waitForFunction(()=>document.querySelector('[name="Input.NameResolution"]').value==='KeepMine');assert.equal(await name.inputValue(),'Mine');assert.equal(await description.inputValue(),'Their description','untouched fields take current server values');await page.locator('[data-use-theirs="Name"]').click();assert.equal(await name.inputValue(),'Their name');assert.equal(await page.locator('[name="Input.NameResolution"]').inputValue(),'UseCurrent');
  assert.equal(await save.isDisabled(),true,'all values now match current server values');
  await name.fill('Original');assert.equal(await page.locator('[name="Input.NameResolution"]').inputValue(),'KeepMine');
  await name.fill('Edited after theirs');
  const savedHtml=fixture.page(fixture.editor({url:base+first,preview:false,name:'Edited after theirs',originalName:'Edited after theirs',description:'Their description',timezone:'UTC',originalTimezone:'UTC'}).replace('name="Input.OriginalDescription" value=""','name="Input.OriginalDescription" value="Their description"'),{eventName:'Edited after theirs'});
  await page.evaluate(({html,url})=>{window.posted=[];window.beforeSaveDocument=document;window.AdminFetch.request=async(target,options)=>{window.posted.push(Object.fromEntries(options.body));return{kind:'handler',data:html,response:{redirected:true,url}};};},{html:savedHtml,url:base+first});
  await page.locator('[data-theme="dark"]').click();
  await save.click();await page.waitForFunction(()=>document.querySelector('.design-event-crumb').textContent==='Edited after theirs');
  assert.equal(await page.locator('[data-theme="dark"]').getAttribute('aria-pressed'),'true');assert.equal(await page.locator('[data-theme="dark"]').evaluate(el=>el.classList.contains('is-on')),true);assert.equal(await page.locator('[data-theme="light"]').getAttribute('aria-pressed'),'false');
  assert.equal(await name.inputValue(),'Edited after theirs');assert.equal(await page.locator('.ev-name').textContent(),'Edited after theirs');assert.equal(await page.evaluate(()=>document===window.beforeSaveDocument),true);
  assert.equal(await page.evaluate(()=>window.posted.length),1);assert.equal(await page.evaluate(()=>window.posted[0]['Input.NameResolution']),'KeepMine');assert.equal(await page.evaluate(()=>window.posted[0]['Input.Name']),'Edited after theirs');assert.equal(await save.isDisabled(),true);
  // A successful saved editor/toast survives an actual malformed shell context.
  await name.fill('Edited after theirs again');
  const badContext=savedHtml.replace('<header data-shell-topbar>','<header>').replace('<div data-toast-host></div>','<div data-toast-host><div data-toast><span class="grow">Saved</span></div></div>');
  const warnings=[];page.on('console',message=>{if(message.type()==='warning')warnings.push(message.text());});
  await page.evaluate(({html,url})=>{window.AdminFetch.request=async()=>({kind:'handler',data:html,response:{redirected:true,url}});},{html:badContext,url:base+first});
  await save.click();await page.locator('[data-toast]').waitFor();assert.equal(await page.locator('[data-toast] .grow').textContent(),'Saved');assert.equal(await name.inputValue(),'Edited after theirs');assert.equal(await name.isEnabled(),true);assert.equal(await page.evaluate(()=>document.querySelector('.main').inert),false);assert.equal(warnings.length,1);assert.match(warnings[0],/shell context could not be refreshed/);
  await name.fill('Still editable');assert.equal(await save.getAttribute('aria-disabled'),'false');assert.equal(await page.locator('[data-identity-state]').textContent(),'Unsaved changes');
  // Returning to the original resets a UseCurrent choice to None.
  await page.goto(base+first);await page.locator('[data-use-theirs="Name"]').click();await name.fill('First event');assert.equal(await page.locator('[name="Input.NameResolution"]').inputValue(),'None');
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

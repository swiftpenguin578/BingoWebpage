const assert = require('node:assert/strict');
const http = require('node:http');
const {chromium,webkit} = require('playwright');
const fixture = require('./fixtures/identity.cjs');
let url,server;
(async()=>{
 const browser=await(process.env.PLAYWRIGHT_BROWSER==='webkit'?webkit.launch({headless:true}):chromium.launch({headless:true,channel:process.env.PLAYWRIGHT_CHANNEL||'chrome'}));
 try {
  const page=await browser.newPage({reducedMotion:'reduce'}),errors=[];page.on('pageerror',e=>errors.push(e.message));page.on('console',message=>{if(message.type()==='warning')console.log(message.text());});
  let culture='en',posts=0,release,requested,legacyResponse=false;
  function html(){return fixture.page(fixture.editor({url,preview:false,name:'First event',originalName:'First event',timezone:'UTC',originalTimezone:'UTC'}))
   .replace('<html lang="en">',`<html lang="${culture}">`)
   .replace('</header>',`<form data-shell-language method="post" action="/language"><input type="hidden" name="returnUrl" value="${new URL(url).pathname}"><button type="submit" name="culture" value="en" class="theme-opt ${culture==='en'?'is-on':''}" aria-pressed="${culture==='en'}">EN</button><button type="submit" name="culture" value="da" class="theme-opt ${culture==='da'?'is-on':''}" aria-pressed="${culture==='da'}">DA</button></form></header>`)
   .replace('<main data-page-region>', '<main data-page-region style="height:80px;overflow:auto">')
   .replace('>Identity</h1>',`>${culture==='da'?'Identitet':'Identity'}</h1>`);}
  server=http.createServer(async(request,response)=>{
   const path=new URL(request.url,url).pathname;
   if(path.startsWith('/js/')){response.setHeader('Content-Type','text/javascript');response.end(fixture.script(path));return;}
   if(path==='/language'){
    posts++;const chunks=[];for await(const chunk of request)chunks.push(chunk);requested=Buffer.concat(chunks).toString();
    await new Promise(resolve=>{release=resolve;});culture=requested.match(/name="culture"\r\n\r\n([^\r]+)/)[1];
    response.writeHead(302,{Location:url});response.end();return;
   }
   response.setHeader('Content-Type','text/html');response.end(legacyResponse?'<h1>Legacy response</h1>':html());
  });
  await new Promise(resolve=>server.listen(0,'127.0.0.1',resolve));
  url=`http://127.0.0.1:${server.address().port}/Admin/Events/Identity/${fixture.eventId}`;
  await page.goto(url);await page.waitForFunction(()=>window.AdminUI);
  const da=page.locator('[data-shell-language] [value="da"]'),en=page.locator('[data-shell-language] [value="en"]');
  await page.evaluate(()=>{window.marker={};window.side=document.querySelector('[data-shell-sidebar]');window.oldEditor=document.querySelector('[data-identity-editor]');document.querySelector('[data-page-region]').scrollTop=25;});
  await da.click();while(!release)await new Promise(resolve=>setTimeout(resolve,10));
  assert.equal(posts,1);assert.equal(await da.getAttribute('aria-pressed'),'true');assert.equal(await page.locator('[data-page-skeleton]').count(),0);
  assert.equal(await page.evaluate(()=>window.oldEditor.isConnected),true);assert.equal(await page.evaluate(()=>AdminUI.guard()),false,'language request protects pending transition');
  release();release=null;await page.waitForFunction(()=>document.documentElement.lang==='da'&&document.querySelector('.h1').textContent==='Identitet');
  assert.equal(await page.evaluate(()=>!!window.marker&&window.side===document.querySelector('[data-shell-sidebar]')),true);
  assert.equal(await page.locator('[data-page-region]').evaluate(el=>el.scrollTop),25);assert.equal(await da.evaluate(el=>el===document.activeElement),true);
  assert.equal(await page.locator('[data-language-transition]').count(),0,'reduced motion has no cross-fade');
  await page.locator('[name="Input.Name"]').fill('Retained draft');await en.click();await page.getByRole('alertdialog').waitFor();
  await page.getByRole('button',{name:'Keep editing',exact:true}).click();await page.getByRole('alertdialog').waitFor({state:'hidden'});
  assert.equal(posts,1);assert.equal(await da.getAttribute('aria-pressed'),'true');assert.equal(await page.locator('[name="Input.Name"]').inputValue(),'Retained draft');
  await en.click();await page.getByRole('button',{name:'Discard',exact:true}).click();while(!release)await new Promise(resolve=>setTimeout(resolve,10));
  assert.equal(posts,2);release();release=null;await page.waitForFunction(()=>document.documentElement.lang==='en');
  assert.equal(await page.locator('[name="Input.Name"]').inputValue(),'First event');assert.equal(await page.evaluate(()=>!!window.marker),true);
  await page.emulateMedia({reducedMotion:'no-preference'});
  await da.click();while(!release)await new Promise(resolve=>setTimeout(resolve,10));release();release=null;
  await page.waitForFunction(()=>document.documentElement.lang==='da');assert.equal(posts,3);
  assert.equal(await page.locator('[data-language-transition]').count(),3,'normal motion marks translated sidebar/topbar/content');
  await page.evaluate(()=>{document.querySelectorAll('[data-language-transition]').forEach(el=>el.dispatchEvent(new Event('animationend')));window.addEventListener('beforeunload',()=>sessionStorage.setItem('languageFallback',JSON.stringify({language:document.documentElement.lang,selected:document.querySelector('[data-shell-language] .is-on').value,content:!!document.querySelector('[data-identity-editor]'),overlay:!!document.querySelector('[data-page-skeleton]')})));});
  assert.equal(await page.locator('[data-language-transition]').count(),0);
  legacyResponse=true;await en.click();while(!release)await new Promise(resolve=>setTimeout(resolve,10));release();release=null;
  await page.getByRole('heading',{name:'Legacy response'}).waitFor();assert.equal(posts,4);
  assert.deepEqual(await page.evaluate(()=>JSON.parse(sessionStorage.getItem('languageFallback'))),{language:'da',selected:'da',content:true,overlay:false});
  assert.deepEqual(errors,[]);
  console.log('PASS language background POST/swap, immediate highlight, no skeleton/reload, retained sidebar/scroll/focus, pending guard and dirty Keep editing/Discard');
 }finally{await browser.close();if(server)await new Promise(resolve=>server.close(resolve));}
})().catch(error=>{console.error(error);process.exitCode=1;});

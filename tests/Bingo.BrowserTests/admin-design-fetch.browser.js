const assert = require('node:assert/strict');
const fs = require('node:fs');
const { chromium } = require('playwright');
(async()=>{
  const browser=await chromium.launch({headless:true,channel:process.env.PLAYWRIGHT_CHANNEL||'chrome'});
  try {
    const page=await browser.newPage();
    const errors=[];page.on('pageerror',error=>errors.push(error.message));
    let posts=0,lastHeaders;
    await page.context().addCookies([{name:'FixtureSession',value:'controlled',url:'https://bingo.test'}]);
    await page.route('https://bingo.test/**',async route=>{
      const request=route.request(),url=new URL(request.url());
      if(request.method()==='POST'){posts++;lastHeaders=request.headers();}
      if(url.pathname==='/fixture') return route.fulfill({contentType:'text/html',body:fs.readFileSync('tests/Bingo.BrowserTests/fixtures/admin-design-templates.html','utf8')+'<!doctype html><body data-admin-design data-navigation-enabled="true" data-session-title="Your changes were not saved" data-session-message="Your session is no longer available. What you entered is still here. Sign in before trying again." data-sign-in="Sign in" data-empty-value="Empty" data-keep-editing="Keep editing"><div class="main"><main data-page-region><h1 class="h1" tabindex="-1">Identity</h1><input id="draft" value="Typed draft"></main></div><div data-modal-host></div><div data-drawer-host></div><div data-toast-host></div><form hidden data-shell-antiforgery><input name="__RequestVerificationToken" value="controlled-token"></form></body>'});
      if(url.pathname==='/json') return route.fulfill({contentType:'application/json',body:'{"version":7}'});
      if(url.pathname.startsWith('/error/')) return route.fulfill({status:Number(url.pathname.split('/').at(-1)),contentType:'text/html',body:'<h1>Unavailable</h1>'});
      if(url.pathname==='/bad-json') return route.fulfill({contentType:'application/json',body:'invalid'});
      if(url.pathname==='/network') return route.abort('connectionfailed');
      if(url.pathname==='/navigation') return route.fulfill({status:204,headers:{'X-Bingo-Post-Navigation':url.searchParams.get('to')}});
      return route.fulfill({contentType:'text/html',body:'<h1>Handler or account page</h1>'});
    });
    await page.goto('https://bingo.test/fixture');
    await page.addScriptTag({path:'src/Bingo.Web/wwwroot/js/admin-design-shell.js'});
    await page.addScriptTag({path:'src/Bingo.Web/wwwroot/js/admin-design-fetch.js'});
    // Playwright routing does not intercept a redirect's subsequent network hop.
    // Model the browser's followed Response here; actual 302/no-data boundaries
    // are exercised by the PostgreSQL HTTP tests. Other requests use real fetch.
    const request=(url,options={})=>page.evaluate(async({url,options})=>{
      let result;
      if(url.startsWith('/redirect?')) {
        const destination=new URL(new URL(url,location.href).searchParams.get('to'),location.href).href;
        const response=new Response('<h1>Followed page</h1>',{headers:{'Content-Type':'text/html'}});
        Object.defineProperties(response,{url:{value:destination},redirected:{value:true}});
        result=await AdminFetch.classify(response,options);
      } else result=await AdminFetch.request(url,{notice:false,...options});
      return {kind:result.kind,data:result.data,destination:result.destination,status:result.status};
    },{url,options});
    let result=await request('/json',{method:'POST',body:'fixture'});
    assert.equal(result.kind,'handler');assert.deepEqual(result.data,{version:7});
    assert.equal(lastHeaders.accept,'application/json');assert.equal(lastHeaders.requestverificationtoken,'controlled-token');assert.equal(lastHeaders['x-requested-with'],'XMLHttpRequest');assert.match(lastHeaders.cookie,/FixtureSession=controlled/);assert.equal(lastHeaders['x-bingo-enhanced-post'],undefined);
    for(const destination of ['/Account/Login?accessChanged=true','/Account/AccessDenied','/Account/ChangePassword']) {
      for(const mode of ['redirect','navigation']) {
        result=await request(`/${mode}?to=${encodeURIComponent(destination)}`,{method:'POST'});
        assert.equal(result.kind,'session-lost',`${mode}: ${destination}`);assert.equal(new URL(result.destination).pathname,destination.split('?')[0]);
      }
    }
    for(const mode of ['redirect','navigation']) {
      result=await request(`/${mode}?to=${encodeURIComponent('/Admin/Events/Manage/fixture')}`,{method:'POST'});
      assert.equal(result.kind,'refused','D16 route refusal cannot read as success');
    }
    assert.equal((await request('/html')).kind,'session-lost','HTML instead of JSON is a session boundary');
    assert.equal((await request('/html',{expect:'html'})).kind,'handler');
    for(const status of [404,429,500]){const error=await request(`/error/${status}`);assert.equal(error.kind,'unknown');assert.equal(error.status,status);}
    assert.equal((await request('/bad-json')).kind,'unknown');
    assert.equal((await request('/network',{method:'POST'})).kind,'unknown');
    assert.equal((await request('/redirect?to=/unexpected',{expect:'html'})).kind,'unknown');
    assert.equal((await request('/redirect?to=/Admin/Events/Identity/fixture',{expect:'html',allowRedirectTo:'/Admin/Events/Identity/fixture'})).kind,'handler','explicit own-page PRG is available for page validation');
    const before=posts;
    await page.evaluate(()=>AdminFetch.request('/navigation?to=%2FAccount%2FLogin%3FaccessChanged%3Dtrue',{method:'POST',draft:{Name:'Typed draft',Description:'<script>untrusted</script>'}}));
    const modal=page.getByRole('alertdialog');await modal.waitFor({state:'visible'});
    assert.equal(await modal.locator('h2').textContent(),'Your changes were not saved');
    assert.equal(await modal.locator('dd').first().textContent(),'Typed draft');
    assert.equal(await modal.locator('script').count(),0,'entered values render as text');
    assert.equal(await page.locator('#draft').inputValue(),'Typed draft');
    assert.equal(await page.locator('.main').evaluate(el=>el.inert),true,'session notice blocks the background');
    const signIn=new URL(await modal.getByRole('link',{name:'Sign in'}).getAttribute('href'));
    assert.equal(signIn.pathname,'/Account/Login');assert.equal(signIn.searchParams.get('accessChanged'),'true');assert.equal(signIn.searchParams.get('ReturnUrl'),'/fixture');
    assert.equal(posts,before+1,'no silent retry');assert.ok(page.url().endsWith('/fixture'),'no automatic session navigation');
    await modal.getByRole('button',{name:'Keep editing'}).click();await modal.waitFor({state:'hidden'});
    assert.equal(await page.locator('#draft').inputValue(),'Typed draft');
    for (const language of ['en','da']) {
      const labels=language==='en'?{Name:'Event name',Description:'Description',BuyInDescription:'Buy-in information',Timezone:'Timezone'}:{Name:'Eventnavn',Description:'Beskrivelse',BuyInDescription:'Oplysninger om indskud',Timezone:'Tidszone'};
      await page.evaluate(language=>{Object.assign(document.body.dataset,language==='en'?{readbackSessionTitle:'You were signed out',readbackSessionMessage:'Could not check whether the change went through. Sign in and use Check again.'}:{readbackSessionTitle:'Du blev logget ud',readbackSessionMessage:'Kunne ikke kontrollere, om ændringen blev gennemført. Log ind, og vælg Kontrollér igen.'});},language);
      for(const readback of [false,true]) {
        await page.evaluate(({labels,readback})=>AdminFetch.request('/navigation?to=%2FAccount%2FLogin',{method:readback?'GET':'POST',readback,labels,draft:{Name:'Typed draft',Description:'Description draft',BuyInDescription:'Payment draft',Timezone:'UTC'}}),{labels,readback});
        await modal.waitFor({state:'visible'});assert.deepEqual(await modal.locator('dt').allTextContents(),Object.values(labels));
        if(readback){assert.equal(await modal.locator('h2').textContent(),language==='en'?'You were signed out':'Du blev logget ud');assert.doesNotMatch(await modal.textContent(),/not saved/);assert.match(await modal.textContent(),language==='en'?/Check again/:/Kontrollér igen/);}
        else assert.equal(await modal.locator('h2').textContent(),'Your changes were not saved');
        assert.equal(await page.locator('#draft').inputValue(),'Typed draft');await modal.getByRole('button',{name:'Keep editing'}).click();await modal.waitFor({state:'hidden'});
      }
    }
    assert.deepEqual(errors,[]);
    console.log('PASS handler/session/refusal/unknown classification, credentials/antiforgery, preserved draft notice and no retry/navigation');
  } finally {await browser.close();}
})().catch(error=>{console.error(error);process.exitCode=1;});

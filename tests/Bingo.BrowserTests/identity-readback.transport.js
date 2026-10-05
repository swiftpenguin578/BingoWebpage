// AU09/A14: native multipart tuple and versioned readback through the new runtime.
const assert=require('node:assert/strict');
const {chromium}=require('playwright'),fixture=require('./fixtures/identity.cjs');
const eventId=fixture.eventId,routeUrl=`https://bingo.test/Admin/Events/Identity/${eventId}`;
(async()=>{
 const browser=await chromium.launch({headless:true,channel:process.env.PLAYWRIGHT_CHANNEL||'chrome'});
 try{
  const page=await browser.newPage({reducedMotion:'reduce'}),errors=[];page.on('pageerror',e=>errors.push(e.message));
  let posts=0,reads=0,readFailure=false,values,version=8,appliedValues;
  await page.route('https://bingo.test/**',async route=>{
   const request=route.request(),url=new URL(request.url());
   if(url.pathname.startsWith('/js/'))return route.fulfill({contentType:'text/javascript',body:fixture.script(url.pathname)});
   if(request.method()==='POST'){
    posts++;const wire=request.postDataBuffer().toString('utf8');
    const submitted=field=>wire.match(new RegExp(`name="Input.${field}"\\r\\n\\r\\n([\\s\\S]*?)\\r\\n--`))[1];
    appliedValues={name:submitted('Name'),description:submitted('Description'),buyInDescription:submitted('BuyInDescription'),timezone:submitted('Timezone')};
    assert.equal(appliedValues.description,'First description line\r\nSecond description line');assert.equal(appliedValues.buyInDescription,'First buy-in line\r\nSecond buy-in line');values={...appliedValues};
    return route.abort('failed');
   }
   if(url.searchParams.get('handler')==='Current'){reads++;if(readFailure)return route.fulfill({status:503,body:'Read unavailable'});return route.fulfill({contentType:'application/json',body:JSON.stringify({eventId,values,version})});}
   if(url.pathname==='/before')return route.fulfill({contentType:'text/html',body:fixture.page('',{identity:false,title:'Before'})});
   return route.fulfill({contentType:'text/html',body:fixture.page(fixture.editor({url:routeUrl,name:'Intended',description:'First description line\nSecond description line',buyInDescription:'First buy-in line\nSecond buy-in line'}))});
  });
  const save=page.locator('[data-identity-save]'),feedback=page.locator('[data-identity-feedback]');
  async function uncertain(){await page.goto(routeUrl);await page.locator('[data-identity-review-confirm]').click();await page.waitForFunction(()=>document.querySelector('[data-identity-save]').textContent==='Check again');assert.equal(await page.locator('[name="Input.Name"]').isDisabled(),true);assert.match(await feedback.textContent(),/Unknown/);}
  await uncertain();assert.equal(posts,1);readFailure=true;await save.click();await page.waitForFunction(()=>!document.querySelector('[data-identity-save]').disabled);assert.match(await feedback.textContent(),/Unknown/);assert.equal(await page.locator('[name="Input.Name"]').inputValue(),'Intended');
  await page.locator('a[href="/before"]').click();const leave=page.getByRole('alertdialog');await leave.waitFor();assert.match(await leave.textContent(),/may already have been saved/);assert.equal(await leave.getByRole('button',{name:'Check again',exact:true}).evaluate(el=>document.activeElement===el),true);await leave.getByRole('button',{name:'Check again',exact:true}).click();await leave.waitFor({state:'hidden'});assert.equal(page.url(),routeUrl);assert.equal(posts,1);
  readFailure=false;await save.click();await page.waitForFunction(()=>document.querySelector('[data-identity-feedback]').textContent.startsWith('Up to date'));assert.equal(posts,1);assert.equal(await page.locator('[name="Input.Name"]').isDisabled(),false);assert.equal(page.url(),routeUrl);
  await uncertain();version=7;await save.click();await page.waitForFunction(()=>document.querySelector('[data-identity-feedback]').textContent.includes("weren't applied"));assert.equal(await save.textContent(),'Save changes');assert.equal(await page.locator('[name="Input.Name"]').inputValue(),'Intended');assert.equal(posts,2);
  await uncertain();version=8;values={...values,buyInDescription:'First buy-in line\r\nGenuinely different second line'};await save.click();await page.waitForFunction(()=>document.querySelector('[data-identity-feedback]').textContent.startsWith('Another admin'));
  assert.equal(await page.locator('[name="Input.BuyInDescription"]').inputValue(),'First buy-in line\nSecond buy-in line','conflicting draft retained');assert.equal(await page.locator('[name="Input.BuyInDescriptionResolution"]').inputValue(),'KeepMine');assert.match(await page.locator('[data-conflict-for="BuyInDescription"]').textContent(),/Genuinely different second line/);
  await page.locator('[data-use-theirs="BuyInDescription"]').click();assert.equal(await page.locator('[name="Input.BuyInDescriptionResolution"]').inputValue(),'UseCurrent');assert.equal(await page.locator('[name="Input.BuyInDescription"]').inputValue(),'First buy-in line\nGenuinely different second line');assert.equal(posts,3,'all readback outcomes are GET-only');
  const result=await page.evaluate(async({eventId,routeUrl})=>{
   const {createIdentityReadbackSession}=await import('/js/event-identity.js');
   const data=new FormData(document.querySelector('form'));
   data.set('Input.HasReviewedValues','true');data.set('Input.Version','7');
   data.set('Input.Name','My original intent');data.set('Input.ReviewedName','Reviewed name');data.set('Input.NameResolution','UseCurrent');
   data.set('Input.Description','Retained first\nRetained second');data.set('Input.OriginalDescription','Retained first\r\nRetained second');data.set('Input.ReviewedDescription','Observed first\nObserved second');
   data.set('Input.BuyInDescription','My changed first\nMy changed second');data.set('Input.BuyInDescriptionResolution','UseCurrent');data.set('Input.ReviewedBuyInDescription','Reviewed first\nReviewed second');data.set('Input.ReviewedTimezone','UTC');
   const session=createIdentityReadbackSession(data,routeUrl,eventId,7);data.set('Input.ReviewedName','Later edit');const expected=session.expected;
   const request=window.AdminFetch.request,requests=[];
   window.AdminFetch.request=async(url,options)=>{requests.push({url,method:options.method||'GET'});return{kind:'handler',data:{eventId,values:{...expected},version:8}};};const matching=await session.checkAgain();
   window.AdminFetch.request=async()=>({kind:'handler',data:{eventId,values:{...expected,description:'Unseen merge after dispatch'},version:8}});const different=await session.checkAgain();
   window.AdminFetch.request=async()=>({kind:'handler',data:{eventId,values:expected,version:7}});const unchanged=await session.checkAgain();
   window.AdminFetch.request=async()=>({kind:'unknown'});const unknown=await session.checkAgain();
   window.AdminFetch.request=async()=>({kind:'refused'});const redirected=await session.checkAgain();
   window.AdminFetch.request=async()=>({kind:'handler',data:{eventId:'another-event',values:expected,version:8}});const wrongEvent=await session.checkAgain();
   window.AdminFetch.request=async()=>({kind:'handler',data:{eventId,values:{name:expected.name},version:8}});const malformed=await session.checkAgain();
   window.AdminFetch.request=async()=>({kind:'handler',data:{eventId,values:expected}});const noVersion=await session.checkAgain();
   window.AdminFetch.request=request;
   return{expected,frozen:Object.isFrozen(expected)&&Object.isFrozen(session)&&Object.isFrozen(session.draft),matching:matching.state,different:different.state,unchanged:unchanged.state,unknown:unknown.state,redirected:redirected.state,wrongEvent:wrongEvent.state,malformed:malformed.state,noVersion:noVersion.state,requests};
  },{eventId,routeUrl});
  assert.equal(result.expected.name,'Reviewed name');assert.equal(result.expected.description,'Observed first\nObserved second','untouched comparison uses submitted CRLF; observed LF stays exact');assert.equal(result.expected.buyInDescription,'Reviewed first\nReviewed second','UseCurrent preserves exact observed newlines');assert.equal(result.frozen,true);
  assert.equal(result.matching,'upToDate');assert.equal(result.different,'different');assert.equal(result.unchanged,'notApplied');for(const state of ['unknown','redirected','wrongEvent','malformed','noVersion'])assert.equal(result[state],'unknown');assert.deepEqual(result.requests.map(x=>x.method),['GET']);assert.match(result.requests[0].url,/handler=Current/);assert.ok(reads>=4);assert.deepEqual(errors,[]);
  console.log('PASS readback: native multipart newline fidelity, exact retained/reviewed tuple, immutable snapshot, all three A14 outcomes, unavailable read/locked draft, uncertainty departure Check again, KeepMine/UseCurrent, no POST retry, no request attribution.');
 }finally{await browser.close();}
})().catch(error=>{console.error(error);process.exitCode=1;});

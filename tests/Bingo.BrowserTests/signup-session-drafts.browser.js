const assert=require('node:assert/strict'),path=require('node:path');
const {chromium,webkit}=require('playwright');
const {startFixture,login}=require('../../scripts/lib/admin-parity-fixture.cjs');
(async()=>{const engine=process.env.PLAYWRIGHT_BROWSER||'chromium',fixture=await startFixture(process.cwd(),path.join(process.cwd(),'artifacts/signup-session-drafts-'+engine));let browser;
try{browser=await(engine==='webkit'?webkit:chromium).launch({headless:true});const context=await browser.newContext(),page=await login(context,fixture),errors=[];page.on('pageerror',e=>errors.push(e.message));const route='/Admin/Events/SignupSetup/'+fixture.events['autumn-bingo-2027'];await page.goto(fixture.origin+route);await page.locator('#add-custom').waitFor({state:'attached'});
const seed=await page.evaluate(async route=>{const html=await(await fetch(route)).text(),doc=new DOMParser().parseFromString(html,'text/html'),root=doc.querySelector('[data-signup-setup]'),state=JSON.parse(root.dataset.current);const account=structuredClone(state.questions.find(q=>q.type==='Account'));
account.id='10000000-0000-0000-0000-000000000001';account.systemField='None';account.label='Extra playing';account.position=10;state.questions.push(account);
for(let i=2;i<=3;i++)state.questions.push({...structuredClone(account),id:'10000000-0000-0000-0000-00000000000'+i,type:'Text',label:'Custom '+i,accountRole:null,position:10+i,required:false});
state.hasFirstResponse=false;state.editable=true;root.dataset.current=JSON.stringify(state);return{html:'<!doctype html>'+doc.documentElement.outerHTML,state,account:account.id,custom:state.questions.at(-2).id,co:state.questions.find(q=>q.systemField==='CoCaptainName').id};},route);
const reason='Server refusal: this fixture event is now read-only.',results=[];let posts=0,reads=0,refused=false,state;
await page.route('**'+route+'**',async r=>{const request=r.request(),url=new URL(request.url());if(request.method()==='POST'){posts++;refused=true;return r.fulfill({status:200,contentType:'text/html',headers:{'X-Bingo-Post-Navigation':'/Account/Login'},body:'<div data-transient-toast><div class="app-toast-copy"><strong>Warning</strong><span>'+reason+'</span></div></div>'});}if(url.searchParams.get('handler')==='Current'){reads++;return r.fulfill({contentType:'application/json',body:JSON.stringify({...state,editable:!refused})});}return r.fulfill({contentType:'text/html',body:seed.html});});
for(const kind of ['capacity','code','add-playing','add-alt','rename','move','co-off','co-on','delete','drawer-add','drawer-edit']){
posts=reads=0;refused=false;state=structuredClone(seed.state);if(kind==='co-on'){state.questions.find(q=>q.id===seed.co).active=false;}
await page.goto(fixture.origin+route+'?refusalCase='+kind);await page.locator('#add-custom').waitFor({state:'attached'});
if(kind==='co-on'){await page.evaluate(id=>{const root=document.querySelector('[data-signup-setup]'),s=JSON.parse(root.dataset.current);s.questions.find(q=>q.id===id).active=false;root.dataset.current=JSON.stringify(s);},seed.co);await page.evaluate(async()=>{const module=await import(document.querySelector('script[data-admin-page-script]').src);await module.init(document.querySelector('[data-page-region]'));});}
if(kind==='capacity'){await page.locator('#cap-input').fill('99');await page.locator('#cap-save').click();}
else if(kind==='code'){await page.locator('#code-on').check();await page.locator('#code-new').fill('Retained refused code');await page.locator('#code-save').click();}
else{await page.locator('#tab-form').click();if(kind.startsWith('add-'))await page.locator('#'+kind).click();
else if(kind==='rename'){await page.locator('#rename-'+seed.account).click();await page.locator('#rename-input-'+seed.account).fill('Retained refused label');await page.locator('#rename-save-'+seed.account).click();}
else if(kind==='move')await page.locator('#move-'+seed.custom+'-false').click();
else if(kind.startsWith('co-')){await page.locator('#toggle-'+seed.co).click();if(kind==='co-off')await page.locator('[data-confirm-accept]').click();}
else if(kind==='delete'){await page.locator('#delete-'+seed.account).click();await page.locator('[data-confirm-accept]').click();}
else{await page.locator(kind==='drawer-add'?'#add-custom':'#edit-'+seed.custom).click();await page.locator('#q-label').fill('Retained refused question');await page.locator('.drawer [data-save]').click();}}
await page.getByRole('heading',{name:'Your changes were not saved'}).waitFor();
const displayed=await page.locator('.modal .kv').evaluate(e=>Object.fromEntries([...e.querySelectorAll('dt')].map(dt=>[dt.textContent,dt.nextElementSibling.textContent])));
const question=state.questions.find(q=>q.id===(kind==='delete'||kind==='rename'?seed.account:kind.startsWith('co-')?seed.co:seed.custom));
const expected=kind==='capacity'?{'Maximum confirmed players':'99'}:kind==='code'?{'Require a signup code':'Required','New code':'Retained refused code'}:kind==='rename'?{Question:'Retained refused label'}:kind.startsWith('add-')?{Question:kind==='add-alt'?'Alt accounts':'Playing accounts',Action:kind==='add-alt'?'Add alt account field':'Add playing account field'}:kind.startsWith('drawer')?{Question:'Retained refused question','Help text':'Empty','Answer type':'Text',Required:'No'}:{Question:question.label,Action:kind==='move'?'Move down':kind==='co-on'?'Turn on':kind==='co-off'?'Turn off':'Delete'};
assert.deepEqual(displayed,expected,kind+' lists only submitted save values');assert.equal(posts,1);assert.equal(reads,0);if(kind==='capacity')assert.equal(await page.locator('#cap-input').inputValue(),'99');if(kind==='code')assert.equal(await page.locator('#code-new').inputValue(),'Retained refused code');if(kind==='rename')assert.equal(await page.locator('#rename-input-'+seed.account).inputValue(),'Retained refused label');if(kind.startsWith('drawer'))assert.equal(await page.locator('#q-label').inputValue(),'Retained refused question');results.push(kind);

}
assert.deepEqual(errors,[]);console.log('PASS '+results.length+' session-loss save paths: '+results.join(', ')+'; exact per-save values, retained entries, no unrelated Settings values.');
}finally{await browser?.close();await fixture.close();}})().catch(e=>{console.error(e);process.exitCode=1});

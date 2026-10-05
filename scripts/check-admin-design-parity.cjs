// Real Razor/Kestrel + owned PostgreSQL against the frozen rendered reference.
// BINGO_PARITY_ROOT can point to a read-only baseline archive checkout; the same
// assertions run there. No baseline exemption or expected-failure inversion.
const fs=require('node:fs'),path=require('node:path'),assert=require('node:assert/strict');
const {chromium,webkit}=require('playwright');
const {startFixture,referencePage,login}=require('./lib/admin-parity-fixture.cjs');
const {comparator,settle}=require('./lib/admin-parity-compare.cjs');
// Brief60 item7: explicit tolerances in CSS px, allowing subpixel rounding only.
// V6's missing8px spacer and V19's extra20px banner padding must both fail.
const geometry={
 'page-head':{box:{x:1,y:1}},
 'card':{box:{x:1,y:1},spacing:{reference:'.page-head',app:'.page-head',from:'bottom',to:'top',tolerancePx:1,label:'head-to-card'}},
 'form-sec-title':{box:{x:1,y:1},spacing:{reference:'.card.form-card',app:'.card.form-card',from:'top',to:'top',tolerancePx:1,label:'V19 card-to-first-heading'}},
 'event-switch':{box:{y:1}},
 'overview item':{box:{y:1},spacing:{reference:'#event-switch',app:'.event-switch',from:'bottom',to:'top',tolerancePx:1,label:'V6 event-to-first-nav'}},
 'form-bar':{box:{x:1,y:1,width:1,height:1}},
 'crumbs':{box:{x:1,y:1,height:1}}
};
const basePairs=require('./lib/admin-parity-pairs.cjs').map(pair=>[...pair,{...geometry[pair[0]],text:/^(nav-label|nav-item text|ev-lbl|ev-name|ev-meta-text|crumb first|crumb sep|crumb mid|crumb cur|h1|summary|form-sec-title|form-sec-sub|lbl|input name|textarea|field-hint|link title|link hint|copy btn$|save$)/.test(pair[0]),icon:/svg|chevron|dot|copy btn$/.test(pair[0]),dimensions:pair[0]==='crumb mid'?['width']:/svg|dot|chevron/.test(pair[0])?['width','height']:[]}]);
const root=path.resolve(process.env.BINGO_PARITY_ROOT||process.cwd()),output=path.resolve(process.env.BINGO_PARITY_OUTPUT||'artifacts/admin-parity');
const state=(page,values)=>page.evaluate(values=>new Promise(resolve=>window.__parityReference.setState(values,resolve)),values);
const P=(name,ref,app,options={})=>[name,ref,app,{text:true,icon:true,required:true,...options}];
const bar=[P('save','#save-btn','[data-identity-save]',{dimensions:['width','height']}),P('dirty','.dirty','[data-identity-dirty]',{required:false}),P('saved','.saved-note','[data-identity-state]',{required:false})];
const modal=[P('modal','.modal','.modal',{text:false}),P('dialog title','.m-title','.m-title'),P('dialog body','.m-body','.m-body'),P('dialog secondary','.m-actions .btn:first-child','.m-actions .btn:first-child'),P('dialog primary','.m-actions .btn:last-child','.m-actions .btn:last-child')];
(async()=>{
 const fixture=await startFixture(root,output),results=[];let browser;
 try{
  for(const engine of (process.env.BINGO_PARITY_ENGINES||'chromium,webkit').split(',')){
   browser=await(engine==='webkit'?webkit.launch({headless:true}):chromium.launch({headless:true,channel:process.env.PLAYWRIGHT_CHANNEL||'chrome'}));
   const compare=comparator(output,results),context=await browser.newContext({viewport:{width:1280,height:900}}),errors=[];let app=await login(context,fixture);
   app.setDefaultTimeout(10000);app.on('dialog',dialog=>dialog.accept());
   app.on('pageerror',error=>errors.push(error.message));
   const identity=slug=>`${fixture.origin}/Admin/Events/Identity/${fixture.events[slug]}`;
   const load=async(slug='autumn-bingo-2027')=>{
    const response=await app.goto(identity(slug));
    try{await app.waitForFunction(()=>window.AdminUI&&window.AdminFetch);await app.locator('[data-identity-editor]').waitFor();await app.evaluate(()=>document.fonts.ready);}
    catch(error){console.error(JSON.stringify({checkpoint:'Identity runtime',finalUrl:app.url(),status:response?.status(),pageErrors:errors}));throw error;}
   };
   const refContext=await browser.newContext({viewport:{width:1280,height:900}}),ref=await referencePage(refContext,fixture,root);
   const resetRef=()=>ref.evaluate(()=>{const c=window.__parityReference;c.world=structuredClone(window.__parityWorld);const f=c.snapshot(c.world['autumn-bingo-2027']);return new Promise(resolve=>c.setState({slug:'autumn-bingo-2027',form:{...f},base:f,status:'idle',saved:false,showErrors:false,stale:null,staleInfo:null,tzm:null,modal:null,menu:null,toasts:[],loading:false,loadError:false,sideOpen:true,mobileNav:false},resolve));});
   const selected=process.env.BINGO_PARITY_SCENARIOS?.split(',');
   const scenario=async(name,fn)=>{if(selected&&!selected.includes(name))return;await app.close();app=await context.newPage();app.setDefaultTimeout(10000);app.on('dialog',dialog=>dialog.accept());app.on('pageerror',error=>errors.push(error.message));try{await fn();}catch(error){await app.screenshot({path:path.join(output,engine+'-'+name+'-failure-app.png'),fullPage:true}).catch(()=>{});await ref.screenshot({path:path.join(output,engine+'-'+name+'-failure-reference.png'),fullPage:true}).catch(()=>{});results.push({name:engine+'-'+name,passed:false,error:error.stack});console.error('FAIL '+engine+'-'+name+' '+error.message);}};
   for(const [name,width,theme] of [['desktop-light',1280,'light'],['desktop-dark',1280,'dark'],['phone-light',390,'light']])await scenario(name,async()=>{
    await load();await app.setViewportSize({width,height:width===390?844:900});await ref.setViewportSize({width,height:width===390?844:900});await resetRef();
    if(width===390){await app.locator('.menu-toggle').click();await app.locator(`[data-theme="${theme}"]`).click();await app.keyboard.press('Escape');}else await app.locator(`[data-theme="${theme}"]`).click();await state(ref,{theme});
    // User 5 October: mobile preferences belong in the hamburger. Compare their
    // unchanged styles separately while open; the bell-only topbar may give MORE
    // breadcrumb room, but never less than the reference's reading width.
    const pairs=width===390?basePairs.filter(p=>!p[0].startsWith('theme-')).map(p=>p[0]==='crumb mid'?[...p.slice(0,3),{...p[3],dimensions:[],minimumDimensions:['width']}]:p):basePairs;
    await compare(engine+'-'+name,app,ref,pairs);assert.equal(await app.title(),await ref.title());assert.deepEqual(await app.locator('#Input_Timezone option').allTextContents(),await ref.locator('#f-tz option:not([hidden])').allTextContents());
    if(width===1280){const icons=page=>page.locator('.nav-item svg').evaluateAll(nodes=>nodes.map(svg=>[...svg.children].map(el=>el.outerHTML.replace(/ data-dc-tpl="[^"]*"/g,''))));assert.deepEqual(await icons(app),await icons(ref));}
   });
   await app.setViewportSize({width:1280,height:900});await ref.setViewportSize({width:1280,height:900});await state(ref,{theme:'light'});
   await scenario('mobile-preferences',async()=>{
    await load();await resetRef();await app.setViewportSize({width:390,height:844});await ref.setViewportSize({width:390,height:844});
    await app.waitForFunction(()=>document.querySelector('[data-mobile-preferences] [data-shell-language]'));
    assert.equal(await app.locator('[data-shell-topbar] [data-theme], [data-shell-topbar] [data-shell-language]').count(),0);
    assert.equal(await app.locator('[data-shell-topbar] [data-menu-target=admin-notifications-menu]').isVisible(),true);
    assert.equal(await app.locator('[data-shell-language]').count(),1);
    await app.locator('#Input_Name').fill('Mobile language draft');await app.evaluate(()=>window.__mobileDocument='retained');
    let posts=0;app.on('request',request=>{if(request.method()==='POST'&&new URL(request.url()).pathname.toLowerCase()==='/language')posts++;});
    await app.locator('.menu-toggle').click();assert.equal(await app.locator('[data-mobile-preferences] [data-shell-language]').isVisible(),true);
    await app.locator('[data-theme=dark]').click();assert.equal(await app.locator('html').evaluate(el=>el.classList.contains('theme-dark')),true);
    await app.locator('[data-theme=light]').click();await state(ref,{theme:'light',mobileNav:true});
    await compare(engine+'-mobile-preferences',app,ref,basePairs.filter(p=>p[0].startsWith('theme-')));
    await app.locator('[name=culture][value=da]').focus();await app.keyboard.press('Enter');await app.getByRole('alertdialog').waitFor();
    await app.getByRole('button',{name:'Keep editing',exact:true}).click();await app.getByRole('alertdialog').waitFor({state:'hidden'});assert.equal(posts,0);assert.equal(await app.locator('#Input_Name').inputValue(),'Mobile language draft');
    assert.equal(await app.locator('#Input_Name').evaluate(el=>el===document.activeElement),true);assert.equal(await app.locator('.main').evaluate(el=>el.inert),false);await app.locator('.menu-toggle').click();
    await app.locator('[name=culture][value=da]').click();await app.getByRole('button',{name:'Discard',exact:true}).click();await app.waitForFunction(()=>document.documentElement.lang==='da');
    assert.equal(posts,1);assert.equal(await app.evaluate(()=>window.__mobileDocument),'retained');assert.equal(await app.locator('[data-mobile-preferences] [name=culture][value=da]').isVisible(),true);
    await app.locator('[name=culture][value=en]').click();await app.waitForFunction(()=>document.documentElement.lang==='en');assert.equal(posts,2);
    await app.keyboard.press('Escape');assert.equal(await app.locator('[data-shell-sidebar]').evaluate(el=>el.classList.contains('is-mobile-open')),false);assert.equal(await app.locator('.main').evaluate(el=>el.inert),false);
    await app.setViewportSize({width:861,height:900});await app.waitForFunction(()=>document.querySelector('[data-shell-topbar] [data-shell-language]'));
    assert.equal(await app.locator('[data-shell-topbar] [data-theme]').count(),2);assert.equal(await app.locator('[data-shell-language]').count(),1);
    results.push({name:engine+'-mobile-language-keyboard-breakpoint',passed:true});await app.setViewportSize({width:1280,height:900});await ref.setViewportSize({width:1280,height:900});
   });
   await scenario('dirty-discard',async()=>{
    await load();await app.locator('[data-theme="light"]').click();await resetRef();
    await app.locator('#Input_Name').fill('Edited event');await ref.locator('#f-name').fill('Edited event');await compare(engine+'-dirty',app,ref,bar);
    await app.locator('.crumb-btn').click();await ref.locator('.crumb-btn').click();await app.getByRole('alertdialog').waitFor();await ref.getByRole('alertdialog').waitFor();await compare(engine+'-discard',app,ref,modal);
    await app.getByRole('button',{name:'Keep editing',exact:true}).click();await ref.getByRole('button',{name:'Keep editing',exact:true}).click();
   });
   await scenario('timezone',async()=>{
    await load();await resetRef();await app.locator('#Input_Name').fill('Edited event');await ref.locator('#f-name').fill('Edited event');
    await app.locator('#Input_Timezone').selectOption('UTC');await ref.locator('#f-tz').selectOption('UTC');
    await compare(engine+'-timezone-note',app,ref,[P('timezone note','#n-tz','[data-timezone-note]')]);
    await app.locator('[data-identity-save]').click();await ref.locator('#save-btn').click();await app.getByRole('alertdialog').waitFor();
    await compare(engine+'-timezone-dialog',app,ref,[...modal,P('compare header','.compare-head','.compare-head'),...Array.from({length:5},(_,i)=>['.compare-label','.compare-old','.compare-new'].map(cell=>P('scheduled row '+i+' '+cell,`.compare-row:nth-child(${i+2}) ${cell}`,`.compare-row:nth-child(${i+2}) ${cell}`))).flat()]);
    await app.getByRole('button',{name:'Cancel',exact:true}).click();await ref.getByRole('button',{name:'Cancel',exact:true}).click();
   });
   await scenario('validation',async()=>{
    await load();await resetRef();await app.locator('#Input_Name').fill('');await ref.locator('#f-name').fill('');await app.locator('#Input_Description').fill('x'.repeat(4001));await ref.locator('#f-desc').fill('x'.repeat(4001));await app.locator('[data-identity-save]').click();await ref.locator('#save-btn').click();
    await compare(engine+'-validation',app,ref,[P('invalid name','#f-name','#Input_Name'),P('name error','#e-name','#error-Name'),P('description error','#e-desc','#error-Description'),P('counter','.field-count.is-over','[data-count-for=Description]'),P('validation banner','.banner.is-error','[data-client-validation]')]);
   });
   await scenario('busy-saved-toast',async()=>{
    await load();await resetRef();await app.locator('#Input_Name').fill('Saved parity event');await ref.locator('#f-name').fill('Saved parity event');
    let release;const gate=new Promise(resolve=>release=resolve);await app.route(identity('autumn-bingo-2027'),async route=>{if(route.request().method()==='POST')await gate;await route.continue();});
    await app.locator('[data-identity-save]').click();await app.locator('[data-identity-save][aria-busy=true]').waitFor();await state(ref,{status:'saving'});await compare(engine+'-busy',app,ref,[...bar,P('spinner','.spin','[data-identity-save] .spin')]);
    release();await app.waitForFunction(()=>document.querySelector('[data-identity-state]')?.textContent.includes('Saved'));await app.unroute(identity('autumn-bingo-2027'));
    await ref.evaluate(()=>{const c=window.__parityReference;c.world[c.state.slug].name='Saved parity event';return new Promise(resolve=>c.setState({base:{...c.state.form},status:'idle',saved:true},()=>{c.toast({text:'Identity saved.'});resolve();}));});
    await compare(engine+'-saved-toast',app,ref,[...bar,P('toast','.toast','.toast'),P('toast icon','.toast .t-ic','.toast .t-ic'),P('dismiss','.toast-x','.toast-x')]);
    assert.equal(await app.locator('[data-identity-save]').evaluate(el=>document.activeElement===el),true);
    await app.locator('#Input_Name').fill('Autumn Bingo 2027');await app.locator('[data-identity-save]').click();await app.waitForFunction(()=>document.querySelector('.ev-name')?.textContent==='Autumn Bingo 2027');
   });
   await scenario('readonly',async()=>{
    for(const [slug,phase] of [['spring-finalized','finalized'],['spring-archived','archived'],['spring-cancelled','cancelled'],['midsummer-skilling-sprint','live']]){
     await load(slug);await resetRef();await ref.evaluate(({slug,phase})=>{const c=window.__parityReference,key=slug.startsWith('spring-')?'spring-bingo-2027':slug;c.world[key].phase=phase;const f=c.snapshot(c.world[key]);return new Promise(resolve=>c.setState({slug:key,form:{...f},base:f},resolve));},{slug,phase});
     await compare(engine+'-'+phase,app,ref,phase==='live'?[P('live lock','.field-lock','.field-lock')]:[P('readonly banner','.page-banner','#identity-readonly'),P('empty value','.ro-value.is-empty','.ro-value.is-empty')]);
    }
   });
   await scenario('collapsed-menus',async()=>{
    await load();await resetRef();await app.locator('.collapse-btn').click();await ref.locator('.collapse-btn').click();
    await compare(engine+'-collapsed',app,ref,[P('collapse arrow','.collapse-btn svg','.collapse-btn svg',{text:false}),P('switcher','.event-switch','.event-switch',{text:false})]);
    assert.equal(await app.locator('.event-switch').getAttribute('title'),'Switch event');assert.equal(await app.locator('.collapse-btn').getAttribute('title'),await ref.locator('.collapse-btn').getAttribute('title'));
    await app.locator('.collapse-btn').click();await ref.locator('.collapse-btn').click();await app.locator('.event-switch').click();await ref.locator('.event-switch').click();
    await compare(engine+'-event-menu',app,ref,[P('menu title','.menu-head b','#admin-event-menu .menu-head b'),P('current check','.menu-check','#admin-event-menu .menu-check')]);
    const checked=app.locator('#admin-event-menu [aria-checked=true]');assert.equal(await checked.getAttribute('role'),'menuitemradio');assert.equal(await checked.evaluate(el=>{const a=el.getBoundingClientRect(),b=el.parentElement.getBoundingClientRect();return a.top>=b.top&&a.bottom<=b.bottom;}),true);
    await app.keyboard.press('Escape');await ref.keyboard.press('Escape');await app.locator('.account-btn').click();await ref.locator('.account-btn').click();
    await compare(engine+'-account-menu',app,ref,[P('account header','.menu-head','#admin-account-menu .menu-head',{text:false}),P('account name','.menu-head b','#admin-account-menu .menu-head b',{text:false})]);
    assert.match(await app.locator('#admin-account-menu .menu-head').textContent(),/parity-admin.*Administrator/s);await app.keyboard.press('Escape');await ref.keyboard.press('Escape');
   });
   await scenario('uncertain-checking',async()=>{
    await load();await resetRef();await app.locator('#Input_Name').fill('Unknown outcome');await ref.locator('#f-name').fill('Unknown outcome');
    const stopPost=route=>route.request().method()==='POST'?route.abort('connectionfailed'):route.continue();await app.route(identity('autumn-bingo-2027'),stopPost);await app.locator('[data-identity-save]').click();await app.getByRole('button',{name:'Check again',exact:true}).waitFor();await app.unroute(identity('autumn-bingo-2027'),stopPost);await state(ref,{status:'uncertain'});
    await compare(engine+'-uncertain',app,ref,[...bar,P('uncertain banner','.banner.is-warning','[data-identity-feedback]')]);
    await app.locator('.crumb-btn').click();await ref.locator('.crumb-btn').click();await app.getByRole('alertdialog').waitFor();await compare(engine+'-uncertain-leave',app,ref,modal);await app.keyboard.press('Escape');await ref.keyboard.press('Escape');
    let release;const gate=new Promise(resolve=>release=resolve);await app.route('**/*?handler=Current',async route=>{await gate;await route.continue();});await app.locator('[data-identity-save]').click();await app.locator('[data-identity-save][aria-busy=true]').waitFor();await state(ref,{status:'checking'});await compare(engine+'-checking',app,ref,[...bar,P('checking banner','.banner.is-warning','[data-identity-feedback]')]);release();
    await app.waitForFunction(()=>document.querySelector('[data-identity-save]')?.textContent==='Save changes');await app.unroute('**/*?handler=Current');await state(ref,{status:'notSaved'});await compare(engine+'-not-applied',app,ref,[P('not-applied banner','.banner.is-info','[data-identity-feedback]')]);
   });
   await scenario('readback-states',async()=>{
    await load();await resetRef();await app.locator('#Input_Name').fill('Readback saved');await ref.locator('#f-name').fill('Readback saved');
    const loseResponse=async route=>{if(route.request().method()==='POST'){await route.fetch();await route.abort('connectionfailed');}else await route.continue();};
    await app.route(identity('autumn-bingo-2027'),loseResponse);await app.locator('[data-identity-save]').click();await app.getByRole('button',{name:'Check again',exact:true}).waitFor();await app.unroute(identity('autumn-bingo-2027'),loseResponse);await app.locator('[data-identity-save]').click();await app.waitForFunction(()=>document.querySelector('[data-identity-state]')?.textContent.includes('Up to date'));
    await ref.evaluate(()=>{const c=window.__parityReference;return new Promise(resolve=>c.setState({base:{...c.state.form},status:'idle',saved:'present'},()=>{c.toast({text:'The event now has the values you entered.'});resolve();}));});
    await compare(engine+'-up-to-date',app,ref,[...bar,P('up-to-date toast','.toast','.toast'),P('no banner','.form-banners','.form-banners',{required:false})]);
    await app.locator('#Input_Name').fill('Autumn Bingo 2027');await app.locator('[data-identity-save]').click();await app.waitForFunction(()=>document.querySelector('.ev-name')?.textContent==='Autumn Bingo 2027');
    await load();await resetRef();await app.locator('#Input_Name').fill('My conflicting name');await ref.locator('#f-name').fill('My conflicting name');
    const stopPost=route=>route.request().method()==='POST'?route.abort('connectionfailed'):route.continue();await app.route(identity('autumn-bingo-2027'),stopPost);await app.locator('[data-identity-save]').click();await app.getByRole('button',{name:'Check again',exact:true}).waitFor();await app.unroute(identity('autumn-bingo-2027'),stopPost);
    const other=await context.newPage();await other.goto(identity('autumn-bingo-2027'));await other.locator('#Input_Name').fill('Their saved version');await other.locator('[data-identity-save]').click();await other.waitForFunction(()=>document.querySelector('[data-identity-state]')?.textContent.includes('Saved'));await other.close();
    await app.locator('[data-identity-save]').click();await app.locator('[data-conflict-for=Name] button').waitFor();await state(ref,{status:'idle',stale:{name:{kind:'conflict',latest:'Their saved version'}},staleInfo:{fields:true,schedule:false}});
    await compare(engine+'-conflict',app,ref,[P('stale banner','#stale-banner','[data-identity-feedback]'),P('conflict note','#s-name','[data-conflict-for=Name]')]);
    await load();await app.locator('#Input_Name').fill('Autumn Bingo 2027');await app.locator('[data-identity-save]').click();await app.waitForFunction(()=>document.querySelector('.ev-name')?.textContent==='Autumn Bingo 2027');
   });
   await scenario('phone-toast-save',async()=>{
    await load();await resetRef();await app.setViewportSize({width:390,height:844});await ref.setViewportSize({width:390,height:844});await app.locator('#Input_Name').fill('Phone saved');await ref.locator('#f-name').fill('Phone saved');
    await app.evaluate(()=>AdminUI.toast('Identity saved.'));await ref.evaluate(()=>window.__parityReference.toast({text:'Identity saved.'}));await app.locator('[data-identity-save]').scrollIntoViewIfNeeded();await ref.locator('#save-btn').scrollIntoViewIfNeeded();
    await compare(engine+'-phone-toast',app,ref,[P('toast','.toast','.toast'),...bar]);
    const box=await app.locator('[data-identity-save]').boundingBox();assert.equal(await app.evaluate(({x,y})=>document.elementFromPoint(x,y)?.closest('[data-identity-save]')!==null,{x:box.x+box.width/2,y:box.y+box.height/2}),true,'Q5 toast never covers Save');
    let posts=0;const count=request=>{if(request.method()==='POST')posts++;};app.on('request',count);await app.locator('[data-identity-save]').click();await app.waitForFunction(()=>document.querySelector('[data-identity-state]')?.textContent.includes('Saved'));assert.equal(posts,1);app.removeListener('request',count);
    await app.locator('#Input_Name').fill('Autumn Bingo 2027');await app.locator('[data-identity-save]').click();await app.waitForFunction(()=>document.querySelector('.ev-name')?.textContent==='Autumn Bingo 2027');await app.setViewportSize({width:1280,height:900});await ref.setViewportSize({width:1280,height:900});
   });
   await scenario('loading-failure',async()=>{
    await load();await resetRef();let release;const gate=new Promise(resolve=>release=resolve);const destination=identity('winter-bingo-2027');await app.route(destination,async route=>{await gate;await route.abort('connectionfailed');});
    await app.locator('.event-switch').click();await app.locator(`#admin-event-menu a[href$="/${fixture.events['winter-bingo-2027']}"]`).click();await app.locator('[data-page-skeleton]').waitFor();
    await ref.evaluate(()=>{const c=window.__parityReference;return new Promise(resolve=>c.setState({slug:'winter-bingo-2027',loading:true},resolve));});
    await compare(engine+'-skeleton',app,ref,[P('loading heading','.page-head','[data-page-skeleton] .page-head'),P('loading card','.card[aria-busy=true]','[data-page-skeleton] .card',{text:false}),P('shimmer','.sk','[data-page-skeleton] .sk',{text:false,dimensions:['width','height']})]);
    assert.equal(await app.locator('.ev-name').textContent(),'Winter Bingo 2027');assert.equal(await app.locator('[data-identity-editor]').count(),1,'retained mounted editor');assert.equal(await app.locator('[data-identity-editor]').isVisible(),false);
    release();await app.locator('[data-load-retry]').waitFor();await state(ref,{loading:false,loadError:true});await compare(engine+'-failed-load',app,ref,[P('failure head','.page-head','[data-page-skeleton] .page-head'),P('failure','.empty.is-error','.empty.is-error',{text:false}),P('failure title','.empty-title','.empty-title'),P('failure message','.empty-text','.empty-text'),P('retry','#retry-btn','[data-load-retry]'),P('failure icon','.empty-ic svg','.empty-ic svg')]);await app.unroute(destination);
   });
   await scenario('copy',async()=>{
    await load();await resetRef();
    for(const page of [app,ref])await page.evaluate(()=>Object.defineProperty(navigator,'clipboard',{configurable:true,value:{writeText:async()=>{}}}));
    await app.locator('[data-copy-url]').click();await ref.locator('#copy-link').click();await compare(engine+'-copied',app,ref,[P('copy state','#copy-link','[data-copy-url]'),P('copy toast','.toast','.toast')]);
    await load();await resetRef();for(const page of [app,ref])await page.evaluate(()=>Object.defineProperty(navigator,'clipboard',{configurable:true,value:{writeText:async()=>{throw Error('Controlled clipboard rejection');}}}));
    await app.locator('[data-copy-url]').click();await ref.locator('#copy-link').click();await compare(engine+'-copy-failed',app,ref,[P('copy failure toast','.toast','.toast')]);assert.equal(await app.evaluate(()=>getSelection().toString()),await app.locator('#event-link').textContent());
   });
   await scenario('served-language',async()=>{
    await load();await resetRef();await settle(app);await app.evaluate(()=>{window.languageAnimations=[];document.addEventListener('animationstart',event=>{if(event.target.matches('[data-language-transition],.fade-in'))window.languageAnimations.push({name:event.animationName,duration:getComputedStyle(event.target).animationDuration,theme:getComputedStyle(event.target).getPropertyValue('--dk-dur-theme').trim()});});});await app.evaluate(()=>{window.__documentMarker='language-retained';window.__sidebar=document.querySelector('[data-shell-sidebar]');});
    await app.locator('#Input_Name').fill('Language draft');await ref.locator('#f-name').fill('Language draft');let posts=0;const count=request=>{if(request.method()==='POST'&&new URL(request.url()).pathname.toLowerCase()==='/language')posts++;};app.on('request',count);
    await app.locator('[name=culture][value=da]').click();await ref.locator('.crumb-btn').click();await app.getByRole('alertdialog').waitFor();await compare(engine+'-language-guard',app,ref,modal);
    await app.getByRole('button',{name:'Keep editing',exact:true}).click();assert.equal(posts,0);assert.equal(await app.locator('html').getAttribute('lang'),'en');assert.equal(await app.locator('#Input_Name').inputValue(),'Language draft');
    await app.locator('[name=culture][value=da]').click();await app.getByRole('button',{name:'Discard',exact:true}).click();await app.waitForFunction(()=>document.documentElement.lang==='da');assert.equal(await app.evaluate(()=>window.__documentMarker),'language-retained');assert.equal(await app.evaluate(()=>window.__sidebar===document.querySelector('[data-shell-sidebar]')),true);assert.equal(posts,1);assert.equal(await app.locator('[data-page-skeleton]').count(),0);
    await settle(app);const animations=await app.evaluate(()=>window.languageAnimations);assert.deepEqual(animations.map(a=>a.name),['adminLanguageFade','adminLanguageFade','adminLanguageFade']);
    const milliseconds=value=>parseFloat(value)*(value.endsWith('ms')?1:1000);
    for(const animation of animations)assert.equal(milliseconds(animation.duration),milliseconds(animation.theme),'language uses exactly the theme duration');
    await app.screenshot({path:path.join(output,engine+'-language-danish-app.png'),fullPage:true});
    await app.emulateMedia({reducedMotion:'reduce'});await app.evaluate(()=>window.languageAnimations=[]);
    await app.locator('[name=culture][value=en]').click();await app.waitForFunction(()=>document.documentElement.lang==='en');assert.equal(posts,2);await settle(app);const reducedAnimations=await app.evaluate(()=>window.languageAnimations);assert.deepEqual(reducedAnimations,[],'reduced motion has no entrance or language animation');app.removeListener('request',count);results.push({name:engine+'-served-language',passed:true,animations,reducedAnimations});
   });
   await scenario('served-navigation',async()=>{
    await load('winter-bingo-2027');await app.evaluate(()=>window.__documentMarker='retained');
    const scripts=await app.locator('script[src]').evaluateAll(nodes=>nodes.map(n=>n.src));assert.equal(scripts.length,4);for(const src of scripts)assert.match(src,/\.[a-z0-9]{10}\.js$/);
    await app.locator('.event-switch').click();await app.locator(`#admin-event-menu a[href$="/${fixture.events['clan-cup-pvm-week']}"]`).click();await app.waitForURL(identity('clan-cup-pvm-week'));await app.locator('#Input_Name').waitFor();assert.equal(await app.evaluate(()=>window.__documentMarker),'retained','fingerprinted layout swaps without full reload');
    await app.locator('[data-shell-event-context] a[href*="/Manage/"]').click();await app.waitForURL(/\/Manage\//);assert.equal(await app.locator('[data-admin-design]').count(),0,'old page full load');await app.goBack();await app.locator('[data-identity-editor]').waitFor();assert.equal(await app.locator('#Input_Name').inputValue(),'Clan Cup: PvM Week');
    await Promise.all([app.waitForEvent('load'),app.evaluate(()=>window.dispatchEvent(new PageTransitionEvent('pageshow',{persisted:true})))]);await app.locator('#Input_Name').fill('Usable after persisted pageshow');assert.equal(await app.locator('[data-identity-save]').getAttribute('aria-disabled'),'false');
    results.push({name:engine+'-served-navigation',passed:true});
   });
   await browser.close();browser=null;
  }
 }finally{if(browser)await browser.close();await fixture.close();fs.writeFileSync(path.join(output,'results.json'),JSON.stringify({passed:results.filter(r=>r.passed).length,failed:results.filter(r=>!r.passed).length,results},null,2));}
 process.exitCode=results.some(r=>!r.passed)?1:0;
})().catch(error=>{console.error(error);process.exitCode=1;});

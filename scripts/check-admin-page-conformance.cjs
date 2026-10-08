// U3 shared page conformance. Register a family once in admin-page-conformance-pages.cjs.
// Q-H1/Q-SK2: reference geometry, with only summary/card and text-wrap growth.
const assert=require('node:assert/strict'),fs=require('node:fs'),path=require('node:path');
const {chromium,webkit}=require('playwright');
const {startFixture,login,referencePage}=require('./lib/admin-parity-fixture.cjs');
const registrations=require('./lib/admin-page-conformance-pages.cjs');
const {observe,checkFrames,checkLoaded,checkUpdate,checkDanish,checkSources,checkDocument,checkFast,checkLoadingSummary,checkRegisteredLinks,registeredBlockShifts}=require('./lib/admin-page-conformance-checks.cjs');
async function until(page,fn){for(let i=0;i<200;i++)if(await page.evaluate(fn))return;throw Error('Microtask checkpoint not reached');}
(async()=>{
 const name=process.env.PLAYWRIGHT_BROWSER||'chromium',engine=name==='webkit'?webkit:chromium,output=path.join(process.cwd(),'artifacts/page-conformance-'+name);
 const pages=registrations.filter(p=>!process.env.BINGO_CONFORMANCE_PAGES||process.env.BINGO_CONFORMANCE_PAGES.split(',').includes(p.family));
 assert.ok(pages.length,'at least one registered family selected');
 // A page may need opt-in fixture data (registration.fixtureEnv); each distinct environment
 // gets its own fixture, so other pages keep the accepted fixture.
 const groups=[...new Set(pages.map(p=>JSON.stringify(p.fixtureEnv||{})))];
 const records=[];let browser,fixture;
 try{
  browser=await engine.launch({headless:true,...(name==='chromium'?{channel:process.env.PLAYWRIGHT_CHANNEL||'chromium'}:{})});
  fs.mkdirSync(output,{recursive:true});
  fs.writeFileSync(path.join(output,'design-checks.json'),JSON.stringify(await checkSources(browser,pages),null,2)+'\n');
  for(const group of groups){
  fixture=await startFixture(process.cwd(),output,JSON.parse(group));
  const context=await browser.newContext({reducedMotion:'no-preference'}),refs=await browser.newContext({reducedMotion:'reduce'});
  const signed=await login(context,fixture);await signed.close();const storageState=await context.storageState();
  const paths=Object.fromEntries(registrations.map(p=>[p.family,p.url(fixture)]));
  const selectors=Object.fromEntries(registrations.map(p=>[p.family,p.blocks]));
  const box=(page,family)=>page.evaluate(({family,selectors,typography})=>{
   const root=document.querySelector('[data-page-skeleton]')||document.querySelector('[data-page-region]>.page:not([hidden])')||document.querySelector('.page');
   const rect=e=>{if(!e)return null;const r=e.getBoundingClientRect();return{x:r.x,y:r.y,width:r.width,height:r.height};};
   const stats=root.querySelector('.stat-strip');
   const bars=stats?[...stats.querySelectorAll('.stat:first-child .sk')].map(rect):[];
   let oneLine=null,wrapGrowth=0;
   if(family==='dashboard'&&!root.hasAttribute('data-page-skeleton')){
    const clone=stats.cloneNode(true);
    clone.style.cssText='position:absolute;visibility:hidden;pointer-events:none;width:'+stats.getBoundingClientRect().width+'px';
    for(const line of clone.querySelectorAll('.stat-label,.stat-note')){
     const original=stats.querySelectorAll(line.classList.contains('stat-label')?'.stat-label':'.stat-note')[Array.from(clone.querySelectorAll(line.classList.contains('stat-label')?'.stat-label':'.stat-note')).indexOf(line)];
     const css=getComputedStyle(original),height=Math.max(line.classList.contains('stat-note')?17:0,parseFloat(css.lineHeight));
     line.style.height=height+'px';line.style.minHeight=height+'px';line.style.whiteSpace='nowrap';line.style.flexWrap='nowrap';
    }
    for(const value of clone.querySelectorAll('.stat-value'))value.style.whiteSpace='nowrap';
    stats.parentElement.append(clone);oneLine=rect(clone);
    const originals=[...stats.querySelectorAll('.stat-value')],copies=[...clone.querySelectorAll('.stat-value')];
    if(originals.some((e,i)=>Math.abs(e.getBoundingClientRect().height-copies[i].getBoundingClientRect().height)>.1))throw Error('Value wraps: only label/note wrapping is authorized');
    wrapGrowth=stats.getBoundingClientRect().height-oneLine.height;clone.remove();
   }
   const lineRows=[...root.querySelectorAll(Object.keys(typography).map(c=>'.'+c).join(','))].map(e=>{const [font,line,min=0]=typography[e.className];return{className:e.className,row:rect(e),bar:rect(e.querySelector('.sk')),expectedLine:Math.max(min,parseFloat(getComputedStyle(e).getPropertyValue('--dk-fs-'+font))*line)};});
   return{title:rect(root.querySelector('.h1')),summary:rect(root.querySelector('.summary')),summaryLine:parseFloat(getComputedStyle(root.querySelector('.summary')).lineHeight),head:rect(root.querySelector('.page-head')),blocks:Object.fromEntries(Object.entries(selectors).map(([k,s])=>[k,rect(root.querySelector(s))])),bars,oneLine,wrapGrowth,lineRows};
  },{family,selectors:selectors[family],typography:registrations.find(p=>p.family===family).textRows});
  for(const registration of pages.filter(p=>JSON.stringify(p.fixtureEnv||{})===group)){
   const {family}=registration;
   const ref=await referencePage(refs,fixture,process.cwd(),registration.reference);
   // A reference that opens on another event shows that event's state (registration.referenceEvent).
   if(registration.referenceEvent)await ref.evaluate(slug=>new Promise(resolve=>__parityReference.setState({slug,snap:JSON.parse(JSON.stringify(__parityReference.world[slug]))},resolve)),registration.referenceEvent);
   const html=await(await context.request.get(fixture.origin+paths[family])).text();
   for(const width of[390,494,860,1280,1440])for(const presentation of family==='dashboard'?['normal','one-line','wrapped']:['normal']){
    const caseContext=await browser.newContext({storageState,reducedMotion:'no-preference'});const page=await caseContext.newPage(),pageErrors=[];page.on('pageerror',error=>pageErrors.push(error.message));await page.setViewportSize({width,height:1000});await ref.setViewportSize({width,height:1000});
    const from=family==='identity'?'events':'identity';await page.goto(fixture.origin+paths[from]);await page.evaluate(async()=>{await import(document.querySelector('script[data-admin-page-script]').src);await document.fonts.ready;});
    await observe(page);
    await page.clock.install({time:new Date('2030-01-01T00:00:00Z')});await page.clock.pauseAt(new Date('2030-01-01T00:01:00Z'));
    const response=await page.evaluate(({html,presentation})=>{
     const doc=new DOMParser().parseFromString(html,'text/html'),card=doc.querySelector('[data-dashboard] .stat-strip');
     if(card&&presentation!=='normal'){
      for(const label of card.querySelectorAll('.stat-label'))label.textContent=presentation==='one-line'?'Label':'A deliberately long statistic label that wraps over multiple text lines';
      for(const note of card.querySelectorAll('.stat-note'))note.textContent=presentation==='one-line'?'Note':'A deliberately long statistic note that wraps over multiple text lines';
     }
     return '<!DOCTYPE html>'+doc.documentElement.outerHTML;
    },{html,presentation});
    await page.evaluate(({html,url})=>{window.geometryFetch=fetch;window.timers=[];const timer=setTimeout;window.setTimeout=(fn,ms,...a)=>{timers.push(ms);return timer(fn,ms,...a);};window.fetch=()=>new Promise(resolve=>window.fulfill=()=>resolve(new Response(html,{headers:{'Content-Type':'text/html'}})));window.done=false;void AdminUI.navigate(url).then(()=>done=true);},{html:response,url:paths[family]+'?bodyMeasure=1'});
    await until(page,()=>!!window.fulfill);await page.clock.runFor(150);await until(page,()=>!!document.querySelector('[data-page-skeleton]'));await page.evaluate(()=>document.fonts.ready);await page.clock.runFor(32);
    const loading=await box(page,family);await checkLoadingSummary(page,registration);await checkFrames(page);await page.evaluate(()=>fulfill());await until(page,()=>timers.includes(368));await page.clock.runFor(367);assert.equal(await page.evaluate(()=>done),false,'399ms minimum hold');await page.clock.runFor(1);await until(page,()=>done);await page.clock.runFor(32);const loaded=await box(page,family);await checkFrames(page);await checkRegisteredLinks(page,paths);await checkLoaded(page,registration,width);
    await ref.evaluate(()=>new Promise(resolve=>__parityReference.setState({loading:false},resolve)));await ref.evaluate(()=>document.fonts.ready);const referenceLoaded=await box(ref,family);
    await ref.evaluate(()=>new Promise(resolve=>__parityReference.setState({loading:true},resolve)));const referenceLoading=await box(ref,family);
    const delta=loaded.head.height-loading.head.height;
    const shifts=registeredBlockShifts(registration,loading,loaded,delta);
    records.push({engine:name,family,width,presentation,loading,loaded,delta,shifts,referenceLoading,referenceLoaded});
    const close=(actual,expected,message)=>assert.ok(Math.abs(actual-expected)<=.1,message+': '+actual+' vs '+expected);
    close(loading.summary.height,loading.summaryLine*(width<=640?2:1),'U3-Q6 phone/wide summary reservation');
    close(loading.head.x,loaded.head.x,'header x');close(loading.head.y,loaded.head.y,'header y');close(loading.head.width,loaded.head.width,'header width');
    close(loading.title.x,loaded.title.x,'title x');close(loading.title.height,loaded.title.height,'title line height');
    if(family!=='dashboard'){close(delta,loaded.summary.height-loading.summary.height,'only summary growth changes header');close(loading.title.y,loaded.title.y,'title y');}
    for(const k of Object.keys(shifts))for(const axis of['x','yBeyondHeader','width'])close(shifts[k][axis],family==='dashboard'&&k==='section'&&axis==='yBeyondHeader'?loaded.wrapGrowth:0,family+' '+width+' '+presentation+' '+k+' '+axis);
    if(family==='dashboard'){
     close(loading.blocks.stats.height,loaded.oneLine.height,'one-line statistic reservation');
     if(presentation==='one-line')close(loaded.wrapGrowth,0,'one-line has zero movement');
     if(presentation==='wrapped')assert.ok(loaded.wrapGrowth>0,'forced label/note wrapping exercises positive growth');
     for(let i=0;i<3;i++){
      close(loading.bars[i].height,referenceLoading.bars[i].height,'reference bar height');
      close(loading.bars[i].width,referenceLoading.bars[i].width,'reference bar width');
      if(i)close(loading.bars[i].y-loading.bars[i-1].y-loading.bars[i-1].height,12,'reference visible gap');
     }
    }
    assert.ok(loading.lineRows.length,'text-line rows exist');
    for(const row of loading.lineRows){close(row.row.height,row.expectedLine,'real typography row '+row.className);assert.ok(row.row.height>=row.bar.height,'bar fits its real text-line row');}
    console.log('PASS '+family+' '+width+' '+presentation+' wrap growth '+loaded.wrapGrowth);
    await page.evaluate(()=>window.fetch=geometryFetch);
    if(width===1280&&presentation==='normal')await checkFast(page,registration,paths,html);
    await page.clock.resume();await checkDocument(page,registration,width);
    if(width===1280&&presentation==='normal'){
     // The in-place update probe may live on another state of the same page (update.url).
     if(registration.update.url){await page.goto(fixture.origin+registration.update.url(fixture));await page.locator(registration.update.control).waitFor({state:'attached'});}
     const {url:updateUrl,...update}=registration.update;void updateUrl;
     await checkUpdate(page,{...registration,update});
     if(updateUrl){await page.goto(fixture.origin+paths[family]);await page.locator(registration.style[0]).first().waitFor();}
     await checkDanish(page,registration,fixture,paths);
    }
    assert.deepEqual(pageErrors,[],family+': no page errors');await caseContext.close();
   }
   await ref.close();
  }
  await context.close();await refs.close();await fixture.close();fixture=null;
  }
  console.log('PASS '+records.length+' registered page geometry cases plus frame/style, no-fade, document, update and Danish checks');
 }finally{await browser?.close();await fixture?.close();fs.writeFileSync(path.join(output,'positions.json'),JSON.stringify(records,null,2));}
})().catch(error=>{console.error(error);process.exitCode=1;});

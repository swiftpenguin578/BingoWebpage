// Brief76 item4: actual skeleton/loaded body geometry, not data-height equality.
const assert=require('node:assert/strict'),fs=require('node:fs'),path=require('node:path');
const {chromium,webkit}=require('playwright');
const {startFixture,login,referencePage}=require('./lib/admin-parity-fixture.cjs');
async function until(page,fn){for(let i=0;i<200;i++)if(await page.evaluate(fn))return;throw Error('Microtask checkpoint not reached');}
(async()=>{
 const name=process.env.PLAYWRIGHT_BROWSER||'chromium',engine=name==='webkit'?webkit:chromium,output=path.join(process.cwd(),'artifacts/u2-rem4-body-'+name);
 const fixture=await startFixture(process.cwd(),output),records=[];let browser;
 try{
  browser=await engine.launch({headless:true,...(name==='chromium'?{channel:process.env.PLAYWRIGHT_CHANNEL||'chromium'}:{})});
  const context=await browser.newContext({reducedMotion:'reduce'}),refs=await browser.newContext({reducedMotion:'reduce'});
  const signed=await login(context,fixture);await signed.close();
  const paths={dashboard:'/Admin',events:'/Admin/Events/Index',identity:'/Admin/Events/Identity/'+fixture.events['autumn-bingo-2027']};
  const selectors={dashboard:{first:'.card',stats:'.stat-strip',section:'.dash-grid>.card:first-child'},events:{first:'.card',toolbar:'.toolbar',table:'.ev-tbl'},identity:{first:'.card.form-card'}};
  const box=(page,family)=>page.evaluate(({family,selectors})=>{
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
   const lineSelectors={dashboard:'.dash-sk-stat-label,.dash-sk-stat-value,.dash-sk-stat-note,.dash-sk-panel-title,.dash-sk-fact-first,.dash-sk-fact',events:'.events-sk-name,.events-sk-main,.events-sk-start,.events-sk-end',identity:'.identity-sk-title'};
   const typography={
    'dash-sk-stat-label':['small',1.45],'dash-sk-stat-value':['stat',1.1],'dash-sk-stat-note':['meta',1.45,17],
    'dash-sk-panel-title':['control',1.45],'dash-sk-fact-first':['control',1.45],'dash-sk-fact':['control',1.45],
    'events-sk-name':['body',1.45],'events-sk-main':['control-sm',1.45],'events-sk-start':['control-sm',1.45],'events-sk-end':['small',1.45],
    'identity-sk-title':['control',1.45]
   };
   const lineRows=[...root.querySelectorAll(lineSelectors[family])].map(e=>{const [font,line,min=0]=typography[e.className];return{className:e.className,row:rect(e),bar:rect(e.querySelector('.sk')),expectedLine:Math.max(min,parseFloat(getComputedStyle(e).getPropertyValue('--dk-fs-'+font))*line)};});
   return{head:rect(root.querySelector('.page-head')),blocks:Object.fromEntries(Object.entries(selectors).map(([k,s])=>[k,rect(root.querySelector(s))])),bars,oneLine,wrapGrowth,lineRows};
  },{family,selectors:selectors[family]});
  for(const family of Object.keys(paths)){
   const ref=await referencePage(refs,fixture,process.cwd(),family==='dashboard'?'Dashboard.dc.html':family==='events'?'Events.dc.html':'Identity.dc.html');
   const html=await(await context.request.get(fixture.origin+paths[family])).text();
   for(const width of[390,494,860,1280,1440])for(const presentation of family==='dashboard'?['normal','one-line','wrapped']:['normal']){
    const page=await context.newPage();await page.setViewportSize({width,height:1000});await ref.setViewportSize({width,height:1000});
    const from=family==='identity'?'events':'identity';await page.goto(fixture.origin+paths[from]);await page.evaluate(async()=>{await import(document.querySelector('script[data-admin-page-script]').src);await document.fonts.ready;});
    await page.clock.install({time:new Date('2030-01-01T00:00:00Z')});await page.clock.pauseAt(new Date('2030-01-01T00:01:00Z'));
    const response=await page.evaluate(({html,presentation})=>{
     const doc=new DOMParser().parseFromString(html,'text/html'),card=doc.querySelector('[data-dashboard] .stat-strip');
     if(card&&presentation!=='normal'){
      for(const label of card.querySelectorAll('.stat-label'))label.textContent=presentation==='one-line'?'Label':'A deliberately long statistic label that wraps over multiple text lines';
      for(const note of card.querySelectorAll('.stat-note'))note.textContent=presentation==='one-line'?'Note':'A deliberately long statistic note that wraps over multiple text lines';
     }
     return '<!DOCTYPE html>'+doc.documentElement.outerHTML;
    },{html,presentation});
    await page.evaluate(({html,url})=>{window.timers=[];const timer=setTimeout;window.setTimeout=(fn,ms,...a)=>{timers.push(ms);return timer(fn,ms,...a);};window.fetch=()=>new Promise(resolve=>window.fulfill=()=>resolve(new Response(html,{headers:{'Content-Type':'text/html'}})));window.done=false;void AdminUI.navigate(url).then(()=>done=true);},{html:response,url:paths[family]+'?bodyMeasure=1'});
    await until(page,()=>!!window.fulfill);await page.clock.runFor(150);await until(page,()=>!!document.querySelector('[data-page-skeleton]'));await page.evaluate(()=>document.fonts.ready);await page.clock.runFor(32);
    const loading=await box(page,family);await page.evaluate(()=>fulfill());await until(page,()=>timers.includes(368));await page.clock.runFor(368);await until(page,()=>done);await page.clock.runFor(32);const loaded=await box(page,family);
    await ref.evaluate(()=>new Promise(resolve=>__parityReference.setState({loading:false},resolve)));await ref.evaluate(()=>document.fonts.ready);const referenceLoaded=await box(ref,family);
    await ref.evaluate(()=>new Promise(resolve=>__parityReference.setState({loading:true},resolve)));const referenceLoading=await box(ref,family);
    const delta=loaded.head.height-loading.head.height;
    const shifts=Object.fromEntries(Object.keys(loading.blocks).filter(k=>loading.blocks[k]&&loaded.blocks[k]).map(k=>[k,{x:loaded.blocks[k].x-loading.blocks[k].x,yBeyondHeader:loaded.blocks[k].y-loading.blocks[k].y-delta,width:loaded.blocks[k].width-loading.blocks[k].width}]));
    records.push({engine:name,family,width,presentation,loading,loaded,delta,shifts,referenceLoading,referenceLoaded});
    const close=(actual,expected,message)=>assert.ok(Math.abs(actual-expected)<=.1,message+': '+actual+' vs '+expected);
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
    await page.clock.resume();await page.close();
   }
   await ref.close();
  }
  console.log('PASS '+records.length+' body loading/loaded checks');
 }finally{await browser?.close();await fixture.close();fs.writeFileSync(path.join(output,'positions.json'),JSON.stringify(records,null,2));}
})().catch(error=>{console.error(error);process.exitCode=1;});

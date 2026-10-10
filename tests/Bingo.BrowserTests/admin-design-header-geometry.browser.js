// Dashboard header geometry: loading and loaded flow in the engine the runner selects.
process.env.BINGO_PARITY_ENGINES = process.env.PLAYWRIGHT_BROWSER === 'webkit' ? 'webkit' : 'chromium';
// Q-H1 Dashboard header: loaded/loading flow proof with durable positions.
const assert=require('node:assert/strict');
const fs=require('node:fs'),path=require('node:path');
const {chromium,webkit}=require('playwright');
const {startFixture,login}=require('../../scripts/lib/admin-parity-fixture.cjs');
async function until(page,fn){for(let i=0;i<200;i++)if(await page.evaluate(fn))return;throw Error('Microtask checkpoint not reached');}
(async()=>{
 const records=[];
 const engineNames=(process.env.BINGO_PARITY_ENGINES||'chromium,webkit').split(',');
 assert.ok(engineNames.length&&engineNames.every(name=>['chromium','webkit'].includes(name)),'Explicit supported header-proof engines required');
 const engines=engineNames.map(name=>name==='webkit'?webkit:chromium);
 const runKey=engineNames.join('-');
 for(const narrow of(process.env.HEADER_DIAG==='1'?[true]:[false,true])){
  const fixture=await startFixture(process.cwd(),path.join(process.cwd(),'artifacts/u2-header-'+runKey+'-'+narrow),narrow?{BINGO_PARITY_NARROW_CARD:'1'}:{});
  try{for(const engine of engines){
   const browser=await engine.launch({headless:true,...(engine===chromium?{channel:process.env.PLAYWRIGHT_CHANNEL||'chromium'}:{})});
   try{
    const context=await browser.newContext({viewport:{width:1440,height:1000},reducedMotion:'reduce'});
    const signed=await login(context,fixture);await signed.close();
    const html=await(await context.request.get(fixture.origin+'/Admin')).text();
    for(const width of(process.env.HEADER_DIAG==='1'?[861]:[390,861,1280,1440])){
     const page=await context.newPage();await page.setViewportSize({width,height:1000});
     await page.goto(fixture.origin+'/Admin/Events/Identity/'+fixture.events['autumn-bingo-2027']);
     await page.evaluate(async()=>{await import(document.querySelector('script[data-admin-page-script]').src);await document.fonts.ready;});
     await page.clock.install({time:new Date('2030-01-01T00:00:00Z')});await page.clock.pauseAt(new Date('2030-01-01T00:01:00Z'));
     await page.evaluate(html=>{
      window.timerLog=[];const timer=window.setTimeout;window.setTimeout=(fn,ms,...args)=>{timerLog.push(ms);return timer(fn,ms,...args);};
      const fetch=window.fetch;window.fetch=(url,options)=>String(url).includes('headerMeasure')?new Promise(resolve=>{window.waiting=true;window.fulfill=()=>resolve(new Response(html,{headers:{'Content-Type':'text/html'}}));}):fetch(url,options);
      window.done=false;AdminUI.navigate('/Admin?headerMeasure=1').then(()=>done=true);
     },html);
     await until(page,()=>waiting);await page.clock.runFor(150);
     await until(page,()=>{const grid=document.querySelector('[data-page-skeleton] .dash-grid');return !!grid&&getComputedStyle(grid).display==='grid';});
     const styles=()=>page.evaluate(async()=>{
      await Promise.all([...document.querySelectorAll('link[rel=stylesheet]')].map(link=>link.sheet?Promise.resolve():new Promise((resolve,reject)=>{link.addEventListener('load',resolve,{once:true});link.addEventListener('error',()=>reject(Error('Stylesheet failed')),{once:true});})));
      void document.body.offsetHeight;await document.fonts.ready;
     });
     await styles();await until(page,()=>{const card=document.querySelector('[data-dashboard-loading-card]');return !!card&&getComputedStyle(card).flex==='0 0 auto';});await page.clock.runFor(32);
     const box=()=>page.evaluate(()=>{
      const root=document.querySelector('[data-page-skeleton]')||document.querySelector('[data-page-region] > .page:not([hidden])');
      return Object.fromEntries([['head','.page-head'],['group','.page-head > div:first-child'],['title','.h1'],['summary','.summary'],['card','.next-event'],['first','.card']].map(([key,selector])=>{const r=(selector===':scope'?root:root.querySelector(selector)).getBoundingClientRect();return[key,{x:r.x,y:r.y,width:r.width,height:r.height}];}));
     });
     const loading=await box();await page.evaluate(()=>fulfill());await until(page,()=>timerLog.includes(368));await page.clock.runFor(368);await until(page,()=>done);
     await styles();await until(page,()=>{const card=document.querySelector('[data-page-region] .next-event');return !!card&&getComputedStyle(card).flex==='0 0 auto';});await page.clock.runFor(32);const loaded=await box();
     const close=(actual,expected,label)=>assert.ok(Math.abs(actual-expected)<=0.1,label+' '+actual+' != '+expected);
     for(const [kind,value]of Object.entries({loading,loaded})){
       const groupHeight=value.title.height+6+value.summary.height;
       const headHeight=width<=860?groupHeight+24+value.card.height:Math.max(groupHeight,value.card.height);
       close(value.head.height,headHeight,kind+' header height from summary/card');
       close(value.title.y,value.head.y+(width<=860?0:headHeight-groupHeight),kind+' bottom-aligned title');
       close(value.summary.y,value.title.y+value.title.height+6,kind+' summary flow');
       close(value.card.y,value.head.y+(width<=860?groupHeight+24:headHeight-value.card.height),kind+' card flow');
       close(value.first.y,value.head.y+headHeight+20,kind+' following content flow');
     }
     close(loading.head.x,loaded.head.x,'page x retained');close(loading.head.y,loaded.head.y,'page y retained');close(loading.head.width,loaded.head.width,'page width retained');
     console.log('PASS '+engine.name()+' '+(narrow?'narrow':'normal')+' '+width+' loaded/loading flow');
     const detail=async(surface)=>surface.locator('.page-head').last().evaluate(root=>({html:root.outerHTML,nodes:[root,root.firstElementChild,...root.querySelectorAll('.next-event,.next-event .grow,.next-meta,.btn,.meter-label')].map(e=>({tag:e.tagName,cls:e.className,width:getComputedStyle(e).width,flex:getComputedStyle(e).flex,gap:getComputedStyle(e).gap,font:getComputedStyle(e).font,text:e.textContent}))}));
     records.push({engine:engine===chromium?'chromium':'webkit',narrow,width,loading,loaded,...(process.env.HEADER_DIAG==='1'?{appDetail:await detail(page)}:{})});await page.close();
    }
    await context.close();
   }finally{await browser.close();}
  }}finally{await fixture.close();}
 }
 const output=path.join(process.cwd(),'artifacts/u2-header-positions-'+runKey+'.json');fs.writeFileSync(output,JSON.stringify(records,null,2));console.log('Header flow: '+records.length+' passed, 0 failed; '+output);
})().catch(error=>{console.error(error);process.exitCode=1;});

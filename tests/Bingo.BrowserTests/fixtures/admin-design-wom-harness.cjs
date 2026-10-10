// U9 WOM browser scenarios share this harness; each admin-design-wom-*.browser.js file owns whole scenarios.
// Owned PostgreSQL/Kestrel only.
const assert=require('node:assert/strict'),path=require('node:path'),fs=require('node:fs');
const {chromium,webkit}=require('playwright');
const {startFixture,login}=require('../../../scripts/lib/admin-parity-fixture.cjs');
module.exports=function runWom(name,define){
(async()=>{
 const engine=process.env.PLAYWRIGHT_BROWSER||'chromium',output=path.join(process.cwd(),'artifacts/'+name+'-'+engine),cases=[];
 const browser=await(engine==='webkit'?webkit:chromium).launch({headless:true});
 async function scenario(variant,body){
  if(process.env.U9_WOM_VARIANTS&&!process.env.U9_WOM_VARIANTS.split(',').includes(variant))return;
  const fixture=await startFixture(process.cwd(),path.join(output,variant),{BINGO_PARITY_UR_PROFILE:variant==='rate-limited'||variant==='fetch-ready'?'live':'final-review',BINGO_PARITY_U9_WOM:variant});
  // Release the fixture whatever happens to the browser (a closed browser rejects
  // newContext/context.close); its dotnet/python children otherwise keep Node alive.
  let context;
  try{context=await browser.newContext({viewport:{width:1280,height:1000}});const page=await login(context,fixture),errors=[];page.on('pageerror',error=>errors.push(error.message));page.on('dialog',dialog=>dialog.accept());
   const route=slug=>'/Admin/Events/WiseOldMan/'+fixture.events[slug],go=async slug=>{await page.goto(fixture.origin+route(slug));await page.waitForFunction(()=>window.AdminUI&&document.querySelector('[data-wom]'));};
   const state=()=>page.locator('[data-wom]').getAttribute('data-current').then(JSON.parse),accept=()=>page.locator('.modal [data-confirm-accept]'),notice=()=>page.locator('[data-wom-notice]');
   await body({page,fixture,route,go,state,accept,notice});assert.deepEqual(errors,[]);console.log('PASS WOM '+variant+' ['+engine+']');
  }finally{try{await context?.close();}finally{await fixture.close();}}
 }
 try{
 await define({scenario,cases,output});
 fs.writeFileSync(path.join(output,process.env.U9_WOM_VARIANTS?'focused-results.json':'results.json'),JSON.stringify({engine,cases},null,2));console.log('PASS '+cases.length+' WOM browser scenarios ['+engine+']');
 }finally{await browser.close();}
})().catch(error=>{console.error(error);process.exitCode=1;});
};

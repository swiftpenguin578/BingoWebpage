// Gate every current/future page stylesheet using the browser's CSS parser.
const assert=require('node:assert/strict'),fs=require('node:fs'),path=require('node:path');
const {chromium,webkit}=require('playwright');
(async()=>{
 const engine=process.env.PLAYWRIGHT_BROWSER==='webkit'?webkit:chromium;
 const browser=await engine.launch({headless:true,...(engine===chromium?{channel:process.env.PLAYWRIGHT_CHANNEL||'chromium'}:{})});
 try{
  const page=await browser.newPage(),directory='src/Bingo.Web/wwwroot/css';
  const files=fs.readdirSync(directory).filter(name=>/^admin-design-.*\.css$/.test(name)&&!['admin-design-tokens.css','admin-design-components.css','admin-design-layout.css'].includes(name));
  let count=0;
  for(const file of files){
   const family=file.slice('admin-design-'.length,-4),source=fs.readFileSync(path.join(directory,file),'utf8');
   const result=await page.evaluate(({source,family})=>{
    const style=document.createElement('style');style.textContent=source;document.head.append(style);
    const selectors=[],bad=[],prefix=':where([data-page-family="'+family+'"])';
    function split(value){let depth=0,start=0,parts=[];for(let i=0;i<value.length;i++){if('(['.includes(value[i]))depth++;if(')]'.includes(value[i]))depth--;if(value[i]===','&&depth===0){parts.push(value.slice(start,i).trim());start=i+1;}}parts.push(value.slice(start).trim());return parts;}
    const walk=rules=>{for(const rule of rules){
     if(rule.selectorText)for(const selector of split(rule.selectorText)){selectors.push(selector);if(!selector.startsWith(prefix)||!/^([\s.#\[:>+~]|$)/.test(selector.slice(prefix.length)))bad.push(selector);}
     else if(rule.cssRules)walk(rule.cssRules);else bad.push(rule.cssText);
    }};walk(style.sheet.cssRules);style.remove();return{selectors,bad};
   },{source,family});
   assert.ok(result.selectors.length>0,file+' contains rules');assert.deepEqual(result.bad,[],file+' has unscoped selector/at-rule');count+=result.selectors.length;
  }
  console.log('PASS all'+files.length+' page stylesheets family-scoped ('+count+' selectors including media blocks)');
 }finally{await browser.close();}
})().catch(error=>{console.error(error);process.exitCode=1;});

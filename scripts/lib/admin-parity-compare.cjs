const fs = require('node:fs'), path = require('node:path');
const properties = ['fontFamily','fontSize','fontWeight','lineHeight','color','backgroundColor','paddingTop','paddingRight','paddingBottom','paddingLeft','marginTop','marginBottom','gap','borderRadius'];
async function settle(page) {
  await page.evaluate(async()=>{
    const deadline=performance.now()+5000; let quiet=0;
    while(quiet<2){await new Promise(requestAnimationFrame);const active=document.getAnimations().filter(a=>a.effect.getTiming().iterations!==Infinity);quiet=active.length?0:quiet+1;if(performance.now()>deadline)throw Error('Static styles did not settle');await Promise.all(active.map(a=>a.finished.catch(()=>{})));}
  });
}
async function capture(page,pairs,index) {
  return page.evaluate(({pairs,index,properties})=>pairs.map(([name,r,a,options={}])=>{
    const el=[...document.querySelectorAll(index===1?r:a)].find(el=>el.checkVisibility());
    if(!el)return {name,missing:true};
    const css=getComputedStyle(el),bounds=el.getBoundingClientRect();
    const relation=options.spacing;
    const anchor=relation?document.querySelector(index===1?relation.reference:relation.app):null;
    const spacing=anchor?bounds[relation.to]-anchor.getBoundingClientRect()[relation.from]:null;
    const svg=el.matches('svg')?el:el.querySelector(':scope > svg');
    const icons=svg?[...svg.children].map(child=>({tag:child.tagName,attrs:Object.fromEntries([...child.attributes].filter(a=>a.name!=='class'&&!a.name.startsWith('data-')).map(a=>[a.name,a.value]))})):null;
    return {name,text:(el.matches('input,textarea,select')?el.value:el.textContent).replace(/\s+/g,' ').trim(),styles:Object.fromEntries(properties.map(key=>[key,css[key]])),icons,rect:{x:bounds.x,y:bounds.y,width:bounds.width,height:bounds.height},spacing,options};
  }),{pairs,index,properties});
}
async function classInventory(page) {
  return page.evaluate(()=>{
    const defined=new Set();const rules=list=>{for(const rule of list){if(rule.selectorText)for(const match of rule.selectorText.matchAll(/\.(-?[_a-zA-Z]+[\w-]*)/g))defined.add(match[1]);if(rule.cssRules)rules(rule.cssRules);}};
    for(const sheet of document.styleSheets)rules(sheet.cssRules);
    const used=new Set();const walk=root=>{for(const el of root.querySelectorAll('*')){for(const name of el.classList)used.add(name);if(el.tagName==='TEMPLATE'&&!el.matches('[data-page-header-template],[data-page-loading-template],[data-page-failure-template]'))walk(el.content);}};walk(document);
    return {used:[...used].sort(),undefined:[...used].filter(name=>!defined.has(name)).sort()};
  });
}
function comparator(output,results) {
  return async function compare(name,app,reference,pairs) {
    await Promise.all([app.mouse.move(0,0),reference.mouse.move(0,0)]);
    await Promise.all([settle(app),settle(reference)]);
    const actual=await capture(app,pairs,2),expected=await capture(reference,pairs,1),differences=[];
    for(let i=0;i<expected.length;i++){
      const a=actual[i],r=expected[i],options=pairs[i][3]||{};
      if(a.missing||r.missing){if(a.missing!==r.missing || options.required)differences.push({element:r.name,property:'presence',actual:!a.missing,expected:!r.missing});continue;}
      for(const key of properties)if(a.styles[key]!==r.styles[key])differences.push({element:r.name,property:key,actual:a.styles[key],expected:r.styles[key]});
      if(options.text && a.text!==r.text)differences.push({element:r.name,property:'text',actual:a.text,expected:r.text});
      if(options.icon && JSON.stringify(a.icons)!==JSON.stringify(r.icons))differences.push({element:r.name,property:'icon geometry',actual:a.icons,expected:r.icons});
      for(const [key,tolerancePx] of Object.entries(options.box||{}))if(Math.abs(a.rect[key]-r.rect[key])>tolerancePx)differences.push({element:r.name,property:'box '+key,actual:a.rect[key],expected:r.rect[key],tolerancePx});
      if(options.spacing && (a.spacing===null || r.spacing===null || Math.abs(a.spacing-r.spacing)>options.spacing.tolerancePx))differences.push({element:r.name,property:'spacing '+options.spacing.label,actual:a.spacing,expected:r.spacing,tolerancePx:options.spacing.tolerancePx});
      for(const key of options.minimumDimensions||[])if(a.rect[key]<r.rect[key])differences.push({element:r.name,property:'minimum '+key,actual:a.rect[key],expected:r.rect[key]});
      for(const key of options.dimensions||[])if(a.rect[key]!==r.rect[key])differences.push({element:r.name,property:key,actual:a.rect[key],expected:r.rect[key]});
    }
    const classes=await classInventory(app);
    if(classes.undefined.length)differences.push({element:'shipped CSS',property:'undefined classes',actual:classes.undefined,expected:[]});
    fs.writeFileSync(path.join(output,name+'.json'),JSON.stringify({actual,expected,classes,differences},null,2));
    await app.screenshot({path:path.join(output,name+'-app.png'),fullPage:true});await reference.screenshot({path:path.join(output,name+'-reference.png'),fullPage:true});
    results.push({name,passed:!differences.length,differences});console.log(`${differences.length?'FAIL':'PASS'} ${name}: ${differences.length} differences`);
  };
}
module.exports={comparator,settle,classInventory};

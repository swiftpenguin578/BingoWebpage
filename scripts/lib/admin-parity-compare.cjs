async function settle(page) {
  await page.evaluate(async()=>{
    const deadline=performance.now()+5000; let quiet=0;
    while(quiet<2){await new Promise(requestAnimationFrame);const active=document.getAnimations().filter(a=>a.effect.getTiming().iterations!==Infinity);quiet=active.length?0:quiet+1;if(performance.now()>deadline)throw Error('Static styles did not settle');await Promise.all(active.map(a=>a.finished.catch(()=>{})));}
  });
}
async function classInventory(page) {
  return page.evaluate(()=>{
    const defined=new Set();const rules=list=>{for(const rule of list){if(rule.selectorText)for(const match of rule.selectorText.matchAll(/\.(-?[_a-zA-Z]+[\w-]*)/g))defined.add(match[1]);if(rule.cssRules)rules(rule.cssRules);}};
    for(const sheet of document.styleSheets)rules(sheet.cssRules);
    const used=new Set();const walk=root=>{for(const el of root.querySelectorAll('*')){for(const name of el.classList)used.add(name);if(el.tagName==='TEMPLATE'&&!el.matches('[data-page-header-template],[data-page-loading-template],[data-page-failure-template]'))walk(el.content);}};walk(document);
    return {used:[...used].sort(),undefined:[...used].filter(name=>!defined.has(name)).sort()};
  });
}
module.exports={settle,classInventory};

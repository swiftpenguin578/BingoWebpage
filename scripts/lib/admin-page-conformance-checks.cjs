const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');

// Native RAF is captured before installing the navigation timing clock. Mutation
// observations catch insertions; RAF catches visible blink/unstyled frames.
async function observe(page) {
  await page.evaluate(() => {
    window.nativeConformanceFrame = requestAnimationFrame.bind(window);
    window.conformanceViolations = [];
    window.conformanceInspections = 0;
    const inspect = frame => {
      const visible = [...document.querySelectorAll('[data-page-skeleton], [data-page-region] > .page')].filter(e => e.checkVisibility());
      if (frame && !visible.length) conformanceViolations.push('blank page frame');
      for (const root of visible) {
        const family = root.dataset.pageFamily;
        if (!family || root.dataset.skeletonLayout === 'generic') continue;
        const link = [...document.querySelectorAll('head link[data-admin-page-style]')].find(e => new RegExp('/admin-design-' + family + '(?:\\.[a-zA-Z0-9]+)?\\.css$').test(new URL(e.href).pathname));
        if (!link?.sheet || link.disabled || link.media === 'not all') conformanceViolations.push({family,frame,reason:'visible before active stylesheet',links:[...document.querySelectorAll('head link')].map(e=>({href:e.href,sheet:!!e.sheet,media:e.media,disabled:e.disabled}))});
        if (root.hasAttribute('data-page-skeleton')) continue;
        conformanceInspections++;
        for (const e of root.querySelectorAll(':scope > .card,:scope > .dash-grid,:scope > .dash-section,:scope > .page-banner,[data-directory-results],[data-directory-banners]')) {
          for (const a of e.getAnimations({subtree: true})) {
            if (['fadeIn', 'fade-in'].includes(a.animationName)) conformanceViolations.push(family + ': load fade on ' + a.effect?.target?.className);
          }
        }
      }
    };
    window.conformanceObserver = new MutationObserver(() => inspect(false));
    conformanceObserver.observe(document.documentElement, {subtree: true, childList: true, attributes: true});
    window.conformanceObserving = true;
    const frame = () => { if (conformanceObserving) { inspect(true); nativeConformanceFrame(frame); } };
    nativeConformanceFrame(frame);
  });
}
async function checkFrames(page) {
  await page.evaluate(() => new Promise(resolve => nativeConformanceFrame(() => nativeConformanceFrame(resolve))));
  assert.deepEqual(await page.evaluate(() => conformanceViolations), []);
}
async function checkLoaded(page, registration, width) {
  assert.equal(await page.locator('[data-page-region]>.page').getAttribute('data-page-family'), registration.family);
  assert.equal(await page.locator('[data-page-region]>.page>.page-head').getAttribute('data-page-family'), registration.family);
  assert.equal(await page.locator('[data-page-region] .fade-in').count(), 0);
  const dimensions = await page.evaluate(() => ({width: document.documentElement.scrollWidth, height: document.documentElement.scrollHeight, viewportHeight: innerHeight}));
  assert.equal(dimensions.width, width, registration.family + ': document width');
  assert.equal(dimensions.height, dimensions.viewportHeight, registration.family + ': document height');
  const style = await page.locator(registration.style[0]).first().evaluate((e, property) => getComputedStyle(e).getPropertyValue(property).trim() || getComputedStyle(e)[property], registration.style[1]);
  const expectedStyle=typeof registration.style[2]==='string'?registration.style[2]:registration.style[2][width<=registration.style[2].breakpoint?'narrow':'wide'];
  assert.equal(style, expectedStyle, registration.family + ': family CSS applies');
  for(const [selector,property,expected] of registration.additionalStyles||[]){assert.equal(await page.locator(selector).evaluate((e,key)=>getComputedStyle(e)[key],property),expected,registration.family+': '+selector);}
  const names = await page.evaluate(() => {
    const probe = document.createElement('div');
    document.querySelector('[data-page-region]>.page').append(probe);
    const names = {};
    for (const [key, cls] of Object.entries({load: 'fade-in results',sortA:'rows swap-a',sortB:'rows swap-b',flash:'row is-flash',error:'field-err',menu:'menu',modal:'modal',scrim:'scrim',drawer:'drawer'})) {
      probe.className=cls;names[key]=getComputedStyle(probe).animationName;
    }
    probe.remove();return names;
  });
  assert.deepEqual(names, {load:'none',sortA:'swapA',sortB:'swapB',flash:'rowFlash',error:'fadeIn',menu:'popIn',modal:'modalIn',scrim:'fadeIn',drawer:'drIn'});
}
async function checkUpdate(page, registration) {
  const update = registration.update;
  assert.ok(update, registration.family + ': an explicit update probe is required');
  // Hold reads so immediate control state and retained nodes are measured before
  // the response, then again when the results have changed.
  await page.evaluate(update => {
    const root=document.querySelector('[data-page-region]>.page');
    window.updateRetained=[root.querySelector('.page-head'), ...root.querySelectorAll('.toolbar,.tabs,input,select,textarea,[data-dashboard-sort]')];
    window.updateControl=root.querySelector(update.control);
    window.updateOriginal=updateControl.value;window.updateOriginalUrl=location.href;
    const main=document.querySelector('main.scroller');main.scrollTop=Math.min(60,main.scrollHeight-main.clientHeight);
    updateControl.focus({preventScroll:true});window.updateScroll=main.scrollTop;
    window.updateRequests=[];window.originalFetch=fetch;
    if(update.request)window.fetch=(url,options)=>new Promise((resolve,reject)=>{
      updateRequests.push({release:()=>originalFetch(url,options).then(resolve,reject)});
    });
    if(update.action==='input'){updateControl.value=update.value;updateControl.dispatchEvent(new Event('input',{bubbles:true}));}
    else updateControl.click();
  }, update);
  const retained = async () => {
    assert.equal(await page.evaluate(() => updateRetained.every(e => e.isConnected)), true, registration.family + ': header/toolbar/control identities');
    assert.equal(await page.evaluate(() => document.activeElement === updateControl), true, registration.family + ': focus');
    assert.equal(await page.evaluate(() => document.querySelector('main.scroller').scrollTop === updateScroll), true, registration.family + ': scroll');
    if(update.action==='input')assert.equal(await page.locator(update.control).inputValue(), update.value, registration.family + ': immediate chosen value');
    if(update.selected) {
      if(update.attribute)assert.equal(await page.locator(update.selected).evaluate((e,u)=>(u.attributeParent?e.closest(u.attributeParent):e).getAttribute(u.attribute[0]),update),update.attribute[1]);
      else assert.equal(await page.locator(update.selected).isVisible(),true);
    }
  };
  await retained();
  if(update.request){
    await page.waitForFunction(()=>updateRequests.length===1);
    await page.waitForSelector('[data-update-skeleton]');await retained();
    await page.evaluate(()=>updateRequests[0].release());
    await page.waitForFunction(()=>!document.querySelector('[data-update-skeleton]')&&location.href!==updateOriginalUrl);await retained();
  }
  await page.evaluate(update=>{
    window.fetch=originalFetch;
    if(update.action==='input'&&!update.request){updateControl.value=updateOriginal;updateControl.dispatchEvent(new Event('input',{bubbles:true}));}
  }, update);
}
async function checkDanish(page, registration, fixture, paths) {
  await page.setViewportSize({width:1280,height:900});
  await page.evaluate(()=>window.languageDocument='retained');
  await page.locator('[name=culture][value=da]').click();
  await page.waitForFunction(()=>document.documentElement.lang==='da');
  assert.equal(await page.evaluate(()=>languageDocument),'retained','language changes retain the document');
  const check = async () => {
    await page.waitForFunction(family=>document.querySelector('[data-page-region]>.page')?.dataset.pageFamily===family,registration.family);
    await checkRegisteredLinks(page,paths);
    await checkLoaded(page,registration,1280);
    assert.equal((await page.locator('[data-page-region] .h1').textContent()).trim(),registration.titleDa);
    assert.doesNotMatch(await page.locator('[data-page-region]').innerText(), /AdminDesign\.|AdminCommunity\./, 'no untranslated resource keys');
  };
  // Read the actual Danish render through the instrumented parity host. This
  // catches ResourceNotFound even for variable keys and JS data-label payloads.
  const rendered = await page.request.get(fixture.origin+paths[registration.family]);
  const audit = rendered.headers()['x-parity-danish-missing'];
  assert.ok(audit, registration.family+': Danish localizer audit must be present');
  const missing = JSON.parse(Buffer.from(audit,'base64').toString('utf8'));
  assert.deepEqual(missing, [], registration.family+': ResourceNotFound during Danish render');
  await check();
  await page.goto(fixture.origin+paths[registration.family]);await check();
  await page.goto(fixture.origin+paths[registration.family==='dashboard'?'events':'dashboard']);
  await page.waitForFunction(()=>window.AdminUI);
  await page.evaluate(url=>AdminUI.navigate(url),paths[registration.family]);await check();
  await page.locator('[name=culture][value=en]').click();await page.waitForFunction(()=>document.documentElement.lang==='en');
}
async function checkSources(browser, registrations) {
  // Checks 1–6: immutable primitives, family-owned additions, inline geometry,
  // shared components, shared busy ownership, shared navigation/transport.
  for(const file of ['tokens','components'])assert.equal(fs.readFileSync('src/Bingo.Web/wwwroot/css/admin-design-'+file+'.css','utf8'),fs.readFileSync('docs/references/admin-ui/ui/'+file+'.css','utf8'),'frozen '+file+' byte parity');
  const page=await browser.newPage(),designChecks=[];
  const shared=fs.readFileSync('src/Bingo.Web/Resources/SharedResource.da.resx','utf8'),community=fs.readFileSync('src/Bingo.Web/Resources/AdminCommunityResource.da.resx','utf8');
  const resources=await page.evaluate(({shared,community})=>Object.fromEntries(Object.entries({T:shared,D:community}).map(([alias,xml])=>[alias,[...new DOMParser().parseFromString(xml,'text/xml').querySelectorAll('data')].filter(e=>e.querySelector('value')?.textContent.trim()).map(e=>e.getAttribute('name'))])),{shared,community});
  try { for(const {family,source:pageSource,module} of registrations) {
    const source=fs.readFileSync('src/Bingo.Web/wwwroot/css/admin-design-'+family+'.css','utf8');
    const bad=await page.evaluate(({source,family})=>{
      const style=document.createElement('style');style.textContent=source;document.head.append(style);const bad=[];
      const prefix=':where([data-page-family="'+family+'"])';
      const split=value=>{let depth=0,start=0,parts=[];for(let i=0;i<value.length;i++){if('(['.includes(value[i]))depth++;if(')]'.includes(value[i]))depth--;if(value[i]===','&&depth===0){parts.push(value.slice(start,i).trim());start=i+1;}}parts.push(value.slice(start).trim());return parts;};
      const walk=rules=>{for(const rule of rules){if(rule.selectorText){for(const selector of split(rule.selectorText))if(!selector.startsWith(prefix))bad.push(selector);}else if(rule.cssRules)walk(rule.cssRules);else bad.push(rule.cssText);}};
      walk(style.sheet.cssRules);style.remove();return bad;
    },{source,family});assert.deepEqual(bad,[],family+': scoped styles');
    assert.doesNotMatch(source, /#[0-9a-fA-F]{3,8}\b|rgba?\(|hsla?\(|font-family|box-shadow|border-radius:\s*[0-9]/,family+': shared visual tokens');
    const markup=fs.readFileSync(path.join('src/Bingo.Web',pageSource),'utf8'),moduleSource=fs.readFileSync('src/Bingo.Web/wwwroot/js/'+module,'utf8');
    const keys=[...markup.matchAll(/\b([TD])\[\"([^\"]+)\"/g)];
    assert.deepEqual(keys.filter(([,alias,key])=>!resources[alias].includes(key)).map(([,alias,key])=>alias+':'+key),[],family+': literal Danish resources');
    assert.match(markup,/class=\"(?:card|.*\bcard\b)|<partial /,family+': shared components');
    assert.ok(/export (?:async )?function init\(/.test(moduleSource),family+': exported init');assert.ok(/export (?:async )?function dispose\(/.test(moduleSource),family+': exported dispose');
    if(family==='accounts')checkAccountSaveTiming(moduleSource);
    else if(/ui\.busy\(/.test(moduleSource))assert.doesNotMatch(moduleSource,/setTimeout\([^;]*(?:600|250)/,'saves use shared busy timing');
    designChecks.push({family,frozen:true,scopedRules:true,sharedTokens:true,inlineGeometryOccurrences:(markup.match(/style=/g)||[]).length,literalDanishKeys:keys.length,sharedComponents:true,sharedBusy: /ui\.busy\(/.test(moduleSource)?'used':'no save in this module',sharedLifecycle:true});
  }} finally {await page.close();}
  return designChecks;
}
module.exports={observe,checkFrames,checkLoaded,checkUpdate,checkDanish,checkSources};

async function checkDocument(page, registration, width) {
  await page.setViewportSize({width,height:342});
  await page.evaluate(()=>new Promise(requestAnimationFrame));
  const result=await page.evaluate(()=>{
    const main=document.querySelector('main.scroller');main.scrollTop=main.scrollHeight;window.scrollTo(9999,9999);
    return {width:document.documentElement.scrollWidth,height:document.documentElement.scrollHeight,x:scrollX,y:scrollY,contentScrollable:main.scrollHeight>main.clientHeight,contentScrolled:main.scrollTop>0};
  });
  assert.deepEqual({width:result.width,height:result.height,x:result.x,y:result.y},{width,height:342,x:0,y:0},registration.family+': short viewport document bounds');
  if(result.contentScrollable)assert.equal(result.contentScrolled,true,'content scrolls inside main');
  await page.evaluate(()=>document.querySelector('main.scroller').scrollTop=0);
  await page.setViewportSize({width,height:1000});
}
async function checkFast(page, registration, paths, html) {
  const source=registration.family==='dashboard'?'events':'dashboard';
  await page.evaluate(url=>AdminUI.navigate(url),paths[source]);
  await page.evaluate(({html,url})=>{
    window.fastFetch=fetch;window.fastSkeletons=0;window.fastDone=false;
    window.fastObserver=new MutationObserver(records=>{for(const r of records)for(const e of r.addedNodes)if(e.nodeType===1&&(e.matches('[data-page-skeleton]')||e.querySelector('[data-page-skeleton]')))fastSkeletons++;});
    fastObserver.observe(document.querySelector('[data-page-region]'),{childList:true,subtree:true});
    window.fetch=()=>new Promise(resolve=>window.fastFulfill=()=>resolve(new Response(html,{headers:{'Content-Type':'text/html'}})));
    void AdminUI.navigate(url).then(()=>fastDone=true);
  },{html,url:paths[registration.family]+'?conformanceFast=1'});
  for(let i=0;i<200;i++)if(await page.evaluate(()=>!!window.fastFulfill))break;
  await page.waitForFunction(family=>[...document.querySelectorAll('head link[data-admin-page-style]')].some(l=>l.href.includes('admin-design-'+family+'.')&&l.sheet),registration.family);
  await page.clock.runFor(149);assert.equal(await page.locator('[data-page-skeleton]').count(),0);
  await page.evaluate(()=>fastFulfill());
  for(let i=0;i<200;i++)if(await page.evaluate(()=>fastDone))break;
  assert.equal(await page.evaluate(()=>fastDone),true,'fast navigation finishes before skeleton threshold');
  await checkFrames(page);
  assert.equal(await page.evaluate(()=>fastSkeletons),0,registration.family+': no transient skeleton on fast load');
  await page.evaluate(()=>{window.fetch=fastFetch;fastObserver.disconnect();});
}
module.exports.checkDocument=checkDocument;
module.exports.checkFast=checkFast;

// Optional page declaration: fixed count words survive loading; only numbers are skeletons.
async function checkLoadingSummary(page, registration) {
  const summary=page.locator('[data-page-skeleton][aria-busy="true"] > .page-head .summary');
  if (registration.fixedSummary) {
    const words=(await page.locator('html').getAttribute('lang'))==='da'?registration.fixedSummary.wordsDa:registration.fixedSummary.words;
    const actual=await summary.locator(':scope > span').allTextContents();
    assert.equal(actual.length,words.length,registration.family+': fixed summary items');
    words.forEach((word,i)=>word instanceof RegExp?assert.match(actual[i].trim(),word):assert.equal(actual[i].trim(),word));
    assert.equal(await summary.locator('.sk').count(),0,registration.family+': fixed text does not need number placeholders');
    return;
  }
  if (!registration.countSummary) {
    assert.equal((await summary.textContent()).trim(), '', registration.family+': data summary stays empty while loading');
    assert.equal(await summary.locator('.sk').count(),0,registration.family+': no invented summary data');
    return;
  }
  const words=(await page.locator('html').getAttribute('lang'))==='da'?registration.countSummary.wordsDa:registration.countSummary.words;
  assert.deepEqual(await summary.locator(':scope > span').allTextContents().then(values=>values.map(text=>text.trim())),words,registration.family+': fixed count-summary words');
  const bars=summary.locator('.tab-count[data-pending-count] > .sk');
  assert.equal(await bars.count(),words.length,registration.family+': same numeric placeholders as tabs');
  assert.equal(await summary.locator('button,.summary-btn,b').count(),0,registration.family+': no data-dependent attention or numbers before response');
  const boxes=await summary.locator('.tab-count[data-pending-count]').evaluateAll(nodes=>nodes.map(e=>{const c=getComputedStyle(e),b=e.querySelector('.sk').getBoundingClientRect();return {display:c.display,width:e.getBoundingClientRect().width,barWidth:b.width,barHeight:b.height,text:e.textContent.trim()};}));
  assert.ok(boxes.every(b=>b.display==='inline-flex'&&b.width>0&&Math.abs(b.barWidth-b.width)<0.1&&b.barHeight===10&&b.text===''),registration.family+': number-sized bars');
}
module.exports.checkLoadingSummary=checkLoadingSummary;

// Registered routes are the shell's current page boundary. Query/culture and GUID
// values do not change that family. Same-page query/drawer actions retain their
// own handlers (user ruling7 October2026); different event IDs are different pages.
async function checkRegisteredLinks(page, paths) {
  const missing=await page.evaluate(paths=>{
    const route=pathname=>pathname.toLowerCase().replace(/\/[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}(?=\/|$)/g,'/:id').replace(/\/index\/?$/,'').replace(/\/$/,'');
    const canonical=pathname=>pathname.toLowerCase().replace(/\/index\/?$/,'').replace(/\/$/,'');
    const registered=new Set(Object.values(paths).map(p=>route(new URL(p,location.href).pathname)));
    return [...document.querySelectorAll('a[href]')].filter(a=>!a.getAttribute('href').startsWith('#')).filter(a=>{const u=new URL(a.href);return u.origin===location.origin&&canonical(u.pathname)!==canonical(location.pathname)&&registered.has(route(u.pathname))&&!a.hasAttribute('data-shell-link');}).map(a=>({text:a.textContent.trim(),href:a.getAttribute('href')}));
  },paths);
  assert.deepEqual(missing,[],'same-origin links to a different registered page use shell navigation');
}
module.exports.checkRegisteredLinks=checkRegisteredLinks;

// Accounts' search debounce is not a save. Its three direct POST transports must
// remain inside the shared busy boundary; query/copy timers are unrelated.
function checkAccountSaveTiming(source) {
  const writes=source.split('\n').filter(line=>/AdminFetch\.request\(/.test(line)&&/method:\s*['"]POST['"]/.test(line));
  assert.equal(writes.length,3,'Accounts: confirmation, reset-link and transfer save paths declared');
  for(const write of writes){
    assert.match(write,/await ui\.busy\(\(\) => window\.AdminFetch\.request\(/,'Accounts save transport uses shared busy timing');
    assert.doesNotMatch(write,/setTimeout\(/,'Accounts save has no local busy timer');
  }
}
module.exports.checkAccountSaveTiming=checkAccountSaveTiming;

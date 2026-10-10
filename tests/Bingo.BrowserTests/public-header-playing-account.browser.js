// Public header "Playing as": bar vs strip at the hamburger breakpoint, geometry, truncation and the popover heading.
// Real site.public-ui.css and fonts against synthetic header markup that mirrors _Layout.cshtml and _PlayingAccount.cshtml (no app or database needed).
const assert=require('node:assert/strict'),fs=require('node:fs'),path=require('node:path');
const {chromium}=require('playwright');
const root=path.resolve(__dirname,'../../src/Bingo.Web/wwwroot');
const cssFiles=['lib/bootstrap/dist/css/bootstrap.min.css','css/site.transitional.foundation.css','css/site.public-ui.css','css/site.transitional.application.css'];
// Link overflow (links.scrollWidth - links.clientWidth) of the admin link set with a current event, without the element, at commit 12b5e855.
const OLD_ADMIN_OVERFLOW={en:{761:23,800:0},da:{761:53,800:25}};
const LINKS={en:['Events','Account','Current event','How to','Admin'],da:['Bingoer','Konto','Current event','Sådan gør du','Admin']};
const LABEL={en:'Playing as',da:'Spiller som'};
const link=t=>`<a class="landing-shell-link" href="#">${t}</a>`;
const playing=(variant,text,two)=>`<div class="landing-shell-playing landing-shell-playing--${variant}" data-playing-account>`+(two
 ?`<div class="landing-shell-playing__menu" data-public-ui-popover><button type="button" class="landing-shell-playing__button" aria-expanded="false" aria-controls="p-${variant}" title="${text}"><span>${text}</span><svg aria-hidden="true" focusable="false" viewBox="0 0 24 24"><path d="m6 9 6 6 6-6" /></svg></button><div id="p-${variant}" class="landing-shell-playing__panel" data-public-ui-popover-panel hidden><strong class="public-ui-header-popover__heading public-ui-section-heading">Switch account</strong><form class="landing-shell-playing__form"><button type="submit" class="landing-shell-playing__option">Other</button></form></div></div>`
 :`<span class="landing-shell-playing__label" title="${text}">${text}</span>`)+'</div>';
const page=({lang,name,two,withElement=true,theme='light',bodyClass='public-ui-page-canvas'})=>{const text=`${LABEL[lang]} ${name}`;return `<!doctype html><html lang="${lang}" data-public-theme="${theme}"><head><meta charset="utf-8"><meta name="viewport" content="width=device-width, initial-scale=1">${cssFiles.map(f=>`<link rel="stylesheet" href="/${f}">`).join('')}</head>
<body class="${bodyClass}"><header class="landing-shell-header"><nav class="landing-shell-nav"><div class="landing-shell-primary">
<a class="landing-shell-brand" href="#"><span class="landing-shell-brand-mark" style="display:block"></span></a>
<div class="landing-shell-links">${LINKS[lang].map(link).join('')}</div>
${withElement?playing('bar',text,two):''}
<div class="landing-shell-actions"><div class="landing-shell-menu" data-public-ui-popover><button type="button" class="landing-shell-icon" aria-label="Open navigation"><svg viewBox="0 0 24 24"><path d="M4 7h16M4 12h16M4 17h16"/></svg></button></div>
<div class="public-ui-header-popover landing-shell-popover" data-notification-inbox><div data-public-ui-popover><button type="button" class="landing-shell-icon" aria-label="Notifications"><svg viewBox="0 0 24 24"><path d="M6 8a6 6 0 0 1 12 0c0 7 3 9 3 9H3s3-2 3-9"/></svg></button></div></div>
<div class="public-ui-header-popover landing-shell-popover"><button type="button" class="landing-shell-account"><span>d2admin</span><svg viewBox="0 0 24 24"><path d="m6 9 6 6 6-6"/></svg></button></div></div>
</div></nav>${withElement?playing('strip',text,two):''}</header><main style="min-height:300px"></main></body></html>`;};
const serve=async(pageObj,html)=>{await pageObj.route('http://header.test/**',route=>{const u=new URL(route.request().url());if(u.pathname==='/')return route.fulfill({contentType:'text/html',body:html.current});const f=path.join(root,decodeURIComponent(u.pathname));return fs.existsSync(f)?route.fulfill({path:f}):route.fulfill({status:404,body:''});});};
const measure=()=>{const r=e=>{const b=e.getBoundingClientRect();return{l:b.left,r:b.right,w:b.width,shown:getComputedStyle(e).display!=='none'&&b.width>0};};
 const q=s=>document.querySelector(s),links=q('.landing-shell-links');
 return{vw:document.documentElement.clientWidth,docOver:document.documentElement.scrollWidth-document.documentElement.clientWidth,linksOver:links.scrollWidth-links.clientWidth,
  bar:q('.landing-shell-playing--bar')&&r(q('.landing-shell-playing--bar')),strip:q('.landing-shell-playing--strip')&&r(q('.landing-shell-playing--strip')),
  bell:r(q('[data-notification-inbox] button')),account:r(q('.landing-shell-account')),actions:r(q('.landing-shell-actions')),hamb:r(q('.landing-shell-menu')),
  primaryPad:parseFloat(getComputedStyle(q('.landing-shell-primary')).paddingRight),
  title:(q('.landing-shell-playing--bar button, .landing-shell-playing--bar .landing-shell-playing__label')||{}).title,barTruncated:(()=>{const s=q('.landing-shell-playing--bar span');return s?s.scrollWidth>s.clientWidth:null;})()};};
const luminance=c=>{const [r,g,b]=c.match(/[\d.]+/g).slice(0,3).map(Number).map(v=>{v/=255;return v<=0.03928?v/12.92:((v+0.055)/1.055)**2.4;});return 0.2126*r+0.7152*g+0.0722*b;};
const contrast=(a,b)=>{const [x,y]=[luminance(a),luminance(b)].sort((m,n)=>n-m);return (x+0.05)/(y+0.05);};
(async()=>{const browser=await chromium.launch({headless:true});const checks=[];const shots=process.env.HEADER_SHOTS;
 try{
  const context=await browser.newContext({viewport:{width:1280,height:700}});const p=await context.newPage();const html={current:''};await serve(p,html);
  const load=async(opts,w)=>{html.current=page(opts);await p.setViewportSize({width:w,height:700});await p.goto('http://header.test/');await p.evaluate(()=>document.fonts.ready);};
  for(const lang of ['en','da'])for(const two of [false,true]){
   const name='Averyveryverylongaccountname9999';
   for(const w of [1920,1280,1100,900,800,761,760,390]){
    await load({lang,name,two},w);const m=await p.evaluate(measure);const tag=`${lang} ${two?'menu':'label'} ${w}px`;
    if(w>=761){assert.ok(m.bar.shown,tag+': bar element visible');assert.equal(m.strip.shown,false,tag+': strip hidden');assert.ok(m.bar.r<=m.account.l+0.5||m.bar.r<=m.actions.l+0.5,tag+': bar clear of the actions');assert.ok(m.bar.l>=0,tag+': bar on screen');}
    else{assert.equal(m.bar.shown,false,tag+': bar hidden');assert.ok(m.strip.shown,tag+': strip visible');assert.ok(m.strip.r<=m.vw+0.5,tag+': strip inside viewport');}
    for(const k of ['bell','account','hamb'])if(k!=='hamb'||w<=760){assert.ok(m[k].l>=0&&m[k].r<=m.vw+0.5,`${tag}: ${k} inside viewport (${JSON.stringify(m[k])})`);}
    assert.ok(m.actions.r<=m.vw+0.5&&m.actions.l>=0,tag+': actions inside viewport');
    assert.equal(m.docOver<=0,true,tag+': no horizontal page scroll');
    if(w<=760)assert.ok(Math.abs(m.actions.r-(m.vw-m.primaryPad))<=1,`${tag}: actions right-aligned (right ${m.actions.r}, expected ${m.vw-m.primaryPad})`);
    assert.equal(m.title,`${LABEL[lang]} ${name}`,tag+': title holds the full name');
    if(w===761&&shots)await p.screenshot({path:path.join(shots,`header-${lang}-${two?'menu':'label'}-761.png`),clip:{x:0,y:0,width:w,height:140}});
    if(w===390&&shots)await p.screenshot({path:path.join(shots,`header-${lang}-${two?'menu':'label'}-390.png`),clip:{x:0,y:0,width:w,height:140}});
   }
   checks.push(`${lang} ${two?'menu':'label'}: bar >=761, strip <=760, controls on screen, actions right-aligned <=760, title full name`);
  }
  // Admin link overflow not worse than at 12b5e855 (same link set, element absent there).
  for(const lang of ['en','da'])for(const w of [761,800]){await load({lang,name:'Zezima',two:false},w);const m=await p.evaluate(measure);
   assert.ok(Math.max(0,m.linksOver)<=OLD_ADMIN_OVERFLOW[lang][w],`${lang} ${w}px: link overflow ${m.linksOver} must be <= ${OLD_ADMIN_OVERFLOW[lang][w]}`);}
  checks.push('admin link overflow at 761 and 800 not worse than 12b5e855');
  // "Switch account" heading readable on every page type and theme.
  for(const theme of ['light','dark'])for(const bodyClass of ['public-ui-page-canvas','public-ui-page-canvas public-ui-pass1','public-ui-page-canvas public-ui-editorial-landing'])for(const w of [1280,390]){
   await load({lang:'en',name:'Ur admin 2',two:true,theme,bodyClass},w);const bar=w>=761?'bar':'strip';
   await p.evaluate(v=>{document.querySelector(`.landing-shell-playing--${v} [data-public-ui-popover-panel]`).hidden=false;},bar);
   const c=await p.evaluate(v=>{const panel=document.querySelector(`.landing-shell-playing--${v} .landing-shell-playing__panel`),h=panel.querySelector('.public-ui-section-heading');return{fg:getComputedStyle(h).color,bg:getComputedStyle(panel).backgroundColor,size:parseFloat(getComputedStyle(h).fontSize)};},bar);
   assert.ok(contrast(c.fg,c.bg)>=4.5,`${theme}/${bodyClass}/${w}px: heading ${c.fg} on ${c.bg} contrast ${contrast(c.fg,c.bg).toFixed(2)}`);
   if(shots&&w===1280)await p.screenshot({path:path.join(shots,`switch-heading-${theme}-${bodyClass.replace(/public-ui-page-canvas ?/,'')||'canvas'}.png`),clip:{x:0,y:0,width:w,height:260}});
  }
  checks.push('Switch account heading contrast >= 4.5 on canvas/pass1/landing bodies, light and dark, bar and strip');
  // The popover must look the same on an event page (event shell) and on every other public page: heading and panel computed styles are identical.
  for(const theme of ['light','dark'])for(const w of [1280,390]){
   const bar=w>=761?'bar':'strip';const seen={};
   for(const bodyClass of ['public-ui-page-canvas','public-ui-page-canvas public-ui-editorial-landing','public-ui-page-canvas public-event-shell public-ui-pass1']){
    await load({lang:'en',name:'Ur admin 2',two:true,theme,bodyClass},w);
    await p.evaluate(v=>{document.querySelector(`.landing-shell-playing--${v} [data-public-ui-popover-panel]`).hidden=false;},bar);
    seen[bodyClass]=await p.evaluate(v=>{const panel=document.querySelector(`.landing-shell-playing--${v} .landing-shell-playing__panel`),h=panel.querySelector('.public-ui-section-heading'),o=panel.querySelector('.landing-shell-playing__option');
     const pick=(e,ks)=>Object.fromEntries(ks.map(k=>[k,getComputedStyle(e)[k]]));
     return{heading:pick(h,['color','fontFamily','fontSize','fontWeight','lineHeight','letterSpacing','textTransform','paddingTop','paddingRight','paddingBottom','paddingLeft','borderBottomWidth','borderBottomStyle','borderBottomColor']),
      panel:pick(panel,['width','paddingTop','paddingLeft','color','backgroundColor','borderTopColor','borderTopWidth','boxShadow']),option:pick(o,['color','fontFamily','fontSize','paddingTop','borderBottomColor'])};},bar);
   }
   const ref=seen['public-ui-page-canvas public-event-shell public-ui-pass1'];
   assert.notEqual(ref.heading.borderBottomWidth,'0px',`${theme}/${w}px: heading is underlined`);
   assert.ok(ref.heading.fontFamily.includes('Barlow Condensed ExtraBold'),`${theme}/${w}px: heading font`);
   for(const [bc,v] of Object.entries(seen))assert.deepEqual(v,ref,`${theme}/${w}px: popover on "${bc}" differs from the event shell`);
  }
  checks.push('Switch account popover (heading colour, underline, font, spacing, panel) identical on canvas, landing and event-shell bodies, light and dark, bar and strip');
 }finally{await browser.close();}
 console.log(checks.join('\n'));
})().catch(e=>{console.error(e);process.exit(1);});

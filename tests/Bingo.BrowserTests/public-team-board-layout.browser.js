// Public team board layout: real site.public-ui.css against a synthetic board (no app or database needed).
const assert=require('node:assert/strict'),fs=require('node:fs'),path=require('node:path');
const {chromium,webkit}=require('playwright');
const sheets=['lib/bootstrap/dist/css/bootstrap.min.css','css/site.transitional.foundation.css','css/site.public-ui.css','css/site.transitional.application.css'].map(f=>fs.readFileSync(path.resolve(__dirname,'../../src/Bingo.Web/wwwroot',f),'utf8'));
const tile=(i,points)=>`<a class="public-tile public-ui-nested-container public-ui-team-board-tile public-ui-team-board-tile--in-progress" href="#">
 <span class="public-ui-team-board-tile__number" aria-hidden="true">${String(i+1).padStart(2,'0')}</span>
 <span class="public-ui-team-board-tile__shade" aria-hidden="true"></span>
 <span class="public-ui-team-board-tile__copy"><span class="public-ui-team-board-tile__position">Points: ${points}</span><strong class="public-ui-team-board-tile__title">Tile ${i+1}</strong>
 <span class="public-ui-team-board-tile__progress-row"><span>1/3</span></span></span></a>`;
const html=`<div class="public-team-board" style="--public-board-columns:5"><div style="max-width:80rem;margin:0 auto;padding:0 1rem"><div class="public-team-workspace">
 <div class="public-team-board-legend"><span class="public-board-legend__item">Completed</span></div>
 <aside class="public-team-sidebar public-ui-sectioned-surface"><section class="team-sidebar-section"><h2 class="team-rail-heading">Team progress</h2><p class="team-rail-supporting">Supporting text</p>
  <dl class="team-metric-list"><div class="team-metric-row"><dt>Team total</dt><dd class="team-metric-value">+1.00</dd></div></dl>
  <details class="team-contributor-block" open><summary class="team-rail-subheading">Contributors</summary><ol class="team-contributor-list"><li><span class="team-contributor-rank">1</span><span class="team-contributor-name">Player</span><span class="team-contributor-value">+1</span></li></ol></details></section></aside>
 <div class="public-board-scroll"><div class="public-full-board">${Array.from({length:25},(_,i)=>tile(i,[1,12,25,48,100][i%5])).join('')}</div></div>
</div><nav class="public-team-switcher"><a href="#"><small>Previous team</small><strong>← #4 Team</strong></a><a class="next" href="#"><small>Next team</small><strong>#6 Team →</strong></a></nav></div></div>`;
const measure=()=>{
 const rect=e=>e.getBoundingClientRect();
 let overlap=0;for(const t of document.querySelectorAll('.public-ui-team-board-tile')){const a=rect(t.querySelector('.public-ui-team-board-tile__position')),n=rect(t.querySelector('.public-ui-team-board-tile__number')),r=rect(t);const range=document.createRange();range.selectNodeContents(t.querySelector('.public-ui-team-board-tile__position'));const text=range.getBoundingClientRect();if(a.top<n.bottom&&n.top<a.bottom)overlap=Math.max(overlap,text.right-n.left);if(a.right>r.right)overlap=Math.max(overlap,a.right-r.right);}
 const sw=rect(document.querySelector('.public-team-switcher'));window.scrollTo(0,document.querySelector('.public-full-board').getBoundingClientRect().top+scrollY);
 const scrolledBoard=rect(document.querySelector('.public-full-board'));
 const ws=document.querySelector('.public-team-workspace'),cols=getComputedStyle(ws).gridTemplateColumns.split(' ').map(parseFloat),gap=parseFloat(getComputedStyle(ws).columnGap);
 const box=s=>{const e=document.querySelector(s),b=e.getBoundingClientRect();return{x:b.x,y:b.y,w:b.width,h:b.height}};
 const size=(s,p)=>parseFloat(getComputedStyle(document.querySelector(s))[p]);
 return{switcher:{l:sw.left,r:sw.right},column:cols[1],overlap,scrolledBottom:scrolledBoard.bottom,innerH:innerHeight,sidebar:box('.public-team-sidebar'),board:box('.public-full-board'),legend:box('.public-team-board-legend'),tile:box('.public-ui-team-board-tile'),
  scrollW:document.documentElement.scrollWidth,innerW:innerWidth,
  numTop:size('.public-ui-team-board-tile__number','top'),numRight:size('.public-ui-team-board-tile__number','right'),numFont:size('.public-ui-team-board-tile__number','fontSize'),pointsWeight:size('.public-ui-team-board-tile__position','fontWeight'),
  points:size('.public-ui-team-board-tile__position','fontSize'),title:size('.public-ui-team-board-tile__title','fontSize'),
  heading:size('.team-rail-heading','fontSize'),supporting:size('.team-rail-supporting','fontSize'),metric:size('.team-metric-value','fontSize'),
  contributor:size('.team-contributor-name','fontSize'),rank:size('.team-contributor-rank','fontSize')};
};

// Selected-tile sidebar: the real class structure of _TileSidebar/_TileActivity with long names, every section and the expanded disclosures.
const longName='Averyveryveryverylongplayername_with_underscores_and_more';
const dropRow=n=>`<div class="tile-context-sidebar__drop-row"><strong>${n}</strong><span>1/1500</span><small>×2</small></div>`;
const evidence=(n,manual)=>manual?`<article class="tile-context-sidebar__evidence-row public-ui-evidence-gallery__cell public-ui-evidence-gallery__cell--manual"><span class="tile-context-sidebar__evidence-mark public-ui-state public-ui-state--success" aria-hidden="true">✓</span><span class="tile-context-sidebar__evidence-copy"><strong>${n}</strong><small>Manual completion</small><small>Manual completion · <time>3 days ago</time></small></span></article>`
 :`<button type="button" class="tile-context-sidebar__evidence-row public-ui-evidence-gallery__cell"><img alt="" src="data:image/gif;base64,R0lGODlhAQABAIAAAAAAAP///yH5BAEAAAAALAAAAAABAAEAAAIBRAA7"/><span class="tile-context-sidebar__evidence-copy"><strong>${n}</strong><small>Voidwaker hilt of the very long item name</small><small>Araxxor · <time>3 days ago</time></small></span></button>`;
const tileHtml=`<div class="public-team-board"><div class="public-team-workspace"><aside class="public-team-sidebar public-ui-surface public-ui-surface--charcoal public-ui-sectioned-surface tile-context-sidebar public-ui-auto-hide-scrollbar" data-tile-context-sidebar><div class="tile-context-sidebar__content">
 <section class="tile-context-sidebar__section tile-context-sidebar__actions"><div class="public-ui-action-list public-ui-action-list--split"><a class="public-ui-action public-ui-action--compact tile-context-sidebar__action--overview" href="#">Team overview</a><a class="public-ui-action public-ui-action--commit public-ui-action--compact" href="#">Submit drop</a></div></section>
 <section class="tile-context-sidebar__section tile-context-sidebar__progress"><header class="public-ui-component-header"><div class="public-ui-component-header__copy"><span class="public-ui-overline">Tile 06</span><h2 class="public-ui-component-title">Phosani's Nightmare and a very long tile name</h2><p class="public-ui-supporting-text">Collect a Voidwaker hilt; Collect a Voidwaker blade; Collect a Voidwaker gem</p></div></header><div class="tile-context-sidebar__progress-summary"><strong class="public-ui-data-value">100/100</strong><span class="public-ui-data-label">Drops</span></div></section>
 <section class="tile-context-sidebar__section tile-context-sidebar__breakdown"><header class="tile-context-sidebar__section-header"><h3 class="public-ui-section-heading">Completion breakdown</h3></header><div class="tile-context-sidebar__breakdown-list"><div class="tile-context-sidebar__breakdown-row"><span>Collect a Voidwaker hilt of the very long item name</span><span class="tile-context-sidebar__breakdown-value tile-context-sidebar__status--complete"><strong>3 OF 3</strong><small>approved</small><span class="public-ui-state public-ui-state--success">✓</span></span></div></div></section>
 <section class="tile-context-sidebar__section tile-context-sidebar__eligible"><header class="tile-context-sidebar__section-header"><h3 class="public-ui-section-heading">Eligible drops</h3><small class="public-ui-supporting-text">6</small></header>
  <div class="tile-context-sidebar__drop-groups"><details class="tile-context-sidebar__drop-disclosure" open><summary><span>Calvar'ion and Vet'ion with a long boss name</span><span class="tile-context-sidebar__drop-meta">2 drops <svg viewBox="0 0 24 24"><path d="m6 9 6 6 6-6"/></svg></span></summary><div class="tile-context-sidebar__drop-rows">${dropRow('Voidwaker hilt of the very long item name')}${dropRow('Short')}</div></details></div>
  <details class="tile-context-sidebar__drop-disclosure tile-context-sidebar__drop-disclosure--overflow" open><summary><span class="tile-context-sidebar__drop-summary-label tile-context-sidebar__drop-summary-label--closed">View all 6</span><span class="tile-context-sidebar__drop-summary-label tile-context-sidebar__drop-summary-label--open">Show less</span><span class="tile-context-sidebar__drop-meta"><svg viewBox="0 0 24 24"><path d="m6 9 6 6 6-6"/></svg></span></summary><div class="tile-context-sidebar__drop-rows">${dropRow('Another long drop name here')}</div></details></section>
 <section class="tile-context-sidebar__section tile-kc-section" data-tile-activity><h2 class="team-rail-heading">KC &amp; Luck</h2><p class="team-rail-supporting">Full-event KC · Drops credited to this tile</p><p class="tile-kc-stale-notice">Last available result · <time>9 Oct 12:00</time></p>
  <dl class="team-metric-list"><div class="team-metric-row"><dt>Team total</dt><dd class="team-metric-value tile-kc-negative">42.5%</dd><dd class="team-metric-value"><span>+1,234</span></dd></div></dl><h3 class="team-rail-subheading tile-kc-metric-heading">Phosani's Nightmare</h3>
  <p class="team-rail-supporting tile-kc-status">Waiting for activity data</p>
  <details class="team-contributor-block" open><summary class="team-rail-subheading"><span>Contributors</span></summary><ul class="team-contributor-list"><li><span class="team-contributor-rank">61.2%</span><span class="team-contributor-name">${longName}</span><strong class="team-contributor-value">+12</strong><small class="tile-kc-player-status">Waiting for activity update</small></li></ul></details></section>
 <section class="tile-context-sidebar__section tile-context-sidebar__evidence public-ui-evidence-gallery-section"><header class="tile-context-sidebar__section-header"><h3 class="public-ui-section-heading">Approved submissions</h3><small class="public-ui-supporting-text">2</small></header><div class="public-ui-evidence-gallery">${evidence(longName,false)}${evidence('Manual player',true)}</div></section>
 <section class="tile-context-sidebar__section tile-context-sidebar__evidence"><div class="tile-context-sidebar__empty"><strong>No approved evidence yet</strong><span class="tile-context-sidebar__empty-support">Approved submissions will appear here.</span></div></section>
</div></aside></div></div>`;
const measureTile=()=>{
 const sb=document.querySelector('[data-tile-context-sidebar]'),sizes=[],walk=document.createTreeWalker(sb,NodeFilter.SHOW_TEXT);
 while(walk.nextNode()){const n=walk.currentNode,e=n.parentElement;if(!n.textContent.trim()||getComputedStyle(e).display==='none'||!e.getBoundingClientRect().width)continue;sizes.push([parseFloat(getComputedStyle(e).fontSize),n.textContent.trim().slice(0,24)]);}
 const content=sb.querySelector('.tile-context-sidebar__content'),over=[...sb.querySelectorAll('.tile-context-sidebar__content *')].filter(e=>e.getBoundingClientRect().right>sb.getBoundingClientRect().right+0.5&&!e.closest('svg')).length;
 const dn=[...sb.querySelectorAll('.tile-context-sidebar__evidence-copy small')].find(e=>e.textContent.includes('very long item name')),dbg=dn&&[getComputedStyle(dn).textOverflow,dn.scrollWidth,dn.clientWidth,dn.scrollHeight,dn.clientHeight,getComputedStyle(dn).display],cut=!dn||getComputedStyle(dn).textOverflow==='ellipsis'||dn.scrollWidth>dn.clientWidth+0.5||dn.scrollHeight>dn.clientHeight+0.5,rowH=dn&&dn.closest('.tile-context-sidebar__evidence-row').getBoundingClientRect().height,imgH=dn&&dn.closest('.tile-context-sidebar__evidence-row').querySelector('img').getBoundingClientRect().height;
 return{dbg,cut,rowH,imgH,sizes,min:Math.min(...sizes.map(x=>x[0])),minText:sizes.sort((a,b)=>a[0]-b[0])[0],scrollW:content.scrollWidth,clientW:content.clientWidth,over,width:sb.getBoundingClientRect().width};
};
(async()=>{
 for(const [name,engine] of [['chromium',chromium],['webkit',webkit]]){
  const browser=await engine.launch({headless:true,...(engine===chromium?{channel:process.env.PLAYWRIGHT_CHANNEL||'chromium'}:{})});
  try{
   const page=await browser.newPage();
   await page.route('http://bingo.test/**',route=>{const pathname=new URL(route.request().url()).pathname;if(pathname==='/')return route.fulfill({contentType:'text/html',body:'<!doctype html><html lang="en"><head><meta name="viewport" content="width=device-width"></head><body class="public-event-shell public-ui-pass1"></body></html>'});const file=path.resolve(__dirname,'../../src/Bingo.Web/wwwroot',pathname.slice(1));fs.existsSync(file)?route.fulfill({path:file}):route.abort()});
   await page.goto('http://bingo.test/');
   for(const content of sheets)await page.addStyleTag({content});
   await page.evaluate(h=>{document.body.innerHTML=h},html);
   await page.evaluate(()=>document.fonts.load('800 20px "Barlow Condensed ExtraBold"'));
   await page.evaluate(()=>document.fonts.load('800 20px "Barlow Condensed Public UI"'));
   await page.evaluate(()=>document.fonts.load('600 12px "Geist Public UI"'));
   const at=async(w,h)=>{await page.setViewportSize({width:w,height:h});return page.evaluate(measure)};
   const log=[];
   const fit=(m,w,h)=>{ // board top scrolled to the window top: the whole board is inside the window, and it is as large as width and height allow
    assert.ok(m.scrolledBottom<=h+0.5||h<640,`${name} ${w}x${h} board bottom ${m.scrolledBottom} inside the window`);
    assert.ok(m.board.w>=640-0.5&&Math.abs(m.board.w-m.board.h)<1.5,`${name} ${w}x${h} board ${m.board.w}x${m.board.h} square and not below today's 640`);
    const heightLimit=Math.max(640,h-48),columnLimit=m.column;
    assert.ok(Math.abs(m.board.w-Math.min(heightLimit,columnLimit))<1.5,`${name} ${w}x${h} board ${m.board.w} equals min(height limit ${heightLimit}, column ${columnLimit})`);
    assert.ok(m.scrollW<=m.innerW,`${name} ${w}x${h} no horizontal scroll`);
    assert.ok(Math.abs(m.numTop-4)<0.5&&Math.abs(m.numRight-5.6)<0.5,`${name} ${w}x${h} tile number sits in the top-right corner (top ${m.numTop}, right ${m.numRight})`);
    assert.ok(Math.abs(m.points-8.96)<0.05&&m.pointsWeight===400,`${name} ${w}x${h} Points label keeps the old 0.56rem regular size (${m.points}/${m.pointsWeight})`);
    assert.ok(m.tile.w<120||m.overlap<=0,`${name} ${w}x${h} Points label overlaps the tile number by ${m.overlap}px`);
    assert.ok(Math.abs(m.switcher.l-m.sidebar.x)<1.5&&Math.abs(m.switcher.r-(m.board.x+m.board.w))<1.5,`${name} ${w}x${h} team switcher row ${m.switcher.l}-${m.switcher.r} equals sidebar left ${m.sidebar.x} to board right ${m.board.x+m.board.w}`);
    log.push(`${w}x${h}: board ${Math.round(m.board.w)} tile ${Math.round(m.tile.w)} sidebar ${Math.round(m.sidebar.w)}`);
   };
   const wide=await at(1920,1080);fit(wide,1920,1080);
   assert.ok(wide.sidebar.w>=340&&wide.sidebar.w<=360,`${name} 1920 sidebar ${wide.sidebar.w}`);
   assert.ok(Math.abs(wide.legend.w-wide.board.w)<1&&Math.abs(wide.legend.x-wide.board.x)<1,`${name} legend follows the board`);
   assert.ok(wide.numFont>=60&&wide.title>=22&&wide.tile.w>=140,`${name} tile text scales with the tile (${wide.numFont}/${wide.title}/${wide.tile.w})`);
   assert.ok(wide.heading>=22.5&&wide.supporting>=15.5&&wide.metric>=14.4&&wide.contributor>=13.5&&wide.rank>=13.5,`${name} sidebar text sizes ${JSON.stringify([wide.heading,wide.supporting,wide.metric,wide.contributor,wide.rank])}`);
   fit(await at(1920,950),1920,950);
   fit(await at(1470,700),1470,700);
   const laptop=await at(1470,956);fit(laptop,1470,956);
   assert.ok(laptop.sidebar.w>=340&&laptop.sidebar.w<=360,`${name} 1470 sidebar ${laptop.sidebar.w}`);
   fit(await at(1366,768),1366,768);
   const w1100=await at(1100,800);assert.ok(Math.abs(w1100.switcher.l-w1100.sidebar.x)<1.5&&Math.abs(w1100.switcher.r-(w1100.board.x+w1100.board.w))<1.5,`${name} 1100 team switcher row ${w1100.switcher.l}-${w1100.switcher.r} vs sidebar ${w1100.sidebar.x} / board right ${w1100.board.x+w1100.board.w}`);
   const mid=await at(1000,800);
   assert.ok(mid.scrollW<=mid.innerW&&mid.board.w>=590&&mid.overlap<=6,`${name} 1000 wide: no page scroll, minimum board; the ~4px touch of 3-digit Points on 111px tiles was accepted by the user on 10 October 2026 and must stay within 6px (${mid.board.w}, ${mid.overlap})`);
   const phone=await at(390,844);
   assert.ok(phone.scrollW<=phone.innerW,`${name} 390 no horizontal page scroll`);
   assert.ok(Math.abs(phone.numTop-4)<0.5&&Math.abs(phone.numRight-5.6)<0.5&&Math.abs(phone.points-8.96)<0.05,`${name} 390 number in the corner, Points at the old size`);
   // 111px tiles (390 phone, <=1000 wide): 'Points: 100' reaches ~4px into the tile number; accepted by the user on 10 October 2026, bounded to 6px.
   assert.ok(phone.overlap<=6,`${name} 390 Points label overlaps the tile number by ${phone.overlap}px, more than the accepted 6px`);
   assert.ok(phone.switcher.l>=0&&phone.switcher.r<=phone.innerW+0.5,`${name} 390 team switcher stays inside the window`);
   assert.ok(phone.sidebar.y<phone.board.y&&Math.abs(phone.sidebar.x-phone.board.x)<1,`${name} 390 keeps the stacked order`);
   await page.evaluate(h=>{document.body.innerHTML=h},tileHtml);
   for(const [w,h] of [[1920,1080],[1470,700],[390,844]]){
    await page.setViewportSize({width:w,height:h});
    const t=await page.evaluate(measureTile);
    assert.ok(t.min>=13.5,`${name} ${w}x${h} tile sidebar text under 13.5px: ${t.minText}`);
    assert.ok(t.scrollW<=t.clientW&&t.over===0,`${name} ${w}x${h} tile sidebar no horizontal overflow (${t.scrollW}/${t.clientW}, ${t.over} elements past the edge)`);
    assert.ok(!t.cut,`${name} ${w}x${h} long evidence drop name is not truncated ${JSON.stringify(t.dbg)}`);
    assert.ok(t.rowH>=t.imgH,`${name} ${w}x${h} evidence row holds its thumbnail`);
    if(w>=1470)assert.ok(t.width>=340&&t.width<=360,`${name} ${w} tile sidebar width ${t.width}`);
   }
   console.log(name,log.join(' | '));
   console.log(`PASS ${name} public team board layout`);
  }finally{await browser.close();}
 }
})().catch(error=>{console.error(error);process.exitCode=1;});

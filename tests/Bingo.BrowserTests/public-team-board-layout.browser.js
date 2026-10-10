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
</div></div></div>`;
const measure=()=>{
 const rect=e=>e.getBoundingClientRect();
 let overlap=0;for(const t of document.querySelectorAll('.public-ui-team-board-tile')){const a=rect(t.querySelector('.public-ui-team-board-tile__position')),n=rect(t.querySelector('.public-ui-team-board-tile__number')),r=rect(t);const range=document.createRange();range.selectNodeContents(t.querySelector('.public-ui-team-board-tile__position'));const text=range.getBoundingClientRect();if(a.top<n.bottom&&n.top<a.bottom)overlap=Math.max(overlap,text.right-n.left);if(a.right>r.right)overlap=Math.max(overlap,a.right-r.right);}
 window.scrollTo(0,document.querySelector('.public-full-board').getBoundingClientRect().top+scrollY);
 const scrolledBoard=rect(document.querySelector('.public-full-board'));
 const ws=document.querySelector('.public-team-workspace'),cols=getComputedStyle(ws).gridTemplateColumns.split(' ').map(parseFloat),gap=parseFloat(getComputedStyle(ws).columnGap);
 const box=s=>{const e=document.querySelector(s),b=e.getBoundingClientRect();return{x:b.x,y:b.y,w:b.width,h:b.height}};
 const size=(s,p)=>parseFloat(getComputedStyle(document.querySelector(s))[p]);
 return{column:cols[1],overlap,scrolledBottom:scrolledBoard.bottom,innerH:innerHeight,sidebar:box('.public-team-sidebar'),board:box('.public-full-board'),legend:box('.public-team-board-legend'),tile:box('.public-ui-team-board-tile'),
  scrollW:document.documentElement.scrollWidth,innerW:innerWidth,
  points:size('.public-ui-team-board-tile__position','fontSize'),title:size('.public-ui-team-board-tile__title','fontSize'),
  heading:size('.team-rail-heading','fontSize'),supporting:size('.team-rail-supporting','fontSize'),metric:size('.team-metric-value','fontSize'),
  contributor:size('.team-contributor-name','fontSize'),rank:size('.team-contributor-rank','fontSize')};
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
    assert.ok(m.overlap<=0,`${name} ${w}x${h} Points label overlaps the tile number by ${m.overlap}px`);
    log.push(`${w}x${h}: board ${Math.round(m.board.w)} tile ${Math.round(m.tile.w)} sidebar ${Math.round(m.sidebar.w)}`);
   };
   const wide=await at(1920,1080);fit(wide,1920,1080);
   assert.ok(wide.sidebar.w>=340&&wide.sidebar.w<=360,`${name} 1920 sidebar ${wide.sidebar.w}`);
   assert.ok(Math.abs(wide.legend.w-wide.board.w)<1&&Math.abs(wide.legend.x-wide.board.x)<1,`${name} legend follows the board`);
   assert.ok(wide.points>=12.5&&wide.title>=22&&wide.tile.w>=140,`${name} tile text scales with the tile (${wide.points}/${wide.title}/${wide.tile.w})`);
   assert.ok(wide.heading>=22.5&&wide.supporting>=15.5&&wide.metric>=14.4&&wide.contributor>=13.5&&wide.rank>=13.5,`${name} sidebar text sizes ${JSON.stringify([wide.heading,wide.supporting,wide.metric,wide.contributor,wide.rank])}`);
   fit(await at(1920,950),1920,950);
   const laptop=await at(1470,956);fit(laptop,1470,956);
   assert.ok(laptop.sidebar.w>=340&&laptop.sidebar.w<=360,`${name} 1470 sidebar ${laptop.sidebar.w}`);
   fit(await at(1366,768),1366,768);
   const mid=await at(1000,800);
   assert.ok(mid.scrollW<=mid.innerW&&mid.board.w>=590&&mid.overlap<=0,`${name} 1000 wide: no page scroll, minimum board, no label overlap (${mid.board.w}, ${mid.overlap})`);
   const phone=await at(390,844);
   assert.ok(phone.scrollW<=phone.innerW,`${name} 390 no horizontal page scroll`);
   assert.ok(phone.overlap<=0,`${name} 390 Points label overlaps the tile number by ${phone.overlap}px`);
   assert.ok(phone.sidebar.y<phone.board.y&&Math.abs(phone.sidebar.x-phone.board.x)<1,`${name} 390 keeps the stacked order`);
   console.log(name,log.join(' | '));
   console.log(`PASS ${name} public team board layout`);
  }finally{await browser.close();}
 }
})().catch(error=>{console.error(error);process.exitCode=1;});

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
 <div class="public-board-scroll"><div class="public-full-board">${Array.from({length:25},(_,i)=>tile(i,[1,12,25,48,3][i%5])).join('')}</div></div>
</div></div></div>`;
const measure=()=>{
 const box=s=>{const e=document.querySelector(s),b=e.getBoundingClientRect();return{x:b.x,y:b.y,w:b.width,h:b.height}};
 const size=(s,p)=>parseFloat(getComputedStyle(document.querySelector(s))[p]);
 return{sidebar:box('.public-team-sidebar'),board:box('.public-full-board'),legend:box('.public-team-board-legend'),tile:box('.public-ui-team-board-tile'),
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
   await page.setContent('<!doctype html><html lang="en"><head><meta name="viewport" content="width=device-width"></head><body class="public-event-shell public-ui-pass1"></body></html>');
   for(const content of sheets)await page.addStyleTag({content});
   await page.evaluate(h=>{document.body.innerHTML=h},html);
   const at=async(w,h)=>{await page.setViewportSize({width:w,height:h});return page.evaluate(measure)};
   const wide=await at(1920,1080);
   assert.ok(wide.sidebar.w>=340&&wide.sidebar.w<=360,`${name} 1920 sidebar ${wide.sidebar.w}`);
   assert.ok(wide.board.w>=750&&Math.abs(wide.board.w-wide.board.h)<1.5,`${name} 1920 board ${wide.board.w}x${wide.board.h} is wider than 640 and square`);
   assert.ok(Math.abs(wide.legend.w-wide.board.w)<1&&Math.abs(wide.legend.x-wide.board.x)<1,`${name} legend follows the board`);
   assert.ok(wide.board.y+wide.board.h<=1080+60,`${name} board fits the viewport height (±the page header)`);
   assert.ok(wide.scrollW<=wide.innerW,`${name} 1920 no horizontal scroll`);
   assert.ok(wide.points>=12.5&&wide.title>=22&&wide.tile.w>=140,`${name} tile text scales with the tile (${wide.points}/${wide.title}/${wide.tile.w})`);
   assert.ok(wide.heading>=22.5&&wide.supporting>=15.5&&wide.metric>=14.4&&wide.contributor>=13.5&&wide.rank>=13.5,`${name} sidebar text sizes ${JSON.stringify([wide.heading,wide.supporting,wide.metric,wide.contributor,wide.rank])}`);
   const laptop=await at(1470,956);
   assert.ok(laptop.sidebar.w>=340&&laptop.sidebar.w<=360,`${name} 1470 sidebar ${laptop.sidebar.w}`);
   assert.ok(laptop.board.w>=640&&laptop.board.w<=650&&Math.abs(laptop.board.w-laptop.board.h)<1.5,`${name} 1470 board stays at today's minimum (${laptop.board.w})`);
   assert.ok(laptop.scrollW<=laptop.innerW,`${name} 1470 no horizontal scroll`);
   const mid=await at(1000,800);
   assert.ok(mid.scrollW<=mid.innerW&&mid.board.w>=590,`${name} 1000 wide: no page scroll, board keeps its minimum (${mid.board.w})`);
   const phone=await at(390,844);
   assert.ok(phone.scrollW<=phone.innerW,`${name} 390 no horizontal page scroll`);
   assert.ok(phone.sidebar.y<phone.board.y&&Math.abs(phone.sidebar.x-phone.board.x)<1,`${name} 390 keeps the stacked order`);
   console.log(`PASS ${name} public team board layout`);
  }finally{await browser.close();}
 }
})().catch(error=>{console.error(error);process.exitCode=1;});

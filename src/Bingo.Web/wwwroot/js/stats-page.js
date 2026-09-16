// Direct port of the approved stats-prototype.js; see CURRENT_STATUS source mapping.
(() => {
  'use strict';
  const root=document.querySelector('.stats-page');if(!root)return;
  const $ = (selector) => root.querySelector(selector);
  let data=window.StatsAdapter.adapt(JSON.parse($('#stats-data').textContent));
  const svgNS = 'http://www.w3.org/2000/svg';
  let {teams,players,everyone,luck}=data;
  let raceController;
  let mode='teams', hidden=new Set(), activeDay=null, geometry=null, luckMode='teams';
  const chart=$('#gp-chart'), tip=$('#chart-tip'),comparisonClear=$('#gp-clear-comparison');
  const money=(value,precise=false)=>value==null?'Unavailable':value<.001?`${Number((value*1000000).toFixed(1))} GP`:value<1?`${Number((value*1000).toFixed(1))}K`:value>=1000 ? `${(value/1000).toFixed(precise ? 3 : 2).replace(/0+$/,'').replace(/\.$/,'')}B` : `${Number(value.toFixed(1))}M`;
  const gpAmount=value=>`${money(value)}${value<.001?'':' GP'}`;
  const axisMoney=(value)=>value===0?'0':value>=1000?`${(value/1000).toFixed(value%1000?1:0)}B`:`${value}M`;
  const color=(series)=>getComputedStyle(root).getPropertyValue(series.color).trim();
  let contributors=data.contributors;
  let selectedTeamId=null,pinnedPlayerId=null,hoveredPlayerId=null,focusedPlayerId=null;
  let legendSignature=null;
  const motionPreference=window.matchMedia?.('(prefers-reduced-motion: reduce)');
  let chartSignature=null,donutSignature=null,lastChart=[],lastDonut=[],lastGpScope=null,helpDismissal=null;
  const motionAllowed=()=>!motionPreference?.matches;
  function animateGp(element,frames,duration=200,easing='cubic-bezier(.2,.7,.2,1)') {
    if(!motionAllowed()||!element?.animate)return null;
    return element.animate(frames,{duration,easing});
  }
  function animateSvg(element,attribute,from,to,duration=240) {
    if(!motionAllowed()||from===to)return;
    const animation=node('animate',{attributeName:attribute,from,to,dur:`${duration}ms`,begin:'indefinite',fill:'remove',calcMode:'spline',keyTimes:'0;1',keySplines:'.2 .7 .2 1'});
    if(!animation.beginElement)return;
    element.append(animation);animation.beginElement();
  }
  function dismissGpHelp(returnFocus=false) {
    if(helpDismissal)return;
    const finish=()=>{$('#gp-help').open=false;helpDismissal=null;if(returnFocus)$('#gp-help').querySelector('summary').focus({preventScroll:true});};
    const animation=animateGp($('.gp-help-box'),[{opacity:1,transform:'translateY(0)'},{opacity:0,transform:'translateY(-3px)'}],140);
    if(!animation){finish();return;}
    helpDismissal=animation;animation.onfinish=finish;
  }
  motionPreference?.addEventListener('change',()=>{
    if(!motionPreference.matches)return;
    $('.gp-panel').getAnimations?.({subtree:true}).forEach(animation=>animation.finish());
    $('.gp-panel').querySelectorAll('animate').forEach(animation=>animation.endElement?.());
    lastChart.forEach(item=>{item.transition=null;});
  });

  const selectedTeam=()=>teams.find(team=>team.id===selectedTeamId);
  const playerView=()=>mode==='players'||Boolean(selectedTeam());
  const rankedPlayers=()=>contributors.filter(p=>!selectedTeamId||p.teamId===selectedTeamId).sort((a,b)=>b.values.at(-1)-a.values.at(-1)||a.id.localeCompare(b.id));
  const previewId=()=>hoveredPlayerId||focusedPlayerId;
  const current=()=>{
    if(!playerView())return mode==='everyone'?everyone:teams;
    const ranked=rankedPlayers(),top=ranked.slice(0,5);
    const extra=ranked.find(p=>p.id===previewId()&&!top.some(t=>t.id===p.id)) || ranked.find(p=>p.id===pinnedPlayerId&&!top.some(t=>t.id===p.id));
    return extra?[...top,extra]:top;
  };
  function gpBreakdown() {
    if(!playerView())return teams;
    const ranked=rankedPlayers();
    if(selectedTeam())return ranked;
    return [...ranked.slice(0,5),...(ranked.length>5?[{id:'others',name:'Others',color:'--gp-others',share:data.stats.value.valueGp>0?ranked.slice(5).reduce((sum,p)=>sum+p.share,0):null,values:Array.from({length:data.dates.length},(_,day)=>ranked.slice(5).reduce((sum,p)=>sum+p.values[day],0))}]:[])];
  }
  function resetComparison(){pinnedPlayerId=null;hoveredPlayerId=null;focusedPlayerId=null;hidden.clear();hideTip();}
  function highlightGp() {
    const id=previewId();
    const chartId=current().some(p=>p.id===id)?id:null;
    const donutId=id&&mode==='players'&&!gpBreakdown().some(p=>p.id===id)?'others':id;
    chart.querySelectorAll('[data-series]').forEach(el=>el.style.opacity=chartId&&el.dataset.series!==chartId?'.2':'1');
    $('#gp-donut').querySelectorAll('[data-team]').forEach(el=>{el.style.opacity=donutId&&el.dataset.team!==donutId?'.25':'1';el.classList.toggle('is-highlighted',el.dataset.team===donutId);});
    $('#share-legend').querySelectorAll('.share-row').forEach(el=>{
      el.classList.toggle('highlighted',el.dataset.team===id);
      el.classList.toggle('pinned',el.dataset.team===pinnedPlayerId);
      if(playerView()&&el.dataset.team!=='others')el.setAttribute('aria-pressed',String(el.dataset.team===pinnedPlayerId));
    });
  }
  function updateComparison() {
    const valid=new Set(current().map(p=>p.id));
    hidden=new Set([...hidden].filter(id=>valid.has(id)));
    if(hidden.size===valid.size)hidden.delete(current()[0]?.id);
    renderLegend();drawChart();highlightGp();
    const pin=contributors.find(p=>p.id===pinnedPlayerId);
    comparisonClear.hidden=!pin||!current().some(p=>p.id===pin.id);
    comparisonClear.textContent='×';
    comparisonClear.title=pin?`Unpin ${pin.name}`:'';
    comparisonClear.setAttribute('aria-label',pin?`Remove pinned comparison for ${pin.name}`:'Remove comparison');
  }
  function previewPlayer(id,kind) {
    if(kind==='focus')focusedPlayerId=id;else hoveredPlayerId=id;
    if(playerView())updateComparison();else highlightGp();
  }
  function pinPlayer(id,toggle=true) {
    const player=rankedPlayers().find(p=>p.id===id);if(!player)return;
    if(rankedPlayers().slice(0,5).some(p=>p.id===id)) {
      hidden.delete(id);updateComparison();announce(`${player.name} is already in the top five.`);return;
    }
    pinnedPlayerId=toggle&&pinnedPlayerId===id?null:id;hidden.delete(id);updateComparison();
    if(pinnedPlayerId)animateGp(comparisonClear.parentElement,[{transform:'scale(1)'},{transform:'scale(1.035)',offset:.45},{transform:'scale(1)'}],240);
    announce(pinnedPlayerId?`${player.name} pinned as the sixth comparison. Select another player to replace it.`:'Comparison unpinned.');
  }
  // Hold the rendered overview's own dimensions; responsive CSS remains the size authority.
  function holdGpLayout() {
    const panel=$('.gp-panel');if(panel.dataset.gpLayout==='held')return;
    const regions={visual:'.gp-visuals',trend:'.gp-trend',share:'.gp-share',legend:'#gp-legend'};
    Object.entries(regions).forEach(([name,selector])=>panel.style.setProperty(`--gp-resting-${name}-height`,`${$(selector).getBoundingClientRect().height}px`));
    panel.dataset.gpLayout='held';
  }
  function releaseGpLayout(){delete $('.gp-panel').dataset.gpLayout;}
  window.addEventListener('resize',releaseGpLayout);
  $('#gp-help').addEventListener('keydown',event=>{if(event.key==='Escape')dismissGpHelp(true);});
  $('#gp-help-close').addEventListener('click',()=>dismissGpHelp(true));
  $('#gp-help').querySelector('summary').addEventListener('click',event=>{
    if(helpDismissal){event.preventDefault();helpDismissal.cancel();helpDismissal=null;return;}
    if($('#gp-help').open){event.preventDefault();dismissGpHelp();}
  });
  function openTeam(id) {
    if(!teams.some(t=>t.id===id))return;
    holdGpLayout();mode='teams';selectedTeamId=id;resetComparison();renderGp();$('#gp-back').focus();
  }
  function renderValuableDrops() {
    const drops=data.drops.filter(drop=>!selectedTeamId||drop.player.teamId===selectedTeamId).sort((a,b)=>(b.value??-1)-(a.value??-1)||a.id.localeCompare(b.id)).slice(0,3);
    const list=$('.gp-panel .treasure-list');list.replaceChildren();
    if(!drops.length){const empty=document.createElement('p');empty.className='stats-empty';empty.textContent='No approved item drops yet';list.append(empty);}
    drops.forEach(drop=>{
      const article=document.createElement('article');article.className='treasure-item';
      const img=document.createElement('img');img.className='item-art';if(drop.asset)img.src=drop.asset;else img.hidden=true;img.alt=drop.name;img.dataset.itemId=drop.itemId;
      const details=document.createElement('div');details.className='item-details';
      const title=document.createElement('h3');title.textContent=drop.name;title.title=drop.name;
      const value=document.createElement('div');value.className='item-value';value.textContent=drop.value==null?'Value unavailable':`${money(drop.value)}${drop.value<.001?'':' GP'}`;
      const byline=document.createElement('p');byline.className='item-byline';byline.textContent=`${drop.player.name} · ${drop.player.teamName}`;
      value.title=value.textContent;byline.title=byline.textContent;
      details.append(title,value,byline);article.append(img,details);list.append(article);
    });
  }
  function renderGp() {
    const team=selectedTeam(),isPlayers=playerView();
    $('.gp-panel').dataset.gpView=team?'team':mode;
    setTabs($('#gp-tabs'),mode);
    $('#gp-team-label').textContent=team?.name||'';$('#gp-team-label').hidden=!team;
    $('#gp-back').hidden=!team;
    $('#gp-shortcut').hidden=mode==='everyone'||Boolean(team)||!contributors.some(p=>p.id===data.currentPlayerId);
    $('#gp-shortcut').textContent=mode==='players'?'Show me':'My team';
    setSearchOpen(false);
    $('#gp-search-toggle').hidden=mode!=='players';
    $('#gp-player-search').placeholder='Search players…';
    $('#gp-search-toggle').setAttribute('aria-label','Search players');
    $('#gp-help').hidden=false;
    $('#gp-help-text').textContent=mode==='everyone'?'The chart combines recorded drop value across the event. The donut shows each team’s share. Hover or tap the chart to explore a date.':team?'Top 5 by total GP. Hover or focus another player to preview a sixth line; click or tap to pin it. Scroll the list for all contributors.':mode==='players'?'Top 5 by total GP. Search for a player to pin a sixth comparison; Others combines everyone else in the donut.':'Click a team in the donut or list to explore its players and most valuable drops.';
    $('#gp-unit').textContent=team?'Cumulative · top 5 contributors':mode==='players'?'Cumulative · top 5 players':mode==='everyone'?'Cumulative · entire event':'Cumulative · all teams';
    $('#gp-share-heading').textContent=isPlayers?'GP by player':'GP by team';
    $('.share-total span').textContent=team?'Total team GP':'Total GP earned';
    const total=team?team.value:data.stats.value;
    $('#stats-drop-count').textContent=String(total.drops);
    if(total.missingPrices)$('#gp-unit').textContent+=` · ${total.missingPrices} missing prices; known GP shown`;
    renderDonut();renderValuableDrops();updateComparison();
    $('#share-legend').scrollTop=0;
    const scope=`${mode}/${selectedTeamId||''}`;
    if(lastGpScope!==null&&scope!==lastGpScope){
      ['#gp-team-label','#share-legend','.share-total'].forEach(selector=>{
        const element=$(selector);if(!element.hidden)animateGp(element,[{opacity:0,transform:'translateY(3px)'},{opacity:1,transform:'translateY(0)'}],200);
      });
    }
    lastGpScope=scope;
  }
  function searchPlayers() {
    const query=$('#gp-player-search').value.trim().toLocaleLowerCase(),results=$('#gp-search-results');
    results.replaceChildren();results.hidden=!query;if(!query)return;
    const matches=(mode==='players'?rankedPlayers():[]).filter(p=>`${p.name} ${p.teamName||''}`.toLocaleLowerCase().includes(query));
    const note=document.createElement('p');note.className='gp-search-note';note.textContent=matches.length?`${Math.min(6,matches.length)} of ${matches.length} matches`:'No matching players';results.append(note);
    matches.slice(0,6).forEach(player=>{
      const button=document.createElement('button');button.type='button';
      button.textContent=`${player.name}${player.teamName?' · '+player.teamName:''} · ${money(player.values.at(-1))}`;
      button.addEventListener('pointerenter',()=>previewPlayer(player.id,'pointer'));
      button.addEventListener('pointerleave',()=>previewPlayer(null,'pointer'));
      button.addEventListener('focus',()=>previewPlayer(player.id,'focus'));
      button.addEventListener('blur',()=>previewPlayer(null,'focus'));
      // Keep mouse focus in the input until click selects the result. Keyboard focus still works.
      button.addEventListener('mousedown',event=>event.preventDefault());
      button.addEventListener('click',event=>{
        event.preventDefault();
        const left=window.scrollX,top=window.scrollY;
        pinPlayer(player.id,false);
        // Transfer focus while the input/results still exist and are enabled.
        // Disabling the active input first can send focus (and the viewport) to the body.
        $('#gp-search-toggle').focus({preventScroll:true});
        closePlayerSearch();
        window.scrollTo({left,top,behavior:'instant'});
      });
      results.append(button);
    });
  }
  function layoutGpSearch() {
    const divider=$('#gp-divider-search');
    // Match the 180px field plus 28px icon; total panel width is not the constraint.
    divider.dataset.layout=divider.getBoundingClientRect().width>=208?'inline':'popover';
  }
  new ResizeObserver(layoutGpSearch).observe($('#gp-divider-search'));
  function setSearchOpen(open) {
    layoutGpSearch();
    $('#gp-divider-search').dataset.open=String(open);
    $('#gp-search-toggle').setAttribute('aria-expanded',String(open));
    $('#gp-player-search').disabled=!open;
    if(!open){$('#gp-player-search').value='';$('#gp-search-results').hidden=true;}
  }
  function openPlayerSearch(){if(mode!=='players')return;setSearchOpen(true);if(helpDismissal){helpDismissal.cancel();helpDismissal=null;}$('#gp-help').open=false;$('#gp-player-search').focus();}
  function closePlayerSearch(){setSearchOpen(false);hoveredPlayerId=null;focusedPlayerId=null;updateComparison();}
  $('#gp-search-toggle').addEventListener('click',()=>{
    if($('#gp-divider-search').dataset.open==='true')closePlayerSearch();else openPlayerSearch();
  });
  $('#gp-back').addEventListener('click',()=>{selectedTeamId=null;resetComparison();renderGp();(contributors.some(p=>p.id===data.currentPlayerId)?$('#gp-shortcut'):$('#gp-tabs').querySelector('button')).focus({preventScroll:true});});
  $('#gp-shortcut').addEventListener('click',()=>{
    const player=contributors.find(p=>p.id===data.currentPlayerId);if(!player)return;
    if(mode==='teams'){openTeam(player.teamId);return;}
    pinPlayer(player.id,false);hidden.delete(player.id);updateComparison();
    const row=[...$('#share-legend').querySelectorAll('.share-row')].find(el=>el.dataset.team===player.id);row?.focus({preventScroll:true});
  });
  comparisonClear.addEventListener('click',()=>{pinnedPlayerId=null;hoveredPlayerId=null;focusedPlayerId=null;updateComparison();(selectedTeam()?$('#gp-back'):$('#gp-search-toggle')).focus({preventScroll:true});});
  $('#gp-player-search').addEventListener('input',searchPlayers);
  $('#gp-player-search').addEventListener('focus',searchPlayers);
  $('#gp-search-wrap').addEventListener('keydown',event=>{
    if(event.key==='Escape'){event.preventDefault();$('#gp-search-toggle').focus({preventScroll:true});closePlayerSearch();}
    if(event.key==='ArrowDown'&&event.target===$('#gp-player-search')){event.preventDefault();$('#gp-search-results').querySelector('button')?.focus();}
  });
  document.addEventListener('pointerdown',event=>{if(!event.target.closest('#gp-divider-search')&&$('#gp-divider-search').dataset.open==='true')closePlayerSearch();});
  $('#gp-divider-search').addEventListener('focusout',event=>{
    // A mouse/touch selection can blur the input without focusing the result button.
    // Outside pointerdown and explicit keyboard focus departure close the search.
    if(event.relatedTarget&&!event.currentTarget.contains(event.relatedTarget))closePlayerSearch();
  });

  function node(tag, attrs={}, text) {
    const el=document.createElementNS(svgNS,tag);
    Object.entries(attrs).forEach(([key,value])=>el.setAttribute(key,String(value)));
    if(text!==undefined) el.textContent=text;
    return el;
  }
  function donutArc(start,end) {
    const radius=73,point=angle=>[90+radius*Math.cos(angle),90+radius*Math.sin(angle)];
    let path=`M${point(start).join(',')}`;
    for(let part=0;part<4;part++){
      const a=start+(end-start)*part/4,b=start+(end-start)*(part+1)/4,k=4/3*Math.tan((b-a)/4),p=point(a),q=point(b);
      path+=` C${p[0]-k*radius*Math.sin(a)},${p[1]+k*radius*Math.cos(a)} ${q[0]+k*radius*Math.sin(b)},${q[1]-k*radius*Math.cos(b)} ${q.join(',')}`;
    }
    return path;
  }
  function revealDonut(donut) {
    if(!motionAllowed()||!donut.animate)return;
    const defs=node('defs'),mask=node('mask',{id:'gp-donut-reveal',maskUnits:'userSpaceOnUse',x:0,y:0,width:180,height:180});
    const sweep=node('circle',{cx:90,cy:90,r:73,fill:'none',stroke:'white','stroke-width':30,pathLength:1,'stroke-dasharray':1,'stroke-dashoffset':0,transform:'rotate(-90 90 90)'});
    mask.append(sweep);defs.append(mask);donut.append(defs);
    const segments=[...donut.querySelectorAll('[data-team]')];
    segments.forEach(segment=>segment.setAttribute('mask','url(#gp-donut-reveal)'));
    const finish=()=>{segments.forEach(segment=>segment.setAttribute('mask','none'));defs.remove();};
    const animation=animateGp(sweep,[{strokeDashoffset:1},{strokeDashoffset:0}],1200,'ease-in-out');
    if(animation){animation.onfinish=finish;animation.oncancel=finish;}else finish();
  }
  // Measure the real loaded font without changing the visible label or its motion.
  function measureStatsText(element,text,fontSize) {
    const sample=element.cloneNode(false);sample.removeAttribute('id');sample.textContent=text;
    sample.setAttribute('aria-hidden','true');
    Object.assign(sample.style,{position:'absolute',visibility:'hidden',pointerEvents:'none',whiteSpace:'nowrap',width:'max-content',maxWidth:'none',left:'0',right:'auto',top:'0',transform:'none'});
    if(fontSize)sample.style.fontSize=`${fontSize}px`;
    element.parentElement.append(sample);const bounds=sample.getBoundingClientRect();sample.remove();return bounds;
  }
  function fitDonutTotal() {
    const ring=$('.share-ring'),total=$('.share-total'),value=$('#share-total-value'),caption=total.querySelector('span');
    const width=ring.getBoundingClientRect().width;if(!width)return;
    const amount=measureStatsText(value,value.textContent,34),label=measureStatsText(caption,caption.textContent,9);
    // The unchanged SVG has r=73 and stroke=21 in a 180-unit viewBox.
    // Fit the entire two-line rectangle inside the inner circle, with 4px clearance.
    const diameter=width*(73-21/2)*2/180-8;
    const valueScale=captionScale=>{
      let low=0,high=1;
      const fits=scale=>Math.hypot(Math.max(amount.width*scale,label.width*captionScale),amount.height*scale+label.height*captionScale+5)<=diameter;
      if(fits(1))return 1;
      for(let i=0;i<20;i++){const scale=(low+high)/2;if(fits(scale))low=scale;else high=scale;}
      return low;
    };
    // Retain the 9px caption whenever possible; compact long totals may use 8px
    // before reducing the large number below 24px. Neither string is abbreviated.
    let captionScale=1,scale=valueScale(captionScale);
    if(scale*34<24){captionScale=8/9;scale=valueScale(captionScale);}
    total.style.setProperty('--share-value-size',`${34*scale}px`);total.style.setProperty('--share-caption-size',`${9*captionScale}px`);
  }
  function renderDonut() {
    const donut=$('#gp-donut'),legend=$('#share-legend'),entries=gpBreakdown();
    const signature=JSON.stringify([mode,selectedTeamId,selectedTeam()?.value||data.stats.value,entries.map(entry=>[entry.id,entry.name,entry.teamName,entry.values.at(-1),entry.share,entry.teamShare])]);
    const total=entries.reduce((sum,entry)=>sum+entry.values.at(-1),0),scopeValue=selectedTeam()?.value||data.stats.value;
    $('#share-total-value').textContent=money(total);$('#share-total-value').title=gpAmount(total);$('#share-total-value').setAttribute('aria-label',gpAmount(total));
    $('.share-total span').textContent=scopeValue.missingPrices?'Known GP':selectedTeam()?'Total team GP':'Total GP earned';
    fitDonutTotal();
    if(signature===donutSignature){highlightGp();return;}
    donutSignature=signature;
    donut.replaceChildren(node('title',{},playerView()?'Player contributions':'Team contributions'));
    donut.setAttribute('role','group');donut.setAttribute('aria-label',playerView()?'Player GP shares':'Team GP shares');
    legend.setAttribute('aria-label',playerView()?'Player GP shares':'Team GP shares');
    legend.replaceChildren();
    const previous=lastDonut;lastDonut=[];
    let start=-Math.PI/2;
    entries.forEach((entry,index)=>{
      const value=entry.values.at(-1),share=selectedTeam()?entry.teamShare:entry.share,fraction=share==null?0:share/100,end=start+fraction*Math.PI*2,gap=Math.min(.016,fraction*Math.PI*.2),radius=73;
      const shape=donutArc(start+gap,end-gap);
      const label=`${entry.name}: ${gpAmount(value)} · ${share==null?'—':share.toFixed(1)+'%'}`;
      const action=entry.id==='others'?'Search for another player':playerView()?'Preview or pin player':mode==='teams'?'Explore team':'Highlight team';
      const segment=node('path',{d:shape,fill:'none',stroke:color(entry),'stroke-width':21,class:'share-segment','data-team':entry.id,role:'button',tabindex:0,'aria-label':`${label}. ${action}`});
      segment.append(node('title',{},label));donut.append(segment);
      if(previous.length)animateSvg(segment,'d',previous.find(item=>item.id===entry.id)?.shape||previous[index]?.shape||donutArc(start,start),shape);
      lastDonut.push({id:entry.id,shape});
      const row=document.createElement('button');row.type='button';row.className='share-row';row.dataset.team=entry.id;row.setAttribute('aria-label',`${label}. ${action}`);
      const dot=document.createElement('span');dot.className='share-dot';dot.style.background=color(entry);
      const name=document.createElement('span');name.className='share-name';name.textContent=entry.name;
      if(mode==='players'&&entry.teamName){const subtitle=document.createElement('small');subtitle.textContent=entry.teamName;name.append(subtitle);}
      const amount=document.createElement('strong');amount.textContent=money(value);
      const percent=document.createElement('span');percent.className='share-percent';percent.textContent=`${share==null?'—':share.toFixed(1)+'%'}`;
      row.append(dot,name,amount,percent);legend.append(row);
      const activate=()=>{
        if(entry.id==='others'){openPlayerSearch();return;}
        if(playerView()){pinPlayer(entry.id);return;}
        if(mode==='teams')openTeam(entry.id);else highlightGp();
      };
      [segment,row].forEach(el=>{
        const preview=kind=>{if(entry.id!=='others')previewPlayer(entry.id,kind);};
        el.addEventListener('pointerenter',()=>preview('pointer'));
        el.addEventListener('pointerleave',()=>previewPlayer(null,'pointer'));
        el.addEventListener('focus',()=>preview('focus'));
        el.addEventListener('blur',()=>previewPlayer(null,'focus'));
        el.addEventListener('click',activate);
        el.addEventListener('keydown',event=>{
          if(event.key==='Escape'){hoveredPlayerId=null;focusedPlayerId=null;updateComparison();}
          if(el===segment&&['Enter',' '].includes(event.key)){event.preventDefault();activate();}
        });
      });
      start=end;
    });
    if(!previous.length)revealDonut(donut);
    highlightGp();
  }
  function nearestDate(fraction) {
    const at=data.start+Math.max(0,Math.min(1,fraction))*(data.end-data.start);
    return data.dates.reduce((best,date,index)=>Math.abs(date-at)<Math.abs(data.dates[best]-at)?index:best,0);
  }
  function gpChartSignature() {
    return JSON.stringify([`${mode}/${selectedTeamId||''}`,Math.max(250,chart.clientWidth),chart.clientHeight,data.dates,data.positions,data.stats.timezone,current().filter(series=>!hidden.has(series.id)).map(series=>[series.id,series.values,color(series)])]);
  }
  const chartPath=points=>points.map(([x,y],index)=>`${index?'L':'M'}${x.toFixed(2)},${y.toFixed(2)}`).join(' ');
  // Resample only animation geometry. The underlying path and hover dates keep the
  // exact authoritative points, including inserted/removed approval timestamps.
  function compatibleChartPoints(from,to) {
    const vertical=new Set();
    for(const points of [from,to])points.forEach(([x],index)=>{if(index&&points[index-1][0]===x)vertical.add(x);});
    const xs=[...new Set([...from,...to].map(point=>point[0]))].sort((a,b)=>a-b).flatMap(x=>vertical.has(x)?[x,x]:[x]);
    const sample=points=>{
      let index=0;
      return xs.map((x,position)=>{
        while(index<points.length-1&&points[index+1][0]<x)index++;
        let first=index;
        if(points[first][0]!==x&&points[first+1]?.[0]===x)first++;
        if(points[first][0]===x){
          let last=first;while(points[last+1]?.[0]===x)last++;
          return [x,points[position&&xs[position-1]===x?last:first][1]];
        }
        const a=points[index],b=points[index+1]||a;
        const fraction=b[0]===a[0]?1:Math.max(0,Math.min(1,(x-a[0])/(b[0]-a[0])));
        return [x,a[1]+(b[1]-a[1])*fraction];
      });
    };
    return {from:sample(from),to:sample(to)};
  }
  function chartMotionFrame(item) {
    const motion=item.transition;
    if(!motion||!motionAllowed())return {points:item.points,labelY:item.labelY};
    let elapsed=performance.now()-motion.started;
    try {elapsed=(motion.animation.getCurrentTime()-motion.animation.getStartTime())*1000;}catch{}
    const time=Math.max(0,Math.min(1,elapsed/240));
    // Match animateSvg's existing .2 .7 .2 1 spline on an interrupted update.
    let low=0,high=1;
    for(let i=0;i<24;i++){
      const t=(low+high)/2,x=3*(1-t)**2*t*.2+3*(1-t)*t*t*.2+t**3;
      if(x<time)low=t;else high=t;
    }
    const t=(low+high)/2,eased=time===0?0:time===1?1:3*(1-t)**2*t*.7+3*(1-t)*t*t+t**3;
    return {points:time===1?item.points:motion.from.map(([x,y],index)=>[x,y+(motion.to[index][1]-y)*eased]),labelY:motion.labelY+(item.labelY-motion.labelY)*eased};
  }
  function drawChart() {
    const width=Math.max(250,chart.clientWidth),height=chart.clientHeight;
    const scope=`${mode}/${selectedTeamId||''}`;
    const signature=gpChartSignature();
    if(signature===chartSignature){highlightGp();return;}
    chartSignature=signature;
    const previous=lastChart,initial=!previous.length,scopeChanged=previous.length&&previous[0].scope!==scope;
    const resized=previous.length&&(previous[0].width!==width||previous[0].height!==height);
    const previousFrames=new Map(previous.map(item=>[item,chartMotionFrame(item)]));
    lastChart=[];
    const left=width<450?37:43,right=width<450?43:53,top=17,bottom=31;
    const plotWidth=width-left-right,plotHeight=height-top-bottom;
    const peak=Math.max(1,...current().map(series=>series.values.at(-1)));
    const magnitude=10**Math.floor(Math.log10(peak));
    const step=peak/magnitude<=2?magnitude/2:magnitude,ceiling=Math.ceil(peak/step)*step;
    const x=(index)=>left+data.positions[index]*plotWidth;
    const y=(value)=>top+plotHeight-value/ceiling*plotHeight;
    geometry={width,height,left,right,top,bottom,plotWidth,plotHeight,x,y};
    chart.setAttribute('viewBox',`0 0 ${width} ${height}`);
    chart.replaceChildren();
    chart.append(node('title',{},`GP over time, ${mode}. ${data.date(data.start)} to ${data.date(data.end)}.`));
    chart.append(node('desc',{},'Left and right arrow keys move between dates. Escape closes the values. Legend buttons show or hide series.'));
    for(let value=0;value<=ceiling;value+=step) {
      chart.append(node('line',{x1:left,y1:y(value),x2:width-right,y2:y(value),class:'chart-grid'}));
      chart.append(node('text',{x:left-8,y:y(value)+4,'text-anchor':'end',class:'axis-text'},axisMoney(value)));
    }
    const dates=[...new Set((width<480?[0,.5,1]:[0,.25,.5,.75,1]).map(f=>nearestDate(f)))];
    for(const index of dates) {
      chart.append(node('text',{x:x(index),y:height-8,'text-anchor':'middle',class:'axis-text'},data.date(data.dates[index])));
    }
    const visible=current().filter(series=>!hidden.has(series.id));
    // In player views, spread clustered end labels while keeping their endpoints exact.
    const labels=visible.map(series=>({id:series.id,y:y(series.values.at(-1))+4})).sort((a,b)=>a.y-b.y);
    if(playerView()) {
      labels.forEach((label,index)=>{if(index)label.y=Math.max(label.y,labels[index-1].y+14);});
      if(labels.length)labels.at(-1).y=Math.min(labels.at(-1).y,height-bottom+4);
      for(let index=labels.length-2;index>=0;index--)labels[index].y=Math.min(labels[index].y,labels[index+1].y-14);
    }
    visible.forEach((series,index)=>{
      const labelY=labels.find(label=>label.id===series.id).y;
      if(playerView()&&Math.abs(labelY-y(series.values.at(-1))-4)>2)chart.append(node('line',{x1:x(data.dates.length-1)+3,y1:y(series.values.at(-1)),x2:x(data.dates.length-1)+7,y2:labelY-4,stroke:color(series),'stroke-width':.7,'data-series':series.id}));
      const points=series.values.map((value,index)=>[Number(x(index).toFixed(2)),Number(y(value).toFixed(2))]),path=chartPath(points);
      const line=node('path',{d:path,pathLength:1,stroke:color(series),class:'chart-line','data-series':series.id});
      const dot=node('circle',{cx:x(data.dates.length-1),cy:y(series.values.at(-1)),r:4.2,fill:color(series),'data-series':series.id});
      const label=node('text',{x:x(data.dates.length-1)+8,y:labelY,fill:color(series),'font-family':'Geist, sans-serif','font-size':width<450?10:11,'font-weight':650,class:'end-value','data-series':series.id},money(series.values.at(-1)));
      chart.append(line,dot,label);
      const before=previous.find(item=>item.id===series.id)||(scopeChanged?previous[index]:null);
      let transition=null;
      if(initial){
        animateGp(line,[{strokeDasharray:'1',strokeDashoffset:'1'},{strokeDasharray:'1',strokeDashoffset:'0'}],1100,'ease-in-out');
        [dot,label].forEach(element=>animateGp(element,[{opacity:0},{opacity:0,offset:.7},{opacity:1}],1100));
      }else if(before&&!resized){
        const frame=previousFrames.get(before),compatible=compatibleChartPoints(frame.points,points);
        animateSvg(line,'d',chartPath(compatible.from),chartPath(compatible.to));
        const animation=line.querySelector('animate');
        if(animation)transition={...compatible,started:performance.now(),animation,labelY:frame.labelY};
        animateSvg(dot,'cy',frame.points.at(-1)[1],y(series.values.at(-1)));
        animateSvg(label,'y',frame.labelY,labelY);
      }else if(!before){
        [line,dot,label].forEach(element=>animateGp(element,[{opacity:0},{opacity:1}],180));
      }
      lastChart.push({id:series.id,path,points,transition,cy:y(series.values.at(-1)),labelY,stroke:color(series),scope,width,height});
    });
    if(!scopeChanged&&!resized&&motionAllowed())previous.filter(item=>!visible.some(series=>series.id===item.id)).forEach(item=>{
      const ghost=node('path',{d:item.path,stroke:item.stroke,class:'chart-line gp-exiting-line','aria-hidden':true});chart.append(ghost);
      const animation=animateGp(ghost,[{opacity:.65},{opacity:0}],150);
      if(animation)animation.onfinish=()=>ghost.remove();else ghost.remove();
    });
    const cursor=node('g',{id:'chart-cursor'}); chart.append(cursor);
    highlightGp();
    if(activeDay!==null) showDay(activeDay,false);
  }
  function renderLegend() {
    const signature=JSON.stringify([mode,selectedTeamId,pinnedPlayerId,current().map(series=>[series.id,color(series)])]);
    if(signature===legendSignature){updateLegendState();return;}
    legendSignature=signature;
    const legend=$('#gp-legend'),scrollTop=legend.scrollTop,scrollLeft=legend.scrollLeft;
    legend.replaceChildren();
    current().forEach(series=>{
      const button=document.createElement('button');button.type='button';button.dataset.series=series.id;
      button.setAttribute('aria-pressed',String(!hidden.has(series.id)));
      button.setAttribute('aria-label',`${hidden.has(series.id)?'Show':'Hide'} ${series.name}`);
      const dot=document.createElement('span');dot.className='dot';dot.style.background=color(series);
      button.append(dot,document.createTextNode(series.name+(playerView()&&!rankedPlayers().slice(0,5).some(p=>p.id===series.id)?' · comparison':'')));
      button.addEventListener('click',event=>{
        event.preventDefault();
        if(hidden.has(series.id)) hidden.delete(series.id);
        else if(hidden.size<current().length-1) hidden.add(series.id);
        else {announce('Keep at least one line visible. Select another name to show its line.');return;}
        updateLegendState();drawChart();
      });
      if(series.id===pinnedPlayerId){
        const group=document.createElement('span');group.className='gp-pinned-legend';group.append(button,comparisonClear);$('#gp-legend').append(group);
      }else $('#gp-legend').append(button);
    });
    if(!current().some(p=>p.id===pinnedPlayerId)){$('#gp-legend').append(comparisonClear);comparisonClear.hidden=true;}
    legend.scrollTop=scrollTop;legend.scrollLeft=scrollLeft;
  }
  function updateLegendState() {
    // Keep the clicked controls, keyboard focus and legend scroll position intact.
    $('#gp-legend').querySelectorAll('button').forEach(button=>{
      const series=current().find(entry=>entry.id===button.dataset.series);if(!series)return;
      button.setAttribute('aria-pressed',String(!hidden.has(series.id)));
      button.setAttribute('aria-label',`${hidden.has(series.id)?'Show':'Hide'} ${series.name}`);
    });
  }
  function hideTip(){activeDay=null;tip.hidden=true;$('#chart-cursor')?.replaceChildren();}
  function showDay(index,readAloud) {
    if(!geometry)return;
    activeDay=Math.max(0,Math.min(data.dates.length-1,index));
    const {x,y,top,height,bottom,width}=geometry,cursor=$('#chart-cursor');
    cursor.replaceChildren(node('line',{x1:x(activeDay),x2:x(activeDay),y1:top,y2:height-bottom,stroke:'var(--muted)','stroke-dasharray':'3 4','stroke-width':1,opacity:.55}));
    const shown=current().filter(series=>!hidden.has(series.id));
    tip.replaceChildren();
    const date=document.createElement('div');date.className='tip-date';date.textContent=data.date(data.dates[activeDay],true);tip.append(date);
    shown.forEach(series=>{
      cursor.append(node('circle',{cx:x(activeDay),cy:y(series.values[activeDay]),r:4.5,fill:color(series),stroke:'var(--surface)','stroke-width':2}));
      const row=document.createElement('div');row.className='tip-row';
      const name=document.createElement('span');name.className='tip-name';
      const dot=document.createElement('span');dot.className='dot';dot.style.background=color(series);
      name.append(dot,document.createTextNode(series.name));
      const value=document.createElement('strong');value.textContent=gpAmount(series.values[activeDay]);
      row.append(name,value);tip.append(row);
    });
    tip.hidden=false;
    const tipWidth=tip.offsetWidth;
    const rawLeft=x(activeDay)>width*.58?x(activeDay)-tipWidth-14:x(activeDay)+14;
    tip.style.left=`${Math.max(0,Math.min(width-tipWidth,rawLeft))}px`;
    tip.style.top='15px';
    if(readAloud) $('#chart-announcement').textContent=`${data.date(data.dates[activeDay],true)}. ${shown.map(s=>`${s.name}: ${gpAmount(s.values[activeDay])}`).join('. ')}`;
  }
  chart.addEventListener('pointermove',event=>{
    if(event.pointerType==='touch')return;
    const rect=chart.getBoundingClientRect();
    showDay(nearestDate((event.clientX-rect.left-geometry.left)/geometry.plotWidth),false);
  });
  chart.addEventListener('pointerleave',event=>{if(event.pointerType!=='touch')hideTip();});
  chart.addEventListener('click',event=>{
    const rect=chart.getBoundingClientRect();
    showDay(nearestDate((event.clientX-rect.left-geometry.left)/geometry.plotWidth),true);
  });
  chart.addEventListener('keydown',event=>{
    if(event.key==='Escape'){hideTip();return;}
    if(!['ArrowLeft','ArrowRight','Home','End'].includes(event.key))return;
    event.preventDefault();
    const next=event.key==='Home'?0:event.key==='End'?data.dates.length-1:(activeDay??Math.floor(data.dates.length/2))+(event.key==='ArrowLeft'?-1:1);
    showDay(next,true);
  });
  chart.addEventListener('blur',hideTip);
  document.addEventListener('pointerdown',event=>{if(!event.target.closest('.chart-wrap'))hideTip();});
  function setTabs(container,selected){container.querySelectorAll('button').forEach(button=>button.setAttribute('aria-pressed',String(button.dataset.mode===selected)));}
  $('#gp-tabs').addEventListener('click',event=>{
    const button=event.target.closest('button[data-mode]');if(!button)return;
    holdGpLayout();mode=button.dataset.mode;selectedTeamId=null;resetComparison();renderGp();
  });
  let luckTeamId=null,luckPinnedId=null,luckHighlightId=null,luckFrame=null,luckMoving=[];
  let lastLuckTeamId,lastLuckPinnedId=null,luckHeadingMotion=null,luckRemoval=null,luckHasDrawn=false;
  function luckPlayers() {
    return [...luck.players].sort((a,b)=>(a.value==null)-(b.value==null)||(a.value??0)-(b.value??0)||a.id.localeCompare(b.id));
  }
  function luckExtremes(roster=luckPlayers()) {
    const ranked=roster.filter(player=>player.value!=null);
    return roster.length<=10?roster:ranked.length>10?[...ranked.slice(0,5),...ranked.slice(-5)]:ranked.length?ranked:roster.slice(0,10);
  }
  let luckScale=60,luckSpan=40,luckLayoutItems=[];
  function fitLuckLabels() {
    if(!luckLayoutItems.length)return;
    // Include the decimal place used by intermediate count-up frames, even when
    // the final value is an integer. Tabular digits make this a shared safe budget.
    const labelWidth=Math.max(...luckLayoutItems.map(({number,value})=>measureStatsText(number,`${value<0?'-':'+'}${Math.ceil(Math.abs(value))}.0%`).width));
    const reserve=Math.ceil(labelWidth)+11; // 7px bar gap + 4px outer clearance.
    $('.luck-panel').style.setProperty('--luck-label-reserve',`${reserve}px`);
    const widths=luckLayoutItems.map(({bar})=>bar.parentElement.getBoundingClientRect().width).filter(width=>width>0);
    luckSpan=widths.length?Math.max(0,Math.min(40,...widths.map(width=>(width/2-reserve)/width*100))):40;
    luckLayoutItems.forEach(item=>{
      const width=Math.abs(item.value)/luckScale*luckSpan;
      item.bar.style.width=`${width}%`;item.bar.style.left=`${item.value<0?50-width:50}%`;
      paintLuckMotion(item,item.progress??1);
    });
  }
  function paintLuckMotion(item,progress) {
    const {bar,number,value}=item;item.progress=progress;
    bar.style.transform=`scaleX(${progress})`;
    const count=Number((value*progress).toFixed(1)),edge=50+Math.abs(value)/luckScale*luckSpan*progress;
    number.textContent=`${count>0?'+':''}${count}%`;
    if(value<0)number.style.right=`calc(${edge}% + 7px)`;else number.style.left=`calc(${edge}% + 7px)`;
  }
  function finishLuckMotion() {
    if(luckFrame!==null)cancelAnimationFrame(luckFrame);
    luckFrame=null;
    luckMoving.forEach(item=>paintLuckMotion(item,1));
    luckMoving=[];
  }
  function animateLuckRows(rows,duration=420) {
    finishLuckMotion();
    if(!motionAllowed())return;
    luckMoving=rows;
    rows.forEach(item=>paintLuckMotion(item,0));
    let start=null;
    const frame=time=>{
      if(start===null)start=time;
      const progress=Math.min(1,(time-start)/duration);
      const eased=duration===1000?(progress<.5?4*progress**3:1-(-2*progress+2)**3/2):1-Math.pow(1-progress,3);
      rows.forEach(item=>paintLuckMotion(item,eased));
      if(progress<1)luckFrame=requestAnimationFrame(frame);else finishLuckMotion();
    };
    luckFrame=requestAnimationFrame(frame);
  }
  motionPreference?.addEventListener('change',()=>{if(!motionAllowed()){finishLuckMotion();$('.luck-panel').getAnimations?.({subtree:true}).forEach(animation=>animation.finish());}});
  let luckHelpDismissal=null;
  function setLuckHelpOpen(open,returnFocus=false) {
    const box=$('#luck-help-box'),toggle=$('#luck-help-toggle');
    if(luckHelpDismissal){luckHelpDismissal.onfinish=null;luckHelpDismissal.cancel();luckHelpDismissal=null;}
    toggle.setAttribute('aria-expanded',String(open));
    if(open){closeLuckSearch();box.hidden=false;return;}
    if(returnFocus)toggle.focus({preventScroll:true});
    const finish=()=>{box.hidden=true;luckHelpDismissal=null;};
    if(box.hidden){finish();return;}
    const animation=animateGp(box,[{opacity:1,transform:'translateY(0)'},{opacity:0,transform:'translateY(-3px)'}],140);
    if(animation){luckHelpDismissal=animation;animation.onfinish=finish;}else finish();
  }
  $('#luck-help-toggle').addEventListener('click',()=>setLuckHelpOpen($('#luck-help-toggle').getAttribute('aria-expanded')!=='true'));
  $('#luck-help-close').addEventListener('click',()=>setLuckHelpOpen(false,true));
  [$('#luck-help-box'),$('#luck-help-toggle')].forEach(element=>element.addEventListener('keydown',event=>{if(event.key==='Escape'){event.preventDefault();setLuckHelpOpen(false,true);}}));
  function closeLuckSearch(returnFocus=false) {
    if(returnFocus)$('#luck-search-toggle').focus({preventScroll:true});
    $('#luck-search-popover').hidden=true;$('#luck-search-toggle').setAttribute('aria-expanded','false');
    $('#luck-player-search').value='';$('#luck-search-results').replaceChildren();
  }
  function openLuckTeam(id) {
    if(!teams.some(team=>team.id===id))return;
    luckTeamId=id;luckMode='teams';luckPinnedId=null;luckHighlightId=null;
    closeLuckSearch();renderLuck();$('#luck-back').focus({preventScroll:true});
  }
  function highlightLuckPlayer(id) {
    const list=$('#luck-rows');
    [...list.children].forEach(row=>row.classList.toggle('is-highlighted',row.dataset.player===id));
    const row=[...list.children].find(row=>row.dataset.player===id);
    // Rows use this positioned list as their offset parent: keep scroll coordinates local.
    // Align the row bottom with the viewport bottom so the whole result is visible.
    if(row){
      const top=row.dataset.player===luckPinnedId?0:Math.max(0,row.offsetTop+row.offsetHeight-list.clientHeight);
      if(list.scrollTo)list.scrollTo({top,behavior:motionAllowed()?'smooth':'instant'});else list.scrollTop=top;
    }
  }
  function selectLuckPlayer(id) {
    const player=luckPlayers().find(player=>player.id===id);if(!player||luckMode!=='players')return;
    closeLuckSearch(true);luckHighlightId=id;
    if(!luckExtremes().some(player=>player.id===id)) {luckPinnedId=id;renderLuck(false);}
    highlightLuckPlayer(id);
    $('#luck-status').textContent=`${player.name}: ${player.label} luck${luckPinnedId===id?', pinned for comparison':''}.`;
  }
  function searchLuckPlayers() {
    const results=$('#luck-search-results'),query=$('#luck-player-search').value.trim().toLocaleLowerCase();
    results.replaceChildren();if(luckMode!=='players'||!query)return;
    const matches=luckPlayers().filter(player=>`${player.name} ${player.teamName}`.toLocaleLowerCase().includes(query));
    if(!matches.length){const note=document.createElement('p');note.textContent='No matching players';results.append(note);}
    matches.slice(0,6).forEach(player=>{
      const button=document.createElement('button');button.type='button';
      button.textContent=`${player.name} · ${player.teamName}`;
      button.addEventListener('mousedown',event=>event.preventDefault());
      button.addEventListener('click',event=>{event.preventDefault();selectLuckPlayer(player.id);});results.append(button);
    });
  }
  function removeLuckComparison(row) {
    if(luckRemoval)return;
    finishLuckMotion();
    $('#luck-search-toggle').focus({preventScroll:true});
    const height=row.offsetHeight;
    row.getAnimations?.().forEach(animation=>animation.cancel());
    const finish=()=>{
      luckRemoval=null;luckPinnedId=null;luckHighlightId=null;
      const list=$('#luck-rows'),scrollTop=list.scrollTop;
      renderLuck(false);list.scrollTop=scrollTop;
      $('#luck-status').textContent='Comparison removed.';
    };
    const animation=animateGp(row,[
      {height:`${height}px`,marginBottom:'12px',opacity:1,overflow:'hidden'},
      {height:'0px',marginBottom:'0px',opacity:0,overflow:'hidden'}
    ],220);
    if(animation){luckRemoval=animation;animation.onfinish=finish;}else finish();
  }
  function renderLuck(animate=true) {
    if(luckRemoval){luckRemoval.onfinish=null;luckRemoval.cancel();luckRemoval=null;}
    finishLuckMotion();
    const list=$('#luck-rows'),roster=luckPlayers(),team=teams.find(team=>team.id===luckTeamId);
    const extremes=luckExtremes(roster),pinned=roster.find(player=>player.id===luckPinnedId);
    const entries=luckMode==='teams'?(team?roster.filter(player=>player.teamId===team.id):[...luck.teams].sort((a,b)=>(a.value==null)-(b.value==null)||(a.value??0)-(b.value??0)||a.id.localeCompare(b.id))):[...(pinned&&!extremes.some(player=>player.id===pinned.id)?[pinned]:[]),...extremes];
    luckScale=Math.max(60,...entries.filter(entry=>Number.isFinite(entry.value)).map(entry=>Math.abs(entry.value)));
    list.replaceChildren();list.scrollTop=0;
    list.style.setProperty('--luck-visible-rows',Math.max(3,Math.min(6,teams.length)));
    list.setAttribute('aria-label',team?`${team.name} player luck`:luckMode==='players'?'Five unluckiest and five luckiest players':'Team luck');
    $('#luck-team-label').textContent=team?.name||'';$('#luck-team-label').hidden=!team;
    $('#luck-team-label').title=team?.name||'';$('#luck-back').hidden=!team;
    $('#luck-search-toggle').hidden=luckMode!=='players';setTabs($('#luck-tabs'),luckMode);
    const luckMeta=data.stats.luck;
    $('.luck-panel .subheading').textContent=luckMeta.stale?`Stale · calculated ${data.date(Date.parse(luckMeta.calculatedAt),true)}`:window.StatsAdapter.luckText(luckMeta.result);
    $('.luck-panel .subheading').title=`Received ${luckMeta.result.received}; expected ${luckMeta.result.expected??'unavailable'}. Evidence revision ${luckMeta.evidenceRevision}. ${luckMeta.upstreamUpdatedAt?'Provider updated '+data.date(Date.parse(luckMeta.upstreamUpdatedAt),true):'Provider update time unavailable'}`;
    $('#luck-help-text').textContent=luckMode==='players'?'Shows the five unluckiest and five luckiest players. Search and select a player to compare their luck against them.':'Click a team to explore its players. Use the back arrow to return to all teams.';
    $('#luck-help-text').textContent+=' Luck compares approved drops with expected drops from retained rates and recorded Playing-account activity. Estimates, missing activity and stale results are labelled.';
    if(lastLuckTeamId!==undefined&&lastLuckTeamId!==luckTeamId){
      luckHeadingMotion?.cancel();
      luckHeadingMotion=animateGp($('.luck-title-group'),[{opacity:0,transform:'translateX(-4px)'},{opacity:1,transform:'translateX(0)'}],180);
    }
    lastLuckTeamId=luckTeamId;
    const comparisonChanged=luckPinnedId!==null&&luckPinnedId!==lastLuckPinnedId;
    const moving=[];let pinnedRow=null;

    entries.forEach((entry,index)=>{
      const {name,value}=entry,isPinned=luckMode==='players'&&entry.id===luckPinnedId&&!extremes.some(player=>player.id===entry.id);
      const row=document.createElement('div');row.className='luck-row';
      row.dataset.player=team||luckMode==='players'?entry.id:'';
      if(luckMode==='players'&&extremes.length===10&&roster.filter(p=>p.value!=null).length>10&&entry.id===extremes[5].id)row.classList.add('luck-group-start');
      if(isPinned)row.classList.add('luck-pinned-row');
      row.setAttribute('aria-label',`${name}: ${entry.label}${isPinned?', pinned comparison':''}`);
      const label=document.createElement(luckMode==='teams'&&!team?'button':'span');label.className='luck-name';label.textContent=name;
      label.title=entry.teamName?`${name} · ${entry.teamName}`:name;
      if(luckMode==='teams'&&!team){label.type='button';label.setAttribute('aria-label',`Explore ${name} players`);label.addEventListener('click',()=>openLuckTeam(entry.id));}
      const track=document.createElement('div');track.className='luck-track';track.setAttribute('aria-hidden','true');
      if(luckMode==='teams'&&!team){track.style.cursor='pointer';track.addEventListener('click',()=>openLuckTeam(entry.id));}
      const width=Math.abs(value)/luckScale*40;
      const bar=document.createElement('span');bar.className=`luck-bar${value<0?' negative':''}`;
      bar.style.width=`${width}%`;bar.style.left=`${value<0?50-width:50}%`;bar.style.transformOrigin=value<0?'right center':'left center';
      const number=document.createElement('span');number.className=`luck-value${value<0?' negative':''}`;
      number.textContent=value==null?entry.label:`${value>0?'+':''}${Number(value.toFixed(1))}%`;
      if(value==null){bar.hidden=true;number.classList.add('luck-unavailable');}
      if(value!=null){if(value<0)number.style.right=`calc(${50+width}% + 7px)`;else number.style.left=`calc(${50+width}% + 7px)`;}
      number.title=entry.label;
      track.append(bar,number);row.append(label,track);
      if(isPinned){const clear=document.createElement('button');clear.type='button';clear.className='luck-clear-pin';clear.textContent='×';clear.setAttribute('aria-label',`Remove ${name} comparison`);clear.addEventListener('click',()=>removeLuckComparison(row));const nameText=document.createElement('span');nameText.className='luck-pinned-name';nameText.textContent=name;label.replaceChildren(nameText,clear);}
      list.append(row);if(value!=null)moving.push({bar,number,value,id:entry.id});if(isPinned)pinnedRow=row;
    });
    luckLayoutItems=moving;fitLuckLabels();
    if(comparisonChanged&&pinnedRow){
      const height=pinnedRow.offsetHeight;
      animateGp(pinnedRow,lastLuckPinnedId===null?[
        {height:'0px',marginBottom:'0px',opacity:0,overflow:'hidden'},
        {height:`${height}px`,marginBottom:'12px',opacity:1,overflow:'hidden'}
      ]:[{opacity:0},{opacity:1}],220);
    }
    if(animate)animateLuckRows(moving,luckHasDrawn?420:1000);
    else if(comparisonChanged&&pinnedRow)animateLuckRows(moving.filter(item=>item.id===luckPinnedId));
    luckHasDrawn=true;lastLuckPinnedId=luckPinnedId;
    if(luckHighlightId)highlightLuckPlayer(luckHighlightId);
  }
  $('#luck-tabs').addEventListener('click',event=>{
    const button=event.target.closest('button[data-mode]');if(!button||button.dataset.mode===luckMode&&!luckTeamId)return;
    closeLuckSearch();luckMode=button.dataset.mode;luckTeamId=null;luckPinnedId=null;luckHighlightId=null;renderLuck();
  });
  $('#luck-back').addEventListener('click',()=>{luckTeamId=null;renderLuck();$('#luck-tabs').querySelector('button[data-mode]').focus({preventScroll:true});});
  $('#luck-search-toggle').addEventListener('click',()=>{
    if(luckMode!=='players')return;
    if(!$('#luck-search-popover').hidden){closeLuckSearch(true);return;}
    setLuckHelpOpen(false,false);
    $('#luck-search-popover').hidden=false;$('#luck-search-toggle').setAttribute('aria-expanded','true');searchLuckPlayers();$('#luck-player-search').focus({preventScroll:true});
  });
  $('#luck-player-search').addEventListener('input',searchLuckPlayers);
  $('#luck-search').addEventListener('keydown',event=>{
    if(event.key==='Escape'){event.preventDefault();closeLuckSearch(true);}
    if(event.key==='Enter'&&event.target===$('#luck-player-search')){const result=$('#luck-search-results').querySelector('button');if(result){event.preventDefault();result.click();}}
  });
  $('#luck-search').addEventListener('focusout',event=>{if(event.relatedTarget&&!$('#luck-search').contains(event.relatedTarget))closeLuckSearch();});
  document.addEventListener('pointerdown',event=>{if(!$('#luck-search').contains(event.target))closeLuckSearch();});
  let raceHelpDismissal=null,raceMotionFinish=null;
  function setRaceHelpOpen(open,returnFocus=false) {
    const box=$('#race-help-box'),toggle=$('#race-help-toggle');
    if(raceHelpDismissal){raceHelpDismissal.onfinish=null;raceHelpDismissal.cancel();raceHelpDismissal=null;}
    toggle.setAttribute('aria-expanded',String(open));
    if(open){box.hidden=false;return;}
    if(returnFocus)toggle.focus({preventScroll:true});
    const finish=()=>{box.hidden=true;raceHelpDismissal=null;};
    if(box.hidden){finish();return;}
    const animation=animateGp(box,[{opacity:1,transform:'translateY(0)'},{opacity:0,transform:'translateY(-3px)'}],140);
    if(animation){raceHelpDismissal=animation;animation.onfinish=finish;}else finish();
  }
  $('#race-help-toggle').addEventListener('click',()=>setRaceHelpOpen($('#race-help-toggle').getAttribute('aria-expanded')!=='true'));
  $('#race-help-close').addEventListener('click',()=>setRaceHelpOpen(false,true));
  [$('#race-help-box'),$('#race-help-toggle')].forEach(element=>element.addEventListener('keydown',event=>{if(event.key==='Escape'){event.preventDefault();setRaceHelpOpen(false,true);}}));
  motionPreference?.addEventListener('change',()=>{if(!motionAllowed()){raceMotionFinish?.();raceHelpDismissal?.finish();}});
  let raceInspection=null;
  function renderRace(animate=true) {
    const inspection=animate?null:raceInspection,focusedMarker=document.activeElement?.dataset?.raceMarker;
    const view=data,markers=new Map();
    raceMotionFinish?.();
    raceController?.abort();raceController=new AbortController();
    const {signal}=raceController;
    $('#race-date').textContent='Current totals';
    $('#race-tooltip').hidden=true;
    const totalTiles=view.stats.rows*view.stats.columns,totalRows=view.stats.rows+view.stats.columns;
    const rowsAt=(entry,tileCount)=>tileCount?entry.marksData[tileCount-1].lines:0;
    function updateCounts(entry,tileCount,coordinate=13) {
      const point=view.atTime(entry.history,view.timeAt(coordinate));
      const completed=point?.completedTiles??0,lines=point?.completedLines??0;
      entry.count.textContent=`${completed}/${totalTiles}`;
      entry.rowsCount.textContent=`${lines}/${totalRows} rows`;
      entry.count.setAttribute('aria-label',`${completed} of ${totalTiles} tiles completed; ${point?.approved??0} of ${point?.target??entry.team.raw.progress.tiles.reduce((sum,tile)=>sum+tile.target,0)} contributions`);
      entry.rowsCount.setAttribute('aria-label',`${lines} of ${totalRows} rows and columns completed`);
      entry.count.title=entry.count.getAttribute('aria-label');
    }
    const host=$('#race-tracks'),panel=host.closest('.race-panel'),tooltip=$('#race-tooltip');host.replaceChildren();host.classList.remove('exploring');
    const entries=view.progress.map(entry=>({...entry,marksData:entry.marks})).sort((a,b)=>b.team.raw.progress.completedTiles-a.team.raw.progress.completedTiles||a.team.id.localeCompare(b.team.id));
    function stamp(day) {return view.date(view.timeAt(day),true);}
    [...$('.race-axis>div').children].forEach((label,index)=>label.textContent=view.date(view.timeAt([0,6,13][index])));
    function cursor(day) {
      raceInspection={time:view.timeAt(day),marker:null};
      raceMotionFinish?.();
      host.classList.add('exploring');host.style.setProperty('--race-cursor',`${day/13*100}%`);
      entries.forEach(entry=>updateCounts(entry,entry.times.filter(time=>time<=day).length,day));
      $('#race-date').textContent=stamp(day);
    }
    function reset() {
      raceInspection=null;
      raceMotionFinish?.();
      host.classList.remove('exploring');tooltip.hidden=true;
      host.querySelectorAll('.selected').forEach(mark=>mark.classList.remove('selected'));
      entries.forEach(entry=>updateCounts(entry,entry.times.length));
      $('#race-date').textContent='Current totals';
    }
    entries.forEach(entry=>{
      const {team,times}=entry;
      const row=document.createElement('div');row.className='race-row';row.style.setProperty('--team-color',`var(${team.color})`);
      const label=document.createElement('span');label.className='race-team';label.textContent=team.name;label.title=team.name;
      const track=document.createElement('div');track.className='race-track';
      entry.marks=[];
      const line=document.createElement('span');entry.line=line;line.className='race-line';line.style.width=`${(times.at(-1)??0)/13*100}%`;track.append(line);
      [0,6/13*100,100].forEach(position=>{const guide=document.createElement('span');guide.className='race-guide';guide.style.left=`${position}%`;track.append(guide);});
      const crosshair=document.createElement('span');crosshair.className='race-cursor';track.append(crosshair);
      times.forEach((day,index)=>{
        const button=document.createElement('button');button.type='button';button.className='race-mark';
        const mark=entry.marksData[index],gained=mark.gained,finished=mark.finished;
        const markerKey=JSON.stringify([team.id,mark.tileId||'official',mark.at]);button.dataset.raceMarker=markerKey;
        if(gained)button.classList.add('rows-completed');if(finished)button.classList.add('finished');
        if(gained>1){const badge=document.createElement('span');badge.className='race-row-gain';badge.textContent=`+${gained}`;badge.setAttribute('aria-hidden','true');button.append(badge);}
        button.style.left=`${day/13*100}%`;entry.marks.push(button);
        const detail=`${team.name} · ${stamp(day)} · ${mark.name} · ${mark.tiles}/${totalTiles} tiles · ${rowsAt(entry,index+1)}/${totalRows} rows${gained?` · ${gained} ${gained===1?'row':'rows'} completed`:''}${finished?' · Board completed':''} · ${mark.approved}/${mark.target} contributions`;
        button.setAttribute('aria-label',detail);
        const show=()=>{
          cursor(day);raceInspection.marker=markerKey;host.querySelectorAll('.selected').forEach(mark=>mark.classList.remove('selected'));button.classList.add('selected');
          tooltip.replaceChildren();
          if(mark.asset){const image=document.createElement('img');image.src=mark.asset;image.alt='';tooltip.append(image);}
          const copy=document.createElement('div'),title=document.createElement('strong'),meta=document.createElement('span'),result=document.createElement('span');
          title.textContent=mark.name;meta.textContent=`${team.name} · ${stamp(day)}`;result.textContent=`${mark.tiles}/${totalTiles} tiles · ${rowsAt(entry,index+1)}/${totalRows} rows${gained?` · ${gained} ${gained===1?'row':'rows'} completed`:''}${finished?' · Board completed':''} · ${mark.approved}/${mark.target} contributions`;
          copy.append(title,meta,result);tooltip.append(copy);tooltip.hidden=false;
          const bounds=panel.getBoundingClientRect(),markBox=button.getBoundingClientRect();
          tooltip.style.left=`${Math.max(8,Math.min(markBox.left-bounds.left-tooltip.offsetWidth/2,panel.clientWidth-tooltip.offsetWidth-8))}px`;
          tooltip.style.top=`${markBox.top-bounds.top-tooltip.offsetHeight-8}px`;
          $('#race-detail').textContent=detail;
        };
        markers.set(markerKey,{button,show});
        button.addEventListener('focus',show);button.addEventListener('click',show);
        button.addEventListener('pointermove',event=>{event.stopPropagation();show();});
        track.append(button);
      });
      track.addEventListener('pointermove',event=>{
        const bounds=track.getBoundingClientRect();cursor(Math.max(0,Math.min(13,(event.clientX-bounds.left)/bounds.width*13)));
        tooltip.hidden=true;host.querySelectorAll('.selected').forEach(mark=>mark.classList.remove('selected'));
      });
      const count=document.createElement('span');count.className='race-count';entry.count=count;
      const rowsCount=document.createElement('span');rowsCount.className='race-count race-rows-count';entry.rowsCount=rowsCount;
      updateCounts(entry,times.length);
      const totals=document.createElement('div');totals.className='race-totals';totals.append(count,rowsCount);
      row.append(label,track,totals);host.append(row);
    });
    // One shared time sweep keeps all teams, markers and totals in sync.
    // Interaction finishes the entrance immediately so hover/focus owns the chart.
    if(animate&&motionAllowed()&&typeof requestAnimationFrame==='function') {
      let frameId=null,started=null;
      const paint=day=>entries.forEach(entry=>{
        entry.line.style.width=`${Math.min(day,entry.times.at(-1)??0)/13*100}%`;
        entry.marks.forEach((mark,index)=>{mark.style.opacity=String(Math.max(0,Math.min(1,(day-entry.times[index])/.18)));});
        updateCounts(entry,entry.times.filter(time=>time<=day).length,day);
      });
      const finish=()=>{
        cancelAnimationFrame(frameId);raceMotionFinish=null;
        entries.forEach(entry=>{
          entry.line.style.width=`${(entry.times.at(-1)??0)/13*100}%`;
          entry.marks.forEach(mark=>{mark.style.opacity='';});
          updateCounts(entry,entry.times.length);
        });
      };
      raceMotionFinish=finish;paint(0);
      const frame=now=>{
        if(started===null)started=now;
        const progress=Math.min(1,(now-started)/1200);
        paint(13*progress);
        if(progress<1)frameId=requestAnimationFrame(frame);else finish();
      };
      frameId=requestAnimationFrame(frame);
    }
    raceInspection=null;
    if(inspection&&inspection.time>=view.start&&inspection.time<=view.end){
      const marker=markers.get(inspection.marker);
      if(marker){if(focusedMarker===inspection.marker)marker.button.focus({preventScroll:true});else marker.show();}
      else cursor((inspection.time-view.start)/(view.end-view.start)*13);
    }
    host.closest('.race-scroll').addEventListener('scroll',reset,{passive:true,signal});
    host.addEventListener('scroll',reset,{passive:true,signal});
    host.addEventListener('pointerleave',reset,{signal});
    host.addEventListener('focusout',event=>{if(!host.contains(event.relatedTarget))reset();},{signal});
    panel.addEventListener('keydown',event=>{if(event.key==='Escape')reset();},{signal});
    document.addEventListener('pointerdown',event=>{if(!panel.contains(event.target))reset();},{signal});
  }
  let timelineSnapshot=null,timelineAnimations=[];
  function animateTimeline(element,frames,duration,delay=0,easing='cubic-bezier(.2,.7,.2,1)') {
    if(!motionAllowed()||!element?.animate)return null;
    const animation=element.animate(frames,{duration,delay,fill:'backwards',easing});
    timelineAnimations.push(animation);return animation;
  }
  motionPreference?.addEventListener('change',()=>{
    if(motionAllowed())return;
    timelineAnimations.forEach(animation=>animation.finish());timelineAnimations=[];
  });

  function timelineEntries(selected='all') {return data.milestones(selected);}
  function renderTimeline() {
    const selected=$('#timeline-filter').value,host=$('#timeline');
    const entries=timelineEntries(selected),latest=entries.filter(item=>item.reached).at(-1)?.id;
    const signature=JSON.stringify([selected,entries]);
    if(timelineSnapshot?.signature===signature)return;
    const previous=timelineSnapshot,sameScope=previous?.selected===selected;
    const before=new Map([...host.children].map(row=>[row.dataset.milestone,row.getBoundingClientRect().left]));
    const scrollLeft=sameScope?host.scrollLeft:0;
    timelineAnimations.forEach(animation=>animation.cancel());timelineAnimations=[];
    host.replaceChildren();host.scrollLeft=scrollLeft;
    const rendered=[];
    entries.forEach(item=>{
      const row=document.createElement('li');row.className=`milestone${item.reached?'':' pending'}${item.missed?' missed':''}${item.id===latest?' current':''}`;
      if(item.id===latest)row.setAttribute('aria-current','step');
      row.dataset.milestone=item.id;row.dataset.state=item.reached?'reached':item.missed?'missed':'pending';
      const time=document.createElement(item.reached?'time':'span');time.className='milestone-stamp';
      if(item.reached){
        time.dateTime=item.at;
        const day=document.createElement('span');day.textContent=data.date(Date.parse(item.at));
        const clock=new Intl.DateTimeFormat(document.documentElement.lang||'en',{timeZone:data.stats.timezone,hour:'2-digit',minute:'2-digit',hourCycle:'h23'}).format(new Date(item.at));
        time.append(day,document.createTextNode(clock));
      }else time.textContent=item.missed?'Not reached':'Upcoming';
      const rail=document.createElement('span');rail.className='milestone-rail';rail.setAttribute('aria-hidden','true');
      const dot=document.createElement('span');dot.className='milestone-dot';rail.append(dot);
      let progress=null;if(item.reached&&item.id!==latest){progress=document.createElement('span');progress.className='milestone-progress';rail.append(progress);}
      if(item.missed){const cross=node('svg',{viewBox:'0 0 8 8',width:6,height:6,'aria-hidden':'true'});cross.append(node('path',{d:'M1 1L7 7M7 1L1 7',fill:'none',stroke:'currentColor','stroke-width':1.3,'stroke-linecap':'round'}));dot.append(cross);}
      const copy=document.createElement('div');copy.className='milestone-copy';
      const meta=document.createElement('div');meta.className='milestone-meta';meta.append(time);
      const text=document.createElement('div'),title=document.createElement('h3'),detail=document.createElement('p');
      title.textContent=item.title;detail.title=item.detail;
      const detailText=document.createElement('span');detailText.textContent=item.detail;detail.append(detailText);
      if(item.asset){
        detail.className='milestone-drop-detail';
        const image=document.createElement('img');image.src=item.asset;image.alt=item.dropName;image.title=item.dropName;image.className='milestone-inline-drop';detail.append(image);
      }
      row.setAttribute('aria-label',`${item.title}. ${item.reached?`${time.dateTime}. ${item.detail}${item.dropName?`. ${item.dropName}`:''}`:item.missed?'Not reached before the event ended':'Not reached yet'}`);
      text.append(title,detail);copy.append(text);row.append(meta,rail,copy);host.append(row);
      rendered.push({item,row,dot,progress});
    });
    host.scrollLeft=scrollLeft;
    const lines=rendered.filter(entry=>entry.progress);
    if(!previous||!sameScope){
      if(previous)animateTimeline(host,[{opacity:0},{opacity:1}],280);
      const duration=1400,segmentDuration=duration/Math.max(1,lines.length);
      lines.forEach(({progress},index)=>animateTimeline(progress,[{transform:'translateY(-50%) scaleX(0)'},{transform:'translateY(-50%) scaleX(1)'}],segmentDuration,index*segmentDuration,'linear'));
      rendered.filter(({item})=>item.reached).forEach(({item,dot},index)=>{
        animateTimeline(dot,[{backgroundColor:'var(--surface)',borderColor:'var(--muted)'},{backgroundColor:'var(--blue)',borderColor:'var(--blue)'}],160,Math.max(0,index*segmentDuration-80));
        if(item.id===latest)animateTimeline(dot,[{boxShadow:'none'},{boxShadow:'0 0 0 2px var(--surface),0 0 0 4px var(--blue)'}],180,lines.length?duration:0);
      });
    }else{
      const newlyReached=new Set(entries.filter(item=>item.reached&&!previous.entries.find(old=>old.id===item.id)?.reached).map(item=>item.id));
      rendered.forEach(({item,row,dot,progress},index)=>{
        const oldLeft=before.get(item.id),newLeft=row.getBoundingClientRect().left;
        if(oldLeft!==undefined&&Math.abs(oldLeft-newLeft)>.5)animateTimeline(row,[{transform:`translateX(${oldLeft-newLeft}px)`},{transform:'translateX(0)'}],280);
        if(newlyReached.has(item.id))animateTimeline(dot,[{backgroundColor:'var(--surface)',borderColor:'var(--muted)'},{backgroundColor:'var(--blue)',borderColor:'var(--blue)'}],180);
        const oldIndex=previous.entries.findIndex(old=>old.id===item.id);
        const oldNext=previous.entries[oldIndex+1];
        if(progress&&newlyReached.size&&(newlyReached.has(item.id)||!oldNext?.reached))animateTimeline(progress,[{transform:'translateY(-50%) scaleX(0)'},{transform:'translateY(-50%) scaleX(1)'}],300,100);
        if(latest!==previous.latest){
          const ring='0 0 0 2px var(--surface),0 0 0 4px var(--blue)';
          if(item.id===previous.latest)animateTimeline(dot,[{boxShadow:ring},{boxShadow:'none'}],140);
          if(item.id===latest)animateTimeline(dot,[{boxShadow:'none'},{boxShadow:ring}],180,newlyReached.size?300:0);
        }
      });
    }
    timelineSnapshot={signature,selected,entries,latest};
  }
  $('#timeline-filter').addEventListener('change',renderTimeline);
  function seriesColors(dark) {
    teams.slice(5).forEach((team,index)=>root.style.setProperty(team.color,`hsl(${(index*137.508+185)%360} 45% ${dark?67:39}%)`));
    contributors.filter(p=>p.color.startsWith('--gp-player-')).forEach((player,index)=>root.style.setProperty(player.color,`hsl(${(index*137.508+35)%360} 45% ${dark?67:39}%)`));
  }
  let artworkEditor;
  function setupArtworkEditor() {
    const live=$('.repeat-drop-card'),dialog=$('#artwork-editor');
    if(!dialog)return {apply:key=>paint(live,data.artwork.get(key)?.fit)};
    const fields=['x','y','scale','rotation'];
    let editSession=0,editorVersion=null,item=null,draft=null,preview=null,defaults=null,resetRequested=false,drag=null;
    const clamp=(value,min,max)=>Math.max(min,Math.min(max,value));
    function validFit(value) {
      if(!value||!['x','y','width','height','scale','rotation'].every(key=>Number.isFinite(value[key])))return null;
      return {x:clamp(value.x,0,100),y:clamp(value.y,0,100),width:clamp(value.width,5,150),height:clamp(value.height,5,200),scale:clamp(value.scale,.5,2.5),rotation:clamp(value.rotation,-180,180)};
    }
    function paint(card,fit) {paintArtwork(card,fit);}
    function defaultFit() {
      const style=getComputedStyle(live),width=live.clientWidth,height=live.clientHeight;
      const artWidth=parseFloat(style.getPropertyValue('--repeat-default-width'));
      const artHeight=parseFloat(style.getPropertyValue('--repeat-default-height'));
      const right=parseFloat(style.getPropertyValue('--repeat-default-right'));
      const bottom=parseFloat(style.getPropertyValue('--repeat-default-bottom'));
      return validFit({x:(width-right-artWidth/2)/width*100,y:(height-bottom-artHeight/2)/height*100,width:artWidth/width*100,height:artHeight/height*100,scale:1,rotation:0});
    }
    function apply(key) {paint(live,data.artwork.get(key)?.fit);}
    function refresh() {
      paint(preview,draft);
      fields.forEach(key=>{
        const value=key==='scale'?Math.round(draft.scale*100):Math.round(draft[key]*10)/10;
        $(`#artwork-${key}`).value=String(value);
        $(`#artwork-${key}-value`).textContent=`${value}${key==='rotation'?'°':'%'}`;
      });
      $('#artwork-editor-error').hidden=true;
    }
    function finishDrag(event) {
      if(!drag||event.pointerId!==drag.pointerId)return;
      drag=null;preview.classList.remove('dragging');
      if(preview.hasPointerCapture(event.pointerId))preview.releasePointerCapture(event.pointerId);
    }
    function open() {
      item=data.stats.repeatedItem?.item.itemId;if(!item||!data.artwork.has(item))return;
      if(pendingSaves){$('#stats-feedback').textContent='Settings are still saving. Wait for the save to finish.';return;}
      refreshRevision++;editSession++;editorVersion=data.artwork.get(item).version;defaults=defaultFit();draft={...(data.artwork.get(item)?.fit||defaults)};resetRequested=false;drag=null;
      $('#artwork-editor-item').textContent=data.stats.repeatedItem.item.name;
      preview=live.cloneNode(true);preview.classList.add('artwork-preview-card');
      preview.removeAttribute('aria-labelledby');preview.removeAttribute('id');
      preview.querySelectorAll('[id]').forEach(element=>element.removeAttribute('id'));
      preview.setAttribute('tabindex','0');preview.setAttribute('role','group');
      preview.setAttribute('aria-label','Artwork placement preview. Drag to move, or use arrow keys. Shift moves in larger steps.');
      $('#artwork-preview-frame').replaceChildren(preview);
      preview.addEventListener('pointerdown',event=>{
        if(event.button!==0||drag)return;
        event.preventDefault();preview.focus();
        drag={pointerId:event.pointerId,clientX:event.clientX,clientY:event.clientY,x:draft.x,y:draft.y};
        preview.setPointerCapture(event.pointerId);preview.classList.add('dragging');
      });
      preview.addEventListener('pointermove',event=>{
        if(!drag||event.pointerId!==drag.pointerId)return;
        const bounds=preview.getBoundingClientRect();
        draft.x=clamp(drag.x+(event.clientX-drag.clientX)/bounds.width*100,0,100);
        draft.y=clamp(drag.y+(event.clientY-drag.clientY)/bounds.height*100,0,100);
        resetRequested=false;refresh();
      });
      ['pointerup','pointercancel','lostpointercapture'].forEach(type=>preview.addEventListener(type,finishDrag));
      preview.addEventListener('keydown',event=>{
        if(!['ArrowLeft','ArrowRight','ArrowUp','ArrowDown'].includes(event.key))return;
        event.preventDefault();const amount=event.shiftKey?5:1;
        if(event.key==='ArrowLeft')draft.x=clamp(draft.x-amount,0,100);
        if(event.key==='ArrowRight')draft.x=clamp(draft.x+amount,0,100);
        if(event.key==='ArrowUp')draft.y=clamp(draft.y-amount,0,100);
        if(event.key==='ArrowDown')draft.y=clamp(draft.y+amount,0,100);
        resetRequested=false;refresh();
      });
      refresh();dialog.showModal();
    }
    fields.forEach(key=>$(`#artwork-${key}`).addEventListener('input',event=>{
      if(!draft)return;
      draft[key]=Number(event.target.value)/(key==='scale'?100:1);
      draft=validFit(draft);resetRequested=false;refresh();
    }));
    $('#adjust-artwork').addEventListener('click',open);
    ['#artwork-close','#artwork-cancel'].forEach(selector=>$(selector).addEventListener('click',()=>dialog.close()));
    dialog.addEventListener('close',()=>{draft=null;drag=null;preview=null;$('#artwork-preview-frame').replaceChildren();$('#adjust-artwork').focus();});
    $('#artwork-reset').addEventListener('click',()=>{draft={...defaults};resetRequested=true;refresh();});
    $('#artwork-save').addEventListener('click',async()=>{
      if(!draft||$('#artwork-save').disabled||pendingSaves)return;
      const savingItem=item,savingSession=editSession,savingName=data.stats.repeatedItem.item.name;beginSettingsSave();
      try {
        const response=await postForm($('#stats-artwork-form'),{itemId:item,expectedVersion:editorVersion,reset:resetRequested,...draft});
        data.artwork.set(savingItem,response.artwork);data.payload.preference.version=response.accountVersion;
        apply(data.stats.repeatedItem?.item.itemId);$('#stats-feedback').textContent=`Artwork settings saved for ${savingName}.`;if(editSession===savingSession)dialog.close();
      } catch(error) {
        if(dialog.open){$('#artwork-editor-error').textContent=error.message;$('#artwork-editor-error').hidden=false;}
        else $('#stats-feedback').textContent=error.message;
      }
      finally {finishSettingsSave();}
    });
    document.querySelectorAll('[data-artwork-view]').forEach(button=>button.addEventListener('click',()=>{
      $('#artwork-preview').dataset.view=button.dataset.artworkView;
      document.querySelectorAll('[data-artwork-view]').forEach(option=>option.setAttribute('aria-pressed',String(option===button)));
    }));
    apply(data.stats.repeatedItem?.item.itemId);
    return {apply};
  }
  function applyTheme() {
    const theme=document.documentElement.dataset.publicTheme==='dark'?'dark':'light';
    if(root.dataset.theme===theme)return;
    root.dataset.theme=theme;seriesColors(theme==='dark');
    // Recolor existing nodes: keep reveal masks, motion, focus and scroll intact.
    const series=new Map([...teams,...contributors,...everyone].map(entry=>[entry.id,entry]));
    chart.querySelectorAll('[data-series]').forEach(element=>{
      const entry=series.get(element.dataset.series);if(!entry)return;
      element.setAttribute(element.tagName.toLowerCase()==='path'||element.tagName.toLowerCase()==='line'?'stroke':'fill',color(entry));
    });
    lastChart.forEach(item=>{if(series.has(item.id))item.stroke=color(series.get(item.id));});
    current().forEach(entry=>{
      const button=[...$('#gp-legend').querySelectorAll('button[data-series]')].find(button=>button.dataset.series===entry.id);
      const dot=button?.querySelector('.dot');if(dot)dot.style.background=color(entry);
    });
    gpBreakdown().forEach(entry=>{
      const segment=[...$('#gp-donut').querySelectorAll('[data-team]')].find(segment=>segment.dataset.team===entry.id);
      segment?.setAttribute('stroke',color(entry));
      const row=[...$('#share-legend').querySelectorAll('[data-team]')].find(row=>row.dataset.team===entry.id);
      const dot=row?.querySelector('.share-dot');if(dot)dot.style.background=color(entry);
    });
    chartSignature=gpChartSignature();
    legendSignature=JSON.stringify([mode,selectedTeamId,pinnedPlayerId,current().map(entry=>[entry.id,color(entry)])]);
  }
  let toastTimer;
  function announce(text){clearTimeout(toastTimer);$('#toast').textContent=text;$('#toast').hidden=false;toastTimer=setTimeout(()=>{$('#toast').hidden=true;},2600);}
  // Integration-only bootstrap, persistence and refresh. The section renderers above
  // retain their approved DOM, geometry, interaction ownership and motion timings.
  function paintArtwork(card,fit) {
    card.dataset.artworkCustom=String(Boolean(fit));
    if(!fit)return;
    ['x','y','width','height'].forEach(key=>card.style.setProperty(`--artwork-${key}`,`${fit[key]}%`));
    card.style.setProperty('--artwork-scale',String(fit.scale));
    card.style.setProperty('--artwork-rotation',`${fit.rotation}deg`);
  }
  async function postForm(form,fields) {
    const values=new URLSearchParams(new FormData(form));
    Object.entries(fields).forEach(([key,value])=>values.set(key,String(value)));
    const response=await fetch(form.action,{method:'POST',body:values,headers:{Accept:'application/json'}}).catch(()=>{throw new Error('Could not save. Check your connection and try again.');});
    if(!response.ok){
      let error;try{error=(await response.json()).error;}catch{}
      throw new Error(error||(response.status===400?'The settings could not be saved. Check the values or reload the page and try again.':response.status===403||response.status===401?'You no longer have permission to save these settings.':response.status===404?'This event or item is no longer available. Reload the page before editing again.':'Could not save. Check your connection and try again.'));
    }
    return response.json();
  }
  function renderSummary() {
    const repeat=data.stats.repeatedItem,card=$('.repeat-drop-card'),asset=repeat?data.itemImage(repeat.item):null;
    card.dataset.itemId=repeat?.item.itemId||'';
    card.style.setProperty('--repeat-drop-art',asset?`url(${JSON.stringify(asset)})`:'none');
    const img=$('.repeat-drop-content img');img.hidden=!asset;if(asset)img.src=asset;
    $('.repeat-drop-stat>strong').textContent=repeat?String(repeat.count):'—';
    $('.repeat-drop-label h3').textContent=repeat?.item.name||'No approved drops yet';
    artworkEditor?.apply(repeat?.item.itemId);
    if($('#adjust-artwork'))$('#adjust-artwork').disabled=!repeat||!data.artwork.has(repeat.item.itemId);
    const versatile=data.stats.mostVersatile;
    $('.versatile-content h3').textContent=versatile?.name||'No contributions yet';
    $('.versatile-team').textContent=teams.find(team=>team.id===versatile?.teamId)?.name||'';
    $('.versatile-stat strong').textContent=versatile?String(versatile.distinctTiles):'—';
  }
  function populateTeams() {
    const filter=$('#timeline-filter'),selected=filter.value;filter.replaceChildren();
    [['all','All teams'],...teams.map(team=>[team.id,team.name])].forEach(([value,name])=>{
      const option=document.createElement('option');option.value=value;option.textContent=name;filter.append(option);
    });
    filter.value=teams.some(team=>team.id===selected)?selected:'all';
  }
  let refreshing=false,refreshAgain=false,lastPayloadSignature,refreshRevision=0,pendingSaves=0;
  const refreshBlocked=()=>pendingSaves>0||$('#artwork-editor')?.open;
  const contentSignature=payload=>JSON.stringify([payload.stats,payload.tiles,payload.artwork,payload.preference,payload.currentMembership]);
  const payloadSignature=payload=>JSON.stringify([contentSignature(payload),payload.stats.endedAt?null:payload.generatedAt]);
  let entranceUntil=0,clockRefreshTimer=null;
  function sectionSignatures(view) {
    const roster=view.stats.teams.map(team=>[team.teamId,team.name]);
    return {
      roster:JSON.stringify(roster),
      gp:JSON.stringify([view.stats.value,view.stats.valueHistory,view.stats.teams.map(team=>[team.teamId,team.name,team.value,team.eventValueShare,team.valueHistory,team.players]),view.drops,view.currentPlayerId,view.stats.timezone]),
      luck:JSON.stringify([roster,view.stats.luck]),
      race:JSON.stringify([roster,view.stats.rows,view.stats.columns,view.stats.timezone,view.stats.teams.map(team=>[team.progress,team.progressHistory,team.officialCompletion]),view.payload.tiles]),
      domain:JSON.stringify([view.start,view.end,view.dates]),
      raceDomain:JSON.stringify([view.start,view.end]),
      summary:JSON.stringify([roster,view.stats.repeatedItem,view.stats.mostVersatile,[...view.artwork]])
    };
  }
  function resumeRefresh() {if(refreshAgain&&!refreshing&&!refreshBlocked())void refreshStats();}
  function beginSettingsSave() {
    refreshRevision++;pendingSaves++;
    ['#artwork-save','#stats-hide-guidance'].forEach(selector=>{const button=$(selector);if(button)button.disabled=true;});
  }
  function finishSettingsSave() {
    refreshRevision++;pendingSaves--;
    ['#artwork-save','#stats-hide-guidance'].forEach(selector=>{const button=$(selector);if(button)button.disabled=false;});
    if($('#adjust-artwork'))$('#adjust-artwork').disabled=!data.stats.repeatedItem||!data.artwork.has(data.stats.repeatedItem.item.itemId);
    resumeRefresh();
  }
  async function refreshStats() {
    if(refreshing||refreshBlocked()){refreshAgain=true;return;}
    refreshing=true;refreshAgain=false;const revision=refreshRevision;
    try {
      const response=await fetch(root.dataset.refreshUrl,{headers:{Accept:'application/json'},cache:'no-store'}).catch(()=>{throw new Error('Stats could not refresh. Check your connection and try again.');});
      if(!response.ok)throw new Error(response.status===404?'Stats is no longer available for this event. Reload the page.':'Stats could not refresh. Reload or try again.');
      const payload=await response.json();
      if(revision!==refreshRevision||refreshBlocked()){refreshAgain=true;return;}
      const signature=payloadSignature(payload);
      if(signature===lastPayloadSignature)return;
      // A startup WatchEvent reply often differs only by its server clock. Let the
      // approved entrances finish, then fetch anew through the same save/draft fence.
      if(motionAllowed()&&performance.now()<entranceUntil&&contentSignature(payload)===contentSignature(data.payload)){
        if(clockRefreshTimer===null)clockRefreshTimer=setTimeout(()=>{clockRefreshTimer=null;void refreshStats();},entranceUntil-performance.now());
        return;
      }
      const next=window.StatsAdapter.adapt(payload),before=sectionSignatures(data),after=sectionSignatures(next);
      const gpChanged=before.gp!==after.gp,luckChanged=before.luck!==after.luck,domainChanged=before.domain!==after.domain;
      const scrolls=['#share-legend','#luck-rows','#gp-legend','.race-scroll','#timeline'].map(selector=>({element:$(selector),top:$(selector).scrollTop,left:$(selector).scrollLeft}));
      const page={left:window.scrollX,top:window.scrollY};
      const search={gp:$('#gp-divider-search').dataset.open==='true',gpText:$('#gp-player-search').value,luck:!$('#luck-search-popover').hidden,luckText:$('#luck-player-search').value};
      const active=document.activeElement,focusId=active?.id;
      const inspectedTime=activeDay===null?null:data.dates[activeDay];
      const searchFocus=active?.closest?.('#gp-search-results')?'gp':active?.closest?.('#luck-search-results')?'luck':null;
      const playerId=active?.dataset?.team||active?.dataset?.series;
      data=next;({teams,players,everyone,luck,contributors}=data);
      if($('#stats-hide-guidance'))$('#stats-hide-guidance').checked=payload.preference.hidden;
      if(selectedTeamId&&!teams.some(team=>team.id===selectedTeamId))selectedTeamId=null;
      if(luckTeamId&&!teams.some(team=>team.id===luckTeamId))luckTeamId=null;
      if(inspectedTime!==null)activeDay=nearestDate((inspectedTime-data.start)/(data.end-data.start));
      if(before.roster!==after.roster)populateTeams();
      seriesColors(root.dataset.theme==='dark');
      if(gpChanged){legendSignature=null;renderGp();}else if(domainChanged)drawChart();
      if(luckChanged)renderLuck();
      if(before.race!==after.race||before.raceDomain!==after.raceDomain)renderRace(false);
      renderTimeline();
      if(before.summary!==after.summary)renderSummary();
      if(gpChanged&&search.gp&&mode==='players'){setSearchOpen(true);$('#gp-player-search').value=search.gpText;searchPlayers();}
      if(luckChanged&&search.luck&&luckMode==='players'){$('#luck-search-popover').hidden=false;$('#luck-search-toggle').setAttribute('aria-expanded','true');$('#luck-player-search').value=search.luckText;searchLuckPlayers();}
      const target=focusId?document.getElementById(focusId):playerId?[...root.querySelectorAll('[data-team],[data-series]')].find(el=>el.dataset.team===playerId||el.dataset.series===playerId):null;
      if(!root.contains(active))(target||(searchFocus==='gp'?$('#gp-player-search'):searchFocus==='luck'?$('#luck-player-search'):null))?.focus({preventScroll:true});
      scrolls.forEach(({element,top,left})=>{element.scrollTop=top;element.scrollLeft=left;});
      window.scrollTo({...page,behavior:'instant'});
      lastPayloadSignature=signature;$('#stats-feedback').textContent='Stats updated.';
    } catch(error){if(revision!==refreshRevision||refreshBlocked())refreshAgain=true;else $('#stats-feedback').textContent=error.message;}
    finally {refreshing=false;resumeRefresh();}
  }
  function initializeProduction() {
    entranceUntil=performance.now()+1400;
    root.dataset.theme=document.documentElement.dataset.publicTheme==='dark'?'dark':'light';
    populateTeams();seriesColors(root.dataset.theme==='dark');
    artworkEditor=setupArtworkEditor();renderGp();renderLuck();renderRace();renderTimeline();renderSummary();
    if(data.payload.preference.hidden){
      $('#gp-help').open=false;
      for(const section of ['luck','race']){$(`#${section}-help-box`).hidden=true;$(`#${section}-help-toggle`).setAttribute('aria-expanded','false');}
    }
    const checkbox=$('#stats-hide-guidance');
    checkbox?.addEventListener('change',async()=>{
      if(pendingSaves)return;
      beginSettingsSave();
      try {
        const result=await postForm($('#stats-guidance-form'),{hidden:checkbox.checked,expectedVersion:data.payload.preference.version});
        data.payload.preference={...data.payload.preference,...result};
        if(result.hidden){dismissGpHelp();setLuckHelpOpen(false);setRaceHelpOpen(false);}
        else {$('#gp-help').open=true;setLuckHelpOpen(true);setRaceHelpOpen(true);}
        $('#stats-feedback').textContent='Tooltip preference saved.';
      } catch(error){checkbox.checked=data.payload.preference.hidden;$('#stats-feedback').textContent=error.message;}
      finally {finishSettingsSave();}
    });
    $('#artwork-editor')?.addEventListener('close',resumeRefresh);
    lastPayloadSignature=payloadSignature(data.payload);
    new MutationObserver(applyTheme).observe(document.documentElement,{attributes:true,attributeFilter:['data-public-theme']});
    window.addEventListener('focus',refreshStats);
    document.addEventListener('visibilitychange',()=>{if(!document.hidden)void refreshStats();});

    // Reuse the public progress hub and the shell's connection when present. It only
    // invalidates this view; the route supplies a new authoritative DTO snapshot.
    if(window.signalR){
      const shared=window.bingoProgressConnection;
      const connection=shared||new window.signalR.HubConnectionBuilder().withUrl('/hubs/progress').withAutomaticReconnect().build();
      const watch=()=>connection.invoke('WatchEvent',root.dataset.statsEvent).then(refreshStats).catch(()=>{});
      connection.on('progressChanged',refreshStats);connection.onreconnected(watch);
      if(shared){
        const waitForConnection=()=>{if(connection.state===window.signalR.HubConnectionState.Connected)void watch();else if(connection.state!==window.signalR.HubConnectionState.Disconnected)setTimeout(waitForConnection,100);};
        waitForConnection();
      }
      else connection.start().then(watch).catch(()=>{});
    }
  }

  initializeProduction();
  let contentLayoutFrame=null;
  function scheduleContentLayout() {
    if(contentLayoutFrame!==null)return;
    contentLayoutFrame=requestAnimationFrame(()=>{contentLayoutFrame=null;fitLuckLabels();fitDonutTotal();});
  }
  const contentObserver=new ResizeObserver(scheduleContentLayout);
  contentObserver.observe($('.luck-panel'));contentObserver.observe($('.share-ring'));
  window.addEventListener('resize',scheduleContentLayout);
  document.fonts?.ready.then(scheduleContentLayout);
  document.fonts?.addEventListener('loadingdone',scheduleContentLayout);
  let pending;
  new ResizeObserver(()=>{cancelAnimationFrame(pending);pending=requestAnimationFrame(drawChart);}).observe(chart);
})();

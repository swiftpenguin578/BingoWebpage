(() => {
  'use strict';
  const $ = (selector) => document.querySelector(selector);
  const svgNS = 'http://www.w3.org/2000/svg';
  // Deliberately fixed concept data; no pricing or luck calculation is implied.
  const baseTeams = [
    {id:'agency', name:'The Agency', color:'--blue', values:[0,8,14,28,50,90,120,1320,1340,1350,1370,1380,1390,1400]},
    {id:'saeh', name:'Såeh CS?', color:'--coral', values:[0,4,14,24,42,1022,1030,1038,1045,1060,1070,1080,1090,1100]},
    {id:'monkeys', name:'Morytania Monkeys', color:'--sage', values:[0,26,54,72,132,160,195,210,248,280,590,610,625,640]},
    {id:'xeno', name:'Xeno’s Drops', color:'--bronze', values:[0,18,34,54,72,110,135,168,180,240,286,330,365,420]},
    {id:'zala', name:'Zalamikum', color:'--purple', values:[0,10,24,46,68,83,100,135,159,187,220,240,254,280]}
  ];
  const basePlayers = [
    {id:'maya',name:'Maya',color:'--blue',values:[0,0,0,0,0,0,0,1200,1200,1200,1200,1200,1200,1200]},
    {id:'chris',name:'Calm Chris',color:'--coral',values:[0,0,0,0,0,980,980,980,980,980,980,980,980,980]},
    {id:'agent',name:'Agent Slidt',color:'--sage',values:[0,0,0,0,0,0,0,0,0,0,310,310,310,310]},
    {id:'rasmus',name:'Rasmus',color:'--bronze',values:[0,8,16,30,42,60,78,86,110,144,162,180,202,235]},
    {id:'crunch',name:'crunch704',color:'--purple',values:[0,10,16,24,42,55,64,79,96,119,134,153,172,180]}
  ];
  const baseLuck = {
    teams:[['Såeh CS?',-32],['The Agency',-18],['Morytania Monkeys',8],['Xeno’s Drops',24],['Zalamikum',46]],
    players:[['Rasmus',-38],['crunch704',-21],['Agent Slidt',12],['Calm Chris',34],['Maya',48]]
  };
  // Deterministic fixtures for comparing layouts, not event calculations.
  function sampleEvent(size) {
    const extraNames=['Barrows Brothers','Falador Knights','Lumbridge Legends','Prifddinas Crew','Karamja Crew','Varrock Wanderers','Fremennik Raiders','Desert Scorpions','Draynor Willows','Taverley Titans'];
    const teams=baseTeams.slice(0,size);
    for(let index=5;index<size;index++) {
      const total=190+(index-5)*73;
      teams.push({id:`sample-${index+1}`,name:extraNames[index-5],color:`--sample-team-${index+1}`,
        values:Array.from({length:14},(_,day)=>Math.round(total*Math.pow(day/13,1+(index%4)*.35)))});
    }
    const players=basePlayers.slice(0,size);
    teams.slice(5).forEach((team,index)=>players.push({id:`sample-player-${index+6}`,name:`Adventurer ${index+6}`,color:team.color,values:team.values.map(value=>Math.round(value*.65))}));
    const everyone=[{id:'everyone',name:'Everyone',color:'--blue',values:teams[0].values.map((_,day)=>teams.reduce((sum,team)=>sum+team.values[day],0))}];
    const originalLuck=[-18,-32,8,24,46];
    const luck={teams:teams.map((team,index)=>[team.name,originalLuck[index]??((index*17)%101)-50]).sort((a,b)=>a[1]-b[1]),
      players:players.map((player,index)=>[player.name,baseLuck.players.find(([name])=>name===player.name)?.[1]??((index*23)%101)-50]).sort((a,b)=>a[1]-b[1])};
    return {teams,players,everyone,luck};
  }
  let {teams,players,everyone,luck}=sampleEvent(5);
  let raceController;
  let mode='teams', hidden=new Set(), activeDay=null, geometry=null, luckMode='teams';
  const chart=$('#gp-chart'), tip=$('#chart-tip'),comparisonClear=$('#gp-clear-comparison');
  const money=(value,precise=false)=>value>=1000 ? `${(value/1000).toFixed(precise ? 3 : 2).replace(/0+$/,'').replace(/\.$/,'')}B` : `${Number(value.toFixed(1))}M`;
  const axisMoney=(value)=>value===0?'0':value>=1000?`${(value/1000).toFixed(value%1000?1:0)}B`:`${value}M`;
  const color=(series)=>getComputedStyle(document.documentElement).getPropertyValue(series.color).trim();
  // Drop-value-only fixtures: contributor curves partition each team's recorded GP.
  // These samples do not change the approved Luck or Board progress fixtures.
  function buildContributors(eventTeams) {
    const names=['Northstar','Rune Runner','Ash','Winter Fox','Oak','Ember','Rune Finch'];
    const weights=[24,20,17,14,11,8,6];
    return eventTeams.flatMap((team,index)=>{
      const lead=basePlayers[index] || {id:`gp-lead-${team.id}`,name:`Adventurer ${index+1}`,color:team.color,values:team.values.map(v=>Math.floor(v*.65))};
      const leadValues=index<3?lead.values:team.values.map(v=>v*lead.values.at(-1)/team.values.at(-1));
      const rest=team.values.map((v,day)=>v-leadValues[day]);
      return [{...lead,values:leadValues,teamId:team.id,teamName:team.name},...weights.map((weight,member)=>{
        return {id:`gp-${team.id}-${member}`,name:`${names[member]} ${index+1}`,teamId:team.id,teamName:team.name,color:`--gp-player-${index}-${member}`,
          values:rest.map(v=>v*weight/100)};
      })];
    });
  }
  let contributors=buildContributors(teams);
  let selectedTeamId=null,pinnedPlayerId=null,hoveredPlayerId=null,focusedPlayerId=null;
  let legendSignature=null;
  const motionPreference=window.matchMedia?.('(prefers-reduced-motion: reduce)');
  let chartSignature=null,lastChart=[],lastDonut=[],lastGpScope=null,helpDismissal=null;
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
    return [...ranked.slice(0,5),...(ranked.length>5?[{id:'others',name:'Others',color:'--gp-others',values:Array.from({length:14},(_,day)=>ranked.slice(5).reduce((sum,p)=>sum+p.values[day],0))}]:[])];
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
    // Local illustrative items, scoped to the team. Hovering does not alter this strip.
    const items=[['Twisted bow','twisted-bow.png',1200],['Tumeken’s shadow','tumekens-shadow.png',980],['Torva platebody','torva-platebody.png',310],['Bandos chestplate','bandos-chestplate.png'],['Berserker ring','berserker-ring.png']];
    const drops=teams.flatMap((team,index)=>{
      const roster=contributors.filter(p=>p.teamId===team.id);
      const first=items[index]||['Dragon warhammer','dragon-warhammer-detail.png'];
      return [{name:first[0],asset:first[1],value:first[2]??Math.round(roster[0].values.at(-1)*.7),player:roster[0]},
        {name:'Dragon warhammer',asset:'dragon-warhammer-detail.png',value:Math.max(1,Math.round(roster[1].values.at(-1)*.7)),player:roster[1]},
        {name:'Abyssal whip',asset:'abyssal-whip.png',value:Math.max(1,Math.round(roster[2].values.at(-1)*.5)),player:roster[2]}];
    }).filter(drop=>!selectedTeamId||drop.player.teamId===selectedTeamId).sort((a,b)=>b.value-a.value).slice(0,3);
    const list=$('.gp-panel .treasure-list');list.replaceChildren();
    drops.forEach(drop=>{
      const article=document.createElement('article');article.className='treasure-item';
      const img=document.createElement('img');img.className='item-art';img.src=`assets/items/${drop.asset}`;img.alt=drop.name;
      const details=document.createElement('div');details.className='item-details';
      const title=document.createElement('h3');title.textContent=drop.name;
      const value=document.createElement('div');value.className='item-value';value.textContent=`${drop.value>=1000?(drop.value/1000).toFixed(2)+'B':money(drop.value)} GP`;
      const byline=document.createElement('p');byline.className='item-byline';byline.textContent=`${drop.player.name} · ${drop.player.teamName}`;
      details.append(title,value,byline);article.append(img,details);list.append(article);
    });
  }
  function renderGp() {
    const team=selectedTeam(),isPlayers=playerView();
    $('.gp-panel').dataset.gpView=team?'team':mode;
    setTabs($('#gp-tabs'),mode);
    $('#gp-team-label').textContent=team?.name||'';$('#gp-team-label').hidden=!team;
    $('#gp-back').hidden=!team;
    $('#gp-shortcut').hidden=mode==='everyone'||Boolean(team);
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
    const totalCount=Math.round(169*teams.length/5),index=teams.findIndex(t=>t.id===selectedTeamId);
    $('#sample-drop-count').textContent=String(team?Math.floor(totalCount/teams.length)+(index<totalCount%teams.length?1:0):totalCount);
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
  $('#gp-back').addEventListener('click',()=>{selectedTeamId=null;resetComparison();renderGp();$('#gp-shortcut').focus();});
  $('#gp-shortcut').addEventListener('click',()=>{
    if(mode==='teams'){openTeam('agency');return;}
    const maya=contributors.find(p=>p.id==='maya');hidden.delete(maya.id);updateComparison();
    const row=[...$('#share-legend').querySelectorAll('.share-row')].find(el=>el.dataset.team===maya.id);row?.focus();
    announce('Maya is your illustrative player, already in the top five.');
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
  function renderDonut() {
    const donut=$('#gp-donut'),legend=$('#share-legend'),entries=gpBreakdown();
    const total=entries.reduce((sum,entry)=>sum+entry.values.at(-1),0);
    donut.replaceChildren(node('title',{},playerView()?'Player contributions':'Team contributions'));
    donut.setAttribute('role','group');donut.setAttribute('aria-label',playerView()?'Player GP shares':'Team GP shares');
    legend.setAttribute('aria-label',playerView()?'Player GP shares':'Team GP shares');
    legend.replaceChildren();$('#share-total-value').textContent=money(total);
    const previous=lastDonut;lastDonut=[];
    let start=-Math.PI/2;
    entries.forEach((entry,index)=>{
      const value=entry.values.at(-1),fraction=value/total,end=start+fraction*Math.PI*2,gap=Math.min(.016,fraction*Math.PI*.2),radius=73;
      const shape=donutArc(start+gap,end-gap);
      const label=`${entry.name}: ${money(value)} GP · ${(fraction*100).toFixed(1)}%`;
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
      const percent=document.createElement('span');percent.className='share-percent';percent.textContent=`${(fraction*100).toFixed(1)}%`;
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
  function drawChart() {
    const width=Math.max(250,chart.clientWidth),height=chart.clientHeight;
    const scope=`${mode}/${selectedTeamId||''}`;
    const signature=JSON.stringify([scope,width,height,current().filter(series=>!hidden.has(series.id)).map(series=>[series.id,series.values,color(series)])]);
    if(signature===chartSignature){highlightGp();return;}
    chartSignature=signature;
    const previous=lastChart,initial=!previous.length,scopeChanged=previous.length&&previous[0].scope!==scope;
    const resized=previous.length&&(previous[0].width!==width||previous[0].height!==height);
    lastChart=[];
    const left=width<450?37:43,right=width<450?43:53,top=17,bottom=31;
    const plotWidth=width-left-right,plotHeight=height-top-bottom;
    const peak=Math.max(...current().map(series=>series.values.at(-1)));
    const magnitude=10**Math.floor(Math.log10(peak));
    const step=peak/magnitude<=2?magnitude/2:magnitude,ceiling=Math.ceil(peak/step)*step;
    const x=(index)=>left+index/13*plotWidth;
    const y=(value)=>top+plotHeight-value/ceiling*plotHeight;
    geometry={width,height,left,right,top,bottom,plotWidth,plotHeight,x,y};
    chart.setAttribute('viewBox',`0 0 ${width} ${height}`);
    chart.replaceChildren();
    chart.append(node('title',{},`GP over time, ${mode}. Sample data from September 1 to 14.`));
    chart.append(node('desc',{},'Left and right arrow keys move between dates. Escape closes the values. Legend buttons show or hide series.'));
    for(let value=0;value<=ceiling;value+=step) {
      chart.append(node('line',{x1:left,y1:y(value),x2:width-right,y2:y(value),class:'chart-grid'}));
      chart.append(node('text',{x:left-8,y:y(value)+4,'text-anchor':'end',class:'axis-text'},axisMoney(value)));
    }
    const dates=width<480?[0,6,13]:[0,3,6,9,13];
    for(const index of dates) {
      chart.append(node('text',{x:x(index),y:height-8,'text-anchor':'middle',class:'axis-text'},`Sep ${String(index+1).padStart(2,'0')}`));
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
      if(playerView()&&Math.abs(labelY-y(series.values[13])-4)>2)chart.append(node('line',{x1:x(13)+3,y1:y(series.values[13]),x2:x(13)+7,y2:labelY-4,stroke:color(series),'stroke-width':.7,'data-series':series.id}));
      const path=series.values.map((value,index)=>`${index?'L':'M'}${x(index).toFixed(2)},${y(value).toFixed(2)}`).join(' ');
      const line=node('path',{d:path,pathLength:1,stroke:color(series),class:'chart-line','data-series':series.id});
      const dot=node('circle',{cx:x(13),cy:y(series.values[13]),r:4.2,fill:color(series),'data-series':series.id});
      const label=node('text',{x:x(13)+8,y:labelY,fill:color(series),'font-family':'Geist, sans-serif','font-size':width<450?10:11,'font-weight':650,class:'end-value','data-series':series.id},money(series.values[13]));
      chart.append(line,dot,label);
      const before=previous.find(item=>item.id===series.id)||(scopeChanged?previous[index]:null);
      if(initial){
        animateGp(line,[{strokeDasharray:'1',strokeDashoffset:'1'},{strokeDasharray:'1',strokeDashoffset:'0'}],1100,'ease-in-out');
        [dot,label].forEach(element=>animateGp(element,[{opacity:0},{opacity:0,offset:.7},{opacity:1}],1100));
      }else if(before&&!resized){
        animateSvg(line,'d',before.path,path);
        animateSvg(dot,'cy',before.cy,y(series.values[13]));
        animateSvg(label,'y',before.labelY,labelY);
      }else if(!before){
        [line,dot,label].forEach(element=>animateGp(element,[{opacity:0},{opacity:1}],180));
      }
      lastChart.push({id:series.id,path,cy:y(series.values[13]),labelY,stroke:color(series),scope,width,height});
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
    activeDay=Math.max(0,Math.min(13,index));
    const {x,y,top,height,bottom,width}=geometry,cursor=$('#chart-cursor');
    cursor.replaceChildren(node('line',{x1:x(activeDay),x2:x(activeDay),y1:top,y2:height-bottom,stroke:'var(--muted)','stroke-dasharray':'3 4','stroke-width':1,opacity:.55}));
    const shown=current().filter(series=>!hidden.has(series.id));
    tip.replaceChildren();
    const date=document.createElement('div');date.className='tip-date';date.textContent=`September ${activeDay+1}, 2026`;tip.append(date);
    shown.forEach(series=>{
      cursor.append(node('circle',{cx:x(activeDay),cy:y(series.values[activeDay]),r:4.5,fill:color(series),stroke:'var(--surface)','stroke-width':2}));
      const row=document.createElement('div');row.className='tip-row';
      const name=document.createElement('span');name.className='tip-name';
      const dot=document.createElement('span');dot.className='dot';dot.style.background=color(series);
      name.append(dot,document.createTextNode(series.name));
      const value=document.createElement('strong');value.textContent=`${money(series.values[activeDay])} GP`;
      row.append(name,value);tip.append(row);
    });
    tip.hidden=false;
    const tipWidth=tip.offsetWidth;
    const rawLeft=x(activeDay)>width*.58?x(activeDay)-tipWidth-14:x(activeDay)+14;
    tip.style.left=`${Math.max(0,Math.min(width-tipWidth,rawLeft))}px`;
    tip.style.top='15px';
    if(readAloud) $('#chart-announcement').textContent=`September ${activeDay+1}. ${shown.map(s=>`${s.name}: ${money(s.values[activeDay])} GP`).join('. ')}`;
  }
  chart.addEventListener('pointermove',event=>{
    if(event.pointerType==='touch')return;
    const rect=chart.getBoundingClientRect();
    showDay(Math.round((event.clientX-rect.left-geometry.left)/geometry.plotWidth*13),false);
  });
  chart.addEventListener('pointerleave',event=>{if(event.pointerType!=='touch')hideTip();});
  chart.addEventListener('click',event=>{
    const rect=chart.getBoundingClientRect();
    showDay(Math.round((event.clientX-rect.left-geometry.left)/geometry.plotWidth*13),true);
  });
  chart.addEventListener('keydown',event=>{
    if(event.key==='Escape'){hideTip();return;}
    if(!['ArrowLeft','ArrowRight','Home','End'].includes(event.key))return;
    event.preventDefault();
    const next=event.key==='Home'?0:event.key==='End'?13:(activeDay??6)+(event.key==='ArrowLeft'?-1:1);
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
    // Full illustrative roster; no production luck formula is implied.
    return contributors.map((player,index)=>({...player,value:baseLuck.players.find(([name])=>name===player.name)?.[1]??((index*23+7)%101)-50}))
      .sort((a,b)=>a.value-b.value||a.name.localeCompare(b.name));
  }
  function luckExtremes(roster=luckPlayers()) {
    return roster.length<=10?roster:[...roster.slice(0,5),...roster.slice(-5)];
  }
  function paintLuckMotion({bar,number,value},progress) {
    bar.style.transform=`scaleX(${progress})`;
    const count=Math.round(value*progress),edge=50+Math.abs(value)/60*40*progress;
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
    $('#luck-status').textContent=`${player.name}: ${player.value>0?'+':''}${player.value}% luck${luckPinnedId===id?', pinned for comparison':''}.`;
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
    const entries=luckMode==='teams'?(team?roster.filter(player=>player.teamId===team.id):luck.teams.map(([name,value])=>({id:teams.find(team=>team.name===name).id,name,value}))):[...(pinned&&!extremes.some(player=>player.id===pinned.id)?[pinned]:[]),...extremes];
    list.replaceChildren();list.scrollTop=0;
    list.style.setProperty('--luck-visible-rows',Math.max(3,Math.min(6,teams.length)));
    list.setAttribute('aria-label',team?`${team.name} player luck`:luckMode==='players'?'Five unluckiest and five luckiest players':'Team luck');
    $('#luck-team-label').textContent=team?.name||'';$('#luck-team-label').hidden=!team;
    $('#luck-team-label').title=team?.name||'';$('#luck-back').hidden=!team;
    $('#luck-search-toggle').hidden=luckMode!=='players';setTabs($('#luck-tabs'),luckMode);
    $('#luck-help-text').textContent=luckMode==='players'?'Shows the five unluckiest and five luckiest players. Search and select a player to compare their luck against them.':'Click a team to explore its players. Use the back arrow to return to all teams.';
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
      if(luckMode==='players'&&roster.length>10&&entry.id===extremes[5].id)row.classList.add('luck-group-start');
      if(isPinned)row.classList.add('luck-pinned-row');
      row.setAttribute('aria-label',`${name}: ${value>0?'+':''}${value}%${isPinned?', pinned comparison':''}`);
      const label=document.createElement(luckMode==='teams'&&!team?'button':'span');label.className='luck-name';label.textContent=name;
      label.title=entry.teamName?`${name} · ${entry.teamName}`:name;
      if(luckMode==='teams'&&!team){label.type='button';label.setAttribute('aria-label',`Explore ${name} players`);label.addEventListener('click',()=>openLuckTeam(entry.id));}
      const track=document.createElement('div');track.className='luck-track';track.setAttribute('aria-hidden','true');
      if(luckMode==='teams'&&!team){track.style.cursor='pointer';track.addEventListener('click',()=>openLuckTeam(entry.id));}
      const width=Math.abs(value)/60*40;
      const bar=document.createElement('span');bar.className=`luck-bar${value<0?' negative':''}`;
      bar.style.width=`${width}%`;bar.style.left=`${value<0?50-width:50}%`;bar.style.transformOrigin=value<0?'right center':'left center';
      const number=document.createElement('span');number.className=`luck-value${value<0?' negative':''}`;
      number.textContent=`${value>0?'+':''}${value}%`;
      if(value<0)number.style.right=`calc(${50+width}% + 7px)`;else number.style.left=`calc(${50+width}% + 7px)`;
      track.append(bar,number);row.append(label,track);
      if(isPinned){const clear=document.createElement('button');clear.type='button';clear.className='luck-clear-pin';clear.textContent='×';clear.setAttribute('aria-label',`Remove ${name} comparison`);clear.addEventListener('click',()=>removeLuckComparison(row));const nameText=document.createElement('span');nameText.className='luck-pinned-name';nameText.textContent=name;label.replaceChildren(nameText,clear);}
      list.append(row);moving.push({bar,number,value,id:entry.id});if(isPinned)pinnedRow=row;
    });
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
  function renderRace() {
    raceMotionFinish?.();
    raceController?.abort();raceController=new AbortController();
    const {signal}=raceController;
    $('#race-date').textContent='Current totals';
    $('#race-tooltip').hidden=true;
    // Illustrative completion times, in days since the sample event began.
    const completions=[
      [1.65,2.3,3.1,3.4,4.2,4.3,5.8,6.1,6.3,7.4,8.2,8.5,9.1,9.4,10.3,11.1,11.3,12.1,12.7],
      [.8,1.2,1.8,2.1,2.5,2.7,3.05,3.12,3.4,3.55,3.63,4.2,4.6,5.1,5.8,6.2,6.8,7.3,8.1,8.5,9.4,10.2,11.1,11.8,12.3],
      [2.1,2.6,3.7,4.5,4.7,5.2,6.4,7.1,8.2,8.35,8.5,9.8,10.4,11.3,12.1,12.8],
      [2.3,3.4,4.1,5.2,6.2,6.4,6.6,8.1,9.3,10.2,11.1,11.4,12.6],
      [3.2,4.3,5.2,6.3,7.1,8.3,9.1,9.4,10.7,11.8,12.5]
    ];
    const tiles=['Araxxor','Vorkath','Chambers of Xeric','Dragon warhammer','Torva collection','Tumeken’s shadow','Zulrah','The Gauntlet','Dagannoth Kings','Bandos','Saradomin','Zamorak','Armadyl','Nex','The Nightmare','Corporeal Beast','The Leviathan','The Whisperer','Duke Sucellus','Vardorvis','Cerberus','Thermonuclear smoke devil','Alchemical Hydra','Theatre of Blood','Tombs of Amascut'];
    // Sample 5×5 board with 12 scoring rows; gains keyed by completion index.
    const totalRows=12;
    const rowGains=[{14:1,16:1,18:2},{10:1,14:1,17:2,19:1,21:2,23:2,24:3},{12:1,15:1},{11:1,12:1},{9:1}];
    const rowsAt=(entry,tileCount)=>Object.entries(entry.rowGains).reduce((sum,[index,gain])=>sum+(Number(index)<tileCount?gain:0),0);
    function updateCounts(entry,tileCount) {
      entry.count.textContent=`${tileCount}/25`;
      entry.rowsCount.textContent=`${rowsAt(entry,tileCount)}/${totalRows} rows`;
      entry.count.setAttribute('aria-label',`${tileCount} of 25 tiles completed`);
      entry.rowsCount.setAttribute('aria-label',`${rowsAt(entry,tileCount)} of ${totalRows} rows completed`);
    }
    const host=$('#race-tracks'),panel=host.closest('.race-panel'),tooltip=$('#race-tooltip');host.replaceChildren();host.classList.remove('exploring');
    const entries=teams.map((team,index)=>({team,times:completions[index]??Array.from({length:7+(index*3)%18},(_,tile)=>Number((.5+(tile+1)/(8+(index*3)%18)*12.3).toFixed(2))),rowGains:rowGains[index]??{8:1,12:1,17:2}})).sort((a,b)=>b.times.length-a.times.length);
    const artwork={'Araxxor':'objectives/araxxor.png','Vorkath':'objectives/vorkath.png','Chambers of Xeric':'objectives/chambers-of-xeric.png','Dragon warhammer':'items/dragon-warhammer-detail.png','Torva collection':'items/torva-platebody.png','Tumeken’s shadow':'items/tumekens-shadow.png'};
    function stamp(day) {
      const minutes=Math.round(day*1440);
      return `Sep ${String(Math.floor(minutes/1440)+1).padStart(2,'0')}, ${String(Math.floor(minutes%1440/60)).padStart(2,'0')}:${String(minutes%60).padStart(2,'0')}`;
    }
    function cursor(day) {
      raceMotionFinish?.();
      host.classList.add('exploring');host.style.setProperty('--race-cursor',`${day/13*100}%`);
      entries.forEach(entry=>updateCounts(entry,entry.times.filter(time=>time<=day).length));
      $('#race-date').textContent=stamp(day);
    }
    function reset() {
      raceMotionFinish?.();
      host.classList.remove('exploring');tooltip.hidden=true;
      host.querySelectorAll('.selected').forEach(mark=>mark.classList.remove('selected'));
      entries.forEach(entry=>updateCounts(entry,entry.times.length));
      $('#race-date').textContent='Current totals';
    }
    entries.forEach(entry=>{
      const {team,times}=entry;
      const row=document.createElement('div');row.className='race-row';row.style.setProperty('--team-color',`var(${team.color})`);
      const label=document.createElement('span');label.className='race-team';label.textContent=team.name;
      const track=document.createElement('div');track.className='race-track';
      entry.marks=[];
      const line=document.createElement('span');entry.line=line;line.className='race-line';line.style.width=`${times.at(-1)/13*100}%`;track.append(line);
      [0,6/13*100,100].forEach(position=>{const guide=document.createElement('span');guide.className='race-guide';guide.style.left=`${position}%`;track.append(guide);});
      const crosshair=document.createElement('span');crosshair.className='race-cursor';track.append(crosshair);
      times.forEach((day,index)=>{
        const button=document.createElement('button');button.type='button';button.className='race-mark';
        const gained=entry.rowGains[index]||0,finished=times.length===25 && index===24;
        if(gained)button.classList.add('rows-completed');if(finished)button.classList.add('finished');
        if(gained>1){const badge=document.createElement('span');badge.className='race-row-gain';badge.textContent=`+${gained}`;badge.setAttribute('aria-hidden','true');button.append(badge);}
        button.style.left=`${day/13*100}%`;entry.marks.push(button);
        const detail=`${team.name} · ${stamp(day)} · ${tiles[index]} · ${index+1}/25 tiles · ${rowsAt(entry,index+1)}/${totalRows} rows${gained?` · ${gained} ${gained===1?'row':'rows'} completed`:''}${finished?' · Board completed':''}`;
        button.setAttribute('aria-label',detail);
        const show=()=>{
          cursor(day);host.querySelectorAll('.selected').forEach(mark=>mark.classList.remove('selected'));button.classList.add('selected');
          tooltip.replaceChildren();
          if(artwork[tiles[index]]){const image=document.createElement('img');image.src=`assets/${artwork[tiles[index]]}`;image.alt='';tooltip.append(image);}
          const copy=document.createElement('div'),title=document.createElement('strong'),meta=document.createElement('span'),result=document.createElement('span');
          title.textContent=tiles[index];meta.textContent=`${team.name} · ${stamp(day)}`;result.textContent=`${index+1}/25 tiles · ${rowsAt(entry,index+1)}/${totalRows} rows${gained?` · ${gained} ${gained===1?'row':'rows'} completed`:''}${finished?' · Board completed':''}`;
          copy.append(title,meta,result);tooltip.append(copy);tooltip.hidden=false;
          const bounds=panel.getBoundingClientRect(),mark=button.getBoundingClientRect();
          tooltip.style.left=`${Math.max(8,Math.min(mark.left-bounds.left-tooltip.offsetWidth/2,panel.clientWidth-tooltip.offsetWidth-8))}px`;
          tooltip.style.top=`${mark.top-bounds.top-tooltip.offsetHeight-8}px`;
          $('#race-detail').textContent=detail;
        };
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
    if(motionAllowed()&&typeof requestAnimationFrame==='function') {
      let frameId=null,started=null;
      const paint=day=>entries.forEach(entry=>{
        entry.line.style.width=`${Math.min(day,entry.times.at(-1))/13*100}%`;
        entry.marks.forEach((mark,index)=>{mark.style.opacity=String(Math.max(0,Math.min(1,(day-entry.times[index])/.18)));});
        updateCounts(entry,entry.times.filter(time=>time<=day).length);
      });
      const finish=()=>{
        cancelAnimationFrame(frameId);raceMotionFinish=null;
        entries.forEach(entry=>{
          entry.line.style.width=`${entry.times.at(-1)/13*100}%`;
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
    host.closest('.race-scroll').addEventListener('scroll',reset,{passive:true,signal});
    host.addEventListener('scroll',reset,{passive:true,signal});
    host.addEventListener('pointerleave',reset,{signal});
    host.addEventListener('focusout',event=>{if(!host.contains(event.relatedTarget))reset();},{signal});
    panel.addEventListener('keydown',event=>{if(event.key==='Escape')reset();},{signal});
    document.addEventListener('pointerdown',event=>{if(!panel.contains(event.target))reset();},{signal});
  }
  let timelineStage='current';
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

  const timelineCutoffs={start:.75,early:2,week:6.999,current:13.75,ended:14};
  const timelineDefaults=[
    ['start','Event started'],['submission','First submission'],['all-submissions','All teams submitted'],
    ['tile','First tile completed'],['all-tiles','All teams completed a tile'],['halfway','Halfway there'],
    ['row','First row completed'],['all-rows','All teams completed a row'],
    ['board','First board completed'],['all-boards','All teams completed the board'],['end','Event ended']
  ];
  function timelineTeamMoments() {
    // Compact illustrative snapshots aligned with the existing Board progress demo.
    const snapshots=[{"tile":1.65,"halfway":9.1,"row":10.3,"board":null},{"tile":0.8,"halfway":4.6,"row":3.63,"board":12.3},{"tile":2.1,"halfway":10.4,"row":10.4,"board":null},{"tile":2.3,"halfway":12.6,"row":11.4,"board":null},{"tile":3.2,"halfway":null,"row":11.8,"board":null}];
    const assets=['dragon-warhammer-detail.png','tumekens-shadow.png','torva-platebody.png','dragon-warhammer-detail.png','abyssal-whip.png'];
    const dropNames=['Dragon warhammer','Tumeken’s shadow','Torva platebody','Dragon warhammer','Abyssal whip'];
    return teams.map((team,index)=>{
      const count=7+(index*3)%18;
      const completion=tile=>tile<count?Number((.5+(tile+1)/(8+(index*3)%18)*12.3).toFixed(2)):null;
      const snapshot=snapshots[index]||{tile:completion(0),halfway:completion(12),row:completion(8),board:null};
      const player=contributors.find(player=>player.teamId===team.id);
      return {team,player,asset:assets[index]||'dragon-warhammer-detail.png',dropName:dropNames[index]||'Dragon warhammer',...snapshot,
        submission:index===0?.758333:Math.max(.76,snapshot.tile-.025)};
    });
  }
  function collectiveTimelineMoment(moments,key) {
    if(!moments.length||moments.some(item=>item[key]===null||item[key]===undefined))return null;
    return {at:Math.max(...moments.map(item=>item[key])),detail:`All ${moments.length} teams`};
  }
  function timelineEntries(selected='all',cutoff=timelineCutoffs[timelineStage]) {
    const all=timelineTeamMoments(),scoped=selected==='all'?all:all.filter(item=>item.team.id===selected);
    const first=key=>scoped.filter(item=>item[key]!==null).sort((a,b)=>a[key]-b[key])[0];
    const occurrences={start:{at:.75,detail:'Vinterbingo 2026'},end:{at:13+1439/1440,detail:''}};
    ['submission','tile','halfway','row','board'].forEach(key=>{
      const moment=first(key);if(!moment)return;
      const detail=key==='submission'?(selected==='all'?`${moment.player.name} · ${moment.team.name}`:moment.player.name)
        :key==='tile'?(selected==='all'?`${moment.team.name} · Araxxor`:'Araxxor')
        :key==='halfway'?(selected==='all'?`${moment.team.name} · 13/25`:'13/25 tiles')
        :key==='row'?(selected==='all'?`${moment.team.name} · Row 1`:'Row 1')
        :selected==='all'?moment.team.name:'25/25 tiles';
      occurrences[key]={at:moment[key],detail,asset:key==='submission'?moment.asset:null,dropName:key==='submission'?moment.dropName:null};
    });
    if(selected==='all')[['all-submissions','submission'],['all-tiles','tile'],['all-rows','row'],['all-boards','board']].forEach(([id,key])=>{
      const occurrence=collectiveTimelineMoment(all,key);if(occurrence)occurrences[id]=occurrence;
    });
    return timelineDefaults.filter(([id])=>selected==='all'||!id.startsWith('all-')).map(([id,title],order)=>{
      const occurrence=occurrences[id],reached=Boolean(occurrence&&occurrence.at<=cutoff);
      return {id,title:selected!=='all'&&id==='board'?'Board completed':title,order,reached,missed:!reached&&cutoff>=occurrences.end.at,at:reached?occurrence.at:null,detail:reached?occurrence.detail:'',asset:reached?occurrence.asset:null,dropName:reached?occurrence.dropName:null};
    }).sort((a,b)=>a.id==='start'||b.id==='start'?Number(b.id==='start')-Number(a.id==='start'):a.reached!==b.reached?(a.reached?-1:1):a.reached?a.at-b.at||a.order-b.order:a.order-b.order);
  }
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
        const minutes=Math.round(item.at*1440),dayNumber=Math.floor(minutes/1440)+1;
        const clock=`${String(Math.floor(minutes%1440/60)).padStart(2,'0')}:${String(minutes%60).padStart(2,'0')}`;
        time.dateTime=`2026-09-${String(dayNumber).padStart(2,'0')}T${clock}:00+02:00`;
        const day=document.createElement('span');day.textContent=`Sep ${String(dayNumber).padStart(2,'0')}`;
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
        const image=document.createElement('img');image.src=`assets/items/${item.asset}`;image.alt=item.dropName;image.title=item.dropName;image.className='milestone-inline-drop';detail.append(image);
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
  $('#timeline-stage').addEventListener('change',event=>{
    if(!Object.hasOwn(timelineCutoffs,event.target.value))return;
    timelineStage=event.target.value;renderTimeline();
    $('#sample-size-status').textContent=`Milestone preview: ${event.target.selectedOptions?.[0]?.textContent||timelineStage}. Other charts show their current sample totals.`;
  });
  function sampleColors(dark) {
    teams.slice(5).forEach((team,index)=>document.documentElement.style.setProperty(team.color,`hsl(${(index*137.508+185)%360} 45% ${dark?67:39}%)`));
    contributors.filter(p=>p.color.startsWith('--gp-player-')).forEach((player,index)=>document.documentElement.style.setProperty(player.color,`hsl(${(index*137.508+35)%360} 45% ${dark?67:39}%)`));
  }
  function setEventSize(size) {
    if(![2,3,4,5,8,15].includes(size))return;
    releaseGpLayout();
    closeLuckSearch();luckTeamId=null;luckPinnedId=null;luckHighlightId=null;
    ({teams,players,everyone,luck}=sampleEvent(size));
    document.documentElement.dataset.sampleSize=String(size);
    contributors=buildContributors(teams);selectedTeamId=null;resetComparison();sampleColors(document.documentElement.dataset.theme==='dark');
    const filter=$('#timeline-filter'),selected=filter.value;
    filter.replaceChildren();
    [['all','All teams'],...teams.map(team=>[team.id,team.name])].forEach(([value,name])=>{
      const option=document.createElement('option');option.value=value;option.textContent=name;filter.append(option);
    });
    filter.value=teams.some(team=>team.id===selected)?selected:'all';
    renderGp();renderLuck();renderRace();renderTimeline();
    ['#share-legend','#luck-rows','#race-tracks','#gp-legend','.race-scroll','#timeline'].forEach(selector=>{
      const element=$(selector);element.scrollTop=0;element.scrollLeft=0;
    });
    $('#sample-size-status').textContent=`Showing an illustrative event with ${size} teams.`;
  }
  $('#event-size').addEventListener('change',event=>setEventSize(Number(event.target.value)));
  const dropPreviews={
    warhammer:{name:'Dragon warhammer',count:18,asset:'dragon-warhammer-detail.png'},
    bow:{name:'Twisted bow',count:3,asset:'twisted-bow.png'},
    shadow:{name:'Tumeken’s shadow',count:7,asset:'tumekens-shadow.png'},
    torva:{name:'Torva platebody',count:12,asset:'torva-platebody.png'},
    whip:{name:'Abyssal whip',count:27,asset:'abyssal-whip.png'},
    bandos:{name:'Bandos chestplate',count:14,asset:'bandos-chestplate.png'},
    berserker:{name:'Berserker ring',count:36,asset:'berserker-ring.png'}
  };
  const artworkEditor=setupArtworkEditor();
  $('#drop-preview').addEventListener('change',event=>{
    const drop=dropPreviews[event.target.value];if(!drop)return;
    const card=$('.repeat-drop-card');
    card.style.setProperty('--repeat-drop-art',`url('assets/items/${drop.asset}')`);
    $('.repeat-drop-content img').src=`assets/items/${drop.asset}`;
    $('.repeat-drop-stat>strong').textContent=String(drop.count);
    $('.repeat-drop-label h3').textContent=drop.name;
    artworkEditor.apply(event.target.value);
    $('#sample-size-status').textContent=`Previewing ${drop.count} ${drop.name} drops.`;
  });
  function setupArtworkEditor() {
    const storageKey='bingo-stats-artwork-v1',live=$('.repeat-drop-card'),dialog=$('#artwork-editor');
    const fields=['x','y','scale','rotation'];
    let saved={},item=null,draft=null,preview=null,defaults=null,resetRequested=false,drag=null;
    const clamp=(value,min,max)=>Math.max(min,Math.min(max,value));
    function validFit(value) {
      if(!value||!['x','y','width','height','scale','rotation'].every(key=>Number.isFinite(value[key])))return null;
      return {x:clamp(value.x,0,100),y:clamp(value.y,0,100),width:clamp(value.width,5,150),height:clamp(value.height,5,200),scale:clamp(value.scale,.5,2.5),rotation:clamp(value.rotation,-180,180)};
    }
    try {
      const stored=JSON.parse(localStorage.getItem(storageKey)||'{}');
      Object.keys(dropPreviews).forEach(key=>{const fit=validFit(stored[key]);if(fit)saved[key]=fit;});
    } catch { /* Missing, malformed or unavailable storage keeps the original fitting. */ }
    function paint(card,fit) {
      card.dataset.artworkCustom=String(Boolean(fit));
      if(!fit)return;
      ['x','y','width','height'].forEach(key=>card.style.setProperty(`--artwork-${key}`,`${fit[key]}%`));
      card.style.setProperty('--artwork-scale',String(fit.scale));
      card.style.setProperty('--artwork-rotation',`${fit.rotation}deg`);
    }
    function defaultFit() {
      const style=getComputedStyle(live),width=live.clientWidth,height=live.clientHeight;
      const artWidth=parseFloat(style.getPropertyValue('--repeat-default-width'));
      const artHeight=parseFloat(style.getPropertyValue('--repeat-default-height'));
      const right=parseFloat(style.getPropertyValue('--repeat-default-right'));
      const bottom=parseFloat(style.getPropertyValue('--repeat-default-bottom'));
      return validFit({x:(width-right-artWidth/2)/width*100,y:(height-bottom-artHeight/2)/height*100,width:artWidth/width*100,height:artHeight/height*100,scale:1,rotation:0});
    }
    function apply(key) {paint(live,saved[key]);}
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
      item=$('#drop-preview').value;if(!dropPreviews[item])return;
      defaults=defaultFit();draft={...(saved[item]||defaults)};resetRequested=false;drag=null;
      $('#artwork-editor-item').textContent=dropPreviews[item].name;
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
    $('#artwork-save').addEventListener('click',()=>{
      if(!draft)return;
      const next={...saved};if(resetRequested)delete next[item];else next[item]={...draft};
      try {localStorage.setItem(storageKey,JSON.stringify(next));}
      catch {$('#artwork-editor-error').textContent='Could not save artwork settings. Browser storage is unavailable. Your previous settings are unchanged.';$('#artwork-editor-error').hidden=false;return;}
      saved=next;apply(item);$('#sample-size-status').textContent=`Artwork settings saved for ${dropPreviews[item].name}.`;dialog.close();
    });
    document.querySelectorAll('[data-artwork-view]').forEach(button=>button.addEventListener('click',()=>{
      $('#artwork-preview').dataset.view=button.dataset.artworkView;
      document.querySelectorAll('[data-artwork-view]').forEach(option=>option.setAttribute('aria-pressed',String(option===button)));
    }));
    apply($('#drop-preview').value);
    return {apply};
  }
  function applyTheme(dark) {
    document.documentElement.dataset.theme=dark?'dark':'light';
    sampleColors(dark);
    $('#theme-toggle').textContent=`Switch to ${dark?'light':'dark'} mode`;
    $('#header-theme').setAttribute('aria-label',`Switch to ${dark?'light':'dark'} mode`);
    try{localStorage.setItem('bingo-stats-prototype-theme',dark?'dark':'light');}catch{}
    hoveredPlayerId=null;focusedPlayerId=null;renderGp();
  }
  function toggleTheme(){applyTheme(document.documentElement.dataset.theme!=='dark');}
  $('#theme-toggle').addEventListener('click',toggleTheme);$('#header-theme').addEventListener('click',toggleTheme);
  let toastTimer;
  function announce(text){clearTimeout(toastTimer);$('#toast').textContent=text;$('#toast').hidden=false;toastTimer=setTimeout(()=>{$('#toast').hidden=true;},2600);}
  document.querySelectorAll('[data-static]').forEach(control=>control.addEventListener('click',event=>{
    event.preventDefault();announce(`${control.dataset.static} stays in the app. You’re viewing the Stats concept.`);
  }));
  let dark=false;try{dark=localStorage.getItem('bingo-stats-prototype-theme')==='dark';}catch{}
  renderLuck();renderRace();renderTimeline();applyTheme(dark);
  let pending;
  new ResizeObserver(()=>{cancelAnimationFrame(pending);pending=requestAnimationFrame(drawChart);}).observe(chart);
})();

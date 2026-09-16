const fs=require('node:fs'),path=require('node:path'),vm=require('node:vm'),assert=require('node:assert/strict'),test=require('node:test');
const root=path.resolve(__dirname,'../../src/Bingo.Web');
function fixture(size=5,missing=false){
 const start='2026-09-01T08:00:00Z',end='2026-09-14T08:00:00Z';
 const value=n=>({drops:n,knownValueGp:n*1000000,valueGp:missing?null:n*1000000,missingPrices:missing?1:0});
 const result=n=>({received:3,expected:2,percentage:missing?null:n,status:missing?'WaitingForActivityData':'Calculated',estimated:false,zeroRecordedApproximation:false});
 const teams=Array.from({length:size},(_,i)=>({teamId:`team-${i}`,name:`Team ${i} with a long public name <script>no</script>`,slug:`team-${i}`,value:value(28),eventValueShare:missing?null:100/size,
   valueHistory:[{at:end,value:value(28)}],mostValuableDrop:null,players:Array.from({length:7},(_,j)=>({playerId:`player-${i}-${j}`,name:`Player ${i} ${j}`,value:value(7-j),teamValueShare:missing?null:(7-j)/28*100,eventValueShare:missing?null:(7-j)/(28*size)*100,valueHistory:[{at:end,value:value(7-j)}]})),
   progress:{completedTiles:1,tiles:[{id:'tile-1',target:10,approved:10,complete:true,completedAt:end}]},progressHistory:[{at:end,completedTiles:1,completedLines:0,approved:10,target:40,boardComplete:false,tiles:[{id:'tile-1',complete:true}]}],milestones:[{id:'start',defaultOrder:0,state:'Reached',at:start},{id:'tile',defaultOrder:3,state:'Reached',at:end,tileId:'tile-1',teamId:`team-${i}`},{id:'end',defaultOrder:10,state:'Pending',at:null}]}));
 const stats={eventId:'event-id',name:'Synthetic <event>',timezone:'Europe/Copenhagen',rows:2,columns:2,startedAt:start,endedAt:null,evidenceRevision:9,value:value(size*28),valueHistory:[{at:end,value:value(size*28)}],teams,drops:[{submissionId:'submission-1',teamId:'team-0',playerId:'player-0-0',characterName:'Player 0 0',item:{itemId:'item-1',name:'Exact item <variant>',imageUrl:'https://oldschool.runescape.wiki/images/Dragon_warhammer.png'},valueGp:missing?null:1000000,submittedAt:end}],
 luck:{result:result(25),teams:teams.map((team,i)=>({teamId:team.teamId,name:team.name,result:result(i*10-20),players:team.players.map((player,j)=>({playerId:player.playerId,teamId:team.teamId,name:player.name,result:result((i*7+j)%51-25)}))})),stale:false,evidenceRevision:9,calculatedAt:end,upstreamUpdatedAt:end},milestones:teams[0]?.milestones||[],repeatedItem:size?{item:{itemId:'item-1',name:'Exact item',imageUrl:'https://oldschool.runescape.wiki/images/Dragon_warhammer.png'},count:1}:null,mostVersatile:size?{playerId:'player-0-0',teamId:'team-0',name:'Player 0 0',distinctTiles:1}:null};
 if(!size)stats.drops=[];
 return {stats,generatedAt:'2026-09-15T08:00:00Z',tiles:[{id:'tile-1',name:'Actual tile'}],artwork:[{itemId:'item-1',version:1,externalIdentifier:'13576',fit:null}],preference:{canSave:true,version:1,hidden:false},canEditArtwork:true,currentMembership:{playerId:'player-0-0',teamId:'team-0'}};
}
function harness(payload=fixture(),admin=true,viewportWidth=700,options={}){
 const frames=new Map(),animations=[],posts=[],listeners={},media={matches:false,addEventListener(type,fn){(listeners.motion??=[]).push(fn)}};let frameId=0,document,now=0,timerId=0,resolveFonts;const timers=new Map(),observers=[],resizeObservers=[],hubHandlers={};let reconnect;const hub={state:"Connected",calls:[],invoke(...args){this.calls.push(args);return Promise.resolve()},on(name,fn){hubHandlers[name]=fn},onreconnected(fn){reconnect=fn}};
 const camel=s=>s.replace(/-([a-z])/g,(_,c)=>c.toUpperCase());
 class Element {
  constructor(tag='div'){this.tagName=tag;this.children=[];this.attrs={};this.dataset={};this.handlers={};this.hidden=false;this.disabled=false;this.open=false;this.value='';this.checked=false;this._text='';this.style={setProperty(k,v){this[k]=String(v)},getPropertyValue(k){return this[k]||''}};this.scrollTop=0;this.scrollLeft=0;this.clientWidth=viewportWidth;this.clientHeight=270;this.offsetWidth=150;this.classList={contains:c=>this.className.split(' ').includes(c),add:(...cs)=>this.className=[...new Set([...this.className.split(' '),...cs])].join(' '),remove:(...cs)=>this.className=this.className.split(' ').filter(c=>!cs.includes(c)).join(' '),toggle:(c,on)=>{if(on??!this.classList.contains(c))this.classList.add(c);else this.classList.remove(c)}};}
  get className(){return this.attrs.class||''}set className(v){this.attrs.class=v}
  get id(){return this.attrs.id||''}set id(v){this.attrs.id=v}
  get textContent(){return this._text+this.children.map(c=>c.textContent).join('')}set textContent(v){this._text=String(v);this.children=[]}
  get parentElement(){return this.parent}get offsetHeight(){return 37}get offsetTop(){return this.parent?this.parent.children.indexOf(this)*37:0}
  append(...nodes){nodes.forEach(node=>{if(typeof node==='string')node=new Text(node);node.remove?.();node.parent=this;this.children.push(node)})}
  replaceChildren(...nodes){this.children.forEach(n=>n.parent=null);this.children=[];this._text='';this.append(...nodes)}
  setAttribute(k,v){this.attrs[k]=String(v);if(k.startsWith('data-'))this.dataset[camel(k.slice(5))]=String(v);if(k==='value')this.value=String(v);if(k==='hidden')this.hidden=true;if(k==='open')this.open=true;if(k==='disabled')this.disabled=true;}
  getAttribute(k){return this.attrs[k]??null}removeAttribute(k){delete this.attrs[k]}
  matches(selector){return selector.split(',').some(s=>{
   s=s.trim();if(!s)return false;
   const attrs=[...s.matchAll(/\[([^=\]]+)(?:=['"]?([^\]'"\s]+)['"]?)?\]/g)];s=s.replace(/\[[^\]]+\]/g,'');
   if(attrs.some(([,k,v])=>{const a=k.startsWith('data-')?this.dataset[camel(k.slice(5))]:this.getAttribute(k);return a==null||v!==undefined&&a!==v}))return false;
   const id=s.match(/#([\w-]+)/);if(id&&this.id!==id[1])return false;
   if([...s.matchAll(/\.([\w-]+)/g)].some(([,c])=>!this.classList.contains(c)))return false;
   const tag=s.match(/^[\w-]+/);return !tag||tag[0]===this.tagName;
  })}
  querySelectorAll(selector){
   const all=this.children.flatMap(c=>c instanceof Element?[c,...c.querySelectorAll('*')]:[]);
   return all.filter(el=>selector.split(',').some(select=>{
    const parts=select.trim().replace(/\s*>\s*/g,' > ').split(/\s+/);let cursor=el;
    if(!cursor.matches(parts.pop()))return false;
    while(parts.length){let wanted=parts.pop();if(wanted==='>'){wanted=parts.pop();cursor=cursor.parent;if(!cursor?.matches(wanted))return false;}else{cursor=cursor.parent;while(cursor&&!cursor.matches(wanted))cursor=cursor.parent;if(!cursor)return false;}}
    return true;
   }));
  }
  querySelector(s){return this.querySelectorAll(s)[0]||null}closest(s){return this.matches(s)?this:this.parent?.closest(s)||null}
  contains(el){return el===this||this.children.some(c=>c.contains?.(el))}
  addEventListener(type,fn,options){if(options?.signal?.aborted)return;(this.handlers[type]??=[]).push(fn);options?.signal?.addEventListener("abort",()=>{this.handlers[type]=this.handlers[type].filter(handler=>handler!==fn)})}
  async fire(type,extra={}){for(const fn of this.handlers[type]||[])await fn({target:this,currentTarget:this,preventDefault(){},stopPropagation(){},...extra})}
  focus(options){document.activeElement=this;this.focusOptions=options;void this.fire('focus')}
  scrollTo({top}){this.scrollTop=top}getBoundingClientRect(){
   if(this.style.visibility==='hidden'&&this.style.width==='max-content'){
    const fontSize=Number.parseFloat(this.style.fontSize)||(this.classList.contains('luck-value')?11:14);
    const factor=this.tagName==='strong'?.48:.56;
    return {left:0,top:0,width:this.textContent.length*fontSize*factor*(options.fontFactor||1),height:fontSize*(this.tagName==='strong'?1:1.5)};
   }
   const width=this.classList.contains('share-ring')?(options.ringWidth||150):this.classList.contains('luck-track')?(options.trackWidth||this.clientWidth):this.clientWidth;
   return {left:0,top:0,width,height:this.clientHeight};
  }
  animate(keyframes,options){const a={element:this,keyframes,options,finish(){this.finished=true;this.onfinish?.()},cancel(){this.cancelled=true;this.oncancel?.()}};animations.push(a);return a}
  beginElement(){this.started=now}getCurrentTime(){return now/1000}getStartTime(){return this.started/1000}getAnimations(){return animations.filter(a=>this.contains(a.element)&&!a.finished&&!a.cancelled)}endElement(){this.ended=true}
  remove(){if(this.parent)this.parent.children=this.parent.children.filter(c=>c!==this);this.parent=null}
  cloneNode(deep){const clone=new Element(this.tagName);Object.entries(this.attrs).forEach(([k,v])=>clone.setAttribute(k,v));clone._text=this._text;clone.clientWidth=this.clientWidth;if(deep)this.children.forEach(c=>clone.append(c.cloneNode(true)));return clone}
  showModal(){this.open=true}close(){this.open=false;void this.fire('close')}
  setPointerCapture(id){this.pointer=id}hasPointerCapture(id){return this.pointer===id}releasePointerCapture(){this.pointer=null}
 }
 class Text{constructor(text){this.textContent=text}cloneNode(){return new Text(this.textContent)}}
 document=new Element('document');document.documentElement=new Element('html');document.documentElement.lang='en';document.documentElement.dataset.publicTheme='light';document.append(document.documentElement);document.activeElement=null;document.createElement=t=>new Element(t);document.createElementNS=(_,t)=>new Element(t);document.createTextNode=t=>new Text(t);document.getElementById=id=>document.querySelector('#'+id);
 if(options.fonts)document.fonts={ready:new Promise(resolve=>{resolveFonts=resolve}),addEventListener(type,fn){(listeners.fonts??=[]).push(fn)}};
 let markup=fs.readFileSync(path.join(root,'Pages/Events/Stats.cshtml'),'utf8');markup=markup.slice(markup.indexOf('<div class="stats-page"'),markup.indexOf('<script id="stats-data"')).replace(/^<div[^\n]+>/,'<div class="stats-page">');
 if(!admin)markup=markup.replace(/<dialog[\s\S]*?<\/dialog>/,'').replace(/<button id="adjust-artwork"[\s\S]*?<\/button>/,'');
 const stack=[document.documentElement];for(const token of markup.match(/<[^>]*>|[^<]+/g)||[]){
  if(token.startsWith('</')){const tag=token.match(/^<\/([\w-]+)/)?.[1];if(stack.at(-1).tagName===tag)stack.pop();continue;}
  if(token.startsWith('<')){const tag=token.match(/^<([\w-]+)/)?.[1];if(!tag)continue;const el=new Element(tag);for(const [,key,a,b,c] of token.matchAll(/([\w:-]+)(?:=(?:"([^"]*)"|'([^']*)'|([^\s>]+)))?/g)){if(key===tag)continue;el.setAttribute(key,a??b??c??'');}stack.at(-1).append(el);if(!['input','img','br','link','meta'].includes(tag)&&!token.endsWith('/>'))stack.push(el);}
 }
 const page=document.querySelector('.stats-page');assert.ok(page);page.dataset.refreshUrl='/Events/synthetic/Stats?handler=Data';page.dataset.statsEvent='event-id';
 const bootstrap=new Element('script');bootstrap.id='stats-data';bootstrap.textContent=JSON.stringify(payload);page.append(bootstrap);
 const window={document,matchMedia:()=>media,scrollX:0,scrollY:123,scrollTo({left,top}){this.scrollX=left;this.scrollY=top},addEventListener(type,fn){(listeners[type]??=[]).push(fn)}};
 if(options.hub){window.signalR={HubConnectionState:{Connected:"Connected",Disconnected:"Disconnected"}};window.bingoProgressConnection=hub;}
 let nextPayload=payload;const deferred=[];
 const defer=(method='GET')=>{let resolve,reject;const promise=new Promise((yes,no)=>{resolve=yes;reject=no});const request={method,promise,resolve:(body,status=200)=>resolve({ok:status<400,status,json:async()=>body}),reject};deferred.push(request);return request};
 const scriptRoot=process.env.STATS_PASS5_SCRIPT_ROOT||path.join(root,'wwwroot/js');
 const context={window,document,Intl,URLSearchParams,AbortController,FormData:class{constructor(){}*[Symbol.iterator](){yield ['__RequestVerificationToken','test-token'];}},fetch:async(url,options)=>{posts.push({url,options});const index=deferred.findIndex(request=>request.method===(options?.method||'GET'));if(index>=0)return deferred.splice(index,1)[0].promise;return {ok:true,json:async()=>options?.method==='POST'?{hidden:true,version:2,artwork:{itemId:'item-1',version:2,fit:{x:25,y:50,width:20,height:50,scale:1.2,rotation:25}},accountVersion:2}:nextPayload}},getComputedStyle:el=>({getPropertyValue:key=>el.style[key]||({'--repeat-default-width':'140px','--repeat-default-height':'162px','--repeat-default-right':'-8px','--repeat-default-bottom':'-30px'}[key]||(({'--blue':0,'--coral':1,'--sage':2,'--bronze':3,'--purple':4,'--muted':5}[key]!==undefined)?`${page.dataset.theme==='dark'?'#ee':'#33'}${String({'--blue':0,'--coral':1,'--sage':2,'--bronze':3,'--purple':4,'--muted':5}[key]).repeat(4)}`:'#abcdef'))}),MutationObserver:class{constructor(callback){this.callback=callback}observe(target,options){observers.push({callback:this.callback,target,options})}},ResizeObserver:class{constructor(callback){this.callback=callback}observe(target){resizeObservers.push({callback:this.callback,target})}},requestAnimationFrame(fn){frames.set(++frameId,fn);return frameId},cancelAnimationFrame:id=>frames.delete(id),performance:{now:()=>now},setTimeout(fn,delay){timers.set(++timerId,{fn,at:now+delay});return timerId},clearTimeout:id=>timers.delete(id),console};vm.createContext(context);
 vm.runInContext(fs.readFileSync(path.join(scriptRoot,'stats-adapter.js'),'utf8'),context);
 let source=fs.readFileSync(path.join(scriptRoot,'stats-page.js'),'utf8');source=source.replace(/\}\)\(\);\s*$/,`globalThis.api={money,gpAmount,data:()=>data,current,rankedPlayers,gpBreakdown,openTeam,pinPlayer,previewPlayer,searchPlayers,renderGp,renderLuck,luckPlayers,luckExtremes,selectLuckPlayer,finishLuckMotion,openLuckTeam,searchLuckPlayers,renderRace,renderTimeline,refreshStats,applyTheme,scale:()=>luckScale,finishRace:()=>raceMotionFinish?.(),setMode(value){mode=value;selectedTeamId=null;resetComparison();renderGp()},setLuckMode(value){luckMode=value;luckTeamId=null;renderLuck()},state:()=>({mode,selectedTeamId,pinnedPlayerId,luckTeamId,luckPinnedId}),setData(payload){data=window.StatsAdapter.adapt(payload);({teams,contributors,players,everyone,luck}=data);},showDay};})();`);
 const names=['drawChart','renderGp','renderDonut','renderLuck','renderRace','renderTimeline','renderSummary'];
 source=source.replace('  initializeProduction();',`globalThis.calls={};${names.map(name=>`const original_${name}=${name};${name}=(...args)=>{globalThis.calls.${name}=(globalThis.calls.${name}||0)+1;return original_${name}(...args)};`).join('')}
  initializeProduction();`);
 vm.runInContext(source,context);
 return {api:context.api,calls:context.calls,defer,document,page,window,frames,animations,media,posts,listeners,timers,hub,finishFonts:()=>resolveFonts?.(),resize(selector){resizeObservers.filter(observer=>observer.target===document.querySelector(selector)).forEach(observer=>observer.callback([{target:observer.target}]))},async reconnect(){reconnect();await settle()},async progress(){await hubHandlers.progressChanged()},theme(value){document.documentElement.dataset.publicTheme=value;observers.filter(o=>o.target===document.documentElement&&o.options.attributeFilter.includes("data-public-theme")).forEach(o=>o.callback([{type:"attributes",attributeName:"data-public-theme",target:document.documentElement}]))},advance(time){now=time;for(const [id,timer] of timers)if(timer.at<=now){timers.delete(id);timer.fn()}},setResponse:p=>nextPayload=p,tick(time){now=time;const pending=[...frames.values()];frames.clear();pending.forEach(fn=>fn(time))}};
}
test('GP formatting uses K below one million and preserves raw GP, M and B',()=>{
 const {money,gpAmount}=harness().api;
 for(const [value,label] of [[null,'Unavailable'],[0,'0 GP'],[.000999,'999 GP'],[.001,'1K'],[.0147,'14.7K'],[.9999,'999.9K'],[1,'1M'],[1.5,'1.5M'],[1000,'1B'],[1234.567,'1.23B']])assert.equal(money(value),label);
 assert.equal(money(1234.567,true),'1.235B');assert.equal(gpAmount(.0147),'14.7K GP');assert.equal(gpAmount(.000999),'999 GP');
});
for(const count of [0,2,3,4,5,8,15])test(`actual production bootstrap, ${count} teams, all demo controls absent`,()=>{
 const h=harness(fixture(count),false);for(const id of ['event-size','timeline-stage','drop-preview','header-theme','theme-toggle','sample-size-status'])assert.equal(h.document.querySelector('#'+id),null);
 assert.equal(h.api.data().teams.length,count);assert.equal(h.document.querySelectorAll('.share-row').length,count);
 assert.equal(h.document.querySelectorAll('.race-row').length,count);h.api.showDay(0,true);h.tick(0);h.tick(1500);
 assert.doesNotMatch(h.document.querySelector('#gp-chart').textContent,/September 1 to 14|Sample data/);
 assert.doesNotMatch(JSON.stringify(h.document.querySelector('#gp-chart').attrs),/NaN|Infinity/);
});
test('global top five, sixth hover/focus/pin, scoped drops, search does not move the page',async()=>{
 const h=harness(),a=h.api;a.setMode('players');assert.equal(a.current().length,5);const extra=a.rankedPlayers()[6];a.previewPlayer(extra.id,'pointer');assert.equal(a.current().length,6);a.pinPlayer(extra.id,false);a.previewPlayer(null,'pointer');assert.equal(a.current().at(-1).id,extra.id);
 const input=h.document.querySelector('#gp-player-search');input.value=extra.name;a.searchPlayers();const button=h.document.querySelector('#gp-search-results button');await button.fire('click');assert.equal(h.window.scrollY,123);assert.equal(h.document.activeElement.id,'gp-search-toggle');
 a.openTeam('team-1');assert.equal(h.document.querySelectorAll('.treasure-item').length,0);assert.equal(a.gpBreakdown().length,7);
});
test('Luck uses min 60 scale; positive/negative outliers and pinned comparison share the same extent',()=>{
 const p=fixture();const h=harness(p);assert.equal(h.api.scale(),60);
 p.stats.luck.teams[0].result.percentage=350;p.stats.luck.teams[1].result.percentage=-200;h.api.setData(p);h.api.renderLuck();assert.equal(h.api.scale(),350);h.api.finishLuckMotion();
 assert.ok(h.document.querySelectorAll('.luck-bar').every(bar=>parseFloat(bar.style.width)<=40));
 const list=h.document.querySelector('#luck-rows');list.scrollTop=999;void list.fire('scroll');assert.equal(h.api.scale(),350);
 h.api.setLuckMode('players');const middle=h.api.luckPlayers()[17];h.api.selectLuckPlayer(middle.id);assert.equal(h.api.state().luckPinnedId,middle.id);assert.ok(h.document.querySelector('.luck-pinned-row'));
});
test('missing prices/activity and stale metadata stay honest; no fabricated Luck bar',()=>{
 const p=fixture(3,true);p.stats.luck.stale=true;p.stats.luck.evidenceRevision=6;const h=harness(p);
 assert.match(h.document.querySelector('.luck-panel .subheading').textContent,/Stale/);assert.match(h.document.querySelector('.luck-panel .subheading').title,/revision 6/);
 assert.ok(h.document.querySelectorAll('.luck-bar').every(bar=>bar.hidden));assert.match(h.document.querySelector('#gp-unit').textContent,/missing prices/);
 assert.equal(h.document.querySelector('.share-percent').textContent,'—');assert.match(h.document.querySelector('.item-value').textContent,/unavailable/);
});
test('actual progress marker focus exposes server counts/time/name and interrupts entrance',async()=>{
 const h=harness();await h.document.querySelector('.race-mark').fire('focus');assert.match(h.document.querySelector('#race-detail').textContent,/Actual tile.*1\/4 tiles.*0\/4 rows.*10\/40 contributions/);
 assert.equal(h.document.querySelector('#race-tooltip').hidden,false);assert.match(h.document.querySelector('#race-date').textContent,/2026/);
 h.media.matches=true;(h.listeners.motion||[]).forEach(fn=>fn());
});
test('artwork actual item, draft cancel/reset and saved appearance for ordinary readers',async()=>{
 const h=harness();await h.document.querySelector('#adjust-artwork').fire('click');assert.equal(h.document.querySelector('#artwork-editor').open,true);
 const slider=h.document.querySelector('#artwork-rotation');slider.value='25';await slider.fire('input');assert.equal(h.document.querySelector('.repeat-drop-card').dataset.artworkCustom,'false');
 await h.document.querySelector('#artwork-cancel').fire('click');assert.equal(h.posts.length,0);
 await h.document.querySelector('#adjust-artwork').fire('click');await h.document.querySelector('#artwork-reset').fire('click');assert.equal(h.posts.length,0);
 await h.document.querySelector('#artwork-save').fire('click');assert.equal(h.posts.length,1);assert.equal(h.posts[0].options.body.get('itemId'),'item-1');assert.equal(h.posts[0].options.body.get('reset'),'true');assert.equal(h.document.querySelector('.repeat-drop-card').dataset.artworkCustom,'true');
 const p=fixture();p.artwork[0].fit={x:1,y:2,width:30,height:50,scale:2,rotation:45};const reader=harness(p,false);assert.equal(reader.document.querySelector('.repeat-drop-card').style['--artwork-rotation'],'45deg');
});
test('saved guidance hides on load, help reopens, preference save includes account version',async()=>{
 const p=fixture();p.preference.hidden=true;const h=harness(p);h.animations.forEach(a=>a.finish());assert.equal(h.document.querySelector('#gp-help').open,false);assert.equal(h.document.querySelector('#luck-help-box').hidden,true);
 await h.document.querySelector('#luck-help-toggle').fire('click');assert.equal(h.document.querySelector('#luck-help-box').hidden,false);
 const checkbox=h.document.querySelector('#stats-hide-guidance');checkbox.checked=true;await checkbox.fire('change');assert.equal(h.posts[0].options.body.get('expectedVersion'),'1');
});
test('refresh preserves team/player scope, scroll, real authoritative data and safe labels',async()=>{
 const h=harness();h.api.openTeam('team-0');h.document.querySelector('#share-legend').scrollTop=88;const p=fixture();p.stats.evidenceRevision++;p.stats.teams[0].name='Updated <img src=x onerror=alert(1)>';h.setResponse(p);await h.api.refreshStats();assert.equal(h.api.state().selectedTeamId,'team-0');assert.equal(h.document.querySelector('#share-legend').scrollTop,88);assert.equal(h.window.scrollY,123);assert.equal(h.document.querySelector('#gp-team-label').textContent,p.stats.teams[0].name);
 assert.equal(h.document.querySelector('#gp-team-label img'),null);
});
test('approved baseline hashes, CSS declarations/order and unchanged interaction functions are preserved',()=>{
 const crypto=require('node:crypto'),baseline=path.resolve(root,'../../prototypes/stats/outputs');
 const hashes={'stats-page-prototype.html':'fcd542de6afb2c0889435423b15d461ec601566dee8bee755cc5ca7b9112ed26','stats-density.css':'88d4c40780c5c675600e736ec80c1e8f6975cb5a7c2ff83693e9f43f70eff95d','stats-prototype.js':'50d6f6512117cd863c4c0a4f9e7a2d3a9d1dcbd2e13e365b3379b656cd598b24'};
 for(const [file,hash] of Object.entries(hashes))assert.equal(crypto.createHash('sha256').update(fs.readFileSync(path.join(baseline,file))).digest('hex'),hash);
 const leafBlocks=css=>[...css.replace(/\/\*[\s\S]*?\*\//g,'').matchAll(/\{([^{}]*)\}/g)].map(match=>match[1].replaceAll('/stats/assets/','assets/').replace(/\s+/g,' ').trim());
 const html=fs.readFileSync(path.join(baseline,'stats-page-prototype.html'),'utf8');
 assert.deepEqual(leafBlocks(fs.readFileSync(path.join(root,'wwwroot/css/stats-base.css'),'utf8')),leafBlocks(html.split('<style>')[1].split('</style>')[0]));
 assert.deepEqual(leafBlocks(fs.readFileSync(path.join(root,'wwwroot/css/stats-density.css'),'utf8')),leafBlocks(fs.readFileSync(path.join(baseline,'stats-density.css'),'utf8')));
 const source=fs.readFileSync(path.join(baseline,'stats-prototype.js'),'utf8'),port=fs.readFileSync(path.join(root,'wwwroot/js/stats-page.js'),'utf8');
 for(const name of ['animateGp','animateSvg','dismissGpHelp','highlightGp','updateComparison','previewPlayer','pinPlayer','holdGpLayout','openTeam','searchPlayers','layoutGpSearch','setSearchOpen','node','donutArc','revealDonut','renderLegend','updateLegendState','finishLuckMotion','animateLuckRows','setLuckHelpOpen','closeLuckSearch','openLuckTeam','highlightLuckPlayer','searchLuckPlayers','removeLuckComparison','setRaceHelpOpen','animateTimeline']){
   const extract=script=>{const start=script.indexOf(`  function ${name}(`);assert.ok(start>=0,name);return script.slice(start,script.indexOf('\n  }',start)+4)};
   assert.equal(extract(port),extract(source),`${name} remains the actual approved implementation`);
 }
 for(const file of ['fonts/BarlowCondensed-ExtraBold.woff2','fonts/BarlowCondensed-SemiBold.woff2','fonts/Geist-Variable.woff2','branding/login-artwork-dark.svg','items/dragon-warhammer-detail.png','items/twisted-bow.png','items/tumekens-shadow.png','items/torva-platebody.png','items/abyssal-whip.png','items/bandos-chestplate.png','items/berserker-ring.png'])assert.deepEqual(fs.readFileSync(path.join(root,'wwwroot/stats/assets',file)),fs.readFileSync(path.join(baseline,'assets',file)));
});

for(const width of [320,560,850,1150])test(`responsive integration at ${width}px preserves compact search and fixed content controls`,()=>{
 const h=harness(fixture(15),false,width);h.api.setMode('players');h.api.openTeam('team-0');assert.equal(h.api.current().length,5);h.api.setMode('players');
 const divider=h.document.querySelector('#gp-divider-search');divider.clientWidth=180;h.document.querySelector('#gp-search-toggle').fire('click');assert.equal(divider.dataset.layout,'popover');
 const css=fs.readFileSync(path.join(root,'wwwroot/css/stats-base.css'),'utf8');assert.match(css,/\.shell\{max-width:1640px;margin:auto;padding:0 3\.6%\}/);assert.match(css,/@media\(max-width:850px\)/);assert.match(css,/\.shell\{padding:0 5%\}/);
 const integration=fs.readFileSync(path.join(root,'wwwroot/css/stats-integration.css'),'utf8');assert.match(integration,/width: calc\(100% \+ 2 \* var\(--public-page-gutter\)\)/);assert.ok(h.page.querySelector('.shell'));
});
test('ambient app panel and Bootstrap toast defaults cannot alter approved layout or hide feedback',()=>{
 const css=fs.readFileSync(path.join(root,'wwwroot/css/stats-integration.css'),'utf8');
 assert.match(css,/\.stats-page \.panel \{ margin-top: 0; \}/);
 assert.match(css,/\.stats-page \.toast \{ width: auto; border: 0;/);
 assert.match(css,/\.stats-page \.toast:not\(\[hidden\]\) \{ display: block; \}/);
 const h=harness();h.api.setMode('players');h.api.pinPlayer(h.api.rankedPlayers()[0].id);assert.equal(h.document.querySelector('#toast').hidden,false);
});

function movedPlayerFixture(){
 const p=fixture(),old=p.stats.teams[0].players[0],current=p.stats.teams[1].players[0];
 current.playerId=old.playerId;current.name=old.name;current.value={...current.value,knownValueGp:1000000,valueGp:1000000};current.valueHistory[0].value=current.value;
 p.stats.luck.teams[1].players[0].playerId=old.playerId;p.stats.luck.teams[1].players[0].name=old.name;
 p.currentMembership={playerId:old.playerId,teamId:p.stats.teams[1].teamId};
 return p;
}
async function assertMovedPlayerControls(p){
 const h=harness(p),id=p.currentMembership.playerId,teamId=p.currentMembership.teamId;
 const copies=h.api.data().contributors.filter(player=>player.raw.playerId===id);
 assert.equal(copies.length,2);assert.notEqual(copies[0].id,copies[1].id,'retained and current GP contributors need independent keys');
 const current=copies.find(player=>player.teamId===teamId),old=copies.find(player=>player.teamId!==teamId);
 await h.document.querySelector('#gp-shortcut').fire('click');assert.equal(h.api.state().selectedTeamId,teamId);
 h.api.setMode('players');
 h.document.querySelector('#gp-player-search').value=current.name;h.api.searchPlayers();
 const results=h.document.querySelectorAll('#gp-search-results button');assert.equal(results.length,2);
 const oldResult=results.find(button=>button.textContent.includes(old.teamName)),currentResult=results.find(button=>button.textContent.includes(current.teamName));
 await oldResult.fire('pointerenter');assert.ok(h.api.current().some(player=>player.id===old.id));
 const expectedPin=h.api.rankedPlayers().slice(0,5).some(player=>player.id===current.id)?null:current.id;
 await currentResult.fire('click');assert.equal(h.api.state().pinnedPlayerId,expectedPin);assert.ok(h.api.current().some(player=>player.id===current.id));
 const legend=h.document.querySelectorAll('#gp-legend button').find(button=>button.dataset.series===current.id);await legend.fire('click');
 assert.equal(legend.getAttribute('aria-pressed'),'false');
 assert.ok(h.document.querySelectorAll('.chart-line').some(line=>line.dataset.series===old.id));
 assert.ok(!h.document.querySelectorAll('.chart-line').filter(line=>!line.classList.contains('gp-exiting-line')).some(line=>line.dataset.series===current.id));
 h.api.setMode('players');await h.document.querySelector('#gp-shortcut').fire('click');assert.equal(h.api.state().pinnedPlayerId,expectedPin);assert.ok(h.api.current().some(player=>player.id===current.id));
 h.api.setLuckMode('players');const luckCopies=h.api.data().luck.players.filter(player=>player.playerId===id);
 assert.equal(luckCopies.length,2);assert.notEqual(luckCopies[0].id,luckCopies[1].id);
 for(const player of luckCopies){
  h.document.querySelector('#luck-player-search').value=player.name;h.api.searchLuckPlayers();
  const button=h.document.querySelectorAll('#luck-search-results button').find(button=>button.textContent.includes(player.teamName));await button.fire('click');
  const highlighted=h.document.querySelectorAll('#luck-rows .is-highlighted');assert.equal(highlighted.length,1);assert.equal(highlighted[0].dataset.player,player.id);
  if(!h.api.luckExtremes().some(entry=>entry.id===player.id))assert.equal(h.api.state().luckPinnedId,player.id);
 }
 const before=JSON.stringify(h.api.data().stats.drops);assert.equal(h.api.data().drops[0].player.teamId,p.stats.drops[0].teamId);assert.equal(JSON.stringify(h.api.data().stats.drops),before);
}
test('F1 moved participant keeps independent GP/Luck controls and My team uses current membership',()=>assertMovedPlayerControls(movedPlayerFixture()));

test('F2 official completion moves the finish marker while raw tile times remain intact; reopening restores raw finish',async()=>{
 const p=fixture(2),team=p.stats.teams[0],raw=team.progressHistory[0],corrected='2026-09-14T08:01:00Z';
 raw.boardComplete=true;raw.completedTiles=4;raw.completedLines=4;team.officialCompletion={boardComplete:true,completedAt:corrected,completedTiles:4,completedLines:4};
 team.milestones.push({id:'board',defaultOrder:9,state:'Reached',at:corrected,teamId:team.teamId});
 const h=harness(p);h.api.finishRace();let marks=h.api.data().progress[0].marks;
 assert.equal(marks.find(mark=>mark.tileId==='tile-1').at,raw.at);assert.equal(marks.find(mark=>mark.tileId==='tile-1').finished,false);
 assert.equal(marks.find(mark=>mark.finished).at,corrected);
 let finish=h.document.querySelector('.race-mark.finished');await finish.fire('focus');assert.ok(h.document.querySelector('#race-detail').textContent.includes(h.api.data().date(corrected,true)));assert.match(h.document.querySelector('#race-detail').textContent,/Board completed/);
 p.stats.teams[0].officialCompletion=null;p.stats.evidenceRevision++;h.setResponse(p);await h.api.refreshStats();
 marks=h.api.data().progress[0].marks;assert.equal(marks.length,1);assert.equal(marks[0].at,raw.at);assert.equal(marks[0].finished,true);
 team.officialCompletion={boardComplete:false,completedAt:null,completedTiles:3,completedLines:2};h.api.setData(p);h.api.renderRace();assert.equal(h.document.querySelector('.race-mark.finished'),null);
});

for(const change of ['ended','submission','clock'])test(`F3 chart redraw aligns paths, labels and inspection after ${change} timestamp changes with unchanged values`,async()=>{
 const p=fixture(2),h=harness(p);h.media.matches=true;
 const before=h.document.querySelector('.chart-line').getAttribute('d'),values=JSON.stringify(h.api.data().teams.map(team=>team.values));
 if(change==='ended')p.stats.endedAt='2026-09-14T20:00:00Z';
 else if(change==='clock')p.generatedAt='2026-09-16T08:00:00Z';
 else {const corrected='2026-09-13T08:00:00Z';p.stats.valueHistory[0].at=corrected;p.stats.teams.forEach(team=>{team.valueHistory[0].at=corrected;team.players.forEach(player=>player.valueHistory[0].at=corrected)});}
 h.setResponse(p);await h.api.refreshStats();assert.equal(JSON.stringify(h.api.data().teams.map(team=>team.values)),values);
 const line=h.document.querySelector('.chart-line');assert.notEqual(line.getAttribute('d'),before);
 h.api.showDay(1,true);const x=Number(line.getAttribute('d').split(' ')[1].slice(1).split(',')[0]);
 assert.ok(Math.abs(Number(h.document.querySelector('#chart-cursor line').getAttribute('x1'))-x)<.01);
 assert.equal(h.document.querySelector('.tip-date').textContent,h.api.data().date(h.api.data().dates[1],true));
 assert.ok(h.document.querySelectorAll('.axis-text').some(label=>label.textContent===h.api.data().date(h.api.data().end)));
});
const settle=()=>new Promise(resolve=>setImmediate(resolve));
test('F4 GET released while artwork draft is open is discarded and refetched after cancel',async()=>{
 const h=harness(),stale=fixture();stale.stats.evidenceRevision++;
 const request=h.defer(),refresh=h.api.refreshStats();await h.document.querySelector('#adjust-artwork').fire('click');
 const slider=h.document.querySelector('#artwork-rotation');slider.value='42';await slider.fire('input');request.resolve(stale);await refresh;
 assert.equal(h.api.data().stats.evidenceRevision,9);assert.equal(slider.value,'42');assert.equal(h.posts.length,1);
 const fresh=fixture();fresh.stats.evidenceRevision=11;h.setResponse(fresh);await h.document.querySelector('#artwork-cancel').fire('click');await settle();
 assert.equal(h.api.data().stats.evidenceRevision,11);assert.equal(h.posts.length,2);
});
for(const kind of ['artwork','guidance'])for(const outcome of ['success','error','in-flight'])test(`F4 deferred GET cannot overwrite ${kind} ${outcome} mutation`,async()=>{
 const h=harness(),stale=fixture();stale.stats.evidenceRevision=10;
 const get=h.defer(),refresh=h.api.refreshStats();
 if(kind==='artwork')await h.document.querySelector('#adjust-artwork').fire('click');
 const post=h.defer('POST'),control=h.document.querySelector(kind==='artwork'?'#artwork-save':'#stats-hide-guidance');if(kind==='guidance')control.checked=true;
 const saving=control.fire(kind==='artwork'?'click':'change');
 assert.equal(h.document.querySelector('#stats-hide-guidance').disabled,true);
 if(kind==='artwork'&&outcome==='in-flight')await h.document.querySelector('#artwork-cancel').fire('click');
 const fresh=fixture();fresh.stats.evidenceRevision=12;
 if(outcome==='in-flight'){get.resolve(stale);await refresh;assert.equal(h.api.data().stats.evidenceRevision,9);assert.equal(h.posts.length,2);}
 if(outcome==='error')post.resolve({error:'Controlled save conflict'},409);
 else {
  fresh.preference={...fresh.preference,hidden:kind==='guidance',version:2};fresh.artwork[0]={...fresh.artwork[0],version:kind==='artwork'?2:1,fit:kind==='artwork'?{x:25,y:50,width:20,height:50,scale:1.2,rotation:25}:null};
  post.resolve(kind==='artwork'?{artwork:fresh.artwork[0],accountVersion:2}:{hidden:true,version:2});
 }
 const followup=h.defer();h.setResponse(fresh);await saving;
 if(outcome!=='in-flight'){get.resolve(stale);await refresh;}
 assert.equal(h.api.data().payload.preference.version,outcome==='error'?1:2);
 if(kind==='artwork'&&outcome==='error'){assert.equal(h.document.querySelector('#artwork-editor').open,true);assert.equal(h.posts.length,2);await h.document.querySelector('#artwork-cancel').fire('click');}
 await settle();assert.equal(h.api.data().stats.evidenceRevision,9,'stale response never applied while replacement is pending');
 if(kind==='artwork'&&outcome!=='error'){assert.equal(h.api.data().artwork.get('item-1').version,2);assert.equal(h.document.querySelector('.repeat-drop-card').style['--artwork-rotation'],'25deg');}
 if(kind==='guidance'&&outcome==='error')assert.equal(control.checked,false);
 followup.resolve(fresh);await settle();assert.equal(h.api.data().stats.evidenceRevision,12);assert.equal(h.posts.length,3);
 assert.equal(h.document.querySelector('#stats-hide-guidance').disabled,false);
});
test('F4 opening and cancelling an artwork draft before GET resolves still invalidates that response',async()=>{
 const h=harness(),get=h.defer(),refresh=h.api.refreshStats();await h.document.querySelector('#adjust-artwork').fire('click');await h.document.querySelector('#artwork-cancel').fire('click');
 const followup=h.defer();get.resolve({...fixture(),stats:{...fixture().stats,evidenceRevision:10}});await refresh;assert.equal(h.api.data().stats.evidenceRevision,9);
 followup.resolve({...fixture(),stats:{...fixture().stats,evidenceRevision:11}});await settle();assert.equal(h.api.data().stats.evidenceRevision,11);
});

const postgresFixtures=process.env.STATS_PASS5_FIXTURE_DIRECTORY;
test('F1 actual PostgreSQL moved/departed membership DTO through production renderer',{skip:!postgresFixtures},async()=>{
 const read=name=>JSON.parse(fs.readFileSync(path.join(postgresFixtures,name+'.json'),'utf8'));
 await assertMovedPlayerControls(read('moved-player'));
 const h=harness(read('departed-player'));assert.equal(h.document.querySelector('#gp-shortcut').hidden,true);
});
test('F2 actual PostgreSQL finalized, archived and unfinalized DTOs through production renderer',{skip:!postgresFixtures},async()=>{
 const payloads=['finalized','archived','reopened'].map(name=>JSON.parse(fs.readFileSync(path.join(postgresFixtures,name+'.json'),'utf8')));
 const h=harness(payloads[0]);
 for(const p of payloads){
  h.setResponse(p);await h.api.refreshStats();h.api.finishRace();
  const team=h.api.data().teams[0],marks=h.api.data().progress[0].marks,rawAt=team.raw.progressHistory.at(-1).at;
  const finish=marks.filter(mark=>mark.finished);assert.equal(finish.length,1);
  const expected=team.raw.officialCompletion?.completedAt||rawAt;assert.equal(finish[0].at,expected);
  assert.equal(marks.filter(mark=>mark.tileId).length,25);assert.equal(marks.filter(mark=>mark.tileId).at(-1).at,rawAt);
  assert.equal(h.api.data().milestones('all').find(mark=>mark.id==='board').at,expected);
  const button=h.document.querySelector('.race-mark.finished');await button.fire('focus');
  assert.ok(h.document.querySelector('#race-detail').textContent.includes(h.api.data().date(expected,true)));
  assert.match(h.document.querySelector('#race-tooltip').textContent,/Board completed/);
  assert.equal(Number.parseFloat(button.style.left),h.api.data().day(expected)/13*100);
 }
});

test('F4 cancelled pending artwork save restores focus, blocks reopening, and exposes failure outside the dialog',async()=>{
 const h=harness();await h.document.querySelector('#adjust-artwork').fire('click');const post=h.defer('POST'),saving=h.document.querySelector('#artwork-save').fire('click');
 await h.document.querySelector('#artwork-cancel').fire('click');assert.equal(h.document.activeElement.id,'adjust-artwork');assert.equal(h.document.activeElement.disabled,false);
 await h.document.querySelector('#adjust-artwork').fire('click');assert.equal(h.document.querySelector('#artwork-editor').open,false);assert.match(h.document.querySelector('#stats-feedback').textContent,/still saving/);
 post.resolve({error:'Controlled save conflict'},409);await saving;assert.equal(h.document.querySelector('#stats-feedback').textContent,'Controlled save conflict');
 await h.document.querySelector('#adjust-artwork').fire('click');assert.equal(h.document.querySelector('#artwork-editor').open,true);
});

// Fidelity regressions invoke the actual observer/hub/window callbacks and renderer.
// The harness models clocks and DOM ownership, not browser layout or SMIL painting.
function finishEntry(h){h.tick(0);h.tick(1500);h.animations.forEach(animation=>animation.finish());}
function liveLines(h){return h.document.querySelectorAll('.chart-line').filter(line=>!line.classList.contains('gp-exiting-line'));}
function pathPoints(value){return value.split(' ').map(command=>command.slice(1).split(',').map(Number));}
function pointAt(points,x,last=false){
 const exact=points.filter(point=>point[0]===x);if(exact.length)return (last?exact.at(-1):exact[0])[1];
 const right=points.findIndex(point=>point[0]>x);if(right<=0)return points[right===0?0:points.length-1][1];
 const a=points[right-1],b=points[right];return a[1]+(b[1]-a[1])*(x-a[0])/(b[0]-a[0]);
}
function assertSamePolyline(sample,expected){
 for(const [index,[x,y]] of sample.entries())assert.ok(Math.abs(y-pointAt(expected,x,index>0&&sample[index-1][0]===x))<.035,`${x},${y} lies on the expected polyline`);
 for(const [index,[x,y]] of expected.entries())assert.ok(Math.abs(y-pointAt(sample,x,index>0&&expected[index-1][0]===x))<.035,`${x},${y} is retained in sampled geometry`);
}
function historyPayload(){
 const p=fixture(2),at='2026-09-10T08:00:00Z';
 p.stats.valueHistory.unshift({at,value:{...p.stats.value,knownValueGp:4000000,valueGp:4000000}});
 p.stats.teams.forEach(team=>team.valueHistory.unshift({at,value:{...team.value,knownValueGp:2000000,valueGp:2000000}}));
 return p;
}
test('UI1 same-theme observer and immediate WatchEvent clock reply preserve all entrance nodes and durations',async()=>{
 const h=harness(fixture(),true,700,{hub:true}),q=s=>h.document.querySelector(s);
 const nodes=['.chart-line','#gp-donut defs','.luck-row','.race-mark','#timeline'].map(q),calls={...h.calls};
 const p=fixture();p.generatedAt='2026-09-15T08:00:01Z';h.setResponse(p);
 h.theme('light');await settle();assert.equal(h.hub.calls[0][0],'WatchEvent');assert.equal(h.posts.length,1);
 nodes.forEach((node,index)=>assert.equal(['.chart-line','#gp-donut defs','.luck-row','.race-mark','#timeline'].map(q)[index],node));
 assert.deepEqual({...h.calls},calls);assert.equal(h.api.data().payload.generatedAt,fixture().generatedAt);
 assert.ok(h.animations.some(a=>a.element===nodes[0]&&a.options.duration===1100));
 assert.ok(h.animations.some(a=>a.element.parent?.parent===nodes[1]&&a.options.duration===1200));
 assert.ok(h.animations.some(a=>a.options.duration===1400));
 h.tick(0);h.tick(500);assert.notEqual(q('.luck-bar').style.transform,'scaleX(1)');assert.notEqual(q('.race-mark').style.opacity,'');
 h.tick(1000);assert.equal(q('.luck-bar').style.transform,'scaleX(1)');h.tick(1200);assert.equal(q('.race-mark').style.opacity,'');
 h.advance(1400);await settle();assert.equal(h.posts.length,2);assert.equal(h.api.data().payload.generatedAt,p.generatedAt);
 assert.equal(h.calls.renderLuck,calls.renderLuck);assert.equal(h.calls.renderDonut,calls.renderDonut);assert.equal(q('.luck-row'),nodes[2]);
});
for(const trigger of ['focus','visibility','reconnect'])test(`UI1 ${trigger} clock refresh preserves race inspection/focus without replaying unchanged Luck or race`,async()=>{
 const h=harness(fixture(2),true,700,{hub:true});await settle();finishEntry(h);
 const q=s=>h.document.querySelector(s),mark=q('.race-mark');mark.focus();const key=mark.dataset.raceMarker,date=q('#race-date').textContent,detail=q('#race-detail').textContent;
 q('.race-scroll').scrollTop=67;q('#luck-rows').scrollTop=44;const luckRow=q('.luck-row'),calls={...h.calls},motions=h.frames.size;
 const p=fixture(2);p.generatedAt='2026-09-16T08:00:00Z';h.setResponse(p);
 if(trigger==='focus')await h.listeners.focus[0]();else if(trigger==='visibility'){await h.document.fire('visibilitychange');await settle();}else await h.reconnect();
 assert.equal(h.api.data().end,Date.parse(p.generatedAt));assert.equal(h.calls.renderLuck,calls.renderLuck);assert.equal(q('.luck-row'),luckRow);
 assert.equal(h.frames.size,motions,'background race redraw schedules no entrance sweep');assert.equal(h.document.activeElement.dataset.raceMarker,key);
 assert.ok(h.page.contains(h.document.activeElement));assert.equal(q('#race-date').textContent,date);assert.equal(q('#race-detail').textContent,detail);
 assert.equal(q('#race-tooltip').hidden,false);assert.equal(q('.race-scroll').scrollTop,67);assert.equal(q('#luck-rows').scrollTop,44);assert.equal(h.window.scrollY,123);
 const after={...h.calls};await h.api.refreshStats();assert.deepEqual({...h.calls},after,'identical reply renders no sections');
});
test('UI1 actual GP history update during entrance updates GP immediately and leaves unchanged Luck/race/donut motion alone',async()=>{
 const h=harness(fixture(2)),q=s=>h.document.querySelector(s),luckRow=q('.luck-row'),raceMark=q('.race-mark'),mask=q('#gp-donut defs'),calls={...h.calls};
 h.tick(0);h.tick(300);const luckPaint=q('.luck-bar').style.transform,racePaint=raceMark.style.opacity;
 const p=historyPayload();h.setResponse(p);await h.api.refreshStats();
 assert.equal(h.api.data().dates.length,4);assert.equal(h.calls.renderLuck,calls.renderLuck);assert.equal(h.calls.renderRace,calls.renderRace);
 assert.equal(q('.luck-row'),luckRow);assert.equal(q('.race-mark'),raceMark);assert.equal(q('#gp-donut defs'),mask);
 assert.equal(q('.luck-bar').style.transform,luckPaint);assert.equal(raceMark.style.opacity,racePaint);
 h.tick(1000);assert.equal(q('.luck-bar').style.transform,'scaleX(1)');h.tick(1200);assert.equal(raceMark.style.opacity,'');
});
test('UI1 arbitrary race inspection time survives live-domain and real progress updates; changed Luck still renders',async()=>{
 const h=harness(fixture(2));finishEntry(h);const q=s=>h.document.querySelector(s);
 await q('.race-track').fire('pointermove',{clientX:350});const date=q('#race-date').textContent,counts=q('.race-count').textContent;
 const p=fixture(2);p.generatedAt='2026-09-16T08:00:00Z';p.stats.teams[1].progressHistory[0].approved=11;p.stats.luck.teams[0].result.percentage=42;
 const calls={...h.calls};h.setResponse(p);await h.api.refreshStats();assert.equal(q('#race-date').textContent,date);assert.equal(q('.race-count').textContent,counts);
 assert.equal(q('#race-tracks').classList.contains('exploring'),true);assert.equal(q('#race-tooltip').hidden,true);assert.equal(h.calls.renderLuck,calls.renderLuck+1);
 h.api.finishLuckMotion();assert.ok(q('#luck-rows').textContent.includes('+42%'));
});
test('UI1 deferred startup clock fetch still respects an artwork draft and newer save revision',async()=>{
 const h=harness();const p=fixture();p.generatedAt='2026-09-15T08:00:01Z';h.setResponse(p);await h.api.refreshStats();assert.equal(h.posts.length,1);
 await h.document.querySelector('#adjust-artwork').fire('click');h.advance(1500);await settle();assert.equal(h.posts.length,1);
 const post=h.defer('POST'),saving=h.document.querySelector('#artwork-save').fire('click'),fresh=fixture();fresh.generatedAt=p.generatedAt;fresh.preference.version=2;fresh.artwork[0].version=2;fresh.artwork[0].fit={x:20,y:30,width:40,height:50,scale:1,rotation:10};h.setResponse(fresh);
 post.resolve({artwork:fresh.artwork[0],accountVersion:2});await saving;await settle();assert.equal(h.api.data().artwork.get('item-1').version,2);assert.equal(h.api.data().payload.preference.version,2);assert.equal(h.posts.length,3);
});
for(const count of [5,15])test(`UI3 real theme flips synchronize ${count} chart/donut/legend swatches in place through entry and pinned interaction`,()=>{
 const h=harness(fixture(count)),q=s=>h.document.querySelector(s),line=q('.chart-line'),mask=q('#gp-donut defs'),legend=q('#gp-legend button'),calls={...h.calls};legend.focus();q('#gp-legend').scrollTop=66;
 function colorsAgree(){
  for(const line of liveLines(h)){
   const button=h.document.querySelectorAll('#gp-legend button').find(button=>button.dataset.series===line.dataset.series);
   assert.equal(button.querySelector('.dot').style.background,line.getAttribute('stroke'));
   const segment=h.document.querySelectorAll('.share-segment').find(segment=>segment.dataset.team===line.dataset.series);
   if(segment)assert.equal(segment.getAttribute('stroke'),line.getAttribute('stroke'));
  }
  for(const segment of h.document.querySelectorAll('.share-segment')){
   const row=h.document.querySelectorAll('.share-row').find(row=>row.dataset.team===segment.dataset.team);
   assert.equal(row.querySelector('.share-dot').style.background,segment.getAttribute('stroke'));
  }
 }
 const light=liveLines(h).map(line=>line.getAttribute('stroke'));h.theme('dark');colorsAgree();
 liveLines(h).forEach((line,index)=>assert.notEqual(line.getAttribute('stroke'),light[index]));assert.equal(q('.chart-line'),line);assert.equal(q('#gp-donut defs'),mask);
 assert.equal(h.document.activeElement,legend);assert.equal(q('#gp-legend').scrollTop,66);assert.deepEqual({...h.calls},calls);
 h.api.setMode('players');const extra=h.api.rankedPlayers()[6];h.api.pinPlayer(extra.id,false);const pinned=q('.gp-pinned-legend button');pinned.focus();q('#gp-legend').scrollTop=77;const playerCalls={...h.calls};h.theme('light');colorsAgree();
 assert.equal(h.api.state().pinnedPlayerId,extra.id);assert.equal(h.document.activeElement,pinned);assert.equal(q('#gp-legend').scrollTop,77);assert.equal(h.window.scrollY,123);assert.deepEqual({...h.calls},playerCalls);
 h.theme('light');assert.deepEqual({...h.calls},playerCalls);
});
test('UI2 timestamp insertion/removal morph uses compatible polylines with exact authoritative end path and hover dates',async()=>{
 const h=harness(fixture(2));finishEntry(h);const original=liveLines(h)[0].getAttribute('d');
 h.setResponse(historyPayload());await h.api.refreshStats();let line=liveLines(h)[0],animation=line.querySelector('animate'),final=line.getAttribute('d');
 assert.ok(animation);assert.equal(animation.getAttribute('dur'),'240ms');assert.equal(animation.getAttribute('keySplines'),'.2 .7 .2 1');
 const from=pathPoints(animation.getAttribute('from')),to=pathPoints(animation.getAttribute('to'));
 assert.equal(from.length,to.length);assertSamePolyline(from,pathPoints(original));assertSamePolyline(to,pathPoints(final));assert.equal(pathPoints(final).length,4);
 const expectedXs=h.api.data().positions.map(position=>Number((43+position*604).toFixed(2)));assert.deepEqual(pathPoints(final).map(p=>p[0]),Array.from(expectedXs));
 h.api.showDay(1,true);assert.equal(h.document.querySelector('.tip-date').textContent,h.api.data().date('2026-09-10T08:00:00Z',true));assert.equal(h.api.data().dates.length,4);
 h.advance(1800);h.setResponse(fixture(2));await h.api.refreshStats();line=liveLines(h)[0];animation=line.querySelector('animate');
 assert.equal(line.getAttribute('d'),original);assert.equal(pathPoints(animation.getAttribute('from')).length,pathPoints(animation.getAttribute('to')).length);
 assertSamePolyline(pathPoints(animation.getAttribute('from')),pathPoints(final));assertSamePolyline(pathPoints(animation.getAttribute('to')),pathPoints(original));assert.equal(h.api.data().dates.length,3);
});
test('UI2 interrupted morph starts at its displayed spline position, including time and Y-domain shifts',async()=>{
 const h=harness(fixture(2));finishEntry(h);const p=historyPayload();h.setResponse(p);await h.api.refreshStats();
 const animation=liveLines(h)[0].querySelector('animate'),from=pathPoints(animation.getAttribute('from')),to=pathPoints(animation.getAttribute('to'));
 h.advance(1560); // 60/240 ms; evaluate the cubic independently by dense parameter search.
 let best=0,distance=Infinity;for(let i=0;i<=100000;i++){const t=i/100000,x=.6*(1-t)*t+t**3;if(Math.abs(x-.25)<distance){distance=Math.abs(x-.25);best=t;}}
 const eased=2.1*(1-best)**2*best+3*(1-best)*best**2+best**3,displayed=from.map(([x,y],i)=>[x,y+(to[i][1]-y)*eased]);
 const next=fixture(2);next.generatedAt='2026-09-20T08:00:00Z';next.stats.teams[0].value.knownValueGp=56000000;next.stats.teams[0].valueHistory[0].value.knownValueGp=56000000;
 h.setResponse(next);await h.api.refreshStats();const line=liveLines(h)[0],second=line.querySelector('animate'),secondFrom=pathPoints(second.getAttribute('from')),secondTo=pathPoints(second.getAttribute('to'));
 assert.equal(secondFrom.length,secondTo.length);assertSamePolyline(secondFrom,displayed);assertSamePolyline(secondTo,pathPoints(line.getAttribute('d')));
 assert.equal(pathPoints(line.getAttribute('d')).at(-1)[1],31.8);assert.equal(h.api.data().end,Date.parse(next.generatedAt));assert.notEqual(second.getAttribute('from'),animation.getAttribute('to'));
});
test('UI2 rounded coincident timestamps retain vertical segments and reduced motion ends directly at exact data',async()=>{
 const h=harness(fixture(2));finishEntry(h);const p=historyPayload(),at='2026-09-10T08:00:01Z';
 p.stats.valueHistory.splice(1,0,{at,value:{...p.stats.value,knownValueGp:12000000}});p.stats.teams.forEach(team=>team.valueHistory.splice(1,0,{at,value:{...team.value,knownValueGp:6000000}}));
 h.setResponse(p);await h.api.refreshStats();let line=liveLines(h)[0],animation=line.querySelector('animate');assertSamePolyline(pathPoints(animation.getAttribute('to')),pathPoints(line.getAttribute('d')));
 const final=line.getAttribute('d');h.media.matches=true;h.listeners.motion.forEach(fn=>fn());assert.equal(animation.ended,true);
 h.media.matches=false;h.listeners.motion.forEach(fn=>fn());h.setResponse(fixture(2));await h.api.refreshStats();line=liveLines(h)[0];
 assertSamePolyline(pathPoints(line.querySelector('animate').getAttribute('from')),pathPoints(final));
 h.media.matches=true;h.listeners.motion.forEach(fn=>fn());h.setResponse(historyPayload());await h.api.refreshStats();line=liveLines(h)[0];assert.equal(line.querySelector('animate'),null);assert.equal(pathPoints(line.getAttribute('d')).length,4);assert.equal(h.frames.size,0);
});

// Read the complete approved CSS tree, retaining grouping queries and selector order.
// This deliberately does not claim to compute browser layout or container dimensions.
function cssTree(source){
 source=source.replace(/\/\*[\s\S]*?\*\//g,'');const rules=[];let start=0,open=-1,depth=0,quote=null;
 for(let i=0;i<source.length;i++){
  const char=source[i];if(quote){if(char==='\\')i++;else if(char===quote)quote=null;continue;}if(char==='"'||char==="'"){quote=char;continue;}
  if(char==='{'){if(depth++===0)open=i;}
  else if(char==='}'&&--depth===0){
   const header=source.slice(start,open).trim().replace(/\s+/g,' '),body=source.slice(open+1,i);
   const group=/^@(media|container|supports|layer|keyframes)\b/.test(header);
   rules.push({header,...(group?{children:cssTree(body)}:{body:body.trim().replaceAll('/stats/assets/','assets/').replace(/\s+/g,' ')})});start=i+1;
  }
 }
 assert.equal(depth,0,'balanced CSS blocks');assert.equal(source.slice(start).trim(),'','every rule is parsed');return rules;
}
function cssSelectors(header){
 const selectors=[];let start=0,depth=0,quote=null;
 for(let i=0;i<header.length;i++){
  const char=header[i];if(quote){if(char==='\\')i++;else if(char===quote)quote=null;continue;}if(char==='"'||char==="'"){quote=char;continue;}
  if(char==='('||char==='[')depth++;else if(char===')'||char===']')depth--;else if(char===','&&!depth){selectors.push(header.slice(start,i).trim());start=i+1;}
 }
 selectors.push(header.slice(start).trim());return selectors;
}
const statsScope='.public-stats-page .stats-page';
function scopedStatsSelector(selector){
 if(selector===':root'||selector==='body')return statsScope;
 if(selector===':root[data-theme=dark]')return `html[data-public-theme=dark] ${statsScope}`;
 if(selector.startsWith('html')){const [html,...rest]=selector.split(' ');return [html.replace('data-theme','data-public-theme'),statsScope,...rest].join(' ');}
 return `${statsScope} ${selector}`;
}
function expectedStatsTree(rules,keyframes=false){return rules.map(rule=>rule.children?{...rule,children:expectedStatsTree(rule.children,/^@keyframes\b/.test(rule.header))}:{...rule,header:keyframes||rule.header.startsWith('@')?rule.header:cssSelectors(rule.header).map(scopedStatsSelector).join(',')});}
function statsCssTrees(){
 const baseline=path.resolve(root,'../../prototypes/stats/outputs'),html=fs.readFileSync(path.join(baseline,'stats-page-prototype.html'),'utf8');
 const cssRoot=process.env.STATS_PASS5_CSS_ROOT||path.join(root,'wwwroot/css');
 return {original:[cssTree(html.split('<style>')[1].split('</style>')[0]),cssTree(fs.readFileSync(path.join(baseline,'stats-density.css'),'utf8'))],production:['stats-base.css','stats-density.css'].map(file=>cssTree(fs.readFileSync(path.join(cssRoot,file),'utf8')))};
}
test('responsive port preserves full base/density selector trees, nested query ancestry and Stats-only scope',()=>{
 const {original,production}=statsCssTrees();
 for(let i=0;i<original.length;i++)assert.deepEqual(production[i],expectedStatsTree(original[i]),`${i?'density':'base'} retains the exact scoped selector/query/declaration tree`);
});
function flatCssRules(rules,ancestors=[]){return rules.flatMap(rule=>rule.children?/^@keyframes/.test(rule.header)?[]:flatCssRules(rule.children,[...ancestors,rule.header]):rule.header.startsWith('@')?[]:cssSelectors(rule.header).map(selector=>({selector,ancestors,body:rule.body})));}
function widthQueryApplies(query,viewport,card){
 assert.match(query,/^@(media|container)\b/,'cascade probe supports only these existing groups');
 if(query.includes('prefers-reduced-motion:reduce'))return false;
 const width=query.startsWith('@container')?card:viewport;
 return [...query.matchAll(/\((min|max)-width:\s*([\d.]+)px\)/g)].every(([,kind,value])=>kind==='min'?width>=Number(value):width<=Number(value));
}
function simpleSpecificity(selector){
 // Relevant layout selectors use types, IDs, classes and attributes. Pseudo-state
 // rules are excluded below; the complete tree comparison still covers them.
 const withoutAttrs=selector.replace(/\[[^\]]*\]/g,'');
 return [(withoutAttrs.match(/#[\w-]+/g)||[]).length,(withoutAttrs.match(/\.[\w-]+/g)||[]).length+(selector.match(/\[[^\]]*\]/g)||[]).length,(withoutAttrs.replace(/[.#][\w-]+/g,'').match(/(?:^|[\s>+~])([a-z][\w-]*)/gi)||[]).length];
}
function matchesLayoutSelector(element,selector){
 const parts=selector.trim().replace(/\s*>\s*/g,' > ').split(/\s+/);let cursor=element;
 if(!cursor.matches(parts.pop()))return false;
 while(parts.length){let wanted=parts.pop();if(wanted==='>'){wanted=parts.pop();cursor=cursor.parentElement;if(!cursor?.matches(wanted))return false;}else{cursor=cursor.parentElement;while(cursor&&!cursor.matches(wanted))cursor=cursor.parentElement;if(!cursor)return false;}}
 return true;
}
function cascadeProbe(rules,viewport,card){
 const winners={};
 rules.forEach(rule=>{
  if(rule.selector.includes(':')||!rule.ancestors.every(query=>widthQueryApplies(query,viewport,card)))return;
  const specificity=simpleSpecificity(rule.selector);
  for(const declaration of rule.body.split(';').filter(Boolean)){
   const colon=declaration.indexOf(':'),property=declaration.slice(0,colon).trim(),raw=declaration.slice(colon+1).trim(),important=raw.endsWith('!important'),value=raw.replace(/!important$/,'');
   const rank=[Number(important),...specificity],before=winners[property],difference=before?rank.map((n,i)=>n-before.rank[i]).find(n=>n!==0):1;
   if(!before||difference===undefined||difference>0)winners[property]={value,rank,selector:rule.selector,ancestors:rule.ancestors};
  }
 });return winners;
}
test('responsive GP cascade matches prototype at the 600px card boundary, viewport breakpoints and held layouts',()=>{
 const {original,production}=statsCssTrees(),sources=[original,production].map(trees=>trees.flatMap(tree=>flatCssRules(tree)));
 const h=harness(fixture(2)),wrapper=h.document.createElement('div');wrapper.className='public-stats-page';h.document.documentElement.append(wrapper);wrapper.append(h.page);
 const elements=new Map(),q=s=>{if(!elements.has(s))elements.set(s,h.document.querySelector(s));return elements.get(s)},panel=q('.gp-panel');
 const targets=['.gp-panel','.gp-visuals','.gp-share','.gp-share h3','.share-ring','.share-row','.share-legend','.gp-share-content','.gp-trend','.gp-trend>.chart-wrap','#gp-legend'];
 const properties=['display','grid-column','grid-template-columns','grid-template-rows','align-items','font-size','gap','column-gap','border-left','border-top','padding','margin','margin-bottom','width','height','min-height','max-height','overflow-y','flex','flex-direction','container-type','--gp-chart-min-height','--gp-key-row-height','--share-row-size'];
 const thresholds=[...new Set(sources[0].flatMap(rule=>rule.ancestors.filter(group=>group.startsWith('@media')).flatMap(group=>[...group.matchAll(/(?:min|max)-width:\s*(\d+)px/g)].map(match=>Number(match[1])))))];
 const widths=[...new Set(thresholds.flatMap(width=>[width-1,width,width+1]))];
 // Selector matching and specificity are prepared once; only query activation varies.
 const candidates=sources.map(rules=>new Map(targets.map(selector=>[selector,rules.filter(rule=>!rule.selector.includes(':')&&matchesLayoutSelector(q(selector),rule.selector))])));
 for(const held of [false,true]){
  if(held)panel.dataset.gpLayout='held';else delete panel.dataset.gpLayout;
  // Held selectors are selected after applying the attribute, not assumed active.
  const heldCandidates=held?sources.map(rules=>new Map(targets.map(selector=>[selector,rules.filter(rule=>!rule.selector.includes(':')&&matchesLayoutSelector(q(selector),rule.selector))]))):candidates;
  for(const viewport of widths)for(const card of [599,600,601]){
   const styles=heldCandidates.map(byTarget=>Object.fromEntries(targets.map(selector=>[selector,cascadeProbe(byTarget.get(selector),viewport,card)])));
   for(const selector of targets)for(const property of properties){
    const expected=styles[0][selector][property],actual=styles[1][selector][property],where=`${selector} ${property}, viewport ${viewport}, card ${card}, held ${held}`;
    assert.equal(actual?.value,expected?.value,where);
    if(expected){assert.equal(actual.selector,scopedStatsSelector(expected.selector),where+' winner');assert.deepEqual(actual.ancestors,expected.ancestors,where+' query ancestry');assert.deepEqual(actual.rank,[expected.rank[0],expected.rank[1],expected.rank[2]+2,expected.rank[3]],where+' uniform scope specificity');}
   }
   const value=(selector,property)=>styles[1][selector][property]?.value;
   assert.equal(value('.gp-panel','container-type'),'inline-size');
   assert.equal(value('.gp-visuals','grid-template-columns'),card<=600?'1fr':'minmax(0,1fr) 215px');
   assert.equal(value('.gp-share','display'),card<=600?'grid':'flex');
   assert.equal(value('.gp-share-content','display'),card<=600?'contents':'flex');
   assert.equal(value('.share-ring','width'),card<=600?'130px':'150px');
   if(card<=600){assert.equal(value('.gp-share','grid-template-columns'),'130px minmax(0,1fr)');assert.equal(value('.gp-share h3','margin-bottom'),'4px');}
   if(held){assert.equal(value('.gp-visuals','height'),'var(--gp-resting-visual-height)');assert.equal(value('#gp-legend','max-height'),'var(--gp-resting-legend-height)');assert.equal(value('.share-legend','overflow-y'),'auto');if(card<=600){assert.equal(value('.gp-visuals','grid-template-rows'),'var(--gp-resting-trend-height) var(--gp-resting-share-height)');assert.equal(value('.share-legend','max-height'),'100%');}}
  }
 }
});

function contentFixture(count){
 const p=fixture(count),names=['Prifddinas Pioneers','Karamja Crew','The Last Guardians of the Very Long Wilderness Team Name','Desert Treasure Seekers','Morytania Moonwalkers','Falador Foundry','Varrock Vanguards'],scores=[-96,94.2,-94.2,1350.5,-2048.7,0,2048.7];
 p.stats.value.knownValueGp=55450000000;p.stats.value.valueGp=55450000000;p.stats.valueHistory[0].value={...p.stats.value};
 p.stats.teams.forEach((team,i)=>{team.name=names[i%names.length];team.value.knownValueGp=55450000000/count;team.valueHistory[0].value={...team.value};p.stats.luck.teams[i].name=team.name;p.stats.luck.teams[i].result.percentage=scores[i%scores.length];team.players.forEach((player,j)=>{player.name=`Test teams-${count} ${i+1} ${j+1} ff3d`;p.stats.luck.teams[i].players[j].name=player.name;p.stats.luck.teams[i].players[j].result.percentage=scores[(i*7+j)%scores.length]});});
 p.stats.drops=Array.from({length:3},(_,i)=>({...p.stats.drops[0],submissionId:`content-${i}`,characterName:p.stats.teams[0].players[0].name,item:{...p.stats.drops[0].item,name:'TEST Twisted bow with a long exact item variant'},valueGp:1400000000-i*1000000}));
 return p;
}
function assertLuckEndpoints(h,trackWidth,fontFactor=1){
 let common=null;
 for(const row of h.document.querySelectorAll('.luck-row')){
  const bar=row.querySelector('.luck-bar'),number=row.querySelector('.luck-value');if(bar.hidden)continue;
  const value=Number.parseFloat(number.title),width=Number.parseFloat(bar.style.width),progress=Number(bar.style.transform.match(/scaleX\(([^)]+)\)/)[1]);
  if(value){const proportion=width/Math.abs(value);if(common!==null)assert.ok(Math.abs(proportion-common)<1e-9,'all entries use the same proportional span');common=proportion;}
  const offset=Number.parseFloat((value<0?number.style.right:number.style.left).slice(5))*trackWidth/100+7;
  const labelWidth=number.textContent.length*11*.56*fontFactor;
  const left=value<0?trackWidth-offset-labelWidth:offset,right=left+labelWidth;
  assert.ok(left>=4-1e-6,`${number.textContent} leaves the left/name clearance (${left})`);assert.ok(right<=trackWidth-4+1e-6,`${number.textContent} leaves the right/panel clearance (${right})`);
  const end=trackWidth/2+(value<0?-1:1)*width/100*trackWidth*progress;
  assert.ok(Math.abs((value<0?left+labelWidth+7:left-7)-end)<1e-6,'label and visible bar endpoint stay synchronized');
  assert.equal(number.textContent,`${Number((value*progress).toFixed(1))>0?'+':''}${Number((value*progress).toFixed(1))}%`);
 }
 return common;
}
for(const count of [2,3,5,8,15])test(`R1–R3 long content stays available and measured labels fit for ${count} teams`,async()=>{
 const p=contentFixture(count),original=JSON.stringify(p),options={trackWidth:130,ringWidth:130},h=harness(p,true,1150,options),q=s=>h.document.querySelector(s);
 h.tick(0);h.tick(500);assertLuckEndpoints(h,130);h.tick(1000);assertLuckEndpoints(h,130);h.tick(1200);
 const raceNames=h.document.querySelectorAll('.race-team');raceNames.forEach((label,i)=>{assert.equal(label.textContent,[...p.stats.teams].sort((a,b)=>a.teamId.localeCompare(b.teamId))[i].name);assert.equal(label.title,label.textContent)});
 const mark=q('.race-mark');mark.focus();const key=mark.dataset.raceMarker;assert.ok(q('#race-detail').textContent.includes(p.stats.teams[0].name));
 assert.equal(q('#share-total-value').textContent,'55.45B');assert.equal(q('#share-total-value').title,'55.45B GP');assert.equal(q('#share-total-value').getAttribute('aria-label'),'55.45B GP');
 const total=q('.share-total'),caption=total.querySelector('span'),mask=q('#gp-donut defs');
 function fitted(){
  const valueSize=Number.parseFloat(total.style['--share-value-size']),captionSize=Number.parseFloat(total.style['--share-caption-size']);
  const width=Math.max(q('#share-total-value').textContent.length*valueSize*.48,caption.textContent.length*captionSize*.56),height=valueSize+captionSize*1.5+5;
  assert.ok(Math.hypot(width,height)<=options.ringWidth*125/180-8+.001,'entire text rectangle fits the supplied inner-circle geometry');assert.ok(valueSize>0&&valueSize<=34);assert.ok(captionSize>=8&&captionSize<=9);
 }
 fitted();const compact=Number.parseFloat(total.style['--share-value-size']);options.ringWidth=150;options.trackWidth=700;h.resize('.share-ring');h.resize('.luck-panel');h.tick(1250);fitted();
 assert.ok(Number.parseFloat(total.style['--share-value-size'])>=compact);assert.ok(Math.abs(assertLuckEndpoints(h,700)*h.api.scale()-40)<1e-8,'roomy tracks retain the approved 40% half-track');
 assert.equal(q('#gp-donut defs'),mask);assert.equal(h.document.activeElement.dataset.raceMarker,key,'content fitting does not replace a focused marker');
 h.api.setLuckMode('players');h.api.finishLuckMotion();const middle=h.api.luckPlayers().find(entry=>!h.api.luckExtremes().some(extreme=>extreme.id===entry.id));
 if(middle){h.api.selectLuckPlayer(middle.id);h.api.finishLuckMotion();const pinned=q('.luck-pinned-row');assert.ok(pinned);assert.equal(pinned.querySelector('.luck-pinned-name').textContent,middle.name);assert.ok(pinned.querySelector('.luck-clear-pin').getAttribute('aria-label').includes(middle.name));assertLuckEndpoints(h,700);await pinned.querySelector('.luck-clear-pin').fire('click');h.animations.at(-1).finish();assert.equal(h.api.state().luckPinnedId,null);assert.equal(q('.luck-pinned-row'),null);}
 assert.equal(JSON.stringify(p),original,'actual names and values are not shortened or modified');
});
test('R1 resize during Luck entry keeps progress; unavailable labels and reduced motion remain usable',()=>{
 const options={trackWidth:180,ringWidth:130},p=contentFixture(8);p.stats.luck.teams[0].result.percentage=null;p.stats.luck.teams[0].result.status='WaitingForActivityData';
 const h=harness(p,true,1150,options);h.tick(0);h.tick(350);assertLuckEndpoints(h,180);const first=h.document.querySelector('.luck-bar'),progress=first.style.transform,calls={...h.calls};
 options.trackWidth=140;h.resize('.luck-panel');h.tick(400);assertLuckEndpoints(h,140);assert.equal(h.calls.renderLuck,calls.renderLuck);assert.notEqual(first.style.transform,progress,'existing entry frame continues');
 const unavailable=h.document.querySelector('.luck-unavailable');assert.equal(unavailable.textContent,'Waiting for activity');assert.equal(unavailable.title,'Waiting for activity');assert.equal(unavailable.parentElement.querySelector('.luck-bar').hidden,true);
 h.media.matches=true;h.listeners.motion.forEach(fn=>fn());assertLuckEndpoints(h,140);assert.ok(h.document.querySelectorAll('.luck-bar').filter(bar=>!bar.hidden).every(bar=>bar.style.transform==='scaleX(1)'));
});
function contentCssRules(){
 const {production}=statsCssTrees(),integration=process.env.STATS_PASS5_INTEGRATION_CSS||path.join(root,'wwwroot/css/stats-integration.css');
 return [...production,cssTree(fs.readFileSync(integration,'utf8'))].flatMap(tree=>flatCssRules(tree));
}
test('R1–R4 scoped content cascade bounds names, text and natural drop height at narrow card/wide viewport',()=>{
 const rules=contentCssRules(),h=harness(contentFixture(5)),wrapper=h.document.createElement('div');wrapper.className='public-stats-page';h.document.documentElement.append(wrapper);wrapper.append(h.page);
 h.api.setLuckMode('players');const middle=h.api.luckPlayers().find(entry=>!h.api.luckExtremes().some(extreme=>extreme.id===entry.id));h.api.selectLuckPlayer(middle.id);h.api.finishLuckMotion();
 const targets=['.race-team','.race-row','.race-scroll','.luck-row','.luck-key','.luck-clear-pin','.share-total strong','.share-total span','.treasure','.treasure-list','.treasure-item','.item-details','.item-details h3','.item-byline','.item-value','.gp-visuals'];
 const elements=Object.fromEntries(targets.map(s=>[s,h.document.querySelector(s)])),matching=Object.fromEntries(targets.map(s=>[s,rules.filter(rule=>!rule.selector.includes(':')&&matchesLayoutSelector(elements[s],rule.selector))]));
 const get=(selector,property,viewport,card)=>cascadeProbe(matching[selector],viewport,card)[property]?.value;
 for(const viewport of [320,480,850,1150,1600])for(const card of [280,360,599,600,601,649,650,651,1000]){
  const value=(selector,property)=>get(selector,property,viewport,card);
  assert.equal(value('.race-team','white-space'),'nowrap');assert.equal(value('.race-team','text-overflow'),'ellipsis');assert.equal(value('.race-team','min-width'),'0');assert.equal(value('.race-team','overflow'),'hidden');assert.equal(value('.race-row','min-height'),'43px');assert.equal(value('.race-scroll','overflow-x'),'auto');
  assert.equal(value('.luck-row','grid-template-columns'),'minmax(min(80px,25%),.8fr) minmax(calc(2 * var(--luck-label-reserve,48px) + 16px),1.6fr)');assert.equal(value('.luck-key','grid-template-columns'),value('.luck-row','grid-template-columns'));assert.equal(value('.luck-clear-pin','flex'),'none');
  assert.equal(value('.share-total strong','font-size'),'var(--share-value-size,34px)');assert.equal(value('.share-total span','font-size'),'var(--share-caption-size,9px)');
  for(const selector of ['.treasure','.treasure-list']){assert.equal(value(selector,'flex'),'1 0 auto');assert.equal(value(selector,'min-height'),'min-content');assert.notEqual(value(selector,'overflow'),'hidden');}
  for(const selector of ['.item-details h3','.item-byline']){assert.equal(value(selector,'-webkit-line-clamp'),'2');assert.equal(value(selector,'overflow-wrap'),'anywhere');}
  assert.equal(value('.item-value','white-space'),'nowrap');assert.notEqual(value('.item-value','overflow'),'hidden');
  assert.equal(value('.treasure-list','grid-template-columns'),card<=650||viewport<=480?'1fr':'repeat(3,minmax(0,1fr))');
  if(card<=650){assert.equal(value('.treasure-item','grid-template-columns'),'38px minmax(0,1fr)');assert.equal(value('.item-details','grid-template-columns'),'minmax(0,1fr) auto');assert.equal(value('.item-value','grid-row'),'1 / 3');assert.equal(value('.treasure-item','border-right'),'0');}
 }
 const queries=rules.filter(rule=>rule.selector.includes('.gp-panel .treasure-list')&&rule.ancestors.some(group=>group.includes('650px')));assert.ok(queries.length);queries.forEach(rule=>assert.deepEqual(rule.ancestors,['@container (max-width:650px)']));
 // These are computed winning declarations, not a claim that the synthetic DOM laid out rows.
 h.document.querySelector('.gp-panel').dataset.gpLayout='held';
 const held=rules.filter(rule=>!rule.selector.includes(':')&&matchesLayoutSelector(elements['.gp-visuals'],rule.selector));
 assert.equal(cascadeProbe(held,1150,599).height.value,'var(--gp-resting-visual-height)');assert.equal(cascadeProbe(held,1150,599)['grid-template-rows'].value,'var(--gp-resting-trend-height) var(--gp-resting-share-height)');
});
for(const count of [2,3,5,8,15])test(`R4 ${count}-team valuable drops retain full text and values through default/held scopes`,async()=>{
 const p=contentFixture(count),h=harness(p),q=s=>h.document.querySelector(s),cards=()=>h.document.querySelectorAll('.treasure-item');
 for(const scope of ['default','players','everyone','teams','team']){
  if(scope==='team')h.api.openTeam('team-0');else if(scope!=='default'){const button=h.document.querySelectorAll('#gp-tabs button').find(button=>button.dataset.mode===scope);await q('#gp-tabs').fire('click',{target:button});}
  assert.equal(cards().length,3);for(const article of cards())for(const selector of ['.item-details h3','.item-byline','.item-value']){const element=article.querySelector(selector);assert.equal(element.title,element.textContent);assert.ok(element.textContent.length);}
  assert.ok(cards()[0].querySelector('.item-byline').textContent.includes(p.stats.teams[0].name));assert.equal(cards()[0].querySelector('.item-value').textContent,'1.4B GP');
  if(scope!=='default'){assert.equal(q('.gp-panel').dataset.gpLayout,'held');assert.ok(q('.gp-panel').style['--gp-resting-visual-height']);}
 }
 const css=fs.readFileSync(path.join(root,'wwwroot/css/stats-density.css'),'utf8');assert.match(css,/\.treasure-item:hover \.item-art\{transform:translateY\(-3px\)\}/,'existing artwork hover stays intact');
 assert.ok(!h.animations.some(animation=>animation.element.closest('.treasure')),'no drop change animation was added');
});
test('R3 55.45B fits the normal/compact hole after loaded-font metrics change without replacing the donut',async()=>{
 const options={trackWidth:200,ringWidth:130,fonts:true},h=harness(contentFixture(8),true,1150,options),q=s=>h.document.querySelector(s),donut=q('#gp-donut'),segment=q('.share-segment'),mask=q('#gp-donut defs');
 h.tick(0);h.tick(300);options.fontFactor=1.1;h.finishFonts();await settle();h.tick(350);assertLuckEndpoints(h,200,1.1);
 for(const ringWidth of [130,150]){
  options.ringWidth=ringWidth;h.resize('.share-ring');h.tick(400);
  const total=q('.share-total'),value=q('#share-total-value'),caption=total.querySelector('span'),v=Number.parseFloat(total.style['--share-value-size']),c=Number.parseFloat(total.style['--share-caption-size']);
  assert.ok(v>0&&Number.isFinite(v),'full formatted number has a usable fitted size');assert.ok(Math.hypot(Math.max(value.textContent.length*v*.48,caption.textContent.length*c*.56)*1.1,v+c*1.5+5)<=ringWidth*125/180-8+.001);
  assert.equal(value.textContent,'55.45B');assert.equal(q('#gp-donut'),donut);assert.equal(q('.share-segment'),segment);assert.equal(q('#gp-donut defs'),mask);assert.ok(!h.document.querySelector('[aria-hidden=true][id=share-total-value]'),'measurement clones never duplicate the value id');
 }
});
test('R4 three columns remain at 651px card width and stack at 650px even in a 1600px viewport',()=>{
 const rules=contentCssRules(),h=harness(contentFixture(3)),wrapper=h.document.createElement('div');wrapper.className='public-stats-page';h.document.documentElement.append(wrapper);wrapper.append(h.page);
 for(const held of [false,true]){
  if(held)h.api.openTeam('team-0');const list=h.document.querySelector('.treasure-list'),matching=rules.filter(rule=>!rule.selector.includes(':')&&matchesLayoutSelector(list,rule.selector));
  assert.equal(cascadeProbe(matching,1600,650)['grid-template-columns'].value,'1fr');assert.equal(cascadeProbe(matching,1600,651)['grid-template-columns'].value,'repeat(3,minmax(0,1fr))');
  assert.equal(cascadeProbe(matching,1600,650).flex.value,'1 0 auto','drop content cannot shrink out of its allocated natural height');
 }
});

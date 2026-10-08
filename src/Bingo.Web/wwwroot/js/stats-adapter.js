// Presentation adapter for the approved Stats renderers. Server totals, shares, Luck and
// progress snapshots are authoritative; this module only aligns them to chart coordinates.
(() => {
  'use strict';
  const palette=['--blue','--coral','--sage','--bronze','--purple'];
  const artByWikiId={'13576':'dragon-warhammer-detail.png','20997':'twisted-bow.png','27275':'tumekens-shadow.png','26384':'torva-platebody.png','4151':'abyssal-whip.png','11832':'bandos-chestplate.png','6737':'berserker-ring.png'};
  const titles={start:'Event started',submission:'First submission','all-submissions':'All teams submitted',tile:'First tile completed','all-tiles':'All teams completed a tile',halfway:'Halfway there',row:'First row completed','all-rows':'All teams completed a row',board:'First board completed','all-boards':'All teams completed the board',end:'Event ended'};
  const safeImage=value=>typeof value==='string'&&(/^(https?:\/\/)/i.test(value)||/^\/(?!\/)/.test(value))?value:null;
  const statusText=result=>({NoEligibleActivity:'No eligible activity',WaitingForActivityData:'Waiting for activity',WaitingForActivityUpdate:'Waiting for activity update',Incomplete:'Incomplete data'}[result.status]||'Unavailable');
  const rounded=value=>{const number=Number(value.toFixed(1));return Object.is(number,-0)?0:number};
  const luckText=result=>result.percentage!=null?`${rounded(result.percentage)}%${result.zeroRecordedApproximation?' · zero-recorded estimate':result.estimated?' · estimated':''}`:statusText(result);
  const kcText=result=>result.kcDifference!=null?`${rounded(result.kcDifference)>0?'+':''}${rounded(result.kcDifference)} KC${result.zeroRecordedApproximation?' · zero-recorded estimate':result.estimated?' · estimated':''}`:statusText(result);
  const playerKey=(teamId,playerId)=>`${teamId}/${playerId}`;
  function adapt(payload) {
    const stats=payload.stats,artwork=new Map(payload.artwork.map(item=>[item.itemId,item]));
    const observed=stats.valueHistory.map(point=>Date.parse(point.at));
    const progressTimes=stats.teams.flatMap(team=>[...team.progressHistory.map(point=>Date.parse(point.at)),...(team.officialCompletion?.completedAt?[Date.parse(team.officialCompletion.completedAt)]:[])]);
    const started=stats.startedAt?Date.parse(stats.startedAt):null;
    const now=Date.parse(payload.generatedAt);
    const start=Math.min(started??observed[0]??now,...observed,...progressTimes);
    const end=Math.max(start+1,stats.endedAt?Date.parse(stats.endedAt):now,...observed,...progressTimes);
    const dates=[...new Set([start,...observed,end])].sort((a,b)=>a-b);
    const atTime=(history,at)=>history.findLast(point=>Date.parse(point.at)<=at);
    const values=history=>dates.map(at=>(atTime(history,at)?.value.knownValueGp??0)/1000000);
    const teamColor=index=>palette[index]||`--sample-team-${index+1}`;
    const teams=stats.teams.map((team,index)=>({id:team.teamId,name:team.name,color:teamColor(index),values:values(team.valueHistory),value:team.value,share:team.eventValueShare,raw:team}));
    const contributors=teams.flatMap((team,teamIndex)=>team.raw.players.map((player,index)=>({id:playerKey(team.id,player.playerId),playerId:player.playerId,name:player.name,teamId:team.id,teamName:team.name,color:index===0?team.color:`--gp-player-${teamIndex}-${index-1}`,values:values(player.valueHistory),value:player.value,share:player.eventValueShare,teamShare:player.teamValueShare,raw:player})));
    const currentPlayerId=payload.currentMembership?playerKey(payload.currentMembership.teamId,payload.currentMembership.playerId):null;
    const everyone=[{id:'everyone',name:'Everyone',color:'--blue',values:values(stats.valueHistory),value:stats.value}];
    const itemImage=item=>{
      const identity=artwork.get(item.itemId);
      const asset=artByWikiId[identity?.externalIdentifier];
      return asset?`/stats/assets/items/${asset}`:safeImage(item.imageUrl);
    };
    const drops=stats.drops.map(drop=>({id:drop.submissionId,itemId:drop.item.itemId,name:drop.item.name,asset:itemImage(drop.item),value:drop.valueGp==null?null:drop.valueGp/1000000,at:drop.submittedAt,player:contributors.find(p=>p.id===playerKey(drop.teamId,drop.playerId))||{id:playerKey(drop.teamId,drop.playerId),playerId:drop.playerId,name:drop.characterName,teamId:drop.teamId,teamName:teams.find(t=>t.id===drop.teamId)?.name||''}}));
    const luckEntry=(entry,team)=>({id:team?playerKey(team.teamId,entry.playerId):entry.teamId,playerId:entry.playerId,name:entry.name,teamId:entry.teamId||team?.teamId,teamName:team?.name,value:entry.result.percentage,kcDifference:entry.result.kcDifference,result:entry.result,label:luckText(entry.result),kcLabel:kcText(entry.result)});
    const luck={teams:stats.luck.teams.map(team=>luckEntry(team)),players:stats.luck.teams.flatMap(team=>team.players.map(player=>luckEntry(player,team)))};
    const day=at=>(Date.parse(at)-start)/(end-start)*13;
    const timeAt=coordinate=>start+coordinate/13*(end-start);
    const date=(at,withTime=false)=>new Intl.DateTimeFormat(document.documentElement.lang||'en',{timeZone:stats.timezone,month:'short',day:'2-digit',...(withTime?{year:'numeric',hour:'2-digit',minute:'2-digit',hourCycle:'h23'}:{})}).format(new Date(at));
    const tileMap=new Map(payload.tiles.map(tile=>[tile.id,tile.name]));
    const tileName=id=>tileMap.get(id)||'Tile';
    const progress=teams.map(team=>{
      const marks=[],seen=new Set(),official=team.raw.officialCompletion;let previousLines=0;
      team.raw.progressHistory.forEach(point=>{
        const newlyComplete=point.tiles.filter(tile=>tile.complete&&!seen.has(tile.id));
        newlyComplete.forEach((tile,index)=>{
          seen.add(tile.id);
          marks.push({at:point.at,day:day(point.at),tileId:tile.id,name:tileName(tile.id),asset:safeImage(payload.tiles.find(entry=>entry.id===tile.id)?.imageUrl),tiles:point.completedTiles,lines:point.completedLines,approved:point.approved,target:point.target,finished:!official&&point.boardComplete&&index===newlyComplete.length-1,gained:index===newlyComplete.length-1?Math.max(0,point.completedLines-previousLines):0});
        });
        previousLines=point.completedLines;
      });
      if(official?.boardComplete&&official.completedAt){
        const at=official.completedAt,matching=marks.findLast(mark=>Date.parse(mark.at)===Date.parse(at));
        if(matching)matching.finished=true;
        else {
          const point=atTime(team.raw.progressHistory,Date.parse(at));
          marks.push({at,day:day(at),name:'Official completion',tiles:official.completedTiles,lines:official.completedLines,approved:point?.approved??0,target:point?.target??team.raw.progress.tiles.reduce((sum,tile)=>sum+tile.target,0),finished:true,gained:0});
          marks.sort((a,b)=>a.day-b.day);
        }
      }
      return {team,marks,times:marks.map(mark=>mark.day),rowGains:Object.fromEntries(marks.map((mark,index)=>[index,mark.gained])),history:team.raw.progressHistory};
    });
    const milestones=selected=>{
      const source=selected==='all'?stats.milestones:teams.find(team=>team.id===selected)?.raw.milestones||[];
      return source.map(item=>{
        const team=teams.find(team=>team.id===item.teamId),player=contributors.find(player=>player.id===playerKey(item.teamId,item.playerId));
        let detail=item.id==='start'?stats.name:item.id.startsWith('all-')?`All ${teams.length} teams`:'';
        if(item.id==='submission')detail=[player?.name,selected==='all'?team?.name:null].filter(Boolean).join(' · ');
        if(item.id==='tile')detail=[selected==='all'?team?.name:null,tileName(item.tileId)].filter(Boolean).join(' · ');
        if(item.id==='halfway')detail=[selected==='all'?team?.name:null,`${Math.ceil(stats.rows*stats.columns/2)}/${stats.rows*stats.columns} tiles`].filter(Boolean).join(' · ');
        if(item.id==='row')detail=[selected==='all'?team?.name:null,`${item.lineKind==='column'?'Column':'Row'} ${(item.lineIndex??0)+1}`].filter(Boolean).join(' · ');
        if(item.id==='board')detail=selected==='all'?team?.name||'':`${stats.rows*stats.columns}/${stats.rows*stats.columns} tiles`;
        return {id:item.id,title:selected!=='all'&&item.id==='board'?'Board completed':titles[item.id],order:item.defaultOrder,reached:item.state==='Reached',missed:item.state==='NotReached',at:item.at,detail:item.state==='Reached'?detail:'',asset:item.drop?itemImage(item.drop.item):null,dropName:item.drop?.item.name};
      });
    };
    return {payload,stats,teams,currentPlayerId,contributors,players:contributors,everyone,luck,drops,dates,positions:dates.map(at=>(at-start)/(end-start)),start,end,progress,milestones,day,timeAt,date,itemImage,artwork,atTime};
  }
  window.StatsAdapter={adapt,luckText,kcText,safeImage};
})();

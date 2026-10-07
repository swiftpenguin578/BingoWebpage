import './event-schedule.js';
import { mountDateTime, localToUtc, utcToLocal } from './admin-date-time.js';

let release;
export function dispose() { const cleanup=release; release=null; return cleanup?.(); }
export async function init(region, ui=window.AdminUI, retained=null) {
  await dispose();
  const root=region.matches?.('[data-schedule-editor]')?region:region.querySelector('[data-schedule-editor]');
  if(!root)return;
  const form=root.querySelector('form'), life=new AbortController(), text=key=>root.dataset[key]||'';
  const observedAt=Date.now(), now=()=>Date.parse(root.dataset.now)+Date.now()-observedAt;
  const fieldNodes=[...root.querySelectorAll('[data-schedule-field]')], fields=fieldNodes.map(el=>el.dataset.scheduleField);
  const nodes=Object.fromEntries(fieldNodes.map(el=>[el.dataset.scheduleField,el]));
  const inputs=Object.fromEntries(fieldNodes.map(el=>[el.dataset.scheduleField,form.elements.namedItem('Input.'+el.dataset.localName)]));
  const save=root.querySelector('[data-identity-save]'), dirtyNote=root.querySelector('[data-identity-dirty]'), saved=root.querySelector('[data-identity-state]'), feedback=root.querySelector('[data-schedule-feedback]');
  let current=JSON.parse(root.dataset.current), pending=false, uncertain=null, layer=null, reason=retained?.reason??form.elements.namedItem('Input.EventEndReason').value;
  const pickers={};
  const listen=(el,type,fn)=>el?.addEventListener(type,fn,{signal:life.signal});
  const displayTimezone=snapshot=>snapshot.displayTimezone||snapshot.timezone;
  const local=(snapshot,field)=>snapshot.values[field]===null?'':utcToLocal(Date.parse(snapshot.values[field]),displayTimezone(snapshot));
  const draft=()=>Object.fromEntries(fields.map(field=>[nodes[field].dataset.label,inputs[field].value]));
  const changed=()=>fields.some(field=>inputs[field].value!==local(current,field));
  const dirty=()=>!!uncertain||changed()||Object.values(pickers).some(picker=>picker.dirty());
  const format=value=>{if(value==null)return text('none');const parts=Object.fromEntries(new Intl.DateTimeFormat(document.documentElement.lang==='da'?'da-DK':'en-GB',{timeZone:displayTimezone(current),day:'numeric',month:'short',year:'numeric',hour:'2-digit',minute:'2-digit',hourCycle:'h23'}).formatToParts(new Date(value)).map(p=>[p.type,p.value]));return `${Number(parts.day)} ${document.documentElement.lang==='da'?parts.month:parts.month.slice(0,3)} ${parts.year}, ${parts.hour}:${parts.minute}`;};
  const offset=value=>value==null?'':new Intl.DateTimeFormat('en-GB',{timeZone:displayTimezone(current),timeZoneName:'longOffset'}).formatToParts(new Date(value)).find(part=>part.type==='timeZoneName').value.replace('GMT','UTC');
  const error=(field,message)=>{const el=nodes[field].querySelector('[data-field-error]')||nodes[field].querySelector('.field-err'); if(el){el.hidden=!message;el.classList.toggle('field-validation-valid',!message);el.classList.toggle('field-validation-error',!!message);const span=el.querySelector('[data-component-text]');(span||el).textContent=message;pickers[field]?.setInvalid(!!message);} };
  function report(message,tone='warning') {
    const component=ui.template('banner-'+tone).firstElementChild;
    component.querySelector('[data-component-text]').textContent=message;
    feedback.replaceChildren(...component.childNodes); feedback.className='banner is-'+tone;feedback.hidden=!message;
  }
  function intended(validate=false) {
    const values={...current.values};let valid=true;
    for(const field of fields){
      if(validate)error(field,'');
      if(!current.editable[field]||inputs[field].value===local(current,field))continue;
      if(!inputs[field].value){values[field]=null;continue;}
      try{const result=localToUtc(inputs[field].value,current.timezone);if(result.status!=='ok'){valid=false;if(validate)error(field,text(result.status==='ambiguous'?'dateAmbiguous':result.status==='invalid'?'dateSkipped':'dateInvalid'));}else values[field]=new Date(result.ms).toISOString();}
      catch{valid=false;if(validate)error(field,text('dateInvalid'));}
    }
    const openingChanged=values.signupOpensAt!==current.values.signupOpensAt;
    values.scheduledSignupOpeningEnabled=current.phase==='Draft'&&values.signupOpensAt!==null&&(!openingChanged&&current.values.scheduledSignupOpeningEnabled||openingChanged&&Date.parse(values.signupOpensAt)>now());
    return valid?values:null;
  }
  function paint() {
    for(const field of fields)pickers[field]?.setDisabled(pending||!!uncertain||!current.editable[field]);
    if(save){save.disabled=pending;save.setAttribute('aria-disabled',String(!pending&&!uncertain&&!dirty()));save.title=!uncertain&&!dirty()?text('noChanges'):'';save.classList.toggle('is-busy',pending);save.querySelector('.spin').hidden=!pending;save.querySelector('[data-component-text]').textContent=text(pending?(uncertain?'checking':'saving'):uncertain?'checkAgain':'save');}
    if(dirtyNote)dirtyNote.hidden=!dirty();if(saved&&dirty())saved.hidden=true;
    const values=intended(),end=values?.eventEndsAt; root.querySelector('.sc-derived').hidden=!end;root.querySelector('[data-uploads-close]').textContent=end?format(Date.parse(end)+30*60*1000):text('none');
    const warning=root.querySelector('[data-overdue-start]');if(warning){warning.hidden=!(current.values.eventStartsAt&&Date.parse(current.values.eventStartsAt)<=now()&&inputs.eventStartsAt.value===local(current,'eventStartsAt'));nodes.eventStartsAt.querySelector('.field-hint').hidden=!warning.hidden;}
    if(layer){layer.element.setAttribute('aria-busy',String(pending));for(const b of layer.element.querySelectorAll('button,textarea'))b.disabled=pending;const action=layer.element.querySelector('[data-confirm-accept]');action.classList.toggle('is-busy',pending);action.querySelector('.spin').hidden=!pending;}
    ui.refreshDirty?.();
  }
  for(const field of fields){const picker=nodes[field].querySelector('[data-date-time]');if(picker)pickers[field]=mountDateTime(picker,inputs[field],()=>{error(field,'');nodes[field].querySelector('[data-schedule-conflict]').hidden=true;paint();},root.querySelector('[data-date-time-template]'));}
  function replaceCurrent(next,reset=false) {
    for(const field of fields){
      const mine=inputs[field].value,was=local(current,field),theirs=local(next,field),note=nodes[field].querySelector('[data-schedule-conflict]');const noteText=note.querySelector('[data-conflict-text]');noteText.replaceChildren();note.hidden=true;
      if(reset||mine===was||!next.editable[field]){inputs[field].value=theirs;pickers[field]?.refresh();if(!reset&&theirs!==was){note.hidden=false;noteText.textContent=text('updated');}}
      else if(theirs!==was&&mine!==theirs){
        note.hidden=false;noteText.append(document.createTextNode(text('theirs')+' '+format(next.values[field])+'. '+text('mineKept')+' '));
        const button=document.createElement('button');button.type='button';button.className='text-btn';button.textContent=text('useTheirs');listen(button,'click',()=>{inputs[field].value=theirs;pickers[field]?.refresh();note.hidden=true;paint();});noteText.append(button);
      }
      const picker=nodes[field].querySelector('[data-date-time]');if(picker)picker.dataset.timezone=displayTimezone(next);
    }
    current=next;if(next.phase!=='Draft')root.dataset.published='true';form.elements.namedItem('Input.Version').value=next.version;
    root.dataset.current=JSON.stringify(next);paint();
  }
  const contextChanged=next=>next.phase!==current.phase||next.timezone!==current.timezone||next.draftState!==current.draftState||fields.some(field=>next.editable[field]!==current.editable[field]);
  async function replaceView(doc, carry=null){
    const replacement=document.importNode(doc.querySelector('[data-schedule-editor]'),true);
    const oldBanner=root.parentNode.querySelector('.page-banner'),newBanner=doc.querySelector('.page-banner');
    oldBanner?.remove();if(newBanner)root.before(document.importNode(newBanner,true));
    const summary=document.querySelector('.page-head .summary'),newSummary=doc.querySelector('.page-head .summary');
    if(summary&&newSummary)summary.replaceChildren(...[...newSummary.childNodes].map(node=>document.importNode(node,true)));
    await dispose();root.replaceWith(replacement);await init(replacement,ui,carry);ui.refreshContext(doc);return replacement;
  }
  async function refreshContextView(message,reset=false,doc=null){
    const carry={baseline:current,entries:Object.fromEntries(fields.map(field=>[field,inputs[field].value])),reason,message,reset};
    if(!doc){
      const outcome=await ui.busy(()=>window.AdminFetch.request(form.action,{expect:'html',cache:'no-store',readback:true,signal:life.signal,draft:draft()}));
      if(life.signal.aborted||outcome.kind!=='handler')return false;
      doc=new DOMParser().parseFromString(outcome.data,'text/html');
    }
    const nextRoot=doc.querySelector('[data-schedule-editor]');if(!nextRoot)return false;
    const next=JSON.parse(nextRoot.dataset.current);if(next.eventId!==current.eventId||BigInt(next.version)<BigInt(current.version))return false;
    if(reset&&uncertain){const freshValues=window.createScheduleReadbackSession(next,next.values,form.action).expected;carry.reset=Object.keys(uncertain.expected).every(key=>freshValues[key]===uncertain.expected[key]);carry.message=carry.reset?'upToDate':'stale';}
    await replaceView(doc,carry);return true;
  }
  async function checkAgain(){
    if(pending||!uncertain)return;pending=true;paint();
    const result=await ui.busy(()=>uncertain.checkAgain(life.signal,draft()));if(life.signal.aborted)return;pending=false;
    if(result.current&&contextChanged(result.current)){if(await refreshContextView(result.state==='upToDate'?'upToDate':'stale',result.state==='upToDate'))return;report(text('unknown'));paint();return;}
    if(result.state==='upToDate'){uncertain=null;replaceCurrent(result.current,true);report(text('upToDate'),'info');}
    else if(result.state==='unchanged'){uncertain=null;report(text('unchanged'),'info');}
    else if(result.state==='different'){uncertain=null;replaceCurrent(result.current);report(text('stale'));}
    else report(text('unknown'));
    paint();save?.focus({preventScroll:true});
  }
  async function submit(confirmed=false){
    if(pending)return;if(uncertain){await checkAgain();return;}if(!dirty())return;
    let valid=true;for(const picker of Object.values(pickers))if(!picker.validate())valid=false;
    const expected=intended(true);if(!valid||!expected){form.reportValidity();return;}
    const consequence=fields.some(field=>field!=='draftAt'&&inputs[field].value!==local(current,field));
    if(!confirmed&&consequence&&(root.dataset.published==='true'||current.phase==='Live')){openReview(expected);return;}
    const data=new FormData(form);data.set('Input.ConfirmChanges',String(confirmed));data.set('Input.EventEndReason',reason);
    const session=window.createScheduleReadbackSession(current,expected,form.action);pending=true;paint();
    const outcome=await ui.busy(()=>window.AdminFetch.request(form.action,{method:'POST',body:data,expect:'html',allowRedirectTo:form.action,notice:false,signal:life.signal,draft:draft()}));
    if(life.signal.aborted)return;pending=false;
    if(outcome.kind==='session-lost'){await layer?.close(false);window.AdminFetch.sessionNotice(draft(),outcome.destination);paint();return;}
    if(outcome.kind==='refused'){await layer?.close(false);report(text('readonly'),'error');paint();return;}
    const doc=outcome.kind==='handler'?new DOMParser().parseFromString(outcome.data,'text/html'):null,next=doc?.querySelector('[data-schedule-editor]');
    if(!next){await layer?.close(false);uncertain=session;report(text('unknown'));paint();return;}
    const observed=JSON.parse(next.dataset.current);
    if(observed.eventId!==current.eventId){await layer?.close(false);uncertain=session;report(text('unknown'));paint();return;}
    const success=outcome.response.redirected&&new URL(outcome.response.url).pathname===new URL(form.action).pathname;
    if(success){await layer?.close(false);const replacement=await replaceView(doc);for(const toast of doc.querySelectorAll('[data-toast-host] [data-toast]'))ui.toast(toast.querySelector('.grow')?.textContent||toast.textContent);replacement.querySelector('[data-identity-save]')?.focus({preventScroll:true});return;}
    if(observed.version!==current.version){await layer?.close(false);if(contextChanged(observed)&&await refreshContextView('stale'))return;replaceCurrent(observed);report(text('stale'));}
    else {
      for(const field of fields){const err=next.querySelector(`[data-schedule-field="${field}"] .field-err`);error(field,err&&!err.hidden?err.textContent.trim():'');}
      const reasonError=doc.querySelector('[data-schedule-reason-error]')?.textContent;
      if(reasonError&&layer){const error=layer.element.querySelector('[data-reason-error]');error.textContent=reasonError;error.hidden=false;}
      else {await layer?.close(false);const banner=next.querySelector('.form-banners .banner:not([hidden])');report(banner?.textContent.trim()||'', 'error');}
    }
    paint();
  }
  function openReview(expected){
    if(layer)return;
    const content=ui.template('confirmation'),live=current.phase==='Live';
    const title=text(live?'liveTitle':'publishedTitle');content.querySelector('[data-confirm-title]').textContent=title;content.querySelector('[data-confirm-description]').textContent=text(live?'liveBody':'confirmBody')+' '+current.timezone;
    const actions=content.querySelector('.m-actions'),table=document.createElement('div');table.className='compare';table.setAttribute('role','table');
    function row(label,before,after,head=false){const r=document.createElement('div');r.className=head?'compare-head':'compare-row';r.setAttribute('role','row');for(const [index,value]of[label,before,after].entries()){const c=document.createElement('span');c.className=index===0?'compare-label':index===1?'compare-old':'compare-new';c.setAttribute('role',head?'columnheader':index===0?'rowheader':'cell');c.textContent=head||index===0?value:format(value);if(!head&&index){const off=document.createElement('span');off.className='compare-off';off.textContent=offset(value);c.append(off);}r.append(c);}table.append(r);}
    row(text('time'),text('nowLabel'),text('after'),true);
    for(const field of fields)if(field!=='draftAt'&&inputs[field].value!==local(current,field))row(nodes[field].dataset.label,current.values[field],expected[field]);
    if(inputs.eventEndsAt.value!==local(current,'eventEndsAt'))row(text('uploads'),current.values.eventEndsAt?Date.parse(current.values.eventEndsAt)+1800000:null,expected.eventEndsAt?Date.parse(expected.eventEndsAt)+1800000:null);
    actions.before(table);
    if(inputs.draftAt.value!==local(current,'draftAt')){const note=document.createElement('p');note.className='compare-foot';note.textContent=text('draftTogether');actions.before(note);}
    let reasonInput,error;
    if(live){const field=document.createElement('div');field.className='field';const label=document.createElement('label');label.className='lbl';label.htmlFor='schedule-end-reason';label.textContent=text('reason');reasonInput=document.createElement('textarea');reasonInput.id='schedule-end-reason';reasonInput.className='textarea sc-reason';reasonInput.rows=4;reasonInput.maxLength=2000;reasonInput.value=reason;reasonInput.required=true;const hint=document.createElement('div');hint.className='field-hint';hint.textContent=text('reasonHint');error=document.createElement('div');error.className='field-err';error.dataset.reasonError='';error.hidden=true;const count=document.createElement('div');count.className='field-hint';count.hidden=reason.length<=1700;count.textContent=reason.length+' / 2,000';listen(reasonInput,'input',()=>{reason=reasonInput.value;count.hidden=reason.length<=1700;count.textContent=reason.length+' / 2,000';error.hidden=true;});field.append(label,reasonInput,hint,error,count);actions.before(field);}
    content.querySelector('[data-confirm-cancel]').textContent=text('keepEditing');content.querySelector('[data-confirm-accept] [data-component-text]').textContent=text('save');
    layer=ui.openLayer({title,content,confirmation:true,dismissible:true,pending:()=>pending,opener:save,onClose:()=>{layer=null;}});layer.element.classList.add('is-wide');layer.element.dataset.pageFamily='schedule';
    listen(layer.element.querySelector('[data-confirm-cancel]'),'click',()=>{if(!pending)void layer.close(false);});
    listen(layer.element.querySelector('[data-confirm-accept]'),'click',()=>{if(live&&(!reason.trim()||reason.length>2000)){error.textContent=text(reason.length>2000?'reasonLimit':'reasonRequired');error.hidden=false;reasonInput.focus();return;}void submit(true);});
  }
  const unregister=ui.registerDraft(root,{isDirty:dirty,isPending:()=>pending,closeTransient:async()=>{if(layer){if(!pending)await layer.close(false);return true;}const open=Object.values(pickers).filter(picker=>picker.isOpen());for(const picker of open)picker.close();return open.length>0;},confirmLeave:async()=>{if(!uncertain)return ui.confirmDiscard();const result=await ui.confirm({title:text('leaveTitle'),description:text('leaveMessage'),cancelLabel:text('leave'),actionLabel:text('checkAgain'),cancelClass:'btn-outline-danger',cancelResult:'leave',focusAction:true});if(result===true)await checkAgain();return result==='leave';},discard:()=>{uncertain=null;replaceCurrent(current,true);}});
  listen(form,'submit',event=>{event.preventDefault();void submit();});
  release=async()=>{life.abort();unregister();for(const picker of Object.values(pickers))picker.dispose();await layer?.close(false);};
  if(retained){const fresh=current;current=retained.baseline;for(const field of fields)inputs[field].value=retained.entries[field];replaceCurrent(fresh,retained.reset);report(text(retained.message),retained.message==='upToDate'?'info':'warning');}
  paint();
}

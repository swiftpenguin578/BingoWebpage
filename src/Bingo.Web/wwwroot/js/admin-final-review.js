// U9 / RC08: event-scoped intents, guarded shared layers and honest cached readback.
let release = () => {};
export function dispose() { release(); release = () => {}; }
export function init(region, ui = window.AdminUI) {
  dispose();
  const life = new AbortController(), on = (node, name, fn) => node?.addEventListener(name, fn, {signal:life.signal});
  const root = region.querySelector('[data-final-review]'); if (!root) return;
  const labels = JSON.parse(root.dataset.labels), t = (key,...args) => (labels[key] || key).replace(/\{(\d+)\}/g,(_,i)=>String(args[+i] ?? ''));
  const base = new URL(location.href); base.search = ''; base.hash = '';
  const eventId = root.dataset.eventId, storageKey = 'u9-final:' + eventId;
  let state = JSON.parse(root.dataset.current), dialog = null, drawer = null, writing = false, closingFromUrl = false;
  const saved = () => { try { return JSON.parse(sessionStorage.getItem(storageKey) || '{}'); } catch { return {}; } };
  const save = patch => { try { sessionStorage.setItem(storageKey, JSON.stringify({...saved(),...patch})); } catch { /* Storage may be unavailable; live draft still stays in the layer. */ } };
  const el = (tag,cls,text) => { const node=document.createElement(tag);if(cls)node.className=cls;if(text!==undefined)node.textContent=text;return node; };
  const url = handler => {const u=new URL(base);if(handler)u.searchParams.set('handler',handler);return u.href;};
  const post = (handler, body, draft) => ui.busy(() => window.AdminFetch.request(url(handler), {method:'POST',body,draft,signal:life.signal}));
  const current = () => window.AdminFetch.request(url('Current'), {cache:'no-store',readback:true,signal:life.signal,draft:{[t('Reason')]:saved().reason||''}});
  async function refresh() {
    await ui.update(location.href, {root:region.querySelector('.page'),results:root,
      patch:doc=>{const next=doc.querySelector('[data-final-review]');if(!next)throw Error('Missing Final Review');
        const head=doc.querySelector('[data-page-region] .page-head');region.querySelector('.page-head').replaceChildren(...[...head.childNodes].map(n=>document.importNode(n,true)));
        root.replaceChildren(...[...next.childNodes].map(n=>document.importNode(n,true)));Object.assign(root.dataset,next.dataset);state=JSON.parse(root.dataset.current);ui.refreshContext(doc);return location.href;},
      pending:()=>document.querySelector('[data-page-loading-template="final-review"]').content.cloneNode(true),
      failed:()=>ui.template('load-failure'),fallbackFocus:()=>root.querySelector('#official-title,#pub-title')||region.querySelector('.h1')});
  }
  function notice(text, check=false, tone='is-warning') {
    const node=root.querySelector('[data-final-notice]');node.className='banner page-banner '+tone;node.hidden=false;node.replaceChildren(el('span','grow',text));
    if(check){const button=el('button','btn btn-sm',t('Check current state'));button.type='button';button.dataset.finalReadback='';node.append(button);}
    node.focus({preventScroll:false});
    root.querySelectorAll('[data-final-action] button[type="submit"]').forEach(b=>{if(check){b.dataset.intentDisabled=String(b.disabled);b.disabled=true;}else if(b.dataset.intentDisabled){b.disabled=b.dataset.intentDisabled==='true';delete b.dataset.intentDisabled;}});
  }
  const uncertain = kind => t(kind==='publish'?'We couldn’t confirm whether the results were published.':'We couldn’t confirm whether the results were reopened.');
  async function check() {
    if(writing)return;const intent=saved().pending;if(!intent)return;writing=true;ui.refreshDirty();
    const result=await ui.busy(()=>current(),true);writing=false;ui.refreshDirty();if(life.signal.aborted)return;
    if(result.kind==='handler'&&result.data?.eventId===eventId){state=result.data;save({pending:null});await refresh();
      notice(uncertain(intent.kind)+' '+t('Current state: {0}.',t(state.state))+ (state.latestFinalization?t(' Latest retained version: {0}.',state.latestFinalization.version):''),false,'is-info');
      await syncDrawer(location.href);
    }else notice(uncertain(intent.kind)+' '+t('The current state is unavailable. Check again before trying another action.'),true);
  }
  async function openAction(form,opener) {
    if(dialog||writing)return;if(saved().pending){notice(uncertain(saved().pending.kind),true);return;}
    const kind=form.dataset.finalAction, before={...state}, content=ui.template('confirmation');
    content.querySelector('[data-confirm-title]').textContent=form.dataset.confirmTitle;
    const description=content.querySelector('[data-confirm-description]');description.textContent=form.dataset.confirmDescription;
    const effects=el('ul','fr-effects');
    for(const key of kind==='publish'?['Archives the event in the same step: uploads and review close.','Tries a final Wise Old Man refresh first. If it’s skipped or fails, the results still publish and you’ll be told.','Participants are notified that official results are available.']:['The event becomes current again. Review stays available for corrections.','The official version stays in history. Publishing again creates a new version.','Doesn’t restart the event or reopen uploads.'])effects.append(el('li',null,t(key)));
    description.after(effects);
    if(kind==='publish'){
      const top=el('ol','fr-top');
      // RC08 F4: places are already computed on the full server standings, before slicing.
      const rows=[...root.querySelectorAll('.fr-standings .rows .row')];for(const row of rows.slice(0,3))top.append(el('li',null,row.querySelector('.place').textContent+' '+row.querySelector('.cell-main').textContent));
      if(rows.length>3)top.append(el('li','choice-sub',t('+ {0} more teams, as in the standings.',rows.length-3)));description.after(top);
      if(['Pending','Rejected'].includes(before.womEndUpdateStatus))effects.append(el('li',null,t('Publishing now makes the last fetch before the end the official WOM data.')));
    }
    const message=el('div','banner is-error');message.hidden=true;message.setAttribute('role','alert');message.tabIndex=-1;effects.after(message);
    let reason=null;if(kind==='reopen'){
      const field=el('div','field m-extra'),label=el('label','lbl',t('Reason'));label.htmlFor='fr-reason';reason=el('textarea','textarea');reason.id='fr-reason';reason.maxLength=2000;reason.value=saved().reason||'';reason.setAttribute('aria-describedby','fr-reason-hint');
      const hint=el('div','field-hint',t('Use 2,000 characters or fewer.'));hint.id='fr-reason-hint';field.append(label,reason,hint);content.querySelector('.m-actions').before(field);
      on(reason,'input',()=>{save({reason:reason.value});message.hidden=true;});
    }
    const cancel=content.querySelector('[data-confirm-cancel]'),accept=content.querySelector('[data-confirm-accept]');cancel.textContent=t('Cancel');accept.className='btn '+(kind==='reopen'?'btn-danger':'btn-primary');accept.querySelector('[data-component-text]').textContent=t(kind==='reopen'?'Reopen results':'Publish and archive');
    const layer=ui.openLayer({title:form.dataset.confirmTitle,content,confirmation:!reason,pending:()=>writing,opener,confirmLeave:async()=>{const discard=await ui.confirmDiscard();if(discard)save({reason:''});return discard;},onClose:()=>{dialog=null;}});dialog=layer;layer.element.dataset.pageFamily='final-review';
    on(cancel,'click',()=>ui.closeLayer());
    on(accept,'click',async()=>{
      if(writing)return;if(reason&&!reason.value.trim()){message.textContent=t('Enter a reason.');message.hidden=false;reason.focus();return;}
      writing=true;ui.refreshDirty();cancel.disabled=true;accept.disabled=true;accept.classList.add('is-busy');accept.querySelector('.spin').hidden=false;layer.element.setAttribute('aria-busy','true');
      const body=new URLSearchParams(new FormData(form));body.set('ConfirmLifecycleAction','true');if(reason)body.set('Reason',reason.value.trim());
      const intent={kind,version:before.version,cycle:before.reviewCycleId};save({pending:intent});
      const result=await post(kind==='publish'?'Finalize':'Unfinalize',body,reason?{[t('Reason')]:reason.value}:{});
      writing=false;ui.refreshDirty();if(life.signal.aborted)return;cancel.disabled=false;accept.disabled=false;accept.classList.remove('is-busy');accept.querySelector('.spin').hidden=true;layer.element.removeAttribute('aria-busy');
      if(result.kind==='handler'&&typeof result.data?.succeeded==='boolean'&&['applied','queued','skipped','failed','refused'].includes(result.data?.outcome)){
        save({pending:null});if(result.data.succeeded){save({reason:''});layer.markClean();await layer.close();await refresh();
          const refreshState=result.data.current?.finalRefresh;const note=kind==='publish'?(refreshState?.status===0?t('Final WOM refresh succeeded.'):root.querySelector('.fr-wom-note')?.textContent.trim()||''):'';notice(t(kind==='publish'?'Official results published. The event is archived.':'Results reopened. The official version stays in history.')+(note?' '+note:''),false,refreshState&&refreshState.status!==0?'is-warning':'is-info');return;}
        message.textContent=result.data.error||t('Nothing was saved. Your entries are still here.');message.hidden=false;message.focus();
        const fresh=await current();if(fresh.kind==='handler'&&fresh.data?.eventId===eventId&&fresh.data.version!==before.version){
          const reopened=kind==='reopen'&&fresh.data.reviewCycleId!==before.reviewCycleId&&fresh.data.state==='AwaitingFinalReview'&&fresh.data.latestFinalization?.unfinalizedAt;
          layer.markClean();await layer.close();await refresh();notice(reopened?t('These results were already reopened.'):t('The event changed while this was open. Check the current details before trying again.'),false,'is-info');}
        return;
      }
      if(result.kind==='session-lost'||result.kind==='refused'){
        save({pending:null});message.textContent=t('Nothing was saved. Your entries are still here.')+(result.reason?' '+result.reason:'');message.hidden=false;message.focus();return;
      }
      layer.markClean();await layer.close();notice(uncertain(kind)+' '+t('Check the current state before trying another action.'),true);
    });
  }
  const schema={version:{valid:value=>/^[1-9][0-9]*$/.test(value)&&Number.isSafeInteger(Number(value)),default:null}};
  async function syncDrawer(next,record=false) {
    const parsed=new URL(next,base),raw=parsed.searchParams.get('version'),version=raw&&schema.version.valid(raw)?raw:null;
    if(raw&&!version)ui.setUrl({version:null},schema);
    if(drawer?.version===version)return;
    if(drawer){closingFromUrl=true;await drawer.layer.close();closingFromUrl=false;drawer=null;}
    if(!version)return;
    if(record)ui.setUrl({version},schema,{record:true});
    const source=root.querySelector(`template[data-final-history="${version}"]`),content=document.createDocumentFragment();
    const head=el('div','dr-head'),title=el('h2','dr-title',t('Version {0}',version)),close=el('button','btn btn-sm',t('Close'));head.append(title,close);content.append(head);
    const body=el('div','dr-body');body.append(source?source.content.cloneNode(true):el('div','empty',t('This version isn’t available')));content.append(body);
    const layer=ui.openLayer({kind:'drawer',title:t('Version {0}',version),content,onClose:()=>{drawer=null;if(!closingFromUrl)ui.setUrl({version:null},schema);}});layer.element.dataset.pageFamily='final-review';drawer={version,layer};on(close,'click',()=>ui.closeLayer());
  }
  on(root,'click',event=>{const toggle=event.target.closest('#how-btn');if(toggle){const expanded=toggle.getAttribute('aria-expanded')!=='true';toggle.setAttribute('aria-expanded',String(expanded));root.querySelector('#how-body').hidden=!expanded;}
    const link=event.target.closest('[data-final-version]');if(link){event.preventDefault();void syncDrawer(link.href,true);}if(event.target.closest('[data-final-readback]'))void check();});
  on(root,'submit',event=>{const form=event.target.closest('[data-final-action]');if(form){event.preventDefault();void openAction(form,event.submitter);}});
  const unregister=ui.registerDraft(region,{isDirty:()=>false,isPending:()=>writing});
  const unregisterUrl=ui.registerUrlState(async next=>{if(new URL(next).pathname!==base.pathname)return false;await syncDrawer(next);return true;});
  void syncDrawer(location.href);if(saved().pending)notice(uncertain(saved().pending.kind)+' '+t('Check the current state before trying another action.'),true);
  release=()=>{life.abort();unregister();unregisterUrl();writing=false;closingFromUrl=true;if(dialog)void dialog.close();if(drawer)void drawer.layer.close();};
}

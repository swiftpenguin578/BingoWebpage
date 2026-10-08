// E03/AU03: one durable request key per open, authoritative readback after unknown writes.
let cleanup;
export function disposeCreate() { cleanup?.(); cleanup = null; }
export function initCreate(region, ui) {
  disposeCreate();
  const source = region.querySelector('[data-event-create-template]'); if (!source) return;
  const labels = JSON.parse(source.dataset.createLabels), names = JSON.parse(source.dataset.createNames);
  const t = key => labels[key] || key, life = new AbortController();
  let modal, unregister, status = 'idle', requestId, leaving = false, fieldError = '', validatedName = '', openedFromDirectory = false;
  const schema = values => Object.fromEntries(Object.keys(values).map(key => [key, { valid: () => true, default: '' }]));
  const url = create => { const values = Object.fromEntries(new URL(location.href).searchParams); if (create) values.create = '1'; else delete values.create; ui.setUrl(values, schema(values), {record: create}); };
  const dirty = () => !!modal && (modal.element.querySelector('#cm-name').value !== '' || modal.element.querySelector('#cm-tz').value !== 'Europe/Copenhagen' || status === 'uncertain');
  const pending = () => status === 'saving' || status === 'checking';
  function paint() {
    if (!modal) return;
    const panel = modal.element, name = panel.querySelector('#cm-name'), tz = panel.querySelector('#cm-tz');
    const locked = pending() || status === 'uncertain'; name.disabled = tz.disabled = locked;
    panel.setAttribute('aria-busy', String(pending()));
    const submit = panel.querySelector('#cm-submit'); submit.disabled = pending(); submit.classList.toggle('is-busy', pending());
    submit.querySelector('.spin').hidden = !pending();
    const label = submit.querySelector('[data-component-text]'), nextLabel = t(status === 'saving' ? 'Creating…' : status === 'checking' ? 'Checking…' : status === 'uncertain' ? 'Check again' : 'Create event');
    if (label.textContent !== nextLabel) label.textContent = nextLabel;
    panel.querySelector('#cm-cancel').disabled = pending();
    const count = panel.querySelector('[data-create-count]'), length = Array.from(name.value).length; count.hidden = length <= 40; count.textContent = length+' / 50';count.classList.toggle('is-over',length>50);
    const error = panel.querySelector('#cm-name-err'); error.hidden = !fieldError; error.querySelector('[data-component-text]').textContent = fieldError;
    name.classList.toggle('is-invalid',!!fieldError); name.setAttribute('aria-invalid',String(!!fieldError));
    const dupe = !locked && names.find(item => item.name.toLocaleLowerCase(document.documentElement.lang) === name.value.trim().toLocaleLowerCase(document.documentElement.lang));
    const note = panel.querySelector('#cm-name-dup');note.hidden=!dupe;
    if(dupe)note.querySelector('[data-component-text]').textContent=t('An event called “{0}” already exists ({1}). You can still create another; it gets its own link.').replace(/\{([01])\}/g,(_match,key)=>key==='0'?dupe.name:dupe.phase);
    const described = [fieldError?'cm-name-err':'',dupe?'cm-name-dup':''].filter(Boolean).join(' '); if(described)name.setAttribute('aria-describedby',described);else name.removeAttribute('aria-describedby');
    const notices = [['cm-failure','failed','Couldn’t create the event. Nothing was saved, and your details are still here.'],['cm-uncertain','uncertain','We couldn’t confirm whether the event was created. Check before trying again, so the event isn’t created twice.'],['cm-not-found','not-found',"We couldn't find it. It may not have been created, or it was removed since. You can create it again with these details."]];
    for(const[id,state,message]of notices){const node=panel.querySelector('#'+id);node.hidden=status!==state && !(id==='cm-uncertain'&&status==='checking');node.querySelector('[data-component-text]').textContent=t(message);}
    ui.refreshDirty();
  }
  async function askDiscard() {
    if (status !== 'uncertain') return ui.confirm({title:document.body.dataset.discardTitle,description:t('The name and timezone you entered haven’t been saved.'),actionLabel:document.body.dataset.discard,cancelLabel:document.body.dataset.keepEditing,actionClass:'btn-danger'});
    const leave = await ui.confirm({title:t('Leave before checking?'),description:t('The event may already have been created. Check again before creating another event. Leaving closes this dialog; it does not undo a creation.'),actionLabel:t('Leave anyway'),cancelLabel:t('Check again'),actionClass:'btn-danger'});
    if (!leave) void submit();
    return leave;
  }
  async function cancel() { if(pending()||!modal)return;if(dirty()&&!await askDiscard())return;await modal.close(false); }
  async function success(id) {
    if(!id||!/^[0-9a-f-]{36}$/i.test(id)) {status='uncertain';paint();return;}
    const values = new URL(location.href);values.searchParams.delete('create');
    sessionStorage.setItem('admin-event-created',JSON.stringify({id,url:values.pathname+values.search}));
    leaving=true;status='idle';unregister?.();unregister=null;await modal.close(true);
    // Overview still uses the accepted native layout until U4; replace, never push/resubmit.
    location.replace('/Admin/Events/Manage/'+id);
  }
  async function submit() {
    if(pending()||!modal)return;
    const panel=modal.element,name=panel.querySelector('#cm-name'),timezone=panel.querySelector('#cm-tz');
    const checking=status==='uncertain';
    if(!checking){validatedName=name.value;fieldError=!name.value.trim()?t('Enter an event name.'):Array.from(name.value.trim()).length>50?t('Event names must be 50 characters or fewer.'):'';if(fieldError){paint();name.focus();return;}}
    const draft={[t('Event name')]:name.value,[t('Timezone')]:timezone.value};
    status=checking?'checking':'saving';paint();
    const body=new URLSearchParams({'Input.RequestId':requestId,'Input.Name':name.value,'Input.Timezone':timezone.value});
    const response=await ui.busy(()=>window.AdminFetch.request(checking?'/Admin/Events/Create?handler=CheckAgain&requestId='+requestId:'/Admin/Events/Create',{
      method:checking?'GET':'POST',...(checking?{}:{body}),signal:life.signal,draft,readback:checking
    }));
    if(!modal)return;
    if(response.kind==='handler'){
      if(response.data.eventId)return success(response.data.eventId);
      if(response.data.outcome==='invalid'){
        status='idle';fieldError=response.data.errors?.['Input.Name']?.join(' ')||'';
        const tzError=response.data.errors?.['Input.Timezone']?.join(' ')||'';const tzNode=panel.querySelector('#cm-tz-err');tzNode.hidden=!tzError;tzNode.textContent=tzError;
        paint();if(fieldError)name.focus();else if(tzError)timezone.focus();else {status='failed';paint();}return;
      }
    }
    if(response.status===404){status='not-found';requestId=crypto.randomUUID();paint();return;}
    // Session notice keeps the unsent details; readback remains available after sign-in.
    status='uncertain';paint();
  }
  function open(record=false,opener=document.activeElement,fromDirectory=record) {
    if(modal)return;
    leaving=false;openedFromDirectory=fromDirectory;status='idle';fieldError='';requestId=crypto.randomUUID();
    const content=source.content.cloneNode(true);content.querySelector('#Input_RequestId').value=requestId;
    modal=ui.openLayer({title:t('Create event'),content,pending,dirty,confirmLeave:askDiscard,opener,onClose:async(_result,{navigating}={})=>{
      modal=null;unregister?.();unregister=null;ui.refreshDirty();
      if(!leaving&&!navigating){if(openedFromDirectory)await ui.backUrl();else url(false);}
    }});modal.element.classList.add('modal-form');
    unregister=ui.registerDraft(source,{isDirty:dirty,isPending:pending,confirmLeave:askDiscard,discard:()=>{modal?.element.querySelector('#cm-name')&&(modal.element.querySelector('#cm-name').value='');if(modal)modal.element.querySelector('#cm-tz').value='Europe/Copenhagen';status='idle';}});
    modal.element.addEventListener('input',event=>{if(event.target.id==='cm-name'&&event.target.value!==validatedName)fieldError='';paint();},{signal:life.signal});
    // Name input already paints on input. Repainting its blur/change replaces
    // button text during pointer-down and can cancel native WebKit activation.
    modal.element.addEventListener('change',event=>{if(event.target.id==='cm-tz')paint();},{signal:life.signal});
    modal.element.querySelector('#cm-cancel').addEventListener('click',()=>void cancel(),{signal:life.signal});modal.element.querySelector('#cm-submit').addEventListener('click',()=>void submit(),{signal:life.signal});
    modal.element.addEventListener('keydown',event=>{if(event.key==='Escape'){event.preventDefault();event.stopPropagation();void cancel();}else if(event.key==='Enter'&&event.target.id==='cm-name'){event.preventDefault();void submit();}},{signal:life.signal});
    if(record)url(true);paint();
  }
  region.addEventListener('click',event=>{const control=event.target.closest('[data-create-event]');if(control&&event.button===0&&!event.metaKey&&!event.ctrlKey&&!event.shiftKey&&!event.altKey){event.preventDefault();open(true,control);}},{signal:life.signal});
  const unregisterUrl=ui.registerUrlState(async(next,previous)=>{
    const a=new URL(next),b=new URL(previous);a.searchParams.delete('create');b.searchParams.delete('create');
    if(a.href!==b.href)return false;
    if(new URL(next).searchParams.get('create')==='1')open(false,region.querySelector('[data-create-event]'),true);
    else if(modal){leaving=true;await modal.close(false);}
    return true;
  });
  if(new URL(location.href).searchParams.get('create')==='1')open();
  cleanup=()=>{unregisterUrl();life.abort();leaving=true;unregister?.();unregister=null; if(modal){status='idle';void modal.close(false);modal=null;}ui.refreshDirty();};
}

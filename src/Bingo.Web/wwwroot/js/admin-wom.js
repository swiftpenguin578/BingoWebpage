// U9 2a workspace: shared layers retain native POST compatibility until item 2b.
let release=()=>{};
export function dispose(){release();release=()=>{};}
export function init(region,ui=window.AdminUI){
 dispose();const life=new AbortController(),root=region.querySelector('[data-wom]');if(!root)return;
 root.addEventListener('click',event=>{const button=event.target.closest('#tech-btn,#miss-btn');if(button){const expanded=button.getAttribute('aria-expanded')!=='true';button.setAttribute('aria-expanded',String(expanded));root.querySelector('#'+button.getAttribute('aria-controls')).hidden=!expanded;}},{signal:life.signal});
 root.addEventListener('submit',event=>{const form=event.target.closest('[data-wom-action]');if(!form||form.dataset.womAction==='fetch')return;event.preventDefault();
  const content=ui.template('confirmation');content.querySelector('[data-confirm-title]').textContent=form.dataset.title;content.querySelector('[data-confirm-description]').textContent=form.dataset.description;
  const fields=form.querySelector('template[data-wom-fields]');if(fields)content.querySelector('.m-actions').before(fields.content.cloneNode(true));
  const cancel=content.querySelector('[data-confirm-cancel]'),accept=content.querySelector('[data-confirm-accept]');accept.querySelector('[data-component-text]').textContent=form.dataset.action;
  const layer=ui.openLayer({title:form.dataset.title,content,confirmation:!fields});layer.element.dataset.pageFamily='wom';cancel.addEventListener('click',()=>ui.closeLayer());accept.addEventListener('click',async()=>{for(const field of layer.element.querySelectorAll('input,textarea')){const copy=document.createElement('input');copy.type='hidden';copy.name=field.name;copy.value=field.value;form.append(copy);}layer.markClean();await layer.close();HTMLFormElement.prototype.submit.call(form);});
 },{signal:life.signal});release=()=>life.abort();
}

// U9 1a: Final Review workspace. Lifecycle dialogs are bound in item 1b.
let release = () => {};
export function dispose() { release(); release = () => {}; }
export function init(region, ui = window.AdminUI) {
  dispose();
  const life = new AbortController();
  const root = region.querySelector('[data-final-review]');
  if (!root) return;
  root.querySelector('#how-btn')?.addEventListener('click', event => {
    const button = event.currentTarget;
    const expanded = button.getAttribute('aria-expanded') !== 'true';
    button.setAttribute('aria-expanded', String(expanded));
    root.querySelector('#how-body').hidden = !expanded;
  }, { signal: life.signal });
  // A10: the workspace uses shared layers; item 1b replaces the native transport.
  root.querySelectorAll('form[data-final-action]').forEach(form => form.addEventListener('submit', event => {
    event.preventDefault();
    const content = ui.template('confirmation');
    content.querySelector('[data-confirm-title]').textContent = form.dataset.confirmTitle;
    content.querySelector('[data-confirm-description]').textContent = form.dataset.confirmDescription;
    const cancel = content.querySelector('[data-confirm-cancel]'), accept = content.querySelector('[data-confirm-accept]');
    cancel.textContent = form.dataset.cancelLabel;
    accept.querySelector('[data-component-text]').textContent = form.dataset.confirmAction;
    let reason;
    if (form.dataset.confirmRequireReason === 'true') {
      const field = document.createElement('div'); field.className = 'field m-extra';
      const label = document.createElement('label'); label.className = 'lbl'; label.htmlFor = 'fr-reason'; label.textContent = form.dataset.reasonLabel;
      reason = document.createElement('textarea'); reason.id = 'fr-reason'; reason.className = 'textarea'; reason.maxLength = 2000;
      field.append(label, reason); content.querySelector('.m-actions').before(field);
    }
    const layer = ui.openLayer({title: form.dataset.confirmTitle, content, confirmation: !reason});
    cancel.addEventListener('click', () => ui.closeLayer());
    accept.addEventListener('click', async () => {
      if (reason && !reason.value.trim()) { reason.focus(); return; }
      if (reason) form.elements.Reason.value = reason.value;
      form.elements.ConfirmLifecycleAction.value = 'true';
      layer.markClean(); await layer.close(); HTMLFormElement.prototype.submit.call(form);
    });
  }, {signal: life.signal}));
  release = () => life.abort();
}

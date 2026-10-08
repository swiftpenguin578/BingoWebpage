// U9 / RC09: cached readback, event-scoped drafts/intents, no provider calls on GET.
let release = () => {};
export function dispose() { release(); release = () => {}; }
export function init(region, ui = window.AdminUI) {
  dispose();
  const life = new AbortController(), root = region.querySelector('[data-wom]');
  if (!root) return;
  const on = (node, event, callback) => node?.addEventListener(event, callback, {signal: life.signal});
  const labels = JSON.parse(root.dataset.labels), t = (key, ...args) => (labels[key] || key).replace(/\{(\d+)\}/g, (_, i) => String(args[+i] ?? ''));
  const base = new URL(location.href); base.search = ''; base.hash = '';
  const eventId = root.dataset.eventId, storageKey = 'u9-wom:' + eventId;
  let state = JSON.parse(root.dataset.current), dialog = null, writing = false;
  const saved = () => { try { return JSON.parse(sessionStorage.getItem(storageKey) || '{}'); } catch { return {}; } };
  const save = patch => { try { sessionStorage.setItem(storageKey, JSON.stringify({...saved(), ...patch})); } catch { /* Keep the live layer when storage is unavailable. */ } };
  const el = (tag, cls, text) => { const node = document.createElement(tag); if (cls) node.className = cls; if (text !== undefined) node.textContent = text; return node; };
  const url = handler => { const value = new URL(base); value.searchParams.set('handler', handler); return value.href; };
  const post = (handler, body, draft) => ui.busy(() => window.AdminFetch.request(url(handler), {method: 'POST', body, draft, signal: life.signal}));
  const fmt = value => value ? new Intl.DateTimeFormat(document.documentElement.lang === 'da' ? 'da-DK' : 'en-GB', {timeZone: root.dataset.timezone || 'UTC', dateStyle: 'medium', timeStyle: 'short'}).format(new Date(value)) : t('None yet');
  const pendingCaption = () => { if ((saved().pending && saved().pending.key !== 'fetch') || (saved().lastUnknown && saved().lastUnknown !== 'fetch')) { const title = root.querySelector('#sync-state'); if (title) title.textContent = t(saved().pending?.knownQueued && saved().pending?.status !== 'Unknown' ? 'Update queued' : 'Update unconfirmed'); } };
  async function refresh() {
    await ui.update(location.href, {root: region.querySelector('.page'), results: root,
      patch: doc => {
        const next = doc.querySelector('[data-wom]'); if (!next) throw Error('Missing WOM workspace');
        const head = doc.querySelector('[data-page-region] .page-head');
        region.querySelector('.page-head').replaceChildren(...[...head.childNodes].map(node => document.importNode(node, true)));
        root.replaceChildren(...[...next.childNodes].map(node => document.importNode(node, true))); Object.assign(root.dataset, next.dataset);
        state = JSON.parse(root.dataset.current); ui.refreshContext(doc); pendingCaption(); return location.href;
      }, pending: () => document.querySelector('[data-page-loading-template="wom"]').content.cloneNode(true),
      failed: () => ui.template('load-failure'), fallbackFocus: () => root.querySelector('#comp-title,#connect-title') || region.querySelector('.h1')});
  }
  function notice(message, check = false, tone = 'is-warning', lock = check) {
    const node = root.querySelector('[data-wom-notice]'); if (!node) return;
    node.className = 'banner page-banner ' + tone; node.hidden = false; node.replaceChildren(el('span', 'grow', message));
    if (check) { const button = el('button', 'btn btn-sm', t('Check current state')); button.type = 'button'; button.dataset.womReadback = ''; node.append(button); }
    if (lock) root.querySelectorAll('[data-wom-action] button').forEach(button => { button.disabled = true; });
    pendingCaption(); node.focus({preventScroll: false});
  }
  function busy(value) {
    writing = value; ui.refreshDirty();
    if (dialog) {
      dialog.layer.element.setAttribute('aria-busy', String(value)); dialog.cancel.disabled = value; dialog.accept.disabled = value;
      dialog.accept.classList.toggle('is-busy', value); dialog.accept.querySelector('.spin').hidden = !value;
      dialog.fields.forEach(field => { field.disabled = value; });
    }
    const fetch = root.querySelector('#fetch-btn'); if (fetch) { fetch.disabled = value || !state.integration?.canRefresh; fetch.classList.toggle('is-busy', value); fetch.querySelector('.spin').hidden = !value; fetch.querySelector('svg').style.display = value ? 'none' : ''; fetch.querySelector('[data-component-text]').textContent = t(value ? 'Fetching…' : 'Fetch now'); }
  }
  const uncertain = key => t(key === 'fetch' ? 'We couldn’t confirm whether new data was fetched.' : 'We couldn’t confirm whether this change was saved.');
  const statusText = status => t(({NotManaged:'Not configured',Active:'Active',Pending:'Queued',Sending:'Sending',Unknown:'Unknown outcome',Failed:'Failed',Conflict:'Conflict',Deleted:'Deleted',Cancelled:'Cancelled'})[status] || 'Not configured');
  async function check() {
    if (writing) return; const intent = saved().pending; busy(true);
    const result = await ui.busy(() => window.AdminFetch.request(url('Current'), {cache: 'no-store', readback: true, signal: life.signal}), true);
    busy(false); if (life.signal.aborted) return;
    if (result.kind !== 'handler' || result.data?.eventId !== eventId) { notice(t('The current state is unavailable. Check again before trying another action.'), true); return; }
    state = result.data; save({pending: null}); await refresh();
    const connection = state.integration?.competitionId ? '#' + state.integration.competitionId : t('Not connected');
    const message = (intent && !intent.knownQueued ? uncertain(intent.key) + ' ' : '') + t('Current stored connection: {0}. Last successful fetch on record: {1}. Operation status: {2}.', connection, fmt(state.integration?.lastSuccessfulAt), statusText(state.management?.status));
    const unresolved = ['Pending','Sending','Retry','Unknown'].includes(state.management?.status);
    notice(message, unresolved, 'is-info', false);
  }
  async function run(form, fields = [], layer = null) {
    if (writing) return;
    const key = form.dataset.womAction, body = new URLSearchParams(new FormData(form));
    for (const field of fields) body.set(field.name, field.value);
    const id = fields.find(field => field.name === 'CompetitionId'), code = fields.find(field => field.name === 'CompetitionVerificationCode');
    let error = id && !/^[1-9][0-9]*$/.test(id.value.trim()) ? t('Enter a valid competition ID.') : code && !code.value.trim() ? t('Enter a valid Wise Old Man management code.') : '';
    if (error) { dialog.message.textContent = error; dialog.message.hidden = false; (id || code).focus(); return; }
    const draft = {[t('Action')]: form.dataset.title || t('Fetch now')};
    for (const field of fields.filter(field => field !== code)) draft[field.labels?.[0]?.textContent || field.name] = field.value;
    // RC09 W4: clear the submitted secret before awaiting any response, including a lost one.
    if (code) { code.value = ''; save({CompetitionVerificationCode: ''}); draft[t('Management code')] = t('The submitted code was cleared. Enter it again if needed.'); layer.markClean(); }
    const intent = {key, version: state.version}; save({pending: intent}); busy(true);
    const result = await post(new URL(form.action).searchParams.get('handler'), body, draft);
    busy(false); if (life.signal.aborted) return;
    if (result.kind === 'handler' && typeof result.data?.succeeded === 'boolean') {
      const outcome = result.data; save({pending: null});
      if (outcome.succeeded || outcome.outcome === 'skipped' || outcome.outcome === 'queued') {
        if (outcome.succeeded) {
          if (key === 'link') save({CompetitionId: '', lastUnknown: null});
          if (key === 'disconnect') save({CompetitionClearReason: '', CompetitionVerificationCode: '', lastUnknown: null});
          if (saved().lastUnknown === key) save({lastUnknown: null});
        }
        if (layer) { layer.markClean(); await layer.close(); }
        const queued = outcome.outcome === 'queued'; if (queued) save({pending: {...intent, knownQueued: true, status: outcome.status}});
        await refresh(); const retry = outcome.retryAt ? ' ' + t('Next eligible time: {0}', fmt(outcome.retryAt)) : '';
        notice((queued ? (outcome.status === 'Unknown' ? uncertain(key) : t('The operation is queued. Check the current state for its result.')) + ' ' : '') + (outcome.message || t('The current connection is shown.')) + retry, queued, outcome.succeeded ? 'is-info' : 'is-warning', false); return;
      }
      error = outcome.error || outcome.message || t('Nothing was saved. Your entries are still here.');
    } else if (result.kind === 'session-lost' || result.kind === 'refused') {
      save({pending: null}); error = t('Nothing was saved. Your entries are still here.') + (result.reason ? ' ' + result.reason : '');
    } else {
      save({lastUnknown: key}); if (layer) { layer.markClean(); await layer.close(); }
      notice(uncertain(key) + ' ' + t('Check the current state before trying another action.'), true); return;
    }
    if (dialog) { dialog.message.textContent = error; dialog.message.hidden = false; dialog.message.focus(); }
    else { await refresh(); notice(error, false, 'is-error', false); }
  }
  function open(form, opener) {
    if (writing || dialog) return;
    if (saved().pending && !saved().pending.knownQueued) { notice(uncertain(saved().pending.key), true); return; }
    if (form.dataset.womAction === 'fetch') { void run(form); return; }
    const content = ui.template('confirmation'); content.querySelector('[data-confirm-title]').textContent = form.dataset.title;
    content.querySelector('[data-confirm-description]').textContent = form.dataset.description;
    const source = form.querySelector('template[data-wom-fields]'); if (source) content.querySelector('.m-actions').before(source.content.cloneNode(true));
    if (form.dataset.womAction === 'create') content.querySelector('.m-actions').before(root.querySelector('[data-create-checks]').cloneNode(true));
    const fields = [...content.querySelectorAll('input,textarea')]; for (const field of fields) field.value = saved()[field.name] || '';
    const message = el('div', 'banner is-error'); message.hidden = true; message.setAttribute('role', 'alert'); message.tabIndex = -1; content.querySelector('.m-actions').before(message);
    const cancel = content.querySelector('[data-confirm-cancel]'), accept = content.querySelector('[data-confirm-accept]'); cancel.textContent = t('Cancel'); accept.querySelector('[data-component-text]').textContent = form.dataset.action;
    if (form.dataset.womAction === 'delete') accept.className = 'btn btn-danger';
    const layer = ui.openLayer({title: form.dataset.title, content, confirmation: !fields.length, pending: () => writing, opener,
      confirmLeave: async () => { const discard = await ui.confirmDiscard(); if (discard) save(Object.fromEntries(fields.map(field => [field.name, '']))); return discard; }, onClose: () => { dialog = null; }});
    layer.element.dataset.pageFamily = 'wom'; dialog = {layer, fields, cancel, accept, message};
    fields.forEach(field => on(field, 'input', () => { save({[field.name]: field.value}); message.hidden = true; }));
    on(cancel, 'click', () => ui.closeLayer()); on(accept, 'click', () => void run(form, fields, layer));
    on(layer.element, 'keydown', event => { if (event.key === 'Enter' && event.target.matches('input')) { event.preventDefault(); void run(form, fields, layer); } });
  }
  on(root, 'click', event => {
    const button = event.target.closest('#tech-btn,#miss-btn'); if (button) { const expanded = button.getAttribute('aria-expanded') !== 'true'; button.setAttribute('aria-expanded', String(expanded)); root.querySelector('#' + button.getAttribute('aria-controls')).hidden = !expanded; }
    if (event.target.closest('[data-wom-readback]')) void check();
  });
  on(root, 'submit', event => { const form = event.target.closest('[data-wom-action]'); if (form) { event.preventDefault(); open(form, event.submitter); } });
  const unregister = ui.registerDraft(region, {isDirty: () => false, isPending: () => writing});
  if (saved().pending) notice(saved().pending.knownQueued && saved().pending.status !== 'Unknown' ? t('The operation is queued. Check the current state for its result.') : uncertain(saved().pending.key) + ' ' + t('Check the current state before trying another action.'), true, 'is-warning', !saved().pending.knownQueued);
  else pendingCaption();
  release = () => { life.abort(); unregister(); writing = false; if (dialog) void dialog.layer.close(); };
}

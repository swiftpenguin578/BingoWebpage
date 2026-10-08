// Participants (U5, brief 87). Server-owned directory state through the shared in-page
// update helper; row menus, confirmations (transient layers, never routes), the
// participant drawer (?participant=) and Add (?add=1). The shell owns transport
// classification (AdminFetch, C-CMP-2), busy timing (AdminUI.busy), layers, history and
// dirty guards; this module never retries a write. A lost response re-reads the
// current state and says so; only a definite refusal is shown as a refusal (rule 13).
let release;
export function dispose() { release?.(); release = null; }
export function init(region, ui = window.AdminUI) {
  dispose();
  const root = region.querySelector('[data-participants]');
  if (!root) return;
  const labels = JSON.parse(root.dataset.labels || '{}');
  const t = (key, ...args) => (labels[key] ?? key).replace(/\{(\d)\}/g, (_match, i) => args[i] ?? '');
  const life = new AbortController(), signal = life.signal;
  const listen = (node, type, fn, options = {}) => node?.addEventListener(type, fn, { signal, ...options });
  const eventId = root.dataset.eventId;
  const results = root.querySelector('[data-participants-results]');
  const search = root.querySelector('[data-participants-search]');
  const menuPanel = root.querySelector('[data-participant-menu-panel]');
  let query = new URL(root.dataset.directoryCanonical, location.href);
  let timer, edits = 0;
  const schema = values => Object.fromEntries(Object.keys(values).map(key => [key, { valid: () => true, default: '' }]));
  const setUrl = (url, record = false) => { const values = Object.fromEntries(new URL(url, location.href).searchParams); ui.setUrl(values, schema(values), { record }); };
  const importChildren = (target, source) => { if (target && source) target.replaceChildren(...document.importNode(source, true).childNodes); };
  const actionUrl = handler => `/Admin/Events/Participants/${eventId}?handler=${handler}`;
  const post = (handler, body, draft) => ui.busy(() => window.AdminFetch.request(actionUrl(handler), { method: 'POST', body, expect: 'json', draft, signal }));
  const state = () => ({
    version: root.dataset.eventVersion, capacity: Number(root.dataset.capacity || 0), confirmed: Number(root.dataset.confirmed || 0),
    waiting: Number(root.dataset.waiting || 0), firstWaiter: root.dataset.firstWaiter || '', editable: root.dataset.editable === 'true',
    privateEditable: root.dataset.privateEditable === 'true', lockReason: root.dataset.lockReason || '', eventName: root.dataset.eventName || ''
  });
  const rowOf = id => root.querySelector(`[data-participant-row="${CSS.escape(id)}"]`);
  const rowData = row => row ? { id: row.dataset.participantRow, name: row.dataset.name, status: row.dataset.status, version: row.dataset.version,
    paid: row.dataset.paid === 'true', team: row.dataset.team || '', position: Number(row.dataset.position || 0) } : null;

  /* ---------------- directory ---------------- */
  function patchDirectory(doc) {
    const fresh = doc.querySelector('[data-participants]');
    if (!fresh) throw new Error('Missing participants directory');
    importChildren(results, fresh.querySelector('[data-participants-results]'));
    importChildren(root.querySelector('[data-participants-footer]'), fresh.querySelector('[data-participants-footer]'));
    importChildren(root.querySelector('[data-participants-columns]'), fresh.querySelector('[data-participants-columns]'));
    importChildren(root.querySelector('[data-participants-banners]'), fresh.querySelector('[data-participants-banners]'));
    for (const count of fresh.querySelectorAll('[data-tab-count]')) { const own = root.querySelector(`[data-tab-count="${count.dataset.tabCount}"]`); if (own) own.textContent = count.textContent; }
    const summary = region.querySelector('.page-head .summary'), nextSummary = doc.querySelector('.page-head .summary');
    if (summary && nextSummary) importChildren(summary, nextSummary);
    const actions = region.querySelector('[data-participants-head-actions]'), nextActions = doc.querySelector('[data-participants-head-actions]');
    if (actions && nextActions) importChildren(actions, nextActions);
    for (const key of ['directoryCanonical', 'eventVersion', 'capacity', 'confirmed', 'waiting', 'firstWaiter', 'editable', 'privateEditable', 'lockReason', 'playingSlots']) root.dataset[key] = fresh.dataset[key] ?? '';
    query = new URL(fresh.dataset.directoryCanonical, location.href);
    paintQuery();
    markSelected();
    return directoryUrl();
  }
  function paintQuery() {
    const tab = query.searchParams.get('tab') || 'confirmed', pay = query.searchParams.get('pay') || 'any';
    for (const input of root.querySelectorAll('[data-participants-tab]')) { input.checked = input.value === tab; input.closest('.tab').classList.toggle('is-on', input.checked); }
    for (const input of root.querySelectorAll('[data-participants-pay]')) { input.checked = input.value === pay; input.closest('.seg-opt').classList.toggle('is-on', input.checked); }
    search.closest('.search').classList.toggle('has-clear', search.value.length > 0);
    root.querySelector('[data-participants-clear]').hidden = !search.value.length;
  }
  const pendingRows = () => document.importNode(root.querySelector('template[data-participants-pending]').content, true);
  const failedRows = () => document.importNode(root.querySelector('template[data-participants-failed]').content, true);
  // The drawer's query value rides on the directory URL (A2).
  let drawerParam = null;
  function directoryUrl(extra = drawerParam) {
    const url = new URL(query.href); url.searchParams.delete('participant'); url.searchParams.delete('add');
    if (extra) url.searchParams.set(extra[0], extra[1]);
    return url.href;
  }
  function readDirectory(target, { record = false } = {}) {
    const url = new URL(target, location.href), revision = edits;
    url.searchParams.delete('participant'); url.searchParams.delete('add');
    query = new URL(url.href); paintQuery();
    if (record) setUrl(directoryUrl(), true);
    return ui.update(url.href, { root, results, patch: patchDirectory, pending: pendingRows, failed: failedRows,
      fallbackFocus: () => search, signal, scrollRegions: [root.querySelector('[data-participants-wrap]')],
      current: () => edits === revision, draft: () => ({ [t('Search participants')]: search.value }) });
  }
  const reread = () => readDirectory(query.href);
  function withQuery(change) { const url = new URL(query.href); change(url.searchParams); url.searchParams.delete('page'); return url.href; }
  function searchNow() {
    clearTimeout(timer);
    const value = search.value.trim().slice(0, 100);
    void readDirectory(withQuery(params => { if (value) params.set('q', value); else params.delete('q'); }));
  }
  listen(search, 'input', () => { edits++; ui.supersedeUpdate(); clearTimeout(timer); paintQuery(); timer = setTimeout(searchNow, 250); });
  listen(search, 'keydown', event => {
    if (event.key === 'Enter') { event.preventDefault(); searchNow(); }
    else if (event.key === 'Escape' && search.value) { event.preventDefault(); search.value = ''; edits++; searchNow(); }
  });
  listen(root.querySelector('[data-participants-clear]'), 'click', () => { search.value = ''; edits++; search.focus({ preventScroll: true }); searchNow(); });
  listen(root, 'change', event => {
    const tab = event.target.closest('[data-participants-tab]'), pay = event.target.closest('[data-participants-pay]'), per = event.target.closest('[data-participants-per]');
    if (tab) void readDirectory(withQuery(params => { params.delete('sort'); params.delete('dir'); if (tab.value === 'confirmed') params.delete('tab'); else params.set('tab', tab.value); }));
    else if (pay) void readDirectory(withQuery(params => { if (pay.value === 'any') params.delete('pay'); else params.set('pay', pay.value); }));
    else if (per) void readDirectory(withQuery(params => { if (per.value === '25') params.delete('per'); else params.set('per', per.value); }));
  });
  listen(region, 'click', event => {
    if (event.target.closest('[data-page-skeleton]')) return;
    const control = event.target.closest('[data-participants-url]');
    if (control) {
      event.preventDefault();
      if (control.hasAttribute('data-participants-clear-all')) { search.value = ''; edits++; }
      void readDirectory(control.dataset.participantsUrl, { record: control.hasAttribute('data-participants-push') });
      return;
    }
    const menuButton = event.target.closest('[data-participant-menu]');
    if (menuButton) { fillMenu(menuButton.dataset.participantMenu); return; } // The shell opens it next.
    const item = event.target.closest('[data-menu-action]');
    if (item && menuPanel.contains(item)) { event.preventDefault(); runMenu(item); return; }
    const pay = event.target.closest('[data-participant-pay]');
    if (pay) { event.preventDefault(); void togglePay(rowData(pay.closest('[data-participant-row]')), pay); return; }
    const add = event.target.closest('[data-participant-add]');
    if (add) { event.preventDefault(); if (!add.disabled) void openAdd?.(add); return; }
    const link = event.target.closest('[data-participant-open]');
    if (link && (event.button !== 0 || event.metaKey || event.ctrlKey || event.shiftKey || event.altKey)) return;
    const row = event.target.closest('[data-participant-row]');
    if (link || row && !event.target.closest('a,button,input,label,select')) {
      event.preventDefault();
      const id = (link || row.querySelector('[data-participant-open]')).dataset.participantOpen;
      void openDrawer?.(id, { push: true, opener: rowOf(id)?.querySelector('[data-participant-open]') });
    }
  });
  const wrap = root.querySelector('[data-participants-wrap]');
  let overflowFrame;
  const overflow = new ResizeObserver(() => { cancelAnimationFrame(overflowFrame); overflowFrame = requestAnimationFrame(() => wrap.classList.toggle('is-scroll', wrap.scrollWidth > wrap.clientWidth + 1)); });
  overflow.observe(wrap);
  function markSelected() { for (const row of root.querySelectorAll('[data-participant-row]')) row.classList.toggle('is-selected', row.dataset.participantRow === drawer?.id); }
  function flash(id) {
    const row = rowOf(id); if (!row) return;
    row.classList.remove('is-flash'); void row.offsetWidth; row.classList.add('is-flash');
    row.addEventListener('animationend', () => row.classList.remove('is-flash'), { once: true });
  }

  /* ---------------- row menu ---------------- */
  let menuTarget = null;
  function fillMenu(id) {
    const data = rowData(rowOf(id)), current = state();
    menuTarget = data;
    if (!data) { menuPanel.replaceChildren(); return; }
    const locked = !current.editable, full = current.confirmed >= current.capacity, left = Math.max(0, current.capacity - current.confirmed);
    const items = [];
    const add = (action, label, { hint = '', disabled = false, title = '', danger = false } = {}) => items.push({ action, label, hint, disabled, title: title || (hint === t('Locked') ? current.lockReason : ''), danger });
    add('open', t('Open details'));
    add('pay', data.paid ? t('Mark as unpaid') : t('Mark as paid'), { disabled: !current.privateEditable });
    items.push('sep');
    if (data.status === 'waiting') {
      if (full) add('confirm-full', locked ? t('Confirm and add a place') : t('Confirm and add a place…'), { hint: locked ? t('Locked') : '', disabled: locked, title: locked ? '' : t('The event is full. Capacity increases by one and {0} gets the new place.', data.name) });
      else add('confirm', t('Confirm'), { hint: locked ? t('Locked') : t('{0} left', left), disabled: locked });
    }
    if (data.status === 'confirmed') {
      const nobody = current.waiting === 0, available = full && !nobody && !locked;
      add('to-waiting', available ? t('Move to waiting list…') : t('Move to waiting list'), {
        hint: locked ? t('Locked') : !full ? t('Places open') : nobody ? t('No one waiting') : '', disabled: !available,
        title: locked ? '' : !full ? t('Only available when the event is full. With places open, nobody is waiting for this place.') : nobody ? t('Nobody is on the waiting list to take this place.') : '' });
    }
    if (data.status !== 'withdrawn') {
      if (data.status === 'confirmed') items.push('sep');
      add('withdraw', t('Withdraw…'), { hint: locked ? t('Locked') : '', disabled: locked, danger: true });
    } else add('restore', t('Restore…'), { hint: locked ? t('Locked') : '', disabled: locked });
    menuPanel.replaceChildren(...items.map(item => {
      if (item === 'sep') { const sep = document.createElement('div'); sep.className = 'menu-sep'; sep.setAttribute('role', 'separator'); return sep; }
      const button = document.createElement('button'); button.type = 'button'; button.className = 'menu-item' + (item.danger ? ' is-danger' : ''); button.setAttribute('role', 'menuitem');
      button.dataset.menuAction = item.action; button.disabled = item.disabled; if (item.title) button.title = item.title;
      const grow = document.createElement('span'); grow.className = 'grow'; grow.textContent = item.label;
      const hint = document.createElement('span'); hint.className = 'menu-hint'; hint.textContent = item.hint;
      button.append(grow, hint); return button;
    }));
  }
  function runMenu(item) {
    const data = menuTarget; if (!data || item.disabled) return;
    const opener = rowOf(data.id)?.querySelector('[data-participant-menu]');
    const overlay = ['open', 'confirm-full', 'to-waiting', 'withdraw', 'restore'].includes(item.dataset.menuAction);
    ui.closeMenu(!overlay);
    switch (item.dataset.menuAction) {
      case 'open': void openDrawer?.(data.id, { push: true, opener }); break;
      case 'pay': void togglePay(data, rowOf(data.id)?.querySelector('[data-participant-pay]')); break;
      case 'confirm': void run('Confirm', data, { addPlace: false }); break;
      case 'confirm-full': openConfirm('confirm-full', data, opener); break;
      case 'to-waiting': openConfirm('to-waiting', data, opener); break;
      case 'withdraw': openConfirm('withdraw', data, opener); break;
      case 'restore': openConfirm('restore', data, opener); break;
    }
  }

  /* ---------------- actions, confirmations and outcomes ---------------- */
  const handlerLabel = { Confirm: 'Confirm', MoveToWaiting: 'Move to waiting list', Withdraw: 'Withdraw', Restore: 'Restore', Payment: 'Payment' };
  function actionBody(data, extra = {}) {
    const body = new FormData();
    body.set('participantId', data.id); body.set('eventVersion', state().version); body.set('responseVersion', data.version);
    for (const [key, value] of Object.entries(extra)) body.set(key, String(value));
    return body;
  }
  function successText(handler, data, result) {
    const value = result?.data || {};
    const capacity = value.addedPlace ? t(' · capacity now {0}', value.capacity) : '';
    switch (handler) {
      case 'Confirm': return value.addedPlace ? t('{0} confirmed · capacity now {1}', data.name, value.capacity) : t('AdminDesign.{0} confirmed', data.name);
      case 'Withdraw': return value.promoted?.length ? t('{0} withdrawn · {1} confirmed from the waiting list', data.name, value.promoted.join(', ')) : t('{0} withdrawn', data.name);
      case 'Restore': return (value.status === 'waiting' ? t('{0} restored to the waiting list (#{1})', data.name, value.waitingPosition) : t('{0} restored to confirmed', data.name)) + capacity;
      case 'MoveToWaiting': return t('{0} moved to the waiting list (#{1}) · {2} confirmed', data.name, value.waitingPosition, (value.promoted || []).join(', '));
      default: return '';
    }
  }
  // Posts one row action and classifies the answer; settle() then shows it. A layer that is
  // still open (busy) would block the shared re-read, so modals close before settling.
  async function act(handler, data, extra = {}) {
    const draft = { [t('Action')]: t(handlerLabel[handler]), [t('Participant')]: data.name };
    const outcome = await post(handler, actionBody(data, extra), draft);
    if (outcome.kind === 'session-lost' || signal.aborted) return { kind: 'silent' };
    const result = outcome.kind === 'handler' ? outcome.data : null;
    if (result?.outcome === 'done') return { kind: 'done', result };
    if (result?.outcome === 'stale') return { kind: 'stale' };
    if (result?.outcome === 'refused') return { kind: 'refused', message: result.message || t('That didn’t go through.') };
    if (outcome.kind === 'refused') return { kind: 'refused', message: outcome.reason || t('That didn’t go through.') };
    return { kind: 'unknown' }; // Never repeat it; show what is true now.
  }
  async function settle(handler, data, response) {
    if (response.kind !== 'refused') void refreshDrawer(data.id); // A status action from the drawer re-reads it.
    if (response.kind === 'done') { await reread(); flash(data.id); ui.toast(successText(handler, data, response.result)); }
    else if (response.kind === 'stale') { await reread(); ui.toast(t('This participant changed while you were looking at it. The current list is shown; nothing was done.'), { error: true }); }
    else if (response.kind === 'refused') ui.toast(response.message, { error: true });
    else if (response.kind === 'unknown') { await reread(); ui.toast(t('We couldn’t confirm whether this happened. The current list is shown; check it before trying again.'), { error: true }); }
  }
  async function run(handler, data, extra = {}) { await settle(handler, data, await act(handler, data, extra)); }
  async function togglePay(data, button) {
    if (!data || button?.disabled) return;
    const paid = !data.paid;
    button?.classList.add('is-busy'); if (button) button.disabled = true;
    const draft = { [t('Payment')]: paid ? t('Paid') : t('Unpaid'), [t('Participant')]: data.name };
    const body = new FormData(); body.set('participantId', data.id); body.set('payment', paid ? 'Paid' : 'Unpaid');
    const outcome = await post('Payment', body, draft);
    if (button?.isConnected) { button.classList.remove('is-busy'); button.disabled = false; }
    if (outcome.kind === 'session-lost' || signal.aborted) return;
    const result = outcome.kind === 'handler' ? outcome.data : null;
    if (result?.outcome === 'done') { await reread(); flash(data.id); ui.toast(paid ? t('{0} marked as paid', data.name) : t('{0} marked as unpaid', data.name)); return; }
    if (result?.outcome === 'refused' || result?.outcome === 'stale') { await reread(); ui.toast(result.message || t('That didn’t go through.'), { error: true }); return; }
    if (outcome.kind === 'refused') { ui.toast(outcome.reason || t('That didn’t go through.'), { error: true }); return; }
    await reread();
    ui.toast(t('We couldn’t confirm whether this happened. The current list is shown; check it before trying again.'), { error: true });
  }
  function openConfirm(kind, data, opener) {
    const current = state(), full = current.confirmed >= current.capacity, name = data.name;
    const content = ui.template('confirmation');
    const title = content.querySelector('[data-confirm-title]'), body = content.querySelector('[data-confirm-description]');
    const cancel = content.querySelector('[data-confirm-cancel]'), accept = content.querySelector('[data-confirm-accept]');
    const label = accept.querySelector('[data-component-text]');
    cancel.textContent = t('Cancel');
    let handler, idle, busyLabel, extra = () => ({}), choice = null;
    if (kind === 'withdraw') {
      title.textContent = t('Withdraw {0}?', name);
      let text = data.status === 'confirmed'
        ? current.firstWaiter ? t('{0} will leave the confirmed roster, and {1}, next on the waiting list, will take their place.', name, current.firstWaiter) : t('{0} will leave the confirmed roster. Nobody is waiting, so their place stays open for the next signup.', name)
        : t('{0} will leave the waiting list and everyone behind them moves up. No confirmed place is freed.', name);
      if (data.team) text += ' ' + t('They’ll also be removed from {0}.', data.team);
      if (data.paid) text += ' ' + t('Their payment stays recorded — handle any refund separately.');
      body.textContent = text + ' ' + t('You can restore them later.');
      handler = 'Withdraw'; idle = t('Withdraw'); busyLabel = t('Withdrawing…'); accept.className = 'btn btn-danger'; extra = () => ({ confirmLifecycleAction: true });
    } else if (kind === 'confirm-full') {
      title.textContent = t('Confirm {0} and add a place?', name);
      body.textContent = t('{0} is full ({1}/{1}). Capacity increases to {2} and {3} gets the new place. Nobody else is promoted, and everyone else on the waiting list keeps their order.', current.eventName, current.capacity, current.capacity + 1, name);
      handler = 'Confirm'; idle = t('Confirm and add a place'); busyLabel = t('Confirming…'); extra = () => ({ addPlace: true });
    } else if (kind === 'to-waiting') {
      title.textContent = t('Move {0} to the waiting list?', name);
      let text = t('{0} goes to the end of the waiting list (#{1}), and {2}, next on the waiting list, takes their confirmed place.', name, current.waiting, current.firstWaiter);
      if (data.team) text += ' ' + t('They’ll also be removed from {0}.', data.team);
      body.textContent = text;
      handler = 'MoveToWaiting'; idle = t('Move to waiting list'); busyLabel = t('Moving…');
    } else {
      title.textContent = t('Restore {0}?', name);
      body.textContent = (full ? t('{0} is full, so they return to the waiting list unless you add a place.', current.eventName) : t('A place is open, so they return as confirmed.')) + ' ' + t('Their accounts, answers and notes were kept.');
      choice = full ? 'waiting' : 'confirmed';
      const group = document.createElement('div'); group.className = 'm-extra'; group.setAttribute('role', 'radiogroup'); group.setAttribute('aria-label', t('Restore to'));
      const option = (value, heading, sub, disabled) => {
        const row = document.createElement('label'); row.className = 'check-row';
        const input = document.createElement('input'); input.type = 'radio'; input.name = 'restore-to'; input.value = value; input.disabled = disabled; input.checked = choice === value;
        const text = document.createElement('span'); const strong = document.createElement('span'); strong.className = 'pa-strong'; strong.textContent = heading;
        const small = document.createElement('span'); small.className = 'choice-sub pa-block'; small.textContent = sub;
        text.append(strong, small); row.append(input, text); return row;
      };
      group.append(option('waiting', t('Waiting list'), full ? t('Joins at #{0}', current.waiting + 1) : t('Only when the event is full'), !full),
        option('confirmed', full ? t('Confirm and add a place') : t('AdminDesign.Confirmed'), full ? t('Capacity {0} → {1}, and they get the new place', current.capacity, current.capacity + 1) : t((current.capacity - current.confirmed) === 1 ? '{0} spot left' : '{0} spots left', current.capacity - current.confirmed), false));
      body.after(group);
      handler = 'Restore'; busyLabel = t('Restoring…');
      idle = () => choice === 'confirmed' && full ? t('Restore and add a place') : t('Restore');
      extra = () => ({ addPlace: choice === 'confirmed' && full });
      group.addEventListener('change', event => { choice = event.target.value; paintChoice(); });
    }
    const error = ui.template('banner-error').firstElementChild; error.classList.add('pa-error'); error.hidden = true; error.setAttribute('role', 'alert');
    content.querySelector('.m-actions').before(error);
    let busy = false;
    const idleText = () => typeof idle === 'function' ? idle() : idle;
    const paintChoice = () => { for (const row of error.parentElement?.querySelectorAll('.check-row') || []) row.classList.toggle('is-on', row.querySelector('input').checked); for (const row of error.parentElement?.querySelectorAll('.check-row') || []) row.classList.toggle('is-disabled', row.querySelector('input').disabled); if (!busy) label.textContent = idleText(); };
    const modal = ui.openLayer({ title: title.textContent, content, confirmation: true, pending: () => busy, opener: opener || rowOf(data.id)?.querySelector('[data-participant-menu]') });
    modal.element.dataset.pageFamily = 'participants';
    paintChoice();
    const paintBusy = () => {
      accept.disabled = cancel.disabled = busy; accept.classList.toggle('is-busy', busy); accept.querySelector('.spin').hidden = !busy;
      label.textContent = busy ? busyLabel : idleText(); modal.element.setAttribute('aria-busy', String(busy));
      for (const input of modal.element.querySelectorAll('input')) input.disabled = busy || (input.value === 'waiting' && !full);
    };
    cancel.addEventListener('click', () => void ui.closeLayer());
    accept.addEventListener('click', async () => {
      if (busy) return;
      busy = true; error.hidden = true; paintBusy();
      const response = await act(handler, data, extra());
      busy = false; paintBusy();
      if (response.kind === 'silent') return; // C-CMP-2: the shell shows what wasn't saved.
      if (response.kind === 'refused') {
        // A definite refusal keeps the confirmation open with the server's reason.
        error.querySelector('[data-component-text]').textContent = response.message; error.querySelector('[data-component-lead]').hidden = true; error.hidden = false;
        return;
      }
      await modal.close(true);
      await settle(handler, data, response);
    });
  }

  /* ---------------- participant drawer (item 1b) ---------------- */
  // One drawer at a time (?participant=). It opens from the no-store current-state read
  // and saves everything with one POST (U5-Q2). A lost response re-reads and says so (U5-Q3).
  const RSN = /^[A-Za-z0-9 _-]{1,12}$/;
  const lang = document.documentElement.lang || undefined;
  const ehbFormat = new Intl.NumberFormat(lang, { maximumFractionDigits: 1 });
  const fmtEhb = value => value === null || value === undefined || String(value).trim() === '' || !Number.isFinite(Number(value)) ? '—' : ehbFormat.format(Number(value));
  const fmtNumber = value => new Intl.NumberFormat(lang).format(value);
  const noteLimit = 2000;
  const rosterEditable = () => root.dataset.editable === 'true';
  let drawer = null, drawerSeq = 0, openAdd = null;
  const tpl = name => document.importNode(root.querySelector(`template[${name}]`).content, true);
  const el = (tag, className, text) => { const node = document.createElement(tag); if (className) node.className = className; if (text !== undefined) node.textContent = text; return node; };
  // "{0}" parts of a translated sentence become the given nodes (e.g. a bold number).
  function fillNodes(template, ...parts) {
    const out = document.createDocumentFragment();
    for (const piece of template.split(/(\{\d\})/)) { const match = /^\{(\d)\}$/.exec(piece); out.append(match ? parts[Number(match[1])] ?? '' : piece); }
    return out;
  }
  function fieldNote(kind, text) {
    if (!text) return document.createDocumentFragment();
    if (kind === 'error') { const node = ui.template('field-error').firstElementChild; node.removeAttribute('id'); node.className = 'field-err'; node.querySelector('[data-component-text]').textContent = text; return node; }
    return el('div', 'field-hint', text);
  }
  const statusText = view => view.status === 'confirmed' ? t('AdminDesign.Confirmed') : view.status === 'waiting' ? t('Waiting · #{0}', view.waitingPosition) : t('Withdrawn');
  const statusTone = view => view.status === 'confirmed' ? 'badge-success' : view.status === 'waiting' ? 'badge-warning' : 'badge-neutral';
  const primaryName = view => view.accounts.find(item => item.role === 'playing' && item.primary)?.name || view.accounts.find(item => item.role === 'playing')?.name || t('External roster member');

  function makeDraft(view) {
    const playing = view.accounts.filter(item => item.role === 'playing'), alts = view.accounts.filter(item => item.role === 'alt');
    const accounts = playing.map(item => ({ key: 'a' + (++drawerSeq), assignmentId: item.assignmentId, rsn: item.name, ehb: item.ehb === null || item.ehb === undefined ? '' : String(item.ehb), hint: '' }));
    const primary = playing.findIndex(item => item.primary);
    // An alt the event no longer has a slot for stays visible so a save never drops it silently.
    const slots = Math.max(view.altSlots.length, alts.length);
    return {
      accounts, primaryKey: accounts[primary >= 0 ? primary : 0]?.key ?? null,
      alts: Array.from({ length: slots }, (_, index) => ({ assignmentId: alts[index]?.assignmentId ?? null, rsn: alts[index]?.name ?? '' })),
      answers: Object.fromEntries(view.answers.map(item => [item.questionId, item.value ?? ''])),
      paid: view.paid, note: view.adminNote || ''
    };
  }
  function signature(state) {
    const draft = state.draft, view = state.view;
    const captain = view.answers.find(item => item.system === 'captain');
    const volunteered = captain ? draft.answers[captain.questionId] === 'true' : true;
    const answers = view.answers.filter(item => !item.retired).map(item => item.system === 'cocaptain' && !volunteered ? '' : String(draft.answers[item.questionId] ?? '').trim());
    const primary = draft.accounts.find(item => item.key === draft.primaryKey);
    return JSON.stringify({ a: draft.accounts.map(item => [item.assignmentId, item.rsn.trim(), String(item.ehb).trim()]), p: primary?.rsn.trim() ?? '',
      alt: draft.alts.map(item => item.rsn.trim()), answers, paid: draft.paid, note: draft.note });
  }
  const isDirty = state => !!state?.view && signature(state) !== state.original;
  function validate(state) {
    const errors = {}, view = state.view, draft = state.draft;
    if (!view?.editable) return errors;
    const seen = new Set();
    for (const account of draft.accounts) {
      const name = account.rsn.trim(), rsnId = 'rsn-' + account.key, ehbId = 'ehb-' + account.key;
      if (!name) errors[rsnId] = t('Enter an RSN.');
      else if (!RSN.test(name)) errors[rsnId] = t('Use up to 12 letters, numbers, spaces, - or _.');
      else { if (seen.has(name.toLowerCase())) errors[rsnId] = t('This account is already listed.'); seen.add(name.toLowerCase()); }
      const ehb = String(account.ehb).trim();
      if (ehb === '') errors[ehbId] = t('Required. 0 is valid.');
      else if (/^-/.test(ehb)) errors[ehbId] = t('Can’t be negative.');
      else if (!/^\d+(\.\d+)?$/.test(ehb)) errors[ehbId] = t('Enter a number.');
      else if (Number(ehb) > 100000) errors[ehbId] = t('Use 100,000 or less.');
    }
    draft.alts.forEach((alt, index) => {
      const name = alt.rsn.trim(); if (!name) return;
      if (!RSN.test(name)) errors['alt-' + index] = t('Use up to 12 letters, numbers, spaces, - or _.');
      else if (seen.has(name.toLowerCase())) errors['alt-' + index] = t('This is already a playing account.');
      else seen.add(name.toLowerCase());
    });
    return { ...errors, ...state.serverErrors };
  }

  /* drawer rendering */
  function renderAccounts(state) {
    const rows = state.element.querySelector('[data-d-account-rows]'), view = state.view, draft = state.draft;
    rows.replaceChildren(...draft.accounts.map(account => {
      const row = tpl('data-participant-account-row').firstElementChild;
      row.dataset.key = account.key;
      const radio = row.querySelector('[data-a-primary]'), rsn = row.querySelector('[data-a-rsn]'), ehb = row.querySelector('[data-a-ehb]');
      radio.checked = account.key === draft.primaryKey; rsn.id = 'rsn-' + account.key; ehb.id = 'ehb-' + account.key;
      rsn.value = account.rsn; ehb.value = account.ehb;
      return row;
    }));
    const saved = new Set(draft.accounts.map(item => item.rsn.trim().toLowerCase()));
    state.element.querySelector('[data-d-owner-accounts]').replaceChildren(...view.savedAccounts.filter(item => !saved.has(item.name.toLowerCase())).map(item => {
      const option = el('option', '', item.ehb === null || item.ehb === undefined ? t('No saved EHB') : t('{0} EHB saved', fmtEhb(item.ehb))); option.value = item.name; return option;
    }));
  }
  function renderAlts(state) {
    const host = state.element.querySelector('[data-d-alts]'), view = state.view, draft = state.draft;
    host.replaceChildren(...draft.alts.map((alt, index) => {
      const section = el('section', 'sec'), id = 'alt-' + index;
      // One Informational slot keeps the reference's "Alt account"; several use each slot's own label.
      const name = view.altSlots.length === 1 ? t('Alt account') : view.altSlots[index] || t('Alt account');
      const label = el(view.editable ? 'label' : 'div', 'lbl', name + ' '); if (view.editable) label.htmlFor = id;
      label.append(el('span', 'opt', t('· optional, no EHB')));
      section.append(label);
      if (view.editable) {
        const input = el('input', 'input'); input.id = id; input.maxLength = 12; input.placeholder = t('None'); input.value = alt.rsn; input.autocomplete = 'off';
        input.dataset.dAlt = String(index); input.setAttribute('aria-describedby', 'e-' + id);
        const note = el('div'); note.dataset.dAltNote = String(index);
        section.append(input, note);
      } else {
        const value = el('div', 'ro-value' + (alt.rsn ? '' : ' is-empty'), alt.rsn || t('None')); section.append(value);
      }
      return section;
    }));
  }
  const answerText = (item, value) => item.type === 'YesNo' ? (value === 'true' ? t('Yes') : value === 'false' ? t('No') : '') : value;
  function renderAnswers(state) {
    const host = state.element.querySelector('[data-d-answers]'), view = state.view, draft = state.draft;
    state.element.querySelector('[data-d-answers-section]').hidden = view.answers.length === 0;
    const parts = [];
    for (const item of view.answers) {
      const wrap = el('div', parts.length ? 'pa-gap' : ''), id = 'answer-' + item.questionId;
      wrap.dataset.dAnswer = item.questionId;
      const value = String(draft.answers[item.questionId] ?? '');
      if (item.system === 'captain') {
        const label = el('div', 'lbl', t('Volunteers to captain')); label.id = 'd-cap-lbl';
        const seg = el('div', 'seg'); seg.setAttribute('role', 'radiogroup'); seg.setAttribute('aria-labelledby', 'd-cap-lbl');
        for (const [key, text] of [['true', t('Yes')], ['false', t('No')]]) {
          const option = el('label', 'seg-opt'), input = el('input', 'sr'); input.type = 'radio'; input.name = 'd-cap'; input.value = key; input.checked = value === key; input.disabled = !view.editable;
          input.dataset.dCaptain = item.questionId; option.append(input, text); seg.append(option);
        }
        wrap.append(label, seg);
      } else if (!view.editable || item.retired) {
        const label = el('div', 'lbl', item.system === 'cocaptain' ? t('Requested co-captain') : item.label + (item.retired ? ' ' : ''));
        if (item.retired) label.append(el('span', 'opt', t('· retired question')));
        const shown = answerText(item, value);
        wrap.append(label, el('div', 'ro-value' + (shown ? '' : ' is-empty'), shown || (item.system === 'cocaptain' ? t('No request') : t('No answer'))));
      } else {
        const label = el('label', 'lbl', item.system === 'cocaptain' ? t('Requested co-captain') + ' ' : item.label); label.htmlFor = id;
        if (item.system === 'cocaptain') label.append(el('span', 'opt', t('· optional')));
        let control;
        if (item.type === 'YesNo' || item.type === 'SingleChoice') {
          control = el('select', 'select pa-full');
          const options = item.type === 'YesNo' ? [['true', t('Yes')], ['false', t('No')]] : item.options.map(option => [option, option]);
          control.append(...[['', t('No answer')], ...options].map(([key, text]) => { const option = el('option', '', text); option.value = key; return option; }));
          if (value && !options.some(([key]) => key === value)) { const option = el('option', '', value); option.value = value; control.append(option); }
          control.value = value;
        } else {
          control = el('input', 'input'); control.value = value; control.autocomplete = 'off';
          if (item.type === 'Number') control.inputMode = 'decimal'; else control.maxLength = 4000;
        }
        control.id = id; control.dataset.dAnswerInput = item.questionId;
        wrap.append(label, control, el('div'));
      }
      parts.push(wrap);
    }
    host.replaceChildren(...parts);
  }
  function renderDetails(state) {
    const view = state.view, list = state.element.querySelector('[data-d-details]');
    const rows = [
      [t('Signup order'), t('#{0} of {1}', view.signupSequence, view.totalCount)],
      [t('Signed up'), view.signedUp],
      [t('Source'), view.source],
      [t('Website account'), view.username ? '@' + view.username : t('No website account'), !view.username],
      ...(view.memberSince ? [[t('Member since'), view.memberSince]] : []),
      [t('Discord'), view.discordLinked ? t('Linked') : t('Not linked'), !view.discordLinked]
    ];
    list.replaceChildren(...rows.flatMap(([key, value, muted]) => [el('dt', '', key), el('dd', muted ? 'muted' : '', value)]));
  }
  function install(state, view) {
    state.view = view; state.draft = makeDraft(view); state.serverErrors = {}; state.touched = new Set(); state.showErrors = false;
    state.original = signature(state);
    const node = state.element, editable = view.editable, privateEditable = view.privateEditable;
    node.querySelector('[data-d-state]').replaceChildren();
    node.querySelector('[data-d-content]').hidden = false;
    node.querySelector('[data-d-eyebrow]').textContent = view.username ? t('Participant · @{0}', view.username) : t('Participant');
    node.querySelector('[data-d-title]').textContent = primaryName(view);
    node.querySelector('[data-d-badges]').hidden = false;
    const badge = node.querySelector('[data-d-status]'); badge.className = 'badge ' + statusTone(view); badge.textContent = statusText(view);
    node.querySelector('[data-d-team]').textContent = view.team || t('No team yet');
    // B-Participants-3: a withdrawn participant is read-only with a "Restore to edit" hint;
    // after the draft starts the reference's lock banner shows instead.
    const rosterLocked = !rosterEditable() || (!editable && view.status !== 'withdrawn');
    node.querySelector('[data-d-locked]').hidden = !rosterLocked;
    node.querySelector('[data-d-withdrawn]').hidden = rosterLocked || view.status !== 'withdrawn';
    for (const input of node.querySelectorAll('[data-d-pay]')) { input.checked = (input.value === 'paid') === view.paid; input.disabled = !privateEditable; input.closest('.seg-opt').classList.toggle('is-disabled', !privateEditable); }
    node.querySelector('[data-d-accounts-edit]').hidden = !editable;
    const read = node.querySelector('[data-d-accounts-read]'); read.hidden = editable;
    read.replaceChildren(...view.accounts.filter(item => item.role === 'playing').map(item => {
      const row = el('div', 'ro-row'); row.append(el('span', 'grow', item.name));
      if (item.primary) row.append(el('span', 'pill', t('Primary')));
      row.append(el('span', 'tnum pa-ro-ehb', fmtEhb(item.ehb))); return row;
    }));
    if (editable) renderAccounts(state);
    renderAlts(state); renderAnswers(state); renderDetails(state);
    const note = node.querySelector('[data-d-note]'); note.value = state.draft.note; note.disabled = !privateEditable;
    const withdraw = node.querySelector('[data-d-status-action="withdraw"]'), restore = node.querySelector('[data-d-status-action="restore"]');
    withdraw.hidden = view.status === 'withdrawn'; restore.hidden = view.status !== 'withdrawn';
    for (const button of [withdraw, restore]) { button.disabled = !rosterEditable(); button.title = rosterEditable() ? '' : view.lockReason; }
    paintDrawer(state);
    state.layer.markClean();
  }
  // Everything that follows the draft without rebuilding inputs (focus and caret stay).
  function paintDrawer(state) {
    const node = state.element, view = state.view, draft = state.draft;
    if (!view) return;
    const errors = validate(state), shown = id => state.showErrors || state.touched.has(id) ? errors[id] || '' : '';
    for (const row of node.querySelectorAll('[data-d-account-rows] .acct-row')) {
      const account = draft.accounts.find(item => item.key === row.dataset.key); if (!account) continue;
      const name = account.rsn.trim() || t('new account');
      const rsnError = shown('rsn-' + account.key), ehbError = shown('ehb-' + account.key);
      const rsn = row.querySelector('[data-a-rsn]'), ehb = row.querySelector('[data-a-ehb]'), radio = row.querySelector('[data-a-primary]'), remove = row.querySelector('[data-a-remove]');
      radio.setAttribute('aria-label', t('Use {0} as primary account', name)); rsn.setAttribute('aria-label', t('RSN for {0}', name)); ehb.setAttribute('aria-label', t('EHB for {0}', name));
      rsn.classList.toggle('is-invalid', !!rsnError); rsn.setAttribute('aria-invalid', String(!!rsnError));
      ehb.classList.toggle('is-invalid', !!ehbError); ehb.setAttribute('aria-invalid', String(!!ehbError));
      const rsnNote = row.querySelector('[data-a-rsn-note]'), ehbNote = row.querySelector('[data-a-ehb-note]');
      rsnNote.replaceChildren(rsnError ? fieldNote('error', rsnError) : fieldNote('hint', account.hint));
      ehbNote.replaceChildren(fieldNote('error', ehbError));
      const noteNode = rsnNote.firstElementChild; if (noteNode) { noteNode.id = 'e-rsn-' + account.key; rsn.setAttribute('aria-describedby', noteNode.id); } else rsn.removeAttribute('aria-describedby');
      const only = draft.accounts.length <= 1;
      remove.disabled = only; remove.setAttribute('aria-label', t('Remove {0}', name)); remove.title = only ? t('At least one playing account is required') : t('Remove account');
    }
    for (const holder of node.querySelectorAll('[data-d-alt-note]')) {
      const index = holder.dataset.dAltNote, error = shown('alt-' + index), input = node.querySelector(`[data-d-alt="${index}"]`);
      holder.replaceChildren(fieldNote('error', error)); if (holder.firstElementChild) holder.firstElementChild.id = 'e-alt-' + index;
      input.classList.toggle('is-invalid', !!error); input.setAttribute('aria-invalid', String(!!error));
    }
    for (const holder of node.querySelectorAll('[data-d-answer]')) {
      const id = 'answer-' + holder.dataset.dAnswer, error = shown(id), control = holder.querySelector('[data-d-answer-input]');
      if (!control) continue;
      holder.lastElementChild.replaceChildren(fieldNote('error', error));
      control.classList.toggle('is-invalid', !!error); control.setAttribute('aria-invalid', String(!!error));
    }
    const slots = view.playingSlots, full = draft.accounts.length >= slots;
    const add = node.querySelector('[data-d-add-account]'); add.disabled = full; add.title = full ? t('All {0} account slots for this event are used', slots) : '';
    node.querySelector('[data-d-slot-text]').textContent = t('{0} of {1} account slots', draft.accounts.length, slots);
    const primary = view.editable ? draft.accounts.find(item => item.key === draft.primaryKey) : view.accounts.find(item => item.role === 'playing' && item.primary);
    const value = el('b', '', primary && /^\d+(\.\d+)?$/.test(String(primary.ehb ?? '').trim()) ? fmtEhb(primary.ehb) : '—');
    node.querySelector('[data-d-draft-value]').replaceChildren(fillNodes(labels['Draft value {0} EHB'] ?? 'Draft value {0} EHB', value));
    const captain = view.answers.find(item => item.system === 'captain'), volunteered = captain ? draft.answers[captain.questionId] === 'true' : true;
    for (const option of node.querySelectorAll('[data-d-captain]')) option.closest('.seg-opt').classList.toggle('is-on', option.checked), option.closest('.seg-opt').classList.toggle('is-disabled', option.disabled);
    const cocaptain = view.answers.find(item => item.system === 'cocaptain');
    const coInput = cocaptain && node.querySelector(`[data-d-answer-input="${cocaptain.questionId}"]`);
    if (coInput) { coInput.disabled = !volunteered; coInput.placeholder = volunteered ? t('No request') : t('Only for captain volunteers'); }
    for (const input of node.querySelectorAll('[data-d-pay]')) input.closest('.seg-opt').classList.toggle('is-on', input.checked);
    const was = node.querySelector('[data-d-pay-was]'); was.hidden = draft.paid === view.paid; was.textContent = view.paid ? t('Was paid') : t('Was unpaid');
    const count = node.querySelector('[data-d-note-count]'), length = draft.note.length;
    count.textContent = length > noteLimit * 0.7 ? t('{0} / {1}', fmtNumber(length), fmtNumber(noteLimit)) : ''; count.className = length > noteLimit * 0.9 ? 'is-near' : '';
    const dirty = isDirty(state);
    node.querySelector('[data-d-dirty]').hidden = !dirty;
    const save = node.querySelector('[data-d-save]');
    save.disabled = state.saving || !dirty; save.classList.toggle('is-busy', state.saving); save.querySelector('.spin').hidden = !state.saving;
    save.querySelector('[data-d-save-label]').textContent = state.saving ? t('Saving…') : t('Save changes');
    for (const button of node.querySelectorAll('[data-d-close]')) if (button.classList.contains('btn')) button.disabled = state.saving;
    node.setAttribute('aria-busy', String(state.saving));
    const count2 = Object.keys(errors).length, summary = node.querySelector('[data-d-summary]');
    if (state.showErrors && count2 && !state.saving) {
      const text = count2 === 1 ? t('Fix the highlighted field to save.') : t('Fix the {0} highlighted fields to save.', count2);
      if (summary) summary.querySelector('[data-component-text]').textContent = text;
      else { const banner = ui.template('banner-error').firstElementChild; banner.dataset.dSummary = ''; banner.querySelector('[data-component-lead]').hidden = true; banner.querySelector('[data-component-text]').textContent = text; node.querySelector('[data-d-messages]').prepend(banner); }
    } else summary?.remove();
  }
  // A drawer message: a definite refusal, a stale read or a lost response (rule 13).
  function drawerMessage(state, tone, text) {
    const host = state.element.querySelector('[data-d-messages]');
    for (const node of host.querySelectorAll('[data-d-message]')) node.remove();
    if (!text) return;
    const banner = ui.template(tone === 'error' ? 'banner-error' : tone === 'uncertain' ? 'banner-uncertain' : 'banner-warning').firstElementChild;
    banner.dataset.dMessage = ''; banner.querySelector('[data-component-lead]').hidden = true; banner.querySelector('[data-component-text]').textContent = text;
    host.append(banner);
    state.element.querySelector('#drawer-body').scrollTop = 0;
  }
  function showDrawerState(state, name) {
    state.element.querySelector('[data-d-content]').hidden = true;
    state.element.querySelector('[data-d-state]').replaceChildren(tpl(name));
    for (const button of state.element.querySelectorAll('[data-d-status-action]')) button.hidden = true;
  }
  async function readCurrent(id) {
    const outcome = await window.AdminFetch.request(`${actionUrl('Current')}&participant=${encodeURIComponent(id)}`, { expect: 'json', readback: true, signal, cache: 'no-store' });
    if (outcome.kind === 'handler' && outcome.data?.id) return { kind: 'ok', view: outcome.data };
    if (outcome.status === 404) return { kind: 'missing' };
    return { kind: outcome.kind === 'session-lost' ? 'session-lost' : 'failed' };
  }
  async function loadDrawer(state) {
    showDrawerState(state, 'data-participant-drawer-loading');
    const result = await readCurrent(state.id);
    if (drawer !== state) return false;
    if (result.kind === 'ok') { install(state, result.view); return true; }
    showDrawerState(state, result.kind === 'missing' ? 'data-participant-drawer-missing' : 'data-participant-drawer-failed');
    if (result.kind === 'missing') state.element.querySelector('[data-d-title]').textContent = t('Participant not found');
    return false;
  }

  async function openDrawer(id, { push = false, opener = document.activeElement, view, missing = false } = {}) {
    if (drawer?.id === id) return;
    if (drawer && !await closeDrawer({ history: false })) return;
    drawerParam = ['participant', id];
    if (push) setUrl(directoryUrl(), true);
    const state = drawer = { mode: 'edit', id, view: null, draft: null, pushed: push, opener, saving: false, silent: false, serverErrors: {}, touched: new Set(), showErrors: false };
    const layer = ui.openLayer({ kind: 'drawer', title: t('Participant'), content: tpl('data-participant-drawer'), opener,
      dirty: () => isDirty(state), pending: () => state.saving,
      onClose: async (_result, { navigating } = {}) => {
        if (drawer === state) { drawer = null; drawerParam = null; }
        markSelected();
        if (navigating) return;
        if (!state.silent) { if (state.pushed) await ui.backUrl(); else setUrl(directoryUrl()); }
        (rowOf(id)?.querySelector('[data-participant-open]') || search).focus({ preventScroll: true });
      } });
    state.layer = layer; state.element = layer.element;
    layer.element.dataset.pageFamily = 'participants';
    layer.element.setAttribute('aria-labelledby', 'drawer-title'); layer.element.removeAttribute('aria-label');
    wireDrawer(state);
    markSelected();
    if (view) install(state, view);
    else if (missing) { showDrawerState(state, 'data-participant-drawer-missing'); state.element.querySelector('[data-d-title]').textContent = t('Participant not found'); }
    else await loadDrawer(state);
    state.element.querySelector('#drawer-close').focus({ preventScroll: true });
  }
  async function closeDrawer({ history = true } = {}) {
    if (!drawer) return true;
    const state = drawer; state.silent = !history;
    const closed = await state.layer.close(false);
    if (!closed) state.silent = false;
    return closed;
  }
  function changed(state, id) { if (id) { state.touched.add(id); delete state.serverErrors[id]; } paintDrawer(state); }
  function wireDrawer(state) {
    const node = state.element, on = (type, fn) => node.addEventListener(type, fn, { signal });
    on('click', event => {
      if (event.target.closest('[data-d-close]')) { void ui.closeLayer(); return; }
      if (event.target.closest('[data-d-retry]')) { void loadDrawer(state); return; }
      if (!state.view || state.saving) return;
      const draft = state.draft;
      if (event.target.closest('[data-d-add-account]')) {
        if (draft.accounts.length >= state.view.playingSlots) return;
        const key = 'a' + (++drawerSeq);
        draft.accounts.push({ key, assignmentId: null, rsn: '', ehb: '', hint: '' });
        if (!draft.primaryKey) draft.primaryKey = key;
        renderAccounts(state); paintDrawer(state);
        node.querySelector('#rsn-' + key)?.focus();
        return;
      }
      const remove = event.target.closest('[data-a-remove]');
      if (remove && !remove.disabled) {
        const row = remove.closest('.acct-row'), key = row.dataset.key;
        if (draft.accounts.length <= 1) return;
        draft.accounts = draft.accounts.filter(item => item.key !== key);
        if (draft.primaryKey === key) draft.primaryKey = draft.accounts[0].key;
        for (const id of ['rsn-' + key, 'ehb-' + key]) { delete state.serverErrors[id]; state.touched.delete(id); }
        row.classList.add('is-removing');
        const done = () => { if (drawer === state) { renderAccounts(state); paintDrawer(state); } };
        if (ui.reducedMotion() || getComputedStyle(row).animationName === 'none') done(); else row.addEventListener('animationend', done, { once: true });
        node.querySelector('#add-acct')?.focus({ preventScroll: true });
        paintDrawer(state);
        return;
      }
      const toggle = event.target.closest('[data-d-details-toggle]');
      if (toggle) { toggleDetails(state, toggle); return; }
      const status = event.target.closest('[data-d-status-action]');
      if (status && !status.disabled) void statusAction(state, status);
      if (event.target.closest('[data-d-save]')) void saveDrawer(state);
    });
    on('input', event => {
      if (!state.view) return;
      const target = event.target, draft = state.draft, row = target.closest('.acct-row');
      if (target.matches('[data-a-rsn]')) { const account = draft.accounts.find(item => item.key === row.dataset.key); account.rsn = target.value; account.hint = ''; delete state.serverErrors['rsn-' + account.key]; }
      else if (target.matches('[data-a-ehb]')) { const account = draft.accounts.find(item => item.key === row.dataset.key); account.ehb = target.value; account.hint = ''; delete state.serverErrors['ehb-' + account.key]; delete state.serverErrors['rsn-' + account.key]; }
      else if (target.matches('[data-d-alt]')) { draft.alts[Number(target.dataset.dAlt)].rsn = target.value; delete state.serverErrors['alt-' + target.dataset.dAlt]; }
      else if (target.matches('[data-d-answer-input]')) { draft.answers[target.dataset.dAnswerInput] = target.value; delete state.serverErrors['answer-' + target.dataset.dAnswerInput]; }
      else if (target.matches('[data-d-note]')) { draft.note = target.value; delete state.serverErrors.note; }
      else return;
      paintDrawer(state);
    });
    on('change', event => {
      if (!state.view) return;
      const target = event.target, draft = state.draft;
      if (target.matches('[data-a-primary]')) draft.primaryKey = target.closest('.acct-row').dataset.key;
      else if (target.matches('[data-d-pay]')) draft.paid = target.value === 'paid';
      else if (target.matches('[data-d-captain]')) draft.answers[target.dataset.dCaptain] = target.value;
      else if (target.matches('[data-d-answer-input]')) { draft.answers[target.dataset.dAnswerInput] = target.value; changed(state, 'answer-' + target.dataset.dAnswerInput); return; }
      else return;
      paintDrawer(state);
    });
    on('focusout', event => {
      if (!state.view) return;
      const target = event.target;
      if (target.matches('[data-a-rsn]')) { rsnBlur(state, target.closest('.acct-row').dataset.key); changed(state, target.id); }
      else if (target.matches('[data-a-ehb], [data-d-alt]')) changed(state, target.id);
    });
  }
  // Reads the owner's saved accounts only; an event edit never writes back to them (U5-Q1).
  function rsnBlur(state, key) {
    const account = state.draft.accounts.find(item => item.key === key), name = account?.rsn.trim();
    if (!account || !name) return;
    const saved = state.view.savedAccounts.find(item => item.name.toLowerCase() === name.toLowerCase());
    if (saved && String(account.ehb).trim() === '' && saved.ehb !== null && saved.ehb !== undefined) {
      account.rsn = saved.name; account.ehb = String(saved.ehb); account.hint = t('EHB filled in from the saved account');
      const row = state.element.querySelector(`.acct-row[data-key="${CSS.escape(key)}"]`);
      row.querySelector('[data-a-rsn]').value = account.rsn; row.querySelector('[data-a-ehb]').value = account.ehb;
    } else if (!saved && RSN.test(name) && !state.view.accounts.some(item => item.assignmentId === account.assignmentId && item.name.toLowerCase() === name.toLowerCase())) {
      account.hint = state.view.username ? t('Not one of @{0}’s saved accounts. Used for this event only.', state.view.username) : t('Not a saved account. Used for this event only.');
    }
  }
  function toggleDetails(state, toggle) {
    const list = state.element.querySelector('[data-d-details]'), open = toggle.getAttribute('aria-expanded') === 'true';
    if (!open) { list.classList.remove('is-closing'); list.hidden = false; toggle.setAttribute('aria-expanded', 'true'); toggle.classList.add('is-open'); return; }
    if (list.classList.contains('is-closing')) return;
    toggle.setAttribute('aria-expanded', 'false'); toggle.classList.remove('is-open'); list.classList.add('is-closing');
    const done = () => { list.hidden = true; list.classList.remove('is-closing'); };
    if (ui.reducedMotion() || getComputedStyle(list).animationName === 'none') done(); else list.addEventListener('animationend', done, { once: true });
  }
  function drawerRow(state) {
    const view = state.view;
    return { id: view.id, name: primaryName(view), status: view.status, version: String(view.responseVersion), paid: view.paid, team: view.team || '', position: view.waitingPosition || 0 };
  }
  async function statusAction(state, button) {
    // Unsaved drawer edits would be stale after a status change: ask first (decision B).
    if (isDirty(state) && !await ui.confirmDiscard()) return;
    openConfirm(button.dataset.dStatusAction, drawerRow(state), button);
  }
  async function refreshDrawer(id) {
    const state = drawer;
    if (state?.mode !== 'edit' || state.id !== id || state.saving) return;
    const result = await readCurrent(id);
    if (drawer === state && result.kind === 'ok') install(state, result.view);
  }
  function mapServerField(state, field) {
    const match = /^(playing|alt):(\d+)$/.exec(field || '');
    if (match?.[1] === 'playing') { const account = state.draft.accounts[Number(match[2])]; return account ? 'rsn-' + account.key : null; }
    if (match?.[1] === 'alt') { const filled = state.draft.alts.map((item, index) => [item, index]).filter(([item]) => item.rsn.trim()); const entry = filled[Number(match[2])]; return entry ? 'alt-' + entry[1] : null; }
    if (field?.startsWith('answer:')) return 'answer-' + field.slice('answer:'.length);
    return null;
  }
  async function saveDrawer(state) {
    if (state.saving || !state.view || !isDirty(state)) return;
    const view = state.view, draft = state.draft;
    state.serverErrors = {}; state.showErrors = true;
    const errors = validate(state), first = Object.keys(errors)[0];
    if (first) { drawerMessage(state, null, ''); paintDrawer(state); state.element.querySelector('#' + CSS.escape(first))?.focus(); return; }
    const body = new FormData();
    body.set('participantId', view.id); body.set('expectedResponseVersion', String(view.responseVersion));
    body.set('expectedPaid', String(view.paid)); body.set('expectedNote', view.adminNote || '');
    body.set('paid', String(draft.paid)); body.set('note', draft.note);
    if (view.editable) {
      body.set('accounts', JSON.stringify([
        ...draft.accounts.map(item => ({ assignmentId: item.assignmentId, name: item.rsn.trim(), ehb: Number(String(item.ehb).trim()), role: 'playing', primary: item.key === draft.primaryKey })),
        ...draft.alts.filter(item => item.rsn.trim()).map(item => ({ assignmentId: item.assignmentId, name: item.rsn.trim(), ehb: null, role: 'alt', primary: false }))]));
      body.set('answers', JSON.stringify(Object.fromEntries(view.answers.filter(item => !item.retired).map(item => [item.questionId, String(draft.answers[item.questionId] ?? '').trim()]))));
    }
    const name = view.editable ? (draft.accounts.find(item => item.key === draft.primaryKey)?.rsn.trim() || primaryName(view)) : primaryName(view);
    const sessionDraft = { [t('Participant')]: primaryName(view), [t('Entry payment')]: draft.paid ? t('Paid') : t('Unpaid'), [t('Private note')]: draft.note };
    if (view.editable) sessionDraft[t('Playing accounts')] = draft.accounts.map(item => `${item.rsn.trim()} (${item.ehb})`).join(', ');
    state.saving = true; drawerMessage(state, null, ''); paintDrawer(state);
    const outcome = await post('SaveParticipant', body, sessionDraft);
    state.saving = false;
    if (drawer !== state || signal.aborted) return;
    paintDrawer(state);
    if (outcome.kind === 'session-lost') return; // C-CMP-2: the shell shows what wasn't saved; the draft stays.
    const result = outcome.kind === 'handler' ? outcome.data : null;
    if (result?.outcome === 'saved') {
      await state.layer.close(true);
      await reread(); flash(view.id);
      ui.toast(t('Changes to {0} saved', name));
      return;
    }
    if (result?.outcome === 'invalid') {
      const id = mapServerField(state, result.field);
      if (id) { state.serverErrors[id] = result.message || t('Check this field.'); paintDrawer(state); state.element.querySelector('#' + CSS.escape(id))?.focus(); }
      else { drawerMessage(state, 'error', result.message || t('That didn’t go through.')); }
      return;
    }
    if (result?.outcome === 'refused' || outcome.kind === 'refused') {
      // A definite refusal: nothing was saved; the server says why and the edits stay.
      drawerMessage(state, 'error', result?.message || outcome.reason || t('That didn’t go through.'));
      return;
    }
    if (result?.outcome === 'stale') {
      const current = await readCurrent(view.id);
      if (drawer !== state) return;
      if (current.kind === 'ok') install(state, current.view);
      drawerMessage(state, 'warning', result.message || t('This participant changed while you were editing. The current details are shown; nothing was saved.'));
      void reread();
      return;
    }
    // Lost response: never repeat the write; show what is true now.
    const current = await readCurrent(view.id);
    if (drawer !== state) return;
    if (current.kind === 'ok') { install(state, current.view); drawerMessage(state, 'uncertain', t('We couldn’t confirm whether your changes were saved. The current details are shown; check them before saving again.')); }
    else { drawerMessage(state, 'uncertain', t('We couldn’t confirm whether your changes were saved, and the current details didn’t load. Your edits are still here; check the participant before saving again.')); return; } // no list re-read: it would ask to discard the kept edits
    void reread();
  }

  /* ---------------- Add drawer (item 1c, F04) ---------------- */
  // A website account's saved Playing accounts, no questions and no WOM (B-Participants-5 search).
  const initials = name => (name || '?').replace(/[^A-Za-z0-9]+/g, ' ').trim().split(' ').slice(0, 2).map(part => part[0]?.toUpperCase() || '').join('') || '?';
  const addDirty = state => !!(state.query.trim() || state.owner);
  async function openAddDrawer(_button, { push = true, opener = root.querySelector('#add-btn') || document.activeElement } = {}) {
    if (drawer?.mode === 'add') return;
    if (!rosterEditable()) return;
    if (drawer && !await closeDrawer({ history: false })) return;
    drawerParam = ['add', '1'];
    // The Add drawer replaces the URL rather than pushing, so no history entry can resubmit it.
    setUrl(directoryUrl(), false);
    const current = state(), full = current.confirmed >= current.capacity;
    const add = drawer = { mode: 'add', id: null, query: '', owner: null, accounts: [], selected: new Set(), primary: null, place: full ? 'waiting' : 'confirmed', paid: false,
      results: [], searchToken: 0, showErrors: false, saving: false, silent: false, pushed: false, opener };
    const layer = ui.openLayer({ kind: 'drawer', title: t('Add participant'), content: tpl('data-participant-add-drawer'), opener,
      dirty: () => !add.quiet && addDirty(add), pending: () => add.saving,
      onClose: async (_result, { navigating } = {}) => {
        if (drawer === add) { drawer = null; drawerParam = null; }
        if (navigating) return;
        if (!add.silent) setUrl(directoryUrl());
      } });
    add.layer = layer; add.element = layer.element;
    layer.element.dataset.pageFamily = 'participants';
    layer.element.setAttribute('aria-labelledby', 'drawer-title'); layer.element.removeAttribute('aria-label');
    wireAdd(add); paintAdd(add);
    layer.markClean(); // paintAdd sets the default place and payment radios; they are the baseline, not edits
    add.element.querySelector('#add-search').focus({ preventScroll: true });
  }
  openAdd = openAddDrawer;
  function addErrors(add) {
    if (!add.showErrors) return {};
    if (!add.owner) return { search: t('Choose a website account.') };
    if (!add.selected.size) return { accounts: add.accounts.some(item => !eligibility(add, item)) ? t('Select at least one playing account.') : t('This account has no playing accounts that can be added.') };
    return {};
  }
  function eligibility(add, item) {
    if (item.savedEhb === null || item.savedEhb === undefined) return t('No saved EHB — update the account first');
    if (item.inEvent) return t('Already in this event');
    return '';
  }
  function paintAdd(add) {
    const node = add.element, current = state(), slots = Number(root.dataset.playingSlots || 1);
    const full = current.confirmed >= current.capacity, left = Math.max(0, current.capacity - current.confirmed);
    const errors = addErrors(add);
    node.querySelector('[data-ad-pick]').hidden = !!add.owner;
    const picked = node.querySelector('[data-ad-picked]'); picked.hidden = !add.owner;
    const search = node.querySelector('[data-ad-search]');
    search.classList.toggle('is-invalid', !!errors.search); search.setAttribute('aria-invalid', String(!!errors.search));
    const searchNote = node.querySelector('[data-ad-search-note]'); searchNote.replaceChildren(fieldNote('error', errors.search)); if (searchNote.firstElementChild) searchNote.firstElementChild.id = 'add-search-err';
    if (add.owner) {
      node.querySelector('[data-ad-initials]').textContent = initials(add.owner.username);
      node.querySelector('[data-ad-username]').textContent = '@' + add.owner.username;
      node.querySelector('[data-ad-sub]').textContent = add.owner.discordName || t('Discord not linked');
    }
    node.querySelector('[data-ad-placeholder]').hidden = !!add.owner;
    node.querySelector('[data-ad-accounts-wrap]').hidden = !add.owner;
    const list = node.querySelector('[data-ad-accounts]');
    list.replaceChildren(...add.accounts.map(item => {
      const checked = add.selected.has(item.characterId);
      const reason = eligibility(add, item) || (!checked && add.selected.size >= slots ? t('All {0} account slots are used', slots) : '');
      const row = el('div', 'pick' + (reason ? ' is-disabled' : checked ? ' is-checked' : ''));
      const box = el('input'); box.type = 'checkbox'; box.id = 'addacc-' + item.characterId; box.checked = checked; box.disabled = !!reason; box.dataset.adAccount = item.characterId;
      const label = el('label', 'pick-label'); label.htmlFor = box.id; label.append(el('div', 'pick-name', item.name)); if (reason) label.append(el('div', 'pick-sub', reason));
      const ehb = el('div', 'pick-ehb' + (item.savedEhb === null || item.savedEhb === undefined ? ' is-missing' : ''), item.savedEhb === null || item.savedEhb === undefined ? t('No EHB') : t('{0} EHB', fmtEhb(item.savedEhb)));
      const primary = el('label', 'pick-prim' + (checked ? '' : ' is-hidden')); const radio = el('input'); radio.type = 'radio'; radio.name = 'add-primary'; radio.checked = add.primary === item.characterId; radio.disabled = !checked; radio.dataset.adPrimary = item.characterId;
      radio.setAttribute('aria-label', t('Use {0} as primary account', item.name)); primary.append(radio, t('Primary'));
      row.append(box, label, ehb, primary); return row;
    }));
    const accountsNote = node.querySelector('[data-ad-accounts-note]'); accountsNote.replaceChildren(fieldNote('error', errors.accounts)); if (accountsNote.firstElementChild) accountsNote.firstElementChild.id = 'add-accounts-err';
    node.querySelector('[data-ad-accounts-hint]').textContent = t(slots === 1 ? 'EHB comes from the saved account and can be changed for this event after adding. Up to {0} playing account per participant.' : 'EHB comes from the saved account and can be changed for this event after adding. Up to {0} playing accounts per participant.', slots);
    const primary = add.accounts.find(item => item.characterId === add.primary);
    const draftValue = node.querySelector('[data-ad-draft]'); draftValue.hidden = !primary;
    if (primary) draftValue.replaceChildren(fillNodes(labels['Draft value {0} EHB'] ?? 'Draft value {0} EHB', el('b', '', fmtEhb(primary.savedEhb))));
    if (!full) add.place = 'confirmed';
    for (const input of node.querySelectorAll('[data-ad-place]')) {
      input.checked = input.value === add.place; input.disabled = add.saving || (input.value === 'waiting' && !full);
      input.closest('.choice').classList.toggle('is-on', input.checked); input.closest('.choice').classList.toggle('is-disabled', input.value === 'waiting' && !full);
    }
    node.querySelector('[data-ad-pc-title]').textContent = full ? t('Confirm and add a place') : t('AdminDesign.Confirmed');
    node.querySelector('[data-ad-pc-sub]').textContent = full ? t('Full · capacity {0} → {1}', current.capacity, current.capacity + 1) : t('{0} of {1} spots left', left, current.capacity);
    node.querySelector('[data-ad-pw-sub]').textContent = full ? t('Joins at #{0}', current.waiting + 1) : t('Only when the event is full');
    const capHint = node.querySelector('[data-ad-cap-hint]'); capHint.hidden = !(full && add.place === 'confirmed');
    capHint.textContent = t('Capacity will increase from {0} to {1} when you add this participant.', current.capacity, current.capacity + 1);
    for (const input of node.querySelectorAll('[data-ad-pay]')) { input.checked = (input.value === 'paid') === add.paid; input.closest('.seg-opt').classList.toggle('is-on', input.checked); }
    const submit = node.querySelector('[data-ad-submit]');
    submit.disabled = add.saving; submit.classList.toggle('is-busy', add.saving); submit.querySelector('.spin').hidden = !add.saving;
    submit.querySelector('[data-ad-submit-label]').textContent = add.saving ? t('Adding…') : t('Add participant');
    for (const button of node.querySelectorAll('.dr-foot [data-d-close]')) button.disabled = add.saving;
    node.setAttribute('aria-busy', String(add.saving));
  }
  function paintResults(add) {
    const node = add.element, box = node.querySelector('[data-ad-results]'), query = add.query.trim();
    box.hidden = !query; // B-Participants-5: no "Recently joined" list before a search.
    if (!query) return;
    node.querySelector('[data-ad-results-label]').textContent = add.results.length ? t(add.results.length === 1 ? '{0} match' : '{0} matches', add.results.length) : t('No matches');
    const list = node.querySelector('[data-ad-result-list]');
    if (!add.results.length) { list.replaceChildren(el('div', 'res-empty', t('No website accounts match “{0}”.', query))); return; }
    list.replaceChildren(...add.results.map(owner => {
      const button = el('button', 'res'); button.type = 'button'; button.dataset.adOwner = owner.id; button.disabled = !!owner.inEvent;
      const text = el('span', 'grow'); text.append(el('span', 'res-name pa-block', '@' + owner.username),
        el('span', 'res-sub pa-block', (owner.matchedAccount ? t('Owns {0}', owner.matchedAccount) : owner.savedAccounts.join(', ')) + ' · ' + (owner.discordName || t('Discord not linked'))));
      const note = el('span', 'res-note', owner.inEvent === 'withdrawn' ? t('Withdrawn · restore instead') : owner.inEvent ? t('Already in event') : t(owner.savedAccounts.length === 1 ? '{0} saved account' : '{0} saved accounts', owner.savedAccounts.length));
      const avatar = el('span', 'avatar', initials(owner.username)); avatar.setAttribute('aria-hidden', 'true');
      button.append(avatar, text, note); return button;
    }));
  }
  async function searchOwners(add) {
    const token = ++add.searchToken, query = add.query.trim();
    if (!query) { add.results = []; paintResults(add); return; }
    const outcome = await window.AdminFetch.request(`${actionUrl('SearchOwnerAccounts')}&search=${encodeURIComponent(query)}`, { expect: 'json', readback: true, signal, cache: 'no-store' });
    if (drawer !== add || token !== add.searchToken) return;
    if (outcome.kind === 'handler' && Array.isArray(outcome.data)) { add.results = outcome.data; paintResults(add); drawerMessage(add, null, ''); }
    else if (outcome.kind !== 'session-lost') drawerMessage(add, 'error', t('Website accounts couldn’t be searched. Try again.'));
  }
  async function pickOwner(add, owner) {
    const outcome = await window.AdminFetch.request(`${actionUrl('OwnerAccounts')}&owner=${encodeURIComponent(owner.id)}`, { expect: 'json', readback: true, signal, cache: 'no-store' });
    if (drawer !== add) return;
    if (!(outcome.kind === 'handler' && Array.isArray(outcome.data))) { if (outcome.kind !== 'session-lost') drawerMessage(add, 'error', t('That account’s saved playing accounts didn’t load. Try again.')); return; }
    add.owner = owner; add.accounts = outcome.data; add.selected = new Set(); add.primary = null;
    const eligible = add.accounts.filter(item => !eligibility(add, item));
    if (eligible.length === 1) { add.selected.add(eligible[0].characterId); add.primary = eligible[0].characterId; }
    drawerMessage(add, null, ''); paintAdd(add);
    (add.element.querySelector(`#addacc-${CSS.escape(eligible[0]?.characterId || '')}`) || add.element.querySelector('#add-change'))?.focus({ preventScroll: true });
  }
  function wireAdd(add) {
    const node = add.element, on = (type, fn) => node.addEventListener(type, fn, { signal });
    let searchTimer;
    on('click', event => {
      if (event.target.closest('[data-d-close]')) { void ui.closeLayer(); return; }
      if (add.saving) return;
      const owner = event.target.closest('[data-ad-owner]');
      if (owner && !owner.disabled) { void pickOwner(add, add.results.find(item => item.id === owner.dataset.adOwner)); return; }
      if (event.target.closest('[data-ad-change]')) { add.owner = null; add.accounts = []; add.selected = new Set(); add.primary = null; paintAdd(add); node.querySelector('#add-search').focus(); return; }
      if (event.target.closest('[data-ad-submit]')) void submitAdd(add);
    });
    on('input', event => {
      if (!event.target.matches('[data-ad-search]')) return;
      add.query = event.target.value; clearTimeout(searchTimer); searchTimer = setTimeout(() => void searchOwners(add), 250);
    });
    on('keydown', event => {
      if (event.target.matches('[data-ad-search]')) {
        if (event.key === 'Enter') { event.preventDefault(); clearTimeout(searchTimer); void searchOwners(add); }
        else if (event.key === 'ArrowDown') { event.preventDefault(); node.querySelector('#add-results button.res:not([disabled])')?.focus(); }
        return;
      }
      const result = event.target.closest('#add-results button.res');
      if (result && (event.key === 'ArrowDown' || event.key === 'ArrowUp')) {
        event.preventDefault();
        const items = [...node.querySelectorAll('#add-results button.res:not([disabled])')], index = items.indexOf(result) + (event.key === 'ArrowDown' ? 1 : -1);
        if (index < 0) node.querySelector('#add-search').focus(); else items[Math.min(index, items.length - 1)]?.focus();
      }
    });
    on('change', event => {
      const target = event.target, slots = Number(root.dataset.playingSlots || 1);
      if (target.matches('[data-ad-account]')) {
        const id = target.dataset.adAccount;
        if (target.checked && add.selected.size < slots) { add.selected.add(id); if (!add.primary) add.primary = id; }
        else { add.selected.delete(id); if (add.primary === id) add.primary = [...add.selected][0] || null; }
      } else if (target.matches('[data-ad-primary]')) add.primary = target.dataset.adPrimary;
      else if (target.matches('[data-ad-place]')) add.place = target.value;
      else if (target.matches('[data-ad-pay]')) add.paid = target.value === 'paid';
      else return;
      paintAdd(add);
      (target.id ? node.querySelector('#' + CSS.escape(target.id)) : target.isConnected ? target : null)?.focus({ preventScroll: true });
    });
  }
  // The list behind an open Add drawer is re-read without the shell's unsaved-changes prompt (the drawer's draft is kept).
  async function rereadBehind(add) {
    add.quiet = true; add.layer.markClean();
    try { await reread(); } finally { add.quiet = false; }
  }
  async function submitAdd(add) {
    if (add.saving) return;
    add.showErrors = true;
    const errors = addErrors(add);
    if (errors.search || errors.accounts) { paintAdd(add); (add.element.querySelector(errors.search ? '#add-search' : '#add-accounts input:not([disabled])') || add.element.querySelector('#add-accounts'))?.focus(); return; }
    const current = state(), full = current.confirmed >= current.capacity;
    const body = new FormData();
    body.set('owner', add.owner.id); for (const id of add.selected) body.append('accounts', id);
    body.set('primary', add.primary); body.set('paid', String(add.paid)); body.set('addPlace', String(full && add.place === 'confirmed')); body.set('eventVersion', current.version);
    const names = add.accounts.filter(item => add.selected.has(item.characterId)).map(item => item.name);
    const sessionDraft = { [t('Website account')]: '@' + add.owner.username, [t('Playing accounts')]: names.join(', '), [t('Entry payment')]: add.paid ? t('Paid') : t('Unpaid') };
    add.saving = true; drawerMessage(add, null, ''); paintAdd(add);
    const outcome = await post('Add', body, sessionDraft);
    add.saving = false;
    if (drawer !== add || signal.aborted) return;
    paintAdd(add);
    if (outcome.kind === 'session-lost') return;
    const result = outcome.kind === 'handler' ? outcome.data : null;
    if (result?.outcome === 'done') {
      const value = result.data || {}, name = value.name || add.accounts.find(item => item.characterId === add.primary)?.name || '';
      await add.layer.close(true);
      await reread(); if (value.participantId) flash(value.participantId);
      const text = (value.status === 'waiting' ? t('{0} added to the waiting list (#{1})', name, value.waitingPosition) : t('{0} added to confirmed', name)) + (value.addedPlace ? t(' · capacity now {0}', value.capacity) : '');
      ui.toast(text, { actionLabel: t('Show'), action: () => {
        search.value = name;
        void readDirectory(withQuery(params => { params.set('q', name); params.delete('pay'); if (value.status === 'waiting') params.set('tab', 'waiting'); else params.delete('tab'); }), { record: true }).then(() => { if (value.participantId) flash(value.participantId); });
      } });
      return;
    }
    if (result?.outcome === 'stale') { await rereadBehind(add); paintAdd(add); drawerMessage(add, 'warning', t('The event changed while you were adding. The current numbers are shown; nothing was added.')); return; }
    if (result?.outcome === 'refused' || outcome.kind === 'refused') { drawerMessage(add, 'error', result?.message || outcome.reason || t('Couldn’t add the participant. Nothing was saved.')); return; }
    // Lost response: never resubmit; show the current list and say so.
    await rereadBehind(add); paintAdd(add);
    drawerMessage(add, 'uncertain', t('We couldn’t confirm whether {0} was added. The current list is shown; check it before trying again.', '@' + add.owner.username));
  }

  /* ---------------- URL state ---------------- */
  const sameDirectory = (a, b) => { const x = new URL(a), y = new URL(b); for (const u of [x, y]) { u.searchParams.delete('participant'); u.searchParams.delete('add'); } return x.pathname.toLowerCase() === y.pathname.toLowerCase() && x.search === y.search; };
  const unregisterUrl = ui.registerUrlState(async (next, previous) => {
    const nextUrl = new URL(next);
    if (nextUrl.pathname.toLowerCase() !== `/admin/events/participants/${eventId}`.toLowerCase()) return false;
    const participant = nextUrl.searchParams.get('participant'), add = nextUrl.searchParams.get('add') === '1';
    if (!sameDirectory(next, previous)) {
      if (drawer) await closeDrawer({ history: false });
      search.value = nextUrl.searchParams.get('q') || '';
      await readDirectory(next);
    }
    if (participant) { if (drawer?.id !== participant) await openDrawer(participant, { opener: search }); }
    else if (add) { if (drawer?.mode !== 'add') await openAdd?.(null, { opener: search }); }
    else if (drawer) await closeDrawer({ history: false });
    return true;
  });
  // Canonical URL for this render; a direct ?participant= link opens its drawer.
  const initial = root.querySelector('[data-participant-drawer-initial]');
  if (initial) {
    drawerParam = ['participant', initial.dataset.participantId];
    setUrl(directoryUrl());
    let view = null; try { view = JSON.parse(initial.textContent || 'null'); } catch { view = null; }
    void openDrawer(initial.dataset.participantId, { opener: search, view, missing: !view });
  } else if (root.dataset.drawerAdd === 'true') {
    drawerParam = ['add', '1'];
    setUrl(directoryUrl());
    void openAdd?.(null, { opener: search });
  } else setUrl(directoryUrl());
  release = () => {
    life.abort(); clearTimeout(timer); overflow.disconnect(); cancelAnimationFrame(overflowFrame); unregisterUrl();
    drawer = null; menuTarget = null;
  };
}

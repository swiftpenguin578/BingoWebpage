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
    for (const key of ['directoryCanonical', 'eventVersion', 'capacity', 'confirmed', 'waiting', 'firstWaiter', 'editable', 'privateEditable', 'lockReason']) root.dataset[key] = fresh.dataset[key] ?? '';
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

  /* ---------------- drawer and Add (items 1b, 1c) ---------------- */
  let drawer = null;
  let openDrawer = null, openAdd = null;

  /* ---------------- URL state ---------------- */
  const sameDirectory = (a, b) => { const x = new URL(a), y = new URL(b); for (const u of [x, y]) { u.searchParams.delete('participant'); u.searchParams.delete('add'); } return x.pathname.toLowerCase() === y.pathname.toLowerCase() && x.search === y.search; };
  const unregisterUrl = ui.registerUrlState(async (next, previous) => {
    const nextUrl = new URL(next);
    if (nextUrl.pathname.toLowerCase() !== `/admin/events/participants/${eventId}`.toLowerCase()) return false;
    if (!sameDirectory(next, previous)) {
      search.value = nextUrl.searchParams.get('q') || '';
      await readDirectory(next);
    }
    return true;
  });
  setUrl(directoryUrl());
  release = () => {
    life.abort(); clearTimeout(timer); overflow.disconnect(); cancelAnimationFrame(overflowFrame); unregisterUrl();
    drawer = null; menuTarget = null;
  };
}

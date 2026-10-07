// Accounts (WA-5). Server-owned directory state through the shared in-page update helper;
// one account drawer (?account=), alert-dialog confirmations, the transient reset link and
// the Transfer dialog. The shell owns transport classification (AdminFetch, C-CMP-2), busy
// timing (AdminUI.busy), layers, history and dirty guards; this module never retries a write.
let release;
export function dispose() { release?.(); release = null; }
export function init(region, ui = window.AdminUI) {
  dispose();
  const root = region.querySelector('[data-accounts-directory]');
  if (!root) return;
  const labels = JSON.parse(root.dataset.accountLabels || '{}');
  const t = (key, ...args) => (labels[key] ?? key).replace(/\{(\d)\}/g, (_match, i) => args[i] ?? '');
  const life = new AbortController(), signal = life.signal;
  const listen = (node, type, fn) => node?.addEventListener(type, fn, { signal });
  const results = root.querySelector('[data-accounts-results]');
  const search = root.querySelector('[data-accounts-search]');
  const actorName = root.dataset.actorName || '';
  let query = new URL(root.dataset.directoryCanonical, location.href);
  let timer, edits = 0;
  const schema = values => Object.fromEntries(Object.keys(values).map(key => [key, { valid: () => true, default: '' }]));
  const setUrl = (url, record = false) => { const values = Object.fromEntries(new URL(url, location.href).searchParams); ui.setUrl(values, schema(values), { record }); };
  const importChildren = (target, source) => target.replaceChildren(...document.importNode(source, true).childNodes);
  const template = selector => document.importNode(root.querySelector(selector).content, true);
  const directoryUrl = (account = drawer?.id) => { const url = new URL(query.href); url.searchParams.delete('account'); if (account) url.searchParams.set('account', account); return url.href; };

  /* ---------------- directory ---------------- */
  function patchDirectory(doc) {
    const fresh = doc.querySelector('[data-accounts-directory]');
    if (!fresh) throw new Error('Missing accounts directory');
    importChildren(results, fresh.querySelector('[data-accounts-results]'));
    importChildren(root.querySelector('[data-accounts-footer]'), fresh.querySelector('[data-accounts-footer]'));
    importChildren(root.querySelector('[data-directory-banners]'), fresh.querySelector('[data-directory-banners]'));
    const summary = region.querySelector('[data-accounts-summary]'), nextSummary = doc.querySelector('[data-accounts-summary]');
    if (summary && nextSummary) summary.textContent = nextSummary.textContent;
    const transfer = fresh.querySelector('template[data-account-transfer-template]'), ownTransfer = root.querySelector('template[data-account-transfer-template]');
    if (transfer && ownTransfer) ownTransfer.replaceWith(document.importNode(transfer, true));
    root.dataset.directoryCanonical = fresh.dataset.directoryCanonical;
    query = new URL(fresh.dataset.directoryCanonical, location.href);
    paintQuery();
    markSelected();
    return directoryUrl();
  }
  function paintQuery() {
    const role = query.searchParams.get('role') || '';
    for (const input of root.querySelectorAll('[data-accounts-role]')) { input.checked = input.value === role; input.closest('.seg-opt').classList.toggle('is-on', input.checked); }
    search.closest('.search').classList.toggle('has-clear', search.value.length > 0);
    root.querySelector('[data-accounts-clear]').hidden = !search.value.length;
  }
  const pendingRows = () => {
    const fragment = document.createDocumentFragment();
    for (const node of document.querySelector('template[data-page-loading-template="accounts"]').content.querySelectorAll('.sk-row')) fragment.append(document.importNode(node, true));
    return fragment;
  };
  const failedRows = () => {
    const fragment = document.createDocumentFragment();
    fragment.append(document.importNode(document.querySelector('template[data-page-failure-template="accounts"]').content.querySelector('.empty'), true));
    return fragment;
  };
  function readDirectory(target, { record = false } = {}) {
    const url = new URL(target, location.href), revision = edits;
    url.searchParams.delete('account');
    query = new URL(url.href); paintQuery();
    if (record) setUrl(url.href, true);
    return ui.update(url.href, { root, results, patch: patchDirectory, pending: pendingRows, failed: failedRows,
      fallbackFocus: () => search, signal, scrollRegions: [root.querySelector('[data-accounts-wrap]')],
      current: () => edits === revision, draft: () => ({ [search.getAttribute('aria-label')]: search.value }) });
  }
  function searchNow() {
    clearTimeout(timer);
    const url = new URL(query.href); url.searchParams.delete('page');
    const value = search.value.trim().slice(0, 100);
    if (value) url.searchParams.set('q', value); else url.searchParams.delete('q');
    void readDirectory(url.href);
  }
  listen(search, 'input', () => { edits++; ui.supersedeUpdate(); clearTimeout(timer); paintQuery(); timer = setTimeout(searchNow, 250); });
  listen(search, 'keydown', event => {
    if (event.key === 'Enter') { event.preventDefault(); searchNow(); }
    else if (event.key === 'Escape' && search.value) { event.preventDefault(); search.value = ''; edits++; searchNow(); }
  });
  listen(root.querySelector('[data-accounts-clear]'), 'click', () => { search.value = ''; edits++; search.focus({ preventScroll: true }); searchNow(); });
  listen(root, 'change', event => {
    const input = event.target.closest('[data-accounts-role]');
    if (!input) return;
    const url = new URL(query.href); url.searchParams.delete('page');
    if (input.value) url.searchParams.set('role', input.value); else url.searchParams.delete('role');
    void readDirectory(url.href);
  });
  listen(region, 'click', event => {
    const transfer = event.target.closest('[data-account-transfer-open]');
    if (transfer && !transfer.closest('[data-page-skeleton]')) { event.preventDefault(); openTransfer(transfer); return; }
    const control = event.target.closest('[data-accounts-url]');
    if (control) {
      event.preventDefault();
      if (control.closest('.empty')) { search.value = new URL(control.dataset.accountsUrl, location.href).searchParams.get('q') || ''; edits++; }
      void readDirectory(control.dataset.accountsUrl, { record: control.hasAttribute('data-accounts-push') });
      return;
    }
    const link = event.target.closest('[data-account-open]');
    if (link && (event.button !== 0 || event.metaKey || event.ctrlKey || event.shiftKey || event.altKey)) return;
    const row = event.target.closest('[data-account-row]');
    if (link || row && !event.target.closest('a,button')) {
      event.preventDefault();
      void openDrawer((link || row.querySelector('[data-account-open]')).dataset.accountOpen, { push: true, opener: row.querySelector('[data-account-open]') });
    }
  });
  const wrap = root.querySelector('[data-accounts-wrap]');
  let overflowFrame;
  const overflow = new ResizeObserver(() => { cancelAnimationFrame(overflowFrame); overflowFrame = requestAnimationFrame(() => wrap.classList.toggle('is-scroll', wrap.scrollWidth > wrap.clientWidth + 1)); });
  overflow.observe(wrap);

  /* ---------------- drawer ---------------- */
  let drawer = null, drawerToken = 0, confirmModal = null;
  const reasonDrafts = new Map();
  function markSelected() { for (const row of root.querySelectorAll('[data-account-row]')) row.classList.toggle('is-selected', row.dataset.accountRow === drawer?.id); }
  function drawerData() {
    const source = drawer?.source;
    return source ? { id: source.dataset.accountId, name: source.dataset.accountName, version: source.dataset.accountVersion, role: source.dataset.accountRole, active: source.dataset.accountActive === 'true', disabledByMe: source.dataset.accountDisabledByMe === 'true', reason: source.dataset.accountDisabledReason || '' } : null;
  }
  function installDrawer(source, { keepBanner = false } = {}) {
    // Everything the drawer shows comes from this template for its own account id (A1).
    if (source.dataset.accountMissing !== 'true' && source.dataset.accountId !== drawer.id) throw new Error('Drawer content for another account');
    const panel = drawer.element, content = source.content;
    drawer.source = source;
    panel.querySelector('.dr-head').replaceWith(document.importNode(content.querySelector('.dr-head'), true));
    const body = document.importNode(content.querySelector('.dr-body'), true);
    const serverBanner = body.querySelector('[data-account-banner-slot]'); serverBanner?.remove();
    if (!keepBanner) drawer.slot.replaceChildren(...(serverBanner ? serverBanner.childNodes : []));
    drawer.holder.replaceChildren(...body.childNodes);
    drawer.confirms = new Map([...content.querySelectorAll('template[data-account-confirm]')].map(node => [node.dataset.accountConfirm, node]));
    paintDrawer();
  }
  function paintDrawer() {
    if (!drawer) return;
    const locked = !!drawer.busy || !!drawer.unresolved;
    for (const button of drawer.element.querySelectorAll('[data-account-action] button, [data-account-transfer-open]')) button.disabled = locked;
  }
  function setBanner(tone, title, text, action) {
    if (!drawer) return;
    const slot = drawer.slot;
    const wrapper = document.createElement('div'); wrapper.className = 'ac-dr-banner';
    const banner = ui.template(tone === 'is-success' ? 'banner-info' : tone === 'is-info' ? 'banner-info' : tone === 'is-error' ? 'banner-error' : 'banner-warning').firstElementChild;
    banner.className = `banner ${tone}`; banner.id = 'dr-banner'; banner.tabIndex = -1; banner.setAttribute('role', 'alert'); banner.hidden = false;
    if (tone === 'is-success') banner.querySelector('svg')?.replaceWith(ui.template('toast').querySelector('svg.t-ic')?.cloneNode(true) ?? document.createTextNode(''));
    const lead = banner.querySelector('[data-component-lead]'); lead.hidden = !title; lead.textContent = title;
    banner.querySelector('[data-component-text]').textContent = ' ' + text;
    if (action) {
      const button = document.createElement('button'); button.type = 'button'; button.className = 'btn btn-sm'; button.textContent = action.label;
      button.addEventListener('click', () => action.run(button)); banner.append(button);
    } else {
      const dismiss = document.createElement('button'); dismiss.type = 'button'; dismiss.className = 'banner-btn'; dismiss.textContent = t('Dismiss');
      dismiss.addEventListener('click', () => { wrapper.remove(); drawer?.element.querySelector('#sec-access')?.focus({ preventScroll: true }); });
      banner.append(dismiss);
    }
    wrapper.append(banner); slot.replaceChildren(wrapper);
    banner.focus({ preventScroll: true });
  }
  function readDrawer() {
    if (!drawer) return Promise.resolve(false);
    const state = drawer, token = drawerToken;
    return ui.update(directoryUrl(state.id), { root, results: state.holder, signal,
      patch: doc => {
        const source = doc.querySelector('template[data-account-drawer]');
        if (!source || token !== drawerToken || drawer !== state) throw new Error('Missing or superseded account drawer');
        patchDirectory(doc);
        installDrawer(source, { keepBanner: true });
        return directoryUrl(state.id);
      },
      pending: () => template('template[data-account-drawer-pending]'),
      failed: () => template('template[data-account-drawer-failed]'),
      fallbackFocus: () => state.element.querySelector('#dr-banner') || state.element.querySelector('#drawer-close'),
      current: () => token === drawerToken && drawer === state });
  }
  function drawerShell(name) {
    const content = document.createDocumentFragment();
    const head = document.createElement('div'); head.className = 'dr-head';
    const grow = document.createElement('div'); grow.className = 'grow';
    const eyebrow = document.createElement('div'); eyebrow.className = 'eyebrow'; eyebrow.textContent = t('Website account');
    const title = document.createElement('h2'); title.className = 'dr-title'; title.id = 'drawer-title'; title.textContent = name || t('Loading…');
    grow.append(eyebrow, title);
    const close = document.createElement('button'); close.type = 'button'; close.className = 'icon-btn'; close.id = 'drawer-close'; close.setAttribute('aria-label', t('Close')); close.dataset.accountDrawerClose = '';
    close.append(ui.template('toast').querySelector('.toast-x svg').cloneNode(true));
    head.append(grow, close);
    const body = document.createElement('div'); body.className = 'dr-body'; body.id = 'drawer-body';
    const slot = document.createElement('div'); slot.dataset.accountBannerSlot = '';
    const holder = document.createElement('div'); holder.dataset.accountDrawerContent = '';
    body.append(slot, holder);
    const foot = document.createElement('div'); foot.className = 'dr-foot';
    const spacer = document.createElement('div'); spacer.className = 'spacer';
    const done = document.createElement('button'); done.type = 'button'; done.className = 'btn'; done.id = 'dr-cancel'; done.dataset.accountDrawerClose = ''; done.textContent = t('Close');
    foot.append(spacer, done);
    content.append(head, body, foot);
    return { content, slot, holder };
  }
  async function openDrawer(id, { push = false, opener = document.activeElement, source = null } = {}) {
    if (drawer?.id === id) return;
    if (drawer && !await closeDrawer({ history: false })) return;
    if (push) setUrl(directoryUrl(id), true);
    const { content, slot, holder } = drawerShell(root.querySelector(`[data-account-open="${CSS.escape(id)}"]`)?.textContent);
    const state = drawer = { id, busy: null, unresolved: null, pushed: push, opener, slot, holder, confirms: new Map(), silent: false };
    drawerToken++;
    const layer = ui.openLayer({ kind: 'drawer', title: t('Website account'), content, opener,
      pending: () => !!state.busy,
      onClose: async (_result, { navigating } = {}) => {
        if (drawer === state) { drawer = null; drawerToken++; }
        markSelected();
        if (navigating) return;
        if (!state.silent) { if (state.pushed) await ui.backUrl(); else setUrl(directoryUrl(null)); }
        // A4: the row can be gone (filtered out); fall back to the search control.
        (root.querySelector(`#open-${CSS.escape(id)}`) || search).focus({ preventScroll: true });
      } });
    state.layer = layer; state.element = layer.element;
    layer.element.classList.add('is-wide'); layer.element.dataset.pageFamily = 'accounts';
    layer.element.setAttribute('aria-labelledby', 'drawer-title'); layer.element.removeAttribute('aria-label');
    markSelected();
    if (source) { installDrawer(source); state.element.querySelector('#dr-banner')?.focus({ preventScroll: true }); return; }
    await readDrawer();
  }
  async function closeDrawer({ history = true } = {}) {
    if (!drawer) return true;
    const state = drawer; state.silent = !history;
    const closed = await state.layer.close(false);
    if (!closed) state.silent = false;
    return closed;
  }
  listen(document, 'click', event => {
    if (!drawer || !drawer.element.contains(event.target)) return;
    if (event.target.closest('[data-account-drawer-close]')) { void ui.closeLayer(); return; }
    if (event.target.closest('[data-account-banner-dismiss]')) { event.target.closest('.ac-dr-banner')?.remove(); drawer.element.querySelector('#sec-access')?.focus({ preventScroll: true }); return; }
    if (event.target.closest('[data-account-transfer-open]')) { openTransfer(event.target.closest('[data-account-transfer-open]')); return; }
    if (event.target.closest('[data-account-secret-done]')) { clearSecret(true); return; }
    if (event.target.closest('[data-account-copy]')) { void copySecret(); return; }
    const button = event.target.closest('[data-account-action] button');
    if (button) { event.preventDefault(); const form = button.closest('form'); if (form.dataset.accountAction === 'GenerateResetLink') void generateLink(form); else openConfirm(form); }
  });
  listen(document, 'submit', event => { if (drawer?.element.contains(event.target)) event.preventDefault(); });

  /* ---------------- confirmations and outcomes ---------------- */
  const doneText = {
    GrantAdmin: name => [t('Admin access granted.'), t('{0} is now an Admin. Their sessions were ended, so they’ll sign in again to use it.', name)],
    RevokeAdmin: name => [t('Admin access revoked.'), t('{0} is now a User. Their account, events, team roles and characters are unchanged; their sessions were ended.', name)],
    Disable: name => [t('Account disabled.'), t('{0} can’t sign in and their sessions were ended. Their account, rosters and records are kept.', name)],
    Restore: name => [t('Account restored.'), t('{0} can sign in again. Event signups and roles weren’t changed.', name)]
  };
  const draftLabel = { GrantAdmin: 'Grant Admin to {0}', RevokeAdmin: 'Revoke Admin from {0}', Disable: 'Disable {0}', Restore: 'Restore {0}' };
  const intended = { GrantAdmin: data => data.role === 'admin', RevokeAdmin: data => data.role === 'user', Disable: data => !data.active, Restore: data => data.active };
  function openConfirm(form) {
    const state = drawer, kind = form.dataset.accountAction, source = state.confirms.get(kind);
    if (!source || state.busy || state.unresolved) return;
    const data = drawerData(), content = document.importNode(source.content, true);
    const reason = content.querySelector('[data-account-reason]');
    if (reason && reasonDrafts.has(data.id)) reason.value = reasonDrafts.get(data.id);
    let busy = false;
    const modal = confirmModal = ui.openLayer({ title: content.querySelector('[data-confirm-title]').textContent, content, confirmation: true, pending: () => busy, opener: form.querySelector('button'), onClose: () => { if (confirmModal === modal) confirmModal = null; } });
    modal.element.classList.add('is-wide'); modal.element.dataset.pageFamily = 'accounts';
    const panel = modal.element, accept = panel.querySelector('[data-account-confirm-accept]'), cancel = panel.querySelector('[data-account-confirm-cancel]');
    const label = accept.querySelector('[data-component-text]'), idle = label.textContent;
    const error = panel.querySelector('#cm-reason-err'), count = panel.querySelector('[data-account-reason-count]');
    const paintReason = (message = '') => {
      if (!reason) return;
      const length = reason.value.length; count.textContent = t('{0} / 500', length); count.classList.toggle('is-near', length > 450);
      error.hidden = !message; error.querySelector('[data-component-text]').textContent = message;
      reason.classList.toggle('is-invalid', !!message); reason.setAttribute('aria-invalid', String(!!message));
    };
    paintReason();
    reason?.addEventListener('input', () => { paintReason(); reasonDrafts.set(data.id, reason.value); });
    const paintBusy = () => {
      accept.disabled = cancel.disabled = busy; accept.classList.toggle('is-busy', busy); accept.querySelector('.spin').hidden = !busy;
      label.textContent = busy ? source.dataset.accountBusy : idle; panel.setAttribute('aria-busy', String(busy));
      if (reason) reason.disabled = busy;
    };
    cancel.addEventListener('click', () => void ui.closeLayer());
    accept.addEventListener('click', async () => {
      if (busy) return;
      if (reason) {
        const value = reason.value.trim(), message = !value ? t('Enter a reason.') : value.length > 500 ? t('Use 500 characters or fewer.') : '';
        if (message) { paintReason(message); reason.focus(); return; }
      }
      const body = new FormData(form);
      if (reason) body.set('Reason', reason.value);
      const draft = { [t('Action')]: t(draftLabel[kind], data.name), ...(reason ? { [t('Reason')]: reason.value } : {}) };
      busy = true; state.busy = kind; paintBusy(); paintDrawer();
      const outcome = await ui.busy(() => window.AdminFetch.request(form.action, { method: 'POST', body, expect: 'json', draft, signal }));
      busy = false; if (state.busy === kind) state.busy = null; paintBusy(); paintDrawer();
      if (outcome.kind === 'session-lost' || signal.aborted || drawer !== state) return; // C-CMP-2: notice shows what wasn't saved.
      const result = outcome.kind === 'handler' ? outcome.data : null;
      if (result?.outcome === 'invalid') { paintReason(result.message); reason?.focus(); return; }
      await modal.close(true);
      if (result?.outcome === 'completed') {
        reasonDrafts.delete(data.id);
        if (await readDrawer() && drawer === state) { const [title, text] = doneText[kind](data.name); setBanner('is-success', title, text); }
        else if (drawer === state) { const [title, text] = doneText[kind](data.name); setBanner('is-success', title, text); }
        return;
      }
      if (result?.outcome === 'stale') { await readDrawer(); if (drawer === state) setBanner('is-warning', t('This record was changed by another administrator.'), t('Current values are shown; review them before trying again.')); return; }
      if (result?.outcome === 'refused') { await readDrawer(); if (drawer === state) setBanner('is-warning', t('That couldn’t be done.'), result.message); return; }
      if (outcome.status === 404) { await readDrawer(); return; }
      // Unknown: never repeat it; gate further actions until the current state is checked (A2).
      state.unresolved = { kind, version: data.version, reason: reason?.value.trim() || '' }; paintDrawer();
      setBanner('is-warning', t('We couldn’t confirm the change.'), t('It may have gone through. Check the current status before trying again.'), { label: t('Check current status'), run: button => void checkStatus(button) });
    });
  }
  async function checkStatus(button) {
    const state = drawer; if (!state?.unresolved) return;
    button.disabled = true; button.textContent = t('Checking…');
    const { kind, version, reason } = state.unresolved;
    const read = await readDrawer();
    if (drawer !== state) return;
    if (!read) {
      setBanner('is-warning', t('Couldn’t check the current status.'), t('Nothing was repeated. Check again when you’re back online.'), { label: t('Check current status'), run: next => void checkStatus(next) });
      return;
    }
    const data = drawerData();
    state.unresolved = null; paintDrawer();
    // Versions only grow with a change, so an unchanged version proves nothing happened.
    if (data.version === version) setBanner('is-info', t('It wasn’t changed.'), t('The current values are shown. You can try again.'));
    else if (intended[kind](data) && kind === 'Disable' && data.disabledByMe && data.reason === reason) { const [title, text] = doneText[kind](data.name); setBanner('is-success', t('It went through.') + ' ' + title, text); }
    else if (intended[kind](data)) setBanner('is-info', t('The account shows this change now.'), t('It may be yours or another administrator’s. The current values are shown; nothing was repeated.'));
    else setBanner('is-warning', t('The account was changed, but not like this.'), t('Another change happened since. The current values are shown; review them before trying again.'));
  }

  /* ---------------- reset link (D5/A1: response only, drawer memory only) ---------------- */
  function resetSlot() { return drawer?.element.querySelector('[data-account-reset-slot]'); }
  function clearSecret(focus) {
    resetSlot()?.replaceChildren();
    const button = drawer?.element.querySelector('#a-reset'); if (button) button.hidden = false;
    if (focus) button?.focus({ preventScroll: true });
  }
  function secretNode(kind, data, link) {
    const node = document.createElement('div');
    node.className = `secret-result${kind === 'unsure' ? ' is-warn' : ''}`; node.setAttribute('role', 'region'); node.id = 'sr'; node.tabIndex = -1;
    node.dataset.accountSecretFor = data.id;
    const headRow = document.createElement('div'); headRow.className = 'secret-head';
    const titleNode = document.createElement('span'); titleNode.className = 'secret-title'; titleNode.id = 'sr-title';
    node.setAttribute('aria-labelledby', 'sr-title');
    const notes = document.createElement('ul'); notes.className = 'secret-notes';
    const note = text => { const li = document.createElement('li'); li.textContent = text; notes.append(li); };
    const foot = document.createElement('div'); foot.className = 'secret-foot';
    const footText = document.createElement('span');
    const button = (text, cls, run) => { const b = document.createElement('button'); b.type = 'button'; b.className = `btn btn-sm ${cls}`; b.textContent = text; b.addEventListener('click', run); return b; };
    if (kind === 'link') {
      titleNode.textContent = t('Reset link for {0}', data.name);
      const expires = document.createElement('span'); expires.className = 'secret-exp'; expires.textContent = t('Expires {0}', link.expires);
      headRow.append(titleNode, expires);
      const field = document.createElement('div'); field.className = 'copy-field'; field.setAttribute('role', 'group'); field.setAttribute('aria-label', t('Reset link'));
      const value = document.createElement('span'); value.className = 'mono'; value.id = 'sr-link'; value.textContent = link.link;
      const copy = button(t('Copy'), 'btn-quiet', () => void copySecret()); copy.id = 'sr-copy'; copy.dataset.accountCopy = '';
      field.append(value, copy);
      note(t('The link stops working after 60 minutes, after one use, or if this account’s role, status or ownership changes.'));
      note(t('It doesn’t change their password or send them anything. Share it with {0} privately.', data.name));
      footText.textContent = t('Shown only here. Closing this account clears it.');
      const doneButton = button(t('Done'), '', () => clearSecret(true)); doneButton.id = 'sr-done';
      foot.append(footText, doneButton);
      node.append(headRow, field, notes, foot);
    } else {
      titleNode.textContent = t('We couldn’t confirm whether a link was created'); headRow.append(titleNode);
      note(t('If one was, it can’t be shown again; links are never stored in readable form.'));
      note(t('Generate a new link only if {0} still needs one. It replaces any earlier link.', data.name));
      const form = drawer.element.querySelector('[data-account-action="GenerateResetLink"]');
      foot.append(footText, button(t('Not now'), 'btn-quiet', () => clearSecret(true)), button(t('Generate a new link'), '', () => void generateLink(form)));
      node.append(headRow, notes, foot);
    }
    return node;
  }
  async function generateLink(form) {
    const state = drawer; if (!state || state.busy || state.unresolved || !form) return;
    const data = drawerData(), token = drawerToken;
    const button = form.querySelector('button'), label = button.querySelector('[data-component-text]'), idle = t('Create reset link');
    clearSecret(false);
    state.busy = 'reset'; paintDrawer();
    button.classList.add('is-busy'); button.querySelector('.spin').hidden = false; label.textContent = t('Creating…');
    const outcome = await ui.busy(() => window.AdminFetch.request(form.action, { method: 'POST', body: new FormData(form), expect: 'json', draft: { [t('Action')]: t('Create a reset link for {0}', data.name) }, signal }));
    if (state.busy === 'reset') state.busy = null;
    button.classList.remove('is-busy'); button.querySelector('.spin').hidden = true; label.textContent = idle; paintDrawer();
    // A1: a response is shown only in the drawer of the account it was requested for.
    if (drawer !== state || token !== drawerToken || signal.aborted || outcome.kind === 'session-lost') return;
    const result = outcome.kind === 'handler' ? outcome.data : null;
    if (result?.outcome === 'completed' && result.accountId === data.id && drawerData()?.id === data.id) {
      button.hidden = true;
      const node = secretNode('link', data, result); resetSlot().replaceChildren(node); node.focus({ preventScroll: true });
      return;
    }
    if (result?.outcome === 'refused') { await readDrawer(); if (drawer === state) setBanner('is-warning', t('No link was created.'), result.message); return; }
    if (outcome.status === 404) { await readDrawer(); return; }
    button.hidden = true;
    const node = secretNode('unsure', data); resetSlot().replaceChildren(node); node.focus({ preventScroll: true });
  }
  async function copySecret() {
    const value = drawer?.element.querySelector('#sr-link'), button = drawer?.element.querySelector('#sr-copy');
    if (!value || !button) return;
    const select = () => { const range = document.createRange(); range.selectNodeContents(value); const selection = getSelection(); selection.removeAllRanges(); selection.addRange(range); ui.toast(t('Selected. Press Ctrl+C or ⌘C to copy.')); };
    try {
      await navigator.clipboard.writeText(value.textContent);
      button.textContent = t('Copied');
      // Copy feedback only (not a busy state): the label returns after two seconds.
      setTimeout(() => { if (button.isConnected) button.textContent = t('Copy'); }, 2000);
    } catch { select(); }
  }

  /* ---------------- ownership transfer ---------------- */
  let transfer = null;
  function openTransfer(opener) {
    const source = root.querySelector('template[data-account-transfer-template]');
    if (!source || transfer) return;
    const content = document.importNode(source.content, true);
    const state = transfer = { step: 'form', busy: false, leaving: false, chosen: null, done: null };
    const modal = ui.openLayer({ title: t('Transfer ownership'), content, opener,
      pending: () => state.busy || (!state.leaving && (state.step === 'done' || state.step === 'unsure')),
      dirty: () => state.step === 'confirm',
      onClose: () => { if (transfer === state) transfer = null; password.value = ''; } });
    state.modal = modal;
    const panel = modal.element; panel.classList.add('is-wide', 'modal-form'); panel.dataset.pageFamily = 'accounts';
    const q = selector => panel.querySelector(selector);
    const to = q('[data-transfer-to]'), password = q('[data-transfer-password]'), name = q('[data-transfer-name]');
    const confirm = q('[data-transfer-confirm]'), cancel = q('[data-transfer-cancel]'), confirmLabel = confirm.querySelector('[data-component-text]');
    const fieldError = (input, message) => {
      const error = q('#' + input.getAttribute('aria-describedby').split(' ').at(-1));
      error.hidden = !message; error.querySelector('[data-component-text]').textContent = message || '';
      input.classList.toggle('is-invalid', !!message); input.setAttribute('aria-invalid', String(!!message));
    };
    const banner = (tone, title, text) => {
      const node = q('[data-transfer-banner]'); node.hidden = !tone; if (!tone) return;
      node.className = `banner m-banner ${tone}`; q('[data-transfer-banner-title]').textContent = title; q('[data-transfer-banner-text]').textContent = text; node.focus({ preventScroll: true });
    };
    const option = () => to.selectedOptions[0]?.value ? to.selectedOptions[0] : null;
    function rows(pairs, before, after) {
      q('[data-transfer-before-head]').textContent = before; q('[data-transfer-after-head]').textContent = after;
      const holder = q('[data-transfer-rows]'); holder.replaceChildren();
      for (const [who, from, into] of pairs) {
        const row = template('template[data-account-transfer-row]').firstElementChild;
        row.querySelector('.chg-field').textContent = who;
        const keys = row.querySelectorAll('.chg-k'), values = row.querySelectorAll('[data-value]');
        keys[0].textContent = before; keys[1].textContent = after; values[0].textContent = from; values[1].textContent = into;
        holder.append(row);
      }
    }
    function points(list) { const node = q('[data-transfer-points]'); node.replaceChildren(...list.map(text => { const li = document.createElement('li'); li.textContent = text; return li; })); node.hidden = !list.length; }
    function paint() {
      const step = state.step, chosen = step === 'form' ? (option() ? { id: option().value, name: option().dataset.username, role: option().dataset.role, version: option().dataset.version } : null) : state.chosen;
      q('[data-transfer-title]').textContent = step === 'confirm' ? t('Transfer ownership to {0}?', chosen.name) : step === 'unsure' ? t('We couldn’t confirm the transfer') : step === 'done' ? t('Ownership transferred') : t('Transfer ownership');
      q('[data-transfer-form]').hidden = step !== 'form';
      q('[data-transfer-confirm-field]').hidden = step !== 'confirm';
      const pair = chosen ? [[chosen.name, chosen.role, t('Super Admin')], [t('{0} (you)', actorName), t('Super Admin'), t('Admin')]] : [];
      q('[data-transfer-change]').hidden = !(chosen && step !== 'unsure') ;
      if (step === 'done') rows([[state.done.destination, state.done.before, t('Super Admin')], [t('{0} (you)', actorName), t('Super Admin'), t('Admin')]], t('Before'), t('Now'));
      else if (chosen) rows(pair, t('Now'), t('After'));
      points(step === 'form' ? (chosen ? [t('Both accounts are signed out and must sign in again.')] : [])
        : step === 'confirm' ? [t('{0} becomes Super Admin; your account remains an Admin.', chosen.name), t('Both accounts must sign in again. Only {0} could transfer it back.', chosen.name)]
        : step === 'done' ? [t('Your session has ended. Sign in again to continue as an Admin.')] : []);
      if (step === 'confirm') q('[data-transfer-name-hint]').textContent = t('Type {0} to confirm.', chosen.name);
      cancel.hidden = step === 'done';
      cancel.textContent = step === 'confirm' ? t('Back') : step === 'unsure' ? t('Close') : t('Cancel');
      confirm.className = `btn ${step === 'confirm' ? 'btn-danger' : 'btn-primary'}${state.busy ? ' is-busy' : ''}`;
      confirmLabel.textContent = state.busy ? (step === 'unsure' ? t('Checking…') : t('Transferring…')) : step === 'confirm' ? t('Transfer ownership') : step === 'unsure' ? t('Check ownership') : step === 'done' ? t('Sign in again') : t('Continue');
      confirm.querySelector('.spin').hidden = !state.busy;
      confirm.disabled = cancel.disabled = to.disabled = password.disabled = name.disabled = state.busy;
      panel.setAttribute('aria-busy', String(state.busy));
      ui.refreshDirty();
    }
    to.addEventListener('change', () => { fieldError(to, ''); paint(); });
    password.addEventListener('input', () => fieldError(password, ''));
    name.addEventListener('input', () => fieldError(name, ''));
    const back = () => { state.step = 'form'; password.value = ''; name.value = ''; fieldError(name, ''); banner(); paint(); password.focus(); };
    cancel.addEventListener('click', () => { if (state.busy) return; if (state.step === 'confirm') back(); else { state.leaving = true; void modal.close(false); } });
    async function submit() {
      const chosen = state.chosen, typed = name.value.trim();
      if (!typed) { fieldError(name, t('Type the new owner’s username to confirm.')); name.focus(); return; }
      if (typed.toLocaleUpperCase() !== chosen.name.toLocaleUpperCase()) { fieldError(name, t('This doesn’t match {0}.', chosen.name)); name.focus(); return; }
      const body = new FormData();
      body.set('Input.DestinationId', chosen.id); body.set('Input.ExpectedAuthorizationVersion', chosen.version);
      body.set('Input.DestinationUsernameConfirmation', typed); body.set('Input.CurrentPassword', password.value);
      state.busy = true; banner(); paint();
      // The password is never part of the unsaved-input notice.
      const outcome = await ui.busy(() => window.AdminFetch.request('/Admin/Accounts?handler=Transfer', { method: 'POST', body, expect: 'json', draft: { [t('New Super Admin')]: chosen.name }, signal }));
      state.busy = false;
      if (transfer !== state || signal.aborted) return;
      const result = outcome.kind === 'handler' ? outcome.data : null;
      if (outcome.kind === 'session-lost') { paint(); return; }
      if (result?.outcome === 'completed') { state.step = 'done'; state.done = result; password.value = ''; paint(); confirm.focus(); return; }
      if (result?.outcome === 'invalid' && result.field === 'password') { state.step = 'form'; password.value = ''; paint(); fieldError(password, result.message); password.focus(); return; }
      if (result?.outcome === 'invalid' && result.field === 'confirmation') { paint(); fieldError(name, result.message); name.focus(); return; }
      if (result?.outcome === 'recipient') {
        const blank = to.options[0];
        to.replaceChildren(blank, ...result.destinations.map(item => { const node = document.createElement('option'); node.value = item.id; node.dataset.version = item.authorizationVersion; node.dataset.username = item.username; node.dataset.role = item.roleLabel || ''; node.textContent = t('{0} · {1}', item.username, item.roleLabel); return node; }));
        to.value = ''; state.step = 'form'; password.value = ''; name.value = ''; paint();
        fieldError(to, result.disabled ? t('Choose another active website account. {0} was disabled a moment ago.', chosen.name) : t('Choose another active website account. {0} was changed by another administrator a moment ago; choose again to continue.', chosen.name));
        to.focus(); return;
      }
      if (result) { paint(); banner('is-warning', t('Ownership wasn’t transferred.'), result.message || t('Nothing changed. Try again.')); return; }
      state.step = 'unsure'; password.value = ''; paint();
      banner('is-warning', t('Don’t repeat it.'), t('It may have gone through. Check who owns the community now.')); confirm.focus();
    }
    async function checkOwnership() {
      state.busy = true; paint();
      const url = new URL('/Admin/Accounts', location.href); url.searchParams.set('account', state.chosen.id);
      const outcome = await ui.busy(() => window.AdminFetch.request(url.href, { expect: 'html', readback: true, signal, headers: { 'X-Admin-Navigation': 'true' } }), true);
      state.busy = false;
      if (transfer !== state || signal.aborted) return;
      if (outcome.kind !== 'handler') { paint(); return; }
      const source = new DOMParser().parseFromString(outcome.data, 'text/html').querySelector('template[data-account-drawer]');
      if (source?.dataset.accountRole === 'superadmin') { state.step = 'done'; state.done = { destination: state.chosen.name, before: state.chosen.role }; paint(); confirm.focus(); return; }
      // A successful read means this session is still valid, so the owner's role did not change.
      state.step = 'form'; paint(); banner('is-info', t('It didn’t go through.'), t('You’re still the Super Admin. Nothing changed.'));
    }
    confirm.addEventListener('click', async () => {
      if (state.busy) return;
      if (state.step === 'form') {
        const chosen = option(), missingTo = !chosen, missingPassword = !password.value;
        fieldError(to, missingTo ? t('Choose an account.') : ''); fieldError(password, missingPassword ? t('Enter your current password.') : '');
        if (missingTo || missingPassword) { (missingTo ? to : password).focus(); return; }
        // A6: the confirmation keeps the selected account's version and role as its basis.
        state.chosen = { id: chosen.value, name: chosen.dataset.username, role: chosen.dataset.role, version: chosen.dataset.version };
        state.step = 'confirm'; banner(); paint(); name.focus(); return;
      }
      if (state.step === 'confirm') { await submit(); return; }
      if (state.step === 'unsure') { await checkOwnership(); return; }
      if (state.step === 'done') {
        state.leaving = true; await modal.close(true);
        const login = new URL('/Account/Login', location.href); login.searchParams.set('ReturnUrl', '/Admin/Accounts');
        location.assign(login.href);
      }
    });
    panel.addEventListener('keydown', event => { if (event.key === 'Enter' && event.target.matches('input')) { event.preventDefault(); confirm.click(); } });
    paint(); to.focus();
  }

  /* ---------------- URL state ---------------- */
  const sameDirectory = (a, b) => { const x = new URL(a), y = new URL(b); x.searchParams.delete('account'); y.searchParams.delete('account'); return x.pathname.toLowerCase() === y.pathname.toLowerCase() && x.search === y.search; };
  const unregisterUrl = ui.registerUrlState(async (next, previous) => {
    const nextUrl = new URL(next);
    if (!/^\/admin\/accounts(\/index)?$/i.test(nextUrl.pathname)) return false;
    const account = nextUrl.searchParams.get('account');
    // Back/Forward close stacked dialogs first; the shell already asked about unsaved input.
    if (confirmModal && !await confirmModal.close(false)) return false;
    if (transfer) { transfer.leaving = true; if (!await transfer.modal.close(false)) return false; }
    if (!sameDirectory(next, previous)) {
      if (drawer) await closeDrawer({ history: false });
      search.value = nextUrl.searchParams.get('q') || '';
      await readDirectory(next);
      if (account) await openDrawer(account, { opener: search });
      return true;
    }
    if (account && drawer?.id !== account) await openDrawer(account, { opener: search });
    else if (!account && drawer) await closeDrawer({ history: false });
    return true;
  });

  // Canonical URL for this render; a direct ?account= link opens its drawer (A4: also after a retry).
  const initial = root.querySelector('template[data-account-drawer]');
  const requested = new URL(location.href).searchParams.get('account');
  query = new URL(root.dataset.directoryCanonical, location.href);
  setUrl(requested ? directoryUrl(requested) : query.href);
  if (initial) void openDrawer(initial.dataset.accountId || requested, { opener: search, source: initial });
  release = () => {
    life.abort(); clearTimeout(timer); overflow.disconnect(); cancelAnimationFrame(overflowFrame); unregisterUrl();
    drawer = null; transfer = null; reasonDrafts.clear();
  };
}

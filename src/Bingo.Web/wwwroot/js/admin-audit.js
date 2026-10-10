// Audit (WA-5): read-only history. Server-owned filters, page and entry live in the URL; list
// changes use the shared in-page update helper (skeleton timing, C-CMP-2, focus/scroll) and push
// a history entry like the reference. Entries open in the shared drawer layer from server-rendered
// templates, so opening and Newer/Older never refetch.
let release;
export function dispose() { release?.(); release = null; }
export function init(region, ui = window.AdminUI) {
  dispose();
  const root = region.querySelector('[data-audit-directory]');
  if (!root) return;
  const life = new AbortController(), signal = life.signal;
  const listen = (node, type, fn, options) => node?.addEventListener(type, fn, { signal, ...options });
  const results = root.querySelector('[data-audit-results]');
  const actor = root.querySelector('[data-audit-actor]');
  const actorError = root.querySelector('[data-audit-actor-err]');
  const importChildren = (target, source) => target.replaceChildren(...document.importNode(source, true).childNodes);
  let query = new URL(root.dataset.directoryCanonical, location.href);
  const schema = values => Object.fromEntries(Object.keys(values).map(key => [key, { valid: () => true, default: '' }]));
  const setUrl = (url, record = false) => { const values = Object.fromEntries(new URL(url, location.href).searchParams); ui.setUrl(values, schema(values), { record }); };
  const listUrl = (entry = drawer?.id) => { const url = new URL(query.href); url.searchParams.delete('entry'); if (entry) url.searchParams.set('entry', entry); return url.href; };

  /* ---------------- list ---------------- */
  function patch(doc) {
    const fresh = doc.querySelector('[data-audit-directory]');
    if (!fresh) throw new Error('Missing audit history');
    importChildren(results, fresh.querySelector('[data-audit-results]'));
    for (const selector of ['[data-audit-footer]', '[data-audit-fragments]', '[data-audit-menus]', '[data-audit-direct]']) importChildren(root.querySelector(selector), fresh.querySelector(selector));
    for (const selector of ['[data-audit-event-button]', '[data-audit-action-button]', '[data-audit-more]']) {
      const button = root.querySelector(selector), next = fresh.querySelector(selector);
      importChildren(button, next); button.className = next.className;
    }
    root.dataset.directoryCanonical = fresh.dataset.directoryCanonical;
    query = new URL(fresh.dataset.directoryCanonical, location.href);
    if (document.activeElement !== actor) actor.value = query.searchParams.get('actor') || '';
    paintActor();
    return listUrl();
  }
  const fragment = failed => {
    const content = document.querySelector(`template[data-page-${failed ? 'failure' : 'loading'}-template="audit"]`).content, nodes = document.createDocumentFragment();
    for (const node of content.querySelectorAll(failed ? '.empty' : '.sk-row')) nodes.append(document.importNode(node, true));
    return nodes;
  };
  // Brief 147 item 4: the actor search applies while typing, like Participants: each input
  // supersedes the pending update and waits 250 ms; a response for an older query is ignored.
  let timer, edits = 0;
  function readList(target, { record = true } = {}) {
    closePanel(false);
    const url = new URL(target, location.href), revision = edits; url.searchParams.delete('entry');
    query = new URL(url.href);
    if (record) setUrl(url.href, true);
    return ui.update(url.href, { root, results, patch, pending: () => fragment(false), failed: () => fragment(true), signal, current: () => edits === revision,
      fallbackFocus: () => actor, scrollRegions: [root.querySelector('[data-audit-wrap]')],
      draft: () => ({ [actor.getAttribute('aria-label')]: actor.value }) });
  }
  function paintActor(message = '') {
    actor.closest('.search').classList.toggle('has-clear', actor.value.length > 0);
    root.querySelector('[data-audit-actor-clear]').hidden = !actor.value.length;
    actorError.hidden = !message; actorError.querySelector('[data-component-text]').textContent = message;
    actor.classList.toggle('is-invalid', !!message); actor.setAttribute('aria-invalid', String(!!message));
  }
  // Review 156 M2: compare with the actor the list actually shows (only patch() moves the canonical
  // URL) or with the search still in flight; typing cancels that search, so it is then forgotten.
  const appliedActor = () => new URL(root.dataset.directoryCanonical, location.href).searchParams.get('actor') || '';
  let inflight = null;
  function commitActor() {
    // C-AUD-3: a leading "@" is ignored; applies 250 ms after typing stops, on Enter or when leaving the field.
    clearTimeout(timer);
    const value = actor.value.trim().replace(/^@\s*/, '');
    if (value.length > 100) { paintActor(root.dataset.actorLengthError); return; }
    paintActor();
    if (value === (inflight ?? appliedActor())) return;
    const url = new URL(query.href); url.searchParams.delete('page');
    if (value) url.searchParams.set('actor', value); else url.searchParams.delete('actor');
    // Review 156 M1: like Participants, a search replaces the URL and adds no history entry.
    const mine = inflight = value;
    void readList(url.href, { record: false }).finally(() => { if (inflight === mine) inflight = null; });
  }
  listen(actor, 'input', () => { edits++; inflight = null; ui.supersedeUpdate(); clearTimeout(timer); paintActor(); timer = setTimeout(commitActor, 250); });
  listen(actor, 'keydown', event => {
    if (event.key === 'Enter') { event.preventDefault(); commitActor(); }
    else if (event.key === 'Escape' && (actor.value !== appliedActor() || inflight !== null)) {
      // Escape restores the value the list shows and re-reads it when another search is pending.
      event.preventDefault(); clearTimeout(timer); actor.value = appliedActor(); paintActor(); commitActor();
    }
  });
  listen(actor, 'change', commitActor);
  listen(root.querySelector('[data-audit-actor-clear]'), 'click', () => { actor.value = ''; actor.focus({ preventScroll: true }); commitActor(); });
  listen(region, 'click', event => {
    const control = event.target.closest('[data-audit-url]');
    if (control) {
      event.preventDefault();
      if (control.closest('.menu')) ui.closeMenu(false);
      if (control.hasAttribute('data-audit-clear-all')) actor.value = '';
      void readList(control.dataset.auditUrl);
      return;
    }
    if (event.target.closest('[data-audit-specific]')) { ui.closeMenu(false); openPanel(); return; }
    if (event.target.closest('[data-audit-more]')) { if (panel()?.hidden === false) void requestClosePanel(true); else openPanel(); return; }
    if (event.target.closest('[data-audit-notice-dismiss]')) { event.target.closest('.banner')?.remove(); actor.focus({ preventScroll: true }); return; }
    const link = event.target.closest('[data-audit-open]');
    if (link && (event.button !== 0 || event.metaKey || event.ctrlKey || event.shiftKey || event.altKey)) return;
    const row = event.target.closest('[data-audit-row]');
    if (link || row && !event.target.closest('a,button')) { event.preventDefault(); void openDrawer((link || row).dataset.auditOpen || row.dataset.auditEntry, { push: true }); }
  });
  const wrap = root.querySelector('[data-audit-wrap]');
  let overflowFrame;
  const overflow = new ResizeObserver(() => { cancelAnimationFrame(overflowFrame); overflowFrame = requestAnimationFrame(() => wrap.classList.toggle('is-scroll', wrap.scrollWidth > wrap.clientWidth + 1)); });
  overflow.observe(wrap);

  /* ---------------- More filters panel ---------------- */
  const panel = () => root.querySelector('[data-audit-panel]');
  const moreButton = () => root.querySelector('[data-audit-more]');
  const monthNames = root.dataset.monthNames.split('|').map(pair => pair.split(',').map(value => value.toLocaleLowerCase()));
  const english = ['jan', 'feb', 'mar', 'apr', 'may', 'jun', 'jul', 'aug', 'sep', 'oct', 'nov', 'dec'];
  function parseDate(text) {
    // Accepts "27 May 2027" (current language or English) or 2027-05-27; returns yyyy-mm-dd, '' or null.
    const value = text.trim(); if (!value) return '';
    let year, month, day;
    const isoMatch = /^(\d{4})-(\d{2})-(\d{2})$/.exec(value);
    if (isoMatch) [year, month, day] = [Number(isoMatch[1]), Number(isoMatch[2]), Number(isoMatch[3])];
    else {
      const match = /^(\d{1,2})\.?\s+([^\s\d.]+)\.?\s+(\d{4})$/u.exec(value); if (!match) return null;
      const name = match[2].toLocaleLowerCase();
      const index = monthNames.findIndex(([short, long]) => name === short || name === long || (name.length >= 3 && long.startsWith(name)));
      const fallback = english.findIndex(short => name.startsWith(short));
      month = (index >= 0 ? index : fallback) + 1; day = Number(match[1]); year = Number(match[3]);
      if (month < 1) return null;
    }
    const date = new Date(Date.UTC(year, month - 1, day));
    if (date.getUTCFullYear() !== year || date.getUTCMonth() !== month - 1 || date.getUTCDate() !== day || year < 1900) return null;
    return `${year}-${String(month).padStart(2, '0')}-${String(day).padStart(2, '0')}`;
  }
  let panelState = null;
  function placePanel() {
    const node = panel(), bounds = moreButton().getBoundingClientRect(), width = Math.min(320, innerWidth - 16);
    const x = Math.max(8, Math.min(innerWidth - width - 8, bounds.right - width)), y = bounds.bottom + 6;
    // A4: the panel stays within the viewport; it moves up when the space below is short and its
    // body scrolls when the viewport itself is short.
    node.style.left = `${Math.round(x)}px`;
    node.style.maxHeight = `${Math.round(innerHeight - 16)}px`;
    node.style.top = `${Math.round(Math.max(8, Math.min(y, innerHeight - 8 - node.offsetHeight)))}px`;
  }
  function paintPresets() {
    const node = panel(); if (!node) return;
    for (const preset of node.querySelectorAll('[data-audit-preset]')) {
      const on = node.querySelector('#fp-from').value.trim() === preset.dataset.from && node.querySelector('#fp-to').value.trim() === preset.dataset.to;
      preset.setAttribute('aria-pressed', String(on)); preset.classList.toggle('is-on', on);
    }
  }
  function dateError(message, fields = []) {
    const node = panel(), error = node.querySelector('#fp-date-err');
    error.hidden = !message; error.querySelector('[data-component-text]').textContent = message || '';
    for (const id of ['fp-from', 'fp-to']) { const input = node.querySelector('#' + id), bad = fields.includes(id); input.classList.toggle('is-invalid', bad); input.setAttribute('aria-invalid', String(bad)); }
  }
  function openPanel() {
    const node = panel(); if (!node || !node.hidden) return;
    ui.closeMenu(false);
    panelState = { opener: moreButton(), values: [...node.querySelectorAll('select,input')].map(input => input.value) };
    node.hidden = false; node.dataset.pageFamily = 'audit';
    moreButton().setAttribute('aria-expanded', 'true');
    placePanel(); paintPresets(); dateError();
    node.querySelector('#fp-action').focus();
  }
  const panelInputs = () => [...panel().querySelectorAll('select,input')];
  const panelDirty = () => { const node = panel(); return !!node && !node.hidden && !!panelState && panelInputs().some((input, index) => input.value !== panelState.values[index]); };
  let confirming = false, swallowClick = false;
  // T1 review M2 (decision B / A12): with unapplied edits, Escape or an outside click asks with the
  // shared discard confirmation (as the drawers do) instead of silently dropping them.
  async function requestClosePanel(restore) {
    if (confirming) return;
    if (panelDirty()) {
      confirming = true;
      const discard = await ui.confirmDiscard();
      confirming = false;
      if (!discard) { (panel().contains(document.activeElement) ? document.activeElement : panel().querySelector('#fp-action'))?.focus({ preventScroll: true }); return; }
    }
    closePanel(restore);
  }
  function closePanel(restore) {
    const node = panel(); if (!node || node.hidden) return;
    // Closing resets the fields to the applied filters (after the discard choice when edited).
    const inputs = [...node.querySelectorAll('select,input')];
    panelState?.values.forEach((value, index) => { inputs[index].value = value; });
    node.hidden = true; moreButton().setAttribute('aria-expanded', 'false'); dateError();
    if (restore) (panelState?.opener?.isConnected ? panelState.opener : moreButton()).focus({ preventScroll: true });
    panelState = null;
  }
  function applyPanel() {
    const node = panel(), fromText = node.querySelector('#fp-from').value, toText = node.querySelector('#fp-to').value;
    const from = parseDate(fromText), to = parseDate(toText);
    if (from === null || to === null) { dateError(root.dataset.dateFormatError, [from === null ? 'fp-from' : '', to === null ? 'fp-to' : ''].filter(Boolean)); node.querySelector(from === null ? '#fp-from' : '#fp-to').focus(); return; }
    if (from && to && from > to) { dateError(root.dataset.dateOrderError, ['fp-from', 'fp-to']); node.querySelector('#fp-from').focus(); return; }
    const url = new URL(query.href); url.searchParams.delete('page');
    const current = url.searchParams.get('action') || '', area = current.endsWith('.') ? current : '';
    const set = (key, value) => value ? url.searchParams.set(key, value) : url.searchParams.delete(key);
    set('action', node.querySelector('#fp-action').value || area);
    set('type', node.querySelector('#fp-type').value);
    set('from', from); set('to', to);
    panelState = null; node.hidden = true; moreButton().setAttribute('aria-expanded', 'false');
    moreButton().focus({ preventScroll: true });
    void readList(url.href);
  }
  listen(root, 'click', event => {
    if (event.target.closest('[data-audit-panel-apply]')) applyPanel();
    else if (event.target.closest('[data-audit-panel-reset]')) { const node = panel(); node.querySelector('#fp-action').value = ''; node.querySelector('#fp-type').value = ''; node.querySelector('#fp-from').value = ''; node.querySelector('#fp-to').value = ''; dateError(); paintPresets(); }
    else { const preset = event.target.closest('[data-audit-preset]'); if (preset) { const node = panel(); node.querySelector('#fp-from').value = preset.dataset.from; node.querySelector('#fp-to').value = preset.dataset.to; dateError(); paintPresets(); } }
  });
  listen(root, 'input', event => { if (event.target.closest('[data-audit-panel]')) paintPresets(); });
  listen(root, 'keydown', event => {
    if (!event.target.closest('[data-audit-panel]')) return;
    if (event.key === 'Escape') { event.preventDefault(); event.stopPropagation(); void requestClosePanel(true); }
    else if (event.key === 'Enter' && event.target.tagName === 'INPUT') { event.preventDefault(); applyPanel(); }
  });
  listen(document, 'pointerdown', event => {
    const node = panel();
    if (!node || node.hidden || confirming || node.contains(event.target) || event.target.closest('[data-audit-more], [data-modal-host]')) return;
    // An outside click that has to ask first must not also act on what it hit.
    if (panelDirty()) swallowClick = true;
    void requestClosePanel(false);
  });
  listen(document, 'click', event => { if (swallowClick) { swallowClick = false; if (!event.target.closest('[data-modal-host]')) { event.preventDefault(); event.stopPropagation(); } } }, { capture: true });
  // Leaving the page with unapplied panel edits uses the shared dirty guard too.
  const unregisterPanelDraft = ui.registerDraft(root, { isDirty: panelDirty, discard: () => closePanel(false) });
  listen(window, 'resize', () => { const node = panel(); if (node && !node.hidden) placePanel(); });

  /* ---------------- entry drawer ---------------- */
  let drawer = null;
  const detail = id => root.querySelector(`template[data-audit-detail="${CSS.escape(id)}"]`);
  const rowIds = () => [...results.querySelectorAll('[data-audit-row]')].map(row => row.dataset.auditEntry);
  function markSelected() { for (const row of results.querySelectorAll('[data-audit-row]')) row.classList.toggle('is-selected', row.dataset.auditEntry === drawer?.id); }
  function fill(id) {
    const source = detail(id) || detail('missing');
    drawer.element.replaceChildren(document.importNode(source.content, true));
    drawer.element.setAttribute('aria-labelledby', 'drawer-title'); drawer.element.removeAttribute('aria-label');
    const ids = rowIds(), index = ids.indexOf(id);
    const newer = drawer.element.querySelector('[data-audit-step="-1"]'), older = drawer.element.querySelector('[data-audit-step="1"]');
    if (newer) newer.disabled = index <= 0;
    if (older) older.disabled = index < 0 || index >= ids.length - 1;
    markSelected();
  }
  async function openDrawer(id, { push = false } = {}) {
    if (drawer?.id === id) return;
    if (drawer && !await closeDrawer({ history: false })) return;
    if (push) setUrl(listUrl(id), true);
    const opener = root.querySelector(`#open-${CSS.escape(id)}`) || document.activeElement;
    const state = drawer = { id, pushed: push, silent: false };
    const layer = ui.openLayer({ kind: 'drawer', title: '', content: document.createDocumentFragment(), opener,
      onClose: async (_result, { navigating } = {}) => {
        if (drawer === state) drawer = null;
        markSelected();
        if (navigating) return;
        if (!state.silent) { if (state.pushed) await ui.backUrl(); else setUrl(listUrl(null)); }
        // Focus returns to the row's action, or the first row's, or the actor search.
        (root.querySelector(`#open-${CSS.escape(state.id)}`) || root.querySelector('[data-audit-open]') || actor).focus({ preventScroll: true });
      } });
    state.layer = layer; state.element = layer.element;
    layer.element.classList.add('is-wide'); layer.element.dataset.pageFamily = 'audit';
    fill(id);
    layer.element.querySelector('#drawer-close')?.focus({ preventScroll: true });
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
    if (event.target.closest('[data-audit-drawer-close]')) { void ui.closeLayer(); return; }
    const step = event.target.closest('[data-audit-step]');
    if (step) {
      // Newer / Older replace the URL so they don't fill the history.
      const ids = rowIds(), next = ids[ids.indexOf(drawer.id) + Number(step.dataset.auditStep)];
      if (!next) return;
      drawer.id = next; setUrl(listUrl(next)); fill(next);
      drawer.element.querySelector('#drawer-body').scrollTop = 0;
      const again = drawer.element.querySelector(`[data-audit-step="${step.dataset.auditStep}"]`);
      (again && !again.disabled ? again : drawer.element.querySelector('#drawer-close')).focus({ preventScroll: true });
      return;
    }
    const tech = event.target.closest('[data-audit-tech]');
    if (tech) { const open = tech.getAttribute('aria-expanded') !== 'true'; tech.setAttribute('aria-expanded', String(open)); tech.classList.toggle('is-open', open); drawer.element.querySelector('#dr-tech-body').hidden = !open; return; }
    const clamp = event.target.closest('[data-audit-clamp]');
    if (clamp) {
      const row = clamp.closest('.chg-row'), open = clamp.getAttribute('aria-expanded') !== 'true';
      row.querySelectorAll('.chg-val').forEach(value => value.classList.toggle('is-clamped', !open));
      clamp.setAttribute('aria-expanded', String(open)); clamp.textContent = open ? clamp.dataset.less : clamp.dataset.more;
    }
  });

  /* ---------------- URL state (Back / Forward) ---------------- */
  const sameList = (a, b) => { const x = new URL(a), y = new URL(b); x.searchParams.delete('entry'); y.searchParams.delete('entry'); return x.pathname.toLowerCase() === y.pathname.toLowerCase() && x.search === y.search; };
  const unregisterUrl = ui.registerUrlState(async (next, previous) => {
    const nextUrl = new URL(next);
    if (!/^\/admin\/audit(\/index)?$/i.test(nextUrl.pathname)) return false;
    // Back closes an open menu or the filter panel first, then follows the URL.
    ui.closeMenu(false); closePanel(false);
    const entry = nextUrl.searchParams.get('entry');
    if (!sameList(next, previous)) {
      if (drawer) await closeDrawer({ history: false });
      actor.value = nextUrl.searchParams.get('actor') || '';
      await readList(next, { record: false });
      if (entry) await openDrawer(entry);
      return true;
    }
    if (entry && drawer?.id !== entry) await openDrawer(entry);
    else if (!entry && drawer) await closeDrawer({ history: false });
    return true;
  });

  // Canonical URL for this render (dropped link parts are replaced); a direct entry link opens its drawer.
  const requested = root.querySelector('[data-audit-requested]')?.dataset.auditRequested;
  const requestedId = new URL(location.href).searchParams.get('entry');
  setUrl(requestedId ? listUrl(requested && requested !== 'missing' ? requested : requestedId) : query.href);
  if (requested) void openDrawer(requested === 'missing' ? (requestedId || 'missing') : requested);
  root.querySelector('#au-notice')?.focus({ preventScroll: true });
  release = () => { life.abort(); unregisterPanelDraft(); overflow.disconnect(); cancelAnimationFrame(overflowFrame); unregisterUrl(); drawer = null; };
}

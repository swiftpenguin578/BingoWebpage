// Catalogue (WA-5, T2). Directory filtered in place (all activities are loaded), one activity drawer
// (?activity=, ?new=1), one drop editor at a time (&drop=), Add drop, the S10 deactivate confirmations,
// D7/D9 named-activity confirmations and Super Admin deletion with its dependency check.
// The shell owns transport classification (AdminFetch, C-CMP-2), busy timing (AdminUI.busy), layers,
// history and dirty guards. This module never repeats a write on its own: unknown outcomes offer a readback.
let release;
export function dispose() { release?.(); release = null; }
export function init(region, ui = window.AdminUI) {
  dispose();
  const root = region.querySelector('[data-catalogue-directory]');
  if (!root) return;
  const labels = JSON.parse(root.dataset.catalogueLabels || '{}');
  const t = (key, ...args) => (labels[key] ?? key).replace(/\{(\d)\}/g, (_m, i) => args[i] ?? '');
  const life = new AbortController(), signal = life.signal;
  const listen = (node, type, fn, options = {}) => node?.addEventListener(type, fn, { signal, ...options });
  const superAdmin = root.dataset.superAdmin === 'true';
  const decimal = root.dataset.decimal || '.';
  const lang = document.documentElement.lang || 'en';
  const CATS = ['Boss', 'Skilling boss', 'Minigame'];
  const search = root.querySelector('[data-catalogue-search]');
  let timer;

  // Paint functions run on input/change; rewriting an unchanged label would replace the text node under the
  // pointer, and WebKit then drops the click that a blur-triggered change interrupted (T2 review M1, case 2).
  const setText = (node, text) => { if (node && node.textContent !== text) node.textContent = text; };
  /* ---------------- numbers and rates (DropRateParser and EhbCalculator, mirrored for previews) ---------------- */
  const fmt = (n, d = 2) => n.toLocaleString(lang, { maximumFractionDigits: d });
  function parseNumber(v) {
    if (!v) return null; v = v.replace(/ /g, '');
    if (/^\d{1,3}(,\d{3})+$/.test(v)) v = v.replace(/,/g, ''); else if (/^\d{1,3}(\.\d{3})+$/.test(v)) v = v.replace(/\./g, ''); else v = v.replace(',', '.');
    const n = parseFloat(v); return isFinite(n) && /^\d+(\.\d+)?$/.test(v) ? n : null;
  }
  function parseRate(text) {
    const m = /^(?:(\d+(?:[.,]\d+)?)\s*[x×*]\s*)?(\d+(?:[.,]\d+)?)\s*\/\s*([\d., ]+?)(?:\s*[x×*]\s*(\d+)\s*(?:rolls?)?)?$/i.exec((text || '').trim());
    if (!m) return null;
    const pre = parseNumber(m[1]), suf = parseNumber(m[4]); if (pre != null && suf != null) return null;
    const mult = pre != null ? pre : suf, a = parseNumber(m[2]), b = parseNumber(m[3]);
    if (!(a > 0) || !(b > 0)) return null;
    const p = a / b; if (p > 1) return null;
    if (mult == null) return { p, rolls: 1 };
    if (!(mult > 0) || mult !== Math.trunc(mult) || mult > 10000) return null;
    return { p, rolls: mult };
  }
  const perKill = (p, rolls) => p > 0 && p <= 1 && rolls >= 1 ? 1 - Math.pow(1 - p, rolls) : null;
  const ehbOf = (rate, p, rolls) => { const c = perKill(p, rolls); return rate > 0 && c > 0 ? 1 / (rate * c) : null; };
  const oneIn = c => fmt(Math.round(1 / c), 0);
  const ehbText = e => fmt(e, e < 10 ? 2 : 1);
  const activityRateNumber = text => { const v = (text || '').trim(); return /^\d+([.,]\d+)?$/.test(v) ? parseFloat(v.replace(',', '.')) : null; };
  const norm = s => (s || '').trim().toUpperCase();
  // OsrsWikiImageUrl.Normalize, mirrored so a readback compares what the server stores.
  const escapeData = value => encodeURIComponent(value).replace(/[!'()*]/g, c => '%' + c.charCodeAt(0).toString(16).toUpperCase());
  function normImage(value) {
    const trimmed = (value || '').trim(); if (!trimmed) return '';
    let url; try { url = new URL(trimmed); } catch { return trimmed; }
    if (url.host.toLowerCase() !== 'oldschool.runescape.wiki') return trimmed;
    const after = (text, marker) => text.toLowerCase().startsWith(marker.toLowerCase()) && text.slice(marker.length).trim() ? text.slice(marker.length).trim() : null;
    const fragment = decodeURIComponent(url.hash.replace(/^#/, '')), path = decodeURIComponent(url.pathname);
    const file = after(fragment, '/media/File:') ?? after(path, '/w/File:') ?? after(path, '/wiki/File:') ?? after(path, '/Special:Redirect/file/');
    return file == null ? trimmed : 'https://oldschool.runescape.wiki/w/Special:Redirect/file/' + escapeData(file);
  }
  const gpText = value => value == null ? t('no value yet') : value < 0 ? t('untradeable, 0 gp') : t('value {0} gp', fmt(value, 0));

  /* ---------------- URL state ---------------- */
  const keys = ['q', 'cat', 'status', 'activity', 'drop', 'new'];
  const schema = Object.fromEntries(keys.map(key => [key, { valid: () => true, default: '' }]));
  const filters = (() => { const url = new URL(root.dataset.directoryCanonical, location.href); return { q: url.searchParams.get('q') || '', cat: url.searchParams.get('cat') || '', status: url.searchParams.get('status') === 'inactive' ? 'inactive' : '' }; })();
  const values = (extra = {}) => ({ q: filters.q, cat: filters.cat, status: filters.status, activity: drawer?.id || '', drop: drawer?.editor?.id || '', new: drawer?.add ? '1' : '', ...extra });
  // Any URL write supersedes a pending delayed search write, so the timer can never rewrite a history entry
  // while Back/Forward is travelling (it could otherwise stamp the drawer URL onto the previous entry).
  const writeUrl = (extra, record = false) => { clearTimeout(timer); ui.setUrl(values(extra), schema, { record }); };
  const pageUrl = extra => { const url = new URL('/Admin/Catalogue', location.href); url.search = ui.query.build(values(extra), schema); return url.href; };

  /* ---------------- directory (filtered in place; the reference filters without a reload) ---------------- */
  const rows = () => [...root.querySelectorAll('[data-activity-row]')];
  function applyFilters() {
    const term = filters.q.trim().toLowerCase(), wantActive = filters.status !== 'inactive';
    let shown = 0;
    for (const row of rows()) {
      let visible = (row.dataset.active === 'true') === wantActive && (!filters.cat || row.dataset.category === filters.cat), match = null;
      if (visible && term && !row.dataset.name.toLowerCase().includes(term)) {
        match = (row.dataset.dropNames || '').split('\n').find(name => name.toLowerCase().includes(term)) || null;
        visible = !!match;
      }
      row.hidden = !visible; if (visible) shown++;
      const sub = row.querySelector('.cell-sub');
      sub.textContent = sub.dataset.categoryLabel + (match ? ' · ' + t('matches {0}', match) : '');
      row.classList.toggle('is-selected', row.dataset.activityRow === drawer?.id);
    }
    const total = rows().length, filtered = !!(term || filters.cat);
    const empty = root.querySelector('[data-catalogue-empty]');
    empty.hidden = shown > 0;
    root.querySelector('[data-catalogue-empty-title]').textContent = !total ? t('The catalogue is empty') : filtered ? t('No activities match') : wantActive ? t('No active activities') : t('No inactive activities');
    root.querySelector('[data-catalogue-empty-text]').textContent = !total ? t('Add a boss or activity, then its drops, to give boards EHB estimates.') : filtered ? t('Search looks at activity and drop names. Try another word or category.') : t('Deactivated activities appear here.');
    root.querySelector('[data-catalogue-empty-clear]').hidden = !(filtered && total);
    root.querySelector('[data-catalogue-foot]').textContent = !shown ? '' : filtered ? (shown === 1 ? t('1 activity match') : t('{0} activities match', shown)) : (shown === 1 ? t('1 activity') : t('{0} activities', shown));
    search.closest('.search').classList.toggle('has-clear', !!search.value);
    root.querySelector('[data-catalogue-clear]').hidden = !search.value;
    for (const input of root.querySelectorAll('[data-catalogue-category]')) { input.checked = input.value === filters.cat; input.closest('.seg-opt').classList.toggle('is-on', input.checked); }
    for (const input of root.querySelectorAll('[data-catalogue-status]')) { input.checked = input.value === filters.status; input.closest('.tab').classList.toggle('is-on', input.checked); }
  }
  function setFilter(patch, { delay = false } = {}) {
    Object.assign(filters, patch); applyFilters();
    clearTimeout(timer);
    if (delay) { clearTimeout(timer); timer = setTimeout(() => writeUrl(), 250); } else writeUrl();
  }
  listen(search, 'input', () => setFilter({ q: search.value.slice(0, 100) }, { delay: true }));
  listen(search, 'keydown', event => { if (event.key === 'Escape' && search.value) { event.preventDefault(); search.value = ''; setFilter({ q: '' }); } });
  listen(root.querySelector('[data-catalogue-clear]'), 'click', () => { search.value = ''; setFilter({ q: '' }); search.focus({ preventScroll: true }); });
  listen(root, 'change', event => {
    if (event.target.matches('[data-catalogue-category]')) setFilter({ cat: event.target.value });
    else if (event.target.matches('[data-catalogue-status]')) setFilter({ status: event.target.value });
  });
  listen(root.querySelector('[data-catalogue-empty-clear]'), 'click', () => { search.value = ''; setFilter({ q: '', cat: '' }); search.focus({ preventScroll: true }); });
  listen(region, 'click', event => {
    const add = event.target.closest('[data-catalogue-new]');
    const plain = event.button !== 0 || event.metaKey || event.ctrlKey || event.shiftKey || event.altKey;
    if (add && !add.closest('[data-page-skeleton]')) { if (plain) return; event.preventDefault(); void openDrawer({ add: true, push: true, opener: add }); return; }
    const link = event.target.closest('[data-catalogue-open]');
    if (link && plain) return;
    const row = event.target.closest('[data-activity-row]');
    if (link || row && !event.target.closest('a,button')) {
      event.preventDefault();
      const open = link || row.querySelector('[data-catalogue-open]');
      void openDrawer({ id: open.dataset.catalogueOpen, push: true, opener: open });
    }
  });
  const wrap = root.querySelector('[data-catalogue-wrap]');
  let overflowFrame;
  const overflow = new ResizeObserver(() => { cancelAnimationFrame(overflowFrame); overflowFrame = requestAnimationFrame(() => wrap.classList.toggle('is-scroll', wrap.scrollWidth > wrap.clientWidth + 1)); });
  overflow.observe(wrap);
  listen(wrap, 'scroll', () => wrap.classList.toggle('is-scrolled', wrap.scrollLeft > 2), { passive: true });
  function patchDirectory(doc) {
    const fresh = doc.querySelector('[data-catalogue-directory]');
    if (!fresh) throw new Error('Missing catalogue directory');
    root.querySelector('[data-catalogue-rows]').replaceChildren(...document.importNode(fresh.querySelector('[data-catalogue-rows]'), true).childNodes);
    for (const count of root.querySelectorAll('[data-catalogue-count]')) count.textContent = fresh.querySelector(`[data-catalogue-count="${count.dataset.catalogueCount}"]`)?.textContent ?? count.textContent;
    const summary = region.querySelector('.page-head .summary'), nextSummary = doc.querySelector('.page-head .summary');
    if (summary && nextSummary) summary.replaceChildren(...document.importNode(nextSummary, true).childNodes);
    applyFilters();
  }

  /* ---------------- shared pieces ---------------- */
  function banner(tone, title, text, action) {
    const node = ui.template(tone === 'is-error' ? 'banner-error' : tone === 'is-warning' ? 'banner-warning' : 'banner-info').firstElementChild;
    node.className = `banner ${tone}`; node.tabIndex = -1; node.hidden = false; node.setAttribute('role', 'alert');
    const lead = node.querySelector('[data-component-lead]'); lead.hidden = !title; lead.textContent = title || '';
    node.querySelector('[data-component-text]').textContent = (title ? ' ' : '') + text;
    if (action) {
      const button = document.createElement('button'); button.type = 'button'; button.className = 'btn btn-sm'; button.textContent = action.label;
      button.addEventListener('click', () => action.run(button)); node.append(button);
    }
    return node;
  }
  const dialogBanner = (...args) => { const node = banner(...args); node.classList.add('m-banner'); return node; };
  function showBanner(slot, tone, title, text, action) {
    if (!slot) return null;
    const node = banner(tone, title, text, action);
    if (slot.matches('[data-catalogue-banner-slot]')) { const holder = document.createElement('div'); holder.className = 'ct-dr-banner'; holder.append(node); slot.replaceChildren(holder); }
    else slot.replaceChildren(node);
    node.focus({ preventScroll: true }); node.scrollIntoView({ block: 'nearest' });
    return node;
  }
  function fieldError(scope, id, message) {
    const error = scope.querySelector('#error-' + id), input = scope.querySelector('#' + id);
    if (error) { error.querySelector('[data-component-text]').textContent = message || ''; error.classList.toggle('field-validation-valid', !message); error.classList.toggle('field-validation-error', !!message); }
    if (input) { input.classList.toggle('is-invalid', !!message); input.setAttribute('aria-invalid', String(!!message)); }
  }
  function setBusy(button, busy, label) {
    if (!button) return;
    const text = button.querySelector('[data-component-text]');
    if (busy) { button.dataset.idle = text?.textContent ?? ''; if (text && label) text.textContent = label; }
    else if (text && button.dataset.idle != null) text.textContent = button.dataset.idle;
    button.classList.toggle('is-busy', busy); button.querySelector('.spin')?.toggleAttribute('hidden', !busy);
  }
  const post = (handler, body, draft) => ui.busy(() => window.AdminFetch.request(`/Admin/Catalogue?handler=${handler}`, { method: 'POST', body, expect: 'json', draft, signal }));
  const getJson = url => ui.busy(() => window.AdminFetch.request(url, { expect: 'json', signal, readback: true }), true);
  const form = entries => { const body = new FormData(); for (const [key, value] of Object.entries(entries)) { if (Array.isArray(value)) value.forEach(item => body.append(key, item)); else if (value != null) body.set(key, value); } return body; };
  const resultOf = outcome => outcome.kind === 'handler' ? outcome.data : null;

  // A modal built from the shared confirmation pattern: title, body, actions. Stays open on an outside click.
  function dialog({ title, opener }) {
    const content = document.createDocumentFragment();
    const heading = document.createElement('h2'); heading.className = 'm-title'; heading.dataset.confirmTitle = ''; heading.textContent = title;
    const body = document.createElement('div'); body.dataset.confirmDescription = '';
    const actions = document.createElement('div'); actions.className = 'm-actions';
    content.append(heading, body, actions);
    let busy = false;
    const layer = ui.openLayer({ title, content, confirmation: true, pending: () => busy, opener });
    layer.element.classList.add('is-wide'); layer.element.dataset.pageFamily = 'catalogue';
    const button = (text, cls, run) => { const b = document.createElement('button'); b.type = 'button'; b.className = `btn ${cls}`; b.innerHTML = '<span class="spin" hidden></span><span data-component-text></span>'; b.querySelector('[data-component-text]').textContent = text; b.addEventListener('click', run); return b; };
    return {
      layer, heading, body, actions, button,
      setBusy(value) { busy = value; layer.element.setAttribute('aria-busy', String(value)); actions.querySelectorAll('button').forEach(b => { b.disabled = value; }); },
      close: result => layer.close(result)
    };
  }
  const points = list => { const ul = document.createElement('ul'); ul.className = 'm-points'; for (const item of list) { const li = document.createElement('li'); if (typeof item === 'string') li.textContent = item; else li.append(item); ul.append(li); } return ul; };
  const checking = () => { const p = document.createElement('p'); p.className = 'm-body ct-checking'; p.innerHTML = '<span class="spin"></span>'; p.append(t('Checking what uses it…')); return p; };

  /* ---------------- drawer ---------------- */
  let drawer = null, drawerToken = 0, items = {};
  const $ = selector => drawer?.element.querySelector(selector);
  function drawerShell(name, art, add) {
    const content = document.createDocumentFragment();
    const head = document.createElement('div'); head.className = 'dr-head';
    const thumb = document.createElement('span'); thumb.className = 'thumb is-lg'; thumb.setAttribute('aria-hidden', 'true'); if (art) thumb.setAttribute('style', art);
    const grow = document.createElement('div'); grow.className = 'grow ct-min';
    const eyebrow = document.createElement('div'); eyebrow.className = 'eyebrow'; eyebrow.textContent = t('Catalogue');
    const title = document.createElement('h2'); title.className = 'dr-title'; title.id = 'drawer-title'; title.textContent = add ? t('Add activity') : name || t('Loading…');
    grow.append(eyebrow, title);
    const close = document.createElement('button'); close.type = 'button'; close.className = 'icon-btn'; close.id = 'drawer-close'; close.setAttribute('aria-label', t('Close')); close.dataset.catalogueClose = '';
    close.append(ui.template('toast').querySelector('.toast-x svg').cloneNode(true));
    head.append(thumb, grow, close);
    const body = document.createElement('div'); body.className = 'dr-body'; body.id = 'drawer-body';
    const foot = document.createElement('div'); foot.className = 'dr-foot';
    const spacer = document.createElement('div'); spacer.className = 'spacer';
    const done = document.createElement('button'); done.type = 'button'; done.className = 'btn'; done.id = 'dr-cancel'; done.dataset.catalogueClose = ''; done.textContent = t('Close');
    foot.append(spacer, done);
    content.append(head, body, foot);
    return { content, body };
  }
  async function openDrawer({ id = null, add = false, push = false, opener = document.activeElement, source = null, drop = null }) {
    if (drawer && (add ? drawer.add : drawer.id === id)) return;
    if (drawer && !await closeDrawer({ history: false })) return;
    const row = id ? root.querySelector(`[data-activity-row="${CSS.escape(id)}"]`) : null;
    const { content, body } = drawerShell(row?.querySelector('[data-catalogue-open]')?.textContent, row?.querySelector('.thumb')?.getAttribute('style'), add);
    const state = drawer = { id, add, pushed: push, opener, busy: null, editor: null, addForm: null, unresolved: null, body, silent: false };
    if (push) writeUrl({ activity: id || '', new: add ? '1' : '', drop: '' }, true);
    drawerToken++;
    // The layer's own input baseline cannot follow content replaced in place, so the page answers
    // every leave request itself: closing a drawer with no real edits never asks (decision B).
    const layer = ui.openLayer({ kind: 'drawer', title: add ? t('Add activity') : t('Activity'), content, opener,
      pending: () => !!state.busy, confirmLeave: () => confirmLeave(state),
      onClose: async (_result, { navigating } = {}) => {
        if (drawer === state) { drawer = null; drawerToken++; unregisterDraft?.(); unregisterDraft = null; }
        applyFilters();
        if (navigating) return;
        if (!state.silent) { if (state.pushed) await ui.backUrl(); else writeUrl({ activity: '', drop: '', new: '' }); }
        (root.querySelector(`#open-${CSS.escape(id || '')}`) || (add ? document.querySelector('#add-activity') : null) || search).focus({ preventScroll: true });
      } });
    state.layer = layer; state.element = layer.element;
    layer.element.classList.add('is-wide'); layer.element.dataset.pageFamily = 'catalogue';
    layer.element.setAttribute('aria-labelledby', 'drawer-title'); layer.element.removeAttribute('aria-label');
    // Navigation (sidebar, Back) asks through the same check; nothing is discarded silently.
    unregisterDraft = ui.registerDraft(state, { isDirty: () => drawer === state, confirmLeave: () => confirmLeave(state) });
    applyFilters();
    if (source) { install(source); return; }
    await loadDrawer(state, drop);
  }
  let unregisterDraft = null;
  function loadDrawer(state, drop = null) {
    const token = drawerToken;
    return ui.update(pageUrl({ activity: state.id || '', new: state.add ? '1' : '', drop: drop || '' }), { root, results: state.body, signal,
      patch: doc => {
        const source = doc.querySelector('template[data-catalogue-drawer]');
        if (!source || token !== drawerToken || drawer !== state) throw new Error('Missing or superseded activity drawer');
        patchDirectory(doc); install(source);
        return pageUrl();
      },
      pending: () => document.importNode(root.querySelector('template[data-catalogue-drawer-pending]').content, true),
      failed: () => document.importNode(root.querySelector('template[data-catalogue-drawer-failed]').content, true),
      fallbackFocus: () => $('#drawer-close'),
      current: () => token === drawerToken && drawer === state });
  }
  async function fetchPage(extra) {
    const outcome = await window.AdminFetch.request(pageUrl(extra), { expect: 'html', signal, readback: true, headers: { 'X-Admin-Navigation': 'true' } });
    return outcome.kind === 'handler' ? new DOMParser().parseFromString(outcome.data, 'text/html') : null;
  }
  // RC10 C2 / decision B: every form keeps its own baseline. A re-read after a save (or a check) installs the
  // server's values and then puts back every *other* part's unsaved entry; only the part named in `saved` takes
  // the server's values. Parts: 'activity', 'map' (Wise Old Man metric), 'drop', 'price', 'add'.
  function captureDrafts() {
    const state = drawer, drafts = {};
    if (!state || state.missing) return drafts;
    if (!state.add && activityDirty()) drafts.activity = activityValues();
    if (metricDirty()) drafts.metric = $('#m-metric').value;
    const e = state.editor;
    if (e) drafts.editor = { id: e.id, values: editorValues(), price: priceValues(), editorDirty: editorDirty(), priceDirty: priceDirty(), useShared: e.useShared };
    if (state.addForm && addFormHasInput()) drafts.add = addFormValues();
    return drafts;
  }
  function restoreDrafts(drafts, saved) {
    const state = drawer;
    if (!state || state.missing) return;
    if (drafts.activity && saved !== 'activity') { setActivityValues(drafts.activity); paintActivity(); }
    if (drafts.metric != null && saved !== 'map' && $('#m-metric')) { $('#m-metric').value = drafts.metric; openDisclosure($('#map-btn'), true); }
    const d = drafts.editor;
    if (d) {
      const keepEditor = d.editorDirty && saved !== 'drop', keepPrice = d.priceDirty && saved !== 'price';
      if ((keepEditor || keepPrice) && state.editor?.id !== d.id) openEditor(d.id, { quiet: true });
      if (state.editor?.id === d.id) {
        if (keepEditor) {
          ed('#e-name').value = d.values.name; ed('#e-rate').value = d.values.rate; ed('#e-img').value = d.values.image;
          if (ed('#e-group')) ed('#e-group').value = d.values.group;
          paintEditor();
          if (d.useShared && ed('#e-use-shared')) { ed('#e-use-shared').checked = true; state.editor.useShared = true; }
        }
        if (keepPrice) {
          const mode = ed(`input[name=priceMode][value="${d.price.mode}"]`); if (mode) mode.checked = true;
          ed('#p-gp').value = d.price.gp; ed('#p-id').value = d.price.id;
          openDisclosure(ed('#price-btn'), true);
        }
        paintEditor();
      }
    }
    if (drafts.add && saved !== 'add' && !state.editor) { openAdd(false); restoreAddForm(drafts.add); }
  }
  async function refresh({ saved = null, editor = null, doc = null } = {}) {
    const state = drawer; if (!state) return false;
    const drafts = captureDrafts();
    doc ??= await fetchPage({ drop: '' });
    if (drawer !== state || !doc) return false;
    const source = doc.querySelector('template[data-catalogue-drawer]');
    patchDirectory(doc);
    if (!source) return false;
    install(source, { openEditor: editor });
    restoreDrafts(drafts, saved);
    return true;
  }
  function install(source, { keep = null, openEditor: editorId = null } = {}) {
    const state = drawer, content = source.content;
    state.missing = source.dataset.missing === 'true';
    state.version = source.dataset.activityVersion; state.name = source.dataset.activityName; state.active = source.dataset.activityActive === 'true';
    state.rate = activityRateNumber(source.dataset.activityRate || ''); state.category = source.dataset.activityCategory;
    state.editor = null; state.addForm = null;
    const panel = state.element;
    panel.querySelector('.dr-head').replaceWith(document.importNode(content.querySelector('.dr-head'), true));
    state.body.replaceChildren(...document.importNode(content.querySelector('.dr-body'), true).childNodes);
    panel.querySelector('.dr-foot').replaceWith(document.importNode(content.querySelector('.dr-foot'), true));
    state.addTemplate = content.querySelector('template[data-catalogue-add-template]');
    try { items = JSON.parse(content.querySelector('script[data-catalogue-items]')?.textContent || '{}'); } catch { items = {}; }
    if (state.missing) return;
    const formNode = $('[data-catalogue-activity-form]');
    state.baseline = activityValues();
    state.metricBase = $('#m-metric')?.value ?? null;
    if (keep) setActivityValues(keep);
    formNode.addEventListener('input', paintActivity); formNode.addEventListener('change', paintActivity);
    formNode.addEventListener('submit', event => event.preventDefault());
    paintActivity();
    const open = editorId || source.dataset.openDrop;
    if (open) openEditor(open, { quiet: true });
  }
  async function closeDrawer({ history = true } = {}) {
    if (!drawer) return true;
    const state = drawer; state.silent = !history;
    const closed = await state.layer.close(false);
    if (!closed) state.silent = false;
    return closed;
  }

  /* ---------------- unsaved work: list exactly what would be discarded ---------------- */
  function dirtyParts() {
    const parts = [];
    if (!drawer || drawer.missing) return parts;
    if (activityDirty()) parts.push(drawer.add ? t('the new activity') : t('the activity settings'));
    if (metricDirty()) parts.push(t('the Wise Old Man metric'));
    parts.push(...editorParts());
    if (drawer.addForm && addFormHasInput()) parts.push(t('the new drop'));
    return parts;
  }
  function editorParts() {
    const parts = [];
    if (drawer?.editor && editorDirty()) parts.push(t('your changes to {0}', drawer.editor.name));
    if (drawer?.editor && priceDirty()) parts.push(t('the value and mapping changes for {0}', drawer.editor.name));
    return parts;
  }
  function metricDirty() { const input = drawer?.element?.querySelector('#m-metric'); return !!input && drawer.metricBase != null && input.value.trim() !== drawer.metricBase.trim(); }
  async function askDiscard(parts) {
    const box = dialog({ title: t('Discard unsaved changes?') });
    box.body.append(points(parts.map(part => t('Discards {0}.', part))));
    return new Promise(resolve => {
      let answered = false;
      const done = value => { if (answered) return; answered = true; void box.close(value).then(() => resolve(value)); };
      box.actions.append(box.button(t('Keep editing'), '', () => done(false)), box.button(t('Discard'), 'btn-danger', () => done(true)));
      box.actions.firstElementChild.focus();
      box.layer.element.addEventListener('keydown', event => { if (event.key === 'Escape') { event.stopPropagation(); done(false); } }, { capture: true });
    });
  }
  async function confirmLeave(state) {
    if (drawer !== state) return true;
    const parts = dirtyParts();
    return !parts.length || await askDiscard(parts);
  }

  /* ---------------- activity settings ---------------- */
  const activityFields = () => ({ name: $('#a-name'), category: $('#a-cat'), rate: $('#a-rate'), team: $('#a-team'), image: $('#a-img') });
  function activityValues() { const f = activityFields(); return f.name ? { name: f.name.value, category: f.category.value, rate: f.rate.value, team: f.team.value, image: f.image.value } : null; }
  function setActivityValues(v) { const f = activityFields(); f.name.value = v.name; f.category.value = v.category; f.rate.value = v.rate; f.team.value = v.team; f.image.value = v.image; }
  function activityDirty() {
    const now = activityValues(); if (!now || !drawer?.baseline) return false;
    if (drawer.add) return !!(now.name.trim() || now.rate.trim() || now.image.trim() || now.team.trim() !== '1' || now.category !== 'Boss');
    return Object.keys(now).some(key => now[key].trim() !== drawer.baseline[key].trim());
  }
  function paintActivity() {
    if (!drawer || drawer.missing) return;
    const dirty = activityDirty(), f = activityFields(), minigame = f.category.value === 'Minigame';
    setText($('[data-catalogue-rate-label]'), minigame ? t('Runs per hour') : t('Kills per hour'));
    setText($('[data-catalogue-rate-hint]'), minigame ? t('Efficient runs or completions in one hour.') : t('Efficient kills in one hour, at the group size the drop rates assume.'));
    const note = $('[data-catalogue-recalc]');
    const rateChanged = !drawer.add && f.rate.value.trim() !== drawer.baseline.rate.trim(), rate = activityRateNumber(f.rate.value);
    note.hidden = !(rateChanged && (rate > 0 || !f.rate.value.trim()));
    note.querySelector('[data-component-text]').textContent = rate > 0
      ? t('Saving recalculates EHB for this activity’s {0} drops and any draft boards using them. Approved boards keep their snapshot.', drawer.element.querySelectorAll('.dlist-item').length)
      : t('Without a rate, its drops have no EHB and draft tiles using them can’t be approved.');
    const head = $('[data-catalogue-act-dirty]'); if (head) head.hidden = !(dirty && !drawer.add);
    const footNote = $('[data-catalogue-foot-note]'); footNote.hidden = !dirty;
    const save = $('[data-catalogue-save]'); save.hidden = !(drawer.add || dirty);
    setText($('[data-catalogue-cancel]'), drawer.add ? t('Cancel') : dirty ? t('Discard') : t('Close'));
    paintLocks();
  }
  function activityErrors() {
    const v = activityValues(), errors = {}, name = v.name.trim();
    if (!name) errors['a-name'] = t('Enter a name.');
    else if (name.length > 200) errors['a-name'] = t('Use 200 characters or fewer.');
    else if (rows().some(row => row.dataset.activityRow !== drawer.id && row.dataset.name.toLowerCase() === name.toLowerCase())) errors['a-name'] = t('An activity named {0} already exists.', name);
    if (!CATS.includes(v.category)) errors['a-cat'] = t('Choose Boss, Skilling boss or Minigame.');
    const rate = v.rate.trim(); if (rate && !(activityRateNumber(rate) > 0 && activityRateNumber(rate) <= 100000)) errors['a-rate'] = t('Enter a number above 0, like 35 or 7.5. Leave it empty if unknown.');
    if (!/^\d+$/.test(v.team.trim()) || parseInt(v.team, 10) < 1) errors['a-team'] = t('Enter a whole number of 1 or more.');
    if (v.image.trim() && !/^https?:\/\/\S+$/.test(v.image.trim())) errors['a-img'] = t('Enter a full link starting with https://.');
    return errors;
  }
  const serverField = { name: 'a-name', category: 'a-cat', rate: 'a-rate', teamSize: 'a-team', image: 'a-img' };
  function showErrors(scope, errors, order) {
    for (const id of order) fieldError(scope, id, errors[id] || '');
    const first = order.find(id => errors[id]); if (first) scope.querySelector('#' + first)?.focus();
    return !!first;
  }
  const invariantRate = text => { const n = activityRateNumber(text); return n == null ? '' : String(n).replace('.', decimal); };
  async function saveActivity() {
    const state = drawer; if (!state || state.busy) return;
    const order = ['a-name', 'a-cat', 'a-rate', 'a-team', 'a-img'];
    if (showErrors(state.element, activityErrors(), order)) return;
    const v = activityValues(), snapshot = { ...v };
    const body = state.add
      ? form({ 'Boss.Name': v.name.trim(), 'Boss.Category': v.category, 'Boss.EfficientRate': invariantRate(v.rate), 'Boss.TeamSize': v.team.trim(), 'Boss.ImageUrl': v.image.trim() })
      : form({ recordId: state.id, expectedVersion: state.version, name: v.name.trim(), category: v.category, efficientRate: invariantRate(v.rate), teamSize: v.team.trim(), imageUrl: v.image.trim() });
    const button = $('[data-catalogue-save]');
    state.busy = 'activity'; setBusy(button, true, state.add ? t('Adding…') : t('Saving…')); paintLocks();
    const outcome = await post(state.add ? 'Boss' : 'UpdateBoss', body, { [t('Name')]: v.name, [t('Category')]: v.category, [t('Kills per hour')]: v.rate, [t('Team size')]: v.team, [t('Image URL')]: v.image });
    state.busy = null; setBusy(button, false); paintLocks();
    if (drawer !== state || outcome.kind === 'session-lost' || signal.aborted) return;
    const result = resultOf(outcome);
    const slot = $('[data-catalogue-banner-slot]');
    if (result?.outcome === 'completed') {
      if (state.add) {
        state.add = false; state.id = result.activityId;
        writeUrl({ activity: state.id, new: '', drop: '' });
        await refresh({ saved: 'activity' }); $('#sec-drops')?.focus({ preventScroll: true });
        ui.toast(t('{0} added. Add its drops next.', result.name)); return;
      }
      await refresh({ saved: 'activity' });
      ui.toast(result.rateChanged ? t('{0} saved. EHB recalculated for its drops.', result.name) : t('{0} saved.', result.name));
      $('#drawer-title')?.focus({ preventScroll: true });
      return;
    }
    if (result?.outcome === 'invalid') { const errors = {}; for (const [key, message] of Object.entries(result.errors || {})) errors[serverField[key] || 'a-name'] = message; showErrors(state.element, errors, order); return; }
    if (result?.outcome === 'stale') { await refresh({ saved: 'activity' }); showBanner($('[data-catalogue-banner-slot]'), 'is-warning', t('Another admin changed this activity.'), t('Your changes weren’t saved. The current values are shown; check them and edit again.')); return; }
    if (result?.outcome === 'refused') { showBanner(slot, 'is-warning', t('That couldn’t be saved.'), result.message); return; }
    if (outcome.status === 404) { await loadDrawer(state); return; }
    showBanner(slot, 'is-warning', t('We couldn’t confirm the save.'), t('It may have gone through. Check the current values before saving again.'),
      { label: t('Check current values'), run: button => void checkActivity(button, snapshot) });
  }
  async function checkActivity(button, snapshot) {
    const state = drawer; button.disabled = true; button.textContent = t('Checking…');
    const created = state.add;
    const doc = await fetchPage(created ? { new: '', activity: '' } : { drop: '' });
    if (drawer !== state) return;
    const slot = $('[data-catalogue-banner-slot]');
    if (!doc) { showBanner(slot, 'is-warning', t('Couldn’t check the current values.'), t('Nothing was repeated. Check again when you’re back online.'), { label: t('Check current values'), run: next => void checkActivity(next, snapshot) }); return; }
    patchDirectory(doc);
    if (created) {
      // Exact name only (RC10 C1: never a partial match).
      const found = rows().find(item => item.dataset.name.toLowerCase() === snapshot.name.trim().toLowerCase());
      const foundDoc = found ? await fetchPage({ new: '', activity: found.dataset.activityRow }) : null;
      if (drawer !== state) return;
      const fd = foundDoc?.querySelector('template[data-catalogue-drawer]')?.dataset;
      const row = fd && fd.activityName === snapshot.name.trim() && fd.activityCategory === snapshot.category
        && activityRateNumber(fd.activityRate || '') === activityRateNumber(snapshot.rate) && (fd.activityTeam || '') === String(parseInt(snapshot.team, 10))
        && normImage(fd.activityImage) === normImage(snapshot.image) ? found : null;
      if (row) { state.add = false; state.id = row.dataset.activityRow; writeUrl({ activity: state.id, new: '' }); await refresh({ saved: 'activity' }); showBanner($('[data-catalogue-banner-slot]'), 'is-info', t('It was added.'), t('The current values are shown.')); }
      else showBanner(slot, 'is-info', t('It wasn’t added.'), t('Your entries are still here; add it again when ready.'));
      return;
    }
    const source = doc.querySelector('template[data-catalogue-drawer]');
    // RC10 C1: "Saved" only when every submitted field matches what the server now has.
    const d = source?.dataset;
    const saved = !!d && d.activityName === snapshot.name.trim() && d.activityCategory === snapshot.category
      && activityRateNumber(d.activityRate || '') === activityRateNumber(snapshot.rate) && (d.activityTeam || '') === String(parseInt(snapshot.team, 10))
      && normImage(d.activityImage) === normImage(snapshot.image);
    if (saved) { await refresh({ saved: 'activity', doc }); showBanner($('[data-catalogue-banner-slot]'), 'is-info', t('Saved.'), t('The current values match what you entered.')); }
    else showBanner(slot, 'is-info', t('It wasn’t saved.'), t('Your entries are still here; save again when ready.'));
  }

  /* ---------------- Wise Old Man metric ---------------- */
  async function mapSuggest(button) {
    const state = drawer, result = $('#m-result');
    state.busy = 'map'; paintLocks(); setBusy(button, true);
    const outcome = await post('SuggestBossApi', form({ itemName: state.name }), { [t('Name')]: state.name });
    state.busy = null; paintLocks(); setBusy(button, false);
    if (drawer !== state || outcome.kind === 'session-lost') return;
    const data = resultOf(outcome);
    result.hidden = false;
    if (data?.id) { $('#m-metric').value = data.id; result.className = 'ct-result'; result.textContent = t('Suggested “{0}”. Validate to save it.', data.id); }
    else {
      result.className = 'ct-result is-warn';
      result.textContent = data?.reason === 'no-match' ? t('No exact match. Enter the metric for this exact activity or raid mode.')
        : data?.reason === 'rate-limited' ? t('Wise Old Man is limiting requests. Nothing changed; try again later.')
        : t('Wise Old Man isn’t responding. Nothing changed; you can still enter the metric yourself.');
    }
  }
  async function mapSave(operation, button) {
    const state = drawer, value = $('#m-metric').value.trim();
    state.busy = 'map'; paintLocks(); setBusy(button, true);
    const outcome = await post('BossApi', form({ recordId: state.id, expectedVersion: state.version, externalIdentifier: value, operation }), { [t('Metric')]: value });
    state.busy = null; paintLocks(); setBusy(button, false);
    if (drawer !== state || outcome.kind === 'session-lost') return;
    const data = resultOf(outcome);
    if (data?.outcome === 'completed') {
      await refresh({ saved: 'map' });
      const status = !value ? 'none' : operation === 'save' ? 'unchecked' : { Verified: 'verified', Unsupported: 'unsupported', TemporarilyUnavailable: 'unavailable' }[data.status] || 'unchecked';
      const text = { verified: t('Saved and verified. Wise Old Man tracks this metric; not every player will have data.'), unsupported: t('Saved. Wise Old Man doesn’t recognise this metric; check the exact activity or raid mode.'), unavailable: t('Saved. Wise Old Man couldn’t be reached to check it; validate again later.'), unchecked: t('Saved without checking.'), none: t('Saved. No metric is set, so events can’t show Wise Old Man activity for it.') }[status];
      openDisclosure($('#map-btn'), true);
      const result = $('#m-result'); result.hidden = false; result.className = `ct-result ${status === 'verified' ? 'is-ok' : status === 'unsupported' || status === 'unavailable' ? 'is-warn' : ''}`; result.textContent = text; result.focus({ preventScroll: true });
      return;
    }
    if (data?.outcome === 'stale') { await refresh({ saved: 'map' }); showBanner($('[data-catalogue-banner-slot]'), 'is-warning', t('Another admin changed this activity.'), t('Your changes weren’t saved. The current values are shown; check them and edit again.')); return; }
    const result = $('#m-result'); result.hidden = false; result.className = 'ct-result is-warn';
    result.textContent = t('We couldn’t confirm the save. Check the current values before saving again.');
  }

  /* ---------------- drop editor (one at a time) ---------------- */
  const itemFor = name => items[norm(name)] || null;
  const usesHere = item => (item?.uses || []).find(use => use.activity === drawer.id) || null;
  async function toggleEditor(id) {
    const state = drawer;
    if (state.editor?.id === id) { const parts = editorParts(); if (parts.length && !await askDiscard(parts)) return; closeEditor(true); return; }
    await openEditorGuarded(id);
  }
  async function openEditorGuarded(id) {
    const state = drawer;
    const parts = [];
    if (state.editor) parts.push(...editorParts());
    if (state.addForm && addFormHasInput()) parts.push(t('the new drop'));
    if (parts.length && !await askDiscard(parts)) return;
    openEditor(id);
  }
  function openEditor(id, { quiet = false } = {}) {
    const state = drawer;
    const template = state.element.querySelector(`template[data-catalogue-drop-editor="${CSS.escape(id)}"]`);
    if (!template) return;
    closeAdd(); closeEditor(false, true);
    const li = template.closest('.dlist-item'), row = li.querySelector('.dlist-row');
    const node = document.importNode(template.content, true).firstElementChild;
    li.append(node); li.classList.add('is-open'); row.setAttribute('aria-expanded', 'true');
    const d = template.dataset;
    const editor = state.editor = { id, li, node, row, version: d.version, itemId: d.itemId, itemVersion: d.itemVersion, name: d.itemName, rate: d.rate, rolls: parseInt(d.rolls, 10) || 1,
      image: d.image || '', group: d.rollGroup, active: d.active === 'true', ehb: d.ehb || '', others: d.others || '', normalized: d.normalized, mode: d.priceMode, value: d.priceValue || '', externalId: d.externalId || '', useShared: false };
    editor.base = editorValues(); editor.priceBase = priceValues();
    node.addEventListener('input', () => paintEditor());
    node.addEventListener('change', () => paintEditor());
    paintEditor();
    if (!quiet) writeUrl({ drop: id });
    if (!quiet || document.activeElement === document.body) node.querySelector('#e-name').focus({ preventScroll: quiet });
    node.scrollIntoView({ block: 'nearest' });
  }
  function closeEditor(focusRow, silent = false) {
    const state = drawer; const editor = state?.editor; if (!editor) return;
    editor.node.remove(); editor.li.classList.remove('is-open'); editor.row.setAttribute('aria-expanded', 'false');
    state.editor = null;
    if (!silent) writeUrl({ drop: '' });
    if (focusRow) editor.row.focus({ preventScroll: true });
    paintLocks();
  }
  const ed = selector => drawer?.editor?.node.querySelector(selector);
  function editorValues() { return { name: ed('#e-name').value, rate: ed('#e-rate').value, image: ed('#e-img').value, group: ed('#e-group')?.value ?? drawer.editor.group }; }
  function priceValues() { return { mode: ed('input[name=priceMode]:checked')?.value || 'Api', gp: ed('#p-gp').value, id: ed('#p-id').value }; }
  function editorDirty() { const e = drawer?.editor; if (!e) return false; const v = editorValues(); return v.name.trim() !== e.base.name.trim() || v.rate.trim() !== e.base.rate.trim() || v.image.trim() !== e.base.image.trim() || v.group.trim() !== e.base.group.trim(); }
  function priceDirty() { const e = drawer?.editor; if (!e) return false; const v = priceValues(); return v.mode !== e.priceBase.mode || (v.mode === 'Manual' && v.gp.trim() !== e.priceBase.gp.trim()) || v.id.trim() !== e.priceBase.id.trim(); }
  function preview(parsed, rolls) {
    const c = perKill(parsed.p, rolls), unit = state => state.category === 'Minigame' ? t('run') : t('kill');
    const e = drawer.rate > 0 ? ehbOf(drawer.rate, parsed.p, rolls) : null;
    return (rolls > 1 ? t('{0} rolls of 1 in {1}', rolls, oneIn(parsed.p)) + ' · ' : '') + t('1 in {0} per {1}', oneIn(c), unit(drawer))
      + (e != null ? ' · ' + t('about {0} EHB', ehbText(e)) : !(drawer.rate > 0) ? ' · ' + t('no EHB until the activity has a rate') : '');
  }
  function editorErrors() {
    const e = drawer.editor, v = editorValues(), errors = {}, name = v.name.trim();
    if (!name) errors['e-name'] = t('Enter the item name.');
    else if (name.length > 200) errors['e-name'] = t('Use 200 characters or fewer.');
    const other = itemFor(name);
    const nameChanged = name !== e.name;
    if (nameChanged && other && other.id !== e.itemId) {
      const here = usesHere(other);
      if (here) errors['e-name'] = t('This activity already has a drop named {0}.', other.name);
      else if (!e.useShared) errors['e-shared'] = t('Confirm using the shared item, or change the name.');
    }
    const rate = v.rate.trim();
    if (!rate) errors['e-rate'] = t('Enter the drop rate, like 1/512.');
    else if (rate !== e.rate && !parseRate(rate)) errors['e-rate'] = t('Enter a rate like 1/512, 3/1,024 or 2 x 1/1,024.');
    if (superAdmin && (!v.group.trim() || v.group.trim().length > 120)) errors['e-group'] = t('Enter a roll group of 120 characters or fewer.');
    return errors;
  }
  function paintEditor() {
    const e = drawer?.editor; if (!e) return;
    const v = editorValues(), name = v.name.trim(), nameChanged = name !== e.name;
    const other = nameChanged ? itemFor(name) : null, merge = other && other.id !== e.itemId && !usesHere(other) ? other : null;
    setText(ed('#e-name-hint'), e.others ? (nameChanged && !merge ? t('Renames the shared item for {0} too.', e.others) : t('Shared with {0}. Name, image and value change there too.', e.others)) : t('The item’s name everywhere it appears.'));
    const slot = ed('[data-catalogue-merge-slot]');
    if (merge && slot.dataset.item !== merge.id) {
      slot.dataset.item = merge.id; e.useShared = false;
      slot.replaceChildren(sharedBox(merge, 'e-use-shared', t('Point this drop at the shared item instead. The current item keeps its own value and history.'),
        t('Used by {0} · {1}. Its rate here stays {2}.', merge.uses.map(use => use.name).join(', ') || t('no activity'), gpText(merge.value), v.rate.trim() || e.rate),
        checked => { e.useShared = checked; paintEditor(); }));
    } else if (!merge) { slot.replaceChildren(); delete slot.dataset.item; e.useShared = false; }
    const parsed = parseRate(v.rate), rateChanged = v.rate.trim() !== e.rate;
    setText(ed('[data-catalogue-preview]'), parsed ? preview(parsed, rateChanged ? parsed.rolls : e.rolls) : t('Like 1/512, 3/1,024 or 2 x 1/1,024.'));
    const newEhb = rateChanged && parsed ? ehbOf(drawer.rate, parsed.p, parsed.rolls) : null;
    const calc = ed('[data-catalogue-calc]'), note = ed('[data-catalogue-calc-note]');
    const shown = rateChanged ? (newEhb != null ? ehbText(newEhb) : null) : e.ehb || null;
    setText(calc, shown != null ? t('{0} EHB', shown) : t('Unavailable')); calc.classList.toggle('is-muted', shown == null);
    note.textContent = shown == null
      ? (!(drawer.rate > 0) ? (drawer.category === 'Minigame' ? t('The activity has no runs per hour. Add it in Settings above.') : t('The activity has no kills per hour. Add it in Settings above.')) : t('The rate can’t be turned into a probability. Enter it like 1/512.'))
      : rateChanged && e.ehb ? t('Was {0}. Approved boards keep their snapshot.', e.ehb)
      : drawer.category === 'Minigame' ? t('Hours to get one at {0} runs per hour.', fmt(drawer.rate)) : t('Hours to get one at {0} kills per hour.', fmt(drawer.rate));
    const dirty = editorDirty();
    ed('#e-save').disabled = !dirty || !!drawer.busy;
    setText(ed('#e-cancel'), dirty ? t('Discard') : t('Close'));
    setText(ed('[data-catalogue-save-what]'), dirty ? (superAdmin ? t('Saves the name, rate, image and roll group.') : t('Saves the name, rate and image.')) : t('No changes yet.'));
    const p = priceValues();
    ed('[data-catalogue-manual]').hidden = p.mode !== 'Manual';
    ed('[data-catalogue-item-id]').hidden = p.mode === 'Untradeable';
    setText(ed('[data-catalogue-mode-hint]'), p.mode === 'Api' ? t('Uses the Wiki hourly price for the item ID. Validate to fetch it.') : p.mode === 'Manual' ? t('A fixed value. Price refreshes don’t change it. Zero is fine.') : t('Stored as 0 GP and never priced.'));
    for (const input of ed('[data-catalogue-price]').querySelectorAll('input[name=priceMode]')) input.closest('.seg-opt').classList.toggle('is-on', input.checked);
    const validate = ed('#p-validate [data-component-text]'); if (!ed('#p-validate').classList.contains('is-busy')) validate.textContent = p.mode === 'Api' ? validate.dataset.api : validate.dataset.other;
    paintLocks();
  }
  function sharedBox(item, id, checkText, text, onChange) {
    const box = document.createElement('div'); box.className = 'ct-shared'; box.setAttribute('role', 'group'); box.setAttribute('aria-labelledby', id + '-title');
    const main = document.createElement('div'); main.className = 'ct-shared-main';
    const thumb = document.createElement('span'); thumb.className = 'thumb'; thumb.setAttribute('aria-hidden', 'true');
    const textBox = document.createElement('div'); textBox.className = 'ct-min';
    const title = document.createElement('div'); title.className = 'ct-shared-title'; title.id = id + '-title'; title.textContent = t('“{0}” is already a shared item', item.name);
    const sub = document.createElement('div'); sub.className = 'ct-shared-text'; sub.textContent = text;
    textBox.append(title, sub); main.append(thumb, textBox);
    const label = document.createElement('label'); label.className = 'ct-check';
    const check = document.createElement('input'); check.type = 'checkbox'; check.id = id; check.setAttribute('aria-describedby', 'error-' + id);
    check.addEventListener('change', () => { fieldError(box, id, ''); onChange(check.checked); });
    const span = document.createElement('span'); span.textContent = checkText; label.append(check, span);
    const error = ui.template('field-error').firstElementChild; error.id = 'error-' + id; error.querySelector('[data-component-text]').textContent = '';
    box.append(main, label, error);
    return box;
  }
  async function saveDrop(confirmation = null) {
    const state = drawer, e = state?.editor; if (!e || state.busy) return;
    const errors = editorErrors();
    if (errors['e-shared']) fieldError(e.node, 'e-use-shared', errors['e-shared']);
    if (showErrors(e.node, errors, ['e-name', 'e-rate', 'e-group']) || errors['e-shared']) { if (errors['e-shared'] && !errors['e-name']) ed('#e-use-shared')?.focus(); return; }
    if (!editorDirty()) return;
    const v = editorValues();
    // RC10 C1: the readback compares every field this save sends, including the target item when repointing.
    const snapshot = { ...v, target: e.useShared ? itemFor(v.name)?.id : e.itemId, group: superAdmin ? v.group.trim() : null };
    const entries = { recordId: e.id, expectedVersion: e.version, expectedItemVersion: e.itemVersion, itemName: v.name.trim(), displayRate: v.rate.trim(), originalDisplayRate: e.rate, imageUrl: v.image.trim(), useExistingItem: e.useShared ? 'true' : 'false' };
    if (superAdmin) entries.rollGroup = v.group.trim();
    if (confirmation) entries.sharedItemConfirmationActivityIds = confirmation;
    const button = ed('#e-save');
    state.busy = 'drop'; setBusy(button, true, t('Saving…')); paintLocks();
    const outcome = await post('UpdateDrop', form(entries), { [t('Item name')]: v.name, [t('Drop rate')]: v.rate, [t('Image URL')]: v.image });
    state.busy = null; if (button.isConnected) setBusy(button, false); paintLocks();
    if (drawer !== state || outcome.kind === 'session-lost' || signal.aborted) return;
    const result = resultOf(outcome), slot = () => ed('[data-catalogue-editor-banner]');
    if (result?.outcome === 'completed') { await refresh({ saved: 'drop' }); ui.toast(t('{0} saved. EHB recalculated.', result.itemName)); $(`#row-${CSS.escape(e.id)}`)?.focus({ preventScroll: true }); return; }
    if (result?.outcome === 'confirm-shared') { const ids = await confirmShared(result.activities, v.name.trim() !== e.name ? 'rename' : 'image', !!confirmation); if (ids) await saveDrop(ids); return; }
    if (result?.outcome === 'shared') { fieldError(e.node, 'e-use-shared', t('Confirm using the shared item, or change the name.')); ed('#e-use-shared')?.focus(); return; }
    if (result?.outcome === 'invalid') { const map = { name: 'e-name', rate: 'e-rate', rollGroup: 'e-group' }; const errors = {}; for (const [key, message] of Object.entries(result.errors || {})) errors[map[key] || 'e-name'] = message; showErrors(e.node, errors, ['e-name', 'e-rate', 'e-group']); return; }
    if (result?.outcome === 'refused') { showBanner(slot(), 'is-warning', t('That couldn’t be saved.'), result.message); return; }
    if (result?.outcome === 'stale') { await refresh({ saved: 'drop', editor: e.id }); showBanner(slot(), 'is-warning', t('Another admin changed this drop or its shared item.'), t('Your changes weren’t saved. The current values are shown; check them and edit again.')); return; }
    if (outcome.status === 404) { await refresh({ saved: 'drop' }); return; }
    showBanner(slot(), 'is-warning', t('We couldn’t confirm the save.'), t('It may have gone through. Check before saving again.'), { label: t('Check current values'), run: b => void checkDrop(b, snapshot) });
  }
  async function checkDrop(button, snapshot) {
    const state = drawer, e = state.editor; button.disabled = true; button.textContent = t('Checking…');
    const doc = await fetchPage({ drop: '' });
    if (drawer !== state || !state.editor) return;
    if (!doc) { showBanner(ed('[data-catalogue-editor-banner]'), 'is-warning', t('Couldn’t check the current values.'), t('Nothing was repeated. Check again when you’re back online.'), { label: t('Check current values'), run: next => void checkDrop(next, snapshot) }); return; }
    const fresh = doc.querySelector(`template[data-catalogue-drop-editor="${CSS.escape(e.id)}"]`);
    const f = fresh?.dataset;
    const saved = !!f && f.rate === snapshot.rate.trim() && f.itemName === snapshot.name.trim() && normImage(f.image) === normImage(snapshot.image)
      && f.itemId === snapshot.target && (snapshot.group == null || f.rollGroup === snapshot.group);
    if (saved) { await refresh({ saved: 'drop', doc }); ui.toast(t('It was saved. The current values match what you entered.')); return; }
    showBanner(ed('[data-catalogue-editor-banner]'), 'is-info', t('It wasn’t saved.'), t('Your entries are still here; save again when ready.'));
  }
  // D7/D9: a change to a shared item's name or image needs a confirmation naming every other activity.
  async function confirmShared(activities, kind, again) {
    const names = activities.map(a => a.name).join(', ');
    const box = dialog({ title: t('Change the shared item?') });
    if (again) box.body.append(dialogBanner('is-warning', t('The activities using it changed.'), t('Check the list again before confirming.')));
    box.body.append(points([kind === 'rename' ? t('This also renames it for {0}.', names) : t('This also changes its image for {0}.', names), t('Names and images belong to the item, so every activity that uses it shows the change.')]));
    return new Promise(resolve => {
      let answered = false;
      const done = value => { if (answered) return; answered = true; void box.close(!!value).then(() => resolve(value)); };
      box.actions.append(box.button(t('Cancel'), '', () => done(null)), box.button(kind === 'rename' ? t('Rename everywhere') : t('Change everywhere'), 'btn-primary', () => done(activities.map(a => a.id))));
      box.actions.firstElementChild.focus();
      box.layer.element.addEventListener('keydown', event => { if (event.key === 'Escape') { event.stopPropagation(); done(null); } }, { capture: true });
    });
  }
  function resetEditor() {
    const e = drawer.editor; ed('#e-name').value = e.base.name; ed('#e-rate').value = e.base.rate; ed('#e-img').value = e.base.image; if (ed('#e-group')) ed('#e-group').value = e.base.group;
    for (const id of ['e-name', 'e-rate', 'e-group']) fieldError(e.node, id, '');
    ed('[data-catalogue-editor-banner]').replaceChildren(); paintEditor();
  }

  /* ---------------- value and item mapping (the shared item's own save) ---------------- */
  async function priceSuggest(button) {
    const state = drawer, e = state.editor, result = ed('#p-result');
    state.busy = 'price'; setBusy(button, true); paintEditor();
    const outcome = await post('SuggestItemApi', form({ itemName: e.name }), { [t('Item name')]: e.name });
    state.busy = null; if (button.isConnected) setBusy(button, false); paintEditor();
    if (drawer !== state || state.editor !== e || outcome.kind === 'session-lost') return;
    const data = resultOf(outcome); result.hidden = false;
    if (data?.id) { ed('#p-id').value = data.id; result.className = 'ct-result'; result.textContent = t('Suggested an ID for “{0}”. Validate to save it.', e.name); paintEditor(); }
    else { result.className = 'ct-result is-warn'; result.textContent = data?.reason === 'no-match' ? t('No unique exact-name match. Enter the exact item ID, or mark it untradeable. Nothing changed.') : t('The item list couldn’t be reached. Nothing changed; enter the ID yourself or try later.'); }
  }
  // The reference's outcome texts (Catalogue.dc.html:857-862), from the server's structured result.
  function priceText(data, operation) {
    const gp = data.value == null ? null : t('{0} gp', fmt(data.value, 0));
    switch (data.result) {
      case 'unavailable': return ['is-warn', t('Saved.') + ' ' + (data.rateLimited ? t('The price service is limiting requests, so the ID wasn’t checked.') : t('The price service isn’t responding, so the ID wasn’t checked.')) + ' ' + (gp ? t('The stored value was kept.') : t('No value is stored yet; enter a manual value or validate later.'))];
      case 'unsupported': return ['is-warn', t('Saved. This ID isn’t in the tradeable item list; check the exact variant, or mark it untradeable.')];
      case 'verified-price': return ['is-ok', t('Verified. Hourly price saved: {0}.', gp)];
      case 'verified-rejected': return ['is-warn', t('Verified. The new price looked like an unusual change, so the trusted value was kept. Review it or enter a checked manual value.')];
      case 'verified-kept': return ['is-ok', t('Verified. Your chosen value was kept.')];
      case 'verified-no-price': case 'verified-price-unavailable': return ['is-warn', data.message];
      default:
        if (data.mode === 'Manual') return ['is-ok', t('Saved. {0} is a fixed value; price refreshes won’t change it.', gp)];
        if (data.mode === 'Untradeable') return ['is-ok', t('Saved as untradeable (0 GP).')];
        return ['', t('Saved without checking.') + ' ' + (data.cleared ? t('The old price was cleared with the old ID; validate or enter a manual value.') : t('Validate to fetch the hourly price.'))];
    }
  }
  async function priceSave(operation, button) {
    const state = drawer, e = state.editor, p = priceValues(), result = ed('#p-result');
    fieldError(e.node, 'p-gp', '');
    if (p.mode === 'Manual' && !/^\d+$/.test(p.gp.trim())) { fieldError(e.node, 'p-gp', p.gp.trim() ? t('Enter whole GP, like 2500. Zero is fine.') : t('Enter a value. Zero is fine.')); ed('#p-gp').focus(); return; }
    if (p.id.trim() && !/^\d+$/.test(p.id.trim())) { result.hidden = false; result.className = 'ct-result is-warn'; result.textContent = t('Item IDs are whole numbers, like 12922.'); return; }
    state.busy = 'price'; setBusy(button, true); paintEditor();
    const outcome = await post('ItemApi', form({ recordId: e.id, expectedItemId: e.itemId, expectedItemVersion: e.itemVersion, externalIdentifier: p.mode === 'Untradeable' ? p.id.trim() : p.id.trim(), priceMode: p.mode, manualValue: p.mode === 'Manual' ? p.gp.trim() : '', operation }),
      { [t('Price source')]: p.mode, [t('Manual value')]: p.gp, [t('OSRS Wiki item ID')]: p.id });
    state.busy = null; if (button.isConnected) setBusy(button, false); if (state.editor === e) paintEditor();
    if (drawer !== state || outcome.kind === 'session-lost') return;
    const data = resultOf(outcome);
    if (data?.outcome === 'completed') {
      await refresh({ saved: 'price', editor: e.id });
      openDisclosure(ed('#price-btn'), true);
      const [tone, text] = priceText(data, operation);
      const node = ed('#p-result'); node.hidden = false; node.className = `ct-result ${tone}`; node.textContent = text; node.focus({ preventScroll: true });
      return;
    }
    if (data?.outcome === 'stale') { await refresh({ saved: 'price', editor: e.id }); showBanner(ed('[data-catalogue-editor-banner]'), 'is-warning', t('Another admin changed this drop or its shared item.'), t('Your changes weren’t saved. The current values are shown; check them and edit again.')); return; }
    result.hidden = false; result.className = 'ct-result is-warn';
    result.textContent = outcome.status === 400 ? t('That couldn’t be saved. Check the ID and value and try again.') : t('We couldn’t confirm the save. Check the current values before saving again.');
  }

  /* ---------------- add drop ---------------- */
  function openAdd(focus = true) {
    const state = drawer; if (!state.addTemplate) return;
    closeEditor(false); closeAdd();
    const node = document.importNode(state.addTemplate.content, true).firstElementChild;
    $('[data-catalogue-add-slot]').replaceChildren(node);
    $('#add-drop')?.setAttribute('aria-expanded', 'true');
    state.addForm = { node, useShared: false };
    node.addEventListener('input', paintAdd); node.addEventListener('change', paintAdd);
    paintAdd();
    if (focus) node.querySelector('#n-name').focus();
    writeUrl({ drop: '' });
  }
  function closeAdd(focus = false) {
    const state = drawer; if (!state?.addForm) return;
    state.addForm.node.remove(); state.addForm = null; $('#add-drop')?.setAttribute('aria-expanded', 'false');
    if (focus) $('#add-drop')?.focus({ preventScroll: true });
    paintLocks();
  }
  const ad = selector => drawer?.addForm?.node.querySelector(selector);
  const addFormValues = () => ({ name: ad('#n-name').value, rate: ad('#n-rate').value, gp: ad('#n-gp').value, image: ad('#n-img').value, mode: ad('input[name=addMode]:checked').value, useShared: drawer.addForm.useShared });
  function restoreAddForm(v) { ad('#n-name').value = v.name; ad('#n-rate').value = v.rate; ad('#n-gp').value = v.gp; ad('#n-img').value = v.image; ad(`input[name=addMode][value="${v.mode}"]`).checked = true; paintAdd(); if (v.useShared && ad('#n-use-shared')) { ad('#n-use-shared').checked = true; drawer.addForm.useShared = true; } }
  const addFormHasInput = () => { const v = addFormValues(); return !!(v.name.trim() || v.rate.trim() || v.gp.trim() || v.image.trim()); };
  function addShared() { const item = itemFor(ad('#n-name').value); return item; }
  function paintAdd() {
    const f = drawer?.addForm; if (!f) return;
    const v = addFormValues(), shared = addShared(), here = shared ? usesHere(shared) : null;
    const slot = ad('[data-catalogue-shared-slot]');
    if (shared && !(here && here.active) && slot.dataset.item !== shared.id) {
      slot.dataset.item = shared.id; f.useShared = false;
      slot.replaceChildren(sharedBox(shared, 'n-use-shared', t('Use the shared item for this drop. Its name, image and value stay shared; the rate is only for this activity.'),
        t('Used by {0} · {1}.', shared.uses.map(use => use.name).join(', ') || t('no activity yet'), gpText(shared.value)), checked => { f.useShared = checked; }));
    } else if (!shared || here?.active) { slot.replaceChildren(); delete slot.dataset.item; f.useShared = false; }
    ad('[data-catalogue-value]').hidden = !!shared;
    ad('[data-catalogue-add-gp]').hidden = v.mode !== 'manual';
    for (const input of f.node.querySelectorAll('input[name=addMode]')) input.closest('.seg-opt').classList.toggle('is-on', input.checked);
    setText(ad('[data-catalogue-value-hint]'), v.mode === 'manual' ? t('Zero is fine. Manual values stay fixed when prices refresh.') : v.mode === 'fetch' ? t('Looks up the exact name and its hourly price when you add the drop. If that fails, nothing is added.') : t('Stored as 0 GP and marked untradeable.'));
    const parsed = parseRate(v.rate);
    setText(ad('[data-catalogue-preview]'), parsed ? preview(parsed, parsed.rolls) : t('Write it as shown on the Wiki. Your own chance, at the group size the activity rate uses.'));
    const save = ad('#add-save'); if (!save.classList.contains('is-busy')) save.querySelector('[data-component-text]').textContent = v.mode === 'fetch' && !shared ? t('Fetch price and add') : t('Add drop');
    paintLocks();
  }
  function addErrors() {
    const v = addFormValues(), errors = {}, name = v.name.trim(), shared = addShared();
    if (!name) errors['n-name'] = t('Enter the item name.'); else if (name.length > 200) errors['n-name'] = t('Use 200 characters or fewer.');
    if (shared) { const here = usesHere(shared); if (here?.active) errors['n-name'] = t('{0} is already a drop for this activity.', shared.name); else if (!drawer.addForm.useShared) errors['n-use-shared'] = t('Confirm using the shared item, or change the name.'); }
    if (!v.rate.trim()) errors['n-rate'] = t('Enter the drop rate, like 1/512.'); else if (!parseRate(v.rate)) errors['n-rate'] = t('Enter a rate like 1/512, 3/1,024 or 2 x 1/1,024.');
    if (!shared && v.mode === 'manual' && !/^\d+$/.test(v.gp.trim())) errors['n-gp'] = v.gp.trim() ? t('Enter whole GP, like 2500. Zero is fine.') : t('Enter a value for the new item. Zero is fine.');
    return errors;
  }
  async function saveAdd(confirmation = null) {
    const state = drawer, f = state?.addForm; if (!f || state.busy) return;
    const errors = addErrors();
    if (showErrors(f.node, errors, ['n-name', 'n-rate', 'n-use-shared', 'n-gp'])) return;
    const v = addFormValues(), shared = addShared();
    const entries = { 'BossDrop.BossActivityId': state.id, 'BossDrop.ItemName': v.name.trim(), 'BossDrop.DisplayRate': v.rate.trim(), 'BossDrop.ImageUrl': v.image.trim(), 'BossDrop.UseExistingItem': shared && f.useShared ? 'true' : 'false',
      'BossDrop.FetchPrice': !shared && v.mode === 'fetch' ? 'true' : 'false', 'BossDrop.Untradeable': !shared && v.mode === 'untradeable' ? 'true' : 'false', 'BossDrop.InitialValueGp': !shared && v.mode === 'manual' ? v.gp.trim() : null };
    if (confirmation) entries.sharedItemConfirmationActivityIds = confirmation;
    const button = ad('#add-save');
    state.busy = 'add'; setBusy(button, true, v.mode === 'fetch' && !shared ? t('Fetching…') : t('Adding…')); paintLocks();
    const outcome = await post('BossDrop', form(entries), { [t('Item name')]: v.name, [t('Drop rate')]: v.rate, [t('Image URL')]: v.image });
    state.busy = null; if (button.isConnected) setBusy(button, false); paintLocks();
    if (drawer !== state || outcome.kind === 'session-lost' || signal.aborted) return;
    const result = resultOf(outcome), slot = () => ad('[data-catalogue-add-banner]');
    if (result?.outcome === 'completed') {
      await refresh({ saved: 'add' }); $('#add-drop')?.focus({ preventScroll: true });
      ui.toast(result.reactivated ? t('{0} reactivated on {1}.', result.itemName, state.name) : t('{0} added to {1}.', result.itemName, state.name));
      if (result.notice) showBanner($('[data-catalogue-banner-slot]'), 'is-info', t('Added with a manual value.'), result.notice);
      return;
    }
    if (result?.outcome === 'provider') {
      ad('input[name=addMode][value=manual]').checked = true; paintAdd();
      const reason = { 'no-match': t('No unique exact-name item was found, so there’s no price.'), 'rate-limited': t('The price service is limiting requests.'), unavailable: t('The price service isn’t responding.') }[result.code] || result.reason || '';
      showBanner(slot(), 'is-warning', '', t('{0} The drop wasn’t added. Enter a manual value (zero is fine) and add it; you can set up price fetching later.', reason));
      return;
    }
    if (result?.outcome === 'confirm-shared') { const ids = await confirmShared(result.activities, 'image', !!confirmation); if (ids) await saveAdd(ids); return; }
    if (result?.outcome === 'shared') { fieldError(f.node, 'n-use-shared', t('Confirm using the shared item, or change the name.')); return; }
    if (result?.outcome === 'invalid') { const map = { name: 'n-name', rate: 'n-rate', value: 'n-gp', image: 'n-img' }; const errs = {}; for (const [key, message] of Object.entries(result.errors || {})) errs[map[key] || 'n-name'] = message; showErrors(f.node, errs, ['n-name', 'n-rate', 'n-gp']); return; }
    if (result?.outcome === 'refused') { showBanner(slot(), 'is-warning', t('That couldn’t be saved.'), result.message); return; }
    await refresh();
    showBanner(slot(), 'is-warning', '', t('We couldn’t confirm whether it was added. Check the list below before adding it again.'));
  }

  /* ---------------- deactivate (S10), reactivate, delete ---------------- */
  function boardList(impact) {
    const list = document.createElement('ul'); list.className = 'ct-boards';
    for (const board of impact.boards) {
      const li = document.createElement('li');
      const name = board.eventName + (board.correction ? ' ' + t('(correction)') : '');
      if (board.url) { const a = document.createElement('a'); a.href = board.url; a.dataset.shellLink = ''; a.textContent = name; li.append(a); }
      else li.append(name);
      if (board.hidden) { const pill = document.createElement('span'); pill.className = 'pill is-neutral'; pill.textContent = t('Hidden'); li.append(' ', pill); }
      list.append(li);
    }
    if (impact.hiddenCount) { const li = document.createElement('li'); li.className = 'ct-muted'; li.textContent = impact.hiddenCount === 1 ? t('and 1 hidden event') : t('and {0} hidden events', impact.hiddenCount); list.append(li); }
    return list;
  }
  async function deactivate(type, opener) {
    const state = drawer, e = state.editor;
    const id = type === 'boss' ? state.id : e.id, version = type === 'boss' ? state.version : e.version, name = type === 'boss' ? state.name : e.name;
    const box = dialog({ title: t('Deactivate {0}?', name), opener });
    box.body.append(checking());
    const cancel = box.button(t('Cancel'), '', () => void box.close(false));
    box.actions.append(cancel); cancel.focus();
    const outcome = await getJson(`/Admin/Catalogue?handler=DeactivationImpact&recordType=${type}&recordId=${encodeURIComponent(id)}&expectedVersion=${encodeURIComponent(version)}`);
    if (outcome.kind === 'session-lost') { void box.close(false); return; }
    const data = resultOf(outcome);
    box.body.replaceChildren();
    if (data?.outcome !== 'impact') {
      box.body.append(data?.outcome === 'stale' ? dialogBanner('is-warning', t('It changed since you opened it.'), t('Another admin edited it. Close this, check the current values and try again.')) : dialogBanner('is-warning', t('Couldn’t check what uses it.'), t('Nothing was changed. Close this and try again.')));
      cancel.querySelector('[data-component-text]').textContent = t('Close');
      return;
    }
    const impact = data.impact, used = impact.boards.length + impact.hiddenCount;
    const items = [type === 'boss' ? t('It can’t be picked for new tiles. Its {0} drops stay with it.', state.element.querySelectorAll('.dlist-item').length) : t('It can’t be picked for new tiles. It stays on {0} and can be reactivated.', state.name)];
    if (used) { const lead = document.createElement('div'); lead.append(t('Draft boards that use it can’t be approved until it’s reactivated or those tiles change:'), boardList(impact)); items.push(lead); }
    else items.push(t('No draft board uses it now.'));
    items.push(t('Approved and published boards keep their snapshot.'));
    if (type === 'boss') items.push(t('You can reactivate it at any time.'));
    box.body.append(points(items));
    const confirm = box.button(t('Deactivate'), 'btn-primary', async () => {
      box.setBusy(true); setBusy(confirm, true, t('Deactivating…'));
      const result = await post(type === 'boss' ? 'ToggleBoss' : 'ToggleDrop', form({ recordId: id, expectedVersion: version, confirmed: 'true' }), { [t('Action')]: t('Deactivate {0}', name) });
      box.setBusy(false); setBusy(confirm, false);
      if (result.kind === 'session-lost') return;
      const body = resultOf(result);
      if (body?.outcome === 'completed') {
        await box.close(true); await refresh({ editor: type === 'drop' ? id : null });
        ui.toast(used ? t('{0} deactivated. Draft boards that use it can’t be approved until it’s reactivated.', name) : t('{0} deactivated. It stays in the catalogue and can be reactivated.', name));
        (type === 'boss' ? $('#a-toggle') : ed('#e-toggle'))?.focus({ preventScroll: true });
        return;
      }
      if (body?.outcome === 'stale') { box.body.prepend(dialogBanner('is-warning', t('It changed since you opened it.'), t('Another admin edited it. Close this, check the current values and try again.'))); confirm.remove(); cancel.querySelector('[data-component-text]').textContent = t('Close'); return; }
      await box.close(false);
      showBanner(type === 'boss' ? $('[data-catalogue-banner-slot]') : ed('[data-catalogue-editor-banner]'), 'is-warning', t('We couldn’t confirm the change.'), t('It may have gone through. Check the current status before trying again.'),
        { label: t('Check current status'), run: async button => { button.disabled = true; await refresh({ editor: type === 'drop' ? id : null }); } });
    });
    box.actions.append(confirm); confirm.focus();
  }
  async function reactivate(type, button) {
    const state = drawer, e = state.editor;
    const id = type === 'boss' ? state.id : e.id, version = type === 'boss' ? state.version : e.version, name = type === 'boss' ? state.name : e.name;
    state.busy = 'toggle'; paintLocks(); setBusy(button, true);
    const outcome = await post(type === 'boss' ? 'ToggleBoss' : 'ToggleDrop', form({ recordId: id, expectedVersion: version }), { [t('Action')]: t('Reactivate {0}', name) });
    state.busy = null; paintLocks(); if (button.isConnected) setBusy(button, false);
    if (drawer !== state || outcome.kind === 'session-lost') return;
    const data = resultOf(outcome);
    if (data?.outcome === 'completed') { await refresh({ editor: type === 'drop' ? id : null }); ui.toast(type === 'boss' ? t('{0} reactivated. It can be picked for new tiles again.', name) : t('{0} reactivated. It can be picked for tiles again.', name)); (type === 'boss' ? $('#a-toggle') : ed('#e-toggle'))?.focus({ preventScroll: true }); return; }
    if (data?.outcome === 'stale') { await refresh({ editor: type === 'drop' ? id : null }); showBanner($('[data-catalogue-banner-slot]'), 'is-warning', t('This record was changed by another administrator.'), t('Current values are shown; review them before trying again.')); return; }
    showBanner($('[data-catalogue-banner-slot]'), 'is-warning', t('We couldn’t confirm the change.'), t('It may have gone through. Check the current status before trying again.'), { label: t('Check current status'), run: async b => { b.disabled = true; await refresh({ editor: type === 'drop' ? id : null }); } });
  }
  async function remove(type, opener) {
    const state = drawer, e = state.editor;
    const id = type === 'boss' ? state.id : e.id, version = type === 'boss' ? state.version : e.version, name = type === 'boss' ? state.name : e.name, active = type === 'boss' ? state.active : e.active;
    const box = dialog({ title: t('Delete {0} permanently?', name), opener });
    box.body.append(checking());
    const cancel = box.button(t('Cancel'), '', () => void box.close(false)); box.actions.append(cancel); cancel.focus();
    const outcome = await getJson(`/Admin/Catalogue?handler=DeletionImpact&recordType=${type}&recordId=${encodeURIComponent(id)}&expectedVersion=${encodeURIComponent(version)}`);
    if (outcome.kind === 'session-lost') { void box.close(false); return; }
    const data = resultOf(outcome); box.body.replaceChildren();
    const closeOnly = () => { cancel.querySelector('[data-component-text]').textContent = t('Close'); };
    if (data?.outcome === 'referenced') {
      box.heading.textContent = t('{0} can’t be deleted', name);
      box.body.append(dialogBanner('is-info', t('It’s in use.'), type === 'boss' ? t('It has drops or is used by boards, so deleting it would break them. Deactivate it instead; it can be reactivated later.') : t('Boards use this drop, so deleting it would break them. Deactivate it instead.')));
      closeOnly();
      if (active) box.actions.append(box.button(t('Deactivate instead'), '', async () => { await box.close(false); void deactivate(type, opener); }));
      return;
    }
    if (data?.outcome !== 'deletable') { box.body.append(dialogBanner('is-warning', t('It changed since you opened it.'), data?.outcome === 'stale' ? t('Another admin edited it. Close this, check the current values and try again.') : t('Nothing was changed. Close this and try again.'))); closeOnly(); return; }
    box.body.append(points([t('Nothing uses it. It’s removed from the catalogue for good; this can’t be undone.'), type === 'boss' ? t('Shared items, prices and Audit history are kept.') : t('Only this activity’s drop is removed. The shared item, its value and Audit history are kept.')]));
    const confirm = box.button(t('Delete permanently'), 'btn-danger', async () => {
      box.setBusy(true); setBusy(confirm, true, t('Deleting…'));
      const result = await post('Delete', form({ recordType: type, recordId: id, expectedVersion: version, confirmed: 'true' }), { [t('Action')]: t('Delete {0} permanently', name) });
      box.setBusy(false); setBusy(confirm, false);
      if (result.kind === 'session-lost') return;
      const body = resultOf(result);
      if (body?.outcome === 'completed') {
        await box.close(true);
        if (type === 'boss') {
          state.silent = true; await state.layer.close(true); writeUrl({ activity: '', drop: '', new: '' });
          const doc = await fetchPage({ activity: '', drop: '' }); if (doc) patchDirectory(doc);
          document.querySelector('#page-h1')?.focus({ preventScroll: true });
          ui.toast(t('{0} permanently deleted. Shared items, prices and Audit history are kept.', name));
        } else { await refresh({ saved: 'drop' }); $('#sec-drops')?.focus({ preventScroll: true }); ui.toast(t('{0} permanently deleted from this activity. The shared item is kept.', name)); }
        return;
      }
      box.body.replaceChildren(body?.outcome === 'referenced'
        ? dialogBanner('is-info', t('It’s in use.'), t('It became used by a board meanwhile, so it wasn’t deleted. Deactivate it instead.'))
        : dialogBanner('is-warning', t('It wasn’t deleted.'), body?.message || t('We couldn’t confirm the deletion. Close this and check the current values.')));
      confirm.remove(); closeOnly();
    });
    box.actions.append(confirm);
  }

  /* ---------------- drawer events ---------------- */
  function paintLocks() {
    if (!drawer || drawer.missing || !drawer.element) return;
    const locked = !!drawer.busy;
    for (const node of drawer.element.querySelectorAll('.dr-body input, .dr-body select, .dr-body button, .dr-foot button')) {
      if (node.matches('#e-save')) { node.disabled = locked || !editorDirty(); continue; }
      if (node.matches('#p-validate')) { node.disabled = locked || !(drawer.editor && priceValues().id.trim()); continue; }
      node.disabled = locked;
    }
  }
  function openDisclosure(button, force) {
    if (!button) return;
    const target = drawer.element.querySelector('#' + button.getAttribute('aria-controls'));
    const open = force ?? button.getAttribute('aria-expanded') !== 'true';
    button.setAttribute('aria-expanded', String(open)); button.classList.toggle('is-open', open); target.hidden = !open;
  }
  listen(document, 'click', event => {
    if (!drawer || !drawer.element?.contains(event.target)) return;
    const target = event.target;
    if (target.closest('[data-catalogue-close]')) { void ui.closeLayer(); return; }
    const disclosure = target.closest('[data-catalogue-disclosure]'); if (disclosure) { openDisclosure(disclosure); return; }
    if (target.closest('[data-catalogue-cancel]')) {
      if (!drawer.add && activityDirty()) { setActivityValues(drawer.baseline); for (const id of ['a-name', 'a-cat', 'a-rate', 'a-team', 'a-img']) fieldError(drawer.element, id, ''); paintActivity(); ui.toast(t('Activity changes discarded.')); }
      else void ui.closeLayer();
      return;
    }
    if (target.closest('[data-catalogue-save]')) { void saveActivity(); return; }
    const mapSaveButton = target.closest('[data-catalogue-map-save]'); if (mapSaveButton) { void mapSave(mapSaveButton.dataset.catalogueMapSave, mapSaveButton); return; }
    if (target.closest('[data-catalogue-map-suggest]')) { void mapSuggest(target.closest('button')); return; }
    const toggle = target.closest('[data-catalogue-drop-toggle]'); if (toggle) { void toggleEditor(toggle.dataset.catalogueDropToggle); return; }
    if (target.closest('[data-catalogue-drop-save]')) { void saveDrop(); return; }
    if (target.closest('[data-catalogue-drop-cancel]')) {
      // Discard resets the editor's own fields (reference); Close asks when value/mapping changes are unsaved.
      if (editorDirty()) resetEditor();
      else void (async () => { const parts = editorParts(); if (parts.length && !await askDiscard(parts)) return; closeEditor(true); })();
      return;
    }
    const priceButton = target.closest('[data-catalogue-price-save]'); if (priceButton) { void priceSave(priceButton.dataset.cataloguePriceSave, priceButton); return; }
    if (target.closest('[data-catalogue-price-suggest]')) { void priceSuggest(target.closest('button')); return; }
    if (target.closest('[data-catalogue-add-open]')) { void (async () => { const parts = editorParts(); if (parts.length && !await askDiscard(parts)) return; openAdd(); })(); return; }
    if (target.closest('[data-catalogue-add-save]')) { void saveAdd(); return; }
    if (target.closest('[data-catalogue-add-cancel]')) { void (async () => { if (addFormHasInput() && !await askDiscard([t('the new drop')])) return; closeAdd(true); })(); return; }
    const activeDrop = target.closest('[data-catalogue-drop-active]'); if (activeDrop) { if (drawer.editor.active) void deactivate('drop', activeDrop); else void reactivate('drop', activeDrop); return; }
    const activeActivity = target.closest('[data-catalogue-activity-active]'); if (activeActivity) { if (drawer.active) void deactivate('boss', activeActivity); else void reactivate('boss', activeActivity); return; }
    const del = target.closest('[data-catalogue-delete]'); if (del) { void remove(del.dataset.catalogueDelete, del); }
  });
  listen(document, 'keydown', event => {
    if (!drawer || !drawer.element?.contains(event.target) || event.key !== 'Enter' || !event.target.matches('input:not([type=checkbox]):not([type=radio])')) return;
    event.preventDefault();
    if (event.target.closest('[data-catalogue-activity-form]')) void saveActivity();
    else if (event.target.closest('.dlist-ed') && !event.target.closest('[data-catalogue-price]')) void saveDrop();
    else if (event.target.closest('.ct-add')) void saveAdd();
  });

  /* ---------------- history (Back/Forward) ---------------- */
  const unregisterUrl = ui.registerUrlState(async next => {
    clearTimeout(timer);
    const url = new URL(next);
    if (!/^\/admin\/catalogue(\/index)?$/i.test(url.pathname)) return false;
    const p = url.searchParams;
    Object.assign(filters, { q: (p.get('q') || '').slice(0, 100), cat: CATS.includes(p.get('cat')) ? p.get('cat') : '', status: p.get('status') === 'inactive' ? 'inactive' : '' });
    search.value = filters.q; applyFilters();
    const activity = p.get('activity'), add = p.get('new') === '1' && !activity, drop = p.get('drop');
    if (!activity && !add) { if (drawer) await closeDrawer({ history: false }); return true; }
    if (add ? drawer?.add : drawer?.id === activity) {
      if (drop && drawer.editor?.id !== drop) openEditor(drop, { quiet: true }); else if (!drop && drawer.editor) closeEditor(false, true);
      return true;
    }
    await openDrawer({ id: activity, add, opener: search, drop });
    return true;
  });

  // Canonical URL for this render; a direct ?activity= / ?new=1 link opens its drawer.
  const initial = root.querySelector('template[data-catalogue-drawer]');
  ui.setUrl(Object.fromEntries(new URL(root.dataset.directoryCanonical, location.href).searchParams), schema);
  applyFilters();
  if (initial) void openDrawer({ id: initial.dataset.activityId || null, add: initial.dataset.new === 'true', opener: search, source: initial });
  release = () => {
    life.abort(); clearTimeout(timer); overflow.disconnect(); cancelAnimationFrame(overflowFrame); unregisterUrl(); unregisterDraft?.();
    drawer = null;
  };
}

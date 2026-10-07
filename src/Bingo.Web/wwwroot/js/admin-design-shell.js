// Shared behavior for opted-in Admin pages. Page modules export init(root, ui)
// and dispose(); the shell disposes the old module before replacing its content.
(() => {
  'use strict';
  if (!document.body.hasAttribute('data-admin-design')) return;
  const body = document.body;
  const text = key => body.dataset[key] || '';
  const reducedMotion = () => matchMedia('(prefers-reduced-motion: reduce)').matches;
  const focus = element => (element?.isConnected ? element : document.querySelector('.h1'))?.focus({ preventScroll: true });
  const controls = root => [...root.querySelectorAll('a[href],button,input,select,textarea,[tabindex]')].filter(el => !el.disabled && el.tabIndex >= 0 && el.getClientRects().length && !el.closest('[inert]'));
  function trapTab(event, root) {
    if (event.key !== 'Tab') return;
    const list = controls(root), first = list[0], last = list.at(-1), active = document.activeElement;
    if (!first) { event.preventDefault(); focus(root); }
    else if (!root.contains(active) || active === root) { event.preventDefault(); focus(event.shiftKey ? last : first); }
    else if (event.shiftKey && active === first) { event.preventDefault(); focus(last); }
    else if (!event.shiftKey && active === last) { event.preventDefault(); focus(first); }
  }
  function afterExit(element) {
    if (reducedMotion()) return Promise.resolve();
    const style = getComputedStyle(element);
    const ms = value => parseFloat(value) * (value.trim().endsWith('ms') ? 1 : 1000) || 0;
    const duration = style.animationDuration.split(',').map(ms), delay = style.animationDelay.split(',').map(ms);
    const longest = style.animationName === 'none' ? 0 : Math.max(0, ...duration.map((value, i) => value + delay[i % delay.length]));
    return new Promise(resolve => setTimeout(resolve, longest ? longest + 20 : 0));
  }
  const BUSY_MINIMUM_MS = 600, QUICK_BUSY_MINIMUM_MS = 250;
  const LOADING_DELAY_MS = 150, LOADING_MINIMUM_MS = 400;
  function delayedLoading(show, signal, inheritedAt = null, isReady = () => true) {
    let shownAt = null, timer, release, eligible = inheritedAt !== null, finished = false;
    const tryShow = () => {
      if (eligible && !finished && !signal.aborted && shownAt === null && isReady()) {
        shownAt = inheritedAt ?? performance.now(); show(shownAt);
      }
    };
    const cancel = () => { finished = true; clearTimeout(timer); release?.(); };
    signal.addEventListener('abort', cancel, { once: true });
    if (eligible) tryShow();
    else timer = setTimeout(() => { eligible = true; tryShow(); }, LOADING_DELAY_MS);
    return {
      ready: tryShow,
      async complete() {
        finished = true;
        clearTimeout(timer);
        if (shownAt === null || signal.aborted) return;
        const remaining = LOADING_MINIMUM_MS - (performance.now() - shownAt);
        if (remaining > 0) await new Promise(resolve => { release = resolve; timer = setTimeout(resolve, remaining); });
      },
      cancel() { cancel(); signal.removeEventListener('abort', cancel); }
    };
  }
  async function busy(request, quick = false) {
    const started = performance.now();
    try { return await request(); }
    finally {
      const remaining = (quick ? QUICK_BUSY_MINIMUM_MS : BUSY_MINIMUM_MS) - (performance.now() - started);
      if (!reducedMotion() && remaining > 0) await new Promise(resolve => setTimeout(resolve, remaining));
    }
  }

  let menu = null;
  const menuExits = new WeakMap();
  const layers = [];
  let mobile = false;
  const mobileQuery = matchMedia('(max-width: 860px)');
  function lock() {
    document.querySelector('.scroller')?.classList.toggle('is-locked', !!layers.length || mobile);
    const main = document.querySelector('.main'), side = document.querySelector('[data-shell-sidebar]');
    if (main) main.inert = !!layers.length || mobile;
    if (side) side.inert = !!layers.length;
    layers.forEach((layer, index) => { layer.panel.inert = index !== layers.length - 1; layer.panel.classList.toggle('is-behind', index !== layers.length - 1); });
  }
  function closeMenu(restore = true) {
    if (!menu) return;
    const closing = menu, token = Symbol(); menu = null;
    menuExits.set(closing.element, token);
    closing.element.classList.add('is-closing');
    closing.opener.setAttribute('aria-expanded', 'false');
    if (restore) focus(closing.opener);
    void afterExit(closing.element).then(() => {
      if (menuExits.get(closing.element) !== token) return;
      closing.element.hidden = true; closing.element.classList.remove('is-closing'); menuExits.delete(closing.element);
    });
  }
  function openMenu(opener) {
    const element = document.getElementById(opener.dataset.menuTarget);
    if (!element) return;
    if (menu?.element === element) { closeMenu(); return; }
    closeMenu(false);
    menuExits.delete(element); element.classList.remove('is-closing'); element.hidden = false;
    const bounds = opener.getBoundingClientRect();
    const side = opener.closest('[data-shell-sidebar]');
    const align = side?.classList.contains('is-collapsed') && !mobileQuery.matches ? 'side' : opener.dataset.menuAlign || 'start';
    if (side) element.style.width = `${Math.min(innerWidth - 16, Math.max(256, bounds.width))}px`;
    const width = element.offsetWidth, height = element.offsetHeight;
    let x = align === 'side' ? bounds.right + 8 : align === 'end' ? bounds.right - width : bounds.left;
    let y = align === 'side' ? bounds.top : bounds.bottom + (align === 'end' ? 4 : 6);
    if (align === 'end' && y + height > innerHeight - 8) y = bounds.top - height - 4;
    x = Math.max(8, Math.min(x, innerWidth - width - 8)); y = Math.max(8, y);
    element.style.left = `${Math.round(x)}px`; element.style.top = `${Math.round(y)}px`;
    opener.setAttribute('aria-expanded', 'true');
    menu = { element, opener };
    const checked = element.querySelector('[role="menuitemradio"][aria-checked="true"]');
    focus(checked || controls(element)[0]);
    checked?.scrollIntoView({ block: 'nearest' });
  }
  function placePreferences() {
    const topbar = document.querySelector('[data-shell-topbar]');
    // Prefer newly translated/refreshed controls to the retained mobile instance.
    const preferences = topbar?.querySelector('[data-shell-preferences]') || document.querySelector('[data-shell-preferences]');
    const target = mobileQuery.matches ? document.querySelector('[data-mobile-preferences]') : topbar?.querySelector('[data-desktop-preferences]');
    if (!preferences || !target) return;
    document.querySelectorAll('[data-shell-preferences]').forEach(other => { if (other !== preferences) other.remove(); });
    if (preferences.parentElement !== target) target.append(preferences);
  }
  function paintSideLabel() {
    placePreferences();
    const collapsed = document.querySelector('[data-shell-sidebar]')?.classList.contains('is-collapsed');
    const label = text(mobileQuery.matches ? 'closeNavigation' : collapsed ? 'expandNavigation' : 'collapseNavigation');
    document.querySelectorAll('.collapse-btn').forEach(button => { button.setAttribute('aria-label', label); button.title = label; });
    document.querySelectorAll('[data-collapse-arrow]').forEach(arrow => arrow.classList.toggle('is-flip', !!collapsed && !mobileQuery.matches));
    document.querySelectorAll('[data-collapsed-title]').forEach(element => {
      if (collapsed && !mobileQuery.matches) element.title = element.dataset.collapsedTitle;
      else element.removeAttribute('title');
    });
  }
  function toggleSide(force) {
    const side = document.querySelector('[data-shell-sidebar]');
    if (mobileQuery.matches) {
      mobile = force ?? !mobile;
      side?.classList.toggle('is-mobile-open', mobile);
      document.querySelector('[data-side-scrim]')?.classList.toggle('is-on', mobile);
      document.querySelectorAll('[data-side-toggle]').forEach(button => button.setAttribute('aria-expanded', String(mobile)));
      lock();
      if (mobile) focus(controls(side)[0]);
      else focus(document.querySelector('.menu-toggle'));
    } else {
      side?.classList.toggle('is-collapsed');
      document.querySelectorAll('[data-side-toggle]').forEach(button => button.setAttribute('aria-expanded', String(!side?.classList.contains('is-collapsed'))));
    }
    paintSideLabel();
  }
  mobileQuery.addEventListener('change', event => {
    paintSideLabel();
    if (event.matches || !mobile) return;
    mobile = false;
    document.querySelector('[data-shell-sidebar]')?.classList.remove('is-mobile-open');
    document.querySelector('[data-side-scrim]')?.classList.remove('is-on');
    document.querySelectorAll('[data-side-toggle]').forEach(button => button.setAttribute('aria-expanded', String(!document.querySelector('[data-shell-sidebar]')?.classList.contains('is-collapsed'))));
    lock();
  });
  const inputSelector = 'input:not([type=hidden]):not([type=submit]):not([type=button]),select,textarea,[contenteditable="true"]';
  let lastPageEditable = null;
  document.addEventListener('focusin', event => {
    const element = event.target;
    if (element.matches?.(inputSelector) && element.closest('[data-page-region]') && !element.disabled && !element.readOnly) lastPageEditable = element;
  });
  let layerId = 0;
  function openLayer({ kind = 'modal', title, content, confirmation = false, dirty = () => false, pending = () => false, confirmLeave, onClose = () => {}, opener = document.activeElement }) {
    closeMenu(false);
    const wrapper = document.createElement('div'), scrim = document.createElement('div'), panel = document.createElement('div');
    wrapper.className = kind === 'drawer' ? 'design-drawer-layer' : 'm-wrap';
    scrim.className = kind === 'drawer' ? 'scrim' : 'm-scrim';
    panel.className = kind === 'drawer' ? 'drawer' : `modal${layers.length ? ' is-stacked' : ''}`;
    panel.tabIndex = -1;
    panel.setAttribute('role', confirmation ? 'alertdialog' : 'dialog');
    panel.setAttribute('aria-modal', 'true');
    panel.setAttribute('aria-label', title);
    panel.append(content);
    const heading = panel.querySelector('[data-confirm-title]'), description = panel.querySelector('[data-confirm-description]');
    if (heading) {
      const id = ++layerId; heading.id = `admin-dialog-title-${id}`;
      panel.removeAttribute('aria-label'); panel.setAttribute('aria-labelledby', heading.id);
      if (description) { description.id = `admin-dialog-description-${id}`; panel.setAttribute('aria-describedby', description.id); }
    }
    wrapper.append(panel);
    const host = document.querySelector(kind === 'drawer' ? '[data-drawer-host]' : '[data-modal-host]');
    host.append(scrim, wrapper);
    const inputValues = () => JSON.stringify([...panel.querySelectorAll(inputSelector)].map(input => [input.name || input.id, input.type === 'checkbox' || input.type === 'radio' ? input.checked : input.isContentEditable ? input.textContent : input.value]));
    const baseline = inputValues();
    const layer = { wrapper, scrim, panel, confirmation, dirty: () => dirty() || inputValues() !== baseline, pending, confirmLeave, opener, onClose, closing: false };
    layer.closed = new Promise(resolve => { layer.resolveClosed = resolve; });
    layers.push(layer);
    lock();
    focus(panel.querySelector('[autofocus]') || controls(panel)[0] || panel);
    const outside = event => { if ((event.target === scrim || event.target === wrapper) && layers.at(-1) === layer && !confirmation) void closeLayer(layer); };
    scrim.addEventListener('click', outside);
    wrapper.addEventListener('click', outside);
    return { element: panel, close: result => closeLayer(layer, result, true) };
  }
  async function closeLayer(layer = layers.at(-1), result = false, confirmed = false, navigating = false) {
    if (!layer) return false;
    if (layer.closing) return layer.closed;
    if (layer !== layers.at(-1) || layer.pending()) return false;
    if (!confirmed && layer.dirty() && !await (layer.confirmLeave ? layer.confirmLeave() : confirmDiscard())) return false;
    layer.closing = true;
    layer.panel.classList.add('is-closing');
    layer.scrim.classList.add('is-closing');
    await afterExit(layer.panel);
    layers.pop();
    layer.wrapper.remove(); layer.scrim.remove(); lock();
    focus(layer.opener);
    await layer.onClose(result, { navigating });
    layer.resolveClosed(true);
    return true;
  }
  function template(name) {
    const source = document.querySelector(`template[data-admin-template="${CSS.escape(name)}"]`);
    if (!source) throw new Error(`Missing shared Admin template: ${name}`);
    return source.content.cloneNode(true);
  }
  function confirm({ title, description, actionLabel, cancelLabel, actionClass = 'btn-primary', cancelClass = '', focusAction = false, cancelResult = false }) {
    return new Promise(resolve => {
      const content = template('confirmation');
      content.querySelector('[data-confirm-title]').textContent = title;
      content.querySelector('[data-confirm-description]').textContent = description;
      const cancel = content.querySelector('[data-confirm-cancel]'), accept = content.querySelector('[data-confirm-accept]');
      cancel.className = `btn ${cancelClass}`; accept.className = `btn ${actionClass}`;
      if (focusAction) { cancel.removeAttribute('autofocus'); accept.setAttribute('autofocus', ''); }
      cancel.textContent = cancelLabel; accept.querySelector('[data-component-text]').textContent = actionLabel;
      const layer = openLayer({ title, content, confirmation: true, onClose: resolve });
      cancel.addEventListener('click', () => void layer.close(cancelResult));
      accept.addEventListener('click', () => void layer.close(true));
    });
  }
  const confirmDiscard = () => confirm({ title: text('discardTitle'), description: text('discardDescription'), actionLabel: text('discard'), cancelLabel: text('keepEditing'), actionClass: 'btn-danger' });

  const toastTimers = new Map();
  function removeToast(element) {
    const state = toastTimers.get(element);
    clearTimeout(state?.timer); toastTimers.delete(element);
    element.remove();
  }
  function dismissToast(element) {
    if (!toastTimers.has(element) || element.classList.contains('is-leaving')) return false;
    clearTimeout(toastTimers.get(element).timer); element.classList.add('is-leaving');
    void afterExit(element).then(() => removeToast(element)); return true;
  }
  let toastBar = null;
  const toastBarObserver = new ResizeObserver(() => placeToasts());
  function placeToasts() {
    const host = document.querySelector('[data-toast-host]'), bar = document.querySelector('[data-page-region] .form-bar');
    if (bar !== toastBar) { toastBarObserver.disconnect(); toastBar = bar; if (bar) toastBarObserver.observe(bar); }
    if (!host) return;
    const rect = bar?.getBoundingClientRect();
    const clearance = matchMedia('(max-width:640px)').matches && rect?.height && rect.top < innerHeight && rect.bottom > 0 ? Math.max(20, innerHeight - rect.top + 12) : 20;
    host.style.setProperty('--admin-toast-bottom', `${clearance}px`);
  }
  document.addEventListener('scroll', placeToasts, { capture: true, passive: true });
  window.addEventListener('resize', placeToasts);
  function startToast(element, duration) {
    element.querySelector('[data-toast-close]')?.addEventListener('click', () => dismissToast(element));
    toastTimers.set(element, { timer: setTimeout(() => dismissToast(element), duration) });
    for (const toast of [...toastTimers.keys()]) if (toast.classList.contains('is-leaving')) removeToast(toast);
    while (toastTimers.size > 3) removeToast(toastTimers.keys().next().value);
    placeToasts();
  }
  function toast(message, { error = false, actionLabel, action } = {}) {
    const element = template(error ? 'toast-error' : 'toast').firstElementChild;
    element.querySelector('[data-component-text]').textContent = message;
    if (actionLabel && action) { const button = element.querySelector('.toast-act'); button.hidden = false; button.textContent = actionLabel; button.addEventListener('click', () => { if (dismissToast(element)) action(); }); }
    document.querySelector('[data-toast-host]').append(element);
    startToast(element, actionLabel && action ? 7000 : 4500);
    return element;
  }

  const drafts = new Map();
  const isDirty = () => [...drafts.values()].some(draft => draft.isDirty()) || layers.some(layer => layer.dirty());
  const isPending = () => languagePending || [...drafts.values()].some(draft => draft.isPending?.()) || layers.some(layer => layer.pending());
  const beforeUnload = event => { if (isDirty() || isPending()) { event.preventDefault(); event.returnValue = ''; } };
  let watching = false;
  function refreshDirty() {
    placeToasts();
    const needed = isDirty() || isPending();
    if (needed === watching) return;
    watching = needed;
    window[needed ? 'addEventListener' : 'removeEventListener']('beforeunload', beforeUnload);
  }
  function registerDraft(owner, draft) { drafts.set(owner, draft); refreshDirty(); return () => { drafts.delete(owner); refreshDirty(); }; }
  function trackForm(form) {
    const values = () => JSON.stringify([...new FormData(form)].filter(([name]) => name !== '__RequestVerificationToken').map(([name, value]) => [name, typeof value === 'string' ? value : { name: value.name, size: value.size, type: value.type, lastModified: value.lastModified }]));
    let baseline = values();
    const unregister = registerDraft(form, { isDirty: () => values() !== baseline, discard: () => { form.reset(); baseline = values(); } });
    return { isDirty: () => values() !== baseline, markClean: () => { baseline = values(); refreshDirty(); }, dispose: unregister };
  }
  async function guard() {
    if (isPending()) return false;
    if (!isDirty()) return true;
    const dirtyDrafts = [...drafts.values()].filter(draft => draft.isDirty());
    const custom = dirtyDrafts.find(draft => draft.confirmLeave);
    const returnTarget = lastPageEditable;
    if (!await (custom ? custom.confirmLeave() : confirmDiscard())) {
      if (!layers.length && returnTarget?.isConnected && returnTarget.closest('[data-page-region]')) {
        // A mobile drawer makes the page inert; Keep editing must make it usable.
        if (mobile) toggleSide(false);
        focus(returnTarget);
      }
      return false;
    }
    for (const draft of drafts.values()) draft.discard?.();
    refreshDirty();
    return true;
  }
  const query = {
    parse(search, schema) {
      const params = new URLSearchParams(search), result = {};
      for (const [key, rule] of Object.entries(schema)) {
        const value = params.get(key);
        result[key] = value !== null && rule.valid(value) ? (rule.parse ? rule.parse(value) : value) : rule.default;
      }
      return result;
    },
    build(values, schema) {
      const params = new URLSearchParams();
      for (const [key, rule] of Object.entries(schema)) {
        const value = values[key];
        if (value != null && value !== rule.default && rule.valid(String(value))) params.set(key, value);
      }
      return params.size ? `?${params}` : '';
    }
  };

  let modules = [], navigation = null, sequence = 0, updating = null, updateOverlay = null;
  const positions = new Map(), urlStates = new Set();
  const registerUrlState = handler => { urlStates.add(handler); return () => urlStates.delete(handler); };
  let index = history.state?.adminDesignIndex ?? 0;
  let activeUrl = location.href;
  history.replaceState({ ...history.state, adminDesignIndex: index }, '', activeUrl);
  let travel = null, handlingPop = false;
  const fullLoad = url => location.assign(url);
  async function disposePage() { urlStates.clear(); for (const module of modules.reverse()) await module.dispose(); modules = []; drafts.clear(); refreshDirty(); }
  async function loadModules(doc) {
    const result = [];
    for (const script of doc.querySelectorAll('script[data-admin-page-script]')) {
      const url = new URL(script.getAttribute('src'), location.href);
      if (url.origin !== location.origin || script.type !== 'module') throw new Error('Unexpected page script');
      const module = await import(url.href);
      if (typeof module.init !== 'function' || typeof module.dispose !== 'function') throw new Error('Missing page lifecycle');
      result.push(module);
    }
    return result;
  }
  async function initModules(next) {
    modules = next;
    for (const module of modules) await module.init(document.querySelector('[data-page-region]'), api);
  }
  const stagedStyles = new WeakMap();
  async function preparePageStyles(sources, signal) {
    const created = [], wanted = new Set();
    const owner = {};
    let committed = false;
    const retain = () => { committed = true; for (const link of created) if (stagedStyles.get(link) === owner) stagedStyles.delete(link); };
    const discard = () => { if (!committed) for (const link of created) if (stagedStyles.get(link) === owner) { stagedStyles.delete(link); link.remove(); } };
    try {
      await Promise.all([...sources].map(source => {
        const href = new URL(source.getAttribute('href'), location.href).href;
        if (new URL(href).origin !== location.origin) throw new Error('Unexpected page stylesheet');
        wanted.add(href);
        let link = [...document.querySelectorAll('head link[data-admin-page-style]')].find(link => link.href === href);
        // A failed/unloaded existing link may never fire another event. Reload it.
        if (link && !link.sheet) { stagedStyles.delete(link); link.remove(); link = null; }
        const fresh = !link;
        if (fresh) {
          link = document.createElement('link');
          for (const attribute of source.attributes) link.setAttribute(attribute.name, attribute.value);
          link.href = href;
          link.setAttribute('data-admin-page-style', '');
          // Page CSS is family-scoped, so it can load active without affecting
          // the still-visible leaving page.
        }
        // A successor owns a reused staged link; the cancelled owner cannot remove it.
        if (fresh || stagedStyles.has(link)) { stagedStyles.set(link, owner); created.push(link); }
        return new Promise((resolve, reject) => {
          const finish = error => {
            link.removeEventListener('load', loaded); link.removeEventListener('error', failed);
            signal.removeEventListener('abort', aborted);
            error ? reject(error) : resolve();
          };
          const loaded = () => finish();
          const failed = () => finish(new Error('Page stylesheet failed'));
          const aborted = () => finish(new DOMException('Aborted', 'AbortError'));
          link.addEventListener('load', loaded, { once: true });
          link.addEventListener('error', failed, { once: true });
          signal.addEventListener('abort', aborted, { once: true });
          if (signal.aborted) aborted();
          else if (fresh) document.head.append(link);
          else if (link.sheet) loaded();
        });
      }));
      return { discard, retain, commit() {
        // Remove obsolete styles only after their old content is gone.
        retain();
        for (const link of document.querySelectorAll('head link[data-admin-page-style]')) if (!wanted.has(link.href)) link.remove();
      } };
    } catch (error) { discard(); throw error; }
  }
  const skeletons = new Map();
  const failures = new Map();
  const loadingHeads = new Map();
  const pageTitles = new Map();
  function pageKind(url) {
    const path = new URL(url).pathname.replace(/\/$/, '').toLowerCase();
    if (path === '/admin' || path === '/admin/index') return 'dashboard';
    if (path === '/admin/events' || path === '/admin/events/index' || path === '/admin/events/create') return 'events';
    return path.split('/').at(-2) || 'page';
  }
  function rememberSkeletons(doc) {
    for (const template of doc.querySelectorAll('template[data-page-loading-template]')) {
      const content = template.content.cloneNode(true), styles = [...content.querySelectorAll('link[rel="stylesheet"]')];
      styles.forEach(link => link.remove());
      skeletons.set(template.dataset.pageLoadingTemplate, { content, styles });
      if (template.dataset.pageTitle) pageTitles.set(template.dataset.pageLoadingTemplate, template.dataset.pageTitle);
    }
    for (const template of doc.querySelectorAll('template[data-page-failure-template]')) {
      const content = template.content.cloneNode(true);
      content.querySelectorAll('link[rel="stylesheet"]').forEach(link => link.remove());
      failures.set(template.dataset.pageFailureTemplate, content);
    }
    for (const template of doc.querySelectorAll('template[data-page-header-template]')) loadingHeads.set(template.dataset.pageHeaderTemplate, template.content.cloneNode(true));
  }
  rememberSkeletons(document);
  let overlay = null;
  let pendingEvents = null;
  function bindPendingEvents(element, url, failed = false) {
    pendingEvents?.abort(); pendingEvents = new AbortController();
    const signal = pendingEvents.signal, state = new URL(url), params = state.searchParams;
    const view = params.get('view') || (params.get('filter') === 'hidden' ? 'hidden' : 'all');
    const phase = params.get('phase') || 'all', sort = params.get('sort') || 'default';
    const direction = params.get('direction') === 'desc' ? 'desc' : 'asc';
    const table = element.querySelector('[data-events-pending]');
    if (!table) return;
    table.dataset.query = state.search;
    const change = values => {
      const next = new URL(state); for (const [key, value] of Object.entries(values)) value ? next.searchParams.set(key, value) : next.searchParams.delete(key);
      next.searchParams.delete('filter'); void navigate(next.href, { mode: 'replace' });
    };
    for (const input of table.querySelectorAll('[data-pending-view]')) {
      input.checked = input.dataset.pendingView === view; input.closest('.tab').classList.toggle('is-on', input.checked);
      if (failed) input.addEventListener('change', () => change({view: input.dataset.pendingView, phase: 'all', page: '1'}), {signal});
    }
    const search = table.querySelector('[data-pending-search]'); search.value = params.get('search') || '';
    if (failed) search.addEventListener('keydown', event => { if (event.key === 'Enter') { event.preventDefault(); change({search: search.value, page: '1'}); } }, {signal});
    const attention = table.querySelector('[data-pending-attention]'); attention.hidden = params.get('attention') !== '1';
    if (failed) attention.addEventListener('click', () => change({attention: null, page: '1'}), {signal});
    const menu = table.querySelector('[data-pending-phase-menu]'), button = table.querySelector('[data-pending-phase-button]');
    menu.id = 'pending-events-phase-menu'; button.dataset.menuTarget = menu.id;
    button.dataset.menuAlign = 'end'; button.classList.toggle('is-active', phase !== 'all');
    for (const option of menu.querySelectorAll('[data-pending-phase]')) {
      const selected = option.dataset.pendingPhase === phase; option.setAttribute('aria-checked', String(selected));
      if (selected) table.querySelector('[data-pending-phase-value]').textContent = option.textContent;
      if (failed) option.addEventListener('click', () => { closeMenu(false); change({phase: option.dataset.pendingPhase, page: '1'}); }, {signal});
    }
    for (const column of table.querySelectorAll('[data-pending-column]')) {
      const selected = column.dataset.pendingColumn === sort;
      column.setAttribute('aria-sort', selected ? (direction === 'desc' ? 'descending' : 'ascending') : 'none');
      column.classList.toggle('is-sorted', selected); column.querySelector('.sort-ic').classList.toggle('is-desc', selected && direction === 'desc'); column.querySelector('.sort-ic').classList.toggle('is-asc', selected && direction === 'asc');
      if (failed) column.querySelector('button').addEventListener('click', () => change({sort: column.dataset.pendingColumn, direction: selected && direction === 'asc' ? 'desc' : 'asc', page: '1'}), {signal});
    }
  }
  const contexts = new Map();
  function contextSnapshot() {
    const snapshot = document.implementation.createHTMLDocument();
    for (const element of document.querySelectorAll('[data-shell-sidebar],[data-shell-topbar],[data-shell-menu]')) snapshot.body.append(element.cloneNode(true));
    return snapshot;
  }
  function clearOverlay(restore = true) {
    pendingEvents?.abort(); pendingEvents = null;
    if (!overlay) return;
    const previous = overlay; overlay = null;
    previous.element.remove();
    for (const saved of previous.children) {
      saved.element.hidden = saved.hidden; saved.element.inert = saved.inert;
      if (saved.aria === null) saved.element.removeAttribute('aria-hidden'); else saved.element.setAttribute('aria-hidden', saved.aria);
    }
    if (previous.busy === null) previous.main.removeAttribute('aria-busy'); else previous.main.setAttribute('aria-busy', previous.busy);
    if (restore) { refreshContext(previous.context); restorePosition(previous.position); }
  }
  function destinationContext(url) {
    const cached = contexts.get(url);
    if (cached) { refreshContext(cached); return; }
    const eventId = new URL(url).pathname.split('/').at(-1);
    const choice = [...document.querySelectorAll('[data-event-id]')].find(link => link.href === url || link.dataset.eventId === eventId);
    if (!choice) return;
    const context = document.querySelector('[data-shell-event-context]');
    const oldId = context?.dataset.selectedEventId;
    const name = choice.dataset.eventName;
    const setText = (selector, value) => { const node = document.querySelector(selector); if (node && value !== undefined) node.textContent = value; };
    setText('.ev-name', name); setText('.crumb-mid', name);
    setText('.ev-meta-text', `${choice.dataset.eventStage} · ${choice.dataset.eventWhen}`);
    const dot = context?.querySelector('.dot'); if (dot) dot.className = `dot ${choice.dataset.eventTone}`;
    if (context) {
      context.dataset.selectedEventId = choice.dataset.eventId;
      if (oldId) for (const link of context.querySelectorAll('a[data-shell-link]')) link.href = link.href.replace(oldId, choice.dataset.eventId);
    }
  }
  function skeleton(url, originalPosition = rememberPosition(), shownAt = performance.now()) {
    pendingEvents?.abort(); pendingEvents = null;
    const previous = overlay;
    const main = document.querySelector('[data-page-region]'), context = previous?.context || contextSnapshot(), position = previous?.position || originalPosition;
    contexts.set(activeUrl, context);
    const children = previous?.children || [...main.children].map(element => ({ element, hidden: element.hidden, inert: element.inert, aria: element.getAttribute('aria-hidden') }));
    const busy = previous ? previous.busy : main.getAttribute('aria-busy');
    for (const saved of children) { saved.element.hidden = true; saved.element.inert = true; saved.element.setAttribute('aria-hidden', 'true'); }
    destinationContext(url);
    const kind = pageKind(url);
    const destination = [...document.querySelectorAll('[data-shell-link]')].find(link => link.href === url);
    const title = pageTitles.get(kind) || destination?.dataset.pageTitle || destination?.querySelector('.nav-text')?.textContent || text('loading');
    const crumb = document.querySelector('.crumb-cur'); if (crumb) crumb.textContent = title;
    const placeholder = document.createElement('div'); placeholder.className = 'page'; placeholder.dataset.pageSkeleton = kind; placeholder.dataset.pageFamily = kind;
    placeholder.setAttribute('role', 'status'); placeholder.setAttribute('aria-label', text('loading')); placeholder.setAttribute('aria-busy', 'true');
    const head = loadingHeads.get(kind)?.cloneNode(true).firstElementChild || main.querySelector('.page-head')?.cloneNode(true);
    if (head) {
      head.hidden = false; head.inert = false; head.removeAttribute('aria-hidden');
      head.querySelector('.h1').textContent = title;
      // A fallback header supplies a title, never the previous page's actions.
      if (!loadingHeads.has(kind)) [...head.children].slice(1).forEach(element => element.remove());
      head.querySelectorAll('[id]').forEach(element => element.removeAttribute('id'));
      const summary = head.querySelector('[data-summary-template]');
      if (summary) summary.textContent = summary.dataset.summaryTemplate.replace('{0}', () => document.querySelector('.ev-name')?.textContent || '');
      if (!loadingHeads.has(kind)) {
        [...head.children].slice(1).forEach(element => element.remove());
        head.querySelector('.summary')?.remove();
      }
      placeholder.append(head);
    }
    const provided = skeletons.get(kind); placeholder.dataset.skeletonLayout = provided ? 'page' : 'generic';
    placeholder.append(provided ? provided.content.cloneNode(true) : template('loading'));
    if (previous) previous.element.replaceWith(placeholder); else main.append(placeholder);
    main.setAttribute('aria-busy', 'true');
    overlay = { element: placeholder, main, children, busy, context, position, shownAt };
    if (kind === 'events') bindPendingEvents(placeholder, url);
  }
  function refreshSidebar(doc, translated = false) {
    const side = document.querySelector('[data-shell-sidebar]'), next = doc.querySelector('[data-shell-sidebar]');
    if (translated) { const scroll = side.scrollTop; side.replaceChildren(...document.importNode(next, true).childNodes); side.scrollTop = scroll; return; }
    const context = side.querySelector('[data-shell-event-context]'), nextContext = next.querySelector('[data-shell-event-context]');
    if (context && nextContext) { context.replaceChildren(...document.importNode(nextContext, true).childNodes); context.dataset.selectedEventId = nextContext.dataset.selectedEventId || ''; }
    const links = [...side.querySelectorAll('a[data-shell-link]')], replacements = [...next.querySelectorAll('a[data-shell-link]')];
    if (links.length !== replacements.length) throw new Error('Unexpected sidebar navigation');
    links.forEach((link, index) => {
      const replacement = replacements[index];
      link.setAttribute('href', replacement.getAttribute('href'));
      link.classList.toggle('is-current', replacement.classList.contains('is-current'));
      if (replacement.hasAttribute('aria-current')) link.setAttribute('aria-current', replacement.getAttribute('aria-current')); else link.removeAttribute('aria-current');
    });
  }
  function refreshContext(doc) {
    closeMenu(false);
    refreshSidebar(doc);
    for (const selector of ['[data-shell-topbar]', '[data-shell-menu]']) {
      const old = [...document.querySelectorAll(selector)], replacements = [...doc.querySelectorAll(selector)];
      if (old.length !== replacements.length) throw new Error('Unexpected shell context');
      old.forEach((element, index) => element.replaceWith(document.importNode(replacements[index], true)));
    }
    window.adminDesignTheme?.apply(); paintSideLabel();
  }
  function rememberPosition() {
    const active = document.activeElement;
    const selector = active?.id ? `#${CSS.escape(active.id)}`
      : active?.matches('[data-shell-language] button') ? `[data-shell-language] button[value="${CSS.escape(active.value)}"]`
      : active?.name ? `[name="${CSS.escape(active.name)}"]`
      : active?.getAttribute('aria-label') ? `[aria-label="${CSS.escape(active.getAttribute('aria-label'))}"]` : null;
    const scroller = document.querySelector('[data-page-region]');
    return { active, selector, regions: [...document.querySelectorAll('[data-admin-scroll-region][id]')].map(element => ({ id: element.id, top: element.scrollTop, left: element.scrollLeft })), selection: typeof active?.selectionStart === 'number' ? [active.selectionStart, active.selectionEnd, active.selectionDirection] : null, top: scroller?.scrollTop || 0, left: scroller?.scrollLeft || 0 };
  }
  function restorePosition(position) {
    const scroller = document.querySelector('[data-page-region]');
    if (scroller) { scroller.scrollTop = position.top; scroller.scrollLeft = position.left; }
    const target = position.active?.isConnected ? position.active : position.selector ? document.querySelector(position.selector) : document.querySelector('.h1');
    focus(target);
    if (position.selection && typeof target?.setSelectionRange === 'function') target.setSelectionRange(...position.selection);
    for (const saved of position.regions || []) { const element = document.getElementById(saved.id); if (element) { element.scrollTop = saved.top; element.scrollLeft = saved.left; } }
  }
  async function closeNavigationLayers() {
    while (layers.length) if (!await closeLayer(layers.at(-1), false, true, true)) return false;
    return true;
  }
  function validatePage(doc, response, language) {
    if (!doc.querySelector('[data-page-region]') || !doc.querySelector('body[data-admin-design]')
        || !doc.querySelector('[data-shell-sidebar]') || !doc.querySelector('[data-shell-topbar]')
        || !doc.querySelector('[data-shell-antiforgery]') || (response.redirected && !language)) return false;
    if ([...doc.querySelectorAll('script')].some(script => !script.hasAttribute('data-admin-page-script') && !script.hasAttribute('data-admin-shell-script'))) return false;
    if ([...doc.querySelectorAll('script[src],link[data-admin-page-style]')].some(node => new URL(node.getAttribute('src') || node.getAttribute('href'), location.href).origin !== location.origin)) return false;
    return ['[data-page-region]', '[data-shell-topbar]', '[data-shell-menu]', '[data-shell-antiforgery]'].every(selector => document.querySelectorAll(selector).length === doc.querySelectorAll(selector).length);
  }
  async function navigate(url, { mode = 'push', targetIndex, check = true, language = false, response: suppliedResponse, position: suppliedPosition, rollbackContext } = {}) {
    url = new URL(url, location.href).href;
    if (check && !await guard()) return false;
    if (!await closeNavigationLayers()) return false;
    const inheritedUpdate = updateOverlay; updateOverlay = null;
    updating?.abort(); navigation?.abort();
    if (language) inheritedUpdate?.restore();
    if (language) clearOverlay();
    if (body.dataset.navigationEnabled.toLowerCase() === 'false') { fullLoad(url); return true; }
    const ticket = ++sequence;
    const controller = navigation = new AbortController();
    closeMenu(false);
    if (mobile && !language) toggleSide(false);
    const position = suppliedPosition || overlay?.position || rememberPosition();
    if (!language) positions.set(index, position);
    const currentMain = document.querySelector('[data-page-region]'), wasInert = currentMain.inert;
    let restored = false;
    const restoreMain = () => { if (!restored) { currentMain.inert = wasInert; restored = true; } };
    // Preserve the current display during the delay without accepting edits that
    // could be lost by the already-guarded navigation.
    if (!language) currentMain.inert = true;
    controller.signal.addEventListener('abort', restoreMain, { once: true });
    const knownStyles = !language ? skeletons.get(pageKind(url))?.styles || [] : [];
    let stylesReady = !knownStyles.length, earlyStyles = null, styleFailure = false, preload = Promise.resolve();
    const loading = !language ? delayedLoading(shownAt => {
      skeleton(url, position, shownAt);
      // A shown skeleton owns its loaded CSS until its content is replaced,
      // even if a successor is still waiting for a different family's CSS.
      earlyStyles?.retain(); inheritedUpdate?.restore(); restoreMain();
    }, controller.signal, overlay?.element.getAttribute('aria-busy') === 'true' ? overlay.shownAt : inheritedUpdate?.shownAt ?? null, () => stylesReady) : null;
    let receivedPage = false, pageStyles = null;
    const fallback = destination => { restoreMain(); clearOverlay(); if (rollbackContext) { refreshContext(rollbackContext); restorePosition(position); } fullLoad(destination); return true; };
    try {
      if (knownStyles.length) preload = preparePageStyles(knownStyles, controller.signal).then(prepared => {
        earlyStyles = prepared; stylesReady = true; loading?.ready();
      }).catch(error => { styleFailure = true; throw error; });
      const [response] = await Promise.all([suppliedResponse || fetch(url, { credentials: 'same-origin', signal: controller.signal, headers: { 'X-Admin-Navigation': 'true' } }), preload]);
      if (!response.ok) throw new Error('Page load failed');
      const html = await response.text(); receivedPage = true;
      const doc = new DOMParser().parseFromString(html, 'text/html');
      if (ticket !== sequence) return false;
      if (!validatePage(doc, response, language)) return fallback(response.url || url);
      pageStyles = await preparePageStyles(doc.querySelectorAll('link[data-admin-page-style]'), controller.signal);
      if (ticket !== sequence) return false;
      const nextModules = await loadModules(doc);
      if (ticket !== sequence) return false;
      await loading?.complete();
      if (ticket !== sequence) return false;
      restoreMain();
      // Compatibility and imports are known before any old module is disposed.
      await disposePage();
      clearOverlay(false);
      rememberSkeletons(doc);
      document.title = doc.title; document.documentElement.lang = doc.documentElement.lang;
      const nextBody = doc.querySelector('body[data-admin-design]');
      for (const [key, value] of Object.entries(nextBody.dataset)) body.dataset[key] = value;
      // Translated content is the same page, so only the language cross-fade runs.
      if (language) doc.querySelectorAll('[data-page-region] .fade-in').forEach(element => element.classList.remove('fade-in'));
      refreshSidebar(doc, language);
      for (const selector of ['[data-page-region]', '[data-shell-topbar]', '[data-shell-menu]', '[data-shell-antiforgery]', 'template[data-admin-template]']) {
        const old = [...document.querySelectorAll(selector)], replacements = [...doc.querySelectorAll(selector)];
        old.forEach((element, i) => { if (replacements[i]) element.replaceWith(document.importNode(replacements[i], true)); });
      }
      pageStyles.commit();
      earlyStyles?.retain();
      for (const notice of doc.querySelectorAll('[data-toast-host] [data-toast]')) toast(notice.querySelector('.grow')?.textContent || notice.textContent, { error: notice.classList.contains('is-error') });
      window.adminDesignTheme?.apply(); paintSideLabel();
      if (mode === 'push') {
        // A new navigation can replace an in-flight pop load. Its physical history
        // entry already moved, even though that old load has not committed index.
        index = (history.state?.adminDesignIndex ?? index) + 1;
        history.pushState({ adminDesignIndex: index }, '', url);
      }
      else if (mode === 'pop') index = targetIndex;
      else if (mode === 'replace') history.replaceState({ ...history.state, adminDesignIndex: index }, '', url);
      activeUrl = url;
      await initModules(nextModules);
      if (language) {
        restorePosition(position);
        if (!reducedMotion()) for (const element of document.querySelectorAll('[data-shell-sidebar],[data-shell-topbar],[data-page-region]')) {
          element.dataset.languageTransition = '';
          element.addEventListener('animationend', () => element.removeAttribute('data-language-transition'), { once: true });
        }
      } else if (mode === 'pop' && positions.has(targetIndex)) restorePosition(positions.get(targetIndex));
      else focus(document.querySelector('.h1'));
      document.dispatchEvent(new CustomEvent('admin:page-changed', { detail: { url } }));
      return true;
    } catch (error) {
      if (error.name === 'AbortError' || ticket !== sequence) return false;
      if (receivedPage || language || styleFailure) { console.warn('Admin page swap could not finish.', error); return fallback(url); }
      // A failed HTTP read still needs the remembered family CSS before its
      // decided failure UI can replace the visible page.
      try { await preload; }
      catch (styleError) { if (styleError.name === 'AbortError' || ticket !== sequence) return false; return fallback(url); }
      await loading?.complete();
      if (ticket !== sequence) return false;
      loading?.cancel();
      if (!overlay) skeleton(url, position); // Fast failures show only the decided failure state.
      earlyStyles?.retain(); // Failure placeholders own their family CSS too.
      restoreMain();
      const placeholder = overlay.element;
      placeholder.setAttribute('aria-busy', 'false'); overlay.main.removeAttribute('aria-busy');
      placeholder.querySelector('[data-dashboard-loading-card]')?.remove();
      for (const child of [...placeholder.children]) if (!child.classList.contains('page-head')) child.remove();
      const failed = failures.get(pageKind(url))?.cloneNode(true) || template('load-failure');
      const retry = failed.querySelector('[data-load-retry]');
      retry.addEventListener('click', () => void navigate(url, { mode, targetIndex, check: false }));
      placeholder.append(failed); focus(retry);
      if (pageKind(url) === 'events') {
        placeholder.dataset.eventsLoadFailed = '';
        placeholder.querySelector('.summary')?.replaceChildren();
        bindPendingEvents(placeholder, url, true);
      }
      return false;
    } finally { pageStyles?.discard(); earlyStyles?.discard(); loading?.cancel(); restoreMain(); }
  }
  // Shared in-page reads: page adapters supply only fragments/presentation,
  // never their own transport, delayed-loading clock or URL/focus/scroll rules.
  function supersedeUpdate() {
    const inherited = updateOverlay; updateOverlay = null;
    updating?.abort(); updating = null;
    // A fresh owner prevents the cancelled request's finally from clearing it.
    if (inherited) updateOverlay = { ...inherited };
  }
  async function update(url, { root, results, patch, pending, failed, fallbackFocus, signal, scrollRegions = [], current = () => true, draft = {} }) {
    if (!await guard()) return false;
    const inherited = updateOverlay?.results === results ? updateOverlay : null;
    if (inherited) updateOverlay = null; // Transfer display ownership before abort cleanup.
    updating?.abort();
    const controller = updating = new AbortController();
    const cancel = () => controller.abort();
    signal?.addEventListener('abort', cancel, { once: true });
    const before = rememberPosition();
    let restore, shown = false, display;
    const loading = delayedLoading(shownAt => {
      shown = true;
      if (inherited) {
        inherited.placeholder.replaceChildren(pending());
        display = updateOverlay = { ...inherited }; restore = inherited.restore; return;
      }
      const children = [...results.children].map(element => ({ element, hidden: element.hidden }));
      const minimum = results.style.minHeight, busy = results.getAttribute('aria-busy');
      results.style.minHeight = results.getBoundingClientRect().height + 'px';
      children.forEach(value => { value.element.hidden = true; });
      const placeholder = document.createElement('div'); placeholder.dataset.updateSkeleton = '';
      placeholder.append(pending()); results.append(placeholder); results.setAttribute('aria-busy', 'true');
      restore = () => {
        placeholder.remove(); children.forEach(value => { value.element.hidden = value.hidden; });
        results.style.minHeight = minimum;
        if (busy === null) results.removeAttribute('aria-busy'); else results.setAttribute('aria-busy', busy);
      };
      display = updateOverlay = { results, placeholder, restore, shownAt };
    }, controller.signal, inherited?.shownAt ?? null);
    const clear = () => { if (display && updateOverlay === display) { restore?.(); updateOverlay = null; } restore = null; };
    controller.signal.addEventListener('abort', clear, { once: true });
    const preserve = action => {
      const position = rememberPosition();
      if (position.active === body && shown) Object.assign(position, { active: before.active, selector: before.selector, selection: before.selection });
      const scroll = scrollRegions.map(element => ({ element, left: element.scrollLeft, top: element.scrollTop }));
      clear();
      action();
      const target = position.active?.isConnected ? position.active : document.querySelector(position.selector || ':not(*)');
      if (!target || target.disabled || !target.getClientRects().length || target.closest('[inert]')) {
        position.active = fallbackFocus(position); position.selector = null;
      }
      restorePosition(position);
      for (const value of scroll) { value.element.scrollLeft = value.left; value.element.scrollTop = value.top; }
    };
    try {
      const outcome = await window.AdminFetch.request(url, { expect: 'html', signal: controller.signal, headers: { 'X-Admin-Navigation': 'true' }, notice: false });
      if (controller.signal.aborted || !root.isConnected || !current()) return false;
      if (outcome.kind === 'session-lost') {
        clear();
        window.AdminFetch.sessionNotice(typeof draft === 'function' ? draft() : draft, outcome.destination);
        return false;
      }
      if (outcome.kind !== 'handler') throw new Error('Results load failed');
      const doc = new DOMParser().parseFromString(outcome.data, 'text/html');
      if (!validatePage(doc, outcome.response, false)) throw new Error('Results response is incompatible');
      await loading.complete();
      if (controller.signal.aborted || !root.isConnected || !current()) return false;
      preserve(() => {
        const canonical = patch(doc);
        history.replaceState({ ...history.state, adminDesignIndex: index }, '', canonical || url);
        activeUrl = location.href;
      });
      return true;
    } catch (error) {
      if (controller.signal.aborted || !root.isConnected || !current()) return false;
      await loading.complete();
      if (controller.signal.aborted || !root.isConnected || !current()) return false;
      preserve(() => {
        results.replaceChildren(failed());
        results.querySelector('[data-load-retry]').addEventListener('click', () => void update(url, { root, results, patch, pending, failed, fallbackFocus, signal, scrollRegions, current, draft }));
      });
      return false;
    } finally {
      loading.cancel(); clear(); signal?.removeEventListener('abort', cancel);
      if (updating === controller) updating = null;
    }
  }
  let languagePending = false;
  async function changeLanguage(form, button) {
    if (languagePending || !button || button.classList.contains('is-on')) return;
    if (!await guard() || !await closeNavigationLayers()) return;
    updating?.abort(); navigation?.abort(); clearOverlay();
    const rollbackContext = contextSnapshot();
    languagePending = true; refreshDirty();
    const position = rememberPosition(); position.active = button; position.selector = `[data-shell-language] button[value="${CSS.escape(button.value)}"]`;
    const oldLanguage = document.documentElement.lang;
    const buttons = [...form.querySelectorAll('button[name="culture"]')];
    const highlight = value => buttons.forEach(item => { item.classList.toggle('is-on', item.value === value); item.setAttribute('aria-pressed', String(item.value === value)); });
    highlight(button.value);
    buttons.forEach(item => item.disabled = true);
    try {
      const data = new FormData(form); data.set(button.name, button.value); data.set('returnUrl', location.pathname + location.search);
      const response = await fetch(form.action, { method: 'POST', credentials: 'same-origin', body: data });
      await navigate(location.href, { mode: 'replace', check: false, language: true, response, position, rollbackContext });
    } catch {
      highlight(oldLanguage); clearOverlay(); location.reload();
    } finally { languagePending = false; buttons.forEach(item => item.disabled = false); refreshDirty(); }
  }
  function goTo(target) { return new Promise(resolve => { travel = { target, resolve }; history.go(target - (history.state?.adminDesignIndex ?? index)); }); }
  async function onPop(event) {
    const target = event.state?.adminDesignIndex, url = location.href;
    if (travel) { if (target === travel.target) { const resolve = travel.resolve; travel = null; resolve(); } return; }
    if (overlay && url === activeUrl) { navigation?.abort(); sequence++; clearOverlay(); return; }
    const currentDocument = new URL(activeUrl), destinationDocument = new URL(url);
    if ((target === undefined || target === index) && currentDocument.origin === destinationDocument.origin
        && currentDocument.pathname === destinationDocument.pathname && currentDocument.search === destinationDocument.search) {
      // Adopt the native fragment entry without adding another history entry.
      // Its distinct index lets later Back/Forward restore dirty pages correctly.
      if (target === undefined) history.replaceState({ adminDesignIndex: ++index }, '', url);
      activeUrl = url;
      return;
    }
    if (handlingPop) return;
    if (target === undefined) { if (await guard()) location.reload(); else history.pushState({ adminDesignIndex: index }, '', activeUrl); return; }
    handlingPop = true;
    try {
      if (isDirty() || isPending()) {
        await goTo(index);
        if (!await guard()) return;
        await goTo(target);
      }
      for (const handler of urlStates) if (await handler(url, activeUrl)) { index = target; activeUrl = url; return; }
      await navigate(url, { mode: 'pop', targetIndex: target, check: false });
    } finally { handlingPop = false; }
  }
  async function backUrl() { const target = index - 1; await goTo(target); index = target; activeUrl = location.href; }
  function setUrl(values, schema, { record = false } = {}) {
    const url = new URL(location.href); url.search = query.build(values, schema);
    if (record) index++;
    history[record ? 'pushState' : 'replaceState']({ adminDesignIndex: index }, '', url); activeUrl = url.href;
  }
  const api = window.AdminUI = { template, trapTab, openLayer, closeLayer, closeMenu, confirm, confirmDiscard, toast, busy, reducedMotion, registerDraft, trackForm, refreshDirty, guard, query, setUrl, backUrl, registerUrlState, navigate, update, supersedeUpdate, refreshContext };
  document.addEventListener('submit', event => { if (event.target.matches('[data-shell-language]')) { event.preventDefault(); void changeLanguage(event.target, event.submitter); } });
  document.addEventListener('input', refreshDirty);
  document.addEventListener('change', refreshDirty);
  document.addEventListener('pointerdown', () => body.classList.remove('using-keyboard'));
  document.addEventListener('keydown', event => {
    if (event.key === 'Tab') body.classList.add('using-keyboard');
    if (menu) {
      const items = controls(menu.element), at = items.indexOf(document.activeElement);
      if (event.key === 'ArrowDown' || event.key === 'ArrowUp' || event.key === 'Home' || event.key === 'End') {
        event.preventDefault(); const target = event.key === 'Home' ? 0 : event.key === 'End' ? items.length - 1 : (at + (event.key === 'ArrowDown' ? 1 : -1) + items.length) % items.length; focus(items[target]);
      } else if (event.key === 'Escape' || event.key === 'Tab') { event.preventDefault(); closeMenu(); }
      return;
    }
    if (layers.length) { trapTab(event, layers.at(-1).panel); if (event.key === 'Escape') { event.preventDefault(); void closeLayer(); } return; }
    if (mobile) { trapTab(event, document.querySelector('[data-shell-sidebar]')); if (event.key === 'Escape') toggleSide(false); }
  });
  document.addEventListener('click', event => {
    const opener = event.target.closest('[data-menu-target]');
    if (opener) { openMenu(opener); return; }
    if (menu && !menu.element.contains(event.target) && !menu.element.querySelector(inputSelector)) closeMenu(false);
    if (event.target.closest('[data-side-toggle]')) { toggleSide(); return; }
    if (event.target.closest('[data-side-scrim]')) { toggleSide(false); return; }
    const link = event.target.closest('a[data-shell-link]');
    if (!link || event.button !== 0 || event.metaKey || event.ctrlKey || event.shiftKey || event.altKey || link.target || link.hasAttribute('download')) return;
    const url = new URL(link.href);
    if (url.origin !== location.origin) return;
    event.preventDefault(); void navigate(url.href);
  });
  window.addEventListener('popstate', event => void onPop(event));
  window.addEventListener('pageshow', event => { if (event.persisted) { navigation?.abort(); sequence++; clearOverlay(); location.reload(); } });
  document.querySelectorAll('[data-toast]').forEach(element => startToast(element, 4500));
  paintSideLabel();
  void loadModules(document).then(initModules).catch(() => toast(text('loadError'), { error: true }));
})();

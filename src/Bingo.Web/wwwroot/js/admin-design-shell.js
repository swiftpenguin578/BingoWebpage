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
  async function busy(request, quick = false) {
    const started = performance.now();
    try { return await request(); }
    finally {
      const remaining = (quick ? 250 : 600) - (performance.now() - started);
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
  function paintSideLabel() {
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
  let layerId = 0;
  function openLayer({ kind = 'modal', title, content, confirmation = false, dirty = () => false, pending = () => false, onClose = () => {}, opener = document.activeElement }) {
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
    const layer = { wrapper, scrim, panel, confirmation, dirty: () => dirty() || inputValues() !== baseline, pending, opener, onClose, closing: false };
    layer.closed = new Promise(resolve => { layer.resolveClosed = resolve; });
    layers.push(layer);
    lock();
    focus(panel.querySelector('[autofocus]') || controls(panel)[0] || panel);
    const outside = event => { if ((event.target === scrim || event.target === wrapper) && layers.at(-1) === layer && !confirmation && !panel.querySelector(inputSelector) && !layer.dirty()) void closeLayer(layer); };
    scrim.addEventListener('click', outside);
    wrapper.addEventListener('click', outside);
    return { element: panel, close: result => closeLayer(layer, result, true) };
  }
  async function closeLayer(layer = layers.at(-1), result = false, confirmed = false) {
    if (!layer) return false;
    if (layer.closing) return layer.closed;
    if (layer !== layers.at(-1) || layer.pending()) return false;
    if (!confirmed && layer.dirty() && !await confirmDiscard()) return false;
    layer.closing = true;
    layer.panel.classList.add('is-closing');
    layer.scrim.classList.add('is-closing');
    await afterExit(layer.panel);
    layers.pop();
    layer.wrapper.remove(); layer.scrim.remove(); lock();
    focus(layer.opener);
    layer.onClose(result);
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
  function startToast(element, duration) {
    const state = { remaining: duration, started: performance.now(), timer: null };
    const resume = () => { state.started = performance.now(); state.timer = setTimeout(async () => { element.classList.add('is-leaving'); await afterExit(element); removeToast(element); }, state.remaining); };
    element.addEventListener('mouseenter', () => { clearTimeout(state.timer); state.remaining = Math.max(0, state.remaining - (performance.now() - state.started)); });
    element.addEventListener('mouseleave', resume);
    element.querySelector('[data-toast-close]')?.addEventListener('click', () => removeToast(element));
    toastTimers.set(element, state); resume();
    while (toastTimers.size > 3) removeToast(toastTimers.keys().next().value);
  }
  function toast(message, { error = false, actionLabel, action } = {}) {
    const element = template(error ? 'toast-error' : 'toast').firstElementChild;
    element.querySelector('[data-component-text]').textContent = message;
    if (actionLabel && action) { const button = element.querySelector('.toast-act'); button.hidden = false; button.textContent = actionLabel; button.addEventListener('click', action); }
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
    if (!await (custom ? custom.confirmLeave() : confirmDiscard())) return false;
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

  let modules = [], navigation = null, sequence = 0;
  let index = history.state?.adminDesignIndex ?? 0;
  let activeUrl = location.href;
  history.replaceState({ ...history.state, adminDesignIndex: index }, '', activeUrl);
  let travel = null, handlingPop = false;
  const fullLoad = url => location.assign(url);
  async function disposePage() { for (const module of modules.reverse()) await module.dispose(); modules = []; drafts.clear(); refreshDirty(); }
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
  const skeletons = new Map();
  function rememberSkeletons(doc) {
    for (const template of doc.querySelectorAll('template[data-page-loading-template]')) skeletons.set(template.dataset.pageLoadingTemplate, template.content.cloneNode(true));
  }
  rememberSkeletons(document);
  let overlay = null;
  const contexts = new Map();
  function contextSnapshot() {
    const snapshot = document.implementation.createHTMLDocument();
    for (const element of document.querySelectorAll('[data-shell-sidebar],[data-shell-topbar],[data-shell-menu]')) snapshot.body.append(element.cloneNode(true));
    return snapshot;
  }
  function clearOverlay(restore = true) {
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
    const choice = [...document.querySelectorAll('[data-event-id]')].find(link => link.href === url);
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
  function skeleton(url) {
    clearOverlay();
    const main = document.querySelector('[data-page-region]'), context = contextSnapshot(), position = rememberPosition();
    contexts.set(activeUrl, context);
    const children = [...main.children].map(element => ({ element, hidden: element.hidden, inert: element.inert, aria: element.getAttribute('aria-hidden') }));
    const busy = main.getAttribute('aria-busy');
    for (const saved of children) { saved.element.hidden = true; saved.element.inert = true; saved.element.setAttribute('aria-hidden', 'true'); }
    destinationContext(url);
    const kind = new URL(url).pathname.split('/').at(-2)?.toLowerCase() || 'page';
    const placeholder = document.createElement('div'); placeholder.className = 'page'; placeholder.dataset.pageSkeleton = kind;
    placeholder.setAttribute('role', 'status'); placeholder.setAttribute('aria-label', text('loading')); placeholder.setAttribute('aria-busy', 'true');
    const head = main.querySelector('.page-head')?.cloneNode(true);
    if (head) {
      head.hidden = false; head.inert = false; head.removeAttribute('aria-hidden');
      head.querySelectorAll('[id]').forEach(element => element.removeAttribute('id'));
      const summary = head.querySelector('[data-summary-template]');
      if (summary) summary.textContent = summary.dataset.summaryTemplate.replace('{0}', document.querySelector('.ev-name')?.textContent || '');
      placeholder.append(head);
    }
    const provided = skeletons.get(kind); placeholder.dataset.skeletonLayout = provided ? 'page' : 'generic';
    placeholder.append(provided ? provided.cloneNode(true) : template('loading'));
    main.append(placeholder); main.setAttribute('aria-busy', 'true');
    overlay = { element: placeholder, main, children, busy, context, position };
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
      : active?.name ? `[name="${CSS.escape(active.name)}"]` : null;
    const scroller = document.querySelector('[data-page-region]');
    return { active, selector, top: scroller?.scrollTop || 0, left: scroller?.scrollLeft || 0 };
  }
  function restorePosition(position) {
    const scroller = document.querySelector('[data-page-region]');
    if (scroller) { scroller.scrollTop = position.top; scroller.scrollLeft = position.left; }
    focus(position.active?.isConnected ? position.active : position.selector ? document.querySelector(position.selector) : document.querySelector('.h1'));
  }
  async function closeNavigationLayers() {
    while (layers.length) if (!await closeLayer(layers.at(-1), false, true)) return false;
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
    navigation?.abort(); clearOverlay();
    if (body.dataset.navigationEnabled.toLowerCase() === 'false') { fullLoad(url); return true; }
    const ticket = ++sequence;
    navigation = new AbortController();
    closeMenu(false);
    if (mobile) toggleSide(false);
    const position = suppliedPosition || rememberPosition();
    if (!language) skeleton(url);
    let receivedPage = false;
    const fallback = destination => { clearOverlay(); if (rollbackContext) { refreshContext(rollbackContext); restorePosition(position); } fullLoad(destination); return true; };
    try {
      const response = suppliedResponse || await fetch(url, { credentials: 'same-origin', signal: navigation.signal, headers: { 'X-Admin-Navigation': 'true' } });
      if (!response.ok) throw new Error('Page load failed');
      const html = await response.text(); receivedPage = true;
      const doc = new DOMParser().parseFromString(html, 'text/html');
      if (ticket !== sequence) return false;
      if (!validatePage(doc, response, language)) return fallback(response.url || url);
      const nextModules = await loadModules(doc);
      if (ticket !== sequence) return false;
      // Compatibility and imports are known before any old module is disposed.
      await disposePage();
      clearOverlay(false);
      rememberSkeletons(doc);
      document.title = doc.title; document.documentElement.lang = doc.documentElement.lang;
      const nextBody = doc.querySelector('body[data-admin-design]');
      for (const [key, value] of Object.entries(nextBody.dataset)) body.dataset[key] = value;
      refreshSidebar(doc, language);
      for (const selector of ['[data-page-region]', '[data-shell-topbar]', '[data-shell-menu]', '[data-shell-antiforgery]', 'template[data-admin-template]']) {
        const old = [...document.querySelectorAll(selector)], replacements = [...doc.querySelectorAll(selector)];
        old.forEach((element, i) => { if (replacements[i]) element.replaceWith(document.importNode(replacements[i], true)); });
      }
      document.querySelectorAll('link[data-admin-page-style]').forEach(style => style.remove());
      doc.querySelectorAll('link[data-admin-page-style]').forEach(style => document.head.append(document.importNode(style, true)));
      for (const notice of doc.querySelectorAll('[data-toast-host] [data-toast]')) toast(notice.querySelector('.grow')?.textContent || notice.textContent, { error: notice.classList.contains('is-error') });
      window.adminDesignTheme?.apply(); paintSideLabel();
      if (mode === 'push') { index++; history.pushState({ adminDesignIndex: index }, '', url); }
      else if (mode === 'pop') index = targetIndex;
      activeUrl = url;
      await initModules(nextModules);
      if (language) {
        restorePosition(position);
        if (!reducedMotion()) for (const element of document.querySelectorAll('[data-shell-sidebar],[data-shell-topbar],[data-page-region]')) {
          element.dataset.languageTransition = '';
          element.addEventListener('animationend', () => element.removeAttribute('data-language-transition'), { once: true });
        }
      } else focus(document.querySelector('.h1'));
      document.dispatchEvent(new CustomEvent('admin:page-changed', { detail: { url } }));
      return true;
    } catch (error) {
      if (error.name === 'AbortError' || ticket !== sequence) return false;
      if (receivedPage || language) { console.warn('Admin page swap could not finish.', error); return fallback(url); }
      const placeholder = overlay?.element;
      if (!placeholder) return false;
      placeholder.setAttribute('aria-busy', 'false'); overlay.main.removeAttribute('aria-busy');
      for (const child of [...placeholder.children]) if (!child.classList.contains('page-head')) child.remove();
      const failed = template('load-failure');
      const retry = failed.querySelector('[data-load-retry]');
      retry.addEventListener('click', () => void navigate(url, { mode, targetIndex, check: false }));
      placeholder.append(failed); focus(retry);
      return false;
    }
  }
  let languagePending = false;
  async function changeLanguage(form, button) {
    if (languagePending || !button || button.classList.contains('is-on')) return;
    if (!await guard() || !await closeNavigationLayers()) return;
    navigation?.abort(); clearOverlay();
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
    if (overlay) { navigation?.abort(); sequence++; clearOverlay(); }
    const target = event.state?.adminDesignIndex, url = location.href;
    if (travel) { if (target === travel.target) { const resolve = travel.resolve; travel = null; resolve(); } return; }
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
      await navigate(url, { mode: 'pop', targetIndex: target, check: false });
    } finally { handlingPop = false; }
  }
  function setUrl(values, schema, { record = false } = {}) {
    const url = new URL(location.href); url.search = query.build(values, schema);
    if (record) index++;
    history[record ? 'pushState' : 'replaceState']({ adminDesignIndex: index }, '', url); activeUrl = url.href;
  }
  const api = window.AdminUI = { template, trapTab, openLayer, closeLayer, confirm, confirmDiscard, toast, busy, reducedMotion, registerDraft, trackForm, refreshDirty, guard, query, setUrl, navigate, refreshContext };
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

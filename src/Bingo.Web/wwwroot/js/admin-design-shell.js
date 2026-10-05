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
  const layers = [];
  let mobile = false;
  function lock() {
    document.querySelector('.scroller')?.classList.toggle('is-locked', !!layers.length || mobile);
    const main = document.querySelector('.main'), side = document.querySelector('[data-shell-sidebar]');
    if (main) main.inert = !!layers.length || mobile;
    if (side) side.inert = !!layers.length;
    layers.forEach((layer, index) => { layer.panel.inert = index !== layers.length - 1; layer.panel.classList.toggle('is-behind', index !== layers.length - 1); });
  }
  function closeMenu(restore = true) {
    if (!menu) return;
    menu.element.hidden = true;
    menu.opener.setAttribute('aria-expanded', 'false');
    if (restore) focus(menu.opener);
    menu = null;
  }
  function openMenu(opener) {
    const element = document.getElementById(opener.dataset.menuTarget);
    if (!element) return;
    if (menu?.element === element) { closeMenu(); return; }
    closeMenu(false);
    element.hidden = false;
    const bounds = opener.getBoundingClientRect();
    element.style.left = `${Math.max(8, Math.min(bounds.left, innerWidth - element.offsetWidth - 8))}px`;
    element.style.top = `${Math.max(8, Math.min(bounds.bottom + 6, innerHeight - element.offsetHeight - 8))}px`;
    opener.setAttribute('aria-expanded', 'true');
    menu = { element, opener };
    focus(controls(element)[0]);
  }
  function toggleSide(force) {
    const side = document.querySelector('[data-shell-sidebar]');
    if (matchMedia('(max-width: 860px)').matches) {
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
  }
  const inputSelector = 'input:not([type=hidden]):not([type=submit]):not([type=button]),select,textarea,[contenteditable="true"]';
  function openLayer({ kind = 'modal', title, content, confirmation = false, dirty = () => false, pending = () => false, onClose = () => {}, opener = document.activeElement }) {
    closeMenu(false);
    const wrapper = document.createElement('div'), scrim = document.createElement('div'), panel = document.createElement('section');
    wrapper.className = kind === 'drawer' ? 'design-drawer-layer' : 'm-wrap';
    scrim.className = kind === 'drawer' ? 'scrim' : 'm-scrim';
    panel.className = kind === 'drawer' ? 'drawer' : `modal${layers.length ? ' is-stacked' : ''}`;
    panel.tabIndex = -1;
    panel.setAttribute('role', confirmation ? 'alertdialog' : 'dialog');
    panel.setAttribute('aria-modal', 'true');
    panel.setAttribute('aria-label', title);
    panel.append(content);
    wrapper.append(panel);
    const host = document.querySelector(kind === 'drawer' ? '[data-drawer-host]' : '[data-modal-host]');
    host.append(scrim, wrapper);
    const layer = { wrapper, scrim, panel, confirmation, dirty, pending, opener, onClose, closing: false };
    layers.push(layer);
    lock();
    focus(panel.querySelector('[autofocus]') || controls(panel)[0] || panel);
    const outside = event => { if ((event.target === scrim || event.target === wrapper) && layers.at(-1) === layer && !confirmation && !panel.querySelector(inputSelector) && !dirty()) void closeLayer(layer); };
    scrim.addEventListener('click', outside);
    wrapper.addEventListener('click', outside);
    return { element: panel, close: result => closeLayer(layer, result, true) };
  }
  async function closeLayer(layer = layers.at(-1), result = false, confirmed = false) {
    if (!layer || layer.closing || layer !== layers.at(-1) || layer.pending()) return false;
    if (!confirmed && layer.dirty() && !await confirmDiscard()) return false;
    layer.closing = true;
    layer.panel.classList.add('is-closing');
    layer.scrim.classList.add('is-closing');
    await afterExit(layer.panel);
    layers.pop();
    layer.wrapper.remove(); layer.scrim.remove(); lock();
    focus(layer.opener);
    layer.onClose(result);
    return true;
  }
  function confirm({ title, description, actionLabel, cancelLabel }) {
    return new Promise(resolve => {
      const content = document.createElement('div');
      const heading = document.createElement('h2'); heading.className = 'm-title'; heading.textContent = title;
      const support = document.createElement('p'); support.className = 'm-sub'; support.textContent = description;
      const footer = document.createElement('div'); footer.className = 'm-actions';
      const cancel = document.createElement('button'); cancel.className = 'btn'; cancel.textContent = cancelLabel; cancel.autofocus = true;
      const accept = document.createElement('button'); accept.className = 'btn btn-primary'; accept.textContent = actionLabel;
      footer.append(cancel, accept); content.append(heading, support, footer);
      const layer = openLayer({ title, content, confirmation: true, onClose: resolve });
      cancel.addEventListener('click', () => void layer.close(false));
      accept.addEventListener('click', () => void layer.close(true));
    });
  }
  const confirmDiscard = () => confirm({ title: text('discardTitle'), description: text('discardDescription'), actionLabel: text('discard'), cancelLabel: text('keepEditing') });

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
    const element = document.createElement('div'); element.className = `toast${error ? ' is-error' : ''}`; element.dataset.toast = ''; element.setAttribute('role', error ? 'alert' : 'status');
    const copy = document.createElement('span'); copy.className = 'grow'; copy.textContent = message; element.append(copy);
    if (actionLabel && action) { const button = document.createElement('button'); button.className = 'toast-act'; button.textContent = actionLabel; button.addEventListener('click', action); element.append(button); }
    const close = document.createElement('button'); close.className = 'toast-x'; close.dataset.toastClose = ''; close.setAttribute('aria-label', text('close')); close.textContent = '×'; element.append(close);
    document.querySelector('[data-toast-host]').append(element);
    startToast(element, actionLabel && action ? 7000 : 4500);
    return element;
  }

  const drafts = new Map();
  const isDirty = () => [...drafts.values()].some(draft => draft.isDirty());
  const isPending = () => [...drafts.values()].some(draft => draft.isPending?.());
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
    if (!await confirmDiscard()) return false;
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
  function skeleton(url) {
    const main = document.querySelector('[data-page-region]');
    const kind = new URL(url).pathname.split('/').at(-2)?.toLowerCase() || 'page';
    const placeholder = document.createElement('div'); placeholder.className = 'page'; placeholder.dataset.pageSkeleton = kind; placeholder.setAttribute('role', 'status'); placeholder.setAttribute('aria-label', text('loading'));
    const rows = kind === 'identity' ? 4 : 6;
    for (let row = 0; row < rows; row++) { const bar = document.createElement('div'); bar.className = 'sk-row'; placeholder.append(bar); }
    main.replaceChildren(placeholder); main.setAttribute('aria-busy', 'true');
  }
  async function navigate(url, { mode = 'push', targetIndex, check = true } = {}) {
    url = new URL(url, location.href).href;
    if (check && !await guard()) return false;
    if (body.dataset.navigationEnabled.toLowerCase() === 'false') { fullLoad(url); return true; }
    const ticket = ++sequence;
    navigation?.abort(); navigation = new AbortController();
    closeMenu(false);
    if (mobile) toggleSide(false);
    await disposePage();
    if (mode === 'pop') { index = targetIndex; activeUrl = url; }
    skeleton(url);
    let receivedPage = false;
    try {
      const response = await fetch(url, { credentials: 'same-origin', signal: navigation.signal, headers: { 'X-Admin-Navigation': 'true' } });
      if (!response.ok) throw new Error('Page load failed');
      const html = await response.text();
      receivedPage = true;
      const doc = new DOMParser().parseFromString(html, 'text/html');
      if (ticket !== sequence) return false;
      const region = doc.querySelector('[data-page-region]'), nextBody = doc.querySelector('body[data-admin-design]');
      if (!region || !nextBody || response.redirected || !doc.querySelector('[data-shell-sidebar]') || !doc.querySelector('[data-shell-topbar]')) { fullLoad(response.url || url); return true; }
      const tokenHost = doc.querySelector('[data-shell-antiforgery]');
      const unexpectedScripts = [...doc.querySelectorAll('script')].some(script => !script.hasAttribute('data-admin-page-script') && !script.hasAttribute('data-admin-shell-script') && new URL(script.getAttribute('src') || '/', location.href).pathname !== '/js/admin-design-theme.js');
      if (!tokenHost || unexpectedScripts) { fullLoad(url); return true; }
      const styles = [...doc.querySelectorAll('link[data-admin-page-style]')];
      if (styles.some(style => new URL(style.href, location.href).origin !== location.origin)) { fullLoad(url); return true; }
      const nextModules = await loadModules(doc);
      if (ticket !== sequence) return false;
      document.title = doc.title;
      document.documentElement.lang = doc.documentElement.lang;
      for (const [key, value] of Object.entries(nextBody.dataset)) body.dataset[key] = value;
      for (const selector of ['[data-page-region]', '[data-shell-sidebar]', '[data-shell-topbar]', '[data-shell-menu]']) {
        const old = [...document.querySelectorAll(selector)], replacements = [...doc.querySelectorAll(selector)];
        if (old.length !== replacements.length) { fullLoad(url); return true; }
        old.forEach((element, i) => element.replaceWith(document.importNode(replacements[i], true)));
      }
      document.querySelector('[data-shell-antiforgery]')?.replaceWith(document.importNode(tokenHost, true));
      document.querySelectorAll('link[data-admin-page-style]').forEach(style => style.remove());
      styles.forEach(style => document.head.append(document.importNode(style, true)));
      for (const notice of doc.querySelectorAll('[data-toast-host] [data-toast]')) toast(notice.querySelector('.grow')?.textContent || notice.textContent, { error: notice.classList.contains('is-error') });
      window.adminDesignTheme?.apply();
      if (mode === 'push') { index++; history.pushState({ adminDesignIndex: index }, '', url); }
      else if (mode === 'pop') index = targetIndex;
      activeUrl = url;
      await initModules(nextModules);
      focus(document.querySelector('.h1'));
      document.dispatchEvent(new CustomEvent('admin:page-changed', { detail: { url } }));
      return true;
    } catch (error) {
      if (error.name === 'AbortError' || ticket !== sequence) return false;
      if (receivedPage) { await disposePage(); fullLoad(url); return true; }
      const main = document.querySelector('[data-page-region]');
      main.removeAttribute('aria-busy');
      const failed = document.createElement('div'); failed.className = 'empty is-error'; failed.setAttribute('role', 'alert');
      const message = document.createElement('p'); message.textContent = text('loadError');
      const retry = document.createElement('button'); retry.className = 'btn'; retry.textContent = text('retry');
      retry.addEventListener('click', () => void navigate(url, { mode, targetIndex, check: false }));
      failed.append(message, retry); main.replaceChildren(failed); focus(retry);
      return false;
    }
  }
  function goTo(target) { return new Promise(resolve => { travel = { target, resolve }; history.go(target - (history.state?.adminDesignIndex ?? index)); }); }
  async function onPop(event) {
    const target = event.state?.adminDesignIndex, url = location.href;
    if (travel) { if (target === travel.target) { const resolve = travel.resolve; travel = null; resolve(); } return; }
    if (handlingPop) return;
    if (target === undefined) { if (await guard()) fullLoad(url); else history.pushState({ adminDesignIndex: index }, '', activeUrl); return; }
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
  const api = window.AdminUI = { trapTab, openLayer, closeLayer, confirm, confirmDiscard, toast, busy, reducedMotion, registerDraft, trackForm, refreshDirty, guard, query, setUrl, navigate };
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
  document.querySelectorAll('[data-toast]').forEach(element => startToast(element, 4500));
  void loadModules(document).then(initModules).catch(() => toast(text('loadError'), { error: true }));
})();

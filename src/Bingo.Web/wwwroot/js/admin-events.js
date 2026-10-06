// Server-owned directory state. The shell owns navigation and module disposal.
import { initCreate, disposeCreate } from './admin-event-create.js';
let release;
export function dispose() { disposeCreate(); release?.(); release = null; }
export function init(region, ui = window.AdminUI) {
  dispose();
  const root = region.querySelector('[data-events-directory]');
  if (!root) return;
  const canonical = new URL(root.dataset.directoryCanonical, location.href);
  if (new URL(location.href).searchParams.get('create') === '1') canonical.searchParams.set('create', '1');
  const values = Object.fromEntries(canonical.searchParams);
  ui.setUrl(values, Object.fromEntries(Object.keys(values).map(key => [key, { valid: () => true, default: '' }])));
  const life = new AbortController();
  const listen = (node, type, fn) => node?.addEventListener(type, fn, { signal: life.signal });
  let timer, overflowFrame, hint;
  const search = root.querySelector('[data-directory-search]');
  let edits = 0;
  const results = root.querySelector('[data-directory-results]');
  const replace = (old, fresh) => old.replaceChildren(...document.importNode(fresh, true).childNodes);
  function patch(doc) {
    const fresh = doc.querySelector('[data-events-directory]');
    if (!fresh) throw new Error('Missing directory results');
    for (const selector of ['[data-directory-results]', '[data-directory-footer]', '[data-directory-chips]', '[data-directory-banners]']) replace(root.querySelector(selector), fresh.querySelector(selector));
    const summary = region.querySelector('.summary'), nextSummary = doc.querySelector('.summary');
    // Keep the summary action itself connected when it started this update.
    [...nextSummary.children].forEach((next, index) => {
      const old = summary.children[index];
      if (old?.tagName === next.tagName) {
        replace(old, next);
        if (next.dataset.directoryUrl) old.dataset.directoryUrl = next.dataset.directoryUrl;
        if (next.hasAttribute('aria-pressed')) old.setAttribute('aria-pressed', next.getAttribute('aria-pressed'));
      } else if (old) old.replaceWith(document.importNode(next, true));
      else summary.append(document.importNode(next, true));
    });
    while (summary.children.length > nextSummary.children.length) summary.lastElementChild.remove();
    const sync = (selector, fn) => {
      const nodes = [...root.querySelectorAll(selector)], next = [...fresh.querySelectorAll(selector)];
      nodes.forEach((node, index) => fn(node, next[index]));
    };
    sync('.tabs input', (node, next) => {
      node.checked = next.checked; node.dataset.directoryUrl = next.dataset.directoryUrl;
      node.closest('.tab').className = next.closest('.tab').className;
      node.closest('.tab').querySelector('.tab-count').textContent = next.closest('.tab').querySelector('.tab-count').textContent;
    });
    sync('#phase-btn, #directory-phase-menu button', (node, next) => {
      replace(node, next); node.className = next.className;
      if (next.dataset.directoryUrl) node.dataset.directoryUrl = next.dataset.directoryUrl;
      if (next.hasAttribute('aria-checked')) node.setAttribute('aria-checked', next.getAttribute('aria-checked'));
    });
    sync('.th', (node, next) => {
      node.className = next.className; node.setAttribute('aria-sort', next.getAttribute('aria-sort'));
      const button = node.querySelector('button'), nextButton = next.querySelector('button');
      button.dataset.directoryUrl = nextButton.dataset.directoryUrl; button.setAttribute('aria-label', nextButton.getAttribute('aria-label'));
      node.querySelector('.sort-ic').setAttribute('class', next.querySelector('.sort-ic').getAttribute('class'));
    });
    search.closest('.search').classList.toggle('has-clear', search.value.length > 0);
    root.querySelector('[data-directory-clear]').hidden = !search.value.length;
    root.dataset.directoryCanonical = fresh.dataset.directoryCanonical;
    root.dataset.sortAnnouncement = fresh.dataset.sortAnnouncement;
    root.querySelector('[data-sort-status]').textContent = fresh.dataset.sortAnnouncement;
    clearHint();
    const url = new URL(fresh.dataset.directoryCanonical, location.href);
    if (new URL(location.href).searchParams.get('create') === '1') url.searchParams.set('create', '1');
    return url.href;
  }
  const fragment = failed => {
    const content = document.querySelector(`template[data-page-${failed ? 'failure' : 'loading'}-template="events"]`).content;
    const fragment = document.createDocumentFragment();
    for (const node of content.querySelectorAll(failed ? '.empty' : '.sk-row')) fragment.append(document.importNode(node, true));
    return fragment;
  };
  const navigate = target => {
    const url = new URL(target, location.href), revision = edits;
    return ui.update(url.href, { root, results, patch, pending: () => fragment(false), failed: () => fragment(true),
      fallbackFocus: () => root.querySelector('.th-btn'), signal: life.signal, scrollRegions: [root.querySelector('[data-directory-wrap]')],
      current: () => edits === revision, draft: () => ({ [search.getAttribute('aria-label')]: search.value }) });
  };
  function searchNow() {
    clearTimeout(timer);
    const url = new URL(location.href); url.searchParams.delete('filter');
    url.searchParams.set('search', search.value.trim()); url.searchParams.set('page', '1');
    void navigate(url.href);
  }
  listen(search, 'input', () => { edits++; clearTimeout(timer); timer = setTimeout(searchNow, 250); });
  listen(search, 'keydown', event => { if (event.key === 'Enter') { event.preventDefault(); searchNow(); } });
  listen(region, 'click', event => {
    const control = event.target.closest('[data-directory-url]');
    if (control && control.type !== 'radio') {
      event.preventDefault(); clearTimeout(timer);
      if (control.closest('#directory-phase-menu')) ui.closeMenu();
      const url = new URL(control.dataset.directoryUrl, location.href);
      if (control.closest('.empty')) { search.value = url.searchParams.get('search') || ''; edits++; }
      else if (search.value.trim()) url.searchParams.set('search', search.value.trim());
      void navigate(url.href); return;
    }
    if (event.target.closest('[data-directory-clear]')) { search.value = ''; edits++; search.focus({preventScroll:true}); searchNow(); return; }
    const row = event.target.closest('[data-event-id]');
    if (row && !event.target.closest('a,button,.hint')) void ui.navigate(row.dataset.openUrl);
  });
  listen(root, 'change', event => { if (event.target.matches('input[data-directory-url]')) void navigate(event.target.dataset.directoryUrl); });
  const wrap = root.querySelector('[data-directory-wrap]');
  const overflow = new ResizeObserver(() => { cancelAnimationFrame(overflowFrame); overflowFrame = requestAnimationFrame(() => wrap.classList.toggle('is-scroll', wrap.scrollWidth > wrap.clientWidth + 1)); });
  overflow.observe(wrap);
  function clearHint() { hint?.classList.remove('is-placed', 'is-below'); hint = null; }
  function placeHint(node) {
    clearHint(); hint = node; node.classList.remove('is-dismissed'); node.classList.add('is-placed');
    const tip = node.querySelector('.hint-tip'), box = node.getBoundingClientRect(), below = box.top < tip.offsetHeight + 16;
    node.classList.toggle('is-below', below);
    node.style.setProperty('--hint-x', Math.max(8, Math.min(box.left + box.width / 2 - tip.offsetWidth / 2, innerWidth - tip.offsetWidth - 8)) + 'px');
    node.style.setProperty('--hint-y', (below ? box.bottom + 8 : box.top - tip.offsetHeight - 8) + 'px');
  }
  listen(root, 'pointerover', event => { const node = event.target.closest('.hint'); if (node && !node.contains(event.relatedTarget)) placeHint(node); });
  listen(root, 'pointerout', event => { if (hint && !hint.contains(event.relatedTarget)) clearHint(); });
  listen(root, 'focusin', event => { const node = event.target.closest('.hint'); if (node) placeHint(node); });
  listen(root, 'focusout', clearHint);
  listen(root, 'keydown', event => { if (event.key === 'Escape' && hint) { hint.classList.add('is-dismissed'); clearHint(); } });
  const frame = requestAnimationFrame(() => { root.querySelector('[data-sort-status]').textContent = root.dataset.sortAnnouncement; });
  let flash;
  try {
    const saved = JSON.parse(sessionStorage.getItem('admin-event-created') || 'null');
    if (saved?.url === location.pathname + location.search) {
      sessionStorage.removeItem('admin-event-created');flash = root.querySelector(`[data-event-id="${CSS.escape(saved.id)}"]`);
      if (flash) { flash.classList.add('is-flash');flash.querySelector('.name-btn')?.focus({preventScroll:true});flash.scrollIntoView({block:'nearest'});if(ui.reducedMotion())requestAnimationFrame(()=>flash.classList.remove('is-flash'));else listen(flash,'animationend',()=>flash.classList.remove('is-flash')); }
    }
  } catch { sessionStorage.removeItem('admin-event-created'); }
  initCreate(region, ui);
  release = () => { life.abort(); clearTimeout(timer); overflow.disconnect(); cancelAnimationFrame(overflowFrame); cancelAnimationFrame(frame); clearHint(); };
}

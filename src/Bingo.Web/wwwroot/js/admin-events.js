// Server-owned directory state. The shell owns navigation and module disposal.
import { initCreate, disposeCreate } from './admin-event-create.js';
let release;
export function dispose() { disposeCreate(); release?.(); release = null; }
export function init(region, ui = window.AdminUI) {
  dispose();
  const root = region.querySelector('[data-events-directory]');
  if (!root) return;
  const canonical = new URL(root.dataset.directoryCanonical, location.href);
  let query = new URL(canonical.href);
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
    const sync = (parent, nextParent, key, fn) => {
      const nodes = new Map([...parent.children].map(node => [key(node), node]));
      [...nextParent.children].forEach((next, index) => {
        const id = key(next), old = nodes.get(id), node = old || document.importNode(next, true);
        if (old) { fn(old, next); nodes.delete(id); }
        if (parent.children[index] !== node) parent.insertBefore(node, parent.children[index] || null);
      });
      nodes.forEach(node => node.remove());
    };
    const viewKey = node => node.querySelector('input') ? new URL(node.querySelector('input').dataset.directoryUrl, location.href).searchParams.get('view') : 'separator';
    sync(root.querySelector('.tabs'), fresh.querySelector('.tabs'), viewKey, (node, next) => {
      node.className = next.className;
      const input = node.querySelector('input'), nextInput = next.querySelector('input');
      if (input) { input.checked = nextInput.checked; input.dataset.directoryUrl = nextInput.dataset.directoryUrl; node.querySelector('.tab-count').textContent = next.querySelector('.tab-count').textContent; }
    });
    const phase = root.querySelector('#phase-btn'), nextPhase = fresh.querySelector('#phase-btn');
    replace(phase, nextPhase); phase.className = nextPhase.className;
    // Options vary by view; only the menu list is replaceable, not its opener.
    replace(root.querySelector('#directory-phase-menu'), fresh.querySelector('#directory-phase-menu'));
    sync(root.querySelector('.th-row'), fresh.querySelector('.th-row'), node => node.querySelector('button').id, (node, next) => {
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
    query = new URL(url.href);
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
    query = new URL(url.href);
    return ui.update(url.href, { root, results, patch, pending: () => fragment(false), failed: () => fragment(true),
      fallbackFocus: position => {
        const pager = position.active?.closest('.pager');
        if (pager) {
          const other = root.querySelector('.pager button:' + (position.active === pager.querySelector('button') ? 'last-child' : 'first-child'));
          if (other && !other.disabled) return other;
        }
        return root.querySelector('.th-btn');
      }, signal: life.signal, scrollRegions: [root.querySelector('[data-directory-wrap]')],
      current: () => edits === revision, draft: () => ({ [search.getAttribute('aria-label')]: search.value }) });
  };
  function searchNow() {
    clearTimeout(timer);
    const url = new URL(query.href); url.searchParams.delete('filter');
    url.searchParams.set('search', search.value.trim()); url.searchParams.set('page', '1');
    void navigate(url.href);
  }
  listen(search, 'input', () => { edits++; ui.supersedeUpdate(); clearTimeout(timer); timer = setTimeout(searchNow, 250); });
  listen(search, 'keydown', event => { if (event.key === 'Enter') { event.preventDefault(); searchNow(); } });
  function controlNow(control) {
    clearTimeout(timer);
    const target = new URL(control.dataset.directoryUrl, location.href), url = new URL(query.href);
    const put = key => { const value = target.searchParams.get(key); if (value === null) url.searchParams.delete(key); else url.searchParams.set(key, value); };
    if (control.type === 'radio') {
      put('view');
      const phase = [...root.querySelectorAll('#directory-phase-menu button')].find(option => new URL(option.dataset.directoryUrl, location.href).searchParams.get('phase') === (url.searchParams.get('phase') || 'all'));
      if (!phase?.dataset.phaseViews?.split(',').includes(url.searchParams.get('view'))) url.searchParams.set('phase', 'all');
    } else if (control.closest('#directory-phase-menu')) { put('phase'); ui.closeMenu(); }
    else if (control.closest('.th')) {
      const sort = target.searchParams.get('sort');
      url.searchParams.set('direction', url.searchParams.get('sort') === sort && url.searchParams.get('direction') !== 'desc' ? 'desc' : 'asc');
      url.searchParams.set('sort', sort);
    } else if (control.closest('.pager')) put('page');
    else if (control.closest('.tfoot')) { url.searchParams.set('sort', ''); url.searchParams.set('direction', 'asc'); }
    else if (control.closest('.empty')) { for (const key of ['view', 'phase', 'attention']) put(key); search.value = target.searchParams.get('search') || ''; edits++; ui.supersedeUpdate(); }
    else if (control.closest('.summary') || control.classList.contains('filter-chip')) put('attention');
    if (!control.closest('.pager, [data-directory-banners]')) url.searchParams.set('page', '1');
    url.searchParams.set('search', search.value.trim());
    void navigate(url.href);
  }
  listen(region, 'click', event => {
    const control = event.target.closest('[data-directory-url]');
    if (control && control.type !== 'radio') {
      event.preventDefault(); controlNow(control); return;
    }
    if (event.target.closest('[data-directory-clear]')) { search.value = ''; edits++; ui.supersedeUpdate(); search.focus({preventScroll:true}); searchNow(); return; }
    const row = event.target.closest('[data-event-id]');
    if (row && !event.target.closest('a,button,.hint')) void ui.navigate(row.dataset.openUrl);
  });
  listen(root, 'change', event => { if (event.target.matches('input[data-directory-url]')) controlNow(event.target); });
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

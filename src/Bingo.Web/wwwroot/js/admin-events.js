// Server-owned directory state. The shell owns navigation and module disposal.
let release;
export function dispose() { release?.(); release = null; }
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
  const navigate = url => ui.navigate(url, { mode: 'replace' });
  const search = root.querySelector('[data-directory-search]');
  function searchNow() {
    clearTimeout(timer);
    const url = new URL(location.href); url.searchParams.delete('filter');
    url.searchParams.set('search', search.value.trim()); url.searchParams.set('page', '1');
    void navigate(url.href);
  }
  listen(search, 'input', () => { clearTimeout(timer); timer = setTimeout(searchNow, 250); });
  listen(search, 'keydown', event => { if (event.key === 'Enter') { event.preventDefault(); searchNow(); } });
  listen(region, 'click', event => {
    const control = event.target.closest('[data-directory-url]');
    if (control && control.type !== 'radio') { event.preventDefault(); clearTimeout(timer); void navigate(control.dataset.directoryUrl); return; }
    if (event.target.closest('[data-directory-clear]')) { search.value = ''; searchNow(); return; }
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
  release = () => { life.abort(); clearTimeout(timer); overflow.disconnect(); cancelAnimationFrame(overflowFrame); cancelAnimationFrame(frame); clearHint(); };
}

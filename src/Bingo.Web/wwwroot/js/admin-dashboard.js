// Read-only Dashboard. The shared shell owns navigation and this module's lifetime.
let release;
export function dispose() { release?.(); release = null; }
export function init(region, ui = window.AdminUI) {
  dispose();
  const root = region.querySelector('[data-dashboard]');
  if (!root) return;
  const lifetime = new AbortController();
  const listen = (node, event, action) => node?.addEventListener(event, action, { signal: lifetime.signal });
  const bars = [...root.querySelectorAll('[data-chart-bar]')];
  const wrap = root.querySelector('.tbl-wrap');
  let overflowFrame;
  const overflow = new ResizeObserver(() => {
    cancelAnimationFrame(overflowFrame);
    overflowFrame = requestAnimationFrame(() => { if (wrap) wrap.classList.toggle('is-scroll', wrap.scrollWidth > wrap.clientWidth + 1); });
  });
  if (wrap) overflow.observe(wrap);
  let active = null, dismissed = null;
  let hint = null;
  function clearHint() { hint?.classList.remove('is-placed', 'is-below'); hint = null; }
  function placeHint(node) {
    clearHint(); hint = node; node.classList.remove('is-dismissed'); node.classList.add('is-placed');
    const tip = node.querySelector('.hint-tip'), box = node.getBoundingClientRect();
    if (!tip) return;
    const left = Math.max(8, Math.min(box.left + box.width / 2 - tip.offsetWidth / 2, document.documentElement.clientWidth - tip.offsetWidth - 8));
    const below = box.top < tip.offsetHeight + 16;
    node.classList.toggle('is-below', below);
    node.style.setProperty('--hint-x', left + 'px');
    node.style.setProperty('--hint-y', (below ? box.bottom + 8 : box.top - tip.offsetHeight - 8) + 'px');
  }
  listen(root, 'pointerover', event => { const node = event.target.closest('.hint'); if (node && !node.contains(event.relatedTarget)) placeHint(node); });
  listen(root, 'pointerout', event => { if (hint && !hint.contains(event.relatedTarget)) clearHint(); });
  listen(root, 'focusin', event => { const node = event.target.closest('.hint'); if (node) placeHint(node); });
  listen(root, 'focusout', clearHint);
  listen(root, 'keydown', event => { if (event.key === 'Escape' && hint) { hint.classList.add('is-dismissed'); clearHint(); } });
  function hide() {
    active?.classList.remove('is-active');
    root.querySelector('.bars')?.classList.remove('has-active');
    root.querySelectorAll('[data-chart-tip]').forEach(tip => { tip.classList.remove('is-on', 'is-glide'); tip.setAttribute('aria-hidden', 'true'); });
    active = null;
  }
  function show(bar) {
    if (dismissed === bar) return;
    const glide = active && active !== bar;
    hide(); active = bar; bar.classList.add('is-active'); bar.closest('.bars').classList.add('has-active');
    const tip = root.querySelector(`[data-chart-tip="${bar.dataset.chartBar}"]`);
    if (!tip) return;
    tip.classList.add('is-on'); tip.classList.toggle('is-glide', !!glide); tip.setAttribute('aria-hidden', 'false');
    const chart = bar.closest('.chart').getBoundingClientRect(), target = bar.querySelector('.bar-stack').getBoundingClientRect();
    const width = tip.offsetWidth, height = tip.offsetHeight;
    let left = target.right - chart.left + 14;
    if (left + width > chart.width - 4) left = target.left - chart.left - width - 14;
    tip.style.left = Math.round(Math.max(4, left)) + 'px';
    tip.style.top = Math.round(Math.max(4, Math.min(target.top - chart.top, chart.height - height - 4))) + 'px';
  }
  for (const bar of bars) {
    listen(bar, 'pointerenter', () => { dismissed = null; show(bar); });
    listen(bar, 'pointerleave', () => { if (document.activeElement !== bar) hide(); });
    listen(bar, 'focus', () => { dismissed = null; show(bar); });
    listen(bar, 'blur', hide);
    listen(bar, 'keydown', event => {
      if (event.key === 'Escape') { event.preventDefault(); dismissed = bar; hide(); }
      if (event.key === 'ArrowRight' || event.key === 'ArrowLeft') {
        event.preventDefault(); bars[(bars.indexOf(bar) + (event.key === 'ArrowRight' ? 1 : bars.length - 1)) % bars.length].focus();
      }
    });
    listen(bar, 'click', () => void ui.navigate(bar.dataset.openUrl));
  }
  listen(root, 'click', event => {
    const row = event.target.closest('[data-history-event]');
    if (row && !event.target.closest('a,button,.hint')) void ui.navigate(row.dataset.openUrl);
  });
  listen(window, 'resize', () => { if (active) show(active); });
  const frame = requestAnimationFrame(() => { if (new URL(location.href).searchParams.has('sort')) root.querySelector('[data-sort-status]').textContent = root.dataset.sortAnnouncement; });
  release = () => { lifetime.abort(); overflow.disconnect(); cancelAnimationFrame(overflowFrame); cancelAnimationFrame(frame); clearHint(); hide(); };
}

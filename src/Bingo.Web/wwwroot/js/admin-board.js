// U7 / Board.dc.html: the Board workspace. The server renders one view (Board.View.cs);
// this module paints the header actions, banner, grid, line totals and planning panel
// from it and re-reads it after every command. Every EHB figure is the server's (RC05 B7).
// Commands answer in place (Board.Transport.cs) through AdminFetch (C-CMP-2); an
// unknown outcome is settled by the no-store Readback, never assumed.
// Extensions (tile editor, publication flows) install onto the shared context.

import { ROWS, posName, parseWhole, leaseRenewer } from './admin-board-model.js';
import { install as installEditor } from './admin-board-editor.js';
import { install as installPublication } from './admin-board-publication.js';

const extensions = [installEditor, installPublication];

let release = () => {};
export function dispose() { release(); release = () => {}; }

export async function init(region, ui = window.AdminUI) {
  dispose();
  const root = region.querySelector('[data-board]');
  if (!root) return;
  const life = new AbortController();
  const on = (target, type, fn, options) => target?.addEventListener(type, fn, { signal: life.signal, ...options });
  const labels = JSON.parse(root.dataset.labels);
  const t = (key, ...args) => (labels[key] ?? key).replace(/\{(\d+)\}/g, (_, i) => String(args[+i] ?? ''));
  const lang = document.documentElement.lang || 'en';
  const number = new Intl.NumberFormat(lang, { maximumFractionDigits: 1, minimumFractionDigits: 0 });
  const fmt1 = value => number.format(Math.round(Number(value) * 10) / 10);
  const day = value => value ? new Intl.DateTimeFormat(lang, { day: 'numeric', month: 'long' }).format(new Date(value)) : '';
  const el = (tag, cls, text) => { const e = document.createElement(tag); if (cls) e.className = cls; if (text !== undefined && text !== null) e.textContent = text; return e; };
  const icon = (name, cls = 'ic') => { const source = document.querySelector(`template[data-board-icon="${name}"]`); const svg = source ? source.content.firstElementChild.cloneNode(true) : el('span'); svg.setAttribute('class', cls); return svg; };
  const button = (cls, text, onClick, id) => { const b = el('button', cls); b.type = 'button'; if (id) b.id = id; if (text) b.append(document.createTextNode(text)); if (onClick) on(b, 'click', onClick); return b; };

  const ctx = {
    ui, t, el, icon, button, on, fmt1, day, life, root, lang,
    view: JSON.parse(root.dataset.view),
    pending: null, unsure: null, checking: false, issues: null, notice: null,
    moving: null, focusPos: 0, dragFrom: null, planMore: false, sizeDraft: null, sizeErr: '', sizeSaved: false,
    hooks: { openTile: null, primary: null, banner: [], afterPaint: [], removeTile: null }
  };
  const field = name => root.querySelector(`[data-board-fields] [name="${name}"]`);
  ctx.boardVersion = () => field('BoardVersion')?.value || '0';
  ctx.fingerprint = () => field('ApprovalCatalogueFingerprint')?.value || '';
  ctx.url = handler => { const u = new URL(location.href); u.search = ''; if (handler) u.searchParams.set('handler', handler); return u.href; };
  ctx.mode = () => ctx.view.mode;
  ctx.editable = () => !ctx.view.readOnly && ['draft', 'approved', 'correction'].includes(ctx.view.mode);
  ctx.canEdit = () => ctx.editable() && ctx.view.control?.who === 'me';
  ctx.blocked = () => !!ctx.pending || ctx.checking || !!ctx.unsure;
  ctx.tileAt = pos => ctx.view.tiles?.find(tile => tile.pos === pos) || null;
  ctx.cellId = pos => { const tile = ctx.tileAt(pos); return tile ? 'bt-' + tile.id : 'be-' + pos; };
  ctx.where = pos => posName(pos, ctx.view.cols);
  ctx.focusSoon = id => requestAnimationFrame(() => document.getElementById(id)?.focus());

  /* ---------------- transport ---------------- */
  const body = values => { const data = new URLSearchParams(); for (const [k, v] of Object.entries(values)) if (v !== undefined && v !== null) data.append(k, String(v)); return data; };
  // Every command: one busy request; a handler answer is definite, the route refusal
  // (302 Manage) is a refusal with the server's reason, a lost session keeps the
  // draft (AdminFetch notice), and anything else is settled by the Readback.
  ctx.command = async (handler, values, { draft, labels: draftLabels, form } = {}) => {
    ctx.pending = handler; paint();
    let result;
    try {
      const payload = form || body({ BoardVersion: ctx.boardVersion(), ...values });
      result = await ui.busy(() => window.AdminFetch.request(ctx.url(handler), { method: 'POST', body: payload, draft, labels: draftLabels, signal: life.signal }));
    } finally { ctx.pending = null; }
    if (life.signal.aborted) return { kind: 'aborted' };
    if (result.kind === 'handler') {
      const data = result.data || {};
      if ('outcome' in data) return { kind: data.outcome === 'saved' ? 'saved' : 'refused', message: data.message, issues: data.issues || [], current: data.current };
      return { kind: 'state', issues: data.localized || [], current: data.current };
    }
    if (result.kind === 'refused') return { kind: 'route-refused', message: result.reason || t('This event is read-only in its current lifecycle state.') };
    if (result.kind === 'session-lost') return { kind: 'session-lost' };
    return { kind: 'unknown' };
  };
  ctx.readback = async () => {
    const result = await window.AdminFetch.request(ctx.url('Readback'), { cache: 'no-store', readback: true, signal: life.signal });
    return result.kind === 'handler' && result.data?.known ? result.data.state : null;
  };
  // Re-read the server view in place (no navigation): controls, focus and scroll stay.
  ctx.refresh = async () => {
    const result = await window.AdminFetch.request(location.pathname, { expect: 'html', cache: 'no-store', readback: true, signal: life.signal });
    if (result.kind !== 'handler') return false;
    const next = new DOMParser().parseFromString(result.data, 'text/html').querySelector('[data-board]');
    if (!next) return false;
    ctx.view = JSON.parse(next.dataset.view);
    for (const input of next.querySelectorAll('[data-board-fields] input')) { const mine = field(input.name); if (mine) mine.value = input.value; }
    paint();
    return true;
  };
  // A lost or unreadable answer: say so, re-read, and report what is now true.
  ctx.uncertain = async (what, check) => {
    ctx.unsure = { what, check }; ctx.checking = true; paint();
    const state = await ctx.readback();
    ctx.checking = false;
    if (state && check(state)) { ctx.unsure = null; await ctx.refresh(); return 'happened'; }
    if (state) { ctx.unsure = null; ctx.notice = { cls: 'is-warning', title: t('We couldn’t confirm that this happened.'), text: t('The board below is the current state; it didn’t change as asked. Check it before trying again.') }; await ctx.refresh(); return 'not-happened'; }
    paint();
    return 'unknown';
  };
  ctx.recheck = async () => { if (!ctx.unsure) return; const { what, check } = ctx.unsure; await ctx.uncertain(what, check); };
  // Shared answer handling for workspace commands without an editor draft.
  ctx.settle = async (outcome, { what, check, ok, focus }) => {
    if (outcome.kind === 'saved') { if (ok) ui.toast(outcome.message || ok); await ctx.refresh(); if (focus) ctx.focusSoon(focus); return true; }
    if (outcome.kind === 'refused' || outcome.kind === 'route-refused') { ui.toast(outcome.message || t('That change wasn’t saved.'), { error: true }); await ctx.refresh(); return false; }
    if (outcome.kind === 'session-lost' || outcome.kind === 'aborted') return false;
    return await ctx.uncertain(what, check) === 'happened';
  };

  /* ---------------- header ---------------- */
  const head = document.querySelector('[data-page-region] .page-head[data-page-family="board"]') || document.querySelector('.page-head');
  const acts = head?.querySelector('[data-board-acts]');
  const moreMenu = root.querySelector('#board-more-menu');
  const badgeFor = mode => (/*labels*/{ draft: ['Draft · private', 'badge-neutral'], approved: ['Approved · private', 'badge-success'], published: ['Published', 'badge-success'], correction: ['Correction · private', 'badge-warning'] }/*end*/)[mode];
  function stateNote() {
    const v = ctx.view;
    if (v.mode === 'approved') return t('Approved {0}. Any tile, position or size change returns it to draft.', day(v.approvedAt));
    if (v.mode === 'published') return t('Published {0}. Players see this board.', day(v.publishedAt));
    if (v.mode === 'correction') return t('Players see the version published {0} until you publish this correction.', day(v.publishedAt));
    return '';
  }
  function paintHead() {
    if (!head) return;
    const v = ctx.view, badge = head.querySelector('[data-board-badge]'), b = badgeFor(v.mode);
    if (badge) { badge.hidden = !b; if (b) { badge.textContent = t(b[0]); badge.className = 'badge ' + b[1]; } }
    const summary = head.querySelector('.summary');
    if (summary && v.mode !== 'none') {
      const parts = [el('span', null, v.rows + ' × ' + v.cols)];
      if (ctx.canEdit()) parts.push(button('text-btn', t('Resize'), () => ctx.openResize(), 'resize-btn'));
      parts.push(el('span', null, t('{0} of {1} tiles', v.tiles.length, v.rows * v.cols)));
      const note = stateNote(); if (note) parts.push(el('span', null, note));
      if (ctx.blocked()) parts.filter(p => p.tagName === 'BUTTON').forEach(p => { p.disabled = true; });
      summary.classList.add('bd-summary');
      summary.replaceChildren(...parts);
    }
    if (!acts) return;
    acts.replaceChildren();
    if (v.mode === 'none') return;
    const ctl = controlChip(); if (ctl) acts.append(ctl);
    const preview = button('btn', null, () => ctx.openPreview(), 'preview-btn'); preview.append(icon('eye'), document.createTextNode(t('Preview')));
    acts.append(preview);
    const primary = ctx.hooks.primary?.();
    if (primary) {
      const p = button('btn btn-primary' + (primary.busy ? ' is-busy' : ''), null, () => { if (primary.inert) { if (primary.describedBy) document.getElementById(primary.describedBy)?.focus(); return; } primary.run(); }, 'primary-btn');
      if (primary.busy) { const spin = el('span', 'spin'); p.append(spin); p.disabled = true; }
      p.append(document.createTextNode(primary.label));
      p.setAttribute('aria-disabled', primary.inert ? 'true' : 'false');
      if (primary.title) p.title = primary.title;
      if (primary.describedBy) p.setAttribute('aria-describedby', primary.describedBy);
      acts.append(p);
    }
    const items = menuItems();
    if (items.length) {
      const more = button('icon-btn', null, null, 'more-btn');
      more.setAttribute('aria-label', t('More board actions')); more.setAttribute('aria-haspopup', 'menu'); more.setAttribute('aria-expanded', 'false');
      more.dataset.menuTarget = 'board-more-menu'; more.dataset.menuAlign = 'end'; more.append(icon('more'));
      acts.append(more);
      moreMenu.replaceChildren(...items.map(item => {
        if (item.sep) { const s = el('div', 'menu-sep'); s.setAttribute('role', 'separator'); return s; }
        const b = button('menu-item' + (item.danger ? ' is-danger' : ''), null, () => { ui.closeMenu(false); item.run(); });
        b.setAttribute('role', 'menuitem'); b.disabled = !!item.disabled; if (item.disabled && item.hint) b.title = item.hint;
        b.append(el('span', 'grow', item.label));
        if (item.hint) b.append(el('span', 'menu-hint', item.hint));
        return b;
      }));
    }
  }
  function controlChip() {
    const v = ctx.view, who = v.control?.who;
    if (!ctx.editable()) return null;
    const chip = el('span', 'ctl'); chip.setAttribute('role', 'status');
    chip.append(el('span', 'dot'));
    chip.firstChild.setAttribute('aria-hidden', 'true');
    let text, action, run, title;
    if (who === 'me') { text = t('You’re editing'); action = t('Stop'); run = () => ctx.release(); title = t('Editing control renews while you work and lapses after five minutes without board activity.'); }
    else if (who === 'other') { chip.classList.add('is-other'); text = t('{0} is editing', v.control.name || t('Another administrator')); action = t('Take over…'); run = () => ctx.takeover(); title = t('You can view everything. Their control lapses after five minutes without board activity.'); }
    else { chip.classList.add('is-none'); text = t('Viewing'); action = t('Start editing'); run = () => ctx.takeControl(); title = t('Start editing to change the board.'); }
    chip.title = title;
    chip.append(document.createTextNode(text));
    const act = button('text-btn', action, run, 'ctl-btn'); act.disabled = ctx.blocked();
    chip.append(act);
    return chip;
  }
  function menuItems() {
    const v = ctx.view, me = ctx.canEdit(), items = [];
    const startFirst = t('Start editing first');
    if (ctx.editable()) items.push({ label: t('Board size…'), disabled: !me || ctx.blocked(), hint: me ? v.rows + ' × ' + v.cols : startFirst, run: () => ctx.openResize() });
    for (const extra of ctx.hooks.menu || []) items.push(...extra());
    return items;
  }

  /* ---------------- editing control (lease) ---------------- */
  ctx.takeControl = async () => {
    const outcome = await ctx.command('AcquireEditing', {});
    await ctx.settle(outcome, { what: t('start editing'), check: s => s.controllerId === ctx.view.me, focus: 'ctl-btn' });
  };
  ctx.release = async () => {
    const outcome = await ctx.command('ReleaseEditing', {});
    await ctx.settle(outcome, { what: t('stop editing'), check: s => s.controllerId !== ctx.view.me, focus: 'ctl-btn' });
  };
  // RC05 B2: the takeover dialog never says anyone's draft is lost.
  ctx.takeover = async () => {
    const name = ctx.view.control?.name || t('Another administrator');
    const yes = await ui.confirm({ title: t('Take over editing?'), description: t('{0} returns to viewing. Anything they haven’t saved stays in their browser; nothing on the board changes.', name), actionLabel: t('Take over'), cancelLabel: t('Cancel') });
    if (!yes) return;
    const outcome = await ctx.command('TakeEditing', {});
    await ctx.settle(outcome, { what: t('take over editing'), check: s => s.controllerId === ctx.view.me, focus: 'ctl-btn' });
  };
  // U7-E1 (c): no SignalR in the new layout. While this admin holds the lease, any
  // pointer, key or input activity on the page or its drawer renews it over HTTP, at
  // most once a minute. A lost lease re-reads the view (the chip shows who edits);
  // an open tile draft stays in the drawer and a later save meets the stale refusal.
  const renew = leaseRenewer(async () => {
    const result = await window.AdminFetch.request(ctx.url('RenewEditing'), { method: 'POST', body: body({}), notice: false, signal: life.signal }); /* background POST */
    if (result.kind === 'handler' && result.data?.renewed === false && ctx.view.control?.who === 'me' && !ctx.blocked()) await ctx.refresh();
  });
  for (const type of ['pointerdown', 'keydown', 'input']) on(document, type, () => { if (ctx.canEdit() && !life.signal.aborted) renew(); }, { passive: true, capture: true });

  /* ---------------- banner ---------------- */
  const bannerHost = root.querySelector('[data-board-banner]');
  function banner({ cls, role = 'status', title, text, list, action, run, busy }) {
    const b = el('div', `banner ${cls} page-banner`); b.setAttribute('role', role); b.id = 'bd-banner'; b.tabIndex = -1;
    b.append(icon(cls === 'is-info' ? 'info' : cls === 'is-error' ? 'error' : 'warning'));
    const span = el('span', 'grow'); span.append(el('b', null, title));
    if (text) span.append(document.createTextNode(' ' + text));
    if (list?.length) {
      const ul = el('ul', 'bd-issues');
      for (const item of list) {
        const li = el('li');
        if (item.go) li.append(button('banner-btn', item.label, item.go)); else if (item.label) li.append(document.createTextNode(item.label));
        li.append(document.createTextNode(' ' + item.text));
        ul.append(li);
      }
      span.append(ul);
    }
    b.append(span);
    if (action) { const a = button('banner-btn', action, run, 'bd-banner-act'); a.disabled = !!busy; b.append(a); }
    return b;
  }
  function paintBanner() {
    const v = ctx.view;
    let spec = null;
    if (ctx.unsure || ctx.checking) spec = { role: 'alert', cls: 'is-warning', title: ctx.checking ? t('Checking…') : t('We couldn’t confirm whether this went through: {0}.', ctx.unsure.what), text: t('Check before trying again, so nothing happens twice.'), action: ctx.checking ? null : t('Check again'), run: () => ctx.recheck(), busy: ctx.checking };
    for (const hook of ctx.hooks.banner) if (!spec) spec = hook();
    if (!spec && ctx.notice) spec = { role: 'alert', ...ctx.notice, action: ctx.notice.action, run: ctx.notice.run || (() => { ctx.notice = null; paint(); }) };
    if (!spec && v.mode === 'none') spec = { cls: 'is-info', title: t('No board recorded.'), text: t('This event ended without a board, so there is nothing to show or change here.') };
    if (!spec && v.readOnly) spec = { cls: 'is-info', title: t('Read-only.'), text: t('This event is {0}, so its board can be viewed but not changed.', t(v.eventState)) }; /*labels*/['Cancelled', 'Finalized', 'Archived']/*end*/;
    if (!spec && v.mode === 'correction') spec = { cls: 'is-warning', title: t('Private correction.'), text: (v.reason ? t('Reason: {0}', v.reason) + ' ' : '') + t('Objectives with submitted evidence keep their requirements and scoring; wording can change. Changed tiles are marked with a dot.') };
    bannerHost.replaceChildren(...(spec ? [banner(spec)] : []));
  }

  /* ---------------- grid ---------------- */
  const work = root.querySelector('[data-board-work]');
  const moveHost = root.querySelector('[data-board-move]');
  function lineVm(line, kind, index, average, fullCount) {
    const label = kind === 'row' ? t('Row {0}', ROWS[index]) : t('Column {0}', index + 1);
    if (!line.full) return { total: line.n ? fmt1(line.total) : '—', cls: 'is-partial', aria: label + ': ' + (line.n ? t('{0} EHB so far, incomplete', fmt1(line.total)) : t('no tiles yet')), title: t('Incomplete line') };
    const dev = average ? (line.total - average) / average : 0, far = Math.abs(dev) >= 0.25 && fullCount > 2, pct = Math.round(Math.abs(dev) * 100);
    return { total: fmt1(line.total), cls: far ? (dev > 0 ? 'is-high' : 'is-low') : '', note: far ? pct + '%' : '', up: far && dev > 0, down: far && dev < 0,
      aria: label + ': ' + fmt1(line.total) + ' EHB' + (far ? ', ' + t(dev > 0 ? '{0}% above the average line' : '{0}% below the average line', pct) : ''),
      title: far ? t(dev > 0 ? '{0}% above the average line ({1} EHB)' : '{0}% below the average line ({1} EHB)', pct, fmt1(average)) : t('Average line {0} EHB', fmt1(average ?? 0)) };
  }
  function lineCell(vm, extra = '') {
    const cell = el('div', `bline ${extra} ${vm.cls}`.trim()); cell.setAttribute('role', 'gridcell'); cell.setAttribute('aria-label', vm.aria); cell.title = vm.title;
    cell.append(el('b', null, vm.total));
    if (vm.note) { const s = el('span'); s.append(icon(vm.up ? 'arrow-up' : 'arrow-down'), document.createTextNode(vm.note)); cell.append(s); }
    return cell;
  }
  function paintGrid() {
    const v = ctx.view;
    if (v.mode === 'none') { work.hidden = true; return; }
    work.hidden = false;
    work.classList.toggle('is-stacked', v.cols >= 7);
    const me = ctx.canEdit(), m = ctx.moving;
    const full = [...v.lines.rows, ...v.lines.cols].filter(l => l.full), average = full.length ? full.reduce((a, l) => a + l.total, 0) / full.length : null;
    const grid = root.querySelector('#bgrid');
    grid.className = 'bgrid' + (m ? ' is-moving' : '');
    grid.style.setProperty('--cols', v.cols);
    grid.style.setProperty('--tile-h', (v.rows <= 4 ? 120 : v.rows <= 5 ? 108 : v.rows <= 6 ? 96 : 84) + 'px');
    grid.setAttribute('aria-label', t('Board {0} rows by {1} columns', v.rows, v.cols));
    const issuePositions = (ctx.issues?.list || []).map(x => x.pos);
    const rows = [];
    for (let r = 0; r < v.rows; r++) {
      const row = el('div', 'bgrid-row'); row.setAttribute('role', 'row');
      for (let c = 0; c < v.cols; c++) {
        const pos = r * v.cols + c, tile = ctx.tileAt(pos), where = ctx.where(pos), focus = (m ? m.to : ctx.focusPos) === pos;
        const wrap = el('div', ['bcell', m && m.from === pos ? 'is-src' : '', m && m.to === pos && m.from !== pos ? 'is-target' : '', ctx.dragFrom === pos ? 'is-src' : ''].filter(Boolean).join(' '));
        wrap.setAttribute('role', 'gridcell'); wrap.dataset.pos = pos;
        let cell;
        if (tile) {
          cell = button('bcell btile' + (issuePositions.includes(pos) ? ' is-issue' : '') + (tile.changed ? ' is-changed' : ''), null, () => open(pos), 'bt-' + tile.id);
          cell.draggable = me && !ctx.blocked();
          const top = el('span', 'btile-top');
          if (tile.art) { const art = el('span', 'btile-art'); art.style.setProperty('--art', `url("${tile.art}")`); art.setAttribute('aria-hidden', 'true'); top.append(art); }
          top.append(el('span', 'btile-name', tile.name));
          const foot = el('span', 'btile-foot'), ehb = el('span', 'btile-ehb'), flags = el('span', 'btile-flags');
          const noEstimate = tile.noEstimate || tile.needsVerification;
          ehb.append(document.createTextNode(noEstimate ? t('No estimate') : fmt1(tile.ehb)));
          if (!noEstimate) ehb.append(el('small', null, ' EHB'));
          if (tile.overridden) { const p = el('span', 'pill is-neutral', t('Manual')); p.title = t('Manual EHB replaces the calculated estimate'); flags.append(p); }
          if (tile.manual) { const i = icon('challenge'); i.append(Object.assign(document.createElementNS('http://www.w3.org/2000/svg', 'title'), { textContent: t('Manual challenge') })); flags.append(i); }
          if (tile.parts > 1) { const p = el('span', 'pill is-neutral', t('{0} parts', tile.parts)); p.title = t('{0} objectives, all required', tile.parts); flags.append(p); }
          if (tile.locked) flags.append(icon('lock'));
          if (noEstimate) { const w = icon('warning', 'ic btile-flag-issue'); flags.append(w); cell.title = tile.needsVerification ? t('{0} needs catalogue verification. Review the affected choices or restore the missing catalogue mapping before approval.', tile.name) : t('No estimate'); }
          foot.append(ehb, flags); cell.append(top, foot);
          const estimate = noEstimate ? t('no estimate') : fmt1(tile.ehb) + ' EHB' + (tile.overridden ? ' (' + t('manual') + ')' : '');
          cell.setAttribute('aria-label', where + ': ' + tile.name + ', ' + estimate + (tile.parts > 1 ? ', ' + t('{0} objectives', tile.parts) : '') + (tile.locked ? ', ' + t('evidence submitted') : '') + (tile.changed ? ', ' + t('changed in this correction') : '')
            + (m ? (m.from === pos ? ', ' + t('moving') : ', ' + t('press Enter to swap here')) : me ? '. ' + t('Enter to edit, M to move') : '. ' + t('Enter to view')));
          on(cell, 'dragstart', event => { if (!ctx.canEdit() || ctx.blocked()) { event.preventDefault(); return; } try { event.dataTransfer.effectAllowed = 'move'; event.dataTransfer.setData('text/plain', tile.id); } catch { /* Safari */ } ctx.dragFrom = pos; wrap.classList.add('is-src'); });
          on(cell, 'dragend', () => { ctx.dragFrom = null; paintGrid(); });
        } else {
          cell = button('bcell bempty' + (me ? '' : ' is-readonly') + (issuePositions.includes(pos) ? ' is-issue' : ''), null, () => open(pos), 'be-' + pos);
          const span = el('span'); if (me) span.append(icon('plus')); span.append(document.createTextNode(me ? t('Add tile') : t('Empty'))); cell.append(span);
          cell.setAttribute('aria-label', where + ': ' + t('empty') + (m ? ', ' + t('press Enter to move here') : me ? '. ' + t('Enter to add a tile') : ''));
        }
        cell.tabIndex = focus ? 0 : -1;
        on(wrap, 'dragover', event => { if (ctx.dragFrom == null) return; event.preventDefault(); wrap.classList.toggle('is-target', ctx.dragFrom !== pos); });
        on(wrap, 'dragleave', () => wrap.classList.remove('is-target'));
        on(wrap, 'drop', event => { event.preventDefault(); const from = ctx.dragFrom; ctx.dragFrom = null; if (from != null && from !== pos) void move(from, pos); else paintGrid(); });
        wrap.append(cell); row.append(wrap);
      }
      row.append(lineCell(lineVm(v.lines.rows[r], 'row', r, average, full.length)));
      rows.push(row);
    }
    const totals = el('div', 'bgrid-row'); totals.setAttribute('role', 'row');
    v.lines.cols.forEach((line, i) => totals.append(lineCell(lineVm(line, 'col', i, average, full.length), 'is-col')));
    const corner = el('div', 'bline is-corner', 'EHB'); corner.setAttribute('role', 'gridcell'); corner.setAttribute('aria-label', t('Line totals in EHB')); totals.append(corner);
    rows.push(totals);
    grid.replaceChildren(...rows);
    root.querySelector('#grid-help').textContent = me
      ? t('Arrow keys move between positions. Enter edits a tile or adds one; M picks a tile up to move or swap it (or drag it), Delete removes it. Row and column totals are in EHB; ↑ and ↓ mark lines 25% or more from the average.')
      : t('Arrow keys move between positions; Enter opens a tile. Row and column totals are in EHB.');
    moveHost.replaceChildren();
    if (m) {
      const bar = el('div', 'bmove-bar'); bar.setAttribute('role', 'status');
      const from = ctx.tileAt(m.from);
      bar.append(icon('move'), el('span', 'grow', t('Moving {0} from {1}. Choose a position with the arrow keys and press Enter; Escape cancels.', from?.name || '', ctx.where(m.from))), button('btn btn-sm', t('Cancel'), () => cancelMove(), 'move-cancel'));
      moveHost.append(bar);
    }
  }
  function open(pos) {
    if (ctx.moving) { void move(ctx.moving.from, pos); return; }
    ctx.focusPos = pos;
    if (!ctx.tileAt(pos) && !ctx.canEdit()) return;
    ctx.hooks.openTile?.(pos);
  }
  function startMove(pos) { if (!ctx.canEdit() || ctx.blocked() || !ctx.tileAt(pos)) return; ctx.moving = { from: pos, to: pos }; paintGrid(); ctx.focusSoon(ctx.cellId(pos)); }
  function cancelMove() { const m = ctx.moving; if (!m) return; ctx.moving = null; ctx.focusPos = m.from; paintGrid(); ctx.focusSoon(ctx.cellId(m.from)); }
  // Move = to an empty position; swap = onto an occupied one. One command each.
  async function move(from, to) {
    ctx.moving = null;
    const tile = ctx.tileAt(from);
    if (!tile || from === to) { paintGrid(); return; }
    const target = ctx.tileAt(to);
    ctx.focusPos = to;
    const outcome = await ctx.command('Move', { sourceId: tile.id, targetPosition: to });
    if (outcome.kind === 'refused' || outcome.kind === 'route-refused') { ui.toast(outcome.message || t('That change wasn’t saved.'), { error: true }); await ctx.refresh(); return; }
    if (outcome.kind === 'unknown') {
      await ctx.uncertain(target ? t('swap {0} and {1}', ctx.where(from), ctx.where(to)) : t('move {0} to {1}', ctx.where(from), ctx.where(to)),
        s => s.working.tiles.some(x => x.tileId === tile.id && x.row * ctx.view.cols + x.column === to));
      return;
    }
    if (outcome.kind !== 'saved' && outcome.kind !== 'state') { await ctx.refresh(); return; }
    await ctx.refresh();
    ctx.ui.toast(target ? t('Swapped {0} and {1}.', ctx.where(from), ctx.where(to)) : t('Moved to {0}.', ctx.where(to)));
    ctx.focusSoon(ctx.cellId(to));
  }
  on(root, 'keydown', event => {
    const cell = event.target.closest('.bcell[id]');
    if (!cell || !root.querySelector('#bgrid').contains(cell)) return;
    const v = ctx.view, pos = Number(cell.parentElement.dataset.pos), m = ctx.moving;
    const at = m ? m.to : pos;
    let next = null;
    if (event.key === 'ArrowRight') next = at % v.cols < v.cols - 1 ? at + 1 : at;
    else if (event.key === 'ArrowLeft') next = at % v.cols > 0 ? at - 1 : at;
    else if (event.key === 'ArrowDown') next = at + v.cols < v.rows * v.cols ? at + v.cols : at;
    else if (event.key === 'ArrowUp') next = at - v.cols >= 0 ? at - v.cols : at;
    else if (event.key === 'Home') next = at - at % v.cols;
    else if (event.key === 'End') next = at - at % v.cols + v.cols - 1;
    if (next !== null) { event.preventDefault(); if (m) { m.to = next; paintGrid(); } else ctx.focusPos = next; ctx.focusSoon(m ? ctx.cellId(next) : ctx.cellId(next)); return; }
    if (event.key === 'Escape' && m) { event.preventDefault(); event.stopPropagation(); cancelMove(); return; }
    if ((event.key === 'm' || event.key === 'M') && !m) { event.preventDefault(); startMove(pos); return; }
    if (event.key === 'Enter' && m) { event.preventDefault(); void move(m.from, m.to); return; }
    if ((event.key === 'Delete' || event.key === 'Backspace') && !m && ctx.tileAt(pos) && ctx.canEdit()) { event.preventDefault(); ctx.hooks.removeTile?.(ctx.tileAt(pos)); }
  });

  /* ---------------- planning ---------------- */
  const plan = root.querySelector('[data-board-plan]');
  function paintPlan() {
    const v = ctx.view, st = v.stats;
    if (!st) return;
    const total = plan.querySelector('[data-plan-total]'); total.textContent = fmt1(st.total);
    const sizeDd = plan.querySelector('[data-plan-size]');
    const lockedSize = !v.teamSizeEditable || v.control?.who === 'other' || v.readOnly;
    sizeDd.replaceChildren();
    if (lockedSize) {
      // Rule 18: read-only values use the read-only field box with the lock icon.
      const ro = el('div', 'ro-value'); ro.append(icon('lock'), document.createTextNode(' ' + (st.teamSize ?? t('Not set'))));
      ro.id = 'team-size'; ro.setAttribute('aria-labelledby', 'team-size-lbl');
      sizeDd.append(ro);
    } else {
      const input = el('input', 'input' + (ctx.sizeErr ? ' is-invalid' : '')); input.id = 'team-size'; input.type = 'text'; input.inputMode = 'numeric';
      input.value = ctx.sizeDraft ?? String(st.teamSize ?? ''); input.disabled = ctx.blocked() && ctx.pending !== 'TeamSize';
      input.setAttribute('aria-describedby', 'team-size-hint' + (ctx.sizeErr ? ' team-size-err' : '')); input.setAttribute('aria-invalid', ctx.sizeErr ? 'true' : 'false');
      on(input, 'input', () => { ctx.sizeDraft = input.value; ctx.sizeErr = ''; ctx.sizeSaved = false; });
      on(input, 'blur', () => void commitSize());
      on(input, 'keydown', event => { if (event.key === 'Enter') { event.preventDefault(); void commitSize(); } else if (event.key === 'Escape' && ctx.sizeDraft != null) { event.preventDefault(); event.stopPropagation(); ctx.sizeDraft = null; ctx.sizeErr = ''; paintPlan(); } });
      sizeDd.append(input);
      if (ctx.pending === 'TeamSize') { const s = el('span', 'spin'); s.setAttribute('aria-label', t('Saving')); sizeDd.append(s); }
      if (ctx.sizeSaved) { const ok = el('span', 'plan-ok'); ok.setAttribute('role', 'status'); ok.append(icon('check'), el('span', 'sr', t('Saved'))); sizeDd.append(ok); }
    }
    const err = plan.querySelector('[data-plan-size-err]'); err.hidden = !ctx.sizeErr; err.querySelector('[data-text]').textContent = ctx.sizeErr;
    plan.querySelector('[data-plan-per-player]').textContent = st.perPlayer == null ? '—' : fmt1(st.perPlayer);
    const perDay = plan.querySelector('[data-plan-per-day]'); perDay.replaceChildren(document.createTextNode(st.perDay == null ? '—' : fmt1(st.perDay)), el('small', null, ' ' + t('over {0} days', fmt1(st.days))));
    const full = [...v.lines.rows, ...v.lines.cols].filter(l => l.full), avg = full.length ? full.reduce((a, l) => a + l.total, 0) / full.length : null;
    const spread = full.length > 1 ? Math.max(...full.map(l => l.total)) - Math.min(...full.map(l => l.total)) : null;
    plan.querySelector('[data-plan-spread]').replaceChildren(document.createTextNode(spread == null ? '—' : fmt1(spread)), el('small', null, spread == null || !avg ? '' : ' · ' + Math.round(spread / avg * 100) + '%'));
    plan.querySelector('[data-plan-hint]').textContent = lockedSize && v.mode !== 'none' && !v.teamSizeEditable && !v.readOnly
      ? t('Players per team can only be changed before the event goes Live.')
      : t('An estimate for the planning figures. It doesn’t change rosters or scoring.');
    paintMore();
  }
  // "More figures" toggles in place: the planning inputs keep their identity and focus (rule 9).
  function paintMore() {
    const v = ctx.view, st = v.stats;
    if (!st) return;
    const toggle = plan.querySelector('#plan-more'); toggle.setAttribute('aria-expanded', String(ctx.planMore)); toggle.classList.toggle('is-open', ctx.planMore);
    const list = plan.querySelector('#plan-more-list'); list.hidden = !ctx.planMore;
    const more = [[t('Tiles'), t('{0} of {1}', st.filled, st.cells)], [t('Average tile'), st.avgTile == null ? '—' : fmt1(st.avgTile) + ' EHB'], [t('Lowest line'), st.lo ? fmt1(st.lo) + ' EHB' : '—'],
      [t('Highest line'), st.hi ? fmt1(st.hi) + ' EHB' : '—'], [t('Without an estimate'), st.missing ? t(st.missing === 1 ? '{0} tile' : '{0} tiles', st.missing) : t('None')], [t('Event length'), t('{0} days', fmt1(st.days))]];
    list.replaceChildren(...more.flatMap(([k, value]) => [el('dt', null, k), el('dd', null, value)]));
  }
  on(root.querySelector('#plan-more'), 'click', () => { ctx.planMore = !ctx.planMore; paintMore(); });
  async function commitSize() {
    if (ctx.sizeDraft == null || ctx.pending) return;
    const value = parseWhole(ctx.sizeDraft, 1, 100);
    if (String(ctx.sizeDraft).trim() === String(ctx.view.stats.teamSize ?? '')) { ctx.sizeDraft = null; return; }
    if (value == null) { ctx.sizeErr = t('Enter a whole number from 1 to 100.'); paintPlan(); return; }
    const outcome = await ctx.command('TeamSize', { expectedTeamSize: value });
    if (outcome.kind === 'saved') { ctx.sizeDraft = null; ctx.sizeSaved = true; await ctx.refresh(); return; }
    if (outcome.kind === 'refused' || outcome.kind === 'route-refused') { ctx.sizeErr = outcome.message || t('That change wasn’t saved.'); await ctx.refresh(); ctx.focusSoon('team-size'); return; }
    if (outcome.kind === 'unknown' && await ctx.uncertain(t('change players per team'), s => s.expectedTeamSize === value) === 'happened') { ctx.sizeDraft = null; ctx.sizeSaved = true; paintPlan(); }
    else paintPlan();
  }

  /* ---------------- resize ---------------- */
  ctx.openResize = () => {
    if (!ctx.canEdit() || ctx.blocked()) return;
    const v = ctx.view, state = { rows: v.rows, cols: v.cols, busy: false, error: '' };
    const content = el('div', 'bd-layer');
    const headEl = el('div', 'mf-head'); const title = el('h2', 'm-title', t('Board size')); title.dataset.confirmTitle = ''; const desc = el('p', 'm-body', t('Rows and columns from 1 to 8. Tiles keep their positions.')); desc.dataset.confirmDescription = '';
    headEl.append(title, desc);
    const bodyEl = el('div', 'mf-body'), bannerSlot = el('div'), sizeRow = el('div', 'bd-size-row'), impact = el('div'); impact.id = 'rz-impact'; impact.setAttribute('role', 'status');
    const stepper = (key, label, less, more) => {
      const f = el('div', 'field'), lbl = el('label', 'lbl', t(label)); lbl.htmlFor = 'rz-' + key;
      const s = el('div', 'bd-stepper'), down = button('icon-btn', null, () => set(key, state[key] - 1)), input = el('input', 'input'), up = button('icon-btn', null, () => set(key, state[key] + 1));
      down.setAttribute('aria-label', t(less)); up.setAttribute('aria-label', t(more)); down.append(icon('minus')); up.append(icon('plus'));
      input.id = 'rz-' + key; input.type = 'text'; input.inputMode = 'numeric'; input.setAttribute('aria-describedby', 'rz-impact');
      on(input, 'input', () => { const n = parseWhole(input.value, 1, 8); if (n) { state[key] = n; paintRz(false); } });
      s.append(down, input, up); f.append(lbl, s);
      return { f, input, down, up };
    };
    const rowsCtl = /*labels*/stepper('rows', 'Rows', 'Fewer rows', 'More rows'), colsCtl = stepper('cols', 'Columns', 'Fewer columns', 'More columns')/*end*/;
    const x = el('span', 'bd-size-x', '×'); x.setAttribute('aria-hidden', 'true');
    sizeRow.append(rowsCtl.f, x, colsCtl.f); bodyEl.append(bannerSlot, sizeRow, impact);
    const foot = el('div', 'mf-foot'), cancel = button('btn', t('Cancel'), () => void layer.close(false), 'rz-cancel'), apply = button('btn btn-primary', null, () => void applyRz(), 'rz-apply');
    foot.append(cancel, apply); content.append(headEl, bodyEl, foot);
    function set(key, value) { state[key] = Math.max(1, Math.min(8, value)); paintRz(true); }
    const outside = () => v.tiles.filter(tile => Math.floor(tile.pos / v.cols) >= state.rows || tile.pos % v.cols >= state.cols);
    function paintRz(syncInputs) {
      if (syncInputs) { rowsCtl.input.value = state.rows; colsCtl.input.value = state.cols; }
      rowsCtl.down.disabled = state.rows <= 1 || state.busy; rowsCtl.up.disabled = state.rows >= 8 || state.busy; colsCtl.down.disabled = state.cols <= 1 || state.busy; colsCtl.up.disabled = state.cols >= 8 || state.busy;
      rowsCtl.input.disabled = colsCtl.input.disabled = state.busy;
      const blocked = outside(), same = state.rows === v.rows && state.cols === v.cols;
      impact.className = blocked.length ? 'bd-rz-impact is-blocked' : 'bd-rz-impact';
      const p = el('p', 'bd-rz-text', same ? t('This is the current size.') : blocked.length
        ? t('Move or remove these tiles first; they’re outside {0} × {1}:', state.rows, state.cols)
        : t('{0} × {1} has {2} positions; {3} tiles keep their positions.', state.rows, state.cols, state.rows * state.cols, v.tiles.length) + (v.mode === 'approved' ? ' ' + t('The board returns to draft.') : ''));
      impact.replaceChildren(p);
      if (blocked.length) { const ul = el('ul', 'bd-rz-list'); for (const tile of blocked) ul.append(el('li', null, ctx.where(tile.pos) + ' ' + tile.name)); impact.append(ul); }
      apply.replaceChildren(...(state.busy ? [el('span', 'spin')] : []), document.createTextNode(state.busy ? t('Applying…') : t('Apply size')));
      apply.classList.toggle('is-busy', state.busy); apply.disabled = state.busy; cancel.disabled = state.busy;
      apply.setAttribute('aria-disabled', blocked.length || same ? 'true' : 'false');
      bannerSlot.replaceChildren(...(state.error ? [banner({ cls: 'is-error', role: 'alert', title: state.error })] : []));
    }
    content.dataset.pageFamily = 'board';
    const layer = ui.openLayer({ title: t('Board size'), content, pending: () => state.busy }); layer.element.dataset.pageFamily = 'board';
    layer.element.classList.add('modal-form');
    paintRz(true); layer.markClean();
    async function applyRz() {
      if (state.busy || outside().length || (state.rows === v.rows && state.cols === v.cols)) return;
      state.busy = true; state.error = ''; paintRz(false);
      const target = { rows: state.rows, cols: state.cols };
      const outcome = await ctx.command('Resize', { Rows: target.rows, Columns: target.cols });
      state.busy = false;
      if (outcome.kind === 'saved') { await layer.close(true); await ctx.refresh(); ui.toast(t('Board resized to {0} × {1}.', target.rows, target.cols)); ctx.focusSoon('resize-btn'); return; }
      if (outcome.kind === 'refused' || outcome.kind === 'route-refused') { state.error = outcome.message || t('That change wasn’t saved.'); paintRz(false); await ctx.refresh(); return; }
      if (outcome.kind === 'unknown') { await layer.close(true); await ctx.uncertain(t('resize the board to {0} × {1}', target.rows, target.cols), s => s.working.rows === target.rows && s.working.columns === target.cols); return; }
      paintRz(false);
    }
  };

  /* ---------------- preview (U7-Q3: placeholder; never approves or publishes) ---------------- */
  ctx.openPreview = () => {
    const v = ctx.view;
    const eyebrow = /*labels*/{ draft: 'Preview · current draft values · nothing is approved or published', approved: 'Preview · the approved version · not published', published: 'Preview · what players see now', correction: 'Preview · your private correction · players still see the published version' }/*end*/[v.mode] || 'Preview';
    const content = el('div', 'bd-layer');
    const top = el('div', 'bd-pv-head'), grow = el('div', 'grow'), title = el('h2', 'm-title', v.eventName); title.dataset.confirmTitle = '';
    grow.append(el('div', 'eyebrow', t(eyebrow)), title);
    const close = button('icon-btn', null, () => void layer.close(false), 'pv-close'); close.setAttribute('aria-label', t('Close preview')); close.append(icon('close'));
    top.append(grow, close);
    const pvBody = el('div', 'bd-pv-body'), placeholder = el('div', 'bd-pv-none', t('Not supported yet'));
    pvBody.append(placeholder); content.append(top, pvBody);
    content.dataset.pageFamily = 'board';
    const layer = ui.openLayer({ title: t('Preview'), content }); layer.element.dataset.pageFamily = 'board';
    layer.element.classList.add('bd-pv'); layer.element.dataset.pageFamily = 'board';
  };

  /* ---------------- paint ---------------- */
  function paint() {
    paintHead(); paintBanner(); paintGrid(); paintPlan();
    for (const hook of ctx.hooks.afterPaint) hook();
  }
  ctx.paint = paint;
  for (const install of extensions) install(ctx);
  paint();
  const unregister = ui.registerDraft(root, { isDirty: () => false, isPending: () => !!ctx.pending || ctx.checking });
  release = () => { life.abort(); unregister(); for (const fn of ctx.hooks.release || []) fn(); };
  for (const fn of ctx.hooks.ready || []) fn();
}

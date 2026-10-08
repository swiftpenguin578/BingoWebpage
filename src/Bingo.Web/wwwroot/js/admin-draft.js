// Teams / Draft (U6, brief 93): TeamsDraft.dc.html on the new layout. One workspace
// drawn from the server's page state (?handler=State, no-store), re-read after every
// command and live update. Commands post through AdminFetch inside AdminUI.busy and
// read an explicit JSON outcome: "done" is success, "refused"/"stale" is a definite
// refusal shown with the server's reason, anything else is uncertain. An uncertain
// outcome keeps the intended ids/fields, re-reads the authoritative readback once
// (no blind retry) and says what is now true; a matching state never proves that this
// request made it (AU14). The shell owns transport classification (C-CMP-2), busy
// timing, layers, menus, toasts and history.
let release;
export function dispose() { release?.(); release = null; }
export function init(region, ui = window.AdminUI) {
  dispose();
  const root = region.querySelector('[data-draft]');
  if (!root) return;
  const labels = JSON.parse(root.dataset.labels || '{}');
  const t = (key, ...args) => (labels[key] ?? key).replace(/\{(\d)\}/g, (_match, i) => args[i] ?? '');
  const life = new AbortController(), signal = life.signal;
  const eventId = root.dataset.eventId, me = (root.dataset.accountId || '').toLowerCase();
  const body = root.querySelector('[data-draft-body]'), menu = root.querySelector('[data-draft-menu-panel]');
  const head = region.querySelector('.page-head'), pageEl = region.querySelector('.page');
  const lang = document.documentElement.lang || 'en';
  let S = JSON.parse(root.querySelector('[data-draft-state]').textContent);
  const L = { pending: null, unsure: null, checking: false, notice: null, fresh: null, q: '', sort: 'ehb', poolFocus: null, live: '', swap: 'a', settling: false, loadError: false, own: null, offline: false };
  let menuSpec = null;

  /* ---------------- helpers ---------------- */
  function h(tag, props, ...kids) {
    const el = document.createElement(tag);
    for (const [key, value] of Object.entries(props || {})) {
      if (value == null || value === false) continue;
      if (key === 'class') el.className = value;
      else if (key === 'text') el.textContent = value;
      else if (key.startsWith('on') && typeof value === 'function') el.addEventListener(key.slice(2), value, { signal });
      else if (key === 'dataset') Object.assign(el.dataset, value);
      else el.setAttribute(key, value === true ? '' : String(value));
    }
    for (const kid of kids.flat(Infinity)) if (kid != null && kid !== false) el.append(kid);
    return el;
  }
  const iconTemplate = root.querySelector('template[data-draft-icons]');
  const icon = name => iconTemplate.content.querySelector(`[data-icon="${name}"]`)?.cloneNode(true) || null;
  const num = value => Math.round(Number(value) || 0).toLocaleString(lang);
  const plural = (n, one, many) => t(n === 1 ? one : many, num(n));
  const ROLE = { C: () => t('Captain'), CC: () => t('Co-captain'), P: () => t('Participant') };
  const ROLE_ENUM = { P: 'Participant', C: 'Captain', CC: 'CoCaptain' }, ROLE_NUM = { P: 1, C: 2, CC: 3 };
  const url = handler => `/Admin/Events/Draft/${eventId}?handler=${handler}`;
  const team = id => S.teams.find(x => x.id === id);
  const drafted = () => S.teams.filter(x => x.included);
  const manual = () => drafted().length < 2;
  const memberOf = pid => S.teams.find(x => x.members.some(m => m.participantId === pid));
  const pname = pid => S.participants.find(x => x.id === pid)?.name || S.teams.flatMap(x => x.members).find(m => m.participantId === pid)?.name || '';
  const stage = () => S.stage;
  const canCorrect = () => S.stage === 'final' && S.canCorrect;
  const busy = () => !!L.pending || L.checking;
  const blocked = () => busy() || !!L.unsure || L.settling;
  const inControl = () => S.control.who === 'me';
  const focusSoon = (id, options) => requestAnimationFrame(() => document.getElementById(id)?.focus(options));
  const dayText = value => value ? new Date(value).toLocaleDateString(lang, { day: 'numeric', month: 'short' }) : '';

  /* ---------------- transport ---------------- */
  const post = (handler, values, quick = false) => ui.busy(() => window.AdminFetch.request(url(handler), { method: 'POST', body: new URLSearchParams(values), expect: 'json', draft: L.pending?.draft, signal }), quick);
  async function readState() {
    const outcome = await window.AdminFetch.request(url('State'), { expect: 'json', signal, readback: true, draft: L.pending?.draft });
    if (outcome.kind !== 'handler' || !outcome.data || !Array.isArray(outcome.data.teams)) return false;
    S = outcome.data; return true;
  }
  async function readback() {
    const outcome = await window.AdminFetch.request(url('Readback'), { expect: 'json', signal, readback: true });
    return outcome.kind === 'handler' && outcome.data?.known === true ? outcome.data.state : null;
  }
  async function refresh() {
    const before = { stage: S.stage, who: S.control?.who };
    if (!await readState()) return false;
    L.loadError = false;
    // Control left this admin without their own release/cancel/finalize: say what happened.
    if (before.stage === 'running' && S.stage === 'running' && before.who === 'me' && S.control.who !== 'me' && !['release', 'cancel', 'finalize'].includes(L.own))
      L.notice = S.control.who === 'other' ? lostNotice() : lapsedNotice();
    if (S.stage !== 'running' && L.notice?.kind && ['lost', 'lapsed', 'offline'].includes(L.notice.kind)) L.notice = null;
    render(); return true;
  }
  const LEASE_MIN = 5; // DraftControlLease.Duration
  function lostNotice() { return { kind: 'lost', cls: 'is-warning', title: t('Another admin took control of the draft.'), text: t('Your picks won’t go through until you take control back.'), action: t('Take over…'), run: () => openCx('takeover') }; }
  function lapsedNotice() { return { kind: 'lapsed', cls: 'is-warning', title: t('Your control lapsed.'), text: t('Control is released when this page can’t renew it for {0} minutes, for example after a lost connection. Take control to keep picking.', num(LEASE_MIN)), action: t('Take control'), run: () => takeControl() }; }
  function offlineNotice() { return { kind: 'offline', cls: 'is-warning', title: t('Live updates stopped.'), text: t('While this page is disconnected it can’t renew your draft control or show other admins’ changes. Control lapses {0} minutes after its last renewal.', num(LEASE_MIN)), action: t('Reload'), run: () => void ui.navigate(location.pathname + location.search, { mode: 'replace' }) }; }

  // One runner for every command (reference run/settleRun). o: { handler, values, what,
  // kind, layer, quick, pending, verify(readbackState), okText, notText, onOk(data) }.
  async function run(o) {
    if (blocked()) return;
    L.pending = Object.assign({ kind: o.kind, draft: o.draft }, o.pending || {});
    L.notice = null; L.own = o.kind;
    o.layer?.setBusy(true);
    render();
    const result = await post(o.handler, o.values, !!o.quick);
    L.pending = null;
    o.layer?.setBusy(false);
    if (signal.aborted) return;
    if (result.kind === 'handler' && typeof result.data?.outcome === 'string') {
      const data = result.data;
      if (data.outcome === 'done') {
        await o.layer?.close();
        await refresh(); L.own = null;
        o.onOk?.(data);
        return;
      }
      if (data.outcome === 'refused' || data.outcome === 'stale') {
        await refresh(); L.own = null;
        const text = data.message || t('That didn’t go through.');
        if (o.layer?.open()) o.layer.error(t('Couldn’t {0}.', o.what), text);
        else ui.toast(t('Couldn’t {0}.', o.what) + ' ' + text, { error: true });
        o.onRefused?.(data);
        return;
      }
    }
    if (result.kind === 'session-lost') { L.own = null; render(); return; } // AdminFetch shows what wasn't saved; the layer and its draft stay.
    if (result.kind === 'refused') {
      await refresh(); L.own = null;
      const text = result.reason || t('This event is read-only in its current lifecycle state.');
      if (o.layer?.open()) o.layer.error(t('Couldn’t {0}.', o.what), text); else ui.toast(t('Couldn’t {0}.', o.what) + ' ' + text, { error: true });
      return;
    }
    // Uncertain: a lost response, a non-JSON answer or a JSON answer without an outcome.
    L.own = null;
    await o.layer?.close();
    L.unsure = { kind: o.kind, what: o.what, verify: o.verify, okText: o.okText, notText: o.notText, onOk: o.onOk };
    render(); focusSoon('td-banner');
    await check();
  }
  async function check() {
    const u = L.unsure; if (!u || L.checking) return;
    L.checking = true; render();
    const state = await readback();
    L.checking = false;
    if (!state) { render(); focusSoon('td-banner'); return; } // still unknown: no blind retry
    const happened = !!u.verify?.(state);
    L.unsure = null; L.own = u.kind;
    await refresh(); L.own = null;
    L.notice = { cls: happened ? 'is-info' : 'is-warning', title: t('We couldn’t confirm the response.'), text: (happened ? u.okText : u.notText) + ' ' + t('The current teams are shown; check them before trying again.') };
    render(); focusSoon('td-banner');
  }

  /* ---------------- readiness and summary ---------------- */
  function readiness() {
    const checks = [];
    checks.push({ done: S.signupClosed, label: S.signupClosed ? t('Signups are closed') : t('Close signups first'), link: S.signupClosed ? null : { label: t('Overview'), href: `/Admin/Events/Manage/${eventId}` } });
    // U6-Q2: the draft can only be finalized while the configured end is in the future.
    if (S.endMissingOrPast) checks.push({ done: false, label: t('Set the event end in Schedule'), link: { label: t('Schedule'), href: `/Admin/Events/Schedule/${eventId}` } });
    if (manual()) {
      const empty = S.teams.filter(x => !x.members.length);
      if (S.unplaced && S.teams.length) checks.push({ info: true, label: plural(S.unplaced, '{0} confirmed player isn’t on a team. They won’t be on a published roster; this doesn’t block finalizing.', '{0} confirmed players aren’t on a team. They won’t be on a published roster; this doesn’t block finalizing.') });
      checks.push({ done: S.teams.length > 0 && !empty.length, label: !S.teams.length ? t('Add at least one team') : empty.length ? (empty.length === 1 ? t('{0} needs a member', empty[0].name) : t('{0} teams need a member', num(empty.length))) : t('Every team has a member'),
        action: empty.length ? { label: t('Add'), run: () => openPs('member', empty[0].id) } : null });
    } else {
      const noCap = drafted().filter(x => !x.hasUsableCaptain);
      checks.push({ done: !noCap.length, label: !noCap.length ? t('Every drafted team has a captain') : noCap.length === 1 ? t('{0} needs a captain', noCap[0].name) : t('{0} drafted teams need a captain', num(noCap.length)),
        action: noCap.length ? { label: t('Assign'), run: () => openPs('captain', noCap[0].id) } : null });
      checks.push({ done: !S.blockers.length, label: S.blockers.length ? S.blockers[0] : t('Team sizes work out') });
    }
    const first = checks.find(c => !c.done && !c.info);
    return { ok: !first, checks };
  }
  function sizeSummary() {
    const d = S.distribution, manualTeams = S.teams.filter(x => !x.included), onManual = manualTeams.reduce((a, x) => a + x.members.length, 0);
    if (!d) {
      const placed = S.participants.filter(p => memberOf(p.id)).length, left = S.participants.length - placed;
      return [plural(S.participants.length, '{0} confirmed player', '{0} confirmed players'), t('{0} on teams', num(placed))].concat(left ? [t('{0} not on a team yet', num(left))] : []).join(' · ') + (drafted().length === 1 ? '. ' + t('A website draft needs at least two drafted teams.') : '.');
    }
    const shape = d.largerCount ? t('{0} of {1} and {2} of {3}', num(d.largerCount), num(d.larger), num(d.teams - d.largerCount), num(d.smaller)) : t('{0} teams of {1}', num(d.teams), num(d.smaller));
    let text = t('{0} players across {1} drafted teams: {2}.', num(d.included), num(d.teams), shape);
    if (manualTeams.length) text += ' ' + (manualTeams.length === 1 ? t('1 manual-roster team ({0}) isn’t drafted.', plural(onManual, '{0} player', '{0} players')) : t('{0} manual-roster teams ({1}) aren’t drafted.', num(manualTeams.length), plural(onManual, '{0} player', '{0} players')));
    return text;
  }
  function summaryText() {
    const st = stage();
    if (st === 'setup') return !S.teams.length ? t('Add a team for each captain to get started.') : manual() ? t('Assemble each roster by hand, then finalize to publish them.') : t('Prepare teams and captains, then run the snake draft.');
    if (st === 'final') return t('Published rosters for {0}.', S.eventName);
    if (st === 'running') return '';
    return t('Rosters for {0}.', S.eventName);
  }

  /* ---------------- render ---------------- */
  function keepFocus(fn) {
    const active = document.activeElement, id = active && root.contains(active) ? active.id : '';
    const selection = active && 'selectionStart' in active ? [active.selectionStart, active.selectionEnd] : null;
    fn();
    if (!id) return;
    const next = document.getElementById(id);
    if (next && next !== document.activeElement) { next.focus({ preventScroll: true }); if (selection && 'setSelectionRange' in next) try { next.setSelectionRange(...selection); } catch { /* not a text input */ } }
  }
  // Places nodes without detaching the ones already in position, so the running view's
  // persistent controls (pool search, sort) keep their identity, focus and scroll (rule 9).
  function place(parent, nodes) {
    const keep = new Set(nodes);
    for (const child of [...parent.childNodes]) if (!keep.has(child)) child.remove();
    let ref = parent.firstChild;
    for (const node of nodes) { if (node === ref) { ref = node.nextSibling; continue; } parent.insertBefore(node, ref); }
  }
  const liveEl = h('p', { class: 'sr', 'aria-live': 'polite' });
  function render() {
    keepFocus(() => {
      const st = stage();
      renderHead(st);
      const parts = [];
      const banner = vmBanner(st);
      if (banner) parts.push(bannerEl(banner));
      if (L.loadError) parts.push(loadFailed());
      else if (st === 'running') parts.push(renderRunning());
      else {
        if (st === 'setup') parts.push(...renderSetupTop());
        parts.push(renderCards(st));
      }
      if (liveEl.textContent !== L.live) liveEl.textContent = L.live;
      parts.push(liveEl);
      place(body, parts);
      pageEl?.classList.toggle('is-live', st === 'running');
      head?.classList.toggle('sr', st === 'running');
      side(st === 'running' && !L.loadError);
    });
  }
  function renderHead(st) {
    const summary = head?.querySelector('.summary');
    if (summary) summary.replaceChildren(h('span', { 'data-draft-summary': true, text: summaryText() }));
    const slot = region.querySelector('[data-draft-head]');
    if (!slot) return;
    const rd = st === 'setup' ? readiness() : null;
    let action = null;
    if (st === 'setup' && S.teams.length) {
      const inert = !rd.ok || blocked();
      const label = manual() ? t('Finalize rosters…') : t('Start draft…');
      action = h('button', { class: 'btn btn-primary', type: 'button', id: 'head-action', 'aria-disabled': inert ? 'true' : 'false', 'aria-describedby': 'ready-checks', title: rd.ok ? null : t('Finish the steps above first'), text: label,
        onclick: () => { if (rd.ok && !blocked()) openCx(manual() ? 'finalize' : 'start'); else focusSoon('ready-title'); } });
    } else if (['final', 'locked', 'review', 'terminal'].includes(st)) action = openBoard();
    slot.replaceChildren(...(action ? [action] : []));
  }
  // Planner ruling 2 (8 October): "Open Board" only when the Board page is reachable.
  function openBoard() {
    if (S.boardExists) return h('a', { class: 'btn', href: `/Admin/Events/Board/${eventId}`, 'data-shell-link': true, id: 'head-action', text: t('Open Board') });
    return h('button', { class: 'btn', type: 'button', id: 'head-action', 'aria-disabled': 'true', title: t('No board yet. Create one on the Board page.'), text: t('Open Board') });
  }
  function bannerEl(b) {
    return h('div', { class: `banner ${b.cls} page-banner`, role: 'alert', id: 'td-banner', tabindex: '-1' }, icon(b.cls === 'is-info' ? 'info' : 'warning'),
      h('span', { class: 'grow' }, h('b', { text: b.title }), ' ' + (b.text || '')),
      b.action ? h('button', { class: 'banner-btn', id: 'td-banner-act', type: 'button', disabled: b.busy, text: b.action, onclick: b.run }) : null);
  }
  function vmBanner(st) {
    if (L.unsure || L.checking) return { cls: 'is-warning', title: L.checking ? t('Checking…') : t('We couldn’t confirm whether this went through: {0}.', L.unsure.what), text: L.checking ? '' : t('The current state didn’t load. Check before trying again, so nothing happens twice.'), action: L.checking ? null : t('Check again'), busy: L.checking, run: () => check() };
    if (L.notice) return L.notice;
    if (st === 'final') return { cls: 'is-info', title: t('Rosters published {0}.', dayText(S.publishedAt)), text: t('Until {0} first goes Live, you can add or remove members and change roles. Each change republishes the rosters.', S.eventName) };
    if (st === 'locked') return { cls: 'is-info', title: t('Rosters are locked.'), text: t('{0} went Live {1}, so members can no longer be added or removed. Captain and Co-captain roles can still change.', S.eventName, dayText(S.liveSince)) };
    // T-24 (planner default, to confirm at the early look): final rosters read-only.
    if (st === 'review') return { cls: 'is-info', title: t('Rosters are final.'), text: S.canChangeRoles ? t('{0} is in final review. Members can’t change; Captain and Co-captain roles can still change while uploads are open.', S.eventName) : t('{0} is in final review and uploads are closed, so rosters and roles can no longer change.', S.eventName) };
    // A historical paused draft (Pause retired) stays read-only; the server refuses every change.
    if (st === 'paused') return { cls: 'is-info', title: t('Historical paused draft.'), text: t('Pausing was retired, so this draft is read-only and no roster changes are available.') };
    if (st === 'terminal') return { cls: 'is-info', title: S.eventState === 'Cancelled' ? t('This event is cancelled.') : S.eventState === 'Archived' ? t('This event is archived.') : t('This event is finished.'), text: t('Rosters are shown as they were.') };
    return null;
  }
  function loadFailed() {
    return h('div', { class: 'card' }, h('div', { class: 'empty is-error' }, h('div', { class: 'empty-ic' }, icon('error')), h('div', { class: 'empty-title', text: t('Couldn’t load the teams') }),
      h('div', { class: 'empty-text', text: t('Check your connection and try again. Nothing was changed.') }), h('button', { class: 'btn', type: 'button', id: 'retry-btn', text: t('Try again'), onclick: async () => { if (!await refresh()) { L.loadError = true; render(); focusSoon('retry-btn'); } } })));
  }
  function renderSetupTop() {
    const rd = readiness();
    const title = !S.teams.length ? t('Getting started') : rd.ok ? (manual() ? t('Ready to finalize') : t('Ready to start the draft')) : (manual() ? t('Before finalizing') : t('Before starting the draft'));
    const checks = h('ul', { class: 'td-checks', id: 'ready-checks', 'aria-label': t('Readiness') }, rd.checks.map(c =>
      h('li', { class: `td-check ${c.info ? 'is-info' : c.done ? 'is-done' : ''}` }, h('span', { class: 'check-mark', 'aria-hidden': 'true' }, c.done ? icon('check') : null),
        h('span', { class: 'grow' }, c.label, h('span', { class: 'sr', text: c.info ? ' ' + t('(note)') : c.done ? ' ' + t('(done)') : ' ' + t('(to do)') })),
        c.link ? h('a', { class: 'text-btn', href: c.link.href, 'data-shell-link': true, text: c.link.label }) : null,
        c.action ? h('button', { class: 'text-btn', type: 'button', disabled: blocked(), text: c.action.label, onclick: c.action.run }) : null)));
    return [
      h('section', { class: 'card td-ready', 'aria-labelledby': 'ready-title' }, h('div', { class: 'td-ready-main' },
        h('div', { class: 'td-ready-text' }, h('h2', { class: 'td-ready-title', id: 'ready-title', tabindex: '-1', text: title }), h('p', { class: 'td-ready-sizes', text: sizeSummary() })), checks)),
      h('div', { class: 'td-bar' }, h('h2', { class: 'td-section-title', id: 'teams-title' }, t('Teams'), ' ', h('span', { class: 'td-count', text: num(S.teams.length) })), h('span', { class: 'spacer' }),
        h('button', { class: 'btn btn-sm', type: 'button', id: 'add-team', disabled: blocked(), onclick: () => openTf(null) }, icon('plus'), t('Add team')))
    ];
  }
  function memberTag(st, x, m) {
    if (st === 'setup') return null;
    if (m.tag === 'pick') return { tag: '#' + m.pick, title: t('Drafted with pick {0}', num(m.pick)) };
    if (m.tag === 'correction') return { tag: t('Added'), title: t('Added as a correction after the rosters were published') };
    return x.included ? { tag: t('Pre'), title: t('Preassigned before the draft') } : null;
  }
  const rank = { C: 0, CC: 1, P: 2 };
  function teamOrder(st) {
    const inc = S.teams.filter(x => x.included), man = S.teams.filter(x => !x.included);
    if (st !== 'setup' && inc.every(x => x.position != null)) inc.sort((a, b) => a.position - b.position);
    return inc.concat(man);
  }
  function teamHasMenu(st) { return st === 'setup' || canCorrect() || st === 'locked' || (st === 'review' && S.canChangeRoles); }
  function memberHasMenu(st) { return st === 'setup' ? true : canCorrect() || ((st === 'locked' || st === 'review' || st === 'final') && S.canChangeRoles); }
  function renderCards(st) {
    if (!S.teams.length) return h('div', { class: 'card' }, h('div', { class: 'empty' }, h('div', { class: 'empty-ic' }, icon('teams')), h('div', { class: 'empty-title', text: t('No teams yet') }),
      h('div', { class: 'empty-text', text: st === 'setup' ? t('Add a team for each captain. Teams in the website draft are filled by the draft; manual-roster teams are assembled here.') : t('This event has no teams.') }),
      st === 'setup' ? h('button', { class: 'btn btn-primary', type: 'button', id: 'empty-add-team', disabled: blocked(), text: t('Add team'), onclick: () => openTf(null) }) : null));
    const d = S.distribution, ro = blocked(), corr = canCorrect();
    const finalRange = d ? (d.largerCount ? `${num(d.smaller)}–${num(d.larger)}` : num(d.smaller)) : '';
    return h('div', { class: 'tcards', 'aria-labelledby': st === 'setup' ? 'teams-title' : null, 'aria-label': st === 'setup' ? null : t('Teams') }, teamOrder(st).map(x => {
      const hasCap = x.members.some(m => m.role === 'C');
      let warn = '';
      if (st === 'setup' && x.included && !manual() && !x.hasUsableCaptain) warn = t('Needs a captain before the draft can start.');
      else if (st === 'setup' && d && x.included && x.members.length > d.larger) warn = t('More preassigned members than the final size ({0}).', num(d.larger));
      else if (st === 'setup' && manual() && !x.members.length) warn = t('Needs at least one member before rosters can be finalized.');
      const members = x.members.slice().sort((a, b) => (rank[a.role] - rank[b.role]) || ((a.pick || 999) - (b.pick || 999))).map(m => {
        const tag = memberTag(st, x, m);
        return h('li', { class: `tmem ${m.participantId === L.fresh ? 'is-new' : ''}`, id: 'm-' + m.id },
          m.role !== 'P' ? h('span', { class: `role-badge ${m.role === 'CC' ? 'is-co' : ''}`, title: ROLE[m.role]() }, h('span', { 'aria-hidden': 'true', text: m.role === 'C' ? 'C' : 'CC' }), h('span', { class: 'sr', text: ROLE[m.role]() })) : null,
          h('span', { class: 'tmem-name', text: m.name }),
          tag ? h('span', { class: 'tmem-tag', title: tag.title, text: tag.tag }) : null,
          h('span', { class: 'tmem-ehb', text: num(m.ehb) }),
          memberHasMenu(st) ? h('button', { class: 'icon-btn', type: 'button', id: 'm-' + m.id + '-menu', 'aria-label': t('Actions for {0}', m.name), 'aria-haspopup': 'menu', 'aria-expanded': 'false', 'data-menu-target': 'draft-menu', 'data-menu-align': 'end', disabled: ro, dataset: { draftMenu: 'member', team: x.id, member: m.id } }, icon('more')) : null);
      });
      const ehb = x.members.reduce((a, m) => a + Number(m.ehb || 0), 0);
      const canAddHere = st === 'setup' ? (!x.included || !S.everPicked) : corr;
      let sizeText = plural(x.members.length, '{0} member', '{0} members');
      if (st === 'setup' && x.included && d && !manual()) sizeText = t('{0} preassigned · final size {1}', num(x.members.length), finalRange);
      const badge = x.included ? (manual() && st === 'setup' ? t('Drafted (needs 2 teams)') : t('Website draft')) : t('Manual roster');
      const canCaptain = st === 'setup' && !hasCap && (!x.included || !S.everPicked || x.members.length > 0);
      const foot = (st === 'setup' || corr) && (canAddHere || canCaptain);
      return h('section', { class: 'tcard', id: 'card-' + x.id, 'aria-labelledby': 't-' + x.id + '-name', dataset: { teamCard: x.id } },
        h('div', { class: 'tcard-head' }, h('div', { class: 'grow' }, h('h3', { class: 'tcard-name', id: 't-' + x.id + '-name', text: x.name }),
          h('div', { class: 'tcard-sub' }, h('span', { class: 'badge badge-neutral', text: badge }), h('span', { text: sizeText }), x.members.length ? h('span', { text: t('{0} EHB', num(ehb)) }) : null)),
          teamHasMenu(st) ? h('button', { class: 'icon-btn', type: 'button', id: 't-' + x.id + '-menu', 'aria-label': t('Actions for {0}', x.name), 'aria-haspopup': 'menu', 'aria-expanded': 'false', 'data-menu-target': 'draft-menu', 'data-menu-align': 'end', disabled: ro, dataset: { draftMenu: 'team', team: x.id } }, icon('more')) : null),
        warn ? h('div', { class: 'tcard-warn' }, icon('warning'), warn) : null,
        !x.members.length ? h('div', { class: 'tcard-empty', text: st === 'setup' && x.included && !manual() ? t('No one preassigned yet. Assign a captain; everyone else joins through the draft.') : t('No members yet.') }) : null,
        h('ul', { class: 'tcard-list', 'aria-label': t('Members of {0}', x.name) }, members),
        foot ? h('div', { class: 'tcard-foot' },
          canCaptain ? h('button', { class: 'btn btn-sm', type: 'button', id: 't-' + x.id + '-captain', disabled: ro, text: t('Assign captain'), onclick: () => openPs('captain', x.id) }) : null,
          canAddHere ? h('button', { class: 'btn btn-sm btn-quiet', type: 'button', id: 't-' + x.id + '-add', disabled: ro, onclick: () => openPs(corr ? 'correction' : 'member', x.id) }, icon('plus'), st === 'setup' && x.included && !manual() ? t('Preassign player') : t('Add member')) : null) : null);
    }));
  }
  /* ---------------- running draft (TeamsDraft.dc.html vmTurn/vmBoard/vmPool/vmCtl) ---------------- */
  const available = () => S.participants.filter(p => !p.teamId);
  const complete = () => S.orderReady && !S.turn && !S.blockers.length;
  const drawing = () => L.pending?.kind === 'draw' || L.settling;
  const tokenMs = name => { const v = getComputedStyle(root).getPropertyValue(name).trim(); return v.endsWith('ms') ? parseFloat(v) : (parseFloat(v) || 0) * 1000; };
  function poolList() {
    const q = L.q.trim().toLowerCase();
    const list = available().filter(p => !q || p.name.toLowerCase().includes(q) || (p.account || '').toLowerCase().includes(q));
    return list.sort(L.sort === 'name' ? (a, b) => a.name.localeCompare(b.name, lang) : (a, b) => (b.ehb - a.ehb) || a.name.localeCompare(b.name, lang));
  }
  let R = null;
  function runningNodes() {
    if (R) return R;
    const search = h('input', { class: 'input', id: 'pool-search', autocomplete: 'off', placeholder: t('Find a player'), 'aria-label': t('Find a player'), 'data-1p-ignore': true, 'data-lpignore': 'true', 'data-bwignore': true, 'data-form-type': 'other',
      oninput: () => { L.q = search.value; render(); }, onkeydown: searchKey });
    const sortOpt = (key, label) => h('label', { class: 'seg-opt', id: 'pool-sort-' + key + '-opt' },
      h('input', { class: 'sr', type: 'radio', name: 'pool-sort', id: 'pool-sort-' + key, value: key, onchange: () => { L.sort = key; render(); } }), label);
    const grid = h('div', { class: 'pool-grid', id: 'pool-grid', role: 'group', 'aria-describedby': 'pool-help', onkeydown: gridKey });
    R = {
      turn: h('div', { class: 'turn', role: 'region', 'aria-label': t('Draft status') }),
      board: h('div', { class: 'dboard', 'aria-label': t('Teams') }),
      count: h('span', { class: 'pool-count' }), search, sorts: { ehb: sortOpt('ehb', t('EHB')), name: sortOpt('name', t('Name')) },
      empty: h('div', { class: 'pool-empty' }), grid, help: h('p', { class: 'sr', id: 'pool-help' }), note: h('p', { class: 'td-manual-note' }), teams: new Map()
    };
    R.pool = h('section', { class: 'pool', 'aria-labelledby': 'pool-title' },
      h('div', { class: 'pool-head' }, h('h2', { class: 'pool-title', id: 'pool-title', text: t('Available players') }), R.count, h('span', { class: 'spacer' }),
        h('div', { class: 'search td-search' }, icon('search'), search), h('div', { class: 'seg', role: 'radiogroup', 'aria-label': t('Sort players') }, R.sorts.ehb, R.sorts.name)),
      R.empty, grid, R.help);
    R.wrap = h('div', { class: 'td-live' }, R.turn, R.board, R.pool);
    return R;
  }
  function renderRunning() {
    const r = runningNodes();
    renderTurn(r); renderBoard(r); renderPool(r);
    const manualTeams = S.teams.filter(x => !x.included);
    r.note.textContent = manualTeams.length ? t('Not in the draft: {0}, assembled by hand.', manualTeams.map(x => `${x.name} (${num(x.members.length)})`).join(', ')) : '';
    place(r.wrap, [r.turn, r.board, r.pool].concat(manualTeams.length ? [r.note] : []));
    return r.wrap;
  }
  function renderTurn(r) {
    const me = inControl(), last = S.latestPick, n = S.latestPick ? S.latestPick.number : 0, b = blocked(), pend = L.pending;
    let pick, sub, now = null, msg = '', primary = null;
    if (drawing()) {
      pick = t('Drawing order'); sub = plural(drafted().length, '{0} team', '{0} teams'); msg = L.settling ? t('Moving the teams into the drawn order…') : t('Shuffling the teams…');
      if (me && !S.everPicked) primary = { label: t('Drawing…'), busy: true, disabled: true };
    } else if (!S.orderReady) {
      pick = t('Order not drawn'); sub = plural(drafted().length, '{0} team', '{0} teams') + ' · ' + plural(available().length, '{0} player', '{0} players');
      msg = me ? t('Draw the team order when captains are ready. You can redraw it until the first pick.') : t('Waiting for the admin with control to draw the team order.');
      if (me) primary = { label: t('Draw order'), disabled: b, run: draw };
    } else if (complete()) {
      pick = plural(n, '{0} pick', '{0} picks'); sub = t('Draft complete'); msg = t('Every player is on a team. Check the rosters, then finalize to publish them.');
      if (me) primary = { label: t('Finalize…'), disabled: b, run: () => openCx('finalize') };
    } else if (!S.turn) {
      pick = t('AdminDesign.Pick {0}', num(n + 1)); sub = t('Can’t continue');
      msg = S.blockers.length ? S.blockers[0] + ' ' + t('Undo picks back to zero, cancel the draft and fix the team in setup.') : t('No team can take the next pick.');
    } else {
      pick = S.turn.total ? t('Pick {0} of {1}', num(S.turn.pickNumber), num(S.turn.total)) : t('AdminDesign.Pick {0}', num(S.turn.pickNumber)); sub = t('Round {0}', num(S.turn.round));
      now = { team: S.turn.teamName, next: S.turn.next ? (S.turn.next.teamId === S.turn.teamId ? t('{0} again', S.turn.next.teamName) : S.turn.next.teamName) : '' };
    }
    const undoing = pend?.kind === 'undo';
    const ctl = S.control.who === 'me' ? { cls: '', text: t('You have control'), action: t('Release'), run: releaseControl }
      : S.control.who === 'other' ? { cls: 'is-other', text: t('Another admin has control'), action: t('Take over…'), run: () => openCx('takeover') }
        : { cls: 'is-none', text: t('No one has control'), action: t('Take control'), run: () => takeControl() };
    r.turn.className = 'turn is-swap-' + L.swap;
    r.turn.replaceChildren(...[
      h('div', { class: 'turn-pick' }, h('b', { text: pick }), h('span', { text: sub })),
      ...(now ? [h('div', { class: 'turn-now', 'aria-live': 'polite' }, h('span', { class: 'dot', 'aria-hidden': 'true' }), h('span', { class: 'sr', text: t('Picking now:') + ' ' }), h('span', { class: 'turn-team', text: now.team })),
        h('div', { class: 'turn-next grow' }, now.next ? [t('then') + ' ', h('b', { text: now.next })] : null)] : []),
      msg ? h('div', { class: 'turn-msg grow', 'aria-live': 'polite', text: msg }) : null,
      h('div', { class: 'turn-acts' },
        S.orderReady ? h('button', { class: `btn btn-sm ${undoing ? 'is-busy' : ''}`, type: 'button', id: 'undo-btn', disabled: !last || !me || b, title: last ? t('Undo pick {0}: {1} to {2}', num(last.number), last.name, team(last.teamId)?.name || '') : t('No picks to undo'), onclick: undo },
          undoing ? h('span', { class: 'spin' }) : icon('undo'), undoing ? t('Undoing…') : last ? t('Undo #{0}', num(last.number)) : t('Undo')) : null,
        primary ? h('button', { class: `btn btn-sm btn-primary ${primary.busy ? 'is-busy' : ''}`, type: 'button', id: 'turn-primary', disabled: primary.disabled, onclick: primary.run }, primary.busy ? h('span', { class: 'spin' }) : null, primary.label) : null,
        h('span', { class: `ctl ${ctl.cls}`, role: 'status' }, h('span', { class: 'dot', 'aria-hidden': 'true' }), ctl.text, h('button', { class: 'text-btn', type: 'button', id: 'ctl-btn', disabled: b, text: ctl.action, onclick: ctl.run })),
        h('button', { class: 'icon-btn', type: 'button', id: 'draft-more', 'aria-label': t('More draft actions'), 'aria-haspopup': 'menu', 'aria-expanded': 'false', 'data-menu-target': 'draft-menu', 'data-menu-align': 'end', dataset: { draftMenu: 'more' } }, icon('more')))].filter(Boolean));
  }
  function renderBoard(r) {
    const order = drafted().slice().sort((a, b) => (a.position ?? 1e9) - (b.position ?? 1e9));
    const pend = L.pending?.kind === 'pick' ? L.pending : null, shuffling = L.pending?.kind === 'draw';
    const oldIds = [...r.board.children].map(e => e.dataset.teamId), newIds = order.map(x => x.id);
    const moved = r.board.isConnected && oldIds.length === newIds.length && oldIds.join() !== newIds.join() && !ui.reducedMotion?.();
    const before = moved ? new Map([...r.board.children].map(e => [e.dataset.teamId, e.getBoundingClientRect()])) : null;
    const sections = order.map((x, i) => {
      let el = r.teams.get(x.id);
      if (!el) { el = h('section', { class: 'dteam', id: 'bt-' + x.id, 'aria-labelledby': 'bt-' + x.id + '-name', dataset: { teamId: x.id } }); r.teams.set(x.id, el); }
      const pre = x.members.filter(m => m.tag !== 'pick').sort((a, b) => rank[a.role] - rank[b.role]), picks = x.members.filter(m => m.tag === 'pick').sort((a, b) => a.pick - b.pick);
      const rows = pre.concat(picks).map(m => h('li', { class: `dmem ${m.participantId === L.fresh ? 'is-new' : ''}` },
        h('span', { class: 'dmem-tag', title: m.tag === 'pick' ? t('AdminDesign.Pick {0}', num(m.pick)) : t('Preassigned before the draft'), text: m.tag === 'pick' ? '#' + m.pick : t('Pre') }),
        m.role !== 'P' ? h('span', { class: `role-badge ${m.role === 'CC' ? 'is-co' : ''}`, title: ROLE[m.role]() }, h('span', { 'aria-hidden': 'true', text: m.role === 'C' ? 'C' : 'CC' }), h('span', { class: 'sr', text: ROLE[m.role]() })) : null,
        h('span', { class: 'dmem-name', text: m.name }), h('span', { class: 'dmem-ehb', text: num(m.ehb) })));
      const pending = pend && pend.teamId === x.id ? S.participants.find(p => p.id === pend.pid) : null;
      if (pending) rows.push(h('li', { class: 'dmem is-pending' }, h('span', { class: 'dmem-tag', title: t('Pick {0}, saving', num(pend.pickNo)), text: '#' + pend.pickNo }), h('span', { class: 'dmem-name', text: pending.name }), h('span', { class: 'spin', 'aria-hidden': 'true' }), h('span', { class: 'dmem-ehb', text: num(pending.ehb) })));
      const ehb = x.members.reduce((a, m) => a + Number(m.ehb || 0), 0);
      el.className = `dteam ${S.turn?.teamId === x.id && !drawing() ? 'is-turn' : ''}`;
      el.style.setProperty('--i', String(i));
      el.replaceChildren(h('div', { class: 'dteam-head' }, h('span', { class: 'dteam-ord', title: t('Draft order'), text: S.orderReady && !shuffling && x.position != null ? num(x.position) : '–' }),
        h('h2', { class: 'dteam-name', id: 'bt-' + x.id + '-name', text: x.name }), h('div', { class: 'dteam-meta' }, h('b', { text: `${num(x.members.length + (pending ? 1 : 0))} / ${num(x.finalSize || x.members.length)}` }), h('span', { text: t('{0} EHB', num(ehb)) }))),
        h('ol', { class: 'dteam-list', 'aria-label': t('{0} roster', x.name) }, rows));
      return el;
    });
    for (const [id, el] of r.teams) if (!newIds.includes(id)) { el.remove(); r.teams.delete(id); }
    place(r.board, sections);
    r.board.className = 'dboard' + (shuffling ? ' is-shuffling' : L.settling ? ' is-reordering' : '');
    r.board.style.setProperty('--team-count', String(Math.max(order.length, 1)));
    r.board.setAttribute('aria-busy', String(drawing()));
    if (before) void reorder(r, before);
  }
  // The columns travel to the confirmed order (FLIP, --dk-dur-reorder); picks wait until settled.
  async function reorder(r, before) {
    L.settling = true; render();
    const duration = tokenMs('--dk-dur-reorder'), stagger = tokenMs('--dk-dur-reorder-stagger');
    const runs = [...r.board.children].map((el, i) => {
      const from = before.get(el.dataset.teamId), to = el.getBoundingClientRect(); if (!from) return null;
      const dx = from.left - to.left, dy = from.top - to.top; if (!dx && !dy) return null;
      return el.animate([{ transform: `translate(${dx}px, ${dy}px)` }, { transform: 'none' }], { duration, delay: i * stagger, easing: 'cubic-bezier(.2,.7,.2,1)', fill: 'backwards' }).finished.catch(() => {});
    });
    await Promise.all(runs);
    if (signal.aborted) return;
    L.settling = false; render();
  }
  function renderPool(r) {
    const list = poolList(), all = available().length, me = inControl(), can = !!S.turn && me && !blocked();
    const pend = L.pending?.kind === 'pick' ? L.pending : null;
    let reason = '';
    if (drawing()) reason = t('Picking opens once the drawn order has settled.');
    else if (!S.orderReady) reason = t('Draw the team order to start picking.');
    else if (!me) reason = S.control.who === 'other' ? t('Another admin is picking. Take over to pick from here.') : t('Take control to pick.');
    else if (L.unsure) reason = t('Check the last change before picking again.');
    const focusId = list.some(p => p.id === L.poolFocus) ? L.poolFocus : list[0]?.id;
    const teamName = S.turn ? S.turn.teamName : '';
    r.pool.className = 'pool' + (can ? '' : ' is-inert');
    r.count.textContent = all === list.length ? t('{0} left', num(all)) : t('{0} of {1}', num(list.length), num(all));
    if (r.search.value !== L.q) r.search.value = L.q;
    for (const [key, opt] of Object.entries(r.sorts)) { const on = L.sort === key; opt.classList.toggle('is-on', on); opt.querySelector('input').checked = on; }
    r.empty.hidden = !!list.length; r.empty.textContent = all ? t('No available player matches “{0}”.', L.q.trim()) : t('Everyone has been drafted.');
    r.grid.setAttribute('aria-label', can ? t('Available players. Choosing one drafts them to {0} straight away.', teamName) : t('Available players.') + ' ' + (reason || t('Picking is paused.')));
    r.help.textContent = (reason ? reason + ' ' : '') + t('Arrow keys move between players; Enter drafts the focused player. Press / to search; Enter in the search drafts a single match.');
    r.grid.replaceChildren(...list.map(p => h('button', { class: `pchip ${pend && pend.pid === p.id ? 'is-pending' : ''}`, id: 'pc-' + p.id, type: 'button', tabindex: p.id === focusId ? '0' : '-1', 'aria-disabled': can ? 'false' : 'true',
      'aria-label': t('{0}, {1} EHB', p.name, num(p.ehb)) + (can ? '. ' + t('Draft to {0}', teamName) : ''), onclick: () => { if (can) pick(p.id); } },
      h('span', { class: 'pchip-name', text: p.name }), pend && pend.pid === p.id ? h('span', { class: 'spin', 'aria-hidden': 'true' }) : null, h('span', { class: 'pchip-ehb', text: num(p.ehb) }))));
  }
  function gridKey(e) {
    const keys = ['ArrowRight', 'ArrowLeft', 'ArrowDown', 'ArrowUp', 'Home', 'End'];
    if (!keys.includes(e.key)) return;
    const grid = R.grid, chips = [...grid.querySelectorAll('.pchip')], i = chips.indexOf(document.activeElement); if (i < 0) return;
    const cols = Math.max(1, getComputedStyle(grid).gridTemplateColumns.split(' ').filter(Boolean).length);
    let j = i;
    if (e.key === 'ArrowRight') j = i + 1; else if (e.key === 'ArrowLeft') j = i - 1; else if (e.key === 'ArrowDown') j = i + cols; else if (e.key === 'ArrowUp') j = i - cols;
    else if (e.key === 'Home') j = 0; else j = chips.length - 1;
    if (j < 0 || j >= chips.length) { if (e.key === 'ArrowUp' && j < 0) { e.preventDefault(); R.search.focus(); } return; }
    e.preventDefault();
    for (const chip of chips) chip.tabIndex = -1;
    chips[j].tabIndex = 0; L.poolFocus = chips[j].id.slice(3);
    chips[j].focus({ preventScroll: true }); chips[j].scrollIntoView({ block: 'nearest' });
  }
  function searchKey(e) {
    if (e.key === 'Escape' && L.q) { e.preventDefault(); e.stopPropagation(); L.q = ''; render(); return; }
    if (e.key === 'Enter') { const list = poolList(); if (list.length === 1) { e.preventDefault(); const id = list[0].id; L.q = ''; pick(id); } return; }
    if (e.key === 'ArrowDown') { const first = R.grid.querySelector('.pchip[tabindex="0"]') || R.grid.querySelector('.pchip'); if (first) { e.preventDefault(); first.focus(); } }
  }
  document.addEventListener('keydown', e => {
    if (e.key !== '/' || e.ctrlKey || e.metaKey || e.altKey || stage() !== 'running' || !R?.search.isConnected) return;
    const target = e.target; if (target && (target.tagName === 'INPUT' || target.tagName === 'TEXTAREA' || target.tagName === 'SELECT' || target.isContentEditable)) return;
    if (document.querySelector('[aria-modal="true"]') || !menu.hidden) return;
    e.preventDefault(); R.search.focus();
  }, { signal });

  /* ---------------- draft actions ---------------- */
  function pick(pid) {
    const turn = S.turn;
    if (blocked() || !turn || !inControl() || !available().some(p => p.id === pid)) return;
    const p = S.participants.find(x => x.id === pid), name = p.name, teamName = turn.teamName, pickNo = turn.pickNumber;
    // Roving focus moves to the neighbour when the picked chip leaves the pool.
    const list = poolList(), i = list.findIndex(x => x.id === pid), next = list[i + 1] || list[i - 1];
    const inGrid = document.activeElement?.classList?.contains('pchip');
    L.poolFocus = next ? next.id : null;
    void run({ handler: 'Pick', values: { participantId: pid }, kind: 'pick', quick: true, what: t('draft {0}', name), pending: { pid, teamId: turn.teamId, pickNo },
      verify: rb => rb.picks.some(x => x.participantId === pid && x.undoneAt == null),
      okText: t('{0} is on {1}.', name, teamName), notText: t('{0} wasn’t drafted.', name),
      onOk: () => { L.fresh = pid; L.swap = L.swap === 'a' ? 'b' : 'a'; L.live = t('Pick {0}: {1} to {2}.', num(pickNo), name, teamName); render(); if (inGrid && next) focusSoon('pc-' + next.id, { preventScroll: true }); else if (!inGrid) focusSoon('pool-search'); } });
  }
  function undo() {
    const last = S.latestPick;
    if (blocked() || !last || !inControl()) return;
    const teamName = team(last.teamId)?.name || '';
    // AU14: the pick shown as latest, by its id; the server refuses when it is no longer the latest.
    void run({ handler: 'Undo', values: { pickId: last.pickId }, kind: 'undo', quick: true, what: t('undo pick {0}', num(last.number)),
      verify: rb => rb.picks.some(x => x.pickId === last.pickId && x.undoneAt != null),
      okText: t('Pick {0} is undone. {1} is back in the pool.', num(last.number), last.name), notText: t('Pick {0} wasn’t undone.', num(last.number)),
      onOk: () => { L.fresh = null; L.swap = L.swap === 'a' ? 'b' : 'a'; L.live = t('Pick {0} undone: {1} left {2}.', num(last.number), last.name, teamName); render(); focusSoon('undo-btn'); } });
  }
  function draw() {
    if (blocked() || !inControl() || S.everPicked) return;
    const redraw = S.orderReady, previous = drafted().map(x => x.id + ':' + x.position).join();
    void run({ handler: 'Scramble', values: {}, kind: 'draw', what: redraw ? t('redraw the order') : t('draw the order'),
      verify: rb => { const inc = rb.teams.filter(x => x.active && x.includedInDraft); return inc.length > 1 && inc.every(x => x.draftPosition != null) && (!redraw || inc.map(x => x.teamId + ':' + x.draftPosition).join() !== previous); },
      okText: t('A team order is drawn.'), notText: t('The order wasn’t drawn.'),
      onOk: () => {
        L.swap = L.swap === 'a' ? 'b' : 'a';
        const first = S.turn ? S.turn.teamName : drafted().find(x => x.position === 1)?.name || '';
        L.live = t('Order drawn: {0}.', drafted().slice().sort((a, b) => a.position - b.position).map(x => x.name).join(', '));
        ui.toast(redraw ? t('Order redrawn. {0} picks first.', first) : t('Order drawn. {0} picks first.', first)); render(); focusSoon('pool-search');
      } });
  }
  function takeControl(layerHandle) {
    if (blocked() && !layerHandle) return;
    const force = !!layerHandle;
    void run({ handler: force ? 'TakeControl' : 'AcquireControl', values: force ? { confirmed: 'true' } : {}, kind: force ? 'takeover' : 'control', quick: !force, layer: layerHandle, what: force ? t('take over the draft') : t('take control of the draft'),
      verify: rb => (rb.controllerId || '').toLowerCase() === me, okText: t('You have control of the draft.'), notText: t('You don’t have control.'),
      onOk: () => { ui.toast(t('You have control of the draft.')); focusSoon('pool-search'); } });
  }
  function releaseControl() {
    if (blocked() || !inControl()) return;
    void run({ handler: 'ReleaseControl', values: {}, kind: 'release', quick: true, what: t('release draft control'),
      verify: rb => (rb.controllerId || '').toLowerCase() !== me, okText: t('Control is released.'), notText: t('You still have control.'),
      onOk: () => { ui.toast(t('Control released. Any admin can take it.')); focusSoon('ctl-btn'); } });
  }
  function moreItemsRunning() {
    const n = S.latestPick ? S.latestPick.number : 0, done = complete(), mine = inControl(), left = available().length;
    return [
      { label: S.orderReady ? t('Redraw order') : t('Draw order'), disabled: !mine || S.everPicked, hint: S.everPicked ? t('Fixed after the first pick') : !mine ? t('Needs control') : '', run: draw },
      // AddMember to a drafted team: only before the first pick ever recorded.
      { label: t('Preassign a player…'), disabled: S.everPicked, hint: S.everPicked ? t('Closed after the first pick') : '', run: () => openPs('member', S.turn ? S.turn.teamId : drafted()[0]?.id, { pickTeam: true }) },
      'sep',
      { label: t('Finalize draft…'), disabled: !done || !mine, hint: !done ? plural(left, '{0} player left', '{0} players left') : !mine ? t('Needs control') : '', run: () => openCx('finalize') },
      { label: t('Cancel draft…'), danger: true, hint: n ? plural(n, '{0} active pick', '{0} active picks') : '', run: () => openCx('cancel') }
    ];
  }
  function moreConfirmRunning(kind) {
    if (kind === 'takeover') return { title: S.control.name ? t('Take over from {0}?', S.control.name) : t('Take over the draft?'), body: S.control.name ? t('{0} has control of this draft. Taking over stops their picks from going through.', S.control.name) : t('Another admin has control of this draft. Taking over stops their picks from going through.'),
      points: [t('Only take over if they’ve stopped or asked you to.'), t('Their control lapses on its own {0} minutes after their page stops renewing it.', num(LEASE_MIN))], confirm: t('Take over'), busyLabel: t('Working…'), custom: layerHandle => takeControl(layerHandle) };
    if (kind !== 'cancel') return null;
    const n = S.latestPick ? S.latestPick.number : 0;
    if (n) return { title: t('Undo picks before cancelling'), body: plural(n, 'The draft still has {0} active pick. Cancelling never removes picks for you.', 'The draft still has {0} active picks. Cancelling never removes picks for you.'),
      block: t('Use Undo to remove picks one at a time, latest first. When none are left, you can cancel and return to setup.'), cancel: t('Close') };
    return { title: t('Cancel the draft?'), body: t('The draft returns to setup. Teams, captains and preassigned members stay as they are.'),
      points: [S.everPicked ? t('The drawn order is kept, because a first pick was made.') : t('The drawn order is cleared; you’ll draw again after restarting.'), t('Control is released.')],
      confirm: t('Cancel draft'), busyLabel: t('Working…'), cls: 'btn-danger', cancel: t('Keep drafting'), handler: 'Cancel', values: {}, what: t('cancel the draft'),
      verify: rb => rb.state === 1, okText: t('The draft was cancelled.'), notText: t('The draft is still running.'),
      onOk: () => { ui.toast(t('Draft cancelled. Teams are back in setup.')); focusSoon('page-h1'); } };
  }

  /* ---------------- sidebar: collapsed while the draft runs ---------------- */
  // The admin's own toggles while running are kept until the draft ends; the earlier
  // state comes back when the draft ends or the page is left.
  const narrow = matchMedia('(max-width: 860px)');
  let sideBase = null;
  function side(runningNow) {
    const bar = document.querySelector('[data-shell-sidebar]'), toggle = bar?.querySelector('.collapse-btn');
    if (!bar || !toggle) return;
    if (runningNow && sideBase === null) { sideBase = bar.classList.contains('is-collapsed'); if (!sideBase && !narrow.matches) toggle.click(); }
    else if (!runningNow && sideBase !== null) restoreSide();
  }
  function restoreSide() {
    const bar = document.querySelector('[data-shell-sidebar]'), toggle = bar?.querySelector('.collapse-btn');
    if (bar && toggle && sideBase !== null && !narrow.matches && bar.classList.contains('is-collapsed') !== sideBase) toggle.click();
    sideBase = null;
  }

  /* ---------------- live updates (AdminCollaborationHub) ---------------- */
  // Messages are invalidations only: the page re-reads its state. Control is renewed every
  // 120 s while this admin holds it; a lost connection while in control is said plainly.
  function connectHub() {
    if (S.stage === 'terminal') return;
    const start = () => {
      if (signal.aborted || !window.signalR) return;
      const connection = new window.signalR.HubConnectionBuilder().withUrl('/hubs/admin-collaboration').withAutomaticReconnect().build();
      let timer = 0;
      connection.on('draftChanged', () => { clearTimeout(timer); timer = setTimeout(() => { if (!signal.aborted && !busy() && !L.unsure && !L.settling) void refresh(); }, 150); });
      const subscribe = async () => { await connection.invoke('WatchDraft', eventId); if (inControl()) await connection.invoke('RenewDraftControl', eventId); };
      const lost = () => { if (signal.aborted) return; L.offline = true; if (S.stage === 'running' && inControl()) { L.notice = offlineNotice(); render(); } };
      connection.onreconnecting(lost); connection.onclose(lost);
      connection.onreconnected(() => { L.offline = false; if (L.notice?.kind === 'offline') L.notice = null; subscribe().catch(() => {}); void refresh(); });
      connection.start().then(subscribe).catch(() => {});
      const renew = setInterval(() => { if (inControl() && connection.state === 'Connected') connection.invoke('RenewDraftControl', eventId).catch(() => {}); }, 120000);
      extensions.push(() => { clearInterval(renew); clearTimeout(timer); connection.stop().catch(() => {}); });
    };
    if (window.signalR) { start(); return; }
    let script = document.querySelector('script[data-signalr]');
    if (!script) {
      script = document.createElement('script');
      script.src = 'https://cdnjs.cloudflare.com/ajax/libs/microsoft-signalr/8.0.7/signalr.min.js';
      script.crossOrigin = 'anonymous'; script.referrerPolicy = 'no-referrer'; script.dataset.signalr = '';
      document.head.append(script);
    }
    script.addEventListener('load', start, { once: true, signal });
  }

  /* ---------------- menus (one shared menu, filled per opener) ---------------- */
  function menuItems(spec) {
    const st = stage();
    if (spec.kind === 'team') {
      const x = team(spec.team); if (!x) return [];
      const items = [];
      if (st === 'setup' || canCorrect()) items.push({ label: st === 'setup' ? t('Edit team…') : t('Rename team…'), run: () => openTf(x.id) });
      if (S.canChangeRoles || st === 'setup') items.push({ label: t('Add captain…'), run: () => openPs('captain', x.id) });
      if (st === 'setup') items.push('sep', { label: t('Remove team…'), danger: true, run: () => openCx('removeTeam', { teamId: x.id, teamName: x.name, size: x.members.length }) });
      return items;
    }
    if (spec.kind === 'member') {
      const x = team(spec.team), m = x?.members.find(y => y.id === spec.member); if (!m) return [];
      const items = [{ header: m.name, hint: ROLE[m.role]() + ' · ' + x.name }];
      if (S.canChangeRoles) for (const r of ['C', 'CC', 'P'].filter(r => r !== m.role)) items.push({ label: r === 'P' ? t('Make participant') : r === 'C' ? t('Make captain') : t('Make co-captain'), run: () => changeRole(x, m, r) });
      if (st === 'setup' && !x.included) for (const o of S.teams.filter(y => y.id !== x.id && !y.included)) items.push({ label: t('Move to {0}', o.name), run: () => moveMember(x, m, o) });
      // S5: "Remove from team…" with its confirmation is the only finalized removal.
      if (st === 'setup' || canCorrect()) items.push('sep', { label: canCorrect() ? t('Remove from team…') : t('Remove from team'), danger: true, run: () => removeMember(x, m) });
      return items;
    }
    return spec.items ? spec.items() : [];
  }
  function fillMenu(opener) {
    menuSpec = { kind: opener.dataset.draftMenu, team: opener.dataset.team, member: opener.dataset.member, items: opener.dataset.draftMenu === 'more' ? moreItems : null };
    const items = menuItems(menuSpec);
    menu.setAttribute('aria-label', menuSpec.kind === 'team' ? t('Team actions') : menuSpec.kind === 'member' ? t('Member actions') : t('Draft actions'));
    menu.replaceChildren(...items.map((item, index) => {
      if (item === 'sep') return h('div', { class: 'menu-sep', role: 'separator' });
      if (item.header) return h('div', { class: 'menu-head', role: 'presentation' }, h('b', { text: item.header }), item.hint);
      return h('button', { class: `menu-item ${item.danger ? 'is-danger' : ''}`, type: 'button', role: 'menuitem', disabled: item.disabled, dataset: { draftItem: String(index) } },
        h('span', { class: 'grow', text: item.label }), h('span', { class: 'menu-hint', text: item.hint || '' }));
    }));
    menuSpec.list = items;
  }
  const moreItems = moreItemsRunning;
  root.addEventListener('click', event => {
    const opener = event.target.closest('[data-draft-menu]');
    if (opener) { if (blocked()) { event.stopPropagation(); return; } fillMenu(opener); return; } // the shell opens it next
    const item = event.target.closest('[data-draft-item]');
    if (item && menu.contains(item) && !item.disabled) {
      const chosen = menuSpec?.list?.[Number(item.dataset.draftItem)];
      ui.closeMenu(false);
      chosen?.run?.();
    }
  }, { signal });

  /* ---------------- layers ---------------- */
  // A transient shared layer (modal or drawer) with the page's busy/error wiring.
  function layer({ kind = 'modal', title, content, confirmation = false, dirty, cls = '', bannerHost }) {
    let isBusy = false, isOpen = true;
    content.classList.add('td-layer'); content.dataset.pageFamily = 'draft'; // display:contents, carries the family CSS into the shared host
    const handle = ui.openLayer({ kind, title, content, confirmation, dirty: dirty || (() => false), pending: () => isBusy, onClose: () => { isOpen = false; } });
    if (cls) handle.element.classList.add(...cls.split(' ').filter(Boolean));
    return {
      element: handle.element, markClean: handle.markClean, open: () => isOpen,
      close: () => isOpen ? handle.close(true) : Promise.resolve(),
      setBusy(value) {
        isBusy = value;
        handle.element.setAttribute('aria-busy', String(value));
        for (const control of handle.element.querySelectorAll('button,input,select,textarea')) { if (value) { control.dataset.wasDisabled = String(control.disabled); control.disabled = true; } else if (control.dataset.wasDisabled) { control.disabled = control.dataset.wasDisabled === 'true'; delete control.dataset.wasDisabled; } }
        const save = handle.element.querySelector('[data-layer-save]');
        if (save) { save.classList.toggle('is-busy', value); save.querySelector('.spin')?.remove(); if (value) save.prepend(h('span', { class: 'spin' })); const text = save.querySelector('[data-label]'); if (text) text.textContent = value ? save.dataset.busyLabel : save.dataset.label; }
      },
      error(titleText, text) {
        const host = handle.element.querySelector(bannerHost || '[data-layer-banner]'); if (!host) return;
        host.replaceChildren(h('div', { class: 'banner is-error', role: 'alert', tabindex: '-1' }, icon('error'), h('span', { class: 'grow' }, h('b', { text: titleText }), ' ' + text)));
        host.hidden = false; host.firstElementChild.focus();
      }
    };
  }
  const saveButton = (label, busyLabel, cls = 'btn-primary', id) => h('button', { class: `btn ${cls}`, type: 'button', id, 'data-layer-save': true, dataset: { label, busyLabel } }, h('span', { 'data-label': true, text: label }));

  /* ---------------- team form (Add / Edit / Rename) ---------------- */
  function openTf(teamId) {
    if (blocked()) return;
    const x = teamId ? team(teamId) : null, setup = stage() === 'setup';
    const base = { name: x ? x.name : '', incl: x ? x.included : true };
    const name = h('input', { class: 'input', id: 'tf-name', autocomplete: 'off', 'aria-required': 'true', 'aria-describedby': 'tf-name-hint', placeholder: t('e.g. Team Torva'), maxlength: '60' });
    name.value = base.name;
    const count = h('span', { class: 'field-count', 'aria-live': 'polite', hidden: true });
    const err = h('div', { class: 'field-err', id: 'tf-name-err', hidden: true });
    const incl = h('input', { type: 'checkbox', id: 'tf-incl', 'aria-describedby': 'tf-incl-hint' }); incl.checked = base.incl;
    const inclHint = h('span', { class: 'choice-sub', id: 'tf-incl-hint' });
    const inclRow = h('label', { class: 'check-row' }, incl, h('span', {}, h('span', { class: 'choice-title', text: t('Takes part in the website draft') }), inclHint));
    const paint = () => {
      const len = name.value.trim().length; count.hidden = len <= 22; count.textContent = `${len} / 30`; count.classList.toggle('is-over', len > 30);
      inclHint.textContent = incl.checked ? t('Filled by the snake draft. Needs a captain before the draft starts.') : t('Not drafted: you assemble this roster by hand, and its members aren’t in the draft pool.');
      inclRow.classList.toggle('is-on', incl.checked);
    };
    const error = () => {
      const n = name.value.trim(); if (!n) return t('Enter a team name.');
      if ([...n].length > 30) return t('Use 30 characters or fewer.');
      if (S.teams.some(y => y.id !== teamId && y.name.toLowerCase() === n.toLowerCase())) return t('Another team already has this name.');
      return '';
    };
    const showErr = () => { const e = error(); err.hidden = !e; err.replaceChildren(icon('error'), e); name.classList.toggle('is-invalid', !!e); name.setAttribute('aria-invalid', e ? 'true' : 'false'); name.setAttribute('aria-describedby', (e ? 'tf-name-err ' : '') + 'tf-name-hint'); return e; };
    let shown = false;
    name.addEventListener('input', () => { paint(); if (shown) showErr(); }, { signal });
    incl.addEventListener('change', paint, { signal });
    const save = saveButton(teamId ? t('Save') : t('Add team'), t('Saving…'), 'btn-primary', 'tf-save'), before = republishBase();
    const titleText = teamId ? (setup ? t('Edit team') : t('Rename team')) : t('Add team');
    const content = h('div', {}, h('div', { class: 'mf-head' }, h('h2', { class: 'm-title', id: 'tf-title', 'data-confirm-title': true, text: titleText })),
      h('div', { class: 'mf-body' }, h('div', { 'data-layer-banner': true, hidden: true }),
        h('div', { class: 'field' }, h('div', { class: 'lbl-row' }, h('label', { class: 'lbl', for: 'tf-name', text: t('Team name') }), count), name, err,
          h('div', { class: 'field-hint', id: 'tf-name-hint', text: t('Up to 30 characters. Players see it on the team pages.') })),
        setup ? h('div', { class: 'field' }, inclRow) : null),
      h('div', { class: 'mf-foot' }, h('button', { class: 'btn', type: 'button', id: 'tf-cancel', text: t('Cancel'), onclick: () => void ui.closeLayer() }), save));
    paint();
    const L_tf = layer({ title: titleText, content, cls: 'modal-form', dirty: () => name.value.trim() !== base.name || incl.checked !== base.incl });
    const submit = () => {
      if (teamId && name.value.trim() === base.name && incl.checked === base.incl) { void L_tf.close(); return; }
      shown = true; if (showErr()) { name.focus(); return; }
      const n = name.value.trim(), included = setup ? incl.checked : x?.included ?? true, id = teamId;
      const values = id ? { teamId: id, name: n, version: String(x.version) } : { name: n, includedInDraft: String(included) };
      if (id && setup) values.includedInDraft = String(included);
      void run({ handler: id ? 'UpdateTeam' : 'AddTeam', values, what: id ? t('save {0}', n) : t('add {0}', n), kind: 'team', layer: L_tf, draft: { [t('Team name')]: n },
        verify: rb => id ? rb.teams.some(y => y.teamId === id && y.active && y.name === n && y.includedInDraft === included) : rb.teams.some(y => y.active && y.name === n),
        // New-team creation identity stays uncertain (DP:1151): never "created by you".
        okText: id ? t('{0} is saved.', n) : t('A team named {0} now exists. It isn’t known whether this request created it.', n), notText: id ? t('{0} wasn’t saved.', n) : t('No team named {0} exists.', n),
        onOk: () => { void announce(id ? t('{0} saved.', n) : t('{0} added.', n), before); const created = S.teams.find(y => y.name === n); focusSoon(created ? 't-' + created.id + '-menu' : 'add-team'); } });
    };
    save.addEventListener('click', submit, { signal });
    name.addEventListener('keydown', event => { if (event.key === 'Enter') { event.preventDefault(); submit(); } }, { signal });
    requestAnimationFrame(() => name.focus());
  }

  /* ---------------- participant selector (drawer) ---------------- */
  function openPs(mode, teamId, opts = {}) {
    if (blocked()) return;
    const p = { mode, teamId, pickTeam: !!opts.pickTeam, q: '', src: 'part', pick: null, role: mode === 'captain' ? 'C' : 'P' };
    const corr = mode === 'correction';
    const titleEl = h('h2', { class: 'dr-title', id: 'ps-title', 'data-confirm-title': true });
    const eyebrow = h('div', { class: 'eyebrow' });
    const search = h('input', { class: 'input', id: 'ps-search', type: 'search', autocomplete: 'off', 'aria-describedby': 'ps-err', 'data-1p-ignore': true, 'data-lpignore': 'true', 'data-bwignore': true, 'data-form-type': 'other' });
    const err = h('div', { class: 'field-err', id: 'ps-err', hidden: true });
    const results = h('div', { class: 'results', id: 'ps-results' });
    const searchBlock = h('div', {}, h('div', { class: 'search td-full' }, icon('search'), search), err, results);
    const picked = h('div', { class: 'picked', id: 'ps-picked', tabindex: '-1', hidden: true });
    const src = corr && S.accounts.length ? h('div', { class: 'seg td-src', role: 'radiogroup', 'aria-label': t('Who to add') },
      [['part', t('Signed-up players')], ['acc', t('Other website accounts')]].map(([key, label]) => h('label', { class: `seg-opt ${key === 'part' ? 'is-on' : ''}` }, h('input', { class: 'sr', type: 'radio', name: 'ps-src', value: key, checked: key === 'part', onchange: () => { p.src = key; p.pick = null; paint(); search.focus(); } }), label))) : null;
    const teamSelect = p.pickTeam ? h('select', { class: 'select select-field', id: 'ps-team', 'aria-describedby': 'ps-team-hint', onchange: e => { p.teamId = e.target.value; p.pick = null; paint(); } }, drafted().map(x => h('option', { value: x.id, selected: x.id === teamId, text: t('{0} · {1}', x.name, plural(x.members.length, '{0} member', '{0} members')) }))) : null;
    const roleLbl = h('h3', { class: 'sec-title', id: 'ps-role-lbl' });
    const roles = h('div', { class: 'td-roles', role: 'radiogroup', 'aria-labelledby': 'ps-role-lbl', 'aria-describedby': 'ps-role-hint' },
      [['P', t('Participant'), t('A regular member.')], ['C', t('Captain'), t('Leads the team and calls draft picks.')], ['CC', t('Co-captain'), t('Helps the captain; a separate role.')]].map(([key, label, sub]) =>
        h('label', { class: 'choice', dataset: { role: key } }, h('input', { type: 'radio', name: 'ps-role', value: key, checked: key === p.role, onchange: () => { p.role = key; paint(); } }), h('span', {}, h('span', { class: 'choice-title', text: label }), h('span', { class: 'choice-sub', text: sub })))));
    const roleSec = h('section', { class: 'sec', 'aria-labelledby': 'ps-role-lbl' }, h('div', { class: 'sec-head' }, roleLbl), roles, h('div', { class: 'field-hint', id: 'ps-role-hint', text: t('Volunteering to captain doesn’t give anyone a role. Choose one here.') }));
    const note = h('p', { class: 'td-note' });
    const save = saveButton('', t('Saving…'), 'btn-primary', 'ps-save');
    const content = h('div', {},
      h('div', { class: 'dr-head' }, h('div', { class: 'grow' }, eyebrow, titleEl), h('button', { class: 'icon-btn', type: 'button', id: 'ps-close', 'aria-label': t('Close'), onclick: () => void ui.closeLayer() }, icon('close'))),
      h('div', { class: 'dr-body' }, h('div', { class: 'dr-banners', 'data-layer-banner': true, hidden: true }),
        teamSelect ? h('section', { class: 'sec' }, h('div', { class: 'field' }, h('label', { class: 'lbl', for: 'ps-team', text: t('Team') }), h('div', { class: 'select-wrap' }, teamSelect, icon('chevron-down')),
          h('div', { class: 'field-hint', id: 'ps-team-hint', text: t('Preassigning is possible until the first pick. It counts toward the team’s size and can change whose turn comes next.') }))) : null,
        h('section', { class: 'sec', 'aria-labelledby': 'ps-who-lbl' }, h('div', { class: 'sec-head' }, h('h3', { class: 'sec-title', id: 'ps-who-lbl', text: corr ? t('Who to add') : t('Player') }), mode === 'captain' ? h('span', { class: 'sec-aside', text: t('Volunteers first') }) : null),
          src, searchBlock, picked),
        roleSec, h('section', { class: 'sec', 'data-ps-note': true }, note)),
      h('div', { class: 'dr-foot' }, h('div', { class: 'spacer' }), h('button', { class: 'btn', type: 'button', id: 'ps-cancel', text: t('Cancel'), onclick: () => void ui.closeLayer() }), save));
    const results_ = () => {
      const x = team(p.teamId); if (!x) return [];
      const q = p.q.trim().toLowerCase();
      if (p.src === 'acc') return S.accounts.filter(a => !q || a.name.toLowerCase().includes(q) || a.login.toLowerCase().includes(q))
        .map(a => ({ id: 'a:' + a.accountId + ':' + a.characterId, acc: a, name: a.name, sub: t('Not signed up · @{0} · {1} EHB', a.login, a.ehb == null ? '–' : num(a.ehb)), disabled: a.ehb == null, note: a.ehb == null ? t('No saved EHB') : '' }));
      const st = stage();
      const list = S.participants.filter(y => !q || y.name.toLowerCase().includes(q) || (y.account || '').toLowerCase().includes(q)).map(y => {
        const on = memberOf(y.id), here = on && on.id === p.teamId, cur = here ? on.members.find(m => m.participantId === y.id) : null;
        // A new member joins here only in setup (a drafted team: before the first pick) or as a
        // correction; Live/Final review roles pick a current member (no membership changes).
        const canJoin = mode === 'correction' || (st === 'setup' && (!x.included || !S.everPicked));
        const disabled = (!!on && !(here && mode === 'captain')) || (!on && !canJoin) || (cur && cur.role === 'C');
        return { id: 'p:' + y.id, part: y, name: y.name, vol: y.volunteer && mode !== 'correction', disabled, cur,
          note: here ? (cur && cur.role !== 'P' ? t('{0} of this team', ROLE[cur.role]()) : t('On this team')) : on ? t('On {0}', on.name) : !canJoin ? t('Not on this team') : '',
          sub: (y.account ? '@' + y.account + ' · ' : '') + t('Primary account · {0} EHB', num(y.ehb)) };
      });
      list.sort((a, b) => (a.disabled - b.disabled) || (mode === 'captain' ? (b.part.volunteer - a.part.volunteer) : 0) || a.name.localeCompare(b.name, lang));
      return list;
    };
    function paint() {
      const x = team(p.teamId) || { name: '', members: [] }, st = stage();
      eyebrow.textContent = p.pickTeam ? t('Before the first pick') : x.name;
      titleEl.textContent = p.pickTeam ? t('Preassign a player') : mode === 'captain' ? t('Add captain') : corr ? t('Add to the published roster') : t('Add to {0}', x.name);
      for (const option of src?.querySelectorAll('input') || []) { option.checked = option.value === p.src; option.closest('.seg-opt').classList.toggle('is-on', option.checked); }
      searchBlock.hidden = !!p.pick; picked.hidden = !p.pick;
      search.placeholder = p.src === 'acc' ? t('Search website accounts') : t('Search players by account name'); search.setAttribute('aria-label', search.placeholder);
      if (!p.pick) {
        const all = results_(), shown = all.slice(0, 40);
        const label = mode !== 'captain' && all.length && all.every(r => r.disabled) ? t('Everyone here is already on a team') : (all.length > shown.length ? t('Showing {0} of {1}', num(shown.length), num(all.length)) : plural(all.length, '{0} match', '{0} matches')) + (p.src === 'acc' ? ' · ' + t('not signed up for this event') : '');
        results.replaceChildren(h('div', { class: 'res-label', text: label }), ...shown.map((r, i) => h('button', { class: 'res', type: 'button', id: 'ps-r' + i, disabled: r.disabled, onclick: () => choose(r) },
          h('span', { class: 'grow' }, h('span', { class: 'res-name', text: r.name }), h('span', { class: 'res-sub', text: r.sub })), r.vol ? h('span', { class: 'pill', text: t('Captain volunteer') }) : null, r.note ? h('span', { class: 'res-note', text: r.note }) : null)),
          ...(!shown.length ? [h('div', { class: 'res-empty', text: p.q.trim() ? t('No players match “{0}”.', p.q.trim()) : t('No players to choose from.') })] : []));
      } else picked.replaceChildren(h('span', { class: 'grow' }, h('span', { class: 'res-name', text: p.pick.name }), h('span', { class: 'res-sub', text: p.pick.sub })), ...(p.pick.vol ? [h('span', { class: 'pill', text: t('Captain volunteer') })] : []),
        h('button', { class: 'btn btn-sm', type: 'button', id: 'ps-change', text: t('Change'), onclick: () => { p.pick = null; paint(); search.focus(); } }));
      // U6-Q1 (a): the finalized correction Add keeps the role choice (one publication).
      roleSec.hidden = !(mode === 'member' || corr);
      roleLbl.textContent = t('Role on {0}', x.name);
      for (const choice of roles.querySelectorAll('.choice')) { const on = choice.dataset.role === p.role; choice.querySelector('input').checked = on; choice.classList.toggle('is-on', on); }
      const noteText = corr ? (p.src === 'acc' ? t('They join as a confirmed participant with this playing account and its saved EHB, with the role chosen above. Saving republishes the rosters; original picks stay in the draft history.') : t('They join {0} with the role chosen above. Saving republishes the rosters; original picks stay in the draft history. Corrections close when the event first goes Live.', x.name))
        : mode === 'captain' ? (st === 'locked' || st === 'review' ? t('Membership is locked, so choose a current member of {0}. A team can have more than one Captain.', x.name)
          : st === 'final' ? t('Choose a current member of {0}, or add a new member with a role. A team can have more than one Captain.', x.name)
          : (x.included ? t('Captains call the picks during the draft.') : t('Captain of a manual-roster team.')) + ' ' + t('A team can have more than one Captain, and Co-captain is a separate role in the member’s menu.')) : '';
      note.textContent = noteText; note.parentElement.hidden = !noteText;
      const existing = p.pick?.part && x.members.find(m => m.participantId === p.pick.part.id);
      save.dataset.label = mode === 'captain' ? (existing ? t('Make captain') : t('Add as captain')) : corr ? t('Add and republish') : t('Add to team');
      save.querySelector('[data-label]').textContent = save.dataset.label;
    }
    function choose(r) { p.pick = r; err.hidden = true; search.classList.remove('is-invalid'); search.setAttribute('aria-invalid', 'false'); paint(); focusSoon('ps-save'); }
    search.addEventListener('input', () => { p.q = search.value; paint(); }, { signal });
    search.addEventListener('keydown', event => {
      if (event.key === 'ArrowDown') { const first = results.querySelector('.res:not(:disabled)'); if (first) { event.preventDefault(); first.focus(); } }
      if (event.key === 'Enter') { const ok = results_().filter(r => !r.disabled); if (ok.length === 1) { event.preventDefault(); choose(ok[0]); } }
    }, { signal });
    results.addEventListener('keydown', event => {
      const items = [...results.querySelectorAll('.res:not(:disabled)')], i = items.indexOf(document.activeElement);
      if (event.key === 'ArrowDown' && i >= 0 && items[i + 1]) { event.preventDefault(); items[i + 1].focus(); }
      if (event.key === 'ArrowUp' && i >= 0) { event.preventDefault(); (items[i - 1] || search).focus(); }
    }, { signal });
    paint();
    const L_ps = layer({ kind: 'drawer', title: titleEl.textContent, content, dirty: () => !!p.pick });
    // A picker fills itself after opening: its first state is the clean baseline.
    L_ps.markClean();
    save.addEventListener('click', () => {
      if (!p.pick) { err.hidden = false; err.replaceChildren(icon('error'), mode === 'captain' ? t('Choose who will captain {0}.', team(p.teamId)?.name || '') : t('Choose a player to add.')); search.classList.add('is-invalid'); search.setAttribute('aria-invalid', 'true'); search.focus(); return; }
      const x = team(p.teamId), pk = p.pick, role = mode === 'captain' ? 'C' : p.role;
      const existing = pk.part && x.members.find(m => m.participantId === pk.part.id);
      if (existing) { changeRole(x, existing, role, L_ps); return; }
      const values = { teamId: x.id, role: ROLE_ENUM[role], rosterTeamId: x.id, expectedTeamVersion: String(x.version) };
      if (corr) values.confirmed = 'true';
      if (pk.acc) Object.assign(values, { accountId: pk.acc.accountId, playingCharacterId: pk.acc.characterId, playingEhb: String(pk.acc.ehb) });
      else values.participantId = pk.part.id;
      const name = pk.name, pid = pk.part?.id, before = republishBase();
      void run({ handler: 'AddMember', values, what: t('add {0}', name), kind: 'member', layer: L_ps, draft: { [t('Player')]: name, [t('Team')]: x.name },
        verify: rb => rb.memberships.some(m => m.teamId === x.id && m.leftAt == null && m.role === ROLE_NUM[role] && (pid ? m.participantId === pid : true)) && (pid ? true : rb.participants.some(q => q.accountId === pk.acc.accountId)),
        okText: t('{0} is on {1}.', name, x.name), notText: t('{0} isn’t on {1}.', name, x.name),
        onOk: data => { L.fresh = data?.data?.participantId || pid || null; void announce(role !== 'P' ? t('{0} added to {1} as {2}.', name, x.name, ROLE[role]().toLowerCase()) : t('{0} added to {1}.', name, x.name), before); render(); focusSoon('t-' + x.id + '-add'); } });
    }, { signal });
    requestAnimationFrame(() => search.focus());
  }
  /* ---------------- republish and Wise Old Man outcome lines (AU14, RC04/T-12/T-13/T-15) ---------------- */
  // A finalized roster change republishes the rosters and queues a separate Wise Old Man
  // group update. The toast says each part separately from the authoritative readback:
  // the local republish (a newer publication cycle than before the change) and the WOM outcome
  // of an operation created at or after that publication. Unknown or failed is said
  // plainly, without provider text; it never says the group is updated unless it is.
  function republishBase() { return ['final', 'locked'].includes(S.stage) ? { cycle: S.publicationCycle ?? 0 } : null; }
  function womLine(rb) {
    const sync = rb.synchronization, published = rb.rosterPublishedAt ? Date.parse(rb.rosterPublishedAt) : NaN;
    if (!sync || sync.managementStatus === 'NotManaged') return '';
    if (sync.managementStatus === 'ReadOnly') return t('This event’s Wise Old Man competition isn’t managed from here, so its group isn’t changed.');
    const op = sync.lastOperation;
    if (op && Date.parse(op.createdAt) >= published) {
      if (op.phase === 4) return t('Wise Old Man: the group update went through.');
      if ([1, 2, 3, 5].includes(op.phase)) return t('Wise Old Man: the group update is waiting to be sent.');
      if (op.phase === 7) return t('Wise Old Man: the group update failed. Check the Wise Old Man page.');
      return t('Wise Old Man: whether the group was updated isn’t known. Check the Wise Old Man page.');
    }
    if (sync.lastLocalQueueStatus === 'Failed' && sync.lastLocalQueueAt && Date.parse(sync.lastLocalQueueAt) >= published) return t('Wise Old Man: the group update couldn’t be queued. Check the Wise Old Man page.');
    return t('Wise Old Man: whether the group was updated isn’t known. Check the Wise Old Man page.');
  }
  async function outcomeLines(before) {
    const rb = await readback();
    if (!rb) return t('Whether the rosters were republished couldn’t be checked; the Wise Old Man outcome isn’t known.');
    // A newer roster publication cycle than the page showed before the change.
    if (!(rb.rosterPublicationCycle > before.cycle)) return '';
    return [t('Rosters republished.'), womLine(rb)].filter(Boolean).join(' ');
  }
  async function announce(text, before, options) {
    const lines = before ? await outcomeLines(before) : '';
    if (!signal.aborted) ui.toast(lines ? text + ' ' + lines : text, options);
  }

  /* ---------------- member actions ---------------- */
  function changeRole(x, m, role, viaLayer) {
    const name = m.name, before = republishBase();
    void run({ handler: 'ChangeRole', values: { membershipId: m.id, role: ROLE_ENUM[role], membershipVersion: String(m.version), rosterTeamId: x.id }, what: t('change {0}’s role', name), kind: 'role', layer: viaLayer,
      verify: rb => rb.memberships.some(y => y.membershipId === m.id && y.leftAt == null && y.role === ROLE_NUM[role]),
      okText: t('{0} is now {1}.', name, ROLE[role]().toLowerCase()), notText: t('{0}’s role didn’t change.', name),
      onOk: () => { void announce(t('{0} is now {1} of {2}.', name, ROLE[role]().toLowerCase(), x.name), before); focusSoon('m-' + m.id + '-menu'); } });
  }
  function removeMember(x, m) {
    if (canCorrect()) { openCx('removeMember', { teamId: x.id, teamName: x.name, member: m, size: x.members.length }); return; }
    void run({ handler: 'RemoveMember', values: { membershipId: m.id, rosterTeamId: x.id }, what: t('AdminDesign.remove {0}', m.name), kind: 'remove',
      verify: rb => rb.memberships.some(y => y.membershipId === m.id && y.leftAt != null), okText: t('{0} was removed.', m.name), notText: t('{0} is still on the team.', m.name),
      onOk: () => { ui.toast(t('{0} removed from {1}.', m.name, x.name)); focusSoon('t-' + x.id + '-add'); } });
  }
  function moveMember(x, m, to) {
    void run({ handler: 'MoveMember', values: { membershipId: m.id, targetTeamId: to.id, rosterTeamId: x.id }, what: t('move {0}', m.name), kind: 'move',
      verify: rb => rb.memberships.some(y => y.teamId === to.id && y.participantId === m.participantId && y.leftAt == null), okText: t('{0} is on {1}.', m.name, to.name), notText: t('{0} didn’t move.', m.name),
      onOk: () => { L.fresh = m.participantId; ui.toast(t('{0} moved to {1}.', m.name, to.name)); render(); focusSoon('t-' + to.id + '-menu'); } });
  }

  /* ---------------- confirmations ---------------- */
  function openCx(kind, c = {}) {
    if (blocked()) return;
    const n = S.latestPick ? S.latestPick.number : 0, d = S.distribution, before = republishBase(), wasManual = manual();
    let o;
    if (kind === 'start') o = { title: t('Start the draft?'), body: t('{0} teams and {1} players are in the draft. You’ll take control, then draw the team order as a separate step.', num(drafted().length), num(d ? d.included : 0)),
      points: [t('Team sizes: {0}.', d ? (d.largerCount ? t('{0} of {1} and {2} of {3}', num(d.largerCount), num(d.larger), num(d.teams - d.largerCount), num(d.smaller)) : t('{0} teams of {1}', num(d.teams), num(d.smaller))) : ''), t('Drafted teams can’t be added, removed or switched out of the draft while it runs.'), t('You can cancel and return to setup whenever no picks are active.')],
      confirm: t('Start draft'), busyLabel: t('Starting…'), handler: 'Start', values: {}, what: t('start the draft'), verify: rb => rb.state === 2, okText: t('The draft is running.'), notText: t('The draft didn’t start.'),
      onOk: () => { ui.toast(t('Draft started. Draw the team order when captains are ready.')); focusSoon('turn-primary'); } };
    else if (kind === 'finalize') o = manual()
      ? { title: t('Finalize the rosters?'), body: plural(S.teams.length, 'This publishes {0} team roster to players. No draft is run and no picks are recorded.', 'This publishes {0} team rosters to players. No draft is run and no picks are recorded.'), wide: true,
        points: (S.unplaced ? [plural(S.unplaced, '{0} confirmed player isn’t on a team and won’t appear on a published roster. You can add them afterwards as corrections, until the first Live.', '{0} confirmed players aren’t on a team and won’t appear on a published roster. You can add them afterwards as corrections, until the first Live.')] : [])
          .concat([t('Team sizes: {0}.', S.teams.map(x => `${x.name} ${num(x.members.length)}`).join(', ')), t('The board isn’t published and the event doesn’t start.'), t('Until the event first goes Live, you can still add or remove individual members; each change republishes the rosters.'), t('Rosters can’t return to setup.')]) }
      : { title: t('Finalize the draft?'), body: t('This publishes the rosters and the draft results ({0} picks) to players.', num(n)), wide: true,
        points: [t('Team sizes: {0}.', drafted().map(x => `${x.name} ${num(x.members.length)}`).join(', ')), t('The board isn’t published and the event doesn’t start.'), t('Until the event first goes Live, you can add or remove individual members; each change republishes the rosters and the original picks stay in the history.'), t('The draft can’t be reopened.')] };
    if (kind === 'finalize') Object.assign(o, { confirm: t('Finalize and publish'), busyLabel: t('Publishing…'), handler: 'Finalize', values: { confirmed: 'true' }, what: manual() ? t('finalize the rosters') : t('finalize the draft'),
      verify: rb => rb.state === 4, okText: t('The rosters are published.'), notText: t('Nothing was published.'),
      onOk: () => finalized(wasManual), onRefused: data => { const focus = data?.data?.rosterTeamId; if (focus) focusTeam(focus); } });
    else if (kind === 'removeTeam') o = { title: t('Remove {0}?', c.teamName), body: c.size ? plural(c.size, 'Its {0} member goes back to having no team. Nothing about the player changes; they stay signed up.', 'Its {0} members go back to having no team. Nothing about the players changes; they stay signed up.') : t('It has no members.'),
      confirm: t('Remove team'), busyLabel: t('Removing…'), cls: 'btn-danger', handler: 'RemoveDraftTeam', values: c.size ? { teamId: c.teamId, confirmRemoveMembers: 'true' } : { teamId: c.teamId }, what: t('AdminDesign.remove {0}', c.teamName),
      verify: rb => rb.teams.some(y => y.teamId === c.teamId && !y.active), okText: t('{0} was removed.', c.teamName), notText: t('{0} wasn’t removed.', c.teamName),
      onOk: () => { ui.toast(t('{0} removed.', c.teamName)); focusSoon('add-team'); } };
    else if (kind === 'removeMember') {
      const m = c.member, left = c.size - 1;
      o = { title: t('Remove {0} from {1}?', m.name, c.teamName), body: t('The rosters are republished without them. {0}', t(left === 1 ? '{1} will have {0} member.' : '{1} will have {0} members.', num(left), c.teamName)),
        points: [m.tag === 'pick' ? t('Their original pick stays in the draft history.') : t('The publication history keeps a record of this change.'), t('To put them on another team, add them there afterwards.')].concat(m.role === 'C' ? [t('{0} will have no captain until you assign one.', c.teamName)] : []),
        confirm: t('Remove and republish'), busyLabel: t('Removing…'), cls: 'btn-danger', handler: 'RemoveMember', values: { membershipId: m.id, confirmed: 'true', expectedMembershipVersion: String(m.version), rosterTeamId: c.teamId }, what: t('AdminDesign.remove {0}', m.name),
        verify: rb => rb.memberships.some(y => y.membershipId === m.id && y.leftAt != null), okText: t('{0} was removed.', m.name), notText: t('{0} is still on the team.', m.name),
        onOk: () => { void announce(t('{0} removed from {1}.', m.name, c.teamName), before); focusSoon('t-' + c.teamId + '-add'); } };
    }
    else if (moreConfirm(kind, c)) o = moreConfirm(kind, c);
    if (!o) return;
    const save = o.confirm ? saveButton(o.confirm, o.busyLabel || t('Working…'), o.cls || 'btn-primary', 'cx-confirm') : null;
    const content = h('div', {}, h('h2', { class: 'm-title', id: 'cx-title', 'data-confirm-title': true, text: o.title }),
      h('div', { id: 'cx-desc', 'data-confirm-description': true }, h('p', { class: 'm-body', text: o.body }), o.points?.length ? h('ul', { class: 'm-points' }, o.points.map(pt => h('li', { text: pt }))) : null,
        o.block ? h('div', { class: 'banner is-warning m-banner' }, icon('warning'), h('span', { class: 'grow', text: o.block })) : null, h('div', { 'data-layer-banner': true, hidden: true })),
      h('div', { class: 'm-actions' }, h('button', { class: 'btn', type: 'button', id: 'cx-cancel', autofocus: !save, text: o.cancel || t('Cancel'), onclick: () => void L_cx.close() }), save));
    const L_cx = layer({ title: o.title, content, confirmation: true, cls: o.wide ? 'is-wide' : '' });
    save?.addEventListener('click', () => o.custom ? o.custom(L_cx) : void run({ handler: o.handler, values: o.values, what: o.what, kind, layer: L_cx, verify: o.verify, okText: o.okText, notText: o.notText, onOk: o.onOk, onRefused: o.onRefused }), { signal });
    requestAnimationFrame(() => (save || content.querySelector('#cx-cancel')).focus());
  }
  const moreConfirm = moreConfirmRunning;
  // F1: finalizing stays on Teams. The toast offers Open Board only when a board exists
  // (planner ruling 2); the WOM line follows the same honesty rule as corrections.
  async function finalized(wasManual) {
    focusSoon('page-h1');
    const rb = await readback(), wom = rb ? womLine(rb) : t('Wise Old Man: whether the group was updated isn’t known. Check the Wise Old Man page.');
    if (signal.aborted) return;
    const text = (wasManual ? t('Rosters published.') : t('Rosters and draft results published.')) + (wom ? ' ' + wom : '');
    ui.toast(text, S.boardExists ? { actionLabel: t('Open Board'), action: () => void ui.navigate(`/Admin/Events/Board/${eventId}`) } : undefined);
  }
  function focusTeam(teamId) {
    const card = document.getElementById('card-' + teamId); if (!card) return;
    card.scrollIntoView({ block: 'start', behavior: ui.reducedMotion?.() ? 'auto' : 'smooth' });
    (card.querySelector('.tcard-head .icon-btn') || card.querySelector('h3'))?.focus({ preventScroll: true });
  }

  /* ---------------- lifecycle ---------------- */
  render();
  // rosterTeamId (stored notification links): focus that team's card; unknown ids are ignored.
  const requested = new URL(location.href).searchParams.get('rosterTeamId');
  if (requested && team(requested.toLowerCase())) requestAnimationFrame(() => focusTeam(requested.toLowerCase()));
  const extensions = [];
  connectHub();
  release = () => { life.abort(); for (const stop of extensions) stop(); restoreSide(); pageEl?.classList.remove('is-live'); head?.classList.remove('sr'); };
}

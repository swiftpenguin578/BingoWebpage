// U7 1c / Board.dc.html approval, publication and correction. ApproveState/PublishState
// and Readback are the primary calls; every refusal is shown with the server's issues
// (U7-Q1 drop names, U7-Q2 all publish refusals together, AU19 one "(N empty)" item that
// jumps to the first empty position). Unknown outcomes are settled by the Readback and
// publish and discard are different outcomes (RC05 B1). The correction-reason dialog is
// the confirmation (no checkbox, at most 2,000 characters, B-Board-2).
import { posName } from './admin-board-model.js';

const DRAFT = 1, VALIDATED = 2, PUBLISHED = 3;

export function install(ctx) {
  const { ui, t, el, icon, button, on } = ctx;
  let issues = null; // { title, text, list }

  /* ---------------- issue list (AU19) ---------------- */
  function issueList(found) {
    const cols = ctx.view.cols, list = [];
    const empty = found.filter(x => x.code === 'board-incomplete');
    if (empty.length) {
      const first = Math.min(...empty.map(x => x.position ?? 0));
      list.push({ label: t('Fill every board position'), text: t('({0} empty).', empty.length), pos: first });
    }
    for (const issue of found.filter(x => x.code !== 'board-incomplete')) {
      const pos = issue.position ?? null, where = pos != null ? posName(pos, cols) + ' ' + (issue.tileName || '') : '';
      let text = issue.message;
      if (issue.code === 'catalogue-rates-missing' && issue.dropNames?.length)
        text = t('needs automatic EHB: no drop rate in the catalogue for {0}. Correct the rate in Catalogue; a manual EHB can’t replace it.', issue.dropNames.join(', '));
      else if (issue.code === 'item-price-missing' && pos != null)
        text = t('uses drops with no catalogue GP value. {0}', issue.message);
      list.push({ label: where.trim(), text, pos });
    }
    return list;
  }
  function showIssues(found, title) {
    const list = issueList(found);
    issues = { title: title(list.length), text: '', list };
    ctx.issues = { list: list.filter(x => x.pos != null) };
    ctx.paint();
    ctx.focusSoon('bd-banner');
  }
  function goToIssue(item) {
    issues = issues; ctx.focusPos = item.pos;
    const tile = ctx.tileAt(item.pos);
    ctx.paint();
    if (tile || ctx.canEdit()) ctx.hooks.openTile?.(item.pos); else ctx.focusSoon(ctx.cellId(item.pos));
  }
  ctx.hooks.banner.push(() => {
    if (issues) return { role: 'alert', cls: 'is-error', title: issues.title, text: issues.text, list: issues.list.map(item => ({ label: item.label, text: item.text, go: item.pos != null ? () => goToIssue(item) : null })), action: t('Dismiss'), run: () => { issues = null; ctx.issues = null; ctx.paint(); ctx.focusSoon('primary-btn'); } };
    const v = ctx.view;
    if (v.mode === 'approved' && !v.readOnly) {
      const ready = readiness();
      if (ready.length) {
        const spec = { cls: 'is-info', title: t('Ready to publish once:'), text: ready.map(x => x.text).join(' ') };
        if (ready.some(x => x.go)) { spec.action = t('Open {0}', t('Teams / Draft')); spec.run = () => void ui.navigate(location.pathname.replace(/\/Board\//i, '/Draft/')); }
        return spec;
      }
    }
    return null;
  });
  function readiness() {
    const r = ctx.view.publishReady || {}, out = [];
    if (!r.roster) out.push({ text: t('Finalize the team rosters first.'), go: true });
    if (r.started) out.push({ text: t('The event has already started.') });
    if (r.ended) out.push({ text: t('The event’s end has passed.') });
    return out;
  }

  /* ---------------- confirmation layer (points, reason, busy, in-dialog error) ---------------- */
  function confirmLayer({ title, body, points = [], reason = false, confirmLabel, confirmCls = 'btn-primary', run, wide = false }) {
    const content = el('div', 'bd-layer'); content.dataset.pageFamily = 'board';
    const h = el('h2', 'm-title', title); h.dataset.confirmTitle = '';
    const desc = el('div'); desc.dataset.confirmDescription = ''; desc.append(el('p', 'm-body', body));
    if (points.length) { const ul = el('ul', 'm-points'); for (const p of points) ul.append(el('li', null, p)); desc.append(ul); }
    const errSlot = el('div');
    const state = { busy: false, reason: '', showErr: false };
    let field, ta, count, reasonErr;
    if (reason) {
      field = el('div', 'field bd-reason');
      const lr = el('div', 'lbl-row'), lbl = el('label', 'lbl', t('Reason')); lbl.htmlFor = 'cx-reason'; count = el('span', 'field-count'); count.setAttribute('aria-live', 'polite'); lr.append(lbl, count);
      ta = el('textarea', 'textarea'); ta.id = 'cx-reason'; ta.placeholder = t('e.g. Tile C4 named the wrong boss.'); ta.setAttribute('aria-describedby', 'cx-reason-hint');
      const hint = el('div', 'field-hint', t('Required. Kept in the board history.')); hint.id = 'cx-reason-hint';
      reasonErr = el('div');
      on(ta, 'input', () => { state.reason = ta.value; paintReason(); });
      field.append(lr, ta, hint, reasonErr);
    }
    const acts = el('div', 'm-actions'), cancel = button('btn', t('Cancel'), () => void layer.close(false), 'cx-cancel'), confirm = button('btn ' + confirmCls, null, () => void go(), 'cx-confirm');
    acts.append(cancel, confirm);
    content.append(h, desc, errSlot, ...(field ? [field] : []), acts);
    const layer = ui.openLayer({ title, content, confirmation: true, pending: () => state.busy });
    if (wide) layer.element.classList.add('is-wide');
    function reasonError() {
      const r = state.reason.trim();
      return !state.showErr ? '' : !r ? t('Enter a reason.') : r.length > 2000 ? t('Use 2,000 characters or fewer.') : '';
    }
    function paintReason() {
      if (!reason) return;
      const e = reasonError(), n = state.reason.length;
      count.hidden = n <= 1700; count.textContent = n.toLocaleString(ctx.lang) + ' / 2,000'; count.classList.toggle('is-over', n > 2000);
      ta.classList.toggle('is-invalid', !!e); ta.setAttribute('aria-invalid', e ? 'true' : 'false');
      ta.setAttribute('aria-describedby', 'cx-reason-hint' + (e ? ' cx-reason-err' : ''));
      reasonErr.replaceChildren(...(e ? [Object.assign(el('div', 'field-err'), { id: 'cx-reason-err' })] : []));
      if (e) reasonErr.firstChild.append(icon('error'), document.createTextNode(e));
    }
    function paintButtons() {
      confirm.replaceChildren(...(state.busy ? [el('span', 'spin')] : []), document.createTextNode(state.busy ? t('Working…') : confirmLabel));
      confirm.classList.toggle('is-busy', state.busy); confirm.disabled = state.busy; cancel.disabled = state.busy; if (ta) ta.disabled = state.busy;
    }
    function setError(text) {
      const b = el('div', 'banner is-error m-banner'); b.setAttribute('role', 'alert'); b.id = 'cx-banner'; b.tabIndex = -1;
      b.append(icon('error'), el('span', 'grow', text)); errSlot.replaceChildren(b); b.focus();
    }
    async function go() {
      if (state.busy) return;
      if (reason) { state.showErr = true; paintReason(); if (reasonError()) { ta.focus(); return; } }
      state.busy = true; errSlot.replaceChildren(); paintButtons();
      const result = await run(state.reason.trim(), { setError });
      state.busy = false; paintButtons();
      if (result === 'close') await layer.close(true);
    }
    paintButtons(); paintReason();
    if (ta) requestAnimationFrame(() => ta.focus());
    return layer;
  }

  /* ---------------- commands ---------------- */
  // Shared settling for state commands. 'close' closes the confirmation.
  async function settleState(outcome, { want, what, ok, title, setError }) {
    if (outcome.kind === 'state') {
      if (outcome.issues.length === 0 && outcome.current?.state && want(outcome.current.state)) { issues = null; ctx.issues = null; await ctx.refresh(); ui.toast(ok); ctx.focusSoon('primary-btn'); return 'close'; }
      if (outcome.issues.length) { await ctx.refresh(); showIssues(outcome.issues, title); return 'close'; }
    }
    if (outcome.kind === 'saved') { await ctx.refresh(); ui.toast(outcome.message || ok); ctx.focusSoon('primary-btn'); return 'close'; }
    if (outcome.kind === 'refused' || outcome.kind === 'route-refused') {
      if (outcome.issues?.length) { await ctx.refresh(); showIssues(outcome.issues, title); return 'close'; }
      if (setError) { setError(outcome.message || t('That change wasn’t saved.')); await ctx.refresh(); return 'stay'; }
      ui.toast(outcome.message || t('That change wasn’t saved.'), { error: true }); await ctx.refresh(); return 'close';
    }
    if (outcome.kind === 'session-lost' || outcome.kind === 'aborted') return 'stay';
    const result = await ctx.uncertain(what, want);
    if (result === 'happened') ui.toast(ok);
    return 'close';
  }
  const fixTitle = n => n === 1 ? t('One thing needs fixing first.') : t('{0} things need fixing first.', n);
  async function approve() {
    if (ctx.blocked() || !ctx.canEdit()) return;
    issues = null; ctx.issues = null;
    const outcome = await ctx.command('ApproveState', { ApprovalCatalogueFingerprint: ctx.fingerprint(), confirmed: false });
    await settleState(outcome, { want: s => s.state === VALIDATED && !s.correctionInProgress, what: t('approve the board'), ok: t('Board approved privately. Publishing is a separate step.'), title: fixTitle });
  }
  async function unapprove() {
    if (ctx.blocked() || !ctx.canEdit()) return;
    const outcome = await ctx.command('Unapprove', {});
    await settleState(outcome, { want: s => s.state === DRAFT, what: t('return the board to draft'), ok: t('Board returned to draft. The approval stays in its history.') });
  }
  function openPublish() {
    const v = ctx.view;
    confirmLayer({
      title: t('Publish the board?'), body: t('Players will see the approved {0} × {1} board for {2}.', v.rows, v.cols, v.eventName),
      points: [t('Publishing doesn’t start the event; it starts at its scheduled time or from Overview.'), t('After publishing, changes need a correction with a reason.')],
      confirmLabel: t('Publish board'),
      run: async (_, { setError }) => settleState(await ctx.command('PublishState', { confirmed: true }), { want: s => s.state === PUBLISHED && !s.correctionInProgress, what: t('publish the board'), ok: t('Board published. Players can see it; the event starts separately.'), title: () => t('The board can’t be published yet.'), setError })
    });
  }
  function openCorrect() {
    confirmLayer({
      title: t('Correct the published board?'), body: t('This is for exceptional fixes. You edit a private copy; players keep the current board until you publish the correction.'),
      points: [t('Objectives with submitted evidence keep their requirements and scoring; wording can change.'), t('Results, evidence and the published history stay as they are.')],
      reason: true, wide: true, confirmLabel: t('Start correction'),
      run: async (reason, { setError }) => settleState(await ctx.command('CorrectPublished', { confirmed: true, reason }, { draft: { [t('Reason')]: reason } }), { want: s => s.correctionInProgress, what: t('start a correction'), ok: t('Private correction started. Players still see the published board.'), setError })
    });
  }
  // Planner ruling 3: the server count, naming the removed tiles.
  function differs() {
    const d = ctx.view.differences || { count: 0, removed: 0 };
    if (!d.count) return t('No tiles differ from the published board.');
    const base = d.count === 1 ? t('1 tile differs from the published board') : t('{0} tiles differ from the published board', d.count);
    return d.removed ? base + ', ' + t(d.removed === 1 ? '1 of them removed.' : '{0} of them removed.', d.removed) : base + '.';
  }
  function openPublishCorrection() {
    const v = ctx.view;
    confirmLayer({
      title: t('Publish the correction?'), body: differs() + ' ' + t('The corrected board is validated and replaces it for players.'),
      points: [t('The earlier version stays in history; evidence and results aren’t rescored.'), ...(v.reason ? [t('Reason: {0}', v.reason)] : [])],
      wide: true, confirmLabel: t('Publish correction'),
      run: async (_, { setError }) => settleState(await ctx.command('ApproveState', { ApprovalCatalogueFingerprint: ctx.fingerprint(), confirmed: true }), { want: s => s.state === PUBLISHED && !s.correctionInProgress && s.activeApprovalId !== ctx.view.activeApprovalId, what: t('publish the correction'), ok: t('Corrected board published. The earlier version stays in history.'), title: fixTitle, setError })
    });
  }
  function openDiscard() {
    confirmLayer({
      title: t('Discard the correction?'), body: t('All unpublished edits in this correction will be lost, and the working board returns to the published version.'),
      points: [t('Published results and submitted evidence stay as they are.')],
      confirmLabel: t('Discard correction'), confirmCls: 'btn-danger',
      // Publish and discard are different outcomes: a discard keeps the same active approval.
      run: async (_, { setError }) => settleState(await ctx.command('DiscardCorrection', { confirmed: true }), { want: s => !s.correctionInProgress && s.activeApprovalId === ctx.view.activeApprovalId, what: t('discard the correction'), ok: t('Correction discarded. The working board matches the published one again.'), setError })
    });
  }

  /* ---------------- header primary action and menu ---------------- */
  ctx.hooks.primary = () => {
    const v = ctx.view, me = ctx.canEdit(), busy = ctx.blocked();
    if (v.readOnly) return null;
    if (v.mode === 'draft') return { label: ctx.pending === 'ApproveState' ? t('Approving…') : t('Approve board'), busy: ctx.pending === 'ApproveState', inert: !me || busy, title: me ? undefined : t('Start editing to approve'), run: () => void approve() };
    if (v.mode === 'approved') { const r = readiness(); return { label: t('Publish board…'), inert: r.length > 0 || busy, title: r.length ? r.map(x => x.text).join(' ') : undefined, describedBy: r.length ? 'bd-banner' : undefined, run: openPublish }; }
    if (v.mode === 'correction') return { label: t('Publish correction…'), inert: !me || busy, title: me ? undefined : t('Start editing to publish'), run: openPublishCorrection };
    return null;
  };
  (ctx.hooks.menu ||= []).push(() => {
    const v = ctx.view, me = ctx.canEdit(), items = [], startFirst = t('Start editing first');
    if (v.readOnly) return items;
    if (v.mode === 'approved') items.push({ label: t('Return to draft'), disabled: !me || ctx.blocked(), hint: me ? '' : startFirst, run: () => void unapprove() });
    if (v.mode === 'published') items.push({ label: t('Correct published board…'), disabled: !v.correctionAllowed || ctx.blocked(), hint: v.correctionAllowed ? '' : t('Not available in this event state'), run: openCorrect });
    if (v.mode === 'correction') items.push({ sep: true }, { label: t('Discard correction…'), danger: true, disabled: !me || ctx.blocked(), hint: me ? '' : startFirst, run: openDiscard });
    return items;
  });
}

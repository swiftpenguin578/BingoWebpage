// U4 / OS-1: Overview.dc.html lifecycle dialogs, evidence codes and public-link copy.
// The page is server-rendered; dialogs come from data-current (OverviewPresenter) and
// every action is re-checked by the server. Transport: AdminFetch (C-CMP-2).
import { mountDateTime, localToUtc } from './admin-date-time.js';

let release = () => {};
let focusAfterRefresh = null;
export function dispose() { release(); release = () => {}; }
export async function init(region, ui = window.AdminUI) {
  dispose();
  const life = new AbortController();
  const on = (el, type, fn) => el?.addEventListener(type, fn, { signal: life.signal });
  let root = region.querySelector('[data-overview]');
  if (!root) return;
  const labels = JSON.parse(root.dataset.labels);
  const t = (key, ...args) => (labels[key] || key).replace(/\{(\d+)\}/g, (_, i) => String(args[+i] ?? ''));
  let state = JSON.parse(root.dataset.current);
  const timezone = () => root.dataset.timezone || 'UTC';
  const receivedAt = Date.now(), serverNow = Date.parse(root.dataset.now) || Date.now();
  const now = () => serverNow + Date.now() - receivedAt;
  const url = handler => { const u = new URL(location.href); u.search = ''; if (handler) u.searchParams.set('handler', handler); return u.href; };
  const form = entries => new URLSearchParams(Object.entries(entries).filter(([, value]) => value !== undefined && value !== null).map(([k, value]) => [k, String(value)]));
  const post = (handler, body, draft) => ui.busy(() => window.AdminFetch.request(url(handler), { method: 'POST', body: form(body), draft, signal: life.signal }));
  async function read(draft = {}) {
    const result = await window.AdminFetch.request(url('Current'), { cache: 'no-store', readback: true, draft, signal: life.signal });
    if (result.kind === 'handler' && result.data?.version) return { ok: true, data: result.data };
    return { ok: false, gone: result.kind === 'unknown' && result.status === 404, sessionLost: result.kind === 'session-lost' };
  }
  const el = (tag, cls, text) => { const e = document.createElement(tag); if (cls) e.className = cls; if (text !== undefined) e.textContent = text; return e; };
  const icon = name => { const source = document.querySelector(`[data-overview-icon="${name}"]`); return source ? source.content.firstElementChild.cloneNode(true) : el('span'); };
  const fmtLocal = ms => {
    const parts = Object.fromEntries(new Intl.DateTimeFormat(document.documentElement.lang === 'da' ? 'da-DK' : 'en-GB', { timeZone: timezone(), day: 'numeric', month: 'short', year: 'numeric', hour: '2-digit', minute: '2-digit', hourCycle: 'h23' }).formatToParts(new Date(ms)).map(p => [p.type, p.value]));
    return `${Number(parts.day)} ${parts.month} ${parts.year}, ${parts.hour}:${parts.minute}`;
  };

  // Page refresh after an outcome: the server re-renders the composition; header, sidebar
  // context and the snapshot are replaced in place (no navigation, scroll kept).
  async function refresh(focusId = 'now-title') {
    const page = region.querySelector('.page'), results = root;
    focusAfterRefresh = focusId;
    await ui.update(location.href, {
      root: page, results,
      patch: doc => {
        const next = doc.querySelector('[data-overview]'), head = doc.querySelector('[data-page-region] .page-head');
        if (head) page.querySelector('.page-head').replaceChildren(...[...head.childNodes].map(node => document.importNode(node, true)));
        root.replaceChildren(...[...next.childNodes].map(node => document.importNode(node, true)));
        for (const key of Object.keys(next.dataset)) root.dataset[key] = next.dataset[key];
        state = JSON.parse(root.dataset.current);
        ui.refreshContext(doc);
        return location.href;
      },
      pending: () => document.querySelector('template[data-page-loading-template="overview"]').content.cloneNode(true),
      failed: () => document.querySelector('[data-admin-template="load-failure"]').content.cloneNode(true),
      fallbackFocus: () => document.getElementById(focusId) || document.getElementById('now-title') || page.querySelector('.h1')
    });
    const target = document.getElementById(focusAfterRefresh) || document.getElementById('now-title');
    focusAfterRefresh = null;
    target?.focus({ preventScroll: false });
  }

  /* ---------------- lifecycle dialogs ---------------- */
  let dialog = null;
  function openAction(key, opener) {
    if (dialog) return;
    const model = state.dialogs[key];
    if (!model || !model.applicable) return;
    if (!model.ready && !model.requirement) { ui.toast(t('Finish the requirements listed first.')); return; }
    const content = ui.template('confirmation');
    const description = el('div'); description.dataset.confirmDescription = '';
    content.querySelector('[data-confirm-description]').replaceWith(description);
    const banner = el('div', 'banner ov-dlg-banner'); banner.setAttribute('role', 'alert'); banner.hidden = true;
    banner.append(icon('error'), el('span', 'grow'));
    const effects = el('ul', 'ov-effects');
    description.append(banner, effects);
    const fields = el('div', 'm-extra ov-dlg-fields');
    let reason = null, reasonError = null, until = null, untilValue = null, untilError = null, picker = null;
    if (model.needsUntil) {
      const field = el('div', 'field field-narrow'), label = el('div', 'lbl', model.untilLabel); label.id = 'dlg-until-lbl';
      const dtp = el('div', 'dtp'); Object.assign(dtp.dataset, { dateTime: 'dlg-until', label: model.untilLabel, timezone: timezone(), now: new Date(now()).toISOString(), clearable: 'false' });
      dtp.id = 'dlg-until-field'; dtp.setAttribute('role', 'group'); dtp.setAttribute('aria-labelledby', 'dlg-until-lbl'); dtp.setAttribute('aria-describedby', 'dlg-until-hint');
      const date = el('input', 'dtp-date'); date.id = 'dlg-until-date'; date.placeholder = 'dd mmm yyyy'; date.autocomplete = 'off'; date.spellcheck = false; date.setAttribute('aria-label', t('{0}, date', model.untilLabel));
      const time = el('input', 'dtp-time'); time.id = 'dlg-until-time'; time.placeholder = 'hh:mm'; time.inputMode = 'numeric'; time.autocomplete = 'off'; time.setAttribute('aria-label', t('{0}, time (24-hour)', model.untilLabel));
      const sep = el('span', 'dtp-sep'); sep.setAttribute('aria-hidden', 'true');
      const button = el('button', 'icon-btn dtp-btn'); button.type = 'button'; button.id = 'dlg-until-btn'; button.title = t('Calendar'); button.setAttribute('aria-label', t('Choose {0} from a calendar', model.untilLabel)); button.setAttribute('aria-haspopup', 'dialog'); button.append(icon('calendar'));
      dtp.append(date, sep, time, button);
      untilValue = el('input'); untilValue.type = 'hidden'; untilValue.name = model.untilField; untilValue.value = model.untilDefault || '';
      const hint = el('div', 'field-hint', model.untilHint); hint.id = 'dlg-until-hint';
      untilError = el('div', 'field-err'); untilError.id = 'dlg-until-err'; untilError.hidden = true;
      field.append(label, untilValue, dtp, hint, untilError); fields.append(field);
      until = dtp;
    }
    if (model.needsReason) {
      const field = el('div', 'field'), label = el('label', 'lbl', model.reasonLabel); label.htmlFor = 'dlg-reason';
      reason = el('textarea', 'textarea ov-reason-input'); reason.id = 'dlg-reason'; reason.name = model.reasonField; reason.maxLength = 2000; reason.setAttribute('aria-describedby', 'dlg-reason-hint dlg-reason-err');
      const hint = el('div', 'field-hint', model.reasonHint); hint.id = 'dlg-reason-hint';
      reasonError = el('div', 'field-err'); reasonError.id = 'dlg-reason-err'; reasonError.hidden = true;
      field.append(label, reason, hint, reasonError); fields.append(field);
    }
    if (reason || until) content.querySelector('.m-actions').before(fields);
    const cancel = content.querySelector('[data-confirm-cancel]'), confirm = content.querySelector('[data-confirm-accept]');
    cancel.removeAttribute('autofocus');
    const d = { key, model, version: state.version, status: 'idle', what: '', error: '', intent: null };
    const busy = () => d.status === 'busy' || d.status === 'checking';
    const layer = ui.openLayer({
      title: model.title, content, confirmation: !(reason || until), pending: busy,
      // An unknown or overtaken outcome is not an unsaved draft (U-A): close and re-read.
      confirmLeave: () => (['uncertain', 'gone', 'notApplied'].includes(d.status) ? Promise.resolve(true) : ui.confirmDiscard()),
      opener,
      onClose: async () => { picker?.dispose(); const reread = ['uncertain', 'gone', 'checking'].includes(d.status); dialog = null; if (reread) await refresh(opener?.id || 'now-title'); }
    });
    if (untilValue) { picker = mountDateTime(until, untilValue, () => { untilError.hidden = true; }, document.querySelector('[data-date-time-template]')); layer.markClean(); }
    const panel = layer.element; panel.classList.add('is-wide'); panel.dataset.pageFamily = 'overview'; panel.setAttribute('role', 'alertdialog');
    dialog = { layer, d };
    const error = (node, message) => { if (!node) return; node.replaceChildren(icon('error'), document.createTextNode(message || '')); node.hidden = !message; };
    function paint() {
      const m = d.model, st = d.status;
      panel.querySelector('[data-confirm-title]').textContent = m.title;
      const tone = st === 'failed' || st === 'refused' ? 'is-error' : st === 'notApplied' ? 'is-info' : 'is-warning';
      const text = st === 'failed' || st === 'refused' ? d.error || t('Couldn’t complete this. Nothing changed, and your entries are still here.')
        : st === 'stale' ? (d.what ? t('The event changed while this was open: {0}. The details below are up to date. Check them and confirm again.', d.what) : t('The event changed while this was open. The details below are up to date. Check them and confirm again.'))
        : st === 'gone' ? (d.error || (d.what ? t('The event changed while this was open: {0}. {1} no longer applies, so nothing was done.', d.what, m.label.replace('…', '')) : t('The event changed while this was open. {0} no longer applies, so nothing was done.', m.label.replace('…', ''))))
        : st === 'checking' ? t('Checking the event…')
        : st === 'uncertain' ? t('We couldn’t confirm whether this happened. Check before trying again, so it doesn’t happen twice.')
        : st === 'notApplied' ? t('The event is unchanged, so it didn’t happen. You can try again.')
        : m.requirement || '';
      banner.className = `banner ov-dlg-banner ${tone}`; banner.hidden = !text; banner.querySelector('.grow').textContent = text;
      effects.replaceChildren(...(st === 'gone' ? [] : m.effects.map(x => el('li', null, x))));
      fields.hidden = st === 'gone';
      const locked = busy() || st === 'uncertain' || st === 'checking' || st === 'gone';
      if (reason) reason.disabled = locked;
      picker?.setDisabled(locked);
      cancel.textContent = st === 'gone' ? t('Close') : t('Cancel'); cancel.disabled = busy();
      confirm.hidden = st === 'gone';
      confirm.className = `btn ${st === 'uncertain' || st === 'checking' ? 'btn-primary' : m.cls}${busy() ? ' is-busy' : ''}`;
      confirm.disabled = busy() || !!m.requirement && !['uncertain', 'checking'].includes(st);
      confirm.querySelector('.spin').hidden = !busy();
      confirm.querySelector('[data-component-text]').textContent = st === 'checking' ? t('Checking…') : st === 'uncertain' ? t('Check again') : st === 'busy' ? t('Working…') : m.confirm;
      panel.setAttribute('aria-busy', String(busy()));
    }
    function validate() {
      let first = null;
      if (reason) { const missing = !reason.value.trim(); error(reasonError, missing ? t('Enter a reason.') : ''); reason.setAttribute('aria-invalid', String(missing)); reason.classList.toggle('is-invalid', missing); if (missing) first = reason; }
      if (untilValue) {
        let message = '';
        if (!picker.validate()) message = ' ';
        else {
          const converted = localToUtc(untilValue.value, timezone());
          message = converted.status === 'empty' ? t('Choose a date and time.')
            : converted.status === 'invalid' ? t('That local time does not exist because the clocks change at that time.')
            : converted.status === 'ambiguous' ? t('That local time is ambiguous because the clocks change at that time. Choose another time.')
            : converted.ms <= now() ? t('Choose a time in the future.')
            : Number(untilValue.value.slice(-2)) % 5 ? t('Use a 5-minute step.') : '';
        }
        error(untilError, message.trim()); picker.setInvalid(!!message);
        if (message && !first) first = panel.querySelector('#dlg-until-date');
      }
      first?.focus();
      return !first;
    }
    const draft = () => Object.fromEntries([...(reason ? [[d.model.reasonLabel, reason.value]] : []), ...(untilValue ? [[d.model.untilLabel, untilValue.value ? fmtLocal(localToUtc(untilValue.value, timezone()).ms ?? Date.now()) : '']] : [])]);
    async function run() {
      if (d.status === 'uncertain') return check();
      if (!validate()) return;
      const m = d.model, body = { EventVersion: d.version };
      if (m.confirmField) body[m.confirmField] = 'true';
      if (reason) body[m.reasonField] = reason.value.trim();
      if (untilValue) body[m.untilField] = untilValue.value;
      d.intent = { until: untilValue?.value || null };
      d.status = 'busy'; paint(); panel.focus({ preventScroll: true });
      const result = await post(m.handler, body, draft());
      if (!dialog || dialog.d !== d) return;
      if (result.kind === 'session-lost') { d.status = 'idle'; paint(); return; }
      if (result.kind === 'refused') { d.error = result.reason || t('Couldn’t complete this. Nothing changed, and your entries are still here.'); const fresh = await read(); if (fresh.ok) state = fresh.data; d.status = 'gone'; paint(); cancel.focus(); return; }
      if (result.kind === 'unknown' && result.status === 404) { d.status = 'gone'; d.error = ''; paint(); cancel.focus(); return; }
      if (result.kind !== 'handler' || typeof result.data?.succeeded !== 'boolean') { d.status = 'uncertain'; paint(); confirm.focus(); return; }
      const outcome = result.data;
      if (outcome.succeeded) return finish(outcome.message || m.success.replace('{0}', ''), outcome.location);
      if (outcome.outcome === 'stale') return restale();
      if (outcome.outcome === 'invalid' && outcome.fieldErrors) {
        d.status = 'idle'; paint();
        if (outcome.fieldErrors.reason) { error(reasonError, outcome.fieldErrors.reason); reason?.focus(); }
        if (outcome.fieldErrors.until) { error(untilError, outcome.fieldErrors.until); picker?.setInvalid(true); panel.querySelector('#dlg-until-date')?.focus(); }
        if (!outcome.fieldErrors.reason && !outcome.fieldErrors.until) { d.status = 'refused'; d.error = outcome.error; paint(); }
        return;
      }
      d.status = 'refused'; d.error = outcome.error || ''; paint(); confirm.focus();
    }
    async function restale() {
      const fresh = await read(draft());
      if (!dialog || dialog.d !== d) return;
      if (!fresh.ok) { d.status = fresh.gone ? 'gone' : 'failed'; paint(); return; }
      state = fresh.data; d.what = fresh.data.lastChange || '';
      const next = fresh.data.dialogs[d.key];
      if (!next || !next.applicable || !next.ready && !next.requirement) { d.status = 'gone'; d.error = ''; paint(); cancel.focus(); return; }
      d.model = next; d.version = fresh.data.version; d.status = 'stale'; paint(); confirm.focus();
    }
    async function check() {
      d.status = 'checking'; paint(); panel.focus({ preventScroll: true });
      const fresh = await ui.busy(() => read(draft()));
      if (!dialog || dialog.d !== d) return;
      const m = d.model;
      if (fresh.gone && m.doneGone) return finish(m.success, '/Admin/Events/Index');
      if (fresh.gone) { d.status = 'gone'; d.error = ''; paint(); cancel.focus(); return; }
      if (!fresh.ok) { d.status = 'uncertain'; paint(); confirm.focus(); return; }
      state = fresh.data;
      const reopenedMs = fresh.data.reopenedUntil ? Date.parse(fresh.data.reopenedUntil) : null;
      const done = m.donePhase ? fresh.data.phase === m.donePhase
        : m.doneHidden !== null && m.doneHidden !== undefined ? fresh.data.hidden === m.doneHidden
        : m.doneReopened ? reopenedMs !== null && d.intent?.until && localToUtc(d.intent.until, timezone()).ms === reopenedMs : false;
      if (done) return finish(m.doneReopened ? m.success.replace('{0}', fmtLocal(reopenedMs)) : m.success);
      d.status = 'notApplied'; d.version = fresh.data.version; const next = fresh.data.dialogs[d.key]; if (next) d.model = next; paint(); confirm.focus();
    }
    async function finish(message, location) {
      d.status = 'done';
      await layer.close(true);
      ui.toast(message);
      if (location) { await ui.navigate(location); return; }
      await refresh('now-title');
    }
    on(cancel, 'click', () => { if (!busy()) void ui.closeLayer(); });
    on(confirm, 'click', () => { if (!busy()) void run(); });
    paint();
    (reason || panel.querySelector('#dlg-until-date') || cancel).focus();
  }

  /* ---------------- evidence codes (RC01 R2) ---------------- */
  let codes = null;
  function openCodes(opener) {
    if (codes || dialog) return;
    const c = state.codes;
    const content = document.createDocumentFragment();
    const head = el('div', 'mf-head'), title = el('h2', 'm-title', t('Evidence codes')); title.id = 'codes-title'; title.dataset.confirmTitle = '';
    const desc = el('p', 'm-body', t('Captains include the active code in screenshots, so reviewers can tell when evidence was captured. Each code is retired when the next one starts.')); desc.id = 'codes-desc'; desc.dataset.confirmDescription = '';
    head.append(title, desc);
    const body = el('div', 'mf-body');
    const banner = el('div', 'banner is-error'); banner.setAttribute('role', 'alert'); banner.hidden = true; banner.append(icon('error'), el('span', 'grow'));
    const toggleRow = el('label', 'check-row'), toggle = el('input'); toggle.type = 'checkbox'; toggle.id = 'codes-enabled';
    const toggleText = el('span'), toggleTitle = el('span', null, t('Require evidence codes')), toggleSub = el('span', 'choice-sub');
    toggleTitle.style.display = 'block'; toggleTitle.style.fontWeight = '550'; toggleSub.style.display = 'block';
    toggleText.append(toggleTitle, toggleSub); toggleRow.append(toggle, toggleText);
    const list = el('div', 'ro-list'); list.setAttribute('aria-label', t('Codes'));
    const codeForm = el('div', 'ov-code-form');
    const codeField = el('div', 'field'), codeLabel = el('label', 'lbl', t('New code')); codeLabel.htmlFor = 'code-value';
    const codeRow = el('div', 'ov-code-input'), code = el('input', 'input mono'); code.id = 'code-value'; code.maxLength = 100; code.autocomplete = 'off'; code.setAttribute('aria-describedby', 'code-err');
    const generate = el('button', 'btn', t('Generate')); generate.type = 'button';
    codeRow.append(code, generate);
    const codeError = el('div', 'field-err'); codeError.id = 'code-err'; codeError.hidden = true;
    codeField.append(codeLabel, codeRow, codeError);
    const fromField = el('div', 'field'), fromLabel = el('div', 'lbl'); fromLabel.id = 'code-from-lbl'; fromLabel.append(document.createTextNode(t('Active from') + ' '), el('span', 'opt', '· ' + c.timezone));
    const fromValue = el('input'); fromValue.type = 'hidden'; fromValue.value = c.defaultFrom;
    const dtp = el('div', 'dtp'); Object.assign(dtp.dataset, { dateTime: 'code-from', label: t('Active from'), timezone: timezone(), now: new Date(now()).toISOString(), clearable: 'false' });
    dtp.setAttribute('role', 'group'); dtp.setAttribute('aria-labelledby', 'code-from-lbl');
    const date = el('input', 'dtp-date'); date.id = 'code-from-date'; date.placeholder = 'dd mmm yyyy'; date.autocomplete = 'off'; date.setAttribute('aria-label', t('{0}, date', t('Active from')));
    const time = el('input', 'dtp-time'); time.id = 'code-from-time'; time.placeholder = 'hh:mm'; time.inputMode = 'numeric'; time.autocomplete = 'off'; time.setAttribute('aria-label', t('{0}, time (24-hour)', t('Active from')));
    const sep = el('span', 'dtp-sep'); sep.setAttribute('aria-hidden', 'true');
    const calendar = el('button', 'icon-btn dtp-btn'); calendar.type = 'button'; calendar.title = t('Calendar'); calendar.setAttribute('aria-label', t('Choose {0} from a calendar', t('Active from'))); calendar.setAttribute('aria-haspopup', 'dialog'); calendar.append(icon('calendar'));
    dtp.append(date, sep, time, calendar);
    const fromError = el('div', 'field-err'); fromError.id = 'from-err'; fromError.hidden = true;
    fromField.append(fromLabel, fromValue, dtp, fromError);
    const noteField = el('div', 'field'), noteLabel = el('label', 'lbl'); noteLabel.htmlFor = 'code-note'; noteLabel.append(document.createTextNode(t('Note') + ' '), el('span', 'opt', '· ' + t('optional, admins only')));
    const note = el('input', 'input'); note.id = 'code-note'; note.autocomplete = 'off'; note.maxLength = 1000;
    noteField.append(noteLabel, note);
    codeForm.append(codeField, fromField, noteField);
    body.append(banner, toggleRow, list, codeForm);
    const foot = el('div', 'mf-foot'), close = el('button', 'btn', t('Close')); close.type = 'button'; close.id = 'codes-close';
    const save = el('button', 'btn btn-primary'); save.type = 'button'; save.id = 'codes-save'; const spin = el('span', 'spin'); spin.hidden = true; const saveText = el('span', null, t('Add code')); save.append(spin, saveText);
    foot.append(close, save);
    content.append(head, body, foot);
    let busy = false, changed = false, chosen = null;
    let picker = null;
    const layer = ui.openLayer({ title: t('Evidence codes'), content, pending: () => busy, opener, onClose: async () => { picker?.dispose(); codes = null; if (changed) await refresh(opener?.id || 'now-title'); } });
    picker = mountDateTime(dtp, fromValue, () => { fromError.hidden = true; }, document.querySelector('[data-date-time-template]')); layer.markClean();
    const panel = layer.element; panel.classList.add('modal-form'); panel.dataset.pageFamily = 'overview'; panel.removeAttribute('aria-label'); panel.setAttribute('aria-labelledby', 'codes-title'); panel.setAttribute('aria-describedby', 'codes-desc');
    codes = { layer };
    const showError = (node, message) => { node.replaceChildren(icon('error'), document.createTextNode(message || '')); node.hidden = !message; };
    function paint() {
      const current = state.codes;
      // The chosen state shows at once while the save is pending.
      const shown = chosen ?? current.enabled; toggle.checked = shown; toggleRow.classList.toggle('is-on', shown); toggleSub.textContent = current.enabledSub; toggle.disabled = busy;
      list.hidden = current.list.length === 0;
      list.replaceChildren(...current.list.map(row => {
        const r = el('div', 'ro-row'), value = el('span', 'mono', row.code); value.style.fontSize = '13px'; value.style.minWidth = '72px';
        const when = el('span', 'grow', row.when); if (row.note) { const sub = el('span', 'choice-sub', row.note); sub.style.display = 'block'; when.append(sub); }
        r.append(value, when, el('span', `pill ${row.pillCls}`, row.state)); return r;
      }));
      const locked = busy || !current.enabled;
      for (const control of [code, generate, note]) control.disabled = locked;
      picker.setDisabled(locked);
      close.disabled = busy; save.disabled = locked; save.classList.toggle('is-busy', busy); spin.hidden = !busy; saveText.textContent = busy ? t('Saving…') : t('Add code');
    }
    async function reread() { const fresh = await read(); if (fresh.ok) state = fresh.data; return fresh.ok; }
    on(toggle, 'change', async () => {
      if (busy) return;
      const enabled = toggle.checked;
      busy = true; chosen = enabled; banner.hidden = true; paint();
      const result = await post(enabled ? 'EnableEvidenceCodes' : 'DisableEvidenceCodes', { EventVersion: state.version }, { [t('Require evidence codes')]: enabled ? '✓' : '—' });
      busy = false; chosen = null;
      if (result.kind === 'handler' && result.data?.succeeded) { changed = true; await reread(); layer.markClean(); ui.toast(result.data.message); }
      else if (result.kind !== 'session-lost') {
        await reread(); layer.markClean();
        banner.querySelector('.grow').textContent = result.kind === 'handler' ? result.data?.error || t('Couldn’t complete this. Nothing changed, and your entries are still here.') : result.kind === 'refused' ? result.reason || t('Couldn’t complete this. Nothing changed, and your entries are still here.') : t('Couldn’t confirm the save. Close and reopen to see the current codes before trying again.');
        banner.hidden = false;
      }
      paint();
    });
    on(generate, 'click', () => { const chars = 'ABCDEFGHJKLMNPQRSTUVWXYZ23456789'; const bytes = crypto.getRandomValues(new Uint8Array(6)); code.value = [...bytes].map(b => chars[b % chars.length]).join(''); showError(codeError, ''); });
    on(save, 'click', async () => {
      if (busy) return;
      banner.hidden = true;
      if (!state.codes.enabled) { banner.querySelector('.grow').textContent = t('Turn on evidence codes first.'); banner.hidden = false; return; }
      const missingCode = !code.value.trim();
      showError(codeError, missingCode ? t('Enter or generate a code.') : ''); code.classList.toggle('is-invalid', missingCode); code.setAttribute('aria-invalid', String(missingCode));
      let fromMessage = '';
      if (!picker.validate()) fromMessage = ' ';
      else {
        const converted = localToUtc(fromValue.value, timezone());
        fromMessage = converted.status === 'empty' ? t('Choose when it becomes active.')
          : converted.status === 'invalid' ? t('That local time does not exist because the clocks change at that time.')
          : converted.status === 'ambiguous' ? t('That local time is ambiguous because the clocks change at that time. Choose another time.')
          : state.evidenceCodes.some(x => Date.parse(x.activatesAt) === converted.ms) ? t('Another code already starts at that exact time.') : '';
      }
      showError(fromError, fromMessage.trim()); picker.setInvalid(!!fromMessage);
      if (missingCode) { code.focus(); return; }
      if (fromMessage) { date.focus(); return; }
      busy = true; paint();
      const draft = { [t('New code')]: code.value.trim(), [t('Active from')]: fmtLocal(localToUtc(fromValue.value, timezone()).ms), [t('Note')]: note.value };
      const result = await post('CreateEvidenceCode', { EventVersion: state.version, NewEvidenceCode: code.value.trim(), EvidenceCodeActivatesAtLocal: fromValue.value, EvidenceCodeNote: note.value.trim() }, draft);
      busy = false;
      if (result.kind === 'session-lost') { paint(); return; }
      if (result.kind === 'handler' && result.data?.succeeded) {
        changed = true; await reread();
        code.value = ''; note.value = ''; fromValue.value = state.codes.defaultFrom; picker.refresh?.(); layer.markClean();
        ui.toast(result.data.message); paint(); code.focus(); return;
      }
      if (result.kind === 'handler' && result.data?.outcome === 'invalid' && result.data.fieldErrors) {
        if (result.data.fieldErrors.code) showError(codeError, result.data.fieldErrors.code);
        if (result.data.fieldErrors.from) { showError(fromError, result.data.fieldErrors.from); picker.setInvalid(true); }
        if (!result.data.fieldErrors.code && !result.data.fieldErrors.from) { banner.querySelector('.grow').textContent = result.data.error; banner.hidden = false; }
        paint(); return;
      }
      // RC01 R2: no request key exists, so an unknown save is never retried blindly.
      banner.querySelector('.grow').textContent = result.kind === 'handler' && result.data?.outcome === 'refused' ? result.data.error || t('Couldn’t save the code. Nothing changed; your entries are still here.')
        : result.kind === 'refused' ? result.reason || t('Couldn’t complete this. Nothing changed, and your entries are still here.')
        : result.kind === 'handler' && result.data?.outcome === 'stale' ? t('Couldn’t save the code. Nothing changed; your entries are still here.')
        : t('Couldn’t confirm the save. Close and reopen to see the current codes before trying again.');
      if (result.kind === 'handler' && result.data?.outcome === 'stale') await reread();
      banner.hidden = false; paint();
    });
    on(close, 'click', () => { if (!busy) void ui.closeLayer(); });
    paint();
    toggle.focus();
  }

  /* ---------------- copy ---------------- */
  const copiedTimers = new Set();
  async function copy(button) {
    const ok = () => { const label = button.querySelector('[data-copy-label]'); label.textContent = t('Copied'); const timer = setTimeout(() => { copiedTimers.delete(timer); if (label.isConnected) label.textContent = t('Copy'); }, 2000); copiedTimers.add(timer); ui.toast(t('Link copied.')); };
    const fail = () => ui.toast(t('Couldn’t copy automatically. Select the link and copy it.'), { error: true });
    try { if (navigator.clipboard?.writeText) await navigator.clipboard.writeText(button.dataset.overviewCopy).then(ok, fail); else fail(); } catch { fail(); }
  }

  on(region, 'click', event => {
    const action = event.target.closest('[data-overview-action]');
    if (action && region.contains(action)) { event.preventDefault(); openAction(action.dataset.overviewAction, action); return; }
    const codeButton = event.target.closest('[data-overview-codes]');
    if (codeButton) { openCodes(codeButton); return; }
    const copyButton = event.target.closest('[data-overview-copy]');
    if (copyButton) void copy(copyButton);
  });
  on(region, 'submit', event => { if (event.target.matches('[data-overview-restore]')) event.preventDefault(); });
  const unregister = ui.registerDraft(region, { isDirty: () => false, isPending: () => !!dialog && ['busy', 'checking'].includes(dialog.d.status) });
  release = () => { life.abort(); unregister(); for (const timer of copiedTimers) clearTimeout(timer); if (dialog) void dialog.layer.close(true); if (codes) void codes.layer.close(true); dialog = null; codes = null; };
  if (focusAfterRefresh) { document.getElementById(focusAfterRefresh)?.focus(); focusAfterRefresh = null; }
}

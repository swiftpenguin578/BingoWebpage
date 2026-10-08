// U8 Review (Review.dc.html): the queue and the submission workspace of one event.
// Queue: server-owned search/status in the URL; results update in place through the shared update helper.
// Workspace: decisions answer in place (JSON outcome from the existing handlers). A definite refusal shows the
// server's reason; a stale decision shows the current record; a lost response re-reads through Readback
// (no-store, current state only) and never claims that this request did or did not save.
const RETURN_KEY = 'admin-review-return';
let release;
let carry = null; // state handed to the next init after an in-place refresh of the same submission

export function dispose() { release?.(); release = null; }

export function init(region, ui = window.AdminUI) {
  dispose();
  const queue = region.querySelector('[data-review-queue]');
  const workspace = region.querySelector('[data-review-workspace]');
  if (queue) release = initQueue(region, queue, ui);
  else if (workspace) release = initWorkspace(region, workspace, ui);
}

const remember = id => { try { sessionStorage.setItem(RETURN_KEY, id); } catch { /* per-viewer convenience only */ } };
const recall = () => { try { const id = sessionStorage.getItem(RETURN_KEY); sessionStorage.removeItem(RETURN_KEY); return id; } catch { return null; } };

/* ---------------- queue ---------------- */
function initQueue(region, root, ui) {
  const life = new AbortController(), signal = life.signal;
  const listen = (node, type, fn, options) => node?.addEventListener(type, fn, { signal, ...options });
  const search = root.querySelector('[data-review-search]');
  const results = root.querySelector('[data-review-results]');
  let query = new URL(root.dataset.queueUrl, location.href);
  let timer = 0;
  const importChildren = (target, source) => target.replaceChildren(...document.importNode(source, true).childNodes);
  const schema = values => Object.fromEntries(Object.keys(values).map(key => [key, { valid: () => true, default: '' }]));
  const setUrl = (url, record) => { const values = Object.fromEntries(new URL(url, location.href).searchParams); ui.setUrl(values, schema(values), { record }); };
  const paintSearch = () => {
    const has = search.value.length > 0;
    search.closest('.search').classList.toggle('has-clear', has);
    root.querySelector('[data-review-search-clear]').hidden = !has;
  };
  function patch(doc) {
    const fresh = doc.querySelector('[data-review-queue]');
    if (!fresh) throw new Error('Missing review queue');
    importChildren(results, fresh.querySelector('[data-review-results]'));
    for (const count of root.querySelectorAll('[data-review-count]')) count.textContent = fresh.querySelector(`[data-review-count="${count.dataset.reviewCount}"]`)?.textContent ?? count.textContent;
    importChildren(root.querySelector('[data-review-status-select]'), fresh.querySelector('[data-review-status-select]'));
    const pending = document.querySelector('[data-review-pending]'), freshPending = doc.querySelector('[data-review-pending]');
    if (pending && freshPending) pending.textContent = freshPending.textContent;
    root.dataset.queueUrl = fresh.dataset.queueUrl;
    query = new URL(fresh.dataset.queueUrl, location.href);
    paintStatus();
    return query.href;
  }
  const fragment = failed => {
    const content = document.querySelector(`template[data-page-${failed ? 'failure' : 'loading'}-template="review"]`)?.content;
    const nodes = document.createDocumentFragment();
    if (content) for (const node of content.querySelectorAll(failed ? '.empty' : '.sk-row')) nodes.append(document.importNode(node, true));
    return nodes;
  };
  function read(target, { record = true } = {}) {
    clearTimeout(timer);
    const url = new URL(target, location.href);
    query = new URL(url.href);
    paintStatus();
    if (record) setUrl(url.href, true);
    return ui.update(url.href, { root, results, patch, pending: () => fragment(false), failed: () => fragment(true), signal,
      fallbackFocus: () => search, scrollRegions: [results.querySelector('[data-review-wrap]')].filter(Boolean),
      draft: () => ({ [search.getAttribute('aria-label')]: search.value }) });
  }
  const withValue = (key, value) => { const url = new URL(query.href); if (value) url.searchParams.set(key, value); else url.searchParams.delete(key); return url.href; };
  function paintStatus() {
    const status = query.searchParams.get('status') || 'all';
    for (const input of root.querySelectorAll('[data-review-status]')) { input.checked = input.value === status; input.closest('.seg-opt').classList.toggle('is-on', input.checked); }
    const select = root.querySelector('[data-review-status-select]'); if (select) select.value = status;
  }
  listen(search, 'input', () => { paintSearch(); clearTimeout(timer); timer = setTimeout(() => void read(withValue('search', search.value.trim()), { record: false }), 250); });
  listen(search, 'keydown', event => {
    if (event.key === 'Escape' && search.value) { event.preventDefault(); search.value = ''; paintSearch(); void read(withValue('search', '')); }
    else if (event.key === 'Enter') { event.preventDefault(); void read(withValue('search', search.value.trim())); }
  });
  listen(root.querySelector('[data-review-search-clear]'), 'click', () => { search.value = ''; paintSearch(); search.focus(); void read(withValue('search', '')); });
  listen(root, 'change', event => {
    const control = event.target.closest('[data-review-status], [data-review-status-select]');
    if (!control) return;
    void read(withValue('status', control.value === 'all' ? '' : control.value));
  });
  listen(root, 'click', event => {
    if (event.target.closest('[data-review-clear-all]')) { search.value = ''; paintSearch(); const url = new URL(query.href); url.searchParams.delete('search'); url.searchParams.delete('status'); void read(url.href).then(() => search.focus()); return; }
    const row = event.target.closest('[data-review-row]');
    if (!row || event.target.closest('a,button,input,select')) return;
    row.querySelector('[data-review-open]')?.click();
  });
  const unregisterUrl = ui.registerUrlState(async next => {
    const url = new URL(next);
    if (!/^\/admin\/review(\/index)?$/i.test(url.pathname) || url.searchParams.get('eventId') !== query.searchParams.get('eventId')) return false;
    search.value = url.searchParams.get('search') || ''; paintSearch();
    await read(url.href, { record: false });
    return true;
  });
  // Returning from a submission: focus its row and flash it once.
  const returned = recall();
  if (returned) {
    const link = root.querySelector(`#open-${CSS.escape(returned)}`);
    if (link) { link.focus({ preventScroll: false }); const row = link.closest('[data-review-row]'); row.classList.add('is-flash'); listen(row, 'animationend', () => row.classList.remove('is-flash'), { once: true }); }
  }
  return () => { life.abort(); clearTimeout(timer); unregisterUrl(); };
}

/* ---------------- workspace ---------------- */
function initWorkspace(region, root, ui) {
  const life = new AbortController(), signal = life.signal;
  const listen = (node, type, fn, options) => node?.addEventListener(type, fn, { signal, ...options });
  const data = root.dataset, id = data.submissionId;
  const decide = root.querySelector('[data-review-decide]');
  const panel = decide.querySelector('[data-review-panel]');
  const notice = root.querySelector('[data-review-notice]');
  const decideError = decide.querySelector('[data-review-decide-error]');
  const correction = root.querySelector('[data-review-form="correct"]');
  const format = (template, ...values) => values.reduce((text, value, index) => text.replaceAll(`{${index}}`, value), template || '');
  let mode = 'idle', pending = false, unsure = null, layer = null;
  const reasonOf = name => (name === 'reverse' ? layer?.element : root).querySelector(`[data-review-form="${name}"] [data-review-reason]`);

  const viewerRelease = initViewer(root.querySelector('[data-review-viewer]'), signal);

  /* disclosures (History, Earlier approvals, Files) */
  listen(root, 'click', event => {
    const button = event.target.closest('[data-review-disclosure]');
    if (!button) return;
    const open = button.getAttribute('aria-expanded') !== 'true';
    button.setAttribute('aria-expanded', String(open)); button.classList.toggle('is-open', open);
    document.getElementById(button.getAttribute('aria-controls')).hidden = !open;
  });
  for (const link of root.querySelectorAll('[data-review-back]')) listen(link, 'click', () => remember(id));
  for (const link of document.querySelectorAll('#back-btn')) listen(link, 'click', () => remember(id));

  /* banners */
  function banner(host, kind, title, text, extra) {
    const node = ui.template(kind === 'error' ? 'banner-error' : kind === 'info' ? 'banner-info' : kind === 'uncertain' ? 'banner-uncertain' : 'banner-warning').firstElementChild;
    const copy = node.querySelector('[data-component-text]') || node.querySelector('.grow');
    copy.replaceChildren();
    const strong = document.createElement('b'); strong.textContent = title;
    copy.append(strong, text ? ' ' + text : '');
    if (extra) copy.append(extra);
    node.tabIndex = -1; node.setAttribute('role', 'alert');
    host.replaceChildren(node);
    return node;
  }
  const clearBanners = () => { notice.replaceChildren(); decideError.replaceChildren(); };

  /* modes */
  const correctionBaseline = () => correction ? JSON.stringify([correction.querySelector('[data-cf-req]').value, correction.querySelector('[data-cf-drop]').value, correction.querySelector('[data-cf-acct]').value]) : '';
  let baseline = correctionBaseline();
  const changed = () => correction && correctionBaseline() !== baseline;
  const dirty = () => (mode === 'reject' && !!reasonOf('reject')?.value.trim()) || (mode === 'correct' && (!!reasonOf('correct')?.value.trim() || changed()));
  function setMode(next, { focus = true } = {}) {
    mode = next;
    for (const part of panel.querySelectorAll('[data-review-mode]')) part.hidden = part.dataset.reviewMode !== next;
    if (correction) correction.hidden = next !== 'correct';
    decideError.replaceChildren();
    if (next === 'correct') { void loadCharacters(); syncCorrection(); }
    if (!focus) return;
    if (next === 'reject') reasonOf('reject')?.focus();
    else if (next === 'correct') correction.querySelector('[data-cf-req]').focus();
    else panel.querySelector(next === 'idle' ? '#dec-correct' : 'button')?.focus();
  }
  async function discardThen(action) {
    if (dirty() && !await discardDialog()) return;
    for (const name of ['reject', 'correct']) { const field = reasonOf(name); if (field) { field.value = ''; paintReason(field); } }
    if (correction) { correction.reset(); baseline = correctionBaseline(); }
    action();
  }
  const discardDialog = () => ui.confirm({ title: document.body.dataset.discardTitle, description: document.body.dataset.discardDescription, actionLabel: document.body.dataset.discard, cancelLabel: document.body.dataset.keepEditing, actionClass: 'btn-danger' });
  listen(panel, 'click', event => {
    const start = event.target.closest('[data-review-start]');
    if (start && !pending) { setMode(start.dataset.reviewStart); return; }
    if (event.target.closest('[data-review-cancel]') && !pending) void discardThen(() => setMode('idle'));
  });

  /* reason fields: counter over 3,600 characters, inline errors */
  function paintReason(field, message = '') {
    const box = field.closest('.field');
    const count = box.querySelector('[data-reason-count]'), error = box.querySelector('[data-reason-err]');
    const length = field.value.length;
    if (count) { count.hidden = length <= 3600; count.textContent = `${length.toLocaleString(document.documentElement.lang)} / ${(4000).toLocaleString(document.documentElement.lang)}`; count.classList.toggle('is-over', length > 4000); }
    error.hidden = !message; error.querySelector('[data-component-text]').textContent = message;
    field.classList.toggle('is-invalid', !!message); field.setAttribute('aria-invalid', String(!!message));
  }
  listen(root, 'input', event => { if (event.target.matches('[data-review-reason]')) paintReason(event.target); });
  function reasonError(field, required) {
    const value = field.value.trim();
    if (!value) return required;
    if (value.length > 4000) return data.textReasonLong;
    return '';
  }

  /* correction picker (AU17a): accounts with Released / Left team / Current markers */
  let characters = null;
  async function loadCharacters() {
    if (characters || !correction) return;
    const select = correction.querySelector('[data-cf-acct]'), hint = correction.querySelector('[data-cf-acct-hint]');
    const outcome = await window.AdminFetch.request(data.charactersUrl, { cache: 'no-store', draft: draftValues(), readback: true });
    if (outcome.kind !== 'handler' || !Array.isArray(outcome.data)) { hint.textContent = data.textAccountsFailed; return; }
    characters = outcome.data;
    const current = select.value;
    select.replaceChildren(...characters.map(item => {
      const option = document.createElement('option');
      option.value = item.characterId;
      const marker = item.released ? data.textReleased : item.leftTeam ? data.textLeft : data.textCurrent;
      option.textContent = `${item.characterName} · ${marker}`;
      option.selected = item.characterId === current;
      return option;
    }));
    if (!characters.some(item => item.characterId === current)) { const option = document.createElement('option'); option.value = current; option.textContent = root.dataset.account; option.selected = true; select.prepend(option); }
    baseline = correctionBaseline();
    ui.refreshDirty?.();
  }
  function syncCorrection(event) {
    if (!correction) return;
    const req = correction.querySelector('[data-cf-req]'), drop = correction.querySelector('[data-cf-drop]');
    const option = req.selectedOptions[0];
    correction.querySelector('[data-cf-tile]').value = option.dataset.tile;
    const manual = option.dataset.manual === 'true';
    correction.querySelector('[data-cf-drop-field]').hidden = manual;
    const choices = [...drop.options].filter(item => item.value);
    for (const item of choices) item.hidden = item.disabled = item.dataset.requirement !== req.value;
    if (manual) drop.value = '';
    else if (event?.target === req) { const own = choices.filter(item => item.dataset.requirement === req.value); drop.value = own.length === 1 ? own[0].value : ''; }
    const weight = manual ? Number(option.dataset.weight) : drop.selectedOptions[0]?.dataset.weight ? Number(drop.selectedOptions[0].dataset.weight) : null;
    const current = Number(data.weight);
    correction.querySelector('[data-cf-weight]').textContent = weight == null ? data.textChooseDropWeight
      : `${weight === current ? format(data.textSameWeight, weight) : format(data.textNewWeight, weight, current)} ${data.textWeightSource}`;
  }
  listen(correction, 'change', event => { if (event.target.matches('[data-cf-req], [data-cf-drop]')) syncCorrection(event); });

  /* reverse approval: a confirmation layer with a written reason (never closes on an outside click) */
  listen(panel, 'click', event => {
    if (!event.target.closest('[data-review-reverse]') || pending) return;
    const content = panel.querySelector('[data-review-reverse-template]').content.cloneNode(true);
    const opener = event.target.closest('button');
    const created = ui.openLayer({ title: content.querySelector('[data-confirm-title]').textContent, content, confirmation: true, opener,
      dirty: () => !!layer?.element.querySelector('[data-review-reason]')?.value.trim(), pending: () => pending, onClose: () => { layer = null; } });
    layer = created;
    created.element.querySelector('[data-review-dialog-cancel]').addEventListener('click', () => void created.close(false));
    created.element.querySelector('form').addEventListener('submit', submit);
    created.element.querySelector('[data-review-reason]').focus();
  });

  /* draft registration: unsaved reasons and uncertain outcomes guard navigation */
  const draftValues = () => {
    const values = {};
    for (const name of ['reject', 'correct', 'reverse']) { const field = reasonOf(name); if (field?.value.trim()) values[data.textReasonLabel] = field.value; }
    return values;
  };
  const unregisterDraft = ui.registerDraft(root, {
    isDirty: () => dirty() || !!unsure,
    isPending: () => pending,
    discard: () => {},
    confirmLeave: async () => {
      if (unsure) {
        // Decision B: only the explicit Leave button leaves; Escape keeps the submission and the uncertainty.
        const choice = await ui.confirm({ title: data.textLeaveTitle, description: data.textLeaveBody, actionLabel: data.textCheck, cancelLabel: data.textLeave, cancelClass: 'btn-outline-danger', cancelResult: 'leave' });
        if (choice === 'leave') { unsure = null; return true; }
        if (choice === true) void check();
        return false;
      }
      return discardDialog();
    }
  });

  /* decisions */
  const kindOf = form => form.dataset.reviewForm;
  const what = kind => data[`what${kind[0].toUpperCase()}${kind.slice(1)}`];
  function validate(form) {
    const kind = kindOf(form);
    if (kind === 'reject' || kind === 'reverse' || kind === 'correct') {
      const field = form.querySelector('[data-review-reason]');
      const message = reasonError(field, kind === 'reject' ? data.textReasonRequired : kind === 'reverse' ? data.textReverseReason : data.textCorrectReasonRequired);
      paintReason(field, message);
      if (kind === 'correct') {
        const drop = form.querySelector('[data-cf-drop]'), manual = form.querySelector('[data-cf-req]').selectedOptions[0].dataset.manual === 'true';
        const dropError = !manual && !drop.value ? data.textChooseDrop : '';
        const box = form.querySelector('[data-cf-drop-err]'); box.hidden = !dropError; box.querySelector('[data-component-text]').textContent = dropError;
        drop.classList.toggle('is-invalid', !!dropError); drop.setAttribute('aria-invalid', String(!!dropError));
        const formError = !changed() ? data.textNoop : '';
        const errorBox = panel.querySelector('[data-cf-form-err]'); errorBox.hidden = !formError; errorBox.querySelector('[data-component-text]').textContent = formError;
        const first = dropError ? drop : message ? field : null;
        if (first) { first.focus(); return false; }
        if (formError) { errorBox.focus?.(); return false; }
        return true;
      }
      if (message) { field.focus(); return false; }
    }
    return true;
  }
  function setBusy(form, on) {
    const submitter = form.id === 'cf-card' ? panel.querySelector('#cf-save') : form.querySelector('[type="submit"]');
    for (const control of [...decide.querySelectorAll('button, textarea, select'), ...(correction ? correction.querySelectorAll('select, textarea') : []), ...(layer ? layer.element.querySelectorAll('button, textarea') : [])]) {
      if (on) { control.dataset.wasDisabled = control.disabled ? 'true' : ''; control.disabled = true; }
      else { control.disabled = control.dataset.wasDisabled === 'true'; delete control.dataset.wasDisabled; }
    }
    decide.setAttribute('aria-busy', String(on));
    if (submitter) {
      if (on) { submitter.dataset.label = submitter.textContent; submitter.classList.add('is-busy'); submitter.textContent = submitter.dataset.busyLabel || submitter.textContent; }
      else if (submitter.dataset.label != null) { submitter.classList.remove('is-busy'); submitter.textContent = submitter.dataset.label; delete submitter.dataset.label; }
    }
  }
  async function submit(event) {
    const form = event.target.closest('form');
    if (!form?.dataset.reviewForm) return;
    event.preventDefault();
    if (pending) return;
    if (unsure) { notice.querySelector('[data-review-check]')?.focus(); return; }
    if (!validate(form)) return;
    const kind = kindOf(form);
    const body = new FormData(form);
    if (!body.get('__RequestVerificationToken')) { const token = document.querySelector('[data-shell-antiforgery] input[name="__RequestVerificationToken"]')?.value; if (token) body.set('__RequestVerificationToken', token); }
    const before = { version: data.version, status: data.status };
    pending = true; ui.refreshDirty?.(); clearBanners(); setBusy(form, true);
    let outcome;
    try { outcome = await ui.busy(() => window.AdminFetch.request(form.action, { method: 'POST', body, draft: draftValues() })); }
    finally { pending = false; setBusy(form, false); ui.refreshDirty?.(); }
    if (!root.isConnected) return;
    if (outcome.kind === 'session-lost') return; // The shared notice shows what wasn't saved; the reason stays.
    if (outcome.kind !== 'handler' || !outcome.data?.outcome) { await closeDialog(); setUnsure(kind, before); return; }
    const result = outcome.data;
    if (result.outcome === 'saved') { await closeDialog(); refresh({ saved: kind, amount: result.amount }); return; }
    if (result.outcome === 'stale') {
      await closeDialog();
      const keep = result.status === 'Pending' && (mode === 'reject' || mode === 'correct') ? { mode, reason: reasonOf(mode)?.value || '' } : null;
      refresh({ stale: { by: result.changedBy, message: result.message }, keep });
      return;
    }
    const target = kind === 'reverse' && layer ? layer.element.querySelector('[data-review-dialog-error]') : decideError;
    if (result.outcome === 'blocked') {
      const link = document.createElement('a'); link.className = 'banner-btn rv-warn-link'; link.href = result.blockingUrl; link.dataset.shellLink = ''; link.textContent = data.textOpenEarlier;
      banner(target, 'error', format(data.textCouldnt, what(kind)), result.message, link).focus();
      return;
    }
    banner(target, 'error', format(data.textCouldnt, what(kind)), result.message).focus();
  }
  async function closeDialog() { if (layer) await layer.close(true); }
  listen(root, 'submit', submit);

  /* uncertain outcome: Check status reads the current state; nothing is assumed */
  function setUnsure(kind, before) {
    unsure = { kind, before };
    ui.refreshDirty?.();
    const button = document.createElement('button');
    button.type = 'button'; button.className = 'btn btn-sm'; button.dataset.reviewCheck = ''; button.textContent = data.textCheck;
    button.addEventListener('click', () => void check());
    banner(notice, 'uncertain', format(data.textUnsure, what(kind)), data.textUnsureSub, button).focus();
  }
  async function check() {
    if (!unsure || pending) return;
    const button = notice.querySelector('[data-review-check]');
    if (button) { button.disabled = true; button.textContent = data.textChecking; }
    pending = true;
    const outcome = await ui.busy(() => window.AdminFetch.request(data.readbackUrl, { cache: 'no-store', readback: true, draft: draftValues() }), true);
    pending = false;
    if (!root.isConnected) return;
    const state = outcome.kind === 'handler' ? outcome.data?.state : null;
    if (!state) {
      if (button) { button.disabled = false; button.textContent = data.textCheck; }
      const text = notice.querySelector('.grow'); if (text && outcome.kind !== 'session-lost') { const strong = text.querySelector('b'); text.replaceChildren(strong, ' ' + data.textStillUnsure, button); }
      return;
    }
    const { before } = unsure;
    unsure = null; ui.refreshDirty?.();
    if (String(state.version) === String(before.version) && state.status === before.status) {
      banner(notice, 'info', data.textNotSaved, data.textNotSavedSub).focus();
      return;
    }
    refresh({ now: state.status });
  }

  /* in-place refresh: the current record replaces the page; the outcome is shown on the fresh page */
  function refresh(next) {
    carry = { id, ...next };
    void ui.navigate(location.href, { mode: 'replace', check: false });
  }
  function applyCarry() {
    if (!carry || carry.id !== id) { carry = null; return; }
    const state = carry; carry = null;
    if (state.saved === 'correct') { ui.toast(data.textCorrected); panel.querySelector('#dec-correct')?.focus(); return; }
    if (state.saved) {
      const result = decide.querySelector('[data-review-result]');
      const title = { approve: data.textApproved, reject: data.textRejected, reverse: data.textReversed }[state.saved];
      const sub = { approve: format(data.textApprovedSub, state.amount ?? ''), reject: data.textRejectedSub, reverse: format(data.textReversedSub, state.amount ?? '') }[state.saved];
      result.querySelector('[data-review-result-title]').textContent = title;
      result.querySelector('[data-review-result-sub]').textContent = sub;
      result.querySelector('[data-review-result-box]').classList.toggle('is-success', state.saved === 'approve');
      result.hidden = false; panel.hidden = true;
      result.querySelector('#dec-result').focus();
      return;
    }
    if (state.stale) {
      banner(notice, 'warning', state.stale.by ? format(data.textStaleBy, '@' + state.stale.by) : data.textStale, data.textStaleSub).focus();
      if (state.keep && panel.querySelector(`[data-review-mode="${state.keep.mode}"]`)) {
        setMode(state.keep.mode, { focus: false });
        const field = reasonOf(state.keep.mode); if (field) { field.value = state.keep.reason; paintReason(field); }
      }
      return;
    }
    if (state.now) banner(notice, 'info', format(data.textNow, state.now), data.textNowSub).focus();
  }

  setMode('idle', { focus: false });
  applyCarry();
  return () => { life.abort(); unregisterDraft(); viewerRelease(); };
}

/* ---------------- screenshot viewer ----------------
   Same gesture model as public-evidence.js (pointer drag, two-pointer pinch, wheel, keyboard), adapted to the
   inline viewer: Fit, actual size (100%), zoom 'fit or 100%, whichever is smaller' to 400%. */
function initViewer(section, signal) {
  if (!section?.dataset.src) return () => {};
  const stage = section.querySelector('[data-iv-stage]'), image = section.querySelector('[data-iv-img]');
  const loading = section.querySelector('[data-iv-loading]'), error = section.querySelector('[data-iv-error]');
  const pct = section.querySelector('[data-iv-pct]');
  const control = name => section.querySelector(`[data-iv="${name}"]`);
  const natural = { w: Number(section.dataset.width) || 1, h: Number(section.dataset.height) || 1 };
  const MAX = 4;
  let view = { s: 1, x: 0, y: 0, fit: true }, ready = false, frame = 0;
  const box = () => { const rect = stage.getBoundingClientRect(); return { w: rect.width, h: rect.height }; };
  const fitScale = () => { const st = box(); return Math.min(st.w / natural.w, st.h / natural.h, 2); };
  const clampPos = (s, x, y) => {
    const st = box(), w = natural.w * s, h = natural.h * s;
    return { x: w <= st.w ? (st.w - w) / 2 : Math.min(0, Math.max(st.w - w, x)), y: h <= st.h ? (st.h - h) / 2 : Math.min(0, Math.max(st.h - h, y)) };
  };
  function paint(anim = false) {
    image.classList.toggle('is-anim', anim && !matchMedia('(prefers-reduced-motion: reduce)').matches);
    image.style.width = natural.w + 'px'; image.style.height = natural.h + 'px';
    image.style.transform = `translate(${Math.round(view.x * 10) / 10}px, ${Math.round(view.y * 10) / 10}px) scale(${view.s})`;
    const fs = fitScale();
    stage.classList.toggle('is-zoomed', view.s > fs + 0.001);
    pct.textContent = Math.round(view.s * 100) + '%';
    pct.setAttribute('aria-label', pct.textContent);
    control('out').disabled = !ready || view.s <= Math.min(fs, 1) + 0.001;
    control('in').disabled = !ready || view.s >= MAX - 0.001;
    for (const name of ['fit', 'one']) control(name).disabled = !ready;
    const fitOn = ready && view.fit, oneOn = ready && Math.abs(view.s - 1) < 0.001;
    control('fit').setAttribute('aria-pressed', String(fitOn)); control('fit').classList.toggle('is-on', fitOn);
    control('one').setAttribute('aria-pressed', String(oneOn)); control('one').classList.toggle('is-on', oneOn);
  }
  const set = (s, x, y, fit, anim) => { const p = clampPos(s, x, y); view = { s, x: p.x, y: p.y, fit: !!fit }; paint(anim); };
  const fit = anim => { if (ready) set(fitScale(), 0, 0, true, anim); };
  const zoomAt = (next, px, py, anim) => {
    if (!ready) return;
    next = Math.max(Math.min(fitScale(), 1), Math.min(MAX, next));
    if (Math.abs(next - view.s) < 0.001) return;
    set(next, px - (px - view.x) * (next / view.s), py - (py - view.y) * (next / view.s), Math.abs(next - fitScale()) < 0.001, anim);
  };
  const zoomBy = factor => { const st = box(); zoomAt(view.s * factor, st.w / 2, st.h / 2, true); };
  const pan = (dx, dy) => { if (ready) set(view.s, view.x + dx, view.y + dy, view.fit, true); };
  function load() {
    ready = false; image.hidden = true; error.hidden = true; loading.hidden = false; paint();
    const probe = new Image();
    probe.onload = () => {
      if (signal.aborted) return;
      natural.w = probe.naturalWidth || natural.w; natural.h = probe.naturalHeight || natural.h;
      image.style.setProperty('--art', `url("${section.dataset.src}")`);
      ready = true; loading.hidden = true; image.hidden = false; fit(false);
    };
    probe.onerror = () => { if (signal.aborted) return; loading.hidden = true; error.hidden = false; paint(); };
    probe.src = section.dataset.src + (load.retried ? `${section.dataset.src.includes('?') ? '&' : '?'}retry=${Date.now()}` : '');
  }
  section.addEventListener('click', event => {
    const button = event.target.closest('[data-iv]');
    if (!button) return;
    const action = button.dataset.iv;
    if (action === 'in') zoomBy(1.25); else if (action === 'out') zoomBy(0.8); else if (action === 'fit') fit(true);
    else if (action === 'one') { const st = box(); zoomAt(1, st.w / 2, st.h / 2, true); }
    else if (action === 'retry') { load.retried = true; load(); }
  }, { signal });
  stage.addEventListener('keydown', event => {
    if (!ready) return;
    const k = event.key, step = 60;
    const handled = { '+': () => zoomBy(1.25), '=': () => zoomBy(1.25), '-': () => zoomBy(0.8), '_': () => zoomBy(0.8), '0': () => fit(true), '1': () => { const st = box(); zoomAt(1, st.w / 2, st.h / 2, true); },
      ArrowLeft: () => pan(step, 0), ArrowRight: () => pan(-step, 0), ArrowUp: () => pan(0, step), ArrowDown: () => pan(0, -step) }[k];
    if (handled) { event.preventDefault(); handled(); }
  }, { signal });
  stage.addEventListener('wheel', event => {
    if (!ready) return;
    event.preventDefault();
    const rect = stage.getBoundingClientRect();
    zoomAt(view.s * (event.deltaY < 0 ? 1.15 : 1 / 1.15), event.clientX - rect.left, event.clientY - rect.top, false);
  }, { passive: false, signal });
  const pointers = new Map();
  let drag = null, pinch = null;
  const distance = () => { const [a, b] = [...pointers.values()]; return a && b ? Math.hypot(a.x - b.x, a.y - b.y) : 0; };
  stage.addEventListener('pointerdown', event => {
    if (!ready || event.button > 0) return;
    pointers.set(event.pointerId, { x: event.clientX, y: event.clientY });
    try { stage.setPointerCapture(event.pointerId); } catch { /* capture is optional */ }
    if (pointers.size === 2) { pinch = { d: distance(), s: view.s }; drag = null; }
    else drag = { x: event.clientX, y: event.clientY, ox: view.x, oy: view.y, moved: false };
  }, { signal });
  stage.addEventListener('pointermove', event => {
    if (!pointers.has(event.pointerId)) return;
    pointers.set(event.pointerId, { x: event.clientX, y: event.clientY });
    if (pinch && pointers.size > 1) {
      const rect = stage.getBoundingClientRect(), [a, b] = [...pointers.values()];
      zoomAt(pinch.s * (distance() / Math.max(1, pinch.d)), (a.x + b.x) / 2 - rect.left, (a.y + b.y) / 2 - rect.top, false);
    } else if (drag) {
      const dx = event.clientX - drag.x, dy = event.clientY - drag.y;
      if (Math.abs(dx) + Math.abs(dy) > 3) drag.moved = true;
      if (drag.moved) { cancelAnimationFrame(frame); frame = requestAnimationFrame(() => set(view.s, drag ? drag.ox + dx : view.x, drag ? drag.oy + dy : view.y, false, false)); }
    }
  }, { signal });
  const end = event => { pointers.delete(event.pointerId); if (pointers.size < 2) pinch = null; if (pointers.size === 0) drag = null; };
  stage.addEventListener('pointerup', end, { signal });
  stage.addEventListener('pointercancel', end, { signal });
  stage.addEventListener('dblclick', event => {
    if (!ready) return;
    const rect = stage.getBoundingClientRect();
    if (view.fit || view.s < 1) zoomAt(Math.max(1, fitScale() * 2), event.clientX - rect.left, event.clientY - rect.top, true); else fit(true);
  }, { signal });
  const observer = new ResizeObserver(() => { if (!ready) return; if (view.fit) fit(false); else set(view.s, view.x, view.y, false, false); });
  observer.observe(stage);
  load();
  return () => { observer.disconnect(); cancelAnimationFrame(frame); };
}

// Identity's page module: lifecycle owned by the shared shell, transport by AdminFetch.
const fields = ['Name', 'Description', 'BuyInDescription', 'Timezone'];
const key = field => field[0].toLowerCase() + field.slice(1);
// Match .NET String.Trim (including U+0085, excluding U+FEFF).
const trim = value => value.replace(/^[\u0009-\u000d\u0020\u0085\u00a0\u1680\u2000-\u200a\u2028\u2029\u202f\u205f\u3000]+|[\u0009-\u000d\u0020\u0085\u00a0\u1680\u2000-\u200a\u2028\u2029\u202f\u205f\u3000]+$/g, '');
const canonical = (field, value) => { const result = trim(value ?? ''); return field === 'Description' || field === 'BuyInDescription' ? result || null : result; };
const wire = value => value.replace(/\r\n|\r|\n/g, '\r\n');
export function createIdentityReadbackSession(data, action, eventId, version, labels = {}) {
  const yes = name => String(data.get(name)).toLowerCase() === 'true';
  const reviewed = yes('Input.HasReviewedValues'), baseline = yes('Input.HasBaseline');
  const expected = {}, draft = {};
  const basis = Number(version ?? data.get('Input.Version'));
  let complete = Boolean(eventId) && Number.isSafeInteger(basis) && basis >= 0;
  for (const field of fields) {
    const get = (prefix, submitted = false) => {
      const value = data.get(`Input.${prefix}${field}`);
      if (typeof value !== 'string') { complete = false; return null; }
      return canonical(field, submitted ? wire(value) : value);
    };
    const proposed = get('', true), original = baseline ? get('Original', true) : proposed;
    const observed = reviewed ? get('Reviewed') : baseline ? get('Original') : proposed;
    const choice = data.get(`Input.${field}Resolution`);
    if (choice && !['None', 'KeepMine', 'UseCurrent', '0', '1', '2'].includes(choice)) complete = false;
    if (['UseCurrent', '2'].includes(choice) && !reviewed) complete = false;
    expected[key(field)] = proposed === original || ['UseCurrent', '2'].includes(choice) ? observed : proposed;
    draft[field] = data.get(`Input.${field}`);
  }
  Object.freeze(expected); Object.freeze(draft);
  const url = new URL(action, location.href); url.searchParams.set('handler', 'Current');
  const unknown = outcome => ({ state: 'unknown', expected, outcome });
  return Object.freeze({ expected, version: basis, draft, async checkAgain(signal) {
    if (!complete) return unknown();
    const outcome = await window.AdminFetch.request(url.href, { cache: 'no-store', signal, draft, labels, readback: true });
    if (outcome.kind !== 'handler') return unknown(outcome);
    const current = outcome.data;
    if (current?.eventId !== eventId || !current.values || !Number.isSafeInteger(current.version) || current.version < basis) return unknown();
    for (const field of fields) {
      const value = current.values[key(field)];
      if (typeof value !== 'string' && !((field === 'Description' || field === 'BuyInDescription') && value === null)) return unknown();
    }
    const state = current.version === basis ? 'notApplied' : Object.keys(expected).every(name => expected[name] === current.values[name]) ? 'upToDate' : 'different';
    return { state, expected, current };
  } });
}

let release = null;
export function dispose() { const cleanup = release; release = null; return cleanup?.(); }
export async function init(region, ui = window.AdminUI) {
  await dispose();
  const root = region.matches?.('[data-identity-editor]') ? region : region.querySelector('[data-identity-editor]');
  const form = root?.querySelector('form');
  if (!form) return;
  const lifetime = new AbortController(), request = new AbortController();
  const text = name => root.dataset[name] || '';
  const get = name => form.elements.namedItem(`Input.${name}`);
  const value = name => get(name)?.value ?? '';
  const set = (name, content) => { let input = get(name); if (!input) { input = document.createElement('input'); input.type = 'hidden'; input.name = `Input.${name}`; form.append(input); } input.value = content ?? ''; };
  const listen = (element, type, handler) => element?.addEventListener(type, handler, { signal: lifetime.signal });
  let pending = false, uncertain = null, layer = null, retained = value('HasReviewedValues').toLowerCase() === 'true' || root.dataset.identityReviewRequired === 'true';
  const save = root.querySelector('[data-identity-save]'), state = root.querySelector('[data-identity-state]'), feedback = root.querySelector('[data-identity-feedback]');
  let cleanNote = state?.querySelector('[data-component-text]')?.textContent || state?.textContent.trim() || '';
  let copyTimer;
  const labels = Object.fromEntries(fields.map(field => [field, text(`label${field}`)]));
  const editable = root.dataset.identityEditable !== 'false';
  const snapshot = () => JSON.stringify(fields.map(field => value(field)));
  let baseline = snapshot();
  const changed = () => fields.some(field => canonical(field, wire(value(field))) !== canonical(field, wire(value(`${value('HasReviewedValues').toLowerCase() === 'true' ? 'Reviewed' : 'Original'}${field}`))));
  const dirty = () => retained || uncertain !== null || snapshot() !== baseline;
  const report = (message, tone = 'is-warning', kind = 'warning', lead = '') => {
    if (!feedback) return;
    const component = ui.template(`banner-${kind}`).firstElementChild;
    component.querySelector('[data-component-text]').textContent = message;
    const heading = component.querySelector('[data-component-lead]'); heading.textContent = lead; heading.hidden = !lead;
    feedback.replaceChildren(...component.childNodes); feedback.className = `banner ${tone}`;
    feedback.setAttribute('role', tone === 'is-info' ? 'status' : 'alert'); feedback.hidden = !message && !lead;
  };
  function staleReport() {
    const conflicts = root.querySelector('[data-conflict-for]:not([hidden]) [data-use-theirs]:not([hidden])');
    const message = root.dataset.identityScheduleStale === 'true' ? text('staleSchedule') : conflicts ? text('stale') : changed() ? text('staleMerged') : text('staleClean');
    report(message + (needsTimezoneReview() ? text('staleReview') : ''), 'is-warning', 'warning', text('staleLead'));
  }
  function paint() {
    for (const field of fields) {
      const control = get(field);
      if (control && control.type !== 'hidden') control.disabled = pending || !!uncertain;
    }
    if (save) {
      save.disabled = pending; save.setAttribute('aria-disabled', String(!pending && !uncertain && !changed()));
      save.title = !uncertain && !changed() ? text('noChanges') : '';
      save.querySelector('[data-component-text]').textContent = pending ? text(uncertain ? 'checking' : 'saving') : text(uncertain ? 'checkAgain' : 'save');
      save.querySelector('.spin').hidden = !pending; save.classList.toggle('is-busy', pending); save.setAttribute('aria-busy', String(pending));
    }
    if (state) { state.hidden = pending || changed() || !!uncertain || !cleanNote; state.querySelector('[data-component-text]').textContent = cleanNote; }
    const dirtyNote = root.querySelector('[data-identity-dirty]'); if (dirtyNote) dirtyNote.hidden = pending || (!changed() && !uncertain);
    const note = root.querySelector('[data-timezone-note]');
    if (note) { note.hidden = !(get('Timezone') instanceof HTMLSelectElement) || value('Timezone') === value('TimezoneConfirmationOriginal'); note.querySelector('[data-component-text]').textContent = root.dataset.identityPublic === 'true' ? text('timezonePublicNote') : text('timezonePrivateNote').replace('{0}', value('Timezone')); }
    if (layer) {
      layer.element.setAttribute('aria-busy', String(pending));
      layer.element.querySelectorAll('button').forEach(button => { button.disabled = pending; });
      const confirm = layer.element.querySelector('[data-identity-review-confirm]');
      if (confirm) { confirm.classList.toggle('is-busy', pending); confirm.querySelector('.spin').hidden = !pending; confirm.querySelector('[data-component-text]').textContent = pending ? text('saving') : text('timezoneAction').replace('{0}', value('Timezone')); }
    }
    for (const field of fields) {
      const input = get(field); if (!input || input.type === 'hidden') continue;
      const ids = [`error-${field}`, `conflict-${field}`, `hint-${field}`, `note-${field}`].filter(id => {
        const element = root.querySelector(`#${id}`); return element && !element.hidden && !element.classList.contains('field-validation-valid');
      });
      if (ids.length) input.setAttribute('aria-describedby', ids.join(' ')); else input.removeAttribute('aria-describedby');
    }
    ui.refreshDirty();
  }
  function conflict(field, theirs) {
    const note = root.querySelector(`[data-conflict-for="${field}"]`);
    if (!note) return;
    note.replaceChildren(); note.hidden = false;
    const contents = ui.template('field-note').firstElementChild;
    contents.querySelector('[data-component-text]').textContent = (field === 'Timezone' ? text('timezoneTheirs') : text('theirs')).replace('{0}', field === 'Timezone' ? theirs : theirs ? '“' + (theirs.length > 140 ? theirs.slice(0, 140) + '…' : theirs) + '”' : text('emptyQuote'));
    const use = contents.querySelector('[data-use-theirs]'); use.textContent = text('useTheirs'); use.dataset.useTheirs = field;
    note.append(...contents.childNodes);
    set(`${field}Resolution`, 'KeepMine');
    listen(use, 'click', () => { set(field, theirs); set(`${field}Resolution`, 'UseCurrent'); note.hidden = true; clearFieldError(field); validate(); paint(); });
  }
  function mergedNote(field) {
    const note = root.querySelector(`[data-conflict-for="${field}"]`); if (!note) return;
    const component = ui.template('field-note').firstElementChild;
    component.querySelector('[data-component-text]').textContent = text('merged'); component.querySelector('[data-use-theirs]').hidden = true;
    note.replaceChildren(...component.childNodes); note.hidden = false;
  }
  function mergeCurrent(current) {
    for (const field of fields) {
      const theirs = current.values[key(field)], original = canonical(field, wire(value(`Original${field}`))), mine = canonical(field, wire(value(field)));
      set(`Reviewed${field}`, theirs);
      set(`${field}Resolution`, 'None');
      const note = root.querySelector(`[data-conflict-for="${field}"]`); if (note) note.hidden = true;
      if (mine === original) { set(field, theirs); if (canonical(field, theirs) !== original) mergedNote(field); }
      else if (canonical(field, theirs) !== original && canonical(field, theirs) !== mine) conflict(field, theirs);
    }
    set('HasReviewedValues', 'true'); root.dataset.identityCurrentVersion = String(current.version);
    retained = true; staleReport();
  }
  function clearFieldError(field) {
    get(field)?.classList.remove('input-validation-error', 'is-invalid');
    const error = root.querySelector(`#error-${field}`);
    if (error) { error.querySelector('[data-component-text]').textContent = ''; error.classList.remove('field-validation-error'); error.classList.add('field-validation-valid'); }
  }
  function validate() {
    let errors = 0;
    for (const field of fields.slice(0, 3)) {
      const input = get(field); if (!input || input.type === 'hidden') continue;
      const limit = field === 'Name' ? 50 : field === 'Description' ? 4000 : 2000;
      const count = field === 'Name' ? [...trim(input.value)].length : input.value.length;
      const unchangedName = field === 'Name' && canonical(field, input.value) === canonical(field, value('HasReviewedValues') === 'true' ? value('ReviewedName') : value('OriginalName'));
      const tooLong = count > limit && !unchangedName;
      // Name is limited in Unicode code points; HTML maxlength counts UTF-16 units.
      input.removeAttribute('maxlength');
      const message = field === 'Name' && !trim(input.value) ? text('nameRequired') : tooLong ? root.dataset[`${key(field)}Limit`] || '' : '';
      input.setCustomValidity(message);
      if (message) errors++;
      const error = root.querySelector(`#error-${field}`);
      if (error && (message || error.dataset.clientError === 'true')) {
        error.querySelector('[data-component-text]').textContent = message; error.dataset.clientError = String(!!message);
        error.classList.toggle('field-validation-valid', !message);
        error.classList.toggle('field-validation-error', !!message);
      }
      const counter = root.querySelector(`[data-count-for="${field}"]`);
      if (counter) { counter.textContent = `${count.toLocaleString(document.documentElement.lang)} / ${limit.toLocaleString(document.documentElement.lang)}`; counter.hidden = count <= limit * .85; counter.classList.toggle('is-over', tooLong); }
      input.classList.toggle('is-invalid', !!message || input.classList.contains('input-validation-error'));
      input.setAttribute('aria-invalid', String(!!message || input.classList.contains('input-validation-error')));
    }
    const summary = root.querySelector('[data-client-validation]');
    if (summary) { const component = ui.template('banner-error').firstElementChild; component.querySelector('[data-component-text]').textContent = errors > 1 ? text('validationSummary').replace('{0}', String(errors)) : ''; summary.replaceChildren(...component.childNodes); summary.hidden = errors < 2; }
  }
  async function checkAgain() {
    if (!uncertain || pending) return;
    pending = true; report(text('unknown'), 'is-warning', 'uncertain', text('checkingLead')); paint();
    const session = uncertain;
    const result = await ui.busy(() => session.checkAgain(request.signal));
    if (lifetime.signal.aborted) return;
    pending = false;
    if (result.state === 'upToDate') {
      uncertain = null; retained = false;
      for (const field of fields) { set(field, result.current.values[key(field)]); set(`Original${field}`, result.current.values[key(field)]); set(`${field}Resolution`, 'None'); }
      set('Version', result.current.version); set('HasBaseline', 'true'); set('HasReviewedValues', 'false'); root.dataset.identityCurrentVersion = String(result.current.version);
      baseline = snapshot(); report(''); ui.toast(text('upToDate')); cleanNote = text('upToDateNote');
    } else if (result.state === 'notApplied') { uncertain = null; retained = true; report(text('notApplied'), 'is-info', 'info'); }
    else if (result.state === 'different') { uncertain = null; mergeCurrent(result.current); }
    else if (result.outcome?.kind === 'refused') report(text('refused'), 'is-error', 'error');
    else report(text('unknown'), 'is-warning', 'uncertain', text('uncertainLead'));
    validate(); paint(); save?.focus({ preventScroll: true });
  }
  async function confirmLeave() {
    if (!uncertain) return ui.confirmDiscard();
    const leave = await ui.confirm({ title: text('leaveTitle'), description: text('leaveMessage'), cancelLabel: text('leave'), actionLabel: text('checkAgain'), cancelClass: 'btn-outline-danger', focusAction: true, cancelResult: 'leave' });
    if (leave === true) await checkAgain();
    return leave === 'leave';
  }
  const unregister = ui.registerDraft(root, { isDirty: dirty, isPending: () => pending, confirmLeave, discard: () => { retained = false; uncertain = null; baseline = snapshot(); } });
  async function submit(confirmed = false) {
    if (pending) return;
    if (uncertain) { await checkAgain(); return; }
    if (!changed()) return;
    validate(); if (!form.reportValidity()) return;
    if (!confirmed && needsTimezoneReview()) { openReview(); return; }
    const data = new FormData(form);
    if (confirmed) { data.set('Input.ConfirmTimezoneChange', 'true'); data.set('Input.TimezoneConfirmationProposed', value('Timezone')); }
    const session = createIdentityReadbackSession(data, form.action, root.dataset.identityEventId, root.dataset.identityCurrentVersion, labels);
    pending = true; paint();
    const outcome = await ui.busy(() => window.AdminFetch.request(form.action, { method: 'POST', body: data, expect: 'html', allowRedirectTo: form.action, notice: false, signal: request.signal, draft: session.draft }));
    if (lifetime.signal.aborted) return;
    pending = false;
    if (layer) { await layer.close(false); layer = null; }
    if (outcome.kind === 'session-lost') { retained = true; window.AdminFetch.sessionNotice(session.draft, outcome.destination, { labels }); paint(); return; }
    if (outcome.kind === 'refused') { retained = true; report(text('refused'), 'is-error', 'error'); paint(); return; }
    const parsed = outcome.kind === 'handler' ? new DOMParser().parseFromString(outcome.data, 'text/html') : null;
    const next = parsed?.querySelector('[data-identity-editor]');
    if (!next || next.dataset.identityEventId !== root.dataset.identityEventId || next.dataset.identitySaveUncertain === 'true') {
      uncertain = session; retained = true; report(text('unknown'), 'is-warning', 'uncertain', text('uncertainLead')); paint(); return;
    }
    const succeeded = outcome.response.redirected && new URL(outcome.response.url).pathname === new URL(form.action).pathname;
    const nextRoot = document.importNode(next, true);
    await dispose();
    const oldLock = document.getElementById('identity-readonly'), newLock = parsed.querySelector('#identity-readonly');
    oldLock?.remove(); if (newLock) root.before(document.importNode(newLock, true));
    root.replaceWith(nextRoot); await init(nextRoot, ui);
    if (succeeded) {
      try { ui.refreshContext(parsed); }
      catch (error) { console.warn('Saved identity, but shell context could not be refreshed.', error); }
      for (const toast of parsed.querySelectorAll('[data-toast-host] [data-toast]')) ui.toast(toast.querySelector('.grow')?.textContent || toast.textContent);
      nextRoot.querySelector('[data-identity-save]')?.focus({ preventScroll: true });
      const summary = document.querySelector('.summary span'), updated = parsed.querySelector('.summary span'); if (summary && updated) summary.textContent = updated.textContent;
    }
  }
  function needsTimezoneReview() {
    return root.dataset.identityPublic === 'true' && value('Timezone') !== value('TimezoneConfirmationOriginal');
  }
  function openReview() {
    if (layer || pending) return;
    const template = root.querySelector(`template[data-identity-timezone-preview][data-zone="${CSS.escape(value('Timezone'))}"]`);
    if (!template) return;
    const content = ui.template('confirmation');
    const heading = content.querySelector('[data-confirm-title]'); heading.textContent = text('timezoneTitle').replace('{0}', value('Timezone'));
    content.querySelector('[data-confirm-description]').replaceWith(template.content.cloneNode(true));
    const other = fields.slice(0, 3).filter(field => canonical(field, wire(value(field))) !== canonical(field, wire(value(`Original${field}`)))).map(field => labels[field]);
    const actions = content.querySelector('.m-actions');
    if (other.length) { const together = document.createElement('p'); together.className = 'compare-foot'; together.dataset.savedTogether = ''; together.textContent = text('savedTogether').replace('{0}', other.join(', ')); actions.before(together); }
    const cancel = content.querySelector('[data-confirm-cancel]'); cancel.textContent = text('cancel'); cancel.dataset.identityReviewCancel = '';
    const confirm = content.querySelector('[data-confirm-accept]'); confirm.querySelector('[data-component-text]').textContent = text('timezoneAction').replace('{0}', value('Timezone')); confirm.dataset.identityReviewConfirm = '';
    layer = ui.openLayer({ title: heading.textContent, content, confirmation: true, pending: () => pending, opener: save, onClose: () => { layer = null; } });
    layer.element.classList.add('is-wide'); layer.element.setAttribute('aria-busy', 'false');
    listen(cancel, 'click', () => { if (!pending) void layer?.close(false); });
    listen(confirm, 'click', () => void submit(true));
  }
  const initialConflicts = (root.dataset.identityConflicts || '').split(',');
  if (retained) {
    for (const field of fields) {
      if (initialConflicts.includes(field)) conflict(field, value(`Reviewed${field}`));
      else if (canonical(field, wire(value(field))) === canonical(field, wire(value(`Original${field}`)))) { if (canonical(field, value(`Reviewed${field}`)) !== canonical(field, wire(value(`Original${field}`)))) mergedNote(field); set(field, value(`Reviewed${field}`)); }
    }
    if (initialConflicts.some(Boolean) || root.dataset.identityScheduleStale === 'true') staleReport();
  }
  if (root.dataset.identitySaveUncertain === 'true') { uncertain = createIdentityReadbackSession(new FormData(form), form.action, root.dataset.identityEventId, root.dataset.identityCurrentVersion, labels); report(text('unknown'), 'is-warning', 'uncertain', text('uncertainLead')); }
  listen(form, 'submit', event => { event.preventDefault(); void submit(); });
  function edited(event) {
    const field = event.target.name?.replace(/^Input\./, '');
    if (!fields.includes(field)) return;
    cleanNote = '';
    if (['UseCurrent', '2'].includes(value(`${field}Resolution`)))
      set(`${field}Resolution`, canonical(field, wire(value(field))) === canonical(field, wire(value(`Original${field}`))) ? 'None' : 'KeepMine');
    clearFieldError(field);
  }
  listen(form, 'input', event => { edited(event); validate(); paint(); });
  listen(form, 'change', event => {
    edited(event);
    const select = get('Timezone');
    if (select instanceof HTMLSelectElement) for (const option of select.options) if (!['UTC', 'Europe/Copenhagen', select.value].includes(option.value)) option.disabled = true;
    validate(); paint();
  });
  listen(root.querySelector('[data-copy-url]'), 'click', async event => {
    const button = event.currentTarget;
    try { await navigator.clipboard.writeText(button.dataset.copyUrl); if (lifetime.signal.aborted) return; button.querySelector('[data-component-text]').textContent = text('copyDone'); clearTimeout(copyTimer); copyTimer = setTimeout(() => { button.querySelector('[data-component-text]').textContent = text('copy'); }, 2000); ui.toast(text('copied')); }
    catch { if (lifetime.signal.aborted) return; const link = root.querySelector('#event-link'); if (link) { const range = document.createRange(); range.selectNodeContents(link); const selection = getSelection(); selection.removeAllRanges(); selection.addRange(range); } ui.toast(text('copyFailed'), { error: true }); }
  });
  release = () => { clearTimeout(copyTimer); lifetime.abort(); request.abort(); unregister(); return layer && !pending ? layer.close(false) : undefined; };
  validate(); paint();
  if (editable && root.dataset.identityReviewRequired === 'true' && root.dataset.identityScheduleStale !== 'true' && !uncertain) openReview();
  else root.querySelector('.input-validation-error')?.focus();
}

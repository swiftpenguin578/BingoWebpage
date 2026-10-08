// U7 1b / Board.dc.html tile drawer: one drawer for creating, editing and viewing a tile.
// Choices come from EditorData on demand. The drawer state is in the query (?tile=B3,
// RC05 B5). AU11: the override control sends ChangeManualEhbOverride for every set,
// change and reset. RC05 B1: an unconfirmed save keeps the whole draft and compares the
// complete intent with the Readback. RC05 B2: the draft survives takeover and refresh;
// it keeps the board version it was opened on and saving after the board changed needs
// an explicit choice. EHB figures are the server's (B7): the calculated baseline shown is
// EditorData's CalculatedEhb for the stored objectives.
import { posName, parseTileRef, parseDecimal, parseWhole, artworkConfirmed } from './admin-board-model.js';

const TILE_SCHEMA = { tile: { default: '', valid: value => /^[A-Ha-h][1-8]$/.test(value) } };

export function install(ctx) {
  const { ui, t, el, icon, button, on, fmt1, life } = ctx;
  let choices = null; // { revision, bosses, drops }
  let ed = null;       // the open editor
  let pushed = false;

  /* ---------------- choices ---------------- */
  async function loadChoices(tileId) {
    const url = new URL(ctx.url('EditorData')); if (tileId) url.searchParams.set('tileId', tileId);
    const result = await window.AdminFetch.request(url.href, { cache: 'no-store', readback: true, signal: life.signal });
    if (result.kind !== 'handler') return { failed: true, gone: result.kind === 'unknown' && result.status === 404 };
    const data = result.data;
    if (!choices || choices.revision !== data.revision) choices = { revision: data.revision, bosses: data.bosses, drops: data.drops };
    return { tile: data.tile, version: data.boardVersion };
  }
  const bossById = id => choices?.bosses.find(b => b.id === id);
  const dropById = id => choices?.drops.find(d => d.id === id);

  /* ---------------- form model ---------------- */
  const blankDrops = () => ({ rid: null, sources: [], drops: [], weights: {}, target: '1', repeats: true, weightsOn: false, q: '', countOpen: false, pop: false, active: 0 });
  const blankManual = () => ({ rid: null, text: '', target: '1' });
  function formFrom(tile) {
    if (!tile) return { name: '', descManual: false, desc: '', art: null, file: null, removeArt: false, kind: 'drops', objs: { drops: [blankDrops()], manual: [blankManual()] }, override: false, ehb: '' };
    const manual = tile.requirements.length > 0 && tile.requirements.every(r => r.kind === 'challenge');
    const objs = tile.requirements.map(r => r.kind === 'challenge'
      ? { rid: r.requirementId, text: r.description, target: String(r.target) }
      : { rid: r.requirementId, sources: [...r.bossIds], drops: [...r.dropIds], weights: Object.fromEntries(Object.entries(r.dropWeights).map(([k, w]) => [k, String(w)])), target: String(r.target), repeats: r.duplicatesAllowed, weightsOn: Object.values(r.dropWeights).some(w => w > 1), q: '', countOpen: false, pop: false, active: 0 });
    return {
      name: tile.name, descManual: !!tile.description, desc: tile.description || '', art: tile.imageUrl, file: null, removeArt: false,
      kind: manual ? 'manual' : 'drops',
      objs: { drops: manual ? [blankDrops()] : objs, manual: manual ? objs : [blankManual()] },
      override: !manual && tile.manualEhb != null, ehb: tile.manualEhb != null ? String(tile.manualEhb) : ''
    };
  }
  const snapshot = f => JSON.stringify({ ...f, file: f.file ? [f.file.name, f.file.size, f.file.lastModified] : null, objs: f.objs[f.kind].map(o => ({ ...o, q: '', countOpen: false, pop: false, active: 0 })) });
  const objsOf = f => f.objs[f.kind];
  // RC05 B2: dirty is measured on the draft itself, independent of edit permission.
  const isDirty = () => !!ed && !ed.loading && !ed.error && snapshot(ed.f) !== ed.base;

  /* ---------------- validation (B6 parser; B-Board-2 limits) ---------------- */
  function errors() {
    const f = ed.f, out = { objs: [] };
    if (f.name.trim().length > 80 && f.name.trim() !== (ed.tile?.name || '')) out.name = t('Use 80 characters or fewer.');
    if (f.descManual && !f.desc.trim()) out.desc = t('Write a description, or use the automatic one.');
    if (f.descManual && f.desc.length > 4000) out.desc = t('Use 4,000 characters or fewer.');
    objsOf(f).forEach((o, i) => {
      const oe = {};
      if (parseWhole(o.target, 1, 10000) == null) oe.target = t('Enter a whole number from 1 to 10,000.');
      if (f.kind === 'drops') {
        if (!o.sources.length) oe.src = t('Choose at least one boss or activity.');
        else if (!o.drops.length) oe.drops = t('Choose at least one eligible drop.');
        if (o.weightsOn && o.drops.some(id => parseWhole(o.weights[id] ?? '1', 1, 10000) == null)) oe.weights = t('Each drop counts as a whole number from 1 to 10,000.');
      } else if (!o.text.trim()) oe.text = t('Describe what must be completed.');
      out.objs[i] = oe;
    });
    const n = parseDecimal(f.ehb);
    if (f.kind === 'manual' && !(n > 0 && n <= 100000)) out.ehb = t('Enter an EHB estimate above 0 (up to 100,000).');
    if (f.kind === 'drops' && f.override && !(n > 0 && n <= 100000)) out.ehb = t('Enter an EHB above 0 (up to 100,000), or use the calculated estimate.');
    out.count = (out.name ? 1 : 0) + (out.desc ? 1 : 0) + (out.ehb ? 1 : 0) + out.objs.reduce((a, oe) => a + Object.keys(oe).length, 0);
    return out;
  }

  /* ---------------- payload and intent ---------------- */
  function payload() {
    const f = ed.f, form = new FormData();
    form.append('BoardVersion', String(ed.openingVersion));
    if (ed.tileId) form.append('TileDraft.TileId', ed.tileId);
    form.append('TileDraft.Position', String(ed.pos));
    form.append('TileDraft.Name', f.name.trim());
    form.append('TileDraft.Description', f.descManual ? f.desc.trim() : '');
    if (f.file) form.append('TileDraft.Image', f.file);
    if (f.removeArt) form.append('TileDraft.RemoveImage', 'true');
    const base = ed.tile, baseOverride = base && base.manualEhb != null && base.requirements.every(r => r.kind !== 'challenge') ? Number(base.manualEhb) : null;
    if (f.kind === 'manual') form.append('TileDraft.ManualEhb', String(parseDecimal(f.ehb)));
    else {
      // AU11: every set, change and reset is explicit; otherwise the server keeps the stored override.
      const wanted = f.override ? parseDecimal(f.ehb) : null;
      const change = wanted !== baseOverride;
      form.append('TileDraft.ChangeManualEhbOverride', change ? 'true' : 'false');
      form.append('TileDraft.ManualEhb', wanted == null ? '' : String(wanted));
    }
    objsOf(f).forEach((o, i) => {
      const p = `TileDraft.Requirements[${i}].`;
      if (o.rid) form.append(p + 'RequirementId', o.rid);
      form.append(p + 'Kind', f.kind === 'manual' ? 'challenge' : 'drops');
      form.append(p + 'Target', String(parseWhole(o.target, 1, 10000)));
      if (f.kind === 'manual') { form.append(p + 'Description', o.text.trim()); form.append(p + 'DuplicatesAllowed', 'true'); return; }
      form.append(p + 'DuplicatesAllowed', o.repeats ? 'true' : 'false');
      for (const id of o.sources) form.append(p + 'BossIds', id);
      for (const id of o.drops) { form.append(p + 'DropIds', id); form.append(`${p}DropWeights[${id}]`, String(o.weightsOn ? parseWhole(o.weights[id] ?? '1', 1, 10000) : 1)); }
    });
    return form;
  }
  // RC05 B1: the complete intent (name, description, artwork, EHB, objectives) as the Readback shows it.
  function intent() {
    const f = ed.f;
    return {
      name: f.name.trim() || null,
      description: f.descManual ? f.desc.trim() : null,
      removeArt: f.removeArt, newArt: !!f.file,
      manualEhb: f.kind === 'manual' ? null : f.override ? parseDecimal(f.ehb) : null,
      ehb: f.kind === 'manual' ? parseDecimal(f.ehb) : null,
      objectives: objsOf(f).map(o => f.kind === 'manual'
        ? { manual: true, target: parseWhole(o.target, 1, 10000), description: o.text.trim() }
        : { manual: false, target: parseWhole(o.target, 1, 10000), duplicatesAllowed: o.repeats, bossIds: [...o.sources].sort(), drops: Object.fromEntries(o.drops.map(id => [id, o.weightsOn ? parseWhole(o.weights[id] ?? '1', 1, 10000) : 1])) })
    };
  }
  function matches(state, want) {
    const tiles = state.working?.tiles || [];
    const tile = ed.tileId ? tiles.find(x => x.tileId === ed.tileId) : tiles.find(x => x.row * ctx.view.cols + x.column === ed.pos);
    if (!tile) return false;
    if (want.name && tile.name !== want.name) return false;
    if (want.description !== null ? tile.descriptionIsAutomatic || tile.description !== want.description : !tile.descriptionIsAutomatic) return false;
    if (!artworkConfirmed(want, tile, ed.openingArt)) return false;
    if (want.ehb !== null && Number(tile.ehb) !== want.ehb) return false;
    if (want.ehb === null && (tile.manualEhbOverride == null ? null : Number(tile.manualEhbOverride)) !== want.manualEhb) return false;
    const objectives = [...tile.objectives].sort((a, b) => a.position - b.position);
    if (objectives.length !== want.objectives.length) return false;
    return want.objectives.every((w, i) => {
      const o = objectives[i];
      if (o.manual !== w.manual || o.target !== w.target) return false;
      if (w.manual) return o.description === w.description;
      if (o.duplicatesAllowed !== w.duplicatesAllowed) return false;
      if (JSON.stringify([...o.bossIds].sort()) !== JSON.stringify(w.bossIds)) return false;
      const drops = Object.fromEntries(o.drops.map(d => [d.sourceDropId, d.weight]));
      return Object.keys(drops).length === Object.keys(w.drops).length && Object.entries(w.drops).every(([id, weight]) => drops[id] === weight);
    });
  }

  /* ---------------- open / close / history ---------------- */
  const canEditTile = () => ctx.canEdit() && ed && !ed.viewOnly;
  async function open(pos, { push = true } = {}) {
    if (ed) return;
    const tile = ctx.tileAt(pos);
    if (!tile && !ctx.canEdit()) return;
    ed = { pos, tileId: tile?.id || null, tileView: tile, loading: true, error: null, busy: false, banner: null, openObj: 0, viewOnly: !ctx.canEdit(), openingVersion: ctx.view.version, f: formFrom(null), base: '', tile: null, calc: null };
    buildLayer();
    if (push && !new URL(location.href).searchParams.get('tile')) { ui.setUrl({ tile: posName(pos, ctx.view.cols) }, TILE_SCHEMA, { record: true }); pushed = true; }
    await load();
  }
  async function load() {
    ed.loading = true; ed.error = null; paint();
    const result = await loadChoices(ed.tileId);
    if (!ed) return;
    ed.loading = false;
    if (result.failed) { ed.error = result.gone ? 'gone' : 'failed'; paint(); return; }
    ed.tile = result.tile; ed.calc = result.tile?.calculatedEhb ?? null;
    ed.f = formFrom(result.tile); ed.base = snapshot(ed.f);
    ed.openingArt = ed.tileId && result.tile?.imageUrl ? undefined : null; // M2: unknown until the Readback answers
    paint(); ed.layer.markClean();
    requestAnimationFrame(() => (ed?.layer.element.querySelector('#ed-name') || ed?.layer.element.querySelector('#ed-close'))?.focus());
    // M2: remember the artwork reference as the drawer opens (undefined = could not be read).
    if (ed.tileId && result.tile?.imageUrl) {
      const mine = ed, opened = await ctx.readback();
      if (ed !== mine) return;
      const found = opened?.working?.tiles?.find(x => x.tileId === mine.tileId);
      mine.openingArt = found ? (found.artworkReference ?? null) : undefined;
    }
  }
  function onClosed() {
    const wasPushed = pushed; ed = null; pushed = false;
    if (new URL(location.href).searchParams.get('tile')) { if (wasPushed) void ui.backUrl(); else ui.setUrl({}, TILE_SCHEMA); }
    ctx.focusSoon(ctx.cellId(ctx.focusPos));
  }
  const unregisterUrl = ui.registerUrlState(async next => {
    const url = new URL(next);
    if (url.pathname.toLowerCase() !== location.pathname.toLowerCase() && url.pathname !== new URL(ctx.url()).pathname) return false;
    const ref = url.searchParams.get('tile');
    if (!ref) { if (ed) { pushed = false; await ed.layer.close(true); } return true; }
    const pos = parseTileRef(ref, ctx.view.rows, ctx.view.cols);
    if (pos == null) { ctx.notice = unknownTile(); ctx.paint(); return true; }
    if (!ed) await open(pos, { push: false });
    return true;
  });
  // RC05 B4: an unknown or foreign tile reference shows unavailable with a way out.
  const unknownTile = () => ({ cls: 'is-warning', title: t('That tile isn’t on this board.'), text: t('The link may be from another board size or an older version.'), action: t('Close'), run: () => { ctx.notice = null; ui.setUrl({}, TILE_SCHEMA); ctx.paint(); } });

  /* ---------------- save ---------------- */
  async function save() {
    if (!ed || ed.busy || !canEditTile()) return;
    const errs = errors();
    if (errs.count) { ed.showErrors = true; paint(); focusFirstError(); return; }
    const want = intent();
    ed.busy = true; ed.banner = null; paint();
    const draft = { [t('Tile')]: ed.f.name.trim() || ctx.where(ed.pos) };
    const outcome = await ctx.command(ed.tileId ? 'EditTile' : 'CreateTile', null, { form: payload(), draft });
    if (!ed) return;
    ed.busy = false;
    if (outcome.kind === 'saved') { const name = ed.f.name.trim(); await ed.layer.close(true); await ctx.refresh(); ui.toast(outcome.message || t('Tile saved.')); return; }
    if (outcome.kind === 'refused' || outcome.kind === 'route-refused') {
      await ctx.refresh();
      // RC05 B2: the board moved on (takeover, another admin, lapse). Keep the draft; saving needs an explicit choice.
      if (ctx.view.version !== ed.openingVersion || ctx.view.control?.who !== 'me')
        ed.banner = { cls: 'is-warning', title: t('The board changed while this was open.'), text: (outcome.message ? outcome.message + ' ' : '') + t('Your changes are still here. Check the board, then save your version on the latest board.'), action: ctx.view.control?.who === 'me' ? t('Use the latest board') : null, run: () => { ed.openingVersion = ctx.view.version; ed.banner = null; paint(); } };
      else ed.banner = { cls: 'is-error', title: t('The tile wasn’t saved.'), text: (outcome.message || '') + ' ' + t('Your changes are still here.') };
      paint(); focusBanner(); return;
    }
    if (outcome.kind !== 'unknown') { paint(); return; }
    // RC05 B1: never claim it was saved, or that retrying is harmless, from state alone.
    ed.checking = true; paint();
    const state = await ctx.readback();
    if (!ed) return;
    ed.checking = false;
    if (state && matches(state, want)) { await ed.layer.close(true); await ctx.refresh(); ui.toast(t('Tile saved. The board confirms your changes.')); return; }
    ed.banner = state
      ? { cls: 'is-warning', title: t('We couldn’t confirm the save.'), text: t('The board doesn’t show these changes. They’re still here; check the board before saving again.') }
      : { cls: 'is-warning', title: t('We couldn’t confirm whether the tile was saved.'), text: t('Your changes are still here. Check before saving again, so nothing is saved twice.'), action: t('Check again'), run: () => void recheck(want) };
    if (state) await ctx.refresh();
    paint(); focusBanner();
  }
  async function recheck(want) {
    ed.checking = true; ed.banner = null; paint();
    const state = await ctx.readback();
    if (!ed) return;
    ed.checking = false;
    if (state && matches(state, want)) { await ed.layer.close(true); await ctx.refresh(); ui.toast(t('Tile saved. The board confirms your changes.')); return; }
    ed.banner = state ? { cls: 'is-warning', title: t('We couldn’t confirm the save.'), text: t('The board doesn’t show these changes. They’re still here; check the board before saving again.') }
      : { cls: 'is-warning', title: t('We couldn’t confirm whether the tile was saved.'), text: t('Your changes are still here. Check before saving again, so nothing is saved twice.'), action: t('Check again'), run: () => void recheck(want) };
    paint(); focusBanner();
  }
  const focusBanner = () => requestAnimationFrame(() => ed?.layer.element.querySelector('#ed-banner')?.focus());
  const focusFirstError = () => requestAnimationFrame(() => ed?.layer.element.querySelector('[aria-invalid="true"]')?.focus());

  /* ---------------- remove ---------------- */
  async function remove(tile) {
    if (!ctx.canEdit() || ctx.blocked() || !tile || tile.locked) return;
    const approved = ctx.view.mode === 'approved';
    const yes = await ui.confirm({ title: t('Remove {0}?', tile.name), description: t('{0} becomes empty. The tile and its objectives are deleted from this board.', ctx.where(tile.pos)) + (approved ? ' ' + t('The approved board returns to draft.') : ''), actionLabel: t('Remove tile'), cancelLabel: t('Cancel'), actionClass: 'btn-danger' });
    if (!yes) return;
    if (ed) await ed.layer.close(true);
    const outcome = await ctx.command('Remove', { tileId: tile.id });
    await ctx.settle(outcome, { what: t('remove {0}', tile.name), check: s => !s.working.tiles.some(x => x.tileId === tile.id), ok: t('Tile removed.'), focus: 'be-' + tile.pos });
  }
  ctx.hooks.removeTile = tile => void remove(tile);
  ctx.hooks.openTile = pos => void open(pos);

  /* ---------------- drawer rendering ---------------- */
  function buildLayer() {
    const content = el('div', 'bd-ed bd-layer'); content.dataset.pageFamily = 'board';
    const layer = ui.openLayer({ kind: 'drawer', title: t('Tile'), content, dirty: isDirty, pending: () => !!ed?.busy || !!ed?.checking, onClose: () => onClosed() }); layer.element.dataset.pageFamily = 'board';
    layer.element.classList.add('is-wide');
    ed.layer = layer; ed.content = content;
    on(content, 'keydown', event => { if (event.key === 'Enter' && event.target.matches('input.input:not([role=combobox])') && !event.target.closest('.drop-row,.count-line')) { event.preventDefault(); void save(); } });
  }
  const fieldErr = (id, text) => { const e = el('div', 'field-err'); e.id = id; e.append(icon('error'), document.createTextNode(text)); return e; };
  const lock = text => { const e = el('div', 'field-lock'); e.append(icon('lock'), document.createTextNode(text)); return e; };
  function bannerEl(spec) {
    const b = el('div', `banner ${spec.cls}`); b.setAttribute('role', 'alert'); b.id = 'ed-banner'; b.tabIndex = -1;
    b.append(icon(spec.cls === 'is-error' ? 'error' : 'warning'));
    const g = el('span', 'grow'); g.append(el('b', null, spec.title), document.createTextNode(' ' + spec.text)); b.append(g);
    if (spec.action) b.append(button('banner-btn', spec.action, spec.run, 'ed-banner-act'));
    return b;
  }
  function paint() {
    if (!ed) return;
    const v = ctx.view, f = ed.f, tile = ed.tileView, ro = !canEditTile(), busy = ed.busy || ed.checking, locked = !!tile?.locked;
    const errs = ed.showErrors && !ed.loading && !ed.error ? errors() : { objs: [] };
    const scrollTop = ed.content.querySelector('.dr-body')?.scrollTop || 0;
    const head = el('div', 'dr-head'), grow = el('div', 'grow');
    const eyebrow = ctx.where(ed.pos) + (v.mode === 'correction' ? ' · ' + t('Working copy') : '');
    grow.append(el('div', 'eyebrow', eyebrow));
    const title = el('h2', 'dr-title', ed.tileId ? (f.name.trim() || tile?.name || '') : t('New tile')); title.id = 'ed-title'; title.dataset.confirmTitle = '';
    grow.append(title);
    const badges = [];
    if (ro) badges.push([v.control?.who === 'other' ? t('{0} is editing', v.control.name || t('Another administrator')) : ctx.editable() ? t('View only') : t('Read-only'), 'badge-neutral']);
    if (locked) badges.push([t('Evidence submitted'), 'badge-warning']);
    if (badges.length) { const bw = el('div', 'dr-badges'); for (const [label, cls] of badges) bw.append(el('span', 'badge ' + cls, label)); grow.append(bw); }
    const closeBtn = button('icon-btn', null, () => void ui.closeLayer(), 'ed-close'); closeBtn.setAttribute('aria-label', t('Close')); closeBtn.append(icon('close')); closeBtn.disabled = busy;
    head.append(grow, closeBtn);
    const body = el('div', 'dr-body'); body.id = 'ed-body';
    if (ed.loading) {
      const sk = el('div', 'sec'); sk.setAttribute('aria-busy', 'true');
      for (const w of [40, 90, 70, 80, 60]) { const s = el('div', 'bd-sk-line'); const bar = el('div', 'sk'); bar.style.width = w + '%'; s.append(bar); sk.append(s); }
      body.append(sk);
    } else if (ed.error) {
      const empty = el('div', 'empty is-error'), ic = el('div', 'empty-ic'); ic.append(icon('error'));
      empty.append(ic, el('div', 'empty-title', ed.error === 'gone' ? t('This tile isn’t on the board any more.') : t('Couldn’t load the tile editor')), el('div', 'empty-text', ed.error === 'gone' ? t('Another administrator may have removed or moved it. Nothing was changed.') : t('The catalogue choices didn’t load. Nothing was changed.')));
      if (ed.error !== 'gone') empty.append(button('btn', t('Try again'), () => void load(), 'ed-retry'));
      body.append(empty);
    } else {
      if (ed.banner || ed.checking) { const w = el('div', 'dr-banners'); w.append(bannerEl(ed.checking ? { cls: 'is-warning', title: t('Checking…'), text: t('Reading the current board.') } : ed.banner)); body.append(w); }
      if (ctx.view.control?.who === 'other' && !ed.viewOnly && isDirty()) { const w = el('div', 'dr-banners'); w.append(bannerEl({ cls: 'is-warning', title: t('{0} is editing now.', v.control.name || t('Another administrator')), text: t('Your changes are still here; take over editing to save them.') })); body.append(w); }
      body.append(tileSection(ro, busy, errs), objectivesSection(ro, busy, errs, locked), estimateSection(ro, busy, errs, locked));
    }
    const foot = el('div', 'dr-foot');
    if (ed.tileId && !ro && !locked) { const r = button('btn btn-quiet-danger', t('Remove tile…'), () => void remove(tile), 'ed-remove'); r.disabled = busy; foot.append(r); }
    if (ed.tileId && !ro && locked) { const l = lock(t('Can’t be removed: evidence was submitted')); l.classList.add('bd-lock-inline'); foot.append(l); }
    foot.append(el('div', 'spacer'));
    if (isDirty()) { const d = el('span', 'dirty'); d.setAttribute('role', 'status'); d.append(el('span', 'dot'), document.createTextNode(t('Unsaved changes'))); foot.append(d); }
    const cancel = button('btn', ro ? t('Close') : t('Cancel'), () => void ui.closeLayer(), 'ed-cancel'); cancel.disabled = busy; foot.append(cancel);
    if (!ro && !ed.loading && !ed.error) {
      const s = button('btn btn-primary' + (busy ? ' is-busy' : ''), null, () => void save(), 'ed-save');
      if (busy) s.append(el('span', 'spin'));
      s.append(document.createTextNode(busy ? t('Saving…') : ed.tileId ? t('Save tile') : t('Add tile'))); s.disabled = busy;
      foot.append(s);
    }
    const active = document.activeElement?.id;
    ed.content.replaceChildren(head, body, foot);
    ed.content.querySelector('.dr-body').scrollTop = scrollTop;
    if (active && ed.content.querySelector('#' + CSS.escape(active))) ed.content.querySelector('#' + CSS.escape(active)).focus({ preventScroll: true });
    ed.layer.markClean();
  }
  const setF = patch => { Object.assign(ed.f, patch); paint(); };
  function input(id, value, onInput, { cls = 'input', invalid, describedBy, disabled, placeholder, inputMode } = {}) {
    const i = el('input', cls + (invalid ? ' is-invalid' : '')); i.id = id; i.type = 'text'; i.value = value ?? ''; i.disabled = !!disabled; i.autocomplete = 'off';
    if (placeholder) i.placeholder = placeholder; if (inputMode) i.inputMode = inputMode;
    if (describedBy) i.setAttribute('aria-describedby', describedBy); i.setAttribute('aria-invalid', invalid ? 'true' : 'false');
    on(i, 'input', () => onInput(i.value));
    on(i, 'change', () => paint());
    return i;
  }
  function tileSection(ro, busy, errs) {
    const f = ed.f, sec = el('section', 'sec'); sec.setAttribute('aria-labelledby', 'ed-sec-tile');
    const sh = el('div', 'sec-head'), st = el('h3', 'sec-title', t('Tile')); st.id = 'ed-sec-tile'; sh.append(st); sec.append(sh);
    const fields = el('div', 'form-fields');
    // name
    const nf = el('div', 'field'), nl = el('label', 'lbl', t('Name')); nl.htmlFor = 'ed-name'; nf.append(nl);
    if (ro) nf.append(el('div', 'ro-value', f.name || ed.tileView?.name || ''));
    else {
      nf.append(input('ed-name', f.name, value => { ed.f.name = value; refreshFoot(); }, { invalid: !!errs.name, describedBy: 'ed-name-hint' + (errs.name ? ' ed-name-err' : ''), disabled: busy, placeholder: t('Name this tile') }));
      if (errs.name) nf.append(fieldErr('ed-name-err', errs.name));
      const hint = el('div', 'field-hint', t('Shown on the board and to players. Leave blank to name it after the chosen bosses.')); hint.id = 'ed-name-hint'; nf.append(hint);
    }
    // description
    const df = el('div', 'field'), lr = el('div', 'lbl-row'), dl = el('span', 'lbl', t('Description')); dl.id = 'ed-desc-lbl'; lr.append(dl);
    if (f.descManual && f.desc.length > 3400) lr.append(el('span', 'field-count' + (f.desc.length > 4000 ? ' is-over' : ''), f.desc.length.toLocaleString(ctx.lang) + ' / 4,000'));
    df.append(lr);
    if (!f.descManual) {
      const unchanged = ed.tileId && !isDirty();
      const auto = el('div', 'auto-text', unchanged && !ed.tile?.description ? (ed.tileView?.desc || '') : t('Written automatically from the objectives when you save.'));
      auto.setAttribute('aria-labelledby', 'ed-desc-lbl'); df.append(auto);
      const cs = el('div', 'count-sum bd-count-sum'); cs.append(el('span', null, t('Written automatically from the objectives.')));
      if (!ro) { const b = button('text-btn', t('Write your own'), () => { setF({ descManual: true }); requestAnimationFrame(() => ed.content.querySelector('#ed-desc')?.focus()); }, 'ed-desc-write'); b.disabled = busy; cs.append(b); }
      df.append(cs);
    } else {
      if (ro) { const r = el('div', 'ro-value', f.desc); r.setAttribute('aria-labelledby', 'ed-desc-lbl'); df.append(r); }
      else {
        const ta = el('textarea', 'textarea bd-desc' + (errs.desc ? ' is-invalid' : '')); ta.id = 'ed-desc'; ta.value = f.desc; ta.disabled = busy;
        ta.setAttribute('aria-labelledby', 'ed-desc-lbl'); ta.setAttribute('aria-describedby', 'ed-desc-hint' + (errs.desc ? ' ed-desc-err' : '')); ta.setAttribute('aria-invalid', errs.desc ? 'true' : 'false');
        on(ta, 'input', () => { ed.f.desc = ta.value; refreshFoot(); }); on(ta, 'change', () => paint());
        df.append(ta);
        if (errs.desc) df.append(fieldErr('ed-desc-err', errs.desc));
      }
      const cs = el('div', 'count-sum bd-count-sum'); cs.id = 'ed-desc-hint'; cs.append(el('span', null, t('Your own wording. Players see it instead of the automatic description.')));
      if (!ro) { const b = button('text-btn', t('Use automatic description'), () => setF({ descManual: false, desc: '' }), 'ed-desc-auto'); b.disabled = busy; cs.append(b); }
      df.append(cs);
    }
    // artwork (stays editable on evidence-locked tiles, AU11)
    const af = el('div', 'field'), al = el('span', 'lbl', t('Artwork')); al.id = 'ed-art-lbl'; al.append(el('span', 'opt', ' · ' + t('optional'))); af.append(al);
    const pick = el('div', 'art-pick'); pick.setAttribute('role', 'group'); pick.setAttribute('aria-labelledby', 'ed-art-lbl');
    const thumb = el('div', 'art-thumb'), artUrl = f.file ? (ed.preview ||= URL.createObjectURL(f.file)) : f.removeArt ? null : f.art;
    if (artUrl) { const img = el('span', 'art-img'); img.setAttribute('role', 'img'); img.setAttribute('aria-label', t('Current tile artwork')); img.style.setProperty('--art', `url("${artUrl}")`); thumb.append(img); } else thumb.append(icon('image'));
    const side = el('div');
    if (!ro) {
      const actsEl = el('div', 'art-acts'), lbl = el('label', 'btn btn-sm', artUrl ? t('Replace') : t('Upload')); lbl.htmlFor = 'ed-art-file'; lbl.id = 'ed-art-btn'; lbl.tabIndex = 0;
      const file = el('input'); file.type = 'file'; file.id = 'ed-art-file'; file.accept = 'image/png,image/jpeg,image/webp'; file.disabled = busy; file.hidden = true;
      on(lbl, 'keydown', event => { if (event.key === 'Enter' || event.key === ' ') { event.preventDefault(); file.click(); } });
      on(file, 'change', () => { const chosen = file.files?.[0]; if (!chosen) return; if (ed.preview) URL.revokeObjectURL(ed.preview); ed.preview = null; ed.artErr = chosen.size > 10 * 1024 * 1024 || !/^image\/(png|jpeg|webp)$/.test(chosen.type) ? t('Choose a PNG, JPEG or WebP image up to 10 MB.') : ''; if (!ed.artErr) setF({ file: chosen, removeArt: false }); else paint(); });
      actsEl.append(lbl, file);
      if (artUrl) { const r = button('btn btn-sm btn-quiet-danger', t('Remove'), () => { if (ed.preview) URL.revokeObjectURL(ed.preview); ed.preview = null; setF({ file: null, removeArt: !!ed.f.art }); }, 'ed-art-remove'); r.disabled = busy; actsEl.append(r); }
      side.append(actsEl);
    }
    const ah = el('div', 'field-hint bd-field-hint-gap', t('PNG, JPEG or WebP. Shown on the tile instead of the default artwork.')); side.append(ah);
    if (ed.artErr) side.append(fieldErr('ed-art-err', ed.artErr));
    pick.append(thumb, side); af.append(pick);
    fields.append(nf, df, af); sec.append(fields);
    return sec;
  }
  function objectivesSection(ro, busy, errs, locked) {
    const f = ed.f, objs = objsOf(f), multi = objs.length > 1, sec = el('section', 'sec'); sec.setAttribute('aria-labelledby', 'ed-sec-obj');
    const sh = el('div', 'sec-head'), st = el('h3', 'sec-title', f.kind === 'manual' ? t(multi ? 'Challenges' : 'Challenge') : t(multi ? 'Objectives' : 'Objective')); st.id = 'ed-sec-obj'; sh.append(st);
    if (multi) sh.append(el('span', 'sec-aside', t('All {0} must be completed', objs.length)));
    sec.append(sh);
    if (!ro && !locked) {
      const seg = el('div', 'seg seg-lg bd-kind'); seg.setAttribute('role', 'radiogroup'); seg.setAttribute('aria-label', t('Objective kind')); seg.setAttribute('aria-describedby', 'ed-kind-hint');
      for (const [kind, label, ic] of [['drops', t('Collect drops'), 'drops'], ['manual', t('Manual challenge'), 'challenge']]) {
        const l = el('label', 'seg-opt' + (f.kind === kind ? ' is-on' : '')), r = el('input', 'sr'); r.type = 'radio'; r.name = 'ed-kind'; r.checked = f.kind === kind; r.disabled = busy;
        on(r, 'change', () => { ed.openObj = 0; setF({ kind }); });
        l.append(r, icon(ic), document.createTextNode(label)); seg.append(l);
      }
      const hint = el('div', 'field-hint bd-kind-hint', t('One kind per tile; switching keeps what you entered for the other kind until you save.')); hint.id = 'ed-kind-hint';
      sec.append(seg, hint);
    } else sec.append(el('p', 'bd-kind-ro', f.kind === 'manual' ? t('Manual challenge') : t('Collect drops')));
    objs.forEach((o, i) => sec.append(objective(o, i, objs, ro, busy, errs.objs[i] || {}, locked)));
    if (!ro && !locked) {
      const add = button('btn btn-sm btn-quiet bd-add-obj', null, () => { objs.push(f.kind === 'manual' ? blankManual() : blankDrops()); ed.openObj = objs.length - 1; paint(); requestAnimationFrame(() => ed.content.querySelector(`#ed-o${objs.length - 1}-${f.kind === 'manual' ? 'text' : 'src'}`)?.focus()); }, 'ed-add-obj');
      add.append(icon('plus'), document.createTextNode(f.kind === 'manual' ? t('Add another challenge') : t('Add another objective'))); add.disabled = busy;
      sec.append(add);
    }
    return sec;
  }
  function objectiveSummary(o) {
    if (ed.f.kind === 'manual') return o.text.trim() || t('New manual objective');
    const names = o.drops.map(id => dropById(id)?.itemName).filter(Boolean);
    if (!names.length) return t('New objective');
    return t('Collect {0}: {1}', o.target || '?', names.slice(0, 3).join(', ') + (names.length > 3 ? ' +' + (names.length - 3) : ''));
  }
  function objective(o, i, objs, ro, busy, oe, locked) {
    const f = ed.f, id = 'ed-o' + i, multi = objs.length > 1, isOpen = !multi || ed.openObj === i, disabledRules = ro || busy || locked;
    const box = el('div', 'obj' + (isOpen ? ' is-open' : '')); box.id = id;
    if (multi) {
      const h = el('div', 'obj-head'), num = el('span', 'obj-num', String(i + 1)); num.setAttribute('aria-hidden', 'true');
      const toggle = button('disc-btn obj-toggle bd-obj-toggle' + (isOpen ? ' is-open' : ''), null, () => { ed.openObj = isOpen ? -1 : i; paint(); }, id + '-toggle');
      toggle.setAttribute('aria-expanded', String(isOpen)); toggle.setAttribute('aria-controls', id + '-body');
      const sum = el('span', 'obj-sum'); sum.append(el('span', 'sr', t('Objective {0}: ', i + 1)), document.createTextNode(objectiveSummary(o))); toggle.append(sum, icon('chevron-down'));
      h.append(num, toggle);
      if (locked) { const fl = el('span', 'flag'); fl.title = t('Evidence submitted: requirements locked'); fl.append(icon('lock'), el('span', 'sr', t('Locked by submitted evidence'))); h.append(fl); }
      if (!ro && !locked) { const r = button('icon-btn', null, () => { objs.splice(i, 1); ed.openObj = Math.max(0, i - 1); paint(); }, id + '-remove'); r.setAttribute('aria-label', t('Remove objective {0}', i + 1)); r.title = t('Remove objective'); r.append(icon('close')); r.disabled = busy; h.append(r); }
      box.append(h);
    }
    if (!isOpen) return box;
    const bodyEl = el('div', 'obj-body'); bodyEl.id = id + '-body';
    if (locked) bodyEl.append(lock(t('Evidence has been submitted for this objective, so what it requires and how it counts can’t change.')));
    const target = parseWhole(o.target, 1, 10000) || 0;
    if (f.kind === 'drops') {
      bodyEl.append(sourcesField(o, id, disabledRules, oe), dropsField(o, id, disabledRules, oe));
      const cf = el('div', 'field'), cl = el('div', 'count-line'), lbl = el('label', null, t('Collect')); lbl.htmlFor = id + '-target';
      cl.append(lbl, input(id + '-target', o.target, value => { o.target = value; refreshFoot(); }, { invalid: !!oe.target, describedBy: (oe.target ? id + '-target-err ' : '') + id + '-count', disabled: disabledRules, inputMode: 'numeric' }),
        el('span', null, o.drops.length === 1 ? t(target === 1 ? 'drop' : 'drops') : t('of the chosen drops')));
      cf.append(cl);
      if (oe.target) cf.append(fieldErr(id + '-target-err', oe.target));
      const heavier = o.weightsOn ? o.drops.filter(d => (parseWhole(o.weights[d] ?? '1', 1, 10000) || 1) > 1).length : 0;
      const cs = el('div', 'count-sum bd-count-sum'); cs.id = id + '-count';
      cs.append(el('span', null, (o.repeats ? t('The same drop can count again') : t('Each chosen drop counts once')) + ' · ' + (heavier ? t(heavier === 1 ? '{0} drop counts extra' : '{0} drops count extra', heavier) : t('every drop counts as 1'))));
      if (!ro && !locked) { const b = button('text-btn', o.countOpen ? t('Done') : t('Change'), () => { o.countOpen = !o.countOpen; paint(); }, id + '-count-btn'); b.setAttribute('aria-expanded', String(o.countOpen)); cs.append(b); }
      cf.append(cs);
      if (o.countOpen) {
        const opts = el('div', 'count-opts');
        for (const [key, titleText, sub] of [['repeats', t('The same drop can count more than once'), t('Off: each chosen drop counts once, so the target needs that many different drops.')], ['weightsOn', t('Some drops count as more than one'), t('Set how much each chosen drop counts toward the target, for example a rare drop counting as 2.')]]) {
          const l = el('label', 'check-row' + (o[key] ? ' is-on' : '')), c = el('input'); c.type = 'checkbox'; c.checked = o[key]; c.disabled = disabledRules;
          on(c, 'change', () => { o[key] = c.checked; paint(); });
          const sp = el('span'); sp.append(el('span', 'choice-title bd-choice-line', titleText), el('span', 'choice-sub bd-choice-line', sub)); l.append(c, sp); opts.append(l);
        }
        cf.append(opts);
      }
      bodyEl.append(cf);
    } else {
      const tf = el('div', 'field'), tl = el('label', 'lbl', t('What must be completed?')); tl.htmlFor = id + '-text';
      const ta = el('textarea', 'textarea bd-manual' + (oe.text ? ' is-invalid' : '')); ta.id = id + '-text'; ta.value = o.text; ta.disabled = ro || busy; ta.placeholder = t('e.g. Complete a four-player Theatre of Blood in under 22:00');
      ta.setAttribute('aria-invalid', oe.text ? 'true' : 'false'); if (oe.text) ta.setAttribute('aria-describedby', id + '-text-err');
      on(ta, 'input', () => { o.text = ta.value; refreshFoot(); }); on(ta, 'change', () => paint());
      tf.append(tl, ta); if (oe.text) tf.append(fieldErr(id + '-text-err', oe.text));
      const cf = el('div', 'field'), cl = el('div', 'count-line'), lbl = el('label', null, t('Complete')); lbl.htmlFor = id + '-target';
      cl.append(lbl, input(id + '-target', o.target, value => { o.target = value; refreshFoot(); }, { invalid: !!oe.target, describedBy: oe.target ? id + '-target-err' : null, disabled: disabledRules, inputMode: 'numeric' }), el('span', null, t(target === 1 ? 'time' : 'times')));
      cf.append(cl); if (oe.target) cf.append(fieldErr(id + '-target-err', oe.target));
      cf.append(el('div', 'field-hint', t('Each approved completion counts once. Evidence and approval are still required.')));
      bodyEl.append(tf, cf);
    }
    box.append(bodyEl);
    return box;
  }
  function sourcesField(o, id, disabled, oe) {
    const fieldEl = el('div', 'field'), lbl = el('label', 'lbl', t('Bosses or activities')); lbl.htmlFor = id + '-src'; lbl.id = id + '-src-lbl';
    const combo = el('div', 'combo'), chips = el('div', 'combo-chips'); chips.setAttribute('aria-label', t('Chosen bosses or activities'));
    for (const sid of o.sources) {
      const boss = bossById(sid), chip = el('span', 'combo-chip', boss?.name || t('Unavailable boss'));
      if (!disabled) { const x = button(null, null, () => { o.sources = o.sources.filter(s => s !== sid); o.drops = o.drops.filter(d => dropById(d)?.bossId !== sid); paint(); }); x.setAttribute('aria-label', t('Remove {0}', boss?.name || '')); x.append(icon('close')); chip.append(x); }
      chips.append(chip);
    }
    combo.append(chips);
    if (!disabled) {
      const search = el('div', 'search'), q = el('input', 'input' + (oe.src ? ' is-invalid' : '')); q.id = id + '-src'; q.autocomplete = 'off'; q.placeholder = t('Search the catalogue'); q.value = o.q;
      q.setAttribute('role', 'combobox'); q.setAttribute('aria-expanded', String(o.pop)); q.setAttribute('aria-controls', id + '-pop'); q.setAttribute('aria-autocomplete', 'list'); q.setAttribute('aria-invalid', oe.src ? 'true' : 'false');
      if (oe.src) q.setAttribute('aria-describedby', id + '-src-err');
      const options = () => (choices?.bosses || []).filter(b => !o.q.trim() || b.name.toLowerCase().includes(o.q.trim().toLowerCase())).slice(0, 40);
      const pick = boss => { o.sources = o.sources.includes(boss.id) ? o.sources.filter(s => s !== boss.id) : [...o.sources, boss.id]; if (!o.sources.includes(boss.id)) o.drops = o.drops.filter(d => dropById(d)?.bossId !== boss.id); o.q = ''; paint(); requestAnimationFrame(() => ed.content.querySelector('#' + id + '-src')?.focus()); };
      on(q, 'input', () => { o.q = q.value; o.pop = true; o.active = 0; paintPop(); });
      on(q, 'focus', () => { o.pop = true; paintPop(); });
      on(q, 'blur', () => setTimeout(() => { if (q.isConnected && o.pop && document.activeElement !== q) { o.pop = false; paintPop(); } }, 150));
      on(q, 'keydown', event => {
        const list = options();
        if (event.key === 'ArrowDown' || event.key === 'ArrowUp') { event.preventDefault(); o.pop = true; o.active = (o.active + (event.key === 'ArrowDown' ? 1 : -1) + list.length) % Math.max(1, list.length); paintPop(); }
        else if (event.key === 'Enter') { event.preventDefault(); if (o.pop && list[o.active]) pick(list[o.active]); }
        else if (event.key === 'Escape' && o.pop) { event.preventDefault(); event.stopPropagation(); o.pop = false; paintPop(); }
      });
      search.append(icon('search'), q); combo.append(search);
      const pop = el('div', 'combo-pop'); pop.id = id + '-pop'; pop.setAttribute('role', 'listbox'); pop.setAttribute('aria-multiselectable', 'true'); pop.setAttribute('aria-labelledby', id + '-src-lbl');
      function paintPop() {
        q.setAttribute('aria-expanded', String(o.pop));
        // The shared .combo-pop has its own display; an absent listbox is removed, as in the reference.
        if (!o.pop) { pop.remove(); q.removeAttribute('aria-activedescendant'); return; }
        if (!pop.isConnected) combo.append(pop);
        const list = options(); pop.replaceChildren();
        list.forEach((boss, n) => {
          const on_ = o.sources.includes(boss.id), opt = button('combo-opt' + (n === o.active ? ' is-active' : ''), null, null, id + '-opt-' + n);
          opt.setAttribute('role', 'option'); opt.setAttribute('aria-selected', String(on_)); opt.tabIndex = -1;
          opt.addEventListener('mousedown', event => { event.preventDefault(); pick(boss); });
          opt.append(el('span', 'grow', boss.name), el('small', null, boss.category || ''));
          if (on_) opt.append(icon('check', 'ic menu-check'));
          pop.append(opt);
        });
        if (!list.length) pop.append(el('div', 'combo-empty', t('No boss or activity matches “{0}”.', o.q.trim())));
        if (o.pop && list.length) q.setAttribute('aria-activedescendant', id + '-opt-' + o.active); else q.removeAttribute('aria-activedescendant');
      }
      paintPop();
    }
    fieldEl.append(lbl, combo);
    if (oe.src) fieldEl.append(fieldErr(id + '-src-err', oe.src));
    return fieldEl;
  }
  function dropsField(o, id, disabled, oe) {
    const fieldEl = el('div', 'field'), lbl = el('div', 'lbl', t('Eligible drops')); lbl.id = id + '-drops-lbl'; fieldEl.append(lbl);
    if (!o.sources.length) { fieldEl.append(el('div', 'placeholder', t('Choose a boss or activity to see its drops.'))); return fieldEl; }
    for (const sid of o.sources) {
      const boss = bossById(sid), drops = (choices?.drops || []).filter(d => d.bossId === sid);
      const kept = o.drops.filter(d => !dropById(d) && ed.tile?.requirements.some(r => r.drops.some(x => x.id === d)));
      const group = el('div', 'drop-group'); group.setAttribute('role', 'group'); group.setAttribute('aria-label', t('{0} drops', boss?.name || ''));
      const gh = el('div', 'drop-group-head'), chosen = drops.filter(d => o.drops.includes(d.id)).length;
      gh.append(el('span', null, boss?.name || t('Unavailable boss')), el('span', 'drop-group-count', t('{0} of {1}', chosen, drops.length)));
      if (!disabled && drops.length) { const all = button('text-btn', chosen === drops.length ? t('Clear') : t('Choose all'), () => { o.drops = chosen === drops.length ? o.drops.filter(d => !drops.some(x => x.id === d)) : [...new Set([...o.drops, ...drops.map(d => d.id)])]; paint(); }); gh.append(all); }
      const list = el('div', 'drop-list');
      for (const d of drops) {
        const on_ = o.drops.includes(d.id), row = el('div', 'drop-row' + (on_ ? ' is-on' : '')), l = el('label'), c = el('input'); c.type = 'checkbox'; c.id = `${id}-d-${d.id}`; c.checked = on_; c.disabled = disabled;
        on(c, 'change', () => { o.drops = c.checked ? [...o.drops, d.id] : o.drops.filter(x => x !== d.id); paint(); });
        l.append(c, el('span', 'grow', d.itemName));
        const noRate = !d.rate || /no rate/i.test(d.rate) || boss?.efficientRate == null;
        const rate = el('span', 'drop-rate' + (noRate ? ' is-missing' : ''), d.rate || t('No rate')); if (noRate) rate.title = t('No drop rate in the catalogue');
        row.append(l, rate);
        if (o.weightsOn && on_) {
          const w = el('span', 'drop-weight'), wl = el('label', null, t('counts as')); wl.htmlFor = `${id}-w-${d.id}`;
          const wi = input(`${id}-w-${d.id}`, o.weights[d.id] ?? '1', value => { o.weights[d.id] = value; refreshFoot(); }, { invalid: parseWhole(o.weights[d.id] ?? '1', 1, 10000) == null, disabled, inputMode: 'numeric' });
          w.append(wl, wi); row.append(w);
        }
        list.append(row);
      }
      for (const d of kept) list.append(el('div', 'drop-row is-on', ed.tile.requirements.flatMap(r => r.drops).find(x => x.id === d)?.itemName || t('Unavailable item')));
      group.append(gh, list); fieldEl.append(group);
    }
    if (oe.drops) fieldEl.append(fieldErr(id + '-drops-err', oe.drops));
    if (oe.weights) fieldEl.append(fieldErr(id + '-weights-err', oe.weights));
    const hint = el('div', 'field-hint'); hint.append(document.createTextNode(t('Missing a drop or rate?') + ' '));
    const link = el('a', 'text-btn', t('Open Catalogue')); link.href = '/Admin/Catalogue'; link.target = '_blank'; link.rel = 'noopener';
    hint.append(link, document.createTextNode(' ' + t('in a new tab; your entries stay here.')));
    fieldEl.append(hint);
    return fieldEl;
  }
  function calcNotes() {
    // Missing catalogue data for the chosen drops (not an EHB calculation, B7).
    const notes = [];
    for (const o of objsOf(ed.f)) for (const id of o.drops) {
      const d = dropById(id); if (!d) continue;
      const boss = bossById(d.bossId);
      if (!d.rate || /no rate/i.test(d.rate)) notes.push(t('{0} has no drop rate in the catalogue.', d.itemName));
      else if (boss && boss.efficientRate == null) notes.push(t('{0} has no kill rate in the catalogue, so {1} can’t be estimated.', boss.name, d.itemName));
    }
    return [...new Set(notes)];
  }
  function estimateSection(ro, busy, errs, locked) {
    const f = ed.f, sec = el('section', 'sec is-last'); sec.setAttribute('aria-labelledby', 'ed-sec-ehb');
    const sh = el('div', 'sec-head'), st = el('h3', 'sec-title', t('Estimate')); st.id = 'ed-sec-ehb';
    const objectivesUnchanged = ed.tileId && JSON.stringify(JSON.parse(snapshot(f)).objs) === JSON.stringify(JSON.parse(ed.base).objs) && f.kind === (JSON.parse(ed.base).kind);
    const calc = objectivesUnchanged ? ed.calc : null;
    const effective = f.kind === 'manual' ? parseDecimal(f.ehb) : f.override ? parseDecimal(f.ehb) : calc;
    const aside = el('span', 'sec-aside'); aside.append(document.createTextNode(t('Effective') + ' '), el('b', null, effective == null ? t('none yet') : fmt1(effective) + ' EHB'));
    sh.append(st, aside); sec.append(sh);
    const box = el('div', 'ehb-box');
    if (f.kind === 'drops') {
      const row = el('div', 'ehb-row'); row.append(el('span', 'grow', t('Calculated from catalogue rates')));
      const val = el('span', 'ehb-val' + (f.override && calc != null ? ' is-struck' : ''), calc != null ? fmt1(calc) + ' EHB' : objectivesUnchanged ? t('Can’t calculate') : t('Calculated when you save'));
      row.append(val); box.append(row);
      for (const note of calcNotes()) {
        const n = el('div', 'ehb-note'); n.append(icon('warning'));
        const sp = el('span', null, note + ' '); const a = el('a', null, t('Correct the rate in Catalogue')); a.href = '/Admin/Catalogue'; a.target = '_blank'; a.rel = 'noopener'; sp.append(a, document.createTextNode(' ' + t('A manual EHB can’t replace a missing rate; approval still needs it.')));
        n.append(sp); box.append(n);
      }
      if (f.override) {
        const r = el('div', 'ehb-row'), l = el('label', 'grow', t('Manual EHB for this tile')); l.htmlFor = 'ed-ehb';
        if (ro || locked) r.append(l, el('span', 'ehb-val', parseDecimal(f.ehb) == null ? '—' : fmt1(parseDecimal(f.ehb)) + ' EHB'));
        else r.append(l, input('ed-ehb', f.ehb, value => { ed.f.ehb = value; refreshFoot(); }, { invalid: !!errs.ehb, describedBy: 'ed-ehb-hint' + (errs.ehb ? ' ed-ehb-err' : ''), disabled: busy, inputMode: 'decimal' }));
        box.append(r);
        if (errs.ehb) box.append(fieldErr('ed-ehb-err', errs.ehb));
        const cs = el('div', 'count-sum'); cs.id = 'ed-ehb-hint'; cs.append(el('span', null, t('Replaces the calculated estimate for this event’s tile only. Catalogue rates aren’t changed.')));
        if (!ro && !locked) { const b = button('text-btn', t('Use calculated'), () => { setF({ override: false, ehb: '' }); requestAnimationFrame(() => ed.content.querySelector('#ed-ehb-set')?.focus()); }, 'ed-ehb-calc'); b.disabled = busy; cs.append(b); }
        box.append(cs);
      } else if (!ro && !locked) {
        const cs = el('div', 'count-sum'); cs.append(el('span', null, t('Uses the calculated estimate.')));
        const b = button('text-btn', t('Set EHB manually'), () => { setF({ override: true, ehb: f.ehb || (calc != null ? String(Math.round(calc * 10) / 10) : '') }); requestAnimationFrame(() => ed.content.querySelector('#ed-ehb')?.focus()); }, 'ed-ehb-set'); b.disabled = busy; cs.append(b);
        box.append(cs);
      }
    } else {
      const r = el('div', 'ehb-row'), l = el('label', 'grow', t('Estimated EHB')); l.htmlFor = 'ed-ehb'; l.append(el('span', 'opt', ' · ' + t('required')));
      if (ro || locked) r.append(l, el('span', 'ehb-val', parseDecimal(f.ehb) == null ? '—' : fmt1(parseDecimal(f.ehb))));
      else r.append(l, input('ed-ehb', f.ehb, value => { ed.f.ehb = value; refreshFoot(); }, { invalid: !!errs.ehb, describedBy: 'ed-ehb-hint' + (errs.ehb ? ' ed-ehb-err' : ''), disabled: busy, inputMode: 'decimal' }));
      box.append(r);
      if (errs.ehb) box.append(fieldErr('ed-ehb-err', errs.ehb));
      const h = el('div', 'field-hint', t('Manual challenges have no drop rates to calculate from, so enter the expected hours for one player.')); h.id = 'ed-ehb-hint'; box.append(h);
    }
    if (locked) box.append(lock(t('Evidence has been submitted for this tile, so its EHB is part of its scoring and can’t change.')));
    sec.append(box);
    const note = ctx.view.mode === 'approved' && !ro ? t('Saving any change returns the approved board to draft.') : ctx.view.mode === 'correction' && !ro ? t('Changes stay private until you publish the correction.') : '';
    if (note) sec.append(el('p', 'field-hint bd-approval-note', note));
    return sec;
  }
  // Typing updates only the footer's unsaved marker; full repaint happens on change.
  function refreshFoot() {
    if (!ed) return;
    const foot = ed.content.querySelector('.dr-foot'), marker = foot?.querySelector('.dirty');
    if (isDirty() && !marker) { const d = el('span', 'dirty'); d.setAttribute('role', 'status'); d.append(el('span', 'dot'), document.createTextNode(t('Unsaved changes'))); foot.querySelector('#ed-cancel')?.before(d); }
    else if (!isDirty() && marker) marker.remove();
    ui.refreshDirty();
  }

  /* ---------------- direct visits and lifecycle ---------------- */
  (ctx.hooks.ready ||= []).push(() => {
    const ref = new URL(location.href).searchParams.get('tile');
    if (!ref || ctx.view.mode === 'none') return;
    const pos = parseTileRef(ref, ctx.view.rows, ctx.view.cols);
    if (pos == null || (!ctx.tileAt(pos) && !ctx.canEdit())) { ctx.notice = unknownTile(); ctx.paint(); return; }
    ctx.focusPos = pos;
    void open(pos, { push: false });
  });
  (ctx.hooks.afterPaint ||= []).push(() => { if (ed && !ed.loading) { ed.tileView = ed.tileId ? ctx.view.tiles.find(x => x.id === ed.tileId) || ed.tileView : null; if (!ed.busy) paint(); } });
  (ctx.hooks.release ||= []).push(() => { unregisterUrl(); if (ed) void ed.layer.close(true); });
  ctx.editorOpen = () => !!ed;
}

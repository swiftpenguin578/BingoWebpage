// Shared .dtp production adapter. Calendar/typing/keyboard rules ported from
// ui/behavior.js; page lifecycle owns listeners and canonical local values.
const focusSoon = id => requestAnimationFrame(() => document.getElementById(id)?.focus({ preventScroll: true }));
const afterExit = (cmp, selector, done) => { const el = document.querySelector(selector); if (!el) return done(); const ms = parseFloat(getComputedStyle(el).animationDuration) * 1000; setTimeout(() => { if (!cmp.disposed) done(); }, Number.isFinite(ms) ? ms : 0); };
const trapTab = (event, id) => { const items = [...document.getElementById(id).querySelectorAll('button:not([disabled]),[tabindex="0"]')].filter(el => el.tabIndex >= 0), at = items.indexOf(document.activeElement); if (event.shiftKey && at <= 0 || !event.shiftKey && at === items.length - 1) { event.preventDefault(); items[event.shiftKey ? items.length - 1 : 0]?.focus(); } };
  var MONTHS = ['January', 'February', 'March', 'April', 'May', 'June', 'July', 'August', 'September', 'October', 'November', 'December'];
  var MON3 = MONTHS.map(function (m) { return m.slice(0, 3); });
  var WD = [['Mo', 'Monday'], ['Tu', 'Tuesday'], ['We', 'Wednesday'], ['Th', 'Thursday'], ['Fr', 'Friday'], ['Sa', 'Saturday'], ['Su', 'Sunday']];
  function pad(n) { return (n < 10 ? '0' : '') + n; }
  function ymdOf(y, m, d) { return y + '-' + pad(m) + '-' + pad(d); }
  function daysIn(y, m) { return new Date(Date.UTC(y, m, 0)).getUTCDate(); }
  function splitValue(v) { var x = /^(\d{4})-(\d{2})-(\d{2})T(\d{2}):(\d{2})$/.exec(v || ''); return x ? { y: +x[1], m: +x[2], d: +x[3], h: +x[4], mi: +x[5] } : null; }
  function splitYmd(s) { var x = /^(\d{4})-(\d{2})-(\d{2})$/.exec(s || ''); return x ? { y: +x[1], m: +x[2], d: +x[3] } : null; }
  function addDays(p, n) { var t = new Date(Date.UTC(p.y, p.m - 1, p.d + n)); return { y: t.getUTCFullYear(), m: t.getUTCMonth() + 1, d: t.getUTCDate() }; }
  function addMonths(p, n) { var mm = p.m - 1 + n, y = p.y + Math.floor(mm / 12); mm = ((mm % 12) + 12) % 12; return { y: y, m: mm + 1, d: Math.min(p.d, daysIn(y, mm + 1)) }; }
  function weekday(p) { return (new Date(Date.UTC(p.y, p.m - 1, p.d)).getUTCDay() + 6) % 7; } // Monday = 0
  function localeMonths() { if(globalThis.document?.documentElement?.lang!=='da')return MON3;return MONTHS.map((_,i)=>new Intl.DateTimeFormat(globalThis.document?.documentElement?.lang==='da'?'da-DK':'en-GB',{month:'short',timeZone:'UTC'}).format(new Date(Date.UTC(2026,i,1)))); }
  function fmtDate(p) { return p ? p.d + ' ' + localeMonths()[p.m - 1] + ' ' + p.y : ''; }
  function fmtTime(p) { return p ? pad(p.h) + ':' + pad(p.mi) : ''; }
  function parseDate(t) {
    t = String(t || '').trim(); if (!t) return { empty: true };
    var y, m, d, x;
    if ((x = /^(\d{4})-(\d{1,2})-(\d{1,2})$/.exec(t))) { y = +x[1]; m = +x[2]; d = +x[3]; }
    else if ((x = /^(\d{1,2})[.\/\- ](\d{1,2})[.\/\- ](\d{4})$/.exec(t))) { d = +x[1]; m = +x[2]; y = +x[3]; }
    else if ((x = /^(\d{1,2})\.?\s*([A-Za-z]{3,})\.?,?\s*(\d{4})$/.exec(t))) {
      var k = x[2].slice(0, 3).toLowerCase(); m = MON3.map(function (s) { return s.toLowerCase(); }).indexOf(k) + 1; if(!m)m=localeMonths().map(s=>s.slice(0,3).toLowerCase()).indexOf(k)+1; d = +x[1]; y = +x[3];
    }
    if (!y || !m || m > 12 || !d || d > daysIn(y, m) || y < 2000 || y > 2100) return { error: 'Enter a date like 27 Jun 2027.' };
    return { y: y, m: m, d: d };
  }
  function parseTime(t) {
    t = String(t || '').trim(); if (!t) return { empty: true };
    var x = /^(\d{1,2})(?:[:.]?(\d{2}))?$/.exec(t);
    if (!x) return { error: 'Enter a time like 18:30.' };
    var h = +x[1], mi = x[2] == null ? 0 : +x[2];
    if (h > 23 || mi > 59) return { error: 'Enter a time like 18:30.' };
    if (mi % 5) return { error: 'Choose a time in five-minute steps.' };
    return { h: h, mi: mi };
  }
  // Local wall time in a timezone -> UTC ms. status: 'ok' | 'invalid' (skipped by a clock change) |
  // 'ambiguous' (happens twice). Uses the browser's timezone data, like the application's TimeZoneInfo.
  function offsetMs(ms, tz) {
    try {
      var parts = new Intl.DateTimeFormat('en-GB', { timeZone: tz, hourCycle: 'h23', year: 'numeric', month: '2-digit', day: '2-digit', hour: '2-digit', minute: '2-digit', second: '2-digit' }).formatToParts(new Date(ms));
      var g = {}; parts.forEach(function (p) { g[p.type] = +p.value; });
      return Date.UTC(g.year, g.month - 1, g.day, g.hour, g.minute, g.second) - Math.floor(ms / 1000) * 1000;
    } catch (e) { throw new Error('Unsupported event timezone'); }
  }
  function localToUtc(value, tz) {
    var p = splitValue(value); if (!p) return { status: 'empty', ms: null };
    var wall = Date.UTC(p.y, p.m - 1, p.d, p.h, p.mi);
    var seen = {}, hits = [];
    [offsetMs(wall - 864e5, tz), offsetMs(wall + 864e5, tz)].forEach(function (o) {
      var utc = wall - o; if (seen[utc]) return; seen[utc] = 1;
      if (offsetMs(utc, tz) === o) hits.push(utc);
    });
    if (!hits.length) return { status: 'invalid', ms: null };
    if (hits.length > 1) return { status: 'ambiguous', ms: Math.min.apply(null, hits) };
    return { status: 'ok', ms: hits[0] };
  }
  function utcToLocal(ms, tz) {
    if (ms == null) return '';
    var t = new Date(ms + offsetMs(ms, tz));
    return ymdOf(t.getUTCFullYear(), t.getUTCMonth() + 1, t.getUTCDate()) + 'T' + pad(t.getUTCHours()) + ':' + pad(t.getUTCMinutes());
  }
  function todayIn(tz, now) { return utcToLocal(now == null ? Date.now() : now, tz).slice(0, 10); }

  var dtpOpen = null; // { cmp, id } of the open popup (one at a time)
  function pkGet(cmp, id) { return (cmp.state.dtp && cmp.state.dtp[id]) || {}; }
  function pkSet(cmp, id, patch, done) {
    cmp.setState(function (s) { var all = Object.assign({}, s.dtp || {}); all[id] = Object.assign({}, all[id] || {}, patch); return { dtp: all }; }, done);
  }
  // Texts being typed take precedence over the stored value until they are committed.
  function texts(cmp, id, value) {
    var st = pkGet(cmp, id), p = splitValue(value);
    return { date: st.dateText != null ? st.dateText : fmtDate(p), time: st.timeText != null ? st.timeText : fmtTime(p) };
  }
  // What the typed texts mean: { value, error } — value is the committed string, or undefined while invalid.
  function interpret(dateText, timeText) {
    var d = parseDate(dateText), t = parseTime(timeText);
    if (d.empty && t.empty) return { value: '' };
    if (d.error) return { error: d.error, part: 'date' };
    if (d.empty) return { error: 'Enter a date.', part: 'date' };
    if (t.error) return { error: t.error, part: 'time' };
    if (t.empty) return { error: 'Enter a time.', part: 'time' };
    return { value: ymdOf(d.y, d.m, d.d) + 'T' + pad(t.h) + ':' + pad(t.mi) };
  }
  // The picker's own validation for a page to combine with its rules: { error, pending }.
  function dtpStatus(cmp, id, value) {
    var x = texts(cmp, id, value), r = interpret(x.date, x.time);
    return { error: r.error || null, pending: r.value === undefined };
  }
  function dtpReset(cmp, id) {
    cmp.setState(function (s) { if (!s.dtp) return null; if (id == null) return { dtp: {} }; var all = Object.assign({}, s.dtp); delete all[id]; return { dtp: all }; });
  }
  function popSize() { var vw = window.innerWidth; return { w: Math.min(404, vw - 16), stacked: vw < 440 }; }
  function placePop(fieldEl, stacked) {
    var r = fieldEl.getBoundingClientRect(), s = popSize(), h = stacked ? 492 : 336;
    var x = Math.max(8, Math.min(r.left, window.innerWidth - s.w - 8)), y = r.bottom + 6, up = false;
    if (y + h > window.innerHeight - 8 && r.top - h - 6 >= 8) { y = r.top - h - 6; up = true; }
    else if (y + h > window.innerHeight - 8) y = Math.max(8, window.innerHeight - h - 8);
    return { x: Math.round(x), y: Math.round(y), w: Math.round(s.w), up: up, stacked: s.stacked };
  }
  function onDocPointer(e) {
    if (!dtpOpen) return;
    var f = document.getElementById(dtpOpen.id + '-field'), p = document.getElementById(dtpOpen.id + '-pop');
    if ((f && f.contains(e.target)) || (p && p.contains(e.target))) return;
    dtpClose(dtpOpen.cmp, dtpOpen.id, false);
  }
  function onReflow() {
    if (!dtpOpen) return;
    var f = document.getElementById(dtpOpen.id + '-field'); if (!f) return;
    var st = pkGet(dtpOpen.cmp, dtpOpen.id); pkSet(dtpOpen.cmp, dtpOpen.id, placePop(f, st.stacked));
  }


  function focusDay(id, p) { focusSoon(id + '-d-' + ymdOf(p.y, p.m, p.d), { preventScroll: true }); }
  function scrollOptions(id) {
    requestAnimationFrame(function () { requestAnimationFrame(function () {
      ['-hours', '-mins'].forEach(function (k) {
        var col = document.getElementById(id + k), sel = col && col.querySelector('[aria-selected=true]');
        if (col && sel) col.scrollTop += sel.getBoundingClientRect().top - col.getBoundingClientRect().top - col.clientHeight / 2 + sel.offsetHeight / 2;
      });
    }); });
  }
  function dtpOpenPop(cmp, cfg) {
    if (dtpOpen && (dtpOpen.cmp !== cmp || dtpOpen.id !== cfg.id)) dtpClose(dtpOpen.cmp, dtpOpen.id, false);
    var f = document.getElementById(cfg.id + '-field'); if (!f) return;
    var p = splitValue(cfg.value) || null;
    var x = texts(cmp, cfg.id, cfg.value), typed = parseDate(x.date);
    var base = typed.y ? typed : p || splitYmd(cfg.today) || splitYmd(todayIn('UTC'));
    var place = placePop(f, popSize().stacked);
    dtpOpen = { cmp: cmp, id: cfg.id };
    pkSet(cmp, cfg.id, Object.assign({ open: true, closing: false, vy: base.y, vm: base.m, fd: { y: base.y, m: base.m, d: base.d } }, place), function () { focusDay(cfg.id, base); scrollOptions(cfg.id); });
  }
  function dtpClose(cmp, id, restore) {
    var st = pkGet(cmp, id); if (!st.open || st.closing) return;
    if (dtpOpen && dtpOpen.cmp === cmp && dtpOpen.id === id) dtpOpen = null;
    pkSet(cmp, id, { closing: true }, function () {
      afterExit(cmp, '#' + id + '-pop', function () { if (pkGet(cmp, id).closing) pkSet(cmp, id, { open: false, closing: false }); });
      if (restore) focusSoon(id + '-btn', { preventScroll: true });
    });
  }
  // Commit a full value from the popup (keeps whichever part wasn't touched).
  function commit(cmp, cfg, patch) {
    var cur = splitValue(cfg.value), x = texts(cmp, cfg.id, cfg.value);
    var d = patch.date || (parseDate(x.date).y ? parseDate(x.date) : cur);
    var t = parseTime(x.time); t = patch.time || (t.h != null ? t : cur ? { h: cur.h, mi: cur.mi } : null);
    if (!t && patch.date) { var dt = parseTime(cfg.defaultTime || '12:00'); t = { h: dt.h, mi: dt.mi }; }
    if (!d) { var td = splitYmd(cfg.today); d = td; }
    var v = ymdOf(d.y, d.m, d.d) + 'T' + pad(t.h) + ':' + pad(t.mi);
    pkSet(cmp, cfg.id, { dateText: null, timeText: null, touched: true });
    cfg.onChange(v);
  }

  // View model for one picker. cfg: { id, label, labelId, value, onChange(v), today ('YYYY-MM-DD' in the
  // event timezone), min (optional 'YYYY-MM-DD'; earlier days are disabled), defaultTime ('HH:mm' used when a
  // date is picked before any time), disabled, readOnly, clearable, invalid (the page's own error),
  // showErrors (show typing errors without waiting for blur), describedBy }.
  function dtpView(cmp, cfg) {
    var id = cfg.id, st = pkGet(cmp, id), x = texts(cmp, id, cfg.value), r = interpret(x.date, x.time);
    var typingErr = r.error && (st.touched || cfg.showErrors) ? r.error : '';
    var sel = splitValue(cfg.value), today = splitYmd(cfg.today), min = splitYmd(cfg.min);
    var minKey = min ? ymdOf(min.y, min.m, min.d) : '';
    var open = !!st.open && !cfg.disabled && !cfg.readOnly;
    var vm = { id: id, label: cfg.label, labelId: cfg.labelId,
      cls: (open ? 'is-open ' : '') + (cfg.disabled ? 'is-disabled ' : '') + (cfg.readOnly ? 'is-readonly ' : '') + (cfg.invalid || typingErr ? 'is-invalid' : ''),
      dateText: x.date, timeText: x.time, disabled: !!cfg.disabled, readOnly: !!cfg.readOnly, editable: !cfg.disabled && !cfg.readOnly,
      invalid: cfg.invalid || typingErr ? 'true' : 'false', hasTypingErr: !!typingErr, typingErr: typingErr, typingErrId: id + '-terr',
      describedBy: [cfg.describedBy, typingErr ? id + '-terr' : ''].filter(Boolean).join(' ') || undefined,
      expanded: open ? 'true' : 'false', btnLabel: 'Choose ' + (cfg.label || 'date and time').toLowerCase() + ' from a calendar',
      open: open, popCls: (st.up ? 'is-up ' : '') + (st.closing ? 'is-closing ' : '') + (st.stacked ? 'is-stacked' : ''), x: st.x || 0, y: st.y || 0, w: st.w || 392,
      onDate: function (e) { var v = e.target.value; var t2 = texts(cmp, id, cfg.value); var r2 = interpret(v, t2.time); pkSet(cmp, id, { dateText: v, timeText: t2.time }); if (r2.value !== undefined && r2.value !== cfg.value) cfg.onChange(r2.value); },
      onTime: function (e) { var v = e.target.value; var t2 = texts(cmp, id, cfg.value); var r2 = interpret(t2.date, v); pkSet(cmp, id, { dateText: t2.date, timeText: v }); if (r2.value !== undefined && r2.value !== cfg.value) cfg.onChange(r2.value); },
      onBlur: function (e) {
        var to = e.relatedTarget, f = document.getElementById(id + '-field');
        if (to && f && f.contains(to)) return;
        var t2 = texts(cmp, id, cfg.value), r2 = interpret(t2.date, t2.time);
        // A valid entry is normalised ("27/6/2027" -> "27 Jun 2027"); an invalid one stays as typed.
        if (r2.value !== undefined) pkSet(cmp, id, { dateText: null, timeText: null, touched: true }); else pkSet(cmp, id, { touched: true });
      },
      onKey: function (e) {
        if (e.key === 'Escape' && pkGet(cmp, id).open) { e.preventDefault(); e.stopPropagation(); dtpClose(cmp, id, true); return; }
        if ((e.key === 'ArrowDown' && e.altKey) || (e.key === 'ArrowDown' && e.target.id === id + '-date' && !e.target.value)) { e.preventDefault(); dtpOpenPop(cmp, cfg); }
        else if (e.key === 'Enter') { var t2 = texts(cmp, id, cfg.value); if (interpret(t2.date, t2.time).value !== undefined) pkSet(cmp, id, { dateText: null, timeText: null, touched: true }); else pkSet(cmp, id, { touched: true }); }
      },
      toggle: function () { if (pkGet(cmp, id).open) dtpClose(cmp, id, true); else dtpOpenPop(cmp, cfg); },
      weekdays: WD.map(function (w) { return { short: w[0], long: w[1] }; }),
      weeks: [], hours: [], mins: [], monthLabel: '', clearable: !!cfg.clearable
    };
    if (!open) return vm;
    var vy = st.vy, vmo = st.vm, fd = st.fd || { y: vy, m: vmo, d: 1 };
    var first = { y: vy, m: vmo, d: 1 }, start = addDays(first, -weekday(first));
    var selKey = sel ? ymdOf(sel.y, sel.m, sel.d) : '', todayKey = today ? ymdOf(today.y, today.m, today.d) : '', fKey = ymdOf(fd.y, fd.m, fd.d);
    for (var w = 0; w < 6; w++) {
      var days = [];
      for (var i = 0; i < 7; i++) {
        var p = addDays(start, w * 7 + i), key = ymdOf(p.y, p.m, p.d);
        var outside = p.m !== vmo, isSel = key === selKey, isToday = key === todayKey, dis = !!minKey && key < minKey;
        days.push((function (p, key, outside, isSel, isToday, dis) { return {
          id: id + '-d-' + key, n: p.d, ti: key === fKey ? '0' : '-1', sel: isSel ? 'true' : 'false', cur: isToday ? 'date' : undefined, dis: dis ? 'true' : undefined,
          cls: (outside ? 'is-outside ' : '') + (isSel ? 'is-selected ' : '') + (isToday ? 'is-today ' : '') + (dis ? 'is-disabled' : ''),
          aria: WD[weekday(p)][1] + ' ' + p.d + ' ' + MONTHS[p.m - 1] + ' ' + p.y + (isToday ? ', today' : '') + (dis ? ', unavailable' : ''),
          pick: function () { if (dis) return; commit(cmp, cfg, { date: p }); pkSet(cmp, id, { vy: p.y, vm: p.m, fd: p }, function () { focusSoon(id + '-h-' + (splitValue(cfg.value) ? splitValue(cfg.value).h : parseTime(cfg.defaultTime || '12:00').h), { preventScroll: true }); scrollOptions(id); }); }
        }; })(p, key, outside, isSel, isToday, dis));
      }
      vm.weeks.push({ key: 'w' + w, days: days });
    }
    vm.monthLabel = MONTHS[vmo - 1] + ' ' + vy;
    var go = function (n) { var t = addMonths(fd, n); pkSet(cmp, id, { vy: t.y, vm: t.m, fd: t }); };
    vm.prev = function () { go(-1); }; vm.next = function () { go(1); };
    var curT = parseTime(x.time); if (curT.h == null && sel) curT = { h: sel.h, mi: sel.mi };
    for (var h = 0; h < 24; h++) vm.hours.push((function (h) { var on = curT.h === h; return { id: id + '-h-' + h, label: pad(h), sel: on ? 'true' : 'false', ti: on || (curT.h == null && h === 12) ? '0' : '-1', cls: on ? 'is-selected' : '', pick: function () { commit(cmp, cfg, { time: { h: h, mi: curT.mi != null ? curT.mi : 0 } }); } }; })(h));
    for (var m = 0; m < 60; m += 5) vm.mins.push((function (m) { var on = curT.mi === m && curT.h != null; return { id: id + '-m-' + m, label: pad(m), sel: on ? 'true' : 'false', ti: on || (!on && curT.mi == null && m === 0) || (curT.h != null && curT.mi === m) ? '0' : '-1', cls: on ? 'is-selected' : '', pick: function () { commit(cmp, cfg, { time: { h: curT.h != null ? curT.h : 12, mi: m } }); } }; })(m));
    if (!vm.mins.some(function (o) { return o.ti === '0'; })) vm.mins[0].ti = '0';
    vm.today = function () { var t = splitYmd(cfg.today); if (!t || (minKey && cfg.today < minKey)) return; commit(cmp, cfg, { date: t }); pkSet(cmp, id, { vy: t.y, vm: t.m, fd: t }, function () { focusDay(id, t); }); };
    vm.todayDisabled = !today || (!!minKey && cfg.today < minKey);
    vm.clear = function () { pkSet(cmp, id, { dateText: null, timeText: null, touched: false }); cfg.onChange(''); dtpClose(cmp, id, true); };
    vm.done = function () { dtpClose(cmp, id, true); };
    vm.popKey = function (e) {
      var t = e.target;
      if (e.key === 'Escape') { e.preventDefault(); e.stopPropagation(); dtpClose(cmp, id, true); return; }
      if (e.key === 'Tab') { trapTab(e, id + '-pop'); e.stopPropagation(); return; }
      if (t.classList.contains('dtp-day')) {
        var dlt = { ArrowLeft: -1, ArrowRight: 1, ArrowUp: -7, ArrowDown: 7 }[e.key], nd = null;
        if (dlt) nd = addDays(fd, dlt);
        else if (e.key === 'PageUp') nd = addMonths(fd, e.shiftKey ? -12 : -1);
        else if (e.key === 'PageDown') nd = addMonths(fd, e.shiftKey ? 12 : 1);
        else if (e.key === 'Home') nd = addDays(fd, -weekday(fd));
        else if (e.key === 'End') nd = addDays(fd, 6 - weekday(fd));
        if (nd) { e.preventDefault(); e.stopPropagation(); pkSet(cmp, id, { vy: nd.y, vm: nd.m, fd: nd }, function () { focusDay(id, nd); }); }
        return;
      }
      if (t.classList.contains('dtp-opt')) {
        var col = t.parentNode, opts = Array.prototype.slice.call(col.querySelectorAll('.dtp-opt')), i = opts.indexOf(t), j = null;
        if (e.key === 'ArrowDown') j = Math.min(opts.length - 1, i + 1); else if (e.key === 'ArrowUp') j = Math.max(0, i - 1);
        else if (e.key === 'Home') j = 0; else if (e.key === 'End') j = opts.length - 1;
        if (j != null) { e.preventDefault(); e.stopPropagation(); opts[j].click(); var nid = opts[j].id; focusSoon(nid, { preventScroll: true }); requestAnimationFrame(function () { var el = document.getElementById(nid); if (el && el.scrollIntoView) el.scrollIntoView({ block: 'nearest' }); }); }
      }
    };
    return vm;
  }
  var dtp = { view: dtpView, status: dtpStatus, reset: dtpReset, close: dtpClose, isOpen: function (cmp, id) { return !!pkGet(cmp, id).open; },
    parseDate: parseDate, parseTime: parseTime, fmtDate: fmtDate, fmtTime: fmtTime, split: splitValue, localToUtc: localToUtc, utcToLocal: utcToLocal, todayIn: todayIn };


export { localToUtc, utcToLocal, todayIn };
export function mountDateTime(root, input, onChange, template) {
  const id = root.dataset.dateTime, life = new AbortController();
  const date = root.querySelector('.dtp-date'), time = root.querySelector('.dtp-time'), button = root.querySelector('.dtp-btn');
  const labels = template.dataset, translate = text => labels[Object.keys(labels).find(key => key.startsWith('message') && labels[key + 'Source'] === text)] || text;
  const receivedAt=Date.now(), now=()=>root.dataset.now?Date.parse(root.dataset.now)+Date.now()-receivedAt:Date.now();
  let disabled = false, invalid = false, showErrors = false, popup = null, vm;
  const cmp = { state: {}, disposed: false, setState(patch, done) { this.state = { ...this.state, ...(typeof patch === 'function' ? patch(this.state) : patch) }; render(); done?.(); } };
  const listen = (el, event, fn, capture = false) => el.addEventListener(event, fn, { signal: life.signal, capture });
  function config() { return { id, label: root.dataset.label, labelId: `label-${id}`, value: input.value, today: todayIn(root.dataset.timezone,now()), min: todayIn(root.dataset.timezone,now()), invalid, clearable: root.dataset.clearable === 'true', disabled, showErrors, onChange(value) { input.value = value; render(); onChange(value); } }; }
  function render() {
    if (cmp.disposed) return;
    vm = dtpView(cmp, config()); root.className = `dtp ${vm.cls}`;
    if (date.value !== vm.dateText) date.value = vm.dateText;
    if (time.value !== vm.timeText) time.value = vm.timeText;
    for (const control of [date, time, button]) control.disabled = disabled;
    date.setCustomValidity(translate(vm.typingErr)); time.setCustomValidity(translate(vm.typingErr));
    date.setAttribute('aria-invalid', vm.invalid); time.setAttribute('aria-invalid', vm.invalid); button.setAttribute('aria-expanded', vm.expanded);
    let error = root.parentNode.querySelector('[data-picker-error]');
    if (!error) { error = document.createElement('div'); error.className = 'field-err'; error.dataset.pickerError = ''; error.id = `${id}-terr`; root.after(error); }
    error.textContent = translate(vm.typingErr); error.hidden = !vm.hasTypingErr;
    for (const control of [date, time]) control.setAttribute('aria-describedby', `${control.getAttribute('aria-describedby')?.replace(` ${id}-terr`, '') || ''}${vm.hasTypingErr ? ` ${id}-terr` : ''}`.trim());
    if (!vm.open) { popup?.remove(); popup = null; return; }
    const active = popup?.contains(document.activeElement) ? document.activeElement.id : null;
    const scroll = [...(popup?.querySelectorAll('.dtp-col') || [])].map(el => el.scrollTop);
    if (!popup) { popup = template.content.firstElementChild.cloneNode(true); root.append(popup); listen(popup, 'keydown', event => vm.popKey(event)); }
    popup.id = `${id}-pop`; popup.className = `dtp-pop ${vm.popCls}`;
    popup.setAttribute('aria-label', `${root.dataset.label}: ${labels.choose}`);
    Object.assign(popup.style, { left: `${vm.x}px`, top: `${vm.y}px`, width: `${vm.w}px` });
    const month = popup.querySelector('.dtp-month'); month.id = `${id}-month`; month.textContent = new Intl.DateTimeFormat(document.documentElement.lang, { month: 'long', year: 'numeric', timeZone: 'UTC' }).format(new Date(Date.UTC(cmp.state.dtp[id].vy, cmp.state.dtp[id].vm - 1)));
    popup.querySelector('table').setAttribute('aria-labelledby', month.id);
    for (const action of ['prev','next','today','clear','done']) { const el = popup.querySelector(`[data-picker-${action}]`); el.onclick = () => vm[action](); if (action === 'clear') el.hidden = !vm.clearable; if (action === 'today') el.disabled = vm.todayDisabled; }
    const head = popup.querySelector('thead tr'); head.replaceChildren(...vm.weekdays.map((day,i) => { const el = document.createElement('th'); el.scope = 'col'; el.textContent = new Intl.DateTimeFormat(document.documentElement.lang,{weekday:'short',timeZone:'UTC'}).format(new Date(Date.UTC(2024,0,1+i))); return el; }));
    popup.querySelector('tbody').replaceChildren(...vm.weeks.map(week => { const tr = document.createElement('tr'); for (const day of week.days) { const td = document.createElement('td'); td.setAttribute('role','gridcell'); td.setAttribute('aria-selected',day.sel); const b = document.createElement('button'); b.type='button'; b.className=`dtp-day ${day.cls}`; b.id=day.id; b.tabIndex=Number(day.ti); b.textContent=day.n; b.setAttribute('aria-label',new Intl.DateTimeFormat(document.documentElement.lang,{dateStyle:'full',timeZone:'UTC'}).format(new Date(day.id.slice(-10)+'T12:00Z'))); if(day.cur)b.setAttribute('aria-current',day.cur); if(day.dis)b.setAttribute('aria-disabled',day.dis); b.onclick=day.pick; td.append(b); tr.append(td); } return tr; }));
    ['hours','mins'].forEach((name,index) => { const col=popup.querySelector(`[data-picker-${name}]`); col.id=id+'-'+name; const label=col.previousElementSibling; label.id=id+'-'+name+'-label'; col.setAttribute('aria-labelledby',label.id); col.replaceChildren(...vm[name].map(option=>{ const b=document.createElement('button'); b.type='button'; b.className=`dtp-opt ${option.cls}`; b.id=option.id; b.textContent=option.label; b.tabIndex=Number(option.ti); b.setAttribute('role','option'); b.setAttribute('aria-selected',option.sel); b.onclick=option.pick; return b; })); col.scrollTop=scroll[index]||0; });
    if (active) document.getElementById(active)?.focus({preventScroll:true});
  }
  listen(date,'input',event=>vm.onDate(event)); listen(time,'input',event=>vm.onTime(event));
  for (const el of [date,time]) { listen(el,'blur',event=>vm.onBlur(event)); listen(el,'keydown',event=>vm.onKey(event)); }
  listen(button,'click',()=>vm.toggle()); listen(document,'pointerdown',onDocPointer,true); listen(window,'resize',onReflow); listen(window,'scroll',event=>{ if(!event.target?.closest?.('.dtp-pop'))onReflow(); },true);
  render();
  return { isOpen() {return !!pkGet(cmp,id).open;}, close() {dtpClose(cmp,id,true);}, setInvalid(value) {invalid=value;render();}, refresh() { dtpReset(cmp,id); render(); }, setDisabled(value) { disabled=value; render(); }, validate() { showErrors=true; render(); return !dtpStatus(cmp,id,input.value).error; }, dirty() { const parsed=splitValue(input.value); return date.value!==fmtDate(parsed)||time.value!==fmtTime(parsed); }, dispose() { cmp.disposed=true; life.abort(); if(dtpOpen?.cmp===cmp)dtpOpen=null; popup?.remove(); } };
}

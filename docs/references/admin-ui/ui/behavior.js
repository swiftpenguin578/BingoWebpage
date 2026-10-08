/* DK Legacy Admin — shared interaction behaviour (window.DKAdmin)
   Extracted from the approved Participants page. Pure helpers: nothing here draws UI.
   Pages keep their own state and call these for focus, dismissal, keyboard and timing so every
   overlay behaves the same. A production implementation re-creates these contracts; this file is
   reference code for the design canvas, not an application library. */
(function () {
  'use strict';

  // How long toasts stay (display time, not animation). Animation timing is never duplicated here:
  // exits are measured from the element's computed CSS animation (see afterExit), so tokens.css is
  // the only source of durations and prefers-reduced-motion is respected automatically.
  var motion = { toastLife: 4500, toastLifeWithAction: 7000, toastMax: 3 };
  var EXIT_SLACK = 20;  // ms after the measured animation, so `forwards` exits finish before unmount

  function toMs(v) { v = String(v).trim(); var n = parseFloat(v) || 0; return /ms$/.test(v) ? n : n * 1000; }
  function resolve(targets) {
    if (!targets) return [];
    if (typeof targets === 'string') return Array.prototype.slice.call(document.querySelectorAll(targets));
    return (Array.isArray(targets) ? targets : [targets]).filter(Boolean);
  }
  // Longest finite animation (duration + delay) currently applied to the targets, in ms.
  // 0 when an element is missing, hidden or not animating.
  function animationMs(targets) {
    var max = 0;
    resolve(targets).forEach(function (el) {
      var cs = getComputedStyle(el); if (cs.display === 'none' || cs.animationName === 'none') return;
      var names = cs.animationName.split(','), durs = cs.animationDuration.split(','), dels = cs.animationDelay.split(','), its = cs.animationIterationCount.split(',');
      names.forEach(function (n, i) {
        if (n.trim() === 'none' || its[i % its.length].trim() === 'infinite') return;
        max = Math.max(max, toMs(durs[i % durs.length]) + toMs(dels[i % dels.length]));
      });
    });
    return max;
  }
  // Run fn once the targets' current (exit) animation has finished; immediately-ish when nothing animates
  // (reduced motion collapses animations to 1ms). Call after the state that applies the exit class is committed.
  function afterExit(cmp, targets, fn) {
    var ms = animationMs(targets);
    return cmp.later(fn, ms > 0 ? ms + EXIT_SLACK : 0);
  }

  function reducedMotion() {
    try { return window.matchMedia('(prefers-reduced-motion: reduce)').matches; } catch (e) { return false; }
  }

  // Focus an element after the next paint (the element usually mounts in the same update).
  // Pass {preventScroll:true} when opening an overlay so the page doesn't jump.
  function focusSoon(id, opts) {
    requestAnimationFrame(function () { requestAnimationFrame(function () {
      var el = document.getElementById(id); if (el) el.focus(opts || {});
    }); });
  }

  // Return focus to the control that opened a layer; fall back to a stable id if it has gone.
  function restoreFocus(el, fallbackId) {
    requestAnimationFrame(function () { requestAnimationFrame(function () {
      if (el && document.contains(el) && !el.disabled) { el.focus({ preventScroll: true }); return; }
      var f = fallbackId && document.getElementById(fallbackId);
      if (f && !f.disabled) f.focus({ preventScroll: true });
    }); });
  }

  // Keep Tab / Shift+Tab inside a drawer, modal or mobile nav. Radio groups count as one stop.
  // Focus on the container itself (dialogs use tabindex="-1") wraps like any other stop: Shift+Tab goes to the
  // last control, Tab to the first. With no enabled controls (e.g. every button disabled while saving), focus
  // stays on the container instead of escaping to the page behind.
  function trapTab(e, containerId) {
    if (e.key !== 'Tab') return;
    var c = document.getElementById(containerId); if (!c) return;
    var f = Array.prototype.filter.call(
      c.querySelectorAll('button:not([disabled]),input:not([disabled]),textarea:not([disabled]),select:not([disabled]),a[href]'),
      function (el) {
        if (el.offsetParent === null) return false;
        var g = el.type === 'radio' && el.closest('[role=radiogroup]');
        return !(g && !el.checked && g.querySelector('input[type=radio]:checked'));
      });
    var active = document.activeElement;
    if (!f.length) { e.preventDefault(); if (active !== c && c.hasAttribute('tabindex')) c.focus(); return; }
    var first = f[0], last = f[f.length - 1];
    if (active === c || !c.contains(active)) { e.preventDefault(); (e.shiftKey ? last : first).focus(); return; }
    if (e.shiftKey && document.activeElement === first) { e.preventDefault(); last.focus(); }
    else if (!e.shiftKey && document.activeElement === last) { e.preventDefault(); first.focus(); }
  }

  // Menu keyboard: arrows wrap, Home/End jump, Escape and Tab close (restoring focus via onClose).
  function menuKeydown(e, containerId, onClose) {
    var c = document.getElementById(containerId); if (!c) return;
    var items = Array.prototype.slice.call(c.querySelectorAll('[role^=menuitem]:not([disabled])'));
    var i = items.indexOf(document.activeElement);
    if (e.key === 'ArrowDown') { e.preventDefault(); (items[(i + 1) % items.length] || items[0]).focus(); }
    else if (e.key === 'ArrowUp') { e.preventDefault(); (items[(i - 1 + items.length) % items.length] || items[0]).focus(); }
    else if (e.key === 'Home') { e.preventDefault(); if (items[0]) items[0].focus(); }
    else if (e.key === 'End') { e.preventDefault(); if (items.length) items[items.length - 1].focus(); }
    else if (e.key === 'Escape' || e.key === 'Tab') { e.preventDefault(); onClose(); }
  }
  function focusFirstMenuItem(containerId) {
    requestAnimationFrame(function () {
      var c = document.getElementById(containerId);
      var f = c && c.querySelector('[role^=menuitem]:not([disabled])'); if (f) f.focus();
    });
  }

  // Result-list keyboard (searchable pickers): ArrowDown/Up move between enabled options; ArrowUp on
  // the first returns to the search field.
  function listKeydown(e, containerId, itemSelector, searchId) {
    if (e.key !== 'ArrowDown' && e.key !== 'ArrowUp') return;
    var c = document.getElementById(containerId); if (!c) return;
    var items = Array.prototype.slice.call(c.querySelectorAll(itemSelector)); if (!items.length) return;
    e.preventDefault();
    var i = items.indexOf(document.activeElement);
    if (e.key === 'ArrowDown') (items[i + 1] || items[0]).focus();
    else if (i <= 0) { var s = document.getElementById(searchId); if (s) s.focus(); }
    else items[i - 1].focus();
  }

  // Menu geometry: item 34px, separator 9px, header 44px, 12px padding.
  function menuHeight(items) {
    return items.reduce(function (n, it) { return n + (it.type === 'sep' ? 9 : it.type === 'header' ? 44 : 34); }, 12);
  }
  // Place a menu against its trigger. align: 'end' (row actions: right-aligned, flips up near the
  // bottom), 'start' (below, left-aligned), 'side' (to the right; collapsed sidebar). Kept 8px inside
  // the viewport.
  function placeMenu(rect, opts) {
    var w = opts.width || 236, h = opts.height || 0, x, y, up = false;
    if (opts.align === 'side') { x = rect.right + 8; y = rect.top; }
    else if (opts.align === 'start') { x = rect.left; y = rect.bottom + 6; }
    else { x = rect.right - w; y = rect.bottom + 4; if (y + h > window.innerHeight - 8) { y = rect.top - h - 4; up = true; } }
    x = Math.max(8, Math.min(x, window.innerWidth - w - 8)); y = Math.max(8, y);
    return { x: Math.round(x), y: Math.round(y), w: Math.round(w), up: up };
  }

  // Pagination model: all pages up to 7, else first, current±1, last with gaps.
  function pageList(page, pageCount) {
    var out = [], i;
    if (pageCount <= 7) { for (i = 1; i <= pageCount; i++) out.push({ page: i }); return out; }
    out.push({ page: 1 });
    var a = Math.max(2, page - 1), b = Math.min(pageCount - 1, page + 1);
    if (a > 2) out.push({ gap: true });
    for (i = a; i <= b; i++) out.push({ page: i });
    if (b < pageCount - 1) out.push({ gap: true });
    out.push({ page: pageCount });
    return out;
  }

  // Toasts: component keeps `state.toasts`; provides `later(fn, ms)`. At most 3 visible, newest last.
  // Success toasts live 4.5s, toasts with an action (e.g. Retry) 7s. o = {text, tone:'error'?, action:{label, run}}
  var tseq = 1;
  function toast(cmp, o) {
    var id = tseq++;
    cmp.setState(function (s) { return { toasts: s.toasts.filter(function (x) { return !x.leaving; }).slice(-(motion.toastMax - 1)).concat([Object.assign({ id: id }, o)]) }; });
    cmp.later(function () { dismissToast(cmp, id); }, o.action ? motion.toastLifeWithAction : motion.toastLife);
    return id;
  }
  function dismissToast(cmp, id) {
    if (!cmp.state.toasts.some(function (t) { return t.id === id && !t.leaving; })) return;
    cmp.setState(function (s) { return { toasts: s.toasts.map(function (t) { return t.id === id ? Object.assign({}, t, { leaving: true }) : t; }) }; }, function () {
      afterExit(cmp, '[data-toast="' + id + '"]', function () { cmp.setState(function (s) { return { toasts: s.toasts.filter(function (t) { return t.id !== id; }) }; }); });
    });
  }
  // View model for the toast region markup (see components.css .toast).
  function toastView(cmp) {
    return cmp.state.toasts.map(function (t) {
      return { id: t.id, text: t.text, cls: (t.tone === 'error' ? 'is-error' : '') + (t.leaving ? ' is-leaving' : ''), isError: t.tone === 'error', isOk: t.tone !== 'error',
        hasAction: !!t.action, actionLabel: t.action ? t.action.label : '',
        act: function () { dismissToast(cmp, t.id); t.action.run(); }, close: function () { dismissToast(cmp, t.id); } };
    });
  }

  // Symmetric close: mark the layer `closing` (plays its CSS exit animation), then clear it once that
  // animation has run. key: the state field holding the layer ('menu' | 'modal' | 'drawer');
  // targets: selector/elements that animate out (the panel and its scrim).
  function closeLayer(cmp, key, targets, after) {
    var cur = cmp.state[key]; if (!cur || cur.closing) return false;
    var patch = {}; patch[key] = Object.assign({}, cur, { closing: true });
    cmp.setState(patch, function () {
      afterExit(cmp, targets, function () {
        if (!cmp.state[key] || !cmp.state[key].closing) return;
        var clear = {}; clear[key] = null; cmp.setState(clear, after);
      });
    });
    return true;
  }

  // Tables scroll sideways only when their columns don't fit (keeps the sticky header otherwise).
  // Calls cmp.setState({[stateKey]: bool}) when that changes; re-checks when `observeId` resizes.
  function watchOverflowX(cmp, wrapId, stateKey, observeId) {
    var check = function () {
      var w = document.getElementById(wrapId); if (!w) return;
      var t = w.firstElementChild; var need = !!t && t.offsetWidth > w.clientWidth + 1;
      if (need !== cmp.state[stateKey]) { var p = {}; p[stateKey] = need; cmp.setState(p); }
    };
    var ro = null;
    try { ro = new ResizeObserver(check); var o = document.getElementById(observeId); if (o) ro.observe(o); } catch (e) {}
    check();
    return { check: check, disconnect: function () { if (ro) ro.disconnect(); } };
  }

  // Place a floating tip beside a target (right side, or left when there's no room), top-aligned with it,
  // inside a positioned container. Returns {left, top} in px. Keeps tips clear of the container's edges so
  // they are never clipped by a card.
  function tipBeside(container, target, tipWidth, tipHeight, gap) {
    if (!container || !target) return { left: 0, top: 0 };
    var c = container.getBoundingClientRect(), t = target.getBoundingClientRect(), g = gap == null ? 12 : gap;
    var left = t.right - c.left + g;
    if (left + tipWidth > c.width - 4) left = t.left - c.left - g - tipWidth;
    left = Math.max(4, left);
    var top = Math.max(4, Math.min(t.top - c.top, c.height - (tipHeight || 0) - 4));
    return { left: Math.round(left), top: Math.round(top) };
  }

  // Hint tooltips (.hint > .hint-tip). CSS alone places the tip inside its card, where the card's
  // rounded-corner clipping (and the table's scroll box) can cut it off. While a hint is hovered or
  // focused, its tip is placed against the viewport instead: above the hint, or below when there is
  // no room, and kept inside the screen edges. Without script, the CSS placement still works.
  var hintEdge = 8, hintGap = 8, activeHint = null;
  function placeHint(hint) {
    var tip = hint && hint.querySelector('.hint-tip');
    if (!tip) return;
    var r = hint.getBoundingClientRect(), vw = document.documentElement.clientWidth, vh = window.innerHeight;
    hint.classList.add('is-placed');
    var w = tip.offsetWidth, h = tip.offsetHeight;
    var left = Math.max(hintEdge, Math.min(r.left + r.width / 2 - w / 2, vw - w - hintEdge));
    var below = r.top - hintGap - h < hintEdge && r.bottom + hintGap + h <= vh - hintEdge;
    var top = below ? r.bottom + hintGap : r.top - hintGap - h;
    hint.classList.toggle('is-below', below);
    hint.style.setProperty('--hint-x', Math.round(left) + 'px');
    hint.style.setProperty('--hint-y', Math.round(top) + 'px');
  }
  function hintFrom(e) { return e.target && e.target.closest ? e.target.closest('.hint') : null; }
  document.addEventListener('pointerover', function (e) { var h = hintFrom(e); if (h && h !== activeHint) { activeHint = h; placeHint(h); } }, true);
  document.addEventListener('focusin', function (e) { var h = hintFrom(e); if (h) { activeHint = h; placeHint(h); } }, true);
  window.addEventListener('scroll', function () { if (activeHint) placeHint(activeHint); }, true);
  window.addEventListener('resize', function () { if (activeHint) placeHint(activeHint); });

  // URL state for directory pages. toQuery leaves out defaults (null, undefined, '' and false), so a page
  // passes only what differs from its default view and shared links stay short. fromQuery reads one back;
  // the page validates every value (unknown values fall back to defaults).
  function toQuery(values) {
    var parts = [];
    Object.keys(values).forEach(function (k) {
      var v = values[k]; if (v === null || v === undefined || v === '' || v === false) return;
      parts.push(encodeURIComponent(k) + '=' + encodeURIComponent(v));
    });
    return parts.length ? '?' + parts.join('&') : '';
  }
  function fromQuery(search) {
    var out = {};
    String(search || '').replace(/^\?/, '').split('&').forEach(function (p) {
      if (!p) return;
      var i = p.indexOf('='), dec = function (x) { try { return decodeURIComponent(x.replace(/\+/g, ' ')); } catch (e) { return x; } };
      out[dec(i < 0 ? p : p.slice(0, i))] = i < 0 ? '' : dec(p.slice(i + 1));
    });
    return out;
  }

  // A key that changes whenever the listed state fields change (drives the table's view-change fade;
  // typing in search is deliberately excluded by the page).
  function viewSignature(state, keys) { return keys.map(function (k) { return String(state[k]); }).join('|'); }


  // ---------------------------------------------------------------------------------------------
  // Saving feedback and prototype requests.
  // busyMin: the shortest time a "Saving…" state stays visible, so a fast response still reads as a
  // deliberate step (product behaviour: keep it in the application). settle() applies it.
  // mock: prototype-only simulated server latency. It stands in for the network and is NOT a product
  // timing rule; the application just awaits its request.
  // busyMinQuick: the same rule for one-click actions on a live board (a pick, Undo): the spinner still shows,
  // but briefly, so repeated actions feel responsive. Pass it as settle's 4th argument. Never shows success
  // early: settle only delays the presentation of a result that has already been confirmed.
  motion.busyMin = 600;
  motion.busyMinQuick = 250;
  function settle(cmp, startedAt, fn, min) {
    var left = (min == null ? motion.busyMin : min) - (Date.now() - startedAt);
    if (left > 0 && !reducedMotion()) cmp.later(fn, left); else fn();
  }
  // quickLatency: prototype stand-in for a small in-place request (a pick), not a product timing.
  var mock = { latency: 650, quickLatency: 300, request: function (cmp, fn, ms) { return cmp.later(fn, ms == null ? mock.latency : ms); } };

  // A duration token (e.g. '--dk-dur-reorder') in ms, read from the themed root so tokens.css stays the source.
  function tokenMs(name, el) {
    var root = el || document.querySelector('.dk-theme') || document.documentElement;
    return toMs(getComputedStyle(root).getPropertyValue(name) || 0);
  }

  // ---------------------------------------------------------------------------------------------
  // Reorder (FLIP). measure() before the state that reorders items is committed; play() after it.
  // Items are matched by id, so it works whether the runtime moves DOM nodes or rewrites them in place.
  // Each item travels from its old position to its new one with a small lift, staggered by DOM order.
  // The motion only presents an order the page already has: it never chooses one. Reduced motion: no
  // movement, done() runs at once.
  var reorder = {
    measure: function (selector) { var m = {}; resolve(selector).forEach(function (el) { if (el.id) m[el.id] = el.getBoundingClientRect(); }); return m; },
    play: function (cmp, before, selector, done) {
      var els = resolve(selector);
      if (!els.length || reducedMotion() || !els[0].animate) { if (done) done(); return; }
      var dur = tokenMs('--dk-dur-reorder', els[0]), stagger = tokenMs('--dk-dur-reorder-stagger', els[0]);
      var ease = getComputedStyle(els[0]).getPropertyValue('--dk-ease-out').trim() || 'ease-out';
      var total = 0, n = 0;
      els.forEach(function (el) {
        var b = before[el.id]; if (!b) return;
        var a = el.getBoundingClientRect(); var dx = b.left - a.left, dy = b.top - a.top;
        if (Math.abs(dx) < 1 && Math.abs(dy) < 1) return;
        var delay = n++ * stagger; total = Math.max(total, delay + dur);
        el.animate([
          { transform: 'translate(' + dx + 'px,' + dy + 'px)' },
          { transform: 'translate(' + (dx / 2) + 'px,' + (dy / 2 - 10) + 'px) scale(1.025)', offset: 0.45 },
          { transform: 'none' }
        ], { duration: dur, delay: delay, easing: ease, fill: 'backwards' });
      });
      cmp.later(function () { if (done) done(); }, total ? total + EXIT_SLACK : 0);
    }
  };

  // ---------------------------------------------------------------------------------------------
  // Date/time picker (.dtp). Replaces the browser's datetime-local popup with one styled component.
  // Value: a local wall time "YYYY-MM-DDTHH:mm" in the event's timezone (the same string datetime-local
  // produced), or ''. The page owns the value; the picker's own UI state lives in cmp.state.dtp[id].
  // Typing: date as "27 Jun 2027", "27/6/2027" or "2027-06-27"; time as "18:30", "18.30" or "1830".
  // Times are in five-minute steps. Escape closes the popup before any dialog around it.
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
  function fmtDate(p) { return p ? p.d + ' ' + MON3[p.m - 1] + ' ' + p.y : ''; }
  function fmtTime(p) { return p ? pad(p.h) + ':' + pad(p.mi) : ''; }
  function parseDate(t) {
    t = String(t || '').trim(); if (!t) return { empty: true };
    var y, m, d, x;
    if ((x = /^(\d{4})-(\d{1,2})-(\d{1,2})$/.exec(t))) { y = +x[1]; m = +x[2]; d = +x[3]; }
    else if ((x = /^(\d{1,2})[.\/\- ](\d{1,2})[.\/\- ](\d{4})$/.exec(t))) { d = +x[1]; m = +x[2]; y = +x[3]; }
    else if ((x = /^(\d{1,2})\.?\s*([A-Za-z]{3,})\.?,?\s*(\d{4})$/.exec(t))) {
      var k = x[2].slice(0, 3).toLowerCase(); m = MON3.map(function (s) { return s.toLowerCase(); }).indexOf(k) + 1; d = +x[1]; y = +x[3];
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
    } catch (e) { return 0; }
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
  document.addEventListener('pointerdown', onDocPointer, true);
  window.addEventListener('resize', onReflow);
  window.addEventListener('scroll', function (e) { if (dtpOpen && !(e.target && e.target.closest && e.target.closest('.dtp-pop'))) onReflow(); }, true);

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

  window.DKAdmin = {
    motion: motion, reducedMotion: reducedMotion, animationMs: animationMs, afterExit: afterExit,
    focusSoon: focusSoon, restoreFocus: restoreFocus, trapTab: trapTab,
    menuKeydown: menuKeydown, focusFirstMenuItem: focusFirstMenuItem, listKeydown: listKeydown,
    menuHeight: menuHeight, placeMenu: placeMenu, pageList: pageList,
    toast: toast, dismissToast: dismissToast, toastView: toastView,
    closeLayer: closeLayer, watchOverflowX: watchOverflowX, viewSignature: viewSignature, tipBeside: tipBeside,
    toQuery: toQuery, fromQuery: fromQuery,
    settle: settle, mock: mock, tokenMs: tokenMs, reorder: reorder, dtp: dtp
  };
})();

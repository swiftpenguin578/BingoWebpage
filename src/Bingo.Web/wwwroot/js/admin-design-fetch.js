// C-CMP-2 boundary: classify transport outcomes before a page interprets data.
// No retry is performed here. Unknown writes belong to the page's readback flow.
(() => {
  'use strict';
  const accountPaths = new Set(['/account/login', '/account/accessdenied', '/account/changepassword']);
  const localUrl = value => { try { const url = new URL(value, location.href); return url.origin === location.origin ? url : null; } catch { return null; } };
  async function classify(response, { expect = 'json', allowRedirectTo } = {}) {
    if (!response.ok) return { kind: 'unknown', status: response.status };
    const navigation = response.headers.get('X-Bingo-Post-Navigation') || response.headers.get('Location');
    const destination = localUrl(navigation || response.url || location.href);
    const path = destination?.pathname.replace(/\/$/, '').toLowerCase();
    if (accountPaths.has(path)) return { kind: 'session-lost', destination: destination.href };
    const redirected = !!navigation || response.redirected;
    if (redirected && /^\/admin\/events\/manage\/[^/]+$/.test(path || '')) {
      let reason = '';
      if ((response.headers.get('Content-Type') || '').includes('text/html')) {
        try { reason = new DOMParser().parseFromString(await response.text(), 'text/html').querySelector('[data-transient-toast] .app-toast-copy > span')?.textContent.trim() || ''; } catch { /* The redirect still definitively refused the write. */ }
      }
      return { kind: 'refused', destination: destination.href, reason };
    }
    if (response.type === 'opaqueredirect' || (redirected && (!allowRedirectTo || destination?.pathname !== localUrl(allowRedirectTo)?.pathname))) return { kind: 'unknown' };
    const contentType = (response.headers.get('Content-Type') || '').toLowerCase();
    if (expect === 'json' && contentType.includes('text/html')) return { kind: 'session-lost', destination: null };
    try {
      if (expect === 'json' && contentType.includes('application/json')) return { kind: 'handler', data: await response.json(), response };
      if (expect === 'html' && contentType.includes('text/html')) return { kind: 'handler', data: await response.text(), response };
    } catch { return { kind: 'unknown' }; }
    return { kind: 'unknown' };
  }
  function sessionNotice(draft, destination, { readback = false, labels = {} } = {}) {
    const ui = window.AdminUI, text = key => document.body.dataset[key] || '';
    const titleText = text(readback ? 'readbackSessionTitle' : 'sessionTitle');
    const content = document.createElement('div');
    const title = document.createElement('h2'); title.className = 'm-title'; title.textContent = titleText;
    const message = document.createElement('p'); message.textContent = text(readback ? 'readbackSessionMessage' : 'sessionMessage');
    const values = document.createElement('dl'); values.className = 'kv';
    for (const [label, value] of Object.entries(draft || {})) { const term = document.createElement('dt'), detail = document.createElement('dd'); term.textContent = labels[label] || label; detail.textContent = value == null || value === '' ? text('emptyValue') : String(value); values.append(term, detail); }
    const actions = document.createElement('div'); actions.className = 'm-actions';
    const keep = document.createElement('button'); keep.className = 'btn'; keep.textContent = text('keepEditing'); keep.autofocus = true;
    const signIn = document.createElement('a'); signIn.className = 'btn btn-primary'; signIn.textContent = text('signIn');
    const login = new URL('/Account/Login', location.href); login.searchParams.set('ReturnUrl', location.pathname + location.search);
    const target = localUrl(destination);
    if (target?.searchParams.get('accessChanged') === 'true') login.searchParams.set('accessChanged', 'true');
    signIn.href = login.href;
    actions.append(keep, signIn); content.append(title, message, values, actions);
    const layer = ui.openLayer({ title: titleText, content, confirmation: true });
    keep.addEventListener('click', () => void layer.close(false));
    return layer;
  }
  async function request(url, { expect = 'json', allowRedirectTo, draft, labels, readback = false, notice = true, ...options } = {}) {
    const headers = new Headers(options.headers);
    headers.set('X-Requested-With', 'XMLHttpRequest');
    if (expect === 'json') headers.set('Accept', 'application/json');
    const method = (options.method || 'GET').toUpperCase();
    if (method !== 'GET' && method !== 'HEAD') {
      const token = document.querySelector('[data-shell-antiforgery] input[name="__RequestVerificationToken"]')?.value;
      if (token) headers.set('RequestVerificationToken', token);
    }
    let outcome;
    try { outcome = await classify(await fetch(url, { ...options, headers, credentials: 'same-origin' }), { expect, allowRedirectTo }); }
    catch (error) { outcome = { kind: 'unknown', aborted: error.name === 'AbortError' }; }
    if (outcome.kind === 'session-lost' && notice) sessionNotice(draft, outcome.destination, { readback, labels });
    return outcome;
  }
  window.AdminFetch = { request, classify, sessionNotice };
})();

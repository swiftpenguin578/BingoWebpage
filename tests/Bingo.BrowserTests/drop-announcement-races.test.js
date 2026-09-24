const assert = require('node:assert/strict');
const test = require('node:test');
const fs = require('node:fs');
const vm = require('node:vm');
const path = require('node:path');
const script = fs.readFileSync(path.join(__dirname, '../../src/Bingo.Web/wwwroot/js/drop-announcement.js'), 'utf8');
const flush = () => new Promise(resolve => setImmediate(resolve));
const deferred = () => { let resolve, reject; const promise = new Promise((yes, no) => { resolve = yes; reject = no; }); return { promise, resolve, reject }; };
const entry = id => ({ submissionId: id, tileName: `Tile ${id}`, teamName: 'Team', playerName: 'Player', progressAfter: 1, target: 2 });
const snapshot = (queue = [entry('one')], extra = {}) => ({ eventId: 'event', eventSlug: 'event', generation: 1, snapshotSequence: 1, queueTotalCount: queue.length, newCount: queue.length, queue, ...extra });
const response = value => ({ ok: true, status: value ? 200 : 204, json: async () => value });
class Element {
  constructor() { this.dataset = {}; this.listeners = {}; this.children = []; this.hidden = true; this.classList = { add() {} }; this.style = { removeProperty() {} }; }
  addEventListener(type, callback) { this.listeners[type] = callback; }
  dispatch(type, event = {}) { return this.listeners[type]?.(event); }
  querySelector(selector) { return this.parts?.[selector] || null; }
  append(...children) { this.children.push(...children); this.firstElementChild = this.children[0]; }
  replaceChildren(...children) { this.children = children; this.firstElementChild = children[0]; }
  setAttribute(name, value) { this[name] = value; }
  getBoundingClientRect() { return { height: 100 }; }
  contains() { return false; }
}
function setup({ initial = snapshot(), claim = async () => response({ claimed: true }), eventContext = null } = {}) {
  const root = new Element(); root.dataset.accountId = 'account'; root.parts = {};
  for (const name of ['main', 'nav', 'previous', 'next', 'position', 'link', 'count', 'expand', 'dismiss']) root.parts[`[data-drop-announcement-${name}]`] = new Element();
  root.parts['input[name="__RequestVerificationToken"]'] = { value: 'antiforgery' };
  const clear = new Element(); clear.dataset.dropEventId = 'event';
  const badge = new Element();
  const progressMarker = new Element();
  const statsMarker = new Element();
  const setEventContext = context => {
    progressMarker.dataset.progressEvent = context?.progressEvent;
    statsMarker.dataset.statsEvent = context?.statsEvent;
  };
  setEventContext(eventContext);
  const nav = new Element(); nav.dataset.dropNavigationSlug = 'event'; nav.parts = { '[data-drop-navigation-new]': badge };
  const events = {};
  const timers = [];
  const storage = new Map();
  const requests = [];
  const state = { get: async () => response(initial), claim, mutation: async () => response(null) };
  const window = {
    addEventListener(type, callback) { events[type] = callback; },
    dispatchEvent(event) { return events[event.type]?.(event); },
    setTimeout(callback) { timers.push(callback); return callback; }, clearTimeout() {},
    requestAnimationFrame(callback) { callback(); }, cancelAnimationFrame() {},
    matchMedia: () => ({ matches: true })
  };
  const document = {
    querySelector: selector => selector === '[data-drop-announcement]' ? root : selector === '[data-drop-clear-all]' ? clear
      : selector === '[data-progress-event]' ? (progressMarker.dataset.progressEvent ? progressMarker : null)
        : selector === '[data-stats-event]' ? (statsMarker.dataset.statsEvent ? statsMarker : null) : null,
    querySelectorAll: selector => selector === '[data-drop-navigation]' ? [nav] : [],
    createElement: () => new Element(), createTextNode: textContent => ({ textContent }), addEventListener() {}
  };
  vm.runInNewContext(script, {
    document, window, sessionStorage: { getItem: key => storage.get(key), setItem: (key, value) => storage.set(key, value), removeItem: key => storage.delete(key) },
    CustomEvent: class { constructor(type, init) { this.type = type; this.detail = init?.detail; } },
    fetch: (url, options) => {
      requests.push({ url, options });
      return url.includes('/current?') || /\/api\/drop-announcements\/[^?]+\?limit=/.test(url)
        ? state.get(url) : url.endsWith('/claim') ? state.claim() : state.mutation(url, options);
    }
  });
  return { state, root, clear, badge, events, storage, requests, timers, setEventContext, refresh: () => events['bingo-progress-changed']() };
}

test('event pages target the viewed Board or Stats event and a 404 clears a stale snapshot', async () => {
  const h = setup({ eventContext: { progressEvent: 'board-event' } }); await flush();
  assert.ok(h.requests.some(request => request.url.includes('/api/drop-announcements/board-event?limit=100')));
  assert.equal(h.root.dataset.visible, 'true');

  h.setEventContext({ statsEvent: 'stats-event' });
  h.state.get = async url => url.includes('/stats-event?') ? { ok: false, status: 404, json: async () => null } : response(snapshot());
  await h.refresh(); await flush();
  assert.ok(h.requests.some(request => request.url.includes('/api/drop-announcements/stats-event?limit=100')));
  assert.equal(h.root.hidden, true, 'an exact-event 404 clears a banner from the previous viewed event');
  assert.equal(h.badge.hidden, true);

  h.setEventContext(null);
  h.state.get = async () => response(null);
  await h.refresh(); await flush();
  assert.ok(h.requests.some(request => request.url.includes('/api/drop-announcements/current?limit=100')), 'pages without event context keep the participant-scoped fallback');
});

test('a delayed eligible GET cannot restore a queue or NEW badge after a newer finalized response', async () => {
  const h = setup(); await flush();
  assert.equal(h.root.dataset.visible, 'true'); assert.equal(h.badge.hidden, false);
  const old = deferred(); h.state.get = () => old.promise; const oldRead = h.refresh();
  h.state.get = async () => response(null); await h.refresh();
  assert.equal(h.root.hidden, true); assert.equal(h.badge.hidden, true); assert.equal(h.storage.size, 0);
  old.resolve(response(snapshot())); await oldRead; await flush();
  assert.equal(h.root.hidden, true); assert.equal(h.badge.hidden, true); assert.equal(h.storage.size, 0);
});

for (const mutation of ['dismiss', 'clear-all-new', 'acknowledge-both']) {
  test(`a delayed GET cannot undo successful ${mutation}, including reads attempted during the write`, async () => {
    const h = setup(); await flush();
    const old = deferred(); h.state.get = () => old.promise; const oldRead = h.refresh();
    const write = deferred(); h.state.mutation = () => write.promise;
    const action = mutation === 'dismiss' ? h.root.parts['[data-drop-announcement-dismiss]'].dispatch('click')
      : mutation === 'clear-all-new' ? h.clear.dispatch('click')
        : h.events['public-evidence-opened']({ detail: { eventId: 'event', submissionId: 'one' } });
    const reads = h.requests.filter(request => request.url.includes('/current?')).length;
    await h.refresh();
    assert.equal(h.requests.filter(request => request.url.includes('/current?')).length, reads, 'reads wait for the mutation boundary');
    h.state.get = async () => response(snapshot([], { newCount: mutation === 'dismiss' ? 1 : 0 }));
    write.resolve(response(null)); await action;
    old.resolve(response(snapshot())); await oldRead; await flush();
    assert.equal(h.root.dataset.visible, 'false');
    if (mutation !== 'dismiss') assert.equal(h.badge.hidden, true);
    assert.equal(JSON.parse([...h.storage.values()][0]).selectedId, null);
  });
}

for (const outcome of ['claimed', 'declined', 'failed']) {
  test(`a pending ${outcome} claim cannot show stale text after clearing eligibility`, async () => {
    const claim = deferred(); const h = setup({ claim: () => claim.promise }); await flush();
    assert.equal(h.requests.filter(request => request.url.endsWith('/claim')).length, 1);
    h.state.get = async () => response(null); await h.refresh();
    if (outcome === 'failed') claim.reject(new Error('offline')); else claim.resolve(response({ claimed: outcome === 'claimed' }));
    await flush();
    assert.equal(h.root.hidden, true); assert.equal(h.root.dataset.visible, 'false'); assert.equal(h.badge.hidden, true);
    assert.equal(h.storage.size, 0);
  });
}

test('a claim from a previous event/generation cannot expand the current compact queue', async () => {
  const claim = deferred(); const h = setup({ claim: () => claim.promise }); await flush();
  h.state.claim = async () => response({ claimed: false });
  h.state.get = async () => response(snapshot([entry('other')], { eventId: 'other-event', eventSlug: 'other-event', generation: 2 }));
  await h.refresh(); await flush();
  assert.equal(h.root.dataset.state, 'compact');
  claim.resolve(response({ claimed: true })); await flush();
  assert.equal(h.root.dataset.state, 'compact');
  assert.match(h.root.parts['[data-drop-announcement-link]'].href, /other-event.*other/);
});

for (const change of ['approval', 'finalization']) {
  test(`dismiss replays the ${change} invalidation after its local hide without another event`, async () => {
    const h = setup(); await flush();
    const write = deferred(); h.state.mutation = () => write.promise;
    const action = h.root.parts['[data-drop-announcement-dismiss]'].dispatch('click');
    const readsBefore = h.requests.filter(request => request.url.includes('/current?')).length;
    let dismissalTimer;
    h.state.get = async () => {
      dismissalTimer = h.timers.at(-1);
      assert.equal(h.root.dataset.visible, 'false', 'dismiss finishes its local hide before the queued read');
      assert.equal(JSON.parse([...h.storage.values()][0]).selectedId, null, 'dismiss saves its local queue before the queued read');
      return response(change === 'approval' ? snapshot([entry('newer')], { snapshotSequence: 2 }) : null);
    };
    await h.refresh(); await h.refresh();
    assert.equal(h.requests.filter(request => request.url.includes('/current?')).length, readsBefore);
    write.resolve(response(null)); await action; await flush();
    assert.equal(h.requests.filter(request => request.url.includes('/current?')).length, readsBefore + 1, 'suppressed invalidations coalesce into one read');
    if (change === 'approval') {
      assert.equal(h.root.dataset.visible, 'true'); assert.equal(h.badge.hidden, false);
      assert.match(h.root.parts['[data-drop-announcement-link]'].href, /submissionId=newer$/);
      // Complete the old dismissal's timeout after the new queue was displayed.
      dismissalTimer();
      assert.equal(h.root.hidden, false, 'the old dismissal cannot hide the newer queue');
    } else {
      assert.equal(h.root.hidden, true); assert.equal(h.badge.hidden, true); assert.equal(h.storage.size, 0);
    }
  });
}

for (const mutation of ['dismiss', 'clear-all-new', 'acknowledge-both']) {
  test(`failed ${mutation} still drains a suppressed finalization invalidation`, async () => {
    const h = setup(); await flush();
    const write = deferred(); h.state.mutation = () => write.promise;
    const action = mutation === 'dismiss' ? h.root.parts['[data-drop-announcement-dismiss]'].dispatch('click')
      : mutation === 'clear-all-new' ? h.clear.dispatch('click')
        : h.events['public-evidence-opened']({ detail: { eventId: 'event', submissionId: 'one' } });
    const readsBefore = h.requests.filter(request => request.url.includes('/current?')).length;
    h.state.get = async () => {
      if (mutation === 'clear-all-new') assert.equal(h.clear.disabled, false, 'CLEAR ALL settles its local controls before refreshing');
      return response(null);
    };
    await h.refresh();
    write.reject(new Error('response lost')); await action; await flush();
    assert.equal(h.requests.filter(request => request.url.includes('/current?')).length, readsBefore + 1);
    assert.equal(h.root.hidden, true); assert.equal(h.badge.hidden, true); assert.equal(h.storage.size, 0);
  });
}

test('queued refresh waits for all concurrent mutation handlers and coalesces their explicit refreshes', async () => {
  const h = setup(); await flush();
  const dismiss = deferred(); const clear = deferred();
  h.state.mutation = url => url.endsWith('/dismiss') ? dismiss.promise : clear.promise;
  const dismissAction = h.root.parts['[data-drop-announcement-dismiss]'].dispatch('click');
  const clearAction = h.clear.dispatch('click');
  const readsBefore = h.requests.filter(request => request.url.includes('/current?')).length;
  h.state.get = async () => {
    assert.equal(h.root.dataset.visible, 'false', 'the dismiss handler has hidden its old queue');
    assert.equal(h.clear.disabled, false, 'the final CLEAR ALL handler has settled');
    return response(snapshot([entry('newer')], { snapshotSequence: 2 }));
  };
  await h.refresh();
  dismiss.resolve(response(null)); await dismissAction; await flush();
  assert.equal(h.requests.filter(request => request.url.includes('/current?')).length, readsBefore, 'the other mutation still owns the write fence');
  clear.resolve(response(null)); await clearAction; await flush();
  assert.equal(h.requests.filter(request => request.url.includes('/current?')).length, readsBefore + 1);
  assert.equal(h.root.dataset.visible, 'true');
  assert.match(h.root.parts['[data-drop-announcement-link]'].href, /submissionId=newer$/);
});

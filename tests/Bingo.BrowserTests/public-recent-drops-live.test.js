const assert = require('node:assert/strict');
const test = require('node:test');
const { initialize } = require('../../src/Bingo.Web/wwwroot/js/public-recent-drops.js');
const flush = () => new Promise(resolve => setImmediate(resolve));
const deferred = () => { let resolve; const promise = new Promise(yes => { resolve = yes; }); return { promise, resolve }; };
class Element {
  constructor(dataset = {}, className = '') { this.dataset = dataset; this.className = className; this.children = []; this.listeners = {}; }
  append(...children) { children.forEach(child => { if (typeof child === 'object') child.parentElement = this; this.children.push(child); }); }
  prepend(child) { child.parentElement = this; this.children.unshift(child); }
  remove() { this.parentElement?.children.splice(this.parentElement.children.indexOf(this), 1); }
  addEventListener(type, listener) { (this.listeners[type] ??= []).push(listener); }
  querySelector(selector) { return this.querySelectorAll(selector)[0] || null; }
  querySelectorAll(selector) { return this.children.flatMap(child => typeof child === 'object' ? [...(child.matches(selector) ? [child] : []), ...child.querySelectorAll(selector)] : []); }
  matches(selector) {
    if (selector.startsWith('.')) return this.className.split(' ').includes(selector.slice(1));
    const match = selector.match(/^\[data-([\w-]+)(?:="([^"]+)")?\]$/);
    if (!match) return false;
    const name = match[1].replace(/-([a-z])/g, (_value, letter) => letter.toUpperCase());
    return Object.hasOwn(this.dataset, name) && (match[2] === undefined || this.dataset[name] === match[2]);
  }
}
const at = minute => new Date(Date.UTC(2026, 8, 16, 10, minute)).toISOString();
const drop = (submissionId, minute = 1) => ({ submissionId, approvedAt: at(minute), tileName: 'Tile', teamName: 'Team', playerName: 'Player' });
function harness({ shared = false, count = 2 } = {}) {
  const root = new Element(); const region = new Element({ publicRecentDrops: '' }); const feed = new Element({}, 'public-ui-recent-drop-feed'); const grid = new Element({}, 'public-ui-recent-drop-grid');
  const popup = new Element({ evidenceDialog: '' }); popup.open = true;
  const marker = new Element({ progressEvent: 'event' });
  const search = new Element({ publicRecentDropsSearch: '' }); search.value = 'Player';
  const team = new Element({ publicRecentDropsTeam: '' }); team.value = 'team';
  const toolbar = new Element({ publicRecentDropsToolbar: '' }); toolbar.append(search, team);
  const validIds = new Set();
  for (let i = 0; i < count; i++) {
    const card = new Element({ publicRecentDropId: `loaded-${i}`, publicRecentDropApprovedAt: at(0) });
    card.append(new Element({}, 'public-ui-recent-drop-title')); grid.append(card); validIds.add(`loaded-${i}`);
  }
  feed.append(grid); region.append(feed); root.append(marker, region, toolbar, popup);
  const events = {}; const hubEvents = {}; const reconnects = []; const watches = []; const requests = []; const scrolls = [];
  let starts = 0;
  const ready = deferred();
  const connection = {
    state: shared ? 'Connecting' : 'Disconnected',
    on(type, listener) { (hubEvents[type] ??= []).push(listener); },
    onreconnected(callback) { reconnects.push(callback); },
    invoke(method, id) { watches.push({ method, id }); return Promise.resolve(); },
    start() { starts++; connection.state = 'Connected'; return Promise.resolve(); }
  };
  const state = { drops: [], validIds, newIds: ['loaded-0'], recent: null, newResponse: null };
  const window = {
    location: { href: 'https://example.test/Events/event/Board?view=drops&dropCount=150&dropSearch=Player&dropTeam=team', pathname: '/Events/event/Board' },
    scrollX: 10, scrollY: 480, scrollTo: (...args) => scrolls.push(args), requestAnimationFrame: callback => callback(),
    addEventListener(type, listener) { (events[type] ??= []).push(listener); },
    signalR: { HubConnectionState: { Connected: 'Connected' }, HubConnectionBuilder: class { withUrl(url) { assert.equal(url, '/hubs/progress'); return this; } withAutomaticReconnect() { return this; } build() { return connection; } } },
    fetch: async (url, options) => {
      const parsed = new URL(String(url), window.location.href); requests.push({ url: parsed, options });
      if (parsed.pathname.endsWith('/recent-drops')) {
        if (state.recent) return state.recent(parsed);
        const ids = (parsed.searchParams.get('loadedSubmissionIds') || '').split(',').filter(Boolean);
        return { ok: true, json: async () => ({ drops: state.drops, validSubmissionIds: ids.filter(id => state.validIds.has(id)) }) };
      }
      assert.equal(parsed.pathname, '/api/drop-announcements/new');
      return state.newResponse ? state.newResponse() : { ok: true, json: async () => state.newIds };
    }
  };
  if (shared) { window.bingoProgressConnection = connection; window.bingoProgressReady = ready.promise; }
  initialize(root, window, { createElement: () => new Element(), querySelector: () => null });
  const signal = () => Promise.all((hubEvents.progressChanged || []).map(callback => callback()));
  return { root, region, popup, grid, state, connection, watches, requests, scrolls, window, events, ready, starts: () => starts, signal, reconnect: () => Promise.all(reconnects.map(callback => callback())) };
}

test('anonymous Drops subscribes initially and removes a reversed loaded row without discarding valid older pages', async () => {
  const h = harness({ count: 125 }); await flush();
  assert.equal(h.starts(), 1); assert.deepEqual(h.watches, [{ method: 'WatchEvent', id: 'event' }]);
  assert.equal(h.requests.filter(request => request.url.pathname.endsWith('/recent-drops')).length, 2);
  const older = h.grid.children[124];
  h.state.validIds.delete('loaded-0'); h.state.drops = [drop('new')]; h.state.validIds.add('new');
  await h.signal(); await flush();
  assert.equal(h.region.querySelector('[data-public-recent-drop-id="loaded-0"]'), null);
  assert.equal(h.region.querySelector('[data-public-recent-drop-id="loaded-124"]'), older);
  assert.ok(h.region.querySelector('[data-public-recent-drop-id="new"]'));
  assert.equal(h.grid.children.length, 125);
  assert.equal(h.popup.open, true); assert.equal(h.popup.parentElement, h.root);
  assert.ok(h.scrolls.every(args => args[0] === 10 && args[1] === 480));
  assert.match(h.window.location.href, /dropCount=150/);
  for (const request of h.requests.filter(request => request.url.pathname.endsWith('/recent-drops'))) {
    assert.equal(request.url.searchParams.get('dropSearch'), 'Player'); assert.equal(request.url.searchParams.get('dropTeam'), 'team');
    assert.ok(request.url.searchParams.get('loadedSubmissionIds').split(',').length <= 100);
  }
});

test('shared connection waits for initial readiness and catches missed approval and finalization on reconnect', async () => {
  const h = harness({ shared: true }); await flush();
  assert.equal(h.starts(), 0); assert.equal(h.watches.length, 0);
  h.state.drops = [drop('initial')]; h.state.validIds.add('initial'); h.ready.resolve(); await flush();
  assert.equal(h.watches.length, 1); assert.ok(h.region.querySelector('[data-public-recent-drop-id="initial"]'));
  h.state.drops = [drop('missed', 2), drop('initial')]; h.state.validIds.add('missed');
  await h.reconnect(); await flush();
  assert.equal(h.watches.length, 2); assert.ok(h.region.querySelector('[data-public-recent-drop-id="missed"]'));
  const loaded = h.region.querySelector('[data-public-recent-drop-id="loaded-0"]');
  assert.ok(loaded.querySelector('.public-drop-new'));
  h.state.newIds = []; await h.reconnect(); await flush();
  assert.equal(h.watches.length, 3); assert.equal(loaded.querySelector('.public-drop-new'), null, 'missed finalization clears NEW after rejoin');
  assert.equal(h.grid.children.length, 4, 'finalization retains approved activity');
});

test('a superseded live response cannot remove a card or insert a reversed arrival', async () => {
  const h = harness(); await flush();
  const old = deferred(); h.state.recent = () => old.promise;
  const first = h.signal(); await flush();
  h.state.drops = []; h.state.recent = null;
  const second = h.signal();
  old.resolve({ ok: true, json: async () => ({ drops: [drop('reversed')], validSubmissionIds: [] }) });
  await Promise.all([first, second]); await flush();
  assert.equal(h.grid.children.length, 2); assert.equal(h.region.querySelector('[data-public-recent-drop-id="reversed"]'), null);
});

test('a deferred NEW response cannot restore a badge after a newer cleared response', async () => {
  const h = harness(); await flush();
  const stale = deferred(); h.state.newResponse = () => ({ ok: true, json: () => stale.promise });
  const previous = h.events['drop-announcement-acknowledged'][0]({ detail: { eventId: 'event', all: true } });
  await flush(); h.state.newResponse = null; h.state.newIds = [];
  h.events['drop-announcement-acknowledged'][0]({ detail: { eventId: 'event', all: true } }); await flush();
  stale.resolve(['loaded-0']); await previous; await flush();
  assert.equal(h.region.querySelector('[data-public-recent-drop-id="loaded-0"]').querySelector('.public-drop-new'), null);
});

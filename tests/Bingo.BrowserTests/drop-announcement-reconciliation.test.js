const assert = require("node:assert/strict");
const { initialize } = require("../../src/Bingo.Web/wwwroot/js/public-recent-drops.js");

class Node {
  constructor({ className = "", dataset = {}, children = [] } = {}) {
    this.className = className;
    this.dataset = dataset;
    this.children = [];
    this.listeners = {};
    this.parentElement = null;
    children.forEach(child => this.append(child));
  }
  append(...children) { children.forEach(child => { if (child && typeof child === "object") child.parentElement = this; this.children.push(child); }); }
  addEventListener(type, listener) { (this.listeners[type] ??= []).push(listener); }
  prepend(...children) { children.reverse().forEach(child => { if (child && typeof child === "object") child.parentElement = this; this.children.unshift(child); }); }
  dispatch(event) { return (this.listeners[event.type] ?? []).map(listener => listener(event)); }
  remove() { this.parentElement?.children.splice(this.parentElement.children.indexOf(this), 1); }
  querySelector(selector) { return this.querySelectorAll(selector)[0] ?? null; }
  querySelectorAll(selector) {
    const matches = [];
    if (this.matches(selector)) matches.push(this);
    for (const child of this.children) if (child?.querySelectorAll) matches.push(...child.querySelectorAll(selector));
    return matches;
  }
  matches(selector) {
    if (selector.startsWith(".")) return this.className.split(" ").includes(selector.slice(1));
    const data = selector.match(/^\[data-([\w-]+)(?:="([^"]+)")?\]$/);
    if (!data) return false;
    const key = data[1].replace(/-([a-z])/g, (_match, character) => character.toUpperCase());
    return Object.hasOwn(this.dataset, key) && (data[2] === undefined || this.dataset[key] === data[2]);
  }
}

const marker = new Node({ dataset: { progressEvent: "event-a" } });
const fixedNow = Date.parse("2026-09-13T12:00:00.000Z");
Date.now = () => fixedNow;
const oldApprovedAt = new Date(fixedNow - 3 * 60 * 1000).toISOString();
const liveApprovedAt = new Date(fixedNow - 2 * 60 * 1000).toISOString();
const secondLiveApprovedAt = new Date(fixedNow - 150 * 1000).toISOString();
const lateApprovedAt = liveApprovedAt;
const initialNewestId = "00000000-0000-0000-0000-000000000010";
const sameTimeLiveId = "00000000-0000-0000-0000-000000000015";
const liveId = "00000000-0000-0000-0000-000000000020";
const historicalSameTimeId = "00000000-0000-0000-0000-000000000005";
const lateLiveId = "00000000-0000-0000-0000-000000000030";
const titleA = new Node({ className: "public-ui-recent-drop-title" });
const titleB = new Node({ className: "public-ui-recent-drop-title" });
const cardA = new Node({ dataset: { publicRecentDropId: "a", publicRecentDropApprovedAt: oldApprovedAt }, children: [titleA] });
const cardB = new Node({ dataset: { publicRecentDropId: initialNewestId, publicRecentDropApprovedAt: secondLiveApprovedAt }, children: [titleB] });
const loadedCards = [cardA, cardB];
for (let index = 2; index < 25; index++) loadedCards.push(new Node({ dataset: { publicRecentDropId: `loaded-${index}`, publicRecentDropApprovedAt: oldApprovedAt } }));
const grid = new Node({ className: "public-ui-recent-drop-grid", children: loadedCards });
const feed = new Node({ className: "public-ui-recent-drop-feed", children: [grid] });
const region = new Node({ dataset: { publicRecentDrops: "", evidenceAltTemplate: "Godkendt bevis fra {0}", justNowLabel: "Lige nu" }, children: [feed] });
const root = new Node({ children: [marker, region] });
const listeners = {};
let newIds = ["a", initialNewestId];
let recentFetchCalls = 0;
const historicalDrops = Array.from({ length: 99 }, (_, index) => ({
  submissionId: `historical-${index}`,
  dropName: `Historical drop ${index}`,
  tileName: "Old tile",
  bossName: "Old boss",
  playerName: "Old player",
  teamName: "Team",
  approvedAt: new Date(fixedNow - (26 + index) * 60 * 1000).toISOString()
}));
const windowObject = {
  location: { href: "https://example.test/Events/event-a/Board?view=drops", pathname: "/Events/event-a/Board" },
  fetch: async url => {
    if (url.toString().includes("/recent-drops")) {
      recentFetchCalls++;
      const drops = [
        { submissionId: liveId, dropName: "Live drop", tileName: "Tile", bossName: "Boss", evidenceAssetId: "asset", playerName: "Player", teamName: "Team", approvedAt: liveApprovedAt },
        { submissionId: sameTimeLiveId, dropName: "Same-time live drop", tileName: "Tile", bossName: "Boss", playerName: "Second player", teamName: "Team", approvedAt: secondLiveApprovedAt },
        { submissionId: historicalSameTimeId, dropName: "Initial-boundary drop", tileName: "Tile", bossName: "Boss", playerName: "Boundary player", teamName: "Team", approvedAt: secondLiveApprovedAt },
        ...historicalDrops
      ];
      if (recentFetchCalls > 1) drops.splice(2, 0, { submissionId: lateLiveId, dropName: "Same-time live drop", tileName: "Tile", bossName: "Boss", playerName: "Late player", teamName: "Team", approvedAt: lateApprovedAt });
      return { ok: true, json: async () => ({ drops }) };
    }
    assert.match(url, /api\/drop-announcements\/new/);
    return { ok: true, json: async () => newIds };
  },
  addEventListener(type, listener) { listeners[type] = listener; },
  dispatchEvent(event) { listeners[event.type]?.(event); }
};
const documentObject = { querySelector: () => null, createElement: () => new Node() };

initialize(root, windowObject, documentObject);

(async () => {
  await new Promise(resolve => setImmediate(resolve));
  assert.ok(titleA.querySelector(".public-drop-new"));
  assert.ok(titleB.querySelector(".public-drop-new"));

  windowObject.dispatchEvent({ type: "bingo-progress-changed" });
  await new Promise(resolve => setImmediate(resolve));
  const liveCard = region.querySelector(`[data-public-recent-drop-id="${liveId}"]`);
  assert.equal(liveCard.querySelector(".public-ui-recent-drop-thumbnail").dataset.evidenceAlt, "Godkendt bevis fra Player", "live evidence alt text uses the rendered Danish template");
  assert.equal(liveCard.querySelector(".public-ui-recent-drop-thumbnail").children[0].alt, "Godkendt bevis fra Player", "live evidence image alt text matches the localized template");
  assert.equal(liveCard.dataset.publicRecentDropApprovedAt, liveApprovedAt, "live card retains the authoritative approval timestamp");
  assert.equal(liveCard.querySelector(".public-ui-recent-drop-player").textContent, "Player · 2 min ago", "live relative time is derived from approvedAt");
  assert.equal(region.querySelector('[data-public-recent-drop-id="historical-0"]'), null, "unloaded historical rows are not promoted into the live feed");
  assert.equal(grid.querySelectorAll("[data-public-recent-drop-id]").length, 27, "new approvals are added to the initial 25 cards");
  assert.equal(region.querySelector(`[data-public-recent-drop-id="${historicalSameTimeId}"]`), null, "an unseen lower-ID row at the initial timestamp remains historical");
  assert.deepEqual(grid.querySelectorAll("[data-public-recent-drop-id]").slice(0, 3).map(card => card.dataset.publicRecentDropId), [liveId, sameTimeLiveId, "a"], "new approvals are prepended in approval order ahead of existing cards");

  windowObject.dispatchEvent({ type: "bingo-progress-changed" });
  await new Promise(resolve => setImmediate(resolve));
  assert.equal(region.querySelectorAll(`[data-public-recent-drop-id="${liveId}"]`).length, 1, "repeated updates do not duplicate a live card");
  assert.equal(region.querySelectorAll(`[data-public-recent-drop-id="${sameTimeLiveId}"]`).length, 1, "a higher canonical ID at the initial timestamp is reconciled once");
  assert.equal(region.querySelectorAll(`[data-public-recent-drop-id="${lateLiveId}"]`).length, 1, "a later approval sharing a prior approval timestamp is still reconciled");
  assert.equal(region.querySelectorAll(`[data-public-recent-drop-id="${historicalSameTimeId}"]`).length, 0, "the lower-ID same-time historical row remains absent");
  assert.equal(titleA.parentElement, cardA, "existing card remains in place after reconciliation");

  await Promise.all([listeners["bingo-progress-changed"](), listeners["bingo-progress-changed"]()]);
  await new Promise(resolve => setImmediate(resolve));
  assert.equal(region.querySelectorAll(`[data-public-recent-drop-id="${liveId}"]`).length, 1, "overlapping invalidations do not duplicate a live card");

  newIds = ["b"];
  windowObject.dispatchEvent({ type: "drop-announcement-acknowledged", detail: { eventId: "event-a", submissionIds: ["a"] } });
  await new Promise(resolve => setImmediate(resolve));
  assert.equal(titleA.querySelector(".public-drop-new"), null, "successful viewed-one acknowledgement removes its loaded badge");
  assert.ok(titleB.querySelector(".public-drop-new"), "successful viewed-one acknowledgement preserves other badges");

  // A failed server acknowledgement emits no success event, so the badge remains.
  assert.ok(titleB.querySelector(".public-drop-new"), "failed acknowledgement leaves the badge in place");

  newIds = [];
  windowObject.dispatchEvent({ type: "drop-announcement-acknowledged", detail: { eventId: "event-a", all: true } });
  await new Promise(resolve => setImmediate(resolve));
  assert.equal(titleA.querySelector(".public-drop-new"), null, "CLEAR ALL removes the first loaded badge");
  assert.equal(titleB.querySelector(".public-drop-new"), null, "CLEAR ALL removes every loaded badge");
})().catch(error => { setImmediate(() => { throw error; }); });

const assert = require("node:assert/strict");
const fs = require("node:fs");
const path = require("node:path");

const { initialize } = require(path.join(__dirname, "../../src/Bingo.Web/wwwroot/js/captain-ledger.js"));

const ledgerMarkup = fs.readFileSync(path.join(__dirname, "../../src/Bingo.Web/Pages/Captain/_SubmissionLedger.cshtml"), "utf8");
assert.match(ledgerMarkup, /<table class="public-ui-table captain-ledger-table" data-public-leaderboard-sort-table/);

class Node {
  constructor(tagName, options = {}) {
    this.tagName = tagName.toUpperCase();
    this.dataset = options.dataset ?? {};
    this.value = options.value ?? "";
    this.href = options.href ?? "";
    this.listeners = {};
    this.children = [];
    this.parentElement = null;
    this.attributes = {};
    this.classList = { toggle() {} };
  }

  addEventListener(type, listener) { (this.listeners[type] ??= []).push(listener); }
  dispatch(type, extra = {}) {
    const event = { target: this, defaultPrevented: false, preventDefault() { this.defaultPrevented = true; }, ...extra };
    for (const listener of this.listeners[type] ?? []) listener(event);
    return event;
  }
  append(...children) { children.forEach(child => { child.parentElement = this; this.children.push(child); }); }
  appendChild(child) {
    if (child.parentElement === this) this.children.splice(this.children.indexOf(child), 1);
    else if (child.parentElement) child.parentElement.children.splice(child.parentElement.children.indexOf(child), 1);
    this.append(child);
    return child;
  }
  setAttribute(name, value) { this.attributes[name] = String(value); }
  getAttribute(name) { return this.attributes[name]; }
  removeAttribute(name) { delete this.attributes[name]; }
  contains(node) { return this === node || this.children.some(child => child.contains(node)); }
  focus() {}
  matches(selector) {
    if (selector === "tbody") return this.tagName === "TBODY";
    if (selector === "[data-captain-ledger-form]") return this.dataset.captainLedgerForm !== undefined;
    if (selector === "[data-captain-ledger-search]") return this.dataset.captainLedgerSearch !== undefined;
    if (selector === "[data-captain-ledger-player]") return this.dataset.captainLedgerPlayer !== undefined;
    if (selector === "[data-captain-ledger-clear]") return this.dataset.captainLedgerClear !== undefined;
    if (selector === "[data-captain-ledger-results]") return this.dataset.captainLedgerResults !== undefined;
    if (selector === "[data-captain-ledger-page]") return this.dataset.captainLedgerPage !== undefined;
    if (selector === "[data-public-leaderboard-sort-table]") return this.dataset.publicLeaderboardSortTable !== undefined;
    if (selector === "[data-public-leaderboard-sort-control]") return this.dataset.publicLeaderboardSortControl !== undefined;
    if (selector === "[data-public-leaderboard-sort-row]") return this.dataset.publicLeaderboardSortRow !== undefined;
    return false;
  }
  querySelector(selector) { return this.querySelectorAll(selector)[0] ?? null; }
  querySelectorAll(selector) {
    const found = [];
    const visit = node => {
      if (node.matches?.(selector)) found.push(node);
      node.children.forEach(visit);
    };
    this.children.forEach(visit);
    return found;
  }
  replaceWith(replacement) {
    const parent = this.parentElement;
    if (!parent) return;
    parent.children.splice(parent.children.indexOf(this), 1, replacement);
    replacement.parentElement = parent;
  }
  closest(selector) { return this.matches(selector) ? this : null; }
}

function row(status, drop, tile, player, submitted) {
  return new Node("tr", {
    dataset: {
      publicLeaderboardSortRow: "",
      sortStatus: status,
      sortDrop: drop,
      sortTile: tile,
      sortPlayer: player,
      sortSubmitted: String(submitted)
    }
  });
}

function table(rows) {
  const tableNode = new Node("table", { dataset: { publicLeaderboardSortTable: "" } });
  const head = new Node("thead");
  const body = new Node("tbody");
  for (const key of ["status", "drop", "tile", "player", "submitted"]) {
    const header = new Node("th");
    const control = new Node("button", { dataset: { publicLeaderboardSortControl: "", publicLeaderboardSortKey: key, publicLeaderboardSortType: key === "submitted" ? "number" : "text", publicLeaderboardSortLabel: key } });
    header.append(control);
    head.append(header);
  }
  head.append(new Node("th"));
  body.append(...rows);
  tableNode.append(head, body);
  return tableNode;
}

const documentNode = new Node("document");
const form = new Node("form", { dataset: { captainLedgerForm: "true" } });
form.action = "https://example.test/Submissions";
form.elements = { eventId: { value: "event-1" }, teamId: { value: "team-1" } };
const search = new Node("input", { value: "" });
search.dataset.captainLedgerSearch = "true";
const clear = new Node("button", { dataset: { captainLedgerClear: "true" } });
const player = new Node("select", { value: "" });
player.dataset.captainLedgerPlayer = "true";
form.append(search, clear, player);
form.search = search;
form.clear = clear;
form.player = player;
documentNode.append(form);

const initialResults = new Node("div", { dataset: { captainLedgerResults: "true" } });
initialResults.append(table([row("Approved", "Drop B", "Tile B", "Player B", 2), row("Pending", "Drop A", "Tile A", "Player A", 1)]));
documentNode.append(initialResults);

const replacements = [
  new Node("div", { dataset: { captainLedgerResults: "true" } }),
  new Node("div", { dataset: { captainLedgerResults: "true" } }),
  new Node("div", { dataset: { captainLedgerResults: "true" } }),
  new Node("div", { dataset: { captainLedgerResults: "true" } })
];
replacements[0].append(table([row("Rejected", "Drop D", "Tile D", "Player D", 4), row("Approved", "Drop C", "Tile C", "Player C", 3)]));
replacements[1].append(table([row("Pending", "Drop F", "Tile F", "Player F", 6), row("Approved", "Drop E", "Tile E", "Player E", 5)]));
replacements[2].append(table([row("Pending", "Drop H", "Tile H", "Player H", 8), row("Approved", "Drop G", "Tile G", "Player G", 7)]));
replacements[3].append(table([row("Pending", "Drop J", "Tile J", "Player J", 10), row("Approved", "Drop I", "Tile I", "Player I", 9)]));
const nextPage = new Node("a", { dataset: { captainLedgerPage: "true" }, href: "https://example.test/Submissions?eventId=event-1&teamId=team-1&search=ledger&player=player-1&ledgerPage=2" });
replacements[2].append(nextPage);

const fetches = [];
const historyUrls = [];
const windowObject = {
  location: { href: form.action, assign(value) { this.assigned = value; } },
  history: { state: null, replaceState(_state, _title, value) { historyUrls.push(value); } },
  DOMParser: class { parseFromString() { return { querySelector() { return replacements.shift(); } }; } },
  setTimeout(callback) { callback(); return 1; },
  clearTimeout() {},
  fetch: async (url, options) => {
    fetches.push({ url: new URL(url), options });
    return { ok: true, text: async () => "<div data-captain-ledger-results></div>" };
  }
};

initialize(documentNode, windowObject);

(async () => {
  const initialTable = initialResults.querySelector("[data-public-leaderboard-sort-table]");
  const initialSorts = [
    ["status", "Approved", "ascending"],
    ["drop", "Drop A", "ascending"],
    ["tile", "Tile A", "ascending"],
    ["player", "Player A", "ascending"],
    ["submitted", "2", "descending"]
  ];
  for (const [key, expected, direction] of initialSorts) {
    const control = initialTable.querySelectorAll("[data-public-leaderboard-sort-control]").find(value => value.dataset.publicLeaderboardSortKey === key);
    const sortEvent = control.dispatch("click");
    assert.equal(sortEvent.defaultPrevented, true);
    assert.equal(initialTable.querySelector("tbody").children[0].dataset[`sort${key[0].toUpperCase()}${key.slice(1)}`], expected);
    assert.equal(control.parentElement.getAttribute("aria-sort"), direction);
  }
  const submittedSort = initialTable.querySelectorAll("[data-public-leaderboard-sort-control]")[4];
  submittedSort.dispatch("click");
  assert.equal(initialTable.querySelector("tbody").children[0].dataset.sortSubmitted, "1");
  assert.equal(submittedSort.parentElement.getAttribute("aria-sort"), "ascending");
  assert.equal(fetches.length, 0);

  player.value = "player-1";
  await player.dispatch("change");
  await new Promise(resolve => setImmediate(resolve));
  assert.equal(fetches[0].options.headers["X-Requested-With"], "XMLHttpRequest");
  assert.equal(fetches[0].url.searchParams.get("player"), "player-1");
  assert.equal(fetches[0].url.searchParams.get("ledgerPage"), "1");
  const replacedTable = documentNode.querySelector("[data-public-leaderboard-sort-table]");
  const replacedSorts = [
    ["status", "Approved", "ascending"],
    ["drop", "Drop C", "ascending"],
    ["tile", "Tile C", "ascending"],
    ["player", "Player C", "ascending"],
    ["submitted", "4", "descending"]
  ];
  for (const [key, expected, direction] of replacedSorts) {
    const control = replacedTable.querySelectorAll("[data-public-leaderboard-sort-control]").find(value => value.dataset.publicLeaderboardSortKey === key);
    control.dispatch("click");
    assert.equal(replacedTable.querySelector("tbody").children[0].dataset[`sort${key[0].toUpperCase()}${key.slice(1)}`], expected);
    assert.equal(control.parentElement.getAttribute("aria-sort"), direction);
  }

  search.value = "ledger";
  await search.dispatch("input");
  await new Promise(resolve => setImmediate(resolve));
  assert.equal(fetches[1].url.searchParams.get("search"), "ledger");
  assert.equal(fetches[1].url.searchParams.get("player"), "player-1");
  assert.equal(fetches[1].url.searchParams.get("ledgerPage"), "1");

  await clear.dispatch("click");
  await new Promise(resolve => setImmediate(resolve));
  assert.equal(search.value, "");
  assert.equal(fetches[2].url.searchParams.has("search"), false);
  assert.equal(fetches[2].url.searchParams.get("player"), "player-1");

  const delegatedClick = { target: nextPage, defaultPrevented: false, preventDefault() { this.defaultPrevented = true; } };
  for (const listener of documentNode.listeners.click ?? []) listener(delegatedClick);
  await new Promise(resolve => setImmediate(resolve));
  assert.equal(delegatedClick.defaultPrevented, true);
  assert.equal(fetches[3].url.searchParams.get("ledgerPage"), "2");
  assert.equal(fetches[3].url.searchParams.get("search"), "ledger");
  assert.equal(fetches[3].url.searchParams.get("player"), "player-1");
  assert.equal(historyUrls.length, 4);
  assert.equal(windowObject.location.assigned, undefined);
})();

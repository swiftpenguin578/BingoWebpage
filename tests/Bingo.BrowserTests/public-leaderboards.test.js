const assert = require("node:assert/strict");
const fs = require("node:fs");
const path = require("node:path");
const { initialize } = require("../../src/Bingo.Web/wwwroot/js/public-leaderboards.js");

const localizedDocument = { documentElement: { dataset: {
  publicSelectOption: "Select option",
  publicPageTemplate: "Page {0} of {1}",
  publicSortTemplate: "Sort by {0}",
  publicSortCurrentTemplate: "Currently sorted by {0} {1}. Activate to sort {2}.",
  publicSortAscending: "ascending",
  publicSortDescending: "descending"
} } };

class Node {
  constructor({ dataset = {}, href = null, children = [], tagName = "div", open = false, id = null, value = "" } = {}) {
    this.dataset = dataset;
    this.ownerDocument = localizedDocument;
    this.href = href;
    this.id = id;
    this.tagName = tagName.toUpperCase();
    this.open = open;
    this.value = value;
    this.children = children;
    this.listeners = {};
    this.attributes = {};
    this.hidden = false;
    this.classList = { values: new Set(), contains: name => this.classList.values.has(name), toggle: (name, enabled) => enabled ? this.classList.values.add(name) : this.classList.values.delete(name) };
    children.forEach(child => child.parentElement = this);
  }

  addEventListener(type, listener) { (this.listeners[type] ??= []).push(listener); }
  dispatch(type, event) { return (this.listeners[type] ?? []).map(listener => listener({ type, target: this, defaultPrevented: false, preventDefault() { this.defaultPrevented = true; }, ...event })); }
  appendChild(child) { this.children = this.children.filter(value => value !== child); this.children.push(child); child.parentElement = this; return child; }
  setAttribute(name, value) { this.attributes[name] = String(value); }
  removeAttribute(name) { delete this.attributes[name]; }
  getAttribute(name) { return name === "id" ? this.id : this.attributes[name]; }
  focus() { this.focused = true; }
  contains(node) { return node === this || this.children.some(child => child.contains(node)); }
  closest(selector) { return selector === "[data-public-leaderboard-view]" && this.dataset.publicLeaderboardView ? this : null; }
  querySelector(selector) { return this.querySelectorAll(selector)[0] ?? null; }
  querySelectorAll(selector) {
    const matches = node => selector.split(",").some(value => {
      const current = value.trim();
      if (current === "summary") return node.tagName === "SUMMARY";
      if (current === "[data-public-ui-compact-dropdown]") return node.dataset.publicUiCompactDropdown !== undefined;
      if (current === "[data-public-ui-dropdown-option]") return node.dataset.publicUiDropdownOption !== undefined;
      if (current === "[data-public-ui-dropdown-label]") return node.dataset.publicUiDropdownLabel !== undefined;
      if (current === "[data-public-ui-dropdown-check]") return node.dataset.publicUiDropdownCheck !== undefined;
      if (current === "[data-public-leaderboard-view]") return node.dataset.publicLeaderboardView !== undefined;
      if (current === "[data-public-leaderboard-panel]") return node.dataset.publicLeaderboardPanel !== undefined;
      if (current === "[data-public-leaderboard-switcher]") return node.dataset.publicLeaderboardSwitcher !== undefined;
      if (current === "[data-public-leaderboard-details]") return node.dataset.publicLeaderboardDetails !== undefined;
      if (current === "[data-public-leaderboards-layout]") return node.dataset.publicLeaderboardsLayout !== undefined;
      if (current === "[data-public-leaderboards-rail-toggle]") return node.dataset.publicLeaderboardsRailToggle !== undefined;
      if (current === "[data-public-leaderboards-rail-body]") return node.dataset.publicLeaderboardsRailBody !== undefined;
      if (current === "[data-public-leaderboard-expand-control]") return node.dataset.publicLeaderboardExpandControl !== undefined;
      if (current === "[data-public-leaderboard-expand-heading]") return node.dataset.publicLeaderboardExpandHeading !== undefined;
      if (current === "[data-public-leaderboard-expand-cell]") return node.dataset.publicLeaderboardExpandCell !== undefined;
      if (current === "[data-public-leaderboard-standings-metric]") return node.dataset.publicLeaderboardStandingsMetric !== undefined;
      if (current === "[data-public-masthead-metric-selector]") return node.dataset.publicMastheadMetricSelector !== undefined;
      if (current === "[data-public-masthead-metric]") return node.dataset.publicMastheadMetric !== undefined;
      if (current === "[data-public-leaderboard-sort-table]") return node.dataset.publicLeaderboardSortTable !== undefined;
      if (current === "[data-public-leaderboard-pagination]") return node.dataset.publicLeaderboardPagination !== undefined;
      if (current === "[data-public-leaderboard-page-label]") return node.dataset.publicLeaderboardPageLabel !== undefined;
      if (current === "[data-public-leaderboard-page-control]") return node.dataset.publicLeaderboardPageControl !== undefined;
      if (current === "[data-public-leaderboard-sort-control]") return node.dataset.publicLeaderboardSortControl !== undefined;
      if (current === "[data-public-leaderboard-sort-row]") return node.dataset.publicLeaderboardSortRow !== undefined;
      if (current === "tbody") return node.tagName === "TBODY";
      return current === "[data-public-leaderboard-table]" && node.dataset.publicLeaderboardTable !== undefined;
    });
    return this.children.flatMap(child => [ ...(matches(child) ? [child] : []), ...child.querySelectorAll(selector) ]);
  }
}

const activityLink = new Node({ dataset: { publicLeaderboardView: "activity" }, href: "https://example.test/Events/test/Board?view=leaderboards&ranking=activity" });
const dropsLink = new Node({ dataset: { publicLeaderboardView: "drops" }, href: "https://example.test/Events/test/Board?view=leaderboards&ranking=drops" });
const playersLink = new Node({ dataset: { publicLeaderboardView: "players" }, href: "https://example.test/Events/test/Board?view=leaderboards&ranking=players" });
const switcher = new Node({ dataset: { publicLeaderboardSwitcher: "" }, children: [activityLink, dropsLink, playersLink] });
const activitySummary = new Node({ tagName: "summary" });
const activityDetails = new Node({ dataset: { publicLeaderboardDetails: "activity-a", publicLeaderboardRank: "1" }, id: "activity-details-team-a", open: false, children: [activitySummary] });
const activitySummaryB = new Node({ tagName: "summary" });
const activityDetailsB = new Node({ dataset: { publicLeaderboardDetails: "activity-b", publicLeaderboardRank: "1" }, id: "activity-details-team-b", open: false, children: [activitySummaryB] });
const activityControl = new Node({ dataset: { publicLeaderboardExpandControl: "", publicLeaderboardRank: "1" } });
activityControl.setAttribute("aria-controls", "activity-details-team-a");
const activityControlB = new Node({ dataset: { publicLeaderboardExpandControl: "", publicLeaderboardRank: "1" } });
activityControlB.setAttribute("aria-controls", "activity-details-team-b");
const activityHeading = new Node({ dataset: { publicLeaderboardExpandHeading: "" } });
const activityCell = new Node({ dataset: { publicLeaderboardExpandCell: "" } });
const activityTable = new Node({ dataset: { publicLeaderboardTable: "" }, children: [activityHeading, activityDetails, activityDetailsB, activityCell, activityControlB, activityControl] });
const makeSortTable = (columns, rows, players = false) => {
  const headers = columns.map(({ key, type, label }) => {
    const control = new Node({ dataset: { publicLeaderboardSortControl: "", publicLeaderboardSortKey: key, publicLeaderboardSortType: type, publicLeaderboardSortLabel: label } });
    const header = new Node({ tagName: "th", children: [control] });
    header.setAttribute("aria-sort", "none");
    return header;
  });
  const body = new Node({ tagName: "tbody", children: rows });
  return new Node({ dataset: { publicLeaderboardSortTable: "", ...(players ? { publicLeaderboardPlayersTable: "" } : {}) }, children: [...headers, body] });
};
const ehbColumns = [
  { key: "rank", type: "number", label: "Rank" },
  { key: "player", type: "text", label: "Player" },
  { key: "ehb-gained", type: "number", label: "EHB gained" }
];
const ehbRows = [
  new Node({ dataset: { publicLeaderboardSortRow: "", sortRank: "1", sortPlayer: "Zulu", sortEhbGained: "20", rankLabel: "#1" } }),
  new Node({ dataset: { publicLeaderboardSortRow: "", sortRank: "2", sortPlayer: "Alpha", sortEhbGained: "10", rankLabel: "#2" } })
];
const ehbRowsIndependent = [
  new Node({ dataset: { publicLeaderboardSortRow: "", sortRank: "1", sortPlayer: "Bravo", sortEhbGained: "1", rankLabel: "#1" } }),
  new Node({ dataset: { publicLeaderboardSortRow: "", sortRank: "2", sortPlayer: "Alpha", sortEhbGained: "2", rankLabel: "#2" } })
];
const ehbSortTable = makeSortTable(ehbColumns, ehbRows);
const ehbSortTableIndependent = makeSortTable(ehbColumns, ehbRowsIndependent);
const activityPanel = new Node({ dataset: { publicLeaderboardPanel: "activity" }, children: [activityTable, ehbSortTable, ehbSortTableIndependent] });
const dropsSummary = new Node({ tagName: "summary" });
const dropsDetails = new Node({ dataset: { publicLeaderboardDetails: "drops" }, id: "drops-details", open: false, children: [dropsSummary] });
const dropsControl = new Node({ dataset: { publicLeaderboardExpandControl: "" } });
dropsControl.setAttribute("aria-controls", "drops-details");
const dropsHeading = new Node({ dataset: { publicLeaderboardExpandHeading: "" } });
const dropsCell = new Node({ dataset: { publicLeaderboardExpandCell: "" } });
const dropsTable = new Node({ dataset: { publicLeaderboardTable: "" }, children: [dropsHeading, dropsDetails, dropsCell, dropsControl] });
const dropsSortTable = makeSortTable([
  { key: "rank", type: "number", label: "Rank" },
  { key: "player", type: "text", label: "Player" },
  { key: "drop-ehb", type: "number", label: "Drop EHB" },
  { key: "total-drops", type: "number", label: "Total drops" },
  { key: "team-share", type: "number", label: "Team share" }
], [
  new Node({ dataset: { publicLeaderboardSortRow: "", sortRank: "1", sortPlayer: "Bravo", sortDropEhb: "4", sortTotalDrops: "8", sortTeamShare: "50", rankLabel: "#1" } }),
  new Node({ dataset: { publicLeaderboardSortRow: "", sortRank: "2", sortPlayer: "Alpha", sortDropEhb: "2", sortTotalDrops: "4", sortTeamShare: "25", rankLabel: "#2" } })
]);
const dropsPanel = new Node({ dataset: { publicLeaderboardPanel: "drops" }, children: [dropsTable, dropsSortTable] });
const playersRows = Array.from({ length: 41 }, (_, index) => new Node({ dataset: {
  publicLeaderboardSortRow: "", sortRank: String(index + 1), sortPlayer: `Player${String(index + 1).padStart(2, "0")}`,
  sortTeam: index % 2 === 0 ? "Dragon Knights" : "Loot Goblins", sortEhbGained: String(100 - index),
  sortDropEhb: String(80 - index), sortTotalDrops: String(40 - index), rankLabel: `#${index + 1}`
} }));
delete playersRows[40].dataset.sortRank;
delete playersRows[40].dataset.sortEhbGained;
delete playersRows[40].dataset.sortDropEhb;
const playersSortTable = makeSortTable([
  { key: "rank", type: "number", label: "Rank" },
  { key: "player", type: "text", label: "Player" },
  { key: "team", type: "text", label: "Team" },
  { key: "ehb-gained", type: "number", label: "EHB gained" },
  { key: "drop-ehb", type: "number", label: "Drop EHB" },
  { key: "total-drops", type: "number", label: "Total drops" }
], playersRows, true);
const playersPageLabel = new Node({ dataset: { publicLeaderboardPageLabel: "" } });
const playersPageControls = ["first", "previous", "next", "last"].map(page => new Node({ dataset: { publicLeaderboardPageControl: page } }));
const playersPager = new Node({ dataset: { publicLeaderboardPagination: "" }, children: [playersPageLabel, ...playersPageControls] });
const playersPanel = new Node({ dataset: { publicLeaderboardPanel: "players" }, children: [new Node({ children: [playersSortTable, playersPager] })] });
const mastheadMetricSelector = new Node({ tagName: "details", dataset: { publicUiCompactDropdown: "", publicMastheadMetricSelector: "", publicMastheadEvent: "event-a" } });
const mastheadTrigger = new Node({ tagName: "summary" });
const mastheadOptions = ["spooned", "drops", "ehb"].map((key, index) => new Node({ dataset: { publicUiDropdownOption: key, publicUiDropdownLabel: key === "spooned" ? "Most spooned" : key === "drops" ? "Highest DEHB" : "Highest EHB" }, children: [new Node({ dataset: { publicUiDropdownCheck: "" } })] }));
mastheadOptions.forEach((option, index) => option.setAttribute("aria-selected", String(index === 0)));
const mastheadLabel = new Node({ dataset: { publicUiDropdownLabel: "" } });
const mastheadMenu = new Node({ children: mastheadOptions });
mastheadMetricSelector.children = [mastheadTrigger, mastheadLabel, mastheadMenu];
mastheadTrigger.parentElement = mastheadMetricSelector;
mastheadLabel.parentElement = mastheadMetricSelector;
mastheadMenu.parentElement = mastheadMetricSelector;
const mastheadSpoonedMetric = new Node({ dataset: { publicMastheadMetric: "spooned" } });
const mastheadDropsMetric = new Node({ dataset: { publicMastheadMetric: "drops" } });
const mastheadEhbMetric = new Node({ dataset: { publicMastheadMetric: "ehb" } });
const activityMetric = new Node({ dataset: { publicLeaderboardStandingsMetric: "activity" } });
const dropsMetric = new Node({ dataset: { publicLeaderboardStandingsMetric: "drops" } });
const standings = new Node({ children: [activityMetric, dropsMetric] });
const railToggle = new Node({ dataset: { publicLeaderboardsRailToggle: "", expandedLabel: "Collapse Bingo standings", collapsedLabel: "Expand Bingo standings" } });
const railBody = new Node({ dataset: { publicLeaderboardsRailBody: "" } });
const leaderboardsLayout = new Node({ dataset: { publicLeaderboardsLayout: "" }, children: [railToggle, railBody] });
const root = new Node({ children: [mastheadMetricSelector, mastheadSpoonedMetric, mastheadDropsMetric, mastheadEhbMetric, switcher, activityPanel, dropsPanel, playersPanel, standings, leaderboardsLayout] });
const stackedMedia = {
  matches: false,
  listeners: [],
  addEventListener(_type, listener) { this.listeners.push(listener); },
  setMatches(value) { this.matches = value; this.listeners.forEach(listener => listener()); }
};
const window = {
  location: { href: activityLink.href },
  history: { pushState(_state, _title, href) { window.location.href = href; } },
  sessionStorage: { values: new Map(), getItem(key) { return this.values.get(key) ?? null; }, setItem(key, value) { this.values.set(key, String(value)); } },
  addEventListener(type, listener) { this.listeners ??= {}; (this.listeners[type] ??= []).push(listener); },
  matchMedia(query) { assert.equal(query, "(max-width: 900px)"); return stackedMedia; }
};

initialize(root, window);
initialize(root, window);
assert.equal(mastheadMetricSelector.dataset.publicUiDropdownValue, "spooned");
assert.equal(mastheadTrigger.attributes["aria-expanded"], "false");
assert.equal(mastheadSpoonedMetric.hidden, false);
assert.equal(mastheadDropsMetric.hidden, true);
assert.equal(mastheadEhbMetric.hidden, true);
mastheadTrigger.dispatch("click");
assert.equal(mastheadMetricSelector.open, false);
mastheadMetricSelector.open = true;
mastheadTrigger.dispatch("keydown", { key: "ArrowDown" });
assert.equal(mastheadOptions[1].focused, true);
mastheadOptions[1].dispatch("click");
assert.equal(mastheadSpoonedMetric.hidden, true);
assert.equal(mastheadDropsMetric.hidden, false);
assert.equal(window.sessionStorage.getItem("public-leaderboard-masthead-metric:event-a"), "drops");
mastheadMetricSelector.open = true;
mastheadOptions[1].dispatch("keydown", { key: "ArrowDown" });
assert.equal(mastheadOptions[2].focused, true);
mastheadOptions[2].dispatch("keydown", { key: "Enter" });
assert.equal(mastheadEhbMetric.hidden, false);
assert.equal(mastheadOptions[2].attributes["aria-selected"], "true");
assert.equal(mastheadOptions[2].querySelector("[data-public-ui-dropdown-check]").attributes.hidden, undefined);
assert.equal(mastheadTrigger.focused, true);
mastheadMetricSelector.open = true;
root.dispatch("click", { target: new Node() });
assert.equal(mastheadMetricSelector.open, false);
mastheadMetricSelector.open = true;
mastheadOptions[2].dispatch("keydown", { key: "Escape" });
assert.equal(mastheadMetricSelector.open, false);
const restoredMetricSelector = new Node({ tagName: "details", dataset: { publicUiCompactDropdown: "", publicMastheadMetricSelector: "", publicMastheadEvent: "event-a" } });
const restoredTrigger = new Node({ tagName: "summary" });
const restoredOptions = ["spooned", "drops", "ehb"].map(key => new Node({ dataset: { publicUiDropdownOption: key, publicUiDropdownLabel: key } }));
restoredOptions[0].setAttribute("aria-selected", "true");
restoredMetricSelector.children = [restoredTrigger, new Node({ dataset: { publicUiDropdownLabel: "" } }), new Node({ children: restoredOptions })];
restoredMetricSelector.children.forEach(child => child.parentElement = restoredMetricSelector);
const restoredSpoonedMetric = new Node({ dataset: { publicMastheadMetric: "spooned" } });
const restoredDropsMetric = new Node({ dataset: { publicMastheadMetric: "drops" } });
const restoredEhbMetric = new Node({ dataset: { publicMastheadMetric: "ehb" } });
initialize(new Node({ children: [restoredMetricSelector, restoredSpoonedMetric, restoredDropsMetric, restoredEhbMetric] }), window);
assert.equal(restoredMetricSelector.dataset.publicUiDropdownValue, "ehb");
assert.equal(restoredEhbMetric.hidden, false);
const otherEventSelector = new Node({ tagName: "details", dataset: { publicUiCompactDropdown: "", publicMastheadMetricSelector: "", publicMastheadEvent: "event-b" } });
const otherEventTrigger = new Node({ tagName: "summary" });
const otherEventOptions = ["spooned", "drops", "ehb"].map(key => new Node({ dataset: { publicUiDropdownOption: key, publicUiDropdownLabel: key } }));
otherEventOptions[0].setAttribute("aria-selected", "true");
otherEventSelector.children = [otherEventTrigger, new Node({ dataset: { publicUiDropdownLabel: "" } }), new Node({ children: otherEventOptions })];
otherEventSelector.children.forEach(child => child.parentElement = otherEventSelector);
const otherEventSpoonedMetric = new Node({ dataset: { publicMastheadMetric: "spooned" } });
const otherEventDropsMetric = new Node({ dataset: { publicMastheadMetric: "drops" } });
const otherEventEhbMetric = new Node({ dataset: { publicMastheadMetric: "ehb" } });
initialize(new Node({ children: [otherEventSelector, otherEventSpoonedMetric, otherEventDropsMetric, otherEventEhbMetric] }), window);
assert.equal(otherEventSelector.dataset.publicUiDropdownValue, "spooned");
assert.equal(otherEventSpoonedMetric.hidden, false);
const ehbSortControls = ehbSortTable.querySelectorAll("[data-public-leaderboard-sort-control]");
assert.equal(ehbSortControls[0].parentElement.attributes["aria-sort"], "none");
assert.equal(ehbRows[0].dataset.rankLabel, "#1");
ehbSortControls[2].dispatch("click");
assert.equal(ehbSortControls[2].parentElement.attributes["aria-sort"], "descending");
assert.equal(ehbSortControls[2].classList.values.has("is-descending"), true);
assert.deepEqual(ehbSortTable.querySelectorAll("[data-public-leaderboard-sort-row]"), ehbRows);
ehbSortControls[2].dispatch("click");
assert.equal(ehbSortControls[2].parentElement.attributes["aria-sort"], "ascending");
assert.deepEqual(ehbSortTable.querySelectorAll("[data-public-leaderboard-sort-row]"), [ehbRows[1], ehbRows[0]]);
assert.equal(ehbRows[1].dataset.rankLabel, "#2");
ehbSortControls[1].dispatch("click");
assert.equal(ehbSortControls[1].parentElement.attributes["aria-sort"], "ascending");
assert.deepEqual(ehbSortTable.querySelectorAll("[data-public-leaderboard-sort-row]"), [ehbRows[1], ehbRows[0]]);
ehbSortControls[1].dispatch("click");
assert.equal(ehbSortControls[1].parentElement.attributes["aria-sort"], "descending");
assert.deepEqual(ehbSortTable.querySelectorAll("[data-public-leaderboard-sort-row]"), ehbRows);
const independentSortControls = ehbSortTableIndependent.querySelectorAll("[data-public-leaderboard-sort-control]");
independentSortControls[0].dispatch("click");
assert.equal(independentSortControls[0].parentElement.attributes["aria-sort"], "ascending");
assert.deepEqual(ehbSortTableIndependent.querySelectorAll("[data-public-leaderboard-sort-row]"), ehbRowsIndependent);
independentSortControls[0].dispatch("click");
assert.equal(independentSortControls[0].parentElement.attributes["aria-sort"], "descending");
assert.deepEqual(ehbSortTableIndependent.querySelectorAll("[data-public-leaderboard-sort-row]"), [ehbRowsIndependent[1], ehbRowsIndependent[0]]);
independentSortControls[2].dispatch("click");
assert.deepEqual(ehbSortTableIndependent.querySelectorAll("[data-public-leaderboard-sort-row]"), [ehbRowsIndependent[1], ehbRowsIndependent[0]]);
assert.deepEqual(ehbSortTable.querySelectorAll("[data-public-leaderboard-sort-row]"), ehbRows);
const dropSortControls = dropsSortTable.querySelectorAll("[data-public-leaderboard-sort-control]");
dropSortControls[4].dispatch("click");
assert.equal(dropSortControls[4].parentElement.attributes["aria-sort"], "descending");
dropSortControls[4].dispatch("click");
assert.deepEqual(dropsSortTable.querySelectorAll("[data-public-leaderboard-sort-row]").map(row => row.dataset.rankLabel), ["#2", "#1"]);
assert.equal(activityDetails.dataset.publicLeaderboardRank, activityDetailsB.dataset.publicLeaderboardRank);
assert.notEqual(activityDetails.id, activityDetailsB.id);
assert.equal(activityControl.getAttribute("aria-controls"), activityDetails.id);
assert.equal(activityControlB.getAttribute("aria-controls"), activityDetailsB.id);
assert.equal(activityPanel.hidden, false);
assert.equal(dropsPanel.hidden, true);
assert.equal(activityLink.classList.values.has("is-current"), true);
assert.equal(dropsLink.classList.values.has("is-current"), false);
assert.equal(activityLink.attributes["aria-current"], "page");
assert.equal(dropsLink.attributes["aria-current"], undefined);
assert.equal(activityHeading.attributes.hidden, undefined);
assert.equal(activityCell.attributes.hidden, undefined);
assert.equal(activityControl.attributes.hidden, undefined);
assert.equal(activityControl.attributes["aria-expanded"], "false");
assert.equal(activitySummary.attributes.tabindex, "-1");
assert.equal(activitySummary.attributes["aria-disabled"], "true");
assert.equal(activityMetric.hidden, false);
assert.equal(dropsMetric.hidden, true);
assert.equal(playersPanel.hidden, true);
assert.equal(playersPager.hidden, false);
assert.equal(playersPageLabel.textContent, "Page 1 of 3");
assert.equal(playersRows.filter(row => !row.hidden).length, 20);
assert.equal(playersPageControls[0].disabled, true);
assert.equal(playersPageControls[1].disabled, true);
assert.equal(playersPageControls[2].disabled, false);
assert.equal(playersPageControls[3].disabled, false);
assert.equal(railBody.hidden, false);
assert.equal(railToggle.attributes["aria-expanded"], "true");

railToggle.dispatch("click");
assert.equal(leaderboardsLayout.classList.values.has("is-rail-collapsed"), true);
assert.equal(railBody.hidden, true);
assert.equal(railToggle.attributes["aria-expanded"], "false");
railToggle.dispatch("click");
assert.equal(railBody.hidden, false);
assert.equal(railToggle.attributes["aria-expanded"], "true");
railToggle.dispatch("click");
stackedMedia.setMatches(true);
assert.equal(railBody.hidden, false);
assert.equal(railToggle.disabled, true);
assert.equal(railToggle.attributes["aria-expanded"], "true");
assert.equal(railToggle.attributes["aria-disabled"], "true");

activityControl.dispatch("click");
assert.equal(activityDetails.open, true);
assert.equal(activityDetailsB.open, false);
assert.equal(activityControl.attributes["aria-expanded"], "true");
assert.equal(activityControl.classList.values.has("is-expanded"), true);
activityControl.dispatch("click");
assert.equal(activityDetails.open, false);
assert.equal(activityDetailsB.open, false);
assert.equal(activityControl.attributes["aria-expanded"], "false");
assert.equal(activityControl.classList.values.has("is-expanded"), false);
activityControlB.dispatch("click");
assert.equal(activityDetails.open, false);
assert.equal(activityDetailsB.open, true);
assert.equal(activityControlB.attributes["aria-expanded"], "true");
assert.equal(dropsControl.attributes["aria-expanded"], "false");

switcher.dispatch("click", { target: dropsLink });
assert.equal(activityPanel.hidden, true);
assert.equal(dropsPanel.hidden, false);
assert.equal(activityLink.classList.values.has("is-current"), false);
assert.equal(dropsLink.classList.values.has("is-current"), true);
assert.equal(activityLink.attributes["aria-current"], undefined);
assert.equal(dropsLink.attributes["aria-current"], "page");
assert.match(window.location.href, /ranking=drops/);
assert.equal(activityMetric.hidden, true);
assert.equal(dropsMetric.hidden, false);

switcher.dispatch("click", { target: playersLink });
assert.equal(activityPanel.hidden, true);
assert.equal(dropsPanel.hidden, true);
assert.equal(playersPanel.hidden, false);
assert.equal(activityMetric.hidden, false);
assert.equal(playersLink.classList.values.has("is-current"), true);
assert.match(window.location.href, /ranking=players/);
playersPageControls[2].dispatch("click");
playersPageControls[3].dispatch("click");
assert.equal(playersPageLabel.textContent, "Page 3 of 3");
assert.equal(playersRows.filter(row => !row.hidden).length, 1);
assert.equal(playersPageControls[2].disabled, true);
assert.equal(playersPageControls[3].disabled, true);
const playersSortControls = playersSortTable.querySelectorAll("[data-public-leaderboard-sort-control]");
playersSortControls[3].dispatch("click");
assert.equal(playersSortTable.querySelectorAll("[data-public-leaderboard-sort-row]").at(-1), playersRows[40]);
playersSortControls[4].dispatch("click");
assert.equal(playersSortTable.querySelectorAll("[data-public-leaderboard-sort-row]").at(-1), playersRows[40]);
playersSortControls[4].dispatch("click");
assert.equal(playersSortTable.querySelectorAll("[data-public-leaderboard-sort-row]").at(-1), playersRows[40]);
playersSortControls[1].dispatch("click");
assert.equal(playersSortControls[1].parentElement.attributes["aria-sort"], "ascending");
assert.equal(playersPageLabel.textContent, "Page 1 of 3");
assert.equal(playersRows.filter(row => !row.hidden).length, 20);
assert.equal(playersRows[40].dataset.rankLabel, "#41");

window.location.href = activityLink.href;
window.listeners.popstate[0]();
assert.equal(activityPanel.hidden, false);
assert.equal(dropsPanel.hidden, true);
assert.equal(activityLink.classList.values.has("is-current"), true);
assert.equal(dropsLink.classList.values.has("is-current"), false);
assert.equal(activityMetric.hidden, false);
assert.equal(dropsMetric.hidden, true);

const boardMarkup = fs.readFileSync(path.join(__dirname, "../../src/Bingo.Web/Pages/Events/Board.cshtml"), "utf8");
const boardModelMarkup = fs.readFileSync(path.join(__dirname, "../../src/Bingo.Web/Pages/Events/Board.cshtml.cs"), "utf8");
const catalogueMarkup = fs.readFileSync(path.join(__dirname, "../../src/Bingo.Web/Pages/Admin/PublicUi.cshtml"), "utf8");
const publicBoardServiceMarkup = fs.readFileSync(path.join(__dirname, "../../src/Bingo.Infrastructure/Boards/PublicBoardService.cs"), "utf8");
const siteCss = ["site.transitional.foundation.css", "site.public-ui.css", "site.transitional.application.css"]
  .map(file => fs.readFileSync(path.join(__dirname, "../../src/Bingo.Web/wwwroot/css", file), "utf8"))
  .join("\n");
const publicUiCss = fs.readFileSync(path.join(__dirname, "../../src/Bingo.Web/wwwroot/css/site.public-ui.css"), "utf8");
const publicLeaderboardsJs = fs.readFileSync(path.join(__dirname, "../../src/Bingo.Web/wwwroot/js/public-leaderboards.js"), "utf8");
const boardStandingsMarkup = boardMarkup.match(/<section class="public-ui-standings[\s\S]*?<\/section>/)?.[0] ?? "";
const catalogueStandingsMarkup = catalogueMarkup.match(/<section class="public-ui-standings[\s\S]*?<\/section>/)?.[0] ?? "";
assert.match(boardMarkup, /Event overview[\s\S]*?data-public-countdown[\s\S]*?public-ui-masthead-result[\s\S]*?Most spooned[\s\S]*?public-dashboard-masthead-actions/, "production masthead keeps the permanent lead before Most spooned and actions");
assert.match(boardMarkup, /<div class="public-dashboard-masthead-section public-ui-masthead-card public-dashboard-title">[\s\S]*?<span class="public-ui-overline">Event overview<\/span>[\s\S]*?<div class="public-ui-component-header">[\s\S]*?<h1 class="public-ui-component-title">[\s\S]*?<\/h1>[\s\S]*?<\/div>[\s\S]*?<p class="public-ui-supporting-text"><span class="event-status-dot">/, "production Event overview uses the shared three-row masthead anatomy");
assert.doesNotMatch(boardMarkup, /public-ui-component-header__copy public-dashboard-title/, "production Event overview has no legacy copy wrapper");
assert.doesNotMatch(siteCss, /\.public-dashboard-title \{ gap: 0\.15rem; \}/, "Event overview has no special spacing compensation");
assert.match(boardMarkup, /@if \(isDrops\)[\s\S]*?public-recent-drops\.js[\s\S]*?\}\s*<script src="~\/js\/public-leaderboards\.js"/, "production loads the masthead selector initializer on every Board view");
assert.match(boardModelMarkup, /MostSpooned = Players[\s\S]*?Where\(value => value\.TotalDrops > 0\)[\s\S]*?OrderByDescending\(value => value\.TotalDrops\)/, "Most spooned is based on confirmed roster approved-drop counts");
assert.match(boardModelMarkup, /HighestDropEhb = Players[\s\S]*?Where\(value => value\.DropEhb > 0\)[\s\S]*?OrderByDescending\(value => value\.DropEhb\)/, "Highest DEHB is based on positive roster player Drop EHB");
assert.match(boardModelMarkup, /HighestEhb = Players[\s\S]*?Where\(value => value\.HasActivity && value\.Participant\.TotalGainedEhb > 0\)[\s\S]*?OrderByDescending\(value => value\.Participant\.TotalGainedEhb\)/, "Highest EHB is based on usable roster player activity");
assert.match(boardMarkup, /Model\.Activity\.HasRankings[\s\S]*?data-public-ui-compact-dropdown[\s\S]*?data-public-ui-dropdown-option="spooned"[\s\S]*?data-public-ui-dropdown-option="drops"[\s\S]*?data-public-ui-dropdown-option="ehb"/, "production renders the three-metric dropdown only with usable rankings");
assert.match(boardMarkup, /if \(Model\.Activity\.HasRankings\)[\s\S]*?else[\s\S]*?<span class="public-ui-overline">@T\["Highest DEHB"\]<\/span>/, "production falls back to static Highest DEHB without rankings");
assert.match(boardMarkup, /data-public-masthead-metric="spooned"[\s\S]*?mostSpooned\.TotalDrops\.ToString\("\+0;-0;0"\)[\s\S]*?data-public-masthead-metric="drops"[\s\S]*?highestDropEhb\.DropEhb\.ToString\("\+0\.00;-0\.00;0\.00"\)[\s\S]*?data-public-masthead-metric="ehb"[\s\S]*?highestEhb\.Participant\.TotalGainedEhb\.ToString\("\+0\.00;-0\.00;0\.00"\)/, "production formats count, DEHB, and EHB masthead metrics distinctly");
assert.match(catalogueMarkup, /data-public-ui-compact-dropdown[\s\S]*?data-public-ui-dropdown-option="spooned"[\s\S]*?data-public-ui-dropdown-option="drops"[\s\S]*?data-public-ui-dropdown-option="ehb"[\s\S]*?data-public-masthead-static-fallback/, "catalogue represents the metric dropdown and no-rankings fallback state");
assert.match(publicLeaderboardsJs, /sessionStorage\?\.getItem\(storageKey\)[\s\S]*?sessionStorage\?\.setItem\(storageKey, metric\)/, "masthead metric selection uses best-effort session storage");
assert.match(publicLeaderboardsJs, /data-public-ui-compact-dropdown[\s\S]*?ArrowDown[\s\S]*?Escape[\s\S]*?chooseCompactDropdownValue/, "compact dropdown supports keyboard navigation and option selection");
assert.match(publicLeaderboardsJs, /enhanceSelectDropdowns[\s\S]*?select\.public-ui-control-visual--select[\s\S]*?data-public-ui-select-enhanced/, "semantic PublicUi selects progressively enhance into the shared compact dropdown");
assert.match(publicLeaderboardsJs, /select\.dispatchEvent\(new Event\("change", \{ bubbles: true \}\)\)/, "compact dropdown choices synchronize back to the semantic select");
assert.match(publicUiCss, /\[data-public-ui-select-dropdown\]\[data-public-ui-select-enhanced\] > select \{ position: absolute;[^}]*clip: rect\(0 0 0 0\);[^}]*clip-path: inset\(50%\);/, "enhanced semantic selects remain focusable through clipped visual hiding");
assert.doesNotMatch(publicUiCss, /\[data-public-ui-select-dropdown\]\[data-public-ui-select-enhanced\] > select \{ display: none; \}/, "enhanced semantic selects are not display-none");
assert.match(publicLeaderboardsJs, /select\.addEventListener\("invalid"[\s\S]*?aria-invalid[\s\S]*?summary\?\.focus/, "invalid semantic select focus is routed to the visible compact summary");
assert.match(publicLeaderboardsJs, /select\.checkValidity\(\)[\s\S]*?summary\?\.removeAttribute\("aria-invalid"\)/, "valid select changes clear the visible invalid state");
assert.match(publicUiCss, /\.public-ui-compact-dropdown > summary \{[^}]*background: rgba\(0, 0, 0, 0\.12\);[^}]*border: 1px solid rgba\(210, 220, 230, 0\.18\);[\s\S]*?\.public-ui-compact-dropdown-menu \{ position: absolute;[^}]*min-width: 100%;[\s\S]*?\.public-ui-compact-dropdown-option\[aria-selected="true"\]/, "compact dropdown shares the input control surface, overlays below the trigger, and marks the selected option");
assert.match(publicUiCss, /\.public-ui-masthead-metric-selector \{[^}]*position: relative;[^}]*width: max-content;[^}]*min-width: 0;[^}]*color:/, "masthead selector owns only the disclosure positioning and intrinsic width");
assert.match(publicUiCss, /\.public-ui-masthead-metric-selector > summary \{[^}]*display: flex;[^}]*padding: 0;[^}]*background: transparent;[^}]*border: 0;[^}]*line-height: normal;[^}]*cursor: pointer;/, "masthead summary is a block-level plain overline disclosure trigger");
assert.doesNotMatch(publicUiCss, /\.public-ui-masthead-metric-selector > summary \{[^}]*[;{]\s*(?:min-height|min-width|block-size|height|margin|transform|translate|top|bottom):/, "masthead summary has no generic control sizing or offset hack");
assert.match(publicUiCss, /\.public-ui-masthead-metric \{ display: contents; \}[\s\S]*\.public-ui-masthead-metric\[hidden\] \{ display: none; \}/, "metric panels flatten into the owning masthead grid while hidden alternatives remain hidden");
assert.match(publicUiCss, /\.public-ui-compact-dropdown-option \{[^}]*font: inherit;[^}]*font-size: 0\.62rem;[^}]*line-height: 1\.3;/, "compact dropdown options reuse the supporting Public UI text scale");
assert.match(publicUiCss, /\.public-ui-compact-dropdown-menu \{[^}]*max-height: 13rem;[^}]*overflow-y: auto;[^}]*overscroll-behavior: contain;[^}]*background: var\(--public-ui-control-overlay-surface\);[^}]*border: 1px solid rgba\(210, 220, 230, 0\.18\);/, "compact dropdown menu uses the named opaque control-overlay surface, bounded scrolling, and shared outline");
assert.match(publicUiCss, /\.public-ui-evidence-drop \{[^}]*border: 1px dashed var\(--public-ui-divider\);/, "evidence upload empty state uses a complete spaced dashed perimeter");
assert.match(publicUiCss, /\.public-ui-compact-dropdown:not\(\[open\]\) > \.public-ui-compact-dropdown-menu \{ display: none; \}/, "compact dropdown explicitly hides its menu while closed");
const boardMastheadDetails = boardMarkup.match(/<details[^>]*data-public-masthead-metric-selector[^>]*>/)?.[0] ?? "";
const catalogueMastheadDetails = catalogueMarkup.match(/<details[^>]*data-public-masthead-metric-selector[^>]*>/)?.[0] ?? "";
assert.match(boardMastheadDetails, /class="public-ui-masthead-metric-selector"[^>]*data-public-ui-compact-dropdown/, "production masthead uses the semantic disclosure owner and data hook");
assert.match(catalogueMastheadDetails, /class="public-ui-masthead-metric-selector"[^>]*data-public-ui-compact-dropdown/, "catalogue masthead uses the semantic disclosure owner and data hook");
assert.doesNotMatch(boardMastheadDetails, /class="[^"]*public-ui-compact-dropdown/, "production masthead does not carry the generic dropdown class");
assert.doesNotMatch(catalogueMastheadDetails, /class="[^"]*public-ui-compact-dropdown/, "catalogue masthead does not carry the generic dropdown class");
assert.doesNotMatch(boardMarkup, /id="public-masthead-metric-panels"|id="public-masthead-metric-panels-specimen"/, "production and catalogue metric panels have no wrapper");
assert.doesNotMatch(catalogueMarkup, /id="public-masthead-metric-panels"|id="public-masthead-metric-panels-specimen"/, "catalogue metric panels have no wrapper");
assert.match(boardMarkup, /public-ui-masthead-metric-selector[\s\S]*public-ui-masthead-metric" data-public-masthead-metric="spooned"[\s\S]*public-ui-component-header[\s\S]*public-ui-supporting-text/, "production ranking section exposes selector, metric header, and supporting text as direct rows");
assert.match(catalogueMarkup, /public-ui-masthead-metric-selector[\s\S]*public-ui-masthead-metric" data-public-masthead-metric="spooned"[\s\S]*public-ui-component-header[\s\S]*public-ui-supporting-text/, "catalogue ranking section exposes selector, metric header, and supporting text as direct rows");
assert.match(boardMarkup, /public-ui-masthead-metric-chevron[^>]*>[\s\S]*?<path d="m6 9 6 6 6-6" \/>/, "production metric selector uses the shared downward chevron geometry");
assert.match(boardMarkup, /var hasDualMastheadActions = showSubmitDrop && Model\.Board\.WiseOldManCompetitionId is not null;[\s\S]*?public-dashboard-masthead-actions @\(hasDualMastheadActions \? "public-dashboard-masthead-actions--dual" : null\)/, "production exposes a minimal dual-action masthead state hook");
assert.doesNotMatch(boardMarkup, /else if \(showDropResult\)/, "production actions no longer replace the permanent lead slot");
assert.match(boardMarkup, /var leadResult = Model\.Board\.EventResult[\s\S]*?EventState\.Live[\s\S]*?new PublicEventResult/, "production masthead provides a live current-leader fallback");
assert.match(boardMarkup, /Model\.Board\.WiseOldManCompetitionId is \{ \} competitionId[\s\S]*?href="https:\/\/wiseoldman\.net\/competitions\/@competitionId" target="_blank" rel="noopener noreferrer" aria-label="@T\["View Wise Old Man competition"\]" title="@T\["View Wise Old Man competition"\]">@T\["WoM"\]<\/a>/, "production actions expose the compact WoM label with full accessible meaning and safe new-tab behavior");
assert.match(boardMarkup, /class="public-ui-action public-ui-action--standard public-ui-action--hyperlink" href="https:\/\/wiseoldman\.net\/competitions\/@competitionId"/, "production competition link uses the neutral standard action treatment");
assert.match(publicBoardServiceMarkup, /EventCompetitionSynchronizations\.AsNoTracking\(\)[\s\S]*?CompetitionId != null[\s\S]*?OrderByDescending\(value => value\.Generation\)/, "competition links use stored configuration without triggering a fetch");
assert.match(catalogueMarkup, /Event overview[\s\S]*?Ends in[\s\S]*?In the lead[\s\S]*?Most spooned[\s\S]*?Actions[\s\S]*?WoM/, "catalogue represents all five masthead slots and dual actions");
assert.match(catalogueMarkup, /class="public-ui-action public-ui-action--standard public-ui-action--hyperlink" href="https:\/\/wiseoldman\.net\/competitions\/145197" target="_blank" rel="noopener noreferrer" aria-label="View Wise Old Man competition" title="View Wise Old Man competition">WoM<\/a>/, "catalogue competition link uses the compact label, accessible meaning, and neutral standard action treatment");
assert.match(catalogueMarkup, /public-dashboard-masthead-actions public-dashboard-masthead-actions--dual/, "catalogue represents the dual-action masthead state");
assert.match(siteCss, /\.public-event-dashboard \.public-dashboard-hero \{[^}]*grid-template-columns: minmax\(0, 1fr\) 1px minmax\(0, 1fr\) 1px minmax\(0, 1fr\) 1px minmax\(0, 1fr\) 1px minmax\(0, 1fr\)/, "production masthead keeps five explicit desktop slots and four dividers on one row");
assert.match(siteCss, /\.public-ui-masthead-row \{ display: grid; width: min\(100%, 80rem\); grid-template-columns: repeat\(5, minmax\(0, 1fr\)\)/, "catalogue masthead uses the shared five-slot composition");
assert.match(siteCss, /@media \(max-width: 900px\) \{[\s\S]*?\.public-event-dashboard \.public-dashboard-hero \{[\s\S]*?grid-template-columns: minmax\(0, 1fr\) 1px minmax\(0, 1fr\)[\s\S]*?grid-template-rows: auto 1px auto 1px auto;[\s\S]*?\.public-event-dashboard \.public-dashboard-hero > :nth-child\(9\) \{ grid-column: 1 \/ -1; grid-row: 5; \}/, "masthead uses coherent paired rows and a spanning actions row at intermediate widths");
assert.match(siteCss, /@media \(max-width: 900px\) \{[\s\S]*?\.public-ui-masthead-row \{ grid-template-columns: repeat\(2, minmax\(0, 1fr\)\);/, "catalogue masthead uses the matching intermediate two-column composition");
assert.match(siteCss, /@media \(max-width: 900px\) \{[\s\S]*?\.public-dashboard-hero > \.public-dashboard-title,\s+\.public-event-dashboard \.public-dashboard-hero > \.public-dashboard-masthead-actions \{ box-sizing: border-box; padding-inline: 0\.75rem; \}/, "wrapped masthead overview and actions reuse the shared section inset");
assert.match(siteCss, /@media \(max-width: 900px\) \{[\s\S]*?\.public-event-dashboard \.public-dashboard-hero > \.public-dashboard-masthead-actions \{ justify-content: flex-end; padding-block: 0\.75rem; \}/, "wrapped masthead actions stay right-aligned in a balanced spanning band");
assert.match(siteCss, /@media \(max-width: 900px\) \{\s+\.public-event-dashboard \.public-dashboard-masthead:has\(> \.public-dashboard-hero > \.public-dashboard-masthead-actions > \.public-ui-action\) \{ margin-bottom: 0\.35rem; \}/, "non-empty wrapped actions remove the extra external masthead margin");
assert.match(siteCss, /@media \(min-width: 901px\) and \(max-width: 1100px\) \{[\s\S]*?\.public-dashboard-hero:has\(> \.public-dashboard-masthead-actions--dual\)[\s\S]*?grid-template-rows: auto 0\.55rem auto;[\s\S]*?\.public-dashboard-masthead-divider:has\(\+ \.public-dashboard-masthead-actions--dual\)[\s\S]*?display: none;[\s\S]*?\.public-dashboard-masthead-actions--dual \{[\s\S]*?grid-row: 3;[\s\S]*?justify-content: flex-end;/, "dual masthead actions use a full-width second row with a modest gap and no separator at the 1100px tier");
assert.match(siteCss, /@media \(min-width: 901px\) and \(max-width: 1100px\) \{\s+\.public-event-dashboard \.public-dashboard-masthead:has\(> \.public-dashboard-hero > \.public-dashboard-masthead-actions--dual\) \{ margin-bottom: 1\.35rem; \}/, "dual wrapped masthead balances its external bottom margin against actual upper spacing");
assert.match(siteCss, /\.public-event-dashboard \.public-dashboard-hero > \.public-dashboard-masthead-actions--dual \{[\s\S]*?padding-block: 0\.75rem;/, "dual wrapped actions use balanced existing masthead vertical spacing");
assert.match(siteCss, /\.public-ui-masthead-card \.public-ui-component-header \{ display: flex; width: auto; min-width: 0; gap: 0\.75rem; align-items: center; justify-content: space-between;/, "masthead highlighted values use the shared full-width space-between alignment");
assert.doesNotMatch(siteCss, /@media \(max-width: 390px\) \{[\s\S]*?\.public-event-dashboard \.public-dashboard-hero > :nth-child\(1\),[\s\S]*?width: fit-content;[\s\S]*?margin-inline: auto;[\s\S]*?justify-self: center;/, "small-phone information blocks retain the shared left-aligned available-width layout");
assert.doesNotMatch(siteCss, /@media \(max-width: 390px\) \{[\s\S]*?\.public-event-dashboard \.public-dashboard-hero > :nth-child\(3\) \{ width: min\(100%, 24rem\);[\s\S]*?justify-self: center;/, "small-phone countdown retains the shared full-width left-inset layout");
assert.doesNotMatch(siteCss, /@media \(max-width: 767px\) \{\s+\.public-event-dashboard \.public-dashboard-hero \{ grid-template-columns: 1fr;/, "masthead keeps the paired two-column composition on phones");
assert.match(siteCss, /@media \(max-width: 390px\) \{\s+\.public-event-dashboard \.public-dashboard-hero \{ grid-template-columns: 1fr; grid-template-rows: none;[\s\S]*?\.public-ui-masthead-row \{ grid-template-columns: 1fr; \}/, "masthead uses the exact small-phone one-column fallback");
assert.doesNotMatch(siteCss, /@media \(max-width: 540px\) \{\s+\.public-event-dashboard \.public-dashboard-hero \{ grid-template-columns: 1fr;/, "masthead remains paired through 391px");
assert.doesNotMatch(siteCss, /@media \(max-width: 1199px\) \{\s+\.public-event-dashboard \.public-dashboard-masthead/, "masthead does not collapse at ordinary laptop widths");
assert.doesNotMatch(siteCss, /\.public-event-dashboard \.public-dashboard-title h1 \{ font-size:/, "event title uses the shared component-title role without an oversized override");
assert.match(siteCss, /\.public-event-dashboard \.public-dashboard-hero > \.public-dashboard-masthead-actions \{ justify-content: flex-end; \}/, "masthead actions occupy the far-right slot");
assert.match(boardMarkup, /<tr class="public-ui-leaderboard-detail-row">\s*<td colspan="7">\s*<details/, "production leaderboards use full-width detail rows");
assert.equal((boardMarkup.match(/public-ui-table--nested(?:\s|")/g) ?? []).length, 2, "production has one nested detail table per leaderboard variant");
assert.match(boardMarkup, /data-public-leaderboard-view="activity"[\s\S]*?data-public-leaderboard-view="drops"[\s\S]*?data-public-leaderboard-view="players"/, "production exposes the route-backed three-view selector");
assert.match(boardMarkup, /<div class="public-ui-surface-content">\s*<nav[^>]*data-public-leaderboard-switcher[\s\S]*?data-public-leaderboard-panel="activity"[\s\S]*?data-public-leaderboard-panel="drops"[\s\S]*?data-public-leaderboard-panel="players"[\s\S]*?<\/div>\s*<\/section>/, "production keeps all three leaderboard panels inside the owning surface content");
assert.match(boardMarkup, /data-public-leaderboard-players-table[\s\S]*?@T\["Rank"\][\s\S]*?@T\["Player"\][\s\S]*?@T\["Team"\][\s\S]*?@T\["EHB gained"\][\s\S]*?@T\["Drop EHB"\][\s\S]*?@T\["Total drops"\][\s\S]*?@T\["WoM"\]/, "production Players view uses the approved owner table columns");
assert.match(boardMarkup, /data-sort-player="@primaryAccountName" data-sort-team="@player\.TeamName" data-sort-ehb-gained="@\(player\.HasActivity \? participant\.TotalGainedEhb/, "production Players rows expose owner/team and conditional EHB sort values");
assert.match(boardMarkup, /data-sort-ehb-gained="@\(player\.HasActivity \? participant\.TotalGainedEhb\.ToString\(System\.Globalization\.CultureInfo\.InvariantCulture\) : null\)" data-sort-drop-ehb="@\(player\.TotalDrops > 0 \? player\.DropEhb\.ToString\(System\.Globalization\.CultureInfo\.InvariantCulture\) : null\)" data-sort-total-drops="@player\.TotalDrops"/, "production Players rows expose conditional EHB and independent approved-drop sort values");
assert.match(boardMarkup, /player\.HasActivity \? player\.Rank\.ToString\(System\.Globalization\.CultureInfo\.InvariantCulture\) : null[\s\S]*?player\.HasActivity \? participant\.TotalGainedEhb\.ToString\(System\.Globalization\.CultureInfo\.InvariantCulture\) : null/, "production Players leave Rank and EHB unavailable before activity");
assert.match(boardMarkup, /player\.TotalDrops > 0 \? player\.DropEhb\.ToString\("\+0\.00;-0\.00;0\.00"\) : "—"/, "production Players show an em dash for zero-drop Drop EHB");
assert.match(boardMarkup, /class="public-ui-section-heading @\(player\.HasActivity \? "public-ui-positive-delta--success" : null\)">@gainedEhb/, "production unavailable EHB values omit the success color");
assert.match(boardMarkup, /class="public-ui-section-heading @\(player\.TotalDrops > 0 \? "public-ui-positive-delta--success" : null\)">@\(player\.TotalDrops > 0/, "production unavailable Drop EHB values omit the success color");
assert.match(boardMarkup, /public-ui-table--nested-ehb[\s\S]*?@T\["Rank"\][\s\S]*?@T\["Player"\][\s\S]*?@T\["EHB gained"\][\s\S]*?@T\["Start EHB"\][\s\S]*?@T\["End EHB"\][\s\S]*?@T\["WoM"\]/, "production EHB details use the ranked six-column variant and approved column order");
assert.match(boardMarkup, /public-ui-table--nested-drop-ehb[\s\S]*?@T\["Rank"\][\s\S]*?@T\["Player"\][\s\S]*?@T\["Drop EHB"\][\s\S]*?@T\["Total drops"\][\s\S]*?@T\["Team share"\][\s\S]*?@T\["Drops"\]/, "production Drop EHB details use the approved six-column order");
assert.match(boardMarkup, /var teamShareValue = team\.TotalDrops == 0 \? 0m : player\.ApprovedSubmissions \/ \(decimal\)team\.TotalDrops \* 100m;[\s\S]*?var teamShare = team\.TotalDrops == 0 \? "—" : \$"\{teamShareValue:0\.0\}%";[\s\S]*?<td><strong class="public-ui-section-heading">@teamShare<\/strong>/, "production Drop EHB details derive neutral team share from approved drop counts with an all-zero em dash");
assert.match(boardMarkup, /data-sort-rank="@\(player\.ApprovedSubmissions > 0 \? \(playerIndex \+ 1\)\.ToString\(System\.Globalization\.CultureInfo\.InvariantCulture\) : null\)"[\s\S]*?data-sort-drop-ehb="@\(player\.ApprovedSubmissions > 0 \? player\.DropEhb\.ToString\(System\.Globalization\.CultureInfo\.InvariantCulture\) : null\)"[\s\S]*?player\.ApprovedSubmissions > 0 \? player\.DropEhb\.ToString\("\+0\.00;-0\.00;0\.00"\) : "—"/, "production Drop EHB rows keep unavailable rank and Drop EHB values out of numeric sorting");
assert.equal((boardMarkup.match(/data-public-leaderboard-sort-table/g) ?? []).length, 3, "production marks both nested variants and Players as sortable tables");
assert.equal((catalogueMarkup.match(/data-public-leaderboard-sort-table/g) ?? []).length, 5, "catalogue marks both nested variants and Players as sortable tables");
assert.equal((boardMarkup.match(/data-public-leaderboard-sort-control/g) ?? []).length, 16, "production exposes sortable nested and Players headers");
assert.match(boardMarkup, /data-sort-ehb-gained="@aggregateGainedEhb\.ToString\(System\.Globalization\.CultureInfo\.InvariantCulture\)"[\s\S]*?data-sort-start-ehb="@aggregateStartEhb\.ToString\(System\.Globalization\.CultureInfo\.InvariantCulture\)"[\s\S]*?data-sort-end-ehb="@aggregateEndEhb\.ToString\(System\.Globalization\.CultureInfo\.InvariantCulture\)"/, "production EHB rows sort multi-account numeric aggregates");
assert.match(boardMarkup, /data-sort-drop-ehb="@\(player\.ApprovedSubmissions > 0 \? player\.DropEhb\.ToString\(System\.Globalization\.CultureInfo\.InvariantCulture\) : null\)"[\s\S]*?data-sort-total-drops="@player\.ApprovedSubmissions"[\s\S]*?data-sort-team-share="@teamShareValue\.ToString\(System\.Globalization\.CultureInfo\.InvariantCulture\)"/, "production Drop EHB rows expose aggregate sort values and unavailable sentinels");
assert.doesNotMatch(boardMarkup, /data-public-leaderboard-sort-key="(?:wom|drops)"/, "production action columns remain non-sortable");
assert.doesNotMatch(boardMarkup, /<td>\s*<details id="public-leaderboard-/, "team cells no longer contain expanded details");
assert.match(boardMarkup, /wiseoldman\.net\/players\/\{Uri\.EscapeDataString\(accountName\)\}\/gained\?metric=ehb&startDate=\{Uri\.EscapeDataString\(eventStart\)\}&endDate=\{Uri\.EscapeDataString\(eventEnd\)\}/, "EHB details link to the event-window gained view");
assert.match(boardMarkup, /for \(var accountIndex = 0; accountIndex < playingAccountNames\.Count; accountIndex\+\+\)[\s\S]*?target="_blank" rel="noopener noreferrer">@accountName/, "EHB details expose a separate new-tab link for each playing account");
assert.match(boardMarkup, /<small class="public-ui-supporting-text @\(accountIndex > 0 \? "public-ui-leaderboard-detail-secondary" : null\)"\s*><a class="public-ui-action public-ui-action--text public-ui-action--hyperlink" href="@womUrl"/, "EHB WoM links use compact stacked secondary account lines");
const boardEhbWomCell = boardMarkup.match(/<td>\s*@for \(var accountIndex = 0; accountIndex < playingAccountNames\.Count; accountIndex\+\+\)[\s\S]*?<\/td>/)?.[0] ?? "";
assert.notEqual(boardEhbWomCell, "", "production EHB WoM cell is present");
assert.match(boardEhbWomCell, /public-ui-leaderboard-detail-secondary/, "production EHB WoM cell marks secondary account links with the shared detail role");
assert.doesNotMatch(boardEhbWomCell, /if \(accountIndex > 0\)[\s\S]*?<span aria-hidden="true"> · <\/span>/, "production EHB WoM links no longer use an inline separator");
assert.match(boardMarkup, /asp-route-dropSearch="@dropSearch"/, "Drop EHB details preserve the real search fallback");
assert.equal((boardMarkup.match(/<div class="public-ui-section">\s*<header class="public-ui-component-header">[\s\S]*?<table class="public-ui-table(?: [^"]+)?" data-public-leaderboard-table/g) ?? []).length, 3, "production metadata and tables share the Public UI section gap");
assert.match(boardMarkup, /<small class="public-ui-supporting-text">@T\["Calculated from approved drops"\]<\/small>/, "production uses concise Drop EHB metadata");
assert.match(boardMarkup, /id="public-leaderboard-players-heading" class="public-ui-supporting-text"><span>@T\["Wise Old Man"\]<\/span> <span aria-hidden="true">·<\/span> <span>@T\["Player activity across all teams"\]<\/span>/, "production Players uses compact sibling metadata");
assert.match(boardMarkup, /data-public-leaderboard-players-table[^>]*class="public-ui-table public-ui-table--players"|class="public-ui-table public-ui-table--players"[^>]*data-public-leaderboard-players-table/, "production Players uses the scoped action-column alignment class");
assert.equal((catalogueMarkup.match(/public-ui-table--nested(?:\s|")/g) ?? []).length, 4, "catalogue mirrors nested detail tables for both variants");
assert.equal((catalogueMarkup.match(/public-ui-table--nested-ehb/g) ?? []).length, 2, "catalogue mirrors the EHB nested variant");
assert.equal((catalogueMarkup.match(/public-ui-table--nested-drop-ehb/g) ?? []).length, 2, "catalogue mirrors the Drop EHB nested variant");
assert.match(catalogueMarkup, /data-public-leaderboard-sort-key="rank"[\s\S]*?data-public-leaderboard-sort-key="player"[\s\S]*?data-public-leaderboard-sort-key="ehb-gained"[\s\S]*?data-public-leaderboard-sort-key="start-ehb"[\s\S]*?data-public-leaderboard-sort-key="end-ehb"[\s\S]*?<th[^>]*>WoM<\/th>/, "catalogue EHB details include the approved ranked sortable column order");
assert.match(catalogueMarkup, /data-public-leaderboard-sort-key="rank"[\s\S]*?data-public-leaderboard-sort-key="player"[\s\S]*?data-public-leaderboard-sort-key="drop-ehb"[\s\S]*?data-public-leaderboard-sort-key="total-drops"[\s\S]*?data-public-leaderboard-sort-key="team-share"[\s\S]*?<th[^>]*>Drops<\/th>/, "catalogue Drop EHB details include the approved ranked sortable column order");
assert.match(catalogueMarkup, /<small class="public-ui-supporting-text"><a class="public-ui-action public-ui-action--text public-ui-action--hyperlink"[\s\S]*?>RuneWarden<\/a><\/small><small class="public-ui-supporting-text public-ui-leaderboard-detail-secondary"><a class="public-ui-action public-ui-action--text public-ui-action--hyperlink"[\s\S]*?>SpoonDealer<\/a><\/small>/, "catalogue demonstrates compact stacked independently clickable EHB links");
assert.doesNotMatch(catalogueMarkup, /RuneWarden<\/a> · <a class="public-ui-action public-ui-action--text public-ui-action--hyperlink"[\s\S]*?>SpoonDealer<\/a>/, "catalogue EHB WoM links omit the inline separator");
assert.match(catalogueMarkup, /public-ui-leaderboard-detail-row/, "catalogue demonstrates full-width detail rows");
assert.equal((catalogueMarkup.match(/<div class="public-ui-section">\s*<header class="public-ui-component-header">[\s\S]*?<table class="public-ui-table(?: [^"]+)?" data-public-leaderboard-table/g) ?? []).length, 3, "catalogue metadata and tables share the Public UI section gap");
assert.match(catalogueMarkup, /data-public-leaderboard-view="activity"[\s\S]*?data-public-leaderboard-view="drops"[\s\S]*?data-public-leaderboard-view="players"/, "catalogue mirrors the three-view selector");
assert.match(catalogueMarkup, /<div class="public-ui-surface-content">\s*<nav[^>]*data-public-leaderboard-switcher[\s\S]*?data-public-leaderboard-panel="activity"[\s\S]*?data-public-leaderboard-panel="drops"[\s\S]*?data-public-leaderboard-panel="players"[\s\S]*?<\/div>\s*<\/section>/, "catalogue keeps all three leaderboard panels inside the owning surface content");
assert.match(catalogueMarkup, /data-public-leaderboard-players-table[\s\S]*?Rank[\s\S]*?Player[\s\S]*?Team[\s\S]*?EHB gained[\s\S]*?Drop EHB[\s\S]*?Total drops[\s\S]*?WoM/, "catalogue mirrors the Players table columns");
assert.match(catalogueMarkup, /data-public-leaderboard-pagination[\s\S]*?data-public-leaderboard-page-control="first"[\s\S]*?data-public-leaderboard-page-control="last"/, "catalogue mirrors the shared Players pagination controls");
assert.match(catalogueMarkup, /id="public-ui-players-table-heading" class="public-ui-supporting-text"><span>Wise Old Man<\/span> <span aria-hidden="true">·<\/span> <span>Player activity across all teams<\/span>/, "catalogue Players uses compact sibling metadata");
assert.match(catalogueMarkup, /data-public-leaderboard-players-table[^>]*class="public-ui-table public-ui-table--players"|class="public-ui-table public-ui-table--players"[^>]*data-public-leaderboard-players-table/, "catalogue Players uses the scoped action-column alignment class");
assert.match(catalogueMarkup, /var hasActivity = playerIndex < 21;[\s\S]*?data-sort-rank="@\(hasActivity \? playerIndex\.ToString\(\) : null\)"[\s\S]*?data-sort-drop-ehb="@\(totalDrops > 0 \? dropEhb\.ToString\(System\.Globalization\.CultureInfo\.InvariantCulture\) : null\)"[\s\S]*?totalDrops > 0 \? \$"\+\{dropEhb:0\.00\}" : "—"/, "catalogue Players demonstrates roster-first unavailable activity with independent drops");
assert.match(catalogueMarkup, /data-sort-player="IronMoth" data-sort-drop-ehb="" data-sort-total-drops="0" data-sort-team-share="0"[\s\S]*?<td><strong class="public-ui-section-heading">—<\/strong><\/td>[\s\S]*?<td><strong class="public-ui-section-heading">0<\/strong><\/td>[\s\S]*?dropSearch=IronMoth/, "catalogue Drop EHB demonstrates an all-zero roster row with usable Drops search");
assert.match(catalogueMarkup, /class="public-ui-section-heading @\(hasActivity \? "public-ui-positive-delta--success" : null\)">@\(hasActivity \?/, "catalogue unavailable EHB values omit the success color");
assert.match(catalogueMarkup, /class="public-ui-section-heading @\(totalDrops > 0 \? "public-ui-positive-delta--success" : null\)">@\(totalDrops > 0/, "catalogue unavailable Drop EHB values omit the success color");
assert.match(boardMarkup, /data-public-leaderboard-page-control="first"[\s\S]*?<path d="M4 6v12" \/><path d="m15 6-6 6 6 6" \/>[\s\S]*?data-public-leaderboard-page-control="previous"[\s\S]*?<path d="m15 6-6 6 6 6" \/>[\s\S]*?data-public-leaderboard-page-control="next"[\s\S]*?<path d="m9 6 6 6-6 6" \/>[\s\S]*?data-public-leaderboard-page-control="last"[\s\S]*?<path d="m9 6 6 6-6 6" \/><path d="M20 6v12" \/>/, "production Players pager keeps horizontal first/last bar geometry");
assert.match(catalogueMarkup, /data-public-leaderboard-page-control="first"[\s\S]*?<path d="M4 6v12" \/><path d="m15 6-6 6 6 6" \/>[\s\S]*?data-public-leaderboard-page-control="last"[\s\S]*?<path d="m9 6 6 6-6 6" \/><path d="M20 6v12" \/>/, "catalogue Players pager keeps horizontal first/last bar geometry");
assert.match(catalogueMarkup, /<small class="public-ui-supporting-text">Calculated from approved drops<\/small>/, "catalogue uses concise Drop EHB metadata");
assert.match(siteCss, /\.public-ui-action--standard:hover:not\(:disabled\):not\(\[aria-disabled="true"\]\)[\s\S]*?\.public-ui-action--text\.public-ui-action--hyperlink:hover:not\(:disabled\):not\(\[aria-disabled="true"\]\)/, "standard hyperlink actions retain standard hover while text hyperlinks keep ghost hover");
assert.doesNotMatch(boardStandingsMarkup, /public-ui-data-line/, "live standings rows omit decorative data lines");
assert.doesNotMatch(catalogueStandingsMarkup, /public-ui-data-line/, "catalogue standings rows omit decorative data lines");
assert.match(boardStandingsMarkup, /<strong class="public-ui-section-heading">@team\.TeamName<\/strong>/, "live standings use the smaller team-name role");
assert.match(boardStandingsMarkup, /data-public-leaderboard-standings-metric="activity"[\s\S]*?activityTeam!?\.TotalGainedEhb\.ToString\("\+0\.00;-0\.00;0\.00"\)/, "live standings expose event EHB gain metrics");
assert.match(boardStandingsMarkup, /hasActivityMetric \? [\s\S]*?"— EHB"/, "live standings use an unavailable EHB fallback");
assert.match(boardStandingsMarkup, /data-public-leaderboard-standings-metric="drops"[\s\S]*?dropEhbTeam\.DropEhb\.ToString\("\+0\.00;-0\.00;0\.00"\)[\s\S]*?DEHB/, "live standings expose Drop EHB metrics");
assert.match(catalogueMarkup, /data-public-leaderboard-switcher[\s\S]*?data-public-leaderboard-view="activity"[\s\S]*?data-public-leaderboard-view="drops"/, "catalogue standings reuse the leaderboard switcher");
assert.match(catalogueStandingsMarkup, /data-public-leaderboard-standings-metric="activity" class="public-ui-section-heading public-ui-positive-delta--success">\+2487\.00 EHB/, "catalogue EHB metric uses the green small role");
assert.match(catalogueStandingsMarkup, /data-public-leaderboard-standings-metric="drops" class="public-ui-section-heading public-ui-positive-delta--success" hidden>\+842\.70 DEHB/, "catalogue Drop EHB metric uses the green small role");
assert.match(boardMarkup, /<div class="public-ui-recent-drops-sidebar public-ui-recent-drops-sidebar--flush">\s*<section class="public-ui-standings/, "live standings reuse the sticky sidebar wrapper");
assert.match(catalogueMarkup, /public-ui-leaderboard-overview-layout">\s*<div class="public-ui-section">\s*<section[\s\S]*public-ui-recent-drops-sidebar public-ui-recent-drops-sidebar--flush[\s\S]*public-ui-standings/, "catalogue represents the direct main section and standings sidebar composition");
assert.match(boardStandingsMarkup, /<span class="public-ui-overline">@T\["Team progress"\]<\/span>\s*<strong id="standings-heading" class="public-ui-component-title">@T\["Bingo standings"\]<\/strong>/, "live standings use the two-line Team progress header");
assert.doesNotMatch(boardStandingsMarkup, /Always live|<small class="public-ui-supporting-text">@T\["Team progress"\]<\/small>/, "live standings omit redundant header lines");
assert.match(catalogueStandingsMarkup, /<span class="public-ui-overline">Team progress<\/span>\s*<strong id="public-standings-specimen-heading" class="public-ui-component-title">Bingo standings<\/strong>/, "catalogue standings use the two-line Team progress header");
assert.doesNotMatch(catalogueStandingsMarkup, /A compact team summary alongside public drop activity\./, "catalogue standings omit the extra supporting line");
assert.doesNotMatch(boardStandingsMarkup, /public-ui-positive-delta(?!-[-]?success)/, "live standings contain no blue metric values");
assert.doesNotMatch(catalogueStandingsMarkup, /public-ui-positive-delta(?!-[-]?success)/, "catalogue standings contain no blue metric values");
assert.match(siteCss, /\.public-ui-surface-content \{[^}]*min-width: 0;/, "surface content can shrink to the viewport");
assert.match(siteCss, /\.public-ui-surface-content > \* \{ min-width: 0; \}/, "surface grid children cannot widen the scroll owner");
assert.match(siteCss, /\.public-ui-standings li > \[data-public-leaderboard-standings-metric\] \{ align-self: center; \}/, "both standings metrics share row-center alignment");
assert.match(siteCss, /\.public-ui-standings li > \[data-public-leaderboard-standings-metric\] \{ grid-column: 2; \}/, "standings metrics retain responsive stacking");
assert.match(siteCss, /\.public-ui-view-switcher \{ display: grid; box-sizing: border-box; width: 100%; min-width: 0;[^}]*grid-template-columns: repeat\(3, minmax\(0, 1fr\)\);/, "shared switcher is box-sized and shrinkable");
assert.match(siteCss, /\.public-ui-view-switcher \{[^}]*padding: 0\.2rem;/, "shared switcher uses equal outer side and vertical gutters");
assert.match(siteCss, /\.public-ui-view-switcher--two \{ grid-template-columns: repeat\(2, minmax\(0, 1fr\)\); \}/, "two-item switcher keeps equal flexible columns");
assert.match(siteCss, /\.public-ui-view-switcher-item \{ display: flex; min-width: 0;/, "switcher items can shrink evenly");
assert.match(siteCss, /\.public-ui-recent-drops-sidebar \{ display: grid; position: sticky; top: 1rem;/, "standings reuse the recent-drops sticky policy");
assert.match(siteCss, /\.public-ui-recent-drops-sidebar--flush \{ margin-top: 0; \}/, "standings sidebar flushes to the leaderboard surface");
assert.match(siteCss, /\.public-ui-recent-drops-layout > \.public-ui-section > \.public-ui-surface, \.public-ui-recent-drops-layout > \.public-ui-recent-drops-sidebar > \.public-ui-surface \{ width: auto; min-width: 0; margin: 0; \}/, "sidebar surface sizing is shared");
assert.match(siteCss, /\.public-ui-recent-drops-sidebar \{ display: contents; position: static; margin-top: 0; \}/, "sidebar returns to document flow on narrow screens");
assert.match(siteCss, /\.public-ui-recent-drops-sidebar > \.public-ui-surface \{ position: static; grid-column: 1; grid-row: auto; order: 1; margin-top: 0; \}/, "sidebar surfaces move above main content on narrow screens");
assert.match(siteCss, /@media \(max-width: 900px\) \{[\s\S]*\.public-ui-recent-drops-layout > \.public-ui-section \{ grid-column: 1; grid-row: auto; order: 3; \}/, "direct leaderboard main sections follow the sidebar on narrow screens");
assert.match(siteCss, /@media \(max-width: 900px\) \{[\s\S]*\.public-ui-standings ol \{ grid-template-columns: repeat\(2, minmax\(0, 1fr\)\); column-gap: 0; \}/, "standings use continuous paired-row dividers in narrow paired rows");
assert.doesNotMatch(siteCss, /\.public-ui-standings li:nth-child\(odd\):not\(:last-child\)::after/, "standings have no vertical separators between paired cells");
assert.match(siteCss, /@media \(max-width: 760px\) \{ \/\* Public UI catalogue table narrow overrides \*\/[\s\S]*\.public-ui-standings li > \[data-public-leaderboard-standings-metric\] \{ grid-column: 2; \}[\s\S]*\n\}\s*@media \(max-width: 390px\) \{[\s\S]*\.public-ui-standings ol \{ grid-template-columns: 1fr; \}[\s\S]*\.public-ui-standings li \{ width: 100%; box-sizing: border-box; grid-template-columns: 1\.6rem minmax\(0, 1fr\) minmax\(6\.5rem, max-content\); \}[\s\S]*\.public-ui-standings li:nth-child\(odd\):not\(:last-child\) \{ padding-right: 0; \}[\s\S]*\.public-ui-standings li > \[data-public-leaderboard-standings-metric\] \{ grid-column: 3; align-self: center; justify-self: stretch; text-align: right; \}/, "standings use a full-width right track with uniform small-phone row insets");
assert.match(siteCss, /@media \(min-width: 391px\) and \(max-width: 900px\) \{[\s\S]*\.public-ui-standings li:nth-child\(odd\):nth-last-child\(2\) \{ border-bottom: 0; \}/, "standings remove the dangling divider beneath an even final pair");
assert.match(siteCss, /@media \(max-width: 900px\) \{[\s\S]*?\.public-ui-table\.public-ui-table--nested \{ width: max-content; min-width: 72rem; max-width: none; table-layout: auto; \}/, "nested leaderboard tables size intrinsically inside the narrow section scroll owner");
assert.match(siteCss, /@media \(max-width: 900px\) \{[\s\S]*\.public-ui-table--players th:nth-child\(3\), \.public-ui-table--players td:nth-child\(3\) \{ white-space: normal; overflow-wrap: anywhere; \}/, "Players team names wrap safely in the narrow table");
assert.match(siteCss, /\.public-ui-table\.public-ui-table--nested \{ width: min\(100%, 72rem\); max-width: 72rem; min-width: 0; margin: 0; table-layout: fixed;/, "nested tables share a capped responsive fixed layout without a top offset");
assert.doesNotMatch(siteCss, /\.public-ui-table tbody tr:last-child td \{ border-bottom: 0; \}/, "nested tables are not subject to the broad last-child divider reset");
assert.match(siteCss, /\.public-ui-table:not\(\.public-ui-table--nested\) > tbody > tr:last-child > td \{ border-bottom: 0; \}/, "outer table boundary reset excludes nested player rows");
assert.match(siteCss, /\.public-ui-table--nested > tbody > tr:not\(:last-child\) > td \{ border-bottom: 1px solid color-mix\(in srgb, var\(--public-ui-divider\) 70%, transparent\); \}/, "nested tables retain softer dividers between player rows for both variants");
assert.match(siteCss, /\.public-ui-table--nested > tbody > tr:last-child > td \{ border-bottom: 1px solid color-mix\(in srgb, var\(--public-ui-divider\) 70%, transparent\); \}/, "nested tables restore the closing divider beneath the final player row");
assert.match(siteCss, /\.public-ui-table > tbody > tr:has\(\+ \.public-ui-leaderboard-detail-row details\[open\]\) > td \{ background: color-mix\(in srgb, var\(--public-ui-charcoal-surface\) 82%, var\(--public-ui-flat-surface\)\); border-bottom: 0; \}/, "expanded team rows receive a selected surface tone");
assert.match(siteCss, /\.public-ui-table > tbody > \.public-ui-leaderboard-detail-row:has\(details\[open\]\) > td > details \{ margin-inline: 0\.6rem; background: color-mix\(in srgb, var\(--public-ui-charcoal-surface\) 72%, var\(--public-ui-page-canvas\)\); border-left: 2px solid var\(--public-ui-data-blue\); \}/, "expanded detail regions stay within parent bounds with an inset tone and shared accent rail");
assert.match(siteCss, /\.public-ui-table--nested-ehb th:nth-child\(1\), \.public-ui-table--nested-ehb td:nth-child\(1\) \{ width: 8%;/, "EHB nested columns keep rank compact");
assert.match(siteCss, /\.public-ui-table--nested-ehb th:nth-child\(2\), \.public-ui-table--nested-ehb td:nth-child\(2\) \{ width: 21%;/, "EHB nested columns distribute reclaimed width to Player");
assert.match(siteCss, /\.public-ui-table--nested-ehb th:nth-child\(3\), \.public-ui-table--nested-ehb td:nth-child\(3\) \{ width: 17%;/, "EHB nested columns distribute reclaimed width to gained values");
assert.match(siteCss, /\.public-ui-table--nested-ehb th:nth-child\(4\), \.public-ui-table--nested-ehb td:nth-child\(4\) \{ width: 15%;/, "EHB nested columns distribute reclaimed width to Start EHB");
assert.match(siteCss, /\.public-ui-table--nested-ehb th:nth-child\(5\), \.public-ui-table--nested-ehb td:nth-child\(5\) \{ width: 15%;/, "EHB nested columns distribute reclaimed width to End EHB");
assert.match(siteCss, /\.public-ui-table--nested-ehb th:nth-child\(6\), \.public-ui-table--nested-ehb td:nth-child\(6\) \{ width: 24%;/, "EHB nested columns keep WoM narrower");
assert.match(siteCss, /\.public-ui-table--nested-drop-ehb th:nth-child\(1\), \.public-ui-table--nested-drop-ehb td:nth-child\(1\) \{ width: 8%;/, "Drop EHB nested columns keep rank compact");
assert.match(siteCss, /\.public-ui-table--nested-drop-ehb th:nth-child\(2\), \.public-ui-table--nested-drop-ehb td:nth-child\(2\) \{ width: 24%;/, "Drop EHB nested columns give Player a balanced share");
assert.match(siteCss, /\.public-ui-table--nested-drop-ehb th:nth-child\(3\), \.public-ui-table--nested-drop-ehb td:nth-child\(3\) \{ width: 17%;/, "Drop EHB nested columns size Drop EHB evenly");
assert.match(siteCss, /\.public-ui-table--nested-drop-ehb th:nth-child\(4\), \.public-ui-table--nested-drop-ehb td:nth-child\(4\) \{ width: 16%;/, "Drop EHB nested columns size Total drops evenly");
assert.match(siteCss, /\.public-ui-table--nested-drop-ehb th:nth-child\(5\), \.public-ui-table--nested-drop-ehb td:nth-child\(5\) \{ width: 17%;/, "Drop EHB nested columns size Team share evenly");
assert.match(siteCss, /\.public-ui-table--nested-drop-ehb th:nth-child\(6\), \.public-ui-table--nested-drop-ehb td:nth-child\(6\) \{ width: 18%;/, "Drop EHB nested columns preserve a compact Drops action column");
assert.match(siteCss, /\.public-ui-table \.public-ui-table-sort-control \{ display: inline-flex; width: auto; min-height: 1\.5rem; gap: 0\.3rem; align-items: center; justify-content: flex-start; padding: 0;/, "nested sortable headers keep label/icon inline at the shared cell start");
assert.match(siteCss, /\.public-ui-table \.public-ui-table-sort-control:focus-visible \{ outline: 2px solid var\(--public-ui-data-blue\);/, "nested sortable headers reuse the shared Public UI focus treatment");
assert.match(siteCss, /\.public-ui-table \.public-ui-table-sort-control\.is-sorted \{ color: var\(--public-ui-data-blue\); \}/, "active nested sort headers use the Public UI accent");
assert.match(siteCss, /\.public-ui-table \.public-ui-table-sort-icon \{ width: 0\.75rem; height: 0\.75rem; flex: 0 0 auto; transform: rotate\(-90deg\);/, "inactive nested sort headers show a right-pointing chevron");
assert.match(siteCss, /\.public-ui-table \.public-ui-table-sort-control\.is-descending \.public-ui-table-sort-icon \{ transform: rotate\(0deg\); \}/, "descending nested sorts point the chevron down");
assert.match(siteCss, /\.public-ui-table \.public-ui-table-sort-control\.is-ascending \.public-ui-table-sort-icon \{ transform: rotate\(180deg\); \}/, "ascending nested sorts rotate the shared chevron");
assert.match(siteCss, /\.public-ui-table--nested th:last-child \{ padding-inline-start: calc\(0\.5rem \+ 0\.15rem \+ 1px\); vertical-align: middle; \}/, "nested action headers align vertically and horizontally with visible hyperlink text");
assert.match(siteCss, /\.public-ui-table--players th:last-child \{ padding-inline-start: calc\(0\.5rem \+ 0\.15rem \+ 1px\); vertical-align: middle; \}/, "Players action headers reuse the approved alignment");
assert.match(siteCss, /\.public-ui-table--players tbody td \{ padding: 0\.42rem 0\.5rem; \}/, "Players body cells reuse compact nested-table density without changing headers");
assert.match(siteCss, /\.public-ui-table--players thead th \{ padding-block: 0\.2rem; \}/, "Players sortable headers compensate their control height to match main-table headers");
assert.match(siteCss, /\.public-ui-table\.public-ui-table--players \{ table-layout: fixed; \}/, "Players table uses a stable fixed layout");
assert.match(siteCss, /\.public-ui-table--players th:nth-child\(1\), \.public-ui-table--players td:nth-child\(1\) \{ width: 5%; \}[\s\S]*?\.public-ui-table--players th:nth-child\(2\), \.public-ui-table--players td:nth-child\(2\) \{ width: 15%; \}[\s\S]*?\.public-ui-table--players th:nth-child\(3\), \.public-ui-table--players td:nth-child\(3\) \{ width: 18%; \}[\s\S]*?\.public-ui-table--players th:nth-child\(4\), \.public-ui-table--players td:nth-child\(4\) \{ width: 14%; \}[\s\S]*?\.public-ui-table--players th:nth-child\(5\), \.public-ui-table--players td:nth-child\(5\) \{ width: 12%; \}[\s\S]*?\.public-ui-table--players th:nth-child\(6\), \.public-ui-table--players td:nth-child\(6\) \{ width: 10%; \}[\s\S]*?\.public-ui-table--players th:nth-child\(7\), \.public-ui-table--players td:nth-child\(7\) \{ width: 26%; overflow: hidden; text-overflow: ellipsis; \}/, "Players columns keep the approved 100% width allocation");
assert.match(siteCss, /@media \(max-width: 760px\) \{[\s\S]*?\.public-ui-table--players \.public-ui-table-sort-control \{ gap: 0\.2rem; \}[\s\S]*?\.public-ui-table--players \.public-ui-table-sort-icon \{ width: 0\.68rem; height: 0\.68rem; \}/, "Players sort controls compact only at the narrow breakpoint");
assert.match(publicLeaderboardsJs, /const leftUnavailable = numeric && leftValue === "";[\s\S]*?if \(leftUnavailable !== rightUnavailable\) return leftUnavailable \? 1 : -1;/, "numeric Players sorting keeps unavailable values after available values");
assert.match(siteCss, /\.public-ui-table--nested th:last-child \{ padding-inline-start: calc\(0\.45rem \+ 0\.15rem \+ 1px\); vertical-align: middle; \}/, "nested action headers retain alignment at narrow widths");
assert.match(siteCss, /\.public-ui-leaderboard-pagination \{ display: flex; flex-wrap: wrap; gap: 0\.6rem; align-items: center; justify-content: center;/, "Players pagination is centered and responsive");
assert.match(siteCss, /\.public-ui-leaderboard-pagination-control \{ display: grid; width: 1\.85rem; height: 1\.75rem;[^}]*border: 1px solid var\(--public-ui-divider\);/, "Players pagination reuses an outlined Public UI control treatment");
assert.match(siteCss, /\.public-ui-leaderboard-pagination-control:focus-visible \{ outline: 2px solid var\(--public-ui-data-blue\);/, "Players pagination controls reuse the Public UI focus treatment");

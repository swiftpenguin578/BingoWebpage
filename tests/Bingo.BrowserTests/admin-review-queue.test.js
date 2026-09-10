const assert = require("node:assert/strict");
const { initializeAdminReviewQueue } = require("../../src/Bingo.Web/wwwroot/js/admin-review-queue.js");

class Control {
  constructor(value = "") {
    this.value = value;
    this.hidden = false;
    this.listeners = {};
  }

  addEventListener(type, listener) { (this.listeners[type] ??= []).push(listener); }
  dispatch(type) {
    const event = { preventDefault() { this.defaultPrevented = true; }, defaultPrevented: false };
    for (const listener of this.listeners[type] ?? []) listener(event);
    return event;
  }
  focus() { this.focused = true; }
}

class Link {
  constructor(href) { this.href = href; }
  getAttribute(name) { return name === "href" ? this.href : null; }
}

function row(team, player, tile, status) {
  return { hidden: false, dataset: { reviewTeam: team, reviewPlayer: player, reviewTile: tile, reviewStatus: status } };
}

const form = new Control();
const search = new Control("Alice");
const status = new Control("Pending");
const clear = new Control();
const table = { hidden: false };
const empty = { hidden: true };
const timestampLink = new Link("/Admin/Review/Details/submission-1?eventId=event-123");
const detailsLink = new Link("/Admin/Review/Details/submission-1?eventId=event-123");
const rows = [
  row("Ravens", "Alice", "Zulrah", "Pending"),
  row("Wolves", "Bob", "Vorkath", "Approved"),
  row("Eagles", "Carol", "Fire Cape", "Rejected")
];
form.querySelector = selector => ({
  "[data-admin-review-search]": search,
  "[data-admin-review-status]": status,
  "[data-admin-search-clear]": clear
}[selector] ?? null);
const page = {
  dataset: { eventId: "event-123" },
  querySelector: selector => ({
    "[data-admin-review-filter-form]": form,
    "[data-admin-review-table-wrap]": table,
    "[data-admin-review-empty]": empty
  }[selector] ?? null),
  querySelectorAll: selector => {
    if (selector === "[data-admin-review-row]") return rows;
    if (selector === "a.admin-review-submission-link, a.event-overview-row-action") return [timestampLink, detailsLink];
    return [];
  }
};

global.window = {
  location: { href: "https://example.test/Admin/Review?eventId=event-123&search=Alice&status=Pending" },
  history: {
    state: { queue: true },
    replaceState(_state, _title, next) {
      this.last = String(next);
      global.window.location.href = `https://example.test${next}`;
    }
  }
};

initializeAdminReviewQueue(page);
assert.equal(rows.filter(row => !row.hidden).length, 1, "initial URL state filters the full ledger");
assert.equal(rows[0].hidden, false);
assert.match(window.history.last, /eventId=event-123/);
assert.match(window.history.last, /search=Alice/);
assert.match(window.history.last, /status=Pending/);
assert.match(timestampLink.href, /search=Alice/);
assert.match(timestampLink.href, /status=Pending/);
assert.match(detailsLink.href, /search=Alice/);
assert.match(detailsLink.href, /status=Pending/);

search.value = "wolves";
status.value = "Approved";
search.dispatch("input");
assert.equal(rows.filter(row => !row.hidden).length, 1, "search and status combine without navigation");
assert.equal(rows[1].hidden, false);
assert.match(window.history.last, /eventId=event-123/);
assert.match(window.history.last, /search=wolves/);
assert.match(window.history.last, /status=Approved/);
assert.match(timestampLink.href, /search=wolves/);
assert.match(timestampLink.href, /status=Approved/);
assert.match(detailsLink.href, /search=wolves/);
assert.match(detailsLink.href, /status=Approved/);

search.value = "fire cape";
status.value = "Rejected";
search.dispatch("input");
assert.equal(rows.filter(row => !row.hidden).length, 1, "tile search combines with the selected status");
assert.equal(rows[2].hidden, false);

search.value = "zulrah";
search.dispatch("input");
assert.equal(rows.filter(row => !row.hidden).length, 0, "a search field/status mismatch shows no matches");
assert.equal(table.hidden, true);
assert.equal(empty.hidden, false);

search.value = "";
clear.dispatch("click");
assert.equal(rows.filter(row => !row.hidden).length, 1, "clear restores the selected status filter");
assert.equal(rows[2].hidden, false);
assert.equal(search.focused, true);

status.value = "";
status.dispatch("change");
assert.equal(rows.filter(row => !row.hidden).length, 3, "clearing status restores every row");
const submit = form.dispatch("submit");
assert.equal(submit.defaultPrevented, true, "filter form never navigates while enhanced");

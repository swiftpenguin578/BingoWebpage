const assert = require("node:assert/strict");
const fs = require("node:fs");
const path = require("node:path");

const repositoryRoot = path.resolve(__dirname, "../..");
const participantsMarkup = fs.readFileSync(path.join(repositoryRoot, "src/Bingo.Web/Pages/Admin/Events/Participants.cshtml"), "utf8");
const participantForm = fs.readFileSync(path.join(repositoryRoot, "src/Bingo.Web/Pages/Admin/Events/_InternalParticipantForm.cshtml"), "utf8");
const manageScript = fs.readFileSync(path.join(repositoryRoot, "src/Bingo.Web/wwwroot/js/event-manage.js"), "utf8");
const siteCss = ["site.transitional.foundation.css", "site.public-ui.css", "site.transitional.application.css"]
  .map(file => fs.readFileSync(path.join(repositoryRoot, "src/Bingo.Web/wwwroot/css", file), "utf8"))
  .join("\n");
const desktopPlacement = siteCss.indexOf(".participant-capacity-region .participant-settings-form .participant-capacity-fields { width: 100%; max-width: none; display: grid;");
const narrowReset = siteCss.indexOf("@media (max-width: 700px)", desktopPlacement);
const narrowCss = siteCss.slice(narrowReset);

assert.match(participantsMarkup, /participant-capacity-field[\s\S]*Maximum players[\s\S]*SignupAdministration\.ParticipantCap/);
assert.match(participantsMarkup, /participant-shared-row[\s\S]*participant-shared-row-heading[\s\S]*waiting list enabled[\s\S]*participant-shared-row-content[\s\S]*Keep accepting signups after capacity is reached\./);
assert.ok(participantsMarkup.includes('aria-labelledby="waiting-list-enabled-heading" aria-describedby="waiting-list-enabled-support"'));
assert.match(participantsMarkup, /<tr class="participant-table-empty" hidden="@\(group\.Rows\.Count > 0 \? "hidden" : null\)"><td colspan="8">/);
assert.ok(siteCss.includes("grid-template-areas: \"capacity-heading waiting-heading\" \"capacity-control waiting-control\" \"capacity-error .\" \"confirm confirm\";"));
assert.ok(siteCss.includes("gap: 0.25rem 1.25rem; margin-inline: 0;"));
assert.ok(siteCss.includes(".participant-capacity-region .participant-capacity-field > label { grid-area: capacity-heading; }"));
assert.ok(siteCss.includes(".participant-capacity-region .participant-capacity-field > input { grid-area: capacity-control; }"));
assert.ok(siteCss.includes(".admin-shell-body .event-participants-page .participant-shared-row-heading { grid-area: waiting-heading;"));
assert.ok(siteCss.includes(".participant-capacity-region .participant-capacity-field > label,\n.admin-shell-body .event-participants-page .participant-shared-row-heading { color: var(--admin-text-soft); font-family: inherit; font-size: 0.75rem; font-weight: 500; line-height: 1.25; }"));
assert.ok(siteCss.includes(".admin-shell-body .event-participants-page .participant-shared-row { display: contents; }"));
assert.ok(!participantsMarkup.includes("participant-shared-row-toggle") && !siteCss.includes("participant-shared-row-toggle"));
assert.ok(desktopPlacement >= 0 && narrowReset > desktopPlacement);
assert.ok(narrowCss.includes(".participant-capacity-region .participant-settings-form .participant-capacity-fields { width: 100%; grid-template-columns: 1fr; grid-template-rows: none; grid-template-areas: none; }"));
assert.ok(narrowCss.includes(".participant-capacity-region .participant-capacity-field { display: grid; }"));
assert.ok(narrowCss.includes(".admin-shell-body .event-participants-page .participant-shared-row { display: grid; grid-template-columns: 1fr; grid-column: auto; grid-row: auto;"));
assert.ok(narrowCss.includes(".admin-shell-body .event-participants-page .participant-shared-row-heading,\n  .admin-shell-body .event-participants-page .participant-shared-row-content { grid-column: auto; grid-row: auto; }"));
assert.ok(narrowCss.includes(".admin-shell-body .event-participants-page .participant-shared-row-content { display: flex;"));
assert.ok(narrowCss.includes(".admin-shell-body .event-participants-page .participant-shared-row-heading,\n  .admin-shell-body .event-participants-page .participant-shared-row-content,\n  .admin-shell-body .event-participants-page .participant-waiting-confirm { grid-area: auto; }"));
assert.doesNotMatch(siteCss, /(?:participant|event-participants-page)[^}]*70rem|70rem[^}]*?(?:participant|event-participants-page)/is);
assert.ok(manageScript.includes('const routePage = page.querySelector(".participant-add-route-page")'));
assert.ok(manageScript.includes("const interactionTarget = trigger instanceof HTMLElement ? trigger : routePage"));
assert.ok(manageScript.includes("const directRouteFallback = routePage instanceof HTMLElement && trigger?.hidden === true"));
assert.ok(manageScript.includes("hide(false, false)"));
assert.ok(manageScript.includes("const focusTarget = directRouteFallback && returnToParticipants ? trigger : opener"));
assert.ok(manageScript.includes("!focusTarget.hidden"));
assert.ok(manageScript.includes("setRouteVisibility(true)"));
assert.ok(manageScript.includes("window.createAdminEditorGuard"));
assert.ok(manageScript.includes('prefix: "participant-add"'));
assert.ok(manageScript.includes("response.url || action"));
assert.ok(!manageScript.includes("const canEnhance = () => window.innerWidth > 900"));
assert.ok(participantsMarkup.includes("data-participant-add-route-trigger hidden"));
assert.ok(participantsMarkup.indexOf("data-participant-add-route-trigger hidden") < participantsMarkup.indexOf('<section class="participant-add-route-page"'));
assert.ok(participantForm.includes("data-participant-add-discard"));
assert.ok(participantForm.includes("data-participant-add-feedback"));
assert.ok(participantForm.includes('class="btn admin-button-create" type="submit">+ @T["Create participant"]'));
assert.ok(siteCss.includes(".admin-shell-body .admin-button-create"));
assert.ok(siteCss.includes("#participant-add-dialog { width: 100%; max-width: none; height: 100dvh;"));
assert.ok(!siteCss.includes("participant-add-dialog-page .admin-dialog-page-actions .admin-button-primary"));

class Element {
  constructor({ tagName = "div", dataset = {}, textContent = "", children = [] } = {}) {
    this.tagName = tagName.toUpperCase();
    this.dataset = dataset;
    this.textContent = textContent;
    this.children = children;
    this.hidden = false;
    this.attributes = {};
    this.listeners = {};
    this.classList = { add: (...names) => names.forEach((name) => { this.className = `${this.className ?? ""} ${name}`.trim(); }), remove: (...names) => { names.forEach((name) => { this.className = (this.className ?? "").split(" ").filter((item) => item && item !== name).join(" "); }); }, contains: (name) => (this.className ?? "").split(" ").includes(name) };
    children.forEach((child) => { child.parentElement = this; });
  }

  addEventListener(type, listener) { (this.listeners[type] ??= []).push(listener); }
  dispatch(type) {
    const event = { type, target: this, defaultPrevented: false, preventDefault() { this.defaultPrevented = true; } };
    for (const listener of this.listeners[type] ?? []) listener(event);
    return event;
  }
  querySelector(selector) { return this.querySelectorAll(selector)[0] ?? null; }
  querySelectorAll(selector) {
    if (this.selectors?.[selector]) return this.selectors[selector];
    if (selector === "path") return this.children.filter((child) => child.tagName === "PATH");
    if (selector === ".participant-sort-icon") return this.children.filter((child) => child.className === "participant-sort-icon");
    return [];
  }
  closest(selector) {
    let current = this;
    while (current) {
      if (selector === ".event-participants-page" && current.dataset.eventParticipantsPage !== undefined) return current;
      if (selector === "th" && current.tagName === "TH") return current;
      current = current.parentElement;
    }
    return null;
  }
  setAttribute(name, value) { this.attributes[name] = String(value); if (name === "class") this.className = String(value); }
  removeAttribute(name) { delete this.attributes[name]; }
  getAttribute(name) { return this.attributes[name] ?? null; }
  append(child) { child.parentElement = this; this.children.push(child); }
  replaceChildren(...children) { this.children = children; children.forEach((child) => { child.parentElement = this; }); }
  remove() { this.parentElement.children = this.parentElement.children.filter((child) => child !== this); }
  focus() { this.focused = true; }
  contains(candidate) { return this === candidate || this.children.some((child) => child.contains(candidate)); }
}

class SelectElement extends Element {}
class InputElement extends Element {}
class FormElement extends Element {}
class DetailsElement extends Element {}
class DialogElement extends Element {
  showModal() { this.open = true; }
  close() { this.open = false; }
}

function table(rows, sortButtons) {
  const empty = new Element({ tagName: "tr", children: [new Element({ tagName: "td", textContent: "No matching participants." })] });
  empty.className = "participant-table-empty";
  empty.hidden = rows.length > 0;
  const tbody = new Element({ tagName: "tbody", children: [empty, ...rows] });
  tbody.insertBefore = (row, before) => {
    tbody.children = tbody.children.filter((item) => item !== row);
    const index = before ? tbody.children.indexOf(before) : -1;
    tbody.children.splice(index < 0 ? tbody.children.length : index, 0, row);
    row.parentElement = tbody;
  };
  const element = new Element({ tagName: "table" });
  element.selectors = {
    "tbody": [tbody],
    "[data-participant-row]": () => tbody.children.filter((row) => row.dataset.participantRow !== undefined),
    ".participant-table-empty": [empty]
  };
  element.querySelectorAll = (selector) => typeof element.selectors[selector] === "function" ? element.selectors[selector]() : element.selectors[selector] ?? [];
  sortButtons.forEach((button) => { button.parentElement.parentElement = element; });
  return { element, empty, tbody };
}

function row(name, status, sequence) {
  return new Element({
    tagName: "tr",
    textContent: `${sequence} ${name} ${status}`,
    dataset: {
      participantRow: "",
      participantName: name,
      participantStatus: status,
      participantSequence: String(sequence),
      participantEhb: String(sequence),
      participantPayment: "Unpaid",
      participantSignedUp: String(sequence),
      participantOwnership: "Unassigned"
    }
  });
}

function sortButton(key, label) {
  const th = new Element({ tagName: "th" });
  const button = new Element({ tagName: "a", dataset: { participantSort: "", sortKey: key, sortLabel: label } });
  button.parentElement = th;
  return { button, th };
}

const currentSort = sortButton("ownership", "Team");
const currentName = sortButton("participant", "Participant");
const historySort = sortButton("ownership", "Team");
const historyName = sortButton("participant", "Participant");
const currentRows = [row("Bob", "WaitingList", 2), row("Alice", "Confirmed", 1)];
const historyRows = [row("Carol", "Withdrawn", 3)];
const current = table(currentRows, [currentSort.button, currentName.button]);
const history = table(historyRows, [historySort.button, historyName.button]);
const search = new InputElement({ dataset: { participantSearchInput: "" } });
search.value = "";
const clear = new Element({ dataset: { adminSearchClear: "" } });
const status = new SelectElement();
status.value = "";
const form = new Element();
form.action = "/Admin/Events/Participants/test";
form.requestSubmit = () => { throw new Error("Participants filters should not navigate when JavaScript is active."); };
form.selectors = {
  "[data-participant-search-input]": [search],
  "select[name='ParticipantStatus']": [status],
  "[data-admin-search-clear]": [clear]
};
form.querySelectorAll = (selector) => form.selectors[selector] ?? [];

const page = new Element({ dataset: { eventParticipantsPage: "" } });
const addTrigger = new Element({ tagName: "a", dataset: { participantAddTrigger: "" }, textContent: "Add participant" });
addTrigger.setAttribute("href", "/Admin/Events/Participants/test?addParticipant=1");
page.selectors = {
  "form[data-participant-filter-form]": [form],
  "[data-participant-table]": [current.element, history.element],
  "[data-participant-sort]": [currentSort.button, currentName.button, historySort.button, historyName.button],
  "[data-participant-group]": [],
  "[data-participant-add-trigger]": [addTrigger]
};
page.querySelectorAll = (selector) => page.selectors[selector] ?? [];
form.parentElement = page;
const body = new Element({ children: [page] });

global.HTMLElement = Element;
global.HTMLSelectElement = SelectElement;
global.HTMLInputElement = InputElement;
global.HTMLFormElement = FormElement;
global.HTMLDetailsElement = DetailsElement;
global.HTMLDialogElement = DialogElement;
const documentListeners = {};
global.window = {
  innerWidth: 1200,
  location: { href: "https://example.test/Admin/Events/Participants/test?sort=ownership&direction=desc" },
  history: { replaceState(_state, _title, next) { this.last = next; global.window.location.href = next instanceof URL ? next.href : `https://example.test${next}`; }, pushState(_state, _title, next) { this.last = next; global.window.location.href = next instanceof URL ? next.href : `https://example.test${next}`; }, state: null },
  setTimeout(callback) { callback(); },
  addEventListener() {},
  removeEventListener() {},
  fetch: async (url) => { global.fetchedParticipantUrl = String(url); return { ok: false }; }
};
global.history = global.window.history;
global.document = {
  body,
  addEventListener(type, listener) { (documentListeners[type] ??= []).push(listener); },
  dispatchEvent(event) { for (const listener of documentListeners[event.type] ?? []) listener(event); },
  createElement: (tagName) => tagName === "dialog" ? new DialogElement({ tagName }) : new Element({ tagName }),
  createElementNS: (_namespace, tagName) => new Element({ tagName }),
  querySelector(selector) { return selector === "dialog.admin-route-dialog[open]" ? null : null; },
  querySelectorAll(selector) { return selector === ".event-participants-page" ? [page] : []; }
};

require("../../src/Bingo.Web/wwwroot/js/admin-editor-guard.js");
require("../../src/Bingo.Web/wwwroot/js/event-manage.js");

void (async () => {
  search.value = "alice";
  search.dispatch("input");
  assert.equal(currentRows[0].hidden, true);
  assert.equal(historyRows[0].hidden, true);
  assert.equal(history.empty.hidden, false);
assert.match(window.location.href, /ParticipantSearch=alice/);
assert.match(window.location.href, /sort=ownership/);

status.value = "Confirmed";
status.dispatch("change");
assert.equal(currentRows[1].hidden, false);
assert.equal(window.location.href.includes("ParticipantStatus=Confirmed"), true);

const click = currentName.button.dispatch("click");
assert.equal(click.defaultPrevented, true);
assert.deepEqual(current.element.querySelectorAll("[data-participant-row]").map((item) => item.dataset.participantName), ["Alice", "Bob"]);
assert.equal(currentName.th.getAttribute("aria-sort"), "ascending");
assert.equal(currentSort.th.getAttribute("aria-sort"), null);
assert.equal(historyName.th.getAttribute("aria-sort"), "ascending");
assert.equal(historySort.th.getAttribute("aria-sort"), null);
assert.equal(currentName.button.querySelectorAll(".participant-sort-icon").length, 1);
assert.equal(currentSort.button.querySelectorAll(".participant-sort-icon").length, 0);
assert.equal(historyName.button.querySelectorAll(".participant-sort-icon").length, 1);
assert.equal(historySort.button.querySelectorAll(".participant-sort-icon").length, 0);
assert.match(window.location.href, /ParticipantSearch=alice/);
assert.match(window.location.href, /ParticipantStatus=Confirmed/);
assert.match(window.location.href, /sort=participant/);
assert.match(window.location.href, /direction=asc/);

search.value = "nobody";
search.dispatch("input");
assert.equal(current.empty.hidden, false);
assert.equal(history.empty.hidden, false);

search.value = "alice";
search.dispatch("input");
clear.dispatch("click");
assert.equal(search.value, "");
assert.equal(clear.hidden, true);
assert.equal(search.focused, true);
assert.doesNotMatch(window.location.href, /ParticipantSearch=/);

  addTrigger.dispatch("click");
  await Promise.resolve();
  await Promise.resolve();
  assert.equal(body.classList.contains("admin-route-dialog-open"), true);
  assert.match(global.fetchedParticipantUrl, /addParticipant=1/);
  document.dispatchEvent({ type: "bingo:content-will-update" });
  assert.equal(body.classList.contains("admin-route-dialog-open"), false);
})().catch((error) => { console.error(error); process.exitCode = 1; });

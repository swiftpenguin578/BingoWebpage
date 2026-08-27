const assert = require("node:assert/strict");

class Node {
  constructor(tagName, { id, className, dataset = {}, textContent = "", children = [] } = {}) {
    this.tagName = tagName.toUpperCase();
    this.id = id;
    this.className = className ?? "";
    this.dataset = dataset;
    this.textContent = textContent;
    this.children = [];
    this.attributes = {};
    this.listeners = {};
    this.hidden = false;
    this.open = false;
    this.classList = {
      add: (...names) => names.forEach(name => { if (!this.className.split(" ").includes(name)) this.className = `${this.className} ${name}`.trim(); }),
      remove: (...names) => { this.className = this.className.split(" ").filter(name => name && !names.includes(name)).join(" "); },
      toggle: (name, force) => { if (force === undefined ? !this.classList.contains(name) : force) this.classList.add(name); else this.classList.remove(name); },
      contains: name => this.className.split(" ").includes(name)
    };
    this.append(...children);
  }
  append(...children) { children.flat().forEach(child => { child.parentElement = this; this.children.push(child); }); }
  replaceChildren(...children) { this.children = []; this.append(...children); }
  remove() { this.parentElement?.children.splice(this.parentElement.children.indexOf(this), 1); this.parentElement = null; }
  removeAttribute(name) { delete this.attributes[name]; }
  addEventListener(type, listener) { (this.listeners[type] ??= []).push(listener); }
  dispatch(type, properties = {}) { const event = { type, target: this, defaultPrevented: false, propagationStopped: false, preventDefault() { this.defaultPrevented = true; }, stopPropagation() { this.propagationStopped = true; }, ...properties }; for (const listener of this.listeners[type] ?? []) listener(event); return event; }
  setAttribute(name, value) { this.attributes[name] = String(value); if (name === "id") this.id = String(value); if (name === "class") this.className = String(value); if (name.startsWith("data-")) this.dataset[name.slice(5).replace(/-([a-z])/g, (_m, c) => c.toUpperCase())] = String(value); }
  getAttribute(name) { if (name === "id") return this.id ?? null; if (name === "class") return this.className || null; if (name.startsWith("data-")) return this.dataset[name.slice(5).replace(/-([a-z])/g, (_m, c) => c.toUpperCase())] ?? null; return this.attributes[name] ?? null; }
  focus() { this.focused = true; }
  contains(node) { return this === node || this.children.some(child => child.contains(node)); }
  querySelector(selector) { return this.querySelectorAll(selector)[0] ?? null; }
  querySelectorAll(selector) { const result = []; const visit = node => { node.children.forEach(child => { if (child.matches(selector)) result.push(child); visit(child); }); }; visit(this); return result; }
  matches(selector) {
    if (selector.includes(",")) return selector.split(",").some(item => this.matches(item.trim()));
    const data = selector.match(/^([^[]*)\[data-([\w-]+)(?:=['"]([^'"]*)['"])?\]$/);
    if (data) { const [, prefix, key, value] = data; const datasetKey = key.replace(/-([a-z])/g, (_m, c) => c.toUpperCase()); return (!prefix || this.matches(prefix)) && Object.hasOwn(this.dataset, datasetKey) && (value === undefined || String(this.dataset[datasetKey]) === value); }
    const tagClass = selector.match(/^([^.#[\s]+)\.([\w-]+)(\[open\])?$/);
    if (tagClass) return this.tagName === tagClass[1].toUpperCase() && this.classList.contains(tagClass[2]) && (!tagClass[3] || this.open);
    const id = selector.match(/^([^.#[]+)?#([\w-]+)(?:\[open\])?$/);
    if (id) return (!id[1] || this.tagName === id[1].toUpperCase()) && this.id === id[2] && (!selector.includes("[open]") || this.open);
    const attr = selector.match(/^([^.#[]*)\[([^=]+)=['"]([^'"]+)['"]\]$/);
    if (attr) return (!attr[1] || this.matches(attr[1])) && this.getAttribute(attr[2]) === attr[3];
    if (selector.startsWith(".")) return this.classList.contains(selector.slice(1));
    if (selector.startsWith("#")) return this.id === selector.slice(1);
    return this.tagName === selector.toUpperCase();
  }
  cloneNode(deep) { const clone = new Node(this.tagName, { id: this.id, className: this.className, dataset: { ...this.dataset }, textContent: this.textContent, children: deep ? this.children.map(child => child.cloneNode(true)) : [] }); if (this.options) clone.options = this.options.map(option => ({ ...option })); return clone; }
}

class Dialog extends Node {
  constructor() { super("dialog", { id: "catalogue-editor-dialog", className: "admin-route-dialog catalogue-route-dialog" }); }
  showModal() { if (!body.contains(this)) throw new Error("InvalidStateError: dialog is detached"); this.open = true; }
  close() { this.open = false; }
}

const pageUrl = "/Admin/Catalogue/Index";
const recordUrl = `${pageUrl}?bossId=record-1`;
const entries = [{ state: {}, url: `https://example.test${recordUrl}&overlay=1` }];
let entryIndex = 0;
const requestedUrls = [];
const windowListeners = {};
const documentListeners = {};
const failedDirectEntry = process.env.CATALOGUE_FAILURE === "1";
const body = new Node("body");
const main = new Node("main", { id: "main-content" });
const setUrl = value => { window.location.href = value instanceof URL ? value.href : new URL(value, "https://example.test").href; };
let backCalls = 0;
const history = {
  state: entries[0].state,
  replaceState(state, _title, value) { entries[entryIndex] = { state, url: new URL(value, window.location.href).href }; this.state = state; setUrl(entries[entryIndex].url); },
  pushState(state, _title, value) { entries.splice(entryIndex + 1); entries.push({ state, url: new URL(value, window.location.href).href }); entryIndex++; this.state = state; setUrl(entries[entryIndex].url); },
  back() { backCalls++; if (entryIndex === 0) return; entryIndex--; this.state = entries[entryIndex].state; setUrl(entries[entryIndex].url); windowListeners.popstate?.forEach(listener => listener({ type: "popstate" })); }
};

function cataloguePage(editorRoute) {
  return new Node("section", { className: "admin-catalogue-page", dataset: { cataloguePage: "", catalogueEditorRoute: String(editorRoute), catalogueOverlay: String(editorRoute), catalogueUrl: pageUrl } });
}

function workspacePage() {
  const page = cataloguePage(false);
  const search = new Node("input", { id: "boss-search" });
  search.value = "";
  const clear = new Node("button", { dataset: { adminSearchClear: "" } });
  clear.hidden = true;
  const filter = new Node("select", { dataset: { catalogueCategoryFilter: "" } });
  filter.options = [{ value: "all" }, { value: "Boss" }, { value: "Skilling boss" }, { value: "Minigame" }, { value: "inactive" }];
  filter.value = "all";
  const empty = new Node("p", { id: "no-boss-results" });
  const form = new Node("form", { dataset: { catalogueDirectorySearch: "" } });
  form.append(search, clear, filter);
  const card = new Node("a", { className: "catalogue-record-card", dataset: { catalogueRecord: "", editorUrl: recordUrl, category: "Boss", active: "true", search: "araxxor boss fang" } });
  card.setAttribute("href", recordUrl);
  page.append(form, empty, card);
  return page;
}

function editorPage() {
  const page = cataloguePage(true);
  const rateLabel = new Node("label", { dataset: { catalogueRateLabel: "" }, textContent: "Kills per hour" });
  const rateCategory = new Node("select", { dataset: { catalogueRateCategory: "" } });
  rateCategory.value = "Boss";
  const dropRows = ["drop-1", "drop-2"].map(id => {
    const row = new Node("article", { dataset: { catalogueDropRow: "", catalogueDropInitialExpanded: "false", catalogueDropId: id } });
    const toggle = new Node("button", { dataset: { catalogueDropToggle: "" } });
    const panel = new Node("div", { dataset: { catalogueDropEditor: "" } });
    row.append(toggle, panel);
    return { row, toggle, panel };
  });
  const confirmation = new Node("details", { className: "catalogue-confirmation-box", children: [new Node("summary"), new Node("button", { dataset: { catalogueConfirmationCancel: "" } })] });
  page.append(new Node("section", { className: "catalogue-editor-component", dataset: { catalogueEditor: "" }, children: [new Node("button", { dataset: { catalogueClose: "" } }), rateLabel, rateCategory, ...dropRows.map(drop => drop.row), confirmation] }));
  return page;
}

body.append(main);
main.append(editorPage());

global.HTMLElement = Node;
global.HTMLInputElement = Node;
global.HTMLSelectElement = Node;
global.HTMLFormElement = Node;
global.window = {
  innerWidth: 1200,
  location: { href: entries[0].url, replace(value) { this.replaced = new URL(value, this.href).href; setUrl(this.replaced); } },
  fetch: async url => { requestedUrls.push(String(url)); const isEditor = String(url).includes("bossId"); return { ok: !(failedDirectEntry && isEditor), text: async () => isEditor ? "editor" : "workspace" }; },
  setTimeout: callback => callback(),
  addEventListener(type, listener) { (windowListeners[type] ??= []).push(listener); }
};
global.location = { get pathname() { return new URL(window.location.href).pathname; } };
global.history = history;
global.sessionStorage = { getItem: () => null, setItem: () => {} };
global.document = {
  body,
  addEventListener(type, listener) { (documentListeners[type] ??= []).push(listener); },
  dispatchEvent(event) { documentListeners[event.type]?.forEach(listener => listener(event)); },
  createElement: tag => tag === "dialog" ? new Dialog() : new Node(tag),
  importNode: node => node.cloneNode(true),
  querySelector: selector => selector === "main#main-content" ? main : body.querySelector(selector),
  querySelectorAll: selector => body.querySelectorAll(selector)
};
global.DOMParser = class { parseFromString(value) { return { querySelector: selector => selector.includes("catalogue-editor-route='false'") ? workspacePage() : selector === "[data-catalogue-page]" ? editorPage() : null }; } };

require("../../src/Bingo.Web/wwwroot/js/catalogue-admin.js");

(async () => {
  for (let index = 0; index < 10; index++) await Promise.resolve();
  if (failedDirectEntry) {
    assert.equal(main.hidden, false, "direct-load failure restores the underlying Catalogue workspace");
    assert.ok(document.querySelector("dialog#catalogue-editor-dialog[open]"), "direct-load failure keeps a visible error dialog");
    return;
  }
  assert.equal(new URL(window.location.href).searchParams.get("overlay"), "1");
  assert.deepEqual(requestedUrls.map(value => new URL(value).searchParams.get("overlay")), [null, "1"], "direct desktop entry requests workspace then editor");
  assert.equal(main.hidden, false, "restored workspace is revealed after loading");
  assert.ok(main.querySelector("[data-catalogue-record]"), "restored workspace contains catalogue cards");
  const search = main.querySelector("#boss-search");
  const filter = main.querySelector("[data-catalogue-category-filter]");
  const clear = main.querySelector("[data-admin-search-clear]");
  search.value = "fang";
  search.dispatch("input");
  filter.value = "Minigame";
  filter.dispatch("change");
  assert.equal(main.querySelector("[data-catalogue-record]").hidden, true, "category select drives client-side filtering");
  filter.value = "all";
  filter.dispatch("change");
  search.value = "fang";
  search.dispatch("input");
  assert.equal(clear.hidden, false, "search clear is visible for non-empty input");
  clear.dispatch("click");
  assert.equal(search.value, "", "clear button empties the search");
  assert.equal(main.querySelector("[data-catalogue-record]").hidden, false, "clear reapplies search filtering");
  assert.equal(filter.value, "all", "clear preserves the selected category");
  assert.equal(clear.hidden, true, "search clear hides when empty");
  assert.equal(search.focused, true, "clear returns focus to search");
  const dialog = document.querySelector("dialog#catalogue-editor-dialog[open]");
  assert.ok(dialog, "direct desktop entry opens a connected editor dialog");
  assert.ok(dialog.querySelector(".admin-catalogue-page"), "desktop-loaded editor retains the Catalogue scoped wrapper");
  const rateCategory = dialog.querySelector("[data-catalogue-rate-category]");
  const rateLabel = dialog.querySelector("[data-catalogue-rate-label]");
  assert.equal(rateLabel.textContent, "Kills per hour", "AddBoss defaults to kill wording");
  rateCategory.value = "Minigame";
  rateCategory.dispatch("change");
  assert.equal(rateLabel.textContent, "Runs per hour", "AddBoss switches to run wording for Minigame");
  rateCategory.value = "Skilling boss";
  rateCategory.dispatch("change");
  assert.equal(rateLabel.textContent, "Kills per hour", "AddBoss uses kill wording for Skilling boss");
  const dropRows = dialog.querySelectorAll("[data-catalogue-drop-row]");
  const firstDropToggle = dropRows[0].querySelector("[data-catalogue-drop-toggle]");
  const secondDropToggle = dropRows[1].querySelector("[data-catalogue-drop-toggle]");
  assert.equal(dropRows[0].querySelector("[data-catalogue-drop-editor]").hidden, true, "drop editors start collapsed after enhancement");
  firstDropToggle.dispatch("click");
  assert.equal(dropRows[0].querySelector("[data-catalogue-drop-editor]").hidden, false, "edit action expands its drop");
  secondDropToggle.dispatch("click");
  assert.equal(dropRows[0].querySelector("[data-catalogue-drop-editor]").hidden, true, "opening another drop closes the first");
  assert.equal(dropRows[1].querySelector("[data-catalogue-drop-editor]").hidden, false, "only the selected drop remains expanded");
  secondDropToggle.dispatch("click");
  assert.equal(dropRows[1].querySelector("[data-catalogue-drop-editor]").hidden, true, "activating the open drop closes it");

  const confirmation = dialog.querySelector("details.catalogue-confirmation-box");
  const summary = confirmation.querySelector("summary");
  const openConfirmation = () => { confirmation.setAttribute("open", ""); confirmation.open = true; };
  const backCallsBeforeConfirmation = backCalls;
  openConfirmation();
  const backdropEvent = dialog.dispatch("click", { target: dialog });
  assert.equal(confirmation.open, false, "outer backdrop closes the open confirmation first");
  assert.equal(summary.focused, true, "closing the confirmation restores focus to its summary");
  assert.equal(backCalls, backCallsBeforeConfirmation, "outer backdrop does not close the editor while confirmation is open");
  assert.equal(backdropEvent.defaultPrevented, false, "confirmation backdrop close preserves the existing click behavior");

  openConfirmation();
  const cancelEvent = dialog.dispatch("cancel", { target: dialog });
  assert.equal(confirmation.open, false, "outer cancel closes the open confirmation first");
  assert.equal(backCalls, backCallsBeforeConfirmation, "outer cancel does not close the editor while confirmation is open");
  assert.equal(cancelEvent.defaultPrevented, true, "outer cancel is prevented while confirmation is open");

  openConfirmation();
  const escapeEvent = dialog.dispatch("keydown", { key: "Escape", target: dialog });
  assert.equal(confirmation.open, false, "outer Escape closes the open confirmation first");
  assert.equal(backCalls, backCallsBeforeConfirmation, "outer Escape does not close the editor while confirmation is open");
  assert.equal(escapeEvent.defaultPrevented, true, "outer Escape is prevented while confirmation is open");
  assert.equal(escapeEvent.propagationStopped, true, "outer Escape does not propagate while confirmation is open");

  dialog.dispatch("click", { target: dialog });
  assert.equal(backCalls, backCallsBeforeConfirmation + 1, "the next outer backdrop follows the route close path");
  assert.equal(document.querySelector("dialog#catalogue-editor-dialog[open]"), null, "the next outer backdrop closes the editor");

  const card = main.querySelector("[data-catalogue-record]");
  card.dispatch("click");
  for (let index = 0; index < 5; index++) await Promise.resolve();
  assert.equal(document.querySelector("dialog#catalogue-editor-dialog[open]") !== null, true, "card activation opens the route editor");
  window.innerWidth = 800;
  windowListeners.resize.forEach(listener => listener({ type: "resize" }));
  assert.equal(new URL(window.location.replaced).searchParams.has("overlay"), false, "narrow resize falls back to standalone route");

  document.dispatchEvent({ type: "bingo:content-updated", detail: { selectors: [".catalogue-editor-component"] } });
  const currentDialog = document.querySelector("dialog#catalogue-editor-dialog");
  assert.equal(currentDialog.querySelector("[data-catalogue-close]").dataset.catalogueCloseReady, "true", "partial updates rebind the current Catalogue editor");
})().catch(error => { console.error(error); process.exitCode = 1; });

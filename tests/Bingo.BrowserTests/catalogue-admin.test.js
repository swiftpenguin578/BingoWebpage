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
    this.value = "";
    this.disabled = false;
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
  replaceWith(next) { const parent = this.parentElement; const index = parent.children.indexOf(this); parent.children[index] = next; next.parentElement = parent; this.parentElement = null; }
  scrollIntoView(options) { this.scrollOptions = options; }
  get form() { for (let node = this.parentElement; node; node = node.parentElement) if (node.tagName === "FORM") return node; return null; }
  requestSubmit() { this.dispatch("submit"); }
  reset() { this.querySelectorAll("input").forEach(input => { input.value = input.defaultValue || ""; }); }
  remove() { this.parentElement?.children.splice(this.parentElement.children.indexOf(this), 1); this.parentElement = null; }
  removeAttribute(name) { delete this.attributes[name]; }
  addEventListener(type, listener) { (this.listeners[type] ??= []).push(listener); }
  dispatch(type, properties = {}) { const event = { type, target: this, defaultPrevented: false, propagationStopped: false, preventDefault() { this.defaultPrevented = true; }, stopPropagation() { this.propagationStopped = true; }, ...properties }; for (const listener of this.listeners[type] ?? []) listener(event); return event; }
  setAttribute(name, value) { this.attributes[name] = String(value); if (name === "id") this.id = String(value); if (name === "class") this.className = String(value); if (name.startsWith("data-")) this.dataset[name.slice(5).replace(/-([a-z])/g, (_m, c) => c.toUpperCase())] = String(value); }
  getAttribute(name) { if (name === "name") return this.name || null; if (name === "id") return this.id ?? null; if (name === "class") return this.className || null; if (name.startsWith("data-")) return this.dataset[name.slice(5).replace(/-([a-z])/g, (_m, c) => c.toUpperCase())] ?? null; return this.attributes[name] ?? null; }
  focus() { this.focused = true; }
  contains(node) { return this === node || this.children.some(child => child.contains(node)); }
  querySelector(selector) { return this.querySelectorAll(selector)[0] ?? null; }
  querySelectorAll(selector) { const result = []; const visit = node => { node.children.forEach(child => { if (child.matches(selector)) result.push(child); visit(child); }); }; visit(this); return result; }
  matches(selector) {
    const attrs = selector.match(/\[[^\]]+\]/g);
    if (attrs?.length > 1) return attrs.every(attr => this.matches(attr));
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
  cloneNode(deep) { const clone = new Node(this.tagName, { id: this.id, className: this.className, dataset: { ...this.dataset }, textContent: this.textContent, children: deep ? this.children.map(child => child.cloneNode(true)) : [] }); clone.value = this.value; clone.name = this.name; clone.action = this.action; clone.attributes = { ...this.attributes }; clone.hidden = this.hidden; clone.defaultValue = this.defaultValue; if (this.options) clone.options = this.options.map(option => ({ ...option })); return clone; }
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
const toasts = [];
const windowListeners = {};
const documentListeners = {};
const failedDirectEntry = process.env.CATALOGUE_FAILURE === "1";
const body = new Node("body", { dataset: { adminCatalogueRunsPerHour: "Runs per hour", adminCatalogueKillsPerHour: "Kills per hour", signupQuestionsSaveError: "Could not save", adminCatalogueStaleError: "Record changed; reload before saving" } });
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
  const editor = page.querySelector("[data-catalogue-editor]");
  const field = (name, value) => { const input = new Node("input"); input.name = name; input.value = value; input.defaultValue = value; return input; };
  const activity = new Node("form"); activity.action = `https://example.test${recordUrl}&handler=UpdateBoss&overlay=1`;
  activity.append(field("name", "Hydra"), field("recordId", "record-1"), field("expectedVersion", "1"));
  const dropForm = new Node("form"); dropForm.action = `https://example.test${recordUrl}&handler=UpdateDrop&overlay=1`; dropForm.append(field("itemName", "Fang"));
  const itemName = dropForm.querySelector("input"); itemName.dataset.originalItemName = "Fang";
  const useExisting = field("useExistingItem", "false"); useExisting.dataset.useExistingItem = "";
  const duplicate = new Node("div", { dataset: { catalogueDuplicateConfirmation: "" } }); duplicate.hidden = true;
  duplicate.append(new Node("button", { dataset: { catalogueDuplicateCancel: "" } }), new Node("button", { dataset: { catalogueDuplicateConfirm: "" } }));
  dropForm.append(useExisting, duplicate);
  const otherName = field("otherItem", "Claw"); otherName.dataset.originalItemName = "Claw";
  const otherForm = new Node("form"); otherForm.append(otherName); dropRows[1].panel.append(otherForm);
  dropRows[0].panel.append(dropForm);
  const deleteForm = new Node("form"); deleteForm.action = `https://example.test${recordUrl}&handler=Delete&overlay=1`; deleteForm.append(field("confirmation", "")); confirmation.append(deleteForm);
  const discard = new Node("div", { dataset: { catalogueEditorDiscard: "" } }); discard.hidden = true;
  discard.append(new Node("button", { dataset: { catalogueEditorKeep: "" } }), new Node("button", { dataset: { catalogueEditorDiscardConfirm: "" } }));
  editor.append(activity, discard, new Node("p", { dataset: { catalogueEditorFeedback: "" } }), new Node("button", { dataset: { catalogueReload: "" } }));
  return page;
}

body.append(main);
main.append(editorPage());

global.HTMLElement = Node;
global.HTMLInputElement = Node;
global.HTMLSelectElement = Node;
global.HTMLFormElement = Node;
global.FormData = class {
  constructor(form) { this.entries = form.querySelectorAll("input, select, textarea").filter(item => item.name && !item.disabled).map(item => [item.name, item.value]); }
  get(name) { return this.entries.find(entry => entry[0] === name)?.[1] ?? null; }
  *[Symbol.iterator]() { yield* this.entries; }
};
global.window = {
  innerWidth: 1200,
  showBingoToast(message, type) { toasts.push({ message, type }); },
  location: { href: entries[0].url, reload() { this.reloads = (this.reloads || 0) + 1; }, replace(value) { this.replaced = new URL(value, this.href).href; setUrl(this.replaced); } },
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
global.DOMParser = class { parseFromString(value) {
  if (value === "deleted" || value === "missing") {
    const result = cataloguePage(true);
    result.dataset.catalogueStatusType = value === "missing" ? "warning" : "success";
    result.dataset.catalogueStatusMessage = "Server outcome";
    result.append(new Node("section", { dataset: { catalogueEditor: "" } }));
    return { querySelector: selector => selector === "[data-catalogue-page]" ? result : null };
  }
  return { querySelector: selector => selector.includes("catalogue-editor-route='false'") ? workspacePage() : selector === "[data-catalogue-page]" ? editorPage() : null }; } };

require("../../src/Bingo.Web/wwwroot/js/admin-editor-guard.js");
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
  (windowListeners.resize || []).forEach(listener => listener({ type: "resize" }));
  assert.equal(new URL(window.location.href).searchParams.get("overlay"), "1", "narrow resize retains modal route");
  assert.equal(document.querySelector("dialog#catalogue-editor-dialog[open]") !== null, true);

  document.dispatchEvent({ type: "bingo:content-updated", detail: { selectors: [".catalogue-editor-component"] } });
  const currentDialog = document.querySelector("dialog#catalogue-editor-dialog");
  assert.equal(currentDialog.querySelector("[data-catalogue-close]").dataset.catalogueCloseReady, "true", "partial updates rebind the current Catalogue editor");
  const dirtyForm = currentDialog.querySelector("input[name='name']").parentElement;
  const name = dirtyForm.querySelector("input[name='name']");
  name.value = "Unsaved activity";
  const dropForm = currentDialog.querySelector("input[name='itemName']").parentElement;
  const beforeCrossSave = requestedUrls.length;
  dropForm.dispatch("submit");
  assert.equal(requestedUrls.length, beforeCrossSave, "cross-form save waits for dirty edit decision");
  assert.equal(currentDialog.querySelector("[data-catalogue-editor-discard]").hidden, false);
  currentDialog.querySelector("[data-catalogue-editor-keep]").dispatch("click");
  assert.equal(name.value, "Unsaved activity");
  let settle;
  let posts = 0;
  const normalFetch = window.fetch;
  window.fetch = () => { posts++; return new Promise(resolve => { settle = resolve; }); };
  dirtyForm.dispatch("submit"); dirtyForm.dispatch("submit");
  currentDialog.querySelector("[data-catalogue-close]").dispatch("click");
  assert.equal(posts, 1, "pending saves cannot submit twice");
  assert.equal(currentDialog.open, true, "pending Close retains editor");
  settle({ ok: false });
  for (let index = 0; index < 20; index++) await Promise.resolve();
  assert.equal(name.value, "Unsaved activity", "failed save retains values");
  assert.equal(name.disabled, false, "failure restores retry controls");
  assert.deepEqual(toasts.at(-1), { message: "Could not save", type: "error" }, "failed POST emits error feedback only");
  currentDialog.querySelector("[data-catalogue-close]").dispatch("click");
  assert.equal(currentDialog.querySelector("[data-catalogue-editor-discard]").hidden, false);
  currentDialog.querySelector("[data-catalogue-editor-keep]").dispatch("click");
  window.fetch = normalFetch;
  dirtyForm.dispatch("submit");
  for (let index = 0; index < 25; index++) await Promise.resolve();
  assert.equal(currentDialog.open, true, "successful save preserves connected editor dialog");
  assert.equal(body.contains(currentDialog), true);
  assert.equal(currentDialog.querySelector("input[name='name']").value, "Hydra", "successful response replaces edited values");
  assert.equal(requestedUrls.at(-1).includes("bossId"), false, "success refreshes whole parent catalogue");
  const typedConfirmation = currentDialog.querySelector("details.catalogue-confirmation-box");
  typedConfirmation.querySelector("summary").dispatch("click");
  const typedDelete = typedConfirmation.querySelector("input[name='confirmation']");
  typedDelete.value = "DEL";
  currentDialog.querySelector("[data-catalogue-close]").dispatch("click");
  assert.equal(typedConfirmation.open, false, "discard choice temporarily hides typed confirmation");
  currentDialog.querySelector("[data-catalogue-editor-keep]").dispatch("click");
  assert.equal(typedConfirmation.open, true, "discard cancel restores typed confirmation visibly");
  assert.equal(typedDelete.value, "DEL");
  typedDelete.value = "";
  typedConfirmation.querySelector("[data-catalogue-confirmation-cancel]").dispatch("click");
  const rename = currentDialog.querySelector("input[name='itemName']");
  rename.value = "Claw";
  rename.form.dispatch("submit");
  const duplicate = rename.form.querySelector("[data-catalogue-duplicate-confirmation]");
  assert.equal(duplicate.hidden, false);
  duplicate.dispatch("keydown", { key: "Escape" });
  assert.equal(duplicate.hidden, true);
  assert.equal(rename.focused, true, "duplicate Escape restores the name field outside the hidden decision");
  const renameSave = new Node("button");
  rename.form.append(renameSave);
  rename.form.dispatch("submit", { submitter: renameSave });
  assert.equal(renameSave.hidden, true, "duplicate decision hides its Save trigger");
  duplicate.dispatch("keydown", { key: "Escape" });
  assert.equal(renameSave.hidden, false, "Escape restores Save before returning focus");
  assert.equal(renameSave.focused, true);
  assert.equal(rename.value, "Claw", "cancelling preserves the typed name");
  rename.value = "Fang";

  const latestForm = currentDialog.querySelector("input[name='name']").form;
  let stalePosts = 0;
  window.fetch = async () => { stalePosts++; return { ok: true, text: async () => "missing" }; };
  latestForm.dispatch("submit");
  for (let index = 0; index < 15; index++) await Promise.resolve();
  assert.equal(currentDialog.querySelector("[data-catalogue-editor-feedback]").textContent, "Record changed; reload before saving");
  assert.equal(currentDialog.querySelector("[data-catalogue-reload]").hidden, false);
  latestForm.dispatch("submit");
  for (let index = 0; index < 5; index++) await Promise.resolve();
  assert.equal(stalePosts, 1, "missing current record blocks resubmitting stale inputs");
  currentDialog.querySelector("[data-catalogue-close]").dispatch("click");
  window.fetch = normalFetch;
  main.querySelector("[data-catalogue-record]").dispatch("click");
  for (let index = 0; index < 10; index++) await Promise.resolve();
  const deleteDialog = document.querySelector("dialog#catalogue-editor-dialog[open]");
  window.fetch = async (url, options) => options?.method === "POST" ? { ok: true, text: async () => "deleted" } : normalFetch(url);
  deleteDialog.querySelector("input[name='confirmation']").form.dispatch("submit");
  for (let index = 0; index < 25; index++) await Promise.resolve();
  assert.equal(document.querySelector("dialog#catalogue-editor-dialog[open]"), null, "deleted activity shell does not retain an empty editor");
  assert.equal(new URL(window.location.href).searchParams.has("bossId"), false, "deleted activity URL is removed after parent refresh");
  // Exercise the standalone completion branch with a typed confirmation baseline.
  window.fetch = normalFetch;
  main.querySelector("[data-catalogue-record]").dispatch("click");
  for (let index = 0; index < 10; index++) await Promise.resolve();
  const standaloneDialog = document.querySelector("dialog#catalogue-editor-dialog[open]");
  const standaloneDelete = standaloneDialog.querySelector("input[name='confirmation']");
  standaloneDelete.value = "DELETE";
  standaloneDialog.close();
  let navigationGuarded = false;
  const replaceLocation = window.location.replace.bind(window.location);
  window.location.replace = value => {
    const event = { preventDefault() { navigationGuarded = true; } };
    (windowListeners.beforeunload || []).forEach(listener => listener(event));
    replaceLocation(value);
  };
  window.fetch = async () => ({ ok: true, text: async () => "deleted" });
  standaloneDelete.form.dispatch("submit");
  for (let index = 0; index < 15; index++) await Promise.resolve();
  assert.equal(navigationGuarded, false, "completed standalone deletion navigates without pending or typed DELETE dirty guards");
  assert.equal(new URL(window.location.replaced).searchParams.has("bossId"), false);



})().catch(error => { console.error(error); process.exitCode = 1; });

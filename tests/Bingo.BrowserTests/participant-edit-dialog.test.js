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
      contains: name => this.className.split(" ").includes(name)
    };
    this.append(...children);
  }

  append(...children) { children.flat().forEach(child => { child.parentElement = this; this.children.push(child); }); }
  prepend(...children) { children.forEach(child => { child.parentElement = this; }); this.children.unshift(...children); }
  replaceChildren(...children) { this.children = []; this.append(...children); }
  replaceWith(next) { const index = this.parentElement.children.indexOf(this); this.parentElement.children.splice(index, 1, next); next.parentElement = this.parentElement; }
  remove() { this.parentElement?.children.splice(this.parentElement.children.indexOf(this), 1); this.parentElement = null; }
  addEventListener(type, listener) { (this.listeners[type] ??= []).push(listener); }
  dispatch(type, properties = {}) {
    const event = { type, target: this, defaultPrevented: false, preventDefault() { this.defaultPrevented = true; }, stopPropagation() { this.propagationStopped = true; }, ...properties };
    for (const listener of this.listeners[type] ?? []) listener(event);
    return event;
  }
  setAttribute(name, value) { this.attributes[name] = String(value); if (name === "id") this.id = String(value); if (name === "class") this.className = String(value); }
  getAttribute(name) { if (name === "id") return this.id ?? null; if (name === "class") return this.className || null; return this.attributes[name] ?? null; }
  removeAttribute(name) { delete this.attributes[name]; }
  focus() { this.focused = true; }
  scrollIntoView(options) { this.scrollOptions = options; }
  contains(node) { return this === node || this.children.some(child => child.contains(node)); }
  closest(selector) { for (let node = this; node; node = node.parentElement) if (node.matches(selector)) return node; return null; }
  querySelector(selector) { return this.querySelectorAll(selector)[0] ?? null; }
  querySelectorAll(selector) {
    const result = [];
    const visit = node => {
      node.children.forEach(child => { if (child.matches(selector)) result.push(child); visit(child); });
    };
    visit(this);
    return result;
  }
  matches(selector) {
    if (selector.includes(",")) return selector.split(",").some(item => this.matches(item.trim()));
    if (selector === "input, select, textarea") return ["INPUT", "SELECT", "TEXTAREA"].includes(this.tagName);
    if (selector.includes(" ")) {
      const parts = selector.split(/\s+/);
      return this.matches(parts.at(-1)) && this.closest(parts.slice(0, -1).join(" ")) !== null;
    }
    const attribute = selector.match(/^([^[]*)\[data-([\w-]+)(?:=['"]([^'"]*)['"])?\]$/);
    if (attribute) {
      const [, prefix, key, value] = attribute;
      const datasetKey = key.replace(/-([a-z])/g, (_match, character) => character.toUpperCase());
      return (!prefix || this.matches(prefix)) && Object.prototype.hasOwnProperty.call(this.dataset, datasetKey) && (value === undefined || String(this.dataset[datasetKey]) === value);
    }
    if (selector.startsWith(".")) return this.classList.contains(selector.slice(1));
    if (selector.startsWith("#")) return this.id === selector.slice(1);
    const namedAttribute = selector.match(/^([^[]*)\[name=['"]([^'"]+)['"]\]$/);
    if (namedAttribute) return (!namedAttribute[1] || this.matches(namedAttribute[1])) && this.getAttribute("name") === namedAttribute[2];
    const id = selector.match(/^([^.#[]+)?#([\w-]+)(?:\[open\])?$/);
    if (id) return (!id[1] || this.tagName === id[1].toUpperCase()) && this.id === id[2] && (!selector.includes("[open]") || this.open);
    const tagClass = selector.match(/^([^.#[]+)?\.([\w-]+)(?:\[open\])?$/);
    if (tagClass) return (!tagClass[1] || this.tagName === tagClass[1].toUpperCase()) && this.classList.contains(tagClass[2]) && (!selector.includes("[open]") || this.open);
    if (selector === "[open]") return this.open;
    return this.tagName === selector.toUpperCase();
  }
  cloneNode(deep) { const copy = new this.constructor(this.tagName, { id: this.id, className: this.className, dataset: { ...this.dataset }, textContent: this.textContent, children: deep ? this.children.map(child => child.cloneNode(true)) : [] }); copy.hidden = this.hidden; return copy; }
}

class Form extends Node {}
class Dialog extends Node {
  constructor() { super("dialog", { id: "participant-edit-dialog", className: "admin-route-dialog participant-edit-dialog" }); }
  showModal() {
    if (!body.contains(this)) throw new Error("InvalidStateError: dialog is detached");
    this.open = true;
  }
  close() { this.open = false; }
}

function editor(overlay) {
  const page = new Node("section", { dataset: { participantEditPage: "", participantsUrl: "/Admin/Events/Participants/test", editPath: "/Admin/Events/Participant/test/Participants/p1", participantId: "p1" } });
  if (overlay) page.append(new Node("button", { dataset: { participantEditClose: "" } }));
  const workspace = new Node("div", { className: "participant-admin-workspace" });
  const form = new Form("form");
  workspace.append(form, new Form("form", { id: "admin-notes" }));
  const discard = new Node("div", { dataset: { participantEditorDiscard: "" }, children: [new Node("button", { dataset: { participantEditorKeep: "" } }), new Node("button", { dataset: { participantEditorDiscardConfirm: "" } })] });
  discard.hidden = true;
  page.append(new Node("p", { dataset: { participantEditorFeedback: "" } }));
  page.append(discard);
  page.append(workspace);
  if (overlay) {
    const footer = new Node("div", { className: "participant-lifecycle-footer" });
    const participantConfirmation = className => {
      const confirmation = new Node("details", { className });
      const summary = new Node("summary", { className: "admin-button-secondary" });
      const box = new Node("div", { className: "event-confirmation-box" });
      const cancel = new Node("button", { dataset: { confirmationCancel: "" } });
      const confirm = new Node("button", { className: "admin-button-secondary" });
      confirmation.append(summary, box);
      box.append(cancel, confirm);
      return confirmation;
    };
    footer.append(
      participantConfirmation("participant-confirmation-box participant-ownership-confirmation"),
      participantConfirmation("participant-confirmation-box participant-restore-confirmation"),
      participantConfirmation("admin-destructive-confirmation participant-confirmation-box")
    );
    page.append(footer);
  }
  page.form = form;
  page.workspace = workspace;
  return page;
}

function participantsPage() {
  const page = new Node("section", { className: "event-participants-page" });
  const filter = new Form("form", { dataset: { participantFilterForm: "" } });
  const search = new Node("input", { dataset: { participantSearchInput: "" } });
  const status = new Node("select");
  search.value = "";
  status.value = "";
  status.setAttribute("name", "ParticipantStatus");
  filter.append(search, status);
  page.append(filter, new Node("a", { dataset: { signupQuestionsTrigger: "true" } }));
  const row = new Node("div", { dataset: { participantId: "p1" } });
  row.append(new Node("a", { className: "participant-edit-action", dataset: {}, children: [] }));
  row.children[0].setAttribute("href", "/Admin/Events/Participant/test/Participants/p1");
  page.append(row);
  return page;
}

function adminPageContext(title, description) {
  const context = new Node("div", { className: "admin-page-context" });
  context.append(new Node("span", { className: "admin-page-title", textContent: title }));
  if (description) context.append(new Node("span", { className: "admin-page-description", textContent: description }));
  return context;
}

const directPath = "/Admin/Events/Participant/test/Participants/p1";
const canonicalListPath = "/Admin/Events/Participants/test?ParticipantSearch=alice&sort=ownership#players";
const failedDirectEntry = process.env.PARTICIPANT_EDIT_FAILURE === "1";
const entries = [{ state: { participantEditOverlay: true, participantEditReturnUrl: `https://example.test${canonicalListPath}` }, url: `https://example.test${directPath}?overlay=1` }];
let entryIndex = 0;
const requestedUrls = [];
let historyReplaceCount = 0;
const replacements = [];
let mainReplacementWasHidden = null;
const windowListeners = {};
const documentListeners = {};
const body = new Node("body", { dataset: { adminParentRefreshError: "Saved; close to refresh." } });
const main = new Node("main", { id: "main-content" });
const initialEditor = editor(false);
body.append(adminPageContext("Participants"));
main.append(initialEditor);
body.append(main);

const setUrl = value => { window.location.href = value instanceof URL ? value.href : new URL(value, "https://example.test").href; };
const history = {
  state: entries[0].state,
  replaceState(state, _title, value) { historyReplaceCount++; entries[entryIndex] = { state, url: new URL(value, window.location.href).href }; this.state = state; setUrl(entries[entryIndex].url); },
  pushState(state, _title, value) { entries.splice(entryIndex + 1); entries.push({ state, url: new URL(value, window.location.href).href }); entryIndex++; this.state = state; setUrl(entries[entryIndex].url); },
  forward() { entryIndex++; this.state = entries[entryIndex].state; setUrl(entries[entryIndex].url); windowListeners.popstate?.forEach(listener => listener({ type: "popstate" })); },
  back() { if (entryIndex === 0) return; entryIndex--; this.state = entries[entryIndex].state; setUrl(entries[entryIndex].url); windowListeners.popstate?.forEach(listener => listener({ type: "popstate" })); }
};

global.CustomEvent = class { constructor(type, options) { this.type = type; Object.assign(this, options); } };
global.HTMLElement = Node;
global.HTMLFormElement = Form;
global.HTMLSelectElement = class extends Node {};
global.HTMLInputElement = class extends Node {};
global.HTMLDialogElement = Dialog;
global.window = {
  innerWidth: 1200,
  scrollX: 0, scrollY: 320, scrollTo(value) { this.lastScroll = value; },
  location: { href: entries[0].url, reload() { this.reloadCount = (this.reloadCount || 0) + 1; }, replace(value) { this.replaced = new URL(value, this.href).href; replacements.push(this.replaced); setUrl(this.replaced); } },
  history,
  fetch: async url => {
    requestedUrls.push(String(url));
    if (failedDirectEntry && String(url).includes("/Participant/test/")) throw new Error("forced editor fetch failure");
    return { ok: true, headers: { get: () => null }, text: async () => String(url).includes("/Participants/test") && !String(url).includes("/Participant/test/") ? "participants" : "overlay-editor" };
  },
  setTimeout: callback => callback(),
  addEventListener(type, listener) { (windowListeners[type] ??= []).push(listener); },
  removeEventListener(type, listener) { windowListeners[type] = (windowListeners[type] || []).filter(item => item !== listener); },
  dispatchEvent(event) { windowListeners[event.type]?.forEach(listener => listener(event)); }
};
global.history = history;
global.document = {
  body,
  activeElement: null,
  addEventListener(type, listener) { (documentListeners[type] ??= []).push(listener); },
  removeEventListener(type, listener) { documentListeners[type] = (documentListeners[type] || []).filter(item => item !== listener); },
  dispatchEvent(event) { documentListeners[event.type]?.forEach(listener => listener(event)); },
  createElement: tag => tag === "dialog" ? new Dialog() : new Node(tag),
  importNode: node => node.cloneNode(true),
  querySelector: selector => selector === "main#main-content" ? main : body.querySelector(selector),
  querySelectorAll: selector => body.querySelectorAll(selector)
};
global.DOMParser = class {
  parseFromString(value) { return { querySelectorAll: () => [], querySelector: selector => value === "participants" && selector === ".event-participants-page" ? participantsPage() : value === "participants" && selector === ".admin-page-context" ? adminPageContext("Participants & signups", "Manage signup settings, capacity, and participants.") : value === "overlay-editor" && selector === "[data-participant-edit-page]" ? editor(true) : null }; }
};

const originalMainReplaceChildren = main.replaceChildren.bind(main);
main.replaceChildren = (...children) => { mainReplacementWasHidden = main.hidden; originalMainReplaceChildren(...children); };

global.FormData = class { constructor(form) { this.entries = form.entries || []; } [Symbol.iterator]() { return this.entries[Symbol.iterator](); } };
require("../../src/Bingo.Web/wwwroot/js/admin-editor-guard.js");
const questionsDialog = new Dialog();
questionsDialog.id = "signup-questions-dialog";
questionsDialog.dataset.signupQuestionsDialog = "";
questionsDialog.append(new Node("div", { dataset: { signupQuestionsContent: "" } }));
body.append(questionsDialog);
require("../../src/Bingo.Web/wwwroot/js/signup-questions-overlay.js");
require("../../src/Bingo.Web/wwwroot/js/event-manage.js");

(async () => {
  for (let index = 0; index < 10; index++) await Promise.resolve();
  if (failedDirectEntry) {
    assert.equal(main.hidden, false, "direct enhancement failure leaves the Participants context visible before navigation");
    assert.equal(document.querySelector(".admin-page-context").hidden, false, "direct enhancement failure leaves the page context visible");
    assert.equal(document.querySelector("dialog#participant-edit-dialog"), null, "direct enhancement failure does not show an error-only dialog");
    assert.equal(replacements.length, 1, "direct enhancement failure replaces the route exactly once");
    assert.equal(new URL(replacements[0]).pathname, directPath, "direct enhancement failure uses the standalone participant edit route");
    assert.equal(new URL(replacements[0]).searchParams.has("overlay"), false, "direct fallback removes only the overlay query");
    assert.equal(historyReplaceCount, 1, "direct enhancement failure does not rewrite history toward Participants");
    return;
  }
  let url = new URL(window.location.href);
  assert.equal(url.pathname, directPath);
  assert.equal(url.searchParams.get("overlay"), "1");
  assert.equal(requestedUrls.length, 2, "direct desktop entry preloads the workspace and one editor fragment");
  assert.ok(requestedUrls.some(value => new URL(value).pathname === canonicalListPath.split("?")[0]), "direct desktop entry requests the Participants workspace");
  assert.ok(requestedUrls.some(value => new URL(value).pathname === directPath && new URL(value).searchParams.get("overlay") === "1"), "direct desktop entry requests the participant editor route");
  assert.equal(mainReplacementWasHidden, true, "Participants workspace replacement stays hidden until ready");
  assert.equal(main.hidden, false, "restored dialog reveals the populated workspace");
  assert.ok(main.querySelector(".event-participants-page [data-participant-id]"), "restored dialog has the Participants workspace behind it");
  assert.equal(document.querySelector(".admin-page-context .admin-page-title")?.textContent, "Participants & signups", "restored dialog keeps the canonical Participants header");
  assert.equal(document.querySelector(".admin-page-context .admin-page-description")?.textContent, "Manage signup settings, capacity, and participants.", "restored dialog keeps the canonical Participants description");
  assert.equal(history.state?.participantEditOverlay, true, "restored dialog keeps its history state");
  const restoredQuestionsTrigger = main.querySelector("[data-signup-questions-trigger='true']");
  assert.equal(restoredQuestionsTrigger.dataset.signupQuestionsTriggerReady, "true", "direct reload restoration binds the shared Questions trigger that did not exist at script startup");
  assert.equal(restoredQuestionsTrigger.listeners.click.length, 1, "restoration binds the Questions trigger once");
  const dialog = document.querySelector("dialog#participant-edit-dialog[open]");
  assert.ok(dialog, "direct desktop entry opens the participant dialog");

  const confirmation = dialog.querySelector("details.participant-restore-confirmation");
  const cancel = confirmation.querySelector("[data-confirmation-cancel]");
  const confirm = confirmation.querySelector("button.admin-button-secondary");
  assert.equal(confirmation.querySelector("summary").classList.contains("admin-button-secondary"), true, "Restore trigger uses the shared secondary action");
  assert.equal(confirmation.querySelector("summary").classList.contains("admin-button-primary"), false, "Restore trigger does not use the primary action");
  assert.equal(confirmation.querySelector("summary").classList.contains("admin-button-accent-outline"), false, "Restore trigger does not use the invented Admin accent-outline action");
  assert.equal(confirmation.querySelector("summary").classList.contains("action-accent-outline"), false, "Restore trigger does not use the legacy accent-outline action");
  assert.equal(confirm.classList.contains("admin-button-secondary"), true, "Restore confirmation uses the shared secondary action");
  assert.equal(confirm.classList.contains("admin-button-primary"), false, "Restore confirmation does not use the primary action");
  assert.equal(confirm.classList.contains("admin-button-accent-outline"), false, "Restore confirmation does not use the invented Admin accent-outline action");
  assert.equal(confirm.classList.contains("action-accent-outline"), false, "Restore confirmation does not use the legacy accent-outline action");
  const revealParticipantConfirmation = (item, label) => {
    item.open = true;
    item.setAttribute("open", "");
    item.dispatch("toggle");
    assert.deepEqual(item.scrollOptions, { block: "nearest" }, `${label} reveal scrolls into view`);
    item.querySelector("[data-confirmation-cancel]").dispatch("click");
    assert.equal(item.open, false, `${label} Cancel closes only the confirmation`);
  };
  revealParticipantConfirmation(dialog.querySelector("details.participant-ownership-confirmation"), "Transfer ownership");
  revealParticipantConfirmation(dialog.querySelector("details.admin-destructive-confirmation"), "Remove");
  confirmation.open = true;
  confirmation.setAttribute("open", "");
  confirmation.dispatch("toggle");
  assert.deepEqual(confirmation.scrollOptions, { block: "nearest" }, "Restore reveal scrolls into view");
  assert.ok(confirmation.querySelector(".event-confirmation-box"), "Restore uses the compact confirmation layer");
  assert.ok(confirm, "Restore confirmation uses the accent-outline action");
  cancel.dispatch("click");
  assert.equal(confirmation.open, false, "dynamic Restore Cancel closes only the confirmation");
  assert.equal(dialog.open, true, "dynamic Restore Cancel retains the participant dialog");
  assert.equal(confirmation.querySelector("summary").focused, true, "Restore Cancel restores focus to its trigger");
  confirmation.open = true;
  confirmation.setAttribute("open", "");
  confirmation.dispatch("keydown", { key: "Escape" });
  assert.equal(confirmation.open, false, "Restore Escape closes only the confirmation");
  assert.equal(dialog.open, true, "Restore Escape retains the participant dialog");

  const beforeSave = requestedUrls.length;
  const priorRow = main.querySelector("[data-participant-id]");
  dialog.querySelector("form").dispatch("submit");
  for (let index = 0; index < 30; index++) await Promise.resolve();
  assert.equal(requestedUrls.length, beforeSave + 2, "successful save fetches its current parent once");
  assert.equal(requestedUrls.at(-1), `https://example.test${canonicalListPath}`, "refresh retains parent filters and sort");
  assert.notEqual(main.querySelector("[data-participant-id]"), priorRow, "parent rows and concurrency fields are replaced");
  assert.equal(dialog.open, true, "refresh keeps editor open");
  assert.deepEqual(window.lastScroll, { left: 0, top: 320 });

  const normalFetch = window.fetch;
  window.fetch = async (url, options) => options?.method === "POST" ? normalFetch(url, options) : { ok: false };
  dialog.querySelector("form").dispatch("submit");
  for (let index = 0; index < 20; index++) await Promise.resolve();
  assert.equal(dialog.querySelector("[data-participant-editor-feedback]").textContent, "Saved; close to refresh.", "refresh failure acknowledges persisted save without suggesting another POST");
  window.fetch = normalFetch;

  let failedSaveRequests = 0;
  window.fetch = async () => { failedSaveRequests++; return { ok: false }; };
  const currentRow = main.querySelector("[data-participant-id]");
  dialog.querySelector("form").dispatch("submit");
  for (let index = 0; index < 10; index++) await Promise.resolve();
  assert.equal(failedSaveRequests, 1, "failed mutation does not fetch the parent");
  assert.equal(main.querySelector("[data-participant-id]"), currentRow);
  window.fetch = normalFetch;

  const form = dialog.querySelector("form");
  form.entries = [["Input.Answer", "Unsaved answer"]];
  const discard = dialog.querySelector("[data-participant-editor-discard]");
  const keep = dialog.querySelector("[data-participant-editor-keep]");
  const beforeGuardFetches = requestedUrls.length;
  dialog.querySelector("#admin-notes").dispatch("submit");
  assert.equal(requestedUrls.length, beforeGuardFetches, "another form cannot save over unsaved answers without a discard decision");
  assert.equal(discard.hidden, false);
  keep.dispatch("click");
  assert.equal(form.entries[0][1], "Unsaved answer");
  history.back();
  assert.equal(dialog.open, true, "dirty Back keeps the editor open");
  assert.equal(new URL(window.location.href).searchParams.get("overlay"), "1");
  assert.equal(discard.hidden, false);
  keep.dispatch("click");
  dialog.querySelector("[data-participant-edit-close]").dispatch("click");
  assert.equal(discard.hidden, false, "Close uses the same discard decision");
  keep.dispatch("click");
  form.entries = [];
  const replacement = new Node("div", { className: "participant-admin-workspace", textContent: "Validation: enter a valid character" });
  document.dispatchEvent({ type: "bingo:content-will-update", detail: { selectors: [".participant-admin-workspace"], form } });
  dialog.querySelector(".participant-admin-workspace").replaceWith(replacement);
  document.dispatchEvent({ type: "bingo:content-updated", detail: { selectors: [".participant-admin-workspace"] } });
  assert.equal(document.querySelector("dialog#participant-edit-dialog[open]"), dialog, "participant validation post keeps the same dialog");
  assert.equal(dialog.querySelector(".participant-admin-workspace").textContent, "Validation: enter a valid character");
  assert.equal(new URL(window.location.href).searchParams.get("overlay"), "1");

  dialog.querySelector("[data-participant-edit-close]").dispatch("click");
  assert.equal(window.location.href, `https://example.test${canonicalListPath}`, "close restores the canonical list URL");
  assert.equal(window.location.reloadCount, 1, "failed parent refresh forces a document reload even at the same fragment-bearing parent URL");
  assert.equal(history.state?.participantEditOverlay, undefined, "close removes participant dialog route state");
  assert.equal(history.state?.participantEditBase, undefined, "close removes the participant base marker");
  assert.equal(history.state?.participantEditReturnUrl, undefined, "close removes the participant return marker");
  assert.equal(document.querySelector("dialog#participant-edit-dialog[open]"), null, "close returns to the participants route");
  assert.ok(main.querySelector(".event-participants-page [data-participant-id]"), "close leaves the populated Participants workspace behind");
  assert.equal(document.querySelector(".admin-page-context .admin-page-title")?.textContent, "Participants & signups", "close leaves the canonical Participants header");

  history.pushState({ participantEditOverlay: true, participantEditReturnUrl: `https://example.test${canonicalListPath}` }, "", `${directPath}?overlay=1`);
  window.dispatchEvent({ type: "popstate" });
  for (let index = 0; index < 10; index++) await Promise.resolve();
  window.innerWidth = 800;
  window.dispatchEvent({ type: "resize" });
  assert.ok(document.querySelector("dialog#participant-edit-dialog[open]"), "narrow resize retains the same modal");
  assert.equal(new URL(window.location.href).searchParams.get("overlay"), "1");
  assert.equal(window.location.replaced, undefined, "resize never navigates away from the editor");
})().catch(error => { console.error(error); process.exitCode = 1; });

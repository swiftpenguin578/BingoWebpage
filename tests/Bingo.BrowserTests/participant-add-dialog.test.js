const assert = require("node:assert/strict");

class Node {
  constructor(tagName, { id = "", className = "", dataset = {}, textContent = "", children = [] } = {}) {
    this.tagName = tagName.toUpperCase();
    this.id = id;
    this.className = className;
    this.dataset = dataset;
    this.textContent = textContent;
    this.children = [];
    this.attributes = {};
    this.listeners = {};
    this.hidden = false;
    this.open = false;
    this.disabled = false;
    this.value = "";
    this.classList = {
      add: (...names) => names.forEach(name => { if (!this.className.split(" ").includes(name)) this.className = (this.className + " " + name).trim(); }),
      remove: (...names) => { this.className = this.className.split(" ").filter(name => name && !names.includes(name)).join(" "); },
      contains: name => this.className.split(" ").includes(name)
    };
    this.append(...children);
  }

  append(...children) { children.flat().forEach(child => { child.parentElement = this; this.children.push(child); }); }
  get childNodes() { return this.children; }
  prepend(...children) { children.flat().forEach(child => { child.parentElement = this; this.children.unshift(child); }); }
  replaceChildren(...children) { this.children = []; this.append(...children); }
  replaceWith(next) { const index = this.parentElement.children.indexOf(this); this.parentElement.children.splice(index, 1, next); next.parentElement = this.parentElement; }
  remove() { this.parentElement?.children.splice(this.parentElement.children.indexOf(this), 1); this.parentElement = null; }
  addEventListener(type, listener) { (this.listeners[type] ??= []).push(listener); }
  dispatch(type, properties = {}) {
    const event = { type, target: this, defaultPrevented: false, preventDefault() { this.defaultPrevented = true; }, stopPropagation() { this.propagationStopped = true; }, ...properties };
    for (const listener of this.listeners[type] ?? []) listener(event);
    return event;
  }
  setAttribute(name, value) {
    this.attributes[name] = String(value);
    if (name === "id") this.id = String(value);
    if (name === "class") this.className = String(value);
    if (name === "value") this.value = String(value);
  }
  getAttribute(name) { if (name === "id") return this.id || null; if (name === "class") return this.className || null; return this.attributes[name] ?? null; }
  removeAttribute(name) { delete this.attributes[name]; }
  focus() { this.focused = true; if (global.document) global.document.activeElement = this; }
  scrollIntoView() {}
  contains(node) { return this === node || this.children.some(child => child.contains(node)); }
  closest(selector) { for (let node = this; node; node = node.parentElement) if (node.matches(selector)) return node; return null; }
  querySelector(selector) { return this.querySelectorAll(selector)[0] ?? null; }
  querySelectorAll(selector) {
    const result = [];
    const visit = node => {
      node.children.forEach(child => {
        if (child.matches(selector)) result.push(child);
        visit(child);
      });
    };
    visit(this);
    return result;
  }
  matches(selector) {
    selector = selector.trim();
    if (selector.includes(",")) return selector.split(",").some(item => this.matches(item));
    if (selector.includes(" ")) {
      const parts = selector.split(/\s+/);
      return this.matches(parts.at(-1)) && this.closest(parts.slice(0, -1).join(" ")) !== null;
    }
    const data = selector.match(/^([^[]*)\[data-([\w-]+)(?:=['"]([^'"]*)['"])?\]$/);
    if (data) {
      const [, prefix, key, value] = data;
      const datasetKey = key.replace(/-([a-z])/g, (_match, character) => character.toUpperCase());
      return (!prefix || this.matches(prefix)) && Object.prototype.hasOwnProperty.call(this.dataset, datasetKey) && (value === undefined || String(this.dataset[datasetKey]) === value);
    }
    const named = selector.match(/^([^[]*)\[name=['"]([^'"]+)['"]\]$/);
    if (named) return (!named[1] || this.matches(named[1])) && this.getAttribute("name") === named[2];
    const open = selector.endsWith("[open]");
    if (open) selector = selector.slice(0, -6);
    const id = selector.match(/^([^.#]+)?#([\w-]+)$/);
    if (id) return (!id[1] || this.tagName === id[1].toUpperCase()) && this.id === id[2] && (!open || this.open);
    const classMatch = selector.match(/^([^.#]+)?\.([\w-]+)$/);
    if (classMatch) return (!classMatch[1] || this.tagName === classMatch[1].toUpperCase()) && this.classList.contains(classMatch[2]) && (!open || this.open);
    if (selector === "input, select, textarea") return ["INPUT", "SELECT", "TEXTAREA"].includes(this.tagName);
    return this.tagName === selector.toUpperCase() && (!open || this.open);
  }
  cloneNode(deep) {
    const copy = this.tagName === "FORM" ? new Form("form") : new Node(this.tagName, { id: this.id, className: this.className, dataset: { ...this.dataset }, textContent: this.textContent });
    copy.attributes = { ...this.attributes };
    copy.hidden = this.hidden;
    copy.open = this.open;
    copy.disabled = this.disabled;
    copy.value = this.value;
    if (this.entries) copy.entries = this.entries.map(entry => [...entry]);
    if (deep) copy.append(...this.children.map(child => child.cloneNode(true)));
    return copy;
  }
}

class Form extends Node {}
class Dialog extends Node {
  constructor() { super("dialog", { id: "participant-add-dialog", className: "admin-route-dialog participant-add-dialog" }); }
  showModal() { if (!body.contains(this)) throw new Error("Detached dialog"); this.open = true; }
  close() { this.open = false; }
}

function filterForm() {
  const form = new Form("form");
  form.action = "/Admin/Events/Participants/test";
  form.dataset.participantFilterForm = "";
  const search = new Node("input", { dataset: { participantSearchInput: "" } });
  search.value = "alice";
  const status = new Node("select");
  status.setAttribute("name", "ParticipantStatus");
  form.append(search, status);
  return form;
}

function parentPage() {
  const page = new Node("section", { className: "event-participants-page", dataset: { eventParticipantsPage: "" } });
  const sortHeader = new Node("th");
  const sort = new Node("a", { dataset: { participantSort: "", sortKey: "participant", sortLabel: "Participant" } });
  sortHeader.append(sort);
  const trigger = new Node("a", { dataset: { participantAddTrigger: "" }, textContent: "Add participant" });
  trigger.setAttribute("href", "/Admin/Events/Participants/test?addParticipant=1&ParticipantSearch=alice&sort=ownership&direction=desc");
  page.append(filterForm(), sortHeader, trigger);
  page.trigger = trigger;
  page.sort = sort;
  return page;
}

function addEditor() {
  const editor = new Node("section", { className: "admin-dialog-page participant-add-dialog-page event-overview-section" });
  const close = new Node("button", { dataset: { participantAddClose: "" } });
  const discard = new Node("div", { dataset: { participantAddDiscard: "" } });
  discard.hidden = true;
  const keep = new Node("button", { dataset: { participantAddKeep: "" } });
  const discardConfirm = new Node("button", { dataset: { participantAddDiscardConfirm: "" } });
  discard.append(keep, discardConfirm);
  const feedback = new Node("p", { dataset: { participantAddFeedback: "" } });
  feedback.hidden = true;
  const form = new Form("form");
  form.action = "/Admin/Events/Participants/test?handler=CreateInternalParticipant";
  form.entries = [["InternalParticipant.AccountAnswers[1].CharacterName", ""]];
  const input = new Node("input");
  input.setAttribute("name", form.entries[0][0]);
  form.append(input);
  editor.append(close, discard, feedback, form);
  editor.form = form;
  editor.close = close;
  editor.discard = discard;
  editor.keep = keep;
  editor.discardConfirm = discardConfirm;
  editor.feedback = feedback;
  return editor;
}

function notice(type, message) {
  const host = new Node("div", { id: "app-notice-region" });
  const toast = new Node("div", { className: "app-toast app-toast-" + type });
  const copy = new Node("div", { className: "app-toast-copy" });
  copy.append(new Node("span", { textContent: message }));
  toast.append(copy);
  host.append(toast);
  return host;
}

class Parser {
  parseFromString(value) {
    return {
      querySelector(selector) {
        if (value === "editor" && selector === ".participant-add-dialog-page") return addEditor();
        if (value === "success" && selector === ".event-participants-page") return parentPage();
        if (value === "success" && selector === "#app-notice-region") return notice("success", "Internal participant created.");
        if (value === "refresh-failed" && selector === "#app-notice-region") return notice("success", "Internal participant created.");
        if (value === "error" && selector === "#app-notice-region") return notice("error", "Capacity is full.");
        return null;
      },
      querySelectorAll() { return []; }
    };
  }
}

const initialUrl = "https://example.test/Admin/Events/Participants/test?ParticipantSearch=alice&sort=ownership&direction=desc";
const windowListeners = {};
const entries = [{ state: null, url: initialUrl }];
let entryIndex = 0;
let outcome = "success";
let pendingResolve = null;
const requested = [];
const setUrl = value => { window.location.href = new URL(value, window.location.href).href; };
const history = {
  state: null,
  replaceState(state, _title, value) { this.state = state; entries[entryIndex] = { state, url: new URL(value, window.location.href).href }; setUrl(entries[entryIndex].url); },
  pushState(state, _title, value) { this.state = state; entries.splice(entryIndex + 1); entries.push({ state, url: new URL(value, window.location.href).href }); entryIndex++; setUrl(entries[entryIndex].url); },
  back() { if (entryIndex === 0) return; entryIndex--; this.state = entries[entryIndex].state; setUrl(entries[entryIndex].url); windowListeners.popstate?.forEach(listener => listener({ type: "popstate" })); },
  forward() { if (entryIndex >= entries.length - 1) return; entryIndex++; this.state = entries[entryIndex].state; setUrl(entries[entryIndex].url); windowListeners.popstate?.forEach(listener => listener({ type: "popstate" })); }
};

const body = new Node("body", { dataset: { adminParentRefreshError: "Changes saved, but the participant list could not refresh. Close this editor to reload the list." } });
const initialPage = parentPage();
const noticeHost = notice("information", "");
body.append(noticeHost, initialPage);

global.HTMLElement = Node;
global.HTMLFormElement = Form;
global.HTMLInputElement = class extends Node {};
global.HTMLSelectElement = class extends Node {};
global.HTMLDialogElement = Dialog;
global.DOMParser = Parser;
global.CustomEvent = class { constructor(type, options) { this.type = type; Object.assign(this, options); } };
global.FormData = class {
  constructor(form) { this.items = (form?.entries || []).map(entry => [...entry]); }
  append(name, value) { this.items.push([name, value]); }
  [Symbol.iterator]() { return this.items[Symbol.iterator](); }
};
global.window = {
  innerWidth: 1200,
  scrollX: 0,
  scrollY: 440,
  location: {
    href: initialUrl,
    origin: "https://example.test",
    assign(value) { this.assigned = new URL(value, this.href).href; setUrl(this.assigned); },
    replace(value) { this.replaced = new URL(value, this.href).href; setUrl(this.replaced); },
    reload() { this.reloadCount = (this.reloadCount || 0) + 1; }
  },
  history,
  setTimeout(callback) { callback(); },
  addEventListener(type, listener) { (windowListeners[type] ??= []).push(listener); },
  removeEventListener(type, listener) { windowListeners[type] = (windowListeners[type] || []).filter(item => item !== listener); },
  dispatchEvent(event) { windowListeners[event.type]?.forEach(listener => listener(event)); },
  scrollTo(value) { this.scrollX = value.left; this.scrollY = value.top; },
  fetch: async (url, options) => {
    requested.push({ url: String(url), method: options?.method || "GET" });
    if (options?.method === "POST" && outcome === "pending") return new Promise(resolve => { pendingResolve = resolve; });
    if (options?.method === "POST") return { ok: true, url: initialUrl + "#players", redirected: true, headers: { get: () => null }, text: async () => outcome };
    return { ok: true, url: String(url), headers: { get: () => null }, text: async () => "editor" };
  }
};
global.history = history;
global.document = {
  body,
  activeElement: null,
  addEventListener(type, listener) { (this.listeners ??= {})[type] ??= []; this.listeners[type].push(listener); },
  removeEventListener(type, listener) { this.listeners[type] = (this.listeners[type] || []).filter(item => item !== listener); },
  dispatchEvent(event) { for (const listener of this.listeners?.[event.type] || []) listener(event); },
  createElement(tag) { return tag === "dialog" ? new Dialog() : new Node(tag); },
  createElementNS(_namespace, tag) { return new Node(tag); },
  importNode(node) { return node.cloneNode(true); },
  querySelector(selector) { return body.querySelector(selector); },
  querySelectorAll(selector) { return body.querySelectorAll(selector); }
};

require("../../src/Bingo.Web/wwwroot/js/admin-editor-guard.js");
require("../../src/Bingo.Web/wwwroot/js/event-manage.js");

const wait = async (count = 12) => { for (let index = 0; index < count; index++) await Promise.resolve(); };
const openAdd = async () => {
  const page = document.querySelector(".event-participants-page");
  const trigger = page.querySelector("[data-participant-add-trigger]");
  trigger.dispatch("click");
  await wait();
  return document.querySelector("#participant-add-dialog");
};

(async () => {
  const page = document.querySelector(".event-participants-page");
  const search = page.querySelector("[data-participant-search-input]");
  const status = page.querySelector("select[name='ParticipantStatus']");
  const staleTriggerHref = page.trigger.getAttribute("href");
  search.value = "current-filter";
  search.dispatch("input");
  status.value = "WaitingList";
  status.dispatch("change");
  page.sort.dispatch("click");
  const currentContext = new URL(window.location.href);
  assert.equal(currentContext.searchParams.get("ParticipantSearch"), "current-filter");
  assert.equal(currentContext.searchParams.get("ParticipantStatus"), "WaitingList");
  assert.equal(currentContext.searchParams.get("sort"), "participant");
  assert.equal(currentContext.searchParams.get("direction"), "asc");

  let dialog = await openAdd();
  assert.ok(dialog?.open, "Add participant opens as a modal");
  assert.ok(requested.some(item => item.method === "GET" && item.url.includes("addParticipant=1")), "Add participant loads its route fragment");
  const addRequest = requested.filter(item => item.method === "GET" && item.url.includes("addParticipant=1")).at(-1);
  assert.equal(new URL(staleTriggerHref, window.location.href).searchParams.get("ParticipantSearch"), "alice", "the rendered trigger retains its original search context");
  assert.equal(new URL(addRequest.url).searchParams.get("ParticipantSearch"), "current-filter", "Add GET uses the current search context");
  assert.equal(new URL(addRequest.url).searchParams.get("ParticipantStatus"), "WaitingList", "Add GET uses the current status context");
  assert.equal(new URL(addRequest.url).searchParams.get("sort"), "participant", "Add GET uses the current sort context");
  assert.equal(new URL(addRequest.url).searchParams.get("direction"), "asc", "Add GET uses the current sort direction");
  dialog.querySelector("[data-participant-add-close]").dispatch("click");
  await wait(2);
  assert.equal(document.querySelector("#participant-add-dialog"), null, "clean Add close returns to the Participants workspace");
  search.value = "alice";
  status.value = "";
  history.replaceState(null, "", initialUrl);
  dialog = await openAdd();

  window.innerWidth = 800;
  window.dispatchEvent({ type: "resize" });
  assert.ok(dialog.open, "Resize keeps the Add participant modal open");
  assert.match(window.location.href, /addParticipant=1/, "Resize keeps the route marker");

  const editor = dialog.querySelector(".participant-add-dialog-page");
  const editorForm = editor.querySelector("form");
  const close = editor.querySelector("[data-participant-add-close]");
  const discard = editor.querySelector("[data-participant-add-discard]");
  const keep = editor.querySelector("[data-participant-add-keep]");
  const discardConfirm = editor.querySelector("[data-participant-add-discard-confirm]");
  editorForm.entries[0][1] = "typed character";
  close.dispatch("click");
  assert.equal(discard.hidden, false, "Dirty close opens the discard choice");
  keep.dispatch("click");
  assert.equal(discard.hidden, true, "Discard cancel keeps the form");
  assert.equal(editorForm.entries[0][1], "typed character", "Discard cancel retains entered values");
  close.dispatch("click");
  discardConfirm.dispatch("click");
  await wait(2);
  assert.equal(document.querySelector("#participant-add-dialog"), null, "Confirmed discard closes the modal");
  assert.doesNotMatch(window.location.href, /addParticipant=1/, "Confirmed discard removes the route marker");

  dialog = await openAdd();
  const failedEditor = dialog.querySelector(".participant-add-dialog-page");
  const failedForm = failedEditor.querySelector("form");
  failedForm.entries[0][1] = "keep on failure";
  assert.ok(dialog.open, "reopened Add participant modal is open");
  outcome = "error";
  failedForm.dispatch("submit", { submitter: null });
  await wait();
  assert.ok(dialog.open, "Failed creation keeps the modal open");
  assert.equal(failedForm.entries[0][1], "keep on failure", "Failed creation retains typed input");
  assert.equal(failedEditor.querySelector("[data-participant-add-feedback]").textContent, "Capacity is full.", "Failed creation shows the rendered server message");

  outcome = "pending";
  failedForm.dispatch("submit", { submitter: null });
  await wait(2);
  assert.equal(requested.filter(item => item.method === "POST").length, 2, "Pending creation sends one request");
  const cancel = dialog.dispatch("cancel");
  assert.equal(cancel.defaultPrevented, true, "Pending creation blocks Escape dismissal");
  pendingResolve({ ok: true, url: initialUrl + "#players", redirected: true, headers: { get: () => null }, text: async () => "error" });
  await wait();
  assert.equal(dialog.open, true, "Pending failure leaves the modal open for retry");

  outcome = "success";
  failedForm.entries[0][1] = "";
  window.scrollY = 515;
  failedForm.dispatch("submit", { submitter: null });
  await wait();
  assert.equal(document.querySelector("#participant-add-dialog"), null, "Successful creation closes the modal");
  assert.doesNotMatch(window.location.href, /addParticipant=1/, "Successful creation returns to the Participants route");
  assert.match(window.location.href, /ParticipantSearch=alice/);
  assert.match(window.location.href, /sort=ownership/);
  assert.equal(document.querySelector("#app-notice-region").querySelectorAll(".app-toast-success").length, 1, "Successful creation renders one success toast");
  assert.equal(window.scrollY, 515, "Successful creation preserves the parent scroll position");
  assert.ok(document.querySelector(".event-participants-page [data-participant-add-trigger]"), "Fresh Participants data rebinds Add participant");

  const exerciseCompletedRefreshFailure = async (dismiss) => {
    dialog = await openAdd();
    const completedEditor = dialog.querySelector(".participant-add-dialog-page");
    const completedForm = completedEditor.querySelector("form");
    completedForm.entries[0][1] = "created once";
    const postsBefore = requested.filter(item => item.method === "POST").length;
    outcome = "refresh-failed";
    completedForm.dispatch("submit", { submitter: null });
    await wait();
    assert.ok(dialog.open, "Refresh failure keeps the completed Add editor available for recovery");
    assert.equal(completedEditor.querySelector("[data-participant-add-feedback]").textContent, body.dataset.adminParentRefreshError, "Refresh failure states that creation succeeded");
    assert.equal(completedEditor.querySelector("[data-participant-add-discard]").hidden, true, "Completed creation clears the dirty state");
    completedForm.dispatch("submit", { submitter: null });
    await wait(2);
    assert.equal(requested.filter(item => item.method === "POST").length, postsBefore + 1, "Completed creation cannot be submitted again");
    const reloadsBefore = window.location.reloadCount || 0;
    dismiss(dialog);
    await wait(2);
    assert.equal(window.location.reloadCount, reloadsBefore + 1, "Completed creation dismissal reloads the canonical Participants route");
    assert.doesNotMatch(window.location.href, /addParticipant=1/, "Recovery removes the Add route marker");
    assert.match(window.location.href, /ParticipantSearch=alice/, "Recovery retains the current search context");
    outcome = "success";
  };

  await exerciseCompletedRefreshFailure(currentDialog => currentDialog.querySelector("[data-participant-add-close]").dispatch("click"));
  await exerciseCompletedRefreshFailure(currentDialog => currentDialog.dispatch("cancel"));
  await exerciseCompletedRefreshFailure(() => history.back());
})().catch(error => { console.error(error); process.exitCode = 1; });

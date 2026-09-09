const assert = require("node:assert/strict");
const fs = require("node:fs");
const path = require("node:path");

const draftHandler = fs.readFileSync(path.join(__dirname, "../../src/Bingo.Web/Pages/Admin/Events/Draft.cshtml.cs"), "utf8");
const draftMarkup = fs.readFileSync(path.join(__dirname, "../../src/Bingo.Web/Pages/Admin/Events/Draft.cshtml"), "utf8");
const eventManage = fs.readFileSync(path.join(__dirname, "../../src/Bingo.Web/wwwroot/js/event-manage.js"), "utf8");
const siteScript = fs.readFileSync(path.join(__dirname, "../../src/Bingo.Web/wwwroot/js/site.js"), "utf8");
const adminStyles = fs.readFileSync(path.join(__dirname, "../../src/Bingo.Web/wwwroot/css/site.transitional.application.css"), "utf8");
const danishResources = fs.readFileSync(path.join(__dirname, "../../src/Bingo.Web/Resources/SharedResource.da.resx"), "utf8");
assert.match(draftHandler, /if \(string\.IsNullOrWhiteSpace\(name\)\) \{ SetStatus\(Localize\("A team name is required\."\), UiMessageType\.Error\);/, "Blank Add team names use a dedicated validation message");
assert.match(danishResources, /<data name="A team name is required\."[^>]*><value>Et holdnavn er påkrævet\.<\/value>/, "Blank Add team names are translated in Danish");
assert.match(draftMarkup, /data-toast-host[\s\S]*data-draft-add-team-dialog/, "Add team opts its native dialog into the toast host owner");
assert.match(draftMarkup, /data-draft-add-team-cancel[\s\S]*data-draft-add-team-confirm/, "Finalized Add confirmation keeps Cancel before Add");
assert.match(siteScript, /dialog\.admin-route-dialog\[open\], dialog\[data-toast-host\]\[open\]/, "Toast host follows the Add native dialog");
assert.match(adminStyles, /draft-page #add-team :is\([^\n]+\)\[hidden\] \{ display: none !important; \}/, "Add hidden controls beat generic grid and inline-flex display rules");
assert.match(adminStyles, /#add-team \.draft-confirmation\[open\] > \.event-confirmation-box \{ position: static;/, "Finalized Add confirmation remains inline");
assert.match(eventManage, /sessionStorage\.setItem\("bingo:pending-toast", JSON\.stringify\(result\)\)[\s\S]*window\.location\.reload\(\)/, "Successful Add carries the server notice through an identical-URL reload");
assert.match(eventManage, /form\.reset\(\)[\s\S]*guard\.initialize\(\)[\s\S]*closeNow\(\)/, "Discard resets the form and guard baseline before closing");

class Node {
  constructor(tagName, { id = "", className = "", dataset = {}, name = "", value = "", textContent = "" } = {}) {
    this.tagName = tagName.toUpperCase();
    this.id = id;
    this.className = className;
    this.dataset = { ...dataset };
    this.name = name;
    this.value = value;
    this.defaultValue = value;
    this.textContent = textContent;
    this.children = [];
    this.parentElement = null;
    this.attributes = {};
    this.listeners = {};
    this.hidden = false;
    this.open = false;
    this.disabled = false;
    this.focused = false;
    this.classList = {
      add: (...names) => names.forEach(name => { if (!this.className.split(" ").includes(name)) this.className = `${this.className} ${name}`.trim(); }),
      remove: (...names) => { this.className = this.className.split(" ").filter(name => name && !names.includes(name)).join(" "); },
      contains: name => this.className.split(" ").includes(name)
    };
  }

  append(...children) { children.flat().filter(Boolean).forEach(child => { child.parentElement = this; this.children.push(child); }); }
  replaceChildren(...children) { this.children.forEach(child => { child.parentElement = null; }); this.children = []; this.append(...children); }
  reset() { this.querySelectorAll("input, select, textarea").forEach(control => { control.value = control.defaultValue ?? ""; }); }
  addEventListener(type, listener) { (this.listeners[type] ??= []).push(listener); }
  dispatch(type, properties = {}) {
    if (type === "click" && !this.disabled) this.focus();
    const event = { type, target: this, defaultPrevented: false, preventDefault() { this.defaultPrevented = true; }, stopPropagation() { this.propagationStopped = true; }, ...properties };
    for (const listener of this.listeners[type] ?? []) listener(event);
    return event;
  }
  setAttribute(name, value) {
    this.attributes[name] = String(value);
    if (name === "id") this.id = String(value);
    if (name === "class") this.className = String(value);
    if (name === "name") this.name = String(value);
    if (name === "value") this.value = String(value);
    if (name === "href") this.href = String(value);
    if (name === "action") this.action = String(value);
    if (name === "method") this.method = String(value);
  }
  getAttribute(name) {
    if (name === "id") return this.id || null;
    if (name === "class") return this.className || null;
    if (name === "name") return this.name || null;
    if (name === "value") return this.value || null;
    if (name === "href") return this.href || null;
    if (name === "action") return this.action || null;
    return this.attributes[name] ?? null;
  }
  removeAttribute(name) { delete this.attributes[name]; }
  hasAttribute(name) { return Object.prototype.hasOwnProperty.call(this.attributes, name); }
  focus() { this.focused = true; if (global.document) global.document.activeElement = this; }
  contains(node) { return this === node || this.children.some(child => child.contains(node)); }
  closest(selector) { for (let node = this; node; node = node.parentElement) if (node.matches(selector)) return node; return null; }
  querySelector(selector) { return this.querySelectorAll(selector)[0] ?? null; }
  querySelectorAll(selector) {
    const result = [];
    const visit = node => node.children.forEach(child => { if (child.matches(selector)) result.push(child); visit(child); });
    visit(this);
    return result;
  }
  matches(selector) {
    selector = selector.trim();
    if (selector === ":modal") return this.open;
    if (selector.includes(",")) return selector.split(",").some(item => this.matches(item));
    if (selector.includes(" ")) {
      const parts = selector.split(/\s+/);
      return this.matches(parts.at(-1)) && this.closest(parts.slice(0, -1).join(" ")) !== null;
    }
    const open = selector.endsWith("[open]");
    if (open) selector = selector.slice(0, -6);
    const tag = selector.match(/^[a-z-]+/i)?.[0];
    if (tag && this.tagName !== tag.toUpperCase()) return false;
    const id = selector.match(/#([\w-]+)/)?.[1];
    if (id && this.id !== id) return false;
    for (const className of selector.matchAll(/\.([\w-]+)/g)) if (!this.classList.contains(className[1])) return false;
    for (const attribute of selector.matchAll(/\[data-([\w-]+)(?:=['"]([^'"]*)['"])?\]/g)) {
      const key = attribute[1].replace(/-([a-z])/g, (_match, letter) => letter.toUpperCase());
      if (!Object.prototype.hasOwnProperty.call(this.dataset, key) || (attribute[2] !== undefined && String(this.dataset[key]) !== attribute[2])) return false;
    }
    const named = selector.match(/\[name=['"]([^'"]+)['"]\]/);
    if (named && this.name !== named[1]) return false;
    return !open || this.open;
  }
  cloneNode(deep) {
    const copy = new Node(this.tagName, { id: this.id, className: this.className, dataset: { ...this.dataset }, name: this.name, value: this.value, textContent: this.textContent });
    copy.hidden = this.hidden;
    copy.open = this.open;
    copy.disabled = this.disabled;
    copy.attributes = { ...this.attributes };
    if (deep) copy.append(...this.children.map(child => child.cloneNode(true)));
    return copy;
  }
}

class Form extends Node {}
class Input extends Node {}
class Select extends Node {}
class Dialog extends Node {
  showModal() { this.open = true; }
  close() { this.open = false; }
}

class FormDataMock {
  constructor(form) {
    this.items = [];
    form?.querySelectorAll("input, select, textarea").forEach(control => {
      if (control.name && !control.disabled) this.items.push([control.name, String(control.value)]);
    });
  }
  append(name, value) { this.items.push([name, String(value)]); }
  get(name) { return this.items.find(item => item[0] === name)?.[1] ?? null; }
  *[Symbol.iterator]() { yield* this.items; }
}

const makeNotice = (type, message) => {
  const host = new Node("div", { id: "app-notice-region" });
  const toast = new Node("div", { className: `app-toast app-toast-${type}` });
  const copy = new Node("div", { className: "app-toast-copy" });
  copy.append(new Node("span", { textContent: message }));
  toast.append(copy);
  host.append(toast);
  return host;
};

const makeAddPage = () => {
  const page = new Node("section", { className: "draft-page" });
  const trigger = new Node("button", { dataset: { draftDialogOpen: "add-team" } });
  const dialog = new Dialog("dialog", { id: "add-team", className: "tile-dialog team-roster-dialog external-team-dialog", dataset: { draftAddTeamDialog: "" } });
  const close = new Node("button", { dataset: { draftAddTeamClose: "" } });
  const discard = new Node("div", { dataset: { draftAddTeamDiscard: "" } });
  discard.hidden = true;
  const keep = new Node("button", { dataset: { draftAddTeamKeep: "" } });
  const discardConfirm = new Node("button", { dataset: { draftAddTeamDiscardConfirm: "" } });
  discard.append(keep, discardConfirm);
  const feedback = new Node("p", { dataset: { draftAddTeamFeedback: "" } });
  feedback.hidden = true;
  const form = new Form("form", { dataset: { draftAddTeamForm: "" } });
  form.action = "https://example.test/Admin/Events/Draft/event-1?handler=AddTeam";
  const name = new Input("input", { name: "name" });
  const formation = new Select("select", { name: "formationType", value: "Preformed" });
  const affiliation = new Input("input", { name: "affiliation" });
  const confirmation = new Node("details", { dataset: { draftAddTeamConfirmation: "" } });
  const review = new Node("summary", { dataset: { draftAddTeamReview: "" } });
  const confirmButton = new Node("button", { dataset: { draftAddTeamConfirm: "" } });
  const cancel = new Node("button", { dataset: { draftAddTeamCancel: "" } });
  const confirmationField = new Input("input", { name: "confirmed", value: "true", dataset: { draftAddTeamConfirmed: "" } });
  confirmationField.disabled = true;
  confirmation.append(review, confirmationField, confirmButton, cancel);
  form.append(name, formation, affiliation, confirmation);
  dialog.append(close, discard, feedback, form);
  page.append(trigger, dialog);
  return { page, trigger, dialog, close, discard, keep, discardConfirm, feedback, form, name, formation, affiliation, confirmation, review, confirmButton, cancel, confirmationField };
};

const makeRosterPage = () => {
  const page = new Node("section", { className: "draft-page" });
  const trigger = new Node("a", { dataset: { draftRosterTrigger: "team-42" } });
  trigger.setAttribute("href", "https://example.test/Admin/Events/Draft/event-1?rosterTeamId=42");
  const dialog = new Dialog("dialog", { id: "team-42", className: "tile-dialog team-roster-dialog" });
  dialog.getBoundingClientRect = () => ({ left: 100, right: 300, top: 100, bottom: 300 });
  const close = new Node("button", { dataset: { draftRosterClose: "" } });
  const confirmation = new Node("details", { className: "draft-confirmation team-role-remove-form" });
  confirmation.open = true;
  confirmation.setAttribute("open", "");
  const confirmationBox = new Node("div", { className: "event-confirmation-box" });
  const cancel = new Node("button", { dataset: { confirmationCancel: "" } });
  confirmationBox.append(cancel);
  confirmation.append(confirmationBox);
  dialog.append(close, confirmation);
  page.append(trigger, dialog);
  return { page, trigger, dialog, close, confirmationBox, cancel };
};

const initial = makeAddPage();
const roster = makeRosterPage();
const body = new Node("body", { dataset: { adminPostError: "The change could not be sent. Check your connection and try again." } });
body.append(makeNotice("information", ""), initial.page, roster.page);
const requested = [];
const toastCalls = [];
const windowListeners = {};
let outcome = "error";
let pendingResolve;
const location = {
  href: "https://example.test/Admin/Events/Draft/event-1",
  origin: "https://example.test",
  reload() { this.reloadCount = (this.reloadCount || 0) + 1; },
  replace(value) { this.replaced = new URL(value, this.href).href; this.href = this.replaced; },
  assign(value) { this.assigned = new URL(value, this.href).href; this.href = this.assigned; }
};
const stored = new Map();

global.HTMLElement = Node;
global.HTMLInputElement = Input;
global.HTMLSelectElement = Select;
global.HTMLDialogElement = Dialog;
global.HTMLFormElement = Form;
global.FormData = FormDataMock;
global.DOMParser = class {
  parseFromString(value) {
    const documentNode = new Node("document");
    if (value === "success") documentNode.append(makeNotice("success", "Night Owls created."));
    if (value === "error") documentNode.append(makeNotice("error", "A team with that name already exists for this event."));
    return documentNode;
  }
};
const history = {
  state: null,
  pushCalls: 0,
  backCalls: 0,
  pushState(state, _title, url) {
    this.state = state;
    this.pushCalls++;
    location.href = new URL(url, location.href).href;
  },
  replaceState(state, _title, url) {
    this.state = state;
    location.href = new URL(url, location.href).href;
  },
  back() {
    this.backCalls++;
    const url = new URL(location.href);
    url.searchParams.delete("rosterTeamId");
    this.state = null;
    location.href = url.href;
    (windowListeners.popstate || []).forEach(listener => listener({ type: "popstate" }));
  },
  forward() {}
};
global.window = {
  innerWidth: 1200,
  location,
  fetch: async (url, options = {}) => {
    requested.push({ url: String(url), method: options.method || "GET", body: options.body });
    if (options.method === "POST" && outcome === "pending") return new Promise(resolve => { pendingResolve = resolve; });
    return { ok: true, url: "https://example.test/Admin/Events/Draft/event-1", text: async () => outcome };
  },
  showBingoToast(message, type) {
    toastCalls.push({ message, type });
    const host = document.querySelector("#app-notice-region");
    host.replaceChildren(new Node("div", { className: `app-toast app-toast-${type || "success"}` }));
  },
  setTimeout: callback => callback(),
  addEventListener(type, listener) { (windowListeners[type] ??= []).push(listener); },
  removeEventListener(type, listener) { windowListeners[type] = (windowListeners[type] || []).filter(item => item !== listener); }
};
global.history = history;
global.sessionStorage = { getItem: key => stored.get(key) ?? null, setItem: (key, value) => stored.set(key, String(value)), removeItem: key => stored.delete(key) };
global.document = {
  body,
  activeElement: null,
  addEventListener() {},
  dispatchEvent() {},
  getElementById: id => body.querySelector(`#${id}`),
  querySelector: selector => body.querySelector(selector),
  querySelectorAll: selector => body.querySelectorAll(selector),
  createElement: tag => tag === "dialog" ? new Dialog() : tag === "form" ? new Form("form") : new Node(tag),
  importNode: node => node.cloneNode(true)
};

require("../../src/Bingo.Web/wwwroot/js/admin-editor-guard.js");
require("../../src/Bingo.Web/wwwroot/js/event-manage.js");

const flush = async () => { for (let index = 0; index < 20; index++) await Promise.resolve(); };
const submitWithConfirmation = async () => {
  initial.review.dispatch("click");
  initial.confirmButton.dispatch("click");
  initial.form.dispatch("submit", { submitter: initial.confirmButton });
  await flush();
};

(async () => {
  roster.trigger.dispatch("click");
  assert.equal(roster.dialog.open, true, "Roster trigger opens the native modal");
  assert.equal(history.pushCalls, 1, "Roster opening pushes its route state");
  assert.match(location.href, /rosterTeamId=42/, "Roster opening adds the roster route parameter");
  roster.dialog.dispatch("click", { target: roster.dialog, clientX: 150, clientY: 150, detail: 1 });
  assert.equal(roster.dialog.open, true, "Clicking inside the roster bounds does not close it");
  roster.dialog.dispatch("click", { target: roster.confirmationBox, clientX: 350, clientY: 350, detail: 1 });
  assert.equal(roster.dialog.open, true, "Clicking the positioned removal confirmation does not close the roster");
  roster.dialog.dispatch("pointerdown", { target: roster.dialog, clientX: 150, clientY: 150 });
  roster.dialog.dispatch("click", { target: roster.dialog, clientX: 350, clientY: 350, detail: 1 });
  assert.equal(roster.dialog.open, true, "A gesture that starts inside and ends outside does not close the roster");
  roster.dialog.dispatch("click", { target: roster.dialog, clientX: 350, clientY: 350, detail: 1 });
  assert.equal(roster.dialog.open, false, "A clean outside click closes the roster");
  assert.equal(history.backCalls, 1, "Outside dismissal uses the existing history close path");
  assert.doesNotMatch(location.href, /rosterTeamId=/, "Outside dismissal removes the roster route parameter");
  assert.equal(roster.trigger.focused, true, "Outside dismissal restores focus to the roster trigger");

  initial.trigger.dispatch("click");
  assert.equal(initial.dialog.open, true, "Add team opens as a native modal");
  assert.equal(body.classList.contains("admin-route-dialog-open"), true, "Opening Add team locks the page behind the native modal");
  (windowListeners.resize || []).forEach(listener => listener({ type: "resize" }));
  assert.equal(body.classList.contains("admin-route-dialog-open"), true, "Roster synchronization preserves the Add team scroll lock");

  initial.name.value = "typed team";
  initial.formation.value = "Drafted";
  initial.affiliation.value = "Clan";
  initial.feedback.textContent = "Old failure";
  initial.feedback.hidden = false;
  initial.close.dispatch("click");
  assert.equal(initial.discard.hidden, false, "Dirty close opens the discard choice");
  initial.keep.dispatch("click");
  assert.equal(initial.name.value, "typed team", "Cancelling discard retains typed values");
  assert.equal(initial.close.focused, true, "Discard Cancel returns focus to the close trigger");
  initial.close.dispatch("click");
  initial.discardConfirm.dispatch("click");
  assert.equal(initial.dialog.open, false, "Confirmed discard closes the modal");
  assert.equal(initial.trigger.focused, true, "Confirmed discard restores trigger focus");
  assert.equal(initial.name.value, "", "Confirmed discard resets the team name");
  assert.equal(initial.formation.value, "Preformed", "Confirmed discard resets the formation");
  assert.equal(initial.affiliation.value, "", "Confirmed discard resets the affiliation");
  assert.equal(initial.feedback.hidden, true, "Confirmed discard clears failure feedback");
  const afterDiscardUnload = { preventDefault() { this.prevented = true; } };
  windowListeners.beforeunload.forEach(listener => listener(afterDiscardUnload));
  assert.equal(afterDiscardUnload.prevented, undefined, "Discarded values do not trigger a navigation prompt");

  initial.trigger.dispatch("click");
  initial.name.value = "failed team";
  initial.confirmButton.dispatch("click");
  initial.form.dispatch("submit", { submitter: initial.confirmButton });
  assert.equal(requested.filter(request => request.method === "POST").length, 0, "A hidden confirmation button cannot submit the form");
  assert.equal(initial.confirmation.open, true, "Finalized Add requires the visible Review decision");
  assert.equal(initial.review.hidden, true, "Review trigger hides while confirmation is open");
  const enterFromField = initial.form.dispatch("keydown", { key: "Enter", target: initial.name });
  assert.equal(enterFromField.defaultPrevented, true, "Enter from an Add field cannot implicitly accept the finalized action");
  initial.form.dispatch("submit", { submitter: initial.confirmButton });
  assert.equal(requested.filter(request => request.method === "POST").length, 0, "Field Enter does not submit the finalized Add");
  initial.dialog.dispatch("keydown", { key: "Escape", target: initial.name });
  assert.equal(initial.confirmation.open, false, "Escape from an Add field closes the confirmation");
  assert.equal(initial.review.focused, true, "Escape from an Add field restores Review focus");
  initial.review.dispatch("click");
  initial.cancel.dispatch("click");
  assert.equal(initial.confirmation.open, false, "Confirmation Cancel closes only the confirmation");
  assert.equal(initial.review.hidden, false, "Confirmation Cancel reveals the review trigger");
  assert.equal(initial.review.focused, true, "Confirmation Cancel restores review focus");

  outcome = "error";
  await submitWithConfirmation();
  assert.equal(requested.filter(request => request.method === "POST").length, 1, "Confirmed Add sends one POST");
  assert.equal(initial.dialog.open, true, "Failed Add keeps the modal open");
  assert.equal(initial.name.value, "failed team", "Failed Add retains typed input");
  assert.equal(initial.feedback.hidden, false, "Failed Add exposes retry feedback");
  assert.equal(toastCalls.at(-1).message, "A team with that name already exists for this event.", "Failed Add shows the rendered server message");
  assert.equal(toastCalls.at(-1).type, "error", "Failed Add shows the rendered error severity");
  assert.equal(initial.confirmationField.disabled, true, "Failed Add requires a fresh visible confirmation");

  outcome = "pending";
  initial.review.dispatch("click");
  initial.confirmButton.dispatch("click");
  initial.form.dispatch("submit", { submitter: initial.confirmButton });
  await flush();
  assert.equal(requested.filter(request => request.method === "POST").length, 2, "Pending Add sends one request");
  initial.form.dispatch("submit", { submitter: initial.confirmButton });
  assert.equal(requested.filter(request => request.method === "POST").length, 2, "Pending Add blocks duplicate submissions");
  const cancel = initial.dialog.dispatch("cancel");
  assert.equal(cancel.defaultPrevented, true, "Pending Add blocks Escape dismissal");
  pendingResolve({ ok: true, url: "https://example.test/Admin/Events/Draft/event-1", text: async () => "error" });
  await flush();
  assert.equal(initial.dialog.open, true, "Pending failure keeps the modal open");

  outcome = "success";
  await submitWithConfirmation();
  assert.equal(location.reloadCount, 1, "Successful Add performs one real reload at the current Draft URL");
  assert.equal(location.replaced, undefined, "Successful Add does not fabricate a second navigation response");
  assert.equal(requested.filter(request => request.method === "POST").length, 3, "Successful Add is submitted once");
  assert.equal(requested.at(-1).body.get("confirmed"), "true", "Successful Add sends confirmation only after the visible action");
  assert.deepEqual(JSON.parse(sessionStorage.getItem("bingo:pending-toast")), { message: "Night Owls created.", type: "success" }, "Successful Add hands the rendered server notice to the existing pending-toast owner");
  assert.equal(document.querySelector("#app-notice-region").querySelectorAll(".app-toast-success").length, 0, "Reload mock does not invent a success toast");
  const beforeUnload = { preventDefault() { this.prevented = true; } };
  windowListeners.beforeunload.forEach(listener => listener(beforeUnload));
  assert.equal(beforeUnload.prevented, undefined, "Completed Add does not trigger an unsaved-change prompt");
  initial.form.dispatch("submit", { submitter: initial.confirmButton });
  await flush();
  assert.equal(requested.filter(request => request.method === "POST").length, 3, "Completed Add cannot be resubmitted");
})().catch(error => { console.error(error); process.exitCode = 1; });

const assert = require("node:assert/strict");
const fs = require("node:fs");
const path = require("node:path");

const eventManage = fs.readFileSync(path.join(__dirname, "../../src/Bingo.Web/wwwroot/js/event-manage.js"), "utf8");
const rosterMarkup = fs.readFileSync(path.join(__dirname, "../../src/Bingo.Web/Pages/Admin/Events/Draft.cshtml"), "utf8");
const adminLayout = fs.readFileSync(path.join(__dirname, "../../src/Bingo.Web/Pages/Shared/_AdminLayout.cshtml"), "utf8");
assert.match(rosterMarkup, /data-draft-roster-discard[\s\S]*data-draft-roster-keep[\s\S]*data-draft-roster-discard-confirm/, "Roster dialogs expose the shared discard choice");
assert.match(rosterMarkup, /team-roster-dialog" data-preserve-post-dialog data-toast-host/, "Roster dialogs opt into the shared toast owner");
assert.match(adminLayout, /data-admin-roster-parent-refresh-error/, "Roster refresh failure has a localized message owner");
assert.match(eventManage, /draftRosterFormReady/, "Roster forms use the shared pending transport guard");

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

  append(...children) {
    children.flat().filter(Boolean).forEach(child => {
      if (child.parentElement) {
        const index = child.parentElement.children.indexOf(child);
        if (index >= 0) child.parentElement.children.splice(index, 1);
      }
      child.parentElement = this;
      this.children.push(child);
    });
  }
  replaceChildren(...children) { this.children.forEach(child => { child.parentElement = null; }); this.children = []; this.append(...children); }
  replaceWith(next) { const index = this.parentElement.children.indexOf(this); this.parentElement.children.splice(index, 1, next); next.parentElement = this.parentElement; this.parentElement = null; }
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
    if (name === "open") this.open = true;
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
  removeAttribute(name) { delete this.attributes[name]; if (name === "open") this.open = false; }
  hasAttribute(name) { return name === "open" ? this.open : Object.prototype.hasOwnProperty.call(this.attributes, name); }
  focus() { this.focused = true; if (global.document) global.document.activeElement = this; }
  click() { return this.dispatch("click"); }
  contains(node) { return this === node || this.children.some(child => child.contains(node)); }
  closest(selector) { for (let node = this; node; node = node.parentElement) if (node.matches(selector)) return node; return null; }
  querySelector(selector) {
    if (selector === ":scope > summary") return this.children.find(child => child.matches("summary")) ?? null;
    return this.querySelectorAll(selector)[0] ?? null;
  }
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
    if (selector.includes(" ")) return this.matches(selector.split(/\s+/).at(-1));
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
    const copy = this.tagName === "FORM" ? new Form("form") : this.tagName === "DIALOG" ? new Dialog("dialog") : this.tagName === "INPUT" ? new Input("input") : this.tagName === "SELECT" ? new Select("select") : new Node(this.tagName);
    copy.id = this.id;
    copy.className = this.className;
    copy.dataset = { ...this.dataset };
    copy.name = this.name;
    copy.value = this.value;
    copy.defaultValue = this.defaultValue;
    copy.textContent = this.textContent;
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

let emptyFileLastModified = 0;
class FormDataMock {
  constructor(form) {
    this.items = [];
    form?.querySelectorAll("input, select, textarea").forEach(control => {
      if (control.name && !control.disabled) {
        const value = control.type === "file" && control.value?.name === "" && Number(control.value?.size) === 0
          ? { ...control.value, lastModified: ++emptyFileLastModified }
          : control.value;
        this.items.push([control.name, value]);
      }
    });
  }
  append(name, value) { this.items.push([name, value]); }
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

const addFormControl = (form, tag, name, value = "") => {
  const control = tag === "select" ? new Select(tag, { name, value }) : new Input(tag, { name, value });
  form.append(control);
  return control;
};

const makeParticipantSection = label => {
  const section = new Node("section", { className: "draft-participant-section", dataset: { draftParticipantSection: "", participantGroup: "", noMatches: "No matching participants." }, textContent: label });
  const controls = new Node("div", { className: "admin-events-directory-controls" });
  const search = new Input("input", { dataset: { participantSearch: "" } });
  controls.append(search);
  const tableWrap = new Node("div", { className: "admin-events-table-wrap" });
  const scroll = new Node("div", { className: "admin-events-table-scroll" });
  const table = new Node("table");
  const thead = new Node("thead");
  const header = new Node("th");
  const sort = new Node("button", { dataset: { participantSort: "", sortType: "text", sortIndex: "0" } });
  header.append(sort);
  thead.append(header);
  table.append(thead, new Node("tbody"));
  scroll.append(table);
  tableWrap.append(scroll);
  section.append(controls, tableWrap);
  return { section, search, sort, scroll };
};

const makeRosterDialog = teamId => {
  const dialog = new Dialog("dialog", { id: `team-${teamId}`, className: "tile-dialog team-roster-dialog" });
  dialog.getBoundingClientRect = () => ({ left: 100, right: 300, top: 100, bottom: 300 });
  const close = new Node("button", { dataset: { draftRosterClose: "" } });
  const discard = new Node("div", { dataset: { draftRosterDiscard: "" } });
  discard.hidden = true;
  const keep = new Node("button", { dataset: { draftRosterKeep: "" } });
  const discardConfirm = new Node("button", { dataset: { draftRosterDiscardConfirm: "" } });
  discard.append(keep, discardConfirm);
  const feedback = new Node("p", { dataset: { draftRosterFeedback: "" } });
  feedback.hidden = true;
  dialog.append(close, discard, feedback);

  const details = new Node("details", { className: "team-roster-section" });
  const update = new Form("form");
  update.action = `https://example.test/Admin/Events/Draft/event-1?handler=UpdateTeam&rosterTeamId=${teamId}`;
  const teamName = addFormControl(update, "input", "name", `Team ${teamId}`);
  addFormControl(update, "input", "affiliation", "Clan");
  details.append(update);
  dialog.append(details);

  const role = new Form("form", { dataset: { draftRoleForm: "" } });
  role.action = `https://example.test/Admin/Events/Draft/event-1?handler=ChangeRole&rosterTeamId=${teamId}`;
  addFormControl(role, "input", "membershipId", "member-1");
  addFormControl(role, "input", "membershipVersion", "3");
  addFormControl(role, "select", "role", "Participant");
  role.querySelector("select").defaultValue = "Participant";
  dialog.append(role);

  const removeDetails = new Node("details", { className: "draft-confirmation team-role-remove-form" });
  const removeSummary = new Node("summary");
  const remove = new Form("form");
  remove.action = `https://example.test/Admin/Events/Draft/event-1?handler=RemoveMember&rosterTeamId=${teamId}`;
  addFormControl(remove, "input", "membershipId", "member-1");
  removeDetails.append(removeSummary, remove);
  dialog.append(removeDetails);

  const moveDetails = new Node("details", { className: "team-member-move" });
  const move = new Form("form");
  move.action = `https://example.test/Admin/Events/Draft/event-1?handler=MoveMember&rosterTeamId=${teamId}`;
  addFormControl(move, "input", "membershipId", "member-1");
  addFormControl(move, "select", "targetTeamId", "target-2");
  moveDetails.append(move);
  dialog.append(moveDetails);

  const external = new Form("form");
  external.action = `https://example.test/Admin/Events/Draft/event-1?handler=AddExternalMember&rosterTeamId=${teamId}`;
  addFormControl(external, "input", "name", "External");
  addFormControl(external, "input", "ehb", "10");
  addFormControl(external, "textarea", "additionalAccounts", "");
  addFormControl(external, "select", "role", "Participant");
  dialog.append(external);

  const credential = new Node("a");
  credential.setAttribute("href", `https://example.test/Admin/Accounts/Create?eventId=event-1&teamId=${teamId}`);
  dialog.append(credential);

  const preview = new Form("form");
  preview.action = `https://example.test/Admin/Events/Draft/event-1?handler=PreviewRosterCsv&rosterTeamId=${teamId}`;
  const csv = addFormControl(preview, "input", "csv", "");
  csv.type = "file";
  csv.value = { name: "", size: 0, type: "application/octet-stream", lastModified: 0 };
  csv.defaultValue = csv.value;
  dialog.append(preview);

  const apply = new Form("form");
  apply.action = `https://example.test/Admin/Events/Draft/event-1?handler=ApplyRosterCsv&rosterTeamId=${teamId}`;
  addFormControl(apply, "input", "previewToken", "preview-1");
  dialog.append(apply);

  const addMember = new Form("form");
  addMember.action = `https://example.test/Admin/Events/Draft/event-1?handler=AddMember&rosterTeamId=${teamId}`;
  addFormControl(addMember, "select", "participantId", "participant-1");
  addFormControl(addMember, "select", "role", "Participant");
  dialog.append(addMember);

  return { dialog, close, discard, keep, discardConfirm, feedback, update, teamName, role, remove, move, external, credential, preview, csv, apply, addMember };
};

const makeDraftPage = (participantLabel = "initial pool") => {
  const page = new Node("section", { className: "draft-page", dataset: { draftPage: "" } });
  const triggerA = new Node("a", { dataset: { draftRosterTrigger: "team-42" } });
  triggerA.setAttribute("href", "https://example.test/Admin/Events/Draft/event-1?rosterTeamId=42");
  const triggerB = new Node("a", { dataset: { draftRosterTrigger: "team-43" } });
  triggerB.setAttribute("href", "https://example.test/Admin/Events/Draft/event-1?rosterTeamId=43");
  const rosterA = makeRosterDialog("42");
  const rosterB = makeRosterDialog("43");
  page.append(triggerA, triggerB, rosterA.dialog, rosterB.dialog);
  const participant = makeParticipantSection(participantLabel);
  return { page, participant: participant.section, participantState: participant, triggerA, triggerB, rosterA, rosterB };
};

let responseMode = "error";
let pendingResolve = null;
const requested = [];
const toastCalls = [];
const windowListeners = {};
const entries = [{ state: null, url: "https://example.test/Admin/Events/Draft/event-1" }];
let entryIndex = 0;
const location = {
  href: entries[0].url,
  origin: "https://example.test",
  reload() { this.reloadCount = (this.reloadCount || 0) + 1; },
  replace(value) { this.replaced = new URL(value, this.href).href; this.href = this.replaced; },
  assign(value) { this.assigned = new URL(value, this.href).href; this.href = this.assigned; }
};
const syncLocation = url => { location.href = new URL(url, location.href).href; };
const history = {
  state: null,
  pushState(state, _title, url) { this.state = state; entries.splice(entryIndex + 1); entries.push({ state, url: new URL(url, location.href).href }); entryIndex++; syncLocation(entries[entryIndex].url); },
  replaceState(state, _title, url) { this.state = state; entries[entryIndex] = { state, url: new URL(url, location.href).href }; syncLocation(entries[entryIndex].url); },
  back() { if (entryIndex === 0) return; entryIndex--; this.state = entries[entryIndex].state; syncLocation(entries[entryIndex].url); (windowListeners.popstate || []).forEach(listener => listener({ type: "popstate" })); },
  forward() { if (entryIndex >= entries.length - 1) return; entryIndex++; this.state = entries[entryIndex].state; syncLocation(entries[entryIndex].url); (windowListeners.popstate || []).forEach(listener => listener({ type: "popstate" })); }
};
const resetHistory = url => { entries.splice(0, entries.length, { state: null, url }); entryIndex = 0; history.state = null; syncLocation(url); };

const initial = makeDraftPage();
const body = new Node("body", { dataset: {
  adminPostError: "The change could not be sent. Check your connection and try again.",
  adminRosterSaved: "Changes saved.",
  adminRosterParentRefreshError: "Changes saved, but the roster could not refresh. Close this editor to reload the roster."
} });
body.append(makeNotice("information", ""), initial.page, initial.participant);

global.HTMLElement = Node;
global.HTMLInputElement = Input;
global.HTMLSelectElement = Select;
global.HTMLDialogElement = Dialog;
global.HTMLFormElement = Form;
global.FormData = FormDataMock;
global.DOMParser = class {
  parseFromString(value) {
    const parsed = new Node("document");
    if (responseMode === "success") {
      const next = makeDraftPage("fresh participant pool");
      parsed.append(makeNotice("success", "Roster saved."), next.page, next.participant);
    }
    else if (responseMode === "saved-no-refresh") parsed.append(makeNotice("success", "Roster saved."));
    else if (responseMode === "error") parsed.append(makeNotice("error", "The roster change was rejected."));
    else if (responseMode === "unexpected200") parsed.append(new Node("main", { textContent: "Sign in" }));
    else if (responseMode === "preview") {
      const next = makeDraftPage("preview participant pool");
      parsed.append(next.page, next.participant);
    }
    else if (responseMode === "csv-error") parsed.append(new Node("section", { className: "draft-csv-callout-error", textContent: "CSV preview needs correction." }));
    return parsed;
  }
};
global.window = {
  innerWidth: 1200,
  location,
  history,
  fetch: async (url, options = {}) => {
    requested.push({ url: String(url), method: options.method || "GET", body: options.body });
    if (options.method === "POST" && responseMode === "pending") return new Promise(resolve => { pendingResolve = resolve; });
    return { ok: true, url: location.href, text: async () => responseMode };
  },
  showBingoToast(message, type) {
    toastCalls.push({ message, type });
    document.querySelector("#app-notice-region")?.replaceChildren(new Node("div", { className: `app-toast app-toast-${type || "success"}` }));
  },
  setTimeout: callback => callback(),
  addEventListener(type, listener) { (windowListeners[type] ??= []).push(listener); },
  removeEventListener(type, listener) { windowListeners[type] = (windowListeners[type] || []).filter(item => item !== listener); },
  scrollX: 0,
  scrollY: 240,
  scrollTo(value) { this.scrollX = value.left; this.scrollY = value.top; }
};
global.history = history;
global.document = {
  body,
  activeElement: null,
  addEventListener(type, listener) { (documentListeners[type] ??= []).push(listener); },
  dispatchEvent(event) { if (event.type === "bingo:content-updated") (documentListeners[event.type] || []).forEach(listener => listener(event)); },
  removeEventListener(type, listener) { documentListeners[type] = (documentListeners[type] || []).filter(item => item !== listener); },
  getElementById: id => body.querySelector(`#${id}`),
  querySelector: selector => body.querySelector(selector),
  querySelectorAll: selector => body.querySelectorAll(selector),
  createElement: tag => tag === "dialog" ? new Dialog("dialog") : tag === "form" ? new Form("form") : new Node(tag),
  importNode: node => node.cloneNode(true)
};
const documentListeners = { "bingo:content-updated": [] };
global.CustomEvent = class { constructor(type, options) { this.type = type; Object.assign(this, options); } };
global.sessionStorage = { getItem: () => null, setItem() {}, removeItem() {} };

require("../../src/Bingo.Web/wwwroot/js/admin-editor-guard.js");
require("../../src/Bingo.Web/wwwroot/js/event-manage.js");

const flush = async () => { for (let index = 0; index < 30; index++) await Promise.resolve(); };

(async () => {
  for (const dialog of [initial.rosterA.dialog, initial.rosterB.dialog]) assert.equal(dialog.querySelectorAll("form").filter(form => form.dataset.draftRosterFormReady === "true").length, 8, "Every roster form receives the enhanced submit path");

  initial.triggerA.dispatch("click");
  assert.equal(initial.rosterA.dialog.open, true, "Roster trigger opens the modal");
  assert.match(location.href, /rosterTeamId=42/, "Roster trigger preserves the route marker");

  initial.rosterA.close.dispatch("click");
  assert.equal(initial.rosterA.dialog.open, false, "clean roster close ignores changing empty-file timestamps");
  initial.triggerA.dispatch("click");

  initial.rosterA.teamName.value = "Changed team";
  initial.rosterA.close.dispatch("click");
  assert.equal(initial.rosterA.discard.hidden, false, "Dirty Close opens the shared discard choice");
  initial.rosterA.keep.dispatch("click");
  assert.equal(initial.rosterA.teamName.value, "Changed team", "Keep retains typed values");
  assert.equal(initial.rosterA.close.focused, true, "Keep restores focus to the close trigger");

  initial.rosterA.csv.value = { name: "roster.csv", size: 24, type: "text/csv", lastModified: 1 };
  initial.rosterA.close.dispatch("click");
  assert.equal(initial.rosterA.discard.hidden, false, "Selecting a CSV is treated as a dirty roster form");
  initial.rosterA.discardConfirm.dispatch("click");
  assert.equal(initial.rosterA.csv.value.name, "", "Discard resets a selected CSV to its baseline");

  initial.triggerA.dispatch("click");
  initial.rosterA.teamName.value = "Outside edit";
  initial.rosterA.dialog.dispatch("click", { target: initial.rosterA.dialog, clientX: 350, clientY: 350, detail: 1 });
  assert.equal(initial.rosterA.discard.hidden, false, "Dirty outside dismissal opens the shared discard choice");
  initial.rosterA.discardConfirm.dispatch("click");
  assert.equal(initial.rosterA.dialog.open, false, "Discard closes the roster");
  assert.equal(initial.rosterA.teamName.value, "Team 42", "Discard resets the baseline fields");

  window.innerWidth = 600;
  initial.triggerA.dispatch("click");
  assert.equal(initial.rosterA.dialog.open, true, "Narrow roster triggers still use the native modal");
  initial.rosterA.teamName.value = "Narrow edit";
  const narrowKeepNavigation = initial.rosterA.dialog.dispatch("click", { target: initial.rosterA.credential });
  assert.equal(narrowKeepNavigation.defaultPrevented, true, "Dirty in-app links are guarded at narrow widths");
  assert.equal(initial.rosterA.discard.hidden, false, "Dirty in-app links open the shared discard choice");
  const assignedBeforeKeep = location.assigned;
  initial.rosterA.keep.dispatch("click");
  assert.equal(location.assigned, assignedBeforeKeep, "Keep cancels guarded in-app navigation");
  initial.rosterA.dialog.dispatch("click", { target: initial.rosterA.credential });
  initial.rosterA.discardConfirm.dispatch("click");
  assert.match(location.assigned, /Admin\/Accounts\/Create\?eventId=event-1&teamId=42/, "Discard allows the guarded in-app navigation");
  initial.rosterA.close.dispatch("click");
  resetHistory("https://example.test/Admin/Events/Draft/event-1");
  window.innerWidth = 1200;

  initial.triggerA.dispatch("click");
  initial.rosterA.teamName.value = "Back edit";
  initial.rosterA.teamName.focus();
  history.back();
  assert.equal(initial.rosterA.dialog.open, true, "Back with dirty values restores the roster before prompting");
  assert.equal(initial.rosterA.discard.hidden, false, "Back with dirty values opens the discard choice");
  initial.rosterA.keep.dispatch("click");
  assert.equal(initial.rosterA.teamName.value, "Back edit", "Keep after Back retains the edit");
  history.back();
  assert.equal(initial.rosterA.discard.hidden, false, "A second Back still restores history before prompting");
  initial.rosterA.discardConfirm.dispatch("click");

  initial.triggerA.dispatch("click");
  initial.rosterA.teamName.value = "Switch edit";
  const switchEvent = initial.triggerB.dispatch("click");
  assert.equal(switchEvent.defaultPrevented, true, "Switching teams is guarded while the current roster is dirty");
  assert.equal(initial.rosterA.discard.hidden, false, "Switching teams opens the discard choice");
  initial.rosterA.keep.dispatch("click");
  assert.equal(initial.rosterA.dialog.open, true, "Keep cancels the team switch");
  initial.triggerB.dispatch("click");
  initial.rosterA.discardConfirm.dispatch("click");
  assert.match(location.href, /rosterTeamId=43/, "Discarded switch navigates to the selected roster");
  assert.equal(initial.rosterB.dialog.open, true, "Discarded switch opens the selected roster");

  responseMode = "pending";
  const roleValue = initial.rosterB.role.querySelector("select[name='role']");
  roleValue.value = "Captain";
  initial.rosterB.role.dispatch("submit", { submitter: null });
  await flush();
  assert.equal(requested.filter(request => request.method === "POST").length, 1, "Pending roster save sends one POST");
  assert.equal(requested.at(-1).body.get("role"), "Captain", "Pending save serializes the selected role before controls are disabled");
  initial.rosterB.role.dispatch("submit", { submitter: null });
  assert.equal(requested.filter(request => request.method === "POST").length, 1, "Pending roster save blocks duplicate submission");
  initial.rosterB.close.dispatch("click");
  assert.equal(initial.rosterB.dialog.open, true, "Pending roster save blocks Close");
  initial.rosterB.dialog.dispatch("click", { target: initial.rosterB.dialog, clientX: 350, clientY: 350, detail: 1 });
  assert.equal(initial.rosterB.dialog.open, true, "Pending roster save blocks outside dismissal");
  const assignedBeforePendingNavigation = location.assigned;
  const pendingNavigation = initial.rosterB.dialog.dispatch("click", { target: initial.rosterB.credential });
  assert.equal(pendingNavigation.defaultPrevented, true, "Pending roster save blocks in-app navigation");
  assert.equal(location.assigned, assignedBeforePendingNavigation, "Pending roster save leaves navigation untouched");
  responseMode = "error";
  pendingResolve({ ok: true, url: location.href, text: async () => "error" });
  await flush();
  assert.equal(initial.rosterB.role.querySelector("select[name='role']").disabled, false, "Failed pending save restores controls");
  assert.equal(initial.rosterB.role.querySelector("select[name='role']").value, "Captain", "Failed pending save retains submitted input");
  assert.equal(initial.rosterB.feedback.hidden, false, "Failed pending save exposes visible recovery feedback");
  assert.equal(toastCalls.at(-1).type, "error", "Failed pending save reports an error outcome");

  initial.rosterB.teamName.value = "Other form edit";
  initial.rosterB.role.dispatch("submit", { submitter: null });
  assert.equal(initial.rosterB.discard.hidden, false, "Cross-form discard opens before submitting the edited form");
  initial.rosterB.discardConfirm.dispatch("click");
  await flush();
  assert.equal(initial.rosterB.role.querySelector("select[name='role']").value, "Captain", "Cross-form discard retains the submitted form values after a failed save");
  initial.rosterB.close.dispatch("click");
  assert.equal(initial.rosterB.discard.hidden, false, "A failed cross-form save leaves the submitted form dirty");
  initial.rosterB.discardConfirm.dispatch("click");
  initial.triggerB.dispatch("click");

  responseMode = "unexpected200";
  initial.rosterB.teamName.value = "Unexpected response";
  initial.rosterB.update.dispatch("submit", { submitter: null });
  await flush();
  assert.equal(initial.rosterB.teamName.value, "Unexpected response", "Unexpected successful HTTP responses retain the edited input");
  assert.equal(initial.rosterB.dialog._draftRosterState.completed, false, "Unexpected successful HTTP responses do not mark the mutation complete");
  assert.equal(toastCalls.at(-1).type, "error", "Unexpected successful HTTP responses report a retryable error");
  initial.rosterB.close.dispatch("click");
  assert.equal(initial.rosterB.discard.hidden, false, "Unexpected successful HTTP responses leave the form dirty for retry");
  initial.rosterB.discardConfirm.dispatch("click");
  initial.triggerB.dispatch("click");

  responseMode = "saved-no-refresh";
  initial.rosterB.role.querySelector("select[name='role']").value = "Captain";
  initial.rosterB.teamName.value = "Saved but stale";
  assert.equal(initial.rosterB.update.listeners.submit?.length, 1, "Update form remains wired after a failed save");
  const postsBeforeRefreshFailure = requested.filter(request => request.method === "POST").length;
  assert.equal(initial.rosterB.dialog._draftRosterState.guard.pending, false, "Failed save has released the roster pending lock");
  assert.equal(initial.rosterB.dialog._draftRosterState.completed, false, "Failed save leaves the roster available for retry");
  initial.rosterB.update.dispatch("submit", { submitter: null });
  assert.equal(initial.rosterB.discard.hidden, false, "A second form cannot silently discard the failed form's retained input");
  initial.rosterB.discardConfirm.dispatch("click");
  await flush();
  assert.equal(requested.filter(request => request.method === "POST").length, postsBeforeRefreshFailure + 1, "Saved response sends the update request");
  assert.equal(initial.rosterB.dialog.open, true, "A saved response with a failed refresh keeps the editor present");
  assert.equal(toastCalls.at(-1).type, "warning", "Saved but stale feedback uses warning severity");
  const postsAfterRefreshFailure = requested.filter(request => request.method === "POST").length;
  initial.rosterB.update.dispatch("submit", { submitter: null });
  assert.equal(requested.filter(request => request.method === "POST").length, postsAfterRefreshFailure, "Saved but stale editor cannot resubmit the completed mutation");
  initial.rosterB.close.dispatch("click");
  assert.equal(location.reloadCount, 1, "Saved but stale recovery offers a real reload on close");

  initial.triggerA.dispatch("click");
  responseMode = "success";
  initial.participantState.search.value = "captain";
  initial.participantState.sort.dataset.direction = "desc";
  initial.participantState.scroll.scrollLeft = 37;
  initial.participantState.scroll.scrollTop = 88;
  initial.rosterA.teamName.value = "Fresh team";
  initial.rosterA.update.dispatch("submit", { submitter: null });
  await flush();
  assert.equal(requested.filter(request => request.method === "POST").length, postsAfterRefreshFailure + 1, "Successful roster save sends one POST");
  assert.equal(document.querySelector("[data-draft-page]").querySelector("#team-42")?.open, true, `Successful roster save keeps the route-backed roster open after refresh (${location.href})`);
  const refreshedParticipants = document.querySelector("[data-draft-participant-section]");
  assert.equal(refreshedParticipants.textContent, "fresh participant pool", "Successful roster save refreshes the sibling participant pool");
  assert.equal(refreshedParticipants.querySelector("[data-participant-search]").value, "captain", "Participant filtering input survives roster refresh");
  assert.equal(refreshedParticipants.querySelector("[data-participant-sort]").dataset.direction, "desc", "Participant sort direction survives roster refresh");
  assert.equal(refreshedParticipants.querySelector(".admin-events-table-scroll").scrollLeft, 37, "Participant table horizontal scroll survives roster refresh");
  assert.equal(refreshedParticipants.querySelector(".admin-events-table-scroll").scrollTop, 88, "Participant table vertical scroll survives roster refresh");
  assert.equal(document.querySelector("#app-notice-region").parentElement.id, "team-42", "The shared toast host remains connected to the replacement native modal");
  assert.equal(toastCalls.at(-1).message, "Roster saved.", "Successful roster save shows the server outcome once");
  assert.equal(toastCalls.at(-1).type, "success", "Successful roster save reports success severity");
  const beforeUnload = { preventDefault() { this.prevented = true; } };
  (windowListeners.beforeunload || []).forEach(listener => listener(beforeUnload));
  assert.equal(beforeUnload.prevented, undefined, "Completed roster save does not leave a false dirty prompt");
})().catch(error => { console.error(error); process.exitCode = 1; });

const assert = require("node:assert/strict");
const fs = require("node:fs");
const vm = require("node:vm");

class Node {
  constructor(tagName, { id, className, dataset = {}, textContent = "", value = "", name = "", type = "", href = "" } = {}) {
    this.tagName = tagName.toUpperCase();
    this.id = id;
    this.className = className ?? "";
    this.dataset = { ...dataset };
    this.textContent = textContent;
    this.value = value;
    this.name = name;
    this.type = type;
    this.href = href;
    this.action = "";
    this.method = "";
    this.children = [];
    this.attributes = {};
    this.listeners = {};
    this.open = false;
    this.hidden = false;
    this.disabled = false;
    this.focused = false;
    this.options = [];
    this.classList = {
      add: (...names) => names.forEach(name => { if (!this.className.split(" ").includes(name)) this.className = `${this.className} ${name}`.trim(); }),
      remove: (...names) => { this.className = this.className.split(" ").filter(name => name && !names.includes(name)).join(" "); },
      contains: name => this.className.split(" ").includes(name)
    };
  }

  append(...children) { children.flat().filter(Boolean).forEach(child => { if (child.parentElement) child.parentElement.children = child.parentElement.children.filter(item => item !== child); child.parentElement = this; this.children.push(child); }); }
  replaceWith(next) { const parent = this.parentElement; const index = parent.children.indexOf(this); parent.children[index] = next; next.parentElement = parent; this.parentElement = null; }
  replaceChildren(...children) { this.children.forEach(child => { child.parentElement = null; }); this.children = []; this.append(...children); }
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
    if (name === "name") this.name = String(value);
    if (name === "type") this.type = String(value);
    if (name === "value") this.value = String(value);
    if (name === "href") this.href = String(value);
    if (name === "action") this.action = String(value);
    if (name === "method") this.method = String(value);
    if (name.startsWith("data-")) this.dataset[name.slice(5).replace(/-([a-z])/g, (_m, c) => c.toUpperCase())] = String(value);
  }
  getAttribute(name) {
    if (name === "id") return this.id ?? null;
    if (name === "class") return this.className || null;
    if (name === "href") return this.href || null;
    if (name === "action") return this.action || this.attributes[name] || null;
    if (name === "method") return this.method || this.attributes[name] || null;
    if (name === "name") return this.name || this.attributes[name] || null;
    if (name.startsWith("data-")) return this.dataset[name.slice(5).replace(/-([a-z])/g, (_m, c) => c.toUpperCase())] ?? null;
    return this.attributes[name] ?? null;
  }
  removeAttribute(name) { delete this.attributes[name]; if (name === "action") this.action = ""; }
  focus() { this.focused = true; }
  scrollIntoView(options) { this.scrollOptions = options; }
  contains(node) { return this === node || this.children.some(child => child.contains(node)); }
  querySelector(selector) { return this.querySelectorAll(selector)[0] ?? null; }
  querySelectorAll(selector) {
    const result = [];
    const visit = node => { node.children.forEach(child => { if (child.matches(selector)) result.push(child); visit(child); }); };
    visit(this);
    return result;
  }
  closest(selector) { for (let node = this; node; node = node.parentElement) if (node.matches(selector)) return node; return null; }
  matches(selector) {
    if (selector.includes(",")) return selector.split(",").some(item => this.matches(item.trim()));
    const attr = selector.match(/^([^.#\[]*)\[([^=\]]+)(?:=['"]([^'"]*)['"])?\]$/);
    if (attr) {
      const [, prefix, name, value] = attr;
      const actual = name.startsWith("data-") ? this.getAttribute(name) : this.getAttribute(name);
      return (!prefix || this.matches(prefix)) && actual !== null && (value === undefined || actual === value);
    }
    const id = selector.match(/^([^.#\[]+)?#([\w-]+)$/);
    if (id) return (!id[1] || this.tagName === id[1].toUpperCase()) && this.id === id[2];
    const tagClass = selector.match(/^([^.#\[]+)?\.([\w-]+)$/);
    if (tagClass) return (!tagClass[1] || this.tagName === tagClass[1].toUpperCase()) && this.classList.contains(tagClass[2]);
    if (selector.startsWith(".")) return this.classList.contains(selector.slice(1));
    if (selector.startsWith("#")) return this.id === selector.slice(1);
    if (selector === "input, select") return this.tagName === "INPUT" || this.tagName === "SELECT";
    return this.tagName === selector.toUpperCase();
  }
  cloneNode(deep) {
    const clone = this instanceof Form ? new Form(this.tagName, { id: this.id, className: this.className, dataset: { ...this.dataset }, textContent: this.textContent, value: this.value, name: this.name, type: this.type, href: this.href }) : new Node(this.tagName, { id: this.id, className: this.className, dataset: { ...this.dataset }, textContent: this.textContent, value: this.value, name: this.name, type: this.type, href: this.href });
    clone.hidden = this.hidden;
    clone.disabled = this.disabled;
    clone.action = this.action;
    clone.method = this.method;
    clone.attributes = { ...this.attributes };
    clone.options = this.options.map(option => ({ ...option }));
    if (deep) clone.append(...this.children.map(child => child.cloneNode(true)));
    return clone;
  }
}

class Form extends Node {
  submit() { this.nativeSubmissions = (this.nativeSubmissions || 0) + 1; }
  requestSubmit() { this.dispatch("submit"); }
}

class Dialog extends Node {
  constructor() { super("dialog"); }
  showModal() { this.open = true; }
  close() { this.open = false; }
}

class FormDataMock {
  constructor(form) {
    this.values = [];
    if (form) form.querySelectorAll("input, select, textarea").forEach(control => { if (control.name && !control.disabled) this.set(control.name, control.value); });
  }
  set(name, value) { this.values = this.values.filter(entry => entry[0] !== name); this.values.push([name, String(value)]); }
  append(name, value) { this.values.push([name, String(value)]); }
  get(name) { return this.values.find(entry => entry[0] === name)?.[1] ?? null; }
  *[Symbol.iterator]() { yield* this.values; }
}

const body = new Node("body");
const main = new Node("main", { id: "main-content" });
const manageTrigger = new Node("a", { dataset: { accountManageTrigger: "true" }, href: "/Admin/Accounts/Manage/emergency-1" });
const createTrigger = new Node("a", { dataset: { accountCreateTrigger: "true" }, href: "/Admin/Accounts/Create" });
body.append(main);

const entries = [{ state: {}, url: "https://example.test/Admin/Accounts/Manage/emergency-1?overlay=1" }];
let entryIndex = 0;
const requested = [];
const assigned = [];
const toastCalls = [];
const queuedTimers = [];
const windowListeners = {};
const setUrl = value => { window.location.href = new URL(value, window.location.href).href; };
const history = {
  state: entries[0].state,
  pushState(state, _title, value) { entries.splice(entryIndex + 1); entries.push({ state, url: new URL(value, window.location.href).href }); entryIndex++; this.state = state; setUrl(entries[entryIndex].url); },
  replaceState(state, _title, value) { entries[entryIndex] = { state, url: new URL(value, window.location.href).href }; this.state = state; setUrl(entries[entryIndex].url); },
  back() { if (entryIndex === 0) return; entryIndex--; this.state = entries[entryIndex].state; setUrl(entries[entryIndex].url); windowListeners.popstate?.forEach(listener => listener({ type: "popstate" })); }
};

const addGuard = page => {
  const box = new Node("div", { dataset: { accountEditorDiscard: "" } });
  box.hidden = true;
  box.append(new Node("button", { dataset: { accountEditorKeep: "" } }), new Node("button", { dataset: { accountEditorDiscardConfirm: "" } }));
  page.append(box, new Node("p", { dataset: { accountEditorFeedback: "" } }));
  return page;
};
const manageRoot = (redirect = false) => {
  if (redirect) return new Node("section");
  const page = new Node("section", { className: "admin-account-dialog-page", dataset: { accountDialogPage: "true", accountDialogKind: "manage", accountManagePage: "true", accountDialogOverlay: "true" } });
  page.setAttribute("data-account-status-message", "Generated a one-time setup or reset link. It expires after 60 minutes.");
  page.append(new Node("button", { dataset: { accountDialogClose: "true" } }));
  page.append(new Node("input", { name: "__RequestVerificationToken", value: "manage-token" }));
  page.append(new Node("button", { dataset: { accountFinalAction: "true", accountHandler: "GenerateResetLink", accountConfirmationTitle: "Generate a reset link?", accountConfirmationSupport: "The link is shown once and expires after 60 minutes.", accountConfirmationLabel: "Generate reset link", accountConfirmationStyle: "secondary" } }));
  page.append(new Node("button", { dataset: { accountFinalAction: "true", accountHandler: "Disable", accountConfirmationTitle: "Disable this account?", accountConfirmationSupport: "Existing authorization and sessions will be protected by the account state change.", accountConfirmationLabel: "Disable account", accountConfirmationStyle: "danger", accountConfirmationReason: "true", accountConfirmationReasonLabel: "Disable reason" } }));
  page.append(new Node("button", { dataset: { accountEmergencyAction: "true", accountHandler: "GenerateEmergencyLink", accountConfirmationTitle: "Generate a setup or reset link?", accountConfirmationSupport: "The link is shown once and expires after 60 minutes.", accountConfirmationLabel: "Generate link", accountConfirmationStyle: "secondary" } }));
  page.append(new Node("button", { dataset: { accountEmergencyAction: "true", accountHandler: "DisableEmergency", accountConfirmationTitle: "Disable this emergency credential?", accountConfirmationSupport: "The assigned team will no longer be able to use this fallback login.", accountConfirmationLabel: "Disable credential", accountConfirmationStyle: "danger" } }));
  return addGuard(page);
};

const createRoot = () => {
  const page = new Node("section", { dataset: { accountDialogPage: "true", accountDialogKind: "create", accountCreatePage: "true", accountDialogOverlay: "true" } });
  page.append(new Node("button", { dataset: { accountDialogClose: "true" } }));
  const scope = new Form("form", { className: "admin-account-option-form", action: "/Admin/Accounts/Create", method: "get" });
  scope.append(new Node("select", { name: "eventId", value: "" }));
  const create = new Form("form", { action: "/Admin/Accounts/Create", method: "post" });
  create.append(new Node("input", { name: "Input.Username", value: "test-account" }), new Node("input", { name: "Input.EventId", value: "event-1" }));
  page.append(scope, create);
  return addGuard(page);
};

const indexDirectory = new Node("section", { className: "admin-accounts-page" });
indexDirectory.append(manageTrigger, createTrigger);
const indexSuccess = new Node("document");
const successToast = new Node("div", { className: "app-toast app-toast-success", dataset: { transientToast: "true" } });
const successCopy = new Node("div", { className: "app-toast-copy" });
successCopy.append(new Node("span", { textContent: "Created disabled emergency credential test-account. Create a setup link before enabling it." }));
successToast.append(successCopy);
indexSuccess.append(indexDirectory, successToast);

const markers = {
  "index-document": indexDirectory,
  manage: manageRoot(),
  "manage-refreshed": manageRoot(),
  create: createRoot(),
  "index-success": indexSuccess,
  index: manageRoot(true)
};

const response = (marker, { redirected = false, url = window.location.href } = {}) => ({ ok: true, redirected, url, text: async () => marker });

const fetchMock = async (url, options = {}) => {
  const requestUrl = new URL(url, window.location.href);
  requested.push({ url: requestUrl.href, method: options.method ?? "GET", body: options.body });
  if (requestUrl.pathname.endsWith("/Index") && !options.method) return response("index-document", { url: requestUrl.href });
  if (requestUrl.pathname.endsWith("/Create") && !options.method) return response("create", { url: requestUrl.href });
  if (requestUrl.pathname.endsWith("/Create") && options.method === "POST") return response("index-success", { redirected: true, url: "https://example.test/Admin/Accounts" });
  if (requestUrl.pathname.includes("/Manage/") && options.method === "POST" && requestUrl.searchParams.get("handler") === "GenerateResetLink") return response("manage-refreshed", { redirected: true, url: "https://example.test/Admin/Accounts/Manage/emergency-1?overlay=1" });
  if (requestUrl.pathname.includes("/Manage/") && options.method === "POST" && requestUrl.searchParams.get("handler") === "GenerateEmergencyLink") return response("manage-refreshed", { redirected: true, url: "https://example.test/Admin/Accounts/Manage/emergency-1?overlay=1" });
  if (requestUrl.pathname.includes("/Manage/") && options.method === "POST") return response("index", { redirected: true, url: "https://example.test/Admin/Accounts/Index" });
  if (requestUrl.pathname.includes("/Manage/")) return response("manage", { url: requestUrl.href });
  return response("index", { url: requestUrl.href });
};

const parser = marker => {
  const documentNode = new Node("document");
  if (markers[marker]) documentNode.append(markers[marker].cloneNode(true));
  return documentNode;
};

global.HTMLElement = Node;
global.HTMLAnchorElement = Node;
global.HTMLDialogElement = Dialog;
global.HTMLDetailsElement = Node;
global.HTMLFormElement = Form;
global.HTMLInputElement = Node;
global.HTMLSelectElement = Node;
global.FormData = FormDataMock;
global.window = {
  innerWidth: 1200,
  location: { href: entries[0].url, assign(value) { assigned.push(new URL(value, this.href).href); setUrl(value); } },
  fetch: fetchMock,
  showBingoToast(message, type) { toastCalls.push({ message, type }); },
  history,
  setTimeout: callback => callback(),
  addEventListener(type, listener) { (windowListeners[type] ??= []).push(listener); }
};
global.history = history;
global.document = {
  body,
  addEventListener() {},
  dispatchEvent() {},
  createElement: tag => tag === "dialog" ? new Dialog() : tag === "form" ? new Form("form") : new Node(tag),
  importNode: node => node.cloneNode(true),
  querySelector: selector => selector === "main#main-content" ? main : body.querySelector(selector),
  querySelectorAll: selector => body.querySelectorAll(selector)
};
global.DOMParser = class { parseFromString(value) { return parser(value); } };

global.CustomEvent = class { constructor(type, data) { this.type = type; Object.assign(this, data); } };
vm.runInThisContext(fs.readFileSync("src/Bingo.Web/wwwroot/js/admin-editor-guard.js", "utf8"));
vm.runInThisContext(fs.readFileSync("src/Bingo.Web/wwwroot/js/account-manage-dialog.js", "utf8"));

const flush = async () => { for (let index = 0; index < 24; index++) await Promise.resolve(); };
const outer = () => body.querySelector("dialog.account-manage-route-dialog");
let lastConfirmation;
const nested = () => (lastConfirmation = body.querySelector(".admin-account-inline-confirmation") || lastConfirmation);

(async () => {
  await flush();
  assert.equal(main.querySelector(".admin-accounts-page") !== null, true, "direct overlay hydration restores the real Accounts directory");
  assert.equal(outer().open, true, "direct desktop Manage overlay opens over the hydrated directory");
  assert.match(requested[0].url, /\/Admin\/Accounts\/Index$/);
  assert.match(requested[1].url, /\/Admin\/Accounts\/Manage\/emergency-1\?overlay=1$/);
  outer().querySelector("[data-account-dialog-close]").dispatch("click");
  assert.equal(new URL(window.location.href).pathname, "/Admin/Accounts/Index", "direct overlay close returns to Accounts");
  assert.equal(outer().open, false);

  const liveCreateTrigger = main.querySelector("[data-account-create-trigger]");
  liveCreateTrigger.dispatch("click");
  await flush();
  assert.equal(outer().open, true, "desktop Create opens the shared native route dialog");
  assert.equal(outer().querySelector("[data-account-dialog-kind]").getAttribute("data-account-dialog-kind"), "create");
  assert.equal(outer().querySelectorAll("[data-account-dialog-page]").length, 1, "Create response inserts one component root");
  assert.match(requested.at(-1).url, /\/Admin\/Accounts\/Create\?overlay=1$/);
  const username = outer().querySelector("input[name='Input.Username']");
  username.value = "unsaved-name";
  const scopeSelect = outer().querySelector("select[name='eventId']");
  const beforeScope = requested.length;
  scopeSelect.value = "event-2";
  scopeSelect.dispatch("change");
  assert.equal(requested.length, beforeScope, "scope changes do not send before other credential edits are resolved");
  assert.equal(scopeSelect.value, "event-1", "cancelled scope retains loaded event");
  assert.equal(outer().querySelector("[data-account-editor-discard]").hidden, false);
  outer().querySelector("[data-account-editor-keep]").dispatch("click");
  assert.equal(username.value, "unsaved-name", "scope cancel preserves credential input");
  let settleSave;
  window.fetch = () => new Promise(resolve => { settleSave = resolve; });
  const createFormPending = outer().querySelectorAll("form")[1];
  createFormPending.dispatch("submit");
  const firstPending = settleSave;
  createFormPending.dispatch("submit");
  assert.equal(settleSave, firstPending, "pending submission cannot send twice");
  window.innerWidth = 600;
  windowListeners.resize?.forEach(listener => listener());
  outer().querySelector("[data-account-dialog-close]").dispatch("click");
  assert.equal(outer().open, true, "pending resize and close keep editor present");
  settleSave({ ok: false });
  await flush();
  assert.equal(username.value, "unsaved-name", "failed save retains input");
  assert.equal(username.disabled, false, "failed save restores retry controls");
  window.fetch = fetchMock;
  outer().querySelector("[data-account-dialog-close]").dispatch("click");
  assert.equal(outer().querySelector("[data-account-editor-discard]").hidden, false, "dirty close asks for explicit discard");
  outer().querySelector("[data-account-editor-keep]").dispatch("click");
  username.value = "test-account";

  outer().querySelector("[data-account-dialog-close]").dispatch("click");
  assert.equal(outer().open, false, "Create close returns to Accounts");

  liveCreateTrigger.dispatch("click");
  await flush();
  const createToastCount = toastCalls.length;
  window.setTimeout = callback => queuedTimers.push(callback);
  outer().querySelectorAll("form")[1].dispatch("submit");
  await flush();
  assert.equal(outer().open, false, "successful Create closes the dialog without document navigation");
  assert.equal(new URL(window.location.href).pathname, "/Admin/Accounts/Index", "successful Create settles on the canonical Accounts directory");
  assert.equal(assigned.length, 0, "successful Create uses in-place directory hydration");
  assert.equal(toastCalls.length, createToastCount, "successful Create waits until the dialog teardown completes before showing its toast");
  while (queuedTimers.length) queuedTimers.shift()();
  assert.equal(toastCalls.length, createToastCount + 1, "successful Create emits one toast");
  assert.equal(toastCalls.some(call => call.message === "Could not create the emergency credential. Please try again."), false, "successful Create does not emit the generic error toast");
  assert.deepEqual(toastCalls.at(-1), {
    message: "Created disabled emergency credential test-account. Create a setup link before enabling it.",
    type: "success"
  }, "enhanced Create shows the server success toast after closing");
  window.setTimeout = callback => callback();

  const liveManageTrigger = main.querySelector("[data-account-manage-trigger]");
  liveManageTrigger.dispatch("click");
  await flush();
  const emergency = outer().querySelector("[data-account-emergency-action]");
  const triggerClass = emergency.className;
  emergency.dispatch("click");
  assert.equal(nested().hidden, false, "Emergency action reveals an inline confirmation");
  assert.equal(emergency.hidden, true, "active action trigger is hidden");
  assert.deepEqual(nested().scrollOptions, { block: "nearest", behavior: "instant" }, "revealed confirmation is brought into view without animation");
  assert.equal(outer().open, true, "The Manage dialog remains open behind confirmation");
  assert.equal(emergency.className, triggerClass, "Opening confirmation does not alter the closed trigger class");
  nested().dispatch("keydown", { key: "Escape" });
  assert.equal(nested().hidden, true, "nested Escape closes only confirmation");
  assert.equal(outer().open, true);
  assert.equal(emergency.focused, true, "nested close restores trigger focus");
  assert.equal(emergency.hidden, false, "Escape restores the trigger");

  emergency.dispatch("click");
  nested().dispatch("click", { target: nested() });
  assert.equal(nested().hidden, true, "nested backdrop closes only confirmation");
  emergency.dispatch("click");
  nested().querySelector("[data-account-confirmation-cancel]").dispatch("click");
  assert.equal(nested().hidden, true, "Cancel closes only confirmation");

  emergency.dispatch("click");
  const confirmationForm = nested().querySelector("form");
  const confirmationSubmit = confirmationForm.dispatch("submit");
  await flush();
  assert.equal(confirmationSubmit.defaultPrevented, true, "Emergency confirm uses fetch instead of navigation");
  assert.equal(requested.at(-2).method, "POST");
  assert.match(requested.at(-2).url, /handler=GenerateEmergencyLink/);
  assert.equal([...requested.at(-2).body].some(([name, value]) => name === "overlay" && value === "1"), true, "Emergency confirm posts overlay=1");
  assert.equal(nested().hidden, true);
  assert.equal(outer().open, true, "rendered Manage response stays in the outer dialog");
  assert.equal(outer().querySelector("[data-account-dialog-page]").getAttribute("data-account-dialog-overlay"), "true", "only the overlay component is inserted after mutation");

  const reset = outer().querySelector("[data-account-final-action]");
  reset.dispatch("click");
  assert.equal(nested().hidden, false, "website reset action uses the shared inline confirmation");
  assert.equal(nested().querySelector("[data-account-confirmation-submit]").textContent, "Generate reset link");
  nested().querySelector("form").dispatch("submit");
  await flush();
  assert.equal(nested().hidden, true, "website reset confirmation closes after the rendered response");
  assert.equal(outer().open, true, "website reset response stays in the outer dialog");
  assert.deepEqual(toastCalls.at(-1), { message: "Generated a one-time setup or reset link. It expires after 60 minutes.", type: "success" }, "enhanced link success uses the shared toast once after replacement");

  const websiteDisable = outer().querySelectorAll("[data-account-final-action]")[1];
  websiteDisable.dispatch("click");
  assert.equal(nested().hidden, false, "website disable action uses the shared inline confirmation");
  assert.equal(nested().querySelector("textarea").required, true, "website disable confirmation keeps the required reason field");
  assert.equal(nested().querySelector("textarea").name, "Reason");
  const reason = nested().querySelector("textarea");
  reason.value = "Retained reason";
  outer().querySelector("[data-account-dialog-close]").dispatch("click");
  assert.equal(nested().hidden, true, "discard decision temporarily hides action confirmation");
  assert.equal(websiteDisable.hidden, false, "discard restores the suspended trigger");
  outer().querySelector("[data-account-editor-keep]").dispatch("click");
  assert.equal(nested().hidden, false, "cancelling discard restores the reason form visibly");
  assert.equal(nested().querySelector("textarea"), reason, "cancel retains the original confirmation form");
  assert.equal(reason.value, "Retained reason");
  assert.equal(websiteDisable.hidden, true, "discard Cancel hides the restored action trigger");
  reason.value = "";

  nested().querySelector("[data-account-confirmation-cancel]").dispatch("click");
  assert.equal(nested().hidden, true, "website disable Cancel closes only the confirmation");
  assert.equal(websiteDisable.hidden, false, "Cancel restores the trigger");

  const disable = outer().querySelectorAll("[data-account-emergency-action]")[1];
  disable.dispatch("click");
  nested().querySelector("form").dispatch("submit");
  await flush();
  assert.equal(outer().open, false, "redirect to Index closes the outer dialog");
  assert.equal(assigned.at(-1), "https://example.test/Admin/Accounts/Index");

  const standaloneCreate = createRoot();
  standaloneCreate.dataset.accountDialogOverlay = "false";
  main.replaceChildren(standaloneCreate);
  history.replaceState({}, "", "https://example.test/Admin/Accounts/Create?eventId=event-1");
  vm.runInThisContext(fs.readFileSync("src/Bingo.Web/wwwroot/js/account-manage-dialog.js", "utf8"));
  const standaloneUsername = standaloneCreate.querySelector("input[name='Input.Username']");
  const standaloneScope = standaloneCreate.querySelector("form.admin-account-option-form");
  const standaloneEvent = standaloneScope.querySelector("select[name='eventId']");
  standaloneUsername.value = "keep-this-credential";
  standaloneEvent.value = "event-2";
  assert.equal(standaloneScope.dispatch("submit").defaultPrevented, true);
  assert.equal(standaloneScope.nativeSubmissions || 0, 0, "standalone Load teams cannot discard typed details without consent");
  standaloneCreate.querySelector("[data-account-editor-keep]").dispatch("click");
  assert.equal(standaloneUsername.value, "keep-this-credential");
  assert.equal(standaloneEvent.value, "event-1", "standalone scope cancellation restores loaded selection");
  standaloneEvent.value = "event-2";
  standaloneScope.dispatch("submit");
  standaloneCreate.querySelector("[data-account-editor-discard-confirm]").dispatch("click");
  assert.equal(standaloneScope.nativeSubmissions, 1, "confirmed standalone scope uses its native route");
  assert.equal(standaloneEvent.value, "event-2");

})().catch(error => { console.error(error); process.exitCode = 1; });

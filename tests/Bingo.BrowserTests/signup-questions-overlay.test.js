const assert = require("node:assert/strict");
const fs = require("node:fs");
const vm = require("node:vm");

class Element {
  constructor({ tagName = "div", href = "", children = [] } = {}) {
    this.tagName = tagName.toUpperCase();
    this.href = href;
    this.children = children;
    this.childNodes = children;
    this.dataset = {};
    this.listeners = {};
    this.attributes = {};
    this.hidden = false;
    this.classList = { add: (...names) => names.forEach((name) => this.classes?.add(name)), remove: (...names) => names.forEach((name) => this.classes?.delete(name)) };
    children.forEach((child) => { child.parentElement = this; });
  }

  addEventListener(type, listener) { (this.listeners[type] ??= []).push(listener); }
  dispatch(type, extras = {}) {
    const event = { type, target: this, defaultPrevented: false, propagationStopped: false, preventDefault() { this.defaultPrevented = true; }, stopPropagation() { this.propagationStopped = true; }, ...extras };
    let current = this;
    while (current) {
      event.currentTarget = current;
      for (const listener of current.listeners[type] ?? []) listener(event);
      if (event.propagationStopped) break;
      current = current.parentElement;
    }
    return event;
  }
  querySelector(selector) { return this.querySelectorAll(selector)[0] ?? null; }
  querySelectorAll(selector) { return this.selectors?.[selector] ?? []; }
  focus(options) { this.focused = true; this.focusOptions = options; }
  setAttribute(name, value) { this.attributes[name] = String(value); }
  getAttribute(name) { return this.attributes[name] ?? null; }
  removeAttribute(name) { delete this.attributes[name]; }
  replaceChildren(...children) { this.children = children; this.childNodes = children; children.forEach((child) => { child.parentElement = this; }); }
  replaceWith(replacement) {
    const parent = this.parentElement;
    if (!parent) return;
    const index = parent.children.indexOf(this);
    if (index < 0) return;
    parent.children[index] = replacement;
    parent.childNodes = parent.children;
    replacement.parentElement = parent;
  }
}

class DialogElement extends Element {
  constructor() { super(); this.open = false; }
  showModal() { this.open = true; }
  close() { this.open = false; }
}

class SelectElement extends Element {}

class DetailsElement extends Element {
  constructor(summary) { super({ tagName: "details", children: [summary] }); this.open = false; }
  querySelectorAll(selector) { return selector === "summary" ? this.children : super.querySelectorAll(selector); }
  setAttribute(name, value) { super.setAttribute(name, value); if (name === "open") this.open = true; }
  removeAttribute(name) { super.removeAttribute(name); if (name === "open") this.open = false; }
}

const dialog = new DialogElement();
const content = new Element();
content.parentElement = dialog;
const closeButton = new Element();
const trigger = new Element({ href: "/Admin/Events/Questions/test" });
trigger.dataset.signupQuestionsTrigger = "true";
dialog.selectors = { "[data-signup-questions-content]": [content], "[data-signup-questions-close]": [closeButton] };
content.querySelector = (selector) => {
  if (selector === "[data-signup-questions-editor]") return content.children[0] ?? null;
  return content.children[0]?.querySelector(selector) ?? null;
};

const editor = new Element();
const editorCloseButton = new Element();
const mutationForm = new Element();
mutationForm.entries = [["Input.Label", ""]];
const destructiveForm = new Element();
destructiveForm.entries = [["questionId", "question-1"], ["confirmed", "true"], ["expectedAnswerCount", "1"], ["expectedEventRegistrationReleaseCount", "0"], ["expectedQuestionVersion", "4"]];
destructiveForm.method = "post";
destructiveForm.action = "/Admin/Events/Questions/test?handler=Deactivate";
destructiveForm.dataset.signupQuestionConfirmation = "true";
destructiveForm.dataset.signupQuestionConfirmationTitle = "Delete question?";
destructiveForm.dataset.signupQuestionConfirmationDescription = "This deletes the question and its answers.";
destructiveForm.dataset.signupQuestionConfirmationImpact = "Current impact: 1 saved answer, 0 event registrations released, question version 4.";
destructiveForm.dataset.signupQuestionConfirmationAction = "Delete question";
const destructiveInputs = Object.fromEntries(destructiveForm.entries.filter(([name]) => name !== "confirmed").map(([name, value]) => {
  const input = new Element({ tagName: "input" });
  input.value = value;
  return [name, input];
}));
destructiveForm.selectors = Object.fromEntries(Object.entries(destructiveInputs).map(([name, input]) => [`input[name='${name}']`, [input]]));
const destructiveButton = new Element();
destructiveButton.parentElement = destructiveForm;
const freshDestructiveForm = new Element();
freshDestructiveForm.dataset.signupQuestionConfirmation = "true";
freshDestructiveForm.dataset.signupQuestionConfirmationTitle = "Delete question after reload?";
freshDestructiveForm.dataset.signupQuestionConfirmationDescription = "The question changed on the server.";
freshDestructiveForm.dataset.signupQuestionConfirmationImpact = "Current impact: 2 saved answers, 1 event registration released, question version 5.";
freshDestructiveForm.dataset.signupQuestionConfirmationAction = "Delete question again";
const freshInputs = Object.fromEntries([["questionId", "question-1"], ["expectedAnswerCount", "2"], ["expectedEventRegistrationReleaseCount", "1"], ["expectedQuestionVersion", "5"]].map(([name, value]) => {
  const input = new Element({ tagName: "input" });
  input.value = value;
  return [name, input];
}));
freshDestructiveForm.selectors = Object.fromEntries(Object.entries(freshInputs).map(([name, input]) => [`input[name='${name}']`, [input]]));
const otherForm = new Element();
otherForm.entries = [["Input.Label", ""]];
const discard = new Element();
discard.hidden = true;
const keep = new Element();
const discardConfirm = new Element();
discard.selectors = { "[data-signup-questions-keep]": [keep] };
const failure = new Element();
failure.hidden = true;
mutationForm.method = "post";
mutationForm.action = "/Admin/Events/Questions/test?handler=Add";
editor.selectors = { "[data-signup-questions-close]": [editorCloseButton], form: [mutationForm, destructiveForm, otherForm], "[data-signup-questions-discard]": [discard], "[data-signup-questions-keep]": [keep], "[data-signup-questions-discard-confirm]": [discardConfirm], "[data-signup-questions-feedback]": [failure] };
editor.querySelectorAll = (selector) => {
  return editor.selectors?.[selector] ?? [];
};
editor.dataset.signupQuestionCount = "4";
editor.setAttribute("aria-labelledby", "signup-questions-dialog-title");
editor.setAttribute("aria-describedby", "signup-questions-dialog-description");
const feedback = new Element();
const notice = new Element({ children: [] });
const noticeRegion = new Element({ children: [] });
const questionSummary = new Element();
questionSummary.dataset.signupQuestionSummary = "{0} active questions configured.";
questionSummary.textContent = "4 active questions configured.";
let validationError = false;
let staleImpactResponse = false;
const parsedDocument = { querySelectorAll(selector) {
  if (selector === "form[data-signup-question-confirmation='true']") return staleImpactResponse ? [freshDestructiveForm] : [];
  if (staleImpactResponse && selector.includes("app-toast-error")) return [{ textContent: "Reload this question before confirming: it currently has 2 saved answer(s)." }];
  return validationError ? [{ textContent: "Question is required." }] : [];
}, querySelector(selector) {
  if (selector === "[data-signup-questions-editor]") return editor;
  if (selector === "#app-notice-region") return notice;
  return null;
} };
const confirmationDialog = new Element();
const confirmationTitle = new Element();
const confirmationDescription = new Element();
const confirmationAction = new Element();
confirmationDialog.selectors = {
  "#admin-confirmation-title": [confirmationTitle],
  "#admin-confirmation-description": [confirmationDescription],
  "[data-admin-confirmation-action]": [confirmationAction]
};
const bodyClasses = new Set();
const body = new Element();
body.classList = { add: (name) => bodyClasses.add(name), remove: (name) => bodyClasses.delete(name) };
body.contains = (candidate) => candidate === trigger;
const documentListeners = {};
let standaloneEditor = null;
const document = {
  body,
  querySelector(selector) {
    if (selector === "[data-signup-questions-editor]") return standaloneEditor;
    if (selector === "[data-signup-questions-dialog]") return dialog;
    if (selector === "[data-signup-questions-trigger='true']") return trigger;
    if (selector === "#app-notice-region") return noticeRegion;
    if (selector === "[data-signup-question-summary]") return questionSummary;
    if (selector === "[data-admin-confirmation]") return confirmationDialog;
    return null;
  },
  querySelectorAll(selector) { return selector === "[data-signup-questions-trigger='true']" ? [trigger] : []; },
  addEventListener(type, listener) { (documentListeners[type] ??= []).push(listener); },
  importNode(node) { return node; },
  createElement() { return new Element(); }
};

let url = new URL("https://example.test/Admin/Events/Participants/test?ParticipantSearch=alice&sort=participant");
const entries = [{ url: url.href, state: null }];
let entryIndex = 0;
let historyBackCalls = 0;
const window = {
  innerWidth: 1200,
  get location() { return { href: url.href }; },
  history: {
    state: null,
    pushState(state, _title, next) { entries.splice(entryIndex + 1); entries.push({ url: next.href, state }); entryIndex++; url = new URL(next.href); this.state = state; },
    replaceState(state, _title, next) { if (next) { url = new URL(next.href); entries[entryIndex].url = url.href; } entries[entryIndex].state = state; this.state = state; },
    back() { historyBackCalls++; entryIndex--; url = new URL(entries[entryIndex].url); this.state = entries[entryIndex].state; window.listeners.popstate?.forEach((listener) => listener()); },
    forward() { entryIndex++; url = new URL(entries[entryIndex].url); this.state = entries[entryIndex].state; window.listeners.popstate?.forEach((listener) => listener()); }
  },
  listeners: {},
  addEventListener(type, listener) { (this.listeners[type] ??= []).push(listener); },
  removeEventListener(type, listener) { this.listeners[type] = (this.listeners[type] || []).filter(item => item !== listener); },
  setTimeout(callback) { callback(); },
  pendingFetches: [],
  submittedBodies: [],
  fetch: (_url, options = {}) => {
    if (options.body) window.submittedBodies.push(options.body.entries);
    return new Promise((resolve) => window.pendingFetches.push(resolve));
  }
};

let pendingSharedConfirmation = null;
window.adminConfirmation = {
  active: false,
  pending: false,
  open(options) {
    this.active = true;
    this.options = { onConfirm: async () => true, ...options };
    this.editorWasOpen = dialog.open;
    if (this.editorWasOpen) dialog.close();
    return new Promise((resolve) => { pendingSharedConfirmation = { resolve }; });
  },
  cancel() {
    if (!this.active || this.pending) return false;
    this.active = false;
    if (this.editorWasOpen) dialog.showModal();
    pendingSharedConfirmation?.resolve(false);
    pendingSharedConfirmation = null;
    return true;
  },
  discard(options = {}) {
    return this.open(options);
  },
  async confirm() {
    if (!this.active) return false;
    this.pending = true;
    const result = await this.options.onConfirm();
    this.pending = false;
    if (result === true || result?.succeeded === true) {
      this.active = false;
      if (this.editorWasOpen) dialog.showModal();
      pendingSharedConfirmation?.resolve(true);
      pendingSharedConfirmation = null;
    }
    return result;
  }
};

vm.runInNewContext(fs.readFileSync("src/Bingo.Web/wwwroot/js/admin-editor-guard.js", "utf8") + "\n" + fs.readFileSync("src/Bingo.Web/wwwroot/js/signup-questions-overlay.js", "utf8"), {
  document,
  window,
  history: window.history,
  HTMLElement: Element,
  HTMLDialogElement: DialogElement,
  HTMLDetailsElement: DetailsElement,
  HTMLSelectElement: SelectElement,
  HTMLInputElement: Element,
  HTMLFormElement: Element,
  DOMParser: class { parseFromString() { return parsedDocument; } },
  URL,
  FormData: class { constructor(form) { this.entries = (form.entries || []).map(([name, value]) => [name, form.querySelector(`input[name='${name}']`)?.value ?? value]); } [Symbol.iterator]() { return this.entries[Symbol.iterator](); } },
  setTimeout: window.setTimeout
});

async function run() {
  const click = trigger.dispatch("click");
  assert.equal(click.defaultPrevented, true);
  assert.equal(dialog.open, false, "the dialog stays hidden while editor content loads");
  window.history.back();
  window.pendingFetches.shift()({ ok: true, text: async () => "<questions-editor />" });
  await new Promise((resolve) => setImmediate(resolve));
  assert.equal(dialog.open, false, "a closed request cannot expose the dialog later");

  trigger.dispatch("click");
  assert.equal(dialog.open, false);
  window.pendingFetches.shift()({ ok: true, text: async () => "<questions-editor />" });
  await new Promise((resolve) => setImmediate(resolve));
  assert.equal(dialog.open, true);
  assert.equal(content.children[0], editor, "the dialog owns the fetched editor content");
  assert.equal(dialog.getAttribute("aria-labelledby"), "signup-questions-dialog-title");
  assert.equal(dialog.getAttribute("aria-describedby"), "signup-questions-dialog-description");
  assert.equal(bodyClasses.has("admin-route-dialog-open"), true);

  const beforeResize = url.href;
  window.innerWidth = 800;
  (window.listeners.resize || []).forEach((listener) => listener());
  assert.equal(dialog.open, true, "clean editor remains modal on narrowing");
  assert.equal(url.href, beforeResize);
  assert.equal(window.pendingFetches.length, 0, "resize never reloads the editor");
  editor.dataset.signupQuestionCount = "5";
  notice.replaceChildren(feedback);
  const mutation = mutationForm.dispatch("submit");
  assert.equal(mutation.defaultPrevented, true, "successful overlay mutations stay in the dialog flow");
  assert.equal(dialog.open, true, "the dialog stays open while the mutation loads");
  window.pendingFetches.shift()({ ok: true, text: async () => "<questions-editor saved />" });
  await new Promise((resolve) => setImmediate(resolve));
  assert.equal(dialog.open, true, "a successful mutation retains the open dialog");
  assert.equal(content.children[0], editor, "the successful mutation replaces the dialog editor");
  assert.equal(noticeRegion.children[0], feedback, "mutation feedback transfers to the page notice region");
  assert.equal(questionSummary.textContent, "5 active questions configured.", "the Participants signup-form summary refreshes in place");

  mutationForm.entries[0][1] = "Unsaved question";
  mutationForm.dispatch("submit");
  mutationForm.dispatch("submit");
  assert.equal(window.pendingFetches.length, 1, "a pending write prevents duplicate submission");
  dialog.dispatch("cancel");
  assert.equal(dialog.open, true, "Escape cannot dismiss a pending write");
  window.history.back();
  assert.equal(dialog.open, true, "Back cannot dismiss a pending write");
  assert.match(url.search, /signupQuestions=1/, "pending Back restores the editor route");
  window.pendingFetches.shift()({ ok: false });
  await new Promise((resolve) => setImmediate(resolve));
  assert.equal(content.children[0], editor, "transport failure retains the editor");
  assert.equal(mutationForm.entries[0][1], "Unsaved question", "transport failure retains input");
  assert.equal(failure.hidden, false, "failure exposes inline feedback for retry");
  mutationForm.dispatch("submit");
  validationError = true;
  window.pendingFetches.shift()({ ok: true, text: async () => "<invalid />" });
  await new Promise((resolve) => setImmediate(resolve));
  assert.equal(failure.textContent, "Question is required.", "server validation displays actionable feedback");
  assert.equal(mutationForm.entries[0][1], "Unsaved question", "validation does not reset submitted input");
  validationError = false;
  dialog.dispatch("cancel");
  assert.equal(window.adminConfirmation.active, true, "dirty Escape opens the shared discard confirmation");
  window.adminConfirmation.cancel();
  await new Promise((resolve) => setImmediate(resolve));
  assert.equal(dialog.open, true);
  assert.equal(mutationForm.entries[0][1], "Unsaved question");
  window.history.back();
  await new Promise((resolve) => setImmediate(resolve));
  assert.equal(window.adminConfirmation.active, true, "dirty Back opens the shared discard confirmation");
  assert.match(url.search, /signupQuestions=1/);
  window.adminConfirmation.cancel();
  await new Promise((resolve) => setImmediate(resolve));
  otherForm.entries[0][1] = "Other unsaved value";
  mutationForm.dispatch("submit");
  assert.equal(window.pendingFetches.length, 0, "another dirty form prevents silent replacement");
  assert.equal(window.adminConfirmation.active, true, "another dirty form uses the shared discard confirmation");
  window.adminConfirmation.cancel();
  await new Promise((resolve) => setImmediate(resolve));
  assert.equal(otherForm.entries[0][1], "Other unsaved value");
  mutationForm.dispatch("submit");
  const discardAttempt = window.adminConfirmation.confirm();
  await discardAttempt;
  await new Promise((resolve) => setImmediate(resolve));
  assert.equal(window.pendingFetches.length, 1, "explicit discard authorizes the queued write");
  window.pendingFetches.shift()({ ok: true, text: async () => "<saved />" });
  await new Promise((resolve) => setImmediate(resolve));

  destructiveForm.dispatch("submit", { submitter: destructiveButton });
  assert.equal(window.adminConfirmation.active, true, "destructive question actions use the shared confirmation");
  assert.equal(window.adminConfirmation.options.title, "Delete question?");
  assert.match(window.adminConfirmation.options.description, /Current impact: 1 saved answer/);
  assert.equal(window.adminConfirmation.options.actionLabel, "Delete question");
  assert.equal(window.pendingFetches.length, 0, "opening confirmation does not mutate");
  assert.equal(window.adminConfirmation.cancel(), true, "Cancel closes the shared confirmation");
  assert.equal(window.pendingFetches.length, 0, "Cancel never submits the destructive form");

  staleImpactResponse = true;
  destructiveForm.dispatch("submit", { submitter: destructiveButton });
  const staleAttempt = window.adminConfirmation.confirm();
  await new Promise((resolve) => setImmediate(resolve));
  assert.equal(window.pendingFetches.length, 1, "stale impact confirmation sends one server request");
  const staleBody = window.submittedBodies[window.submittedBodies.length - 1];
  assert.equal(staleBody.find(([name]) => name === "expectedAnswerCount")[1], "1", "stale request carries the originally confirmed answer count");
  window.pendingFetches.shift()({ ok: true, text: async () => "<questions-editor stale />" });
  const staleResult = await staleAttempt;
  assert.equal(staleResult.succeeded, false, "stale server impact is not treated as a successful mutation");
  assert.equal(window.adminConfirmation.active, true, "stale impact keeps the shared confirmation recoverable");
  assert.equal(destructiveInputs.expectedAnswerCount.value, "2", "stale response refreshes the answer count in the submitted form");
  assert.equal(destructiveInputs.expectedEventRegistrationReleaseCount.value, "1", "stale response refreshes the release count in the submitted form");
  assert.equal(destructiveInputs.expectedQuestionVersion.value, "5", "stale response refreshes the question version in the submitted form");
  assert.equal(confirmationTitle.textContent, "Delete question after reload?", "stale response refreshes the modal title");
  assert.match(confirmationDescription.textContent, /2 saved answers/);
  assert.equal(confirmationAction.textContent, "Delete question again", "stale response refreshes the modal action");

  staleImpactResponse = false;
  const staleRetry = window.adminConfirmation.confirm();
  await new Promise((resolve) => setImmediate(resolve));
  const refreshedBody = window.submittedBodies[window.submittedBodies.length - 1];
  assert.equal(refreshedBody.find(([name]) => name === "expectedAnswerCount")[1], "2", "retry submits the refreshed answer count");
  assert.equal(refreshedBody.find(([name]) => name === "expectedEventRegistrationReleaseCount")[1], "1", "retry submits the refreshed release count");
  assert.equal(refreshedBody.find(([name]) => name === "expectedQuestionVersion")[1], "5", "retry submits the refreshed question version");
  window.pendingFetches.shift()({ ok: true, text: async () => "<questions-editor saved />" });
  assert.equal(await staleRetry, true, "retry after stale impact resolves the shared confirmation");
  assert.equal(window.adminConfirmation.active, false, "successful stale-impact retry closes the shared confirmation");

  destructiveForm.dispatch("submit", { submitter: destructiveButton });
  const firstAttempt = window.adminConfirmation.confirm();
  await new Promise((resolve) => setImmediate(resolve));
  assert.equal(window.pendingFetches.length, 1, "confirmed action submits once");
  assert.equal(window.adminConfirmation.cancel(), false, "pending confirmation cannot be dismissed");
  window.pendingFetches.shift()({ ok: false });
  const failed = await firstAttempt;
  assert.equal(failed.succeeded, false, "transport failure keeps the shared confirmation recoverable");
  assert.equal(window.adminConfirmation.active, true, "failed destructive action remains open for retry");

  const retry = window.adminConfirmation.confirm();
  await new Promise((resolve) => setImmediate(resolve));
  assert.equal(window.pendingFetches.length, 1, "retry submits one request");
  window.pendingFetches.shift()({ ok: true, text: async () => "<questions-editor saved />" });
  assert.equal(await retry, true, "successful retry resolves the shared confirmation");
  assert.equal(window.adminConfirmation.active, false, "successful retry closes the shared confirmation");
  assert.equal(dialog.open, true, "successful destructive action leaves the Questions editor available");

  editorCloseButton.dispatch("click");
  assert.equal(dialog.open, false);
  assert.equal(content.children.length, 0);
  assert.equal(bodyClasses.has("admin-route-dialog-open"), false);
  assert.equal(trigger.focused, true);
  assert.match(url.search, /ParticipantSearch=alice/);
  assert.match(url.search, /sort=participant/);

  window.history.forward();
  assert.equal(dialog.open, false, "Forward waits for editor content before reopening");
  window.pendingFetches.shift()({ ok: true, text: async () => "<questions-editor />" });
  await new Promise((resolve) => setImmediate(resolve));
  assert.equal(dialog.open, true, "Forward reopens the dialog");

  trigger.focused = false;
  window.history.back();
  assert.equal(dialog.open, false, "real history traversal closes the dialog");
  assert.equal(trigger.focused, true, "real history traversal restores focus to the Edit signup form trigger");

  window.history.forward();
  assert.equal(dialog.open, false, "Forward waits for editor content before reopening after history close");
  window.pendingFetches.shift()({ ok: true, text: async () => "<questions-editor />" });
  await new Promise((resolve) => setImmediate(resolve));

  dialog.dispatch("cancel");
  assert.equal(dialog.open, false, "Escape closes through history");

  window.history.forward();
  assert.equal(dialog.open, false, "Forward waits for editor content before reopening after Escape");
  window.pendingFetches.shift()({ ok: true, text: async () => "<questions-editor />" });
  await new Promise((resolve) => setImmediate(resolve));
  mutationForm.entries[0][1] = "Dirty before narrowing";
  window.innerWidth = 800;
  (window.listeners.resize || []).forEach((listener) => listener());
  assert.equal(dialog.open, true, "narrowing retains an active dirty editor");
  editorCloseButton.dispatch("click");
  assert.equal(window.adminConfirmation.active, true, "narrowing discard uses the shared confirmation");
  await window.adminConfirmation.confirm();
  await new Promise((resolve) => setImmediate(resolve));
  assert.equal(dialog.open, false, "confirmed discard completes close after narrowing");
  assert.equal(content.children.length, 0);
  assert.equal(bodyClasses.has("admin-route-dialog-open"), false);
  assert.doesNotMatch(url.search, /signupQuestions=1/);
  window.innerWidth = 1200;
  trigger.dispatch("click");
  window.pendingFetches.shift()({ ok: true, text: async () => "<questions-editor />" });
  await new Promise((resolve) => setImmediate(resolve));
  dialog.dispatch("click");
  assert.equal(dialog.open, false, "a reopened editor still closes after confirmed narrow discard");

  // The same real form handlers also run on the narrow/direct standalone editor.
  standaloneEditor = editor;
  let standaloneReplacements = 0;
  editor.replaceWith = (replacement) => { standaloneEditor = replacement; standaloneReplacements++; };
  window.innerWidth = 800;
  mutationForm.entries[0][1] = "Attempted standalone edit";
  mutationForm.dispatch("submit");
  mutationForm.dispatch("submit");
  assert.equal(window.pendingFetches.length, 1, "standalone pending write prevents duplicate submission");
  validationError = true;
  window.pendingFetches.shift()({ ok: true, text: async () => "<invalid />" });
  await new Promise((resolve) => setImmediate(resolve));
  assert.equal(standaloneReplacements, 0, "invalid standalone response retains the expanded editor DOM");
  assert.equal(mutationForm.entries[0][1], "Attempted standalone edit");
  assert.equal(failure.textContent, "Question is required.");
  validationError = false;
  mutationForm.dispatch("submit");
  window.pendingFetches.shift()({ ok: false });
  await new Promise((resolve) => setImmediate(resolve));
  assert.equal(standaloneReplacements, 0, "standalone transport failure retains editor values");
  mutationForm.dispatch("submit");
  window.pendingFetches.shift()({ ok: true, text: async () => "<saved />" });
  await new Promise((resolve) => setImmediate(resolve));
  assert.equal(standaloneReplacements, 1, "standalone retry updates its original in-page editor");
  assert.equal(dialog.open, false);
  assert.equal(content.children.length, 0, "standalone save does not move content into a hidden dialog");
}

run().catch((error) => { console.error(error); process.exitCode = 1; });

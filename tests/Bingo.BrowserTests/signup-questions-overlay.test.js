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
  if (selector === "details.signup-question-remove[open]") return content.children[0]?.querySelector(selector) ?? null;
  return content.children[0]?.querySelector(selector) ?? null;
};

const editor = new Element();
const editorCloseButton = new Element();
const mutationForm = new Element();
mutationForm.entries = [["Input.Label", ""]];
const otherForm = new Element();
otherForm.entries = [["Settings.NewSignupCode", ""]];
const discard = new Element();
discard.hidden = true;
const keep = new Element();
const discardConfirm = new Element();
discard.selectors = { "[data-signup-questions-keep]": [keep] };
const codeToggle = new Element();
const codeControl = new Element();
const codeInput = new Element();
codeInput.dataset.hasSignupCode = "false";
const failure = new Element();
failure.hidden = true;
mutationForm.method = "post";
mutationForm.action = "/Admin/Events/Questions/test?handler=Add";
const confirmationSummary = new Element();
const confirmation = new DetailsElement(confirmationSummary);
const cancelRemoval = new Element();
cancelRemoval.parentElement = confirmation;
confirmation.parentElement = editor;
confirmationSummary.addEventListener("click", () => confirmation.setAttribute("open", ""));
editor.selectors = { "[data-signup-questions-close]": [editorCloseButton], form: [mutationForm, otherForm], "[data-signup-code-toggle]": [codeToggle], "[data-signup-code-control]": [codeControl], "[data-signup-code-input]": [codeInput], "[data-signup-questions-discard]": [discard], "[data-signup-questions-keep]": [keep], "[data-signup-questions-discard-confirm]": [discardConfirm], "[data-signup-questions-feedback]": [failure] };
editor.querySelectorAll = (selector) => {
  if (selector === "details.signup-question-remove") return [confirmation];
  if (selector === "details.signup-question-remove[open]") return confirmation.open ? [confirmation] : [];
  if (selector === "[data-signup-question-cancel-removal]") return [cancelRemoval];
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
const parsedDocument = { querySelectorAll() { return validationError ? [{ textContent: "Question is required." }] : []; }, querySelector(selector) {
  if (selector === "[data-signup-questions-editor]") return editor;
  if (selector === "#app-notice-region") return notice;
  return null;
} };
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
  setTimeout(callback) { callback(); },
  pendingFetches: [],
  fetch: () => new Promise((resolve) => window.pendingFetches.push(resolve))
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
  FormData: class { constructor(form) { this.entries = form.entries || []; } [Symbol.iterator]() { return this.entries[Symbol.iterator](); } },
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

  assert.equal(codeControl.hidden, true, "disabled code settings hide their input");
  codeToggle.checked = true;
  codeToggle.dispatch("change");
  assert.equal(codeControl.hidden, false);
  assert.equal(codeInput.required, true, "enabling without a stored code requires one");
  assert.equal(window.pendingFetches.length, 0, "toggling does not autosave");
  codeInput.dataset.hasSignupCode = "true";
  codeToggle.dispatch("change");
  assert.equal(codeInput.required, false, "an existing code may be retained with a blank replacement");
  codeToggle.checked = false;
  codeToggle.dispatch("change");
  assert.equal(codeControl.hidden, true);
  assert.equal(codeInput.required, false);
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
  assert.equal(discard.hidden, false, "dirty Escape asks before discarding");
  keep.dispatch("click");
  assert.equal(dialog.open, true);
  assert.equal(mutationForm.entries[0][1], "Unsaved question");
  window.history.back();
  assert.equal(discard.hidden, false, "dirty Back asks before discarding");
  assert.match(url.search, /signupQuestions=1/);
  keep.dispatch("click");
  otherForm.entries[0][1] = "Other unsaved value";
  mutationForm.dispatch("submit");
  assert.equal(window.pendingFetches.length, 0, "another dirty form prevents silent replacement");
  assert.equal(discard.hidden, false);
  keep.dispatch("click");
  assert.equal(otherForm.entries[0][1], "Other unsaved value");
  mutationForm.dispatch("submit");
  discardConfirm.dispatch("click");
  assert.equal(window.pendingFetches.length, 1, "explicit discard authorizes the queued write");
  window.pendingFetches.shift()({ ok: true, text: async () => "<saved />" });
  await new Promise((resolve) => setImmediate(resolve));

  const historyBeforeConfirmation = historyBackCalls;
  confirmationSummary.dispatch("click");
  assert.equal(confirmation.open, true, "the remove confirmation opens from its summary");
  const escape = confirmationSummary.dispatch("keydown", { key: "Escape" });
  assert.equal(escape.defaultPrevented, true, "confirmation Escape is consumed locally");
  assert.equal(escape.propagationStopped, true, "confirmation Escape does not reach the outer dialog");
  assert.equal(confirmation.open, false, "confirmation Escape closes only the confirmation");
  assert.equal(confirmationSummary.focused, true, "confirmation Escape restores summary focus");
  assert.equal(confirmationSummary.focusOptions?.preventScroll, true);
  assert.equal(historyBackCalls, historyBeforeConfirmation, "confirmation Escape does not navigate history");

  confirmationSummary.dispatch("click");
  dialog.dispatch("cancel");
  assert.equal(confirmation.open, false, "outer cancel closes an open confirmation first");
  assert.equal(historyBackCalls, historyBeforeConfirmation, "outer cancel does not navigate while confirmation is open");

  confirmationSummary.dispatch("click");
  cancelRemoval.dispatch("click");
  assert.equal(confirmation.open, false, "explicit Cancel closes the confirmation");
  assert.equal(confirmationSummary.focused, true, "explicit Cancel restores summary focus");

  confirmationSummary.dispatch("click");
  dialog.dispatch("click", { target: dialog });
  assert.equal(confirmation.open, false, "the first backdrop activation closes only the confirmation");
  assert.equal(dialog.open, true, "the first backdrop activation leaves Questions open");
  assert.equal(historyBackCalls, historyBeforeConfirmation, "the first backdrop activation does not navigate history");
  dialog.dispatch("click", { target: dialog });
  assert.equal(historyBackCalls, historyBeforeConfirmation + 1, "the second backdrop activation closes through history");
  assert.equal(dialog.open, false, "the second backdrop activation closes Questions");

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
  assert.equal(discard.hidden, false);
  discardConfirm.dispatch("click");
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

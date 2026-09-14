const assert = require("node:assert/strict");
const fs = require("node:fs");
const vm = require("node:vm");

class FakeNode {}

class FakeElement extends FakeNode {
  constructor(tagName = "div") {
    super();
    this.tagName = tagName.toUpperCase();
    this.parentElement = null;
    this.children = [];
    this.open = false;
    this.attributes = {};
    this.dataset = {};
    this.listeners = {};
    this.connected = true;
    this.classes = new Set();
    this.classList = { add: value => this.classes.add(value), contains: value => this.classes.has(value) };
    this.dismiss = new FakeElement.Dismiss();
  }

  setAttribute(name, value) {
    this.attributes[name] = String(value);
    if (name === "class") this.className = String(value);
    if (name.startsWith("data-")) this.dataset[name.slice(5).replace(/-([a-z])/g, (_match, letter) => letter.toUpperCase())] = String(value);
  }

  addEventListener(type, listener) { (this.listeners[type] ??= []).push(listener); }
  dispatch(type, extras = {}) {
    for (const listener of this.listeners[type] ?? []) listener({ type, relatedTarget: null, preventDefault() {}, ...extras });
  }
  querySelector(selector) {
    if (selector === "[data-dismiss-toast]") return this.dismiss;
    if (selector === ".app-toast-copy span") return this.message ??= new FakeElement("span");
    return null;
  }
  appendChild(child) {
    child.parentElement?.children.splice(child.parentElement.children.indexOf(child), 1);
    child.parentElement = this;
    this.children.push(child);
    return child;
  }
  append(...children) { children.forEach(child => this.appendChild(child)); }
  contains(candidate) { return candidate === this || this.children.some(child => child.contains(candidate)); }
  closest(selector) {
    for (let node = this.parentElement; node; node = node.parentElement) if (selector === "dialog" && node.tagName === "DIALOG") return node;
    return null;
  }
  remove() { this.connected = false; this.parentElement?.children.splice(this.parentElement.children.indexOf(this), 1); }
  get isConnected() { return this.connected; }
}

FakeElement.Dismiss = class extends FakeNode {
  constructor() { super(); this.listeners = {}; }
  addEventListener(type, listener) { (this.listeners[type] ??= []).push(listener); }
  dispatch(type) { for (const listener of this.listeners[type] ?? []) listener({ type }); }
};

const source = fs.readFileSync("src/Bingo.Web/wwwroot/js/site.js", "utf8");
const popoverStart = source.indexOf("function initializePublicHeaderPopovers()");
const popoverEnd = source.indexOf("function initializePublicTheme()", popoverStart);
const start = source.indexOf("const transientToastTypes");
const end = source.indexOf("function initializeAdminMenu");
const showStart = source.indexOf("window.showBingoToast =");
const showEnd = source.indexOf("\n};", showStart) + 3;
assert.ok(start >= 0 && end > start && showStart > end && showEnd > showStart, "toast implementation boundary is present");
assert.ok(popoverStart >= 0 && popoverEnd > popoverStart && popoverEnd < start, "actual popover initializer dependency is present");

let now = 0;
let nextTimer = 1;
const timers = new Map();
const window = {
  setTimeout(callback, delay) {
    const id = nextTimer++;
    timers.set(id, { callback, at: now + delay });
    return id;
  },
  clearTimeout(id) { timers.delete(id); }
};
window.matchMedia = () => ({ matches: false });
const body = new FakeElement("body");
const dialog = new FakeElement("dialog");
const host = new FakeElement();
dialog.appendChild(host);
body.appendChild(dialog);
const document = {
  body,
  readyState: "complete",
  documentElement: { dataset: {
    publicToastSuccess: "Success", publicToastWarning: "Warning", publicToastError: "Error",
    publicToastInformation: "Information", publicToastDismiss: "Dismiss"
  } },
  listeners: {},
  addEventListener(type, listener) { (this.listeners[type] ??= []).push(listener); },
  querySelectorAll() { return []; },
  querySelector(selector) { return selector === "#app-notice-region" ? host : null; },
  createElement(tagName) { return new FakeElement(tagName); }
};

const context = {
  Date: { now: () => now },
  Node: FakeNode,
  HTMLElement: FakeElement,
  HTMLDialogElement: FakeElement,
  MutationObserver: undefined,
  document,
  window
};
// The toast slice registers and invokes this real dependency. This fixture has no
// header menus, so its normal empty-menu path runs without loading unrelated UI.
vm.runInNewContext(source.slice(popoverStart, popoverEnd) + source.slice(start, end) + source.slice(showStart, showEnd), context);
assert.ok(document.listeners["bingo:content-updated"].includes(context.initializePublicHeaderPopovers), "content updates retain the actual popover initializer");

const toast = new FakeElement();
toast.dataset.toastDuration = "6000";
const due = () => [...timers].filter(([, timer]) => timer.at <= now).forEach(([id, timer]) => { timers.delete(id); timer.callback(); });

context.initializeTransientToast(toast);
assert.equal(timers.size, 1);
now = 2500;
toast.dispatch("mouseenter");
assert.equal(timers.size, 0, "hover pauses the auto-dismiss timer");
now = 9000;
toast.dispatch("mouseleave");
assert.equal([...timers.values()][0].at, 12500, "hover pause preserves the remaining duration");

now = 10000;
toast.dispatch("focusin");
assert.equal(timers.size, 0, "keyboard focus pauses the auto-dismiss timer");
toast.dispatch("focusout", { relatedTarget: null });
assert.equal(timers.size, 1, "leaving focus resumes the remaining timer");
now = 12500;
due();
assert.equal(toast.connected, true, "toast remains present during its dismissal fade");
assert.equal(toast.classList.contains("is-dismissing"), true, "toast fades on auto-dismiss");
now = 12640;
due();
assert.equal(toast.connected, false, "toast is removed after its dismissal fade");

const closeToast = new FakeElement();
closeToast.dataset.toastDuration = "6000";
context.initializeTransientToast(closeToast);
closeToast.dismiss.dispatch("click");
assert.equal(closeToast.connected, true, "close-X dismissal allows the fade to run");
assert.equal(closeToast.classList.contains("is-dismissing"), true, "close-X dismissal fades the toast");
now += 140;
due();
assert.equal(closeToast.connected, false, "close-X dismissal removes the toast after its fade");

window.matchMedia = () => ({ matches: true });
const reducedMotionToast = new FakeElement();
reducedMotionToast.dataset.toastDuration = "6000";
context.initializeTransientToast(reducedMotionToast);
reducedMotionToast.dispatch("keydown", { key: "Escape" });
assert.equal(reducedMotionToast.connected, false, "reduced motion removes dismissed toast without animation");

dialog.open = false;
context.window.showBingoToast("Created disabled emergency credential test-account.", "success");
const visibleToast = host.children.at(-1);
assert.equal(host.parentElement, body, "a toast host is restored outside a closed route dialog");
assert.equal(dialog.contains(host), false, "a closed route dialog no longer owns the toast host");
assert.equal(visibleToast?.dataset.transientToast, "true", "the shared toast remains visible after reparenting");

for (const [input, expected, label] of [
  ["success", "success", "Success"], ["warning", "warning", "Warning"],
  ["error", "error", "Error"], ["information", "information", "Information"],
  ["INFO", "information", "Information"], ["unknown", "information", "Information"]
]) {
  context.window.showBingoToast("Fixture <message>", input);
  const current = host.children.at(-1);
  assert.equal(current.className, `app-toast app-toast-${expected}`, `${input} uses normalized severity styling`);
  assert.equal(current.attributes.role, expected === "error" ? "alert" : "status", `${input} uses the appropriate announcement role`);
  assert.ok(current.innerHTML.includes(`<strong>${label}</strong>`), `${input} retains its localized severity label`);
  assert.match(current.innerHTML, /data-dismiss-toast aria-label="Dismiss"/, "generated toast has a labeled dismiss button");
  assert.equal(current.querySelector(".app-toast-copy span").textContent, "Fixture <message>", "message is assigned as text");
  assert.equal(current.dataset.toastDuration, "6000", "each severity retains the default duration");
  assert.equal(timers.get(current._toastTimer).at, now + 6000, "generated toast schedules its actual duration");
  const timer = current._toastTimer;
  context.initializeTransientToast(current);
  assert.equal(current._toastTimer, timer, "reinitialization preserves the existing timer");
  current.dismiss.dispatch("click");
  assert.equal(current.connected, false, "generated toast supports reduced-motion dismissal");
  assert.equal(timers.has(timer), false, "dismissal clears the generated toast timer");
}

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
const start = source.indexOf("const transientToastTypes");
const end = source.indexOf("function initializeAdminMenu");
const showStart = source.indexOf("window.showBingoToast =");
const showEnd = source.indexOf("\n};", showStart) + 3;
assert.ok(start >= 0 && end > start && showStart > end && showEnd > showStart, "toast implementation boundary is present");

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
  addEventListener() {},
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
vm.runInNewContext(source.slice(start, end) + source.slice(showStart, showEnd), context);

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

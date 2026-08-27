const assert = require("node:assert/strict");
const fs = require("node:fs");
const path = require("node:path");
const vm = require("node:vm");

class Node {
  constructor(tagName, className = "") {
    this.tagName = tagName.toUpperCase();
    this.className = className;
    this.children = [];
    this.listeners = {};
  }

  addEventListener(type, listener) { (this.listeners[type] ??= []).push(listener); }
  dispatch(type, properties = {}) {
    const event = { type, target: this, ...properties };
    for (const listener of this.listeners[type] ?? []) listener(event);
  }
  querySelectorAll() { return []; }
}

class Dialog extends Node {
  constructor() { super("dialog", "board-page tile-dialog"); this.closeCalls = 0; }
  close() { this.closeCalls++; }
}

const repositoryRoot = path.resolve(__dirname, "../..");
const boardMarkup = fs.readFileSync(path.join(repositoryRoot, "src/Bingo.Web/Pages/Admin/Events/Board.cshtml"), "utf8");
const inlineScript = boardMarkup.match(/<script>\n([\s\S]*?)\n<\/script>/)?.[1];
assert.ok(inlineScript, "Board inline script should be present");

const dialog = new Dialog();
const document = {
  querySelector: () => null,
  querySelectorAll: selector => selector === ".board-page dialog.tile-dialog" ? [dialog] : [],
  getElementById: () => null,
  addEventListener: () => {}
};

vm.runInNewContext(inlineScript, {
  document,
  window: { matchMedia: () => ({ matches: false }) },
  console,
  Map,
  JSON,
  Number,
  String,
  Math,
  Event: class Event {}
});

dialog.dispatch("click", { target: dialog });
assert.equal(dialog.closeCalls, 1, "desktop backdrop click closes the Board dialog");

const content = new Node("div");
dialog.dispatch("click", { target: content });
assert.equal(dialog.closeCalls, 1, "internal dialog click does not close the Board dialog");

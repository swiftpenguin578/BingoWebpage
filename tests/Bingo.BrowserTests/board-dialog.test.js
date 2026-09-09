const assert = require("node:assert/strict");
const fs = require("node:fs");
const path = require("node:path");
const vm = require("node:vm");

const repositoryRoot = path.resolve(__dirname, "../..");
const boardMarkup = fs.readFileSync(path.join(repositoryRoot, "src/Bingo.Web/Pages/Admin/Events/Board.cshtml"), "utf8");
const guardScript = fs.readFileSync(path.join(repositoryRoot, "src/Bingo.Web/wwwroot/js/admin-editor-guard.js"), "utf8");
const inlineScript = boardMarkup.match(/<script>\n([\s\S]*?)\n<\/script>/)?.[1].replace(/@Json.Serialize\(T\["([^"]+)"\]\.Value\)/g, (_, value) => JSON.stringify(value));
assert.ok(inlineScript, "Board inline script should be present");

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
    this.listeners = {};
    this.attributes = {};
    this.hidden = false;
    this.open = false;
    this.disabled = false;
    this.focused = false;
    this._innerHTML = "";
    Object.defineProperty(this, "innerHTML", {
      get: () => this._innerHTML,
      set: value => { this._innerHTML = String(value); this.replaceChildren(); }
    });
    this.classList = {
      add: (...names) => names.forEach(name => { if (!this.className.split(" ").includes(name)) this.className = `${this.className} ${name}`.trim(); }),
      remove: (...names) => { this.className = this.className.split(" ").filter(name => name && !names.includes(name)).join(" "); },
      toggle: (name, force) => {
        const present = this.className.split(" ").includes(name);
        const next = force === undefined ? !present : force;
        if (next && !present) this.className = `${this.className} ${name}`.trim();
        if (!next && present) this.classList.remove(name);
        return next;
      },
      contains: name => this.className.split(" ").includes(name)
    };
  }

  append(...children) {
    children.flat().filter(Boolean).forEach(child => {
      child.parentElement = this;
      child.ownerDocument = this.ownerDocument;
      this.children.push(child);
      child.setOwnerDocument?.(this.ownerDocument);
    });
  }

  setOwnerDocument(document) {
    this.ownerDocument = document;
    this.children.forEach(child => child.setOwnerDocument?.(document));
  }

  replaceChildren(...children) {
    this.children.forEach(child => { child.parentElement = null; });
    this.children = [];
    this.append(...children);
  }

  reset() {
    this.querySelectorAll("input, select, textarea").forEach(control => {
      control.value = control.defaultValue ?? "";
      control.disabled = false;
    });
  }

  addEventListener(type, listener) { (this.listeners[type] ??= []).push(listener); }

  setAttribute(name, value) { this.attributes[name] = String(value); if (name.startsWith("data-")) this.dataset[name.slice(5).replace(/-([a-z])/g, (_, char) => char.toUpperCase())] = String(value); }
  hasAttribute(name) { return Object.hasOwn(this.attributes, name); }
  removeAttribute(name) { delete this.attributes[name]; }

  dispatch(type, properties = {}) {
    const event = {
      type,
      target: this,
      currentTarget: this,
      defaultPrevented: false,
      preventDefault() { this.defaultPrevented = true; },
      stopPropagation() { this.propagationStopped = true; },
      stopImmediatePropagation() { this.propagationStopped = true; },
      ...properties
    };
    for (const listener of this.listeners[type] ?? []) listener(event);
    return event;
  }

  focus() {
    this.focused = true;
    if (this.ownerDocument) this.ownerDocument.activeElement = this;
  }

  close() { this.open = false; this.dispatch("close"); }
  showModal() { this.open = true; }
  getBoundingClientRect() { return this.rect ?? { left: 100, top: 100, right: 500, bottom: 500 }; }

  contains(node) {
    return this === node || this.children.some(child => child.contains(node));
  }

  closest(selector) {
    for (let node = this; node; node = node.parentElement) if (node.matches(selector)) return node;
    return null;
  }

  querySelector(selector) { return this.querySelectorAll(selector)[0] ?? null; }

  querySelectorAll(selector) {
    const selectors = selector.split(",").map(item => item.trim()).filter(Boolean);
    const result = [];
    const visit = node => node.children.forEach(child => {
      if (selectors.some(item => child.matches(item))) result.push(child);
      visit(child);
    });
    visit(this);
    return result;
  }

  matches(selector) {
    selector = selector.trim();
    if (selector.includes(" ")) return false;
    if (selector === ":modal") return this.open;
    const tag = selector.match(/^[a-z-]+/i)?.[0];
    if (tag && this.tagName !== tag.toUpperCase()) return false;
    const id = selector.match(/#([\w-]+)/)?.[1];
    if (id && this.id !== id) return false;
    for (const className of selector.matchAll(/\.([\w-]+)/g)) if (!this.classList.contains(className[1])) return false;
    for (const attribute of selector.matchAll(/\[data-([\w-]+)(?:=['"]([^'"]*)['"])?\]/g)) {
      const key = attribute[1].replace(/-([a-z])/g, (_match, letter) => letter.toUpperCase());
      if (!Object.prototype.hasOwnProperty.call(this.dataset, key)) return false;
      if (attribute[2] !== undefined && String(this.dataset[key]) !== attribute[2]) return false;
    }
    const named = selector.match(/\[name=['"]([^'"]+)['"]\]/);
    if (named && this.name !== named[1]) return false;
    return true;
  }
}

class Document extends Node {
  constructor() {
    super("document");
    this.body = new Node("body");
    this.body.setOwnerDocument(this);
    this.append(this.body);
    this.activeElement = null;
  }

  createElement(tag) { return new Node(tag); }

  getElementById(id) {
    if (this.body.id === id) return this.body;
    return [this.body, ...this.body.querySelectorAll("*")].find(node => node.id === id) ?? null;
  }

  querySelectorAll(selector) {
    if (selector === ".board-page dialog.tile-dialog") return this.body.querySelectorAll("dialog.tile-dialog").filter(dialog => dialog.parentElement?.classList.contains("board-page"));
    if (selector.includes(".requirement-editor")) return [];
    return this.body.querySelectorAll(selector);
  }
}

let emptyFileLastModified = 0;
class FormDataMock {
  constructor(form) {
    this.items = [];
    form?.querySelectorAll("input, select, textarea").forEach(control => {
      if (control.name && !control.disabled) {
        const value = control.type === "file" && control.value === ""
          ? { name: "", size: 0, type: "application/octet-stream", lastModified: ++emptyFileLastModified }
          : control.value;
        this.items.push([control.name, value]);
      }
    });
  }

  *[Symbol.iterator]() { yield* this.items; }
  get(name) { return this.items.find(item => item[0] === name)?.[1] ?? null; }
  append(name, value) { this.items.push([name, value]); }
}

const document = new Document();
const boardPage = new Node("section", { className: "board-page" });
const detailDialog = new Node("dialog", { id: "details-tile-1", className: "tile-dialog" });
detailDialog.rect = { left: 100, top: 100, right: 500, bottom: 500 };
const detailClose = new Node("button", { className: "dialog-close" });
detailDialog.append(detailClose);

const createDialog = new Node("dialog", { id: "create-tile-dialog", className: "tile-dialog" });
createDialog.rect = { left: 100, top: 100, right: 500, bottom: 500 };
const createClose = new Node("button", { className: "dialog-close" });
const createCancel = new Node("button", { className: "dialog-cancel" });
const form = new Node("form", { id: "create-tile-form", dataset: { createAction: "/create", editAction: "/edit" } });
const tileId = new Node("input", { id: "tile-id", name: "TileDraft.TileId" });
const tilePosition = new Node("input", { id: "tile-position", name: "TileDraft.Position" });
const tileName = new Node("input", { id: "tile-name", name: "TileDraft.Name" });
const description = new Node("input", { id: "TileDraft_Description", name: "TileDraft.Description" });
const image = new Node("input", { id: "TileDraft_Image", name: "TileDraft.Image" });
image.type = "file";
const removeImage = new Node("input", { id: "TileDraft_RemoveImage", name: "TileDraft.RemoveImage" });
const removeImageRow = new Node("label", { id: "tile-remove-image-row" });
const manualEhb = new Node("input", { id: "TileDraft_ManualEhb", name: "TileDraft.ManualEhb" });
const requirements = new Node("div", { id: "requirements" });
form.append(tileId, tilePosition, tileName, description, image, removeImage, removeImageRow, manualEhb, requirements);
const createHeading = new Node("h2", { id: "create-tile-heading" });
const saveButton = new Node("button", { id: "save-tile-button" });
const addRequirement = new Node("button", { id: "add-requirement" });
const template = new Node("template", { id: "requirement-template" });
template.innerHTML = "";
const discard = new Node("div", { dataset: { boardEditorDiscard: "" } });
discard.hidden = true;
const keep = new Node("button", { dataset: { boardEditorKeep: "" } });
const discardConfirm = new Node("button", { dataset: { boardEditorDiscardConfirm: "" } });
discard.append(keep, discardConfirm);
createDialog.append(createClose, createHeading, form, createCancel, saveButton, addRequirement, discard);

const createTrigger = new Node("button", { className: "create-tile-button", dataset: { position: "4" } });
const editTrigger = new Node("button", { className: "edit-tile-button", dataset: { tileId: "tile-1" } });
boardPage.append(createTrigger, editTrigger, detailDialog, createDialog);
document.body.append(boardPage);
document.body.setOwnerDocument(document);
const tileData = new Node("script", { id: "tile-editor-data" });
tileData.textContent = JSON.stringify([{ id: "tile-1", name: "Original", description: "Original description", imageUrl: null, manualEhb: null, requirements: [] }]);
document.body.append(tileData, template);

const windowListeners = {};
const window = {
  listeners: windowListeners,
  addEventListener(type, listener) { (windowListeners[type] ??= []).push(listener); },
  matchMedia: () => ({ matches: false })
};
const guards = [];
const context = { document, window, Element: Node, FormData: FormDataMock, Event: class Event {}, console };
vm.runInNewContext(guardScript, context);
const guardFactory = window.createAdminEditorGuard;
window.createAdminEditorGuard = options => {
  const guard = guardFactory(options);
  guards.push(guard);
  return guard;
};
vm.runInNewContext(inlineScript, context);
const guard = guards[0];
assert.ok(guard, "Board creates the shared editor guard");

detailDialog.showModal();
detailDialog.dispatch("pointerdown", { target: detailDialog, clientX: 10, clientY: 10 });
detailDialog.dispatch("click", { target: detailDialog, clientX: 10, clientY: 10 });
assert.equal(detailDialog.open, false, "clean detail backdrop closes the Board dialog");
detailDialog.showModal();
detailDialog.dispatch("click", { target: detailDialog, clientX: 250, clientY: 250 });
assert.equal(detailDialog.open, true, "clicking detail-dialog padding does not close the Board dialog");
detailDialog.close();

document.dispatch("click", { target: createTrigger });
assert.equal(createDialog.open, true, "Create opens the shared Board editor dialog");
assert.equal(guard.dirtyForms().length, 0, "initial Create editor is clean");

createClose.dispatch("click");
assert.equal(createDialog.open, false, "clean Create close ignores changing empty-file timestamps");
document.dispatch("click", { target: createTrigger });
const emptyNamedFile = { name: "empty.png", size: 0, type: "", lastModified: 7 };
image.value = emptyNamedFile;
assert.equal(guard.dirtyForms().length, 1, "a named empty file remains a dirty selection");
image.value = "";
assert.equal(guard.dirtyForms().length, 0, "clearing a named empty file restores the clean baseline");

const cleanUnload = { preventDefault() { this.prevented = true; } };
windowListeners.beforeunload.forEach(listener => listener(cleanUnload));
assert.equal(cleanUnload.prevented, undefined, "clean editor does not trigger native beforeunload");

tileName.value = "Draft tile";
createCancel.dispatch("click");
assert.equal(createDialog.open, true, "dirty Cancel keeps the editor open");
assert.equal(discard.hidden, false, "dirty Cancel reveals the discard choice");
keep.dispatch("click");
assert.equal(discard.hidden, true, "Keep closes the discard choice");
assert.equal(tileName.value, "Draft tile", "Keep retains typed fields");

createClose.dispatch("click");
assert.equal(discard.hidden, false, "dirty X uses the discard choice");
discardConfirm.dispatch("click");
assert.equal(createDialog.open, false, "Discard closes the cleanly reset editor");
assert.equal(tileName.value, "", "Discard resets the Create form");

document.dispatch("click", { target: createTrigger });
const finishPending = guard.begin(form);
createCancel.dispatch("click");
assert.equal(createDialog.open, true, "pending mutations block Cancel dismissal");
createDialog.dispatch("pointerdown", { target: createDialog, clientX: 10, clientY: 10 });
createDialog.dispatch("click", { target: createDialog, clientX: 10, clientY: 10 });
assert.equal(createDialog.open, true, "pending mutations block backdrop dismissal");
finishPending();
createCancel.dispatch("click");
assert.equal(createDialog.open, false, "the editor closes once pending state settles");

document.dispatch("click", { target: editTrigger });
assert.equal(createDialog.open, true, "Edit opens the shared Board editor dialog");
assert.equal(tileName.value, "Original", "Edit is populated from cached tile data");
assert.equal(guard.dirtyForms().length, 0, "initial Edit editor is clean");

tileName.value = "Changed";
image.value = { name: "tile.png", size: 10, type: "image/png", lastModified: 4 };
const dynamicObjective = new Node("input", { name: "TileDraft.Requirements[1].Target", value: "2" });
requirements.append(dynamicObjective);
const dirtyUnload = { preventDefault() { this.prevented = true; } };
windowListeners.beforeunload.forEach(listener => listener(dirtyUnload));
assert.equal(dirtyUnload.prevented, true, "file and dynamic objective edits trigger native beforeunload");

createDialog.dispatch("cancel");
assert.equal(discard.hidden, false, "dirty Escape reveals the discard choice");
const cancelDiscard = createDialog.dispatch("cancel");
assert.equal(cancelDiscard.defaultPrevented, true, "Escape cancels the discard choice first");
assert.equal(discard.hidden, true, "Escape keeps the dirty editor open");
createCancel.dispatch("click");
discardConfirm.dispatch("click");
assert.equal(createDialog.open, false, "Edit discard closes after restoring cached data");
assert.equal(tileName.value, "Original", "Edit discard restores the cached tile name");
assert.equal(image.value, "", "Edit discard clears the file selection");
assert.equal(requirements.children.length, 0, "Edit discard removes dynamic objectives");

document.dispatch("click", { target: createTrigger });
tileName.value = "Inside padding stays";
createDialog.dispatch("click", { target: createDialog, clientX: 250, clientY: 250 });
assert.equal(createDialog.open, true, "inside-padding click is not treated as a backdrop dismissal");
createDialog.dispatch("pointerdown", { target: createDialog, clientX: 10, clientY: 10 });
createDialog.dispatch("click", { target: createDialog, clientX: 10, clientY: 10 });
assert.equal(discard.hidden, false, "genuine backdrop click uses the dirty close guard");
discardConfirm.dispatch("click");
assert.equal(createDialog.open, false, "discarding after a backdrop request closes the editor");

document.dispatch("click", { target: createTrigger });
tileName.value = "Retained draft";
document.dispatch("click", { target: editTrigger });
assert.equal(tileName.value, "Retained draft", "tile switch preserves dirty inputs until Discard");
assert.equal(discard.hidden, false);
keep.dispatch("click");
for (const properties of [{ target: "_blank" }, { target: "catalogue" }, { download: true }, { ctrlKey: true }, { metaKey: true }, { shiftKey: true }, { altKey: true }, { button: 1 }]) {
  const link = new Node("a");
  link.href = "/Admin/Catalogue";
  link.target = properties.target || "";
  if (properties.download) link.setAttribute("download", "");
  const event = document.dispatch("click", { ...properties, target: link });
  assert.equal(event.defaultPrevented, false, "noncurrent navigation retains native behavior");
}
const currentLink = new Node("a"); currentLink.href = "/Admin";
assert.equal(document.dispatch("click", { target: currentLink }).defaultPrevented, true, "current-tab navigation remains guarded");
keep.dispatch("click");
createDialog.dispatch("pointerdown", { target: createDialog, clientX: 250, clientY: 250 });
createDialog.dispatch("click", { target: createDialog, clientX: 10, clientY: 10 });
assert.equal(discard.hidden, true, "inside-to-outside selection never opens discard");
window.location = { href: "https://example.test/Admin/Events/Board/1" };
context.capturePostNavigationState = () => ({});
context.rememberPostNavigationState = () => true;
let installed = false;
document.open = () => {};
document.write = () => { installed = true; };
document.close = () => {};
let outcome = "failed";
let version = null;
context.DOMParser = class { parseFromString() { return { querySelector: selector => selector === ".board-page" ? { dataset: { boardTileOutcome: outcome, boardVersion: version } } : { textContent: "Server warning" } }; } };
(async () => {
  const file = { name: "kept.png", size: 12, type: "image/png", lastModified: 4 };
  image.value = file;
  let finishResponse;
  context.fetch = (action, options) => {
    assert.equal(options.body.get(image.name), file, "File captured before controls are disabled");
    return new Promise(resolve => { finishResponse = () => resolve({ ok: true, url: window.location.href, text: async () => "html" }); });
  };
  const submit = () => form.listeners.submit[0]({ currentTarget: form, preventDefault() {} });
  const pending = submit();
  createCancel.dispatch("click");
  assert.equal(createDialog.open, true, "pending async save blocks dismissal");
  await submit();
  finishResponse(); await pending;
  assert.equal(image.value, file, "failed response keeps the original File");
  assert.equal(tileName.value, "Retained draft");
  const feedback = createDialog.querySelector('[data-board-editor-feedback]');
  assert.equal(feedback.textContent, "Server warning", "failed response uses the server explanation");
  const repeated = submit(); finishResponse(); await repeated;
  assert.equal(createDialog.querySelectorAll('[data-board-editor-feedback]').length, 1, "repeated failure reuses feedback");
  assert.equal(createDialog.querySelectorAll('[data-board-reload]').length, 1, "repeated failure reuses reload");
  assert.equal(installed, false, "failed POST never replaces the live form");
  outcome = "committed";
  document.write = () => { installed = true; throw new Error("installation failed"); };
  const success = submit(); finishResponse(); await success;
  assert.equal(installed, true, "explicit committed outcome attempts full authoritative installation even with a warning");
  assert.equal(guard.dirtyForms().length, 0, "committed recovery establishes baseline after controls are restored");
  const completedUnload = { preventDefault() { this.prevented = true; } };
  windowListeners.beforeunload.forEach(listener => listener(completedUnload));
  assert.equal(completedUnload.prevented, undefined, "already saved work does not prompt on native departure");
  context.fetch = () => { throw new Error("confirmed save must never resubmit"); };
  await submit();
  assert.match(feedback.textContent, /changes were saved/);
  // The stale branch must select retained-input wording instead of a loaded-page server message.
  assert.match(inlineScript, /tileSaveBlocked\s*\?\s*"This record changed\. Your edits are still here\. Reload current values before saving again\."/, "stale response uses retained-input recovery rather than server loaded-page wording");
})().catch(error => { console.error(error); process.exitCode = 1; });

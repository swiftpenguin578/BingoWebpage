const assert = require("node:assert/strict");
const fs = require("node:fs");
const path = require("node:path");
const vm = require("node:vm");

const root = path.join(__dirname, "../..");
const css = [
  fs.readFileSync(path.join(root, "src/Bingo.Web/wwwroot/css/site.transitional.foundation.css"), "utf8"),
  fs.readFileSync(path.join(root, "src/Bingo.Web/wwwroot/css/site.public-ui.css"), "utf8"),
  fs.readFileSync(path.join(root, "src/Bingo.Web/wwwroot/css/site.transitional.application.css"), "utf8")
].join("\n");
const script = fs.readFileSync(path.join(root, "src/Bingo.Web/wwwroot/js/public-evidence.js"), "utf8");

assert.match(css, /\.public-lightbox:not\(\[open\]\) \{ display: none; \}/, "closed evidence dialogs stay out of document flow");
assert.match(css, /\.public-lightbox\[open\] \{ display: grid; \}/, "open evidence dialogs use the image-only layout");
assert.match(css, /\.public-ui-viewport-close \{ position: fixed;[\s\S]*width: 44px; height: 44px;[\s\S]*background: transparent; border: 0;/, "evidence close uses the shared PublicUi viewport primitive");
assert.match(css, /\.public-ui-recent-drop-footer[^\n]*margin-top: 0\.75rem/, "recent-drop pagination divider has breathing room");
assert.match(script, /dialog\.addEventListener\("close", clearImage\)/, "closing clears the enlarged evidence image");
assert.match(script, /if \(dialog\.open\) dialog\.close\(\);/, "reload recovery closes a restored open dialog");
assert.match(script, /document\.addEventListener\("click"[\s\S]*viewer\.open\(trigger\)/, "delegated evidence clicks support dynamically inserted board sidebars and previews");
assert.match(script, /updateMetadata\(trigger\)/, "evidence opening refreshes submission metadata");
assert.match(script, /data-evidence-dialog-drop[\s\S]*trigger\.dataset\.evidenceDrop[\s\S]*data-evidence-dialog-source/, "shared evidence viewer preserves submission metadata");
assert.doesNotMatch(script, /data-evidence-zoom-in|data-evidence-zoom-out|data-evidence-zoom-reset/, "accepted evidence interaction does not require a separate control bar");
assert.match(script, /pointerdown[\s\S]*pointermove[\s\S]*pinchDistance/, "evidence viewers support bounded pointer and touch pan/zoom");
assert.match(script, /event\.key === "ArrowLeft"[\s\S]*event\.key === "ArrowDown"/, "evidence viewers support keyboard panning");
assert.match(css, /public-evidence-viewer__viewport[\s\S]*touch-action: none/, "public evidence viewports accept practical touch gestures");
assert.match(css, /admin-review-lightbox__viewport[\s\S]*touch-action: none/, "admin evidence viewports accept practical touch gestures");
assert.match(css, /html\[data-public-theme="dark"\] body\.public-event-shell\.public-ui-pass1 \.public-lightbox::backdrop \{[^}]*background: color-mix\(in srgb, #000 72%, transparent\);[^}]*backdrop-filter: none;[^}]*\}[\s\S]*html\[data-public-theme="dark"\] body\.public-event-shell\.public-ui-pass1 \.public-evidence-viewer,[\s\S]*\.public-evidence-viewer__figure,[\s\S]*\.public-evidence-viewer__meta \{[^}]*background: var\(--board-surface\);[^}]*filter: none;/, "dark Public UI evidence lightboxes use a neutral translucent backdrop and continuous Board surface");

class RuntimeElement {
  constructor() {
    this.listeners = {};
    this.style = {};
    this.classList = { values: new Set(), toggle: (name, enabled) => enabled ? this.classList.values.add(name) : this.classList.values.delete(name) };
    this.dataset = {};
    this.parentElement = null;
    this.offsetWidth = 800;
    this.clientWidth = 400;
    this.clientHeight = 300;
  }
  addEventListener(name, handler) { (this.listeners[name] ??= []).push(handler); }
  dispatch(name, extras = {}) {
    const event = { type: name, target: this, defaultPrevented: false, preventDefault() { this.defaultPrevented = true; }, ...extras };
    for (const handler of this.listeners[name] ?? []) handler(event);
    return event;
  }
  replaceChildren(...children) { this.children = children; }
  removeAttribute(name) { if (name === "src") this.src = ""; }
  getBoundingClientRect() { return { left: 100, top: 50, width: this.offsetWidth, height: this.offsetHeight }; }
}

const dialog = new RuntimeElement();
const image = new RuntimeElement();
image.offsetHeight = 600;
const viewport = new RuntimeElement();
const close = new RuntimeElement();
const metadata = new Map([
  ["[data-evidence-dialog-image]", image],
  ["[data-evidence-viewport]", viewport],
  ["[data-evidence-close]", close]
]);
for (const selector of ["[data-evidence-dialog-drop]", "[data-evidence-dialog-player]", "[data-evidence-dialog-team]", "[data-evidence-dialog-source]"])
  metadata.set(selector, new RuntimeElement());
dialog.querySelector = selector => metadata.get(selector) ?? null;
image.parentElement = viewport;
dialog.dataset.evidenceEmpty = "No evidence";
dialog.dataset.evidenceDefaultSource = "Unknown";
dialog.showModal = () => { dialog.open = true; };
dialog.close = () => { dialog.open = false; dialog.dispatch("close"); };
dialog.open = true;
image.src = "/restored-evidence.png";
const trigger = new RuntimeElement();
trigger.dataset.evidenceImage = "/evidence.png";
trigger.dataset.evidenceAlt = "Evidence";
Object.assign(trigger.dataset, { evidenceDrop: "Test drop", evidencePlayer: "Test player", evidenceTeam: "Test team", evidenceSource: "Test source" });
trigger.closest = selector => selector === "[data-evidence-image]" ? trigger : null;
const document = {
  listeners: {},
  querySelectorAll: selector => selector === "[data-evidence-dialog]" ? [dialog] : [],
  addEventListener(name, handler) { (this.listeners[name] ??= []).push(handler); },
  createTextNode: text => ({ textContent: text })
};
vm.runInNewContext(script, { document });
assert.equal(dialog.open, false, "initialization closes a restored dialog");
assert.equal(image.src, "", "reload recovery clears the restored image");
document.listeners.click[0]({ target: trigger, preventDefault() {} });
assert.equal(dialog.open, true, "evidence trigger opens the runtime dialog");
assert.equal(image.src, "/evidence.png", "delegated opening uses the selected evidence image");
assert.equal(image.alt, "Evidence", "delegated opening retains the image description");
for (const [field, value] of [["drop", "Test drop"], ["player", "Test player"], ["team", "Test team"], ["source", "Test source"]])
  assert.equal(metadata.get(`[data-evidence-dialog-${field}]`).children[0].textContent, value, `opening updates ${field} metadata`);

const pointer = (type, pointerId, clientX, clientY) => viewport.dispatch(type, { pointerId, clientX, clientY });
const tap = (x, y) => { pointer("pointerdown", 1, x, y); pointer("pointerup", 1, x, y); };
const key = value => {
  assert.equal(image.dispatch("keydown", { key: value }).defaultPrevented, true, `${value} handles keyboard interaction`);
};
tap(300, 200);
assert.equal(image.style.transform, "translate3d(200px, 150px, 0) scale(2)", "single-pointer click magnifies around the clicked image point");
tap(300, 200);
assert.equal(image.style.transform, "", "second click restores the image");
key("Enter");
assert.match(image.style.transform, /scale\(2\)/, "Enter toggles magnification");
key(" ");
assert.equal(image.style.transform, "", "Space toggles back to the unzoomed image");
key("+");
assert.match(image.style.transform, /scale\(1\.5\)/, "keyboard zoom increases the image scale");
key("ArrowLeft");
key("ArrowDown");
assert.equal(image.style.transform, "translate3d(-40px, 40px, 0) scale(1.5)", "keyboard arrows pan the zoomed image");
key("ArrowRight");
key("ArrowUp");
assert.equal(image.style.transform, "translate3d(0px, 0px, 0) scale(1.5)", "opposite arrows reverse the pan");
key("-");
assert.equal(image.style.transform, "", "zooming out to one resets the pan");
key("Enter");
pointer("pointerdown", 1, 300, 200);
pointer("pointermove", 1, 330, 220);
pointer("pointerup", 1, 330, 220);
assert.equal(image.style.transform, "translate3d(30px, 20px, 0) scale(2)", "drag pans without toggling zoom on release");
pointer("pointerdown", 1, 330, 220);
pointer("pointermove", 1, 10000, -10000);
pointer("pointerup", 1, 10000, -10000);
assert.equal(image.style.transform, "translate3d(616px, -466px, 0) scale(2)", "pointer pan is clamped to image and viewport bounds");
key("0");
pointer("pointerdown", 1, 200, 200);
pointer("pointerdown", 2, 300, 200);
pointer("pointermove", 2, 400, 200);
pointer("pointerup", 2, 400, 200);
pointer("pointerup", 1, 200, 200);
assert.match(image.style.transform, /scale\(2\)/, "two touch pointers pinch to zoom without a release-click toggle");
assert.equal(viewport.dispatch("wheel", { deltaY: -1 }).defaultPrevented, true, "wheel zoom consumes native scrolling");
assert.match(image.style.transform, /scale\(2\.25\)/, "wheel zoom changes the same scale");
for (let index = 0; index < 10; index++) key("+");
assert.match(image.style.transform, /scale\(4\)/, "zoom cannot exceed the maximum scale");
for (let index = 0; index < 10; index++) key("-");
assert.equal(image.style.transform, "", "zoom cannot shrink below the original scale");
pointer("pointerdown", 1, 300, 200);
pointer("pointercancel", 1, 300, 200);
assert.equal(image.style.transform, "", "cancelled gestures do not act as clicks");
key("Enter");
close.dispatch("click");
assert.equal(dialog.open, false, "close control closes the dialog");
assert.equal(image.style.transform, "", "closing the runtime dialog resets the transform");
assert.equal(image.classList.values.has("is-zoomed"), false, "closing the runtime dialog clears zoom state");
assert.equal(image.src, "", "closing the runtime dialog clears the image source");
assert.equal(image.alt, "", "closing clears the previous image description");
assert.equal(metadata.get("[data-evidence-dialog-drop]").children[0].textContent, "No evidence", "closing resets metadata");

const insertedTrigger = new RuntimeElement();
insertedTrigger.dataset = { evidenceImage: "/inserted-evidence.png", evidenceAlt: "Inserted evidence" };
// A newly inserted child delegates to its newly inserted evidence trigger.
const insertedChild = { closest: selector => selector === "[data-evidence-image]" ? insertedTrigger : null };
document.listeners.click[0]({ target: insertedChild, preventDefault() {} });
assert.equal(image.src, "/inserted-evidence.png", "delegation opens evidence inserted after initialization");
assert.equal(image.style.transform, "", "new evidence starts with a reset transform");
assert.equal(metadata.get("[data-evidence-dialog-source]").children[0].textContent, "Unknown", "new evidence without source does not retain old metadata");
dialog.dispatch("click");
assert.equal(dialog.open, false, "backdrop click closes the evidence dialog");

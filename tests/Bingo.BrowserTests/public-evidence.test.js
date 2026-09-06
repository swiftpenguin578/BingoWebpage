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
assert.match(script, /data-evidence-zoom-in[\s\S]*data-evidence-zoom-out[\s\S]*data-evidence-zoom-reset/, "evidence viewers expose zoom controls");
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
  dispatch(name, event = {}) { for (const handler of this.listeners[name] ?? []) handler(event); }
  replaceChildren(...children) { this.children = children; }
  removeAttribute(name) { if (name === "src") this.src = ""; }
}

const dialog = new RuntimeElement();
const image = new RuntimeElement();
image.offsetHeight = 600;
const viewport = new RuntimeElement();
const close = new RuntimeElement();
const zoomIn = new RuntimeElement();
const zoomOut = new RuntimeElement();
const zoomReset = new RuntimeElement();
const metadata = new Map([
  ["[data-evidence-dialog-image]", image],
  ["[data-evidence-viewport]", viewport],
  ["[data-evidence-close]", close],
  ["[data-evidence-zoom-in]", zoomIn],
  ["[data-evidence-zoom-out]", zoomOut],
  ["[data-evidence-zoom-reset]", zoomReset]
]);
for (const selector of ["[data-evidence-dialog-drop]", "[data-evidence-dialog-player]", "[data-evidence-dialog-team]", "[data-evidence-dialog-source]"])
  metadata.set(selector, new RuntimeElement());
dialog.querySelector = selector => metadata.get(selector) ?? null;
image.parentElement = viewport;
dialog.dataset.evidenceEmpty = "No evidence";
dialog.dataset.evidenceDefaultSource = "Unknown";
dialog.showModal = () => { dialog.open = true; };
dialog.close = () => { dialog.open = false; dialog.dispatch("close"); };
dialog.open = false;
const trigger = new RuntimeElement();
trigger.dataset.evidenceImage = "/evidence.png";
trigger.dataset.evidenceAlt = "Evidence";
trigger.closest = () => trigger;
const document = {
  listeners: {},
  querySelectorAll: selector => selector === "[data-evidence-dialog]" ? [dialog] : [],
  addEventListener(name, handler) { (this.listeners[name] ??= []).push(handler); },
  createTextNode: text => ({ textContent: text })
};
vm.runInNewContext(script, { document });
document.listeners.click[0]({ target: trigger, preventDefault() {} });
assert.equal(dialog.open, true, "evidence trigger opens the runtime dialog");
zoomIn.dispatch("click");
assert.match(image.style.transform, /scale\(1\.5\)/, "runtime zoom changes the image transform");
dialog.close();
assert.equal(image.style.transform, "", "closing the runtime dialog resets the transform");
assert.equal(image.classList.values.has("is-zoomed"), false, "closing the runtime dialog clears zoom state");
assert.equal(image.src, "", "closing the runtime dialog clears the image source");

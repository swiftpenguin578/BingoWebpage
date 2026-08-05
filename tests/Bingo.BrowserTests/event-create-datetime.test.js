const assert = require("node:assert/strict");
const dateTime = require("../../src/Bingo.Web/wwwroot/js/event-create-datetime.js");
const validation = require("../../src/Bingo.Web/wwwroot/js/event-create-validation.js");

class Input {
  constructor(value = "") { this.value = value; this.defaultValue = ""; this.tabIndex = 0; this.attributes = {}; this.listeners = {}; }
  addEventListener(type, listener) { (this.listeners[type] ??= []).push(listener); }
  dispatchEvent(event) { for (const listener of this.listeners[event.type] ?? []) listener(event); }
  setAttribute(name, value) { this.attributes[name] = value; }
  getAttribute(name) { return this.attributes[name] ?? null; }
}

class Control {
  constructor(value = "") {
    this.canonical = new Input(value);
    this.date = new Input();
    this.time = new Input();
  }
  querySelector(selector) {
    return selector.includes("canonical") ? this.canonical : selector.includes("date]") ? this.date : this.time;
  }
}

global.Event ??= class Event { constructor(type) { this.type = type; } };

const control = new Control();
let updates = 0;
control.canonical.addEventListener("input", () => updates++);
assert.equal(control.canonical.tabIndex, 0);
assert.equal(control.canonical.getAttribute("aria-hidden"), null);
dateTime.initializeControl(control);
assert.equal(control.canonical.tabIndex, -1);
assert.equal(control.canonical.getAttribute("aria-hidden"), "true");
assert.equal(control.time.value, "12:30");
assert.equal(control.canonical.value, "");
const existing = new Control("2026-08-12T16:45");
dateTime.initializeControl(existing);
assert.equal(existing.time.value, "16:45");
assert.equal(existing.canonical.value, "2026-08-12T16:45");
control.date.value = "2026-08-10";
control.date.dispatchEvent(new Event("change"));
assert.equal(control.canonical.value, "2026-08-10T12:30");
assert.equal(dateTime.format(control.canonical.value), "10/08/2026 12:30");
assert.ok(updates > 0);

control.time.value = "14:05";
control.time.dispatchEvent(new Event("input"));
assert.equal(control.canonical.value, "2026-08-10T14:05");
control.date.value = "";
control.date.dispatchEvent(new Event("change"));
assert.equal(control.canonical.value, "");
assert.equal(control.time.value, "14:05");
control.date.value = "2026-08-11";
control.date.dispatchEvent(new Event("change"));
control.time.value = "";
control.time.dispatchEvent(new Event("input"));
assert.equal(control.canonical.value, "");

const panel = { dataset: { createPanel: "0" } };
const invalidName = {
  closest: () => panel,
  focus: (options) => { invalidName.focusOptions = options; }
};
const summary = { textContent: "Required" };
const form = {
  querySelector: (selector) => selector.startsWith(".input-validation-error") ? invalidName : summary
};
const validator = { settings: { ignore: ":hidden" } };
const jqueryForm = {
  data: () => validator,
  valid: () => {
    assert.equal(validator.settings.ignore, ":hidden:not([name])");
    return false;
  }
};
let revealedPanel;
assert.equal(validation.validateAndReveal(form, () => jqueryForm, (index) => { revealedPanel = index; }), false);
assert.equal(revealedPanel, 0);
assert.equal(invalidName.focusOptions.preventScroll, true);
assert.equal(summary.textContent, "Required");

const requiredName = {
  required: true,
  attributes: new Set(["required"]),
  hasAttribute(name) { return this.attributes.has(name); },
  classList: { contains: () => true },
  getAttribute: () => "true"
};
const step0 = {
  querySelectorAll: () => [requiredName],
  querySelector: () => requiredName
};
const step1 = { querySelectorAll: () => [], querySelector: () => null };
let nameValue = "";
let focused = false;
requiredName.focus = (options) => { focused = options.preventScroll; };
const stepForm = { querySelector: () => summary };
const stepJquery = (target) => ({
  valid: () => target === stepForm ? true : nameValue.length > 0
});
const panels = [step0, step1];
let activeStep = 0;
let revealed = [];
const reveal = (index) => { activeStep = index; revealed.push(index); };
const validate = (panel) => validation.validateCurrentStep(stepForm, panel, stepJquery);

assert.equal(validation.navigateForward(0, 2, panels, reveal, validate), 0);
assert.equal(activeStep, 0);
assert.equal(focused, true);
nameValue = "Community bingo";
focused = false;
revealed = [];
assert.equal(validation.navigateForward(0, 2, panels, reveal, validate), 2);
assert.deepEqual(revealed, [0, 1]);
assert.equal(validator.settings.ignore, ":hidden");

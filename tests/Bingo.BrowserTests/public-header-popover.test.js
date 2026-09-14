const assert = require("node:assert/strict");
const fs = require("node:fs");
const path = require("node:path");
const vm = require("node:vm");

const repositoryRoot = path.resolve(__dirname, "../..");
const siteScript = fs.readFileSync(path.join(repositoryRoot, "src/Bingo.Web/wwwroot/js/site.js"), "utf8");
const layoutMarkup = fs.readFileSync(path.join(repositoryRoot, "src/Bingo.Web/Pages/Shared/_Layout.cshtml"), "utf8");
const catalogueMarkup = fs.readFileSync(path.join(repositoryRoot, "src/Bingo.Web/Pages/Admin/PublicUi.cshtml"), "utf8");
const publicInboxScript = fs.readFileSync(path.join(repositoryRoot, "src/Bingo.Web/wwwroot/js/notification-inbox.js"), "utf8");

assert.doesNotMatch(layoutMarkup, /class="[^"]*\b(?:nav-panel|notification-panel|notification-list|notification-item|settings-panel|settings-popover)\b/, "public layout no longer uses legacy popover visual classes");
assert.match(layoutMarkup, /<header class="landing-shell-header">[\s\S]*landing-shell-primary[\s\S]*landing-shell-actions/, "the public header uses one shared landing shell class tree");
assert.match(layoutMarkup, /data-public-ui-notification-inbox[\s\S]*public-ui-header-popover__panel--notifications/, "public notifications use the shared header popover family");
assert.match(layoutMarkup, /User\.IsInRole\("Admin"\) \|\| User\.IsInRole\("SuperAdmin"\)[\s\S]*Personal \{0\} · Admin \{1\}[\s\S]*else[\s\S]*Personal \{0\}/, "notification metadata hides the Admin count from ordinary users");
assert.match(layoutMarkup, /public-ui-header-popover__panel--settings[\s\S]*public-ui-control-visual public-ui-control-visual--select/, "public settings use the shared PublicUi select control");
assert.match(layoutMarkup, /data-public-theme-control[\s\S]*@T\["Light"\][\s\S]*@T\["Dark"\]/, "public settings expose the localized local theme control");
assert.match(siteScript, /data-public-theme-control[\s\S]*bingo:public-theme/, "public theme preference is applied locally without a server state");
assert.match(siteScript, /function initializePublicHeaderPopovers[\s\S]*bingo:content-updated/, "public header popovers can be initialized after enhanced navigation");
assert.match(catalogueMarkup, /public-ui-header-popover__panel--notifications[\s\S]*@T\["Personal \{0\}",\s*3\][\s\S]*public-ui-header-popover__panel--settings/, "catalogue demonstrates the ordinary-user notification metadata state and settings popover");
assert.match(publicInboxScript, /publicInbox \? 'public-ui-header-popover__item' : 'notification-item'/, "realtime public notifications use the new item class while Admin keeps its legacy class");

class FakeEventTarget {
    constructor() { this.listeners = {}; }
    addEventListener(type, listener) {
        (this.listeners[type] ??= new Set()).add(listener);
    }
    dispatchEvent(event) {
        for (const listener of this.listeners[event.type] ?? []) listener(event);
    }
}

class FakeMenu extends FakeEventTarget {
    constructor(publicDisclosure) {
        super();
        this.open = false;
        this.publicDisclosure = publicDisclosure;
        this.focused = false;
        this.dataset = {};
        this.panel = { hidden: true };
        this.trigger = Object.assign(new FakeEventTarget(), {
            expanded: "false",
            getAttribute: name => name === "aria-expanded" ? this.trigger.expanded : null,
            setAttribute: (name, value) => {
                assert.equal(name, "aria-expanded");
                this.trigger.expanded = value;
            },
            focus: () => { this.focused = true; }
        });
    }

    matches(selector) { return selector === "[data-public-ui-popover]" && this.publicDisclosure; }
    closest() { return null; }
    contains(target) { return target === this || target === this.trigger || target === this.panel; }
    querySelector(selector) {
        if (selector === "summary") return { focus: () => { this.focused = true; } };
        if (!this.publicDisclosure) return null;
        if (selector === "button[aria-controls]") return this.trigger;
        if (selector === "[data-public-ui-popover-panel]") return this.panel;
        return null;
    }
    click() {
        this.trigger.dispatchEvent({ type: "click", target: this.trigger });
        document.dispatchEvent({ type: "click", target: this.trigger });
    }
    keydown(key) { this.dispatchEvent({ type: "keydown", key, preventDefault() {} }); }
}

const burger = new FakeMenu(true);
const notifications = new FakeMenu(true);
const settings = new FakeMenu(true);
const legacy = new FakeMenu(false);
const menus = [burger, notifications, settings, legacy];
const document = Object.assign(new FakeEventTarget(), {
    documentElement: { dataset: {} },
    querySelectorAll(selector) {
        assert.equal(selector, ".nav-popover details, [data-public-ui-popover]");
        return menus;
    }
});
const outside = { closest: () => null };
const managerStart = siteScript.indexOf("function initializePublicHeaderPopovers()");
const managerEnd = siteScript.indexOf("\n}\n\nfunction initializePublicTheme", managerStart) + 2;
const refreshRegistration = siteScript.match(/document\.addEventListener\("bingo:content-updated", initializePublicHeaderPopovers\);/);
assert.ok(refreshRegistration, "content updates register the public header initializer");
vm.runInNewContext(`${siteScript.slice(managerStart, managerEnd)} ${refreshRegistration[0]} initializePublicHeaderPopovers();`, {
    document,
    dismissTransientToast() {}
});

function assertOpen(menu, expected, message) {
    assert.equal(menu.trigger.expanded, String(expected), `${message}: expanded state`);
    assert.equal(menu.panel.hidden, !expected, `${message}: panel visibility`);
}

burger.click();
assertOpen(burger, true, "the burger opens");
notifications.click();
assertOpen(burger, false, "opening a public disclosure closes the burger");
assertOpen(notifications, true, "the newly opened public disclosure remains open");

document.dispatchEvent({ type: "click", target: outside });
assertOpen(notifications, false, "clicking outside closes the open public disclosure");

settings.click();
settings.keydown("Escape");
assertOpen(settings, false, "Escape closes the public disclosure");
assert.equal(settings.focused, true, "Escape returns focus to the disclosure trigger");

burger.click();
const added = new FakeMenu(true);
menus.push(added);
for (let update = 0; update < 2; update++) {
    document.dispatchEvent({ type: "bingo:content-updated" });
}
assertOpen(burger, true, "reinitialization preserves an existing open disclosure");
for (const menu of [burger, notifications, settings, added]) {
    assert.equal(menu.trigger.listeners.click.size, 1, "each trigger has exactly one click listener");
    assert.equal(menu.listeners.keydown.size, 1, "each disclosure has exactly one key listener");
}
assert.equal(document.listeners.click.size, 1, "reinitialization retains one outside-click listener");

added.click();
assertOpen(burger, false, "a newly inserted disclosure closes an existing one");
assertOpen(added, true, "a newly inserted disclosure opens on one click");
burger.click();
assertOpen(added, false, "an existing disclosure closes a newly inserted one");
assertOpen(burger, true, "an existing disclosure still opens on one click");
burger.click();
assertOpen(burger, false, "an existing disclosure still closes on one click");
added.click();
document.dispatchEvent({ type: "click", target: outside });
assertOpen(added, false, "the existing outside-click manager handles a newly inserted disclosure");
added.click();
added.keydown("Escape");
assertOpen(added, false, "Escape closes a newly inserted disclosure");
assert.equal(added.focused, true, "Escape focuses the newly inserted disclosure trigger");

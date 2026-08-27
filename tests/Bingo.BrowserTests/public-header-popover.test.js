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
assert.doesNotMatch(siteScript, /const transientToastTypes/, "site script can be evaluated again without a duplicate lexical binding");
assert.match(catalogueMarkup, /public-ui-header-popover__panel--notifications[\s\S]*Personal 3[\s\S]*public-ui-header-popover__panel--settings/, "catalogue demonstrates the ordinary-user notification metadata state and settings popover");
assert.match(publicInboxScript, /publicInbox \? 'public-ui-header-popover__item' : 'notification-item'/, "realtime public notifications use the new item class while Admin keeps its legacy class");

class FakeMenu {
    constructor(publicDisclosure) {
        this.open = false;
        this.publicDisclosure = publicDisclosure;
        this.listeners = {};
        this.focused = false;
        this.dataset = {};
    }

    addEventListener(type, listener) { this.listeners[type] = listener; }
    matches(selector) { return selector === "[data-public-ui-popover]" && this.publicDisclosure; }
    closest() { return null; }
    contains(target) { return target === this; }
    querySelector(selector) {
        return selector === "summary" ? { focus: () => { this.focused = true; } } : null;
    }
    toggle() { this.listeners.toggle?.(); }
    keydown(key) { this.listeners.keydown?.({ key, preventDefault() {} }); }
}

const burger = new FakeMenu(true);
const notifications = new FakeMenu(true);
const settings = new FakeMenu(true);
const legacy = new FakeMenu(false);
const listeners = {};
const document = {
    documentElement: { dataset: {} },
    querySelectorAll(selector) {
        assert.equal(selector, ".nav-popover details, details[data-public-ui-popover]");
        return [burger, notifications, settings, legacy];
    },
    addEventListener(type, listener) { listeners[type] = listener; }
};
const outside = { closest: () => null };
const managerStart = siteScript.indexOf("function initializePublicHeaderPopovers()");
const managerEnd = siteScript.indexOf("\n}\n\nfunction initializePublicTheme", managerStart) + 2;
vm.runInNewContext(`${siteScript.slice(managerStart, managerEnd)} initializePublicHeaderPopovers();`, {
    document,
    dismissTransientToast() {}
});

burger.open = true;
burger.toggle();
notifications.open = true;
notifications.toggle();
assert.equal(burger.open, false, "opening a public disclosure closes the burger");
assert.equal(notifications.open, true, "the newly opened public disclosure remains open");

listeners.click({ target: outside });
assert.equal(notifications.open, false, "clicking outside closes the open public disclosure");

settings.open = true;
settings.keydown("Escape");
assert.equal(settings.open, false, "Escape closes the public disclosure");
assert.equal(settings.focused, true, "Escape returns focus to the disclosure summary");
